<?php

/**
 * T102 (ar) — Subscription + Billing UI strings.
 */
return [
    'title' => 'الاشتراك',
    'subtitle' => 'إدارة خطّتك ودورات الدفع والإلغاء.',
    'no_active' => 'مفيش اشتراك مدفوع لحد دلوقتي. ابدأ خطّتك من زر «اشترك في خطة».',
    'subscribe_cta' => 'اشترك في خطة',

    'status' => [
        'Active' => 'نشط',
        'PastDue' => 'متأخر',
        'Cancelled' => 'ملغى',
        'Paused' => 'موقوف مؤقتاً',
    ],
    'cadence' => [
        'Monthly' => 'شهري',
        'Annual' => 'سنوي',
    ],
    'payment_method' => [
        'Card' => 'بطاقة (Visa / Mastercard)',
        'Fawry' => 'فوري',
        'InstaPay' => 'إنستا باي',
        'VodafoneCash' => 'فودافون كاش',
        'BankTransfer' => 'تحويل بنكي',
    ],

    'columns' => [
        'tier' => 'الفئة',
        'cadence' => 'الدفع',
        'status' => 'الحالة',
        'period_start' => 'بداية الفترة',
        'period_end' => 'نهاية الفترة',
        'cancelled_at' => 'تاريخ الإلغاء',
    ],

    'will_cancel_notice' => 'الاشتراك سيُلغى تلقائياً في :date.',

    'start' => [
        'title' => 'اختر خطّتك',
        'subtitle' => 'الأسعار شاملة الضريبة (14٪). تقدر تترقّى أو تنزل لاحقاً.',
        'tier_label' => 'الفئة',
        'cadence_label' => 'دورة الدفع',
        'payment_method_label' => 'طريقة الدفع',
        'payment_method_note' => 'الدفع الإلكتروني (Card/Fawry/InstaPay/VodafoneCash) يمر عبر Paymob. التحويل البنكي يحتاج مراجعة من فريق الدعم.',
        'submit' => 'إنشاء الاشتراك',
        'cancel' => 'إلغاء',
        'has_active_warning' => 'عندك اشتراك نشط بالفعل. لو عايز تغيّر الفئة استخدم زر «ترقية» من صفحة الاشتراك.',
    ],

    'cancel' => [
        'title' => 'إلغاء الاشتراك',
        'confirm' => 'هل أنت متأكد من إلغاء هذا الاشتراك؟ سيظل نشطاً حتى نهاية الفترة الحالية (:date) ثم لن يُجدد.',
        'submit' => 'تأكيد الإلغاء',
        'cancel_action' => 'تراجع',
    ],

    'started_flash' => 'تم إنشاء الاشتراك. الفاتورة :number جاهزة للدفع من صفحة الفواتير.',
    'cancelled_flash' => 'تم الإلغاء. الاشتراك سيظل نشطاً حتى نهاية الفترة الحالية.',
];
