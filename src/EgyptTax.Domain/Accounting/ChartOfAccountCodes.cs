namespace EgyptTax.Domain.Accounting;

/// <summary>
/// T095 — minimal slice of the chart of accounts the post-time
/// emitter needs. The full CoA seed lands in US4
/// (JournalVoucher / GL); these three constants are the only
/// accounts the SalesInvoice journal posts touch:
///
/// * <see cref="AccountsReceivable"/> — debited (credit-note: credited)
///   with the grand total. Tracks what the customer owes.
/// * <see cref="SalesRevenue"/>       — credited (credit-note: debited)
///   with the net-of-discount subtotal. Recognised revenue.
/// * <see cref="OutputVatPayable"/>   — credited (credit-note: debited)
///   with the VAT total. Liability owed to ETA.
///
/// Codes follow the conventional Egyptian SME chart (1xxx assets,
/// 2xxx liabilities, 4xxx revenue) so the demo books reconcile to
/// what an accountant expects to see.
/// </summary>
public static class ChartOfAccountCodes
{
    public const string AccountsReceivable = "1200";
    public const string SalesRevenue = "4000";
    public const string OutputVatPayable = "2110";

    /// <summary>FR-016 / FR-020 — input-VAT recoverable on the
    /// deductible portion of purchase invoices. Asset; reduces the
    /// VAT payable to ETA at filing time.</summary>
    public const string InputVatRecoverable = "1110";

    /// <summary>FR-009 / FR-016 — generic accounts-payable account
    /// posted on purchase invoices + expenses. Liability.</summary>
    public const string AccountsPayable = "2100";

    /// <summary>FR-009 / FR-014 — generic expense account used by
    /// the auto-emitter when the source purchase / expense doesn't
    /// route to a more specific account. Operators can re-classify
    /// via a manual adjusting voucher (FR-031) post-fact; the auto-
    /// emit's job is just to keep the books balanced from minute
    /// one.</summary>
    public const string GenericExpense = "5200";

    /// <summary>T186 / FR-017 / US6 — depreciation-expense account
    /// debited by the monthly depreciation Hangfire job
    /// (RunMonthlyDepreciation). Distinct from GenericExpense so the
    /// trial balance + journal-listing surface depreciation
    /// separately from operator-entered expenses.</summary>
    public const string DepreciationExpense = "5100";

    /// <summary>FR-017 / US6 — accumulated-depreciation contra-asset
    /// account credited by the monthly depreciation job. Liability-
    /// style sign convention even though it lives in the asset block;
    /// the GL nets it against the gross fixed-asset balance to
    /// produce the FR-018 net book value visible on reports.</summary>
    public const string AccumulatedDepreciation = "1290";

    /// <summary>Phase 9 / Round 5 — operating cash account. Credited
    /// when a SupplierPaymentVoucher posts (cash leg paid to the
    /// supplier); debited when a CustomerReceiptVoucher posts (cash
    /// received). Single seeded cash account at MVP scope; multi-
    /// account cash management is Near-term.</summary>
    public const string Cash = "1100";

    /// <summary>FR-051 / US7 — withholding-tax payable to the tax
    /// authority on supplier-services payments. Credited when a
    /// SupplierPaymentVoucher with WHT is posted; cleared when the
    /// company remits the WHT liability to ETA.</summary>
    public const string WhtPayable = "2120";

    /// <summary>FR-052 / US7 — withholding-tax receivable when a
    /// customer withholds tax from a payment to the company.
    /// Debited when the CustomerReceiptVoucher carries a customer-
    /// issued WHT certificate; cleared when ETA refunds / offsets.</summary>
    public const string WhtReceivable = "1120";

    /// <summary>v5 E.1 — current liability holding down-payments a
    /// customer has paid against a future / draft sales invoice
    /// (construction, custom manufacturing, professional-services
    /// engagements). Credited when a <c>CustomerAdvance</c> is
    /// recorded; debited (and AR credited) when the final invoice
    /// posts and the held advance is applied as an offset.</summary>
    public const string CustomerAdvances = "2310";

    /// <summary>v5 E.11 — current asset that holds expenses paid in
    /// advance (annual insurance, prepaid rent, prepaid software
    /// subscriptions, training retainers). Debited when the
    /// <c>PrepaidExpense</c> is booked; credited monthly by the
    /// operator-triggered recognition JE that simultaneously debits
    /// the underlying expense account (e.g. 5200 GenericExpense).</summary>
    public const string PrepaidExpenseAsset = "1400";
}
