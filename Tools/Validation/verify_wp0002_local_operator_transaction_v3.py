#!/usr/bin/env python3
"""Forward-only mixed-reader recovery for the WP-0002 closure.

V1 and v2 remain immutable historical artifacts.  This adapter hash-pins and
loads v2, then corrects only the live protection projection: branch identity
continues to use the ordinary read-only GitHub reader, while branch
protection, repository merge settings, and rulesets all use the same
single-repository Administration-read reader.  Candidate code never receives
that reader.

The failed v2 transaction, receipt, report candidate, and Git objects remain
unchanged.  V3 uses a new receipt and evidence namespace whose verified
control squash becomes the later closure base.  A mismatch diagnostic emits
sorted normalized field names only; it never emits live or recorded values.
"""

from __future__ import annotations

import hashlib
import json
import sys
import types
from datetime import datetime
from pathlib import Path
from typing import Mapping


V2_VERIFIER_SHA256 = (
    "15ea81750bce5b5d3cee5a5711b8287f55238eb44343386c1d0f5ca88f484a19"
)
V2_VERIFIER_PATH = Path(__file__).with_name(
    "verify_wp0002_local_operator_transaction_v2.py"
)
_V2_MODULE_NAME = "_wp0002_local_operator_transaction_v2_pinned_for_v3"
_MISSING = object()
_VOLATILE_PROTECTION_FIELDS = frozenset(
    {"observed_at", "raw_response_sha256", "raw_sha256"}
)
REPOSITORY = "AC-21/sasha-the-land-pirate"
OWNER_LOGIN = "AC-21"
TRANSACTION_ID = "WP0002-LOCAL-OPERATOR-CLOSURE-RECOVERY-20260718"
AUTHORIZATION_CLAIM = "AUTHORIZE-WP0002-LOCAL-OPERATOR-MIXED-READER-RECOVERY"
SUPERSESSION_CLAIM = (
    "SUPERSEDE-WP0002-LOCAL-OPERATOR-SUCCESSOR-CLOSURE-VERIFIER-V2-UNEXECUTED"
)
COMPLETION_CLAIM = "COMPLETE-WP0002-LOCAL-OPERATOR-RECOVERY-CONTROL-TRANSACTION"
TEMPORARY_NONREQUIRED_CHECK = "wp0002-policy"
CONTROL_REQUIRED_CHECKS = ("validate", "wp0002-core")
CONTRACT_SHA256 = "ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa"
CONTROL_BASE_SHA = "166d53698d013c605c7bee749368193dc2834644"
PREDECESSOR_AMENDMENT_ID = "A1B-WP-0002-LOCAL-OPERATOR-SUCCESSOR-20260718"
PREDECESSOR_TRANSACTION_ID = "WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718"
PREDECESSOR_RECEIPT_ID = "RR-WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718"
PREDECESSOR_RECEIPT_SHA256 = (
    "b82fc75e9f359f23a976ed3e073fddcf92a1b6d632b5d9e1d5115e70e43904ce"
)
PREDECESSOR_BOUNDARY_SHA256 = (
    "1f9bbce5906b720ba6aff92143584b9c668731c8c11f71fe6d967cff26e358a2"
)
PREDECESSOR_RECEIPT_PATH = (
    "docs/foundation-v0.1/ledger/receipts/"
    "RR-WP0002-LOCAL-OPERATOR-SUCCESSOR-20260718.json"
)
BOUNDARY_PATH = "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json"
RECEIPT_ID = "RR-WP0002-LOCAL-OPERATOR-RECOVERY-20260718"
RECEIPT_PATH = (
    "docs/foundation-v0.1/ledger/receipts/"
    "RR-WP0002-LOCAL-OPERATOR-RECOVERY-20260718.json"
)
EVIDENCE_ROOT = "docs/evidence/WP-0002/local-operator-recovery"
AUTHORITY_EVIDENCE_PATH = f"{EVIDENCE_ROOT}/authority.json"
PRE_MERGE_EVIDENCE_PATH = f"{EVIDENCE_ROOT}/pre-merge.json"
COMPLETE_EVIDENCE_PATH = f"{EVIDENCE_ROOT}/complete.json"
CLOSURE_EVIDENCE_PATHS = (
    AUTHORITY_EVIDENCE_PATH,
    PRE_MERGE_EVIDENCE_PATH,
    COMPLETE_EVIDENCE_PATH,
)
PREDECESSOR_CLOSURE_EVIDENCE_PATHS = (
    "docs/evidence/WP-0002/local-operator-successor/authority.json",
    "docs/evidence/WP-0002/local-operator-successor/pre-merge.json",
    "docs/evidence/WP-0002/local-operator-successor/complete.json",
)
RETAINED_PREDECESSOR_ARTIFACTS = {
    "Tools/Validation/verify_wp0002_local_operator_transaction.py": (
        "16a3a5950e191f25b64d86977a64489eb77961ee8b8ca16673c7673e17779c51"
    ),
    "Tools/Validation/verify_wp0002_local_operator_transaction_v2.py": (
        V2_VERIFIER_SHA256
    ),
    PREDECESSOR_RECEIPT_PATH: PREDECESSOR_RECEIPT_SHA256,
    "docs/foundation-v0.1/governance/"
    "WP-0002-DELEGATED-LOCAL-UNITY-OPERATOR-SUCCESSOR.md": (
        "b09e2e15f1755524a1843c4e0331727b514d8744aa5d511e71c2e845547d892f"
    ),
    "docs/foundation-v0.1/schemas/"
    "wp0002-local-operator-successor-transaction-evidence.schema.json": (
        "96380cf51d29088f2356b37b5d96a564bf04d8fe098f5b109781fc8710f59f16"
    ),
}
STAGE1_CONTROL_PATHS = frozenset(
    {
        ".github/workflows/wp0002-policy.yml",
        "Tools/Validation/verify_wp0002_local_operator_transaction_v3.py",
        "docs/foundation-v0.1/governance/"
        "WP-0002-LOCAL-OPERATOR-MIXED-READER-RECOVERY.md",
        BOUNDARY_PATH,
        "docs/foundation-v0.1/schemas/local-a1-boundary.schema.json",
        "docs/foundation-v0.1/schemas/"
        "wp0002-local-operator-recovery-control.schema.json",
        "docs/foundation-v0.1/schemas/"
        "wp0002-local-operator-recovery-transaction-evidence.schema.json",
        "docs/foundation-v0.1/tools/test_validate_local_a1_boundary.py",
        "docs/foundation-v0.1/tools/"
        "test_verify_wp0002_local_operator_transaction_v3.py",
        "docs/foundation-v0.1/tools/validate_foundation.py",
        "docs/foundation-v0.1/work-packets/proposed/WP-0002.json",
    }
)


