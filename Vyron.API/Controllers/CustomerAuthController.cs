using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Vyron.API.Data;
using Vyron.API.DTOs;
using Vyron.API.Services;

namespace Vyron.API.Controllers;

/// <summary>
/// Password-based login + OTP-gated signup and password-reset for customers.
/// Does NOT affect the existing /api/auth/ OTP flow used by Admin/Rider apps.
/// </summary>
[ApiController]
[Route("api/customer-auth")]
public class CustomerAuthController : VyronController
{
    private readonly ICustomerAuthService _auth;
    private readonly VyronDbContext _db;

    public CustomerAuthController(ICustomerAuthService auth, VyronDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    // ── LOGIN ──────────────────────────────────────────────────────
    /// <summary>Returning customer login: phone + password. No OTP.</summary>
    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] CustomerLoginRequest req)
    {
        var (response, error) = await _auth.LoginAsync(req);
        if (error != null) return BadRequest(new { Error = error });
        return Ok(response);
    }

    // ── SIGNUP FLOW ───────────────────────────────────────────────
    /// <summary>Step 1 of signup: send OTP to phone for verification.</summary>
    [HttpPost("request-signup-otp"), AllowAnonymous]
    public async Task<IActionResult> RequestSignupOtp([FromBody] SignupOtpRequest req)
    {
        var (success, message, devOtp) = await _auth.RequestSignupOtpAsync(req.Phone);
        if (!success) return BadRequest(new { Error = message });
        return Ok(new { Message = message, DevOtp = devOtp });
    }

    /// <summary>
    /// Step 2 of signup: provide OTP + profile details + password to create account.
    /// Returns a full auth token so the user is immediately logged in.
    /// </summary>
    [HttpPost("complete-profile"), AllowAnonymous]
    public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileRequest req)
    {
        var (response, error) = await _auth.CompleteProfileAsync(req);
        if (error != null) return BadRequest(new { Error = error });
        return Ok(response);
    }

    // ── FORGOT PASSWORD FLOW ──────────────────────────────────────
    /// <summary>Step 1 of password reset: send OTP to phone.</summary>
    [HttpPost("request-password-reset-otp"), AllowAnonymous]
    public async Task<IActionResult> RequestPasswordResetOtp([FromBody] PasswordResetOtpRequest req)
    {
        var (success, message, devOtp) = await _auth.RequestPasswordResetOtpAsync(req.Phone);
        if (!success) return BadRequest(new { Error = message });
        return Ok(new { Message = message, DevOtp = devOtp });
    }

    /// <summary>Step 2 of password reset: verify OTP + set new password.</summary>
    [HttpPost("reset-password"), AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var (success, error) = await _auth.ResetPasswordAsync(req);
        if (!success) return BadRequest(new { Error = error });
        return Ok(new { Message = "Password reset successfully. You can now login with your new password." });
    }

    // ── CURRENT USER ──────────────────────────────────────────────
    /// <summary>Returns the currently authenticated user's profile.</summary>
    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
        if (userId == Guid.Empty) return Unauthorized();

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return NotFound();

        return Ok(new UserDto(user.Id, user.Phone, user.FullName, user.Email, user.Role, user.CreatedAt));
    }
}
