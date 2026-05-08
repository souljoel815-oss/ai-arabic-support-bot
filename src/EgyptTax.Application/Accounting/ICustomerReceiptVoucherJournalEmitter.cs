using EgyptTax.Domain.Documents;

namespace EgyptTax.Application.Accounting;

/// <summary>
/// Phase 9 / FR-052 / US7-ready — port for the post-time journal
/// emitter on the customer-receipt side. The implementation builds:
///
///   * 2-line case (no WHT — Phase 9 default):
///       DR Cash (gross)  CR AR (gross)
///   * 3-line case (with customer-issued WHT certificate):
///       DR Cash (net)  DR WhtReceivable (wht)  CR AR (gross)
///
/// Both shapes balance to gross. Same emitter handles both, so US7's
/// customer-WHT recording automatically produces the 3-line form.
/// </summary>
public interface ICustomerReceiptVoucherJournalEmitter
{
    Task EmitForCustomerReceiptAsync(
        CustomerReceiptVoucher voucher,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default
    );
}
