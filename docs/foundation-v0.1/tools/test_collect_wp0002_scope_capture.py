from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path
from unittest import mock


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
PROTECTED = [
    "docs/foundation-v0.1/",
    ".codex/config.toml",
    "Game/ProjectSettings/ProjectSettings.asset",
    "Game/ProjectSettings/SceneTemplateSettings.json",
]


class WP0002ScopeCaptureTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.repo = Path(os.path.realpath(self.temporary.name)) / "repo"
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
        self.git(
            "remote",
            "add",
            "origin",
            capture_tool.CANONICAL_REMOTE_URL,
        )
        self.git("update-ref", "refs/remotes/origin/main", self.base)
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

    def write_amendment_gate_reports(
        self, counts: tuple[int, int, int] = (1, 1, 1)
    ) -> list[str]:
        paths: list[str] = []
        sequence = 1
        for gate_id, count in zip(capture_tool.AMENDMENT_GATE_IDS, counts):
            for _ in range(count):
                path = (
                    "BuildArtifacts/WP-0002/unity-gates/"
                    f"{gate_id}-{sequence:032x}.json"
                )
                self.write(path, b'{"result":"pass"}\n')
                paths.append(path)
                sequence += 1
        return paths

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

    def retained_root(self):
        return mock.patch.object(
            capture_tool,
            "CANONICAL_COLLECTION_ROOT",
            str(self.repo),
        )

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
        with self.retained_root():
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
        with self.retained_root():
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

    def test_terminal_retained_capture_is_portable_to_another_clone(self) -> None:
        with self.retained_root():
            retained = capture_tool.collect_scope_capture(
                self.repo,
                base_commit=self.base,
                checkpoint_commit=self.base,
                reservation_paths=RESERVATION,
                protected_paths_read_only=PROTECTED,
                output_relative=capture_tool.RETAINED_CAPTURE,
                captured_at=CAPTURE_TIME,
            )
            verifier_repo = Path(self.temporary.name) / "verification-clone"
            clone = subprocess.run(
                [
                    "/usr/bin/git",
                    "clone",
                    "--quiet",
                    str(self.repo),
                    str(verifier_repo),
                ],
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
            )
            self.assertEqual(
                clone.returncode,
                0,
                clone.stderr.decode("utf-8", "replace"),
            )
            capture = json.loads(
                (self.repo / retained["path"]).read_text(encoding="utf-8")
            )
            evidence_paths = [
                retained["path"],
                *(reference["path"] for reference in capture["artifacts"].values()),
            ]
            for relative in evidence_paths:
                destination = verifier_repo / relative
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(self.repo / relative, destination)

            terminal_errors = capture_tool.verify_scope_capture(
                verifier_repo,
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
            self.assertEqual(terminal_errors, [])

            live_errors = capture_tool.verify_scope_capture(
                verifier_repo,
                retained["path"],
                expected_capture_sha256=retained["sha256"],
                expected_base_commit=self.base,
                expected_head_commit=self.base,
                expected_checkpoint_commit=self.base,
                expected_reservation_paths=RESERVATION,
                expected_protected_paths=PROTECTED,
                receipt_issued_at=RECEIPT_TIME,
                mode="live-current",
                now=CAPTURE_TIME + timedelta(seconds=60),
            )
            self.assertTrue(
                any(
                    "retained live-current verification requires canonical collection root"
                    in error
                    for error in live_errors
                ),
                live_errors,
            )

    def test_retained_collection_rejects_noncanonical_root_before_git(self) -> None:
        with mock.patch.object(capture_tool, "_git_output") as git_output:
            with self.assertRaisesRegex(ValueError, "canonical collection root"):
                capture_tool.collect_scope_capture(
                    self.repo,
                    base_commit=self.base,
                    checkpoint_commit=self.base,
                    reservation_paths=RESERVATION,
                    protected_paths_read_only=PROTECTED,
                    output_relative=capture_tool.RETAINED_CAPTURE,
                    captured_at=CAPTURE_TIME,
                )
        git_output.assert_not_called()

    def test_retained_terminal_rejects_valid_absolute_root_rebinding(self) -> None:
        with self.retained_root():
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
            capture = json.loads(capture_path.read_text(encoding="utf-8"))
            capture["repository_root"] = "/different/repository"
            capture["collector"]["status_command"][4] = "/different/repository"
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
        self.assertIn(
            "scope capture repository_root differs from the exact boundary",
            errors,
        )

    def test_schema_and_collector_bind_same_canonical_root(self) -> None:
        schema = json.loads(
            (ROOT / "schemas/wp0002-working-tree-scope-capture.schema.json").read_text(
                encoding="utf-8"
            )
        )
        expected = capture_tool.CANONICAL_COLLECTION_ROOT
        self.assertEqual(schema["properties"]["repository_root"]["const"], expected)
        self.assertEqual(
            schema["properties"]["collector"]["properties"]["status_command"][
                "const"
            ][4],
            expected,
        )

    def test_amendment_profile_derives_exact_dirty_owners_and_scope_facts(self) -> None:
        self.write(".codex/config.toml", CONFIG_BASE)
        generated_paths = self.write_amendment_gate_reports((2, 1, 3))
        amendment_reservation = [*RESERVATION, "BuildArtifacts/WP-0002/"]
        amendment = capture_tool.collect_scope_capture(
            self.repo,
            base_commit=self.base,
            checkpoint_commit=self.base,
            reservation_paths=amendment_reservation,
            protected_paths_read_only=PROTECTED,
            output_relative=capture_tool.AMENDMENT_RETAINED_CAPTURE,
            captured_at=CAPTURE_TIME,
            expected_repository_root=str(self.repo),
            evidence_root=self.repo,
            status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
        )
        errors = capture_tool.verify_scope_capture(
            self.repo,
            amendment["path"],
            expected_capture_sha256=amendment["sha256"],
            expected_base_commit=self.base,
            expected_head_commit=self.base,
            expected_checkpoint_commit=self.base,
            expected_reservation_paths=amendment_reservation,
            expected_protected_paths=PROTECTED,
            receipt_issued_at=RECEIPT_TIME,
            mode="terminal-retained",
            expected_repository_root=str(self.repo),
            status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
        )
        self.assertEqual(errors, [])
        capture = json.loads(
            (self.repo / amendment["path"]).read_text(encoding="utf-8")
        )
        generated = [
            item
            for item in capture["dirty_paths"]
            if item["path"].startswith("BuildArtifacts/")
        ]
        self.assertEqual(
            [item["path"] for item in generated], sorted(generated_paths)
        )
        self.assertTrue(
            all(item["owner"] == "packet-generated-evidence" for item in generated)
        )
        self.assertFalse(capture["reserved_scope_clean"])
        self.assertFalse(capture["non_excluded_scope_clean"])
        self.assertEqual(capture["reserved_protected_overlaps"], [])
        self.assertEqual(
            capture["canonical_main_binding"],
            {
                "top_level": str(self.repo),
                "branch_ref": "refs/heads/main",
                "origin_url": capture_tool.CANONICAL_REMOTE_URL,
                "origin_main_commit": self.base,
            },
        )

    def test_amendment_capture_supports_distinct_observed_and_evidence_roots(self) -> None:
        self.write(".codex/config.toml", CONFIG_BASE)
        self.write_amendment_gate_reports()
        evidence_root = Path(self.temporary.name) / "evidence-clone"
        clone = subprocess.run(
            [
                "/usr/bin/git",
                "clone",
                "--no-hardlinks",
                "--no-checkout",
                str(self.repo),
                str(evidence_root),
            ],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        self.assertEqual(clone.returncode, 0, clone.stderr.decode())
        amendment_reservation = [*RESERVATION, "BuildArtifacts/WP-0002/"]
        amendment = capture_tool.collect_scope_capture(
            self.repo,
            base_commit=self.base,
            checkpoint_commit=self.base,
            reservation_paths=amendment_reservation,
            protected_paths_read_only=PROTECTED,
            output_relative=capture_tool.AMENDMENT_RETAINED_CAPTURE,
            captured_at=CAPTURE_TIME,
            expected_repository_root=str(self.repo),
            evidence_root=evidence_root,
            status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
        )
        errors = capture_tool.verify_scope_capture(
            evidence_root,
            amendment["path"],
            expected_capture_sha256=amendment["sha256"],
            expected_base_commit=self.base,
            expected_head_commit=self.base,
            expected_checkpoint_commit=self.base,
            expected_reservation_paths=amendment_reservation,
            expected_protected_paths=PROTECTED,
            receipt_issued_at=RECEIPT_TIME,
            mode="terminal-retained",
            expected_repository_root=str(self.repo),
            status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
        )
        self.assertEqual(errors, [])

    def test_amendment_capture_rejects_unrelated_dirty_path(self) -> None:
        self.write(".codex/config.toml", CONFIG_BASE)
        self.write_amendment_gate_reports()
        self.write("unrelated.txt", b"not part of the retained Unity state\n")
        with self.assertRaisesRegex(ValueError, "unclassified path: unrelated.txt"):
            capture_tool.collect_scope_capture(
                self.repo,
                base_commit=self.base,
                checkpoint_commit=self.base,
                reservation_paths=[*RESERVATION, "BuildArtifacts/WP-0002/"],
                protected_paths_read_only=PROTECTED,
                output_relative=capture_tool.AMENDMENT_RETAINED_CAPTURE,
                captured_at=CAPTURE_TIME,
                expected_repository_root=str(self.repo),
                evidence_root=self.repo,
                status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
            )

    def test_amendment_capture_requires_each_gate_report_kind(self) -> None:
        self.write(".codex/config.toml", CONFIG_BASE)
        self.write_amendment_gate_reports((1, 0, 2))
        with self.assertRaisesRegex(
            ValueError, "wp0002-editmode-test-assembly"
        ):
            capture_tool.collect_scope_capture(
                self.repo,
                base_commit=self.base,
                checkpoint_commit=self.base,
                reservation_paths=[*RESERVATION, "BuildArtifacts/WP-0002/"],
                protected_paths_read_only=PROTECTED,
                output_relative=capture_tool.AMENDMENT_RETAINED_CAPTURE,
                captured_at=CAPTURE_TIME,
                expected_repository_root=str(self.repo),
                evidence_root=self.repo,
                status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
            )

    def test_amendment_capture_rejects_caller_dirty_classification(self) -> None:
        self.write(".codex/config.toml", CONFIG_BASE)
        self.write_amendment_gate_reports()
        with self.assertRaisesRegex(
            ValueError, "must be derived from raw Git status"
        ):
            capture_tool.collect_scope_capture(
                self.repo,
                base_commit=self.base,
                checkpoint_commit=self.base,
                reservation_paths=[*RESERVATION, "BuildArtifacts/WP-0002/"],
                protected_paths_read_only=PROTECTED,
                output_relative=capture_tool.AMENDMENT_RETAINED_CAPTURE,
                captured_at=CAPTURE_TIME,
                expected_repository_root=str(self.repo),
                expected_dirty_states={},
                evidence_root=self.repo,
                status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
            )

    def test_amendment_read_only_index_proof_fails_closed_on_staged_drift(
        self,
    ) -> None:
        self.git("add", "Game/ProjectSettings/ProjectSettings.asset")
        with mock.patch.object(
            capture_tool,
            "_git_output",
            wraps=capture_tool._git_output,
        ) as git_output:
            facts = capture_tool._git_facts(
                self.repo,
                read_only_index=True,
            )
        self.assertIs(facts["index_clean"], False)
        self.assertEqual(facts["index_tree"], "")
        self.assertFalse(
            any(call.args[1] == ["write-tree"] for call in git_output.call_args_list)
        )

        self.write(".codex/config.toml", CONFIG_BASE)
        self.write_amendment_gate_reports()
        with self.assertRaises(ValueError):
            capture_tool.collect_scope_capture(
                self.repo,
                base_commit=self.base,
                checkpoint_commit=self.base,
                reservation_paths=[*RESERVATION, "BuildArtifacts/WP-0002/"],
                protected_paths_read_only=PROTECTED,
                output_relative=capture_tool.AMENDMENT_RETAINED_CAPTURE,
                captured_at=CAPTURE_TIME,
                expected_repository_root=str(self.repo),
                evidence_root=self.repo,
                status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
            )

    def test_amendment_capture_rejects_stale_origin_main(self) -> None:
        self.write(".codex/config.toml", CONFIG_BASE)
        self.write_amendment_gate_reports()
        self.git("update-ref", "-d", "refs/remotes/origin/main")
        with self.assertRaisesRegex(ValueError, "origin/main"):
            capture_tool.collect_scope_capture(
                self.repo,
                base_commit=self.base,
                checkpoint_commit=self.base,
                reservation_paths=[*RESERVATION, "BuildArtifacts/WP-0002/"],
                protected_paths_read_only=PROTECTED,
                output_relative=capture_tool.AMENDMENT_RETAINED_CAPTURE,
                captured_at=CAPTURE_TIME,
                expected_repository_root=str(self.repo),
                evidence_root=self.repo,
                status_arguments=capture_tool.AMENDMENT_STATUS_ARGUMENTS,
            )

    def test_terminal_retained_capture_rejects_invalid_collection_root(self) -> None:
        capture = self.capture()
        capture["repository_root"] = "relative/repository"
        capture["collector"]["status_command"][4] = "relative/repository"
        self.rewrite_capture(capture)
        errors = self.verify(mode="terminal-retained")
        self.assertIn(
            "scope capture repository_root is not a canonical absolute POSIX path",
            errors,
        )

    def test_terminal_retained_capture_binds_command_to_collection_root(self) -> None:
        capture = self.capture()
        capture["collector"]["status_command"][4] = "/different/repository"
        self.rewrite_capture(capture)
        errors = self.verify(mode="terminal-retained")
        self.assertIn("scope capture collector status_command differs", errors)

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
