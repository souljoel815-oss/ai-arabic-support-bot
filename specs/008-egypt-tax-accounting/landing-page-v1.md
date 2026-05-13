# DaftarX Landing Page (v1) — Copy + Structure

> Per [post-mvp-roadmap-v3.md](post-mvp-roadmap-v3.md) §D2.
>
> Single page, RTL-default, Arabic-primary with English secondary.
> Deploys as static HTML on Cloudflare Pages once a domain is
> registered. Writeable in Framer / Webflow if you want a
> drag-and-drop editor — copy + IA below is the contract.
>
> The accompanying static mockup is at
> `specs/008-egypt-tax-accounting/landing-page.html` — you can open
> it in a browser today to validate the visual + copy before
> committing to a CMS.

---

## Page goals

1. **Convince in 30 seconds**: Egyptian buyer arrives → sees Penalty
   Shield + Inspection Bundle + sales-rep workflow → understands
   we're not generic accounting software.
2. **Drop the trial CTA**: "Try free for 14 days" → downloads the
   .exe + portable .db. Zero IT involvement to start.
3. **Pre-empt Odoo objections**: FAQ section answers "but Odoo has
   X". Doesn't pretend Odoo is bad — frames us as a different
   segment.
4. **Give a price upfront**: Egyptian buyers hate "Contact us for
   pricing". Show the four tiers (Solo / SMB / Enterprise / Firm)
   with annual prices in EGP.

---

## Information architecture (top to bottom)

| # | Section | One-line purpose |
|---|---------|--------------------|
| 1 | **Hero** | Tagline + 3-min demo embed + trial CTA |
| 2 | **3 differentiators** | Penalty Shield + Inspection Bundle + Sales-rep workflow with screenshot for each |
| 3 | **Who is this for?** | 3 customer-segment cards — accountant office / shop with reps / service business |
| 4 | **How DaftarX compares to Odoo Egypt** | Honest table — where Odoo wins, where we win, who picks which |
| 5 | **Pricing** | 4 cards (Solo / SMB / Enterprise / Firm) with annual EGP prices |
| 6 | **FAQ** | The 8 objections from competitive analysis, answered honestly |
| 7 | **Footer** | About / contact / privacy / GitHub link |

---

## Copy (Arabic primary, English secondary in italics)

### 1 — Hero

**Tagline (60pt, navy, bold):**
> محاسبتك المصرية كاملة في برنامج واحد — بدون اشتراك شهري

*Subtagline (24pt, muted):*
> Your complete Egyptian accounting in one .exe — no monthly subscriptions.

**Body line (16pt):**
ETA متوافق · ضرائب 281/2025 محسوبة · مندوبين مع خط سير · بسعر سنوي يبدأ من 3,500 جنيه.

**Two CTAs:**
- Primary (navy bg): **جرّب 14 يوم مجاناً** *(Try free for 14 days)* → `/download`
- Secondary (outlined): **شاهد العرض التجريبي (3 دقائق)** *(Watch 3-min demo)* → opens YouTube embed inline

**Hero visual:**
3-minute YouTube embed (the demo from D1), set to autoplay muted on
desktop, click-to-play on mobile.

**Trust strip beneath the hero (small, gray):**
> مبني خصيصاً للسوق المصري · يعمل أوفلاين · بيانات مخزّنة على جهازك

---

### 2 — 3 Differentiators

Section header:
> **اللي بنعمله ومحدش غيرنا بيعمله**
> *Three things only DaftarX does for Egyptian SMBs*

Three equal-width cards, each:

**Card 1 — Penalty Shield**
- Icon: 🛡️ (shield)
- Title: **درع الغرامات** *(Penalty Shield)*
- Body: "بنحسب الغرامات اللي ممكن تخسرها بسبب التأخر في إرسال
  الفواتير لمصلحة الضرائب — قبل ما تحصل. وبنعرضلك بالظبط الفواتير
  اللي محتاجة تتحرك النهارده عشان تتجنبها."
