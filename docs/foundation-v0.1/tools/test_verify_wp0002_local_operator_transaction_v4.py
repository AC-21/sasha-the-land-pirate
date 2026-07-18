from __future__ import annotations

import copy
import hashlib
import importlib.util
import json
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
    / "verify_wp0002_local_operator_transaction_v4.py"
)
FOUNDATION_PATH = (
    ROOT / "docs" / "foundation-v0.1" / "tools" / "validate_foundation.py"
)
V3_PATH = TOOL_PATH.with_name(
    "verify_wp0002_local_operator_transaction_v3.py"
)
SPEC = importlib.util.spec_from_file_location(
    "verify_wp0002_local_operator_transaction_v4",
    TOOL_PATH,
)
assert SPEC is not None and SPEC.loader is not None
tool = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(tool)
FOUNDATION_SPEC = importlib.util.spec_from_file_location(
    "validate_foundation_for_wp0002_v4_tests",
    FOUNDATION_PATH,
)
assert FOUNDATION_SPEC is not None and FOUNDATION_SPEC.loader is not None
foundation = importlib.util.module_from_spec(FOUNDATION_SPEC)
FOUNDATION_SPEC.loader.exec_module(foundation)


def json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def graphql_payload(*, squash_only: bool) -> dict[str, object]:
    return {
        "data": {
            "repository": {
                "id": tool.REPOSITORY_GRAPHQL_ID,
                "nameWithOwner": tool.REPOSITORY,
                "mergeCommitAllowed": not squash_only,
                "rebaseMergeAllowed": False,
                "squashMergeAllowed": squash_only,
            }
        }
    }


class FakeGitHub:
    def __init__(
        self,
        documents: dict[str, object],
        *,
        merge_payload: dict[str, object] | None = None,
    ) -> None:
        self.documents = copy.deepcopy(documents)
        self.merge_payload = copy.deepcopy(merge_payload)
        self.json_requests: list[str] = []
        self.graphql_requests = 0

    def get_json(self, path: str):
        self.json_requests.append(path)
        if path not in self.documents:
            raise AssertionError(f"unexpected fake GitHub JSON request: {path}")
        value = copy.deepcopy(self.documents[path])
        return tool.APIResult(value, json_bytes(value), {})

    def get_bytes(self, path: str, *, accept: str):
        raise AssertionError(f"unexpected fake GitHub bytes request: {path} {accept}")

    def get_repository_merge_methods(self):
        self.graphql_requests += 1
        if self.merge_payload is None:
            raise AssertionError("unexpected fake GitHub GraphQL request")
        payload = copy.deepcopy(self.merge_payload)
        return tool._repository_merge_method_projection(
            tool.APIResult(payload, json_bytes(payload), {})
        )


class FakeResponse:
    def __init__(self, payload: object) -> None:
        self.raw = json_bytes(payload)
        self.headers = {"X-GitHub-Request-Id": "request-1"}

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, traceback) -> None:
        return None

    def read(self) -> bytes:
        return self.raw


