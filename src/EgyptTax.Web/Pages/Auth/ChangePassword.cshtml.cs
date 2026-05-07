using System.ComponentModel.DataAnnotations;
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
/// FR-038 — force-change-password page. The login flow lands here
/// when <c>User.PasswordMustChange == true</c>; the page is also
/// reachable from a navigation link for voluntary password change.
/// Either path requires the cookie principal to be at least
/// <c>password-verified</c> stage; the page never trusts the
/// supplied user identity beyond what the cookie carries.
/// </summary>
[Authorize]
public sealed class ChangePasswordModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;

    public ChangePasswordModel(AppDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ErrorMessage { get; private set; }

    public sealed class InputModel
    {
        [Required, DataType(DataType.Password), MinLength(12), MaxLength(128)]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = User.UserId();
        if (userId is null)
        {
            return RedirectToPage("/Auth/Login");
        }

        var user = await _db.Set<User>()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
        if (user is null)
        {
            return RedirectToPage("/Auth/Login");
        }

        // Reject the new password if it is identical to the current
        // (catches the user who just typed the bootstrap value back in).
        if (_hasher.Verify(Input.NewPassword, user.PasswordHash))
        {
            ErrorMessage = "New password must differ from the current one.";
            return Page();
        }

        user.SetPassword(_hasher.Hash(Input.NewPassword), mustChange: false);
        await _db.SaveChangesAsync(cancellationToken);

        // After force-change, route per the next required step:
        //   * If MFA is required and not enrolled → /mfa/enroll
        //   * Otherwise back to login so the user authenticates
        //     fully with the new password (the cookie is still at
        //     password-verified stage; we don't auto-promote here).
        if (user.RequiresMfa() && user.MfaSecretEncrypted is null)
        {
            return RedirectToPage("/Auth/EnrollMfa", new { returnUrl });
        }

        return RedirectToPage("/Auth/Login", new { returnUrl });
    }
}
