#!/usr/bin/env python3
"""Validate the foundation pack without third-party dependencies."""

from __future__ import annotations

import json
import hashlib
import re
import subprocess
import sys
from collections import Counter
from datetime import date, datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
DECISIONS = ROOT / "ledger" / "decisions.jsonl"
DECISION_SCHEMA = ROOT / "schemas" / "decision.schema.json"
SCENARIO_SCHEMA = ROOT / "schemas" / "scenario-definition.schema.json"
SCENARIO_REGISTRY = ROOT / "scenarios" / "registry.json"
SCENARIO_DEFINITIONS = ROOT / "scenarios" / "definitions"
SCENARIO_FIXTURES = ROOT / "scenarios" / "fixtures"
SCENARIO_ARTIFACTS = ROOT / "scenarios" / "artifacts"
SCENARIO_FIXTURE_SCHEMA = ROOT / "schemas" / "scenario-fixture-manifest.schema.json"
SCENARIO_ARTIFACT_SCHEMA = ROOT / "schemas" / "scenario-artifact.schema.json"
A1_BOUNDARY_SCHEMA = ROOT / "schemas" / "a1-boundary-manifest.schema.json"

PACKET_CONTRACT_FIELDS = (
    "schema_version", "id", "class", "declared_risk", "save_risk", "created_on",
    "title", "objective", "value", "requested_by", "required_approver",
    "constitutional_links", "decision_links", "system_contracts", "baseline_evidence",
    "in_scope", "non_goals", "affected_domains", "interfaces", "dependencies",
    "declared_paths", "scenario_pins", "save_impact", "acceptance_tests",
    "performance_metrics", "visual_evidence", "rollout", "rollback",
)

PACKET_STATUSES = {
    "proposed", "accepted", "active", "verifying", "candidate", "released",
    "rejected", "rolled-back", "superseded",
}
PACKET_TRANSITIONS = {
    "proposed": {"accepted", "rejected", "superseded"},
    "accepted": {"active", "rejected", "superseded"},
    "active": {"verifying", "rolled-back", "rejected"},
    "verifying": {"active", "candidate", "rolled-back", "rejected"},
    "candidate": {"verifying", "released", "rolled-back", "rejected"},
    "released": {"rolled-back", "superseded"},
    "rejected": set(),
    "rolled-back": set(),
    "superseded": set(),
}

SCENARIO_COUNT_KEYS = {
    "state": {"authoritative_records", "save_sections", "content_definitions", "transactions", "save_generations"},
    "entities": {"total", "settlements", "buildings", "households", "specialists", "robots", "vehicles", "factions", "crises", "expeditions"},
    "objects": {"placed_total", "dynamic", "static", "lod0", "lod1", "lod2"},
    "lights": {"total", "directional", "point", "spot", "shadowed"},
    "crowd": {"authoritative_people", "visible_agents", "offscreen_people"},
    "inputs": {"scripted_commands", "keyboard_events", "controller_events", "pointer_events", "fault_injections"},
}

ENTITY_COUNT_TO_TYPE = {
    "settlements": "settlement",
    "buildings": "building",
    "households": "household",
    "specialists": "specialist",
    "robots": "robot",
    "vehicles": "vehicle",
    "factions": "faction",
    "crises": "crisis",
    "expeditions": "expedition",
}
ENTITY_TYPES = set(ENTITY_COUNT_TO_TYPE.values())
STATE_DOMAINS = {
    "world", "settlement", "population", "logistics", "vehicle",
    "expedition", "faction", "crisis", "audit", "save",
}
CONTENT_KINDS = {
    "resource", "recipe", "building", "capability", "route",
    "faction-rule", "save-section", "presentation-binding",
}
EVENT_KINDS = {
    "scripted_commands", "keyboard_events", "controller_events",
    "pointer_events", "fault_injections",
}
ORACLE_OPERATORS = {
    "equal", "less-than", "less-or-equal", "greater-or-equal",
    "matches-reference",
}
SCENARIO_SELECTOR_VALUES = {
    "scenario-root": None,
    "save-subsystem": "save",
    "clock-subsystem": "clock",
    "oracle-surface": "oracles",
}
TARGET_RESOLUTION_RULE = (
    "resolve every scripted command payload.target by exact logical_target_id before tick zero; "
    "canonical bindings select resolved_id in the base run and the matching case_resolutions entry "
    "in a case run; scenario selectors resolve only by their declared selector_kind and selector_value"
)
UNRESOLVED_TARGET_RULE = (
    "reject the scenario before tick zero on a missing or duplicate binding, an unreferenced binding, "
    "an unknown selector, a missing case resolution, a kind mismatch, or an ID absent from the "
    "hash-bound starting-state or content-set artifact; never infer, fabricate, or fall back"
)
TARGET_BINDING_HASH_RULE = (
    "target_bindings_sha256 is SHA-256 of canonical JSON for the complete ordered target_bindings array"
)

CREATOR_AUTHORITIES = {
    "creator-prompt",
    "creator-clarification",
    "creator-ratification",
}


def fail(message: str) -> None:
    raise ValueError(message)


def load_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        fail(f"Invalid JSON in {path.relative_to(ROOT)}: {exc}")


def validate_decisions() -> tuple[list[dict], list[str]]:
    schema = load_json(DECISION_SCHEMA)
    required = set(schema["required"])
    allowed = set(schema["properties"])
    enum_fields = {
        key: set(value["enum"])
        for key, value in schema["properties"].items()
        if "enum" in value
    }

    records: list[dict] = []
    errors: list[str] = []
    for line_no, raw in enumerate(DECISIONS.read_text(encoding="utf-8").splitlines(), 1):
        if not raw.strip():
            errors.append(f"decisions.jsonl:{line_no}: blank lines are not allowed")
            continue
        try:
            record = json.loads(raw)
        except json.JSONDecodeError as exc:
            errors.append(f"decisions.jsonl:{line_no}: {exc}")
            continue
        if not isinstance(record, dict):
            errors.append(f"decisions.jsonl:{line_no}: record is not an object")
            continue
        missing = required - set(record)
        extra = set(record) - allowed
        if missing:
            errors.append(f"{record.get('id', line_no)} missing: {sorted(missing)}")
        if extra:
            errors.append(f"{record.get('id', line_no)} extra fields: {sorted(extra)}")
        for field, choices in enum_fields.items():
            if record.get(field) not in choices:
                errors.append(f"{record.get('id', line_no)} invalid {field}: {record.get(field)!r}")
        if not re.fullmatch(r"D-\d{4}", str(record.get("id", ""))):
            errors.append(f"line {line_no} has invalid ID: {record.get('id')!r}")
        try:
            date.fromisoformat(record.get("recorded_on", ""))
        except ValueError:
            errors.append(f"{record.get('id', line_no)} has invalid recorded_on")
        if not isinstance(record.get("consequences"), list) or not record.get("consequences"):
            errors.append(f"{record.get('id', line_no)} needs at least one consequence")
        if not isinstance(record.get("revisit_triggers"), list):
            errors.append(f"{record.get('id', line_no)} revisit_triggers must be an array")
        if record.get("status") == "ratified" and record.get("authority") not in {
            "creator-prompt",
            "creator-clarification",
            "creator-ratification",
        }:
            errors.append(f"{record.get('id', line_no)} is ratified without creator authority")
        if record.get("status") == "rejected" and record.get("authority") not in {
            "creator-prompt",
            "creator-clarification",
            "creator-ratification",
        }:
            errors.append(f"{record.get('id', line_no)} is rejected without creator authority")
        if record.get("status") == "open" and not record.get("recommended_default"):
            errors.append(f"{record.get('id', line_no)} is open without a recommended default")
        records.append(record)

    ids = [record.get("id") for record in records]
    if len(ids) != len(set(ids)):
        errors.append("decision IDs are not unique")
    expected = [f"D-{number:04d}" for number in range(1, len(records) + 1)]
    if ids != expected:
        errors.append(f"decision IDs are not monotonic and gap-free: expected {expected}, got {ids}")
    known: set[str] = set()
    previous_hash: str | None = None
    for record in records:
        if record.get("sequence") != len(known) + 1:
            errors.append(f"{record['id']} has incorrect sequence {record.get('sequence')}")
        if record.get("previous_event_hash") != previous_hash:
            errors.append(f"{record['id']} has a broken previous_event_hash chain")
        canonical = {
            key: value for key, value in record.items() if key != "event_hash"
        }
        calculated_hash = hashlib.sha256(
            json.dumps(
                canonical,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=False,
            ).encode("utf-8")
        ).hexdigest()
        if record.get("event_hash") != calculated_hash:
            errors.append(f"{record['id']} event_hash does not match canonical record")
        previous_hash = record.get("event_hash")
        supersedes = record.get("supersedes")
        if supersedes is not None and supersedes not in known:
            errors.append(f"{record['id']} supersedes unknown or later record {supersedes}")
        known.add(record["id"])

    human_ledger = (ROOT / "01-DECISION-LEDGER.md").read_text(encoding="utf-8")
    table_rows = re.findall(
        r"^\| (D-\d{4}) \| ([^|]+?) \| ([^|]+?) \|",
        human_ledger,
        flags=re.MULTILINE,
    )
    table_ids = {row[0] for row in table_rows}
    if table_ids != set(ids):
        errors.append(
            "human ledger and JSONL differ: "
            f"missing from table={sorted(set(ids) - table_ids)}, "
            f"missing from JSONL={sorted(table_ids - set(ids))}"
        )
    table_by_id = {row[0]: (row[1].strip().lower(), row[2].strip().lower()) for row in table_rows}
    for record in records:
        table_values = table_by_id.get(record["id"])
        if table_values and table_values != (record["class"], record["status"]):
            errors.append(
                f"{record['id']} human/JSONL class-status mismatch: "
                f"table={table_values}, JSONL={(record['class'], record['status'])}"
            )
    return records, errors


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_canonical_json(value: object) -> str:
    """Hash the foundation's explicit canonical JSON domain."""
    return hashlib.sha256(
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=False,
        ).encode("utf-8")
    ).hexdigest()


def safe_foundation_path(relative: object, label: str) -> tuple[Path | None, str | None]:
    """Resolve a foundation-relative path without accepting escape or ambiguity."""
    if not isinstance(relative, str) or not relative or "\\" in relative:
        return None, f"{label} must be a non-empty POSIX foundation-relative path"
    candidate = Path(relative)
    if candidate.is_absolute() or any(part in {"", ".", ".."} for part in candidate.parts):
        return None, f"{label} is not a safe foundation-relative path: {relative!r}"
    resolved = (ROOT / candidate).resolve()
    try:
        resolved.relative_to(ROOT.resolve())
    except ValueError:
        return None, f"{label} escapes the foundation: {relative!r}"
    return resolved, None


