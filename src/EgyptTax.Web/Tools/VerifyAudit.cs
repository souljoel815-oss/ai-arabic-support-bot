using EgyptTax.Application.Audit;
using EgyptTax.Domain.Audit;
using EgyptTax.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgyptTax.Web.Tools;

/// <summary>
/// T163 / FR-028 — `verify-audit` CLI verb. Replays the entire
/// audit chain via <see cref="AuditChainVerifier"/>, compares
/// against the latest checkpoint (when one exists), and prints a
/// human-readable report. Designed for the operator runbook
/// per contracts/audit-chain-verifier.md §"Operator runbook
/// integration": run after backups, after schema migrations, or
/// any time the auditor wants externally-runnable proof of chain
/// integrity.
///
/// Exit codes:
///   0 — chain verified clean.
///   1 — chain has findings (tamper, missing index, checkpoint
///       mismatch, etc.).
///   2 — usage / argument error.
///   3 — verifier was unable to run (no DB connection, etc.).
///
/// Flags:
///   --json            Emit the report as a JSON object instead of
///                     the human-readable text format.
///   --max N           Cap the number of entries scanned (defaults
///                     to int.MaxValue — full chain).
/// </summary>
public static class VerifyAudit
{
    public static bool IsVerifyAuditInvocation(string[] args) =>
        args.Length > 0
        && string.Equals(args[0], "verify-audit", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(
        string[] args,
        AppDbContext db,
        IAuditCheckpointStore checkpointStore,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(checkpointStore);

        var asJson = args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase));
        var maxEntries = ParseIntFlag(args, "--max") ?? int.MaxValue;

        await stdout
            .WriteLineAsync($"[verify-audit] Loading entries (cap={maxEntries:N0}) …")
            .WaitAsync(cancellationToken);

        var entries = await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .OrderBy(e => e.Index)
            .Take(maxEntries)
            .ToListAsync(cancellationToken);

        var checkpoint = await checkpointStore.ReadLatestAsync(cancellationToken);

        await stdout
            .WriteLineAsync($"[verify-audit] Verifying {entries.Count:N0} entries …")
            .WaitAsync(cancellationToken);
        if (checkpoint is not null)
        {
            await stdout
                .WriteLineAsync(
                    $"[verify-audit] Comparing against checkpoint at index {checkpoint.LastIndex:N0}."
                )
                .WaitAsync(cancellationToken);
        }
        else
        {
            await stdout
                .WriteLineAsync(
                    "[verify-audit] No checkpoint on file (chain hash-only verification)."
                )
                .WaitAsync(cancellationToken);
        }

        var report = AuditChainVerifier.Verify(entries, checkpoint);

        if (asJson)
        {
            var jsonPayload = BuildJson(
                report,
                entries.Count,
                maxEntries == entries.Count && entries.Count > 0
            );
            await stdout.WriteLineAsync(jsonPayload).WaitAsync(cancellationToken);
        }
        else
        {
            await WriteHumanReportAsync(stdout, report, entries.Count, cancellationToken);
        }

        return report.IsValid ? 0 : 1;
    }

    private static async Task WriteHumanReportAsync(
        TextWriter stdout,
        AuditChainReport report,
        int entriesScanned,
        CancellationToken cancellationToken
    )
    {
        if (report.IsValid)
        {
            await stdout
                .WriteLineAsync($"[verify-audit] ✓ CHAIN VALID across {entriesScanned:N0} entries.")
                .WaitAsync(cancellationToken);
            return;
        }

        await stdout
            .WriteLineAsync(
                $"[verify-audit] ✗ CHAIN INTEGRITY FAILED — {report.Findings.Count} finding(s):"
            )
            .WaitAsync(cancellationToken);
        foreach (var f in report.Findings)
        {
            var line = f.Notes is null
                ? $"[verify-audit]   - {f.Kind} at index {f.AtIndex}"
                : $"[verify-audit]   - {f.Kind} at index {f.AtIndex}: {f.Notes}";
            await stdout.WriteLineAsync(line).WaitAsync(cancellationToken);
        }
    }

    private static string BuildJson(AuditChainReport report, int entriesScanned, bool truncated)
    {
        var findings = string.Join(
            ",",
            report.Findings.Select(f =>
                $$"""{"kind":"{{f.Kind}}","atIndex":{{f.AtIndex}},"notes":{{(f.Notes is null ? "null" : "\"" + Escape(f.Notes) + "\"")}}}"""
            )
        );
        return $$"""{"isValid":{{(report.IsValid ? "true" : "false")}},"entriesScanned":{{entriesScanned}},"truncated":{{(truncated ? "true" : "false")}},"findings":[{{findings}}]}""";
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static int? ParseIntFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (
                string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[i + 1], out var n)
                && n > 0
            )
            {
                return n;
            }
        }
        return null;
    }
}
