using System.Diagnostics;
using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using Microsoft.EntityFrameworkCore;

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
                await db.Database.EnsureCreatedAsync();
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
