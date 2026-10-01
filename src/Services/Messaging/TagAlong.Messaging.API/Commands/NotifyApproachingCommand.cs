using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

public record NotifyApproachingCommand(Guid ConversationId, Guid UserId) : ICommand<bool>;

public class NotifyApproachingCommandHandler : ICommandHandler<NotifyApproachingCommand, bool>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IEventBus _eventBus;
    private readonly ILogger<NotifyApproachingCommandHandler> _logger;

    public NotifyApproachingCommandHandler(
        IConversationRepository conversationRepository,
        IEventBus eventBus,
        ILogger<NotifyApproachingCommandHandler> logger)
    {
        _conversationRepository = conversationRepository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(NotifyApproachingCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<bool>(Error.NotFound("Conversation not found"));

        // Only the driver (TravelerId) may call this
        if (conversation.TravelerId != request.UserId)
            return Result.Failure<bool>(Error.Unauthorized("Only the driver can send this notification"));

        // Only fire when driver is approaching (LockedIn) or ride has started (InProgress)
        if (conversation.Status != ConversationStatus.LockedIn && conversation.Status != ConversationStatus.InProgress)
            return Result.Success(false);

        await _eventBus.PublishAsync(new DriverApproachingIntegrationEvent(
            request.ConversationId,
            conversation.SenderId,
            string.Empty));

        _logger.LogInformation("Driver approaching event published for conversation {ConversationId}", request.ConversationId);
        return Result.Success(true);
    }
}
