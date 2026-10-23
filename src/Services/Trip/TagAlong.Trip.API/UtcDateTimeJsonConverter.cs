using System.Text.Json;
using System.Text.Json.Serialization;

namespace TagAlong.Trip.API;

/// <summary>
/// Times come out of the database without a zone; every one we store is UTC,
/// so write them with a "Z" and clients read them correctly.
/// Reading keeps the zone the client sent (or none) so it can be converted.
/// </summary>
public class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value,
        };
        writer.WriteStringValue(utc);
    }
}
