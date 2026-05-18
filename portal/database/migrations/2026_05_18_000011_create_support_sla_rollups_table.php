<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T156 per spec SC-005 — daily SLA rollup snapshots, one row per day
 * per tier so the ops dashboard can chart "% of tickets answered
 * within SLA" trend with no on-the-fly aggregation.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('support_sla_rollups', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->date('rollup_date');
            $table->string('tier_bucket', 32);              // Solo|SMB|Enterprise|Firm|ALL
            $table->unsignedInteger('total_tickets');
            $table->unsignedInteger('replies_within_sla');
            $table->decimal('percent_within_sla', 5, 2);   // 0.00 to 100.00
            $table->timestamps();

            $table->index('rollup_date');
            $table->unique(['rollup_date', 'tier_bucket']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('support_sla_rollups');
    }
};
