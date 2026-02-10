using Microsoft.EntityFrameworkCore;
using TagAlong.Configuration.Domain.Entities;

namespace TagAlong.Configuration.Infrastructure.Persistence;

public class ConfigurationDbContext : DbContext
{
    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) : base(options)
    {
    }

    public DbSet<PlatformConfiguration> PlatformConfigurations => Set<PlatformConfiguration>();
    public DbSet<FeeConfiguration> FeeConfigurations => Set<FeeConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlatformConfiguration>(entity =>
        {
            entity.ToTable("platform_configurations");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.Key).HasColumnName("key").HasMaxLength(200).IsRequired();
            entity.Property(c => c.Value).HasColumnName("value").HasMaxLength(2000).IsRequired();
            entity.Property(c => c.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
            entity.Property(c => c.Type).HasColumnName("type").HasConversion<string>().IsRequired();
            entity.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(c => c.IsDeleted).HasColumnName("is_deleted").IsRequired();
            entity.Property(c => c.DeletedAt).HasColumnName("deleted_at");
            entity.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(c => c.Key).IsUnique();
            entity.HasIndex(c => c.Type);
            entity.HasIndex(c => c.IsActive);
        });

        modelBuilder.Entity<FeeConfiguration>(entity =>
        {
            entity.ToTable("fee_configurations");

            entity.HasKey(f => f.Id);

            entity.Property(f => f.Id).HasColumnName("id");
            entity.Property(f => f.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(f => f.MinPercentage).HasColumnName("min_percentage").HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(f => f.MaxPercentage).HasColumnName("max_percentage").HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(f => f.DefaultPercentage).HasColumnName("default_percentage").HasColumnType("decimal(5,2)").IsRequired();
            entity.Property(f => f.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
            entity.Property(f => f.IsActive).HasColumnName("is_active").IsRequired();
            entity.Property(f => f.IsDeleted).HasColumnName("is_deleted").IsRequired();
            entity.Property(f => f.DeletedAt).HasColumnName("deleted_at");
            entity.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(f => f.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(f => f.Name).IsUnique();
            entity.HasIndex(f => f.IsActive);
        });

        // Seed default fee configuration
        modelBuilder.Entity<FeeConfiguration>().HasData(
            new
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Platform Fee",
                MinPercentage = 5.0m,
                MaxPercentage = 20.0m,
                DefaultPercentage = 10.0m,
                Description = "Platform fee percentage range for delivery services",
                IsActive = true,
                IsDeleted = false,
                DeletedAt = (DateTime?)null,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = (DateTime?)null
            }
        );
    }
}
