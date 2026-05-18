<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T020 per data-model.md §9. Immutable record of every customer-visible
 * state change (FR-023). One row per verb. Retained INDEFINITELY — never
 * purged by the FR-024 nightly soft-delete job even when the owning
 * CustomerOrganisation is hard-deleted.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('audit_log_entries', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('customer_organisation_id')
                ->constrained('customer_organisations')
                // NB: deliberately NOT cascadeOnDelete — audit rows
                // outlive the org (data-model.md §9 retention).
                ->restrictOnDelete();
            // actor_team_member_id is nullable for system actors
            // (webhooks, scheduled jobs). NO FK constraint because the
            // member might be hard-deleted before the audit row's
            // owning org is — keeping the row valid is more important
            // than referential integrity for audit-log purposes.
            $table->uuid('actor_team_member_id')->nullable();
            $table->string('actor_display_name_snapshot', 128);
            $table->string('verb', 64);
            $table->string('subject_kind', 32);
            $table->string('subject_id', 64);
            $table->json('payload_json')->nullable();
            $table->string('originating_ip', 45);
            $table->timestamp('occurred_at');
            $table->timestamps();

            $table->index(['customer_organisation_id', 'occurred_at']);
            $table->index('verb');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('audit_log_entries');
    }
};
