using EgyptTax.Application.Compliance;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.Eta;
using EgyptTax.Domain.Expenses;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Periods;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Compliance;

/// <summary>
/// Differentiator 2 — EF-backed cockpit projection. Composes signals
/// already proven in the underlying suites (VAT report query, risk
/// rules, ETA dashboard query, period lock guard); the cockpit is the
/// "did I miss anything?" landing page that pulls those answers into
/// one place.
/// </summary>
public sealed class SqlMonthlyTaxClosingCockpitQuery : IMonthlyTaxClosingCockpitQuery
{
    private const int ExamplesPerBucket = 10;

    private readonly AppDbContext _db;

    public SqlMonthlyTaxClosingCockpitQuery(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MonthlyTaxClosingCockpit> RunAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default
    )
    {
        if (year is < 1900 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        // Period lock status — single seek against ux_tax_periods_*.
        var periodRow = await _db.Set<TaxPeriod>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p =>
                    p.PeriodKind == TaxPeriodKind.VatMonth
                    && p.Year == year
                    && p.MonthOrQuarter == month,
                cancellationToken
            );
        var isLocked = periodRow?.Status == TaxPeriodStatus.Locked;

        // Posted sales invoices in period (counts the document number
        // assignment, so credit notes are included via the same
        // SalesInvoice table).
        var postedSales = await _db.Set<SalesInvoice>()
            .AsNoTracking()
            .Where(s =>
                s.State == DocumentState.Posted
                && s.DocumentDate >= periodStart
                && s.DocumentDate <= periodEnd
            )
            .Select(s => new
            {
                s.Id,
                s.DocumentNumber,
                s.DocumentDate,
                s.IsCreditNote,
                s.CustomerId,
            })
            .ToListAsync(cancellationToken);

        // Posted purchases in period — pull line-level deductible
        // info because FR-016 + non-recoverable VAT both need it.
        var postedPurchases = await (
            from p in _db.Set<PurchaseInvoice>().AsNoTracking()
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
                p.SupplierTaxProfileSnapshot.ProfileType,
                AnyDeductibleLine = p.Lines.Any(l => l.DeductibleFlag),
                InputVat = p.Lines.Where(l => l.DeductibleFlag).Sum(l => (decimal?)l.LineVat.Amount)
                    ?? 0m,
            }
        ).ToListAsync(cancellationToken);

