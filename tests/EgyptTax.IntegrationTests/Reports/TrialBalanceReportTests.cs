using EgyptTax.Application.Audit;
using EgyptTax.Application.Invoices;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Audit;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Accounting;
using EgyptTax.Infrastructure.Audit;
using EgyptTax.Infrastructure.Invoices;
using EgyptTax.Infrastructure.Numbering;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Reports;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;

namespace EgyptTax.IntegrationTests.Reports;

/// <summary>
/// FR-024 — trial balance balance-invariant test. Posts a single
/// 1,000 EGP @ 14% sale via the production handler (which emits
/// the journal entry through SalesInvoiceJournalEmitter), then
/// runs the trial balance and asserts the per-account amounts
/// + the SUM(debits) == SUM(credits) bedrock invariant.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class TrialBalanceReportTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task SinglePostedSale_Yields_BalancedTrialBalance()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedAsync(db);

        // 1,000 net + 140 VAT = 1,140 grand. Journal:
        //   DEBIT  1200 AccountsReceivable  1,140
        //   CREDIT 4000 SalesRevenue        1,000
        //   CREDIT 2110 OutputVatPayable      140
        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 5, 7)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(1_000m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var emitter = new SalesInvoiceJournalEmitter(db);
        var handler = new PostSalesInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore(),
            emitter
        );
        await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );

        var report = await new SqlTrialBalanceReportQuery(db).RunAsync(
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31)
        );

        report
            .IsBalanced.Should()
            .BeTrue(
                because: "the journal-entry factory enforces SUM(debits) == SUM(credits) at write time; the trial balance MUST reflect that"
            );
        report.TotalDebits.Amount.Should().Be(1_140m);
        report.TotalCredits.Amount.Should().Be(1_140m);

        report.Rows.Should().HaveCount(3);
        report
            .Rows.Should()
            .Contain(r =>
                r.AccountCode == ChartOfAccountCodes.AccountsReceivable
                && r.TotalDebit.Amount == 1_140m
                && r.TotalCredit.Amount == 0m
            );
        report
            .Rows.Should()
            .Contain(r =>
                r.AccountCode == ChartOfAccountCodes.SalesRevenue
                && r.TotalCredit.Amount == 1_000m
                && r.TotalDebit.Amount == 0m
            );
        report
            .Rows.Should()
            .Contain(r =>
                r.AccountCode == ChartOfAccountCodes.OutputVatPayable
                && r.TotalCredit.Amount == 140m
                && r.TotalDebit.Amount == 0m
            );
    }

    [Fact]
    public async Task DocumentsOutsidePeriod_Are_NotIncluded_InTrialBalance()
    {
        await using var db = await _fixture.CreateContextAsync();
        var (customer, item, vat, user) = await SeedAsync(db);

        var draft = SalesInvoice.CreateDraft(
            customer.Id,
            customer.TaxProfile,
            new DateOnly(2026, 4, 30)
        );
        draft.AddLine(item.Id, 1m, MoneyEgp.From(500m), vat.Id, vat.RatePercent);
        db.Add(draft);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 4, 30, 11, 0, 0, DateTimeKind.Utc));
        var emitter = new SalesInvoiceJournalEmitter(db);
        var handler = new PostSalesInvoiceHandler(
            db,
            new SqlSequentialNumberAllocator(db),
            clock,
            new CaptureAuditLogStore(),
            emitter
        );
        await handler.HandleAsync(
            new PostSalesInvoiceCommand(draft.Id, user.Id),
            CancellationToken.None
        );

        // Query for May only — April-posted entry should be excluded.
        var mayReport = await new SqlTrialBalanceReportQuery(db).RunAsync(
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31)
        );
        mayReport.Rows.Should().BeEmpty();
        mayReport.TotalDebits.Amount.Should().Be(0m);
        mayReport.TotalCredits.Amount.Should().Be(0m);
        mayReport
            .IsBalanced.Should()
            .BeTrue(because: "0 == 0 is balanced — the empty case is balanced too");
    }

    private static async Task<(Customer customer, Item item, VatCategory vat, User user)> SeedAsync(
        AppDbContext db
    )
    {
        var vat = new VatCategory(
            code: "Standard",
            name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m,
            effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null,
            recoverableInputVat: true
        );
        var customer = new Customer(
            code: $"CUST-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل", "Customer"),
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo",
                "Downtown",
                "Tahrir",
                "1"
            ),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"),
                false,
                vat.Id
            )
        );
        var item = new Item(
            code: $"ITEM-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("صنف", "Item"),
            defaultVatCategoryId: vat.Id
        );
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(vat);
        db.Add(customer);
        db.Add(item);
        db.Add(user);
        await db.SaveChangesAsync();
        return (customer, item, vat, user);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class CaptureAuditLogStore : IAuditLogStore
    {
        public Task<AuditLogEntry> AppendAsync(
            AuditLogPayload payload,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(payload);
            return Task.FromResult(
                new AuditLogEntry(
                    index: 1,
                    tsUtc: DateTime.UtcNow,
                    actorUserId: payload.ActorUserId,
                    actorFirmName: payload.ActorFirmName,
                    companyId: payload.CompanyId,
                    kind: payload.Kind,
                    payloadJson: payload.PayloadJson,
                    prevHash: new byte[32],
                    thisHash: new byte[32]
                )
            );
        }
    }
}
