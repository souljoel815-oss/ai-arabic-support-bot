# DaftarX — Phase F Sprint Plan
## خطة تنفيذ الـ Odoo Parity المتبقية (38 يوم)

**تاريخ الإعداد:** 15 مايو 2026
**الهدف:** رفع تغطية DaftarX من 82% إلى 95%+ مقارنة بـ Odoo Accounting
**المرجع:** تحليل كورس "محاسب أودو المحترف" (160 فيديو) + DaftarX Verification Report (commit 58639ae)

---

## §1. ملخص تنفيذي

Phase F تحتوي على **8 features** مقسمة على **3 sprints** حسب الأولوية. الهدف هو الوصول لنقطة "جاهز للبيع" بأسرع وقت (Sprint 1 = 10 أيام) ثم إكمال الباقي بالتوازي مع أول عملاء.

```
Sprint 1 (10 أيام) → 88% parity → ابدأ بيع
Sprint 2 (21 يوم)  → 93% parity → عملاء أكبر
Sprint 3 (7 أيام)  → 95%+ parity → full coverage
```

---

## §2. الجدول الزمني الكامل

| الأسبوع | الأيام | Sprint | الـ Features | النتيجة |
|---------|--------|--------|-------------|---------|
| Week 1 | Day 1-4 | Sprint 1 | F.2 Realized FX | فرق العملة شغال |
| Week 2 | Day 5-7 | Sprint 1 | F.8 Year-end Retained Earnings | إغلاق السنة كامل |
| Week 2 | Day 8-10 | Sprint 1 | F.7 Auto-PO from Reorder | المخزون آلي |
| **Milestone** | **Day 10** | | | **✓ 88% → جاهز للبيع** |
| Week 3 | Day 11-20 | Sprint 2 | F.5 Landed Cost | المستوردين مغطيين |
| Week 4-5 | Day 21-26 | Sprint 2 | F.6 Multi-dim Cost Centers | الشركات الكبيرة |
| Week 5 | Day 27-31 | Sprint 2 | F.3 Unrealized FX Revaluation | إعادة تقييم آخر الشهر |
| **Milestone** | **Day 31** | | | **✓ 93% → enterprise-ready** |
| Week 6 | Day 32-33 | Sprint 3 | F.1 Deferred Revenue | شركات الاشتراكات |
| Week 6-7 | Day 34-38 | Sprint 3 | F.4 Fiscal Position | المصدرين + مناطق حرة |
| **Milestone** | **Day 38** | | | **✓ 95%+ → full Odoo parity** |

---

## §3. Sprint 1 — "جاهز للبيع" (10 أيام)

### F.2 — Realized FX (فرق العملة المحقق عند السداد)
**المدة:** 4 أيام
**الأولوية:** 🔴 Critical
**المرجع:** كورس أودو المحترف — Videos 138-139
**يعتمد على:** لا شيء

**الوصف:** عند سداد فاتورة بعملة أجنبية، لو سعر الصرف يوم السداد مختلف عن يوم الفاتورة، لازم يتسجل فرق العملة (ربح أو خسارة) تلقائياً.

**التفاصيل اليومية:**

| اليوم | المهمة | التفاصيل |
|-------|--------|----------|
| Day 1 | Schema + Config | إضافة `ExchangeGainAccount` و `ExchangeLossAccount` في Company settings. إضافة `ExchangeDifference` field في Payment entity |
| Day 2 | Engine Logic | عند تسجيل Payment: حساب الفرق = `(PaymentAmount × PaymentRate) - (InvoiceAmount × InvoiceRate)`. لو موجب → ExchangeGain. لو سالب → ExchangeLoss |
| Day 3 | Journal Entry | توليد قيد فرق العملة تلقائي: Debit/Credit حساب العميل/المورد + Credit/Debit حساب فرق العملة. ربط القيد بالـ Payment |
| Day 4 | UI + Tests | عرض فرق العملة في شاشة السداد + تقرير أرباح/خسائر العملة + Unit tests |

**القيد المحاسبي (مثال — ربح عملة):**
```
Dr. حساب المورد (بالجنيه المصري)     10,000
    Cr. البنك (بالجنيه المصري)                 9,800
    Cr. أرباح فروق عملة                          200
```

