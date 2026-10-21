using TagAlong.EventBus;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.IntegrationEvents;

/// <summary>Sent by Identity when someone adds or changes their phone number.</summary>
public record UserPhoneChangedIntegrationEvent(Guid UserId, string PhoneNumber) : IntegrationEvent;

/// <summary>Keeps the profile's phone number in step with the sign-in account (the admin portal reads it here).</summary>
public class UserPhoneChangedIntegrationEventHandler : IIntegrationEventHandler<UserPhoneChangedIntegrationEvent>
{
    private readonly IUserProfileRepository _profiles;
    private readonly ILogger<UserPhoneChangedIntegrationEventHandler> _logger;

    public UserPhoneChangedIntegrationEventHandler(IUserProfileRepository profiles, ILogger<UserPhoneChangedIntegrationEventHandler> logger)
    {
        _profiles = profiles;
        _logger = logger;
    }

    public async Task HandleAsync(UserPhoneChangedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(@event.PhoneNumber)) return;
        var profile = await _profiles.GetByAuthUserIdAsync(@event.UserId, cancellationToken);
        if (profile == null || profile.PhoneNumber == @event.PhoneNumber) return;

        profile.UpdatePhoneNumber(@event.PhoneNumber.Trim());
        _profiles.Update(profile);
        await _profiles.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Synced phone number for user {UserId}", @event.UserId);
    }
}
