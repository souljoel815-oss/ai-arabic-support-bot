<x-guest-layout>
    @section('title', 'إعادة تعيين كلمة المرور · ' . __('messages.app.name'))

    <header class="mb-6">
        <p class="eyebrow mb-2">تعيين كلمة المرور</p>
        <h1 class="display-2 mb-1.5">كلمة مرور جديدة</h1>
        <p class="text-sm text-ink-600">ادخل كلمة مرور قوية وأكدها.</p>
    </header>

    <form method="POST" action="{{ route('password.store') }}" class="space-y-4">
        @csrf
        <input type="hidden" name="token" value="{{ $request->route('token') }}">

        <div>
            <x-input-label for="email" value="البريد الإلكتروني" />
            <x-text-input id="email" type="email" name="email" :value="old('email', $request->email)" required autofocus autocomplete="username" />
            <x-input-error :messages="$errors->get('email')" />
        </div>

        <div>
            <x-input-label for="password" value="كلمة المرور الجديدة" />
            <x-text-input id="password" type="password" name="password" required autocomplete="new-password"
                          placeholder="••••••••••••" />
            <p class="form-hint">12 حرف · حرف كبير + صغير + رقم</p>
            <x-input-error :messages="$errors->get('password')" />
        </div>

        <div>
            <x-input-label for="password_confirmation" value="تأكيد كلمة المرور" />
            <x-text-input id="password_confirmation" type="password" name="password_confirmation" required autocomplete="new-password"
                          placeholder="••••••••••••" />
            <x-input-error :messages="$errors->get('password_confirmation')" />
        </div>

        <x-primary-button class="w-full !py-3">
            تعيين كلمة المرور وتسجيل الدخول
        </x-primary-button>
    </form>
</x-guest-layout>
