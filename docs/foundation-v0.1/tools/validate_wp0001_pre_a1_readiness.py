#!/usr/bin/env python3
"""Focused semantic validation for the WP-0001 pre-A1 readiness snapshot."""

from __future__ import annotations

import re
from pathlib import Path
from typing import Callable


READINESS_REPO_RELATIVE = Path("docs/evidence/WP-0001/pre-a1-readiness-20260716.json")
SCHEMA_RELATIVE = Path("schemas/wp0001-pre-a1-readiness.schema.json")
PACKET_RELATIVE = Path("work-packets/proposed/WP-0001.json")

REQUIRED_TOOLCHAIN_STATUSES = {
    "UNITY-HUB": "matched",
    "UNITY-EDITOR": "mismatch",
    "MAC-BUILD-SUPPORT-IL2CPP": "missing",
    "XCODE": "missing",
    "DOTNET-SDK": "missing",
    "ROSETTA-2": "matched",
}
REQUIRED_PACKAGE_STATUSES = {
    "UNITY-AI-ASSISTANT": "matched",
    "URP": "mismatch",
    "UNITY-TEST-FRAMEWORK": "mismatch",
}
REQUIRED_DEVIATIONS = {
    "DEV-MCP-READCONSOLE-001",
    "DEV-DIRECT-UNITY-VERSION-001",
    "DEV-DIRECT-UNITY-HUB-001",
}
REQUIRED_BLOCKERS = {
    "BLK-D0047-EDITOR",
    "BLK-MAC-IL2CPP",
    "BLK-XCODE",
    "BLK-DOTNET-SDK",
    "BLK-PACKAGE-GRAPH",
    "BLK-WP0001-PROJECT-IDENTITY",
    "BLK-UNITY-AI-SEAT-LINKAGE",
    "BLK-MCP-TARGET",
    "BLK-MCP-NETWORK-BOUNDARY",
    "BLK-MCP-APPROVAL-SCOPE",
    "BLK-CLEAN-ZERO-TOOL-CYCLE",
    "BLK-A1-QUARANTINE",
    "BLK-A1-ACTIVATION-RECEIPT",
}
FORBIDDEN_SECRET_KEYS = {
    "api_key",
    "authorization",
    "credential",
    "license_key",
    "password",
    "secret",
    "token",
}
FORBIDDEN_RAW_ID_KEYS = {
    "cloud_project_id",
    "license_id",
    "organization_id",
}


def _records_by_id(records: object, label: str, errors: list[str]) -> dict[str, dict]:
    if not isinstance(records, list):
        errors.append(f"{label} must be an array")
        return {}
    by_id = {
        item.get("id"): item
        for item in records
        if isinstance(item, dict) and isinstance(item.get("id"), str)
    }
    if len(by_id) != len(records):
        errors.append(f"{label} IDs must be present and unique")
    return by_id


def _scan_for_secrets(value: object, label: str, errors: list[str]) -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            normalized = key.lower()
            if normalized in FORBIDDEN_SECRET_KEYS:
                errors.append(f"{label} contains forbidden secret-bearing key {key!r}")
            if normalized in FORBIDDEN_RAW_ID_KEYS:
                errors.append(f"{label} contains raw identifier key {key!r}; store only its SHA-256")
            _scan_for_secrets(child, f"{label}.{key}", errors)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            _scan_for_secrets(child, f"{label}[{index}]", errors)
    elif isinstance(value, str):
        if re.search(r"(?i)(?:bearer\s+[a-z0-9._-]{12,}|sk-[a-z0-9_-]{12,})", value):
            errors.append(f"{label} resembles a credential value")


