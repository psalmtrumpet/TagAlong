namespace TagAlong.User.API.DTOs;

// Requests
public record SetAvailabilityRequest(
    bool IsAvailable,
    double? Latitude,
    double? Longitude,
    string? LocationName,
    int? DurationMinutes);

public record UpdateLocationRequest(
    double Latitude,
    double Longitude,
    string? LocationName);

public record UpdateLocationPreferencesRequest(
    double MaxTravelRadiusKm,
    bool AllowLocationSharing);

public record SearchAvailableUsersRequest(
    double Latitude,
    double Longitude,
    double? RadiusKm,
    int? Page,
    int? PageSize);

// Responses
public record AvailabilityResponse(
    bool IsAvailable,
    double? Latitude,
    double? Longitude,
    string? LocationName,
    DateTime? AvailabilityStartedAt,
    DateTime? AvailabilityExpiresAt,
    DateTime? LocationUpdatedAt,
    double MaxTravelRadiusKm,
    bool AllowLocationSharing);

public record LocationUpdateResponse(
    double Latitude,
    double Longitude,
    string? LocationName,
    DateTime UpdatedAt);

public record AvailableUserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? ProfileImageUrl,
    decimal AverageRating,
    int TotalRatings,
    int CompletedDeliveries,
    int CompletedTrips,
    bool IsVerified,
    double DistanceKm,
    string? LocationName,
    DateTime? LocationUpdatedAt);

public record AvailableUsersPagedResponse(
    IEnumerable<AvailableUserResponse> Users,
    int TotalCount,
    int Page,
    int PageSize);
