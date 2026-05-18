<x-guest-layout>
    @section('title', 'استعادة كلمة المرور · ' . __('messages.app.name'))

    <header class="mb-6">
        <p class="eyebrow mb-2">استعادة الحساب</p>
        <h1 class="display-2 mb-1.5">نسيت كلمة المرور؟</h1>
        <p class="text-sm text-ink-600">
            ادخل بريدك الإلكتروني وسنرسل لك رابط لإعادة تعيين كلمة المرور.
        </p>
    </header>

    <x-auth-session-status class="mb-4" :status="session('status')" />

    <form method="POST" action="{{ route('password.email') }}" class="space-y-4">
        @csrf

        <div>
            <x-input-label for="email" value="البريد الإلكتروني" />
            <x-text-input id="email" type="email" name="email" :value="old('email')" required autofocus
                          placeholder="name@company.com" />
            <x-input-error :messages="$errors->get('email')" />
        </div>

        <x-primary-button class="w-full !py-3">
            إرسال رابط إعادة التعيين
        </x-primary-button>
    </form>

    <div class="mt-7 pt-6 border-t border-ink-100">
        <p class="text-center text-sm text-ink-600">
            <a href="{{ route('login') }}" class="text-brand-700 font-semibold hover:text-brand-800">← العودة لتسجيل الدخول</a>
        </p>
    </div>
</x-guest-layout>
