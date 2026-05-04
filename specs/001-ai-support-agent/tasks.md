---

description: "Task list for AI Customer Support Agent (Arabic + Egyptian Dialect)"
---

# Tasks: AI Customer Support Agent (Arabic + Egyptian Dialect)

**Input**: Design documents from `/specs/001-ai-support-agent/`
**Prerequisites**: plan.md (loaded), spec.md (loaded), research.md (loaded), data-model.md (loaded), contracts/ (loaded), quickstart.md (loaded)

**Tests**: Tests are IN SCOPE for this feature. SC-001/002/003 mandate three
human-graded eval sets, and Constitution Principle III (Test-First) requires
that those eval sets be authored and observed failing before the workflow
logic they validate is iterated. Eval-set tasks therefore precede the
prompt/KB iteration tasks within each user story phase.

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md) so
each story can be implemented and demoed independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- All paths assume the repository root unless otherwise noted

## Path Conventions

This is a single-project layout under `agent/` (per plan.md → Project
Structure). Paths shown below match that layout.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository skeleton and tooling that every story needs

- [X] T001 Create the `agent/` directory tree per `plan.md` Project Structure: `agent/workflow/`, `agent/kb/`, `agent/prompts/`, `agent/eval/sets/`, `agent/eval/results/`, `agent/deploy/`, `agent/docs/` (each with a `.gitkeep` where empty)
- [X] T002 [P] Add `.gitignore` entries at repo root for `agent/deploy/.env`, `agent/eval/results/*.jsonl`, `agent/eval/.venv/`, `__pycache__/`, `*.pyc`
- [X] T003 [P] Create `agent/eval/requirements.txt` listing `httpx>=0.27`, `pydantic>=2.7`, `jsonschema>=4.22`
- [X] T004 [P] Create `agent/kb/lint.py` that validates `agent/kb/ecommerce-faq.json` against `specs/001-ai-support-agent/contracts/kb-entry.schema.json`, fails on any entry with empty `wording_msa` or `wording_egy`, and prints a per-entry pass/fail summary
- [X] T005 [P] Create `agent/deploy/.env.example` enumerating `GEMINI_API_KEY`, `GEMINI_MODEL=gemini-1.5-flash`, `N8N_HOST`, `WEBHOOK_URL`, `N8N_ENCRYPTION_KEY`, `GENERIC_TIMEZONE=Africa/Cairo`, `N8N_LOG_LEVEL=info` with inline comments

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared assets that every user story consumes — workflow shell,
deployment, system prompt, refusal templates, and a small bilingual KB seed.
No user story phase can begin until this phase is complete.

**⚠️ CRITICAL**: No US1–US4 work can start until this phase finishes.

