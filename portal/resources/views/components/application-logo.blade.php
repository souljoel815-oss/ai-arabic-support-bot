{{-- DaftarX brand wordmark. No image asset — pure CSS so it scales
     + colour-shifts cleanly. Replaces the default Laravel SVG logo. --}}
@props(['size' => 'md'])
@php
    $sizeClasses = match ($size) {
        'sm' => 'text-lg',
        'md' => 'text-2xl',
        'lg' => 'text-4xl',
        'xl' => 'text-5xl',
        default => 'text-2xl',
    };
@endphp
<span {{ $attributes->class(['wordmark', $sizeClasses])->merge() }}>
    Daftar<span class="wordmark-accent">X</span>
</span>
