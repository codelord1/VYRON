using Microsoft.AspNetCore.Authentication.Cookies;
using Vyron.Admin.Persistence;
using Vyron.Admin.Services;

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

app.Run();
