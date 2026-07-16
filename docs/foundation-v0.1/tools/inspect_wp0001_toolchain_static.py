#!/usr/bin/env python3
"""Static, A0-only inspection of the WP-0001 host toolchain.

This collector reads bounded filesystem metadata and file bytes. It never
starts Unity, Unity Hub, an Editor, the relay, Codex, MCP, Xcode, or dotnet; it
never opens a network connection; and its output has no activation authority.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import plistlib
import re
import stat
import struct
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
from typing import Any, Iterable


PACKET_ID = "WP-0001"
PACKET_CONTRACT_SHA256 = (
    "eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da02abfe008faeda3"
)
COLLECTOR_NAME = "inspect_wp0001_toolchain_static"
COLLECTOR_VERSION = "wp0001-static-toolchain-v1"
DOCUMENT_KIND = "wp0001-static-host-toolchain-observation"

REQUIRED_HUB_VERSION = "3.19.5"
REQUIRED_EDITOR_VERSION = "6000.3.19f1"
REQUIRED_EDITOR_CHANGESET = "7689f4515d75"
REQUIRED_XCODE_VERSION = "26.3"
REQUIRED_DOTNET_SDK = "10.0.301"
REQUIRED_ASSISTANT = "2.14.0-pre.1"
REQUIRED_URP_PATTERN = re.compile(
    r"17\.3(?:\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?)?"
)
REQUIRED_TEST_PATTERN = re.compile(
    r"1\.6(?:\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?)?"
)
REGISTRY_VERSION_PATTERN = re.compile(
    r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?"
)
UNITY_EDITOR_DIRECTORY_PATTERN = re.compile(
    r"[0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+"
)
GIT_OBJECT_PATTERN = re.compile(r"[0-9a-f]{40}")
SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")
SECRET_VALUE_PATTERN = re.compile(
    r"(?i)(?:"
    r"bearer\s+[a-z0-9._-]{12,}|"
    r"sk-(?:proj-|svcacct-)?[a-z0-9_-]{12,}|"
    r"(?:ghp|github_pat|rk_live|sk_live)_[a-z0-9_-]{12,}|"
    r"xox[baprs]-[a-z0-9-]{12,}|"
    r"(?:AKIA|ASIA)[A-Z0-9]{16}|"
    r"eyJ[a-z0-9_-]{10,}\.eyJ[a-z0-9_-]{10,}\.[a-z0-9_-]{8,}|"
    r"-----BEGIN [A-Z ]*PRIVATE KEY-----|"
    r"(?:password|passwd|token)\s*[:=]\s*\S{8,}"
    r")"
)

MAX_PLIST_BYTES = 4 * 1024 * 1024
MAX_JSON_BYTES = 16 * 1024 * 1024
MAX_EXECUTABLE_BYTES = 512 * 1024 * 1024
MAX_MARKER_BYTES = 512 * 1024 * 1024
MAX_DIRECTORY_ENTRIES = 4096

BLOCKING_STATUSES = {"mismatch", "missing", "unsafe"}
REQUIREMENT_STATUSES = {
    "matched",
    "mismatch",
    "missing",
    "unverified",
    "unsafe",
}
EXPECTED_REQUIREMENT_IDS = {
    "UNITY-HUB",
    "UNITY-EDITOR",
    "MAC-BUILD-SUPPORT-IL2CPP",
    "XCODE",
    "DOTNET-SDK",
    "ROSETTA-2",
    "UNITY-AI-ASSISTANT",
    "URP",
    "UNITY-TEST-FRAMEWORK",
}
# Intentionally empty until a creator-protected decision binds an exact profile
# digest. A caller-supplied profile cannot add itself to this allowlist.
TRUSTED_IL2CPP_PROFILE_SHA256: frozenset[str] = frozenset()


class MissingEvidence(Exception):
    """The contracted path does not exist."""


class UnsafeEvidence(Exception):
    """The source cannot safely be treated as static evidence."""


@dataclass(frozen=True)
class FileSnapshot:
    path: Path
    data: bytes | bytearray
    sha256: str
    size: int
    uid: int
    gid: int
    mode: int


@dataclass(frozen=True)
class CollectorPaths:
    repo_root: Path
    project_root: Path
    hub_app: Path
    editors_root: Path
    xcode_app: Path
    xcode_select_link: Path
    dotnet_root: Path
    rosetta_receipt: Path
    rosetta_markers: tuple[Path, ...]

    @classmethod
    def defaults(cls, repo_root: Path, project_root: Path) -> "CollectorPaths":
        return cls(
            repo_root=repo_root,
            project_root=project_root,
            hub_app=Path("/Applications/Unity Hub.app"),
            editors_root=Path("/Applications/Unity/Hub/Editor"),
            xcode_app=Path("/Applications/Xcode.app"),
            xcode_select_link=Path("/private/var/db/xcode_select_link"),
            dotnet_root=Path("/usr/local/share/dotnet"),
            rosetta_receipt=Path(
                "/Library/Apple/System/Library/Receipts/"
                "com.apple.pkg.RosettaUpdateAuto.plist"
            ),
            rosetta_markers=(
                Path("/Library/Apple/usr/libexec/oah/libRosettaRuntime"),
                Path("/usr/libexec/rosetta/runtime"),
                Path("/usr/libexec/rosetta/translate_tool"),
            ),
        )


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical_sha256(value: object) -> str:
    encoded = json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")
    return sha256_bytes(encoded)


def opaque_value(value: object) -> str:
    return f"redacted-sha256:{canonical_sha256(value)}"


def opaque_error(error: BaseException) -> str:
    return f"{type(error).__name__}:{opaque_value(str(error))}"


def version_display(value: object) -> str:
    if (
        isinstance(value, str)
        and len(value) <= 80
        and (
            re.fullmatch(
                r"(?:Unity version )?[0-9][0-9A-Za-z.+() _-]*",
                value,
            )
            or re.fullmatch(r"[0-9a-f]{12,64}", value)
        )
    ):
        return value
    if value is None:
        return "null"
    return opaque_value(value)


def package_label(value: object) -> str:
    if (
        isinstance(value, str)
        and re.fullmatch(r"com\.unity\.[a-z0-9.-]+", value)
    ):
        return value
    return opaque_value(value)


def require_secret_free_output(value: object, label: str = "observation") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            if SECRET_VALUE_PATTERN.search(str(key)):
                raise UnsafeEvidence(f"secret-shaped key rejected at {label}")
            require_secret_free_output(child, f"{label}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            require_secret_free_output(child, f"{label}[{index}]")
    elif isinstance(value, str) and SECRET_VALUE_PATTERN.search(value):
        raise UnsafeEvidence(f"secret-shaped value rejected at {label}")


def utc_now() -> str:
    return (
        datetime.now(timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z")
    )


def _require_absolute(path: Path) -> None:
    if not path.is_absolute():
        raise UnsafeEvidence(f"path is not absolute: {path}")


def path_components_symlink_free(path: Path, *, include_leaf: bool = True) -> bool:
    """Return whether every existing contracted component is not a symlink."""
    _require_absolute(path)
    cursor = Path(path.anchor)
    parts = path.parts[1:] if path.anchor else path.parts
    if not include_leaf and parts:
        parts = parts[:-1]
    for part in parts:
        cursor /= part
        try:
            metadata = os.lstat(cursor)
        except FileNotFoundError:
            return False
        except OSError:
            return False
        if stat.S_ISLNK(metadata.st_mode):
            return False
    return True


def _parent_components_safe(path: Path) -> None:
    _require_absolute(path)
    cursor = Path(path.anchor)
    parts = path.parts[1:-1] if path.anchor else path.parts[:-1]
    for part in parts:
        cursor /= part
        try:
            metadata = os.lstat(cursor)
        except FileNotFoundError as exc:
            raise MissingEvidence(str(path)) from exc
        except OSError as exc:
            raise UnsafeEvidence(f"cannot inspect path ancestry {cursor}: {exc}") from exc
        if stat.S_ISLNK(metadata.st_mode):
            raise UnsafeEvidence(f"symlinked path ancestry: {cursor}")


def read_regular_file(path: Path, *, max_bytes: int) -> FileSnapshot:
    """Read a stable regular file without following the leaf symlink."""
    _parent_components_safe(path)
    try:
        leaf = os.lstat(path)
    except FileNotFoundError as exc:
        raise MissingEvidence(str(path)) from exc
    except OSError as exc:
        raise UnsafeEvidence(f"cannot inspect {path}: {exc}") from exc
    if stat.S_ISLNK(leaf.st_mode) or not stat.S_ISREG(leaf.st_mode):
        raise UnsafeEvidence(f"not a regular non-symlink file: {path}")
    if leaf.st_size > max_bytes:
        raise UnsafeEvidence(f"file exceeds {max_bytes} bytes: {path}")

    flags = os.O_RDONLY
    if hasattr(os, "O_CLOEXEC"):
        flags |= os.O_CLOEXEC
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    try:
        descriptor = os.open(path, flags)
    except FileNotFoundError as exc:
        raise MissingEvidence(str(path)) from exc
    except OSError as exc:
        raise UnsafeEvidence(f"cannot open {path}: {exc}") from exc
    try:
        before = os.fstat(descriptor)
        identity_fields = ("st_dev", "st_ino", "st_mode", "st_uid", "st_gid")
        if any(
            getattr(leaf, field) != getattr(before, field)
            for field in identity_fields
        ):
            raise UnsafeEvidence(f"file identity changed before open: {path}")
        if not stat.S_ISREG(before.st_mode):
            raise UnsafeEvidence(f"opened source is not regular: {path}")
        if before.st_size > max_bytes:
            raise UnsafeEvidence(f"file exceeds {max_bytes} bytes: {path}")
        data = bytearray()
        digest = hashlib.sha256()
        remaining = max_bytes + 1
        while remaining > 0:
            chunk = os.read(descriptor, min(1024 * 1024, remaining))
            if not chunk:
                break
            data.extend(chunk)
            digest.update(chunk)
            remaining -= len(chunk)
        if len(data) > max_bytes:
            raise UnsafeEvidence(f"file grew beyond {max_bytes} bytes: {path}")
        after = os.fstat(descriptor)
    finally:
        os.close(descriptor)

    stable_fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_uid",
        "st_gid",
        "st_size",
        "st_mtime_ns",
        "st_ctime_ns",
    )
    if any(getattr(before, field) != getattr(after, field) for field in stable_fields):
        raise UnsafeEvidence(f"file changed during inspection: {path}")
    if len(data) != before.st_size:
        raise UnsafeEvidence(f"short or inconsistent read: {path}")
    return FileSnapshot(
        path=path,
        data=data,
        sha256=digest.hexdigest(),
        size=before.st_size,
        uid=before.st_uid,
        gid=before.st_gid,
        mode=stat.S_IMODE(before.st_mode),
    )


def require_safe_directory(path: Path) -> os.stat_result:
    _parent_components_safe(path)
    try:
        metadata = os.lstat(path)
    except FileNotFoundError as exc:
        raise MissingEvidence(str(path)) from exc
    except OSError as exc:
        raise UnsafeEvidence(f"cannot inspect directory {path}: {exc}") from exc
    if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(metadata.st_mode):
        raise UnsafeEvidence(f"not a directory without symlink indirection: {path}")
    return metadata


def safe_directory_names(path: Path) -> list[str]:
    initial = require_safe_directory(path)
    flags = os.O_RDONLY
    if hasattr(os, "O_CLOEXEC"):
        flags |= os.O_CLOEXEC
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    if hasattr(os, "O_DIRECTORY"):
        flags |= os.O_DIRECTORY
    try:
        descriptor = os.open(path, flags)
    except FileNotFoundError as exc:
        raise MissingEvidence(str(path)) from exc
    except OSError as exc:
        raise UnsafeEvidence(f"cannot open directory {path}: {exc}") from exc
    try:
        before = os.fstat(descriptor)
        identity_fields = ("st_dev", "st_ino", "st_mode", "st_uid", "st_gid")
        if any(
            getattr(initial, field) != getattr(before, field)
            for field in identity_fields
        ):
            raise UnsafeEvidence(f"directory identity changed before open: {path}")
        if not stat.S_ISDIR(before.st_mode):
            raise UnsafeEvidence(f"opened source is not a directory: {path}")
        with os.scandir(descriptor) as entries:
            names = []
            for index, entry in enumerate(entries):
                if index >= MAX_DIRECTORY_ENTRIES:
                    raise UnsafeEvidence(f"directory entry bound exceeded: {path}")
                names.append(entry.name)
        after = os.fstat(descriptor)
    except UnsafeEvidence:
        raise
    except OSError as exc:
        raise UnsafeEvidence(f"cannot list directory {path}: {exc}") from exc
    finally:
        os.close(descriptor)
    stable_fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_uid",
        "st_gid",
        "st_mtime_ns",
        "st_ctime_ns",
    )
    if any(getattr(before, field) != getattr(after, field) for field in stable_fields):
        raise UnsafeEvidence(f"directory changed during inspection: {path}")
    return sorted(names)


def safe_link_target(path: Path) -> tuple[str, str, int]:
    """Read an intentional symlink without following it."""
    _parent_components_safe(path)
    try:
        metadata = os.lstat(path)
    except FileNotFoundError as exc:
        raise MissingEvidence(str(path)) from exc
    except OSError as exc:
        raise UnsafeEvidence(f"cannot inspect link {path}: {exc}") from exc
    if not stat.S_ISLNK(metadata.st_mode):
        raise UnsafeEvidence(f"expected a symlink: {path}")
    try:
        target = os.readlink(path)
    except OSError as exc:
        raise UnsafeEvidence(f"cannot read link {path}: {exc}") from exc
    try:
        after = os.lstat(path)
    except OSError as exc:
        raise UnsafeEvidence(f"link changed during inspection: {path}: {exc}") from exc
    stable_fields = (
        "st_dev",
        "st_ino",
        "st_mode",
        "st_uid",
        "st_gid",
        "st_size",
        "st_mtime_ns",
        "st_ctime_ns",
    )
    if any(getattr(metadata, field) != getattr(after, field) for field in stable_fields):
        raise UnsafeEvidence(f"link changed during inspection: {path}")
    return target, sha256_bytes(target.encode("utf-8")), metadata.st_uid


def parse_json_bytes(data: bytes | bytearray, label: str) -> Any:
    def reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"duplicate JSON key {key!r} in {label}")
            result[key] = value
        return result

    try:
        return json.loads(
            data.decode("utf-8"),
            object_pairs_hook=reject_duplicates,
            parse_constant=lambda value: (_ for _ in ()).throw(
                ValueError(f"non-finite JSON constant {value!r} in {label}")
            ),
        )
    except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as exc:
        raise UnsafeEvidence(f"invalid JSON in {label}: {exc}") from exc


def parse_plist_bytes(data: bytes | bytearray, label: str) -> dict[str, Any]:
    try:
        value = plistlib.loads(data)
    except Exception as exc:
        raise UnsafeEvidence(f"invalid plist in {label}: {exc}") from exc
    if not isinstance(value, dict):
        raise UnsafeEvidence(f"plist root is not a dictionary: {label}")
    return value


def macho_architectures(
    data: bytes | bytearray,
    *,
    _allow_fat: bool = True,
) -> list[str]:
    """Validate Mach-O headers/load-command bounds and return slice architectures."""
    cpu_names = {
        7: "x86",
        0x01000007: "x86_64",
        12: "arm",
        0x0100000C: "arm64",
    }
    if len(data) < 4:
        raise UnsafeEvidence("Mach-O header is truncated")
    magic = bytes(data[:4])
    thin = {
        b"\xce\xfa\xed\xfe": ("<", 28),
        b"\xfe\xed\xfa\xce": (">", 28),
        b"\xcf\xfa\xed\xfe": ("<", 32),
        b"\xfe\xed\xfa\xcf": (">", 32),
    }
    if magic in thin:
        endian, header_size = thin[magic]
        if len(data) < header_size:
            raise UnsafeEvidence("Mach-O thin header is truncated")
        cpu_type = struct.unpack(f"{endian}I", data[4:8])[0]
        file_type = struct.unpack(f"{endian}I", data[12:16])[0]
        command_count = struct.unpack(f"{endian}I", data[16:20])[0]
        command_bytes = struct.unpack(f"{endian}I", data[20:24])[0]
        if file_type not in {2, 6, 8}:
            raise UnsafeEvidence(f"unsupported Mach-O file type {file_type}")
        if (
            command_count < 1
            or command_count > 65535
            or header_size + command_bytes > len(data)
        ):
            raise UnsafeEvidence("Mach-O load-command table is out of bounds")
        cursor = header_size
        command_end = header_size + command_bytes
        segment_command = 0x19 if header_size == 32 else 0x1
        minimum_segment_size = 72 if header_size == 32 else 56
        saw_segment = False
        for _ in range(command_count):
            if cursor + 8 > command_end:
                raise UnsafeEvidence("Mach-O load command header is truncated")
            command = struct.unpack(
                f"{endian}I",
                data[cursor : cursor + 4],
            )[0]
            command_size = struct.unpack(
                f"{endian}I",
                data[cursor + 4 : cursor + 8],
            )[0]
            if (
                command_size < 8
                or command_size % 4
                or cursor + command_size > command_end
            ):
                raise UnsafeEvidence("Mach-O load command size is invalid")
            if command == segment_command:
                if command_size < minimum_segment_size:
                    raise UnsafeEvidence("Mach-O segment command is truncated")
                saw_segment = True
            cursor += command_size
        if cursor != command_end:
            raise UnsafeEvidence("Mach-O load-command byte count is inconsistent")
        if not saw_segment:
            raise UnsafeEvidence("Mach-O image has no segment load command")
        return [cpu_names.get(cpu_type, f"cpu-{cpu_type}")]

    fat = {
        b"\xca\xfe\xba\xbe": (">", 20, False),
        b"\xbe\xba\xfe\xca": ("<", 20, False),
        b"\xca\xfe\xba\xbf": (">", 32, True),
        b"\xbf\xba\xfe\xca": ("<", 32, True),
    }
    if magic not in fat or not _allow_fat:
        raise UnsafeEvidence("file is not a recognized Mach-O image")
    endian, entry_size, is_64 = fat[magic]
    if len(data) < 8:
        raise UnsafeEvidence("Mach-O fat header is truncated")
    count = struct.unpack(f"{endian}I", data[4:8])[0]
    table_end = 8 + count * entry_size
    if count < 1 or count > 32 or table_end > len(data):
        raise UnsafeEvidence("invalid Mach-O fat header")
    architectures = []
    ranges: list[tuple[int, int]] = []
    for index in range(count):
        entry = 8 + index * entry_size
        cpu_type = struct.unpack(f"{endian}I", data[entry : entry + 4])[0]
        if is_64:
            slice_offset = struct.unpack(
                f"{endian}Q",
                data[entry + 8 : entry + 16],
            )[0]
            slice_size = struct.unpack(
                f"{endian}Q",
                data[entry + 16 : entry + 24],
            )[0]
        else:
            slice_offset = struct.unpack(
                f"{endian}I",
                data[entry + 8 : entry + 12],
            )[0]
            slice_size = struct.unpack(
                f"{endian}I",
                data[entry + 12 : entry + 16],
            )[0]
        slice_end = slice_offset + slice_size
        if (
            slice_offset < table_end
            or slice_size < 28
            or slice_end > len(data)
        ):
            raise UnsafeEvidence("Mach-O fat slice is out of bounds")
        if any(slice_offset < end and start < slice_end for start, end in ranges):
            raise UnsafeEvidence("Mach-O fat slices overlap")
        ranges.append((slice_offset, slice_end))
        nested = macho_architectures(
            data[slice_offset:slice_end],
            _allow_fat=False,
        )
        declared = cpu_names.get(cpu_type, f"cpu-{cpu_type}")
        if nested != [declared]:
            raise UnsafeEvidence("Mach-O fat slice architecture differs from table")
        architectures.append(declared)
    return sorted(set(architectures))


def evidence_item(
    method: str,
    location: Path,
    observation: str,
    *,
    sha256: str | None,
) -> dict[str, Any]:
    observation = {
        "method": method,
        "location": str(location),
        "sha256": sha256,
        "observation": observation,
    }
    require_secret_free_output(observation)
    return observation


def requirement(
    requirement_id: str,
    required: str,
    observed: str | None,
    status: str,
    evidence: Iterable[dict[str, Any]] = (),
    limitations: Iterable[str] = (),
) -> dict[str, Any]:
    if status not in REQUIREMENT_STATUSES:
        raise ValueError(f"unknown requirement status {status!r}")
    return {
        "id": requirement_id,
        "required": required,
        "observed": observed,
        "status": status,
        "evidence": list(evidence),
        "limitations": list(limitations),
    }


def inspect_bundle(
    app: Path,
    *,
    required_identifier: str,
    required_version: str,
    executable_fallback: str,
    version_key: str = "CFBundleShortVersionString",
    require_arm64: bool = False,
) -> tuple[dict[str, Any], list[dict[str, Any]], list[str]]:
    plist_path = app / "Contents" / "Info.plist"
    plist_snapshot = read_regular_file(plist_path, max_bytes=MAX_PLIST_BYTES)
    plist = parse_plist_bytes(plist_snapshot.data, str(plist_path))
    executable_name = plist.get("CFBundleExecutable", executable_fallback)
    executable_relative = (
        PurePosixPath(executable_name)
        if isinstance(executable_name, str)
        else None
    )
    if (
        executable_relative is None
        or not executable_name
        or executable_relative.is_absolute()
        or len(executable_relative.parts) != 1
        or executable_relative.parts[0] in {".", ".."}
        or executable_name != executable_fallback
    ):
        raise UnsafeEvidence(f"invalid CFBundleExecutable in {plist_path}")
    executable_path = app / "Contents" / "MacOS" / executable_name
    executable_snapshot = read_regular_file(
        executable_path,
        max_bytes=MAX_EXECUTABLE_BYTES,
    )
    if executable_snapshot.mode & 0o022:
        raise UnsafeEvidence(f"executable is group/world-writable: {executable_path}")
    architectures = macho_architectures(executable_snapshot.data)
    facts = {
        "identifier": plist.get("CFBundleIdentifier"),
        "version": plist.get(version_key),
        "bundle_version": plist.get("CFBundleVersion"),
        "build_number": plist.get("UnityBuildNumber"),
        "executable": executable_name,
        "architectures": architectures,
        "executable_mode": executable_snapshot.mode,
    }
    evidence = [
        evidence_item(
            "plist-read",
            plist_path,
            (
                f"identifier_matches={facts['identifier'] == required_identifier}; "
                f"version={version_display(facts['version'])}; "
                f"bundle_version={version_display(facts['bundle_version'])}; "
                f"build_number={version_display(facts['build_number'])}"
            ),
            sha256=plist_snapshot.sha256,
        ),
        evidence_item(
            "bounded-file-hash-and-mach-o-header",
            executable_path,
            (
                f"architectures={architectures!r}; "
                f"mode={executable_snapshot.mode:04o}"
            ),
            sha256=executable_snapshot.sha256,
        ),
    ]
    mismatches = []
    if facts["identifier"] != required_identifier:
        mismatches.append("bundle identifier differs")
    observed_version = facts["version"]
    if (
        isinstance(observed_version, str)
        and observed_version.startswith("Unity version ")
    ):
        observed_version = observed_version.removeprefix("Unity version ")
    if (
        observed_version != required_version
        or facts["bundle_version"] != required_version
    ):
        mismatches.append("bundle version differs")
    if require_arm64 and "arm64" not in architectures:
        mismatches.append("ARM64 executable slice is absent")
    if executable_snapshot.mode & 0o111 == 0:
        mismatches.append("executable mode has no execute bit")
    return facts, evidence, mismatches


def inspect_hub(paths: CollectorPaths) -> dict[str, Any]:
    required = f"Unity Hub {REQUIRED_HUB_VERSION}"
    try:
        facts, evidence, mismatches = inspect_bundle(
            paths.hub_app,
            required_identifier="com.unity3d.unityhub",
            required_version=REQUIRED_HUB_VERSION,
            executable_fallback="Unity Hub",
            require_arm64=False,
        )
    except MissingEvidence:
        return requirement(
            "UNITY-HUB",
            required,
            None,
            "missing",
            limitations=["Only the contracted Unity Hub application path was inspected."],
        )
    except UnsafeEvidence as exc:
        return requirement("UNITY-HUB", required, opaque_error(exc), "unsafe")
    observed = (
        f"version {version_display(facts['version'] or facts['bundle_version'])}; "
        f"architectures {', '.join(facts['architectures'])}"
    )
    return requirement(
        "UNITY-HUB",
        required,
        observed,
        "mismatch" if mismatches else "matched",
        evidence,
        mismatches,
    )


def _installed_editor_names(paths: CollectorPaths) -> tuple[list[str], str | None]:
    try:
        names = safe_directory_names(paths.editors_root)
    except MissingEvidence:
        return [], None
    except UnsafeEvidence as exc:
        return [], opaque_error(exc)
    versions = []
    for name in names:
        if UNITY_EDITOR_DIRECTORY_PATTERN.fullmatch(name) is None:
            continue
        candidate = paths.editors_root / name
        try:
            require_safe_directory(candidate)
        except (MissingEvidence, UnsafeEvidence):
            continue
        versions.append(name)
    return sorted(versions), None


def inspect_editor(paths: CollectorPaths) -> dict[str, Any]:
    required = (
        f"Unity Editor {REQUIRED_EDITOR_VERSION} changeset "
        f"{REQUIRED_EDITOR_CHANGESET} ARM64"
    )
    versions, listing_error = _installed_editor_names(paths)
    if listing_error:
        return requirement("UNITY-EDITOR", required, listing_error, "unsafe")
    editor_root = paths.editors_root / REQUIRED_EDITOR_VERSION
    app = editor_root / "Unity.app"
    try:
        facts, evidence, mismatches = inspect_bundle(
            app,
            required_identifier="com.unity3d.UnityEditor5.x",
            required_version=REQUIRED_EDITOR_VERSION,
            executable_fallback="Unity",
            require_arm64=True,
        )
    except MissingEvidence:
        if versions:
            return requirement(
                "UNITY-EDITOR",
                required,
                f"Hub Editor directories: {', '.join(versions)}; exact target absent",
                "mismatch",
                limitations=[
                    "Only the contracted Unity Hub Editor installation root was inspected."
                ],
            )
        return requirement(
            "UNITY-EDITOR",
            required,
            None,
            "missing",
            limitations=[
                "Only the contracted Unity Hub Editor installation root was inspected."
            ],
        )
    except UnsafeEvidence as exc:
        return requirement("UNITY-EDITOR", required, opaque_error(exc), "unsafe")
    if facts["build_number"] != REQUIRED_EDITOR_CHANGESET:
        mismatches.append("UnityBuildNumber changeset differs")
    observed_version = facts["bundle_version"] or facts["version"]
    observed = (
        f"{version_display(observed_version)} changeset "
        f"{version_display(facts['build_number'])}; "
        f"architectures {', '.join(facts['architectures'])}"
    )
    return requirement(
        "UNITY-EDITOR",
        required,
        observed,
        "mismatch" if mismatches else "matched",
        evidence,
        mismatches,
    )


def _validate_marker_profile(profile: object) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    if not isinstance(profile, dict):
        raise UnsafeEvidence("IL2CPP marker profile is not an object")
    allowed = {"schema_version", "editor_version", "editor_changeset", "markers"}
    if set(profile) != allowed:
        raise UnsafeEvidence("IL2CPP marker profile fields differ from v1")
    if (
        profile.get("schema_version") != 1
        or profile.get("editor_version") != REQUIRED_EDITOR_VERSION
        or profile.get("editor_changeset") != REQUIRED_EDITOR_CHANGESET
    ):
        raise UnsafeEvidence("IL2CPP marker profile binds the wrong Editor")
    markers = profile.get("markers")
    if not isinstance(markers, list) or not markers or len(markers) > 256:
        raise UnsafeEvidence("IL2CPP marker profile marker count is invalid")
    normalized = []
    seen = set()
    for marker in markers:
        if not isinstance(marker, dict) or set(marker) - {
            "path",
            "sha256",
            "architectures",
        }:
            raise UnsafeEvidence("invalid IL2CPP marker record")
        raw_path = marker.get("path")
        digest = marker.get("sha256")
        architectures = marker.get("architectures", [])
        if (
            not isinstance(raw_path, str)
            or not raw_path
            or PurePosixPath(raw_path).is_absolute()
            or ".." in PurePosixPath(raw_path).parts
            or not isinstance(digest, str)
            or SHA256_PATTERN.fullmatch(digest) is None
            or not isinstance(architectures, list)
            or any(item not in {"arm64", "x86_64"} for item in architectures)
        ):
            raise UnsafeEvidence("invalid IL2CPP marker path, hash, or architecture")
        if raw_path in seen:
            raise UnsafeEvidence("duplicate IL2CPP marker path")
        seen.add(raw_path)
        normalized.append(
            {
                "path": raw_path,
                "sha256": digest,
                "architectures": architectures,
            }
        )
    return profile, normalized


def inspect_il2cpp(
    paths: CollectorPaths,
    editor: dict[str, Any],
    *,
    marker_profile: object | None,
    marker_profile_evidence: dict[str, Any] | None,
) -> dict[str, Any]:
    required = (
        f"Mac Build Support (IL2CPP) for Unity {REQUIRED_EDITOR_VERSION}, "
        "bound to a protected physical-marker profile"
    )
    if editor["status"] == "unsafe":
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            "exact Editor evidence is unsafe",
            "unsafe",
        )
    editor_root = paths.editors_root / REQUIRED_EDITOR_VERSION
    modules_path = editor_root / "modules.json"
    module_root = editor_root / "PlaybackEngines" / "MacStandaloneSupport"
    try:
        modules_snapshot = read_regular_file(modules_path, max_bytes=MAX_JSON_BYTES)
        modules = parse_json_bytes(modules_snapshot.data, str(modules_path))
    except MissingEvidence:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            "exact Editor module catalog or Editor is absent",
            "missing",
        )
    except UnsafeEvidence as exc:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            opaque_error(exc),
            "unsafe",
        )
    if not isinstance(modules, list):
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            "modules.json root is not an array",
            "unsafe",
        )
    records = [
        item
        for item in modules
        if isinstance(item, dict) and item.get("id") == "mac-il2cpp"
    ]
    if len(records) != 1:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            f"mac-il2cpp catalog records={len(records)}",
            "mismatch",
            [
                evidence_item(
                    "duplicate-key-safe-json-read",
                    modules_path,
                    f"mac-il2cpp records={len(records)}",
                    sha256=modules_snapshot.sha256,
                )
            ],
        )
    record = records[0]
    record_text = json.dumps(record, sort_keys=True)
    catalog_matches = (
        record.get("name") == "Mac Build Support (IL2CPP)"
        and record.get("destination")
        == "{UNITY_PATH}/PlaybackEngines/MacStandaloneSupport"
        and REQUIRED_EDITOR_VERSION in record_text
        and REQUIRED_EDITOR_CHANGESET in record_text
    )
    catalog_evidence = evidence_item(
        "duplicate-key-safe-json-read",
        modules_path,
        (
            f"id_matches={record.get('id') == 'mac-il2cpp'}; "
            f"name_matches={record.get('name') == 'Mac Build Support (IL2CPP)'}; "
            "destination_matches="
            f"{record.get('destination') == '{UNITY_PATH}/PlaybackEngines/MacStandaloneSupport'}; "
            f"editor_tuple_bound={REQUIRED_EDITOR_VERSION in record_text and REQUIRED_EDITOR_CHANGESET in record_text}"
        ),
        sha256=modules_snapshot.sha256,
    )
    if not catalog_matches:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            "mac-il2cpp catalog record differs from the exact Editor tuple",
            "mismatch",
            [catalog_evidence],
        )
    try:
        require_safe_directory(module_root)
    except MissingEvidence:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            "catalog advertises the module, but the physical module tree is absent",
            "missing",
            [catalog_evidence],
            ["modules.json lists available modules and is not installation proof."],
        )
    except UnsafeEvidence as exc:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            opaque_error(exc),
            "unsafe",
            [catalog_evidence],
        )

    variations_root = module_root / "Variations"
    try:
        variation_names = safe_directory_names(variations_root)
    except MissingEvidence:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            "MacStandaloneSupport exists, but its Variations directory is absent",
            "missing",
            [catalog_evidence],
        )
    except UnsafeEvidence as exc:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            opaque_error(exc),
            "unsafe",
            [catalog_evidence],
        )
    il2cpp_variations = [
        name
        for name in variation_names
        if re.fullmatch(
            r"macos_arm64_player_(?:development|nondevelopment)_il2cpp",
            name,
        )
    ]
    variation_evidence = evidence_item(
        "bounded-directory-listing",
        variations_root,
        (
            f"variation_count={len(variation_names)}; "
            f"arm64_il2cpp_variations={il2cpp_variations!r}; "
            f"listing_sha256={canonical_sha256(variation_names)}"
        ),
        sha256=canonical_sha256(variation_names),
    )
    if not il2cpp_variations:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            "Mac player variations are present, but ARM64 IL2CPP variations are absent",
            "missing",
            [catalog_evidence, variation_evidence],
            ["The baseline Mac support tree may contain Mono variations only."],
        )

    if marker_profile is None:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            "physical module tree exists, but no protected marker profile was supplied",
            "unverified",
            [catalog_evidence, variation_evidence],
            [
                "A complete physical marker inventory must be frozen from trusted "
                "installer evidence before this component can match."
            ],
        )
    try:
        _, markers = _validate_marker_profile(marker_profile)
    except UnsafeEvidence as exc:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            opaque_error(exc),
            "unsafe",
            [catalog_evidence],
        )
    evidence = [catalog_evidence, variation_evidence]
    if marker_profile_evidence is not None:
        evidence.append(marker_profile_evidence)
    profile_digest = (
        marker_profile_evidence.get("sha256")
        if isinstance(marker_profile_evidence, dict)
        else None
    )
    if profile_digest not in TRUSTED_IL2CPP_PROFILE_SHA256:
        return requirement(
            "MAC-BUILD-SUPPORT-IL2CPP",
            required,
            (
                "physical IL2CPP variations exist, but the supplied marker "
                "profile digest is not protected"
            ),
            "unverified",
            evidence,
            [
                "A caller-supplied profile cannot authorize itself; its SHA-256 "
                "must be added by a creator-protected control-plane revision."
            ],
        )
    mismatches = []
    for marker in markers:
        marker_path = module_root / marker["path"]
        marker_label = sha256_bytes(marker["path"].encode("utf-8"))[:16]
        try:
            snapshot = read_regular_file(marker_path, max_bytes=MAX_MARKER_BYTES)
        except MissingEvidence:
            mismatches.append(f"missing marker id={marker_label}")
            continue
        except UnsafeEvidence as exc:
            return requirement(
                "MAC-BUILD-SUPPORT-IL2CPP",
                required,
                opaque_error(exc),
                "unsafe",
                evidence,
            )
        if snapshot.sha256 != marker["sha256"]:
            mismatches.append(f"hash mismatch for marker id={marker_label}")
        architecture_observation = ""
        if marker["architectures"]:
            try:
                architectures = macho_architectures(snapshot.data)
            except UnsafeEvidence:
                mismatches.append(f"invalid Mach-O marker id={marker_label}")
                architectures = []
            if sorted(architectures) != sorted(marker["architectures"]):
                mismatches.append(
                    f"architecture mismatch for marker id={marker_label}"
                )
            architecture_observation = f"; architectures={architectures!r}"
        evidence.append(
            evidence_item(
                "protected-marker-hash",
                Path(f"MacStandaloneSupport/marker-{marker_label}"),
                f"profile marker id={marker_label}{architecture_observation}",
                sha256=snapshot.sha256,
            )
        )
    return requirement(
        "MAC-BUILD-SUPPORT-IL2CPP",
        required,
        f"validated {len(markers)} protected physical markers",
        "mismatch" if mismatches else "matched",
        evidence,
        mismatches
        or [
            "Static markers do not prove that an IL2CPP ARM64 player builds or runs."
        ],
    )


def inspect_xcode(paths: CollectorPaths) -> dict[str, Any]:
    required = f"full Xcode {REQUIRED_XCODE_VERSION}"
    info_path = paths.xcode_app / "Contents" / "Info.plist"
    executable_path = (
        paths.xcode_app
        / "Contents"
        / "Developer"
        / "usr"
        / "bin"
        / "xcodebuild"
    )
    try:
        info_snapshot = read_regular_file(info_path, max_bytes=MAX_PLIST_BYTES)
        info = parse_plist_bytes(info_snapshot.data, str(info_path))
        executable_snapshot = read_regular_file(
            executable_path,
            max_bytes=MAX_EXECUTABLE_BYTES,
        )
        if executable_snapshot.mode & 0o022:
            raise UnsafeEvidence(
                f"executable is group/world-writable: {executable_path}"
            )
        architectures = macho_architectures(executable_snapshot.data)
    except MissingEvidence:
        selected = None
        try:
            selected = safe_link_target(paths.xcode_select_link)[0]
        except (MissingEvidence, UnsafeEvidence):
            pass
        selected_display = (
            selected
            if selected in {
                "/Library/Developer/CommandLineTools",
                str(paths.xcode_app / "Contents" / "Developer"),
            }
            else (opaque_value(selected) if selected is not None else None)
        )
        return requirement(
            "XCODE",
            required,
            (
                f"full Xcode absent at contracted path; selected root={selected_display!r}"
                if selected is not None
                else "full Xcode absent at contracted path"
            ),
            "missing",
            limitations=[
                "The collector does not search arbitrary renamed or external-volume "
                "Xcode installations."
            ],
        )
    except UnsafeEvidence as exc:
        return requirement("XCODE", required, opaque_error(exc), "unsafe")
    evidence = [
        evidence_item(
            "plist-read",
            info_path,
            (
                "identifier_matches="
                f"{info.get('CFBundleIdentifier') == 'com.apple.dt.Xcode'}; "
                f"version={version_display(info.get('CFBundleShortVersionString'))}; "
                f"build={version_display(info.get('CFBundleVersion'))}"
            ),
            sha256=info_snapshot.sha256,
        ),
        evidence_item(
            "bounded-file-hash-and-mach-o-header",
            executable_path,
            (
                f"architectures={architectures!r}; "
                f"mode={executable_snapshot.mode:04o}"
            ),
            sha256=executable_snapshot.sha256,
        ),
    ]
    mismatches = []
    limitations = [
        "Static presence does not prove license acceptance or successful builds."
    ]
    if info.get("CFBundleIdentifier") != "com.apple.dt.Xcode":
        mismatches.append("bundle identifier differs")
    if info.get("CFBundleShortVersionString") != REQUIRED_XCODE_VERSION:
        mismatches.append("Xcode version differs")
    if "arm64" not in architectures:
        limitations.append(
            "xcodebuild has no ARM64 slice; D-0047 fixes the Xcode version, "
            "not its executable architecture."
        )
    if executable_snapshot.mode & 0o111 == 0:
        mismatches.append("xcodebuild mode has no execute bit")
    try:
        target, target_hash, uid = safe_link_target(paths.xcode_select_link)
        absolute_target = Path(target)
        if not absolute_target.is_absolute():
            absolute_target = paths.xcode_select_link.parent / absolute_target
        normalized_target = Path(os.path.normpath(str(absolute_target)))
        expected_target = paths.xcode_app / "Contents" / "Developer"
        target_display = (
            str(normalized_target)
            if normalized_target
            in {expected_target, Path("/Library/Developer/CommandLineTools")}
            else opaque_value(str(normalized_target))
        )
        evidence.append(
            evidence_item(
                "root-owned-symlink-read",
                paths.xcode_select_link,
                f"target={target_display}; uid={uid}",
                sha256=target_hash,
            )
        )
        if normalized_target != expected_target or uid != 0:
            limitations.append(
                "selected developer root differs or link is not root-owned"
            )
    except MissingEvidence:
        limitations.append("selected developer root link is absent")
    except UnsafeEvidence as exc:
        limitations.append(f"selected developer root could not be trusted: {exc}")
    observed = (
        f"version {version_display(info.get('CFBundleShortVersionString'))}; "
        f"build {version_display(info.get('CFBundleVersion'))}; "
        f"architectures {architectures}"
    )
    return requirement(
        "XCODE",
        required,
        observed,
        "mismatch" if mismatches else "matched",
        evidence,
        mismatches + limitations,
    )


def inspect_dotnet(paths: CollectorPaths) -> dict[str, Any]:
    required = f"standalone .NET SDK {REQUIRED_DOTNET_SDK}"
    try:
        names = safe_directory_names(paths.dotnet_root)
    except MissingEvidence:
        return requirement(
            "DOTNET-SDK",
            required,
            None,
            "unverified",
            limitations=[
                "Only /usr/local/share/dotnet is contracted for passive inspection; "
                "absence there is not an exhaustive machine-wide search."
            ],
        )
    except UnsafeEvidence as exc:
        return requirement("DOTNET-SDK", required, opaque_error(exc), "unsafe")
    executable_path = paths.dotnet_root / "dotnet"
    sdk_root = paths.dotnet_root / "sdk"
    sdk_path = sdk_root / REQUIRED_DOTNET_SDK
    required_files = (sdk_path / "dotnet.dll", sdk_path / "MSBuild.dll")
    try:
        executable = read_regular_file(
            executable_path,
            max_bytes=MAX_EXECUTABLE_BYTES,
        )
        if executable.mode & 0o022:
            raise UnsafeEvidence(
                f"executable is group/world-writable: {executable_path}"
            )
        architectures = macho_architectures(executable.data)
    except MissingEvidence:
        return requirement(
            "DOTNET-SDK",
            required,
            (
                f"dotnet root exists with {len(names)} entries "
                f"(listing_sha256={canonical_sha256(names)}), "
                "but host executable is absent"
            ),
            "unverified",
            limitations=[
                "The single contracted root is not an exhaustive machine-wide search."
            ],
        )
    except UnsafeEvidence as exc:
        return requirement("DOTNET-SDK", required, opaque_error(exc), "unsafe")
    evidence = [
        evidence_item(
            "bounded-file-hash-and-mach-o-header",
            executable_path,
            f"architectures={architectures!r}; mode={executable.mode:04o}",
            sha256=executable.sha256,
        )
    ]
    try:
        sdk_names = safe_directory_names(sdk_root)
    except MissingEvidence:
        return requirement(
            "DOTNET-SDK",
            required,
            "standalone host exists, but SDK root is absent",
            "unverified",
            evidence,
            ["The single contracted root is not an exhaustive machine-wide search."],
        )
    except UnsafeEvidence as exc:
        return requirement(
            "DOTNET-SDK",
            required,
            opaque_error(exc),
            "unsafe",
            evidence,
        )
    mismatches = []
    supplemental_limitations = []
    if "arm64" not in architectures:
        supplemental_limitations.append(
            "dotnet host has no ARM64 slice; D-0047 fixes the SDK version, "
            "not its executable architecture."
        )
    if executable.mode & 0o111 == 0:
        mismatches.append("dotnet host mode has no execute bit")
    if REQUIRED_DOTNET_SDK not in sdk_names:
        version_names = [
            name
            for name in sdk_names
            if REGISTRY_VERSION_PATTERN.fullmatch(name) is not None
        ]
        return requirement(
            "DOTNET-SDK",
            required,
            (
                f"candidate SDK versions={version_names!r}; "
                f"listing_sha256={canonical_sha256(sdk_names)}"
            ),
            "unverified",
            evidence,
            ["The single contracted root is not an exhaustive machine-wide search."],
        )
    missing_markers = []
    for required_file in required_files:
        try:
            snapshot = read_regular_file(required_file, max_bytes=MAX_MARKER_BYTES)
        except MissingEvidence:
            missing_markers.append(f"missing {required_file.name}")
            continue
        except UnsafeEvidence as exc:
            return requirement(
                "DOTNET-SDK",
                required,
                opaque_error(exc),
                "unsafe",
                evidence,
            )
        evidence.append(
            evidence_item(
                "bounded-file-hash",
                required_file,
                f"required SDK marker {required_file.name}",
                sha256=snapshot.sha256,
            )
        )
    return requirement(
        "DOTNET-SDK",
        required,
        f"SDK {REQUIRED_DOTNET_SDK}; architectures {architectures}",
        "mismatch"
        if mismatches
        else ("unverified" if missing_markers else "matched"),
        evidence,
        mismatches + missing_markers + supplemental_limitations,
    )


def inspect_rosetta(paths: CollectorPaths) -> dict[str, Any]:
    required = "Rosetta 2 installed where required by Unity Apple-Silicon tooling"
    try:
        receipt_snapshot = read_regular_file(
            paths.rosetta_receipt,
            max_bytes=MAX_PLIST_BYTES,
        )
        receipt = parse_plist_bytes(receipt_snapshot.data, str(paths.rosetta_receipt))
    except MissingEvidence:
        return requirement("ROSETTA-2", required, None, "missing")
    except UnsafeEvidence as exc:
        return requirement("ROSETTA-2", required, opaque_error(exc), "unsafe")
    evidence = [
        evidence_item(
            "package-receipt-plist-read",
            paths.rosetta_receipt,
            (
                "identifier_matches="
                f"{receipt.get('PackageIdentifier') == 'com.apple.pkg.RosettaUpdateAuto'}; "
                f"version={version_display(receipt.get('PackageVersion'))}"
            ),
            sha256=receipt_snapshot.sha256,
        )
    ]
    mismatches = []
    limitations = []
    if receipt.get("PackageIdentifier") != "com.apple.pkg.RosettaUpdateAuto":
        mismatches.append("package receipt identifier differs")
    for marker in paths.rosetta_markers:
        try:
            snapshot = read_regular_file(marker, max_bytes=MAX_MARKER_BYTES)
        except MissingEvidence:
            limitations.append(f"supplemental physical marker is absent: {marker}")
            continue
        except UnsafeEvidence as exc:
            limitations.append(
                f"supplemental physical marker could not be trusted: {exc}"
            )
            continue
        if snapshot.uid != 0:
            limitations.append(f"supplemental marker is not root-owned: {marker}")
        evidence.append(
            evidence_item(
                "supplemental-physical-marker-hash",
                marker,
                f"uid={snapshot.uid}; mode={snapshot.mode:04o}; size={snapshot.size}",
                sha256=snapshot.sha256,
            )
        )
    return requirement(
        "ROSETTA-2",
        required,
        (
            "com.apple.pkg.RosettaUpdateAuto "
            f"{version_display(receipt.get('PackageVersion'))}"
        ),
        "mismatch" if mismatches else "matched",
        evidence,
        mismatches + limitations,
    )


def _package_requirement_status(
    package_id: str,
    package_name: str,
    required: str,
    manifest_dependencies: dict[str, Any],
    lock_dependencies: dict[str, Any],
    *,
    version_matches: Any,
    graph_safe: bool,
    evidence: list[dict[str, Any]],
    graph_limitations: list[str],
) -> dict[str, Any]:
    manifest_version = manifest_dependencies.get(package_name)
    lock_record = lock_dependencies.get(package_name)
    lock_version = lock_record.get("version") if isinstance(lock_record, dict) else None
    if manifest_version is None or lock_record is None:
        status = "missing"
    elif not graph_safe:
        status = "unsafe"
    elif (
        lock_record.get("source") != "registry"
        or lock_record.get("url") != "https://packages.unity.com"
    ):
        status = "unsafe"
    elif not version_matches(manifest_version) or not version_matches(lock_version):
        status = "mismatch"
    elif manifest_version != lock_version or lock_record.get("depth") != 0:
        status = "mismatch"
    else:
        status = "matched"
    return requirement(
        package_id,
        required,
        (
            f"manifest={version_display(manifest_version)!r}; "
            f"lock={version_display(lock_version)!r}; "
            f"depth={lock_record.get('depth') if isinstance(lock_record, dict) else None!r}"
        ),
        status,
        evidence,
        graph_limitations if status != "matched" else (),
    )


def inspect_package_graph(paths: CollectorPaths) -> dict[str, Any]:
    manifest_path = paths.project_root / "Packages" / "manifest.json"
    lock_path = paths.project_root / "Packages" / "packages-lock.json"
    empty_requirements = [
        requirement(
            "UNITY-AI-ASSISTANT",
            f"com.unity.ai.assistant {REQUIRED_ASSISTANT} from Unity Registry",
            None,
            "missing",
        ),
        requirement(
            "URP",
            "com.unity.render-pipelines.universal 17.3 line from Unity Registry",
            None,
            "missing",
        ),
        requirement(
            "UNITY-TEST-FRAMEWORK",
            "com.unity.test-framework 1.6 line from Unity Registry",
            None,
            "missing",
        ),
    ]
    try:
        manifest_snapshot = read_regular_file(manifest_path, max_bytes=MAX_JSON_BYTES)
        lock_snapshot = read_regular_file(lock_path, max_bytes=MAX_JSON_BYTES)
        manifest = parse_json_bytes(manifest_snapshot.data, str(manifest_path))
        lock = parse_json_bytes(lock_snapshot.data, str(lock_path))
    except MissingEvidence:
        return {
            "project_root": "WP0001_GAME_PROJECT_ROOT",
            "manifest": None,
            "lock": None,
            "graph_status": "missing",
            "canonical_graph_sha256": None,
            "requirements": empty_requirements,
            "limitations": [
                "Only the explicit WP-0001 project Packages files may satisfy this "
                "check; PackageCache and observation projects are excluded."
            ],
        }
    except UnsafeEvidence as exc:
        unsafe_requirements = [
            {**item, "status": "unsafe", "observed": opaque_error(exc)}
            for item in empty_requirements
        ]
        return {
            "project_root": "WP0001_GAME_PROJECT_ROOT",
            "manifest": None,
            "lock": None,
            "graph_status": "unsafe",
            "canonical_graph_sha256": None,
            "requirements": unsafe_requirements,
            "limitations": [opaque_error(exc)],
        }
    if not isinstance(manifest, dict) or not isinstance(lock, dict):
        graph_safe = False
        graph_limitations = ["manifest and lock roots must be JSON objects"]
        manifest_dependencies = {}
        lock_dependencies = {}
    else:
        manifest_dependencies = (
            manifest.get("dependencies")
            if isinstance(manifest.get("dependencies"), dict)
            else {}
        )
        lock_dependencies = (
            lock.get("dependencies")
            if isinstance(lock.get("dependencies"), dict)
            else {}
        )
        graph_limitations = []
        allowed_manifest_keys = {
            "dependencies",
            "enableLockFile",
            "resolutionStrategy",
            "testables",
            "useSatSolver",
        }
        if set(manifest) - allowed_manifest_keys:
            graph_limitations.append("manifest contains an unauthorized package-source key")
        if (
            manifest.get("enableLockFile") is not True
            or manifest.get("resolutionStrategy") != "lowest"
        ):
            graph_limitations.append("lockfile or lowest-resolution policy differs")
        testables = manifest.get("testables", [])
        if not isinstance(testables, list) or any(
            item not in manifest_dependencies for item in testables
        ):
            graph_limitations.append("manifest testables are invalid")
        for name, version in manifest_dependencies.items():
            if (
                not isinstance(name, str)
                or not name.startswith("com.unity.")
                or not isinstance(version, str)
                or REGISTRY_VERSION_PATTERN.fullmatch(version) is None
            ):
                graph_limitations.append(
                    f"invalid manifest dependency {package_label(name)}"
                )
        if set(manifest_dependencies) - set(lock_dependencies):
            graph_limitations.append("lock omits a direct manifest dependency")
        for name, record in lock_dependencies.items():
            if (
                not isinstance(name, str)
                or not name.startswith("com.unity.")
                or not isinstance(record, dict)
                or record.get("source") not in {"registry", "builtin"}
                or not isinstance(record.get("depth"), int)
                or isinstance(record.get("depth"), bool)
                or record.get("depth", -1) < 0
                or not isinstance(record.get("version"), str)
                or REGISTRY_VERSION_PATTERN.fullmatch(record["version"]) is None
                or (
                    record.get("source") == "registry"
                    and record.get("url") != "https://packages.unity.com"
                )
                or (
                    record.get("source") == "builtin"
                    and record.get("url") is not None
                )
            ):
                graph_limitations.append(
                    f"invalid lock record {package_label(name)}"
                )
                continue
            dependencies = record.get("dependencies", {})
            if not isinstance(dependencies, dict):
                graph_limitations.append(
                    "invalid transitive dependencies for "
                    f"{package_label(name)}"
                )
                continue
            for child, version in dependencies.items():
                child_record = lock_dependencies.get(child)
                if (
                    not isinstance(child, str)
                    or not child.startswith("com.unity.")
                    or not isinstance(version, str)
                    or REGISTRY_VERSION_PATTERN.fullmatch(version) is None
                    or not isinstance(child_record, dict)
                    or child_record.get("version") != version
                ):
                    graph_limitations.append(
                        "unresolved or mismatched edge "
                        f"{package_label(name)}->{package_label(child)}"
                    )
        direct_names = set(manifest_dependencies)
        for name, record in lock_dependencies.items():
            if not isinstance(record, dict):
                continue
            depth = record.get("depth")
            if name in direct_names and depth != 0:
                graph_limitations.append(
                    f"direct dependency {package_label(name)} does not have depth 0"
                )
            if name not in direct_names and depth == 0:
                graph_limitations.append(
                    "non-direct dependency "
                    f"{package_label(name)} incorrectly has depth 0"
                )
        reachable = set(direct_names)
        minimum_depth = {name: 0 for name in direct_names}
        frontier = list(direct_names)
        while frontier:
            parent = frontier.pop()
            parent_record = lock_dependencies.get(parent)
            if not isinstance(parent_record, dict):
                continue
            children = parent_record.get("dependencies", {})
            if not isinstance(children, dict):
                continue
            for child in children:
                candidate_depth = minimum_depth[parent] + 1
                improved = (
                    child not in minimum_depth
                    or candidate_depth < minimum_depth[child]
                )
                if improved:
                    minimum_depth[child] = candidate_depth
                    reachable.add(child)
                    frontier.append(child)
        orphaned = set(lock_dependencies) - reachable
        if orphaned:
            graph_limitations.append(
                "lock contains unreachable packages "
                f"{[package_label(name) for name in sorted(orphaned)]!r}"
            )
        for name, expected_depth in minimum_depth.items():
            record = lock_dependencies.get(name)
            if (
                isinstance(record, dict)
                and record.get("depth") != expected_depth
            ):
                graph_limitations.append(
                    f"lock depth for {package_label(name)} "
                    "is not minimal reachable depth "
                    f"{expected_depth}"
                )
        visiting: set[str] = set()
        visited: set[str] = set()

        def visit(package_name: str) -> bool:
            if package_name in visiting:
                return True
            if package_name in visited:
                return False
            visiting.add(package_name)
            record = lock_dependencies.get(package_name)
            children = (
                record.get("dependencies", {})
                if isinstance(record, dict)
                else {}
            )
            if isinstance(children, dict):
                for child in children:
                    if child in lock_dependencies and visit(child):
                        return True
            visiting.remove(package_name)
            visited.add(package_name)
            return False

        if any(visit(name) for name in sorted(lock_dependencies) if name not in visited):
            graph_limitations.append("lock dependency graph contains a cycle")
        graph_safe = not graph_limitations
    file_evidence = [
        evidence_item(
            "duplicate-key-safe-json-read",
            Path("Game/Packages/manifest.json"),
            "WP-0001 project package manifest",
            sha256=manifest_snapshot.sha256,
        ),
        evidence_item(
            "duplicate-key-safe-json-read",
            Path("Game/Packages/packages-lock.json"),
            "WP-0001 Editor-generated package lock",
            sha256=lock_snapshot.sha256,
        ),
    ]
    requirements = [
        _package_requirement_status(
            "UNITY-AI-ASSISTANT",
            "com.unity.ai.assistant",
            f"com.unity.ai.assistant {REQUIRED_ASSISTANT} from Unity Registry",
            manifest_dependencies,
            lock_dependencies,
            version_matches=lambda value: value == REQUIRED_ASSISTANT,
            graph_safe=graph_safe,
            evidence=file_evidence,
            graph_limitations=graph_limitations,
        ),
        _package_requirement_status(
            "URP",
            "com.unity.render-pipelines.universal",
            "com.unity.render-pipelines.universal 17.3 line from Unity Registry",
            manifest_dependencies,
            lock_dependencies,
            version_matches=lambda value: (
                isinstance(value, str)
                and REQUIRED_URP_PATTERN.fullmatch(value) is not None
            ),
            graph_safe=graph_safe,
            evidence=file_evidence,
            graph_limitations=graph_limitations,
        ),
        _package_requirement_status(
            "UNITY-TEST-FRAMEWORK",
            "com.unity.test-framework",
            "com.unity.test-framework 1.6 line from Unity Registry",
            manifest_dependencies,
            lock_dependencies,
            version_matches=lambda value: (
                isinstance(value, str)
                and REQUIRED_TEST_PATTERN.fullmatch(value) is not None
            ),
            graph_safe=graph_safe,
            evidence=file_evidence,
            graph_limitations=graph_limitations,
        ),
    ]
    requirement_statuses = {item["status"] for item in requirements}
    if not graph_safe or "unsafe" in requirement_statuses:
        graph_status = "unsafe"
    elif "missing" in requirement_statuses:
        graph_status = "missing"
    elif "mismatch" in requirement_statuses:
        graph_status = "mismatch"
    elif "unverified" in requirement_statuses:
        graph_status = "unverified"
    else:
        graph_status = "matched"
    graph_value = {
        "manifest_dependencies": manifest_dependencies,
        "lock_dependencies": lock_dependencies,
        "enableLockFile": manifest.get("enableLockFile")
        if isinstance(manifest, dict)
        else None,
        "resolutionStrategy": manifest.get("resolutionStrategy")
        if isinstance(manifest, dict)
        else None,
    }
    return {
        "project_root": "WP0001_GAME_PROJECT_ROOT",
        "manifest": {
            "path": "Game/Packages/manifest.json",
            "sha256": manifest_snapshot.sha256,
        },
        "lock": {
            "path": "Game/Packages/packages-lock.json",
            "sha256": lock_snapshot.sha256,
        },
        "graph_status": graph_status,
        "canonical_graph_sha256": canonical_sha256(graph_value),
        "requirements": requirements,
        "limitations": graph_limitations,
    }


def derive_profile_status(
    toolchain: Iterable[dict[str, Any]],
    package_requirements: Iterable[dict[str, Any]],
) -> tuple[str, list[str]]:
    records = list(toolchain) + list(package_requirements)
    identifiers = [
        item.get("id") for item in records if isinstance(item, dict)
    ]
    if (
        len(records) != len(EXPECTED_REQUIREMENT_IDS)
        or len(identifiers) != len(set(identifiers))
        or set(identifiers) != EXPECTED_REQUIREMENT_IDS
        or any(item.get("status") not in REQUIREMENT_STATUSES for item in records)
    ):
        return "indeterminate", []
    blocking = sorted(
        item["id"] for item in records if item.get("status") in BLOCKING_STATUSES
    )
    if blocking:
        return "blocked", blocking
    if any(item.get("status") == "unverified" for item in records):
        return "indeterminate", []
    if records and all(item.get("status") == "matched" for item in records):
        return "matched", []
    return "indeterminate", []


def _git_dir(repo_root: Path) -> Path:
    marker = repo_root / ".git"
    try:
        metadata = os.lstat(marker)
    except OSError as exc:
        raise UnsafeEvidence(f"cannot inspect repository metadata: {exc}") from exc
    if stat.S_ISDIR(metadata.st_mode) and not stat.S_ISLNK(metadata.st_mode):
        return marker
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        raise UnsafeEvidence("repository .git marker is not a regular file/directory")
    snapshot = read_regular_file(marker, max_bytes=4096)
    try:
        line = snapshot.data.decode("utf-8").strip()
    except UnicodeDecodeError as exc:
        raise UnsafeEvidence("repository .git marker is not UTF-8") from exc
    if not line.startswith("gitdir: "):
        raise UnsafeEvidence("repository .git marker has unknown format")
    target = Path(line.removeprefix("gitdir: "))
    if not target.is_absolute():
        target = repo_root / target
    target = Path(os.path.normpath(str(target)))
    require_safe_directory(target)
    return target


def read_git_head(repo_root: Path) -> str:
    git_dir = _git_dir(repo_root)
    head_path = git_dir / "HEAD"
    head = read_regular_file(head_path, max_bytes=4096).data.decode("ascii").strip()
    if GIT_OBJECT_PATTERN.fullmatch(head):
        return head
    if not head.startswith("ref: "):
        raise UnsafeEvidence("Git HEAD has unknown format")
    reference = head.removeprefix("ref: ")
    relative = PurePosixPath(reference)
    if relative.is_absolute() or ".." in relative.parts:
        raise UnsafeEvidence("Git HEAD reference is unsafe")
    ref_path = git_dir.joinpath(*relative.parts)
    try:
        value = read_regular_file(ref_path, max_bytes=4096).data.decode("ascii").strip()
    except MissingEvidence:
        packed_path = git_dir / "packed-refs"
        packed = read_regular_file(packed_path, max_bytes=16 * 1024 * 1024)
        value = ""
        for raw_line in packed.data.decode("ascii").splitlines():
            if not raw_line or raw_line.startswith(("#", "^")):
                continue
            object_id, separator, name = raw_line.partition(" ")
            if separator and name == reference:
                value = object_id
                break
    if GIT_OBJECT_PATTERN.fullmatch(value) is None:
        raise UnsafeEvidence("Git HEAD object ID is absent or invalid")
    return value


def load_marker_profile(path: Path | None) -> tuple[object | None, dict[str, Any] | None]:
    if path is None:
        return None, None
    snapshot = read_regular_file(path, max_bytes=MAX_JSON_BYTES)
    value = parse_json_bytes(snapshot.data, str(path))
    return value, evidence_item(
        "duplicate-key-safe-json-read",
        Path("CALLER_SUPPLIED_IL2CPP_MARKER_PROFILE"),
        "caller-supplied IL2CPP marker profile; authority is not inferred",
        sha256=snapshot.sha256,
    )


def collect_observation(
    paths: CollectorPaths,
    *,
    captured_at: str | None = None,
    base_commit: str | None = None,
    source_path: Path | None = None,
    marker_profile: object | None = None,
    marker_profile_evidence: dict[str, Any] | None = None,
    host: dict[str, str] | None = None,
) -> dict[str, Any]:
    captured_at = captured_at or utc_now()
    _require_absolute(paths.project_root)
    if paths.project_root.name != "Game":
        raise UnsafeEvidence("project root must be the explicit WP-0001 Game directory")
    if base_commit is None:
        base_commit = read_git_head(paths.repo_root)
    if GIT_OBJECT_PATTERN.fullmatch(base_commit) is None:
        raise UnsafeEvidence("base commit is not a SHA-1 Git object ID")
    source_path = source_path or Path(__file__).resolve()
    source_snapshot = read_regular_file(source_path, max_bytes=MAX_JSON_BYTES)
    try:
        source_label = source_path.relative_to(paths.repo_root).as_posix()
    except ValueError:
        source_label = f"external-source-{opaque_value(str(source_path))}"

    hub = inspect_hub(paths)
    editor = inspect_editor(paths)
    il2cpp = inspect_il2cpp(
        paths,
        editor,
        marker_profile=marker_profile,
        marker_profile_evidence=marker_profile_evidence,
    )
    xcode = inspect_xcode(paths)
    dotnet = inspect_dotnet(paths)
    rosetta = inspect_rosetta(paths)
    toolchain = [hub, editor, il2cpp, xcode, dotnet, rosetta]
    package_graph = inspect_package_graph(paths)
    profile_status, blocking = derive_profile_status(
        toolchain,
        package_graph["requirements"],
    )
    host_record = host or {
        "os": "macOS" if platform.system() == "Darwin" else platform.system(),
        "os_version": platform.release(),
        "architecture": platform.machine(),
    }
    if (
        set(host_record) != {"os", "os_version", "architecture"}
        or any(not isinstance(value, str) or not value for value in host_record.values())
    ):
        raise UnsafeEvidence("host metadata has an invalid shape")
    host_compatible = (
        host_record["os"] == "macOS"
        and host_record["architecture"] == "arm64"
    )
    if profile_status == "matched" and not host_compatible:
        profile_status = "indeterminate"
    observation_stamp = re.sub(r"[^0-9]", "", captured_at)[:14]
    observation = {
        "schema_version": 1,
        "document_kind": DOCUMENT_KIND,
        "observation_id": f"OBS-WP0001-STATIC-{observation_stamp}Z",
        "packet_id": PACKET_ID,
        "packet_contract_sha256": PACKET_CONTRACT_SHA256,
        "base_commit": base_commit,
        "captured_at": captured_at,
        "collector": {
            "name": COLLECTOR_NAME,
            "version": COLLECTOR_VERSION,
            "source_path": source_label,
            "source_sha256": source_snapshot.sha256,
        },
        "authority": {
            "current_autonomy": "A0",
            "activation_authority": False,
            "activation_evidence_eligible": False,
            "a1_activation_receipt_id": None,
        },
        "collection": {
            "mode": "static-filesystem-only",
            "status": "complete",
            "contains_secret_values": False,
            "unity_family_processes_invoked": False,
            "external_processes_invoked": False,
            "network_accessed": False,
        },
        "host": host_record,
        "toolchain": toolchain,
        "package_graph": package_graph,
        "profile_status": profile_status,
        "blocking_requirement_ids": blocking,
        "evidence_limitations": [
            "This A0 diagnostic has no activation or implementation authority.",
            "Static files cannot prove installer provenance, accepted terms, "
            "loaded process bytes, successful IL2CPP builds, or runtime behavior.",
            "The collector does not inspect Unity account, seat, organization, "
            "project linkage, license, keychain, logs, or cloud identity state.",
            "The collector does not prove MCP configuration, allowlists, handshakes, "
            "socket/FD identity, sandbox attachment, network policy, or quarantine.",
            "base_commit identifies Git HEAD but does not attest worktree cleanliness; "
            "collector.source_sha256 separately identifies the collector bytes.",
            (
                "A non-macOS or non-ARM64 host prevents a matched profile."
                if not host_compatible
                else "Host OS and architecture match the macOS ARM64 target."
            ),
            "No-follow and inode-stability checks reduce path races but do not "
            "constitute a hostile-process quarantine; activation requires the "
            "separate physical boundary.",
            "Filesystem facts can drift immediately after capture.",
            "A matched diagnostic still requires creator screenshots, protected "
            "evidence, a physical boundary, and a distinct activation receipt.",
        ],
    }
    require_secret_free_output(observation)
    return observation


def _path_is_within(path: Path, parent: Path) -> bool:
    try:
        path.relative_to(parent)
    except ValueError:
        return False
    return True


def write_output(path: Path, data: bytes, *, repo_root: Path) -> None:
    _require_absolute(path)
    activation_root = (
        repo_root / "docs" / "evidence" / "WP-0001" / "a1-activation"
    ).resolve(strict=False)
    normalized = path.resolve(strict=False)
    if _path_is_within(normalized, activation_root):
        raise UnsafeEvidence(
            "static diagnostics cannot be written beneath a1-activation/"
        )
    _parent_components_safe(path)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_CLOEXEC"):
        flags |= os.O_CLOEXEC
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(path, flags, 0o600)
    try:
        offset = 0
        while offset < len(data):
            offset += os.write(descriptor, data[offset:])
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def parse_args(argv: list[str]) -> argparse.Namespace:
    repo_root = Path(__file__).resolve().parents[3]
    parser = argparse.ArgumentParser(
        description=(
            "Inspect the exact WP-0001 toolchain using static files only. "
            "No Unity-family or external process is started."
        )
    )
    parser.add_argument(
        "--project-root",
        type=Path,
        default=repo_root / "Game",
        help="Exact WP-0001 Game project root to inspect (default: repository Game).",
    )
    parser.add_argument(
        "--il2cpp-marker-profile",
        type=Path,
        help=(
            "Optional protected JSON marker profile. Supplying a file does not "
            "establish its authority."
        ),
    )
    parser.add_argument(
        "--output",
        type=Path,
        help=(
            "Create a new secret-free JSON file. Paths beneath the WP-0001 "
            "a1-activation directory are rejected. Default: stdout."
        ),
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(list(argv or sys.argv[1:]))
    repo_root = Path(__file__).resolve().parents[3]
    project_root = Path(os.path.abspath(args.project_root))
    try:
        marker_profile, marker_evidence = load_marker_profile(
            Path(os.path.abspath(args.il2cpp_marker_profile))
            if args.il2cpp_marker_profile is not None
            else None
        )
        observation = collect_observation(
            CollectorPaths.defaults(repo_root, project_root),
            marker_profile=marker_profile,
            marker_profile_evidence=marker_evidence,
        )
        encoded = (
            json.dumps(observation, indent=2, sort_keys=True, ensure_ascii=False)
            + "\n"
        ).encode("utf-8")
        if args.output is None:
            sys.stdout.buffer.write(encoded)
        else:
            write_output(
                Path(os.path.abspath(args.output)),
                encoded,
                repo_root=repo_root,
            )
    except (MissingEvidence, UnsafeEvidence, OSError, ValueError) as exc:
        print(f"STATIC TOOLCHAIN INSPECTION ERROR: {exc}", file=sys.stderr)
        return 2
    return 0 if observation["profile_status"] == "matched" else 1


if __name__ == "__main__":
    raise SystemExit(main())
