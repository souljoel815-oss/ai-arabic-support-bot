<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * T078 per data-model.md §6. Cleared payment record bound to a
 * Subscription. Sequential per-org invoice_number for FR-016
 * compliance (Egyptian tax-line numbering requirements).
 *
 * Two unique-index features:
 *   - (customer_organisation_id, invoice_number) UNIQUE — sequential
 *     numbering inside each customer's books.
 *   - (paymob_transaction_id) UNIQUE-where-not-null — webhook
 *     idempotency: replays of the same Paymob transaction don't
 *     double-charge.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('invoices', function (Blueprint $table) {
            $table->uuid('id')->primary();
            $table->foreignUuid('customer_organisation_id')
                ->constrained('customer_organisations')
                ->cascadeOnDelete();
            $table->foreignUuid('subscription_id')
                ->constrained('subscriptions')
                ->cascadeOnDelete();
            $table->string('invoice_number', 32);
            $table->enum('kind', ['FirstPeriod', 'Renewal', 'Upgrade', 'Refund']);
            $table->enum('payment_method', ['Card', 'Fawry', 'InstaPay', 'VodafoneCash', 'BankTransfer']);
            // Amounts stored as piasters (bigint unsigned in MySQL;
            // SQLite gracefully treats unsigned-ness as a no-op).
            $table->unsignedBigInteger('amount_egp_minor');
            $table->unsignedBigInteger('vat_egp_minor');
            $table->string('paymob_transaction_id', 64)->nullable();
            $table->enum('status', ['Pending', 'Paid', 'Refunded', 'Failed']);
            $table->string('pdf_storage_path', 512)->nullable();
            $table->timestamp('paid_at')->nullable();
            $table->timestamp('refunded_at')->nullable();
            $table->timestamps();

            $table->unique(['customer_organisation_id', 'invoice_number']);
            $table->index(['customer_organisation_id', 'status', 'created_at']);
        });

        // Filtered unique on paymob_transaction_id WHERE NOT NULL —
        // webhook idempotency. SQLite supports partial unique indexes
        // natively; MySQL 8 needs a generated column.
        if (Schema::getConnection()->getDriverName() === 'sqlite') {
            Schema::getConnection()->statement(
                'CREATE UNIQUE INDEX ux_invoices_paymob_txn '
                . 'ON invoices (paymob_transaction_id) '
                . 'WHERE paymob_transaction_id IS NOT NULL'
            );
        } else {
            Schema::getConnection()->statement(
                'ALTER TABLE invoices '
                . 'ADD COLUMN paymob_idem_key VARCHAR(64) AS (paymob_transaction_id) STORED, '
                . 'ADD UNIQUE KEY ux_invoices_paymob_txn (paymob_idem_key)'
            );
        }
    }

    public function down(): void
    {
        Schema::dropIfExists('invoices');
    }
};
