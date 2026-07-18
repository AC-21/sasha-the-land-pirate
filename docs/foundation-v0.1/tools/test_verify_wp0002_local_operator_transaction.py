from __future__ import annotations

import copy
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from datetime import datetime, timezone
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[3]
TOOL_PATH = ROOT / "Tools" / "Validation" / "verify_wp0002_local_operator_transaction.py"
SPEC = importlib.util.spec_from_file_location("verify_wp0002_local_operator_transaction", TOOL_PATH)
assert SPEC is not None and SPEC.loader is not None
tool = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = tool
SPEC.loader.exec_module(tool)

FOUNDATION_PATH = ROOT / "docs" / "foundation-v0.1" / "tools" / "validate_foundation.py"
FOUNDATION_SPEC = importlib.util.spec_from_file_location(
    "validate_foundation_for_transaction_test", FOUNDATION_PATH
)
assert FOUNDATION_SPEC is not None and FOUNDATION_SPEC.loader is not None
foundation = importlib.util.module_from_spec(FOUNDATION_SPEC)
sys.modules[FOUNDATION_SPEC.name] = foundation
FOUNDATION_SPEC.loader.exec_module(foundation)


def json_bytes(value: object) -> bytes:
    return tool.canonical_json_bytes(value)


class FakeGitHub:
    def __init__(self) -> None:
        self.documents: dict[str, object] = {}
        self.bytes: dict[tuple[str, str], bytes] = {}
        self.json_requests: list[str] = []

    def get_json(self, path: str):
        self.json_requests.append(path)
        if path not in self.documents:
            raise AssertionError(f"unexpected fake GitHub JSON request: {path}")
        value = copy.deepcopy(self.documents[path])
        return tool.APIResult(value, json_bytes(value), {})

    def get_bytes(self, path: str, *, accept: str):
        key = (path, accept)
        if key not in self.bytes:
            raise AssertionError(f"unexpected fake GitHub byte request: {key}")
        return tool.APIResult(None, self.bytes[key], {})


