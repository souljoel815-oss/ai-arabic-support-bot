<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T107 per data-model.md §8. Three tables created together because the
 * lifecycle is atomic (a ticket is always created with at least one
 * row; replies + attachments are append-only children).
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('support_tickets', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('customer_organisation_id')
                ->constrained('customer_organisations')
                ->cascadeOnDelete();
            $table->foreignUuid('opened_by_team_member_id')
                ->constrained('team_members')
                ->restrictOnDelete(); // keep the ticket if the member is removed
            $table->enum('category', ['Billing', 'Bug', 'FeatureRequest', 'AccountingQuestion', 'Urgent']);
            $table->enum('priority', ['Low', 'Normal', 'High']);
            $table->string('subject', 256);
            $table->text('body');
            $table->enum('status', ['Open', 'InProgress', 'Resolved', 'Closed']);
            // Vendor staff are tracked outside this schema (an internal
            // staff system), so we don't FK this column.
            $table->uuid('assigned_vendor_staff_id')->nullable();
            $table->timestamp('opened_at');
            // T156 / SC-005 — set on the first vendor reply.
            $table->timestamp('first_reply_at')->nullable();
            $table->timestamp('resolved_at')->nullable();
            $table->timestamp('closed_at')->nullable();
            $table->timestamps();

            $table->index(['customer_organisation_id', 'status']);
            $table->index('opened_at');
        });

        Schema::create('support_ticket_replies', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('support_ticket_id')
                ->constrained('support_tickets')
                ->cascadeOnDelete();
            $table->enum('author_kind', ['Customer', 'VendorStaff']);
            // Exactly one of these two is populated per row.
            $table->uuid('author_team_member_id')->nullable();
            $table->uuid('author_vendor_staff_id')->nullable();
            $table->text('body');
            $table->timestamps();

            $table->index(['support_ticket_id', 'created_at']);
        });

        Schema::create('support_ticket_attachments', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('support_ticket_id')
                ->constrained('support_tickets')
                ->cascadeOnDelete();
            $table->string('original_filename', 256);
            $table->string('mime_type', 128);
            $table->unsignedInteger('size_bytes');
            // Path under storage/app/private/tickets/{org-id}/{ticket-id}/.
            $table->string('storage_path', 512);
            $table->timestamps();

            $table->index('support_ticket_id');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('support_ticket_attachments');
        Schema::dropIfExists('support_ticket_replies');
        Schema::dropIfExists('support_tickets');
    }
};
