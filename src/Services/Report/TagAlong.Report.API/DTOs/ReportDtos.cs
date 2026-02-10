namespace TagAlong.Report.API.DTOs;

public record ReportDto(
    Guid Id,
    Guid ReporterId,
    Guid? ReportedUserId,
    Guid? ReportedDeliveryId,
    string ReportType,
    string Reason,
    string Description,
    string Status,
    string? AdminNotes,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? Resolution,
    DateTime CreatedAt);

public record CreateUserReportRequest(
    Guid ReportedUserId,
    string Reason,
    string Description);

public record CreateDeliveryReportRequest(
    Guid ReportedDeliveryId,
    string Reason,
    string Description);

public record ReviewReportRequest(string AdminNotes);

public record ResolveReportRequest(string Resolution);

public record RejectReportRequest(string Resolution);
