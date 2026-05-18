# Quickstart: DaftarX Android App

**Plan**: [plan.md](./plan.md)
**Audience**: A developer who has cloned the `speckit-co` repo and wants to build the Android app pointed at a local DaftarX server.

This walks the P1 MVP slice end-to-end — server config + biometric unlock + WebView dashboard — without any of the P2/P3 native features, which confirms the spec's "P1 alone is shippable" claim.

---

## Prerequisites

1. **JDK 17** (Temurin recommended). Set `JAVA_HOME`.
2. **Android Studio Koala (2024.1.1)** or newer — for the Compose preview + emulator manager.
3. **An Android emulator OR physical device on Android 7.0+**. For the biometric flow, the emulator's "Phone" profile with the "Use Host GPU" + "Cold Boot" toggles works; biometrics are simulated via `adb -e emu finger touch 1`.
4. **A running DaftarX server** on the same network as the emulator/device — either:
   - The existing dev server (`dotnet run --project src/EgyptTax.Web`) bound to `0.0.0.0:5050`, OR
   - A `DaftarX-Setup.msi` install with the Windows service running on `0.0.0.0:50063`.
5. **The Android module checked out** at `android/` (will be created by the first task in `tasks.md`; until then the directory is empty).

---

## First build

From the repo root:

```pwsh
cd android
./gradlew :app:assembleDebug
```

The Gradle wrapper auto-downloads Gradle 8.7+. First build takes 3–5 minutes; subsequent incremental builds are seconds.

Output: `android/app/build/outputs/apk/debug/app-debug.apk`.

---

## Install on device / emulator

```pwsh
adb install -r android/app/build/outputs/apk/debug/app-debug.apk
adb shell am start -n com.daftarx.mobile/.MainActivity
```

The first launch lands on the **Setup screen**.

---

## P1 walkthrough

1. **Setup screen** appears (no saved server config).
   - Type the dev server host: `10.0.2.2` (the Android emulator's alias for the host machine's `localhost`) — or your machine's LAN IP for a physical device.
   - Port: `5050` (or `50063` for an MSI install).
   - Tap "اتصال" / "Connect".
2. **Server probe** runs (a `HEAD /` with 5 s timeout). On success the WebView opens; on failure the Error screen appears with a "Try again" button.
3. **Login** — the WebView shows the standard DaftarX login. Sign in with `admin@daftarx.local` + the password from `first-run-credentials.txt`.
4. **Biometric enrolment prompt** — after the first successful login the app asks "Enable biometric unlock?". Tap Yes.
   - On emulator: enrol a fingerprint via Settings → Security → Fingerprint, then `adb -e emu finger touch 1` to simulate the touch.
5. **Cold restart** — `adb shell am force-stop com.daftarx.mobile` then `adb shell am start -n com.daftarx.mobile/.MainActivity`.
6. **Biometric prompt** appears. Authenticate. The WebView opens directly on the dashboard — no Setup screen, no password retype.

This is the SC-001 + SC-002 slice end-to-end. P1 done.

---

## Running the tests

```pwsh
# Unit tier (JVM, fast)
./gradlew :app:testDebugUnitTest

# Instrumented tier (needs emulator/device)
./gradlew :app:connectedDebugAndroidTest

# Maestro smoke (after `brew install maestro` or `iwr -useb https://get.maestro.mobile.dev | iex`)
maestro test android/maestro/p1-mvp-flow.yaml
```

The Maestro flow drives the P1 walkthrough above end-to-end against an emulator and is the recommended pre-PR smoke.

---

## Common dev pitfalls

- **Emulator can't reach `localhost`**: use `10.0.2.2` (emulator-to-host alias) or your machine's LAN IP. Plain `localhost` resolves to the emulator itself.
- **HTTP cleartext rejected**: the per-host cleartext allowance is built at runtime from the saved server config. If you change the config without going through the Setup screen (e.g. by hand-editing the encrypted prefs file), the new host won't be whitelisted. Always go through the in-app reset flow.
- **Biometric prompt doesn't appear**: ensure the emulator has at least one biometric enrolled (Settings → Security → Fingerprint). The app falls back to `DEVICE_CREDENTIAL` (PIN/pattern) when no biometric is enrolled — make sure the device has a lockscreen credential set.
- **WebView shows the desktop layout instead of the responsive layout**: the WebView's User-Agent string includes "Mobile" by default, but if it doesn't, the server's mobile-detection middleware (if added later) might mis-classify. Inspect via `chrome://inspect` on the host browser.
- **`com.daftarx.mobile` already installed with a different signing key**: `adb uninstall com.daftarx.mobile` first. This happens when you flip between debug builds and a signed release.

---

## Backend dependencies

