using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TagAlong.Trip.Domain.Entities;
using TagAlong.Trip.Infrastructure.Persistence;

namespace TagAlong.Trip.Infrastructure.Services;

public class TripRouteService : ITripRouteService
{
    private readonly IDbContextFactory<TripDbContext> _dbFactory;
    private readonly GoogleDirectionsClient _directionsClient;
    private readonly ILogger<TripRouteService> _logger;

    public TripRouteService(
        IDbContextFactory<TripDbContext> dbFactory,
        GoogleDirectionsClient directionsClient,
        ILogger<TripRouteService> logger)
    {
        _dbFactory = dbFactory;
        _directionsClient = directionsClient;
        _logger = logger;
    }

    public async Task FetchAndStoreRouteAsync(Guid tripId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId);
        if (trip is null)
            return;

        try
        {
            trip.MarkRouteStatus(TripRouteStatus.Pending);
            db.Trips.Update(trip);
            await db.SaveChangesAsync();

            var routeLine = await _directionsClient.GetRouteAsync(
                trip.OriginLatitude, trip.OriginLongitude,
                trip.DestinationLatitude, trip.DestinationLongitude);

            if (routeLine is null)
            {
                trip.MarkRouteStatus(TripRouteStatus.Failed);
                _logger.LogWarning("No route returned from Directions API for trip {TripId}", tripId);
            }
            else
            {
                trip.SetRoute(routeLine);
                _logger.LogInformation("Route stored for trip {TripId} ({Points} pts)", tripId, routeLine.NumPoints);
            }
        }
        catch (Exception ex)
        {
            trip.MarkRouteStatus(TripRouteStatus.Failed);
            _logger.LogError(ex, "Route fetch failed for trip {TripId}", tripId);
        }

        db.Trips.Update(trip);
        await db.SaveChangesAsync();
    }
}
