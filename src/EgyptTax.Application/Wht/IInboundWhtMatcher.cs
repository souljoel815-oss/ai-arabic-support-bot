using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Wht;

/// <summary>
/// P1.14 — find candidate sales invoices that a customer-issued
/// WHT certificate could plausibly relate to. The operator types
/// in (customer, period, amount withheld), and we score every
/// posted sales invoice for that customer in the period against
/// the implied gross-of-WHT amount, returning matches within a
/// configurable tolerance ranked by closeness.
///
/// The customer issues the cert AFTER they pay us a net amount —
/// so the gross-of-WHT amount is what was on our original invoice.
/// We match certificate.amount_withheld against
/// invoice.grand_total * cert_rate / 100 with a ±2% tolerance
/// (covers rounding + small fee deductions on the customer's side).
/// </summary>
public interface IInboundWhtMatcher
{
    Task<IReadOnlyList<InboundWhtMatchCandidate>> FindCandidatesAsync(
        Guid customerId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal amountWithheldEgp,
        decimal rateAppliedPercent,
        decimal tolerancePercent = 2.0m,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One ranked candidate. <see cref="ScorePercent"/> is 100 when the
/// invoice's implied withholding equals the certificate amount
/// exactly; lower as the gap widens. Sorted descending by score.
/// </summary>
public sealed record InboundWhtMatchCandidate(
    Guid SalesInvoiceId,
    string DocumentNumber,
    DateOnly DocumentDate,
    MoneyEgp GrandTotal,
    MoneyEgp ImpliedWithholding,
    decimal ScorePercent);
