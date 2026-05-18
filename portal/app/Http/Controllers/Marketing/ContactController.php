<?php

namespace App\Http\Controllers\Marketing;

use App\Http\Controllers\Controller;
use App\Services\Sales\CreateSalesLeadService;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;

/**
 * T045 per FR-005. Contact / sales form. GET renders the form; POST
 * validates + persists a SalesLead row via CreateSalesLeadService +
 * redirects back with a success flash.
 *
 * v1 stays no-op on the email side — vendor staff watches the
 * internal sales-leads list. Future iteration: dispatch a notification
 * email to sales@daftarx.app.
 */
class ContactController extends Controller
{
    public function __construct(
        private readonly CreateSalesLeadService $createSalesLead,
    ) {
    }

    public function show(): View
    {
        return view('marketing.contact');
    }

    public function submit(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'name' => ['required', 'string', 'max:128'],
            'email' => ['required', 'email:rfc', 'max:256'],
            'phone' => ['nullable', 'string', 'max:32'],
            'interested_tier' => ['nullable', 'in:Solo,SMB,Enterprise,Firm'],
            'message' => ['nullable', 'string', 'max:4000'],
        ]);

        $this->createSalesLead->create(
            $data,
            referrerPage: $request->headers->get('referer') ?: '/contact'
        );

        return redirect()->back()->with('status', __('marketing.contact.form.success'));
    }
}
