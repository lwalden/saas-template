using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SaasTemplate.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SubscriptionEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => s.StripeSubscriptionId).IsUnique();
            e.HasIndex(s => s.StripeCustomerId);
            e.Property(s => s.StripePriceId).HasMaxLength(255);
            e.Property(s => s.Tier).HasMaxLength(50);
            e.Property(s => s.Status).HasMaxLength(50);
            e.HasOne(s => s.User).WithOne(u => u.Subscription).HasForeignKey<SubscriptionEntity>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasMaxLength(100).IsRequired();
            e.Property(a => a.UserId).HasMaxLength(450);
            e.Property(a => a.Email).HasMaxLength(256);
            e.Property(a => a.TargetType).HasMaxLength(100);
            e.Property(a => a.TargetId).HasMaxLength(256);
            e.Property(a => a.IpAddress).HasMaxLength(64);
            e.Property(a => a.UserAgent).HasMaxLength(512);
            e.HasIndex(a => a.Timestamp);
            e.HasIndex(a => a.UserId);
            e.HasIndex(a => a.Action);
        });

        builder.Entity<UsageEvent>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.UserId).HasMaxLength(450).IsRequired();
            e.Property(u => u.Meter).HasMaxLength(100).IsRequired();
            // Hot query: sum quantity for a user/meter within a period window.
            e.HasIndex(u => new { u.UserId, u.Meter, u.OccurredAt });
        });
    }
}
