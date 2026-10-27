using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TagAlong.EventBus;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Services;

/// <summary>Picked up by the Notification service to tell the referrer what they earned.</summary>
public record ReferralRewardedIntegrationEvent(
    Guid UserId,
    string Reward,
    decimal Amount,
    string? ReferredName) : IntegrationEvent;

/// <summary>
/// Referral codes and rewards. Every ride the referred person completes (as
/// passenger or driver) rewards the referrer: one TagAlong-fee-free ride if
/// they're an approved driver, otherwise wallet credit (Referral:RiderCredit,
/// default ₦100).
/// </summary>
public class ReferralService
{
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I

    private readonly UserDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ReferralService> _logger;

    public ReferralService(UserDbContext db, IConfiguration config, IEventBus eventBus, ILogger<ReferralService> logger)
    {
        _db = db;
        _config = config;
        _eventBus = eventBus;
        _logger = logger;
    }

    public decimal RiderCredit =>
        decimal.TryParse(_config["Referral:RiderCredit"], System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : 100m;

    /// <summary>The user's referral account, created with a fresh code on first use.</summary>
    public async Task<ReferralAccount> GetOrCreateAccountAsync(Guid userId, CancellationToken ct)
    {
        var account = await _db.ReferralAccounts.FirstOrDefaultAsync(a => a.AuthUserId == userId, ct);
        if (account != null) return account;

        var first = await _db.UserProfiles.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.AuthUserId == userId).Select(p => p.FirstName).FirstOrDefaultAsync(ct);
        var prefix = new string((first ?? "").ToUpperInvariant().Where(char.IsAsciiLetterUpper).Take(5).ToArray());
        if (prefix.Length < 2) prefix = "TAG";

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = prefix + RandomPart(4);
            if (await _db.ReferralAccounts.AnyAsync(a => a.Code == code, ct)) continue;
            account = ReferralAccount.Create(userId, code);
            _db.ReferralAccounts.Add(account);
            try
            {
                await _db.SaveChangesAsync(ct);
                return account;
            }
            catch (DbUpdateException)
            {
                // Lost a race (same code or a second request for this user) — retry/reload
                _db.Entry(account).State = EntityState.Detached;
                var existing = await _db.ReferralAccounts.FirstOrDefaultAsync(a => a.AuthUserId == userId, ct);
                if (existing != null) return existing;
            }
        }
        throw new InvalidOperationException("Couldn't create a referral code");
    }

    private static string RandomPart(int length) =>
        new(Enumerable.Range(0, length).Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]).ToArray());

    /// <summary>Whether the user can still enter someone's code (not referred yet, no rides done).</summary>
    public async Task<bool> CanApplyCodeAsync(Guid userId, CancellationToken ct)
    {
        if (await _db.Referrals.AnyAsync(r => r.ReferredUserId == userId, ct)) return false;
        var done = await _db.UserProfiles.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.AuthUserId == userId)
            .Select(p => p.CompletedTrips + p.CompletedDeliveries).FirstOrDefaultAsync(ct);
        return done == 0;
    }

    /// <summary>Returns null on success, or a message for the user.</summary>
    public async Task<string?> ApplyCodeAsync(Guid userId, string code, CancellationToken ct)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length < 4) return "Enter a valid referral code.";
        if (!await CanApplyCodeAsync(userId, ct))
            return "A referral code can only be added once, before your first ride.";

        var referrer = await _db.ReferralAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == code, ct);
        if (referrer == null) return "We couldn't find that referral code.";
        if (referrer.AuthUserId == userId) return "You can't use your own referral code.";

        _db.Referrals.Add(Referral.Create(referrer.AuthUserId, userId, code));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return "A referral code can only be added once, before your first ride.";
        }
        _logger.LogInformation("Referral: {UserId} signed up with {ReferrerId}'s code", userId, referrer.AuthUserId);
        return null;
    }

    /// <summary>
    /// Called when a ride completes: if this user was referred, their referrer
    /// is rewarded for this ride (once per ride).
    /// </summary>
    /// <summary>Points per referral: anyone can refer anyone.</summary>
    public const int PointsPerReferral = 2;

    /// <summary>
    /// The referred person completed a ride: on their first one, the person who
    /// referred them gets 2 points. Once per referral.
    /// </summary>
    public async Task RewardRideAsync(Guid userId, Guid conversationId, CancellationToken ct)
    {
        var referral = await _db.Referrals.FirstOrDefaultAsync(r => r.ReferredUserId == userId, ct);
        if (referral == null || referral.IsQualified) return;

        var account = await GetOrCreateAccountAsync(referral.ReferrerUserId, ct);
        var referredName = await _db.UserProfiles.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.AuthUserId == userId).Select(p => p.FirstName).FirstOrDefaultAsync(ct);

        account.AddPoints(PointsPerReferral);
        referral.Qualify(ReferralReward.Points, PointsPerReferral);
        _db.ReferralEarnings.Add(ReferralEarning.Create(referral, conversationId, ReferralReward.Points, PointsPerReferral));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return; // rewarded concurrently
        }
        _logger.LogInformation("Referral points: {ReferrerId} got {Points} for {UserId}'s first ride {ConversationId}",
            referral.ReferrerUserId, PointsPerReferral, userId, conversationId);

        try
        {
            await _eventBus.PublishAsync(new ReferralRewardedIntegrationEvent(
                referral.ReferrerUserId, ReferralReward.Points.ToString(), PointsPerReferral, referredName));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't publish referral reward for {ReferrerId}", referral.ReferrerUserId);
        }
    }

    /// <summary>Uses one fee-free ride if the driver has one. Atomic, so it can't be spent twice.</summary>
    public async Task<bool> UseFreeTripAsync(Guid userId, CancellationToken ct)
    {
        var updated = await _db.ReferralAccounts
            .Where(a => a.AuthUserId == userId && a.FreeTripCredits > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.FreeTripCredits, a => a.FreeTripCredits - 1), ct);
        return updated > 0;
    }
}
