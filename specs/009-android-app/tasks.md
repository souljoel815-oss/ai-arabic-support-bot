---

description: "Task list for 009-android-app feature implementation"
---

# Tasks: DaftarX Android App

**Input**: Design documents from `/specs/009-android-app/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Test tasks are INCLUDED. Constitution III (Test-First Discipline) is non-negotiable and the plan commits to contract tests for every new server endpoint plus integration tests for every cross-boundary native flow. Tests MUST be written and observed failing before their production code lands.

**Organization**: Tasks are grouped by user story (US1–US5) to enable independent implementation and testing. P1 (US1) alone constitutes a shippable MVP per `plan.md`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5); omitted for Setup, Foundational, and Polish phases
- All file paths are project-relative

## Path Conventions (from plan.md)

- **Android module**: `android/app/src/main/kotlin/com/daftarx/mobile/`
- **Android unit tests**: `android/app/src/test/kotlin/com/daftarx/mobile/`
- **Android instrumented tests**: `android/app/src/androidTest/kotlin/com/daftarx/mobile/`
- **Server companion**: `src/EgyptTax.Web/` (new endpoints under `Pages/Api/Mobile/`, notifiers under `Notifications/`)
- **Server tests**: `tests/EgyptTax.UnitTests/Web/Mobile/`
- **EF Core migrations**: `src/EgyptTax.Infrastructure/Migrations/`
- **Vendor privacy + signing docs**: `privacy/`
- **Maestro flows**: `android/maestro/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Stand up the empty `android/` Gradle module + version catalog + signing scaffolding. Server side stays untouched in this phase.

