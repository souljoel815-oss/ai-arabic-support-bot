# Implementation Plan: DaftarX Android App

**Branch**: `009-android-app` | **Date**: 2026-05-17 (refreshed after `/speckit-clarify`) | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/009-android-app/spec.md`

## Summary

Ship a thin native Android shell that hosts the existing DaftarX server UI in an in-app web view, augmented with five native capabilities the browser can't deliver well: biometric session unlock, native-camera receipt OCR, system share-target intake, push notifications, and OS-level deep links. The shell defers all business logic to the server (no native invoice/expense editors) so it inherits v5.1 edition gating, RTL/i18n, and feature releases for free.

Three small server endpoints land in lock-step (feature snapshot — extended in this revision to carry server version, minimum supported app version, and latest published app version per FR-019/FR-020; push-token registration; mobile-tuned receipt-scan), plus a Firebase Cloud Messaging publisher that fans out the four notification categories at lockscreen-private visibility (FR-021). Crash telemetry via Firebase Crashlytics (FR-017) reuses the same Firebase project as FCM. The Play Store listing's Data Safety declaration covers only the vendor-controlled data flows (FR-018) — customer-server data flows are governed by the customer's own privacy policy.

Distribution is dual-channel — side-loaded APK and Google Play — from a single signed build, with a notification-only "new version available" banner (FR-020) keeping side-load installs informed without requiring `REQUEST_INSTALL_PACKAGES`.

## Technical Context

**Language/Version**: Kotlin 2.0.x; Java 17 toolchain; Android Gradle Plugin 8.5+.
**Primary Dependencies**:
- *Android side*: Jetpack Compose (UI), AndroidX WebKit (managed WebView), AndroidX Biometric, CameraX, Firebase Cloud Messaging Android SDK, **Firebase Crashlytics Android SDK** (new — FR-017), AndroidX Security Crypto (encrypted SharedPreferences), Coroutines + Flow, Retrofit + Moshi (small HTTP/JSON layer to the server), Hilt (DI), AndroidX AppCompat (per-app locale).
- *Server side*: existing `EgyptTax.Web` (ASP.NET Core 8 + Blazor Server) plus the FirebaseAdmin .NET SDK for the FCM publisher. The features-snapshot endpoint reads the assembly's `AssemblyInformationalVersion` to report `serverVersion` (FR-019), and reads two app-config keys for `minAppVersion` and `latestKnownAppVersion` (FR-019/FR-020).
**Storage**:
- *Android*: `EncryptedSharedPreferences` for server URL + biometric flag + idle-timeout setting; Android Keystore-wrapped AES key for the cached session credential; no local relational database in v1 (offline-first sync explicitly out of scope per spec). Crash buffer is in-memory (Crashlytics' own queue) — not persisted by us.
- *Server*: existing SQLite store; one new table `mobile_push_registrations` for FCM tokens scoped per (user, device).
**Testing**: JUnit 5 + MockK + Robolectric for the Android unit tier; Espresso + Compose UI tests for instrumented tests; Maestro for end-to-end shell flows (server-config → biometric → dashboard, plus version-gate and update-banner scenarios). On the server: existing xUnit harness extended with three new contract tests for the new endpoints (now including version-field assertions) and one integration test wiring the FCM publisher against a fake transport.
**Target Platform**: Android phones, `minSdk = 24` (Android 7.0 Nougat — ~98% device coverage), `targetSdk = 34` (Android 14 — Play Store requirement for new uploads in 2026). Phone form-factor only; tablet, fold, Auto, Wear, and Chromecast are out of scope per the spec.
**Project Type**: Mobile + API (Option 3) — new `android/` Gradle project at repo root, augmented by three new HTTP endpoints + an FCM publisher + a Crashlytics publishing pipeline inside the existing `src/EgyptTax.Web/`. Privacy policy authored as a static page hosted by the vendor (one task in the eventual `tasks.md`).
**Performance Goals**: Cold start → dashboard ≤ 5 s on a Pixel 4a-class device (SC-002); native receipt capture → row visible in `/scan-history` ≤ 10 s end-to-end (SC-003); push-tap → correct deep-linked page ≥ 95% of the time (SC-004); APK install size ≤ 25 MB (SC-005 derived target — Crashlytics adds ~200 KB to the APK; well within budget).
**Constraints**: Cleartext HTTP allowed only for the user-configured server host (network-security-config built dynamically at runtime); HTTPS required for any other origin. Single configured server per install (multi-account explicitly out of scope). The shell never executes business logic locally. Push notifications MUST set `Notification.VISIBILITY_PRIVATE` (FR-021); the public-form text is a redacted summary like `"3 إشعارات بانتظار المراجعة"`. Crash reports MUST NOT include any DOM content, server URLs, session credentials, FCM tokens, or captured receipt content (FR-017 — enforced by a custom `Crashlytics` keys allowlist + a unit test that fakes the WebView and asserts no DOM string ever reaches the recorder). The spec's "in-app web view" terminology is implemented as Android's `android.webkit.WebView` via AndroidX WebKit — the two terms refer to the same artifact across spec.md (technology-agnostic) and plan.md/research.md/tasks.md (implementation-facing).
**Scale/Scope**: Roughly twelve native shell screens (splash, server-config, server-probe, login-bridge, lock, biometric-prompt, settings, scan-capture, scan-confirm, error/retry, license-required, share-receive, **plus two new from FR-019/FR-020**: app-version-too-old and server-version-too-old). The rest of the app surface (≈146 routable pages) lives inside the web view and inherits the v5.1 gating already built in `008-egypt-tax-accounting`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Spec-First Development** | ✅ Pass | `spec.md` complete with **21** functional requirements (16 from initial spec + 5 from `/speckit-clarify` session 2026-05-17), 5 prioritised user stories, 8 measurable success criteria, 0 `[NEEDS CLARIFICATION]` markers. All ambiguous points resolved into Assumptions or the new Clarifications section. |
| **II. Plan Before Code** | ✅ Pass | This plan documents language, dependencies, storage, testing, project structure, and target platform — refreshed to integrate FR-017 through FR-021. No code may be written until tasks are generated from this plan. |
| **III. Test-First Discipline** | ✅ Pass (commitment) | Contract tests for the three new server endpoints (now including the three new version fields on `/api/v1/me/features` per FR-019/FR-020), plus integration tests for: server-config save+probe, biometric unlock, camera capture→upload, share-intent receive, deep-link routing, license-snapshot refresh, version-gate screens (both directions), update-banner banner, lockscreen-private notification visibility, and Crashlytics-no-PII guard. All to be written and observed failing before their production code, recorded in `tasks.md` per `/speckit-tasks`. |
| **IV. Simplicity & YAGNI** | ✅ Pass | Every dependency is off-the-shelf AndroidX/Jetpack/Firebase. The "WebView + native shell" architecture remains the simplest design satisfying the spec. The new clarifications add only standard mechanisms (Crashlytics out of the box, `VISIBILITY_PRIVATE` is a single setter call, version fields piggyback on an already-needed endpoint, the update banner uses no new permission). No Complexity Tracking entries needed. |
| **V. Incremental, Independently Testable Delivery** | ✅ Pass | Spec still decomposes into the same five user stories (P1–P3) with the clarifications layered orthogonally — Crashlytics + Data Safety + version negotiation + update banner + notification visibility cut across stories and do not introduce a new MVP-blocking dependency. P1 (pocket DaftarX + biometric unlock) is still independently shippable; the new requirements add small amounts of work inside each story group. |

**Re-check after Phase 1 design**: see [Post-Design Constitution Check](#post-design-constitution-check) below.

## Project Structure

### Documentation (this feature)

```text
specs/009-android-app/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   ├── me-features.md           # GET /api/v1/me/features (now with serverVersion, minAppVersion, latestKnownAppVersion)
│   ├── notifications-register.md # POST /api/v1/notifications/register
│   ├── scan-receipt-mobile.md   # POST /api/v1/ai/scan-receipt-mobile
│   └── deep-links.md            # daftarx:// + https://<server>/* URI grammar
├── checklists/
│   └── requirements.md  # spec-quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
android/                                         # NEW: Kotlin/Compose mobile shell
├── app/
│   ├── src/main/
│   │   ├── kotlin/com/daftarx/mobile/
│   │   │   ├── DaftarXApp.kt                   # Application + Hilt entry + Crashlytics bootstrap (FR-017)
│   │   │   ├── MainActivity.kt                 # Single-activity host + nav
│   │   │   ├── shell/                          # Native Compose screens
│   │   │   │   ├── SetupScreen.kt              # First-run server URL
│   │   │   │   ├── LockScreen.kt               # Idle re-lock UI
│   │   │   │   ├── BiometricPrompt.kt          # Biometric/PIN gate
│   │   │   │   ├── SettingsScreen.kt           # Reset server, idle timeout, screenshot block
│   │   │   │   ├── ErrorScreen.kt              # Server unreachable
│   │   │   │   ├── ScanResultScreen.kt         # Post-capture confirmation
│   │   │   │   ├── AppOutdatedScreen.kt        # NEW (FR-019): app version below server's minimum
│   │   │   │   ├── ServerOutdatedScreen.kt     # NEW (FR-019): server version below app's minimum
│   │   │   │   └── UpdateAvailableBanner.kt    # NEW (FR-020): non-blocking "new version" banner
│   │   │   ├── web/                            # WebView host + bridge
│   │   │   │   ├── DaftarXWebView.kt
│   │   │   │   ├── DaftarXWebViewClient.kt     # URL whitelist, deep-link interception, scrubs WebView frames from Crashlytics
│   │   │   │   └── DeepLinkRouter.kt           # daftarx://invoice/<id> → web route
│   │   │   ├── auth/                           # Biometric + session vault
│   │   │   │   ├── BiometricGate.kt
│   │   │   │   ├── SessionVault.kt             # Keystore-backed cookie cache
│   │   │   │   └── IdleTimer.kt
│   │   │   ├── config/                         # Server URL persistence
│   │   │   │   ├── ServerConfig.kt
│   │   │   │   └── ServerProbe.kt              # Connectivity check on save
│   │   │   ├── camera/                         # CameraX wrapper
│   │   │   │   ├── ReceiptCapture.kt
│   │   │   │   └── ReceiptUploader.kt
│   │   │   ├── share/                          # System share-target
│   │   │   │   ├── ShareReceiverActivity.kt
│   │   │   │   └── PendingShareQueue.kt        # Queue when not authenticated
│   │   │   ├── push/                           # FCM wiring
│   │   │   │   ├── DaftarXMessagingService.kt  # Sets VISIBILITY_PRIVATE (FR-021)
│   │   │   │   ├── NotificationRouter.kt       # tap → deep link
│   │   │   │   ├── NotificationContent.kt      # NEW: split full + redacted summaries (FR-021)
│   │   │   │   └── TokenRegistration.kt
│   │   │   ├── license/                        # Feature snapshot polling + version gate
│   │   │   │   ├── LicenseSnapshot.kt          # Carries 3 new fields per FR-019/FR-020
│   │   │   │   ├── FeaturePoller.kt
│   │   │   │   └── VersionGate.kt              # NEW (FR-019): decides which screen to show
│   │   │   ├── telemetry/                      # NEW (FR-017): crash + non-fatal reporting
│   │   │   │   ├── CrashlyticsBootstrap.kt
│   │   │   │   └── CrashScrubber.kt            # Allowlist of keys; strips DOM / URLs / tokens
│   │   │   ├── net/                            # HTTP layer (Retrofit + Moshi)
│   │   │   │   ├── DaftarXApi.kt
│   │   │   │   └── HttpClientFactory.kt        # Per-host cleartext rules
│   │   │   └── ui/theme/                       # Mission Control gold/charcoal
│   │   ├── res/
│   │   │   ├── values/strings.xml              # en defaults (includes 8 new strings for version + banner screens)
│   │   │   ├── values-ar/strings.xml           # Arabic + RTL
│   │   │   └── xml/network_security_config.xml # base config (dynamic at runtime)
│   │   └── AndroidManifest.xml                 # Adds Crashlytics + FCM service declarations
│   ├── src/test/                               # JUnit 5 + MockK + Robolectric
│   ├── src/androidTest/                        # Espresso + Compose UI
│   └── build.gradle.kts                        # Adds com.google.firebase:firebase-crashlytics
├── settings.gradle.kts
├── build.gradle.kts                            # Applies com.google.firebase.crashlytics plugin
├── gradle/libs.versions.toml                   # Version catalog
└── README.md                                   # see specs/009-android-app/quickstart.md

