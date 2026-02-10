namespace TagAlong.Common.Contracts.Events;

public record ReportSubmittedEvent(
    Guid ReportId,
    Guid ReporterId,
    Guid ReportedUserId,
    Guid? DeliveryId,
    string ReportType,
    string Description,
    DateTime SubmittedAt);
