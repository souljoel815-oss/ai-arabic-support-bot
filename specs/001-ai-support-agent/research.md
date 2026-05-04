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

## R2. Knowledge base storage shape

**Question**: Where should the bilingual KB live, and in what shape?

**Decision**: A single **JSON file** at `agent/kb/ecommerce-faq.json`, loaded
into the workflow at execution time via n8n's "Read Binary File" or HTTP
fetch from the local filesystem. Conforms to `kb-entry.schema.json`
(Phase 1 contract).

**Rationale**:
- Scale is 20–50 entries — a flat JSON file is trivially loaded into memory,
  searchable by keyword, and version-controllable in git.
- FR-011 requires editing without code changes — editing a JSON file in a
  text editor (or via the VPS's web shell) satisfies this.
- Avoids dragging in Postgres/SQLite for a workload that doesn't need
  indexing, joins, or concurrent writes.
- Deterministic: the entire KB is auditable in a single commit.

**Alternatives considered**:
- **n8n built-in Data Tables / variables**: Tied to a single workflow, less
  greppable, harder to diff across versions; rejected.
- **External Postgres**: Massive overkill for ~50 rows; rejected on
  Principle IV (Simplicity & YAGNI).
- **Separate MSA and Egyptian files**: Risks drift between registers; the
  single-file-with-both-fields shape encodes the FR-014 invariant in the
  schema itself.

**Action**: Phase 1 produces the JSON Schema in `contracts/kb-entry.schema.json`
and a sample seed file with 5–10 entries; the full 20–50-entry KB is a
Phase 2 authoring task.

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

**Decision**: **JSON Lines file** at `/var/log/ai-support-agent/turns.jsonl`
on the VPS, written by a "Write to File" node at the end of each workflow
execution. n8n's own execution history stays as a secondary, automatic log.

**Rationale**:
- One line per turn → trivially appendable, greppable, and rotatable with
  `logrotate`.
- Conforms to a contract (`contracts/eval-record.schema.json` covers eval
  records; turn logs follow a parallel small schema documented in
  `data-model.md`).
- Visitors are anonymous (only `sessionId`), so no PII handling is needed.

**Alternatives considered**:
- **Database table**: Adds dependency and migration concerns for content
  the portfolio owner mostly never reads; rejected.
- **Rely solely on n8n execution history**: Hard to query in aggregate;
  loses structure (n8n stores entire execution payloads, not turn-shaped
  records); rejected.
- **External log aggregator (Loki, ELK)**: Overkill for portfolio-grade;
  rejected on Principle IV.

**Action**: Phase 2 task adds the Write-to-File node with explicit field
ordering (`{ts, sessionId, turnIndex, register, visitorText, agentText, kbEntryIds}`).

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

## R7. VPS deployment shape

**Question**: How is the n8n workflow actually deployed and made stably
reachable on a public URL?

**Decision**: **Docker Compose on a $5–10/mo VPS** (Hetzner CX11 or
DigitalOcean basic droplet, Ubuntu 22.04 LTS), with:
- Caddy as a reverse proxy fronting n8n on port 443 with auto-renewing
  Let's Encrypt certificates.
- A subdomain such as `support-demo.<owner-domain>` pointing to the VPS.
- n8n configured with `N8N_HOST`, `WEBHOOK_URL`, and a randomly generated
  encryption key.
- A `deploy/` directory in the repo containing `docker-compose.yml`,
  `.env.example`, and a `README.md` with copy-pasteable setup steps.

**Rationale**:
- Caddy + Docker Compose is the lowest-overhead path to TLS + a stable URL
  on a single small VPS. It is also a well-trodden n8n self-host setup,
  which keeps the demo focused on the agent rather than novel infra.
- Including `deploy/` in the repo is itself part of the portfolio signal
  (reviewers can read the README).

**Alternatives considered**:
- **Bare nginx + certbot**: More moving parts than Caddy; rejected.
- **Traefik**: Overkill for one service; rejected on Principle IV.
- **n8n Cloud Starter**: Already rejected by the spec via clarification Q5;
  noted here for traceability.

**Action**: Phase 1 produces `quickstart.md` with the VPS setup steps;
Phase 2 produces `deploy/docker-compose.yml` and `.env.example`.

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
