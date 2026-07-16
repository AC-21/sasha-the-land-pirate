#!/usr/bin/env python3
"""Read-only live verifier for the prepared WP-0001 direct MCP route.

The verifier inspects an already-running process and filesystem boundary. It
never starts Unity, Unity Hub, the Assistant relay, Codex, or an MCP session.
It also never emits credential values or raw process environments.
"""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
import os
import re
import stat
import struct
import subprocess
import sys
import time
import tomllib
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable


VALIDATOR_VERSION = "wp0001-mcp-live-v1"
CAPTURE_SCHEMA_VERSION = 1
SYSTEM_SYSCTL = "/usr/sbin/sysctl"
SYSTEM_CODESIGN = "/usr/bin/codesign"
SYSTEM_DEFAULTS = "/usr/bin/defaults"
SYSTEM_LSOF = "/usr/sbin/lsof"
SYSTEM_PS = "/bin/ps"
SYSTEM_CODEX_CONFIG = Path("/etc/codex/config.toml")
SYSTEM_CODEX_HOOKS = Path("/etc/codex/hooks.json")
SYSTEM_CODEX_MANAGED_CONFIG = Path("/etc/codex/managed_config.toml")
SYSTEM_CODEX_PLUGINS = Path("/etc/codex/plugins")
SYSTEM_CODEX_REQUIREMENTS = Path("/etc/codex/requirements.toml")
SYSTEM_MANAGED_PREFERENCES_ROOT = Path("/Library/Managed Preferences")
MACOS_TMP_ALIAS = Path("/tmp")
MACOS_PRIVATE_TMP = Path("/private/tmp")
SYSTEM_ROOT_UID = 0
CODEX_MDM_DOMAIN = "com.openai.codex"
CODEX_MDM_KEYS = (
    "config_toml_base64",
    "requirements_toml_base64",
)
APPROVED_READ_ONLY_TOOLS = {
    SYSTEM_SYSCTL,
    SYSTEM_CODESIGN,
    SYSTEM_DEFAULTS,
    SYSTEM_LSOF,
    SYSTEM_PS,
}
SAFE_SUBPROCESS_ENV = {
    "HOME": "/var/empty",
    "XDG_CONFIG_HOME": "/var/empty",
    "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
    "LANG": "C",
    "LC_ALL": "C",
}
ABSENT_CREDENTIAL_VARIABLES = (
    "AWS_ACCESS_KEY_ID",
    "AWS_SECRET_ACCESS_KEY",
    "AWS_SESSION_TOKEN",
    "GH_TOKEN",
    "GITHUB_TOKEN",
    "GIT_ASKPASS",
    "SSH_AUTH_SOCK",
    "VERCEL_TOKEN",
)
SAFE_PROCESS_ENVIRONMENT_NAMES = frozenset(
    {
        "HOME",
        "TMPDIR",
        "TMP",
        "TEMP",
        "CODEX_HOME",
        "XDG_CONFIG_HOME",
        "XDG_CACHE_HOME",
        "XDG_DATA_HOME",
        "GIT_CONFIG_NOSYSTEM",
        "GIT_CONFIG_GLOBAL",
        "GIT_TERMINAL_PROMPT",
        "PATH",
        "LANG",
        "LC_ALL",
        "USER",
        "LOGNAME",
        "SHELL",
        "TERM",
        "TERM_PROGRAM",
        "TERM_PROGRAM_VERSION",
        "PWD",
        "SHLVL",
        "_",
        "__CFBundleIdentifier",
        "__CF_USER_TEXT_ENCODING",
        "COMMAND_MODE",
    }
)
CONNECTION_RECORD_KEYS = {
    "connection_type",
    "connection_path",
    "created_date",
    "project_path",
    "protocol_version",
    "editor_pid",
}
PROCESS_CHECK_NAMES = (
    "process_inspection_complete",
    "pid_matches",
    "uid_matches",
    "start_time_matches",
    "identity_stable_across_inspection",
    "executable_is_regular_not_symlink",
    "executable_path_ancestry_safe",
    "executable_vnode_stable",
    "executable_path_matches",
    "executable_sha256_matches",
    "cwd_path_ancestry_safe",
    "cwd_matches",
    "arguments_match",
    "process_birth_id_matches",
    "signing_identity_matches",
    "environment_names_safe",
    "environment_names_hash_matches",
    "required_environment_values_match",
    "credential_environment_absent",
)
CONNECTION_CHECK_NAMES = (
    "record_is_object",
    "keys_exact",
    "connection_type_matches",
    "connection_path_matches",
    "project_path_matches",
    "protocol_version_matches",
    "editor_pid_matches",
    "created_date_valid",
)
RUNTIME_SERVER_CHECK_NAMES = (
    "runtime_server_is_table",
    "keys_exact",
    "command_matches",
    "arguments_match",
    "enabled_exact",
    "required_matches",
    "enabled_tools_match",
    "disabled_tools_do_not_overlap",
    "default_tools_approval_mode_matches",
    "environment_matches",
)
FD_GRAPH_CHECK_NAMES = (
    "inspection_complete",
    "endpoint_path_exact",
    "processes_distinct",
    "client_pid_exact",
    "editor_pid_exact",
    "relay_pid_exact",
    "editor_endpoint_listener_owned_by_exact_pid",
    "relay_connected_peer_open_by_exact_pid",
    "relay_peer_bound_to_editor_endpoint_fd",
    "client_relay_stdin_pipe_bound",
    "client_relay_stdout_pipe_bound",
    "client_relay_pipe_pairs_distinct",
    "client_relay_channel_proven",
    "channel_address_hashes_present",
    "records_sanitized",
    "digest_recomputed",
)
EXPECTED_CHECK_NAMES = frozenset(
    {
        *(f"client.{name}" for name in PROCESS_CHECK_NAMES),
        *(f"relay.{name}" for name in PROCESS_CHECK_NAMES),
        *(f"bridge.{name}" for name in PROCESS_CHECK_NAMES),
        *(f"connection.{name}" for name in CONNECTION_CHECK_NAMES),
        *(f"config.server.{name}" for name in RUNTIME_SERVER_CHECK_NAMES),
        "relay.parent_pid_matches_client",
        "client.arguments_policy_safe",
        "bridge.arguments_policy_safe",
        "relay.package_copy_regular_not_symlink",
        "relay.package_copy_path_ancestry_safe",
        "relay.package_copy_hash_matches",
        "connection.file_regular_not_symlink",
        "connection.file_path_ancestry_safe",
        "connection.file_sha256_matches",
        "connection.created_after_editor_start",
        "connection.created_before_capture",
        "connection.session_record_hash_matches",
        "endpoint.exists",
        "endpoint.is_socket",
        "endpoint.not_symlink",
        "endpoint.path_ancestry_safe",
        "endpoint.reported_to_canonical_binding_safe",
        "endpoint.owner_uid_matches",
        "endpoint.mode_matches",
        "endpoint.shared_temp_exception_exact",
        *(f"fd_graph.{name}" for name in FD_GRAPH_CHECK_NAMES),
        "fd_graph.session_hash_matches",
        "session.identity_hash_matches",
        "environment.guard_present",
        "environment.guard_matches_runtime",
        "environment.guard_matches_derived_home",
        "environment.runtime_paths_ancestry_safe",
        "environment.values_match",
        "environment.credential_variables_absent",
        "environment.hash_bindings_match",
        "config.runtime_path_under_codex_home",
        "config.runtime_path_ancestry_safe",
        "config.inventory_inspection_complete",
        "config.all_inventory_paths_ancestry_safe",
        "config.exactly_one_active_server",
        "config.no_unexpected_project_or_ancestor_servers",
        "config.no_unexpected_config_layers_or_features",
        "config.no_hooks_or_plugins",
        "config.no_requirements_or_managed_layers",
        "config.no_profile_layers",
        "config.no_mdm_preferences",
        "config.runtime_codex_home_entries_exact",
        "config.protected_candidate_config_present",
        "config.protected_candidate_server_disabled_exact",
        "config.runtime_server_set_exact",
        "config.runtime_regular_not_symlink",
        "config.runtime_hash_matches_session",
        "config.runtime_hash_matches_handshake",
        "config.runtime_hash_matches_policy_evidence",
        "config.inventory_hash_bindings_match",
        "allowlist.is_nonempty_list",
        "allowlist.client_visible_exact",
        "allowlist.policy_hash_matches",
        "allowlist.handshake_hash_matches",
        "allowlist.session_hash_matches",
        "boot_session.matches_manifest",
        "route.is_direct_external",
        "boundary.packet_is_wp0001",
    }
)


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        separators=(",", ":"),
        sort_keys=True,
    ).encode("utf-8")


def canonical_sha256(value: Any) -> str:
    return hashlib.sha256(canonical_json_bytes(value)).hexdigest()


def client_arguments_policy_safe(arguments: object) -> bool:
    if not isinstance(arguments, list) or not arguments:
        return False
    if any(not isinstance(argument, str) or not argument for argument in arguments):
        return False
    executable = Path(arguments[0]).name
    return (
        len(arguments) == 1
        and executable == "codex-mcp-client"
    ) or (
        len(arguments) == 2
        and executable == "codex"
        and arguments[1] == "mcp-client"
    )


def editor_arguments_policy_safe(
    arguments: object,
    project_path: object,
) -> bool:
    return (
        isinstance(arguments, list)
        and len(arguments) == 3
        and all(isinstance(argument, str) for argument in arguments)
        and Path(arguments[0]).name == "Unity"
        and arguments[1] == "-projectPath"
        and isinstance(project_path, str)
        and Path(arguments[2]).resolve(strict=False)
        == Path(project_path).resolve(strict=False)
    )


def route_contract(boundary: dict[str, Any]) -> dict[str, Any]:
    """Project the stable route contract without circular evidence references."""
    route = json.loads(json.dumps(boundary.get("unity_mcp_route", {})))
    if isinstance(route, dict):
        route.pop("process_observation", None)
        for section, evidence_key in (
            ("bridge", "discovery_record"),
            ("entitlement", "evidence"),
            ("project_identity", "evidence"),
            ("codex_policy", "evidence"),
            ("handshake", "transcript"),
            ("activation_session", "evidence"),
            ("controls", "observation"),
        ):
            value = route.get(section)
            if isinstance(value, dict):
                value.pop(evidence_key, None)
    return {
        "contract_version": 1,
        "packet_id": boundary.get("packet_id"),
        "packet_contract_sha256": boundary.get("packet_contract_sha256"),
        "repository": boundary.get("repository"),
        "reservation": boundary.get("reservation"),
        "approved_toolchain": boundary.get("approved_toolchain"),
        "approved_environment": boundary.get("approved_environment"),
        "runtime_boundary": boundary.get("runtime_boundary"),
        "foundation_binding": boundary.get("foundation_binding"),
        "protection_boundary": boundary.get("protection_boundary"),
        "credential_boundary": boundary.get("credential_boundary"),
        "manual_import_boundary": boundary.get("manual_import_boundary"),
        "project_seed": boundary.get("project_seed"),
        "wp0001_toolchain_profile": boundary.get("wp0001_toolchain_profile"),
        "unity_mcp_route": route,
    }


def session_identity_contract(
    route: dict[str, Any],
    runtime: dict[str, Any],
    fd_graph_sha256: object,
) -> dict[str, Any]:
    client = route.get("client", {})
    relay = route.get("relay", {})
    bridge = route.get("bridge", {})
    policy = route.get("codex_policy", {})
    session = route.get("activation_session", {})
    policy_evidence = (
        policy.get("evidence")
        if isinstance(policy.get("evidence"), dict)
        else {}
    )
    return {
        "contract_version": 1,
        "boot_session_sha256": runtime.get("boot_session_sha256"),
        "client_process_birth_id_sha256": client.get(
            "process_birth_id_sha256"
        ),
        "relay_process_birth_id_sha256": relay.get(
            "process_birth_id_sha256"
        ),
        "editor_process_birth_id_sha256": bridge.get(
            "process_birth_id_sha256"
        ),
        "runtime_config_sha256": policy_evidence.get("sha256"),
        "enabled_tools_sha256": policy.get("enabled_tools_sha256"),
        "environment_sha256": policy.get("environment_sha256"),
        "server_inventory_sha256": policy.get(
            "effective_server_inventory_sha256"
        ),
        "connection_record_sha256": bridge.get(
            "connection_file_sha256"
        ),
        "fd_graph_sha256": fd_graph_sha256,
        "captured_at": session.get("captured_at"),
        "creator_approved": session.get("creator_approved"),
    }


