# Refusal Templates

> Loaded by the n8n workflow's `validate_and_overwrite` Function node.
> When the LLM emits any `refuse_*` action, the workflow replaces its
> `reply` with the template below matching `(action, register)`.
>
> Format the workflow expects: a YAML/JSON-style key-value map keyed by
> `<action>.<register>`. The Function node parses this file by
> regex-locating the leading `~~~map` fence and treating the contents
> as plain key-value pairs (key on left of `:`, value as the rest of the
> line, single-line strings only).

~~~map
refuse_off_topic.msa: عذراً، أنا مختصّ فقط بأسئلة الدعم المتعلقة بالطلبات والإرجاع والشحن والدفع والحساب. هل يمكنني مساعدتك في أحد هذه المواضيع؟
refuse_off_topic.egyptian: آسف، أنا متخصص بس في أسئلة الدعم بتاعة الطلبات والمرتجعات والشحن والدفع والحساب. تحب اساعدك في حاجة من دي؟
refuse_off_topic.arabizi: آسف، أنا متخصص بس في أسئلة الدعم بتاعة الطلبات والمرتجعات والشحن والدفع والحساب. تحب اساعدك في حاجة من دي؟

refuse_unsafe.msa: لا أستطيع المساعدة في هذا الطلب. إن كنت بحاجة إلى دعم بشأن طلب أو حساب، يسعدني مساعدتك في ذلك.
refuse_unsafe.egyptian: مش هاقدر اساعدك في الطلب ده. لو محتاج مساعدة في حاجة تخص الطلب أو الحساب، انا تحت أمرك.
refuse_unsafe.arabizi: مش هاقدر اساعدك في الطلب ده. لو محتاج مساعدة في حاجة تخص الطلب أو الحساب، انا تحت أمرك.

refuse_unknown_language.msa: I can only help in Arabic. أتمنى أن تطرح سؤالك باللغة العربية الفصحى أو باللهجة المصرية، وسأساعدك بكل سرور.
refuse_unknown_language.egyptian: I can only help in Arabic. ابعتلي سؤالك بالعربي الفصحى أو بالمصري وانا تحت أمرك.
refuse_unknown_language.arabizi: I can only help in Arabic. ابعتلي سؤالك بالعربي الفصحى أو بالمصري وانا تحت أمرك.

refuse_role_override.msa: لا يمكنني تغيير دوري؛ أنا هنا فقط للإجابة على أسئلة الدعم الخاصة بالمتجر. هل يمكنني مساعدتك في شيء يخص طلبك أو حسابك؟
refuse_role_override.egyptian: مش هاقدر اغير دوري؛ أنا هنا بس عشان اجاوب على أسئلة الدعم بتاعة المتجر. تحب اساعدك في حاجة بخصوص طلبك أو حسابك؟
refuse_role_override.arabizi: مش هاقدر اغير دوري؛ أنا هنا بس عشان اجاوب على أسئلة الدعم بتاعة المتجر. تحب اساعدك في حاجة بخصوص طلبك أو حسابك؟
~~~

## Notes

- The `arabizi.*` rows are intentional duplicates of the `egyptian.*`
  rows: per `system.md` §2 the agent always replies in Egyptian Arabic
  **script** to Arabizi visitors, so the refusal text is identical.
- For `refuse_unknown_language` we lead with one English line so an
  English-speaking visitor knows immediately that they hit a language
  mismatch, then continue in Arabic as required by FR-010.
- All Egyptian phrasings here are first-pass — they MUST be polished
  by a native Egyptian-Arabic reviewer during Phase 6 (T036) before
  SC-003 sign-off. Mark any drift in tone or register on the reviewer
  template.
