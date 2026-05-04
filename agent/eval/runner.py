#!/usr/bin/env python3
"""
Evaluation harness for the AI Customer Support Agent.

Two modes:

  1) Run an eval set against the deployed Chat Trigger webhook and write a
     JSONL results file (one record per prompt) conforming to
     specs/001-ai-support-agent/contracts/eval-record.schema.json.

         python agent/eval/runner.py \\
             --set msa \\
             --webhook https://support-demo.example.com/webhook/chat \\
             --model gemini-2.5-flash-lite

     Multi-turn mode preserves sessionId across turns of the same
     conversation:

         python agent/eval/runner.py \\
             --set multiturn \\
             --webhook https://support-demo.example.com/webhook/chat \\
             --model gemini-2.5-flash-lite

  2) Aggregate `verdict` values from a results file and print a pass-rate
     against the matching SC threshold:

         python agent/eval/runner.py --grade-summary path/to/results.jsonl

Eval-set file format (JSONL, one record per line):

  Single-turn sets (msa, egyptian, adversarial):
      {"prompt_id": "<id>", "prompt": "<text>", "expected_topic": "..."}
      (extra fields like `expected_action` are tolerated and copied through)

  Multi-turn set (multiturn):
      {"prompt_id": "<id>", "conversation_id": "<id>", "turn_index": 0, "prompt": "<text>"}
      Records sharing a `conversation_id` are sent on the same sessionId
      in turn_index order.

Results-file format: one JSON line per prompt, schema in
contracts/eval-record.schema.json. The `verdict` field starts as null;
the human reviewer fills it in by editing the file.
"""
from __future__ import annotations

import argparse
import collections
import json
import sys
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

try:
    import httpx
except ImportError:
    sys.stderr.write(
        "[runner] httpx is not installed. Run:\n"
        "    pip install -r agent/eval/requirements.txt\n"
    )
    sys.exit(2)


REPO_ROOT = Path(__file__).resolve().parents[2]
EVAL_DIR = REPO_ROOT / "agent" / "eval"
SETS_DIR = EVAL_DIR / "sets"
RESULTS_DIR = EVAL_DIR / "results"

VALID_SETS = ("msa", "egyptian", "adversarial", "multiturn")

SC_THRESHOLDS: dict[str, tuple[float, str]] = {
    # set name → (pass-rate threshold, source)
    "msa": (0.90, "SC-001"),
    "egyptian": (0.85, "SC-002"),
    "adversarial": (0.95, "SC-003"),
    "multiturn": (1.00, "scripted multi-turn — every back-reference must resolve"),
}


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def _load_set(set_name: str) -> list[dict[str, Any]]:
    path = SETS_DIR / f"{set_name}.jsonl"
    if not path.exists():
        sys.stderr.write(
            f"[runner] eval set not found: {path}\n"
            f"[runner] (this set is authored as part of the user-story phase that depends on it)\n"
        )
        sys.exit(1)
    records = []
    for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        line = line.strip()
        if not line or line.startswith("//"):
            continue
        try:
            records.append(json.loads(line))
        except json.JSONDecodeError as exc:
            sys.stderr.write(f"[runner] {path.name}:{lineno}: invalid JSON — {exc}\n")
            sys.exit(1)
    if not records:
        sys.stderr.write(f"[runner] eval set is empty: {path}\n")
        sys.exit(1)
    return records


def _post_chat(webhook: str, session_id: str, chat_input: str, timeout: float) -> tuple[str, int, str]:
    """Returns (response_output, latency_ms, raw_response_json_string)."""
    body = {"sessionId": session_id, "action": "sendMessage", "chatInput": chat_input}
    started = time.monotonic()
    try:
        resp = httpx.post(webhook, json=body, timeout=timeout)
        elapsed_ms = int((time.monotonic() - started) * 1000)
    except httpx.HTTPError as exc:
        elapsed_ms = int((time.monotonic() - started) * 1000)
        return f"<network error: {exc}>", elapsed_ms, ""
    raw = resp.text
    if resp.status_code != 200:
        return f"<http {resp.status_code}: {raw[:200]}>", elapsed_ms, raw
    try:
        payload = resp.json()
    except json.JSONDecodeError:
        return f"<non-json response: {raw[:200]}>", elapsed_ms, raw
    output = payload.get("output", "")
    if not isinstance(output, str):
        output = json.dumps(output, ensure_ascii=False)
    return output, elapsed_ms, raw


def _detect_register_from_response(_raw_payload: str) -> str:
    """The Chat Trigger contract intentionally does not surface `register` to
    visitors. The runner records `unknown` and leaves dialect verification
    to the human reviewer (or to a future debug-mode response shape).
    """
    return "unknown"