def session_identity_sha256(
    route: dict[str, Any],
    runtime: dict[str, Any],
    fd_graph_sha256: object,
) -> str:
    return canonical_sha256(
        session_identity_contract(route, runtime, fd_graph_sha256)
    )


def path_lexists(path: Path) -> bool:
    try:
        path.lstat()
        return True
    except FileNotFoundError:
        return False
    except OSError:
        # Fail closed: an uninspectable supported layer is not equivalent to
        # a proven-absent layer.
        return True


def path_ancestry_facts(
    path: Path,
    *,
    require_leaf: bool,
) -> dict[str, Any]:
    """Inspect a path component-by-component without following symlinks."""

    raw = os.fspath(path)
    absolute = path.is_absolute()
    lexically_clean = (
        absolute
        and (raw == "/" or not raw.startswith("//"))
        and ".." not in path.parts
        and os.path.normpath(raw) == raw
    )
    symlink_components: list[str] = []
    missing_component: str | None = None
    inspection_error: str | None = None
    leaf_exists = False
    if not lexically_clean:
        return {
            "absolute": absolute,
            "lexically_clean": lexically_clean,
            "leaf_exists": False,
            "symlink_components": [],
            "missing_component": None,
            "inspection_error": None,
            "safe": False,
        }

    current = Path(path.anchor)
    components = path.parts[1:]
    for index, component in enumerate(components):
        current /= component
        is_leaf = index == len(components) - 1
        try:
            info = current.lstat()
        except FileNotFoundError:
            missing_component = str(current)
            if not is_leaf:
                inspection_error = "an ancestor component is missing"
            break
        except OSError as exc:
            inspection_error = f"{type(exc).__name__}: ancestry inspection failed"
            break
        if stat.S_ISLNK(info.st_mode):
            symlink_components.append(str(current))
            break
        if not is_leaf and not stat.S_ISDIR(info.st_mode):
            inspection_error = "an ancestor component is not a directory"
            break
        if is_leaf:
            leaf_exists = True

    if not components:
        leaf_exists = True
    safe = (
        lexically_clean
        and not symlink_components
        and inspection_error is None
        and (leaf_exists or not require_leaf)
    )
    return {
        "absolute": absolute,
        "lexically_clean": lexically_clean,
        "leaf_exists": leaf_exists,
        "symlink_components": symlink_components,
        "missing_component": missing_component,
        "inspection_error": inspection_error,
        "safe": safe,
    }


def path_ancestry_is_safe(path: Path, *, require_leaf: bool = True) -> bool:
    return bool(
        path_ancestry_facts(path, require_leaf=require_leaf).get("safe")
    )


def require_safe_path_ancestry(
    path: Path,
    *,
    require_leaf: bool = True,
) -> None:
    facts = path_ancestry_facts(path, require_leaf=require_leaf)
    if facts.get("safe") is not True:
        raise ValueError(f"{path} has unsafe or incomplete path ancestry")


def paths_lexically_equal(left: Path, right: Path) -> bool:
    return (
        left.is_absolute()
        and right.is_absolute()
        and ".." not in left.parts
        and ".." not in right.parts
        and os.path.normpath(os.fspath(left))
        == os.path.normpath(os.fspath(right))
    )


def canonical_physical_path(
    reported: Path,
    *,
    allow_macos_tmp_alias: bool = False,
) -> dict[str, Any]:
    """Bind a reported path to a symlink-free physical path.

    The sole exception is the macOS system `/tmp -> /private/tmp` alias.
    """

    if path_ancestry_is_safe(reported):
        return {
            "reported": str(reported),
            "canonical": str(reported),
            "alias_used": False,
            "safe": True,
        }
    result = {
        "reported": str(reported),
        "canonical": None,
        "alias_used": False,
        "safe": False,
    }
    if not allow_macos_tmp_alias or sys.platform != "darwin":
        return result
    try:
        relative = reported.relative_to(MACOS_TMP_ALIAS)
    except ValueError:
        return result
    if not relative.parts or ".." in relative.parts:
        return result
    try:
        alias_info = MACOS_TMP_ALIAS.lstat()
        target = Path(os.readlink(MACOS_TMP_ALIAS))
        physical_info = MACOS_PRIVATE_TMP.lstat()
    except OSError:
        return result
    expanded_target = (
        target
        if target.is_absolute()
        else MACOS_TMP_ALIAS.parent / target
    )
    alias_exact = (
        stat.S_ISLNK(alias_info.st_mode)
        and alias_info.st_uid == SYSTEM_ROOT_UID
        and paths_lexically_equal(expanded_target, MACOS_PRIVATE_TMP)
        and path_ancestry_is_safe(MACOS_PRIVATE_TMP)
        and stat.S_ISDIR(physical_info.st_mode)
        and physical_info.st_uid == SYSTEM_ROOT_UID
        and stat.S_IMODE(physical_info.st_mode) == 0o1777
    )
    canonical = MACOS_PRIVATE_TMP / relative
    safe = alias_exact and path_ancestry_is_safe(canonical)
    return {
        "reported": str(reported),
        "canonical": str(canonical) if alias_exact else None,
        "alias_used": alias_exact,
        "safe": safe,
    }


def _open_regular_nofollow(path: Path) -> tuple[int, os.stat_result]:
    require_safe_path_ancestry(path)
    before = path.lstat()
    if stat.S_ISLNK(before.st_mode) or not stat.S_ISREG(before.st_mode):
        raise ValueError(f"{path} is not a regular non-symlink file")
    if len(path.parts) < 2:
        raise ValueError(f"{path} has no leaf component")
    directory_flags = (
        os.O_RDONLY
        | getattr(os, "O_CLOEXEC", 0)
        | getattr(os, "O_DIRECTORY", 0)
        | getattr(os, "O_NOFOLLOW", 0)
    )
    file_flags = (
        os.O_RDONLY
        | getattr(os, "O_CLOEXEC", 0)
        | getattr(os, "O_NOFOLLOW", 0)
    )
    directory_descriptor = os.open(path.anchor, directory_flags)
    try:
        for component in path.parts[1:-1]:
            next_descriptor = os.open(
                component,
                directory_flags,
                dir_fd=directory_descriptor,
            )
            os.close(directory_descriptor)
            directory_descriptor = next_descriptor
        descriptor = os.open(
            path.parts[-1],
            file_flags,
            dir_fd=directory_descriptor,
        )
    finally:
        os.close(directory_descriptor)
    after = os.fstat(descriptor)
    final = path.lstat()
    if (
        not stat.S_ISREG(after.st_mode)
        or before.st_dev != after.st_dev
        or before.st_ino != after.st_ino
        or final.st_dev != after.st_dev
        or final.st_ino != after.st_ino
        or stat.S_ISLNK(final.st_mode)
    ):
        os.close(descriptor)
        raise ValueError(f"{path} changed during inspection")
    return descriptor, after


def read_regular_file_bytes(path: Path) -> tuple[bytes, dict[str, Any]]:
    descriptor, info = _open_regular_nofollow(path)
    try:
        chunks: list[bytes] = []
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            chunks.append(chunk)
    finally:
        os.close(descriptor)
    data = b"".join(chunks)
    return data, {
        "exists": True,
        "regular": True,
        "symlink": False,
        "sha256": hashlib.sha256(data).hexdigest(),
        "uid": info.st_uid,
        "mode": f"{stat.S_IMODE(info.st_mode):04o}",
    }


