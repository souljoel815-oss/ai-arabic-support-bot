<?php

namespace Database\Factories;

use App\Models\CustomerOrganisation;
use App\Models\Invoice;
use App\Models\Subscription;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\Invoice>
 */
class InvoiceFactory extends Factory
{
    protected $model = Invoice::class;

    public function definition(): array
    {
        return [
            'customer_organisation_id' => CustomerOrganisation::factory(),
            'subscription_id' => Subscription::factory(),
            'invoice_number' => 'INV-2026-'.str_pad((string) fake()->unique()->numberBetween(1, 99999), 5, '0', STR_PAD_LEFT),
            'kind' => Invoice::KIND_FIRST_PERIOD,
            'payment_method' => 'BankTransfer',
            'amount_egp_minor' => 45_000,
            'vat_egp_minor' => 5_526,
            'status' => Invoice::STATUS_PENDING,
        ];
    }
}
