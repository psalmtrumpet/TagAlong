namespace TagAlong.Identity.Domain.Entities;

public class WaitlistEntry
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    /// <summary>Area of Lagos the person signed up for (picked from the list, or typed via "Other").</summary>
    public string? Area { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private WaitlistEntry() { }

    public static WaitlistEntry Create(string email, string phone, string? area, string? name = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = Clip(name, 100) ?? string.Empty,
            Email = email.Trim().ToLowerInvariant(),
            Phone = Clip(phone, 20) ?? string.Empty,
            Area = Clip(area, 100),
            JoinedAt = DateTime.UtcNow,
        };

    private static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}
