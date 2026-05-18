@extends('layouts.portal')

@section('title', 'حذف الحساب')

@section('content')
    <header class="mb-6">
        <h1 class="text-2xl font-bold text-ink-950 mb-1">حذف الحساب</h1>
        <p class="text-ink-600">طلب حذف نهائي لمؤسستك من DaftarX.</p>
    </header>

    @if ($org->soft_deleted_at)
        <div class="card-padded mb-6 border-red-200 bg-red-50">
            <h2 class="text-lg font-bold text-red-900 mb-2">حسابك مجدول للحذف</h2>
            <p class="text-sm text-red-800 mb-3">
                طلب الحذف اتعمل في {{ $org->soft_deleted_at->format('Y-m-d') }}،
                والحذف النهائي هيتم في
                <strong>{{ $org->soft_deleted_at->copy()->addDays(30)->format('Y-m-d') }}</strong>.
                لسه عندك {{ now()->diffInDays($org->soft_deleted_at->copy()->addDays(30)) }} يوم لإلغاء الطلب.
            </p>
            <form method="POST" action="{{ route('portal.account.restore') }}">
                @csrf
                <button type="submit" class="btn-primary">
                    إلغاء طلب الحذف واستعادة الحساب
                </button>
            </form>
        </div>
    @else
        <div class="card-padded mb-6 border-red-200 bg-red-50">
            <h2 class="text-lg font-bold text-red-900 mb-3">⚠ تحذير</h2>
            <ul class="space-y-2 text-sm text-red-800 mb-4 list-disc list-inside">
                <li>الحذف بيشتغل بـ <strong>30 يوم Grace Period</strong>: الحساب هيـ deactivate فوراً + هتقدر تستعيده خلال 30 يوم.</li>
                <li>بعد 30 يوم: حذف <strong>نهائي</strong> لكل البيانات (اشتراكات، تراخيص، فواتير، تذاكر).</li>
                <li>سجل التدقيق (Audit Log) بيتحفظ دائماً عشان متطلبات الرقابة المالية.</li>
                <li>الأعضاء التانيين في المؤسسة هيخرجوا من الحساب فوراً.</li>
                <li>التراخيص النشطة على أجهزة العملاء هتنتهي بانتهاء فترة الاشتراك الحالية.</li>
            </ul>
        </div>

        <form method="POST" action="{{ route('portal.account.delete') }}" class="card-padded space-y-4 max-w-xl">
            @csrf
            <div>
                <label class="form-label">
                    عشان نتأكد، اكتب اسم الشركة بالظبط:
                    <strong class="text-red-700 font-mono text-sm">{{ $org->legal_name_ar }}</strong>
                </label>
                <input type="text" name="confirmation_text" required dir="rtl" class="form-input">
                @error('confirmation_text')<p class="form-error">{{ $message }}</p>@enderror
            </div>
            <div class="pt-2 border-t border-ink-100">
                <button type="submit" class="btn-danger">
                    أؤكد حذف الحساب
                </button>
                <a href="{{ route('portal.dashboard') }}" class="btn-ghost ms-2">إلغاء</a>
            </div>
        </form>
    @endif
@endsection
