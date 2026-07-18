#!/usr/bin/env python3
"""Forward-only GraphQL transport fix for the WP-0002 closure verifier.

V1, v2, and v3 remain immutable historical artifacts. This adapter hash-pins
and loads v3, then changes only the live repository merge-method projection:

* the ordinary read-only reader still supplies branch identity, repository
  identity, pull requests, comments, check runs, and Git fetch identity;
* the single-repository, short-expiry Administration-read reader still supplies
  classic branch protection and rulesets through REST; and
* that same protected reader supplies the three repository merge-method
  booleans through one exact read-only GraphQL query.

The GraphQL response is reduced to v3's existing REST-shaped three-boolean
evidence projection, so historical reports and schemas remain unchanged. The
raw GraphQL response is hash-bound. Candidate code never receives the protected
credential. Diagnostics contain normalized field names only, never values,
headers, tokens, or raw API bodies.
"""

from __future__ import annotations

import ast
import hashlib
import json
import os
import sys
import types
import urllib.error
import urllib.request
from datetime import datetime
from pathlib import Path
from typing import Mapping


V3_VERIFIER_SHA256 = (
    "2d8cd873f0a8ee357848d18e2cb57f440f6fd28fb5817e7454f816d07dc8e52d"
)
V3_VERIFIER_PATH = Path(__file__).with_name(
    "verify_wp0002_local_operator_transaction_v3.py"
)
_V3_MODULE_NAME = "_wp0002_local_operator_transaction_v3_pinned_for_v4"
_MISSING = object()


class V4LoaderError(RuntimeError):
    """Raised when the immutable v3 verifier cannot be loaded exactly."""


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _load_v3() -> types.ModuleType:
    try:
        source = V3_VERIFIER_PATH.read_bytes()
    except OSError as exc:
        raise V4LoaderError(f"v3 verifier cannot be read: {exc}") from exc
    if _sha256(source) != V3_VERIFIER_SHA256:
        raise V4LoaderError("v3 verifier hash mismatch")
    previous = sys.modules.get(_V3_MODULE_NAME, _MISSING)
    module = types.ModuleType(_V3_MODULE_NAME)
    module.__file__ = str(V3_VERIFIER_PATH)
    module.__package__ = ""
    sys.modules[_V3_MODULE_NAME] = module
    try:
        exec(compile(source, str(V3_VERIFIER_PATH), "exec"), module.__dict__)
    except Exception as exc:
        raise V4LoaderError(f"v3 verifier cannot load: {exc}") from exc
    finally:
        if previous is _MISSING:
            sys.modules.pop(_V3_MODULE_NAME, None)
        else:
            sys.modules[_V3_MODULE_NAME] = previous  # type: ignore[assignment]
    return module


_v3 = _load_v3()
_v1 = _v3._v1
VerificationError = _v3.VerificationError
GitRepository = _v3.GitRepository
GitHubReader = _v3.GitHubReader
LiveGitHubReader = _v3.LiveGitHubReader
APIResult = _v3.APIResult

REPOSITORY = _v3.REPOSITORY
REPOSITORY_GRAPHQL_ID = "R_kgDOTZ3pzA"
TRANSACTION_ID = _v3.TRANSACTION_ID
ORIGINAL_CONTROL_SQUASH_SHA = "1763c20bb07398f81ef82cecb908299946283e3e"
AUTHORITY_EVIDENCE_PATH = _v3.AUTHORITY_EVIDENCE_PATH
PRE_MERGE_EVIDENCE_PATH = _v3.PRE_MERGE_EVIDENCE_PATH
COMPLETE_EVIDENCE_PATH = _v3.COMPLETE_EVIDENCE_PATH
CLOSURE_EVIDENCE_PATHS = _v3.CLOSURE_EVIDENCE_PATHS
V4_VERIFIER_PATH = (
    "Tools/Validation/verify_wp0002_local_operator_transaction_v4.py"
)
V4_TEST_PATH = (
    "docs/foundation-v0.1/tools/"
    "test_verify_wp0002_local_operator_transaction_v4.py"
)
V4_WORKFLOW_PATH = ".github/workflows/wp0002-policy.yml"
V4_MANIFEST_PATH = "docs/foundation-v0.1/tools/validate_foundation.py"
V4_FIX_PATHS = frozenset(
    {V4_VERIFIER_PATH, V4_TEST_PATH, V4_WORKFLOW_PATH, V4_MANIFEST_PATH}
)

