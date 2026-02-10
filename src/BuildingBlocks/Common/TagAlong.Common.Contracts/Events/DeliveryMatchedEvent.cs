namespace TagAlong.Common.Contracts.Events;

public record DeliveryMatchedEvent(
    Guid DeliveryId,
    Guid PackageRequestId,
    Guid TripId,
    Guid SenderId,
    Guid TravelerId,
    decimal AgreedPrice,
    DateTime MatchedAt);
