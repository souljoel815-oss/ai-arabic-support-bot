using EgyptTax.Application.Reports;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Reports;

/// <summary>
/// FR-023 — EF-backed taxable income report. Three query branches
/// (sales, purchase lines, expenses); rows are bucketed by the
/// user-facing labels the page consumes.
/// </summary>
public sealed class SqlTaxableIncomeReportQuery : ITaxableIncomeReportQuery
{
    private const string BucketRevenue = "Revenue";
    private const string BucketDeductible = "Deductible expense";
    private const string BucketNonDeductible = "Non-deductible adjustment";

    private readonly AppDbContext _db;

    public SqlTaxableIncomeReportQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TaxableIncomeReport> RunAsync(
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default
    )
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException(
                $"PeriodEnd ({periodEnd:yyyy-MM-dd}) cannot be before PeriodStart ({periodStart:yyyy-MM-dd}).",
                nameof(periodEnd)
            );
        }

        // Sales — net-of-VAT subtotals; credit notes are already
        // negative by construction so summing yields net revenue.
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
            })
            .ToListAsync(cancellationToken);

        // Purchase lines — line-level granularity because the
        // deductible_flag is per-line.
        var purchaseLines = await (
            from p in _db.Set<PurchaseInvoice>().AsNoTracking()
            from l in p.Lines
            where
                p.State == DocumentState.Posted
                && p.DateReceived >= periodStart
                && p.DateReceived <= periodEnd
            select new
            {
                p.Id,
                p.DocumentNumber,
                p.DateReceived,
                p.SupplierId,
                LineSubtotal = l.LineSubtotal.Amount,
                l.DeductibleFlag,
            }
        ).ToListAsync(cancellationToken);

        // Expenses — header-level (no lines on this aggregate).
        var expenseRows = await _db.Set<Expense>()
            .AsNoTracking()
            .Where(e =>
                e.State == DocumentState.Posted
                && e.DocumentDate >= periodStart
                && e.DocumentDate <= periodEnd
            )
            .Select(e => new
            {
                e.Id,
                e.DocumentNumber,
                e.DocumentDate,
                e.CategoryId,
                Amount = e.Amount.Amount,
                e.DeductibleFlag,
                DescriptionEn = e.Description.English,
            })
            .ToListAsync(cancellationToken);

        // Bulk-resolve display names.
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
        var categoryIds = expenseRows.Select(r => r.CategoryId).Distinct().ToArray();
        var categoryNames = await _db.Set<DeductibleExpenseCategory>()
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);

        var rows = new List<TaxableIncomeRow>();
        foreach (var s in salesRows)
        {
            rows.Add(
                new TaxableIncomeRow(
                    Bucket: BucketRevenue,
                    DocumentId: s.Id,
                    DocumentType: s.IsCreditNote
                        ? DocumentType.CreditNote
                        : DocumentType.SalesInvoice,
                    DocumentNumber: s.DocumentNumber,
                    DocumentDate: s.DocumentDate,
                    Description: customerNames.GetValueOrDefault(
                        s.CustomerId,
                        s.CustomerId.ToString("D")
                    ),
                    Amount: MoneyEgp.From(s.NetAmount)
                )
            );
        }

        // Group purchase lines per (document, deductible) so a single
        // invoice with mixed deductible / non-deductible lines surfaces
        // as TWO rows — operator can see the split.
        var purchaseGrouped = purchaseLines
            .GroupBy(r => new
            {
                r.Id,
                r.DocumentNumber,
                r.DateReceived,
                r.SupplierId,
                r.DeductibleFlag,
            })
            .Select(g => new
            {
                g.Key.Id,
                g.Key.DocumentNumber,
                g.Key.DateReceived,
                g.Key.SupplierId,
                g.Key.DeductibleFlag,
                Amount = g.Sum(x => x.LineSubtotal),
            });
        foreach (var p in purchaseGrouped)
        {
            rows.Add(
                new TaxableIncomeRow(
                    Bucket: p.DeductibleFlag ? BucketDeductible : BucketNonDeductible,
                    DocumentId: p.Id,
                    DocumentType: DocumentType.PurchaseInvoice,
                    DocumentNumber: p.DocumentNumber,
                    DocumentDate: p.DateReceived,
                    Description: supplierNames.GetValueOrDefault(
                        p.SupplierId,
                        p.SupplierId.ToString("D")
                    ) + (p.DeductibleFlag ? "" : " (non-deductible portion)"),
                    Amount: MoneyEgp.From(p.Amount)
                )
            );
        }

        foreach (var e in expenseRows)
        {
            rows.Add(
                new TaxableIncomeRow(
                    Bucket: e.DeductibleFlag ? BucketDeductible : BucketNonDeductible,
                    DocumentId: e.Id,
                    DocumentType: DocumentType.Expense,
                    DocumentNumber: e.DocumentNumber,
                    DocumentDate: e.DocumentDate,
                    Description: $"{categoryNames.GetValueOrDefault(e.CategoryId, "?")} — {e.DescriptionEn}",
                    Amount: MoneyEgp.From(e.Amount)
                )
            );
        }

        var revenue = decimal.Round(salesRows.Sum(s => s.NetAmount), 2, MidpointRounding.ToEven);
        var deductibleFromPurchases = purchaseLines
            .Where(l => l.DeductibleFlag)
            .Sum(l => l.LineSubtotal);
        var deductibleFromExpenses = expenseRows.Where(e => e.DeductibleFlag).Sum(e => e.Amount);
        var deductible = decimal.Round(
            deductibleFromPurchases + deductibleFromExpenses,
            2,
            MidpointRounding.ToEven
        );
        var nonDeductibleFromPurchases = purchaseLines
            .Where(l => !l.DeductibleFlag)
            .Sum(l => l.LineSubtotal);
        var nonDeductibleFromExpenses = expenseRows
            .Where(e => !e.DeductibleFlag)
            .Sum(e => e.Amount);
        var nonDeductible = decimal.Round(
            nonDeductibleFromPurchases + nonDeductibleFromExpenses,
            2,
            MidpointRounding.ToEven
        );

        var managementPL = decimal.Round(
            revenue - deductible - nonDeductible,
            2,
            MidpointRounding.ToEven
        );
        var taxableIncome = decimal.Round(revenue - deductible, 2, MidpointRounding.ToEven);

        return new TaxableIncomeReport(
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            Revenue: MoneyEgp.From(revenue),
            DeductibleExpenses: MoneyEgp.From(deductible),
            NonDeductibleAdjustments: MoneyEgp.From(nonDeductible),
            ManagementProfitLoss: MoneyEgp.From(managementPL),
            TaxableIncome: MoneyEgp.From(taxableIncome),
            Rows: rows.OrderBy(r => r.DocumentDate)
                .ThenBy(r => r.Bucket, StringComparer.Ordinal)
                .ThenBy(r => r.DocumentNumber, StringComparer.Ordinal)
                .ToList()
        );
    }
}
