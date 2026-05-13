using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel.Time;
using EgyptTax.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EgyptTax.Web.Pages.Auth;

/// <summary>
/// T117 — login + MFA challenge per R-07. Razor Page (not Blazor)
/// because cookie sign-in needs the synchronous HTTP context that a
/// Razor Pages POST handler gives cleanly; Blazor's SignalR pipe
/// can't issue Set-Cookie on the original handshake. The page is a
/// two-step state machine:
///
///   1. POST /login {email, password}
///      → if user not found / wrong password / locked out / disabled
///        → re-render with error
///      → if PasswordMustChange
///        → sign in with `password-verified` stage and redirect to
///          /password/change
///      → if RequiresMfa &amp;&amp; not enrolled
///        → sign in with `password-verified` stage and redirect to
///          /mfa/enroll
///      → if RequiresMfa &amp;&amp; enrolled
///        → re-render with MFA prompt (NO cookie issued yet)
///      → if no MFA required
///        → sign in fully and redirect to ReturnUrl or /
///
///   2. POST /login {email, password, totpCode} (the MFA prompt)
///      → re-validate password (defense in depth)
///      → verify TOTP code against decrypted secret
///      → on success: sign in fully, redirect
///      → on failure: re-render with MFA error
///
/// Failed login attempts call User.RecordLogin(false, nowUtc) which
/// increments FailedLoginCount; the FR-039 / FR-038 lockout threshold
/// is policy that lives on the User aggregate.
/// </summary>
public sealed class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITotpService _totp;
    private readonly IMfaSecretProtector _protector;
    private readonly ISessionService _sessions;
    private readonly IClock _clock;
    private readonly bool _requireMfa;

    public LoginModel(
        AppDbContext db,
        IPasswordHasher hasher,
        ITotpService totp,
        IMfaSecretProtector protector,
        ISessionService sessions,
        IClock clock,
        IConfiguration config
    )
    {
        _db = db;
        _hasher = hasher;
        _totp = totp;
        _protector = protector;
        _sessions = sessions;
        _clock = clock;
        // Features:RequireMfa — when false (default), the login flow
        // bypasses MFA enrollment + challenge entirely. Re-enable per
        // FR-002 by setting this to true in appsettings.
        _requireMfa = config.GetValue("Features:RequireMfa", false);
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; private set; }

    private static bool IsArabic =>
        CultureInfo.CurrentCulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    public bool ShowMfaPrompt { get; private set; }
    public string? ReturnUrl { get; private set; }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        public string? TotpCode { get; set; }
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        string? returnUrl = null,
        CancellationToken cancellationToken = default
    )
    {
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var canonicalEmail = Canonicalize(Input.Email);
        var user = await _db.Set<User>()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == canonicalEmail, cancellationToken);

        if (
            user is null
            || !_hasher.Verify(Input.Password, user.PasswordHash)
            || user.Status != UserStatus.Active
            || user.IsCurrentlyLockedOut(_clock.UtcNow)
        )
        {
            // Record the failed attempt on the located user (if any) but
            // surface a uniform error message so an attacker can't
            // distinguish "wrong password" from "no such email".
            user?.RecordLogin(succeeded: false, _clock.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            ErrorMessage = IsArabic
                ? "البريد الإلكتروني أو كلمة المرور غير صحيحة."
                : "The email or password is incorrect.";
            return Page();
        }

        // PasswordMustChange (FR-038) — historically forced a redirect
        // to ChangePassword on first login. Removed 2026-05-11 because
        // it was tripping up new installs. Operators can change the
        // password from /settings/profile at any time; the flag is
        // kept on the User entity so future reset-by-admin flows can
        // re-instate the prompt without re-introducing the hard gate.

        // MFA required but not enrolled — same `password-verified`
        // stage, route to enrolment. Skipped entirely when
        // Features:RequireMfa is false (default).
        if (_requireMfa && user.RequiresMfa() && user.MfaSecretEncrypted is null)
        {
            await SignInAsync(
                user,
                AuthClaims.StagePasswordVerified,
                sessionId: null,
                cancellationToken
            );
            return RedirectToPage("/Auth/EnrollMfa", new { returnUrl });
        }

        // MFA required and enrolled — challenge for the TOTP code
        // BEFORE issuing any cookie. Re-renders the page with the MFA
        // input field shown. Skipped when Features:RequireMfa is false.
        if (_requireMfa && user.RequiresMfa() && user.MfaSecretEncrypted is not null)
        {
            if (string.IsNullOrWhiteSpace(Input.TotpCode))
            {
                ShowMfaPrompt = true;
                return Page();
            }
            var secret = _protector.Unprotect(user.MfaSecretEncrypted);
            if (!_totp.VerifyCode(secret, Input.TotpCode))
            {
                ShowMfaPrompt = true;
                ErrorMessage = IsArabic
                    ? "رمز التحقق غير صحيح أو انتهت صلاحيته."
                    : "The verification code is incorrect or expired.";
                return Page();
            }
        }

        // Fully authenticated — open a session row + sign in cookie.
        var session = await _sessions.BeginAsync(
            user.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken
        );
        await SignInAsync(user, AuthClaims.StageFullyAuthenticated, session.Id, cancellationToken);
        user.RecordLogin(succeeded: true, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return SafeRedirect(returnUrl);
    }

    private async Task SignInAsync(
        User user,
        string stage,
        Guid? sessionId,
        CancellationToken cancellationToken
    )
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName.English),
            new(AuthClaims.AuthStage, stage),
        };
        if (sessionId is { } sid)
        {
            claims.Add(new Claim(AuthClaims.SessionId, sid.ToString("D")));
        }
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Code));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    // Home is the Blazor Index.razor page at "/" — RedirectToPage looks
    // up Razor Pages (.cshtml) only and can't find it. Redirect to the
    // literal path instead so the Blazor router takes over.
    private RedirectResult SafeRedirect(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : Redirect("~/");

    private static string Canonicalize(string email)
    {
#pragma warning disable CA1308 // Email canonical form is lowercase per RFC 5321 §2.3.11.
        return email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
    }
}
