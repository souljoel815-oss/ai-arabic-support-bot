<x-guest-layout>
    @section('title', 'تأكيد البريد · ' . __('messages.app.name'))

    <header class="mb-6 text-center">
        <div class="mx-auto mb-3 w-12 h-12 rounded-full bg-brand-100 flex items-center justify-center text-2xl">
            ✉
        </div>
        <h1 class="text-2xl font-bold text-ink-950 mb-1">تأكيد بريدك الإلكتروني</h1>
        <p class="text-sm text-ink-600 leading-relaxed">
            بعتنالك لينك تأكيد على بريدك. تقدر تتفرّج على البوابة عادي،
            لكن لما تيجي تشترك أو تنشّط ترخيص لازم تأكّد البريد الأول.
        </p>
    </header>

    @if (session('status') == 'verification-link-sent')
        <div class="mb-4 rounded-lg bg-emerald-50 border border-emerald-200 px-4 py-3 text-sm text-emerald-800">
            ✓ تم إرسال لينك تأكيد جديد للبريد المسجّل.
        </div>
    @endif

    <div class="space-y-3">
        <form method="POST" action="{{ route('verification.send') }}">
            @csrf
            <x-primary-button class="w-full">
                إعادة إرسال لينك التأكيد
            </x-primary-button>
        </form>

        <a href="{{ route('portal.dashboard') }}" class="btn-secondary w-full">
            متابعة للبوابة (بدون تأكيد)
        </a>

        <form method="POST" action="{{ route('logout') }}">
            @csrf
            <button type="submit" class="btn-ghost w-full justify-center">
                تسجيل الخروج
            </button>
        </form>
    </div>
</x-guest-layout>
