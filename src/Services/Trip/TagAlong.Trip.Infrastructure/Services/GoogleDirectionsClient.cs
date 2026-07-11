using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using System.Text.Json;

namespace TagAlong.Trip.Infrastructure.Services;

public class GoogleDirectionsClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GoogleDirectionsClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["GoogleMaps:ApiKey"]
            ?? throw new InvalidOperationException("GoogleMaps:ApiKey not configured");
    }

    public async Task<LineString?> GetRouteAsync(
        double originLat, double originLon,
        double destLat, double destLon,
        CancellationToken ct = default)
    {
        var url = $"https://maps.googleapis.com/maps/api/directions/json" +
                  $"?origin={originLat},{originLon}" +
                  $"&destination={destLat},{destLon}" +
                  $"&key={_apiKey}";

        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("status", out var status) || status.GetString() != "OK")
            return null;

        var routes = root.GetProperty("routes");
        if (routes.GetArrayLength() == 0)
            return null;

        var overviewPolyline = routes[0]
            .GetProperty("overview_polyline")
            .GetProperty("points")
            .GetString();

        if (string.IsNullOrEmpty(overviewPolyline))
            return null;

        var line = PolylineDecoder.Decode(overviewPolyline);
        return PolylineDecoder.Simplify(line, 200);
    }
}
