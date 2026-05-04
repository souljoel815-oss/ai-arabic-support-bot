# Phase 0 Research: AI Customer Support Agent

**Feature**: 001-ai-support-agent
**Date**: 2026-05-04
**Purpose**: Resolve technical unknowns surfaced by `plan.md` so that Phase 1
design and Phase 2 task generation can proceed with locked decisions.

---

## R1. Specific Gemini variant (Pro vs. Flash vs. 2.0)

**Question**: The spec locks the LLM family to Google Gemini but defers the
specific variant to a planning bake-off. Which variant should the workflow
target?

**Decision**: Default to **`gemini-1.5-flash`** for both register-detection and
answer-generation, with a fallback path to **`gemini-1.5-pro`** if the
flash bake-off fails to clear SC-002 (≥ 85% Egyptian-dialect correctness).

**Rationale**:
- Cost: Flash is roughly an order of magnitude cheaper per token than Pro,
  which matters because the eval suite (~80 prompts × multiple iterations)
  and any future demo traffic will run repeatedly.
- Latency: Flash typically produces first-token latency comfortably under
  the SC-004 budget of 3 seconds at small response lengths; Pro is closer to
  the budget, especially with longer prompts.
- Quality: For grounded retrieval-style answers (the agent retrieves a KB
  entry's pre-translated wording and stitches a short reply), Flash's
  multilingual quality is sufficient. The hardest task — generating Egyptian
  dialect from scratch — is **avoided by FR-014** (pre-translated wording).
- Fallback: If the eval bake-off shows Flash leaks MSA into Egyptian replies
  more than 15% of the time, a single Gemini variable in `.env` swaps the
  workflow to Pro.

**Alternatives considered**:
- **Gemini 1.5 Pro as default**: Higher quality but ~10× cost; rejected as
  premature given FR-014 reduces the model's burden.
- **Gemini 2.0 Flash (if GA)**: Promising on latency and reasoning, but
  release-cadence/availability inside n8n's Google Gemini node is not
  guaranteed; treat as a future upgrade path, not a launch dependency.
- **Mixed: Flash for detection, Pro for generation**: Adds workflow
  complexity (two API keys, two nodes, two failure modes) for marginal
  quality gain when the heavy lifting is retrieval, not generation.

**Action**: A single `GEMINI_MODEL` env var consumed by the n8n workflow,
defaulting to `gemini-1.5-flash`. The variant bake-off is a Phase 2 task
("Run eval suite against `flash` and `pro`, record SC-001/002/003 pass rates,
commit results in `eval/results/`").

---

**Decision (revised post-T011, per Q9 clarification)**: The actual locked
variant is **`gemini-2.5-flash-lite`**, not `gemini-1.5-flash`. The
operator picked it during T011 (workflow build on n8n Cloud) — by the
time the workflow was wired up, the Gemini 2.5 family had become
generally available, and `gemini-2.5-flash-lite` strictly dominates
`gemini-1.5-flash` on both axes (cheaper, faster) while remaining
adequate for grounded retrieval over a pre-translated KB.

First-run baselines (the four eval sets posted at 2026-05-04 06:19–06:23)
showed:
- p50 latency ~2048 ms across all 90 records (SC-004 budget 3000 ms)
- 0 detected MSA leakage in Egyptian replies on heuristic pre-screen
- 0 prompt-leakage / persona-break under 6 injection attempts
- 4 transient HTTP 503s from upstream Gemini overload (capacity, not bug)
- 1 fabrication-via-overgeneralization in the Egyptian set (refused→answered)
- 3 ad-hoc refusals in the adversarial set instead of canonical templates

The 503s are mitigated by adding **retry-on-error** to the AI Agent node
in the n8n Cloud editor (3 tries, 2-second backoff) — a workflow-side
fix, not a model choice.

**Upgrade path**: Phase 7 T040 is reframed as a stability/quality
upgrade test against `gemini-2.5-flash` (regular tier, ~3× cost) and
`gemini-2.5-pro` (~10× cost), to be triggered only if SC-001 or SC-002
fail human grading on `2.5-flash-lite`.

---

## R2. Knowledge base storage shape

**Question**: Where should the bilingual KB live, and in what shape?

**Decision (revised — n8n Cloud)**: The bilingual KB is **inlined as a JSON
literal inside the workflow's `load_kb` Code node** on n8n Cloud. The
repo's `agent/kb/ecommerce-faq.json` remains the **authoritative,
version-controlled source of truth**; the operator copy-pastes its
contents into the Code node when updating. Conforms to
`contracts/kb-entry.schema.json` (Phase 1 contract).

**Rationale**:
- Scale is 20–50 entries — fits comfortably as an inline JSON literal in
  a Code node (a few KB).
- n8n Cloud has no host filesystem mount, so the original "read JSON file
  from `/data/...`" approach (this section's earlier decision) is not
  available.
- FR-011 ("update KB content without code changes") is still met:
  refreshing the inline JSON is a content edit, not a JavaScript code
  edit. SC-005 ("under 10 minutes editor time") is comfortably met by
  copy-paste into the n8n editor.
- The repo file remains the authoritative version-controlled source so
  KB content stays diffable, reviewable in PRs, and recoverable from git.

**Alternatives considered (post-n8n-Cloud)**:
- **Public raw GitHub URL fetch**: `load_kb` HTTP-GETs the JSON file from
  `raw.githubusercontent.com`. Eliminates the copy-paste step and makes
  KB updates fully zero-touch, but requires the repo to be public (or a
  PAT). Held in reserve as a v2 upgrade if SC-005 fails in practice.
- **n8n Data Tables**: stores rows in n8n itself; rejected because it
  decouples KB content from git history.
- **External Postgres**: still overkill; rejected.

**Original (pre-n8n-Cloud) decision and alternatives are preserved in the
Q5 → Q6 supersession recorded in spec.md → Clarifications.**

**Action**: Phase 2 (T008) produces the seed file at
`agent/kb/ecommerce-faq.json`; the operator copies its contents into the
workflow's `load_kb` Code node on n8n Cloud. The full 20–50-entry KB
grow-out is Phase 7 (T042).

---

## R3. Register / dialect detection strategy

**Question**: How does the agent decide whether an incoming visitor message
is MSA or Egyptian dialect (or Arabizi, or non-Arabic)?

**Decision**: **Single-LLM-call register detection**: the system prompt
instructs Gemini to first emit a structured tag for the detected register
(`msa` | `egyptian` | `arabizi` | `other`), then produce the answer in the
matching register. No separate detection step, no rule-based heuristic.

**Rationale**:
- Saves a round-trip / second LLM call → directly supports SC-004 latency.
- Gemini handles the four-way classification reliably enough on the eval
  set; misclassifications on borderline mixed-register inputs are tolerable
  per the edge case "agent picks the dominant register".
- Structured output (a leading JSON header in the response, parsed by an
  n8n Function node) is supported by Gemini's response-schema feature.
- Arabizi handling (FR-007) becomes "if detected register is `arabizi`,
  respond using the Egyptian wording" — a single conditional branch.

**Alternatives considered**:
- **Rule-based regex/lexicon classifier**: Brittle on Arabizi (Latin-script
  transliteration); would need a substantial lexicon to be useful; adds
  maintenance burden; rejected.
- **Two-call pipeline (classify, then answer)**: Doubles latency; rejected
  on SC-004.
- **Fine-tuned dialect classifier**: Massive overkill for a portfolio piece
  with 3-class output; rejected on Principle IV.

**Action**: The system prompt template (`prompts/system.md`) defines the
structured-response shape and the four register tags. The contract is
formalized in `contracts/llm-prompt.md`.

---

## R4. Conversation memory / multi-turn context (US3)

**Question**: How does the agent remember prior turns within a session
(US3, FR-006)?

**Decision**: Use **n8n's native chat session memory** via the AI Agent /
LangChain memory node configured with a sliding window of the **last 6
turns** (3 visitor + 3 agent), keyed by the Chat Trigger's `sessionId`.
Idle expiry is 30 minutes (per Assumptions).

**Rationale**:
- n8n's Chat Trigger already exposes `sessionId`; the AI Agent node has a
  built-in memory option that uses it. Zero glue code.
- A sliding window of 6 turns covers the typical multi-turn support flow
  (initial Q → clarifying follow-up → next question) without inflating
  prompt size.
- 30-minute idle expiry matches the Assumption already in the spec, which
  is restated by FR-006.

**Alternatives considered**:
- **Full transcript replay**: Larger prompts, higher cost, marginal benefit
  for a 3-turn typical session; rejected.
- **External vector store of past turns**: Complexity not justified for
  demo-grade scope; rejected on Principle IV.
- **No memory (single-turn only)**: Fails US3; rejected.

**Action**: Phase 2 task wires the AI Agent node's memory option to the
Chat Trigger's `sessionId`, with the 6-turn window encoded as a workflow
parameter.

---

## R5. Conversation logging

**Question**: FR-012 requires logging timestamps, visitor input, agent
reply, and detected register. Where do logs live?

**Decision (revised — n8n Cloud)**: Use **n8n's built-in execution
history**. Each workflow run automatically records every node's input
and output payloads, including the visitor turn, the structured Gemini
response, and the final reply. The dedicated `log_turn` Code node and
the `/var/log/ai-support-agent/turns.jsonl` sink are dropped.

**Rationale**:
- n8n Cloud has no host filesystem; writing JSONL to disk would require
  shelling out to an external service.
- The execution-history payloads contain everything FR-012 requires
  (timestamp, visitor input, agent reply, detected register, and chosen
  KB entry IDs — all present in node payloads). Aggregate queries are
  done via the Executions UI, which is acceptable for portfolio-grade
  reliability and demo-scale traffic.
- Removing the dedicated log node simplifies the workflow graph by one
  node and one Code-node failure mode.

**Alternatives considered (post-n8n-Cloud)**:
- **Webhook-out to an external logger** (Datadog, a Cloudflare Worker,
  Google Sheets): adds a dependency and an outbound-call failure mode
  for content the portfolio owner mostly never reads at scale; deferred
  as a v2 upgrade if FR-012 review needs become more demanding.
- **External log aggregator (Loki, ELK)**: still overkill; rejected.

**Original (pre-n8n-Cloud) JSONL decision is preserved by the Q8
clarification in spec.md → Clarifications.**

**Action**: No new node is added; the workflow's Respond to Webhook
node (after `validate_and_overwrite`) terminates the run, and n8n
Cloud's execution history captures the full per-turn payload for review.

---

## R6. Safety, prompt-injection resistance, and refusals

**Question**: How does the agent satisfy FR-008/FR-009 and US4 safety
scenarios?

**Decision**: A **layered defense** consisting of:
1. A locked-role system prompt that explicitly states the agent's role,
   forbids role override, and refuses to reveal its instructions.
2. Gemini's built-in safety filters (default thresholds).
3. A short curated list of **MSA + Egyptian refusal phrasings** in
   `prompts/refusal-templates.md`, which the system prompt instructs the
   model to draw from for off-topic / abusive / injection cases.
4. The structured-response contract: if the model emits a `refuse` action,
   the workflow returns the matching refusal template instead of a
   free-form generation.

**Rationale**:
- Pure prompt-based defense is sufficient for portfolio-grade and easy to
  iterate; heavyweight guardrail libraries are unnecessary for the
  adversarial set size (20 inputs).
- Pre-curated refusal templates eliminate dialect leakage and tone drift
  on refusal turns — a common embarrassment vector for live demos.

**Alternatives considered**:
- **External moderation API (Perspective, etc.)**: Adds a dependency and a
  network hop; not justified at this scale; rejected.
- **Policy-grammar enforcement (e.g., Guardrails AI)**: Heavyweight; harder
  to demo from inside n8n; rejected.
- **No structured refusal action**: Allows the model to free-form a refusal
  in the wrong dialect or tone; rejected on SC-003 risk.

**Action**: Phase 1 contract `contracts/llm-prompt.md` documents the
structured response shape including the `refuse` action; Phase 2 authors
the refusal templates and verifies them with adversarial-set evaluation.

---

## R7. Deployment shape

**Question**: How is the n8n workflow actually deployed and made stably
reachable on a public URL?

**Decision (revised)**: **n8n Cloud (managed)**. The workflow is built
and operated in the portfolio owner's n8n Cloud workspace at
`https://guillaume120.app.n8n.cloud`. The Chat Trigger exposes a public
chat URL that the portfolio site links to:
`https://guillaume120.app.n8n.cloud/webhook/da1f362e-200c-4255-a624-9bb6544821d0/chat`.

**Rationale**:
- TLS, uptime, scaling, and updates are handled by n8n Cloud — zero
  infrastructure work needed for portfolio-grade reliability.
- The portfolio's signal is "I built this on n8n", not "I run n8n
  myself"; managed hosting lets the work focus stay on the agent.
- Eliminates the entire VPS provisioning / DNS / Caddy / Docker
  surface area, simplifying onboarding for a reviewer who clones the
  repo (no infra prerequisites).

**Alternatives considered (post-revision)**:
- **Self-hosted on a small VPS** (the original Q5 / R7 decision): real
  signal of DevOps competence, but ongoing operational toll and a
  separate "things that can break during a demo" surface area.
  Superseded by Q6 in spec clarifications.
- **Bare nginx + certbot, Traefik, etc.**: moot under managed hosting.

**Original (pre-n8n-Cloud) decision is preserved by clarifications Q5 →
Q6 in spec.md.**

**Action**: Phase 1 (T011/T012) is replaced by:
- T011 (build the workflow) — done by the portfolio owner in the n8n
  Cloud editor; the workflow JSON SHOULD be exported and committed
  over `agent/workflow/ai-support-agent.json` for repo
  self-containment, but the live runtime is in n8n Cloud, not in the
  repo.
- T012 (provision VPS) — dropped. n8n Cloud account creation replaces
  it (a one-time UI sign-up, not a runbook).

The VPS-deploy artifacts authored before this revision
(`agent/deploy/Caddyfile`, `agent/deploy/docker-compose.yml`,
`agent/deploy/.env.example`, the previous `agent/deploy/README.md`)
are removed from the working tree but remain in git history under the
Phase 2 boundary commit for reference. They are replaced by a single
short `agent/deploy/n8n-cloud-setup.md`.

---

## R8. Evaluation methodology

**Question**: How are SC-001, SC-002, SC-003 actually measured?

**Decision**: A **Python eval harness** (`agent/eval/runner.py`) that:
1. Reads one of three eval-set JSONL files (`msa.jsonl`, `egyptian.jsonl`,
   `adversarial.jsonl`).
2. Posts each prompt to the Chat Trigger webhook with a fresh `sessionId`
   per prompt (so no cross-contamination).
3. Records the response, latency, and detected register into
   `eval/results/<set>-<timestamp>.jsonl` conforming to
   `contracts/eval-record.schema.json`.
4. The portfolio owner (and a native Egyptian-Arabic-speaking reviewer for
   the dialect set) then opens the JSONL output and fills the
   `reviewer-template.md` with pass/fail and notes; pass-rate is computed
   by a simple aggregation script.

**Rationale**:
- Automated rubric grading by another LLM was considered but adds a
  confounder ("did the grader judge correctly?") that defeats the purpose
  of the human-judged success criteria.
- JSONL + a thin runner is the lowest-ceremony way to keep eval results
  reproducible and version-controllable.

**Alternatives considered**:
- **LLM-as-judge auto-grading**: Faster but conflates model competence with
  evaluation competence; rejected for the headline metrics. May be added
  later as a regression-screen.
- **Notebook-based eval**: Harder to commit/diff; rejected.

**Action**: Phase 2 tasks generate the eval sets, the runner, and the
reviewer template.

---

## Summary of unresolved items

None. All Phase 0 questions resolved; no `[NEEDS CLARIFICATION]` markers
remain anywhere in the planning artifacts. Proceeding to Phase 1.
