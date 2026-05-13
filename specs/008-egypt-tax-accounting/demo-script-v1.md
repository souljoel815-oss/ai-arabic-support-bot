# DaftarX — 3-Minute Demo Script (v1)

> Per [post-mvp-roadmap-v3.md](post-mvp-roadmap-v3.md) §D1.
>
> Audience: Egyptian SMB owner, accountant, or sales manager who's
> never seen DaftarX. Goal: by 3:00, they understand what we sell
> and want to try the trial.
>
> Recording stack: OBS Studio + a quiet room + the seeded demo company
> (extend the seed CLI per D0.5 to load realistic Egyptian data:
> "Al-Nour Foods Co" + 5 customers + 20 items + 30 sample invoices
> spanning 3 months + 1 sales rep ("أحمد محمود") with a few visits +
> orders).
>
> Narration: **Egyptian Arabic** (لهجة مصرية، مش فصحى) — natural,
> conversational, NOT an ad voiceover. Pace: ~140 words/min Arabic.
>
> Resolution: 1920×1080. Browser zoom 110% so text is readable on
> mobile playback. No music, no stock loops, no transitions other
> than hard cuts.

---

## Act 1 (0:00 – 0:30) — The Hook (Penalty Shield)

**Goal:** in 30 seconds, the viewer knows what specific Egyptian
problem we solve.

| Time | Screen | Narration (Arabic) |
|------|--------|--------------------|
| 0:00 | Penalty Shield page (`/penalty-shield`) — Tier 3 alert showing 5,000 EGP exposure | "تعرف إن قرار 281 لسنة 2025 ممكن يكلفك 5 آلاف جنيه على الفاتورة الواحدة لو اتأخرت في إرسالها لمصلحة الضرائب؟" |
| 0:08 | Cursor hovers the "5,000 EGP" number | "DaftarX بيحسبلك الغرامة قبل ما تحصل، ويوريك بالظبط الفاتورة دي اللي ممكن تخسرك فلوس." |
| 0:15 | Click "Open work queue" → list of risky invoices | "والأهم — بيقولك إيه اللي تعمله النهارده عشان تتجنبها." |
| 0:22 | Cursor lands on the topmost invoice in the queue | "ده اللي مفيش حد تاني في السوق المصري بيعمله." |

**Key on-screen text overlay (subtle, bottom-right):**
"درع الغرامات — Penalty Shield"

---

## Act 2 (0:30 – 1:30) — The Boring Stuff Done Right

**Goal:** prove we handle ETA + audit + the actual tax compliance
piece, not just the marketing.

| Time | Screen | Narration |
|------|--------|------------|
| 0:30 | Hard cut to invoice editor (`/invoices/new`) | "خد سيناريو عادي — هتعمل فاتورة لعميل." |
| 0:36 | Quick fill: pick "متجر التقنية" customer, add 2× ITM-001 (Laptop) | "تختار العميل، تضيف الصنف، الكمية، السعر." |
| 0:48 | Click "Post" → invoice INV-2026-000007 appears with ETA Submitted badge | "وبضغطة زرار: الفاتورة اترحَّلت، اتسجلت في القيود، واتبعتت لمصلحة الضرائب — في ثانية واحدة." |
| 0:58 | Click "Send by email" button | "تبعتها للعميل بالبريد..." |
| 1:02 | Toast: "Sent invoice INV-2026-000007 to customer@…" | "...وصلتله مع الـ PDF فعلاً." |
| 1:08 | Click "Quick WhatsApp" → wa.me opens in new tab with pre-filled message | "أو على واتساب — أسرع عند الزباين المصريين." |
| 1:16 | Hard cut to `/inspection-bundle` page | "ولو جالك مأمور الضرائب بكرة..." |
| 1:20 | Date range = current month, click "Generate bundle" | "...بضغطة واحدة بتجمع له كل اللي محتاجه." |
| 1:26 | Toast shows "Generated bundle: 2.4 MB" | "كل الفواتير، كل الإيصالات، كل قيود الـ JE — في ملف واحد." |

**Key on-screen text overlay:**
"كل خطوة محسوبة قانونياً — ETA + قانون 6/2025 + قرار 281/2025"

---

## Act 3 (1:30 – 2:30) — The Sales-Rep Story

**Goal:** show we have a story for SALES that other generic accounting
software doesn't ship.

