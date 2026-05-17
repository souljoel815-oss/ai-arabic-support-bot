# DaftarX Android — Signing Setup (T013)

**Refer also**: [research §11](../specs/009-android-app/research.md#11-apk--play-signing-for-dual-channel-distribution), FR-013, SC-006.

The dual-channel distribution (side-loaded APK + Google Play Store) requires both channels to install the **same Android package signed with the same key**, otherwise Android refuses to install one over the other. Google's "Play App Signing" model makes this slightly subtle. This doc captures the one-time setup the vendor's release engineer runs.

---

## Once, per vendor: create the upload key

The **upload key** is what *we* hold; Google's Play App Signing service holds the actual **app-signing key** that ships to user devices. Side-loaded APKs must be signed with a key whose SHA-256 matches the app-signing key — most teams achieve this by also using the upload key locally and registering it as the app-signing key inside Play.

```pwsh
# Generate the upload key (RSA 2048, valid 25 years per Play recommendation).
keytool -genkeypair `
  -alias daftarx-upload `
  -keyalg RSA -keysize 2048 -validity 9125 `
  -keystore daftarx-upload.keystore `
  -storepass <strong-store-password> `
  -keypass <strong-key-password> `
  -dname "CN=DaftarX, OU=Mobile, O=DaftarX, L=Cairo, ST=Cairo, C=EG"
```

Store `daftarx-upload.keystore` in the vendor's password manager / HSM. **Never commit it to git.**

---

## Once, per Play Console: enrol in Play App Signing using the upload key

When uploading the first release AAB to Play Console:

1. Pick "Use this key for both upload signing and app signing" (this is the path that lets side-load installs match Play installs).
2. Upload `daftarx-upload.keystore` extract (Play takes the cert) OR generate-in-Play and download the upload key.
3. Note the resulting **app-signing key SHA-256 fingerprint** — visible in Play Console → Setup → App Integrity → App Signing.

---

## Per dev machine / CI: provide signing.properties

The build looks for `signing.properties` at the **root of the `android/` directory**. It is **gitignored** (see `android/.gitignore`).

```properties
# android/signing.properties — vendor-only, NEVER commit
storeFile=daftarx-upload.keystore
storePassword=<strong-store-password>
keyAlias=daftarx-upload
keyPassword=<strong-key-password>
```

On CI, inject these as masked environment variables and reconstruct the file in a pre-step. **Never echo the file contents in build logs.**

---

## Per release: side-load APK + Play AAB from the same build

```pwsh
# Side-load APK (for the vendor's customer-direct download page)
.\gradlew :app:assembleRelease
# → android/app/build/outputs/apk/release/app-release.apk

# Play AAB (for upload to Play Console)
.\gradlew :app:bundleRelease
# → android/app/build/outputs/bundle/release/app-release.aab
```

Both artifacts come from the **same signing identity** because both build types reference `signingConfigs.release` in `android/app/build.gradle.kts`. A customer who installed the side-load APK can install the Play release over the top without uninstalling (SC-006); a Play customer can side-load a newer build for testing without losing their data.

---

## When the upload key needs rotation

Per Google's recommendation, only rotate the upload key (NOT the app-signing key). Play Console → Setup → App Integrity → Upload Key → Request Reset. Google approves within a few days. After approval, generate a new upload keystore, upload its cert to Play, and update `signing.properties`. The app-signing key remains unchanged so existing installs are unaffected.

The app-signing key should never rotate during the product's lifetime — doing so would break the install chain on every existing device.
