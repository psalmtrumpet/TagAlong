using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

public record ForgotPasswordCommand(string Email) : ICommand<bool>;

public class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(IUserRepository userRepository, IEmailService emailService)
    {
        _userRepository = userRepository;
        _emailService = emailService;
    }

    public async Task<Result<bool>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken);

        // Always return success to prevent user enumeration
        if (user == null)
            return Result.Success(true);

        var otp = GenerateOtp();
        user.SetPasswordResetOtp(otp);
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        var html = BuildResetEmail(user.FirstName, otp);
        _ = _emailService.SendAsync(user.Email, user.FirstName, "Reset your TagAlong password", html)
            .ContinueWith(t => { /* fire-and-forget */ }, TaskContinuationOptions.OnlyOnFaulted);

        return Result.Success(true);
    }

    private static string GenerateOtp()
    {
        var rng = new Random();
        return rng.Next(100000, 999999).ToString();
    }

    private static string BuildResetEmail(string firstName, string otp)
    {
        const string t = @"
<!DOCTYPE html>
<html><head><meta charset='utf-8'>
<style>
body{margin:0;padding:0;background:#f5f5f5;font-family:sans-serif}
.wrap{max-width:480px;margin:40px auto;background:#fff;border-radius:12px;overflow:hidden}
.header{background:#FF6B00;padding:24px;text-align:center}
.header img{height:40px}
.body{padding:32px}
.otp{font-size:40px;font-weight:700;color:#FF6B00;letter-spacing:8px;text-align:center;
     background:#fff5f0;border:2px dashed #FF6B00;border-radius:8px;padding:16px;margin:24px 0}
.note{font-size:13px;color:#888;text-align:center}
.footer{background:#f9f9f9;padding:16px;text-align:center;font-size:12px;color:#aaa}
</style>
</head>
<body>
<div class='wrap'>
  <div class='header'>
    <img src='https://tagalong.delivery/assets/images/TAGALONG-LOGO-N.png' alt='TagAlong'>
  </div>
  <div class='body'>
    <p>Hi __FNAME__,</p>
    <p>We received a request to reset your TagAlong password. Enter the code below in the app:</p>
    <div class='otp'>__OTP__</div>
    <p class='note'>This code expires in 15 minutes. If you did not request a reset, ignore this email.</p>
  </div>
  <div class='footer'>&copy; __YEAR__ TagAlong. All rights reserved.</div>
</div>
</body></html>";
        return t
            .Replace("__FNAME__", firstName)
            .Replace("__OTP__", otp)
            .Replace("__YEAR__", DateTime.UtcNow.Year.ToString());
    }
}
