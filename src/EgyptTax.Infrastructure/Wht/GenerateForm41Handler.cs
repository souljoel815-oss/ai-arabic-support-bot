using System.Globalization;
using System.Security.Cryptography;
using EgyptTax.Application.Wht;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Documents;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Purchases;
using EgyptTax.Domain.Tax;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Infrastructure.Wht;

/// <summary>
/// FR-046 / US7 / T209 — Form 41 generator. Aggregates the
/// supplier-side outbound WhtCertificate rows for one quarter
/// into the regulator-shaped <see cref="Form41Payload"/>:
///
///   * Lines: one per cert, joined to its supplier (TIN +
///     bilingual name) + supplier-payment voucher (number + date)
///     + source purchase invoice (number).
///   * Totals: line count + gross + amount withheld + per-category
///     breakdown (line count + amount per category code).
///   * Reconciliation: WHT-payable account balance at period end
///     (SUM of credits to the WhtPayable ledger account inside the
///     quarter) compared to the cert total. Match → clean filing;
///     mismatch → discrepancyAmount populated, the operator must
///     fix the books before MarkFiled is allowed.
///   * Audit chain extract ref: SHA-256 over the canonical JSON
///     of the filing (placeholder until the audit-extract path is
///     wired in batch 4b).
///
/// Persists a <see cref="Form41Filing"/> row in `Unfiled` status
/// so the lifecycle dashboard surfaces it; one canonical filing
/// per (year, quarter) is enforced by the unique index on the
/// table — generating twice for the same quarter throws.
/// </summary>
public sealed class GenerateForm41Handler
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public GenerateForm41Handler(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<GenerateForm41Result> HandleAsync(
        GenerateForm41Command command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Quarter is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(command),
                $"Quarter {command.Quarter} must be 1, 2, 3, or 4.");
        }

        var company = await _db.Set<Company>().AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No Company row exists. Seed the company profile before generating Form 41.");

        // Refuse a duplicate generation — the unique index would
        // also catch it on Save, but failing here gives a friendlier
        // error.
        var existing = await _db.Set<Form41Filing>().AsNoTracking()
            .AnyAsync(f => f.FiscalYear == command.FiscalYear && f.Quarter == command.Quarter,
                cancellationToken);
        if (existing)
        {
            throw new InvalidOperationException(
                $"Form 41 for {command.FiscalYear}-Q{command.Quarter} has already been generated. " +
                "Each quarter has exactly one canonical filing per FR-046.");
        }

        var (periodStart, periodEnd) = QuarterDates(command.FiscalYear, command.Quarter);

        // Pull supplier-side outbound certificates for the quarter.
        var certs = await _db.Set<WhtCertificate>().AsNoTracking()
            .Where(c => c.Direction == WhtCertificateDirection.OutboundToSupplier
                && c.Date >= periodStart && c.Date <= periodEnd)
            .OrderBy(c => c.Date).ThenBy(c => c.CertificateNumber)
            .ToListAsync(cancellationToken);

        // Resolve supplier + voucher + invoice + category lookups
        // in batch (avoids N+1).
        var supplierIds = certs.Select(c => c.CounterpartyId).Distinct().ToArray();
        var voucherIds = certs.Select(c => c.SourceVoucherId).Distinct().ToArray();
        var invoiceIds = certs.Select(c => c.SourceInvoiceId).Distinct().ToArray();
        var categoryIds = certs.Select(c => c.WhtCategoryId).Distinct().ToArray();

        var suppliers = await _db.Set<Supplier>().AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var vouchers = await _db.Set<SupplierPaymentVoucher>().AsNoTracking()
            .Where(v => voucherIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);
        var invoices = await _db.Set<PurchaseInvoice>().AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        var categories = await _db.Set<WhtCategory>().AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var lines = certs.Select(cert =>
        {
            suppliers.TryGetValue(cert.CounterpartyId, out var supplier);
            vouchers.TryGetValue(cert.SourceVoucherId, out var voucher);
            invoices.TryGetValue(cert.SourceInvoiceId, out var invoice);
            categories.TryGetValue(cert.WhtCategoryId, out var category);

            return new Form41Line(
                SupplierTin: supplier?.TaxProfile.TinValue
                    ?? throw new InvalidOperationException(
                        $"Supplier {cert.CounterpartyId} on cert {cert.Id} has no TIN; cannot include in Form 41."),
                SupplierName: supplier is null
                    ? new BilingualText("(unknown)", "(unknown)")
                    : new BilingualText(supplier.Name.Arabic, supplier.Name.English),
                WhtCategoryCode: category?.Code ?? "(unknown)",
                RateAppliedPercent: cert.RateAppliedPercent,
                GrossPaymentTotal: voucher?.GrossPaymentAmount.Amount ?? 0m,
                AmountWithheld: cert.AmountWithheld.Amount,
                SupplierPaymentVoucherNumber: voucher?.DocumentNumber ?? "(unknown)",
                SupplierPaymentVoucherDate: voucher?.PaymentDate ?? cert.Date,
                SourceInvoiceNumber: invoice?.DocumentNumber ?? "(unknown)",
                OutboundCertificateNumber: cert.CertificateNumber);
        }).ToList();

        // Per-category roll-up.
        var byCategory = lines
            .GroupBy(l => l.WhtCategoryCode)
            .Select(g => new Form41ByCategoryRow(
                WhtCategoryCode: g.Key,
                LineCount: g.Count(),
                AmountWithheld: g.Sum(x => x.AmountWithheld)))
            .OrderBy(c => c.WhtCategoryCode, StringComparer.Ordinal)
            .ToList();

        var totalGross = lines.Sum(l => l.GrossPaymentTotal);
        var totalWithheld = lines.Sum(l => l.AmountWithheld);

        // Reconciliation: WHT-payable account balance accrued
        // inside the quarter (SUM of credits to WhtPayable from
        // journal entries posted in the period).
        var startUtc = periodStart.ToDateTime(TimeOnly.MinValue);
        var endExclusive = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var whtPayableAccrued = await (
            from e in _db.Set<JournalEntry>().AsNoTracking()
            from l in e.Lines
            where e.PostedAtUtc >= startUtc && e.PostedAtUtc < endExclusive
                && l.AccountCode == ChartOfAccountCodes.WhtPayable
            select l.Credit.Amount - l.Debit.Amount).SumAsync(cancellationToken);

        var matches = whtPayableAccrued == totalWithheld;
        var discrepancy = matches ? (decimal?)null : whtPayableAccrued - totalWithheld;

        var nowUtc = _clock.UtcNow;

        // Build the payload BEFORE computing its hash so the audit
        // chain extract ref points at the immutable JSON bytes.
        var auditExtractRef = new Form41AuditChainExtractRef(
            StartIndex: 0,  // placeholder — full audit-extract wiring lands in batch 4b
            EndIndex: 0,
            ExtractSha256: ComputePlaceholderHash(command.FiscalYear, command.Quarter, totalWithheld));

        var payload = new Form41Payload(
            FilingHeader: new Form41Header(
                CompanyTin: company.TaxRegistrationNumber,
                CompanyName: new BilingualText(company.LegalName.Arabic, company.LegalName.English),
                FiscalYear: command.FiscalYear,
                Quarter: command.Quarter,
                FillingPeriodStart: periodStart,
                FillingPeriodEnd: periodEnd,
                PreparedAt: nowUtc,
                PreparedByUserId: command.PreparedByUserId),
            Lines: lines,
            Totals: new Form41Totals(
                LineCount: lines.Count,
                TotalGrossPayment: totalGross,
                TotalAmountWithheld: totalWithheld,
                ByCategory: byCategory),
            Reconciliation: new Form41Reconciliation(
                WhtPayableAccountBalanceAtPeriodEnd: whtPayableAccrued,
                MatchesTotalAmountWithheld: matches,
                DiscrepancyAmount: discrepancy),
            AuditChainExtractRef: auditExtractRef);

        // Persist a Form41Filing row (Unfiled) so the lifecycle
        // dashboard sees it. PDF + JSON file paths are stubbed to
        // empty until batch 4b wires the on-disk write — the entity
        // already records the totals + line count for dashboard
        // queries.
        var filing = Form41Filing.CreateUnfiled(
            fiscalYear: command.FiscalYear,
            quarter: command.Quarter,
            generatedAtUtc: nowUtc,
            pdfPath: "",
            structuredJsonPath: "",
            totalWhtPayable: MoneyEgp.From(decimal.Round(totalWithheld, 2, MidpointRounding.ToEven)),
            lineCount: lines.Count);
        _db.Add(filing);
        await _db.SaveChangesAsync(cancellationToken);

        return new GenerateForm41Result(filing.Id, payload);
    }

    /// <summary>Egyptian fiscal year is calendar; quarters are
    /// Jan–Mar, Apr–Jun, Jul–Sep, Oct–Dec.</summary>
    private static (DateOnly Start, DateOnly End) QuarterDates(int fiscalYear, int quarter)
    {
        var startMonth = (quarter - 1) * 3 + 1;
        var endMonth = startMonth + 2;
        var start = new DateOnly(fiscalYear, startMonth, 1);
        var end = new DateOnly(fiscalYear, endMonth,
            DateTime.DaysInMonth(fiscalYear, endMonth));
        return (start, end);
    }

    /// <summary>Placeholder hash for the audit-extract ref until
    /// the proper extract is wired (batch 4b). Deterministic per
    /// (year, quarter, totalWithheld) so re-generating the same
    /// quarter produces the same hash — useful for the contract
    /// test even before the real extract lands.</summary>
    private static string ComputePlaceholderHash(int year, int quarter, decimal totalWithheld)
    {
        var input = $"form41|{year}|{quarter}|{totalWithheld.ToString("F2", CultureInfo.InvariantCulture)}";
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
#pragma warning disable CA1308
        return Convert.ToHexString(bytes).ToLowerInvariant();
#pragma warning restore CA1308
    }
}
