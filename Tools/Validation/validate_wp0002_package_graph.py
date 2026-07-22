#!/usr/bin/env python3
"""Fail-closed WP-0002 package-graph comparison against protected base."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import secrets
import stat
import subprocess
import sys
from pathlib import Path


CHECKER_CONTRACT_VERSION = "wp0002-package-graph-v3"
PROTECTED_BASE_COMMIT = "b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5"
GRAPH_PATHS = (
    "Game/Packages/manifest.json",
    "Game/Packages/packages-lock.json",
    "SimulationCore/package.json",
    "SaveContracts/package.json",
)
LAST_BEARING_PATHS = (
    "SimulationCore/Runtime/LastBearing",
    "SaveContracts/Runtime/LastBearing",
    "Game/Assets/AtomicLandPirate/LastBearing",
    "Game/Assets/AtomicLandPirate/LastBearing.meta",
    "Tests/AtomicLandPirate.CoreTests/LastBearing",
)
LOCAL_PACKAGE_LINKS = {
    "com.ac21.sasha.simulation-core": "file:../../SimulationCore",
    "com.ac21.sasha.save-contracts": "file:../../SaveContracts",
}
LOCAL_LOCK_ENTRIES = {
    package_name: {
        "version": link,
        "depth": 0,
        "source": "local",
        "dependencies": {},
    }
    for package_name, link in LOCAL_PACKAGE_LINKS.items()
}
PIPELINE_PACKAGE_LINK = {
    "com.unity.pipeline": "0.3.1-exp.1",
}
PIPELINE_LOCK_ENTRIES = {
    "com.unity.pipeline": {
        "version": "0.3.1-exp.1",
        "depth": 0,
        "source": "registry",
        "dependencies": {
            "com.unity.test-framework": "1.1.33",
            "com.unity.modules.jsonserialize": "1.0.0",
            "com.unity.nuget.newtonsoft-json": "3.0.2",
            "com.unity.nuget.mono-cecil": "1.11.6",
            "com.unity.modules.uielements": "1.0.0",
            "com.unity.modules.screencapture": "1.0.0",
        },
        "url": "https://packages.unity.com",
    },
    "com.unity.modules.screencapture": {
        "version": "1.0.0",
        "depth": 1,
        "source": "builtin",
        "dependencies": {
            "com.unity.modules.imageconversion": "1.0.0",
        },
    },
}
AUTHORIZED_MANIFEST_ADDITIONS = {
    **LOCAL_PACKAGE_LINKS,
    **PIPELINE_PACKAGE_LINK,
}
AUTHORIZED_LOCK_ADDITIONS = {
    **LOCAL_LOCK_ENTRIES,
    **PIPELINE_LOCK_ENTRIES,
}


class DuplicateKeyError(ValueError):
    """Raised when JSON uses an ambiguous duplicate object key."""


def _unique_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise DuplicateKeyError(f"duplicate JSON key {key!r}")
        result[key] = value
    return result


def _parse_json(data: bytes, path: str) -> tuple[object | None, list[str]]:
    try:
        return (
            json.loads(data.decode("utf-8"), object_pairs_hook=_unique_object),
            [],
        )
    except (UnicodeDecodeError, json.JSONDecodeError, DuplicateKeyError) as exc:
        return None, [f"{path} is not unambiguous UTF-8 JSON: {exc}"]


def _json_values_equal(left: object, right: object) -> bool:
    """Compare JSON values without Python's bool/int or int/float coercions."""
    if type(left) is not type(right):
        return False
    if isinstance(left, dict):
        if set(left) != set(right):  # type: ignore[arg-type]
            return False
        return all(
            _json_values_equal(value, right[key])  # type: ignore[index]
            for key, value in left.items()
        )
    if isinstance(left, list):
        return len(left) == len(right) and all(  # type: ignore[arg-type]
            _json_values_equal(left_value, right_value)
            for left_value, right_value in zip(left, right)  # type: ignore[arg-type]
        )
    return left == right


