using TagAlong.Common.Domain;

namespace TagAlong.User.Domain.Entities;

public class UserProfile : AggregateRoot
{
    public Guid AuthUserId { get; private set; }
    public string Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string? Bio { get; private set; }
    public string? ProfileImageUrl { get; private set; }
    public decimal AverageRating { get; private set; }
    public int TotalRatings { get; private set; }
    public int CompletedDeliveries { get; private set; }
    public int CompletedTrips { get; private set; }
    public bool IsVerified { get; private set; }
    public UserVerificationStatus VerificationStatus { get; private set; } = UserVerificationStatus.Pending;
    public string? IdentityDocumentUrl { get; private set; }
    public DateTime? VerifiedAt { get; private set; }

    // Availability Status
    public bool IsAvailable { get; private set; }
    public DateTime? AvailabilityStartedAt { get; private set; }
    public DateTime? AvailabilityExpiresAt { get; private set; }

    // Current Location (only valid when IsAvailable = true)
    public double? CurrentLatitude { get; private set; }
    public double? CurrentLongitude { get; private set; }
    public string? CurrentLocationName { get; private set; }
    public DateTime? LocationUpdatedAt { get; private set; }

    // Trip Destination (only valid when IsAvailable = true)
    public double? TripDestinationLatitude { get; private set; }
    public double? TripDestinationLongitude { get; private set; }
    public string? TripDestinationName { get; private set; }

    // Passenger count for current availability session (max 3)
    public int ActivePassengerCount { get; private set; }

    // Location Preferences
    public double MaxTravelRadiusKm { get; private set; } = 10.0;
    public bool AllowLocationSharing { get; private set; } = true;

    private UserProfile() { }

    public static UserProfile Create(
        Guid authUserId,
        string email,
        string firstName,
        string lastName,
        string phoneNumber)
    {
        return new UserProfile
        {
            AuthUserId = authUserId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            AverageRating = 0,
            TotalRatings = 0,
            CompletedDeliveries = 0,
            CompletedTrips = 0
        };
    }

    public void UpdateProfile(string firstName, string lastName, string? bio, string? profileImageUrl)
    {
        FirstName = firstName;
        LastName = lastName;
        Bio = bio;
        if (!string.IsNullOrEmpty(profileImageUrl))
        {
            ProfileImageUrl = profileImageUrl;
        }
        SetUpdated();
    }

    public void UpdatePhoneNumber(string phoneNumber)
    {
        PhoneNumber = phoneNumber;
        SetUpdated();
    }

    public void UpdateRating(decimal newRating)
    {
        var totalScore = AverageRating * TotalRatings + newRating;
        TotalRatings++;
        AverageRating = totalScore / TotalRatings;
        SetUpdated();
    }

    public void IncrementCompletedDeliveries()
    {
        CompletedDeliveries++;
        SetUpdated();
    }

    public void IncrementCompletedTrips()
    {
        CompletedTrips++;
        SetUpdated();
    }

    public void SubmitVerification(string identityDocumentUrl)
    {
        IdentityDocumentUrl = identityDocumentUrl;
        VerificationStatus = UserVerificationStatus.Pending;
        SetUpdated();
    }

