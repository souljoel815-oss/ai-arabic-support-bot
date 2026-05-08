using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
using EgyptTax.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EgyptTax.Web.Pages.Auth;

/// <summary>
/// Sign-out endpoint. Revokes the server-side session row first
/// (FR-039 — administrator-revocable sessions), then clears the
/// cookie. Order matters: if SignOutAsync ran first the cookie would
/// be gone before we could read its session-id claim.
/// </summary>
[Authorize]
public sealed class LogoutModel : PageModel
{
    private readonly ISessionService _sessions;

    public LogoutModel(ISessionService sessions)
    {
        _sessions = sessions;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await SignOutCoreAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        return await SignOutCoreAsync(cancellationToken);
    }

    private async Task<IActionResult> SignOutCoreAsync(CancellationToken cancellationToken)
    {
        if (User.SessionIdValue() is { } sessionId)
        {
            await _sessions.RevokeAsync(
                sessionId,
                SessionRevocationReason.UserLogout,
                cancellationToken
            );
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Auth/Login");
    }
}
