namespace EgyptTax.Application.Reports;

/// <summary>v4 A.1 — General Ledger query, period + account scoped.</summary>
public interface IGeneralLedgerReportQuery
{
    /// <summary>
    /// Run the GL for the period. Pass <c>null</c> or an empty list
    /// for <paramref name="accountCodes"/> to include every account
    /// with activity; pass a non-empty set to restrict to those
    /// account codes (case-sensitive, matches <c>JournalEntryLine.AccountCode</c>
    /// exactly).
    /// </summary>
    Task<GeneralLedgerReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyCollection<string>? accountCodes,
        CancellationToken cancellationToken = default
    );

    /// <summary>List of distinct account codes that have ever had
    /// journal-entry activity. Used to populate the page's account
    /// picker without inventing a separate <c>ChartOfAccount</c>
    /// table (the v3 chart is a static <c>const</c> set in
    /// <c>ChartOfAccountCodes</c>; new codes naturally appear here
    /// the first time a journal entry posts to them).</summary>
    Task<IReadOnlyList<string>> GetKnownAccountCodesAsync(
        CancellationToken cancellationToken = default
    );
}
