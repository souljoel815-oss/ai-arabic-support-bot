@extends('layouts.marketing')

@section('title', __('marketing.terms.title'))

@section('content')
    <article class="max-w-3xl mx-auto">
        <h1 class="text-4xl font-bold mb-2">{{ __('marketing.terms.title') }}</h1>
        <p class="text-sm text-stone-500 mb-8">{{ __('marketing.terms.last_updated') }}</p>

        <p class="text-stone-700 mb-8 leading-relaxed">{{ __('marketing.terms.body') }}</p>

        @foreach (['use', 'data', 'liability', 'changes'] as $key)
            <section class="mb-6">
                <h2 class="text-xl font-bold mb-2">{{ __('marketing.terms.sections.'.$key.'.title') }}</h2>
                <p class="text-stone-700 leading-relaxed">{{ __('marketing.terms.sections.'.$key.'.body') }}</p>
            </section>
        @endforeach
    </article>
@endsection
