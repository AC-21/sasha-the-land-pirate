#!/usr/bin/env python3
"""Versioned forward-only WP-0002 local-operator transaction verifier.

V1 remains an immutable historical artifact.  This adapter hash-pins and loads
that reviewed implementation as a real temporary module, then replaces only
the versioned identity, path, and receipt-binding seams needed by the v2
successor.  The public historical-object verifier independently recomputes the
Stage-1, receipt-only final head, and squash merge from Git objects; repository
working-tree bytes are never accepted as a substitute for those objects.
"""

from __future__ import annotations

import hashlib
import json
import re
import sys
import types
from datetime import datetime
from pathlib import Path
from typing import Mapping


REPOSITORY = "AC-21/sasha-the-land-pirate"
OWNER_LOGIN = "AC-21"
TRANSACTION_ID = "WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718"
AUTHORIZATION_CLAIM = "AUTHORIZE-WP0002-DELEGATED-LOCAL-UNITY-OPERATOR-SUCCESSOR"
SUPERSESSION_CLAIM = "SUPERSEDE-WP0002-LOCAL-OPERATOR-20260717-UNEXECUTED"
COMPLETION_CLAIM = "COMPLETE-WP0002-LOCAL-OPERATOR-SUCCESSOR-CONTROL-TRANSACTION"
CONTRACT_SHA256 = "ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa"
V1_CONTROL_MERGE_SHA = "96002331dc069db5a7bab36baaf359d1b46cc64c"
V1_AMENDMENT_ID = "A1B-WP-0002-LOCAL-OPERATOR-20260717"
V1_RECEIPT_ID = "RR-WP0002-LOCAL-OPERATOR-20260717"
V1_RECEIPT_SHA256 = "fb1df094bac7c4b944e1438eff4e761963e7076fb58b69f36ef497939eba8b8e"
V1_BOUNDARY_SHA256 = "e92e7276dc97478d7412307f43a5f90b60b99256b2a8a7cd5d00626c4f2e0962"
V1_RECEIPT_PATH = (
    "docs/foundation-v0.1/ledger/receipts/"
    "RR-WP0002-LOCAL-OPERATOR-20260717.json"
)
V1_BOUNDARY_PATH = "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json"
RECEIPT_ID = "RR-WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718"
RECEIPT_PATH = (
    "docs/foundation-v0.1/ledger/receipts/"
    "RR-WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718.json"
)
EVIDENCE_ROOT = "docs/evidence/WP-0002/local-operator-successor"
AUTHORITY_EVIDENCE_PATH = f"{EVIDENCE_ROOT}/authority.json"
PRE_MERGE_EVIDENCE_PATH = f"{EVIDENCE_ROOT}/pre-merge.json"
COMPLETE_EVIDENCE_PATH = f"{EVIDENCE_ROOT}/complete.json"
PROTECTION_BEFORE_PATH = f"{EVIDENCE_ROOT}/control/protection-before.json"
MAX_BEFORE_TO_AUTHORITY_SECONDS = 300
CLOSURE_EVIDENCE_PATHS = (
    AUTHORITY_EVIDENCE_PATH,
    PRE_MERGE_EVIDENCE_PATH,
    COMPLETE_EVIDENCE_PATH,
)
V1_CLOSURE_EVIDENCE_PATHS = (
    "docs/evidence/WP-0002/local-operator-amendment/authority.json",
    "docs/evidence/WP-0002/local-operator-amendment/pre-merge.json",
    "docs/evidence/WP-0002/local-operator-amendment/complete.json",
)
V1_RETAINED_ARTIFACT_SHA256 = {
    "Tools/Validation/collect_wp0002_scope_capture.py": (
        "68dfa2c5ce802b71a29717f530be63344d74c50cc8e5e5de4c1b26aa3dcde9f2"
    ),
    "Tools/Validation/verify_wp0002_local_operator_transaction.py": (
        "16a3a5950e191f25b64d86977a64489eb77961ee8b8ca16673c7673e17779c51"
    ),
    "docs/foundation-v0.1/tools/test_collect_wp0002_scope_capture.py": (
        "8891447debe6946485512fc1834209a3371ef886c932cfbd4d779f2c83656de9"
    ),
    "docs/foundation-v0.1/tools/test_verify_wp0002_local_operator_transaction.py": (
        "dea35a4a689bdf196910d23c7d6742bd4265907c0833cc1fce87aa9c12810ad9"
    ),
    "docs/foundation-v0.1/schemas/wp0002-local-operator-scope-capture.schema.json": (
        "391a24931c0eff1fd2c605eb171f2dabeb2bbb896b95ba702d839713d908cb7c"
    ),
    "docs/foundation-v0.1/schemas/wp0002-local-operator-transaction-evidence.schema.json": (
        "0baed1e7f44e3aeeb402f4b278d5e0ffde5899d6551481ec9385199052b45aca"
    ),
    "docs/foundation-v0.1/governance/"
    "WP-0002-DELEGATED-LOCAL-UNITY-OPERATOR-AMENDMENT.md": (
        "c9214dda35e7f484e8fdfe342e4b4f3c88fb959bd79706c25f5d4b5d42c20d40"
    ),
    "docs/evidence/WP-0002/local-operator-amendment/scope-capture/"
    "working-tree-scope.json": (
        "06427c95857bf1763a036cdad7c2de7d8864efd4f53ac8f68120573bdce283a1"
    ),
    "docs/evidence/WP-0002/local-operator-amendment/scope-capture/"
    "working-tree-scope.observations."
    "089a49fc03dbbc473158a02b31c6a3ca5fea1f7707c78b5466247f17511554ad.json": (
        "089a49fc03dbbc473158a02b31c6a3ca5fea1f7707c78b5466247f17511554ad"
    ),
    "docs/evidence/WP-0002/local-operator-amendment/scope-capture/"
    "working-tree-scope.status."
    "bea960c9a69eda2a80c43103d093da728e30df0c65763f17ba6d1a994ea83d02.bin": (
        "bea960c9a69eda2a80c43103d093da728e30df0c65763f17ba6d1a994ea83d02"
    ),
}
STAGE1_CONTROL_PATHS = frozenset(
    {
        ".github/workflows/wp0002-policy.yml",
        "AGENTS.md",
        "README.md",
        "Tools/Validation/collect_wp0002_scope_capture_successor.py",
        "Tools/Validation/verify_wp0002_local_operator_transaction_v2.py",
        PROTECTION_BEFORE_PATH,
        "docs/foundation-v0.1/04-TECHNICAL-ARCHITECTURE.md",
        "docs/foundation-v0.1/15-LEAN-A1-LOCAL-DEVELOPMENT.md",
        "docs/foundation-v0.1/README.md",
        "docs/foundation-v0.1/governance/"
        "WP-0002-DELEGATED-LOCAL-UNITY-OPERATOR-SUCCESSOR.md",
        V1_BOUNDARY_PATH,
        "docs/foundation-v0.1/schemas/local-a1-boundary.schema.json",
        "docs/foundation-v0.1/schemas/"
        "wp0002-local-operator-successor-scope-capture.schema.json",
        "docs/foundation-v0.1/schemas/"
        "wp0002-local-operator-successor-transaction-evidence.schema.json",
        "docs/foundation-v0.1/tools/"
        "test_collect_wp0002_scope_capture_successor.py",
        "docs/foundation-v0.1/tools/"
        "test_verify_wp0002_local_operator_transaction_v2.py",
        "docs/foundation-v0.1/tools/test_validate_local_a1_boundary.py",
        "docs/foundation-v0.1/tools/validate_foundation.py",
        "docs/foundation-v0.1/work-packets/proposed/WP-0002.json",
    }
)
STAGE1_SCOPE_CAPTURE_PATH = (
    "docs/evidence/WP-0002/local-operator-successor/scope-capture/"
    "working-tree-scope.json"
)
STAGE1_SCOPE_CAPTURE_ARTIFACT_PATTERNS = {
    "raw_status": re.compile(
        r"docs/evidence/WP-0002/local-operator-successor/scope-capture/"
        r"working-tree-scope\.status\.[0-9a-f]{64}\.bin"
    ),
    "observations": re.compile(
        r"docs/evidence/WP-0002/local-operator-successor/scope-capture/"
        r"working-tree-scope\.observations\.[0-9a-f]{64}\.json"
    ),
}
TEMPORARY_NONREQUIRED_CHECK = "wp0002-policy"
V1_VERIFIER_SHA256 = "16a3a5950e191f25b64d86977a64489eb77961ee8b8ca16673c7673e17779c51"
V1_VERIFIER_PATH = Path(__file__).with_name(
    "verify_wp0002_local_operator_transaction.py"
)
_V1_MODULE_NAME = "_wp0002_local_operator_transaction_v1_pinned"
_MISSING = object()


