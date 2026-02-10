using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Identity.API.DTOs;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

public record GoogleLoginCommand(string IdToken) : ICommand<AuthResponse>;

public class GoogleLoginCommandHandler : ICommandHandler<GoogleLoginCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IJwtService _jwtService;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;

    public GoogleLoginCommandHandler(
        IUserRepository userRepository,
        IGoogleAuthService googleAuthService,
        IJwtService jwtService,
        IEventBus eventBus,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _googleAuthService = googleAuthService;
        _jwtService = jwtService;
        _eventBus = eventBus;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        var googleUser = await _googleAuthService.ValidateGoogleTokenAsync(request.IdToken);

        if (googleUser == null)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("Invalid Google token"));
        }

        var user = await _userRepository.GetByGoogleIdAsync(googleUser.GoogleId, cancellationToken)
                   ?? await _userRepository.GetByEmailAsync(googleUser.Email, cancellationToken);

        bool isNewUser = false;

        if (user == null)
        {
            user = ApplicationUser.CreateFromGoogle(
                googleUser.Email,
                googleUser.GoogleId,
                googleUser.FirstName,
                googleUser.LastName,
                googleUser.ProfileImageUrl);

            await _userRepository.AddAsync(user, cancellationToken);
            isNewUser = true;
        }
        else if (string.IsNullOrEmpty(user.GoogleId))
        {
            user.LinkGoogleAccount(googleUser.GoogleId);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("Account is deactivated"));
        }

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "7"));

        user.UpdateRefreshToken(refreshToken, refreshTokenExpiry);
        user.UpdateLastLogin();

        if (!isNewUser)
        {
            _userRepository.Update(user);
        }

        await _userRepository.SaveChangesAsync(cancellationToken);

        if (isNewUser)
        {
            await _eventBus.PublishAsync(new UserCreatedIntegrationEvent(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                DateTime.UtcNow), cancellationToken);
        }

        return Result.Success(new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpirationInMinutes"] ?? "60"))));
    }
}
