using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TagAlong.User.API.DTOs;
using TagAlong.User.Domain.Repositories;

namespace TagAlong.User.API.Hubs;

public interface ILocationClient
{
    Task ReceiveLocationUpdate(LocationUpdateDto update);
    Task UserBecameAvailable(AvailableUserDto user);
    Task UserBecameUnavailable(Guid userId);
    Task NearbyUsersUpdated(IEnumerable<AvailableUserDto> users);
    Task AvailabilityStatusChanged(AvailabilityStatusDto status);

    // Route-match notifications
    Task HelperGoingYourWay(RouteMatchNotification helper);
    Task SenderAlongYourRoute(RouteMatchNotification sender);
}

public record LocationUpdateDto(
    Guid UserId,
    double Latitude,
    double Longitude,
    string? LocationName,
    DateTime UpdatedAt);

public record AvailableUserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? ProfileImageUrl,
    decimal AverageRating,
    int CompletedDeliveries,
    bool IsVerified,
    double DistanceKm,
    string? LocationName,
    string? TripDestinationName = null,
    int ActivePassengerCount = 0);

public record AvailabilityStatusDto(
    bool IsAvailable,
    DateTime? ExpiresAt);

/// <summary>
/// In-memory record of a sender's active trip route subscription.
/// Cleared when the connection disconnects.
/// </summary>
public record ActiveTripSubscription(
    Guid SenderAuthUserId,
    double PickupLat,
    double PickupLng,
    double DropoffLat,
    double DropoffLng);

