namespace EgyptTax.Application.Compliance;

/// <summary>
/// Differentiator 2 — port for the cockpit projection. The page
/// invokes once per refresh; result is a fully-assembled view-model.
/// </summary>
public interface IMonthlyTaxClosingCockpitQuery
{
    Task<MonthlyTaxClosingCockpit> RunAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default
    );
}
