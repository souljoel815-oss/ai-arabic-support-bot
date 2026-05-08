namespace EgyptTax.Application.Wht;

/// <summary>
/// FR-045 / US7 / T207 — structured payload that drives BOTH the
/// PDF renderer + the regulator-shaped JSON. Schema mirror of
/// <c>contracts/wht-certificate.schema.json</c> (T198 contract
/// test pins it). Property names lowercase-camel via the default
/// JsonNamingPolicy so `IssuerCompanyName` serialises as
/// `issuerCompanyName` per the schema.
/// </summary>
public sealed record WhtCertificatePayload(
    string CertificateNumber,
    string Direction, // "OutboundToSupplier" | "InboundFromCustomer"
    DateTime IssuedAt,
    string IssuerCompanyTin,
    BilingualText IssuerCompanyName,
    string CounterpartyTin,
    BilingualText CounterpartyName,
    string SourceInvoiceNumber,
    DateOnly SourceInvoiceDate,
    string SourceVoucherNumber,
    DateOnly SourceVoucherDate,
    string WhtCategoryCode,
    BilingualText WhtCategoryName,
    decimal RateAppliedPercent,
    decimal AmountWithheld,
    decimal? GrossPayment,
    decimal? NetPayment,
    string Currency, // const "EGP"
    string Language, // const "ar+en"
    string? AuditChainHash
);

/// <summary>Schema-shaped { ar, en } pair. Distinct from
/// <c>ArabicEnglishText</c> because the schema needs the lowercase
/// property names; the value-object struct from SharedKernel
/// serialises with PascalCase.</summary>
public sealed record BilingualText(string Ar, string En);
