<?php

namespace Database\Factories;

use App\Models\CustomerOrganisation;
use App\Models\Subscription;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\Subscription>
 */
class SubscriptionFactory extends Factory
{
    protected $model = Subscription::class;

    public function definition(): array
    {
        $start = now()->subDays(5);
        return [
            'customer_organisation_id' => CustomerOrganisation::factory(),
            'tier' => Subscription::TIER_SOLO,
            'billing_cadence' => 'Monthly',
            'status' => Subscription::STATUS_ACTIVE,
            'current_period_start_at' => $start,
            'current_period_end_at' => $start->copy()->addMonth(),
        ];
    }

    public function tier(string $tier): static
    {
        return $this->state(fn () => ['tier' => $tier]);
    }
}
