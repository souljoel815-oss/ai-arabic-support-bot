using EgyptTax.Domain.Documents;

namespace EgyptTax.Application.Accounting;

/// <summary>
/// Phase 9 / FR-051 / US7-ready — port for the post-time journal
/// emitter on the supplier-payment side. The implementation builds:
///
///   * 2-line case (no WHT — Phase 9 default):
///       DR AP   (gross)  CR Cash (gross)
///   * 3-line case (with WHT — when ApplyWhtSplit was called):
///       DR AP   (gross)  CR Cash (net)  CR WhtPayable (wht)
///
/// Both shapes balance to gross. The same emitter handles both —
/// US7's WHT compute landing on the voucher AUTOMATICALLY produces
/// the 3-line form without changes here.
/// </summary>
public interface ISupplierPaymentVoucherJournalEmitter
{
    Task EmitForSupplierPaymentAsync(
        SupplierPaymentVoucher voucher,
        DateTime postedAtUtc,
        CancellationToken cancellationToken = default
    );
}
