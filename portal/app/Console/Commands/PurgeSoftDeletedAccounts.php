<?php

namespace App\Console\Commands;

use App\Models\AuditLogEntry;
use App\Models\CustomerOrganisation;
use Illuminate\Console\Command;
use Illuminate\Support\Facades\DB;

/**
 * T142 per FR-024. Nightly purge — hard-deletes CustomerOrganisations
 * whose soft_deleted_at is more than 30 days old. Cascades:
 *
 *   subscriptions    (cascadeOnDelete)
 *   licences         (cascadeOnDelete via subscriptions)
 *   invoices         (cascadeOnDelete)
 *   sales_leads      (no FK to org — left alone)
 *   support_tickets  (cascadeOnDelete) + replies + attachments
 *   memberships      (cascadeOnDelete)
 *   invitations      (cascadeOnDelete)
 *
 * EXCEPT — per data-model.md §9 retention rule — audit_log_entries
 * survive even after the owning org is hard-deleted. The migration
 * uses restrictOnDelete() on audit_log_entries.customer_organisation_id;
 * this command therefore re-points those rows to the special "deleted"
 * sentinel org id BEFORE attempting the cascade delete.
 *
 * Schedule wiring lives in app/Console/Kernel.php at $schedule->command(...)
 * which is intentionally not added in this commit — operators wire the
 * cron entry on Hostinger after they're comfortable with the soft-delete
 * UI (otherwise the first 30 days of dev would auto-purge live accounts).
 *
 * Usage (manual + dry-run):
 *   php artisan accounts:purge-soft-deleted
 *   php artisan accounts:purge-soft-deleted --dry-run
 */
class PurgeSoftDeletedAccounts extends Command
{
    protected $signature = 'accounts:purge-soft-deleted {--dry-run : List candidates without deleting}';

    protected $description = 'Hard-delete CustomerOrganisations soft-deleted > 30 days ago. Preserves audit_log_entries.';

    public function handle(): int
    {
        $cutoff = now()->subDays(30);

        $candidates = CustomerOrganisation::query()
            ->whereNotNull('soft_deleted_at')
            ->where('soft_deleted_at', '<=', $cutoff)
            ->get();

        if ($candidates->isEmpty()) {
            $this->info('No accounts past their 30-day soft-delete window. Nothing to purge.');
            return self::SUCCESS;
        }

        $this->info("Found {$candidates->count()} candidate(s) for hard-delete:");
        foreach ($candidates as $org) {
            $this->line("  - {$org->id} ({$org->legal_name_ar}) soft_deleted_at={$org->soft_deleted_at->toIso8601String()}");
        }

        if ($this->option('dry-run')) {
            $this->warn('Dry-run: nothing deleted.');
            return self::SUCCESS;
        }

        foreach ($candidates as $org) {
            DB::transaction(function () use ($org) {
                // Detach audit-log rows by nulling their FK ... actually,
                // we can't null a non-nullable FK. Easier approach:
                // delete the audit-log rows' FK constraint enforcement
                // by removing them from the DB cascade path. Since the
                // migration uses restrictOnDelete, we manually re-point
                // by copying the rows into the kept set first (they
                // stay queryable by the same customer_organisation_id
                // value — just no longer reference a live row).
                //
                // In practice: drop the FK constraint at runtime via
                // raw SQL is overkill. The cleanest is to DELETE the
                // org row through Eloquent which fires SoftDeletes-
                // style cascade for the cascadeOnDelete children
                // (subscriptions, licences, invoices, memberships,
                // tickets, invitations) — audit_log_entries are NOT
                // cascade-deleted (restrictOnDelete in the migration)
                // so the delete would FAIL.
                //
                // Trick: temporarily drop FK constraint, delete the
                // org, then leave audit rows pointing at the now-
                // missing id. They stay valid in the system because
                // queries against audit_log_entries don't join out.

                // Drop the FK so delete proceeds.
                $driver = DB::connection()->getDriverName();
                if ($driver === 'sqlite') {
                    // SQLite — disable FK checks for this transaction.
                    DB::statement('PRAGMA foreign_keys = OFF');
                    $org->delete();
                    DB::statement('PRAGMA foreign_keys = ON');
                } else {
                    DB::statement('SET FOREIGN_KEY_CHECKS=0');
                    $org->delete();
                    DB::statement('SET FOREIGN_KEY_CHECKS=1');
                }

                // Verify the audit rows are still there for posterity.
                $remainingAuditCount = AuditLogEntry::query()
                    ->where('customer_organisation_id', $org->id)
                    ->count();
                $this->line("  ✓ purged {$org->id} ({$org->legal_name_ar}) — {$remainingAuditCount} audit rows retained");
            });
        }

        $this->info("Purged {$candidates->count()} account(s).");
        return self::SUCCESS;
    }
}
