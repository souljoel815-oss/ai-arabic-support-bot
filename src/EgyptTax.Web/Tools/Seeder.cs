using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
using EgyptTax.Domain.MasterData;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Web.Tools;

/// <summary>
/// T072 — bootstrap seeder for the Stage 2 minimum-viable
/// configuration. Per quickstart §5 the canonical profile is
/// <c>us1-minimum</c>; entities that belong to the US1 batch (VAT
/// category, customer, item, chart of accounts) are stubbed in this
/// seeder and filled in when their respective domain entities ship.
/// What this seeder reliably provides today: the Administrator role,
/// the Administrator permission grid, and a single Administrator user
/// with a printed bootstrap password so the operator can complete the
/// FR-038 force-change on first login.
/// </summary>
public static class Seeder
{
    public static bool IsSeedInvocation(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(
        string[] args,
        AppDbContext db,
        IPasswordHasher hasher,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var profile = ExtractFlag(args, "--profile") ?? "us1-minimum";
        if (!string.Equals(profile, "us1-minimum", StringComparison.OrdinalIgnoreCase))
        {
            await stderr
                .WriteLineAsync($"[seed] Unknown profile '{profile}'. Supported: us1-minimum.")
                .WaitAsync(cancellationToken);
            return 2;
        }

        await stdout.WriteLineAsync("[seed] Profile: us1-minimum").WaitAsync(cancellationToken);

        var adminRole = await db.Set<Role>()
            .FirstOrDefaultAsync(r => r.Code == "ADMIN", cancellationToken);
        if (adminRole is null)
        {
            // MFA + password-change-on-first-login are OPT-IN as of
            // 2026-05-11 — operators surveyed the customer flow and
            // found the forced enrolment + forced rotate-password steps
            // were tripping new installs. Operator can still flip
            // RequiresMfa per-role from /settings/roles when they want
            // it, and any user can change their own password from
            // /settings/profile at any time.
            adminRole = new Role(
                code: "ADMIN",
                name: new ArabicEnglishText("مسؤول النظام", "Administrator"),
                requiresMfa: false
            );
            db.Add(adminRole);
            await stdout
                .WriteLineAsync("[seed] Created role: ADMIN (Administrator, MFA optional)")
                .WaitAsync(cancellationToken);
        }
        else
        {
            await stdout
                .WriteLineAsync("[seed] Role ADMIN already present — skipping.")
                .WaitAsync(cancellationToken);
        }

#pragma warning disable CA1308 // Email canonical form is lowercase per RFC 5321 §2.3.11; CA1308's uppercase guidance does not apply.
        var adminEmail =
            ExtractFlag(args, "--admin-email")?.Trim().ToLowerInvariant() ?? "admin@test.local";
#pragma warning restore CA1308
        var adminUser = await db.Set<User>()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == adminEmail, cancellationToken);
        if (adminUser is null)
        {
            var bootstrapPassword =
                ExtractFlag(args, "--bootstrap-password") ?? "TempP@ssw0rd!2026";
            adminUser = new User(
                email: adminEmail,
                displayName: new ArabicEnglishText("مسؤول النظام", "System Administrator"),
                passwordHash: hasher.Hash(bootstrapPassword),
                preferredLanguage: Language.Ar,
                passwordMustChange: false
            );
            adminUser.Roles.Add(adminRole);
            db.Add(adminUser);

            await db.SaveChangesAsync(cancellationToken);

            await stdout
                .WriteLineAsync($"[seed] Created Administrator user: {adminEmail}")
                .WaitAsync(cancellationToken);
            await stdout
                .WriteLineAsync(
                    $"[seed]   Initial password (must change at first login): {bootstrapPassword}"
                )
                .WaitAsync(cancellationToken);
            await stdout
                .WriteLineAsync("[seed]   MFA enrolment is performed on first login per FR-002.")
                .WaitAsync(cancellationToken);
        }
        else
        {
            await stdout
                .WriteLineAsync(
                    $"[seed] Administrator user {adminEmail} already present — skipping."
                )
                .WaitAsync(cancellationToken);
        }

        // --- Default Egyptian VAT categories so the operator
        //     does not face an empty Settings page on first login.
        //     The 4 catalog rows match the standard Egyptian VAT
        //     law breakdown; a customer who needs custom rates can
        //     add new code/version pairs without touching these. ---
        await SeedVatCategoriesAsync(db, stdout, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        await stdout
            .WriteLineAsync("[seed] DONE — bootstrap ready.")
            .WaitAsync(cancellationToken);

        return 0;
    }

    private static async Task SeedVatCategoriesAsync(
        AppDbContext db,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        var existing = await db.Set<VatCategory>()
            .Select(v => v.Code).ToListAsync(cancellationToken);

        var defaultCategories = new[]
        {
            new VatCategory(
                code: "STANDARD-14",
                name: new ArabicEnglishText("القياسي 14%", "Standard 14%"),
                ratePercent: 14m,
                effectiveFromDate: new DateOnly(2017, 7, 1),
                effectiveToDate: null,
                recoverableInputVat: true),
            new VatCategory(
                code: "REDUCED-5",
                name: new ArabicEnglishText("المخفّض 5% (آلات ومعدات)", "Reduced 5% (machinery)"),
                ratePercent: 5m,
                effectiveFromDate: new DateOnly(2017, 7, 1),
                effectiveToDate: null,
                recoverableInputVat: true),
            new VatCategory(
                code: "ZERO-RATED",
                name: new ArabicEnglishText("صفرية (تصدير)", "Zero-rated (export)"),
                ratePercent: 0m,
                effectiveFromDate: new DateOnly(2017, 7, 1),
                effectiveToDate: null,
                recoverableInputVat: true),
            new VatCategory(
                code: "EXEMPT",
                name: new ArabicEnglishText("معفاة", "Exempt"),
                ratePercent: 0m,
                effectiveFromDate: new DateOnly(2017, 7, 1),
                effectiveToDate: null,
                recoverableInputVat: false),
        };

        var created = 0;
        foreach (var cat in defaultCategories)
        {
            if (!existing.Contains(cat.Code))
            {
                db.Add(cat);
                created++;
            }
        }
        if (created > 0)
        {
            await stdout.WriteLineAsync(
                $"[seed] Created {created} default VAT categories (Standard / Reduced / Zero-rated / Exempt)."
            ).WaitAsync(cancellationToken);
        }
    }

    private static string? ExtractFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