- *English:* "We compute the ETA penalty exposure your business
  faces — before it materialises — and surface the exact invoices
  you must act on today to avoid Tier-3 fines under Resolution
  281/2025."
- Screenshot: `/penalty-shield` page with a 5,000 EGP exposure
  scenario.

**Card 2 — Inspection Bundle**
- Icon: 📁 (folder)
- Title: **حزمة الفحص الضريبي** *(Inspection Bundle)*
- Body: "جالك مأمور الضرائب؟ بضغطة زرار بتجمعله ZIP فيه كل الفواتير
  والإيصالات وقيود الـ JE والمرفقات لأي فترة — مع manifest موقّع
  بـ SHA-256 يثبت إن الملفات ما اتغيرتش."
- *English:* "Tax inspector visit? One click bundles every invoice,
  receipt, journal entry, and attachment for any period into a
  signed ZIP — with SHA-256 manifest proving nothing's been
  altered."
- Screenshot: `/inspection-bundle` page mid-generation.

**Card 3 — Sales-Rep Workflow**
- Icon: 🚐 (delivery van)
- Title: **خط سير المندوب + حد الائتمان** *(Sales-rep workflow + credit limit)*
- Body: "المندوب في الشارع بيخطط زياراته على الموبايل، ياخد طلبيات،
  والمكتب يحوّلها لفواتير. النظام بيمنع البيع للعميل اللي تجاوز حد
  ائتمانه — تلقائياً."
- *English:* "Reps plan visits on their phone, take orders in the
  field, the office converts them to invoices. Credit-limit
  guard auto-blocks sales to customers above their ceiling. None
  of that exists in QuickBooks Egypt or Odoo Egypt without
  custom code."
- Screenshot: `/dashboards/me` (rep self-dashboard) on a phone
  viewport.

---

### 3 — Who is this for?

Section header:
> **النظام ده مناسب لمين بالظبط؟**

Three cards, each with a customer photo placeholder + a one-liner:

**Card 1 — مكتب محاسبة صغير**
"عندك 10–50 عميل بتمسك حساباتهم. محتاج أداة واحدة تجمع كل عميل
وتعمل إقراراته ETA + الإقفال الشهري + تجهيز فحص الضرائب."

**Card 2 — محل بيع بمندوبين**
"عندك محل + فرع تخزين، 3–5 مناديب بيلفّوا على الزباين. محتاج تتبع
الزيارات + التحصيلات + حد الائتمان لكل عميل."

**Card 3 — شركة خدمات / اشتراكات**
"بتوصّل خدمة شهرية / صيانة / استشارات. محتاج فوترة دورية تتولّد
تلقائياً وكشف حساب لكل عميل."

(The third card depends on L3 shipping — until then, drop it or
replace with "Service business with monthly billing — coming soon".)

---

### 4 — How DaftarX compares to Odoo Egypt

Section header:
> **DaftarX مقابل Odoo Egypt — الحقيقة الكاملة**
> *(We're not Odoo. Here's where Odoo wins, where we win, and who picks each.)*

Two-column comparison table (mobile: collapse to vertical):

| الميزة / Feature | DaftarX | Odoo Egypt |
|------------------|---------|------------|
| الفاتورة الإلكترونية ETA | ✅ مباشر + درع الغرامات | ✅ مباشر |
| المخزون | ✅ مكان واحد (متعدد قريباً) | ✅ متعدد المستودعات + استراتيجيات |
| الموارد البشرية والرواتب | ❌ | ✅ كامل |
| التصنيع MRP | ❌ | ✅ كامل |
| المتجر الإلكتروني | ❌ | ✅ |
| CRM متكامل | ⚠️ بسيط (خط سير فقط) | ✅ Pipeline كامل |
| **خط سير المندوبين** | ✅ مدمج | ❌ يحتاج موديول إضافي |
| **درع الغرامات** | ✅ | ❌ |
| **حزمة فحص ضريبي** | ✅ | ❌ |
| **حد ائتمان مع منع البيع** | ✅ مدمج | ⚠️ يحتاج تخصيص |
| السعر السنوي (10 مستخدمين) | 8,000 ج.م | ~55,000+ ج.م + تنفيذ |
| التشغيل | .exe + ملف واحد | يحتاج Postgres + IT |
| اللغة العربية | first-class | ترجمة |

