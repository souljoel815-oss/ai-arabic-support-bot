<?php

namespace Tests\Feature\Subscriptions;

use App\Models\CustomerOrganisation;
use App\Models\Invoice;
use App\Models\Licence;
use App\Models\OrganisationMembership;
use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Subscriptions\CancelSubscriptionService;
use App\Services\Subscriptions\ConvertTrialToPaidService;
use App\Services\Subscriptions\Pricing;
use App\Services\Subscriptions\RefundFirstPeriodService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

/**
 * T072-T075 + T153 — Trial-conversion + cancel + refund flow tests.
 *
 * Full Paymob webhook integration tests (T071) land alongside the
 * webhook handler in a follow-up commit (real Paymob HMAC verification
 * needs a known fixture body to be useful).
 */
class SubscriptionFlowsTest extends TestCase
{
    use RefreshDatabase;

    public function test_convert_to_paid_creates_subscription_plus_pending_first_period_invoice(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        $service = app(ConvertTrialToPaidService::class);

        [$subscription, $invoice] = $service->start(
            org: $org,
            tier: Subscription::TIER_SMB,
            billingCadence: 'Monthly',
            paymentMethod: 'BankTransfer',
            actor: $user,
            originatingIp: '127.0.0.1',
        );

        $this->assertSame(Subscription::TIER_SMB, $subscription->tier);
        $this->assertSame(Subscription::STATUS_ACTIVE, $subscription->status);
        $this->assertSame(Invoice::KIND_FIRST_PERIOD, $invoice->kind);
        $this->assertSame(Invoice::STATUS_PENDING, $invoice->status);

        $expectedPrice = Pricing::tierPrice(Subscription::TIER_SMB, 'Monthly');
        $this->assertSame($expectedPrice['amount_piasters'], $invoice->amount_egp_minor);
        $this->assertSame($expectedPrice['vat_piasters'], $invoice->vat_egp_minor);

        $this->assertDatabaseHas('audit_log_entries', [
            'verb' => 'subscription.started',
            'subject_id' => $subscription->id,
        ]);
    }

    public function test_convert_to_paid_rejects_when_already_active(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        $service = app(ConvertTrialToPaidService::class);

        $service->start($org, Subscription::TIER_SOLO, 'Monthly', 'BankTransfer', $user, '127.0.0.1');

        $this->expectException(\InvalidArgumentException::class);
        $service->start($org, Subscription::TIER_SMB, 'Monthly', 'BankTransfer', $user, '127.0.0.1');
    }

    public function test_mark_invoice_paid_command_flips_pending_to_paid(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        $service = app(ConvertTrialToPaidService::class);
        [$_subscription, $invoice] = $service->start($org, Subscription::TIER_SOLO, 'Monthly', 'BankTransfer', $user, '127.0.0.1');

        $this->assertSame(Invoice::STATUS_PENDING, $invoice->status);

        $exitCode = $this->artisan('invoice:mark-paid', ['invoice_number' => $invoice->invoice_number])->run();

        $this->assertSame(0, $exitCode);
        $invoice->refresh();
        $this->assertSame(Invoice::STATUS_PAID, $invoice->status);
        $this->assertNotNull($invoice->paid_at);
        $this->assertDatabaseHas('audit_log_entries', [
            'verb' => 'payment.cleared',
            'subject_id' => $invoice->id,
        ]);
    }

    public function test_mark_invoice_paid_is_idempotent(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        [$_subscription, $invoice] = app(ConvertTrialToPaidService::class)
            ->start($org, Subscription::TIER_SOLO, 'Monthly', 'BankTransfer', $user, '127.0.0.1');
        $this->artisan('invoice:mark-paid', ['invoice_number' => $invoice->invoice_number]);
        // Run a second time — should print "already Paid" and exit 0.
        $exitCode = $this->artisan('invoice:mark-paid', ['invoice_number' => $invoice->invoice_number])->run();
        $this->assertSame(0, $exitCode);

        // Only ONE payment.cleared audit row should exist (not two).
        $this->assertSame(1, \App\Models\AuditLogEntry::query()
            ->where('verb', 'payment.cleared')
            ->where('subject_id', $invoice->id)
            ->count());
    }

