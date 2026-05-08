using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// B1 — the single-tenant company record. The MVP installation has
/// exactly one row; FR-005 mandates the fields required for legal
/// invoice rendering (issuer name, TIN, commercial registration,
/// address) plus operational config (fiscal-year start month,
/// default currency / language). The same row is also the issuer
/// block on every ETA eInvoice submission per the
/// <c>eta-einvoice.schema.json</c> contract.
/// </summary>
public sealed class Company
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ArabicEnglishText LegalName { get; private set; }
    public string TaxRegistrationNumber { get; private set; } = default!;
    public string CommercialRegistrationNumber { get; private set; } = default!;
    public PostalAddress Address { get; private set; }
    public string? LogoPath { get; private set; }
    public int FiscalYearStartMonth { get; private set; } = 1;
    public string DefaultCurrency { get; private set; } = "EGP";
    public Language DefaultLanguage { get; private set; } = Language.Ar;
    public string TaxpayerActivityCode { get; private set; } = default!;

    private Company() { }

    public Company(
        ArabicEnglishText legalName,
        EgyptianTin taxRegistrationNumber,
        string commercialRegistrationNumber,
        PostalAddress address,
        string taxpayerActivityCode,
        int fiscalYearStartMonth = 1,
        Language defaultLanguage = Language.Ar
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commercialRegistrationNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxpayerActivityCode);
        if (fiscalYearStartMonth is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fiscalYearStartMonth),
                "Fiscal-year start month must be between 1 and 12."
            );
        }

        LegalName = legalName;
        TaxRegistrationNumber = taxRegistrationNumber.Value;
        CommercialRegistrationNumber = commercialRegistrationNumber;
        Address = address;
        TaxpayerActivityCode = taxpayerActivityCode;
        FiscalYearStartMonth = fiscalYearStartMonth;
        DefaultLanguage = defaultLanguage;
    }

    public void UpdateAddress(PostalAddress address) => Address = address;

    public void UpdateLogo(string? logoPath) => LogoPath = logoPath;

    public void UpdateTaxpayerActivityCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        TaxpayerActivityCode = code;
    }
}
