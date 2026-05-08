using EgyptTax.Application.Reports;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Reports;

/// <summary>
/// FR-021 — EF-backed implementation. Two queries (sales side +
/// purchase-line side) feed the totals; counterparty names are
/// resolved in a third bulk query keyed by id-set so the report
/// renders without N+1.
///
/// At MVP scale (R-02 says reports may use Dapper for hot paths;
/// EF is fine here because the FK indexes already exist and the
/// SC-002 bar is "&lt; 5 s p95 at 5 k docs"). If profiling shows
/// EF overhead under load, swap to a Dapper materialiser without
/// changing the contract.
/// </summary>
public sealed class SqlVatMonthlyReportQuery : IVatMonthlyReportQuery
{
    private readonly AppDbContext _db;

    public SqlVatMonthlyReportQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<VatMonthlyReport> RunAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default
    )
    {
        if (year is < 1900 or > 9999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                "Year must be a 4-digit calendar year."
            );
        }
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be in [1, 12].");
        }

        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        // Sales side: posted SalesInvoice + CreditNote (same table)
        // whose document_date falls inside the period. Credit-note
        // VAT totals are negative by construction so summing yields
        // the net VAT charged.
        var salesRows = await _db.Set<SalesInvoice>()
            .AsNoTracking()
            .Where(s =>
                s.State == DocumentState.Posted
                && s.DocumentDate >= periodStart
                && s.DocumentDate <= periodEnd
            )
            .Select(s => new
            {
                s.Id,
                s.IsCreditNote,
                s.DocumentNumber,
                s.DocumentDate,
                s.CustomerId,
                NetAmount = s.NetBeforeVat.Amount,
                VatAmount = s.VatTotal.Amount,
            })
            .ToListAsync(cancellationToken);

        // Purchase side: posted PurchaseInvoice whose date_received
        // is in the period AND supplier-snapshot is RegisteredTaxpayer
        // (per FR-020 input VAT is recoverable only against registered
        // taxpayers). We sum LINE-level VAT for lines flagged
        // deductible — non-deductible lines on the same invoice don't
        // contribute.
        var purchaseLines = await (
            from p in _db.Set<PurchaseInvoice>().AsNoTracking()
            from l in p.Lines
            where
                p.State == DocumentState.Posted
                && p.DateReceived >= periodStart
                && p.DateReceived <= periodEnd
                && l.DeductibleFlag
                && p.SupplierTaxProfileSnapshot.ProfileType
                    == SupplierTaxProfileType.RegisteredTaxpayer
            select new
            {
                p.Id,
                p.DocumentNumber,
                p.DateReceived,
                p.SupplierId,
                LineSubtotal = l.LineSubtotal.Amount,
                LineVat = l.LineVat.Amount,
            }
        ).ToListAsync(cancellationToken);

        // Bulk-resolve counterparty names so the row builder is
        // single-pass.
        var customerIds = salesRows.Select(r => r.CustomerId).Distinct().ToArray();
        var customerNames = await _db.Set<Customer>()
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name.English, cancellationToken);
        var supplierIds = purchaseLines.Select(r => r.SupplierId).Distinct().ToArray();
        var supplierNames = await _db.Set<Supplier>()
            .AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name.English, cancellationToken);

        var rows = new List<VatMonthlyReportRow>();
        foreach (var s in salesRows)
        {
            rows.Add(
                new VatMonthlyReportRow(
                    DocumentId: s.Id,
                    DocumentType: s.IsCreditNote
                        ? DocumentType.CreditNote
                        : DocumentType.SalesInvoice,
                    DocumentNumber: s.DocumentNumber,
                    DocumentDate: s.DocumentDate,
                    CounterpartyName: customerNames.GetValueOrDefault(
                        s.CustomerId,
                        s.CustomerId.ToString("D")
                    ),
                    NetAmount: MoneyEgp.From(s.NetAmount),
                    VatAmount: MoneyEgp.From(s.VatAmount),
                    ContributesToOutput: true
                )
            );
        }

        // Group purchase lines back into per-document rows so the
        // report shows one row per invoice rather than one per line.
        var purchaseGrouped = purchaseLines
            .GroupBy(r => new
            {
                r.Id,
                r.DocumentNumber,
                r.DateReceived,
                r.SupplierId,
            })
            .Select(g => new
            {
                g.Key.Id,
                g.Key.DocumentNumber,
                g.Key.DateReceived,
                g.Key.SupplierId,
                NetAmount = g.Sum(x => x.LineSubtotal),
                VatAmount = g.Sum(x => x.LineVat),
            });
        foreach (var p in purchaseGrouped)
        {
            rows.Add(
                new VatMonthlyReportRow(
                    DocumentId: p.Id,
                    DocumentType: DocumentType.PurchaseInvoice,
                    DocumentNumber: p.DocumentNumber,
                    DocumentDate: p.DateReceived,
                    CounterpartyName: supplierNames.GetValueOrDefault(
                        p.SupplierId,
                        p.SupplierId.ToString("D")
                    ),
                    NetAmount: MoneyEgp.From(p.NetAmount),
                    VatAmount: MoneyEgp.From(p.VatAmount),
                    ContributesToOutput: false
                )
            );
        }

        var orderedRows = rows.OrderBy(r => r.DocumentDate)
            .ThenBy(r => r.DocumentNumber, StringComparer.Ordinal)
            .ToList();

        var outputVat = MoneyEgp.From(
            decimal.Round(salesRows.Sum(s => s.VatAmount), 2, MidpointRounding.ToEven)
        );
        var inputVat = MoneyEgp.From(
            decimal.Round(purchaseLines.Sum(l => l.LineVat), 2, MidpointRounding.ToEven)
        );
        var net = outputVat - inputVat;

        return new VatMonthlyReport(
            Year: year,
            Month: month,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            OutputVat: outputVat,
            InputVatRecoverable: inputVat,
            NetPayable: net,
            Rows: orderedRows
        );
    }
}