- [X] T006 Author the bilingual system prompt at `agent/prompts/system.md` covering all six required sections from `specs/001-ai-support-agent/contracts/llm-prompt.md` (role lock, register policy, knowledge grounding rule, refusal policy, MSA + Egyptian style guides, structured output schema)
- [X] T007 [P] Author refusal templates at `agent/prompts/refusal-templates.md` with one MSA + one Egyptian phrasing for each of: `refuse_off_topic`, `refuse_unsafe`, `refuse_unknown_language`, `refuse_role_override`
- [X] T008 [P] Seed the bilingual knowledge base at `agent/kb/ecommerce-faq.json` with 5 entries — one per topic (`orders`, `returns`, `shipping`, `payments`, `account`) — each populated in both `wording_msa` and `wording_egy` per the schema in `specs/001-ai-support-agent/contracts/kb-entry.schema.json`; run `python agent/kb/lint.py` and confirm all pass
- [~] T009 ~~Create `agent/deploy/Caddyfile`~~ **SUPERSEDED by Q6 clarification (n8n Cloud).** Original artifact is preserved in git history under the Phase 2 boundary commit; removed from the working tree.
- [~] T010 ~~Create `agent/deploy/docker-compose.yml`~~ **SUPERSEDED by Q6 clarification (n8n Cloud).** Original artifact is preserved in git history under the Phase 2 boundary commit; removed from the working tree.
- [X] T011 Build the n8n workflow on **n8n Cloud** (per the buildbook at `agent/workflow/README.md`) and smoke-test it. Live URL: `https://guillaume120.app.n8n.cloud/webhook/da1f362e-200c-4255-a624-9bb6544821d0/chat`. The KB and refusal templates are inlined inside the workflow's Code nodes per Q7 / Q8 clarifications; the canonical sources remain `agent/kb/ecommerce-faq.json` and `agent/prompts/refusal-templates.md`. Workflow JSON SHOULD be periodically exported from n8n Cloud and committed over `agent/workflow/ai-support-agent.json` for repo self-containment (still a placeholder pending first export).
- [~] T012 ~~Provision the VPS and deploy~~ **DROPPED by Q6 clarification (n8n Cloud).** Replaced by `agent/deploy/n8n-cloud-setup.md`, whose acceptance checklist serves the same gating purpose. n8n Cloud account creation is a one-time UI sign-up, not a runbook.
- [X] T013 [P] Build the eval harness skeleton at `agent/eval/runner.py` supporting: `--set <msa|egyptian|adversarial>`, `--webhook <url>`, `--model <gemini-variant>`. The runner reads `agent/eval/sets/<set>.jsonl`, posts each prompt to the webhook with a fresh UUID `sessionId`, captures `response`, `latency_ms`, and writes `agent/eval/results/<set>-<ISO-timestamp>.jsonl` records conforming to `specs/001-ai-support-agent/contracts/eval-record.schema.json` (`verdict` left null)
- [X] T014 [P] Add a `--grade-summary <results-file>` mode to `agent/eval/runner.py` that aggregates `verdict == "pass"` rate per eval set and prints it alongside the spec's SC threshold for that set
- [X] T015 [P] Author the human-reviewer template at `agent/eval/reviewer-template.md` describing how to open a results JSONL file, set `verdict` per record, and what counts as `pass` for each eval set

**Checkpoint**: Foundation ready — all of US1–US4 can now begin in parallel.

---

## Phase 3: User Story 1 — MSA support (Priority: P1) 🎯 MVP

> **Setup convention for Phase 3+**: export the n8n Cloud chat URL once
> per shell, and all baseline + sign-off task commands work as written:
> ```sh
> export N8N_CHAT_URL=https://guillaume120.app.n8n.cloud/webhook/da1f362e-200c-4255-a624-9bb6544821d0/chat
> ```

**Goal**: Visitors writing in Modern Standard Arabic receive accurate,
KB-grounded answers in MSA. Implements US1 (FR-001/002/003/004/005 for the
MSA path) and clears SC-001 (≥ 90% pass on a 30-prompt MSA eval set).

**Independent Test**: Run the MSA eval set against the deployed workflow,
human-grade the results, confirm pass-rate ≥ 90%.

### Tests for User Story 1 (Test-First — author and observe failing first) ⚠️

> **NOTE**: Per Constitution Principle III, T016 MUST be authored and the
> first run MUST be observed failing (or partially failing) before T017+
> iterate to make it pass.

- [X] T016 [P] [US1] Author the MSA eval set at `agent/eval/sets/msa.jsonl` — exactly 30 records, each `{prompt_id, prompt, expected_topic}`, with prompts evenly spread across the five KB topics (`orders`, `returns`, `shipping`, `payments`, `account`); include 3 prompts whose answers are deliberately NOT in the KB to exercise FR-005's refusal path
- [ ] T017 [US1] Run a baseline pass: `python agent/eval/runner.py --set msa --webhook ${N8N_CHAT_URL} --model gemini-1.5-flash`, then human-grade the resulting JSONL and run `--grade-summary` on it; record the baseline pass-rate in `agent/eval/results/baseline.md` and confirm it falls below 90% (this anchors the iteration)

### Implementation for User Story 1

