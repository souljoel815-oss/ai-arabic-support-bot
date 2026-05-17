# Phase 0 Research: DaftarX Android App

**Plan**: [plan.md](./plan.md)
**Date**: 2026-05-17

Each decision below resolves an open technology choice from `plan.md`'s Technical Context. There are no `NEEDS CLARIFICATION` markers carried over from the spec — the Assumptions section there already nailed down the scope edges. This file captures the *technology* choices that the plan deferred.

> **Refreshed 2026-05-17 after `/speckit-clarify` Session 2026-05-17.** Decisions §16 (Crashlytics), §17 (server-version reporting + bidirectional gate), §18 (side-load update banner), and §19 (privacy policy + Data Safety form) were added; §8 (push notifications) was extended with the `VISIBILITY_PRIVATE` clause.

---

## 1. Hosting the DaftarX server UI inside the app

**Decision**: Use AndroidX WebKit's `WebView` from the `androidx.webkit:webkit` artifact, wrapped in a single `DaftarXWebView` Compose interop component. Enable JavaScript, DOM storage, third-party cookies (for the SignalR negotiate roundtrip), and `safe-browsing` API.

**Rationale**: The existing DaftarX server is Blazor Server, which depends on a long-lived SignalR WebSocket back to the server plus first- and third-party cookies for the session. The system WebView on Android 7.0+ is Chromium-based (delivered via Google Play Services on stock devices), supports WebSockets and modern CSS/JS, and inherits security patches via Play Store updates without the app shipping its own engine. `androidx.webkit` adds backwards-compatible feature flags (e.g. `WebSettingsCompat.setForceDark`, `ProcessGlobalConfig.apply`) without raising the minimum SDK.

**Alternatives considered**:
- *Bundling Chromium via GeckoView or Crosswalk*: Ships ~40 MB of native code, defeats the SC-005 install-size target, duplicates the system WebView's security surface, and is no longer maintained for Crosswalk.
- *Trusted Web Activity (TWA)*: Forces the address bar visible on first launch and assumes Digital Asset Links between the app and a public origin, which on-prem LAN installs can't provide.
- *Custom Tabs*: Same TWA limitation — opens in the user's default browser context, breaking the "feels like a native app" promise from User Story 1.

**References**: AndroidX WebKit release notes; Blazor Server hosting model docs (SignalR transport).

---

## 2. Per-host cleartext HTTP allowance

