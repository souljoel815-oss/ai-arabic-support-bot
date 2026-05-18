@props(['active'])

@php
    $classes = ($active ?? false)
        ? 'block w-full ps-3 pe-4 py-2 text-start text-base font-semibold text-brand-700 bg-brand-50 border-s-4 border-brand-600 transition'
        : 'block w-full ps-3 pe-4 py-2 text-start text-base font-medium text-ink-700 border-s-4 border-transparent hover:text-ink-950 hover:bg-ink-50 hover:border-ink-200 transition';
@endphp

<a {{ $attributes->merge(['class' => $classes]) }}>
    {{ $slot }}
</a>
