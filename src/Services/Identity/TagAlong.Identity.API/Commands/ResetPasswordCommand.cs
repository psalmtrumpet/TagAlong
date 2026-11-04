using Microsoft.Extensions.Caching.Memory;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

public record ResetPasswordCommand(string Email, string Code, string NewPassword) : ICommand<bool>;

public class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;

    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly TagAlong.Identity.API.Services.RefreshSessions _sessions;

    /// <summary>Wrong codes allowed before the reset code stops working.</summary>
    private const int MaxTries = 5;

    public ResetPasswordCommandHandler(IUserRepository userRepository, IPasswordService passwordService,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
        TagAlong.Identity.API.Services.RefreshSessions sessions)
    {
        _sessions = sessions;
        _cache = cache;
        _userRepository = userRepository;
        _passwordService = passwordService;
    }

    public async Task<Result<bool>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken);
        if (user == null)
            return Result.Failure<bool>(Error.Validation("Invalid code or email"));

        if (user.PasswordResetOtp == null || user.PasswordResetOtpExpiry == null)
            return Result.Failure<bool>(Error.Validation("No reset was requested for this account"));

        if (DateTime.UtcNow > user.PasswordResetOtpExpiry)
            return Result.Failure<bool>(Error.Validation("Reset code has expired"));

        if (user.PasswordResetOtp != request.Code.Trim())
        {
            var tries = $"reset-tries:{user.Id}";
            var n = (_cache.TryGetValue(tries, out int t) ? t : 0) + 1;
            if (n >= MaxTries)
            {
                // Too many guesses: this code is finished
                _cache.Remove(tries);
                user.ClearPasswordResetOtp();
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync(cancellationToken);
                return Result.Failure<bool>(Error.Validation("Too many wrong codes. Ask for a new reset code."));
            }
            _cache.Set(tries, n, TimeSpan.FromMinutes(15));
            return Result.Failure<bool>(Error.Validation("That code doesn't match. If you asked for more than one, use the code in the newest email."));
        }
        _cache.Remove($"reset-tries:{user.Id}");

        var problem = TagAlong.Identity.API.Validators.PasswordRules.Problem(request.NewPassword);
        if (problem != null)
            return Result.Failure<bool>(Error.Validation(problem));

        var hash = _passwordService.HashPassword(request.NewPassword);
        user.UpdatePassword(hash);
        user.ClearPasswordResetOtp();
        user.RevokeRefreshToken();
        await _sessions.RevokeAllAsync(user.Id, "password reset", cancellationToken);

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
