@props(['active'])

@php
    $classes = ($active ?? false)
        ? 'inline-flex items-center px-3 py-2 text-sm font-semibold text-brand-700 border-b-2 border-brand-600 transition'
        : 'inline-flex items-center px-3 py-2 text-sm font-medium text-ink-600 border-b-2 border-transparent hover:text-ink-900 hover:border-ink-200 transition';
@endphp

<a {{ $attributes->merge(['class' => $classes]) }}>
    {{ $slot }}
</a>
