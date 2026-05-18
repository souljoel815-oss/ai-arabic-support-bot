<x-guest-layout>
    @section('title', 'ابدأ التجربة · ' . __('messages.app.name'))

    <header class="mb-6">
        <p class="eyebrow mb-2">حساب جديد</p>
        <h1 class="display-2 mb-1.5">ابدأ التجربة المجانية</h1>
        <p class="text-sm text-ink-600">14 يوم تجربة مجانية لكل المميزات. بدون بطاقة ائتمان.</p>
    </header>

    <form method="POST" action="{{ route('register') }}" class="space-y-4">
        @csrf

        <div>
            <x-input-label for="display_name" value="الاسم" />
            <x-text-input id="display_name" type="text" name="display_name" :value="old('display_name')" required autofocus autocomplete="name"
                          placeholder="أحمد محمد" />
            <x-input-error :messages="$errors->get('display_name')" />
        </div>

        <div>
            <x-input-label for="organisation_legal_name_ar" value="الاسم القانوني للمؤسسة" />
            <x-text-input id="organisation_legal_name_ar" type="text" name="organisation_legal_name_ar" :value="old('organisation_legal_name_ar')" required dir="rtl"
                          placeholder="شركة ___ للتجارة" />
            <p class="form-hint">يظهر على الفواتير الضريبية. يمكن تعديله من الإعدادات لاحقاً.</p>
            <x-input-error :messages="$errors->get('organisation_legal_name_ar')" />
        </div>

        <div>
            <x-input-label for="email" value="البريد الإلكتروني" />
            <x-text-input id="email" type="email" name="email" :value="old('email')" required autocomplete="username"
                          placeholder="name@company.com" />
            <x-input-error :messages="$errors->get('email')" />
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
                <x-input-label for="password" value="كلمة المرور" />
                <x-text-input id="password" type="password" name="password" required autocomplete="new-password"
                              placeholder="••••••••••••" />
                <p class="form-hint">12 حرف · حرف كبير + صغير + رقم</p>
                <x-input-error :messages="$errors->get('password')" />
            </div>
            <div>
                <x-input-label for="password_confirmation" value="تأكيد كلمة المرور" />
                <x-text-input id="password_confirmation" type="password" name="password_confirmation" required autocomplete="new-password"
                              placeholder="••••••••••••" />
            </div>
        </div>

        <div>
            <x-input-label for="locale_preference" value="اللغة المفضلة" />
            <select id="locale_preference" name="locale_preference" class="form-select">
                <option value="ar-EG" {{ old('locale_preference', 'ar-EG') === 'ar-EG' ? 'selected' : '' }}>العربية</option>
                <option value="en-US" {{ old('locale_preference') === 'en-US' ? 'selected' : '' }}>English</option>
            </select>
        </div>

        <x-primary-button class="w-full !py-3 mt-2">
            ابدأ التجربة
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 rtl:rotate-180" viewBox="0 0 24 24"
                 fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M5 12h14M13 5l7 7-7 7" />
            </svg>
        </x-primary-button>

        <p class="text-xs text-ink-500 text-center leading-relaxed">
            بإنشاء الحساب فأنت توافق على
            <a href="/terms" class="text-brand-700 hover:underline">شروط الاستخدام</a>
            و
            <a href="/privacy" class="text-brand-700 hover:underline">سياسة الخصوصية</a>.
        </p>
    </form>

    <div class="mt-7 pt-6 border-t border-ink-100">
        <p class="text-center text-sm text-ink-600">
            مسجّل عندك حساب؟
            <a href="{{ route('login') }}" class="text-brand-700 font-semibold hover:text-brand-800">تسجيل الدخول ←</a>
        </p>
    </div>
</x-guest-layout>