render_authorization_body = _v3.render_authorization_body
build_authority_evidence = _v3.build_authority_evidence
validate_authority_evidence = _v3.validate_authority_evidence
validate_pre_merge_evidence = _v3.validate_pre_merge_evidence
validate_complete_evidence = _v3.validate_complete_evidence
validate_historical_git_objects = _v3.validate_historical_git_objects
sanitized_mismatch_fields = _v3.sanitized_mismatch_fields

_GRAPHQL_REPOSITORY_MERGE_METHODS_QUERY = """\
query RepositoryMergeMethods($owner: String!, $name: String!) {
  repository(owner: $owner, name: $name) {
    id
    nameWithOwner
    mergeCommitAllowed
    rebaseMergeAllowed
    squashMergeAllowed
  }
}
"""


def _repository_merge_method_projection(response: APIResult) -> APIResult:
    """Normalize the exact GraphQL response to v3's evidence shape."""

    payload = _v1._dict(response.data, "GraphQL merge-method response")
    if payload.get("errors") is not None:
        raise VerificationError(
            "GitHub GraphQL repository merge-method query returned errors"
        )
    _v1._exact_keys(payload, {"data"}, "GraphQL merge-method response")
    data = _v1._dict(payload.get("data"), "GraphQL merge-method data")
    _v1._exact_keys(data, {"repository"}, "GraphQL merge-method data")
    repository = _v1._dict(
        data.get("repository"),
        "GraphQL merge-method repository",
    )
    _v1._exact_keys(
        repository,
        {
            "id",
            "nameWithOwner",
            "mergeCommitAllowed",
            "rebaseMergeAllowed",
            "squashMergeAllowed",
        },
        "GraphQL merge-method repository",
    )
    if repository.get("nameWithOwner") != REPOSITORY:
        raise VerificationError(
            "GitHub GraphQL merge-method response names the wrong repository"
        )
    if repository.get("id") != REPOSITORY_GRAPHQL_ID:
        raise VerificationError(
            "GitHub GraphQL merge-method response has the wrong repository ID"
        )
    projection = {
        "id": repository.get("id"),
        "nameWithOwner": repository.get("nameWithOwner"),
        "allow_merge_commit": repository.get("mergeCommitAllowed"),
        "allow_rebase_merge": repository.get("rebaseMergeAllowed"),
        "allow_squash_merge": repository.get("squashMergeAllowed"),
    }
    if any(
        type(projection[key]) is not bool
        for key in (
            "allow_merge_commit",
            "allow_rebase_merge",
            "allow_squash_merge",
        )
    ):
        raise VerificationError(
            "GitHub GraphQL merge-method response contains a non-boolean field"
        )
    return APIResult(projection, response.raw, response.headers)


class ProtectionGitHubReader(LiveGitHubReader):
    """REST protection reader with one dedicated read-only GraphQL query."""

    def get_repository_merge_methods(self) -> APIResult:
        if not self._token:
            raise VerificationError(
                "v4 GraphQL merge-method query requires the protected credential"
            )
        owner, name = REPOSITORY.split("/", 1)
        body = _v1.canonical_json_bytes(
            {
                "query": _GRAPHQL_REPOSITORY_MERGE_METHODS_QUERY,
                "variables": {"name": name, "owner": owner},
            }
        )
        headers = {
            "Accept": "application/vnd.github+json",
            "Content-Type": "application/json",
            "User-Agent": "wp0002-local-operator-transaction-verifier/4",
            "X-GitHub-Api-Version": "2022-11-28",
            "Authorization": f"Bearer {self._token}",
        }
        request = urllib.request.Request(
            _v1.GITHUB_API + "/graphql",
            data=body,
            method="POST",
            headers=headers,
        )
        try:
            with urllib.request.urlopen(
                request,
                timeout=self._timeout,
            ) as response:
                raw = response.read()
                response_headers = {
                    key.lower(): value for key, value in response.headers.items()
                }
        except (urllib.error.URLError, TimeoutError, OSError) as exc:
            raise VerificationError(
                f"GitHub GraphQL repository merge-method query failed: {exc}"
            ) from exc
        try:
            data = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise VerificationError(
                "GitHub GraphQL repository merge-method query returned invalid JSON"
            ) from exc
        return _repository_merge_method_projection(
            APIResult(data, raw, response_headers)
        )


