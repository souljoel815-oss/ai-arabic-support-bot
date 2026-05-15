# مواصفات تصميم لوحة تحكم "دفاتر" — Dashboard
**Operator-supplied, 2026-05-15 evening. Source of truth for the Dashboard look.**

> القوائم (Sidebar) **مقفولة** على نظام 2-tier (`ModuleSidebar` + `SubNavPanel`) — راجع `ui-redesign-spec-2026-05-15.md` §LOCKED. هذا الـ spec يطبّق على المحتوى الرئيسي والـ Dashboard فقط، **لا الـ Sidebar.**

## 1. نظام الألوان

| الاستخدام | اللون |
|---|---|
| خلفية الصفحة | `#0a1628` / `#0d1b2a` (navy داكن جداً) |
| خلفية الكارت | `#1a2332` / `#162032` (navy أفتح قليلاً) |
| خلفية الـ Sidebar | `#0a1628` (نفس الصفحة) |
| Gold accent | `#f0b90b` (primary) / `#d4a017` (hover) |
| نص أساسي | `#ffffff` |
| نص ثانوي | `#8899aa` / `#a0aec0` |
| Success | `#22c55e` |
| Warning | `#f97316` |
| Danger | `#ef4444` |
| Info | `#3b82f6` |
| حدود كارت | `rgba(255,255,255,0.08)` |
| زر sidebar نشط | bg gold + text `#0a1628` |

## 2. التخطيط

```
┌──────────────────────────────────────────────┐
│ Header / Top Bar                              │
├─────────────────────────────┬────────────────┤
│ Main Content                │ Sidebar (RIGHT) │
│ (KPIs, charts, table, etc.) │ (250px in RTL) │
├─────────────────────────────┴────────────────┤
│ Footer                                        │
└──────────────────────────────────────────────┘
```

## 3. Top Bar (من اليمين لليسار)

1. **Logo**: "دفاتر للمحاسبة والفواتير" — أيقونة كتاب/دفتر gold + الاسم
2. **رسالة ترحيب**: "مرحباً، أحمد 👋" + sub: "لوحة التحكم"
3. **Date range picker pill**: "مايو 2024 - 31 مايو 2024" + 📅 icon
4. **Company selector dropdown**: "شركة النور للتجارة" + "الرقم الضريبي: 123-456-789" + chevron
5. **أيقونات**: ⚙ Settings + 🔔 bell بـ badge أحمر "3"

## 4. 5 KPI Cards (الصف الأول، من اليمين لليسار)

| الكارت | القيمة | المقارنة | الأيقونة |
|---|---|---|---|
| إجمالي المبيعات | 1,250,000.00 ج.م | +12.5% (green) | TrendingUp + sparkline gold |
| إجمالي المشتريات | 750,000.00 ج.م | +8.3% (green) | ShoppingCart |
| صافي الربح | 325,000.00 ج.م | +15.7% (green) | DollarSign |
| المستحقات المدينة | 180,000.00 ج.م | -5.2% (red) | UserCheck |
| المستحقات الدائنة | 95,000.00 ج.م | -3.1% (red) | Wallet/Briefcase |

### تصميم الكارت
- خلفية داكنة + حدود خفيفة
- أيقونة في الزاوية العليا
- العنوان بخط صغير رمادي
- القيمة بخط كبير أبيض bold
- نسبة المقارنة بخط صغير (green/red)
- **Mini sparkline** ذهبي في الكارت الأول فقط

## 5. الصف الأوسط (3 أعمدة)

### 5.1 الالتزام الضريبي (يمين)
- Badge gold: "ملتزم"
- أيقونة شعار الجمهورية المصرية (نسر) gold كبير
- عنوان: "حالتك الضريبية سليمة"
- وصف: "جميع إقراراتك محدثة وتم تقديمها بنجاح"
- 4 checks خضراء:
  - ✓ تسجيل في منظومة الفاتورة الإلكترونية
  - ✓ إرسال الفواتير الإلكترونية
  - ✓ الإقرار الضريبي الشهري
  - ✓ سداد المستحقات الضريبية
- Button بحدود gold: "عرض التفاصيل الضريبية ←"

### 5.2 المبيعات والمشتريات (وسط — Bar Chart)
- Toggle: "شهري" (active) | "6 أشهر"
- Legend: 🟡 المبيعات (gold) — 🔵 المشتريات (blue)
- Grouped Bar Chart, 6 شهور (ديسمبر → مايو)
- Y-axis: 0 → 1,500K بخطوات 250K

### 5.3 المبيعات حسب طرق الدفع (يسار — Donut)
- Donut chart مع 1,250,000 ج.م في النص
- 4 segments:
  - تحويل بنكي 40% (gold)
  - نقدي 30% (teal/green)
  - بطاقة ائتمان 20% (yellow/gold-bright)
  - آجل 10% (blue)
- Legend جنب الـ donut
- رابط: "عرض تقرير مفصل ←"

## 6. الصف السفلي

### 6.1 إجراءات سريعة (يمين، 2×2 grid)
- ⊕ إنشاء فاتورة جديدة
- 👤+ إضافة عميل جديد
- 🏢+ إضافة مورد جديد
- 📈 تقرير المبيعات

### 6.2 الفاتورة الإلكترونية (أسفل الإجراءات)
- Card بحدود gold خفيفة
- شعار كبير (drosop/shield + ✓)
- نص: "متصل بمنظومة الفاتورة الإلكترونية"
- Status: 🟢 متصل

### 6.3 آخر الفواتير (يسار — Table)
- عنوان "آخر الفواتير" + رابط "عرض الكل ←"
- أعمدة: رقم الفاتورة | العميل/المورد | النوع | التاريخ | الإجمالي | الضريبة | شامل الضريبة | الحالة
- Status pills:
  - مدفوعة → green
  - مسودة → gray
  - غير مدفوعة → red
  - مدفوعة جزئياً → orange
- نوع: "فاتورة مبيعات" gold / "فاتورة شراء" green

## 7. Footer

- يمين: "إصدار 2.1.0"
- يسار: "جميع الحقوق محفوظة © 2024 © دفاتر للمحاسبة والفواتير"

## 8. Design tokens

| Property | Value |
|---|---|
| Font | Cairo, Tajawal, IBM Plex Arabic |
| Heading size | 24-28px |
| Big number | 20-24px bold |
| Body | 14-16px |
| Small | 12px |
| Card radius | 12-16px |
| Button radius | 8px |
| Pill radius | 20px |
| Card padding | 20-24px |
| Section gap | 16-24px |
| Card border | `1px solid rgba(255,255,255,0.08)` |
| Shadow | `0 4px 6px rgba(0,0,0,0.1)` |

## 9. Responsive

- > 1200px: Layout كامل
- 768-1200: Sidebar → drawer
- < 768: KPIs 2 per row, sections stacked, table scroll-x

## 10. التطبيق على المشروع الحالي

✅ التطبيق:
- Color tokens: حدّث `--dx-bg`, `--dx-bg-card`, `--dx-gold` لتطابق الـ spec
- 5 KPI cards بنفس الترتيب والأيقونات والـ deltas
- Donut chart 4 segments بالألوان والنسب المطلوبة
- Compliance card بـ checklist و CTA gold
- Quick actions 2×2 + ETA card تحتها
- Recent invoices table بنفس الأعمدة + status pills
- Footer: إصدار + copyright

⛔ مش هيتغيّر:
- **القوائم (Sidebar architecture)** — مقفولة على 2-tier حسب decision سابقة.
