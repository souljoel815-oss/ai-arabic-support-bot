@props(['status'])

@if ($status)
    <div {{ $attributes->merge(['class' => 'rounded-lg bg-emerald-50 border border-emerald-200 px-4 py-3 text-sm text-emerald-800']) }}>
        {{ $status }}
    </div>
@endif
