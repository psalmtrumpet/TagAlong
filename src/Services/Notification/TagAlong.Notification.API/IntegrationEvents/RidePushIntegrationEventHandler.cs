using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

/// <summary>
/// A push for an on-demand ride: a new request for a driver, or the request
/// matched / expired for the passenger. Messaging writes the wording.
/// </summary>
public record RidePushIntegrationEvent(
    Guid UserId,
    string Title,
    string Body,
    Guid ReferenceId,
    string ReferenceType) : IntegrationEvent;

public class RidePushIntegrationEventHandler : IIntegrationEventHandler<RidePushIntegrationEvent>
{
    private readonly INotificationService _notificationService;

    public RidePushIntegrationEventHandler(INotificationService notificationService) =>
        _notificationService = notificationService;

    public Task HandleAsync(RidePushIntegrationEvent @event, CancellationToken cancellationToken = default) =>
        _notificationService.SendNotificationAsync(
            @event.UserId,
            @event.Title,
            @event.Body,
            NotificationType.System,
            @event.ReferenceId,
            @event.ReferenceType,
            null,
            cancellationToken);
}