def _compare_dependency_document(
    *,
    label: str,
    base_data: bytes,
    candidate_data: bytes,
    exact_additions: dict[str, object],
) -> list[str]:
    errors: list[str] = []
    base, base_errors = _parse_json(base_data, f"protected {label}")
    candidate, candidate_errors = _parse_json(candidate_data, label)
    errors.extend(base_errors)
    errors.extend(candidate_errors)
    if errors:
        return errors
    if not isinstance(base, dict) or not isinstance(candidate, dict):
        return [f"{label} and its protected base must be JSON objects"]
    if set(candidate) != set(base):
        errors.append(f"{label} top-level keys differ from protected base")
        return errors
    base_dependencies = base.get("dependencies")
    candidate_dependencies = candidate.get("dependencies")
    if not isinstance(base_dependencies, dict) or not isinstance(
        candidate_dependencies, dict
    ):
        return errors + [f"{label}.dependencies must remain a JSON object"]
    for key, value in base.items():
        if key != "dependencies" and not _json_values_equal(candidate.get(key), value):
            errors.append(f"{label} top-level field {key!r} differs from protected base")
    for package_name, base_value in base_dependencies.items():
        if package_name not in candidate_dependencies:
            errors.append(f"{label} removed existing dependency {package_name}")
        elif not _json_values_equal(candidate_dependencies[package_name], base_value):
            errors.append(f"{label} changed existing dependency {package_name}")
    additions = set(candidate_dependencies) - set(base_dependencies)
    expected_additions = set(exact_additions)
    if additions != expected_additions:
        errors.append(
            f"{label} additions must be exactly {sorted(expected_additions)}, "
            f"found {sorted(additions)}"
        )
    for package_name, expected in exact_additions.items():
        if not _json_values_equal(candidate_dependencies.get(package_name), expected):
            errors.append(
                f"{label} dependency {package_name} must equal {expected!r}"
            )
    return errors


def compare_package_graph(
    base_files: dict[str, bytes],
    candidate_files: dict[str, bytes],
    *,
    require_links: bool = False,
) -> list[str]:
    """Return exact package-graph violations for pre- or post-materialization state."""
    errors: list[str] = []
    if set(base_files) != set(GRAPH_PATHS):
        errors.append("protected package-graph input paths are incomplete or excessive")
    if set(candidate_files) != set(GRAPH_PATHS):
        errors.append("candidate package-graph input paths are incomplete or excessive")
    if errors:
        return errors
    if all(base_files[path] == candidate_files[path] for path in GRAPH_PATHS):
        if require_links:
            return [
                "LastBearing materialization requires the exact authorized UPM "
                "dependencies and lock entries"
            ]
        return []
    for path in ("SimulationCore/package.json", "SaveContracts/package.json"):
        if candidate_files[path] != base_files[path]:
            errors.append(f"{path} must remain byte-identical to protected base")
    manifest, manifest_errors = _parse_json(
        candidate_files["Game/Packages/manifest.json"],
        "Game/Packages/manifest.json",
    )
    errors.extend(manifest_errors)
    manifest_dependencies = (
        manifest.get("dependencies") if isinstance(manifest, dict) else None
    )
    pipeline_declared = (
        isinstance(manifest_dependencies, dict)
        and "com.unity.pipeline" in manifest_dependencies
    )
    manifest_additions = (
        AUTHORIZED_MANIFEST_ADDITIONS
        if pipeline_declared
        else LOCAL_PACKAGE_LINKS
    )
    lock_additions = (
        AUTHORIZED_LOCK_ADDITIONS
        if pipeline_declared
        else LOCAL_LOCK_ENTRIES
    )
    errors.extend(
        _compare_dependency_document(
            label="Game/Packages/manifest.json",
            base_data=base_files["Game/Packages/manifest.json"],
            candidate_data=candidate_files["Game/Packages/manifest.json"],
            exact_additions=manifest_additions,
        )
    )
    errors.extend(
        _compare_dependency_document(
            label="Game/Packages/packages-lock.json",
            base_data=base_files["Game/Packages/packages-lock.json"],
            candidate_data=candidate_files["Game/Packages/packages-lock.json"],
            exact_additions=lock_additions,
        )
    )
    return errors


