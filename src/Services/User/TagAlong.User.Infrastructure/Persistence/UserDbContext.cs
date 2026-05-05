using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;

namespace TagAlong.User.Infrastructure.Persistence;

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
    {
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(e => e.Email)
                .IsUnique();

            entity.HasIndex(e => e.AuthUserId)
                .IsUnique();

            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20);

            entity.Property(e => e.Bio)
                .HasMaxLength(500);

            entity.Property(e => e.ProfileImageUrl)
                .HasMaxLength(1024);

            entity.Property(e => e.IdentityDocumentUrl)
                .HasMaxLength(1024);

            entity.Property(e => e.AverageRating)
                .HasPrecision(3, 2);

            entity.Property(e => e.VerificationStatus)
                .HasConversion<string>();

            // Availability Status
            entity.Property(e => e.IsAvailable)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.AvailabilityStartedAt);
            entity.Property(e => e.AvailabilityExpiresAt);

            // Current Location
            entity.Property(e => e.CurrentLatitude);
            entity.Property(e => e.CurrentLongitude);

            entity.Property(e => e.CurrentLocationName)
                .HasMaxLength(256);

            entity.Property(e => e.LocationUpdatedAt);

            // Trip Destination
            entity.Property(e => e.TripDestinationLatitude);
            entity.Property(e => e.TripDestinationLongitude);

            entity.Property(e => e.TripDestinationName)
                .HasMaxLength(256);

            entity.Property(e => e.ActivePassengerCount)
                .IsRequired()
                .HasDefaultValue(0);

            // Location Preferences
            entity.Property(e => e.MaxTravelRadiusKm)
                .IsRequired()
                .HasDefaultValue(10.0);

            entity.Property(e => e.AllowLocationSharing)
                .IsRequired()
                .HasDefaultValue(true);

            // Index for finding available users by location
            entity.HasIndex(e => new { e.IsAvailable, e.CurrentLatitude, e.CurrentLongitude })
                .HasFilter("[IsAvailable] = 1 AND [IsDeleted] = 0");

            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }
}
