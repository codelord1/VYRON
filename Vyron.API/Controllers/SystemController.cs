using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vyron.API.Data;
using Vyron.API.Models;
using Vyron.API.Services;

namespace Vyron.API.Controllers;

/// <summary>
/// Internal system/diagnostic endpoints.
/// The test-email route is guarded so it cannot be used as an open public relay.
/// In Development: accessible with [AllowAnonymous] for easy local testing.
/// In Production:  requires Admin or SuperAdmin role.
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController : VyronController
{
    private readonly INotificationService _notifications;
    private readonly EmailSettings _emailCfg;
    private readonly IWebHostEnvironment _env;
    private readonly VyronDbContext _db;

    public SystemController(
        INotificationService notifications,
        IOptions<EmailSettings> emailOptions,
        IWebHostEnvironment env,
        VyronDbContext db)
    {
        _notifications = notifications;
        _emailCfg = emailOptions.Value;
        _env = env;
        _db = db;
    }

    /// <summary>
    /// Send a test email to verify SMTP is working.
    /// Development: no auth required.
    /// Production:  Admin / SuperAdmin only — must supply a valid JWT.
    /// </summary>
    [HttpPost("test-email")]
    [AllowAnonymous]           // auth is enforced by the environment gate below
    public async Task<IActionResult> TestEmail()
    {
        // ── Production guard ──────────────────────────────────────────
        if (!_env.IsDevelopment())
        {
            // In Production this endpoint requires Admin / SuperAdmin.
            if (!User.Identity?.IsAuthenticated == true)
                return Unauthorized(new { error = "Authentication required in Production." });

            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            if (role is not ("Admin" or "SuperAdmin"))
                return Forbid();
        }

        // ── Config check ──────────────────────────────────────────────
        if (!_emailCfg.Enabled)
            return Ok(new
            {
                success = false,
                message = "Email is disabled (Email:Enabled=false).",
                provider = _emailCfg.Provider,
                recipient = (string?)null,
                timestamp = DateTime.UtcNow
            });

        if (string.IsNullOrWhiteSpace(_emailCfg.Password))
            return Ok(new
            {
                success = false,
                message = "Email:Password is not set. Use user-secrets (Dev) or Email__Password env var (Prod).",
                provider = _emailCfg.Provider,
                recipient = (string?)null,
                timestamp = DateTime.UtcNow
            });

        // ── Send ──────────────────────────────────────────────────────
        const string recipient = "codeboxtechnologies@gmail.com";
        const string subject   = "VYRON Email Test";
        const string html      = """
            <h2>VYRON Email Test</h2>
            <p>VYRON email sending is working.</p>
            <p>Sent from Vyron.API.</p>
            """;

        var sentBefore = DateTime.UtcNow.AddSeconds(-2);
        await _notifications.SendEmailAsync(recipient, subject, html);

        // Read the CommunicationLog written by SendEmailAsync to confirm outcome
        var log = await _db.CommunicationLogs
            .AsNoTracking()
            .Where(l => l.RecipientEmail == recipient
                     && l.Channel == "Email"
                     && l.CreatedAt >= sentBefore)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        var ok  = log?.Status == "Sent";
        var msg = log?.Status switch
        {
            "Sent"    => "Test email sent successfully. Check codeboxtechnologies@gmail.com inbox.",
            "Failed"  => $"SMTP send failed: {log?.ErrorMessage}",
            "Skipped" => $"Email skipped: {log?.ErrorMessage}",
            _         => "Send attempted but outcome unknown (no log found)."
        };

        return Ok(new
        {
            success   = ok,
            message   = msg,
            provider  = _emailCfg.Provider,
            recipient,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>Returns a safe summary of the email configuration (no password).</summary>
    [HttpGet("email-config")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public IActionResult EmailConfig() => Ok(new
    {
        enabled      = _emailCfg.Enabled,
        provider     = _emailCfg.Provider,
        host         = _emailCfg.Host,
        port         = _emailCfg.Port,
        useStartTls  = _emailCfg.UseStartTls,
        useSsl       = _emailCfg.UseSsl,
        username     = _emailCfg.Username,
        fromEmail    = _emailCfg.FromEmail,
        fromName     = _emailCfg.FromName,
        passwordSet  = !string.IsNullOrWhiteSpace(_emailCfg.Password),
        fullyConfigured = _emailCfg.IsFullyConfigured
    });
}