**Acceptance Criteria:**
- [ ] فاتورة بالدولار + سداد بسعر صرف مختلف → قيد فرق عملة تلقائي
- [ ] فرق العملة يظهر في قائمة الدخل (أرباح/خسائر أخرى)
- [ ] تقرير أرباح وخسائر فروق العملة
- [ ] Unit tests: 5+ scenarios (ربح، خسارة، سداد جزئي، multi-currency)

---

### F.8 — Year-end Retained Earnings JV (قيد إغلاق الأرباح المحتجزة)
**المدة:** 3 أيام
**الأولوية:** 🔴 Critical
**المرجع:** كورس أودو المحترف — Video 150
**يعتمد على:** لا شيء (Closing Cockpit موجود)

**الوصف:** في نهاية السنة المالية، صافي الربح/الخسارة لازم يتحول من حساب "أرباح السنة الجارية" إلى حساب "الأرباح المحتجزة" بقيد إغلاق تلقائي.

**التفاصيل اليومية:**

| اليوم | المهمة | التفاصيل |
|-------|--------|----------|
| Day 5 | Config + Logic | إضافة `RetainedEarningsAccount` في Company settings. حساب صافي الربح = مجموع الإيرادات - مجموع المصروفات للسنة المالية |
| Day 6 | JV Generation | توليد قيد الإغلاق: Zero out كل حسابات الإيرادات والمصروفات → ترحيل الصافي لحساب الأرباح المحتجزة. إضافة زر "Generate Year-end Closing" في Closing Cockpit |
| Day 7 | UI + Lock + Tests | عرض القيد في Closing Cockpit + إغلاق الفترة (Lock Date) بعد الترحيل + Unit tests |

**القيد المحاسبي (مثال — ربح 500,000):**
```
Dr. ملخص الدخل (الإيرادات)          2,000,000
    Cr. ملخص الدخل (المصروفات)              1,500,000
    Cr. الأرباح المحتجزة                       500,000
```

**Acceptance Criteria:**
- [ ] زر "إغلاق السنة المالية" في Closing Cockpit
- [ ] القيد يصفّر كل حسابات الإيرادات والمصروفات
- [ ] الصافي يرحّل لحساب الأرباح المحتجزة
- [ ] بعد الإغلاق: الفترة تتقفل (Lock Date)
- [ ] Unit tests: ربح + خسارة + سنة بدون حركات

---

### F.7 — Auto-PO from Reorder Rules (أمر شراء تلقائي من قواعد إعادة الطلب)
**المدة:** 3 أيام
**الأولوية:** 🔴 Critical
**المرجع:** Extension للـ Reorder Alert الموجود
**يعتمد على:** Reorder Rules (شغال — alert only)

**الوصف:** لما المخزون ينزل تحت الـ Minimum Quantity، بدل ما يطلع alert بس، النظام يعمل Draft Purchase Order تلقائي بالكمية المطلوبة (حتى الـ Maximum Quantity).

**التفاصيل اليومية:**

| اليوم | المهمة | التفاصيل |
|-------|--------|----------|
| Day 8 | Config | إضافة `AutoCreatePO` flag في ReorderRule. إضافة `PreferredVendor` field في Product (لو مش موجود). إضافة `MaxQuantity` field |
| Day 9 | Engine | Hangfire Job (أو extension للـ existing check): لكل rule فيها AutoCreatePO=true → لو CurrentStock < MinQty → Create Draft PO بكمية = MaxQty - CurrentStock. تجميع items لنفس المورد في PO واحد |
| Day 10 | UI + Tests | عرض الـ Auto-PO في شاشة Reorder Rules + notification للمستخدم "تم إنشاء أمر شراء مسودة" + Unit tests |

**Acceptance Criteria:**
- [ ] لما المخزون ينزل تحت الحد → Draft PO يتعمل تلقائي
- [ ] الكمية = MaxQty - CurrentStock
- [ ] أصناف متعددة لنفس المورد → PO واحد
- [ ] الـ PO يفضل Draft (مش confirmed) — المستخدم لازم يوافق
- [ ] Notification للمستخدم
- [ ] Unit tests: 5+ scenarios

---

## §4. Sprint 2 — "Enterprise-Ready" (21 يوم)

### F.5 — Landed Cost (تحميل تكاليف الشحن والجمارك)
**المدة:** 10 أيام
**الأولوية:** 🟡 High
**المرجع:** كورس أودو المحترف — Video 66
**يعتمد على:** Purchase Cycle (شغال)

