# Feature Specification: DaftarX Android App

**Feature Branch**: `009-android-app`
**Created**: 2026-05-17
**Status**: Draft
**Input**: User description: "DaftarX Android app — Hybrid Kotlin/Compose shell hosting a WebView pointed at the customer's DaftarX server, augmented with native shell features (biometric unlock, camera OCR, share target, FCM push, deep links). Distribution: side-loaded APK + Google Play Store."

## Clarifications

### Session 2026-05-17

- Q: How should the vendor learn about client-side crashes and runtime errors in the field? → A: Firebase Crashlytics (same Firebase project as push notifications; stack traces + device metadata only, no customer business data).
- Q: What scope of data collection should the Play Store Data Safety form declare? → A: Vendor-paths only — declare what the vendor sees (crash stack traces + device metadata via the crash service; push device token + per-install device-id via the push channel). Customer-server data flows (receipts, business records) are treated as customer-controlled processing, with the listing pointing at the customer's own privacy policy.
- Q: How should the app and the on-prem server handle version skew (app newer than server OR app older than what the server requires)? → A: Bidirectional negotiation — on every foreground, the server reports its version + a minimum supported app version; the app compares against its own minimum supported server version. Either-direction mismatch shows a friendly screen identifying which side needs to update, blocking native shell flows until resolved.
- Q: How do side-loaded APK installs (which do not benefit from Play Store auto-update) learn about new versions, especially when bidirectional version negotiation blocks them on an outdated build? → A: Notification-only banner — the server's features-snapshot response carries a `latestKnownAppVersion` field; when the running app is older, a dismissable banner advertises the new version with a link to the vendor's download page. No in-app download mechanism, no `REQUEST_INSTALL_PACKAGES` permission. Play Store installs naturally never see the banner because Play has already updated them.
- Q: How much detail should push notifications expose on the device lockscreen (where shoulder-surfers may glance)? → A: Standard Android visibility — full content (vendor names, amounts, deadlines) shown when the device is unlocked; redacted summary ("3 approvals waiting") shown on the lockscreen. Uses Android's built-in `Notification.VISIBILITY_PRIVATE` mechanism — no per-category configuration UI.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pocket DaftarX for the owner (Priority: P1)

A small-business owner installs the DaftarX Android app, points it at their on-prem DaftarX server once, and from that day forward unlocks it with their fingerprint to glance at today's sales, pending approvals, and tax deadlines while away from the office. The app feels like a native phone app — no browser chrome, no URL bar, no repeated logins — even though most screens are the same UI they use on the desktop.

**Why this priority**: This is the primary "why buy a phone app" value. Owners are mobile, frequently away from the desk, and need quick read-only visibility. Every other native feature builds on top of this baseline of "install once, unlock fast, see the dashboard."

**Independent Test**: Install the APK on a clean device, type the server URL, sign in to a known account with biometric unlock enabled, close the app, re-open it, biometrically authenticate, and confirm the DaftarX home dashboard renders. No other features (camera, push, share) are needed for this slice to deliver value.

**Acceptance Scenarios**:

1. **Given** a fresh install with no saved server URL, **When** the user opens the app for the first time, **Then** a server-configuration screen appears prompting for the server host (with port 50063 pre-filled as default).
2. **Given** a valid server URL is configured and saved, **When** the user opens the app again, **Then** the configuration screen is bypassed and the in-app web view opens the saved server's login page (or dashboard if a session is still valid).
3. **Given** the user successfully signed in and enabled biometric unlock, **When** they re-open the app after closing it, **Then** a biometric prompt appears; on success the in-app web view resumes the previous session without re-typing the password.
4. **Given** the user is signed in, **When** they navigate within the DaftarX web app via the in-app web view (sidebar, sub-nav, deep pages), **Then** all interactions behave identically to the desktop browser experience, including bilingual labels and RTL layout.

---

### User Story 2 - Sales rep captures a receipt in the field (Priority: P2)

A sales rep visits a customer, pays a small expense at a roadside stop, and immediately captures the paper receipt using the app's native camera. The image is uploaded to the DaftarX server's OCR endpoint, the rep sees a confirmation that the receipt was queued, and when they return to the office the bookkeeper can review the extracted vendor / total / VAT in the desktop scan-history.

