using System.Collections.Concurrent;

namespace TagAlong.Messaging.API.Services;

/// <summary>
/// Drivers' positions waiting to be saved. Only the newest per ride matters (it's
/// the "last known position"), so a ride's pending position is simply replaced.
/// Positions the phone buffered while offline arrive late and out of date: one
/// older than what was already saved is ignored, by the phone's own clock.
/// </summary>
public class LocationWriteQueue
{
    public record Pending(double Latitude, double Longitude, long? TimestampMs);

    private readonly ConcurrentDictionary<Guid, Pending> _pending = new();
    private readonly ConcurrentDictionary<Guid, long> _savedUpTo = new();

    public void Enqueue(Guid conversationId, double latitude, double longitude, long? timestampMs)
    {
        if (timestampMs is { } t && _savedUpTo.TryGetValue(conversationId, out var saved) && t < saved) return;
        _pending.AddOrUpdate(conversationId,
            new Pending(latitude, longitude, timestampMs),
            (_, old) => old.TimestampMs is { } o && timestampMs is { } n && n < o ? old : new Pending(latitude, longitude, timestampMs));
    }

    /// <summary>Takes everything waiting (each ride's newest position).</summary>
    public List<(Guid ConversationId, Pending Position)> Drain()
    {
        var taken = new List<(Guid, Pending)>();
        foreach (var id in _pending.Keys)
        {
            if (!_pending.TryRemove(id, out var p)) continue;
            if (p.TimestampMs is { } t) _savedUpTo.AddOrUpdate(id, t, (_, old) => Math.Max(old, t));
            taken.Add((id, p));
        }
        return taken;
    }
}

/// <summary>Saves queued driver positions about once a second, off the live path.</summary>
public class LocationWriteService : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(1);

    private readonly LocationWriteQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LocationWriteService> _logger;

    public LocationWriteService(LocationWriteQueue queue, IServiceScopeFactory scopes, ILogger<LocationWriteService> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var (id, p) in _queue.Drain())
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<LocationRelay>().PersistAsync(id, p.Latitude, p.Longitude);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Couldn't save the driver's position for {ConversationId}", id);
                }
            }
            await Task.Delay(Every, stoppingToken).ContinueWith(_ => { });
        }
    }
}