**الوصف:** عند استيراد بضاعة، تكاليف الشحن والجمارك والتأمين لازم تتحمّل على تكلفة الأصناف المستوردة (مش تتسجل كمصروف). التوزيع يكون بالقيمة أو بالوزن أو بالكمية.

**التفاصيل اليومية:**

| اليوم | المهمة | التفاصيل |
|-------|--------|----------|
| Day 11-12 | Schema | Entity: `LandedCost` (Id, Date, SourceDocument, Status). Entity: `LandedCostLine` (CostType, Amount, SplitMethod: ByValue/ByWeight/ByQuantity). Entity: `LandedCostAllocation` (ProductId, OriginalCost, AdditionalCost, FinalCost) |
| Day 13-14 | Allocation Engine | حساب التوزيع: ByValue = (ItemValue / TotalValue) × CostAmount. ByWeight = (ItemWeight / TotalWeight) × CostAmount. ByQuantity = (ItemQty / TotalQty) × CostAmount |
| Day 15-16 | Journal Entries | قيد تحميل التكلفة: Dr. المخزون (الفرق) + Cr. حساب التكلفة الإضافية. تحديث Unit Cost في Stock Valuation |
| Day 17-18 | UI | شاشة Landed Cost: اختيار GRN → إضافة التكاليف → اختيار طريقة التوزيع → Preview → Validate |
| Day 19-20 | Reports + Tests | تقرير تكلفة الاستيراد + تأثير على Stock Valuation Report + Unit tests |

**القيد المحاسبي (مثال):**
```
فاتورة الشحن:
Dr. تكاليف استيراد معلقة          5,000
    Cr. المورد (شركة الشحن)              5,000

تحميل على المخزون:
Dr. المخزون (صنف أ)               3,000
Dr. المخزون (صنف ب)               2,000
    Cr. تكاليف استيراد معلقة             5,000
```

**Acceptance Criteria:**
- [ ] إنشاء Landed Cost مرتبط بـ GRN
- [ ] 3 طرق توزيع (قيمة، وزن، كمية)
- [ ] القيد المحاسبي يتولد تلقائي
- [ ] Unit Cost يتحدث في Stock Valuation
- [ ] تقرير تكلفة الاستيراد
- [ ] Unit tests: 10+ scenarios

---

### F.6 — Multi-dimensional Cost Center Allocation (توزيع متعدد الأبعاد)
**المدة:** 6 أيام
**الأولوية:** 🟡 High
**المرجع:** كورس أودو المحترف — Videos 120-124
**يعتمد على:** Cost Centers (شغال — basic tagging)

**الوصف:** حالياً الـ Cost Center هو tag واحد على الـ Journal Entry Line. المطلوب: توزيع تلقائي لمصروف واحد على أكثر من مركز تكلفة بنسب محددة (مثلاً: إيجار المبنى → 40% إدارة + 30% مبيعات + 30% إنتاج).

**التفاصيل اليومية:**

| اليوم | المهمة | التفاصيل |
|-------|--------|----------|
| Day 21-22 | Schema | Entity: `AllocationTemplate` (Name, Lines[]). Entity: `AllocationTemplateLine` (CostCenterId, Percentage). Validation: مجموع النسب = 100% |
| Day 23-24 | Engine | عند ترحيل قيد بـ AllocationTemplate: تقسيم السطر الواحد إلى أسطر متعددة (سطر لكل مركز تكلفة بنسبته). دعم Allocation على مستوى الحساب (Auto-allocate) |
| Day 25-26 | UI + Reports + Tests | شاشة إدارة Templates + تقرير مراكز التكلفة المقارن (فعلي vs ميزانية) + Pivot table + Unit tests |

**Acceptance Criteria:**
- [ ] إنشاء Allocation Template بنسب
- [ ] تطبيق Template على قيد → تقسيم تلقائي
- [ ] Auto-allocate على مستوى الحساب
- [ ] تقرير مراكز التكلفة (فعلي vs ميزانية)
- [ ] Unit tests: 5+ scenarios

---

### F.3 — Unrealized FX Revaluation (إعادة تقييم العملات غير المحققة)
**المدة:** 5 أيام
**الأولوية:** 🟡 Medium
**المرجع:** Extension لـ F.2
**يعتمد على:** F.2 (Realized FX)

