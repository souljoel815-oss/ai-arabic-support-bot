# Feature Specification: FPL Ultimate Analyzer

**Feature Branch**: `007-fpl-analyzer`
**Created**: 2026-05-06
**Status**: Draft
**Input**: User description: "Build a Fantasy Premier League (FPL) analyzer that helps a manager make better weekly decisions across a multi-gameweek planning horizon. The system ingests live FPL data (players, fixtures, prices, ownership, results) and scores each player's expected points using an ML ensemble of gradient-boosting models plus quantile/Poisson regression for ceiling and goal-rate estimates. It produces (a) a single-gameweek recommendation — transfer or hold, captain/vice picks, expected score — and (b) a multi-week transfer plan via beam search that respects bank balance, free-transfer rollover (capped at 5), and -4 hit costs. Two operating modes: (1) from-scratch squad construction within £100m using an LP optimizer that enforces all FPL squad-composition rules (15 players, 2 GK / 5 DEF / 5 MID / 3 FWD, max 3 per club), and (2) squad-continuity mode when the user supplies their FPL Team ID, comparing their actual squad against alternatives. Surface the analysis through a CLI (gameweek/horizon/team-id flags, backtest mode, cache control) and a Streamlit web UI (sidebar inputs, starting XI / bench tables, multi-week plan table, predicted-points heatmap, chip-strategy cards for Triple Captain / Bench Boost / Free Hit / Wildcard, differential-picks panel, model diagnostics). Cache fetched data and trained models for ~1 hour to avoid redundant work. Stay robust when optional data sources (e.g. Understat xG/xA) are unavailable on the user's Python version — expose a no-understat toggle and degrade gracefully with documented accuracy impact. Existing scaffolding lives in specs/007-fpl-analyzer/ (fpl_main.py CLI shim, fpl_gui.py Streamlit shim, requirements.txt) — these import from a `fpl/` package that is not yet present and needs to be designed and built as part of this feature."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Weekly transfer & captain decision for my existing squad (Priority: P1)

An FPL manager has an active team and wants a clear, data-backed answer to a single question every gameweek: "Should I transfer, and if so who in/out, and who do I captain?" They supply their FPL Team ID, name a target gameweek, and within a couple of minutes see a single recommendation card: HOLD or a specific 1-/2-transfer move with the projected net gain after any -4 hit, plus a captain and vice-captain pick.

**Why this priority**: This is the recurring weekly use-case — the manager touches the tool once per gameweek and needs an unambiguous "do this" answer. Everything else in the product is supplementary to this decision.

**Independent Test**: Given a valid Team ID and a target gameweek before the deadline, the system returns one recommendation card containing (a) action (HOLD or transfer-in/transfer-out), (b) net projected gain, (c) hit cost, (d) captain and vice-captain, and (e) the resulting bank balance — without needing any other feature to be present.

**Acceptance Scenarios**:

1. **Given** a Team ID with a known squad and bank balance, **When** the manager requests a recommendation for the next upcoming gameweek, **Then** the system returns either HOLD or a specific transfer with projected gain, hit cost, and captain pick.
2. **Given** a transfer would only marginally outscore HOLD after a -4 hit, **When** the recommendation is computed, **Then** the system recommends HOLD and explains that no positive-gain transfer was found.
3. **Given** the manager has 0 free transfers and the best move requires a hit, **When** the recommendation runs, **Then** the gain shown is net of the -4 hit and the hit cost is displayed prominently.
4. **Given** an invalid or unknown Team ID is supplied, **When** the manager requests a recommendation, **Then** the system returns a clear error and falls back to from-scratch mode.

---

### User Story 2 - Multi-week transfer plan across the horizon (Priority: P2)

An FPL manager wants more than a one-week answer — they want to see how this week's decision interacts with the next 3-5 gameweeks. The system produces a step-by-step plan: for each gameweek in the horizon, what action to take, what bank/free-transfer state results, and what the cumulative score gain is versus simply holding every week. They can also see two or three alternative paths to compare.

**Why this priority**: Free transfers and bank balance carry over (FTs cap at 5), so the locally-best move this week often isn't globally optimal. Surfacing the multi-week plan converts the tool from a single-shot recommender into a strategy aid.

**Independent Test**: Given a Team ID and a horizon length (1-5), the system returns a planned action per gameweek with bank-after, free-transfers-after, weekly predicted score, cumulative hits, and the net gain versus a hold-every-week baseline.

**Acceptance Scenarios**:

