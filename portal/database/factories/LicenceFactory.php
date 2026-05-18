<?php

namespace Database\Factories;

use App\Models\Licence;
use App\Models\Subscription;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\Licence>
 */
class LicenceFactory extends Factory
{
    protected $model = Licence::class;

    public function definition(): array
    {
        return [
            'subscription_id' => Subscription::factory(),
            'hwid' => strtoupper(
                sprintf('%04X-%04X-%04X-%04X',
                    random_int(0, 0xFFFF), random_int(0, 0xFFFF),
                    random_int(0, 0xFFFF), random_int(0, 0xFFFF))
            ),
            'edition' => Subscription::TIER_SOLO,
            'signed_token_base64' => base64_encode('{"fake":"licence-token"}'),
            'issued_at' => now(),
            'expires_at' => now()->addMonth(),
        ];
    }
}
