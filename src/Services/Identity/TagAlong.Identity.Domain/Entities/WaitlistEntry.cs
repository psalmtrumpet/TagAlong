namespace TagAlong.Identity.Domain.Entities;

public class WaitlistEntry
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    /// <summary>Optional — the WhatsApp number is how we reach people at launch.</summary>
    public string? Email { get; private set; }
    public string Phone { get; private set; } = string.Empty;
    /// <summary>Area of Lagos the person signed up for (picked from the list, or typed via "Other").</summary>
    public string? Area { get; private set; }
    /// <summary>"ride" (book rides) or "drive" (drive and earn).</summary>
    public string? Role { get; private set; }
    /// <summary>Their usual trip — tells us which routes to launch first.</summary>
    public string? FromArea { get; private set; }
    public string? ToArea { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private WaitlistEntry() { }

    public static WaitlistEntry Create(string? email, string phone, string? area, string? name = null,
        string? role = null, string? fromArea = null, string? toArea = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = Clip(name, 100) ?? string.Empty,
            Email = Clip(email, 254)?.ToLowerInvariant(),
            Phone = NormalizePhone(phone),
            Area = Clip(area, 100) ?? Clip(fromArea, 100),
            Role = Clip(role, 20)?.ToLowerInvariant(),
            FromArea = Clip(fromArea, 100),
            ToArea = Clip(toArea, 100),
            JoinedAt = DateTime.UtcNow,
        };

    /// <summary>Digits only, Nigerian numbers as 234… — so the same number isn't signed up twice.</summary>
    public static string NormalizePhone(string? phone)
    {
        var d = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (d.Length == 11 && d.StartsWith('0')) d = "234" + d[1..];
        return d.Length > 20 ? d[..20] : d;
    }

    private static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}