class V3LoaderError(RuntimeError):
    """Raised when the immutable v2 verifier cannot be loaded exactly."""


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _load_v2() -> types.ModuleType:
    try:
        source = V2_VERIFIER_PATH.read_bytes()
    except OSError as exc:
        raise V3LoaderError(f"v2 verifier cannot be read: {exc}") from exc
    if _sha256(source) != V2_VERIFIER_SHA256:
        raise V3LoaderError("v2 verifier hash mismatch")
    previous = sys.modules.get(_V2_MODULE_NAME, _MISSING)
    module = types.ModuleType(_V2_MODULE_NAME)
    module.__file__ = str(V2_VERIFIER_PATH)
    module.__package__ = ""
    sys.modules[_V2_MODULE_NAME] = module
    try:
        exec(compile(source, str(V2_VERIFIER_PATH), "exec"), module.__dict__)
    except Exception as exc:
        raise V3LoaderError(f"v2 verifier cannot load: {exc}") from exc
    finally:
        if previous is _MISSING:
            sys.modules.pop(_V2_MODULE_NAME, None)
        else:
            sys.modules[_V2_MODULE_NAME] = previous  # type: ignore[assignment]
    return module


_v2 = _load_v2()
_v1 = _v2._v1
VerificationError = _v2.VerificationError
GitRepository = _v2.GitRepository
GitHubReader = _v2.GitHubReader
LiveGitHubReader = _v2.LiveGitHubReader
APIResult = _v2.APIResult


def authorization_binding(stage1: Mapping[str, object]) -> dict[str, object]:
    return {
        "changed_files_manifest_sha256": stage1[
            "changed_files_manifest_sha256"
        ],
        "claim": AUTHORIZATION_CLAIM,
        "contract_sha256": CONTRACT_SHA256,
        "predecessor_boundary_sha256": PREDECESSOR_BOUNDARY_SHA256,
        "predecessor_receipt_id": PREDECESSOR_RECEIPT_ID,
        "predecessor_receipt_sha256": PREDECESSOR_RECEIPT_SHA256,
        "predecessor_transaction_id": PREDECESSOR_TRANSACTION_ID,
        "receipt_path": RECEIPT_PATH,
        "stage1_commit": stage1["commit_sha"],
        "stage1_patch_sha256": stage1["deterministic_patch_sha256"],
        "stage1_tree": stage1["tree_oid"],
        "supersession_claim": SUPERSESSION_CLAIM,
        "temporary_nonrequired_check": TEMPORARY_NONREQUIRED_CHECK,
    }


def _validate_binding(
    value: object,
    *,
    completion: bool = False,
) -> dict[str, object]:
    if completion:
        return _v1._validate_binding_v3_original(value, completion=True)
    binding = _v1._dict(value, "v3 comment binding")
    _v1._exact_keys(
        binding,
        {
            "changed_files_manifest_sha256",
            "claim",
            "contract_sha256",
            "predecessor_boundary_sha256",
            "predecessor_receipt_id",
            "predecessor_receipt_sha256",
            "predecessor_transaction_id",
            "receipt_path",
            "stage1_commit",
            "stage1_patch_sha256",
            "stage1_tree",
            "supersession_claim",
            "temporary_nonrequired_check",
        },
        "v3 comment binding",
    )
    return binding


