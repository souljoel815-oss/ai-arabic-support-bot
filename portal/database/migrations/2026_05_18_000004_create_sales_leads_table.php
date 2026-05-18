<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T079 (early — landed with US1 because the Contact form needs it).
 * Per data-model.md §7. Pre-signup record created by the contact form
 * (FR-005). Vendor staff converts it to a CustomerOrganisation when
 * the prospect signs up.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('sales_leads', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->string('name', 128);
            $table->string('email', 256);
            $table->string('phone', 32)->nullable();
            $table->enum('interested_tier', ['Solo', 'SMB', 'Enterprise', 'Firm'])->nullable();
            $table->string('referrer_page', 256)->nullable();
            $table->text('message')->nullable();
            $table->timestamp('last_contacted_at')->nullable();
            $table->uuid('converted_to_organisation_id')->nullable();
            $table->timestamps();

            $table->index('email');
            $table->index(['interested_tier', 'created_at']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('sales_leads');
    }
};
