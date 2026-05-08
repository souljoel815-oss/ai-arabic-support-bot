using System.Diagnostics;
using System.IO.Compression;
using EgyptTax.Application.Inspection;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.FileStorage;
using EgyptTax.Infrastructure.Inspection;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.IntegrationTests.Infrastructure;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.IntegrationTests.Inspection;

/// <summary>
/// US9 final-hardening — proves the builder + verify-bundle.ps1
/// agree on the SHA-256 protocol end-to-end. Without this, a
/// regression in either the builder's hashing or the script's
/// parsing could silently ship bundles that fail at the inspector.
/// Tampering with any bundled file MUST cause the script to exit
/// non-zero — that's the bedrock guarantee an inspector relies on.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class BundleVerifierScriptRoundTripTests(SqlServerFixture fixture) : IDisposable
{
    private readonly SqlServerFixture _fixture = fixture;
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"egypttax-bundle-roundtrip-{Guid.NewGuid():N}"
    );

    [Fact]
    public async Task VerifierScript_PassesOnPristineBundle_AndFailsOnTamperedFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            // verify-bundle.ps1 is pure Windows PowerShell 5.1 — skip on
            // other platforms (this repo targets Windows operators per
            // the inspector environment described in FR-048).
            return;
        }

        await using var db = await _fixture.CreateContextAsync();
        await EnsureCompanyAsync(db);
        var user = new User(
            email: $"op-{Guid.NewGuid():N}@firm.eg",
            displayName: new ArabicEnglishText("مشغل", "Op"),
            passwordHash: "argon2id$m=65536,t=3,p=4$AAAA$BBBB",
            preferredLanguage: Language.Ar,
            passwordMustChange: false
        );
        db.Add(user);
        await db.SaveChangesAsync();

        var clock = new TestClock(new DateTime(2026, 5, 7, 11, 0, 0, DateTimeKind.Utc));
        var store = new FileSystemAttachmentStore(_tempRoot, clock);
        var builder = new InspectionBundleBuilder(
            db,
            store,
            clock,
            new EgyptTax.Infrastructure.Reports.SqlTrialBalanceReportQuery(db)
        );

        var result = await builder.BuildAsync(
            new InspectionBundleRequest(
                PeriodStart: new DateOnly(2026, 5, 1),
                PeriodEnd: new DateOnly(2026, 5, 31),
                GeneratedByUserId: user.Id,
                AllowDrafts: false
            ),
            CancellationToken.None
        );

        // T232 sanity — verify-bundle.ps1 MUST be in the manifest under
        // the VerifierScript category, otherwise the rest of this test
        // is meaningless (no script in the bundle to invoke).
        result
            .Manifest.Files.Should()
            .Contain(
                f => f.RelativePath == "verify-bundle.ps1" && f.Category == "VerifierScript",
                because: "T232 — every bundle MUST embed verify-bundle.ps1 so an inspector can verify integrity on a clean Windows machine without the application installed"
            );

        // Extract the produced ZIP to disk — that's the directory the
        // inspector would point the script at after unzipping.
        var extractDir = Path.Combine(_tempRoot, "extracted");
        Directory.CreateDirectory(extractDir);
        await using (var zipStream = new MemoryStream(result.ZipBytes))
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read))
        {
            zip.ExtractToDirectory(extractDir);
        }

        var scriptPath = Path.Combine(extractDir, "verify-bundle.ps1");
        File.Exists(scriptPath)
            .Should()
            .BeTrue(because: "the script MUST land at the bundle root after extraction");

        // Pristine bundle — every on-disk SHA-256 matches the manifest's
        // recorded value, so the script MUST exit 0 with PASS.
        var (cleanExit, cleanOutput) = RunVerifierScript(scriptPath, extractDir);
        cleanExit
            .Should()
            .Be(0, because: "pristine bundle MUST verify clean. Script output:\n" + cleanOutput);
        cleanOutput.Should().Contain("BUNDLE INTEGRITY: PASS");

        // Tamper with the README — flip one byte in place. Size stays
        // identical; SHA-256 changes; the script's per-file hash check
        // MUST catch it.
        var readmePath = Path.Combine(extractDir, "README-FOR-INSPECTOR.md");
        var readmeBytes = await File.ReadAllBytesAsync(readmePath);
        readmeBytes[10] ^= 0xFF;
        await File.WriteAllBytesAsync(readmePath, readmeBytes);

        var (tamperedExit, tamperedOutput) = RunVerifierScript(scriptPath, extractDir);
        tamperedExit
            .Should()
            .Be(
                1,
                because: "the byte-flip in README changes its SHA-256, which the script MUST catch. Script output:\n"
                    + tamperedOutput
            );
        tamperedOutput.Should().Contain("BUNDLE INTEGRITY: FAIL");
        tamperedOutput
            .Should()
            .Contain(
                "README-FOR-INSPECTOR.md",
                because: "the script MUST name the failing file so the inspector knows exactly what was tampered"
            );
    }

    private static (int ExitCode, string Output) RunVerifierScript(
        string scriptPath,
        string bundleDirectory
    )
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

        using var p =
            Process.Start(psi)
            ?? throw new InvalidOperationException(
                "Failed to spawn powershell.exe — is it on PATH?"
            );
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        if (!p.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"verify-bundle.ps1 did not complete within 60s. Partial stdout:\n{stdout}\nstderr:\n{stderr}"
            );
        }
        return (p.ExitCode, stdout + Environment.NewLine + stderr);
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

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