1. **Given** a horizon of 4 gameweeks, **When** the plan is computed, **Then** the system returns 4 sequential decisions with bank and FT state correctly carried forward and FTs capped at 5.
2. **Given** a multi-transfer move in week 2 unlocks a stronger sequence, **When** the plan is computed, **Then** the recommended path may take a -4 hit in week 2 if the cumulative net gain is positive.
3. **Given** the planning horizon includes a double-gameweek or blank gameweek, **When** the plan is computed, **Then** weekly predictions reflect the actual fixture count for each player and the plan adjusts accordingly.

---

### User Story 3 - From-scratch squad / Wildcard planning (Priority: P3)

At season start, on a Wildcard, or when curious about the unconstrained optimum, the manager wants to see "what's the best 15-player squad I could build right now from scratch within £100m?" The system returns a complete legal squad (2 GK / 5 DEF / 5 MID / 3 FWD, max 3 per club), an optimal starting XI within that squad, a captain pick, and the multi-gameweek outlook for that squad.

**Why this priority**: Used a few times per season (start, wildcard activations) but high stakes when used. Also the natural fallback when no Team ID is supplied.

**Independent Test**: With no Team ID and a budget of £100m, the system returns a 15-player squad that satisfies all FPL composition rules and stays within budget, plus a starting XI and captain.

**Acceptance Scenarios**:

1. **Given** a £100m budget and the current gameweek, **When** the manager requests a from-scratch squad, **Then** the result contains exactly 15 players (2/5/5/3 by position) with no more than 3 from any single club and total cost ≤ £100m.
2. **Given** a from-scratch squad has been generated, **When** the manager views the result, **Then** they see both the full 15 and the recommended starting XI with captain/vice clearly marked.
3. **Given** the manager is in continuity mode (Team ID supplied) but wants to preview a Wildcard, **When** they enable the Wildcard preview, **Then** a from-scratch squad is shown alongside their current squad for comparison.

---

### User Story 4 - Chip-strategy timing recommendations (Priority: P4)

The manager has four chips per season (Triple Captain, Bench Boost, Free Hit, Wildcard) and wants timing guidance: which gameweek to play each chip and why. The system surfaces one recommendation card per chip with the suggested gameweek, the supporting rationale (ceiling for TC, bench projection for BB, blank/double-gameweek swing for FH, horizon-score swing for WC), and the projected upside.

**Why this priority**: Chip timing is high-leverage — the right TC week can swing 15-25 points — but it's a "nice-to-have" alongside the weekly transfer call. Independent of P1/P2/P3 decisions.

**Independent Test**: For a given gameweek and horizon, the system returns up to four chip recommendations (TC, BB, FH, WC), each with a target gameweek and a numeric supporting metric, or "no recommendation" if no positive case exists.

**Acceptance Scenarios**:

1. **Given** the current gameweek is a regular single-fixture week and an upcoming gameweek contains doubles, **When** chip recommendations are computed, **Then** Bench Boost prefers the double-gameweek and Free Hit prefers a swing-favorable gameweek.
2. **Given** a player has an exceptionally high ceiling in a specific gameweek, **When** Triple Captain is computed, **Then** that gameweek is recommended and the supporting ceiling is displayed.
3. **Given** none of the chips has a positive case in the horizon, **When** chip recommendations are computed, **Then** the system clearly indicates "no recommendation" rather than fabricating one.

---

### User Story 5 - Visual analysis via the web UI (Priority: P5)

A manager who prefers a richer interface than the CLI opens the Streamlit app, enters their Team ID and horizon in the sidebar, and explores the analysis visually: starting XI table, bench, multi-week plan, predicted-points heatmap across the horizon, chip cards, differential picks (low-ownership candidates), and a model-diagnostics panel showing top features and baseline accuracy.

**Why this priority**: The CLI is sufficient to deliver the recommendations, but a visual UI dramatically improves comprehension of multi-week plans and chip timing. P5 because it depends on the analyses from P1-P4 being correct first.

**Independent Test**: With the analyses from P1-P4 producing valid output, the web UI renders all panels (XI table, bench, multi-week plan, heatmap, chip cards, differentials, diagnostics) without errors and reflects sidebar input changes correctly.

**Acceptance Scenarios**:

1. **Given** a valid Team ID and horizon in the sidebar, **When** the manager clicks Run Analysis, **Then** the page renders the recommendation card, XI/bench tables, multi-week plan, and chip cards within the time budget defined in Success Criteria.
2. **Given** an analysis has already been run and the manager toggles a display option (e.g., hide differentials), **When** the page rerenders, **Then** the underlying analysis is **not** recomputed (display toggles do not invalidate cache).
3. **Given** the manager changes the gameweek or horizon, **When** they click Run Analysis, **Then** a fresh analysis runs and the new results replace the old ones.

