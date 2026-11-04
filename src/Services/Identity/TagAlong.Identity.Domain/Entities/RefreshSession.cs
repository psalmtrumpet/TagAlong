using TagAlong.Common.Domain;

namespace TagAlong.Identity.Domain.Entities;

/// <summary>
/// One signed-in phone. Its refresh token changes on every refresh; only
/// hashes are kept. The previous token is remembered so a copy being used
/// after the phone moved on can be spotted (and the session ended).
/// </summary>
public class RefreshSession : Entity
{
    public Guid UserId { get; private set; }
    public string CurrentHash { get; private set; } = string.Empty;
    public string? PreviousHash { get; private set; }
    public DateTime? RotatedAt { get; private set; }
    public DateTime LastUsedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokeReason { get; private set; }
    public string? UserAgent { get; private set; }

    private RefreshSession() { }

    public static RefreshSession Start(Guid userId, string tokenHash, DateTime expiresAt, string? userAgent) => new()
    {
        UserId = userId,
        CurrentHash = tokenHash,
        LastUsedAt = DateTime.UtcNow,
        ExpiresAt = expiresAt,
        UserAgent = userAgent is { Length: > 200 } ua ? ua[..200] : userAgent,
    };

    public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;

    public void Revoke(string reason)
    {
        if (RevokedAt != null) return;
        RevokedAt = DateTime.UtcNow;
        RevokeReason = reason;
        SetUpdated();
    }
}
