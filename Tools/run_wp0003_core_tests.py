#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ElementTree
from collections import deque
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TESTS = ROOT / "Tests"
PROJECT = Path("AtomicLandPirate.CoreTests/AtomicLandPirate.CoreTests.csproj")
LOCAL_ONLY_ARTIFACTS = ROOT / "BuildArtifacts/WP-0003/local-only"
LOCAL_ARTIFACTS = LOCAL_ONLY_ARTIFACTS / "core-tests"
DOTNET_ARTIFACTS = LOCAL_ONLY_ARTIFACTS / "dotnet"
CROSS_ROOT_ARTIFACTS = LOCAL_ARTIFACTS / "cross-root"
SYSTEM_GIT = Path("/usr/bin/git")
UNITY_BASELINE_MANIFEST = (
    ROOT / "docs/manifests/WP-0003-unity-canonical-v2.json"
)
UNITY_BASELINE_MANIFEST_SHA256 = (
    "d7b9d48c1669ed1eb59a1cc435f22f12f1054298c2b33c3ade61517c0bd5a587"
)
UNITY_BASELINE_COMMIT = (
    "0031133d476789f0c83d5791180c606f9d1af6f9"
)
UNITY_BASELINE_GAME_TREE_OID = (
    "1ddad4d837466476b9b704a5eba461400937336b"
)
UNITY_TECHNICAL_SANDBOX_MANIFEST = (
    ROOT / "docs/manifests/WP-0003-unity-technical-sandbox-v1.json"
)
UNITY_TECHNICAL_SANDBOX_MANIFEST_SHA256 = (
    "68fb887cdc796470c9b76d69c52a63ae5badc2f8a89ac9453a6a0844b09db9e7"
)
TECHNICAL_SANDBOX_ROOT = (
    "Assets/AtomicLandPirate/TechnicalSandbox"
)
TECHNICAL_SANDBOX_PREFIX = f"{TECHNICAL_SANDBOX_ROOT}/"
TECHNICAL_SANDBOX_STRUCTURAL_METAS = {
    "Assets/AtomicLandPirate.meta",
    "Assets/AtomicLandPirate/TechnicalSandbox.meta",
}
TECHNICAL_SANDBOX_ALLOWED_SUFFIXES = (
    ".asmdef",
    ".cs",
    ".meta",
    ".unity",
)
EDITOR_BUILD_SETTINGS_PATH = (
    "ProjectSettings/EditorBuildSettings.asset"
)
EXPECTED_DIRECT_PACKAGES = {
    "com.unity.ai.assistant": "2.14.0-pre.1",
    "com.unity.ai.navigation": "2.0.13",
    "com.unity.inputsystem": "1.19.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.imageconversion": "1.0.0",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.physics2d": "1.0.0",
    "com.unity.modules.unityanalytics": "1.0.0",
    "com.unity.modules.unitywebrequestaudio": "1.0.0",
    "com.unity.modules.video": "1.0.0",
    "com.unity.render-pipelines.universal": "17.5.0",
    "com.unity.test-framework": "1.7.0",
}
EXPECTED_DIRECT_PACKAGES_SHA256 = (
    "45d64e2107723c13d0bba45beba73dbc54e02ba826f3bf30219cde9ddc0c4f3e"
)
EXPECTED_PACKAGE_LOCK_SHA256 = (
    "9840ac57af6738620a6efc6c50bfbaf2f758a71e7fd8fd148fed5fae202ebf5b"
)
EXPECTED_PROJECT_IDENTITY = {
    "company_name": "AC-21",
    "product_name": "Sasha the Atomic Land Pirate",
    "product_guid": "a6426107030e53cca527036b78d7a7e3",
    "project_name": "Sasha the Atomic Land Pirate",
    "cloud_project_id": "",
    "organization_id": "",
    "bundle_identifier": "com.ac21.sasha.atomiclandpirate",
    "identity_status": "temporary-development-only",
    "durable_save_bytes": False,
    "cloud_services_enabled": False,
}
PACKAGE_NAME_PATTERN = re.compile(
    r"com\.unity(?:\.[a-z0-9][a-z0-9-]*)+\Z"
)
SEMVER_PATTERN = re.compile(
    r"(?:0|[1-9][0-9]*)\."
    r"(?:0|[1-9][0-9]*)\."
    r"(?:0|[1-9][0-9]*)"
    r"(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\Z"
)
COMMAND_TIMEOUT_SECONDS = 120
UNITY_DOTNET = Path(
    "/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app/Contents/"
    "Resources/Scripting/DotNetSdk/dotnet"
)
UNITY_SDK_VERSION = "8.0.318"
UNITY_DOTNET_SHA256 = (
    "635898abd14a453117adbdbb45460fb0a3a55dd2b99eb38982bddd12b8d0649b"
)
UNITY_CSC = (
    UNITY_DOTNET.parent
    / "sdk"
    / UNITY_SDK_VERSION
    / "Roslyn/bincore/csc.dll"
)
UNITY_CSC_SHA256 = (
    "27d007d0b5a269c9b9549ba0faeafaae5df693114465f4a551207a780689b9a2"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run the offline WP-0003 deterministic-core test seam."
    )
    parser.add_argument(
        "--dotnet",
        help="dotnet executable; defaults to Unity-bundled SDK, then PATH",
    )
    return parser.parse_args()


def resolve_dotnet(requested: str | None) -> str:
    if requested:
        return requested
    if UNITY_DOTNET.is_file():
        return str(UNITY_DOTNET)
    discovered = shutil.which("dotnet")
    if discovered:
        return discovered
    raise SystemExit(
        "No dotnet executable found. No installation was attempted."
    )


def resolve_executable_path(command: str) -> Path:
    candidate = Path(command).expanduser()
    if candidate.is_file():
        return candidate.resolve()
    discovered = shutil.which(command)
    if discovered:
        return Path(discovered).resolve()
    raise SystemExit(f"dotnet executable does not exist: {command}")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def system_git_executable() -> str:
    if (
        SYSTEM_GIT.is_symlink()
        or not SYSTEM_GIT.is_file()
        or not os.access(SYSTEM_GIT, os.X_OK)
    ):
        raise SystemExit("Pinned system Git executable is missing or unsafe")
    return str(SYSTEM_GIT)


def require_safe_artifact_directory(path: Path) -> None:
    try:
        path.relative_to(LOCAL_ONLY_ARTIFACTS)
        relative_to_root = path.relative_to(ROOT)
    except ValueError as exception:
        raise SystemExit(
            f"Artifact path escapes the WP-0003 local-only root: {path}"
        ) from exception

    current = ROOT
    for component in relative_to_root.parts:
        current /= component
        if current.is_symlink():
            raise SystemExit(
                f"Artifact path contains a symlink: "
                f"{current.relative_to(ROOT)}"
            )
        if current.exists() and not current.is_dir():
            raise SystemExit(
                f"Artifact path contains a non-directory: "
                f"{current.relative_to(ROOT)}"
            )

    resolved_root = ROOT.resolve(strict=True)
    resolved_boundary = LOCAL_ONLY_ARTIFACTS.resolve(strict=False)
    resolved_path = path.resolve(strict=False)
    if resolved_root not in resolved_boundary.parents or (
        resolved_path != resolved_boundary
        and resolved_boundary not in resolved_path.parents
    ):
        raise SystemExit(
            f"Artifact path resolved outside the repository: {path}"
        )


def canonical_json_sha256(value: object) -> str:
    encoded = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def semver_core(version: str) -> tuple[int, int, int]:
    match = SEMVER_PATTERN.fullmatch(version)
    if match is None:
        raise SystemExit(f"Invalid Unity package version: {version!r}")
    core = version.split("-", 1)[0]
    return tuple(int(part) for part in core.split("."))


def require_unique_yaml_scalar(
    text: str,
    key: str,
    indent: int,
    label: str,
) -> str:
    pattern = re.compile(
        rf"(?m)^{re.escape(' ' * indent + key)}:[ \t]*(.*?)[ \t]*$"
    )
    values = pattern.findall(text)
    if len(values) != 1:
        raise SystemExit(
            f"{label} must contain exactly one {key!r} scalar"
        )
    return values[0]


def require_unique_yaml_map(
    text: str,
    key: str,
    indent: int,
    child_indent: int,
    label: str,
) -> dict[str, str]:
    lines = text.splitlines()
    header = " " * indent + key + ":"
    indexes = [index for index, line in enumerate(lines) if line == header]
    if len(indexes) != 1:
        raise SystemExit(
            f"{label} must contain exactly one {key!r} map"
        )
    result: dict[str, str] = {}
    prefix = " " * child_indent
    for line in lines[indexes[0] + 1:]:
        if not line.strip():
            continue
        leading = len(line) - len(line.lstrip(" "))
        if leading <= indent:
            break
        if leading != child_indent or not line.startswith(prefix):
            raise SystemExit(f"{label} {key!r} map shape drifted")
        child = line[child_indent:]
        if ":" not in child:
            raise SystemExit(f"{label} {key!r} map entry drifted")
        child_key, child_value = child.split(":", 1)
        if not child_key or child_key in result:
            raise SystemExit(f"{label} {key!r} map key drifted")
        result[child_key] = child_value.strip()
    return result


def run(
    command: list[str],
    environment: dict[str, str],
    *,
    capture_output: bool = False,
    cwd: Path = TESTS,
) -> bytes:
    rendered = " ".join(command)
    print(f"+ {rendered}", flush=True)
    try:
        result = subprocess.run(
            command,
            cwd=cwd,
            env=environment,
            check=True,
            stdout=subprocess.PIPE if capture_output else None,
            stderr=subprocess.STDOUT if capture_output else None,
            timeout=COMMAND_TIMEOUT_SECONDS,
        )
    except subprocess.TimeoutExpired as exception:
        raise SystemExit(
            f"Command exceeded {COMMAND_TIMEOUT_SECONDS}s timeout: {rendered}"
        ) from exception
    except subprocess.CalledProcessError as exception:
        if exception.stdout:
            sys.stdout.buffer.write(exception.stdout)
            sys.stdout.flush()
        raise SystemExit(
            f"Command failed with exit code {exception.returncode}: {rendered}"
        ) from exception

    if result.stdout:
        sys.stdout.buffer.write(result.stdout)
        sys.stdout.flush()
        return result.stdout
    return b""


def selected_compiler_path(
    dotnet: str,
    version: str,
    environment: dict[str, str],
) -> Path:
    sdk_lines = run(
        [dotnet, "--list-sdks"],
        environment,
        capture_output=True,
    ).decode("utf-8").splitlines()
    matches: list[Path] = []
    for line in sdk_lines:
        match = re.fullmatch(r"(\S+) \[(.+)\]", line.strip())
        if match and match.group(1) == version:
            matches.append(
                Path(match.group(2))
                / version
                / "Roslyn/bincore/csc.dll"
            )

    if len(matches) != 1 or not matches[0].is_file():
        raise SystemExit(
            "Could not resolve exactly one Roslyn compiler for selected SDK "
            f"{version}"
        )
    return matches[0].resolve()