---

### Edge Cases

- **Stale public data**: Public FPL endpoints only update after each gameweek deadline. If the manager has already made transfers since the last finished gameweek, those won't be reflected. The UI must visibly warn the manager.
- **Optional data source unavailable**: If Understat data cannot be fetched (e.g., the underlying library doesn't support the user's Python version), the analysis must still produce a valid recommendation, with a documented accuracy impact (~5-8% MAE) and a visible indicator that xG/xA features were skipped.
- **Mid-season price changes & player removals**: Players can be transferred between Premier League clubs, retire, or be flagged as injured/suspended. The analyzer must always use the latest available status and exclude unavailable players from recommendations.
- **Blank and double gameweeks**: Some teams play 0 or 2 matches in a single gameweek. Predictions and plans must reflect the actual fixture count per player, not assume one match per gameweek.
- **Insufficient bank for any recommended transfer**: If no in-budget transfer produces a positive net gain, the recommendation must fall back to HOLD with a clear explanation.
- **Free-transfer cap reached**: When the manager already has 5 banked free transfers, the multi-week plan must not silently model an unreachable 6th FT.
- **Backtest mode for past gameweeks**: When asked to evaluate prediction quality against actuals from past gameweeks, the system must use only data that would have been available before each evaluated deadline (no look-ahead bias).
- **Cache becomes stale within the hour**: If the official data source is updated between two requests in the same cache window (e.g., a player is flagged injured), the cached analysis may disagree with reality. The manager must be able to clear the cache manually.

## Requirements *(mandatory)*

### Functional Requirements

**Data ingestion & state**

- **FR-001**: System MUST ingest the current state of all Premier League players (id, name, club, position, price, total points, ownership, status flag) from the official public FPL data feed.
- **FR-002**: System MUST ingest fixture data covering at least the current gameweek through the maximum supported horizon (5 gameweeks ahead), including blank- and double-gameweek information per club.
- **FR-003**: System MUST ingest historical results sufficient to train the prediction model (rolling form, recent minutes, recent goals/assists/clean sheets/saves, opponent strength).
- **FR-004**: System MUST allow the manager to look up their own current squad, bank balance, free-transfer count, and chip-availability state by supplying an FPL Team ID.
- **FR-005**: System MUST OPTIONALLY enrich predictions with xG/xA data from Understat when available, and MUST function correctly without it when unavailable, with a visible indicator that the optional source was skipped.
- **FR-006**: System MUST cache fetched data and trained prediction models for approximately one hour, keyed by the inputs that affect analysis (target gameweek, horizon, budget, Team ID, no-understat toggle), and MUST expose a manual cache-clear control.

**Prediction & scoring**

- **FR-007**: System MUST produce a numeric expected-points prediction for every available player for each gameweek in the planning horizon.
- **FR-008**: System MUST also produce a "ceiling" estimate per player per gameweek (an upper-quantile of the predicted distribution) for use in captaincy and chip decisions.
- **FR-009**: System MUST surface baseline prediction accuracy (e.g., MAE on a held-out evaluation set) so the manager can judge how much weight to give the recommendations.
- **FR-010**: System MUST exclude players flagged as unavailable (injured / suspended / not in squad) from all recommended transfers and starting XIs.

**Single-gameweek recommendation (squad-continuity mode)**

- **FR-011**: When given a valid Team ID, system MUST return exactly one recommended action for the target gameweek: HOLD, a 1-transfer move, or a multi-transfer move.
- **FR-012**: System MUST report, for the recommended action: the players in/out, the net projected gain after any hit cost, the hit cost itself (multiples of 4), the resulting bank balance, and the captain and vice-captain picks.
- **FR-013**: System MUST also return up to 5 alternative single-week actions considered, ranked by net gain.
- **FR-014**: System MUST recommend HOLD if no transfer produces a positive net gain after hits.

**Multi-week transfer plan**

- **FR-015**: When given a Team ID and a horizon between 1 and 5 gameweeks, system MUST return a per-gameweek plan with: action, transfer details (if any), hit cost, weekly predicted score, bank after, and free-transfers after.
- **FR-016**: The multi-week plan MUST correctly carry bank balance and free-transfer count between gameweeks, with free transfers capped at 5.
- **FR-017**: System MUST report cumulative hits, plan total score, hold-every-week baseline score, and net gain versus baseline.
- **FR-018**: System MUST surface up to 3 alternative paths so the manager can compare strategies.

