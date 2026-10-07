namespace TagAlong.User.Domain.Entities;

public enum DriverProfileStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Driver's licence and vehicle details, submitted the first time a user
/// offers a ride or delivery. An admin must approve them before the user
/// can take passengers or packages.
/// </summary>
public class DriverProfile
{
    public Guid Id { get; private set; }
    public Guid AuthUserId { get; private set; }

    public string LicenseNumber { get; private set; } = string.Empty;
    public DateTime? LicenseExpiry { get; private set; }
    public string LicenseImagePath { get; private set; } = string.Empty;

    public string VehicleType { get; private set; } = string.Empty;
    public string VehicleMake { get; private set; } = string.Empty;
    public string VehicleModel { get; private set; } = string.Empty;
    public string VehicleColor { get; private set; } = string.Empty;
    public string VehiclePlate { get; private set; } = string.Empty;
    /// <summary>Front of the vehicle, plate visible.</summary>
    public string? VehicleImagePath { get; private set; }
    /// <summary>Back of the vehicle, plate visible.</summary>
    public string? VehicleBackImagePath { get; private set; }

    public DriverProfileStatus Status { get; private set; } = DriverProfileStatus.Pending;
    public string? RejectionReason { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }

    // Automatic document read (helps the reviewer; never decides on its own)
    public string? DocumentCheckJson { get; private set; }
    public int? DocumentCheckFailures { get; private set; }
    public DateTime? DocumentCheckedAt { get; private set; }

    private DriverProfile() { }

    public static DriverProfile Create(Guid authUserId) => new()
    {
        Id = Guid.NewGuid(),
        AuthUserId = authUserId,
    };

    /// <summary>Any (re)submission goes back to review.</summary>
    public void Submit(
        string licenseNumber, DateTime? licenseExpiry, string? licenseImagePath,
        string vehicleType, string vehicleMake, string vehicleModel, string vehicleColor,
        string vehiclePlate, string? vehicleImagePath, string? vehicleBackImagePath)
    {
        LicenseNumber = licenseNumber.Trim().ToUpperInvariant();
        LicenseExpiry = licenseExpiry;
        if (!string.IsNullOrEmpty(licenseImagePath)) LicenseImagePath = licenseImagePath;
        VehicleType = vehicleType.Trim();
        VehicleMake = vehicleMake.Trim();
        VehicleModel = vehicleModel.Trim();
        VehicleColor = vehicleColor.Trim();
        VehiclePlate = NormalisePlate(vehiclePlate);
        if (!string.IsNullOrEmpty(vehicleImagePath)) VehicleImagePath = vehicleImagePath;
        if (!string.IsNullOrEmpty(vehicleBackImagePath)) VehicleBackImagePath = vehicleBackImagePath;

        Status = DriverProfileStatus.Pending;
        RejectionReason = null;
        SubmittedAt = DateTime.UtcNow;
        ReviewedAt = null;
        ReviewedBy = null;
        DocumentCheckJson = null;
        DocumentCheckFailures = null;
        DocumentCheckedAt = null;
    }

    public void Approve(Guid adminId)
    {
        Status = DriverProfileStatus.Approved;
        RejectionReason = null;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = adminId;
    }

    public void Reject(Guid adminId, string reason)
    {
        Status = DriverProfileStatus.Rejected;
        RejectionReason = reason.Trim();
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = adminId;
    }

    public void RecordDocumentCheck(string json, int failures)
    {
        DocumentCheckJson = json;
        DocumentCheckFailures = failures;
        DocumentCheckedAt = DateTime.UtcNow;
    }

    public bool IsApproved => Status == DriverProfileStatus.Approved;

    public static string NormalisePlate(string plate) =>
        new string(plate.Trim().ToUpperInvariant().Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' ').ToArray())
            .Replace(' ', '-');
}
