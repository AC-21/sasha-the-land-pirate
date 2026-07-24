#!/usr/bin/env python3
"""Run one hash-pinned WP-0002 Last Bearing scenario headlessly."""

from __future__ import annotations

import os
import json
import shutil
import subprocess
import sys
from pathlib import Path


SCENARIOS = frozenset(
    {
        "SCN_COMPOSITION_LOOP_SMOKE",
        "SCN_TIME_POLICY",
        "SCN_PREPARATION_MODULE_MATRIX",
        "SCN_FACTION_WAIT_CLAIM",
        "SCN_BEARING_COOPERATE",
        "SCN_BEARING_TAKE",
    }
)

NAMED_TESTS = frozenset(
    {
        "dev-save-atomic",
        "dev-save-boundary",
        "vgr05-one-good-batch",
        "vgr21-post-fuel-bond",
        "v0-hands-on-service-cell",
    }
)


def _dotnet() -> tuple[str, bool]:
    configured = os.environ.get("DOTNET_HOST_PATH")
    if configured and Path(configured).is_file():
        return configured, "Unity.app/Contents/Resources/Scripting/DotNetSdk" in configured
    discovered = shutil.which("dotnet")
    if discovered:
        return discovered, False
    unity_host = Path(
        "/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app/Contents/"
        "Resources/Scripting/DotNetSdk/dotnet"
    )
    if unity_host.is_file():
        return str(unity_host), True
    raise RuntimeError("dotnet host is unavailable")


def _compile_with_unity_sdk(
    repo_root: Path,
    dotnet: str,
    local_root: Path,
    environment: dict[str, str],
) -> Path:
    sdk_root = Path(dotnet).parent
    csc = sdk_root / "sdk/8.0.318/Roslyn/bincore/csc.dll"
    net8_reference_root = (
        sdk_root / "packs/Microsoft.NETCore.App.Ref/8.0.21/ref/net8.0"
    )
    netstandard_reference_root = (
        sdk_root / "packs/NETStandard.Library.Ref/2.1.0/ref/netstandard2.1"
    )
    if (
        not csc.is_file()
        or not net8_reference_root.is_dir()
        or not netstandard_reference_root.is_dir()
    ):
        raise RuntimeError("exact Unity .NET 8.0.318 reference toolchain is unavailable")

    output = local_root / "headless"
    output.mkdir(parents=True, exist_ok=True)
    common = [
        dotnet,
        str(csc),
        "-noconfig",
        "-nostdlib+",
        "-langversion:9.0",
        "-nullable:enable",
        "-warnaserror+",
        "-deterministic+",
        "-debug:embedded",
        "-pathmap:" + str(repo_root) + "=/_/SashaAtomicLandPirate",
    ]
    net8_references = [
        "-r:" + str(path)
        for path in sorted(net8_reference_root.glob("*.dll"))
    ]
    netstandard_references = [
        "-r:" + str(path)
        for path in sorted(netstandard_reference_root.glob("*.dll"))
    ]

    core_dll = output / "AC21.Sasha.SimulationCore.dll"
    save_dll = output / "AC21.Sasha.SaveContracts.dll"
    test_dll = output / "AtomicLandPirate.LastBearingTests.dll"
    compile_steps = [
        [
            *common,
            *netstandard_references,
            "-target:library",
            "-out:" + str(core_dll),
            *[str(path) for path in sorted((repo_root / "SimulationCore/Runtime").rglob("*.cs"))],
        ],
        [
            *common,
            *netstandard_references,
            "-target:library",
            "-out:" + str(save_dll),
            *[str(path) for path in sorted((repo_root / "SaveContracts/Runtime").rglob("*.cs"))],
        ],
        [
            *common,
            *net8_references,
            "-target:exe",
            "-out:" + str(test_dll),
            "-r:" + str(core_dll),
            "-r:" + str(save_dll),
            *[
                str(path)
                for path in sorted(
                    (repo_root / "Tests/AtomicLandPirate.CoreTests/LastBearing").glob("*.cs")
                )
            ],
            str(
                repo_root
                / "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingSaveAdapter.cs"
            ),
        ],
    ]
    for command in compile_steps:
        completed = subprocess.run(
            command,
            cwd=repo_root,
            env=environment,
            check=False,
        )
        if completed.returncode != 0:
            raise RuntimeError("Unity SDK headless compilation failed")

    runtime_config = {
        "runtimeOptions": {
            "tfm": "net8.0",
            "framework": {
                "name": "Microsoft.NETCore.App",
                "version": "8.0.0",
            },
        },
    }
    test_dll.with_suffix(".runtimeconfig.json").write_text(
        json.dumps(runtime_config, sort_keys=True, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )
    return test_dll


def main(argv: list[str]) -> int:
    program_arguments: list[str]
    if len(argv) == 2 and argv[1] in SCENARIOS:
        program_arguments = ["--scenario", argv[1]]
    elif len(argv) == 3 and argv[1] == "--test" and argv[2] in NAMED_TESTS:
        program_arguments = ["--test", argv[2]]
    elif len(argv) == 2 and argv[1] == "--all":
        program_arguments = []
    else:
        scenarios = ", ".join(sorted(SCENARIOS))
        tests = ", ".join(sorted(NAMED_TESTS))
        print(
            f"usage: {argv[0]} <scenario> | --test <id> | --all; "
            f"scenarios: {scenarios}; tests: {tests}",
            file=sys.stderr,
        )
        return 2

    repo_root = Path(__file__).resolve().parents[2]
    project = (
        repo_root
        / "Tests/AtomicLandPirate.CoreTests/LastBearing/"
        "AtomicLandPirate.LastBearingTests.csproj"
    )
    local_root = repo_root / "BuildArtifacts/WP-0002/local-only"
    dotnet_home = local_root / "dotnet-home"
    nuget_packages = local_root / "nuget"
    dotnet_home.mkdir(parents=True, exist_ok=True)
    nuget_packages.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment["DOTNET_CLI_HOME"] = str(dotnet_home)
    environment["NUGET_PACKAGES"] = str(nuget_packages)
    environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
    environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    dotnet, unity_embedded = _dotnet()
    if unity_embedded:
        test_dll = _compile_with_unity_sdk(
            repo_root,
            dotnet,
            local_root,
            environment,
        )
        command = [dotnet, str(test_dll), *program_arguments]
    else:
        command = [
            dotnet,
            "run",
            "--project",
            str(project),
            "--configuration",
            "Release",
            "--property:RestoreConfigFile=" + str(repo_root / "Tools/nuget-offline.config"),
            "--",
            *program_arguments,
        ]
    return subprocess.run(
        command,
        cwd=repo_root,
        env=environment,
        check=False,
    ).returncode


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