def git_commit_exists(commit: str) -> bool:
    result = subprocess.run(
        ["git", "-C", str(REPO_ROOT), "cat-file", "-e", f"{commit}^{{commit}}"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    return result.returncode == 0


def git_foundation_blob(commit: str, relative: str) -> bytes | None:
    repo_relative = (Path("docs") / ROOT.name / Path(relative)).as_posix()
    result = subprocess.run(
        ["git", "-C", str(REPO_ROOT), "show", f"{commit}:{repo_relative}"],
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    return result.stdout if result.returncode == 0 else None


def validate_receipts(records: list[dict]) -> tuple[list[dict], list[str]]:
    errors: list[str] = []
    receipts: list[dict] = []
    receipt_dir = ROOT / "ledger" / "receipts"
    for path in sorted(receipt_dir.glob("*.json")):
        errors.extend(validate_instance_shape(path, ROOT / "schemas" / "ratification-receipt.schema.json"))
        receipt = load_json(path)
        receipts.append(receipt)
        subject_claim_bindings = receipt.get("subject_claims", [])
        if not isinstance(subject_claim_bindings, list):
            subject_claim_bindings = []
            errors.append(f"{path.relative_to(ROOT)} subject_claims must be an array")
        if any(not isinstance(binding, dict) for binding in subject_claim_bindings):
            errors.append(f"{path.relative_to(ROOT)} subject_claims entries must be objects")
        bound_subjects = [binding.get("subject_id") for binding in subject_claim_bindings if isinstance(binding, dict)]
        if len(bound_subjects) != len(set(bound_subjects)):
            errors.append(f"{path.relative_to(ROOT)} repeats a subject_claims binding")
        if set(bound_subjects) != set(receipt.get("subject_ids", [])):
            errors.append(f"{path.relative_to(ROOT)} subject_ids and subject_claims differ")
        for binding in subject_claim_bindings:
            if not isinstance(binding, dict):
                continue
            claims = binding.get("claims")
            if not isinstance(binding.get("subject_id"), str) or not binding.get("subject_id"):
                errors.append(f"{path.relative_to(ROOT)} has an invalid subject_claims subject_id")
            if not isinstance(claims, list) or not claims or len(claims) != len(set(claims)):
                errors.append(f"{path.relative_to(ROOT)} has invalid or duplicate subject claims")
            elif any(not isinstance(claim, str) or not re.fullmatch(r"[A-Z0-9+_-]+", claim) for claim in claims):
                errors.append(f"{path.relative_to(ROOT)} has a malformed subject claim")
        resolver = receipt.get("artifact_resolver", {})
        resolver_type = resolver.get("type") if isinstance(resolver, dict) else None
        source_reference = receipt.get("source_reference")
        artifact_hashes = receipt.get("artifact_sha256", {})
        if resolver_type == "local-git-tree":
            source_path, source_error = safe_foundation_path(
                source_reference, f"{path.relative_to(ROOT)} source_reference"
            )
            if source_error:
                errors.append(source_error)
            safe_artifacts: list[tuple[str, Path, str]] = []
            if isinstance(artifact_hashes, dict):
                for relative, expected_hash in artifact_hashes.items():
                    artifact, artifact_error = safe_foundation_path(
                        relative, f"{path.relative_to(ROOT)} artifact_sha256 key"
                    )
                    if artifact_error:
                        errors.append(artifact_error)
                    elif artifact is not None:
                        safe_artifacts.append((relative, artifact, expected_hash))

            if receipt.get("sealed"):
                commit = receipt.get("accepted_commit")
                if not isinstance(commit, str) or not git_commit_exists(commit):
                    errors.append(
                        f"{path.relative_to(ROOT)} accepted local Git commit does not exist"
                    )
                else:
                    if source_error is None and isinstance(source_reference, str):
                        source_blob = git_foundation_blob(commit, source_reference)
                        if source_blob is None:
                            errors.append(
                                f"{path.relative_to(ROOT)} source_reference is absent from accepted commit"
                            )
                        elif hashlib.sha256(source_blob).hexdigest() != receipt.get("approval_text_sha256"):
                            errors.append(
                                f"{path.relative_to(ROOT)} approval source hash differs from accepted tree"
                            )
                    for relative, _, expected_hash in safe_artifacts:
                        blob = git_foundation_blob(commit, relative)
                        if blob is None:
                            errors.append(
                                f"{path.relative_to(ROOT)} artifact is absent from accepted tree: {relative}"
                            )
                        elif hashlib.sha256(blob).hexdigest() != expected_hash:
                            errors.append(
                                f"{path.relative_to(ROOT)} artifact hash differs from accepted tree: {relative}"
                            )
            else:
                if source_path is not None:
                    if not source_path.is_file():
                        errors.append(f"{path.relative_to(ROOT)} source_reference is missing")
                    elif sha256_file(source_path) != receipt.get("approval_text_sha256"):
                        errors.append(f"{path.relative_to(ROOT)} approval source hash mismatch")
                for relative, artifact, expected_hash in safe_artifacts:
                    if not artifact.is_file():
                        errors.append(f"{path.relative_to(ROOT)} artifact is missing: {relative}")
                    elif sha256_file(artifact) != expected_hash:
                        errors.append(f"{path.relative_to(ROOT)} artifact hash mismatch: {relative}")
        elif resolver_type == "external-protected":
            if receipt.get("sealed") and not (
                isinstance(resolver.get("resolver_reference"), str)
                and resolver.get("resolver_reference")
                and receipt.get("signature_reference")
            ):
                errors.append(
                    f"{path.relative_to(ROOT)} external protected receipt lacks resolver/signature authority"
                )
        else:
            errors.append(f"{path.relative_to(ROOT)} uses an unknown artifact resolver")

        if receipt.get("sealed") and not receipt.get("signature_reference"):
            errors.append(f"{path.relative_to(ROOT)} is sealed without a signature reference")

    by_id = {receipt["receipt_id"]: receipt for receipt in receipts}
    if len(by_id) != len(receipts):
        errors.append("ratification receipt IDs are not unique")
    records_by_id = {record["id"]: record for record in records}
    for receipt in receipts:
        event_bindings = receipt.get("subject_event_sha256", {})
        if not isinstance(event_bindings, dict):
            continue
        expected_decision_subjects = {
            subject_id
            for subject_id in receipt.get("subject_ids", [])
            if subject_id in records_by_id
        }
        if set(event_bindings) != expected_decision_subjects:
            errors.append(
                f"receipt {receipt.get('receipt_id')} decision event bindings differ from its decision subjects"
            )
        for decision_id in expected_decision_subjects:
            if event_bindings.get(decision_id) != records_by_id[decision_id].get("event_hash"):
                errors.append(
                    f"receipt {receipt.get('receipt_id')} does not bind exact event hash for {decision_id}"
                )
    for record in records:
        if record.get("status") != "ratified":
            continue
        receipt_id = record.get("approval_receipt_id")
        receipt = by_id.get(receipt_id)
        if receipt is None:
            errors.append(f"{record['id']} ratified without a known receipt")
        elif record["id"] not in receipt.get("subject_ids", []):
            errors.append(f"{record['id']} is not named by receipt {receipt_id}")
        elif receipt.get("subject_event_sha256", {}).get(record["id"]) != record.get("event_hash"):
            errors.append(f"{record['id']} receipt does not bind its exact event hash")
        elif receipt.get("issuer_role") != "creator":
            errors.append(f"{record['id']} is ratified without a creator-issued receipt")
    return receipts, errors


def validate_local_links() -> list[str]:
    errors: list[str] = []
    link_pattern = re.compile(r"\[[^\]]*\]\(([^)]+)\)")
    for markdown in ROOT.rglob("*.md"):
        text = markdown.read_text(encoding="utf-8")
        for target in link_pattern.findall(text):
            if target.startswith(("http://", "https://", "#", "mailto:")):
                continue
            path_part = target.split("#", 1)[0]
            if not path_part:
                continue
            resolved = (markdown.parent / path_part).resolve()
            try:
                resolved.relative_to(ROOT)
            except ValueError:
                errors.append(f"{markdown.relative_to(ROOT)} link escapes foundation: {target}")
                continue
            if not resolved.exists():
                errors.append(f"{markdown.relative_to(ROOT)} has missing local link: {target}")
    return errors


def gfm_heading_anchors(path: Path) -> set[str]:
    """Return GitHub-style heading anchors, including duplicate suffixes."""
    anchors: set[str] = set()
    occurrences: Counter[str] = Counter()
    in_fence = False
    fence_marker = ""
    for line in path.read_text(encoding="utf-8").splitlines():
        fence = re.match(r"^\s*(```|~~~)", line)
        if fence:
            marker = fence.group(1)
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = ""
            continue
        if in_fence:
            continue
        match = re.match(r"^\s{0,3}#{1,6}\s+(.+?)\s*$", line)
        if not match:
            continue
        heading = re.sub(r"\s+#+\s*$", "", match.group(1))
        heading = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", heading)
        heading = re.sub(r"<[^>]+>", "", heading)
        base = re.sub(r"[^\w\- ]", "", heading.lower())
        base = re.sub(r"\s", "-", base)
        suffix = occurrences[base]
        occurrences[base] += 1
        anchors.add(base if suffix == 0 else f"{base}-{suffix}")
    return anchors


def validate_packet_system_contracts(path: Path, packet: dict) -> list[str]:
    """Validate Markdown paths and anchors embedded as JSON strings."""
    errors: list[str] = []
    contracts = packet.get("system_contracts")
    if not isinstance(contracts, list) or not contracts:
        return [f"{path.relative_to(ROOT)} system_contracts must be a non-empty array"]
    if len(contracts) != len(set(item for item in contracts if isinstance(item, str))):
        errors.append(f"{path.relative_to(ROOT)} system_contracts repeats a reference")
    for index, target in enumerate(contracts):
        label = f"{path.relative_to(ROOT)} system_contracts[{index}]"
        if not isinstance(target, str) or not target:
            errors.append(f"{label} must be a non-empty Markdown reference")
            continue
        file_part, separator, anchor = target.partition("#")
        if not file_part or file_part.startswith("/") or ".." in Path(file_part).parts:
            errors.append(f"{label} has an unsafe file path: {target!r}")
            continue
        if Path(file_part).suffix.lower() != ".md":
            errors.append(f"{label} must target a Markdown file: {target!r}")
        resolved = (ROOT / file_part).resolve()
        try:
            resolved.relative_to(ROOT.resolve())
        except ValueError:
            errors.append(f"{label} escapes the foundation: {target!r}")
            continue
        if not resolved.is_file():
            errors.append(f"{label} targets a missing file: {target!r}")
            continue
        if separator:
            if not anchor or "#" in anchor:
                errors.append(f"{label} has a malformed anchor: {target!r}")
            elif anchor not in gfm_heading_anchors(resolved):
                errors.append(f"{label} targets a missing GFM anchor: {target!r}")
    return errors


def validate_references(records: list[dict]) -> list[str]:
    errors: list[str] = []
    known = {record["id"] for record in records}
    for path in ROOT.rglob("*.md"):
        for decision_id in set(re.findall(r"D-\d{4}", path.read_text(encoding="utf-8"))):
            if decision_id not in known:
                errors.append(f"{path.relative_to(ROOT)} references unknown {decision_id}")
    return errors


def validate_markdown_scenario_references(scenarios: dict[str, dict]) -> list[str]:
    errors: list[str] = []
    fenced_code = re.compile(r"(^|\n)(```|~~~).*?\n\2(?:\n|$)", flags=re.DOTALL)
    for path in ROOT.rglob("*.md"):
        text = fenced_code.sub("\n", path.read_text(encoding="utf-8"))
        for scenario_id in sorted(set(re.findall(r"SCN_[A-Z0-9_]+", text))):
            if scenario_id not in scenarios:
                errors.append(f"{path.relative_to(ROOT)} references unregistered scenario {scenario_id}")
    return errors


def _json_equal(left: object, right: object) -> bool:
    """JSON equality that does not confuse booleans with numbers."""
    if isinstance(left, bool) or isinstance(right, bool):
        return type(left) is type(right) and left == right
    if isinstance(left, (int, float)) and isinstance(right, (int, float)):
        return left == right
    if isinstance(left, list) and isinstance(right, list):
        return len(left) == len(right) and all(
            _json_equal(left_item, right_item)
            for left_item, right_item in zip(left, right)
        )
    if isinstance(left, dict) and isinstance(right, dict):
        return set(left) == set(right) and all(
            _json_equal(left[key], right[key]) for key in left
        )
    return type(left) is type(right) and left == right


def _json_type_matches(value: object, expected: str) -> bool:
    checks = {
        "null": lambda item: item is None,
        "boolean": lambda item: isinstance(item, bool),
        "object": lambda item: isinstance(item, dict),
        "array": lambda item: isinstance(item, list),
        "string": lambda item: isinstance(item, str),
        "integer": lambda item: isinstance(item, int) and not isinstance(item, bool),
        "number": lambda item: isinstance(item, (int, float)) and not isinstance(item, bool),
    }
    return expected in checks and checks[expected](value)


def _resolve_local_ref(root_schema: dict, reference: str) -> object:
    if not reference.startswith("#/"):
        raise ValueError(f"unsupported non-local schema reference {reference!r}")
    current: object = root_schema
    for raw_part in reference[2:].split("/"):
        part = raw_part.replace("~1", "/").replace("~0", "~")
        if not isinstance(current, dict) or part not in current:
            raise ValueError(f"unresolved schema reference {reference!r}")
        current = current[part]
    return current


def _format_matches(value: str, format_name: str) -> bool:
    if format_name == "date":
        try:
            return date.fromisoformat(value).isoformat() == value
        except ValueError:
            return False
    if format_name == "date-time":
        return parse_datetime(value) is not None and ("T" in value or "t" in value)
    return True


def validate_schema_subset(
    instance: object,
    schema: object,
    root_schema: dict,
    label: str,
) -> list[str]:
    """Recursively assert the exact JSON Schema subset used by this repository.

    This is dependency-free bootstrap lint, not the pinned full Draft 2020-12
    validator required for the protected gatekeeper.
    """
    if isinstance(schema, bool):
        return [] if schema else [f"{label} is rejected by a false schema"]
    if not isinstance(schema, dict):
        return [f"{label} has an invalid local schema node"]
    if "$ref" in schema:
        try:
            target = _resolve_local_ref(root_schema, schema["$ref"])
        except ValueError as exc:
            return [f"{label}: {exc}"]
        ref_errors = validate_schema_subset(instance, target, root_schema, label)
        siblings = {key: value for key, value in schema.items() if key != "$ref"}
        if siblings:
            ref_errors.extend(validate_schema_subset(instance, siblings, root_schema, label))
        return ref_errors

    errors: list[str] = []
    if "allOf" in schema:
        for index, branch in enumerate(schema["allOf"]):
            errors.extend(validate_schema_subset(instance, branch, root_schema, f"{label}.allOf[{index}]"))
    if "oneOf" in schema:
        branch_results = [
            validate_schema_subset(instance, branch, root_schema, label)
            for branch in schema["oneOf"]
        ]
        matches = sum(not branch_errors for branch_errors in branch_results)
        if matches != 1:
            errors.append(f"{label} must match exactly one oneOf branch; matched {matches}")
    if "if" in schema:
        condition_matches = not validate_schema_subset(instance, schema["if"], root_schema, label)
        branch_name = "then" if condition_matches else "else"
        if branch_name in schema:
            errors.extend(validate_schema_subset(instance, schema[branch_name], root_schema, label))

    if "const" in schema and not _json_equal(instance, schema["const"]):
        errors.append(f"{label} must equal {schema['const']!r}")
    if "enum" in schema and not any(_json_equal(instance, choice) for choice in schema["enum"]):
        errors.append(f"{label} is not in the allowed enum")

    expected_types = schema.get("type")
    if isinstance(expected_types, str):
        expected_types = [expected_types]
    if isinstance(expected_types, list) and not any(
        isinstance(item, str) and _json_type_matches(instance, item)
        for item in expected_types
    ):
        errors.append(f"{label} has the wrong JSON type; expected {expected_types}")
        return errors

    if isinstance(instance, str):
        if len(instance) < schema.get("minLength", 0):
            errors.append(f"{label} is shorter than minLength")
        if "maxLength" in schema and len(instance) > schema["maxLength"]:
            errors.append(f"{label} is longer than maxLength")
        if "pattern" in schema and re.search(schema["pattern"], instance) is None:
            errors.append(f"{label} fails pattern {schema['pattern']!r}")
        if "format" in schema and not _format_matches(instance, schema["format"]):
            errors.append(f"{label} fails format {schema['format']!r}")

    if isinstance(instance, (int, float)) and not isinstance(instance, bool):
        if "minimum" in schema and instance < schema["minimum"]:
            errors.append(f"{label} is below minimum")
        if "maximum" in schema and instance > schema["maximum"]:
            errors.append(f"{label} is above maximum")

    if isinstance(instance, list):
        if len(instance) < schema.get("minItems", 0):
            errors.append(f"{label} has fewer than minItems")
        if "maxItems" in schema and len(instance) > schema["maxItems"]:
            errors.append(f"{label} has more than maxItems")
        if schema.get("uniqueItems"):
            if any(
                _json_equal(instance[left], instance[right])
                for left in range(len(instance))
                for right in range(left + 1, len(instance))
            ):
                errors.append(f"{label} violates uniqueItems")
        if "items" in schema:
            for index, item in enumerate(instance):
                errors.extend(validate_schema_subset(item, schema["items"], root_schema, f"{label}[{index}]"))

    if isinstance(instance, dict):
        if len(instance) < schema.get("minProperties", 0):
            errors.append(f"{label} has fewer than minProperties")
        properties = schema.get("properties", {})
        required = schema.get("required", [])
        for key in required:
            if key not in instance:
                errors.append(f"{label} is missing required property {key!r}")
        if "propertyNames" in schema:
            for key in instance:
                errors.extend(validate_schema_subset(key, schema["propertyNames"], root_schema, f"{label}.propertyName[{key!r}]"))
        for key, value in instance.items():
            if key in properties:
                errors.extend(validate_schema_subset(value, properties[key], root_schema, f"{label}.{key}"))
                continue
            additional = schema.get("additionalProperties", {})
            if additional is False:
                errors.append(f"{label} has forbidden property {key!r}")
            elif isinstance(additional, (dict, bool)):
                errors.extend(validate_schema_subset(value, additional, root_schema, f"{label}.{key}"))
    return errors


def validate_instance_shape(path: Path, schema_path: Path) -> list[str]:
    """Validate one JSON instance with the repository's recursive subset."""
    instance = load_json(path)
    schema = load_json(schema_path)
    return validate_schema_subset(instance, schema, schema, str(path.relative_to(ROOT)))


def is_nonnegative_int(value: object) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def validate_scenario_definition(path: Path, registered_id: str) -> tuple[dict, list[str]]:
    errors = validate_instance_shape(path, SCENARIO_SCHEMA)
    scenario = load_json(path)
    label = str(path.relative_to(ROOT))
    if not isinstance(scenario, dict):
        return {}, errors + [f"{label} must be an object"]

    scenario_id = scenario.get("id")
    if scenario_id != registered_id:
        errors.append(f"{label} ID {scenario_id!r} does not match registry ID {registered_id!r}")
    if path.stem != registered_id:
        errors.append(f"{label} filename does not match scenario ID {registered_id}")
    if scenario.get("schema_version") != 2:
        errors.append(f"{label} schema_version must be 2")
    if not isinstance(scenario.get("revision"), int) or isinstance(scenario.get("revision"), bool) or scenario["revision"] < 1:
        errors.append(f"{label} revision must be a positive integer")
    if scenario.get("kind") not in {"performance", "save", "migration", "rollback", "gameplay", "aggregate"}:
        errors.append(f"{label} has an invalid kind")
    for field in ("title", "purpose"):
        if not isinstance(scenario.get(field), str) or not scenario[field]:
            errors.append(f"{label} {field} must be a non-empty string")

    packets = scenario.get("applies_to_packets")
    if not isinstance(packets, list) or not packets:
        errors.append(f"{label} applies_to_packets must be a non-empty array")
    elif len(packets) != len(set(packets)) or any(not re.fullmatch(r"WP-\d{4}", str(item)) for item in packets):
        errors.append(f"{label} applies_to_packets contains a duplicate or invalid packet ID")

    fixture = scenario.get("fixture")
    fixture_keys = {
        "world_seed", "starting_tick", "tick_rate_hz", "warmup_ticks", "duration_ticks",
        "warmup_seconds", "duration_seconds", "starting_state_id", "content_set_id", "input_script_id",
        "fixture_manifest",
    }
    if not isinstance(fixture, dict):
        errors.append(f"{label} fixture must be an object")
    else:
        if set(fixture) != fixture_keys:
            errors.append(f"{label} fixture fields differ: expected {sorted(fixture_keys)}, got {sorted(fixture)}")
        for field in ("world_seed", "starting_tick"):
            if not re.fullmatch(r"[0-9]+", str(fixture.get(field, ""))):
                errors.append(f"{label} fixture {field} must be an unsigned decimal string")
        tick_rate = fixture.get("tick_rate_hz")
        if not is_nonnegative_int(tick_rate) or not 1 <= tick_rate <= 120:
            errors.append(f"{label} fixture tick_rate_hz must be an integer from 1 to 120")
        for field in ("warmup_ticks", "duration_ticks", "warmup_seconds", "duration_seconds"):
            if not is_nonnegative_int(fixture.get(field)):
                errors.append(f"{label} fixture {field} must be a non-negative integer")
        if is_nonnegative_int(tick_rate):
            if is_nonnegative_int(fixture.get("warmup_seconds")) and fixture.get("warmup_ticks") != fixture["warmup_seconds"] * tick_rate:
                errors.append(f"{label} warmup_ticks does not equal warmup_seconds * tick_rate_hz")
            if is_nonnegative_int(fixture.get("duration_seconds")) and fixture.get("duration_ticks") != fixture["duration_seconds"] * tick_rate:
                errors.append(f"{label} duration_ticks does not equal duration_seconds * tick_rate_hz")
        fixture_id_pattern = r"[a-z0-9][a-z0-9._-]+"
        for field in ("starting_state_id", "content_set_id", "input_script_id"):
            if not re.fullmatch(fixture_id_pattern, str(fixture.get(field, ""))):
                errors.append(f"{label} fixture {field} is invalid")

        manifest_ref = fixture.get("fixture_manifest")
        if not isinstance(manifest_ref, dict) or set(manifest_ref) != {"path", "sha256"}:
            errors.append(f"{label} fixture_manifest must contain exactly path and sha256")
        else:
            manifest_relative = manifest_ref.get("path")
            manifest_hash = manifest_ref.get("sha256")
            expected_manifest_relative = f"scenarios/fixtures/{scenario_id}.fixture.json"
            if manifest_relative != expected_manifest_relative:
                errors.append(
                    f"{label} fixture_manifest path must be {expected_manifest_relative!r}, "
                    f"got {manifest_relative!r}"
                )
            if not re.fullmatch(r"[0-9a-f]{64}", str(manifest_hash)):
                errors.append(f"{label} fixture_manifest sha256 is invalid")
            if isinstance(manifest_relative, str):
                manifest_path = (ROOT / manifest_relative).resolve()
                try:
                    manifest_path.relative_to(SCENARIO_FIXTURES.resolve())
                except ValueError:
                    errors.append(f"{label} fixture_manifest path escapes scenarios/fixtures")
                else:
                    if not manifest_path.exists() or not manifest_path.is_file():
                        errors.append(f"{label} fixture_manifest is missing: {manifest_relative}")
                    else:
                        actual_manifest_hash = sha256_file(manifest_path)
                        if actual_manifest_hash != manifest_hash:
                            errors.append(
                                f"{label} fixture_manifest hash mismatch: expected {manifest_hash}, "
                                f"got {actual_manifest_hash}"
                            )
                        manifest = load_json(manifest_path)
                        if not isinstance(manifest, dict):
                            errors.append(f"{manifest_relative} must be an object")
                        else:
                            manifest_fields = {
                                "schema_version", "scenario_id", "scenario_revision", "hash_algorithm",
                                "canonicalization", "fixture_contract", "deterministic_contract", "contract_hashes",
                            }
                            if set(manifest) != manifest_fields:
                                errors.append(
                                    f"{manifest_relative} fields differ from the fixture-manifest contract"
                                )
                            if manifest.get("schema_version") != 1:
                                errors.append(f"{manifest_relative} schema_version must be 1")
                            if manifest.get("scenario_id") != scenario_id:
                                errors.append(f"{manifest_relative} scenario_id does not match {scenario_id}")
                            if manifest.get("scenario_revision") != scenario.get("revision"):
                                errors.append(f"{manifest_relative} scenario_revision does not match the scenario")
                            if manifest.get("hash_algorithm") != "sha256":
                                errors.append(f"{manifest_relative} hash_algorithm must be sha256")
                            if manifest.get("canonicalization") != "json-sort-keys-utf8-no-whitespace-v1":
                                errors.append(f"{manifest_relative} has an unsupported canonicalization")
                            expected_fixture_contract = {
                                key: value for key, value in fixture.items() if key != "fixture_manifest"
                            }
                            if manifest.get("fixture_contract") != expected_fixture_contract:
                                errors.append(
                                    f"{manifest_relative} fixture_contract does not exactly bind the scenario fixture"
                                )
                            expected_deterministic_contract = {
                                "counts": scenario.get("counts"),
                                "parameters": scenario.get("parameters"),
                                "oracles": scenario.get("oracles"),
                            }
                            if manifest.get("deterministic_contract") != expected_deterministic_contract:
                                errors.append(
                                    f"{manifest_relative} deterministic_contract does not exactly bind "
                                    "counts, parameters, and oracles"
                                )
                            contract_hashes = manifest.get("contract_hashes")
                            expected_contract_hashes = {
                                "counts_sha256": sha256_canonical_json(scenario.get("counts")),
                                "parameters_sha256": sha256_canonical_json(scenario.get("parameters")),
                                "oracles_sha256": sha256_canonical_json(scenario.get("oracles")),
                            }
                            if contract_hashes != expected_contract_hashes:
                                errors.append(
                                    f"{manifest_relative} contract_hashes do not bind counts, parameters, and oracles"
                                )

    counts = scenario.get("counts")
    if not isinstance(counts, dict) or set(counts) != set(SCENARIO_COUNT_KEYS):
        errors.append(f"{label} counts must declare exactly {sorted(SCENARIO_COUNT_KEYS)}")
    else:
        for group_name, expected_keys in SCENARIO_COUNT_KEYS.items():
            group = counts.get(group_name)
            if not isinstance(group, dict) or set(group) != expected_keys:
                errors.append(f"{label} counts.{group_name} fields differ from the scenario contract")
                continue
            for field, value in group.items():
                if not is_nonnegative_int(value):
                    errors.append(f"{label} counts.{group_name}.{field} must be a non-negative integer")
        entities = counts.get("entities", {})
        if set(entities) == SCENARIO_COUNT_KEYS["entities"] and all(is_nonnegative_int(value) for value in entities.values()):
            entity_sum = sum(value for key, value in entities.items() if key != "total")
            if entities["total"] != entity_sum:
                errors.append(f"{label} counts.entities.total does not equal the declared entity classes")
        objects = counts.get("objects", {})
        if set(objects) == SCENARIO_COUNT_KEYS["objects"] and all(is_nonnegative_int(value) for value in objects.values()):
            if objects["placed_total"] != objects["dynamic"] + objects["static"]:
                errors.append(f"{label} placed_total does not equal dynamic + static")
            if objects["placed_total"] != objects["lod0"] + objects["lod1"] + objects["lod2"]:
                errors.append(f"{label} placed_total does not equal lod0 + lod1 + lod2")
        lights = counts.get("lights", {})
        if set(lights) == SCENARIO_COUNT_KEYS["lights"] and all(is_nonnegative_int(value) for value in lights.values()):
            if lights["total"] != lights["directional"] + lights["point"] + lights["spot"]:
                errors.append(f"{label} lights.total does not equal directional + point + spot")
            if lights["shadowed"] > lights["total"]:
                errors.append(f"{label} shadowed lights exceed total lights")
        crowd = counts.get("crowd", {})
        if (
            set(crowd) == SCENARIO_COUNT_KEYS["crowd"]
            and all(is_nonnegative_int(value) for value in crowd.values())
            and crowd["authoritative_people"] != crowd["visible_agents"] + crowd["offscreen_people"]
        ):
            errors.append(f"{label} authoritative_people does not equal visible_agents + offscreen_people")
        if scenario.get("kind") == "aggregate":
            runtime_values = [value for group in counts.values() for value in group.values()]
            if any(runtime_values):
                errors.append(f"{label} aggregate scenario must declare every runtime count as zero")
            if isinstance(fixture, dict) and (fixture.get("warmup_ticks") != 0 or fixture.get("duration_ticks") != 0):
                errors.append(f"{label} aggregate scenario must have zero warmup and duration")

    parameters = scenario.get("parameters")
    if not isinstance(parameters, list):
        errors.append(f"{label} parameters must be an array")
    else:
        names: list[object] = []
        for parameter in parameters:
            if not isinstance(parameter, dict) or set(parameter) != {"name", "value", "unit"}:
                errors.append(f"{label} has an invalid parameter record")
                continue
            names.append(str(parameter.get("name", "")))
            if not re.fullmatch(r"[a-z][a-z0-9_.-]+", str(parameter.get("name", ""))):
                errors.append(f"{label} has an invalid parameter name")
            if isinstance(parameter.get("value"), (dict, list)) or parameter.get("value") is None:
                errors.append(f"{label} parameter {parameter.get('name')} must have a scalar value")
            if parameter.get("unit") is not None and not isinstance(parameter.get("unit"), str):
                errors.append(f"{label} parameter {parameter.get('name')} has an invalid unit")
        if len(names) != len(set(names)):
            errors.append(f"{label} parameter names are not unique")

    oracles = scenario.get("oracles")
    if not isinstance(oracles, list) or not oracles:
        errors.append(f"{label} oracles must be a non-empty array")
    else:
        oracle_ids: list[object] = []
        oracle_subjects: list[str] = []
        for oracle in oracles:
            if not isinstance(oracle, dict) or set(oracle) != {"id", "subject", "operator", "expected", "unit"}:
                errors.append(f"{label} has an invalid oracle record")
                continue
            oracle_ids.append(str(oracle.get("id", "")))
            oracle_subjects.append(str(oracle.get("subject", "")))
            if not re.fullmatch(r"ORC_[A-Z0-9_]+", str(oracle.get("id", ""))):
                errors.append(f"{label} has an invalid oracle ID")
            if not re.fullmatch(r"[a-z][a-z0-9_.-]+", str(oracle.get("subject", ""))):
                errors.append(f"{label} oracle {oracle.get('id')} has an invalid subject")
            if oracle.get("operator") not in {"equal", "less-than", "less-or-equal", "greater-or-equal", "matches-reference"}:
                errors.append(f"{label} oracle {oracle.get('id')} has an invalid operator")
            if isinstance(oracle.get("expected"), (dict, list)) or oracle.get("expected") is None:
                errors.append(f"{label} oracle {oracle.get('id')} must have a scalar expected value")
            if oracle.get("unit") is not None and not isinstance(oracle.get("unit"), str):
                errors.append(f"{label} oracle {oracle.get('id')} has an invalid unit")
        if len(oracle_ids) != len(set(oracle_ids)):
            errors.append(f"{label} oracle IDs are not unique")
        if len(oracle_subjects) != len(set(oracle_subjects)):
            errors.append(f"{label} oracle subjects are not unique")

    return scenario, errors


def validate_scenario_registry() -> tuple[dict[str, dict], list[str]]:
    errors: list[str] = []
    scenarios: dict[str, dict] = {}
    registry = load_json(SCENARIO_REGISTRY)
    if not isinstance(registry, dict):
        return {}, ["scenarios/registry.json must be an object"]
    expected_registry_fields = {"schema_version", "hash_algorithm", "hash_domain", "scenarios"}
    if set(registry) != expected_registry_fields:
        errors.append("scenarios/registry.json has unexpected or missing fields")
    if registry.get("schema_version") != 1:
        errors.append("scenarios/registry.json schema_version must be 1")
    if registry.get("hash_algorithm") != "sha256" or registry.get("hash_domain") != "raw-file-bytes":
        errors.append("scenarios/registry.json must hash raw file bytes with sha256")
    entries = registry.get("scenarios")
    if not isinstance(entries, list) or not entries:
        return {}, errors + ["scenarios/registry.json scenarios must be a non-empty array"]

    entry_ids: list[str] = []
    entry_paths: list[str] = []
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != {"id", "path", "sha256"}:
            errors.append("scenarios/registry.json contains an invalid entry")
            continue
        scenario_id = str(entry.get("id", ""))
        relative = str(entry.get("path", ""))
        expected_hash = str(entry.get("sha256", ""))
        entry_ids.append(scenario_id)
        entry_paths.append(relative)
        if not re.fullmatch(r"SCN_[A-Z0-9_]+", scenario_id):
            errors.append(f"scenario registry has invalid ID {scenario_id!r}")
        if not re.fullmatch(r"[0-9a-f]{64}", expected_hash):
            errors.append(f"scenario registry {scenario_id} has invalid sha256")
        path = (ROOT / relative).resolve()
        try:
            path.relative_to(SCENARIO_DEFINITIONS.resolve())
        except ValueError:
            errors.append(f"scenario registry {scenario_id} path escapes scenario definitions: {relative}")
            continue
        if not path.exists() or not path.is_file():
            errors.append(f"scenario registry {scenario_id} path is missing: {relative}")
            continue
        actual_hash = sha256_file(path)
        if actual_hash != expected_hash:
            errors.append(f"scenario registry {scenario_id} hash mismatch: expected {expected_hash}, got {actual_hash}")
        scenario, definition_errors = validate_scenario_definition(path, scenario_id)
        errors.extend(definition_errors)
        scenarios[scenario_id] = scenario

    if len(entry_ids) != len(set(entry_ids)):
        errors.append("scenario registry IDs are not unique")
    if len(entry_paths) != len(set(entry_paths)):
        errors.append("scenario registry paths are not unique")
    if entry_ids != sorted(entry_ids):
        errors.append("scenario registry entries must be sorted by ID")
    registered_paths = {(ROOT / relative).resolve() for relative in entry_paths}
    definition_paths = {path.resolve() for path in SCENARIO_DEFINITIONS.glob("*.json")}
    if registered_paths != definition_paths:
        missing = sorted(str(path.relative_to(ROOT)) for path in definition_paths - registered_paths)
        stale = sorted(str(path) for path in registered_paths - definition_paths)
        errors.append(f"scenario registry/file set differs: unregistered={missing}, missing={stale}")
    referenced_fixture_paths = {
        (ROOT / scenario["fixture"]["fixture_manifest"]["path"]).resolve()
        for scenario in scenarios.values()
        if isinstance(scenario.get("fixture"), dict)
        and isinstance(scenario["fixture"].get("fixture_manifest"), dict)
        and isinstance(scenario["fixture"]["fixture_manifest"].get("path"), str)
    }
    fixture_paths = {path.resolve() for path in SCENARIO_FIXTURES.glob("*.fixture.json")}
    if referenced_fixture_paths != fixture_paths:
        unreferenced = sorted(str(path.relative_to(ROOT)) for path in fixture_paths - referenced_fixture_paths)
        missing = sorted(str(path) for path in referenced_fixture_paths - fixture_paths)
        errors.append(
            f"scenario fixture-manifest set differs: unreferenced={unreferenced}, missing={missing}"
        )
    return scenarios, errors


def validate_packet_scenario_references(path: Path, scenarios: dict[str, dict]) -> list[str]:
    errors: list[str] = []
    packet = load_json(path)
    packet_id = packet.get("id")
    references: list[tuple[str, str]] = []
    for scenario_id in packet.get("save_impact", {}).get("golden_scenarios", []):
        references.append(("save_impact.golden_scenarios", scenario_id))
    for metric in packet.get("performance_metrics", []):
        references.append((f"performance_metrics.{metric.get('name')}", metric.get("scenario")))
    for signal in packet.get("rollout", {}).get("health_signals", []):
        references.append((f"rollout.health_signals.{signal.get('name')}", signal.get("scenario")))

    for source, scenario_id in references:
        if not isinstance(scenario_id, str) or not re.fullmatch(r"SCN_[A-Z0-9_]+", scenario_id):
            errors.append(f"{path.relative_to(ROOT)} {source} must reference a registered SCN_ ID, got {scenario_id!r}")
            continue
        scenario = scenarios.get(scenario_id)
        if scenario is None:
            errors.append(f"{path.relative_to(ROOT)} {source} references unknown {scenario_id}")
            continue
        if packet_id not in scenario.get("applies_to_packets", []):
            errors.append(f"{path.relative_to(ROOT)} references {scenario_id}, which does not apply to {packet_id}")
    for signal in packet.get("rollout", {}).get("health_signals", []):
        if signal.get("name") == "required-test-failures":
            scenario = scenarios.get(signal.get("scenario"))
            if scenario is not None and scenario.get("kind") != "aggregate":
                errors.append(f"{path.relative_to(ROOT)} required-test-failures must use an aggregate scenario")
            if scenario is not None and scenario.get("kind") == "aggregate":
                parameters = {item.get("name"): item.get("value") for item in scenario.get("parameters", [])}
                required_count = sum(1 for test in packet.get("acceptance_tests", []) if test.get("required"))
                if parameters.get("required-acceptance-tests") != required_count:
                    errors.append(
                        f"{path.relative_to(ROOT)} has {required_count} required tests but "
                        f"{signal.get('scenario')} freezes {parameters.get('required-acceptance-tests')!r}"
                    )
                minimum_runs = packet.get("rollout", {}).get("minimum_runs")
                if parameters.get("minimum-runs") != minimum_runs:
                    errors.append(
                        f"{path.relative_to(ROOT)} minimum_runs {minimum_runs!r} differs from "
                        f"{signal.get('scenario')} value {parameters.get('minimum-runs')!r}"
                    )

    def check_metric_oracle(metric: dict, source: str) -> None:
        scenario = scenarios.get(metric.get("scenario"))
        if scenario is None:
            return
        matches = [oracle for oracle in scenario.get("oracles", []) if oracle.get("subject") == metric.get("name")]
        if len(matches) != 1:
            errors.append(
                f"{path.relative_to(ROOT)} {source} requires exactly one {metric.get('name')!r} oracle "
                f"in {metric.get('scenario')}, found {len(matches)}"
            )
            return
        oracle = matches[0]
        if oracle.get("expected") != metric.get("target"):
            errors.append(
                f"{path.relative_to(ROOT)} {source} target {metric.get('target')!r} differs from "
                f"{metric.get('scenario')} oracle {oracle.get('expected')!r}"
            )
        packet_comparator = metric.get("comparator")
        oracle_operator = oracle.get("operator")
        compatible = oracle_operator == packet_comparator or (
            oracle_operator == "equal" and packet_comparator in {"less-or-equal", "greater-or-equal"}
        )
        if not compatible:
            errors.append(
                f"{path.relative_to(ROOT)} {source} comparator {packet_comparator!r} conflicts with "
                f"{metric.get('scenario')} oracle operator {oracle_operator!r}"
            )

    for metric in packet.get("performance_metrics", []):
        check_metric_oracle(metric, f"performance_metrics.{metric.get('name')}")
    for signal in packet.get("rollout", {}).get("health_signals", []):
        check_metric_oracle(signal, f"rollout.health_signals.{signal.get('name')}")
    return errors


def validate_count_contract_v2(value: object, label: str) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, dict) or set(value) != set(SCENARIO_COUNT_KEYS):
        return [f"{label} must contain exactly {sorted(SCENARIO_COUNT_KEYS)}"]
    for group, keys in SCENARIO_COUNT_KEYS.items():
        block = value.get(group)
        if not isinstance(block, dict) or set(block) != keys:
            errors.append(f"{label}.{group} must contain exactly {sorted(keys)}")
            continue
        for key, count in block.items():
            if not is_nonnegative_int(count):
                errors.append(f"{label}.{group}.{key} must be a non-negative integer")
    entities = value.get("entities", {})
    if isinstance(entities, dict):
        expected = sum(count for key, count in entities.items() if key != "total" and is_nonnegative_int(count))
        if entities.get("total") != expected:
            errors.append(f"{label}.entities.total {entities.get('total')!r} != component sum {expected}")
    objects = value.get("objects", {})
    if isinstance(objects, dict):
        if objects.get("placed_total") != objects.get("dynamic", 0) + objects.get("static", 0):
            errors.append(f"{label}.objects placed_total != dynamic + static")
        if objects.get("placed_total") != objects.get("lod0", 0) + objects.get("lod1", 0) + objects.get("lod2", 0):
            errors.append(f"{label}.objects placed_total != LOD occupancy sum")
    lights = value.get("lights", {})
    if isinstance(lights, dict) and lights.get("total") != lights.get("directional", 0) + lights.get("point", 0) + lights.get("spot", 0):
        errors.append(f"{label}.lights total != directional + point + spot")
    crowd = value.get("crowd", {})
    if isinstance(crowd, dict) and crowd.get("authoritative_people") != crowd.get("visible_agents", 0) + crowd.get("offscreen_people", 0):
        # Visible agents may include robots, so only reject when visible humans are not explicitly mixed.
        if value.get("entities", {}).get("robots", 0) == 0:
            errors.append(f"{label}.crowd people != visible + offscreen for a no-robot case")
    return errors


def validate_materialized_entities(
    value: object,
    counts: object,
    label: str,
) -> list[str]:
    """Require exact, valued entity records rather than a count-only recipe."""
    errors: list[str] = []
    if not isinstance(value, list):
        return [f"{label} must be a materialized entity array"]
    entity_counts = counts.get("entities", {}) if isinstance(counts, dict) else {}
    expected_total = entity_counts.get("total")
    if len(value) != expected_total:
        errors.append(f"{label} has {len(value)} records; expected {expected_total}")
    ids: list[str] = []
    actual_types: Counter[str] = Counter()
    ordinals: dict[str, list[int]] = {kind: [] for kind in ENTITY_TYPES}
    required = {"id", "type", "ordinal", "value"}
    for index, record in enumerate(value):
        item_label = f"{label}[{index}]"
        if not isinstance(record, dict) or set(record) != required:
            errors.append(f"{item_label} must contain exactly {sorted(required)}")
            continue
        entity_id = record.get("id")
        entity_type = record.get("type")
        ordinal = record.get("ordinal")
        materialized_value = record.get("value")
        if not isinstance(entity_id, str) or not entity_id:
            errors.append(f"{item_label}.id must be a non-empty string")
        else:
            ids.append(entity_id)
        if entity_type not in ENTITY_TYPES:
            errors.append(f"{item_label}.type is invalid: {entity_type!r}")
        else:
            actual_types[entity_type] += 1
            if isinstance(ordinal, int) and not isinstance(ordinal, bool):
                ordinals[entity_type].append(ordinal)
        if not isinstance(ordinal, int) or isinstance(ordinal, bool) or ordinal < 1:
            errors.append(f"{item_label}.ordinal must be a positive integer")
        if not isinstance(materialized_value, dict) or len(materialized_value) < 3:
            errors.append(f"{item_label}.value must contain at least three concrete fields")
    if len(ids) != len(set(ids)):
        errors.append(f"{label} repeats an entity ID")
    for count_key, entity_type in ENTITY_COUNT_TO_TYPE.items():
        expected = entity_counts.get(count_key)
        if actual_types[entity_type] != expected:
            errors.append(
                f"{label} has {actual_types[entity_type]} {entity_type} records; expected {expected}"
            )
        if sorted(ordinals[entity_type]) != list(range(1, actual_types[entity_type] + 1)):
            errors.append(f"{label} {entity_type} ordinals must be contiguous from one")
    return errors


def validate_materialized_state_records(
    value: object,
    expected_count: object,
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, list):
        return [f"{label} must be a materialized state-record array"]
    if len(value) != expected_count:
        errors.append(f"{label} has {len(value)} records; expected {expected_count}")
    ids: list[str] = []
    ordinals: list[int] = []
    required = {"id", "domain", "ordinal", "version", "value"}
    for index, record in enumerate(value):
        item_label = f"{label}[{index}]"
        if not isinstance(record, dict) or set(record) != required:
            errors.append(f"{item_label} must contain exactly {sorted(required)}")
            continue
        record_id = record.get("id")
        ordinal = record.get("ordinal")
        materialized_value = record.get("value")
        if not isinstance(record_id, str) or not record_id:
            errors.append(f"{item_label}.id must be a non-empty string")
        else:
            ids.append(record_id)
        if record.get("domain") not in STATE_DOMAINS:
            errors.append(f"{item_label}.domain is invalid: {record.get('domain')!r}")
        if not isinstance(ordinal, int) or isinstance(ordinal, bool) or ordinal < 1:
            errors.append(f"{item_label}.ordinal must be a positive integer")
        else:
            ordinals.append(ordinal)
        if record.get("version") != 1:
            errors.append(f"{item_label}.version must be 1")
        if not isinstance(materialized_value, dict) or len(materialized_value) < 3:
            errors.append(f"{item_label}.value must contain at least three concrete fields")
    if len(ids) != len(set(ids)):
        errors.append(f"{label} repeats a state-record ID")
    if sorted(ordinals) != list(range(1, len(value) + 1)):
        errors.append(f"{label} ordinals must be contiguous from one")
    return errors


def validate_observations(
    value: object,
    oracles: object,
    start_tick: int,
    end_tick: int,
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, list):
        return [f"{label} must be an explicit observation array"]
    oracle_list = oracles if isinstance(oracles, list) else []
    expected = {
        (oracle.get("id"), oracle.get("subject")): oracle
        for oracle in oracle_list
        if isinstance(oracle, dict)
    }
    actual: dict[tuple[object, object], dict] = {}
    required = {
        "sequence", "tick", "subject", "selector", "operator",
        "expected_source", "oracle_id",
    }
    for index, observation in enumerate(value):
        item_label = f"{label}[{index}]"
        if not isinstance(observation, dict) or set(observation) != required:
            errors.append(f"{item_label} must contain exactly {sorted(required)}")
            continue
        sequence = observation.get("sequence")
        tick = observation.get("tick")
        if sequence != index:
            errors.append(f"{item_label}.sequence must equal its array position")
        if not isinstance(tick, int) or isinstance(tick, bool) or not start_tick <= tick <= end_tick:
            errors.append(f"{item_label}.tick must be within [{start_tick}, {end_tick}]")
        if not isinstance(observation.get("selector"), str) or not observation.get("selector"):
            errors.append(f"{item_label}.selector must be non-empty")
        if observation.get("operator") not in ORACLE_OPERATORS:
            errors.append(f"{item_label}.operator is invalid")
        if observation.get("expected_source") != "scenario-definition":
            errors.append(f"{item_label}.expected_source must be scenario-definition")
        key = (observation.get("oracle_id"), observation.get("subject"))
        if key in actual:
            errors.append(f"{label} repeats oracle observation {key[0]!r}")
        actual[key] = observation
    if set(actual) != set(expected):
        errors.append(
            f"{label} oracle bindings differ: expected {sorted(map(str, expected))}, "
            f"got {sorted(map(str, actual))}"
        )
    for key in set(actual) & set(expected):
        if actual[key].get("operator") != expected[key].get("operator"):
            errors.append(f"{label} {key[0]} operator differs from scenario definition")
    return errors


def validate_explicit_events(
    value: object,
    expected_counts: object,
    start_tick: int,
    end_tick: int,
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, list):
        return [f"{label} must be an explicit ordered event array"]
    counts = expected_counts if isinstance(expected_counts, dict) else {}
    expected_total = sum(count for count in counts.values() if is_nonnegative_int(count))
    if len(value) != expected_total:
        errors.append(f"{label} has {len(value)} events; expected {expected_total}")
    actual_counts: Counter[str] = Counter()
    ids: list[str] = []
    ordering: list[tuple[int, int]] = []
    required = {"sequence", "tick", "kind", "payload"}
    payload_fields = {
        "scripted_commands": {"command", "command_id", "target", "arguments"},
        "keyboard_events": {"device", "key", "phase"},
        "pointer_events": {"button", "phase", "x_milli", "y_milli"},
        "fault_injections": {"boundary", "fault", "occurrence"},
    }
    for index, event in enumerate(value):
        item_label = f"{label}[{index}]"
        if not isinstance(event, dict) or set(event) != required:
            errors.append(f"{item_label} must contain exactly {sorted(required)}")
            continue
        sequence = event.get("sequence")
        tick = event.get("tick")
        kind = event.get("kind")
        payload = event.get("payload")
        if sequence != index:
            errors.append(f"{item_label}.sequence must equal its array position")
        if not isinstance(tick, int) or isinstance(tick, bool) or not start_tick <= tick <= end_tick:
            errors.append(f"{item_label}.tick must be within [{start_tick}, {end_tick}]")
        if isinstance(tick, int) and not isinstance(tick, bool) and isinstance(sequence, int) and not isinstance(sequence, bool):
            ordering.append((tick, sequence))
        if kind not in EVENT_KINDS:
            errors.append(f"{item_label}.kind is invalid: {kind!r}")
        else:
            actual_counts[kind] += 1
        if not isinstance(payload, dict) or len(payload) < 2:
            errors.append(f"{item_label}.payload must be a concrete object")
            continue
        if kind in payload_fields and set(payload) != payload_fields[kind]:
            errors.append(
                f"{item_label}.payload must contain exactly {sorted(payload_fields[kind])}"
            )
        if kind == "controller_events" and len(payload) < 2:
            errors.append(f"{item_label}.payload lacks a concrete controller event")
        if kind == "scripted_commands":
            if not isinstance(payload.get("command"), str) or not payload.get("command"):
                errors.append(f"{item_label}.payload.command must be non-empty")
            command_id = payload.get("command_id")
            if not isinstance(command_id, str) or not command_id:
                errors.append(f"{item_label}.payload.command_id must be non-empty")
            else:
                ids.append(command_id)
            if not isinstance(payload.get("target"), str) or not payload.get("target"):
                errors.append(f"{item_label}.payload.target must be non-empty")
            if not isinstance(payload.get("arguments"), dict):
                errors.append(f"{item_label}.payload.arguments must be an object")
    if ordering != sorted(ordering):
        errors.append(f"{label} must be ordered by ascending (tick, sequence)")
    if len(ids) != len(set(ids)):
        errors.append(f"{label} repeats a command_id")
    for kind in EVENT_KINDS:
        if actual_counts[kind] != counts.get(kind):
            errors.append(f"{label} has {actual_counts[kind]} {kind}; expected {counts.get(kind)}")
    return errors


def validate_materialized_starting_state(
    artifact: dict,
    scenario: dict,
    label: str,
) -> list[str]:
    errors: list[str] = []
    required = {
        "schema_version", "artifact_kind", "semantic_id", "scenario_id",
        "scenario_revision", "world_seed", "starting_tick", "canonicalization",
        "canonical_entities", "canonical_state_records", "case_states",
    }
    if set(artifact) != required:
        errors.append(f"{label} must contain exactly {sorted(required)}")
    fixture = scenario.get("fixture", {})
    if artifact.get("world_seed") != fixture.get("world_seed") or artifact.get("starting_tick") != fixture.get("starting_tick"):
        errors.append(f"{label} seed/tick differ from scenario fixture")
    if not isinstance(artifact.get("canonicalization"), str) or not artifact.get("canonicalization"):
        errors.append(f"{label} canonicalization must be non-empty")
    errors.extend(validate_materialized_entities(artifact.get("canonical_entities"), scenario.get("counts"), f"{label}.canonical_entities"))
    expected_records = scenario.get("counts", {}).get("state", {}).get("authoritative_records")
    errors.extend(validate_materialized_state_records(artifact.get("canonical_state_records"), expected_records, f"{label}.canonical_state_records"))
    cases = scenario.get("cases", [])
    case_states = artifact.get("case_states")
    if not isinstance(case_states, list):
        return errors + [f"{label}.case_states must be a materialized array"]
    expected_cases = {case.get("id"): case for case in cases if isinstance(case, dict)}
    actual_cases = {case.get("id"): case for case in case_states if isinstance(case, dict)}
    if len(actual_cases) != len(case_states):
        errors.append(f"{label}.case_states must have unique, well-formed IDs")
    if set(actual_cases) != set(expected_cases):
        errors.append(f"{label}.case_states IDs differ from scenario cases")
    case_required = {"id", "canonical_entities", "canonical_state_records", "ablation_contract"}
    for case_id in sorted(set(actual_cases) & set(expected_cases)):
        state = actual_cases[case_id]
        case = expected_cases[case_id]
        case_label = f"{label}.case_states.{case_id}"
        if set(state) != case_required:
            errors.append(f"{case_label} must contain exactly {sorted(case_required)}")
        errors.extend(validate_materialized_entities(state.get("canonical_entities"), case.get("counts"), f"{case_label}.canonical_entities"))
        case_records = case.get("counts", {}).get("state", {}).get("authoritative_records")
        errors.extend(validate_materialized_state_records(state.get("canonical_state_records"), case_records, f"{case_label}.canonical_state_records"))
        contract = state.get("ablation_contract")
        if not isinstance(contract, dict) or not contract:
            errors.append(f"{case_label}.ablation_contract must bind the case parameters")
        expected_contract = {
            item.get("name"): item.get("value")
            for item in case.get("parameters", [])
            if isinstance(item, dict) and item.get("name") in {
                "ablation", "shared-bottleneck", "case-composition",
                "preparation", "vehicle-module", "clock-policy",
            }
        }
        if contract != expected_contract:
            errors.append(f"{case_label}.ablation_contract differs from scenario parameters")
    return errors


def validate_materialized_content_set(
    artifact: dict,
    scenario: dict,
    label: str,
) -> list[str]:
    errors: list[str] = []
    required = {
        "schema_version", "artifact_kind", "semantic_id", "scenario_id",
        "scenario_revision", "canonicalization", "definitions",
    }
    allowed = required | {"render_workload"}
    if not required.issubset(artifact) or not set(artifact).issubset(allowed):
        errors.append(f"{label} has missing or extra materialized content fields")
    if not isinstance(artifact.get("canonicalization"), str) or not artifact.get("canonicalization"):
        errors.append(f"{label} canonicalization must be non-empty")
    definitions = artifact.get("definitions")
    if not isinstance(definitions, list):
        return errors + [f"{label}.definitions must be a materialized array"]
    expected_count = scenario.get("counts", {}).get("state", {}).get("content_definitions")
    if len(definitions) != expected_count:
        errors.append(f"{label}.definitions has {len(definitions)} records; expected {expected_count}")
    ids: list[str] = []
    definition_required = {"id", "kind", "version", "tags", "enabled", "contract"}
    for index, definition in enumerate(definitions):
        item_label = f"{label}.definitions[{index}]"
        if not isinstance(definition, dict) or set(definition) != definition_required:
            errors.append(f"{item_label} must contain exactly {sorted(definition_required)}")
            continue
        definition_id = definition.get("id")
        if not isinstance(definition_id, str) or not definition_id:
            errors.append(f"{item_label}.id must be non-empty")
        else:
            ids.append(definition_id)
        if definition.get("kind") not in CONTENT_KINDS:
            errors.append(f"{item_label}.kind is invalid")
        if definition.get("version") != 1:
            errors.append(f"{item_label}.version must be 1")
        tags = definition.get("tags")
        if not isinstance(tags, list) or not tags or any(not isinstance(tag, str) or not tag for tag in tags):
            errors.append(f"{item_label}.tags must contain non-empty strings")
        if not isinstance(definition.get("enabled"), bool):
            errors.append(f"{item_label}.enabled must be boolean")
        contract = definition.get("contract")
        if not isinstance(contract, dict) or len(contract) < 2:
            errors.append(f"{item_label}.contract must contain concrete content values")
    if len(ids) != len(set(ids)):
        errors.append(f"{label}.definitions repeats an ID")
    if scenario.get("id") == "SCN_SPIKE_SLICE":
        workload = artifact.get("render_workload")
        if not isinstance(workload, dict) or not workload:
            errors.append(f"{label}.render_workload must materialize the renderer fixture")
    return errors


def validate_target_bindings(
    artifact: dict,
    scenario: dict,
    starting_artifact: object,
    content_artifact: object,
    label: str,
) -> list[str]:
    """Prove every logical command target resolves before the first tick."""
    errors: list[str] = []
    bindings = artifact.get("target_bindings")
    if not isinstance(bindings, list):
        return [f"{label}.target_bindings must be an explicit array"]
    if artifact.get("target_bindings_sha256") != sha256_canonical_json(bindings):
        errors.append(f"{label}.target_bindings_sha256 does not bind the canonical table")

    referenced: list[str] = []
    event_groups: list[tuple[str | None, object]] = [(None, artifact.get("events"))]
    for case_event in artifact.get("case_events", []) if isinstance(artifact.get("case_events"), list) else []:
        if isinstance(case_event, dict):
            event_groups.append((case_event.get("id"), case_event.get("events")))
    for case_id, events in event_groups:
        if not isinstance(events, list):
            continue
        for event in events:
            if not isinstance(event, dict) or event.get("kind") != "scripted_commands":
                continue
            payload = event.get("payload")
            target = payload.get("target") if isinstance(payload, dict) else None
            if isinstance(target, str) and target:
                referenced.append(target)

    starting_value = starting_artifact if isinstance(starting_artifact, dict) else {}
    content_value = content_artifact if isinstance(content_artifact, dict) else {}
    base_entities = {
        item.get("id")
        for item in starting_value.get("canonical_entities", [])
        if isinstance(item, dict)
    }
    base_records = {
        item.get("id")
        for item in starting_value.get("canonical_state_records", [])
        if isinstance(item, dict)
    }
    content_definitions = {
        item.get("id")
        for item in content_value.get("definitions", [])
        if isinstance(item, dict)
    }
    case_states = {
        item.get("id"): item
        for item in starting_value.get("case_states", [])
        if isinstance(item, dict)
    }
    expected_case_ids = {
        case.get("id") for case in scenario.get("cases", []) if isinstance(case, dict)
    }

    logical_ids: list[str] = []
    canonical_fields = {"logical_target_id", "binding_kind", "resolved_id", "case_resolutions"}
    selector_fields = {"logical_target_id", "binding_kind", "selector_kind", "selector_value"}
    for index, binding in enumerate(bindings):
        item_label = f"{label}.target_bindings[{index}]"
        if not isinstance(binding, dict):
            errors.append(f"{item_label} must be an object")
            continue
        logical_id = binding.get("logical_target_id")
        if not isinstance(logical_id, str) or not logical_id:
            errors.append(f"{item_label}.logical_target_id must be non-empty")
        else:
            logical_ids.append(logical_id)
        binding_kind = binding.get("binding_kind")
        if binding_kind == "scenario-selector":
            if set(binding) != selector_fields:
                errors.append(f"{item_label} selector must contain exactly {sorted(selector_fields)}")
                continue
            selector_kind = binding.get("selector_kind")
            if selector_kind not in SCENARIO_SELECTOR_VALUES:
                errors.append(f"{item_label} has unknown selector_kind {selector_kind!r}")
                continue
            expected_value = (
                scenario.get("id")
                if selector_kind == "scenario-root"
                else SCENARIO_SELECTOR_VALUES[selector_kind]
            )
            if binding.get("selector_value") != expected_value:
                errors.append(
                    f"{item_label}.selector_value must be {expected_value!r} for {selector_kind}"
                )
            continue
        if set(binding) != canonical_fields:
            errors.append(f"{item_label} canonical binding must contain exactly {sorted(canonical_fields)}")
            continue
        resolved_id = binding.get("resolved_id")
        resolution_sets = {
            "canonical-entity": base_entities,
            "canonical-state-record": base_records,
            "content-definition": content_definitions,
        }
        allowed_ids = resolution_sets.get(binding_kind)
        if allowed_ids is None:
            errors.append(f"{item_label} has unknown binding_kind {binding_kind!r}")
            continue
        if resolved_id not in allowed_ids:
            errors.append(f"{item_label}.resolved_id is absent from its canonical artifact")
        case_resolutions = binding.get("case_resolutions")
        if not isinstance(case_resolutions, list):
            errors.append(f"{item_label}.case_resolutions must be an array")
            continue
        resolution_by_case: dict[object, object] = {}
        for case_index, resolution in enumerate(case_resolutions):
            resolution_label = f"{item_label}.case_resolutions[{case_index}]"
            if not isinstance(resolution, dict) or set(resolution) != {"case_id", "resolved_id"}:
                errors.append(f"{resolution_label} must contain exactly case_id and resolved_id")
                continue
            case_id = resolution.get("case_id")
            if case_id in resolution_by_case:
                errors.append(f"{item_label} repeats case resolution {case_id!r}")
            resolution_by_case[case_id] = resolution.get("resolved_id")
        if binding_kind == "content-definition":
            if case_resolutions:
                errors.append(f"{item_label} content definitions must not declare case overrides")
            continue
        if set(resolution_by_case) != expected_case_ids:
            errors.append(f"{item_label} case resolutions must exactly cover scenario cases")
        for case_id in set(resolution_by_case) & expected_case_ids:
            case_state = case_states.get(case_id, {})
            collection = (
                case_state.get("canonical_entities", [])
                if binding_kind == "canonical-entity"
                else case_state.get("canonical_state_records", [])
            )
            case_ids = {item.get("id") for item in collection if isinstance(item, dict)}
            if resolution_by_case[case_id] not in case_ids:
                errors.append(
                    f"{item_label} case {case_id} resolved_id is absent from its case starting state"
                )

    if len(logical_ids) != len(set(logical_ids)):
        errors.append(f"{label}.target_bindings repeats a logical_target_id")
    if logical_ids != sorted(logical_ids):
        errors.append(f"{label}.target_bindings must be ordered by logical_target_id")
    if set(logical_ids) != set(referenced):
        errors.append(
            f"{label}.target_bindings must exactly equal referenced scripted targets: "
            f"missing={sorted(set(referenced) - set(logical_ids))}, "
            f"unreferenced={sorted(set(logical_ids) - set(referenced))}"
        )
    return errors


def validate_materialized_input_script(
    artifact: dict,
    scenario: dict,
    label: str,
    starting_artifact: object,
    content_artifact: object,
) -> list[str]:
    errors: list[str] = []
    required = {
        "schema_version", "artifact_kind", "semantic_id", "scenario_id",
        "scenario_revision", "tick_rate_hz", "warmup_ticks", "duration_ticks",
        "normative_execution", "target_bindings_sha256", "target_bindings",
        "events", "case_events", "oracle_observations",
    }
    allowed = required | {"camera_path"}
    if not required.issubset(artifact) or not set(artifact).issubset(allowed):
        errors.append(f"{label} has missing or extra explicit input fields")
    fixture = scenario.get("fixture", {})
    for field in ("tick_rate_hz", "warmup_ticks", "duration_ticks"):
        if artifact.get(field) != fixture.get(field):
            errors.append(f"{label}.{field} differs from scenario fixture")
    execution = artifact.get("normative_execution")
    execution_fields = {
        "ordering", "fixed_tick_rule", "case_isolation", "unknown_command_rule",
        "integer_rule", "autonomous_tick_rule", "target_resolution_rule",
        "unresolved_target_rule", "target_binding_hash_rule",
    }
    if not isinstance(execution, dict) or set(execution) != execution_fields:
        errors.append(f"{label}.normative_execution must contain exactly {sorted(execution_fields)}")
    elif any(not isinstance(value, str) or not value for value in execution.values()):
        errors.append(f"{label}.normative_execution rules must be non-empty")
    else:
        exact_target_laws = {
            "target_resolution_rule": TARGET_RESOLUTION_RULE,
            "unresolved_target_rule": UNRESOLVED_TARGET_RULE,
            "target_binding_hash_rule": TARGET_BINDING_HASH_RULE,
        }
        for field, expected in exact_target_laws.items():
            if execution.get(field) != expected:
                errors.append(f"{label}.normative_execution.{field} differs from the exact law")
    start_tick = int(fixture.get("starting_tick", "0"))
    end_tick = start_tick + fixture.get("duration_ticks", 0)
    errors.extend(validate_explicit_events(artifact.get("events"), scenario.get("counts", {}).get("inputs"), start_tick, end_tick, f"{label}.events"))
    errors.extend(validate_observations(artifact.get("oracle_observations"), scenario.get("oracles"), start_tick, end_tick, f"{label}.oracle_observations"))
    errors.extend(validate_target_bindings(artifact, scenario, starting_artifact, content_artifact, label))
    cases = scenario.get("cases", [])
    case_events = artifact.get("case_events")
    if not isinstance(case_events, list):
        return errors + [f"{label}.case_events must be an explicit array"]
    expected_cases = {case.get("id"): case for case in cases if isinstance(case, dict)}
    actual_cases = {case.get("id"): case for case in case_events if isinstance(case, dict)}
    if len(actual_cases) != len(case_events):
        errors.append(f"{label}.case_events must have unique, well-formed IDs")
    if set(actual_cases) != set(expected_cases):
        errors.append(f"{label}.case_events IDs differ from scenario cases")
    case_required = {"id", "events", "oracle_observations"}
    for case_id in sorted(set(actual_cases) & set(expected_cases)):
        case_event = actual_cases[case_id]
        case = expected_cases[case_id]
        case_label = f"{label}.case_events.{case_id}"
        if set(case_event) != case_required:
            errors.append(f"{case_label} must contain exactly {sorted(case_required)}")
        errors.extend(validate_explicit_events(case_event.get("events"), case.get("counts", {}).get("inputs"), start_tick, end_tick, f"{case_label}.events"))
        errors.extend(validate_observations(case_event.get("oracle_observations"), case.get("oracles"), start_tick, end_tick, f"{case_label}.oracle_observations"))
    if scenario.get("id") == "SCN_SPIKE_SLICE":
        camera_path = artifact.get("camera_path")
        if not isinstance(camera_path, dict) or not camera_path:
            errors.append(f"{label}.camera_path must materialize the renderer fixture")
    return errors


def validate_current_scenario_v2(path: Path, entry: dict) -> tuple[dict, list[str]]:
    errors = validate_instance_shape(path, SCENARIO_SCHEMA)
    scenario = load_json(path)
    label = str(path.relative_to(ROOT))
    sid = entry.get("id")
    revision = entry.get("revision")
    if scenario.get("id") != sid or scenario.get("revision") != revision:
        errors.append(f"{label} identity/revision differs from registry")
    expected_path = f"scenarios/definitions/{sid}/r{revision}.json"
    if str(path.relative_to(ROOT)) != expected_path:
        errors.append(f"{label} is not the exact revisioned path {expected_path}")
    if scenario.get("schema_version") != 2:
        errors.append(f"{label} schema_version must be 2")
    errors.extend(validate_count_contract_v2(scenario.get("counts"), f"{label}.counts"))
    cases = scenario.get("cases", [])
    if cases:
        case_ids = [case.get("id") for case in cases if isinstance(case, dict)]
        if len(case_ids) != len(cases) or len(case_ids) != len(set(case_ids)):
            errors.append(f"{label} cases must have unique IDs")
        for case in cases:
            if not isinstance(case, dict) or set(case) != {"id", "counts", "parameters", "oracles"}:
                errors.append(f"{label} has malformed case contract")
                continue
            errors.extend(validate_count_contract_v2(case.get("counts"), f"{label}.cases.{case.get('id')}.counts"))
            oracle_ids = [item.get("id") for item in case.get("oracles", []) if isinstance(item, dict)]
            if len(oracle_ids) != len(set(oracle_ids)):
                errors.append(f"{label} case {case.get('id')} repeats an oracle ID")

    fixture = scenario.get("fixture", {})
    tick_rate = fixture.get("tick_rate_hz")
    if not is_nonnegative_int(tick_rate) or tick_rate < 1:
        errors.append(f"{label} has invalid tick rate")
    elif fixture.get("warmup_ticks") != fixture.get("warmup_seconds", -1) * tick_rate or fixture.get("duration_ticks") != fixture.get("duration_seconds", -1) * tick_rate:
        errors.append(f"{label} fixture seconds/ticks are inconsistent")
    manifest_ref = fixture.get("fixture_manifest", {})
    expected_manifest_path = f"scenarios/fixtures/{sid}/r{revision}.fixture.json"
    if manifest_ref.get("path") != expected_manifest_path or manifest_ref.get("path") != entry.get("fixture_manifest_path"):
        errors.append(f"{label} does not bind registry fixture manifest path")
    if manifest_ref.get("sha256") != entry.get("fixture_manifest_sha256"):
        errors.append(f"{label} does not bind registry fixture manifest hash")
    manifest_path = (ROOT / str(manifest_ref.get("path", ""))).resolve()
    try:
        manifest_path.relative_to(SCENARIO_FIXTURES.resolve())
    except ValueError:
        errors.append(f"{label} fixture manifest escapes scenario fixtures")
        return scenario, errors
    if not manifest_path.is_file():
        errors.append(f"{label} fixture manifest is missing")
        return scenario, errors
    if sha256_file(manifest_path) != manifest_ref.get("sha256"):
        errors.append(f"{label} fixture manifest hash mismatch")
    errors.extend(validate_instance_shape(manifest_path, SCENARIO_FIXTURE_SCHEMA))
    manifest = load_json(manifest_path)
    manifest_fields = {
        "schema_version", "scenario_id", "scenario_revision", "hash_algorithm",
        "artifacts",
    }
    if set(manifest) != manifest_fields:
        errors.append(f"{manifest_path.relative_to(ROOT)} must contain exactly {sorted(manifest_fields)}")
    if manifest.get("schema_version") != 2 or manifest.get("hash_algorithm") != "sha256":
        errors.append(f"{manifest_path.relative_to(ROOT)} must use artifact-backed manifest schema v2 and SHA-256")
    if manifest.get("scenario_id") != sid or manifest.get("scenario_revision") != revision:
        errors.append(f"{manifest_path.relative_to(ROOT)} identity/revision mismatch")
    artifact_ids = {
        "starting_state": fixture.get("starting_state_id"),
        "content_set": fixture.get("content_set_id"),
        "input_script": fixture.get("input_script_id"),
    }
    manifest_artifacts = manifest.get("artifacts")
    if not isinstance(manifest_artifacts, dict) or set(manifest_artifacts) != set(artifact_ids):
        errors.append(f"{manifest_path.relative_to(ROOT)} must bind exactly the three typed artifacts")
    expected_kinds = {"starting_state": "starting-state", "content_set": "content-set", "input_script": "input-script"}
    loaded_artifacts: dict[str, dict] = {}
    for key, semantic_id in artifact_ids.items():
        ref = manifest.get("artifacts", {}).get(key, {}) if isinstance(manifest.get("artifacts"), dict) else {}
        ref_fields = {"kind", "semantic_id", "path", "sha256", "media_type", "schema_version"}
        if not isinstance(ref, dict) or set(ref) != ref_fields:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} must contain exactly {sorted(ref_fields)}")
        expected_artifact_path = f"scenarios/artifacts/{sid}/r{revision}/{key.replace('_', '-')}.json"
        if ref.get("kind") != expected_kinds[key] or ref.get("semantic_id") != semantic_id:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} kind/semantic ID mismatch")
        if ref.get("media_type") != "application/json" or ref.get("schema_version") != 1:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} media/schema contract mismatch")
        if ref.get("path") != expected_artifact_path:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} must use {expected_artifact_path}")
        artifact_path = (ROOT / str(ref.get("path", ""))).resolve()
        try:
            artifact_path.relative_to(SCENARIO_ARTIFACTS.resolve())
        except ValueError:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} escapes scenario artifacts")
            continue
        if not artifact_path.is_file():
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} is missing")
            continue
        if sha256_file(artifact_path) != ref.get("sha256"):
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} hash mismatch")
        errors.extend(validate_instance_shape(artifact_path, SCENARIO_ARTIFACT_SCHEMA))
        artifact = load_json(artifact_path)
        loaded_artifacts[key] = artifact
        if artifact.get("schema_version") != 1 or artifact.get("artifact_kind") != expected_kinds[key] or artifact.get("semantic_id") != semantic_id:
            errors.append(f"{artifact_path.relative_to(ROOT)} typed identity mismatch")
        if artifact.get("scenario_id") != sid or artifact.get("scenario_revision") != revision:
            errors.append(f"{artifact_path.relative_to(ROOT)} scenario binding mismatch")
        if key == "starting_state":
            errors.extend(validate_materialized_starting_state(artifact, scenario, str(artifact_path.relative_to(ROOT))))
        elif key == "content_set":
            errors.extend(validate_materialized_content_set(artifact, scenario, str(artifact_path.relative_to(ROOT))))
        else:
            errors.extend(
                validate_materialized_input_script(
                    artifact,
                    scenario,
                    str(artifact_path.relative_to(ROOT)),
                    loaded_artifacts.get("starting_state"),
                    loaded_artifacts.get("content_set"),
                )
            )
    return scenario, errors


