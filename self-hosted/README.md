# Self-hosted version (no external LLM)

This is a dependency-free, always-on implementation of the same bilingual
support agent. Instead of calling an LLM, it does **dialect-aware keyword
retrieval** over the canonical bilingual knowledge base
([`../agent/kb/ecommerce-faq.json`](../agent/kb/ecommerce-faq.json)):

1. **Register detection** — Arabic script + Egyptian markers → Egyptian; Latin → Arabizi; otherwise MSA.
2. **Retrieval** — token-overlap + full-phrase scoring over `keywords_msa` + `keywords_egy`; the top entry wins.
3. **Grounded reply** — returns the entry's `wording_egy` (Egyptian/Arabizi) or `wording_msa` (MSA); off-KB questions get a polite refusal in the detected register.

Stdlib only (Python 3.11+), no API keys, no paid services — which is why it
can run as an always-on live demo.

## Run

```bash
cd self-hosted
python3 support_bot.py          # serves http://127.0.0.1:8504
```

Then open the page, or POST to `/chat`:

```bash
curl -X POST http://127.0.0.1:8504/chat -H "Content-Type: application/json" \
  -d '{"message":"ازاي ألغي الأوردر؟"}'
```

## Live

▶ **https://chat.107-148-158-132.sslip.io**

> Trade-off vs the n8n + Gemini build: no free-form generation and no on-the-fly
> dialect translation — it retrieves pre-translated answers. That makes it
> 100% reproducible and free to host, at the cost of the LLM's flexibility on
> phrasings far from the KB keywords.
