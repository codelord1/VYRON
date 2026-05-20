using Microsoft.EntityFrameworkCore;
using Vyron.API.Models;

namespace Vyron.Admin.Data;

/// <summary>
/// Admin shares the same entity model as the API. The API owns schema
/// management — this context is for read/write admin operations only.
/// </summary>
public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<LaundryStore> Stores => Set<LaundryStore>();
    public DbSet<ServiceOffering> ServiceOfferings => Set<ServiceOffering>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Rider> Riders => Set<Rider>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeMessage> DisputeMessages => Set<DisputeMessage>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<StoreImage> StoreImages => Set<StoreImage>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
    public DbSet<ServiceTypeEntity> ServiceTypes => Set<ServiceTypeEntity>();
    public DbSet<CommunicationLog> CommunicationLogs => Set<CommunicationLog>();
    public DbSet<StoreUserAssignment> StoreUserAssignments => Set<StoreUserAssignment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponUsage> CouponUsages => Set<CouponUsage>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);

        // Don't generate keys — API/DB owns Id assignment
        m.Entity<User>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<LaundryStore>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<ServiceOffering>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<Order>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<OrderStatusHistory>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<Rider>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<Review>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<Dispute>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<DisputeMessage>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<Payment>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<Address>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<SystemConfig>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<AuditLog>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<Notification>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<StoreImage>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<PasswordResetToken>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<AppRole>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<AppUserRole>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<ServiceTypeEntity>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<CommunicationLog>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<StoreUserAssignment>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<ActivityLog>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<Coupon>().Property(x => x.Id).ValueGeneratedNever();
        m.Entity<CouponUsage>().Property(x => x.Id).ValueGeneratedNever();

        // Table names must match API
        m.Entity<AppRole>().ToTable("Roles");
        m.Entity<AppUserRole>().ToTable("UserRoles");
        m.Entity<ServiceTypeEntity>().ToTable("ServiceTypes");
        m.Entity<CommunicationLog>().ToTable("CommunicationLogs");
        m.Entity<StoreUserAssignment>().ToTable("StoreUserAssignments");
        m.Entity<ActivityLog>().ToTable("ActivityLogs");

        // Coupon / CouponUsage
        m.Entity<Coupon>(e => {
            e.ToTable("Coupons");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.DiscountType).HasMaxLength(20).IsRequired();
            e.Property(x => x.DiscountValue).HasColumnType("decimal(18,2)");
        });
        m.Entity<CouponUsage>(e => {
            e.ToTable("CouponUsages");
            e.HasIndex(x => new { x.CouponId, x.CustomerId });
            e.Property(x => x.DiscountApplied).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Coupon).WithMany(c => c.Usages).HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        });

        // ServiceOffering → ServiceTypeEntity optional FK
        m.Entity<ServiceOffering>()
            .HasOne(x => x.ServiceTypeRef)
            .WithMany(st => st.ServiceOfferings)
            .HasForeignKey(x => x.ServiceTypeEntityId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // CommunicationLog FKs
        m.Entity<CommunicationLog>()
            .HasOne(x => x.Recipient).WithMany()
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.NoAction).IsRequired(false);
        m.Entity<CommunicationLog>()
            .HasOne(x => x.SentByAdmin).WithMany()
            .HasForeignKey(x => x.SentByAdminId)
            .OnDelete(DeleteBehavior.NoAction).IsRequired(false);

        // StoreUserAssignment FKs
        m.Entity<StoreUserAssignment>()
            .HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<StoreUserAssignment>()
            .HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<StoreUserAssignment>()
            .HasOne(x => x.AssignedBy).WithMany().HasForeignKey(x => x.AssignedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // ActivityLog FK
        m.Entity<ActivityLog>()
            .HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull).IsRequired(false);

        // Order DeliveryRider FK
        m.Entity<Order>().HasOne(x => x.DeliveryRider).WithMany()
            .HasForeignKey(x => x.DeliveryRiderId).OnDelete(DeleteBehavior.NoAction).IsRequired(false);

        // Decimal for CommunicationLog (none) — no changes needed

        // UserRoles FK relationships
        m.Entity<AppUserRole>()
            .HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<AppUserRole>()
            .HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mirror the FK rules from API context
        m.Entity<LaundryStore>().HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<ServiceOffering>().HasOne(x => x.Store).WithMany(s => s.Services)
            .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<Order>().HasOne(x => x.Customer).WithMany(u => u.Orders)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Order>().HasOne(x => x.Store).WithMany(s => s.Orders)
            .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Order>().HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceOfferingId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<Order>().HasOne(x => x.Rider).WithMany(r => r.Orders)
            .HasForeignKey(x => x.RiderId).OnDelete(DeleteBehavior.SetNull);
        m.Entity<Order>().HasOne(x => x.Review).WithOne(r => r.Order)
            .HasForeignKey<Review>(r => r.OrderId);
        m.Entity<Order>().HasOne(x => x.Dispute).WithOne(d => d.Order)
            .HasForeignKey<Dispute>(d => d.OrderId);
        m.Entity<OrderStatusHistory>().HasOne(x => x.Order).WithMany(o => o.StatusHistory)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<Rider>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        m.Entity<Review>().HasOne(x => x.Customer).WithMany(u => u.Reviews)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Review>().HasOne(x => x.Store).WithMany(s => s.Reviews)
            .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Dispute>().HasOne(x => x.RaisedBy).WithMany(u => u.Disputes)
            .HasForeignKey(x => x.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<DisputeMessage>().HasOne(x => x.Dispute).WithMany(d => d.Messages)
            .HasForeignKey(x => x.DisputeId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<DisputeMessage>().HasOne(x => x.Sender).WithMany()
            .HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Payment>().HasOne(x => x.Order).WithMany(o => o.Payments)
            .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Address>().HasOne(x => x.User).WithMany(u => u.Addresses)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<StoreImage>().HasOne(x => x.Store).WithMany(s => s.StoreImages)
            .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<PasswordResetToken>().HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        // Decimal precision (must match API context)
        foreach (var prop in new[] {
            (typeof(LaundryStore), nameof(LaundryStore.PickupFee)),
            (typeof(LaundryStore), nameof(LaundryStore.DeliveryFee)),
            (typeof(LaundryStore), nameof(LaundryStore.AverageRating)),
            (typeof(ServiceOffering), nameof(ServiceOffering.BasePrice)),
            (typeof(ServiceOffering), nameof(ServiceOffering.MinimumCharge)),
            (typeof(Order), nameof(Order.EstimatedWeight)),
            (typeof(Order), nameof(Order.EstimatedLaundryCost)),
            (typeof(Order), nameof(Order.ActualLaundryCost)),
            (typeof(Order), nameof(Order.PickupFee)),
            (typeof(Order), nameof(Order.DeliveryFee)),
            (typeof(Order), nameof(Order.TotalEstimate)),
            (typeof(Order), nameof(Order.ActualTotal)),
            (typeof(Order), nameof(Order.PickupFeeAmount)),
            (typeof(Order), nameof(Order.BalanceAmount)),
            (typeof(Payment), nameof(Payment.Amount)),
            (typeof(Rider), nameof(Rider.TotalEarnings)),
            (typeof(Dispute), nameof(Dispute.RefundAmount))
        })
        {
            m.Entity(prop.Item1).Property(prop.Item2).HasColumnType(
                prop.Item2 == nameof(LaundryStore.AverageRating) ? "decimal(3,2)" : "decimal(18,2)");
        }
    }
}