def _protection_reader(token_env: str) -> ProtectionGitHubReader:
    token = os.environ.get(token_env)
    if not token:
        raise VerificationError(
            "required GitHub credential environment variable is absent: "
            f"{token_env}"
        )
    return ProtectionGitHubReader(token)


def _protected_self_verification_manifest(source: bytes) -> dict[str, str]:
    """Read only the literal protected-hash map; never execute candidate code."""

    try:
        module = ast.parse(source.decode("utf-8"), filename=V4_MANIFEST_PATH)
    except (UnicodeDecodeError, SyntaxError) as exc:
        raise VerificationError(
            "v4 protected self-verification manifest is not valid Python source"
        ) from exc
    for statement in module.body:
        if not isinstance(statement, ast.Assign) or len(statement.targets) != 1:
            continue
        target = statement.targets[0]
        if (
            isinstance(target, ast.Name)
            and target.id == "WP0002_PROTECTED_SELF_VERIFICATION"
        ):
            try:
                value = ast.literal_eval(statement.value)
            except (ValueError, TypeError, SyntaxError) as exc:
                raise VerificationError(
                    "v4 protected self-verification manifest is not literal"
                ) from exc
            if not isinstance(value, dict) or any(
                not isinstance(key, str) or not isinstance(item, str)
                for key, item in value.items()
            ):
                raise VerificationError(
                    "v4 protected self-verification manifest has invalid entries"
                )
            return value
    raise VerificationError(
        "v4 protected self-verification manifest is absent"
    )


def _require_workflow_hash_pins(
    source: bytes,
    *,
    v4_sha256: str,
) -> None:
    try:
        workflow = source.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise VerificationError("v4 policy workflow is not UTF-8") from exc
    ordered_markers = (
        'verifier="Tools/Validation/verify_wp0002_local_operator_transaction_v3.py"',
        f'expected_verifier_sha256="{V3_VERIFIER_SHA256}"',
        f'verifier="{V4_VERIFIER_PATH}"',
        f'expected_verifier_sha256="{v4_sha256}"',
        'env PYTHONDONTWRITEBYTECODE=1 python3 -B "$verifier"',
    )
    positions: list[int] = []
    for marker in ordered_markers:
        if workflow.count(marker) != 1:
            raise VerificationError(
                "v4 policy workflow lacks an exact unique verifier hash pin"
            )
        positions.append(workflow.index(marker))
    if positions != sorted(positions):
        raise VerificationError(
            "v4 policy workflow does not execute after the ordered v3/v4 pins"
        )


