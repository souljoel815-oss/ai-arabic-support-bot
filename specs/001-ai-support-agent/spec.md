# Feature Specification: AI Customer Support Agent (Arabic + Egyptian Dialect)

**Feature Branch**: `001-ai-support-agent`
**Created**: 2026-05-04
**Status**: Draft
**Input**: User description: "AI Customer Support Agent support arabic and egyptian using n8n i want it for portfolio"

## Clarifications

### Session 2026-05-04

- Q: What support domain should the agent serve? → A: Generic e-commerce store (orders, returns, shipping, payments, account)
- Q: Which underlying language model family should power the agent? → A: Google Gemini family
- Q: How should the bilingual knowledge base be authored? → A: Pre-translated — every entry is manually authored in both MSA and Egyptian dialect
- Q: Through which chat surface should visitors reach the agent? → A: n8n native Chat Trigger (hosted public chat URL provided by n8n, linked from the portfolio page)
- Q: Where should the n8n workflow be hosted? → A: Self-hosted n8n on a small VPS (stable public URL, owner-operated)
- Q: Hosting decision update — actual deployment? → A: **n8n Cloud** (managed). Supersedes Q5 above. Owner has built and tested the workflow on n8n Cloud at the URL recorded in the Assumptions section. The repo's VPS-deploy artifacts (Caddyfile, docker-compose.yml) are retained in git history for reference only and removed from the working tree.
- Q: How should the bilingual KB be loaded by the workflow? → A: **Inline JSON inside a Code node** in the n8n Cloud workflow. The repo's `agent/kb/ecommerce-faq.json` remains the authoritative version-controlled source; updates are propagated by copy-pasting the file's contents into the workflow's `load_kb` Code node and saving.
- Q: How should conversation logging (FR-012) be implemented? → A: **n8n's built-in execution history**. The dedicated `log_turn` node and `/var/log/ai-support-agent/turns.jsonl` sink are dropped; per-turn payloads (timestamps, visitor input, agent reply, detected register) are inspectable in the n8n Cloud Executions UI, which satisfies FR-012.
- Q: Specific Gemini variant — locked decision? → A: **`gemini-2.5-flash-lite`**. Picked during T011 (workflow build on n8n Cloud) ahead of the originally-deferred Phase 7 bake-off. This supersedes research.md R1's "default to `gemini-1.5-flash`" placeholder. Rationale: 2.5-flash-lite is cheapest and lowest-latency in the current Gemini family, and the agent's task (structured retrieval + lightly-stitched reply from a pre-translated KB) is well within its capability — first-run latency p50 is ~2 sec, well under SC-004's 3000 ms budget. T040 (Phase 7) is reframed as a stability/quality upgrade test against `gemini-2.5-flash` (regular) and `gemini-2.5-pro`, not against the deprecated 1.5 line.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Answer support questions in Modern Standard Arabic (Priority: P1)

An Arabic-speaking visitor opens the support chat, types a customer-support question
in Modern Standard Arabic (MSA / فصحى), and receives a clear, accurate answer in
MSA grounded in the agent's knowledge base.

**Why this priority**: This is the baseline competence of the agent. Without it,
the feature has no reason to exist. It is also the simplest path to a working
demonstration and therefore constitutes the MVP.

**Independent Test**: A reviewer (or the portfolio owner) sends a fixed set of
MSA support questions covering the curated knowledge base topics and verifies
that each response is in MSA, factually correct against the source material, and
delivered within the agreed response-time budget.

**Acceptance Scenarios**:

1. **Given** a visitor on the support interface, **When** they submit an MSA
   question whose answer exists in the knowledge base, **Then** the agent
   responds in MSA with content derived from the knowledge base.
2. **Given** an MSA question that the knowledge base cannot answer, **When** the
   visitor submits it, **Then** the agent responds in MSA with an explicit
   "I don't have information about that" style message rather than fabricating
   an answer.
3. **Given** a visitor mid-conversation in MSA, **When** they ask a follow-up
   that depends on the previous turn, **Then** the agent uses the prior turn as
   context and responds coherently in MSA.

