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

    public ResetPasswordCommandHandler(IUserRepository userRepository, IPasswordService passwordService)
    {
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
            return Result.Failure<bool>(Error.Validation("That code doesn't match. If you asked for more than one, use the code in the newest email."));

        var problem = TagAlong.Identity.API.Validators.PasswordRules.Problem(request.NewPassword);
        if (problem != null)
            return Result.Failure<bool>(Error.Validation(problem));

        var hash = _passwordService.HashPassword(request.NewPassword);
        user.UpdatePassword(hash);
        user.ClearPasswordResetOtp();
        user.RevokeRefreshToken();

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