def _validate_v4_fix_base(
    repository: GitRepository,
    base_sha: str,
) -> dict[str, object]:
    """Require exactly one protected, content-bound verifier-fix squash."""

    base_sha = _v1._git_sha(base_sha, "v4 closure base")
    base_commit = repository.commit(base_sha)
    if base_commit.get("parents") != [ORIGINAL_CONTROL_SQUASH_SHA]:
        raise VerificationError(
            "v4 closure base is not the sole-child verifier fix of the control squash"
        )
    delta = repository.changed_files(ORIGINAL_CONTROL_SQUASH_SHA, base_sha)
    if (
        len(delta) != len(V4_FIX_PATHS)
        or {str(item.get("path")) for item in delta} != V4_FIX_PATHS
    ):
        raise VerificationError(
            "v4 verifier-fix squash changes paths outside the exact four-file contract"
        )
    for item in delta:
        path = str(item.get("path"))
        if item.get("new_mode") != "100644":
            raise VerificationError(
                f"v4 verifier-fix path is not a regular file: {path}"
            )
        if path in {V4_VERIFIER_PATH, V4_TEST_PATH}:
            exact = (
                item.get("status") == "A"
                and item.get("old_mode") == "000000"
                and item.get("old_oid") == "0" * 40
            )
        else:
            exact = (
                item.get("status") == "M"
                and item.get("old_mode") == "100644"
                and item.get("old_oid") != "0" * 40
            )
        if not exact:
            raise VerificationError(
                f"v4 verifier-fix path has the wrong delta shape: {path}"
            )

    artifacts = {
        path: _sha256(repository.blob_at(base_sha, path))
        for path in sorted(V4_FIX_PATHS)
    }
    running_v4_sha256 = _sha256(Path(__file__).read_bytes())
    if artifacts[V4_VERIFIER_PATH] != running_v4_sha256:
        raise VerificationError(
            "v4 closure base verifier differs from the executing protected verifier"
        )
    for commit_sha in (ORIGINAL_CONTROL_SQUASH_SHA, base_sha):
        if (
            _sha256(
                repository.blob_at(
                    commit_sha,
                    "Tools/Validation/verify_wp0002_local_operator_transaction_v3.py",
                )
            )
            != V3_VERIFIER_SHA256
        ):
            raise VerificationError(
                "v4 verifier fix does not retain the immutable v3 verifier"
            )

    manifest = _protected_self_verification_manifest(
        repository.blob_at(base_sha, V4_MANIFEST_PATH)
    )
    required_manifest_hashes = {
        "Tools/Validation/verify_wp0002_local_operator_transaction_v3.py": (
            V3_VERIFIER_SHA256
        ),
        V4_VERIFIER_PATH: artifacts[V4_VERIFIER_PATH],
        V4_TEST_PATH: artifacts[V4_TEST_PATH],
        V4_WORKFLOW_PATH: artifacts[V4_WORKFLOW_PATH],
    }
    if any(
        manifest.get(path) != expected
        for path, expected in required_manifest_hashes.items()
    ):
        raise VerificationError(
            "v4 protected self-verification hashes do not bind the fix artifacts"
        )
    _require_workflow_hash_pins(
        repository.blob_at(base_sha, V4_WORKFLOW_PATH),
        v4_sha256=artifacts[V4_VERIFIER_PATH],
    )
    return {
        "base_sha": base_sha,
        "sole_parent_sha": ORIGINAL_CONTROL_SQUASH_SHA,
        "delta": delta,
        "artifact_sha256": artifacts,
        "protected_hash_bindings": "PASS",
    }


def validate_pending_v4_fix_head(
    repository: GitRepository,
) -> dict[str, object]:
    """Validate direct-main or GitHub synthetic-merge pending v4 HEAD."""

    if not isinstance(repository, GitRepository):
        raise VerificationError(
            "pending v4 verifier fix requires the pinned GitRepository"
        )
    checkout_head_sha = _v1._git_sha(
        repository._run("rev-parse", "--verify", "HEAD").decode(
            "ascii"
        ).strip(),
        "pending v4 verifier-fix HEAD",
    )
    checkout = repository.commit(checkout_head_sha)
    parents = checkout.get("parents")
    if parents == [ORIGINAL_CONTROL_SQUASH_SHA]:
        fix_sha = checkout_head_sha
        checkout_shape = "direct-child"
    elif (
        isinstance(parents, list)
        and len(parents) == 2
        and parents[0] == ORIGINAL_CONTROL_SQUASH_SHA
    ):
        fix_sha = _v1._git_sha(
            parents[1],
            "pending v4 synthetic-merge candidate",
        )
        candidate = repository.commit(fix_sha)
        if (
            candidate.get("parents") != [ORIGINAL_CONTROL_SQUASH_SHA]
            or checkout.get("tree") != candidate.get("tree")
        ):
            raise VerificationError(
                "pending v4 synthetic merge does not exactly project the "
                "direct-child verifier fix"
            )
        checkout_shape = "github-synthetic-merge"
    else:
        raise VerificationError(
            "pending v4 verifier-fix HEAD has the wrong parent shape"
        )
    result = _validate_v4_fix_base(repository, fix_sha)
    return {
        **result,
        "checkout_head_sha": checkout_head_sha,
        "checkout_shape": checkout_shape,
    }


