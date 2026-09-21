using MediatR;
using Microsoft.AspNetCore.Mvc;
using TagAlong.User.API.Commands;

namespace TagAlong.User.API.Controllers;

[ApiController]
[Route("api/users/kyc")]
public class SmileWebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;
    private readonly ILogger<SmileWebhookController> _logger;

    public SmileWebhookController(IMediator mediator, IConfiguration config, ILogger<SmileWebhookController> logger)
    {
        _mediator = mediator;
        _config = config;
        _logger = logger;
    }

    [HttpPost("smile-webhook")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        string rawBody;
        using (var reader = new System.IO.StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        var apiKey = _config["SmileId:ApiKey"] ?? string.Empty;
        var partnerId = _config["SmileId:PartnerId"] ?? "6808";

        // SmileID puts signature + timestamp in Response-Signature / Response-Timestamp headers
        var headerSig = Request.Headers["Response-Signature"].FirstOrDefault();
        var headerTs = Request.Headers["Response-Timestamp"].FirstOrDefault();

        _logger.LogInformation("Smile ID webhook received. headerSig={HS} headerTs={HT}",
            headerSig != null ? (headerSig.Length > 8 ? headerSig[..8] : headerSig) : "none",
            headerTs ?? "none");

        var command = new ProcessSmileWebhookCommand(rawBody, apiKey, partnerId, headerSig, headerTs);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            _logger.LogError("Smile ID webhook handler fault: {Error}", result.Error.Message);

        // Always return 200 so Smile ID does not retry
        return Ok(new { received = true });
    }
}
