namespace TagAlong.User.Domain.Entities;

/// <summary>
/// A user's referral code and the rewards it has earned: fee-free rides (for
/// drivers) and wallet credit (for riders).
/// </summary>
public class ReferralAccount
{
    public Guid Id { get; private set; }
    public Guid AuthUserId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    /// <summary>Completed rides that won't be charged TagAlong's fee.</summary>
    public int FreeTripCredits { get; private set; }
    public decimal WalletBalance { get; private set; }
    /// <summary>Referral points: 2 for each person who joins with this code and completes a ride.</summary>
    public int Points { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ReferralAccount() { }

    public static ReferralAccount Create(Guid authUserId, string code) => new()
    {
        Id = Guid.NewGuid(),
        AuthUserId = authUserId,
        Code = code,
        CreatedAt = DateTime.UtcNow,
    };

    public void AddFreeTrip() => FreeTripCredits++;

    /// <summary>Returns false when there's no free ride to use.</summary>
    public bool UseFreeTrip()
    {
        if (FreeTripCredits <= 0) return false;
        FreeTripCredits--;
        return true;
    }

    public void Credit(decimal amount) => WalletBalance += amount;

    public void AddPoints(int points) => Points += points;
}

public enum ReferralReward
{
    None,
    FreeTrip,
    WalletCredit,
    Points,
}

/// <summary>
/// One person signing up with someone's code. The referrer is rewarded for
/// every ride the referred person completes; QualifiedAt is their first one.
/// </summary>
public class Referral
{
    public Guid Id { get; private set; }
    public Guid ReferrerUserId { get; private set; }
    public Guid ReferredUserId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? QualifiedAt { get; private set; }
    public ReferralReward Reward { get; private set; }
    public decimal RewardAmount { get; private set; }

    private Referral() { }

    public static Referral Create(Guid referrerUserId, Guid referredUserId, string code) => new()
    {
        Id = Guid.NewGuid(),
        ReferrerUserId = referrerUserId,
        ReferredUserId = referredUserId,
        Code = code,
        CreatedAt = DateTime.UtcNow,
    };

    public bool IsQualified => QualifiedAt != null;

    public void Qualify(ReferralReward reward, decimal amount)
    {
        QualifiedAt = DateTime.UtcNow;
        Reward = reward;
        RewardAmount = amount;
    }
}

/// <summary>The reward a referrer got for one ride completed by someone they referred.</summary>
public class ReferralEarning
{
    public Guid Id { get; private set; }
    public Guid ReferralId { get; private set; }
    public Guid ReferrerUserId { get; private set; }
    public Guid ReferredUserId { get; private set; }
    public Guid ConversationId { get; private set; }
    public ReferralReward Reward { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ReferralEarning() { }

    public static ReferralEarning Create(Referral referral, Guid conversationId, ReferralReward reward, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        ReferralId = referral.Id,
        ReferrerUserId = referral.ReferrerUserId,
        ReferredUserId = referral.ReferredUserId,
        ConversationId = conversationId,
        Reward = reward,
        Amount = amount,
        CreatedAt = DateTime.UtcNow,
    };
}

/// <summary>A movement in a user's wallet (e.g. a referral credit).</summary>
public class WalletTransaction
{
    public Guid Id { get; private set; }
    public Guid AuthUserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? ReferenceId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private WalletTransaction() { }

    public static WalletTransaction Create(Guid authUserId, decimal amount, string description, Guid? referenceId = null) => new()
    {
        Id = Guid.NewGuid(),
        AuthUserId = authUserId,
        Amount = amount,
        Description = description,
        ReferenceId = referenceId,
        CreatedAt = DateTime.UtcNow,
    };
}