**Verdict box (highlighted):**
- **اختار Odoo لو:** عندك مصنع، 3 مستودعات، تحتاج HR ورواتب، عندك
  ميزانية 50 ألف+ جنيه للتنفيذ.
- **اختار DaftarX لو:** بتشتغل فاتورة وتحصيل وامتثال ضريبي، 1–20
  موظف، عاوز حاجة تشغل النهاردة بدون شركة تنفيذ.

---

### 5 — Pricing

Section header:
> **الأسعار واضحة. مفيش رسوم خفية.**
> *(Annual subscription. Full features at every tier — limits are on user count + multi-company only.)*

Four equal-width cards:

**Card: Solo**
- 3,500 ج.م / سنة
- مستخدم واحد
- 1 شركة
- كل المميزات الأساسية + ETA + درع الغرامات
- CTA: "ابدأ مجاناً 14 يوم"

**Card: SMB (most popular)** — highlight with navy border
- 8,000 ج.م / سنة
- 3 مستخدمين
- 1 شركة
- كل ميزات Solo + إيميل + واتساب + إدارة مناديب + كشف حساب العميل
- CTA: "ابدأ مجاناً 14 يوم"

**Card: Enterprise**
- 17,500 ج.م / سنة
- مستخدمين غير محدود
- 1 شركة
- كل ميزات SMB + بنود متقدمة + تكاملات
- CTA: "ابدأ مجاناً 14 يوم"

**Card: Firm Portal**
- 30,000 ج.م / سنة
- مستخدمين غير محدود
- شركات غير محدودة
- مكاتب المحاسبة + تبديل بين الشركات + وضع المراجعة
- CTA: "تواصل معانا"

**Footnote:**
"مفيش رسوم تنفيذ. مفيش رسوم تركيب. تنزّل الـ .exe، تشغّله، خلاص."

---

### 6 — FAQ (objections from competitive analysis)

Accordion, 8 questions:

**Q1: هل أقدر آخد بياناتي لو سبت البرنامج؟**
"أكيد. كل صفحة فيها زرار 'تصدير CSV' بيخرّجلك كل بياناتك Excel-ready.
وملف الـ SQLite بتاعك على جهازك — تقدر تاخده وتفتحه بأي أداة SQL."

**Q2: لو المحاسب بتاعي مشي، هلاقي بديل يعرف DaftarX؟**
"النظام مصمم بشكل بسيط — أي محاسب يتعلمه في يوم. مفيش شهادات
مطلوبة، ولا أكاديميات. الواجهة بالعربي والمحاسب المصري عارف أصلاً
الأكونتنج المصري — بس عاوز أداة سهلة."

**Q3: بتنافسوا Odoo إزاي وأنتم مش 12 مليون مستخدم؟**
"إحنا مش بنحاول نكون Odoo. Odoo حلّ شامل لشركة متوسطة (50+ موظف).
DaftarX حلّ مركّز للـ SMB المصرية (1–20 موظف) اللي شغّالة بالورق
أو الإكسيل دلوقتي. الشركتين مش بتاعت نفس الزبون."

**Q4: بتشتغلوا أوفلاين؟**
"الـ .exe بيشتغل على جهازك بالكامل. ده يعني إنك مش محتاج إنترنت
لتفتحه — بس محتاج إنترنت بس عند إرسال الفاتورة لـ ETA. ده مش
'كلاود-only' زي Daftra/Wafeq."

**Q5: التحديثات بتنزل إزاي؟**
"كل تحديث بيكون .exe جديد — تنزّله من حسابك في الموقع وتشغّله
مكان القديم. بيناتك في ملف الـ .db لوحده — التحديث ما بيمسهاش."

