<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\Invoice;
use App\Services\InvoicePdf\ArabicInvoiceRenderer;
use App\Services\Subscriptions\RefundFirstPeriodService;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Http\Response;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\Storage;
use Throwable;

/**
 * T100. Billing UI:
 *   GET  /portal/billing                 → invoices list (newest first)
 *   GET  /portal/billing/{id}/pdf        → invoice PDF download (text receipt
 *                                          stub; DOMPDF Arabic renderer
 *                                          lands in T093 Phase 9 polish)
 *   POST /portal/billing/{id}/refund     → RefundFirstPeriodService
 */
class BillingController extends Controller
{
    public function __construct(
        private readonly RefundFirstPeriodService $refunder,
        private readonly ArabicInvoiceRenderer $pdfRenderer,
    ) {
    }

    public function index(): View
    {
        $orgIds = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->pluck('customer_organisations.id');

        $invoices = Invoice::query()
            ->whereIn('customer_organisation_id', $orgIds)
            ->with('subscription')
            ->orderByDesc('created_at')
            ->get();

        return view('portal.billing.invoices', compact('invoices'));
    }

    /**
     * Stream the Arabic-RTL invoice PDF. Renders on demand if a stored
     * copy doesn't exist yet — first hit pays the DOMPDF cost, subsequent
     * hits serve straight from the `local` disk.
     *
     * Only Paid invoices are renderable (Pending invoices have nothing
     * to bill the customer for yet).
     */
    public function downloadPdf(Invoice $invoice): Response
    {
        $this->authoriseInvoice($invoice);

        if ($invoice->status !== Invoice::STATUS_PAID) {
            abort(404, 'Invoice is not yet paid — no PDF available.');
        }

        $path = $invoice->pdf_storage_path && Storage::disk('local')->exists($invoice->pdf_storage_path)
            ? $invoice->pdf_storage_path
            : $this->pdfRenderer->renderAndStore($invoice);

        return response(Storage::disk('local')->get($path), 200, [
            'Content-Type' => 'application/pdf',
            'Content-Disposition' => 'attachment; filename="invoice-' . $invoice->invoice_number . '.pdf"',
            'Cache-Control' => 'no-store',
        ]);
    }

    public function refund(Request $request, Invoice $invoice): RedirectResponse
    {
        $this->authoriseInvoice($invoice);
        try {
            $this->refunder->refund($invoice, Auth::user(), $request->ip() ?? '0.0.0.0');
        } catch (Throwable $e) {
            return back()->withErrors(['refund' => $e->getMessage()]);
        }
        return redirect()->route('portal.billing')->with('status', __('billing.refunded_flash'));
    }

    private function authoriseInvoice(Invoice $invoice): void
    {
        $orgIds = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->pluck('customer_organisations.id');
        if (! $orgIds->contains($invoice->customer_organisation_id)) {
            abort(403);
        }
    }
}