**From-scratch squad construction**

- **FR-019**: System MUST be able to construct a complete 15-player squad from scratch within a configurable budget (default £100m) that satisfies all FPL composition rules: exactly 2 goalkeepers, 5 defenders, 5 midfielders, 3 forwards, and no more than 3 players from any single club.
- **FR-020**: System MUST select an optimal starting XI from the constructed squad along with a captain and vice-captain.
- **FR-021**: When no Team ID is supplied, system MUST default to from-scratch mode automatically.
- **FR-022**: When in continuity mode, the manager MUST be able to also view a from-scratch squad as a Wildcard preview without losing their continuity-mode results.

**Chip-strategy recommendations**

- **FR-023**: System MUST produce, for each of Triple Captain, Bench Boost, Free Hit, and Wildcard, either a recommended target gameweek with supporting metric, or an explicit "no recommendation" indicator if no positive case exists in the horizon.
- **FR-024**: Triple Captain recommendation MUST be based on the per-gameweek ceiling estimate, not the mean prediction.
- **FR-025**: Bench Boost recommendation MUST favour gameweeks where the bench projection (4 bench players combined) is highest.
- **FR-026**: Free Hit recommendation MUST favour gameweeks where the swing between the manager's own squad and the from-scratch optimum is largest, taking blank- and double-gameweek information into account.

**Differentials**

- **FR-027**: System MUST produce a differentials panel listing high-value low-ownership candidates (default: ownership below 10%) so the manager can pick rank-climbing options.

**CLI surface**

- **FR-028**: The CLI MUST accept at minimum: target gameweek (auto-detect if omitted), planning horizon (default 3), Team ID (optional), budget (default £100m), no-understat toggle, multi-week-plan toggle, and a clear-cache action.
- **FR-029**: The CLI MUST support a backtest mode that evaluates prediction accuracy against actual results for a user-supplied list of past gameweeks, using only data available before each evaluated deadline (no look-ahead bias).

**Web UI surface**

- **FR-030**: The web UI MUST present sidebar inputs equivalent to the CLI flags (target GW, horizon, budget, Team ID, no-understat) and a Run Analysis button.
- **FR-031**: The web UI MUST render the recommendation card, starting XI table, bench table, multi-week plan table, predicted-points heatmap, chip-strategy cards, differentials panel, and a model-diagnostics panel.
- **FR-032**: The web UI MUST visibly warn the manager that public FPL endpoints only update after each deadline, and that recent transfers may not be reflected.
- **FR-033**: Display-only UI toggles (e.g., show/hide differentials, show/hide a chip card) MUST NOT invalidate the cached analysis.

**Robustness & UX**

- **FR-034**: System MUST gracefully handle an invalid Team ID by reporting the error and falling back to from-scratch mode rather than crashing.
- **FR-035**: System MUST gracefully handle transient failures of the public data feed by retrying briefly and then surfacing a clear error to the manager.
- **FR-036**: All numeric outputs (predicted points, gains, costs) MUST be displayed with sufficient precision to distinguish recommendations (typically 2 decimal places).

### Key Entities *(include if feature involves data)*

