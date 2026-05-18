<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\CustomerOrganisation;
use App\Models\Subscription;
use App\Models\SupportTicket;
use App\Models\SupportTicketAttachment;
use App\Services\Support\CreateTicketService;
use App\Services\Support\ReplyTicketService;
use App\Services\Support\SlaCalculator;
use Illuminate\Contracts\View\View;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\Http\Response;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\Storage;
use Throwable;

/**
 * T111-T114 per US4. Customer-side support-ticket UI:
 *   GET  /portal/support               → list of customer's tickets
 *   GET  /portal/support/new           → new-ticket form
 *   POST /portal/support               → CreateTicketService
 *   GET  /portal/support/{id}          → ticket detail + reply form
 *   POST /portal/support/{id}/reply    → ReplyTicketService (Customer side)
 *   GET  /portal/support/attachments/{id} → secure attachment download
 *
 * Vendor-staff UI lives outside this controller (internal vendor system).
 */
class SupportTicketController extends Controller
{
    public function __construct(
        private readonly CreateTicketService $createTicket,
        private readonly ReplyTicketService $replyTicket,
        private readonly SlaCalculator $sla,
    ) {
    }

    public function index(): View
    {
        $orgIds = $this->actorOrgIds();
        $tickets = SupportTicket::query()
            ->whereIn('customer_organisation_id', $orgIds)
            ->orderByDesc('opened_at')
            ->get();

        // Compute the SLA deadline per ticket against the active
        // subscription's tier (or Solo default if no subscription).
        $tierByOrg = Subscription::query()
            ->whereIn('customer_organisation_id', $orgIds)
            ->where('status', Subscription::STATUS_ACTIVE)
            ->pluck('tier', 'customer_organisation_id');

        $slaDeadlines = [];
        foreach ($tickets as $ticket) {
            $tier = $tierByOrg[$ticket->customer_organisation_id] ?? Subscription::TIER_SOLO;
            $slaDeadlines[$ticket->id] = $this->sla->computeDeadline($ticket->opened_at, $tier);
        }

        return view('portal.support.list', compact('tickets', 'slaDeadlines'));
    }

    public function showNew(): View
    {
        $org = $this->resolveActiveOrg();
        $highAllowed = Subscription::query()
            ->where('customer_organisation_id', $org->id)
            ->where('status', Subscription::STATUS_ACTIVE)
            ->whereIn('tier', [
                Subscription::TIER_SMB,
                Subscription::TIER_ENTERPRISE,
                Subscription::TIER_FIRM,
            ])
            ->exists();

        return view('portal.support.new', compact('org', 'highAllowed'));
    }

    public function create(Request $request): RedirectResponse
    {
        $data = $request->validate([
            'category' => ['required', 'in:Billing,Bug,FeatureRequest,AccountingQuestion,Urgent'],
            'priority' => ['required', 'in:Low,Normal,High'],
            'subject' => ['required', 'string', 'max:256'],
            'body' => ['required', 'string', 'max:8000'],
            'attachments' => ['nullable', 'array', 'max:3'],
            'attachments.*' => ['file', 'max:5120'], // 5 MB per file
        ]);

        $org = $this->resolveActiveOrg();

        try {
            $ticket = $this->createTicket->create(
                org: $org,
                actor: Auth::user(),
                category: $data['category'],
                priority: $data['priority'],
                subject: $data['subject'],
                body: $data['body'],
                attachments: $request->file('attachments') ?? [],
                originatingIp: $request->ip() ?? '0.0.0.0',
            );
        } catch (Throwable $e) {
            return back()->withInput()->withErrors(['priority' => $e->getMessage()]);
        }

        return redirect()
            ->route('portal.support.detail', ['ticket' => $ticket->id])
            ->with('status', __('support.created_flash'));
    }

    public function show(SupportTicket $ticket): View
    {
        $this->authoriseTicket($ticket);
        $tier = Subscription::query()
            ->where('customer_organisation_id', $ticket->customer_organisation_id)
            ->where('status', Subscription::STATUS_ACTIVE)
            ->value('tier') ?? Subscription::TIER_SOLO;

        $deadline = $this->sla->computeDeadline($ticket->opened_at, $tier);

        $ticket->load(['replies.authorTeamMember', 'attachments']);

        return view('portal.support.detail', compact('ticket', 'tier', 'deadline'));
    }

    public function reply(Request $request, SupportTicket $ticket): RedirectResponse
    {
        $this->authoriseTicket($ticket);
        $data = $request->validate([
            'body' => ['required', 'string', 'max:8000'],
        ]);
        try {
            $this->replyTicket->replyAsCustomer($ticket, Auth::user(), $data['body'], $request->ip() ?? '0.0.0.0');
        } catch (Throwable $e) {
            return back()->withInput()->withErrors(['body' => $e->getMessage()]);
        }
        return redirect()->route('portal.support.detail', ['ticket' => $ticket->id]);
    }

    public function downloadAttachment(SupportTicketAttachment $attachment): Response
    {
        $attachment->load('ticket');
        $this->authoriseTicket($attachment->ticket);

        if (! Storage::disk('local')->exists($attachment->storage_path)) {
            abort(404);
        }

        return response(
            Storage::disk('local')->get($attachment->storage_path),
            200,
            [
                'Content-Type' => $attachment->mime_type,
                'Content-Disposition' => 'attachment; filename="'.addslashes($attachment->original_filename).'"',
                'Content-Length' => (string) $attachment->size_bytes,
                'Cache-Control' => 'no-store',
            ]
        );
    }

    // ----- helpers --------------------------------------------------------

    private function actorOrgIds(): \Illuminate\Support\Collection
    {
        return Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->pluck('customer_organisations.id');
    }

    private function resolveActiveOrg(): CustomerOrganisation
    {
        $org = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->first();
        if ($org === null) {
            abort(403, __('messages.errors.not_a_member'));
        }
        return $org;
    }

    private function authoriseTicket(SupportTicket $ticket): void
    {
        if (! $this->actorOrgIds()->contains($ticket->customer_organisation_id)) {
            abort(403);
        }
    }
}
