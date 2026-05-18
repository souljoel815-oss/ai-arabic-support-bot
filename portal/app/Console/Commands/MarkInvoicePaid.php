<?php

namespace App\Console\Commands;

use App\Models\Invoice;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Console\Command;
use Illuminate\Support\Facades\DB;

/**
 * T092 + quickstart.md "Convert trial → paid (manual activation)".
 *
 * Vendor-only Artisan command. Used by ops to flip a Bank Transfer
 * invoice from Pending → Paid once the transfer lands in the vendor's
 * bank account. Also useful in dev for the SC-002 walkthrough where
 * the customer picks Bank Transfer and there's no real Paymob webhook
 * to clear the invoice.
 *
 * Usage:
 *   php artisan invoice:mark-paid INV-2026-00001
 *
 * Idempotent: re-running on an already-Paid invoice prints a notice
 * and exits 0 without re-writing the audit row.
 */
class MarkInvoicePaid extends Command
{
    protected $signature = 'invoice:mark-paid {invoice_number : Per-org invoice number, e.g. INV-2026-00001}';

    protected $description = 'Mark a Pending invoice as Paid (vendor-only — Bank Transfer reconciliation).';

    public function handle(AuditLogWriter $audit): int
    {
        $invoiceNumber = $this->argument('invoice_number');

        $invoice = Invoice::query()
            ->where('invoice_number', $invoiceNumber)
            ->first();

        if ($invoice === null) {
            $this->error("Invoice {$invoiceNumber} not found.");
            return self::FAILURE;
        }

        if ($invoice->status === Invoice::STATUS_PAID) {
            $this->info("Invoice {$invoiceNumber} is already Paid (cleared {$invoice->paid_at?->toDateTimeString()}). No-op.");
            return self::SUCCESS;
        }

        if ($invoice->status !== Invoice::STATUS_PENDING) {
            $this->error("Invoice {$invoiceNumber} is in status {$invoice->status}; only Pending invoices can be manually marked Paid.");
            return self::FAILURE;
        }

        DB::transaction(function () use ($invoice, $audit) {
            $invoice->update([
                'status' => Invoice::STATUS_PAID,
                'paid_at' => now(),
            ]);

            $audit->write(
                organisationId: $invoice->customer_organisation_id,
                actorTeamMemberId: null,  // system actor — CLI invocation
                actorDisplayNameSnapshot: 'vendor:cli',
                verb: 'payment.cleared',
                subjectKind: 'Invoice',
                subjectId: $invoice->id,
                payload: [
                    'invoice_number' => $invoice->invoice_number,
                    'payment_method' => $invoice->payment_method,
                    'amount_egp' => $invoice->amount_egp,
                    'cleared_by' => 'MarkInvoicePaid CLI',
                ],
                originatingIp: '127.0.0.1',
            );
        });

        $this->info("Invoice {$invoiceNumber} marked Paid.");
        $this->line("  Customer: org {$invoice->customer_organisation_id}");
        $this->line("  Amount:   {$invoice->amount_egp} EGP ({$invoice->payment_method})");
        $this->line("  Subscription {$invoice->subscription_id} is now fully active.");

        return self::SUCCESS;
    }
}
