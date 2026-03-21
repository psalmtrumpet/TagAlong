using TagAlong.EventBus;
using TagAlong.Notification.API.Services;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.API.IntegrationEvents;

public record NegotiationMessageSentIntegrationEvent(
    Guid ConversationId,
    Guid? PackageRequestId,
    Guid SenderId,
    Guid RecipientId,
    string MessageType,
    decimal? ProposedPrice,
    DateTime SentAt) : IntegrationEvent;

public class NegotiationMessageSentIntegrationEventHandler : IIntegrationEventHandler<NegotiationMessageSentIntegrationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NegotiationMessageSentIntegrationEventHandler> _logger;

    public NegotiationMessageSentIntegrationEventHandler(
        INotificationService notificationService,
        ILogger<NegotiationMessageSentIntegrationEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(NegotiationMessageSentIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling NegotiationMessageSentIntegrationEvent for conversation {ConversationId}", @event.ConversationId);

        var (title, message, type) = GetNotificationContent(@event.MessageType, @event.ProposedPrice);

        await _notificationService.SendNotificationAsync(
            @event.RecipientId,
            title,
            message,
            type,
            @event.ConversationId,
            "Conversation",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                @event.MessageType,
                @event.ProposedPrice,
                @event.PackageRequestId
            }),
            cancellationToken);
    }

    private static (string title, string message, NotificationType type) GetNotificationContent(string messageType, decimal? proposedPrice)
    {
        return messageType switch
        {
            "PriceProposal" => ("New Price Proposal", $"You received a price proposal: {proposedPrice:C}", NotificationType.PriceProposal),
            "PriceAccepted" => ("Price Accepted!", "Your price has been accepted.", NotificationType.PriceAccepted),
            "PriceRejected" => ("Price Rejected", "Your price proposal was rejected.", NotificationType.PriceRejected),
            _ => ("New Message", "You have a new message.", NotificationType.NewMessage)
        };
    }
}