- **Player**: A Premier League player available for FPL selection. Key attributes: id, name, club, position, current price, ownership %, availability status, recent form, fixture difficulty, predicted points per upcoming gameweek, ceiling per upcoming gameweek.
- **Fixture**: A single match between two clubs in a specific gameweek. Key attributes: gameweek, home club, away club, kickoff time, opponent strength on each side. May contribute to blank/double-gameweek state for a club.
- **Gameweek**: A scheduling window during which fixtures are played and FPL transfers are locked. Key attributes: gameweek number, deadline, finished/active flag, total fixtures, list of clubs with 0 fixtures (blank) and 2+ fixtures (double).
- **Squad**: A 15-player selection. Attributes: list of 15 players (with position breakdown 2/5/5/3 and max-3-per-club constraint), bank balance, free-transfer count, chip availability state, captain, vice-captain, starting XI selection.
- **Recommendation**: A single proposed action for a single gameweek. Attributes: kind (hold / 1-transfer / multi-transfer), transfers (list of in/out pairs), hit cost, projected net gain, captain pick, resulting bank balance, optional supporting note.
- **Multi-Week Plan**: An ordered sequence of per-gameweek decisions across the horizon. Attributes: ordered steps (each with action, transfer details, hit cost, weekly predicted score, bank after, FTs after), cumulative hits, total score, baseline score, net gain vs baseline, optional alternative paths.
- **Chip Plan**: Per-chip target gameweek and supporting metric for the four chips (TC, BB, FH, WC), or an explicit "no recommendation" entry per chip.
- **Cached Analysis Result**: The full output produced for a given (target gameweek, horizon, budget, Team ID, no-understat) input tuple, retained for ~1 hour.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A manager with a valid Team ID can obtain a complete weekly recommendation (transfer/hold + captain + multi-week plan + chip cards) for the upcoming gameweek in under **2 minutes** end-to-end on a first run, and under **5 seconds** on a cached run.
- **SC-002**: A manager with no Team ID can obtain a complete from-scratch squad (15 players + starting XI + captain) within **2 minutes** on a first run.
- **SC-003**: Across a representative season of past gameweeks, the analyzer's per-player point predictions achieve a **mean absolute error within 15% of the season's mean weekly score**, evaluated in backtest mode without look-ahead bias.
- **SC-004**: When the optional xG/xA data source is unavailable, the system still produces a complete recommendation with a documented MAE increase of no more than **8 percentage points** versus the with-Understat baseline, and a visible indicator that the source was skipped.
- **SC-005**: The system serves at least **95% of repeat queries within the cache window from cache** (no model retraining, no remote fetches).
- **SC-006**: 100% of from-scratch squads produced satisfy every FPL composition rule (15 players, 2/5/5/3 by position, ≤3 per club, ≤£100m by default).
- **SC-007**: 100% of multi-week plans produced respect the free-transfer cap of 5, never produce a negative bank balance, and apply hit costs of -4 per extra transfer correctly.
- **SC-008**: A manager who follows the analyzer's recommendation across a representative 10-gameweek backtest **outperforms a hold-every-week baseline by at least 1 point per gameweek on average**.
- **SC-009**: First-time users with basic FPL knowledge can complete a full weekly analysis flow (open the app, enter their Team ID, click Run, and act on the recommendation) without consulting external documentation in **under 5 minutes**.

## Assumptions

- **Single-user, local-first tool**. The analyzer is intended to be used by an individual manager on their own machine. Multi-tenant hosting, user accounts, and authentication are out of scope for v1.
- **No persistent state between sessions**. The Team ID and other inputs are re-entered each session; only the ~1-hour analysis cache persists. A future iteration may add lightweight profile persistence.
- **Official FPL public data feed is the primary source of truth**. Player, fixture, and squad data come from the official public endpoints. The only optional secondary source is Understat for xG/xA.
- **Public data is post-deadline only**. Public FPL endpoints reflect each manager's squad as of the last finished gameweek deadline. Mid-week transfers made after that deadline are not visible to the analyzer until the next deadline passes; this is surfaced as a visible warning, not a bug.
- **Default planning horizon is 3 gameweeks** (range 1-5). Longer horizons compound prediction error and exhaust beam-search budget without proportional gain.
- **Default budget is £100m** for from-scratch mode (the FPL season-start budget). Custom budgets are supported for what-if analysis.
- **Differential ownership threshold defaults to 10%**. Players above this are considered widely-owned and excluded from the differentials panel.
- **No look-ahead bias in backtest mode**. Backtests are evaluated using only data available before each evaluated gameweek's deadline.
- **Display-only toggles do not invalidate cache**. Toggles like "hide differentials" or "hide a chip card" only affect rendering, not the analysis itself.
- **Existing scaffolding will be replaced**. `specs/007-fpl-analyzer/fpl_main.py`, `fpl_gui.py`, and `requirements.txt` exist as shells that import from a `fpl/` package which is not yet present. The `fpl/` package is in scope to design and build as part of this feature; the existing shims may be kept as-is, refactored, or replaced during planning.

## Out of Scope

- **Multi-tenant hosting / user accounts / authentication.** Single-user local tool only.
- **Push notifications / scheduled reminders** ahead of deadlines.
- **Mobile-native applications.** A web UI is in scope; native iOS/Android apps are not.
- **Live in-play data** (events as they happen during fixtures). Analysis runs against the post-deadline state only.
- **Automated transfer execution** against the FPL site. The tool recommends; the manager executes manually.
- **Cup competitions outside the standard Premier League FPL game** (e.g., FPL Draft, custom leagues with non-standard rules).
- **Historical season analytics** beyond what the prediction model needs for training.
