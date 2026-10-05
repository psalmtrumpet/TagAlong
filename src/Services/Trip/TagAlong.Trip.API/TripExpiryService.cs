using Microsoft.EntityFrameworkCore;
using TagAlong.EventBus;
using TagAlong.Trip.API.Commands;
using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.API;

/// <summary>
/// Closes out trips nobody finished:
///  - Scheduled trips whose departure passed more than 6 h ago are cancelled.
///  - InProgress trips that departed more than 24 h ago are completed.
/// Publishes TripStatusChanged so Messaging closes the trip's unstarted bookings.
/// </summary>
public class TripExpiryService : BackgroundService
{
    private static readonly TimeSpan ScheduledGrace = TimeSpan.FromHours(6);
    private static readonly TimeSpan InProgressGrace = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TripExpiryService> _logger;

    public TripExpiryService(IServiceScopeFactory scopeFactory, ILogger<TripExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireTripsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Trip expiry: run failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task ExpireTripsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var now = DateTime.UtcNow;
        var scheduledCutoff = now - ScheduledGrace;
        var inProgressCutoff = now - InProgressGrace;

        var stale = await db.Trips
            .Where(t => (t.Status == TripStatus.Scheduled && t.DepartureTime < scheduledCutoff) ||
                        (t.Status == TripStatus.InProgress && t.DepartureTime < inProgressCutoff))
            .ToListAsync(ct);

        if (stale.Count == 0) return;

        var changes = new List<(Domain.Entities.Trip Trip, string OldStatus)>();
        foreach (var trip in stale)
        {
            var old = trip.Status.ToString();
            if (trip.Status == TripStatus.Scheduled) trip.Cancel();
            else trip.Complete();
            changes.Add((trip, old));
        }
        await db.SaveChangesAsync(ct);

        foreach (var (trip, old) in changes)
        {
            await eventBus.PublishAsync(new TripStatusChangedIntegrationEvent(
                trip.Id, trip.TravelerId, old, trip.Status.ToString(), now), ct);
        }

        _logger.LogInformation("Trip expiry: closed {Count} stale trip(s)", changes.Count);
    }
}