**Why this priority**: The native-camera UX is the highest-leverage native feature — it's the one piece the in-app web view genuinely can't deliver well (the in-page file picker offers no cropping, lighting hints, or burst mode). It also unlocks AP-cycle compression for any customer on SMB+ with the ReceiptOcr feature.

**Independent Test**: With the app signed in and the customer's license including the ReceiptOcr feature, tap a visible "Scan receipt" entry point, capture a photo of a sample receipt, and confirm the row appears in /scan-history on the desktop within 10 seconds.

**Acceptance Scenarios**:

1. **Given** the user is signed in to a license that includes ReceiptOcr, **When** they look at the app, **Then** a native "Scan receipt" floating action or menu entry is visible.
2. **Given** the user taps Scan receipt, **When** the OS grants camera permission and the user captures + confirms an image, **Then** the image is uploaded to the server's scan-receipt endpoint and a success confirmation is shown.
3. **Given** the user is on a Solo license (no ReceiptOcr feature), **When** they look at the app, **Then** the Scan-receipt entry point is hidden — same gating as the desktop sidebar.
4. **Given** the user denied camera permission, **When** they tap Scan receipt, **Then** a clear explanation appears with a button that opens the OS app-settings screen.

---

### User Story 3 - Approver gets pinged when something needs review (Priority: P2)

An accountant has set DaftarX as their approval-queue handler. When a colleague submits a sales invoice for review, the accountant's phone receives a push notification ("3 invoices waiting for your approval"). Tapping the notification opens DaftarX directly on the approval queue, already filtered to items assigned to them.

**Why this priority**: Asynchronous approvals are a major pain in the desktop-only world — accountants have to remember to open DaftarX. Push notifications close that loop. Same priority as the camera because the two features together transform the field experience.

**Independent Test**: Have a second user submit a document needing the test user's approval. Confirm the test device receives a notification within 60 seconds. Tap the notification and confirm the app opens the approval queue.

**Acceptance Scenarios**:

1. **Given** the user signs in with notifications permission granted, **When** the sign-in completes, **Then** the device's push-notification token is registered with the server bound to that user.
2. **Given** a document is submitted that lands in the user's approval queue (per the server's existing queue-visibility logic — i.e. the user is in an approver role AND did not submit the document themselves), **When** the server emits the approval-pending event, **Then** the server MUST hand off to the push channel within 60 seconds; actual on-device delivery latency depends on the push transport's best-effort SLA.
3. **Given** the user taps the push notification, **When** the app opens, **Then** it lands on the approval queue page (deep-linked) without an intermediate dashboard hop.
4. **Given** the user signs out of the app, **When** sign-out completes, **Then** the device's push-notification token is unregistered from the server so further notifications stop.

---

### User Story 4 - Share-to-DaftarX from WhatsApp / email (Priority: P3)

A supplier sends an invoice PDF over WhatsApp. The user long-presses the PDF, taps "Share", picks DaftarX from the list, and the file is uploaded directly to DaftarX as a scanned receipt — no save-to-Downloads-then-open-DaftarX-then-pick-file roundtrip.

**Why this priority**: A high-delight smoothing-of-workflow feature, but secondary to the camera path because it depends on the receipt arriving as a digital file (often suppliers send paper). Bundled with P3 because the implementation reuses most of the camera path.

**Independent Test**: From the Android share sheet on any image or PDF, select DaftarX. Confirm the upload completes and the file lands in /scan-history.

**Acceptance Scenarios**:

1. **Given** the user has DaftarX installed and signed in, **When** they share a PDF or image from any other app, **Then** DaftarX appears as a target in the system share sheet.
2. **Given** DaftarX is selected as the share target, **When** the share intent fires, **Then** the file is uploaded to the server's scan-receipt endpoint and a confirmation is shown without requiring the user to navigate inside the app.
3. **Given** the user is not signed in when sharing, **When** they pick DaftarX, **Then** they are prompted to sign in first and the share is queued + completed after sign-in.

---

### User Story 5 - License-aware native shell (Priority: P3)

