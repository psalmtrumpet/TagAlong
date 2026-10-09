using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TagAlong.Trip.Infrastructure.Services;

/// <summary>
/// Free OpenStreetMap routing (OSRM). Used when Google Directions is
/// unavailable, e.g. when its API key has expired.
/// </summary>
public class OsrmDirectionsClient
{
    public const string BaseUrl = "https://router.project-osrm.org/route/v1/driving/";
    private readonly HttpClient _http;

    public OsrmDirectionsClient(HttpClient http) => _http = http;

    public async Task<RouteInfo?> GetRouteAsync(double originLat, double originLon, double destLat, double destLon, CancellationToken ct = default)
    {
        using var doc = await FetchAsync(new[] { (originLat, originLon), (destLat, destLon) }, "full", ct);
        if (doc == null) return null;
        var route = doc.RootElement.GetProperty("routes")[0];
        var polyline = route.GetProperty("geometry").GetString();
        if (string.IsNullOrEmpty(polyline)) return null;
        var line = PolylineDecoder.Simplify(PolylineDecoder.Decode(polyline), 200);
        return new RouteInfo(line, (int)route.GetProperty("duration").GetDouble());
    }

    public async Task<int?> GetDurationAsync((double Lat, double Lon)[] points, CancellationToken ct = default)
    {
        using var doc = await FetchAsync(points, "false", ct);
        return doc == null ? null : (int)doc.RootElement.GetProperty("routes")[0].GetProperty("duration").GetDouble();
    }

    private async Task<JsonDocument?> FetchAsync((double Lat, double Lon)[] points, string overview, CancellationToken ct)
    {
        try
        {
            var coords = string.Join(";", points.Select(p =>
                $"{p.Lon.ToString(CultureInfo.InvariantCulture)},{p.Lat.ToString(CultureInfo.InvariantCulture)}"));
            using var res = await _http.GetAsync($"{BaseUrl}{coords}?overview={overview}&geometries=polyline", ct);
            if (!res.IsSuccessStatusCode) return null;
            var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.GetProperty("code").GetString() != "Ok" || doc.RootElement.GetProperty("routes").GetArrayLength() == 0)
            {
                doc.Dispose();
                return null;
            }
            return doc;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Google Directions first; OSRM when Google returns nothing.</summary>
public class FallbackDirectionsClient : IGoogleDirectionsClient
{
    private readonly GoogleDirectionsClient? _google;
    private readonly OsrmDirectionsClient _osrm;
    private readonly ILogger<FallbackDirectionsClient> _logger;

    public FallbackDirectionsClient(OsrmDirectionsClient osrm, ILogger<FallbackDirectionsClient> logger, GoogleDirectionsClient? google = null)
    {
        _google = google;
        _osrm = osrm;
        _logger = logger;
    }

    public async Task<RouteInfo?> GetRouteAsync(double originLat, double originLon, double destLat, double destLon, CancellationToken ct = default)
    {
        if (_google != null)
        {
            var r = await _google.GetRouteAsync(originLat, originLon, destLat, destLon, ct);
            if (r != null) return r;
            _logger.LogWarning("Google Directions returned no route — using OSRM");
        }
        return await _osrm.GetRouteAsync(originLat, originLon, destLat, destLon, ct);
    }

    public async Task<int?> GetDetourDurationAsync(double originLat, double originLon, double pickupLat, double pickupLon,
        double dropoffLat, double dropoffLon, double destLat, double destLon, CancellationToken ct = default)
    {
        if (_google != null)
        {
            var d = await _google.GetDetourDurationAsync(originLat, originLon, pickupLat, pickupLon, dropoffLat, dropoffLon, destLat, destLon, ct);
            if (d != null) return d;
        }
        return await _osrm.GetDurationAsync(new[]
        {
            (originLat, originLon), (pickupLat, pickupLon), (dropoffLat, dropoffLon), (destLat, destLon),
        }, ct);
    }
}
