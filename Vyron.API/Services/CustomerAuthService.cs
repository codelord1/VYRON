using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vyron.API.Data;
using Vyron.API.DTOs;
using Vyron.API.Models;
using Vyron.Shared.Enums;

namespace Vyron.API.Services;

// ─── INTERFACE ────────────────────────────────────────────────────
public interface ICustomerAuthService
{
    /// <summary>Phone + password login for returning customers. Never sends OTP.</summary>
    Task<(CustomerLoginResponse? Response, string? Error)> LoginAsync(CustomerLoginRequest request);

    /// <summary>Send an OTP to verify phone during new customer signup.</summary>
    Task<(bool Success, string Message, string? DevOtp)> RequestSignupOtpAsync(string phone);

    /// <summary>
    /// Complete signup: verify OTP, create account, hash password.
    /// Returns a full auth token pair so the user is immediately logged in.
    /// </summary>
    Task<(CustomerLoginResponse? Response, string? Error)> CompleteProfileAsync(CompleteProfileRequest request);

    /// <summary>Send an OTP for password reset (existing users only).</summary>
    Task<(bool Success, string Message, string? DevOtp)> RequestPasswordResetOtpAsync(string phone);

    /// <summary>Verify OTP + set new hashed password.</summary>
    Task<(bool Success, string? Error)> ResetPasswordAsync(ResetPasswordRequest request);
}

// ─── IMPLEMENTATION ───────────────────────────────────────────────
public class CustomerAuthService : ICustomerAuthService
{
    private readonly VyronDbContext _db;
    private readonly ITokenService _tokens;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly IConfiguration _config;
    private readonly ILogger<CustomerAuthService> _logger;
    private readonly PasswordHasher<User> _hasher = new();

    public CustomerAuthService(
        VyronDbContext db, ITokenService tokens,
        INotificationService notifications, IAuditService audit,
        IConfiguration config, ILogger<CustomerAuthService> logger)
    {
        _db = db; _tokens = tokens; _notifications = notifications;
        _audit = audit; _config = config; _logger = logger;
    }