---

### User Story 2 - Answer support questions in Egyptian Arabic dialect (Priority: P1)

A visitor types a customer-support question in Egyptian Arabic dialect (عامية
مصرية) and receives an accurate answer in the same Egyptian dialect, not in MSA.

**Why this priority**: Dialect awareness is the headline differentiator of this
portfolio piece — it is what distinguishes it from a generic Arabic chatbot.
Without it, the project does not achieve its purpose.

**Independent Test**: A native Egyptian-Arabic speaker sends a fixed set of
dialect questions and confirms that responses (a) are in Egyptian dialect, not
MSA, and (b) are factually correct against the knowledge base.

**Acceptance Scenarios**:

1. **Given** a visitor writing in Egyptian Arabic, **When** they ask a question
   covered by the knowledge base, **Then** the agent responds in Egyptian Arabic
   dialect with the correct content.
2. **Given** a visitor whose first message was in MSA and second in Egyptian
   dialect, **When** they submit the second message, **Then** the agent matches
   the dialect of the most recent visitor message in its reply.
3. **Given** a visitor writing in Egyptian dialect, **When** they ask something
   outside the knowledge base, **Then** the agent declines in Egyptian dialect
   and offers a graceful next step (e.g., suggest contacting human support).

---

### User Story 3 - Maintain conversation context across multiple turns (Priority: P2)

A visitor has a multi-turn conversation in either MSA or Egyptian dialect, and
the agent remembers earlier turns within the same session so follow-up questions
work naturally without the visitor having to repeat information.

**Why this priority**: Single-turn Q&A is functional but not impressive for a
portfolio. Multi-turn coherence is what makes the demo feel like a real
assistant. It depends on US1/US2 working first, which is why it is P2.

**Independent Test**: Run a scripted 5-turn conversation that includes
back-references (e.g., "what about the second one?", "وكمان السعر بتاعها؟"), and
confirm the agent answers correctly each turn.

**Acceptance Scenarios**:

1. **Given** an active session with previous turns, **When** the visitor asks a
   follow-up using a pronoun or back-reference, **Then** the agent resolves the
   reference using prior turns and answers correctly.
2. **Given** a session that has been idle for the configured retention window,
   **When** the visitor returns and asks a back-referencing question, **Then**
   the agent either restores context or transparently asks the visitor to
   restate the topic.

---

### User Story 4 - Graceful handling of out-of-scope or unsafe questions (Priority: P3)

A visitor asks something the agent should not answer (out of knowledge scope,
abusive, or requesting information the agent cannot verify). The agent declines
clearly in the visitor's language/dialect and offers a constructive next step.

**Why this priority**: A polished portfolio demo must not embarrass the owner
when a reviewer pokes at edge cases. This story protects the demo's credibility
but is not required for the core value.

**Independent Test**: Run a fixed adversarial test set (off-topic questions,
prompt-injection attempts, abusive messages) and confirm the agent declines
politely in matching language without fabricating answers or breaking character.

**Acceptance Scenarios**:

1. **Given** a clearly off-topic question, **When** the visitor sends it,
   **Then** the agent declines and points the visitor to the supported topic
   areas, in matching language/dialect.
2. **Given** an abusive or unsafe message, **When** the visitor sends it,
   **Then** the agent does not echo the abuse, declines in a neutral tone, and
   ends or redirects the exchange.
3. **Given** an attempt to override the agent's role (prompt injection),
   **When** the visitor sends it, **Then** the agent stays in its support role
   and does not reveal internal instructions.

---

### Edge Cases

- Visitor mixes MSA and Egyptian dialect within a single message — the agent
  picks the dominant register and responds in that one rather than mixing.
- Visitor writes in Arabic with Latin characters ("Franco-Arabic" / Arabizi,
  e.g., "ezzayak?") — the agent recognizes it as Egyptian Arabic and replies in
  proper Egyptian Arabic script.
- Visitor writes in a non-Arabic language (e.g., English) — the agent responds
  in MSA and politely notes that supported languages are MSA and Egyptian Arabic.