def validate_readiness_semantics(
    evidence: dict,
    packet: dict,
    *,
    git_commit_exists: Callable[[str], bool],
    receipts_by_id: dict[str, dict],
) -> list[str]:
    """Validate the snapshot's cross-record and fail-closed readiness semantics."""
    errors: list[str] = []
    label = str(READINESS_REPO_RELATIVE)

    if evidence.get("packet_id") != packet.get("id"):
        errors.append(f"{label} binds a different work packet")
    authority = evidence.get("authority_binding", {})
    if authority.get("packet_contract_sha256") != packet.get("contract_sha256"):
        errors.append(f"{label} binds the wrong immutable WP-0001 contract")
    base_commit = authority.get("base_commit")
    if not isinstance(base_commit, str) or not git_commit_exists(base_commit):
        errors.append(f"{label} base commit is absent from the repository")
    if evidence.get("overall_status") != "blocked":
        errors.append(f"{label} must remain blocked until a successor observation proves every gate")
    if evidence.get("activation_authority") is not False:
        errors.append(f"{label} must not claim activation authority")
    if evidence.get("contains_secret_values") is not False:
        errors.append(f"{label} must declare that it contains no secret values")

    expected_receipts = {
        "RR-D0051-20260716",
        "RR-WP0001-ROUTE-20260716",
    }
    observed_receipts = set(authority.get("route_receipt_ids", []))
    if observed_receipts != expected_receipts:
        errors.append(f"{label} route receipt set differs from protected authority")
    for receipt_id in expected_receipts:
        receipt = receipts_by_id.get(receipt_id)
        if not receipt or not receipt.get("sealed"):
            errors.append(f"{label} references missing or unsealed route receipt {receipt_id}")
    if authority.get("a1_activation_receipt_id") is not None:
        errors.append(f"{label} cannot name an A1 activation receipt while blocked")

    toolchain = _records_by_id(evidence.get("toolchain"), f"{label}.toolchain", errors)
    for component_id, expected_status in REQUIRED_TOOLCHAIN_STATUSES.items():
        record = toolchain.get(component_id)
        if not record:
            errors.append(f"{label} lacks toolchain observation {component_id}")
        elif record.get("status") != expected_status:
            errors.append(
                f"{label} {component_id} status must be {expected_status!r}, "
                f"got {record.get('status')!r}"
            )

    packages = _records_by_id(
        evidence.get("package_graph", {}).get("packages"),
        f"{label}.package_graph.packages",
        errors,
    )
    for package_id, expected_status in REQUIRED_PACKAGE_STATUSES.items():
        record = packages.get(package_id)
        if not record:
            errors.append(f"{label} lacks package observation {package_id}")
        elif record.get("status") != expected_status:
            errors.append(
                f"{label} {package_id} status must be {expected_status!r}, "
                f"got {record.get('status')!r}"
            )

    project = evidence.get("observation_project", {})
    if project.get("matches_wp0001_temporary_identity") is not False:
        errors.append(f"{label} must not treat the onboarding project as WP-0001")
    if project.get("same_organization_project_linkage_verified") is not False:
        errors.append(f"{label} must not claim verified organization/project linkage")

    route = evidence.get("direct_mcp_route", {})
    if route.get("selected_route") != "UNITY-MCP-EXTERNAL":
        errors.append(f"{label} selected route differs from D-0051")
    if route.get("bridge", {}).get("target_matches_wp0001") is not False:
        errors.append(f"{label} must not claim the observation project is the WP-0001 target")
    relay = route.get("relay", {})
    if (
        relay.get("matches_package_copy") is not True
        or relay.get("sha256") != relay.get("package_copy_sha256")
    ):
        errors.append(f"{label} relay must match the exact Assistant package copy")
    repository_config = route.get("repository_config", {})
    if repository_config.get("enabled") is not False:
        errors.append(f"{label} protected-main MCP configuration must remain disabled")
    if repository_config.get("global_unity_mcp_present") is not False:
        errors.append(f"{label} global Unity MCP must remain absent")

    listeners = {
        item.get("role"): item
        for item in route.get("network_listeners", [])
        if isinstance(item, dict)
    }
    mcp_listener = listeners.get("mcp-client", {})
    if mcp_listener.get("port") != 9002 or mcp_listener.get("exposure") != "all-interfaces":
        errors.append(f"{label} must preserve the observed unsafe MCP listener state")

    session = route.get("session_history", {})
    expected_session = {
        "connected_direct_clients": 0,
        "historical_handshakes_observed": 33,
        "handshakes_with_zero_tools_observed": 2,
        "handshakes_with_full_toolset_observed": 31,
        "handshake_tool_count": 54,
        "minimal_scope_verified": False,
        "approval_history_state": "inherited-auto-approval-observed",
        "zero_tool_clean_handshake_verified": False,
        "unity_tool_invocations_before_activation": 1,
    }
    for field, expected in expected_session.items():
        if session.get(field) != expected:
            errors.append(
                f"{label} session_history.{field} must equal {expected!r}, "
                f"got {session.get(field)!r}"
            )

    entitlement = route.get("entitlement_and_linkage", {})
    if entitlement.get("eligible_unity_ai_seat") != "unverified":
        errors.append(f"{label} must keep eligible Unity AI seat status unverified")
    if entitlement.get("same_organization_project_linkage") != "unverified":
        errors.append(f"{label} must keep same-organization linkage status unverified")
    if entitlement.get("license_secret_material_copied") is not False:
        errors.append(f"{label} must not contain Unity license secret material")

    deviations = _records_by_id(evidence.get("deviations"), f"{label}.deviations", errors)
    if set(deviations) != REQUIRED_DEVIATIONS:
        errors.append(
            f"{label} deviation closure differs: "
            f"missing={sorted(REQUIRED_DEVIATIONS - set(deviations))}, "
            f"unexpected={sorted(set(deviations) - REQUIRED_DEVIATIONS)}"
        )
    for deviation_id, deviation in deviations.items():
        if deviation.get("activation_evidence_eligible") is not False:
            errors.append(f"{label} {deviation_id} cannot be activation evidence")
        if deviation.get("clean_cycle_required") is not True:
            errors.append(f"{label} {deviation_id} must require a clean cycle")
        if deviation.get("preservation_status") != "preserved":
            errors.append(f"{label} {deviation_id} must remain preserved")
    if deviations.get("DEV-MCP-READCONSOLE-001", {}).get("mcp_tool_invocation") is not True:
        errors.append(f"{label} must classify Unity_ReadConsole as an MCP tool invocation")
    if deviations.get("DEV-DIRECT-UNITY-VERSION-001", {}).get("mcp_tool_invocation") is not False:
        errors.append(f"{label} must classify direct Unity -version outside MCP")
    if deviations.get("DEV-DIRECT-UNITY-HUB-001", {}).get("mcp_tool_invocation") is not False:
        errors.append(f"{label} must classify direct Unity Hub CLI outside MCP")

    blockers = _records_by_id(evidence.get("blockers"), f"{label}.blockers", errors)
    if set(blockers) != REQUIRED_BLOCKERS:
        errors.append(
            f"{label} blocker closure differs: "
            f"missing={sorted(REQUIRED_BLOCKERS - set(blockers))}, "
            f"unexpected={sorted(set(blockers) - REQUIRED_BLOCKERS)}"
        )
    if any(blocker.get("status") != "blocking" for blocker in blockers.values()):
        errors.append(f"{label} contains a non-blocking blocker record")

    _scan_for_secrets(evidence, label, errors)
    return errors


