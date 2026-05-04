# n8n Workflow Buildbook (n8n Cloud)

> **The workflow runs on n8n Cloud.** This file is the recipe for
> building it in the n8n Cloud editor; the JSON file in this directory
> is a snapshot of what's been exported from the editor (and a
> placeholder until the first export lands).
>
> Why a buildbook instead of authoritative committed JSON? n8n workflow
> JSON is tightly coupled to exact node `typeVersion`s and
> sub-connection wiring (`ai_languageModel`, `ai_memory`, …) that vary
> across n8n versions. Authoring the workflow once in the editor on
> the n8n version actually running in your Cloud workspace produces
> JSON that round-trips cleanly when you export it.

---

## Prerequisites

Before you start, confirm:

- [ ] You have an n8n Cloud workspace (see
      `agent/deploy/n8n-cloud-setup.md`).
- [ ] You have created a **Google Gemini (PaLM) API** credential in
      n8n Cloud and pasted your Gemini API key.
- [ ] You have read
      `specs/001-ai-support-agent/contracts/llm-prompt.md` so the
      structured response shape and registers are clear.

---

## 1. Create the workflow

1. **Workflows → New** → name it `ai-support-agent`.
2. Open the workflow's **Settings** (gear icon) and set:
   - **Save successful executions**: yes
   - **Save failed executions**: yes
   - **Timezone**: `Africa/Cairo`

---

## 2. Add nodes (in order)

### Node 1 — Chat Trigger

- Add node: **`@n8n/n8n-nodes-langchain.chatTrigger`**.
- **Public**: ON.
- **Authentication**: None.
- **Initial Messages** (optional, shown to visitor on open): leave the
  default greeting or paste a short bilingual greeting.
- **Response Mode**: **`Using Respond to Webhook node`** (this is
  important — the default "last node" mode would skip our validation
  layer).
- Note the public chat URL n8n shows you; this is what you link from
  the portfolio.

### Node 2 — Code: `load_kb`

- Add node: **`n8n-nodes-base.code`** (Code).
- **Mode**: Run Once for All Items.
- **Language**: JavaScript.
- **Code**: paste the contents of
  `agent/kb/ecommerce-faq.json` as a JS literal (the canonical KB
  remains the file in this repo; this node is just the runtime mirror).

  ```javascript
  // === KB INLINE ===
  // Source of truth: agent/kb/ecommerce-faq.json in the repo.
  // To update: edit the repo file, commit, then copy its contents
  // into the array below and save the workflow.
  const kb = [
    /* paste the contents of agent/kb/ecommerce-faq.json here */
  ];

  return [{
    json: {
      sessionId: $json.sessionId,
      chatInput: ($json.chatInput || '').slice(0, 4096),
      chatInputTruncated: ($json.chatInput || '').length > 4096,
      kb,
    },
  }];
  ```

- Connect: `Chat Trigger` → `load_kb`.

### Node 3 — Code: `build_llm_input`

- Add node: **`n8n-nodes-base.code`** (Code).
- **Code**: implement keyword retrieval (top 5 candidates) + assemble
  the structured user message per `contracts/llm-prompt.md`:

  ```javascript
  const { sessionId, chatInput, chatInputTruncated, kb } = $json;
  const text = chatInput.toLowerCase();
  // Score by keyword hit count across both register lists.
  const scored = kb.map(entry => {
    const all = (entry.keywords_msa || []).concat(entry.keywords_egy || []);
    const hits = all.filter(k => text.includes(k.toLowerCase())).length;
    return { entry, hits };
  });
  scored.sort((a, b) => b.hits - a.hits);
  const candidates = scored.filter(s => s.hits > 0).slice(0, 5).map(s => ({
    id: s.entry.id,
    topic: s.entry.topic,
    wording_msa: s.entry.wording_msa,
    wording_egy: s.entry.wording_egy,
  }));
  // If no keyword hit, pass the top-5 by topic spread so the model can
  // still see options (and decide refuse_off_topic if needed).
  const fallback = kb.slice(0, 5).map(e => ({
    id: e.id, topic: e.topic, wording_msa: e.wording_msa, wording_egy: e.wording_egy,
  }));
  const retrieved = candidates.length ? candidates : fallback;

  const userMessage = [
    '<visitor_message>',
    chatInput,
    '</visitor_message>',
    '',
    '<retrieved_kb>',
    JSON.stringify(retrieved, null, 2),
    '</retrieved_kb>',
    '',
    // <recent_turns> is injected by the AI Agent's memory module in T031.
  ].join('\n');

  return [{ json: { sessionId, userMessage, chatInputTruncated } }];
  ```

