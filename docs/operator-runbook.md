# Operator Runbook (placeholder)

**Status**: Stage 1 placeholder. Full content authored in Stage 15 (T248).

This file will become the operator-facing runbook for an on-prem EgyptTax installation. Scope (per [tasks.md](../specs/008-egypt-tax-accounting/tasks.md) T248):

- Install the MSI, choose audit-checkpoint storage mode (file or table) per FR-028, configure SQL connection, NTP server, attachments root, MFA bootstrap.
- Daily / weekly / monthly maintenance checklist.
- Backup + restore procedure (database + attachments + audit checkpoints together).
- Period close + reopen workflow (FR-037).
- Audit verifier usage: `dotnet run --project src\EgyptTax.Web -- verify-audit ...` per [contracts/audit-chain-verifier.md](../specs/008-egypt-tax-accounting/contracts/audit-chain-verifier.md).
- Out-of-band administrator-password recovery (FR-038) when MFA-locked.
- NTP failure response (FR-042).
- Upgrade path (Roadmap Group 14, Near-term).

Until Stage 15, refer to [specs/008-egypt-tax-accounting/quickstart.md](../specs/008-egypt-tax-accounting/quickstart.md) for developer-oriented setup that overlaps operator concerns.
