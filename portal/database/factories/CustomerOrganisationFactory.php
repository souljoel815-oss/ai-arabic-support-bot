<?php

namespace Database\Factories;

use App\Models\CustomerOrganisation;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\CustomerOrganisation>
 */
class CustomerOrganisationFactory extends Factory
{
    protected $model = CustomerOrganisation::class;

    public function definition(): array
    {
        return [
            'legal_name_ar' => 'شركة ' . fake()->company(),
            'legal_name_en' => 'Test ' . fake()->company(),
            'billing_email' => fake()->companyEmail(),
            'country_code' => 'EG',
            'requires_mfa_for_owners' => false,
        ];
    }
}