class TransactionFixture(unittest.TestCase):
    OWNER_ID = 424242
    REPOSITORY_ID = 919191
    PR = 77
    AUTH_COMMENT = 8801
    COMPLETE_COMMENT = 8802

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.repo = Path(self.temporary.name) / "repo"
        self.repo.mkdir()
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Transaction Test")
        self.git("config", "user.email", "transaction@example.invalid")
        self.write("AGENTS.md", b"base agent contract\n")
        self.write("docs/foundation-v0.1/README.md", b"base foundation\n")
        self.git("add", "AGENTS.md", "docs/foundation-v0.1/README.md")
        self.git("commit", "-m", "base")
        self.base = self.git_text("rev-parse", "HEAD")
        self.git("checkout", "-b", "agent/wp0002-control")
        self.write("AGENTS.md", b"amended agent contract\n")
        self.write(
            "docs/foundation-v0.1/governance/WP-0002-DELEGATED-LOCAL-UNITY-OPERATOR-AMENDMENT.md",
            b"delegated operator amendment\n",
        )
        self.write(
            "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json",
            b'{"test":"amended-boundary"}\n',
        )
        self.write(
            "docs/foundation-v0.1/work-packets/proposed/WP-0002.json",
            b'{"test":"amended-packet"}\n',
        )
        self.git("add", "AGENTS.md", "docs/foundation-v0.1")
        self.git("commit", "-m", "stage1")
        self.stage1 = self.git_text("rev-parse", "HEAD")
        self.repository = tool.GitRepository(self.repo)
        self.github = FakeGitHub()
        self.github.documents[f"/repos/{tool.REPOSITORY}"] = self.repository_payload()
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/rulesets?includes_parents=true"
        ] = []
        self.github.documents[f"/repos/{tool.REPOSITORY}/pulls/{self.PR}"] = self.pull_payload(self.stage1)
        body = tool.render_authorization_body(self.repository, self.base, self.stage1)
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/issues/comments/{self.AUTH_COMMENT}"
        ] = self.comment_payload(self.AUTH_COMMENT, body, "2026-07-17T20:00:00Z")
        self.authority = tool.build_authority_evidence(
            self.repository,
            self.github,
            pull_number=self.PR,
            comment_id=self.AUTH_COMMENT,
            stage1_sha=self.stage1,
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

    def git_text(self, *args: str) -> str:
        return self.git(*args).stdout.decode("utf-8").strip()

    def write(self, relative: str, data: bytes) -> None:
        path = self.repo / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def repository_payload(self) -> dict:
        return {
            "id": self.REPOSITORY_ID,
            "full_name": tool.REPOSITORY,
            "owner": {"login": tool.OWNER_LOGIN, "id": self.OWNER_ID},
            "allow_squash_merge": True,
            "allow_merge_commit": False,
            "allow_rebase_merge": False,
            "allow_auto_merge": True,
        }

    def pull_payload(
        self,
        head: str,
        *,
        merged: bool = False,
        merge_commit: str | None = None,
    ) -> dict:
        return {
            "number": self.PR,
            "url": f"{tool.GITHUB_API}/repos/{tool.REPOSITORY}/pulls/{self.PR}",
            "html_url": f"{tool.GITHUB_WEB}/{tool.REPOSITORY}/pull/{self.PR}",
            "base": {"ref": "main", "sha": self.base},
            "head": {
                "ref": "agent/wp0002-control",
                "sha": head,
                "repo": {"full_name": tool.REPOSITORY},
            },
            "auto_merge": None,
            "merged": merged,
            "merged_at": "2026-07-17T20:03:00Z" if merged else None,
            "merge_commit_sha": merge_commit,
        }

    def comment_payload(self, comment_id: int, body: str, timestamp: str) -> dict:
        return {
            "id": comment_id,
            "node_id": f"IC_{comment_id}",
            "url": f"{tool.GITHUB_API}/repos/{tool.REPOSITORY}/issues/comments/{comment_id}",
            "html_url": f"{tool.GITHUB_WEB}/{tool.REPOSITORY}/pull/{self.PR}#issuecomment-{comment_id}",
            "issue_url": f"{tool.GITHUB_API}/repos/{tool.REPOSITORY}/issues/{self.PR}",
            "user": {"login": tool.OWNER_LOGIN, "id": self.OWNER_ID, "type": "User"},
            "author_association": "OWNER",
            "created_at": timestamp,
            "updated_at": timestamp,
            "body": body,
        }

    def protection_payload(self, checks: tuple[str, ...]) -> dict:
        return {
            "required_status_checks": {
                "strict": True,
                "checks": [
                    {"context": name, "app_id": tool.REQUIRED_APP_ID}
                    for name in checks
                ],
            },
            "enforce_admins": {"enabled": True},
            "required_pull_request_reviews": {
                "dismiss_stale_reviews": False,
                "require_code_owner_reviews": False,
                "required_approving_review_count": 0,
                "require_last_push_approval": False,
                "bypass_pull_request_allowances": {
                    "users": [], "teams": [], "apps": []
                },
            },
            "restrictions": {"users": [], "teams": [], "apps": []},
            "required_conversation_resolution": {"enabled": True},
            "required_linear_history": {"enabled": True},
            "allow_force_pushes": {"enabled": False},
            "allow_deletions": {"enabled": False},
        }

    def set_live_protection(self, main_sha: str, checks: tuple[str, ...]) -> None:
        self.github.documents[f"/repos/{tool.REPOSITORY}/branches/main"] = {
            "commit": {"sha": main_sha}
        }
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/branches/main/protection"
        ] = self.protection_payload(checks)

    def protection_reader(self) -> FakeGitHub:
        reader = FakeGitHub()
        for path in (
            f"/repos/{tool.REPOSITORY}/branches/main/protection",
            f"/repos/{tool.REPOSITORY}/rulesets?includes_parents=true",
        ):
            reader.documents[path] = copy.deepcopy(self.github.documents[path])
        return reader

    def capture(self, main_sha: str, checks: tuple[str, ...], at: str) -> dict:
        self.set_live_protection(main_sha, checks)
        return tool.capture_protection(
            self.github,
            observed_at=datetime.fromisoformat(at.replace("Z", "+00:00")),
        )

    def materialize_receipt(
        self,
        *,
        extra_artifact: bool = False,
        extra_file: bool = False,
    ) -> str:
        comment = self.authority["authorization_comment"]
        source = comment["html_url"]
        artifacts = {
            source.removeprefix("https://"): comment["body_utf8_sha256"]
        }
        for item in self.authority["stage1"]["changed_files"]:
            artifacts[item["path"]] = item["new_blob_sha256"]
        if extra_artifact:
            artifacts["invented.txt"] = "11" * 32
        receipt = {
            "receipt_id": tool.RECEIPT_ID,
            "issued_at": "2026-07-17T20:00:30Z",
            "issued_by": tool.OWNER_LOGIN,
            "issuer_role": "creator",
            "receipt_kind": "creator-authorization",
            "artifact_resolver": {
                "type": "external-protected",
                "resolver_reference": source,
            },
            "source_reference": source,
            "subject_ids": ["WP-0002"],
            "subject_claims": [
                {"subject_id": "WP-0002", "claims": [tool.AUTHORIZATION_CLAIM]}
            ],
            "approval_text_sha256": comment["body_utf8_sha256"],
            "accepted_commit": self.stage1,
            "artifact_sha256": artifacts,
            "subject_contract_sha256": {"WP-0002": tool.CONTRACT_SHA256},
            "subject_event_sha256": {},
            "foundation_binding": None,
            "signature_reference": source,
            "sealed": True,
        }
        self.write(tool.RECEIPT_PATH, json.dumps(receipt, indent=2, sort_keys=True).encode() + b"\n")
        self.git("add", tool.RECEIPT_PATH)
        if extra_file:
            self.write("unexpected.txt", b"not receipt-only\n")
            self.git("add", "unexpected.txt")
        self.git("commit", "-m", "receipt")
        return self.git_text("rev-parse", "HEAD")

    def prepare_pre_merge(
        self,
        *,
        extra_artifact: bool = False,
        extra_file: bool = False,
        weaken_during: bool = False,
    ) -> dict:
        before = self.capture(
            self.base,
            tool.FULL_REQUIRED_CHECKS,
            "2026-07-17T20:01:00Z",
        )
        final_head = self.materialize_receipt(
            extra_artifact=extra_artifact,
            extra_file=extra_file,
        )
        self.github.documents[f"/repos/{tool.REPOSITORY}/pulls/{self.PR}"] = self.pull_payload(final_head)
        self.github.bytes[
            (
                f"/repos/{tool.REPOSITORY}/pulls/{self.PR}",
                "application/vnd.github.patch",
            )
        ] = b"github final patch bytes\n"
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/commits/{final_head}/check-runs?per_page=100"
        ] = {
            "check_runs": [
                {
                    "id": index + 10,
                    "name": name,
                    "head_sha": final_head,
                    "status": "completed",
                    "conclusion": "success",
                    "app": {"id": tool.REQUIRED_APP_ID},
                }
                for index, name in enumerate(tool.REQUIRED_CHECKS)
            ]
        }
        during = self.capture(
            self.base,
            ("validate",) if weaken_during else tool.REQUIRED_CHECKS,
            "2026-07-17T20:02:00Z",
        )
        self.set_live_protection(
            self.base,
            ("validate",) if weaken_during else tool.REQUIRED_CHECKS,
        )
        return tool.build_pre_merge_evidence(
            self.repository,
            self.github,
            self.authority,
            before,
            during,
            now=datetime(2026, 7, 17, 20, 2, tzinfo=timezone.utc),
        )

    def make_squash(self, final_head: str) -> str:
        tree = self.git_text("rev-parse", f"{final_head}^{{tree}}")
        merge = self.git_text("commit-tree", tree, "-p", self.base, "-m", "squash merge")
        self.git("update-ref", "refs/heads/main", merge)
        return merge

    def prepare_complete(self, pre_merge: dict, *, restore_policy: bool = True) -> dict:
        final_head = pre_merge["final_pull_request"]["head_sha"]
        merge = self.make_squash(final_head)
        self.github.documents[f"/repos/{tool.REPOSITORY}/pulls/{self.PR}"] = self.pull_payload(
            final_head,
            merged=True,
            merge_commit=merge,
        )
        after_checks = tool.FULL_REQUIRED_CHECKS if restore_policy else tool.REQUIRED_CHECKS
        after = self.capture(merge, after_checks, "2026-07-17T20:04:00Z")
        self.set_live_protection(merge, after_checks)
        body = tool.render_completion_body(
            self.repository,
            self.github,
            pre_merge,
            after,
            now=datetime(2026, 7, 17, 20, 4, tzinfo=timezone.utc),
        )
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/issues/comments/{self.COMPLETE_COMMENT}"
        ] = self.comment_payload(self.COMPLETE_COMMENT, body, "2026-07-17T20:05:00Z")
        return tool.build_complete_evidence(
            self.repository,
            self.github,
            pre_merge,
            after,
            completion_comment_id=self.COMPLETE_COMMENT,
            now=datetime(2026, 7, 17, 20, 4, tzinfo=timezone.utc),
        )

    def materialize_closure(
        self,
        pre_merge: dict,
        complete: dict,
        *,
        extra_file: bool = False,
        reports: tuple[dict, dict, dict] | None = None,
    ) -> tuple[str, str]:
        self.git("checkout", "main")
        self.git("checkout", "-b", "agent/wp0002-evidence-closure")
        base = self.git_text("rev-parse", "HEAD")
        values = reports or (self.authority, pre_merge, complete)
        for path, value in zip(tool.CLOSURE_EVIDENCE_PATHS, values, strict=True):
            self.write(
                path,
                json.dumps(value, indent=2, sort_keys=True).encode("utf-8") + b"\n",
            )
            self.git("add", path)
        if extra_file:
            self.write("unexpected-closure-file.txt", b"not evidence-only\n")
            self.git("add", "unexpected-closure-file.txt")
        self.git("commit", "-m", "add WP-0002 transaction evidence")
        return base, self.git_text("rev-parse", "HEAD")

    def bare_repository(self) -> tool.GitRepository:
        bare = Path(self.temporary.name) / "objects.git"
        result = subprocess.run(
            ["/usr/bin/git", "clone", "--bare", str(self.repo), str(bare)],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
        )
        if result.returncode != 0:
            self.fail(result.stderr.decode("utf-8", "replace"))
        return tool.GitRepository(bare)


