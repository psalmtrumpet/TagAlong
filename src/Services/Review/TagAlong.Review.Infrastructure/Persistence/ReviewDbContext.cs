using Microsoft.EntityFrameworkCore;
using TagAlong.Review.Domain.Entities;

namespace TagAlong.Review.Infrastructure.Persistence;

public class ReviewDbContext : DbContext
{
    public ReviewDbContext(DbContextOptions<ReviewDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.Entities.Review> Reviews => Set<Domain.Entities.Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Entities.Review>(entity =>
        {
            entity.ToTable("reviews");

            entity.HasKey(r => r.Id);

            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.DeliveryId).HasColumnName("delivery_id").IsRequired();
            entity.Property(r => r.ReviewerId).HasColumnName("reviewer_id").IsRequired();
            entity.Property(r => r.RevieweeId).HasColumnName("reviewee_id").IsRequired();
            entity.Property(r => r.Type).HasColumnName("type").HasConversion<string>().IsRequired();
            entity.Property(r => r.Rating).HasColumnName("rating").IsRequired();
            entity.Property(r => r.Comment).HasColumnName("comment").HasMaxLength(1000);
            entity.Property(r => r.ReviewerRole).HasColumnName("reviewer_role").HasConversion<string>().IsRequired();
            entity.Property(r => r.IsEdited).HasColumnName("is_edited").HasDefaultValue(false);
            entity.Property(r => r.EditedAt).HasColumnName("edited_at");
            entity.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(r => r.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(r => r.DeliveryId);
            entity.HasIndex(r => r.ReviewerId);
            entity.HasIndex(r => r.RevieweeId);
            entity.HasIndex(r => new { r.DeliveryId, r.ReviewerId }).IsUnique();
        });
    }
}
