<?php

namespace App\Services\Subscriptions;

use App\Models\Invoice;
use Illuminate\Support\Facades\DB;

/**
 * T078 (helper) — sequential per-organisation invoice number generator
 * matching the FR-016 Egyptian tax-line numbering requirement.
 *
 * Format: INV-YYYY-NNNNN where YYYY = invoice year, NNNNN = 5-digit
 * sequence reset annually inside each organisation. The DB unique
 * constraint on (customer_organisation_id, invoice_number) catches
 * any race; on collision the caller retries within the transaction.
 */
class InvoiceNumberGenerator
{
    public function next(string $customerOrganisationId, ?int $year = null): string
    {
        $year ??= (int) now()->format('Y');

        // SELECT MAX(...) inside an open transaction with a row-level
        // lock prevents two concurrent invoice issuers from getting the
        // same number. SQLite serializes writes anyway; MySQL uses
        // SELECT ... FOR UPDATE (Laravel's lockForUpdate()).
        $prefix = "INV-{$year}-";
        $existing = DB::table('invoices')
            ->where('customer_organisation_id', $customerOrganisationId)
            ->where('invoice_number', 'like', $prefix.'%')
            ->lockForUpdate()
            ->pluck('invoice_number');

        $maxSeq = 0;
        foreach ($existing as $num) {
            if (preg_match('/^INV-\d{4}-(\d+)$/', $num, $m)) {
                $maxSeq = max($maxSeq, (int) $m[1]);
            }
        }

        return $prefix.str_pad((string) ($maxSeq + 1), 5, '0', STR_PAD_LEFT);
    }
}
