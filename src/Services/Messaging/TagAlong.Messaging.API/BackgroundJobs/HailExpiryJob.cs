using TagAlong.Messaging.API.Hails;

namespace TagAlong.Messaging.API.BackgroundJobs;

/// <summary>Closes on-demand ride requests nobody accepted in time and tells the passenger.</summary>
public class HailExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<HailExpiryJob> _logger;

    public HailExpiryJob(IServiceScopeFactory scopes, ILogger<HailExpiryJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<HailService>().ExpireDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Ride request expiry failed");
            }
        }
    }
}
