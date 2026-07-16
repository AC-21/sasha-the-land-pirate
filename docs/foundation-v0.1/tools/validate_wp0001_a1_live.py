#!/usr/bin/env python3
"""Creator-side live verification for a prepared WP-0001 A1 boundary.

This tool never starts Unity, Hub, the relay, Codex, or MCP. It validates only
the already-prepared filesystem, Git, principal, and environment boundary.
"""

from __future__ import annotations

import argparse
import errno
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


VALIDATOR_VERSION = "wp0001-a1-live-v1"
SYSTEM_GIT = "/usr/bin/git"
SYSTEM_SYSCTL = "/usr/sbin/sysctl"
SAFE_SUBPROCESS_ENV = {
    "HOME": "/var/empty",
    "XDG_CONFIG_HOME": "/var/empty",
    "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
    "LANG": "C",
    "LC_ALL": "C",
    "GIT_CONFIG_NOSYSTEM": "1",
    "GIT_CONFIG_GLOBAL": "/dev/null",
    "GIT_OPTIONAL_LOCKS": "0",
    "GIT_NO_REPLACE_OBJECTS": "1",
    "GIT_TERMINAL_PROMPT": "0",
    "GIT_PAGER": "",
    "PAGER": "",
}
SAFE_GIT_CONFIG_ARGS = (
    "-c",
    "core.fsmonitor=false",
    "-c",
    "core.hooksPath=/dev/null",
    "-c",
    "maintenance.auto=false",
    "-c",
    "gc.auto=0",
)
APPROVED_SYSTEM_TOOLS = {SYSTEM_GIT, SYSTEM_SYSCTL}
APPROVED_GIT_SUBCOMMANDS = {
    "rev-parse",
    "remote",
    "symbolic-ref",
    "ls-files",
    "ls-tree",
}
FORBIDDEN_CREDENTIAL_ENV_KEYS = {
    "GH_TOKEN",
    "GITHUB_TOKEN",
    "GIT_ASKPASS",
    "SSH_AUTH_SOCK",
    "VERCEL_TOKEN",
    "AWS_ACCESS_KEY_ID",
    "AWS_SECRET_ACCESS_KEY",
    "AWS_SESSION_TOKEN",
}
CLIENT_ENVIRONMENT_KEYS = (
    "CODEX_HOME",
    "XDG_CONFIG_HOME",
    "XDG_CACHE_HOME",
    "XDG_DATA_HOME",
    "GIT_CONFIG_NOSYSTEM",
    "GIT_CONFIG_GLOBAL",
    "GIT_TERMINAL_PROMPT",
)
SAFE_CORE_GIT_CONFIG = {
    "repositoryformatversion": {"0"},
    "filemode": {"true", "false"},
    "bare": {"false"},
    "logallrefupdates": {"true"},
    "ignorecase": {"true", "false"},
    "precomposeunicode": {"true", "false"},
    "symlinks": {"true", "false"},
}
WRITE_DENIAL_ERRNOS = {
    errno.EACCES,
    errno.EPERM,
    errno.EROFS,
}


def run(
    args: list[str],
    *,
    cwd: Path | None = None,
    check: bool = True,
) -> subprocess.CompletedProcess[str]:
    if (
        not args
        or args[0] not in APPROVED_SYSTEM_TOOLS
        or not trusted_system_executable(Path(args[0]))
    ):
        raise ValueError("unapproved or mutable system tool")
    return subprocess.run(
        args,
        cwd=cwd,
        check=check,
        capture_output=True,
        text=True,
        stdin=subprocess.DEVNULL,
        env=SAFE_SUBPROCESS_ENV,
        timeout=15,
    )


def trusted_system_executable(path: Path) -> bool:
    try:
        metadata = path.lstat()
    except OSError:
        return False
    return (
        stat.S_ISREG(metadata.st_mode)
        and not stat.S_ISLNK(metadata.st_mode)
        and metadata.st_uid == 0
        and stat.S_IMODE(metadata.st_mode) & 0o022 == 0
    )


