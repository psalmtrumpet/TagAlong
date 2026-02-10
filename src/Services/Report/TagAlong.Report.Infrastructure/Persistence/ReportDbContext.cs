using Microsoft.EntityFrameworkCore;
using TagAlong.Report.Domain.Entities;

namespace TagAlong.Report.Infrastructure.Persistence;

public class ReportDbContext : DbContext
{
    public ReportDbContext(DbContextOptions<ReportDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.Entities.Report> Reports => Set<Domain.Entities.Report>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Entities.Report>(entity =>
        {
            entity.ToTable("reports");

            entity.HasKey(r => r.Id);

            entity.Property(r => r.Id).HasColumnName("id");
            entity.Property(r => r.ReporterId).HasColumnName("reporter_id").IsRequired();
            entity.Property(r => r.ReportedUserId).HasColumnName("reported_user_id");
            entity.Property(r => r.ReportedDeliveryId).HasColumnName("reported_delivery_id");
            entity.Property(r => r.ReportType).HasColumnName("report_type").HasConversion<string>().IsRequired();
            entity.Property(r => r.Reason).HasColumnName("reason").HasConversion<string>().IsRequired();
            entity.Property(r => r.Description).HasColumnName("description").HasMaxLength(2000).IsRequired();
            entity.Property(r => r.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            entity.Property(r => r.AdminNotes).HasColumnName("admin_notes").HasMaxLength(2000);
            entity.Property(r => r.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(r => r.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(r => r.Resolution).HasColumnName("resolution").HasMaxLength(2000);
            entity.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(r => r.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(r => r.ReporterId);
            entity.HasIndex(r => r.ReportedUserId);
            entity.HasIndex(r => r.ReportedDeliveryId);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.ReportType);
        });
    }
}