    // ── LOGIN ──────────────────────────────────────────────────────
    public async Task<(CustomerLoginResponse? Response, string? Error)> LoginAsync(CustomerLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password))
            return (null, "Phone and password are required.");

        // Single-table lookup — no UserRoles JOIN before password is verified.
        // Avoids a 3-table join on every failed/wrong-password attempt.
        // Role names are loaded lean via a separate SELECT only after auth passes.
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Phone == request.Phone && u.IsActive);

        if (user == null)
            return (null, "No account found for this phone number. Please create an account.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            return (null, "This account does not have a password set. Please use 'Forgot Password' to create one.");

        // ⚠ bcrypt/PBKDF2 verification is intentionally slow (~600–700 ms on typical
        // hardware). This is the dominant cost of login and cannot be reduced without
        // weakening security. All operations below complete in < 100 ms combined.
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return (null, "Incorrect password. Please try again.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            user.PasswordChangedAt = DateTime.UtcNow;
        }

        user.LastLoginAt = DateTime.UtcNow;

        // Back-fill role entry for legacy accounts (DB check, not in-memory navigation)
        var hasRole = await _db.UserRoles.AnyAsync(ur => ur.UserId == user.Id);
        if (!hasRole)
            await EnsureCustomerRoleAsync(user);

        var refreshVal = _tokens.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshVal,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();

        // Lean role name query — only Names, no Role entity hydration
        var roleNames = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role!.Name)
            .Where(n => n != null)
            .Cast<string>()
            .ToListAsync();

        var access = _tokens.GenerateAccessToken(user, roleNames.Count > 0 ? roleNames : null);
        await _audit.LogAsync(user.Id, "CUSTOMER_LOGIN", "User", user.Id);

        _logger.LogInformation("[LOGIN] {Phone} ({Name})", user.Phone, user.FullName);
        return (new CustomerLoginResponse(access, refreshVal, DateTime.UtcNow.AddMinutes(60),
            new UserDto(user.Id, user.Phone, user.FullName, user.Email, user.Role, user.CreatedAt)), null);
    }

    // ── SIGNUP OTP ────────────────────────────────────────────────
    public async Task<(bool Success, string Message, string? DevOtp)> RequestSignupOtpAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return (false, "Phone number is required.", null);

        // Reject if account already exists with a password (they should just login)
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Phone == phone);
        if (existing != null && !string.IsNullOrEmpty(existing.PasswordHash))
            return (false, "An account with this phone number already exists. Please login instead.", null);

        return await SendOtpAsync(phone, "signup");
    }

    // ── COMPLETE PROFILE ─────────────────────────────────────────
    public async Task<(CustomerLoginResponse? Response, string? Error)> CompleteProfileAsync(CompleteProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return (null, "Full name is required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return (null, "Password must be at least 6 characters.");

        // Verify the OTP
        var otp = await _db.OtpCodes
            .Where(o => o.Phone == request.Phone && o.Code == request.Code
                     && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return (null, "Invalid or expired verification code. Please request a new one.");

        otp.IsUsed = true;

        // Load or create user — roles loaded separately via lean query after save
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Phone == request.Phone);

        var isNew = user == null;
        if (user == null)
        {
            user = new User
            {
                Phone = request.Phone,
                FullName = request.FullName.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Role = UserRole.Customer,
                IsActive = true
            };
            _db.Users.Add(user);
        }
        else
        {
            user.FullName = request.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(request.Email))
                user.Email = request.Email.Trim();
        }

        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.PhoneNumberVerified = true;
        user.LastLoginAt = DateTime.UtcNow;

        await EnsureCustomerRoleAsync(user);

        var refreshVal = _tokens.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshVal,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();

        try
        {
            await _notifications.SendSmsAsync(user.Phone,
                $"Welcome to VYRON, {user.FullName.Split(' ')[0]}!");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Welcome SMS failed after customer account creation for {Phone}", user.Phone);
        }

        var roleNames = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role!.Name)
            .Where(n => n != null)
            .Cast<string>()
            .ToListAsync();
        var access = _tokens.GenerateAccessToken(user, roleNames.Count > 0 ? roleNames : null);
        await _audit.LogAsync(user.Id, isNew ? "CUSTOMER_REGISTER" : "CUSTOMER_COMPLETE_PROFILE", "User", user.Id);

        _logger.LogInformation("[PROFILE] Customer profile completed: {Phone} ({Name})", user.Phone, user.FullName);
        return (new CustomerLoginResponse(access, refreshVal, DateTime.UtcNow.AddMinutes(60),
            new UserDto(user.Id, user.Phone, user.FullName, user.Email, user.Role, user.CreatedAt)), null);
    }

    // ── FORGOT PASSWORD OTP ───────────────────────────────────────
    public async Task<(bool Success, string Message, string? DevOtp)> RequestPasswordResetOtpAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return (false, "Phone number is required.", null);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == phone && u.IsActive);
        if (user == null)
            return (false, "No account found for this phone number.", null);

        return await SendOtpAsync(phone, "password-reset");
    }

    // ── RESET PASSWORD ────────────────────────────────────────────
    public async Task<(bool Success, string? Error)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return (false, "Password must be at least 6 characters.");

        var otp = await _db.OtpCodes
            .Where(o => o.Phone == request.Phone && o.Code == request.Code
                     && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return (false, "Invalid or expired verification code.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == request.Phone && u.IsActive);
        if (user == null)
            return (false, "Account not found.");

        otp.IsUsed = true;
        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.PhoneNumberVerified = true;

        await _db.SaveChangesAsync();
        await _audit.LogAsync(user.Id, "CUSTOMER_PASSWORD_RESET", "User", user.Id);

        _logger.LogInformation("[PWD-RESET] {Phone}", user.Phone);
        return (true, null);
    }

    // ── HELPERS ───────────────────────────────────────────────────
    private async Task<(bool Success, string Message, string? DevOtp)> SendOtpAsync(string phone, string context)
    {
        var recentCount = await _db.OtpCodes
            .CountAsync(o => o.Phone == phone && o.CreatedAt > DateTime.UtcNow.AddMinutes(-10));
        if (recentCount >= 3)
            return (false, "Too many requests. Please wait 10 minutes.", null);

        var old = await _db.OtpCodes
            .Where(o => o.Phone == phone && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
        foreach (var o in old) o.IsUsed = true;

        var expiryMinutes = _config.GetValue<int>("Otp:DefaultExpiryMinutes", 5);
        var code = new Random().Next(100000, 999999).ToString();
        _db.OtpCodes.Add(new OtpCode
        {
            Phone = phone,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
        });
        await _db.SaveChangesAsync();

        var returnOtpInDev = _config.GetValue<bool>("Otp:ReturnOtpInDevelopment", false);
        if (returnOtpInDev)
        {
            _logger.LogWarning("[DEV-OTP] ({Context}) for {Phone} -> {Code}", context, phone, code);
            return (true, "Verification code generated. (Development mode — see DevOtp in response.)", code);
        }

        var msg = context == "password-reset"
            ? $"Your VYRON password reset code is {code}. Valid for {expiryMinutes} minutes."
            : $"Your VYRON verification code is {code}. Valid for {expiryMinutes} minutes.";

        await _notifications.SendSmsAsync(phone, msg);
        _logger.LogInformation("OTP ({Context}) dispatched to {Phone}", context, phone);
        return (true, "Verification code sent to your phone.", null);
    }

    // Called only when a DB AnyAsync check confirmed the user has no roles yet.
    // Does NOT rely on the user navigation property being loaded.
    private async Task EnsureCustomerRoleAsync(User user)
    {
        var customerRole = await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == "CUSTOMER");
        if (customerRole != null)
            _db.UserRoles.Add(new AppUserRole { UserId = user.Id, RoleId = customerRole.Id });
    }
}
