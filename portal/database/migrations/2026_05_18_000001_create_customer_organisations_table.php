<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T017 per data-model.md §1. Top-level account that owns subscriptions,
 * licences, invoices, tickets, and team-member memberships. One per real
 * business customer. Soft-deleted via FR-024's 30-day window.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('customer_organisations', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->string('legal_name_ar', 256);
            $table->string('legal_name_en', 256)->nullable();
            $table->string('tax_registration_number', 32)->nullable();
            $table->string('billing_email', 256);
            $table->string('billing_phone', 32)->nullable();
            $table->json('billing_address')->nullable();
            $table->char('country_code', 2)->default('EG');
            // FR-011 / T155 — org-policy MFA enforcement; checked by
            // OrganisationMfaPolicy middleware.
            $table->boolean('requires_mfa_for_owners')->default(false);
            $table->timestamp('soft_deleted_at')->nullable()->index();
            $table->timestamps();

            $table->index('billing_email');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('customer_organisations');
    }
};
