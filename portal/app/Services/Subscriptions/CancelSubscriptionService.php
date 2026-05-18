<?php

namespace App\Services\Subscriptions;

use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use InvalidArgumentException;

/**
 * T153 (per audit fix to spec) — per data-model.md §4 lifecycle
 * `[Active] → [Active until current_period_end_at] → renewal-job → [Cancelled]`.
 *
 * Sets `cancelled_at = now()` so the renewal job knows to flip the
 * status to Cancelled at period end. Status STAYS `Active` until then
 * — customers who cancel mid-period continue using the product
 * through the period they already paid for.
 *
 * Writes a `subscription.cancelled` audit row immediately so Owners
 * see the cancellation in their audit-log viewer (Phase 9 T143).
 */
class CancelSubscriptionService
{
    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    public function cancel(Subscription $subscription, TeamMember $actor, string $originatingIp): Subscription
    {
        if ($subscription->status !== Subscription::STATUS_ACTIVE) {
            throw new InvalidArgumentException(
                "Can only cancel an Active subscription. Current status: {$subscription->status}."
            );
        }
        if ($subscription->cancelled_at !== null) {
            throw new InvalidArgumentException(
                "Subscription was already cancelled on {$subscription->cancelled_at->toDateString()}."
            );
        }

        return DB::transaction(function () use ($subscription, $actor, $originatingIp) {
            $subscription->update(['cancelled_at' => now()]);

            $this->audit->write(
                organisationId: $subscription->customer_organisation_id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'subscription.cancelled',
                subjectKind: 'Subscription',
                subjectId: $subscription->id,
                payload: [
                    'tier' => $subscription->tier,
                    'effective_at' => $subscription->current_period_end_at?->toIso8601String(),
                ],
                originatingIp: $originatingIp,
            );

            return $subscription->fresh();
        });
    }
}
