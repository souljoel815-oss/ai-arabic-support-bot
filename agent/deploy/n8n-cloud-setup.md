# n8n Cloud Setup

> The workflow runs on **n8n Cloud** (managed). This file replaces the
> earlier VPS runbook (which is preserved in git history under the
> Phase 2 boundary commit, before clarifications Q6–Q8 were applied).

---

## Live demo URL

```
https://guillaume120.app.n8n.cloud/webhook/da1f362e-200c-4255-a624-9bb6544821d0/chat
```

Linked from the portfolio site as "Try the demo".

---

## One-time account setup

1. Sign up at <https://n8n.io/cloud/> with the portfolio owner's email.
2. Pick the lowest tier that includes the AI / LangChain nodes (Starter
   at minimum).
3. The n8n Cloud workspace URL takes the form
   `https://<workspace>.app.n8n.cloud`. Note the workspace name —
   you'll need it later for the eval harness.

## One-time credential setup (Gemini)

In the n8n Cloud UI:

1. **Credentials → Add credential → Google Gemini (PaLM) API**.
2. Paste the Gemini API key from Google AI Studio.
3. Save. This credential is referenced by the workflow's Gemini chat
   model node.

## Build the workflow

Follow `agent/workflow/README.md`. Notable n8n-Cloud-specific points:

- The KB is **inlined as JSON** inside the `load_kb` Code node — there
  is no host filesystem to read from. Source of truth remains
  `agent/kb/ecommerce-faq.json` in this repo; copy-paste its contents
  into the Code node when updating.
- The system prompt and refusal templates live similarly inside the
  workflow. The repo files at `agent/prompts/system.md` and
  `agent/prompts/refusal-templates.md` are the authoritative versioned
  copies; paste their relevant content into the AI Agent's system
  message slot and the `validate_and_overwrite` Code node respectively.
- There is **no `log_turn` node** — n8n Cloud's built-in execution
  history is the conversation log (Executions tab).

## Activate the workflow

In the n8n Cloud editor, toggle **Active** in the top-right. The chat
URL printed by the Chat Trigger node is now the live URL.

## Routine ops

| Task | How |
|---|---|
| Update KB content | Edit `agent/kb/ecommerce-faq.json` in the repo; commit; copy-paste the new contents into the workflow's `load_kb` Code node; **Save** the workflow |
| Update system prompt | Edit `agent/prompts/system.md`; copy-paste into the AI Agent node's system message; Save |
| Update refusal templates | Edit `agent/prompts/refusal-templates.md`; copy-paste into the `validate_and_overwrite` Code node's tplMap parser source; Save |
| Rotate Gemini API key | n8n Cloud UI → Credentials → edit the Google Gemini credential |
| Read recent conversation logs | n8n Cloud UI → Executions tab; filter by workflow → click any execution to inspect node-by-node payloads |
| Switch Gemini variant (Flash ↔ Pro) | Open the workflow in n8n Cloud → Gemini chat-model node → change the Model field → Save |
| Pause / unpause the demo | Toggle Active in the top-right of the workflow editor |

## Export the workflow into the repo

After every meaningful change to the workflow in n8n Cloud, export it
and overwrite the placeholder in this repo so the repo is the canonical
record:

1. n8n Cloud editor → ⋮ menu → **Download** → save the JSON.
2. Move the downloaded file:
   ```sh
   mv ~/Downloads/ai-support-agent.json agent/workflow/ai-support-agent.json
   git add agent/workflow/ai-support-agent.json
   git commit -m "[Spec Kit] Sync n8n Cloud workflow → repo"
   git push
   ```

The exported JSON is for repo bookkeeping / portfolio readability — it
is **not** auto-imported back to n8n Cloud. The runtime is whatever is
saved in the n8n Cloud editor; the repo file is a snapshot.

## Acceptance check (replaces old T012)

T012 (VPS provisioning) is dropped. The replacement acceptance check is:

- [ ] The chat URL above loads in a browser without TLS errors.
- [ ] Sending an MSA test message in the chat returns an MSA reply
      derived from the KB.
- [ ] Sending an Egyptian-dialect test message returns an Egyptian
      reply.
- [ ] Sending an off-topic question returns a refusal in matching
      register, not a fabricated answer.
- [ ] An Execution is recorded under the workflow's Executions tab and
      contains the visitor input + structured Gemini response + final
      reply.

When all five checks pass, the foundation (Phase 1 + Phase 2) is
complete and Phase 3 (US1) can begin.
