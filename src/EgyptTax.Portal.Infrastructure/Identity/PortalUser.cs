using Microsoft.AspNetCore.Identity;

namespace EgyptTax.Portal.Infrastructure.Identity;

/// <summary>
/// T018 per data-model.md §2 + FR-032. The portal's Team Member identity,
/// completely separate from the on-prem product's user store (no SSO,
/// no cross-system password sync). Backed by AspNetCore.Identity's
/// PBKDF2 password hasher; TOTP MFA via <see cref="LocalePreference"/>'s
/// sibling <see cref="TwoFactorEnabled"/> flag (handled by Identity itself).
/// </summary>
public class PortalUser : IdentityUser<Guid>
{
    /// <summary><c>ar-EG</c> (default) or <c>en-US</c>. Drives locale routing + email template language.</summary>
    public string LocalePreference { get; set; } = "ar-EG";

    /// <summary>"Ahmed Hassan" — surfaces in support-ticket threads + audit log.</summary>
    public string? DisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>FR-024 — set when the user requests account deletion. Nightly purge job hard-deletes 30 days later.</summary>
    public DateTime? SoftDeletedAtUtc { get; set; }
}
