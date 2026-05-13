namespace EgyptTax.Domain.Customers;

/// <summary>
/// L5 (v3 roadmap) — magic-link portal access for a customer.
/// Token-in-URL design: operator clicks "Generate portal link" on
/// a customer, system mints a cryptographically-random opaque
/// token + 30-day expiry, gives the operator the URL to send via
/// WhatsApp/email/copy-paste. Customer clicks → lands on their
/// own statement + invoice list. No password, no OTP, no separate
/// ASP.NET auth scheme.
///
/// Trade-off vs OTP:
///   • Pro: zero infrastructure dependency (works without SMTP),
///     zero friction for the customer (one click)
///   • Con: anyone with the URL can access until expiry (treat
///     the link like a one-time WhatsApp share, not a posted-on-
///     a-website thing)
///
/// Tokens are revocable: operator can call <see cref="Revoke"/>
/// to invalidate all outstanding tokens for a customer (e.g.
/// after a dispute) and generate a fresh one.
/// </summary>
public sealed class CustomerPortalAccess
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }

    /// <summary>Opaque base64-url-encoded random bytes. 32 bytes
    /// of entropy → 256 bits, infeasible to guess.</summary>
    public string Token { get; init; } = "";

    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastViewedAtUtc { get; private set; }
    public bool Revoked { get; private set; }

    /// <summary>Operator who generated the link. Null for older
    /// portal-access rows (pre-this-field).</summary>
    public Guid? CreatedByUserId { get; init; }

    private CustomerPortalAccess() { }

    public CustomerPortalAccess(
        Guid customerId,
        string token,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        Guid? createdByUserId)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId required.", nameof(customerId));
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Expiry must be after creation.", nameof(expiresAtUtc));

        CustomerId = customerId;
        Token = token;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public bool IsValid(DateTime nowUtc) => !Revoked && nowUtc < ExpiresAtUtc;

    public void RecordView(DateTime nowUtc) => LastViewedAtUtc = nowUtc;

    public void Revoke() => Revoked = true;

    public void Extend(DateTime newExpiry)
    {
        if (newExpiry <= ExpiresAtUtc)
            throw new ArgumentException("New expiry must be later than current.", nameof(newExpiry));
        ExpiresAtUtc = newExpiry;
    }
}
