# Contract: FPL Public API Consumption

**Stability**: Locked by `tests/contract/test_fpl_api.py` against checked-in fixtures under `tests/fixtures/fpl/`. If FPL changes the response shape, the tests will fail at the boundary — exactly the "fail loudly at the seam" the constitution's Boundary Validation rule asks for.

This file enumerates every FPL endpoint the analyzer is allowed to consume in v1, plus the field subset it relies on. Anything not listed here MUST NOT be read; FR-037 caps cold-run requests at ≤10 across all endpoints.

## 1. `GET https://fantasy.premierleague.com/api/bootstrap-static/`

**Called**: Once per cold run (cache TTL 1 h).
**Auth**: None (Q1 / FR-004).
**Headers sent**: `User-Agent: fpl-analyzer/<version>`, `Accept: application/json`.

Fields consumed (top-level → field):

- `events[]` → `id, name, deadline_time, finished, is_current, is_next, average_entry_score, top_element`
- `teams[]` → `id, name, short_name, strength, strength_overall_home, strength_overall_away`
- `elements[]` → `id, web_name, team, element_type, now_cost, total_points, points_per_game, form, selected_by_percent, chance_of_playing_next_round, status, news, expected_goals, expected_assists, history_past`
- `element_types[]` → `id, singular_name_short` (mapping 1↔GK, 2↔DEF, 3↔MID, 4↔FWD)

Fields explicitly NOT consumed (because they encourage per-element follow-up calls): `elements[].history`. Per-element history is fetched via the bulk endpoint above (`history_past` summary suffices for FR-007's mean prediction at MVP scope; per-fixture features come from `fixtures` in §2).

## 2. `GET https://fantasy.premierleague.com/api/fixtures/`

**Called**: Once per cold run.
**Auth**: None.

Fields consumed:

- `id, event, kickoff_time, finished, team_h, team_a, team_h_difficulty, team_a_difficulty`

Optional query: `?event={gw}` is supported but the analyzer prefers the unfiltered call so a single response covers the entire horizon (still 1 request).

## 3. `GET https://fantasy.premierleague.com/api/entry/{team_id}/event/{gw}/picks/`

**Called**: At most once per cold run, only when `team_id` is supplied.
**Auth**: None — only post-deadline data is exposed by this endpoint, which matches Q1 exactly.
**`{gw}` resolution**: The analyzer always queries the **last finished GW** (not the target GW) per the spec's "Stale public data" edge case.

Fields consumed:

- `picks[]` → `element, position, multiplier, is_captain, is_vice_captain`
- `entry_history` → `bank, value, total_transfers, points_on_bench, event_transfers_cost`

Failure handling:

- HTTP 404 → raise `fpl.errors.UnknownTeamId` → CLI exit 5.
- HTTP 5xx → polite-client retry; if exhausted, `fpl.errors.FPLApiError` → CLI exit 3.

## 4. `GET https://understat.com/league/EPL/{season}` (optional, FR-005)

**Called**: At most once per cold run, only when `--no-understat` is NOT set.

Not strictly an FPL endpoint, but listed here because it is the only other external HTTP call the analyzer makes. Treated as best-effort: any failure (HTTP error, schema change, parse error) is recorded in the Run Status / Diagnostics panel as `understat: unavailable_<reason>` (FR-005, FR-038) and the analysis proceeds with `xG`/`xA` features set to 0.

## Forbidden endpoints (would violate FR-037 or Q1)

- `GET .../api/element-summary/{element_id}/` — per-player history; would require ~600 calls.
- `GET .../api/me/` or any cookie-authed endpoint — Q1 forbids login.
- `GET .../api/leagues-classic/{league_id}/standings/` — out of scope (no league analytics in v1).
- `GET .../api/event-status/` — adds noise; the data needed (current GW, deadlines) is already in `bootstrap-static.events`.

## Request budget

A cold run with `--team-id` and Understat enabled issues **exactly 4 outbound requests** (1 + 1 + 1 + 1). Without `--team-id` and without Understat: **2 requests**. Both fit comfortably under the FR-037 ≤10 cap with margin to spare for occasional retries.
