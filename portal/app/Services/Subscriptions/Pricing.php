<?php

namespace App\Services\Subscriptions;

use App\Models\Subscription;
use InvalidArgumentException;

/**
 * T085 (partial) — single source of truth for tier prices. Used by
 * ConvertTrialToPaidService when computing the first-period invoice
 * amount, by UpgradeTierService for proration math, and by the
 * Pricing Blade page where the translation files mirror these numbers.
 *
 * All amounts are EGP-inclusive of Egyptian VAT (14%). Stored as
 * integer piasters (×100) to avoid float rounding throughout the
 * payment + accounting chain.
 *
 * If you change the numbers here, also update
 * lang/{ar-EG,en-US}/marketing.php pricing.tiers.*.monthly|annual so
 * the Blade pricing-page tier cards stay in sync.
 */
class Pricing
{
    public const VAT_RATE_BPS = 1400; // 14.00% in basis points

    /** @var array<string, array{Monthly:int, Annual:int}> piasters */
    public const PRICE_TABLE = [
        Subscription::TIER_SOLO => ['Monthly' => 45_000, 'Annual' => 450_000],
        Subscription::TIER_SMB => ['Monthly' => 120_000, 'Annual' => 1_200_000],
        Subscription::TIER_ENTERPRISE => ['Monthly' => 350_000, 'Annual' => 3_500_000],
        Subscription::TIER_FIRM => ['Monthly' => 750_000, 'Annual' => 7_500_000],
    ];

    /**
     * @return array{amount_piasters:int, vat_piasters:int}
     *         where amount_piasters = total including VAT
     *         and vat_piasters = the VAT portion of that total
     */
    public static function tierPrice(string $tier, string $cadence): array
    {
        if (! isset(self::PRICE_TABLE[$tier][$cadence])) {
            throw new InvalidArgumentException("Unknown tier+cadence: {$tier} / {$cadence}");
        }
        $total = self::PRICE_TABLE[$tier][$cadence];

        // VAT-inclusive backout: VAT = total - total / (1 + rate)
        // total = net + vat, vat = total - (total / (1 + rateRatio))
        $rateRatio = self::VAT_RATE_BPS / 10_000; // 0.14
        $netFloat = $total / (1 + $rateRatio);
        $vat = (int) round($total - $netFloat);

        return [
            'amount_piasters' => $total,
            'vat_piasters' => $vat,
        ];
    }

    /**
     * Compute the prorated remainder of the new tier owed when an
     * operator upgrades mid-period. Returns the additional charge
     * (in piasters) the customer pays today. Used by UpgradeTierService.
     */
    public static function upgradeProration(
        string $oldTier,
        string $newTier,
        string $cadence,
        int $daysRemaining,
        int $totalDaysInPeriod,
    ): int {
        $oldDailyTotal = self::PRICE_TABLE[$oldTier][$cadence] / $totalDaysInPeriod;
        $newDailyTotal = self::PRICE_TABLE[$newTier][$cadence] / $totalDaysInPeriod;
        $diff = ($newDailyTotal - $oldDailyTotal) * $daysRemaining;
        return (int) round(max($diff, 0));
    }
}