class V2LoaderError(RuntimeError):
    """Raised when the immutable v1 implementation cannot be loaded exactly."""


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _load_v1() -> types.ModuleType:
    try:
        source = V1_VERIFIER_PATH.read_bytes()
    except OSError as exc:
        raise V2LoaderError(f"v1 verifier cannot be read: {exc}") from exc
    if _sha256(source) != V1_VERIFIER_SHA256:
        raise V2LoaderError("v1 verifier hash mismatch")
    previous = sys.modules.get(_V1_MODULE_NAME, _MISSING)
    module = types.ModuleType(_V1_MODULE_NAME)
    module.__file__ = str(V1_VERIFIER_PATH)
    module.__package__ = ""
    sys.modules[_V1_MODULE_NAME] = module
    try:
        exec(compile(source, str(V1_VERIFIER_PATH), "exec"), module.__dict__)
    except Exception as exc:
        raise V2LoaderError(f"v1 verifier cannot load: {exc}") from exc
    finally:
        if previous is _MISSING:
            sys.modules.pop(_V1_MODULE_NAME, None)
        else:
            sys.modules[_V1_MODULE_NAME] = previous  # type: ignore[assignment]
    return module


_v1 = _load_v1()
VerificationError = _v1.VerificationError
GitRepository = _v1.GitRepository
GitHubReader = _v1.GitHubReader
LiveGitHubReader = _v1.LiveGitHubReader
APIResult = _v1.APIResult
evidence_sha256 = _v1.evidence_sha256
canonical_json_bytes = _v1.canonical_json_bytes


