using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Controllers;

/// <param name="HasOngoingTrip">Someone is on board right now (Messaging decides if they're nearly done).</param>
public record AvailableDriverDto(
    Guid UserId,
    string Name,
    decimal AverageRating,
    int TotalRatings,
    double Latitude,
    double Longitude,
    bool HasOngoingTrip,
    string Vehicle,
    string VehiclePlate);

/// <summary>
/// Service-to-service calls on the internal network only (the gateway and nginx
/// route nothing to /internal).
/// </summary>
[ApiController]
[Route("internal/availability")]
[AllowAnonymous]
public class InternalAvailabilityController : ControllerBase
{
    /// <summary>Idle drivers must have shared their position this recently.</summary>
    private static readonly TimeSpan MaxLocationAge = TimeSpan.FromMinutes(30);

    private readonly UserDbContext _db;

    public InternalAvailabilityController(UserDbContext db) => _db = db;

    /// <summary>
    /// Approved drivers who are available within <paramref name="radiusKm"/>
    /// (straight line) of a point — candidates for an on-demand ride request.
    /// <paramref name="alsoIds"/>: drivers to include wherever their profile says
    /// they are (Messaging knows they're nearby from their live ride position).
    /// </summary>
    [HttpGet("drivers-near")]
    public async Task<IActionResult> DriversNear(double lat, double lng, double radiusKm = 15, string? alsoIds = null, CancellationToken ct = default)
    {
        var also = (alsoIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
        var now = DateTime.UtcNow;
        var fresh = now - MaxLocationAge;
        var dLat = radiusKm / 111.0;
        var dLng = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180));

        var profiles = await _db.UserProfiles.AsNoTracking()
            .Where(u => u.IsAvailable && !u.IsSuspended && u.IsVerified)
            .Where(u => u.CurrentLatitude.HasValue && u.CurrentLongitude.HasValue)
            .Where(u => also.Contains(u.AuthUserId) ||
                        (u.CurrentLatitude >= lat - dLat && u.CurrentLatitude <= lat + dLat &&
                         u.CurrentLongitude >= lng - dLng && u.CurrentLongitude <= lng + dLng))
            .Where(u => u.HasOngoingTrip || !u.AvailabilityExpiresAt.HasValue || u.AvailabilityExpiresAt > now)
            .Where(u => u.HasOngoingTrip || (u.LocationUpdatedAt.HasValue && u.LocationUpdatedAt > fresh))
            .ToListAsync(ct);
        if (profiles.Count == 0) return Ok(new List<AvailableDriverDto>());

        var ids = profiles.Select(p => p.AuthUserId).ToList();
        var vehicles = await _db.DriverProfiles.AsNoTracking()
            .Where(d => ids.Contains(d.AuthUserId) && d.Status == DriverProfileStatus.Approved)
            .ToDictionaryAsync(d => d.AuthUserId, ct);

        var drivers = profiles
            .Where(p => vehicles.ContainsKey(p.AuthUserId))
            .Where(p => also.Contains(p.AuthUserId) || p.DistanceFromKm(lat, lng) <= radiusKm)
            .OrderBy(p => p.DistanceFromKm(lat, lng))
            .Take(100)
            .Select(p =>
            {
                var v = vehicles[p.AuthUserId];
                var car = string.Join(" ", new[] { v.VehicleColor, v.VehicleMake, v.VehicleModel }.Where(x => !string.IsNullOrWhiteSpace(x)));
                return new AvailableDriverDto(
                    p.AuthUserId, $"{p.FirstName} {p.LastName}".Trim(), p.AverageRating, p.TotalRatings,
                    p.CurrentLatitude!.Value, p.CurrentLongitude!.Value, p.HasOngoingTrip, car, v.VehiclePlate);
            })
            .ToList();
        return Ok(drivers);
    }
}
