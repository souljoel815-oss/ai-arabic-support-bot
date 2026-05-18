@extends('layouts.portal')

@section('title', 'SLA dashboard')

@section('content')
    <header class="mb-8">
        <p class="eyebrow mb-2">Ops · Support SLA</p>
        <h1 class="display-1 mb-1.5">SLA — last 30 days</h1>
        <p class="text-ink-600">
            % of tickets answered within their tier's SLA (24h Solo/SMB, 4h Enterprise/Firm).
            Generated daily by <code class="text-xs bg-ink-100 rounded px-1.5 py-0.5">php artisan support:compute-sla-rollup</code>.
        </p>
    </header>

    @if ($allRows->isEmpty())
        <div class="card-padded text-center py-16">
            <p class="text-ink-600">No SLA data yet. The rollup runs nightly via scheduler.</p>
        </div>
    @else
        {{-- Headline metric --}}
        @php
            $latest = $allRows->first();
            $headlinePct = $latest?->percent_within_sla ?? 0;
            $color = $headlinePct >= 95 ? 'emerald' : ($headlinePct >= 85 ? 'amber' : 'red');
        @endphp
        <section class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
            <div class="card-padded relative overflow-hidden">
                <p class="eyebrow-muted mb-2">Today's % within SLA</p>
                <p class="text-5xl font-extrabold text-{{ $color }}-600 leading-none">{{ number_format($headlinePct, 1) }}<span class="text-2xl">%</span></p>
                <p class="text-xs text-ink-600 mt-2">{{ $latest?->replies_within_sla ?? 0 }} / {{ $latest?->total_tickets ?? 0 }} tickets</p>
            </div>
            <div class="card-padded">
                <p class="eyebrow-muted mb-2">30-day average</p>
                <p class="text-5xl font-extrabold text-ink-950 leading-none">
                    {{ number_format($thirtyDayAvg, 1) }}<span class="text-2xl">%</span>
                </p>
                <p class="text-xs text-ink-600 mt-2">across {{ $allRows->sum('total_tickets') }} tickets</p>
            </div>
            <div class="card-padded">
                <p class="eyebrow-muted mb-2">Tier with worst SLA</p>
                <p class="text-3xl font-extrabold text-ink-950 leading-tight">{{ $worstTier ?? '—' }}</p>
                <p class="text-xs text-ink-600 mt-2">{{ $worstTierPct }}% within SLA</p>
            </div>
        </section>

        {{-- Daily breakdown table --}}
        <section class="card overflow-hidden shadow-elevation-2">
            <table class="w-full text-sm">
                <thead class="bg-ink-50/70 text-ink-600">
                    <tr class="text-xs uppercase tracking-wider">
                        <th class="text-start px-5 py-3 font-semibold">Date</th>
                        <th class="text-start px-5 py-3 font-semibold">Total tickets</th>
                        <th class="text-start px-5 py-3 font-semibold">Within SLA</th>
                        <th class="text-start px-5 py-3 font-semibold">%</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-ink-100">
                    @foreach ($allRows as $row)
                        @php
                            $rowColor = $row->percent_within_sla >= 95
                                ? 'pill-success'
                                : ($row->percent_within_sla >= 85 ? 'pill-warning' : 'pill-danger');
                        @endphp
                        <tr class="hover:bg-ink-50/40 transition">
                            <td class="px-5 py-3 font-mono text-ink-800">{{ $row->rollup_date->format('Y-m-d') }}</td>
                            <td class="px-5 py-3">{{ $row->total_tickets }}</td>
                            <td class="px-5 py-3">{{ $row->replies_within_sla }}</td>
                            <td class="px-5 py-3"><span class="{{ $rowColor }}">{{ number_format($row->percent_within_sla, 1) }}%</span></td>
                        </tr>
                    @endforeach
                </tbody>
            </table>
        </section>
    @endif
@endsection
