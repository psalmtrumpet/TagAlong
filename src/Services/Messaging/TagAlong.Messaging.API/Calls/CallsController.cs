using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TagAlong.EventBus;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.API.Services;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.API.Calls;

/// <summary>What a phone needs to join an in-app call.</summary>
public record CallJoinDto(Guid CallId, Guid ConversationId, string AppId, string Channel, string Token, uint Uid);

/// <summary>A call ringing on the other phone.</summary>
public record IncomingCallDto(Guid CallId, Guid ConversationId, Guid CallerId, string CallerName);

public record EndCallRequest(string? Reason);

/// <summary>
/// Calls between a driver and their passenger while a ride is booked or under
/// way: an in-app voice call (Agora), or the phone dialler with the other
/// person's number (never shown in the app). Nobody else can call either.
/// </summary>
[ApiController]
[Authorize]
[Route("api/conversations/{conversationId:guid}")]
public class CallsController : ControllerBase
{
    /// <summary>How long a call pass lasts (longer than any ride's call).</summary>
    private const uint TokenSeconds = 3 * 60 * 60;

    private readonly MessagingDbContext _db;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hub;
    private readonly IEventBus _eventBus;
    private readonly IUserLookupService _users;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<CallsController> _logger;

    public CallsController(MessagingDbContext db, IHubContext<MessagingHub, IMessagingClient> hub, IEventBus eventBus,
        IUserLookupService users, IHttpClientFactory http, IConfiguration config, ILogger<CallsController> logger)
    {
        _db = db;
        _hub = hub;
        _eventBus = eventBus;
        _users = users;
        _http = http;
        _config = config;
        _logger = logger;
    }

    /// <summary>The other person's number, for the phone dialler only.</summary>
    [HttpGet("phone")]
    public async Task<IActionResult> Phone(Guid conversationId, CancellationToken ct)
    {
        var (ride, me, error) = await RideAsync(conversationId, ct);
        if (error != null) return error;
        var other = ride!.GetOtherParticipant(me);
        try
        {
            var res = await _http.CreateClient("user-api").GetAsync($"internal/users/{other}/phone", ct);
            if (!res.IsSuccessStatusCode) return NotFound(new { error = "Their phone number isn't available." });
            var body = await res.Content.ReadFromJsonAsync<PhoneDto>(cancellationToken: ct);
            return string.IsNullOrWhiteSpace(body?.Phone)
                ? NotFound(new { error = "Their phone number isn't available." })
                : Ok(new { phone = body.Phone });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't look up a phone number for ride {ConversationId}", conversationId);
            return StatusCode(503, new { error = "Couldn't get their number. Try again." });
        }
    }

    /// <summary>Start an in-app call: rings the other person.</summary>
    [HttpPost("calls")]
    public async Task<IActionResult> Start(Guid conversationId, CancellationToken ct)
    {
        var (ride, me, error) = await RideAsync(conversationId, ct);
        if (error != null) return error;
        if (!Configured(out var appId, out var cert))
            return StatusCode(503, new { error = "In-app calls aren't available yet. Use a phone call." });

        var callId = Guid.NewGuid();
        var other = ride!.GetOtherParticipant(me);
        var name = FirstName(await _users.GetDisplayNameAsync(me, ct));
        var incoming = new IncomingCallDto(callId, conversationId, me, name);

        // Open app: the live connection; closed or in the background: a push that rings
        await _hub.Clients.Group($"user_{other}").IncomingCall(incoming);
        await PushAsync(other, "incoming", incoming, ct);

        _logger.LogInformation("Call {CallId} started on ride {ConversationId}", callId, conversationId);
        return Ok(Join(callId, conversationId, appId, cert, UidOf(ride, me)));
    }

