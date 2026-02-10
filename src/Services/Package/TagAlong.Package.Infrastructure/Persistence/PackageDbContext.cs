using Microsoft.EntityFrameworkCore;
using TagAlong.Package.Domain.Entities;

namespace TagAlong.Package.Infrastructure.Persistence;

public class PackageDbContext : DbContext
{
    public PackageDbContext(DbContextOptions<PackageDbContext> options) : base(options)
    {
    }

    public DbSet<PackageRequest> PackageRequests => Set<PackageRequest>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PackageRequest>(entity =>
        {
            entity.ToTable("package_requests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PickupLocation)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.DeliveryLocation)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.PackageDescription)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.SpecialInstructions)
                .HasMaxLength(500);

            entity.Property(e => e.PackageImageUrl)
                .HasMaxLength(1024);

            entity.Property(e => e.Size)
                .HasConversion<string>();

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.EstimatedWeight)
                .HasPrecision(10, 2);

            entity.Property(e => e.OfferedPrice)
                .HasPrecision(10, 2);

            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.PickupLatitude, e.PickupLongitude });
            entity.HasIndex(e => new { e.DeliveryLatitude, e.DeliveryLongitude });

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.ToTable("deliveries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MeetupLocation)
                .HasMaxLength(256);

            entity.Property(e => e.DeliveryProofImageUrl)
                .HasMaxLength(1024);

            entity.Property(e => e.ReceiverName)
                .HasMaxLength(100);

            entity.Property(e => e.ReceiverPhone)
                .HasMaxLength(20);

            entity.Property(e => e.Notes)
                .HasMaxLength(1000);

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.AgreedPrice)
                .HasPrecision(10, 2);

            entity.Property(e => e.PlatformFee)
                .HasPrecision(10, 2);

            entity.Property(e => e.TravelerPayout)
                .HasPrecision(10, 2);

            entity.HasIndex(e => e.PackageRequestId);
            entity.HasIndex(e => e.TripId);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.TravelerId);
            entity.HasIndex(e => e.Status);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });
    }
}
