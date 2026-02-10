using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Identity.API.DTOs;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;
using TagAlong.Common.Contracts.Events;

namespace TagAlong.Identity.API.Commands;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber) : ICommand<AuthResponse>;

public class RegisterCommandHandler : ICommandHandler<RegisterCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IJwtService _jwtService;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IJwtService jwtService,
        IEventBus eventBus,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _jwtService = jwtService;
        _eventBus = eventBus;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsAsync(request.Email, cancellationToken))
        {
            return Result.Failure<AuthResponse>(Error.Conflict("A user with this email already exists"));
        }

        var passwordHash = _passwordService.HashPassword(request.Password);

        var user = ApplicationUser.Create(
            request.Email,
            passwordHash,
            request.FirstName,
            request.LastName,
            request.PhoneNumber);

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "7"));

        user.UpdateRefreshToken(refreshToken, refreshTokenExpiry);
        user.UpdateLastLogin();

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new UserCreatedIntegrationEvent(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            DateTime.UtcNow), cancellationToken);

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

public record UserCreatedIntegrationEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateTime CreatedAt) : IntegrationEvent;
