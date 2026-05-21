using Vyron.CustomerApp.DTOs;
using Vyron.CustomerApp.Helpers;
using Vyron.CustomerApp.Models;

namespace Vyron.CustomerApp.Services;

public interface IAuthService
{
    // ── Legacy OTP flow (kept for backward compat / Admin+Rider apps) ──────
    /// <summary>
    /// Requests an OTP for the given E.164 phone number.
    /// Returns (Ok, DevOtp, Error). DevOtp is non-null only when the API is in Development mode.
    /// </summary>
    Task<(bool Ok, string? DevOtp, string? Error)> SendOtpAsync(string phone);

    /// <summary>
    /// Verifies the OTP. On success, persists the session and returns
    /// (Ok, RequiresProfileCompletion, Error).
    /// RequiresProfileCompletion = true means route to CompleteProfile screen.
    /// </summary>
    Task<(bool Ok, bool RequiresProfileCompletion, string? Error)> VerifyOtpAsync(string phone, string code);

    // ── New password-based auth flows ───────────────────────────────────────
    /// <summary>Returning customer login: phone + password. No OTP.</summary>
    /// <param name="rememberMe">When true, token is persisted to SecureStorage and restored on next app launch.</param>
    Task<(bool Ok, string? Error)> LoginAsync(string phone, string password, bool rememberMe = false);

    /// <summary>Send OTP to phone for new-customer signup verification.</summary>
    Task<(bool Ok, string? DevOtp, string? Error)> RequestSignupOtpAsync(string phone);

    /// <summary>
    /// Verify OTP + create account with profile + hashed password.
    /// Persists session on success.
    /// </summary>
    Task<(bool Ok, string? Error)> CompleteProfileAsync(string phone, string code, string fullName, string? email, string password);

    /// <summary>Send OTP to phone for password reset (must be existing account).</summary>
    Task<(bool Ok, string? DevOtp, string? Error)> RequestPasswordResetAsync(string phone);

    /// <summary>Verify reset OTP and set new hashed password.</summary>
    Task<(bool Ok, string? Error)> ResetPasswordAsync(string phone, string code, string newPassword);

    // ── Session ─────────────────────────────────────────────────────────────
    Task<bool> TryRestoreSessionAsync();
    Task LogoutAsync();
}

public class AuthService : IAuthService
{
    private readonly ApiService         _api;
    private readonly OrderDraftService  _draft;

    private const string KeyRefreshToken = "vyron_refresh_token";
    private const string KeyAccessToken  = "vyron_access_token";
    private const string KeyExpiresAt    = "vyron_token_expires";
    private const string KeyUserId       = "vyron_user_id";
    private const string KeyUserFullName = "vyron_user_name";
    private const string KeyUserPhone    = "vyron_user_phone";
    private const string KeyRememberMe   = "vyron_remember_me";

    public AuthService(ApiService api, OrderDraftService draft)
    {
        _api   = api;
        _draft = draft;
    }

    public async Task<(bool Ok, string? DevOtp, string? Error)> SendOtpAsync(string phone)
    {
        var (response, error) = await _api.PostAsync<SendOtpResponse>("api/auth/send-otp",
            new SendOtpRequest(phone));

        if (error != null)
            return (false, null, error);

        if (response == null || !response.Success)
            return (false, null, response?.Message ?? "Failed to send OTP. Please try again.");

        return (true, response.DevOtp, null);
    }

    public async Task<(bool Ok, bool RequiresProfileCompletion, string? Error)> VerifyOtpAsync(
        string phone, string code)
    {
        // FullName is intentionally null here — profile completion is handled on a separate screen.
        var (response, error) = await _api.PostAsync<VerifyOtpResponse>("api/auth/verify-otp",
            new VerifyOtpRequest(phone, code, null));

        if (response == null)
            return (false, false, error ?? "Invalid or expired OTP.");

        // Persist session before returning so CompleteProfile screen can use the token.
        var authData = new AuthResponse
        {
            AccessToken  = response.AccessToken,
            RefreshToken = response.RefreshToken,
            ExpiresAt    = response.ExpiresAt,
            User         = response.User
        };
        AppSession.Current.SetAuth(authData);
        await PersistTokenAsync(authData);

        return (true, response.RequiresProfileCompletion, null);
    }

