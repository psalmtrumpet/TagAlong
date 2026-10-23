namespace TagAlong.Trip.Domain;

/// <summary>
/// Trip times are stored in UTC. Older app builds send departure times as plain
/// Lagos time with no zone; Nigeria is UTC+1 all year (no daylight saving).
/// </summary>
public static class TripTime
{
    public const int LagosOffsetHours = 1;

    /// <summary>UTC for any incoming time: "Z"/offset times convert, zone-less ones are Lagos time.</summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.AddHours(-LagosOffsetHours), DateTimeKind.Utc),
    };

    public static DateTime? ToUtc(DateTime? value) => value.HasValue ? ToUtc(value.Value) : null;

    /// <summary>A calendar day in Lagos as a UTC range [start, end).</summary>
    public static (DateTime Start, DateTime End) LagosDayUtc(DateTime day)
    {
        var start = DateTime.SpecifyKind(day.Date.AddHours(-LagosOffsetHours), DateTimeKind.Utc);
        return (start, start.AddDays(1));
    }
}
