using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Identity.API.DTOs;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

/// <summary>
/// The 6-digit code that proves a new account's email address (form sign-ups;
/// Google has already checked the address). A new code replaces the old one.
/// </summary>
public static class EmailCode
{
    public const int ValidMinutes = 15;

    /// <summary>At most one code a minute per account (sign-up again, or "Send a new code").</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// <summary>True (and starts the minute) when a code may go out now.</summary>
    public static bool MaySend(IMemoryCache cache, Guid userId)
    {
        var key = $"email-code-sent:{userId}";
        if (cache.TryGetValue(key, out _)) return false;
        cache.Set(key, true, Cooldown);
        return true;
    }

    /// <summary>A new code by email (the previous one stops working). Sent in the background.</summary>
    public static async Task SendAsync(ApplicationUser user, IVerificationCodeRepository codes, IEmailService email,
        CancellationToken ct)
    {
        await codes.InvalidatePreviousCodesAsync(user.Id, VerificationType.Email, ct);
        var code = VerificationCode.Create(user.Id, VerificationType.Email, ValidMinutes);
        await codes.AddAsync(code, ct);
        await codes.SaveChangesAsync(ct);

        _ = email.SendAsync(user.Email, user.FirstName, $"{code.Code} is your TagAlong code", Build(user.FirstName, code.Code))
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Console.Error.WriteLine($"[email code] {t.Exception?.InnerException?.Message}");
            }, TaskScheduler.Default);
    }

    private static string Build(string firstName, string code)
    {
        const string t = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'>
<style>
body{margin:0;padding:0;background:#F5F3EE;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
.wrap{max-width:480px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}
.header{background:#fff;padding:32px 24px 8px;text-align:center}
.header img{width:200px;height:auto}
.body{padding:32px}
.otp{font-size:40px;font-weight:700;color:#000;letter-spacing:8px;text-align:center;
     background:#FDF5E4;border:2px dashed #F1B01C;border-radius:8px;padding:16px;margin:24px 0}
.note{font-size:13px;color:#888;text-align:center}
.footer{background:#f9f9f9;padding:16px;text-align:center;font-size:12px;color:#aaa}
</style>
</head>
<body>
<div class='wrap'>
  <div class='header'>
    <img src='https://tagalong.delivery/assets/images/tagalong-wordmark-email.png' alt='TagAlong' width='200'>
  </div>
  <div class='body'>
    <p>Hi __FNAME__,</p>
    <p>Welcome to TagAlong! Enter this code in the app to confirm your email address:</p>
    <div class='otp'>__CODE__</div>
    <p class='note'>The code expires in __MINUTES__ minutes. If you didn't create a TagAlong account, you can ignore this email.</p>
  </div>
  <div class='footer'>&copy; __YEAR__ TagAlong. All rights reserved.</div>
</div>
</body></html>";
        return t
            .Replace("__FNAME__", System.Net.WebUtility.HtmlEncode(firstName?.Trim() ?? string.Empty))
            .Replace("__CODE__", code)
            .Replace("__MINUTES__", ValidMinutes.ToString())
            .Replace("__YEAR__", DateTime.UtcNow.Year.ToString());
    }
}

/// <summary>Send the signed-in user a new email code (at most once a minute).</summary>
public record SendEmailCodeCommand : ICommand<bool>;

public class SendEmailCodeCommandHandler : ICommandHandler<SendEmailCodeCommand, bool>
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IEmailService _email;
    private readonly IHttpContextAccessor _http;
    private readonly IMemoryCache _cache;

    public SendEmailCodeCommandHandler(IUserRepository users, IVerificationCodeRepository codes, IEmailService email,
        IHttpContextAccessor http, IMemoryCache cache)
    {
        _users = users;
        _codes = codes;
        _email = email;
        _http = http;
        _cache = cache;
    }

    public async Task<Result<bool>> Handle(SendEmailCodeCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return Result.Failure<bool>(Error.Unauthorized("Not signed in"));
        var user = await _users.GetByIdAsync(userId, ct);
        if (user == null) return Result.Failure<bool>(Error.NotFound("Account not found"));
        if (user.IsEmailVerified) return Result.Success(true);

        if (!EmailCode.MaySend(_cache, userId))
            return Result.Failure<bool>(Error.Validation("We just sent a code. Wait a minute before asking for another."));

        await EmailCode.SendAsync(user, _codes, _email, ct);
        return Result.Success(true);
    }
}

/// <summary>Check the email code; on success the account is verified and gets fresh tokens.</summary>
public record VerifyEmailCommand(string Code) : ICommand<AuthResponse>;

public class VerifyEmailCommandHandler : ICommandHandler<VerifyEmailCommand, AuthResponse>
{
    /// <summary>Wrong codes allowed before the code stops working and a new one is needed.</summary>
    private const int MaxTries = 5;

    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IJwtService _jwt;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor _http;
    private readonly IMemoryCache _cache;

    public VerifyEmailCommandHandler(IUserRepository users, IVerificationCodeRepository codes, IJwtService jwt,
        IEmailService email, IConfiguration config, IHttpContextAccessor http, IMemoryCache cache)
    {
        _users = users;
        _codes = codes;
        _jwt = jwt;
        _email = email;
        _config = config;
        _http = http;
        _cache = cache;
    }

    public async Task<Result<AuthResponse>> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return Result.Failure<AuthResponse>(Error.Unauthorized("Not signed in"));
        var user = await _users.GetByIdAsync(userId, ct);
        if (user == null) return Result.Failure<AuthResponse>(Error.NotFound("Account not found"));

        if (!user.IsEmailVerified)
        {
            var entered = new string((request.Code ?? string.Empty).Where(char.IsDigit).ToArray());
            var tries = $"email-code-tries:{userId}";
            var code = entered.Length == 6
                ? await _codes.GetValidCodeAsync(userId, entered, VerificationType.Email, ct)
                : null;
            if (code == null)
            {
                var n = _cache.GetOrCreate(tries, e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(EmailCode.ValidMinutes); return 0; }) + 1;
                _cache.Set(tries, n, TimeSpan.FromMinutes(EmailCode.ValidMinutes));
                if (n >= MaxTries)
                {
                    // Too many guesses: this code is finished
                    await _codes.InvalidatePreviousCodesAsync(userId, VerificationType.Email, ct);
                    await _codes.SaveChangesAsync(ct);
                    _cache.Remove(tries);
                    return Result.Failure<AuthResponse>(Error.Validation("Too many wrong codes. Tap \"Send a new code\"."));
                }
                return Result.Failure<AuthResponse>(Error.Validation("That code isn't right, or it has expired. Check the email and try again."));
            }

            _cache.Remove(tries);
            code.MarkAsUsed();
            await _codes.SaveChangesAsync(ct);
            user.VerifyEmail();
            WelcomeEmail.Send(_email, user.Email, user.FirstName);
        }

        // New tokens: they carry email_verified = true
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = _jwt.GenerateRefreshToken();
        user.UpdateRefreshToken(refreshToken,
            DateTime.UtcNow.AddDays(int.Parse(_config["JwtSettings:RefreshTokenExpirationInDays"] ?? "7")));
        _users.Update(user);
        await _users.SaveChangesAsync(ct);

        return Result.Success(new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(int.Parse(_config["JwtSettings:ExpirationInMinutes"] ?? "60")),
            user.PhoneNumber,
            user.Gender,
            user.IsEmailVerified));
    }
}
