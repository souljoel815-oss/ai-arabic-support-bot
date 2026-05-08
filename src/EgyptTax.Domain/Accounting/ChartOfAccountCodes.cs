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
}
