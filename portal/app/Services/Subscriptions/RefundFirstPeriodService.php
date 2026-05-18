<?php

namespace App\Services\Subscriptions;

use App\Models\Invoice;
use App\Models\Licence;
use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use RuntimeException;

/**
 * T087 per FR-034 — 7-day full refund on the first paid Subscription
 * period only. Per the spec:
 *   - Customer requests via Billing page self-service.
 *   - Portal cancels the Subscription immediately (status → Cancelled).
 *   - All active Licences under the Subscription are retired with
 *     reason = Refunded. On-prem product auto-reverts to trial-expired
 *     on next launch.
 *   - Original Invoice is marked Refunded; a paired Refund Invoice
 *     row is inserted for the audit trail.
 *   - The actual payment reversal call to Paymob is fired separately
 *     by the caller; this service handles only the portal-side state.
 */
class RefundFirstPeriodService
{
    public function __construct(
        private readonly InvoiceNumberGenerator $invoiceNumbers,
        private readonly AuditLogWriter $audit,
    ) {
    }

    public function refund(Invoice $invoice, TeamMember $actor, string $originatingIp): Invoice
    {
        if ($invoice->refund_eligibility !== 'eligible') {
            throw new RuntimeException(
                "Invoice {$invoice->invoice_number} is not refund-eligible. Reason: {$invoice->refund_eligibility}."
            );
        }

        return DB::transaction(function () use ($invoice, $actor, $originatingIp) {
            $subscription = $invoice->subscription;

            // Retire ALL active licences under this subscription.
            $licences = Licence::query()
                ->where('subscription_id', $subscription->id)
                ->whereNull('retired_at')
                ->get();
            foreach ($licences as $licence) {
                $licence->update([
                    'retired_at' => now(),
                    'retired_reason' => Licence::REASON_REFUNDED,
                ]);
            }

            // Subscription → Cancelled immediately (NOT at next renewal —
            // this is a refund, not a normal cancellation).
            $subscription->update([
                'status' => Subscription::STATUS_CANCELLED,
                'cancelled_at' => now(),
            ]);

            // Original invoice → Refunded.
            $invoice->update([
                'status' => Invoice::STATUS_REFUNDED,
                'refunded_at' => now(),
            ]);

            // Paired Refund invoice for the audit trail.
            $refundInvoice = Invoice::create([
                'customer_organisation_id' => $invoice->customer_organisation_id,
                'subscription_id' => $invoice->subscription_id,
                'invoice_number' => $this->invoiceNumbers->next($invoice->customer_organisation_id),
                'kind' => Invoice::KIND_REFUND,
                'payment_method' => $invoice->payment_method,
                'amount_egp_minor' => $invoice->amount_egp_minor,
                'vat_egp_minor' => $invoice->vat_egp_minor,
                'status' => Invoice::STATUS_PAID,  // The refund row "settles" immediately in our books
                'paid_at' => now(),
            ]);

            $this->audit->write(
                organisationId: $invoice->customer_organisation_id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'subscription.refunded',
                subjectKind: 'Invoice',
                subjectId: $invoice->id,
                payload: [
                    'original_invoice_number' => $invoice->invoice_number,
                    'refund_invoice_number' => $refundInvoice->invoice_number,
                    'amount_egp' => $invoice->amount_egp,
                    'licences_retired' => $licences->count(),
                ],
                originatingIp: $originatingIp,
            );

            return $invoice->fresh();
        });
    }
}
