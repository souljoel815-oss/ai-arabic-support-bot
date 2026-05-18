{{-- DaftarX brand mark. Two variants:
       size=xs|sm → glyph only (compact rail / collapsed sidebar)
       size=md|lg|xl → glyph + wordmark
     The glyph is a gold-bordered square holding a stylised "DX" monogram
     so it reads as a brand even at 24px (favicon-class). --}}
@props(['size' => 'md', 'variant' => 'light'])
@php
    $sizeClasses = match ($size) {
        'sm' => 'text-base',
        'md' => 'text-2xl',
        'lg' => 'text-3xl',
        'xl' => 'text-5xl',
        default => 'text-2xl',
    };
    $glyphSize = match ($size) {
        'sm' => 'h-7 w-7 text-xs',
        'md' => 'h-9 w-9 text-sm',
        'lg' => 'h-12 w-12 text-base',
        'xl' => 'h-16 w-16 text-lg',
        default => 'h-9 w-9 text-sm',
    };
    $wordmarkColor = $variant === 'dark' ? 'text-white' : 'text-ink-950';
@endphp
<span {{ $attributes->class(['inline-flex items-center gap-2.5'])->merge() }}>
    {{-- Glyph mark: gold square + DX monogram --}}
    <span class="{{ $glyphSize }} relative inline-flex shrink-0 items-center justify-center rounded-lg font-black tracking-tight text-white shadow-brand-glow"
          style="background: linear-gradient(135deg, #d68a1f 0%, #b06d18 50%, #8b5616 100%);">
        <span class="relative z-10">DX</span>
        <span class="pointer-events-none absolute inset-0 rounded-lg ring-1 ring-inset ring-white/20"></span>
    </span>
    @if (!in_array($size, ['xs', 'sm'], true))
        <span class="wordmark {{ $sizeClasses }} {{ $wordmarkColor }} leading-none">
            Daftar<span class="wordmark-accent">X</span>
        </span>
    @endif
</span>
