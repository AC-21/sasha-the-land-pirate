from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[3]
TOOL_PATH = (
    ROOT
    / "Tools"
    / "Validation"
    / "verify_wp0002_local_operator_transaction_v3.py"
)
SPEC = importlib.util.spec_from_file_location(
    "verify_wp0002_local_operator_transaction_v3",
    TOOL_PATH,
)
assert SPEC is not None and SPEC.loader is not None
tool = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(tool)


def json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


class FakeGitHub:
    def __init__(self, documents: dict[str, object]) -> None:
        self.documents = copy.deepcopy(documents)
        self.json_requests: list[str] = []

    def get_json(self, path: str):
        self.json_requests.append(path)
        if path not in self.documents:
            raise AssertionError(f"unexpected fake GitHub JSON request: {path}")
        value = copy.deepcopy(self.documents[path])
        return tool.APIResult(value, json_bytes(value), {})

    def get_bytes(self, path: str, *, accept: str):
        raise AssertionError(f"unexpected fake GitHub bytes request: {path} {accept}")


class WP0002MixedReaderRecoveryTests(unittest.TestCase):
    def branch(self) -> dict:
        return {"commit": {"sha": "1" * 40}}

    def protection(self) -> dict:
        return {
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

    def repository(self, *, squash_only: bool) -> dict:
        return {
            "id": 919191,
            "full_name": tool.REPOSITORY,
            "owner": {"login": "AC-21", "id": 424242},
            "allow_squash_merge": squash_only,
            "allow_merge_commit": not squash_only,
            "allow_rebase_merge": False,
        }

    def test_capture_routes_all_administration_fields_to_one_reader(self) -> None:
        branch_path = f"/repos/{tool.REPOSITORY}/branches/main"
        protection_path = f"{branch_path}/protection"
        repository_path = f"/repos/{tool.REPOSITORY}"
        rulesets_path = f"{repository_path}/rulesets?includes_parents=true"
        ordinary = FakeGitHub(
            {
                branch_path: self.branch(),
                repository_path: self.repository(squash_only=False),
            }
        )
        administration = FakeGitHub(
            {
                protection_path: self.protection(),
                repository_path: self.repository(squash_only=True),
                rulesets_path: [],
            }
        )

        capture = tool.capture_protection(
            ordinary,
            protection_github=administration,
        )

        self.assertEqual(ordinary.json_requests, [branch_path])
        self.assertEqual(
            administration.json_requests,
            [protection_path, repository_path, rulesets_path],
        )
        self.assertIs(capture["normalized"]["squash_only"], True)
        self.assertEqual(
            capture["raw"]["repository"],
            self.repository(squash_only=True),
        )

    def test_capture_cli_requires_and_routes_the_administration_reader(self) -> None:
        branch_path = f"/repos/{tool.REPOSITORY}/branches/main"
        protection_path = f"{branch_path}/protection"
        repository_path = f"/repos/{tool.REPOSITORY}"
        rulesets_path = f"{repository_path}/rulesets?includes_parents=true"
        ordinary = FakeGitHub({branch_path: self.branch()})
        administration = FakeGitHub(
            {
                protection_path: self.protection(),
                repository_path: self.repository(squash_only=True),
                rulesets_path: [],
            }
        )
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "protection.json"
            with mock.patch.object(
                tool._v1,
                "_reader",
                side_effect=[ordinary, administration],
            ) as reader:
                result = tool.main(
                    [
                        "--token-env",
                        "ORDINARY_TOKEN",
                        "--protection-token-env",
                        "ADMINISTRATION_TOKEN",
                        "capture-protection",
                        "--output",
                        str(output),
                    ]
                )
            self.assertEqual(result, 0)
            self.assertTrue(output.is_file())
        self.assertEqual(
            reader.call_args_list,
            [
                mock.call("ORDINARY_TOKEN"),
                mock.call("ADMINISTRATION_TOKEN", required=True),
            ],
        )
        self.assertEqual(ordinary.json_requests, [branch_path])
        self.assertEqual(
            administration.json_requests,
            [protection_path, repository_path, rulesets_path],
        )

    def test_mismatch_diagnostic_contains_field_names_only(self) -> None:
        live = {
            "main_sha": "1" * 40,
            "squash_only": True,
            "raw_sha256": "2" * 64,
        }
        recorded = {
            "main_sha": "3" * 40,
            "squash_only": False,
            "raw_sha256": "4" * 64,
        }
        fields = tool.sanitized_mismatch_fields(live, recorded)
        self.assertEqual(fields, ["main_sha", "squash_only"])
        rendered = ",".join(fields)
        for forbidden in [*live.values(), *recorded.values()]:
            if isinstance(forbidden, str):
                self.assertNotIn(forbidden, rendered)

    def test_live_recheck_uses_split_readers_and_reports_field_names_only(self) -> None:
        branch_path = f"/repos/{tool.REPOSITORY}/branches/main"
        protection_path = f"{branch_path}/protection"
        repository_path = f"/repos/{tool.REPOSITORY}"
        rulesets_path = f"{repository_path}/rulesets?includes_parents=true"
        ordinary = FakeGitHub({branch_path: self.branch()})
        expected_admin = FakeGitHub(
            {
                protection_path: self.protection(),
                repository_path: self.repository(squash_only=True),
                rulesets_path: [],
            }
        )
        expected_capture = tool.capture_protection(
            ordinary,
            protection_github=expected_admin,
        )
        ordinary.json_requests.clear()
        drifted_admin = FakeGitHub(
            {
                protection_path: self.protection(),
                repository_path: self.repository(squash_only=False),
                rulesets_path: [],
            }
        )
        with self.assertRaisesRegex(
            tool.VerificationError,
            r"^live protection mismatch fields: squash_only$",
        ) as raised:
            tool._same_live_protection(
                ordinary,
                expected_capture["normalized"],
                protection_github=drifted_admin,
            )
        rendered = str(raised.exception)
        self.assertNotIn(str(self.repository(squash_only=True)), rendered)
        self.assertNotIn(str(self.repository(squash_only=False)), rendered)
        self.assertEqual(ordinary.json_requests, [branch_path])
        self.assertEqual(
            drifted_admin.json_requests,
            [protection_path, repository_path, rulesets_path],
        )

    def test_official_live_cli_paths_thread_both_readers(self) -> None:
        ordinary = object()
        administration = object()
        repository = object()
        command_cases = [
            (
                [
                    "pre-merge",
                    "--repository-path",
                    "/tmp/repository",
                    "--authority-evidence",
                    "/tmp/authority.json",
                    "--protection-before",
                    "/tmp/before.json",
                    "--protection-during",
                    "/tmp/during.json",
                    "--output",
                    "/tmp/pre-merge.json",
                ],
                "build_pre_merge_evidence",
                {"phase": "pre-merge"},
            ),
            (
                [
                    "render-completion-body",
                    "--repository-path",
                    "/tmp/repository",
                    "--pre-merge-evidence",
                    "/tmp/pre-merge.json",
                    "--protection-after",
                    "/tmp/after.json",
                ],
                "render_completion_body",
                "completion-body",
            ),
            (
                [
                    "complete",
                    "--repository-path",
                    "/tmp/repository",
                    "--pre-merge-evidence",
                    "/tmp/pre-merge.json",
                    "--protection-after",
                    "/tmp/after.json",
                    "--completion-comment-id",
                    "73",
                    "--output",
                    "/tmp/complete.json",
                ],
                "build_complete_evidence",
                {"phase": "complete"},
            ),
        ]
        for argv, callable_name, returned in command_cases:
            with self.subTest(command=argv[0]):
                with mock.patch.object(
                    tool._v1,
                    "_reader",
                    side_effect=[ordinary, administration],
                ) as reader, mock.patch.object(
                    tool,
                    "GitRepository",
                    return_value=repository,
                ), mock.patch.object(
                    tool._v1,
                    "_read_json",
                    return_value={},
                ), mock.patch.object(
                    tool._v1,
                    "_write_json",
                ), mock.patch.object(
                    tool,
                    callable_name,
                    return_value=returned,
                ) as invoked:
                    result = tool.main(
                        [
                            "--token-env",
                            "ORDINARY_TOKEN",
                            "--protection-token-env",
                            "ADMINISTRATION_TOKEN",
                            *argv,
                        ]
                    )
                self.assertEqual(result, 0)
                self.assertEqual(
                    reader.call_args_list,
                    [
                        mock.call("ORDINARY_TOKEN"),
                        mock.call("ADMINISTRATION_TOKEN", required=True),
                    ],
                )
                self.assertIs(
                    invoked.call_args.kwargs["protection_github"],
                    administration,
                )

    def test_v3_dynamic_check_count_does_not_mutate_v1_or_v2(self) -> None:
        self.assertEqual(tool._v1.REQUIRED_CHECKS, tool.CONTROL_REQUIRED_CHECKS)
        self.assertIs(
            tool._v1.validate_pre_merge_evidence,
            tool.validate_pre_merge_evidence,
        )
        fresh_v2 = tool._load_v2()
        self.assertEqual(fresh_v2._v1.REQUIRED_CHECKS, ("validate", "wp0002-core"))
        self.assertIsNot(
            fresh_v2._v1.validate_pre_merge_evidence,
            tool.validate_pre_merge_evidence,
        )

    def test_v3_validator_uses_configured_check_cardinality(self) -> None:
        configured = ("validate", "wp0002-core", "wp0002-policy")
        head = "4" * 40
        evidence = {
            "schema_version": 1,
            "transaction_id": tool.TRANSACTION_ID,
            "phase": "pre-merge",
            "repository": {},
            "authority_evidence_sha256": "5" * 64,
            "authorization_comment": {},
            "final_pull_request": {
                "number": 1,
                "base_ref": "main",
                "base_sha": "3" * 40,
                "head_ref": "agent/recovery",
                "head_sha": head,
                "head_repository": tool.REPOSITORY,
                "deterministic_patch_sha256": "6" * 64,
                "github_patch_sha256": "7" * 64,
                "changed_files_manifest_sha256": "8" * 64,
            },
            "receipt_materialization": {
                "commit_sha": head,
                "parent_sha": "2" * 40,
                "tree_oid": "1" * 40,
                "receipt_path": tool.RECEIPT_PATH,
                "receipt_sha256": "9" * 64,
                "delta_patch_sha256": "a" * 64,
                "delta": [{}],
            },
            "required_check_runs": [
                {
                    "name": name,
                    "app_id": tool._v1.REQUIRED_APP_ID,
                    "head_sha": head,
                    "status": "completed",
                    "conclusion": "success",
                }
                for name in configured
            ],
            "protection_before": {},
            "protection_during": {},
            "raw_artifacts": {
                "pull_request": {},
                "github_patch": {},
                "check_runs": {},
                "protection_before": {},
                "protection_during": {},
            },
        }
        with mock.patch.object(
            tool,
            "CONTROL_REQUIRED_CHECKS",
            configured,
        ), mock.patch.object(
            tool._v1,
            "_validate_repository_shape",
        ), mock.patch.object(
            tool._v1,
            "_sha256_value",
        ), mock.patch.object(
            tool._v1,
            "_validate_comment_shape",
        ), mock.patch.object(
            tool._v1,
            "_validate_changed_manifest",
        ), mock.patch.object(
            tool,
            "_require_recovery_protection_contract",
        ), mock.patch.object(
            tool._v1,
            "_validate_artifact_ref",
        ):
            tool.validate_pre_merge_evidence(evidence)

    def test_recovery_protection_contract_changes_only_the_policy_check(self) -> None:
        branch_path = f"/repos/{tool.REPOSITORY}/branches/main"
        protection_path = f"{branch_path}/protection"
        repository_path = f"/repos/{tool.REPOSITORY}"
        rulesets_path = f"{repository_path}/rulesets?includes_parents=true"
        ordinary = FakeGitHub({branch_path: self.branch()})
        administration = FakeGitHub(
            {
                protection_path: self.protection(),
                repository_path: self.repository(squash_only=True),
                rulesets_path: [],
            }
        )
        before = tool.capture_protection(
            ordinary,
            protection_github=administration,
        )["normalized"]
        during = copy.deepcopy(before)
        during["required_checks"] = [
            item
            for item in during["required_checks"]
            if item["context"] in tool.CONTROL_REQUIRED_CHECKS
        ]
        tool._require_recovery_protection_contract(
            before,
            during,
            base_sha="1" * 40,
        )
        weakened = copy.deepcopy(during)
        weakened["strict"] = False
        with self.assertRaisesRegex(
            tool.VerificationError,
            "weakens a retained invariant",
        ):
            tool._require_recovery_protection_contract(
                before,
                weakened,
                base_sha="1" * 40,
            )

    def test_completion_deadline_rejects_late_comment_despite_backdated_capture(self) -> None:
        evidence = {
            "merged_pull_request": {
                "merged_at": "2026-07-18T12:00:00Z",
            },
            "protection_after": {
                "observed_at": "2026-07-18T12:01:00Z",
            },
            "completion_comment": {
                "created_at": "2026-07-18T12:10:01Z",
            },
        }
        self.assertIs(
            tool._v1.validate_complete_evidence,
            tool.validate_complete_evidence,
        )
        with mock.patch.object(
            tool,
            "_original_validate_complete_evidence",
        ):
            with self.assertRaisesRegex(
                tool.VerificationError,
                "authenticated completion comment is outside the 600-second merge window",
            ):
                tool.validate_complete_evidence(evidence)
            evidence["completion_comment"]["created_at"] = (
                "2026-07-18T12:10:00Z"
            )
            tool.validate_complete_evidence(evidence)
            evidence["completion_comment"]["created_at"] = (
                "2026-07-18T11:59:59Z"
            )
            with self.assertRaisesRegex(
                tool.VerificationError,
                "authenticated completion comment is outside the 600-second merge window",
            ):
                tool.validate_complete_evidence(evidence)

    def test_protected_base_policy_rejects_the_full_recovery_control_diff(self) -> None:
        base = tool.CONTROL_BASE_SHA
        head = subprocess.run(
            ["/usr/bin/git", "-C", str(ROOT), "rev-parse", "HEAD"],
            check=True,
            stdout=subprocess.PIPE,
            text=True,
        ).stdout.strip()
        source = subprocess.run(
            [
                "/usr/bin/git",
                "-C",
                str(ROOT),
                "show",
                f"{base}:Tools/Validation/validate_wp0002_policy.py",
            ],
            check=True,
            stdout=subprocess.PIPE,
        ).stdout
        with tempfile.TemporaryDirectory() as directory:
            temporary = Path(directory)
            policy_path = temporary / "validate_wp0002_policy.py"
            policy_path.write_bytes(source)
            module_name = "wp0002_protected_base_policy_replay"
            policy = types.ModuleType(module_name)
            policy.__file__ = str(policy_path)
            sys.modules[module_name] = policy
            try:
                exec(compile(source, str(policy_path), "exec"), policy.__dict__)
                bare = temporary / "objects.git"
                subprocess.run(
                    ["/usr/bin/git", "clone", "--bare", str(ROOT), str(bare)],
                    check=True,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                )
                report, errors = policy.validate_repository_policy(
                    bare,
                    base=base,
                    head=head,
                    base_ref="main",
                    head_ref="agent/wp0002-recovery-v3",
                    base_repository=tool.REPOSITORY,
                    head_repository=tool.REPOSITORY,
                    policy_source_sha=base,
                    fetch_head=False,
                )
            finally:
                sys.modules.pop(module_name, None)
        self.assertEqual(report["phase"], "implementation")
        self.assertTrue(errors)
        rendered = "\n".join(errors)
        self.assertIn("diff modifies frozen self-verification", rendered)
        self.assertIn("implementation diff escapes exact WP-0002 scope", rendered)

    def test_caching_reader_observes_each_json_path_once(self) -> None:
        path = "/repos/AC-21/sasha-the-land-pirate"
        delegate = FakeGitHub({path: {"version": 1}})
        reader = tool._CachingReader(delegate)
        first = reader.get_json(path)
        delegate.documents[path] = {"version": 2}
        second = reader.get_json(path)
        self.assertIs(first, second)
        self.assertEqual(first.data, {"version": 1})
        self.assertEqual(delegate.json_requests, [path])

    def test_workflow_triggers_recovery_and_forbids_both_predecessors(self) -> None:
        workflow = (ROOT / ".github" / "workflows" / "wp0002-policy.yml").read_text(
            encoding="utf-8"
        )
        closure_block = workflow.split("closure_paths=(", 1)[1].split(")", 1)[0]
        self.assertIn("local-operator-recovery/authority.json", closure_block)
        self.assertIn("local-operator-recovery/pre-merge.json", closure_block)
        self.assertIn("local-operator-recovery/complete.json", closure_block)
        self.assertNotIn("local-operator-successor/", closure_block)
        forbidden_block = workflow.split(
            "forbidden_predecessor_paths=(", 1
        )[1].split(")", 1)[0]
        for namespace in ("local-operator-amendment", "local-operator-successor"):
            for phase in ("authority", "pre-merge", "complete"):
                self.assertIn(f"{namespace}/{phase}.json", forbidden_block)

    def test_workflow_hash_pins_the_exact_v3_verifier(self) -> None:
        workflow = (ROOT / ".github" / "workflows" / "wp0002-policy.yml").read_text(
            encoding="utf-8"
        )
        actual = hashlib.sha256(TOOL_PATH.read_bytes()).hexdigest()
        self.assertIn(
            'verifier="Tools/Validation/verify_wp0002_local_operator_transaction_v3.py"',
            workflow,
        )
        self.assertIn(f'expected_verifier_sha256="{actual}"', workflow)

    def test_loader_restores_absent_none_and_existing_entries(self) -> None:
        name = tool._V2_MODULE_NAME
        original = sys.modules.pop(name, tool._MISSING)
        try:
            tool._load_v2()
            self.assertNotIn(name, sys.modules)
            for sentinel in (None, object()):
                sys.modules[name] = sentinel
                tool._load_v2()
                self.assertIs(sys.modules[name], sentinel)
        finally:
            if original is tool._MISSING:
                sys.modules.pop(name, None)
            else:
                sys.modules[name] = original

    def test_loader_hashes_before_execution_and_cleans_up_exception(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "v2.py"
            source = b"raise RuntimeError('must not survive')\n"
            path.write_bytes(source)
            name = tool._V2_MODULE_NAME
            sentinel = object()
            sys.modules[name] = sentinel
            with mock.patch.object(tool, "V2_VERIFIER_PATH", path):
                with mock.patch.object(tool, "V2_VERIFIER_SHA256", "0" * 64):
                    with self.assertRaisesRegex(tool.V3LoaderError, "hash mismatch"):
                        tool._load_v2()
                self.assertIs(sys.modules[name], sentinel)
                with mock.patch.object(
                    tool,
                    "V2_VERIFIER_SHA256",
                    hashlib.sha256(source).hexdigest(),
                ):
                    with self.assertRaisesRegex(tool.V3LoaderError, "cannot load"):
                        tool._load_v2()
                self.assertIs(sys.modules[name], sentinel)
            sys.modules.pop(name, None)


if __name__ == "__main__":
    unittest.main()