- [X] T001 [P] Create `android/` Gradle wrapper + `settings.gradle.kts` + root `build.gradle.kts` pinning Kotlin 2.0, AGP 8.5, Java 17 toolchain per plan.md Technical Context
- [X] T002 [P] Create version catalog at `android/gradle/libs.versions.toml` with sections for AndroidX, Compose, Firebase, Hilt, Retrofit, test deps
- [X] T003 [P] Add AndroidX deps to `android/gradle/libs.versions.toml`: `androidx.webkit`, `androidx.biometric`, `androidx.camera:camera-core/camera2/lifecycle/view`, `androidx.security:security-crypto`, `androidx.appcompat`, `androidx.lifecycle:lifecycle-runtime-ktx`, `androidx.lifecycle:lifecycle-process` per research §1–§7, §12
- [X] T004 [P] Add Compose BOM + UI + Material3 + Tooling + UI-Test deps to `android/gradle/libs.versions.toml`
- [X] T005 [P] Add Firebase BoM + `firebase-messaging` + `firebase-crashlytics` + the `com.google.firebase.crashlytics` Gradle plugin to `android/gradle/libs.versions.toml` per research §8, §16
- [X] T006 [P] Add Hilt (`hilt-android`, `hilt-compiler`) + KSP plugin to `android/gradle/libs.versions.toml`
- [X] T007 [P] Add Retrofit + Moshi + OkHttp + Moshi-Kotlin-Codegen to `android/gradle/libs.versions.toml`
- [X] T008 [P] Add JUnit 5 + MockK + Robolectric + Espresso + Compose UI Test deps (test scope) to `android/gradle/libs.versions.toml` per research §14
- [X] T009 Create `android/app/build.gradle.kts` applying com.android.application + kotlin-android + ksp + hilt + google-services + firebase-crashlytics plugins; consume all version-catalog deps; configure `minSdk=24`, `targetSdk=34`, `versionCode=1`, `versionName="1.0.0"`
- [X] T010 [P] Create `android/app/src/main/AndroidManifest.xml` skeleton with `<application>`, `android:supportsRtl="true"`, single `<activity android:name=".MainActivity">` with the launcher intent-filter
- [X] T011 [P] Create `android/app/src/main/res/xml/network_security_config.xml` denying cleartext by default per research §2; reference it from `<application android:networkSecurityConfig>`
- [X] T012 [P] Create `android/app/proguard-rules.pro` keeping Compose APIs, WebView interfaces, and Crashlytics-required classes
- [X] T013 [P] Configure signing in `android/app/build.gradle.kts`: debug uses default debug keystore; release reads from `signing.properties` (gitignored) — document the dual-channel Play App Signing flow per research §11 in `android/SIGNING.md`
- [X] T014 [P] Create `android/.gitignore` covering `build/`, `local.properties`, `signing.properties`, `google-services.json`, `.kotlin/`, `.gradle/`
- [X] T015 [P] Create Mission Control theme at `android/app/src/main/kotlin/com/daftarx/mobile/ui/theme/Theme.kt` (Material3 ColorScheme with gold + charcoal swatches matching the existing web)
- [X] T016 [P] Create empty Maestro flow file `android/maestro/p1-mvp-flow.yaml` (filled in T046)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Hilt + Crashlytics + HTTP client + the server-side push-registration table. Every user story depends on these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T017 Bootstrap `DaftarXApp` at `android/app/src/main/kotlin/com/daftarx/mobile/DaftarXApp.kt` (Application + `@HiltAndroidApp` + Crashlytics init + log-tag setup per FR-017)
- [X] T018 Register `DaftarXApp` in `android/app/src/main/AndroidManifest.xml` as `android:name=".DaftarXApp"`
- [X] T019 Create `MainActivity` at `android/app/src/main/kotlin/com/daftarx/mobile/MainActivity.kt` (single-activity host, `@AndroidEntryPoint`, Compose `setContent` with placeholder nav scaffold)
- [X] T020 [P] Write unit test for `CrashScrubber` at `android/app/src/test/kotlin/com/daftarx/mobile/telemetry/CrashScrubberTest.kt` — asserts that any key outside the allowlist (`appVersion`, `locale`, `idleTimeoutSec`, `serverHostHash`) is rejected (per FR-017). Test MUST fail before T021 lands.
- [X] T021 [P] Implement `CrashScrubber` at `android/app/src/main/kotlin/com/daftarx/mobile/telemetry/CrashScrubber.kt` (allowlist gate over `FirebaseCrashlytics.setCustomKey`)
- [X] T022 [P] Implement `CrashlyticsBootstrap` at `android/app/src/main/kotlin/com/daftarx/mobile/telemetry/CrashlyticsBootstrap.kt` (configures Crashlytics with the scrubber and the app's locale/version keys)
- [X] T023 [P] Implement `HttpClientFactory` at `android/app/src/main/kotlin/com/daftarx/mobile/net/HttpClientFactory.kt` (OkHttp builder + per-host cleartext interceptor per research §2 + persistent `CookieJar` backed by `SessionVault`)
- [X] T024 [P] Define `DaftarXApi` Retrofit interface at `android/app/src/main/kotlin/com/daftarx/mobile/net/DaftarXApi.kt` with stubs for `GET /api/v1/me/features`, `POST/DELETE /api/v1/notifications/register`, `POST /api/v1/ai/scan-receipt-mobile`
- [X] T025 [P] Server-side: scaffold EF Core migration at `src/EgyptTax.Infrastructure/Migrations/<timestamp>_MobilePushRegistrations.cs` per data-model §5 (table + composite unique index on `(user_id, device_id)` filtered on `revoked_at_utc IS NULL`)
- [X] T026 [P] Server-side: add `MobilePushRegistration` EF entity at `src/EgyptTax.Infrastructure/Persistence/Entities/MobilePushRegistration.cs` and register in `AppDbContext.cs` `OnModelCreating` per data-model §5
- [X] T027 [P] Server-side: add `MobileVersionConfig.cs` at `src/EgyptTax.Web/Mobile/MobileVersionConfig.cs` (binds `IOptionsMonitor<MobileVersionConfig>` to appsettings section with reload-on-change per research §17)
- [X] T028 Server-side: append `MobileVersionConfig` block to `src/EgyptTax.Web/appsettings.json` with `MinAppVersion="1.0.0"` and `LatestKnownAppVersion="1.0.0"`
- [X] T029 Server-side: register `MobileVersionConfig` in `src/EgyptTax.Web/Program.cs` DI via `builder.Services.Configure<MobileVersionConfig>(builder.Configuration.GetSection("MobileVersionConfig"))`

**Checkpoint**: Foundation ready — Crashlytics fires, HTTP client knows the per-host cleartext policy, the new push-registration table exists, and the server's version-config endpoint backbone is wired. User stories can begin.

---

## Phase 3: User Story 1 - Pocket DaftarX (Priority: P1) 🎯 MVP

**Goal**: New user installs app, configures server URL on first run, signs in once, enables biometric unlock, then re-enters via biometric on subsequent cold starts. WebView hosts the full DaftarX dashboard.

**Independent Test**: From a clean device install, complete Setup → server probe → sign in via WebView → enable biometric → force-stop → re-launch → biometric prompt → land on dashboard (no Setup, no password retype). Maestro flow `p1-mvp-flow.yaml` covers this end-to-end.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T030 [P] [US1] Contract test for `ServerConfig` at `android/app/src/test/kotlin/com/daftarx/mobile/config/ServerConfigTest.kt` (save → reload round-trip, reject blank host / port out of range / control chars, wipe on reset per data-model §1)
- [ ] T031 [P] [US1] Contract test for `SessionVault` at `android/app/src/test/kotlin/com/daftarx/mobile/auth/SessionVaultTest.kt` (encrypt+store cookie blob, decrypt only after biometric proof, handle `KeyPermanentlyInvalidatedException`, wipe on reset per data-model §2)
- [ ] T032 [P] [US1] Compose UI test for `SetupScreen` at `android/app/src/androidTest/kotlin/com/daftarx/mobile/shell/SetupScreenTest.kt` (renders host + port fields, validates input, fires `onConnect` callback)
- [ ] T033 [US1] Integration test for full Setup flow at `android/app/src/androidTest/kotlin/com/daftarx/mobile/SetupFlowTest.kt` (Espresso: clean state → SetupScreen → probe success → WebView visible)
- [ ] T034 [US1] Integration test for biometric unlock at `android/app/src/androidTest/kotlin/com/daftarx/mobile/BiometricUnlockTest.kt` (cold restart with vault populated → biometric prompt → cookie injected → WebView resumes on dashboard URL)
- [ ] T035 [US1] Integration test for idle re-lock at `android/app/src/androidTest/kotlin/com/daftarx/mobile/IdleReLockTest.kt` (foreground app → background > 5 min via clock advance → re-foreground → LockScreen visible per FR-010)

### Implementation for User Story 1

- [ ] T036 [P] [US1] Implement `ServerConfig` at `android/app/src/main/kotlin/com/daftarx/mobile/config/ServerConfig.kt` (`EncryptedSharedPreferences` file `server_config.prefs` per data-model §1)
- [ ] T037 [P] [US1] Implement `ServerProbe` at `android/app/src/main/kotlin/com/daftarx/mobile/config/ServerProbe.kt` (HEAD request with 5 s timeout returning `Ok`/`Unreachable`/`Malformed` per data-model §1 lifecycle)
- [ ] T038 [P] [US1] Implement `SessionVault` at `android/app/src/main/kotlin/com/daftarx/mobile/auth/SessionVault.kt` (`MasterKey` + separate `session_vault.prefs` per data-model §2 + research §4)
- [ ] T039 [P] [US1] Implement `BiometricGate` at `android/app/src/main/kotlin/com/daftarx/mobile/auth/BiometricGate.kt` (`BiometricPrompt` with `BIOMETRIC_STRONG | DEVICE_CREDENTIAL` per research §5)
- [ ] T040 [P] [US1] Implement `IdleTimer` at `android/app/src/main/kotlin/com/daftarx/mobile/auth/IdleTimer.kt` (`ProcessLifecycleOwner` observer reading `EncryptedSharedPreferences.idle_timeout_seconds`, defaults to 300 per FR-010 + research §12)
- [ ] T041 [P] [US1] Implement `DaftarXWebView` Compose wrapper at `android/app/src/main/kotlin/com/daftarx/mobile/web/DaftarXWebView.kt` (`AndroidView` over `WebView` with JS, DOM storage, third-party cookies, `setForceDark` per research §1)
- [ ] T042 [P] [US1] Implement `DaftarXWebViewClient` at `android/app/src/main/kotlin/com/daftarx/mobile/web/DaftarXWebViewClient.kt` (URL whitelist for configured host, `shouldOverrideUrlLoading` reject for any non-configured-host cleartext per research §2; injects `CrashScrubber.markWebViewFrame` to keep DOM strings out of Crashlytics)
- [ ] T043 [P] [US1] Compose UI: `SetupScreen` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/SetupScreen.kt` (bilingual ar/en, host + port inputs, Connect button, error toast)
- [ ] T044 [P] [US1] Compose UI: `BiometricPrompt` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/BiometricPrompt.kt` (renders signed-in email from `CachedSession.userEmail` + biometric trigger)
- [ ] T045 [P] [US1] Compose UI: `LockScreen` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/LockScreen.kt` (shown on idle re-lock, single CTA → biometric)
- [ ] T046 [P] [US1] Compose UI: `ErrorScreen` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/ErrorScreen.kt` (FR-015: shows configured URL + Try again + Reconfigure)
- [ ] T047 [P] [US1] Compose UI: `SettingsScreen` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/SettingsScreen.kt` (reset server per FR-014; screenshot block toggle per FR-009; idle timeout slider per FR-010)
- [ ] T048 [US1] Wire `MainActivity` navigation: on launch, branch on `ServerConfig` presence → SetupScreen or LockScreen/BiometricPrompt → DaftarXWebView (deps T036, T038, T041)
- [ ] T049 [US1] Implement `FLAG_SECURE` toggle plumbing in `MainActivity` reading the screenshot-block setting (FR-009). The WebView's current URL is watched; `FLAG_SECURE` is applied when the URL matches any of: `/login`, `/reports/*`, `/tax/*`, `/audit-log`, `/penalty-shield`, `/compliance/health`, `/settings?tab=license`. URL patterns live as a single static list in `web/SecureUrlMatcher.kt` so the allowlist is easy to extend.
- [ ] T050 [P] [US1] Per-app locale via `AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags("ar-EG"))` on first launch with a settings toggle (FR-011 + research §13)
- [ ] T051 [P] [US1] String resources at `android/app/src/main/res/values/strings.xml` (English) + `android/app/src/main/res/values-ar/strings.xml` (Arabic, primary) for every US1 UI element
- [ ] T052 [US1] Author Maestro flow at `android/maestro/p1-mvp-flow.yaml` driving the independent test scenario above. The flow MUST assert SC-001 (cold-install → biometric-enabled → dashboard ≤ 180 s wall-clock) and SC-002 (force-stop → biometric → dashboard ≤ 5 s wall-clock) via Maestro's `assertVisible` timestamp deltas; flow fails if either budget is exceeded.

**Checkpoint**: User Story 1 fully functional. The app is shippable as a P1 MVP at this point. Stop here and demo / get user feedback before continuing.

---

## Phase 4: User Story 2 - Sales rep captures receipt (Priority: P2)

**Goal**: When the active license includes `receipt_ocr`, a native Scan Receipt action appears. Tapping it opens the device camera; capture → POST to `/api/v1/ai/scan-receipt-mobile` → row appears in `/scan-history` on the desktop within 10 s.

**Independent Test**: Sign in to an SMB+ install → confirm Scan Receipt action visible → capture → verify upload confirmation → load `/scan-history` in the WebView → matching row present.

### Tests for User Story 2

- [ ] T053 [P] [US2] Server contract test at `tests/EgyptTax.UnitTests/Web/Mobile/ScanReceiptMobileEndpointTests.cs` — 10 assertions per [contracts/scan-receipt-mobile.md](./contracts/scan-receipt-mobile.md) (anon 401, Solo 403, valid 202, PDF 202, image/webp 415, 9 MB 413, missing device id 400, bad capture source 400, row persists with device-id + source, audit log written)
- [ ] T054 [P] [US2] Android contract test at `android/app/src/test/kotlin/com/daftarx/mobile/camera/ReceiptUploaderTest.kt` (asserts binary POST with `Content-Type`, `X-DaftarX-Device-Id`, `X-DaftarX-Capture-Source: camera` headers)
- [ ] T055 [US2] Integration test at `android/app/src/androidTest/kotlin/com/daftarx/mobile/ReceiptCaptureFlowTest.kt` (mock CameraX → upload → success state surfaced)

### Implementation for User Story 2

- [ ] T056 [US2] Server: implement `ScanReceiptMobileEndpoint` at `src/EgyptTax.Web/Pages/Api/Mobile/ScanReceiptMobileEndpoint.cs` (auth check + `EditionGate.Require(Feature.ReceiptOcr)` + content-type / size guards + delegates to existing `OcrReceiptHandler` per contract)
- [ ] T057 [US2] Server: register the new endpoint in `src/EgyptTax.Web/Program.cs` minimal-API map under `/api/v1/ai/scan-receipt-mobile`
- [ ] T058 [P] [US2] Implement `ReceiptCapture` at `android/app/src/main/kotlin/com/daftarx/mobile/camera/ReceiptCapture.kt` (CameraX `ImageCapture` use case per research §6, converts `ImageProxy` → JPEG bytes)
- [ ] T059 [P] [US2] Implement `ReceiptUploader` at `android/app/src/main/kotlin/com/daftarx/mobile/camera/ReceiptUploader.kt` (binary POST via `DaftarXApi.scanReceiptMobile` with `X-DaftarX-Capture-Source: camera`; surfaces 403 → upgrade banner, 503 → retry banner)
- [ ] T060 [P] [US2] Compose UI: capture screen at `android/app/src/main/kotlin/com/daftarx/mobile/shell/ScanCaptureScreen.kt` (CameraX preview + shutter button + cancel)
- [ ] T061 [P] [US2] Compose UI: `ScanResultScreen` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/ScanResultScreen.kt` (post-capture confirmation + "view on scan history" deep link)
- [ ] T062 [US2] Add native floating-action "Scan receipt" entry to the WebView overlay, visibility bound to `LicenseFeatureSnapshot.features.contains("receipt_ocr")` (FR-007); refresh on every poll
- [ ] T063 [US2] Camera permission flow: request on first tap; on denial, route to `ErrorScreen` variant with one-tap OS app-settings button per FR-016
- [ ] T064 [P] [US2] String resources for US2 (Scan Receipt label, permission rationale, confirmation messages, error variants — Arabic + English)
- [ ] T065 [P] [US2] Maestro flow extension at `android/maestro/p2-scan-receipt.yaml`. The flow MUST assert SC-003 (camera-shutter-tap → row visible in `/scan-history` ≤ 10 s wall-clock) via Maestro `assertVisible` timestamp deltas against the WebView's rendered history table.

**Checkpoint**: User Story 2 fully functional and integrates cleanly with User Story 1.

---

## Phase 5: User Story 3 - Approver gets push notification (Priority: P2)

**Goal**: When a document is submitted requiring the user's approval (or a tax deadline / ETA failure / license expiry triggers), their device receives a push notification within 60 s. Tapping deep-links to the relevant page. Lockscreen previews are redacted per FR-021.

**Independent Test**: Sign in on test device → submit a doc needing the user's approval → confirm notification arrives → tap → land on `/approvals`. Lock device → confirm preview shows only "3 approvals waiting" (no vendor name / amount).

### Tests for User Story 3

- [ ] T066 [P] [US3] Server contract test at `tests/EgyptTax.UnitTests/Web/Mobile/NotificationRegisterEndpointTests.cs` — 8 assertions per [contracts/notifications-register.md](./contracts/notifications-register.md) (first POST 201, refresh POST 200, invalid device-id 400, anon 401, DELETE 204, idempotent DELETE 204, re-activation on POST after DELETE, audit log NEVER contains fcm_token)
- [ ] T067 [P] [US3] Server test for `FcmPublisher` at `tests/EgyptTax.UnitTests/Web/Mobile/FcmPublisherTests.cs` (fake `FirebaseMessagingClient`; verify payload carries `notification.visibility=private` + redacted summary text per FR-021; verify revoked rows are skipped)
- [ ] T068 [P] [US3] Android contract test for `NotificationContent` at `android/app/src/test/kotlin/com/daftarx/mobile/push/NotificationContentTest.kt` (asserts every category builds a notification with `setVisibility(VISIBILITY_PRIVATE)` and a `setPublicVersion` redacted-summary builder per FR-021)
- [ ] T069 [P] [US3] Android contract test for `DeepLinkRouter` at `android/app/src/androidTest/kotlin/com/daftarx/mobile/web/DeepLinkRouterTest.kt` — 7 assertions per [contracts/deep-links.md](./contracts/deep-links.md) (each known URI → web path, unknown → home, pending through biometric, in-session direct nav, 5-min expiry, payload type mapping, App Link verification status check)
- [ ] T070 [US3] Integration test at `android/app/src/androidTest/kotlin/com/daftarx/mobile/PushNotificationFlowTest.kt` (inject fake FCM payload via `FirebaseMessagingService.onMessageReceived` shim → notification posted with private visibility → tap → DeepLinkRouter resolves to `/approvals`)

### Implementation for User Story 3

- [ ] T071 [US3] Server: implement `NotificationRegisterEndpoint` at `src/EgyptTax.Web/Pages/Api/Mobile/NotificationRegisterEndpoint.cs` (POST upsert + DELETE soft-delete + audit-log writes per contract, never logs `fcm_token`)
- [ ] T072 [US3] Server: register the endpoint at `src/EgyptTax.Web/Program.cs` under `/api/v1/notifications/register`
- [ ] T073 [P] [US3] Server: implement `FcmPublisher` at `src/EgyptTax.Web/Notifications/FcmPublisher.cs` (FirebaseAdmin wrapper, queries active registrations, emits `VISIBILITY_PRIVATE` payloads per FR-021)
- [ ] T074 [P] [US3] Server: implement `ApprovalNotifier` at `src/EgyptTax.Web/Notifications/ApprovalNotifier.cs` (subscribes to existing approval-pending domain events → for each event, queries the same visibility logic the existing `/approvals` page uses — i.e. all approver-role users EXCEPT the submitter per FR-004 — and fans the notification out to each visible user's registered devices via `FcmPublisher` with `data.type=approval_pending`). The 60-second budget (FR-005 / US3 scenario 2) is measured from event emission to FCM publish call, not on-device receipt.
- [ ] T075 [P] [US3] Server: implement `DeadlineNotifier` at `src/EgyptTax.Web/Notifications/DeadlineNotifier.cs` (Hangfire recurring job: T-7 + T-1 from existing compliance calendar)
- [ ] T076 [P] [US3] Server: implement `EtaFailureNotifier` at `src/EgyptTax.Web/Notifications/EtaFailureNotifier.cs` (hooks ETA submission failure event)
- [ ] T077 [P] [US3] Server: implement `LicenseExpiryNotifier` at `src/EgyptTax.Web/Notifications/LicenseExpiryNotifier.cs` (Hangfire recurring job: T-30 + T-7 from `LicenseStatus.ExpiresAtUtc`)
- [ ] T078 [US3] Server: register all four notifiers + the `FcmPublisher` in `src/EgyptTax.Web/Program.cs` DI + Hangfire wire-up
- [ ] T079 [P] [US3] Android: implement `TokenRegistration` at `android/app/src/main/kotlin/com/daftarx/mobile/push/TokenRegistration.kt` (POST on sign-in, DELETE on sign-out, refresh on FCM token rotation via `FirebaseMessaging.getToken()`)
- [ ] T080 [P] [US3] Android: implement `NotificationContent` at `android/app/src/main/kotlin/com/daftarx/mobile/push/NotificationContent.kt` (per-category split: full builder + redacted `setPublicVersion` builder per FR-021)
- [ ] T081 [US3] Android: implement `DaftarXMessagingService` at `android/app/src/main/kotlin/com/daftarx/mobile/push/DaftarXMessagingService.kt` (`FirebaseMessagingService` subclass; `onMessageReceived` → `NotificationContent.build` → `NotificationManagerCompat.notify`)
- [ ] T082 [US3] Android: register `DaftarXMessagingService` in `android/app/src/main/AndroidManifest.xml` with the required `<intent-filter>` for `com.google.firebase.MESSAGING_EVENT`
- [ ] T083 [P] [US3] Android: implement `NotificationRouter` at `android/app/src/main/kotlin/com/daftarx/mobile/push/NotificationRouter.kt` (payload `type` → `daftarx://...` URI per [contracts/deep-links.md](./contracts/deep-links.md) "Notification payload mapping")
- [ ] T084 [P] [US3] Android: implement `DeepLinkRouter` at `android/app/src/main/kotlin/com/daftarx/mobile/web/DeepLinkRouter.kt` (custom scheme + App Link handling + pending-destination single-slot per contract)
- [ ] T085 [US3] Android: add `daftarx://` + `https://<server>/*` `<intent-filter>` entries on `MainActivity` in `android/app/src/main/AndroidManifest.xml` (HTTPS uses `android:autoVerify="true"`)
- [ ] T086 [US3] Android: POST_NOTIFICATIONS permission flow on first sign-in (Android 13+); on denial route to `ErrorScreen` variant with OS-settings deep link per FR-016
- [ ] T087 [P] [US3] String resources for US3 (notification titles + bodies per category, redacted summaries — Arabic + English)
- [ ] T088 [P] [US3] Maestro flow extension at `android/maestro/p2-push-notification.yaml`. The flow MUST assert server-side hand-off latency for the SC-004-supporting path (synthetic approval-pending event → server-emitted FCM publish ≤ 60 s wall-clock — measured via a server log scrape Maestro step, since on-device delivery is governed by FCM's best-effort SLA per the spec's Assumptions). Routing-accuracy testing for SC-004's "95% land on intended page" remains in T069 + T070 (contract test + integration test asserts every payload type → URI mapping).

**Checkpoint**: User Story 3 fully functional. P2 stories complete.

---

## Phase 6: User Story 4 - Share-to-DaftarX (Priority: P3)

**Goal**: User shares a PDF/image from another app (WhatsApp, email, Drive); DaftarX appears in the system share sheet; selecting it routes the file through the same `/scan-receipt-mobile` endpoint as the camera path.

**Independent Test**: From any image source app, share an image → pick DaftarX → confirm upload → row appears in `/scan-history`.

### Tests for User Story 4

- [ ] T089 [P] [US4] Android contract test at `android/app/src/androidTest/kotlin/com/daftarx/mobile/share/ShareReceiverActivityTest.kt` (asserts cache-copy of incoming URI, queue when unauthenticated, immediate upload when authenticated)
- [ ] T090 [P] [US4] Android contract test at `android/app/src/test/kotlin/com/daftarx/mobile/share/PendingShareQueueTest.kt` (asserts 1-hour stale drop, single-slot semantics, clear on consume per data-model §3)
- [ ] T091 [US4] Integration test at `android/app/src/androidTest/kotlin/com/daftarx/mobile/ShareIntakeFlowTest.kt` (fires synthetic `ACTION_SEND` intent → upload completes)

### Implementation for User Story 4

- [ ] T092 [P] [US4] Implement `PendingShareQueue` at `android/app/src/main/kotlin/com/daftarx/mobile/share/PendingShareQueue.kt` (`MutableStateFlow<PendingShareIntake?>` singleton with 1-hour expiry on foreground per data-model §3)
- [ ] T093 [US4] Implement `ShareReceiverActivity` at `android/app/src/main/kotlin/com/daftarx/mobile/share/ShareReceiverActivity.kt` (reads incoming URI → cache copy → if authenticated, hand to `ReceiptUploader` with `X-DaftarX-Capture-Source: share`; else enqueue and launch lock/login). **Depends on T059** — `ReceiptUploader` must already accept the `X-DaftarX-Capture-Source` header before this task lands; sequence US4 after US2's T059 if working in parallel teams to avoid merge contention on the upload path.
- [ ] T094 [US4] Register `ShareReceiverActivity` in `android/app/src/main/AndroidManifest.xml` with `<intent-filter>` for `ACTION_SEND` + `ACTION_SEND_MULTIPLE` on `image/*` + `application/pdf` per research §7
- [ ] T095 [US4] Wire `PendingShareQueue` consumption into `MainActivity` post-biometric-unlock (drain queue → hand to `ReceiptUploader`)
- [ ] T096 [P] [US4] String resources for US4 (share-receive confirmation messages — Arabic + English)
- [ ] T097 [P] [US4] Maestro flow extension at `android/maestro/p3-share-target.yaml`

**Checkpoint**: User Story 4 fully functional.

---

## Phase 7: User Story 5 - License-aware shell + version negotiation + update banner (Priority: P3)

**Goal**: Native shell entry points (camera, AI) refresh within one polling cycle of a license change (FR-007). Version skew (app↔server) is detected on every foreground and routes to gate screens (FR-019). Side-load installs see a dismissable "new version available" banner (FR-020). All driven by `GET /api/v1/me/features`.

**Independent Test**: Sign in on Solo license → confirm Scan Receipt hidden. Activate SMB license on server → re-foreground app → confirm Scan Receipt appears within one polling cycle. Separately: bump server's `MinAppVersion` above the running app → re-foreground → confirm `AppOutdatedScreen` appears and blocks native flows.

### Tests for User Story 5

- [ ] T098 [P] [US5] Server contract test at `tests/EgyptTax.UnitTests/Web/Mobile/MeFeaturesEndpointTests.cs` — 11 assertions per [contracts/me-features.md](./contracts/me-features.md) including the 6 new version-field assertions (T6: regex format, T7: assembly source-of-truth, T8: appsettings round-trip, T9: MinAppVersion default `"1.0.0"`, T10: LatestKnownAppVersion defaults to serverVersion, T11: reload-on-change without restart)
- [ ] T099 [P] [US5] Android contract test for `LicenseSnapshot` at `android/app/src/test/kotlin/com/daftarx/mobile/license/LicenseSnapshotTest.kt` (parse all 7 fields, version regex, component-wise compare, fail-open on parse error per data-model §4 validation)
- [ ] T100 [P] [US5] Android contract test for `VersionGate` at `android/app/src/test/kotlin/com/daftarx/mobile/license/VersionGateTest.kt` (emits `Ok` / `AppOutdated` / `ServerOutdated` for the cartesian of server/app version combos per research §17)
- [ ] T101 [US5] Integration test at `android/app/src/androidTest/kotlin/com/daftarx/mobile/LicenseRefreshTest.kt` (mock features endpoint to return different feature sets across calls → verify native camera FAB visibility flips within one ON_RESUME cycle)
- [ ] T102 [US5] Integration test at `android/app/src/androidTest/kotlin/com/daftarx/mobile/VersionGateTest.kt` (server returns `minAppVersion > BuildConfig.VERSION_NAME` → `AppOutdatedScreen` shown and native shell flows blocked; reset → screen dismisses)
- [ ] T103 [US5] Integration test at `android/app/src/androidTest/kotlin/com/daftarx/mobile/UpdateBannerTest.kt` (server returns `latestKnownAppVersion > BuildConfig.VERSION_NAME` → banner visible + dismissable; equal version → banner absent)

### Implementation for User Story 5

- [ ] T104 [US5] Server: implement `MeFeaturesEndpoint` at `src/EgyptTax.Web/Pages/Api/Mobile/MeFeaturesEndpoint.cs` (returns the full payload per [contracts/me-features.md](./contracts/me-features.md): edition + features + expiresAtUtc + fetchedAtUtc + serverVersion + minAppVersion + latestKnownAppVersion; reads `LicenseStatus`, `IOptionsMonitor<MobileVersionConfig>`, and `Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()`)
- [ ] T105 [US5] Server: register the endpoint at `src/EgyptTax.Web/Program.cs` under `/api/v1/me/features`
- [ ] T106 [P] [US5] Android: implement `LicenseSnapshot` data class + parser at `android/app/src/main/kotlin/com/daftarx/mobile/license/LicenseSnapshot.kt` (per data-model §4 with all 7 fields)
- [ ] T107 [P] [US5] Android: implement `FeaturePoller` at `android/app/src/main/kotlin/com/daftarx/mobile/license/FeaturePoller.kt` (foreground-triggered fetch; fail-closed only when snapshot > 1 h stale per data-model §4). **Polling cadence**: triggered ONLY on `Lifecycle.Event.ON_RESUME` — no background timer, no heartbeat. This satisfies SC-007 ("license downgrade reflected within 60 seconds of the next app foreground event") and intentionally accepts that a long-held foreground session retains the in-memory snapshot until the next pause/resume cycle. Document this trade-off as an inline KDoc on the class.
- [ ] T108 [P] [US5] Android: implement `VersionGate` at `android/app/src/main/kotlin/com/daftarx/mobile/license/VersionGate.kt` (component-wise version compare; reads `BuildConfig.VERSION_NAME` + a compile-time `MIN_SERVER_VERSION` constant; emits `Ok`/`AppOutdated`/`ServerOutdated` per research §17)
- [ ] T109 [P] [US5] Compose UI: `AppOutdatedScreen` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/AppOutdatedScreen.kt` (FR-019 copy "please update from Play Store / re-download APK" + vendor download URL CTA)
- [ ] T110 [P] [US5] Compose UI: `ServerOutdatedScreen` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/ServerOutdatedScreen.kt` (FR-019 copy "your DaftarX server needs an update" + shows configured URL)
- [ ] T111 [P] [US5] Compose UI: `UpdateAvailableBanner` at `android/app/src/main/kotlin/com/daftarx/mobile/shell/UpdateAvailableBanner.kt` (FR-020: 56 dp dismissable card pinned above WebView; vendor download URL via `Intent.ACTION_VIEW`; remembers dismissal for the current process only)
- [ ] T112 [US5] Wire `VersionGate` + `FeaturePoller` into `MainActivity.onResume`: gate result decides whether to show `AppOutdatedScreen` / `ServerOutdatedScreen` / WebView with optional `UpdateAvailableBanner` overlay
- [ ] T113 [US5] Refresh the WebView's edition-aware floating-actions on every `LicenseSnapshot` change (Scan Receipt FAB visibility ties to `features.contains("receipt_ocr")`)
- [ ] T114 [P] [US5] String resources for US5 (version-gate screens + update banner — Arabic + English)
- [ ] T115 [P] [US5] Maestro flow extension at `android/maestro/p3-version-gate.yaml`

**Checkpoint**: All five user stories complete and independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Release-engineering items, the vendor privacy policy, CI, lint, accessibility, distribution docs.

- [ ] T116 [P] Author vendor privacy policy at `privacy/daftarx-android-privacy-policy.md` per FR-018 + research §19 (covers Crashlytics + FCM token + per-install device-id; notes customer-server data flows are out of vendor scope)
- [ ] T117 [P] Author Play Store Data Safety form draft at `privacy/play-data-safety-form.md` per FR-018 + research §19 (Diagnostics + Device IDs collected; sharing note about customer server)
- [ ] T118 [P] Document side-load distribution flow at `privacy/sideload-distribution.md` (download URL placement, signing-key disclosure, side-load update banner copy per FR-020 + research §18)
- [ ] T119 [P] Set up Play App Signing flow: generate upload key, document import procedure into the Play Console, capture the resulting app-signing-key SHA-256 in `android/SIGNING.md` (per research §11; ensures dual-channel updates per FR-013 + SC-006)
- [ ] T120 [P] Set up CI workflow at `.github/workflows/android-build.yml`: assembleDebug + JUnit unit tests + Android Lint + Detekt; nightly job for `connectedAndroidTest` + Maestro on the emulator runner
- [ ] T121 [P] Add Android Lint baseline at `android/app/lint-baseline.xml` to capture inherited warnings; CI fails on new lint errors
- [ ] T122 [P] Add Detekt config at `android/config/detekt/detekt.yml` (Kotlin static analysis); CI fails on errors
- [ ] T123 [P] Accessibility pass: add `contentDescription` to every native Compose icon + run TalkBack walkthrough on Maestro emulator; document any gaps in `android/ACCESSIBILITY.md`
- [ ] T124 [P] Add the `version-banner` Maestro flow asserting `UpdateAvailableBanner` dismissal persists for the session at `android/maestro/p3-update-banner-dismissal.yaml`
- [ ] T125 [P] Run `quickstart.md` end-to-end on a fresh emulator to confirm the documented walkthrough still works after all stories land
- [ ] T126 Update `CLAUDE.md` `<!-- SPECKIT START -->` block to point at this `tasks.md` path so future agents resume mid-implementation correctly
- [ ] T127 Document the new server-side endpoints in the existing `008-egypt-tax-accounting` quickstart's "API surface" section so the desktop+web team knows about them
- [ ] T128 [P] Cross-channel install Maestro flow at `android/maestro/p8-cross-channel-install.yaml` asserting SC-006: install side-load APK → complete Setup + login + enable biometric → populate one captured-receipt + one notification-registration → install Play release APK (same signing identity per T013/T119) over the top → confirm server URL, cached session, biometric binding, and per-install device-id all survived the upgrade (no Setup screen reappears, biometric unlock still works first try, push-registration row in server table still active for the same device-id).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — **BLOCKS** all user stories.
- **User Stories (Phase 3+)**: Each depends only on Foundational. Once Foundational is complete, the five stories can proceed in parallel if staffed; otherwise sequentially in priority order (US1 → US2/US3 → US4/US5).
- **Polish (Phase 8)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **US1 (Pocket DaftarX, P1)**: Foundational only. **MVP** — no upstream story dependencies.
- **US2 (Camera, P2)**: Foundational only. Reuses `ReceiptUploader` which US4 also touches (file edit ordering matters between T059 and T093) but conceptually independent.
- **US3 (Push, P2)**: Foundational only. Independent of US1/US2.
- **US4 (Share, P3)**: Foundational + uses `ReceiptUploader` from US2. If staffed in parallel with US2, the team needs to merge `ReceiptUploader` edits carefully (T059 and T093).
- **US5 (License-aware + version gate + update banner, P3)**: Foundational only. The `LicenseSnapshot` it builds is consumed by US2's Scan FAB visibility check (T062); US5 can ship slightly before US2's native-FAB wiring lands if the FAB defaults to hidden.

### Within Each User Story

- Tests (test-first per Constitution III) **MUST** be written and observed failing before implementation.
- Within the test group, tasks marked `[P]` can run in parallel (different test files).
- Implementation order: models / data classes → services → UI screens → wiring into `MainActivity`.
- Within the implementation group, tasks marked `[P]` can run in parallel (different production files).
- Each story is complete only when its checkpoint scenario passes the Maestro flow.

### Parallel Opportunities

- All Setup tasks marked `[P]` (T001–T008, T010–T016) can run in parallel after the initial Gradle scaffolding (T009 sequentialises on T002).
- All Foundational tasks marked `[P]` (T020–T027) can run in parallel (different files).
- Once Foundational is done, the five user stories can proceed in parallel.
- Within each story, all `[P]` tests can run in parallel; all `[P]` implementations can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Launch US1 tests in parallel (different files):
Task: "T030 [P] [US1] Contract test for ServerConfig in android/app/src/test/kotlin/com/daftarx/mobile/config/ServerConfigTest.kt"
Task: "T031 [P] [US1] Contract test for SessionVault in android/app/src/test/kotlin/com/daftarx/mobile/auth/SessionVaultTest.kt"
Task: "T032 [P] [US1] Compose UI test for SetupScreen in android/app/src/androidTest/kotlin/com/daftarx/mobile/shell/SetupScreenTest.kt"

# Launch US1 implementation in parallel (different files):
Task: "T036 [P] [US1] Implement ServerConfig in android/app/src/main/kotlin/com/daftarx/mobile/config/ServerConfig.kt"
Task: "T037 [P] [US1] Implement ServerProbe in android/app/src/main/kotlin/com/daftarx/mobile/config/ServerProbe.kt"
Task: "T038 [P] [US1] Implement SessionVault in android/app/src/main/kotlin/com/daftarx/mobile/auth/SessionVault.kt"
Task: "T039 [P] [US1] Implement BiometricGate in android/app/src/main/kotlin/com/daftarx/mobile/auth/BiometricGate.kt"
Task: "T040 [P] [US1] Implement IdleTimer in android/app/src/main/kotlin/com/daftarx/mobile/auth/IdleTimer.kt"
Task: "T041 [P] [US1] Implement DaftarXWebView in android/app/src/main/kotlin/com/daftarx/mobile/web/DaftarXWebView.kt"
Task: "T042 [P] [US1] Implement DaftarXWebViewClient in android/app/src/main/kotlin/com/daftarx/mobile/web/DaftarXWebViewClient.kt"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T016).
2. Complete Phase 2: Foundational (T017–T029) — **CRITICAL**, blocks every story.
3. Complete Phase 3: User Story 1 (T030–T052).
4. **STOP and VALIDATE**: Run `android/maestro/p1-mvp-flow.yaml` against a real emulator. Run `./gradlew :app:testDebugUnitTest` + `:app:connectedDebugAndroidTest`. Demo to stakeholders.
5. Deploy/demo if ready — this is a complete pocket-DaftarX shell minus native camera + push + share + license refresh.

### Incremental Delivery (recommended)

1. Setup + Foundational → Foundation ready.
2. Add US1 → Test independently → Deploy/Demo (**MVP**).
3. Add US2 + US3 in parallel → Test independently → Deploy/Demo (camera + push live).
4. Add US4 + US5 in parallel → Test independently → Deploy/Demo (share + license-aware shell live).
5. Polish phase → Play Store submission.

### Parallel Team Strategy

With three developers:

1. All three complete Setup + Foundational together (~3–5 days).
2. Once Foundational is done:
   - **Dev A**: US1 (P1 MVP shell).
   - **Dev B**: US2 + US3 (camera + push — biggest native shell value-add).
   - **Dev C**: US5 (license-aware shell + version gate + update banner; server-side endpoint).
3. US4 (share) follows after US2 has merged `ReceiptUploader` (small task, 1–2 days for any free developer).
4. Polish phase shared by all three.

---

## Notes

- `[P]` tasks = different files, no dependencies — safe for parallel execution.
- `[Story]` label maps each task to a specific user story for traceability with `spec.md`.
- Each user story is independently completable and testable; stopping after any story's checkpoint yields working, demonstrable value.
- Verify tests fail before implementing (Constitution III).
- Commit after each task or each tight logical group; never commit a failing build to main.
- The same signing key drives both the side-load APK and the Play Store build (FR-013 + research §11); T013 + T119 capture the setup; the Maestro flow in T125 confirms cross-channel update compatibility (SC-006).
- Cross-cutting items already integrated into the right stories so no separate "FR-017 task" / "FR-018 task" / etc. is needed:
  - **FR-007** (edition gating) → US5 (T106–T113).
  - **FR-017** (Crashlytics) → Foundational (T020–T022).
  - **FR-018** (Play Data Safety) → Polish (T117).
  - **FR-019** (bidirectional version gate) → US5 (T098, T100, T102, T104, T108–T112).
  - **FR-020** (side-load update banner) → US5 (T098, T103, T104, T111) + Polish (T118 for the vendor download-page docs).
  - **FR-021** (private lockscreen notifications) → US3 (T067, T068, T080).
  - **SC-001/002 timing** → US1 (T052 Maestro asserts wall-clock budgets).
  - **SC-003 timing** → US2 (T065 Maestro asserts ≤ 10 s shutter → history row).
  - **SC-004 server-side hand-off** → US3 (T088 Maestro scrapes server log for ≤ 60 s emit-to-publish).
  - **SC-006 cross-channel install** → Polish (T128 Maestro flow upgrades side-load APK to Play release).
