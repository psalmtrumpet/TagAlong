namespace TagAlong.Common.Contracts.Events;

public record TripStatusChangedEvent(
    Guid TripId,
    Guid TravelerId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt);
