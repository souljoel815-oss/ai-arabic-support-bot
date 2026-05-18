@extends('layouts.marketing')

@section('title', __('marketing.contact.title'))

@section('content')
    <header class="mb-8 text-center">
        <h1 class="text-4xl font-bold mb-3">{{ __('marketing.contact.title') }}</h1>
        <p class="text-lg text-stone-700">{{ __('marketing.contact.subtitle') }}</p>
    </header>

    <div class="grid grid-cols-1 md:grid-cols-3 gap-8">
        {{-- Form: 2/3 width on desktop --}}
        <div class="md:col-span-2 rounded-lg border border-stone-200 bg-white p-6">
            @if (session('status'))
                <div class="mb-4 rounded bg-green-100 px-4 py-2 text-sm text-green-800">{{ session('status') }}</div>
            @endif

            <form method="POST" action="/contact" class="space-y-4">
                @csrf
                <div>
                    <label for="name" class="block text-sm font-semibold mb-1">{{ __('marketing.contact.form.name') }}</label>
                    <input id="name" name="name" type="text" required value="{{ old('name') }}" class="w-full rounded border border-stone-300 px-3 py-2 focus:border-amber-600 focus:ring-1 focus:ring-amber-600">
                    @error('name')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
                </div>
                <div>
                    <label for="email" class="block text-sm font-semibold mb-1">{{ __('marketing.contact.form.email') }}</label>
                    <input id="email" name="email" type="email" required value="{{ old('email') }}" class="w-full rounded border border-stone-300 px-3 py-2 focus:border-amber-600 focus:ring-1 focus:ring-amber-600">
                    @error('email')<p class="mt-1 text-sm text-red-600">{{ $message }}</p>@enderror
                </div>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                        <label for="phone" class="block text-sm font-semibold mb-1">{{ __('marketing.contact.form.phone') }}</label>
                        <input id="phone" name="phone" type="tel" value="{{ old('phone') }}" class="w-full rounded border border-stone-300 px-3 py-2 focus:border-amber-600 focus:ring-1 focus:ring-amber-600">
                    </div>
                    <div>
                        <label for="interested_tier" class="block text-sm font-semibold mb-1">{{ __('marketing.contact.form.interested_tier') }}</label>
                        <select id="interested_tier" name="interested_tier" class="w-full rounded border border-stone-300 px-3 py-2 focus:border-amber-600 focus:ring-1 focus:ring-amber-600">
                            <option value="">—</option>
                            @foreach (['Solo', 'SMB', 'Enterprise', 'Firm'] as $tier)
                                <option value="{{ $tier }}" {{ old('interested_tier') === $tier ? 'selected' : '' }}>
                                    {{ __('marketing.pricing.tiers.'.strtolower($tier).'.name') }}
                                </option>
                            @endforeach
                        </select>
                    </div>
                </div>
                <div>
                    <label for="message" class="block text-sm font-semibold mb-1">{{ __('marketing.contact.form.message') }}</label>
                    <textarea id="message" name="message" rows="5" class="w-full rounded border border-stone-300 px-3 py-2 focus:border-amber-600 focus:ring-1 focus:ring-amber-600">{{ old('message') }}</textarea>
                </div>
                <button type="submit" class="rounded bg-amber-600 px-6 py-2 text-white font-semibold hover:bg-amber-700">
                    {{ __('marketing.contact.form.submit') }}
                </button>
            </form>
        </div>

        {{-- Channels: 1/3 width sidebar --}}
        <aside class="rounded-lg border border-stone-200 bg-white p-6 h-fit">
            <h2 class="text-lg font-bold mb-4">{{ __('marketing.contact.channels_title') }}</h2>
            <ul class="space-y-3 text-sm">
                <li><strong>{{ __('marketing.contact.channels.email') }}:</strong><br>
                    <a href="mailto:{{ __('marketing.contact.channels.sales') }}" class="text-amber-700 hover:underline">{{ __('marketing.contact.channels.sales') }}</a>
                </li>
                <li><strong>{{ __('messages.nav.contact') }}:</strong><br>
                    <a href="mailto:{{ __('marketing.contact.channels.support') }}" class="text-amber-700 hover:underline">{{ __('marketing.contact.channels.support') }}</a>
                </li>
                <li><strong>{{ __('marketing.contact.channels.whatsapp') }}:</strong> +20 100 000 0000</li>
                <li><strong>{{ __('marketing.contact.channels.phone') }}:</strong> +20 2 0000 0000</li>
            </ul>
        </aside>
    </div>
@endsection
