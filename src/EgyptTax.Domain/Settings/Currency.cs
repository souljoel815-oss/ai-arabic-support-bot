namespace EgyptTax.Domain.Settings;

/// <summary>
/// v3 §11 #2 / N-phase #7 — supported currency. EGP is always
/// present (seeded as base). Operator adds USD, EUR, etc as
/// needed. ExchangeRate rows reference these by Code.
///
/// v1 ships master-data + rate management only. Per-document
/// currency override on invoices + FX revaluation on period
/// close are deferred (touch every monetary calculation;
/// XL work). Customers who only deal in EGP see no change.
/// </summary>
public sealed class Currency
{
    /// <summary>ISO 4217 code (e.g. "EGP", "USD", "EUR").</summary>
    public string Code { get; init; } = "";
    public string Symbol { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string NameAr { get; init; } = "";
    public bool IsBase { get; init; }
    public bool IsActive { get; private set; } = true;

    private Currency() { }

    public Currency(string code, string symbol, string nameEn, string nameAr, bool isBase = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameEn);
        if (code.Length != 3) throw new ArgumentException("Code must be 3 chars (ISO 4217).", nameof(code));
        Code = code.ToUpperInvariant();
        Symbol = symbol ?? "";
        NameEn = nameEn;
        NameAr = nameAr ?? nameEn;
        IsBase = isBase;
    }

    public void Deactivate()
    {
        if (IsBase) throw new InvalidOperationException("Cannot deactivate the base currency.");
        IsActive = false;
    }

    public void Reactivate() => IsActive = true;
}

/// <summary>
/// Per-day rate from a foreign currency to the base (EGP).
/// One row per (currency, effective_date). Lookup picks the
/// latest effective_date ≤ document date.
/// </summary>
public sealed class ExchangeRate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    /// <summary>Foreign currency code; never base.</summary>
    public string CurrencyCode { get; init; } = "";
    public DateOnly EffectiveDate { get; init; }
    /// <summary>1 unit of CurrencyCode = RateToBase units of base (EGP).
    /// Example: USD → 50 means 1 USD = 50 EGP.</summary>
    public decimal RateToBase { get; private set; }
    public string? Source { get; init; }

    private ExchangeRate() { }

    public ExchangeRate(string currencyCode, DateOnly effectiveDate, decimal rateToBase, string? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rateToBase);
        CurrencyCode = currencyCode.ToUpperInvariant();
        EffectiveDate = effectiveDate;
        RateToBase = rateToBase;
        Source = source;
    }

    public void UpdateRate(decimal newRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newRate);
        RateToBase = newRate;
    }
}
