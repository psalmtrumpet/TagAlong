using Microsoft.EntityFrameworkCore;
using TagAlong.Identity.Domain.Entities;

namespace TagAlong.Identity.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.HasIndex(e => e.Email)
                .IsUnique();

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(512);

            entity.Property(e => e.GoogleId)
                .HasMaxLength(256);

            entity.HasIndex(e => e.GoogleId)
                .IsUnique()
                .HasFilter("\"GoogleId\" IS NOT NULL");

            entity.Property(e => e.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.LastName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20);

            entity.Property(e => e.RefreshToken)
                .HasMaxLength(512);

            entity.Property(e => e.ProfileImageUrl)
                .HasMaxLength(1024);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<VerificationCode>(entity =>
        {
            entity.ToTable("verification_codes");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.Type)
                .IsRequired()
                .HasConversion<string>();

            entity.HasIndex(e => new { e.UserId, e.Code, e.Type });
        });
    }
}
