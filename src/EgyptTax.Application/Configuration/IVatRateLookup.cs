using EgyptTax.Domain.MasterData;

namespace EgyptTax.Application.Configuration;

/// <summary>
/// FR-019 / FR-022 / US5 — date-driven VAT-category lookup.
/// Mirrors <see cref="EgyptTax.Application.Wht.IWhtComputeService"/>
/// on the VAT side: pick the row whose code matches and whose
/// effective window covers <paramref name="date"/>. Ties broken by
/// latest <c>EffectiveFromDate</c> (supersession by inserting newer
/// rows). Returns null when no row matches — caller decides how to
/// handle (UI: surface "no rate effective on this date"; sales-line
/// edit: refuse the line until the operator configures a rate).
/// </summary>
public interface IVatRateLookup
{
    Task<VatCategory?> GetEffectiveAsync(
        string code,
        DateOnly date,
        CancellationToken cancellationToken = default
    );

    /// <summary>List every row for a given code (any effective
    /// window). Used by the VAT-category settings page to display
    /// the supersession history + flag overlapping rows that the
    /// operator should resolve.</summary>
    Task<IReadOnlyList<VatCategory>> ListByCodeAsync(
        string code,
        CancellationToken cancellationToken = default
    );
}
