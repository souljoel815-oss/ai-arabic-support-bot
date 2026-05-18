<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T018 per data-model.md §2 + FR-032. Replaces Breeze's default `users`
 * migration. The portal's identity table is named `team_members` (matches
 * the spec's Key Entities naming + the on-prem product's user table
 * stays at `users` over there — physically separate schemas per FR-032).
 *
 * Switches:
 *   - PK is char(36) UUID (not bigint) so customer count isn't leaked
 *     via incrementing IDs.
 *   - `name` → `display_name` (matches spec).
 *   - Adds `locale_preference`, `mfa_secret` (encrypted-at-rest cast on
 *     the model), `mfa_enabled_at`, `last_login_at`, `soft_deleted_at`.
 *   - `sessions.user_id` switched to char(36) so it can FK to team_members.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('team_members', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->string('display_name', 128)->nullable();
            $table->string('email', 256)->unique();
            $table->timestamp('email_verified_at')->nullable();
            $table->string('password');
            $table->text('mfa_secret')->nullable();      // Encrypted via the TeamMember model cast.
            $table->timestamp('mfa_enabled_at')->nullable();
            $table->string('locale_preference', 8)->default('ar-EG');
            $table->rememberToken();
            $table->timestamp('last_login_at')->nullable();
            $table->timestamp('soft_deleted_at')->nullable();
            $table->timestamps();
        });

        Schema::create('password_reset_tokens', function (Blueprint $table) {
            $table->string('email')->primary();
            $table->string('token');
            $table->timestamp('created_at')->nullable();
        });

        Schema::create('sessions', function (Blueprint $table) {
            $table->string('id')->primary();
            // user_id is char(36) to match team_members.id — Breeze
            // defaults to foreignId() (bigint) which would refuse to
            // store the UUID-string PKs the portal uses.
            $table->uuid('user_id')->nullable()->index();
            $table->string('ip_address', 45)->nullable();
            $table->text('user_agent')->nullable();
            $table->longText('payload');
            $table->integer('last_activity')->index();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('sessions');
        Schema::dropIfExists('password_reset_tokens');
        Schema::dropIfExists('team_members');
    }
};
