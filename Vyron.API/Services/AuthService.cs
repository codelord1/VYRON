using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using System.Net.Http;
using Vyron.API.Data;
using Vyron.API.DTOs;
using Vyron.API.Models;
using Vyron.Shared.Enums;

namespace Vyron.API.Services;

// ─── NOTIFICATION ─────────────────────────────────────────────────
public interface INotificationService
{
    Task SendSmsAsync(string phone, string message, Guid? userId = null,
        string? entityType = null, Guid? entityId = null);
    Task SendEmailAsync(string to, string subject, string htmlBody, Guid? userId = null,
        string? entityType = null, Guid? entityId = null);
    /// <summary>Persist an in-app notification to the Notifications table.</summary>
    Task SendInAppAsync(Guid userId, string title, string message, string type = "inapp",
        string? entityType = null, Guid? entityId = null);
}

public class NotificationService : INotificationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;
    private readonly VyronDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public NotificationService(IConfiguration config, ILogger<NotificationService> logger,
        VyronDbContext db, IHttpClientFactory httpClientFactory)
    { _config = config; _logger = logger; _db = db; _httpClientFactory = httpClientFactory; }

    public async Task SendSmsAsync(string phone, string message, Guid? userId = null,
        string? entityType = null, Guid? entityId = null)
    {
        var log = new CommunicationLog
        {
            Channel = "SMS", RecipientUserId = userId, RecipientPhone = phone,
            Subject = "SMS", Body = message.Length > 4000 ? message[..4000] : message,
            RelatedEntityType = entityType, RelatedEntityId = entityId,
            Provider = "Termii", Status = "Pending"
        };
        _db.CommunicationLogs.Add(log);

        var apiKey = _config["Sms:TermiiApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            try
            {
                // Use named client from IHttpClientFactory — reuses TCP connections, 8s timeout
                var client = _httpClientFactory.CreateClient("sms");
                var payload = new {
                    to = phone, from = _config["Sms:TermiiSenderId"] ?? "VYRON",
                    sms = message, type = "plain", channel = "generic", api_key = apiKey
                };
                var resp = await client.PostAsJsonAsync("https://api.ng.termii.com/api/sms/send", payload);
                log.Status = resp.IsSuccessStatusCode ? "Sent" : "Failed";
                log.SentAt = DateTime.UtcNow;
                if (!resp.IsSuccessStatusCode)
                    log.ErrorMessage = $"HTTP {(int)resp.StatusCode}";
            }
            catch (Exception ex)
            {
                log.Status = "Failed"; log.ErrorMessage = ex.Message;
                _logger.LogError(ex, "SMS send failed to {Phone}", phone);
            }
        }
        else
        {
            // No SMS provider configured — log as Skipped (graceful degradation)
            log.Status = "Skipped"; log.ErrorMessage = "SMS provider not configured";
            _logger.LogWarning("[SMS-SKIP] no provider -> {Phone}: {Message}", phone, message);
        }

        try { await _db.SaveChangesAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "CommunicationLog save failed"); }
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, Guid? userId = null,
        string? entityType = null, Guid? entityId = null)
    {
        var log = new CommunicationLog
        {
            Channel = "Email", RecipientUserId = userId, RecipientEmail = to,
            Subject = subject.Length > 300 ? subject[..300] : subject,
            Body = htmlBody.Length > 4000 ? htmlBody[..4000] : htmlBody,
            RelatedEntityType = entityType, RelatedEntityId = entityId,
            Provider = "SMTP", Status = "Pending"
        };
        _db.CommunicationLogs.Add(log);

        var host = _config["Email:Host"];
        var username = _config["Email:Username"];
        var password = _config["Email:Password"];

        if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_config["Email:FromName"] ?? "VYRON", username));
                email.To.Add(MailboxAddress.Parse(to));
                email.Subject = subject;
                email.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(host, int.Parse(_config["Email:Port"] ?? "587"), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(username, password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                log.Status = "Sent"; log.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                log.Status = "Failed"; log.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Email failed to {To}", to);
            }
        }
        else
        {
            log.Status = "Skipped"; log.ErrorMessage = "Email provider not configured";
            _logger.LogWarning("[EMAIL-SKIP] no provider -> {To}: {Subject}", to, subject);
        }

        try { await _db.SaveChangesAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "CommunicationLog email save failed"); }
    }

    public async Task SendInAppAsync(Guid userId, string title, string message, string type = "inapp",
        string? entityType = null, Guid? entityId = null)
    {
        try
        {
            _db.Notifications.Add(new Models.Notification
            {
                UserId = userId, Title = title, Message = message, Type = type,
                IsSent = true, IsRead = false, SentAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
                RelatedEntityType = entityType, RelatedEntityId = entityId
            });
            // Also log CommunicationLog for in-app
            _db.CommunicationLogs.Add(new CommunicationLog
            {
                Channel = "InApp", RecipientUserId = userId,
                Subject = title.Length > 300 ? title[..300] : title,
                Body = message.Length > 4000 ? message[..4000] : message,
                RelatedEntityType = entityType, RelatedEntityId = entityId,
                Provider = "InApp", Status = "Sent", SentAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "In-app notification failed for user {UserId}", userId); }
    }
}

