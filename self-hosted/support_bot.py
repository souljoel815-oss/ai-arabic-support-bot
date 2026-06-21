#!/usr/bin/env python3
"""Self-hosted bilingual (MSA / Egyptian / Arabizi) e-commerce support bot.

No external LLM: dialect-aware keyword retrieval over a pre-translated bilingual
knowledge base. Stdlib only. Serves a chat UI at / and answers at POST /chat.
"""
from __future__ import annotations

import json
import re
import unicodedata
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

BASE = Path(__file__).resolve().parent
KB = json.loads((BASE.parent / "agent" / "kb" / "ecommerce-faq.json").read_text(encoding="utf-8"))
PORT = 8504
HOST = "127.0.0.1"

EGY_MARKERS = [
    "ازاي", "عايز", "عاوز", "ايه", "ده", "دي", "عشان", "مش", "بتاع", "الاوردر",
    "الباسوورد", "ينفع", "ازيك", "امتى", "فين", "كده", "اعمل ايه", "معلش", "خالص",
]
ARABIZI_HINT = ["ezzay", "3ayz", "3and", "msh", "el ", "fen", "3awz", "ana", "eih", "izay"]
GREET = ["سلام", "السلام", "اهلا", "أهلا", "مرحبا", "هاي", "hi", "hello", "صباح", "مساء", "ezzayak", "ازيك"]


def _norm(t: str) -> str:
    t = unicodedata.normalize("NFKC", t).lower().strip()
    t = re.sub(r"[ً-ْـ]", "", t)          # diacritics + tatweel
    t = (t.replace("أ", "ا").replace("إ", "ا").replace("آ", "ا")
           .replace("ة", "ه").replace("ى", "ي").replace("ؤ", "و").replace("ئ", "ي"))
    t = re.sub(r"[^\w؀-ۿ ]+", " ", t)
    return re.sub(r"\s+", " ", t)


def detect_register(raw: str) -> str:
    n = _norm(raw)
    has_ar = bool(re.search(r"[؀-ۿ]", raw))
    if not has_ar:
        return "arabizi"
    if any(m in n for m in (_norm(x) for x in EGY_MARKERS)):
        return "egyptian"
    return "msa"


STOP = {"el", "al", "the", "my", "a", "an", "of", "in", "to", "is", "ال", "في", "من",
        "على", "ya", "ana", "اي", "ده", "دي", "هو", "هي", "مع", "عن", "او", "and"}


def _tokens(s: str) -> set:
    return {w for w in _norm(s).split() if len(w) >= 2 and w not in STOP}


def _score(entry: dict, raw: str) -> int:
    n = _norm(raw)
    itoks = _tokens(raw)
    s = 0
    for kw in entry.get("keywords_msa", []) + entry.get("keywords_egy", []):
        nk = _norm(kw)
        if nk and nk in n:            # full-phrase match = strong signal
            s += 3
        s += len(_tokens(kw) & itoks)  # shared tokens = weak signal
    return s


def answer(raw: str) -> dict:
    reg = detect_register(raw)
    n = _norm(raw)
    egy = reg in ("egyptian", "arabizi")

    if any(_norm(g) in (" " + n + " ") for g in GREET) and len(n.split()) <= 3:
        msg = ("أهلاً بيك! 👋 أنا مساعد خدمة العملاء. أقدر أساعدك في الأوردرات، الدفع، "
               "الشحن، الإرجاع، والحساب — اسأل اللي محتاجه." if egy else
               "مرحباً بك! 👋 أنا مساعد خدمة العملاء. يمكنني مساعدتك في الطلبات، الدفع، "
               "الشحن، الإرجاع، والحساب — تفضّل بسؤالك.")
        return {"reply": msg, "register": reg, "topic": "greeting", "grounded": False}

    best, best_score = None, 0
    for e in KB:
        s = _score(e, raw)
        if s > best_score:
            best, best_score = e, s

    if best and best_score > 0:
        reply = best["wording_egy"] if egy else best["wording_msa"]
        return {"reply": reply, "register": reg, "topic": best["topic"],
                "grounded": True, "score": best_score}

    refuse = ("آسف، معنديش معلومات عن الموضوع ده. أقدر أساعدك في الأوردرات، الدفع، "
              "الشحن، الإرجاع، والحساب. 🙏" if egy else
              "أعتذر، لا تتوفر لديّ معلومات حول هذا الموضوع. يمكنني مساعدتك في الطلبات، "
              "الدفع، الشحن، الإرجاع، والحساب. 🙏")
    return {"reply": refuse, "register": reg, "topic": "off_topic", "grounded": False}


