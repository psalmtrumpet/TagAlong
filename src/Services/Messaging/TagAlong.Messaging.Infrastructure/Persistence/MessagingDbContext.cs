using Microsoft.EntityFrameworkCore;
using TagAlong.Messaging.Domain.Entities;

namespace TagAlong.Messaging.Infrastructure.Persistence;

public class MessagingDbContext : DbContext
{
    public MessagingDbContext(DbContextOptions<MessagingDbContext> options) : base(options)
    {
    }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<HailRequest> HailRequests => Set<HailRequest>();
    public DbSet<HailCandidate> HailCandidates => Set<HailCandidate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("conversations");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.PackageRequestId).HasColumnName("package_request_id");
            entity.Property(c => c.TripId).HasColumnName("trip_id");
            entity.Property(c => c.IsDelivery).HasColumnName("is_delivery").HasDefaultValue(false);
            entity.Property(c => c.PickupLat).HasColumnName("pickup_lat");
            entity.Property(c => c.PickupLng).HasColumnName("pickup_lng");
            entity.Property(c => c.PickupAddress).HasColumnName("pickup_address").HasMaxLength(500);
            entity.Property(c => c.MeetLat).HasColumnName("meet_lat");
            entity.Property(c => c.MeetLng).HasColumnName("meet_lng");
            entity.Property(c => c.MeetName).HasColumnName("meet_name").HasMaxLength(200);
            entity.Property(c => c.DropLat).HasColumnName("drop_lat");
            entity.Property(c => c.DropLng).HasColumnName("drop_lng");
            entity.Property(c => c.DropName).HasColumnName("drop_name").HasMaxLength(200);
            entity.Property(c => c.SenderId).HasColumnName("sender_id").IsRequired();
            entity.Property(c => c.TravelerId).HasColumnName("traveler_id").IsRequired();
            entity.Property(c => c.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            entity.Property(c => c.RecipientUserId).HasColumnName("recipient_user_id");
            entity.Property(c => c.RecipientName).HasColumnName("recipient_name").HasMaxLength(200);
            entity.Property(c => c.AgreedPrice).HasColumnName("agreed_price").HasColumnType("decimal(18,2)");
            entity.Property(c => c.PlatformFee).HasColumnName("platform_fee").HasColumnType("decimal(18,2)");
            entity.Property(c => c.DriverEarning).HasColumnName("driver_earning").HasColumnType("decimal(18,2)");
            entity.Property(c => c.FeeWaived).HasColumnName("fee_waived").HasDefaultValue(false);
            entity.Property(c => c.IsOnDemand).HasColumnName("is_on_demand").HasDefaultValue(false);
            entity.Property(c => c.LockInProposedBy).HasColumnName("lock_in_proposed_by");
            entity.Property(c => c.StartedAt).HasColumnName("started_at");
            entity.Property(c => c.DeliveredAt).HasColumnName("delivered_at");
            entity.Property(c => c.PassengerDestLat).HasColumnName("passenger_dest_lat");
            entity.Property(c => c.PassengerDestLng).HasColumnName("passenger_dest_lng");
            entity.Property(c => c.PassengerDestAddress).HasColumnName("passenger_dest_address").HasMaxLength(500);
            entity.Property(c => c.HelperLastLat).HasColumnName("helper_last_lat");
            entity.Property(c => c.HelperLastLng).HasColumnName("helper_last_lng");
            entity.Property(c => c.HelperLastSeenAt).HasColumnName("helper_last_seen_at");
            entity.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(c => c.PackageRequestId);
            entity.HasIndex(c => c.SenderId);
            entity.HasIndex(c => c.TravelerId);
            entity.HasIndex(c => new { c.SenderId, c.TravelerId });

            entity.HasMany(c => c.Messages)
                  .WithOne(m => m.Conversation)
                  .HasForeignKey(m => m.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");

            entity.HasKey(m => m.Id);

            entity.Property(m => m.Id).HasColumnName("id");
            entity.Property(m => m.ConversationId).HasColumnName("conversation_id").IsRequired();
            entity.Property(m => m.SenderId).HasColumnName("sender_id").IsRequired();
            entity.Property(m => m.Content).HasColumnName("content").HasMaxLength(2000).IsRequired();
            entity.Property(m => m.MessageType).HasColumnName("message_type").HasConversion<string>().IsRequired();
            entity.Property(m => m.ProposedPrice).HasColumnName("proposed_price").HasColumnType("decimal(18,2)");
            entity.Property(m => m.SentAt).HasColumnName("sent_at").IsRequired();
            entity.Property(m => m.ReadAt).HasColumnName("read_at");
            entity.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(m => m.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(m => m.ConversationId);
            entity.HasIndex(m => m.SenderId);
            entity.HasIndex(m => m.SentAt);
        });

        modelBuilder.Entity<HailRequest>(entity =>
        {
            entity.ToTable("hail_requests");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).HasColumnName("id");
            entity.Property(h => h.PassengerId).HasColumnName("passenger_id").IsRequired();
            entity.Property(h => h.PickupLat).HasColumnName("pickup_lat");
            entity.Property(h => h.PickupLng).HasColumnName("pickup_lng");
            entity.Property(h => h.PickupName).HasColumnName("pickup_name").HasMaxLength(300);
            entity.Property(h => h.DestLat).HasColumnName("dest_lat");
            entity.Property(h => h.DestLng).HasColumnName("dest_lng");
            entity.Property(h => h.DestName).HasColumnName("dest_name").HasMaxLength(300);
            entity.Property(h => h.OfferedPrice).HasColumnName("offered_price").HasColumnType("decimal(18,2)");
            entity.Property(h => h.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(h => h.MatchedDriverId).HasColumnName("matched_driver_id");
            entity.Property(h => h.ConversationId).HasColumnName("conversation_id");
            entity.Property(h => h.ExpiresAt).HasColumnName("expires_at");
            entity.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(h => h.UpdatedAt).HasColumnName("updated_at");
            entity.Ignore(h => h.IsDeleted);
            entity.Ignore(h => h.DeletedAt);
            entity.Ignore(h => h.IsOpen);
            entity.HasIndex(h => new { h.Status, h.ExpiresAt });
            entity.HasIndex(h => h.PassengerId);
        });

        modelBuilder.Entity<HailCandidate>(entity =>
        {
            entity.ToTable("hail_candidates");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.HailId).HasColumnName("hail_id").IsRequired();
            entity.Property(c => c.DriverId).HasColumnName("driver_id").IsRequired();
            entity.Property(c => c.DriverName).HasColumnName("driver_name").HasMaxLength(200);
            entity.Property(c => c.DriverRating).HasColumnName("driver_rating").HasColumnType("decimal(4,2)");
            entity.Property(c => c.DriverTotalRatings).HasColumnName("driver_total_ratings");
            entity.Property(c => c.Vehicle).HasColumnName("vehicle").HasMaxLength(200);
            entity.Property(c => c.VehiclePlate).HasColumnName("vehicle_plate").HasMaxLength(30);
            entity.Property(c => c.EtaSeconds).HasColumnName("eta_seconds");
            entity.Property(c => c.FinishingCurrentRide).HasColumnName("finishing_current_ride");
            entity.Property(c => c.CounterPrice).HasColumnName("counter_price").HasColumnType("decimal(18,2)");
            entity.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(c => c.UpdatedAt).HasColumnName("updated_at");
            entity.Ignore(c => c.IsDeleted);
            entity.Ignore(c => c.DeletedAt);
            entity.HasIndex(c => c.HailId);
            entity.HasIndex(c => c.DriverId);
        });
    }
}
