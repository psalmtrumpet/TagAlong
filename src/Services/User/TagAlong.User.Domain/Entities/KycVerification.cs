namespace TagAlong.User.Domain.Entities;

public class KycVerification
{
    public Guid Id { get; private set; }
    public Guid AuthUserId { get; private set; }
    public string? NIN { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? MiddleName { get; private set; }
    public string? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public string? Nationality { get; private set; }
    public string? ResidenceState { get; private set; }
    public string? Address { get; private set; }
    /// <summary>Set when Smile ID approved the person but flagged the phone used (possible fraud).</summary>
    public string? DeviceFlag { get; private set; }
    /// <summary>Keyed hash of the full NIN, so one NIN can only verify one account.</summary>
    public string? NinHash { get; private set; }
    public string? PhotoPath { get; private set; }
    public KycStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string? QoreIdReference { get; private set; }
    public string? SmileJobId { get; private set; }
    public string? SmileUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? JobStartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private KycVerification() { }

    public static KycVerification Create(Guid authUserId, string? qoreIdReference = null, string? smileJobId = null) => new()
    {
        Id = Guid.NewGuid(),
        AuthUserId = authUserId,
        QoreIdReference = qoreIdReference,
        SmileJobId = smileJobId,
        Status = KycStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        JobStartedAt = DateTime.UtcNow
    };

    public void SetSmileJobId(string jobId)
    {
        SmileJobId = jobId;
    }

    public void SetSmileUserId(string userId)
    {
        SmileUserId = userId;
    }

    public void ResetForRetry(string smileJobId)
    {
        SmileJobId = smileJobId;
        SmileUserId = null;
        Status = KycStatus.Pending;
        FailureReason = null;
        CompletedAt = null;
        JobStartedAt = DateTime.UtcNow;
    }

    /// <summary>The NIN the user entered for this verification.</summary>
    public void RecordIdNumber(string? nin)
    {
        if (!string.IsNullOrWhiteSpace(nin)) NIN = MaskNin(nin);
    }

    /// <summary>
    /// NINs are never stored in full: the last 3 digits are hidden
    /// ("87246870***"). Already-masked values pass through unchanged.
    /// </summary>
    public static string MaskNin(string nin)
    {
        var n = nin.Trim();
        if (n.EndsWith("***")) return n;
        return n.Length <= 3 ? "***" : n[..^3] + "***";
    }

    /// <summary>Stores the selfie captured during verification.</summary>
    public void AttachPhoto(string photoPath) => PhotoPath = photoPath;

    public void FlagDevice(string reason) => DeviceFlag = reason;

    public void SetNinHash(string hash) => NinHash = hash;

    public const string NinTakenMessage =
        "This NIN is already linked to another TagAlong account. If it's yours, please contact TagAlong support.";

    /// <summary>Address held against the NIN, as Smile ID returns it.</summary>
    public void RecordAddress(string? address)
    {
        if (!string.IsNullOrWhiteSpace(address)) Address = address.Trim();
    }

    /// <summary>Name, date of birth and gender held against the NIN (kept even before the check finishes).</summary>
    public void RecordPersonalInfo(string? firstName, string? lastName, string? middleName, string? dateOfBirth, string? gender)
    {
        FirstName = firstName ?? FirstName;
        LastName = lastName ?? LastName;
        MiddleName = middleName ?? MiddleName;
        DateOfBirth = dateOfBirth ?? DateOfBirth;
        Gender = gender ?? Gender;
        Nationality ??= "Nigerian";
    }

    public void Complete(
        string nin,
        string? firstName,
        string? lastName,
        string? middleName,
        string? dateOfBirth,
        string? gender,
        string? nationality,
        string? residenceState,
        string? photoPath)
    {
        NIN = string.IsNullOrWhiteSpace(nin) ? NIN : MaskNin(nin);
        FirstName = firstName;
        LastName = lastName;
        MiddleName = middleName;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        Nationality = nationality;
        ResidenceState = residenceState;
        PhotoPath = photoPath;
        Status = KycStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        FailureReason = reason;
        Status = KycStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }
}

public enum KycStatus { Pending, Completed, Failed }
