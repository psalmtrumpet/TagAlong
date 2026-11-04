using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Messaging.API.Hails;
using TagAlong.Messaging.API.Services;
using TagAlong.Messaging.Domain.Entities;

namespace TagAlong.Messaging.API.Controllers;

public record ConfirmFeePaymentRequest(string? Reference);

/// <summary>The signed-in driver's TagAlong fees: what's owed, and paying it through Paystack.</summary>
[ApiController]
[Route("api/conversations/fees")]
[Authorize]
public class DriverFeesController : ControllerBase
{
    private readonly DriverFees _fees;
    private readonly HailService _hails;

    public DriverFeesController(DriverFees fees, HailService hails)
    {
        _fees = fees;
        _hails = hails;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (UserId is not { } id) return Unauthorized();
        return Ok(await _fees.StatusAsync(id, ct));
    }

    /// <summary>Start paying what's owed: open the returned Paystack page.</summary>
    [HttpPost("pay")]
    public async Task<IActionResult> Pay(CancellationToken ct)
    {
        if (UserId is not { } id) return Unauthorized();
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
        var result = await _fees.StartAsync(id, email, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error.Message });
    }

    /// <summary>Back from Paystack: check the payment (the latest one when no reference is given).</summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmFeePaymentRequest? body, CancellationToken ct)
    {
        if (UserId is not { } id) return Unauthorized();
        var reference = body?.Reference is { Length: > 0 } r ? r : await _fees.LatestPendingReferenceAsync(id, ct);
        if (reference != null)
        {
            var (_, paidBy) = await _fees.ConfirmAsync(reference, ct);
            await PaystackController.AfterPaidAsync(paidBy, _fees, _hails, ct);
        }
        return Ok(await _fees.StatusAsync(id, ct));
    }

    private Guid? UserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}

/// <summary>Paystack calls these: the webhook, and the page a driver lands on after paying.</summary>
[ApiController]
[Route("api/public/paystack")]
[AllowAnonymous]
public class PaystackController : ControllerBase
{
    private readonly DriverFees _fees;
    private readonly HailService _hails;
    private readonly ILogger<PaystackController> _logger;

    public PaystackController(DriverFees fees, HailService hails, ILogger<PaystackController> logger)
    {
        _fees = fees;
        _hails = hails;
        _logger = logger;
    }

    internal static async Task AfterPaidAsync(Guid? driverId, DriverFees fees, HailService hails, CancellationToken ct)
    {
        if (driverId is not { } id) return;
        await fees.AfterPaidAsync(id, ct);
        await hails.OfferTeasedAsync(id, ct);
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var body = ms.ToArray();
        if (!_fees.IsFromPaystack(body, Request.Headers["x-paystack-signature"]))
        {
            _logger.LogWarning("Paystack webhook with a bad signature ignored");
            return Unauthorized();
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var evt = doc.RootElement.GetProperty("event").GetString();
            if (evt == "charge.success" &&
                doc.RootElement.GetProperty("data").GetProperty("reference").GetString() is { Length: > 0 } reference)
            {
                // Checked again with Paystack before anything is settled
                var (_, paidBy) = await _fees.ConfirmAsync(reference, ct);
                await AfterPaidAsync(paidBy, _fees, _hails, ct);
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Couldn't read a Paystack webhook");
        }
        return Ok();
    }

    /// <summary>Where Paystack sends the driver after checkout.</summary>
    [HttpGet("return")]
    public async Task<IActionResult> Return([FromQuery] string? reference, [FromQuery] string? trxref, CancellationToken ct)
    {
        var r = reference ?? trxref;
        var status = DriverFeePaymentStatus.Pending;
        if (!string.IsNullOrEmpty(r))
        {
            (status, var paidBy) = await _fees.ConfirmAsync(r, ct);
            await AfterPaidAsync(paidBy, _fees, _hails, ct);
        }
        var (title, text) = status switch
        {
            DriverFeePaymentStatus.Paid => ("Payment received", "Thank you! Go back to TagAlong — you'll get ride requests again."),
            DriverFeePaymentStatus.Failed => ("Payment didn't go through", "Nothing was taken. Go back to TagAlong and try again."),
            _ => ("Payment not finished", "Go back to TagAlong. If you paid, it will show there in a moment."),
        };
        var html = $$"""
            <!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{{title}} · TagAlong</title>
            <style>body{margin:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#F5F3EE;color:#111;
            display:flex;min-height:100vh;align-items:center;justify-content:center}
            .card{background:#fff;border-radius:16px;padding:32px 24px;max-width:360px;margin:16px;text-align:center}
            h1{font-size:22px;margin:0 0 8px}p{color:#555;line-height:1.5;margin:0}
            .dot{width:56px;height:56px;border-radius:50%;background:#F1B01C;margin:0 auto 16px}</style></head>
            <body><div class="card"><div class="dot"></div><h1>{{title}}</h1><p>{{text}}</p></div></body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }
}
