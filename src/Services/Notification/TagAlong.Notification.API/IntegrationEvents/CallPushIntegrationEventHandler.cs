using FirebaseAdmin.Messaging;
using TagAlong.EventBus;
using TagAlong.Notification.Domain.Repositories;

namespace TagAlong.Notification.API.IntegrationEvents;

/// <summary>Rings a phone for an in-app call ("incoming"), or stops it ringing ("ended").</summary>
public record CallPushIntegrationEvent(
    Guid UserId,
    string Kind,
    Guid CallId,
    Guid ConversationId,
    Guid CallerId,
    string CallerName) : IntegrationEvent;

/// <summary>
/// A data-only, high-priority push so the app wakes up (even when closed) and
/// shows the phone's own incoming-call screen. Not kept in the notifications list.
/// </summary>
public class CallPushIntegrationEventHandler : IIntegrationEventHandler<CallPushIntegrationEvent>
{
    private readonly IDeviceTokenRepository _tokens;
    private readonly ILogger<CallPushIntegrationEventHandler> _logger;

    public CallPushIntegrationEventHandler(IDeviceTokenRepository tokens, ILogger<CallPushIntegrationEventHandler> logger)
    {
        _tokens = tokens;
        _logger = logger;
    }

    public async Task HandleAsync(CallPushIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var device = await _tokens.GetByUserIdAsync(@event.UserId, cancellationToken);
        if (device == null) return;
        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(new Message
            {
                Token = device.Token,
                Data = new Dictionary<string, string>
                {
                    ["type"] = @event.Kind == "incoming" ? "IncomingCall" : "CallEnded",
                    ["callId"] = @event.CallId.ToString(),
                    ["conversationId"] = @event.ConversationId.ToString(),
                    ["callerId"] = @event.CallerId.ToString(),
                    ["callerName"] = @event.CallerName,
                },
                // A ring that arrives late is worse than none
                Android = new AndroidConfig { Priority = Priority.High, TimeToLive = TimeSpan.FromSeconds(40) },
                Apns = new ApnsConfig
                {
                    Headers = new Dictionary<string, string> { ["apns-priority"] = "10", ["apns-push-type"] = "background" },
                    Aps = new Aps { ContentAvailable = true },
                },
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Call push failed for user {UserId}", @event.UserId);
        }
    }
}