def validate_scenario_registry_v2() -> tuple[dict[str, dict], list[str]]:
    errors: list[str] = []
    registry = load_json(SCENARIO_REGISTRY)
    registry_fields = {
        "schema_version", "hash_algorithm", "hash_domain",
        "revision_baseline", "scenarios",
    }
    if set(registry) != registry_fields:
        errors.append(f"scenario registry must contain exactly {sorted(registry_fields)}")
    if registry.get("schema_version") != 2 or registry.get("hash_algorithm") != "sha256" or registry.get("hash_domain") != "raw-file-bytes":
        errors.append("scenario registry must use schema_version 2 and raw-file SHA-256")
    if registry.get("revision_baseline") != "initial-commit-r1":
        errors.append("scenario registry must declare the initial-commit-r1 revision baseline")
    entries = registry.get("scenarios", [])
    if not isinstance(entries, list):
        return {}, errors + ["scenario registry scenarios must be an array"]
    expected_fields = {"id", "revision", "path", "sha256", "fixture_manifest_path", "fixture_manifest_sha256"}
    scenarios: dict[str, dict] = {}
    ids: list[str] = []
    registered_definitions: set[Path] = set()
    registered_manifests: set[Path] = set()
    registered_artifacts: set[Path] = set()
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != expected_fields:
            errors.append("scenario registry contains a malformed revision pin")
            continue
        sid, revision = entry.get("id"), entry.get("revision")
        ids.append(sid)
        if revision != 1:
            errors.append(f"scenario registry {sid} must begin at r1 before the initial commit")
        expected_path = f"scenarios/definitions/{sid}/r{revision}.json"
        expected_fixture = f"scenarios/fixtures/{sid}/r{revision}.fixture.json"
        if entry.get("path") != expected_path or entry.get("fixture_manifest_path") != expected_fixture:
            errors.append(f"scenario registry {sid} has non-revisioned or mismatched paths")
        path = ROOT / entry.get("path", "")
        manifest_path = ROOT / entry.get("fixture_manifest_path", "")
        registered_definitions.add(path.resolve())
        registered_manifests.add(manifest_path.resolve())
        if not path.is_file() or sha256_file(path) != entry.get("sha256"):
            errors.append(f"scenario registry {sid} definition missing or hash mismatch")
            continue
        if not manifest_path.is_file() or sha256_file(manifest_path) != entry.get("fixture_manifest_sha256"):
            errors.append(f"scenario registry {sid} manifest missing or hash mismatch")
        else:
            manifest = load_json(manifest_path)
            for ref in manifest.get("artifacts", {}).values() if isinstance(manifest.get("artifacts"), dict) else []:
                if isinstance(ref, dict) and isinstance(ref.get("path"), str):
                    registered_artifacts.add((ROOT / ref["path"]).resolve())
        scenario, scenario_errors = validate_current_scenario_v2(path, entry)
        scenario["_registry_entry"] = entry
        scenarios[sid] = scenario
        errors.extend(scenario_errors)
    if ids != sorted(ids) or len(ids) != len(set(ids)):
        errors.append("scenario registry IDs must be unique and sorted")
    definitions_on_disk = {item.resolve() for item in SCENARIO_DEFINITIONS.rglob("r*.json")}
    manifests_on_disk = {item.resolve() for item in SCENARIO_FIXTURES.rglob("r*.fixture.json")}
    artifacts_on_disk = {item.resolve() for item in SCENARIO_ARTIFACTS.rglob("*.json")}
    if registered_definitions != definitions_on_disk:
        errors.append(
            "scenario definition closure differs: "
            f"unregistered={sorted(str(item.relative_to(ROOT)) for item in definitions_on_disk - registered_definitions)}, "
            f"missing={sorted(str(item.relative_to(ROOT)) for item in registered_definitions - definitions_on_disk)}"
        )
    if registered_manifests != manifests_on_disk:
        errors.append(
            "scenario fixture-manifest closure differs: "
            f"unregistered={sorted(str(item.relative_to(ROOT)) for item in manifests_on_disk - registered_manifests)}, "
            f"missing={sorted(str(item.relative_to(ROOT)) for item in registered_manifests - manifests_on_disk)}"
        )
    if registered_artifacts != artifacts_on_disk:
        errors.append(
            "scenario artifact closure differs: "
            f"unregistered={sorted(str(item.relative_to(ROOT)) for item in artifacts_on_disk - registered_artifacts)}, "
            f"missing={sorted(str(item.relative_to(ROOT)) for item in registered_artifacts - artifacts_on_disk)}"
        )
    expected_artifact_count = len(entries) * 3
    if len(artifacts_on_disk) != expected_artifact_count:
        errors.append(
            f"scenario artifact set has {len(artifacts_on_disk)} files; expected exactly {expected_artifact_count}"
        )
    return scenarios, errors