def _validate_receipt(
    receipt: object,
    *,
    authority: Mapping[str, object],
    expected_artifacts: Mapping[str, str],
) -> None:
    value = _v1._dict(receipt, "v3 recovery receipt")
    _v1._exact_keys(value, _v1.RECEIPT_KEYS, "v3 recovery receipt")
    comment = _v1._dict(authority["authorization_comment"], "authority comment")
    source = str(comment["html_url"])
    resolver = _v1._dict(value.get("artifact_resolver"), "receipt resolver")
    exact = {
        "accepted_commit": _v1._dict(authority["stage1"], "stage1")[
            "commit_sha"
        ],
        "approval_text_sha256": comment["body_utf8_sha256"],
        "foundation_binding": None,
        "issued_by": OWNER_LOGIN,
        "issuer_role": "creator",
        "receipt_id": RECEIPT_ID,
        "receipt_kind": "creator-authorization",
        "sealed": True,
        "signature_reference": source,
        "source_reference": source,
        "subject_claims": [
            {
                "subject_id": PREDECESSOR_AMENDMENT_ID,
                "claims": [SUPERSESSION_CLAIM],
            },
            {"subject_id": "WP-0002", "claims": [AUTHORIZATION_CLAIM]},
        ],
        "subject_contract_sha256": {
            PREDECESSOR_AMENDMENT_ID: PREDECESSOR_BOUNDARY_SHA256,
            "WP-0002": CONTRACT_SHA256,
        },
        "subject_event_sha256": {},
        "subject_ids": [PREDECESSOR_AMENDMENT_ID, "WP-0002"],
    }
    for key, expected in exact.items():
        if value.get(key) != expected:
            raise VerificationError(f"v3 recovery receipt has wrong {key}")
    _v1._parse_time(value.get("issued_at"), "receipt issued_at")
    if resolver != {"type": "external-protected", "resolver_reference": source}:
        raise VerificationError("v3 receipt resolver does not bind authority")
    artifacts = _v1._dict(value.get("artifact_sha256"), "receipt artifacts")
    exact_artifacts = dict(expected_artifacts)
    if PREDECESSOR_RECEIPT_PATH in exact_artifacts:
        raise VerificationError("Stage-1 modifies the retained v2 receipt")
    exact_artifacts[PREDECESSOR_RECEIPT_PATH] = PREDECESSOR_RECEIPT_SHA256
    if artifacts != exact_artifacts:
        raise VerificationError("v3 receipt artifact keys or values differ")


def _require_recovery_protection_contract(
    before: Mapping[str, object],
    during: Mapping[str, object],
    *,
    base_sha: str,
) -> None:
    expected_by_phase = {
        "before": {
            (name, _v1.REQUIRED_APP_ID)
            for name in _v1.FULL_REQUIRED_CHECKS
        },
        "during": {
            (name, _v1.REQUIRED_APP_ID) for name in CONTROL_REQUIRED_CHECKS
        },
    }
    for label, protection in (("before", before), ("during", during)):
        _v1._validate_protection_shape(protection, f"protection_{label}")
        if protection.get("main_sha") != base_sha:
            raise VerificationError(
                f"protection {label} main SHA differs from PR base"
            )
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
        if any(protection.get(key) is not True for key in required_true):
            raise VerificationError(
                f"protection {label} weakens a retained invariant"
            )
        if protection.get("dismiss_stale_reviews") is not False:
            raise VerificationError(
                f"protection {label} enables stale-review dismissal"
            )
        checks = _v1._list(
            protection.get("required_checks"),
            f"{label} required checks",
        )
        expected_checks = expected_by_phase[label]
        if (
            _v1._required_check_set(protection) != expected_checks
            or len(checks) != len(expected_checks)
        ):
            raise VerificationError(
                f"protection {label} has the wrong exact required-check set"
            )
    if (
        _v1._protection_state_without_phase_fields(before)
        != _v1._protection_state_without_phase_fields(during)
    ):
        raise VerificationError(
            "recovery transaction changes a non-temporary protection setting"
        )
    if _v1._parse_time(
        before["observed_at"], "before observed_at"
    ) > _v1._parse_time(during["observed_at"], "during observed_at"):
        raise VerificationError("protection captures are not chronological")


