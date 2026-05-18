# Feature Specification: DaftarX Website + Customer Portal

**Feature Branch**: `010-website-portal`
**Created**: 2026-05-18
**Status**: Draft
**Input**: User description: "DaftarX website — vendor-hosted public marketing site PLUS authenticated customer portal at daftarx.app. Combines the public-facing surface (prospects discovering DaftarX) with the post-purchase surface (existing customers managing their licenses + downloads + subscriptions + support)."

## Clarifications

### Session 2026-05-18

- Q: How should the "Start trial" call-to-action behave end-to-end (duration, feature set, payment-method requirement, end-of-trial behaviour)? → A: 14-day full-feature trial mirroring the existing `LicenseEdition.Trial` set (Enterprise minus Firm Portal — 22 features), no payment method required at signup, at day 14 the install auto-pauses with a single-tap "subscribe to keep going" prompt that converts the prospect into a paid Subscription without re-entering anything except payment method.
- Q: How does a customer go from "signed up on the portal" to "running DaftarX activated on their machine" (activation flow, placeholder HWID vs activation code vs manual token download)? → A: Trial requires NO portal interaction at all — the prospect downloads the installer from the marketing surface, runs it, and the existing on-prem product's `LicenseStatus.RecordTrial` auto-grants the 14-day trial client-side. Paid conversion uses the existing manual install-first flow: customer reads their hardware id from the install screen, pastes it into the portal's "Activate paid licence" form, the portal signs a token via the existing `Issue-License.ps1` flow, customer downloads the `.token` file and drops it at `%PROGRAMDATA%\DaftarX\license\license.token`. No new activation endpoint, no installer-side networking required for trial, no changes to the signed-token format.
- Q: Should the portal's user accounts (Team Members) be the SAME as the on-prem product's users, or separate identity stores? → A: Separate identity stores — portal Team Members manage the commercial relationship (subscriptions, billing, support); on-prem product users manage operational data (invoices, journals, approvals). Same person may exist in both with different emails. No SSO, no cross-system password sync. The portal MUST NOT make any auth call to the customer's on-prem server, and the on-prem server MUST NOT call the portal for authentication.
- Q: How should tier upgrades and downgrades work — self-service vs sales contact, and when do they take effect? → A: Self-service upgrade with instant proration (any unused EGP from the current period credits toward the new tier, customer pays the difference now); self-service downgrade that takes effect at the next renewal (no mid-period refund). No sales involvement required for either direction. Mirrors the Stripe / Notion / Linear pattern; upgrades are friction-free for revenue, downgrades wait for natural renewal boundary to avoid refund complexity + rage-downgrade abuse.
- Q: What are the refund policy terms (window, eligible transactions, partial vs full)? → A: 7-day full refund on the first paid Subscription period only; no refunds on any renewal; no refunds on tier upgrades after the 7-day window. Renewals are non-refundable because the customer has had 14 days of trial + at least 30 days of paid use to evaluate before a renewal charges. Refund requests within the 7-day window are self-service through the portal and process to the original payment method within the payment provider's standard settlement window.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prospect discovers DaftarX and lands on a price they can act on (Priority: P1)

A small-business owner in Cairo searches Google for "برنامج محاسبة مصري" or "Egypt e-invoice software". The first result they trust is the DaftarX homepage. They read the value props (Arabic-first, ETA e-invoicing built in, Egyptian tax law baked in), browse to the features page to confirm it covers what their accountant cares about (VAT return, WHT, payroll basics), then land on the pricing page where they see four clear tiers in EGP. They pick the right tier for their headcount, click "Start trial", and end up either on a signup form or talking to a sales person — without ever feeling lost.

**Why this priority**: This is the entire commercial top-of-funnel. Without it, no customers exist for any of the other surfaces to serve. Marketing pages are also static-cacheable and have no auth complexity, so they're the cheapest slice to ship and the one that delivers the most immediate business value.

**Independent Test**: Open the homepage from a clean browser (no cookies), navigate Homepage → Features → Pricing → "Start trial" CTA, and confirm the trial sign-up flow is reachable in ≤ 3 clicks. All four pricing tiers (Solo / SMB / Enterprise / Firm) are visible with EGP prices and the right feature checklists.

**Acceptance Scenarios**:

