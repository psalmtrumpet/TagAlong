using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Controllers;

public record ApplyReferralRequest(string Code);

/// <summary>The signed-in user's referral code, rewards and wallet.</summary>
[ApiController]
[Route("api/users/referrals")]
[Authorize]
public class ReferralsController : ControllerBase
{
    private readonly ReferralService _referrals;
    private readonly UserDbContext _db;

    public ReferralsController(ReferralService referrals, UserDbContext db)
    {
        _referrals = referrals;
        _db = db;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var account = await _referrals.GetOrCreateAccountAsync(userId.Value, ct);
        var mine = await _db.Referrals.AsNoTracking().Where(r => r.ReferrerUserId == userId.Value).ToListAsync(ct);
        var usedCode = await _db.Referrals.AsNoTracking()
            .Where(r => r.ReferredUserId == userId.Value).Select(r => r.Code).FirstOrDefaultAsync(ct);
        var transactions = await _db.WalletTransactions.AsNoTracking()
            .Where(t => t.AuthUserId == userId.Value)
            .OrderByDescending(t => t.CreatedAt).Take(50)
            .Select(t => new { t.Amount, t.Description, t.CreatedAt })
            .ToListAsync(ct);

        return Ok(new
        {
            code = account.Code,
            successfulReferrals = mine.Count(r => r.QualifiedAt != null),
            pendingReferrals = mine.Count(r => r.QualifiedAt == null),
            freeTripCredits = account.FreeTripCredits,
            walletBalance = account.WalletBalance,
            riderCredit = _referrals.RiderCredit,
            usedCode,
            canApplyCode = usedCode == null && await _referrals.CanApplyCodeAsync(userId.Value, ct),
            transactions,
        });
    }

    /// <summary>Enter someone's code — once, before your first ride.</summary>
    [HttpPost("me/apply")]
    public async Task<IActionResult> Apply([FromBody] ApplyReferralRequest req, CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();
        var error = await _referrals.ApplyCodeAsync(userId.Value, req.Code, ct);
        return error == null ? Ok(new { applied = true }) : BadRequest(new { error });
    }

    private Guid? CurrentUserId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}

/// <summary>
/// Service-to-service calls on the internal network only (the gateway and nginx
/// route nothing to /internal).
/// </summary>
[ApiController]
[Route("internal/referrals")]
[AllowAnonymous]
public class InternalReferralsController : ControllerBase
{
    private readonly ReferralService _referrals;

    public InternalReferralsController(ReferralService referrals) => _referrals = referrals;

    /// <summary>Messaging asks this when a driver completes a ride: was it fee-free?</summary>
    [HttpPost("{userId:guid}/use-free-trip")]
    public async Task<IActionResult> UseFreeTrip(Guid userId, CancellationToken ct) =>
        Ok(new { used = await _referrals.UseFreeTripAsync(userId, ct) });
}
