using Microsoft.EntityFrameworkCore;
using Vyron.API.Models;
using Vyron.Shared.Enums;

namespace Vyron.API.Data;

public class VyronDbContext : DbContext
{
    public VyronDbContext(DbContextOptions<VyronDbContext> options) : base(options) { }

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
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<StoreImage> StoreImages => Set<StoreImage>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ServiceTypeEntity> ServiceTypes => Set<ServiceTypeEntity>();
    public DbSet<CommunicationLog> CommunicationLogs => Set<CommunicationLog>();

    // ─── Stable seed GUIDs (same across both providers) ─────────────
    private static readonly Guid AdminUserId   = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RiderUserId   = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Owner1UserId  = new("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Owner2UserId  = new("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Owner3UserId  = new("55555555-5555-5555-5555-555555555555");
    private static readonly Guid RiderId       = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Store1Id      = new("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Store2Id      = new("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Store3Id      = new("bbbbbbbb-0000-0000-0000-000000000003");

    // ─── Stable Role GUIDs ────────────────────────────────────────
    public static readonly Guid CustomerRoleId  = new("eeeeeeee-0000-0000-0000-000000000001");
    public static readonly Guid RiderRoleId     = new("eeeeeeee-0000-0000-0000-000000000002");
    public static readonly Guid StoreOwnerRoleId = new("eeeeeeee-0000-0000-0000-000000000003");
    public static readonly Guid AdminRoleId     = new("eeeeeeee-0000-0000-0000-000000000004");
    public static readonly Guid SuperAdminRoleId = new("eeeeeeee-0000-0000-0000-000000000005");

    protected override void OnModelCreating(ModelBuilder m)
    {
        base.OnModelCreating(m);

        // ─── USER ──────────────────────────────────────────────────
        m.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Role);
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
            e.Property(x => x.ProfilePhoto).HasMaxLength(500);
        });

