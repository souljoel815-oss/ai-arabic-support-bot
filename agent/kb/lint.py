#!/usr/bin/env python3
"""
Lint the bilingual knowledge base against contracts/kb-entry.schema.json.

Validates:
  - JSON parses
  - Each entry conforms to the schema
  - wording_msa and wording_egy are non-empty (FR-014)
  - id values are unique within the file

Exit code: 0 on full pass, 1 on any failure.
Run: python agent/kb/lint.py
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

try:
    from jsonschema import Draft202012Validator
except ImportError:
    sys.stderr.write(
        "[lint] jsonschema is not installed. Run:\n"
        "    pip install -r agent/eval/requirements.txt\n"
    )
    sys.exit(2)


REPO_ROOT = Path(__file__).resolve().parents[2]
SCHEMA_PATH = (
    REPO_ROOT / "specs" / "001-ai-support-agent" / "contracts" / "kb-entry.schema.json"
)
KB_PATH = REPO_ROOT / "agent" / "kb" / "ecommerce-faq.json"


def main() -> int:
    if not SCHEMA_PATH.exists():
        sys.stderr.write(f"[lint] schema not found: {SCHEMA_PATH}\n")
        return 2
    if not KB_PATH.exists():
        sys.stderr.write(
            f"[lint] KB file not found: {KB_PATH}\n"
            "[lint] (this is expected before T008 seeds it; rerun after seeding)\n"
        )
        return 1

    schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
    validator = Draft202012Validator(schema)

    raw = KB_PATH.read_text(encoding="utf-8")
    try:
        entries = json.loads(raw)
    except json.JSONDecodeError as exc:
        sys.stderr.write(f"[lint] {KB_PATH.name}: invalid JSON — {exc}\n")
        return 1

    if not isinstance(entries, list):
        sys.stderr.write(f"[lint] {KB_PATH.name}: top-level value must be a JSON array\n")
        return 1

    failures = 0
    seen_ids: set[str] = set()

    for index, entry in enumerate(entries):
        label = f"[{index:>3}] {entry.get('id', '<missing-id>') if isinstance(entry, dict) else '<not-an-object>'}"

        errors = sorted(validator.iter_errors(entry), key=lambda e: e.path)
        if errors:
            failures += 1
            print(f"FAIL {label}")
            for err in errors:
                pointer = "/".join(str(p) for p in err.path) or "<root>"
                print(f"     - {pointer}: {err.message}")
            continue

        # Schema enforces minLength: 1 already, but keep an explicit check that
        # mirrors FR-014 wording so failures here read more clearly than a raw
        # schema error would.
        if not entry.get("wording_msa", "").strip():
            failures += 1
            print(f"FAIL {label}")
            print("     - wording_msa is empty (FR-014)")
            continue
        if not entry.get("wording_egy", "").strip():
            failures += 1
            print(f"FAIL {label}")
            print("     - wording_egy is empty (FR-014)")
            continue

        entry_id = entry["id"]
        if entry_id in seen_ids:
            failures += 1
            print(f"FAIL {label}")
            print(f"     - duplicate id: {entry_id}")
            continue
        seen_ids.add(entry_id)

        print(f"PASS {label}")

    total = len(entries)
    passed = total - failures
    print(f"\n[lint] {passed}/{total} entries passed")
    return 0 if failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
