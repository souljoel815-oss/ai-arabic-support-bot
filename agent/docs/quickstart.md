# Quickstart pointer

> The canonical quickstart lives at
> [`specs/001-ai-support-agent/quickstart.md`](../../specs/001-ai-support-agent/quickstart.md).
> This file exists so a developer cloning the repo finds the deploy
> path without spelunking under `specs/`.

## Two-minute summary

The workflow runs on **n8n Cloud** (managed). To run your own instance:

1. Create an n8n Cloud workspace (Starter tier or higher)
2. Add a Google Gemini API credential in the n8n Cloud UI
3. Build the workflow following [`../workflow/README.md`](../workflow/README.md) — Chat Trigger → load_kb → build_llm_input → AI Agent (Gemini) → validate_and_overwrite → Respond to Webhook
4. Paste the contents of [`../kb/ecommerce-faq.json`](../kb/ecommerce-faq.json) into the workflow's `load_kb` Code node
5. Paste [`../prompts/system.md`](../prompts/system.md) into the AI Agent node's system message slot
6. Activate the workflow → smoke-test the public chat URL

## Live demo (this project's own deployment)

```
https://guillaume120.app.n8n.cloud/webhook/da1f362e-200c-4255-a624-9bb6544821d0/chat
```

## Run the eval suite locally

```sh
export N8N_CHAT_URL=<your chat URL>
cd agent/eval
python -m venv .venv && . .venv/bin/activate     # or .\.venv\Scripts\activate on Windows
pip install -r requirements.txt
python runner.py --set msa         --webhook "$N8N_CHAT_URL"
python runner.py --set egyptian    --webhook "$N8N_CHAT_URL"
python runner.py --set adversarial --webhook "$N8N_CHAT_URL"
python runner.py --set multiturn   --webhook "$N8N_CHAT_URL"
```

Grade the resulting JSONL files using
[`../eval/reviewer-template.md`](../eval/reviewer-template.md), then
compute pass-rates with `--grade-summary <results-file>`.

For everything else — full deployment runbook, ops table,
acceptance criteria, troubleshooting — read the canonical quickstart at
[`specs/001-ai-support-agent/quickstart.md`](../../specs/001-ai-support-agent/quickstart.md).
