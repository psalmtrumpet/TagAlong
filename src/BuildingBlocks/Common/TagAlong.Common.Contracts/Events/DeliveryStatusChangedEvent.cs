namespace TagAlong.Common.Contracts.Events;

public record DeliveryStatusChangedEvent(
    Guid DeliveryId,
    Guid PackageRequestId,
    Guid SenderId,
    Guid TravelerId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt);