    // ── NEW: password-based login ──────────────────────────────────
    public async Task<(bool Ok, string? Error)> LoginAsync(string phone, string password, bool rememberMe = false)
    {
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"[VYRON LOGIN CLIENT] Phone sent to API: '{phone}'");
#endif
        var (response, error) = await _api.PostAsync<CustomerLoginResponse>("api/customer-auth/login",
            new CustomerLoginRequest(phone, password));

        if (response == null)
            return (false, FriendlyAuthError(error ?? "Login failed. Please check your credentials and try again."));

        // ── Session isolation: clear any previous user's state before setting new auth ──
        _draft.Clear();
        AppSession.Current.Clear();
        await ClearStoredTokenAsync();

        var authData = new AuthResponse
        {
            AccessToken  = response.AccessToken,
            RefreshToken = response.RefreshToken,
            ExpiresAt    = response.ExpiresAt,
            User         = response.User
        };
        AppSession.Current.SetAuth(authData);

        // Only persist to SecureStorage when "Remember me" is checked.
        // Without this flag, closing and relaunching the app clears the session.
        if (rememberMe)
            await PersistTokenAsync(authData, rememberMe: true);

        return (true, null);
    }

    // ── NEW: signup OTP ───────────────────────────────────────────
    public async Task<(bool Ok, string? DevOtp, string? Error)> RequestSignupOtpAsync(string phone)
    {
        var (response, error) = await _api.PostAsync<OtpSentResponse>("api/customer-auth/request-signup-otp",
            new SignupOtpRequest(phone));

        if (error != null) return (false, null, FriendlyAuthError(error));
        if (response == null) return (false, null, "Could not send verification code. Please try again.");
        return (true, response.DevOtp, null);
    }

    // ── NEW: complete profile (OTP + profile + password in one step) ─
    public async Task<(bool Ok, string? Error)> CompleteProfileAsync(
        string phone, string code, string fullName, string? email, string password)
    {
        var (response, error) = await _api.PostAsync<CustomerLoginResponse>("api/customer-auth/complete-profile",
            new CompleteProfileRequest(phone, code, fullName, email, password));

        if (response == null)
            return (false, FriendlyAuthError(error ?? "Account creation failed. Please try again."));

        // ── Session isolation: clear any previous user's state before setting new auth ──
        _draft.Clear();
        AppSession.Current.Clear();
        await ClearStoredTokenAsync();

        var authData = new AuthResponse
        {
            AccessToken  = response.AccessToken,
            RefreshToken = response.RefreshToken,
            ExpiresAt    = response.ExpiresAt,
            User         = response.User
        };
        AppSession.Current.SetAuth(authData);
        await PersistTokenAsync(authData);
        return (true, null);
    }

    // ── NEW: password reset OTP ───────────────────────────────────
    public async Task<(bool Ok, string? DevOtp, string? Error)> RequestPasswordResetAsync(string phone)
    {
        var (response, error) = await _api.PostAsync<OtpSentResponse>("api/customer-auth/request-password-reset-otp",
            new PasswordResetOtpRequest(phone));

        if (error != null) return (false, null, FriendlyAuthError(error));
        if (response == null) return (false, null, "Could not send reset code. Please try again.");
        return (true, response.DevOtp, null);
    }

    // ── NEW: reset password ───────────────────────────────────────
    public async Task<(bool Ok, string? Error)> ResetPasswordAsync(string phone, string code, string newPassword)
    {
        var (_, error) = await _api.PostAsync<object>("api/customer-auth/reset-password",
            new ResetPasswordRequest(phone, code, newPassword));

        return error != null ? (false, FriendlyAuthError(error)) : (true, null);
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            // If the user did not check "Remember me", wipe stored credentials and return false.
            var rememberFlag = await SecureStorage.Default.GetAsync(KeyRememberMe);
            if (rememberFlag != "true")
            {
                await ClearStoredTokenAsync();
                return false;
            }

            var refreshToken = await SecureStorage.Default.GetAsync(KeyRefreshToken);
            if (string.IsNullOrEmpty(refreshToken)) return false;

            var expiresStr = await SecureStorage.Default.GetAsync(KeyExpiresAt);
            if (!DateTime.TryParse(expiresStr, out var expires) || expires < DateTime.UtcNow.AddMinutes(2))
            {
                // Access token expired — try refresh
                var (auth, _) = await _api.PostAsync<AuthResponse>("api/auth/refresh-token",
                    new RefreshTokenRequest(refreshToken));

                if (auth == null)
                {
                    await ClearStoredTokenAsync();
                    return false;
                }

                AppSession.Current.SetAuth(auth);
                await PersistTokenAsync(auth, rememberMe: true);
                return true;
            }

            // Restore from stored values — token is still valid
            var accessToken = await SecureStorage.Default.GetAsync(KeyAccessToken);
            if (string.IsNullOrEmpty(accessToken)) return false;

            var userId = await SecureStorage.Default.GetAsync(KeyUserId);
            var name   = await SecureStorage.Default.GetAsync(KeyUserFullName);
            var phone  = await SecureStorage.Default.GetAsync(KeyUserPhone);

            AppSession.Current.SetAuth(new AuthResponse
            {
                AccessToken  = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt    = expires,
                User = new UserDto
                {
                    Id       = Guid.TryParse(userId, out var id) ? id : Guid.Empty,
                    FullName = name  ?? "",
                    Phone    = phone ?? ""
                }
            });
            return true;
        }
        catch { return false; }
    }

    public async Task LogoutAsync()
    {
        var refresh = AppSession.Current.RefreshToken;
        if (!string.IsNullOrEmpty(refresh))
            await _api.PostAsync<object>("api/auth/logout", new RefreshTokenRequest(refresh));

        // Clear all session state: token, user, cart draft
        _draft.Clear();
        AppSession.Current.Clear();
        await ClearStoredTokenAsync();
    }

    private static async Task PersistTokenAsync(AuthResponse auth, bool rememberMe = false)
    {
        await SecureStorage.Default.SetAsync(KeyAccessToken,  auth.AccessToken);
        await SecureStorage.Default.SetAsync(KeyRefreshToken, auth.RefreshToken);
        await SecureStorage.Default.SetAsync(KeyExpiresAt,    auth.ExpiresAt.ToString("O"));
        await SecureStorage.Default.SetAsync(KeyUserId,       auth.User.Id.ToString());
        await SecureStorage.Default.SetAsync(KeyUserFullName, auth.User.FullName);
        await SecureStorage.Default.SetAsync(KeyUserPhone,    auth.User.Phone);
        await SecureStorage.Default.SetAsync(KeyRememberMe,   rememberMe ? "true" : "false");
    }

    private static async Task ClearStoredTokenAsync()
    {
        SecureStorage.Default.Remove(KeyAccessToken);
        SecureStorage.Default.Remove(KeyRefreshToken);
        SecureStorage.Default.Remove(KeyExpiresAt);
        SecureStorage.Default.Remove(KeyUserId);
        SecureStorage.Default.Remove(KeyUserFullName);
        SecureStorage.Default.Remove(KeyUserPhone);
        SecureStorage.Default.Remove(KeyRememberMe);
        await Task.CompletedTask;
    }

    private static string FriendlyAuthError(string error)
    {
        if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            return PhoneHelper.FriendlyDuplicateMessage;

        if (error.Contains("websocket", StringComparison.OrdinalIgnoreCase))
            return "Account setup completed, but the app could not start live updates. Please login to continue.";

        return error;
    }
}
