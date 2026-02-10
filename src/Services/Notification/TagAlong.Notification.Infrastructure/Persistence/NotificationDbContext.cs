using Microsoft.EntityFrameworkCore;
using TagAlong.Notification.Domain.Entities;

namespace TagAlong.Notification.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.Entities.Notification> Notifications => Set<Domain.Entities.Notification>();
    public DbSet<UserConnection> UserConnections => Set<UserConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Entities.Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.Type)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(e => e.ReferenceType)
                .HasMaxLength(100);

            entity.Property(e => e.Data)
                .HasMaxLength(4000);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasIndex(e => e.CreatedAt);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<UserConnection>(entity =>
        {
            entity.ToTable("user_connections");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ConnectionId)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.DeviceType)
                .HasMaxLength(50);

            entity.HasIndex(e => e.ConnectionId)
                .IsUnique();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.IsActive });
        });
    }
}