**الوصف:** في نهاية كل شهر، الأرصدة المفتوحة بالعملة الأجنبية (عملاء/موردين/بنوك) لازم تتقيّم بسعر الصرف الحالي. الفرق يتسجل كربح/خسارة غير محققة (وينعكس أول الشهر الجديد).

**التفاصيل اليومية:**

| اليوم | المهمة | التفاصيل |
|-------|--------|----------|
| Day 27 | Exchange Rate Table | شاشة أسعار الصرف (Currency, Rate, Date). Import من البنك المركزي (manual أو API لاحقاً) |
| Day 28-29 | Revaluation Engine | Wizard: اختيار التاريخ → جلب كل الأرصدة المفتوحة بعملات أجنبية → حساب الفرق بين القيمة الدفترية والقيمة بسعر اليوم → توليد قيد إعادة التقييم |
| Day 30 | Reversal | أول يوم في الشهر الجديد → عكس قيد إعادة التقييم تلقائياً (Reversal Entry) |
| Day 31 | UI + Tests | شاشة Revaluation History + تقرير أرباح/خسائر غير محققة + Unit tests |

**القيد المحاسبي (مثال — خسارة غير محققة):**
```
31/05 - إعادة التقييم:
Dr. خسائر فروق عملة غير محققة     3,000
    Cr. عملاء (تعديل تقييم)                3,000

01/06 - العكس التلقائي:
Dr. عملاء (تعديل تقييم)             3,000
    Cr. خسائر فروق عملة غير محققة          3,000
```

**Acceptance Criteria:**
- [ ] جدول أسعار صرف بالتاريخ
- [ ] Wizard إعادة التقييم → قيد تلقائي
- [ ] عكس القيد أول الشهر الجديد
- [ ] تقرير أرباح/خسائر غير محققة
- [ ] Unit tests: 5+ scenarios

---

## §5. Sprint 3 — "Full Coverage" (7 أيام)

### F.1 — Deferred Revenue (الإيرادات المؤجلة)
**المدة:** 2 أيام
**الأولوية:** 🟢 Low
**المرجع:** كورس أودو المحترف — Video 148 (جزء من Assets section)
**يعتمد على:** لا شيء

**الوصف:** لما تقبض إيراد مقدماً (اشتراك سنوي مثلاً)، المبلغ يتسجل كالتزام (إيراد مؤجل) ويتحول لإيراد فعلي شهرياً بالتساوي.

**التفاصيل اليومية:**

| اليوم | المهمة | التفاصيل |
|-------|--------|----------|
| Day 32 | Schema + Engine | Entity: `DeferredRevenue` (Amount, StartDate, EndDate, MonthlyAmount, Status). Hangfire Job: أول كل شهر → توليد قيد الاعتراف بالإيراد |
| Day 33 | UI + Tests | شاشة الإيرادات المؤجلة + جدول الاستحقاق + ربط بالفاتورة + Unit tests |

**القيد المحاسبي (مثال — اشتراك 12,000 ج.م سنوي):**
```
عند القبض:
Dr. البنك                          12,000
    Cr. إيرادات مؤجلة                     12,000

كل شهر:
Dr. إيرادات مؤجلة                   1,000
    Cr. إيرادات اشتراكات                   1,000
```

**Acceptance Criteria:**
- [ ] إنشاء Deferred Revenue من فاتورة أو يدوياً
- [ ] جدول استحقاق شهري
- [ ] قيد شهري تلقائي (Hangfire)
- [ ] Unit tests: 3+ scenarios

---

### F.4 — Fiscal Position (الموقف الضريبي)
**المدة:** 5 أيام
**الأولوية:** 🟢 Low
**المرجع:** Odoo Fiscal Positions concept
**يعتمد على:** Tax Engine (شغال)

**الوصف:** حسب موقع العميل أو نوع النشاط، الضرائب تتغير تلقائياً. مثلاً: عميل في منطقة حرة = 0% VAT. عميل تصدير = 0% VAT. عميل محلي = 14% VAT.

**التفاصيل اليومية:**

| اليوم | المهمة | التفاصيل |
|-------|--------|----------|
| Day 34-35 | Schema | Entity: `FiscalPosition` (Name, AutoApply, Country, VatRequired). Entity: `FiscalPositionTaxMap` (SourceTax → DestinationTax). Entity: `FiscalPositionAccountMap` (SourceAccount → DestinationAccount) |
| Day 36-37 | Engine | عند إنشاء فاتورة: لو العميل/المورد عنده FiscalPosition → استبدال الضرائب والحسابات تلقائياً. Auto-detect من العنوان (لو AutoApply=true) |
| Day 38 | UI + Tests | شاشة إدارة Fiscal Positions + ربط بالعميل/المورد + Unit tests |