def cmd_run(args: argparse.Namespace) -> int:
    set_name: str = args.set
    if set_name not in VALID_SETS:
        sys.stderr.write(f"[runner] --set must be one of {VALID_SETS}\n")
        return 1

    records = _load_set(set_name)
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%dT%H%M%S")
    out_path = RESULTS_DIR / f"{set_name}-{timestamp}.jsonl"

    eval_set_for_record = "adversarial" if set_name == "adversarial" else (
        "egyptian" if set_name == "egyptian" else (
            "msa" if set_name == "msa" else "msa"  # multiturn rolls up under msa for schema
        )
    )

    print(f"[runner] eval_set={set_name}  webhook={args.webhook}")
    print(f"[runner] model={args.model}  records={len(records)}")
    print(f"[runner] writing → {out_path}")

    # Group multiturn records by conversation_id, preserve turn_index order.
    if set_name == "multiturn":
        grouped: dict[str, list[dict[str, Any]]] = collections.defaultdict(list)
        for r in records:
            cid = r.get("conversation_id") or r.get("prompt_id", "default")
            grouped[cid].append(r)
        # Each conversation reuses one sessionId across its turns.
        order: list[tuple[str, str, dict[str, Any]]] = []
        for cid, turns in grouped.items():
            session_id = str(uuid.uuid4())
            turns.sort(key=lambda r: r.get("turn_index", 0))
            for r in turns:
                order.append((cid, session_id, r))
    else:
        # Single-turn sets get a fresh sessionId per prompt.
        order = [(str(uuid.uuid4()), str(uuid.uuid4()), r) for r in records]

    written = 0
    with out_path.open("w", encoding="utf-8") as fh:
        for _conv, session_id, rec in order:
            prompt = rec["prompt"]
            output, latency_ms, raw = _post_chat(
                args.webhook, session_id, prompt, timeout=args.timeout
            )
            register = _detect_register_from_response(raw)
            line = {
                "eval_set": eval_set_for_record,
                "prompt_id": rec["prompt_id"],
                "prompt": prompt,
                "response": output,
                "detected_register": register,
                "latency_ms": latency_ms,
                "model": args.model,
                "run_at": _now_iso(),
                "verdict": None,
            }
            # Carry useful eval-set metadata forward, even though the schema
            # does not require it — reviewers benefit from seeing
            # expected_topic / expected_action inline.
            for k in ("expected_topic", "expected_action", "conversation_id", "turn_index"):
                if k in rec:
                    line[k] = rec[k]
            fh.write(json.dumps(line, ensure_ascii=False) + "\n")
            written += 1
            print(f"  [{written:>3}/{len(order)}] {rec['prompt_id']:<20s}  {latency_ms:>5d} ms")

    print(f"\n[runner] wrote {written} records to {out_path}")
    print(f"[runner] grade them by setting `verdict` per record, then run:")
    print(f"    python {Path(__file__).relative_to(REPO_ROOT)} --grade-summary {out_path.relative_to(REPO_ROOT)}")
    return 0


def cmd_grade_summary(path_str: str) -> int:
    path = Path(path_str)
    if not path.is_absolute():
        # Allow paths relative to repo root for ergonomics.
        candidate = REPO_ROOT / path
        if candidate.exists():
            path = candidate
    if not path.exists():
        sys.stderr.write(f"[runner] results file not found: {path_str}\n")
        return 1

    counts: dict[str, int] = collections.Counter()
    by_set: dict[str, collections.Counter] = collections.defaultdict(collections.Counter)

    for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        line = line.strip()
        if not line:
            continue
        try:
            rec = json.loads(line)
        except json.JSONDecodeError as exc:
            sys.stderr.write(f"[runner] {path.name}:{lineno}: invalid JSON — {exc}\n")
            return 1
        verdict = rec.get("verdict")
        eval_set = rec.get("eval_set", "<unknown>")
        counts[str(verdict)] += 1
        by_set[eval_set][str(verdict)] += 1

    print(f"\n[grade-summary] file: {path}")
    print(f"  total records: {sum(counts.values())}")
    for v in ("pass", "fail", "partial", "None"):
        print(f"    verdict={v:<8s} {counts.get(v, 0):>4d}")

    # Per-set summary against SC thresholds.
    for set_name, c in by_set.items():
        graded = c.get("pass", 0) + c.get("fail", 0) + c.get("partial", 0)
        total = sum(c.values())
        passing = c.get("pass", 0)
        if graded == 0:
            print(f"\n  {set_name}: 0 of {total} records graded yet")
            continue
        rate = passing / graded
        threshold, sc_id = SC_THRESHOLDS.get(set_name, (None, "—"))
        line_summary = f"  {set_name}: {passing}/{graded} pass = {rate:.0%}"
        if threshold is not None:
            verdict = "✓ MEETS" if rate >= threshold else "✗ BELOW"
            line_summary += f"  (threshold {threshold:.0%} from {sc_id} → {verdict})"
        print(line_summary)

    return 0


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description="AI support-agent eval harness.")
    p.add_argument("--set", choices=VALID_SETS, help="Which eval set to run.")
    p.add_argument("--webhook", help="Chat Trigger webhook URL (e.g. https://.../webhook/chat).")
    p.add_argument("--model", default="gemini-2.5-flash-lite", help="Gemini variant label to record on each result (default: gemini-2.5-flash-lite, matching the locked workflow choice per Q9 clarification).")
    p.add_argument("--timeout", type=float, default=30.0, help="HTTP timeout per request, seconds (default 30).")
    p.add_argument("--grade-summary", metavar="RESULTS_FILE", help="Aggregate verdicts from a results JSONL file and print pass-rates.")

    args = p.parse_args(argv)

    if args.grade_summary:
        return cmd_grade_summary(args.grade_summary)

    if not args.set or not args.webhook:
        p.error("--set and --webhook are required (or pass --grade-summary).")
    return cmd_run(args)


if __name__ == "__main__":
    sys.exit(main())
