<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\Invoice;
use App\Services\Subscriptions\RefundFirstPeriodService;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Http\Response;
use Illuminate\Support\Facades\Auth;
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
    public function __construct(private readonly RefundFirstPeriodService $refunder)
    {
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
     * Plain-text "invoice" stub. Real DOMPDF Arabic-RTL rendering is
     * T093 (Phase 9 polish — needs DejaVu Sans + Cairo font embedding).
     */
    public function downloadPdf(Invoice $invoice): Response
    {
        $this->authoriseInvoice($invoice);

        $body = "DaftarX — Tax invoice\n"
            . "=====================\n\n"
            . "Number:   {$invoice->invoice_number}\n"
            . "Kind:     {$invoice->kind}\n"
            . "Method:   {$invoice->payment_method}\n"
            . "Status:   {$invoice->status}\n"
            . "Amount:   {$invoice->amount_egp} EGP (incl. VAT " . number_format($invoice->vat_egp_minor / 100, 2) . ")\n"
            . "Issued:   {$invoice->created_at?->toDateString()}\n"
            . ($invoice->paid_at ? "Paid:     {$invoice->paid_at->toDateString()}\n" : "")
            . "\n(DOMPDF Arabic-RTL invoice template lands in T093 Phase 9 polish.)\n";

        return response($body, 200, [
            'Content-Type' => 'text/plain; charset=utf-8',
            'Content-Disposition' => 'attachment; filename="invoice-' . $invoice->invoice_number . '.txt"',
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
