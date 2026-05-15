// v5 DP.3 — global Ctrl+K / Cmd+K listener that nudges Blazor to
// open the command palette. The Blazor component owns the modal +
// filtering + navigation; this file is purely a keyboard-shortcut
// shim. Designed to coexist with browser/native Cmd+K (browser's
// own shortcut for "search the address bar") — we preventDefault
// only when the focus isn't already in an input the user is typing
// into, so search boxes still let ⌘K do the native thing.
window.daftarxRegisterCmdPalette = function (dotNetRef) {
  if (window.__daftarxCmdPaletteRegistered) return;
  window.__daftarxCmdPaletteRegistered = true;

  document.addEventListener('keydown', (e) => {
    const isMeta = (e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey;
    if (!isMeta || e.key !== 'k') return;

    // Don't hijack if the operator is mid-edit in a normal input
    // OUTSIDE the palette itself; the modal's own input stays
    // accessible because once it's open we trap focus inside.
    const t = e.target;
    if (t && t.closest && t.closest('.cmd-palette-modal')) return;

    e.preventDefault();
    dotNetRef.invokeMethodAsync('Toggle');
  });

  // Esc closes the palette when open. We always preventDefault on
  // Esc when the palette is visible so it doesn't bubble into other
  // close-handlers (e.g. modal backdrops on the page beneath).
  document.addEventListener('keydown', (e) => {
    if (e.key !== 'Escape') return;
    const palette = document.querySelector('.cmd-palette-modal');
    if (palette && palette.classList.contains('is-open')) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync('Close');
    }
  });
};
