namespace TagAlong.Messaging.API.DTOs;

public record ConversationDto(
    Guid Id,
    Guid? PackageRequestId,
    Guid SenderId,
    Guid TravelerId,
    string? SenderName,
    string? TravelerName,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    MessageDto? LastMessage,
    Guid? RecipientUserId = null,
    string? RecipientName = null,
    decimal? AgreedPrice = null,
    Guid? LockInProposedBy = null,
    DateTime? StartedAt = null,
    DateTime? DeliveredAt = null,
    double? PassengerDestLat = null,
    double? PassengerDestLng = null,
    string? PassengerDestAddress = null,
    double? HelperLastLat = null,
    double? HelperLastLng = null,
    Guid? TripId = null,
    bool IsDelivery = false,
    double? PickupLat = null,
    double? PickupLng = null,
    string? PickupAddress = null,
    double? MeetLat = null,
    double? MeetLng = null,
    string? MeetName = null,
    double? DropLat = null,
    double? DropLng = null,
    string? DropName = null,
    decimal? PlatformFee = null,
    decimal? DriverEarning = null);

public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Content,
    string MessageType,
    decimal? ProposedPrice,
    DateTime SentAt,
    DateTime? ReadAt);

public record CreateConversationRequest(
    Guid TravelerId,
    Guid? PackageRequestId,
    string? InitialMessage,
    Guid? RecipientUserId = null,
    string? RecipientName = null,
    double? PassengerDestLat = null,
    double? PassengerDestLng = null,
    string? PassengerDestAddress = null,
    Guid? TripId = null,
    bool IsDelivery = false,
    double? PickupLat = null,
    double? PickupLng = null,
    string? PickupAddress = null,
    double? MeetLat = null,
    double? MeetLng = null,
    string? MeetName = null,
    double? DropLat = null,
    double? DropLng = null,
    string? DropName = null);

/// <summary>Driver moves the pickup (meet) and/or drop-off point. Omit a pair to leave it.</summary>
public record SetMeetPointsRequest(
    double? MeetLat = null,
    double? MeetLng = null,
    string? MeetName = null,
    double? DropLat = null,
    double? DropLng = null,
    string? DropName = null);

/// <summary>One completed ride in a driver's earnings.</summary>
public record RideEarningDto(
    Guid ConversationId,
    DateTime CompletedAt,
    string? PassengerName,
    string? From,
    string? To,
    decimal Fare,
    decimal PlatformFee,
    decimal Earning,
    bool IsDelivery);

public record EarningsTotalsDto(int Rides, decimal Fares, decimal PlatformFees, decimal Earnings);

/// <summary>A driver's earnings: totals for all time, this week and this month, plus recent rides.</summary>
public record EarningsDto(
    EarningsTotalsDto AllTime,
    EarningsTotalsDto ThisWeek,
    EarningsTotalsDto ThisMonth,
    decimal CurrentPlatformFee,
    List<RideEarningDto> Rides);

public record SendMessageRequest(
    string Content);

public record SendPriceProposalRequest(
    decimal ProposedPrice,
    string? Message);

public record AcceptPriceRequest(
    decimal AcceptedPrice,
    string? Message);

public record RejectPriceRequest(
    decimal? CounterPrice,
    string? Message);

public record ProposeLockInRequest(
    decimal AgreedPrice);
