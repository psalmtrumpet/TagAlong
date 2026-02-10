namespace TagAlong.Common.Contracts.Events;

public record PackageRequestCreatedEvent(
    Guid PackageRequestId,
    Guid SenderId,
    string PickupLocation,
    string DeliveryLocation,
    string PackageDescription,
    decimal EstimatedWeight,
    decimal? OfferedPrice,
    DateTime CreatedAt);
