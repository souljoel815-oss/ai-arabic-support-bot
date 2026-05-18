<?php

namespace App\Services\Support;

use App\Models\Subscription;

/**
 * T110 per FR-019. Computes the acknowledgement-SLA target per tier:
 *   Solo, SMB        → 24 business hours
 *   Enterprise, Firm → 4 business hours  (priority-support entitlement)
 *
 * Business hours = Sunday-Thursday (Egyptian work week) from 09:00-17:00
 * (Africa/Cairo). Friday + Saturday are weekend; the calculator skips
 * them when adding hours so a Thursday-17:00 ticket on Enterprise is
 * due Sunday-13:00, not Friday-21:00.
 *
 * Returns a Carbon-like immutable timestamp for the deadline so the
 * UI can render it + the rollup job (T156) can compare against
 * SupportTicket.first_reply_at to compute SC-005 compliance.
 */
class SlaCalculator
{
    private const BUSINESS_DAY_HOURS = 8;       // 09:00 → 17:00 = 8 working hours
    private const BUSINESS_DAY_START_HOUR = 9;
    private const BUSINESS_DAY_END_HOUR = 17;

    /** Egyptian work week: Sun=0, Mon=1, Tue=2, Wed=3, Thu=4. Fri=5, Sat=6 = weekend. */
    private const WEEKEND_DAYS = [5, 6];

    public function slaHours(string $tier): int
    {
        return in_array($tier, Subscription::PRIORITY_SUPPORT_TIERS, true) ? 4 : 24;
    }

    /**
     * Add `slaHours` BUSINESS hours to `$openedAt` and return the
     * deadline timestamp. Skips Egyptian weekends + before-9-after-17.
     */
    public function computeDeadline(\DateTimeInterface $openedAt, string $tier): \DateTimeImmutable
    {
        $hoursToAdd = $this->slaHours($tier);
        $cursor = \DateTimeImmutable::createFromInterface($openedAt);

        // Roll the cursor forward to the next business hour if we're
        // outside business hours when the ticket opened.
        $cursor = $this->advanceToNextBusinessHour($cursor);

        while ($hoursToAdd > 0) {
            $hoursLeftInDay = self::BUSINESS_DAY_END_HOUR - (int) $cursor->format('H');
            if ($hoursLeftInDay <= 0) {
                $cursor = $this->advanceToNextBusinessHour($cursor->modify('+1 day')->setTime(0, 0));
                continue;
            }

            $chunk = min($hoursToAdd, $hoursLeftInDay);
            $cursor = $cursor->modify("+{$chunk} hours");
            $hoursToAdd -= $chunk;

            if ($hoursToAdd > 0) {
                // Roll to the next business day's 09:00.
                $cursor = $this->advanceToNextBusinessHour($cursor->modify('+1 day')->setTime(0, 0));
            }
        }

        return $cursor;
    }

    private function advanceToNextBusinessHour(\DateTimeImmutable $cursor): \DateTimeImmutable
    {
        // 1. Skip weekend days.
        while (in_array((int) $cursor->format('w'), self::WEEKEND_DAYS, true)) {
            $cursor = $cursor->modify('+1 day')->setTime(self::BUSINESS_DAY_START_HOUR, 0);
        }
        // 2. If before 09:00, roll to 09:00 same day.
        if ((int) $cursor->format('H') < self::BUSINESS_DAY_START_HOUR) {
            return $cursor->setTime(self::BUSINESS_DAY_START_HOUR, 0);
        }
        // 3. If at/after 17:00, roll to next day's 09:00 + retry weekend skip.
        if ((int) $cursor->format('H') >= self::BUSINESS_DAY_END_HOUR) {
            return $this->advanceToNextBusinessHour($cursor->modify('+1 day')->setTime(self::BUSINESS_DAY_START_HOUR, 0));
        }
        return $cursor;
    }
}
