namespace TagAlong.User.Domain.Entities;

/// <summary>Someone a user wants alerted if they feel unsafe on a ride.</summary>
public class EmergencyContact
{
    public Guid Id { get; private set; }
    public Guid AuthUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private EmergencyContact() { }

    public static EmergencyContact Create(Guid authUserId, string name, string phone) => new()
    {
        Id = Guid.NewGuid(),
        AuthUserId = authUserId,
        Name = name,
        Phone = phone,
        CreatedAt = DateTime.UtcNow,
    };
}

/// <summary>
/// Audio a rider or driver recorded during a ride for their safety. Stored
/// encrypted; only admins can play it back, when looking into a report.
/// Deleted after 30 days unless it's attached to a report.
/// </summary>
public class SafetyRecording
{
    public Guid Id { get; private set; }
    public Guid AuthUserId { get; private set; }
    public Guid? ConversationId { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public int DurationSeconds { get; private set; }
    /// <summary>Attached to a report: kept for longer.</summary>
    public bool Flagged { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SafetyRecording() { }

    public static SafetyRecording Create(Guid id, Guid authUserId, Guid? conversationId, string filePath, long sizeBytes, int durationSeconds) => new()
    {
        Id = id,
        AuthUserId = authUserId,
        ConversationId = conversationId,
        FilePath = filePath,
        SizeBytes = sizeBytes,
        DurationSeconds = durationSeconds,
        CreatedAt = DateTime.UtcNow,
    };

    public void Flag() => Flagged = true;
}