def validate_dotnet(
    dotnet: str,
    environment: dict[str, str],
) -> None:
    executable = resolve_executable_path(dotnet)
    version = run(
        [dotnet, "--version"],
        environment,
        capture_output=True,
    ).decode("utf-8").strip()
    compiler = selected_compiler_path(dotnet, version, environment)
    executable_hash = sha256_file(executable)
    compiler_hash = sha256_file(compiler)
    is_unity_bundled = executable == UNITY_DOTNET.resolve()

    if is_unity_bundled:
        if version != UNITY_SDK_VERSION:
            raise SystemExit(
                "Unity-bundled SDK version drifted: "
                f"expected {UNITY_SDK_VERSION}, received {version}"
            )
        if executable_hash != UNITY_DOTNET_SHA256:
            raise SystemExit(
                "Unity-bundled dotnet hash drifted: "
                f"expected {UNITY_DOTNET_SHA256}, received {executable_hash}"
            )
        if compiler != UNITY_CSC.resolve():
            raise SystemExit(
                "Unity-bundled Roslyn compiler path drifted: "
                f"expected {UNITY_CSC}, received {compiler}"
            )
        if compiler_hash != UNITY_CSC_SHA256:
            raise SystemExit(
                "Unity-bundled Roslyn compiler hash drifted: "
                f"expected {UNITY_CSC_SHA256}, received {compiler_hash}"
            )
    elif re.fullmatch(r"8\.0\.3\d{2}", version) is None:
        raise SystemExit(
            "CI dotnet must resolve inside the explicit 8.0.3xx "
            f"compatibility band; received {version}"
        )

    print(f"TOOLCHAIN dotnet_version={version}")
    print(f"TOOLCHAIN dotnet_path={executable}")
    print(f"TOOLCHAIN dotnet_sha256={executable_hash}")
    print(f"TOOLCHAIN csc_path={compiler}")
    print(f"TOOLCHAIN csc_sha256={compiler_hash}")


def validate_project(
    path: Path,
    expected_properties: dict[str, str],
    expected_items: list[tuple[str, str]],
) -> None:
    root = ElementTree.parse(path).getroot()
    relative = path.relative_to(ROOT)
    if root.tag != "Project" or root.attrib != {"Sdk": "Microsoft.NET.Sdk"}:
        raise SystemExit(f"{relative} must use only Microsoft.NET.Sdk")

    properties: dict[str, str] = {}
    items: list[tuple[str, str]] = []
    for child in root:
        if child.tag == "PropertyGroup":
            if child.attrib:
                raise SystemExit(f"{relative} has conditional properties")
            for property_element in child:
                if property_element.attrib or list(property_element):
                    raise SystemExit(
                        f"{relative} has a non-literal property "
                        f"{property_element.tag}"
                    )
                if property_element.tag in properties:
                    raise SystemExit(
                        f"{relative} repeats property {property_element.tag}"
                    )
                properties[property_element.tag] = (
                    property_element.text or ""
                ).strip()
        elif child.tag == "ItemGroup":
            if child.attrib:
                raise SystemExit(f"{relative} has a conditional item group")
            for item in child:
                if (
                    set(item.attrib) != {"Include"}
                    or list(item)
                    or (item.text or "").strip()
                ):
                    raise SystemExit(
                        f"{relative} has a non-literal item {item.tag}"
                    )
                items.append((item.tag, item.attrib["Include"]))
        else:
            raise SystemExit(
                f"{relative} declares forbidden project element {child.tag}"
            )

    if properties != expected_properties:
        raise SystemExit(f"{relative} property contract drifted")
    if items != expected_items:
        raise SystemExit(f"{relative} item contract drifted")


def validate_file_closure(
    root: Path,
    expected_relative_paths: set[str],
) -> None:
    actual_relative: set[str] = set()
    for path in root.rglob("*"):
        if path.is_symlink():
            raise SystemExit(
                f"{path.relative_to(ROOT)} may not be a symlink"
            )
        if path.is_file():
            actual_relative.add(path.relative_to(root).as_posix())

    if actual_relative != expected_relative_paths:
        raise SystemExit(
            f"{root.relative_to(ROOT)} file closure drifted: "
            f"expected {sorted(expected_relative_paths)}, "
            f"received {sorted(actual_relative)}"
        )


def validate_global_json() -> None:
    path = ROOT / "Tests/global.json"
    document = json.loads(path.read_text(encoding="utf-8"))
    expected = {
        "sdk": {
            "version": "8.0.300",
            "rollForward": "latestPatch",
            "allowPrerelease": False,
        }
    }
    if document != expected:
        raise SystemExit("Tests/global.json SDK contract drifted")


def validate_nuget_config() -> None:
    path = ROOT / "Tools/nuget-offline.config"
    root = ElementTree.parse(path).getroot()
    if root.tag != "configuration" or root.attrib or len(root) != 1:
        raise SystemExit("nuget-offline.config root contract drifted")
    package_sources = root[0]
    if (
        package_sources.tag != "packageSources"
        or package_sources.attrib
        or len(package_sources) != 1
    ):
        raise SystemExit("nuget-offline.config package sources drifted")
    clear = package_sources[0]
    if (
        clear.tag != "clear"
        or clear.attrib
        or len(clear)
        or (clear.text or "").strip()
    ):
        raise SystemExit("nuget-offline.config must contain only clear")


def validate_output_props(
    path: Path,
    expected_artifacts_root: str,
) -> None:
    root = ElementTree.parse(path).getroot()
    if root.tag != "Project" or root.attrib:
        raise SystemExit(
            f"{path.relative_to(ROOT)} must be an unqualified project"
        )

    if len(root) != 1:
        raise SystemExit(
            f"{path.relative_to(ROOT)} must contain one PropertyGroup"
        )
    group = root[0]
    if group.tag != "PropertyGroup" or group.attrib:
        raise SystemExit(
            f"{path.relative_to(ROOT)} has a conditional output group"
        )

    actual_properties: dict[str, str] = {}
    for property_element in group:
        if property_element.attrib or list(property_element):
            raise SystemExit(
                f"{path.relative_to(ROOT)} output properties must be literal"
            )
        actual_properties[property_element.tag] = (
            property_element.text or ""
        ).strip()
    expected_properties = {
        "ArtifactsRoot": expected_artifacts_root,
        "BaseOutputPath": "$(ArtifactsRoot)bin/",
        "BaseIntermediateOutputPath": "$(ArtifactsRoot)obj/",
        "MSBuildProjectExtensionsPath": "$(BaseIntermediateOutputPath)",
    }
    if actual_properties != expected_properties:
        raise SystemExit(
            f"{path.relative_to(ROOT)} output contract drifted"
        )


def validate_empty_targets(path: Path) -> None:
    root = ElementTree.parse(path).getroot()
    if root.tag != "Project" or root.attrib or len(root):
        raise SystemExit(
            f"{path.relative_to(ROOT)} must remain an empty targets boundary"
        )


def validate_source_set(
    root: Path,
    expected_relative_paths: set[str],
) -> list[Path]:
    actual = sorted(root.rglob("*.cs"))
    actual_relative = {
        path.relative_to(root).as_posix()
        for path in actual
    }
    if actual_relative != expected_relative_paths:
        raise SystemExit(
            f"{root.relative_to(ROOT)} source set drifted: "
            f"expected {sorted(expected_relative_paths)}, "
            f"received {sorted(actual_relative)}"
        )
    return actual


def pinned_git_capture(arguments: list[str]) -> bytes:
    git_directory = ROOT / ".git"
    if git_directory.is_symlink() or not git_directory.is_dir():
        raise SystemExit("Repository must use a real independent .git directory")
    environment = {
        key: value
        for key, value in os.environ.items()
        if not key.startswith("GIT_")
    }
    environment.update(
        {
            "GIT_ATTR_NOSYSTEM": "1",
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_CONFIG_SYSTEM": os.devnull,
            "GIT_NO_REPLACE_OBJECTS": "1",
            "GIT_TERMINAL_PROMPT": "0",
        }
    )
    try:
        result = subprocess.run(
            [
                system_git_executable(),
                f"--git-dir={git_directory}",
                f"--work-tree={ROOT}",
                *arguments,
            ],
            check=True,
            env=environment,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=10,
        )
    except (
        OSError,
        subprocess.CalledProcessError,
        subprocess.TimeoutExpired,
    ) as exception:
        detail = getattr(exception, "stderr", b"")
        if isinstance(detail, bytes):
            detail = detail.decode("utf-8", errors="replace").strip()
        suffix = f": {detail}" if detail else ""
        raise SystemExit(
            f"Could not inspect the pinned Git repository{suffix}"
        ) from exception
    return result.stdout


def git_index_game_files(game_root: Path) -> dict[str, str]:
    """Read staged Game paths without materializing their blob contents."""

    relative_root = game_root.relative_to(ROOT).as_posix()
    if pinned_git_capture(
        ["ls-files", "--unmerged", "-z", "--", relative_root]
    ):
        raise SystemExit("Game has an unmerged Git index")

    indexed: dict[str, str] = {}
    output = pinned_git_capture(
        ["ls-files", "--stage", "-z", "--", relative_root]
    )
    prefix = f"{relative_root}/"
    for record in output.split(b"\0"):
        if not record:
            continue
        try:
            header, encoded_path = record.split(b"\t", 1)
            mode, object_id, stage = header.split()
        except ValueError as exception:
            raise SystemExit(
                "Git returned an invalid Game index record"
            ) from exception
        path = os.fsdecode(encoded_path)
        if stage != b"0" or not path.startswith(prefix):
            raise SystemExit(f"Git returned an invalid Game index path: {path}")
        if mode != b"100644":
            raise SystemExit(
                f"Game index mode is forbidden: {path} "
                f"({os.fsdecode(mode)})"
            )
        relative = path[len(prefix):]
        if not relative or relative in indexed:
            raise SystemExit(f"Git returned a duplicate Game index path: {path}")
        object_id_text = os.fsdecode(object_id)
        if re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", object_id_text) is None:
            raise SystemExit(f"Git returned an invalid object ID: {path}")
        indexed[relative] = object_id_text
    return indexed