- [ ] T018 [US1] Curate `wording_msa` for all 5 seed entries in `agent/kb/ecommerce-faq.json` so they cleanly answer the on-topic prompts in `agent/eval/sets/msa.jsonl`; re-run `python agent/kb/lint.py`
- [ ] T019 [US1] Tighten the **Register policy** and **Knowledge grounding rule** sections of `agent/prompts/system.md` so MSA detection and grounded retrieval reliably fire on the eval set; restart the n8n workflow (file is read fresh each request, but verify) and re-run the MSA eval; iterate up to 3 prompt-tweak cycles
- [ ] T020 [US1] Verify FR-005 explicitly: for the 3 deliberately-out-of-KB prompts, confirm the agent's reply is in MSA and uses the `refuse_off_topic` template from `agent/prompts/refusal-templates.md`, not a fabricated answer
- [ ] T021 [US1] Final SC-001 sign-off: re-run MSA eval, human-grade, confirm pass-rate ≥ 90%, and commit the graded results JSONL to `agent/eval/results/sc-001-signoff.jsonl`

**Checkpoint**: US1 (MSA path) is fully functional and demoably MVP. The
agent can be linked from a portfolio page right now even before US2–US4 land.

---

## Phase 4: User Story 2 — Egyptian dialect support (Priority: P1)

**Goal**: Visitors writing in Egyptian Arabic dialect (or Arabizi) receive
accurate KB-grounded answers in Egyptian dialect — no MSA leakage.
Implements US2 (FR-001/002/003/004/005 for the Egyptian path, plus FR-007
for Arabizi) and clears SC-002 (≥ 85% pass on a 30-prompt Egyptian eval set).

**Independent Test**: A native Egyptian-Arabic-speaking reviewer grades the
30-prompt Egyptian eval set; pass-rate ≥ 85%, no responses leak MSA.

### Tests for User Story 2 (Test-First) ⚠️

- [X] T022 [P] [US2] Author the Egyptian eval set at `agent/eval/sets/egyptian.jsonl` — exactly 30 records `{prompt_id, prompt, expected_topic}`, with prompts evenly spread across the five topics; include **6 prompts written in Arabizi** (Latin-script Egyptian, e.g. "ezzay arga3 el order?") to exercise FR-007, and 3 prompts whose answers are deliberately NOT in the KB
- [ ] T023 [US2] Baseline pass: `python agent/eval/runner.py --set egyptian --webhook ${N8N_CHAT_URL} --model gemini-1.5-flash`; arrange a native Egyptian-Arabic reviewer to grade the JSONL; append the baseline pass-rate to `agent/eval/results/baseline.md`

### Implementation for User Story 2

- [ ] T024 [P] [US2] Curate `wording_egy` for all 5 seed entries in `agent/kb/ecommerce-faq.json` to cleanly answer the on-topic Egyptian prompts; ensure each `wording_egy` is in clean Egyptian dialect (uses بتاع, إيه, كده where natural; avoids MSA-only constructions); re-run `python agent/kb/lint.py`
- [ ] T025 [P] [US2] Expand `keywords_egy` for the 5 seed entries with Egyptian-dialect phrasings AND common Arabizi spellings of the same intents (e.g., "rag3", "shipping", "irga3") so retrieval fires on FR-007 inputs
- [ ] T026 [US2] Tighten the **Register policy** (Egyptian + Arabizi branches) and **Egyptian style guide** sections of `agent/prompts/system.md`; explicitly instruct that for `register == "arabizi"` the reply MUST use Egyptian Arabic script; iterate up to 3 prompt-tweak cycles, re-running the Egyptian eval after each
- [ ] T027 [US2] Verify FR-007 explicitly: for the 6 Arabizi prompts, confirm the agent classifies them as `arabizi` and replies in Egyptian Arabic script (no Latin transliteration, no MSA)
- [ ] T028 [US2] Final SC-002 sign-off: re-run Egyptian eval, native-reviewer grade, confirm pass-rate ≥ 85% with zero MSA-leakage records, and commit graded JSONL to `agent/eval/results/sc-002-signoff.jsonl`