// ─── AUDIT ────────────────────────────────────────────────────────
public interface IAuditService
{
    Task LogAsync(Guid? userId, string action, string entity, Guid? entityId,
        string? oldValue = null, string? newValue = null, string? ip = null);
}

/// <summary>
/// Fire-and-forget audit logger. Returns Task.CompletedTask immediately so callers
/// are never blocked by an audit write. Exceptions are caught and logged.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IServiceScopeFactory scopeFactory, ILogger<AuditService> logger)
    { _scopeFactory = scopeFactory; _logger = logger; }

    public Task LogAsync(Guid? userId, string action, string entity, Guid? entityId,
        string? oldValue = null, string? newValue = null, string? ip = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VyronDbContext>();
                db.AuditLogs.Add(new AuditLog
                {
                    UserId = userId, Action = action, Entity = entity, EntityId = entityId,
                    OldValue = oldValue, NewValue = newValue, IpAddress = ip
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "AuditLog failed: {Action} on {Entity}", action, entity); }
        });
        return Task.CompletedTask;
    }
}

// ─── ACTIVITY LOG ─────────────────────────────────────────────────
public interface IActivityLogService
{
    Task LogAsync(Guid? userId, string activityType, string? description = null,
        string? entityType = null, Guid? entityId = null, string? ip = null, string? ua = null);
}

/// <summary>Fire-and-forget activity logger — never blocks the request thread.</summary>
public class ActivityLogService : IActivityLogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(IServiceScopeFactory scopeFactory, ILogger<ActivityLogService> logger)
    { _scopeFactory = scopeFactory; _logger = logger; }

    public Task LogAsync(Guid? userId, string activityType, string? description = null,
        string? entityType = null, Guid? entityId = null, string? ip = null, string? ua = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VyronDbContext>();
                db.ActivityLogs.Add(new ActivityLog
                {
                    UserId = userId, ActivityType = activityType, Description = description,
                    EntityType = entityType, EntityId = entityId,
                    IpAddress = ip, UserAgent = ua
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "ActivityLog failed for {Type}", activityType); }
        });
        return Task.CompletedTask;
    }
}

// ─── AUTH ─────────────────────────────────────────────────────────
public interface IAuthService
{
    /// <summary>
    /// Generates and (in non-dev) sends an OTP for the given phone number.
    /// Returns DevOtp only when Otp:ReturnOtpInDevelopment is true in config.
    /// DevOtp is always null in production.
    /// </summary>
    Task<(bool Success, string Message, string? DevOtp)> SendOtpAsync(string phone);
    Task<VerifyOtpResponse?> VerifyOtpAsync(VerifyOtpRequest request);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken);
}

public class AuthService : IAuthService
{
    private readonly VyronDbContext _db;
    private readonly ITokenService _tokens;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(VyronDbContext db, ITokenService tokens,
        INotificationService notifications, IAuditService audit,
        IConfiguration config, ILogger<AuthService> logger)
    {
        _db = db; _tokens = tokens; _notifications = notifications;
        _audit = audit; _config = config; _logger = logger;
    }