def git_tree_game_files(
    game_root: Path,
    commit: str,
    expected_tree_oid: str,
) -> dict[str, str]:
    """Read an immutable historical Game closure through pinned Git."""

    if re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", commit) is None:
        raise SystemExit("Unity baseline commit identity is malformed")
    resolved_commit = pinned_git_capture(
        ["rev-parse", "--verify", f"{commit}^{{commit}}"]
    ).decode("ascii", errors="strict").strip()
    if resolved_commit != commit:
        raise SystemExit("Unity baseline commit identity drifted")

    relative_root = game_root.relative_to(ROOT).as_posix()
    resolved_tree = pinned_git_capture(
        ["rev-parse", "--verify", f"{commit}:{relative_root}"]
    ).decode("ascii", errors="strict").strip()
    if resolved_tree != expected_tree_oid:
        raise SystemExit("Unity baseline Game tree identity drifted")

    historical: dict[str, str] = {}
    output = pinned_git_capture(
        [
            "ls-tree",
            "-r",
            "-z",
            "--full-tree",
            commit,
            "--",
            relative_root,
        ]
    )
    prefix = f"{relative_root}/"
    for record in output.split(b"\0"):
        if not record:
            continue
        try:
            header, encoded_path = record.split(b"\t", 1)
            mode, object_type, object_id = header.split()
        except ValueError as exception:
            raise SystemExit(
                "Git returned an invalid historical Game tree record"
            ) from exception
        path = os.fsdecode(encoded_path)
        if (
            mode != b"100644"
            or object_type != b"blob"
            or not path.startswith(prefix)
        ):
            raise SystemExit(
                f"Git returned a forbidden historical Game path: {path}"
            )
        relative = path[len(prefix):]
        if not relative or relative in historical:
            raise SystemExit(
                f"Git returned a duplicate historical Game path: {path}"
            )
        object_id_text = os.fsdecode(object_id)
        if re.fullmatch(
            r"[0-9a-f]{40}|[0-9a-f]{64}",
            object_id_text,
        ) is None:
            raise SystemExit(
                f"Git returned an invalid historical object ID: {path}"
            )
        historical[relative] = object_id_text
    return historical


def parse_manifest_file_entries(
    entries: object,
    label: str,
) -> dict[str, tuple[int, str]]:
    if not isinstance(entries, list) or not entries:
        raise SystemExit(f"{label} has no file closure")

    expected_files: dict[str, tuple[int, str]] = {}
    casefolded_paths: set[str] = set()
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != {
            "path",
            "size",
            "sha256",
        }:
            raise SystemExit(f"{label} file entry shape drifted")
        relative = entry["path"]
        size = entry["size"]
        digest = entry["sha256"]
        if (
            not isinstance(relative, str)
            or not relative
            or relative.startswith("/")
            or "\\" in relative
            or any(ord(character) < 32 for character in relative)
            or any(
                part in ("", ".", "..")
                for part in relative.split("/")
            )
        ):
            raise SystemExit(f"Unsafe {label} path: {relative!r}")
        if (
            not isinstance(size, int)
            or isinstance(size, bool)
            or size < 0
            or not isinstance(digest, str)
            or re.fullmatch(r"[0-9a-f]{64}", digest) is None
        ):
            raise SystemExit(f"{label} file metadata drifted: {relative}")
        folded = relative.casefold()
        if relative in expected_files or folded in casefolded_paths:
            raise SystemExit(
                f"Duplicate or case-colliding {label} path: {relative}"
            )
        expected_files[relative] = (size, digest)
        casefolded_paths.add(folded)

    if list(expected_files) != sorted(expected_files):
        raise SystemExit(f"{label} file closure must be path-sorted")
    return expected_files


def validate_git_blob_closure(
    indexed_files: dict[str, str],
    expected_files: dict[str, tuple[int, str]],
    label: str,
) -> None:
    if set(indexed_files) != set(expected_files):
        raise SystemExit(
            f"{label} closure drifted: expected {sorted(expected_files)}, "
            f"received {sorted(indexed_files)}"
        )
    for relative, object_id in indexed_files.items():
        expected_size, expected_digest = expected_files[relative]
        size_text = pinned_git_capture(
            ["cat-file", "-s", object_id]
        ).decode("ascii", errors="strict").strip()
        try:
            indexed_size = int(size_text)
        except ValueError as exception:
            raise SystemExit(
                f"Git returned an invalid blob size: {relative}"
            ) from exception
        if indexed_size != expected_size:
            raise SystemExit(f"{label} size drifted: {relative}")
        contents = pinned_git_capture(["cat-file", "blob", object_id])
        if len(contents) != expected_size:
            raise SystemExit(f"{label} blob length drifted: {relative}")
        if sha256_bytes(contents) != expected_digest:
            raise SystemExit(f"{label} hash drifted: {relative}")


def validate_technical_sandbox_overlay(
    baseline_document: dict[str, object],
    baseline_files: dict[str, tuple[int, str]],
) -> tuple[
    dict[str, tuple[int, str]],
    dict[str, str],
    dict[str, object],
]:
    if sha256_file(UNITY_TECHNICAL_SANDBOX_MANIFEST) != (
        UNITY_TECHNICAL_SANDBOX_MANIFEST_SHA256
    ):
        raise SystemExit("Unity technical-sandbox manifest digest drifted")
    overlay = json.loads(
        UNITY_TECHNICAL_SANDBOX_MANIFEST.read_text(encoding="utf-8")
    )
    if not isinstance(overlay, dict) or set(overlay) != {
        "added_files",
        "asset_guids",
        "base_commit",
        "baseline_binding",
        "branch",
        "build_scene",
        "constraints",
        "created_at",
        "creator_approval",
        "manifest_id",
        "packet_id",
        "replaced_files",
        "schema_version",
        "source_root",
        "structural_meta_paths",
    }:
        raise SystemExit("Unity technical-sandbox manifest shape drifted")
    if overlay.get("schema_version") != 1:
        raise SystemExit("Unity technical-sandbox manifest schema drifted")
    if overlay.get("manifest_id") != (
        "WP0003-UNITY-TECHNICAL-SANDBOX-20260716"
    ):
        raise SystemExit("Unity technical-sandbox manifest identity drifted")
    if overlay.get("packet_id") != "WP-0003":
        raise SystemExit("Unity technical-sandbox packet drifted")
    if overlay.get("created_at") != "2026-07-17T02:24:31Z":
        raise SystemExit("Unity technical-sandbox creation time drifted")
    if overlay.get("base_commit") != (
        "f95626233b2841de9a3144d89170c245d9a1b8ac"
    ):
        raise SystemExit("Unity technical-sandbox base commit drifted")
    if overlay.get("branch") != "agent/wp0003-technical-sandbox-001":
        raise SystemExit("Unity technical-sandbox branch drifted")
    if overlay.get("creator_approval") != {
        "captured_on": "2026-07-16",
        "instructions": [
            "Are you sure you can't access it? Just opened the game",
            (
                "You can start building the game, there has been a lot of "
                "setup. Can we get this moving now?"
            ),
        ],
        "scope": (
            "one non-gameplay technical sandbox and its exact build-scene "
            "registration"
        ),
    }:
        raise SystemExit("Unity technical-sandbox approval record drifted")
    if overlay.get("baseline_binding") != {
        "manifest_path": (
            "docs/manifests/WP-0003-unity-canonical-v2.json"
        ),
        "manifest_sha256": UNITY_BASELINE_MANIFEST_SHA256,
        "commit": UNITY_BASELINE_COMMIT,
        "game_tree_oid": UNITY_BASELINE_GAME_TREE_OID,
    }:
        raise SystemExit("Unity technical-sandbox baseline binding drifted")
    if overlay.get("source_root") != TECHNICAL_SANDBOX_ROOT:
        raise SystemExit("Unity technical-sandbox source root drifted")
    structural_meta_paths = overlay.get("structural_meta_paths")
    if (
        not isinstance(structural_meta_paths, list)
        or structural_meta_paths != sorted(TECHNICAL_SANDBOX_STRUCTURAL_METAS)
    ):
        raise SystemExit("Unity technical-sandbox structural metas drifted")
    if overlay.get("constraints") != {
        "kind": "non-gameplay-technical-sandbox",
        "production_content": False,
        "package_changes": False,
        "save_bytes_written": False,
        "cloud_services_enabled": False,
    }:
        raise SystemExit("Unity technical-sandbox constraints drifted")

    added_files = parse_manifest_file_entries(
        overlay.get("added_files"),
        "Unity technical-sandbox",
    )
    if not TECHNICAL_SANDBOX_STRUCTURAL_METAS.issubset(added_files):
        raise SystemExit("Unity technical-sandbox structural metas are missing")
    for relative in added_files:
        if relative not in TECHNICAL_SANDBOX_STRUCTURAL_METAS and (
            not relative.startswith(TECHNICAL_SANDBOX_PREFIX)
        ):
            raise SystemExit(
                f"Unity technical-sandbox path escapes source root: {relative}"
            )
        if not relative.endswith(TECHNICAL_SANDBOX_ALLOWED_SUFFIXES):
            raise SystemExit(
                f"Unity technical-sandbox file type is forbidden: {relative}"
            )

    baseline_casefolded = {
        relative.casefold(): relative
        for relative in baseline_files
    }
    for relative in added_files:
        collision = baseline_casefolded.get(relative.casefold())
        if collision is not None:
            raise SystemExit(
                "Unity technical-sandbox addition collides with baseline: "
                f"{relative} and {collision}"
            )

    replacements = overlay.get("replaced_files")
    if (
        not isinstance(replacements, list)
        or len(replacements) != 1
        or not isinstance(replacements[0], dict)
        or set(replacements[0]) != {
            "baseline_sha256",
            "baseline_size",
            "path",
            "sha256",
            "size",
        }
    ):
        raise SystemExit("Unity technical-sandbox replacement shape drifted")
    replacement = replacements[0]
    if replacement.get("path") != EDITOR_BUILD_SETTINGS_PATH:
        raise SystemExit("Unity technical-sandbox replacement path drifted")
    baseline_entry = baseline_files.get(EDITOR_BUILD_SETTINGS_PATH)
    if baseline_entry is None or baseline_entry != (
        replacement.get("baseline_size"),
        replacement.get("baseline_sha256"),
    ):
        raise SystemExit(
            "Unity technical-sandbox replacement baseline drifted"
        )
    replacement_size = replacement.get("size")
    replacement_digest = replacement.get("sha256")
    if (
        not isinstance(replacement_size, int)
        or isinstance(replacement_size, bool)
        or replacement_size < 0
        or not isinstance(replacement_digest, str)
        or re.fullmatch(r"[0-9a-f]{64}", replacement_digest) is None
    ):
        raise SystemExit(
            "Unity technical-sandbox replacement metadata drifted"
        )

    overlay_guids = overlay.get("asset_guids")
    expected_meta_paths = {
        relative for relative in added_files if relative.endswith(".meta")
    }
    if (
        not isinstance(overlay_guids, dict)
        or list(overlay_guids) != sorted(overlay_guids)
        or set(overlay_guids) != expected_meta_paths
    ):
        raise SystemExit("Unity technical-sandbox GUID inventory drifted")
    baseline_guids = baseline_document.get("asset_guids")
    if not isinstance(baseline_guids, dict):
        raise SystemExit("Unity baseline GUID inventory shape drifted")
    seen_guids = set(baseline_guids.values())
    for relative, guid in overlay_guids.items():
        if (
            not isinstance(relative, str)
            or not isinstance(guid, str)
            or re.fullmatch(r"[0-9a-f]{32}", guid) is None
            or guid in seen_guids
        ):
            raise SystemExit(
                f"Invalid or duplicate technical-sandbox GUID: {relative}"
            )
        seen_guids.add(guid)

    build_scene = overlay.get("build_scene")
    expected_scene_path = (
        "Assets/AtomicLandPirate/TechnicalSandbox/Scenes/"
        "WP0003_TechnicalSandbox.unity"
    )
    if (
        not isinstance(build_scene, dict)
        or set(build_scene) != {"enabled", "guid", "path"}
        or build_scene.get("enabled") is not True
        or build_scene.get("path") != expected_scene_path
        or build_scene.get("path") not in added_files
        or build_scene.get("guid") != overlay_guids.get(
            f"{expected_scene_path}.meta"
        )
    ):
        raise SystemExit("Unity technical-sandbox build scene drifted")

    current_files = dict(baseline_files)
    current_files[EDITOR_BUILD_SETTINGS_PATH] = (
        replacement_size,
        replacement_digest,
    )
    current_files.update(added_files)
    return current_files, overlay_guids, build_scene


