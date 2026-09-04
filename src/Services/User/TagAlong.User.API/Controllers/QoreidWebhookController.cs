using MediatR;
using Microsoft.AspNetCore.Mvc;
using TagAlong.User.API.Commands;

namespace TagAlong.User.API.Controllers;

[ApiController]
[Route("api/users/kyc")]
public class QoreidWebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;
    private readonly ILogger<QoreidWebhookController> _logger;

    public QoreidWebhookController(IMediator mediator, IConfiguration config, ILogger<QoreidWebhookController> logger)
    {
        _mediator = mediator;
        _config = config;
        _logger = logger;
    }

    [HttpPost("qoreid-webhook")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        string rawBody;
        using (var reader = new System.IO.StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        var signature = Request.Headers["X-QoreID-Signature"].FirstOrDefault() ?? string.Empty;
        var webhookSecret = _config["QoreId:WebhookSecret"] ?? string.Empty;

        _logger.LogInformation("QoreID webhook received. Processing payload.");

        var command = new ProcessQoreidWebhookCommand(rawBody, signature, webhookSecret);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("QoreID webhook handler fault: {Error}", result.Error.Message);
        }

        // Always return 200 so QoreID does not retry
        return Ok(new { received = true });
    }
}
