namespace EgyptTax.Application.Wht;

/// <summary>
/// FR-046 / US7 / T209 — structured payload for Form 41 (نموذج 41).
/// Schema mirror of <c>contracts/form41.schema.json</c> (T199
/// contract test pins it). Serialised via
/// <see cref="WhtCertificateJsonSerializer"/> options for byte-
/// identical shape to the WHT certificate JSON; the FiscalYear
/// + Quarter pair is the natural key.
///
/// Both the regulator-facing PDF and the JSON exported for
/// downstream tooling derive from this same payload — no
/// per-channel drift. When ETA publishes the official ingest
/// shape, this document remaps to it without changing the
/// generator handler.
/// </summary>
public sealed record Form41Payload(
    Form41Header FilingHeader,
    IReadOnlyList<Form41Line> Lines,
    Form41Totals Totals,
    Form41Reconciliation Reconciliation,
    Form41AuditChainExtractRef AuditChainExtractRef
);

public sealed record Form41Header(
    string CompanyTin,
    BilingualText CompanyName,
    int FiscalYear,
    int Quarter,
    DateOnly FillingPeriodStart,
    DateOnly FillingPeriodEnd,
    DateTime PreparedAt,
    Guid PreparedByUserId
);

public sealed record Form41Line(
    string SupplierTin,
    BilingualText SupplierName,
    string WhtCategoryCode,
    decimal RateAppliedPercent,
    decimal GrossPaymentTotal,
    decimal AmountWithheld,
    string SupplierPaymentVoucherNumber,
    DateOnly SupplierPaymentVoucherDate,
    string SourceInvoiceNumber,
    string? OutboundCertificateNumber
);

public sealed record Form41Totals(
    int LineCount,
    decimal TotalGrossPayment,
    decimal TotalAmountWithheld,
    IReadOnlyList<Form41ByCategoryRow> ByCategory
);

public sealed record Form41ByCategoryRow(
    string WhtCategoryCode,
    int LineCount,
    decimal AmountWithheld
);

public sealed record Form41Reconciliation(
    decimal WhtPayableAccountBalanceAtPeriodEnd,
    bool MatchesTotalAmountWithheld,
    decimal? DiscrepancyAmount
);

public sealed record Form41AuditChainExtractRef(
    long StartIndex,
    long EndIndex,
    string ExtractSha256
);
