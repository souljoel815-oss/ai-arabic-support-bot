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

/// <summary>FR-046 / T210 / US7 scenario 3 — transition a
/// generated Form 41 from Unfiled → Filed. Two guards fire:
///   * The filing's reconciliation MUST be clean (cert total =
///     WHT-payable account accrual). A dirty filing can't be
///     marked Filed — the operator fixes the books first (likely
///     a missing manual adjusting JV) then re-generates.
///   * The Form41Filing entity refuses second-call (one canonical
///     filing per quarter per FR-046).
/// On success, every WhtCertificate in the filing's quarter is
/// stamped with <c>IncludedInForm41FilingId</c> so it can never
/// appear in another filing (T200 immutability).</summary>
public sealed record MarkForm41FiledCommand(
    Guid Form41FilingId,
    Guid FiledByUserId);
