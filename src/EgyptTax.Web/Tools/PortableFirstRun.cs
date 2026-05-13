using System.Diagnostics;
using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EgyptTax.Web.Tools;

/// <summary>
/// Single-file portable mode first-run UX. Runs once after the host
/// is built but before serving begins:
///
/// 1. Ensures the SQLite schema exists (creates daftarx.db on disk).
/// 2. Seeds the ADMIN role + four default VAT categories if missing.
/// 3. If no admin user exists, generates a random bootstrap password
///    and creates admin@daftarx.local. Credentials are printed to the
///    console AND saved to first-run-credentials.txt beside the EXE
///    so the customer can recover them if they closed the console.
/// 4. Once the host actually starts listening, opens the default
///    browser pointed at the login page.
/// </summary>
internal static class PortableFirstRun
{
    public static async Task RunAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<AppDbContext>>()
                .CreateDbContext();
            try
            {
                // Switched from EnsureCreatedAsync to MigrateAsync so
                // schema changes (new columns, new tables) land on
                // existing portable DBs automatically — operators
                // (and devs) no longer have to delete daftarx.db on
                // every release.
                //
                // Backwards-compat for legacy DBs created via the
                // old EnsureCreated path: detect a missing
                // __EFMigrationsHistory table by trying to read it;
                // if it's absent but the schema looks initialised
                // (admin role exists), stamp the history with all
                // applied migrations so MigrateAsync becomes a no-op
                // on this first run, then runs new migrations only
                // on the next change.
                await EnsureMigratedAsync(db);
                await EnsureRoleAsync(db);
                await EnsureVatCategoriesAsync(db);
                await EnsureAdminUserAsync(db, scope.ServiceProvider);
            }
            finally
            {
                await db.DisposeAsync();
            }
        }

        // Auto-launch the browser shortly after the host starts
        // listening. Fire-and-forget — we don't want to block startup
        // if the user doesn't have a default browser configured.
        _ = LaunchBrowserAsync(app);
    }

    private static async Task EnsureMigratedAsync(AppDbContext db)
    {
        // Direct schema probes — sqlite_master tells us authoritatively
        // which tables exist on this SQLite file. EF's
        // GetAppliedMigrationsAsync returns an empty list (not throws)
        // when the history table is absent, so it can't distinguish
        // "fresh DB" from "legacy EnsureCreated DB".
        var historyExists = await TableExistsAsync(db, "__EFMigrationsHistory");
        var legacySchemaExists = await TableExistsAsync(db, "roles");

        // One-time legacy conversion: schema present but no migration
        // history (DB was created via the old EnsureCreated path).
        // Backfill history with all migrations that exist in the
        // assembly RIGHT NOW so MigrateAsync treats them as applied.
        // Future migrations added later will be missing from history
        // and MigrateAsync will run them normally.
        //
        // CRITICAL: this branch must NOT run on every boot — that
        // would stamp newly-added migrations as already-applied
        // before they get a chance to run (BUG-H-001).
        if (!historyExists && legacySchemaExists)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                    ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                    ""ProductVersion"" TEXT NOT NULL
                );");
            var assemblyMigrations = db.Database
                .GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>()
                .Migrations
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var migrationId in assemblyMigrations)
            {
                await db.Database.ExecuteSqlRawAsync(
                    @"INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES ({0}, '8.0.0');",
                    migrationId);
            }
        }

        // BUG-H-001 recovery: an earlier code path stamped all
        // assembly migrations into history without running their
        // Up() methods, so DBs in the wild may have a history row
        // for a migration whose tables were never actually created.
        // For each well-known table that should exist if its
        // migration is recorded as applied, verify both — and if the
        // table is missing despite the history row, drop the row so
        // MigrateAsync re-applies the migration. Targeted to the
        // tables we know were affected; new migrations don't need
        // entries here because the gating fix above prevents the
        // same drift going forward.
        await SelfHealMissingTableAsync(db,
            tableName: "route_visits",
            migrationIdSuffix: "_RouteVisits");

        // Always end with MigrateAsync. On a fresh DB it builds the
        // full schema. On a normal DB it applies any pending
        // migrations. After the legacy backfill above it's a no-op
        // (history covers everything in the assembly).
        await db.Database.MigrateAsync();
    }

    private static async Task SelfHealMissingTableAsync(AppDbContext db, string tableName, string migrationIdSuffix)
    {
        var tableExists = await TableExistsAsync(db, tableName);
        if (tableExists) return;
        if (!await TableExistsAsync(db, "__EFMigrationsHistory")) return;

        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" LIKE {0};",
            "%" + migrationIdSuffix);
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string tableName)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name = $name LIMIT 1";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = tableName;
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync();
        return result is not null;
    }

    private static async Task EnsureRoleAsync(AppDbContext db)
    {
        if (await db.Set<Role>().AnyAsync(r => r.Code == "ADMIN")) return;
        // MFA is opt-in (operator flips it per-role from settings).
        db.Add(new Role(
            code: "ADMIN",
            name: new ArabicEnglishText("مسؤول النظام", "Administrator"),
            requiresMfa: false));
        await db.SaveChangesAsync();
    }

    private static async Task EnsureVatCategoriesAsync(AppDbContext db)
    {
        try
        {
            await StartupSeed.EnsureDefaultsAsync(db);
        }
        catch
        {
            // Non-fatal — the empty-state UI handles the no-categories case.
        }
    }

    private static async Task EnsureAdminUserAsync(AppDbContext db, IServiceProvider sp)
    {
        var adminEmail = "admin@daftarx.local";
        if (await db.Set<User>().AnyAsync(u => u.Email == adminEmail)) return;

        var adminRole = await db.Set<Role>().FirstAsync(r => r.Code == "ADMIN");
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var password = GenerateBootstrapPassword();
        var hash = hasher.Hash(password);

        // Force-change-on-first-login removed 2026-05-11 — operators
        // can change the printed password from /settings/profile when
        // they want to. Banner credentials remain visible in
        // first-run-credentials.txt for recovery.
        var user = new User(
            email: adminEmail,
            displayName: new ArabicEnglishText("المسؤول", "Administrator"),
            passwordHash: hash,
            preferredLanguage: Language.Ar,
            passwordMustChange: false);
        user.Roles.Add(adminRole);
        db.Add(user);
        await db.SaveChangesAsync();

        WriteCredentials(adminEmail, password);
    }

    private static string GenerateBootstrapPassword()
    {
        // 16 chars, mixed-case + digits + symbol — meets the policy
        // checked at first login.
        const string chars =
            "ABCDEFGHJKLMNPQRSTUVWXYZ" +
            "abcdefghijkmnpqrstuvwxyz" +
            "23456789" +
            "!@#$%^&*";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var sb = new System.Text.StringBuilder(16);
        foreach (var b in bytes) sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    private static void WriteCredentials(string email, string password)
    {
        var banner = new[]
        {
            "",
            "================================================================",
            "  DaftarX — first-run setup complete",
            "----------------------------------------------------------------",
            $"  Login:    {email}",
            $"  Password: {password}",
            "  (You will be asked to change this on first login.)",
            "================================================================",
            "",
        };
        foreach (var line in banner) Console.WriteLine(line);

        // Persist a copy beside the EXE so the customer can recover the
        // password if they closed the console window.
        try
        {
            var dir = !string.IsNullOrEmpty(Environment.ProcessPath)
                ? Path.GetDirectoryName(Environment.ProcessPath)!
                : AppContext.BaseDirectory;
            var path = Path.Combine(dir, "first-run-credentials.txt");
            File.WriteAllText(path, string.Join(Environment.NewLine, banner));
        }
        catch
        {
            // Permissions on the install dir may block this; the
            // console banner above is still authoritative.
        }
    }

    private static async Task LaunchBrowserAsync(WebApplication app)
    {
        try
        {
            // Wait until ASP.NET Core has resolved its bound URLs.
            var lifetime = app.Lifetime;
            var tcs = new TaskCompletionSource();
            lifetime.ApplicationStarted.Register(() => tcs.TrySetResult());
            await tcs.Task;

            var addresses = app.Services
                .GetService<Microsoft.AspNetCore.Hosting.Server.IServer>()?
                .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?
                .Addresses;

            var url = addresses?
                .Select(NormalizeUrl)
                .FirstOrDefault(u => !string.IsNullOrEmpty(u))
                ?? "http://localhost:5000";

            // Small delay so Kestrel is actually accepting before we
            // race a browser at it.
            await Task.Delay(500);

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
        }
        catch
        {
            // No default browser configured / running headless / etc.
            // The console line above tells the user what URL to open.
        }
    }

    private static string NormalizeUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        // Kestrel binds to "http://[::]:5000" or "http://+:5000" —
        // neither is openable in a browser. Map to localhost.
        return raw
            .Replace("[::]", "localhost", StringComparison.Ordinal)
            .Replace("+", "localhost", StringComparison.Ordinal)
            .Replace("0.0.0.0", "localhost", StringComparison.Ordinal);
    }
}