    /// <summary>Pick up: the pass to join, and tells the caller.</summary>
    [HttpPost("calls/{callId:guid}/answer")]
    public async Task<IActionResult> Answer(Guid conversationId, Guid callId, CancellationToken ct)
    {
        var (ride, me, error) = await RideAsync(conversationId, ct);
        if (error != null) return error;
        if (!Configured(out var appId, out var cert))
            return StatusCode(503, new { error = "In-app calls aren't available yet." });
        await _hub.Clients.Group($"user_{ride!.GetOtherParticipant(me)}").CallAnswered(callId);
        return Ok(Join(callId, conversationId, appId, cert, UidOf(ride, me)));
    }

    /// <summary>Hang up, decline, cancel before it's answered, or no answer.</summary>
    [HttpPost("calls/{callId:guid}/end")]
    public async Task<IActionResult> End(Guid conversationId, Guid callId, [FromBody] EndCallRequest? request, CancellationToken ct)
    {
        var (ride, me, error) = await RideAsync(conversationId, ct, anyStatus: true);
        if (error != null) return error;
        var other = ride!.GetOtherParticipant(me);
        var reason = request?.Reason is "declined" or "missed" or "cancelled" ? request.Reason : "ended";
        await _hub.Clients.Group($"user_{other}").CallEnded(callId, reason);
        // Stop it ringing on a phone that only got the push
        await PushAsync(other, "ended", new IncomingCallDto(callId, conversationId, me, ""), ct);

        // Missed calls show in the chat, like on a phone
        if (reason is "missed" or "cancelled" or "declined")
        {
            // Declined: sent by the one who was called; missed/cancelled: by the caller
            var name = FirstName(await _users.GetDisplayNameAsync(me, ct));
            var msg = Message.CreateSystemMessage(conversationId, reason == "declined"
                ? $"{name} couldn't take the call."
                : $"Missed call from {name}.");
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync(ct);
            var dto = new TagAlong.Messaging.API.DTOs.MessageDto(msg.Id, msg.ConversationId, msg.SenderId, msg.Content,
                msg.MessageType.ToString(), msg.ProposedPrice, msg.SentAt, msg.ReadAt);
            await _hub.Clients.Group($"conversation_{conversationId}").ReceiveMessage(dto);
        }
        return Ok();
    }

    // ── Helpers ──

    /// <summary>The ride, if the caller is on it and it's booked or under way.</summary>
    private async Task<(Conversation? Ride, Guid Me, IActionResult? Error)> RideAsync(Guid conversationId, CancellationToken ct,
        bool anyStatus = false)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var me))
            return (null, Guid.Empty, Unauthorized());
        var ride = await _db.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == conversationId, ct);
        if (ride == null || (ride.SenderId != me && ride.TravelerId != me))
            return (null, me, NotFound(new { error = "Ride not found" }));
        if (!anyStatus && ride.Status is not (ConversationStatus.LockedIn or ConversationStatus.InProgress))
            return (null, me, BadRequest(new { error = "You can call once the ride is booked, until it ends." }));
        return (ride, me, null);
    }

    private bool Configured(out string appId, out string cert)
    {
        appId = _config["Agora:AppId"] ?? "";
        cert = _config["Agora:AppCertificate"] ?? "";
        return appId.Length > 0 && cert.Length > 0;
    }

    /// <summary>Each call has its own channel; the passenger is 1, the driver 2.</summary>
    private static CallJoinDto Join(Guid callId, Guid conversationId, string appId, string cert, uint uid)
    {
        var channel = "call_" + callId.ToString("N");
        return new CallJoinDto(callId, conversationId, appId, channel,
            AgoraToken.BuildRtc(appId, cert, channel, uid, TokenSeconds), uid);
    }

    private static uint UidOf(Conversation ride, Guid me) => ride.SenderId == me ? 1u : 2u;

    private async Task PushAsync(Guid userId, string kind, IncomingCallDto call, CancellationToken ct)
    {
        try
        {
            await _eventBus.PublishAsync(new CallPushIntegrationEvent(
                userId, kind, call.CallId, call.ConversationId, call.CallerId, call.CallerName), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't send call push to {UserId}", userId);
        }
    }

    private static string FirstName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "TagAlong" : name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

    private record PhoneDto(string? Phone);
}
