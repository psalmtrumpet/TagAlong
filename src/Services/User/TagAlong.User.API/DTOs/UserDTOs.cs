namespace TagAlong.User.API.DTOs;

public record UserProfileResponse(
    Guid Id,
    Guid AuthUserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Bio,
    string? ProfileImageUrl,
    decimal AverageRating,
    int TotalRatings,
    int CompletedDeliveries,
    int CompletedTrips,
    bool IsVerified,
    string VerificationStatus,
    DateTime CreatedAt);

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? Bio,
    string? ProfileImageUrl);

public record UserPublicProfileResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Bio,
    string? ProfileImageUrl,
    decimal AverageRating,
    int TotalRatings,
    int CompletedDeliveries,
    int CompletedTrips,
    bool IsVerified);
