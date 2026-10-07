using System.Net;
using Microsoft.EntityFrameworkCore;
using TagAlong.EventBus;
using TagAlong.User.Infrastructure.Persistence;

namespace TagAlong.User.API.Services;

/// <summary>Picked up by the Notification service, which sends the push notification.</summary>
public record DriverProfileReviewedIntegrationEvent(
    Guid UserId,
    string Status,
    string? Reason) : IntegrationEvent;

/// <summary>
/// Tells a driver the outcome of their licence and vehicle review: an email with
/// the details and a push notification pointing them to it.
/// </summary>
public class DriverReviewNotifier
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IEventBus _eventBus;
    private readonly ILogger<DriverReviewNotifier> _logger;

    public DriverReviewNotifier(IServiceScopeFactory scopes, IEventBus eventBus, ILogger<DriverReviewNotifier> logger)
    {
        _scopes = scopes;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <param name="problems">What to fix, addressed to the driver (rejections only).</param>
    public async Task NotifyAsync(Guid authUserId, bool approved, IReadOnlyList<string> problems, CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.AuthUserId == authUserId, ct);
            if (profile != null && !string.IsNullOrWhiteSpace(profile.Email))
            {
                var name = string.IsNullOrWhiteSpace(profile.FirstName) ? "there" : profile.FirstName;
                if (approved)
                    await email.SendAsync(profile.Email, $"{profile.FirstName} {profile.LastName}".Trim(),
                        "You're approved to drive on TagAlong", ApprovedEmail(name), ct);
                else
                    await email.SendAsync(profile.Email, $"{profile.FirstName} {profile.LastName}".Trim(),
                        "Your TagAlong driver details were not approved", RejectedEmail(name, problems), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't email driver review result to {UserId}", authUserId);
        }

        try
        {
            await _eventBus.PublishAsync(new DriverProfileReviewedIntegrationEvent(
                authUserId, approved ? "Approved" : "Rejected", approved ? null : string.Join(" ", problems)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't publish driver review result for {UserId}", authUserId);
        }
    }

    private static string ApprovedEmail(string name) => Layout($"""
        <p>Hi {Enc(name)},</p>
        <p>Good news — we've checked your driver's licence and vehicle, and you're approved.
        You can now offer rides and deliveries on TagAlong.</p>
        <p>Drive safely!</p>
        """);

    private static string RejectedEmail(string name, IReadOnlyList<string> problems)
    {
        var items = problems.Count == 0
            ? "<li>The photos or details you sent could not be verified.</li>"
            : string.Concat(problems.Select(p => $"<li>{Enc(p)}</li>"));
        return Layout($"""
            <p>Hi {Enc(name)},</p>
            <p>We couldn't approve the driver's licence and vehicle details you sent, so you can't
            offer rides or deliveries yet.</p>
            <p><strong>What we found:</strong></p>
            <ul>{items}</ul>
            <p><strong>Please upload the correct documents again:</strong></p>
            <ul>
              <li>A photo of <strong>your own driver's licence</strong> (front), with the number, name and expiry date readable.</li>
              <li>A photo of the <strong>front</strong> and the <strong>back</strong> of your vehicle, with the plate number clearly visible in both.</li>
              <li>Photos must be <strong>sharp, not blurry</strong>, taken in good light with no glare.
              Take them directly with your camera — no screenshots or photos of a screen.</li>
            </ul>
            <p>To resubmit, open the TagAlong app and go to <strong>Profile → Driver details</strong>.</p>
            """);
    }

    private static string Layout(string body) => $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#111;line-height:1.5">
          <div style="border-top:4px solid #F1B01C;padding:24px 4px">
            <div style="font-size:22px;font-weight:700;margin-bottom:16px"><span style="color:#228B22">Tag</span><span style="color:#F1B01C">Along</span></div>
            {body}
            <p style="color:#737373;font-size:13px;margin-top:24px">— The TagAlong team</p>
          </div>
        </div>
        """;

    private static string Enc(string s) => WebUtility.HtmlEncode(s);
}
