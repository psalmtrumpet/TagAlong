using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.User.Infrastructure.Persistence;
using TagAlong.User.Infrastructure.Services;

namespace TagAlong.User.API.Controllers;

/// <summary>
/// A user's verification selfie — lets a passenger see who is picking them up.
/// Served only to signed-in users, never as a public URL.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class PhotosController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly FileService _files;

    public PhotosController(UserDbContext db, FileService files)
    {
        _db = db;
        _files = files;
    }

    [HttpGet("{authUserId:guid}/photo")]
    public async Task<IActionResult> GetPhoto(Guid authUserId, CancellationToken ct)
    {
        var path = await _db.UserProfiles.AsNoTracking()
            .Where(p => p.AuthUserId == authUserId && p.IsVerified)
            .Select(p => p.IdentityDocumentUrl)
            .FirstOrDefaultAsync(ct);

        var file = _files.ResolvePath(path);
        if (file == null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=3600";
        return PhysicalFile(file, "image/jpeg");
    }
}