def load_json_object(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError(f"{path} must contain a JSON object")
    return value


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso_utc(value: datetime) -> str:
    return value.astimezone(timezone.utc).replace(microsecond=0).isoformat().replace(
        "+00:00",
        "Z",
    )


def parse_datetime(value: str) -> datetime:
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        raise ValueError("timestamp has no timezone")
    return parsed.astimezone(timezone.utc)


def run_read_only(args: list[str]) -> subprocess.CompletedProcess[bytes]:
    if not args or args[0] not in APPROVED_READ_ONLY_TOOLS:
        raise ValueError("unapproved read-only system tool")
    return subprocess.run(
        args,
        check=False,
        capture_output=True,
        stdin=subprocess.DEVNULL,
        env=SAFE_SUBPROCESS_ENV,
        timeout=15,
    )


def boot_session_sha256() -> str:
    result = run_read_only([SYSTEM_SYSCTL, "-n", "kern.boottime"])
    raw = result.stdout.strip()
    if (
        result.returncode != 0
        or not raw
        or re.search(rb"\bsec\s*=\s*[0-9]+\b", raw) is None
    ):
        raise ValueError("boot-session inspection failed")
    return hashlib.sha256(raw).hexdigest()


def parse_codesign_identity(
    output: str,
    returncode: int = 0,
    *,
    strict_verified: bool = False,
) -> dict[str, Any]:
    authorities: list[str] = []
    identifier: str | None = None
    team_identifier: str | None = None
    cdhash: str | None = None
    designated_requirement: str | None = None
    signature: str | None = None
    for raw_line in output.splitlines():
        line = raw_line.strip()
        if line.startswith("Authority="):
            authorities.append(line.split("=", 1)[1])
        elif line.startswith("Identifier="):
            identifier = line.split("=", 1)[1]
        elif line.startswith("TeamIdentifier="):
            team_identifier = line.split("=", 1)[1]
        elif line.startswith("CDHash="):
            cdhash = line.split("=", 1)[1].lower()
        elif line.startswith("designated =>"):
            designated_requirement = line
        elif line.startswith("Signature="):
            signature = line.split("=", 1)[1]
    unsigned = "not signed at all" in output.lower()
    primary = authorities[0] if authorities else ("unsigned" if unsigned else None)
    return {
        "available": returncode == 0 or unsigned,
        "strict_verified": strict_verified,
        "primary_identity": primary,
        "authorities": authorities,
        "identifier": identifier,
        "team_identifier": team_identifier,
        "cdhash": cdhash,
        "designated_requirement": designated_requirement,
        "designated_requirement_sha256": (
            hashlib.sha256(designated_requirement.encode("utf-8")).hexdigest()
            if isinstance(designated_requirement, str)
            else None
        ),
        "authorities_sha256": canonical_sha256(authorities),
        "signature": signature,
    }


def codesign_identity(path: Path) -> dict[str, Any]:
    verification = run_read_only(
        [
            SYSTEM_CODESIGN,
            "--verify",
            "--strict",
            "--verbose=4",
            str(path),
        ]
    )
    display = run_read_only(
        [SYSTEM_CODESIGN, "-d", "--verbose=4", str(path)]
    )
    requirement = run_read_only(
        [SYSTEM_CODESIGN, "-d", "--verbose=4", "-r-", str(path)]
    )
    combined = (
        display.stdout
        + display.stderr
        + requirement.stdout
        + requirement.stderr
    ).decode("utf-8", errors="replace")
    return parse_codesign_identity(
        combined,
        display.returncode if display.returncode != 0 else requirement.returncode,
        strict_verified=verification.returncode == 0,
    )


def code_identity_matches(
    observed: dict[str, Any],
    expected_identity: object,
) -> bool:
    return (
        isinstance(expected_identity, dict)
        and observed.get("available") is True
        and observed.get("strict_verified") is True
        and expected_identity.get("verification_scope")
        == "codesign-strict-component"
        and observed.get("identifier") == expected_identity.get("identifier")
        and observed.get("team_identifier")
        == expected_identity.get("team_identifier")
        and observed.get("cdhash") == expected_identity.get("cdhash")
        and observed.get("designated_requirement_sha256")
        == expected_identity.get("designated_requirement_sha256")
        and observed.get("authorities_sha256")
        == expected_identity.get("authorities_sha256")
    )


def parse_procargs_buffer(
    data: bytes,
    *,
    value_names: Iterable[str],
    presence_only_names: Iterable[str],
) -> tuple[list[str], dict[str, str], list[str], list[str], list[str]]:
    """Parse macOS KERN_PROCARGS2 bytes without retaining unapproved values."""

    if len(data) < struct.calcsize("i"):
        raise ValueError("process argument buffer is truncated")
    argc = struct.unpack_from("i", data, 0)[0]
    if argc < 1 or argc > 100_000:
        raise ValueError("process argument count is invalid")
    offset = struct.calcsize("i")

    def next_nul(start: int) -> int:
        end = data.find(b"\0", start)
        if end < 0:
            raise ValueError("process argument buffer is malformed")
        return end

    offset = next_nul(offset) + 1  # executable path
    while offset < len(data) and data[offset] == 0:
        offset += 1

    argv: list[str] = []
    for _ in range(argc):
        end = next_nul(offset)
        argv.append(data[offset:end].decode("utf-8", errors="surrogateescape"))
        offset = end + 1

    allowed_values = set(value_names)
    presence_only = set(presence_only_names)
    values: dict[str, str] = {}
    present: set[str] = set()
    names: list[str] = []
    while offset < len(data):
        while offset < len(data) and data[offset] == 0:
            offset += 1
        if offset >= len(data):
            break
        end = next_nul(offset)
        item = data[offset:end]
        offset = end + 1
        separator = item.find(b"=")
        if separator <= 0:
            continue
        key = item[:separator].decode("ascii", errors="ignore")
        names.append(key)
        if key in allowed_values:
            values[key] = item[separator + 1 :].decode(
                "utf-8",
                errors="surrogateescape",
            )
        elif key in presence_only:
            present.add(key)
    name_counts = Counter(names)
    duplicates = sorted(
        name for name, count in name_counts.items() if count > 1
    )
    return argv, values, sorted(present), sorted(name_counts), duplicates


def macos_procargs(
    pid: int,
    *,
    value_names: Iterable[str],
    presence_only_names: Iterable[str],
) -> tuple[list[str], dict[str, str], list[str], list[str], list[str]]:
    libc = ctypes.CDLL("/usr/lib/libc.dylib", use_errno=True)
    mib = (ctypes.c_int * 3)(1, 49, pid)  # CTL_KERN, KERN_PROCARGS2
    size = ctypes.c_size_t()
    if libc.sysctl(mib, 3, None, ctypes.byref(size), None, 0) != 0:
        raise OSError(ctypes.get_errno(), "sysctl KERN_PROCARGS2 size failed")
    buffer = ctypes.create_string_buffer(size.value)
    if libc.sysctl(mib, 3, buffer, ctypes.byref(size), None, 0) != 0:
        raise OSError(ctypes.get_errno(), "sysctl KERN_PROCARGS2 read failed")
    return parse_procargs_buffer(
        buffer.raw[: size.value],
        value_names=value_names,
        presence_only_names=presence_only_names,
    )


def linux_procargs(
    pid: int,
    *,
    value_names: Iterable[str],
    presence_only_names: Iterable[str],
) -> tuple[list[str], dict[str, str], list[str], list[str], list[str]]:
    argv = [
        item.decode("utf-8", errors="surrogateescape")
        for item in Path(f"/proc/{pid}/cmdline").read_bytes().split(b"\0")
        if item
    ]
    allowed_values = set(value_names)
    presence_only = set(presence_only_names)
    values: dict[str, str] = {}
    present: set[str] = set()
    names: list[str] = []
    for item in Path(f"/proc/{pid}/environ").read_bytes().split(b"\0"):
        key, separator, value = item.partition(b"=")
        if not separator:
            continue
        decoded_key = key.decode("ascii", errors="ignore")
        names.append(decoded_key)
        if decoded_key in allowed_values:
            values[decoded_key] = value.decode(
                "utf-8",
                errors="surrogateescape",
            )
        elif decoded_key in presence_only:
            present.add(decoded_key)
    name_counts = Counter(names)
    duplicates = sorted(
        name for name, count in name_counts.items() if count > 1
    )
    return argv, values, sorted(present), sorted(name_counts), duplicates


def process_args_environment(
    pid: int,
    *,
    value_names: Iterable[str],
    presence_only_names: Iterable[str],
) -> tuple[list[str], dict[str, str], list[str], list[str], list[str]]:
    if sys.platform == "darwin":
        return macos_procargs(
            pid,
            value_names=value_names,
            presence_only_names=presence_only_names,
        )
    if sys.platform.startswith("linux"):
        return linux_procargs(
            pid,
            value_names=value_names,
            presence_only_names=presence_only_names,
        )
    raise OSError("exact process argv/environment inspection is unsupported")


def process_executable_path(pid: int) -> Path:
    if sys.platform == "darwin":
        libc = ctypes.CDLL("/usr/lib/libproc.dylib", use_errno=True)
        buffer = ctypes.create_string_buffer(4096)
        length = libc.proc_pidpath(pid, buffer, len(buffer))
        if length <= 0:
            raise OSError(ctypes.get_errno(), "proc_pidpath failed")
        return Path(buffer.value.decode("utf-8", errors="surrogateescape"))
    if sys.platform.startswith("linux"):
        return Path(os.readlink(f"/proc/{pid}/exe"))
    raise OSError("exact process executable inspection is unsupported")


def process_cwd(pid: int) -> Path:
    if sys.platform.startswith("linux"):
        return Path(os.readlink(f"/proc/{pid}/cwd"))
    result = run_read_only(
        [SYSTEM_LSOF, "-a", "-p", str(pid), "-d", "cwd", "-Fn"]
    )
    if result.returncode != 0:
        raise OSError("lsof cwd inspection failed")
    paths = [
        line[1:]
        for line in result.stdout.decode("utf-8", errors="replace").splitlines()
        if line.startswith("n/")
    ]
    if len(paths) != 1:
        raise OSError("lsof returned no unique cwd")
    return Path(paths[0])


def parse_lsof_field_records(
    output: bytes,
) -> tuple[list[dict[str, Any]], list[str]]:
    """Parse null-delimited lsof fields for internal cross-correlation."""

    records: list[dict[str, Any]] = []
    errors: list[str] = []
    current_pid: int | None = None
    current: dict[str, Any] | None = None

    def finish_current() -> None:
        nonlocal current
        if current is None:
            return
        if (
            isinstance(current.get("pid"), int)
            and isinstance(current.get("fd_raw"), str)
            and isinstance(current.get("type"), str)
            and isinstance(current.get("name"), str)
        ):
            records.append(current)
        else:
            errors.append("lsof file record is incomplete")
        current = None

    for raw_field in output.split(b"\0"):
        field = raw_field.strip(b"\r\n")
        if not field:
            continue
        key = chr(field[0])
        value = field[1:].decode("utf-8", errors="surrogateescape")
        if key == "p":
            finish_current()
            try:
                current_pid = int(value)
            except ValueError:
                current_pid = None
                errors.append("lsof process id is malformed")
        elif key == "f":
            finish_current()
            current = {
                "pid": current_pid,
                "fd_raw": value,
                "type": None,
                "name": None,
            }
        elif key in {"t", "n"}:
            if current is None:
                errors.append("lsof field appeared outside a file record")
                continue
            destination = "type" if key == "t" else "name"
            if current.get(destination) is not None:
                errors.append(f"lsof {destination} field is duplicated")
            current[destination] = value
        else:
            errors.append("lsof returned an unexpected field")
    finish_current()
    return records, errors


def parse_lsof_human_records(
    output: bytes,
) -> tuple[list[dict[str, Any]], list[str]]:
    """Parse macOS lsof table output; callers must sanitize before emission."""

    lines = [
        line
        for line in output.decode(
            "utf-8",
            errors="surrogateescape",
        ).splitlines()
        if line.strip()
    ]
    if not lines:
        return [], ["lsof human output is empty"]
    header = lines[0].split()
    if header[:7] != [
        "COMMAND",
        "PID",
        "USER",
        "FD",
        "TYPE",
        "DEVICE",
        "SIZE/OFF",
    ]:
        return [], ["lsof human header is malformed"]
    records: list[dict[str, Any]] = []
    errors: list[str] = []
    for line in lines[1:]:
        parts = line.split(None, 7)
        if len(parts) < 7:
            errors.append("lsof human record is malformed")
            continue
        try:
            pid = int(parts[1])
        except ValueError:
            errors.append("lsof human process id is malformed")
            continue
        records.append(
            {
                "pid": pid,
                "fd_raw": parts[3],
                "type": parts[4].lower(),
                "device": parts[5],
                "name": parts[7] if len(parts) == 8 else "",
            }
        )
    return records, errors


def parse_lsof_fd(fd_raw: object) -> tuple[int | None, str | None]:
    if not isinstance(fd_raw, str):
        return None, None
    match = re.fullmatch(r"([0-9]+)([rwu])(?:[A-Za-z]*)?", fd_raw)
    if match is None:
        return None, None
    return int(match.group(1)), match.group(2)


def normalize_kernel_address(value: object) -> str | None:
    if not isinstance(value, str) or re.fullmatch(
        r"0x[0-9a-fA-F]+",
        value,
    ) is None:
        return None
    return f"0x{int(value, 16):x}"


def lsof_peer_address(name: object) -> str | None:
    if not isinstance(name, str):
        return None
    match = re.search(r"(?:^|\s)->(0x[0-9a-fA-F]+)(?=$|\s)", name)
    return normalize_kernel_address(match.group(1)) if match else None


def kernel_address_sha256(address: str) -> str:
    return hashlib.sha256(
        f"darwin-kernel-channel:{address}".encode("ascii")
    ).hexdigest()


def kernel_address_pair_sha256(left: str, right: str) -> str:
    return canonical_sha256(
        {
            "kind": "darwin-kernel-channel-pair",
            "addresses": sorted([left, right]),
        }
    )


def lsof_name_references_endpoint(name: object, endpoint: Path) -> bool:
    if not isinstance(name, str):
        return False
    expected = str(endpoint)
    if (
        not expected
        or not endpoint.is_absolute()
        or any(character in expected for character in ("\0", "\r", "\n"))
    ):
        return False
    for segment in name.split("->"):
        normalized = segment.strip()
        if normalized == expected:
            return True
        if any(
            normalized.startswith(expected + suffix)
            for suffix in (
                " type=",
                " (LISTEN)",
                " (CONNECTED)",
                " (DISCONNECTED)",
            )
        ):
            return True
    return False


def lsof_socket_state(name: str) -> str:
    normalized = name.upper()
    if "DISCONNECTED" in normalized:
        return "disconnected"
    if "LISTEN" in normalized:
        return "listening"
    if "CONNECTED" in normalized or lsof_peer_address(name) is not None:
        return "connected"
    return "open"


def _lsof_completed(args: list[str]) -> subprocess.CompletedProcess[bytes] | None:
    try:
        result = run_read_only(args)
    except (OSError, ValueError, subprocess.SubprocessError):
        return None
    return result if result.returncode == 0 else None


def _record_projection(
    records: Iterable[dict[str, Any]],
) -> list[tuple[int, str, str, str]]:
    return sorted(
        (
            int(record["pid"]),
            str(record["fd_raw"]),
            str(record["type"]).lower(),
            str(record["name"]),
        )
        for record in records
    )


def _human_record_projection(
    records: Iterable[dict[str, Any]],
) -> list[tuple[int, str, str, str, str]]:
    return sorted(
        (
            int(record["pid"]),
            str(record["fd_raw"]),
            str(record["type"]).lower(),
            str(record["device"]),
            str(record["name"]),
        )
        for record in records
    )


def inspect_process_unix_inventory(
    *,
    pid: object,
    role: str,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    observation = {
        "role": role,
        "pid": pid,
        "inspection_complete": False,
        "inspection_error": None,
    }
    if (
        not isinstance(pid, int)
        or pid < 1
        or role not in {"editor", "relay"}
    ):
        observation["inspection_error"] = "invalid unix process identity"
        return observation, []
    field_command = [
        SYSTEM_LSOF,
        "-nP",
        "-a",
        "-p",
        str(pid),
        "-U",
        "-F0pftn",
    ]
    human_command = [
        SYSTEM_LSOF,
        "-nP",
        "-a",
        "-p",
        str(pid),
        "-U",
    ]
    before_result = _lsof_completed(field_command)
    human_result = _lsof_completed(human_command)
    after_result = _lsof_completed(field_command)
    if None in (before_result, human_result, after_result):
        observation["inspection_error"] = "lsof unix inspection failed"
        return observation, []
    before, before_errors = parse_lsof_field_records(before_result.stdout)
    human, human_errors = parse_lsof_human_records(human_result.stdout)
    after, after_errors = parse_lsof_field_records(after_result.stdout)
    errors = [*before_errors, *human_errors, *after_errors]
    if {
        record.get("pid")
        for record in [*before, *human, *after]
        if isinstance(record.get("pid"), int)
    } != {pid}:
        errors.append("lsof unix records did not bind to the exact pid")
    before_projection = _record_projection(before)
    human_projection = _record_projection(human)
    after_projection = _record_projection(after)
    if not (
        before_projection
        and before_projection == human_projection == after_projection
    ):
        errors.append("lsof unix descriptor inventory changed during inspection")
    valid_records: list[dict[str, Any]] = []
    for record in human:
        descriptor, access = parse_lsof_fd(record.get("fd_raw"))
        device = normalize_kernel_address(record.get("device"))
        if (
            record.get("pid") != pid
            or record.get("type") != "unix"
            or descriptor is None
            or access is None
            or device is None
        ):
            errors.append("lsof unix descriptor record is invalid")
            continue
        valid_records.append(
            {
                **record,
                "fd": descriptor,
                "access": access,
                "device": device,
            }
        )
    observation["inspection_complete"] = errors == []
    if errors:
        observation["inspection_error"] = "; ".join(sorted(set(errors)))
    return observation, valid_records


def inspect_process_pipe_inventory(
    *,
    pid: object,
    role: str,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    observation = {
        "role": role,
        "pid": pid,
        "inspection_complete": False,
        "inspection_error": None,
    }
    if (
        not isinstance(pid, int)
        or pid < 1
        or role not in {"client", "relay"}
    ):
        observation["inspection_error"] = "invalid pipe process identity"
        return observation, []
    command = (
        [
            SYSTEM_LSOF,
            "-nP",
            "-a",
            "-p",
            str(pid),
            "-d",
            "0,1",
        ]
        if role == "relay"
        else [
            SYSTEM_LSOF,
            "-nP",
            "-p",
            str(pid),
        ]
    )
    before_result = _lsof_completed(command)
    after_result = _lsof_completed(command)
    if before_result is None or after_result is None:
        observation["inspection_error"] = "lsof pipe inspection failed"
        return observation, []
    before_all, before_errors = parse_lsof_human_records(before_result.stdout)
    after_all, after_errors = parse_lsof_human_records(after_result.stdout)
    errors = [*before_errors, *after_errors]
    if {
        record.get("pid")
        for record in [*before_all, *after_all]
        if isinstance(record.get("pid"), int)
    } != {pid}:
        errors.append("lsof pipe records did not bind to the exact pid")
    before = [
        record for record in before_all if record.get("type") == "pipe"
    ]
    after = [
        record for record in after_all if record.get("type") == "pipe"
    ]
    if (
        not before
        or _human_record_projection(before)
        != _human_record_projection(after)
    ):
        errors.append("lsof pipe descriptor inventory changed during inspection")
    valid_records: list[dict[str, Any]] = []
    for record in before:
        descriptor, access = parse_lsof_fd(record.get("fd_raw"))
        device = normalize_kernel_address(record.get("device"))
        peer = lsof_peer_address(record.get("name"))
        if (
            record.get("pid") != pid
            or descriptor is None
            or access is None
            or device is None
            or peer is None
        ):
            errors.append("lsof pipe descriptor record is invalid")
            continue
        valid_records.append(
            {
                **record,
                "access": access,
                "device": device,
                "fd": descriptor,
                "peer": peer,
            }
        )
    observation["inspection_complete"] = errors == []
    if errors:
        observation["inspection_error"] = "; ".join(sorted(set(errors)))
    return observation, valid_records


def _sanitized_descriptor(
    record: dict[str, Any],
    *,
    channel_sha256: str,
    state: str,
) -> dict[str, Any]:
    return {
        "access": record["access"],
        "channel_address_sha256": channel_sha256,
        "fd": record["fd"],
        "state": state,
        "type": record["type"],
    }


def _reciprocal_pipe_matches(
    relay_record: dict[str, Any],
    client_records: Iterable[dict[str, Any]],
    *,
    client_access: str,
) -> list[dict[str, Any]]:
    return [
        record
        for record in client_records
        if record.get("access") == client_access
        and record.get("device") == relay_record.get("peer")
        and record.get("peer") == relay_record.get("device")
    ]


def inspect_endpoint_fd_graph(
    *,
    client_pid: object,
    editor_pid: object,
    relay_pid: object,
    endpoint: Path,
    accepted_aliases: Iterable[Path] = (),
) -> tuple[dict[str, Any], dict[str, bool]]:
    accepted_paths = tuple(
        sorted(
            {endpoint, *tuple(accepted_aliases)},
            key=str,
        )
    )
    editor_unix, editor_unix_records = inspect_process_unix_inventory(
        pid=editor_pid,
        role="editor",
    )
    relay_unix, relay_unix_records = inspect_process_unix_inventory(
        pid=relay_pid,
        role="relay",
    )
    client_pipe, client_pipe_records = inspect_process_pipe_inventory(
        pid=client_pid,
        role="client",
    )
    relay_pipe, relay_pipe_records = inspect_process_pipe_inventory(
        pid=relay_pid,
        role="relay",
    )
    editor_endpoint_records = [
        record
        for record in editor_unix_records
        if any(
            lsof_name_references_endpoint(record.get("name"), candidate)
            for candidate in accepted_paths
        )
        and lsof_socket_state(str(record.get("name"))) != "disconnected"
    ]
    relay_peer_records = [
        {
            **record,
            "peer": lsof_peer_address(record.get("name")),
        }
        for record in relay_unix_records
        if lsof_peer_address(record.get("name")) is not None
        and lsof_socket_state(str(record.get("name"))) == "connected"
    ]
    unix_matches = [
        (editor_record, relay_record)
        for editor_record in editor_endpoint_records
        for relay_record in relay_peer_records
        if editor_record.get("device") == relay_record.get("peer")
    ]
    relay_stdin = [
        record
        for record in relay_pipe_records
        if record.get("fd") == 0 and record.get("access") == "r"
    ]
    relay_stdout = [
        record
        for record in relay_pipe_records
        if record.get("fd") == 1 and record.get("access") == "w"
    ]
    stdin_client_matches = (
        _reciprocal_pipe_matches(
            relay_stdin[0],
            client_pipe_records,
            client_access="w",
        )
        if len(relay_stdin) == 1
        else []
    )
    stdout_client_matches = (
        _reciprocal_pipe_matches(
            relay_stdout[0],
            client_pipe_records,
            client_access="r",
        )
        if len(relay_stdout) == 1
        else []
    )
    unix_match = unix_matches[0] if len(unix_matches) == 1 else None
    stdin_match = (
        (stdin_client_matches[0], relay_stdin[0])
        if len(stdin_client_matches) == 1 and len(relay_stdin) == 1
        else None
    )
    stdout_match = (
        (stdout_client_matches[0], relay_stdout[0])
        if len(stdout_client_matches) == 1 and len(relay_stdout) == 1
        else None
    )
    unix_channel_hash = (
        kernel_address_sha256(str(unix_match[0]["device"]))
        if unix_match is not None
        else None
    )
    stdin_pair_hash = (
        kernel_address_pair_sha256(
            str(stdin_match[1]["device"]),
            str(stdin_match[1]["peer"]),
        )
        if stdin_match is not None
        else None
    )
    stdout_pair_hash = (
        kernel_address_pair_sha256(
            str(stdout_match[1]["device"]),
            str(stdout_match[1]["peer"]),
        )
        if stdout_match is not None
        else None
    )
    editor_descriptors = [
        _sanitized_descriptor(
            record,
            channel_sha256=kernel_address_sha256(str(record["device"])),
            state=lsof_socket_state(str(record["name"])),
        )
        for record in editor_endpoint_records
    ]
    relay_descriptors = [
        _sanitized_descriptor(
            record,
            channel_sha256=kernel_address_sha256(str(record["peer"])),
            state="connected",
        )
        for record in relay_peer_records
    ]
    client_descriptors: list[dict[str, Any]] = []
    relay_pipe_descriptors: list[dict[str, Any]] = []
    for match, pair_hash in (
        (stdin_match, stdin_pair_hash),
        (stdout_match, stdout_pair_hash),
    ):
        if match is None or pair_hash is None:
            continue
        client_record, relay_record = match
        client_descriptors.append(
            _sanitized_descriptor(
                client_record,
                channel_sha256=pair_hash,
                state="connected",
            )
        )
        relay_pipe_descriptors.append(
            _sanitized_descriptor(
                relay_record,
                channel_sha256=pair_hash,
                state="connected",
            )
        )
    for descriptors in (
        editor_descriptors,
        relay_descriptors,
        client_descriptors,
        relay_pipe_descriptors,
    ):
        descriptors.sort(
            key=lambda item: (
                item["type"],
                item["fd"],
                item["access"],
                item["channel_address_sha256"],
            )
        )
    client_process = {
        **client_pipe,
        "descriptors": client_descriptors,
    }
    editor_process = {
        **editor_unix,
        "descriptors": editor_descriptors,
    }
    relay_errors = sorted(
        error
        for error in (
            relay_unix.get("inspection_error"),
            relay_pipe.get("inspection_error"),
        )
        if isinstance(error, str)
    )
    relay_process = {
        "role": "relay",
        "pid": relay_pid,
        "inspection_complete": (
            relay_unix.get("inspection_complete") is True
            and relay_pipe.get("inspection_complete") is True
        ),
        "inspection_error": "; ".join(relay_errors) if relay_errors else None,
        "descriptors": [*relay_descriptors, *relay_pipe_descriptors],
    }
    relay_process["descriptors"].sort(
        key=lambda item: (
            item["type"],
            item["fd"],
            item["access"],
            item["channel_address_sha256"],
        )
    )
    graph = {
        "schema_version": 1,
        "endpoint": {
            "canonical_path": str(endpoint),
            "canonical_path_sha256": hashlib.sha256(
                str(endpoint).encode("utf-8", errors="surrogateescape")
            ).hexdigest(),
            "accepted_path_sha256s": sorted(
                hashlib.sha256(
                    str(path).encode("utf-8", errors="surrogateescape")
                ).hexdigest()
                for path in accepted_paths
            ),
        },
        "channels": {
            "editor_relay_unix": {
                "address_sha256": unix_channel_hash,
                "editor_fd": (
                    unix_match[0]["fd"] if unix_match is not None else None
                ),
                "relay_fd": (
                    unix_match[1]["fd"] if unix_match is not None else None
                ),
            },
            "client_relay_stdin": {
                "address_pair_sha256": stdin_pair_hash,
                "client_fd": (
                    stdin_match[0]["fd"] if stdin_match is not None else None
                ),
                "relay_fd": 0 if stdin_match is not None else None,
            },
            "client_relay_stdout": {
                "address_pair_sha256": stdout_pair_hash,
                "client_fd": (
                    stdout_match[0]["fd"] if stdout_match is not None else None
                ),
                "relay_fd": 1 if stdout_match is not None else None,
            },
        },
        "processes": [client_process, editor_process, relay_process],
        "residuals": [],
    }
    graph_sha256 = canonical_sha256(graph)
    sanitized_descriptor_keys = {
        "access",
        "channel_address_sha256",
        "fd",
        "state",
        "type",
    }
    client_relay_stdin_bound = stdin_match is not None
    client_relay_stdout_bound = stdout_match is not None
    pipe_pairs_distinct = (
        stdin_match is not None
        and stdout_match is not None
        and stdin_match[0]["fd"] != stdout_match[0]["fd"]
        and stdin_pair_hash != stdout_pair_hash
    )
    checks = {
        "inspection_complete": (
            client_process.get("inspection_complete") is True
            and editor_process.get("inspection_complete") is True
            and relay_process.get("inspection_complete") is True
        ),
        "endpoint_path_exact": (
            graph["endpoint"]["canonical_path"] == str(endpoint)
            and graph["endpoint"]["canonical_path_sha256"]
            == hashlib.sha256(
                str(endpoint).encode("utf-8", errors="surrogateescape")
            ).hexdigest()
            and graph["endpoint"]["accepted_path_sha256s"]
            == sorted(
                hashlib.sha256(
                    str(path).encode("utf-8", errors="surrogateescape")
                ).hexdigest()
                for path in accepted_paths
            )
            and all(
                path.is_absolute()
                and not any(
                    character in str(path)
                    for character in ("\0", "\r", "\n")
                )
                for path in accepted_paths
            )
        ),
        "processes_distinct": (
            isinstance(client_pid, int)
            and isinstance(editor_pid, int)
            and isinstance(relay_pid, int)
            and client_pid > 0
            and editor_pid > 0
            and relay_pid > 0
            and len({client_pid, editor_pid, relay_pid}) == 3
        ),
        "client_pid_exact": (
            client_process.get("role") == "client"
            and client_process.get("pid") == client_pid
        ),
        "editor_pid_exact": (
            editor_process.get("role") == "editor"
            and editor_process.get("pid") == editor_pid
        ),
        "relay_pid_exact": (
            relay_process.get("role") == "relay"
            and relay_process.get("pid") == relay_pid
        ),
        "editor_endpoint_listener_owned_by_exact_pid": (
            bool(editor_endpoint_records)
            and any(
                lsof_socket_state(str(record.get("name")))
                in {"listening", "open"}
                for record in editor_endpoint_records
            )
        ),
        "relay_connected_peer_open_by_exact_pid": bool(relay_peer_records),
        "relay_peer_bound_to_editor_endpoint_fd": unix_match is not None,
        "client_relay_stdin_pipe_bound": client_relay_stdin_bound,
        "client_relay_stdout_pipe_bound": client_relay_stdout_bound,
        "client_relay_pipe_pairs_distinct": pipe_pairs_distinct,
        "client_relay_channel_proven": (
            client_relay_stdin_bound
            and client_relay_stdout_bound
            and pipe_pairs_distinct
        ),
        "channel_address_hashes_present": (
            isinstance(unix_channel_hash, str)
            and isinstance(stdin_pair_hash, str)
            and isinstance(stdout_pair_hash, str)
        ),
        "records_sanitized": all(
            isinstance(descriptor, dict)
            and set(descriptor) == sanitized_descriptor_keys
            for process in graph["processes"]
            for descriptor in process.get("descriptors", [])
        ),
        "digest_recomputed": graph_sha256 == canonical_sha256(graph),
    }
    return {
        "graph": graph,
        "sha256": graph_sha256,
    }, checks


def ps_process_metadata(pid: int) -> dict[str, Any]:
    result = run_read_only(
        [
            SYSTEM_PS,
            "-ww",
            "-p",
            str(pid),
            "-o",
            "pid=",
            "-o",
            "ppid=",
            "-o",
            "uid=",
            "-o",
            "lstart=",
        ]
    )
    if result.returncode != 0:
        raise OSError("ps process inspection failed")
    line = result.stdout.decode("utf-8", errors="replace").strip()
    parts = line.split()
    if len(parts) != 8:
        raise ValueError("ps process output is malformed")
    observed_pid = int(parts[0])
    parent_pid = int(parts[1])
    uid = int(parts[2])
    if observed_pid != pid:
        raise ValueError("ps returned a different PID")
    lstart = " ".join(parts[3:8])
    local_tuple = time.strptime(lstart, "%a %b %d %H:%M:%S %Y")
    started_at = datetime.fromtimestamp(time.mktime(local_tuple), timezone.utc)
    return {
        "pid": pid,
        "parent_pid": parent_pid,
        "uid": uid,
        "started_at": iso_utc(started_at),
    }


def regular_file_facts(path: Path) -> dict[str, Any]:
    ancestry_safe = path_ancestry_is_safe(path)
    try:
        before = path.lstat()
    except OSError:
        return {
            "exists": False,
            "regular": False,
            "symlink": False,
            "sha256": None,
            "ancestry_safe": ancestry_safe,
            "device": None,
            "inode": None,
            "size": None,
            "mtime_ns": None,
        }
    is_symlink = stat.S_ISLNK(before.st_mode)
    is_regular = stat.S_ISREG(before.st_mode)
    digest: str | None = None
    info = before
    if is_regular and not is_symlink and ancestry_safe:
        try:
            descriptor, info = _open_regular_nofollow(path)
            digest_state = hashlib.sha256()
            try:
                while True:
                    chunk = os.read(descriptor, 1024 * 1024)
                    if not chunk:
                        break
                    digest_state.update(chunk)
            finally:
                os.close(descriptor)
            after = path.lstat()
            unchanged = (
                not stat.S_ISLNK(after.st_mode)
                and stat.S_ISREG(after.st_mode)
                and info.st_dev == after.st_dev
                and info.st_ino == after.st_ino
                and info.st_size == after.st_size
                and info.st_mtime_ns == after.st_mtime_ns
            )
            if unchanged:
                digest = digest_state.hexdigest()
            else:
                is_regular = False
        except (OSError, ValueError):
            is_regular = False
    return {
        "exists": True,
        "regular": is_regular,
        "symlink": is_symlink,
        "sha256": digest,
        "ancestry_safe": ancestry_safe,
        "uid": info.st_uid,
        "mode": f"{stat.S_IMODE(info.st_mode):04o}",
        "device": info.st_dev,
        "inode": info.st_ino,
        "size": info.st_size,
        "mtime_ns": info.st_mtime_ns,
    }


def socket_facts(path: Path) -> dict[str, Any]:
    ancestry_before = path_ancestry_is_safe(path)
    try:
        before = path.lstat()
    except OSError:
        return {
            "exists": False,
            "is_socket": False,
            "is_symlink": False,
            "uid": None,
            "mode": None,
        }
    try:
        after = path.lstat()
    except OSError:
        after = None
    stable = (
        ancestry_before
        and path_ancestry_is_safe(path)
        and after is not None
        and before.st_dev == after.st_dev
        and before.st_ino == after.st_ino
        and before.st_mode == after.st_mode
    )
    return {
        "exists": True,
        "is_socket": stable and stat.S_ISSOCK(before.st_mode),
        "is_symlink": stat.S_ISLNK(before.st_mode),
        "uid": before.st_uid,
        "mode": f"{stat.S_IMODE(before.st_mode):04o}",
    }


def inspect_endpoint(reported: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    binding = canonical_physical_path(
        reported,
        allow_macos_tmp_alias=True,
    )
    canonical_value = binding.get("canonical")
    canonical = (
        Path(canonical_value)
        if isinstance(canonical_value, str)
        else reported
    )
    return (
        {
            "path": str(reported),
            "canonical_path": (
                str(canonical)
                if binding.get("safe") is True
                else None
            ),
            **socket_facts(canonical),
        },
        binding,
    )


def process_birth_id(
    *,
    boot_sha256: str,
    pid: int,
    started_at: str,
    executable_sha256: str,
    arguments: list[str],
) -> str:
    return canonical_sha256(
        {
            "arguments_sha256": canonical_sha256(arguments),
            "boot_session_sha256": boot_sha256,
            "executable_sha256": executable_sha256,
            "pid": pid,
            "started_at": iso_utc(parse_datetime(started_at)),
        }
    )


def normalized_process_arguments(
    actual: list[str],
    expected: object,
) -> list[str]:
    """Match manifest convention: relay records contain args without argv[0]."""

    if (
        isinstance(expected, list)
        and expected
        and isinstance(expected[0], str)
        and expected[0].startswith("-")
        and actual
        and not actual[0].startswith("-")
    ):
        return actual[1:]
    return actual


def timestamps_match(left: object, right: object) -> bool:
    if not isinstance(left, str) or not isinstance(right, str):
        return False
    try:
        return abs((parse_datetime(left) - parse_datetime(right)).total_seconds()) <= 1
    except ValueError:
        return False


def collect_process_snapshot(
    pid: int,
    *,
    value_names: Iterable[str],
    presence_only_names: Iterable[str],
) -> dict[str, Any]:
    metadata = ps_process_metadata(pid)
    executable = process_executable_path(pid)
    cwd = process_cwd(pid)
    (
        argv,
        environment,
        forbidden_present,
        environment_names,
        duplicate_environment_names,
    ) = process_args_environment(
        pid,
        value_names=value_names,
        presence_only_names=presence_only_names,
    )
    return {
        "metadata": metadata,
        "executable": executable,
        "cwd": cwd,
        "argv": argv,
        "environment": environment,
        "forbidden_present": forbidden_present,
        "environment_names": environment_names,
        "duplicate_environment_names": duplicate_environment_names,
        "executable_facts": regular_file_facts(executable),
    }


def process_snapshot_stability(
    start: dict[str, Any],
    end: dict[str, Any],
) -> tuple[bool, bool]:
    identity_stable = all(
        start.get(key) == end.get(key)
        for key in (
            "metadata",
            "executable",
            "cwd",
            "argv",
            "environment",
            "forbidden_present",
            "environment_names",
            "duplicate_environment_names",
        )
    )
    start_facts = start.get("executable_facts")
    end_facts = end.get("executable_facts")
    vnode_stable = (
        isinstance(start_facts, dict)
        and isinstance(end_facts, dict)
        and all(
            start_facts.get(key) == end_facts.get(key)
            for key in (
                "device",
                "inode",
                "sha256",
                "size",
                "mtime_ns",
                "uid",
                "mode",
                "regular",
                "symlink",
                "ancestry_safe",
            )
        )
        and start_facts.get("regular") is True
        and start_facts.get("symlink") is False
        and start_facts.get("ancestry_safe") is True
    )
    return identity_stable, vnode_stable


def inspect_process(
    expected: dict[str, Any],
    *,
    executable_key: str,
    pid_key: str,
    boot_sha256: str,
    expected_environment_values: dict[str, str],
    absent_environment_names: Iterable[str],
) -> tuple[dict[str, Any], dict[str, bool]]:
    pid = expected.get(pid_key)
    checks: dict[str, bool] = {name: False for name in PROCESS_CHECK_NAMES}
    observed: dict[str, Any] = {
        "pid": pid,
        "parent_pid": None,
        "uid": None,
        "started_at": None,
        "executable_path": None,
        "executable_sha256": None,
        "executable_regular": False,
        "executable_symlink": False,
        "cwd": None,
        "arguments_sha256": None,
        "process_birth_id_sha256": None,
        "signing_identity": {
            "available": False,
            "strict_verified": False,
            "primary_identity": None,
            "authorities": [],
            "identifier": None,
            "team_identifier": None,
            "cdhash": None,
            "designated_requirement": None,
            "designated_requirement_sha256": None,
            "authorities_sha256": canonical_sha256([]),
            "signature": None,
        },
        "inspection_error": None,
    }
    observed["environment"] = {
        "values": {},
        "names": [],
        "duplicate_names": [],
        "names_sha256": canonical_sha256([]),
        "absent_variable_names_present": [],
    }
    if not isinstance(pid, int) or pid < 1:
        observed["inspection_error"] = "manifest PID is invalid"
        return observed, checks
    try:
        start_snapshot = collect_process_snapshot(
            pid,
            value_names=expected_environment_values,
            presence_only_names=absent_environment_names,
        )
        executable = start_snapshot["executable"]
        signing = codesign_identity(executable)
        end_snapshot = collect_process_snapshot(
            pid,
            value_names=expected_environment_values,
            presence_only_names=absent_environment_names,
        )
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        observed["inspection_error"] = f"{type(exc).__name__}: {exc}"
        return observed, checks

    identity_stable, vnode_stable = process_snapshot_stability(
        start_snapshot,
        end_snapshot,
    )
    metadata = start_snapshot["metadata"]
    cwd = start_snapshot["cwd"]
    argv = start_snapshot["argv"]
    environment = start_snapshot["environment"]
    forbidden_present = start_snapshot["forbidden_present"]
    environment_names = start_snapshot["environment_names"]
    duplicate_environment_names = start_snapshot[
        "duplicate_environment_names"
    ]
    executable_facts = start_snapshot["executable_facts"]
    signing["executable_vnode"] = {
        "device": executable_facts.get("device"),
        "inode": executable_facts.get("inode"),
        "size": executable_facts.get("size"),
        "mtime_ns": executable_facts.get("mtime_ns"),
        "stable_across_inspection": vnode_stable,
    }
    executable_digest = executable_facts.get("sha256")
    normalized_arguments = normalized_process_arguments(
        argv,
        expected.get("arguments"),
    )
    birth_id = (
        process_birth_id(
            boot_sha256=boot_sha256,
            pid=pid,
            started_at=metadata["started_at"],
            executable_sha256=executable_digest,
            arguments=normalized_arguments,
        )
        if isinstance(executable_digest, str)
        else None
    )
    observed.update(
        {
            "parent_pid": metadata["parent_pid"],
            "uid": metadata["uid"],
            "started_at": metadata["started_at"],
            "executable_path": str(executable),
            "executable_sha256": executable_digest,
            "executable_regular": executable_facts.get("regular"),
            "executable_symlink": executable_facts.get("symlink"),
            "cwd": str(cwd),
            "arguments_sha256": canonical_sha256(normalized_arguments),
            "process_birth_id_sha256": birth_id,
            "signing_identity": signing,
            "inspection_error": (
                None
                if identity_stable and vnode_stable
                else "process identity changed during inspection"
            ),
        }
    )
    environment_names_sha256 = canonical_sha256(environment_names)
    observed["environment"] = {
        "values": {key: environment[key] for key in sorted(environment)},
        "names": environment_names,
        "duplicate_names": duplicate_environment_names,
        "names_sha256": environment_names_sha256,
        "absent_variable_names_present": forbidden_present,
    }

    expected_path = expected.get(executable_key)
    expected_path_value = (
        Path(expected_path) if isinstance(expected_path, str) else None
    )
    executable_ancestry_safe = (
        path_ancestry_is_safe(executable)
        and expected_path_value is not None
        and path_ancestry_is_safe(expected_path_value)
    )
    expected_cwd_value = (
        Path(expected["cwd"])
        if isinstance(expected.get("cwd"), str)
        else None
    )
    cwd_ancestry_safe = (
        path_ancestry_is_safe(cwd)
        and expected_cwd_value is not None
        and path_ancestry_is_safe(expected_cwd_value)
    )
    checks.update(
        {
            "process_inspection_complete": identity_stable and vnode_stable,
            "pid_matches": metadata["pid"] == pid,
            "uid_matches": metadata["uid"] == expected.get("principal_uid"),
            "start_time_matches": timestamps_match(
                metadata["started_at"],
                expected.get("started_at"),
            ),
            "identity_stable_across_inspection": identity_stable,
            "executable_is_regular_not_symlink": bool(
                executable_facts.get("regular")
                and not executable_facts.get("symlink")
                and executable_facts.get("ancestry_safe")
            ),
            "executable_path_ancestry_safe": executable_ancestry_safe,
            "executable_vnode_stable": vnode_stable,
            "executable_path_matches": (
                executable_ancestry_safe
                and expected_path_value is not None
                and paths_lexically_equal(executable, expected_path_value)
            ),
            "executable_sha256_matches": (
                executable_digest == expected.get("sha256")
                if executable_key == "path"
                else executable_digest == expected.get("editor_sha256")
            ),
            "cwd_path_ancestry_safe": cwd_ancestry_safe,
            "cwd_matches": (
                cwd_ancestry_safe
                and expected_cwd_value is not None
                and paths_lexically_equal(cwd, expected_cwd_value)
            ),
            "arguments_match": normalized_arguments == expected.get("arguments"),
            "process_birth_id_matches": birth_id
            == expected.get("process_birth_id_sha256"),
            "signing_identity_matches": code_identity_matches(
                signing,
                expected.get("signing_identity"),
            ),
            "environment_names_safe": (
                not duplicate_environment_names
                and set(environment_names).issubset(
                    SAFE_PROCESS_ENVIRONMENT_NAMES
                )
            ),
            "environment_names_hash_matches": (
                environment_names_sha256
                == expected.get("environment_names_sha256")
            ),
            "required_environment_values_match": (
                environment == expected_environment_values
            ),
            "credential_environment_absent": forbidden_present == [],
        }
    )
    return observed, checks


def validate_connection_record(
    record: object,
    expected: dict[str, Any],
) -> dict[str, bool]:
    checks = {name: False for name in CONNECTION_CHECK_NAMES}
    if not isinstance(record, dict):
        return checks
    checks.update(
        {
            "record_is_object": True,
            "keys_exact": set(record) == CONNECTION_RECORD_KEYS,
            "connection_type_matches": record.get("connection_type")
            == expected.get("discovery_connection_type"),
            "connection_path_matches": record.get("connection_path")
            == expected.get("endpoint"),
            "project_path_matches": record.get("project_path")
            == expected.get("project_path"),
            "protocol_version_matches": record.get("protocol_version")
            == expected.get("protocol_version"),
            "editor_pid_matches": record.get("editor_pid")
            == expected.get("editor_pid"),
            "created_date_valid": isinstance(record.get("created_date"), str)
            and _valid_timestamp(record["created_date"]),
        }
    )
    return checks


def _valid_timestamp(value: str) -> bool:
    try:
        parse_datetime(value)
        return True
    except ValueError:
        return False


def load_toml_regular(path: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    data, facts = read_regular_file_bytes(path)
    value = tomllib.loads(data.decode("utf-8"))
    if not isinstance(value, dict):
        raise ValueError(f"{path} does not contain a TOML table")
    return value, facts


def mcp_servers(config: dict[str, Any]) -> dict[str, dict[str, Any]]:
    raw = config.get("mcp_servers", {})
    if raw is None:
        return {}
    if not isinstance(raw, dict):
        raise ValueError("mcp_servers must be a TOML table")
    result: dict[str, dict[str, Any]] = {}
    for name, value in raw.items():
        if not isinstance(name, str) or not isinstance(value, dict):
            raise ValueError("every MCP server must be a named TOML table")
        result[name] = value
    return result


def server_inventory_record(
    *,
    path: Path,
    layer: str,
    config: dict[str, Any],
    sha256: str,
) -> dict[str, Any]:
    servers = mcp_servers(config)
    return {
        "layer": layer,
        "path": str(path),
        "sha256": sha256,
        "top_level_keys": sorted(config),
        "servers": [
            {
                "enabled": value.get("enabled", True) is not False,
                "name": name,
            }
            for name, value in sorted(servers.items())
        ],
    }


def ancestor_config_paths(candidate: Path, runtime_config: Path) -> list[Path]:
    result: list[Path] = []
    cursor = candidate
    while True:
        config = cursor / ".codex" / "config.toml"
        if config != runtime_config and path_lexists(config):
            result.append(config)
        if cursor.parent == cursor:
            break
        cursor = cursor.parent
    return result


def runtime_profile_paths(runtime_config: Path) -> list[Path]:
    codex_home = runtime_config.parent
    if not path_ancestry_is_safe(codex_home):
        return []
    try:
        entries = list(codex_home.iterdir())
    except OSError:
        return []
    return sorted(
        (
            path
            for path in entries
            if path.name.endswith(".config.toml")
            and path != runtime_config
        ),
        key=str,
    )


def inspect_config_inventory(
    *,
    runtime_config: Path,
    candidate: Path,
) -> tuple[list[dict[str, Any]], dict[str, dict[str, Any]], list[str]]:
    records: list[dict[str, Any]] = []
    errors: list[str] = []
    runtime_servers: dict[str, dict[str, Any]] = {}
    paths = [(runtime_config, "runtime")]
    paths.extend(
        (path, "candidate" if path.parent.parent == candidate else "ancestor")
        for path in ancestor_config_paths(candidate, runtime_config)
    )
    paths.extend((path, "profile") for path in runtime_profile_paths(runtime_config))
    paths.extend(
        (path, layer)
        for path, layer in (
            (SYSTEM_CODEX_CONFIG, "system"),
            (SYSTEM_CODEX_REQUIREMENTS, "requirements"),
            (SYSTEM_CODEX_MANAGED_CONFIG, "managed"),
        )
        if path_lexists(path)
    )
    seen_paths: set[str] = set()
    for path, layer in paths:
        path_key = str(path)
        if path_key in seen_paths:
            errors.append(f"duplicate config layer path: {path_key}")
            continue
        seen_paths.add(path_key)
        try:
            require_safe_path_ancestry(path)
            config, facts = load_toml_regular(path)
            servers = mcp_servers(config)
        except (OSError, ValueError, tomllib.TOMLDecodeError) as exc:
            errors.append(f"{layer} config inspection failed: {type(exc).__name__}")
            continue
        records.append(
            server_inventory_record(
                path=path,
                layer=layer,
                config=config,
                sha256=str(facts["sha256"]),
            )
        )
        if layer == "runtime":
            runtime_servers = servers
    records.sort(key=lambda item: (item["layer"], item["path"]))
    return records, runtime_servers, errors


def unexpected_config_features(
    records: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    unexpected: list[dict[str, Any]] = []
    for record in records:
        layer = record.get("layer")
        keys = record.get("top_level_keys")
        if layer == "runtime":
            allowed: set[str] | None = {"approval_policy", "mcp_servers"}
        elif layer == "candidate":
            allowed = {"mcp_servers"}
        else:
            allowed = None
        if (
            allowed is None
            or not isinstance(keys, list)
            or set(keys) != allowed
        ):
            unexpected.append(
                {
                    "layer": layer,
                    "path": record.get("path"),
                    "top_level_keys": keys,
                }
            )
    return unexpected


def active_hook_or_plugin_paths(
    *,
    candidate: Path,
    runtime_config: Path,
) -> list[str]:
    candidates = [
        runtime_config.parent / "hooks.json",
        runtime_config.parent / "plugins",
        SYSTEM_CODEX_HOOKS,
        SYSTEM_CODEX_PLUGINS,
    ]
    cursor = candidate
    while True:
        candidates.extend(
            [
                cursor / ".codex" / "hooks.json",
                cursor / ".codex" / "plugins",
            ]
        )
        if cursor.parent == cursor:
            break
        cursor = cursor.parent
    return sorted(
        {
            str(path)
            for path in candidates
            if path_lexists(path)
        }
    )


def runtime_codex_home_entries(
    runtime_config: Path,
) -> tuple[list[str], list[str]]:
    codex_home = runtime_config.parent
    errors: list[str] = []
    if not path_ancestry_is_safe(codex_home):
        return [], ["runtime CODEX_HOME ancestry is unsafe"]
    try:
        entries = sorted(str(path) for path in codex_home.iterdir())
    except OSError as exc:
        return [], [f"runtime CODEX_HOME inspection failed: {type(exc).__name__}"]
    if entries != [str(runtime_config)]:
        errors.append("runtime CODEX_HOME contains unexpected entries")
    return entries, errors


def macos_managed_preference_keys() -> tuple[list[str], list[str]]:
    if sys.platform != "darwin":
        return [], []
    present: list[str] = []
    errors: list[str] = []
    for key in CODEX_MDM_KEYS:
        result = run_read_only(
            [SYSTEM_DEFAULTS, "read-type", CODEX_MDM_DOMAIN, key]
        )
        if result.returncode == 0:
            present.append(key)
            continue
        output = (result.stdout + result.stderr).decode(
            "utf-8",
            errors="replace",
        ).lower()
        if "does not exist" not in output and "not found" not in output:
            errors.append(f"managed preference inspection failed for {key}")
    return sorted(present), errors


def macos_managed_preference_files() -> tuple[list[str], list[str]]:
    if sys.platform != "darwin":
        return [], []
    root = SYSTEM_MANAGED_PREFERENCES_ROOT
    if not path_lexists(root):
        return [], []
    if not path_ancestry_is_safe(root):
        return [], ["managed preference root ancestry is unsafe"]
    candidates = [root / f"{CODEX_MDM_DOMAIN}.plist"]
    errors: list[str] = []
    try:
        with os.scandir(root) as entries:
            for entry in entries:
                try:
                    if entry.is_symlink():
                        errors.append(
                            "managed preference root contains a symlink entry"
                        )
                        continue
                    if entry.is_dir(follow_symlinks=False):
                        candidates.append(
                            Path(entry.path) / f"{CODEX_MDM_DOMAIN}.plist"
                        )
                except OSError as exc:
                    errors.append(
                        "managed preference directory inspection failed: "
                        f"{type(exc).__name__}"
                    )
    except OSError as exc:
        return [], [
            f"managed preference root inspection failed: {type(exc).__name__}"
        ]
    present = sorted(
        str(path)
        for path in candidates
        if path_lexists(path)
    )
    if any(
        not path_ancestry_is_safe(Path(path))
        for path in present
    ):
        errors.append("managed preference file ancestry is unsafe")
    return present, errors


def active_inventory_servers(records: list[dict[str, Any]]) -> list[dict[str, str]]:
    active: list[dict[str, str]] = []
    for record in records:
        for server in record["servers"]:
            if server["enabled"]:
                active.append(
                    {
                        "layer": record["layer"],
                        "name": server["name"],
                        "path": record["path"],
                    }
                )
    return active


def unexpected_project_servers(records: list[dict[str, Any]]) -> list[dict[str, str]]:
    unexpected: list[dict[str, str]] = []
    for record in records:
        if record["layer"] == "runtime":
            continue
        for server in record["servers"]:
            allowed_disabled_candidate = (
                record["layer"] == "candidate"
                and server["name"] == "unity_mcp"
                and not server["enabled"]
            )
            if not allowed_disabled_candidate:
                unexpected.append(
                    {
                        "layer": record["layer"],
                        "name": server["name"],
                        "path": record["path"],
                    }
                )
    return unexpected


def expected_client_environment(
    boundary: dict[str, Any],
    policy: dict[str, Any],
) -> tuple[dict[str, str], list[str], bool]:
    runtime = boundary.get("runtime_boundary", {})
    bindings = policy.get("environment_bindings", {})
    guard = policy.get("client_environment_guard")
    if not isinstance(guard, dict):
        guard = runtime.get("client_environment_guard")
    guard_present = isinstance(guard, dict)
    values: dict[str, str] = {}
    if isinstance(bindings, dict):
        for name in ("HOME", "TMPDIR", "TMP", "TEMP"):
            value = bindings.get(name)
            if isinstance(value, str):
                values[name] = value
    absent = list(ABSENT_CREDENTIAL_VARIABLES)
    if isinstance(guard, dict):
        for name in (
            "CODEX_HOME",
            "XDG_CONFIG_HOME",
            "XDG_CACHE_HOME",
            "XDG_DATA_HOME",
            "GIT_CONFIG_NOSYSTEM",
            "GIT_CONFIG_GLOBAL",
            "GIT_TERMINAL_PROMPT",
        ):
            value = guard.get(name)
            if isinstance(value, str):
                values[name] = value
        raw_absent = guard.get("absent_variables")
        if isinstance(raw_absent, list) and all(
            isinstance(item, str) for item in raw_absent
        ):
            absent = sorted(raw_absent)
    return values, absent, guard_present


def runtime_environment_paths_are_safe(values: dict[str, str]) -> bool:
    path_names = {
        "HOME",
        "TMPDIR",
        "TMP",
        "TEMP",
        "CODEX_HOME",
        "XDG_CONFIG_HOME",
        "XDG_CACHE_HOME",
        "XDG_DATA_HOME",
        "GIT_CONFIG_GLOBAL",
    }
    expected_names = path_names & set(values)
    if expected_names != path_names:
        return False
    for name in sorted(path_names):
        value = values.get(name)
        if not isinstance(value, str):
            return False
        if not path_ancestry_is_safe(
            Path(value),
            require_leaf=name != "GIT_CONFIG_GLOBAL",
        ):
            return False
    return True


def runtime_server_checks(
    server: object,
    *,
    policy: dict[str, Any],
    relay: dict[str, Any],
) -> dict[str, bool]:
    checks = {name: False for name in RUNTIME_SERVER_CHECK_NAMES}
    if not isinstance(server, dict):
        return checks
    enabled_tools = policy.get("enabled_tools")
    checks.update(
        {
            "runtime_server_is_table": True,
            "keys_exact": set(server)
            == {
                "command",
                "args",
                "enabled",
                "required",
                "enabled_tools",
                "default_tools_approval_mode",
                "env",
            },
            "command_matches": server.get("command") == relay.get("path"),
            "arguments_match": server.get("args") == relay.get("arguments"),
            "enabled_exact": server.get("enabled") is True,
            "required_matches": (
                isinstance(server.get("required"), bool)
                and server.get("required") == policy.get("required")
            ),
            "enabled_tools_match": server.get("enabled_tools") == enabled_tools,
            "disabled_tools_do_not_overlap": not (
                set(server.get("disabled_tools", []))
                & set(enabled_tools if isinstance(enabled_tools, list) else [])
            )
            if isinstance(server.get("disabled_tools", []), list)
            else False,
            "default_tools_approval_mode_matches": (
                server.get("default_tools_approval_mode")
                == policy.get("default_tools_approval_mode")
                == "prompt"
            ),
            "environment_matches": isinstance(server.get("env"), dict),
        }
    )
    return checks


def add_prefixed_checks(
    destination: dict[str, bool],
    prefix: str,
    source: dict[str, bool],
) -> None:
    for name, passed in source.items():
        destination[f"{prefix}.{name}"] = bool(passed)


def verify(boundary: dict[str, Any]) -> dict[str, Any]:
    captured_at = utc_now()
    boot_hash = boot_session_sha256()
    checks: dict[str, bool] = {}
    observed: dict[str, Any] = {}
    route = boundary.get("unity_mcp_route")
    repository = boundary.get("repository")
    if not isinstance(route, dict) or not isinstance(repository, dict):
        raise ValueError("boundary lacks unity_mcp_route or repository")

    client = route.get("client", {})
    relay = route.get("relay", {})
    bridge = route.get("bridge", {})
    policy = route.get("codex_policy", {})
    session = route.get("activation_session", {})
    if not all(
        isinstance(value, dict)
        for value in (client, relay, bridge, policy, session)
    ):
        raise ValueError("route process and policy records must be objects")

    expected_env, absent_env, guard_present = expected_client_environment(
        boundary,
        policy,
    )
    client_observed, client_checks = inspect_process(
        client,
        executable_key="path",
        pid_key="pid",
        boot_sha256=boot_hash,
        expected_environment_values=expected_env,
        absent_environment_names=absent_env,
    )
    relay_observed, relay_checks = inspect_process(
        {**relay, "cwd": client.get("cwd")},
        executable_key="path",
        pid_key="pid",
        boot_sha256=boot_hash,
        expected_environment_values=expected_env,
        absent_environment_names=absent_env,
    )
    editor_expected = dict(bridge)
    editor_expected["sha256"] = bridge.get("editor_sha256")
    editor_observed, editor_checks = inspect_process(
        editor_expected,
        executable_key="editor_path",
        pid_key="editor_pid",
        boot_sha256=boot_hash,
        expected_environment_values=(
            policy.get("environment_bindings")
            if isinstance(policy.get("environment_bindings"), dict)
            else {}
        ),
        absent_environment_names=absent_env,
    )
    add_prefixed_checks(checks, "client", client_checks)
    add_prefixed_checks(checks, "relay", relay_checks)
    add_prefixed_checks(checks, "bridge", editor_checks)
    checks["client.arguments_policy_safe"] = client_arguments_policy_safe(
        client.get("arguments")
    )
    checks["bridge.arguments_policy_safe"] = editor_arguments_policy_safe(
        bridge.get("arguments"),
        bridge.get("project_path"),
    )
    checks["relay.parent_pid_matches_client"] = (
        relay_observed.get("parent_pid") == client.get("pid")
        and relay.get("parent_pid") == client.get("pid")
    )

    client_environment = client_observed.get("environment", {})
    environment_values = (
        client_environment.get("values", {})
        if isinstance(client_environment, dict)
        else {}
    )
    forbidden_present = (
        client_environment.get("absent_variable_names_present", [])
        if isinstance(client_environment, dict)
        else []
    )
    environment_guard = policy.get("client_environment_guard")
    observed_environment_guard: dict[str, Any] = {}
    if isinstance(environment_guard, dict):
        for name in (
            "CODEX_HOME",
            "XDG_CONFIG_HOME",
            "XDG_CACHE_HOME",
            "XDG_DATA_HOME",
            "GIT_CONFIG_NOSYSTEM",
            "GIT_CONFIG_GLOBAL",
            "GIT_TERMINAL_PROMPT",
        ):
            if name in environment_values:
                observed_environment_guard[name] = environment_values[name]
        observed_environment_guard["absent_variables"] = sorted(
            name for name in absent_env if name not in forbidden_present
        )
    environment_hash = canonical_sha256(observed_environment_guard)
    checks["environment.guard_present"] = guard_present
    runtime = boundary.get("runtime_boundary", {})
    runtime_guard = (
        runtime.get("client_environment_guard")
        if isinstance(runtime, dict)
        else None
    )
    expected_home = (
        runtime.get("ephemeral_home_root") if isinstance(runtime, dict) else None
    )
    derived_guard = (
        {
            "CODEX_HOME": f"{expected_home}/.codex",
            "XDG_CONFIG_HOME": f"{expected_home}/.config",
            "XDG_CACHE_HOME": f"{expected_home}/.cache",
            "XDG_DATA_HOME": f"{expected_home}/.local/share",
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_CONFIG_GLOBAL": f"{expected_home}/.gitconfig",
            "GIT_TERMINAL_PROMPT": "0",
            "absent_variables": list(ABSENT_CREDENTIAL_VARIABLES),
        }
        if isinstance(expected_home, str)
        else None
    )
    checks["environment.guard_matches_runtime"] = (
        environment_guard == runtime_guard
    )
    checks["environment.guard_matches_derived_home"] = (
        environment_guard == derived_guard
    )
    checks["environment.runtime_paths_ancestry_safe"] = (
        runtime_environment_paths_are_safe(expected_env)
    )
    checks["environment.values_match"] = environment_values == expected_env
    checks["environment.credential_variables_absent"] = forbidden_present == []
    expected_environment_hashes = [
        value
        for value in (
            policy.get("environment_sha256"),
            client.get("environment_sha256"),
            route.get("handshake", {}).get("environment_sha256")
            if isinstance(route.get("handshake"), dict)
            else None,
            session.get("environment_sha256"),
        )
        if isinstance(value, str)
    ]
    checks["environment.hash_bindings_match"] = (
        len(expected_environment_hashes) == 4
        and all(value == environment_hash for value in expected_environment_hashes)
    )

    relay_package_path = Path(str(relay.get("package_copy_path", "")))
    relay_package_facts = regular_file_facts(relay_package_path)
    observed["relay_package_copy"] = {
        "path": str(relay_package_path),
        **relay_package_facts,
    }
    checks["relay.package_copy_regular_not_symlink"] = bool(
        relay_package_facts.get("regular")
        and not relay_package_facts.get("symlink")
    )
    checks["relay.package_copy_path_ancestry_safe"] = bool(
        relay_package_facts.get("ancestry_safe")
    )
    checks["relay.package_copy_hash_matches"] = (
        relay_package_facts.get("sha256") == relay.get("package_copy_sha256")
        == relay_observed.get("executable_sha256")
    )

    connection_path = Path(str(bridge.get("connection_file", "")))
    connection_facts = regular_file_facts(connection_path)
    connection_record: object = None
    connection_error: str | None = None
    if connection_facts.get("regular") and not connection_facts.get("symlink"):
        try:
            connection_data, connection_facts = read_regular_file_bytes(
                connection_path
            )
            connection_record = json.loads(
                connection_data.decode("utf-8")
            )
            if not isinstance(connection_record, dict):
                raise ValueError("connection record must be an object")
        except (OSError, ValueError, json.JSONDecodeError) as exc:
            connection_error = f"{type(exc).__name__}: connection record invalid"
    add_prefixed_checks(
        checks,
        "connection",
        validate_connection_record(connection_record, bridge),
    )
    checks["connection.file_regular_not_symlink"] = bool(
        connection_facts.get("regular") and not connection_facts.get("symlink")
    )
    checks["connection.file_path_ancestry_safe"] = (
        path_ancestry_is_safe(connection_path)
    )
    checks["connection.file_sha256_matches"] = (
        connection_facts.get("sha256") == bridge.get("connection_file_sha256")
    )
    created_at = (
        connection_record.get("created_date")
        if isinstance(connection_record, dict)
        else None
    )
    checks["connection.created_after_editor_start"] = (
        isinstance(created_at, str)
        and isinstance(bridge.get("started_at"), str)
        and _valid_timestamp(created_at)
        and parse_datetime(created_at) >= parse_datetime(bridge["started_at"])
    )
    checks["connection.created_before_capture"] = (
        isinstance(created_at, str)
        and _valid_timestamp(created_at)
        and parse_datetime(created_at) <= captured_at
    )
    checks["connection.session_record_hash_matches"] = (
        session.get("connection_record_sha256")
        == bridge.get("connection_file_sha256")
        == connection_facts.get("sha256")
    )
    observed["connection"] = {
        "path": str(connection_path),
        "sha256": connection_facts.get("sha256"),
        "error": connection_error,
        "record": {
            key: connection_record.get(key)
            for key in sorted(CONNECTION_RECORD_KEYS)
        }
        if isinstance(connection_record, dict)
        else None,
    }

    endpoint = Path(str(bridge.get("endpoint", "")))
    endpoint_observed, endpoint_binding = inspect_endpoint(endpoint)
    canonical_endpoint_value = endpoint_binding.get("canonical")
    canonical_endpoint = (
        Path(canonical_endpoint_value)
        if isinstance(canonical_endpoint_value, str)
        else endpoint
    )
    endpoint_facts = {
        key: endpoint_observed[key]
        for key in (
            "exists",
            "is_socket",
            "is_symlink",
            "uid",
            "mode",
        )
    }
    observed["endpoint"] = endpoint_observed
    checks["endpoint.exists"] = bool(endpoint_facts["exists"])
    checks["endpoint.is_socket"] = bool(endpoint_facts["is_socket"])
    checks["endpoint.not_symlink"] = not endpoint_facts["is_symlink"]
    checks["endpoint.path_ancestry_safe"] = (
        endpoint_binding.get("safe") is True
    )
    checks["endpoint.reported_to_canonical_binding_safe"] = (
        endpoint_binding.get("safe") is True
        and endpoint_binding.get("reported") == str(endpoint)
        and endpoint_binding.get("canonical") == str(canonical_endpoint)
    )
    checks["endpoint.owner_uid_matches"] = (
        endpoint_facts["uid"] == bridge.get("endpoint_owner_uid")
    )
    checks["endpoint.mode_matches"] = (
        endpoint_facts["mode"] == bridge.get("endpoint_mode")
    )
    checks["endpoint.shared_temp_exception_exact"] = (
        str(canonical_endpoint)
        in boundary.get("runtime_boundary", {}).get(
            "ambient_shared_temp_write_exceptions",
            [],
        )
    )
    fd_graph, fd_graph_checks = inspect_endpoint_fd_graph(
        client_pid=client.get("pid"),
        editor_pid=bridge.get("editor_pid"),
        relay_pid=relay.get("pid"),
        endpoint=canonical_endpoint,
        accepted_aliases=(
            (endpoint,)
            if endpoint_binding.get("safe") is True
            and endpoint != canonical_endpoint
            else ()
        ),
    )
    add_prefixed_checks(
        checks,
        "fd_graph",
        fd_graph_checks,
    )
    checks["fd_graph.session_hash_matches"] = (
        session.get("fd_graph_sha256") == fd_graph.get("sha256")
    )
    checks["session.identity_hash_matches"] = (
        session.get("session_id_sha256")
        == session_identity_sha256(
            route,
            boundary.get("runtime_boundary", {}),
            fd_graph.get("sha256"),
        )
    )
    observed["fd_graph"] = fd_graph

    candidate = Path(str(repository.get("absolute_root", "")))
    codex_home = expected_env.get("CODEX_HOME")
    runtime_config = Path(str(policy.get("runtime_config_path", "")))
    checks["config.runtime_path_under_codex_home"] = (
        isinstance(codex_home, str)
        and runtime_config == Path(codex_home) / "config.toml"
    )
    checks["config.runtime_path_ancestry_safe"] = path_ancestry_is_safe(
        runtime_config
    )
    inventory, runtime_servers, inventory_errors = inspect_config_inventory(
        runtime_config=runtime_config,
        candidate=candidate,
    )
    inventory_hash = canonical_sha256(inventory)
    active_servers = active_inventory_servers(inventory)
    unexpected_servers = unexpected_project_servers(inventory)
    unexpected_features = unexpected_config_features(inventory)
    active_hooks_or_plugins = active_hook_or_plugin_paths(
        candidate=candidate,
        runtime_config=runtime_config,
    )
    runtime_home_entries, runtime_home_errors = runtime_codex_home_entries(
        runtime_config
    )
    mdm_preferences, mdm_errors = macos_managed_preference_keys()
    mdm_files, mdm_file_errors = macos_managed_preference_files()
    expected_server_name = policy.get("server_name")
    runtime_server = runtime_servers.get(expected_server_name)
    checks["config.inventory_inspection_complete"] = (
        inventory_errors == []
        and runtime_home_errors == []
        and mdm_errors == []
        and mdm_file_errors == []
    )
    checks["config.all_inventory_paths_ancestry_safe"] = (
        bool(inventory)
        and all(
            isinstance(record.get("path"), str)
            and path_ancestry_is_safe(Path(record["path"]))
            for record in inventory
        )
    )
    checks["config.exactly_one_active_server"] = active_servers == [
        {
            "layer": "runtime",
            "name": expected_server_name,
            "path": str(runtime_config),
        }
    ]
    checks["config.no_unexpected_project_or_ancestor_servers"] = (
        unexpected_servers == []
    )
    checks["config.no_unexpected_config_layers_or_features"] = (
        unexpected_features == []
    )
    checks["config.no_hooks_or_plugins"] = active_hooks_or_plugins == []
    checks["config.no_requirements_or_managed_layers"] = not any(
        path_lexists(path)
        for path in (
            SYSTEM_CODEX_REQUIREMENTS,
            SYSTEM_CODEX_MANAGED_CONFIG,
        )
    )
    checks["config.no_profile_layers"] = runtime_profile_paths(
        runtime_config
    ) == []
    checks["config.no_mdm_preferences"] = (
        mdm_preferences == []
        and mdm_files == []
        and mdm_errors == []
        and mdm_file_errors == []
    )
    checks["config.runtime_codex_home_entries_exact"] = (
        runtime_home_errors == []
        and runtime_home_entries == [str(runtime_config)]
    )
    candidate_records = [
        record for record in inventory if record["layer"] == "candidate"
    ]
    checks["config.protected_candidate_config_present"] = (
        len(candidate_records) == 1
    )
    checks["config.protected_candidate_server_disabled_exact"] = (
        len(candidate_records) == 1
        and candidate_records[0]["servers"]
        == [{"enabled": False, "name": "unity_mcp"}]
        and policy.get("project_config_disabled") is True
    )
    checks["config.runtime_server_set_exact"] = set(runtime_servers) == {
        expected_server_name
    }
    add_prefixed_checks(
        checks,
        "config.server",
        runtime_server_checks(
            runtime_server,
            policy=policy,
            relay=relay,
        ),
    )
    if isinstance(runtime_server, dict):
        server_env = runtime_server.get("env")
        checks["config.server.environment_matches"] = server_env == expected_env

    runtime_config_facts = regular_file_facts(runtime_config)
    runtime_config_hash = runtime_config_facts.get("sha256")
    checks["config.runtime_regular_not_symlink"] = bool(
        runtime_config_facts.get("regular")
        and not runtime_config_facts.get("symlink")
    )
    checks["config.runtime_hash_matches_session"] = (
        runtime_config_hash == session.get("runtime_config_sha256")
    )
    handshake = route.get("handshake", {})
    checks["config.runtime_hash_matches_handshake"] = (
        isinstance(handshake, dict)
        and runtime_config_hash == handshake.get("runtime_config_sha256")
    )
    evidence = policy.get("evidence", {})
    checks["config.runtime_hash_matches_policy_evidence"] = (
        isinstance(evidence, dict)
        and runtime_config_hash == evidence.get("sha256")
    )
    expected_inventory_hashes = [
        value
        for value in (
            policy.get("effective_server_inventory_sha256"),
            client.get("server_inventory_sha256"),
            handshake.get("server_inventory_sha256")
            if isinstance(handshake, dict)
            else None,
            session.get("server_inventory_sha256"),
        )
        if isinstance(value, str)
    ]
    checks["config.inventory_hash_bindings_match"] = (
        len(expected_inventory_hashes) == 4
        and all(value == inventory_hash for value in expected_inventory_hashes)
    )
    protected_config_hash = (
        candidate_records[0]["sha256"] if len(candidate_records) == 1 else None
    )

    enabled_tools = (
        runtime_server.get("enabled_tools")
        if isinstance(runtime_server, dict)
        else None
    )
    policy_enabled_tools = policy.get("enabled_tools")
    allowlist_hash = (
        canonical_sha256(enabled_tools) if isinstance(enabled_tools, list) else None
    )
    checks["allowlist.is_nonempty_list"] = isinstance(enabled_tools, list) and bool(
        enabled_tools
    )
    checks["allowlist.client_visible_exact"] = (
        enabled_tools == policy_enabled_tools == policy.get("client_visible_tools")
    )
    checks["allowlist.policy_hash_matches"] = (
        allowlist_hash == policy.get("enabled_tools_sha256")
    )
    checks["allowlist.handshake_hash_matches"] = (
        isinstance(handshake, dict)
        and allowlist_hash == handshake.get("enabled_tools_sha256")
    )
    checks["allowlist.session_hash_matches"] = (
        allowlist_hash == session.get("enabled_tools_sha256")
    )

    checks["boot_session.matches_manifest"] = (
        boot_hash == boundary.get("runtime_boundary", {}).get("boot_session_sha256")
    )
    checks["route.is_direct_external"] = route.get("route") == "UNITY-MCP-EXTERNAL"
    checks["boundary.packet_is_wp0001"] = boundary.get("packet_id") == "WP-0001"

    observed.update(
        {
            "client": client_observed,
            "relay": relay_observed,
            "editor": editor_observed,
            "environment": {
                "sha256": environment_hash,
                "value_names": sorted(expected_env),
                "values": {key: environment_values.get(key) for key in sorted(expected_env)},
                "absent_variables": sorted(absent_env),
                "absent_variable_names_present": forbidden_present,
            },
            "config": {
                "runtime_path": str(runtime_config),
                "runtime_sha256": runtime_config_hash,
                "effective_server_inventory": inventory,
                "effective_server_inventory_sha256": inventory_hash,
                "active_servers": active_servers,
                "unexpected_project_or_ancestor_servers": unexpected_servers,
                "unexpected_config_layers_or_features": unexpected_features,
                "active_hook_or_plugin_paths": active_hooks_or_plugins,
                "inspection_errors": inventory_errors,
            },
            "allowlist": {
                "tools": enabled_tools if isinstance(enabled_tools, list) else None,
                "sha256": allowlist_hash,
            },
        }
    )

    if set(checks) != EXPECTED_CHECK_NAMES:
        missing = sorted(EXPECTED_CHECK_NAMES - set(checks))
        unexpected = sorted(set(checks) - EXPECTED_CHECK_NAMES)
        raise ValueError(
            "live verifier check inventory drifted: "
            f"missing={missing}; unexpected={unexpected}"
        )
    failed_checks = sorted(name for name, passed in checks.items() if not passed)
    observed_exact = {
        "boot_session_sha256": boot_hash,
        "route_contract_sha256": canonical_sha256(route_contract(boundary)),
        "client": client_observed,
        "relay": {
            **relay_observed,
            "package_copy": {
                "path": str(relay_package_path),
                **relay_package_facts,
            },
        },
        "bridge": editor_observed,
        "runtime_config_sha256": runtime_config_hash,
        "protected_config_sha256": protected_config_hash,
        "environment_sha256": environment_hash,
        "enabled_tools_sha256": allowlist_hash,
        "effective_server_inventory": inventory,
        "effective_server_inventory_sha256": inventory_hash,
        "connection_file": observed["connection"],
        "endpoint": observed["endpoint"],
        "fd_graph": observed["fd_graph"],
    }
    return {
        "schema_version": CAPTURE_SCHEMA_VERSION,
        "validator_version": VALIDATOR_VERSION,
        "packet_id": boundary.get("packet_id"),
        "captured_at": iso_utc(captured_at),
        "result": "PASS" if not failed_checks else "FAIL",
        "checks": {name: checks[name] for name in sorted(checks)},
        "observed": observed_exact,
    }


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Read an existing WP-0001 A1 boundary and live OS metadata; never "
            "start Unity, Hub, relay, Codex, or MCP."
        )
    )
    parser.add_argument(
        "--boundary",
        required=True,
        type=Path,
        help="Path to the prepared A1 boundary manifest JSON.",
    )
    parser.add_argument(
        "--output",
        required=True,
        type=Path,
        help="Path for the deterministic JSON capture.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if args.output.parts[-2:] != ("commands", "mcp-route-live.json"):
        print(
            "ERROR: --output must end with commands/mcp-route-live.json",
            file=sys.stderr,
        )
        return 2
    try:
        boundary = load_json_object(args.boundary)
        capture = verify(boundary)
    except (
        OSError,
        ValueError,
        json.JSONDecodeError,
        subprocess.SubprocessError,
    ) as exc:
        print(f"ERROR: {type(exc).__name__}: {exc}", file=sys.stderr)
        return 2
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(
            capture,
            ensure_ascii=False,
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    print(
        f"{capture['result']}: "
        f"{sum(not passed for passed in capture['checks'].values())} "
        "failed checks; "
        f"capture={args.output}"
    )
    return 0 if capture["result"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