src/EgyptTax.Web/                               # EXISTING: server companion
├── Pages/Api/Mobile/                           # NEW endpoint group
│   ├── MeFeaturesEndpoint.cs                   # GET /api/v1/me/features (with serverVersion + minAppVersion + latestKnownAppVersion)
│   ├── NotificationRegisterEndpoint.cs         # POST /api/v1/notifications/register
│   └── ScanReceiptMobileEndpoint.cs            # POST /api/v1/ai/scan-receipt-mobile
├── Notifications/                              # NEW: FCM publisher subsystem
│   ├── FcmPublisher.cs                         # FirebaseAdmin wrapper; emits VISIBILITY_PRIVATE-friendly payloads
│   ├── ApprovalNotifier.cs
│   ├── DeadlineNotifier.cs
│   ├── EtaFailureNotifier.cs
│   ├── LicenseExpiryNotifier.cs
│   └── MobilePushRegistration.cs               # entity for the new table
├── Mobile/                                     # NEW: mobile-version-config service
│   └── MobileVersionConfig.cs                  # reads minSupportedAppVersion + latestKnownAppVersion from appsettings
└── (existing structure unchanged)

src/EgyptTax.Infrastructure/Migrations/         # EXISTING: EF migrations
└── <timestamp>_MobilePushRegistrations.cs      # NEW migration adds one table

