<?php

namespace App\Http\Controllers\Portal;

use App\Http\Controllers\Controller;
use App\Models\AuditLogEntry;
use App\Models\CustomerOrganisation;
use App\Models\OrganisationMembership;
use Illuminate\Contracts\View\View;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;

/**
 * T143 per FR-023. Owner-only paginated + filterable audit-log viewer.
 * Shows every customer-visible state change for the active organisation
 * (members invited/removed, subscription changes, licence
 * transfers, ticket activity, payment clearings, refunds).
 *
 * Filters supported:
 *   - verb prefix (e.g. licence.* or subscription.*)
 *   - free-text in subject_id / actor display name
 *   - date range
 *
 * Pagination is 50 rows/page; the rollup view that aggregates by
 * verb-per-day is deferred to Phase 9 when ops needs it.
 */
class AuditLogController extends Controller
{
    public function show(Request $request): View
    {
        $org = $this->resolveOwnedOrg();

        $query = AuditLogEntry::query()
            ->where('customer_organisation_id', $org->id);

        // Filter: verb prefix (matches "licence" → "licence.*").
        if ($verbPrefix = trim((string) $request->query('verb', ''))) {
            $query->where('verb', 'like', $verbPrefix.'%');
        }

        // Filter: free-text search across actor display name + subject id.
        if ($search = trim((string) $request->query('q', ''))) {
            $query->where(function ($q) use ($search) {
                $q->where('actor_display_name_snapshot', 'like', "%{$search}%")
                    ->orWhere('subject_id', 'like', "%{$search}%");
            });
        }

        // Filter: date range (ISO YYYY-MM-DD).
        if ($from = $request->query('from')) {
            if (preg_match('/^\d{4}-\d{2}-\d{2}$/', $from)) {
                $query->where('occurred_at', '>=', $from . ' 00:00:00');
            }
        }
        if ($to = $request->query('to')) {
            if (preg_match('/^\d{4}-\d{2}-\d{2}$/', $to)) {
                $query->where('occurred_at', '<=', $to . ' 23:59:59');
            }
        }

        $entries = $query->orderByDesc('occurred_at')
            ->paginate(50)
            ->withQueryString();

        // Distinct verb prefixes for the filter dropdown.
        $verbPrefixes = AuditLogEntry::query()
            ->where('customer_organisation_id', $org->id)
            ->selectRaw("DISTINCT SUBSTR(verb, 1, INSTR(verb || '.', '.') - 1) as prefix")
            ->orderBy('prefix')
            ->pluck('prefix')
            ->filter()
            ->values();

        return view('portal.organisation.audit-log', [
            'org' => $org,
            'entries' => $entries,
            'verbPrefixes' => $verbPrefixes,
            'currentFilter' => [
                'verb' => $request->query('verb', ''),
                'q' => $request->query('q', ''),
                'from' => $request->query('from', ''),
                'to' => $request->query('to', ''),
            ],
        ]);
    }

    private function resolveOwnedOrg(): CustomerOrganisation
    {
        $org = Auth::user()
            ->customerOrganisations()
            ->wherePivotNull('revoked_at')
            ->wherePivotNotNull('accepted_at')
            ->wherePivot('role', OrganisationMembership::ROLE_OWNER)
            ->first();
        if ($org === null) {
            abort(403, __('messages.errors.not_a_member'));
        }
        return $org;
    }
}
