using TagAlong.EventBus;
using TagAlong.User.Domain.Entities;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.IntegrationEvents;

public record UserCreatedIntegrationEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateTime CreatedAt) : IntegrationEvent;

public class UserCreatedIntegrationEventHandler : IIntegrationEventHandler<UserCreatedIntegrationEvent>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<UserCreatedIntegrationEventHandler> _logger;

    public UserCreatedIntegrationEventHandler(
        IUserProfileRepository userProfileRepository,
        ILogger<UserCreatedIntegrationEventHandler> logger)
    {
        _userProfileRepository = userProfileRepository;
        _logger = logger;
    }

    public async Task HandleAsync(UserCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling UserCreatedIntegrationEvent for user {UserId}", @event.UserId);

        var existingProfile = await _userProfileRepository.GetByAuthUserIdAsync(@event.UserId, cancellationToken);

        if (existingProfile != null)
        {
            _logger.LogWarning("User profile already exists for user {UserId}", @event.UserId);
            return;
        }

        var profile = UserProfile.Create(
            @event.UserId,
            @event.Email,
            @event.FirstName,
            @event.LastName,
            @event.PhoneNumber);

        await _userProfileRepository.AddAsync(profile, cancellationToken);
        await _userProfileRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created user profile for user {UserId}", @event.UserId);
    }
}