def authorization_binding(stage1: Mapping[str, object]) -> dict[str, object]:
    return {
        "changed_files_manifest_sha256": stage1["changed_files_manifest_sha256"],
        "claim": AUTHORIZATION_CLAIM,
        "contract_sha256": CONTRACT_SHA256,
        "receipt_path": RECEIPT_PATH,
        "stage1_commit": stage1["commit_sha"],
        "stage1_patch_sha256": stage1["deterministic_patch_sha256"],
        "stage1_tree": stage1["tree_oid"],
        "superseded_v1_receipt_id": V1_RECEIPT_ID,
        "superseded_v1_receipt_sha256": V1_RECEIPT_SHA256,
        "superseded_v1_boundary_sha256": V1_BOUNDARY_SHA256,
        "supersession_claim": SUPERSESSION_CLAIM,
        "temporary_nonrequired_check": TEMPORARY_NONREQUIRED_CHECK,
    }


_original_validate_binding = _v1._validate_binding


def _validate_binding(value: object, *, completion: bool = False) -> dict[str, object]:
    if completion:
        return _original_validate_binding(value, completion=True)
    binding = _v1._dict(value, "comment binding")
    _v1._exact_keys(
        binding,
        {
            "changed_files_manifest_sha256",
            "claim",
            "contract_sha256",
            "receipt_path",
            "stage1_commit",
            "stage1_patch_sha256",
            "stage1_tree",
            "superseded_v1_receipt_id",
            "superseded_v1_receipt_sha256",
            "superseded_v1_boundary_sha256",
            "supersession_claim",
            "temporary_nonrequired_check",
        },
        "comment binding",
    )
    return binding


def _validate_receipt(
    receipt: object,
    *,
    authority: Mapping[str, object],
    expected_artifacts: Mapping[str, str],
) -> None:
    value = _v1._dict(receipt, "v2 local operator receipt")
    _v1._exact_keys(value, _v1.RECEIPT_KEYS, "v2 local operator receipt")
    comment = _v1._dict(authority["authorization_comment"], "authority comment")
    source = str(comment["html_url"])
    resolver = _v1._dict(value.get("artifact_resolver"), "receipt artifact_resolver")
    superseded_claim = {
        "subject_id": V1_AMENDMENT_ID,
        "claims": [SUPERSESSION_CLAIM],
    }
    authorization_claim = {
        "subject_id": "WP-0002",
        "claims": [AUTHORIZATION_CLAIM],
    }
    exact = {
        "accepted_commit": _v1._dict(authority["stage1"], "stage1")["commit_sha"],
        "approval_text_sha256": comment["body_utf8_sha256"],
        "foundation_binding": None,
        "issued_by": OWNER_LOGIN,
        "issuer_role": "creator",
        "receipt_id": RECEIPT_ID,
        "receipt_kind": "creator-authorization",
        "sealed": True,
        "signature_reference": source,
        "source_reference": source,
        "subject_claims": [superseded_claim, authorization_claim],
        "subject_contract_sha256": {
            V1_AMENDMENT_ID: V1_BOUNDARY_SHA256,
            "WP-0002": CONTRACT_SHA256,
        },
        "subject_event_sha256": {},
        "subject_ids": [V1_AMENDMENT_ID, "WP-0002"],
    }
    for key, expected in exact.items():
        if value.get(key) != expected:
            raise VerificationError(f"v2 local operator receipt has wrong {key}")
    _v1._parse_time(value.get("issued_at"), "receipt issued_at")
    if resolver != {"type": "external-protected", "resolver_reference": source}:
        raise VerificationError("v2 receipt resolver does not bind the authority comment")
    artifacts = _v1._dict(value.get("artifact_sha256"), "receipt artifact_sha256")
    exact_artifacts = dict(expected_artifacts)
    if V1_RECEIPT_PATH in exact_artifacts:
        raise VerificationError("Stage-1 must not modify the retained v1 receipt")
    exact_artifacts[V1_RECEIPT_PATH] = V1_RECEIPT_SHA256
    if artifacts != exact_artifacts:
        raise VerificationError("v2 receipt artifact keys or values differ")


