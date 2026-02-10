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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("conversations");

            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.PackageRequestId).HasColumnName("package_request_id");
            entity.Property(c => c.SenderId).HasColumnName("sender_id").IsRequired();
            entity.Property(c => c.TravelerId).HasColumnName("traveler_id").IsRequired();
            entity.Property(c => c.Status).HasColumnName("status").HasConversion<string>().IsRequired();
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
    }
}
