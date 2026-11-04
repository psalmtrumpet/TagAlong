using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TagAlong.Identity.API.DTOs;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Infrastructure.Persistence;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Services;

/// <summary>
/// Signed-in phones (one session each) and their refresh tokens, which
/// change on every refresh.
/// </summary>
public class RefreshSessions
{
    /// <summary>
    /// The same old token again this soon is the phone itself (two refreshes
    /// at once, or the answer got lost): it gets the same answer back.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    private readonly IdentityDbContext _db;
    private readonly IJwtService _jwt;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<RefreshSessions> _logger;

    public RefreshSessions(IdentityDbContext db, IJwtService jwt, IConfiguration config, IMemoryCache cache,
        IHttpContextAccessor http, ILogger<RefreshSessions> logger)
    {
        _db = db;
        _jwt = jwt;
        _config = config;
        _cache = cache;
        _http = http;
        _logger = logger;
    }

    private DateTime NewExpiry() =>
        DateTime.UtcNow.AddDays(int.Parse(_config["JwtSettings:RefreshTokenExpirationInDays"] ?? "7"));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>A new session for a sign-in on this phone; returns its refresh token. Saved by the caller.</summary>
    public async Task<string> IssueAsync(Guid userId, CancellationToken ct)
    {
        // Tidy up this account's long-finished sessions
        var old = DateTime.UtcNow.AddDays(-30);
        await _db.RefreshSessions.Where(s => s.UserId == userId && (s.ExpiresAt < old || s.RevokedAt < old))
            .ExecuteDeleteAsync(ct);

        var token = _jwt.GenerateRefreshToken();
        _db.RefreshSessions.Add(RefreshSession.Start(userId, Hash(token), NewExpiry(),
            _http.HttpContext?.Request.Headers.UserAgent.ToString()));
        return token;
    }

    public enum Outcome { Rotated, Repeat, Invalid, Reused }

    /// <summary>
    /// Swap a refresh token for a new one. <paramref name="build"/> makes the
    /// answer from the new refresh token; a repeat within <see cref="Grace"/>
    /// gets the same answer again.
    /// </summary>
    public async Task<(Outcome Outcome, AuthResponse? Response)> RotateAsync(
        Guid userId, string token, ApplicationUser user, Func<string, AuthResponse> build, CancellationToken ct)
    {
        var hash = Hash(token);
        var key = $"refresh-answer:{hash}";
        if (_cache.TryGetValue(key, out AuthResponse? again)) return (Outcome.Repeat, again);

        var session = await _db.RefreshSessions
            .FirstOrDefaultAsync(s => s.UserId == userId && (s.CurrentHash == hash || s.PreviousHash == hash), ct);

        if (session == null)
        {
            // From before sessions: the account's single shared token. Each phone
            // still holding it gets a session of its own (it isn't extended any more)
            if (user.RefreshToken == token && user.RefreshTokenExpiryTime > DateTime.UtcNow)
            {
                // (No repeat answer here: two phones sharing it must not share a session)
                var first = build(await IssueAsync(userId, ct));
                await _db.SaveChangesAsync(ct);
                return (Outcome.Rotated, first);
            }
            return (Outcome.Invalid, null);
        }

        if (!session.IsActive) return (Outcome.Invalid, null);

        if (session.PreviousHash == hash)
        {
            // Rotated a while ago yet the old token is back: someone else has a copy
            session.Revoke("reused");
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Refresh token reused for user {UserId}: session {SessionId} ended", userId, session.Id);
            return (Outcome.Reused, null);
        }

        var next = _jwt.GenerateRefreshToken();
        var now = DateTime.UtcNow;
        var expiry = NewExpiry();
        var nextHash = Hash(next);
        // Only one refresh can move the session on
        var won = await _db.RefreshSessions
            .Where(s => s.Id == session.Id && s.CurrentHash == hash && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.PreviousHash, hash)
                .SetProperty(x => x.CurrentHash, nextHash)
                .SetProperty(x => x.RotatedAt, now)
                .SetProperty(x => x.LastUsedAt, now)
                .SetProperty(x => x.ExpiresAt, expiry)
                .SetProperty(x => x.UpdatedAt, now), ct);
        if (won == 0)
        {
            // Another refresh with the same token got there first: wait for its answer
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(100, ct);
                if (_cache.TryGetValue(key, out AuthResponse? theirs)) return (Outcome.Repeat, theirs);
            }
            return (Outcome.Invalid, null);
        }

        var response = build(next);
        _cache.Set(key, response, Grace);
        return (Outcome.Rotated, response);
    }

    /// <summary>End the session this refresh token belongs to (sign out on this phone).</summary>
    public async Task<bool> RevokeAsync(Guid userId, string token, string reason, CancellationToken ct)
    {
        var hash = Hash(token);
        var n = await _db.RefreshSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && (s.CurrentHash == hash || s.PreviousHash == hash))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.RevokeReason, reason), ct);
        return n > 0;
    }

    /// <summary>End every session of this account, except the one holding <paramref name="keepToken"/>.</summary>
    public Task<int> RevokeAllAsync(Guid userId, string reason, CancellationToken ct, string? keepToken = null)
    {
        var keep = keepToken is { Length: > 0 } t ? Hash(t) : null;
        return _db.RefreshSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && (keep == null || s.CurrentHash != keep))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTime.UtcNow).SetProperty(x => x.RevokeReason, reason), ct);
    }

    /// <summary>How many phones are signed in to this account.</summary>
    public Task<int> CountActiveAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return _db.RefreshSessions.CountAsync(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now, ct);
    }
}
