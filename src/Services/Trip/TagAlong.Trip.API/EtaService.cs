using System.Globalization;
using System.Text.Json;

namespace TagAlong.Trip.API;

public record EtaPoint(double Lat, double Lng);

/// <summary>Drive time from several places to one place, e.g. nearby drivers to a passenger.</summary>
public record EtaRequest(List<EtaPoint> Origins, double DestLat, double DestLng);

/// <param name="Seconds">One per origin, in the same order; null where no route was found.</param>
public record EtaResponse(List<int?> Seconds);

/// <summary>
/// Drive times for on-demand ride requests: Google Distance Matrix with live
/// traffic, or the free OSRM table (no traffic) when Google isn't available.
/// </summary>
public class EtaService
{
    private const int GoogleMaxOrigins = 25;

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<EtaService> _logger;

    public EtaService(IHttpClientFactory http, IConfiguration config, ILogger<EtaService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<List<int?>> GetAsync(List<EtaPoint> origins, double destLat, double destLng, CancellationToken ct)
    {
        if (origins.Count == 0) return new List<int?>();
        return await GoogleAsync(origins, destLat, destLng, ct)
               ?? await OsrmAsync(origins, destLat, destLng, ct)
               ?? origins.Select(_ => (int?)null).ToList();
    }

    private async Task<List<int?>?> GoogleAsync(List<EtaPoint> origins, double destLat, double destLng, CancellationToken ct)
    {
        var key = _config["GoogleMaps:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return null;
        var c = CultureInfo.InvariantCulture;
        var result = new List<int?>();
        try
        {
            foreach (var chunk in origins.Chunk(GoogleMaxOrigins))
            {
                var from = string.Join("|", chunk.Select(o => $"{o.Lat.ToString(c)},{o.Lng.ToString(c)}"));
                var url = "https://maps.googleapis.com/maps/api/distancematrix/json" +
                          $"?origins={Uri.EscapeDataString(from)}&destinations={destLat.ToString(c)},{destLng.ToString(c)}" +
                          $"&departure_time=now&traffic_model=best_guess&key={key}";
                using var res = await _http.CreateClient("osrm").GetAsync(url, ct);
                if (!res.IsSuccessStatusCode) return null;
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.GetProperty("status").GetString() != "OK")
                {
                    _logger.LogWarning("Distance Matrix: {Status}", doc.RootElement.GetProperty("status").GetString());
                    return null;
                }
                foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
                {
                    var el = row.GetProperty("elements")[0];
                    if (el.GetProperty("status").GetString() != "OK") { result.Add(null); continue; }
                    var d = el.TryGetProperty("duration_in_traffic", out var t) ? t : el.GetProperty("duration");
                    result.Add(d.GetProperty("value").GetInt32());
                }
            }
            return result.Count == origins.Count ? result : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Distance Matrix failed — using OSRM");
            return null;
        }
    }

    private async Task<List<int?>?> OsrmAsync(List<EtaPoint> origins, double destLat, double destLng, CancellationToken ct)
    {
        var c = CultureInfo.InvariantCulture;
        try
        {
            var coords = string.Join(";", origins.Select(o => $"{o.Lng.ToString(c)},{o.Lat.ToString(c)}")) +
                         $";{destLng.ToString(c)},{destLat.ToString(c)}";
            var sources = string.Join(";", Enumerable.Range(0, origins.Count));
            var url = $"https://router.project-osrm.org/table/v1/driving/{coords}" +
                      $"?sources={sources}&destinations={origins.Count}&annotations=duration";
            using var res = await _http.CreateClient("osrm").GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.GetProperty("code").GetString() != "Ok") return null;
            return doc.RootElement.GetProperty("durations").EnumerateArray()
                .Select(row => row[0].ValueKind == JsonValueKind.Number ? (int?)(int)Math.Round(row[0].GetDouble()) : null)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "OSRM table failed");
            return null;
        }
    }
}
