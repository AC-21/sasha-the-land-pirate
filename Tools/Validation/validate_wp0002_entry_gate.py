#!/usr/bin/env python3
"""Verify the protected WP-0002 authority gate after materialized activation."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import secrets
import stat
import sys
from pathlib import Path


ENTRY_GATE_CONTRACT_VERSION = "wp0002-entry-gate-v1"
PACKET_PATH = "docs/foundation-v0.1/work-packets/proposed/WP-0002.json"
STATE_PATH = "docs/foundation-v0.1/governance/ratification-state.json"
RECEIPT_ROOT = "docs/foundation-v0.1/ledger/receipts"
DECISIONS_PATH = "docs/foundation-v0.1/ledger/decisions.jsonl"
ACTIVE_STATUSES = {"active", "verifying", "candidate"}
REQUIRED_CLI_CLAIM = "AUTHORIZE-CITY-COMPARISON"

# This checker is a frozen authority seam. These tuples intentionally duplicate
# the protected ratification-state semantics instead of trusting candidate
# requirements to describe what must be proved.
EXACT_DECISION_REQUIREMENTS = (
    ("D-0006", ("ratified",), ("RATIFY-THESIS",), "block-and-revise-constitution-or-packet"),
    ("D-0007", ("ratified",), ("DRIVE", "COMMAND"), "block-and-revise-packet"),
    ("D-0008", ("ratified",), ("SLOWED", "PAUSED", "FULL"), "block-and-revise-packet"),
    ("D-0009", ("ratified",), ("SCARS",), "branch-to-harsh-or-custom-cruelty-proof-packet"),
    ("D-0010", ("ratified",), ("CULTURES",), "branch-to-conquest-compatible-packet"),
    ("D-0011", ("ratified",), ("ACCENT", "NONE"), "branch-to-combat-pillar-packet"),
    ("D-0012", ("ratified",), ("RATIFY-SLICE",), "block-and-revise-slice-and-packet"),
    ("D-0021", ("ratified",), ("SOLO-OFFLINE",), "branch-to-connected-architecture-packet"),
    ("D-0029", ("ratified",), ("ROUTE+ROAD",), "branch-to-selected-topology-packet"),
    ("D-0035", ("ratified",), ("RATIFY-CORE",), "block-and-revise-constitution-or-packet"),
    ("D-0036", ("ratified",), ("TITLE-AND-PROTAGONIST-SASHA",), "block-and-repair-title-protagonist-binding"),
    ("D-0037", ("ratified",), ("COLONY-HUMANS-ROBOTS-OR-MIXED",), "block-and-revise-composition-proof"),
)
EXACT_RECEIPT_REQUIREMENTS = (
    (
        "accept-WP-0002",
        ("WP-0002",),
        ("ACCEPT-WP-0002",),
        "packet-acceptance",
    ),
    (
        "authorize-city-comparison",
        ("D-0030", "WP-0002"),
        ("AUTHORIZE-CITY-COMPARISON",),
        "creator-authorization",
    ),
    (
        "activate-WP-0002-local-development",
        ("WP-0002",),
        ("A1-LOCAL-BOUNDARY-VERIFIED", "ACTIVATE-A1-WP-0002"),
        "packet-activation",
    ),
)
EXACT_ISSUER_ROLE = "creator"
EXACT_RESOLVER_TYPE = "external-protected"


def _load_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"))


def _load_decisions(path: Path) -> list[dict]:
    records: list[dict] = []
    for line_no, raw in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not raw.strip():
            raise ValueError(f"{DECISIONS_PATH}:{line_no}: blank line")
        record = json.loads(raw)
        if not isinstance(record, dict):
            raise ValueError(f"{DECISIONS_PATH}:{line_no}: record is not an object")
        records.append(record)
    return records


def _subject_claims(receipt: dict) -> dict[str, set[str]]:
    result: dict[str, set[str]] = {}
    for item in receipt.get("subject_claims", []):
        if isinstance(item, dict) and isinstance(item.get("subject_id"), str):
            result.setdefault(item["subject_id"], set()).update(item.get("claims", []))
    return result


def _active_decision_heads(
    records: list[dict],
) -> tuple[dict[str, dict], list[str]]:
    errors: list[str] = []
    records_by_id: dict[str, dict] = {}
    superseders: dict[str, list[str]] = {}
    previous_hash: str | None = None
    for sequence, record in enumerate(records, 1):
        record_id = record.get("id")
        if not isinstance(record_id, str) or not re.fullmatch(r"D-\d{4}", record_id):
            errors.append(f"decision ledger record {sequence} has an invalid id")
            continue
        if record_id in records_by_id:
            errors.append(f"decision ledger repeats {record_id}")
            continue
        if record.get("sequence") != sequence:
            errors.append(f"{record_id} has incorrect decision sequence")
        if record.get("previous_event_hash") != previous_hash:
            errors.append(f"{record_id} has a broken previous_event_hash chain")
        canonical = {key: value for key, value in record.items() if key != "event_hash"}
        calculated_hash = hashlib.sha256(
            json.dumps(
                canonical,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=False,
            ).encode("utf-8")
        ).hexdigest()
        event_hash = record.get("event_hash")
        if event_hash != calculated_hash:
            errors.append(f"{record_id} event_hash does not match its canonical record")
        predecessor = record.get("supersedes")
        if predecessor is not None:
            if predecessor not in records_by_id:
                errors.append(f"{record_id} supersedes an unknown or later decision")
            elif isinstance(predecessor, str):
                superseders.setdefault(predecessor, []).append(record_id)
        records_by_id[record_id] = record
        previous_hash = event_hash if isinstance(event_hash, str) else None

    heads: dict[str, dict] = {}
    for root_id, _statuses, _claims, _mismatch in EXACT_DECISION_REQUIREMENTS:
        current = root_id
        visited: set[str] = set()
        if current not in records_by_id:
            errors.append(f"decision ledger lacks required root {root_id}")
            continue
        while current in superseders:
            if current in visited:
                errors.append(f"decision supersession cycle reaches {current}")
                break
            visited.add(current)
            children = superseders[current]
            if len(children) != 1:
                errors.append(f"decision lineage {root_id} forks at {current}: {children}")
                break
            current = children[0]
        else:
            head = records_by_id.get(current)
            if head is not None:
                heads[root_id] = head
    return heads, errors


def _exact_gate_errors(gate: dict, packet_contract: str) -> list[str]:
    errors: list[str] = []
    decisions = gate.get("decision_requirements")
    receipts = gate.get("receipt_requirements")
    if not isinstance(decisions, list) or len(decisions) != len(
        EXACT_DECISION_REQUIREMENTS
    ):
        errors.append("gate decision requirements differ from the exact WP-0002 tuple")
        decisions = []
    if not isinstance(receipts, list) or len(receipts) != len(
        EXACT_RECEIPT_REQUIREMENTS
    ):
        errors.append("gate receipt requirements differ from the exact WP-0002 tuple")
        receipts = []

    decision_keys = {
        "decision_id",
        "accepted_statuses",
        "allowed_claims",
        "receipt_id",
        "required_receipt_kind",
        "required_issuer_role",
        "required_resolver_type",
        "required_subject_contract_sha256",
        "mismatch_action",
    }
    for requirement, expected in zip(decisions, EXACT_DECISION_REQUIREMENTS):
        decision_id, statuses, claims, mismatch_action = expected
        expected_values = {
            "decision_id": decision_id,
            "accepted_statuses": list(statuses),
            "allowed_claims": list(claims),
            "required_receipt_kind": "decision-ratification",
            "required_issuer_role": EXACT_ISSUER_ROLE,
            "required_resolver_type": EXACT_RESOLVER_TYPE,
            "required_subject_contract_sha256": {"WP-0002": packet_contract},
            "mismatch_action": mismatch_action,
        }
        if not isinstance(requirement, dict):
            errors.append(f"{decision_id} gate requirement is not an object")
            continue
        if set(requirement) != decision_keys:
            errors.append(f"{decision_id} gate requirement fields differ")
        for field, value in expected_values.items():
            if requirement.get(field) != value:
                errors.append(f"{decision_id} gate {field} differs from {value!r}")

    receipt_keys = {
        "purpose",
        "receipt_id",
        "subject_ids",
        "required_claims",
        "required_receipt_kind",
        "required_issuer_role",
        "required_resolver_type",
        "required_subject_contract_sha256",
    }
    for requirement, expected in zip(receipts, EXACT_RECEIPT_REQUIREMENTS):
        purpose, subject_ids, claims, receipt_kind = expected
        expected_values = {
            "purpose": purpose,
            "subject_ids": list(subject_ids),
            "required_claims": list(claims),
            "required_receipt_kind": receipt_kind,
            "required_issuer_role": EXACT_ISSUER_ROLE,
            "required_resolver_type": EXACT_RESOLVER_TYPE,
            "required_subject_contract_sha256": {"WP-0002": packet_contract},
        }
        if not isinstance(requirement, dict):
            errors.append(f"{purpose} gate requirement is not an object")
            continue
        if set(requirement) != receipt_keys:
            errors.append(f"{purpose} gate requirement fields differ")
        for field, value in expected_values.items():
            if requirement.get(field) != value:
                errors.append(f"{purpose} gate {field} differs from {value!r}")
    return errors


def _validate_receipt(
    label: str,
    receipt: dict | None,
    packet_contract: str,
    *,
    receipt_kind: str,
    claim_subject: str | tuple[str, ...],
    required_claims: set[str] | None = None,
    allowed_claims: set[str] | None = None,
    exact_subject_ids: tuple[str, ...] | None = None,
) -> list[str]:
    if receipt is None:
        return [f"{label} receipt is missing"]
    errors: list[str] = []
    resolver = receipt.get("artifact_resolver")
    expected_fields = {
        "receipt_kind": receipt_kind,
        "issuer_role": EXACT_ISSUER_ROLE,
    }
    for field, expected in expected_fields.items():
        if receipt.get(field) != expected:
            errors.append(f"{label} receipt {field} differs from {expected!r}")
    if receipt.get("sealed") is not True:
        errors.append(f"{label} receipt is not sealed")
    if not isinstance(resolver, dict) or resolver.get("type") != EXACT_RESOLVER_TYPE:
        errors.append(f"{label} receipt resolver is not external-protected")
    contract_bindings = receipt.get("subject_contract_sha256")
    if not isinstance(contract_bindings, dict) or contract_bindings.get(
        "WP-0002"
    ) != packet_contract:
        errors.append(f"{label} receipt binds the wrong WP-0002 contract")
    if exact_subject_ids is not None and receipt.get("subject_ids") != list(
        exact_subject_ids
    ):
        errors.append(f"{label} receipt subject_ids differ from {list(exact_subject_ids)!r}")
    claim_subjects = (
        (claim_subject,) if isinstance(claim_subject, str) else claim_subject
    )
    receipt_subjects = receipt.get("subject_ids", [])
    claims_by_subject = _subject_claims(receipt)
    for subject_id in claim_subjects:
        if subject_id not in receipt_subjects:
            errors.append(f"{label} receipt does not name claim subject {subject_id}")
        claims = claims_by_subject.get(subject_id, set())
        if allowed_claims is not None:
            matching_claims = claims.intersection(allowed_claims)
            if len(matching_claims) != 1:
                errors.append(
                    f"{label} receipt must bind exactly one allowed decision claim "
                    f"on {subject_id}, found {sorted(matching_claims)}"
                )
        elif required_claims is not None and not required_claims.issubset(claims):
            errors.append(
                f"{label} receipt lacks {sorted(required_claims - claims)} "
                f"on {subject_id}"
            )
    return errors


def validate_entry_gate(
    packet: dict,
    state: dict,
    receipts: dict[str, dict],
    required_claim: str,
    decision_records: list[dict] | None = None,
) -> list[str]:
    errors: list[str] = []
    if packet.get("id") != "WP-0002" or packet.get("status") not in ACTIVE_STATUSES:
        errors.append("WP-0002 must be active, verifying, or candidate")
    contract = packet.get("contract_sha256")
    if not isinstance(contract, str) or len(contract) != 64:
        errors.append("WP-0002 contract hash is invalid")
        contract = ""
    gate = state.get("entry_gates", {}).get("ugly_gameplay_toy")
    if not isinstance(gate, dict) or gate.get("status") != "passed":
        return errors + ["ugly_gameplay_toy gate is not passed"]
    errors.extend(_exact_gate_errors(gate, contract))
    decision_heads, decision_errors = _active_decision_heads(decision_records or [])
    errors.extend(decision_errors)
    decisions = gate.get("decision_requirements", [])
    for requirement, expected in zip(decisions, EXACT_DECISION_REQUIREMENTS):
        decision_id, accepted_statuses, allowed_claims, _mismatch_action = expected
        if not isinstance(requirement, dict):
            continue
        head = decision_heads.get(decision_id)
        if head is None:
            continue
        head_id = head.get("id")
        if head.get("status") not in accepted_statuses:
            errors.append(
                f"{decision_id} active head {head_id} status is not in {list(accepted_statuses)!r}"
            )
        receipt_id = requirement.get("receipt_id")
        if not isinstance(receipt_id, str):
            errors.append(f"{decision_id} receipt remains null")
            continue
        if head.get("approval_receipt_id") != receipt_id:
            errors.append(
                f"{decision_id} active head {head_id} approval receipt "
                f"{head.get('approval_receipt_id')!r} differs from {receipt_id!r}"
            )
        receipt = receipts.get(receipt_id)
        errors.extend(
            _validate_receipt(
                decision_id,
                receipt,
                contract,
                receipt_kind="decision-ratification",
                claim_subject=str(head_id),
                allowed_claims=set(allowed_claims),
            )
        )
        event_bindings = receipt.get("subject_event_sha256") if receipt else None
        if not isinstance(event_bindings, dict) or event_bindings.get(
            head_id
        ) != head.get("event_hash"):
            errors.append(
                f"{decision_id} receipt does not bind active head {head_id} event hash"
            )
    receipt_requirements = gate.get("receipt_requirements", [])
    materialized_ids: list[str] = []
    for requirement, expected in zip(receipt_requirements, EXACT_RECEIPT_REQUIREMENTS):
        purpose, subject_ids, required_claims, receipt_kind = expected
        if not isinstance(requirement, dict):
            continue
        receipt_id = requirement.get("receipt_id")
        if not isinstance(receipt_id, str):
            errors.append(f"{purpose} receipt remains null")
            continue
        materialized_ids.append(receipt_id)
        errors.extend(
            _validate_receipt(
                purpose,
                receipts.get(receipt_id),
                contract,
                receipt_kind=receipt_kind,
                claim_subject=subject_ids,
                required_claims=set(required_claims),
                exact_subject_ids=subject_ids,
            )
        )
    if required_claim != REQUIRED_CLI_CLAIM:
        errors.append(f"required claim must equal {REQUIRED_CLI_CLAIM}")
    if len(materialized_ids) != len(set(materialized_ids)):
        errors.append("acceptance, authorization, and activation receipts are not distinct")
    decision_receipt_ids = {
        requirement.get("decision_id"): requirement.get("receipt_id")
        for requirement in decisions
        if isinstance(requirement, dict)
    }
    if (
        isinstance(decision_receipt_ids.get("D-0036"), str)
        and decision_receipt_ids.get("D-0036") == decision_receipt_ids.get("D-0037")
    ):
        errors.append("D-0036 and D-0037 must resolve through distinct receipts")
    return errors


def _write_report(repo_root: Path, relative: str, payload: dict[str, object]) -> None:
    repo_lexical = Path(os.path.abspath(os.fspath(repo_root)))
    allowed_lexical = repo_lexical / "BuildArtifacts" / "WP-0002"
    destination_lexical = Path(
        os.path.abspath(os.path.join(os.fspath(repo_lexical), relative))
    )
    try:
        allowed_lexical.relative_to(repo_lexical)
        relative_destination = destination_lexical.relative_to(repo_lexical)
        destination_lexical.relative_to(allowed_lexical)
    except ValueError as exc:
        raise ValueError("report must be under BuildArtifacts/WP-0002/") from exc
    if Path(relative).is_absolute() or ".." in Path(relative).parts:
        raise ValueError("report must use a lexical repository-relative path")
    parts = relative_destination.parts
    if len(parts) < 3 or parts[:2] != ("BuildArtifacts", "WP-0002"):
        raise ValueError("report must name a file under BuildArtifacts/WP-0002/")
    repo_stat = os.lstat(repo_lexical)
    if stat.S_ISLNK(repo_stat.st_mode) or not stat.S_ISDIR(repo_stat.st_mode):
        raise ValueError("repository root must be a real directory, not a symlink")
    repo_real = repo_lexical.resolve(strict=True)
    directory_flags = (
        os.O_RDONLY
        | getattr(os, "O_DIRECTORY", 0)
        | getattr(os, "O_NOFOLLOW", 0)
        | getattr(os, "O_CLOEXEC", 0)
    )
    directory_fd = os.open(repo_lexical, directory_flags)
    try:
        for component in parts[:-1]:
            try:
                os.mkdir(component, 0o700, dir_fd=directory_fd)
            except FileExistsError:
                pass
            next_fd = os.open(component, directory_flags, dir_fd=directory_fd)
            if not stat.S_ISDIR(os.fstat(next_fd).st_mode):
                os.close(next_fd)
                raise ValueError(f"report parent component {component!r} is not a directory")
            os.close(directory_fd)
            directory_fd = next_fd
        allowed_real = allowed_lexical.resolve(strict=True)
        try:
            allowed_real.relative_to(repo_real)
        except ValueError as exc:
            raise ValueError("real report root escapes the repository") from exc
        destination_name = parts[-1]
        try:
            destination_stat = os.stat(
                destination_name, dir_fd=directory_fd, follow_symlinks=False
            )
        except FileNotFoundError:
            pass
        else:
            if not stat.S_ISREG(destination_stat.st_mode):
                raise ValueError("report destination must be a regular file, not a symlink")
        temporary_name = f".{destination_name}.tmp-{os.getpid()}-{secrets.token_hex(8)}"
        temporary_fd = -1
        try:
            temporary_fd = os.open(
                temporary_name,
                os.O_WRONLY
                | os.O_CREAT
                | os.O_EXCL
                | getattr(os, "O_NOFOLLOW", 0)
                | getattr(os, "O_CLOEXEC", 0),
                0o600,
                dir_fd=directory_fd,
            )
            os.fchmod(temporary_fd, 0o600)
            encoded = (json.dumps(payload, indent=2, sort_keys=True) + "\n").encode(
                "utf-8"
            )
            offset = 0
            while offset < len(encoded):
                offset += os.write(temporary_fd, encoded[offset:])
            os.fsync(temporary_fd)
            os.close(temporary_fd)
            temporary_fd = -1
            os.replace(
                temporary_name,
                destination_name,
                src_dir_fd=directory_fd,
                dst_dir_fd=directory_fd,
            )
            os.fsync(directory_fd)
        finally:
            if temporary_fd >= 0:
                os.close(temporary_fd)
            try:
                os.unlink(temporary_name, dir_fd=directory_fd)
            except FileNotFoundError:
                pass
    finally:
        os.close(directory_fd)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packet", required=True)
    parser.add_argument("--ratification-state", required=True)
    parser.add_argument("--require-claim", required=True)
    parser.add_argument("--report", required=True)
    args = parser.parse_args()
    repo_root = Path(__file__).resolve().parents[2]
    errors: list[str] = []
    if args.packet != PACKET_PATH:
        errors.append(f"packet path must equal {PACKET_PATH}")
    if args.ratification_state != STATE_PATH:
        errors.append(f"ratification-state path must equal {STATE_PATH}")
    packet: dict = {}
    state: dict = {}
    receipts: dict[str, dict] = {}
    decision_records: list[dict] = []
    if not errors:
        try:
            packet = _load_json(repo_root / PACKET_PATH)  # type: ignore[assignment]
            state = _load_json(repo_root / STATE_PATH)  # type: ignore[assignment]
            decision_records = _load_decisions(repo_root / DECISIONS_PATH)
            for path in sorted((repo_root / RECEIPT_ROOT).glob("*.json")):
                receipt = _load_json(path)
                if isinstance(receipt, dict) and isinstance(receipt.get("receipt_id"), str):
                    receipts[receipt["receipt_id"]] = receipt
        except (OSError, ValueError, json.JSONDecodeError) as exc:
            errors.append(str(exc))
    if not errors:
        errors.extend(
            validate_entry_gate(
                packet,
                state,
                receipts,
                args.require_claim,
                decision_records,
            )
        )
    payload = {
        "schema_version": 1,
        "checker_contract": ENTRY_GATE_CONTRACT_VERSION,
        "result": "pass" if not errors else "fail",
        "errors": errors,
        "packet_sha256": hashlib.sha256(
            (repo_root / PACKET_PATH).read_bytes()
        ).hexdigest()
        if (repo_root / PACKET_PATH).is_file()
        else None,
    }
    try:
        _write_report(repo_root, args.report, payload)
    except (OSError, ValueError) as exc:
        print(f"WP-0002 ENTRY GATE: FAIL: {exc}", file=sys.stderr)
        return 2
    if errors:
        print("WP-0002 ENTRY GATE: FAIL", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print("WP-0002 ENTRY GATE: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
