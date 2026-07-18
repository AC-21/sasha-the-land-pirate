from __future__ import annotations

import hashlib
import importlib.util
import os
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[1]
TOOL_PATH = (
    ROOT.parents[1]
    / "Tools"
    / "Validation"
    / "collect_wp0002_scope_capture_successor.py"
)
SPEC = importlib.util.spec_from_file_location(
    "collect_wp0002_scope_capture_successor",
    TOOL_PATH,
)
assert SPEC is not None and SPEC.loader is not None
collector = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(collector)


CAPTURE_TIME = datetime(2026, 7, 18, 8, 0, 0, tzinfo=timezone.utc)
RESERVATION = ["Game/Assets/AtomicLandPirate/LastBearing/"]
PROTECTED = [
    "docs/foundation-v0.1/",
    "Game/ProjectSettings/ProjectSettings.asset",
    "Game/ProjectSettings/SceneTemplateSettings.json",
]


class WP0002SuccessorScopeCaptureTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.repo = Path(os.path.realpath(self.temporary.name)) / "repo"
        self.repo.mkdir()
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Successor Scope Test")
        self.git("config", "user.email", "scope-successor@example.invalid")
        self.write(
            "Game/ProjectSettings/ProjectSettings.asset",
            b"protected project settings\n",
        )
        self.git("add", "Game/ProjectSettings/ProjectSettings.asset")
        self.git("commit", "-m", "protected main")
        self.base = self.git("rev-parse", "HEAD").stdout.decode().strip()
        self.git("remote", "add", "origin", collector._v1.CANONICAL_REMOTE_URL)
        self.git("update-ref", "refs/remotes/origin/main", self.base)
        self.write(
            "Game/ProjectSettings/ProjectSettings.asset",
            b"creator-owned project settings drift\n",
        )
        self.write(
            "Game/ProjectSettings/SceneTemplateSettings.json",
            b'{"creator":"scene-template"}\n',
        )
        for sequence, gate_id in enumerate(
            collector._v1.AMENDMENT_GATE_IDS,
            start=1,
        ):
            self.write(
                "BuildArtifacts/WP-0002/unity-gates/"
                f"{gate_id}-{sequence:032x}.json",
                b'{"result":"pass"}\n',
            )

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def git(self, *args: str) -> subprocess.CompletedProcess[bytes]:
        result = subprocess.run(
            ["/usr/bin/git", "-C", str(self.repo), *args],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        if result.returncode != 0:
            self.fail(result.stderr.decode("utf-8", "replace"))
        return result

    def write(self, relative: str, data: bytes) -> None:
        path = self.repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def collect(self) -> dict:
        return collector.collect_scope_capture(
            self.repo,
            base_commit=self.base,
            checkpoint_commit=self.base,
            reservation_paths=RESERVATION,
            protected_paths_read_only=PROTECTED,
            output_relative=collector.SUCCESSOR_RETAINED_CAPTURE,
            captured_at=CAPTURE_TIME,
            expected_repository_root=str(self.repo),
            evidence_root=self.repo,
            status_arguments=collector._v1.AMENDMENT_STATUS_ARGUMENTS,
        )

    def test_successor_capture_uses_fresh_namespace_and_validates(self) -> None:
        result = self.collect()
        capture = result["capture"]
        self.assertEqual(
            capture["capture_contract"],
            collector.SUCCESSOR_CAPTURE_CONTRACT,
        )
        self.assertEqual(result["path"], collector.SUCCESSOR_RETAINED_CAPTURE)
        self.assertTrue(
            all(
                reference["path"].startswith(
                    f"{collector.SUCCESSOR_OUTPUT_ROOT}/"
                )
                for reference in capture["artifacts"].values()
            )
        )
        with mock.patch.object(
            collector._v1,
            "CANONICAL_AMENDMENT_ROOT",
            str(self.repo),
        ):
            self.assertEqual(
                collector.verify_scope_capture(
                    self.repo,
                    result["path"],
                    expected_capture_sha256=result["sha256"],
                    expected_base_commit=self.base,
                    expected_head_commit=self.base,
                    expected_checkpoint_commit=self.base,
                    expected_reservation_paths=RESERVATION,
                    expected_protected_paths=PROTECTED,
                    receipt_issued_at="2026-07-18T08:00:30Z",
                    mode="terminal-retained",
                    now=CAPTURE_TIME + timedelta(seconds=60),
                    status_arguments=collector._v1.AMENDMENT_STATUS_ARGUMENTS,
                ),
                [],
            )

    def test_successor_artifacts_do_not_reuse_v1_namespace(self) -> None:
        result = self.collect()
        self.assertNotIn("local-operator-amendment", result["path"])
        for reference in result["capture"]["artifacts"].values():
            self.assertNotIn("local-operator-amendment", reference["path"])

    def test_loader_restores_absent_none_and_existing_entries(self) -> None:
        module_name = collector._MODULE_NAME
        original = sys.modules.pop(module_name, collector._MISSING)
        try:
            collector._load_v1()
            self.assertNotIn(module_name, sys.modules)
            for sentinel in (None, object()):
                sys.modules[module_name] = sentinel
                collector._load_v1()
                self.assertIs(sys.modules[module_name], sentinel)
        finally:
            if original is collector._MISSING:
                sys.modules.pop(module_name, None)
            else:
                sys.modules[module_name] = original

    def test_loader_hashes_before_execution_and_cleans_up_exception(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            source_path = Path(directory) / "collector.py"
            source = b"raise RuntimeError('must not survive')\n"
            source_path.write_bytes(source)
            module_name = collector._MODULE_NAME
            sentinel = object()
            sys.modules[module_name] = sentinel
            with mock.patch.object(collector, "V1_COLLECTOR_PATH", source_path):
                with mock.patch.object(
                    collector,
                    "V1_COLLECTOR_SHA256",
                    "0" * 64,
                ):
                    with self.assertRaisesRegex(
                        collector.SuccessorCollectorError,
                        "hash mismatch",
                    ):
                        collector._load_v1()
                self.assertIs(sys.modules[module_name], sentinel)
                with mock.patch.object(
                    collector,
                    "V1_COLLECTOR_SHA256",
                    hashlib.sha256(source).hexdigest(),
                ):
                    with self.assertRaisesRegex(
                        collector.SuccessorCollectorError,
                        "cannot load",
                    ):
                        collector._load_v1()
                self.assertIs(sys.modules[module_name], sentinel)
            sys.modules.pop(module_name, None)


if __name__ == "__main__":
    unittest.main()
