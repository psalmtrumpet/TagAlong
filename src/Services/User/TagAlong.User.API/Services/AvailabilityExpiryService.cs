using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Services;

/// <summary>
/// Takes users offline once their availability window has run out, so they
/// stop showing as available (users on a trip are left alone).
/// </summary>
public class AvailabilityExpiryService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AvailabilityExpiryService> _logger;

    public AvailabilityExpiryService(IServiceScopeFactory scopes, ILogger<AvailabilityExpiryService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
                await repo.ExpireStaleAvailabilityAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Availability expiry run failed");
            }

            await Task.Delay(Interval, stoppingToken).ContinueWith(_ => { });
        }
    }
}
