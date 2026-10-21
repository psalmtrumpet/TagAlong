using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.API.Services;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Controllers;

public record EmergencyContactDto(string Name, string Phone);

public record SafetyRecordingDto(Guid Id, Guid UserId, Guid? ConversationId, long SizeBytes, int DurationSeconds, bool Flagged, DateTime CreatedAt);

/// <param name="LicenceValidated">Driver's licence checked and approved.</param>
/// <param name="VehiclePhotosChecked">Photos of the car and plate approved.</param>
/// <param name="SelfieVerified">Identity confirmed with a live selfie.</param>
public record DriverVerificationsDto(
    Guid UserId,
    string Name,
    decimal Rating,
    int TotalRatings,
    int CompletedTrips,
    string Vehicle,
    string VehiclePlate,
    bool LicenceValidated,
    bool VehiclePhotosChecked,
    bool SelfieVerified);

/// <summary>Ride safety: emergency contacts, audio recordings, driver checks.</summary>
[ApiController]
[Route("api/users/safety")]
[Authorize]
public class SafetyController : ControllerBase
{
    private const int MaxContacts = 5;

    private readonly UserDbContext _db;
    private readonly RecordingStore _recordings;

    public SafetyController(UserDbContext db, RecordingStore recordings)
    {
        _db = db;
        _recordings = recordings;
    }

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts(CancellationToken ct)
    {
        var me = Me;
        if (me == null) return Unauthorized();
        var list = await _db.EmergencyContacts.AsNoTracking()
            .Where(c => c.AuthUserId == me.Value).OrderBy(c => c.CreatedAt)
            .Select(c => new EmergencyContactDto(c.Name, c.Phone)).ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>Replaces the user's emergency contacts (up to five).</summary>
    [HttpPut("contacts")]
    public async Task<IActionResult> SetContacts([FromBody] List<EmergencyContactDto> contacts, CancellationToken ct)
    {
        var me = Me;
        if (me == null) return Unauthorized();
        var clean = (contacts ?? new())
            .Select(c => (Name: (c.Name ?? "").Trim(), Phone: new string((c.Phone ?? "").Where(ch => char.IsDigit(ch) || ch == '+').ToArray())))
            .Where(c => c.Phone.Length >= 7)
            .ToList();
        if (clean.Count > MaxContacts) return BadRequest(new { error = $"You can add up to {MaxContacts} contacts." });

        _db.EmergencyContacts.RemoveRange(_db.EmergencyContacts.Where(c => c.AuthUserId == me.Value));
        foreach (var c in clean)
            _db.EmergencyContacts.Add(EmergencyContact.Create(me.Value, c.Name.Length > 0 ? c.Name[..Math.Min(100, c.Name.Length)] : c.Phone, c.Phone));
        await _db.SaveChangesAsync(ct);
        return Ok(clean.Select(c => new EmergencyContactDto(c.Name, c.Phone)));
    }

    /// <summary>Upload a ride's audio recording (encrypted at rest).</summary>
    [HttpPost("recordings")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> UploadRecording([FromForm] IFormFile audio, [FromForm] Guid? conversationId,
        [FromForm] int durationSeconds, CancellationToken ct)
    {
        var me = Me;
        if (me == null) return Unauthorized();
        if (audio == null || audio.Length == 0) return BadRequest(new { error = "No audio." });

        var id = Guid.NewGuid();
        await using var stream = audio.OpenReadStream();
        var path = await _recordings.SaveAsync(me.Value, id, stream, ct);
        var rec = SafetyRecording.Create(id, me.Value, conversationId, path, audio.Length, Math.Max(0, durationSeconds));
        _db.SafetyRecordings.Add(rec);
        await _db.SaveChangesAsync(ct);
        return Ok(new { id });
    }

    /// <summary>The user reported a problem on this ride: keep its recordings for the investigation.</summary>
    [HttpPost("recordings/flag")]
    public async Task<IActionResult> FlagRecordings([FromQuery] Guid conversationId, CancellationToken ct)
    {
        var me = Me;
        if (me == null) return Unauthorized();
        var recs = await _db.SafetyRecordings.Where(r => r.AuthUserId == me.Value && r.ConversationId == conversationId).ToListAsync(ct);
        foreach (var r in recs) r.Flag();
        await _db.SaveChangesAsync(ct);
        return Ok(new { flagged = recs.Count });
    }

    /// <summary>What we've checked about a driver, for passengers.</summary>
    [HttpGet("drivers/{userId:guid}")]
    public async Task<IActionResult> DriverVerifications(Guid userId, [FromServices] FileService files, CancellationToken ct)
    {
        var profile = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(u => u.AuthUserId == userId, ct);
        if (profile == null) return NotFound();
        var driver = await _db.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(d => d.AuthUserId == userId, ct);
        var approved = driver?.Status == DriverProfileStatus.Approved;
        var car = driver == null ? "" : string.Join(" ", new[] { driver.VehicleColor, driver.VehicleMake, driver.VehicleModel }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return Ok(new DriverVerificationsDto(
            userId,
            $"{profile.FirstName} {profile.LastName}".Trim(),
            profile.AverageRating,
            profile.TotalRatings,
            profile.CompletedTrips,
            car,
            driver?.VehiclePlate ?? "",
            LicenceValidated: approved,
            VehiclePhotosChecked: approved && files.ResolvePath(driver!.VehicleImagePath) != null,
            SelfieVerified: profile.IsVerified));
    }

    private Guid? Me => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}

/// <summary>Admins: list and play back ride safety recordings (when looking into a report).</summary>
[ApiController]
[Route("api/admin/users/recordings")]
[Authorize(Roles = "Admin")]
public class AdminRecordingsController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly RecordingStore _recordings;
    private readonly ILogger<AdminRecordingsController> _logger;

    public AdminRecordingsController(UserDbContext db, RecordingStore recordings, ILogger<AdminRecordingsController> logger)
    {
        _db = db;
        _recordings = recordings;
        _logger = logger;
    }

    /// <summary>Recordings — for a ride, by a user, or (no filter) the most recent 200.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? conversationId, [FromQuery] Guid? userId, CancellationToken ct)
    {
        var q = _db.SafetyRecordings.AsNoTracking();
        if (conversationId != null) q = q.Where(r => r.ConversationId == conversationId);
        if (userId != null) q = q.Where(r => r.AuthUserId == userId);
        var list = await q.OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .Select(r => new SafetyRecordingDto(r.Id, r.AuthUserId, r.ConversationId, r.SizeBytes, r.DurationSeconds, r.Flagged, r.CreatedAt))
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}/audio")]
    public async Task<IActionResult> Audio(Guid id, CancellationToken ct)
    {
        var rec = await _db.SafetyRecordings.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rec == null) return NotFound();
        var bytes = await _recordings.ReadAsync(rec.FilePath, ct);
        if (bytes == null) return NotFound(new { error = "The recording file is missing." });
        _logger.LogInformation("Admin {AdminId} played safety recording {RecordingId}",
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value, id);
        return File(bytes, "audio/mp4", $"ride-recording-{id:N}.m4a");
    }
}
