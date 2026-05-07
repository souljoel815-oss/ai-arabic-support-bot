using System.Security.Claims;

namespace EgyptTax.Web.Auth;

/// <summary>
/// Canonical claim type names used in the cookie principal. The
/// <see cref="AuthStage"/> claim distinguishes the two-step login
/// state — `password-verified` (only allowed to access force-change-
/// password / MFA enrollment / MFA verify) vs `fully-authenticated`
/// (everywhere else); the policy gating on top of <see cref="Authorize"/>
/// flips the access scope based on it.
/// </summary>
public static class AuthClaims
{
    public const string SessionId = "egypttax:session-id";
    public const string AuthStage = "egypttax:auth-stage";

    public const string StagePasswordVerified = "password-verified";
    public const string StageFullyAuthenticated = "fully-authenticated";

    public static Guid? UserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static Guid? SessionIdValue(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst(SessionId)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string? Stage(this ClaimsPrincipal principal) =>
        principal.FindFirst(AuthStage)?.Value;

    public static bool IsFullyAuthenticated(this ClaimsPrincipal principal) =>
        principal.Stage() == StageFullyAuthenticated;
}
