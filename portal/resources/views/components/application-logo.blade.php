{{-- DaftarX brand mark.
     Uses the official logo PNG from /images/daftarx-logo.png (kufic
     "دفتر" + DaftarX wordmark baked in). The `variant` prop flips the
     filter for dark backgrounds (slight brightness lift). --}}
@props(['size' => 'md', 'variant' => 'light'])
@php
    // Logo PNG has padding around the kufic icon + a small "DaftarX"
    // wordmark baked in below. Heights are bumped slightly vs the
    // earlier monogram component so the icon stays readable.
    $heightClass = match ($size) {
        'sm' => 'h-10',
        'md' => 'h-12',
        'lg' => 'h-16',
        'xl' => 'h-24',
        default => 'h-12',
    };
    $filterStyle = $variant === 'dark'
        ? 'filter: brightness(1.15);'
        : '';
@endphp
<img src="/images/daftarx-logo.png"
     alt="DaftarX"
     style="{{ $filterStyle }}"
     {{ $attributes->class(['inline-block w-auto', $heightClass])->merge() }}>