- Visitor sends an empty message or only emoji/punctuation — the agent prompts
  for a real question without erroring.
- Visitor sends an extremely long message — the agent truncates or summarizes
  the input rather than failing, and answers the dominant intent.
- Knowledge base is updated mid-session — in-flight conversations continue with
  the version active when the session started; new sessions see the new content.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The agent MUST accept text questions in Modern Standard Arabic and
  in Egyptian Arabic dialect.
- **FR-002**: The agent MUST detect which of the two registers (MSA or Egyptian
  dialect) the most recent visitor message is written in.
- **FR-003**: The agent MUST respond in the same register it detected on the
  most recent visitor turn (MSA → MSA, Egyptian → Egyptian).
- **FR-004**: The agent MUST ground answers in a curated knowledge base and MUST
  NOT fabricate facts that are not present in or directly inferable from that
  knowledge base.
- **FR-005**: The agent MUST explicitly decline (in the matching register) when
  asked a question whose answer is not in the knowledge base.
- **FR-006**: The agent MUST preserve conversational context across turns within
  a single visitor session for at least the duration specified in Assumptions.
- **FR-007**: The agent MUST handle Franco-Arabic / Arabizi input (Arabic written
  in Latin script) by recognizing it as Egyptian dialect and replying in
  Egyptian Arabic script.
- **FR-008**: The agent MUST refuse to comply with prompt-injection or
  role-override attempts and MUST remain in its customer-support role.
- **FR-009**: The agent MUST decline abusive or unsafe inputs in a neutral tone
  without echoing the abusive content back to the visitor.
- **FR-010**: The agent MUST politely respond in MSA when a visitor writes in a
  language other than Arabic and inform them which languages are supported.
- **FR-011**: The portfolio owner MUST be able to update the knowledge base
  content (add, edit, remove entries) without requiring code changes.
- **FR-012**: The system MUST log each conversation (timestamps, visitor input,
  agent reply, detected register) for later review by the portfolio owner.
- **FR-013**: The agent MUST be reachable through a publicly demonstrable
  interface that the portfolio owner can link to from a portfolio page.
- **FR-014**: Every Knowledge Entry MUST be authored in both Modern Standard
  Arabic and Egyptian Arabic dialect before being eligible to ground answers.
  The agent MUST select the wording matching the detected register on the most
  recent visitor turn rather than translating across registers at answer time.

### Key Entities *(include if feature involves data)*

- **Conversation Session**: A bounded, ordered series of turns between one
  visitor and the agent. Carries the active register, recent turns used as
  context, and a session identifier.
- **Turn**: A single exchange — visitor message in, agent reply out — with
  timestamp, detected register, and references to source knowledge entries used.
- **Knowledge Entry**: A unit of curated support content (e.g., an FAQ, a
  policy snippet). Each entry MUST carry: topic, **manually authored MSA
  wording**, **manually authored Egyptian-dialect wording**, and
  source/last-updated metadata. Both register variants are required before the
  entry can be used to ground answers (no on-the-fly translation between
  registers).
- **Visitor**: An anonymous end-user identified only by session, with no PII
  required to interact with the agent.
- **Portfolio Owner**: The operator who curates the knowledge base and uses the
  deployed agent as a demonstrable artifact.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a held-out evaluation set of 30 MSA support questions covered
  by the knowledge base, at least 90% of agent responses are judged factually
  correct and entirely in MSA by an Arabic-speaking reviewer.
- **SC-002**: On a held-out evaluation set of 30 Egyptian-dialect support
  questions covered by the knowledge base, at least 85% of agent responses are
  judged factually correct and entirely in Egyptian dialect (no MSA leakage)
  by a native Egyptian-Arabic-speaking reviewer.
- **SC-003**: On a held-out adversarial set of 20 out-of-scope, abusive, and
  prompt-injection inputs, at least 95% of responses are judged as appropriate
  refusals that stay in role and do not fabricate answers.
