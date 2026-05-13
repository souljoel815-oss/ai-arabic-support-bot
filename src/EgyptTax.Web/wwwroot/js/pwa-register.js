// Phase I follow-up — actively unregister any existing service
// worker. The Phase E SW caused stale-JS lock-in (offline banner
// false-positives); replacing service-worker.js with a kill-switch
// stub handles users who load the page again, but this script
// covers users whose browser doesn't re-fetch the SW immediately
// (e.g. they had a tab open). Belt-and-braces.
if ('serviceWorker' in navigator) {
  navigator.serviceWorker.getRegistrations().then((regs) => {
    regs.forEach((r) => r.unregister().catch(() => {}));
  });
}
