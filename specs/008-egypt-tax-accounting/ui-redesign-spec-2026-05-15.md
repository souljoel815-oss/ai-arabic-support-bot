# DaftarX — UI Redesign Spec (Manus drop, 2026-05-15)

**Source:** Manus AI, "خطة تصميم واجهة المستخدم الكاملة" — the long-form spec dropped on 2026-05-15 alongside 5 visual mockups (Journal Entry editor, Command Palette open, Invoice detail, Invoice list, Dashboard).

**Implementation path chosen:** Adopt the design intent in the existing **Blazor Server** stack (NOT the React/TypeScript stack the source spec proposed). The IA, screen layouts, visual design, and UX patterns transfer cleanly to `.razor` components — the file paths the spec suggests as `client/src/components/layout/ModuleSidebar.tsx` map to `src/EgyptTax.Web/Shared/AppShell/ModuleSidebar.razor` etc.

---

## The big idea: 3-tier "Mission Control" navigation

Replace the current single-level sidebar (`MainLayout.razor`, ~272px wide, ~30 flat nav items) with three layers:

1. **Module Sidebar (64px)** — always-visible icon strip, 7 module icons (Dashboard / Sales / Purchases / Inventory / Accounting / Contacts / Settings), gold active-state circle
2. **Sub-Nav Panel (240px)** — context-sensitive; shows the sub-pages of the active module, grouped into named sections (e.g. Accounting → Tax / Closing / Reports)
3. **Command Palette (Ctrl+K)** — universal fuzzy search across screens, customers, invoices, items. Already shipped via `Shared/CmdPalette/CommandPalette.razor` (DP.3); polish + extend in this rebuild.

**Why better than current MainLayout:**
- Cognitive load: 7 icons vs. 30 flat links
- Context matters: when you're in Sales, you don't need to see Accounting's 18 sub-pages
- Power users live in the palette; the sidebar is the visual anchor
- No scroll in the nav (previous failure: sidebar was too long)

---

## The 7 modules + sub-nav structure

### 1. الرئيسية (Dashboard)
- No sub-nav (single-screen module)
- Layout: 4 KPI cards / action items panel / 2 charts (Revenue vs Expenses bar + Cash Flow area)

### 2. المبيعات (Sales)
- عروض الأسعار (`/sales/quotations`)
- أوامر البيع (`/sales/orders`)
- الفواتير (`/sales/invoices`)
- إشعارات دائنة (`/sales/credit-notes`)
- الدفعات المقدمة (`/sales/advances`) ← **needs Phase E.1**
- المتابعات (`/sales/follow-ups`)
- — separator —
- تقارير: تقرير المبيعات / أعمار الديون / كشف حساب عميل

### 3. المشتريات (Purchases)
- أوامر الشراء (`/purchases/orders`)
- فواتير الموردين (`/purchases/bills`)
- إشعارات دائنة (`/purchases/credit-notes`) ← **needs Phase E.3**
- مصروفات (`/purchases/expenses`)
- — separator —
- تقارير: تقرير المشتريات / أعمار الدائنين / كشف حساب مورد

### 4. المخزون (Inventory)
- الأصناف (`/inventory/products`)
- حركات المخزون (`/inventory/moves`)
- التحويلات (`/inventory/transfers`)
- الإتلاف / الفاقد (`/inventory/scrap`) ← **needs Phase E.4**
- جرد المخزون (`/inventory/count`)
- — separator —
- تقارير: رصيد المخزون / تقادم المخزون / تقييم المخزون

### 5. المحاسبة (Accounting)
- قيود اليومية (`/accounting/journal-entries`)
- شجرة الحسابات (`/accounting/chart-of-accounts`)
- المطابقة البنكية (`/accounting/bank-reconciliation`)
- تحويل أموال (`/accounting/fund-transfer`) ← **needs Phase E.5**
- — separator —
- الضرائب: ضريبة القيمة المضافة / خصم من المنبع / ضريبة الدخل / تقويم الالتزامات
- — separator —
- الإقفال: قيود التسوية / كشكول الإقفال / قفل الفترة
- — separator —
- التقارير: ميزان المراجعة / دفتر الأستاذ / قائمة الدخل / الميزانية العمومية / التدفقات النقدية / درع الغرامات

### 6. العملاء (Contacts)
- العملاء (`/contacts/customers`)
- الموردين (`/contacts/vendors`)
- — separator —
- CRM: الفرص (`/contacts/crm/pipeline`) / الأنشطة (`/contacts/crm/activities`)

### 7. الإعدادات (Settings)
- بيانات الشركة / المستخدمين / شروط الدفع / الضرائب / اليوميات / التسلسلات
- الاستيراد والتصدير: استيراد بيانات
- ETA: إعدادات / أكواد الأصناف / سجل الإرسال
- النسخ الاحتياطي / الترخيص

---

## Visual design tokens

The mockups confirm the existing **Leil dark theme** (`[data-theme="leil"]` in `site.css`) is on the right track. Keep the existing token names, refine values to match mockups:

