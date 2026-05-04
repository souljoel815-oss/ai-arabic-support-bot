# AI Customer Support Agent — Arabic + Egyptian Dialect

A bilingual customer-support agent for a fictional e-commerce store, built
on **n8n Cloud** + **Google Gemini**. It answers visitor questions in
**Modern Standard Arabic** (MSA / فصحى) or **Egyptian Arabic dialect**
(عامية مصرية) and matches whichever register the visitor used — including
Franco-Arabic ("Arabizi") inputs like *"ezzay arga3 el order?"*. Built as
a portfolio piece to demonstrate dialect-aware retrieval-grounded support
on a managed workflow platform.

## Try the demo

```
https://guillaume120.app.n8n.cloud/webhook/da1f362e-200c-4255-a624-9bb6544821d0/chat
```

Some prompts to try:

| In MSA | In Egyptian dialect | In Arabizi |
|---|---|---|
| كيف ألغي طلبي؟ | ازاي ألغي الأوردر؟ | ezzay alghi el order? |
| ما هي وسائل الدفع المتاحة؟ | إيه وسائل الدفع المتاحة؟ | bet2balo cash 3and el isti2lam? |
| نسيت كلمة المرور | نسيت الباسوورد، أعمل إيه؟ | nesit el password, a3mel eih? |

The agent should reply in the same register you used. Try mixing registers
across turns; it adjusts. Try off-topic questions; it should decline
gracefully without fabricating.

## What's interesting (technical)

- **Dialect detection without a separate classifier** — the LLM emits a
  structured JSON header (`register`: `msa` | `egyptian` | `arabizi` |
  `other`) on every turn alongside its reply, so the workflow can route
  to the correct refusal template, all in one round-trip.
- **Pre-translated bilingual KB** — every knowledge entry carries both
  MSA and Egyptian wording (no on-the-fly translation), so the model
  retrieves rather than generates dialect — eliminates dialect-quality
  variance and reliably clears the SC-002 "no MSA leakage" bar.
- **Layered safety** — system-prompt role lock + Gemini's safety filters
  + curated MSA/Egyptian refusal templates that the workflow swaps in
  for any `refuse_*` action the model emits. Even if the model
  free-forms a refusal, the workflow overwrites with the canonical
  phrasing.
- **Grounding + retrieval** — keyword scoring over `keywords_msa` +
  `keywords_egy` per KB entry yields top-5 candidates per turn; the
  model picks 0..N to ground on, or emits `refuse_off_topic` if none
  fit. Hallucinations on out-of-KB questions verified ≈ 0 on the
  baseline run.

## Stack

| Layer | Choice |
|---|---|
| Workflow runtime | [n8n Cloud](https://n8n.io/cloud/) (managed) |
| LLM | Google Gemini family — `gemini-2.5-flash-lite` |
| Memory | n8n native session memory (6-turn sliding window, multi-turn US3 phase) |
| Knowledge base | Bilingual JSON inlined in the workflow's `load_kb` Code node; canonical version-controlled at `agent/kb/ecommerce-faq.json` |
| Logging | n8n Cloud's built-in execution history |
| Eval harness | Python 3.11 + httpx; eval sets in `agent/eval/sets/*.jsonl` |

## Repo layout

```
agent/
├── workflow/         n8n Cloud workflow buildbook + JSON snapshot
├── kb/               canonical bilingual KB + linter
├── prompts/          system prompt + refusal templates
├── eval/             eval harness + four eval sets + reviewer rubric
└── deploy/           n8n Cloud setup notes

specs/001-ai-support-agent/
├── spec.md           feature spec (4 user stories, 14 FRs, 6 SCs)
├── plan.md           implementation plan + constitution gate-table
├── research.md       technical decisions + rationale
├── data-model.md     entity schemas
├── contracts/        chat-trigger HTTP contract, KB schema, prompt schema
├── tasks.md          dependency-ordered tasks (49 total, story-grouped)
└── quickstart.md     end-to-end deploy + demo + eval guide

.specify/memory/
└── constitution.md   v1.0.0 — five governing principles
```

## Working on this repo

The project is built using [Spec Kit](https://github.com/github/spec-kit) —
a spec-driven workflow where every feature flows: constitution →
specify → clarify → plan → tasks → implement, with each phase producing
a versioned artifact in `specs/`.

If you're cloning to learn:

- Read [`specs/001-ai-support-agent/spec.md`](specs/001-ai-support-agent/spec.md) for what the agent does and why
- Read [`specs/001-ai-support-agent/plan.md`](specs/001-ai-support-agent/plan.md) for how it's built
- Read [`specs/001-ai-support-agent/research.md`](specs/001-ai-support-agent/research.md) for *why this choice over that one* on each technical fork (KB shape, register detection, memory, logging, deployment, evaluation)
- Read [`specs/001-ai-support-agent/quickstart.md`](specs/001-ai-support-agent/quickstart.md) to deploy your own instance

If you're cloning to extend:

1. Build the workflow in your own n8n Cloud workspace following [`agent/workflow/README.md`](agent/workflow/README.md).
2. Edit [`agent/kb/ecommerce-faq.json`](agent/kb/ecommerce-faq.json) for new content; run `python agent/kb/lint.py`; paste into the workflow's `load_kb` Code node.
3. Iterate the system prompt at [`agent/prompts/system.md`](agent/prompts/system.md).
4. Run evals: `python agent/eval/runner.py --set msa --webhook <YOUR_URL>`.

## Project governance

This project has a [constitution](.specify/memory/constitution.md) (v1.0.0)
with five governing principles: Spec-First, Plan-Before-Code, Test-First,
Simplicity & YAGNI, and Incremental Independently-Testable Delivery. Every
non-trivial decision is recorded as a clarification in the spec or as a
research item with rationale, and every plan is gate-checked against the
constitution before tasks are generated.

## Status

| Phase | Status |
|---|---|
| Phase 1 — Setup | ✅ complete |
| Phase 2 — Foundational | ✅ complete (n8n Cloud workflow live) |
| Phase 3 — US1 (MSA) | author-side complete; baseline ran 2026-05-04 (p50 latency 2048 ms ✓ SC-004); human grading in progress |
| Phase 4 — US2 (Egyptian) | author-side complete; native-speaker grading in progress |
| Phase 5 — US3 (multi-turn) | eval set authored; in-editor wiring + grading pending |
| Phase 6 — US4 (safety) | author-side complete; grading in progress |
| Phase 7 — Polish | KB grown 5 → 20 entries; remaining items: latency sign-off, KB ops timing, README polish |

## License

This is a personal portfolio project. No license file is included; please
ask before reusing the workflow design or content for commercial purposes.
