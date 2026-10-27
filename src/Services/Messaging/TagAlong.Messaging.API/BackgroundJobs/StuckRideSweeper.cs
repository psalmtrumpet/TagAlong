using MediatR;
using TagAlong.Messaging.API.Commands;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.BackgroundJobs;

/// <summary>
/// Safety net for ending a trip: when a driver ends a trip, on-board rides are
/// completed straight away (TripStatusChangedIntegrationEventHandler). If that
/// failed — a service restarting, a lost event — the passenger would be left
/// "on board" forever. Every few minutes, complete any on-board ride whose
/// trip has already ended, exactly like a drop-off.
/// </summary>
public class StuckRideSweeper : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
    // The passenger ends their own ride; if they haven't this long after reaching their stop, it ends itself
    private static readonly TimeSpan EndAfterDrop = TimeSpan.FromMinutes(10);
    // Give the normal trip-ended handling time to run first
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<StuckRideSweeper> _logger;

    public StuckRideSweeper(IServiceScopeFactory scopes, IHttpClientFactory http, ILogger<StuckRideSweeper> logger)
    {
        _scopes = scopes;
        _http = http;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting up
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogWarning(ex, "Stuck ride sweep failed"); }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var conversations = scope.ServiceProvider.GetRequiredService<IConversationRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // At their stop for 10 min and still "on board": the passenger forgot (or their phone died)
        foreach (var ride in await conversations.GetRidesAtDropSinceAsync(DateTime.UtcNow - EndAfterDrop, ct))
        {
            var ended = await mediator.Send(new MarkDeliveredCommand(ride.Id, ride.TravelerId, BySystem: true), ct);
            if (ended.IsFailure)
                _logger.LogWarning("Couldn't end ride {ConversationId} at its drop-off: {Error}", ride.Id, ended.Error.Message);
            else
                _logger.LogInformation("Ended ride {ConversationId}: at the drop-off for {Minutes} min without the passenger ending it", ride.Id, EndAfterDrop.TotalMinutes);
        }

        var onBoard = (await conversations.GetInProgressRidesUnchangedSinceAsync(DateTime.UtcNow - Grace, ct)).ToList();
        if (onBoard.Count == 0) return;

        foreach (var trip in onBoard.GroupBy(c => c.TripId!.Value))
        {
            if (await TripStatusAsync(trip.Key, ct) is not "completed") continue;
            foreach (var ride in trip)
            {
                var result = await mediator.Send(new MarkDeliveredCommand(ride.Id, ride.TravelerId, BySystem: true), ct);
                if (result.IsFailure)
                    _logger.LogWarning("Couldn't complete stuck ride {ConversationId} (trip {TripId} ended): {Error}", ride.Id, trip.Key, result.Error.Message);
                else
                    _logger.LogInformation("Completed stuck ride {ConversationId}: trip {TripId} had already ended", ride.Id, trip.Key);
            }
        }
    }

    /// <summary>The trip's status in lower case, or null if trip-api couldn't say.</summary>
    private async Task<string?> TripStatusAsync(Guid tripId, CancellationToken ct)
    {
        try
        {
            using var res = await _http.CreateClient("trip-api").GetAsync($"api/trips/{tripId}", ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("status", out var s) ? s.GetString()?.ToLowerInvariant() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't check trip {TripId} for stuck rides", tripId);
            return null;
        }
    }
}