| Use | Value | Token |
|---|---|---|
| Page background | very dark navy (#0a0f1c-ish) | `--bg` |
| Card background | slightly lighter navy | `--bg-2` |
| Primary / accent | gold | `--accent-gold` (NEW token) |
| Active nav state | gold circle bg + gold text | `--sidebar-active-bg` (NEW) |
| Body text | cream/off-white | `--text` |
| Muted text | cream-bluish-gray | `--text-muted` |
| Success badge | green | `--success` (kept) |
| Danger badge | red | `--danger` (kept) |

Status badges (per mockups): **مرحل** (green), **مسودة** (gray), **مرفوضة** (red), **مقبولة** (green), **لم ترسل** (gray).

---

## Sprint plan (Blazor adaptation)

The Manus spec proposes 7 sprints; mapped to Blazor effort:

| Sprint | Deliverable | Effort |
|---|---|---|
| 1 | `AppShellLayout.razor` + `ModuleSidebar.razor` + `SubNavPanel.razor` + Dashboard rebuild matching mockup #5 | ~1 week |
| 2 | Migrate Sales pages to AppShell — Quotations, Orders, Invoices, Credit Notes (list + detail patterns set the template for everything else) | ~2 weeks |
| 3 | Migrate Purchases pages — POs, Bills, Credit Notes, Expenses | ~1.5 weeks |
| 4 | Migrate Inventory pages — Products, Moves, Transfers, Scrap, Count | ~1 week |
| 5 | Migrate Accounting pages — JEs, COA, Bank Recon, Fund Transfer + every report | ~2 weeks |
| 6 | Migrate Contacts + CRM Pipeline (Kanban) | ~1 week |
| 7 | Migrate Settings + polish (mobile responsive, empty states, loading states) | ~1 week |

**Total: ~9.5 weeks for full UI rebuild.** Sprint 1 is the high-risk, high-information sprint — once that lands cleanly the rest is mechanical migration.

---

## Critical: where Manus got the stack wrong

The source spec proposes `client/src/components/layout/ModuleSidebar.tsx` — React/TypeScript/Vite/shadcn/ui/tRPC. That's a **complete rewrite** of the existing Blazor Server app (~145 `.razor` pages, all the EF Core wiring, the auth gate, the Hangfire jobs).

**Decision (operator, 2026-05-15):** Adopt the design intent in Blazor. The visual design + IA + UX patterns transfer 1:1 to `.razor` components. Never starting from scratch on the business logic.

When the spec says "use shadcn `Command` component" — we use the existing `CommandPalette.razor`. When it says "shadcn `Table`" — we use the existing `<table class="data-grid">`. When it says "shadcn `Dialog`" — we use the existing modal patterns.

The tech-stack mismatch is the single biggest difference between this spec and what gets shipped. Document it here so future readers don't try to apply the spec literally.

---

## Sprint 1 mockup inventory (operator-supplied, 2026-05-15)

Five PNG mockups attached in chat as visual reference for Sprint 1:

1. **Journal Entry editor** — Accounting module active, JE-2026-0158, draft state, 2 line items, balance check pass at the bottom, "حفظ كمسودة" + "ترحيل" actions
2. **Command Palette open** — Dashboard background dimmed, search "فات" matching فاتورة actions + 2 invoice rows
3. **Sales Invoice detail** — INV-2026-0042 posted, customer block, payment terms block, 3-line items table, totals card, paid/remaining bar
4. **Sales Invoice list** — 8 rows visible, filter bar (status / date / customer / ETA status), bulk-select column, pagination
5. **Dashboard (الرئيسية)** — 4 KPI cards (compliance / net profit / overdue / total sales), action items panel (4 items with one-click action button), 2 charts (revenue vs expenses bar + cash flow area)

Re-attach in future sessions if needed.

---

## 🔒 LOCKED — Sidebar architecture decision (operator, 2026-05-15 evening)

**The 2-tier "Mission Control" sidebar pattern is the FINAL choice and must NOT be reverted to a single-tier collapsible sidebar.**

Layout:
- **`ModuleSidebar`** (248px on the inline-start edge) — fixed-width column of 7 module icons + Arabic labels (Dashboard / Sales / Purchases / Inventory / Accounting / Contacts / Settings). Logo + footer (search / theme toggle / logout) live in this column.
- **`SubNavPanel`** (280px, sits next to the module sidebar) — context-sensitive list of sub-pages for the currently active module. Renders only when the active module's `HasSubNav` is `true` (Dashboard doesn't).
- **Main content area** — flex fills the rest, with `--dx-sidebar-w` + `--dx-subnav-w` reserved.

Files (do not merge / collapse into one sidebar):
- `src/EgyptTax.Web/Shared/AppShell/ModuleSidebar.razor`
- `src/EgyptTax.Web/Shared/AppShell/SubNavPanel.razor`
- `src/EgyptTax.Web/Shared/AppShell/AppShellLayout.razor` — orchestrates both via `AppShellModuleRegistry.ResolveActive(path)`.

**Why locked:** an attempted "match the Manus single-sidebar mockup" merge during the 2026-05-15 evening pass produced a sidebar-inside-sidebar bug and visually regressed to a legacy MainLayout feel. The operator explicitly rejected the merge twice in the same session ("ليه رجعت القوائم بالشكل القديم"، "اثبت علي نظام القوائم دا"). The single-sidebar variant in the Manus PNG is a mockup-only suggestion — the implemented 2-tier pattern wins because:
- The icon strip keeps every module reachable in one click.
- The sub-nav panel scales to modules with many links (Accounting has ~14) without nested scroll.
- It survives long navigation labels without truncation.

**If a future Manus pass proposes a single sidebar again:** ignore for the sidebar component specifically; cherry-pick visual ideas (logo, colors, ETA card placement) only.
