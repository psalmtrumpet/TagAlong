using System.Security.Claims;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Identity.API.DTOs;
using TagAlong.Identity.API.Services;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

public record RefreshTokenCommand(string AccessToken, string RefreshToken) : ICommand<AuthResponse>;

public class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly RefreshSessions _sessions;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IJwtService jwtService,
        IConfiguration configuration,
        RefreshSessions sessions)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _configuration = configuration;
        _sessions = sessions;
    }

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);

        if (principal == null)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("Invalid access token"));
        }

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("Invalid access token"));
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("User not found"));
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("Account is deactivated"));
        }

        // A new refresh token every time; this phone's session moves on to it
        var (outcome, response) = await _sessions.RotateAsync(userId, request.RefreshToken, user, refreshToken => new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            _jwtService.GenerateAccessToken(user),
            refreshToken,
            DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpirationInMinutes"] ?? "60")),
            user.PhoneNumber,
            user.Gender,
            user.IsEmailVerified), cancellationToken);

        return outcome switch
        {
            RefreshSessions.Outcome.Rotated or RefreshSessions.Outcome.Repeat => Result.Success(response!),
            RefreshSessions.Outcome.Reused => Result.Failure<AuthResponse>(Error.Unauthorized("This sign-in was ended for safety. Please sign in again.")),
            _ => Result.Failure<AuthResponse>(Error.Unauthorized("Invalid or expired refresh token")),
        };
    }
}
