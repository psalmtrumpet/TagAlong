using TagAlong.Messaging.Domain.Repositories;

namespace TagAlong.Messaging.API.Services;

/// <summary>
/// Seats left on a shared trip: the driver's capacity minus the seats already
/// booked (a request can be for several passengers).
/// </summary>
public class TripSeats
{
    private readonly IHttpClientFactory _http;
    private readonly IConversationRepository _conversations;
    private readonly ILogger<TripSeats> _logger;

    public TripSeats(IHttpClientFactory http, IConversationRepository conversations, ILogger<TripSeats> logger)
    {
        _http = http;
        _conversations = conversations;
        _logger = logger;
    }

    /// <summary>Seats still free, or null if the trip service couldn't say (then don't block).</summary>
    public async Task<int?> LeftAsync(Guid tripId, CancellationToken ct)
    {
        try
        {
            using var res = await _http.CreateClient("trip-api").GetAsync($"api/trips/{tripId}", ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("passengerCapacity", out var cap) || cap.ValueKind != System.Text.Json.JsonValueKind.Number)
                return null;
            var booked = await _conversations.CountActiveBookingsByTripIdAsync(tripId, ct);
            return Math.Max(0, cap.GetInt32() - booked);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't check seats on trip {TripId}", tripId);
            return null;
        }
    }

    public static string NotEnough(int left) =>
        left == 0 ? "This ride is full." : $"Only {left} seat{(left == 1 ? "" : "s")} left on this ride.";
}