**Decision**: Ship a minimal static `network_security_config.xml` that denies cleartext globally, then call `WebSettingsCompat` + a runtime-built `NetworkSecurityConfigOverrideProvider` (custom `OkHttpClient` interceptor that whitelists the user's configured host for cleartext on each request) so the user-configured server's host is the only origin allowed to talk HTTP.

**Rationale**: The on-prem default bind is `http://0.0.0.0:50063`, and most LAN customers won't deploy TLS. Hardcoding cleartext-allow at build time would weaken security against unrelated hosts the WebView might reach (e.g. an embedded Google Maps tile). The runtime per-host approach is supported by Android 7+ via the `<domain-config>` mechanism, but Android doesn't let `<domain-config>` be edited at runtime in the XML, so the cleanest path is: declare the *base* config as cleartext-denied, and have the WebView's `WebViewClient` intercept `shouldOverrideUrlLoading` to reject any non-configured-host cleartext request.

**Alternatives considered**:
- *Build-time wildcard cleartext*: Trivial to ship but expands the attack surface to every cleartext origin the WebView is ever directed at.
- *Force HTTPS only*: Locks out the entire on-prem LAN customer base; violates the spec's assumption "the on-prem server is reachable over plain HTTP on the LAN".

---

## 3. Encrypted local storage for server URL + biometric flag

**Decision**: `AndroidX Security Crypto`'s `EncryptedSharedPreferences` with the `MasterKey` from `MasterKey.Builder(context).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build()`. One preferences file, three keys: `server_url`, `biometric_required`, `idle_timeout_seconds`.

**Rationale**: Standard Android pattern, hardware-backed when StrongBox/TEE present, gracefully falls back to file-backed AES on older devices. No need for a heavier solution (SQLCipher, Room+Cipher) for three primitives. Survives backup/restore via Auto Backup unless we add `android:fullBackupContent` exclusions — we *will* exclude these prefs so a backup-restored install lands on the first-run server-config screen (per Edge Case "user installs the side-load APK then later installs the Play Store edition").

**Alternatives considered**:
- *Plain `SharedPreferences`*: Server URL isn't a secret per se but device-image extracts of the prefs file can leak a customer's internal LAN topology; encryption is cheap insurance.
- *Android Keystore alone*: Designed for cryptographic keys, not arbitrary string blobs; awkward for short user-typed values.

---

## 4. Cached session cookie storage

**Decision**: Wrap the WebView's session cookie in an AES-GCM ciphertext keyed by a `MasterKey`-derived secret, persisted in a separate `EncryptedSharedPreferences` file (`session_vault.prefs`). On biometric unlock, decrypt and inject via `CookieManager.setCookie()` before loading the URL. On idle re-lock or sign-out, wipe the encrypted blob.

**Rationale**: Blazor Server identity is cookie-based (`AspNetCore.Cookies` + `AspNetCore.Identity.Application`). The cookie is HTTP-only on the server side, which means the WebView's `CookieManager` can read+write it cross-process but JS can't, matching the spec's threat model ("cached session credential released only after biometric prompt").

**Alternatives considered**:
- *Token-based auth (JWT) added to the server*: Forces a server-side auth overhaul out of scope for this feature; cookie reuse is far cheaper.
- *Letting the WebView cookie store handle persistence with no biometric gate*: Defeats FR-002's "release only after successful biometric prompt" requirement — cookies would auto-attach on cold start.

---

## 5. Biometric / PIN gate

**Decision**: `androidx.biometric:biometric` `BiometricPrompt` with `BIOMETRIC_STRONG | DEVICE_CREDENTIAL` authenticators. The latter provides the PIN fallback automatically when biometrics are unavailable or temporarily locked-out.

**Rationale**: `BIOMETRIC_STRONG` requires Class 3 sensors (Pixel, Samsung flagship, etc.); we accept the slightly lower coverage in exchange for the higher assurance level needed to gate a financial-app session. `DEVICE_CREDENTIAL` covers devices without strong biometrics — the user falls back to the same PIN/pattern they use for the lockscreen, which the spec accepts ("if neither exists, every cold start requires the web password").

**Alternatives considered**:
- *`FingerprintManager` directly*: Deprecated as of API 28.
- *Custom in-app PIN screen*: Would require us to store + verify a PIN ourselves (entropy, lockout policy, etc.) when the platform already does this correctly via `DEVICE_CREDENTIAL`.

---

## 6. Native receipt camera

**Decision**: CameraX (`androidx.camera:camera-core` + `camera-camera2` + `camera-lifecycle` + `camera-view`). Capture via `ImageCapture` use case; convert the resulting `ImageProxy` to JPEG before upload.

**Rationale**: CameraX is the Jetpack-recommended path, handles vendor camera quirks across the wildly fragmented Android device matrix, and exposes a lifecycle-aware API that pairs naturally with the Compose host. Lighter than rolling Camera2 by hand; works back to API 21 (safely below our minSdk 24).

**Alternatives considered**:
- *Launch the system camera via `ACTION_IMAGE_CAPTURE` intent*: Simplest, but loses control over the capture UX (no in-app cropping hints, no lighting overlay, no burst mode for blurry first shots) — exactly the affordances the spec called out as the reason to prefer the native camera over the in-WebView file picker.
- *ML Kit document scanner*: Higher-quality output but adds ~12 MB to the install size; the OCR pipeline already lives on the server, so the marginal value of client-side document detection isn't worth the size hit.

---

## 7. System share-target intake

**Decision**: A single `ShareReceiverActivity` declared in `AndroidManifest.xml` with `<intent-filter>` for `ACTION_SEND` + `ACTION_SEND_MULTIPLE` and MIME types `image/*` and `application/pdf`. The activity reads the shared `Uri`, copies it into the app's cache (since shared URIs are short-lived), and either uploads immediately (if authenticated) or enqueues into `PendingShareQueue` (a singleton in-memory hold) and routes the user to the lock/login flow.

**Rationale**: The Android Sharesheet picks up the manifest declaration with no extra wiring. `ACTION_SEND_MULTIPLE` is included for the case where the user shares a multi-page PDF or a batch of receipt photos from Google Photos at once.

**Alternatives considered**:
- *Share Target API (PWA)*: Not applicable — this is a native app, not a TWA/PWA.
- *Persisting the queue to disk*: Over-engineered for the v1 happy path (user shares → unlocks within seconds → uploads). An in-memory queue is sufficient and avoids the cleanup complexity of orphaned share files outliving the process.

---

## 8. Push notifications

**Decision**: Firebase Cloud Messaging via the Android `com.google.firebase:firebase-messaging` SDK; server-side publishing via the `FirebaseAdmin` .NET SDK (`Google.Apis.Auth` for the service-account flow + the FCM HTTP v1 endpoint). One Firebase project owned by the DaftarX vendor; the project's `google-services.json` is shipped in the app bundle. Every notification posted by `DaftarXMessagingService` MUST call `NotificationCompat.Builder.setVisibility(VISIBILITY_PRIVATE)` and call `.setPublicVersion(...)` with a redacted summary builder (per FR-021, clarified 2026-05-17) so the lockscreen content never includes vendor names, amounts, or deadline dates — only the category and an aggregate count.

**Rationale**: FCM is the only viable push transport on Android. HTTP v1 (not the deprecated legacy HTTP API) is the supported wire format as of 2026 — the legacy API was retired by Google in 2024. The .NET FirebaseAdmin SDK wraps the HTTP v1 request signing so we don't manage service-account JWTs by hand. `VISIBILITY_PRIVATE` + `setPublicVersion` is the OS-supported way to give the OS authority over which content variant renders based on the device lock state — no custom polling, no settings UI.

**Alternatives considered**:
- *WebSocket push from the existing SignalR hub*: Doesn't survive the device sleeping (Doze mode). Won't deliver notifications when the app is closed.
- *Self-hosted UnifiedPush*: Requires the customer's accountant to install a separate push relay; non-starter for a v1 SME tool.
- *Always-full or always-minimal notification content* (rejected during `/speckit-clarify`): full leaks PII on the lockscreen; minimal sacrifices utility for an unlocked at-a-glance triage that's the main UX value of the push.

---

## 9. Server-side push registration table

**Decision**: One new EF Core entity `MobilePushRegistration` with columns `UserId` (FK to existing `users.id`), `DeviceId` (a stable UUID the app generates on first launch and persists in `EncryptedSharedPreferences`), `FcmToken`, `Platform` ("android" for now, "ios" reserved), `CreatedAtUtc`, `LastSeenAtUtc`, `RevokedAtUtc?`. Composite unique index `(UserId, DeviceId)`. Soft-delete on logout (`RevokedAtUtc` set, row kept for audit).

**Rationale**: Per-user-per-device scoping matches the spec's FR-006. Soft-delete preserves the audit trail required by the existing FR-028 audit log (the same one the server already writes to `audit_log` for every state change). Reusing EF Core is consistent with the rest of `EgyptTax.Infrastructure`.

**Alternatives considered**:
- *Per-user only (no device dimension)*: Would lose notifications when the user has two devices.
- *Per-device only (no user dimension)*: Would deliver an ex-employee's notifications to their replacement after a logout — privacy violation.

---

## 10. Deep-link URI grammar

**Decision**: Custom scheme `daftarx://<page>/<id>` and HTTPS App Links `https://<server-host>:<port>/<route>`. The manifest declares an `<intent-filter>` with `android:autoVerify="true"` for the HTTPS link form once a customer turns on Digital Asset Links for their public server (optional, not required for v1); the custom scheme works unconditionally.

**Rationale**: Custom schemes work on every Android version with no server-side hosting requirement, which matches the LAN-only deployments. HTTPS App Links are added as a future-friendly bonus for the customers running a public-HTTPS gateway who want notifications from any browser to also land in the app.

**Alternatives considered**:
- *Custom scheme only*: Loses the bonus pathway for HTTPS-deploying customers.
- *App Links only*: Excludes LAN-only customers (most of the target).

---

## 11. APK + Play signing for dual-channel distribution

**Decision**: Upload-key + Play App Signing. The vendor holds the upload key (used to sign artifacts uploaded to Play); Google holds the app-signing key (used for all installs from Play). For the side-loaded APK, build with the same upload key after extracting the app-signing-key SHA-256 from the Play console and configuring the build to sign with a key whose SHA-256 matches. This way an update from either channel to the other is treated by Android as an in-place upgrade.

**Rationale**: Google made Play App Signing mandatory for new apps in August 2021. Without it, we can't publish to Play. Side-loaded APKs must be signed with the *same key Play uses for installs* — meaning we extract the Play app-signing key (or use the same private key for both upload and app-signing in the rare cases Play allows it). The standard recipe is documented in Google's "App Signing by Play" docs.

**Alternatives considered**:
- *Separate keys for side-load and Play*: Side-load installs treated as a different app; updates from one channel fail with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`.
- *Self-managed signing (no Play App Signing)*: No longer available for new app uploads.

---

## 12. Idle re-lock coordinator

**Decision**: A foreground `IdleTimer` that listens for `Lifecycle.Event.ON_PAUSE`, records the timestamp, and on `ON_RESUME` compares against the configurable timeout (default 5 min, stored in `EncryptedSharedPreferences`). On timeout, navigate to the lock screen and re-require biometric unlock.

**Rationale**: Cheaper than running a background `WorkManager` job; survives Android's process-death scenarios because we re-check on every resume. The user's actual screen-off / app-background time is what matters, not wall-clock time while the app is foregrounded.

**Alternatives considered**:
- *`UserActivityCallback` / `Window.Callback`*: Tracks taps within the app, not background time. Would fail to re-lock when the user switches away to another app.
- *System-wide idle detection*: Requires `BIND_ACCESSIBILITY_SERVICE` or equivalent — heavy permissions for a benign feature.

---

## 13. RTL + bilingual layout

**Decision**: Declare `android:supportsRtl="true"` and provide `values-ar/strings.xml` alongside the default `values/strings.xml`. Default the app's per-app locale to Arabic via `AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags("ar-EG"))` on first launch, with a settings toggle to switch to English. The WebView inherits the locale via the standard `Accept-Language` header, which Blazor's `RequestLocalizationOptions` already honours (the server defaults to `ar-EG` per the existing `008-egypt-tax-accounting` config).

**Rationale**: Per-app language is the Android 13+ idiomatic way to let the user override the device locale for one app, and it back-compats to API 21+ via AppCompat. Using AppCompat keeps the codebase free of fork-per-API conditionals.

**Alternatives considered**:
- *Updating `Configuration.locale` manually on every activity*: Requires plumbing through every Composable; AppCompat handles it once.

---

## 14. Testing harness

**Decision**: JUnit 5 (`org.junit.jupiter:junit-jupiter` via the JUnit 5 Android plugin) + MockK for mocking + Robolectric for Android-framework-dependent unit tests. Espresso + Compose UI Test (`androidx.compose.ui:ui-test-junit4`) for instrumented tests. Maestro YAML flows for the smoke end-to-end suite (server-config → biometric → dashboard).

**Rationale**: JUnit 5 is the modern standard; MockK is Kotlin-idiomatic where Mockito is Java-first; Robolectric runs on the JVM so unit tests stay fast in CI. Compose UI Test is the only sane way to test Composables. Maestro replaces the heavier Espresso E2E flows for top-level smoke testing and runs on a Pixel emulator in CI.

**Alternatives considered**:
- *JUnit 4 only*: JUnit 5 is the current Jetpack-recommended default and works fine with the AGP 8.5 JUnit 5 plugin.
- *Mockito*: Less Kotlin-idiomatic; verbose when mocking suspend functions.

---

## 16. Crash + non-fatal telemetry (added after `/speckit-clarify` 2026-05-17)

**Decision**: Firebase Crashlytics via `com.google.firebase:firebase-crashlytics` + the `com.google.firebase.crashlytics` Gradle plugin. Reuses the same Firebase project as FCM (research §8) so no second cloud dependency is introduced. Initialised in `DaftarXApp.onCreate()` after Hilt bootstrap. Custom keys allowlist (`telemetry/CrashScrubber.kt`) restricts any custom-key attachment to the small set defined in FR-017: app version, locale, server-config-host-only-redacted-hash, idle-timeout setting. Any other `Crashlytics.setCustomKey` call is blocked by a unit test that scans the assembled class for the SDK call sites and asserts each one's key is in the allowlist.

**Rationale**: Crashlytics is the path-of-least-resistance for a Firebase-using app — same `google-services.json`, same console, same vendor relationship. Free tier handles the volume an SME-tool will produce. The PII-allowlist guard is necessary because the app's WebView is rendering customer financial data; an accidental `setCustomKey("page", currentDomString)` would leak that into Crashlytics' indefinite retention. The unit-test-as-policy approach (rather than runtime sampling) catches the bug at PR time instead of after exfiltration.

**Alternatives considered**:
- *Sentry self-hosted*: A second cloud or self-managed service; ~3 days of ops setup. Worth it later if Crashlytics retention isn't sufficient or we need pre-flight PII redaction we can't get from Crashlytics' SDK.
- *Sentry hosted*: $$/month per active device. Not justified at v1 scale.
- *No telemetry (rely on user-reported issues)*: The vendor would be blind to crashes that prevent the app reaching the server at all (the exact crashes most likely to lock customers out). Rejected.
- *Custom `/api/v1/mobile/crash` endpoint*: All the work of building dashboards + storage + retention without Crashlytics' off-the-shelf grouping/symbolication. Not justified.

---

## 17. Server-version reporting + bidirectional version gate (added after `/speckit-clarify` 2026-05-17)

**Decision**: The `/api/v1/me/features` response carries three new string fields — `serverVersion`, `minAppVersion`, `latestKnownAppVersion`. `serverVersion` is read from the EgyptTax.Web assembly's `AssemblyInformationalVersion` attribute at server startup (cached for the process lifetime). `minAppVersion` and `latestKnownAppVersion` are read from `appsettings.json` under a new `MobileVersionConfig` section, with reload-on-change so the vendor can bump them without a server restart. On the app side, a single `license/VersionGate.kt` runs on every foreground (alongside the existing feature-snapshot poller) and emits one of three states: `Ok`, `AppOutdated(minServer)`, `ServerOutdated(minApp)`. The host activity navigates to one of two new screens (`AppOutdatedScreen.kt`, `ServerOutdatedScreen.kt`) on the non-`Ok` states.

**Rationale**: A single endpoint for license + version reduces network chatter and keeps the polling cadence singular (one foreground = one HTTP call). Reading `serverVersion` from the assembly avoids the staleness risk of a hand-maintained constant string. Reading `minAppVersion`/`latestKnownAppVersion` from config (not the assembly) lets the vendor push a new app release and ratchet the floor without redeploying the server. Semantic-version comparison uses a tiny string-split routine — no external library — because the version format is a flat "MAJOR.MINOR.PATCH" string.

**Alternatives considered**:
- *Separate `/api/v1/version` endpoint*: Adds a round trip; couples updates to a less-frequent endpoint. Rejected.
- *Embed `minAppVersion` in the assembly*: Requires a server restart to bump; bad ergonomics for vendor release ops.
- *Use `androidx.startup` AppCheck-style approach* on the device: Overkill for a single endpoint roundtrip.
- *Tolerate skew silently / graceful degradation* (rejected during `/speckit-clarify`): Customers can't tell why a feature is missing; support tickets balloon.

---

## 18. Side-load update notification strategy (added after `/speckit-clarify` 2026-05-17)

**Decision**: The `latestKnownAppVersion` field from §17 doubles as the source for the FR-020 update banner. Compared on the device against `BuildConfig.VERSION_NAME`. When the running build is older, the host activity renders a `UpdateAvailableBanner.kt` Compose card pinned above the WebView (≤ 56 dp tall, dismissable for the current session, re-appears next launch until the running build catches up). The banner action is an `Intent.ACTION_VIEW` opening the vendor's download URL in the user's default browser; no in-app downloader, no `REQUEST_INSTALL_PACKAGES`. Play Store installs that are current never see the banner because their `BuildConfig.VERSION_NAME` ≥ `latestKnownAppVersion` (Play auto-updates before customer notices).

**Rationale**: Reuses the version data already needed for FR-019 — zero new endpoints. Avoids `REQUEST_INSTALL_PACKAGES` which Google Play scrutinises heavily and requires manual review on every release. The "dismiss per session" UX matches every other major app's update-available pattern and respects users without nagging. The banner is informational, not blocking, so it doesn't interfere with day-to-day workflows for customers who haven't yet got around to updating.

**Alternatives considered**:
- *Auto-download + install via vendor endpoint*: Requires `REQUEST_INSTALL_PACKAGES` (sensitive permission), a download UI, file integrity checks, and customer signed certs. Heavy for marginal value.
- *Defer entirely to customer IT*: Side-load customers would discover staleness only when the FR-019 version-gate blocks them — frustrating because by then they're already locked out.
- *Persistent (non-dismissable) banner*: Would feel naggy when the customer simply hasn't gotten around to updating yet.

---

## 19. Vendor privacy policy + Play Store Data Safety declaration (added after `/speckit-clarify` 2026-05-17)

**Decision**: A static Markdown source file at `privacy/daftarx-android-privacy-policy.md` (vendor-hosted at e.g. `https://daftarx.app/privacy/android`). The Play Store Data Safety form declares exactly two collection categories and one shared category:
- **Collected**: "Diagnostics" (crash logs + device IDs) — processed by Firebase Crashlytics + FCM; required for app functionality; not used for advertising; not shared with third parties beyond Firebase.
- **Collected**: "Device or other IDs" (FCM device token, per-install device UUID) — processed by Firebase Cloud Messaging; required for app functionality; not used for advertising.
- **Note in "Data shared"**: "This app connects to a self-hosted server operated by your accounting firm or employer. Data you enter — including invoices, expenses, and login credentials — is sent to that server, NOT to DaftarX. Refer to your server operator's privacy policy."