**Q6: لو احتاج ميزة مش موجودة؟**
"إحنا فريق صغير بنركّز على الميزات اللي عملاؤنا فعلاً بيطلبوها.
ابعتلنا على الإيميل اللي تحت — بنرد في 24 ساعة. لو الميزة مفيدة
لأكتر من عميل، بنشحنها في الإصدار الجاي."

**Q7: مين عملاؤكم الحاليين؟**
"إحنا في مرحلة Beta. أول 100 عميل بياخدوا خصم 50% مدى الحياة على
الاشتراك السنوي + خط دعم مباشر معايا. لو حابب تنضم، اضغط على
'تواصل معانا'."

(Note: replace this answer with real customer logos once you have
3+ paying.)

**Q8: شركتي عندها بيانات حساسة. مين بيقدر يشوفها؟**
"الـ .db موجود على جهازك أنت — مش على سيرفر تابع لينا. حتى وضع
Firm Portal للمحاسبين الخارجيين بيشتغل P2P بين الجهاز بتاع الشركة
وجهاز المحاسب، بدون الحاجة لـ cloud وسيط."

---

### 7 — Footer

Three columns:

**Column 1 — DaftarX**
- نبذة عنّا
- المدوّنة (coming soon)
- اشتراكاتنا في قانون الضرائب المصري

**Column 2 — منتج**
- المميزات
- الأسعار
- العروض التوضيحية
- ملاحظات الإصدارات

**Column 3 — تواصل**
- support@daftarx.com (placeholder)
- WhatsApp business number (placeholder)
- GitHub: github.com/souljoel815-oss/ai-arabic-support-bot

**Bottom strip (full-width, dark):**
- "© 2026 DaftarX — مبني في القاهرة"
- Privacy policy (link)
- Terms of service (link)
- "حالة الخدمة" (status page link, placeholder)

---

## Design system

**Colors (mirror the app):**
- Primary navy: `#1e3a5f`
- Primary hover navy: `#16284a`
- Primary accent (blue): `#3b82f6`
- Background: `#f6f8fb`
- Card surface: `#ffffff`
- Text: `#111`
- Muted text: `#6b7280`

**Typography:**
- Arabic: **Cairo** (already shipped in `wwwroot/fonts/`)
- English: system-ui sans-serif
- Headings: bold 700+
- Body: regular 400, line-height 1.7 for Arabic readability

**Spacing:**
- Section padding: 80px desktop, 40px mobile
- Card padding: 32px
- Element gap: 16px / 24px / 48px scale

**Direction:**
- Default `<html dir="rtl" lang="ar">` — flip to LTR if user toggles
  language switcher (preserve choice in localStorage)

---

## Deployment

1. Build static HTML+CSS from this doc (or drop into Framer / Webflow).
2. Register domain `daftarx.com` (or `daftarx.eg` if available).
3. Deploy to Cloudflare Pages — free, fast, global CDN.
4. Point `trial.daftarx.com` at the dev server (or future staging
   environment) so the "Try free for 14 days" button has somewhere
   to send people.
5. Set up basic analytics (Plausible or Cloudflare Analytics — both
   privacy-respecting, no cookie banner needed).
6. Add Open Graph + Twitter Card meta for when the link is shared on
   WhatsApp / X / LinkedIn — preview image = the hero screenshot.

---

## v2 ideas (after first 5 customers ship)

- Customer logos / testimonials section between Differentiators
  and "Who is this for?"
- Dedicated `/penalty-shield` deep-dive page (SEO target: "غرامات
  مصلحة الضرائب 2025")
- Dedicated `/eta-compliance` page (SEO target: "الفاتورة
  الإلكترونية مصر")
- Pricing page split out from landing (more room for tier
  comparisons + EGP/USD toggle for export-business prospects)
- Blog with 1 post / month on Egyptian-tax-law changes (long-tail
  SEO + signal that we're tracking the regulator)
