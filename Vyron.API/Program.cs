using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Vyron.API.Data;
using Vyron.API.Hubs;
using Vyron.API.Persistence;
using Vyron.API.Services;
using Vyron.Shared.Enums;

var builder = WebApplication.CreateBuilder(args);

// ─── LOCAL NETWORK ACCESS FOR PHYSICAL ANDROID TESTING ─────────────
// Allows TECNO / physical Android device on same Wi-Fi to reach the API.
// Phone URL: http://192.168.0.166:50680/swagger/index.html
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls(
        "http://0.0.0.0:50680",
        "https://0.0.0.0:50677"
    );
}

// ─── LOGGING ──────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/vyron-api-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();
builder.Host.UseSerilog();

// ─── RESPONSE COMPRESSION ─────────────────────────────────────────
// Shrinks JSON payloads by 60-70% over mobile networks (Brotli preferred, Gzip fallback).
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json" });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);

// ─── HTTP CLIENT FACTORY ──────────────────────────────────────────
// Reuses TCP connections; prevents socket exhaustion from per-send HttpClient.
builder.Services.AddHttpClient("sms").ConfigureHttpClient(c =>
    c.Timeout = TimeSpan.FromSeconds(8));

// ─── HTTP CONTEXT ACCESSOR ────────────────────────────────────────
// Used by StoreService to build absolute image URLs from the inbound request host.
builder.Services.AddHttpContextAccessor();

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
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<ICouponService, CouponService>();

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

// ─── REQUEST TIMING MIDDLEWARE ────────────────────────────────────
// Logs path, elapsed ms, HTTP status, and user ID for every CustomerApp request.
// NEVER logs passwords, OTPs, tokens, or PII body fields.
app.Use(async (ctx, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    sw.Stop();
    // Only log API calls (skip Hangfire, Swagger, static files)
    if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/hubs"))
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon";
        Log.Information("[PERF] {Method} {Path} → {Status} in {Ms}ms | uid={UserId}",
            ctx.Request.Method, ctx.Request.Path.Value, ctx.Response.StatusCode,
            sw.ElapsedMilliseconds, userId);
    }
});

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

// ─── GLOBAL EXCEPTION HANDLER ────────────────────────────────────
// Must be first in pipeline so it catches exceptions from all downstream middleware/controllers.
// Converts InvalidOperationException (known business errors) to 400/409.
// Catches all others as 500 without leaking stack traces to the mobile client.
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var ex = feature?.Error;
    ctx.Response.ContentType = "application/json";

    (int statusCode, string message) = ex switch
    {
        InvalidOperationException ioe => (StatusCodes.Status400BadRequest, ioe.Message),
        UnauthorizedAccessException   => (StatusCodes.Status401Unauthorized, "Unauthorized."),
        KeyNotFoundException knfe     => (StatusCodes.Status404NotFound, knfe.Message),
        _                             => (StatusCodes.Status500InternalServerError,
                                          "An unexpected error occurred. Please try again.")
    };

    ctx.Response.StatusCode = statusCode;
    await ctx.Response.WriteAsJsonAsync(new { error = message });

    if (statusCode == StatusCodes.Status500InternalServerError)
        Log.Error(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
}));

// ─── PIPELINE ─────────────────────────────────────────────────────
// Response compression first — compresses all downstream responses.
app.UseResponseCompression();
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
