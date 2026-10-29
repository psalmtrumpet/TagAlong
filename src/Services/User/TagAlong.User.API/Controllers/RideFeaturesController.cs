using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Controllers;

public record RideFeatureDto(int Id, string Name, string Icon, int SortOrder, bool IsActive);
public record SaveRideFeatureRequest(string? Name, string? Icon, int? SortOrder, bool? IsActive);
public record SetRideFeaturesRequest(List<int>? RideFeatureIds);

/// <summary>
/// What a driver's car can offer (air conditioning, tinted windows, music…).
/// The list comes from the admin portal, so new ones need no app release.
/// </summary>
[ApiController]
[Authorize]
public class RideFeaturesController : ControllerBase
{
    private readonly UserDbContext _db;

    public RideFeaturesController(UserDbContext db) => _db = db;

    /// <summary>Every feature, hidden ones too (trips that already have them still show them).</summary>
    [HttpGet("api/users/ride-features")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await AllAsync(ct));

    /// <summary>The driver ticks what their car offers (an empty list clears it). Optional.</summary>
    [HttpPut("api/users/availability/me/features")]
    public async Task<IActionResult> SetMine([FromBody] SetRideFeaturesRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return Unauthorized();
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.AuthUserId == userId, ct);
        if (profile == null) return NotFound(new { error = "Profile not found" });

        var wanted = request.RideFeatureIds ?? new();
        var known = await _db.RideFeatures.AsNoTracking()
            .Where(f => f.IsActive && wanted.Contains(f.Id)).Select(f => f.Id).ToListAsync(ct);
        // Keep the driver's order, drop anything unknown or hidden
        profile.SetRideFeatures(wanted.Where(known.Contains));
        await _db.SaveChangesAsync(ct);
        return Ok(new { rideFeatureIds = RideFeature.ParseIds(profile.RideFeatures) });
    }

    // ── Admin ──

    [HttpGet("api/admin/users/ride-features")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminList(CancellationToken ct) => Ok(await AllAsync(ct));

    [HttpPost("api/admin/users/ride-features")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] SaveRideFeatureRequest request, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 2 or > 60) return BadRequest(new { error = "Give the feature a name (2–60 characters)." });
        if (await _db.RideFeatures.AnyAsync(f => f.Name == name, ct))
            return BadRequest(new { error = "There's already a feature with that name." });
        var order = request.SortOrder ?? (await _db.RideFeatures.MaxAsync(f => (int?)f.SortOrder, ct) ?? 0) + 1;
        var feature = RideFeature.Create(name, CleanIcon(request.Icon), order);
        _db.RideFeatures.Add(feature);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(feature));
    }

    [HttpPut("api/admin/users/ride-features/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveRideFeatureRequest request, CancellationToken ct)
    {
        var feature = await _db.RideFeatures.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (feature == null) return NotFound(new { error = "Feature not found" });
        var name = request.Name?.Trim() ?? feature.Name;
        if (name.Length is < 2 or > 60) return BadRequest(new { error = "Give the feature a name (2–60 characters)." });
        if (await _db.RideFeatures.AnyAsync(f => f.Id != id && f.Name == name, ct))
            return BadRequest(new { error = "There's already a feature with that name." });
        feature.Update(name, request.Icon == null ? feature.Icon : CleanIcon(request.Icon),
            request.SortOrder ?? feature.SortOrder, request.IsActive ?? feature.IsActive);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(feature));
    }

    /// <summary>Removes it everywhere, including trips that had it. Hiding keeps those.</summary>
    [HttpDelete("api/admin/users/ride-features/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var feature = await _db.RideFeatures.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (feature == null) return NotFound(new { error = "Feature not found" });
        _db.RideFeatures.Remove(feature);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<List<RideFeatureDto>> AllAsync(CancellationToken ct) =>
        (await _db.RideFeatures.AsNoTracking().OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToListAsync(ct))
            .Select(ToDto).ToList();

    private static RideFeatureDto ToDto(RideFeature f) => new(f.Id, f.Name, f.Icon, f.SortOrder, f.IsActive);

    /// <summary>Lowercase letters, digits and underscores; empty means "a tick".</summary>
    private static string CleanIcon(string? icon)
    {
        var s = new string((icon ?? "").Trim().ToLowerInvariant().Where(c => char.IsAsciiLetterOrDigit(c) || c == '_').ToArray());
        return s.Length == 0 ? "check" : s[..Math.Min(30, s.Length)];
    }
}