def validate_packet_scenario_references_v2(path: Path, scenarios: dict[str, dict]) -> list[str]:
    errors: list[str] = []
    packet = load_json(path)
    packet_id = packet.get("id")
    references: list[tuple[str, object]] = []
    references += [("save_impact.golden_scenarios", sid) for sid in packet.get("save_impact", {}).get("golden_scenarios", [])]
    references += [(f"performance_metrics.{item.get('name')}", item.get("scenario")) for item in packet.get("performance_metrics", [])]
    references += [(f"rollout.health_signals.{item.get('name')}", item.get("scenario")) for item in packet.get("rollout", {}).get("health_signals", [])]
    for test in packet.get("acceptance_tests", []):
        scenario_id = test.get("scenario_id")
        if test.get("kind") in {"scenario", "save", "performance"} and not scenario_id:
            errors.append(f"{path.relative_to(ROOT)} {test.get('id')} must declare scenario_id")
        if scenario_id:
            references.append((f"acceptance_tests.{test.get('id')}", scenario_id))
            command_ids = re.findall(r"SCN_[A-Z0-9_]+", test.get("command") or "")
            if test.get("command") and command_ids != [scenario_id]:
                errors.append(f"{path.relative_to(ROOT)} {test.get('id')} command must reference only {scenario_id}")
    referenced_ids = {sid for _, sid in references if isinstance(sid, str)}
    for source, sid in references:
        scenario = scenarios.get(sid) if isinstance(sid, str) else None
        if scenario is None:
            errors.append(f"{path.relative_to(ROOT)} {source} references unknown scenario {sid!r}")
        elif packet_id not in scenario.get("applies_to_packets", []):
            errors.append(f"{path.relative_to(ROOT)} {source} uses {sid}, which does not apply to {packet_id}")
    pins = packet.get("scenario_pins", [])
    pin_ids = [pin.get("id") for pin in pins if isinstance(pin, dict)]
    if len(pin_ids) != len(set(pin_ids)) or set(pin_ids) != referenced_ids:
        errors.append(f"{path.relative_to(ROOT)} scenario pins must exactly equal references: pins={sorted(pin_ids)}, references={sorted(referenced_ids)}")
    for pin in pins:
        scenario = scenarios.get(pin.get("id")) if isinstance(pin, dict) else None
        if scenario is None:
            continue
        if pin != scenario.get("_registry_entry"):
            errors.append(f"{path.relative_to(ROOT)} has stale or incomplete pin for {pin.get('id')}")
    for signal in packet.get("rollout", {}).get("health_signals", []):
        if signal.get("name") == "required-test-failures":
            scenario = scenarios.get(signal.get("scenario"))
            if scenario and scenario.get("kind") != "aggregate":
                errors.append(f"{path.relative_to(ROOT)} required-test-failures must use an aggregate scenario")
            if scenario:
                parameters = {item.get("name"): item.get("value") for item in scenario.get("parameters", [])}
                required_count = sum(1 for test in packet.get("acceptance_tests", []) if test.get("required"))
                if parameters.get("required-acceptance-tests") != required_count:
                    errors.append(f"{path.relative_to(ROOT)} required test count differs from {signal.get('scenario')}")
                if parameters.get("minimum-runs") != packet.get("rollout", {}).get("minimum_runs"):
                    errors.append(f"{path.relative_to(ROOT)} minimum runs differs from aggregate scenario")
    for collection, prefix in ((packet.get("performance_metrics", []), "performance_metrics"), (packet.get("rollout", {}).get("health_signals", []), "rollout.health_signals")):
        for metric in collection:
            scenario = scenarios.get(metric.get("scenario"))
            if not scenario:
                continue
            matches = [item for item in scenario.get("oracles", []) if item.get("subject") == metric.get("name")]
            if len(matches) != 1:
                errors.append(f"{path.relative_to(ROOT)} {prefix}.{metric.get('name')} needs one matching oracle")
                continue
            oracle_value = matches[0]
            compatible = oracle_value.get("operator") == metric.get("comparator") or (oracle_value.get("operator") == "equal" and metric.get("comparator") in {"less-or-equal", "greater-or-equal"})
            if oracle_value.get("expected") != metric.get("target") or not compatible:
                errors.append(f"{path.relative_to(ROOT)} {prefix}.{metric.get('name')} differs from pinned oracle")
    return errors


