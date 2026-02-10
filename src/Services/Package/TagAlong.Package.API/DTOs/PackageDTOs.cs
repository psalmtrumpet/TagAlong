namespace TagAlong.Package.API.DTOs;

public record CreatePackageRequestDto(
    string PickupLocation,
    double PickupLatitude,
    double PickupLongitude,
    string DeliveryLocation,
    double DeliveryLatitude,
    double DeliveryLongitude,
    string PackageDescription,
    string Size,
    decimal EstimatedWeight,
    decimal? OfferedPrice,
    string? SpecialInstructions,
    DateTime? RequiredByDate,
    string? PackageImageUrl);

public record PackageRequestResponse(
    Guid Id,
    Guid SenderId,
    string PickupLocation,
    double PickupLatitude,
    double PickupLongitude,
    string DeliveryLocation,
    double DeliveryLatitude,
    double DeliveryLongitude,
    string PackageDescription,
    string Size,
    decimal EstimatedWeight,
    decimal? OfferedPrice,
    string Status,
    string? SpecialInstructions,
    DateTime? RequiredByDate,
    string? PackageImageUrl,
    DateTime CreatedAt);

public record CreateDeliveryDto(
    Guid PackageRequestId,
    Guid TripId,
    decimal AgreedPrice,
    string? MeetupLocation,
    double? MeetupLatitude,
    double? MeetupLongitude,
    DateTime? MeetupTime,
    string? ReceiverName,
    string? ReceiverPhone);

public record DeliveryResponse(
    Guid Id,
    Guid PackageRequestId,
    Guid TripId,
    Guid SenderId,
    Guid TravelerId,
    decimal AgreedPrice,
    decimal PlatformFee,
    decimal TravelerPayout,
    string Status,
    string? MeetupLocation,
    double? MeetupLatitude,
    double? MeetupLongitude,
    DateTime? MeetupTime,
    DateTime? PickedUpAt,
    DateTime? DeliveredAt,
    string? DeliveryProofImageUrl,
    string? ReceiverName,
    string? ReceiverPhone,
    string? Notes,
    DateTime CreatedAt);

public record UpdateDeliveryStatusDto(string Status, string? ProofImageUrl = null);

public record SearchPackageRequestsDto(
    string? PickupLocation,
    string? DeliveryLocation,
    double? PickupLatitude,
    double? PickupLongitude,
    double? DeliveryLatitude,
    double? DeliveryLongitude,
    double RadiusKm = 10,
    int Page = 1,
    int PageSize = 20);