def validate_editor_build_settings(
    text: str,
    build_scene: dict[str, object],
) -> None:
    expected = (
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!1045 &1\n"
        "EditorBuildSettings:\n"
        "  m_ObjectHideFlags: 0\n"
        "  serializedVersion: 2\n"
        "  m_Scenes:\n"
        "  - enabled: 1\n"
        f"    path: {build_scene['path']}\n"
        f"    guid: {build_scene['guid']}\n"
        "  m_configObjects: {}\n"
    )
    if text != expected:
        raise SystemExit("Game technical-sandbox build settings drifted")


def validate_game_baseline() -> None:
    if sha256_file(UNITY_BASELINE_MANIFEST) != (
        UNITY_BASELINE_MANIFEST_SHA256
    ):
        raise SystemExit("Unity baseline manifest digest drifted")
    document = json.loads(
        UNITY_BASELINE_MANIFEST.read_text(encoding="utf-8")
    )
    expected_top_level_keys = {
        "asset_guids",
        "base_commit",
        "branch",
        "created_at",
        "creator_approval",
        "direct_packages",
        "first_import_observation",
        "game_files",
        "import_policy",
        "manifest_id",
        "package_lock",
        "packet_id",
        "project_identity",
        "preserved_deviations",
        "resolved_package_count",
        "rollback",
        "schema_version",
        "source_donor",
        "unity_boundary",
    }
    if set(document) != expected_top_level_keys:
        raise SystemExit("Unity baseline manifest top-level shape drifted")
    if document.get("schema_version") != 1:
        raise SystemExit("Unity baseline manifest schema drifted")
    if document.get("manifest_id") != (
        "WP0003-UNITY-CANONICAL-FIRST-IMPORT-20260716"
    ):
        raise SystemExit("Unity baseline manifest identity drifted")
    if document.get("packet_id") != "WP-0003":
        raise SystemExit("Unity baseline manifest packet drifted")
    if document.get("base_commit") != (
        "522c57835214ec621ba1864889d268abd378ccc3"
    ):
        raise SystemExit("Unity baseline manifest base commit drifted")
    if document.get("created_at") != "2026-07-16T23:44:14Z":
        raise SystemExit("Unity baseline manifest creation time drifted")
    if document.get("branch") != (
        "agent/wp0003-first-import-handoff-001"
    ):
        raise SystemExit("Unity baseline manifest branch drifted")

    approval = document.get("creator_approval")
    if approval != {
        "captured_on": "2026-07-16",
        "instruction": "ok we are approved lets keep building",
        "scope": (
            "canonical first import, bounded handoff hygiene, validation, "
            "and protected pull request; no gameplay or production content"
        ),
    }:
        raise SystemExit("Unity dependency approval record drifted")

    expected_source_donor = {
        "repository_url": "https://github.com/AC-21/Sashas.git",
        "commit": "496c60b978741c03c476860ab83d1aadc215c961",
        "commit_tree_oid": (
            "da74bb0ccee61bd9b38abb09e67bc8e9eb4ef42e"
        ),
        "snapshot_source_path": "/Users/sasha/Sasha the Atomic Land Pirate",
        "path_observation": (
            "The same path existed at the final pre-commit provenance check; "
            "local paths are observational and non-authoritative."
        ),
        "editor_version": "6000.5.4f1",
        "editor_revision": "d550df8bd089",
        "live_editor_open_during_audit": True,
        "import_method": (
            "immutable git archive plus a twice-hash-matched selected dirty "
            "patch; no live project file was copied directly"
        ),
        "snapshot_archive": {
            "format": "git archive tar",
            "command": (
                "git archive --format=tar --output=<OUTPUT> "
                "496c60b978741c03c476860ab83d1aadc215c961 -- "
                "Assets/InputSystem_Actions.inputactions "
                "Assets/InputSystem_Actions.inputactions.meta "
                "Assets/Settings.meta Assets/Settings Packages "
                "ProjectSettings"
            ),
            "paths": [
                "Assets/InputSystem_Actions.inputactions",
                "Assets/InputSystem_Actions.inputactions.meta",
                "Assets/Settings.meta",
                "Assets/Settings",
                "Packages",
                "ProjectSettings",
            ],
            "size_bytes": 225280,
            "sha256": (
                "a9d40bbf99125faf6b7c4abe09844d64acdc6d3ead38fe562e9c9569c48037ec"
            ),
        },
        "whole_worktree_diff_sha256": (
            "85752c18d7645dd5fccbbea53aeb05df3d80a0ef4504622748d2f736888c8fac"
        ),
        "selected_worktree_patch_sha256": (
            "e8c6050453fa60825a1e2695227d540a587e528adbcff94f3989fe8f1ff75a5c"
        ),
        "source_manifest_sha256": (
            "bc0a9207b56a53b5dc00045eb057dcf10bec4558bec7ae97039b3c56ae1d2e59"
        ),
        "source_lock_sha256": (
            "6a97aea36ac4663cdfa912cf7067a9d98d3c9d9291cffce285a9ca391011bd00"
        ),
        "selected_patch_paths": [
            "Assets/Settings/Mobile_RPAsset.asset",
            "Assets/Settings/PC_RPAsset.asset",
            "ProjectSettings/PackageManagerSettings.asset",
            "ProjectSettings/Packages/com.unity.ai.assistant/Settings.json",
            "ProjectSettings/ProjectSettings.asset",
            "ProjectSettings/ShaderGraphSettings.asset",
            "ProjectSettings/URPProjectSettings.asset",
        ],
    }
    if document.get("source_donor") != expected_source_donor:
        raise SystemExit("Unity baseline source provenance drifted")
    import_policy = document.get("import_policy")
    if not isinstance(import_policy, dict) or set(import_policy) != {
        "excluded",
        "kept",
        "kind",
        "normalizations",
    }:
        raise SystemExit("Unity baseline import policy shape drifted")
    if import_policy.get("kind") != "canonical first-import baseline" or any(
        not isinstance(import_policy.get(key), list)
        or not import_policy[key]
        or not all(isinstance(value, str) and value for value in import_policy[key])
        for key in ("excluded", "kept", "normalizations")
    ):
        raise SystemExit("Unity baseline import policy drifted")

    game_root = ROOT / "Game"
    baseline_files = parse_manifest_file_entries(
        document.get("game_files"),
        "Unity baseline",
    )
    historical_files = git_tree_game_files(
        game_root,
        UNITY_BASELINE_COMMIT,
        UNITY_BASELINE_GAME_TREE_OID,
    )
    validate_git_blob_closure(
        historical_files,
        baseline_files,
        "Historical Unity baseline Git tree",
    )
    expected_files, overlay_guids, build_scene = (
        validate_technical_sandbox_overlay(document, baseline_files)
    )
    indexed_files = git_index_game_files(game_root)
    validate_git_blob_closure(
        indexed_files,
        expected_files,
        "Game Git index",
    )

    forbidden_components = {
        ".git",
        ".idea",
        ".vscode",
        "builds",
        "library",
        "logs",
        "memorycaptures",
        "obj",
        "recordings",
        "temp",
        "usersettings",
    }
    forbidden_suffixes = (
        ".csproj",
        ".sln",
        ".slnx",
        ".suo",
        ".user",
    )
    allowed_generated_roots = {
        "Builds",
        "builds",
        "Library",
        "library",
        "Logs",
        "logs",
        "MemoryCaptures",
        "memoryCaptures",
        "Obj",
        "obj",
        "Recordings",
        "recordings",
        "Temp",
        "temp",
        "UserSettings",
        "Usersettings",
        "userSettings",
        "usersettings",
    }
    allowed_local_files = {
        "ProjectSettings/Packages/com.unity.ai.assistant/Settings.json",
    }
    actual_files: dict[str, Path] = {}
    if game_root.is_symlink():
        raise SystemExit("Game may not be a symlink")

    def fail_game_walk(exception: OSError) -> None:
        raise SystemExit(
            f"Could not traverse Game: {exception}"
        ) from exception

    for current_root, directory_names, file_names in os.walk(
        game_root,
        topdown=True,
        onerror=fail_game_walk,
        followlinks=False,
    ):
        current = Path(current_root)
        retained_directories: list[str] = []
        for name in directory_names:
            directory = current / name
            relative = directory.relative_to(game_root).as_posix()
            if directory.is_symlink():
                raise SystemExit(
                    f"{directory.relative_to(ROOT)} may not be a symlink"
                )
            relative_parts = relative.split("/")
            lowered_parts = {part.casefold() for part in relative_parts}
            if (
                len(relative_parts) == 1
                and relative_parts[0] in allowed_generated_roots
            ):
                continue
            if lowered_parts & forbidden_components:
                raise SystemExit(
                    f"Forbidden Unity-generated path: {relative}"
                )
            retained_directories.append(name)
        directory_names[:] = retained_directories

        for name in file_names:
            path = current / name
            relative = path.relative_to(game_root).as_posix()
            if path.is_symlink():
                raise SystemExit(
                    f"{path.relative_to(ROOT)} may not be a symlink"
                )
            if not path.is_file():
                raise SystemExit(
                    f"Game path is not a regular file: "
                    f"{path.relative_to(ROOT)}"
                )
            if relative in allowed_local_files:
                continue
            lowered_parts = {
                part.casefold() for part in relative.split("/")
            }
            if lowered_parts & forbidden_components:
                raise SystemExit(
                    f"Forbidden Unity-generated path: {relative}"
                )
            if relative.casefold().endswith(forbidden_suffixes):
                raise SystemExit(
                    f"Forbidden Unity-generated file: {relative}"
                )
            actual_files[relative] = path

    if set(actual_files) != set(expected_files):
        raise SystemExit(
            "Game file closure drifted: "
            f"expected {sorted(expected_files)}, "
            f"received {sorted(actual_files)}"
        )
    for relative, path in actual_files.items():
        expected_size, expected_digest = expected_files[relative]
        if path.stat().st_size != expected_size:
            raise SystemExit(f"Game file size drifted: {relative}")
        if sha256_file(path) != expected_digest:
            raise SystemExit(f"Game file hash drifted: {relative}")

    assets = game_root / "Assets"
    meta_guids: dict[str, str] = {}
    seen_guids: set[str] = set()
    for path in assets.rglob("*.meta"):
        relative = path.relative_to(game_root).as_posix()
        paired_asset = path.with_suffix("")
        if not paired_asset.exists():
            raise SystemExit(
                f"Unity meta lacks an asset pair: {path.relative_to(ROOT)}"
            )
        match = re.search(
            r"(?m)^guid: ([0-9a-f]{32})$",
            path.read_text(encoding="utf-8"),
        )
        if match is None or match.group(1) in seen_guids:
            raise SystemExit(f"Invalid or duplicate Unity meta GUID: {relative}")
        meta_guids[relative] = match.group(1)
        seen_guids.add(match.group(1))
    baseline_guids = document.get("asset_guids")
    if not isinstance(baseline_guids, dict):
        raise SystemExit("Unity baseline GUID inventory shape drifted")
    expected_guids = {**baseline_guids, **overlay_guids}
    if meta_guids != expected_guids:
        raise SystemExit("Unity asset GUID inventory drifted")

    for path in assets.rglob("*"):
        if path.is_file() and path.suffix != ".meta":
            meta = path.with_name(path.name + ".meta")
            if not meta.is_file():
                raise SystemExit(
                    f"Unity asset lacks a meta pair: {path.relative_to(ROOT)}"
                )
        elif path.is_dir() and path != assets:
            meta = path.parent / f"{path.name}.meta"
            if not meta.is_file():
                raise SystemExit(
                    f"Unity asset directory lacks meta: {path.relative_to(ROOT)}"
                )

    forbidden_text = (
        "/Users/",
        "Assets/Scenes/SampleScene.unity",
        "DefaultCompany",
        "HubForceResolve",
        "SENTIS_ANALYTICS_ENABLED",
        "Sashas",
        "TutorialInfo",
        "com.unity.template",
        "7b2d9957599624383ba6e24cb9acacb4",
        "b2f6f654-8c39-4360-bc5e-26a62e50e159",
        "unity_2d2aeb94bdf989c70701",
        "3c72c65a16f0acb438eed22b8b16c24a",
        "0x01004b9000490000",
        "ED1633-NPXX51362_00-0000000000000000",
        "frAQBc8Wsa1xVPfvJcrgRYwTiizs2trQ",
        "052faaac586de48259a63d0c4782560b",
        "5e6cbd92db86f4b18aec3ed561671858",
        "10fc4df2da32a41aaa32d77bc913491c",
        "ab09877e2e707104187f6f83e2f62510",
        "e5c6678ed2aaa91408dd3df699057aae",
        "03cfc4915c15d504a9ed85ecc404e607",
        "53a11f4ebaebf4049b3638ef78dc9664",
        "8f96cd657dc40064aa21efcc7e50a2e7",
        "57d7c4c16e2765b47a4d2069b311bffe",
        "24ec0e140fb444a44ab96ee80844e18e",
        "b9a23f869c4fd45f19c5ada54dd82176",
    )
    for relative, path in actual_files.items():
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError as exception:
            raise SystemExit(
                f"Unity baseline must remain text-inspectable: {relative}"
            ) from exception
        for forbidden in forbidden_text:
            if forbidden in text:
                raise SystemExit(
                    f"Unity baseline retained forbidden donor text "
                    f"{forbidden!r} in {relative}"
                )

    game_manifest = json.loads(
        (game_root / "Packages/manifest.json").read_text(encoding="utf-8")
    )
    direct_packages = document.get("direct_packages")
    if direct_packages != EXPECTED_DIRECT_PACKAGES:
        raise SystemExit("Unity approved direct package set drifted")
    if game_manifest != {"dependencies": EXPECTED_DIRECT_PACKAGES}:
        raise SystemExit("Game direct package graph drifted")
    for name, version in direct_packages.items():
        if PACKAGE_NAME_PATTERN.fullmatch(name) is None:
            raise SystemExit(f"Game direct package name drifted: {name!r}")
        if SEMVER_PATTERN.fullmatch(version) is None:
            raise SystemExit(f"Game direct package version drifted: {name}")
    direct_packages_digest = canonical_json_sha256(direct_packages)
    if direct_packages_digest != EXPECTED_DIRECT_PACKAGES_SHA256:
        raise SystemExit("Game direct package digest drifted")
    prerelease_packages = {
        name: version
        for name, version in direct_packages.items()
        if "-" in version
    }
    if prerelease_packages != {
        "com.unity.ai.assistant": "2.14.0-pre.1"
    }:
        raise SystemExit("Game prerelease package allowlist drifted")

    package_attestation = document.get("package_lock")
    if package_attestation != {
        "sha256": EXPECTED_PACKAGE_LOCK_SHA256,
        "direct_packages_canonical_sha256": (
            EXPECTED_DIRECT_PACKAGES_SHA256
        ),
        "provenance": (
            "shortest reachable closure filtered from the donor lock and "
            "resolved byte-identically by the canonical Editor first import"
        ),
        "allowed_sources": ["builtin", "https://packages.unity.com"],
        "local_packages_linked": False,
    }:
        raise SystemExit("Game package attestation drifted")

    package_manager_settings = (
        game_root / "ProjectSettings/PackageManagerSettings.asset"
    ).read_text(encoding="utf-8")
    if package_manager_settings.splitlines().count("  m_MainRegistry:") != 1:
        raise SystemExit("Unity Package Manager main registry shape drifted")
    if require_unique_yaml_scalar(
        package_manager_settings,
        "m_Url",
        4,
        "Unity Package Manager settings",
    ) != "https://packages.unity.com":
        raise SystemExit("Unity Package Manager main registry drifted")
    if require_unique_yaml_scalar(
        package_manager_settings,
        "m_ScopedRegistries",
        2,
        "Unity Package Manager settings",
    ) != "[]":
        raise SystemExit("Unity Package Manager gained a scoped registry")
    if require_unique_yaml_scalar(
        package_manager_settings,
        "m_EnablePreReleasePackages",
        2,
        "Unity Package Manager settings",
    ) != "0":
        raise SystemExit("Unity Package Manager prerelease setting drifted")

    package_lock_path = game_root / "Packages/packages-lock.json"
    if sha256_file(package_lock_path) != EXPECTED_PACKAGE_LOCK_SHA256:
        raise SystemExit("Game package lock digest drifted")
    package_lock = json.loads(package_lock_path.read_text(encoding="utf-8"))
    if set(package_lock) != {"dependencies"}:
        raise SystemExit("Game package lock top-level shape drifted")
    locked = package_lock.get("dependencies")
    if not isinstance(locked, dict):
        raise SystemExit("Game package lock shape drifted")
    if document.get("resolved_package_count") != 34 or len(locked) != 34:
        raise SystemExit("Game resolved package count drifted")

    for name, entry in locked.items():
        if PACKAGE_NAME_PATTERN.fullmatch(name) is None:
            raise SystemExit(f"Locked package name drifted: {name!r}")
        if not isinstance(entry, dict):
            raise SystemExit(f"Locked package entry drifted: {name}")
        source = entry.get("source")
        expected_keys = {"dependencies", "depth", "source", "version"}
        if source == "registry":
            expected_keys.add("url")
        if set(entry) != expected_keys:
            raise SystemExit(f"Locked package entry shape drifted: {name}")
        version = entry.get("version")
        dependencies = entry.get("dependencies")
        depth = entry.get("depth")
        if not isinstance(version, str) or SEMVER_PATTERN.fullmatch(version) is None:
            raise SystemExit(f"Locked package version drifted: {name}")
        if not isinstance(depth, int) or isinstance(depth, bool) or depth < 0:
            raise SystemExit(f"Locked package depth type drifted: {name}")
        if not isinstance(dependencies, dict):
            raise SystemExit(f"Package dependency map drifted: {name}")
        for dependency, required_version in dependencies.items():
            if (
                not isinstance(dependency, str)
                or PACKAGE_NAME_PATTERN.fullmatch(dependency) is None
                or not isinstance(required_version, str)
                or SEMVER_PATTERN.fullmatch(required_version) is None
            ):
                raise SystemExit(
                    f"Locked package dependency shape drifted: {name}"
                )
        if source not in ("builtin", "registry"):
            raise SystemExit(f"Locked package source is forbidden: {name}")
        if source == "registry" and entry.get("url") != (
            "https://packages.unity.com"
        ):
            raise SystemExit(f"Locked package registry drifted: {name}")

    direct_locked = {
        name: entry.get("version")
        for name, entry in locked.items()
        if entry.get("depth") == 0
    }
    if direct_locked != direct_packages:
        raise SystemExit("Game package lock direct graph drifted")

    depths = {name: 0 for name in direct_packages}
    queue: deque[str] = deque(sorted(direct_packages))
    while queue:
        package = queue.popleft()
        dependencies = locked[package]["dependencies"]
        for dependency, required_version in dependencies.items():
            if dependency not in locked:
                raise SystemExit(
                    f"Locked package dependency is missing: {dependency}"
                )
            resolved_version = locked[dependency]["version"]
            if semver_core(resolved_version) < semver_core(required_version) or (
                semver_core(resolved_version) == semver_core(required_version)
                and "-" not in required_version
                and "-" in resolved_version
            ):
                raise SystemExit(
                    f"Locked package dependency version is incompatible: "
                    f"{package} requires {dependency} {required_version}, "
                    f"resolved {resolved_version}"
                )
            candidate_depth = depths[package] + 1
            if dependency not in depths or candidate_depth < depths[dependency]:
                depths[dependency] = candidate_depth
                queue.append(dependency)
    if set(depths) != set(locked):
        raise SystemExit("Game package lock contains unreachable packages")

    for name, entry in locked.items():
        if entry.get("depth") != depths[name]:
            raise SystemExit(f"Locked package depth drifted: {name}")

    project_version = (
        game_root / "ProjectSettings/ProjectVersion.txt"
    ).read_text(encoding="utf-8")
    expected_project_version = (
        "m_EditorVersion: 6000.5.4f1\n"
        "m_EditorVersionWithRevision: 6000.5.4f1 (d550df8bd089)\n"
    )
    if project_version != expected_project_version:
        raise SystemExit("Game project version drifted")

    player_settings = (
        game_root / "ProjectSettings/ProjectSettings.asset"
    ).read_text(encoding="utf-8")
    identity = document.get("project_identity")
    if identity != EXPECTED_PROJECT_IDENTITY:
        raise SystemExit("Unity project identity attestation drifted")
    expected_player_scalars = {
        "companyName": identity["company_name"],
        "productName": identity["product_name"],
        "productGUID": identity["product_guid"],
        "cloudProjectId": identity["cloud_project_id"],
        "projectName": identity["project_name"],
        "organizationId": identity["organization_id"],
        "cloudEnabled": "0",
        "activeInputHandler": "1",
        "m_ActiveColorSpace": "1",
        "submitAnalytics": "0",
        "cloudServicesEnabled": "{}",
        "scriptingDefineSymbols": "{}",
        "clonedFromGUID": "",
        "switchApplicationID": "0x0000000000000000",
        "ps4ContentID": "",
        "ps4Passcode": "",
    }
    for key, expected_value in expected_player_scalars.items():
        if require_unique_yaml_scalar(
            player_settings,
            key,
            2,
            "Unity Player settings",
        ) != expected_value:
            raise SystemExit(f"Game Player setting drifted: {key}")
    application_identifiers = require_unique_yaml_map(
        player_settings,
        "applicationIdentifier",
        2,
        4,
        "Unity Player settings",
    )
    if application_identifiers != {
        "Android": identity["bundle_identifier"],
        "Standalone": identity["bundle_identifier"],
        "iPhone": identity["bundle_identifier"],
    }:
        raise SystemExit("Game application identifiers drifted")

    editor_settings = (
        game_root / "ProjectSettings/EditorSettings.asset"
    ).read_text(encoding="utf-8")
    for key, expected_value in {
        "m_SerializationMode": "2",
        "m_CacheServerMode": "0",
        "m_CacheServerEnableDownload": "0",
        "m_CacheServerEnableUpload": "0",
    }.items():
        if require_unique_yaml_scalar(
            editor_settings,
            key,
            2,
            "Unity Editor settings",
        ) != expected_value:
            raise SystemExit(f"Game Editor setting drifted: {key}")

    version_control = (
        game_root / "ProjectSettings/VersionControlSettings.asset"
    ).read_text(encoding="utf-8")
    if require_unique_yaml_scalar(
        version_control,
        "m_Mode",
        2,
        "Unity version-control settings",
    ) != "Visible Meta Files" or require_unique_yaml_scalar(
        version_control,
        "inProgressEnabled",
        4,
        "Unity version-control settings",
    ) != "0":
        raise SystemExit("Game version-control settings drifted")

    connect_settings = (
        game_root / "ProjectSettings/UnityConnectSettings.asset"
    ).read_text(encoding="utf-8")
    expected_connect_line_counts = {
        "  m_Enabled: 0": 1,
        "  m_TestMode: 0": 1,
        "    m_EngineDiagnosticsEnabled: 0": 1,
        "    m_EnableCloudDiagnosticsReporting: 0": 1,
        "    m_Enabled: 0": 5,
        "    m_TestMode: 0": 3,
        "    m_InitializeOnStartup: 0": 2,
    }
    connect_lines = connect_settings.splitlines()
    for line, expected_count in expected_connect_line_counts.items():
        if connect_lines.count(line) != expected_count:
            raise SystemExit(
                f"Game cloud-service setting drifted: {line.strip()}"
            )
    service_scalars = re.findall(
        r"(?m)^[ ]+m_(?:Enabled|TestMode|InitializeOnStartup|"
        r"EngineDiagnosticsEnabled|EnableCloudDiagnosticsReporting):"
        r"[ \t]*(.*)$",
        connect_settings,
    )
    if len(service_scalars) != 14 or any(
        value.strip() != "0" for value in service_scalars
    ):
        raise SystemExit("Game cloud service unexpectedly enabled")

    build_settings = (
        game_root / "ProjectSettings/EditorBuildSettings.asset"
    ).read_text(encoding="utf-8")
    validate_editor_build_settings(build_settings, build_scene)

    required_references = {
        "Assets/Settings/PC_RPAsset.asset": (
            "f288ae1f4751b564a96ac7587541f7a2",
        ),
        "ProjectSettings/GraphicsSettings.asset": (
            "4b83569d67af61e458304325a23e5dfd",
            "18dc0cd2c080841dea60987a38ce93fa",
        ),
        "ProjectSettings/QualitySettings.asset": (
            "4b83569d67af61e458304325a23e5dfd",
        ),
    }
    for relative, guids in required_references.items():
        text = (game_root / relative).read_text(encoding="utf-8")
        for guid in guids:
            if guid not in text:
                raise SystemExit(
                    f"Unity GUID closure drifted: {relative} lacks {guid}"
                )

    renderer_settings = (
        game_root / "Assets/Settings/PC_Renderer.asset"
    ).read_text(encoding="utf-8")
    if require_unique_yaml_scalar(
        renderer_settings,
        "m_AssetVersion",
        2,
        "Unity PC renderer",
    ) != "3":
        raise SystemExit("Unity PC renderer asset version drifted")
    if require_unique_yaml_map(
        renderer_settings,
        "m_PrepassLayerMask",
        2,
        4,
        "Unity PC renderer",
    ) != {
        "serializedVersion": "2",
        "m_Bits": "4294967295",
    }:
        raise SystemExit("Unity PC renderer migration state drifted")
    if "probeVolumeResources:" in renderer_settings or (
        "m_UseNativeRenderPass:" in renderer_settings
    ):
        raise SystemExit("Unity PC renderer retained obsolete fields")

    local_ignore = (game_root / ".gitignore").read_text(encoding="utf-8")
    if "/*.slnx" not in local_ignore:
        raise SystemExit("Game local ignore lacks Unity .slnx coverage")
    if (
        "/ProjectSettings/Packages/com.unity.ai.assistant/Settings.json"
        not in local_ignore
    ):
        raise SystemExit(
            "Game local ignore lacks exact Assistant settings coverage"
        )
    local_attributes = (
        game_root / ".gitattributes"
    ).read_text(encoding="utf-8")
    if local_attributes != (
        "# Unity serializes empty YAML scalar values with one trailing space.\n"
        "**/*.asset text eol=lf whitespace=-blank-at-eol\n"
        "**/*.meta text eol=lf whitespace=-blank-at-eol\n"
    ):
        raise SystemExit("Game Unity YAML Git attributes drifted")

    if document.get("first_import_observation") != {
        "captured_at": "2026-07-16T23:30:07Z",
        "screenshot": {
            "path": (
                "docs/evidence/WP-0003/"
                "first-import-console-zero-20260716.jpeg"
            ),
            "size_bytes": 62969,
            "sha256": (
                "7964759028c75dfa25ae606d88ea322cfacf4bece128f0e365e83246161a6d61"
            ),
            "console_counters": {
                "logs": 0,
                "warnings": 0,
                "errors": 0,
            },
            "observation_window_seconds": 12,
            "duration_source": (
                "manual operator observation; screenshot corroborates only "
                "the final instant"
            ),
            "untitled_scene": (
                "unsaved in-memory Unity default; no scene asset created"
            ),
        },
        "editor_log": {
            "path": "Game/Logs/Editor.log",
            "ignored": True,
            "point_in_time_sha256": (
                "9005819fe04afc5afa813c0e6a69366e216a7d88f122ff110b1a0441b299f7e5"
            ),
            "raw_snapshot_retained": False,
            "last_csharp_error_line": 21731,
            "final_tundra_success_line": 22230,
            "final_assembly_reload_line": 22248,
        },
        "bridge": {
            "receipt_path": (
                "/Users/sasha/.unity/mcp/connections/"
                "bridge-81b1b2ca-32476.json"
            ),
            "project_path": (
                "/Users/sasha/Documents/Codex/"
                "sasha-the-land-pirate/Game"
            ),
            "connection_type": "named_pipe",
            "protocol_version": "2.0",
            "editor_pid": 32476,
            "connection_path": "/tmp/unity-mcp-81b1b2ca-32476",
            "status": "historical-stale-after-host-restart",
            "reuse_allowed": False,
        },
        "package_cache": {
            "resolved_packages": 34,
            "canonical_matches_donor": 34,
            "comparison": "byte-for-byte directory-tree comparison",
            "candidate_artifact_retained": False,
        },
    }:
        raise SystemExit("Unity first-import observation drifted")

    screenshot = (
        ROOT
        / document["first_import_observation"]["screenshot"]["path"]
    )
    if screenshot.is_symlink() or not screenshot.is_file():
        raise SystemExit("Unity first-import screenshot is missing or unsafe")
    if screenshot.stat().st_size != 62969:
        raise SystemExit("Unity first-import screenshot size drifted")
    if sha256_file(screenshot) != (
        "7964759028c75dfa25ae606d88ea322cfacf4bece128f0e365e83246161a6d61"
    ):
        raise SystemExit("Unity first-import screenshot digest drifted")

    if document.get("preserved_deviations") != [
        {
            "id": "DEV-WP0003-AGENT-HUB-UI-20260716",
            "status": "creator-directed-and-preserved",
            "creator_directions": [
                (
                    "close the donor project, open the canonical repository's "
                    "Game folder, and validate Unity's first import before "
                    "beginning implementation"
                ),
                "ok we are approved lets keep building",
            ],
            "action": (
                "An agent drove the creator-approved Unity Hub UI and "
                "indirectly caused the Editor to launch the canonical Game "
                "project."
            ),
            "direct_executable_cli_batchmode_or_mcp_call": False,
            "packet_conflict": (
                "WP-0003 names a creator-opened project and denies direct "
                "agent Unity invocation; this indirect UI operation is "
                "conservatively retained as a deviation and cannot satisfy "
                "the first-use creator-opened precondition."
            ),
            "effect": (
                "First-use gate remains closed pending a canonical-root Codex "
                "task, exact target reconfirmation, and fresh creator approval "
                "of the connection and requested action."
            ),
        }
    ]:
        raise SystemExit("Unity preserved-deviation record drifted")

    if document.get("unity_boundary") != {
        "hub_invoked_by_agent": True,
        "editor_invoked_by_agent": True,
        "relay_invoked_by_agent": False,
        "mcp_invoked_by_agent": False,
        "canonical_project_opened": True,
        "first_use_gate": "closed-pending-canonical-task-mcp",
        "compile_observed": True,
        "compile_acceptance_test_satisfied": False,
        "editmode_verified": False,
        "playmode_verified": False,
    }:
        raise SystemExit("Unity execution boundary attestation drifted")
    if document.get("rollback") != {
        "method": "ordinary Git revert or branch deletion",
        "target_commit": "522c57835214ec621ba1864889d268abd378ccc3",
        "donor_modified": False,
    }:
        raise SystemExit("Unity baseline rollback attestation drifted")