def packet_contract_projection(packet: dict) -> dict:
    """Return immutable acceptance domain; lifecycle/evidence fields are excluded."""
    return {
        "contract_version": 1,
        "packet": {field: packet.get(field) for field in PACKET_CONTRACT_FIELDS},
    }


def packet_contract_sha256(packet: dict) -> str:
    return sha256_canonical_json(packet_contract_projection(packet))


def packet_path_is_covered(actual: str, declared: str) -> bool:
    if actual == declared:
        return True
    normalized = declared.rstrip("/")
    return declared.endswith("/") and actual.startswith(f"{normalized}/")


def artifact_is_content_addressed(artifact: object) -> bool:
    return (
        isinstance(artifact, dict)
        and isinstance(artifact.get("uri"), str)
        and artifact.get("uri")
        and not artifact["uri"].startswith("pending://")
        and isinstance(artifact.get("sha256"), str)
        and re.fullmatch(r"[0-9a-f]{64}", artifact["sha256"]) is not None
    )


def validate_status_event_chain(path: Path, packet: dict) -> list[str]:
    errors: list[str] = []
    label = str(path.relative_to(ROOT))
    events = packet.get("status_events", [])
    if not isinstance(events, list) or not events:
        return [f"{label} must retain a non-empty status event chain"]
    event_ids = [event.get("event_id") for event in events if isinstance(event, dict)]
    if len(event_ids) != len(events) or len(event_ids) != len(set(event_ids)):
        errors.append(f"{label} status event IDs must be present and globally unique within the packet")
    previous_to: str | None = None
    previous_at: datetime | None = None
    for index, event in enumerate(events):
        event_label = f"{label} status_events[{index}]"
        if not isinstance(event, dict):
            errors.append(f"{event_label} must be an object")
            continue
        from_status = event.get("from")
        to_status = event.get("to")
        at = parse_datetime(event.get("at"))
        if index == 0:
            if from_status is not None or to_status != "proposed":
                errors.append(f"{event_label} must be the null -> proposed genesis event")
        else:
            if from_status != previous_to:
                errors.append(
                    f"{event_label} from={from_status!r} breaks continuity after {previous_to!r}"
                )
            if from_status not in PACKET_TRANSITIONS or to_status not in PACKET_TRANSITIONS.get(from_status, set()):
                errors.append(f"{event_label} has forbidden transition {from_status!r} -> {to_status!r}")
        if to_status not in PACKET_STATUSES:
            errors.append(f"{event_label} has unknown destination status {to_status!r}")
        if at is None:
            errors.append(f"{event_label} has an invalid timezone-aware timestamp")
        elif previous_at is not None and at <= previous_at:
            errors.append(f"{event_label} is not strictly later than the preceding event")
        previous_to = to_status
        if at is not None:
            previous_at = at
    if previous_to != packet.get("status"):
        errors.append(f"{label} final status event does not equal packet status")
    activation_events = [
        event for event in events
        if isinstance(event, dict) and event.get("to") == "active"
    ]
    if len(activation_events) > 1:
        errors.append(f"{label} has multiple A1 activation events; open a new packet instead")
    return errors


