from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import subprocess
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TOOL_PATH = (
    ROOT.parents[1] / "Tools" / "Validation" / "collect_wp0002_scope_capture.py"
)
SPEC = importlib.util.spec_from_file_location("collect_wp0002_scope_capture", TOOL_PATH)
assert SPEC is not None and SPEC.loader is not None
capture_tool = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(capture_tool)


CAPTURE_TIME = datetime(2026, 7, 17, 7, 0, 0, tzinfo=timezone.utc)
RECEIPT_TIME = "2026-07-17T07:00:30Z"
CONFIG_BASE = b"base config metadata for scope capture\n"
PROJECT_BASE = b"base project metadata for scope capture\n"
CONFIG_DIRTY = b"creator private config sentinel alpha 88731\n"
PROJECT_DIRTY = b"creator private project sentinel beta 99142\n"
SCENE_DIRTY = b"creator private scene sentinel gamma 66309\n"
RESERVATION = ["Game/Assets/AtomicLandPirate/LastBearing/"]
PROTECTED = ["docs/foundation-v0.1/", ".codex/config.toml"]


class WP0002ScopeCaptureTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.repo = Path(self.temporary.name) / "repo"
        self.repo.mkdir()
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Scope Test")
        self.git("config", "user.email", "scope@example.invalid")
        self.write(".gitignore", b"/BuildArtifacts/**/local-only/\n")
        self.write(".codex/config.toml", CONFIG_BASE)
        self.write("Game/ProjectSettings/ProjectSettings.asset", PROJECT_BASE)
        self.git("add", ".gitignore", ".codex/config.toml", "Game/ProjectSettings/ProjectSettings.asset")
        self.git("commit", "-m", "base")
        self.base = self.git("rev-parse", "HEAD").stdout.decode().strip()
        self.write(".codex/config.toml", CONFIG_DIRTY)
        self.write("Game/ProjectSettings/ProjectSettings.asset", PROJECT_DIRTY)
        self.write("Game/ProjectSettings/SceneTemplateSettings.json", SCENE_DIRTY)
        self.result = capture_tool.collect_scope_capture(
            self.repo,
            base_commit=self.base,
            checkpoint_commit=self.base,
            reservation_paths=RESERVATION,
            protected_paths_read_only=PROTECTED,
            captured_at=CAPTURE_TIME,
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

    def capture_path(self) -> Path:
        return self.repo / self.result["path"]

    def capture(self) -> dict:
        return json.loads(self.capture_path().read_text(encoding="utf-8"))

    def rewrite_capture(self, capture: dict) -> None:
        data = capture_tool._json_bytes(capture)
        self.capture_path().write_bytes(data)
        self.result["sha256"] = hashlib.sha256(data).hexdigest()

    def artifact_path(self, name: str) -> Path:
        return self.repo / self.capture()["artifacts"][name]["path"]

    def rewrite_artifact(self, name: str, data: bytes) -> None:
        capture = self.capture()
        path = self.repo / capture["artifacts"][name]["path"]
        path.write_bytes(data)
        capture["artifacts"][name]["sha256"] = hashlib.sha256(data).hexdigest()
        capture["artifacts"][name]["byte_size"] = len(data)
        self.rewrite_capture(capture)

    def verify(
        self,
        *,
        mode: str = "live-current",
        receipt_time: str = RECEIPT_TIME,
        now: datetime | None = None,
    ) -> list[str]:
        return capture_tool.verify_scope_capture(
            self.repo,
            self.result["path"],
            expected_capture_sha256=self.result["sha256"],
            expected_base_commit=self.base,
            expected_head_commit=self.base,
            expected_checkpoint_commit=self.base,
            expected_reservation_paths=RESERVATION,
            expected_protected_paths=PROTECTED,
            receipt_issued_at=receipt_time,
            mode=mode,
            now=now or CAPTURE_TIME + timedelta(seconds=60),
        )

    def test_exact_live_capture_passes_and_retains_no_creator_bytes(self) -> None:
        self.assertEqual(self.verify(), [])
        evidence = self.capture_path().read_bytes()
        capture = self.capture()
        for reference in capture["artifacts"].values():
            evidence += (self.repo / reference["path"]).read_bytes()
        for secret in (CONFIG_DIRTY, PROJECT_DIRTY, SCENE_DIRTY):
            self.assertNotIn(secret, evidence)
        observations = json.loads(
            self.artifact_path("observations").read_text(encoding="utf-8")
        )
        self.assertEqual(
            observations["content_policy"],
            "metadata-only-no-creator-file-bytes",
        )
        self.assertTrue(
            all(
                item["file_bytes"]["content_retained"] is False
                for item in observations["observations"]
            )
        )

    def test_pre_stage_c_collector_can_materialize_retained_terminal_artifacts(self) -> None:
        retained_output = capture_tool.RETAINED_CAPTURE
        retained = capture_tool.collect_scope_capture(
            self.repo,
            base_commit=self.base,
            checkpoint_commit=self.base,
            reservation_paths=RESERVATION,
            protected_paths_read_only=PROTECTED,
            output_relative=retained_output,
            captured_at=CAPTURE_TIME,
        )
        errors = capture_tool.verify_scope_capture(
            self.repo,
            retained["path"],
            expected_capture_sha256=retained["sha256"],
            expected_base_commit=self.base,
            expected_head_commit=self.base,
            expected_checkpoint_commit=self.base,
            expected_reservation_paths=RESERVATION,
            expected_protected_paths=PROTECTED,
            receipt_issued_at=RECEIPT_TIME,
            mode="terminal-retained",
        )
        self.assertEqual(errors, [])
        capture = json.loads(
            (self.repo / retained["path"]).read_text(encoding="utf-8")
        )
        self.assertEqual(
            capture["artifacts"]["raw_status"]["path"],
            (
                f"{capture_tool.RETAINED_OUTPUT_ROOT}/working-tree-scope.status."
                f"{capture['artifacts']['raw_status']['sha256']}.bin"
            ),
        )
        self.assertEqual(
            capture["artifacts"]["observations"]["path"],
            (
                f"{capture_tool.RETAINED_OUTPUT_ROOT}/working-tree-scope.observations."
                f"{capture['artifacts']['observations']['sha256']}.json"
            ),
        )

    def test_retained_artifact_alias_is_rejected(self) -> None:
        retained = capture_tool.collect_scope_capture(
            self.repo,
            base_commit=self.base,
            checkpoint_commit=self.base,
            reservation_paths=RESERVATION,
            protected_paths_read_only=PROTECTED,
            output_relative=capture_tool.RETAINED_CAPTURE,
            captured_at=CAPTURE_TIME,
        )
        capture_path = self.repo / retained["path"]
        original = json.loads(capture_path.read_text(encoding="utf-8"))
        aliases = {
            "raw_status": f"{capture_tool.RETAINED_OUTPUT_ROOT}/raw-status-alias.bin",
            "observations": f"{capture_tool.RETAINED_OUTPUT_ROOT}/observations-alias.json",
        }
        for name, alias in aliases.items():
            with self.subTest(name=name):
                canonical_artifact = self.repo / original["artifacts"][name]["path"]
                (self.repo / alias).write_bytes(canonical_artifact.read_bytes())
                capture = json.loads(json.dumps(original))
                capture["artifacts"][name]["path"] = alias
                capture_data = capture_tool._json_bytes(capture)
                capture_path.write_bytes(capture_data)
                errors = capture_tool.verify_scope_capture(
                    self.repo,
                    retained["path"],
                    expected_capture_sha256=hashlib.sha256(capture_data).hexdigest(),
                    expected_base_commit=self.base,
                    expected_head_commit=self.base,
                    expected_checkpoint_commit=self.base,
                    expected_reservation_paths=RESERVATION,
                    expected_protected_paths=PROTECTED,
                    receipt_issued_at=RECEIPT_TIME,
                    mode="terminal-retained",
                )
                self.assertTrue(
                    any("must use exact path" in error for error in errors),
                    errors,
                )

    def test_retained_derived_capture_alias_is_rejected(self) -> None:
        alias = f"{capture_tool.RETAINED_OUTPUT_ROOT}/capture-alias.json"
        (self.repo / capture_tool.RETAINED_OUTPUT_ROOT).mkdir(
            parents=True, exist_ok=True
        )
        (self.repo / alias).write_bytes(self.capture_path().read_bytes())
        errors = capture_tool.verify_scope_capture(
            self.repo,
            alias,
            expected_capture_sha256=self.result["sha256"],
            expected_base_commit=self.base,
            expected_head_commit=self.base,
            expected_checkpoint_commit=self.base,
            expected_reservation_paths=RESERVATION,
            expected_protected_paths=PROTECTED,
            receipt_issued_at=RECEIPT_TIME,
            mode="terminal-retained",
        )
        self.assertTrue(any("cannot be resolved" in error for error in errors), errors)

    def test_fabricated_derived_json_is_rejected(self) -> None:
        capture = self.capture()
        capture["dirty_paths"][0]["observed_sha256"] = "0" * 64
        self.rewrite_capture(capture)
        errors = self.verify(mode="terminal-retained")
        self.assertTrue(any("faithful projection" in error for error in errors), errors)

    def test_wrong_raw_porcelain_is_rejected_even_when_rehashed(self) -> None:
        self.rewrite_artifact("raw_status", b"? extra.txt\0")
        errors = self.verify(mode="terminal-retained")
        self.assertTrue(any("raw status dirty set differs" in error for error in errors), errors)

    def test_tampered_observation_is_rejected_even_when_rehashed(self) -> None:
        observations = json.loads(
            self.artifact_path("observations").read_text(encoding="utf-8")
        )
        observations["observations"][0]["file_bytes"]["sha256"] = "f" * 64
        self.rewrite_artifact("observations", capture_tool._json_bytes(observations))
        errors = self.verify()
        self.assertTrue(
            any("file-byte observation drifted" in error for error in errors),
            errors,
        )

    def test_observation_artifact_cannot_embed_creator_file_content(self) -> None:
        observations = json.loads(
            self.artifact_path("observations").read_text(encoding="utf-8")
        )
        observations["observations"][0]["content"] = CONFIG_DIRTY.decode("utf-8")
        self.rewrite_artifact("observations", capture_tool._json_bytes(observations))
        errors = self.verify(mode="terminal-retained")
        self.assertTrue(
            any("unapproved fields" in error for error in errors),
            errors,
        )

    def test_stale_capture_relative_to_receipt_is_rejected(self) -> None:
        errors = self.verify(receipt_time="2026-07-17T08:00:00Z")
        self.assertIn(
            "scope capture is not fresh relative to its activation receipt",
            errors,
        )

    def test_terminal_retention_ignores_current_drift_but_live_mode_rejects_it(self) -> None:
        self.write(".codex/config.toml", b"creator changed after terminal activation\n")
        terminal_errors = self.verify(mode="terminal-retained")
        self.assertEqual(terminal_errors, [])
        live_errors = self.verify(mode="live-current")
        self.assertTrue(
            any(
                "live file-byte observation drifted" in error
                or "live raw porcelain-v2 bytes differ" in error
                for error in live_errors
            ),
            live_errors,
        )

    def test_confined_writer_rejects_symlink_escape_and_non_regular_destination(self) -> None:
        other = Path(self.temporary.name) / "outside"
        other.mkdir()
        evidence_root = self.repo / capture_tool.OUTPUT_ROOT
        for child in sorted(evidence_root.iterdir()):
            child.unlink()
        evidence_root.rmdir()
        evidence_root.parent.mkdir(parents=True, exist_ok=True)
        evidence_root.symlink_to(other, target_is_directory=True)
        with self.assertRaises((OSError, ValueError)):
            capture_tool._write_atomic_confined(
                self.repo,
                capture_tool.DEFAULT_OUTPUT,
                b"forbidden",
            )

        evidence_root.unlink()
        evidence_root.mkdir()
        destination = evidence_root / "not-regular.json"
        destination.mkdir()
        with self.assertRaises(ValueError):
            capture_tool._write_atomic_confined(
                self.repo,
                destination.relative_to(self.repo).as_posix(),
                b"forbidden",
            )


if __name__ == "__main__":
    unittest.main()