def _install_v2_bindings() -> None:
    replacements = {
        "TRANSACTION_ID": TRANSACTION_ID,
        "AUTHORIZATION_CLAIM": AUTHORIZATION_CLAIM,
        "COMPLETION_CLAIM": COMPLETION_CLAIM,
        "CONTRACT_SHA256": CONTRACT_SHA256,
        "RECEIPT_ID": RECEIPT_ID,
        "RECEIPT_PATH": RECEIPT_PATH,
        "AUTHORITY_EVIDENCE_PATH": AUTHORITY_EVIDENCE_PATH,
        "PRE_MERGE_EVIDENCE_PATH": PRE_MERGE_EVIDENCE_PATH,
        "COMPLETE_EVIDENCE_PATH": COMPLETE_EVIDENCE_PATH,
        "CLOSURE_EVIDENCE_PATHS": CLOSURE_EVIDENCE_PATHS,
        "authorization_binding": authorization_binding,
        "_validate_binding": _validate_binding,
        "_validate_receipt": _validate_receipt,
    }
    for name, value in replacements.items():
        setattr(_v1, name, value)


_install_v2_bindings()
_original_verify_evidence_closure = _v1.verify_evidence_closure
_original_render_authorization_body = _v1.render_authorization_body
_original_build_authority_evidence = _v1.build_authority_evidence
_original_build_pre_merge_evidence = _v1.build_pre_merge_evidence
_original_render_completion_body = _v1.render_completion_body
_original_build_complete_evidence = _v1.build_complete_evidence


def _blob_sha256_at(repository: GitRepository, commit: str, path: str) -> str:
    return _sha256(repository.blob_at(commit, path))


def _path_exists_at(repository: GitRepository, commit: str, path: str) -> bool:
    safe = _v1._safe_path(path, "historical path")
    return bool(
        repository._run(
            "ls-tree",
            "-z",
            "--full-tree",
            commit,
            "--",
            safe,
        )
    )


def _stage1_scope_capture_paths(
    repository: GitRepository,
    stage1_sha: str,
) -> set[str]:
    """Resolve the exact three capture paths and verify their Git blobs."""
    try:
        raw_capture = repository.blob_at(stage1_sha, STAGE1_SCOPE_CAPTURE_PATH)
        capture = json.loads(raw_capture.decode("utf-8"))
    except Exception as exc:
        raise VerificationError(
            "historical Stage-1 successor scope capture is unavailable or invalid"
        ) from exc
    if not isinstance(capture, Mapping):
        raise VerificationError("historical Stage-1 successor scope capture is not an object")
    artifacts = capture.get("artifacts")
    if not isinstance(artifacts, Mapping) or set(artifacts) != set(
        STAGE1_SCOPE_CAPTURE_ARTIFACT_PATTERNS
    ):
        raise VerificationError(
            "historical Stage-1 successor scope capture artifacts are not exact"
        )
    paths = {STAGE1_SCOPE_CAPTURE_PATH}
    for key, pattern in STAGE1_SCOPE_CAPTURE_ARTIFACT_PATTERNS.items():
        artifact = artifacts.get(key)
        if not isinstance(artifact, Mapping):
            raise VerificationError(
                f"historical Stage-1 successor {key} artifact is not an object"
            )
        path = artifact.get("path")
        digest = artifact.get("sha256")
        byte_size = artifact.get("byte_size")
        if (
            not isinstance(path, str)
            or pattern.fullmatch(path) is None
            or not isinstance(digest, str)
            or re.fullmatch(r"[0-9a-f]{64}", digest) is None
            or digest not in path
            or not isinstance(byte_size, int)
            or isinstance(byte_size, bool)
            or byte_size < 0
        ):
            raise VerificationError(
                f"historical Stage-1 successor {key} artifact binding is not exact"
            )
        try:
            blob = repository.blob_at(stage1_sha, path)
        except Exception as exc:
            raise VerificationError(
                f"historical Stage-1 successor {key} artifact is unavailable"
            ) from exc
        if _sha256(blob) != digest or len(blob) != byte_size:
            raise VerificationError(
                f"historical Stage-1 successor {key} artifact bytes differ"
            )
        paths.add(path)
    if len(paths) != 3:
        raise VerificationError(
            "historical Stage-1 successor scope capture paths are not distinct"
        )
    return paths


