<?php

namespace App\Console\Commands;

use App\Models\Subscription;
use App\Models\SupportTicket;
use App\Models\SupportSlaRollup;
use App\Services\Support\SlaCalculator;
use Illuminate\Console\Command;
use Illuminate\Support\Carbon;
use Illuminate\Support\Facades\DB;

/**
 * T156 — daily Artisan command (scheduled via Kernel) that summarises
 * yesterday's tickets into the support_sla_rollups table, one row per
 * tier bucket + one "ALL" row.
 *
 * A reply is "within SLA" when:
 *   first_reply_at IS NOT NULL
 *   AND first_reply_at - opened_at <= SLA window for the customer's tier
 *
 * Idempotent: re-running on the same date overwrites existing rows
 * for that date (upsertOrInsert by [rollup_date, tier_bucket]).
 *
 * Usage:
 *   php artisan support:compute-sla-rollup           (defaults to yesterday)
 *   php artisan support:compute-sla-rollup 2026-05-17
 */
class ComputeSupportSlaRollup extends Command
{
    protected $signature = 'support:compute-sla-rollup {date? : Y-m-d; defaults to yesterday}';

    protected $description = 'Compute the daily SLA rollup for support tickets opened on the given date.';

    public function handle(SlaCalculator $sla): int
    {
        $date = Carbon::parse($this->argument('date') ?? Carbon::yesterday()->toDateString())
            ->startOfDay();

        $this->info("Computing SLA rollup for {$date->toDateString()}");

        $tickets = SupportTicket::query()
            ->with('customerOrganisation.subscriptions')
            ->whereDate('opened_at', $date->toDateString())
            ->get();

        $byTier = [];
        foreach (Subscription::TIERS as $tier) {
            $byTier[$tier] = ['total' => 0, 'within' => 0];
        }
        $allTotal = 0;
        $allWithin = 0;

        foreach ($tickets as $ticket) {
            $tier = $ticket->customerOrganisation
                ?->subscriptions
                ?->firstWhere('status', Subscription::STATUS_ACTIVE)
                ?->tier
                ?? Subscription::TIER_SOLO;

            if (!isset($byTier[$tier])) {
                $byTier[$tier] = ['total' => 0, 'within' => 0];
            }
            $byTier[$tier]['total']++;
            $allTotal++;

            if ($ticket->first_reply_at === null) {
                continue;
            }
            $slaHours = in_array($tier, Subscription::PRIORITY_SUPPORT_TIERS, true) ? 4 : 24;
            $elapsedHours = $ticket->opened_at->diffInHours($ticket->first_reply_at);
            if ($elapsedHours <= $slaHours) {
                $byTier[$tier]['within']++;
                $allWithin++;
            }
        }

        DB::transaction(function () use ($date, $byTier, $allTotal, $allWithin) {
            // Per-tier rows + ALL row.
            foreach ($byTier as $tier => $counts) {
                $this->upsert($date, $tier, $counts['total'], $counts['within']);
            }
            $this->upsert($date, SupportSlaRollup::BUCKET_ALL, $allTotal, $allWithin);
        });

        $this->info("Done — {$allTotal} tickets, {$allWithin} within SLA.");
        return self::SUCCESS;
    }

    private function upsert(Carbon $date, string $bucket, int $total, int $within): void
    {
        $percent = $total === 0 ? 0.0 : round(($within / $total) * 100, 2);

        SupportSlaRollup::query()->updateOrCreate(
            ['rollup_date' => $date->toDateString(), 'tier_bucket' => $bucket],
            [
                'total_tickets' => $total,
                'replies_within_sla' => $within,
                'percent_within_sla' => $percent,
            ],
        );
    }
}
