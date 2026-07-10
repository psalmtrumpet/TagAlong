using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Common.Results;
using TagAlong.Messaging.API.Commands;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Queries;

namespace TagAlong.Messaging.API.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConversationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ConversationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var conversations = await _mediator.Send(new GetUserConversationsQuery(userId.Value, page, pageSize), cancellationToken);
        if (conversations.IsFailure) return BadRequest(new { error = conversations.Error.Message });
        return Ok(conversations.Value);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _mediator.Send(new GetConversationByIdQuery(id), cancellationToken);
        if (result.IsFailure || result.Value == null)
            return NotFound();

        if (result.Value.SenderId != userId && result.Value.TravelerId != userId
            && result.Value.RecipientUserId != userId)
            return Forbid();

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(IEnumerable<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var conversationResult = await _mediator.Send(new GetConversationByIdQuery(id), cancellationToken);
        if (conversationResult.IsFailure || conversationResult.Value == null)
            return NotFound();

        if (conversationResult.Value.SenderId != userId && conversationResult.Value.TravelerId != userId)
            return Forbid();

        var messagesResult = await _mediator.Send(new GetMessagesQuery(id, page, pageSize), cancellationToken);
        return Ok(messagesResult.Value);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var command = new CreateConversationCommand(
            userId.Value,
            request.TravelerId,
            request.PackageRequestId,
            request.InitialMessage,
            request.RecipientUserId,
            request.RecipientName);

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Message });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var conversationResult = await _mediator.Send(new GetConversationByIdQuery(id), cancellationToken);
        if (conversationResult.IsFailure || conversationResult.Value == null)
            return NotFound();

        if (conversationResult.Value.SenderId != userId && conversationResult.Value.TravelerId != userId)
            return Forbid();

        var command = new SendMessageCommand(id, userId.Value, request.Content);
        var messageResult = await _mediator.Send(command, cancellationToken);
        if (messageResult.IsFailure)
            return BadRequest(new { error = messageResult.Error.Message });

        return Created($"/api/conversations/{id}/messages/{messageResult.Value.Id}", messageResult.Value);
    }

    [HttpPost("{id:guid}/price-proposal")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendPriceProposal(
        Guid id,
        [FromBody] SendPriceProposalRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var conversationResult = await _mediator.Send(new GetConversationByIdQuery(id), cancellationToken);
        if (conversationResult.IsFailure || conversationResult.Value == null)
            return NotFound();

        if (conversationResult.Value.SenderId != userId && conversationResult.Value.TravelerId != userId)
            return Forbid();

        var command = new SendPriceProposalCommand(id, userId.Value, request.ProposedPrice, request.Message);
        var messageResult = await _mediator.Send(command, cancellationToken);
        if (messageResult.IsFailure)
            return BadRequest(new { error = messageResult.Error.Message });

        return Created($"/api/conversations/{id}/messages/{messageResult.Value.Id}", messageResult.Value);
    }

    [HttpPost("{id:guid}/accept-request")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptRequest(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _mediator.Send(new AcceptConversationCommand(id, userId.Value), cancellationToken);
        if (result.IsFailure)
            return result.Error.Code.Contains("NotFound")
                ? NotFound(new { error = result.Error.Message })
                : BadRequest(new { error = result.Error.Message });

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/decline-request")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeclineRequest(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _mediator.Send(new DeclineConversationCommand(id, userId.Value), cancellationToken);
        if (result.IsFailure)
            return result.Error.Code.Contains("NotFound")
                ? NotFound(new { error = result.Error.Message })
                : BadRequest(new { error = result.Error.Message });

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/accept-price")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptPrice(
        Guid id,
        [FromBody] AcceptPriceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var conversationResult = await _mediator.Send(new GetConversationByIdQuery(id), cancellationToken);
        if (conversationResult.IsFailure || conversationResult.Value == null)
            return NotFound();

        if (conversationResult.Value.SenderId != userId && conversationResult.Value.TravelerId != userId)
            return Forbid();

        var command = new AcceptPriceCommand(id, userId.Value, request.AcceptedPrice, request.Message);
        var messageResult = await _mediator.Send(command, cancellationToken);
        if (messageResult.IsFailure)
            return BadRequest(new { error = messageResult.Error.Message });

        return Ok(messageResult.Value);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