def git(candidate: Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    if not args or args[0] not in APPROVED_GIT_SUBCOMMANDS:
        raise ValueError("unapproved Git subcommand")
    return run(
        [
            SYSTEM_GIT,
            "--no-replace-objects",
            *SAFE_GIT_CONFIG_ARGS,
            "--no-optional-locks",
            "-C",
            str(candidate),
            *args,
        ],
        check=check,
    )


def parse_index_listing(raw: str) -> tuple[dict[str, tuple[str, str]], list[str]]:
    entries: dict[str, tuple[str, str]] = {}
    errors: list[str] = []
    for record in raw.split("\0"):
        if not record:
            continue
        try:
            metadata, path = record.split("\t", 1)
            mode, object_id, stage = metadata.split(" ", 2)
        except ValueError:
            errors.append("malformed-index-record")
            continue
        if (
            stage != "0"
            or mode not in {"100644", "100755"}
            or not path
            or path.startswith("/")
            or ".." in Path(path).parts
            or path in entries
            or len(object_id) not in {40, 64}
            or re.fullmatch(r"[0-9a-f]+", object_id) is None
        ):
            errors.append(f"unsafe-index-record:{path}")
            continue
        entries[path] = (mode, object_id)
    return entries, errors


def parse_tree_listing(raw: str) -> tuple[dict[str, tuple[str, str]], list[str]]:
    entries: dict[str, tuple[str, str]] = {}
    errors: list[str] = []
    for record in raw.split("\0"):
        if not record:
            continue
        try:
            metadata, path = record.split("\t", 1)
            mode, object_type, object_id = metadata.split(" ", 2)
        except ValueError:
            errors.append("malformed-tree-record")
            continue
        if (
            object_type != "blob"
            or mode not in {"100644", "100755"}
            or not path
            or path.startswith("/")
            or ".." in Path(path).parts
            or path in entries
            or len(object_id) not in {40, 64}
            or re.fullmatch(r"[0-9a-f]+", object_id) is None
        ):
            errors.append(f"unsafe-tree-record:{path}")
            continue
        entries[path] = (mode, object_id)
    return entries, errors


def git_blob_oid(path: Path, algorithm: str) -> tuple[str | None, str | None]:
    try:
        before = path.lstat()
        if not stat.S_ISREG(before.st_mode) or stat.S_ISLNK(before.st_mode):
            return None, "not-regular"
        flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(
            os,
            "O_NOFOLLOW",
            0,
        )
        descriptor = os.open(path, flags)
        after = os.fstat(descriptor)
        if (
            not stat.S_ISREG(after.st_mode)
            or before.st_dev != after.st_dev
            or before.st_ino != after.st_ino
        ):
            os.close(descriptor)
            return None, "changed-during-open"
        digest = hashlib.new(algorithm)
        digest.update(f"blob {after.st_size}\0".encode("ascii"))
        try:
            while True:
                chunk = os.read(descriptor, 1024 * 1024)
                if not chunk:
                    break
                digest.update(chunk)
        finally:
            os.close(descriptor)
        final = path.lstat()
        if (
            final.st_dev != after.st_dev
            or final.st_ino != after.st_ino
            or final.st_size != after.st_size
            or final.st_mtime_ns != after.st_mtime_ns
        ):
            return None, "changed-during-read"
        return digest.hexdigest(), None
    except (OSError, ValueError):
        return None, "read-failed"


def raw_worktree_status(
    candidate: Path,
    index_entries: dict[str, tuple[str, str]],
    head_entries: dict[str, tuple[str, str]],
    parse_errors: list[str],
    allowed_scratch_prefixes: list[str] | None = None,
) -> list[str]:
    """Compare raw worktree bytes/types to index and HEAD without Git filters."""
    findings = list(parse_errors)
    scratch_prefixes = tuple(
        path.rstrip("/")
        for path in (allowed_scratch_prefixes or [])
        if isinstance(path, str) and path
    )
    for tracked_path in index_entries:
        if any(
            tracked_path == prefix or tracked_path.startswith(f"{prefix}/")
            for prefix in scratch_prefixes
        ):
            findings.append(f"tracked-scratch:{tracked_path}")
    if index_entries != head_entries:
        findings.append("index-differs-from-head")
    actual_paths: dict[str, os.stat_result] = {}
    for directory, dirnames, filenames in os.walk(candidate, followlinks=False):
        current = Path(directory)
        if current == candidate:
            dirnames[:] = [name for name in dirnames if name != ".git"]
        retained: list[str] = []
        for name in dirnames:
            path = current / name
            try:
                metadata = path.lstat()
            except OSError:
                findings.append(
                    f"unreadable:{path.relative_to(candidate).as_posix()}"
                )
                continue
            if stat.S_ISLNK(metadata.st_mode):
                findings.append(
                    f"symlink:{path.relative_to(candidate).as_posix()}"
                )
            else:
                retained.append(name)
        dirnames[:] = retained
        for name in filenames:
            path = current / name
            relative = path.relative_to(candidate).as_posix()
            try:
                actual_paths[relative] = path.lstat()
            except OSError:
                findings.append(f"unreadable:{relative}")
    for path in sorted(set(actual_paths) - set(index_entries)):
        if not any(
            path == prefix or path.startswith(f"{prefix}/")
            for prefix in scratch_prefixes
        ):
            findings.append(f"untracked:{path}")
    for path in sorted(set(index_entries) - set(actual_paths)):
        findings.append(f"missing:{path}")
    for relative in sorted(set(index_entries) & set(actual_paths)):
        mode, expected_oid = index_entries[relative]
        metadata = actual_paths[relative]
        if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
            findings.append(f"type:{relative}")
            continue
        executable = bool(stat.S_IMODE(metadata.st_mode) & 0o111)
        if executable != (mode == "100755"):
            findings.append(f"mode:{relative}")
        algorithm = "sha1" if len(expected_oid) == 40 else "sha256"
        actual_oid, error = git_blob_oid(candidate / relative, algorithm)
        if error is not None or actual_oid != expected_oid:
            findings.append(f"content:{relative}")
    return sorted(set(findings))


def path_components_symlink_free(
    path: Path,
    *,
    allow_missing_leaf: bool = False,
) -> bool:
    """Reject a symlink at any existing component of an absolute path."""
    if not path.is_absolute():
        return False
    current = Path(path.anchor)
    missing_seen = False
    parts = path.parts[1:] if path.anchor else path.parts
    for index, part in enumerate(parts):
        current /= part
        try:
            metadata = current.lstat()
        except FileNotFoundError:
            if allow_missing_leaf:
                missing_seen = True
                continue
            return False
        except OSError:
            return False
        if missing_seen:
            # An existing child below a missing ancestor indicates a race.
            return False
        if stat.S_ISLNK(metadata.st_mode):
            return False
        if index < len(parts) - 1 and not stat.S_ISDIR(metadata.st_mode):
            return False
    return True


def canonical_symlink_free_path(
    path: Path,
    *,
    allow_missing_leaf: bool = False,
) -> bool:
    return (
        path.is_absolute()
        and str(path) == str(resolved(path))
        and path_components_symlink_free(
            path,
            allow_missing_leaf=allow_missing_leaf,
        )
    )


def path_is_within(path: Path, root: Path) -> bool:
    path = resolved(path)
    root = resolved(root)
    return path == root or root in path.parents


def is_directory_no_symlink(path: Path) -> bool:
    try:
        metadata = path.lstat()
    except OSError:
        return False
    return stat.S_ISDIR(metadata.st_mode) and not stat.S_ISLNK(metadata.st_mode)


def passive_git_config(path: Path) -> bool:
    """Accept only the inert core keys produced by a plain independent repo."""
    try:
        metadata = path.lstat()
        text = path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError):
        return False
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return False
    section: str | None = None
    seen_keys: set[str] = set()
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line or line.startswith(("#", ";")):
            continue
        if line.startswith("["):
            if re.fullmatch(
                r"\[\s*core\s*\](?:\s*[#;].*)?",
                line,
                flags=re.IGNORECASE,
            ) is None:
                return False
            section = "core"
            continue
        if section != "core":
            return False
        match = re.fullmatch(
            r"([A-Za-z][A-Za-z0-9-]*)\s*=\s*([^#;]*?)\s*(?:[#;].*)?",
            line,
        )
        if match is None:
            return False
        key = match.group(1).casefold()
        value = match.group(2).strip().casefold()
        if (
            key in seen_keys
            or key not in SAFE_CORE_GIT_CONFIG
            or value not in SAFE_CORE_GIT_CONFIG[key]
        ):
            return False
        seen_keys.add(key)
    return section == "core" and {
        "repositoryformatversion",
        "bare",
    }.issubset(seen_keys)


