namespace EgyptTax.Domain.Signatures;

/// <summary>
/// v3 §11 #8 (eSignature) — request the customer to acknowledge
/// receipt + agreement on a quotation/invoice. Token-based magic
/// link (similar to L5 customer portal): operator generates a
/// link, customer opens, types their full name + ticks the
/// agreement box, signature is recorded with timestamp + IP.
///
/// v1 ships acknowledgement-style signing (typed name +
/// timestamp). NOT cryptographic e-signature; doesn't claim
/// non-repudiation. Real DocuSign / qualified signature
/// integration lands when a deal explicitly requires it
/// (v4 trigger per §12).
/// </summary>
public sealed class SignatureRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Polymorphic — the document being signed.
    /// Currently used for Quotation and SalesInvoice; can extend.</summary>
    public SignatureDocumentType DocumentType { get; init; }
    public Guid DocumentId { get; init; }

    /// <summary>Random opaque token (43 base64url chars) embedded
    /// in the public sign URL: /sign/{token}.</summary>
    public string Token { get; init; } = "";

    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? SignedAtUtc { get; private set; }
    public string? SignerName { get; private set; }
    public string? SignerIp { get; private set; }
    public bool Revoked { get; private set; }

    private SignatureRequest() { }

    public SignatureRequest(
        SignatureDocumentType documentType,
        Guid documentId,
        string token,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        Guid? createdByUserId)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("DocumentId required.", nameof(documentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Expiry must be after creation.", nameof(expiresAtUtc));
        DocumentType = documentType;
        DocumentId = documentId;
        Token = token;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByUserId = createdByUserId;
    }

    public bool IsValid(DateTime nowUtc) =>
        !Revoked && SignedAtUtc is null && nowUtc < ExpiresAtUtc;

    public void RecordSignature(string signerName, string? signerIp, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signerName);
        if (Revoked) throw new InvalidOperationException("Signature request was revoked.");
        if (SignedAtUtc is not null) throw new InvalidOperationException("Already signed.");
        if (nowUtc >= ExpiresAtUtc) throw new InvalidOperationException("Signature link expired.");
        SignerName = signerName.Trim();
        SignerIp = signerIp;
        SignedAtUtc = nowUtc;
    }

    public void Revoke() => Revoked = true;
}

public enum SignatureDocumentType
{
    Quotation = 0,
    SalesInvoice = 1,
}
