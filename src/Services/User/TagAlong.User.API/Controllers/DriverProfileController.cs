using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Controllers;

public record SubmitDriverProfileRequest(
    string LicenseNumber,
    DateTime? LicenseExpiry,
    string? LicenseImageBase64,
    string VehicleType,
    string VehicleMake,
    string VehicleModel,
    string VehicleColor,
    string VehiclePlate,
    string? VehicleImageBase64,
    string? VehicleBackImageBase64 = null);

/// <summary>
/// Driver's licence and vehicle details. Required (and admin-approved) before a
/// user can offer rides or deliveries.
/// </summary>
[ApiController]
[Route("api/users/driver-profile")]
[Authorize]
public class DriverProfileController : ControllerBase
{
    private static readonly string[] VehicleTypes = { "Car", "SUV", "Bus", "Motorcycle", "Tricycle" };
    private const int MaxImageBytes = 6 * 1024 * 1024;

    private readonly UserDbContext _db;
    private readonly FileService _files;
    private readonly DriverDocumentReader _reader;
    private readonly ILogger<DriverProfileController> _logger;

    public DriverProfileController(UserDbContext db, FileService files, DriverDocumentReader reader, ILogger<DriverProfileController> logger)
    {
        _db = db;
        _files = files;
        _reader = reader;
        _logger = logger;
    }

    /// <summary>My driver profile status ("None" if never submitted).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var p = await _db.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.AuthUserId == userId, ct);
        if (p == null) return Ok(new { status = "None" });