- Connect: `load_kb` → `build_llm_input`.

### Node 4 — AI Agent (LangChain)

- Add node: **`@n8n/n8n-nodes-langchain.agent`** (AI Agent).
- **Agent Type**: Conversational Agent.
- **System Message**: paste the contents of
  `agent/prompts/system.md`. (Source of truth is the repo file; the
  workflow holds a copy.)
- **Text**: `={{ $json.userMessage }}`.
- **Output Parsing**: enable **Structured Output** with the schema:

  ```json
  {
    "type": "object",
    "required": ["register", "action", "kb_entry_ids", "reply"],
    "properties": {
      "register": { "type": "string", "enum": ["msa","egyptian","arabizi","other"] },
      "action": { "type": "string", "enum": ["answer","refuse_off_topic","refuse_unsafe","refuse_unknown_language","refuse_role_override"] },
      "kb_entry_ids": { "type": "array", "items": { "type": "string" } },
      "reply": { "type": "string" }
    }
  }
  ```

- **Sub-connection — Chat Model**: connect a Gemini LM node (next).
- **Sub-connection — Memory**: leave empty for now; **Phase 5 / T031**
  adds a `Memory: Window Buffer` sub-node here keyed off `sessionId`
  with a 6-turn window.

### Node 5 — Google Gemini Chat Model

- Add sub-node: **`@n8n/n8n-nodes-langchain.lmChatGoogleGemini`**.
- **Credential**: the Gemini credential created during n8n Cloud setup.
- **Model**: `gemini-1.5-flash` (default; switch to `gemini-1.5-pro`
  during T040 bake-off if SC-002 fails on Flash).
- **Temperature**: `0.2` (favor faithful retrieval; deterministic).
- **Max Output Tokens**: 1024.
- Wire it to the AI Agent's **Chat Model** sub-input.

### Node 6 — Code: `validate_and_overwrite`

