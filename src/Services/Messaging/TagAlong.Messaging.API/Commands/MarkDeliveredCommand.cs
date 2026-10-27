using Microsoft.AspNetCore.SignalR;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Commands;

/// <param name="BySystem">Ended by the server (trip over, or the passenger never tapped End ride).</param>
public record MarkDeliveredCommand(Guid ConversationId, Guid UserId, bool BySystem = false) : ICommand<ConversationDto>;

public class MarkDeliveredCommandHandler : ICommandHandler<MarkDeliveredCommand, ConversationDto>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hubContext;
    private readonly IEventBus _eventBus;

    public MarkDeliveredCommandHandler(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IHubContext<MessagingHub, IMessagingClient> hubContext,
        IEventBus eventBus,
        IConfiguration config,
        IHttpClientFactory http,
        ILogger<MarkDeliveredCommandHandler> logger)
    {
        _http = http;
        _logger = logger;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _config = config;
    }

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<MarkDeliveredCommandHandler> _logger;

    /// <summary>TagAlong's flat fee for a private (premium) ride — defaults to the shared fee.</summary>
    public static decimal PrivateRideFlatFee(IConfiguration config) =>
        decimal.TryParse(config["Earnings:RidePrivateFlatFee"], System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee >= 0 ? fee : RideFlatFee(config);

    /// <summary>The fee for this ride: private rides have their own fee; a referral free ride waives it.</summary>
    private bool _isPrivate;

    private async Task<(decimal Fee, bool Waived)> FeeForAsync(Conversation c, CancellationToken ct)
    {
        _isPrivate = c.IsOnDemand || await IsPrivateTripAsync(c.TripId, ct);
        var fee = _isPrivate ? PrivateRideFlatFee(_config) : RideFlatFee(_config);
        if (fee <= 0 || c.AgreedPrice is not > 0) return (fee, false);
        try
        {
            using var res = await _http.CreateClient("user-api")
                .PostAsync($"internal/referrals/{c.TravelerId}/use-free-trip", null, ct);
            if (res.IsSuccessStatusCode)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.GetProperty("used").GetBoolean())
                {
                    _logger.LogInformation("Referral free ride used by driver {DriverId} on {ConversationId}", c.TravelerId, c.Id);
                    return (fee, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't check the driver's referral free rides");
        }
        return (fee, false);
    }

    private async Task<bool> IsPrivateTripAsync(Guid? tripId, CancellationToken ct)
    {
        if (tripId == null) return false;
        try
        {
            using var res = await _http.CreateClient("trip-api").GetAsync($"api/trips/{tripId}", ct);
            if (!res.IsSuccessStatusCode) return false;
            using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("isPrivate", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>TagAlong's flat fee per ride (Earnings:RideFlatFee, default ₦200).</summary>
    public static decimal RideFlatFee(IConfiguration config) =>
        decimal.TryParse(config["Earnings:RideFlatFee"], System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee >= 0 ? fee : 200m;

    public async Task<Result<ConversationDto>> Handle(MarkDeliveredCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
            return Result.Failure<ConversationDto>(Error.NotFound("Conversation not found"));

        // A ride is ended by the passenger once they're out of the car; a delivery by the carrier
        var allowed = request.BySystem ||
            (conversation.IsDelivery ? conversation.TravelerId == request.UserId : conversation.SenderId == request.UserId);
        if (!allowed)
            return Result.Failure<ConversationDto>(Error.Unauthorized(conversation.IsDelivery
                ? "Only the carrier can mark this as delivered"
                : "Your passenger ends the ride when they get out."));

        var (fee, waived) = await FeeForAsync(conversation, cancellationToken);
        conversation.MarkDelivered(fee, waived);
        _conversationRepository.Update(conversation);

        var msg = Message.CreateDelivered(request.ConversationId, request.UserId);
        await _messageRepository.AddAsync(msg, cancellationToken);
        await _conversationRepository.SaveChangesAsync(cancellationToken);

        await ConversationLifecyclePublisher.TravelerTripStateAsync(_conversationRepository, _eventBus, conversation.TravelerId, cancellationToken);
        await ConversationLifecyclePublisher.TripBookingsAsync(_conversationRepository, _eventBus, conversation, cancellationToken);
        await ConversationLifecyclePublisher.CompletedAsync(_eventBus, conversation, cancellationToken);

        var dto = ConversationDtoMapper.ToDto(conversation);
        var msgDto = MessageDtoMapper.ToDto(msg);

        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"conversation_{request.ConversationId}").DeliveryCompleted(dto);

        var otherUserId = conversation.GetOtherParticipant(request.UserId);
        await _hubContext.Clients.Group($"user_{otherUserId}").ReceiveMessage(msgDto);
        await _hubContext.Clients.Group($"user_{otherUserId}").DeliveryCompleted(dto);

        // A private ride has one passenger — once they're dropped off, the journey's over.
        // A shared one ends with its last passenger, as the driver's app used to do.
        if (conversation.TripId is { } tripId && !conversation.IsDelivery &&
            (_isPrivate || await _conversationRepository.CountActiveBookingsByTripIdAsync(tripId, cancellationToken) == 0))
        {
            try
            {
                using var res = await _http.CreateClient("trip-api").PostAsync($"internal/trips/{tripId}/complete", null, cancellationToken);
                _logger.LogInformation("Private trip {TripId} ended after drop-off ({Status})", tripId, (int)res.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Couldn't end private trip {TripId} after drop-off", tripId);
            }
        }

        return Result.Success(dto);
    }
}
