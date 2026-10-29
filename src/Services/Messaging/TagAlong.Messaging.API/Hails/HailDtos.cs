namespace TagAlong.Messaging.API.Hails;

public record CreateHailRequest(
    double PickupLat, double PickupLng, string? PickupName,
    double DestLat, double DestLng, string? DestName,
    decimal OfferedPrice);

public record PriceRequest(decimal Price);

public record AcceptCounterRequest(Guid DriverId);

public record SkipDriversRequest(List<Guid> DriverIds);

public record OnlyWithinRequest(int Minutes);

/// <summary>A driver the request went to, as the passenger sees them (list and map).</summary>
public record HailDriverDto(
    Guid DriverId,
    string Name,
    decimal Rating,
    int TotalRatings,
    string Vehicle,
    string VehiclePlate,
    int EtaSeconds,
    int DistanceMeters,
    double Lat,
    double Lng,
    bool FinishingCurrentRide,
    string Status,
    decimal? CounterPrice,
    // What the driver's car offers (ride feature ids; the app has the names)
    List<int>? RideFeatureIds = null);

/// <summary>Suggested fare and how many drivers are around, before the passenger asks.</summary>
public record HailQuoteDto(int DistanceMeters, int? DurationSeconds, decimal SuggestedPrice, int DriversNearby);

/// <summary>A driver's counter-offer, as the passenger sees it.</summary>
public record HailCounterDto(
    Guid HailId,
    Guid DriverId,
    string DriverName,
    decimal DriverRating,
    int DriverTotalRatings,
    string Vehicle,
    string VehiclePlate,
    int EtaSeconds,
    bool FinishingCurrentRide,
    decimal Price,
    List<int>? RideFeatureIds = null);

/// <summary>The passenger's request and its progress.</summary>
public record HailDto(
    Guid Id,
    string Status,
    double PickupLat, double PickupLng, string PickupName,
    double DestLat, double DestLng, string DestName,
    decimal OfferedPrice,
    DateTime ExpiresAt,
    int DriversNotified,
    Guid? ConversationId,
    List<HailCounterDto> Counters,
    List<HailDriverDto> Drivers);

/// <summary>An incoming request, as a driver sees it.</summary>
public record HailOfferDto(
    Guid HailId,
    Guid PassengerId,
    string? PassengerName,
    double PickupLat, double PickupLng, string PickupName,
    double DestLat, double DestLng, string DestName,
    int TripDistanceMeters,
    decimal OfferedPrice,
    int EtaSeconds,
    bool FinishingCurrentRide,
    DateTime ExpiresAt,
    decimal? MyCounterPrice);
