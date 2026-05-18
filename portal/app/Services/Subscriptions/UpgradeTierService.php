<?php

namespace App\Services\Subscriptions;

use App\Models\Invoice;
use App\Models\Licence;
use App\Models\Subscription;
use App\Models\TeamMember;
use App\Services\Audit\AuditLogWriter;
use App\Services\Licences\LicenceSigningService;
use Illuminate\Support\Facades\DB;
use InvalidArgumentException;

/**
 * T085 — FR-033 instant proration on upgrade.
 *
 * Behaviour:
 *   1. Verify the new tier is strictly higher than current (downgrade
 *      flows through ScheduleDowngradeService instead).
 *   2. Compute the prorated remainder using `Pricing::upgradeProration()`.
 *   3. Flip the Subscription.tier to the new tier immediately.
 *   4. Retire every existing Licence with `retired_reason = TierUpgraded`.
 *   5. Re-issue a fresh Licence for each retired HWID at the new edition,
 *      signed with the same key — they keep working without re-activation.
 *   6. Create an Upgrade Invoice for the prorated charge (Pending until
 *      the customer pays via the existing checkout flow).
 *   7. Write a `subscription.upgraded` audit-log entry with the
 *      old→new tier + proration amount.
 *
 * Wrapped in a single DB transaction so a half-upgraded state can
 * never persist if any step fails.
 */
class UpgradeTierService
{
    /**
     * Tier ordering for upgrade validation. Indices line up with rank;
     * higher index = more capable plan.
     */
    private const TIER_RANK = [
        Subscription::TIER_SOLO => 0,
        Subscription::TIER_SMB => 1,
        Subscription::TIER_ENTERPRISE => 2,
        Subscription::TIER_FIRM => 3,
    ];

    public function __construct(
        private readonly InvoiceNumberGenerator $invoiceNumbers,
        private readonly LicenceSigningService $licenceSigner,
        private readonly AuditLogWriter $audit,
    ) {
    }

    /**
     * @return array{0: Subscription, 1: Invoice}  refreshed subscription + new upgrade invoice
     */
    public function upgrade(
        Subscription $subscription,
        string $newTier,
        TeamMember $actor,
        string $originatingIp,
    ): array {
        if ($subscription->status !== Subscription::STATUS_ACTIVE) {
            throw new InvalidArgumentException(
                "Can only upgrade an Active subscription. Current status: {$subscription->status}."
            );
        }
        if (!isset(self::TIER_RANK[$newTier])) {
            throw new InvalidArgumentException("Unknown tier: {$newTier}");
        }
        $oldTier = $subscription->tier;
        if (self::TIER_RANK[$newTier] <= self::TIER_RANK[$oldTier]) {
            throw new InvalidArgumentException(
                "Tier {$newTier} is not an upgrade from {$oldTier}. Use ScheduleDowngradeService for downgrades."
            );
        }

        return DB::transaction(function () use ($subscription, $oldTier, $newTier, $actor, $originatingIp) {
            // Step 1 — compute prorated remainder.
            $periodStart = $subscription->current_period_start_at ?? now();
            $periodEnd = $subscription->current_period_end_at ?? now()->addMonth();
            $totalDays = max(1, (int) $periodStart->diffInDays($periodEnd));
            $daysRemaining = max(0, (int) now()->diffInDays($periodEnd, false));

            $prorationPiasters = Pricing::upgradeProration(
                oldTier: $oldTier,
                newTier: $newTier,
                cadence: $subscription->billing_cadence,
                daysRemaining: $daysRemaining,
                totalDaysInPeriod: $totalDays,
            );

            // Step 2 — flip the tier + retire old licences.
            $subscription->update(['tier' => $newTier]);

            $oldLicences = Licence::query()
                ->where('subscription_id', $subscription->id)
                ->whereNull('retired_at')
                ->get();

            $org = $subscription->customerOrganisation;

            foreach ($oldLicences as $oldLicence) {
                $oldLicence->update([
                    'retired_at' => now(),
                    'retired_reason' => Licence::REASON_TIER_UPGRADED,
                ]);

                // Re-sign at the new edition for the same HWID.
                $issuedAt = now();
                $expiresAt = $oldLicence->expires_at ?? $periodEnd;
                $payload = [
                    'version' => 2,
                    'hwid' => $oldLicence->hwid,
                    'customer' => $org?->legal_name_ar ?? 'Unknown',
                    'edition' => $newTier,
                    'issuedAtUtc' => $issuedAt->utc()->format('Y-m-d\TH:i:s\Z'),
                    'expiresAtUtc' => $expiresAt->utc()->format('Y-m-d\TH:i:s\Z'),
                    'salesPhone' => config('mail.from.address', 'sales@daftarx.app'),
                    'salesEmail' => 'sales@daftarx.app',
                ];
                $envelope = $this->licenceSigner->sign($payload);
                $signedTokenJson = json_encode($envelope, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
                Licence::create([
                    'subscription_id' => $subscription->id,
                    'hwid' => $oldLicence->hwid,
                    'edition' => $newTier,
                    'signed_token_base64' => base64_encode($signedTokenJson),
                    'issued_at' => $issuedAt,
                    'expires_at' => $expiresAt,
                ]);
            }

            // Step 3 — create the proration invoice.
            $invoice = Invoice::create([
                'customer_organisation_id' => $subscription->customer_organisation_id,
                'subscription_id' => $subscription->id,
                'invoice_number' => $this->invoiceNumbers->next($subscription->customer_organisation_id),
                'kind' => Invoice::KIND_UPGRADE,
                'payment_method' => $subscription->payment_method_id ?? 'Card',
                'amount_egp_minor' => $prorationPiasters,
                'vat_egp_minor' => (int) round(
                    $prorationPiasters - ($prorationPiasters / (1 + Pricing::VAT_RATE_BPS / 10_000))
                ),
                'status' => Invoice::STATUS_PENDING,
            ]);

            $this->audit->write(
                organisationId: $subscription->customer_organisation_id,
                actorTeamMemberId: $actor->id,
                actorDisplayNameSnapshot: $actor->display_name ?? $actor->email,
                verb: 'subscription.upgraded',
                subjectKind: 'Subscription',
                subjectId: $subscription->id,
                payload: [
                    'old_tier' => $oldTier,
                    'new_tier' => $newTier,
                    'proration_egp' => number_format($prorationPiasters / 100, 2),
                    'days_remaining' => $daysRemaining,
                    'invoice_number' => $invoice->invoice_number,
                    'licences_re_issued' => $oldLicences->count(),
                ],
                originatingIp: $originatingIp,
            );

            return [$subscription->fresh(), $invoice];
        });
    }
}
