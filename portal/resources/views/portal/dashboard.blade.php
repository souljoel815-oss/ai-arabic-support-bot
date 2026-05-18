@extends('layouts.portal')

@section('title', 'Dashboard')

@section('content')
    <header class="mb-6">
        <h1 class="text-2xl font-bold mb-1">أهلاً {{ $user->display_name ?? $user->email }}</h1>
        <p class="text-stone-600 text-sm">{{ $user->email }}</p>
    </header>

    {{-- FR-010 soft banner: paid actions need email verification first. --}}
    @if ($user->email_verified_at === null)
        <div class="mb-4 rounded border border-stone-300 bg-stone-50 px-4 py-3 text-sm flex items-center justify-between gap-4">
            <span>
                <strong>إيميلك مش متأكد لسه.</strong>
                تقدر تتفرّج على البوابة، لكن قبل ما تشترك أو تنشّط ترخيص لازم تأكّد إيميلك.
            </span>
            <form method="POST" action="{{ route('verification.send') }}">
                @csrf
                <button type="submit" class="rounded border border-stone-400 px-3 py-1 text-xs hover:bg-stone-100">
                    أعد إرسال رسالة التأكيد
                </button>
            </form>
        </div>
    @endif

    @if (! $hasActiveSubscription)
        {{-- T103 — "Subscribe to keep going" CTA when there's no active Subscription (FR-029 + FR-030). --}}
        <section class="rounded-lg border border-amber-300 bg-amber-50 p-6 mb-6">
            <h2 class="text-xl font-bold mb-2">{{ __('subscription.no_active') }}</h2>
            <p class="text-sm text-stone-700 mb-4">
                برنامج دفترx المثبّت عندك يعمل في وضع التجربة (14 يوم). للاستمرار بعد انتهاء التجربة، اشترك في خطة.
            </p>
            <a href="{{ route('portal.subscription.start') }}" class="inline-block rounded bg-amber-600 px-6 py-2 text-white font-semibold hover:bg-amber-700">
                {{ __('subscription.subscribe_cta') }}
            </a>
        </section>
    @else
        <section class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
            @foreach ($subscriptions as $subscription)
                <div class="rounded-lg border border-stone-200 bg-white p-4">
                    <h3 class="font-semibold mb-1">
                        {{ __('marketing.pricing.tiers.'.strtolower($subscription->tier).'.name') }}
                        @if ($subscription->hasPrioritySupport())
                            <span class="ms-1 text-xs text-amber-700">⚡</span>
                        @endif
                    </h3>
                    <p class="text-sm text-stone-600">
                        {{ __('subscription.cadence.'.$subscription->billing_cadence) }} ·
                        {{ __('subscription.status.'.$subscription->status) }}
                    </p>
                    <p class="text-xs text-stone-500 mt-2">
                        {{ __('subscription.columns.period_end') }}: {{ $subscription->current_period_end_at?->format('Y-m-d') }}
                    </p>
                    @if ($subscription->cancelled_at)
                        <p class="text-xs text-red-600 mt-1">
                            {{ __('subscription.will_cancel_notice', ['date' => $subscription->current_period_end_at?->format('Y-m-d')]) }}
                        </p>
                    @endif
                </div>
            @endforeach
        </section>

        <section class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
            <a href="{{ route('portal.licences') }}" class="rounded border border-stone-200 bg-white p-4 hover:bg-stone-50 block">
                <h3 class="font-semibold text-amber-700 mb-1">{{ __('licences.title') }}</h3>
                <p class="text-3xl font-bold">{{ $activeLicenceCount }}</p>
                <p class="text-xs text-stone-500">ترخيص نشط</p>
            </a>
            <a href="{{ route('portal.billing') }}" class="rounded border border-stone-200 bg-white p-4 hover:bg-stone-50 block">
                <h3 class="font-semibold text-amber-700 mb-1">{{ __('billing.title') }}</h3>
                <p class="text-3xl font-bold">{{ $recentInvoices->count() }}</p>
                <p class="text-xs text-stone-500">آخر الفواتير</p>
            </a>
            <a href="/portal/downloads" class="rounded border border-stone-200 bg-white p-4 hover:bg-stone-50 block">
                <h3 class="font-semibold text-amber-700 mb-1">التحميل</h3>
                <p class="text-sm text-stone-700">برنامج Windows + تطبيق Android</p>
            </a>
        </section>

        @if ($recentInvoices->isNotEmpty())
            <section class="rounded-lg border border-stone-200 bg-white p-6">
                <h3 class="text-lg font-bold mb-4">أحدث الفواتير</h3>
                <table class="w-full text-sm">
                    <thead class="border-b border-stone-200 text-stone-600">
                        <tr>
                            <th class="text-start py-2">{{ __('billing.columns.number') }}</th>
                            <th class="text-start py-2">{{ __('billing.columns.amount') }}</th>
                            <th class="text-start py-2">{{ __('billing.columns.status') }}</th>
                            <th class="text-start py-2">{{ __('billing.columns.issued') }}</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach ($recentInvoices as $invoice)
                            <tr class="border-b border-stone-100">
                                <td class="py-2 font-mono text-xs">{{ $invoice->invoice_number }}</td>
                                <td class="py-2">{{ $invoice->amount_egp }} EGP</td>
                                <td class="py-2">
                                    <span class="text-xs px-2 py-0.5 rounded {{ $invoice->status === 'Paid' ? 'bg-green-100 text-green-800' : ($invoice->status === 'Pending' ? 'bg-amber-100 text-amber-800' : 'bg-stone-100 text-stone-700') }}">
                                        {{ __('billing.status.'.$invoice->status) }}
                                    </span>
                                </td>
                                <td class="py-2 text-stone-600">{{ $invoice->created_at?->format('Y-m-d') }}</td>
                            </tr>
                        @endforeach
                    </tbody>
                </table>
            </section>
        @endif
    @endif
@endsection
