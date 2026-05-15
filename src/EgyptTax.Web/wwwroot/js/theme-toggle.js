// v5 DP.5 — theme-toggle JS. Runs INLINE in _Layout.cshtml's
// <head> so the theme is applied before the first paint
// (avoids a light-flash on dark loads). Stores the choice in a
// long-lived cookie + a localStorage backup.
//
// Public API:
//   daftarxApplyThemeOnLoad()   — read cookie + apply (called by inline boot)
//   daftarxSetTheme(theme)      — set + persist (called by Blazor toggle)
//
// Themes: "nahar" (light, default) / "leil" (dark). The CSS
// applies via [data-theme="leil"]; nahar is just the absence
// of the attribute so removing it returns to default.

(function () {
  const COOKIE_NAME = 'daftarx-theme';
  const STORAGE_KEY = 'daftarx-theme';
  const VALID = new Set(['nahar', 'leil']);

  function readCookie(name) {
    const m = document.cookie.match(
      new RegExp('(?:^|; )' + name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '=([^;]*)'));
    return m ? decodeURIComponent(m[1]) : null;
  }

  function writeCookie(name, value, days) {
    const exp = new Date();
    exp.setDate(exp.getDate() + days);
    document.cookie =
      name + '=' + encodeURIComponent(value) +
      '; expires=' + exp.toUTCString() +
      '; path=/; SameSite=Lax';
  }

  function applyTheme(theme) {
    if (!VALID.has(theme)) theme = 'leil';
    if (theme === 'nahar') {
      document.documentElement.removeAttribute('data-theme');
    } else {
      document.documentElement.setAttribute('data-theme', theme);
    }
  }

  window.daftarxApplyThemeOnLoad = function () {
    let theme = readCookie(COOKIE_NAME);
    if (!theme) {
      try { theme = localStorage.getItem(STORAGE_KEY); } catch (_) { /* ignore */ }
    }
    applyTheme(theme || 'leil');
  };

  window.daftarxSetTheme = function (theme) {
    if (!VALID.has(theme)) theme = 'leil';
    applyTheme(theme);
    writeCookie(COOKIE_NAME, theme, 365);
    try { localStorage.setItem(STORAGE_KEY, theme); } catch (_) { /* ignore */ }
  };

  window.daftarxGetTheme = function () {
    return document.documentElement.getAttribute('data-theme') || 'nahar';
  };
})();