def validate_work_packet_semantics(path: Path) -> list[str]:
    errors: list[str] = []
    packet = load_json(path)
    errors.extend(validate_packet_system_contracts(path, packet))
    calculated_contract = packet_contract_sha256(packet)
    if packet.get("contract_sha256") != calculated_contract:
        errors.append(
            f"{path.relative_to(ROOT)} contract_sha256 does not match canonical packet-contract-v1: "
            f"expected {calculated_contract}"
        )
    rank = {"low": 0, "medium": 1, "high": 2, "constitutional": 3}
    declared = packet.get("declared_risk")
    derived = packet.get("derived_risk")
    effective = packet.get("effective_risk")
    if derived is not None and effective != max((declared, derived), key=lambda item: rank[item]):
        errors.append(f"{path.relative_to(ROOT)} effective risk is not max(declared, derived)")
    if packet.get("class") == "architecture" and rank.get(effective, -1) < rank["high"]:
        errors.append(f"{path.relative_to(ROOT)} architecture work derives at least high risk")
    if {"governance", "save"} & set(packet.get("affected_domains", [])) and rank.get(effective, -1) < rank["high"]:
        errors.append(f"{path.relative_to(ROOT)} governance/save work derives at least high risk")
    principals = [packet.get(field) for field in ("implementer", "verifier", "integrator") if packet.get(field)]
    if len(principals) != len(set(principals)):
        errors.append(f"{path.relative_to(ROOT)} reuses a principal across implementation/verification/integration")
    if packet.get("status") not in {"proposed", "rejected", "superseded"}:
        if not packet.get("approved_by") or not packet.get("approval_receipt_id"):
            errors.append(f"{path.relative_to(ROOT)} advanced without approval receipt")
    if packet.get("status") in {"candidate", "released"}:
        if packet.get("verification", {}).get("status") != "passed" or not packet.get("verifier"):
            errors.append(f"{path.relative_to(ROOT)} candidate/release lacks independent passing verification")
    if packet.get("status") == "released" and not (packet.get("release_id") and packet.get("integrator")):
        errors.append(f"{path.relative_to(ROOT)} released without release ID/integrator")
    errors.extend(validate_status_event_chain(path, packet))
    test_ids = [test.get("id") for test in packet.get("acceptance_tests", [])]
    if len(test_ids) != len(set(test_ids)):
        errors.append(f"{path.relative_to(ROOT)} acceptance test IDs are not unique")
    if packet.get("status") not in {"proposed", "rejected", "superseded"}:
        for test in packet.get("acceptance_tests", []):
            if test.get("required") and not test.get("command") and test.get("kind") != "manual":
                errors.append(f"{path.relative_to(ROOT)} accepted required test lacks a command: {test.get('id')}")
    declared_paths = packet.get("declared_paths", [])
    actual_paths = packet.get("actual_paths", [])
    reserved_paths = packet.get("reservation", {}).get("paths", [])
    for candidate_path in declared_paths + actual_paths + reserved_paths:
        if candidate_path.startswith("/") or ".." in Path(candidate_path).parts:
            errors.append(f"{path.relative_to(ROOT)} has unsafe path: {candidate_path}")
    for actual_path in actual_paths:
        if not any(packet_path_is_covered(actual_path, declared) for declared in declared_paths):
            errors.append(
                f"{path.relative_to(ROOT)} actual path is outside declared scope: {actual_path}"
            )
        if reserved_paths and not any(
            packet_path_is_covered(actual_path, reserved) for reserved in reserved_paths
        ):
            errors.append(
                f"{path.relative_to(ROOT)} actual path is outside its reservation: {actual_path}"
            )

    evidence = packet.get("evidence_manifest", [])
    evidence_by_id = {
        item.get("id"): item for item in evidence if isinstance(item, dict)
    }
    if len(evidence_by_id) != len(evidence):
        errors.append(f"{path.relative_to(ROOT)} evidence manifest IDs must be unique")
    if packet.get("status") in {"verifying", "candidate"}:
        if not actual_paths:
            errors.append(f"{path.relative_to(ROOT)} verifying/candidate packet has no actual paths")
        candidate = packet.get("candidate_evidence", {})
        reference_fields = (
            "diff_artifact_id", "artifact_manifest_id", "command_log_artifact_id"
        )
        references = [candidate.get(field) for field in reference_fields]
        if any(not isinstance(item, str) or not item for item in references):
            errors.append(
                f"{path.relative_to(ROOT)} verifying/candidate packet lacks complete diff/artifact/command evidence references"
            )
        elif len(references) != len(set(references)):
            errors.append(f"{path.relative_to(ROOT)} candidate evidence references must be distinct")
        for evidence_id in references:
            artifact = evidence_by_id.get(evidence_id)
            if artifact is None:
                errors.append(
                    f"{path.relative_to(ROOT)} candidate evidence references unknown artifact {evidence_id!r}"
                )
            elif not artifact_is_content_addressed(artifact):
                errors.append(
                    f"{path.relative_to(ROOT)} candidate artifact {evidence_id!r} is not content-addressed"
                )
    return errors


def supersession_authority_codes(
    predecessor: dict,
    successor: dict,
    receipt: dict | None,
) -> list[str]:
    """Return stable policy codes for an invalid supersession edge."""
    codes: list[str] = []
    predecessor_is_creator = predecessor.get("authority") in CREATOR_AUTHORITIES
    successor_is_creator = successor.get("authority") in CREATOR_AUTHORITIES
    if predecessor_is_creator and not successor_is_creator:
        codes.append("lower-authority-cannot-supersede-creator-authority")
    if predecessor.get("class") == "constitutional" and predecessor_is_creator:
        if successor.get("authority") != "creator-ratification":
            codes.append("constitutional-successor-requires-exact-creator-ratification-authority")
        protected = (
            isinstance(receipt, dict)
            and receipt.get("receipt_id") == successor.get("approval_receipt_id")
            and receipt.get("issuer_role") == "creator"
            and receipt.get("receipt_kind") == "decision-ratification"
            and receipt.get("sealed") is True
            and bool(receipt.get("accepted_commit"))
            and successor.get("id") in receipt.get("subject_ids", [])
            and receipt.get("subject_event_sha256", {}).get(successor.get("id"))
                == successor.get("event_hash")
        )
        if not protected:
            codes.append(
                "creator-constitutional-supersession-requires-protected-receipt"
            )
    return codes


def validate_supersession_graph(records: list[dict]) -> list[str]:
    """Enforce one global, acyclic, non-forked successor chain per decision root."""
    errors: list[str] = []
    records_by_id = {record.get("id"): record for record in records}
    children: dict[str, list[str]] = {}
    for record in records:
        predecessor = record.get("supersedes")
        if predecessor:
            children.setdefault(predecessor, []).append(record.get("id"))
    for predecessor, successors in sorted(children.items()):
        if len(successors) != 1:
            errors.append(f"decision supersession forks at {predecessor}: {successors}")
    for start in records_by_id:
        visited: set[str] = set()
        current = start
        while current in children and len(children[current]) == 1:
            if current in visited:
                errors.append(f"decision supersession cycle reaches {current}")
                break
            visited.add(current)
            current = children[current][0]
    heads = set(records_by_id) - set(children)
    expected_heads = {
        record_id for record_id in records_by_id
        if not any(record.get("supersedes") == record_id for record in records)
    }
    if heads != expected_heads:
        errors.append("decision supersession head derivation is inconsistent")
    return errors


def validate_supersession_authority(
    records: list[dict], receipts: list[dict]
) -> list[str]:
    errors: list[str] = []
    records_by_id = {record["id"]: record for record in records}
    receipts_by_id = {receipt["receipt_id"]: receipt for receipt in receipts}
    for successor in records:
        predecessor_id = successor.get("supersedes")
        if not predecessor_id or predecessor_id not in records_by_id:
            continue
        predecessor = records_by_id[predecessor_id]
        receipt = receipts_by_id.get(successor.get("approval_receipt_id"))
        for code in supersession_authority_codes(predecessor, successor, receipt):
            errors.append(
                f"{successor['id']} cannot supersede {predecessor_id}: {code}"
            )
    return errors