    public function test_cancel_keeps_subscription_active_until_period_end(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        [$subscription, $_invoice] = app(ConvertTrialToPaidService::class)
            ->start($org, Subscription::TIER_SMB, 'Monthly', 'BankTransfer', $user, '127.0.0.1');

        $cancelled = app(CancelSubscriptionService::class)->cancel($subscription, $user, '127.0.0.1');

        // Status stays Active per data-model.md §3 lifecycle —
        // renewal-job flips it at period end.
        $this->assertSame(Subscription::STATUS_ACTIVE, $cancelled->status);
        $this->assertNotNull($cancelled->cancelled_at);
        $this->assertDatabaseHas('audit_log_entries', [
            'verb' => 'subscription.cancelled',
            'subject_id' => $subscription->id,
        ]);
    }

    public function test_cancel_rejects_already_cancelled_subscription(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        [$subscription, $_invoice] = app(ConvertTrialToPaidService::class)
            ->start($org, Subscription::TIER_SMB, 'Monthly', 'BankTransfer', $user, '127.0.0.1');
        app(CancelSubscriptionService::class)->cancel($subscription, $user, '127.0.0.1');

        $this->expectException(\InvalidArgumentException::class);
        app(CancelSubscriptionService::class)->cancel($subscription->fresh(), $user, '127.0.0.1');
    }

    public function test_refund_first_period_eligible_within_7_days_paid_invoice(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        [$_subscription, $invoice] = app(ConvertTrialToPaidService::class)
            ->start($org, Subscription::TIER_SOLO, 'Monthly', 'BankTransfer', $user, '127.0.0.1');
        $invoice->update(['status' => Invoice::STATUS_PAID, 'paid_at' => now()->subDays(3)]);

        $this->assertSame('eligible', $invoice->fresh()->refund_eligibility);

        $refunded = app(RefundFirstPeriodService::class)->refund($invoice->fresh(), $user, '127.0.0.1');

        $this->assertSame(Invoice::STATUS_REFUNDED, $refunded->status);
        $this->assertNotNull($refunded->refunded_at);
        $this->assertSame(Subscription::STATUS_CANCELLED, $refunded->subscription->fresh()->status);
        $this->assertDatabaseHas('invoices', [
            'kind' => Invoice::KIND_REFUND,
            'customer_organisation_id' => $org->id,
        ]);
        $this->assertDatabaseHas('audit_log_entries', [
            'verb' => 'subscription.refunded',
            'subject_id' => $invoice->id,
        ]);
    }

    public function test_refund_rejects_when_window_expired(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        [$_subscription, $invoice] = app(ConvertTrialToPaidService::class)
            ->start($org, Subscription::TIER_SOLO, 'Monthly', 'BankTransfer', $user, '127.0.0.1');
        $invoice->update(['status' => Invoice::STATUS_PAID, 'paid_at' => now()->subDays(10)]);

        $this->expectException(\RuntimeException::class);
        app(RefundFirstPeriodService::class)->refund($invoice->fresh(), $user, '127.0.0.1');
    }

    public function test_refund_rejects_renewal_invoice(): void
    {
        [$user, $org] = $this->seedOwnerWithOrg();
        [$subscription, $firstInvoice] = app(ConvertTrialToPaidService::class)
            ->start($org, Subscription::TIER_SOLO, 'Monthly', 'BankTransfer', $user, '127.0.0.1');
        $firstInvoice->update(['status' => Invoice::STATUS_PAID, 'paid_at' => now()->subDays(45)]);

        // Insert a renewal invoice.
        $renewal = Invoice::create([
            'customer_organisation_id' => $org->id,
            'subscription_id' => $subscription->id,
            'invoice_number' => 'INV-2026-99001',
            'kind' => Invoice::KIND_RENEWAL,
            'payment_method' => 'BankTransfer',
            'amount_egp_minor' => 45_000,
            'vat_egp_minor' => 5_526,
            'status' => Invoice::STATUS_PAID,
            'paid_at' => now()->subDays(3),
        ]);

        $this->assertSame('not_eligible_renewal', $renewal->refund_eligibility);
        $this->expectException(\RuntimeException::class);
        app(RefundFirstPeriodService::class)->refund($renewal, $user, '127.0.0.1');
    }

    /**
     * @return array{0: TeamMember, 1: CustomerOrganisation}
     */
    private function seedOwnerWithOrg(): array
    {
        $user = TeamMember::factory()->create();
        $org = CustomerOrganisation::factory()->create();
        OrganisationMembership::create([
            'customer_organisation_id' => $org->id,
            'team_member_id' => $user->id,
            'role' => OrganisationMembership::ROLE_OWNER,
            'invited_at' => now(),
            'accepted_at' => now(),
        ]);
        return [$user, $org];
    }
}
