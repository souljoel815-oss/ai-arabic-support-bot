using EgyptTax.Application.Identity;
using EgyptTax.Domain.Identity;
using EgyptTax.Infrastructure.Identity;
using EgyptTax.Infrastructure.Persistence;
using EgyptTax.SharedKernel;
using EgyptTax.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Web.Tools;

/// <summary>
/// FR-038 — out-of-band administrator recovery. Invoked via the Web
/// host's command-line surface (<c>dotnet run -- recover-admin --email
/// admin@firm.eg --new-password &quot;...&quot;</c>) when no administrator
/// with current credentials remains. Resets the user's password and
/// stages an <see cref="AdminRecoveryRecord"/> row that the next
/// application boot drains through the proper FR-028 audit-log path —
/// this CLI itself cannot append to the audit chain because the
/// <c>trg_audit_log_append_only</c> trigger rejects writes whose
/// <c>APP_NAME()</c> is not the production application identifier.
/// </summary>
public static class AdminRecover
{
    /// <summary>
    /// Returns true if <paramref name="args"/> looked like an admin-
    /// recovery invocation (regardless of success); the host should
    /// exit instead of starting the web server in that case.
    /// </summary>
    public static bool IsRecoveryInvocation(string[] args) =>
        args.Length > 0 && string.Equals(args[0], "recover-admin", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(
        string[] args,
        AppDbContext db,
        IPasswordHasher hasher,
        IClock clock,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var email = ExtractFlag(args, "--email");
        var newPassword = ExtractFlag(args, "--new-password");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
        {
            await stderr.WriteLineAsync(
                "Usage: recover-admin --email <email> --new-password <password>")
                .WaitAsync(cancellationToken);
            return 2;
        }

#pragma warning disable CA1308 // Email canonical form is lowercase per RFC 5321 §2.3.11; CA1308's uppercase guidance does not apply.
        var canonicalEmail = email.Trim().ToLowerInvariant();
#pragma warning restore CA1308
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == canonicalEmail, cancellationToken);
        if (user is null)
        {
            await stderr.WriteLineAsync($"User with email '{canonicalEmail}' not found.")
                .WaitAsync(cancellationToken);
            return 3;
        }

        // Apply the new password and mark must-change so the recovered
        // admin re-authenticates on first login.
        user.SetPassword(hasher.Hash(newPassword), mustChange: true);

        var record = new AdminRecoveryRecord(
            targetUserId: user.Id,
            targetEmail: user.Email,
            recoveredAtUtc: clock.UtcNow,
            machineName: Environment.MachineName,
            operatorIdentity: Environment.UserName);
        db.Add(record);

        await db.SaveChangesAsync(cancellationToken);

        await stdout.WriteLineAsync(
            $"Admin recovery completed for {user.Email}. Audit event will be emitted on next application start.")
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
