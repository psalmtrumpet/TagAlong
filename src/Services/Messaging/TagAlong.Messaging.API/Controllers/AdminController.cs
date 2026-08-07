using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Services;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.API.Controllers;

[ApiController]
[Route("api/admin/conversations")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly MessagingDbContext _db;
    private readonly IUserLookupService _userLookup;

    public AdminController(MessagingDbContext db, IUserLookupService userLookup)
    {
        _db = db;
        _userLookup = userLookup;
    }

    [HttpGet]
    public async Task<IActionResult> ListConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string status = "all",
        CancellationToken cancellationToken = default)
    {
        var query = _db.Conversations.AsQueryable();

        if (status != "all" && Enum.TryParse<ConversationStatus>(status, true, out var statusEnum))
            query = query.Where(c => c.Status == statusEnum);

        var total = await query.CountAsync(cancellationToken);

        var convos = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Resolve user names in parallel
        var userIds = convos
            .SelectMany(c => new[] { c.SenderId, c.TravelerId })
            .Distinct()
            .ToList();

        var nameTasks = userIds.ToDictionary(
            id => id,
            id => _userLookup.GetDisplayNameAsync(id, cancellationToken));
        await Task.WhenAll(nameTasks.Values);
        var names = nameTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);

        var dtos = convos.Select(c =>
        {
            names.TryGetValue(c.SenderId, out var senderName);
            names.TryGetValue(c.TravelerId, out var travelerName);
            return new ConversationDto(
                c.Id, c.PackageRequestId, c.SenderId, c.TravelerId,
                senderName, travelerName, c.Status.ToString(),
                c.CreatedAt, c.UpdatedAt, null,
                c.RecipientUserId, c.RecipientName, c.AgreedPrice,
                c.LockInProposedBy, c.StartedAt, c.DeliveredAt,
                c.PassengerDestLat, c.PassengerDestLng, c.PassengerDestAddress,
                c.HelperLastLat, c.HelperLastLng);
        });

        return Ok(new { conversations = dtos, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetConversation(Guid id, CancellationToken cancellationToken)
    {
        var conv = await _db.Conversations
            .Include(c => c.Messages.OrderBy(m => m.SentAt))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conv == null) return NotFound(new { error = "Conversation not found" });

        var senderTask  = _userLookup.GetDisplayNameAsync(conv.SenderId, cancellationToken);
        var travelerTask = _userLookup.GetDisplayNameAsync(conv.TravelerId, cancellationToken);
        await Task.WhenAll(senderTask, travelerTask);

        var messages = conv.Messages.Select(m => new MessageDto(
            m.Id, m.ConversationId, m.SenderId, m.Content,
            m.MessageType.ToString(), m.ProposedPrice, m.SentAt, m.ReadAt));

        var dto = new ConversationDto(
            conv.Id, conv.PackageRequestId, conv.SenderId, conv.TravelerId,
            senderTask.Result, travelerTask.Result, conv.Status.ToString(),
            conv.CreatedAt, conv.UpdatedAt, null,
            conv.RecipientUserId, conv.RecipientName, conv.AgreedPrice,
            conv.LockInProposedBy, conv.StartedAt, conv.DeliveredAt,
            conv.PassengerDestLat, conv.PassengerDestLng, conv.PassengerDestAddress,
            conv.HelperLastLat, conv.HelperLastLng);

        return Ok(new { conversation = dto, messages });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var total      = await _db.Conversations.CountAsync(cancellationToken);
        var pending    = await _db.Conversations.CountAsync(c => c.Status == ConversationStatus.Pending, cancellationToken);
        var active     = await _db.Conversations.CountAsync(c => c.Status == ConversationStatus.Active, cancellationToken);
        var lockedIn   = await _db.Conversations.CountAsync(c => c.Status == ConversationStatus.LockedIn, cancellationToken);
        var inProgress = await _db.Conversations.CountAsync(c => c.Status == ConversationStatus.InProgress, cancellationToken);
        var closed     = await _db.Conversations.CountAsync(c => c.Status == ConversationStatus.Closed, cancellationToken);

        return Ok(new { total, pending, active, lockedIn, inProgress, closed });
    }
}