**Rationale**: Matches the data-controller-vs-processor split clarified in session 2026-05-17 (the customer is the data controller for business data; the vendor processes only the diagnostic + push channels). The "data shared" note pre-empts the most common Play reviewer question ("you have a CAMERA permission and a network call — where do the photos go?"). Hosting the policy as Markdown in-repo makes legal review diffable and version-controlled.

**Alternatives considered**:
- *Full enumeration*: Adds every category Play tracks (financial info, photos, app activity) even though those go to the customer's server. Triggers more aggressive Play review and scary checkboxes on the listing. Rejected.
- *Defer the form to a "legal review" task*: Blocks Play submission indefinitely. Rejected.
- *Generic boilerplate policy*: Increases legal risk and fails Play's content-quality bar.

---

## 20. Out-of-scope debt acknowledgments

The following items are intentionally NOT researched because the spec puts them out of scope for v1; they are listed here so reviewers see they were considered and deferred:

- **Offline-first sync with a local Room database** — deferred until a customer cohort with poor connectivity emerges.
- **iOS port** — separate feature spec when prioritised.
- **Tablet / foldable layouts** — separate feature spec; non-trivial UX rework.
- **Self-signed TLS support** — deferred until a customer asks; until then, on-prem stays HTTP and public stays CA-signed HTTPS.
- **Multi-account / multi-server profiles** — deferred to a 010-android-multi-account feature when an accounting-firm pilot lands.
- **Wear OS, Android Auto, Chromecast** — out of scope indefinitely.
