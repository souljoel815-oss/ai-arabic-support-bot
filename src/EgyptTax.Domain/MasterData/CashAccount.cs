using EgyptTax.SharedKernel;

namespace EgyptTax.Domain.MasterData;

/// <summary>
/// P3.1 — a specific cashbox or bank account the company holds money
/// in. Replaces the single hardcoded <c>"1100"</c> Cash account on
/// payment vouchers — real businesses have multiple cashboxes per
/// branch and multiple bank accounts (CIB, NBE, USD, EGP).
///
/// The <see cref="AccountCode"/> follows the conventional Egyptian
/// chart-of-accounts pattern of <c>1100.{n}</c> for cashboxes and
/// <c>1100.B{n}</c> for bank accounts, but the operator owns the
/// code so they can map to whatever scheme their existing books use.
///
/// We keep history-bearing rows immutable: deactivation marks
/// <see cref="Status"/> = Inactive (still selectable on historical
/// drill-down but hidden from the new-voucher dropdown). Hard delete
/// is refused once the account has any voucher history.
/// </summary>
public sealed class CashAccount
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AccountCode { get; private set; } = default!;
    public ArabicEnglishText Name { get; private set; }
    public CashAccountKind Kind { get; private set; }
    public string Currency { get; private set; } = "EGP";

    /// <summary>Bank metadata — populated only when <see cref="Kind"/> is Bank.</summary>
    public string? BankName { get; private set; }
    public string? AccountNumber { get; private set; }
    public string? BranchName { get; private set; }
    public string? IbanOrSwift { get; private set; }

    public CashAccountStatus Status { get; private set; } = CashAccountStatus.Active;
    public bool IsDefault { get; private set; }

    private CashAccount() { }

    public CashAccount(
        string accountCode,
        ArabicEnglishText name,
        CashAccountKind kind,
        string currency = "EGP",
        bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        AccountCode = accountCode.Trim();
        Name = name;
        Kind = kind;
        Currency = currency.Trim().ToUpperInvariant();
        IsDefault = isDefault;
    }

    public void UpdateBankMetadata(
        string? bankName,
        string? accountNumber,
        string? branchName,
        string? ibanOrSwift)
    {
        if (Kind != CashAccountKind.Bank)
        {
            throw new InvalidOperationException(
                "Bank metadata can only be set on Bank-kind accounts.");
        }
        BankName = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
        AccountNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim();
        BranchName = string.IsNullOrWhiteSpace(branchName) ? null : branchName.Trim();
        IbanOrSwift = string.IsNullOrWhiteSpace(ibanOrSwift) ? null : ibanOrSwift.Trim();
    }

    public void Rename(ArabicEnglishText newName) => Name = newName;

    public void Deactivate()  => Status = CashAccountStatus.Inactive;
    public void Reactivate()  => Status = CashAccountStatus.Active;

    /// <summary>
    /// Mark this account as the default the system picks when an
    /// operator doesn't specify one (e.g., on legacy voucher imports).
    /// Caller is responsible for un-defaulting any previous default.
    /// </summary>
    public void MarkDefault()    => IsDefault = true;
    public void UnmarkDefault()  => IsDefault = false;
}

public enum CashAccountKind
{
    /// <summary>Physical cashbox (per-branch petty cash).</summary>
    Cash,
    /// <summary>Bank account (current, savings, USD/EGP, etc).</summary>
    Bank,
}

public enum CashAccountStatus
{
    Active,
    Inactive,
}
