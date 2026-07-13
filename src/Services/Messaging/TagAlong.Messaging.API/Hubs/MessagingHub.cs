using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Hubs;

[Authorize]
public class MessagingHub : Hub<IMessagingClient>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<MessagingHub> _logger;

    public MessagingHub(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        ILogger<MessagingHub> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} connected to messaging hub", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} disconnected from messaging hub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation == null || !conversation.IsParticipant(userId.Value))
        {
            throw new HubException("Unauthorized to join this conversation");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");

        // Mark messages as read
        await _messageRepository.MarkAllAsReadAsync(conversationId, userId.Value);
        await _messageRepository.SaveChangesAsync();

        _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
    }

    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        _logger.LogInformation("User left conversation {ConversationId}", conversationId);
    }

    public async Task SendMessage(Guid conversationId, string content)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation == null || !conversation.IsParticipant(userId.Value))
        {
            throw new HubException("Unauthorized to send message to this conversation");
        }

        var message = Message.CreateTextMessage(conversationId, userId.Value, content);
        await _messageRepository.AddAsync(message);
        await _messageRepository.SaveChangesAsync();

        var dto = MapToDto(message);
        await Clients.Group($"conversation_{conversationId}").ReceiveMessage(dto);

        // Notify the other participant
        var otherUserId = conversation.GetOtherParticipant(userId.Value);
        await Clients.Group($"user_{otherUserId}").ReceiveMessage(dto);

        _logger.LogInformation("Message {MessageId} sent in conversation {ConversationId}", message.Id, conversationId);
    }

    public async Task SendPriceProposal(Guid conversationId, decimal proposedPrice, string? content)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation == null || !conversation.IsParticipant(userId.Value))
        {
            throw new HubException("Unauthorized to send price proposal to this conversation");
        }

        var message = Message.CreatePriceProposal(conversationId, userId.Value, proposedPrice, content);
        await _messageRepository.AddAsync(message);
        await _messageRepository.SaveChangesAsync();

        var dto = MapToDto(message);
        await Clients.Group($"conversation_{conversationId}").ReceiveMessage(dto);

        var otherUserId = conversation.GetOtherParticipant(userId.Value);
        await Clients.Group($"user_{otherUserId}").ReceiveMessage(dto);

        _logger.LogInformation("Price proposal {Price} sent in conversation {ConversationId}", proposedPrice, conversationId);
    }

    public async Task SendLocation(Guid conversationId, double latitude, double longitude)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation == null || !conversation.IsParticipant(userId.Value)) return;

        var convIdStr = conversationId.ToString();
        await Clients.Group($"conversation_{conversationId}").ReceiveHelperLocation(convIdStr, latitude, longitude);

        var otherUserId = conversation.GetOtherParticipant(userId.Value);
        await Clients.Group($"user_{otherUserId}").ReceiveHelperLocation(convIdStr, latitude, longitude);
    }

    public async Task MarkAsRead(Guid messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message == null || message.SenderId == userId.Value) return;

        message.MarkAsRead();
        _messageRepository.Update(message);
        await _messageRepository.SaveChangesAsync();

        await Clients.Group($"conversation_{message.ConversationId}").MessageRead(messageId, message.ReadAt!.Value);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static MessageDto MapToDto(Message message) => new(
        message.Id,
        message.ConversationId,
        message.SenderId,
        message.Content,
        message.MessageType.ToString(),
        message.ProposedPrice,
        message.SentAt,
        message.ReadAt);
}
