using System.ComponentModel.DataAnnotations;
using System.Globalization;
using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Web.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Web.Pages.Auth;

/// <summary>
/// FR-002 — MFA enrollment. Per R-07 the page generates a fresh
/// 160-bit base32 TOTP secret on GET, displays the provisioning URI
/// for the operator to scan into their authenticator, and on POST
/// verifies a code against the secret before persisting it (encrypted
/// via <see cref="IMfaSecretProtector"/>) on the user row.
///
/// **Secret transport between GET and POST**: the secret is staged
/// inside an encrypted hidden form field — the
/// <see cref="IMfaSecretProtector"/> double-encrypts the same secret
/// the database row will eventually carry, so the transport copy is
/// no more sensitive than the persisted copy. Server-side cache /
/// TempData would also work but adds infrastructure for no security
/// benefit.
/// </summary>
[Authorize]
public sealed class EnrollMfaModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITotpService _totp;
    private readonly IMfaSecretProtector _protector;

    public EnrollMfaModel(AppDbContext db, ITotpService totp, IMfaSecretProtector protector)
    {
        _db = db;
        _totp = totp;
        _protector = protector;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();
    public string? ProvisioningUri { get; private set; }
    public string? Secret { get; private set; }
    public string? StagedSecretCipher { get; private set; }
    public string? ErrorMessage { get; private set; }

    private static bool IsArabic =>
        CultureInfo.CurrentCulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

    public sealed class InputModel
    {
        [Required, RegularExpression(@"^\d{6}$")]
        public string TotpCode { get; set; } = string.Empty;

        [Required]
        public string StagedSecretCipher { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await LoadCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Auth/Login");
        }

        if (user.MfaSecretEncrypted is not null)
        {
            // Already enrolled — no need to re-enroll. Send the user
            // back to login to authenticate fully with their TOTP.
            return RedirectToPage("/Auth/Login");
        }

        var freshSecret = _totp.GenerateSecret();
        Secret = freshSecret;
        ProvisioningUri = _totp.BuildProvisioningUri(user.Email, freshSecret, issuer: "EgyptTax");
        StagedSecretCipher = Convert.ToBase64String(_protector.Protect(freshSecret));
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await LoadCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Auth/Login");
        }

        // Recover the staged plaintext secret from the encrypted hidden
        // field. A user who tampered with the cipher would land in the
        // catch below.
        string secret;
        try
        {
            secret = _protector.Unprotect(Convert.FromBase64String(Input.StagedSecretCipher));
        }
        catch
        {
            ErrorMessage = IsArabic
                ? "انتهت جلسة تسجيل MFA. أعد تحميل الصفحة من فضلك."
                : "Enrollment session expired. Please reload the page.";
            return Page();
        }

        if (!_totp.VerifyCode(secret, Input.TotpCode))
        {
            // Re-show the page with the same staged secret so the user
            // can retry without losing their authenticator's anchor.
            ErrorMessage = IsArabic
                ? "رمز التحقق غير صحيح أو انتهت صلاحيته."
                : "The verification code is incorrect or expired.";
            ProvisioningUri = _totp.BuildProvisioningUri(user.Email, secret, issuer: "EgyptTax");
            Secret = secret;
            StagedSecretCipher = Input.StagedSecretCipher;
            return Page();
        }

        user.EnrollMfa(_protector.Protect(secret));
        await _db.SaveChangesAsync(cancellationToken);

        // Enrollment complete — send the user back to login for the
        // full TOTP-prompted sign-in (they will land at the home page
        // after that).
        return RedirectToPage("/Auth/Login");
    }

    private async Task<User?> LoadCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = User.UserId();
        if (userId is null)
        {
            return null;
        }
        return await _db.Set<User>()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
    }
}