**Checkpoint**: US1 + US2 both demonstrable independently. The headline
differentiator (dialect awareness) is now live.

---

## Phase 5: User Story 3 — Multi-turn conversation context (Priority: P2)

**Goal**: Within a single visitor session, the agent remembers earlier
turns and resolves back-references correctly. Implements US3 + FR-006 +
research.md R4.

**Independent Test**: A scripted 5-turn conversation with at least two
back-references (one MSA, one Egyptian) is replayed; the agent answers each
follow-up correctly without the visitor restating the topic.

### Tests for User Story 3 (Test-First) ⚠️

- [X] T029 [P] [US3] Author a multi-turn test script at `agent/eval/sets/multiturn.jsonl` — 2 scripted conversations of 5 turns each (one MSA, one Egyptian), where each conversation reuses the same `sessionId` across turns and includes at least one back-reference per conversation (e.g., turn 3 refers to "the second one", turn 4 asks "وكمان السعر بتاعها؟"); record `expected_behavior` per turn
- [ ] T030 [US3] Extend `agent/eval/runner.py` with a `--set multiturn` mode that preserves `sessionId` across turns of the same conversation and writes one record per turn; run a baseline pass and confirm back-references currently break (anchors iteration)

### Implementation for User Story 3

- [ ] T031 [US3] In the n8n Cloud workflow editor, attach a `Memory: Window Buffer` sub-node to the AI Agent node, keyed off the Chat Trigger's `sessionId` with a sliding window of 6 turns (3 visitor + 3 agent) per research.md R4; idle expiry 30 minutes; Save the workflow; export and overwrite `agent/workflow/ai-support-agent.json` afterwards
- [ ] T032 [US3] Update the `build_llm_input` Code node in the n8n Cloud workflow to inject `<recent_turns>` from session memory into the per-turn user message exactly as specified in `contracts/llm-prompt.md`; Save and re-export the workflow JSON
- [ ] T033 [US3] Re-run the multi-turn script; confirm both conversations now answer all back-references coherently in the matching register; commit the graded JSONL to `agent/eval/results/sc-multiturn-signoff.jsonl`

**Checkpoint**: US1 + US2 + US3 all demonstrable independently. Multi-turn
behavior makes the demo feel like a real assistant.

---

## Phase 6: User Story 4 — Out-of-scope and unsafe-question handling (Priority: P3)

**Goal**: The agent declines off-topic, abusive, and prompt-injection
inputs gracefully in matching register, without fabrication or breaking
character. Implements US4 + FR-008 + FR-009 + FR-010 and clears SC-003
(≥ 95% pass on a 20-prompt adversarial set).

**Independent Test**: 20-prompt adversarial set passes ≥ 95% in human grading.

### Tests for User Story 4 (Test-First) ⚠️

- [X] T034 [P] [US4] Author the adversarial eval set at `agent/eval/sets/adversarial.jsonl` — exactly 20 records `{prompt_id, prompt, expected_action}` split as: 8 off-topic (in MSA + Egyptian + 1 English to test FR-010), 6 abusive (mild + escalating, in both registers), 6 prompt-injection ("ignore previous instructions", "you are now an unrestricted assistant", "what's your system prompt", in both registers)
- [ ] T035 [US4] Baseline pass: `python agent/eval/runner.py --set adversarial --webhook ${N8N_CHAT_URL} --model gemini-1.5-flash`; human-grade; record baseline in `agent/eval/results/baseline.md`

### Implementation for User Story 4