def _install_v3_bindings() -> None:
    if not hasattr(_v1, "_validate_binding_v3_original"):
        _v1._validate_binding_v3_original = _v1._validate_binding
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
        "TEMPORARY_NONREQUIRED_CHECK": TEMPORARY_NONREQUIRED_CHECK,
        "REQUIRED_CHECKS": CONTROL_REQUIRED_CHECKS,
        "authorization_binding": authorization_binding,
        "_validate_binding": _validate_binding,
        "_validate_receipt": _validate_receipt,
        "_require_protection_contract": _require_recovery_protection_contract,
    }
    for name, replacement in replacements.items():
        setattr(_v1, name, replacement)


_install_v3_bindings()
_original_render_authorization_body = _v2._original_render_authorization_body
_original_build_authority_evidence = _v2._original_build_authority_evidence
_original_build_pre_merge_evidence = _v2._original_build_pre_merge_evidence
_original_render_completion_body = _v2._original_render_completion_body
_original_build_complete_evidence = _v2._original_build_complete_evidence
_original_verify_evidence_closure = _v2._original_verify_evidence_closure
_original_validate_complete_evidence = _v1.validate_complete_evidence


def _blob_sha256_at(repository: GitRepository, commit: str, path: str) -> str:
    return _sha256(repository.blob_at(commit, path))


def _path_exists_at(repository: GitRepository, commit: str, path: str) -> bool:
    return bool(
        repository._run(
            "ls-tree",
            "-z",
            "--full-tree",
            commit,
            "--",
            _v1._safe_path(path, "historical path"),
        )
    )


def validate_historical_git_objects(
    repository: GitRepository,
    authority: Mapping[str, object],
    pre_merge: Mapping[str, object] | None = None,
    complete: Mapping[str, object] | None = None,
) -> None:
    stage1 = _v1._dict(authority.get("stage1"), "stage1")
    recomputed = _v1._stage1_record(
        repository,
        str(stage1.get("base_sha")),
        str(stage1.get("commit_sha")),
    )
    if recomputed != stage1:
        raise VerificationError("historical v3 Stage-1 Git objects differ")
    base_sha = str(stage1["base_sha"])
    stage1_sha = str(stage1["commit_sha"])
    if base_sha != CONTROL_BASE_SHA:
        raise VerificationError("v3 Stage-1 base is not the exact v2 control squash")
    for path, expected_sha256 in RETAINED_PREDECESSOR_ARTIFACTS.items():
        for phase, commit_sha in (("base", base_sha), ("Stage-1", stage1_sha)):
            if _blob_sha256_at(repository, commit_sha, path) != expected_sha256:
                raise VerificationError(
                    f"historical {phase} retained v2 artifact hash differs: {path}"
                )
    if _blob_sha256_at(repository, base_sha, BOUNDARY_PATH) != PREDECESSOR_BOUNDARY_SHA256:
        raise VerificationError("historical v3 base boundary hash differs")
    changed_paths = {
        str(item.get("path"))
        for item in _v1._list(stage1.get("changed_files"), "Stage-1 files")
        if isinstance(item, Mapping)
    }
    if changed_paths != STAGE1_CONTROL_PATHS:
        missing = sorted(STAGE1_CONTROL_PATHS - changed_paths)
        extra = sorted(changed_paths - STAGE1_CONTROL_PATHS)
        raise VerificationError(
            f"v3 Stage-1 path contract differs; missing={missing}, extra={extra}"
        )
    if RECEIPT_PATH in changed_paths:
        raise VerificationError("v3 Stage-1 contains the recovery receipt")
    if any(_path_exists_at(repository, stage1_sha, path) for path in CLOSURE_EVIDENCE_PATHS):
        raise VerificationError("v3 Stage-1 contains premature recovery reports")
    if any(
        _path_exists_at(repository, stage1_sha, path)
        for path in PREDECESSOR_CLOSURE_EVIDENCE_PATHS
    ):
        raise VerificationError("v3 Stage-1 contains failed v2 closure reports")
    boundary_changes = [
        item
        for item in stage1["changed_files"]
        if item.get("path") == BOUNDARY_PATH
    ]
    if len(boundary_changes) != 1 or boundary_changes[0].get("status") != "M":
        raise VerificationError("v3 Stage-1 does not append the recovery boundary")
    old_boundary = repository.blob(str(boundary_changes[0].get("old_oid")))
    if _sha256(old_boundary) != PREDECESSOR_BOUNDARY_SHA256:
        raise VerificationError("v3 Stage-1 old boundary blob differs")
    if pre_merge is None:
        return
    pull = _v1._dict(pre_merge.get("final_pull_request"), "final pull request")
    materialization = _v1._dict(
        pre_merge.get("receipt_materialization"), "receipt materialization"
    )
    final_head = str(pull.get("head_sha"))
    final_commit = repository.commit(final_head)
    if final_commit["parents"] != [stage1_sha]:
        raise VerificationError("v3 final head is not the receipt-only child")
    if materialization.get("commit_sha") != final_head:
        raise VerificationError("v3 receipt materialization names another head")
    delta = repository.changed_files(stage1_sha, final_head)
    if (
        len(delta) != 1
        or delta[0].get("path") != RECEIPT_PATH
        or delta[0].get("status") != "A"
    ):
        raise VerificationError("v3 final head is not the exact receipt delta")
    if complete is None:
        return
    merged = _v1._dict(complete.get("merged_pull_request"), "merged pull request")
    merge = _v1._dict(complete.get("merge"), "merge proof")
    merge_sha = str(merged.get("merge_commit_sha"))
    merge_commit = repository.commit(merge_sha)
    if merge_commit["parents"] != [base_sha]:
        raise VerificationError("v3 squash parent differs from control base")
    if merge_commit["tree"] != final_commit["tree"]:
        raise VerificationError("v3 squash tree differs from final head")
    if merge.get("tree_oid") != merge_commit["tree"]:
        raise VerificationError("v3 complete evidence names another tree")