tests/EgyptTax.UnitTests/Web/Mobile/            # NEW: server-side contract tests
├── MeFeaturesEndpointTests.cs                  # 5 base + 3 new version-field tests
├── NotificationRegisterEndpointTests.cs
└── ScanReceiptMobileEndpointTests.cs

privacy/                                        # NEW: vendor privacy policy source
└── daftarx-android-privacy-policy.md           # Linked from the Play Store Data Safety form (FR-018)
```

**Structure Decision**: Adopted **Option 3 (Mobile + API)**. The Android module lives at the repo root under `android/`; the server companion stays inside `src/EgyptTax.Web/` to inherit the existing licensing, auth, audit-log, and DI. The clarifications from session 2026-05-17 add: a `telemetry/` package on the Android side (Crashlytics bootstrap + scrubber per FR-017), three new Compose screens for version/update UX (FR-019/FR-020), a `NotificationContent` split for lockscreen-private rendering (FR-021), a `Mobile/MobileVersionConfig.cs` service on the server (FR-019/FR-020), and a vendor-hosted privacy policy source under `privacy/` (FR-018).

## Complexity Tracking

> No constitutional violations to record. The clarifications added five new requirements (FR-017 through FR-021), but each maps to standard off-the-shelf mechanisms (Crashlytics SDK, version-string compare, `Notification.Builder.setVisibility`, a static field in an existing endpoint, a hosted privacy-policy page). No abstractions, fallbacks, or feature flags introduced for hypothetical future requirements.

### Test-First Exception: Third-Party Push Transport

Constitution III (Test-First Discipline) requires contract tests for every external interface boundary. The server→FCM HTTP v1 transport is an exception: it is a third-party API whose contract is owned by Google, not the vendor, so a contract test against the real FCM endpoint would (a) be brittle to Google's evolving wire format, (b) require live network access in CI, and (c) test Google's compliance rather than ours. The `FcmPublisher` is instead covered by a fake-transport test (T067) that asserts *our* publishing logic produces correct `VISIBILITY_PRIVATE` payloads, payload shape, and per-user fan-out. Drift detection on the FCM side relies on Google's published changelogs + vendor monitoring of delivery success rates, not a CI contract test.

## Post-Design Constitution Check

*Re-evaluated after Phase 1 design completed (see `data-model.md`, `contracts/`, `quickstart.md`).*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Spec-First Development** | ✅ Pass | Phase 1 artifacts cite `spec.md` (now with 21 FRs) for every entity, contract, and quickstart step. No new requirements introduced during design. |
| **II. Plan Before Code** | ✅ Pass | Plan, data-model, and contracts are the complete pre-code package, refreshed to reflect the five clarifications. `/speckit-tasks` consumes these next. |
| **III. Test-First Discipline** | ✅ Pass | The refreshed `contracts/me-features.md` defines explicit assertions for `serverVersion`, `minAppVersion`, `latestKnownAppVersion`; tasks generated from this plan will produce a failing contract test for each before its endpoint code. Native tests will include the Crashlytics-no-PII guard and the `VISIBILITY_PRIVATE` assertion. |
| **IV. Simplicity & YAGNI** | ✅ Pass | Data model is five entities. `LicenseFeatureSnapshot` gained three primitive fields; nothing else expanded. One new server table (`mobile_push_registrations`); no new aggregates in the existing domain layer. |
| **V. Incremental, Independently Testable Delivery** | ✅ Pass | The quickstart documents the P1 MVP slice end-to-end without dependencies on P2/P3 stories. Crashlytics + version negotiation + update banner work even on the P1 slice. |