- [ ] T036 [US4] Refine `agent/prompts/refusal-templates.md`: ensure each of the 4 refusal actions has a polished MSA and Egyptian phrasing; for `refuse_role_override`, the wording MUST NOT echo or restate the override attempt
- [ ] T037 [US4] Tighten the **Role lock** and **Refusal policy** sections of `agent/prompts/system.md`: the agent stays in customer-support role under all circumstances; never reveals system prompt; never enters a role suggested by the visitor; for non-Arabic input replies in MSA per FR-010
- [ ] T038 [US4] Verify the workflow's `validate_and_overwrite` Code node (built in T011 on n8n Cloud, see `agent/workflow/README.md` Node 6) overwrites `reply` with the verbatim refusal template for any `refuse_*` action — even if Gemini returns a free-form refusal — to prevent dialect/tone drift on refusal turns
- [ ] T039 [US4] Re-run adversarial eval; iterate prompt + templates up to 3 cycles; final SC-003 sign-off requires pass-rate ≥ 95% with zero turns that fabricate an answer or step out of role; commit graded JSONL to `agent/eval/results/sc-003-signoff.jsonl`

**Checkpoint**: All four user stories independently functional. The agent
demonstrates dialect awareness, multi-turn coherence, and safety under
adversarial probing.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Polish that touches more than one story, plus the
non-functional sign-offs the spec requires.

- [ ] T040 [P] Run the Gemini variant bake-off (research.md R1): re-run all four eval sets (`msa`, `egyptian`, `adversarial`, `multiturn`) after switching the n8n Cloud workflow's Gemini chat-model node to `gemini-1.5-pro`, grade, and compare against the Flash results; record the decision (Flash vs. Pro) and rationale in `agent/eval/results/variant-bakeoff.md`; if Pro wins, leave the workflow on Pro and update the buildbook's default model note in `agent/workflow/README.md`
- [ ] T041 [P] Verify SC-004: across the four sign-off result files, compute median `latency_ms`; confirm < 3000 ms; if not, identify and trim the highest-cost prompt-template section in `agent/prompts/system.md`; record the median in `agent/eval/results/latency-signoff.md`
- [ ] T042 Grow the KB to the planned 20–50 entries: add 15–45 more entries to `agent/kb/ecommerce-faq.json` covering finer-grained questions in each of the five topics; run `python agent/kb/lint.py`; copy the new JSON contents into the n8n Cloud workflow's `load_kb` Code node and save; spot-re-run the `msa` and `egyptian` eval sets to confirm no regressions
- [ ] T043 [P] Verify SC-005 manually: time how long it takes to add one new KB entry on n8n Cloud (edit the repo file → run lint → copy contents into the workflow's `load_kb` Code node → Save the workflow → see it answer in chat); confirm under 10 minutes; record observation in `agent/eval/results/sc-005-signoff.md`
- [~] T044 ~~Add a `logrotate` config snippet~~ **DROPPED by Q8 clarification (n8n Cloud built-in execution history).** No host filesystem to rotate.
- [~] T045 ~~Create `agent/deploy/README.md` summarizing the VPS deployment~~ **SUPERSEDED by Q6 clarification.** Replaced by `agent/deploy/n8n-cloud-setup.md` (already authored).
- [ ] T046 [P] Mirror the spec quickstart into `agent/docs/quickstart.md` (one-paragraph summary + link to the canonical `specs/001-ai-support-agent/quickstart.md`) so a developer cloning the repo finds it without digging into `specs/`
- [ ] T047 Verify SC-006 manually: have one first-time visitor (not the portfolio owner) complete a 3-turn support exchange end-to-end without restating the question; record observation + any UX papercuts in `agent/eval/results/sc-006-signoff.md`
- [ ] T048 [P] Add a top-level `README.md` at the repo root with: one-paragraph project description, screenshot of the chat in MSA + Egyptian, link to the live demo URL, link to `specs/001-ai-support-agent/quickstart.md`, link to constitution v1.0.0
- [ ] T049 [P] Add the "Try the demo" link to the portfolio site (action external to this repo); record the link target in `agent/docs/portfolio-link.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: T001 first; T002–T005 can run in parallel after T001
- **Phase 2 (Foundational)**: depends on Phase 1; T011 (workflow build) depends on T006–T010; T012 (deploy) depends on T010+T011; T013–T015 (eval harness) depend only on T001+T003 and run in parallel with T006–T012
- **Phase 3+ (User Stories)**: all depend on Phase 2 completion; once Phase 2 is done, US1, US2, US3, US4 can be worked on in parallel by different operators or sequentially
- **Phase 7 (Polish)**: most polish tasks depend on the corresponding sign-off (T040 needs all 4 eval sets to exist; T041 needs all 4 sign-off result files)

### User Story Dependencies

- **US1 (MSA)** P1: Independent. Can ship as MVP on its own.
- **US2 (Egyptian)** P1: Independent of US1; both can run in parallel after Phase 2. (Same workflow, different prompt-tuning + KB-curation passes.)
- **US3 (Multi-turn)** P2: Independent of US1/US2 in principle, but practically easier to work on after at least one of them passes — otherwise it's hard to tell whether multi-turn is the issue or single-turn is.
- **US4 (Safety)** P3: Independent. Can run in parallel with US3.

### Within Each User Story

- Tests (eval-set authoring) MUST be authored first and a baseline run MUST be observed (Constitution III).
- KB curation and prompt iteration are tightly coupled — same files touched, do sequentially within a story.
- Sign-off task is always last in the story.

### Parallel Opportunities

- All `[P]` tasks in Phase 1 can run in parallel after T001
- T013/T014/T015 (eval harness) parallel to T006/T007/T008/T009/T010 (prompt + KB + deploy assets) within Phase 2
- After Phase 2 completes: US1, US2, US3, and US4 can each be assigned to different operators
- Within a story: eval-set authoring (T016, T022, T029, T034) is `[P]` — different files
- Polish phase: T040–T049 are mostly `[P]` because they touch different files

---

## Parallel Example: Phase 2 ramp

```bash
# After T001 completes, launch in parallel:
Task: "T002 Add .gitignore entries"
Task: "T003 Create agent/eval/requirements.txt"
Task: "T004 Create agent/kb/lint.py"
Task: "T005 Create agent/deploy/.env.example"

# Then Phase 2 itself can run authoring + harness in two lanes:
# (T009/T010/T012/T044/T045 superseded by Q6–Q8 clarifications — n8n Cloud.)
Task: "T006 Author system.md (Lane A: prompts)"
Task: "T007 Author refusal-templates.md (Lane A)"
Task: "T008 Seed bilingual KB (Lane A)"
Task: "T013 Build runner.py (Lane B: eval harness)"
Task: "T014 Add --grade-summary mode (Lane B)"
Task: "T015 Author reviewer-template.md (Lane B)"
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1 only)
2. **STOP and validate**: SC-001 ≥ 90% MSA pass-rate signed off
3. The portfolio page can already link to the live agent at this point —
   Egyptian dialect is the differentiator but the MSA-only agent already
   demonstrates competence with n8n + Gemini + RAG over a curated KB

