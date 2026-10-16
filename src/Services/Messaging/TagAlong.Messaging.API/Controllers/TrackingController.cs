using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Controllers;

[ApiController]
[Route("api/public/tracking")]
[AllowAnonymous]
public class TrackingController : ControllerBase
{
    private readonly IConversationRepository _repo;

    public TrackingController(IConversationRepository repo)
    {
        _repo = repo;
    }

    /// <summary>
    /// Public endpoint — no auth required.
    /// Returns the carrier's last known location and delivery status for the
    /// web tracking page at tagalong.delivery/track/{id}.
    /// </summary>
    [HttpGet("{conversationId:guid}")]
    public async Task<IActionResult> Get(Guid conversationId, CancellationToken cancellationToken)
    {
        var conv = await _repo.GetByIdAsync(conversationId, cancellationToken);
        if (conv == null) return NotFound();

        return Ok(new
        {
            conversationId = conv.Id,
            status         = conv.Status.ToString(),
            helperLat      = conv.HelperLastLat,
            helperLng      = conv.HelperLastLng,
            helperLastSeen = conv.HelperLastSeenAt,
            destLat        = conv.DropLat ?? conv.PassengerDestLat,
            destLng        = conv.DropLng ?? conv.PassengerDestLng,
            destAddress    = conv.DropName ?? conv.PassengerDestAddress,
            isDelivery     = conv.IsDelivery,
            pickupName     = conv.MeetName ?? conv.PickupAddress,
        });
    }
}