def validate_supersession_fixtures() -> list[str]:
    errors: list[str] = []
    seen_case_ids: set[str] = set()
    fixture_dir = ROOT / "governance" / "fixtures"
    fixture_paths = sorted(fixture_dir.glob("supersession-authority.*.json"))
    if len(fixture_paths) != 2:
        errors.append("supersession authority fixtures must include one valid and one invalid file")
    for fixture_path in fixture_paths:
        fixture = load_json(fixture_path)
        cases = fixture.get("cases")
        if not isinstance(cases, list) or not cases:
            errors.append(f"{fixture_path.relative_to(ROOT)} must contain cases")
            continue
        for case in cases:
            if not isinstance(case, dict):
                errors.append(f"{fixture_path.relative_to(ROOT)} has a non-object case")
                continue
            case_id = case.get("id")
            if not isinstance(case_id, str) or not case_id:
                errors.append(f"{fixture_path.relative_to(ROOT)} has a case without an ID")
                continue
            if case_id in seen_case_ids:
                errors.append(f"duplicate supersession fixture case ID: {case_id}")
            seen_case_ids.add(case_id)
            codes = supersession_authority_codes(
                case.get("predecessor", {}),
                case.get("successor", {}),
                case.get("receipt"),
            )
            expected_valid = case.get("expected_valid")
            if expected_valid is True and codes:
                errors.append(f"{case_id} expected valid but produced {codes}")
            elif expected_valid is False:
                expected_error = case.get("expected_error")
                if expected_error not in codes:
                    errors.append(
                        f"{case_id} expected {expected_error!r} but produced {codes}"
                    )
            else:
                if expected_valid not in {True, False}:
                    errors.append(f"{case_id} expected_valid must be boolean")
    return errors


def parse_datetime(value: object) -> datetime | None:
    if not isinstance(value, str) or not value:
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return None
    return parsed.astimezone(timezone.utc)


def subject_claims(receipt: dict) -> dict[str, set[str]]:
    return {
        item.get("subject_id"): set(item.get("claims", []))
        for item in receipt.get("subject_claims", [])
        if isinstance(item, dict)
        and isinstance(item.get("subject_id"), str)
        and isinstance(item.get("claims"), list)
    }


def validate_a1_boundary_manifest(
    packet: dict,
    state: dict,
    activation_receipt: dict | None,
    receipts_by_id: dict[str, dict],
) -> tuple[dict | None, list[str]]:
    errors: list[str] = []
    packet_id = packet.get("id", "unknown-packet")
    reference = packet.get("a1_boundary_manifest")
    if not isinstance(reference, dict):
        return None, [f"{packet_id} lacks a canonical A1 boundary-manifest reference"]
    expected_path = f"governance/a1-boundaries/{packet_id}.json"
    if reference.get("path") != expected_path:
        errors.append(f"{packet_id} boundary manifest must use {expected_path}")
    manifest_path, path_error = safe_foundation_path(
        reference.get("path"), f"{packet_id} boundary manifest path"
    )
    if path_error:
        return None, [path_error]
    if manifest_path is None or not manifest_path.is_file():
        return None, errors + [f"{packet_id} boundary manifest is missing"]
    actual_hash = sha256_file(manifest_path)
    if reference.get("sha256") != actual_hash:
        errors.append(f"{packet_id} boundary manifest raw hash mismatch")
    errors.extend(validate_instance_shape(manifest_path, A1_BOUNDARY_SCHEMA))
    manifest = load_json(manifest_path)
    if manifest.get("manifest_id") != reference.get("manifest_id"):
        errors.append(f"{packet_id} boundary manifest ID differs from packet reference")
    if manifest.get("packet_id") != packet_id:
        errors.append(f"{packet_id} boundary manifest binds another packet")
    if manifest.get("packet_contract_sha256") != packet.get("contract_sha256"):
        errors.append(f"{packet_id} boundary manifest binds the wrong packet contract")

    reservation = packet.get("reservation", {})
    if manifest.get("repository", {}).get("base_commit") != reservation.get("base_commit"):
        errors.append(f"{packet_id} boundary manifest base commit differs from reservation")
    expected_reservation = {
        "lease_id": reservation.get("lease_id"),
        "fencing_token": reservation.get("fencing_token"),
        "expires_at": reservation.get("expires_at"),
        "paths": reservation.get("paths"),
        "domains": reservation.get("domains"),
    }
    if manifest.get("reservation") != expected_reservation:
        errors.append(f"{packet_id} boundary manifest does not exactly bind its reservation")

    expected_foundation = {
        "constitution_path": "00-GAME-CONSTITUTION.md",
        "constitution_sha256": state.get("constitution_sha256"),
        "decision_ledger_path": "ledger/decisions.jsonl",
        "decision_ledger_sha256": state.get("decision_ledger_sha256"),
        "last_creator_receipt_id": state.get("last_creator_receipt_id"),
    }
    if manifest.get("foundation_binding") != expected_foundation:
        errors.append(f"{packet_id} boundary manifest does not bind current foundation authority")

    protection = manifest.get("protection_boundary", {})
    if set(protection.get("writable_paths", [])) != set(reservation.get("paths", [])):
        errors.append(f"{packet_id} boundary writable paths differ from its exact reservation")
    protected_required = {
        "docs/foundation-v0.1/00-GAME-CONSTITUTION.md",
        "docs/foundation-v0.1/ledger/decisions.jsonl",
        "docs/foundation-v0.1/governance/",
        "docs/foundation-v0.1/ledger/receipts/",
        ".git/refs/heads/main",
    }
    if not protected_required.issubset(set(protection.get("protected_paths", []))):
        errors.append(f"{packet_id} boundary omits a required protected path")
    denied_required = {
        "protected-main-write", "governance-write", "receipt-write",
        "merge", "release",
    }
    denied = set(manifest.get("credential_boundary", {}).get("denied_capabilities", []))
    if not denied_required.issubset(denied):
        errors.append(f"{packet_id} boundary credential denial is incomplete")

    if not isinstance(activation_receipt, dict):
        errors.append(f"{packet_id} boundary has no activation receipt")
    else:
        if manifest.get("attestation_receipt_id") != activation_receipt.get("receipt_id"):
            errors.append(f"{packet_id} manifest names a different attestation receipt")
        if manifest.get("attested_by") != activation_receipt.get("issued_by"):
            errors.append(f"{packet_id} manifest attestor differs from receipt issuer")
        if activation_receipt.get("artifact_sha256", {}).get(reference.get("path")) != actual_hash:
            errors.append(f"{packet_id} activation receipt does not bind exact boundary-manifest bytes")
        expected_receipt_foundation = {
            "constitution_sha256": state.get("constitution_sha256"),
            "decision_ledger_sha256": state.get("decision_ledger_sha256"),
            "last_creator_receipt_id": state.get("last_creator_receipt_id"),
        }
        if activation_receipt.get("foundation_binding") != expected_receipt_foundation:
            errors.append(f"{packet_id} activation receipt lacks the exact foundation binding")
        if activation_receipt.get("subject_contract_sha256", {}).get(packet_id) != packet.get("contract_sha256"):
            errors.append(f"{packet_id} activation receipt binds the wrong packet contract")

    last_creator_receipt = receipts_by_id.get(state.get("last_creator_receipt_id"))
    if not last_creator_receipt or not last_creator_receipt.get("sealed") or last_creator_receipt.get("issuer_role") != "creator":
        errors.append(f"{packet_id} state does not name a sealed prior creator receipt")
    return manifest, errors


def validate_packet_approval_and_events(
    packet: dict,
    receipts_by_id: dict[str, dict],
    activation_receipt_id: str | None,
) -> list[str]:
    errors: list[str] = []
    packet_id = packet.get("id", "unknown-packet")
    status = packet.get("status")
    acceptance_receipt = receipts_by_id.get(packet.get("approval_receipt_id"))
    if status not in {"proposed", "rejected", "superseded"}:
        if not acceptance_receipt or not acceptance_receipt.get("sealed"):
            errors.append(f"{packet_id} lacks a sealed packet-acceptance receipt")
        else:
            if acceptance_receipt.get("receipt_kind") != "packet-acceptance":
                errors.append(f"{packet_id} approval receipt has the wrong receipt kind")
            if acceptance_receipt.get("issuer_role") != packet.get("required_approver"):
                errors.append(f"{packet_id} required approver role differs from receipt issuer role")
            if packet.get("approved_by") != acceptance_receipt.get("issued_by"):
                errors.append(f"{packet_id} approved_by differs from approval receipt issuer")
            if acceptance_receipt.get("subject_contract_sha256", {}).get(packet_id) != packet.get("contract_sha256"):
                errors.append(f"{packet_id} acceptance receipt does not bind immutable packet contract")
            required_claim = f"ACCEPT-{packet_id}"
            if required_claim not in subject_claims(acceptance_receipt).get(packet_id, set()):
                errors.append(f"{packet_id} acceptance receipt lacks {required_claim}")

    events = packet.get("status_events", [])
    acceptance_events = [event for event in events if event.get("to") == "accepted"]
    if status not in {"proposed", "rejected", "superseded"}:
        if len(acceptance_events) != 1 or acceptance_events[0].get("receipt_id") != packet.get("approval_receipt_id"):
            errors.append(f"{packet_id} must retain one acceptance event with its approval receipt")
    activation_events = [event for event in events if event.get("to") == "active"]
    if activation_events:
        if len(activation_events) != 1 or activation_events[0].get("receipt_id") != activation_receipt_id:
            errors.append(f"{packet_id} activation history does not retain its sole activation receipt")
        activation_receipt = receipts_by_id.get(activation_receipt_id)
        if not activation_receipt or not activation_receipt.get("sealed") or activation_receipt.get("receipt_kind") != "packet-activation":
            errors.append(f"{packet_id} retained activation evidence is missing, unsealed, or wrong-kind")
        for event in events:
            if event.get("to") in {"active", "verifying", "candidate"} and event.get("receipt_id") != activation_receipt_id:
                errors.append(f"{packet_id} active-lifecycle event {event.get('event_id')} loses activation receipt continuity")
        if not isinstance(packet.get("a1_boundary_manifest"), dict):
            errors.append(f"{packet_id} dropped its boundary manifest after activation")
    release_events = [event for event in events if event.get("to") == "released"]
    if packet.get("status") in {"released", "rolled-back"}:
        if len(release_events) != 1:
            errors.append(f"{packet_id} must retain exactly one release event")
        else:
            completion_receipt = receipts_by_id.get(release_events[0].get("receipt_id"))
            required_claim = f"ACCEPT-COMPLETION-{packet_id}"
            if (
                not completion_receipt
                or not completion_receipt.get("sealed")
                or completion_receipt.get("receipt_kind") != "packet-completion"
                or completion_receipt.get("issuer_role") != "creator"
            ):
                errors.append(f"{packet_id} release event lacks sealed creator completion evidence")
            else:
                if required_claim not in subject_claims(completion_receipt).get(packet_id, set()):
                    errors.append(f"{packet_id} completion receipt lacks {required_claim}")
                if completion_receipt.get("subject_contract_sha256", {}).get(packet_id) != packet.get("contract_sha256"):
                    errors.append(f"{packet_id} completion receipt binds the wrong packet contract")
    return errors


def validate_packet_dependencies(
    packets: list[dict],
    state: dict,
    receipts_by_id: dict[str, dict],
) -> list[str]:
    errors: list[str] = []
    packets_by_id = {packet.get("id"): packet for packet in packets}
    requirement_map = state.get("packet_dependency_release_requirements", {})
    if set(requirement_map) != set(packets_by_id):
        errors.append("dependency-release state must name every and only canonical work packet")
    for packet_id, packet in packets_by_id.items():
        dependencies = packet.get("dependencies", [])
        for dependency_id in dependencies:
            if dependency_id not in packets_by_id:
                errors.append(f"{packet_id} depends on unknown packet {dependency_id}")
            if dependency_id == packet_id:
                errors.append(f"{packet_id} depends on itself")
        requirements = requirement_map.get(packet_id, [])
        requirement_ids = [item.get("dependency_id") for item in requirements if isinstance(item, dict)]
        if len(requirement_ids) != len(set(requirement_ids)) or set(requirement_ids) != set(dependencies):
            errors.append(f"{packet_id} dependency receipt requirements do not exactly match dependencies")
        if packet.get("status") in {"proposed", "rejected", "superseded"}:
            continue
        for requirement in requirements:
            dependency_id = requirement.get("dependency_id")
            dependency = packets_by_id.get(dependency_id)
            if dependency is None:
                continue
            if dependency.get("status") != "released":
                errors.append(f"{packet_id} is blocked until {dependency_id} is released")
            receipt = receipts_by_id.get(requirement.get("completion_receipt_id"))
            if not receipt or not receipt.get("sealed"):
                errors.append(f"{packet_id} lacks sealed creator-accepted completion evidence for {dependency_id}")
                continue
            if receipt.get("receipt_kind") != "packet-completion" or receipt.get("issuer_role") != "creator":
                errors.append(f"{packet_id} dependency completion receipt has wrong kind/authority")
            missing = set(requirement.get("required_claims", [])) - subject_claims(receipt).get(dependency_id, set())
            if missing:
                errors.append(f"{packet_id} dependency completion receipt lacks {sorted(missing)}")
            if receipt.get("subject_contract_sha256", {}).get(dependency_id) != dependency.get("contract_sha256"):
                errors.append(f"{packet_id} dependency completion receipt binds the wrong contract")

    visiting: set[str] = set()
    visited: set[str] = set()

    def visit(packet_id: str) -> None:
        if packet_id in visiting:
            errors.append(f"work-packet dependency cycle reaches {packet_id}")
            return
        if packet_id in visited:
            return
        visiting.add(packet_id)
        for dependency_id in packets_by_id[packet_id].get("dependencies", []):
            if dependency_id in packets_by_id:
                visit(dependency_id)
        visiting.remove(packet_id)
        visited.add(packet_id)

    for packet_id in sorted(packets_by_id):
        visit(packet_id)
    return errors