When a Solo customer upgrades to SMB and the new license token is activated, the native shell features that were previously hidden (camera scan, AI suggestions) appear without requiring a reinstall — the next time the app polls the server's features endpoint the native UI re-renders accordingly.

**Why this priority**: Required for the gating story to feel coherent, but no individual user explicitly asks for it — they just expect it to "match what the web shows." Lower priority because the gating already works at the in-app web view layer; this is only about the native shell entry points (camera button, share target advertising).

**Independent Test**: Sign in on a Solo license, confirm camera button is hidden. Have the operator activate an SMB license token on the server. Re-open the app and confirm the camera button is now visible.

**Acceptance Scenarios**:

1. **Given** the app is signed in to a Solo license, **When** the user opens the app, **Then** the native camera-scan entry point is hidden and the share-target advertising is disabled.
2. **Given** the server license is upgraded to one that includes ReceiptOcr, **When** the app makes its next features check (on app foreground), **Then** the camera-scan entry point becomes visible without an app restart.

---

### Edge Cases

- The configured server URL is unreachable (LAN down, server stopped, wrong port): the app shows a retry screen with the current URL and a button to reconfigure, not a generic "page can't be displayed" error.
- The user typed a malformed URL (e.g., missing scheme, trailing whitespace): the configuration screen validates + normalises the input before saving.
- Server uses a self-signed TLS certificate: in v1 the app rejects the connection with a clear "certificate not trusted" error; deferring custom CA-bundle support to a later release.
- Biometric hardware is absent or disabled on the device: fall back to a numeric-PIN gate set during onboarding; if neither exists, every cold start requires the web password.
- The OS revokes the push-notification token (periodic rotation, app reinstall, "Clear Data"): on the next app launch the new token is detected and re-registered with the server.
- The customer downgrades their license (Enterprise → SMB): the native shell polls features on foreground and removes any newly-locked native entry points within one polling cycle.
- The user shares a file larger than the server's accepted limit, or a MIME type the OCR pipeline doesn't handle: the app reports the failure with the server's reason message rather than silently dropping the upload.
- The user denies camera or notification permissions: the corresponding features advertise themselves disabled with one-tap deep links to the OS app-settings screen.
- Deep link (`daftarx://invoice/<id>`) arrives while the user is not signed in: the link is queued; after successful login the in-app web view navigates to the original target.
- The in-app web view crashes (rare, usually GPU-process death on low-RAM devices): the shell restarts the in-app web view preserving the current URL + session.
- Device rotation, split-screen, or picture-in-picture: in-app web view state survives the configuration change without a reload.
- The user changes the configured server URL: cached session cookies for the previous server are wiped, biometric unlock is reset, and a fresh login is required.
- The user installs the side-load APK then later installs the Play Store edition: both signed with the same key so Android treats it as an update; user data is preserved.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The app MUST present a server-configuration screen on first launch that accepts a host name or IP address and a port number (defaulting to 50063), validates connectivity to the entered server, and persists the result for subsequent launches.
- **FR-002**: The app MUST cache the in-app web view session credentials so that returning users do not retype the DaftarX web password on every cold start, releasing the cached session only after a successful biometric or PIN unlock.
- **FR-003**: The app MUST expose a native "Scan receipt" action when the active license includes the ReceiptOcr feature; the action MUST open the device camera, allow capture, and upload the captured image to the server's receipt-scan endpoint.
- **FR-004**: The app MUST register itself as a system share target for image and PDF MIME types so that users can share files from other apps; received files MUST be routed to the same receipt-scan endpoint as the native camera path.
- **FR-005**: The app MUST receive push notifications for four event categories — items visible in the user's approval queue (per the server's existing FR-004 logic that hides documents the user submitted themselves), tax deadline reminders (T-7 and T-1), ETA submission failures, and license expiry warnings (T-30 and T-7) — and present each as an actionable notification that deep-links into the relevant page on tap.
- **FR-006**: The app MUST register its push-notification token with the server immediately after a successful sign-in and unregister the token on sign-out, scoped per user-per-device.
- **FR-007**: The native shell MUST honour the same edition-gating that the web UI enforces: native entry points for premium features (camera scan, AI suggestions, etc.) MUST be hidden when the active license does not include the corresponding feature, refreshing within one polling cycle of a license change.
- **FR-008**: The app MUST support deep links of the form `daftarx://...` and HTTPS links matching the configured server's origin, and MUST jump directly to the corresponding in-app web view page on tap; if the user is not authenticated, the link MUST be honoured after sign-in completes.
- **FR-009**: The app MUST allow the user to enable or disable screenshot blocking for sensitive screens (login, financial reports) from a settings screen, defaulting to enabled.
- **FR-010**: The app MUST automatically lock the in-app web view session after a configurable idle period (default five minutes) and require a biometric or PIN unlock to resume.
- **FR-011**: The app MUST default the user interface to Arabic with right-to-left layout and offer an English fallback selectable per device.
- **FR-012**: The app MUST run on devices with Android 7.0 or newer and MUST target the current platform API level required by Google Play distribution rules.
- **FR-013**: The same signing identity MUST be used for the side-loaded APK and the Google Play Store build so that a user who installed via one channel can switch to the other without uninstalling.
- **FR-014**: The app MUST allow the user to reset the server configuration from a settings screen, which on confirmation wipes the cached session, biometric binding, and push-notification registration and returns the user to the first-launch configuration screen.
- **FR-015**: When the configured server is unreachable, the app MUST present a friendly retry screen (not a raw browser error) that displays the current server URL and offers a button to reconfigure.
- **FR-016**: The app MUST present the OS app-settings screen via a single tap when the user previously denied camera or notification permissions and now attempts the corresponding action.
- **FR-017**: The app MUST report uncaught crashes and explicitly-logged non-fatal exceptions to a vendor-controlled telemetry endpoint, capturing only stack traces, device model, OS version, app version, and locale — never customer business data, server URLs, session credentials, push tokens, captured receipt content, or any field from the WebView's DOM.
- **FR-018**: The Play Store listing's Data Safety declaration MUST disclose only the vendor-controlled data flows — crash stack traces + device metadata (collected by the crash-reporting service) and the push device token + per-install device-id (collected by the push channel) — and MUST link a vendor-published privacy policy. The declaration MUST NOT enumerate customer-server data flows (receipts, business records, login credentials), which are governed by the customer's own privacy policy and surfaced via an "additional data handled by the operator's server" note on the listing.
- **FR-019**: The server MUST report its own version and the minimum app version it supports on every license-feature snapshot request, each as a flat `MAJOR.MINOR.PATCH` string; the app MUST compare these against its built-in version and built-in minimum supported server version on every foreground using component-wise integer comparison. When the app version is below the server's minimum, the app MUST present a "please update DaftarX from the Play Store / re-download the APK" screen. When the server version is below the app's minimum, the app MUST present a "your DaftarX server needs an update to version X.Y.Z or newer" screen showing the configured server URL. Both screens MUST block all native shell flows (camera, share, deep links into the web view) until the mismatch is resolved.
- **FR-020**: The server MUST additionally report the latest known app version (the most recent build the vendor has published) on every license-feature snapshot request. When the running app's version is below the reported latest, the app MUST display a dismissable banner advertising the new version with a link to the vendor's download page. The banner MUST NOT block any flows (it is purely informational, distinct from the bidirectional version-gate screens in FR-019). The app MUST NOT request the `REQUEST_INSTALL_PACKAGES` permission or attempt to download the new APK itself.
- **FR-021**: Every push notification the app posts MUST set a private lockscreen visibility, exposing only a redacted summary (category + count or category + generic label) when the device is locked, and revealing full content (vendor name, amount, deadline date, invoice identifier) only when the device is unlocked. Notification visibility MUST NOT be user-configurable per category in v1 — the OS-driven unlock state is the single authority on whether full content is shown.

