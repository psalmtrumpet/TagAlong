using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Identity.API.DTOs;
using TagAlong.Identity.Domain.Entities;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;
using TagAlong.Common.Contracts.Events;
using Microsoft.Extensions.Caching.Memory;

namespace TagAlong.Identity.API.Commands;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber) : ICommand<AuthResponse>;

public partial class RegisterCommandHandler : ICommandHandler<RegisterCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IJwtService _jwtService;
    private readonly IEventBus _eventBus;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _email;
    private readonly IVerificationCodeRepository _codes;
    private readonly IMemoryCache _cache;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IJwtService jwtService,
        IEventBus eventBus,
        IConfiguration configuration,
        IEmailService email,
        IVerificationCodeRepository codes,
        IMemoryCache cache)
    {
        _email = email;
        _codes = codes;
        _cache = cache;
        _userRepository = userRepository;
        _passwordService = passwordService;
        _jwtService = jwtService;
        _eventBus = eventBus;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // The request validators don't run for commands, so check the basics here
        if (string.IsNullOrWhiteSpace(request.Email) ||
            !System.Text.RegularExpressions.Regex.IsMatch(request.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$"))
            return Result.Failure<AuthResponse>(Error.Validation("Enter a valid email address"));
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return Result.Failure<AuthResponse>(Error.Validation("First and last name are required"));
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.PhoneNumber.Count(char.IsDigit) < 10)
            return Result.Failure<AuthResponse>(Error.Validation("Enter a valid phone number"));

        var passwordProblem = TagAlong.Identity.API.Validators.PasswordRules.Problem(request.Password);
        if (passwordProblem != null)
            return Result.Failure<AuthResponse>(Error.Validation(passwordProblem));

        var passwordHash = _passwordService.HashPassword(request.Password);

        var existing = await _userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (existing != null)
        {
            // A confirmed account (or a Google one) is someone's: generic message —
            // do not confirm whether the email is registered
            if (existing.IsEmailVerified || !string.IsNullOrEmpty(existing.GoogleId))
                return Result.Failure<AuthResponse>(Error.Validation("Unable to complete registration with the provided details"));
            // Never confirmed: start again with these details and a new code
            return await RestartAsync(existing, request, passwordHash, cancellationToken);
        }

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

        // Confirm the address first; the welcome email follows once they do
        EmailCode.MaySend(_cache, user.Id);
        await EmailCode.SendAsync(user, _codes, _email, cancellationToken);

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

public partial class RegisterCommandHandler
{
    /// <summary>The same email signed up again before confirming: new details, new code.</summary>
    private async Task<Result<AuthResponse>> RestartAsync(ApplicationUser user, RegisterCommand request, string passwordHash,
        CancellationToken ct)
    {
        var phone = request.PhoneNumber.Trim();
        var phoneChanged = phone != user.PhoneNumber;
        user.RestartSignUp(passwordHash, request.FirstName.Trim(), request.LastName.Trim(), phone);

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        user.UpdateRefreshToken(refreshToken,
            DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "7")));
        user.UpdateLastLogin();
        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(ct);

        // The user profile keeps its own copy of the phone number
        if (phoneChanged)
            await _eventBus.PublishAsync(new UserPhoneChangedIntegrationEvent(user.Id, phone), ct);

        // Tapped Register again within a minute: the code just sent still works
        if (EmailCode.MaySend(_cache, user.Id))
            await EmailCode.SendAsync(user, _codes, _email, ct);

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

public record UserCreatedIntegrationEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateTime CreatedAt) : IntegrationEvent;

/// <summary>Someone added or changed their phone number.</summary>
public record UserPhoneChangedIntegrationEvent(Guid UserId, string PhoneNumber) : IntegrationEvent;
