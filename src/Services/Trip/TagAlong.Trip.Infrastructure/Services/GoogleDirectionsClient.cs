using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using System.Text.Json;

namespace TagAlong.Trip.Infrastructure.Services;

public record RouteInfo(LineString RouteLine, int DurationSeconds);

public interface IGoogleDirectionsClient
{
    Task<RouteInfo?> GetRouteAsync(double originLat, double originLon, double destLat, double destLon, CancellationToken ct = default);
    Task<int?> GetDetourDurationAsync(double originLat, double originLon, double pickupLat, double pickupLon, double dropoffLat, double dropoffLon, double destLat, double destLon, CancellationToken ct = default);
}

public class GoogleDirectionsClient : IGoogleDirectionsClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://maps.googleapis.com/maps/api/directions/json";

    public GoogleDirectionsClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["GoogleMaps:ApiKey"]
            ?? throw new InvalidOperationException("GoogleMaps:ApiKey not configured");
    }

    public async Task<RouteInfo?> GetRouteAsync(
        double originLat, double originLon,
        double destLat, double destLon,
        CancellationToken ct = default)
    {
        var url = $"{_baseUrl}?origin={originLat},{originLon}&destination={destLat},{destLon}&key={_apiKey}";
        using var doc = await FetchDirectionsAsync(url, ct);
        if (doc is null) return null;

        var root = doc.RootElement;
        if (!IsOk(root)) return null;

        var route = root.GetProperty("routes")[0];
        var polyline = route.GetProperty("overview_polyline").GetProperty("points").GetString();
        if (string.IsNullOrEmpty(polyline)) return null;

        var durationSeconds = SumLegDurations(route);
        var line = PolylineDecoder.Simplify(PolylineDecoder.Decode(polyline), 200);

        return new RouteInfo(line, durationSeconds);
    }

    // Returns total trip duration (seconds) for origin → pickup → dropoff → destination.
    public async Task<int?> GetDetourDurationAsync(
        double originLat, double originLon,
        double pickupLat, double pickupLon,
        double dropoffLat, double dropoffLon,
        double destLat, double destLon,
        CancellationToken ct = default)
    {
        var waypoints = $"{pickupLat},{pickupLon}|{dropoffLat},{dropoffLon}";
        var url = $"{_baseUrl}?origin={originLat},{originLon}&destination={destLat},{destLon}" +
                  $"&waypoints={Uri.EscapeDataString(waypoints)}&key={_apiKey}";

        using var doc = await FetchDirectionsAsync(url, ct);
        if (doc is null) return null;

        var root = doc.RootElement;
        if (!IsOk(root)) return null;

        var route = root.GetProperty("routes")[0];
        return SumLegDurations(route);
    }

    private async Task<JsonDocument?> FetchDirectionsAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsOk(JsonElement root) =>
        root.TryGetProperty("status", out var s) && s.GetString() == "OK" &&
        root.GetProperty("routes").GetArrayLength() > 0;

    private static int SumLegDurations(JsonElement route)
    {
        int total = 0;
        foreach (var leg in route.GetProperty("legs").EnumerateArray())
            total += leg.GetProperty("duration").GetProperty("value").GetInt32();
        return total;
    }
}
