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
/// Road routes from Google Directions with live traffic: the choices for a new
/// trip, and the live route a map follows during a ride. Every call is counted
/// per trip (<see cref="GoogleCallLog"/>). There is no free fallback: when Google
/// fails the caller gets nothing and the app shows a marked straight line.
/// </summary>
public class RouteOptionsService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<RouteOptionsService> _logger;
    private readonly GoogleCallLog _calls;

    public RouteOptionsService(IHttpClientFactory http, IConfiguration config, ILogger<RouteOptionsService> logger, GoogleCallLog calls)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _calls = calls;
    }

    public async Task<List<RouteOptionDto>> GetAsync(double oLat, double oLng, double dLat, double dLng,
        double? viaLat, double? viaLng, DateTime? departure, CancellationToken ct, Guid? userId = null)
    {
        var raw = await GoogleAsync(oLat, oLng, dLat, dLng, viaLat, viaLng, departure, alternatives: true, ct);
        await _calls.RecordAsync("route-options", raw != null, userId);
        if (raw == null || raw.Count == 0) return new List<RouteOptionDto>();

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

    /// <summary>The one route a live map follows right now (traffic as of now), or null.</summary>
    public async Task<RouteOptionDto?> LiveAsync(double oLat, double oLng, double dLat, double dLng,
        Guid? userId, Guid? tripId, Guid? conversationId, CancellationToken ct)
    {
        var raw = await GoogleAsync(oLat, oLng, dLat, dLng, null, null, null, alternatives: false, ct);
        await _calls.RecordAsync("live-route", raw != null, userId, tripId, conversationId);
        return raw?.FirstOrDefault();
    }

    private async Task<List<RouteOptionDto>?> GoogleAsync(double oLat, double oLng, double dLat, double dLng,
        double? viaLat, double? viaLng, DateTime? departure, bool alternatives, CancellationToken ct)
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
            if (viaLat is { } vl && viaLng is { } vg)
                url += $"&waypoints=via:{vl.ToString(c)},{vg.ToString(c)}";
            else if (alternatives)
                url += "&alternatives=true";

            using var res = await _http.CreateClient("osrm").GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var status = doc.RootElement.GetProperty("status").GetString();
            if (status != "OK")
            {
                _logger.LogWarning("Directions: Google said {Status}", status);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Directions: Google lookup failed");
            return null;
        }
    }
}
