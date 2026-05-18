<?php

namespace App\Services\Payments;

use App\Models\Invoice;
use App\Models\Licence;
use App\Models\Subscription;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Carbon;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;
use RuntimeException;

/**
 * T090 — idempotent webhook state machine per
 * specs/010-website-portal/contracts/payment-webhook.md "Behaviour".
 *
 * Called from `PaymentWebhookController` AFTER `PaymobHmacMiddleware`
 * has validated the signature. The middleware proves authenticity; this
 * class owns the business transition.
 *
 * Idempotency contract:
 *
 *   - Identical `obj.id` arriving twice → SECOND CALL IS A NO-OP. We
 *     return ::ALREADY_PROCESSED so the controller can still emit 200
 *     (Paymob retries non-200s).
 *   - Unknown `obj.order.merchant_order_id` → ::UNKNOWN_ORDER. Controller
 *     returns 400 — Paymob retries are pointless on a permanent failure.
 *
 * Per contract behaviour test #8: NEVER include the cardholder PAN or
 * Paymob's raw `source_data` in audit-log payloads (PCI scope
 * minimisation). The writer enforces this defensively but discipline
 * here is what keeps the scope clean.
 */
class PaymentWebhookHandler
{
    public const PROCESSED = 'processed';
    public const ALREADY_PROCESSED = 'already_processed';
    public const UNKNOWN_ORDER = 'unknown_order';

    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    /**
     * @param array<string,mixed> $obj  The `obj` sub-object from Paymob's payload.
     * @return self::PROCESSED|self::ALREADY_PROCESSED|self::UNKNOWN_ORDER
     */
    public function handle(array $obj, string $originatingIp): string
    {
        $merchantOrderId = data_get($obj, 'order.merchant_order_id');
        if (!is_string($merchantOrderId) || $merchantOrderId === '') {
            throw new RuntimeException('paymob:webhook payload missing order.merchant_order_id');
        }

        $invoice = Invoice::query()
            ->where('invoice_number', $merchantOrderId)
            ->first();

        if ($invoice === null) {
            Log::warning("paymob:webhook unknown merchant_order_id={$merchantOrderId}");
            return self::UNKNOWN_ORDER;
        }

        $paymobTxnId = data_get($obj, 'id');
        if (!is_int($paymobTxnId) || $paymobTxnId <= 0) {
            throw new RuntimeException('paymob:webhook payload missing numeric obj.id');
        }

        // Idempotency check — re-delivery of a callback we already wrote.
        if ((string) $invoice->paymob_transaction_id === (string) $paymobTxnId) {
            Log::info("paymob:webhook duplicate for invoice={$invoice->invoice_number} txn={$paymobTxnId} — no-op");
            return self::ALREADY_PROCESSED;
        }

        $success = (bool) data_get($obj, 'success', false);
        $isVoided = (bool) data_get($obj, 'is_voided', false);
        $isRefunded = (bool) data_get($obj, 'is_refunded', false);
        $occurredAt = $this->parseTimestamp(data_get($obj, 'created_at'));
        $sourceType = (string) data_get($obj, 'source_data.type', 'unknown');

        DB::transaction(function () use ($invoice, $paymobTxnId, $success, $isVoided, $isRefunded, $occurredAt, $sourceType, $originatingIp) {
            $invoice->paymob_transaction_id = (string) $paymobTxnId;

            if ($isRefunded) {
                $this->applyRefunded($invoice, $sourceType, $originatingIp);
            } elseif ($success && !$isVoided) {
                $this->applyCleared($invoice, $occurredAt, $sourceType, $originatingIp);
            } else {
                $this->applyFailed($invoice, $sourceType, $originatingIp);
            }
        });

        return self::PROCESSED;
    }