def reject_patterns(
    paths: list[Path],
    patterns: tuple[str, ...],
    boundary_name: str,
) -> None:
    for path in paths:
        text = path.read_text(encoding="utf-8")
        for pattern in patterns:
            if re.search(pattern, text):
                raise SystemExit(
                    f"{path.relative_to(ROOT)} crosses {boundary_name}: "
                    f"{pattern}"
                )


def validate_source_boundaries() -> None:
    forbidden_root_build_inputs = (
        ROOT / "Directory.Build.props",
        ROOT / "Directory.Build.targets",
        ROOT / "Directory.Packages.props",
        ROOT / "Directory.Build.rsp",
        ROOT / "MSBuild.rsp",
        ROOT / "global.json",
        ROOT / "NuGet.Config",
        ROOT / "nuget.config",
    )
    for path in forbidden_root_build_inputs:
        if path.exists():
            raise SystemExit(
                f"Unexpected root build input: {path.relative_to(ROOT)}"
            )

    validate_file_closure(
        ROOT / "SimulationCore",
        {
            "AtomicLandPirate.SimulationCore.csproj",
            "AtomicLandPirate.SimulationCore.csproj.meta",
            "Directory.Build.props",
            "Directory.Build.props.meta",
            "Directory.Build.targets",
            "Directory.Build.targets.meta",
            "README.md",
            "README.md.meta",
            "Runtime.meta",
            "Runtime/AC21.Sasha.SimulationCore.asmdef",
            "Runtime/AC21.Sasha.SimulationCore.asmdef.meta",
            "Runtime/AssemblyInfo.cs",
            "Runtime/AssemblyInfo.cs.meta",
            "Runtime/CanonicalState.cs",
            "Runtime/CanonicalState.cs.meta",
            "Runtime/SimulationKernel.cs",
            "Runtime/SimulationKernel.cs.meta",
            "Runtime/TechnicalCommand.cs",
            "Runtime/TechnicalCommand.cs.meta",
            "Runtime/TechnicalDeltaApplied.cs",
            "Runtime/TechnicalDeltaApplied.cs.meta",
            "Runtime/TechnicalReadModel.cs",
            "Runtime/TechnicalReadModel.cs.meta",
            "Runtime/TechnicalRunResult.cs",
            "Runtime/TechnicalRunResult.cs.meta",
            "Runtime/TechnicalState.cs",
            "Runtime/TechnicalState.cs.meta",
            "Runtime/TechnicalTransition.cs",
            "Runtime/TechnicalTransition.cs.meta",
            "package.json",
            "package.json.meta",
        },
    )
    validate_file_closure(
        ROOT / "SaveContracts",
        {
            "AtomicLandPirate.SaveContracts.csproj",
            "AtomicLandPirate.SaveContracts.csproj.meta",
            "Directory.Build.props",
            "Directory.Build.props.meta",
            "Directory.Build.targets",
            "Directory.Build.targets.meta",
            "README.md",
            "README.md.meta",
            "Runtime.meta",
            "Runtime/AC21.Sasha.SaveContracts.asmdef",
            "Runtime/AC21.Sasha.SaveContracts.asmdef.meta",
            "Runtime/ISavePort.cs",
            "Runtime/ISavePort.cs.meta",
            "Runtime/NonPersistingSavePort.cs",
            "Runtime/NonPersistingSavePort.cs.meta",
            "Runtime/SaveAttemptResult.cs",
            "Runtime/SaveAttemptResult.cs.meta",
            "Runtime/SaveCapability.cs",
            "Runtime/SaveCapability.cs.meta",
            "package.json",
            "package.json.meta",
        },
    )
    validate_file_closure(
        ROOT / "Tests",
        {
            "AtomicLandPirate.CoreTests/"
            "AtomicLandPirate.CoreTests.csproj",
            "AtomicLandPirate.CoreTests/Directory.Build.props",
            "AtomicLandPirate.CoreTests/Directory.Build.targets",
            "AtomicLandPirate.CoreTests/Program.cs",
            "global.json",
        },
    )
    validate_file_closure(
        ROOT / "Tools",
        {
            "nuget-offline.config",
            "run_wp0003_core_tests.py",
            # WP-0002 Stage A adds this exact fail-closed control plane.
            "Validation/collect_wp0002_scope_capture.py",
            "Validation/validate_wp0002_entry_gate.py",
            "Validation/validate_wp0002_package_graph.py",
            "Validation/validate_wp0002_policy.py",
        },
    )
    validate_game_baseline()
    validate_global_json()
    validate_nuget_config()
    validate_empty_targets(
        ROOT / "SimulationCore/Directory.Build.targets"
    )
    validate_empty_targets(
        ROOT / "SaveContracts/Directory.Build.targets"
    )
    validate_empty_targets(
        ROOT / "Tests/AtomicLandPirate.CoreTests/Directory.Build.targets"
    )

    validate_output_props(
        ROOT / "SimulationCore/Directory.Build.props",
        (
            "$(MSBuildThisFileDirectory)../BuildArtifacts/"
            "WP-0003/local-only/dotnet/SimulationCore/"
        ),
    )
    validate_output_props(
        ROOT / "SaveContracts/Directory.Build.props",
        (
            "$(MSBuildThisFileDirectory)../BuildArtifacts/"
            "WP-0003/local-only/dotnet/SaveContracts/"
        ),
    )
    validate_output_props(
        ROOT / "Tests/AtomicLandPirate.CoreTests/Directory.Build.props",
        (
            "$(MSBuildThisFileDirectory)../../BuildArtifacts/"
            "WP-0003/local-only/dotnet/CoreTests/"
        ),
    )

    package_roots = (
        ROOT / "SimulationCore",
        ROOT / "SaveContracts",
        ROOT / "Tests/AtomicLandPirate.CoreTests",
    )
    for package_root in package_roots:
        for generated_name in ("bin", "obj"):
            generated_path = package_root / generated_name
            if generated_path.exists():
                raise SystemExit(
                    f"{generated_path.relative_to(ROOT)} must remain outside "
                    "the source/package tree"
                )

    simulation_files = validate_source_set(
        ROOT / "SimulationCore",
        {
            "Runtime/AssemblyInfo.cs",
            "Runtime/CanonicalState.cs",
            "Runtime/SimulationKernel.cs",
            "Runtime/TechnicalCommand.cs",
            "Runtime/TechnicalDeltaApplied.cs",
            "Runtime/TechnicalReadModel.cs",
            "Runtime/TechnicalRunResult.cs",
            "Runtime/TechnicalState.cs",
            "Runtime/TechnicalTransition.cs",
        },
    )
    save_files = validate_source_set(
        ROOT / "SaveContracts",
        {
            "Runtime/ISavePort.cs",
            "Runtime/NonPersistingSavePort.cs",
            "Runtime/SaveAttemptResult.cs",
            "Runtime/SaveCapability.cs",
        },
    )
    validate_source_set(
        ROOT / "Tests/AtomicLandPirate.CoreTests",
        {"Program.cs"},
    )

    reject_patterns(
        simulation_files,
        (
            r"\b(?:global::)?Unity(?:Engine|Editor)\b",
            r"\bSystem\.(?:IO|Net|Threading|Reflection)\b",
            r"\bSystem\.Runtime\.InteropServices\b",
            r"\b(?:DateTime|DateTimeOffset|Guid|Random|Stopwatch)\b",
            r"\bEnvironment\.(?:TickCount|TickCount64)\b",
            r"\b(?:Thread|Task)\b",
            r"\b(?:unsafe|stackalloc|dynamic)\b",
            r"\bDllImport\b",
        ),
        "the deterministic boundary",
    )
    reject_patterns(
        save_files,
        (
            r"\b(?:global::)?Unity(?:Engine|Editor)\b",
            r"\bSystem\.(?:IO|Net|Threading|Reflection|Text\.Json)\b",
            r"\bSystem\.Runtime\.(?:InteropServices|Serialization)\b",
            r"\b(?:File|Directory|Path|Stream|Reader|Writer|Serializer)\b",
            r"\b(?:unsafe|stackalloc|dynamic)\b",
            r"\bDllImport\b",
        ),
        "the disabled-persistence boundary",
    )

    shared_properties = {
        "TargetFramework": "netstandard2.1",
        "LangVersion": "9.0",
        "Nullable": "enable",
        "ImplicitUsings": "disable",
        "TreatWarningsAsErrors": "true",
        "Deterministic": "true",
        "ContinuousIntegrationBuild": "true",
        "DebugType": "embedded",
        "GenerateMSBuildEditorConfigFile": "false",
        "PathMap": (
            "$(MSBuildProjectDirectory)=/_/SimulationCore,"
            "$([System.IO.Path]::GetFullPath('$(ArtifactsRoot)'))"
            "=/_/Artifacts/SimulationCore"
        ),
        "EnableDefaultCompileItems": "false",
    }
    validate_project(
        ROOT / "SimulationCore/AtomicLandPirate.SimulationCore.csproj",
        {
            **shared_properties,
            "AssemblyName": "AC21.Sasha.SimulationCore",
            "RootNamespace": "AtomicLandPirate.Simulation",
        },
        [("Compile", "Runtime/**/*.cs")],
    )
    validate_project(
        ROOT / "SaveContracts/AtomicLandPirate.SaveContracts.csproj",
        {
            **shared_properties,
            "AssemblyName": "AC21.Sasha.SaveContracts",
            "RootNamespace": "AtomicLandPirate.Save",
            "PathMap": (
                "$(MSBuildProjectDirectory)=/_/SaveContracts,"
                "$([System.IO.Path]::GetFullPath('$(ArtifactsRoot)'))"
                "=/_/Artifacts/SaveContracts"
            ),
        },
        [("Compile", "Runtime/**/*.cs")],
    )
    validate_project(
        ROOT / "Tests/AtomicLandPirate.CoreTests/"
        "AtomicLandPirate.CoreTests.csproj",
        {
            "OutputType": "Exe",
            "TargetFramework": "net8.0",
            "AssemblyName": "AtomicLandPirate.CoreTests",
            "RootNamespace": "AtomicLandPirate.CoreTests",
            "LangVersion": "9.0",
            "Nullable": "enable",
            "ImplicitUsings": "disable",
            "TreatWarningsAsErrors": "true",
            "Deterministic": "true",
            "ContinuousIntegrationBuild": "true",
            "DebugType": "embedded",
            "GenerateMSBuildEditorConfigFile": "false",
            "PathMap": (
                "$(MSBuildProjectDirectory)=/_/CoreTests,"
                "$([System.IO.Path]::GetFullPath('$(ArtifactsRoot)'))"
                "=/_/Artifacts/CoreTests"
            ),
        },
        [
            (
                "ProjectReference",
                "../../SimulationCore/"
                "AtomicLandPirate.SimulationCore.csproj",
            ),
            (
                "ProjectReference",
                "../../SaveContracts/"
                "AtomicLandPirate.SaveContracts.csproj",
            ),
        ],
    )

    expected_packages = {
        ROOT / "SimulationCore/package.json": (
            "com.ac21.sasha.simulation-core",
            {},
        ),
        ROOT / "SaveContracts/package.json": (
            "com.ac21.sasha.save-contracts",
            {},
        ),
    }
    for path, (expected_name, expected_dependencies) in expected_packages.items():
        document = json.loads(path.read_text(encoding="utf-8"))
        if document.get("name") != expected_name:
            raise SystemExit(
                f"{path.relative_to(ROOT)} has the wrong package name"
            )
        if document.get("dependencies") != expected_dependencies:
            raise SystemExit(
                f"{path.relative_to(ROOT)} gained a package dependency"
            )

    expected_assemblies = {
        ROOT / "SimulationCore/Runtime/AC21.Sasha.SimulationCore.asmdef":
            "AC21.Sasha.SimulationCore",
        ROOT / "SaveContracts/Runtime/AC21.Sasha.SaveContracts.asmdef":
            "AC21.Sasha.SaveContracts",
    }
    for path, expected_name in expected_assemblies.items():
        document = json.loads(path.read_text(encoding="utf-8"))
        if document.get("name") != expected_name:
            raise SystemExit(
                f"{path.relative_to(ROOT)} has the wrong assembly name"
            )
        if document.get("references") != []:
            raise SystemExit(
                f"{path.relative_to(ROOT)} gained an assembly reference"
            )
        if document.get("noEngineReferences") is not True:
            raise SystemExit(
                f"{path.relative_to(ROOT)} must deny engine references"
            )



