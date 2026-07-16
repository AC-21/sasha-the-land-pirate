#!/usr/bin/env python3
"""Adversarial tests for the A0-only WP-0001 static toolchain collector."""

from __future__ import annotations

import dataclasses
import copy
import hashlib
import json
import os
import plistlib
import struct
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import inspect_wp0001_toolchain_static as static
import validate_foundation as foundation


def thin_macho(cpu_type: int) -> bytes:
    header = b"\xcf\xfa\xed\xfe" + struct.pack(
        "<IIIIIII",
        cpu_type,
        0,
        2,
        1,
        72,
        0,
        0,
    )
    load_command = struct.pack("<II", 0x19, 72) + (b"\0" * 64)
    return header + load_command


ARM64_MACHO = thin_macho(0x0100000C)
X86_64_MACHO = thin_macho(0x01000007)

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


class StaticFixture:
    def __init__(self, root: Path) -> None:
        self.root = root.resolve()
        self.repo = self.root / "repo"
        self.project = self.repo / "Game"
        self.hub = self.root / "Applications" / "Unity Hub.app"
        self.editors = self.root / "Applications" / "Unity" / "Hub" / "Editor"
        self.xcode = self.root / "Applications" / "Xcode.app"
        self.xcode_select = self.root / "private" / "var" / "db" / "xcode_select_link"
        self.dotnet = self.root / "usr" / "local" / "share" / "dotnet"
        self.rosetta_receipt = (
            self.root
            / "Library"
            / "Apple"
            / "System"
            / "Library"
            / "Receipts"
            / "com.apple.pkg.RosettaUpdateAuto.plist"
        )
        self.rosetta_markers = (
            self.root / "Library" / "Apple" / "usr" / "libexec" / "oah" / "libRosettaRuntime",
            self.root / "usr" / "libexec" / "rosetta" / "runtime",
            self.root / "usr" / "libexec" / "rosetta" / "translate_tool",
        )
        self.source = self.root / "collector-source.py"
        self.write(self.source, b"# synthetic collector source\n")
        self.paths = static.CollectorPaths(
            repo_root=self.repo,
            project_root=self.project,
            hub_app=self.hub,
            editors_root=self.editors,
            xcode_app=self.xcode,
            xcode_select_link=self.xcode_select,
            dotnet_root=self.dotnet,
            rosetta_receipt=self.rosetta_receipt,
            rosetta_markers=self.rosetta_markers,
        )

    @staticmethod
    def write(path: Path, data: bytes) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    @staticmethod
    def write_json(path: Path, value: object) -> None:
        StaticFixture.write(
            path,
            json.dumps(value, sort_keys=True, separators=(",", ":")).encode(),
        )

    @staticmethod
    def write_plist(path: Path, value: dict[str, object]) -> None:
        StaticFixture.write(path, plistlib.dumps(value, sort_keys=True))

    def make_bundle(
        self,
        app: Path,
        *,
        identifier: str,
        version: str,
        executable: str,
        architecture: bytes = ARM64_MACHO,
        bundle_version: str | None = None,
        changeset: str | None = None,
    ) -> None:
        info: dict[str, object] = {
            "CFBundleIdentifier": identifier,
            "CFBundleShortVersionString": version,
            "CFBundleVersion": bundle_version or version,
            "CFBundleExecutable": executable,
        }
        if changeset is not None:
            info["UnityBuildNumber"] = changeset
        self.write_plist(app / "Contents" / "Info.plist", info)
        executable_path = app / "Contents" / "MacOS" / executable
        self.write(executable_path, architecture)
        executable_path.chmod(0o755)

    def make_hub(self) -> None:
        self.make_bundle(
            self.hub,
            identifier="com.unity3d.unityhub",
            version=static.REQUIRED_HUB_VERSION,
            executable="Unity Hub",
        )

    def make_editor(
        self,
        *,
        version: str = static.REQUIRED_EDITOR_VERSION,
        changeset: str = static.REQUIRED_EDITOR_CHANGESET,
        architecture: bytes = ARM64_MACHO,
    ) -> Path:
        root = self.editors / version
        self.make_bundle(
            root / "Unity.app",
            identifier="com.unity3d.UnityEditor5.x",
            version=f"Unity version {version}",
            bundle_version=version,
            executable="Unity",
            architecture=architecture,
            changeset=changeset,
        )
        return root

    def make_il2cpp_catalog(self, editor_root: Path) -> None:
        self.write_json(
            editor_root / "modules.json",
            [
                {
                    "id": "mac-il2cpp",
                    "name": "Mac Build Support (IL2CPP)",
                    "destination": (
                        "{UNITY_PATH}/PlaybackEngines/MacStandaloneSupport"
                    ),
                    "slug": (
                        f"{static.REQUIRED_EDITOR_VERSION}-mac-os-arm64-"
                        "mac-il2cpp"
                    ),
                    "downloadUrl": (
                        "https://download.unity3d.com/download_unity/"
                        f"{static.REQUIRED_EDITOR_CHANGESET}/MacEditorTargetInstaller/"
                        "UnitySetup-Mac-IL2CPP-Support.pkg"
                    ),
                }
            ],
        )

    def make_il2cpp_marker(self, editor_root: Path) -> tuple[dict[str, object], Path]:
        relative = (
            Path("Variations")
            / "macos_arm64_player_development_il2cpp"
            / "UnityPlayer.dylib"
        )
        marker = (
            editor_root
            / "PlaybackEngines"
            / "MacStandaloneSupport"
            / relative
        )
        self.write(marker, ARM64_MACHO)
        profile = {
            "schema_version": 1,
            "editor_version": static.REQUIRED_EDITOR_VERSION,
            "editor_changeset": static.REQUIRED_EDITOR_CHANGESET,
            "markers": [
                {
                    "path": relative.as_posix(),
                    "sha256": hashlib.sha256(ARM64_MACHO).hexdigest(),
                    "architectures": ["arm64"],
                }
            ],
        }
        return profile, marker

    @staticmethod
    def valid_manifest(
        *,
        assistant: str = static.REQUIRED_ASSISTANT,
        urp: str = "17.3.0",
        tests: str = "1.6.0",
    ) -> dict[str, object]:
        return {
            "dependencies": {
                "com.unity.ai.assistant": assistant,
                "com.unity.render-pipelines.universal": urp,
                "com.unity.test-framework": tests,
            },
            "enableLockFile": True,
            "resolutionStrategy": "lowest",
            "testables": ["com.unity.test-framework"],
            "useSatSolver": True,
        }

    @staticmethod
    def valid_lock(
        *,
        assistant: str = static.REQUIRED_ASSISTANT,
        urp: str = "17.3.0",
        tests: str = "1.6.0",
    ) -> dict[str, object]:
        def record(version: str) -> dict[str, object]:
            return {
                "version": version,
                "depth": 0,
                "source": "registry",
                "url": "https://packages.unity.com",
                "dependencies": {},
            }

        return {
            "dependencies": {
                "com.unity.ai.assistant": record(assistant),
                "com.unity.render-pipelines.universal": record(urp),
                "com.unity.test-framework": record(tests),
            }
        }

    def make_package_graph(
        self,
        *,
        manifest: dict[str, object] | None = None,
        lock: dict[str, object] | None = None,
    ) -> None:
        self.write_json(
            self.project / "Packages" / "manifest.json",
            manifest or self.valid_manifest(),
        )
        self.write_json(
            self.project / "Packages" / "packages-lock.json",
            lock or self.valid_lock(),
        )

    def make_rosetta(self) -> None:
        self.write_plist(
            self.rosetta_receipt,
            {
                "PackageIdentifier": "com.apple.pkg.RosettaUpdateAuto",
                "PackageVersion": "1.0.0.0.1.fixture",
            },
        )
        for marker in self.rosetta_markers:
            self.write(marker, b"synthetic rosetta marker")


class StaticToolchainInspectionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            dir=str(Path(tempfile.gettempdir()).resolve())
        )
        self.addCleanup(self.temporary.cleanup)
        self.fixture = StaticFixture(Path(self.temporary.name))

    @staticmethod
    def by_id(records: list[dict[str, object]]) -> dict[str, dict[str, object]]:
        return {str(record["id"]): record for record in records}

    def test_duplicate_json_keys_are_unsafe(self) -> None:
        with self.assertRaises(static.UnsafeEvidence):
            static.parse_json_bytes(b'{"a":1,"a":2}', "duplicate-fixture")
        with self.assertRaises(static.UnsafeEvidence):
            static.parse_json_bytes(b'{"a":NaN}', "nan-fixture")

    def test_secret_shaped_output_value_is_rejected(self) -> None:
        with self.assertRaises(static.UnsafeEvidence):
            static.require_secret_free_output(
                {"observed": "Bearer abcdefghijklmnop"},
            )

    def test_truncated_or_commandless_macho_is_rejected(self) -> None:
        with self.assertRaises(static.UnsafeEvidence):
            static.macho_architectures(
                b"\xcf\xfa\xed\xfe" + struct.pack("<I", 0x0100000C)
            )
        invalid = (
            b"\xcf\xfa\xed\xfe"
            + struct.pack("<IIIIIII", 0x0100000C, 0, 2, 1, 72, 0, 0)
            + struct.pack("<II", 0x1B, 128)
            + (b"\0" * 16)
        )
        with self.assertRaises(static.UnsafeEvidence):
            static.macho_architectures(invalid)

    def test_regular_file_reader_rejects_symlink_leaf_and_nonregular_leaf(self) -> None:
        target = self.fixture.root / "target"
        self.fixture.write(target, b"fixture")
        symlink = self.fixture.root / "link"
        symlink.symlink_to(target)
        with self.assertRaises(static.UnsafeEvidence):
            static.read_regular_file(symlink, max_bytes=1024)

        fifo = self.fixture.root / "fifo"
        os.mkfifo(fifo)
        with self.assertRaises(static.UnsafeEvidence):
            static.read_regular_file(fifo, max_bytes=1024)

    def test_regular_file_reader_rejects_symlink_ancestor(self) -> None:
        real = self.fixture.root / "real"
        self.fixture.write(real / "evidence", b"fixture")
        alias = self.fixture.root / "alias"
        alias.symlink_to(real, target_is_directory=True)
        with self.assertRaises(static.UnsafeEvidence):
            static.read_regular_file(alias / "evidence", max_bytes=1024)

    def test_output_rejects_activation_directory_and_existing_file(self) -> None:
        activation = (
            self.fixture.repo
            / "docs"
            / "evidence"
            / "WP-0001"
            / "a1-activation"
        )
        activation.mkdir(parents=True)
        with self.assertRaises(static.UnsafeEvidence):
            static.write_output(
                activation / "static.json",
                b"{}\n",
                repo_root=self.fixture.repo,
            )

        output = self.fixture.root / "capture.json"
        static.write_output(output, b"first\n", repo_root=self.fixture.repo)
        self.assertEqual(b"first\n", output.read_bytes())
        with self.assertRaises(FileExistsError):
            static.write_output(output, b"second\n", repo_root=self.fixture.repo)

    def test_editor_requires_exact_changeset_and_arm64_slice(self) -> None:
        self.fixture.make_editor(changeset="wrongchangeset")
        result = static.inspect_editor(self.fixture.paths)
        self.assertEqual("mismatch", result["status"])
        self.assertIn("UnityBuildNumber changeset differs", result["limitations"])

        self.fixture.make_editor(architecture=X86_64_MACHO)
        result = static.inspect_editor(self.fixture.paths)
        self.assertEqual("mismatch", result["status"])
        self.assertIn("ARM64 executable slice is absent", result["limitations"])

    def test_exact_editor_static_tuple_matches(self) -> None:
        self.fixture.make_editor()
        result = static.inspect_editor(self.fixture.paths)
        self.assertEqual("matched", result["status"])
        self.assertIn(static.REQUIRED_EDITOR_CHANGESET, str(result["observed"]))
        self.assertIn("arm64", str(result["observed"]))

    def test_editor_rejects_conflicting_bundle_version_fields(self) -> None:
        editor_root = self.fixture.make_editor()
        info_path = editor_root / "Unity.app" / "Contents" / "Info.plist"
        info = plistlib.loads(info_path.read_bytes())
        info["CFBundleShortVersionString"] = "Unity version 6000.9.9f9"
        self.fixture.write_plist(info_path, info)
        result = static.inspect_editor(self.fixture.paths)
        self.assertEqual("mismatch", result["status"])

    def test_wrong_editor_il2cpp_does_not_satisfy_exact_editor(self) -> None:
        wrong_root = self.fixture.make_editor(version="6000.5.4f1", changeset="d550df8bd089")
        self.fixture.make_il2cpp_catalog(wrong_root)
        profile, _ = self.fixture.make_il2cpp_marker(wrong_root)

        editor = static.inspect_editor(self.fixture.paths)
        il2cpp = static.inspect_il2cpp(
            self.fixture.paths,
            editor,
            marker_profile=profile,
            marker_profile_evidence=None,
        )
        self.assertEqual("mismatch", editor["status"])
        self.assertEqual("missing", il2cpp["status"])

    def test_il2cpp_requires_physical_variation_and_protected_profile(self) -> None:
        editor_root = self.fixture.make_editor()
        self.fixture.make_il2cpp_catalog(editor_root)
        result = static.inspect_il2cpp(
            self.fixture.paths,
            static.inspect_editor(self.fixture.paths),
            marker_profile=None,
            marker_profile_evidence=None,
        )
        self.assertEqual("missing", result["status"])
        self.assertIn("catalog advertises", str(result["observed"]))

        (
            editor_root
            / "PlaybackEngines"
            / "MacStandaloneSupport"
            / "Variations"
            / "macos_arm64_player_development_il2cpp"
        ).mkdir(parents=True)
        (
            editor_root
            / "PlaybackEngines"
            / "MacStandaloneSupport"
            / "Variations"
            / "client_secret=abcdefghijklmnop"
        ).mkdir()
        result = static.inspect_il2cpp(
            self.fixture.paths,
            static.inspect_editor(self.fixture.paths),
            marker_profile=None,
            marker_profile_evidence=None,
        )
        self.assertEqual("unverified", result["status"])
        self.assertNotIn(
            "client_secret",
            json.dumps(result, sort_keys=True),
        )

        profile, _ = self.fixture.make_il2cpp_marker(editor_root)
        profile_evidence = {
            "method": "fixture",
            "location": "fixture-profile",
            "sha256": "0" * 64,
            "observation": "synthetic profile",
        }
        result = static.inspect_il2cpp(
            self.fixture.paths,
            static.inspect_editor(self.fixture.paths),
            marker_profile=profile,
            marker_profile_evidence=profile_evidence,
        )
        self.assertEqual("unverified", result["status"])

        with mock.patch.object(
            static,
            "TRUSTED_IL2CPP_PROFILE_SHA256",
            frozenset({"0" * 64}),
        ):
            result = static.inspect_il2cpp(
                self.fixture.paths,
                static.inspect_editor(self.fixture.paths),
                marker_profile=profile,
                marker_profile_evidence=profile_evidence,
            )
        self.assertEqual("matched", result["status"])

    def test_xcode_absence_is_not_satisfied_by_command_line_tools(self) -> None:
        self.fixture.xcode_select.parent.mkdir(parents=True)
        self.fixture.xcode_select.symlink_to("/Library/Developer/CommandLineTools")
        result = static.inspect_xcode(self.fixture.paths)
        self.assertEqual("missing", result["status"])
        self.assertIn("CommandLineTools", str(result["observed"]))

    def test_absent_contracted_dotnet_root_is_unverified_not_matched(self) -> None:
        result = static.inspect_dotnet(self.fixture.paths)
        self.assertEqual("unverified", result["status"])
        self.assertIsNone(result["observed"])

    def test_rosetta_receipt_matches_with_marker_gaps_as_limitations(self) -> None:
        self.fixture.write_plist(
            self.fixture.rosetta_receipt,
            {
                "PackageIdentifier": "com.apple.pkg.RosettaUpdateAuto",
                "PackageVersion": "1.0.fixture",
            },
        )
        self.fixture.write(self.fixture.rosetta_markers[0], b"marker")
        result = static.inspect_rosetta(self.fixture.paths)
        self.assertEqual("matched", result["status"])
        self.assertTrue(
            any(
                "supplemental physical marker is absent" in item
                for item in result["limitations"]
            )
        )

        self.fixture.make_rosetta()
        original = static.read_regular_file

        def root_owned(path: Path, *, max_bytes: int) -> static.FileSnapshot:
            snapshot = original(path, max_bytes=max_bytes)
            if path in self.fixture.rosetta_markers:
                return dataclasses.replace(snapshot, uid=0)
            return snapshot

        with mock.patch.object(static, "read_regular_file", side_effect=root_owned):
            result = static.inspect_rosetta(self.fixture.paths)
        self.assertEqual("matched", result["status"])
        self.assertEqual(4, len(result["evidence"]))

    def test_valid_package_graph_matches_and_hash_is_order_independent(self) -> None:
        self.fixture.make_package_graph()
        first = static.inspect_package_graph(self.fixture.paths)
        self.assertEqual("matched", first["graph_status"])
        self.assertTrue(
            all(item["status"] == "matched" for item in first["requirements"])
        )

        manifest = self.fixture.valid_manifest()
        dependencies = manifest["dependencies"]
        assert isinstance(dependencies, dict)
        manifest["dependencies"] = dict(reversed(list(dependencies.items())))
        lock = self.fixture.valid_lock()
        lock_dependencies = lock["dependencies"]
        assert isinstance(lock_dependencies, dict)
        lock["dependencies"] = dict(reversed(list(lock_dependencies.items())))
        self.fixture.make_package_graph(manifest=manifest, lock=lock)
        second = static.inspect_package_graph(self.fixture.paths)
        self.assertEqual(
            first["canonical_graph_sha256"],
            second["canonical_graph_sha256"],
        )

    def test_duplicate_key_in_package_json_makes_graph_unsafe(self) -> None:
        manifest_path = self.fixture.project / "Packages" / "manifest.json"
        self.fixture.write(
            manifest_path,
            (
                b'{"dependencies":{},'
                b'"dependencies":{"com.unity.ai.assistant":"2.14.0-pre.1"}}'
            ),
        )
        self.fixture.write_json(
            self.fixture.project / "Packages" / "packages-lock.json",
            self.fixture.valid_lock(),
        )
        result = static.inspect_package_graph(self.fixture.paths)
        self.assertEqual("unsafe", result["graph_status"])
        self.assertTrue(
            all(item["status"] == "unsafe" for item in result["requirements"])
        )

    def test_headline_packages_must_come_from_unity_registry(self) -> None:
        lock = self.fixture.valid_lock()
        dependencies = lock["dependencies"]
        assert isinstance(dependencies, dict)
        assistant = dependencies["com.unity.ai.assistant"]
        assert isinstance(assistant, dict)
        assistant["source"] = "builtin"
        assistant["url"] = None
        self.fixture.make_package_graph(lock=lock)
        result = static.inspect_package_graph(self.fixture.paths)
        self.assertEqual("unsafe", result["graph_status"])
        self.assertEqual(
            "unsafe",
            self.by_id(result["requirements"])["UNITY-AI-ASSISTANT"]["status"],
        )

    def test_unresolved_or_version_mismatched_package_edge_is_unsafe(self) -> None:
        lock = self.fixture.valid_lock()
        dependencies = lock["dependencies"]
        assert isinstance(dependencies, dict)
        urp = dependencies["com.unity.render-pipelines.universal"]
        assert isinstance(urp, dict)
        urp["dependencies"] = {"com.unity.render-pipelines.core": "17.3.0"}
        dependencies["com.unity.render-pipelines.core"] = {
            "version": "17.3.1",
            "depth": 1,
            "source": "registry",
            "url": "https://packages.unity.com",
            "dependencies": {},
        }
        self.fixture.make_package_graph(lock=lock)
        result = static.inspect_package_graph(self.fixture.paths)
        self.assertEqual("unsafe", result["graph_status"])
        self.assertTrue(
            any("unresolved or mismatched edge" in item for item in result["limitations"])
        )

    def test_orphan_lock_record_is_unsafe(self) -> None:
        lock = self.fixture.valid_lock()
        dependencies = lock["dependencies"]
        assert isinstance(dependencies, dict)
        dependencies["com.unity.unreachable"] = {
            "version": "1.0.0",
            "depth": 1,
            "source": "registry",
            "url": "https://packages.unity.com",
            "dependencies": {},
        }
        self.fixture.make_package_graph(lock=lock)
        result = static.inspect_package_graph(self.fixture.paths)
        self.assertEqual("unsafe", result["graph_status"])
        self.assertTrue(
            any("unreachable" in item or "orphan" in item for item in result["limitations"])
        )

    def test_cyclic_package_lock_is_unsafe(self) -> None:
        lock = self.fixture.valid_lock()
        dependencies = lock["dependencies"]
        assert isinstance(dependencies, dict)
        assistant = dependencies["com.unity.ai.assistant"]
        assert isinstance(assistant, dict)
        assistant["dependencies"] = {
            "com.unity.ai.assistant": static.REQUIRED_ASSISTANT
        }
        self.fixture.make_package_graph(lock=lock)
        result = static.inspect_package_graph(self.fixture.paths)
        self.assertEqual("unsafe", result["graph_status"])
        self.assertIn(
            "lock dependency graph contains a cycle",
            result["limitations"],
        )

    def test_direct_package_depth_must_be_zero(self) -> None:
        manifest = self.fixture.valid_manifest()
        dependencies = manifest["dependencies"]
        assert isinstance(dependencies, dict)
        dependencies["com.unity.extra"] = "1.0.0"
        lock = self.fixture.valid_lock()
        lock_dependencies = lock["dependencies"]
        assert isinstance(lock_dependencies, dict)
        lock_dependencies["com.unity.extra"] = {
            "version": "1.0.0",
            "depth": 2,
            "source": "registry",
            "url": "https://packages.unity.com",
            "dependencies": {},
        }
        self.fixture.make_package_graph(manifest=manifest, lock=lock)
        result = static.inspect_package_graph(self.fixture.paths)
        self.assertEqual("unsafe", result["graph_status"])
        self.assertTrue(any("depth" in item for item in result["limitations"]))

    def test_profile_derivation_requires_exact_nine_unique_requirements(self) -> None:
        records = [
            static.requirement(item, "required", "observed", "matched")
            for item in sorted(EXPECTED_REQUIREMENT_IDS)
        ]
        toolchain = records[:6]
        packages = records[6:]
        self.assertEqual(
            ("matched", []),
            static.derive_profile_status(toolchain, packages),
        )
        self.assertEqual(
            ("matched", []),
            static.derive_profile_status(
                list(reversed(toolchain)),
                list(reversed(packages)),
            ),
        )
        self.assertEqual(
            ("indeterminate", []),
            static.derive_profile_status(toolchain[:-1], packages),
        )
        duplicate_missing = toolchain[:-1] + [toolchain[0]]
        self.assertEqual(
            ("indeterminate", []),
            static.derive_profile_status(duplicate_missing, packages),
        )
        unknown = records + [
            static.requirement("UNKNOWN", "required", "observed", "matched")
        ]
        self.assertEqual(
            ("indeterminate", []),
            static.derive_profile_status(unknown[:6], unknown[6:]),
        )

    def test_profile_blocking_ids_are_sorted_deterministically(self) -> None:
        statuses = {
            "URP": "mismatch",
            "DOTNET-SDK": "unverified",
            "UNITY-EDITOR": "missing",
        }
        records = [
            static.requirement(
                item,
                "required",
                "observed",
                statuses.get(item, "matched"),
            )
            for item in sorted(EXPECTED_REQUIREMENT_IDS)
        ]
        self.assertEqual(
            ("blocked", ["UNITY-EDITOR", "URP"]),
            static.derive_profile_status(records, []),
        )
        self.assertEqual(
            ("blocked", ["UNITY-EDITOR", "URP"]),
            static.derive_profile_status(list(reversed(records)), []),
        )

    def test_exact_matched_tuple_remains_a0_and_non_authoritative(self) -> None:
        matched = {
            item: static.requirement(item, "required", "observed", "matched")
            for item in EXPECTED_REQUIREMENT_IDS
        }
        package_graph = {
            "project_root": str(self.fixture.project),
            "manifest": {"path": "fixture", "sha256": "1" * 64},
            "lock": {"path": "fixture", "sha256": "2" * 64},
            "graph_status": "matched",
            "canonical_graph_sha256": "3" * 64,
            "requirements": [
                matched["UNITY-AI-ASSISTANT"],
                matched["URP"],
                matched["UNITY-TEST-FRAMEWORK"],
            ],
            "limitations": [],
        }
        with (
            mock.patch.object(static, "inspect_hub", return_value=matched["UNITY-HUB"]),
            mock.patch.object(
                static,
                "inspect_editor",
                return_value=matched["UNITY-EDITOR"],
            ),
            mock.patch.object(
                static,
                "inspect_il2cpp",
                return_value=matched["MAC-BUILD-SUPPORT-IL2CPP"],
            ),
            mock.patch.object(static, "inspect_xcode", return_value=matched["XCODE"]),
            mock.patch.object(
                static,
                "inspect_dotnet",
                return_value=matched["DOTNET-SDK"],
            ),
            mock.patch.object(
                static,
                "inspect_rosetta",
                return_value=matched["ROSETTA-2"],
            ),
            mock.patch.object(
                static,
                "inspect_package_graph",
                return_value=package_graph,
            ),
        ):
            observation = static.collect_observation(
                self.fixture.paths,
                captured_at="2026-07-16T12:34:56Z",
                base_commit="a" * 40,
                source_path=self.fixture.source,
                host={
                    "os": "macOS",
                    "os_version": "fixture",
                    "architecture": "arm64",
                },
            )
            non_arm_observation = static.collect_observation(
                self.fixture.paths,
                captured_at="2026-07-16T12:34:57Z",
                base_commit="a" * 40,
                source_path=self.fixture.source,
                host={
                    "os": "macOS",
                    "os_version": "fixture",
                    "architecture": "x86_64",
                },
            )
            with self.assertRaises(static.UnsafeEvidence):
                static.collect_observation(
                    self.fixture.paths,
                    captured_at="2026-07-16T12:34:58Z",
                    base_commit="a" * 40,
                    source_path=self.fixture.source,
                    host={
                        "os": "macOS",
                        "os_version": "fixture",
                        "architecture": "sk-proj-abcdefghijklmnop",
                    },
                )
        self.assertEqual("matched", observation["profile_status"])
        self.assertEqual("indeterminate", non_arm_observation["profile_status"])
        self.assertEqual(
            static.DOCUMENT_KIND,
            observation["document_kind"],
        )
        self.assertEqual("A0", observation["authority"]["current_autonomy"])
        self.assertFalse(observation["authority"]["activation_authority"])
        self.assertFalse(
            observation["authority"]["activation_evidence_eligible"]
        )
        self.assertFalse(
            observation["collection"]["unity_family_processes_invoked"]
        )
        self.assertFalse(observation["collection"]["external_processes_invoked"])
        self.assertFalse(observation["collection"]["network_accessed"])
        schema = foundation.load_json(
            foundation.ROOT
            / "schemas"
            / "wp0001-static-host-toolchain-observation.schema.json"
        )
        self.assertEqual(
            [],
            foundation.validate_schema_subset(
                observation,
                schema,
                schema,
                "synthetic static toolchain observation",
            ),
        )
        wrong_partition = copy.deepcopy(observation)
        wrong_partition["toolchain"][0]["id"] = "UNITY-EDITOR"
        self.assertTrue(
            foundation.validate_schema_subset(
                wrong_partition,
                schema,
                schema,
                "wrong static toolchain partition",
            )
        )

    def test_current_style_tuple_collects_as_blocked_without_authority(self) -> None:
        self.fixture.make_hub()
        wrong_editor = self.fixture.make_editor(
            version="6000.5.4f1",
            changeset="d550df8bd089",
        )
        self.fixture.make_il2cpp_catalog(wrong_editor)
        self.fixture.make_il2cpp_marker(wrong_editor)
        self.fixture.xcode_select.parent.mkdir(parents=True)
        self.fixture.xcode_select.symlink_to("/Library/Developer/CommandLineTools")
        self.fixture.make_package_graph(
            manifest=self.fixture.valid_manifest(urp="17.5.0", tests="1.7.0"),
            lock=self.fixture.valid_lock(urp="17.5.0", tests="1.7.0"),
        )

        observation = static.collect_observation(
            self.fixture.paths,
            captured_at="2026-07-16T12:34:56Z",
            base_commit="b" * 40,
            source_path=self.fixture.source,
            host={
                "os": "macOS",
                "os_version": "fixture",
                "architecture": "arm64",
            },
        )
        records = self.by_id(
            observation["toolchain"]
            + observation["package_graph"]["requirements"]
        )
        self.assertEqual("matched", records["UNITY-HUB"]["status"])
        self.assertEqual("mismatch", records["UNITY-EDITOR"]["status"])
        self.assertEqual("missing", records["MAC-BUILD-SUPPORT-IL2CPP"]["status"])
        self.assertEqual("missing", records["XCODE"]["status"])
        self.assertEqual("unverified", records["DOTNET-SDK"]["status"])
        self.assertEqual("mismatch", records["URP"]["status"])
        self.assertEqual("mismatch", records["UNITY-TEST-FRAMEWORK"]["status"])
        self.assertEqual("blocked", observation["profile_status"])
        self.assertFalse(observation["authority"]["activation_authority"])


if __name__ == "__main__":
    unittest.main()
