using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Vyron.Admin.Data;
using Vyron.Admin.Helpers;
using Vyron.Admin.Persistence;
using Vyron.Admin.Services;
using Vyron.API.Models;
using Vyron.Shared.Enums;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ────────────────────────────────────────────────────
builder.Services.AddVyronAdminDatabase(builder.Configuration);

// ─── Memory cache (for idle timeout filter) ──────────────────────
builder.Services.AddMemoryCache();

// ─── Cookie authentication ───────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/AccessDenied";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        opt.SlidingExpiration = true;
        opt.Cookie.Name = "vyron.admin";
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("AdminOnly", p => p.RequireRole("Admin", "SuperAdmin"));
    opt.AddPolicy("StoreOwnerOnly", p => p.RequireRole("StoreOwner"));
});

// ─── Repos / analytics ───────────────────────────────────────────
builder.Services.AddScoped<IAdminAnalytics, AdminAnalytics>();
builder.Services.AddScoped<IOrderRepo, OrderRepo>();
builder.Services.AddScoped<IStoreRepo, StoreRepo>();
builder.Services.AddScoped<IStoreExtendedRepo, StoreExtendedRepo>();
builder.Services.AddScoped<IStoreImageRepo, StoreImageRepo>();
builder.Services.AddScoped<IRiderRepo, RiderRepo>();
builder.Services.AddScoped<IRiderExtendedRepo, RiderExtendedRepo>();
builder.Services.AddScoped<IDisputeRepo, DisputeRepo>();
builder.Services.AddScoped<IReviewRepo, ReviewRepo>();
builder.Services.AddScoped<IPaymentRepo, PaymentRepo>();
builder.Services.AddScoped<IConfigRepo, ConfigRepo>();
builder.Services.AddScoped<IServiceOfferingRepo, ServiceOfferingRepo>();
builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped<IPasswordResetRepo, PasswordResetRepo>();
builder.Services.AddScoped<IAdminUserRepo, AdminUserRepo>();
builder.Services.AddScoped<IStoreOwnerApprovalRepo, StoreOwnerApprovalRepo>();
builder.Services.AddScoped<IAuditLogRepo, AuditLogRepo>();
builder.Services.AddScoped<IActivityLogRepo, ActivityLogRepo>();
builder.Services.AddScoped<ICommunicationLogRepo, CommunicationLogRepo>();
builder.Services.AddScoped<IStoreStaffRepo, StoreStaffRepo>();

// ─── Idle timeout global filter ──────────────────────────────────
builder.Services.AddScoped<IdleTimeoutFilter>();

// ─── MVC ─────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<IdleTimeoutFilter>();
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ─── Middleware ───────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Account/Login");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default",
    pattern: "{controller=Account}/{action=Index}/{id?}");

// ─── SUPERADMIN SEED ─────────────────────────────────────────────
// Idempotent: runs only if no SuperAdmin exists in the DB.
// CHANGE the default password after first login.
// Default credentials:
//   Email:    superadmin@vyron.com
//   Password: Vyron@SuperAdmin2024!   ← CHANGE ON FIRST LOGIN
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    try
    {
        var hasSuperAdmin = await db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin);
        if (!hasSuperAdmin)
        {
            var superAdminRole = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "SUPERADMIN");
            var su = new User
            {
                FullName  = "Super Admin",
                Email     = "superadmin@vyron.com",
                Phone     = "+2340000000001",
                Role      = UserRole.SuperAdmin,
                IsActive  = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = null
            };
            su.PasswordHash = FileUploadHelper.HashPassword(su, "Vyron@SuperAdmin2024!");
            db.Users.Add(su);
            if (superAdminRole != null)
                db.UserRoles.Add(new AppUserRole { UserId = su.Id, RoleId = superAdminRole.Id });
            await db.SaveChangesAsync();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("[SEED] SuperAdmin created: superadmin@vyron.com — CHANGE PASSWORD NOW.");
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "[SEED] SuperAdmin seed skipped (DB may not be ready).");
    }
}

app.Run();