def reset_dotnet_artifacts() -> None:
    require_safe_artifact_directory(DOTNET_ARTIFACTS)
    if DOTNET_ARTIFACTS.exists():
        shutil.rmtree(DOTNET_ARTIFACTS)
    DOTNET_ARTIFACTS.mkdir(parents=True, exist_ok=True)


def restore_and_build(
    dotnet: str,
    environment: dict[str, str],
    repo_root: Path = ROOT,
) -> None:
    if repo_root != ROOT:
        require_safe_artifact_directory(repo_root)
    tests_root = repo_root / "Tests"
    run(
        [
            dotnet,
            "restore",
            str(PROJECT),
            "--configfile",
            str(repo_root / "Tools/nuget-offline.config"),
            "--packages",
            str(LOCAL_ARTIFACTS / "nuget"),
            "--no-cache",
            "--disable-parallel",
        ],
        environment,
        cwd=tests_root,
    )
    run(
        [
            dotnet,
            "build",
            str(PROJECT),
            "--configuration",
            "Release",
            "--no-restore",
            "--no-incremental",
        ],
        environment,
        cwd=tests_root,
    )


def expected_binaries(repo_root: Path) -> dict[str, Path]:
    artifacts = repo_root / "BuildArtifacts/WP-0003/local-only/dotnet"
    return {
        "SimulationCore": (
            artifacts
            / "SimulationCore/bin/Release/netstandard2.1/"
            "AC21.Sasha.SimulationCore.dll"
        ),
        "SaveContracts": (
            artifacts
            / "SaveContracts/bin/Release/netstandard2.1/"
            "AC21.Sasha.SaveContracts.dll"
        ),
        "CoreTests": (
            artifacts
            / "CoreTests/bin/Release/net8.0/"
            "AtomicLandPirate.CoreTests.dll"
        ),
    }