def validate_protection_before_capture(
    capture: Mapping[str, object],
    *,
    base_sha: str,
    authorization_comment: Mapping[str, object] | None = None,
) -> tuple[dict[str, object], bytes]:
    """Validate exact-three protection state and its owner-authority freshness."""
    normalized, raw_bytes = _v1._validate_protection_capture(capture)
    if normalized.get("main_sha") != base_sha:
        raise VerificationError("protection-before main SHA differs from Stage-1 base")
    required_true = (
        "strict",
        "enforce_admins",
        "pull_request_required",
        "conversation_resolution",
        "linear_history",
        "bypass_allowances_empty",
        "rulesets_empty",
        "push_restrictions_empty",
        "force_push_disabled",
        "deletion_disabled",
        "squash_only",
    )
    if any(normalized.get(key) is not True for key in required_true):
        raise VerificationError("protection-before weakens a retained invariant")
    if normalized.get("dismiss_stale_reviews") is not False:
        raise VerificationError("protection-before enables stale-review dismissal")
    expected_checks = {
        (name, _v1.REQUIRED_APP_ID) for name in _v1.FULL_REQUIRED_CHECKS
    }
    required_checks = _v1._list(
        normalized.get("required_checks"),
        "protection-before required checks",
    )
    if (
        _v1._required_check_set(normalized) != expected_checks
        or len(required_checks) != len(expected_checks)
    ):
        raise VerificationError("protection-before must require exactly all three checks")
    if authorization_comment is not None:
        before_time = _v1._parse_time(
            normalized.get("observed_at"),
            "protection-before observed_at",
        )
        comment_time = _v1._parse_time(
            authorization_comment.get("created_at"),
            "authorization comment created_at",
        )
        delta = (comment_time - before_time).total_seconds()
        if delta < 0 or delta > MAX_BEFORE_TO_AUTHORITY_SECONDS:
            raise VerificationError(
                "protection-before must precede owner authority by at most 300 seconds"
            )
    return normalized, raw_bytes


def _stage1_protection_before(
    repository: GitRepository,
    stage1_sha: str,
    *,
    base_sha: str,
    authorization_comment: Mapping[str, object] | None = None,
) -> tuple[dict[str, object], dict[str, object], bytes]:
    """Load the committed exact-three capture and bind it to owner authority time."""
    try:
        blob = repository.blob_at(stage1_sha, PROTECTION_BEFORE_PATH)
        capture = json.loads(blob.decode("utf-8"))
    except Exception as exc:
        raise VerificationError(
            "historical Stage-1 protection-before capture is unavailable or invalid"
        ) from exc
    if not isinstance(capture, dict):
        raise VerificationError("historical Stage-1 protection-before is not an object")
    normalized, raw_bytes = validate_protection_before_capture(
        capture,
        base_sha=base_sha,
        authorization_comment=authorization_comment,
    )
    return capture, normalized, raw_bytes


