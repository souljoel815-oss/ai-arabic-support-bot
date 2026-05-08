using EgyptTax.Application.Wht;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Tax;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Wht;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Wht;

/// <summary>
/// T201 / US7 scenario 4 / FR-047 — overdue Form 41 surfaced
/// with red indicator (DerivedStatus = Overdue) + estimated
/// penalty. Pins the dashboard's overdue derivation: a Form 41
/// past its (quarter end + 30 days) grace window with status
/// still Unfiled MUST be flagged Overdue, with a non-null
/// EstimatedPenalty proportional to the time overdue + WHT total.
/// Filed filings stay Filed regardless of due date; not-yet-due
/// Unfiled filings stay Unfiled.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class WhtDashboardOverdueTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task UnfiledFilingPastGracePeriod_DerivesAsOverdue_WithEstimatedPenalty()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);

        // Q2 2026 ends 2026-06-30; due date = 2026-07-30
        // (30-day grace). As-of 2026-09-15 → ~47 days overdue → 2
        // months → 2% penalty.
        var q2Filing = Form41Filing.CreateUnfiled(
            fiscalYear: 2026,
            quarter: 2,
            generatedAtUtc: new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc),
            pdfPath: "",
            structuredJsonPath: "",
            totalWhtPayable: MoneyEgp.From(10_000m),
            lineCount: 5
        );
        db.Add(q2Filing);
        await db.SaveChangesAsync();

        var query = new SqlWhtLifecycleDashboardQuery(db);
        var dashboard = await query.GetAsync(asOf: new DateOnly(2026, 9, 15));

        var row = dashboard.Filings.Single(f => f.Id == q2Filing.Id);
        row.DerivedStatus.Should()
            .Be(
                Form41Status.Overdue,
                because: "Q2 2026 due date is 2026-07-30; as-of 2026-09-15 is past it + status is Unfiled"
            );
        row.DueDate.Should().Be(new DateOnly(2026, 7, 30));
        row.DaysOverdue.Should().Be(47, because: "from 2026-07-30 to 2026-09-15 is 47 days");
        row.EstimatedPenalty.Should().NotBeNull();
        row.EstimatedPenalty.Should()
            .Be(200m, because: "47 days = 2 months ceil → 2% × 10,000 = 200 EGP penalty");
    }

    [Fact]
    public async Task FilingFiledBeforeDueDate_StaysFiled_NoOverdueDerivation()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);

        var filing = Form41Filing.CreateUnfiled(
            fiscalYear: 2026,
            quarter: 1,
            generatedAtUtc: new DateTime(2026, 4, 5, 10, 0, 0, DateTimeKind.Utc),
            pdfPath: "",
            structuredJsonPath: "",
            totalWhtPayable: MoneyEgp.From(2_000m),
            lineCount: 1
        );
        filing.MarkFiled(Guid.NewGuid(), new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));
        db.Add(filing);
        await db.SaveChangesAsync();

        var dashboard = await new SqlWhtLifecycleDashboardQuery(db).GetAsync(
            asOf: new DateOnly(2026, 12, 31)
        );

        var row = dashboard.Filings.Single(f => f.Id == filing.Id);
        row.DerivedStatus.Should()
            .Be(
                Form41Status.Filed,
                because: "Filed status is preserved regardless of as-of date — the regulator already received it"
            );
        row.EstimatedPenalty.Should().BeNull(because: "no penalty on Filed filings");
    }

    [Fact]
    public async Task UnfiledFilingInsideGracePeriod_StaysUnfiled_NoPenalty()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);

        // Q2 2026 due date = 2026-07-30. As-of 2026-07-15 → still
        // within grace, so derived status = Unfiled (not Overdue).
        var filing = Form41Filing.CreateUnfiled(
            fiscalYear: 2026,
            quarter: 2,
            generatedAtUtc: new DateTime(2026, 7, 5, 10, 0, 0, DateTimeKind.Utc),
            pdfPath: "",
            structuredJsonPath: "",
            totalWhtPayable: MoneyEgp.From(5_000m),
            lineCount: 3
        );
        db.Add(filing);
        await db.SaveChangesAsync();

        var dashboard = await new SqlWhtLifecycleDashboardQuery(db).GetAsync(
            asOf: new DateOnly(2026, 7, 15)
        );

        var row = dashboard.Filings.Single(f => f.Id == filing.Id);
        row.DerivedStatus.Should()
            .Be(
                Form41Status.Unfiled,
                because: "the grace period (30 days post-quarter-end) is still open on as-of 2026-07-15"
            );
        row.DaysOverdue.Should().Be(0);
        row.EstimatedPenalty.Should().BeNull();
    }

    [Fact]
    public async Task EstimatedPenalty_CapsAt25Percent_OfWhtTotal()
    {
        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);

        // 30 months overdue would naively be 30%; the cap clamps
        // at 25% (placeholder MVP rule).
        var filing = Form41Filing.CreateUnfiled(
            fiscalYear: 2024,
            quarter: 1,
            generatedAtUtc: new DateTime(2024, 4, 5, 10, 0, 0, DateTimeKind.Utc),
            pdfPath: "",
            structuredJsonPath: "",
            totalWhtPayable: MoneyEgp.From(10_000m),
            lineCount: 1
        );
        db.Add(filing);
        await db.SaveChangesAsync();

        // As-of 2027-05-01 → due date 2024-04-30 → ~36 months overdue.
        var dashboard = await new SqlWhtLifecycleDashboardQuery(db).GetAsync(
            asOf: new DateOnly(2027, 5, 1)
        );

        var row = dashboard.Filings.Single(f => f.Id == filing.Id);
        row.DerivedStatus.Should().Be(Form41Status.Overdue);
        row.EstimatedPenalty.Should()
            .Be(
                2_500m,
                because: "penalty caps at 25% × 10,000 = 2,500 EGP regardless of how many months past the cap point"
            );
    }

    private static async Task EnsureCompanyAsync(AppDbContext db)
    {
        if (await db.Set<Company>().AnyAsync())
            return;
        db.Add(
            new Company(
                legalName: new ArabicEnglishText("شركة", "Test Company SAE"),
                taxRegistrationNumber: EgyptianTin.Parse("123456789"),
                commercialRegistrationNumber: "CR-1",
                address: PostalAddress.Create(
                    new ArabicEnglishText("القاهرة", "Cairo"),
                    "Cairo",
                    "Downtown",
                    "Tahrir",
                    "12",
                    postalCode: "11511"
                ),
                taxpayerActivityCode: "0001"
            )
        );
        await db.SaveChangesAsync();
    }
}