def hooks_are_passive(hooks_directory: Path) -> bool:
    try:
        metadata = hooks_directory.lstat()
    except FileNotFoundError:
        return True
    except OSError:
        return False
    if not stat.S_ISDIR(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return False
    try:
        entries = list(hooks_directory.iterdir())
    except OSError:
        return False
    for entry in entries:
        try:
            entry_metadata = entry.lstat()
        except OSError:
            return False
        if (
            stat.S_ISLNK(entry_metadata.st_mode)
            or not stat.S_ISREG(entry_metadata.st_mode)
            or not entry.name.endswith(".sample")
        ):
            return False
    return True


def packed_refs_are_passive(path: Path) -> bool:
    try:
        metadata = path.lstat()
    except FileNotFoundError:
        return True
    except OSError:
        return False
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return False
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeDecodeError):
        return False
    return not any(
        line.strip()
        and not line.lstrip().startswith(("#", "^"))
        and " refs/replace/" in line
        for line in lines
    )


def git_metadata_is_passive(candidate: Path) -> bool:
    git_directory = candidate / ".git"
    forbidden_paths = [
        git_directory / "config.worktree",
        git_directory / "commondir",
        git_directory / "gitdir",
        git_directory / "info" / "grafts",
        git_directory / "info" / "sparse-checkout",
        git_directory / "refs" / "replace",
        git_directory / "shallow",
        git_directory / "worktrees",
        git_directory / "modules",
        git_directory / "objects" / "info" / "alternates",
        git_directory / "objects" / "info" / "http-alternates",
        git_directory / "fsmonitor--daemon.ipc",
        git_directory / "maintenance",
    ]
    if any(os.path.lexists(path) for path in forbidden_paths):
        return False
    if any(git_directory.glob("sharedindex.*")):
        return False
    for relative in ("info/exclude", "info/attributes"):
        path = git_directory / relative
        if not path.is_file():
            continue
        try:
            active_lines = [
                line.strip()
                for line in path.read_text(encoding="utf-8").splitlines()
                if line.strip() and not line.lstrip().startswith("#")
            ]
        except (OSError, UnicodeDecodeError):
            return False
        if active_lines:
            return False
    return (
        passive_git_config(git_directory / "config")
        and hooks_are_passive(git_directory / "hooks")
        and packed_refs_are_passive(git_directory / "packed-refs")
    )


def resolved(path: Path) -> Path:
    return path.expanduser().resolve(strict=False)


def paths_overlap(left: Path, right: Path) -> bool:
    left = resolved(left)
    right = resolved(right)
    return left == right or left in right.parents or right in left.parents


def boot_session_sha256() -> str:
    raw = run([SYSTEM_SYSCTL, "-n", "kern.boottime"]).stdout.strip()
    if not raw or re.search(r"\bsec\s*=\s*[0-9]+\b", raw) is None:
        raise ValueError("boot-session inspection failed")
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def sha256_file(path: Path) -> str:
    before = path.lstat()
    if not stat.S_ISREG(before.st_mode) or stat.S_ISLNK(before.st_mode):
        raise OSError("hash target is not a regular non-symlink file")
    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(
        os,
        "O_NOFOLLOW",
        0,
    )
    descriptor = os.open(path, flags)
    try:
        opened = os.fstat(descriptor)
        if (
            not stat.S_ISREG(opened.st_mode)
            or before.st_dev != opened.st_dev
            or before.st_ino != opened.st_ino
        ):
            raise OSError("hash target changed during open")
        digest = hashlib.sha256()
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    finally:
        os.close(descriptor)
    final = path.lstat()
    if (
        final.st_dev != opened.st_dev
        or final.st_ino != opened.st_ino
        or final.st_size != opened.st_size
        or final.st_mtime_ns != opened.st_mtime_ns
    ):
        raise OSError("hash target changed during read")
    return digest.hexdigest()


def can_create_probe(directory: Path) -> bool:
    probe = directory / f".wp0001-write-probe-{os.getpid()}"
    try:
        descriptor = os.open(
            probe,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL,
            0o600,
        )
    except OSError:
        return False
    else:
        os.close(descriptor)
        try:
            probe.unlink()
        except OSError:
            pass
        return True


def write_open_is_denied(path: Path) -> bool:
    """Test write-open authority without truncating or modifying the file."""
    try:
        before = path.lstat()
    except OSError:
        return False
    if not stat.S_ISREG(before.st_mode) or stat.S_ISLNK(before.st_mode):
        return False
    flags = os.O_WRONLY | getattr(os, "O_CLOEXEC", 0) | getattr(
        os,
        "O_NOFOLLOW",
        0,
    )
    try:
        descriptor = os.open(path, flags)
    except OSError as exc:
        return exc.errno in WRITE_DENIAL_ERRNOS
    else:
        os.close(descriptor)
        return False


