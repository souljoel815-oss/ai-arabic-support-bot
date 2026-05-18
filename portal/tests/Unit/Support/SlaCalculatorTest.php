<?php

namespace Tests\Unit\Support;

use App\Models\Subscription;
use App\Services\Support\SlaCalculator;
use DateTimeImmutable;
use PHPUnit\Framework\TestCase;

/**
 * T106 per FR-019. Egyptian work-week + 4h vs 24h tier rules.
 */
class SlaCalculatorTest extends TestCase
{
    private SlaCalculator $sla;

    protected function setUp(): void
    {
        parent::setUp();
        $this->sla = new SlaCalculator();
    }

    public function test_sla_hours_solo_is_24(): void
    {
        $this->assertSame(24, $this->sla->slaHours(Subscription::TIER_SOLO));
        $this->assertSame(24, $this->sla->slaHours(Subscription::TIER_SMB));
    }

    public function test_sla_hours_enterprise_and_firm_is_4(): void
    {
        $this->assertSame(4, $this->sla->slaHours(Subscription::TIER_ENTERPRISE));
        $this->assertSame(4, $this->sla->slaHours(Subscription::TIER_FIRM));
    }

    public function test_enterprise_4h_within_same_business_day(): void
    {
        // Sunday 10:00 + 4 business hours → Sunday 14:00.
        $opened = new DateTimeImmutable('2026-05-17 10:00:00');  // 2026-05-17 = Sunday
        $deadline = $this->sla->computeDeadline($opened, Subscription::TIER_ENTERPRISE);
        $this->assertSame('2026-05-17 14:00:00', $deadline->format('Y-m-d H:i:s'));
    }

    public function test_enterprise_4h_rolls_to_next_business_day_at_eod(): void
    {
        // Sunday 15:00 + 4 hours = 19:00, past 17:00, so:
        //   Sun 15→17 = 2 hours done, 2 left
        //   Mon 09→11 = 2 hours done
        $opened = new DateTimeImmutable('2026-05-17 15:00:00');  // Sunday
        $deadline = $this->sla->computeDeadline($opened, Subscription::TIER_ENTERPRISE);
        $this->assertSame('2026-05-18 11:00:00', $deadline->format('Y-m-d H:i:s'));
    }

    public function test_solo_24h_skips_weekend(): void
    {
        // Wed 14:00 + 24 business hours:
        //   Wed 14→17 = 3 hours
        //   Thu 09→17 = 8 hours (total 11)
        //   Fri + Sat are weekend (skip)
        //   Sun 09→17 = 8 hours (total 19)
        //   Mon 09→14 = 5 hours (total 24) → deadline Mon 14:00
        $opened = new DateTimeImmutable('2026-05-13 14:00:00');  // Wednesday
        $deadline = $this->sla->computeDeadline($opened, Subscription::TIER_SOLO);
        $this->assertSame('2026-05-18 14:00:00', $deadline->format('Y-m-d H:i:s'));
    }

    public function test_ticket_opened_on_friday_rolls_to_sunday_09_baseline(): void
    {
        // Fri 10:00 = weekend; first business hour is Sun 09:00.
        // + 4h enterprise = Sun 13:00.
        $opened = new DateTimeImmutable('2026-05-15 10:00:00');  // Friday
        $deadline = $this->sla->computeDeadline($opened, Subscription::TIER_ENTERPRISE);
        $this->assertSame('2026-05-17 13:00:00', $deadline->format('Y-m-d H:i:s'));
    }

    public function test_ticket_opened_before_09_rolls_to_09(): void
    {
        // Sun 07:00 (before business start) + 4h enterprise = Sun 13:00.
        $opened = new DateTimeImmutable('2026-05-17 07:00:00');  // Sunday before 9
        $deadline = $this->sla->computeDeadline($opened, Subscription::TIER_ENTERPRISE);
        $this->assertSame('2026-05-17 13:00:00', $deadline->format('Y-m-d H:i:s'));
    }
}