### Key Entities *(include if feature involves data)*

- **Server Configuration**: The host name, port number, and last-validated-at timestamp of the DaftarX server this install talks to. One per install in v1. Lives in encrypted local storage so a device-image extract doesn't expose the URL.
- **Cached Session**: The session token the in-app web view received from the server after the user signed in, plus a flag indicating whether biometric unlock is required to release it. Stored in the device's secure credential store.
- **Push Notification Registration**: The current push-notification device token, bound to (server URL, signed-in user). Re-registered on every app launch and on every token-rotation event.
- **Pending Shared File**: A queued upload from the system share sheet that arrived while the user was not authenticated; held in memory until sign-in completes, then released to the upload pipeline.
- **License Feature Snapshot**: A short-lived copy of the active license's feature list, pulled from the server on app foreground, used to decide which native shell entry points to surface. Not persisted across cold starts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user can install the app, complete the server-configuration screen, sign in for the first time, and enable biometric unlock — all in under three minutes from APK install to dashboard visible.
- **SC-002**: A returning user can unlock the app and reach the DaftarX dashboard via biometric authentication in under five seconds from app icon tap.
- **SC-003**: A receipt captured with the native camera appears in the desktop scan-history within ten seconds of the user confirming the capture.
- **SC-004**: At least 95% of push notifications, when tapped, land the user on the intended deep-linked page (approval queue, compliance calendar, ETA dashboard, license tab) on the first attempt.
- **SC-005**: The signed install package runs without manual prerequisite steps (no separate runtime, no per-device certificate trust) on at least 98% of in-field Android phones — measured as devices running Android 7.0 or newer.
- **SC-006**: A user who installed via the side-load APK can install the Google Play Store edition on top of it (or vice-versa) without losing their server configuration, cached session, or biometric binding.
- **SC-007**: A license downgrade applied on the server is reflected in the native shell within sixty seconds of the next app foreground event.
- **SC-008**: A user who denies camera or notification permission can re-grant the permission via in-app prompts and reach a working state without uninstalling the app.