def effective_write_access(path: Path) -> bool:
    try:
        return os.access(path, os.W_OK, effective_ids=True)
    except (NotImplementedError, TypeError):
        # Unknown access semantics cannot certify a protected path read-only.
        return True


def protected_path_is_read_only(path: Path) -> tuple[bool, int, int]:
    """Non-mutating, recursive write-denial evidence for a file or directory."""
    try:
        root_metadata = path.lstat()
    except OSError:
        return False, 0, 0
    if stat.S_ISLNK(root_metadata.st_mode):
        return False, 0, 0
    if stat.S_ISREG(root_metadata.st_mode):
        return write_open_is_denied(path), 1, 0
    if not stat.S_ISDIR(root_metadata.st_mode) or effective_write_access(path):
        return False, 0, 1 if stat.S_ISDIR(root_metadata.st_mode) else 0
    try:
        traversable = os.access(
            path,
            os.X_OK,
            effective_ids=True,
        )
    except (NotImplementedError, TypeError):
        traversable = True
    if not traversable:
        return True, 0, 1
    file_count = 0
    directory_count = 1
    walk_errors: list[OSError] = []
    for directory, dirnames, filenames in os.walk(
        path,
        followlinks=False,
        onerror=walk_errors.append,
    ):
        current = Path(directory)
        retained: list[str] = []
        for name in dirnames:
            child = current / name
            try:
                metadata = child.lstat()
            except OSError:
                return False, file_count, directory_count
            if (
                stat.S_ISLNK(metadata.st_mode)
                or not stat.S_ISDIR(metadata.st_mode)
                or effective_write_access(child)
            ):
                return False, file_count, directory_count
            directory_count += 1
            retained.append(name)
        dirnames[:] = retained
        for name in filenames:
            child = current / name
            try:
                metadata = child.lstat()
            except OSError:
                return False, file_count, directory_count
            if (
                not stat.S_ISREG(metadata.st_mode)
                or stat.S_ISLNK(metadata.st_mode)
                or not write_open_is_denied(child)
            ):
                return False, file_count, directory_count
            file_count += 1
    return not walk_errors, file_count, directory_count


def declared_path_is_writable(root: Path, value: str) -> tuple[bool, str]:
    path = resolve_declared_relative_path(root, value)
    if path is None:
        return False, ""
    try:
        metadata = path.lstat()
    except OSError:
        return False, str(path)
    if stat.S_ISLNK(metadata.st_mode):
        return False, str(path)
    if value.endswith("/"):
        return (
            stat.S_ISDIR(metadata.st_mode) and can_create_probe(path),
            str(path),
        )
    if not stat.S_ISREG(metadata.st_mode):
        return False, str(path)
    flags = os.O_WRONLY | getattr(os, "O_CLOEXEC", 0) | getattr(
        os,
        "O_NOFOLLOW",
        0,
    )
    try:
        descriptor = os.open(path, flags)
    except OSError:
        return False, str(path)
    else:
        os.close(descriptor)
        return True, str(path)


def candidate_write_scope_is_exact(
    root: Path,
    allowed_values: list[str],
) -> tuple[bool, int, int]:
    allowed_roots: list[Path] = []
    for value in allowed_values:
        path = resolve_declared_relative_path(root, value)
        if path is None:
            return False, 0, 0
        allowed_roots.append(path)

    def write_allowed(path: Path) -> bool:
        return any(path_is_within(path, allowed) for allowed in allowed_roots)

    file_count = 0
    directory_count = 0
    walk_errors: list[OSError] = []
    for directory, dirnames, filenames in os.walk(
        root,
        followlinks=False,
        onerror=walk_errors.append,
    ):
        current = Path(directory)
        try:
            current_metadata = current.lstat()
        except OSError:
            return False, file_count, directory_count
        if (
            stat.S_ISLNK(current_metadata.st_mode)
            or not stat.S_ISDIR(current_metadata.st_mode)
        ):
            return False, file_count, directory_count
        if not write_allowed(current) and can_create_probe(current):
            return False, file_count, directory_count
        directory_count += 1
        retained: list[str] = []
        for name in dirnames:
            child = current / name
            try:
                metadata = child.lstat()
            except OSError:
                return False, file_count, directory_count
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISDIR(
                metadata.st_mode
            ):
                return False, file_count, directory_count
            retained.append(name)
        dirnames[:] = retained
        for name in filenames:
            child = current / name
            try:
                metadata = child.lstat()
            except OSError:
                return False, file_count, directory_count
            if stat.S_ISLNK(metadata.st_mode) or not stat.S_ISREG(
                metadata.st_mode
            ):
                return False, file_count, directory_count
            if not write_allowed(child) and not write_open_is_denied(child):
                return False, file_count, directory_count
            file_count += 1
    return not walk_errors, file_count, directory_count


def resolve_declared_relative_path(root: Path, value: object) -> Path | None:
    relative = Path(str(value))
    if (
        not str(value)
        or relative.is_absolute()
        or ".." in relative.parts
        or "." in relative.parts
    ):
        return None
    path = root / relative
    if (
        not path_is_within(path, root)
        or not canonical_symlink_free_path(path)
    ):
        return None
    return path


def unix_socket_metadata(path: Path) -> dict[str, object]:
    """Return final-path metadata without following a symlink."""
    try:
        metadata = path.lstat()
    except OSError:
        return {
            "exists": False,
            "is_symlink": False,
            "is_socket": False,
            "owner_uid": None,
            "mode": None,
        }
    return {
        "exists": True,
        "is_symlink": stat.S_ISLNK(metadata.st_mode),
        "is_socket": stat.S_ISSOCK(metadata.st_mode),
        "owner_uid": metadata.st_uid,
        "mode": f"{stat.S_IMODE(metadata.st_mode):04o}",
    }


