using Microsoft.EntityFrameworkCore;
using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Infrastructure.Persistence;
using TagAlong.Trip.Infrastructure.Services;

namespace TagAlong.Trip.API;

public class RouteEnrichmentService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RouteEnrichmentService> _logger;

    public RouteEnrichmentService(IServiceScopeFactory scopeFactory, ILogger<RouteEnrichmentService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting before hitting the DB
        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await EnrichPendingTripsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task EnrichPendingTripsAsync(CancellationToken ct)
    {
        List<Guid> ids;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TripDbContext>();
            ids = await db.Trips
                .IgnoreQueryFilters()
                .Where(t => t.RouteStatus == TripRouteStatus.None || t.RouteStatus == TripRouteStatus.Failed)
                .Select(t => t.Id)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Route enrichment: failed to query pending trips");
            return;
        }

        if (ids.Count == 0) return;

        _logger.LogInformation("Route enrichment: {Count} trip(s) need routes", ids.Count);

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var routeService = scope.ServiceProvider.GetRequiredService<ITripRouteService>();
                await routeService.FetchAndStoreRouteAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Route enrichment: error processing trip {TripId}", id);
            }
            // Stay within Google Directions API rate limits (≤ 10 req/s for standard tier)
            await Task.Delay(200, ct);
        }
    }
}