def validate_wp0001_pre_a1_readiness(
    root: Path,
    *,
    load_json: Callable[[Path], dict],
    validate_schema_subset: Callable[[object, object, dict, str], list[str]],
    git_commit_exists: Callable[[str], bool],
) -> list[str]:
    """Validate the canonical readiness file using host foundation helpers."""
    repo_root = root.parents[1]
    evidence_path = repo_root / READINESS_REPO_RELATIVE
    schema_path = root / SCHEMA_RELATIVE
    packet_path = root / PACKET_RELATIVE
    if not evidence_path.is_file():
        return [f"missing canonical readiness evidence: {READINESS_REPO_RELATIVE}"]
    if not schema_path.is_file():
        return [f"missing readiness schema: {SCHEMA_RELATIVE}"]
    evidence = load_json(evidence_path)
    schema = load_json(schema_path)
    errors = validate_schema_subset(
        evidence,
        schema,
        schema,
        str(READINESS_REPO_RELATIVE),
    )
    packet = load_json(packet_path)
    receipt_dir = root / "ledger" / "receipts"
    receipts_by_id = {
        receipt["receipt_id"]: receipt
        for receipt in (
            load_json(path) for path in sorted(receipt_dir.glob("*.json"))
        )
        if isinstance(receipt.get("receipt_id"), str)
    }
    errors.extend(
        validate_readiness_semantics(
            evidence,
            packet,
            git_commit_exists=git_commit_exists,
            receipts_by_id=receipts_by_id,
        )
    )
    return errors


def main() -> int:
    import validate_foundation as foundation

    errors = validate_wp0001_pre_a1_readiness(
        foundation.ROOT,
        load_json=foundation.load_json,
        validate_schema_subset=foundation.validate_schema_subset,
        git_commit_exists=foundation.git_commit_exists,
    )
    if errors:
        print("WP-0001 PRE-A1 READINESS: FAIL")
        for error in errors:
            print(f"- {error}")
        return 1
    print("WP-0001 PRE-A1 READINESS: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
