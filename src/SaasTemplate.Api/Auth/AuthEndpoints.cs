using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SaasTemplate.Api.Auditing;
using SaasTemplate.Api.Data;
using SaasTemplate.Api.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace SaasTemplate.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> userManager, ITokenService tokenService, IEmailService emailService, AppSettings appSettings, IAuditLogger audit, ILogger<Program> logger, CancellationToken ct) =>
        {
            var existing = await userManager.FindByEmailAsync(request.Email);
            if (existing is not null)
                return Results.Conflict(new { error = "An account with this email already exists." });

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

            await audit.LogAsync(AuditAction.UserRegistered, user.Id, user.Email, cancellationToken: ct);

            // Send an email-verification link (best-effort — a mail failure must not fail signup).
            try
            {
                var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var verifyUrl = $"{appSettings.BaseUrl}/auth/verify-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(confirmToken)}";
                await emailService.SendEmailVerificationAsync(user.Email!, verifyUrl, ct);
                await audit.LogAsync(AuditAction.EmailVerificationSent, user.Id, user.Email, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send verification email during registration for {Email}", user.Email);
            }

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new AuthResponse { Token = token, Email = user.Email! });
        }).RequireRateLimiting("auth");

        auth.MapPost("/login", async (LoginRequest request, UserManager<ApplicationUser> userManager, ITokenService tokenService, AppDbContext db, IAuditLogger audit) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            // SA-003: check lockout before validating password to prevent timing oracle
            if (user is not null && await userManager.IsLockedOutAsync(user))
            {
                await audit.LogAsync(AuditAction.LoginFailed, user.Id, request.Email, metadata: new { reason = "locked_out" });
                return Results.Json(new { error = "Account temporarily locked. Try again later." }, statusCode: 429);
            }

            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                if (user is not null)
                    await userManager.AccessFailedAsync(user);
                await audit.LogAsync(AuditAction.LoginFailed, user?.Id, request.Email, metadata: new { reason = "invalid_credentials" });
                return Results.Unauthorized();
            }

            await userManager.ResetAccessFailedCountAsync(user);
            await audit.LogAsync(AuditAction.LoginSucceeded, user.Id, user.Email);
            var token = tokenService.GenerateToken(user);
            var hasSub = await db.Subscriptions.AnyAsync(s => s.UserId == user.Id &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing));
            return Results.Ok(new AuthResponse { Token = token, Email = user.Email!, HasActiveSubscription = hasSub });
        }).RequireRateLimiting("auth");

        auth.MapPost("/magic-link", async (MagicLinkRequest request, UserManager<ApplicationUser> userManager, IEmailService emailService, AppSettings appSettings, ILogger<Program> logger, CancellationToken ct) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            // Auto-create account for new users (magic link doubles as sign-up)
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = request.Email,
                    Email = request.Email
                };
                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    logger.LogWarning("Magic link auto-create failed for {Email}: {Errors}",
                        request.Email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    // Return same message to avoid email enumeration
                    return Results.Ok(new { message = "If this email is valid, a sign-in link has been sent." });
                }
                logger.LogInformation("Auto-created account via magic link for {Email}", request.Email);
            }

            var token = await userManager.GenerateUserTokenAsync(user, "Default", "magic-link");

            var magicLinkUrl = $"{appSettings.BaseUrl}/auth/verify?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

            try
            {
                await emailService.SendMagicLinkAsync(user.Email!, magicLinkUrl, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send magic link email to {Email}", user.Email);
                return Results.Problem("Unable to send sign-in email. Please try again later.", statusCode: 503);
            }

            return Results.Ok(new { message = "If this email is valid, a sign-in link has been sent." });
        }).RequireRateLimiting("auth-magic-link");

        auth.MapPost("/magic-link/verify", async (MagicLinkVerifyRequest request, UserManager<ApplicationUser> userManager, ITokenService tokenService, AppDbContext db) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
                return Results.Unauthorized();

            var valid = await userManager.VerifyUserTokenAsync(user, "Default", "magic-link", request.Token);
            if (!valid)
                return Results.Unauthorized();

            // Invalidate the token so it can't be reused (rotates the security stamp)
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
                return Results.Problem("Sign-in failed. Please request a new link.", statusCode: 500);

            var jwt = tokenService.GenerateToken(user);
            var hasSub = await db.Subscriptions.AnyAsync(s => s.UserId == user.Id &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing));
            return Results.Ok(new AuthResponse { Token = jwt, Email = user.Email!, HasActiveSubscription = hasSub });
        }).RequireRateLimiting("auth");

        // ----- Password reset -----

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest request, UserManager<ApplicationUser> userManager, IEmailService emailService, AppSettings appSettings, IAuditLogger audit, ILogger<Program> logger, CancellationToken ct) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is not null)
            {
                // Audit the request itself, independent of whether email delivery succeeds.
                await audit.LogAsync(AuditAction.PasswordResetRequested, user.Id, user.Email, cancellationToken: ct);

                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var resetUrl = $"{appSettings.BaseUrl}/auth/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
                try
                {
                    await emailService.SendPasswordResetAsync(user.Email!, resetUrl, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                }
            }

            // Always return the same response regardless of whether the account exists (anti-enumeration).
            return Results.Ok(new { message = "If an account exists for this email, a password reset link has been sent." });
        }).RequireRateLimiting("auth-magic-link");

        auth.MapPost("/reset-password", async (ResetPasswordRequest request, UserManager<ApplicationUser> userManager, IAuditLogger audit) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            // Collapse unknown-user and bad-token into one generic message — don't reveal which failed.
            if (user is null)
                return Results.BadRequest(new { error = "Invalid or expired reset link." });

            var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
            {
                // Password-policy failures are safe (and helpful) to surface; token failures stay generic.
                var passwordErrors = result.Errors.Where(e => e.Code.StartsWith("Password")).ToList();
                if (passwordErrors.Count > 0)
                    return Results.ValidationProblem(passwordErrors.ToDictionary(e => e.Code, e => new[] { e.Description }));
                return Results.BadRequest(new { error = "Invalid or expired reset link." });
            }

            await audit.LogAsync(AuditAction.PasswordResetCompleted, user.Id, user.Email);
            return Results.Ok(new { message = "Your password has been reset. You can now sign in." });
        }).RequireRateLimiting("auth");

        // ----- Email verification -----

        auth.MapPost("/send-verification", async (SendVerificationRequest request, UserManager<ApplicationUser> userManager, IEmailService emailService, AppSettings appSettings, IAuditLogger audit, ILogger<Program> logger, CancellationToken ct) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is not null && !await userManager.IsEmailConfirmedAsync(user))
            {
                var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var verifyUrl = $"{appSettings.BaseUrl}/auth/verify-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
                try
                {
                    await emailService.SendEmailVerificationAsync(user.Email!, verifyUrl, ct);
                    await audit.LogAsync(AuditAction.EmailVerificationSent, user.Id, user.Email, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
                }
            }

            // Same response whether or not the email exists / still needs verifying (anti-enumeration).
            return Results.Ok(new { message = "If this email requires verification, a verification link has been sent." });
        }).RequireRateLimiting("auth-magic-link");

        auth.MapPost("/verify-email", async (VerifyEmailRequest request, UserManager<ApplicationUser> userManager, IAuditLogger audit) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
                return Results.BadRequest(new { error = "Invalid or expired verification link." });

            var result = await userManager.ConfirmEmailAsync(user, request.Token);
            if (!result.Succeeded)
                return Results.BadRequest(new { error = "Invalid or expired verification link." });

            await audit.LogAsync(AuditAction.EmailVerified, user.Id, user.Email);
            return Results.Ok(new { message = "Your email has been verified." });
        }).RequireRateLimiting("auth");

        // Google OAuth — initiate challenge
        auth.MapGet("/google", (HttpContext context) =>
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/auth/google/callback"
            };
            return Results.Challenge(properties, ["Google"]);
        });

        // Google OAuth — handle callback, find-or-create user, issue JWT, redirect to Blazor
        auth.MapGet("/google/callback", async (
            HttpContext context,
            UserManager<ApplicationUser> userManager,
            ITokenService tokenService,
            AppDbContext db,
            ILogger<Program> logger) =>
        {
            var result = await context.AuthenticateAsync("ExternalOAuth");
            if (!result.Succeeded)
            {
                logger.LogWarning("Google OAuth callback failed: {Failure}", result.Failure?.Message);
                return Results.Redirect("/login?error=oauth-failed");
            }

            var email = result.Principal?.FindFirstValue(ClaimTypes.Email);
            var name = result.Principal?.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(email))
            {
                logger.LogWarning("Google OAuth: no email claim in response");
                return Results.Redirect("/login?error=no-email");
            }

            // Find or create user
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = name,
                    EmailConfirmed = true // Google verified
                };
                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    logger.LogError("Google OAuth auto-create failed for {Email}: {Errors}",
                        email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return Results.Redirect("/login?error=create-failed");
                }
                logger.LogInformation("Auto-created account via Google OAuth for {Email}", email);
            }

            var jwt = tokenService.GenerateToken(user);

            // Clean up the external cookie
            await context.SignOutAsync("ExternalOAuth");

            // Set JWT in an HTTP-only cookie, then redirect to the dashboard.
            // Blazor components cannot reliably handle OAuth callbacks — the prerender/circuit
            // lifecycle discards session state. Using a server-side cookie that MainLayout reads
            // via HttpContext during prerender is the established pattern.
            context.Response.Cookies.Append("access_token", jwt, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(5), // Short-lived — MainLayout reads and transfers to session
                Path = "/"
            });

            var hasSub = await db.Subscriptions.AnyAsync(s => s.UserId == user.Id &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing));

            return Results.Redirect(hasSub ? "/" : "/billing");
        });
    }
}

public class RegisterRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    // SA-005: mirrors Identity password policy (12 chars minimum, special char required)
    [Required, MinLength(12)]
    public required string Password { get; init; }

    public string? FullName { get; init; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public class MagicLinkRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    public string? ReturnUrl { get; init; }
}

public class MagicLinkVerifyRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Token { get; init; }
}

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }
}

public class ResetPasswordRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Token { get; init; }

    // SA-005: mirrors Identity password policy (12 chars minimum, special char required)
    [Required, MinLength(12)]
    public required string NewPassword { get; init; }
}

public class SendVerificationRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }
}

public class VerifyEmailRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Token { get; init; }
}

public class AuthResponse
{
    public required string Token { get; init; }
    public required string Email { get; init; }
    public bool HasActiveSubscription { get; init; }
}

/// <summary>Request body for POST /api/account/marketing-consent</summary>
public class MarketingConsentRequest
{
    public bool Consent { get; init; }
}