The P1 walkthrough does NOT exercise any of the three new server endpoints from [contracts/](./contracts/). Those land with P2 + P3 stories:

- `GET /api/v1/me/features` — needed for **all of**: P3 Story 5 (license-aware shell, original spec FR-007), bidirectional version-gate screens (FR-019), and the side-load update-available banner (FR-020). The endpoint returns three version fields (`serverVersion`, `minAppVersion`, `latestKnownAppVersion`) in addition to the license feature list. Until added, the app assumes the install includes every feature (no native shell hiding), tolerates any server version, and never shows the update banner.
- `POST /api/v1/notifications/register` — needed for P2 Story 3 (push). Until added, FCM is not configured and the app skips the registration step.
- `POST /api/v1/ai/scan-receipt-mobile` — needed for P2 Story 2 (camera). Until added, the scan button uploads to the existing in-page `/scan-receipt` form-based endpoint as a fallback.

### Firebase Crashlytics dependency (FR-017)

The app initialises Firebase Crashlytics at process start. Until the developer has a valid `google-services.json` in `android/app/`, the Crashlytics SDK no-ops gracefully (no crashes uploaded; no app behaviour change). The Firebase project is the same one that hosts FCM — see [research §16](./research.md#16-crash--non-fatal-telemetry-added-after-speckit-clarify-2026-05-17). For local development against an emulator, drop the vendor's dev-project `google-services.json` into `android/app/` (gitignored).

The dependency direction is **server-first**: each endpoint's failing contract test (server-side) is written before the client code that consumes it (per Constitution III, test-first).

---

## Where things live

| Concern | File |
|---------|------|
| Application + Hilt entry | `android/app/src/main/kotlin/com/daftarx/mobile/DaftarXApp.kt` |
| Single-activity host + nav | `android/app/src/main/kotlin/com/daftarx/mobile/MainActivity.kt` |
| Setup screen (Compose) | `android/app/src/main/kotlin/com/daftarx/mobile/shell/SetupScreen.kt` |
| WebView host + bridge | `android/app/src/main/kotlin/com/daftarx/mobile/web/DaftarXWebView.kt` |
| Biometric + Keystore session vault | `android/app/src/main/kotlin/com/daftarx/mobile/auth/SessionVault.kt` |
| Server URL persistence | `android/app/src/main/kotlin/com/daftarx/mobile/config/ServerConfig.kt` |
| Camera | `android/app/src/main/kotlin/com/daftarx/mobile/camera/ReceiptCapture.kt` |
| Share-target activity | `android/app/src/main/kotlin/com/daftarx/mobile/share/ShareReceiverActivity.kt` |
| FCM messaging service | `android/app/src/main/kotlin/com/daftarx/mobile/push/DaftarXMessagingService.kt` |
| License snapshot poller | `android/app/src/main/kotlin/com/daftarx/mobile/license/FeaturePoller.kt` |
| Arabic strings | `android/app/src/main/res/values-ar/strings.xml` |
| English strings (fallback) | `android/app/src/main/res/values/strings.xml` |
| Manifest | `android/app/src/main/AndroidManifest.xml` |
| Gradle module config | `android/app/build.gradle.kts` |
| Version catalog | `android/gradle/libs.versions.toml` |

---

## After P1

The P2 + P3 stories (camera, push, share, license-aware shell) are layered on top of the P1 shell. Each story has its own task group in `tasks.md` (generated by `/speckit-tasks` from this plan) and is independently testable. The Maestro smoke flow grows one new scenario per story.

When all five stories ship, the dual-channel release ([research §11](./research.md#11-apk--play-signing-for-dual-channel-distribution)) builds a single signed release APK that uploads cleanly to the Play Console and side-loads correctly from a direct link.

---

## Portal integration (T150 — added 2026-05-18)

The Android app does NOT directly validate licences — the desktop
companion does that and exposes a pair-and-stream endpoint over the
LAN. But for users who buy a subscription from the customer portal
(feature `010-website-portal`), the desktop accepts portal-signed
tokens transparently (see `specs/008-egypt-tax-accounting/quickstart.md`
"Portal integration"). The Android app simply mirrors the desktop's
view of the active subscription tier — Solo / SMB / Enterprise / Firm
— via the existing pair-data protocol.

Relevant portal endpoints:
- `GET /portal/downloads` — list of latest Android APK + Play Store link
- `GET /portal/licences` — view active licences (Owner UI)

The Play Store listing references the portal-hosted
`https://daftarx.app/privacy/android` URL. A CI smoke step
(`portal-build.yml` job `smoke-privacy-android`) blocks any deploy that
breaks that route, since removing it would invalidate the Play Store
listing per FR-022.