def validate_pending_receipt_child(
    repository: GitRepository,
    receipt: Mapping[str, object],
) -> dict[str, object]:
    stage1_sha = str(receipt.get("accepted_commit"))
    stage1_commit = repository.commit(stage1_sha)
    parents = stage1_commit.get("parents")
    if not isinstance(parents, list) or parents != [CONTROL_BASE_SHA]:
        raise VerificationError("v3 Stage-1 is not a child of the control base")
    authority: dict[str, object] = {
        "stage1": _v1._stage1_record(
            repository,
            CONTROL_BASE_SHA,
            stage1_sha,
        ),
        "authorization_comment": {
            "html_url": receipt.get("source_reference"),
            "body_utf8_sha256": receipt.get("approval_text_sha256"),
        },
    }
    validate_historical_git_objects(repository, authority)
    head_sha = repository._run("rev-parse", "--verify", "HEAD").decode(
        "ascii"
    ).strip()
    head = repository.commit(head_sha)
    final_head = head_sha
    if head["parents"] != [stage1_sha]:
        parents = head.get("parents")
        if (
            not isinstance(parents, list)
            or len(parents) != 2
            or parents[0] != CONTROL_BASE_SHA
        ):
            raise VerificationError("HEAD is not the v3 receipt-only child")
        pull_head = repository.commit(str(parents[1]))
        if pull_head["parents"] != [stage1_sha] or pull_head["tree"] != head["tree"]:
            raise VerificationError("HEAD is not the v3 receipt-only child")
        final_head = str(pull_head["sha"])
    delta = repository.changed_files(stage1_sha, final_head)
    if (
        len(delta) != 1
        or delta[0].get("path") != RECEIPT_PATH
        or delta[0].get("status") != "A"
    ):
        raise VerificationError("HEAD is not the exact v3 receipt-only child")
    receipt_blob = repository.blob_at(final_head, RECEIPT_PATH)
    try:
        committed = json.loads(receipt_blob.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise VerificationError("committed v3 receipt is not UTF-8 JSON") from exc
    if committed != receipt:
        raise VerificationError("loaded v3 receipt differs from HEAD bytes")
    expected_artifacts = {
        str(receipt.get("source_reference", "")).removeprefix("https://"): str(
            receipt.get("approval_text_sha256", "")
        )
    }
    stage1 = _v1._dict(authority["stage1"], "stage1")
    for item in _v1._list(stage1.get("changed_files"), "Stage-1 files"):
        changed = _v1._dict(item, "Stage-1 file")
        expected_artifacts[str(changed["path"])] = str(
            changed["new_blob_sha256"]
        )
    _validate_receipt(
        committed,
        authority=authority,
        expected_artifacts=expected_artifacts,
    )
    return {
        "stage1_sha": stage1_sha,
        "final_head_sha": final_head,
        "receipt_sha256": _sha256(receipt_blob),
    }


def validate_pre_merge_evidence(evidence: object) -> None:
    """Validate v3 check evidence without inheriting v1's literal run count.

    V1 and v2 keep their immutable two-run rule.  V3 validates the names and
    cardinality from its own exact ``CONTROL_REQUIRED_CHECKS`` tuple so a
    future version cannot silently contradict its configured check set.
    """

    value = _v1._dict(evidence, "pre-merge evidence")
    _v1._exact_keys(
        value,
        {
            "schema_version",
            "transaction_id",
            "phase",
            "repository",
            "authority_evidence_sha256",
            "authorization_comment",
            "final_pull_request",
            "receipt_materialization",
            "required_check_runs",
            "protection_before",
            "protection_during",
            "raw_artifacts",
        },
        "pre-merge evidence",
    )
    if (
        value.get("schema_version") != 1
        or value.get("transaction_id") != TRANSACTION_ID
        or value.get("phase") != "pre-merge"
    ):
        raise VerificationError("pre-merge evidence identity is wrong")
    _v1._validate_repository_shape(value.get("repository"))
    _v1._sha256_value(
        value.get("authority_evidence_sha256"), "authority evidence hash"
    )
    _v1._validate_comment_shape(
        value.get("authorization_comment"), "pre-merge authorization comment"
    )
    pull = _v1._dict(value.get("final_pull_request"), "final pull request")
    _v1._exact_keys(
        pull,
        {
            "number",
            "base_ref",
            "base_sha",
            "head_ref",
            "head_sha",
            "head_repository",
            "deterministic_patch_sha256",
            "github_patch_sha256",
            "changed_files_manifest_sha256",
        },
        "final pull request",
    )
    receipt = _v1._dict(
        value.get("receipt_materialization"), "receipt materialization"
    )
    _v1._exact_keys(
        receipt,
        {
            "commit_sha",
            "parent_sha",
            "tree_oid",
            "receipt_path",
            "receipt_sha256",
            "delta_patch_sha256",
            "delta",
        },
        "receipt materialization",
    )
    _v1._validate_changed_manifest(receipt.get("delta"), "receipt delta")
    if len(receipt["delta"]) != 1 or receipt["receipt_path"] != RECEIPT_PATH:
        raise VerificationError("receipt materialization is not exact")
    runs = _v1._list(value.get("required_check_runs"), "required check runs")
    expected_names = set(CONTROL_REQUIRED_CHECKS)
    if (
        {item.get("name") for item in runs if isinstance(item, dict)}
        != expected_names
        or len(runs) != len(CONTROL_REQUIRED_CHECKS)
    ):
        raise VerificationError(
            "pre-merge evidence lacks the configured exact required checks"
        )
    for index, item in enumerate(runs):
        run = _v1._dict(item, f"required check runs[{index}]")
        _v1._exact_keys(
            run,
            {"name", "app_id", "head_sha", "status", "conclusion"},
            f"required check runs[{index}]",
        )
        if (
            run.get("name") not in CONTROL_REQUIRED_CHECKS
            or run.get("app_id") != _v1.REQUIRED_APP_ID
            or run.get("head_sha") != pull.get("head_sha")
            or run.get("status") != "completed"
            or run.get("conclusion") != "success"
        ):
            raise VerificationError(
                f"required check runs[{index}] does not bind a successful "
                "final-head run"
            )
    before = _v1._dict(value.get("protection_before"), "protection before")
    during = _v1._dict(value.get("protection_during"), "protection during")
    _require_recovery_protection_contract(
        before,
        during,
        base_sha=str(pull["base_sha"]),
    )
    raw = _v1._dict(value.get("raw_artifacts"), "pre-merge raw_artifacts")
    _v1._exact_keys(
        raw,
        {
            "pull_request",
            "github_patch",
            "check_runs",
            "protection_before",
            "protection_during",
        },
        "pre-merge raw_artifacts",
    )
    for key in sorted(raw):
        _v1._validate_artifact_ref(
            raw[key], f"pre-merge raw_artifacts.{key}"
        )


# The loaded v1 module is private to this v3 adapter.  Replacing its validator
# here cannot alter the standalone immutable v1/v2 modules or their semantics.
_v1.validate_pre_merge_evidence = validate_pre_merge_evidence


def _require_completion_comment_deadline(
    merged_pull_request: Mapping[str, object],
    completion_comment: Mapping[str, object],
) -> None:
    """Bind the restoration deadline to GitHub's authenticated comment clock."""

    merged_at = _v1._parse_time(
        merged_pull_request.get("merged_at"),
        "merged pull request merged_at",
    )
    comment_at = _v1._parse_time(
        completion_comment.get("created_at"),
        "completion comment created_at",
    )
    delay = (comment_at - merged_at).total_seconds()
    if delay < 0 or delay > _v1.MAX_RESTORE_DELAY_SECONDS:
        raise VerificationError(
            "authenticated completion comment is outside the 600-second merge window"
        )


def validate_complete_evidence(evidence: object) -> None:
    """Retain v1 structure and add v3's authenticated completion deadline."""

    _original_validate_complete_evidence(evidence)
    value = _v1._dict(evidence, "complete evidence")
    _require_completion_comment_deadline(
        _v1._dict(
            value.get("merged_pull_request"),
            "complete merged pull request",
        ),
        _v1._dict(
            value.get("completion_comment"),
            "completion comment",
        ),
    )


# These replacements affect only the private v1 module loaded inside v3.
_v1.validate_complete_evidence = validate_complete_evidence


def render_authorization_body(
    repository: GitRepository,
    base: str,
    stage1: str,
) -> str:
    authority = {"stage1": _v1._stage1_record(repository, base, stage1)}
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
    authority = {
        "stage1": _v1._stage1_record(
            repository,
            CONTROL_BASE_SHA,
            stage1_sha,
        )
    }
    validate_historical_git_objects(repository, authority)
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
    protection_github: GitHubReader | None = None,
    verify_live_during: bool = True,
    now: datetime | None = None,
) -> dict[str, object]:
    validate_historical_git_objects(repository, authority)
    if verify_live_during and protection_github is None:
        raise VerificationError(
            "v3 pre-merge requires the Administration-read protection reader"
        )
    evidence = _original_build_pre_merge_evidence(
        repository,
        github,
        authority,
        protection_before_capture,
        protection_during_capture,
        verify_live_during=False,
        now=now,
    )
    if verify_live_during:
        _same_live_protection(
            github,
            _v1._dict(evidence["protection_during"], "protection during"),
            protection_github=protection_github,
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
    protection_github: GitHubReader | None = None,
    verify_live_after: bool = True,
    now: datetime | None = None,
) -> str:
    authority = {
        "stage1": _v1._stage1_record(
            repository,
            str(_v1._dict(pre_merge["final_pull_request"], "pull")["base_sha"]),
            str(
                _v1._dict(
                    pre_merge["receipt_materialization"], "receipt"
                )["parent_sha"]
            ),
        )
    }
    validate_historical_git_objects(repository, authority, pre_merge)
    if verify_live_after and protection_github is None:
        raise VerificationError(
            "v3 completion rendering requires the Administration-read protection reader"
        )
    rendered = _original_render_completion_body(
        repository,
        github,
        pre_merge,
        protection_after_capture,
        verify_live_after=False,
        now=now,
    )
    if verify_live_after:
        expected, _raw = _v1._validate_protection_capture(
            protection_after_capture
        )
        _same_live_protection(
            github,
            expected,
            protection_github=protection_github,
            now=now,
        )
    return rendered