def validate_historical_git_objects(
    repository: GitRepository,
    authority: Mapping[str, object],
    pre_merge: Mapping[str, object] | None = None,
    complete: Mapping[str, object] | None = None,
) -> None:
    """Recompute all supplied transaction commits from immutable Git objects."""
    stage1 = _v1._dict(authority.get("stage1"), "stage1")
    recomputed = _v1._stage1_record(
        repository,
        str(stage1.get("base_sha")),
        str(stage1.get("commit_sha")),
    )
    if recomputed != stage1:
        raise VerificationError("historical Stage-1 Git objects differ from evidence")
    _v1._validate_changed_manifest(
        stage1.get("changed_files"),
        "historical Stage-1 changed files",
    )
    base_sha = str(stage1["base_sha"])
    stage1_sha = str(stage1["commit_sha"])
    if base_sha != V1_CONTROL_MERGE_SHA:
        raise VerificationError("Stage-1 base is not the exact unclosed v1 control merge")
    if _blob_sha256_at(repository, base_sha, V1_RECEIPT_PATH) != V1_RECEIPT_SHA256:
        raise VerificationError("historical base v1 receipt hash differs")
    if _blob_sha256_at(repository, stage1_sha, V1_RECEIPT_PATH) != V1_RECEIPT_SHA256:
        raise VerificationError("historical Stage-1 v1 receipt hash differs")
    if _blob_sha256_at(repository, base_sha, V1_BOUNDARY_PATH) != V1_BOUNDARY_SHA256:
        raise VerificationError("historical base v1 boundary hash differs")
    changed_paths = {
        str(item.get("path"))
        for item in stage1["changed_files"]
        if isinstance(item, Mapping)
    }
    for path, expected_sha256 in V1_RETAINED_ARTIFACT_SHA256.items():
        if path in changed_paths:
            raise VerificationError(
                f"Stage-1 modifies retained v1 artifact: {path}"
            )
        for phase, commit_sha in (("base", base_sha), ("Stage-1", stage1_sha)):
            if _blob_sha256_at(repository, commit_sha, path) != expected_sha256:
                raise VerificationError(
                    f"historical {phase} retained v1 artifact hash differs: {path}"
                )
    boundary_changes = [
        item
        for item in stage1["changed_files"]
        if item.get("path") == V1_BOUNDARY_PATH
    ]
    if len(boundary_changes) != 1 or boundary_changes[0].get("status") != "M":
        raise VerificationError("Stage-1 does not supersede the exact v1 boundary")
    old_boundary_blob = repository.blob(str(boundary_changes[0].get("old_oid")))
    if _sha256(old_boundary_blob) != V1_BOUNDARY_SHA256:
        raise VerificationError("Stage-1 old boundary blob differs from v1")
    if V1_RECEIPT_PATH in changed_paths:
        raise VerificationError("Stage-1 modifies the retained v1 receipt")
    injected = [
        path
        for path in V1_CLOSURE_EVIDENCE_PATHS
        if _path_exists_at(repository, stage1_sha, path)
    ]
    if injected:
        raise VerificationError(
            f"Stage-1 contains forbidden v1 closure reports: {injected}"
        )
    injected_successor = [
        path
        for path in CLOSURE_EVIDENCE_PATHS
        if _path_exists_at(repository, stage1_sha, path)
    ]
    if injected_successor:
        raise VerificationError(
            "Stage-1 contains premature successor closure reports: "
            f"{injected_successor}"
        )
    capture_paths = _stage1_scope_capture_paths(repository, stage1_sha)
    expected_stage1_paths = STAGE1_CONTROL_PATHS | capture_paths
    missing_control_paths = sorted(expected_stage1_paths - changed_paths)
    if missing_control_paths:
        raise VerificationError(
            "Stage-1 omits exact successor control paths: "
            f"{missing_control_paths}"
        )
    out_of_scope = sorted(
        path for path in changed_paths if path not in expected_stage1_paths
    )
    if out_of_scope:
        raise VerificationError(
            "Stage-1 contains paths outside the exact successor control scope: "
            f"{out_of_scope}"
        )
    if PROTECTION_BEFORE_PATH not in changed_paths:
        raise VerificationError(
            "Stage-1 does not add the committed protection-before control artifact"
        )
    before_capture, before_normalized, before_raw = _stage1_protection_before(
        repository,
        stage1_sha,
        base_sha=base_sha,
        authorization_comment=(
            authority.get("authorization_comment")
            if isinstance(authority.get("authorization_comment"), Mapping)
            else None
        ),
    )
    if pre_merge is None:
        return
    if pre_merge.get("protection_before") != before_normalized:
        raise VerificationError(
            "pre-merge protection-before differs from the committed Stage-1 capture"
        )
    raw_artifacts = _v1._dict(
        pre_merge.get("raw_artifacts"),
        "pre-merge raw artifacts",
    )
    if raw_artifacts.get("protection_before") != _v1._artifact_ref(before_raw):
        raise VerificationError(
            "pre-merge protection-before raw reference differs from Stage-1"
        )
    final_pull = _v1._dict(pre_merge.get("final_pull_request"), "final pull request")
    materialization = _v1._dict(
        pre_merge.get("receipt_materialization"), "receipt materialization"
    )
    final_head = str(final_pull.get("head_sha"))
    final_commit = repository.commit(final_head)
    if final_commit["parents"] != [stage1["commit_sha"]]:
        raise VerificationError("historical final head is not the receipt-only child")
    if materialization.get("commit_sha") != final_head:
        raise VerificationError("receipt materialization names another final head")
    delta = repository.changed_files(str(stage1["commit_sha"]), final_head)
    if len(delta) != 1 or delta[0].get("path") != RECEIPT_PATH or delta[0].get("status") != "A":
        raise VerificationError("historical final head is not the exact v2 receipt delta")
    if delta[0].get("new_blob_sha256") != materialization.get("receipt_sha256"):
        raise VerificationError("historical receipt blob differs from evidence")
    if complete is None:
        return
    merged = _v1._dict(complete.get("merged_pull_request"), "merged pull request")
    merge = _v1._dict(complete.get("merge"), "merge proof")
    merge_sha = str(merged.get("merge_commit_sha"))
    merge_commit = repository.commit(merge_sha)
    if merge_commit["parents"] != [stage1["base_sha"]]:
        raise VerificationError("historical squash is not a single child of Stage-1 base")
    if merge_commit["tree"] != final_commit["tree"]:
        raise VerificationError("historical squash tree differs from final head tree")
    if merge.get("tree_oid") != merge_commit["tree"]:
        raise VerificationError("historical squash tree differs from complete evidence")


