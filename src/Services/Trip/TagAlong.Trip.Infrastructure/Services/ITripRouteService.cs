namespace TagAlong.Trip.Infrastructure.Services;

public interface ITripRouteService
{
    Task FetchAndStoreRouteAsync(Guid tripId);
}