**أمثلة Fiscal Positions:**

| الاسم | الشرط | التأثير |
|-------|-------|---------|
| تصدير | Country ≠ Egypt | VAT 14% → 0% |
| منطقة حرة | Zone = FreeZone | VAT 14% → 0%, WHT → 0% |
| جهة حكومية | CustomerType = Government | WHT مختلف |

**Acceptance Criteria:**
- [ ] إنشاء Fiscal Position بـ Tax Mapping
- [ ] ربط بالعميل → تطبيق تلقائي على الفواتير
- [ ] Auto-apply بناءً على العنوان
- [ ] Account Mapping (اختياري)
- [ ] Unit tests: 5+ scenarios

---

## §6. ملخص الـ Dependencies

```
F.7 (Auto-PO) ───────────────────── standalone
F.8 (Year-end) ──────────────────── standalone (extends Closing Cockpit)
F.2 (Realized FX) ───────────────── standalone
    └── F.3 (Unrealized FX) ──────── depends on F.2
F.5 (Landed Cost) ───────────────── standalone
F.6 (Multi-dim Cost Centers) ────── standalone (extends existing)
F.1 (Deferred Revenue) ──────────── standalone
F.4 (Fiscal Position) ───────────── standalone
```

**الترتيب الإجباري الوحيد:** F.2 قبل F.3. الباقي كله ممكن يتعمل بأي ترتيب.

---

## §7. Definition of Done (لكل Feature)

- [ ] Schema/Entity changes pushed (`dotnet ef migrations add`)
- [ ] Business logic implemented + edge cases handled
- [ ] Journal entries generated correctly
- [ ] UI screen functional (CRUD + validation)
- [ ] Unit tests passing (minimum 5 per feature)
- [ ] Integration test with existing features (no regressions)
- [ ] Arabic labels/translations added
- [ ] Code reviewed + committed

---

## §8. المخاطر والتخفيف

| المخاطر | الاحتمال | التأثير | التخفيف |
|---------|---------|---------|---------|
| F.5 Landed Cost أعقد من المتوقع | متوسط | +3 أيام | ابدأ بـ ByValue فقط، أضف ByWeight/ByQuantity لاحقاً |
| F.3 Unrealized FX → Exchange Rate API مش متاح | منخفض | +1 يوم | ابدأ manual entry، أضف API لاحقاً |
| F.6 Multi-dim → performance مع قيود كتير | منخفض | +1 يوم | Batch processing + caching |
| Sprint 1 يتأخر | منخفض | تأخر البيع | F.7 ممكن تتأجل — F.2 + F.8 في الأول |

---

## §9. Milestones والقرارات

| Milestone | اليوم | القرار |
|-----------|-------|--------|
| **Sprint 1 Done** | Day 10 | ✓ ابدأ بيع + onboard أول عميل |
| **أول عميل يدفع** | Day 10-20 | لو في feedback → أولوية على Sprint 2 |
| **Sprint 2 Done** | Day 31 | ✓ Target شركات أكبر (مستوردين) |
| **Sprint 3 Done** | Day 38 | ✓ Full Odoo Accounting parity |

---

## §10. ملخص الـ Effort

| Sprint | Features | أيام | النتيجة |
|--------|----------|------|---------|
| Sprint 1 | F.2 + F.8 + F.7 | 10 | 88% parity → جاهز للبيع |
| Sprint 2 | F.5 + F.6 + F.3 | 21 | 93% parity → enterprise-ready |
| Sprint 3 | F.1 + F.4 | 7 | 95%+ parity → full coverage |
| **المجموع** | **8 features** | **38** | **Odoo Accounting parity** |

---

## §11. بعد Phase F — إلى الجاي؟

| الأولوية | الموضوع | ليه |
|----------|---------|-----|
| 1 | **أول 5 عملاء** | Validation > Features |
| 2 | **Phase D (ETA Integration)** | الـ killer feature → بيميّزك عن Odoo |
| 3 | **Phase E Remainder** | 6 features متبقية |
| 4 | **Mobile App** | للمحاسبين on-the-go |
| 5 | **HR/Payroll** | لو العملاء طلبوا |
