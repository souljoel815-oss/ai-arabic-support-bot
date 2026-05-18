<?php

namespace App\Services\Subscriptions;

use App\Models\CustomerOrganisation;
use App\Models\Invoice;
use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use InvalidArgumentException;

/**
 * T084 per FR-029 single-tap convert. Creates the customer's first
 * paid Subscription (status Pending until the first Invoice clears)
 * + a FirstPeriod Invoice with Pending status. The customer pays the
 * Invoice via Paymob (or vendor reconciles manually); the
 * PaymentWebhookHandler / MarkInvoicePaid command flips the Invoice
 * to Paid + Subscription to Active.
 *
 * Per FR-030, this service does NOT create a `Trial`-status row —
 * trial is owned client-side by the on-prem product. The "convert" in
 * the name refers to the UX moment ("customer in trial decides to
 * subscribe"), not a state transition in our database.
 */
class ConvertTrialToPaidService
{
    public function __construct(
        private readonly InvoiceNumberGenerator $invoiceNumbers,
        private readonly AuditLogWriter $audit,
    ) {
    }

    /**
     * @return array{0: Subscription, 1: Invoice}
     */
    public function start(
        CustomerOrganisation $org,
        string $tier,
        string $billingCadence,
        string $paymentMethod,
        TeamMember $actor,
        string $originatingIp,
    ): array {
        if (! in_array($tier, Subscription::TIERS, true)) {
            throw new InvalidArgumentException("Unknown tier: {$tier}");
        }
        if (! in_array($billingCadence, ['Monthly', 'Annual'], true)) {
            throw new InvalidArgumentException("Unknown billing_cadence: {$billingCadence}");
        }
        if (! in_array($paymentMethod, ['Card', 'Fawry', 'InstaPay', 'VodafoneCash', 'BankTransfer'], true)) {
            throw new InvalidArgumentException("Unknown payment_method: {$paymentMethod}");
        }

        // Block duplicate first-paid subscriptions — if the customer
        // already has an Active or Pending one, route them to the
        // existing one instead of double-billing.
        $existing = Subscription::query()
            ->where('customer_organisation_id', $org->id)
            ->whereIn('status', [Subscription::STATUS_ACTIVE, Subscription::STATUS_PAST_DUE])
            ->first();
        if ($existing !== null) {
            throw new InvalidArgumentException(
                "Organisation already has an active subscription ({$existing->id}). Use upgrade/downgrade instead of creating a new one."
            );
        }

        $price = Pricing::tierPrice($tier, $billingCadence);

        return DB::transaction(function () use ($org, $tier, $billingCadence, $paymentMethod, $actor, $originatingIp, $price) {
            $periodStart = now();
            $periodEnd = $billingCadence === 'Monthly'
                ? $periodStart->copy()->addMonth()
                : $periodStart->copy()->addYear();

            // Subscription created Active immediately on Bank Transfer
            // (we trust the vendor's MarkInvoicePaid CLI to flip the
            // invoice). Card / Fawry / InstaPay / VodafoneCash route
            // through Paymob — the subscription stays Active but the
            // Invoice stays Pending until the webhook lands. The
            // dashboard treats "Active but no paid invoice" as "payment
            // pending" UX-wise.
            $subscription = Subscription::create([
                'customer_organisation_id' => $org->id,
                'tier' => $tier,
                'billing_cadence' => $billingCadence,
                'status' => Subscription::STATUS_ACTIVE,
                'current_period_start_at' => $periodStart,
                'current_period_end_at' => $periodEnd,
            ]);

            $invoice = Invoice::create([
                'customer_organisation_id' => $org->id,
                'subscription_id' => $subscription->id,
                'invoice_number' => $this->invoiceNumbers->next($org->id),
                'kind' => Invoice::KIND_FIRST_PERIOD,
                'payment_method' => $paymentMethod,
                'amount_egp_minor' => $price['amount_piasters'],
                'vat_egp_minor' => $price['vat_piasters'],
                'status' => Invoice::STATUS_PENDING,
            ]);

            $this->audit->write(
                organisationId: $org->id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'subscription.started',
                subjectKind: 'Subscription',
                subjectId: $subscription->id,
                payload: [
                    'tier' => $tier,
                    'billing_cadence' => $billingCadence,
                    'payment_method' => $paymentMethod,
                    'invoice_number' => $invoice->invoice_number,
                    'amount_egp' => $invoice->amount_egp,
                ],
                originatingIp: $originatingIp,
            );

            return [$subscription, $invoice];
        });
    }
}
