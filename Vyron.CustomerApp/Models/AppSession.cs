using Vyron.CustomerApp.DTOs;

namespace Vyron.CustomerApp.Models;

/// <summary>
/// Singleton in-memory session state — current logged-in user + token.
/// Token is persisted in SecureStorage; this object holds the deserialized state.
/// </summary>
public class AppSession
{
    private static readonly AppSession _instance = new();
    public static AppSession Current => _instance;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime TokenExpiresAt { get; private set; }
    public UserDto? User { get; private set; }

    public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken)
        && TokenExpiresAt > DateTime.UtcNow.AddMinutes(1);

    public void SetAuth(AuthResponse auth)
    {
        AccessToken = auth.AccessToken;
        RefreshToken = auth.RefreshToken;
        TokenExpiresAt = auth.ExpiresAt;
        User = auth.User;
    }

    public void UpdateUser(UserDto user) => User = user;

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        TokenExpiresAt = default;
        User = null;
    }
}
