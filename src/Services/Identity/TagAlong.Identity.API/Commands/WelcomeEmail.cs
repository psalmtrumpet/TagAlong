using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

/// <summary>The email a new user gets right after creating their account.</summary>
public static class WelcomeEmail
{
    /// <summary>Sends in the background; a mail failure never fails sign-up.</summary>
    public static void Send(IEmailService email, string toEmail, string firstName)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;
        _ = email.SendAsync(toEmail.Trim().ToLowerInvariant(), firstName, "Welcome to TagAlong", Build(firstName))
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Console.Error.WriteLine($"[welcome email] {t.Exception?.InnerException?.Message}");
            }, TaskScheduler.Default);
    }

    private static string Build(string firstName)
    {
        var name = System.Net.WebUtility.HtmlEncode(firstName?.Trim() ?? string.Empty);
        var year = DateTime.UtcNow.Year.ToString();

        var t = @"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Welcome to TagAlong</title>
<style>
body{margin:0;padding:0;background:#f5f5f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
.wrap{max-width:560px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08)}
.hero{background:#FF6B00;padding:36px 40px;text-align:center}
.hero img{height:36px;margin:0 auto 16px;display:block}
.hero h1{margin:0;font-size:22px;font-weight:700;color:#fff;letter-spacing:-.3px}
.hero p{margin:8px 0 0;font-size:14px;color:rgba(255,255,255,.85)}
.body{padding:36px 40px}
.body p{margin:0 0 16px;font-size:15px;line-height:1.6;color:#333}
.step{margin:0 0 14px;padding:14px 16px;background:#fff7f0;border-radius:10px;font-size:14px;line-height:1.5;color:#333}
.step b{color:#c04f00}
.footer{padding:20px 40px;background:#fafafa;border-top:1px solid #eee;text-align:center;font-size:12px;color:#999}
.footer a{color:#FF6B00;text-decoration:none}
@media(max-width:600px){.hero,.body,.footer{padding:28px 24px}}
</style></head>
<body>
<div class=""wrap"">
<div class=""hero"">
<img src=""https://tagalong.delivery/assets/images/TAGALONG-LOGO-N.png"" alt=""TagAlong"" height=""36"">
<h1>Welcome to TagAlong{{COMMA_NAME}}!</h1>
<p>Your account is ready.</p>
</div>
<div class=""body"">
<p>Thanks for joining. TagAlong connects you with people already going your way, so you can share the ride and the cost, or book a private ride when you need one.</p>
<div class=""step""><b>1. Verify your identity.</b> It takes a minute with your NIN and a selfie, and it keeps everyone on TagAlong safe.</div>
<div class=""step""><b>2. Find a ride.</b> Tell us where you're going and pick a shared ride along your route or a private ride to your door.</div>
<div class=""step""><b>3. Driving anyway?</b> Offer a ride, add your driver details, and earn from the empty seats on trips you already make.</div>
<p>Need help or want to report something? Just reply to this email.</p>
<p>See you on the road,<br>The TagAlong team</p>
</div>
<div class=""footer"">&copy; {{YEAR}} TagAlong &middot; <a href=""https://tagalong.delivery"">tagalong.delivery</a></div>
</div>
</body>
</html>";
        return t.Replace("{{COMMA_NAME}}", name.Length > 0 ? ", " + name : string.Empty)
                .Replace("{{YEAR}}", year);
    }
}
