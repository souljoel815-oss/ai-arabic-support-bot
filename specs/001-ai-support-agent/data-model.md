# Data Model: AI Customer Support Agent

**Feature**: 001-ai-support-agent
**Date**: 2026-05-04
**Source**: Derived from `spec.md` Key Entities + `research.md` decisions
(R2, R4, R5).

This document is descriptive — it specifies the shape and rules of each
entity. Concrete schemas (JSON Schema) for the externally visible ones live
in `contracts/`.

---

## Knowledge Entry

A unit of curated bilingual support content. Pre-translated into both
registers per FR-014.

**Fields**:

| Field | Type | Required | Notes |
|---|---|---|---|
| `id` | string | yes | Stable, unique identifier (e.g., `kb-orders-cancel-01`). Used in turn logs to record provenance. |
| `topic` | enum | yes | One of: `orders`, `returns`, `shipping`, `payments`, `account`. Used for retrieval filtering and to define out-of-scope. |
| `keywords_msa` | string[] | yes | 3–10 short phrases in MSA used for keyword-style matching. |
| `keywords_egy` | string[] | yes | 3–10 short phrases in Egyptian dialect (and common Arabizi forms) used for matching. |
| `wording_msa` | string | yes | Final answer in Modern Standard Arabic, fully self-contained. |
| `wording_egy` | string | yes | Final answer in Egyptian dialect. MUST NOT contain MSA paragraphs. |
| `source` | string | no | Free-text provenance note (e.g., "internal FAQ draft v3"). |
| `last_updated` | string (ISO date) | yes | YYYY-MM-DD; updated whenever any wording field changes. |

**Validation rules**:

- Both `wording_msa` and `wording_egy` MUST be non-empty before the entry
  is eligible for use (FR-014).
- `topic` MUST be one of the five enum values; entries with topics outside
  this set are out of scope for v1.
- `id` MUST be unique across the file.
- A simple linter script (`agent/kb/lint.py`, generated as a Phase 2 task)
  validates the schema and the non-empty-bilingual rule on every change.

**State transitions**: None — KB entries are static documents. Updates are
git commits to `agent/kb/ecommerce-faq.json`.

---

## Conversation Session

A bounded, ordered series of turns between one anonymous visitor and the
agent (spec entity).

**Fields**:

| Field | Type | Required | Notes |
|---|---|---|---|
| `session_id` | string | yes | Provided by the n8n Chat Trigger; opaque token, no PII. |
| `started_at` | string (ISO datetime) | yes | First-turn timestamp. |
| `last_active_at` | string (ISO datetime) | yes | Updated on each turn; idle expiry is 30 minutes. |
| `active_register` | enum | yes | One of: `msa`, `egyptian`, `arabizi`, `other`, `unknown`. Set per turn. |
| `recent_turns` | Turn[] | yes | Sliding window of up to 6 most recent turns held in n8n memory (R4). |

**Validation rules**:

- A new session begins automatically when no activity has been seen on a
  given `session_id` for ≥ 30 minutes (FR-006, R4).
- `active_register` is recomputed from the most recent visitor turn
  (FR-002, FR-003).

**State transitions**:

```
[no session]
   │  visitor sends first message
   ▼
[active]
   │  visitor sends additional messages within 30 min
   │  └─► [active] (active_register may change)
   │
   │  30 min elapse since last_active_at
   ▼
[expired] ─► [no session]
```

**Persistence**: In-memory (n8n session memory) only. The `turns.jsonl` log
captures individual turns persistently; full session reconstruction can be
done by filtering the log by `session_id`.

---

## Turn

A single visitor-message → agent-reply exchange.

**Fields** (matches the schema written by the Phase 2 logger node):

| Field | Type | Required | Notes |
|---|---|---|---|
| `ts` | string (ISO datetime) | yes | When the agent finished generating the reply. |
| `session_id` | string | yes | See Conversation Session. |
| `turn_index` | integer | yes | 0-based position within the session. |
| `register` | enum | yes | One of: `msa`, `egyptian`, `arabizi`, `other`. The register the agent decided to reply in. |
| `visitor_text` | string | yes | Raw incoming message (truncated to 4 KB if longer; truncation flag set). |
| `visitor_text_truncated` | boolean | yes | `true` if the original was truncated. |
| `agent_text` | string | yes | Final reply sent to the visitor. |
| `kb_entry_ids` | string[] | yes | IDs of Knowledge Entries used to ground the reply (empty array for refusals or out-of-scope). |
| `action` | enum | yes | One of: `answer`, `refuse_off_topic`, `refuse_unsafe`, `refuse_unknown_language`, `refuse_role_override`. |
| `latency_ms` | integer | yes | Visible latency from request received to first reply byte. |

**Validation rules**:

- For `action = answer`, `kb_entry_ids` MUST be non-empty.
- For any `refuse_*` action, `agent_text` MUST come from
  `prompts/refusal-templates.md` (R6); free-form refusals are a defect.
- `latency_ms` populated by the Function node measuring elapsed time;
  used for SC-004 verification post-hoc.

---

## Visitor

Anonymous end-user. **No persisted entity** — represented only by the
session-scoped `session_id`. No PII collected (Assumptions).

---

## Portfolio Owner

The operator who curates the KB and runs the demo. **No persisted entity**
in the system — modeled only as the actor who edits files in the repo and
deploys the workflow.

---

## Eval-Run Record

A single (prompt, response) pair produced by the eval harness (R8).
Distinct from a Turn — eval records are batch-produced for grading.

**Fields**:

| Field | Type | Required | Notes |
|---|---|---|---|
| `eval_set` | enum | yes | One of: `msa`, `egyptian`, `adversarial`. |
| `prompt_id` | string | yes | Unique within the eval set. |
| `prompt` | string | yes | Verbatim eval-set prompt sent to the agent. |
| `response` | string | yes | Agent reply. |
| `detected_register` | enum | yes | What the agent identified the prompt as. |
| `latency_ms` | integer | yes | Visible latency. |
| `model` | string | yes | Gemini variant used (e.g., `gemini-2.5-flash-lite`). |
| `run_at` | string (ISO datetime) | yes | When the prompt was sent. |
| `verdict` | enum \| null | no | Filled by the human reviewer: `pass` \| `fail` \| `partial` \| null (ungraded). |
| `verdict_notes` | string | no | Free text from the reviewer. |

**Validation rules**:

- `verdict` is null at the moment the runner writes the record; the
  reviewer fills it in by editing the JSONL file in place.
- Pass-rate aggregation simply counts `verdict == "pass"` divided by total
  records, per eval set, against the SC thresholds.

---

## Cross-entity invariants

- Every Turn whose `action == answer` references at least one Knowledge
  Entry that has both `wording_msa` and `wording_egy` populated (chains
  FR-014 from KB through to Turn).
- The `register` value on a Turn matches the language family of
  `agent_text` — verified spot-checked during evaluation, not enforced
  programmatically (would require a register classifier in the verifier).