    public async Task<(bool Success, string Message, string? DevOtp)> SendOtpAsync(string phone)
    {
        var recentCount = await _db.OtpCodes
            .CountAsync(o => o.Phone == phone && o.CreatedAt > DateTime.UtcNow.AddMinutes(-10));
        if (recentCount >= 3)
            return (false, "Too many OTP requests. Please wait 10 minutes.", null);

        var old = await _db.OtpCodes
            .Where(o => o.Phone == phone && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
        foreach (var o in old) o.IsUsed = true;

        var expiryMinutes = _config.GetValue<int>("Otp:DefaultExpiryMinutes", 5);
        var code = new Random().Next(100000, 999999).ToString();
        _db.OtpCodes.Add(new OtpCode
        {
            Phone = phone, Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        });
        await _db.SaveChangesAsync();

        // In Development: return OTP in response + log it. Skip SMS (no provider configured).
        var returnOtpInDev = _config.GetValue<bool>("Otp:ReturnOtpInDevelopment", false);
        if (returnOtpInDev)
        {
            _logger.LogWarning("[DEV-OTP] for {Phone} -> {Code} (not sending SMS in dev mode)", phone, code);
            return (true, "Verification code generated. (Development mode — see DevOtp in response.)", code);
        }

        // Production: send via SMS provider, never expose code in response
        await _notifications.SendSmsAsync(phone,
            $"Your VYRON verification code is {code}. Valid for {expiryMinutes} minutes.");
        _logger.LogInformation("OTP dispatched to {Phone}", phone);
        return (true, "Verification code sent to your phone.", null);
    }

    public async Task<VerifyOtpResponse?> VerifyOtpAsync(VerifyOtpRequest request)
    {
        var otp = await _db.OtpCodes
            .Where(o => o.Phone == request.Phone && o.Code == request.Code
                     && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync();
        if (otp == null) return null;

        otp.IsUsed = true;

        // Load user with their UserRoles so we can emit accurate JWT claims
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Phone == request.Phone);
        var isNew = user == null;

        if (user == null)
        {
            // New user — store phone now; FullName is filled in on the CompleteProfile screen.
            // Using an empty string so RequiresProfileCompletion is correctly flagged.
            user = new User
            {
                Phone    = request.Phone,
                FullName = request.FullName?.Trim() ?? "",
                Role     = UserRole.Customer,
                IsActive = true
            };
            _db.Users.Add(user);

            // Assign Customer role via UserRoles table
            var customerRole = await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "CUSTOMER");
            if (customerRole != null)
            {
                _db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = customerRole.Id });
            }

            _logger.LogInformation("[NEW-USER] registered: {Phone}", request.Phone);
        }
        else
        {
            // Returning user — update name only when one is explicitly provided and none exists.
            if (!string.IsNullOrWhiteSpace(request.FullName) && string.IsNullOrWhiteSpace(user.FullName))
                user.FullName = request.FullName.Trim();

            // Ensure existing user has a UserRoles entry (back-fill if missing)
            if (!user.UserRoles.Any())
            {
                var matchingRole = await _db.Roles
                    .FirstOrDefaultAsync(r => r.Name == user.Role.ToString());
                if (matchingRole != null)
                    _db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = matchingRole.Id });
            }

            _logger.LogInformation("[LOGIN] returning customer: {Phone} ({Name})", user.Phone, user.FullName);
        }

        user.LastLoginAt = DateTime.UtcNow;

        var refreshVal = _tokens.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId    = user.Id,
            Token     = refreshVal,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();

        // Profile is "complete" when the user has set a name.
        var requiresProfileCompletion = string.IsNullOrWhiteSpace(user.FullName);

        // Welcome SMS — only for truly new registrations that already have a name.
        if (isNew && !requiresProfileCompletion)
            await _notifications.SendSmsAsync(user.Phone,
                $"Welcome to VYRON, {user.FullName.Split(' ')[0]}! 🧺");
        else if (isNew)
            await _notifications.SendSmsAsync(user.Phone,
                "Welcome to VYRON! 🧺 Complete your profile to get started.");

        // Generate token using UserRoles as authoritative source
        var roleNames = user.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(n => n != null)
            .Cast<string>()
            .ToList();

        var access = _tokens.GenerateAccessToken(user, roleNames.Count > 0 ? roleNames : null);
        await _audit.LogAsync(user.Id, isNew ? "USER_REGISTER" : "USER_LOGIN", "User", user.Id);

        return new VerifyOtpResponse(
            access, refreshVal, DateTime.UtcNow.AddMinutes(60),
            new UserDto(user.Id, user.Phone, user.FullName, user.Email, user.Role, user.CreatedAt),
            isNew, requiresProfileCompletion);
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens
            .Include(t => t.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.Token == refreshToken
                                  && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);
        if (token == null) return null;

        token.IsRevoked = true;
        var newRefresh = _tokens.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.UserId, Token = newRefresh,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();

        var refreshRoleNames = token.User.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(n => n != null)
            .Cast<string>()
            .ToList();

        var access = _tokens.GenerateAccessToken(token.User,
            refreshRoleNames.Count > 0 ? refreshRoleNames : null);
        return new AuthResponse(access, newRefresh, DateTime.UtcNow.AddMinutes(60),
            new UserDto(token.User.Id, token.User.Phone, token.User.FullName,
                token.User.Email, token.User.Role, token.User.CreatedAt));
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (token != null) { token.IsRevoked = true; await _db.SaveChangesAsync(); }
    }
}