## Assumptions

- The customer's DaftarX server runs the v5.1 (or newer) build with edition-gating wired into the web UI; the Android app is a thin native shell that defers all business logic to the server.
- The device has network connectivity to the server's host — typically the same LAN as the desktop install, optionally extended via the customer's VPN or a publicly-reachable HTTPS gateway.
- The on-prem server is reachable over plain HTTP on the LAN (the existing default bind); the network security configuration of the app marks the user-configured host as cleartext-allowed.
- Customers running the server over public HTTPS use a certificate signed by a CA that ships in the standard Android trust store; self-signed certificates are out of scope for v1.
- A push-notification publishing channel is provisioned for the DaftarX vendor; the server runs the publisher process and the app receives notifications through the platform's standard push delivery. End-to-end delivery latency is best-effort and bounded by the push transport's own SLA; the spec's measurable latency budget (FR-005's "60 seconds") applies to the *server-side* hand-off, not on-device receipt.
- Server-side API endpoints required by this app — receipt scan, features snapshot, notification registration, deep-link routing — either already exist or are added by the server team in lock-step with the Android release; this spec does not enumerate their wire formats.
- A single user has a single Android device registered for notifications in v1; multi-device fan-out is a follow-up.
- One configured server per app install in v1; users who need to switch between two installations (e.g., accountants serving multiple clients) will be supported in a later release.
- The signing key for both distribution channels is held by the vendor and rotated only on a planned schedule; release engineering supports both channels from a single build pipeline.
- Tablet, foldable, Wear OS, Android Auto, and Chromecast form factors are explicitly out of scope for v1; the app is built and tested for phone-only.
- Native invoice, expense, journal, and other editors stay inside the in-app web view; this app does not duplicate any business-logic UI natively.
- Offline-first behaviour (queueing operations against a local database when the server is unreachable) is out of scope for v1; the app shows a retry screen and waits for connectivity.
- Crash telemetry is collected via a vendor-owned crash-reporting service (shared with the same vendor account that hosts the push channel) and is opt-out at the device level via the OS's "Reset advertising ID" / app data controls; explicit in-app opt-out is out of scope for v1.
- The customer (the business operating the DaftarX server) is the data controller for all business records, receipts, and login credentials; the app vendor is a data processor only for crash diagnostics and the push device token. The vendor publishes a privacy policy covering only its own collection; the customer is responsible for surfacing their own policy to the app's end users (e.g. via the existing in-server settings screens).
