<x-guest-layout>
    @section('title', 'تأكيد البريد · ' . __('messages.app.name'))

    <header class="mb-6 text-center">
        <div class="mx-auto mb-4 h-16 w-16 rounded-2xl flex items-center justify-center shadow-brand-glow"
             style="background: linear-gradient(135deg, #d68a1f 0%, #b06d18 100%);">
            <svg xmlns="http://www.w3.org/2000/svg" class="h-8 w-8 text-white" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2zM22 6l-10 7L2 6" />
            </svg>
        </div>
        <p class="eyebrow mb-2">خطوة أخيرة</p>
        <h1 class="display-2 mb-2">تأكيد بريدك الإلكتروني</h1>
        <p class="text-sm text-ink-600 leading-relaxed">
            أرسلنا لك رابط تأكيد على بريدك. يمكنك تصفّح البوابة بشكل عادي،
            لكن قبل الاشتراك أو تنشيط ترخيص لازم تأكّد البريد أولاً.
        </p>
    </header>

    @if (session('status') == 'verification-link-sent')
        <div class="mb-4 rounded-xl bg-emerald-50 border border-emerald-200 px-4 py-3 text-sm text-emerald-900 flex items-center gap-2">
            <span class="text-emerald-600">✓</span>
            <span>تم إرسال رابط تأكيد جديد للبريد المسجّل.</span>
        </div>
    @endif

    <div class="space-y-3">
        <form method="POST" action="{{ route('verification.send') }}">
            @csrf
            <x-primary-button class="w-full !py-3">
                إعادة إرسال رابط التأكيد
            </x-primary-button>
        </form>

        <a href="{{ route('portal.dashboard') }}" class="btn-secondary w-full">
            متابعة للبوابة بدون تأكيد
        </a>

        <form method="POST" action="{{ route('logout') }}">
            @csrf
            <button type="submit" class="btn-ghost w-full justify-center">
                تسجيل الخروج
            </button>
        </form>
    </div>
</x-guest-layout>
