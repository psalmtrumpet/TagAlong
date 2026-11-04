using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.API.Controllers;

/// <summary>
/// For uptime monitors (GET or HEAD, no sign-in): 200 when the gateway, this
/// service and its database all answer, 503 when the database doesn't.
/// </summary>
[ApiController]
[Route("api/public/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly MessagingDbContext _db;

    public HealthController(MessagingDbContext db) => _db = db;

    [HttpGet]
    [HttpHead]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return await _db.Database.CanConnectAsync(ct)
            ? Ok(new { status = "ok" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "database unavailable" });
    }
}