    public void Verify()
    {
        IsVerified = true;
        VerificationStatus = UserVerificationStatus.Verified;
        VerifiedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void RejectVerification()
    {
        VerificationStatus = UserVerificationStatus.Rejected;
        SetUpdated();
    }

    // Availability Methods
    public void SetAvailable(
        double latitude,
        double longitude,
        string? locationName,
        double? tripDestinationLatitude = null,
        double? tripDestinationLongitude = null,
        string? tripDestinationName = null,
        TimeSpan? duration = null)
    {
        if (!AllowLocationSharing)
            throw new InvalidOperationException("Location sharing is disabled");

        if (!IsVerified)
            throw new InvalidOperationException("Only verified users can set availability");

        IsAvailable = true;
        CurrentLatitude = latitude;
        CurrentLongitude = longitude;
        CurrentLocationName = locationName;
        TripDestinationLatitude = tripDestinationLatitude;
        TripDestinationLongitude = tripDestinationLongitude;
        TripDestinationName = tripDestinationName;
        ActivePassengerCount = 0;
        LocationUpdatedAt = DateTime.UtcNow;
        AvailabilityStartedAt = DateTime.UtcNow;
        AvailabilityExpiresAt = duration.HasValue
            ? DateTime.UtcNow.Add(duration.Value)
            : DateTime.UtcNow.AddHours(4); // Default 4 hours
        SetUpdated();
    }

    public void SetUnavailable()
    {
        IsAvailable = false;
        CurrentLatitude = null;
        CurrentLongitude = null;
        CurrentLocationName = null;
        TripDestinationLatitude = null;
        TripDestinationLongitude = null;
        TripDestinationName = null;
        ActivePassengerCount = 0;
        LocationUpdatedAt = null;
        AvailabilityStartedAt = null;
        AvailabilityExpiresAt = null;
        SetUpdated();
    }

    /// <summary>
    /// Increment passenger count. Returns false if already at max (3).
    /// </summary>
    public bool IncrementPassengerCount()
    {
        if (ActivePassengerCount >= 3) return false;
        ActivePassengerCount++;
        SetUpdated();
        return true;
    }

    public void DecrementPassengerCount()
    {
        if (ActivePassengerCount > 0)
        {
            ActivePassengerCount--;
            SetUpdated();
        }
    }

    public void UpdateLocation(double latitude, double longitude, string? locationName)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("Cannot update location when not available");

        CurrentLatitude = latitude;
        CurrentLongitude = longitude;
        CurrentLocationName = locationName;
        LocationUpdatedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void UpdateLocationPreferences(double maxRadiusKm, bool allowSharing)
    {
        MaxTravelRadiusKm = maxRadiusKm;
        AllowLocationSharing = allowSharing;

        // If disabling sharing, also turn off availability
        if (!allowSharing && IsAvailable)
        {
            SetUnavailable();
        }

        SetUpdated();
    }

    public bool IsAvailabilityExpired()
    {
        return AvailabilityExpiresAt.HasValue && DateTime.UtcNow > AvailabilityExpiresAt.Value;
    }

    // Haversine distance calculation (follows Trip.cs pattern)
    public double DistanceFromKm(double latitude, double longitude)
    {
        if (!CurrentLatitude.HasValue || !CurrentLongitude.HasValue)
            return double.MaxValue;

        return HaversineKm(CurrentLatitude.Value, CurrentLongitude.Value, latitude, longitude);
    }

    /// <summary>
    /// Checks whether a sender's pickup (F) lies along this helper's route (current → destination).
    /// Uses bearing comparison: if F is within 60° of the helper's direction of travel and
    /// is not farther than the full route distance, the routes are considered aligned.
    /// </summary>
    public bool IsRouteAlignedWith(double pickupLat, double pickupLng, double dropoffLat, double dropoffLng)
    {
        if (!CurrentLatitude.HasValue || !CurrentLongitude.HasValue ||
            !TripDestinationLatitude.HasValue || !TripDestinationLongitude.HasValue)
            return false;

        double fromLat = CurrentLatitude.Value;
        double fromLng = CurrentLongitude.Value;
        double toLat = TripDestinationLatitude.Value;
        double toLng = TripDestinationLongitude.Value;

        // Helper must have a meaningful route (at least 0.5 km)
        double routeDist = HaversineKm(fromLat, fromLng, toLat, toLng);
        if (routeDist < 0.5) return false;

        // Bearing from helper start → helper destination
        double helperBearing = CalculateBearing(fromLat, fromLng, toLat, toLng);

        // Bearing from helper start → sender's pickup
        double bearingToPickup = CalculateBearing(fromLat, fromLng, pickupLat, pickupLng);

        // Angular difference — must be within 60° of helper's direction
        double angleDiff = Math.Abs(helperBearing - bearingToPickup);
        if (angleDiff > 180) angleDiff = 360 - angleDiff;
        if (angleDiff > 60) return false;

        // Pickup must not be farther than the helper's route distance (with 10% buffer)
        double distToPickup = HaversineKm(fromLat, fromLng, pickupLat, pickupLng);
        return distToPickup <= routeDist * 1.1;
    }

    private static double CalculateBearing(double lat1, double lng1, double lat2, double lng2)
    {
        var dLng = ToRadians(lng2 - lng1);
        var lat1R = ToRadians(lat1);
        var lat2R = ToRadians(lat2);
        var y = Math.Sin(dLng) * Math.Cos(lat2R);
        var x = Math.Cos(lat1R) * Math.Sin(lat2R) - Math.Sin(lat1R) * Math.Cos(lat2R) * Math.Cos(dLng);
        var bearing = Math.Atan2(y, x) * 180 / Math.PI;
        return (bearing + 360) % 360;
    }

    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371;
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

public enum UserVerificationStatus
{
    None,
    Pending,
    Verified,
    Rejected
}
