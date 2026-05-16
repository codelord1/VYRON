using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Vyron.API.Models;

namespace Vyron.Admin.Helpers;

public static class FileUploadHelper
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxFileSizeBytes = 2 * 1024 * 1024; // 2 MB

    public static bool IsValidImageFile(IFormFile? file, out string? error)
    {
        error = null;
        if (file == null || file.Length == 0) { error = "No file selected."; return false; }
        if (file.Length > MaxFileSizeBytes) { error = "File exceeds 2 MB limit."; return false; }
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        { error = "Only .jpg, .jpeg, .png, and .webp files are allowed."; return false; }
        return true;
    }

    public static async Task<string> SaveStoreImageAsync(IFormFile file, IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.WebRootPath, "uploads", "stores");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, uniqueName);
        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/stores/{uniqueName}";
    }

    public static async Task<string> SaveProfileImageAsync(IFormFile file, IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, uniqueName);
        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/profiles/{uniqueName}";
    }

    public static void DeleteFile(string? relativePath, IWebHostEnvironment env)
    {
        if (string.IsNullOrEmpty(relativePath)) return;
        // prevent path traversal
        var safePath = relativePath.TrimStart('/').Replace("..", string.Empty);
        if (!safePath.StartsWith("uploads/")) return;
        var full = Path.Combine(env.WebRootPath, safePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) File.Delete(full);
    }

    // ─── Password hashing ──────────────────────────────────────────
    private static readonly PasswordHasher<User> _hasher = new();

    public static string HashPassword(User user, string password)
        => _hasher.HashPassword(user, password);

    public static bool VerifyPassword(User user, string hash, string provided)
        => _hasher.VerifyHashedPassword(user, hash, provided) != PasswordVerificationResult.Failed;

    // ─── Password reset tokens ─────────────────────────────────────
    public static (string raw, string hash) GenerateResetToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var hash = HashToken(raw);
        return (raw, hash);
    }

    public static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