[Authorize]
public class LocationHub : Hub<ILocationClient>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<LocationHub> _logger;

    // connectionId → active trip route (for route-match push when helper goes available)
    private static readonly ConcurrentDictionary<string, ActiveTripSubscription> _activeTripSubs = new();

    public LocationHub(
        IUserProfileRepository userProfileRepository,
        ILogger<LocationHub> logger)
    {
        _userProfileRepository = userProfileRepository;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            // Join user's personal group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} connected to LocationHub with connection {ConnectionId}", userId, Context.ConnectionId);

            // Get user's current availability status
            var profile = await _userProfileRepository.GetByAuthUserIdAsync(userId.Value);
            if (profile != null)
            {
                await Clients.Caller.AvailabilityStatusChanged(new AvailabilityStatusDto(
                    profile.IsAvailable,
                    profile.AvailabilityExpiresAt));
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("User {UserId} disconnected from LocationHub", userId);
        }

        // Clean up any active trip route subscription for this connection
        _activeTripSubs.TryRemove(Context.ConnectionId, out _);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Update user's current location (called periodically by mobile app)
    /// </summary>
    public async Task UpdateLocation(double latitude, double longitude, string? locationName)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            var profile = await _userProfileRepository.GetByAuthUserIdAsync(userId.Value);
            if (profile == null || !profile.IsAvailable) return;

            // Check if moved significantly (> 50 meters) to reduce unnecessary updates
            if (profile.CurrentLatitude.HasValue && profile.CurrentLongitude.HasValue)
            {
                var distance = profile.DistanceFromKm(latitude, longitude) * 1000; // Convert to meters
                if (distance < 50) return; // Skip if moved less than 50m
            }

            profile.UpdateLocation(latitude, longitude, locationName);
            _userProfileRepository.Update(profile);
            await _userProfileRepository.SaveChangesAsync();

            // Broadcast to subscribers in the same grid cell
            var gridCell = GetGridCellId(latitude, longitude);
            await Clients.Group(gridCell).ReceiveLocationUpdate(new LocationUpdateDto(
                profile.Id,
                latitude,
                longitude,
                locationName,
                DateTime.UtcNow));

            _logger.LogDebug("User {UserId} location updated to ({Lat}, {Lon})", userId, latitude, longitude);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Set availability status with optional trip destination for route matching.
    /// </summary>
    public async Task SetAvailable(
        double latitude,
        double longitude,
        string? locationName,
        int? durationMinutes,
        double? tripDestLat = null,
        double? tripDestLng = null,
        string? tripDestName = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            var profile = await _userProfileRepository.GetByAuthUserIdAsync(userId.Value);
            if (profile == null) return;

            var duration = durationMinutes.HasValue
                ? TimeSpan.FromMinutes(durationMinutes.Value)
                : (TimeSpan?)null;

            profile.SetAvailable(latitude, longitude, locationName, tripDestLat, tripDestLng, tripDestName, duration);
            _userProfileRepository.Update(profile);
            await _userProfileRepository.SaveChangesAsync();

            // Join grid cell group
            var gridCell = GetGridCellId(latitude, longitude);
            await Groups.AddToGroupAsync(Context.ConnectionId, gridCell);

            // Notify caller
            await Clients.Caller.AvailabilityStatusChanged(new AvailabilityStatusDto(true, profile.AvailabilityExpiresAt));

            // Notify others in the area
            await Clients.Group(gridCell).UserBecameAvailable(new AvailableUserDto(
                profile.Id,
                profile.FirstName,
                profile.LastName,
                profile.ProfileImageUrl,
                profile.AverageRating,
                profile.CompletedDeliveries,
                profile.IsVerified,
                0,
                locationName,
                tripDestName,
                profile.ActivePassengerCount));

            // Route-match: notify any senders with pending trips aligned with this helper's route
            if (tripDestLat.HasValue && tripDestLng.HasValue)
            {
                await NotifyMatchingSendersOfHelper(profile, userId.Value);
            }

            _logger.LogInformation("User {UserId} became available at ({Lat}, {Lon}), dest: {Dest}", userId, latitude, longitude, tripDestName);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("User {UserId} cannot set availability: {Message}", userId, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting availability for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Set unavailable
    /// </summary>
    public async Task SetUnavailable()
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        try
        {
            var profile = await _userProfileRepository.GetByAuthUserIdAsync(userId.Value);
            if (profile == null) return;

            // Get old grid cell before clearing location
            string? oldGridCell = null;
            if (profile.CurrentLatitude.HasValue && profile.CurrentLongitude.HasValue)
            {
                oldGridCell = GetGridCellId(profile.CurrentLatitude.Value, profile.CurrentLongitude.Value);
            }

            profile.SetUnavailable();
            _userProfileRepository.Update(profile);
            await _userProfileRepository.SaveChangesAsync();

            // Leave grid cell group
            if (oldGridCell != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldGridCell);
                await Clients.Group(oldGridCell).UserBecameUnavailable(profile.Id);
            }

            await Clients.Caller.AvailabilityStatusChanged(new AvailabilityStatusDto(false, null));

            _logger.LogInformation("User {UserId} became unavailable", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting unavailable for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Subscribe to nearby user updates for a specific area
    /// </summary>
    public async Task SubscribeToNearbyUsers(double latitude, double longitude, double radiusKm)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        // Join grid cells that cover the radius
        var cells = GetAdjacentCellIds(latitude, longitude);
        foreach (var cell in cells)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, cell);
        }

        // Send initial list of nearby users
        var nearbyUsers = await _userProfileRepository.SearchAvailableUsersAsync(
            latitude, longitude, radiusKm, 1, 50);

        var userDtos = nearbyUsers.Select(u => new AvailableUserDto(
            u.Id,
            u.FirstName,
            u.LastName,
            u.ProfileImageUrl,
            u.AverageRating,
            u.CompletedDeliveries,
            u.IsVerified,
            Math.Round(u.DistanceFromKm(latitude, longitude), 2),
            u.CurrentLocationName,
            u.TripDestinationName,
            u.ActivePassengerCount));

        await Clients.Caller.NearbyUsersUpdated(userDtos);
    }

    /// <summary>
    /// Unsubscribe from nearby user updates
    /// </summary>
    public async Task UnsubscribeFromNearbyUsers(double latitude, double longitude)
    {
        var cells = GetAdjacentCellIds(latitude, longitude);
        foreach (var cell in cells)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, cell);
        }
    }

    /// <summary>
    /// Called by a sender when they create a carry-along trip.
    /// Registers the route in memory so helpers going that direction notify them.
    /// Also immediately checks for any already-available helpers along the route
    /// and sends a SenderAlongYourRoute event to each matching helper.
    /// </summary>
    public async Task RegisterSenderTrip(
        double pickupLat, double pickupLng,
        double dropoffLat, double dropoffLng)
    {
        var userId = GetUserId();
        if (!userId.HasValue) return;

        // Store in-memory subscription for this connection
        _activeTripSubs[Context.ConnectionId] = new ActiveTripSubscription(
            userId.Value, pickupLat, pickupLng, dropoffLat, dropoffLng);

        // Find any currently available helpers whose route aligns
        var matchingHelpers = await _userProfileRepository.FindHelpersAlongRouteAsync(
            pickupLat, pickupLng, dropoffLat, dropoffLng);

        foreach (var helper in matchingHelpers)
        {
            if (helper.AuthUserId == userId.Value) continue; // skip self

            // Notify sender that this helper is going their way
            await Clients.Caller.HelperGoingYourWay(new RouteMatchNotification(
                helper.Id,
                helper.AuthUserId,
                helper.FirstName,
                helper.LastName,
                helper.ProfileImageUrl,
                helper.CurrentLocationName,
                helper.TripDestinationName,
                helper.AverageRating,
                helper.ActivePassengerCount,
                3 - helper.ActivePassengerCount));

            // Notify the helper that a sender along their route just posted a trip
            await Clients.Group($"user_{helper.AuthUserId}").SenderAlongYourRoute(new RouteMatchNotification(
                Guid.Empty, // sender profile ID not needed for helper side
                userId.Value,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                0,
                0,
                0));
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// When a helper goes available, notify all senders with in-memory trip subscriptions
    /// whose route aligns with this helper's destination.
    /// </summary>
    private async Task NotifyMatchingSendersOfHelper(
        TagAlong.User.Domain.Entities.UserProfile helperProfile,
        Guid helperAuthUserId)
    {
        foreach (var kvp in _activeTripSubs)
        {
            var sub = kvp.Value;
            if (sub.SenderAuthUserId == helperAuthUserId) continue; // skip self

            if (helperProfile.IsRouteAlignedWith(sub.PickupLat, sub.PickupLng, sub.DropoffLat, sub.DropoffLng))
            {
                // Notify the sender that a helper is going their way
                await Clients.Group($"user_{sub.SenderAuthUserId}").HelperGoingYourWay(new RouteMatchNotification(
                    helperProfile.Id,
                    helperAuthUserId,
                    helperProfile.FirstName,
                    helperProfile.LastName,
                    helperProfile.ProfileImageUrl,
                    helperProfile.CurrentLocationName,
                    helperProfile.TripDestinationName,
                    helperProfile.AverageRating,
                    helperProfile.ActivePassengerCount,
                    3 - helperProfile.ActivePassengerCount));
            }
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Get grid cell ID for geospatial grouping (1km x 1km cells)
    /// </summary>
    private static string GetGridCellId(double lat, double lon, double cellSizeKm = 1.0)
    {
        var latCell = (int)(lat / (cellSizeKm / 111.0));
        var lonCell = (int)(lon / (cellSizeKm / (111.0 * Math.Cos(lat * Math.PI / 180))));
        return $"cell_{latCell}_{lonCell}";
    }

    /// <summary>
    /// Get current cell and 8 adjacent cells for coverage
    /// </summary>
    private static IEnumerable<string> GetAdjacentCellIds(double lat, double lon, double cellSizeKm = 1.0)
    {
        var latStep = cellSizeKm / 111.0;
        var lonStep = cellSizeKm / (111.0 * Math.Cos(lat * Math.PI / 180));

        for (int dLat = -1; dLat <= 1; dLat++)
        {
            for (int dLon = -1; dLon <= 1; dLon++)
            {
                yield return GetGridCellId(lat + dLat * latStep, lon + dLon * lonStep, cellSizeKm);
            }
        }
    }
}
