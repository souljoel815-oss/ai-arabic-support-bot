/*
 * T222 / US8 / FR-049 — browser-side credential pool for the
 * accountant-firm portal. Stores the list of installations a firm
 * rep has accepted invitations from + the per-installation cookie
 * key so a one-click switch lands them on the right web app
 * without re-entering credentials.
 *
 * Topology: each Egyptian SME runs its OWN on-prem installation;
 * there is no central vendor hub (FR-049). The pool lives in the
 * browser only; it never roundtrips to a server beyond the
 * destination installation's own session cookie. Removing the
 * pool entry on the source side is a no-op against the
 * destination's session — the destination installation is the
 * sole authority on its own session lifecycle (FR-049 / US8
 * scenario 4 — revoke is enforced server-side).
 *
 * Storage shape (localStorage key "egypttax.firmPortal.pool"):
 *   { installations: [
 *       { url, label, firmName, lastUsedAtIso }, ...
 *     ] }
 *
 * Per-installation session cookies are written by each
 * installation's own login flow; the pool only tracks the URL
 * we redirect to. The cookie itself stays scoped to its
 * installation's domain (browser-enforced).
 */
(function () {
    'use strict';
    const POOL_KEY = 'egypttax.firmPortal.pool';

    function readPool() {
        try {
            const raw = window.localStorage.getItem(POOL_KEY);
            if (!raw) {
                return { installations: [] };
            }
            const parsed = JSON.parse(raw);
            if (!parsed || !Array.isArray(parsed.installations)) {
                return { installations: [] };
            }
            return parsed;
        } catch (_) {
            return { installations: [] };
        }
    }

    function writePool(pool) {
        window.localStorage.setItem(POOL_KEY, JSON.stringify(pool));
    }

    function normalizeUrl(url) {
        if (!url) {
            return '';
        }
        return url.replace(/\/+$/, '');
    }

    function listInstallations() {
        return readPool().installations.slice().sort(function (a, b) {
            return (b.lastUsedAtIso || '').localeCompare(a.lastUsedAtIso || '');
        });
    }

    function rememberInstallation(url, label, firmName) {
        const normalized = normalizeUrl(url);
        if (!normalized) {
            return;
        }
        const pool = readPool();
        const existing = pool.installations.find(function (i) { return i.url === normalized; });
        const stamp = new Date().toISOString();
        if (existing) {
            existing.label = label || existing.label;
            existing.firmName = firmName || existing.firmName;
            existing.lastUsedAtIso = stamp;
        } else {
            pool.installations.push({
                url: normalized,
                label: label || normalized,
                firmName: firmName || '',
                lastUsedAtIso: stamp
            });
        }
        writePool(pool);
    }

    function forgetInstallation(url) {
        const normalized = normalizeUrl(url);
        const pool = readPool();
        pool.installations = pool.installations.filter(function (i) { return i.url !== normalized; });
        writePool(pool);
    }

    function switchTo(url) {
        const normalized = normalizeUrl(url);
        if (!normalized) {
            return;
        }
        // Stamp last-used so the switcher's sort surface them at the
        // top next time, then redirect. The destination installation
        // is responsible for its own session cookie + auth check —
        // if the rep is no longer authenticated there, it bounces to
        // login.
        const pool = readPool();
        const existing = pool.installations.find(function (i) { return i.url === normalized; });
        if (existing) {
            existing.lastUsedAtIso = new Date().toISOString();
            writePool(pool);
        }
        window.location.assign(normalized + '/');
    }

    window.egyptTaxFirmPortal = {
        list: listInstallations,
        remember: rememberInstallation,
        forget: forgetInstallation,
        switchTo: switchTo
    };
}());
