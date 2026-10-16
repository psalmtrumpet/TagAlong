using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Messaging.API.Commands;
using TagAlong.Messaging.API.DTOs;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.API.Services;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.API.Hails;

/// <summary>
/// On-demand rides ("Ride now"): a passenger's request goes to every available
/// driver within a short drive. Drivers accept the offered price or counter;
/// the first accept (or the counter the passenger picks) gets the ride, which
/// starts straight away as a booked, private, trip-less conversation.
/// </summary>
public class HailService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly MessagingDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hub;
    private readonly IEventBus _eventBus;
    private readonly IUserLookupService _users;
    private readonly IConfiguration _config;
    private readonly ILogger<HailService> _logger;

    public HailService(MessagingDbContext db, IHttpClientFactory http, IHubContext<MessagingHub, IMessagingClient> hub,
        IEventBus eventBus, IUserLookupService users, IConfiguration config, ILogger<HailService> logger)
    {
        _db = db;
        _http = http;
        _hub = hub;
        _eventBus = eventBus;
        _users = users;
        _config = config;
        _logger = logger;
    }

    // ── Settings ──
    private double Setting(string key, double fallback) =>
        double.TryParse(_config[$"Hail:{key}"], NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : fallback;
    private int MaxEtaSeconds => (int)(Setting("MaxEtaMinutes", 20) * 60);
    private int FinishingSoonSeconds => (int)(Setting("FinishingSoonMinutes", 5) * 60);
    private double PrefilterRadiusKm => Setting("PrefilterRadiusKm", 15);
    private TimeSpan Lifetime => TimeSpan.FromSeconds(Setting("ExpirySeconds", 180));
    private const int MaxDrivers = 15;

    // ── Quote ──

    public async Task<HailQuoteDto> QuoteAsync(double pLat, double pLng, double dLat, double dLng, CancellationToken ct)
    {
        var meters = (int)Math.Round(HaversineKm(pLat, pLng, dLat, dLng) * 1000 * 1.3);
        var duration = (await EtasAsync(new List<(double, double)> { (pLat, pLng) }, dLat, dLng, ct)).FirstOrDefault();
        var raw = (decimal)Setting("SuggestedBase", 500) + (decimal)Setting("SuggestedPerKm", 150) * meters / 1000m;
        var suggested = Math.Max(50, Math.Round(raw / 50m) * 50);
        var nearby = (await DriversNearAsync(pLat, pLng, new List<Guid>(), ct)).Count;
        return new HailQuoteDto(meters, duration, suggested, nearby);
    }

    // ── Passenger ──

    public async Task<Result<HailDto>> CreateAsync(Guid passengerId, CreateHailRequest r, CancellationToken ct)
    {
        if (r.OfferedPrice <= 0) return Result.Failure<HailDto>(Error.Validation("Enter the price you'd like to pay."));
        if (Math.Abs(r.PickupLat) < 0.0001 || Math.Abs(r.DestLat) < 0.0001)
            return Result.Failure<HailDto>(Error.Validation("Choose your pickup and destination."));

        // One request at a time: replace any earlier one still waiting
        var earlier = await _db.HailRequests.Where(h => h.PassengerId == passengerId && h.Status == HailStatus.Open).ToListAsync(ct);
        foreach (var e in earlier) e.Cancel();

        var hail = HailRequest.Create(passengerId,
            r.PickupLat, r.PickupLng, string.IsNullOrWhiteSpace(r.PickupName) ? "Pickup" : r.PickupName!.Trim(),
            r.DestLat, r.DestLng, string.IsNullOrWhiteSpace(r.DestName) ? "Destination" : r.DestName!.Trim(),
            r.OfferedPrice, Lifetime);
        _db.HailRequests.Add(hail);
        await _db.SaveChangesAsync(ct);
        foreach (var e in earlier) await NotifyClosedAsync(e.Id, "cancelled", ct);

        var notified = await FindAndNotifyAsync(hail, ct);
        return Result.Success(await ToDtoAsync(hail, notified, ct));
    }

    public async Task<Result<HailDto>> RaiseAsync(Guid passengerId, Guid hailId, decimal price, CancellationToken ct)
    {
        var hail = await _db.HailRequests.FirstOrDefaultAsync(h => h.Id == hailId && h.PassengerId == passengerId, ct);
        if (hail == null) return Result.Failure<HailDto>(Error.NotFound("Ride request not found"));
        if (hail.Status is HailStatus.Matched or HailStatus.Cancelled)
            return Result.Failure<HailDto>(Error.Validation("This request has already finished."));
        if (price <= 0) return Result.Failure<HailDto>(Error.Validation("Enter the price you'd like to pay."));

        hail.Raise(price, Lifetime);
        await _db.SaveChangesAsync(ct);
        var notified = await FindAndNotifyAsync(hail, ct);
        return Result.Success(await ToDtoAsync(hail, notified, ct));
    }

    public async Task<Result> CancelAsync(Guid passengerId, Guid hailId, CancellationToken ct)
    {
        var hail = await _db.HailRequests.FirstOrDefaultAsync(h => h.Id == hailId && h.PassengerId == passengerId, ct);
        if (hail == null) return Result.Failure(Error.NotFound("Ride request not found"));
        if (hail.Status != HailStatus.Open && hail.Status != HailStatus.Expired) return Result.Success();
        hail.Cancel();
        await _db.SaveChangesAsync(ct);
        await NotifyClosedAsync(hail.Id, "cancelled", ct);
        return Result.Success();
    }

    /// <summary>The passenger's latest request that's still waiting (or just finished), to restore the screen.</summary>
    public async Task<HailDto?> GetMineAsync(Guid passengerId, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddMinutes(-10);
        var hail = await _db.HailRequests.AsNoTracking()
            .Where(h => h.PassengerId == passengerId && h.CreatedAt > since && h.Status != HailStatus.Cancelled)
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (hail == null) return null;
        var count = await _db.HailCandidates.CountAsync(c => c.HailId == hail.Id, ct);
        return await ToDtoAsync(hail, count, ct);
    }

    public Task<Result<ConversationDto>> AcceptCounterAsync(Guid passengerId, Guid hailId, Guid driverId, CancellationToken ct) =>
        MatchAsync(hailId, driverId, passengerId, useCounter: true, ct);

    // ── Driver ──

    public Task<Result<ConversationDto>> AcceptAsync(Guid driverId, Guid hailId, CancellationToken ct) =>
        MatchAsync(hailId, driverId, passengerId: null, useCounter: false, ct);

    public async Task<Result> CounterAsync(Guid driverId, Guid hailId, decimal price, CancellationToken ct)
    {
        if (price <= 0) return Result.Failure(Error.Validation("Enter your price."));
        var hail = await _db.HailRequests.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hailId, ct);
        if (hail == null || !hail.IsOpen) return Result.Failure(Error.Validation("This ride request is no longer available."));
        var candidate = await _db.HailCandidates.FirstOrDefaultAsync(c => c.HailId == hailId && c.DriverId == driverId, ct);
        if (candidate == null) return Result.Failure(Error.NotFound("Ride request not found"));

        candidate.Counter(price);
        await _db.SaveChangesAsync(ct);

        await _hub.Clients.Group($"user_{hail.PassengerId}").HailCounterOffer(ToCounterDto(candidate));
        await PushAsync(hail.PassengerId, $"{FirstName(candidate.DriverName)} offers ₦{price:N0}",
            $"{Minutes(candidate.EtaSeconds)} min away{(string.IsNullOrEmpty(candidate.Vehicle) ? "" : $" · {candidate.Vehicle}")}. Tap to accept.",
            hail.Id, "Hail", ct);
        return Result.Success();
    }

    public async Task<Result> DeclineAsync(Guid driverId, Guid hailId, CancellationToken ct)
    {
        var candidate = await _db.HailCandidates.FirstOrDefaultAsync(c => c.HailId == hailId && c.DriverId == driverId, ct);
        if (candidate == null) return Result.Success();
        candidate.Decline();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Requests waiting for this driver's answer (e.g. after opening the app from a push).</summary>
    public async Task<List<HailOfferDto>> GetIncomingAsync(Guid driverId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var rows = await (from c in _db.HailCandidates.AsNoTracking()
                          join h in _db.HailRequests.AsNoTracking() on c.HailId equals h.Id
                          where c.DriverId == driverId && c.Status != HailCandidateStatus.Declined &&
                                h.Status == HailStatus.Open && h.ExpiresAt > now
                          orderby h.CreatedAt descending
                          select new { h, c }).Take(5).ToListAsync(ct);
        var result = new List<HailOfferDto>();
        foreach (var x in rows)
            result.Add(ToOfferDto(x.h, x.c, await _users.GetDisplayNameAsync(x.h.PassengerId, ct)));
        return result;
    }

    // ── Expiry (background job) ──

    public async Task ExpireDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await _db.HailRequests.Where(h => h.Status == HailStatus.Open && h.ExpiresAt <= now).ToListAsync(ct);
        if (due.Count == 0) return;
        foreach (var h in due) h.Expire();
        await _db.SaveChangesAsync(ct);
        foreach (var h in due)
        {
            await _hub.Clients.Group($"user_{h.PassengerId}").HailExpired(h.Id);
            await NotifyClosedAsync(h.Id, "expired", ct);
            await PushAsync(h.PassengerId, "No driver accepted yet",
                "Try raising your offer to get a driver faster.", h.Id, "Hail", ct);
        }
    }

    // ── Matching ──

    private async Task<Result<ConversationDto>> MatchAsync(Guid hailId, Guid driverId, Guid? passengerId, bool useCounter, CancellationToken ct)
    {
        var hail = await _db.HailRequests.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hailId, ct);
        if (hail == null || (passengerId != null && hail.PassengerId != passengerId))
            return Result.Failure<ConversationDto>(Error.NotFound("Ride request not found"));
        var candidate = await _db.HailCandidates.AsNoTracking().FirstOrDefaultAsync(c => c.HailId == hailId && c.DriverId == driverId, ct);
        if (candidate == null) return Result.Failure<ConversationDto>(Error.NotFound("Ride request not found"));
        if (useCounter && candidate.CounterPrice is null)
            return Result.Failure<ConversationDto>(Error.Validation("That driver hasn't made an offer."));
        if (!hail.IsOpen) return Result.Failure<ConversationDto>(Error.Conflict(Taken(hail)));

        // The driver may have taken another ride since they were asked
        var busy = await _db.Conversations.Where(c => c.TravelerId == driverId &&
                (c.Status == ConversationStatus.LockedIn || c.Status == ConversationStatus.InProgress))
            .Select(c => c.Status).ToListAsync(ct);
        if (busy.Contains(ConversationStatus.LockedIn) || busy.Count > 1)
            return Result.Failure<ConversationDto>(Error.Validation(
                useCounter ? "That driver has just taken another ride." : "You already have a ride booked."));

        // First one wins: only flips an open request
        var now = DateTime.UtcNow;
        var won = await _db.HailRequests
            .Where(h => h.Id == hailId && h.Status == HailStatus.Open && h.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.Status, HailStatus.Matched)
                .SetProperty(h => h.MatchedDriverId, driverId)
                .SetProperty(h => h.UpdatedAt, now), ct);
        if (won == 0) return Result.Failure<ConversationDto>(Error.Conflict("Another driver took this ride."));

        var price = useCounter ? candidate.CounterPrice!.Value : hail.OfferedPrice;
        var conversation = Conversation.Create(
            hail.PassengerId, driverId,
            startAsPending: false,
            passengerDestLat: hail.DestLat, passengerDestLng: hail.DestLng, passengerDestAddress: hail.DestName,
            pickupLat: hail.PickupLat, pickupLng: hail.PickupLng, pickupAddress: hail.PickupName,
            meetLat: hail.PickupLat, meetLng: hail.PickupLng, meetName: hail.PickupName,
            dropLat: hail.DestLat, dropLng: hail.DestLng, dropName: hail.DestName,
            isOnDemand: true);
        conversation.BookOnDemand(price);
        _db.Conversations.Add(conversation);
        var msg = Message.CreateLockInConfirmed(conversation.Id, driverId, price);
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync(ct);

        await _db.HailRequests.Where(h => h.Id == hailId)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.ConversationId, conversation.Id), ct);

        var dto = ConversationDtoMapper.ToDto(conversation);
        await _hub.Clients.Group($"user_{hail.PassengerId}").ConversationUpdated(dto);
        await _hub.Clients.Group($"user_{driverId}").ConversationUpdated(dto);
        await _hub.Clients.Group($"user_{hail.PassengerId}").HailMatched(hailId, dto);
        await NotifyClosedAsync(hailId, "taken", ct, except: driverId);

        var eta = Minutes(candidate.EtaSeconds);
        await PushAsync(hail.PassengerId, $"{FirstName(candidate.DriverName)} is coming to pick you up",
            candidate.FinishingCurrentRide
                ? $"Finishing a ride nearby — about {eta} min. ₦{price:N0}."
                : $"About {eta} min away{(string.IsNullOrEmpty(candidate.Vehicle) ? "" : $" · {candidate.Vehicle} {candidate.VehiclePlate}")}. ₦{price:N0}.",
            conversation.Id, "Conversation", ct);

        _logger.LogInformation("Ride request {HailId} matched to driver {DriverId} at {Price} (conversation {ConversationId})",
            hailId, driverId, price, conversation.Id);
        return Result.Success(dto);
    }

    private static string Taken(HailRequest h) => h.Status switch
    {
        HailStatus.Matched => "Another driver took this ride.",
        HailStatus.Cancelled => "The passenger cancelled this request.",
        _ => "This ride request has expired.",
    };

    // ── Finding drivers ──

    private record NearDriver(Guid UserId, string Name, decimal AverageRating, int TotalRatings,
        double Latitude, double Longitude, bool HasOngoingTrip, string Vehicle, string VehiclePlate);
    private record BusyTrip(Guid DriverId, Guid TripId, bool IsPrivate, string Status);
    private record Found(NearDriver Driver, int EtaSeconds, bool Finishing);

    /// <summary>Find drivers within a short drive, record them and send them the request. Returns how many.</summary>
    private async Task<int> FindAndNotifyAsync(HailRequest hail, CancellationToken ct)
    {
        List<Found> found;
        try
        {
            found = await FindDriversAsync(hail, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't find drivers for ride request {HailId}", hail.Id);
            found = new();
        }

        var existing = await _db.HailCandidates.Where(c => c.HailId == hail.Id).ToDictionaryAsync(c => c.DriverId, ct);
        var candidates = new List<HailCandidate>();
        foreach (var f in found)
        {
            if (existing.TryGetValue(f.Driver.UserId, out var c)) c.Renotify(f.EtaSeconds, f.Finishing);
            else
            {
                c = HailCandidate.Create(hail.Id, f.Driver.UserId, f.Driver.Name, f.Driver.AverageRating, f.Driver.TotalRatings,
                    f.Driver.Vehicle, f.Driver.VehiclePlate, f.EtaSeconds, f.Finishing);
                _db.HailCandidates.Add(c);
            }
            candidates.Add(c);
        }
        await _db.SaveChangesAsync(ct);

        var passengerName = await _users.GetDisplayNameAsync(hail.PassengerId, ct);
        foreach (var c in candidates)
        {
            await _hub.Clients.Group($"user_{c.DriverId}").HailRequested(ToOfferDto(hail, c, passengerName));
            await PushAsync(c.DriverId, $"New ride request · ₦{hail.OfferedPrice:N0}",
                (c.FinishingCurrentRide ? "After your drop-off · " : "") +
                $"{Minutes(c.EtaSeconds)} min to pickup at {hail.PickupName} → {hail.DestName}",
                hail.Id, "Hail", ct);
        }
        _logger.LogInformation("Ride request {HailId}: {Count} drivers notified", hail.Id, candidates.Count);
        return candidates.Count;
    }

    private async Task<List<Found>> FindDriversAsync(HailRequest hail, CancellationToken ct)
    {
        // Drivers with someone on board whose live position (not their saved
        // one) is around here — they may be about to finish
        var box = PrefilterRadiusKm / 111.0;
        var seen = DateTime.UtcNow.AddMinutes(-5);
        var onBoardHere = await _db.Conversations.AsNoTracking()
            .Where(c => c.Status == ConversationStatus.InProgress && c.HelperLastSeenAt > seen &&
                        c.HelperLastLat > hail.PickupLat - box && c.HelperLastLat < hail.PickupLat + box &&
                        c.HelperLastLng > hail.PickupLng - box && c.HelperLastLng < hail.PickupLng + box)
            .Select(c => c.TravelerId).Distinct().ToListAsync(ct);

        var near = (await DriversNearAsync(hail.PickupLat, hail.PickupLng, onBoardHere, ct))
            .Where(d => d.UserId != hail.PassengerId).ToList();
        if (near.Count == 0) return new();
        var ids = near.Select(d => d.UserId).ToList();

        // Their current rides: booked-but-not-started rules a driver out; one
        // passenger on board may be fine if that ride is private and nearly done
        var rides = await _db.Conversations.AsNoTracking()
            .Where(c => ids.Contains(c.TravelerId) &&
                        (c.Status == ConversationStatus.LockedIn || c.Status == ConversationStatus.InProgress))
            .ToListAsync(ct);
        var busyTrips = await BusyTripsAsync(ids, ct);

        var idle = new List<NearDriver>();
        var finishing = new List<(NearDriver Driver, Conversation Ride)>();
        foreach (var d in near)
        {
            var mine = rides.Where(r => r.TravelerId == d.UserId).ToList();
            var trips = busyTrips.Where(t => t.DriverId == d.UserId).ToList();
            if (mine.Count == 0)
            {
                if (trips.Count == 0) idle.Add(d);
                continue;
            }
            if (mine.Count > 1 || mine[0].Status != ConversationStatus.InProgress) continue;
            var ride = mine[0];
            var isPrivate = ride.IsOnDemand ||
                            (ride.TripId is { } tid && trips.Any(t => t.TripId == tid && t.IsPrivate));
            // Only that private ride may be under way — no other offered trip
            if (!isPrivate || trips.Any(t => t.TripId != ride.TripId)) continue;
            if (ride.HelperLastLat is null || ride.HelperLastLng is null || DropOf(ride) is null) continue;
            finishing.Add((d, ride));
        }

        var result = new List<Found>();

        // Idle: straight from where they are
        var idleEtas = await EtasAsync(idle.Select(d => (d.Latitude, d.Longitude)).ToList(), hail.PickupLat, hail.PickupLng, ct);
        for (var i = 0; i < idle.Count; i++)
        {
            var eta = idleEtas[i] ?? EstimateSeconds(idle[i].Latitude, idle[i].Longitude, hail.PickupLat, hail.PickupLng);
            if (eta <= MaxEtaSeconds) result.Add(new Found(idle[i], eta, false));
        }

        // Finishing a private ride: to their drop-off first (must be close), then here
        if (finishing.Count > 0)
        {
            var fromDrop = await EtasAsync(finishing.Select(f => DropOf(f.Ride)!.Value).ToList(), hail.PickupLat, hail.PickupLng, ct);
            for (var i = 0; i < finishing.Count; i++)
            {
                var (d, ride) = finishing[i];
                var drop = DropOf(ride)!.Value;
                var toDrop = (await EtasAsync(new List<(double, double)> { (ride.HelperLastLat!.Value, ride.HelperLastLng!.Value) }, drop.Lat, drop.Lng, ct))[0]
                             ?? EstimateSeconds(ride.HelperLastLat!.Value, ride.HelperLastLng!.Value, drop.Lat, drop.Lng);
                if (toDrop > FinishingSoonSeconds) continue;
                var total = toDrop + (fromDrop[i] ?? EstimateSeconds(drop.Lat, drop.Lng, hail.PickupLat, hail.PickupLng));
                if (total <= MaxEtaSeconds) result.Add(new Found(d, total, true));
            }
        }

        return result.OrderBy(f => f.EtaSeconds).Take(MaxDrivers).ToList();
    }

    private static (double Lat, double Lng)? DropOf(Conversation c) =>
        c.DropLat is { } a && c.DropLng is { } b ? (a, b)
        : c.PassengerDestLat is { } x && c.PassengerDestLng is { } y ? (x, y)
        : null;

    private async Task<List<NearDriver>> DriversNearAsync(double lat, double lng, List<Guid> alsoIds, CancellationToken ct)
    {
        try
        {
            var inv = CultureInfo.InvariantCulture;
            var list = await _http.CreateClient("user-api").GetFromJsonAsync<List<NearDriver>>(
                $"internal/availability/drivers-near?lat={lat.ToString(inv)}&lng={lng.ToString(inv)}&radiusKm={PrefilterRadiusKm.ToString(inv)}" +
                (alsoIds.Count > 0 ? $"&alsoIds={string.Join(',', alsoIds)}" : ""),
                Json, ct);
            return list ?? new();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't load nearby drivers");
            return new();
        }
    }

    private async Task<List<BusyTrip>> BusyTripsAsync(List<Guid> driverIds, CancellationToken ct)
    {
        try
        {
            using var res = await _http.CreateClient("trip-api").PostAsJsonAsync("internal/trips/busy-drivers", new { driverIds }, Json, ct);
            if (!res.IsSuccessStatusCode) return new();
            return await res.Content.ReadFromJsonAsync<List<BusyTrip>>(Json, ct) ?? new();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't check drivers' trips");
            return new();
        }
    }

    private record EtaResult(List<int?> Seconds);

    /// <summary>Drive time from each origin to one place; null where unknown.</summary>
    private async Task<List<int?>> EtasAsync(List<(double Lat, double Lng)> origins, double lat, double lng, CancellationToken ct)
    {
        var none = origins.Select(_ => (int?)null).ToList();
        if (origins.Count == 0) return none;
        try
        {
            var body = new { origins = origins.Select(o => new { lat = o.Lat, lng = o.Lng }), destLat = lat, destLng = lng };
            using var res = await _http.CreateClient("trip-api").PostAsJsonAsync("internal/trips/eta", body, Json, ct);
            if (!res.IsSuccessStatusCode) return none;
            var r = await res.Content.ReadFromJsonAsync<EtaResult>(Json, ct);
            return r?.Seconds is { } s && s.Count == origins.Count ? s : none;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't get drive times");
            return none;
        }
    }

    /// <summary>Rough city drive time when routing is unavailable: 1.4× the straight line at 25 km/h.</summary>
    private static int EstimateSeconds(double aLat, double aLng, double bLat, double bLng) =>
        (int)(HaversineKm(aLat, aLng, bLat, bLng) * 1.4 / 25 * 3600);

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // ── Helpers ──

    private async Task NotifyClosedAsync(Guid hailId, string reason, CancellationToken ct, Guid? except = null)
    {
        var drivers = await _db.HailCandidates.AsNoTracking().Where(c => c.HailId == hailId).Select(c => c.DriverId).ToListAsync(ct);
        foreach (var d in drivers.Where(d => d != except))
            await _hub.Clients.Group($"user_{d}").HailClosed(hailId, reason);
    }

    private async Task PushAsync(Guid userId, string title, string body, Guid referenceId, string referenceType, CancellationToken ct)
    {
        try
        {
            await _eventBus.PublishAsync(new RidePushIntegrationEvent(userId, title, body, referenceId, referenceType), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't send ride push to {UserId}", userId);
        }
    }

    private async Task<HailDto> ToDtoAsync(HailRequest h, int notified, CancellationToken ct)
    {
        var counters = await _db.HailCandidates.AsNoTracking()
            .Where(c => c.HailId == h.Id && c.Status == HailCandidateStatus.Countered)
            .OrderBy(c => c.CounterPrice)
            .ToListAsync(ct);
        return new HailDto(h.Id, h.Status.ToString(), h.PickupLat, h.PickupLng, h.PickupName, h.DestLat, h.DestLng, h.DestName,
            h.OfferedPrice, h.ExpiresAt, notified, h.ConversationId, counters.Select(ToCounterDto).ToList());
    }

    private static HailCounterDto ToCounterDto(HailCandidate c) => new(
        c.HailId, c.DriverId, c.DriverName, c.DriverRating, c.DriverTotalRatings, c.Vehicle, c.VehiclePlate,
        c.EtaSeconds, c.FinishingCurrentRide, c.CounterPrice ?? 0);

    private static HailOfferDto ToOfferDto(HailRequest h, HailCandidate c, string? passengerName) => new(
        h.Id, h.PassengerId, passengerName,
        h.PickupLat, h.PickupLng, h.PickupName, h.DestLat, h.DestLng, h.DestName,
        (int)Math.Round(HaversineKm(h.PickupLat, h.PickupLng, h.DestLat, h.DestLng) * 1000 * 1.3),
        h.OfferedPrice, c.EtaSeconds, c.FinishingCurrentRide, h.ExpiresAt, c.CounterPrice);

    private static int Minutes(int seconds) => Math.Max(1, (int)Math.Round(seconds / 60.0));

    private static string FirstName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Your driver" : name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
}