        // Posted expenses in period.
        var postedExpenses = await _db.Set<Expense>()
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
                e.DeductibleFlag,
                e.CategoryId,
            })
            .ToListAsync(cancellationToken);

        // Drafts in period — anything in Draft state with a date in the
        // window. Operator must post or void these before locking.
        var draftSalesCount = await _db.Set<SalesInvoice>()
            .AsNoTracking()
            .CountAsync(
                s =>
                    s.State == DocumentState.Draft
                    && s.DocumentDate >= periodStart
                    && s.DocumentDate <= periodEnd,
                cancellationToken
            );
        var draftPurchaseCount = await _db.Set<PurchaseInvoice>()
            .AsNoTracking()
            .CountAsync(
                p =>
                    p.State == DocumentState.Draft
                    && p.DateReceived >= periodStart
                    && p.DateReceived <= periodEnd,
                cancellationToken
            );
        var draftExpenseCount = await _db.Set<Expense>()
            .AsNoTracking()
            .CountAsync(
                e =>
                    e.State == DocumentState.Draft
                    && e.DocumentDate >= periodStart
                    && e.DocumentDate <= periodEnd,
                cancellationToken
            );
        var totalDrafts = draftSalesCount + draftPurchaseCount + draftExpenseCount;

        // Attachment lookup for FR-016: every deductible purchase or
        // expense post in period that has zero attachments on file.
        var deductiblePurchases = postedPurchases.Where(p => p.AnyDeductibleLine).ToList();
        var deductibleExpenses = postedExpenses.Where(e => e.DeductibleFlag).ToList();
        var documentIds = deductiblePurchases
            .Select(p => p.Id)
            .Concat(deductibleExpenses.Select(e => e.Id))
            .ToArray();
        var attachmentCounts =
            documentIds.Length == 0
                ? new Dictionary<Guid, int>()
                : await _db.Set<Attachment>()
                    .AsNoTracking()
                    .Where(a => documentIds.Contains(a.DocumentId))
                    .GroupBy(a => a.DocumentId)
                    .Select(g => new { Id = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var purchasesMissingAttachment = deductiblePurchases
            .Where(p => attachmentCounts.GetValueOrDefault(p.Id) == 0)
            .ToList();
        var expensesMissingAttachment = deductibleExpenses
            .Where(e => attachmentCounts.GetValueOrDefault(e.Id) == 0)
            .ToList();

        // ETA submissions for the period's posted sales: any in
        // Failed status are surfaced; any Pending past the window
        // are extreme-priority.
        var salesIds = postedSales.Select(s => s.Id).ToArray();
        var etaRows =
            salesIds.Length == 0
                ? new List<EtaSubmission>()
                : await _db.Set<EtaSubmission>()
                    .AsNoTracking()
                    .Where(s => salesIds.Contains(s.SalesInvoiceId))
                    .ToListAsync(cancellationToken);
        var etaBySales = etaRows.ToDictionary(s => s.SalesInvoiceId);
        var failedEta = etaRows.Where(e => e.Status == EtaSubmissionStatus.Failed).ToList();
        var salesMissingEtaSubmission = postedSales
            .Where(s => !etaBySales.ContainsKey(s.Id))
            .ToList();

        // Counterparty names so the bucket examples are readable.
        var customerIds = postedSales.Select(s => s.CustomerId).Distinct().ToArray();
        var customerNames =
            customerIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await _db.Set<Customer>()
                    .AsNoTracking()
                    .Where(c => customerIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Name.English, cancellationToken);
        var supplierIds = postedPurchases.Select(p => p.SupplierId).Distinct().ToArray();
        var supplierNames =
            supplierIds.Length == 0
                ? new Dictionary<Guid, string>()
                : await _db.Set<Supplier>()
                    .AsNoTracking()
                    .Where(s => supplierIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name.English, cancellationToken);

        // Build buckets.
        var missingBuckets = new List<MissingDocumentBucket>();
        if (purchasesMissingAttachment.Count > 0)
        {
            missingBuckets.Add(
                new MissingDocumentBucket(
                    Name: "Posted purchase invoices with deductible lines but no attachment (FR-016)",
                    Count: purchasesMissingAttachment.Count,
                    Examples: purchasesMissingAttachment
                        .Take(ExamplesPerBucket)
                        .Select(p => new MissingDocumentExample(
                            p.Id,
                            "PurchaseInvoice",
                            p.DocumentNumber,
                            p.DateReceived,
                            supplierNames.GetValueOrDefault(
                                p.SupplierId,
                                p.SupplierId.ToString("D")
                            )
                        ))
                        .ToList()
                )
            );
        }
        if (expensesMissingAttachment.Count > 0)
        {
            missingBuckets.Add(
                new MissingDocumentBucket(
                    Name: "Posted deductible expenses with no attachment (FR-016)",
                    Count: expensesMissingAttachment.Count,
                    Examples: expensesMissingAttachment
                        .Take(ExamplesPerBucket)
                        .Select(e => new MissingDocumentExample(
                            e.Id,
                            "Expense",
                            e.DocumentNumber,
                            e.DocumentDate,
                            e.CategoryId.ToString("D")
                        ))
                        .ToList()
                )
            );
        }
        if (salesMissingEtaSubmission.Count > 0)
        {
            missingBuckets.Add(
                new MissingDocumentBucket(
                    Name: "Posted sales invoices with no ETA submission row (FR-035)",
                    Count: salesMissingEtaSubmission.Count,
                    Examples: salesMissingEtaSubmission
                        .Take(ExamplesPerBucket)
                        .Select(s => new MissingDocumentExample(
                            s.Id,
                            s.IsCreditNote ? "CreditNote" : "SalesInvoice",
                            s.DocumentNumber,
                            s.DocumentDate,
                            customerNames.GetValueOrDefault(
                                s.CustomerId,
                                s.CustomerId.ToString("D")
                            )
                        ))
                        .ToList()
                )
            );
        }

        // The failed-ETA total wants per-invoice grand totals; pull
        // them via a small follow-up query for the failed set.
        var failedSalesIds = failedEta.Select(f => f.SalesInvoiceId).ToArray();
        var failedTotal =
            failedSalesIds.Length == 0
                ? 0m
                : await _db.Set<SalesInvoice>()
                    .AsNoTracking()
                    .Where(s => failedSalesIds.Contains(s.Id))
                    .Select(s => s.GrandTotal.Amount)
                    .SumAsync(cancellationToken);

        // VAT readiness: clean = posted documents in period with no
        // bucket membership. Partition is approximate (we only check
        // the missing-attachment + missing-ETA-submission buckets;
        // duplicate-supplier-invoice / unregistered-supplier tend to
        // overlap with these). Good enough for the cockpit's headline
        // number; the per-document badge surfaces the real per-doc score.
        var totalPosts = postedSales.Count + postedPurchases.Count + postedExpenses.Count;
        var dirtyPostIds = new HashSet<Guid>(
            purchasesMissingAttachment
                .Select(p => p.Id)
                .Concat(expensesMissingAttachment.Select(e => e.Id))
                .Concat(salesMissingEtaSubmission.Select(s => s.Id))
                .Concat(failedEta.Select(f => f.SalesInvoiceId))
        );
        var cleanPosts = totalPosts - dirtyPostIds.Count;
        var readiness =
            totalPosts == 0
                ? 100m
                : decimal.Round(
                    (decimal)cleanPosts / totalPosts * 100m,
                    1,
                    MidpointRounding.ToEven
                );

        // Non-recoverable input VAT: deductible purchase lines
        // against non-RegisteredTaxpayer suppliers (operator should
        // have un-flipped these per FR-020).
        var nonRecoverable = decimal.Round(
            postedPurchases
                .Where(p => p.ProfileType != SupplierTaxProfileType.RegisteredTaxpayer)
                .Sum(p => p.InputVat),
            2,
            MidpointRounding.ToEven
        );

        var checklist = new List<PeriodLockChecklistItem>
        {
            new(
                "All drafts dated in period are posted or voided",
                Cleared: totalDrafts == 0,
                RelatedCount: totalDrafts == 0 ? null : totalDrafts
            ),
            new(
                "All deductible posts have at least one attachment (FR-016)",
                Cleared: purchasesMissingAttachment.Count == 0
                    && expensesMissingAttachment.Count == 0,
                RelatedCount: purchasesMissingAttachment.Count + expensesMissingAttachment.Count
                    is var c
                && c == 0
                    ? null
                    : c
            ),
            new(
                "All posted sales invoices have an ETA submission row",
                Cleared: salesMissingEtaSubmission.Count == 0,
                RelatedCount: salesMissingEtaSubmission.Count == 0
                    ? null
                    : salesMissingEtaSubmission.Count
            ),
            new(
                "No Failed ETA submissions outstanding for the period",
                Cleared: failedEta.Count == 0,
                RelatedCount: failedEta.Count == 0 ? null : failedEta.Count
            ),
            new("Period not yet locked (FR-037)", Cleared: !isLocked, RelatedCount: null),
        };

        return new MonthlyTaxClosingCockpit(
            Year: year,
            Month: month,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            PeriodIsLocked: isLocked,
            VatReadinessPercent: readiness,
            TotalPostsInPeriod: totalPosts,
            CleanPostsCount: cleanPosts,
            MissingDocuments: missingBuckets,
            FailedEtaSubmissionCount: failedEta.Count,
            FailedEtaSubmissionTotalGrand: MoneyEgp.From(failedTotal),
            DraftsInPeriodCount: totalDrafts,
            NonRecoverableInputVat: MoneyEgp.From(nonRecoverable),
            PeriodLockChecklist: checklist
        );
    }
}
