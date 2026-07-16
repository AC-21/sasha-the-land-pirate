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
LOCAL_ARTIFACTS = ROOT / "BuildArtifacts/WP-0003/local-only/core-tests"
DOTNET_ARTIFACTS = ROOT / "BuildArtifacts/WP-0003/local-only/dotnet"
CROSS_ROOT_ARTIFACTS = LOCAL_ARTIFACTS / "cross-root"
UNITY_BASELINE_MANIFEST = (
    ROOT / "docs/manifests/WP-0003-unity-donor-v1.json"
)
UNITY_BASELINE_MANIFEST_SHA256 = (
    "556717ee9e1829dce251d73a08aba79cc8b7a4091313103968a3c5218637dc1a"
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
        "game_files",
        "import_policy",
        "manifest_id",
        "package_lock",
        "packet_id",
        "project_identity",
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
    if document.get("manifest_id") != "WP0003-UNITY-DONOR-20260716":
        raise SystemExit("Unity baseline manifest identity drifted")
    if document.get("packet_id") != "WP-0003":
        raise SystemExit("Unity baseline manifest packet drifted")
    if document.get("base_commit") != (
        "75514781219ceed101a96409913bf483ff0b38b2"
    ):
        raise SystemExit("Unity baseline manifest base commit drifted")
    if document.get("created_at") != "2026-07-16T21:58:14Z":
        raise SystemExit("Unity baseline manifest creation time drifted")
    if document.get("branch") != (
        "agent/wp0003-unity-donor-migration-001"
    ):
        raise SystemExit("Unity baseline manifest branch drifted")

    approval = document.get("creator_approval")
    if approval != {
        "captured_on": "2026-07-16",
        "instruction": (
            "Use Sashas as the donor project with the curated package set. "
            "- And yes use the packages we need initially."
        ),
        "scope": (
            "candidate filtered donor baseline and initially needed packages; "
            "exact dependency diff remains creator-review-gated"
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
    if import_policy.get("kind") != "filtered donor seed" or any(
        not isinstance(import_policy.get(key), list)
        or not import_policy[key]
        or not all(isinstance(value, str) and value for value in import_policy[key])
        for key in ("excluded", "kept", "normalizations")
    ):
        raise SystemExit("Unity baseline import policy drifted")

    game_root = ROOT / "Game"
    entries = document.get("game_files")
    if not isinstance(entries, list) or not entries:
        raise SystemExit("Unity baseline manifest has no Game file closure")

    expected_files: dict[str, tuple[int, str]] = {}
    casefolded_paths: set[str] = set()
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != {
            "path",
            "size",
            "sha256",
        }:
            raise SystemExit("Unity baseline file entry shape drifted")
        relative = entry["path"]
        size = entry["size"]
        digest = entry["sha256"]
        if (
            not isinstance(relative, str)
            or not relative
            or relative.startswith("/")
            or "\\" in relative
            or any(part in ("", ".", "..") for part in relative.split("/"))
        ):
            raise SystemExit(f"Unsafe Unity baseline path: {relative!r}")
        if (
            not isinstance(size, int)
            or size < 0
            or not isinstance(digest, str)
            or re.fullmatch(r"[0-9a-f]{64}", digest) is None
        ):
            raise SystemExit(
                f"Unity baseline file metadata drifted: {relative}"
            )
        folded = relative.casefold()
        if relative in expected_files or folded in casefolded_paths:
            raise SystemExit(
                f"Duplicate or case-colliding Unity path: {relative}"
            )
        expected_files[relative] = (size, digest)
        casefolded_paths.add(folded)

    if list(expected_files) != sorted(expected_files):
        raise SystemExit("Unity baseline file closure must be path-sorted")

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
    actual_files: dict[str, Path] = {}
    for path in game_root.rglob("*"):
        if path.is_symlink():
            raise SystemExit(
                f"{path.relative_to(ROOT)} may not be a symlink"
            )
        if not path.is_file():
            continue
        relative = path.relative_to(game_root).as_posix()
        lowered_parts = {part.casefold() for part in relative.split("/")}
        if lowered_parts & forbidden_components:
            raise SystemExit(f"Forbidden Unity-generated path: {relative}")
        if relative.casefold().endswith(forbidden_suffixes):
            raise SystemExit(f"Forbidden Unity-generated file: {relative}")
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
        match = re.search(
            r"(?m)^guid: ([0-9a-f]{32})$",
            path.read_text(encoding="utf-8"),
        )
        if match is None or match.group(1) in seen_guids:
            raise SystemExit(f"Invalid or duplicate Unity meta GUID: {relative}")
        meta_guids[relative] = match.group(1)
        seen_guids.add(match.group(1))
    if meta_guids != document.get("asset_guids"):
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
            "shortest reachable closure filtered from the donor lock; "
            "canonical Editor import is not yet claimed"
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
    if require_unique_yaml_scalar(
        build_settings,
        "m_Scenes",
        2,
        "Unity Editor build settings",
    ) != "[]":
        raise SystemExit("Game baseline unexpectedly contains a scene")

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
    local_attributes = (
        game_root / ".gitattributes"
    ).read_text(encoding="utf-8")
    if local_attributes != (
        "# Unity serializes empty YAML scalar values with one trailing space.\n"
        "**/*.asset text eol=lf whitespace=-blank-at-eol\n"
        "**/*.meta text eol=lf whitespace=-blank-at-eol\n"
    ):
        raise SystemExit("Game Unity YAML Git attributes drifted")

    if document.get("unity_boundary") != {
        "hub_invoked_by_agent": False,
        "editor_invoked_by_agent": False,
        "relay_invoked_by_agent": False,
        "mcp_invoked_by_agent": False,
        "canonical_project_opened": False,
        "first_use_gate": "closed",
        "compile_verified": False,
        "editmode_verified": False,
        "playmode_verified": False,
    }:
        raise SystemExit("Unity execution boundary attestation drifted")
    if document.get("rollback") != {
        "method": "ordinary Git revert or branch deletion",
        "target_commit": "75514781219ceed101a96409913bf483ff0b38b2",
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
            "Directory.Build.props",
            "Directory.Build.targets",
            "README.md",
            "Runtime/AC21.Sasha.SimulationCore.asmdef",
            "Runtime/AssemblyInfo.cs",
            "Runtime/CanonicalState.cs",
            "Runtime/SimulationKernel.cs",
            "Runtime/TechnicalCommand.cs",
            "Runtime/TechnicalDeltaApplied.cs",
            "Runtime/TechnicalReadModel.cs",
            "Runtime/TechnicalRunResult.cs",
            "Runtime/TechnicalState.cs",
            "Runtime/TechnicalTransition.cs",
            "package.json",
        },
    )
    validate_file_closure(
        ROOT / "SaveContracts",
        {
            "AtomicLandPirate.SaveContracts.csproj",
            "Directory.Build.props",
            "Directory.Build.targets",
            "README.md",
            "Runtime/AC21.Sasha.SaveContracts.asmdef",
            "Runtime/ISavePort.cs",
            "Runtime/NonPersistingSavePort.cs",
            "Runtime/SaveAttemptResult.cs",
            "Runtime/SaveCapability.cs",
            "package.json",
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
    if DOTNET_ARTIFACTS.exists():
        shutil.rmtree(DOTNET_ARTIFACTS)
    DOTNET_ARTIFACTS.mkdir(parents=True, exist_ok=True)


def restore_and_build(
    dotnet: str,
    environment: dict[str, str],
    repo_root: Path = ROOT,
) -> None:
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
    git_environment = environment.copy()
    git_environment.update(
        {
            "GIT_AUTHOR_NAME": "WP-0003 Validator",
            "GIT_AUTHOR_EMAIL": "wp0003-validator@example.invalid",
            "GIT_AUTHOR_DATE": "2026-07-16T00:00:00Z",
            "GIT_COMMITTER_NAME": "WP-0003 Validator",
            "GIT_COMMITTER_EMAIL": "wp0003-validator@example.invalid",
            "GIT_COMMITTER_DATE": "2026-07-16T00:00:00Z",
            "TZ": "UTC",
        }
    )
    run(
        ["git", "init", "--quiet", "--initial-branch=main"],
        git_environment,
        cwd=destination,
    )
    run(
        ["git", "config", "core.autocrlf", "false"],
        git_environment,
        cwd=destination,
    )
    run(
        ["git", "config", "commit.gpgsign", "false"],
        git_environment,
        cwd=destination,
    )
    run(
        [
            "git",
            "remote",
            "add",
            "origin",
            "https://github.com/AC-21/sasha-the-land-pirate",
        ],
        git_environment,
        cwd=destination,
    )
    run(
        ["git", "add", "SimulationCore", "SaveContracts", "Tests", "Tools"],
        git_environment,
        cwd=destination,
    )
    run(
        ["git", "commit", "--quiet", "-m", "WP-0003 reproducibility fixture"],
        git_environment,
        cwd=destination,
    )
    return run(
        ["git", "rev-parse", "HEAD"],
        git_environment,
        capture_output=True,
        cwd=destination,
    ).decode("utf-8").strip()


def validate_cross_root_reproducibility(
    dotnet: str,
    environment: dict[str, str],
) -> None:
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

    LOCAL_ARTIFACTS.mkdir(parents=True, exist_ok=True)
    temporary_directory = LOCAL_ARTIFACTS / "tmp"
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
