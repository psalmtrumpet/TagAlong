using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using TagAlong.EventBus;
using TagAlong.Messaging.API.IntegrationEvents;
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
    private readonly IMemoryCache _cache;
    private readonly IEventBus _eventBus;

    /// <summary>Tell a riding passenger their stop is next when the car is this close.</summary>
    private const double NextStopMeters = 500;

    public MessagingHub(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        ILogger<MessagingHub> logger,
        IMemoryCache cache,
        IEventBus eventBus)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _logger = logger;
        _cache = cache;
        _eventBus = eventBus;
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

        if (string.IsNullOrEmpty(content) || content.Length > 2000)
            throw new HubException("Message content must be between 1 and 2000 characters");

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

        // Persist last-known carrier location so the public tracking page can poll it.
        // Only the traveler's position is the carrier's — the sender also broadcasts
        // (so the carrier can find them) and must not overwrite it.
        if (conversation.TravelerId == userId.Value)
        {
            conversation.UpdateHelperLocation(latitude, longitude);
            _conversationRepository.Update(conversation);
            await _conversationRepository.SaveChangesAsync();

            if (conversation.Status == ConversationStatus.InProgress)
                await AnnounceNextStopAsync(conversation, latitude, longitude);
        }

        var convIdStr = conversationId.ToString();
        // OthersInGroup prevents the sender from seeing their own location echoed back.
        // The other user also receives it via their personal user_ group.
        await Clients.OthersInGroup($"conversation_{conversationId}").ReceiveHelperLocation(convIdStr, latitude, longitude);

        var otherUserId = conversation.GetOtherParticipant(userId.Value);
        await Clients.Group($"user_{otherUserId}").ReceiveHelperLocation(convIdStr, latitude, longitude);
    }

    /// <summary>
    /// Once per ride: when the car is near the passenger's drop-off, post
    /// "Next stop" in the chat and push it to the passenger (their app may be closed).
    /// </summary>
    private async Task AnnounceNextStopAsync(Conversation conversation, double lat, double lng)
    {
        var stopLat = conversation.DropLat ?? conversation.PassengerDestLat;
        var stopLng = conversation.DropLng ?? conversation.PassengerDestLng;
        if (stopLat is null || stopLng is null) return;
        if (DistanceMeters(lat, lng, stopLat.Value, stopLng.Value) > NextStopMeters) return;

        var key = $"next-stop:{conversation.Id}";
        if (_cache.TryGetValue(key, out _)) return;
        _cache.Set(key, true, TimeSpan.FromHours(12));

        var stopName = conversation.DropName
            ?? conversation.PassengerDestAddress?.Split(',')[0].Trim()
            ?? "your stop";
        try
        {
            var msg = Message.CreateSystemMessage(conversation.Id, $"Next stop: {stopName} — get ready to get off");
            await _messageRepository.AddAsync(msg);
            await _messageRepository.SaveChangesAsync();
            var dto = MapToDto(msg);
            await Clients.Group($"conversation_{conversation.Id}").ReceiveMessage(dto);
            await Clients.Group($"user_{conversation.SenderId}").ReceiveMessage(dto);

            await _eventBus.PublishAsync(new PassengerStopNextIntegrationEvent(conversation.Id, conversation.SenderId, stopName));
            _logger.LogInformation("Next stop announced for conversation {ConversationId}", conversation.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't announce the next stop for {ConversationId}", conversation.Id);
        }
    }

    private static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }

    public async Task MarkAsRead(Guid messageId)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message == null || message.SenderId == userId.Value) return;

        // Verify the caller is a participant in the conversation before marking
        var conversation = await _conversationRepository.GetByIdAsync(message.ConversationId);
        if (conversation == null || !conversation.IsParticipant(userId.Value)) return;

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
