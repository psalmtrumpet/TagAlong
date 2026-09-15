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
    public string? PhotoPath { get; private set; }
    public KycStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string? QoreIdReference { get; private set; }
    public string? SmileJobId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private KycVerification() { }

    public static KycVerification Create(Guid authUserId, string? qoreIdReference = null, string? smileJobId = null) => new()
    {
        Id = Guid.NewGuid(),
        AuthUserId = authUserId,
        QoreIdReference = qoreIdReference,
        SmileJobId = smileJobId,
        Status = KycStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    public void SetSmileJobId(string jobId)
    {
        SmileJobId = jobId;
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
        NIN = nin;
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