    private function applyCleared(Invoice $invoice, Carbon $occurredAt, string $sourceType, string $originatingIp): void
    {
        $invoice->status = Invoice::STATUS_PAID;
        $invoice->paid_at = $occurredAt;
        $invoice->save();

        // Period rollover for first-period / renewal / upgrade invoices.
        $subscription = $invoice->subscription;
        if ($subscription !== null && in_array($invoice->kind, [Invoice::KIND_FIRST_PERIOD, Invoice::KIND_RENEWAL, Invoice::KIND_UPGRADE], true)) {
            $this->advanceSubscriptionPeriod($subscription, $invoice);
        }

        $this->audit->write(
            organisationId: $invoice->customer_organisation_id,
            actorTeamMemberId: null,  // system actor — webhook from Paymob
            actorDisplayNameSnapshot: 'paymob:webhook',
            verb: 'payment.cleared',
            subjectKind: 'Invoice',
            subjectId: $invoice->id,
            payload: [
                'invoice_number' => $invoice->invoice_number,
                'amount_egp' => $invoice->amount_egp,
                'payment_method' => $invoice->payment_method,
                // NOTE: NEVER include PAN or raw source_data here — contract test #8.
                'source_type' => $sourceType,
            ],
            originatingIp: $originatingIp,
        );

        // TODO(T093 / T095): dispatch PDF render + receipt-email jobs once
        // those services land. For now the audit-log row is the source of
        // truth and `MarkInvoicePaid` parity is achieved.
    }

    private function applyRefunded(Invoice $invoice, string $sourceType, string $originatingIp): void
    {
        $invoice->status = Invoice::STATUS_REFUNDED;
        $invoice->refunded_at = now();
        $invoice->save();

        // Retire every Licence derived from this Subscription. Per
        // contract behaviour 4.c — refund means revoke entitlements.
        if ($invoice->subscription_id !== null) {
            Licence::query()
                ->where('subscription_id', $invoice->subscription_id)
                ->whereNull('retired_at')
                ->update([
                    'retired_at' => now(),
                    'retired_reason' => Licence::REASON_REFUNDED,
                ]);
        }

        $this->audit->write(
            organisationId: $invoice->customer_organisation_id,
            actorTeamMemberId: null,
            actorDisplayNameSnapshot: 'paymob:webhook',
            verb: 'payment.refunded',
            subjectKind: 'Invoice',
            subjectId: $invoice->id,
            payload: [
                'invoice_number' => $invoice->invoice_number,
                'amount_egp' => $invoice->amount_egp,
                'source_type' => $sourceType,
            ],
            originatingIp: $originatingIp,
        );
    }

    private function applyFailed(Invoice $invoice, string $sourceType, string $originatingIp): void
    {
        $invoice->status = Invoice::STATUS_FAILED;
        $invoice->save();

        // If the parent Subscription was Active, move it to PastDue. We
        // do NOT cancel here — that's the renewal-job's responsibility
        // after N retries (per Subscription lifecycle in data-model.md).
        $subscription = $invoice->subscription;
        if ($subscription !== null && $subscription->status === Subscription::STATUS_ACTIVE) {
            $subscription->status = Subscription::STATUS_PAST_DUE;
            $subscription->save();
        }

        $this->audit->write(
            organisationId: $invoice->customer_organisation_id,
            actorTeamMemberId: null,
            actorDisplayNameSnapshot: 'paymob:webhook',
            verb: 'payment.failed',
            subjectKind: 'Invoice',
            subjectId: $invoice->id,
            payload: [
                'invoice_number' => $invoice->invoice_number,
                'amount_egp' => $invoice->amount_egp,
                'payment_method' => $invoice->payment_method,
                'source_type' => $sourceType,
            ],
            originatingIp: $originatingIp,
        );
    }

    /**
     * Advance the Subscription's period boundaries to reflect a fresh
     * paid term. The billing-cadence column is the source of truth for
     * the period length.
     */
    private function advanceSubscriptionPeriod(Subscription $subscription, Invoice $invoice): void
    {
        $periodStart = $invoice->paid_at ?? now();
        $periodEnd = match ($subscription->billing_cadence) {
            'Yearly' => $periodStart->copy()->addYear(),
            default => $periodStart->copy()->addMonth(),
        };
        $subscription->current_period_start_at = $periodStart;
        $subscription->current_period_end_at = $periodEnd;
        if ($subscription->status === Subscription::STATUS_PAST_DUE) {
            $subscription->status = Subscription::STATUS_ACTIVE;
        }
        $subscription->save();
    }

    private function parseTimestamp(mixed $raw): Carbon
    {
        if ($raw instanceof Carbon) {
            return $raw;
        }
        if (is_string($raw) && $raw !== '') {
            try {
                return Carbon::parse($raw);
            } catch (\Throwable) {
                // Fall through to now().
            }
        }
        return now();
    }
}
