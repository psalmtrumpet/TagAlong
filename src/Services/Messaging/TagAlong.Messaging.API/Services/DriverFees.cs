using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TagAlong.Common.Results;
using TagAlong.EventBus;
using TagAlong.Messaging.API.Commands;
using TagAlong.Messaging.API.Hubs;
using TagAlong.Messaging.API.IntegrationEvents;
using TagAlong.Messaging.Domain.Entities;
using TagAlong.Messaging.Infrastructure.Persistence;

namespace TagAlong.Messaging.API.Services;

public record FeePaymentDto(string Reference, decimal Amount, int Rides, string Status, DateTime CreatedAt, DateTime? PaidAt);

/// <summary>Where the driver stands with TagAlong's fees.</summary>
public record DriverFeeStatusDto(
    bool Enabled,
    int RidesPerPayment,
    int UnpaidRides,
    decimal AmountDue,
    bool Blocked,
    decimal FeePerRide,
    List<FeePaymentDto> Payments);

public record StartFeePaymentDto(string Reference, string AuthorizationUrl, decimal Amount, int Rides);

/// <summary>
/// TagAlong's fee is collected from drivers every few rides (Fees:RidesPerPayment,
/// default 5) through Paystack. Once that many rides are unpaid the driver stops
/// getting ride requests — they're told someone nearby wants a ride, and paying
/// lets them take it. Paying settles the rides and the count starts again.
/// Nothing is enforced until a Paystack key is set (Paystack:SecretKey).
/// </summary>
public class DriverFees
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly MessagingDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly IEventBus _eventBus;
    private readonly IHubContext<MessagingHub, IMessagingClient> _hub;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DriverFees> _logger;

    public DriverFees(MessagingDbContext db, IHttpClientFactory http, IConfiguration config, IEventBus eventBus,
        IHubContext<MessagingHub, IMessagingClient> hub, IMemoryCache cache, ILogger<DriverFees> logger)
    {
        _db = db;
        _http = http;
        _config = config;
        _eventBus = eventBus;
        _hub = hub;
        _cache = cache;
        _logger = logger;
    }

    // ── Settings ──
    private string? SecretKey => _config["Paystack:SecretKey"] is { Length: > 0 } k ? k : null;
    public bool Enabled => SecretKey != null;
    public int RidesPerPayment =>
        int.TryParse(_config["Fees:RidesPerPayment"], out var n) && n > 0 ? n : 5;
    private string CallbackUrl => _config["Paystack:CallbackUrl"] is { Length: > 0 } u
        ? u : "https://www.tlimc.net/api/public/paystack/return";

    private IQueryable<Conversation> Unpaid(Guid driverId) => _db.Conversations
        .Where(c => c.TravelerId == driverId && c.DeliveredAt != null && c.FeeSettledAt == null && c.PlatformFee > 0);

    // ── Status ──

    public async Task<DriverFeeStatusDto> StatusAsync(Guid driverId, CancellationToken ct)
    {
        var unpaid = await Unpaid(driverId).AsNoTracking().Select(c => c.PlatformFee!.Value).ToListAsync(ct);
        var payments = await _db.DriverFeePayments.AsNoTracking()
            .Where(p => p.DriverId == driverId && p.Status == DriverFeePaymentStatus.Paid)
            .OrderByDescending(p => p.PaidAt).Take(20)
            .Select(p => new FeePaymentDto(p.Reference, p.Amount, p.Rides, p.Status.ToString(), p.CreatedAt, p.PaidAt))
            .ToListAsync(ct);
        return new DriverFeeStatusDto(Enabled, RidesPerPayment, unpaid.Count, unpaid.Sum(),
            Enabled && unpaid.Count >= RidesPerPayment, MarkDeliveredCommandHandler.RideFlatFee(_config), payments);
    }

    /// <summary>True when this driver must pay before getting another ride request.</summary>
    public async Task<bool> IsBlockedAsync(Guid driverId, CancellationToken ct) =>
        Enabled && await Unpaid(driverId).CountAsync(ct) >= RidesPerPayment;

    /// <summary>Which of these drivers must pay before getting ride requests.</summary>
    public async Task<HashSet<Guid>> BlockedAmongAsync(IReadOnlyCollection<Guid> driverIds, CancellationToken ct)
    {
        if (!Enabled || driverIds.Count == 0) return new();
        var limit = RidesPerPayment;
        var ids = driverIds.ToList();
        var blocked = await _db.Conversations.AsNoTracking()
            .Where(c => ids.Contains(c.TravelerId) && c.DeliveredAt != null && c.FeeSettledAt == null && c.PlatformFee > 0)
            .GroupBy(c => c.TravelerId)
            .Where(g => g.Count() >= limit)
            .Select(g => g.Key)
            .ToListAsync(ct);
        return blocked.ToHashSet();
    }

    public string BlockedMessage(decimal amount) =>
        $"Pay your {Naira(amount)} TagAlong fee to get ride requests again.";

    // ── Notifications ──

    /// <summary>A ride was just completed: tell the driver when their fee is due.</summary>
    public async Task AfterRideCompletedAsync(Guid driverId, CancellationToken ct)
    {
        if (!Enabled) return;
        try
        {
            var unpaid = await Unpaid(driverId).AsNoTracking().Select(c => c.PlatformFee!.Value).ToListAsync(ct);
            if (unpaid.Count < RidesPerPayment) return;
            // Once when the limit is reached (several passengers can be dropped at once)
            if (!_cache.TryGetValue($"fee-due-told:{driverId}:{unpaid.Count / RidesPerPayment}", out _))
            {
                _cache.Set($"fee-due-told:{driverId}:{unpaid.Count / RidesPerPayment}", true, TimeSpan.FromHours(6));
                await PushAsync(driverId, "Time to pay your TagAlong fee",
                    $"You've completed {unpaid.Count} rides. Pay {Naira(unpaid.Sum())} to keep getting ride requests.", ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't check fees for driver {DriverId}", driverId);
        }
    }

    /// <summary>
    /// Someone wants a ride from a driver who hasn't paid: they hear about it
    /// (at most every few minutes) but don't get the request itself.
    /// </summary>
    /// <param name="joiningTrip">They asked to join the driver's offered trip (each request is told),
    /// rather than a "Ride now" request nearby (at most every few minutes).</param>
    public async Task TeaseAsync(Guid driverId, string? passengerName, CancellationToken ct, bool joiningTrip = false)
    {
        var key = $"fee-teased:{driverId}";
        if (!joiningTrip)
        {
            if (_cache.TryGetValue(key, out _)) return;
            _cache.Set(key, true, TimeSpan.FromMinutes(5));
        }
        try
        {
            var due = await Unpaid(driverId).AsNoTracking().SumAsync(c => c.PlatformFee!.Value, ct);
            var who = string.IsNullOrWhiteSpace(passengerName) ? "Someone" : passengerName.Trim().Split(' ')[0];
            if (joiningTrip)
                await PushAsync(driverId, $"{who} wants to join your trip",
                    $"{who} is trying to join your ride. Pay your {Naira(due)} TagAlong fee to see the request and pick them up.", ct);
            else
                await PushAsync(driverId, $"{who} needs a ride near you",
                    $"{who} is trying to request a ride in your area. Pay your {Naira(due)} TagAlong fee to go pick them up.", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't tell driver {DriverId} about a ride they can't take yet", driverId);
        }
    }

    private Task PushAsync(Guid driverId, string title, string body, CancellationToken ct) =>
        _eventBus.PublishAsync(new RidePushIntegrationEvent(driverId, title, body, driverId, "DriverFee"), ct);

    // ── Paying ──

    /// <summary>Start paying everything owed: returns the Paystack checkout page to open.</summary>
    public async Task<Result<StartFeePaymentDto>> StartAsync(Guid driverId, string? email, CancellationToken ct)
    {
        if (SecretKey is not { } key)
            return Result.Failure<StartFeePaymentDto>(Error.Validation("Payments aren't set up yet. Please try again later."));
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<StartFeePaymentDto>(Error.Validation("Add an email address to your account to pay."));

        var rides = await Unpaid(driverId).AsNoTracking().OrderBy(c => c.DeliveredAt)
            .Select(c => new { c.Id, Fee = c.PlatformFee!.Value }).ToListAsync(ct);
        if (rides.Count == 0)
            return Result.Failure<StartFeePaymentDto>(Error.Validation("You don't owe anything right now."));
        var amount = rides.Sum(r => r.Fee);
        var rideIds = string.Join(',', rides.Select(r => r.Id));

        // Tapped Pay again: the checkout already started for the same rides still works
        var since = DateTime.UtcNow.AddMinutes(-30);
        var open = await _db.DriverFeePayments.AsNoTracking()
            .Where(p => p.DriverId == driverId && p.Status == DriverFeePaymentStatus.Pending && p.CreatedAt > since && p.AuthorizationUrl != null)
            .OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(ct);
        if (open != null && open.RideIds == rideIds)
            return Result.Success(new StartFeePaymentDto(open.Reference, open.AuthorizationUrl!, open.Amount, open.Rides));

        var payment = DriverFeePayment.Start(driverId, amount, rides.Select(r => r.Id).ToList());
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "transaction/initialize")
            {
                Content = JsonContent.Create(new
                {
                    email = email.Trim(),
                    amount = (long)Math.Round(amount * 100), // kobo
                    currency = "NGN",
                    reference = payment.Reference,
                    callback_url = CallbackUrl,
                    metadata = new
                    {
                        driver_id = driverId,
                        rides = rides.Count,
                        cancel_action = CallbackUrl,
                    },
                }),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var res = await _http.CreateClient("paystack").SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (!res.IsSuccessStatusCode || !doc.RootElement.GetProperty("status").GetBoolean())
            {
                _logger.LogWarning("Paystack refused to start fee payment for {DriverId}: {Status} {Body}", driverId, (int)res.StatusCode, body);
                return Result.Failure<StartFeePaymentDto>(Error.Validation("Couldn't start the payment. Please try again."));
            }
            payment.SetAuthorizationUrl(doc.RootElement.GetProperty("data").GetProperty("authorization_url").GetString()!);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't reach Paystack for driver {DriverId}", driverId);
            return Result.Failure<StartFeePaymentDto>(Error.Validation("Couldn't reach the payment service. Check your connection and try again."));
        }

        _db.DriverFeePayments.Add(payment);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Fee payment {Reference} started by driver {DriverId}: {Amount} for {Rides} rides",
            payment.Reference, driverId, amount, rides.Count);
        return Result.Success(new StartFeePaymentDto(payment.Reference, payment.AuthorizationUrl!, amount, rides.Count));
    }

    /// <summary>The driver's latest unfinished payment, to check after they come back from Paystack.</summary>
    public Task<string?> LatestPendingReferenceAsync(Guid driverId, CancellationToken ct) =>
        _db.DriverFeePayments.AsNoTracking()
            .Where(p => p.DriverId == driverId && p.Status == DriverFeePaymentStatus.Pending)
            .OrderByDescending(p => p.CreatedAt).Select(p => p.Reference).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Ask Paystack whether this payment went through; if it did, settle its rides.
    /// Safe to call many times (the app, the return page and the webhook all do).
    /// Returns the driver when this call is the one that settled it.
    /// </summary>
    public async Task<(DriverFeePaymentStatus Status, Guid? NewlyPaidDriver)> ConfirmAsync(string reference, CancellationToken ct)
    {
        var payment = await _db.DriverFeePayments.AsNoTracking().FirstOrDefaultAsync(p => p.Reference == reference, ct);
        if (payment == null) return (DriverFeePaymentStatus.Failed, null);
        if (payment.Status != DriverFeePaymentStatus.Pending || SecretKey is not { } key) return (payment.Status, null);

        string status;
        long paidKobo;
        string? currency;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"transaction/verify/{Uri.EscapeDataString(reference)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var res = await _http.CreateClient("paystack").SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return (DriverFeePaymentStatus.Pending, null);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var data = doc.RootElement.GetProperty("data");
            status = data.GetProperty("status").GetString() ?? "";
            paidKobo = data.GetProperty("amount").GetInt64();
            currency = data.TryGetProperty("currency", out var cur) ? cur.GetString() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't check fee payment {Reference} with Paystack", reference);
            return (DriverFeePaymentStatus.Pending, null);
        }

        if (status == "failed")
        {
            await _db.DriverFeePayments.Where(p => p.Id == payment.Id && p.Status == DriverFeePaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, DriverFeePaymentStatus.Failed)
                    .SetProperty(p => p.UpdatedAt, DateTime.UtcNow), ct);
            return (DriverFeePaymentStatus.Failed, null);
        }
        if (status != "success") return (DriverFeePaymentStatus.Pending, null);
        if (paidKobo < (long)Math.Round(payment.Amount * 100) || (currency != null && currency != "NGN"))
        {
            _logger.LogWarning("Fee payment {Reference} paid {Kobo} {Currency}, expected {Amount} NGN — not settled",
                reference, paidKobo, currency, payment.Amount);
            return (DriverFeePaymentStatus.Pending, null);
        }

        // Only one caller settles it
        var now = DateTime.UtcNow;
        var won = await _db.DriverFeePayments
            .Where(p => p.Id == payment.Id && p.Status != DriverFeePaymentStatus.Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, DriverFeePaymentStatus.Paid)
                .SetProperty(p => p.PaidAt, now)
                .SetProperty(p => p.UpdatedAt, now), ct);
        if (won == 0) return (DriverFeePaymentStatus.Paid, null);

        var ids = payment.RideIdList.ToList();
        var rides = await _db.Conversations.Where(c => ids.Contains(c.Id) && c.TravelerId == payment.DriverId).ToListAsync(ct);
        foreach (var r in rides) r.SettleFee(payment.Id);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Fee payment {Reference} received from driver {DriverId}: {Amount}, {Rides} rides settled",
            reference, payment.DriverId, payment.Amount, rides.Count);
        return (DriverFeePaymentStatus.Paid, payment.DriverId);
    }

    /// <summary>
    /// Paid: thank them, and the shared-ride requests that waited while they
    /// owed show up now.
    /// </summary>
    public async Task AfterPaidAsync(Guid driverId, CancellationToken ct)
    {
        try
        {
            var waiting = await _db.Conversations.AsNoTracking()
                .Where(c => c.TravelerId == driverId && c.Status == ConversationStatus.Pending)
                .ToListAsync(ct);
            foreach (var c in waiting)
                await _hub.Clients.Group($"user_{driverId}").ConversationUpdated(ConversationDtoMapper.ToDto(c));
            await PushAsync(driverId, "Payment received — thank you!",
                waiting.Count switch
                {
                    0 => "You'll get ride requests again.",
                    1 => "You'll get ride requests again. 1 ride request is waiting for you.",
                    var n => $"You'll get ride requests again. {n} ride requests are waiting for you.",
                }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't finish up after driver {DriverId} paid", driverId);
        }
    }

    /// <summary>Paystack's signature on a webhook: HMAC-SHA512 of the body with our secret key.</summary>
    public bool IsFromPaystack(byte[] body, string? signature)
    {
        if (SecretKey is not { } key || string.IsNullOrEmpty(signature)) return false;
        var hash = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(key), body));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(hash.ToLowerInvariant()), Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public static string Naira(decimal v) => "₦" + v.ToString("N0", CultureInfo.InvariantCulture);
}
