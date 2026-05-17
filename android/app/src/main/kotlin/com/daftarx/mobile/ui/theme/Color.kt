// T015 per specs/009-android-app/tasks.md.
//
// Mission Control palette — gold + charcoal — mirrors the existing
// DaftarX web theme so the native shell (Setup, Lock, BiometricPrompt,
// version-gate screens) feels continuous with the WebView content.
package com.daftarx.mobile.ui.theme

import androidx.compose.ui.graphics.Color

// Gold — primary brand. The same hex the web app uses for its
// "DX" logo + primary CTAs.
val DaftarXGold = Color(0xFFC9A227)
val DaftarXGoldDim = Color(0xFF8E7218)
val DaftarXGoldOnDark = Color(0xFFE0BF5A)

// Charcoal — surface family.
val DaftarXCharcoal = Color(0xFF1F2024)
val DaftarXCharcoalElevated = Color(0xFF2A2C32)
val DaftarXCharcoalLine = Color(0xFF3A3D45)

// Light surface family (the day theme).
val DaftarXIvory = Color(0xFFF8F5EE)
val DaftarXIvoryElevated = Color(0xFFFFFFFF)
val DaftarXIvoryLine = Color(0xFFE5E0D2)

// Text / on-surface.
val DaftarXTextStrongDark = Color(0xFFE9E4D6)
val DaftarXTextMutedDark = Color(0xFFA0A0A8)
val DaftarXTextStrongLight = Color(0xFF111315)
val DaftarXTextMutedLight = Color(0xFF555861)

// Semantic accents (matches the web's existing classes).
val DaftarXSuccess = Color(0xFF22A06B)
val DaftarXWarning = Color(0xFFB45309)
val DaftarXDanger = Color(0xFFB91C1C)
val DaftarXInfo = Color(0xFF1B6DC1)