PAGE = """<!doctype html><html lang="ar" dir="rtl"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>AI Arabic Support Agent — Live Demo</title>
<style>
:root{--bg:#0b0f1c;--card:#141a2e;--line:#243049;--txt:#e8ecf5;--mut:#8b96b0;--accent:#6d8bff;--accent2:#a855f7;--green:#34d399}
*{box-sizing:border-box;margin:0;padding:0;font-family:'Segoe UI',system-ui,sans-serif}
body{background:radial-gradient(900px 500px at 80% -5%,rgba(109,139,255,.12),transparent),var(--bg);color:var(--txt);min-height:100vh;display:flex;flex-direction:column;align-items:center;padding:20px}
.wrap{width:100%;max-width:680px;display:flex;flex-direction:column;height:calc(100vh - 40px)}
.head{display:flex;align-items:center;justify-content:space-between;gap:10px;padding:6px 4px 14px}
.head h1{font-size:18px;font-weight:800}.head .sub{font-size:12px;color:var(--mut)}
.pill{display:inline-flex;align-items:center;gap:6px;background:var(--card);border:1px solid var(--line);padding:5px 11px;border-radius:99px;font-size:12px;color:var(--mut)}
.dot{width:8px;height:8px;border-radius:99px;background:var(--green);box-shadow:0 0 8px var(--green)}
#chat{flex:1;overflow-y:auto;background:linear-gradient(180deg,var(--card),#0e1426);border:1px solid var(--line);border-radius:16px;padding:18px;display:flex;flex-direction:column;gap:12px}
.msg{max-width:82%;padding:11px 15px;border-radius:14px;font-size:15px;line-height:1.6;white-space:pre-wrap;word-wrap:break-word}
.bot{align-self:flex-start;background:#1b2236;border:1px solid var(--line);border-bottom-left-radius:4px}
.usr{align-self:flex-end;background:linear-gradient(90deg,var(--accent),var(--accent2));color:#fff;border-bottom-right-radius:4px}
.meta{font-size:10.5px;color:var(--mut);margin-top:5px;opacity:.85}
.chips{display:flex;flex-wrap:wrap;gap:7px;margin:12px 0 4px}
.chip{background:var(--card);border:1px solid var(--line);color:var(--mut);font-size:12.5px;padding:6px 12px;border-radius:99px;cursor:pointer;transition:.2s}
.chip:hover{border-color:var(--accent);color:var(--txt)}
.bar{display:flex;gap:9px;margin-top:12px}
#inp{flex:1;background:var(--card);border:1px solid var(--line);color:var(--txt);border-radius:12px;padding:13px 16px;font-size:15px}
#inp:focus{outline:none;border-color:var(--accent)}
#snd{background:linear-gradient(90deg,var(--accent),var(--accent2));color:#fff;border:none;border-radius:12px;padding:0 22px;font-weight:700;font-size:15px;cursor:pointer}
a{color:var(--accent);text-decoration:none}
</style></head><body><div class="wrap">
<div class="head">
  <div><h1>💬 مساعد خدمة العملاء</h1><div class="sub">بيرد بالفصحى · العامية المصرية · الفرانكو — حسب لهجتك</div></div>
  <span class="pill"><span class="dot"></span>Live · no LLM · KB retrieval</span>
</div>
<div id="chat"></div>
<div class="chips" id="chips"></div>
<div class="bar"><input id="inp" placeholder="اكتب سؤالك… مثلاً: ازاي ألغي الأوردر؟" autocomplete="off">
<button id="snd" onclick="send()">إرسال</button></div>
<div class="sub" style="text-align:center;margin-top:10px;color:var(--mut);font-size:11.5px">
  بيانات تجريبية لمتجر وهمي · <a href="https://portfolio.107-148-158-132.sslip.io">← كل المشاريع</a></div>
</div>
<script>
const chat=document.getElementById('chat'),inp=document.getElementById('inp');
const EX=["ازاي ألغي الأوردر؟","ما هي وسائل الدفع المتاحة؟","ezzay arga3 el order?","نسيت الباسوورد، أعمل إيه؟","امتى يوصل الشحن؟"];
document.getElementById('chips').innerHTML=EX.map(e=>`<span class="chip" onclick="ask('${e}')">${e}</span>`).join('');
function bubble(text,cls,meta){const d=document.createElement('div');d.className='msg '+cls;d.textContent=text;
  if(meta){const m=document.createElement('div');m.className='meta';m.textContent=meta;d.appendChild(m);}
  chat.appendChild(d);chat.scrollTop=chat.scrollHeight;}
function ask(t){inp.value=t;send();}
async function send(){const t=inp.value.trim();if(!t)return;bubble(t,'usr');inp.value='';
  try{const r=await fetch('/chat',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({message:t})});
    const d=await r.json();const reg={msa:'فصحى',egyptian:'مصري',arabizi:'فرانكو'}[d.register]||d.register;
    bubble(d.reply,'bot',`اللهجة: ${reg} · الموضوع: ${d.topic}${d.grounded?' · ✓ من قاعدة المعرفة':''}`);
  }catch(e){bubble('حصل خطأ، حاول تاني.','bot');}}
inp.addEventListener('keydown',e=>{if(e.key==='Enter')send();});
setTimeout(()=>bubble('أهلاً بيك! 👋 اسألني عن الأوردرات، الدفع، الشحن، الإرجاع، أو الحساب — بأي لهجة.','bot','جرّب الأمثلة تحت 👇'),300);
</script></body></html>"""


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _send(self, code, body, ctype):
        b = body.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(b)))
        self.end_headers()
        self.wfile.write(b)

    def do_GET(self):
        if self.path == "/" or self.path.startswith("/?"):
            self._send(200, PAGE, "text/html; charset=utf-8")
        elif self.path == "/healthz":
            self._send(200, "ok", "text/plain")
        else:
            self.send_error(404)

    def do_POST(self):
        if self.path != "/chat":
            self.send_error(404); return
        try:
            n = int(self.headers.get("Content-Length", 0))
            data = json.loads(self.rfile.read(n).decode("utf-8"))
            out = answer(str(data.get("message", "")))
            self._send(200, json.dumps(out, ensure_ascii=False), "application/json; charset=utf-8")
        except Exception as e:  # noqa: BLE001
            self._send(500, json.dumps({"reply": "error", "error": str(e)}), "application/json")


def main():
    print(f"[support-bot] http://{HOST}:{PORT} · {len(KB)} KB entries", flush=True)
    ThreadingHTTPServer((HOST, PORT), Handler).serve_forever()


if __name__ == "__main__":
    main()