class WP0002LocalOperatorTransactionTests(TransactionFixture):
    def test_three_phase_transaction_passes(self) -> None:
        pre_merge = self.prepare_pre_merge()
        complete = self.prepare_complete(pre_merge)
        self.assertEqual(self.authority["phase"], "authority")
        self.assertEqual(pre_merge["phase"], "pre-merge")
        self.assertEqual(complete["phase"], "complete")
        self.assertTrue(complete["merge"]["tree_matches_final_head"])
        self.assertEqual(
            complete["parsed_completion_binding"]["claim"],
            tool.COMPLETION_CLAIM,
        )
        self.assertEqual(
            {item["context"] for item in complete["protection_after"]["required_checks"]},
            set(tool.FULL_REQUIRED_CHECKS),
        )
        live_authorization = self.github.get_json(
            f"/repos/{tool.REPOSITORY}/issues/comments/{self.AUTH_COMMENT}"
        )
        self.assertEqual(
            complete["raw_artifacts"]["authorization_comment"],
            tool._artifact_ref(live_authorization.raw),
        )
        schema = json.loads(
            (
                ROOT
                / "docs"
                / "foundation-v0.1"
                / "schemas"
                / "wp0002-local-operator-transaction-evidence.schema.json"
            ).read_text(encoding="utf-8")
        )
        for evidence in (self.authority, pre_merge, complete):
            self.assertEqual(
                foundation.validate_schema_subset(
                    evidence,
                    schema,
                    schema,
                    f"{evidence['phase']} evidence",
                ),
                [],
            )
        tool.validate_complete_evidence(complete)

    def test_authority_rejects_wrong_owner_identity_and_edited_body(self) -> None:
        comment_path = f"/repos/{tool.REPOSITORY}/issues/comments/{self.AUTH_COMMENT}"
        wrong_actor = copy.deepcopy(self.github.documents[comment_path])
        wrong_actor["user"]["id"] += 1
        self.github.documents[comment_path] = wrong_actor
        with self.assertRaisesRegex(tool.VerificationError, "live repository owner"):
            tool.build_authority_evidence(
                self.repository,
                self.github,
                pull_number=self.PR,
                comment_id=self.AUTH_COMMENT,
                stage1_sha=self.stage1,
            )
        edited = copy.deepcopy(wrong_actor)
        edited["user"]["id"] = self.OWNER_ID
        edited["updated_at"] = "2026-07-17T20:00:01Z"
        self.github.documents[comment_path] = edited
        with self.assertRaisesRegex(tool.VerificationError, "remain unedited"):
            tool.build_authority_evidence(
                self.repository,
                self.github,
                pull_number=self.PR,
                comment_id=self.AUTH_COMMENT,
                stage1_sha=self.stage1,
            )

    def test_authority_rejects_noncanonical_or_misbound_body(self) -> None:
        comment_path = f"/repos/{tool.REPOSITORY}/issues/comments/{self.AUTH_COMMENT}"
        comment = copy.deepcopy(self.github.documents[comment_path])
        parsed = json.loads(comment["body"])
        comment["body"] = json.dumps(parsed, indent=2, sort_keys=True)
        self.github.documents[comment_path] = comment
        with self.assertRaisesRegex(tool.VerificationError, "canonical JSON"):
            tool.build_authority_evidence(
                self.repository,
                self.github,
                pull_number=self.PR,
                comment_id=self.AUTH_COMMENT,
                stage1_sha=self.stage1,
            )

    def test_normalized_evidence_rejects_extra_keys(self) -> None:
        candidate = copy.deepcopy(self.authority)
        candidate["invented"] = True
        with self.assertRaisesRegex(tool.VerificationError, "extra=.*invented"):
            tool.validate_authority_evidence(candidate)
        candidate = copy.deepcopy(self.authority)
        candidate["authorization_comment"]["actor"]["invented"] = True
        with self.assertRaisesRegex(tool.VerificationError, "extra=.*invented"):
            tool.validate_authority_evidence(candidate)
        candidate = copy.deepcopy(self.authority)
        candidate["authorization_comment"]["body_utf8_sha256"] = "00" * 32
        with self.assertRaisesRegex(tool.VerificationError, "body hash"):
            tool.validate_authority_evidence(candidate)

    def test_pre_merge_rejects_non_receipt_delta(self) -> None:
        with self.assertRaisesRegex(tool.VerificationError, "receipt file"):
            self.prepare_pre_merge(extra_file=True)

    def test_pre_merge_rejects_extra_receipt_artifact_key(self) -> None:
        with self.assertRaisesRegex(tool.VerificationError, "artifact keys or values"):
            self.prepare_pre_merge(extra_artifact=True)

    def test_pre_merge_rejects_wrong_check_app_and_protection_weakening(self) -> None:
        before = self.capture(self.base, tool.FULL_REQUIRED_CHECKS, "2026-07-17T20:01:00Z")
        final_head = self.materialize_receipt()
        self.github.documents[f"/repos/{tool.REPOSITORY}/pulls/{self.PR}"] = self.pull_payload(final_head)
        self.github.bytes[(f"/repos/{tool.REPOSITORY}/pulls/{self.PR}", "application/vnd.github.patch")] = b"patch"
        self.github.documents[f"/repos/{tool.REPOSITORY}/commits/{final_head}/check-runs?per_page=100"] = {
            "check_runs": [
                {"id": 1, "name": name, "head_sha": final_head, "status": "completed", "conclusion": "success", "app": {"id": 999}}
                for name in tool.REQUIRED_CHECKS
            ]
        }
        during = self.capture(self.base, tool.REQUIRED_CHECKS, "2026-07-17T20:02:00Z")
        with self.assertRaisesRegex(tool.VerificationError, "not from app"):
            tool.build_pre_merge_evidence(
                self.repository, self.github, self.authority, before, during,
                verify_live_during=False,
            )

    def test_pre_merge_rejects_repository_ruleset_inventory(self) -> None:
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/rulesets?includes_parents=true"
        ] = [{"id": 1, "name": "unexpected-ruleset"}]
        with self.assertRaisesRegex(
            tool.VerificationError, "weakens a retained invariant"
        ):
            self.prepare_pre_merge()

    def test_protection_capture_accepts_live_null_empty_actor_shape(self) -> None:
        self.set_live_protection(self.base, tool.FULL_REQUIRED_CHECKS)
        path = f"/repos/{tool.REPOSITORY}/branches/main/protection"
        protection = self.github.documents[path]
        assert isinstance(protection, dict)
        protection["restrictions"] = None
        reviews = protection["required_pull_request_reviews"]
        assert isinstance(reviews, dict)
        reviews.pop("bypass_pull_request_allowances")
        capture = tool.capture_protection(self.github)
        normalized = capture["normalized"]
        self.assertIs(normalized["bypass_allowances_empty"], True)
        self.assertIs(normalized["push_restrictions_empty"], True)

    def test_complete_rejects_missing_policy_restoration(self) -> None:
        pre_merge = self.prepare_pre_merge()
        with self.assertRaisesRegex(tool.VerificationError, "restore all three checks"):
            self.prepare_complete(pre_merge, restore_policy=False)

    def test_complete_rejects_restoration_capture_after_600_seconds(self) -> None:
        pre_merge = self.prepare_pre_merge()
        final_head = pre_merge["final_pull_request"]["head_sha"]
        merge = self.make_squash(final_head)
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/pulls/{self.PR}"
        ] = self.pull_payload(final_head, merged=True, merge_commit=merge)
        after = self.capture(
            merge,
            tool.FULL_REQUIRED_CHECKS,
            "2026-07-17T20:14:01Z",
        )
        self.set_live_protection(merge, tool.FULL_REQUIRED_CHECKS)
        with self.assertRaisesRegex(tool.VerificationError, "600-second"):
            tool.render_completion_body(
                self.repository,
                self.github,
                pre_merge,
                after,
                verify_live_after=False,
            )

    def test_complete_rejects_unsealed_completion_comment(self) -> None:
        pre_merge = self.prepare_pre_merge()
        complete = self.prepare_complete(pre_merge)
        self.assertEqual(
            complete["next_canary_required"],
            "first-post-restoration-implementation-pr",
        )
        comment_path = f"/repos/{tool.REPOSITORY}/issues/comments/{self.COMPLETE_COMMENT}"
        comment = copy.deepcopy(self.github.documents[comment_path])
        body = json.loads(comment["body"])
        body["merge_tree_oid"] = "0" * 40
        comment["body"] = tool.canonical_json_bytes(body).decode()
        self.github.documents[comment_path] = comment
        # Rebuild against the already merged state and a fresh equivalent after capture.
        after = self.capture(
            complete["merged_pull_request"]["merge_commit_sha"],
            tool.FULL_REQUIRED_CHECKS,
            "2026-07-17T20:04:00Z",
        )
        with self.assertRaisesRegex(tool.VerificationError, "does not seal"):
            tool.build_complete_evidence(
                self.repository,
                self.github,
                pre_merge,
                after,
                completion_comment_id=self.COMPLETE_COMMENT,
                verify_live_after=False,
            )

    def test_complete_offline_validation_binds_comment_body_hash(self) -> None:
        pre_merge = self.prepare_pre_merge()
        complete = self.prepare_complete(pre_merge)
        candidate = copy.deepcopy(complete)
        candidate["parsed_completion_binding"]["final_patch_sha256"] = "12" * 32
        with self.assertRaisesRegex(tool.VerificationError, "body hash"):
            tool.validate_complete_evidence(candidate)

    def test_complete_refetches_authorization_comment_and_rejects_drift(self) -> None:
        pre_merge = self.prepare_pre_merge()
        final_head = pre_merge["final_pull_request"]["head_sha"]
        merge = self.make_squash(final_head)
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/pulls/{self.PR}"
        ] = self.pull_payload(final_head, merged=True, merge_commit=merge)
        after = self.capture(
            merge,
            tool.FULL_REQUIRED_CHECKS,
            "2026-07-17T20:04:00Z",
        )
        body = tool.render_completion_body(
            self.repository,
            self.github,
            pre_merge,
            after,
            now=datetime(2026, 7, 17, 20, 4, tzinfo=timezone.utc),
        )
        self.github.documents[
            f"/repos/{tool.REPOSITORY}/issues/comments/{self.COMPLETE_COMMENT}"
        ] = self.comment_payload(
            self.COMPLETE_COMMENT, body, "2026-07-17T20:05:00Z"
        )
        auth_path = (
            f"/repos/{tool.REPOSITORY}/issues/comments/{self.AUTH_COMMENT}"
        )
        drifted = copy.deepcopy(self.github.documents[auth_path])
        binding = json.loads(drifted["body"])
        binding["stage1_patch_sha256"] = "00" * 32
        drifted["body"] = tool.canonical_json_bytes(binding).decode("utf-8")
        self.github.documents[auth_path] = drifted
        with self.assertRaisesRegex(
            tool.VerificationError, "authorization comment changed"
        ):
            tool.build_complete_evidence(
                self.repository,
                self.github,
                pre_merge,
                after,
                completion_comment_id=self.COMPLETE_COMMENT,
                now=datetime(2026, 7, 17, 20, 4, tzinfo=timezone.utc),
            )

    def test_evidence_closure_rejects_any_extra_delta_path(self) -> None:
        pre_merge = self.prepare_pre_merge()
        complete = self.prepare_complete(pre_merge)
        base, head = self.materialize_closure(
            pre_merge, complete, extra_file=True
        )
        with self.assertRaisesRegex(
            tool.VerificationError, "exactly the three regular report files"
        ):
            tool.verify_evidence_closure(
                self.bare_repository(),
                self.github,
                base_sha=base,
                head_sha=head,
                protection_github=self.protection_reader(),
            )

    def test_evidence_closure_rejects_self_consistent_forgery_when_live_comment_differs(
        self,
    ) -> None:
        pre_merge = self.prepare_pre_merge()
        complete = self.prepare_complete(pre_merge)
        fake_authority = copy.deepcopy(self.authority)
        fake_authority["raw_artifacts"]["repository"]["sha256"] = "11" * 32
        fake_pre_merge = copy.deepcopy(pre_merge)
        fake_pre_merge["authority_evidence_sha256"] = tool.evidence_sha256(
            fake_authority
        )
        fake_complete = copy.deepcopy(complete)
        fake_complete["pre_merge_evidence_sha256"] = tool.evidence_sha256(
            fake_pre_merge
        )
        fake_binding = fake_complete["parsed_completion_binding"]
        fake_binding["authority_evidence_sha256"] = tool.evidence_sha256(
            fake_authority
        )
        fake_binding["pre_merge_evidence_sha256"] = tool.evidence_sha256(
            fake_pre_merge
        )
        fake_complete["completion_comment"]["body_utf8_sha256"] = tool._sha256(
            tool.canonical_json_bytes(fake_binding)
        )
        tool.validate_authority_evidence(fake_authority)
        tool.validate_pre_merge_evidence(fake_pre_merge)
        tool.validate_complete_evidence(fake_complete)
        base, head = self.materialize_closure(
            fake_pre_merge,
            fake_complete,
            reports=(fake_authority, fake_pre_merge, fake_complete),
        )
        with self.assertRaisesRegex(
            tool.VerificationError, "live owner completion comment/hash chain"
        ):
            tool.verify_evidence_closure(
                self.bare_repository(),
                self.github,
                base_sha=base,
                head_sha=head,
                protection_github=self.protection_reader(),
            )

    def test_evidence_closure_passes_from_bare_object_repository(self) -> None:
        pre_merge = self.prepare_pre_merge()
        complete = self.prepare_complete(pre_merge)
        base, head = self.materialize_closure(pre_merge, complete)
        result = tool.verify_evidence_closure(
            self.bare_repository(),
            self.github,
            base_sha=base,
            head_sha=head,
            protection_github=self.protection_reader(),
            now=datetime(2026, 7, 17, 20, 6, tzinfo=timezone.utc),
        )
        self.assertEqual(result["closure"]["base_sha"], base)
        self.assertEqual(result["closure"]["head_sha"], head)
        self.assertEqual(
            result["report_object_sha256"]["complete"],
            tool.evidence_sha256(complete),
        )
        self.assertEqual(
            set(result["live_owner_provenance"]),
            {"authorization_binding", "completion_binding"},
        )

    def test_evidence_closure_uses_separate_protection_reader(self) -> None:
        pre_merge = self.prepare_pre_merge()
        complete = self.prepare_complete(pre_merge)
        base, head = self.materialize_closure(pre_merge, complete)
        protection_github = FakeGitHub()
        for path in (
            f"/repos/{tool.REPOSITORY}/branches/main/protection",
            f"/repos/{tool.REPOSITORY}/rulesets?includes_parents=true",
        ):
            protection_github.documents[path] = copy.deepcopy(
                self.github.documents[path]
            )
        del self.github.documents[
            f"/repos/{tool.REPOSITORY}/branches/main/protection"
        ]
        del self.github.documents[
            f"/repos/{tool.REPOSITORY}/rulesets?includes_parents=true"
        ]
        result = tool.verify_evidence_closure(
            self.bare_repository(),
            self.github,
            base_sha=base,
            head_sha=head,
            now=datetime(2026, 7, 17, 20, 6, tzinfo=timezone.utc),
            protection_github=protection_github,
        )
        self.assertEqual(result["closure"]["base_sha"], base)
        self.assertEqual(
            protection_github.json_requests,
            [
                f"/repos/{tool.REPOSITORY}/branches/main/protection",
                f"/repos/{tool.REPOSITORY}/rulesets?includes_parents=true",
            ],
        )

    def test_required_protection_reader_fails_closed_when_secret_is_absent(
        self,
    ) -> None:
        with mock.patch.dict(tool.os.environ, {}, clear=True):
            with self.assertRaisesRegex(
                tool.VerificationError,
                "required GitHub credential environment variable is absent",
            ):
                tool._reader("WP0002_PROTECTION_TOKEN", required=True)


if __name__ == "__main__":
    unittest.main()
