# Contract: LLM Prompt + Structured Response

**Source of truth**: `agent/prompts/system.md` (authored as a Phase 2 task).
This document specifies the contract between the n8n workflow and Gemini —
what gets sent, and what shape the response MUST come back in.

---

## System prompt structure

The system prompt is a single multi-section message authored in Arabic and
English (English meta-instructions for the model, Arabic register-specific
instructions for the response style).

Required sections, in order:

1. **Role lock** — agent name, scope, refusal of role override.
2. **Register policy** — the four-way classification rules
   (`msa` | `egyptian` | `arabizi` | `other`) and the matching response
   register.
3. **Knowledge grounding rule** — the agent MUST only answer from the KB
   payload supplied in the user message; it MUST NOT invent facts.
4. **Refusal policy** — when to emit `refuse_off_topic`,
   `refuse_unsafe`, `refuse_unknown_language`, `refuse_role_override`, and
   which refusal template to draw from.
5. **Style guide** — Egyptian-dialect dos and don'ts (e.g., use بتاع, إيه,
   كده; avoid MSA-only constructions). MSA dos and don'ts.
6. **Output schema** — the structured response shape (below).

---

## Per-turn user message

The Function node assembles a per-turn user message containing:

```
<visitor_message>
{raw visitor input, truncated to 4096 chars}
</visitor_message>

<retrieved_kb>
[
  {
    "id": "<kb-entry-id>",
    "topic": "<topic>",
    "wording_msa": "...",
    "wording_egy": "..."
  },
  ...
]
</retrieved_kb>

<recent_turns>
[
  { "role": "visitor", "text": "..." },
  { "role": "agent",   "text": "..." },
  ...
]
</recent_turns>
```

Retrieval is keyword-based against `keywords_msa` / `keywords_egy`,
returning the top 5 candidate entries. The model picks zero or more
relevant ones.

---

## Required response shape

Gemini is invoked with a response schema enforcing the following JSON
object as the single output:

```json
{
  "register": "msa | egyptian | arabizi | other",
  "action": "answer | refuse_off_topic | refuse_unsafe | refuse_unknown_language | refuse_role_override",
  "kb_entry_ids": ["<kb-entry-id>", "..."],
  "reply": "<final visitor-facing reply, in the matching register>"
}
```

**Field rules**:

- `register`: as detected on the most recent visitor message.
- `action`:
  - `answer` requires `kb_entry_ids` non-empty and `reply` to be a faithful
    rendering of the chosen entry's wording in the matching register.
  - `refuse_*` requires `kb_entry_ids` to be `[]` and `reply` to be drawn
    verbatim from `prompts/refusal-templates.md` for the matching action +
    register pair.
- `arabizi` register: the agent always answers in Egyptian Arabic script
  (FR-007), not in Latin transliteration.
- `other` register: the agent answers in MSA (FR-010), not the visitor's
  original language.

---

## Workflow-side handling

After Gemini returns:

1. The Function node validates the JSON shape against the contract above.
   Validation failure → workflow falls back to a generic MSA "we hit a
   technical issue" reply and logs the malformed payload.
2. For `action != "answer"`, the Function node replaces `reply` with the
   exact refusal-template string (defense in depth — even if the model
   generates a free-form refusal, it is overwritten).
3. The Function node persists a Turn record per the data model.
4. The visible response sent to the chat client is `{"output": reply}`
   per the Chat Trigger contract.
