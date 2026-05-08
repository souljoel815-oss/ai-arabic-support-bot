using EgyptTax.Domain.MasterData;
using EgyptTax.SharedKernel;

namespace EgyptTax.Application.Wht;

/// <summary>
/// FR-045 / FR-046 / R-17 / US7 — pure-function WHT compute. Given
/// a category code + a payment date + a gross amount + a direction
/// (suppliers-side vs customers-side), returns the WHT amount and
/// the rate that produced it. The category in force is selected
/// by **payment date** (R-17): two categories with the same code
/// but different effective windows produce different rates for
/// payments in different periods.
///
/// Returns <c>null</c> when no category matches — callers treat
/// this as "WHT not applicable for this payment" and skip the
/// split (the voucher's WHT amount stays at zero).
/// </summary>
public interface IWhtComputeService
{
    Task<WhtComputation?> ComputeAsync(
        string categoryCode,
        DateOnly paymentDate,
        MoneyEgp grossAmount,
        WhtApplicableTo direction,
        CancellationToken cancellationToken = default
    );
}

public sealed record WhtComputation(
    Guid WhtCategoryId,
    string WhtCategoryCode,
    decimal RateAppliedPercent,
    MoneyEgp AmountWithheld
);
