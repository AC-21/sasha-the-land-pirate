#!/usr/bin/env python3
"""Collect and verify the local-only WP-0002 Stage-C scope attestation.

The collector is intentionally a local pre-Stage-C tool. It never copies
creator-owned file contents into evidence. It preserves only raw Git porcelain
bytes plus lstat, size, SHA-256, and protected-base blob metadata.
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
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath


CAPTURE_CONTRACT = "wp0002-working-tree-scope-capture-v2"
PACKET_ID = "WP-0002"
BOUNDARY_MANIFEST_ID = "A1B-WP-0002-LOCAL-DEV"
LOCAL_OUTPUT_ROOT = "BuildArtifacts/WP-0002/local-only/scope-capture"
RETAINED_OUTPUT_ROOT = "docs/evidence/WP-0002/scope-capture"
RETAINED_CAPTURE = f"{RETAINED_OUTPUT_ROOT}/working-tree-scope.json"
OUTPUT_ROOT = LOCAL_OUTPUT_ROOT
DEFAULT_OUTPUT = f"{LOCAL_OUTPUT_ROOT}/working-tree-scope.json"
PACKET_PATH = "docs/foundation-v0.1/work-packets/proposed/WP-0002.json"
BOUNDARY_PATH = "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json"
POLICY = "preserve-exclude-no-agent-modify-stage-commit-delete-revert-stash"
EXPECTED_DIRTY_STATES = {
    ".codex/config.toml": "unstaged-modified",
    "Game/ProjectSettings/ProjectSettings.asset": "unstaged-modified",
    "Game/ProjectSettings/SceneTemplateSettings.json": "untracked",
}
STATUS_ARGUMENTS = ("status", "--porcelain=v2", "-z")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
MAX_RECEIPT_DELAY_SECONDS = 600
MAX_LIVE_AGE_SECONDS = 300


def _json_bytes(value: object) -> bytes:
    return (
        json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n"
    ).encode("utf-8")


def _safe_relative(value: str) -> str:
    if not isinstance(value, str) or not value or "\\" in value:
        raise ValueError("evidence path must be a non-empty POSIX relative path")
    path = PurePosixPath(value)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        raise ValueError(f"unsafe repository-relative path: {value!r}")
    return path.as_posix()


def _require_real_root(repo_root: Path) -> Path:
    lexical = Path(os.path.abspath(os.fspath(repo_root)))
    metadata = os.lstat(lexical)
    if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
        raise ValueError("repository root must be a real directory, not a symlink")
    return lexical


def _captured_repository_root(value: object) -> tuple[str | None, list[str]]:
    """Validate a collection-time root without requiring it to exist locally."""
    if (
        not isinstance(value, str)
        or not value
        or "\x00" in value
        or "\\" in value
        or value == "/"
        or value.startswith("//")
    ):
        return None, [
            "scope capture repository_root is not a canonical absolute POSIX path"
        ]
    path = PurePosixPath(value)
    if (
        not path.is_absolute()
        or path.as_posix() != value
        or any(part in {".", ".."} for part in path.parts)
    ):
        return None, [
            "scope capture repository_root is not a canonical absolute POSIX path"
        ]
    return value, []


def _artifact_relative(value: str) -> str:
    relative = _safe_relative(value)
    retained_artifact = re.fullmatch(
        re.escape(f"{RETAINED_OUTPUT_ROOT}/working-tree-scope.")
        + r"(?:status\.[0-9a-f]{64}\.bin|observations\.[0-9a-f]{64}\.json)",
        relative,
    )
    if not (
        relative.startswith(f"{LOCAL_OUTPUT_ROOT}/")
        or relative == RETAINED_CAPTURE
        or retained_artifact is not None
    ):
        raise ValueError(
            "scope evidence must use the local-only root or one exact retained WP-0002 filename"
        )
    return relative


def _directory_flags() -> int:
    return (
        os.O_RDONLY
        | getattr(os, "O_DIRECTORY", 0)
        | getattr(os, "O_NOFOLLOW", 0)
        | getattr(os, "O_CLOEXEC", 0)
    )


def _walk_parent_fd(
    repo_root: Path,
    relative: str,
    *,
    create: bool,
) -> tuple[int, str]:
    root = _require_real_root(repo_root)
    parts = PurePosixPath(_safe_relative(relative)).parts
    if not parts:
        raise ValueError("artifact path must name a file")
    directory_fd = os.open(root, _directory_flags())
    try:
        for component in parts[:-1]:
            if create:
                try:
                    os.mkdir(component, 0o700, dir_fd=directory_fd)
                except FileExistsError:
                    pass
            next_fd = os.open(component, _directory_flags(), dir_fd=directory_fd)
            if not stat.S_ISDIR(os.fstat(next_fd).st_mode):
                os.close(next_fd)
                raise ValueError(
                    f"artifact parent component {component!r} is not a directory"
                )
            os.close(directory_fd)
            directory_fd = next_fd
        return directory_fd, parts[-1]
    except Exception:
        os.close(directory_fd)
        raise


def _write_atomic_confined(repo_root: Path, relative: str, data: bytes) -> None:
    relative = _artifact_relative(relative)
    directory_fd, destination_name = _walk_parent_fd(
        repo_root, relative, create=True
    )
    temporary_name = (
        f".{destination_name}.tmp-{os.getpid()}-{secrets.token_hex(8)}"
    )
    temporary_fd = -1
    try:
        try:
            destination = os.stat(
                destination_name, dir_fd=directory_fd, follow_symlinks=False
            )
        except FileNotFoundError:
            pass
        else:
            if not stat.S_ISREG(destination.st_mode):
                raise ValueError(
                    "scope evidence destination must be a regular non-symlink file"
                )
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
        offset = 0
        while offset < len(data):
            written = os.write(temporary_fd, data[offset:])
            if written <= 0:
                raise OSError("short write while preserving scope evidence")
            offset += written
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
        os.close(directory_fd)


def _read_confined(repo_root: Path, relative: str) -> tuple[bytes, os.stat_result]:
    relative = _safe_relative(relative)
    directory_fd, name = _walk_parent_fd(repo_root, relative, create=False)
    file_fd = -1
    try:
        file_fd = os.open(
            name,
            os.O_RDONLY
            | getattr(os, "O_NOFOLLOW", 0)
            | getattr(os, "O_CLOEXEC", 0),
            dir_fd=directory_fd,
        )
        before = os.fstat(file_fd)
        if not stat.S_ISREG(before.st_mode):
            raise ValueError(f"{relative} is not a regular non-symlink file")
        chunks: list[bytes] = []
        while True:
            chunk = os.read(file_fd, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
        after = os.fstat(file_fd)
        if (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) != (
            after.st_dev,
            after.st_ino,
            after.st_size,
            after.st_mtime_ns,
        ):
            raise ValueError(f"{relative} changed while it was inspected")
        return b"".join(chunks), after
    finally:
        if file_fd >= 0:
            os.close(file_fd)
        os.close(directory_fd)


def _run_git(repo_root: Path, args: list[str]) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        [
            "/usr/bin/git",
            "-c",
            "core.hooksPath=/dev/null",
            "-C",
            str(_require_real_root(repo_root)),
            *args,
        ],
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
        timeout=60,
        env={
            "PATH": "/usr/bin:/bin",
            "LANG": "C",
            "LC_ALL": "C",
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_TERMINAL_PROMPT": "0",
        },
    )


def _git_output(repo_root: Path, args: list[str], label: str) -> bytes:
    result = _run_git(repo_root, args)
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", "replace").strip()
        raise ValueError(f"cannot collect {label}: {detail or result.returncode}")
    return result.stdout


def _decode_path(raw: bytes) -> str:
    return _safe_relative(raw.decode("utf-8"))


def parse_porcelain_v2_z(raw: bytes) -> tuple[list[dict[str, str]], list[str]]:
    """Parse the exact status transport without normalizing away record types."""
    entries: list[dict[str, str]] = []
    errors: list[str] = []
    fields = raw.split(b"\0")
    index = 0
    while index < len(fields) and fields[index]:
        record = fields[index]
        try:
            kind = chr(record[0])
            if kind == "?":
                entries.append(
                    {
                        "path": _decode_path(record[2:]),
                        "normalized_git_state": "untracked",
                        "record_kind": "?",
                        "xy": "??",
                    }
                )
                index += 1
                continue
            if kind == "1":
                parts = record.split(b" ", 8)
                if len(parts) != 9:
                    raise ValueError("ordinary record has the wrong field count")
                xy = parts[1].decode("ascii")
                state = (
                    "unstaged-modified"
                    if xy == ".M"
                    else f"unsupported-{xy}"
                )
                entries.append(
                    {
                        "path": _decode_path(parts[8]),
                        "normalized_git_state": state,
                        "record_kind": "1",
                        "xy": xy,
                    }
                )
                index += 1
                continue
            if kind == "2":
                parts = record.split(b" ", 9)
                if len(parts) != 10 or index + 1 >= len(fields):
                    raise ValueError("rename/copy record has the wrong field count")
                entries.append(
                    {
                        "path": _decode_path(parts[9]),
                        "original_path": _decode_path(fields[index + 1]),
                        "normalized_git_state": "unsupported-rename-copy",
                        "record_kind": "2",
                        "xy": parts[1].decode("ascii"),
                    }
                )
                index += 2
                continue
            if kind == "u":
                parts = record.split(b" ", 10)
                if len(parts) != 11:
                    raise ValueError("unmerged record has the wrong field count")
                entries.append(
                    {
                        "path": _decode_path(parts[10]),
                        "normalized_git_state": "unsupported-conflict",
                        "record_kind": "u",
                        "xy": parts[1].decode("ascii"),
                    }
                )
                index += 1
                continue
            raise ValueError(f"unsupported porcelain record kind {kind!r}")
        except (IndexError, UnicodeDecodeError, ValueError) as exc:
            errors.append(f"cannot parse raw porcelain-v2 record {index}: {exc}")
            break
    if fields and fields[-1] != b"":
        errors.append("raw porcelain-v2 transport is not NUL terminated")
    return entries, errors


def _submodule_paths(repo_root: Path) -> list[str]:
    raw = _git_output(repo_root, ["ls-files", "-s", "-z"], "index modes")
    paths: list[str] = []
    for record in raw.split(b"\0"):
        if not record:
            continue
        metadata, separator, raw_path = record.partition(b"\t")
        if not separator:
            raise ValueError("cannot parse index mode record")
        if metadata.split(b" ", 1)[0] == b"160000":
            paths.append(_decode_path(raw_path))
    return sorted(paths)


def _git_facts(repo_root: Path, raw_status: bytes | None = None) -> dict:
    status = (
        raw_status
        if raw_status is not None
        else _git_output(repo_root, list(STATUS_ARGUMENTS), "raw porcelain-v2 status")
    )
    entries, status_errors = parse_porcelain_v2_z(status)
    if status_errors:
        raise ValueError("; ".join(status_errors))
    head = _git_output(repo_root, ["rev-parse", "--verify", "HEAD"], "HEAD")
    head_commit = head.decode("ascii").strip()
    head_tree = _git_output(
        repo_root, ["rev-parse", "HEAD^{tree}"], "HEAD tree"
    ).decode("ascii").strip()
    index_tree = _git_output(repo_root, ["write-tree"], "index tree").decode(
        "ascii"
    ).strip()
    index_result = _run_git(repo_root, ["diff", "--cached", "--quiet", "HEAD", "--"])
    if index_result.returncode not in {0, 1}:
        raise ValueError("cannot determine whether the Git index is clean")
    return {
        "head_commit": head_commit,
        "head_tree": head_tree,
        "index_tree": index_tree,
        "index_clean": index_result.returncode == 0,
        "conflict_paths": sorted(
            entry["path"] for entry in entries if entry["record_kind"] == "u"
        ),
        "submodule_paths": _submodule_paths(repo_root),
    }


def _base_blob(repo_root: Path, base_commit: str, relative: str) -> dict:
    result = _run_git(repo_root, ["show", f"{base_commit}:{relative}"])
    if result.returncode != 0:
        return {"exists": False, "sha256": None, "byte_size": None}
    return {
        "exists": True,
        "sha256": hashlib.sha256(result.stdout).hexdigest(),
        "byte_size": len(result.stdout),
    }


def _file_observation(
    repo_root: Path,
    relative: str,
    normalized_state: str,
    base_commit: str,
) -> tuple[dict, bytes]:
    content, metadata = _read_confined(repo_root, relative)
    observation = {
        "path": relative,
        "normalized_git_state": normalized_state,
        "lstat": {
            "mode": f"{metadata.st_mode:o}",
            "size": metadata.st_size,
            "uid": metadata.st_uid,
            "gid": metadata.st_gid,
            "device": metadata.st_dev,
            "inode": metadata.st_ino,
            "mtime_ns": metadata.st_mtime_ns,
            "regular_file": stat.S_ISREG(metadata.st_mode),
            "symlink": stat.S_ISLNK(metadata.st_mode),
        },
        "file_bytes": {
            "byte_size": len(content),
            "sha256": hashlib.sha256(content).hexdigest(),
            "read_method": "openat-O_NOFOLLOW-fstat-before-after",
            "content_retained": False,
        },
        "base_blob": _base_blob(repo_root, base_commit, relative),
        "owner": "creator",
        "policy": POLICY,
    }
    return observation, content


def _artifact_reference(relative: str, data: bytes) -> dict:
    return {
        "path": relative,
        "sha256": hashlib.sha256(data).hexdigest(),
        "byte_size": len(data),
    }


def _assert_expected_status(entries: list[dict[str, str]]) -> list[str]:
    errors: list[str] = []
    actual: dict[str, str] = {}
    for entry in entries:
        path = entry["path"]
        if path in actual:
            errors.append(f"raw status repeats dirty path {path}")
        actual[path] = entry["normalized_git_state"]
        if "original_path" in entry:
            errors.append(f"raw status contains rename/copy origin {entry['original_path']}")
    if actual != EXPECTED_DIRTY_STATES:
        errors.append(
            f"raw status dirty set differs: expected {EXPECTED_DIRTY_STATES}, found {actual}"
        )
    return errors


def collect_scope_capture(
    repo_root: Path,
    *,
    base_commit: str,
    checkpoint_commit: str,
    reservation_paths: list[str],
    protected_paths_read_only: list[str],
    output_relative: str = DEFAULT_OUTPUT,
    captured_at: datetime | None = None,
) -> dict:
    """Collect the exact metadata-only WP-0002 activation-scope proof."""
    repo_root = _require_real_root(repo_root)
    output_relative = _artifact_relative(output_relative)
    if not COMMIT_RE.fullmatch(base_commit) or not COMMIT_RE.fullmatch(
        checkpoint_commit
    ):
        raise ValueError("base and checkpoint commits must be immutable 40-hex SHAs")
    raw_status = _git_output(
        repo_root, list(STATUS_ARGUMENTS), "raw porcelain-v2 status"
    )
    status_entries, status_errors = parse_porcelain_v2_z(raw_status)
    status_errors.extend(_assert_expected_status(status_entries))
    if status_errors:
        raise ValueError("; ".join(status_errors))
    facts = _git_facts(repo_root, raw_status)
    if facts["head_commit"] != checkpoint_commit:
        raise ValueError("checkpoint commit must equal current HEAD at collection")
    if not facts["index_clean"]:
        raise ValueError("WP-0002 scope capture requires a clean Git index")
    if facts["conflict_paths"] or facts["submodule_paths"]:
        raise ValueError("scope capture rejects conflicts and submodules")

    observations: list[dict] = []
    creator_contents: list[bytes] = []
    for relative, state in EXPECTED_DIRTY_STATES.items():
        observation, content = _file_observation(
            repo_root, relative, state, base_commit
        )
        if state == "untracked" and observation["base_blob"]["exists"]:
            raise ValueError(f"untracked path exists in protected base: {relative}")
        if state != "untracked" and not observation["base_blob"]["exists"]:
            raise ValueError(f"tracked drift is absent from protected base: {relative}")
        if (
            observation["lstat"]["regular_file"] is not True
            or observation["lstat"]["symlink"] is not False
        ):
            raise ValueError(f"creator drift is not a regular non-symlink: {relative}")
        observations.append(observation)
        creator_contents.append(content)

    captured_at = captured_at or datetime.now(timezone.utc)
    if captured_at.tzinfo is None:
        raise ValueError("capture timestamp must be timezone-aware")
    timestamp = captured_at.astimezone(timezone.utc).isoformat().replace(
        "+00:00", "Z"
    )
    stem = PurePosixPath(output_relative).stem
    parent = PurePosixPath(output_relative).parent.as_posix()
    raw_hash = hashlib.sha256(raw_status).hexdigest()
    retained = output_relative == RETAINED_CAPTURE
    if output_relative.startswith(f"{RETAINED_OUTPUT_ROOT}/") and not retained:
        raise ValueError(
            f"retained scope capture must use exact path {RETAINED_CAPTURE}"
        )
    raw_relative = f"{parent}/{stem}.status.{raw_hash}.bin"
    observations_document = {
        "schema_version": 1,
        "capture_contract": CAPTURE_CONTRACT,
        "captured_at": timestamp,
        "content_policy": "metadata-only-no-creator-file-bytes",
        "observations": observations,
    }
    observations_bytes = _json_bytes(observations_document)
    observations_hash = hashlib.sha256(observations_bytes).hexdigest()
    observations_relative = f"{parent}/{stem}.observations.{observations_hash}.json"

    derived_dirty = [
        {
            "path": item["path"],
            "normalized_git_state": item["normalized_git_state"],
            "base_blob_sha256": item["base_blob"]["sha256"],
            "observed_sha256": item["file_bytes"]["sha256"],
            "regular_file_no_symlink": (
                item["lstat"]["regular_file"] is True
                and item["lstat"]["symlink"] is False
            ),
            "owner": "creator",
            "policy": POLICY,
        }
        for item in observations
    ]
    git_version = _git_output(repo_root, ["--version"], "Git version").decode(
        "ascii"
    ).strip()
    capture = {
        "schema_version": 2,
        "capture_contract": CAPTURE_CONTRACT,
        "packet_id": PACKET_ID,
        "boundary_manifest_id": BOUNDARY_MANIFEST_ID,
        "captured_at": timestamp,
        "repository_root": str(repo_root),
        "base_commit": base_commit,
        "head_commit": facts["head_commit"],
        "head_tree": facts["head_tree"],
        "checkpoint_commit": checkpoint_commit,
        "index_tree": facts["index_tree"],
        "status_format": "git-status-porcelain-v2-z-raw",
        "collector": {
            "git_version": git_version,
            "status_command": [
                "/usr/bin/git",
                "-c",
                "core.hooksPath=/dev/null",
                "-C",
                str(repo_root),
                *STATUS_ARGUMENTS,
            ],
            "head_command": ["git", "rev-parse", "--verify", "HEAD"],
            "head_tree_command": ["git", "rev-parse", "HEAD^{tree}"],
            "index_tree_command": ["git", "write-tree"],
            "submodule_command": ["git", "ls-files", "-s", "-z"],
        },
        "artifacts": {
            "raw_status": _artifact_reference(raw_relative, raw_status),
            "observations": _artifact_reference(
                observations_relative, observations_bytes
            ),
        },
        "index_clean": facts["index_clean"],
        "conflict_paths": facts["conflict_paths"],
        "submodule_paths": facts["submodule_paths"],
        "complete_dirty_set": True,
        "dirty_path_count": len(derived_dirty),
        "dirty_paths": derived_dirty,
        "non_excluded_scope_clean": True,
        "reserved_scope_clean": True,
        "reservation_paths": list(reservation_paths),
        "protected_paths_read_only": list(protected_paths_read_only),
        "reserved_protected_overlaps": [],
        "privacy": {
            "creator_file_content_retained": False,
            "secret_scan_method": "assert-no-creator-file-byte-sequence-in-artifacts-min-16-bytes",
            "secret_scan_result": "pass",
        },
    }
    capture_bytes = _json_bytes(capture)
    evidence_without_creator_content = raw_status + observations_bytes + capture_bytes
    for content in creator_contents:
        if len(content) >= 16 and content in evidence_without_creator_content:
            raise ValueError("creator file bytes leaked into scope evidence")

    _write_atomic_confined(repo_root, raw_relative, raw_status)
    _write_atomic_confined(repo_root, observations_relative, observations_bytes)
    _write_atomic_confined(repo_root, output_relative, capture_bytes)
    return {
        "capture": capture,
        "path": output_relative,
        "sha256": hashlib.sha256(capture_bytes).hexdigest(),
        "byte_size": len(capture_bytes),
    }


def _resolve_artifact(repo_root: Path, reference: object, label: str) -> tuple[bytes | None, list[str]]:
    errors: list[str] = []
    if not isinstance(reference, dict):
        return None, [f"{label} reference is missing"]
    path = reference.get("path")
    expected_hash = reference.get("sha256")
    expected_size = reference.get("byte_size")
    if not isinstance(path, str):
        return None, [f"{label} path is invalid"]
    try:
        path = _artifact_relative(path)
        raw, _metadata = _read_confined(repo_root, path)
    except (OSError, ValueError) as exc:
        return None, [f"{label} cannot be resolved: {exc}"]
    actual_hash = hashlib.sha256(raw).hexdigest()
    if not SHA256_RE.fullmatch(str(expected_hash)) or expected_hash != actual_hash:
        errors.append(f"{label} SHA-256 differs from its content-addressed reference")
    if (
        isinstance(expected_hash, str)
        and expected_hash not in PurePosixPath(path).name
    ):
        errors.append(f"{label} filename does not include its content hash")
    if (
        not isinstance(expected_size, int)
        or isinstance(expected_size, bool)
        or expected_size != len(raw)
    ):
        errors.append(f"{label} byte size differs from its reference")
    return raw, errors


def _parse_time(value: object, label: str) -> tuple[datetime | None, list[str]]:
    if not isinstance(value, str):
        return None, [f"{label} is missing"]
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None, [f"{label} is not an ISO-8601 timestamp"]
    if parsed.tzinfo is None:
        return None, [f"{label} must include a timezone"]
    return parsed.astimezone(timezone.utc), []


def verify_scope_capture(
    repo_root: Path,
    capture_relative: str,
    *,
    expected_capture_sha256: str,
    expected_base_commit: str,
    expected_head_commit: str,
    expected_checkpoint_commit: str,
    expected_reservation_paths: list[str],
    expected_protected_paths: list[str],
    receipt_issued_at: str,
    mode: str,
    now: datetime | None = None,
) -> list[str]:
    """Verify a capture in live-current or immutable terminal-retained mode."""
    errors: list[str] = []
    if mode not in {"live-current", "terminal-retained"}:
        return ["scope capture mode must be live-current or terminal-retained"]
    try:
        capture_relative = _artifact_relative(capture_relative)
        if (
            capture_relative.startswith(f"{RETAINED_OUTPUT_ROOT}/")
            and capture_relative != RETAINED_CAPTURE
        ):
            raise ValueError(
                f"retained derived capture must use exact path {RETAINED_CAPTURE}"
            )
        capture_bytes, _metadata = _read_confined(repo_root, capture_relative)
    except (OSError, ValueError) as exc:
        return [f"scope capture cannot be resolved: {exc}"]
    capture_hash = hashlib.sha256(capture_bytes).hexdigest()
    if expected_capture_sha256 != capture_hash:
        errors.append("scope capture SHA-256 differs from its protected reference")
    try:
        capture = json.loads(capture_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        return errors + [f"scope capture is not valid UTF-8 JSON: {exc}"]
    if not isinstance(capture, dict):
        return errors + ["scope capture must be an object"]

    current_repository_root = str(_require_real_root(repo_root))
    captured_repository_root, repository_root_errors = _captured_repository_root(
        capture.get("repository_root")
    )
    errors.extend(repository_root_errors)
    # A live proof is about this checkout. A terminal proof is about immutable,
    # receipt-bound collection-time facts and may be verified in another clone.
    expected_repository_root = (
        current_repository_root
        if mode == "live-current"
        else captured_repository_root
    )

    exact_fields = {
        "schema_version": 2,
        "capture_contract": CAPTURE_CONTRACT,
        "packet_id": PACKET_ID,
        "boundary_manifest_id": BOUNDARY_MANIFEST_ID,
        "repository_root": expected_repository_root,
        "base_commit": expected_base_commit,
        "head_commit": expected_head_commit,
        "checkpoint_commit": expected_checkpoint_commit,
        "status_format": "git-status-porcelain-v2-z-raw",
        "index_clean": True,
        "conflict_paths": [],
        "submodule_paths": [],
        "complete_dirty_set": True,
        "dirty_path_count": 3,
        "non_excluded_scope_clean": True,
        "reserved_scope_clean": True,
        "reservation_paths": expected_reservation_paths,
        "protected_paths_read_only": expected_protected_paths,
        "reserved_protected_overlaps": [],
        "privacy": {
            "creator_file_content_retained": False,
            "secret_scan_method": "assert-no-creator-file-byte-sequence-in-artifacts-min-16-bytes",
            "secret_scan_result": "pass",
        },
    }
    for field, expected in exact_fields.items():
        if capture.get(field) != expected:
            errors.append(f"scope capture {field} differs from the exact boundary")
    collector = capture.get("collector")
    expected_status_command = [
        "/usr/bin/git",
        "-c",
        "core.hooksPath=/dev/null",
        "-C",
        expected_repository_root,
        *STATUS_ARGUMENTS,
    ]
    expected_collector_fields = {
        "git_version",
        "status_command",
        "head_command",
        "head_tree_command",
        "index_tree_command",
        "submodule_command",
    }
    if not isinstance(collector, dict) or set(collector) != expected_collector_fields:
        errors.append("scope capture collector tuple is not exact")
    else:
        expected_commands = {
            "status_command": expected_status_command,
            "head_command": ["git", "rev-parse", "--verify", "HEAD"],
            "head_tree_command": ["git", "rev-parse", "HEAD^{tree}"],
            "index_tree_command": ["git", "write-tree"],
            "submodule_command": ["git", "ls-files", "-s", "-z"],
        }
        for field, expected in expected_commands.items():
            if collector.get(field) != expected:
                errors.append(f"scope capture collector {field} differs")
        if not isinstance(collector.get("git_version"), str) or not collector[
            "git_version"
        ].startswith("git version "):
            errors.append("scope capture Git version is invalid")
    if not COMMIT_RE.fullmatch(str(capture.get("head_tree"))) or not COMMIT_RE.fullmatch(
        str(capture.get("index_tree"))
    ):
        errors.append("scope capture head/index tree facts are invalid")

    artifacts = capture.get("artifacts")
    if not isinstance(artifacts, dict):
        return errors + ["scope capture artifact map is missing"]
    capture_parent = PurePosixPath(capture_relative).parent.as_posix()
    if capture_relative == RETAINED_CAPTURE:
        retained_suffixes = {
            "raw_status": ("status", "bin"),
            "observations": ("observations", "json"),
        }
        for name, (kind, extension) in retained_suffixes.items():
            reference = artifacts.get(name)
            reference_hash = (
                reference.get("sha256") if isinstance(reference, dict) else None
            )
            expected_path = (
                f"{RETAINED_OUTPUT_ROOT}/working-tree-scope."
                f"{kind}.{reference_hash}.{extension}"
            )
            if not isinstance(reference, dict) or reference.get("path") != expected_path:
                errors.append(
                    f"retained {name} artifact must use exact path {expected_path}"
                )
    for name in ("raw_status", "observations"):
        reference = artifacts.get(name)
        if (
            not isinstance(reference, dict)
            or not isinstance(reference.get("path"), str)
            or PurePosixPath(reference["path"]).parent.as_posix() != capture_parent
        ):
            errors.append(f"{name} artifact is not a sibling of the derived capture")
    raw_status, raw_errors = _resolve_artifact(
        repo_root, artifacts.get("raw_status"), "raw porcelain-v2 artifact"
    )
    observations_raw, observation_errors = _resolve_artifact(
        repo_root, artifacts.get("observations"), "file observations artifact"
    )
    errors.extend(raw_errors)
    errors.extend(observation_errors)
    if raw_status is None or observations_raw is None:
        return errors
    entries, parse_errors = parse_porcelain_v2_z(raw_status)
    errors.extend(parse_errors)
    errors.extend(_assert_expected_status(entries))
    try:
        observations_document = json.loads(observations_raw.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        return errors + [f"file observations artifact is invalid JSON: {exc}"]
    if not isinstance(observations_document, dict):
        return errors + ["file observations artifact must be an object"]
    if set(observations_document) != {
        "schema_version",
        "capture_contract",
        "captured_at",
        "content_policy",
        "observations",
    }:
        errors.append("file observations document contains unapproved fields")
    if (
        observations_document.get("capture_contract") != CAPTURE_CONTRACT
        or observations_document.get("captured_at") != capture.get("captured_at")
        or observations_document.get("content_policy")
        != "metadata-only-no-creator-file-bytes"
    ):
        errors.append("file observations metadata differs from the capture")
    observations = observations_document.get("observations")
    if not isinstance(observations, list):
        return errors + ["file observations list is missing"]
    observations_by_path = {
        item.get("path"): item for item in observations if isinstance(item, dict)
    }
    if set(observations_by_path) != set(EXPECTED_DIRTY_STATES):
        errors.append("file observations omit or add creator-owned dirty paths")

    derived_dirty: list[dict] = []
    for relative, expected_state in EXPECTED_DIRTY_STATES.items():
        observation = observations_by_path.get(relative)
        if not isinstance(observation, dict):
            continue
        lstat_claim = observation.get("lstat")
        bytes_claim = observation.get("file_bytes")
        base_claim = observation.get("base_blob")
        if not all(isinstance(item, dict) for item in (lstat_claim, bytes_claim, base_claim)):
            errors.append(f"file observation structure is invalid for {relative}")
            continue
        if set(observation) != {
            "path",
            "normalized_git_state",
            "lstat",
            "file_bytes",
            "base_blob",
            "owner",
            "policy",
        }:
            errors.append(f"file observation contains unapproved fields for {relative}")
        if set(lstat_claim) != {
            "mode",
            "size",
            "uid",
            "gid",
            "device",
            "inode",
            "mtime_ns",
            "regular_file",
            "symlink",
        }:
            errors.append(f"lstat observation contains unapproved fields for {relative}")
        if set(bytes_claim) != {
            "byte_size",
            "sha256",
            "read_method",
            "content_retained",
        }:
            errors.append(f"file-byte observation contains unapproved fields for {relative}")
        if set(base_claim) != {"exists", "sha256", "byte_size"}:
            errors.append(f"base-blob observation contains unapproved fields for {relative}")
        if observation.get("normalized_git_state") != expected_state:
            errors.append(f"file observation state differs for {relative}")
        if (
            lstat_claim.get("regular_file") is not True
            or lstat_claim.get("symlink") is not False
            or bytes_claim.get("content_retained") is not False
            or bytes_claim.get("read_method")
            != "openat-O_NOFOLLOW-fstat-before-after"
            or observation.get("owner") != "creator"
            or observation.get("policy") != POLICY
        ):
            errors.append(f"file observation safety facts are invalid for {relative}")
        actual_base = _base_blob(repo_root, expected_base_commit, relative)
        if base_claim != actual_base:
            errors.append(f"base blob observation differs for {relative}")
        if expected_state == "untracked" and actual_base["exists"]:
            errors.append(f"untracked path exists in protected base: {relative}")
        if expected_state != "untracked" and not actual_base["exists"]:
            errors.append(f"tracked path is absent from protected base: {relative}")
        derived_dirty.append(
            {
                "path": relative,
                "normalized_git_state": expected_state,
                "base_blob_sha256": base_claim.get("sha256"),
                "observed_sha256": bytes_claim.get("sha256"),
                "regular_file_no_symlink": (
                    lstat_claim.get("regular_file") is True
                    and lstat_claim.get("symlink") is False
                ),
                "owner": "creator",
                "policy": POLICY,
            }
        )
        if mode == "live-current":
            try:
                current, metadata = _read_confined(repo_root, relative)
            except (OSError, ValueError) as exc:
                errors.append(f"cannot recompute live observation for {relative}: {exc}")
                continue
            current_facts = {
                "mode": f"{metadata.st_mode:o}",
                "size": metadata.st_size,
                "uid": metadata.st_uid,
                "gid": metadata.st_gid,
                "device": metadata.st_dev,
                "inode": metadata.st_ino,
                "mtime_ns": metadata.st_mtime_ns,
                "regular_file": stat.S_ISREG(metadata.st_mode),
                "symlink": stat.S_ISLNK(metadata.st_mode),
            }
            if lstat_claim != current_facts:
                errors.append(f"live lstat observation drifted for {relative}")
            if (
                bytes_claim.get("byte_size") != len(current)
                or bytes_claim.get("sha256") != hashlib.sha256(current).hexdigest()
            ):
                errors.append(f"live file-byte observation drifted for {relative}")
            if len(current) >= 16 and current in (
                capture_bytes + raw_status + observations_raw
            ):
                errors.append(f"creator file bytes leaked into evidence for {relative}")

    if capture.get("dirty_paths") != derived_dirty:
        errors.append("derived dirty JSON is not a faithful projection of raw observations")

    capture_time, time_errors = _parse_time(capture.get("captured_at"), "captured_at")
    receipt_time, receipt_errors = _parse_time(receipt_issued_at, "receipt issued_at")
    errors.extend(time_errors)
    errors.extend(receipt_errors)
    if capture_time is not None and receipt_time is not None:
        delay = (receipt_time - capture_time).total_seconds()
        if delay < 0 or delay > MAX_RECEIPT_DELAY_SECONDS:
            errors.append("scope capture is not fresh relative to its activation receipt")
    if mode == "live-current" and capture_time is not None:
        current_time = (now or datetime.now(timezone.utc)).astimezone(timezone.utc)
        age = (current_time - capture_time).total_seconds()
        if age < 0 or age > MAX_LIVE_AGE_SECONDS:
            errors.append("live scope capture is stale relative to verification time")

    for commit, label in (
        (expected_base_commit, "base"),
        (expected_head_commit, "head"),
        (expected_checkpoint_commit, "checkpoint"),
    ):
        if not COMMIT_RE.fullmatch(commit) or _run_git(
            repo_root, ["cat-file", "-e", f"{commit}^{{commit}}"]
        ).returncode != 0:
            errors.append(f"captured {label} commit cannot be resolved")
    head_tree = _run_git(repo_root, ["rev-parse", f"{expected_head_commit}^{{tree}}"])
    if (
        head_tree.returncode != 0
        or capture.get("head_tree") != head_tree.stdout.decode("ascii").strip()
    ):
        errors.append("captured head tree differs from the immutable head commit")

    if mode == "live-current":
        live_status = _git_output(
            repo_root, list(STATUS_ARGUMENTS), "live raw porcelain-v2 status"
        )
        if live_status != raw_status:
            errors.append("live raw porcelain-v2 bytes differ from the capture")
        live_facts = _git_facts(repo_root, live_status)
        live_git_version = _git_output(repo_root, ["--version"], "live Git version").decode(
            "ascii"
        ).strip()
        if isinstance(collector, dict) and collector.get("git_version") != live_git_version:
            errors.append("live Git version differs from the captured collector version")
        fact_fields = (
            "head_commit",
            "head_tree",
            "index_tree",
            "index_clean",
            "conflict_paths",
            "submodule_paths",
        )
        for field in fact_fields:
            if live_facts[field] != capture.get(field):
                errors.append(f"live Git fact drifted: {field}")
    return errors


def _load_json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path} must contain a JSON object")
    return value


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True)
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument("--output", default=DEFAULT_OUTPUT)
    parser.add_argument("--ack-local-pre-stage-c", action="store_true")
    args = parser.parse_args()
    repo_root = Path(__file__).resolve().parents[2]
    try:
        if not args.ack_local_pre_stage_c:
            raise ValueError("collector requires explicit local pre-Stage-C acknowledgement")
        if os.environ.get("CI") or os.environ.get("GITHUB_ACTIONS"):
            raise ValueError("scope collector is forbidden in CI")
        branch = _git_output(
            repo_root, ["symbolic-ref", "--short", "HEAD"], "agent branch"
        ).decode("utf-8").strip()
        if not branch.startswith("agent/"):
            raise ValueError("scope collector requires an agent/* branch")
        base_packet_raw = _git_output(
            repo_root, ["show", f"HEAD:{PACKET_PATH}"], "protected-base WP-0002 packet"
        )
        base_packet = json.loads(base_packet_raw.decode("utf-8"))
        if not isinstance(base_packet, dict) or base_packet.get("status") != "accepted":
            raise ValueError("collector runs only from an accepted pre-Stage-C WP-0002 base")
        packet = _load_json(repo_root / PACKET_PATH)
        boundary = _load_json(repo_root / BOUNDARY_PATH)
        reservation_paths = packet.get("reservation", {}).get("paths")
        protected_paths = boundary.get("permission_boundary", {}).get(
            "protected_paths_read_only"
        )
        if not isinstance(reservation_paths, list) or not isinstance(
            protected_paths, list
        ):
            raise ValueError("packet or boundary paths are unavailable")
        result = collect_scope_capture(
            repo_root,
            base_commit=args.base,
            checkpoint_commit=args.checkpoint,
            reservation_paths=reservation_paths,
            protected_paths_read_only=protected_paths,
            output_relative=args.output,
        )
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"WP-0002 SCOPE CAPTURE: FAIL: {exc}", file=sys.stderr)
        return 1
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
