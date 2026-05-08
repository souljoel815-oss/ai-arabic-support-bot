using EgyptTax.Application.Wht;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Wht;

/// <summary>
/// FR-045 / R-17 / US7 — EF-backed WHT compute. Selects the
/// WhtCategory row whose:
///   * code matches,
///   * applicable_to ∈ { direction, Both },
///   * effective_from_date ≤ paymentDate AND
///     (effective_to_date == null OR effective_to_date ≥ paymentDate),
/// and computes amount = round(gross × rate / 100, 2, ToEven).
///
/// When two rows match (e.g., a category superseded mid-window),
/// the latest <c>effective_from_date</c> wins — that's the
/// "supersede by inserting newer" semantics the operator expects.
/// </summary>
public sealed class SqlWhtComputeService : IWhtComputeService
{
    private readonly AppDbContext _db;

    public SqlWhtComputeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WhtComputation?> ComputeAsync(
        string categoryCode,
        DateOnly paymentDate,
        MoneyEgp grossAmount,
        WhtApplicableTo direction,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryCode);

        var category = await _db.Set<WhtCategory>().AsNoTracking()
            .Where(c => c.Code == categoryCode
                && c.EffectiveFromDate <= paymentDate
                && (c.EffectiveToDate == null || c.EffectiveToDate >= paymentDate)
                && (c.ApplicableTo == direction || c.ApplicableTo == WhtApplicableTo.Both))
            .OrderByDescending(c => c.EffectiveFromDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return null;
        }

        var amount = decimal.Round(
            grossAmount.Amount * category.RatePercent / 100m,
            2, MidpointRounding.ToEven);

        return new WhtComputation(
            WhtCategoryId: category.Id,
            WhtCategoryCode: category.Code,
            RateAppliedPercent: category.RatePercent,
            AmountWithheld: MoneyEgp.From(amount));
    }
}
