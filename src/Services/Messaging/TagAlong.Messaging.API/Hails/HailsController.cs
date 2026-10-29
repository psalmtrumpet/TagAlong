using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Common.Results;

namespace TagAlong.Messaging.API.Hails;

/// <summary>On-demand rides ("Ride now"). Under /api/conversations so the gateway already routes it.</summary>
[ApiController]
[Route("api/conversations/hails")]
[Authorize]
public class HailsController : ControllerBase
{
    private readonly HailService _hails;

    public HailsController(HailService hails) => _hails = hails;

    /// <summary>Suggested fare and drivers around, before asking.</summary>
    [HttpGet("quote")]
    public async Task<IActionResult> Quote(double pickupLat, double pickupLng, double destLat, double destLng, CancellationToken ct) =>
        Ok(await _hails.QuoteAsync(pickupLat, pickupLng, destLat, destLng, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHailRequest body, CancellationToken ct) =>
        Respond(await _hails.CreateAsync(Me, body, ct));

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        await _hails.GetMineAsync(Me, ct) is { } h ? Ok(h) : NoContent();

    [HttpPost("{id:guid}/raise")]
    public async Task<IActionResult> Raise(Guid id, [FromBody] PriceRequest body, CancellationToken ct) =>
        Respond(await _hails.RaiseAsync(Me, id, body.Price, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] TagAlong.Messaging.API.DTOs.CancelReasonRequest? body,
        CancellationToken ct) =>
        Respond(await _hails.CancelAsync(Me, id, body?.Reason, ct));

    /// <summary>Don't wait for these drivers (too far).</summary>
    [HttpPost("{id:guid}/skip")]
    public async Task<IActionResult> Skip(Guid id, [FromBody] SkipDriversRequest body, CancellationToken ct) =>
        Respond(await _hails.SkipAsync(Me, id, body.DriverIds ?? new List<Guid>(), ct));

    /// <summary>Keep only drivers who can get here within this many minutes.</summary>
    [HttpPost("{id:guid}/only-within")]
    public async Task<IActionResult> OnlyWithin(Guid id, [FromBody] OnlyWithinRequest body, CancellationToken ct) =>
        Respond(await _hails.OnlyWithinAsync(Me, id, body.Minutes, ct));

    [HttpPost("{id:guid}/accept-counter")]
    public async Task<IActionResult> AcceptCounter(Guid id, [FromBody] AcceptCounterRequest body, CancellationToken ct) =>
        Respond(await _hails.AcceptCounterAsync(Me, id, body.DriverId, ct));

    // Driver

    [HttpGet("incoming")]
    public async Task<IActionResult> Incoming(CancellationToken ct) => Ok(await _hails.GetIncomingAsync(Me, ct));

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct) =>
        Respond(await _hails.AcceptAsync(Me, id, ct));

    [HttpPost("{id:guid}/counter")]
    public async Task<IActionResult> Counter(Guid id, [FromBody] PriceRequest body, CancellationToken ct) =>
        Respond(await _hails.CounterAsync(Me, id, body.Price, ct));

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct) =>
        Respond(await _hails.DeclineAsync(Me, id, ct));

    private Guid Me => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    private IActionResult Respond(Result r) =>
        r.IsSuccess ? Ok() : Problem(r.Error);

    private IActionResult Respond<T>(Result<T> r) =>
        r.IsSuccess ? Ok(r.Value) : Problem(r.Error);

    private IActionResult Problem(Error e) => e.Type switch
    {
        ErrorType.NotFound => NotFound(new { error = e.Message }),
        ErrorType.Conflict => Conflict(new { error = e.Message }),
        _ => BadRequest(new { error = e.Message }),
    };
}
