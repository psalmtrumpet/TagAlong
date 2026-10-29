namespace TagAlong.User.Domain.Entities;

/// <summary>
/// Something a driver's car offers (air conditioning, tinted windows, music…).
/// Admins manage the list; drivers tick the ones they have when they offer a
/// trip or go available, and passengers see them. Optional.
/// </summary>
public class RideFeature
{
    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    /// <summary>Which picture the app shows (e.g. "ac", "music"); unknown ones get a tick.</summary>
    public string Icon { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    /// <summary>Hidden ones still show on trips that already have them, but can't be picked.</summary>
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private RideFeature() { }

    public static RideFeature Create(string name, string icon, int sortOrder) => new()
    {
        Name = name,
        Icon = icon,
        SortOrder = sortOrder,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    public void Update(string name, string icon, int sortOrder, bool isActive)
    {
        Name = name;
        Icon = icon;
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    /// <summary>"3,1,7" → [3, 1, 7]: the ids a driver picked, as stored.</summary>
    public static List<int> ParseIds(string? stored) =>
        (stored ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(id => id > 0).Distinct().ToList();

    public static string? FormatIds(IEnumerable<int>? ids)
    {
        var list = (ids ?? Enumerable.Empty<int>()).Where(id => id > 0).Distinct().Take(20).ToList();
        return list.Count == 0 ? null : string.Join(',', list);
    }
}
