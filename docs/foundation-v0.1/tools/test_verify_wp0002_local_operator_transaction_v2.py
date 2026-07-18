from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


REPO_ROOT = Path(__file__).resolve().parents[3]
TOOL_PATH = (
    REPO_ROOT
    / "Tools"
    / "Validation"
    / "verify_wp0002_local_operator_transaction_v2.py"
)


def load_tool():
    spec = importlib.util.spec_from_file_location("wp0002_v2_test_target", TOOL_PATH)
    if spec is None or spec.loader is None:
        raise RuntimeError("cannot load v2 verifier")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


tool = load_tool()


class LoaderTests(unittest.TestCase):
    def setUp(self) -> None:
        self.path = tool.V1_VERIFIER_PATH
        self.digest = tool.V1_VERIFIER_SHA256
        self.previous = sys.modules.get(tool._V1_MODULE_NAME, tool._MISSING)

    def tearDown(self) -> None:
        tool.V1_VERIFIER_PATH = self.path
        tool.V1_VERIFIER_SHA256 = self.digest
        if self.previous is tool._MISSING:
            sys.modules.pop(tool._V1_MODULE_NAME, None)
        else:
            sys.modules[tool._V1_MODULE_NAME] = self.previous

    def test_success_removes_absent_temporary_module(self) -> None:
        sys.modules.pop(tool._V1_MODULE_NAME, None)
        loaded = tool._load_v1()
        self.assertTrue(callable(loaded.validate_authority_evidence))
        self.assertNotIn(tool._V1_MODULE_NAME, sys.modules)

    def test_success_restores_preexisting_none(self) -> None:
        sys.modules[tool._V1_MODULE_NAME] = None
        tool._load_v1()
        self.assertIsNone(sys.modules[tool._V1_MODULE_NAME])

    def test_hash_drift_fails_before_execution(self) -> None:
        tool.V1_VERIFIER_SHA256 = "0" * 64
        marker = object()
        sys.modules[tool._V1_MODULE_NAME] = marker
        with self.assertRaisesRegex(tool.V2LoaderError, "hash mismatch"):
            tool._load_v1()
        self.assertIs(sys.modules[tool._V1_MODULE_NAME], marker)

    def test_exception_restores_preexisting_module(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            source_path = Path(directory) / "broken.py"
            source = b"raise RuntimeError('loader-boom')\n"
            source_path.write_bytes(source)
            tool.V1_VERIFIER_PATH = source_path
            tool.V1_VERIFIER_SHA256 = hashlib.sha256(source).hexdigest()
            marker = object()
            sys.modules[tool._V1_MODULE_NAME] = marker
            with self.assertRaisesRegex(tool.V2LoaderError, "cannot load"):
                tool._load_v1()
            self.assertIs(sys.modules[tool._V1_MODULE_NAME], marker)


class BindingTests(unittest.TestCase):
    def test_authority_binding_names_only_successor_contract(self) -> None:
        stage1 = {
            "changed_files_manifest_sha256": "1" * 64,
            "commit_sha": "2" * 40,
            "deterministic_patch_sha256": "3" * 64,
            "tree_oid": "4" * 40,
        }
        binding = tool.authorization_binding(stage1)
        self.assertEqual(binding["claim"], tool.AUTHORIZATION_CLAIM)
        self.assertEqual(binding["supersession_claim"], tool.SUPERSESSION_CLAIM)
        self.assertEqual(binding["superseded_v1_receipt_id"], tool.V1_RECEIPT_ID)
        self.assertEqual(binding["superseded_v1_receipt_sha256"], tool.V1_RECEIPT_SHA256)
        self.assertEqual(binding["superseded_v1_boundary_sha256"], tool.V1_BOUNDARY_SHA256)
        self.assertEqual(binding["receipt_path"], tool.RECEIPT_PATH)
        self.assertTrue(tool.AUTHORITY_EVIDENCE_PATH.startswith(tool.EVIDENCE_ROOT))

    def test_receipt_binds_supersession_and_successor_claims(self) -> None:
        source = "https://github.com/AC-21/sasha-the-land-pirate/pull/99#issuecomment-123"
        authority = {
            "authorization_comment": {
                "html_url": source,
                "body_utf8_sha256": "a" * 64,
            },
            "stage1": {"commit_sha": "b" * 40},
        }
        stage1_artifacts = {"example.txt": "c" * 64}
        receipt_artifacts = {
            **stage1_artifacts,
            tool.V1_RECEIPT_PATH: tool.V1_RECEIPT_SHA256,
        }
        receipt = {
            "accepted_commit": "b" * 40,
            "approval_text_sha256": "a" * 64,
            "artifact_resolver": {
                "resolver_reference": source,
                "type": "external-protected",
            },
            "artifact_sha256": receipt_artifacts,
            "foundation_binding": None,
            "issued_at": "2026-07-18T00:00:00Z",
            "issued_by": tool.OWNER_LOGIN,
            "issuer_role": "creator",
            "receipt_id": tool.RECEIPT_ID,
            "receipt_kind": "creator-authorization",
            "sealed": True,
            "signature_reference": source,
            "source_reference": source,
            "subject_claims": [
                {
                    "subject_id": tool.V1_AMENDMENT_ID,
                    "claims": [tool.SUPERSESSION_CLAIM],
                },
                {
                    "subject_id": "WP-0002",
                    "claims": [tool.AUTHORIZATION_CLAIM],
                },
            ],
            "subject_contract_sha256": {
                tool.V1_AMENDMENT_ID: tool.V1_BOUNDARY_SHA256,
                "WP-0002": tool.CONTRACT_SHA256,
            },
            "subject_event_sha256": {},
            "subject_ids": [tool.V1_AMENDMENT_ID, "WP-0002"],
        }
        tool._validate_receipt(
            receipt,
            authority=authority,
            expected_artifacts=stage1_artifacts,
        )
        receipt["subject_claims"][0]["claims"] = ["AUTHORIZE-WRONG"]
        with self.assertRaisesRegex(tool.VerificationError, "wrong subject_claims"):
            tool._validate_receipt(
                receipt,
                authority=authority,
                expected_artifacts=stage1_artifacts,
            )


class HistoricalGitObjectTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.git("init", "-q", "-b", "main")
        self.git("config", "user.name", "Verifier Test")
        self.git("config", "user.email", "verifier@example.invalid")
        self.write("base.txt", b"base\n")
        historical_receipt = subprocess.check_output(
            [
                "git",
                "-C",
                str(REPO_ROOT),
                "show",
                f"{tool.V1_CONTROL_MERGE_SHA}:{tool.V1_RECEIPT_PATH}",
            ]
        )
        historical_boundary = subprocess.check_output(
            [
                "git",
                "-C",
                str(REPO_ROOT),
                "show",
                f"{tool.V1_CONTROL_MERGE_SHA}:{tool.V1_BOUNDARY_PATH}",
            ]
        )
        self.write(
            tool.V1_RECEIPT_PATH,
            historical_receipt,
        )
        self.write(
            tool.V1_BOUNDARY_PATH,
            historical_boundary,
        )
        for retained_path in tool.V1_RETAINED_ARTIFACT_SHA256:
            retained_bytes = subprocess.check_output(
                [
                    "git",
                    "-C",
                    str(REPO_ROOT),
                    "show",
                    f"{tool.V1_CONTROL_MERGE_SHA}:{retained_path}",
                ]
            )
            self.write(retained_path, retained_bytes)
        self.git(
            "add",
            "base.txt",
            tool.V1_RECEIPT_PATH,
            tool.V1_BOUNDARY_PATH,
            *tool.V1_RETAINED_ARTIFACT_SHA256,
        )
        self.git("commit", "-q", "-m", "base")
        self.base = self.rev("HEAD")
        raw_status = b"scope-status\0"
        observations = b'{"schema_version":1,"observations":[]}\n'
        raw_status_hash = hashlib.sha256(raw_status).hexdigest()
        observations_hash = hashlib.sha256(observations).hexdigest()
        self.scope_status_path = (
            "docs/evidence/WP-0002/local-operator-successor/scope-capture/"
            f"working-tree-scope.status.{raw_status_hash}.bin"
        )
        self.scope_observations_path = (
            "docs/evidence/WP-0002/local-operator-successor/scope-capture/"
            f"working-tree-scope.observations.{observations_hash}.json"
        )
        capture = {
            "artifacts": {
                "raw_status": {
                    "path": self.scope_status_path,
                    "sha256": raw_status_hash,
                    "byte_size": len(raw_status),
                },
                "observations": {
                    "path": self.scope_observations_path,
                    "sha256": observations_hash,
                    "byte_size": len(observations),
                },
            }
        }
        self.protection_before = self.protection_capture(
            self.base,
            "2026-07-18T00:00:00Z",
        )
        self.write("README.md", b"successor-control\n")
        self.write(tool.V1_BOUNDARY_PATH, b'{"successor":true}\n')
        for control_path in sorted(
            tool.STAGE1_CONTROL_PATHS
            - {"README.md", tool.V1_BOUNDARY_PATH, tool.PROTECTION_BEFORE_PATH}
        ):
            self.write(control_path, f"successor control: {control_path}\n".encode())
        self.write(tool.STAGE1_SCOPE_CAPTURE_PATH, json.dumps(capture).encode("utf-8"))
        self.write(self.scope_status_path, raw_status)
        self.write(self.scope_observations_path, observations)
        self.write(
            tool.PROTECTION_BEFORE_PATH,
            json.dumps(self.protection_before, indent=2, sort_keys=True).encode("utf-8")
            + b"\n",
        )
        self.git(
            "add",
            *sorted(tool.STAGE1_CONTROL_PATHS),
            tool.STAGE1_SCOPE_CAPTURE_PATH,
            self.scope_status_path,
            self.scope_observations_path,
        )
        self.git("commit", "-q", "-m", "stage1")
        self.stage1 = self.rev("HEAD")
        self.repository = tool.GitRepository(self.root)
        stage1_record = tool._v1._stage1_record(
            self.repository,
            self.base,
            self.stage1,
        )
        source_reference = (
            "https://github.com/AC-21/sasha-the-land-pirate/"
            "pull/99#issuecomment-123"
        )
        approval_hash = "a" * 64
        self.authority = {
            "stage1": stage1_record,
            "authorization_comment": {
                "created_at": "2026-07-18T00:02:00Z",
                "html_url": source_reference,
                "body_utf8_sha256": approval_hash,
            },
        }
        receipt_artifacts = {
            source_reference.removeprefix("https://"): approval_hash,
            **{
                str(item["path"]): str(item["new_blob_sha256"])
                for item in stage1_record["changed_files"]
            },
            tool.V1_RECEIPT_PATH: tool.V1_RECEIPT_SHA256,
        }
        self.pending_receipt = {
            "receipt_id": tool.RECEIPT_ID,
            "issued_at": "2026-07-18T00:02:00Z",
            "issued_by": tool.OWNER_LOGIN,
            "issuer_role": "creator",
            "receipt_kind": "creator-authorization",
            "artifact_resolver": {
                "type": "external-protected",
                "resolver_reference": source_reference,
            },
            "source_reference": source_reference,
            "subject_ids": [tool.V1_AMENDMENT_ID, "WP-0002"],
            "subject_claims": [
                {
                    "subject_id": tool.V1_AMENDMENT_ID,
                    "claims": [tool.SUPERSESSION_CLAIM],
                },
                {
                    "subject_id": "WP-0002",
                    "claims": [tool.AUTHORIZATION_CLAIM],
                },
            ],
            "approval_text_sha256": approval_hash,
            "accepted_commit": self.stage1,
            "artifact_sha256": receipt_artifacts,
            "subject_contract_sha256": {
                tool.V1_AMENDMENT_ID: tool.V1_BOUNDARY_SHA256,
                "WP-0002": tool.CONTRACT_SHA256,
            },
            "subject_event_sha256": {},
            "foundation_binding": None,
            "signature_reference": source_reference,
            "sealed": True,
        }
        self.write(
            tool.RECEIPT_PATH,
            json.dumps(self.pending_receipt, indent=2, sort_keys=True).encode("utf-8")
            + b"\n",
        )
        self.git("add", tool.RECEIPT_PATH)
        self.git("commit", "-q", "-m", "receipt only")
        self.final_head = self.rev("HEAD")
        receipt_sha256 = hashlib.sha256(
            (self.root / tool.RECEIPT_PATH).read_bytes()
        ).hexdigest()
        self.pre_merge = {
            "final_pull_request": {
                "base_sha": self.base,
                "head_sha": self.final_head,
            },
            "receipt_materialization": {
                "commit_sha": self.final_head,
                "parent_sha": self.stage1,
                "receipt_sha256": receipt_sha256,
            },
            "protection_before": self.protection_before["normalized"],
            "raw_artifacts": {
                "protection_before": tool._v1._artifact_ref(
                    tool._v1._validate_protection_capture(
                        self.protection_before
                    )[1]
                )
            },
        }
        final_tree = self.rev("HEAD^{tree}")
        self.squash = self.git_output(
            "commit-tree",
            final_tree,
            "-p",
            self.base,
            "-m",
            "squash",
        ).strip()
        self.complete = {
            "merged_pull_request": {"merge_commit_sha": self.squash},
            "merge": {"tree_oid": final_tree},
        }

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def git(self, *args: str) -> None:
        subprocess.run(
            ["git", "-C", str(self.root), *args],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )

    def git_output(self, *args: str) -> str:
        return subprocess.check_output(
            ["git", "-C", str(self.root), *args],
            text=True,
        )

    def rev(self, value: str) -> str:
        return subprocess.check_output(
            ["git", "-C", str(self.root), "rev-parse", value],
            text=True,
        ).strip()

    def write(self, relative: str, data: bytes) -> None:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def protection_capture(self, main_sha: str, observed_at: str) -> dict[str, object]:
        protection = {
            "required_status_checks": {
                "strict": True,
                "checks": [
                    {"context": name, "app_id": tool._v1.REQUIRED_APP_ID}
                    for name in tool._v1.FULL_REQUIRED_CHECKS
                ],
            },
            "enforce_admins": {"enabled": True},
            "required_pull_request_reviews": {
                "dismiss_stale_reviews": False,
                "require_code_owner_reviews": False,
                "required_approving_review_count": 0,
                "require_last_push_approval": False,
                "bypass_pull_request_allowances": {
                    "users": [],
                    "teams": [],
                    "apps": [],
                },
            },
            "restrictions": {"users": [], "teams": [], "apps": []},
            "required_conversation_resolution": {"enabled": True},
            "required_linear_history": {"enabled": True},
            "allow_force_pushes": {"enabled": False},
            "allow_deletions": {"enabled": False},
        }
        raw = {
            "branch": {"commit": {"sha": main_sha}},
            "protection": protection,
            "repository": {
                "allow_squash_merge": True,
                "allow_merge_commit": False,
                "allow_rebase_merge": False,
            },
            "rulesets": [],
            "raw_response_sha256": {
                "branch": "1" * 64,
                "protection": "2" * 64,
                "repository": "3" * 64,
                "rulesets": "4" * 64,
            },
        }
        return {
            "schema_version": 1,
            "kind": "wp0002-local-operator-protection-capture",
            "normalized": tool._v1._normalize_protection(
                raw,
                observed_at=observed_at,
            ),
            "raw": raw,
        }

    def cloned_authority(self) -> dict[str, object]:
        return json.loads(json.dumps(self.authority))

    def checkout_nominal_stage1_files(self) -> None:
        self.git(
            "checkout",
            self.stage1,
            "--",
            *sorted(tool.STAGE1_CONTROL_PATHS),
            tool.STAGE1_SCOPE_CAPTURE_PATH,
            self.scope_status_path,
            self.scope_observations_path,
        )

    def authority_for(self, base: str, stage1: str) -> dict[str, object]:
        return {
            "stage1": tool._v1._stage1_record(
                self.repository,
                base,
                stage1,
            )
        }

    def stage1_with_protection(
        self,
        capture: dict[str, object],
    ) -> tuple[str, dict[str, object]]:
        self.git("checkout", "-q", "--detach", self.base)
        self.checkout_nominal_stage1_files()
        self.write(
            tool.PROTECTION_BEFORE_PATH,
            json.dumps(capture, indent=2, sort_keys=True).encode("utf-8") + b"\n",
        )
        self.git("add", tool.PROTECTION_BEFORE_PATH)
        self.git("commit", "-q", "-m", "alternate protection-before")
        stage1 = self.rev("HEAD")
        authority = self.authority_for(self.base, stage1)
        authority["authorization_comment"] = {
            "created_at": "2026-07-18T00:02:00Z"
        }
        return stage1, authority

    def commit_receipt_child(
        self,
        receipt: dict[str, object],
        *,
        extra_path: str | None = None,
    ) -> str:
        self.git("checkout", "-q", "--detach", self.stage1)
        self.write(
            tool.RECEIPT_PATH,
            json.dumps(receipt, indent=2, sort_keys=True).encode("utf-8") + b"\n",
        )
        self.git("add", tool.RECEIPT_PATH)
        if extra_path is not None:
            self.write(extra_path, b"not receipt-only\n")
            self.git("add", extra_path)
        self.git("commit", "-q", "-m", "alternate receipt child")
        return self.rev("HEAD")

    def validate(
        self,
        authority: dict[str, object],
        pre_merge: dict[str, object] | None = None,
        complete: dict[str, object] | None = None,
        *,
        expected_base: str | None = None,
    ) -> None:
        # Production has no base override. Tests patch only the pinned constant so
        # an isolated temporary repository can exercise every later invariant.
        with mock.patch.object(
            tool,
            "V1_CONTROL_MERGE_SHA",
            expected_base or self.base,
        ):
            tool.validate_historical_git_objects(
                self.repository,
                authority,
                pre_merge,
                complete,
            )

    def test_stage1_positive_uses_git_objects_not_worktree(self) -> None:
        self.write("README.md", b"uncommitted-worktree-drift\n")
        self.validate(self.authority)

    def test_full_historical_chain_positive(self) -> None:
        self.validate(
            self.authority,
            self.pre_merge,
            self.complete,
        )

    def test_pending_receipt_child_positive(self) -> None:
        result = self.validate_pending_receipt()
        self.assertEqual(result["stage1_sha"], self.stage1)
        self.assertEqual(result["final_head_sha"], self.final_head)

    def test_pending_receipt_child_accepts_exact_synthetic_pr_merge_head(self) -> None:
        synthetic = self.git_output(
            "commit-tree",
            self.rev(f"{self.final_head}^{{tree}}"),
            "-p",
            self.base,
            "-p",
            self.final_head,
            "-m",
            "synthetic pull request merge",
        ).strip()
        self.git("checkout", "-q", "--detach", synthetic)
        result = self.validate_pending_receipt()
        self.assertEqual(result["final_head_sha"], self.final_head)

    def test_pending_receipt_child_rejects_synthetic_merge_on_wrong_base(self) -> None:
        synthetic = self.git_output(
            "commit-tree",
            self.rev(f"{self.final_head}^{{tree}}"),
            "-p",
            self.stage1,
            "-p",
            self.final_head,
            "-m",
            "wrong-base synthetic merge",
        ).strip()
        self.git("checkout", "-q", "--detach", synthetic)
        with self.assertRaisesRegex(tool.VerificationError, "receipt-only child"):
            self.validate_pending_receipt()

    def validate_pending_receipt(self) -> dict[str, object]:
        with mock.patch.object(tool, "V1_CONTROL_MERGE_SHA", self.base):
            return tool.validate_pending_receipt_child(
                self.repository,
                self.pending_receipt,
            )

    def test_pending_receipt_child_rejects_artifact_map_drift(self) -> None:
        receipt = json.loads(json.dumps(self.pending_receipt))
        receipt["artifact_sha256"]["invented.txt"] = "9" * 64
        self.commit_receipt_child(receipt)
        with self.assertRaisesRegex(tool.VerificationError, "artifact keys or values"):
            with mock.patch.object(tool, "V1_CONTROL_MERGE_SHA", self.base):
                tool.validate_pending_receipt_child(self.repository, receipt)

    def test_pending_receipt_child_rejects_non_receipt_delta(self) -> None:
        self.commit_receipt_child(self.pending_receipt, extra_path="invented.txt")
        with self.assertRaisesRegex(tool.VerificationError, "receipt-only child"):
            with mock.patch.object(tool, "V1_CONTROL_MERGE_SHA", self.base):
                tool.validate_pending_receipt_child(
                    self.repository,
                    self.pending_receipt,
                )

    def test_pending_receipt_child_rejects_symlink_receipt(self) -> None:
        self.git("checkout", "-q", "--detach", self.stage1)
        receipt_path = self.root / tool.RECEIPT_PATH
        receipt_path.parent.mkdir(parents=True, exist_ok=True)
        os.symlink("receipt-target.json", receipt_path)
        self.git("add", tool.RECEIPT_PATH)
        self.git("commit", "-q", "-m", "symlink receipt child")
        with self.assertRaisesRegex(tool.VerificationError, "regular mode 100644"):
            self.validate_pending_receipt()

    def test_rejects_stale_protection_before_authority(self) -> None:
        authority = self.cloned_authority()
        authority["authorization_comment"]["created_at"] = "2026-07-18T00:05:01Z"
        with self.assertRaisesRegex(tool.VerificationError, "at most 300 seconds"):
            self.validate(authority)

    def test_rejects_protection_before_after_authority(self) -> None:
        authority = self.cloned_authority()
        authority["authorization_comment"]["created_at"] = "2026-07-17T23:59:59Z"
        with self.assertRaisesRegex(tool.VerificationError, "at most 300 seconds"):
            self.validate(authority)

    def test_rejects_protection_before_main_sha_mismatch(self) -> None:
        capture = self.protection_capture(
            "f" * 40,
            "2026-07-18T00:00:00Z",
        )
        _, authority = self.stage1_with_protection(capture)
        with self.assertRaisesRegex(tool.VerificationError, "main SHA differs"):
            self.validate(authority)

    def test_rejects_pre_merge_normalized_protection_substitution(self) -> None:
        pre_merge = json.loads(json.dumps(self.pre_merge))
        pre_merge["protection_before"]["observed_at"] = "2026-07-18T00:00:01Z"
        with self.assertRaisesRegex(
            tool.VerificationError,
            "differs from the committed Stage-1 capture",
        ):
            self.validate(self.authority, pre_merge)

    def test_rejects_pre_merge_raw_protection_substitution(self) -> None:
        pre_merge = json.loads(json.dumps(self.pre_merge))
        pre_merge["raw_artifacts"]["protection_before"]["sha256"] = "0" * 64
        with self.assertRaisesRegex(
            tool.VerificationError,
            "raw reference differs from Stage-1",
        ):
            self.validate(self.authority, pre_merge)

    def test_build_pre_merge_rejects_supplied_protection_substitution(self) -> None:
        substituted = json.loads(json.dumps(self.protection_before))
        substituted["normalized"]["observed_at"] = "2026-07-18T00:00:01Z"
        with mock.patch.object(tool, "V1_CONTROL_MERGE_SHA", self.base):
            with self.assertRaisesRegex(
                tool.VerificationError,
                "supplied protection-before differs",
            ):
                tool.build_pre_merge_evidence(
                    self.repository,
                    object(),
                    self.authority,
                    substituted,
                    {},
                )

    def test_default_rejects_noncanonical_base(self) -> None:
        with self.assertRaisesRegex(tool.VerificationError, "exact unclosed v1"):
            tool.validate_historical_git_objects(self.repository, self.authority)

    def test_public_and_cli_authority_render_reject_noncanonical_base(self) -> None:
        self.assertIs(tool._v1.render_authorization_body, tool.render_authorization_body)
        self.assertIs(tool._v1.build_authority_evidence, tool.build_authority_evidence)
        self.assertIs(tool._v1.build_pre_merge_evidence, tool.build_pre_merge_evidence)
        self.assertIs(tool._v1.render_completion_body, tool.render_completion_body)
        self.assertIs(tool._v1.build_complete_evidence, tool.build_complete_evidence)
        with self.assertRaisesRegex(tool.VerificationError, "exact unclosed v1"):
            tool.render_authorization_body(
                self.repository,
                self.base,
                self.stage1,
            )
        with mock.patch("sys.stderr", new_callable=io.StringIO) as stderr:
            result = tool.main(
                [
                    "render-authority-body",
                    "--repository-path",
                    str(self.root),
                    "--base",
                    self.base,
                    "--stage1",
                    self.stage1,
                ]
            )
        self.assertEqual(result, 1)
        self.assertIn("exact unclosed v1", stderr.getvalue())

    def test_authority_builder_guard_runs_before_live_builder(self) -> None:
        with mock.patch.object(
            tool,
            "_original_build_authority_evidence",
        ) as original:
            with self.assertRaisesRegex(tool.VerificationError, "exact unclosed v1"):
                tool.build_authority_evidence(
                    self.repository,
                    object(),
                    pull_number=99,
                    comment_id=123,
                    stage1_sha=self.stage1,
                )
        original.assert_not_called()

    def test_phase_builders_apply_historical_guards(self) -> None:
        with (
            mock.patch.object(tool, "V1_CONTROL_MERGE_SHA", self.base),
            mock.patch.object(
                tool,
                "_original_build_authority_evidence",
                return_value=self.authority,
            ),
            mock.patch.object(
                tool,
                "_original_build_pre_merge_evidence",
                return_value=self.pre_merge,
            ),
            mock.patch.object(
                tool,
                "_original_build_complete_evidence",
                return_value=self.complete,
            ),
        ):
            self.assertEqual(
                tool.build_authority_evidence(
                    self.repository,
                    object(),
                    pull_number=99,
                    comment_id=123,
                    stage1_sha=self.stage1,
                ),
                self.authority,
            )
            self.assertEqual(
                tool.build_pre_merge_evidence(
                    self.repository,
                    object(),
                    self.authority,
                    self.protection_before,
                    {},
                ),
                self.pre_merge,
            )
            self.assertEqual(
                tool.build_complete_evidence(
                    self.repository,
                    object(),
                    self.pre_merge,
                    {},
                    completion_comment_id=456,
                ),
                self.complete,
            )

    def test_completion_render_guard_runs_before_live_renderer(self) -> None:
        with mock.patch.object(
            tool,
            "_original_render_completion_body",
        ) as original:
            with self.assertRaisesRegex(tool.VerificationError, "exact unclosed v1"):
                tool.render_completion_body(
                    self.repository,
                    object(),
                    self.pre_merge,
                    {},
                )
        original.assert_not_called()

    def test_retained_v1_artifact_set_is_exact(self) -> None:
        expected = {
            "Tools/Validation/collect_wp0002_scope_capture.py",
            "Tools/Validation/verify_wp0002_local_operator_transaction.py",
            "docs/foundation-v0.1/tools/test_collect_wp0002_scope_capture.py",
            (
                "docs/foundation-v0.1/tools/"
                "test_verify_wp0002_local_operator_transaction.py"
            ),
            (
                "docs/foundation-v0.1/schemas/"
                "wp0002-local-operator-scope-capture.schema.json"
            ),
            (
                "docs/foundation-v0.1/schemas/"
                "wp0002-local-operator-transaction-evidence.schema.json"
            ),
            (
                "docs/foundation-v0.1/governance/"
                "WP-0002-DELEGATED-LOCAL-UNITY-OPERATOR-AMENDMENT.md"
            ),
            (
                "docs/evidence/WP-0002/local-operator-amendment/scope-capture/"
                "working-tree-scope.json"
            ),
            (
                "docs/evidence/WP-0002/local-operator-amendment/scope-capture/"
                "working-tree-scope.observations."
                "089a49fc03dbbc473158a02b31c6a3ca5fea1f7707c78b5466247f17511554ad.json"
            ),
            (
                "docs/evidence/WP-0002/local-operator-amendment/scope-capture/"
                "working-tree-scope.status."
                "bea960c9a69eda2a80c43103d093da728e30df0c65763f17ba6d1a994ea83d02.bin"
            ),
        }
        self.assertEqual(set(tool.V1_RETAINED_ARTIFACT_SHA256), expected)

    def test_rejects_missing_stage1_object(self) -> None:
        authority = self.cloned_authority()
        authority["stage1"]["commit_sha"] = "0" * 40
        with self.assertRaises(tool.VerificationError):
            self.validate(authority)

    def test_rejects_wrong_parent_binding(self) -> None:
        authority = self.cloned_authority()
        authority["stage1"]["base_sha"] = self.stage1
        with self.assertRaisesRegex(tool.VerificationError, "single direct child"):
            self.validate(authority)

    def test_rejects_tree_oid_drift(self) -> None:
        authority = self.cloned_authority()
        authority["stage1"]["tree_oid"] = "f" * 40
        with self.assertRaisesRegex(tool.VerificationError, "differ from evidence"):
            self.validate(authority)

    def test_rejects_blob_hash_drift(self) -> None:
        authority = self.cloned_authority()
        authority["stage1"]["changed_files"][0]["new_blob_sha256"] = "e" * 64
        with self.assertRaisesRegex(tool.VerificationError, "differ from evidence"):
            self.validate(authority)

    def test_rejects_patch_hash_drift(self) -> None:
        authority = self.cloned_authority()
        authority["stage1"]["deterministic_patch_sha256"] = "d" * 64
        with self.assertRaisesRegex(tool.VerificationError, "differ from evidence"):
            self.validate(authority)

    def test_rejects_v1_receipt_mutation_in_stage1(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        self.write(tool.V1_RECEIPT_PATH, b"mutated receipt\n")
        self.write(tool.V1_BOUNDARY_PATH, b'{"successor":true}\n')
        self.git("add", tool.V1_RECEIPT_PATH, tool.V1_BOUNDARY_PATH)
        self.git("commit", "-q", "-m", "mutate receipt")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(tool.VerificationError, "Stage-1 v1 receipt"):
            self.validate(
                self.authority_for(self.base, stage1),
            )

    def test_rejects_v1_boundary_mutation_in_base(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        self.write(tool.V1_BOUNDARY_PATH, b'{"mutated_v1":true}\n')
        self.git("add", tool.V1_BOUNDARY_PATH)
        self.git("commit", "-q", "-m", "mutate old boundary")
        mutated_base = self.rev("HEAD")
        self.write(tool.V1_BOUNDARY_PATH, b'{"successor":true}\n')
        self.git("add", tool.V1_BOUNDARY_PATH)
        self.git("commit", "-q", "-m", "successor")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(tool.VerificationError, "base v1 boundary"):
            self.validate(
                self.authority_for(mutated_base, stage1),
                expected_base=mutated_base,
            )

    def test_rejects_v1_report_injection_in_stage1(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        self.write(tool.V1_BOUNDARY_PATH, b'{"successor":true}\n')
        self.write(tool.V1_CLOSURE_EVIDENCE_PATHS[0], b"{}\n")
        self.git("add", tool.V1_BOUNDARY_PATH, tool.V1_CLOSURE_EVIDENCE_PATHS[0])
        self.git("commit", "-q", "-m", "inject v1 report")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(tool.VerificationError, "forbidden v1 closure"):
            self.validate(
                self.authority_for(self.base, stage1),
            )

    def test_rejects_successor_report_injection_in_stage1(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        self.checkout_nominal_stage1_files()
        self.write(tool.CLOSURE_EVIDENCE_PATHS[0], b"{}\n")
        self.git("add", tool.CLOSURE_EVIDENCE_PATHS[0])
        self.git("commit", "-q", "-m", "inject successor report")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(
            tool.VerificationError,
            "premature successor closure",
        ):
            self.validate(self.authority_for(self.base, stage1))

    def test_rejects_implementation_and_unrelated_stage1_paths(self) -> None:
        forbidden_paths = (
            "Game/Assets/AtomicLandPirate/feature.cs",
            "SimulationCore/Runtime/feature.cs",
            "SaveContracts/Runtime/feature.cs",
            "Tests/feature.cs",
            "control.txt",
        )
        for forbidden_path in forbidden_paths:
            with self.subTest(path=forbidden_path):
                self.git("checkout", "-q", "--detach", self.base)
                self.checkout_nominal_stage1_files()
                self.write(forbidden_path, b"implementation-or-unrelated\n")
                self.git("add", forbidden_path)
                self.git("commit", "-q", "-m", "out-of-scope stage1 path")
                stage1 = self.rev("HEAD")
                with self.assertRaisesRegex(
                    tool.VerificationError,
                    "outside the exact successor control scope",
                ):
                    self.validate(self.authority_for(self.base, stage1))

    def test_rejects_unreferenced_hash_named_scope_artifact(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        self.checkout_nominal_stage1_files()
        extra = b"unreferenced-scope-bytes\n"
        extra_hash = hashlib.sha256(extra).hexdigest()
        extra_path = (
            "docs/evidence/WP-0002/local-operator-successor/scope-capture/"
            f"working-tree-scope.status.{extra_hash}.bin"
        )
        self.write(extra_path, extra)
        self.git("add", extra_path)
        self.git("commit", "-q", "-m", "unreferenced scope artifact")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(
            tool.VerificationError,
            "outside the exact successor control scope",
        ):
            self.validate(self.authority_for(self.base, stage1))

    def test_rejects_missing_fixed_stage1_control_path(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        self.checkout_nominal_stage1_files()
        self.git("rm", "-f", "-q", "AGENTS.md")
        self.git("commit", "-q", "-m", "omit required stage1 control")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(
            tool.VerificationError,
            "omits exact successor control paths",
        ):
            self.validate(self.authority_for(self.base, stage1))

    def test_rejects_symlink_stage1_control(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        self.checkout_nominal_stage1_files()
        readme = self.root / "README.md"
        readme.unlink()
        os.symlink("AGENTS.md", readme)
        self.git("add", "README.md")
        self.git("commit", "-q", "-m", "symlink stage1 control")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(
            tool.VerificationError,
            "regular mode 100644 at README.md",
        ):
            self.validate(self.authority_for(self.base, stage1))

    def test_rejects_retained_v1_artifact_mutation_in_stage1(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        retained_path = (
            "docs/foundation-v0.1/governance/"
            "WP-0002-DELEGATED-LOCAL-UNITY-OPERATOR-AMENDMENT.md"
        )
        self.write(retained_path, b"mutated retained governance\n")
        self.write(tool.V1_BOUNDARY_PATH, b'{"successor":true}\n')
        self.git("add", retained_path, tool.V1_BOUNDARY_PATH)
        self.git("commit", "-q", "-m", "mutate retained v1 artifact")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(
            tool.VerificationError,
            "modifies retained v1 artifact",
        ):
            self.validate(self.authority_for(self.base, stage1))

    def test_rejects_v1_transaction_schema_mutation_in_stage1(self) -> None:
        self.git("checkout", "-q", "--detach", self.base)
        schema_path = (
            "docs/foundation-v0.1/schemas/"
            "wp0002-local-operator-transaction-evidence.schema.json"
        )
        self.write(schema_path, b'{"mutated":"v1 schema"}\n')
        self.write(tool.V1_BOUNDARY_PATH, b'{"successor":true}\n')
        self.git("add", schema_path, tool.V1_BOUNDARY_PATH)
        self.git("commit", "-q", "-m", "mutate retained v1 transaction schema")
        stage1 = self.rev("HEAD")
        with self.assertRaisesRegex(
            tool.VerificationError,
            "modifies retained v1 artifact",
        ):
            self.validate(self.authority_for(self.base, stage1))

    def test_rejects_final_head_that_is_not_receipt_only_child(self) -> None:
        pre_merge = json.loads(json.dumps(self.pre_merge))
        pre_merge["final_pull_request"]["head_sha"] = self.stage1
        pre_merge["receipt_materialization"]["commit_sha"] = self.stage1
        with self.assertRaisesRegex(tool.VerificationError, "receipt-only child"):
            self.validate(
                self.authority,
                pre_merge,
            )

    def test_rejects_receipt_blob_hash_drift(self) -> None:
        pre_merge = json.loads(json.dumps(self.pre_merge))
        pre_merge["receipt_materialization"]["receipt_sha256"] = "9" * 64
        with self.assertRaisesRegex(tool.VerificationError, "receipt blob differs"):
            self.validate(
                self.authority,
                pre_merge,
            )

    def test_rejects_squash_with_wrong_parent(self) -> None:
        complete = json.loads(json.dumps(self.complete))
        wrong = self.git_output(
            "commit-tree",
            self.rev("HEAD^{tree}"),
            "-p",
            self.stage1,
            "-m",
            "wrong parent",
        ).strip()
        complete["merged_pull_request"]["merge_commit_sha"] = wrong
        with self.assertRaisesRegex(tool.VerificationError, "single child"):
            self.validate(
                self.authority,
                self.pre_merge,
                complete,
            )

    def test_rejects_squash_tree_drift(self) -> None:
        complete = json.loads(json.dumps(self.complete))
        wrong = self.git_output(
            "commit-tree",
            self.rev(f"{self.stage1}^{{tree}}"),
            "-p",
            self.base,
            "-m",
            "wrong tree",
        ).strip()
        complete["merged_pull_request"]["merge_commit_sha"] = wrong
        with self.assertRaisesRegex(tool.VerificationError, "tree differs"):
            self.validate(
                self.authority,
                self.pre_merge,
                complete,
            )


if __name__ == "__main__":
    unittest.main()
