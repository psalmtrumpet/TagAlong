using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Controllers;

/// <summary>
/// Service-to-service calls on the internal network only (the gateway and nginx
/// route nothing to /internal).
/// </summary>
[ApiController]
[Route("internal/users")]
[AllowAnonymous]
public class InternalUsersController : ControllerBase
{
    private readonly UserDbContext _db;

    public InternalUsersController(UserDbContext db) => _db = db;

    /// <summary>
    /// A user's phone number, for Messaging to place a call between a driver and
    /// passenger on the same ride (Messaging checks that; the number is dialled,
    /// never shown).
    /// </summary>
    [HttpGet("{authUserId:guid}/phone")]
    public async Task<IActionResult> Phone(Guid authUserId, CancellationToken ct)
    {
        var phone = await _db.UserProfiles.AsNoTracking()
            .Where(p => p.AuthUserId == authUserId)
            .Select(p => p.PhoneNumber)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(phone) ? NotFound() : Ok(new { phone });
    }
}
