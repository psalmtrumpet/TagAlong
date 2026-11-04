using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TagAlong.User.Infrastructure.Persistence;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Controllers;

/// <summary>
/// A user's verification selfie — lets a passenger see who is picking them up.
/// Never a public URL: only the person themselves, admins, and people they
/// have a ride or a ride request with can see it (it's face data).
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class PhotosController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly FileService _files;
    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PhotosController> _logger;

    public PhotosController(UserDbContext db, FileService files, IHttpClientFactory http, IMemoryCache cache,
        ILogger<PhotosController> logger)
    {
        _db = db;
        _files = files;
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("{authUserId:guid}/photo")]
    public async Task<IActionResult> GetPhoto(Guid authUserId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var me)) return Unauthorized();
        if (me != authUserId && !User.IsInRole("Admin") && !await SharedRideAsync(me, authUserId, ct))
            return StatusCode(StatusCodes.Status403Forbidden);

        var path = await _db.UserProfiles.AsNoTracking()
            .Where(p => p.AuthUserId == authUserId && p.IsVerified)
            .Select(p => p.IdentityDocumentUrl)
            .FirstOrDefaultAsync(ct);

        var file = _files.ResolvePath(path);
        if (file == null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=3600";
        return PhysicalFile(file, "image/jpeg");
    }

    /// <summary>Do these two have a ride or a ride request between them? (Asked of the messaging service.)</summary>
    private async Task<bool> SharedRideAsync(Guid a, Guid b, CancellationToken ct)
    {
        var key = a.CompareTo(b) < 0 ? $"photo-rel:{a}:{b}" : $"photo-rel:{b}:{a}";
        if (_cache.TryGetValue(key, out bool known)) return known;
        try
        {
            using var res = await _http.CreateClient("messaging-api").GetAsync($"internal/relations/{a}/{b}", ct);
            if (!res.IsSuccessStatusCode) return false;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var related = doc.RootElement.GetProperty("related").GetBoolean();
            // Once linked they stay linked; a "no" is re-checked soon (a request may arrive any moment)
            _cache.Set(key, related, related ? TimeSpan.FromHours(6) : TimeSpan.FromSeconds(20));
            return related;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Can't tell: don't show the face
            _logger.LogWarning(ex, "Couldn't check whether {A} and {B} share a ride", a, b);
            return false;
        }
    }
}