def object_inodes(git_objects: Path) -> set[tuple[int, int]]:
    inodes: set[tuple[int, int]] = set()
    try:
        root_metadata = git_objects.lstat()
    except OSError:
        return inodes
    if (
        not stat.S_ISDIR(root_metadata.st_mode)
        or stat.S_ISLNK(root_metadata.st_mode)
    ):
        return inodes
    for directory, dirnames, filenames in os.walk(
        git_objects,
        followlinks=False,
    ):
        current = Path(directory)
        retained: list[str] = []
        for name in dirnames:
            try:
                metadata = (current / name).lstat()
            except OSError:
                continue
            if stat.S_ISDIR(metadata.st_mode) and not stat.S_ISLNK(
                metadata.st_mode
            ):
                retained.append(name)
        dirnames[:] = retained
        for name in filenames:
            try:
                metadata = (current / name).lstat()
            except OSError:
                continue
            if stat.S_ISREG(metadata.st_mode) and not stat.S_ISLNK(
                metadata.st_mode
            ):
                inodes.add((metadata.st_dev, metadata.st_ino))
    return inodes


def tree_file_inodes(
    root: Path,
    *,
    excluded_top_level: set[str] | None = None,
) -> tuple[set[tuple[int, int]], bool]:
    """Collect regular-file inodes without following any symlink."""
    excluded = excluded_top_level or set()
    inodes: set[tuple[int, int]] = set()
    symlink_free = True
    if not is_directory_no_symlink(root):
        return inodes, False
    for directory, dirnames, filenames in os.walk(root, followlinks=False):
        current = Path(directory)
        if current == root:
            dirnames[:] = [name for name in dirnames if name not in excluded]
        retained_directories: list[str] = []
        for name in dirnames:
            path = current / name
            try:
                metadata = path.lstat()
            except OSError:
                symlink_free = False
                continue
            if stat.S_ISLNK(metadata.st_mode):
                symlink_free = False
            else:
                retained_directories.append(name)
        dirnames[:] = retained_directories
        for name in filenames:
            path = current / name
            try:
                metadata = path.lstat()
            except OSError:
                symlink_free = False
                continue
            if stat.S_ISLNK(metadata.st_mode):
                symlink_free = False
            elif stat.S_ISREG(metadata.st_mode):
                inodes.add((metadata.st_dev, metadata.st_ino))
    return inodes, symlink_free


