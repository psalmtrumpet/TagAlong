namespace TagAlong.Common.Contracts.Events;

public record UserCreatedEvent(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    DateTime CreatedAt);