def main() -> int:
    for schema_path in sorted((ROOT / "schemas").glob("*.json")):
        load_json(schema_path)

    records, errors = validate_decisions()
    receipts, receipt_errors = validate_receipts(records)
    errors.extend(receipt_errors)
    errors.extend(validate_supersession_graph(records))
    errors.extend(validate_supersession_authority(records, receipts))
    errors.extend(validate_supersession_fixtures())
    errors.extend(validate_local_links())
    errors.extend(validate_references(records))
    scenarios, scenario_errors = validate_scenario_registry_v2()
    errors.extend(scenario_errors)
    errors.extend(validate_markdown_scenario_references(scenarios))
    packet_paths = sorted((ROOT / "work-packets").rglob("*.json"))
    packets: list[dict] = []
    for packet_path in packet_paths:
        errors.extend(validate_instance_shape(packet_path, ROOT / "schemas" / "work-packet.schema.json"))
        errors.extend(validate_work_packet_semantics(packet_path))
        errors.extend(validate_packet_scenario_references_v2(packet_path, scenarios))
        packets.append(load_json(packet_path))
    ratification_state = ROOT / "governance" / "ratification-state.json"
    errors.extend(
        validate_instance_shape(
            ratification_state,
            ROOT / "schemas" / "ratification-state.schema.json",
        )
    )
    state = load_json(ratification_state)
    receipts_by_id = {receipt["receipt_id"]: receipt for receipt in receipts}
    errors.extend(validate_packet_dependencies(packets, state, receipts_by_id))
    all_event_ids = [
        event.get("event_id")
        for packet in packets
        for event in packet.get("status_events", [])
        if isinstance(event, dict)
    ]
    if len(all_event_ids) != len(set(all_event_ids)):
        errors.append("work-packet status event IDs must be globally unique")
    a1_limit = state.get("a1_max_active_packets")
    if a1_limit != 1:
        errors.append("ratification state a1_max_active_packets must equal 1")
    active_a1_packets: list[str] = []
    for packet in packets:
        if (
            packet.get("rollout", {}).get("required_autonomy") == "A1"
            and packet.get("status") in {"active", "verifying", "candidate"}
        ):
            active_a1_packets.append(packet.get("id", "unknown-packet"))
    effective_a1_limit = (
        a1_limit
        if isinstance(a1_limit, int) and not isinstance(a1_limit, bool)
        else 0
    )
    if len(active_a1_packets) > effective_a1_limit:
        errors.append(
            f"active A1 packets exceed a1_max_active_packets: {active_a1_packets}"
        )
    if active_a1_packets and state.get("active_autonomy") != "A1":
        errors.append(
            "A1 packets are active while active_autonomy is "
            f"{state.get('active_autonomy')!r}: {active_a1_packets}"
        )
    if state.get("active_autonomy") == "A1" and len(active_a1_packets) != 1:
        errors.append(
            "active_autonomy A1 requires exactly one active A1 packet, found "
            f"{active_a1_packets}"
        )
    if state.get("active_autonomy") == "A1":
        if (
            len(active_a1_packets) != 1
            or state.get("active_a1_packet_id") != active_a1_packets[0]
        ):
            errors.append("active_a1_packet_id must name the sole active A1 packet")
        constitution_hash = sha256_file(ROOT / "00-GAME-CONSTITUTION.md")
        ledger_hash = sha256_file(DECISIONS)
        if state.get("constitution_sha256") != constitution_hash:
            errors.append("A1 state constitution_sha256 does not match current constitution bytes")
        if state.get("decision_ledger_sha256") != ledger_hash:
            errors.append("A1 state decision_ledger_sha256 does not match current ledger bytes")
    elif state.get("active_a1_packet_id") is not None:
        errors.append("active_a1_packet_id must be null outside A1")
    known_decisions = {record["id"] for record in records}
    records_by_id = {record["id"]: record for record in records}
    superseders: dict[str, list[str]] = {}
    for record in records:
        if record.get("supersedes"):
            superseders.setdefault(record["supersedes"], []).append(record["id"])

    def active_decision_head(root_id: str) -> dict | None:
        current = root_id
        visited: set[str] = set()
        while current in superseders:
            if current in visited:
                errors.append(f"decision supersession cycle reaches {current}")
                return None
            visited.add(current)
            children = superseders[current]
            if len(children) != 1:
                errors.append(f"decision lineage {root_id} forks at {current}: {children}")
                return None
            current = children[0]
        return records_by_id.get(current)

    gate_resolutions: dict[str, bool] = {}
    for gate_name, gate in state.get("entry_gates", {}).items():
        gate_resolved = True
        decision_ids: list[str] = []
        for requirement in gate.get("decision_requirements", []):
            decision_id = requirement.get("decision_id")
            decision_ids.append(decision_id)
            if decision_id not in known_decisions:
                errors.append(f"ratification state {gate_name} references unknown {decision_id}")
                gate_resolved = False
                continue
            head = active_decision_head(decision_id)
            if head is None:
                gate_resolved = False
                continue
            if head.get("status") not in requirement.get("accepted_statuses", []):
                gate_resolved = False
            receipt_id = requirement.get("receipt_id")
            if receipt_id is None:
                gate_resolved = False
                continue
            receipt = receipts_by_id.get(receipt_id)
            if receipt is None:
                errors.append(f"ratification state {gate_name} decision {decision_id} references unknown receipt {receipt_id}")
                gate_resolved = False
                continue
            if head["id"] not in receipt.get("subject_ids", []):
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} does not bind active head {head['id']}"
                )
                gate_resolved = False
            if head.get("approval_receipt_id") != receipt_id:
                errors.append(
                    f"ratification state {gate_name} active head {head['id']} approval receipt "
                    f"{head.get('approval_receipt_id')!r} does not equal {receipt_id}"
                )
                gate_resolved = False
            bindings = subject_claims(receipt)
            matching_claims = set(requirement.get("allowed_claims", [])) & bindings.get(head["id"], set())
            if len(matching_claims) != 1:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} must bind exactly one allowed claim "
                    f"to {head['id']}, found {sorted(matching_claims)}"
                )
                gate_resolved = False
            if not receipt.get("sealed"):
                gate_resolved = False
        if len(decision_ids) != len(set(decision_ids)):
            errors.append(f"ratification state {gate_name} repeats a decision requirement")
        receipt_purposes: list[str] = []
        for requirement in gate.get("receipt_requirements", []):
            receipt_purposes.append(requirement.get("purpose"))
            receipt_id = requirement.get("receipt_id")
            if receipt_id is None:
                gate_resolved = False
                continue
            receipt = receipts_by_id.get(receipt_id)
            if receipt is None:
                errors.append(f"ratification state {gate_name} references unknown receipt {receipt_id}")
                gate_resolved = False
                continue
            missing_subjects = set(requirement.get("subject_ids", [])) - set(receipt.get("subject_ids", []))
            if missing_subjects:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} lacks subjects {sorted(missing_subjects)}"
                )
                gate_resolved = False
            bindings = subject_claims(receipt)
            for subject_id in requirement.get("subject_ids", []):
                missing_claims = set(requirement.get("required_claims", [])) - bindings.get(subject_id, set())
                if missing_claims:
                    errors.append(
                        f"ratification state {gate_name} receipt {receipt_id} lacks claims "
                        f"{sorted(missing_claims)} for {subject_id}"
                    )
                    gate_resolved = False
            required_kind = requirement.get("required_receipt_kind")
            if required_kind is not None and receipt.get("receipt_kind") != required_kind:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} has kind "
                    f"{receipt.get('receipt_kind')!r}, expected {required_kind!r}"
                )
                gate_resolved = False
            required_role = requirement.get("required_issuer_role")
            if required_role is not None and receipt.get("issuer_role") != required_role:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} has issuer role "
                    f"{receipt.get('issuer_role')!r}, expected {required_role!r}"
                )
                gate_resolved = False
            required_resolver = requirement.get("required_resolver_type")
            resolver = receipt.get("artifact_resolver")
            actual_resolver = resolver.get("type") if isinstance(resolver, dict) else None
            if required_resolver is not None and actual_resolver != required_resolver:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} has resolver "
                    f"{actual_resolver!r}, expected {required_resolver!r}"
                )
                gate_resolved = False
            required_contracts = requirement.get("required_subject_contract_sha256", {})
            if not isinstance(required_contracts, dict):
                errors.append(
                    f"ratification state {gate_name} receipt requirement has malformed contract bindings"
                )
                gate_resolved = False
            else:
                undeclared_contract_subjects = set(required_contracts) - set(requirement.get("subject_ids", []))
                if undeclared_contract_subjects:
                    errors.append(
                        f"ratification state {gate_name} receipt requirement binds undeclared contract "
                        f"subjects {sorted(undeclared_contract_subjects)}"
                    )
                    gate_resolved = False
                actual_contracts = receipt.get("subject_contract_sha256")
                if not isinstance(actual_contracts, dict):
                    actual_contracts = {}
                for subject_id, expected_hash in required_contracts.items():
                    actual_hash = actual_contracts.get(subject_id)
                    if actual_hash != expected_hash:
                        errors.append(
                            f"ratification state {gate_name} receipt {receipt_id} binds contract "
                            f"{actual_hash!r} for {subject_id}, expected {expected_hash!r}"
                        )
                        gate_resolved = False
            if not receipt.get("sealed"):
                gate_resolved = False
        if len(receipt_purposes) != len(set(receipt_purposes)):
            errors.append(f"ratification state {gate_name} repeats a receipt purpose")
        if gate.get("status") in {"ready", "passed"} and not gate_resolved:
            errors.append(f"ratification state {gate_name} is {gate.get('status')} with unresolved requirements")
        gate_resolutions[gate_name] = gate_resolved

    packet_ids = [packet.get("id") for packet in packets]
    if len(packet_ids) != len(set(packet_ids)):
        errors.append("work packet IDs are not unique")
    packets_by_id = {
        packet["id"]: packet
        for packet in packets
        if isinstance(packet.get("id"), str)
    }
    a1_packet_ids = {
        packet["id"]
        for packet in packets
        if isinstance(packet.get("id"), str)
        and packet.get("rollout", {}).get("required_autonomy") == "A1"
    }
    packet_entry_gates = state.get("packet_entry_gates")
    entry_gates = state.get("entry_gates", {})
    if not isinstance(packet_entry_gates, dict):
        errors.append("ratification state packet_entry_gates must be an object")
        packet_entry_gates = {}
    for packet_id in sorted(a1_packet_ids):
        mapped_gate = packet_entry_gates.get(packet_id)
        if not isinstance(mapped_gate, str):
            errors.append(f"A1 packet {packet_id} lacks exactly one entry-gate mapping")
        elif mapped_gate not in entry_gates:
            errors.append(f"A1 packet {packet_id} maps to unknown gate {mapped_gate!r}")
    for packet_id in sorted(set(packet_entry_gates) - a1_packet_ids):
        if packet_id not in packets_by_id:
            errors.append(f"packet entry-gate mapping references unknown packet {packet_id}")
        else:
            errors.append(f"non-A1 packet {packet_id} has an A1 entry-gate mapping")

    advanced_statuses = {
        "accepted",
        "active",
        "verifying",
        "candidate",
        "released",
        "rolled-back",
    }
    active_statuses = {"active", "verifying", "candidate"}
    for packet_id in sorted(a1_packet_ids):
        packet = packets_by_id[packet_id]
        mapped_gate = packet_entry_gates.get(packet_id)
        gate = entry_gates.get(mapped_gate, {}) if isinstance(mapped_gate, str) else {}
        receipt_requirements = gate.get("receipt_requirements", [])
        accept_purpose = f"accept-{packet_id}"
        accept_requirements = [
            requirement
            for requirement in receipt_requirements
            if requirement.get("purpose") == accept_purpose
        ]
        if len(accept_requirements) != 1:
            errors.append(
                f"mapped gate for {packet_id} must declare exactly one {accept_purpose} receipt"
            )
            accept_requirement: dict = {}
        else:
            accept_requirement = accept_requirements[0]

        activation_claim = f"ACTIVATE-A1-{packet_id}"
        quarantine_claims = {
            "A1-QUARANTINE-BOUNDARY-VERIFIED",
            activation_claim,
        }
        quarantine_requirements = [
            requirement
            for requirement in receipt_requirements
            if activation_claim in requirement.get("required_claims", [])
        ]
        if len(quarantine_requirements) != 1:
            errors.append(
                f"mapped gate for {packet_id} must declare exactly one packet activation receipt"
            )
            quarantine_requirement: dict = {}
        else:
            quarantine_requirement = quarantine_requirements[0]
            if set(quarantine_requirement.get("subject_ids", [])) != {packet_id}:
                errors.append(
                    f"{packet_id} quarantine receipt must bind only that packet"
                )
            if not quarantine_claims.issubset(
                set(quarantine_requirement.get("required_claims", []))
            ):
                errors.append(
                    f"{packet_id} quarantine receipt declaration lacks both activation claims"
                )

        quarantine_receipt_id = quarantine_requirement.get("receipt_id")
        errors.extend(
            validate_packet_approval_and_events(
                packet,
                receipts_by_id,
                quarantine_receipt_id,
            )
        )
        boundary_manifest: dict | None = None
        activation_events = [
            event for event in packet.get("status_events", [])
            if isinstance(event, dict) and event.get("to") == "active"
        ]
        if activation_events:
            retained_activation_id = activation_events[0].get("receipt_id")
            retained_activation_receipt = receipts_by_id.get(retained_activation_id)
            boundary_manifest, boundary_errors = validate_a1_boundary_manifest(
                packet,
                state,
                retained_activation_receipt,
                receipts_by_id,
            )
            errors.extend(boundary_errors)

        if packet.get("status") in advanced_statuses:
            acceptance_receipt_id = accept_requirement.get("receipt_id")
            if packet.get("approval_receipt_id") != acceptance_receipt_id:
                errors.append(
                    f"{packet_id} approval receipt must equal its mapped ACCEPT-WP receipt"
                )
            acceptance_receipt = receipts_by_id.get(acceptance_receipt_id)
            if not acceptance_receipt or not acceptance_receipt.get("sealed"):
                errors.append(f"{packet_id} acceptance receipt is missing or unsealed")
            else:
                missing_acceptance_claims = set(
                    accept_requirement.get("required_claims", [])
                ) - subject_claims(acceptance_receipt).get(packet_id, set())
                if missing_acceptance_claims:
                    errors.append(
                        f"{packet_id} acceptance receipt lacks claims "
                        f"{sorted(missing_acceptance_claims)}"
                    )

        if packet.get("status") not in active_statuses:
            continue
        if gate.get("status") != "passed" or not gate_resolutions.get(mapped_gate, False):
            errors.append(
                f"active A1 packet {packet_id} requires a passed, fully resolved mapped gate"
            )
        quarantine_receipt = receipts_by_id.get(quarantine_receipt_id)
        if not quarantine_receipt or not quarantine_receipt.get("sealed"):
            errors.append(f"active A1 packet {packet_id} lacks a sealed quarantine receipt")
        else:
            if quarantine_receipt.get("receipt_kind") != "packet-activation":
                errors.append(f"active A1 packet {packet_id} quarantine receipt has wrong kind")
            if quarantine_receipt.get("issuer_role") != "creator":
                errors.append(f"active A1 packet {packet_id} activation is not creator-issued")
            if quarantine_receipt.get("subject_contract_sha256", {}).get(packet_id) != packet.get("contract_sha256"):
                errors.append(f"active A1 packet {packet_id} activation receipt binds wrong contract")
            missing_quarantine_claims = quarantine_claims - subject_claims(
                quarantine_receipt
            ).get(packet_id, set())
            if missing_quarantine_claims:
                errors.append(
                    f"active A1 packet {packet_id} quarantine receipt lacks claims "
                    f"{sorted(missing_quarantine_claims)}"
                )
        exception_ids = set(
            boundary_manifest.get("local_observation_exceptions", [])
            if isinstance(boundary_manifest, dict)
            else []
        )
        baseline_ids = {
            item.get("id") for item in packet.get("baseline_evidence", [])
            if isinstance(item, dict)
        }
        if not exception_ids.issubset(baseline_ids):
            errors.append(f"active A1 packet {packet_id} boundary names unknown baseline exceptions")
        for artifact in packet.get("baseline_evidence", []):
            if not isinstance(artifact, dict):
                continue
            uri = artifact.get("uri")
            evidence_id = artifact.get("id")
            if isinstance(uri, str) and uri.startswith("pending://"):
                errors.append(f"active A1 packet {packet_id} baseline {evidence_id} remains pending")
            elif artifact_is_content_addressed(artifact):
                continue
            elif not (
                evidence_id in exception_ids
                and isinstance(uri, str)
                and uri.startswith("local-observation://")
            ):
                errors.append(
                    f"active A1 packet {packet_id} baseline {evidence_id} is neither content-addressed "
                    "nor an attested local-observation exception"
                )
        if quarantine_receipt_id == accept_requirement.get("receipt_id"):
            errors.append(
                f"active A1 packet {packet_id} must keep acceptance and quarantine receipts distinct"
            )

        reservation = packet.get("reservation", {})
        if reservation.get("status") != "held":
            errors.append(f"active A1 packet {packet_id} reservation is not held")
        base_commit = reservation.get("base_commit")
        if not isinstance(base_commit, str) or not re.fullmatch(
            r"(?:[0-9a-f]{40}|[0-9a-f]{64})", base_commit
        ):
            errors.append(f"active A1 packet {packet_id} lacks an exact base commit")
        for field in ("lease_id", "fencing_token"):
            if not isinstance(reservation.get(field), str) or not reservation.get(field):
                errors.append(f"active A1 packet {packet_id} lacks {field}")
        reserved_paths = reservation.get("paths", [])
        declared_paths = packet.get("declared_paths", [])
        if len(reserved_paths) != len(set(reserved_paths)) or set(reserved_paths) != set(declared_paths):
            errors.append(
                f"active A1 packet {packet_id} reservation paths must exactly match declared_paths"
            )
        reserved_domains = reservation.get("domains", [])
        declared_domains = packet.get("affected_domains", [])
        if len(reserved_domains) != len(set(reserved_domains)) or set(reserved_domains) != set(declared_domains):
            errors.append(
                f"active A1 packet {packet_id} reservation domains must exactly match affected_domains"
            )

        final_event = packet.get("status_events", [])[-1] if packet.get("status_events") else {}
        if final_event.get("to") != packet.get("status"):
            errors.append(f"active A1 packet {packet_id} final event has the wrong status")
        if final_event.get("receipt_id") != quarantine_receipt_id:
            errors.append(
                f"active A1 packet {packet_id} final event must reference its activation receipt"
            )
        activation_at = parse_datetime(final_event.get("at"))
        expires_at = parse_datetime(reservation.get("expires_at"))
        if expires_at is None:
            errors.append(f"active A1 packet {packet_id} has an invalid reservation expiry")
        else:
            if activation_at is None or expires_at <= activation_at:
                errors.append(
                    f"active A1 packet {packet_id} reservation must expire after activation"
                )
            if expires_at <= datetime.now(timezone.utc):
                errors.append(f"active A1 packet {packet_id} reservation has expired")

    boundary_references = {
        (ROOT / packet["a1_boundary_manifest"]["path"]).resolve()
        for packet in packets
        if isinstance(packet.get("a1_boundary_manifest"), dict)
        and isinstance(packet["a1_boundary_manifest"].get("path"), str)
    }
    boundary_directory = ROOT / "governance" / "a1-boundaries"
    boundary_files = (
        {path.resolve() for path in boundary_directory.glob("*.json")}
        if boundary_directory.is_dir()
        else set()
    )
    if boundary_references != boundary_files:
        errors.append(
            "A1 boundary-manifest closure differs between packet references and canonical files"
        )

    if state.get("trusted_gatekeeper_status") != "passed" and state.get("active_autonomy") not in {"A0", "A1"}:
        errors.append("autonomy above A1 requires a passed trusted gatekeeper")

    if errors:
        print("FOUNDATION BOOTSTRAP LINT: FAIL", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    status_counts: dict[str, int] = {}
    for record in records:
        status_counts[record["status"]] = status_counts.get(record["status"], 0) + 1
    print("FOUNDATION BOOTSTRAP LINT: PASS")
    print(f"decision records: {len(records)}")
    print("statuses: " + ", ".join(f"{key}={status_counts[key]}" for key in sorted(status_counts)))
    print(f"markdown files: {len(list(ROOT.rglob('*.md')))}")
    print(f"schemas: {len(list((ROOT / 'schemas').glob('*.json')))}")
    print(f"registered scenarios: {len(scenarios)}")
    print(f"work packets: {len(packet_paths)}")
    print(f"ratification receipts: {len(receipts)} ({sum(1 for receipt in receipts if not receipt.get('sealed'))} unsealed)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
