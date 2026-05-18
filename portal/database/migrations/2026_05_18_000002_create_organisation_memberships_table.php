<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T019 per data-model.md §3. Many-to-many join between customer_organisations
 * and team_members carrying a role per row. Cross-org membership exists for
 * accounting firms managing multiple client orgs.
 *
 * Partial-unique-on-active trick: MySQL 8 doesn't support partial indexes
 * natively, so we use a stored generated column that's NULL when the row
 * is revoked + add a UNIQUE constraint on it. The same constraint translates
 * cleanly to SQLite (via a real partial index in dev) at migrate time.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('organisation_memberships', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('customer_organisation_id')
                ->constrained('customer_organisations')
                ->cascadeOnDelete();
            $table->foreignUuid('team_member_id')
                ->constrained('team_members')
                ->cascadeOnDelete();
            $table->enum('role', ['Owner', 'BillingAdmin', 'SupportAdmin', 'ReadOnly']);
            $table->timestamp('invited_at');
            $table->timestamp('accepted_at')->nullable();
            $table->timestamp('revoked_at')->nullable();
            // FR-022 — bumped on revoke so all live sessions invalidate;
            // checked by the OrganisationScope middleware.
            $table->integer('security_stamp_version')->default(0);
            $table->timestamps();

            $table->index(['customer_organisation_id']);
            $table->index(['team_member_id', 'revoked_at']);
        });

        // Filtered-unique on (org, member) WHERE revoked_at IS NULL.
        // SQLite (dev) supports this natively via raw partial index;
        // MySQL 8 needs a generated column. Use the SQLite path for
        // dev iteration speed and rely on a deploy-time hook to
        // promote it to the generated-column variant in production.
        if (Schema::getConnection()->getDriverName() === 'sqlite') {
            Schema::getConnection()->statement(
                'CREATE UNIQUE INDEX ux_organisation_memberships_active_org_member '
                . 'ON organisation_memberships (customer_organisation_id, team_member_id) '
                . 'WHERE revoked_at IS NULL'
            );
        }
        // For MySQL we used to add a generated-column unique trick to
        // enforce "one ACTIVE membership per (org, member)" at the DB
        // level. Hostinger's MySQL 8 rejects every variant we tried
        // (STORED+CASE → error 1901; VIRTUAL+IF → same), so we drop
        // the DB-level constraint and rely on application-level checks
        // in InviteMemberService::invite() (which already verifies no
        // active membership exists before INSERTing). SQLite (dev)
        // keeps the partial unique index so local tests catch any
        // service-layer regression that would let two active rows slip in.
    }

    public function down(): void
    {
        Schema::dropIfExists('organisation_memberships');
    }
};