def binary_hashes(
    label: str,
    repo_root: Path = ROOT,
) -> dict[str, str]:
    hashes: dict[str, str] = {}
    for name, path in expected_binaries(repo_root).items():
        if not path.is_file():
            raise SystemExit(
                f"Expected {name} binary was not produced: "
                f"{path.relative_to(repo_root)}"
            )
        digest = sha256_file(path)
        hashes[name] = digest
        print(f"BUILD {label} {name}_sha256={digest}")
    return hashes


def copy_cross_root_source(destination: Path) -> None:
    require_safe_artifact_directory(destination)
    destination.mkdir(parents=True, exist_ok=True)
    for directory in ("SimulationCore", "SaveContracts", "Tests"):
        shutil.copytree(ROOT / directory, destination / directory)
    tools = destination / "Tools"
    tools.mkdir()
    shutil.copy2(
        ROOT / "Tools/nuget-offline.config",
        tools / "nuget-offline.config",
    )


def initialize_repro_checkout(
    destination: Path,
    environment: dict[str, str],
) -> str:
    require_safe_artifact_directory(destination)
    git_environment = {
        key: value
        for key, value in environment.items()
        if not key.startswith("GIT_")
    }
    git_environment.update(
        {
            "GIT_AUTHOR_NAME": "WP-0003 Validator",
            "GIT_AUTHOR_EMAIL": "wp0003-validator@example.invalid",
            "GIT_AUTHOR_DATE": "2026-07-16T00:00:00Z",
            "GIT_COMMITTER_NAME": "WP-0003 Validator",
            "GIT_COMMITTER_EMAIL": "wp0003-validator@example.invalid",
            "GIT_COMMITTER_DATE": "2026-07-16T00:00:00Z",
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_CONFIG_SYSTEM": os.devnull,
            "GIT_ATTR_NOSYSTEM": "1",
            "GIT_NO_REPLACE_OBJECTS": "1",
            "GIT_TERMINAL_PROMPT": "0",
            "TZ": "UTC",
        }
    )
    git = system_git_executable()
    run(
        [git, "init", "--quiet", "--initial-branch=main", "--template="],
        git_environment,
        cwd=destination,
    )
    git_directory = destination / ".git"
    if git_directory.is_symlink() or not git_directory.is_dir():
        raise SystemExit("Reproducibility fixture has an unsafe Git directory")
    disabled_hooks = git_directory / "disabled-hooks"
    disabled_hooks.mkdir()
    pinned_git = [
        git,
        f"--git-dir={git_directory}",
        f"--work-tree={destination}",
    ]
    run(
        [*pinned_git, "config", "--local", "core.autocrlf", "false"],
        git_environment,
        cwd=destination,
    )
    run(
        [*pinned_git, "config", "--local", "commit.gpgsign", "false"],
        git_environment,
        cwd=destination,
    )
    run(
        [
            *pinned_git,
            "config",
            "--local",
            "core.hooksPath",
            str(disabled_hooks),
        ],
        git_environment,
        cwd=destination,
    )
    run(
        [
            *pinned_git,
            "remote",
            "add",
            "origin",
            "https://github.com/AC-21/sasha-the-land-pirate",
        ],
        git_environment,
        cwd=destination,
    )
    run(
        [
            *pinned_git,
            "add",
            "SimulationCore",
            "SaveContracts",
            "Tests",
            "Tools",
        ],
        git_environment,
        cwd=destination,
    )
    run(
        [
            *pinned_git,
            "commit",
            "--quiet",
            "-m",
            "WP-0003 reproducibility fixture",
        ],
        git_environment,
        cwd=destination,
    )
    return run(
        [*pinned_git, "rev-parse", "HEAD"],
        git_environment,
        capture_output=True,
        cwd=destination,
    ).decode("utf-8").strip()


