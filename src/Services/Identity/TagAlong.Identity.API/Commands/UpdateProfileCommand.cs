using System.Security.Claims;
using TagAlong.Common.CQRS;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Identity.API.DTOs;
using TagAlong.Identity.Domain.Repositories;
using TagAlong.Identity.Infrastructure.Services;

namespace TagAlong.Identity.API.Commands;

public record UpdateProfileCommand(string FirstName, string LastName, string? PhoneNumber = null, string? Gender = null) : ICommand<AuthResponse>;

public class UpdateProfileCommandHandler : ICommandHandler<UpdateProfileCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEventBus _eventBus;

    public UpdateProfileCommandHandler(
        IUserRepository userRepository,
        IJwtService jwtService,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        IEventBus eventBus)
    {
        _eventBus = eventBus;
        _userRepository = userRepository;
        _jwtService = jwtService;
        _configuration = configuration;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<AuthResponse>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Result.Failure<AuthResponse>(Error.Unauthorized("Not authenticated"));

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            return Result.Failure<AuthResponse>(Error.NotFound("User not found"));

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return Result.Failure<AuthResponse>(Error.Validation("First and last name are required"));

        user.UpdateProfile(request.FirstName.Trim(), request.LastName.Trim());

        var phone = request.PhoneNumber?.Trim();
        var phoneChanged = !string.IsNullOrEmpty(phone) && phone != user.PhoneNumber;
        if (phoneChanged)
            user.UpdatePhoneNumber(phone!);

        var gender = request.Gender?.Trim();
        if (!string.IsNullOrEmpty(gender) && gender != user.Gender)
            user.UpdateGender(gender);

        _userRepository.Update(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        // The user profile keeps its own copy (the admin portal reads it there)
        if (phoneChanged)
            await _eventBus.PublishAsync(new UserPhoneChangedIntegrationEvent(user.Id, phone!), cancellationToken);

        var accessToken = _jwtService.GenerateAccessToken(user);
        return Result.Success(new AuthResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            accessToken,
            user.RefreshToken ?? string.Empty,
            DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:ExpirationInMinutes"] ?? "60")),
            user.PhoneNumber,
            user.Gender,
            user.IsEmailVerified));
    }
}
