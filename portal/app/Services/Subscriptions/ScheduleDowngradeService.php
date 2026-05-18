<?php

namespace App\Services\Subscriptions;

use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use Illuminate\Support\Facades\DB;
use InvalidArgumentException;

/**
 * T086 — FR-033 downgrade scheduled at next renewal (NOT mid-period).
 *
 * Unlike UpgradeTierService which flips the tier immediately, downgrade
 * defers to the next billing cycle so customers aren't penalised for
 * already-paid time at the higher tier:
 *
 *   - Sets `Subscription.pending_tier_change_to = <newTier>` so the
 *     renewal job sees it next cycle and flips tier + issues invoice.
 *   - Subscription.tier stays the same until the renewal job runs.
 *   - Customer keeps the current-tier features for the rest of the
 *     billing period they already paid for.
 *   - No proration refund — they got what they paid for through the
 *     end of the period.
 *
 * Idempotent: calling twice with the same target is a no-op; calling
 * with a different target overwrites the pending change (the renewal
 * job only looks at the latest value).
 */
class ScheduleDowngradeService
{
    /** Tier ordering — must match UpgradeTierService::TIER_RANK. */
    private const TIER_RANK = [
        Subscription::TIER_SOLO => 0,
        Subscription::TIER_SMB => 1,
        Subscription::TIER_ENTERPRISE => 2,
        Subscription::TIER_FIRM => 3,
    ];

    public function __construct(private readonly AuditLogWriter $audit)
    {
    }

    public function schedule(
        Subscription $subscription,
        string $newTier,
        TeamMember $actor,
        string $originatingIp,
    ): Subscription {
        if ($subscription->status !== Subscription::STATUS_ACTIVE) {
            throw new InvalidArgumentException(
                "Can only schedule a downgrade for an Active subscription. Current status: {$subscription->status}."
            );
        }
        if (!isset(self::TIER_RANK[$newTier])) {
            throw new InvalidArgumentException("Unknown tier: {$newTier}");
        }
        $oldTier = $subscription->tier;
        if (self::TIER_RANK[$newTier] >= self::TIER_RANK[$oldTier]) {
            throw new InvalidArgumentException(
                "Tier {$newTier} is not a downgrade from {$oldTier}. Use UpgradeTierService for upgrades."
            );
        }

        return DB::transaction(function () use ($subscription, $oldTier, $newTier, $actor, $originatingIp) {
            $subscription->update(['pending_tier_change_to' => $newTier]);

            $this->audit->write(
                organisationId: $subscription->customer_organisation_id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'subscription.downgrade_scheduled',
                subjectKind: 'Subscription',
                subjectId: $subscription->id,
                payload: [
                    'old_tier' => $oldTier,
                    'new_tier' => $newTier,
                    'effective_at' => $subscription->current_period_end_at?->toIso8601String(),
                ],
                originatingIp: $originatingIp,
            );

            return $subscription->fresh();
        });
    }
}
