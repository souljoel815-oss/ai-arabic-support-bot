namespace EgyptTax.Application.Wht;

/// <summary>FR-046 / US7 — generate the Form 41 payload for a
/// (fiscalYear, quarter) pair. The handler aggregates supplier-
/// side outbound WhtCertificate rows whose date falls inside the
/// quarter, computes the WHT-payable account balance from the
/// general ledger, persists a Form41Filing row (status=Unfiled),
/// and returns the payload for downstream serialization.</summary>
public sealed record GenerateForm41Command(
    int FiscalYear,
    int Quarter,
    Guid PreparedByUserId);

/// <summary>Combined return of GenerateForm41Handler — both the
/// rich payload (for PDF rendering + JSON serialization) and the
/// persisted Form41Filing row's id (so downstream callers can
/// look it up for MarkFiled + the lifecycle dashboard).</summary>
public sealed record GenerateForm41Result(
    Guid Form41FilingId,
    Form41Payload Payload);
