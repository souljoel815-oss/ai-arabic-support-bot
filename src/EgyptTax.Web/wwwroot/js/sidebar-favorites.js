// v5 UI Rebuild Sprint 1 — sidebar favorites pin/unpin via
// localStorage. Per-browser, per-device — no DB roundtrip.
// Stored as a JSON array of { url, labelAr, labelEn, icon }.

(function () {
  const KEY = 'daftarx-sidebar-favorites';
  const MAX = 8;

  function read() {
    try {
      const raw = localStorage.getItem(KEY);
      if (!raw) return [];
      const arr = JSON.parse(raw);
      return Array.isArray(arr) ? arr : [];
    } catch (_) { return []; }
  }

  function write(arr) {
    try { localStorage.setItem(KEY, JSON.stringify(arr.slice(0, MAX))); }
    catch (_) { /* quota / private mode */ }
  }

  window.daftarxGetFavorites = function () { return read(); };

  window.daftarxToggleFavorite = function (item) {
    if (!item || !item.url) return read();
    const list = read();
    const idx = list.findIndex(x => x.url === item.url);
    if (idx >= 0) {
      list.splice(idx, 1);
    } else {
      list.unshift(item);
    }
    write(list);
    return list;
  };

  window.daftarxIsFavorite = function (url) {
    if (!url) return false;
    return read().some(x => x.url === url);
  };

  // v5 UI Sprint 1 — sidebar live search. Walks every link with
  // a data-search attribute under .sidebar-v2 and toggles a
  // .hide-by-search class based on whether the attribute (or
  // the visible text) contains the query (case-insensitive).
  // Also force-opens any <details> group containing a matching
  // link, then resets on clear.
  window.daftarxFilterSidebar = function (query) {
    const sidebar = document.querySelector('.sidebar-v2');
    if (!sidebar) return;
    const q = (query || '').trim().toLowerCase();
    if (!q) {
      sidebar.removeAttribute('data-search-active');
      sidebar.querySelectorAll('.hide-by-search').forEach(el =>
        el.classList.remove('hide-by-search'));
      sidebar.querySelectorAll('details[data-search-forced-open]').forEach(d => {
        d.removeAttribute('data-search-forced-open');
        d.removeAttribute('open');
      });
      return;
    }
    sidebar.setAttribute('data-search-active', '1');
    const links = sidebar.querySelectorAll('a[href]');
    links.forEach(a => {
      const ds = (a.getAttribute('data-search') || '').toLowerCase();
      const txt = (a.textContent || '').toLowerCase();
      if (ds.includes(q) || txt.includes(q)) {
        a.classList.remove('hide-by-search');
      } else {
        a.classList.add('hide-by-search');
      }
    });
    sidebar.querySelectorAll('details.sidebar-group').forEach(d => {
      const hasMatch = d.querySelector('a:not(.hide-by-search)');
      if (hasMatch && !d.hasAttribute('open')) {
        d.setAttribute('open', '');
        d.setAttribute('data-search-forced-open', '1');
      }
    });
  };
})();
