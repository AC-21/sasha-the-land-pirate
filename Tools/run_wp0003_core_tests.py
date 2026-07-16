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
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TESTS = ROOT / "Tests"
PROJECT = Path("AtomicLandPirate.CoreTests/AtomicLandPirate.CoreTests.csproj")
LOCAL_ARTIFACTS = ROOT / "BuildArtifacts/WP-0003/local-only/core-tests"
DOTNET_ARTIFACTS = ROOT / "BuildArtifacts/WP-0003/local-only/dotnet"
CROSS_ROOT_ARTIFACTS = LOCAL_ARTIFACTS / "cross-root"
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
    validate_file_closure(
        ROOT / "Game",
        {
            "Assets/.gitkeep",
            "Packages/manifest.json",
            "ProjectSettings/ProjectVersion.txt",
        },
    )
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

    game_manifest = json.loads(
        (ROOT / "Game/Packages/manifest.json").read_text(encoding="utf-8")
    )
    if game_manifest != {"dependencies": {}}:
        raise SystemExit("Game package seed drifted from the empty dependency graph")

    expected_project_version = (
        "m_EditorVersion: 6000.5.4f1\n"
        "m_EditorVersionWithRevision: 6000.5.4f1 (d550df8bd089)\n"
    )
    actual_project_version = (
        ROOT / "Game/ProjectSettings/ProjectVersion.txt"
    ).read_text(encoding="utf-8")
    if actual_project_version != expected_project_version:
        raise SystemExit("Game project version drifted")


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