- Add node: **`n8n-nodes-base.code`** (Code).
- **Code**: validate the structured output, look up the matching refusal
  template, and overwrite `reply`. The refusal templates are inlined in
  the same Code node (source of truth: `agent/prompts/refusal-templates.md`):

  ```javascript
  // === REFUSAL TEMPLATES INLINE ===
  // Source of truth: agent/prompts/refusal-templates.md in the repo.
  // To update: edit the repo file, then copy the relevant strings here
  // and save the workflow.
  const tplMap = {
    "refuse_off_topic.msa":      "عذراً، أنا مختصّ فقط بأسئلة الدعم المتعلقة بالطلبات والإرجاع والشحن والدفع والحساب. هل يمكنني مساعدتك في أحد هذه المواضيع؟",
    "refuse_off_topic.egyptian": "آسف، أنا متخصص بس في أسئلة الدعم بتاعة الطلبات والمرتجعات والشحن والدفع والحساب. تحب اساعدك في حاجة من دي؟",
    "refuse_off_topic.arabizi":  "آسف، أنا متخصص بس في أسئلة الدعم بتاعة الطلبات والمرتجعات والشحن والدفع والحساب. تحب اساعدك في حاجة من دي؟",
    "refuse_unsafe.msa":         "لا أستطيع المساعدة في هذا الطلب. إن كنت بحاجة إلى دعم بشأن طلب أو حساب، يسعدني مساعدتك في ذلك.",
    "refuse_unsafe.egyptian":    "مش هاقدر اساعدك في الطلب ده. لو محتاج مساعدة في حاجة تخص الطلب أو الحساب، انا تحت أمرك.",
    "refuse_unsafe.arabizi":     "مش هاقدر اساعدك في الطلب ده. لو محتاج مساعدة في حاجة تخص الطلب أو الحساب، انا تحت أمرك.",
    "refuse_unknown_language.msa":      "I can only help in Arabic. أتمنى أن تطرح سؤالك باللغة العربية الفصحى أو باللهجة المصرية، وسأساعدك بكل سرور.",
    "refuse_unknown_language.egyptian": "I can only help in Arabic. ابعتلي سؤالك بالعربي الفصحى أو بالمصري وانا تحت أمرك.",
    "refuse_unknown_language.arabizi":  "I can only help in Arabic. ابعتلي سؤالك بالعربي الفصحى أو بالمصري وانا تحت أمرك.",
    "refuse_role_override.msa":      "لا يمكنني تغيير دوري؛ أنا هنا فقط للإجابة على أسئلة الدعم الخاصة بالمتجر. هل يمكنني مساعدتك في شيء يخص طلبك أو حسابك؟",
    "refuse_role_override.egyptian": "مش هاقدر اغير دوري؛ أنا هنا بس عشان اجاوب على أسئلة الدعم بتاعة المتجر. تحب اساعدك في حاجة بخصوص طلبك أو حسابك؟",
    "refuse_role_override.arabizi":  "مش هاقدر اغير دوري؛ أنا هنا بس عشان اجاوب على أسئلة الدعم بتاعة المتجر. تحب اساعدك في حاجة بخصوص طلبك أو حسابك؟",
  };

  // The AI Agent emits `output` as the parsed structured object.
  const out = $json.output || $json;
  const reg = out.register || 'msa';
  const act = out.action || 'refuse_unsafe';
  let reply = out.reply || '';
  if (act !== 'answer') {
    const key = `${act}.${reg}`;
    reply = tplMap[key] || tplMap[`${act}.msa`] || reply || '';
  }
  return [{
    json: {
      sessionId: $('build_llm_input').first().json.sessionId,
      register: reg,
      action: act,
      kb_entry_ids: out.kb_entry_ids || [],
      reply,
    },
  }];
  ```

- Connect: `AI Agent` → `validate_and_overwrite`.

### Node 7 — Respond to Webhook

- Add node: **`n8n-nodes-base.respondToWebhook`**.
- **Respond With**: JSON.
- **Response Body**:

  ```json
  { "output": "={{ $json.reply }}" }
  ```

- **Response Code**: 200.
- Connect: `validate_and_overwrite` → `Respond to Webhook`.

> **Note — no `log_turn` node.** Conversation logging (FR-012) is
> satisfied by n8n Cloud's built-in execution history. Open the
> workflow's Executions tab to inspect any per-turn payload —
> visitor input, structured Gemini response, final reply, register,
> and chosen KB entry IDs are all captured automatically.

---

## 3. Save, activate, smoke-test

1. **Save** the workflow.
2. Toggle **Active** in the top-right.
3. Open the public chat URL (Node 1) in a private window.
4. Send a test message in MSA: *"كيف ألغي طلبي؟"*. Expect an MSA reply
   referencing the orders KB entry.
5. Send a test in Egyptian: *"ازاي ألغي الأوردر؟"*. Expect an
   Egyptian-dialect reply.
6. Send something off-topic: *"What's the weather?"* — expect the
   `refuse_unknown_language` template (English visitor → MSA refusal
   per FR-010).

If any step fails, open the Executions tab and inspect node-by-node
payloads.

---

## 4. Export and commit

After the smoke-test passes:

```sh
# In the n8n Cloud editor: ⋮ menu → Download → save the JSON
# Move the downloaded file over the placeholder:
mv ~/Downloads/ai-support-agent.json agent/workflow/ai-support-agent.json
git add agent/workflow/ai-support-agent.json
git commit -m "[Spec Kit] Sync n8n Cloud workflow → repo"
```

The exported JSON IS the artifact for repo bookkeeping; this README is
the recipe.

---

## What this workflow does NOT do yet

- **No session memory** — multi-turn (US3) is added in **T031** by
  attaching a `Memory: Window Buffer` sub-node to the AI Agent.
- **No latency timing recorded server-side** — the `latency_ms` field
  is filled by the eval runner client-side.
- **No Gemini variant bake-off** — that is **T040** (Phase 7).
