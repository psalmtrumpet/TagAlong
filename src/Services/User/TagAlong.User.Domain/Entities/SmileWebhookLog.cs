namespace TagAlong.User.Domain.Entities;

public class SmileWebhookLog
{
    public Guid Id { get; private set; }
    public string? JobId { get; private set; }
    public string? ResultCode { get; private set; }
    public Guid? AuthUserId { get; private set; }
    public bool IsJobStatusResult { get; private set; }
    public string Outcome { get; private set; } = string.Empty;
    public string? BodySnippet { get; private set; }
    public DateTime ReceivedAt { get; private set; }

    private SmileWebhookLog() { }

    public static SmileWebhookLog Create(
        string? jobId,
        string? resultCode,
        Guid? authUserId,
        bool isJobStatusResult,
        string outcome,
        string? rawBody)
    {
        var snippet = rawBody?.Length > 1000 ? rawBody[..1000] : rawBody;
        return new SmileWebhookLog
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            ResultCode = resultCode,
            AuthUserId = authUserId,
            IsJobStatusResult = isJobStatusResult,
            Outcome = outcome,
            BodySnippet = snippet,
            ReceivedAt = DateTime.UtcNow,
        };
    }
}