        // ─── LAUNDRY STORE ─────────────────────────────────────────
        m.Entity<LaundryStore>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OwnerId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Area);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.Area).HasMaxLength(100);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.State).HasMaxLength(100);
            e.Property(x => x.OpeningHours).HasMaxLength(100);
            e.Property(x => x.OpeningDays).HasMaxLength(100);
            // TimeOnly maps to SQL Server 'time' column in EF Core 6+
            e.Property(x => x.OpeningTime).HasColumnType("time");
            e.Property(x => x.ClosingTime).HasColumnType("time");
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.BannerUrl).HasMaxLength(500);
            e.Property(x => x.AverageRating).HasColumnType("decimal(3,2)");
            e.Property(x => x.PickupFee).HasColumnType("decimal(18,2)");
            e.Property(x => x.DeliveryFee).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── SERVICE OFFERING ──────────────────────────────────────
        m.Entity<ServiceOffering>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.StoreId);
            e.HasIndex(x => new { x.StoreId, x.ServiceType });
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.BasePrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.MinimumCharge).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Store).WithMany(s => s.Services).HasForeignKey(x => x.StoreId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── ORDER ─────────────────────────────────────────────────
        m.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.StoreId);
            e.HasIndex(x => x.RiderId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.OrderNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.PickupAddress).HasMaxLength(500);
            e.Property(x => x.DeliveryAddress).HasMaxLength(500);
            e.Property(x => x.RequestedPickupSlot).HasMaxLength(50);
            e.Property(x => x.SpecialInstructions).HasMaxLength(1000);
            e.Property(x => x.AdminOverrideReason).HasMaxLength(500);
            foreach (var p in new[] {
                nameof(Order.EstimatedWeight), nameof(Order.EstimatedLaundryCost),
                nameof(Order.ActualLaundryCost), nameof(Order.PickupFee),
                nameof(Order.DeliveryFee), nameof(Order.TotalEstimate),
                nameof(Order.ActualTotal), nameof(Order.PickupFeeAmount),
                nameof(Order.BalanceAmount) })
                e.Property(p).HasColumnType("decimal(18,2)");

            e.HasOne(x => x.Customer).WithMany(u => u.Orders).HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Store).WithMany(s => s.Orders).HasForeignKey(x => x.StoreId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Service).WithMany().HasForeignKey(x => x.ServiceOfferingId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Rider).WithMany(r => r.Orders).HasForeignKey(x => x.RiderId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.DeliveryRider).WithMany().HasForeignKey(x => x.DeliveryRiderId)
             .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            e.HasOne(x => x.Review).WithOne(r => r.Order).HasForeignKey<Review>(r => r.OrderId);
            e.HasOne(x => x.Dispute).WithOne(d => d.Order).HasForeignKey<Dispute>(d => d.OrderId);
        });

        // ─── ORDER STATUS HISTORY ──────────────────────────────────
        m.Entity<OrderStatusHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.Order).WithMany(o => o.StatusHistory).HasForeignKey(x => x.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── ORDER ITEM ─────────────────────────────────────────────
        m.Entity<OrderItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
            e.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
            e.Property(x => x.PricingMode).HasMaxLength(20).IsRequired();
            e.Property(x => x.Weight).HasColumnType("decimal(18,3)");
            e.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(x => x.LineTotal).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Order).WithMany(o => o.Items).HasForeignKey(x => x.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ServiceOffering).WithMany().HasForeignKey(x => x.ServiceOfferingId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── RIDER ─────────────────────────────────────────────────
        m.Entity<Rider>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.VehicleType).HasMaxLength(50);
            e.Property(x => x.VehiclePlate).HasMaxLength(20);
            e.Property(x => x.TotalEarnings).HasColumnType("decimal(18,2)");
            e.Property(x => x.IsApproved).HasDefaultValue(false);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── REVIEW ────────────────────────────────────────────────
        m.Entity<Review>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.StoreId);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.OrderId).IsUnique();
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.PhotoUrl).HasMaxLength(500);
            e.Property(x => x.AdminNote).HasMaxLength(500);
            e.HasOne(x => x.Customer).WithMany(u => u.Reviews).HasForeignKey(x => x.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Store).WithMany(s => s.Reviews).HasForeignKey(x => x.StoreId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── DISPUTE ───────────────────────────────────────────────
        m.Entity<Dispute>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId).IsUnique();
            e.HasIndex(x => x.RaisedByUserId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.EvidenceUrl).HasMaxLength(500);
            e.Property(x => x.AdminNotes).HasMaxLength(3000);
            e.Property(x => x.ResolutionNote).HasMaxLength(1000);
            e.Property(x => x.RefundAmount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.RaisedBy).WithMany(u => u.Disputes).HasForeignKey(x => x.RaisedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<DisputeMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DisputeId);
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.HasOne(x => x.Dispute).WithMany(d => d.Messages).HasForeignKey(x => x.DisputeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── PAYMENT ───────────────────────────────────────────────
        m.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.PaymentRef).IsUnique();
            e.Property(x => x.PaymentRef).HasMaxLength(50).IsRequired();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Type).HasMaxLength(30).IsRequired();
            e.Property(x => x.GatewayRef).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Order).WithMany(o => o.Payments).HasForeignKey(x => x.OrderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── ADDRESS ───────────────────────────────────────────────
        m.Entity<Address>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Label).HasMaxLength(50);
            e.Property(x => x.Street).HasMaxLength(300).IsRequired();
            e.Property(x => x.Area).HasMaxLength(100);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.State).HasMaxLength(100);
            e.Property(x => x.Landmark).HasMaxLength(200);
            e.HasOne(x => x.User).WithMany(u => u.Addresses).HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── OTP ───────────────────────────────────────────────────
        m.Entity<OtpCode>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Phone);
            e.HasIndex(x => new { x.Phone, x.IsUsed });
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Code).HasMaxLength(10).IsRequired();
        });

        // ─── REFRESH TOKEN ─────────────────────────────────────────
        m.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Token).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens).HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── SYSTEM CONFIG ─────────────────────────────────────────
        m.Entity<SystemConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100).IsRequired();
            e.Property(x => x.Value).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
        });

        // ─── AUDIT LOG ─────────────────────────────────────────────
        m.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.Entity, x.EntityId });
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.Entity).HasMaxLength(100).IsRequired();
            e.Property(x => x.OldValue).HasMaxLength(2000);
            e.Property(x => x.NewValue).HasMaxLength(2000);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── STORE IMAGE ───────────────────────────────────────────
        m.Entity<StoreImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.StoreId);
            e.Property(x => x.ImagePath).HasMaxLength(500).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.HasOne(x => x.Store).WithMany(s => s.StoreImages).HasForeignKey(x => x.StoreId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── PASSWORD RESET TOKEN ──────────────────────────────────
        m.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.TokenHash).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── NOTIFICATION ──────────────────────────────────────────
        m.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.IsRead);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Type).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ─── SERVICE TYPE ENTITY ───────────────────────────────────
        m.Entity<ServiceTypeEntity>(e =>
        {
            e.ToTable("ServiceTypes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
        });

        // Update ServiceOffering to reference ServiceTypeEntity (optional FK)
        m.Entity<ServiceOffering>(e =>
        {
            e.HasOne(x => x.ServiceTypeRef)
             .WithMany(st => st.ServiceOfferings)
             .HasForeignKey(x => x.ServiceTypeEntityId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // ─── COMMUNICATION LOG ─────────────────────────────────────
        m.Entity<CommunicationLog>(e =>
        {
            e.ToTable("CommunicationLogs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Channel);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.RecipientUserId);
            e.Property(x => x.Channel).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.Property(x => x.RecipientPhone).HasMaxLength(30);
            e.Property(x => x.RecipientEmail).HasMaxLength(200);
            e.Property(x => x.RecipientName).HasMaxLength(120);
            e.Property(x => x.RelatedEntityType).HasMaxLength(50);
            e.Property(x => x.ErrorMessage).HasMaxLength(500);
            e.HasOne(x => x.Recipient).WithMany()
             .HasForeignKey(x => x.RecipientUserId)
             .OnDelete(DeleteBehavior.NoAction)   // NoAction avoids SQL Server multi-cascade-path error
             .IsRequired(false);
            e.HasOne(x => x.SentByAdmin).WithMany()
             .HasForeignKey(x => x.SentByAdminId)
             .OnDelete(DeleteBehavior.NoAction)   // NoAction avoids SQL Server multi-cascade-path error
             .IsRequired(false);
        });

        // ─── APP ROLE ──────────────────────────────────────────────
        m.Entity<AppRole>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedName).IsUnique();
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(200);
        });

        // ─── APP USER ROLE ─────────────────────────────────────────
        m.Entity<AppUserRole>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.RoleId);
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── SEED DATA ─────────────────────────────────────────────
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        m.Entity<User>().HasData(
            new User { Id = AdminUserId, Phone = "+2348000000000", FullName = "VYRON Admin", Email = "admin@vyron.com", Role = UserRole.Admin, IsActive = true, CreatedAt = seedDate },
            new User { Id = RiderUserId, Phone = "+2348011111111", FullName = "Adamu Kwara", Email = "adamu@vyron.com", Role = UserRole.Rider, IsActive = true, CreatedAt = seedDate },
            new User { Id = Owner1UserId, Phone = "+2348022222222", FullName = "Fresh & Clean Owner", Email = "fresh@vyron.com", Role = UserRole.StoreOwner, IsActive = true, CreatedAt = seedDate },
            new User { Id = Owner2UserId, Phone = "+2348033333333", FullName = "Sparkle Shine Owner", Email = "sparkle@vyron.com", Role = UserRole.StoreOwner, IsActive = true, CreatedAt = seedDate },
            new User { Id = Owner3UserId, Phone = "+2348044444444", FullName = "Royal Wash Owner", Email = "royal@vyron.com", Role = UserRole.StoreOwner, IsActive = true, CreatedAt = seedDate }
        );

        m.Entity<Rider>().HasData(
            new Rider { Id = RiderId, UserId = RiderUserId, VehicleType = "Motorcycle", VehiclePlate = "LND-123-AA", Status = RiderStatus.Offline, CreatedAt = seedDate }
        );

        m.Entity<LaundryStore>().HasData(
            new LaundryStore { Id = Store1Id, OwnerId = Owner1UserId, Name = "Fresh & Clean Laundry", Description = "We provide quality laundry and dry cleaning services with care and hygiene.", Phone = "+2348022222222", Email = "fresh@vyron.com", Address = "15 Admiralty Way", Area = "Lekki Phase 1", City = "Lagos", State = "Lagos", Latitude = 6.4315, Longitude = 3.4215, Status = StoreStatus.Active, IsVerified = true, IsTopRated = true, FastPickup = true, EstimatedPickupMinutes = 25, AverageRating = 4.8m, TotalReviews = 256, PickupFee = 1000, DeliveryFee = 1000, OpeningHours = "8:00 AM - 9:00 PM", CreatedAt = seedDate, UpdatedAt = seedDate },
            new LaundryStore { Id = Store2Id, OwnerId = Owner2UserId, Name = "Sparkle & Shine Cleaners", Description = "Bringing sparkle to your clothes. Trusted by thousands across Lagos.", Phone = "+2348033333333", Email = "sparkle@vyron.com", Address = "8 Opebi Road", Area = "Ikeja", City = "Lagos", State = "Lagos", Latitude = 6.5875, Longitude = 3.3503, Status = StoreStatus.Active, IsVerified = true, FastPickup = true, EstimatedPickupMinutes = 30, AverageRating = 4.6m, TotalReviews = 184, PickupFee = 1000, DeliveryFee = 1000, OpeningHours = "7:00 AM - 8:00 PM", CreatedAt = seedDate, UpdatedAt = seedDate },
            new LaundryStore { Id = Store3Id, OwnerId = Owner3UserId, Name = "Royal Wash House", Description = "Premium laundry services for those who care about quality.", Phone = "+2348044444444", Email = "royal@vyron.com", Address = "22 Bode Thomas", Area = "Surulere", City = "Lagos", State = "Lagos", Latitude = 6.5096, Longitude = 3.3597, Status = StoreStatus.Active, EstimatedPickupMinutes = 40, AverageRating = 4.5m, TotalReviews = 132, PickupFee = 750, DeliveryFee = 750, OpeningHours = "8:00 AM - 7:00 PM", CreatedAt = seedDate, UpdatedAt = seedDate }
        );

        m.Entity<ServiceOffering>().HasData(
            new ServiceOffering { Id = new Guid("cccccccc-0000-0000-0000-000000000001"), StoreId = Store1Id, ServiceType = ServiceType.WashOnly, Name = "Wash Only", PricingMode = PricingMode.PerKg, BasePrice = 800, MinimumCharge = 1500, IsActive = true, EstimatedHours = 24 },
            new ServiceOffering { Id = new Guid("cccccccc-0000-0000-0000-000000000002"), StoreId = Store1Id, ServiceType = ServiceType.WashAndIron, Name = "Wash & Iron", PricingMode = PricingMode.PerKg, BasePrice = 1200, MinimumCharge = 2000, IsActive = true, EstimatedHours = 36 },
            new ServiceOffering { Id = new Guid("cccccccc-0000-0000-0000-000000000003"), StoreId = Store1Id, ServiceType = ServiceType.IronOnly, Name = "Iron Only", PricingMode = PricingMode.PerItem, BasePrice = 150, MinimumCharge = 500, IsActive = true, EstimatedHours = 12 },
            new ServiceOffering { Id = new Guid("cccccccc-0000-0000-0000-000000000004"), StoreId = Store1Id, ServiceType = ServiceType.DryClean, Name = "Dry Clean", PricingMode = PricingMode.PerItem, BasePrice = 2500, MinimumCharge = 2500, IsActive = true, EstimatedHours = 48 },
            new ServiceOffering { Id = new Guid("cccccccc-0000-0000-0000-000000000005"), StoreId = Store2Id, ServiceType = ServiceType.WashOnly, Name = "Wash Only", PricingMode = PricingMode.PerKg, BasePrice = 750, MinimumCharge = 1200, IsActive = true, EstimatedHours = 24 },
            new ServiceOffering { Id = new Guid("cccccccc-0000-0000-0000-000000000006"), StoreId = Store2Id, ServiceType = ServiceType.WashAndIron, Name = "Wash & Iron", PricingMode = PricingMode.PerKg, BasePrice = 1100, MinimumCharge = 1800, IsActive = true, EstimatedHours = 30 },
            new ServiceOffering { Id = new Guid("cccccccc-0000-0000-0000-000000000007"), StoreId = Store3Id, ServiceType = ServiceType.WashOnly, Name = "Wash Only", PricingMode = PricingMode.PerKg, BasePrice = 700, MinimumCharge = 1000, IsActive = true, EstimatedHours = 24 },
            new ServiceOffering { Id = new Guid("cccccccc-0000-0000-0000-000000000008"), StoreId = Store3Id, ServiceType = ServiceType.WashAndIron, Name = "Wash & Iron", PricingMode = PricingMode.PerKg, BasePrice = 1000, MinimumCharge = 1600, IsActive = true, EstimatedHours = 36 }
        );

        m.Entity<SystemConfig>().HasData(
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000001"), Key = "RiderAssignmentMode", Value = "0", Description = "0=Manual, 1=AutoPlatform, 2=AutoThirdParty", UpdatedAt = seedDate },
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000002"), Key = "OtpExpiryMinutes", Value = "5", Description = "OTP validity in minutes", UpdatedAt = seedDate },
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000003"), Key = "MaxOtpAttempts", Value = "3", Description = "Max OTP retries", UpdatedAt = seedDate },
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000004"), Key = "DefaultPickupFee", Value = "1000", Description = "Default pickup fee (NGN)", UpdatedAt = seedDate },
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000005"), Key = "DefaultDeliveryFee", Value = "1000", Description = "Default delivery fee (NGN)", UpdatedAt = seedDate },
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000006"), Key = "PlatformCommissionPercent", Value = "10", Description = "Platform commission %", UpdatedAt = seedDate },
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000007"), Key = "ThirdPartyLogisticsApiUrl", Value = "", Description = "3rd party logistics API endpoint", UpdatedAt = seedDate },
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000008"), Key = "ThirdPartyLogisticsApiKey", Value = "", Description = "3rd party logistics API key", UpdatedAt = seedDate },
            new SystemConfig { Id = new Guid("dddddddd-0000-0000-0000-000000000009"), Key = "PortalIdleTimeoutMinutes", Value = "15", Description = "Portal idle session timeout (5–120 min). Client-side enforced; server cookie uses its own expiry.", UpdatedAt = seedDate }
        );

        // ─── SEED ROLES ────────────────────────────────────────────
        m.Entity<AppRole>().HasData(
            new AppRole { Id = CustomerRoleId,   Name = "Customer",   NormalizedName = "CUSTOMER",   Description = "Customer user",          IsActive = true, CreatedAt = seedDate },
            new AppRole { Id = RiderRoleId,      Name = "Rider",      NormalizedName = "RIDER",      Description = "Delivery rider",         IsActive = true, CreatedAt = seedDate },
            new AppRole { Id = StoreOwnerRoleId, Name = "StoreOwner", NormalizedName = "STOREOWNER", Description = "Laundry store owner",    IsActive = true, CreatedAt = seedDate },
            new AppRole { Id = AdminRoleId,      Name = "Admin",      NormalizedName = "ADMIN",      Description = "Platform administrator", IsActive = true, CreatedAt = seedDate },
            new AppRole { Id = SuperAdminRoleId, Name = "SuperAdmin", NormalizedName = "SUPERADMIN", Description = "Super administrator",    IsActive = true, CreatedAt = seedDate }
        );

        // ─── SEED USER-ROLE MAPPINGS (seeded users only) ───────────
        // Real users created via OTP get their UserRole in AuthService.VerifyOtpAsync.
        m.Entity<AppUserRole>().HasData(
            new AppUserRole { Id = new Guid("ffffffff-0000-0000-0000-000000000001"), UserId = AdminUserId,  RoleId = AdminRoleId,     CreatedAt = seedDate },
            new AppUserRole { Id = new Guid("ffffffff-0000-0000-0000-000000000002"), UserId = RiderUserId,  RoleId = RiderRoleId,     CreatedAt = seedDate },
            new AppUserRole { Id = new Guid("ffffffff-0000-0000-0000-000000000003"), UserId = Owner1UserId, RoleId = StoreOwnerRoleId, CreatedAt = seedDate },
            new AppUserRole { Id = new Guid("ffffffff-0000-0000-0000-000000000004"), UserId = Owner2UserId, RoleId = StoreOwnerRoleId, CreatedAt = seedDate },
            new AppUserRole { Id = new Guid("ffffffff-0000-0000-0000-000000000005"), UserId = Owner3UserId, RoleId = StoreOwnerRoleId, CreatedAt = seedDate }
        );
    }
}
