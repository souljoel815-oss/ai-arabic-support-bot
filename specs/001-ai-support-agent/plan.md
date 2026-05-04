# Implementation Plan: AI Customer Support Agent (Arabic + Egyptian Dialect)

**Branch**: `001-ai-support-agent` | **Date**: 2026-05-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-ai-support-agent/spec.md`

## Summary

A bilingual (MSA + Egyptian Arabic) AI customer-support agent for a fictional
e-commerce store, built as an n8n workflow self-hosted on a small VPS, powered
by the Google Gemini family. Visitors interact through n8n's native Chat
Trigger; the agent retrieves answers from a pre-translated bilingual knowledge
base and responds in whichever register the visitor used. The artifact serves
as a portfolio piece demonstrating dialect-aware support, RAG over a curated
KB, and end-to-end deployment competence on n8n.

## Technical Context

**Language/Version**: n8n workflow JSON (declarative, n8n Cloud current
  version); evaluation harness in Python 3.11 (small CLI scripts only)
**Primary Dependencies**: n8n Cloud (managed), Google Gemini API
  (specific variant decided in Phase 7 bake-off — candidates: Gemini 1.5
  Pro, Gemini 1.5 Flash, Gemini 2.0 Flash if available), n8n Chat Trigger
  node, n8n AI Agent / LangChain nodes
**Storage**: Bilingual knowledge base inlined as JSON inside the workflow's
  `load_kb` Code node (mirrored canonically by `agent/kb/ecommerce-faq.json`
  in the repo); n8n's built-in chat session memory for short-term
  conversation context; conversation logging via n8n Cloud's built-in
  execution history
**Testing**: Custom evaluation harness — Python script that posts eval
  prompts to the n8n Cloud Chat Trigger webhook and records responses for
  human review; three eval sets (MSA, Egyptian, adversarial) of fixed
  sizes per the spec's Success Criteria
**Target Platform**: **n8n Cloud** (managed). Public chat URL:
  `https://guillaume120.app.n8n.cloud/webhook/da1f362e-200c-4255-a624-9bb6544821d0/chat`.
  No self-hosted infrastructure.
**Project Type**: Web service (single project) — one workflow exposing one
  HTTP chat endpoint, with supporting KB and eval scripts in the same repo
**Performance Goals**: Median visible response under 3 seconds (SC-004)
  under demo conditions (≤ 5 concurrent visitors, 1–3 turn sessions);
  cold-start of workflow run < 1 second
**Constraints**: Pre-translated bilingual KB only (no on-the-fly
  cross-register translation, FR-014); Gemini-family LLM only; managed
  n8n Cloud deployment (no shell, no filesystem); anonymous visitors; no
  PII collection; portfolio-grade reliability (works on demand, not 24/7)
**Scale/Scope**: 20–50 KB entries; ~30+30+20 = 80 evaluation prompts; demo
  load ≤ 5 concurrent visitors; one fictional e-commerce domain

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-Phase 0 | Post-Phase 1 | Notes |
|---|---|---|---|
| I. Spec-First Development (NON-NEGOTIABLE) | ✅ Pass | ✅ Pass | Spec exists, 5 clarifications applied, no [NEEDS CLARIFICATION] markers remain. |
| II. Plan Before Code | ✅ Pass | ✅ Pass | This document is the plan; tasks will follow from it. |
| III. Test-First Discipline (NON-NEGOTIABLE) | ✅ Pass | ✅ Pass | The three eval sets (MSA / Egyptian / adversarial) are mandatory tests required by SC-001/002/003. They MUST be authored and observed failing before the workflow is wired to Gemini, then made to pass through prompt iteration and KB curation. Tasks will enforce this ordering. |
| IV. Simplicity & YAGNI | ✅ Pass | ✅ Pass | Single workflow, single LLM family, single domain, JSON-file KB, file-based logs. No premature abstractions. |
| V. Incremental, Independently Testable Delivery | ✅ Pass | ✅ Pass | The spec already groups stories US1 (MSA) + US2 (Egyptian) as P1 MVP, US3 (multi-turn) as P2, US4 (safety) as P3. Each story is independently testable via its own slice of the eval set. Tasks will be grouped accordingly. |

**Result**: All gates pass. No entries required in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/001-ai-support-agent/
├── plan.md                  # This file
├── research.md              # Phase 0 output
├── data-model.md            # Phase 1 output
├── quickstart.md            # Phase 1 output
├── contracts/               # Phase 1 output
│   ├── chat-trigger.md      # Chat Trigger HTTP contract (request/response)
│   ├── kb-entry.schema.json # JSON Schema for one KB entry
│   ├── llm-prompt.md        # System prompt + structured-response contract
│   └── eval-record.schema.json # JSON Schema for an eval-run record
├── checklists/
│   └── requirements.md      # Spec quality checklist (already created)
└── tasks.md                 # Phase 2 output (created by /speckit-tasks)
```

### Source Code (repository root)

```text
agent/
├── workflow/
│   └── ai-support-agent.json     # Exported n8n workflow definition
├── kb/
│   └── ecommerce-faq.json        # Bilingual KB (MSA + Egyptian per entry)
├── prompts/
│   ├── system.md                 # Bilingual system prompt + role-lock + style guide
│   └── refusal-templates.md      # MSA + Egyptian refusal phrasings (out-of-scope, abuse, injection)
├── eval/
│   ├── sets/
│   │   ├── msa.jsonl             # 30 MSA prompts with expected topic tags (SC-001)
│   │   ├── egyptian.jsonl        # 30 Egyptian-dialect prompts (SC-002)
│   │   └── adversarial.jsonl     # 20 out-of-scope/abuse/injection prompts (SC-003)
│   ├── runner.py                 # Hits Chat Trigger webhook, writes eval-record.jsonl
│   └── reviewer-template.md      # Template the human reviewer fills out
├── deploy/
│   └── n8n-cloud-setup.md        # n8n Cloud account / credential / live-URL notes
└── docs/
    └── quickstart.md             # Mirror of specs/.../quickstart.md for dev convenience
```

**Structure Decision**: Single-project layout under `agent/`. There is no
separate frontend (the n8n Chat Trigger provides the chat UI) and no backend
service beyond the n8n workflow itself, so the multi-app templates do not
apply. Tests live under `agent/eval/` rather than a generic `tests/` directory
because the only tests that matter are the three Success-Criteria eval sets;
treating them as first-class artifacts (not scaffolding) reflects their
load-bearing role in this feature.

## Complexity Tracking

> All Constitution Check gates pass. No entries required.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | _(none)_ | _(none)_ |
