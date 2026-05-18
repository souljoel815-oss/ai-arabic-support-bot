<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T060 per data-model.md §4. Subscription binds a CustomerOrganisation
 * to a tier + billing cadence + payment method + billing state.
 *
 * IMPORTANT — no `Trial` status per FR-030. Trial is owned client-side
 * by the on-prem product (LicenseStatus.RecordTrial). The dashboard
 * inspects "is there any non-Cancelled paid Subscription?" to decide
 * whether to render the "Subscribe to keep going" CTA.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('subscriptions', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('customer_organisation_id')
                ->constrained('customer_organisations')
                ->cascadeOnDelete();
            $table->enum('tier', ['Solo', 'SMB', 'Enterprise', 'Firm']);
            $table->enum('billing_cadence', ['Monthly', 'Annual']);
            // payment_method_id is nullable — set when a saved card / method
            // exists. Bank-transfer subscriptions reconcile manually so they
            // don't carry a saved method. FK lands when payment_methods
            // table exists (Phase 5, US3 T100).
            $table->uuid('payment_method_id')->nullable();
            $table->enum('status', ['Active', 'PastDue', 'Cancelled', 'Paused']);
            $table->timestamp('current_period_start_at');
            $table->timestamp('current_period_end_at');
            $table->enum('pending_tier_change_to', ['Solo', 'SMB', 'Enterprise', 'Firm'])->nullable();
            $table->timestamp('cancelled_at')->nullable();
            $table->timestamps();

            $table->index(['customer_organisation_id', 'status']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('subscriptions');
    }
};
