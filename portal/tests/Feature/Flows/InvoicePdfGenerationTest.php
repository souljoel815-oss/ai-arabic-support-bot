<?php

namespace Tests\Feature\Flows;

use App\Models\CustomerOrganisation;
use App\Models\Invoice;
use App\Models\Subscription;
use App\Services\InvoicePdf\ArabicInvoiceRenderer;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Storage;
use Tests\TestCase;

/**
 * T077 — DOMPDF Arabic RTL invoice rendering per FR-016.
 *
 * Validates that:
 *   - the renderer produces a non-empty PDF byte stream
 *   - the file is stored at the expected path
 *   - the Invoice row's pdf_storage_path is updated
 *   - rendering a non-Paid invoice throws
 *
 * Doesn't decode the PDF content — DOMPDF's Arabic glyph coverage is
 * font-dependent and asserted manually in staging.
 */
class InvoicePdfGenerationTest extends TestCase
{
    use RefreshDatabase;

    public function test_renderer_produces_pdf_and_stores_at_expected_path(): void
    {
        Storage::fake('local');

        $org = CustomerOrganisation::factory()->create([
            'legal_name_ar' => 'شركة الاختبار للتجارة',
        ]);
        $subscription = Subscription::factory()->create([
            'customer_organisation_id' => $org->id,
            'tier' => Subscription::TIER_SOLO,
            'billing_cadence' => 'Monthly',
        ]);
        $invoice = Invoice::factory()->create([
            'customer_organisation_id' => $org->id,
            'subscription_id' => $subscription->id,
            'invoice_number' => 'INV-2026-12345',
            'kind' => Invoice::KIND_FIRST_PERIOD,
            'amount_egp_minor' => 50_000,
            'vat_egp_minor' => 6_140,
            'status' => Invoice::STATUS_PAID,
            'paid_at' => now(),
        ]);

        $path = app(ArabicInvoiceRenderer::class)->renderAndStore($invoice);

        $expectedPath = "invoices/{$org->id}/INV-2026-12345.pdf";
        $this->assertSame($expectedPath, $path);
        Storage::disk('local')->assertExists($expectedPath);

        // Stored PDF starts with the standard %PDF- header.
        $bytes = Storage::disk('local')->get($expectedPath);
        $this->assertTrue(str_starts_with($bytes, '%PDF-'), 'Output should be a real PDF.');

        // The invoice row records the storage path.
        $this->assertSame($expectedPath, $invoice->fresh()->pdf_storage_path);
    }

    public function test_renderer_rejects_non_paid_invoice(): void
    {
        $invoice = Invoice::factory()->create([
            'status' => Invoice::STATUS_PENDING,
        ]);

        $this->expectException(\RuntimeException::class);
        $this->expectExceptionMessage('non-Paid');

        app(ArabicInvoiceRenderer::class)->renderAndStore($invoice);
    }
}
