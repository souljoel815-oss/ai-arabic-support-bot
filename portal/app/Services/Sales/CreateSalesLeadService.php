<?php

namespace App\Services\Sales;

use App\Models\SalesLead;

/**
 * T080 (early — used by US1's Contact page). Persists a fresh sales-lead
 * row from the marketing-page contact form. No side effects beyond the
 * insert in v1 (no auto-notification, no CRM integration) — vendor staff
 * sees new leads in their internal Ops dashboard (Phase 9).
 */
class CreateSalesLeadService
{
    /**
     * @param  array{name:string,email:string,phone?:?string,interested_tier?:?string,message?:?string}  $data
     */
    public function create(array $data, ?string $referrerPage = null): SalesLead
    {
        return SalesLead::create([
            'name' => $data['name'],
            'email' => $data['email'],
            'phone' => $data['phone'] ?? null,
            'interested_tier' => $data['interested_tier'] ?? null,
            'referrer_page' => $referrerPage,
            'message' => $data['message'] ?? null,
        ]);
    }
}