class FakeFixRepository:
    def __init__(self, base_sha: str) -> None:
        self.base_sha = base_sha
        self.head_sha = base_sha
        self.parents = [tool.ORIGINAL_CONTROL_SQUASH_SHA]
        self.blobs: dict[tuple[str, str], bytes] = {}
        for path in tool.V4_FIX_PATHS:
            self.blobs[(base_sha, path)] = (ROOT / path).read_bytes()
        v3_path = "Tools/Validation/verify_wp0002_local_operator_transaction_v3.py"
        v3 = (ROOT / v3_path).read_bytes()
        self.blobs[(tool.ORIGINAL_CONTROL_SQUASH_SHA, v3_path)] = v3
        self.blobs[(base_sha, v3_path)] = v3
        self.delta = []
        for path in sorted(tool.V4_FIX_PATHS):
            added = path in {tool.V4_VERIFIER_PATH, tool.V4_TEST_PATH}
            self.delta.append(
                {
                    "path": path,
                    "status": "A" if added else "M",
                    "old_mode": "000000" if added else "100644",
                    "new_mode": "100644",
                    "old_oid": "0" * 40 if added else "1" * 40,
                    "new_oid": "2" * 40,
                }
            )

    def commit(self, sha: str) -> dict[str, object]:
        if sha != self.base_sha:
            raise AssertionError(f"unexpected commit: {sha}")
        return {"parents": list(self.parents), "tree": "3" * 40}

    def changed_files(self, base: str, head: str) -> list[dict[str, object]]:
        if (base, head) != (tool.ORIGINAL_CONTROL_SQUASH_SHA, self.base_sha):
            raise AssertionError(f"unexpected diff: {base}...{head}")
        return copy.deepcopy(self.delta)

    def blob_at(self, commit: str, path: str) -> bytes:
        try:
            return self.blobs[(commit, path)]
        except KeyError as exc:
            raise AssertionError(f"unexpected blob: {commit}:{path}") from exc

    def _run(self, *args: str) -> bytes:
        if args != ("rev-parse", "--verify", "HEAD"):
            raise AssertionError(f"unexpected Git command: {args}")
        return f"{self.head_sha}\n".encode("ascii")


