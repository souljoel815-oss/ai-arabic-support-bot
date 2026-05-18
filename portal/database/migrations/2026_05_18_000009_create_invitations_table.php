<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T121 per data-model.md §10 + FR-020. Stores invitation tokens
 * HASHED (SHA-256) — the raw token is only ever seen by the invitee
 * in their email URL. A DB compromise leaks the hash, not the token,
 * so a pre-image attack is needed to forge an acceptance.
 *
 * 7-day expiry (default), single-use (accepted_at flipped on first
 * acceptance → re-clicks return 410 Gone instead of accepting again).
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('invitations', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('customer_organisation_id')
                ->constrained('customer_organisations')
                ->cascadeOnDelete();
            $table->string('email', 256);
            $table->enum('role', ['Owner', 'BillingAdmin', 'SupportAdmin', 'ReadOnly']);
            $table->string('display_name', 128)->nullable();
            $table->string('locale_preference', 8)->default('ar-EG');
            // SHA-256 hex of the raw token. NEVER store the raw token.
            $table->char('token_hash', 64);
            $table->foreignUuid('invited_by_team_member_id')
                ->constrained('team_members')
                ->restrictOnDelete();
            $table->timestamp('expires_at');
            $table->timestamp('accepted_at')->nullable();
            $table->timestamps();

            $table->unique('token_hash');
            $table->index(['customer_organisation_id', 'email', 'accepted_at']);
            $table->index('expires_at');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('invitations');
    }
};
