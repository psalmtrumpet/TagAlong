using Microsoft.EntityFrameworkCore;
using TagAlong.Payment.Domain.Entities;

namespace TagAlong.Payment.Infrastructure.Persistence;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<Domain.Entities.Payment> Payments => Set<Domain.Entities.Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Entities.Payment>(entity =>
        {
            entity.ToTable("payments");

            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.DeliveryId).HasColumnName("delivery_id").IsRequired();
            entity.Property(p => p.SenderId).HasColumnName("sender_id").IsRequired();
            entity.Property(p => p.TravelerId).HasColumnName("traveler_id").IsRequired();
            entity.Property(p => p.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(p => p.PlatformFee).HasColumnName("platform_fee").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(p => p.TravelerPayout).HasColumnName("traveler_payout").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(p => p.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            entity.Property(p => p.PaymentMethod).HasColumnName("payment_method").HasMaxLength(50);
            entity.Property(p => p.TransactionReference).HasColumnName("transaction_reference").HasMaxLength(255);
            entity.Property(p => p.PaymentProvider).HasColumnName("payment_provider").HasMaxLength(50);
            entity.Property(p => p.PaidAt).HasColumnName("paid_at");
            entity.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(p => p.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(p => p.DeliveryId).IsUnique();
            entity.HasIndex(p => p.SenderId);
            entity.HasIndex(p => p.TravelerId);
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.TransactionReference);
        });
    }
}