def build_complete_evidence(
    repository: GitRepository,
    github: GitHubReader,
    pre_merge: Mapping[str, object],
    protection_after_capture: Mapping[str, object],
    *,
    completion_comment_id: int,
    protection_github: GitHubReader | None = None,
    verify_live_after: bool = True,
    now: datetime | None = None,
) -> dict[str, object]:
    authority = {
        "stage1": _v1._stage1_record(
            repository,
            str(_v1._dict(pre_merge["final_pull_request"], "pull")["base_sha"]),
            str(
                _v1._dict(
                    pre_merge["receipt_materialization"], "receipt"
                )["parent_sha"]
            ),
        )
    }
    validate_historical_git_objects(repository, authority, pre_merge)
    if verify_live_after and protection_github is None:
        raise VerificationError(
            "v3 completion requires the Administration-read protection reader"
        )
    evidence = _original_build_complete_evidence(
        repository,
        github,
        pre_merge,
        protection_after_capture,
        completion_comment_id=completion_comment_id,
        verify_live_after=False,
        now=now,
    )
    if verify_live_after:
        _same_live_protection(
            github,
            _v1._dict(evidence["protection_after"], "protection after"),
            protection_github=protection_github,
            now=now,
        )
    validate_historical_git_objects(repository, authority, pre_merge, evidence)
    return evidence


