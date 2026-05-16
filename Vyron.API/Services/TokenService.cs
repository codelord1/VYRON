using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Vyron.API.Models;

namespace Vyron.API.Services;

public interface ITokenService
{
    /// <summary>
    /// Generates a JWT. Role claims come from <paramref name="roleNames"/> when provided
    /// (loaded from UserRoles table); otherwise falls back to user.Role.ToString().
    /// </summary>
    string GenerateAccessToken(User user, IEnumerable<string>? roleNames = null);
    string GenerateRefreshToken();
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    public TokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(User user, IEnumerable<string>? roleNames = null)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Role claims: prefer UserRoles table; fall back to User.Role column.
        var roles = roleNames?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList();
        var roleClaims = (roles is { Count: > 0 })
            ? roles.Select(r => new Claim(ClaimTypes.Role, r))
            : new[] { new Claim(ClaimTypes.Role, user.Role.ToString()) };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.MobilePhone,    user.Phone),
            new Claim(ClaimTypes.Name,           user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roleClaims);

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
