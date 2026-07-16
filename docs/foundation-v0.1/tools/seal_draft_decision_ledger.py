#!/usr/bin/env python3
"""Build a tamper-evident draft decision chain.

This is mechanical integrity, not authorization. A production repository must run
the equivalent operation through the protected gatekeeper described in
11-TRUST-AND-ENFORCEMENT.md.
"""

from __future__ import annotations

import hashlib
import json
import sys
import uuid
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
LEDGER = ROOT / "ledger" / "decisions.jsonl"
NAMESPACE = uuid.UUID("4bc504ee-9d70-4d8a-9c89-cfa798a05bcb")
BOOTSTRAP_PROMPT_DECISIONS = {f"D-{number:04d}" for number in range(1, 6)}
CREATOR_AUTHORITIES = {"creator-prompt", "creator-clarification", "creator-ratification"}


def canonical_bytes(record: dict) -> bytes:
    payload = {key: value for key, value in record.items() if key != "event_hash"}
    return json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")


def main() -> int:
    write = "--write" in sys.argv[1:]
    records = [json.loads(line) for line in LEDGER.read_text(encoding="utf-8").splitlines() if line.strip()]
    previous_hash = None
    rehashed: list[dict] = []
    for sequence, record in enumerate(records, 1):
        expected_id = f"D-{sequence:04d}"
        if record.get("id") != expected_id:
            raise ValueError(f"expected {expected_id}, got {record.get('id')}")
        record["event_id"] = "de_" + uuid.uuid5(NAMESPACE, expected_id).hex
        record["sequence"] = sequence
        record["previous_event_hash"] = previous_hash
        if record.get("authority") in CREATOR_AUTHORITIES:
            record["actor_id"] = "creator"
        else:
            record["actor_id"] = "foundation-design"
        # Preserve an explicitly bound creator receipt. The original prompt
        # capture remains the bootstrap default only for ratified creator facts
        # that predate a dedicated receipt.
        if record.get("status") == "ratified" and record.get("authority") in CREATOR_AUTHORITIES:
            if not record.get("approval_receipt_id"):
                if record.get("id") in BOOTSTRAP_PROMPT_DECISIONS:
                    record["approval_receipt_id"] = "RR-PROMPT-20260715"
                else:
                    raise ValueError(
                        f"{record.get('id')} is a ratified creator record without an explicit receipt"
                    )
        record["event_hash"] = hashlib.sha256(canonical_bytes(record)).hexdigest()
        previous_hash = record["event_hash"]
        rehashed.append(record)

    output = "".join(
        json.dumps(record, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n"
        for record in rehashed
    )
    if write:
        temporary = LEDGER.with_suffix(".jsonl.tmp")
        temporary.write_text(output, encoding="utf-8")
        temporary.replace(LEDGER)
        print(f"rehashed {len(rehashed)} draft decision events")
    else:
        print(output, end="")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