class _CachingReader:
    """Keep one internally consistent API observation per reader and route."""

    def __init__(self, delegate: GitHubReader) -> None:
        self._delegate = delegate
        self._json: dict[str, APIResult] = {}
        self._bytes: dict[tuple[str, str], APIResult] = {}
        self._repository_merge_methods: APIResult | None = None

    def get_json(self, path: str) -> APIResult:
        if path not in self._json:
            self._json[path] = self._delegate.get_json(path)
        return self._json[path]

    def get_bytes(self, path: str, *, accept: str) -> APIResult:
        key = (path, accept)
        if key not in self._bytes:
            self._bytes[key] = self._delegate.get_bytes(path, accept=accept)
        return self._bytes[key]

    def get_repository_merge_methods(self) -> APIResult:
        if self._repository_merge_methods is None:
            query = getattr(
                self._delegate,
                "get_repository_merge_methods",
                None,
            )
            if not callable(query):
                raise VerificationError(
                    "v4 protection reader lacks the read-only GraphQL "
                    "merge-method query"
                )
            self._repository_merge_methods = query()
        return self._repository_merge_methods


def capture_protection(
    github: GitHubReader,
    *,
    observed_at: datetime | None = None,
    protection_github: GitHubReader | None = None,
) -> dict[str, object]:
    """Capture v3 protection semantics with an exact REST/GraphQL split."""

    if protection_github is None:
        raise VerificationError(
            "v4 protection capture requires the Administration-read reader"
        )
    merge_method_query = getattr(
        protection_github,
        "get_repository_merge_methods",
        None,
    )
    if not callable(merge_method_query):
        raise VerificationError(
            "v4 protection reader lacks the read-only GraphQL merge-method query"
        )
    branch = github.get_json(f"/repos/{REPOSITORY}/branches/main")
    protection = protection_github.get_json(
        f"/repos/{REPOSITORY}/branches/main/protection"
    )
    repository = merge_method_query()
    rulesets = protection_github.get_json(
        f"/repos/{REPOSITORY}/rulesets?includes_parents=true"
    )
    raw = {
        "branch": branch.data,
        "protection": protection.data,
        "repository": repository.data,
        "rulesets": rulesets.data,
        "raw_response_sha256": {
            "branch": _sha256(branch.raw),
            "protection": _sha256(protection.raw),
            "repository": _sha256(repository.raw),
            "rulesets": _sha256(rulesets.raw),
        },
    }
    return {
        "schema_version": 1,
        "kind": "wp0002-local-operator-protection-capture",
        "normalized": _v1._normalize_protection(
            raw,
            observed_at=_v1._timestamp(observed_at),
        ),
        "raw": raw,
    }


def _same_live_protection(
    github: GitHubReader,
    expected: Mapping[str, object],
    *,
    protection_github: GitHubReader | None,
    now: datetime | None = None,
) -> None:
    """Recheck protection with v4 transport and v3 value-safe diagnostics."""

    if protection_github is None:
        raise VerificationError(
            "v4 live protection recheck requires the Administration-read reader"
        )
    live_capture = capture_protection(
        github,
        observed_at=now,
        protection_github=protection_github,
    )
    live, _raw = _v1._validate_protection_capture(live_capture)
    mismatch_fields = sanitized_mismatch_fields(live, expected)
    if mismatch_fields:
        raise VerificationError(
            "live protection mismatch fields: " + ",".join(mismatch_fields)
        )


