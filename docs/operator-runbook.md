# Operator Runbook — On-prem EgyptTax installation

**Audience**: System operator / IT administrator at an Egyptian SME running EgyptTax on-prem. Single-tenant single-server deployment is the MVP target.

**Scope**: Installation, ongoing maintenance, backup/restore, incident response, period-close support, and operator-side CLI tools. Day-to-day accountant workflows live in [`accountant-guide.md`](accountant-guide.md). The inspector-facing format spec lives in [`inspector-bundle-format.md`](inspector-bundle-format.md).

**Prerequisites**: Windows Server 2022 (or Windows 11 Pro for a small-business install), SQL Server 2022 (Express edition is sufficient for ≤ 50,000 docs/year), .NET 8 runtime, an NTP server reachable from the host (default `time.windows.com` works in most cases).

---

## Table of contents

1. [Installation](#1-installation)
2. [Initial configuration](#2-initial-configuration)
3. [MFA bootstrap](#3-mfa-bootstrap)
4. [Daily / weekly / monthly maintenance](#4-daily--weekly--monthly-maintenance)
5. [Backup + restore](#5-backup--restore)
6. [NTP failure response (FR-042)](#6-ntp-failure-response-fr-042)
7. [Period close + reopen support (FR-037)](#7-period-close--reopen-support-fr-037)
8. [Audit-chain verifier (FR-028)](#8-audit-chain-verifier-fr-028)
9. [Out-of-band administrator recovery (FR-038)](#9-out-of-band-administrator-recovery-fr-038)
10. [Hangfire dashboard + background jobs](#10-hangfire-dashboard--background-jobs)
11. [Health endpoints + ops monitoring](#11-health-endpoints--ops-monitoring)
12. [Operational logs (R-20 Serilog)](#12-operational-logs-r-20-serilog)
13. [Upgrade path](#13-upgrade-path)
14. [Incident triage](#14-incident-triage)

---

## 1. Installation

> **Status**: The WiX MSI installer ships under T247 (open). Until then, the supported install path is `dotnet publish` + manual service registration; the steps below describe the eventual MSI flow with the `dotnet publish` fallback noted inline.

### 1.1 MSI install (target experience, post-T247)

1. Run `EgyptTax-Setup-{version}.msi` as Administrator.
2. The installer prompts for:
   - **SQL connection string**. Trusted-connection (Windows-auth) is recommended; SQL-auth is supported for cross-domain installs.
   - **Audit-checkpoint storage mode** (FR-028) — see §1.3.
   - **Attachments root directory** — where uploaded receipts are stored (FR-019). Recommended: a dedicated drive (e.g. `D:\EgyptTax\attachments`) with the same backup discipline as the SQL data files.
   - **NTP server** — defaults to `time.windows.com`; corporate installs typically point at the AD domain controller.
   - **Service account** — defaults to `NT AUTHORITY\NetworkService`; use a dedicated domain account if the SQL connection requires it.
3. The installer:
   - Creates the EgyptTax database via EF migrations.
   - Registers the EgyptTax Windows service (auto-start).
   - Opens HTTPS port 443 in the firewall.
   - Runs the `EgyptTax.Web seed` CLI to create the first Administrator user (one-time temp password printed to the install log).
   - Verifies the readiness probe returns `Healthy` before showing the "install complete" screen (T257).

### 1.2 `dotnet publish` fallback (current path, until T247 ships)

```powershell
# Build + publish
dotnet publish src\EgyptTax.Web\EgyptTax.Web.csproj -c Release -r win-x64 --self-contained false -o C:\EgyptTax\app

# Set connection string + checkpoint mode via env or appsettings.Production.json
$env:ConnectionStrings__EgyptTax = "Server=.;Database=EgyptTax;Trusted_Connection=True;TrustServerCertificate=True"

# Apply migrations
dotnet C:\EgyptTax\app\EgyptTax.Web.dll seed --apply-migrations

# Register as Windows service (sc.exe or NSSM)
sc.exe create EgyptTax binPath= "dotnet C:\EgyptTax\app\EgyptTax.Web.dll" start= auto
sc.exe start EgyptTax
```

### 1.3 Audit-checkpoint storage mode (FR-028)

Two backed implementations:

- **`SqlSchemaCheckpointStore`** (default, recommended for most installs): writes the integrity checkpoint to a separate SQL schema (`audit_meta`). Operators restoring the database get the checkpoints atomically with the audit log. Requires the SQL service account to have schema-creation rights at install time.
- **`FileSystemCheckpointStore`**: writes the checkpoint to a file outside the database (typically `D:\EgyptTax\audit-checkpoints\`). Use this when SQL `sysadmin` privileges cannot be separated from the application service account — the file lives outside SQL's reach so a `sysadmin` who tries to truncate the audit-log tail can't quietly truncate the checkpoint too. Trade-off: backup discipline now spans the DB AND the file directory; restore must restore both atomically (see §5).

The choice is install-time and persistent. Switching modes after the fact requires running the `migrate-checkpoints` CLI (Near-term).

---

## 2. Initial configuration

After install, sign in with the administrator account (temp password from the install log), force-change the password, and visit `/settings/company`:

1. **Company profile** — legal name (Arabic + English), Tax Identification Number (TIN, 9 digits), Commercial Registration Number, registered address, taxpayer activity code (the regulator-assigned ETA code).
2. **Fiscal year start month** — drives the income-tax return boundaries + the depreciation job's first/last-month logic. Egyptian SMEs typically use January (1).
3. **Default language + currency** — Arabic + EGP for most installs.
4. **Logo** — optional; appears on the invoice PDFs.

Under `/settings/`:
- **VAT categories** — seed Standard 14% (FR-022 default rate) effective from the install date.
- **WHT categories** — seed Services 5% / Goods 1% / etc per the rates in force when you install (the system supports effective-from-dated supersession so future rate changes are non-destructive).
- **Expense categories** — add the categories your business uses; default-deductible defaults are operator-settable.
- **Tax periods** — pre-create the months you'll be operating in (the period-lock guard enforces FR-037 on the first lock).

---

## 3. MFA bootstrap

Per FR-002, every Administrator and Approver MUST have MFA enabled. The MSI install seeds the first Administrator with `passwordMustChange = true` AND `requiresMfa = true`; on first sign-in:

1. Operator enters the temp password.
2. System forces a password change.
3. System forces MFA enrollment — displays a TOTP QR code (RFC 6238 / 30s window). Scan with any authenticator app (Microsoft Authenticator, Google Authenticator, Authy).
4. Operator enters the 6-digit code to confirm the secret was successfully installed.
5. System records `mfaEnrolledAtUtc` and the operator can proceed.

The TOTP secret is encrypted at rest using Windows DPAPI scoped to the service account — the `mfa_secret_encrypted` column is opaque even to a SQL `sysadmin` reading the row directly.

**To enroll MFA on a subsequent user**: the user themselves enrolls on first sign-in by the same flow. The operator does NOT see the user's TOTP secret.

---

## 4. Daily / weekly / monthly maintenance

### Daily

- **Confirm the EgyptTax service is running**: `Get-Service EgyptTax | Select Status`. If not running, check `logs/egypttax-{date}.log` for the most recent crash + restart via `Start-Service EgyptTax`.
- **Skim ETA Dashboard** for failed submissions. Persistent failures (more than 1 retry exhausted) need accountant attention or operator investigation depending on the error.
- **Confirm the readiness probe is healthy**: `Invoke-WebRequest https://localhost/api/v1/health/ready -UseBasicParsing` should return HTTP 200 with body `{"status":"Healthy"}`.

### Weekly

- **Review the Hangfire dashboard** at `https://localhost/hangfire`. Confirm the recurring jobs are firing on schedule:
  - `audit-checkpoint` — every 1k entries OR every 15 min.
  - `eta-retry` — every 5 min.
  - `supplier-tin-revalidation` — daily at 03:00.
  - `ntp-health-check` — every 15 min.
  - `monthly-depreciation` — first day of each month at 02:00.
- **Verify backups are landing** — see §5.
- **Check disk space** on the attachments drive + the SQL data drive. Receipts can grow surprisingly fast for retail businesses.

### Monthly

- **Run the audit-chain verifier** — see §8. A clean run is part of the month-end ritual; a tampered result demands immediate investigation.
- **Coordinate with the accountant** on month-end period lock (FR-037) — see §7.
- **Rotate Serilog log files** — automatic via `retainedFileCountLimit: 31`, but check that the `logs/` directory size is bounded.

---

## 5. Backup + restore

The minimal restore unit is **all three of**: SQL database + attachments directory + (if `FileSystemCheckpointStore` is enabled) the audit-checkpoint directory. Restoring any subset puts the audit chain in an inconsistent state — the verifier will flag it.

### Backup script (recommended)

```powershell
# Stop the service so no writes happen mid-backup.
Stop-Service EgyptTax

# 1. SQL backup
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
sqlcmd -S . -E -Q "BACKUP DATABASE [EgyptTax] TO DISK='D:\Backups\EgyptTax-$ts.bak' WITH CHECKSUM, COMPRESSION"

# 2. Attachments
Compress-Archive -Path "D:\EgyptTax\attachments\*" -DestinationPath "D:\Backups\attachments-$ts.zip"

# 3. Audit checkpoints (FileSystemCheckpointStore only — skip if SqlSchemaCheckpointStore)
Compress-Archive -Path "D:\EgyptTax\audit-checkpoints\*" -DestinationPath "D:\Backups\audit-checkpoints-$ts.zip"

# Restart
Start-Service EgyptTax
```

Schedule via Task Scheduler nightly at e.g. 02:30 (after the audit-checkpoint job has run + before the monthly-depreciation job's window). Off-host the `D:\Backups` directory to a separate disk / NAS / cloud bucket — a backup that lives on the same disk as the source isn't a backup.

### Restore

```powershell
Stop-Service EgyptTax

# Reverse order — SQL first, then files
sqlcmd -S . -E -Q "RESTORE DATABASE [EgyptTax] FROM DISK='D:\Backups\EgyptTax-{ts}.bak' WITH REPLACE"
Expand-Archive -Path "D:\Backups\attachments-{ts}.zip" -DestinationPath "D:\EgyptTax\attachments" -Force
Expand-Archive -Path "D:\Backups\audit-checkpoints-{ts}.zip" -DestinationPath "D:\EgyptTax\audit-checkpoints" -Force

Start-Service EgyptTax

# Verify the chain after restore
dotnet C:\EgyptTax\app\EgyptTax.Web.dll verify-audit --range full
```

A clean `verify-audit` result confirms the restore was atomic. A failure is a sign that one of the three components was restored from a different point in time — restore again and confirm timestamps match.

---

## 6. NTP failure response (FR-042)

The `ntp-health-check` Hangfire job pings the configured NTP server every 15 min and records the offset. If the offset exceeds 2 seconds OR the server is unreachable, the next document post fails with a clear error pointing the operator at this section.

**Triage**:

1. Check the configured NTP server is reachable: `w32tm /stripchart /computer:time.windows.com /samples:5 /dataonly`.
2. If unreachable, point at an alternate (corporate AD DC, `pool.ntp.org`, regional ETA-published NTP).
3. Update appsettings: `"Ntp": { "Server": "ntp.example.eg" }` and restart the EgyptTax service.
4. Confirm via the Hangfire dashboard that the next `ntp-health-check` succeeds before re-enabling document posting.

The NTP requirement exists so the FR-028 audit chain has trustworthy timestamps; a clock that drifts forward or backward by minutes can cause downstream regulatory issues with deadline-keyed reports.

---

## 7. Period close + reopen support (FR-037)

The accountant locks a period via `/settings/tax-periods` (see [`accountant-guide.md`](accountant-guide.md) §14). The operator's role:

1. **Before the lock**: ensure no Hangfire jobs are mid-execution against the period (the closing cockpit's "drafts in period" check + the ETA dashboard should be clean).
2. **At the lock**: confirm the audit-chain verifier returns clean (run before AND after the lock to bracket the close).
3. **After the lock**: the period's documents are now immutable per FR-027. Backups should explicitly cover the lock event (the audit-log entry `tax_period.locked` records who/when).

**Reopening a locked period** requires Administrator privilege:

1. Operator (Administrator role) opens `/settings/tax-periods`, selects the locked row, clicks "Reopen" with a written reason.
2. The system audit-logs `tax_period.reopened` with the reason. The previous lock metadata is preserved on the row.
3. The accountant can now post backdated corrections; the operator should ensure these are themselves part of the next backup cycle and that the period gets re-locked once corrections are in.

Reopen is intentionally an Administrator operation — a regular accountant cannot reopen, even with an Approver role.

---

## 8. Audit-chain verifier (FR-028)

The verifier is shipped as a CLI on the same `EgyptTax.Web` binary:

```powershell
# Verify the entire chain
dotnet C:\EgyptTax\app\EgyptTax.Web.dll verify-audit --range full

# Verify a specific index range (faster for spot checks)
dotnet C:\EgyptTax\app\EgyptTax.Web.dll verify-audit --range 1024..2048

# Output formats: text (default) | json
dotnet C:\EgyptTax\app\EgyptTax.Web.dll verify-audit --range full --format json
```

**Exit codes**:
- 0 — chain is valid.
- 1 — chain has at least one finding (insert / edit / delete / reorder / tail-truncation). See `findings[]` in JSON output for the affected index + finding type.
- 2 — usage error.

**Run cadence**: at minimum, monthly as part of the close ritual + after every backup restore. Some installs run nightly via a scheduled task and alert on non-zero exit.

A non-zero exit is a security incident. Snapshot the database + checkpoints + logs immediately + escalate to the team responsible for tax compliance — do not allow further posts until the cause is identified. Most non-zero exits in practice come from a botched restore (atomic-restore violation per §5); a true tamper case requires SQL `sysadmin` privilege on the host and is rare in practice.

---

## 9. Out-of-band administrator recovery (FR-038)

If the only Administrator account on the system is locked out (lost MFA token + lost password, e.g. employee departure), use the out-of-band recovery CLI:

```powershell
dotnet C:\EgyptTax\app\EgyptTax.Web.dll recover-admin --email lockedadmin@firm.eg --new-password <new-password>
```

The CLI:
1. Verifies the requested account exists and has the Administrator role.
2. Stages an `AdminRecoveryRecord` row.
3. Prints a confirmation with a `recovery-token` value the operator must enter on next sign-in.
4. On next sign-in by `lockedadmin@firm.eg`, the `AdminRecoveryDrainer` background service consumes the staged record + grants a one-shot password reset + clears the MFA secret (forcing re-enrollment).
5. The recovery is audit-logged with the operator's host identity (Windows account) so the trail is preserved.

The CLI MUST be run on the host machine by someone with file-system access to the EgyptTax binary — i.e. someone who already has operator-level trust. It is NOT a remote API.

---

## 10. Hangfire dashboard + background jobs

`https://localhost/hangfire` is the operator-facing dashboard. Authentication required (Administrator role).

Recurring jobs registered at startup:

| Job ID | Cadence | Purpose | Notes |
|---|---|---|---|
| `audit-checkpoint` | every 1k entries OR every 15 min | Writes the FR-028 integrity checkpoint outside the audit log | Critical — failures break the FR-028 tail-truncation defense. Investigate immediately. |
| `eta-retry` | every 5 min | Retries failed ETA submissions per FR-036 | Transient network failures self-heal. Persistent failures need investigation. |
| `supplier-tin-revalidation` | daily at 03:00 | Re-validates supplier TINs against the registry per FR-043 | The MVP impl is a stub; the Live registry feed is a Near-term plug-in. |
| `ntp-health-check` | every 15 min | Confirms clock drift is within tolerance (§6) | |
| `monthly-depreciation` | first day of month at 02:00 | Emits depreciation expense per FR-017 / US6 | First day of fiscal year handling: zero proration; full month effect. |

Manually triggering a job from the dashboard is supported (use sparingly — most are idempotent within their windows).

---

## 11. Health endpoints + ops monitoring

| Endpoint | Purpose | Status codes |
|---|---|---|
| `GET /api/v1/health/live` | Liveness — process is up | 200 always (returns immediately if the HTTP listener is alive) |
| `GET /api/v1/health/ready` | Readiness — DB reachable + audit checkpoint store reachable + Hangfire connection healthy | 200 `Healthy`, 503 `Unhealthy` with reason |

Monitor the **readiness** endpoint, not liveness. A `503` from readiness means the service is up but cannot serve requests cleanly — the typical cause is a SQL connection failure. Check the SQL service first, then `logs/egypttax-{date}.log` for the EF connection error.

---

## 12. Operational logs (R-20 Serilog)

Logs land in two sinks (per `Program.cs` `UseSerilog` config):

- **Console** — captured by the Windows service host; viewable via `Get-EventLog -LogName Application -Source EgyptTax`.
- **Rolling file** — `{ContentRoot}\logs\egypttax-{yyyyMMdd}.log`, 31-day retention.

Every log line carries:
- `[CorrelationId]` — request-scoped Guid (echoed back on the response as `X-Correlation-Id`); a support-engineer screenshot of an error includes this in the response headers.
- `[UserId]` — claim-derived; absent on anonymous routes (login, password reset).
- `[FirmName]` — populated when the actor is an external accounting-firm user (T225 / FR-049 / INV-015) — an inspector can filter ops logs by firm without a join.

For incident triage: ask the user for the correlation id from their failed request, then `Select-String -Pattern "<correlation-id>" logs\egypttax-*.log` to recover the full request trace.

---

## 13. Upgrade path

> **Status**: The clean upgrade path ships with T247 MSI + a near-term `dotnet upgrade` companion CLI. Until then, in-place upgrade is "stop service, replace binaries, run `dotnet ... seed --apply-migrations`, start service." The migrations are forward-only by construction (EF Core).

Always:

1. Take a fresh backup before the upgrade (§5).
2. Run the audit-chain verifier (§8) BEFORE upgrading — establish a clean baseline.
3. Run it again AFTER the upgrade — confirm migrations did not corrupt the chain.
4. Spot-check a few accountant flows (post a draft invoice, run the VAT report) before re-opening the system to all users.

---

## 14. Incident triage

Common patterns + first-response steps:

| Symptom | First action |
|---|---|
| Service won't start | `Get-EventLog -LogName Application -Source EgyptTax -Newest 5` → look for the most recent stack trace. Most often: SQL connection failure (check connection string + SQL service). |
| Readiness probe returns 503 | Check `/api/v1/health/ready` JSON body for the failing dependency name — usually SQL or Hangfire-store. |
| Audit verifier fails after a backup restore | Almost always an atomic-restore violation per §5 — restore all three components from the SAME timestamp. |
| Operator can't sign in (lost password + MFA) | §9 recovery CLI. |
| ETA submissions stuck in retry | Operator confirms ETA endpoint reachable; check the failure category in the ETA dashboard — non-retriable errors (400-class) need accountant attention to fix the document, not operator. |
| User reports "the post button does nothing" | Get the correlation id from their browser's network tab → grep logs (§12) → most often a validation error masked by a slow client-side render. |
| Disk space exhausted | Stop service → free space (rotate logs, archive old attachments to a near-line tier) → restart. SQL itself recovers cleanly from disk-exhaustion shutdowns. |

For incidents that require escalating to the EgyptTax development team: collect (a) the correlation id of the failing request, (b) the relevant lines from `logs/egypttax-{date}.log`, (c) the result of the most recent `verify-audit --range full` run, (d) the SQL error log if a SQL-related issue.

---

**Runbook last reviewed: 2026-05-08. Cross-references the canonical specs in [`specs/008-egypt-tax-accounting/`](../specs/008-egypt-tax-accounting/) — when in doubt, the spec wins.**
