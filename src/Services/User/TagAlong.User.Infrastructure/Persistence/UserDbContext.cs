using Microsoft.EntityFrameworkCore;
using TagAlong.User.Domain.Entities;

namespace TagAlong.User.Infrastructure.Persistence;

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
    {
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<KycVerification> KycVerifications => Set<KycVerification>();
    public DbSet<NinCache> NinCaches => Set<NinCache>();
    public DbSet<SmileWebhookLog> SmileWebhookLogs => Set<SmileWebhookLog>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<ReferralAccount> ReferralAccounts => Set<ReferralAccount>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<ReferralEarning> ReferralEarnings => Set<ReferralEarning>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<SafetyRecording> SafetyRecordings => Set<SafetyRecording>();

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

            entity.Property(e => e.HasOngoingTrip)
                .IsRequired()
                .HasDefaultValue(false);

            // Location Preferences
            entity.Property(e => e.MaxTravelRadiusKm)
                .IsRequired()
                .HasDefaultValue(10.0);

            entity.Property(e => e.AllowLocationSharing)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.IsSuspended)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.SuspendedAt);

            entity.Property(e => e.SuspensionReason)
                .HasMaxLength(500);

            // Index for finding available users by location
            entity.HasIndex(e => new { e.IsAvailable, e.CurrentLatitude, e.CurrentLongitude })
                .HasFilter("[IsAvailable] = 1 AND [IsDeleted] = 0");

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<NinCache>(entity =>
        {
            entity.ToTable("nin_cache");
            entity.HasKey(e => e.NIN);
            entity.Property(e => e.NIN).HasMaxLength(64).IsRequired(); // keyed hash of the NIN
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.MiddleName).HasMaxLength(100);
            entity.Property(e => e.DateOfBirth).HasMaxLength(20);
            entity.Property(e => e.Gender).HasMaxLength(10);
        });

        modelBuilder.Entity<SmileWebhookLog>(entity =>
        {
            entity.ToTable("smile_webhook_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobId).HasMaxLength(100);
            entity.Property(e => e.ResultCode).HasMaxLength(20);
            entity.Property(e => e.Outcome).HasMaxLength(100).IsRequired();
            entity.Property(e => e.BodySnippet).HasMaxLength(1000);
            entity.HasIndex(e => e.JobId);
            entity.HasIndex(e => e.ReceivedAt);
        });

        modelBuilder.Entity<KycVerification>(entity =>
        {
            entity.ToTable("kyc_verifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NIN).HasMaxLength(11);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.MiddleName).HasMaxLength(100);
            entity.Property(e => e.DateOfBirth).HasMaxLength(20);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.Nationality).HasMaxLength(100);
            entity.Property(e => e.ResidenceState).HasMaxLength(100);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.DeviceFlag).HasMaxLength(200);
            entity.Property(e => e.NinHash).HasMaxLength(64);
            entity.HasIndex(e => e.NinHash);
            entity.Property(e => e.PhotoPath).HasMaxLength(512);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.SmileUserId).HasMaxLength(100);
            entity.Property(e => e.JobStartedAt);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.AuthUserId);
        });

        modelBuilder.Entity<DriverProfile>(entity =>
        {
            entity.ToTable("driver_profiles");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AuthUserId).IsUnique();
            entity.Property(e => e.LicenseNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LicenseImagePath).HasMaxLength(512).IsRequired();
            entity.Property(e => e.VehicleType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.VehicleMake).HasMaxLength(50).IsRequired();
            entity.Property(e => e.VehicleModel).HasMaxLength(50).IsRequired();
            entity.Property(e => e.VehicleColor).HasMaxLength(30).IsRequired();
            entity.Property(e => e.VehiclePlate).HasMaxLength(20).IsRequired();
            entity.Property(e => e.VehicleImagePath).HasMaxLength(512);
            entity.Property(e => e.VehicleBackImagePath).HasMaxLength(512);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.DocumentCheckJson).HasColumnType("nvarchar(max)");
            entity.Ignore(e => e.IsApproved);
        });

        modelBuilder.Entity<ReferralAccount>(entity =>
        {
            entity.ToTable("referral_accounts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AuthUserId).IsUnique();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.WalletBalance).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Referral>(entity =>
        {
            entity.ToTable("referrals");
            entity.HasKey(e => e.Id);
            // Each person can only be referred once
            entity.HasIndex(e => e.ReferredUserId).IsUnique();
            entity.HasIndex(e => e.ReferrerUserId);
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Reward).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.RewardAmount).HasColumnType("decimal(18,2)");
            entity.Ignore(e => e.IsQualified);
        });

        modelBuilder.Entity<EmergencyContact>(entity =>
        {
            entity.ToTable("emergency_contacts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AuthUserId);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<SafetyRecording>(entity =>
        {
            entity.ToTable("safety_recordings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AuthUserId);
            entity.HasIndex(e => e.ConversationId);
            entity.Property(e => e.FilePath).HasMaxLength(200);
        });

        modelBuilder.Entity<ReferralEarning>(entity =>
        {
            entity.ToTable("referral_earnings");
            entity.HasKey(e => e.Id);
            // A ride rewards each referred participant's referrer once
            entity.HasIndex(e => new { e.ConversationId, e.ReferredUserId }).IsUnique();
            entity.HasIndex(e => e.ReferrerUserId);
            entity.Property(e => e.Reward).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.ToTable("wallet_transactions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AuthUserId);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Description).HasMaxLength(200).IsRequired();
        });
    }
}