def _call_v3_with_v4_transport(function: object, *args: object, **kwargs: object):
    original_capture = _v3.capture_protection
    original_same_live = _v3._same_live_protection
    _v3.capture_protection = capture_protection
    _v3._same_live_protection = _same_live_protection
    try:
        return function(*args, **kwargs)  # type: ignore[operator]
    finally:
        _v3.capture_protection = original_capture
        _v3._same_live_protection = original_same_live


def build_pre_merge_evidence(*args: object, **kwargs: object) -> dict[str, object]:
    return _call_v3_with_v4_transport(
        _v3.build_pre_merge_evidence,
        *args,
        **kwargs,
    )


def render_completion_body(*args: object, **kwargs: object) -> str:
    return _call_v3_with_v4_transport(
        _v3.render_completion_body,
        *args,
        **kwargs,
    )


def build_complete_evidence(*args: object, **kwargs: object) -> dict[str, object]:
    return _call_v3_with_v4_transport(
        _v3.build_complete_evidence,
        *args,
        **kwargs,
    )


def verify_evidence_closure(
    repository: GitRepository,
    github: GitHubReader,
    *,
    base_sha: str,
    head_sha: str,
    protection_github: GitHubReader,
    now: datetime | None = None,
) -> dict[str, object]:
    """Bridge one exact v4 fix squash while retaining the v3 report anchor."""

    if not isinstance(repository, GitRepository):
        raise VerificationError("v4 closure requires the pinned GitRepository")
    if github is None or protection_github is None:
        raise VerificationError("v4 closure requires both trusted GitHub readers")
    if now is not None and not isinstance(now, datetime):
        raise VerificationError("v4 closure time is invalid")
    base_sha = _v1._git_sha(base_sha, "v4 closure base")
    head_sha = _v1._git_sha(head_sha, "v4 closure head")
    v4_fix = _validate_v4_fix_base(repository, base_sha)
    repository.commit(head_sha)
    if not repository.contains(base_sha, head_sha):
        raise VerificationError("v4 closure base is not an ancestor of candidate head")
    delta = repository.changed_files(base_sha, head_sha)
    expected_paths = sorted(CLOSURE_EVIDENCE_PATHS)
    if (
        [item.get("path") for item in delta] != expected_paths
        or len(delta) != 3
        or any(
            item.get("status") != "A"
            or item.get("old_mode") != "000000"
            or item.get("new_mode") != "100644"
            or item.get("old_oid") != "0" * 40
            for item in delta
        )
    ):
        raise VerificationError(
            "v4 closure base...head delta must add exactly the three regular report files"
        )

    reports: dict[str, dict[str, object]] = {}
    report_blobs: dict[str, bytes] = {}
    for path in CLOSURE_EVIDENCE_PATHS:
        reports[path], report_blobs[path] = _v1._json_blob_at(
            repository,
            head_sha,
            path,
        )
    authority = reports[AUTHORITY_EVIDENCE_PATH]
    pre_merge = reports[PRE_MERGE_EVIDENCE_PATH]
    complete = reports[COMPLETE_EVIDENCE_PATH]
    chain = _v1._validate_closure_report_chain(
        repository,
        authority,
        pre_merge,
        complete,
        closure_base=ORIGINAL_CONTROL_SQUASH_SHA,
    )
    validate_historical_git_objects(
        repository,
        authority,
        pre_merge,
        complete,
    )

    ordinary = _CachingReader(github)
    protected = _CachingReader(protection_github)
    repository_response = ordinary.get_json(f"/repos/{REPOSITORY}")
    live_repository = _v1._repository_projection(repository_response.data)
    if live_repository != authority["repository"]:
        raise VerificationError("live repository differs from closure reports")
    authority_response, _authority_projection, authority_binding_live = (
        _v1._live_comment_again(ordinary, authority)
    )

    pre_pull = _v1._dict(
        pre_merge["final_pull_request"],
        "pre-merge final pull request",
    )
    pull_number = int(pre_pull["number"])
    pull_response = ordinary.get_json(f"/repos/{REPOSITORY}/pulls/{pull_number}")
    pull_payload = _v1._dict(
        pull_response.data,
        "live control pull request",
    )
    live_pull = _v1._pull_projection(
        pull_payload,
        pull_number,
        authority=False,
    )
    if any(
        live_pull.get(key) != pre_pull.get(key)
        for key in (
            "base_ref",
            "base_sha",
            "head_ref",
            "head_sha",
            "head_repository",
        )
    ):
        raise VerificationError("live control pull request differs from closure reports")
    merged = _v1._dict(
        complete["merged_pull_request"],
        "complete merged pull request",
    )
    if (
        pull_payload.get("merged") is not True
        or pull_payload.get("merge_commit_sha") != ORIGINAL_CONTROL_SQUASH_SHA
        or pull_payload.get("merged_at") != merged.get("merged_at")
        or merged.get("merge_commit_sha") != ORIGINAL_CONTROL_SQUASH_SHA
    ):
        raise VerificationError(
            "live control pull request is not the original verified squash"
        )

    main_response = ordinary.get_json(f"/repos/{REPOSITORY}/branches/main")
    main = _v1._dict(main_response.data, "live main branch")
    live_main_sha = _v1._git_sha(
        _v1._dict(main.get("commit"), "live main commit").get("sha"),
        "live main SHA",
    )
    if live_main_sha != base_sha:
        raise VerificationError("live main is not the exact v4 verifier-fix base")

    completion_comment = _v1._dict(
        complete["completion_comment"],
        "complete completion comment",
    )
    completion_response = ordinary.get_json(
        f"/repos/{REPOSITORY}/issues/comments/{int(completion_comment['id'])}"
    )
    completion_projection, _completion_body, completion_binding_live = (
        _v1._comment_projection(
            completion_response.data,
            comment_id=int(completion_comment["id"]),
            pull_number=pull_number,
            owner_id=int(live_repository["owner_id"]),
        )
    )
    if (
        completion_projection != completion_comment
        or completion_binding_live != complete["parsed_completion_binding"]
    ):
        raise VerificationError(
            "live owner completion comment/hash chain differs from closure reports"
        )
    if (
        completion_binding_live.get("authority_evidence_sha256")
        != chain["authority"]
        or completion_binding_live.get("pre_merge_evidence_sha256")
        != chain["pre_merge"]
    ):
        raise VerificationError(
            "live owner completion comment does not bind the report hashes"
        )

    live_protection_capture = capture_protection(
        ordinary,
        observed_at=now,
        protection_github=protected,
    )
    live_protection, live_protection_raw = _v1._validate_protection_capture(
        live_protection_capture
    )
    expected_protection = _v1._dict(
        complete["protection_after"],
        "complete protection after",
    )
    mismatch_fields = [
        field
        for field in sanitized_mismatch_fields(
            live_protection,
            expected_protection,
        )
        if field != "main_sha"
    ]
    if mismatch_fields:
        raise VerificationError(
            "live restored protection mismatch fields: "
            + ",".join(mismatch_fields)
        )
    if live_protection.get("main_sha") != base_sha:
        raise VerificationError(
            "live restored protection main_sha is not the v4 verifier-fix base"
        )

    final_head = str(chain["control_final_head"])
    checks_response = ordinary.get_json(
        f"/repos/{REPOSITORY}/commits/{final_head}/check-runs?per_page=100"
    )
    live_checks = _v1._check_runs(checks_response.data, final_head)
    if live_checks != pre_merge["required_check_runs"]:
        raise VerificationError(
            "live control final-head checks differ from pre-merge report"
        )

    return {
        "schema_version": 1,
        "transaction_id": TRANSACTION_ID,
        "phase": "evidence-closure-verification",
        "verified_at": _v1._timestamp(now),
        "closure": {
            "base_sha": base_sha,
            "head_sha": head_sha,
            "delta": delta,
            "report_blob_artifacts": {
                path: _v1._artifact_ref(report_blobs[path])
                for path in CLOSURE_EVIDENCE_PATHS
            },
        },
        "report_object_sha256": {
            "authority": chain["authority"],
            "pre_merge": chain["pre_merge"],
            "complete": chain["complete"],
        },
        "historical_git_object_validation": {
            "stage1_commit_sha": authority["stage1"]["commit_sha"],
            "control_merge_sha": ORIGINAL_CONTROL_SQUASH_SHA,
            "result": "PASS",
        },
        "v4_verifier_fix": v4_fix,
        "live_owner_provenance": {
            "authorization_binding": authority_binding_live,
            "completion_binding": completion_binding_live,
        },
        "live_control_final_head_checks": live_checks,
        "live_protection_after": live_protection,
        "mixed_reader_recovery": {
            "ordinary_reader_fields": [
                "branch_identity",
                "check_runs",
                "comments",
                "pull_request",
                "repository_identity",
            ],
            "administration_reader_fields": [
                "branch_protection_rest",
                "repository_merge_settings_graphql_query",
                "rulesets_rest",
            ],
            "mismatch_diagnostics": "normalized-field-names-only",
            "result": "PASS",
        },
        "raw_artifacts": {
            "repository": _v1._artifact_ref(repository_response.raw),
            "authorization_comment": _v1._artifact_ref(authority_response.raw),
            "pull_request": _v1._artifact_ref(pull_response.raw),
            "main": _v1._artifact_ref(main_response.raw),
            "completion_comment": _v1._artifact_ref(completion_response.raw),
            "protection_after": _v1._artifact_ref(live_protection_raw),
            "check_runs": _v1._artifact_ref(checks_response.raw),
        },
    }


