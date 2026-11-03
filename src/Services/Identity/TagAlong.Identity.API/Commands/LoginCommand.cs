using Microsoft.Extensions.Caching.Memory;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.Identity.API.DTOs;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

public record LoginCommand(string Email, string Password) : ICommand<AuthResponse>;

public class LoginCommandHandler : ICommandHandler<LoginCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    /// <summary>Wrong passwords allowed per account in <see cref="LockFor"/> before sign-in pauses.</summary>
    private const int MaxWrong = 8;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(15);

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IJwtService jwtService,
        IConfiguration configuration,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _cache = cache;
        _userRepository = userRepository;
        _passwordService = passwordService;
        _jwtService = jwtService;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user == null)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("Invalid email or password"));
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(Error.Unauthorized("Account is deactivated"));
        }

        var wrongKey = $"login-wrong:{user.Id}";
        if (_cache.TryGetValue(wrongKey, out int wrong) && wrong >= MaxWrong)
            return Result.Failure<AuthResponse>(Error.Unauthorized(
                "Too many wrong passwords. Wait 15 minutes, or reset your password."));

        if (!_passwordService.VerifyPassword(request.Password, user.PasswordHash))
        {
            _cache.Set(wrongKey, wrong + 1, LockFor);
            return Result.Failure<AuthResponse>(Error.Unauthorized("Invalid email or password"));
        }
        _cache.Remove(wrongKey);

        var accessToken = _jwtService.GenerateAccessToken(user);
        // Reuse a still-valid refresh token: signing in on a second phone (or two
        // refreshes at once) must not sign the other session out
        var refreshToken = !string.IsNullOrEmpty(user.RefreshToken) && user.RefreshTokenExpiryTime > DateTime.UtcNow
            ? user.RefreshToken!
            : _jwtService.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "7"));

        user.UpdateRefreshToken(refreshToken, refreshTokenExpiry);
        user.UpdateLastLogin();

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpirationInMinutes"] ?? "60")),
            user.PhoneNumber,
            user.Gender,
            user.IsEmailVerified));
    }
}
