using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Controllers;

public record RejectDriverRequest(string Reason);

/// <summary>Admin review of drivers' licence and vehicle documents.</summary>
[ApiController]
[Route("api/admin/users/driver-profiles")]
[Authorize(Roles = "Admin")]
public class AdminDriverProfilesController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly FileService _files;
    private readonly DriverDocumentReader _reader;
    private readonly KycPhotoService _photos;
    private readonly DriverReviewNotifier _notifier;

    public AdminDriverProfilesController(UserDbContext db, FileService files, DriverDocumentReader reader, KycPhotoService photos, DriverReviewNotifier notifier)
    {
        _notifier = notifier;
        _db = db;
        _files = files;
        _reader = reader;
        _photos = photos;
    }

    /// <summary>Drivers by review status (default: waiting for review).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string status = "Pending", CancellationToken ct = default)
    {
        var query = _db.DriverProfiles.AsNoTracking();
        if (Enum.TryParse<DriverProfileStatus>(status, true, out var s)) query = query.Where(d => d.Status == s);

        var rows = await query
            .OrderBy(d => d.SubmittedAt)
            .Join(_db.UserProfiles.AsNoTracking(), d => d.AuthUserId, p => p.AuthUserId, (d, p) => new
            {
                d.AuthUserId,
                name = p.FirstName + " " + p.LastName,
                p.Email,
                p.PhoneNumber,
                p.IsVerified,
                status = d.Status.ToString(),
                vehicle = d.VehicleColor + " " + d.VehicleMake + " " + d.VehicleModel,
                d.VehiclePlate,
                d.SubmittedAt,
                d.DocumentCheckFailures,
                documentsChecked = d.DocumentCheckedAt != null,
            })
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("{authUserId:guid}")]
    public async Task<IActionResult> Get(Guid authUserId, CancellationToken ct)
    {
        var d = await _db.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.AuthUserId == authUserId, ct);
        if (d == null) return NotFound();
        var p = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.AuthUserId == authUserId, ct);

        return Ok(new
        {
            d.AuthUserId,
            name = p == null ? null : $"{p.FirstName} {p.LastName}",
            p?.Email,
            p?.PhoneNumber,
            isVerified = p?.IsVerified ?? false,
            status = d.Status.ToString(),
            d.RejectionReason,
            d.LicenseNumber,
            d.LicenseExpiry,
            d.VehicleType,
            d.VehicleMake,
            d.VehicleModel,
            d.VehicleColor,
            d.VehiclePlate,
            hasVehicleImage = d.VehicleImagePath != null,
            hasVehicleBackImage = d.VehicleBackImagePath != null,
            d.SubmittedAt,
            d.ReviewedAt,
            documentCheck = d.DocumentCheckJson == null ? (JsonElement?)null : JsonDocument.Parse(d.DocumentCheckJson).RootElement.Clone(),
            d.DocumentCheckedAt,
        });
    }

    [HttpGet("{authUserId:guid}/license-image")]
    public Task<IActionResult> LicenseImage(Guid authUserId, CancellationToken ct) => Image(authUserId, d => d.LicenseImagePath, ct);

    [HttpGet("{authUserId:guid}/vehicle-image")]
    public Task<IActionResult> VehicleImage(Guid authUserId, CancellationToken ct) => Image(authUserId, d => d.VehicleImagePath, ct);

    [HttpGet("{authUserId:guid}/vehicle-back-image")]
    public Task<IActionResult> VehicleBackImage(Guid authUserId, CancellationToken ct) => Image(authUserId, d => d.VehicleBackImagePath, ct);

    private async Task<IActionResult> Image(Guid authUserId, Func<DriverProfile, string?> pick, CancellationToken ct)
    {
        var d = await _db.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.AuthUserId == authUserId, ct);
        var file = _files.ResolvePath(d == null ? null : pick(d));
        if (file == null) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(file, "image/jpeg");
    }

    [HttpPost("{authUserId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid authUserId, CancellationToken ct)
    {
        var d = await _db.DriverProfiles.FirstOrDefaultAsync(x => x.AuthUserId == authUserId, ct);
        if (d == null) return NotFound();
        d.Approve(AdminId());
        await _db.SaveChangesAsync(ct);
        await _notifier.NotifyAsync(authUserId, approved: true, Array.Empty<string>(), ct);
        return Ok(new { status = d.Status.ToString() });
    }

    [HttpPost("{authUserId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid authUserId, [FromBody] RejectDriverRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) return BadRequest(new { error = "Give the driver a reason so they can fix it." });
        var d = await _db.DriverProfiles.FirstOrDefaultAsync(x => x.AuthUserId == authUserId, ct);
        if (d == null) return NotFound();
        d.Reject(AdminId(), req.Reason);
        await _db.SaveChangesAsync(ct);
        await _notifier.NotifyAsync(authUserId, approved: false, new[] { req.Reason.Trim() }, ct);
        return Ok(new { status = d.Status.ToString() });
    }

    /// <summary>Run the automatic document read again (e.g. after configuring the API key).</summary>
    [HttpPost("{authUserId:guid}/recheck")]
    public async Task<IActionResult> Recheck(Guid authUserId, CancellationToken ct)
    {
        await _reader.ReadAsync(authUserId, ct);
        return await Get(authUserId, ct);
    }

    /// <summary>Save verification selfies for users verified before this was added.</summary>
    [HttpPost("/api/admin/users/kyc-photos/backfill")]
    public async Task<IActionResult> BackfillKycPhotos(CancellationToken ct)
    {
        var userIds = await _db.UserProfiles.AsNoTracking()
            .Where(p => p.IsVerified)
            .Select(p => p.AuthUserId)
            .ToListAsync(ct);

        int saved = 0, missing = 0;
        foreach (var id in userIds)
        {
            if (await _photos.CaptureAsync(id, ct)) saved++; else missing++;
        }
        return Ok(new { verifiedUsers = userIds.Count, withPhoto = saved, noPhotoAvailable = missing });
    }

    private Guid AdminId() =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;
}