1. **Given** a first-time visitor with Arabic browser preferences, **When** they load the homepage, **Then** the page renders in Arabic with RTL layout and the language toggle defaults to ar-EG.
2. **Given** a visitor on the homepage, **When** they click "الأسعار" / "Pricing" in the nav, **Then** the pricing page lists four tiers (Solo, SMB, Enterprise, Firm) with monthly + annual EGP prices, a checklist of included features per tier, and a primary CTA per tier.
3. **Given** the visitor is on any marketing page, **When** they toggle the language switcher to English, **Then** the URL changes to the `/en/...` prefix and the same page renders in English (LTR layout preserved for the few English-only flows).
4. **Given** the visitor reaches the downloads page, **When** they pick "Android", **Then** they see both the Google Play badge AND a direct APK download link with a checksum, plus the version-history "newer version banner" copy that mirrors what the Android app shows (per feature 009 FR-020).

---

### User Story 2 - Existing customer manages their license after replacing a laptop (Priority: P1)

The owner's accountant has a laptop crash. They get a replacement, install DaftarX on the new machine, and the activation screen shows them a new hardware ID. The customer logs in to the DaftarX portal, opens License Management, sees their old laptop's license listed as "active on HWID-old", pastes the new HWID, clicks Transfer, watches the system retire the old token + issue a fresh signed token for the new HWID, downloads the new token file, drops it into the new install, and is back to work — all in under 5 minutes, no support ticket needed.

**Why this priority**: License lifecycle is the single most painful post-purchase experience for any on-prem software. Without self-service transfer, every "I got a new laptop" event becomes a support ticket. P1 because every customer experiences this at least once and the workaround (email vendor + wait + receive token) is unacceptable.

**Independent Test**: Sign in as a customer with one active license. From License Management, paste a different (mocked) HWID into the Transfer dialog, complete the flow, and confirm the new token downloads + the old HWID's row in the licenses table is marked "transferred" with an audit trail.

**Acceptance Scenarios**:

1. **Given** a customer with one active Solo license bound to HWID-A, **When** they log in and open License Management, **Then** the active license is listed with edition, customer name, expiry date, current HWID, and three action buttons (Download token, Transfer, Renew).
2. **Given** the customer clicks Transfer, **When** they paste a new HWID-B and confirm, **Then** the system invalidates the HWID-A token, signs a fresh token for HWID-B, makes the new token downloadable, and writes an audit-log entry naming both HWIDs + the user who initiated the transfer.
3. **Given** the customer is on an Enterprise license, **When** they open License Management, **Then** they see a "Priority support" badge (per Feature.PrioritySupport from the existing licensing model) and faster SLA copy than the Solo/SMB tiers see.

---

### User Story 3 - New customer signs up + pays + downloads the installer (Priority: P1)

A prospect clicks "Start trial" from the pricing page. They sign up with email + password, confirm their email, and download the installer — the trial runs entirely client-side per FR-030 with no portal interaction required. Once they decide to subscribe, a guided multi-page flow in the portal walks them through tier selection → monthly or annual → payment method (Fawry / InstaPay / Vodafone Cash / bank transfer / card) → payment → downloads. Per FR-031 they then return with the hardware id from their install screen, paste it into the "Activate paid licence" form, and download the signed `.token` file. The whole flow from "Start trial" click to "DaftarX-Setup.msi downloading" finishes in under 15 minutes.

**Why this priority**: This is the conversion path that turns marketing traffic into revenue. Same P1 as US1 + US2 because all three are required for a viable vendor business — marketing without checkout doesn't pay rent.

**Independent Test**: From the homepage, click "Start trial" → complete signup with a real email → confirm email link → pick Solo tier + 14-day trial → land on the downloads page with the desktop installer ready to download. Measure total elapsed time.

**Acceptance Scenarios**:

1. **Given** a prospect on the pricing page, **When** they click "Start trial" on the Solo tier, **Then** the signup form appears with email + password fields and a "I agree to Terms + Privacy Policy" checkbox.
2. **Given** the prospect completes signup and confirms their email, **When** they enter the portal for the first time, **Then** a guided multi-page flow walks them through (a) choose tier, (b) monthly or annual, (c) payment method, (d) payment, (e) download. Each step is its own page with persistent state — the customer can leave and resume without re-entering earlier choices.
3. **Given** the customer picks Fawry as the payment method, **When** they confirm, **Then** the portal generates a Fawry reference code, displays it with payment instructions in Arabic, and the Subscription is provisioned automatically when the payment clears (no manual vendor step required).
4. **Given** a customer completes payment, **When** they reach the downloads page, **Then** the DaftarX-Setup.msi for the desktop is the primary download button + the Android variants are listed alongside; the customer is instructed to install the desktop product first, then return to the portal's "Activate paid licence" form with their hardware id from the install screen to receive the signed token.

---

### User Story 4 - Customer submits a support ticket with screenshot (Priority: P2)

A customer hits an obscure bug — their VAT return generator throws an error when they include a specific invoice. From the portal they open Support, click "New ticket", pick category "Bug", write a description, attach a screenshot of the error, and submit. The portal records the ticket with priority + status, the customer sees an estimated SLA based on their tier (Enterprise+ shows "≤ 4 business hours; everyone else shows "≤ 24 business hours"), and they receive an email confirmation. The vendor's support team picks it up, posts updates in the ticket thread, and the customer sees the updates the next time they visit the portal (and via email notification).

**Why this priority**: Support is critical for retention, but most v1 customers can email or WhatsApp the vendor while we ship US1–US3. Bundled into P2 to land in the second iteration alongside organization management.

**Independent Test**: Sign in, create a new ticket with all fields populated + a 2MB screenshot attached, confirm the ticket appears in the customer's ticket list with status "Open", and verify the support-staff view of the same ticket shows the attachment and the customer's tier badge.

**Acceptance Scenarios**:

1. **Given** an authenticated customer on the Support page, **When** they click "New ticket", **Then** a form appears with: category (billing / bug / feature request / accounting question / urgent), priority (low / normal / high — high disabled for Solo customers), description, attachment upload (max 5 MB per file, max 3 files).
2. **Given** an Enterprise customer submits a ticket, **When** the ticket lands in the queue, **Then** it is tagged with "Priority support" + the SLA timer starts at 4 business hours.
3. **Given** a vendor support staff replies, **When** the customer next visits the portal (or opens the email notification), **Then** the new reply is visible inline in the ticket thread with timestamps and the staff member's display name (not their email).

---

### User Story 5 - Owner invites their bookkeeper to share the organisation (Priority: P2)

A small accounting firm subscribes to DaftarX Firm tier. The firm's owner signs up + completes purchase first. Then from the portal's Organisation Settings, they invite their two bookkeepers + one billing admin by email, each with a per-member role (owner / billing-admin / support-admin / read-only). The invitees receive an email with a one-tap link, set their password, and land in the same organisation as the owner — sharing the licenses, the billing history, and the support ticket queue.

**Why this priority**: Multi-user only matters for the SMB+ tiers (Solo customers are a single user by definition). P2 because the single-user portal experience covers the majority of v1 customers; multi-user is the next logical extension.

**Independent Test**: Sign in as an Owner, invite a fresh email address with the "billing-admin" role, accept the invitation from a clean browser session, and confirm the invitee can see Billing pages but cannot transfer licenses (which is an owner-only action).

**Acceptance Scenarios**:

1. **Given** an Owner on Organisation Settings, **When** they enter an email + pick a role, **Then** an invitation email is sent with a one-tap link valid for 7 days.
2. **Given** an invitee clicks the link from a fresh browser, **When** they set their password and accept, **Then** they land on the organisation dashboard with the role granted (e.g. billing-admin sees Billing menu but no License-Transfer button).
3. **Given** an Owner removes a member, **When** the removal completes, **Then** the removed member's active session is terminated within five minutes + future logins are rejected.

---

### User Story 6 - Privacy policy stays at a stable URL for Play Store compliance (Priority: P2)

The Android app's Play Store Data Safety declaration (per feature 009 FR-018) links to `daftarx.app/privacy/android`. As long as this URL renders the vendor's privacy policy in both Arabic and English, the Play Store listing stays compliant. The website must NEVER move or delete this URL without first updating the Play listing — the URL is part of the published product surface.

**Why this priority**: Compliance dependency. Without this URL holding, the Android app's Play Store listing fails review. P2 because the URL only needs to exist before the Android app ships to the public Play Store (which is itself a P3 task in feature 009); P1 in operational terms for the time when those align.

**Independent Test**: Request `https://daftarx.app/privacy/android` from anywhere on the public internet (no auth). Confirm the page returns 200 with the vendor's privacy policy text in both languages and a "last updated" date.

**Acceptance Scenarios**:

1. **Given** the website is live, **When** a Play Store reviewer (or anyone) opens `daftarx.app/privacy/android`, **Then** the page renders the vendor's privacy policy in the visitor's preferred language with a default to ar-EG.
2. **Given** the vendor amends the privacy policy, **When** they publish the update, **Then** the previous version is archived (accessible at `/privacy/android/history/<date>`) so prior consent claims remain auditable.

---

### Edge Cases

- A prospect lands on an `/ar/pricing` URL but their browser is configured for English — the language switcher must offer one tap to swap to `/en/pricing` without losing scroll position.
- A customer's payment provider (Fawry / InstaPay / etc.) takes 24 hours to confirm — the portal must show "Awaiting payment confirmation" with the reference code visible, NOT mark the licence active prematurely.
- A customer tries to transfer a license but the new HWID is already bound to a DIFFERENT customer's licence — reject with a clear error ("this HWID is associated with another active licence; contact support").
- A customer cancels mid-period — billing stops at end of current period, license remains active until expiry, then auto-deactivates with one warning email at T-7.
- A support ticket attachment is uploaded that is larger than 5 MB or in an unsupported type — show inline error, don't lose the description the customer already typed.
- An invited team member's email lands in spam and they click the invitation after the 7-day window — the link must show a friendly "expired; ask the owner to resend" page, not a 404.
- A bot scrapes the homepage repeatedly — rate limit + obvious bot patterns get throttled, real users never see the throttle.
- A customer requests GDPR-style "delete my account" — the portal must support self-service account deletion with a soft-delete window for the audit trail.
- The vendor's payment provider (Paymob, Fawry) is down — the portal shows a clear "payments temporarily unavailable, please retry in N minutes" rather than failing silently or charging twice.
- A customer in an unsupported region (outside Egypt for v1) signs up — the portal still allows it but pricing displays a "EGP only; we don't currently invoice outside Egypt" note before checkout.

## Requirements *(mandatory)*

### Functional Requirements

#### Marketing surface (public, no auth)

- **FR-001**: The website MUST publish a homepage at the root URL (`daftarx.app/`) that presents the product's value proposition for Egyptian SMEs, primary call-to-action buttons (Start trial / See pricing / Download), and brand identification, rendering in Arabic with right-to-left layout by default for first-time Arabic-locale visitors.
- **FR-002**: The website MUST publish a features page that enumerates all features offered by the product, organised by category, with a short bilingual explainer + visual (screenshot or icon) for each, and a clear mapping from each feature to the minimum subscription tier that includes it.
- **FR-003**: The website MUST publish a pricing page listing four subscription tiers (Solo, SMB, Enterprise, Firm) with monthly and annual prices in Egyptian pounds, a per-tier feature checklist, a side-by-side comparison matrix, and a primary call-to-action per tier.
- **FR-004**: The website MUST publish a downloads page that lists every installable artefact (Windows desktop installer, Windows LAN-client installer, Android via Play Store, Android via direct-download APK) with file size, version, release date, and integrity checksum where applicable.
- **FR-005**: The website MUST publish an about page (vendor story, contact for accounting-firm partnerships) and a contact / sales page with a form that creates a sales lead in the portal backend, plus WhatsApp / phone / email channels for direct outreach.
- **FR-006**: The website MUST publish a privacy policy at the stable URL `daftarx.app/privacy/android` (and a vendor-wide policy at `daftarx.app/privacy`), in both Arabic and English, disclosing what the vendor collects via the mobile app (crash diagnostics + push device tokens per feature 009's FR-018) and via the website itself.
- **FR-007**: The website MUST publish terms of service and refund policy pages and link them from the footer of every page.
- **FR-008**: Every marketing page MUST be available in both Arabic (ar-EG, primary) and English (en-US, fallback) via URL-prefixed locales (`/ar/...` and `/en/...`), with a language switcher visible on every page that preserves the visitor's current page when switching.
- **FR-009**: The website MUST resolve a Google search for an Egyptian-accounting-related Arabic query (e.g. "برنامج محاسبة مصري") to the homepage within the first page of results within sixty days of launch (operational SEO target — covered by structured-data, sitemap, and metadata requirements baked into the page templates).

#### Customer portal (authenticated)

- **FR-010**: The portal MUST allow a new visitor to sign up with email + password and confirm their email address before any paid action is allowed.
- **FR-011**: The portal MUST support multi-factor authentication via time-based one-time password as an opt-in setting from the user's security settings, and MUST require MFA for Owner-role users when configured at the organisation level.
- **FR-012**: The portal MUST display a dashboard summarising the customer's subscription status, active licenses, next renewal date, open support ticket count, and recent downloads.
- **FR-013**: The portal MUST allow the Owner role to view every active license bound to the organisation, including the licensed edition, hardware id, customer name on the token, expiry date, and the actions Download token / Transfer to new hardware id / Renew / Cancel.
- **FR-014**: When the Owner initiates a license transfer to a new hardware id, the portal MUST verify the new id is not already bound to a different customer, retire the old token (record the retirement in an audit log entry), issue a freshly signed token for the new id, and make the new token immediately downloadable.
- **FR-015**: The portal MUST accept the four Egyptian payment methods Fawry, InstaPay, Vodafone Cash, and credit/debit card via a payment processor (Paymob or equivalent), plus offline bank transfer with manual vendor reconciliation, for every paid transition (initial purchase, renewal, tier upgrade, add-on purchase).
- **FR-016**: The portal MUST produce a downloadable PDF invoice receipt for every cleared payment, in Arabic with an English-fallback variant, conforming to the Egyptian invoice numbering + tax line requirements of the customer's own tax regime.
- **FR-017**: The portal MUST allow Owners to download the latest Windows desktop installer, Windows LAN-client installer, and Android APK matching their licensed tier, plus access the three most recent prior versions per artefact for emergency rollback.
- **FR-018**: The portal MUST allow any authenticated user to submit a support ticket with category, priority, free-text description, and up to three file attachments (each up to 5 megabytes), and to view the full thread of that ticket including vendor replies.
- **FR-019**: The portal MUST display a service-level acknowledgement target on every new-ticket form (at most twenty-four business hours for Solo/SMB tiers, at most four business hours for Enterprise/Firm tiers) and surface a "Priority support" badge on the dashboard for tiers that include the priority-support entitlement.
- **FR-020**: The portal MUST allow Owners to invite team members by email with a role (owner, billing-admin, support-admin, read-only), and the invited member MUST set their password through a one-tap link valid for seven days before accessing organisation data.
- **FR-021**: The portal MUST scope every authenticated read and write to the user's organisation: a member of organisation A MUST never see any data belonging to organisation B, even by tampering with URL parameters.
- **FR-022**: When an Owner removes a team member, the portal MUST terminate that member's active sessions within five minutes and reject all subsequent authentication attempts from that account against the organisation.
- **FR-023**: The portal MUST emit an audit-log entry for every state change visible to the customer (license transfer, payment cleared, member invited, member removed, ticket status change) and surface a paginated view of the entries to Owners.
- **FR-024**: The portal MUST allow a customer to request self-service account deletion, with a thirty-day soft-delete window during which the data is recoverable on request and after which it is purged irreversibly except for the audit-log entries required for regulatory recordkeeping.

#### Cross-cutting

- **FR-025**: Every page (marketing and portal alike) MUST respond with a successful render in under three seconds from a Cairo-based broadband connection on a mid-range device, measured at the 75th percentile.
- **FR-026**: Every form that accepts user input MUST validate input on both client and server, reject malformed input with an actionable bilingual message, and never silently truncate or accept invalid data.
- **FR-027**: The website MUST be accessible to assistive technologies at the WCAG 2.1 AA conformance level across all marketing pages and the customer-portal core flows (login, dashboard, license management, downloads).
- **FR-028**: The website MUST log every authentication event (signup, login, MFA challenge, password change, account-recovery request) with timestamp + originating IP, retained for ninety days and accessible to the user from their security-settings page.
- **FR-029**: The portal MUST offer every new signup a 14-day trial that includes the full feature set of the existing Trial edition (Enterprise minus Firm Portal — the same feature list as `Feature.DefaultsFor(LicenseEdition.Trial)` in the on-prem product) without requiring any payment method up-front. On day 14, the install MUST auto-pause (the on-prem product's existing trial-expired banner appears, full read-only access is preserved, write actions are blocked) and the portal MUST present a single-tap "Subscribe to keep going" call-to-action that converts the trial Subscription into a paid Subscription on any of the four supported payment methods without re-asking for organisation, tier, or licensed-hardware details.
- **FR-030**: Trial activation MUST NOT require any portal interaction. A prospect who downloads the DaftarX installer from the marketing surface and runs it MUST receive a working 14-day trial without signing up, signing in, or talking to any vendor server — the on-prem product's existing client-side trial mechanism (the `LicenseStatus.RecordTrial` path) is the canonical trial granter. The portal exists only to convert the trial into a paid Subscription and to issue paid licence tokens; it MUST NOT issue or track trial tokens.
- **FR-031**: When a customer purchases or renews a paid Subscription, the portal MUST request the target machine's hardware id from the customer (typed in by hand from the install screen) and MUST sign a token bound to that hardware id via the existing `Issue-License.ps1` flow + `vendor-keys.json` keypair, returning the token as a downloadable `.token` file that the customer drops at `%PROGRAMDATA%\DaftarX\license\license.token`. The portal MUST NOT introduce a new activation-code or HTTPS-bind mechanism in v1; the existing manual install-first flow is the canonical paid-activation path.
- **FR-032**: The portal MUST maintain its own identity store for Team Members, completely separate from the on-prem product's user store. A Team Member account is scoped to commercial relationship management (subscriptions, billing, downloads, support) and MUST NOT grant access to any on-prem product instance. Conversely, an on-prem product user account MUST NOT grant access to the portal. No single sign-on, no cross-system password sync, no authentication call from portal to on-prem server or vice versa in either direction.
- **FR-033**: Tier upgrades MUST be self-service and take effect immediately upon payment success: the portal computes the prorated credit for the unused portion of the current period at the old tier, charges the customer the difference to the new tier's prorated remainder, issues a fresh signed token at the new tier for every active hardware id under the Subscription, and emails the customer the updated invoice. Tier downgrades MUST also be self-service but MUST take effect only at the next renewal: the portal flags the Subscription with a pending tier-change marker, retains the current tier's entitlements until the period ends, then transitions to the new tier on the next billing cycle. The portal MUST NOT issue a mid-period refund for a downgrade in v1.
- **FR-034**: The portal MUST honour a 7-day self-service refund window on the first paid Subscription period only. During the window, the customer MAY request a full refund through a single-tap action on the Billing page; the portal cancels the Subscription, retires the associated paid licence tokens (the on-prem product auto-reverts to trial-expired state on next launch), and reverses the payment to the original payment method within the payment provider's standard settlement window. Renewals MUST NOT be refundable. Tier upgrades MUST NOT be refundable once the 7-day window on the original Subscription has lapsed. The portal MUST display the refund-eligibility status (eligible / not eligible, with the day count) inline on the Billing page for every Invoice.

### Key Entities *(include if feature involves data)*

- **Customer Organisation**: The top-level account that owns subscriptions, licenses, billing history, support tickets, and team-member memberships. One per signup; an individual prospect's signup yields a single-member organisation.
- **Team Member**: A user authenticated against the portal who belongs to one or more Customer Organisations, with a role per membership (owner, billing-admin, support-admin, read-only). Cross-organisation membership exists for accounting firms supporting multiple client organisations.
- **Subscription**: A plan binding a Customer Organisation to a tier (Solo, SMB, Enterprise, Firm), a billing cadence (monthly, annual), a payment method, and a billing state (active, past-due, cancelled, paused). The portal does NOT track a `trial` state — trial is owned client-side by the on-prem product per FR-030.
- **License**: A signed token bound to a hardware id, derived from a Subscription. Multiple licenses per Subscription for the LAN-client + multi-device cases; each license has its own expiry and audit trail of issuance / transfer / revocation events.
- **Invoice**: A cleared payment record bound to a Subscription, with a sequential per-organisation invoice number, the four-method payment selector, the amount in Egyptian pounds, and a downloadable PDF receipt.
- **Support Ticket**: A customer-submitted issue with category, priority, description, attachments, status (open, in-progress, resolved, closed), assigned vendor staff member, and a reply thread visible to both the customer and the staff.
- **Sales Lead**: A pre-signup record created by the contact form, with the prospect's contact details, the page they came from, the tier they expressed interest in, and the date the vendor's sales team last contacted them.
- **Audit Log Entry**: An immutable record of every customer-visible state change, including the actor (user id), the verb (license.transferred, member.invited, etc.), the subject (license id, ticket id), and the timestamp.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A prospect arriving on the homepage from a Google search for an Arabic accounting query reaches the "Start trial" call-to-action in no more than three clicks (homepage → pricing → trial CTA, or homepage → features → pricing → trial CTA).
- **SC-002**: A new customer can complete the full signup → tier selection → payment → license-token download → desktop installer download flow in under fifteen minutes from the moment they first click "Start trial".
- **SC-003**: An existing customer transferring their license to a new hardware id can complete the entire flow (log in → License Management → Transfer dialog → confirm → new token downloaded) in under five minutes, with no support-team involvement.
- **SC-004**: The privacy policy at `daftarx.app/privacy/android` MUST remain reachable with a 200 response on every business day from the moment the Android app's Play Store listing references it; any planned URL change MUST be preceded by a Play-listing update.
- **SC-005**: At least ninety-five percent of support tickets receive a vendor reply (acknowledgement, not necessarily resolution) within twenty-four business hours of submission, measured rolling over each calendar week.
- **SC-006**: Every page on the marketing surface returns the initial render in under three seconds from a Cairo broadband connection on a mid-range device at the seventy-fifth percentile of measurements.
- **SC-007**: An invited team member can move from clicking the invitation email to seeing the organisation dashboard in under three minutes, including setting their password.
- **SC-008**: The downloads page tracks parity with the Android app's update banner: when feature 009 publishes a new APK version, the version string on the downloads page matches within one business day.
- **SC-009**: The portal renders correctly in both Arabic (RTL) and English (LTR) for every authenticated user surface (dashboard, license management, billing, support, organisation settings, account security), with zero layout-mirroring bugs at WCAG-AA inspection.

## Assumptions

- The vendor operates a single regulatory entity in Egypt and bills exclusively in Egyptian pounds for v1; multi-currency and multi-jurisdiction expansion is out of scope (deferred to a follow-up feature).
- The vendor holds a contract with an Egyptian payment aggregator (Paymob or equivalent) supporting the four named payment methods plus card; integration credentials are provisioned outside this spec.
- The on-prem DaftarX product (feature 008) retains its own customer database for accounting business records; this website's database holds only commercial relationship data (Customer Organisations, Subscriptions, Licenses, Invoices, Tickets, Sales Leads, Audit Log) — there is no overlap with the customer-server data the on-prem product manages.
- The vendor's existing `Issue-License.ps1` flow and the Ed25519 signing keypair in `vendor-keys.json` are the canonical source of license tokens; the portal calls into the same signing path so the on-prem product's verifier accepts portal-issued tokens unchanged.
- The website is hosted by the vendor at the root domain `daftarx.app`; subdomains are reserved for future use (status page, blog, docs).
- The website is single-region in v1, served from a Cairo or nearby data centre for the latency target in SC-006; multi-region CDN is desirable but not required.
- The Android app's Play Store data-safety declaration depends on the privacy-policy URL stability documented in FR-006 and SC-004 — coordination between this feature and feature 009 release schedules is the vendor's release-engineering responsibility.
- Search-engine optimisation (SC-001 indirectly) depends on standard structured-data, sitemap, page-metadata, and indexable-HTML practices — the absence of those would silently invalidate the success criterion; the implementation plan must spell them out.
- Account-deletion (FR-024) has a thirty-day soft-delete window aligned with the most permissive interpretation of common consumer-data regulations; the vendor's legal counsel may shorten this in a future amendment but the floor is the audit-log retention required for regulatory recordkeeping.
- Multi-organisation membership for accounting-firm staff (the Team Member entity supporting many-to-many with Customer Organisation) is in scope for v1 because the Firm tier requires it; full firm-management workflows (white-label, branded portal per firm) are explicitly out of scope.
- Live chat, in-product help videos, and a customer's data backup/restore from the cloud are out of scope; support is via tickets + WhatsApp.
