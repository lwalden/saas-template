using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SaasTemplate.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();

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
    }
}
