using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Infrastructure.Persistence;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Controllers;

[ApiController]
[Route("api/admin/auth")]
public class AdminController : ControllerBase
{
    private readonly IdentityDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _config;

    public AdminController(IdentityDbContext db, IPasswordService passwords, IJwtService jwt, IConfiguration config)
    {
        _db = db;
        _passwords = passwords;
        _jwt = jwt;
        _config = config;
    }

    /// <summary>
    /// One-time endpoint to seed the first admin user.
    /// Protected by AdminSeedSecret env var — remove or disable after first use.
    /// POST /api/admin/auth/seed  { email, password, firstName, lastName, adminSecret }
    /// </summary>
    [HttpPost("seed")]
    [AllowAnonymous]
    public async Task<IActionResult> SeedAdmin([FromBody] SeedAdminRequest request, CancellationToken cancellationToken)
    {
        var expectedSecret = _config["Admin:SeedSecret"];
        if (string.IsNullOrEmpty(expectedSecret) || request.AdminSecret != expectedSecret)
            return Unauthorized(new { error = "Invalid admin seed secret" });

        var existing = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken);

        if (existing != null)
        {
            if (existing.Role == "Admin")
                return Conflict(new { error = "Admin user already exists" });

            existing.SetRole("Admin");
            _db.Users.Update(existing);
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Existing user promoted to Admin", userId = existing.Id });
        }

        var hash = _passwords.HashPassword(request.Password);
        var user = ApplicationUser.Create(request.Email, hash, request.FirstName, request.LastName, request.PhoneNumber ?? "0000000000");
        user.SetRole("Admin");
        user.VerifyEmail();

        await _db.Users.AddAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Admin user created", userId = user.Id });
    }

    /// <summary>
    /// List all admin users. Requires Admin JWT.
    /// </summary>
    [HttpGet("admins")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListAdmins(CancellationToken cancellationToken)
    {
        var admins = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.Role == "Admin")
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName, u.CreatedAt })
            .ToListAsync(cancellationToken);

        return Ok(admins);
    }

    /// <summary>
    /// Promote an existing user to Admin. Requires Admin JWT.
    /// </summary>
    [HttpPost("{userId:guid}/promote")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PromoteToAdmin(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return NotFound(new { error = "User not found" });

        user.SetRole("Admin");
        _db.Users.Update(user);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "User promoted to Admin" });
    }

    /// <summary>
    /// Demote admin back to regular user.
    /// </summary>
    [HttpPost("{userId:guid}/demote")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DemoteAdmin(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return NotFound(new { error = "User not found" });

        user.SetRole("User");
        _db.Users.Update(user);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Admin demoted to User" });
    }
}

public record SeedAdminRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string AdminSecret,
    string? PhoneNumber = null);
