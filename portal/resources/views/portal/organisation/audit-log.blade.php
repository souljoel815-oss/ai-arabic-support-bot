@extends('layouts.portal')

@section('title', 'سجل التدقيق')

@section('content')
    <header class="mb-8">
        <a href="{{ route('portal.organisation') }}" class="inline-flex items-center gap-1.5 text-sm text-brand-700 hover:text-brand-800 mb-3 transition">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="m15 18-6-6 6-6" />
            </svg>
            المؤسسة
        </a>
        <p class="eyebrow mb-2">سجل النشاط الإداري</p>
        <h1 class="display-1 mb-1.5">سجل التدقيق</h1>
        <p class="text-ink-600">
            كل تعديل أو إجراء على المؤسسة + مين عمله + إمتى. السجل دائم — لا يُحذف حتى لو الحساب اتحذف.
        </p>
    </header>

    {{-- Filter bar --}}
    <form method="GET" action="{{ route('portal.organisation.audit-log') }}" class="card-padded mb-4 grid grid-cols-1 md:grid-cols-5 gap-3 items-end">
        <div>
            <label class="form-label text-xs">نوع الإجراء</label>
            <select name="verb" class="form-select text-sm">
                <option value="">الكل</option>
                @foreach ($verbPrefixes as $prefix)
                    <option value="{{ $prefix }}" {{ $currentFilter['verb'] === $prefix ? 'selected' : '' }}>{{ $prefix }}.*</option>
                @endforeach
            </select>
        </div>
        <div class="md:col-span-2">
            <label class="form-label text-xs">بحث</label>
            <input type="text" name="q" value="{{ $currentFilter['q'] }}" placeholder="اسم العضو أو معرّف الموضوع..."
                   class="form-input text-sm">
        </div>
        <div>
            <label class="form-label text-xs">من تاريخ</label>
            <input type="date" name="from" value="{{ $currentFilter['from'] }}" class="form-input text-sm">
        </div>
        <div>
            <label class="form-label text-xs">إلى تاريخ</label>
            <input type="date" name="to" value="{{ $currentFilter['to'] }}" class="form-input text-sm">
        </div>
        <div class="md:col-span-5 flex gap-2">
            <button type="submit" class="btn-primary !py-2">تطبيق الفلتر</button>
            <a href="{{ route('portal.organisation.audit-log') }}" class="btn-secondary !py-2">إعادة تعيين</a>
        </div>
    </form>

    {{-- Results --}}
    @if ($entries->isEmpty())
        <div class="card-padded text-center text-ink-600">
            مفيش إجراءات مسجّلة تطابق الفلتر.
        </div>
    @else
        <div class="card overflow-hidden">
            <table class="w-full text-sm">
                <thead class="bg-ink-50 border-b border-ink-100 text-ink-700">
                    <tr>
                        <th class="text-start px-4 py-3 font-semibold">التاريخ</th>
                        <th class="text-start px-4 py-3 font-semibold">الإجراء</th>
                        <th class="text-start px-4 py-3 font-semibold">الموضوع</th>
                        <th class="text-start px-4 py-3 font-semibold">المُنفِّذ</th>
                        <th class="text-start px-4 py-3 font-semibold">IP</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach ($entries as $entry)
                        @php
                            $verbColor = match (explode('.', $entry->verb)[0] ?? '') {
                                'organisation' => 'bg-sky-100 text-sky-800',
                                'subscription' => 'bg-brand-100 text-brand-800',
                                'licence' => 'bg-emerald-100 text-emerald-800',
                                'payment' => 'bg-purple-100 text-purple-800',
                                'member' => 'bg-amber-100 text-amber-900',
                                'ticket' => 'bg-pink-100 text-pink-800',
                                default => 'bg-ink-100 text-ink-700',
                            };
                        @endphp
                        <tr class="border-b border-ink-100 hover:bg-ink-50/50">
                            <td class="px-4 py-3 text-xs whitespace-nowrap text-ink-600" title="{{ $entry->occurred_at?->toIso8601String() }}">
                                {{ $entry->occurred_at?->format('Y-m-d H:i:s') }}
                            </td>
                            <td class="px-4 py-3">
                                <span class="pill {{ $verbColor }} font-mono text-[11px]">{{ $entry->verb }}</span>
                            </td>
                            <td class="px-4 py-3 text-xs">
                                <div class="text-ink-700">{{ $entry->subject_kind }}</div>
                                <div class="font-mono text-[10px] text-ink-500 truncate max-w-[180px]">{{ $entry->subject_id }}</div>
                            </td>
                            <td class="px-4 py-3 text-xs">
                                <div class="text-ink-800 font-medium">{{ $entry->actor_display_name_snapshot }}</div>
                                @if ($entry->actor_team_member_id === null)
                                    <div class="text-[10px] text-ink-500 italic">(نظام)</div>
                                @endif
                            </td>
                            <td class="px-4 py-3 text-xs font-mono text-ink-500">{{ $entry->originating_ip }}</td>
                        </tr>
                        @if ($entry->payload_json)
                            <tr class="border-b border-ink-100">
                                <td colspan="5" class="px-4 pb-3">
                                    <details class="text-xs">
                                        <summary class="cursor-pointer text-ink-500 hover:text-ink-700">عرض التفاصيل</summary>
                                        <pre class="mt-2 p-3 bg-ink-50 rounded text-[11px] overflow-x-auto font-mono">{{ json_encode($entry->payload_json, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE) }}</pre>
                                    </details>
                                </td>
                            </tr>
                        @endif
                    @endforeach
                </tbody>
            </table>
        </div>

        <div class="mt-4">
            {{ $entries->links() }}
        </div>
    @endif
@endsection