def validate_cross_root_reproducibility(
    dotnet: str,
    environment: dict[str, str],
) -> None:
    require_safe_artifact_directory(CROSS_ROOT_ARTIFACTS)
    if CROSS_ROOT_ARTIFACTS.exists():
        shutil.rmtree(CROSS_ROOT_ARTIFACTS)
    first_root = CROSS_ROOT_ARTIFACTS / "checkout-a"
    second_root = CROSS_ROOT_ARTIFACTS / "checkout-b"
    copy_cross_root_source(first_root)
    copy_cross_root_source(second_root)
    first_commit = initialize_repro_checkout(first_root, environment)
    second_commit = initialize_repro_checkout(second_root, environment)
    if first_commit != second_commit:
        raise SystemExit(
            "Independent reproducibility checkouts did not produce the same "
            "source commit"
        )
    print(f"REPRO source_commit={first_commit}")

    restore_and_build(dotnet, environment, first_root)
    first_hashes = binary_hashes("checkout-a", first_root)
    restore_and_build(dotnet, environment, second_root)
    second_hashes = binary_hashes("checkout-b", second_root)
    if first_hashes != second_hashes:
        raise SystemExit(
            "Identical sources built in different checkout roots produced "
            "different DLL hashes"
        )
    print("PASS different checkout roots produce identical DLL hashes")


def main() -> int:
    args = parse_args()
    dotnet = resolve_dotnet(args.dotnet)
    validate_source_boundaries()

    temporary_directory = LOCAL_ARTIFACTS / "tmp"
    artifact_directories = (
        LOCAL_ARTIFACTS,
        LOCAL_ARTIFACTS / "dotnet-home",
        LOCAL_ARTIFACTS / "nuget",
        temporary_directory,
        DOTNET_ARTIFACTS,
        CROSS_ROOT_ARTIFACTS,
    )
    for artifact_directory in artifact_directories:
        require_safe_artifact_directory(artifact_directory)
    LOCAL_ARTIFACTS.mkdir(parents=True, exist_ok=True)
    temporary_directory.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment.update(
        {
            "DOTNET_CLI_HOME": str(LOCAL_ARTIFACTS / "dotnet-home"),
            "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
            "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
            "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE": "1",
            "DOTNET_MULTILEVEL_LOOKUP": "0",
            "DOTNET_NOLOGO": "1",
            "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
            "NUGET_PACKAGES": str(LOCAL_ARTIFACTS / "nuget"),
            "MSBUILDDISABLENODEREUSE": "1",
            "TMPDIR": str(temporary_directory),
        }
    )

    validate_dotnet(dotnet, environment)
    reset_dotnet_artifacts()
    restore_and_build(dotnet, environment)
    first_hashes = binary_hashes("first")
    run_command = [
        dotnet,
        "run",
        "--project",
        str(PROJECT),
        "--configuration",
        "Release",
        "--no-build",
        "--no-restore",
    ]
    first_output = run(
        run_command,
        environment,
        capture_output=True,
    )
    repeated_output = run(
        run_command,
        environment,
        capture_output=True,
    )
    if first_output != repeated_output:
        raise SystemExit("Repeated technical test output was not byte-identical")
    print("PASS repeated process output is byte-identical")

    reset_dotnet_artifacts()
    restore_and_build(dotnet, environment)
    second_hashes = binary_hashes("second")
    if first_hashes != second_hashes:
        raise SystemExit("Clean repeated build DLL hashes were not identical")
    print("PASS clean repeated build DLL hashes are identical")

    rebuilt_output = run(
        run_command,
        environment,
        capture_output=True,
    )
    if first_output != rebuilt_output:
        raise SystemExit(
            "Technical test output changed after the clean repeated build"
        )
    print("PASS clean repeated build output is byte-identical")
    validate_cross_root_reproducibility(dotnet, environment)
    return 0


if __name__ == "__main__":
    sys.exit(main())