def load_boundary(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError("boundary manifest must be a JSON object")
    return value


def verify(boundary: dict) -> tuple[dict, list[str]]:
    errors: list[str] = []
    repository = boundary.get("repository", {})
    runtime = boundary.get("runtime_boundary", {})
    candidate_raw = Path(str(repository.get("absolute_root", "")))
    candidate = resolved(candidate_raw)
    trusted_root_raw = Path(str(repository.get("trusted_root", "")))
    trusted_root = resolved(trusted_root_raw)
    base_commit = repository.get("base_commit")

    checks: dict[str, bool] = {}
    checks["packet_is_wp0001"] = boundary.get("packet_id") == "WP-0001"
    checks["candidate_root_is_canonical"] = (
        canonical_symlink_free_path(candidate_raw)
    )
    candidate_safe = checks["candidate_root_is_canonical"]
    checks["candidate_exists"] = candidate_safe and candidate.is_dir()
    trusted_root_safe = canonical_symlink_free_path(trusted_root_raw)
    checks["trusted_root_exists"] = (
        trusted_root_safe
        and trusted_root.is_dir()
        and is_directory_no_symlink(trusted_root / ".git")
    )
    checks["candidate_separate_from_trusted_root"] = not paths_overlap(
        candidate,
        trusted_root,
    )
    checks["independent_git_directory"] = (
        candidate_safe and is_directory_no_symlink(candidate / ".git")
    )
    checks["candidate_owner_uid_matches"] = (
        candidate_safe
        and candidate.is_dir()
        and candidate.stat().st_uid == runtime.get("principal_uid")
    )
    checks["candidate_root_default_write_denied"] = (
        candidate_safe
        and candidate.is_dir()
        and not can_create_probe(candidate)
    )
    checks["git_directory_owner_uid_matches"] = (
        candidate_safe
        and is_directory_no_symlink(candidate / ".git")
        and (candidate / ".git").stat().st_uid == runtime.get("principal_uid")
    )
    checks["no_shared_git_object_inodes"] = not (
        candidate_safe
        and trusted_root_safe
        and (
            object_inodes(candidate / ".git" / "objects")
            & object_inodes(trusted_root / ".git" / "objects")
        )
    )
    if candidate_safe:
        _candidate_git_inodes, candidate_git_symlink_free = tree_file_inodes(
            candidate / ".git"
        )
        candidate_worktree_inodes, candidate_worktree_symlink_free = (
            tree_file_inodes(candidate, excluded_top_level={".git"})
        )
    else:
        candidate_git_symlink_free = False
        candidate_worktree_inodes = set()
        candidate_worktree_symlink_free = False
    if trusted_root_safe:
        trusted_worktree_inodes, trusted_worktree_symlink_free = tree_file_inodes(
            trusted_root,
            excluded_top_level={".git"},
        )
    else:
        trusted_worktree_inodes = set()
        trusted_worktree_symlink_free = False
    checks["git_directory_symlink_free"] = candidate_git_symlink_free
    checks["candidate_worktree_symlink_free"] = candidate_worktree_symlink_free
    checks["no_shared_worktree_inodes"] = not (
        candidate_safe
        and trusted_root_safe
        and candidate_worktree_inodes
        and trusted_worktree_inodes
        and candidate_worktree_inodes & trusted_worktree_inodes
    )
    checks["no_git_file_indirection"] = (
        candidate_safe and is_directory_no_symlink(candidate / ".git")
    )
    checks["no_alternates"] = not (
        candidate_safe
        and os.path.lexists(
            candidate / ".git" / "objects" / "info" / "alternates"
        )
    )

    protected_values = boundary.get("protection_boundary", {}).get(
        "protected_paths",
        [],
    )
    protected_observed: list[dict[str, object]] = []
    protected_safe = (
        isinstance(protected_values, list)
        and bool(protected_values)
        and all(isinstance(value, str) for value in protected_values)
        and len(protected_values) == len(set(protected_values))
    )
    for value in protected_values if isinstance(protected_values, list) else []:
        candidate_path = (
            resolve_declared_relative_path(candidate, value)
            if candidate_safe
            else None
        )
        trusted_path = (
            resolve_declared_relative_path(trusted_root, value)
            if trusted_root_safe
            else None
        )
        candidate_read_only, candidate_files, candidate_directories = (
            protected_path_is_read_only(candidate_path)
            if candidate_path is not None
            else (False, 0, 0)
        )
        trusted_read_only, trusted_files, trusted_directories = (
            protected_path_is_read_only(trusted_path)
            if trusted_path is not None
            else (False, 0, 0)
        )
        path_safe = (
            candidate_path is not None
            and trusted_path is not None
            and candidate_read_only
            and trusted_read_only
        )
        protected_safe = protected_safe and path_safe
        protected_observed.append(
            {
                "declared_path": value,
                "candidate_path": (
                    str(candidate_path) if candidate_path is not None else None
                ),
                "candidate_read_only": candidate_read_only,
                "candidate_files_checked": candidate_files,
                "candidate_directories_checked": candidate_directories,
                "trusted_path": (
                    str(trusted_path) if trusted_path is not None else None
                ),
                "trusted_read_only": trusted_read_only,
                "trusted_files_checked": trusted_files,
                "trusted_directories_checked": trusted_directories,
            }
        )

    protection = boundary.get("protection_boundary", {})
    writable_values = protection.get("writable_paths", [])
    writable_observed: list[dict[str, object]] = []
    writable_safe = (
        isinstance(writable_values, list)
        and bool(writable_values)
        and all(isinstance(value, str) for value in writable_values)
        and len(writable_values) == len(set(writable_values))
    )
    for value in writable_values if isinstance(writable_values, list) else []:
        allowed, absolute = declared_path_is_writable(candidate, value)
        writable_safe = writable_safe and allowed
        writable_observed.append(
            {
                "declared_path": value,
                "absolute_path": absolute,
                "writable": allowed,
            }
        )
    scratch_values = protection.get("ephemeral_scratch_paths", [])
    scratch_observed: list[dict[str, object]] = []
    scratch_safe = (
        isinstance(scratch_values, list)
        and bool(scratch_values)
        and all(isinstance(value, str) for value in scratch_values)
        and len(scratch_values) == len(set(scratch_values))
    )
    for value in scratch_values if isinstance(scratch_values, list) else []:
        allowed, absolute = declared_path_is_writable(candidate, value)
        scratch_safe = scratch_safe and allowed
        scratch_observed.append(
            {
                "declared_path": value,
                "absolute_path": absolute,
                "writable": allowed,
            }
        )
    checks["declared_writable_paths_exact_and_writable"] = writable_safe
    checks["scratch_paths_exact_and_writable"] = scratch_safe
    candidate_scope_safe, candidate_scope_files, candidate_scope_directories = (
        candidate_write_scope_is_exact(
            candidate,
            [
                *(
                    writable_values
                    if isinstance(writable_values, list)
                    else []
                ),
                *(
                    scratch_values
                    if isinstance(scratch_values, list)
                    else []
                ),
            ],
        )
        if candidate_safe
        else (False, 0, 0)
    )
    checks["candidate_write_scope_exact"] = candidate_scope_safe

    observed: dict[str, object] = {
        "candidate_root": str(candidate),
        "trusted_root": str(trusted_root),
        "protected_paths": protected_observed,
        "writable_paths": writable_observed,
        "scratch_paths": scratch_observed,
        "candidate_write_scope": {
            "exact": candidate_scope_safe,
            "files_checked": candidate_scope_files,
            "directories_checked": candidate_scope_directories,
        },
        "principal_uid": os.getuid(),
        "environment_bindings": {
            key: os.environ.get(key)
            for key in ("HOME", "TMPDIR", "TMP", "TEMP")
        },
        "client_environment": {
            key: os.environ.get(key)
            for key in CLIENT_ENVIRONMENT_KEYS
        },
        "boot_session_sha256": boot_session_sha256(),
        "forbidden_credential_env_keys_present": sorted(
            key for key in FORBIDDEN_CREDENTIAL_ENV_KEYS if key in os.environ
        ),
    }

    checks["git_metadata_passive"] = (
        candidate_safe
        and checks["independent_git_directory"]
        and candidate_git_symlink_free
        and git_metadata_is_passive(candidate)
    )
    if (
        candidate_safe
        and checks["candidate_exists"]
        and checks["independent_git_directory"]
        and checks["git_metadata_passive"]
    ):
        try:
            head = git(candidate, "rev-parse", "HEAD").stdout.strip()
            common_dir_raw = git(
                candidate,
                "rev-parse",
                "--git-common-dir",
            ).stdout.strip()
            git_dir_raw = git(candidate, "rev-parse", "--git-dir").stdout.strip()
            index_listing = git(
                candidate,
                "ls-files",
                "--stage",
                "-z",
            ).stdout
            head_listing = git(
                candidate,
                "ls-tree",
                "-r",
                "-z",
                "HEAD",
            ).stdout
            index_entries, index_errors = parse_index_listing(index_listing)
            head_entries, head_errors = parse_tree_listing(head_listing)
            status = raw_worktree_status(
                candidate,
                index_entries,
                head_entries,
                [*index_errors, *head_errors],
                boundary.get("protection_boundary", {}).get(
                    "ephemeral_scratch_paths",
                    [],
                ),
            )
            remotes = [
                line
                for line in git(candidate, "remote", "-v").stdout.splitlines()
                if line.strip()
            ]
            symbolic = git(
                candidate,
                "symbolic-ref",
                "-q",
                "HEAD",
                check=False,
            )
        except (OSError, subprocess.SubprocessError) as exc:
            errors.append(f"Git inspection failed: {exc}")
        else:
            git_dir = resolved(candidate / git_dir_raw)
            common_dir = resolved(candidate / common_dir_raw)
            observed.update(
                {
                    "head": head,
                    "git_directory": str(git_dir),
                    "git_common_directory": str(common_dir),
                    "status_porcelain": status,
                    "remotes": remotes,
                    "symbolic_head": symbolic.stdout.strip(),
                }
            )
            checks["head_matches"] = head == base_commit
            checks["detached_head"] = symbolic.returncode != 0
            checks["git_directory_is_candidate_dot_git"] = git_dir == candidate / ".git"
            checks["git_common_directory_is_candidate_dot_git"] = (
                common_dir == candidate / ".git"
            )
            checks["clean_worktree"] = status == []
            checks["zero_remotes"] = remotes == []
    else:
        checks.update(
            {
                "head_matches": False,
                "detached_head": False,
                "git_directory_is_candidate_dot_git": False,
                "git_common_directory_is_candidate_dot_git": False,
                "clean_worktree": False,
                "zero_remotes": False,
            }
        )

    expected_bindings = runtime.get("environment_bindings", {})
    checks["principal_uid_matches"] = os.getuid() == runtime.get("principal_uid")
    checks["environment_bindings_match"] = observed["environment_bindings"] == (
        expected_bindings
    )
    expected_client_environment = runtime.get("client_environment_guard", {})
    expected_client_values = {
        key: expected_client_environment.get(key)
        for key in CLIENT_ENVIRONMENT_KEYS
    }
    checks["client_environment_guard_matches"] = (
        observed["client_environment"] == expected_client_values
        and expected_client_environment.get("absent_variables")
        == sorted(FORBIDDEN_CREDENTIAL_ENV_KEYS)
    )
    checks["forbidden_credential_env_absent"] = (
        observed["forbidden_credential_env_keys_present"] == []
    )
    checks["boot_session_matches"] = (
        observed["boot_session_sha256"] == runtime.get("boot_session_sha256")
    )
    trusted_tree_read_only, trusted_files, trusted_directories = (
        protected_path_is_read_only(trusted_root)
        if trusted_root_safe
        else (False, 0, 0)
    )
    observed["trusted_root_write_denial"] = {
        "read_only": trusted_tree_read_only,
        "files_checked": trusted_files,
        "directories_checked": trusted_directories,
    }
    checks["trusted_root_not_writable"] = (
        trusted_root_safe
        and trusted_root.is_dir()
        and trusted_worktree_symlink_free
        and not can_create_probe(trusted_root)
        and trusted_tree_read_only
    )
    creator_home_raw = Path(str(runtime.get("ambient_host_home_root", "")))
    creator_home = resolved(creator_home_raw)
    creator_home_safe = canonical_symlink_free_path(creator_home_raw)
    checks["creator_home_exists"] = creator_home_safe and creator_home.is_dir()
    creator_home_read_only, creator_home_files, creator_home_directories = (
        protected_path_is_read_only(creator_home)
        if creator_home_safe
        else (False, 0, 0)
    )
    observed["creator_home_write_denial"] = {
        "read_only": creator_home_read_only,
        "files_checked": creator_home_files,
        "directories_checked": creator_home_directories,
    }
    checks["creator_home_not_writable"] = (
        creator_home_safe
        and creator_home.is_dir()
        and not can_create_probe(creator_home)
        and creator_home_read_only
    )

    runtime_uid = runtime.get("principal_uid")
    runtime_home_raw = Path(str(runtime.get("ephemeral_home_root", "")))
    runtime_temp_raw = Path(str(runtime.get("private_temp_root", "")))
    runtime_home = resolved(runtime_home_raw)
    runtime_temp = resolved(runtime_temp_raw)
    runtime_home_path_safe = canonical_symlink_free_path(runtime_home_raw)
    runtime_temp_path_safe = canonical_symlink_free_path(runtime_temp_raw)
    checks["runtime_home_exists_owned_private"] = (
        runtime_home_path_safe
        and runtime_home.is_dir()
        and runtime_home.stat().st_uid == runtime_uid
        and runtime_home.stat().st_mode & 0o077 == 0
    )
    checks["runtime_temp_exists_owned_private"] = (
        runtime_temp_path_safe
        and runtime_temp.is_dir()
        and runtime_temp.stat().st_uid == runtime_uid
        and runtime_temp.stat().st_mode & 0o077 == 0
    )
    checks["runtime_home_writable"] = (
        runtime_home_path_safe
        and runtime_home.is_dir()
        and can_create_probe(runtime_home)
    )
    checks["runtime_temp_writable"] = (
        runtime_temp_path_safe
        and runtime_temp.is_dir()
        and can_create_probe(runtime_temp)
    )
    _, runtime_home_tree_symlink_free = (
        tree_file_inodes(runtime_home)
        if runtime_home_path_safe
        else (set(), False)
    )
    _, runtime_temp_tree_symlink_free = (
        tree_file_inodes(runtime_temp)
        if runtime_temp_path_safe
        else (set(), False)
    )
    checks["runtime_home_exists_owned_private"] = (
        checks["runtime_home_exists_owned_private"]
        and runtime_home_tree_symlink_free
    )
    checks["runtime_temp_exists_owned_private"] = (
        checks["runtime_temp_exists_owned_private"]
        and runtime_temp_tree_symlink_free
    )

    client_guard = runtime.get("client_environment_guard", {})
    guarded_paths = {
        "CODEX_HOME": runtime_home / ".codex",
        "XDG_CONFIG_HOME": runtime_home / ".config",
        "XDG_CACHE_HOME": runtime_home / ".cache",
        "XDG_DATA_HOME": runtime_home / ".local" / "share",
        "GIT_CONFIG_GLOBAL": runtime_home / ".gitconfig",
    }
    client_guard_paths_safe = (
        runtime_home_path_safe
        and all(
            client_guard.get(key) == str(expected)
            and path_is_within(expected, runtime_home)
            and canonical_symlink_free_path(
                expected,
                allow_missing_leaf=True,
            )
            for key, expected in guarded_paths.items()
        )
    )
    checks["client_environment_guard_matches"] = (
        checks["client_environment_guard_matches"]
        and client_guard_paths_safe
    )

    shared_root_declarations = [
        Path(str(path))
        for path in runtime.get("ambient_shared_temp_roots", [])
    ]
    shared_roots = [resolved(path) for path in shared_root_declarations]
    shared_roots_safe = bool(shared_roots) and all(
        canonical_symlink_free_path(declared)
        for declared in shared_root_declarations
    )
    checks["shared_temp_roots_exist"] = bool(shared_roots) and all(
        shared_roots_safe and root.is_dir() for root in shared_roots
    )
    checks["shared_temp_default_write_denied"] = bool(shared_roots) and all(
        shared_roots_safe
        and root.is_dir()
        and not can_create_probe(root)
        for root in shared_roots
    )
    socket_exceptions = runtime.get("ambient_shared_temp_write_exceptions", [])
    bridge = boundary.get("unity_mcp_route", {}).get("bridge", {})
    endpoint_declared = Path(str(bridge.get("endpoint", "")))
    endpoint = resolved(endpoint_declared)
    endpoint_path_safe = (
        canonical_symlink_free_path(endpoint_declared)
        and any(path_is_within(endpoint, root) for root in shared_roots)
    )
    socket_metadata = (
        unix_socket_metadata(endpoint_declared)
        if endpoint_path_safe
        else {
            "exists": False,
            "is_symlink": False,
            "is_socket": False,
            "owner_uid": None,
            "mode": None,
        }
    )
    checks["socket_exception_exact"] = socket_exceptions == [str(endpoint)]
    checks["socket_exists_owned_0600"] = (
        socket_metadata["exists"] is True
        and socket_metadata["owner_uid"] == runtime_uid
        and socket_metadata["mode"] == "0600"
    )
    checks["socket_is_unix_domain_not_symlink"] = (
        socket_metadata["is_socket"] is True
        and socket_metadata["is_symlink"] is False
    )

    approved_environment = boundary.get("approved_environment", {})
    activation_evidence = boundary.get("activation_evidence", {})
    sandbox_ref = activation_evidence.get("sandbox_policy", {})
    network_ref = activation_evidence.get("network_policy", {})
    sandbox_path = (
        resolve_declared_relative_path(trusted_root, sandbox_ref.get("path", ""))
        if trusted_root_safe
        else None
    )
    network_path = (
        resolve_declared_relative_path(trusted_root, network_ref.get("path", ""))
        if trusted_root_safe
        else None
    )
    checks["sandbox_policy_hash_matches"] = (
        sandbox_path is not None
        and sandbox_path.is_file()
        and sha256_file(sandbox_path)
        == approved_environment.get("sandbox_profile_sha256")
        == sandbox_ref.get("sha256")
    )
    checks["network_policy_hash_matches"] = (
        network_path is not None
        and network_path.is_file()
        and sha256_file(network_path)
        == approved_environment.get("network_policy_sha256")
        == network_ref.get("sha256")
    )
    observed.update(
        {
            "runtime_home": str(runtime_home),
            "runtime_temp": str(runtime_temp),
            "shared_temp_roots": [str(root) for root in shared_roots],
            "socket_exception": str(endpoint),
            "socket_declared_path": str(endpoint_declared),
            "socket_owner_uid": socket_metadata["owner_uid"],
            "socket_mode": socket_metadata["mode"],
            "socket_is_socket": socket_metadata["is_socket"],
            "socket_is_symlink": socket_metadata["is_symlink"],
            "sandbox_policy_path": (
                str(sandbox_path) if sandbox_path is not None else None
            ),
            "network_policy_path": (
                str(network_path) if network_path is not None else None
            ),
        }
    )

    for name, passed in checks.items():
        if not passed:
            errors.append(f"failed live check: {name}")

    result = {
        "schema_version": 1,
        "validator_version": VALIDATOR_VERSION,
        "packet_id": "WP-0001",
        "captured_at": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "candidate_root": str(candidate),
        "base_commit": base_commit,
        "checks": checks,
        "observed": observed,
        "result": "pass" if not errors else "fail",
    }
    return result, errors


def write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp")
    temporary.write_text(
        json.dumps(value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    os.replace(temporary, path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--boundary", required=True, type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    try:
        boundary = load_boundary(args.boundary)
        result, errors = verify(boundary)
    except (
        OSError,
        ValueError,
        json.JSONDecodeError,
        subprocess.SubprocessError,
    ) as exc:
        print(f"WP-0001 LIVE BOUNDARY: FAIL\n- {exc}", file=sys.stderr)
        return 1

    if args.output is not None:
        write_json(args.output, result)
    else:
        print(json.dumps(result, indent=2, sort_keys=True))
    if errors:
        print("WP-0001 LIVE BOUNDARY: FAIL", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print("WP-0001 LIVE BOUNDARY: PASS", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