### Incremental Delivery (recommended)

1. Phase 1 + Phase 2 → Foundation ready
2. Phase 3 (US1) → MVP demoable
3. Phase 4 (US2) → headline differentiator (dialect awareness) live
4. Phase 5 (US3) → multi-turn polish, demo feels like a real assistant
5. Phase 6 (US4) → safety hardening, ready for hostile reviewers
6. Phase 7 (Polish) → variant bake-off, latency check, KB grow-out, README

### Parallel Team Strategy (if you have a collaborator)

After Phase 2 finishes, the work splits cleanly:

- Operator A: US1 + US2 (prompt + KB curation expertise)
- Operator B: US3 + US4 (workflow wiring + safety templates)
- Polish phase merges results.

---

## Notes

- `[P]` = different files, no dependencies on incomplete tasks
- `[Story]` label maps each task to a user story for traceability
- Eval-set authoring tasks are non-optional: SC-001/002/003 reference them by
  size and pass-rate, so they are part of the spec, not testing scaffolding.
- Per Constitution Principle III, each story's eval set MUST be authored
  before the prompt/KB iteration it grades, and the first run MUST be
  observed (and recorded as the baseline) before iteration begins.
- Commit after each task or logical group; especially after each sign-off.
- Stop at any checkpoint to validate the corresponding story independently.