class WP0002GraphQLTransportFixTests(unittest.TestCase):
    def branch(self) -> dict[str, object]:
        return {"commit": {"sha": "1" * 40}}

    def protection(self) -> dict[str, object]:
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

    def readers(self, *, squash_only: bool = True):
        branch_path = f"/repos/{tool.REPOSITORY}/branches/main"
        protection_path = f"{branch_path}/protection"
        rulesets_path = (
            f"/repos/{tool.REPOSITORY}/rulesets?includes_parents=true"
        )
        ordinary = FakeGitHub({branch_path: self.branch()})
        protected = FakeGitHub(
            {protection_path: self.protection(), rulesets_path: []},
            merge_payload=graphql_payload(squash_only=squash_only),
        )
        return ordinary, protected, branch_path, protection_path, rulesets_path

    def test_v3_is_hash_pinned_and_byte_immutable(self) -> None:
        self.assertEqual(
            hashlib.sha256(V3_PATH.read_bytes()).hexdigest(),
            tool.V3_VERIFIER_SHA256,
        )
        self.assertEqual(
            tool.V3_VERIFIER_SHA256,
            "2d8cd873f0a8ee357848d18e2cb57f440f6fd28fb5817e7454f816d07dc8e52d",
        )
        fresh = tool._load_v3()
        self.assertEqual(fresh.TRANSACTION_ID, tool.TRANSACTION_ID)

    def test_loader_restores_module_entries_and_rejects_drift(self) -> None:
        name = tool._V3_MODULE_NAME
        original = sys.modules.pop(name, tool._MISSING)
        try:
            tool._load_v3()
            self.assertNotIn(name, sys.modules)
            for sentinel in (None, object()):
                sys.modules[name] = sentinel
                tool._load_v3()
                self.assertIs(sys.modules[name], sentinel)
            with tempfile.TemporaryDirectory() as directory:
                drifted = Path(directory) / "v3.py"
                drifted.write_text("raise RuntimeError('not trusted')\n")
                with mock.patch.object(tool, "V3_VERIFIER_PATH", drifted):
                    with mock.patch.object(tool, "V3_VERIFIER_SHA256", "0" * 64):
                        with self.assertRaisesRegex(
                            tool.V4LoaderError,
                            "hash mismatch",
                        ):
                            tool._load_v3()
                self.assertIs(sys.modules[name], sentinel)
        finally:
            if original is tool._MISSING:
                sys.modules.pop(name, None)
            else:
                sys.modules[name] = original

    def test_graphql_transport_is_one_exact_read_only_query(self) -> None:
        response = FakeResponse(graphql_payload(squash_only=True))
        reader = tool.ProtectionGitHubReader("protected-token")
        with mock.patch.object(
            tool.urllib.request,
            "urlopen",
            return_value=response,
        ) as urlopen:
            result = reader.get_repository_merge_methods()
        request = urlopen.call_args.args[0]
        self.assertEqual(request.full_url, "https://api.github.com/graphql")
        self.assertEqual(request.get_method(), "POST")
        self.assertEqual(request.get_header("Authorization"), "Bearer protected-token")
        body = json.loads(request.data.decode("utf-8"))
        self.assertEqual(
            body["variables"],
            {"name": "sasha-the-land-pirate", "owner": "AC-21"},
        )
        self.assertIn("query RepositoryMergeMethods", body["query"])
        self.assertNotIn("mutation", body["query"].casefold())
        for field in (
            "id",
            "nameWithOwner",
            "mergeCommitAllowed",
            "rebaseMergeAllowed",
            "squashMergeAllowed",
        ):
            self.assertEqual(body["query"].count(field), 1)
        self.assertEqual(
            result.data,
            {
                "id": tool.REPOSITORY_GRAPHQL_ID,
                "nameWithOwner": tool.REPOSITORY,
                "allow_merge_commit": False,
                "allow_rebase_merge": False,
                "allow_squash_merge": True,
            },
        )
        self.assertEqual(result.raw, response.raw)

    def test_graphql_projection_fails_closed_without_leaking_payload(self) -> None:
        cases = []
        cases.append(
            (
                {"errors": [{"message": "sensitive server detail"}]},
                "returned errors",
                "sensitive server detail",
            )
        )
        wrong = graphql_payload(squash_only=True)
        wrong["data"]["repository"]["nameWithOwner"] = "AC-21/other"
        cases.append((wrong, "wrong repository", "AC-21/other"))
        wrong_id = graphql_payload(squash_only=True)
        wrong_id["data"]["repository"]["id"] = "R_wrong"
        cases.append((wrong_id, "wrong repository ID", "R_wrong"))
        null_id = graphql_payload(squash_only=True)
        null_id["data"]["repository"]["id"] = None
        cases.append((null_id, "wrong repository ID", "None"))
        non_boolean = graphql_payload(squash_only=True)
        non_boolean["data"]["repository"]["squashMergeAllowed"] = None
        cases.append((non_boolean, "non-boolean field", "None"))
        extra = graphql_payload(squash_only=True)
        extra["data"]["repository"]["unexpected"] = "secret-value"
        cases.append((extra, "keys differ", "secret-value"))
        for payload, expected, forbidden in cases:
            with self.subTest(expected=expected):
                with self.assertRaisesRegex(tool.VerificationError, expected) as raised:
                    tool._repository_merge_method_projection(
                        tool.APIResult(payload, json_bytes(payload), {})
                    )
                self.assertNotIn(forbidden, str(raised.exception))

    def test_capture_uses_rest_only_for_protection_and_rulesets(self) -> None:
        ordinary, protected, branch_path, protection_path, rulesets_path = (
            self.readers()
        )
        capture = tool.capture_protection(
            ordinary,
            protection_github=protected,
        )
        self.assertEqual(ordinary.json_requests, [branch_path])
        self.assertEqual(
            protected.json_requests,
            [protection_path, rulesets_path],
        )
        self.assertNotIn(f"/repos/{tool.REPOSITORY}", protected.json_requests)
        self.assertEqual(protected.graphql_requests, 1)
        self.assertEqual(ordinary.graphql_requests, 0)
        self.assertIs(capture["normalized"]["squash_only"], True)
        self.assertEqual(
            capture["raw"]["repository"],
            {
                "id": tool.REPOSITORY_GRAPHQL_ID,
                "nameWithOwner": tool.REPOSITORY,
                "allow_merge_commit": False,
                "allow_rebase_merge": False,
                "allow_squash_merge": True,
            },
        )

    def test_live_recheck_reports_only_normalized_field_names(self) -> None:
        ordinary, protected, *_paths = self.readers(squash_only=True)
        expected = tool.capture_protection(
            ordinary,
            protection_github=protected,
        )["normalized"]
        ordinary.json_requests.clear()
        ordinary.graphql_requests = 0
        _unused, drifted, *_ = self.readers(squash_only=False)
        with self.assertRaisesRegex(
            tool.VerificationError,
            r"^live protection mismatch fields: squash_only$",
        ) as raised:
            tool._same_live_protection(
                ordinary,
                expected,
                protection_github=drifted,
            )
        rendered = str(raised.exception)
        self.assertNotIn("allow_squash_merge", rendered)
        self.assertNotIn("True", rendered)
        self.assertNotIn("False", rendered)

    def test_caching_reader_caches_rest_and_graphql_independently(self) -> None:
        path = "/repos/AC-21/sasha-the-land-pirate/branches/main/protection"
        delegate = FakeGitHub(
            {path: {"version": 1}},
            merge_payload=graphql_payload(squash_only=True),
        )
        reader = tool._CachingReader(delegate)
        first_rest = reader.get_json(path)
        delegate.documents[path] = {"version": 2}
        second_rest = reader.get_json(path)
        first_graphql = reader.get_repository_merge_methods()
        delegate.merge_payload = graphql_payload(squash_only=False)
        second_graphql = reader.get_repository_merge_methods()
        self.assertIs(first_rest, second_rest)
        self.assertIs(first_graphql, second_graphql)
        self.assertEqual(delegate.json_requests, [path])
        self.assertEqual(delegate.graphql_requests, 1)

    def test_all_live_cli_paths_use_the_dedicated_protection_reader(self) -> None:
        ordinary = object()
        protected = object()
        repository = object()
        command_cases = [
            (
                [
                    "capture-protection",
                    "--output",
                    "/tmp/protection.json",
                ],
                "capture_protection",
                {"normalized": {}},
            ),
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
            (
                [
                    "verify-evidence-closure",
                    "--repository-path",
                    "/tmp/repository",
                    "--base",
                    "1" * 40,
                    "--head",
                    "2" * 40,
                ],
                "verify_evidence_closure",
                {"result": "PASS"},
            ),
        ]
        for argv, callable_name, returned in command_cases:
            with self.subTest(command=argv[0]):
                with mock.patch.object(
                    tool._v1,
                    "_reader",
                    return_value=ordinary,
                ) as ordinary_factory, mock.patch.object(
                    tool,
                    "_protection_reader",
                    return_value=protected,
                ) as protected_factory, mock.patch.object(
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
                            "PROTECTED_TOKEN",
                            *argv,
                        ]
                    )
                self.assertEqual(result, 0)
                ordinary_factory.assert_called_once_with("ORDINARY_TOKEN")
                protected_factory.assert_called_once_with("PROTECTED_TOKEN")
                self.assertIs(
                    invoked.call_args.kwargs["protection_github"],
                    protected,
                )

    def test_transport_patch_restores_v3_globals_after_failure(self) -> None:
        original_capture = tool._v3.capture_protection
        original_same_live = tool._v3._same_live_protection

        def fail():
            self.assertIs(tool._v3.capture_protection, tool.capture_protection)
            self.assertIs(tool._v3._same_live_protection, tool._same_live_protection)
            raise RuntimeError("expected failure")

        with self.assertRaisesRegex(RuntimeError, "expected failure"):
            tool._call_v3_with_v4_transport(fail)
        self.assertIs(tool._v3.capture_protection, original_capture)
        self.assertIs(tool._v3._same_live_protection, original_same_live)

    def test_v4_fix_base_is_one_exact_content_bound_squash(self) -> None:
        base = "b" * 40
        result = tool._validate_v4_fix_base(FakeFixRepository(base), base)
        self.assertEqual(result["base_sha"], base)
        self.assertEqual(
            result["sole_parent_sha"],
            tool.ORIGINAL_CONTROL_SQUASH_SHA,
        )
        self.assertEqual(
            {item["path"] for item in result["delta"]},
            tool.V4_FIX_PATHS,
        )
        self.assertEqual(result["protected_hash_bindings"], "PASS")

    def test_pending_v4_fix_head_accepts_only_an_exact_synthetic_merge(self) -> None:
        candidate = "b" * 40
        synthetic = "c" * 40

        class SyntheticRepository(FakeFixRepository):
            def __init__(self, *, mismatched_tree: bool = False) -> None:
                super().__init__(candidate)
                self.head_sha = synthetic
                self.mismatched_tree = mismatched_tree

            def commit(self, sha: str) -> dict[str, object]:
                if sha == synthetic:
                    return {
                        "parents": [tool.ORIGINAL_CONTROL_SQUASH_SHA, candidate],
                        "tree": "4" * 40 if self.mismatched_tree else "3" * 40,
                    }
                return super().commit(sha)

        repository = SyntheticRepository()
        with mock.patch.object(tool, "GitRepository", SyntheticRepository):
            result = tool.validate_pending_v4_fix_head(repository)
        self.assertEqual(result["base_sha"], candidate)
        self.assertEqual(result["checkout_head_sha"], synthetic)
        self.assertEqual(result["checkout_shape"], "github-synthetic-merge")

        repository = SyntheticRepository(mismatched_tree=True)
        with mock.patch.object(
            tool,
            "GitRepository",
            SyntheticRepository,
        ), self.assertRaisesRegex(
            tool.VerificationError,
            "does not exactly project",
        ):
            tool.validate_pending_v4_fix_head(repository)

    def test_pending_v4_fix_head_reuses_the_exact_content_bound_gate(self) -> None:
        base = "b" * 40
        repository = FakeFixRepository(base)
        with mock.patch.object(tool, "GitRepository", FakeFixRepository):
            result = tool.validate_pending_v4_fix_head(repository)
        self.assertEqual(result["base_sha"], base)
        self.assertEqual(
            result["sole_parent_sha"],
            tool.ORIGINAL_CONTROL_SQUASH_SHA,
        )
        self.assertEqual(result["protected_hash_bindings"], "PASS")
        self.assertEqual(result["checkout_head_sha"], base)
        self.assertEqual(result["checkout_shape"], "direct-child")

    def test_foundation_pending_state_falls_forward_only_to_exact_v4(self) -> None:
        calls: list[tuple[str, object]] = []

        class HistoricalRepository:
            def __init__(self, root: Path) -> None:
                calls.append(("historical-repository", root))

        class ForwardRepository:
            def __init__(self, root: Path) -> None:
                calls.append(("forward-repository", root))

        def reject_historical(repository: object, receipt: dict) -> None:
            calls.append(("historical-validator", repository))
            raise RuntimeError("historical rejection value must not leak")

        def accept_forward(repository: object) -> None:
            calls.append(("forward-validator", repository))

        receipt = {
            "receipt_id": foundation.WP0002_LOCAL_OPERATOR_RECOVERY_RECEIPT_ID,
        }
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with mock.patch.object(
                foundation,
                "REPO_ROOT",
                root,
            ), mock.patch.object(
                foundation,
                "_load_wp0002_transaction_verifier",
                return_value=(
                    {
                        "GitRepository": HistoricalRepository,
                        "validate_pending_receipt_child": reject_historical,
                    },
                    [],
                ),
            ), mock.patch.object(
                foundation,
                "_load_wp0002_forward_verifier",
                return_value=(
                    {
                        "GitRepository": ForwardRepository,
                        "validate_pending_v4_fix_head": accept_forward,
                    },
                    [],
                ),
            ):
                errors = foundation.validate_wp0002_local_operator_transaction_evidence(
                    receipt
                )
        self.assertEqual(errors, [])
        self.assertEqual(
            [name for name, _value in calls],
            [
                "historical-repository",
                "historical-validator",
                "forward-repository",
                "forward-validator",
            ],
        )

    def test_foundation_pending_state_fails_closed_without_leaking_values(self) -> None:
        secret = "must-not-leak"

        class Repository:
            def __init__(self, _root: Path) -> None:
                pass

        def reject(*_args: object) -> None:
            raise RuntimeError(secret)

        receipt = {
            "receipt_id": foundation.WP0002_LOCAL_OPERATOR_RECOVERY_RECEIPT_ID,
        }
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            verifier = {
                "GitRepository": Repository,
                "validate_pending_receipt_child": reject,
            }
            forward = {
                "GitRepository": Repository,
                "validate_pending_v4_fix_head": reject,
            }
            with mock.patch.object(
                foundation,
                "REPO_ROOT",
                root,
            ), mock.patch.object(
                foundation,
                "_load_wp0002_transaction_verifier",
                return_value=(verifier, []),
            ), mock.patch.object(
                foundation,
                "_load_wp0002_forward_verifier",
                return_value=(forward, []),
            ):
                errors = foundation.validate_wp0002_local_operator_transaction_evidence(
                    receipt
                )
        self.assertEqual(
            errors,
            [
                "WP-0002 pending state is neither the exact receipt-only child "
                "nor the exact forward verifier-fix child"
            ],
        )
        self.assertNotIn(secret, "\n".join(errors))

    def test_v4_fix_base_fails_closed_on_parent_path_mode_or_hash_drift(self) -> None:
        base = "b" * 40

        def wrong_parent(repository: FakeFixRepository) -> None:
            repository.parents = ["c" * 40]

        def extra_path(repository: FakeFixRepository) -> None:
            repository.delta.append(
                {
                    "path": "unexpected.txt",
                    "status": "A",
                    "old_mode": "000000",
                    "new_mode": "100644",
                    "old_oid": "0" * 40,
                    "new_oid": "2" * 40,
                }
            )

        def wrong_mode(repository: FakeFixRepository) -> None:
            repository.delta[0]["new_mode"] = "120000"

        def v3_drift(repository: FakeFixRepository) -> None:
            path = "Tools/Validation/verify_wp0002_local_operator_transaction_v3.py"
            repository.blobs[(base, path)] = b"drift"

        def executing_v4_drift(repository: FakeFixRepository) -> None:
            repository.blobs[(base, tool.V4_VERIFIER_PATH)] = b"drift"

        def manifest_drift(repository: FakeFixRepository) -> None:
            key = (base, tool.V4_MANIFEST_PATH)
            current = hashlib.sha256(
                repository.blobs[(base, tool.V4_TEST_PATH)]
            ).hexdigest().encode("ascii")
            repository.blobs[key] = repository.blobs[key].replace(
                current,
                b"0" * 64,
                1,
            )

        def workflow_drift(repository: FakeFixRepository) -> None:
            workflow_key = (base, tool.V4_WORKFLOW_PATH)
            old_hash = hashlib.sha256(repository.blobs[workflow_key]).hexdigest()
            repository.blobs[workflow_key] = repository.blobs[workflow_key].replace(
                b'python3 -B "$verifier"',
                b'python3 -B "$other"',
                1,
            )
            new_hash = hashlib.sha256(repository.blobs[workflow_key]).hexdigest()
            manifest_key = (base, tool.V4_MANIFEST_PATH)
            repository.blobs[manifest_key] = repository.blobs[manifest_key].replace(
                old_hash.encode("ascii"),
                new_hash.encode("ascii"),
                1,
            )

        cases = (
            (wrong_parent, "sole-child verifier fix"),
            (extra_path, "exact four-file contract"),
            (wrong_mode, "not a regular file"),
            (v3_drift, "immutable v3 verifier"),
            (executing_v4_drift, "executing protected verifier"),
            (manifest_drift, "self-verification hashes"),
            (workflow_drift, "unique verifier hash pin"),
        )
        for mutate, expected in cases:
            with self.subTest(expected=expected):
                repository = FakeFixRepository(base)
                mutate(repository)
                with self.assertRaisesRegex(tool.VerificationError, expected):
                    tool._validate_v4_fix_base(repository, base)

    def test_closure_bridges_v4_main_but_anchors_reports_to_control_squash(self) -> None:
        base = "b" * 40
        head = "d" * 40
        pre_pull = {
            "number": 65,
            "base_ref": "main",
            "base_sha": "1" * 40,
            "head_ref": "agent/recovery",
            "head_sha": "2" * 40,
            "head_repository": tool.REPOSITORY,
        }
        repository_identity = {
            "full_name": tool.REPOSITORY,
            "id": 9,
            "owner_login": "AC-21",
            "owner_id": 7,
        }
        completion_binding = {
            "authority_evidence_sha256": "a" * 64,
            "pre_merge_evidence_sha256": "b" * 64,
        }
        branch_path = f"/repos/{tool.REPOSITORY}/branches/main"
        protection_path = f"{branch_path}/protection"
        rulesets_path = (
            f"/repos/{tool.REPOSITORY}/rulesets?includes_parents=true"
        )
        repository_path = f"/repos/{tool.REPOSITORY}"
        pull_path = f"/repos/{tool.REPOSITORY}/pulls/65"
        comment_path = f"/repos/{tool.REPOSITORY}/issues/comments/73"
        checks_path = (
            f"/repos/{tool.REPOSITORY}/commits/{pre_pull['head_sha']}"
            "/check-runs?per_page=100"
        )
        ordinary = FakeGitHub(
            {
                repository_path: {},
                pull_path: {
                    "merged": True,
                    "merged_at": "2026-07-18T12:00:00Z",
                    "merge_commit_sha": tool.ORIGINAL_CONTROL_SQUASH_SHA,
                },
                branch_path: {"commit": {"sha": base}},
                comment_path: {},
                checks_path: {},
            }
        )
        protected = FakeGitHub(
            {protection_path: self.protection(), rulesets_path: []},
            merge_payload=graphql_payload(squash_only=True),
        )
        live_capture = tool.capture_protection(
            ordinary,
            protection_github=protected,
        )
        expected_protection = copy.deepcopy(live_capture["normalized"])
        expected_protection["main_sha"] = tool.ORIGINAL_CONTROL_SQUASH_SHA
        ordinary.json_requests.clear()
        protected.json_requests.clear()
        protected.graphql_requests = 0
        authority = {
            "repository": repository_identity,
            "stage1": {"commit_sha": "3" * 40},
        }
        pre_merge = {
            "final_pull_request": pre_pull,
            "required_check_runs": [],
        }
        complete = {
            "merged_pull_request": {
                "merged_at": "2026-07-18T12:00:00Z",
                "merge_commit_sha": tool.ORIGINAL_CONTROL_SQUASH_SHA,
            },
            "completion_comment": {"id": 73},
            "parsed_completion_binding": completion_binding,
            "protection_after": expected_protection,
        }
        reports = {
            tool.AUTHORITY_EVIDENCE_PATH: authority,
            tool.PRE_MERGE_EVIDENCE_PATH: pre_merge,
            tool.COMPLETE_EVIDENCE_PATH: complete,
        }

        class ClosureRepository:
            def commit(self, sha: str) -> dict[str, object]:
                self.last_commit = sha
                return {"parents": [base], "tree": "4" * 40}

            def contains(self, ancestor: str, descendant: str) -> bool:
                return (ancestor, descendant) == (base, head)

            def changed_files(self, ancestor: str, descendant: str):
                self.last_diff = (ancestor, descendant)
                return [
                    {
                        "path": path,
                        "status": "A",
                        "old_mode": "000000",
                        "new_mode": "100644",
                        "old_oid": "0" * 40,
                    }
                    for path in sorted(tool.CLOSURE_EVIDENCE_PATHS)
                ]

        repository = ClosureRepository()

        def report_at(_repository, _head, path):
            return reports[path], path.encode("utf-8")

        chain = {
            "authority": "a" * 64,
            "pre_merge": "b" * 64,
            "complete": "c" * 64,
            "control_final_head": pre_pull["head_sha"],
            "control_squash": tool.ORIGINAL_CONTROL_SQUASH_SHA,
        }
        with mock.patch.object(
            tool,
            "GitRepository",
            ClosureRepository,
        ), mock.patch.object(
            tool,
            "_validate_v4_fix_base",
            return_value={"base_sha": base, "protected_hash_bindings": "PASS"},
        ), mock.patch.object(
            tool._v1,
            "_json_blob_at",
            side_effect=report_at,
        ), mock.patch.object(
            tool._v1,
            "_validate_closure_report_chain",
            return_value=chain,
        ) as report_chain, mock.patch.object(
            tool,
            "validate_historical_git_objects",
        ), mock.patch.object(
            tool._v1,
            "_repository_projection",
            return_value=repository_identity,
        ), mock.patch.object(
            tool._v1,
            "_live_comment_again",
            return_value=(
                tool.APIResult({}, b"authority", {}),
                {},
                {"claim": "authority"},
            ),
        ), mock.patch.object(
            tool._v1,
            "_pull_projection",
            return_value=pre_pull,
        ), mock.patch.object(
            tool._v1,
            "_comment_projection",
            return_value=({"id": 73}, b"completion", completion_binding),
        ), mock.patch.object(
            tool._v1,
            "_check_runs",
            return_value=[],
        ):
            actual = tool.verify_evidence_closure(
                repository,
                ordinary,
                base_sha=base,
                head_sha=head,
                protection_github=protected,
            )
        self.assertEqual(actual["closure"]["base_sha"], base)
        self.assertEqual(
            actual["historical_git_object_validation"]["control_merge_sha"],
            tool.ORIGINAL_CONTROL_SQUASH_SHA,
        )
        self.assertEqual(actual["live_protection_after"]["main_sha"], base)
        self.assertEqual(
            actual["mixed_reader_recovery"]["administration_reader_fields"],
            [
                "branch_protection_rest",
                "repository_merge_settings_graphql_query",
                "rulesets_rest",
            ],
        )
        self.assertEqual(
            report_chain.call_args.kwargs["closure_base"],
            tool.ORIGINAL_CONTROL_SQUASH_SHA,
        )
        self.assertEqual(
            actual["mixed_reader_recovery"]["administration_reader_fields"],
            [
                "branch_protection_rest",
                "repository_merge_settings_graphql_query",
                "rulesets_rest",
            ],
        )

    def test_protected_reader_factory_requires_the_exact_env(self) -> None:
        with mock.patch.dict(tool.os.environ, {}, clear=True):
            with self.assertRaisesRegex(
                tool.VerificationError,
                "required GitHub credential environment variable is absent: EXACT_TOKEN",
            ):
                tool._protection_reader("EXACT_TOKEN")
        with mock.patch.dict(
            tool.os.environ,
            {"EXACT_TOKEN": "opaque-value"},
            clear=True,
        ):
            reader = tool._protection_reader("EXACT_TOKEN")
        self.assertIsInstance(reader, tool.ProtectionGitHubReader)

    def test_workflow_hash_pins_v4_and_protected_manifest_retains_v3(self) -> None:
        workflow_path = ROOT / ".github" / "workflows" / "wp0002-policy.yml"
        workflow = workflow_path.read_text(encoding="utf-8")
        actual = hashlib.sha256(TOOL_PATH.read_bytes()).hexdigest()
        self.assertIn(
            'verifier="Tools/Validation/verify_wp0002_local_operator_transaction_v4.py"',
            workflow,
        )
        self.assertIn(f'expected_verifier_sha256="{actual}"', workflow)
        foundation = (
            ROOT / "docs" / "foundation-v0.1" / "tools" / "validate_foundation.py"
        ).read_text(encoding="utf-8")
        self.assertIn(tool.V3_VERIFIER_SHA256, foundation)
        self.assertIn(actual, foundation)
        self.assertIn(
            "test_verify_wp0002_local_operator_transaction_v4.py",
            foundation,
        )


if __name__ == "__main__":
    unittest.main()