def _last_bearing_materialized(repo_root: Path) -> bool:
    """Treat any current-tree LastBearing path, including a symlink, as materialized."""
    return any(os.path.lexists(repo_root / path) for path in LAST_BEARING_PATHS)


def _git_blob(repo_root: Path, commit: str, path: str) -> bytes:
    result = subprocess.run(
        [
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
            "--no-optional-locks",
            "-C",
            str(repo_root),
            "show",
            f"{commit}:{path}",
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        stdin=subprocess.DEVNULL,
        check=False,
        env={
            "HOME": "/var/empty",
            "XDG_CONFIG_HOME": "/var/empty",
            "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
            "LANG": "C",
            "LC_ALL": "C",
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_CONFIG_GLOBAL": "/dev/null",
            "GIT_OPTIONAL_LOCKS": "0",
            "GIT_NO_REPLACE_OBJECTS": "1",
            "GIT_NO_LAZY_FETCH": "1",
            "GIT_TERMINAL_PROMPT": "0",
            "GIT_PAGER": "",
            "PAGER": "",
        },
        timeout=20,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        raise RuntimeError(f"cannot read protected {commit}:{path}: {detail}")
    return result.stdout


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _write_report(repo_root: Path, report: str, payload: dict[str, object]) -> None:
    repo_lexical = Path(os.path.abspath(os.fspath(repo_root)))
    allowed_lexical = repo_lexical / "BuildArtifacts" / "WP-0002"
    destination_lexical = Path(
        os.path.abspath(os.path.join(os.fspath(repo_lexical), report))
    )
    try:
        allowed_lexical.relative_to(repo_lexical)
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
    parser.add_argument("--base", required=True)
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--lock", required=True)
    parser.add_argument("--simulation-package", required=True)
    parser.add_argument("--save-package", required=True)
    parser.add_argument("--report", required=True)
    args = parser.parse_args()
    supplied_paths = (
        args.manifest,
        args.lock,
        args.simulation_package,
        args.save_package,
    )
    errors: list[str] = []
    if args.base != PROTECTED_BASE_COMMIT:
        errors.append(f"base must equal protected commit {PROTECTED_BASE_COMMIT}")
    if supplied_paths != GRAPH_PATHS:
        errors.append(f"package graph paths must equal {list(GRAPH_PATHS)!r}")
    repo_root = Path(__file__).resolve().parents[2]
    local_links_required = _last_bearing_materialized(repo_root)
    base_files: dict[str, bytes] = {}
    candidate_files: dict[str, bytes] = {}
    if not errors:
        try:
            for path in GRAPH_PATHS:
                base_files[path] = _git_blob(repo_root, args.base, path)
                candidate_files[path] = (repo_root / path).read_bytes()
        except (OSError, RuntimeError) as exc:
            errors.append(str(exc))
    if not errors:
        errors.extend(
            compare_package_graph(
                base_files,
                candidate_files,
                require_links=local_links_required,
            )
        )
    report = {
        "schema_version": 1,
        "checker_contract": CHECKER_CONTRACT_VERSION,
        "base_commit": args.base,
        "local_links_required": local_links_required,
        "result": "pass" if not errors else "fail",
        "errors": errors,
        "candidate_sha256": {
            path: _sha256(data) for path, data in candidate_files.items()
        },
    }
    try:
        _write_report(repo_root, args.report, report)
    except (OSError, ValueError) as exc:
        print(f"WP-0002 PACKAGE GRAPH: FAIL: {exc}", file=sys.stderr)
        return 2
    if errors:
        print("WP-0002 PACKAGE GRAPH: FAIL", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print("WP-0002 PACKAGE GRAPH: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
