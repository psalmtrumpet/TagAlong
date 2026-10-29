using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using TagAlong.EventBus;
using TagAlong.Messaging.API.Commands;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Services;

/// <summary>
/// One participant's live position during a ride: saved (for the driver), passed
/// to the other person, and checked against the passenger's stop. Used by the
/// hub and by the plain HTTP endpoint (for phones whose live connection dropped).
/// </summary>
public class LocationRelay
{
    /// <summary>Tell a riding passenger their stop is next when the car is this close.</summary>
    private const double NextStopMeters = 500;

    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hub;
    private readonly IMemoryCache _cache;
    private readonly IEventBus _eventBus;
    private readonly ILogger<LocationRelay> _logger;

    public LocationRelay(IConversationRepository conversations, IMessageRepository messages,
        IHubContext<MessagingHub, IMessagingClient> hub, IMemoryCache cache, IEventBus eventBus, ILogger<LocationRelay> logger)
    {
        _conversations = conversations;
        _messages = messages;
        _hub = hub;
        _cache = cache;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <summary>Returns false when the user isn't on this ride.</summary>
    public async Task<bool> ShareAsync(Guid userId, Guid conversationId, double latitude, double longitude, LocationReading? reading = null)
    {
        var conversation = await _conversations.GetByIdAsync(conversationId);
        if (conversation == null || !conversation.IsParticipant(userId)) return false;

        // Persist the carrier's last-known position (the public tracking page and
        // the passenger's map read it). Only the traveler's position is the carrier's.
        if (conversation.TravelerId == userId)
        {
            conversation.UpdateHelperLocation(latitude, longitude);
            _conversations.Update(conversation);
            await _conversations.SaveChangesAsync();

            if (conversation.Status == ConversationStatus.InProgress)
                await AnnounceNextStopAsync(conversation, latitude, longitude);
        }

        var other = conversation.GetOtherParticipant(userId);
        await _hub.Clients.Group($"user_{other}").ReceiveHelperLocation(conversationId.ToString(), latitude, longitude,
            reading?.Accuracy, reading?.Speed, reading?.Heading, reading?.TimestampMs);
        return true;
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
            await _messages.AddAsync(msg);
            await _messages.SaveChangesAsync();
            var dto = MessageDtoMapper.ToDto(msg);
            await _hub.Clients.Group($"conversation_{conversation.Id}").ReceiveMessage(dto);
            await _hub.Clients.Group($"user_{conversation.SenderId}").ReceiveMessage(dto);

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
}

/// <summary>How good a GPS reading is, and when the phone took it.</summary>
public record LocationReading(double? Accuracy, double? Speed, double? Heading, long? TimestampMs);
