#!/usr/bin/env python3
"""Base-trusted WP-0002 pull-request policy.

This program is executed from the protected base revision. It fetches the head
only as Git objects, never checks out or executes candidate bytes, derives the
base...head delta itself, and applies lifecycle-specific fail-closed scope law.
The base-owned `wp0002-policy` workflow is the intended invoker and live canary.
A local pass is only a policy result; it never claims that the check is attached
to the latest head, required, or retained without separate live evidence.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import secrets
import stat
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path, PurePosixPath


POLICY_CONTRACT = "wp0002-base-trusted-pr-policy-v1"
POLICY_REPORT_PATH = "BuildArtifacts/WP-0002/wp0002-policy-report.json"
SHA_RE = re.compile(r"^[0-9a-f]{40}$")
REPOSITORY_RE = re.compile(r"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")
HEAD_REF_RE = re.compile(r"^agent/[A-Za-z0-9._/-]+$")
PACKET_PATH = "docs/foundation-v0.1/work-packets/proposed/WP-0002.json"
WP0003_PACKET_PATH = "docs/foundation-v0.1/work-packets/proposed/WP-0003.json"

CREATOR_DRIFT = {
    ".codex/config.toml",
    "Game/ProjectSettings/ProjectSettings.asset",
    "Game/ProjectSettings/SceneTemplateSettings.json",
}
FROZEN_SELF_VERIFICATION = {
    ".github/workflows/foundation.yml",
    ".github/workflows/wp0002-ci.yml",
    "Tools/Validation/validate_wp0002_entry_gate.py",
    "Tools/Validation/validate_wp0002_package_graph.py",
    "Tools/Validation/validate_wp0002_policy.py",
    "docs/foundation-v0.1/tools/validate_foundation.py",
}

STATUS_DOCUMENT_RULES = {
    "AGENTS.md": {"M"},
    "docs/foundation-v0.1/README.md": {"M"},
    "docs/foundation-v0.1/06-AGENT-OPERATING-MODEL.md": {"M"},
    "docs/foundation-v0.1/08-BUILD-ROADMAP.md": {"M"},
    "docs/foundation-v0.1/10-FIRST-WORK-PACKET.md": {"M"},
    "docs/foundation-v0.1/11-TRUST-AND-ENFORCEMENT.md": {"M"},
    "docs/foundation-v0.1/12-FOUNDATION-AUDIT.md": {"M"},
    "docs/foundation-v0.1/15-LEAN-A1-LOCAL-DEVELOPMENT.md": {"M"},
}
STAGE_B_RULES = {
    **STATUS_DOCUMENT_RULES,
    "docs/foundation-v0.1/01-DECISION-LEDGER.md": {"M"},
    "docs/foundation-v0.1/ledger/decisions.jsonl": {"M"},
    "docs/foundation-v0.1/governance/ratification-state.json": {"M"},
    PACKET_PATH: {"M"},
    WP0003_PACKET_PATH: {"M"},
}
STAGE_C_RULES = {
    **STATUS_DOCUMENT_RULES,
    "docs/foundation-v0.1/governance/ratification-state.json": {"M"},
    PACKET_PATH: {"M"},
    "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json": {"M"},
}
STAGE_C_SCOPE_CAPTURE_PATH = (
    "docs/evidence/WP-0002/scope-capture/working-tree-scope.json"
)
STAGE_C_FIXED_CAPTURE_PATHS = {
    STAGE_C_SCOPE_CAPTURE_PATH,
    "docs/evidence/WP-0002/github-protection.json",
    "docs/evidence/WP-0002/github-protection.raw-api.json",
    "docs/evidence/WP-0002/github-protection.raw-rulesets.json",
    "docs/evidence/WP-0002/cursor-approval-policy.json",
    "docs/evidence/WP-0002/cursor-approval-policy.raw-config.json",
}
STAGE_C_RULES.update({path: {"A"} for path in STAGE_C_FIXED_CAPTURE_PATHS})
LIFECYCLE_RULES = {
    **STATUS_DOCUMENT_RULES,
    "docs/foundation-v0.1/governance/ratification-state.json": {"M"},
    PACKET_PATH: {"M"},
}
RECEIPT_PREFIX = "docs/foundation-v0.1/ledger/receipts/"
WP0002_EVIDENCE_PREFIX = "docs/evidence/WP-0002/"
WP0003_EVIDENCE_PREFIX = "docs/evidence/WP-0003/"
REGULAR_MODES = {"100644", "100755"}
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
POLICY_PATH = "Tools/Validation/validate_wp0002_policy.py"
GIT_PREFIX = (
    "/usr/bin/git",
    "--no-replace-objects",
    "-c",
    "core.fsmonitor=false",
    "-c",
    "core.hooksPath=/dev/null",
    "-c",
    "maintenance.auto=false",
    "-c",
    "gc.auto=0",
    "-c",
    "fetch.writeCommitGraph=false",
    "--no-optional-locks",
    "--no-pager",
)
GIT_ENV = {
    "HOME": "/var/empty",
    "XDG_CONFIG_HOME": "/var/empty",
    "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
    "LANG": "C",
    "LC_ALL": "C",
    "TZ": "UTC",
    "GIT_CONFIG_NOSYSTEM": "1",
    "GIT_CONFIG_SYSTEM": "/dev/null",
    "GIT_CONFIG_GLOBAL": "/dev/null",
    "GIT_OPTIONAL_LOCKS": "0",
    "GIT_NO_REPLACE_OBJECTS": "1",
    "GIT_NO_LAZY_FETCH": "1",
    "GIT_TERMINAL_PROMPT": "0",
    "GIT_ASKPASS": "/usr/bin/false",
    "SSH_ASKPASS": "/usr/bin/false",
    "GIT_PAGER": "",
    "PAGER": "",
}
UNSAFE_LOCAL_CONFIG_KEYS = {
    "core.fsmonitor",
    "core.hookspath",
    "core.attributesfile",
    "core.excludesfile",
    "diff.external",
}
UNSAFE_LOCAL_CONFIG_PREFIXES = (
    "alias.",
    "include.",
    "credential.",
    "filter.",
    "pager.",
    "maintenance.",
    "gc.",
    "protocol.",
    "uploadpack.",
)
ALLOWED_LOCAL_CONFIG_KEYS = {
    "core.repositoryformatversion",
    "core.filemode",
    "core.bare",
    "core.logallrefupdates",
    "core.ignorecase",
    "core.precomposeunicode",
    "remote.origin.url",
    "remote.origin.fetch",
    "remote.origin.mirror",
    "extensions.objectformat",
}


@dataclass(frozen=True)
class DiffEntry:
    status: str
    old_path: str | None
    new_path: str | None
    old_mode: str
    new_mode: str
    old_oid: str
    new_oid: str


def _safe_relative(value: str) -> str | None:
    if not value or "\\" in value:
        return None
    path = PurePosixPath(value)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        return None
    return path.as_posix().rstrip("/") or None


def _covers(scope_path: str, changed_path: str) -> bool:
    scope = _safe_relative(scope_path)
    changed = _safe_relative(changed_path)
    if scope is None or changed is None:
        return False
    return changed == scope or changed.startswith(f"{scope}/")


def _overlaps(left: str, right: str) -> bool:
    return _covers(left, right) or _covers(right, left)


def _run_git(repo: Path, args: list[str]) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        [*GIT_PREFIX, "-C", str(repo), *args],
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
        timeout=60,
        env=GIT_ENV,
    )


def _require_repository(repo: Path) -> None:
    metadata = repo.lstat()
    if not stat.S_ISDIR(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        raise ValueError("repository root must be a real directory")
    bare = _run_git(repo, ["rev-parse", "--is-bare-repository"])
    if bare.returncode != 0 or bare.stdout.strip() != b"true":
        raise ValueError("policy object store must be an exact bare Git repository")
    object_format = _run_git(repo, ["rev-parse", "--show-object-format"])
    if object_format.returncode != 0 or object_format.stdout.strip() != b"sha1":
        raise ValueError("policy object store must use the exact sha1 object format")
    replacements = _run_git(
        repo, ["for-each-ref", "--format=%(refname)", "refs/replace/"]
    )
    if replacements.returncode != 0:
        raise ValueError("policy cannot inspect replace refs")
    if replacements.stdout.strip():
        raise ValueError("policy object store must not contain replace refs")
    config = _run_git(repo, ["config", "--local", "--name-only", "-z", "--list"])
    if config.returncode != 0:
        raise ValueError("policy cannot inspect repository-local configuration")
    try:
        config_keys = {
            item.decode("utf-8").lower()
            for item in config.stdout.split(b"\0")
            if item
        }
    except UnicodeDecodeError as exc:
        raise ValueError("policy repository configuration is not UTF-8") from exc
    unsafe = sorted(
        key
        for key in config_keys
        if key not in ALLOWED_LOCAL_CONFIG_KEYS
        or key in UNSAFE_LOCAL_CONFIG_KEYS
        or key.startswith(UNSAFE_LOCAL_CONFIG_PREFIXES)
        or (key.startswith("diff.") and key.endswith(".command"))
    )
    if unsafe:
        raise ValueError(f"policy object store has unsafe local Git config: {unsafe}")


def _validate_invocation(
    base: str,
    head: str,
    base_ref: str,
    head_ref: str,
    base_repository: str,
    head_repository: str,
    policy_source_sha: str,
) -> list[str]:
    errors: list[str] = []
    if not SHA_RE.fullmatch(base) or not SHA_RE.fullmatch(head) or base == head:
        errors.append("base and head must be distinct immutable 40-hex SHAs")
    if base_ref != "main":
        errors.append("WP-0002 policy requires pull-request base main")
    if not HEAD_REF_RE.fullmatch(head_ref) or ".." in head_ref or "//" in head_ref:
        errors.append("WP-0002 policy requires an unambiguous agent/* head branch")
    if (
        not REPOSITORY_RE.fullmatch(base_repository)
        or head_repository != base_repository
    ):
        errors.append("WP-0002 policy rejects fork or mismatched repositories")
    if not SHA_RE.fullmatch(policy_source_sha) or policy_source_sha == head:
        errors.append("external-policy source must be an immutable non-candidate SHA")
    return errors


def _parse_raw_diff(raw: bytes) -> tuple[list[DiffEntry], list[str]]:
    entries: list[DiffEntry] = []
    errors: list[str] = []
    fields = raw.split(b"\0")
    index = 0
    while index < len(fields) and fields[index]:
        try:
            header = fields[index].decode("ascii")
            parts = header.split()
            if len(parts) != 5 or not parts[0].startswith(":"):
                raise ValueError("invalid raw header")
            old_mode = parts[0][1:]
            new_mode, old_oid, new_oid, raw_status = parts[1:]
            status = raw_status[0]
            if status not in {"A", "M", "D", "R", "C"}:
                raise ValueError(f"unsupported status {raw_status}")
            if status in {"R", "C"}:
                old_path = fields[index + 1].decode("utf-8")
                new_path = fields[index + 2].decode("utf-8")
                index += 3
            else:
                path = fields[index + 1].decode("utf-8")
                old_path = None if status == "A" else path
                new_path = None if status == "D" else path
                index += 2
            entries.append(
                DiffEntry(status, old_path, new_path, old_mode, new_mode, old_oid, new_oid)
            )
        except (IndexError, UnicodeDecodeError, ValueError) as exc:
            errors.append(f"cannot parse base...head raw diff: {exc}")
            break
    return entries, errors


def _git_blob_bytes(repo: Path, oid: str) -> bytes:
    if not SHA_RE.fullmatch(oid):
        raise ValueError(f"candidate object ID is not exact sha1: {oid}")
    result = _run_git(repo, ["cat-file", "blob", oid])
    if result.returncode != 0:
        raise ValueError(f"candidate blob {oid} is unavailable")
    prefix = f"blob {len(result.stdout)}\0".encode("ascii")
    if hashlib.sha1(prefix + result.stdout).hexdigest() != oid:
        raise ValueError(f"candidate blob {oid} fails object hash verification")
    return result.stdout


def _git_path_bytes(repo: Path, commit: str, path: str) -> bytes:
    result = _run_git(repo, ["show", f"{commit}:{path}"])
    if result.returncode != 0:
        raise ValueError(f"cannot read {path} from {commit}")
    return result.stdout


def _json_object(data: bytes, path: str) -> dict:
    try:
        value = json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"{path} is not valid candidate JSON: {exc}") from exc
    if not isinstance(value, dict):
        raise ValueError(f"{path} must be a JSON object")
    return value


def _content_address_report(
    payload: dict[str, object], errors: list[str]
) -> dict[str, object]:
    report: dict[str, object] = {
        **payload,
        "result": "fail" if errors else "pass",
        "errors": list(errors),
        "error_count": len(errors),
    }
    canonical = json.dumps(
        report, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")
    report["report_sha256"] = hashlib.sha256(canonical).hexdigest()
    return report


def _write_report(repo_root: Path, report: str, payload: dict[str, object]) -> None:
    repo_lexical = Path(os.path.abspath(os.fspath(repo_root)))
    allowed_lexical = repo_lexical / "BuildArtifacts" / "WP-0002"
    destination_lexical = Path(
        os.path.abspath(os.path.join(os.fspath(repo_lexical), report))
    )
    try:
        relative_destination = destination_lexical.relative_to(repo_lexical)
        destination_lexical.relative_to(allowed_lexical)
    except ValueError as exc:
        raise ValueError("report must be under BuildArtifacts/WP-0002/") from exc
    if Path(report).is_absolute() or ".." in Path(report).parts:
        raise ValueError("report must use a lexical repository-relative path")
    parts = relative_destination.parts
    if len(parts) < 3 or parts[:2] != ("BuildArtifacts", "WP-0002"):
        raise ValueError("report must name a file under BuildArtifacts/WP-0002/")
    repo_stat = os.lstat(repo_lexical)
    if stat.S_ISLNK(repo_stat.st_mode) or not stat.S_ISDIR(repo_stat.st_mode):
        raise ValueError("report root must be a real directory, not a symlink")
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
                raise ValueError(
                    f"report parent component {component!r} is not a directory"
                )
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
                raise ValueError("report destination must be a regular file")
        temporary_name = (
            f".{destination_name}.tmp-{os.getpid()}-{secrets.token_hex(8)}"
        )
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


def _validate_receipt_blob(path: str, blob: bytes) -> list[str]:
    errors: list[str] = []
    try:
        receipt = json.loads(blob.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return [f"transition receipt {path} is not valid UTF-8 JSON"]
    expected_id = PurePosixPath(path).stem
    if (
        not isinstance(receipt, dict)
        or receipt.get("receipt_id") != expected_id
        or receipt.get("sealed") is not True
        or receipt.get("issuer_role") != "creator"
        or receipt.get("artifact_resolver", {}).get("type") != "external-protected"
    ):
        errors.append(f"transition receipt {path} lacks exact sealed creator authority")
    return errors


def _validate_transition_receipt(
    repo: Path,
    head: str,
    base_status: str,
    head_status: str,
    head_packet: dict,
) -> list[str]:
    matching = [
        event
        for event in head_packet.get("status_events", [])
        if isinstance(event, dict)
        and event.get("from") == base_status
        and event.get("to") == head_status
    ]
    if len(matching) != 1:
        return [
            f"WP-0002 {base_status}->{head_status} must retain exactly one receipt-bound status event"
        ]
    receipt_id = matching[0].get("receipt_id")
    if not isinstance(receipt_id, str) or not re.fullmatch(r"RR-[A-Z0-9-]+", receipt_id):
        return [f"WP-0002 {base_status}->{head_status} status event lacks an exact receipt ID"]
    receipt_path = f"{RECEIPT_PREFIX}{receipt_id}.json"
    try:
        receipt_blob = _git_path_bytes(repo, head, receipt_path)
    except ValueError:
        return [f"WP-0002 {base_status}->{head_status} receipt does not resolve in candidate tree"]
    return _validate_receipt_blob(receipt_path, receipt_blob)


def _stage_c_scope_artifact_paths(
    candidate_blobs: dict[str, bytes],
) -> tuple[set[str], list[str]]:
    """Derive the two content-addressed siblings without trusting aliases."""
    capture_blob = candidate_blobs.get(STAGE_C_SCOPE_CAPTURE_PATH)
    if capture_blob is None:
        return set(), ["stage-c scope capture blob is missing"]

    def unique_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"duplicate JSON key {key!r}")
            result[key] = value
        return result

    try:
        capture = json.loads(
            capture_blob.decode("utf-8"), object_pairs_hook=unique_object
        )
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as exc:
        return set(), [f"stage-c scope capture is not unambiguous UTF-8 JSON: {exc}"]
    if not isinstance(capture, dict):
        return set(), ["stage-c scope capture must be a JSON object"]
    if (
        capture.get("capture_contract") != "wp0002-working-tree-scope-capture-v2"
        or capture.get("packet_id") != "WP-0002"
        or capture.get("boundary_manifest_id") != "A1B-WP-0002-LOCAL-DEV"
    ):
        return set(), ["stage-c scope capture identity differs from the frozen contract"]
    artifacts = capture.get("artifacts")
    if not isinstance(artifacts, dict) or set(artifacts) != {
        "raw_status",
        "observations",
    }:
        return set(), ["stage-c scope capture must name exactly raw_status and observations"]

    derived: set[str] = set()
    errors: list[str] = []
    patterns = {
        "raw_status": re.compile(
            r"^docs/evidence/WP-0002/scope-capture/"
            r"working-tree-scope\.status\.([0-9a-f]{64})\.bin$"
        ),
        "observations": re.compile(
            r"^docs/evidence/WP-0002/scope-capture/"
            r"working-tree-scope\.observations\.([0-9a-f]{64})\.json$"
        ),
    }
    for name, pattern in patterns.items():
        reference = artifacts.get(name)
        if not isinstance(reference, dict) or set(reference) != {
            "path",
            "sha256",
            "byte_size",
        }:
            errors.append(f"stage-c scope capture {name} reference is not exact")
            continue
        relative = reference.get("path")
        digest = reference.get("sha256")
        byte_size = reference.get("byte_size")
        if not isinstance(relative, str) or relative == STAGE_C_SCOPE_CAPTURE_PATH:
            errors.append(f"stage-c scope capture {name} creates a path cycle")
            continue
        match = pattern.fullmatch(relative)
        if (
            match is None
            or not isinstance(digest, str)
            or match.group(1) != digest
            or type(byte_size) is not int
            or byte_size < 0
        ):
            errors.append(f"stage-c scope capture {name} is not content-addressed")
            continue
        artifact_blob = candidate_blobs.get(relative)
        if artifact_blob is None:
            errors.append(f"stage-c scope capture {name} blob is missing: {relative}")
            continue
        if (
            len(artifact_blob) != byte_size
            or hashlib.sha256(artifact_blob).hexdigest() != digest
        ):
            errors.append(f"stage-c scope capture {name} bytes do not match its reference")
            continue
        if relative in derived:
            errors.append("stage-c scope capture sibling references are not distinct")
            continue
        derived.add(relative)
    return derived, errors


def validate_delta(
    entries: list[DiffEntry],
    *,
    phase: str,
    declared_paths: list[str],
    reservation_paths: list[str],
    candidate_blobs: dict[str, bytes] | None = None,
) -> list[str]:
    """Pure fail-closed delta policy used by the CLI and mutation regressions."""
    errors: list[str] = []
    candidate_blobs = candidate_blobs or {}
    stage_c_scope_artifacts: set[str] = set()
    if phase == "stage-c":
        stage_c_scope_artifacts, artifact_errors = _stage_c_scope_artifact_paths(
            candidate_blobs
        )
        errors.extend(artifact_errors)
    normalized_declared = {_safe_relative(path) for path in declared_paths}
    normalized_reserved = {_safe_relative(path) for path in reservation_paths}
    if None in normalized_declared or normalized_declared != normalized_reserved:
        errors.append("WP-0002 declared and reserved path sets are not exactly equal")
    implementation_phases = {
        "implementation",
        "active-to-verifying",
        "verifying-to-candidate",
    }
    lifecycle_phases = {
        "active-to-verifying",
        "verification-to-active",
        "verifying-to-candidate",
        "candidate-to-verifying",
        "candidate-to-released",
        "early-cancellation",
        "rejected-transition",
        "rollback-transition",
        "superseded-transition",
    }
    for entry in entries:
        paths = [path for path in (entry.old_path, entry.new_path) if path is not None]
        if not paths or any(_safe_relative(path) is None for path in paths):
            errors.append("diff contains an unsafe or missing path")
            continue
        for mode, side in ((entry.old_mode, "old"), (entry.new_mode, "new")):
            if mode != "000000" and mode not in REGULAR_MODES:
                errors.append(f"diff {side} mode is not a regular file mode: {mode}")
        for path in paths:
            if any(_overlaps(path, drift) for drift in CREATOR_DRIFT):
                errors.append(f"diff touches excluded creator-owned drift: {path}")
            if path in FROZEN_SELF_VERIFICATION:
                errors.append(f"diff modifies frozen self-verification: {path}")

        scoped = all(
            any(_covers(scope, path) for scope in normalized_declared if scope)
            for path in paths
        )
        if phase == "post-terminal-unrelated":
            wp0002_related = any(
                any(_overlaps(path, scope) for scope in normalized_declared if scope)
                or path == PACKET_PATH
                or path.startswith(WP0002_EVIDENCE_PREFIX)
                for path in paths
            )
            if wp0002_related:
                errors.append(
                    f"terminal WP-0002 state rejects retained-packet scope mutation: "
                    f"{entry.status} {paths}"
                )
            continue
        if entry.status == "D" and phase not in implementation_phases | {
            "rollback-transition"
        }:
            errors.append(f"WP-0002 policy rejects deletion in lifecycle phase {phase}: {paths[0]}")

        transition_rules: dict[str, set[str]] = {}
        if phase == "stage-b":
            transition_rules = STAGE_B_RULES
        elif phase == "stage-c":
            transition_rules = {
                **STAGE_C_RULES,
                **{path: {"A"} for path in stage_c_scope_artifacts},
            }
        elif phase in lifecycle_phases:
            transition_rules = LIFECYCLE_RULES

        if phase in implementation_phases and scoped:
            for path in paths:
                if path.startswith("docs/foundation-v0.1/") or path == "AGENTS.md":
                    errors.append(f"implementation diff touches protected governance: {path}")
        elif phase == "rollback-transition" and entry.status == "D" and scoped:
            pass
        else:
            for path in paths:
                allowed = entry.status in transition_rules.get(path, set())
                if path.startswith(RECEIPT_PREFIX) and path.endswith(".json"):
                    allowed = entry.status == "A"
                    if allowed and entry.new_path:
                        errors.extend(
                            _validate_receipt_blob(path, candidate_blobs.get(path, b""))
                        )
                if phase == "stage-b" and path.startswith(WP0003_EVIDENCE_PREFIX):
                    allowed = entry.status in {"A", "M"}
                if phase in lifecycle_phases and path.startswith(WP0002_EVIDENCE_PREFIX):
                    allowed = entry.status in {"A", "M"}
                if not allowed:
                    if phase in implementation_phases:
                        errors.append(f"implementation diff escapes exact WP-0002 scope: {path}")
                    else:
                        errors.append(f"{phase} diff path/status is not enumerated: {entry.status} {path}")
    changed = {
        (entry.status, path)
        for entry in entries
        for path in (entry.old_path, entry.new_path)
        if path is not None
    }
    if phase == "stage-b":
        required = {
            ("M", "docs/foundation-v0.1/01-DECISION-LEDGER.md"),
            ("M", "docs/foundation-v0.1/ledger/decisions.jsonl"),
            ("M", "docs/foundation-v0.1/governance/ratification-state.json"),
            ("M", PACKET_PATH),
            ("M", WP0003_PACKET_PATH),
        }
        missing = required - changed
        if missing:
            errors.append(f"stage-b lacks exact authority materialization paths: {sorted(missing)}")
        if not any(
            status in {"A", "M"} and path.startswith(WP0003_EVIDENCE_PREFIX)
            for status, path in changed
        ):
            errors.append("stage-b lacks WP-0003 release evidence materialization")
        if not any(
            status == "A" and path.startswith(RECEIPT_PREFIX)
            for status, path in changed
        ):
            errors.append("stage-b lacks new sealed receipt materialization")
    if phase == "stage-c":
        required = {
            ("M", "docs/foundation-v0.1/governance/ratification-state.json"),
            ("M", PACKET_PATH),
            ("M", "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json"),
            *(("A", path) for path in STAGE_C_FIXED_CAPTURE_PATHS),
            *(("A", path) for path in stage_c_scope_artifacts),
        }
        missing = required - changed
        if missing:
            errors.append(f"stage-c lacks exact activation/capture paths: {sorted(missing)}")
        if not any(
            status == "A" and path.startswith(RECEIPT_PREFIX)
            for status, path in changed
        ):
            errors.append("stage-c lacks new activation receipt materialization")
    if phase in lifecycle_phases:
        if ("M", PACKET_PATH) not in changed:
            errors.append(f"{phase} lacks the exact WP-0002 lifecycle packet transition")
        if not any(
            status in {"A", "M"} and path.startswith(WP0002_EVIDENCE_PREFIX)
            for status, path in changed
        ):
            errors.append(f"{phase} lacks exact WP-0002 lifecycle evidence")
    return errors


def _phase_for_transition(base_status: object, head_status: object) -> str | None:
    transition = (base_status, head_status)
    if transition == ("proposed", "accepted"):
        return "stage-b"
    if transition == ("accepted", "active"):
        return "stage-c"
    if transition == ("active", "verifying"):
        return "active-to-verifying"
    if transition == ("verifying", "active"):
        return "verification-to-active"
    if transition == ("verifying", "candidate"):
        return "verifying-to-candidate"
    if transition == ("candidate", "verifying"):
        return "candidate-to-verifying"
    if transition == ("candidate", "released"):
        return "candidate-to-released"
    if head_status == "rolled-back" and base_status in {
        "active",
        "verifying",
        "candidate",
        "released",
    }:
        return "rollback-transition"
    if head_status == "rejected" and base_status in {
        "active",
        "verifying",
        "candidate",
    }:
        return "rejected-transition"
    if head_status in {"rejected", "superseded"} and base_status in {
        "proposed",
        "accepted",
    }:
        return "early-cancellation"
    if transition == ("released", "superseded"):
        return "superseded-transition"
    if base_status == head_status and base_status in {
        "active",
        "verifying",
        "candidate",
    }:
        return "implementation"
    if base_status == head_status and base_status in {
        "released",
        "rejected",
        "rolled-back",
        "superseded",
    }:
        return "post-terminal-unrelated"
    return None


def validate_repository_policy(
    repo: Path,
    *,
    base: str,
    head: str,
    base_ref: str,
    head_ref: str,
    base_repository: str,
    head_repository: str,
    policy_source_sha: str,
    fetch_head: bool,
) -> tuple[dict, list[str]]:
    errors = _validate_invocation(
        base,
        head,
        base_ref,
        head_ref,
        base_repository,
        head_repository,
        policy_source_sha,
    )
    if errors:
        return {}, errors
    try:
        _require_repository(repo)
    except (OSError, ValueError) as exc:
        return {}, [str(exc)]
    if fetch_head:
        fetched = _run_git(repo, ["fetch", "--no-tags", "--no-write-fetch-head", "origin", head])
        if fetched.returncode != 0:
            return {}, ["candidate head could not be fetched as Git objects"]
    for name, commit in (
        ("base", base),
        ("head", head),
        ("external-policy source", policy_source_sha),
    ):
        exists = _run_git(repo, ["cat-file", "-e", f"{commit}^{{commit}}"])
        if exists.returncode != 0:
            errors.append(f"{name} SHA does not resolve to a commit")
    if errors:
        return {}, errors
    source_policy = _run_git(
        repo,
        ["show", f"{policy_source_sha}:{POLICY_PATH}"],
    )
    try:
        running_policy = Path(__file__).read_bytes()
    except OSError as exc:
        return {}, [f"trusted policy source cannot be read: {exc}"]
    if source_policy.returncode != 0 or source_policy.stdout != running_policy:
        return {}, ["executed policy bytes differ from external-policy source SHA"]
    ancestry = _run_git(repo, ["merge-base", "--is-ancestor", base, head])
    if ancestry.returncode != 0:
        return {}, ["candidate head does not descend from exact pull-request base"]

    try:
        base_packet_bytes = _git_path_bytes(repo, base, PACKET_PATH)
        head_packet_bytes = _git_path_bytes(repo, head, PACKET_PATH)
        base_packet = _json_object(base_packet_bytes, PACKET_PATH)
        head_packet = _json_object(head_packet_bytes, PACKET_PATH)
    except ValueError as exc:
        return {}, [str(exc)]
    transition = (base_packet.get("status"), head_packet.get("status"))
    phase = _phase_for_transition(*transition)
    if phase is None:
        return {}, [f"unsupported WP-0002 policy transition: {transition!r}"]

    if transition[0] != transition[1]:
        errors.extend(
            _validate_transition_receipt(
                repo,
                head,
                str(transition[0]),
                str(transition[1]),
                head_packet,
            )
        )

    raw = _run_git(repo, ["diff", "--raw", "-z", "--abbrev=40", "-M", "-C", f"{base}...{head}", "--"])
    if raw.returncode != 0:
        return {}, ["cannot derive exact base...head diff"]
    entries, parse_errors = _parse_raw_diff(raw.stdout)
    errors.extend(parse_errors)
    candidate_blobs: dict[str, bytes] = {}
    blob_hashes: dict[str, str] = {}
    for entry in entries:
        if entry.new_path is None or entry.new_mode == "000000":
            continue
        try:
            blob = _git_blob_bytes(repo, entry.new_oid)
        except ValueError as exc:
            errors.append(str(exc))
            continue
        candidate_blobs[entry.new_path] = blob
        blob_hashes[entry.new_path] = hashlib.sha256(blob).hexdigest()
    errors.extend(
        validate_delta(
            entries,
            phase=phase,
            declared_paths=base_packet.get("declared_paths", []),
            reservation_paths=base_packet.get("reservation", {}).get("paths", []),
            candidate_blobs=candidate_blobs,
        )
    )
    report: dict[str, object] = {
        "schema_version": 1,
        "contract": POLICY_CONTRACT,
        "enforcement_state": "policy-evaluation-only-external-capture-required",
        "phase": phase,
        "source": {
            "commit_sha1": policy_source_sha,
            "path": POLICY_PATH,
            "blob_sha256": hashlib.sha256(source_policy.stdout).hexdigest(),
        },
        "base": {
            "commit_sha1": base,
            "packet_sha256": hashlib.sha256(base_packet_bytes).hexdigest(),
        },
        "candidate": {
            "commit_sha1": head,
            "packet_sha256": hashlib.sha256(head_packet_bytes).hexdigest(),
        },
        "base_head_diff_sha256": hashlib.sha256(raw.stdout).hexdigest(),
        "changed_paths": sorted({path for entry in entries for path in (entry.old_path, entry.new_path) if path}),
        "candidate_blob_sha256": dict(sorted(blob_hashes.items())),
    }
    return _content_address_report(report, errors), errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", required=True)
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", required=True)
    parser.add_argument("--base-ref", required=True)
    parser.add_argument("--head-ref", required=True)
    parser.add_argument("--base-repository", required=True)
    parser.add_argument("--head-repository", required=True)
    parser.add_argument("--policy-source-sha", required=True)
    parser.add_argument("--fetch-head", action="store_true")
    parser.add_argument("--report-root", required=True)
    parser.add_argument("--report", required=True, choices=(POLICY_REPORT_PATH,))
    args = parser.parse_args(argv)
    report, errors = validate_repository_policy(
        Path(args.repo),
        base=args.base,
        head=args.head,
        base_ref=args.base_ref,
        head_ref=args.head_ref,
        base_repository=args.base_repository,
        head_repository=args.head_repository,
        policy_source_sha=args.policy_source_sha,
        fetch_head=args.fetch_head,
    )
    if not report:
        report = _content_address_report(
            {
                "schema_version": 1,
                "contract": POLICY_CONTRACT,
                "enforcement_state": "policy-evaluation-only-external-capture-required",
                "phase": "unresolved",
                "source": {"commit_sha1": args.policy_source_sha},
                "base": {"commit_sha1": args.base},
                "candidate": {"commit_sha1": args.head},
            },
            errors,
        )
    try:
        _write_report(Path(args.report_root), args.report, report)
    except (OSError, ValueError) as exc:
        print(f"ERROR: cannot write policy report: {exc}", file=sys.stderr)
        return 1
    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)
    if errors:
        return 1
    print(f"WP-0002 base-trusted policy: PASS {report['report_sha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
