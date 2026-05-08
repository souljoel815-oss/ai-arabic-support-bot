using System.Diagnostics;
using System.IO.Compression;
using EgyptTax.Application.Inspection;
using EgyptTax.Domain.Accounting;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.Invoices;
using EgyptTax.Domain.MasterData;
using EgyptTax.Domain.Workflow;
using EgyptTax.Infrastructure.FileStorage;
using EgyptTax.Infrastructure.Inspection;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.Infrastructure.Reports;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Inspection;

/// <summary>
/// US9 / SC-014 / T228 — the Tax-Inspection Bundle SLA. Per the
/// spec: "A Tax-Inspection Bundle generated for any fiscal quarter
/// containing up to 5,000 documents is produced in under 5 minutes
/// […] independently re-verifiable by the bundled verification
/// script in under 60 seconds on a clean Windows machine with no
/// network access."
///
/// This test exercises BOTH halves end-to-end: seed 5,000 posted
/// sales invoices + their journal entries, time the bundle build,
/// time the verify-bundle.ps1 verification of the extracted ZIP.
/// Failing this test means we've regressed the inspector
/// experience the whole feature exists for.
///
/// OPT-IN. Wall-clock budget here is 5 min build + 1 min verify =
/// up to ~6 min, plus seed time. Set the environment variable
/// EGYPTTAX_RUN_PERF=1 to enable. Default is "skip silently" so
/// regular dev + CI runs stay fast; this test is meant to run on
/// demand and on the nightly perf suite (T255).
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Slow")]
public class BundleEndToEndTests(SqlServerFixture fixture) : IDisposable
{
    private const int DocumentCount = 5_000;
    private static readonly TimeSpan BuildBudget = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VerifyBudget = TimeSpan.FromSeconds(60);

