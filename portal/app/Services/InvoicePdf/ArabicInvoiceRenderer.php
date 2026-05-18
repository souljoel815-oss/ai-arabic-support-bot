<?php

namespace App\Services\InvoicePdf;

use App\Models\Invoice;
use Barryvdh\DomPDF\Facade\Pdf;
use Illuminate\Support\Facades\Storage;
use RuntimeException;

/**
 * T093 + T094 per FR-016. Renders a paid invoice to a PDF, stores it
 * under `invoices/{org-id}/{invoice-number}.pdf` on the `local` disk,
 * and returns the storage path.
 *
 * Arabic + RTL handling:
 *   DOMPDF's bidi support depends on a font that ships Arabic glyphs.
 *   The bundled DejaVu Sans covers basic Arabic but lacks Cairo's clean
 *   typography. For best results, drop a TTF Arabic font into
 *   `storage/fonts/Cairo-Regular.ttf` + `Cairo-Bold.ttf` and DOMPDF will
 *   pick it up via the `@font-face` block in the Blade template. The
 *   template degrades gracefully to DejaVu if the file isn't present.
 *
 * Egyptian VAT line item is rendered explicitly (FR-016 requires the
 * VAT amount to appear on the customer-facing invoice, not just be
 * implied by the gross total).
 *
 * Sequential per-org invoice numbering is the responsibility of
 * `InvoiceNumberGenerator` — by the time the Invoice row reaches this
 * service, `invoice_number` is already in the canonical `INV-YYYY-#####`
 * format. We just print it.
 */
class ArabicInvoiceRenderer
{
    private const TEMPLATE_VIEW = 'invoices.template';

    /**
     * Renders the invoice to a PDF and stores it. Idempotent: if a PDF
     * already exists at the target path it is overwritten, so re-running
     * after a template tweak refreshes the file.
     *
     * @return string Relative storage path (e.g. `invoices/{uuid}/INV-2026-00042.pdf`).
     */
    public function renderAndStore(Invoice $invoice): string
    {
        if ($invoice->status !== Invoice::STATUS_PAID) {
            throw new RuntimeException(
                "Cannot render invoice PDF for non-Paid invoice {$invoice->invoice_number} (status={$invoice->status})."
            );
        }

        $invoice->loadMissing(['customerOrganisation', 'subscription']);

        $pdf = Pdf::loadView(self::TEMPLATE_VIEW, [
            'invoice' => $invoice,
            'org' => $invoice->customerOrganisation,
            'subscription' => $invoice->subscription,
            'amountEgp' => number_format($invoice->amount_egp_minor / 100, 2),
            'vatEgp' => number_format(($invoice->vat_egp_minor ?? 0) / 100, 2),
            'netEgp' => number_format(
                ($invoice->amount_egp_minor - ($invoice->vat_egp_minor ?? 0)) / 100,
                2
            ),
            'paidAt' => $invoice->paid_at?->format('Y-m-d H:i'),
        ])->setPaper('a4')->setOption([
            'isHtml5ParserEnabled' => true,
            'isRemoteEnabled' => false,    // PCI scope: never let template fetch a URL
            'defaultFont' => 'DejaVu Sans',
            'fontDir' => storage_path('fonts'),
            'fontCache' => storage_path('fonts'),
        ]);

        $path = $this->storagePath($invoice);
        Storage::disk('local')->put($path, $pdf->output());

        // Persist the storage path on the row so BillingController can
        // hand it back via the /portal/billing/{invoice}/pdf endpoint.
        if ($invoice->pdf_storage_path !== $path) {
            $invoice->pdf_storage_path = $path;
            $invoice->save();
        }

        return $path;
    }

    public function storagePath(Invoice $invoice): string
    {
        return "invoices/{$invoice->customer_organisation_id}/{$invoice->invoice_number}.pdf";
    }
}