- **SC-004**: Median visible time from visitor message submitted to first
  character of agent reply is under 3 seconds during demo conditions.
- **SC-005**: The portfolio owner can add a new knowledge entry and have it
  reflected in the agent's answers in under 10 minutes of editor time, without
  modifying any code.
- **SC-006**: A first-time visitor can complete a representative 3-turn support
  exchange (initial question, follow-up, closing) without confusion, measured
  by post-task feedback or completion-without-restating-questions.

## Assumptions

- "Arabic and Egyptian" is interpreted as **Modern Standard Arabic (MSA, فصحى)**
  and **Egyptian Arabic dialect (عامية مصرية)** — the two registers most
  relevant for a portfolio piece targeting MENA audiences.
- The agent is built on the **n8n** workflow-automation platform, as specified
  by the portfolio owner. n8n's role is an implementation detail; the
  requirements above remain technology-agnostic. n8n is fixed because the
  portfolio's purpose is to showcase competence with this platform.
- The underlying language model is **`gemini-2.5-flash-lite`** in the
  Google Gemini family (locked during T011, per Q9 clarification). It
  is the cheapest and lowest-latency variant in the Gemini family and
  has demonstrated SC-004-passing latency (~2 sec p50) on the first
  baseline run. SC-001 and SC-002 sign-off pass-rates are still pending
  human grading; if either falls below threshold, T040 (Phase 7) tests
  upgrades to `gemini-2.5-flash` (regular) or `gemini-2.5-pro`.
- The agent is a **demo / portfolio piece**, not a production support system
  for a real business. Scope is therefore optimized for demonstrability over
  scale, multi-tenancy, or SLA guarantees.
- The support domain is a **generic e-commerce store** (a fictional online shop
  covering orders, returns, shipping, payments, and account topics), with a
  curated knowledge base of roughly 20–50 Q&A entries spanning these topic
  areas. Questions outside these areas are treated as out of scope.
- The visitor channel is the **n8n native Chat Trigger**: visitors interact via
  the hosted public chat URL that n8n exposes for the workflow, and the
  portfolio site links out to that URL (e.g., "Try the demo" button). Other
  channels (custom embedded widget, standalone hosted page, WhatsApp,
  Telegram, voice) are out of scope for v1.
- Modality is **text only** in v1 — no voice input or output.
- Session context is retained for at least **30 minutes of inactivity**, after
  which a new session begins.
- Visitor interactions are **anonymous**; the agent does not collect or require
  personal identifying information.
- Authentication is not required for visitors to use the agent; the agent is
  intended to be openly demonstrable.
- The knowledge base is **manually curated** by the portfolio owner; no
  automated scraping or live data integration is in scope for v1.
- The n8n workflow is hosted on **n8n Cloud** (managed). The active public
  chat URL is
  `https://guillaume120.app.n8n.cloud/webhook/da1f362e-200c-4255-a624-9bb6544821d0/chat`
  — this is the URL linked from the portfolio site. (Earlier clarification
  Q5 specified self-hosted VPS; superseded by clarifications Q6–Q8 in this
  session.)
- The bilingual KB is **inlined as JSON inside a Code node** in the n8n
  Cloud workflow. The repo's `agent/kb/ecommerce-faq.json` is the
  authoritative version-controlled source; KB updates are propagated by
  copy-pasting that file's contents into the workflow's `load_kb` Code
  node and saving the workflow. FR-011 is satisfied — KB content updates
  do not require JavaScript code changes, only an inline-JSON refresh.
- Conversation logging (FR-012) is satisfied by **n8n's built-in execution
  history**: every workflow run records the full per-turn payload
  (timestamps, visitor input, agent reply, detected register, KB entry
  IDs), inspectable in the n8n Cloud Executions UI. No dedicated log file
  or external sink is provisioned in v1.
- "Portfolio-grade reliability" means the agent must work reliably during
  a short live demo (< 30 minutes) on demand. n8n Cloud's managed uptime
  comfortably covers normal review windows; 24/7 uninterrupted uptime is
  NOT a goal.
