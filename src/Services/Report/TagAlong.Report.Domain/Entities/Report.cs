using TagAlong.Common.Domain;

namespace TagAlong.Report.Domain.Entities;

public class Report : Entity
{
    public Guid ReporterId { get; private set; }
    public Guid? ReportedUserId { get; private set; }
    public Guid? ReportedDeliveryId { get; private set; }
    public ReportType ReportType { get; private set; }
    public ReportReason Reason { get; private set; }
    public string Description { get; private set; } = null!;
    public ReportStatus Status { get; private set; }
    public string? AdminNotes { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? Resolution { get; private set; }

    private Report() { }

    public static Report CreateUserReport(
        Guid reporterId,
        Guid reportedUserId,
        ReportReason reason,
        string description)
    {
        return new Report
        {
            ReporterId = reporterId,
            ReportedUserId = reportedUserId,
            ReportType = ReportType.User,
            Reason = reason,
            Description = description,
            Status = ReportStatus.Pending
        };
    }

    public static Report CreateDeliveryReport(
        Guid reporterId,
        Guid reportedDeliveryId,
        ReportReason reason,
        string description)
    {
        return new Report
        {
            ReporterId = reporterId,
            ReportedDeliveryId = reportedDeliveryId,
            ReportType = ReportType.Delivery,
            Reason = reason,
            Description = description,
            Status = ReportStatus.Pending
        };
    }

    public void MarkAsReviewed(Guid reviewedBy, string adminNotes)
    {
        Status = ReportStatus.UnderReview;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        AdminNotes = adminNotes;
        SetUpdated();
    }

    public void Resolve(string resolution)
    {
        Status = ReportStatus.Resolved;
        Resolution = resolution;
        SetUpdated();
    }

    public void Reject(string resolution)
    {
        Status = ReportStatus.Rejected;
        Resolution = resolution;
        SetUpdated();
    }

    public void Escalate()
    {
        Status = ReportStatus.Escalated;
        SetUpdated();
    }
}

public enum ReportType
{
    User,
    Delivery
}

public enum ReportReason
{
    IllegalItems,
    StolenGoods,
    Fraud,
    Harassment,
    Spam,
    FakeProfile,
    DangerousItems,
    NonDelivery,
    DamagedPackage,
    Other
}

public enum ReportStatus
{
    Pending,
    UnderReview,
    Resolved,
    Rejected,
    Escalated
}