def main(argv: list[str] | None = None) -> int:
    args = _v1._parser().parse_args(argv)
    try:
        if args.command == "render-authority-body":
            print(
                render_authorization_body(
                    GitRepository(args.repository_path),
                    args.base,
                    args.stage1,
                )
            )
            return 0
        github = _v1._reader(args.token_env)
        if args.command == "authority":
            evidence = build_authority_evidence(
                GitRepository(args.repository_path),
                github,
                pull_number=args.pr,
                comment_id=args.comment_id,
                stage1_sha=args.stage1,
            )
            _v1._write_json(args.output, evidence)
        elif args.command == "capture-protection":
            administration = _protection_reader(args.protection_token_env)
            _v1._write_json(
                args.output,
                capture_protection(
                    github,
                    protection_github=administration,
                ),
            )
        elif args.command == "pre-merge":
            administration = _protection_reader(args.protection_token_env)
            evidence = build_pre_merge_evidence(
                GitRepository(args.repository_path),
                github,
                _v1._read_json(args.authority_evidence),
                _v1._read_json(args.protection_before),
                _v1._read_json(args.protection_during),
                protection_github=administration,
            )
            _v1._write_json(args.output, evidence)
        elif args.command == "render-completion-body":
            administration = _protection_reader(args.protection_token_env)
            print(
                render_completion_body(
                    GitRepository(args.repository_path),
                    github,
                    _v1._read_json(args.pre_merge_evidence),
                    _v1._read_json(args.protection_after),
                    protection_github=administration,
                )
            )
        elif args.command == "complete":
            administration = _protection_reader(args.protection_token_env)
            evidence = build_complete_evidence(
                GitRepository(args.repository_path),
                github,
                _v1._read_json(args.pre_merge_evidence),
                _v1._read_json(args.protection_after),
                completion_comment_id=args.completion_comment_id,
                protection_github=administration,
            )
            _v1._write_json(args.output, evidence)
        elif args.command == "verify-evidence-closure":
            administration = _protection_reader(args.protection_token_env)
            evidence = verify_evidence_closure(
                GitRepository(args.repository_path),
                github,
                base_sha=args.base,
                head_sha=args.head,
                protection_github=administration,
            )
            if args.output is not None:
                _v1._write_json(args.output, evidence)
        else:  # pragma: no cover - argparse enforces subcommands
            raise VerificationError("unknown command")
    except VerificationError as exc:
        print(
            f"WP-0002 LOCAL OPERATOR TRANSACTION: FAIL: {exc}",
            file=sys.stderr,
        )
        return 1
    print("WP-0002 LOCAL OPERATOR TRANSACTION: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
