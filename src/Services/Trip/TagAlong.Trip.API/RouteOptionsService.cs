using System.Globalization;
using System.Text.Json;

namespace TagAlong.Trip.API;

/// <param name="Summary">Main roads, e.g. "Mobolaji Bank Anthony Way".</param>
/// <param name="DurationInTrafficSeconds">Expected time with current traffic (same as Duration when unknown).</param>
/// <param name="Polyline">Google-encoded route line.</param>
/// <param name="Fastest">Quickest in current traffic.</param>
/// <param name="LeastTraffic">Smallest delay compared with a clear road.</param>
public record RouteOptionDto(
    string Summary,
    int DistanceMeters,
    int DurationSeconds,
    int DurationInTrafficSeconds,
    string Polyline,
    bool Fastest,
    bool LeastTraffic);

/// <summary>
/// Route choices for a new trip so the driver can pick the road they'll
/// actually take: Google with live traffic, or OSRM (no traffic) as a fallback.
/// </summary>
public class RouteOptionsService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<RouteOptionsService> _logger;

    public RouteOptionsService(IHttpClientFactory http, IConfiguration config, ILogger<RouteOptionsService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<List<RouteOptionDto>> GetAsync(double oLat, double oLng, double dLat, double dLng,
        double? viaLat, double? viaLng, DateTime? departure, CancellationToken ct)
    {
        var raw = await GoogleAsync(oLat, oLng, dLat, dLng, viaLat, viaLng, departure, ct)
                  ?? await OsrmAsync(oLat, oLng, dLat, dLng, viaLat, viaLng, ct)
                  ?? new List<RouteOptionDto>();
        if (raw.Count == 0) return raw;

        var fastest = raw.MinBy(r => r.DurationInTrafficSeconds)!;
        var hasTraffic = raw.Any(r => r.DurationInTrafficSeconds != r.DurationSeconds);
        var leastTraffic = hasTraffic && raw.Count > 1
            ? raw.MinBy(r => r.DurationInTrafficSeconds - r.DurationSeconds)
            : null;

        return raw
            .Select(r => r with { Fastest = ReferenceEquals(r, fastest), LeastTraffic = ReferenceEquals(r, leastTraffic) })
            .OrderBy(r => r.DurationInTrafficSeconds)
            .ToList();
    }

    private async Task<List<RouteOptionDto>?> GoogleAsync(double oLat, double oLng, double dLat, double dLng,
        double? viaLat, double? viaLng, DateTime? departure, CancellationToken ct)
    {
        var key = _config["GoogleMaps:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            var c = CultureInfo.InvariantCulture;
            // Traffic needs a departure time that isn't in the past
            var when = departure is { } d && d.ToUniversalTime() > DateTime.UtcNow
                ? new DateTimeOffset(d.ToUniversalTime()).ToUnixTimeSeconds().ToString(c)
                : "now";
            var url = "https://maps.googleapis.com/maps/api/directions/json" +
                      $"?origin={oLat.ToString(c)},{oLng.ToString(c)}&destination={dLat.ToString(c)},{dLng.ToString(c)}" +
                      $"&departure_time={when}&traffic_model=best_guess&key={key}";
            // Google only offers alternatives when there's no stop in between
            url += viaLat is { } vl && viaLng is { } vg
                ? $"&waypoints=via:{vl.ToString(c)},{vg.ToString(c)}"
                : "&alternatives=true";

            using var res = await _http.CreateClient("osrm").GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var status = doc.RootElement.GetProperty("status").GetString();
            if (status != "OK")
            {
                _logger.LogWarning("Route options: Google said {Status}", status);
                return null;
            }

            var list = new List<RouteOptionDto>();
            foreach (var r in doc.RootElement.GetProperty("routes").EnumerateArray())
            {
                int dist = 0, dur = 0, traffic = 0;
                foreach (var leg in r.GetProperty("legs").EnumerateArray())
                {
                    dist += leg.GetProperty("distance").GetProperty("value").GetInt32();
                    var legDur = leg.GetProperty("duration").GetProperty("value").GetInt32();
                    dur += legDur;
                    traffic += leg.TryGetProperty("duration_in_traffic", out var t) ? t.GetProperty("value").GetInt32() : legDur;
                }
                var summary = r.TryGetProperty("summary", out var s) && !string.IsNullOrWhiteSpace(s.GetString()) ? s.GetString()! : "Suggested route";
                list.Add(new RouteOptionDto(summary, dist, dur, traffic,
                    r.GetProperty("overview_polyline").GetProperty("points").GetString() ?? "", false, false));
            }
            return list.Count > 0 ? list : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Route options: Google lookup failed");
            return null;
        }
    }

    private async Task<List<RouteOptionDto>?> OsrmAsync(double oLat, double oLng, double dLat, double dLng,
        double? viaLat, double? viaLng, CancellationToken ct)
    {
        try
        {
            var c = CultureInfo.InvariantCulture;
            var coords = $"{oLng.ToString(c)},{oLat.ToString(c)}";
            if (viaLat is { } vl && viaLng is { } vg) coords += $";{vg.ToString(c)},{vl.ToString(c)}";
            coords += $";{dLng.ToString(c)},{dLat.ToString(c)}";
            var url = $"https://router.project-osrm.org/route/v1/driving/{coords}?overview=full&geometries=polyline" +
                      (viaLat == null ? "&alternatives=3" : "");
            using var res = await _http.CreateClient("osrm").GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.GetProperty("code").GetString() != "Ok") return null;

            var list = new List<RouteOptionDto>();
            foreach (var r in doc.RootElement.GetProperty("routes").EnumerateArray())
            {
                var summary = string.Join(" and ", r.GetProperty("legs").EnumerateArray()
                    .Select(l => l.TryGetProperty("summary", out var s) ? s.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                var dur = (int)r.GetProperty("duration").GetDouble();
                list.Add(new RouteOptionDto(string.IsNullOrWhiteSpace(summary) ? "Suggested route" : summary,
                    (int)r.GetProperty("distance").GetDouble(), dur, dur,
                    r.GetProperty("geometry").GetString() ?? "", false, false));
            }
            return list.Count > 0 ? list : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Route options: OSRM lookup failed");
            return null;
        }
    }
}
