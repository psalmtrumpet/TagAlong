using Microsoft.Extensions.Logging;

namespace TagAlong.BackgroundJobs.Jobs;

public interface IPackageRequestExpirationService
{
    Task ExpireOldPackageRequestsAsync(CancellationToken cancellationToken);
}

public class PackageRequestExpirationJob : IRecurringJob
{
    private readonly IPackageRequestExpirationService _expirationService;
    private readonly ILogger<PackageRequestExpirationJob> _logger;

    public PackageRequestExpirationJob(
        IPackageRequestExpirationService expirationService,
        ILogger<PackageRequestExpirationJob> logger)
    {
        _expirationService = expirationService;
        _logger = logger;
    }

    public string JobId => "package-request-expiration-job";
    public string CronExpression => "0 */6 * * *"; // Every 6 hours

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting package request expiration job");
        await _expirationService.ExpireOldPackageRequestsAsync(cancellationToken);
        _logger.LogInformation("Package request expiration job completed");
    }
}
