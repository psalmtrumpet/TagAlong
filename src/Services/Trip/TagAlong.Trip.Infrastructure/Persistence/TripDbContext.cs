using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using TagAlong.Trip.Domain.Entities;

namespace TagAlong.Trip.Infrastructure.Persistence;

public class TripDbContext : DbContext
{
    public TripDbContext(DbContextOptions<TripDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.Entities.Trip> Trips => Set<Domain.Entities.Trip>();
    public DbSet<TripStop> TripStops => Set<TripStop>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Entities.Trip>(entity =>
        {
            entity.ToTable("trips");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Origin)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Destination)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.VehicleType)
                .HasMaxLength(50)
                .IsRequired(false);

            entity.Property(e => e.VehiclePlateNumber)
                .HasMaxLength(100)
                .IsRequired(false);

            entity.Property(e => e.Notes)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(e => e.Status)
                .HasConversion<string>();

            entity.Property(e => e.TripType)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(e => e.AvailableCapacity)
                .HasPrecision(10, 2);

            entity.HasIndex(e => e.TravelerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DepartureTime);
            entity.HasIndex(e => new { e.OriginLatitude, e.OriginLongitude });
            entity.HasIndex(e => new { e.DestinationLatitude, e.DestinationLongitude });

            entity.Property(e => e.OriginalDurationSeconds)
                .IsRequired(false);

            entity.Property(e => e.RouteLine)
                .HasColumnType("geography")
                .IsRequired(false);

            entity.Property(e => e.RouteStatus)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(TripRouteStatus.None);

            entity.HasMany(e => e.Stops)
                .WithOne()
                .HasForeignKey(e => e.TripId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<TripStop>(entity =>
        {
            entity.ToTable("trip_stops");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Location)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(e => e.TripId);
        });
    }
}
