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
}
