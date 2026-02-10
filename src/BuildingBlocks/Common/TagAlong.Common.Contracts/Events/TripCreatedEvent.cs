namespace TagAlong.Common.Contracts.Events;

public record TripCreatedEvent(
    Guid TripId,
    Guid TravelerId,
    string Origin,
    string Destination,
    List<string> IntermediateStops,
    DateTime DepartureTime,
    DateTime? EstimatedArrivalTime,
    decimal AvailableCapacity,
    DateTime CreatedAt);
