using MediatR;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

public record JoinWaitlistCommand(string Name, string Email, string Phone) : IRequest<JoinWaitlistResult>;
public record JoinWaitlistResult(bool AlreadyOnList);

public class JoinWaitlistCommandHandler : IRequestHandler<JoinWaitlistCommand, JoinWaitlistResult>
{
    private readonly IWaitlistRepository _repo;
    private readonly IEmailService _email;

    public JoinWaitlistCommandHandler(IWaitlistRepository repo, IEmailService email)
    {
        _repo = repo;
        _email = email;
    }

    public async Task<JoinWaitlistResult> Handle(JoinWaitlistCommand request, CancellationToken ct)
    {
        var already = await _repo.ExistsAsync(request.Email, ct);
        if (!already)
        {
            var entry = WaitlistEntry.Create(request.Name, request.Email, request.Phone);
            await _repo.AddAsync(entry, ct);
            await _repo.SaveChangesAsync(ct);

            _ = _email.SendAsync(
                request.Email.Trim().ToLowerInvariant(),
                request.Name.Trim(),
                "You're on the TagAlong waitlist!",
                BuildAckEmail(request.Name.Trim()))
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Console.Error.WriteLine($"[waitlist email] {t.Exception?.InnerException?.Message}");
                }, TaskScheduler.Default);
        }

        return new JoinWaitlistResult(already);
    }

    private static string BuildAckEmail(string fullName)
    {
        var firstName = System.Net.WebUtility.HtmlEncode(
            fullName.Contains(' ') ? fullName.Split(' ')[0] : fullName);
        var year = DateTime.UtcNow.Year.ToString();

        const string t = @"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>You're on the list!</title>
<style>
body{margin:0;padding:0;background:#f5f5f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
.wrap{max-width:560px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08)}
.hero{background:#FF6B00;padding:36px 40px;text-align:center}
.hero img{height:36px;margin:0 auto 16px;display:block}
.hero h1{margin:0;font-size:22px;font-weight:700;color:#fff;letter-spacing:-.3px}
.hero p{margin:8px 0 0;font-size:14px;color:rgba(255,255,255,.85)}
.body{padding:36px 40px}
.body p{margin:0 0 16px;font-size:15px;line-height:1.6;color:#333}
.badge{display:inline-block;background:#fff3e8;border:1px solid #ffd9bb;color:#c04f00;font-size:13px;font-weight:600;padding:8px 18px;border-radius:20px;margin:8px 0 20px}
.footer{padding:20px 40px;background:#fafafa;border-top:1px solid #eee;text-align:center;font-size:12px;color:#999}
.footer a{color:#FF6B00;text-decoration:none}
@media(max-width:600px){.hero,.body,.footer{padding:28px 24px}}
</style></head>
<body>
<div class=""wrap"">
<div class=""hero"">
<img src=""https://tagalong.delivery/assets/images/TAGALONG-LOGO-N.png"" alt=""TagAlong"" height=""36"">
<h1>You're on the list, __FNAME__! 🎉</h1>
<p>TagAlong is launching soon — we'll let you know first.</p>
</div>
<div class=""body"">
<p>Hi __FNAME__,</p>
<p>Thanks for joining the TagAlong waitlist! You're among the first to hear about our launch.</p>
<div style=""text-align:center""><span class=""badge"">&#10003; Waitlist confirmed</span></div>
<p>TagAlong connects travellers and senders — share rides intercity, send packages affordably, or earn on your next trip. When we launch in your city, you'll be the first to know.</p>
<p>Stay tuned — we're moving fast.</p>
<p style=""margin-bottom:0"">The TagAlong Team</p>
</div>
<div class=""footer"">
You received this because you signed up at <a href=""https://tagalong.delivery"">tagalong.delivery</a>.<br>
&copy; __YEAR__ TagAlong. All rights reserved.
</div>
</div>
</body></html>";

        return t.Replace("__FNAME__", firstName).Replace("__YEAR__", year);
    }
}
