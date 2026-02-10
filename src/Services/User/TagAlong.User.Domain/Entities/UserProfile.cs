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
    public void SetAvailable(double latitude, double longitude, string? locationName, TimeSpan? duration = null)
    {
        if (!AllowLocationSharing)
            throw new InvalidOperationException("Location sharing is disabled");

        if (!IsVerified)
            throw new InvalidOperationException("Only verified users can set availability");

        IsAvailable = true;
        CurrentLatitude = latitude;
        CurrentLongitude = longitude;
        CurrentLocationName = locationName;
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
        LocationUpdatedAt = null;
        AvailabilityStartedAt = null;
        AvailabilityExpiresAt = null;
        SetUpdated();
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

        const double R = 6371; // Earth's radius in km
        var dLat = ToRadians(latitude - CurrentLatitude.Value);
        var dLon = ToRadians(longitude - CurrentLongitude.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(CurrentLatitude.Value)) * Math.Cos(ToRadians(latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
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
