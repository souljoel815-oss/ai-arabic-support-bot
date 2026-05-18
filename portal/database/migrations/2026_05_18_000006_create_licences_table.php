<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T061 per data-model.md §5. A signed token bound to a hardware id,
 * derived from a Subscription. Multiple licences per Subscription for
 * the LAN-client + multi-device cases.
 *
 * Filtered unique on hwid WHERE retired_at IS NULL — implements the
 * FR-014 cross-customer collision check (the same machine can't be
 * bound to two active licences). Same SQLite-partial / MySQL-generated-
 * column trick as the organisation_memberships unique index.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('licences', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('subscription_id')
                ->constrained('subscriptions')
                ->cascadeOnDelete();
            $table->string('hwid', 64);
            $table->enum('edition', ['Solo', 'SMB', 'Enterprise', 'Firm']);
            // longText = TEXT on SQLite / LONGTEXT on MySQL; the signed
            // envelope is base64 + small JSON so ~2-4 KB per row.
            $table->longText('signed_token_base64');
            $table->timestamp('issued_at');
            $table->timestamp('expires_at');
            $table->timestamp('retired_at')->nullable();
            $table->enum('retired_reason', [
                'Transferred',
                'SubscriptionCancelled',
                'Refunded',
                'TierUpgraded',
            ])->nullable();
            $table->timestamps();

            $table->index('subscription_id');
            $table->index('hwid');
        });

        // Filtered unique on (hwid) WHERE retired_at IS NULL — prevents
        // two ACTIVE licences from sharing a hardware id (across customers).
        if (Schema::getConnection()->getDriverName() === 'sqlite') {
            Schema::getConnection()->statement(
                'CREATE UNIQUE INDEX ux_licences_active_hwid '
                . 'ON licences (hwid) WHERE retired_at IS NULL'
            );
        } else {
            // MySQL 8: generated column = hwid when active, NULL when retired.
            Schema::getConnection()->statement(
                'ALTER TABLE licences '
                . 'ADD COLUMN active_hwid VARCHAR(64) AS '
                . '(CASE WHEN retired_at IS NULL THEN hwid ELSE NULL END) STORED, '
                . 'ADD UNIQUE KEY ux_licences_active_hwid (active_hwid)'
            );
        }
    }

    public function down(): void
    {
        Schema::dropIfExists('licences');
    }
};