def validate_pending_receipt_child(
    repository: GitRepository,
    receipt: Mapping[str, object],
) -> dict[str, object]:
    """Keep the control PR nonmergeable until HEAD is the exact receipt-only child."""
    stage1_sha = str(receipt.get("accepted_commit"))
    authority = _stage1_authority_from_commit(repository, stage1_sha)
    authority["authorization_comment"] = {
        "html_url": receipt.get("source_reference"),
        "body_utf8_sha256": receipt.get("approval_text_sha256"),
        "created_at": receipt.get("issued_at"),
    }
    validate_historical_git_objects(repository, authority)
    stage1_base_sha = str(
        _v1._dict(authority["stage1"], "stage1").get("base_sha")
    )
    head_sha = repository._run("rev-parse", "--verify", "HEAD").decode(
        "ascii"
    ).strip()
    head = repository.commit(head_sha)
    final_head_sha = head_sha
    if head["parents"] != [stage1_sha]:
        parents = head.get("parents")
        if (
            not isinstance(parents, list)
            or len(parents) != 2
            or parents[0] != stage1_base_sha
        ):
            raise VerificationError("current HEAD is not the exact receipt-only child")
        pull_head = repository.commit(str(parents[1]))
        if pull_head["parents"] != [stage1_sha] or pull_head["tree"] != head["tree"]:
            raise VerificationError("current HEAD is not the exact receipt-only child")
        final_head_sha = str(pull_head["sha"])
    delta = repository.changed_files(stage1_sha, final_head_sha)
    _v1._validate_changed_manifest(delta, "receipt-only delta")
    if (
        len(delta) != 1
        or delta[0].get("path") != RECEIPT_PATH
        or delta[0].get("status") != "A"
    ):
        raise VerificationError("current HEAD is not the exact receipt-only child")
    receipt_blob = repository.blob_at(final_head_sha, RECEIPT_PATH)
    try:
        committed_receipt = json.loads(receipt_blob.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise VerificationError("committed successor receipt is not UTF-8 JSON") from exc
    if committed_receipt != receipt:
        raise VerificationError("loaded successor receipt differs from HEAD bytes")
    stage1 = _v1._dict(authority["stage1"], "stage1")
    expected_artifacts = {
        str(receipt.get("source_reference", "")).removeprefix("https://"): str(
            receipt.get("approval_text_sha256", "")
        )
    }
    for item in _v1._list(stage1.get("changed_files"), "stage1 changed files"):
        changed = _v1._dict(item, "stage1 changed file")
        expected_artifacts[str(changed["path"])] = str(
            changed["new_blob_sha256"]
        )
    _validate_receipt(
        committed_receipt,
        authority=authority,
        expected_artifacts=expected_artifacts,
    )
    if delta[0].get("new_blob_sha256") != _sha256(receipt_blob):
        raise VerificationError("receipt-only child blob hash differs")
    return {
        "stage1_sha": stage1_sha,
        "final_head_sha": final_head_sha,
        "receipt_sha256": _sha256(receipt_blob),
    }


def _stage1_authority_from_commit(
    repository: GitRepository,
    stage1_sha: str,
) -> dict[str, object]:
    commit = repository.commit(stage1_sha)
    parents = commit.get("parents")
    if not isinstance(parents, list) or len(parents) != 1:
        raise VerificationError("Stage-1 is not one direct child of its base")
    return {
        "stage1": _v1._stage1_record(
            repository,
            str(parents[0]),
            stage1_sha,
        )
    }


def _stage1_authority_from_pre_merge(
    repository: GitRepository,
    pre_merge: Mapping[str, object],
) -> dict[str, object]:
    pull = _v1._dict(pre_merge.get("final_pull_request"), "final pull request")
    receipt = _v1._dict(
        pre_merge.get("receipt_materialization"),
        "receipt materialization",
    )
    return {
        "stage1": _v1._stage1_record(
            repository,
            str(pull.get("base_sha")),
            str(receipt.get("parent_sha")),
        )
    }


def render_authorization_body(
    repository: GitRepository,
    base: str,
    stage1: str,
) -> str:
    authority = {
        "stage1": _v1._stage1_record(repository, base, stage1),
    }
    validate_historical_git_objects(repository, authority)
    return _original_render_authorization_body(repository, base, stage1)


def build_authority_evidence(
    repository: GitRepository,
    github: GitHubReader,
    *,
    pull_number: int,
    comment_id: int,
    stage1_sha: str,
) -> dict[str, object]:
    # Reject a bad historical base before any live authority is fetched.
    validate_historical_git_objects(
        repository,
        _stage1_authority_from_commit(repository, stage1_sha),
    )
    evidence = _original_build_authority_evidence(
        repository,
        github,
        pull_number=pull_number,
        comment_id=comment_id,
        stage1_sha=stage1_sha,
    )
    validate_historical_git_objects(repository, evidence)
    return evidence


def build_pre_merge_evidence(
    repository: GitRepository,
    github: GitHubReader,
    authority: Mapping[str, object],
    protection_before_capture: Mapping[str, object],
    protection_during_capture: Mapping[str, object],
    *,
    verify_live_during: bool = True,
    now: datetime | None = None,
) -> dict[str, object]:
    validate_historical_git_objects(repository, authority)
    stage1 = _v1._dict(authority.get("stage1"), "stage1")
    committed_before, _, _ = _stage1_protection_before(
        repository,
        str(stage1.get("commit_sha")),
        base_sha=str(stage1.get("base_sha")),
        authorization_comment=(
            authority.get("authorization_comment")
            if isinstance(authority.get("authorization_comment"), Mapping)
            else None
        ),
    )
    if protection_before_capture != committed_before:
        raise VerificationError(
            "supplied protection-before differs from committed Stage-1 capture"
        )
    evidence = _original_build_pre_merge_evidence(
        repository,
        github,
        authority,
        protection_before_capture,
        protection_during_capture,
        verify_live_during=verify_live_during,
        now=now,
    )
    validate_historical_git_objects(repository, authority, evidence)
    return evidence


def render_completion_body(
    repository: GitRepository,
    github: GitHubReader,
    pre_merge: Mapping[str, object],
    protection_after_capture: Mapping[str, object],
    *,
    verify_live_after: bool = True,
    now: datetime | None = None,
) -> str:
    authority = _stage1_authority_from_pre_merge(repository, pre_merge)
    validate_historical_git_objects(repository, authority, pre_merge)
    # The pinned v1 renderer independently proves the live squash parent/tree.
    return _original_render_completion_body(
        repository,
        github,
        pre_merge,
        protection_after_capture,
        verify_live_after=verify_live_after,
        now=now,
    )


def build_complete_evidence(
    repository: GitRepository,
    github: GitHubReader,
    pre_merge: Mapping[str, object],
    protection_after_capture: Mapping[str, object],
    *,
    completion_comment_id: int,
    verify_live_after: bool = True,
    now: datetime | None = None,
) -> dict[str, object]:
    authority = _stage1_authority_from_pre_merge(repository, pre_merge)
    validate_historical_git_objects(repository, authority, pre_merge)
    evidence = _original_build_complete_evidence(
        repository,
        github,
        pre_merge,
        protection_after_capture,
        completion_comment_id=completion_comment_id,
        verify_live_after=verify_live_after,
        now=now,
    )
    validate_historical_git_objects(repository, authority, pre_merge, evidence)
    return evidence


def _install_historical_phase_guards() -> None:
    _v1.render_authorization_body = render_authorization_body
    _v1.build_authority_evidence = build_authority_evidence
    _v1.build_pre_merge_evidence = build_pre_merge_evidence
    _v1.render_completion_body = render_completion_body
    _v1.build_complete_evidence = build_complete_evidence


_install_historical_phase_guards()


def verify_evidence_closure(*args: object, **kwargs: object) -> dict[str, object]:
    repository = args[0] if args else kwargs.get("repository")
    result = _original_verify_evidence_closure(*args, **kwargs)
    if not isinstance(repository, GitRepository):
        raise VerificationError("v2 closure requires the pinned GitRepository")
    closure = result.get("closure", {})
    head = str(closure.get("head_sha", ""))
    authority = json.loads(repository.blob_at(head, AUTHORITY_EVIDENCE_PATH))
    pre_merge = json.loads(repository.blob_at(head, PRE_MERGE_EVIDENCE_PATH))
    complete = json.loads(repository.blob_at(head, COMPLETE_EVIDENCE_PATH))
    validate_historical_git_objects(repository, authority, pre_merge, complete)
    result["historical_git_object_validation"] = {
        "stage1_commit_sha": authority["stage1"]["commit_sha"],
        "control_merge_sha": complete["merged_pull_request"]["merge_commit_sha"],
        "result": "PASS",
    }
    return result


validate_authority_evidence = _v1.validate_authority_evidence
validate_pre_merge_evidence = _v1.validate_pre_merge_evidence
validate_complete_evidence = _v1.validate_complete_evidence
capture_protection = _v1.capture_protection


def main(argv: list[str] | None = None) -> int:
    _v1.verify_evidence_closure = verify_evidence_closure
    try:
        return _v1.main(argv)
    finally:
        _v1.verify_evidence_closure = _original_verify_evidence_closure


if __name__ == "__main__":
    raise SystemExit(main())
