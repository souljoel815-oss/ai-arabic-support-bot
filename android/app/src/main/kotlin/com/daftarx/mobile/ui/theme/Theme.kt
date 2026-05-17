// T015 per specs/009-android-app/tasks.md.
//
// Mission Control theme — wraps Material 3's ColorScheme with the
// DaftarX gold + charcoal palette. Two variants:
//
//   - DaftarXDarkColors (default — "leil" in the existing web theme)
//   - DaftarXLightColors ("nahar" — light mode)
//
// The choice between them follows the system setting; a settings
// toggle in SettingsScreen.kt (T047) lets the user override.
package com.daftarx.mobile.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable

private val DaftarXDarkColors = darkColorScheme(
    primary = DaftarXGoldOnDark,
    onPrimary = DaftarXCharcoal,
    primaryContainer = DaftarXGoldDim,
    onPrimaryContainer = DaftarXTextStrongDark,
    background = DaftarXCharcoal,
    onBackground = DaftarXTextStrongDark,
    surface = DaftarXCharcoalElevated,
    onSurface = DaftarXTextStrongDark,
    surfaceVariant = DaftarXCharcoalElevated,
    onSurfaceVariant = DaftarXTextMutedDark,
    outline = DaftarXCharcoalLine,
    error = DaftarXDanger,
    onError = DaftarXTextStrongDark,
)

private val DaftarXLightColors = lightColorScheme(
    primary = DaftarXGold,
    onPrimary = DaftarXIvoryElevated,
    primaryContainer = DaftarXGold,
    onPrimaryContainer = DaftarXTextStrongLight,
    background = DaftarXIvory,
    onBackground = DaftarXTextStrongLight,
    surface = DaftarXIvoryElevated,
    onSurface = DaftarXTextStrongLight,
    surfaceVariant = DaftarXIvory,
    onSurfaceVariant = DaftarXTextMutedLight,
    outline = DaftarXIvoryLine,
    error = DaftarXDanger,
    onError = DaftarXIvoryElevated,
)

@Composable
fun DaftarXTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit,
) {
    val colors = if (darkTheme) DaftarXDarkColors else DaftarXLightColors
    MaterialTheme(
        colorScheme = colors,
        typography = DaftarXTypography,
        content = content,
    )
}
