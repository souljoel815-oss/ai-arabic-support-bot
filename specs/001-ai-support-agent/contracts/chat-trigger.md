# Contract: Chat Trigger HTTP Interface

**Source of truth**: n8n's native Chat Trigger node, configured in
`agent/workflow/ai-support-agent.json`. This document captures the
externally observable shape so callers (the eval harness, any future
embedding) can rely on a stable contract.

---

## Endpoint

`POST {WEBHOOK_URL}/chat`

`WEBHOOK_URL` is set by the deployment (`deploy/.env`); during local
development it resolves to `http://localhost:5678/webhook/chat`.

The visitor-facing chat URL (`{WEBHOOK_URL}/chat?...`) is rendered by n8n's
hosted chat page and is what gets linked from the portfolio site. The HTTP
contract below is what the page itself uses under the hood and what the
eval harness exercises directly.

---

## Request

Content-Type: `application/json`

```json
{
  "sessionId": "<opaque-string>",
  "action": "sendMessage",
  "chatInput": "<visitor message, plain text, max 4096 chars>"
}
```

**Field rules**:

- `sessionId`: opaque string supplied by the chat client; the agent treats
  it as the session key for memory (R4). The eval harness generates a
  fresh UUID per prompt to avoid cross-contamination.
- `action`: always `"sendMessage"` for visitor inputs in v1.
- `chatInput`: visitor text. Inputs longer than 4096 characters are
  truncated by the workflow's first Function node and the truncation is
  logged on the resulting Turn record (per data-model `Turn.visitor_text_truncated`).

---

## Response

Content-Type: `application/json`

```json
{
  "output": "<agent reply, plain text in the matched register>"
}
```

**Field rules**:

- `output`: the final visible reply. For `action = answer` Turns it is
  derived from the matched Knowledge Entry's `wording_msa` /
  `wording_egy`; for `refuse_*` Turns it is taken verbatim from
  `prompts/refusal-templates.md`.
- The structured action / register / KB-entry-id metadata is **not**
  exposed in the response payload — it is internal and is recorded in the
  Turn log only.

---

## Error responses

| Condition | HTTP status | Body |
|---|---|---|
| Empty `chatInput` (after trim) | 200 | `{"output": "<MSA prompt to send a real message>"}` (treated as a normal turn, not an error) |
| Gemini API failure | 200 | `{"output": "<MSA apology + suggest retry>"}` (Turn logged with `action = refuse_unsafe` is **not** used; this is a separate `error` action — added to the data model in Phase 2 if needed) |
| Internal workflow error | 500 | `{"error": "internal_error"}` |
| Webhook URL not active | 404 | n8n default error body |

The agent never surfaces raw Gemini API errors or stack traces to visitors
(SC-003 spirit).

---

## Stability guarantees

- Field names (`sessionId`, `action`, `chatInput`, `output`) are part of
  this contract and will not change without a version bump.
- The 4096-character input ceiling is part of this contract.
- New optional response fields MAY be added (e.g., `register` for
  debugging) without breaking existing callers; consumers MUST ignore
  unknown fields.