        return Ok(new
        {
            status = p.Status.ToString(),
            p.RejectionReason,
            p.LicenseNumber,
            p.LicenseExpiry,
            p.VehicleType,
            p.VehicleMake,
            p.VehicleModel,
            p.VehicleColor,
            p.VehiclePlate,
            // Only photos we still have count as "on file"
            hasLicenseImage = _files.ResolvePath(p.LicenseImagePath) != null,
            hasVehicleImage = _files.ResolvePath(p.VehicleImagePath) != null,
            hasVehicleBackImage = _files.ResolvePath(p.VehicleBackImagePath) != null,
            p.SubmittedAt,
            p.ReviewedAt,
        });
    }

    /// <summary>Submit or update licence + vehicle details. Always goes back to review.</summary>
    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Submit([FromBody] SubmitDriverProfileRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.LicenseNumber) || req.LicenseNumber.Trim().Length < 5)
            return BadRequest(new { error = "Enter your driver's licence number." });
        if (req.LicenseExpiry is { } exp && exp.Date < DateTime.UtcNow.Date)
            return BadRequest(new { error = "Your driver's licence has expired." });
        if (!VehicleTypes.Contains(req.VehicleType))
            return BadRequest(new { error = "Choose your vehicle type." });
        if (string.IsNullOrWhiteSpace(req.VehicleMake) || string.IsNullOrWhiteSpace(req.VehicleModel))
            return BadRequest(new { error = "Enter your vehicle's make and model." });
        if (string.IsNullOrWhiteSpace(req.VehicleColor))
            return BadRequest(new { error = "Enter your vehicle's colour." });
        if (DriverProfile.NormalisePlate(req.VehiclePlate ?? "").Length < 5)
            return BadRequest(new { error = "Enter your vehicle's plate number." });

        var profile = await _db.DriverProfiles.FirstOrDefaultAsync(x => x.AuthUserId == userId, ct);
        var isNew = profile == null;
        // Only a new or rejected submission can be changed
        if (profile?.Status == DriverProfileStatus.Pending)
            return BadRequest(new { error = "Your details are being reviewed and can't be changed until the review is done." });
        if (profile?.Status == DriverProfileStatus.Approved)
            return BadRequest(new { error = "Your driver details are approved and can't be changed." });
        if (_files.ResolvePath(profile?.LicenseImagePath) == null && string.IsNullOrWhiteSpace(req.LicenseImageBase64))
            return BadRequest(new { error = "Add a photo of your driver's licence." });
        if (_files.ResolvePath(profile?.VehicleImagePath) == null && string.IsNullOrWhiteSpace(req.VehicleImageBase64))
            return BadRequest(new { error = "Add a photo of the front of your vehicle with the plate number showing." });
        if (_files.ResolvePath(profile?.VehicleBackImagePath) == null && string.IsNullOrWhiteSpace(req.VehicleBackImageBase64))
            return BadRequest(new { error = "Add a photo of the back of your vehicle with the plate number showing." });

        string? licensePath = null, vehiclePath = null, vehicleBackPath = null;
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        if (!string.IsNullOrWhiteSpace(req.LicenseImageBase64))
        {
            if (!IsReasonableImage(req.LicenseImageBase64)) return BadRequest(new { error = "The licence photo is too large or not an image." });
            licensePath = await _files.SaveBase64ImageAsync(req.LicenseImageBase64, $"driver-docs/{userId}", $"license-{stamp}");
            if (licensePath == null) return BadRequest(new { error = "Couldn't read the licence photo. Please try another." });
        }
        if (!string.IsNullOrWhiteSpace(req.VehicleImageBase64))
        {
            if (!IsReasonableImage(req.VehicleImageBase64)) return BadRequest(new { error = "The vehicle photo is too large or not an image." });
            vehiclePath = await _files.SaveBase64ImageAsync(req.VehicleImageBase64, $"driver-docs/{userId}", $"vehicle-{stamp}");
            if (vehiclePath == null) return BadRequest(new { error = "Couldn't read the front vehicle photo. Please try another." });
        }
        if (!string.IsNullOrWhiteSpace(req.VehicleBackImageBase64))
        {
            if (!IsReasonableImage(req.VehicleBackImageBase64)) return BadRequest(new { error = "The back vehicle photo is too large or not an image." });
            vehicleBackPath = await _files.SaveBase64ImageAsync(req.VehicleBackImageBase64, $"driver-docs/{userId}", $"vehicle-back-{stamp}");
            if (vehicleBackPath == null) return BadRequest(new { error = "Couldn't read the back vehicle photo. Please try another." });
        }

        if (isNew)
        {
            profile = DriverProfile.Create(userId.Value);
            _db.DriverProfiles.Add(profile);
        }
        profile!.Submit(req.LicenseNumber, req.LicenseExpiry, licensePath, req.VehicleType,
            req.VehicleMake, req.VehicleModel, req.VehicleColor, req.VehiclePlate, vehiclePath, vehicleBackPath);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Driver profile submitted for {UserId} ({Plate})", userId, profile.VehiclePlate);

        // Read the documents in the background to help the reviewer
        _reader.ReadInBackground(userId.Value);

        return Ok(new { status = profile.Status.ToString() });
    }

    /// <summary>
    /// A driver's vehicle — shown to their passengers so they can check the
    /// plate before getting in. Only approved vehicles are returned.
    /// </summary>
    [HttpGet("{userId:guid}/vehicle")]
    public async Task<IActionResult> GetVehicle(Guid userId, CancellationToken ct)
    {
        var p = await _db.DriverProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AuthUserId == userId && x.Status == DriverProfileStatus.Approved, ct);
        if (p == null) return Ok(new { approved = false });

        return Ok(new
        {
            approved = true,
            p.VehicleType,
            p.VehicleMake,
            p.VehicleModel,
            p.VehicleColor,
            p.VehiclePlate,
        });
    }

    private static bool IsReasonableImage(string base64)
    {
        var data = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
        if (data.Length * 3L / 4 > MaxImageBytes) return false;
        try
        {
            var head = Convert.FromBase64String(data[..(Math.Min(data.Length, 16) / 4 * 4)]);
            // JPEG FF D8 or PNG 89 50
            return head.Length >= 2 && ((head[0] == 0xFF && head[1] == 0xD8) || (head[0] == 0x89 && head[1] == 0x50));
        }
        catch { return false; }
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