| Time | Screen | Narration |
|------|--------|------------|
| 1:30 | Hard cut to mobile-viewport (Chrome devtools mobile preview iPhone 12) showing rep login | "مندوبك في الشارع — محتاج موبايل، مش لابتوب." |
| 1:36 | Login as `rep1@local` → lands on `/dashboards/me` | "بيدخل على لوحته الشخصية، ويلاقي زياراته اليوم..." |
| 1:42 | Cursor lands on "Today's visits = 3" KPI card | "...عميل بيت الخير + الفاسد + الترويج، بالترتيب." |
| 1:48 | Click into `/routes` → tap "Visited" on first row | "بيمشي على الزبون، يضغط 'تمت'، الزيارة بتتسجل بالتوقيت." |
| 1:55 | Hard cut to `/sales-orders/new` → fill 5 laptops | "العميل طلب بضاعة — المندوب يعمل أمر بيع من الموبايل." |
| 2:04 | Click "Confirm order" → SO-2026-000003 confirmed | "بيرجع المكتب يلاقي الأمر جاهز." |
| 2:10 | Switch to admin browser tab → `/sales-orders` → click "→ Invoice" | "المحاسب بضغطة زرار يحوّله لفاتورة..." |
| 2:16 | New invoice draft opens, click "Post" → ERROR: "above credit limit of 1,000 EGP" | "...بس النظام بيوقفه: 'العميل وصل لحد ائتمانه'." |
| 2:24 | Camera follows the error message word-by-word | "حمايتك قبل ما تخسر فلوس. ده مش بيحصل في QuickBooks ولا حتى في Odoo." |

**Key on-screen text overlay:**
"خط سير + أوامر بيع + حد ائتمان — كله مربوط مع بعض"

---

## Act 4 (2:30 – 3:00) — The Close

**Goal:** position vs Odoo + drop the trial CTA.

| Time | Screen | Narration |
|------|--------|------------|
| 2:30 | Hard cut showing the full DaftarX sidebar zoomed out | "DaftarX عنده 7 أقسام في القائمة الجانبية." |
| 2:37 | Quick split-screen: DaftarX sidebar on left, Odoo's 82-app store screenshot on right | "Odoo عنده 82 موديول. أنت محتاج 7." |
| 2:44 | Cursor lands on the price card on the landing page (mocked overlay) showing "3,500 ج.م./سنة" | "بحوالي عُشر سعر Odoo Egypt — وبتشغل من غير IT ولا تنصيب معقد." |
| 2:52 | URL bar zooms to "daftarx.com/trial" with "Try free for 14 days" CTA | "جرّبه ١٤ يوم مجاناً. لو مكسبتش وقت في أول أسبوع، أنا اللي بدفعلك." |
| 2:58 | DX logo + "DaftarX" wordmark fades in center | (silence) |

**Key on-screen text overlay (full-screen, last second):**
"daftarx.com — جرّب 14 يوم مجاناً"

---

## Production checklist

- [ ] Seed CLI extended to populate the demo company, 5 customers,
      20 items, 30 invoices spanning 3 months, 1 sales rep with
      visits + orders. Idempotent — `dotnet run --project
      src/EgyptTax.Web -- seed --demo` (D0.5 in roadmap).
- [ ] Penalty Shield seeded with at least one Tier-3 (5,000 EGP)
      exposure scenario so Act 1 has a real number to point at.
- [ ] Browser zoomed to 110%; OBS recording at 1080p60.
- [ ] Mobile viewport simulator pre-set to iPhone 12 (390×844).
- [ ] Two browser windows pre-arranged: admin in left,
      mobile-rep in right.
- [ ] Mic test before the take. No keyboard noise. No fan in shot.
- [ ] Take 3 cuts of each Act, pick the best per Act, edit
      together. Total runtime should land at 2:55 — 3:05.
- [ ] Export 1080p MP4 + a 720p version for slow connections.
- [ ] Upload to YouTube + embed on landing page (D2).
- [ ] Arabic auto-captions enabled on YouTube + manually corrected.
- [ ] English subtitles published as a second track.

---

## Variants to record once v1 is shipped

These are 30-second feature-spotlight videos for landing-page
sub-pages. Each reuses the same recording setup but focuses on one
feature in isolation — easier to A/B test which actually converts.

1. **Penalty Shield deep-dive** — 30s on قرار 281 + the work queue
2. **Inspection Bundle** — 30s of "auditor visit" framing
3. **Sales-rep mobile** — 30s pure mobile screen-recording
4. **Customer Statement + Credit Limit** — 30s for accounting offices
5. **Recurring invoices** — 30s for service businesses (post L3)
