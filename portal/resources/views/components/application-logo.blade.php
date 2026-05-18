{{-- DaftarX brand mark.
     Uses the official logo PNG from /images/daftarx-logo.png (kufic
     "دفتر" + DaftarX wordmark baked in). The `variant` prop flips the
     filter for dark backgrounds (slight brightness lift). --}}
@props(['size' => 'md', 'variant' => 'light'])
@php
    // Logo PNG has generous padding around the kufic icon + a small
    // "DaftarX" wordmark baked in below — render heights need to be
    // bumped or the actual icon glyph reads tiny in the navbar/sidebar.
    $heightClass = match ($size) {
        'sm' => 'h-14',
        'md' => 'h-16',
        'lg' => 'h-24',
        'xl' => 'h-32',
        default => 'h-16',
    };
    $filterStyle = $variant === 'dark'
        ? 'filter: brightness(1.15);'
        : '';
@endphp
<img src="/images/daftarx-logo.png"
     alt="DaftarX"
     style="{{ $filterStyle }}"
     {{ $attributes->class(['inline-block w-auto', $heightClass])->merge() }}>
