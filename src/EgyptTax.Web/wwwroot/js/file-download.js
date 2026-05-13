// L2 — trigger a browser file download from raw bytes passed by Blazor.
//
// Avoids the eval() anti-pattern that bit Phase E's inspection-bundle
// (multi-MB base64 strings interpolated into a JS eval crashes the
// SignalR circuit). Instead we declare a real function and Blazor
// passes the bytes once via JS interop.
window.daftarxDownloadBytes = function (filename, base64, mimeType) {
  try {
    const bin = atob(base64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    const blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename || 'download';
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  } catch (e) {
    console.error('daftarxDownloadBytes failed', e);
  }
};
