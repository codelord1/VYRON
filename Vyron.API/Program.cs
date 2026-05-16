using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;
using Vyron.API.Data;
using Vyron.API.Hubs;
using Vyron.API.Persistence;
using Vyron.API.Services;
using Vyron.Shared.Enums;

var builder = WebApplication.CreateBuilder(args);

// ─── LOGGING ──────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/vyron-api-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();
builder.Host.UseSerilog();

// ─── DATABASE (provider switch via config) ────────────────────────
builder.Services.AddVyronDatabase(builder.Configuration);

// ─── HANGFIRE (matching provider) ─────────────────────────────────
builder.Services.AddHangfire(cfg =>
{
    cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
       .UseSimpleAssemblyNameTypeSerializer()
       .UseRecommendedSerializerSettings();
    DatabaseConfiguration.ConfigureHangfire(cfg, builder.Configuration);
});
builder.Services.AddHangfireServer();

// ─── AUTH ─────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ─── CORS ─────────────────────────────────────────────────────────
builder.Services.AddCors(opt => opt.AddPolicy("VyronV3", p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ─── SIGNALR ──────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── SERVICES (DI) ────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICustomerAuthService, CustomerAuthService>();
builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IDisputeService, DisputeService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// ─── CONTROLLERS + SWAGGER ────────────────────────────────────────
// JsonStringEnumConverter: serialises UserRole (and all enums) as strings ("Customer")
// instead of integers (0). CustomerApp deserialises UserDto.Role as string — without
// this the response body causes a JsonException on the client and the login never
// completes even though the user was created successfully in the DB.
builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v3", new OpenApiInfo
    {
        Title = "VYRON Laundry Marketplace API",
        Version = "v3",
        Description = "Trust + logistics + payment coordination — Lagos, Nigeria."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your_token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
          Array.Empty<string>() }
    });
});

var app = builder.Build();

// ─── HANGFIRE DB AUTO-CREATE (Development, SQL Server only) ──────
// Prevents "Cannot open database VYRONDB_Hangfire" on first run.
// Only runs in Development — never touches Production databases.
if (app.Environment.IsDevelopment())
{
    var provider = DatabaseConfiguration.GetProvider(builder.Configuration);
    if (provider == DatabaseProvider.SqlServer)
    {
        var hangfireConn = builder.Configuration.GetConnectionString("HangfireConnection");
        if (!string.IsNullOrWhiteSpace(hangfireConn))
        {
            try
            {
                var csb = new SqlConnectionStringBuilder(hangfireConn);
                var dbName = csb.InitialCatalog;
                csb.InitialCatalog = "master";

                using var conn = new SqlConnection(csb.ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}]";
                cmd.ExecuteNonQuery();
                Log.Information("✅ Hangfire database '{DbName}' is ready.", dbName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "⚠️  Could not auto-create Hangfire database. " +
                    "Run manually: IF DB_ID(N'VYRONDB_Hangfire') IS NULL CREATE DATABASE [VYRONDB_Hangfire]");
            }
        }
    }
}

// ─── MIGRATE DATABASE ─────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<VyronDbContext>();
        db.Database.Migrate();
        var provider = DatabaseConfiguration.GetProvider(builder.Configuration);
        Log.Information("✅ VYRONDB migrated using {Provider}.", provider);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Database migration failed.");
    }
}

// ─── PIPELINE ─────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v3/swagger.json", "VYRON API v3"); c.RoutePrefix = "swagger"; });
app.UseCors("VyronV3");
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

app.UseStaticFiles();  // serves API's own wwwroot/uploads/** for store/rider images

// ─── SERVE ADMIN-UPLOADED FILES ───────────────────────────────────
// Admin portal saves uploaded images to Vyron.Admin/wwwroot/uploads/stores/.
// The API only serves its own wwwroot by default, so those images would be
// inaccessible to the CustomerApp. Register the Admin wwwroot as a second
// static-file provider so /uploads/stores/{guid}.png resolves correctly.
{
    var adminWwwroot = Path.GetFullPath(
        Path.Combine(app.Environment.ContentRootPath, "..", "Vyron.Admin", "wwwroot"));
    if (Directory.Exists(adminWwwroot))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(adminWwwroot),
            RequestPath  = ""
        });
        Log.Information("✅ Serving Admin uploaded files from {Path}", adminWwwroot);
    }
    else
    {
        Log.Warning("⚠️  Admin wwwroot not found at {Path} — store images may not load.", adminWwwroot);
    }
}

app.MapControllers();
app.MapHub<OrderTrackingHub>("/hubs/tracking");

Log.Information("🚀 VYRON API v3 started.");
app.Run();
