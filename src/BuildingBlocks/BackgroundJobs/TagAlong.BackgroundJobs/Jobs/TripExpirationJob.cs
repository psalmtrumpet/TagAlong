using Microsoft.Extensions.Logging;

namespace TagAlong.BackgroundJobs.Jobs;

public interface ITripExpirationService
{
    Task ExpireOldTripsAsync(CancellationToken cancellationToken);
}

public class TripExpirationJob : IRecurringJob
{
    private readonly ITripExpirationService _tripExpirationService;
    private readonly ILogger<TripExpirationJob> _logger;

    public TripExpirationJob(ITripExpirationService tripExpirationService, ILogger<TripExpirationJob> logger)
    {
        _tripExpirationService = tripExpirationService;
        _logger = logger;
    }

    public string JobId => "trip-expiration-job";
    public string CronExpression => "0 * * * *"; // Every hour

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting trip expiration job");
        await _tripExpirationService.ExpireOldTripsAsync(cancellationToken);
        _logger.LogInformation("Trip expiration job completed");
    }
}
