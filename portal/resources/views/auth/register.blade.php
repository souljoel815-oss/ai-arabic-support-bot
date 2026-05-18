<x-guest-layout>
    @section('title', 'ابدأ التجربة · ' . __('messages.app.name'))

    <header class="mb-6 text-center">
        <h1 class="text-2xl font-bold text-ink-950 mb-1">ابدأ التجربة المجانية</h1>
        <p class="text-sm text-ink-600">14 يوم تجربة مجانية لكل المميزات. بدون بطاقة ائتمان.</p>
    </header>

    <form method="POST" action="{{ route('register') }}" class="space-y-4">
        @csrf

        <div>
            <x-input-label for="display_name" value="الاسم / Name" />
            <x-text-input id="display_name" type="text" name="display_name" :value="old('display_name')" required autofocus autocomplete="name" />
            <x-input-error :messages="$errors->get('display_name')" />
        </div>

        <div>
            <x-input-label for="organisation_legal_name_ar" value="اسم الشركة بالعربية / Organisation legal name" />
            <x-text-input id="organisation_legal_name_ar" type="text" name="organisation_legal_name_ar" :value="old('organisation_legal_name_ar')" required dir="rtl" />
            <p class="form-hint">ده هيظهر على الفواتير الضريبية. تقدر تعدّله من الإعدادات بعدين.</p>
            <x-input-error :messages="$errors->get('organisation_legal_name_ar')" />
        </div>

        <div>
            <x-input-label for="email" value="البريد الإلكتروني / Email" />
            <x-text-input id="email" type="email" name="email" :value="old('email')" required autocomplete="username" />
            <x-input-error :messages="$errors->get('email')" />
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
                <x-input-label for="password" value="كلمة المرور" />
                <x-text-input id="password" type="password" name="password" required autocomplete="new-password" />
                <p class="form-hint">12 حرف على الأقل + حرف كبير + صغير + رقم.</p>
                <x-input-error :messages="$errors->get('password')" />
            </div>
            <div>
                <x-input-label for="password_confirmation" value="تأكيد كلمة المرور" />
                <x-text-input id="password_confirmation" type="password" name="password_confirmation" required autocomplete="new-password" />
            </div>
        </div>

        <div>
            <x-input-label for="locale_preference" value="اللغة المفضلة" />
            <select id="locale_preference" name="locale_preference" class="form-select">
                <option value="ar-EG" {{ old('locale_preference', 'ar-EG') === 'ar-EG' ? 'selected' : '' }}>العربية</option>
                <option value="en-US" {{ old('locale_preference') === 'en-US' ? 'selected' : '' }}>English</option>
            </select>
        </div>

        <x-primary-button class="w-full mt-2">
            ابدأ التجربة
        </x-primary-button>

        <p class="text-xs text-ink-500 text-center">
            بإنشاء الحساب فأنت توافق على
            <a href="/terms" class="text-brand-700 hover:underline">شروط الاستخدام</a>
            و
            <a href="/privacy" class="text-brand-700 hover:underline">سياسة الخصوصية</a>.
        </p>
    </form>

    <p class="mt-6 text-center text-sm text-ink-600">
        مسجّل عندك حساب؟
        <a href="{{ route('login') }}" class="text-brand-700 font-semibold hover:text-brand-800">تسجيل الدخول ←</a>
    </p>
</x-guest-layout>
