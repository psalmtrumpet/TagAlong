namespace TagAlong.Identity.Domain.Entities;

public class WaitlistEntry
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public DateTime JoinedAt { get; private set; }

    private WaitlistEntry() { }

    public static WaitlistEntry Create(string name, string email, string phone)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name.Trim().Length > 100 ? name.Trim()[..100] : name.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            Phone = string.IsNullOrWhiteSpace(phone) ? string.Empty : phone.Trim(),
            JoinedAt = DateTime.UtcNow,
        };
}