    private readonly SqlServerFixture _fixture = fixture;
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(), $"egypttax-bundle-perf-{Guid.NewGuid():N}");

    [Fact]
    public async Task Bundle_5000Documents_BuildsUnder5Min_AndVerifiesUnder60s_PerSC014()
    {
        if (Environment.GetEnvironmentVariable("EGYPTTAX_RUN_PERF") != "1")
        {
            // Opt-in only — see the class summary.
            return;
        }
        if (!OperatingSystem.IsWindows())
        {
            // verify-bundle.ps1 is Windows PowerShell 5.1.
            return;
        }

        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var (vat, customer, user) = await SeedMastersAsync(db);

        // Seed 5,000 posted sales invoices + matching balanced
        // journal entries. Done via batched SaveChanges (500/batch)
        // with auto-detect-changes off so the seed itself doesn't
        // dominate wall-clock time.
        var seedSw = Stopwatch.StartNew();
        await SeedPostedSalesInvoicesAsync(db, vat, customer, user.Id, DocumentCount);
        seedSw.Stop();

        // Build phase — time ONLY the BuildAsync call so seed cost
        // doesn't bleed into the SC-014 budget.
        var store = new FileSystemAttachmentStore(_tempRoot, new TestClock(DateTime.UtcNow));
        var clock = new TestClock(new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc));
        var builder = new InspectionBundleBuilder(db, store, clock,
            new SqlTrialBalanceReportQuery(db));

        var buildSw = Stopwatch.StartNew();
        var result = await builder.BuildAsync(new InspectionBundleRequest(
            PeriodStart: new DateOnly(2026, 4, 1),
            PeriodEnd: new DateOnly(2026, 6, 30),
            GeneratedByUserId: user.Id,
            AllowDrafts: false), CancellationToken.None);
        buildSw.Stop();

        buildSw.Elapsed.Should().BeLessThan(BuildBudget,
            because: $"SC-014 — bundle for {DocumentCount:N0}-document quarter MUST build in under {BuildBudget.TotalMinutes:F0} min. Took {buildSw.Elapsed.TotalSeconds:F1}s. Seed took {seedSw.Elapsed.TotalSeconds:F1}s (excluded from budget).");

        // Sanity — bundle should reflect the seeded data.
        result.Manifest.Files.Count.Should().BeGreaterThan(5,
            because: "the bundle MUST carry the readme, verifier, audit extract, the five register PDFs, and (per seed) no attachments");

        // Extract bundle to disk for the verifier.
        var extractDir = Path.Combine(_tempRoot, "extracted");
        Directory.CreateDirectory(extractDir);
        await using (var zipStream = new MemoryStream(result.ZipBytes))
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            zip.ExtractToDirectory(extractDir);
        }

        // Verify phase — time ONLY the script invocation.
        var scriptPath = Path.Combine(extractDir, "verify-bundle.ps1");
        var verifySw = Stopwatch.StartNew();
        var (exitCode, output) = RunVerifierScript(scriptPath, extractDir);
        verifySw.Stop();

        exitCode.Should().Be(0,
            because: $"the bundled verify-bundle.ps1 MUST report PASS on a pristine bundle. Output:\n{output}");
        verifySw.Elapsed.Should().BeLessThan(VerifyBudget,
            because: $"SC-014 — bundled verifier MUST complete in under {VerifyBudget.TotalSeconds:F0}s on a clean Windows machine. Took {verifySw.Elapsed.TotalSeconds:F1}s.");
    }

    private static async Task<(VatCategory Vat, Customer Customer, User User)> SeedMastersAsync(AppDbContext db)
    {
        var vat = new VatCategory(
            code: "Standard", name: new ArabicEnglishText("قياسي", "Standard"),
            ratePercent: 14m, effectiveFromDate: new DateOnly(2026, 1, 1),
            effectiveToDate: null, recoverableInputVat: true);
        var customer = new Customer(
            code: $"CUS-{Guid.NewGuid():N}".Substring(0, 12),
            name: new ArabicEnglishText("عميل أداء", "Perf Customer"),
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "10", postalCode: "11511"),
            taxProfile: CustomerTaxProfile.B2BRegistered(
                EgyptianTin.Parse("987654321"),
                vatExemption: false,
                defaultSalesVatCategoryId: null));
        var user = new User(
            email: $"perf-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        db.Add(vat); db.Add(customer); db.Add(user);
        await db.SaveChangesAsync();
        return (vat, customer, user);
    }

    private static async Task SeedPostedSalesInvoicesAsync(
        AppDbContext db, VatCategory vat, Customer customer, Guid userId, int count)
    {
        // Disable change tracking detect-changes for the seed loop —
        // EF re-walks every tracked entity on every Add otherwise,
        // which turns this into an O(n²) operation.
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            const int batchSize = 500;
            var periodStart = new DateOnly(2026, 4, 1);
            for (var batchStart = 0; batchStart < count; batchStart += batchSize)
            {
                for (var i = batchStart; i < Math.Min(batchStart + batchSize, count); i++)
                {
                    // Spread invoices across the Q2 2026 quarter so
                    // they all fall inside the bundle's period filter.
                    var docDate = periodStart.AddDays(i % 91);
                    var invoice = SalesInvoice.CreateDraft(customer.Id, customer.TaxProfile, docDate);
                    invoice.AddLine(itemId: Guid.NewGuid(), quantity: 1m,
                        unitPrice: MoneyEgp.From(100m + (i % 50)),
                        vatCategoryId: vat.Id, vatRatePercent: vat.RatePercent);
                    invoice.MarkPosted(
                        documentNumber: $"PERF-{i:D6}",
                        postedByUserId: userId,
                        postedAtUtc: docDate.ToDateTime(TimeOnly.MinValue).AddHours(10),
                        postingMode: DocumentPostingMode.UnapprovedDirect,
                        approvalEnabled: false);

                    var je = JournalEntry.Create(
                        sourceDocumentId: invoice.Id,
                        sourceDocumentNumber: invoice.DocumentNumber!,
                        sourceDocumentType: DocumentType.SalesInvoice,
                        postedAtUtc: invoice.PostedAtUtc!.Value,
                        lines: new[]
                        {
                            (AccountCode: "1200", Debit: invoice.GrandTotal, Credit: MoneyEgp.Zero, Description: "AR"),
                            (AccountCode: "4000", Debit: MoneyEgp.Zero, Credit: invoice.NetBeforeVat, Description: "Revenue"),
                            (AccountCode: "2110", Debit: MoneyEgp.Zero, Credit: invoice.VatTotal, Description: "Output VAT"),
                        });

                    db.Add(invoice);
                    db.Add(je);
                }
                db.ChangeTracker.DetectChanges();
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    private static async Task EnsureCompanyAsync(AppDbContext db)
    {
        if (await db.Set<Company>().AnyAsync()) return;
        db.Add(new Company(
            legalName: new ArabicEnglishText("شركة", "Test Company SAE"),
            taxRegistrationNumber: EgyptianTin.Parse("123456789"),
            commercialRegistrationNumber: "CR-1",
            address: PostalAddress.Create(
                new ArabicEnglishText("القاهرة", "Cairo"),
                "Cairo", "Downtown", "Tahrir", "12", postalCode: "11511"),
            taxpayerActivityCode: "0001"));
        await db.SaveChangesAsync();
    }

    private static (int ExitCode, string Output) RunVerifierScript(string scriptPath, string bundleDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-BundleDirectory");
        psi.ArgumentList.Add(bundleDirectory);

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to spawn powershell.exe.");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        // Generous wait — the perf budget assertion is what catches
        // a slow run, not this timeout.
        if (!p.WaitForExit(TimeSpan.FromMinutes(5)))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"verify-bundle.ps1 did not complete within 5 min. Partial stdout:\n{stdout}");
        }
        return (p.ExitCode, stdout + Environment.NewLine + stderr);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
        GC.SuppressFinalize(this);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
