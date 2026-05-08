using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
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
            adminRole = new Role(
                code: "ADMIN",
                name: new ArabicEnglishText("مسؤول النظام", "Administrator"),
                requiresMfa: true
            );
            db.Add(adminRole);
            await stdout
                .WriteLineAsync("[seed] Created role: ADMIN (Administrator, MFA required)")
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
                passwordMustChange: true
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

        await db.SaveChangesAsync(cancellationToken);

        await stdout
            .WriteLineAsync("[seed] DONE — Stage 2 identity bootstrap ready.")
            .WaitAsync(cancellationToken);
        await stdout
            .WriteLineAsync(
                "[seed] (VAT category / customer / item / chart-of-accounts seeding lands with US1.)"
            )
            .WaitAsync(cancellationToken);

        return 0;
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