def _install_v3_phase_guards() -> None:
    _v1.render_authorization_body = render_authorization_body
    _v1.build_authority_evidence = build_authority_evidence
    _v1.build_pre_merge_evidence = build_pre_merge_evidence
    _v1.render_completion_body = render_completion_body
    _v1.build_complete_evidence = build_complete_evidence


_install_v3_phase_guards()
validate_authority_evidence = _v1.validate_authority_evidence
validate_pre_merge_evidence = _v1.validate_pre_merge_evidence
validate_complete_evidence = _v1.validate_complete_evidence


class _CachingReader:
    """Keep one internally consistent API observation per reader and path."""

    def __init__(self, delegate: GitHubReader) -> None:
        self._delegate = delegate
        self._json: dict[str, APIResult] = {}
        self._bytes: dict[tuple[str, str], APIResult] = {}

    def get_json(self, path: str) -> APIResult:
        if path not in self._json:
            self._json[path] = self._delegate.get_json(path)
        return self._json[path]

    def get_bytes(self, path: str, *, accept: str) -> APIResult:
        key = (path, accept)
        if key not in self._bytes:
            self._bytes[key] = self._delegate.get_bytes(path, accept=accept)
        return self._bytes[key]


def capture_protection(
    github: GitHubReader,
    *,
    observed_at: datetime | None = None,
    protection_github: GitHubReader | None = None,
) -> dict[str, object]:
    """Capture one protection projection with an explicit mixed-reader split.

    The ordinary reader proves the public main-branch identity.  The protected
    reader supplies every administration-sensitive field: classic protection,
    repository merge settings, and rulesets.
    """

    if protection_github is None:
        raise VerificationError(
            "v3 protection capture requires the Administration-read reader"
        )
    protected_reader = protection_github
    branch = github.get_json(f"/repos/{REPOSITORY}/branches/main")
    protection = protected_reader.get_json(
        f"/repos/{REPOSITORY}/branches/main/protection"
    )
    repository = protected_reader.get_json(f"/repos/{REPOSITORY}")
    rulesets = protected_reader.get_json(
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


def sanitized_mismatch_fields(
    live: Mapping[str, object],
    recorded: Mapping[str, object],
) -> list[str]:
    """Return only differing normalized field names, never their values."""

    keys = (set(live) | set(recorded)) - _VOLATILE_PROTECTION_FIELDS
    return sorted(key for key in keys if live.get(key) != recorded.get(key))


def _same_live_protection(
    github: GitHubReader,
    expected: Mapping[str, object],
    *,
    protection_github: GitHubReader | None,
    now: datetime | None = None,
) -> None:
    """Recheck protection with explicit reader separation and safe errors."""

    if protection_github is None:
        raise VerificationError(
            "v3 live protection recheck requires the Administration-read reader"
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


def _closure_inputs(
    args: tuple[object, ...],
    kwargs: Mapping[str, object],
) -> tuple[GitRepository, GitHubReader, str, str, GitHubReader, datetime | None]:
    repository = args[0] if len(args) > 0 else kwargs.get("repository")
    github = args[1] if len(args) > 1 else kwargs.get("github")
    base_sha = kwargs.get("base_sha")
    head_sha = kwargs.get("head_sha")
    protection_github = kwargs.get("protection_github")
    now = kwargs.get("now")
    if not isinstance(repository, GitRepository):
        raise VerificationError("v3 closure requires the pinned GitRepository")
    if github is None or protection_github is None:
        raise VerificationError("v3 closure requires both trusted GitHub readers")
    if not isinstance(base_sha, str) or not isinstance(head_sha, str):
        raise VerificationError("v3 closure requires exact base and head SHAs")
    if now is not None and not isinstance(now, datetime):
        raise VerificationError("v3 closure time is invalid")
    return repository, github, base_sha, head_sha, protection_github, now


def verify_evidence_closure(*args: object, **kwargs: object) -> dict[str, object]:
    """Verify v2 evidence with one cached, mixed-token protection projection."""

    (
        repository,
        github,
        base_sha,
        head_sha,
        protection_github,
        now,
    ) = _closure_inputs(args, kwargs)
    ordinary = _CachingReader(github)
    administration = _CachingReader(protection_github)

    try:
        complete = json.loads(
            repository.blob_at(head_sha, COMPLETE_EVIDENCE_PATH).decode("utf-8")
        )
    except (UnicodeDecodeError, json.JSONDecodeError, OSError) as exc:
        raise VerificationError("v3 closure complete report is invalid") from exc
    expected = _v1._dict(
        complete.get("protection_after"),
        "complete protection after",
    )
    live_capture = capture_protection(
        ordinary,
        observed_at=now,
        protection_github=administration,
    )
    live, _raw = _v1._validate_protection_capture(live_capture)
    mismatch_fields = sanitized_mismatch_fields(live, expected)
    if mismatch_fields:
        raise VerificationError(
            "live restored protection mismatch fields: "
            + ",".join(mismatch_fields)
        )

    original_capture = _v1.capture_protection
    _v1.capture_protection = capture_protection
    try:
        result = _original_verify_evidence_closure(
            repository,
            ordinary,
            base_sha=base_sha,
            head_sha=head_sha,
            protection_github=administration,
            now=now,
        )
    finally:
        _v1.capture_protection = original_capture
    authority = json.loads(
        repository.blob_at(head_sha, AUTHORITY_EVIDENCE_PATH).decode("utf-8")
    )
    pre_merge = json.loads(
        repository.blob_at(head_sha, PRE_MERGE_EVIDENCE_PATH).decode("utf-8")
    )
    validate_historical_git_objects(
        repository,
        authority,
        pre_merge,
        complete,
    )
    result["historical_git_object_validation"] = {
        "stage1_commit_sha": authority["stage1"]["commit_sha"],
        "control_merge_sha": complete["merged_pull_request"][
            "merge_commit_sha"
        ],
        "result": "PASS",
    }
    result["mixed_reader_recovery"] = {
        "ordinary_reader_fields": [
            "branch_identity",
            "check_runs",
            "comments",
            "pull_request",
            "repository_identity",
        ],
        "administration_reader_fields": [
            "branch_protection",
            "repository_merge_settings",
            "rulesets",
        ],
        "mismatch_diagnostics": "normalized-field-names-only",
        "result": "PASS",
    }
    return result


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
            administration = _v1._reader(
                args.protection_token_env,
                required=True,
            )
            _v1._write_json(
                args.output,
                capture_protection(
                    github,
                    protection_github=administration,
                ),
            )
        elif args.command == "pre-merge":
            administration = _v1._reader(
                args.protection_token_env,
                required=True,
            )
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
            administration = _v1._reader(
                args.protection_token_env,
                required=True,
            )
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
            administration = _v1._reader(
                args.protection_token_env,
                required=True,
            )
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
            administration = _v1._reader(
                args.protection_token_env,
                required=True,
            )
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
