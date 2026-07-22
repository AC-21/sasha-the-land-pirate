from __future__ import annotations

import copy
import importlib.util
import json
import os
import stat
import tempfile
import unittest
from pathlib import Path
from types import ModuleType


REPO_ROOT = Path(__file__).resolve().parents[3]


def load_tool(name: str) -> ModuleType:
    path = REPO_ROOT / "Tools" / "Validation" / f"{name}.py"
    spec = importlib.util.spec_from_file_location(f"test_{name}", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


PACKAGE_GRAPH = load_tool("validate_wp0002_package_graph")
ENTRY_GATE = load_tool("validate_wp0002_entry_gate")


class WP0002ReportWriterSecurityTests(unittest.TestCase):
    WRITERS = (PACKAGE_GRAPH._write_report, ENTRY_GATE._write_report)

    def test_valid_report_is_regular_private_and_inside_allowed_root(self) -> None:
        for writer in self.WRITERS:
            with self.subTest(writer=writer.__module__), tempfile.TemporaryDirectory() as td:
                root = Path(td)
                writer(
                    root,
                    "BuildArtifacts/WP-0002/local-only/report.json",
                    {"result": "pass"},
                )
                report = root / "BuildArtifacts/WP-0002/local-only/report.json"
                self.assertEqual(json.loads(report.read_text()), {"result": "pass"})
                self.assertTrue(stat.S_ISREG(report.lstat().st_mode))
                self.assertEqual(stat.S_IMODE(report.lstat().st_mode), 0o600)

    def test_lexical_parent_escape_is_rejected(self) -> None:
        for writer in self.WRITERS:
            with self.subTest(writer=writer.__module__), tempfile.TemporaryDirectory() as td:
                root = Path(td)
                with self.assertRaises(ValueError):
                    writer(
                        root,
                        "BuildArtifacts/WP-0002/../escaped.json",
                        {"result": "fail"},
                    )
                self.assertFalse((root / "BuildArtifacts/escaped.json").exists())

    def test_symlinked_allowed_root_cannot_escape_repository(self) -> None:
        for writer in self.WRITERS:
            with self.subTest(writer=writer.__module__), tempfile.TemporaryDirectory() as td:
                root = Path(td) / "repo"
                outside = Path(td) / "outside"
                (root / "BuildArtifacts").mkdir(parents=True)
                outside.mkdir()
                (root / "BuildArtifacts/WP-0002").symlink_to(
                    outside, target_is_directory=True
                )
                with self.assertRaises((OSError, ValueError)):
                    writer(
                        root,
                        "BuildArtifacts/WP-0002/report.json",
                        {"result": "fail"},
                    )
                self.assertFalse((outside / "report.json").exists())

    def test_non_directory_parent_component_is_rejected(self) -> None:
        for writer in self.WRITERS:
            with self.subTest(writer=writer.__module__), tempfile.TemporaryDirectory() as td:
                root = Path(td)
                (root / "BuildArtifacts").mkdir()
                (root / "BuildArtifacts/WP-0002").write_text("not a directory")
                with self.assertRaises((OSError, ValueError)):
                    writer(
                        root,
                        "BuildArtifacts/WP-0002/report.json",
                        {"result": "fail"},
                    )

    def test_destination_symlink_and_nonregular_mode_are_rejected(self) -> None:
        for writer in self.WRITERS:
            for destination_kind in ("symlink", "fifo"):
                with (
                    self.subTest(
                        writer=writer.__module__, destination_kind=destination_kind
                    ),
                    tempfile.TemporaryDirectory() as td,
                ):
                    root = Path(td) / "repo"
                    allowed = root / "BuildArtifacts/WP-0002"
                    allowed.mkdir(parents=True)
                    destination = allowed / "report.json"
                    if destination_kind == "symlink":
                        outside = Path(td) / "outside.json"
                        outside.write_text("unchanged")
                        destination.symlink_to(outside)
                    else:
                        os.mkfifo(destination)
                    with self.assertRaises(ValueError):
                        writer(
                            root,
                            "BuildArtifacts/WP-0002/report.json",
                            {"result": "fail"},
                        )
                    if destination_kind == "symlink":
                        self.assertEqual(outside.read_text(), "unchanged")

    def test_symlink_repository_root_is_rejected(self) -> None:
        for writer in self.WRITERS:
            with self.subTest(writer=writer.__module__), tempfile.TemporaryDirectory() as td:
                actual = Path(td) / "actual"
                link = Path(td) / "repo-link"
                actual.mkdir()
                link.symlink_to(actual, target_is_directory=True)
                with self.assertRaises(ValueError):
                    writer(
                        link,
                        "BuildArtifacts/WP-0002/report.json",
                        {"result": "fail"},
                    )


class WP0002PackageGraphTypeTests(unittest.TestCase):
    @staticmethod
    def documents() -> tuple[dict[str, bytes], dict[str, bytes]]:
        base_manifest = {"dependencies": {"existing": "1.2.3"}}
        base_lock = {
            "dependencies": {
                "existing": {
                    "version": "1.2.3",
                    "depth": 0,
                    "source": "registry",
                    "dependencies": {},
                }
            }
        }
        candidate_manifest = copy.deepcopy(base_manifest)
        candidate_manifest["dependencies"].update(
            PACKAGE_GRAPH.AUTHORIZED_MANIFEST_ADDITIONS
        )
        candidate_lock = copy.deepcopy(base_lock)
        candidate_lock["dependencies"].update(
            PACKAGE_GRAPH.AUTHORIZED_LOCK_ADDITIONS
        )

        def encoded(value: object) -> bytes:
            return json.dumps(value, sort_keys=True).encode("utf-8") + b"\n"

        base = {
            "Game/Packages/manifest.json": encoded(base_manifest),
            "Game/Packages/packages-lock.json": encoded(base_lock),
            "SimulationCore/package.json": b'{"name":"simulation"}\n',
            "SaveContracts/package.json": b'{"name":"save"}\n',
        }
        candidate = {
            "Game/Packages/manifest.json": encoded(candidate_manifest),
            "Game/Packages/packages-lock.json": encoded(candidate_lock),
            "SimulationCore/package.json": base["SimulationCore/package.json"],
            "SaveContracts/package.json": base["SaveContracts/package.json"],
        }
        return base, candidate

    @staticmethod
    def rewrite_lock(files: dict[str, bytes], lock: dict) -> None:
        files["Game/Packages/packages-lock.json"] = (
            json.dumps(lock, sort_keys=True).encode("utf-8") + b"\n"
        )

    @staticmethod
    def rewrite_manifest(files: dict[str, bytes], manifest: dict) -> None:
        files["Game/Packages/manifest.json"] = (
            json.dumps(manifest, sort_keys=True).encode("utf-8") + b"\n"
        )

    def test_exact_integer_depth_passes(self) -> None:
        base, candidate = self.documents()
        self.assertEqual(PACKAGE_GRAPH.compare_package_graph(base, candidate), [])

    def test_materialized_last_bearing_rejects_unchanged_graph(self) -> None:
        base, _ = self.documents()
        unchanged = copy.deepcopy(base)
        self.assertEqual(PACKAGE_GRAPH.compare_package_graph(base, unchanged), [])
        errors = PACKAGE_GRAPH.compare_package_graph(
            base,
            unchanged,
            require_links=True,
        )
        self.assertTrue(any("requires the exact authorized" in error for error in errors), errors)

    def test_current_tree_materialization_includes_symlinks(self) -> None:
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            self.assertFalse(PACKAGE_GRAPH._last_bearing_materialized(root))
            path = root / PACKAGE_GRAPH.LAST_BEARING_PATHS[0]
            path.parent.mkdir(parents=True)
            path.symlink_to(root / "missing-target", target_is_directory=True)
            self.assertTrue(PACKAGE_GRAPH._last_bearing_materialized(root))

    def test_pipeline_manifest_version_and_extra_package_are_rejected(self) -> None:
        for mutation in ("wrong-version", "extra-package", "missing-pipeline"):
            with self.subTest(mutation=mutation):
                base, candidate = self.documents()
                manifest = json.loads(candidate["Game/Packages/manifest.json"])
                dependencies = manifest["dependencies"]
                if mutation == "wrong-version":
                    dependencies["com.unity.pipeline"] = "0.3.1-exp.2"
                elif mutation == "extra-package":
                    dependencies["com.example.extra"] = "1.0.0"
                else:
                    del dependencies["com.unity.pipeline"]
                self.rewrite_manifest(candidate, manifest)
                self.assertTrue(PACKAGE_GRAPH.compare_package_graph(base, candidate))

    def test_pipeline_lock_fields_are_exact(self) -> None:
        mutations = {
            "version": "0.3.1-exp.2",
            "depth": 1,
            "source": "local",
            "url": "https://example.invalid",
        }
        for field, value in mutations.items():
            with self.subTest(field=field):
                base, candidate = self.documents()
                lock = json.loads(candidate["Game/Packages/packages-lock.json"])
                lock["dependencies"]["com.unity.pipeline"][field] = value
                self.rewrite_lock(candidate, lock)
                self.assertTrue(PACKAGE_GRAPH.compare_package_graph(base, candidate))

    def test_pipeline_direct_dependency_map_is_exact(self) -> None:
        base, candidate = self.documents()
        lock = json.loads(candidate["Game/Packages/packages-lock.json"])
        lock["dependencies"]["com.unity.pipeline"]["dependencies"][
            "com.unity.test-framework"
        ] = "1.1.34"
        self.rewrite_lock(candidate, lock)
        self.assertTrue(PACKAGE_GRAPH.compare_package_graph(base, candidate))

    def test_screencapture_lock_entry_is_exact(self) -> None:
        mutations = {
            "version": "2.0.0",
            "depth": 2,
            "source": "registry",
            "dependencies": {},
        }
        for field, value in mutations.items():
            with self.subTest(field=field):
                base, candidate = self.documents()
                lock = json.loads(candidate["Game/Packages/packages-lock.json"])
                lock["dependencies"]["com.unity.modules.screencapture"][field] = value
                self.rewrite_lock(candidate, lock)
                self.assertTrue(PACKAGE_GRAPH.compare_package_graph(base, candidate))

    def test_missing_or_extra_lock_entry_is_rejected(self) -> None:
        for mutation in ("missing-pipeline", "missing-screencapture", "extra"):
            with self.subTest(mutation=mutation):
                base, candidate = self.documents()
                lock = json.loads(candidate["Game/Packages/packages-lock.json"])
                dependencies = lock["dependencies"]
                if mutation == "missing-pipeline":
                    del dependencies["com.unity.pipeline"]
                elif mutation == "missing-screencapture":
                    del dependencies["com.unity.modules.screencapture"]
                else:
                    dependencies["com.example.extra"] = {
                        "version": "1.0.0",
                        "depth": 0,
                        "source": "registry",
                        "dependencies": {},
                    }
                self.rewrite_lock(candidate, lock)
                self.assertTrue(PACKAGE_GRAPH.compare_package_graph(base, candidate))

    def test_new_local_depth_bool_or_float_is_rejected(self) -> None:
        for wrong_depth in (False, 0.0):
            with self.subTest(wrong_depth=repr(wrong_depth)):
                base, candidate = self.documents()
                lock = json.loads(candidate["Game/Packages/packages-lock.json"])
                lock["dependencies"]["com.ac21.sasha.simulation-core"][
                    "depth"
                ] = wrong_depth
                self.rewrite_lock(candidate, lock)
                self.assertTrue(PACKAGE_GRAPH.compare_package_graph(base, candidate))

    def test_retained_depth_type_drift_is_rejected(self) -> None:
        for wrong_depth in (False, 0.0):
            with self.subTest(wrong_depth=repr(wrong_depth)):
                base, candidate = self.documents()
                lock = json.loads(candidate["Game/Packages/packages-lock.json"])
                lock["dependencies"]["existing"]["depth"] = wrong_depth
                self.rewrite_lock(candidate, lock)
                errors = PACKAGE_GRAPH.compare_package_graph(base, candidate)
                self.assertTrue(
                    any("changed existing dependency existing" in error for error in errors),
                    errors,
                )


if __name__ == "__main__":
    unittest.main()
