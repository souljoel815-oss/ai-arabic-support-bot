namespace EgyptTax.Application.Verification;

/// <summary>
/// FR-044 / SC-013 — value object encoded in the QR seal embedded on
/// every posted sales-invoice / credit-note PDF. Per the
/// <c>verification-seal-qr.md</c> contract the on-the-wire form is
/// <c>EGT1.&lt;base64url(deterministic-cbor(payload))&gt;</c> with
/// CBOR map keys 1..8.
/// </summary>
public sealed record DocumentSealPayload(
    SealedDocumentType DocumentType,
    string DocumentNumber,
    Guid DocumentId,
    long GrandTotalPiastres,
    byte[] AuditEntryHash,
    long AuditEntryIndex,
    string VerifyUrl,
    string IssuerTin
);

public enum SealedDocumentType
{
    SalesInvoice = 1,
    CreditNote = 2,
}

/// <summary>
/// What the verifier discovered about the live database row, fed back
/// to <see cref="EgyptTax.Infrastructure.Verification.DocumentSealCodec.Verify"/>.
/// </summary>
public sealed record ResolvedDocument(
    string DocumentNumber,
    long GrandTotalPiastres,
    byte[] AuditEntryHash,
    long AuditEntryIndex
);

public sealed record SealVerificationResult(
    SealOutcome Outcome,
    string? DocumentNumber,
    string? DocumentType,
    IReadOnlyList<SealMismatch> Mismatches
);

public enum SealOutcome
{
    Valid,
    Tampered,
    Unknown,
    Malformed,
}

public sealed record SealMismatch(string Field, string Expected, string Actual);
