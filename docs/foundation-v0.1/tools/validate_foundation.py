#!/usr/bin/env python3
"""Validate the foundation pack without third-party dependencies."""

from __future__ import annotations

import json
import hashlib
import re
import stat
import subprocess
import sys
import tomllib
from collections import Counter
from datetime import date, datetime, timedelta, timezone
from pathlib import Path

from validate_wp0001_pre_a1_readiness import validate_wp0001_pre_a1_readiness
from validate_wp0001_mcp_live import (
    EXPECTED_CHECK_NAMES as WP0001_MCP_LIVE_CHECKS,
    VALIDATOR_VERSION as WP0001_MCP_LIVE_VALIDATOR_VERSION,
    client_arguments_policy_safe,
    code_identity_matches,
    editor_arguments_policy_safe,
    route_contract as wp0001_mcp_route_contract,
    session_identity_sha256 as wp0001_session_identity_sha256,
)


ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
SYSTEM_GIT = "/usr/bin/git"
FOUNDATION_GIT_ENV = {
    "HOME": "/var/empty",
    "XDG_CONFIG_HOME": "/var/empty",
    "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
    "LANG": "C",
    "LC_ALL": "C",
    "GIT_CONFIG_NOSYSTEM": "1",
    "GIT_CONFIG_GLOBAL": "/dev/null",
    "GIT_OPTIONAL_LOCKS": "0",
    "GIT_NO_REPLACE_OBJECTS": "1",
    "GIT_NO_LAZY_FETCH": "1",
    "GIT_TERMINAL_PROMPT": "0",
    "GIT_PAGER": "",
    "PAGER": "",
}
FOUNDATION_GIT_ARGS = (
    "--no-replace-objects",
    "-c",
    "core.fsmonitor=false",
    "-c",
    "core.hooksPath=/dev/null",
    "-c",
    "maintenance.auto=false",
    "-c",
    "gc.auto=0",
    "--no-optional-locks",
)
DECISIONS = ROOT / "ledger" / "decisions.jsonl"
DECISION_SCHEMA = ROOT / "schemas" / "decision.schema.json"
SCENARIO_SCHEMA = ROOT / "schemas" / "scenario-definition.schema.json"
SCENARIO_REGISTRY = ROOT / "scenarios" / "registry.json"
SCENARIO_DEFINITIONS = ROOT / "scenarios" / "definitions"
SCENARIO_FIXTURES = ROOT / "scenarios" / "fixtures"
SCENARIO_ARTIFACTS = ROOT / "scenarios" / "artifacts"
SCENARIO_FIXTURE_SCHEMA = ROOT / "schemas" / "scenario-fixture-manifest.schema.json"
SCENARIO_ARTIFACT_SCHEMA = ROOT / "schemas" / "scenario-artifact.schema.json"
A1_BOUNDARY_SCHEMA = ROOT / "schemas" / "a1-boundary-manifest.schema.json"
LOCAL_A1_BOUNDARY_SCHEMA = ROOT / "schemas" / "local-a1-boundary.schema.json"
WP0002_WORKING_TREE_SCOPE_SCHEMA = (
    ROOT / "schemas" / "wp0002-working-tree-scope-capture.schema.json"
)
WP0002_LOCAL_OPERATOR_SCOPE_SCHEMA = (
    ROOT / "schemas" / "wp0002-local-operator-scope-capture.schema.json"
)
WP0002_SCOPE_COLLECTOR = (
    REPO_ROOT / "Tools" / "Validation" / "collect_wp0002_scope_capture.py"
)
WP0002_LOCAL_OPERATOR_ONLINE_VERIFIER = (
    REPO_ROOT
    / "Tools"
    / "Validation"
    / "verify_wp0002_local_operator_transaction.py"
)
WP0002_GITHUB_PROTECTION_SCHEMA = (
    ROOT / "schemas" / "wp0002-github-protection-capture.schema.json"
)
WP0002_EXTERNAL_POLICY_CAPTURE_SCHEMA = (
    ROOT / "schemas" / "wp0002-external-policy-capture.schema.json"
)
WP0002_LOCAL_OPERATOR_TRANSACTION_EVIDENCE_SCHEMA = (
    ROOT
    / "schemas"
    / "wp0002-local-operator-transaction-evidence.schema.json"
)
WP0001_A1_EVIDENCE_SCHEMA = (
    ROOT / "schemas" / "wp0001-a1-activation-evidence.schema.json"
)
WP0001_A1_EVIDENCE_RECORD_SCHEMA = (
    ROOT / "schemas" / "wp0001-a1-evidence-record.schema.json"
)
WP0001_UNITY_EPHEMERAL_SCRATCH_PATHS = (
    "Game/Library/",
    "Game/Temp/",
    "Game/Logs/",
    "Game/Obj/",
    "Game/UserSettings/",
    "Game/MemoryCaptures/",
    "Game/Recordings/",
)
WP0001_PROJECT_SEED_EVIDENCE_PATH = (
    "docs/evidence/WP-0001/a1-activation/project-seed.json"
)
WP0001_RUNTIME_CONFIG_EVIDENCE_PATH = (
    "docs/evidence/WP-0001/a1-activation/codex-runtime-config.toml"
)
WP0001_MCP_LIVE_VERIFIER_PATH = (
    "docs/foundation-v0.1/tools/validate_wp0001_mcp_live.py"
)
WP0001_QUARANTINE_LIVE_VERIFIER_PATH = (
    "docs/foundation-v0.1/tools/validate_wp0001_a1_live.py"
)
WP0001_ACTIVATION_EVIDENCE_PATHS = (
    "docs/evidence/WP-0001/a1-activation/evidence-manifest.json",
    WP0001_PROJECT_SEED_EVIDENCE_PATH,
    "docs/evidence/WP-0001/a1-activation/toolchain.json",
    "docs/evidence/WP-0001/a1-activation/entitlement-linkage.json",
    "docs/evidence/WP-0001/a1-activation/project-identity.json",
    "docs/evidence/WP-0001/a1-activation/quarantine.json",
    "docs/evidence/WP-0001/a1-activation/mcp-route.json",
    "docs/evidence/WP-0001/a1-activation/bridge-discovery.json",
    WP0001_RUNTIME_CONFIG_EVIDENCE_PATH,
    "docs/evidence/WP-0001/a1-activation/clean-handshake.json",
    "docs/evidence/WP-0001/a1-activation/activation-session.json",
    "docs/evidence/WP-0001/a1-activation/network-observation.json",
    "docs/evidence/WP-0001/a1-activation/deviations.json",
    "docs/evidence/WP-0001/a1-activation/sandbox.policy",
    "docs/evidence/WP-0001/a1-activation/network.policy",
)
WP0001_PRESERVED_DEVIATION_IDS = (
    "DEV-WP0001-PRE-D0051-UNITY-READ-CONSOLE",
    "DEV-WP0001-PRE-A1-EDITOR-VERSION-PROBE",
    "DEV-WP0001-PRE-A1-HUB-LIST-PROBE",
)
WP0001_ALWAYS_FORBIDDEN_MCP_TOOLS = {
    "Unity_RunCommand",
    "Unity_PackageManager_ExecuteAction",
    "Unity_ImportExternalModel",
}
WP0001_CLIENT_ABSENT_ENVIRONMENT_VARIABLES = (
    "AWS_ACCESS_KEY_ID",
    "AWS_SECRET_ACCESS_KEY",
    "AWS_SESSION_TOKEN",
    "GH_TOKEN",
    "GITHUB_TOKEN",
    "GIT_ASKPASS",
    "SSH_AUTH_SOCK",
    "VERCEL_TOKEN",
)
WP0001_REQUIRED_RAW_SOURCE_BY_KIND = {
    "mcp-route": (
        "docs/evidence/WP-0001/a1-activation/commands/mcp-route-live.json"
    ),
    "bridge-discovery": (
        "docs/evidence/WP-0001/a1-activation/commands/mcp-route-live.json"
    ),
    "clean-handshake": (
        "docs/evidence/WP-0001/a1-activation/commands/clean-handshake.raw.json"
    ),
    "activation-session": (
        "docs/evidence/WP-0001/a1-activation/commands/"
        "activation-session-live.json"
    ),
    "network-observation": (
        "docs/evidence/WP-0001/a1-activation/commands/network-probes.json"
    ),
}
WP0001_POLICY_ATTACHMENT_RAW_PATH = (
    "docs/evidence/WP-0001/a1-activation/commands/"
    "policy-attachment.json"
)
WP0001_REQUIRED_TOOLCHAIN_VERSIONS = {
    "Unity Hub": "3.19.5",
    "Unity Editor ARM64": "6000.3.19f1",
    "Mac Build Support (IL2CPP)": "6000.3.19f1",
    "Xcode": "26.3",
    ".NET SDK": "10.0.301",
    "com.unity.ai.assistant": "2.14.0-pre.1",
}
WP0001_LIVE_QUARANTINE_CHECKS = {
    "packet_is_wp0001",
    "candidate_root_is_canonical",
    "candidate_exists",
    "trusted_root_exists",
    "candidate_separate_from_trusted_root",
    "candidate_owner_uid_matches",
    "candidate_root_default_write_denied",
    "declared_writable_paths_exact_and_writable",
    "scratch_paths_exact_and_writable",
    "candidate_write_scope_exact",
    "independent_git_directory",
    "git_directory_owner_uid_matches",
    "no_shared_git_object_inodes",
    "git_directory_symlink_free",
    "candidate_worktree_symlink_free",
    "no_shared_worktree_inodes",
    "no_git_file_indirection",
    "no_alternates",
    "head_matches",
    "detached_head",
    "git_directory_is_candidate_dot_git",
    "git_common_directory_is_candidate_dot_git",
    "clean_worktree",
    "git_metadata_passive",
    "zero_remotes",
    "principal_uid_matches",
    "environment_bindings_match",
    "client_environment_guard_matches",
    "forbidden_credential_env_absent",
    "boot_session_matches",
    "trusted_root_not_writable",
    "creator_home_exists",
    "creator_home_not_writable",
    "runtime_home_exists_owned_private",
    "runtime_temp_exists_owned_private",
    "runtime_home_writable",
    "runtime_temp_writable",
    "shared_temp_roots_exist",
    "shared_temp_default_write_denied",
    "socket_exception_exact",
    "socket_exists_owned_0600",
    "socket_is_unix_domain_not_symlink",
    "sandbox_policy_hash_matches",
    "network_policy_hash_matches",
}

PACKET_CONTRACT_FIELDS = (
    "schema_version", "id", "class", "declared_risk", "save_risk", "created_on",
    "title", "objective", "value", "requested_by", "required_approver",
    "constitutional_links", "decision_links", "system_contracts", "baseline_evidence",
    "in_scope", "non_goals", "affected_domains", "interfaces", "dependencies",
    "declared_paths", "scenario_pins", "save_impact", "acceptance_tests",
    "performance_metrics", "visual_evidence", "rollout", "rollback",
)

PACKET_STATUSES = {
    "proposed", "accepted", "active", "verifying", "candidate", "released",
    "rejected", "rolled-back", "superseded",
}
WP0002_PACKAGE_GRAPH_BASE = "b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5"
WP0002_PACKAGE_GRAPH_CHECKER = (
    REPO_ROOT / "Tools" / "Validation" / "validate_wp0002_package_graph.py"
)
WP0002_PACKAGE_GRAPH_CHECKER_SHA256 = (
    "d9b36dd20099a18de940d833085f81f60a0478f527879633e21c2d28fdae5fd2"
)
WP0002_PACKAGE_GRAPH_CHECKER_CONTRACT = "wp0002-package-graph-v2"
WP0002_PACKAGE_GRAPH_PATHS = (
    "Game/Packages/manifest.json",
    "Game/Packages/packages-lock.json",
    "SimulationCore/package.json",
    "SaveContracts/package.json",
)
WP0002_RUNCOMMAND_RESIDUAL_CAPABILITY = {
    "acknowledged_capability": (
        "Unity_RunCommand compiles and executes arbitrary Editor C# with potential "
        "host filesystem, network, process, and package reach"
    ),
    "assistant_permissions_and_five_tool_allowlist_are_a_sandbox": False,
    "denied_actions_enforcement": "creator-authority-policy-only-not-os-enforcement",
    "direct_runcommand_prohibited_until_dispatcher_materialized_and_hash_bound": True,
    "dispatcher": {
        "contract": "wp0002-gate-dispatcher-v1",
        "repository_path": (
            "Game/Assets/AtomicLandPirate/LastBearing/Editor/"
            "WP0002GateDispatcher.cs"
        ),
        "allowed_gate_ids": [
            "asset-refresh-and-compile",
            "wp0002-editmode-test-assembly",
            "wp0002-playmode-test-assembly",
            "wp0002-technical-capture",
        ],
        "content_addressed_source_required_per_call": True,
        "required_log_fields": [
            "gate_id",
            "source_sha256",
            "command",
            "result",
            "started_at",
            "completed_at",
        ],
        "reject_non_enumerated_invocation": True,
    },
    "unattended_third_party_rollout_requires_tool": "Unity_WP0002_RunGate",
}
WP0002_PREVIOUS_BOUNDARY_SHA256 = (
    "770f46788ab927bc18638851b220b33e89adb3ee4d5dcfd08b82fcb587dbff52"
)
WP0002_LOCAL_OPERATOR_RECEIPT_ID = "RR-WP0002-LOCAL-OPERATOR-20260717"
WP0002_LOCAL_OPERATOR_CLAIM = (
    "AUTHORIZE-WP0002-DELEGATED-LOCAL-UNITY-OPERATOR"
)
WP0002_LOCAL_OPERATOR_MAX_RESTORE_DELAY_SECONDS = 600
WP0002_LOCAL_OPERATOR_SOURCE_PATTERN = re.compile(
    r"https://github\.com/AC-21/sasha-the-land-pirate/"
    r"(?:pull|issues)/[0-9]+#issuecomment-[0-9]+"
)
WP0002_LOCAL_OPERATOR_GOVERNANCE_PATH = (
    "governance/WP-0002-DELEGATED-LOCAL-UNITY-OPERATOR-AMENDMENT.md"
)
WP0002_CANONICAL_REPOSITORY_ROOT = (
    "/Users/sasha/Projects/sasha-the-land-pirate"
)
WP0002_CANONICAL_PROJECT_PATH = f"{WP0002_CANONICAL_REPOSITORY_ROOT}/Game"
WP0002_LOCAL_OPERATOR_SCOPE_URI = (
    "repo://docs/evidence/WP-0002/local-operator-amendment/"
    "scope-capture/working-tree-scope.json"
)
WP0002_SUCCESSOR_FIRST_USE_PRECONDITIONS = [
    (
        "creator-or-receipt-bound-delegated-local-operator-opened-or-switched-"
        "exact-game-project"
    ),
    "creator-confirmed-licensed-editor",
    "bridge-running",
    "exact-visible-receipt-bound-codex-client-identity-matched-and-approved",
    "target-matches-game",
    "requested-call-within-allowed-actions",
]
WP0002_ACTIVATION_PROTECTED_PATHS = [
    "AGENTS.md",
    ".codex/config.toml",
    "Game/ProjectSettings/ProjectSettings.asset",
    "Game/ProjectSettings/SceneTemplateSettings.json",
    "docs/foundation-v0.1/",
    "docs/foundation-v0.1/governance/",
    "docs/foundation-v0.1/ledger/receipts/",
    "Tools/Validation/validate_wp0002_package_graph.py",
    "Tools/Validation/validate_wp0002_entry_gate.py",
    "Tools/Validation/validate_wp0002_policy.py",
    "Tools/Validation/collect_wp0002_scope_capture.py",
    ".github/workflows/wp0002-ci.yml",
    ".github/workflows/wp0002-policy.yml",
    "SimulationCore/README.md",
    "SaveContracts/README.md",
    "SimulationCore/package.json",
    "SaveContracts/package.json",
]
WP0002_LOCAL_OPERATOR_TRANSACTION_EVIDENCE_CONTRACT = {
    "schema_path": (
        "docs/foundation-v0.1/schemas/"
        "wp0002-local-operator-transaction-evidence.schema.json"
    ),
    "online_verifier_path": (
        "Tools/Validation/verify_wp0002_local_operator_transaction.py"
    ),
    "online_authentication_required": True,
    "offline_validation_scope": (
        "structural-and-content-consistency-only-not-live-github-authentication"
    ),
    "phases": ["authority", "pre-merge", "complete"],
    "authority_comment_policy": (
        "refetch-and-reject-id-actor-association-updated-at-or-body-hash-drift"
    ),
    "normalized_extra_or_missing_keys_rejected": True,
    "raw_github_responses_hash_bound": True,
    "receipt_artifact_key_policy": (
        "exact-stage1-changed-file-set-plus-authenticated-authority-comment"
    ),
    "receipt_delta_policy": "exactly-one-added-regular-receipt-file",
    "protection_evidence_phases": ["before", "during", "after"],
    "repository_ruleset_policy": (
        "exact-empty-inventory-before-during-after"
    ),
    "pending_without_repository_evidence": (
        "valid-control-materialization-but-successor-non-executable"
    ),
    "partial_repository_evidence_rejected": True,
    "evidence_closure_policy": (
        "separate-protected-evidence-only-pr-after-control-squash-restoration-"
        "and-completion-comment"
    ),
    "evidence_closure_delta_policy": (
        "exact-three-regular-added-report-files-and-no-other-delta"
    ),
    "evidence_closure_required_checks": [
        "validate",
        "wp0002-core",
        "wp0002-policy",
    ],
    "evidence_closure_online_reverification": (
        "base-owned-wp0002-policy-refetches-owner-comments-control-pr-main-"
        "checks-and-restored-protection"
    ),
    "closure_verifier_execution": (
        "protected-base-verifier-sha256-checked-before-execution-no-candidate-code"
    ),
    "closure_protection_authentication": (
        "base-owned-workflow-single-repository-short-expiry-administration-read-"
        "secret-never-candidate-code"
    ),
    "maximum_restoration_delay_seconds": (
        WP0002_LOCAL_OPERATOR_MAX_RESTORE_DELAY_SECONDS
    ),
    "successor_authority_condition": (
        "all-three-reports-validated-and-evidence-closure-merged-to-protected-main"
    ),
    "completion_required_before_successor_authority": True,
    "protected_transaction_paths": [
        "Tools/Validation/verify_wp0002_local_operator_transaction.py"
    ],
    "repository_evidence_paths": {
        "authority": (
            "docs/evidence/WP-0002/local-operator-amendment/authority.json"
        ),
        "pre-merge": (
            "docs/evidence/WP-0002/local-operator-amendment/pre-merge.json"
        ),
        "complete": (
            "docs/evidence/WP-0002/local-operator-amendment/complete.json"
        ),
    },
}
WP0002_BOUNDARY_AMENDMENTS = [
    {
        "amendment_id": "A1B-WP-0002-LOCAL-OPERATOR-20260717",
        "amendment_kind": "append-only-delegated-local-operator",
        "previous_boundary_sha256": WP0002_PREVIOUS_BOUNDARY_SHA256,
        "authorization_receipt_id": WP0002_LOCAL_OPERATOR_RECEIPT_ID,
        "required_claim": WP0002_LOCAL_OPERATOR_CLAIM,
        "governance_record": WP0002_LOCAL_OPERATOR_GOVERNANCE_PATH,
        "packet_contract_changed": False,
        "materialization_control": {
            "classification": (
                "creator-controlled-control-plane-configuration-not-a1-"
                "implementation-authority"
            ),
            "temporary_nonrequired_check": "wp0002-policy",
            "exact_final_pr_head_and_patch_binding_required": True,
            "retained_required_checks": ["validate", "wp0002-core"],
            "retained_protections": [
                "strict-up-to-date",
                "pull-request-required",
                "enforce-admins",
                "conversation-resolution-required",
                "linear-history-required",
                "no-bypass-allowances",
                "force-push-disabled",
                "deletion-disabled",
                "squash-only",
            ],
            "creator_delegated_squash_merge_only": True,
            "restore_wp0002_policy_immediately_after_merge": True,
            "fresh_canary_pr": "first-post-restoration-implementation-pr",
            "general_protection_bypass_authorized": False,
        },
    }
]
WP0002_DELEGATED_LOCAL_UNITY_OPERATOR = {
    "authorization_receipt_id": WP0002_LOCAL_OPERATOR_RECEIPT_ID,
    "effective_authority_condition": (
        "all-three-transaction-reports-validated-in-protected-main-after-"
        "evidence-closure-pr"
    ),
    "control_pr_merge_alone_authorizes_actions": False,
    "interaction_route": "computer-use-visible-ui-only",
    "repository_root": WP0002_CANONICAL_REPOSITORY_ROOT,
    "project_path": WP0002_CANONICAL_PROJECT_PATH,
    "authorized_application_bundles": [
        {
            "bundle_id": "com.unity3d.unityhub",
            "bundle_path": "/Applications/Unity Hub.app",
            "role": "project-add-open-switch",
        },
        {
            "bundle_id": "com.unity3d.UnityEditor5.x",
            "bundle_path": (
                "/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app"
            ),
            "role": "project-open-bridge-approval-and-inspection",
        },
    ],
    "authorized_actions": [
        "add-exact-canonical-project-to-unity-hub",
        "open-or-switch-to-exact-canonical-project-in-unity-hub-or-editor",
        "approve-or-reapprove-trusted-codex-mcp-client-in-visible-unity-bridge-ui",
        (
            "inspect-visible-non-secret-seat-organization-project-editor-package-"
            "bridge-tool-console-and-hierarchy-state"
        ),
    ],
    "trusted_codex_client_identity": {
        "approval_authority": (
            "bridge-visible-os-publisher-and-path-plus-receipt-bound-prior-"
            "host-observation"
        ),
        "visible_bridge_match": {
            "client_label": "codex-mcp-client",
            "publisher_display_name": "OpenAI OpCo, LLC",
            "executable_path": (
                "/Applications/ChatGPT.app/Contents/Resources/codex"
            ),
            "connection_state": "Accepted",
            "all_fields_required": True,
        },
        "application_version_match": {
            "path": "/Applications/ChatGPT.app",
            "bundle_identifier": "com.openai.codex",
            "version": "26.707.91948",
            "build": "5440",
            "all_fields_required": True,
        },
        "prior_host_signature_observation": {
            "evidence_path": (
                "docs/evidence/WP-0001/"
                "COMPONENT-SIGNATURE-RECHECK-20260716.md"
            ),
            "observation_scope": (
                "context-limited-a0-read-only-host-diagnostic-not-authoritative-"
                "a1-verification"
            ),
            "application_cdhash_observed": (
                "3972f0bc0675d00e71d20be5009b5b5c22b3d905"
            ),
            "bridge_cdhash_observed": (
                "398aca71386fdc89bd7a9e30cceefe36764c3809"
            ),
            "bridge_sha256_observed": (
                "bdcb530615d44fcc7b35d12fe00f30c3025c25fc22a21193591dcdb064304385"
            ),
            "team_identifier": "2DC432GLL2",
            "designated_requirement": (
                "identifier codex and anchor apple generic and certificate "
                "1[field.1.2.840.113635.100.6.2.6] exists and certificate "
                "leaf[field.1.2.840.113635.100.6.1.13] exists and certificate "
                "leaf[subject.OU] = \"2DC432GLL2\""
            ),
            "signing_context_gap_resolved": False,
        },
        "visible_ui_proves_cdhash_or_designated_requirement": False,
        "current_approval_condition": (
            "all-visible-bridge-fields-and-application-version-build-match-"
            "receipt-bound-prior-observation"
        ),
        "update_policy": (
            "any-visible-field-or-version-build-drift-denies-approval-until-"
            "new-receipt-and-re-attestation"
        ),
    },
    "existing_cloud_project_linkage": {
        "cloud_project_id": "b2f6f654-8c39-4360-bc5e-26a62e50e159",
        "cloud_project_id_sha256": (
            "0bc8f812dc0be6e99edcea952518fe09437762f5353d450c76e0e62992ab56e1"
        ),
        "organization_id": "unity_2d2aeb94bdf989c70701",
        "organization_id_sha256": (
            "9b502c7e60d87721337fb9836a5bcfbaa6624d472e480de7ecbb328bf97d622a"
        ),
        "identifiers_are_non_secret": True,
        "cloud_enabled_asserted": False,
        "account_or_cloud_configuration_change_authorized": False,
    },
    "creator_delegation_limits": {
        "creator_presence_required_for_repeated_exact_actions": False,
        "identity_impersonation_authorized": False,
        "external_communication_or_creator_commitment_authorized": False,
        "new_authority_inference_authorized": False,
        "action_attribution": "creator-authorized-local-operation-by-Codex",
    },
    "shell-cli-batchmode-or-headless-unity-authorized": False,
}
WP0002_ALLOWED_ACTIONS = [
    "edit-declared-repository-paths",
    "unity-mcp-project-and-object-edits",
    "unity-mcp-play-mode",
    "unity-mcp-project-tests",
    "unity-mcp-console-read",
    "unity-mcp-screen-capture",
    "local-nondestructive-validation",
    "git-commit-push-protected-pr",
    "creator-delegated-manual-release-after-required-checks",
    "link-exact-repository-local-upm-simulation-core-and-save-contracts",
    "unity-runtime-savecontracts-write-fixed-last-bearing-dev-v1-child",
    "computer-use-local-unity-hub-editor-exact-project-open-switch",
    "computer-use-approve-trusted-codex-unity-bridge-connection",
    "computer-use-non-secret-unity-state-inspection",
]
WP0002_DENIED_ACTIONS = [
    "direct-unity-process-invocation",
    "tool-install-or-package-change-except-two-exact-repository-local-upm-links",
    "account-seat-license-billing-purchase-change",
    "credential-or-secret-access",
    "publish-release-deploy-monetize",
    "external-third-party-contact",
    "git-history-rewrite-or-protection-bypass",
    "direct-agent-write-outside-repository",
    "foundation-governance-receipt-write-during-implementation",
    "package-graph-change-except-two-exact-repository-local-upm-links",
    "registry-network-git-tarball-or-external-dependency-change",
    "agent-or-manual-direct-merge",
    "computer-use-unity-bridge-tool-or-configuration-mutation",
    "computer-use-approve-different-or-unattested-client",
]
WP0002_PACKAGE_META_ROOTS = ("SimulationCore", "SaveContracts")
WP0002_PACKAGE_META_FUTURE_ROOTS = (
    "SimulationCore/Runtime/LastBearing",
    "SaveContracts/Runtime/LastBearing",
)
WP0002_REQUIRED_CI_COMMANDS = (
    "python3 Tools/ScenarioRunner/run.py SCN_COMPOSITION_LOOP_SMOKE",
    "python3 Tools/ScenarioRunner/run.py SCN_TIME_POLICY",
    "python3 Tools/ScenarioRunner/run.py SCN_PREPARATION_MODULE_MATRIX",
    "python3 Tools/ScenarioRunner/run.py SCN_FACTION_WAIT_CLAIM",
    "python3 Tools/ScenarioRunner/run.py SCN_BEARING_COOPERATE",
    "dotnet run --project Tests/AtomicLandPirate.CoreTests/LastBearing/AtomicLandPirate.LastBearingTests.csproj --configuration Release -- --test dev-save-atomic",
    "dotnet run --project Tests/AtomicLandPirate.CoreTests/LastBearing/AtomicLandPirate.LastBearingTests.csproj --configuration Release -- --test dev-save-boundary",
)
WP0002_REQUIRED_CI_TEST_IDS = (
    "T-COMPOSITION-LOOP",
    "T-TIME-POLICY",
    "T-PERMUTATIONS",
    "T-FACTION-AUTONOMY",
    "T-FACTION-DOCTRINE",
    "T-DEV-SAVE-ATOMIC",
    "T-DEV-SAVE-BOUNDARY",
)
WP0002_PACKAGE_META_SHA256 = {
    "SimulationCore/AtomicLandPirate.SimulationCore.csproj.meta": "4c91c53271945d37a3e9fd3d025430597c7ac2fbf00459a550a8de45cbf17f09",
    "SimulationCore/Directory.Build.props.meta": "57ef9a8dcc16fe597c51f8606d58c7628c26209e078ebdaf4ecb7ffbd27a09ae",
    "SimulationCore/Directory.Build.targets.meta": "c2107b8e396b6cc3d55bcc72c14372f0daf609d218af1880e9957bf9da7d476c",
    "SimulationCore/README.md.meta": "4dc53b6f2602bff03af074b5256c49fe8f90cec472ae146e53e8bc7e0ad62d8a",
    "SimulationCore/Runtime.meta": "a09dc4b6b8a8267d0309d94503a11d61d6f8df7d697869a326073efecaeecd41",
    "SimulationCore/Runtime/AC21.Sasha.SimulationCore.asmdef.meta": "26b6195c97eb31bb10dbc37546417964043211f1e3e4fb8eb0f497c0e5f41345",
    "SimulationCore/Runtime/AssemblyInfo.cs.meta": "785240d9a9760f4eeab25cd35008195f18e519a7db86b0d92f5b1cafdf33594b",
    "SimulationCore/Runtime/CanonicalState.cs.meta": "fba6fcf4ad72036dc2aa6ce04d73480bfbd3906ab5ee86215339bcd59ff81889",
    "SimulationCore/Runtime/SimulationKernel.cs.meta": "39c90ad25a63869be2313729b87809bf6650f240d0067214a8ffb0494c076a61",
    "SimulationCore/Runtime/TechnicalCommand.cs.meta": "775230fe850d930e297ac2fd04a02f6b6cadfc4b4c45efe44f59b76590cffe0a",
    "SimulationCore/Runtime/TechnicalDeltaApplied.cs.meta": "d0a9854e592270dd734387abe1fbda91c2e3d55b05d24408d3bde8d576cd688c",
    "SimulationCore/Runtime/TechnicalReadModel.cs.meta": "cb91e0bac503dbbb1db23c8d77ea4f4051129853fc1a95ce031af0bdd905f97e",
    "SimulationCore/Runtime/TechnicalRunResult.cs.meta": "6aa6ab347303168275383f53818f539e0c3aee2532af90ad8d26b3d7cd103e02",
    "SimulationCore/Runtime/TechnicalState.cs.meta": "889e8cea32bad49932464fee6dbd0bd8b4d2eea0a4ca723ffec6b05e05cef9f2",
    "SimulationCore/Runtime/TechnicalTransition.cs.meta": "22ca975345dedafe9ecf9384a0f9a71bc240de56da8ec3bad4e303d1ee0d5189",
    "SimulationCore/package.json.meta": "a812ce6911d0d1c442dc33159d261e3aaa9bc1822be8521341f83ec5e0eda532",
    "SaveContracts/AtomicLandPirate.SaveContracts.csproj.meta": "1ac3012347dbe5e77a9b8f561a5dded4ff7b78651469dbf6128f0f4e61b054ac",
    "SaveContracts/Directory.Build.props.meta": "3c90dbeb3a811a1f68b79b42f1d8f282629c7c1fbc5464bb1b54ae69c55d9198",
    "SaveContracts/Directory.Build.targets.meta": "5e439875d29f3b2e1ea0d04410fd7125a0b8fa1153b42db0f8db8a45e7f05d2f",
    "SaveContracts/README.md.meta": "7ae0afaeb3218efa8559f79b96618f9efbc05140f09e0034c750902ea0f9ee0f",
    "SaveContracts/Runtime.meta": "a93a659f6bd355327edec9f66e6d2adfc96c42eac02a8ef9385acddf4da03e0c",
    "SaveContracts/Runtime/AC21.Sasha.SaveContracts.asmdef.meta": "b567c2d4e91c5e2d1d4b9b1e0b339e065a2dbc8e6838c52c866eb472428fe1df",
    "SaveContracts/Runtime/ISavePort.cs.meta": "93f628c2355c2e97f666293fbead07b11b9b87ec317d5ca678640e8d0138bc68",
    "SaveContracts/Runtime/NonPersistingSavePort.cs.meta": "2b68f8b175c8e6c266a2927cefd2386cfdaad800899e6dc41b2fb51f9ab51bb5",
    "SaveContracts/Runtime/SaveAttemptResult.cs.meta": "482e1b15abdf983d63fbe385c1dac8e5432b0aafaab5df607afa1894be2322bb",
    "SaveContracts/Runtime/SaveCapability.cs.meta": "6cefc1774346724c0f3b7a5a128a42710b34615daff3f243ede7928a804ac2f3",
    "SaveContracts/package.json.meta": "0dc20ad4d418e8b83971235e5acdc7cf145a36cdf4d09c9afb2097cc9dc11a9b",
}
WP0002_PROTECTED_SELF_VERIFICATION = {
    "Tools/Validation/validate_wp0002_package_graph.py": (
        "d9b36dd20099a18de940d833085f81f60a0478f527879633e21c2d28fdae5fd2"
    ),
    "Tools/Validation/validate_wp0002_entry_gate.py": (
        "8bba4fccf7a0ac8cdcc488046fba4ce05fee6ef41903b0f545a028b40a3daeb0"
    ),
    "Tools/Validation/validate_wp0002_policy.py": (
        "5292757e6ba0177b7ff2dd3a5be13d699a2c9df1ca2bf3d6e3839b6052179f31"
    ),
    "Tools/Validation/collect_wp0002_scope_capture.py": (
        "68dfa2c5ce802b71a29717f530be63344d74c50cc8e5e5de4c1b26aa3dcde9f2"
    ),
    "Tools/Validation/verify_wp0002_local_operator_transaction.py": (
        "16a3a5950e191f25b64d86977a64489eb77961ee8b8ca16673c7673e17779c51"
    ),
    ".github/workflows/wp0002-ci.yml": (
        "893cd3faacb887b2d9112c30e15a29b27fb8f3511001ef4091a04f1f88e2f0b9"
    ),
    ".github/workflows/wp0002-policy.yml": (
        "9a72d80e80c482c462640c0e9fc7c7c1b47f2ebec4e01561bb259be083e1cd0b"
    ),
}
PACKET_TRANSITIONS = {
    "proposed": {"accepted", "rejected", "superseded"},
    "accepted": {"active", "rejected", "superseded"},
    "active": {"verifying", "rolled-back", "rejected"},
    "verifying": {"active", "candidate", "rolled-back", "rejected"},
    "candidate": {"verifying", "released", "rolled-back", "rejected"},
    "released": {"rolled-back", "superseded"},
    "rejected": set(),
    "rolled-back": set(),
    "superseded": set(),
}

SCENARIO_COUNT_KEYS = {
    "state": {"authoritative_records", "save_sections", "content_definitions", "transactions", "save_generations"},
    "entities": {"total", "settlements", "buildings", "households", "specialists", "robots", "vehicles", "factions", "crises", "expeditions"},
    "objects": {"placed_total", "dynamic", "static", "lod0", "lod1", "lod2"},
    "lights": {"total", "directional", "point", "spot", "shadowed"},
    "crowd": {"authoritative_people", "visible_agents", "offscreen_people"},
    "inputs": {"scripted_commands", "keyboard_events", "controller_events", "pointer_events", "fault_injections"},
}

ENTITY_COUNT_TO_TYPE = {
    "settlements": "settlement",
    "buildings": "building",
    "households": "household",
    "specialists": "specialist",
    "robots": "robot",
    "vehicles": "vehicle",
    "factions": "faction",
    "crises": "crisis",
    "expeditions": "expedition",
}
ENTITY_TYPES = set(ENTITY_COUNT_TO_TYPE.values())
STATE_DOMAINS = {
    "world", "settlement", "population", "logistics", "vehicle",
    "expedition", "faction", "crisis", "audit", "save",
}
CONTENT_KINDS = {
    "resource", "recipe", "building", "capability", "route",
    "faction-rule", "save-section", "presentation-binding",
}
EVENT_KINDS = {
    "scripted_commands", "keyboard_events", "controller_events",
    "pointer_events", "fault_injections",
}
ORACLE_OPERATORS = {
    "equal", "less-than", "less-or-equal", "greater-or-equal",
    "matches-reference",
}
SCENARIO_SELECTOR_VALUES = {
    "scenario-root": None,
    "save-subsystem": "save",
    "clock-subsystem": "clock",
    "oracle-surface": "oracles",
}
TARGET_RESOLUTION_RULE = (
    "resolve every scripted command payload.target by exact logical_target_id before tick zero; "
    "canonical bindings select resolved_id in the base run and the matching case_resolutions entry "
    "in a case run; scenario selectors resolve only by their declared selector_kind and selector_value"
)
UNRESOLVED_TARGET_RULE = (
    "reject the scenario before tick zero on a missing or duplicate binding, an unreferenced binding, "
    "an unknown selector, a missing case resolution, a kind mismatch, or an ID absent from the "
    "hash-bound starting-state or content-set artifact; never infer, fabricate, or fall back"
)
TARGET_BINDING_HASH_RULE = (
    "target_bindings_sha256 is SHA-256 of canonical JSON for the complete ordered target_bindings array"
)

CREATOR_AUTHORITIES = {
    "creator-prompt",
    "creator-clarification",
    "creator-ratification",
}


def fail(message: str) -> None:
    raise ValueError(message)


def load_json(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        fail(f"Invalid JSON in {path.relative_to(ROOT)}: {exc}")


def validate_decisions() -> tuple[list[dict], list[str]]:
    schema = load_json(DECISION_SCHEMA)
    required = set(schema["required"])
    allowed = set(schema["properties"])
    enum_fields = {
        key: set(value["enum"])
        for key, value in schema["properties"].items()
        if "enum" in value
    }

    records: list[dict] = []
    errors: list[str] = []
    for line_no, raw in enumerate(DECISIONS.read_text(encoding="utf-8").splitlines(), 1):
        if not raw.strip():
            errors.append(f"decisions.jsonl:{line_no}: blank lines are not allowed")
            continue
        try:
            record = json.loads(raw)
        except json.JSONDecodeError as exc:
            errors.append(f"decisions.jsonl:{line_no}: {exc}")
            continue
        if not isinstance(record, dict):
            errors.append(f"decisions.jsonl:{line_no}: record is not an object")
            continue
        missing = required - set(record)
        extra = set(record) - allowed
        if missing:
            errors.append(f"{record.get('id', line_no)} missing: {sorted(missing)}")
        if extra:
            errors.append(f"{record.get('id', line_no)} extra fields: {sorted(extra)}")
        for field, choices in enum_fields.items():
            if record.get(field) not in choices:
                errors.append(f"{record.get('id', line_no)} invalid {field}: {record.get(field)!r}")
        if not re.fullmatch(r"D-\d{4}", str(record.get("id", ""))):
            errors.append(f"line {line_no} has invalid ID: {record.get('id')!r}")
        try:
            date.fromisoformat(record.get("recorded_on", ""))
        except ValueError:
            errors.append(f"{record.get('id', line_no)} has invalid recorded_on")
        if not isinstance(record.get("consequences"), list) or not record.get("consequences"):
            errors.append(f"{record.get('id', line_no)} needs at least one consequence")
        if not isinstance(record.get("revisit_triggers"), list):
            errors.append(f"{record.get('id', line_no)} revisit_triggers must be an array")
        if record.get("status") == "ratified" and record.get("authority") not in {
            "creator-prompt",
            "creator-clarification",
            "creator-ratification",
        }:
            errors.append(f"{record.get('id', line_no)} is ratified without creator authority")
        if record.get("status") == "rejected" and record.get("authority") not in {
            "creator-prompt",
            "creator-clarification",
            "creator-ratification",
        }:
            errors.append(f"{record.get('id', line_no)} is rejected without creator authority")
        if record.get("status") == "open" and not record.get("recommended_default"):
            errors.append(f"{record.get('id', line_no)} is open without a recommended default")
        records.append(record)

    ids = [record.get("id") for record in records]
    if len(ids) != len(set(ids)):
        errors.append("decision IDs are not unique")
    expected = [f"D-{number:04d}" for number in range(1, len(records) + 1)]
    if ids != expected:
        errors.append(f"decision IDs are not monotonic and gap-free: expected {expected}, got {ids}")
    known: set[str] = set()
    previous_hash: str | None = None
    for record in records:
        if record.get("sequence") != len(known) + 1:
            errors.append(f"{record['id']} has incorrect sequence {record.get('sequence')}")
        if record.get("previous_event_hash") != previous_hash:
            errors.append(f"{record['id']} has a broken previous_event_hash chain")
        canonical = {
            key: value for key, value in record.items() if key != "event_hash"
        }
        calculated_hash = hashlib.sha256(
            json.dumps(
                canonical,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=False,
            ).encode("utf-8")
        ).hexdigest()
        if record.get("event_hash") != calculated_hash:
            errors.append(f"{record['id']} event_hash does not match canonical record")
        previous_hash = record.get("event_hash")
        supersedes = record.get("supersedes")
        if supersedes is not None and supersedes not in known:
            errors.append(f"{record['id']} supersedes unknown or later record {supersedes}")
        known.add(record["id"])

    human_ledger = (ROOT / "01-DECISION-LEDGER.md").read_text(encoding="utf-8")
    table_rows = re.findall(
        r"^\| (D-\d{4}) \| ([^|]+?) \| ([^|]+?) \|",
        human_ledger,
        flags=re.MULTILINE,
    )
    table_ids = {row[0] for row in table_rows}
    if table_ids != set(ids):
        errors.append(
            "human ledger and JSONL differ: "
            f"missing from table={sorted(set(ids) - table_ids)}, "
            f"missing from JSONL={sorted(table_ids - set(ids))}"
        )
    table_by_id = {row[0]: (row[1].strip().lower(), row[2].strip().lower()) for row in table_rows}
    for record in records:
        table_values = table_by_id.get(record["id"])
        if table_values and table_values != (record["class"], record["status"]):
            errors.append(
                f"{record['id']} human/JSONL class-status mismatch: "
                f"table={table_values}, JSONL={(record['class'], record['status'])}"
            )
    return records, errors


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_canonical_json(value: object) -> str:
    """Hash the foundation's explicit canonical JSON domain."""
    return hashlib.sha256(
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=False,
        ).encode("utf-8")
    ).hexdigest()


def safe_foundation_path(relative: object, label: str) -> tuple[Path | None, str | None]:
    """Resolve a foundation-relative path without accepting escape or ambiguity."""
    if not isinstance(relative, str) or not relative or "\\" in relative:
        return None, f"{label} must be a non-empty POSIX foundation-relative path"
    candidate = Path(relative)
    if candidate.is_absolute() or any(part in {"", ".", ".."} for part in candidate.parts):
        return None, f"{label} is not a safe foundation-relative path: {relative!r}"
    resolved = (ROOT / candidate).resolve()
    try:
        resolved.relative_to(ROOT.resolve())
    except ValueError:
        return None, f"{label} escapes the foundation: {relative!r}"
    return resolved, None


def safe_repo_path(relative: object, label: str) -> tuple[Path | None, str | None]:
    """Resolve a repository-relative path without accepting escape or ambiguity."""
    if not isinstance(relative, str) or not relative or "\\" in relative:
        return None, f"{label} must be a non-empty POSIX repository-relative path"
    candidate = Path(relative)
    if candidate.is_absolute() or any(part in {"", ".", ".."} for part in candidate.parts):
        return None, f"{label} is not a safe repository-relative path: {relative!r}"
    resolved = (REPO_ROOT / candidate).resolve()
    try:
        resolved.relative_to(REPO_ROOT.resolve())
    except ValueError:
        return None, f"{label} escapes the repository: {relative!r}"
    return resolved, None


def run_foundation_git(args: list[str]) -> subprocess.CompletedProcess[bytes]:
    git_path = Path(SYSTEM_GIT)
    try:
        metadata = git_path.lstat()
    except OSError as exc:
        raise RuntimeError("system Git is unavailable") from exc
    if (
        not stat.S_ISREG(metadata.st_mode)
        or stat.S_ISLNK(metadata.st_mode)
        or metadata.st_uid != 0
        or metadata.st_mode & 0o022
    ):
        raise RuntimeError("system Git identity is unsafe")
    return subprocess.run(
        [
            SYSTEM_GIT,
            *FOUNDATION_GIT_ARGS,
            "-C",
            str(REPO_ROOT),
            *args,
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        stdin=subprocess.DEVNULL,
        check=False,
        env=FOUNDATION_GIT_ENV,
        timeout=20,
    )


def git_commit_exists(commit: str) -> bool:
    result = run_foundation_git(
        ["cat-file", "-e", f"{commit}^{{commit}}"]
    )
    return result.returncode == 0


def git_branch_name_is_valid(branch: str) -> bool:
    result = run_foundation_git(["check-ref-format", "--branch", branch])
    return result.returncode == 0


def git_commit_is_ancestor(ancestor: str, descendant: str) -> bool:
    result = run_foundation_git(
        ["merge-base", "--is-ancestor", ancestor, descendant]
    )
    return result.returncode == 0


def git_commit_is_ancestor_of_protected_main(commit: str) -> bool:
    reference = "refs/remotes/origin/main"
    reference_result = run_foundation_git(
        ["rev-parse", "--verify", "--quiet", reference]
    )
    return (
        reference_result.returncode == 0
        and git_commit_is_ancestor(commit, reference)
    )


def git_repo_blob(commit: str, repo_relative: str) -> bytes | None:
    result = run_foundation_git(
        ["show", f"{commit}:{repo_relative}"]
    )
    return result.stdout if result.returncode == 0 else None


def git_repo_tree_listing(commit: str, repo_relative: str) -> bytes | None:
    result = run_foundation_git(
        [
            "ls-tree",
            "-r",
            "-z",
            commit,
            "--",
            repo_relative,
        ]
    )
    return result.stdout if result.returncode == 0 else None


def git_foundation_blob(commit: str, relative: str) -> bytes | None:
    repo_relative = (Path("docs") / ROOT.name / Path(relative)).as_posix()
    result = run_foundation_git(
        ["show", f"{commit}:{repo_relative}"]
    )
    return result.stdout if result.returncode == 0 else None


def validate_receipts(records: list[dict]) -> tuple[list[dict], list[str]]:
    errors: list[str] = []
    receipts: list[dict] = []
    receipt_dir = ROOT / "ledger" / "receipts"
    for path in sorted(receipt_dir.glob("*.json")):
        errors.extend(validate_instance_shape(path, ROOT / "schemas" / "ratification-receipt.schema.json"))
        receipt = load_json(path)
        receipts.append(receipt)
        subject_claim_bindings = receipt.get("subject_claims", [])
        if not isinstance(subject_claim_bindings, list):
            subject_claim_bindings = []
            errors.append(f"{path.relative_to(ROOT)} subject_claims must be an array")
        if any(not isinstance(binding, dict) for binding in subject_claim_bindings):
            errors.append(f"{path.relative_to(ROOT)} subject_claims entries must be objects")
        bound_subjects = [binding.get("subject_id") for binding in subject_claim_bindings if isinstance(binding, dict)]
        if len(bound_subjects) != len(set(bound_subjects)):
            errors.append(f"{path.relative_to(ROOT)} repeats a subject_claims binding")
        if set(bound_subjects) != set(receipt.get("subject_ids", [])):
            errors.append(f"{path.relative_to(ROOT)} subject_ids and subject_claims differ")
        for binding in subject_claim_bindings:
            if not isinstance(binding, dict):
                continue
            claims = binding.get("claims")
            if not isinstance(binding.get("subject_id"), str) or not binding.get("subject_id"):
                errors.append(f"{path.relative_to(ROOT)} has an invalid subject_claims subject_id")
            if not isinstance(claims, list) or not claims or len(claims) != len(set(claims)):
                errors.append(f"{path.relative_to(ROOT)} has invalid or duplicate subject claims")
            elif any(not isinstance(claim, str) or not re.fullmatch(r"[A-Z0-9+_-]+", claim) for claim in claims):
                errors.append(f"{path.relative_to(ROOT)} has a malformed subject claim")
        resolver = receipt.get("artifact_resolver", {})
        resolver_type = resolver.get("type") if isinstance(resolver, dict) else None
        source_reference = receipt.get("source_reference")
        artifact_hashes = receipt.get("artifact_sha256", {})
        if resolver_type == "local-git-tree":
            source_path, source_error = safe_foundation_path(
                source_reference, f"{path.relative_to(ROOT)} source_reference"
            )
            if source_error:
                errors.append(source_error)
            safe_artifacts: list[tuple[str, Path, str]] = []
            if isinstance(artifact_hashes, dict):
                for relative, expected_hash in artifact_hashes.items():
                    artifact, artifact_error = safe_foundation_path(
                        relative, f"{path.relative_to(ROOT)} artifact_sha256 key"
                    )
                    if artifact_error:
                        errors.append(artifact_error)
                    elif artifact is not None:
                        safe_artifacts.append((relative, artifact, expected_hash))

            if receipt.get("sealed"):
                commit = receipt.get("accepted_commit")
                if not isinstance(commit, str) or not git_commit_exists(commit):
                    errors.append(
                        f"{path.relative_to(ROOT)} accepted local Git commit does not exist"
                    )
                else:
                    if source_error is None and isinstance(source_reference, str):
                        source_blob = git_foundation_blob(commit, source_reference)
                        if source_blob is None:
                            errors.append(
                                f"{path.relative_to(ROOT)} source_reference is absent from accepted commit"
                            )
                        elif hashlib.sha256(source_blob).hexdigest() != receipt.get("approval_text_sha256"):
                            errors.append(
                                f"{path.relative_to(ROOT)} approval source hash differs from accepted tree"
                            )
                    for relative, _, expected_hash in safe_artifacts:
                        blob = git_foundation_blob(commit, relative)
                        if blob is None:
                            errors.append(
                                f"{path.relative_to(ROOT)} artifact is absent from accepted tree: {relative}"
                            )
                        elif hashlib.sha256(blob).hexdigest() != expected_hash:
                            errors.append(
                                f"{path.relative_to(ROOT)} artifact hash differs from accepted tree: {relative}"
                            )
            else:
                if source_path is not None:
                    if not source_path.is_file():
                        errors.append(f"{path.relative_to(ROOT)} source_reference is missing")
                    elif sha256_file(source_path) != receipt.get("approval_text_sha256"):
                        errors.append(f"{path.relative_to(ROOT)} approval source hash mismatch")
                for relative, artifact, expected_hash in safe_artifacts:
                    if not artifact.is_file():
                        errors.append(f"{path.relative_to(ROOT)} artifact is missing: {relative}")
                    elif sha256_file(artifact) != expected_hash:
                        errors.append(f"{path.relative_to(ROOT)} artifact hash mismatch: {relative}")
        elif resolver_type == "external-protected":
            if receipt.get("sealed") and not (
                isinstance(resolver.get("resolver_reference"), str)
                and resolver.get("resolver_reference")
                and receipt.get("signature_reference")
            ):
                errors.append(
                    f"{path.relative_to(ROOT)} external protected receipt lacks resolver/signature authority"
                )
        else:
            errors.append(f"{path.relative_to(ROOT)} uses an unknown artifact resolver")

        if receipt.get("sealed") and not receipt.get("signature_reference"):
            errors.append(f"{path.relative_to(ROOT)} is sealed without a signature reference")

    by_id = {receipt["receipt_id"]: receipt for receipt in receipts}
    if len(by_id) != len(receipts):
        errors.append("ratification receipt IDs are not unique")
    records_by_id = {record["id"]: record for record in records}
    for receipt in receipts:
        event_bindings = receipt.get("subject_event_sha256", {})
        if not isinstance(event_bindings, dict):
            continue
        expected_decision_subjects = {
            subject_id
            for subject_id in receipt.get("subject_ids", [])
            if subject_id in records_by_id
        }
        if set(event_bindings) != expected_decision_subjects:
            errors.append(
                f"receipt {receipt.get('receipt_id')} decision event bindings differ from its decision subjects"
            )
        for decision_id in expected_decision_subjects:
            if event_bindings.get(decision_id) != records_by_id[decision_id].get("event_hash"):
                errors.append(
                    f"receipt {receipt.get('receipt_id')} does not bind exact event hash for {decision_id}"
                )
    for record in records:
        if record.get("status") != "ratified":
            continue
        receipt_id = record.get("approval_receipt_id")
        receipt = by_id.get(receipt_id)
        if receipt is None:
            errors.append(f"{record['id']} ratified without a known receipt")
        elif record["id"] not in receipt.get("subject_ids", []):
            errors.append(f"{record['id']} is not named by receipt {receipt_id}")
        elif receipt.get("subject_event_sha256", {}).get(record["id"]) != record.get("event_hash"):
            errors.append(f"{record['id']} receipt does not bind its exact event hash")
        elif receipt.get("issuer_role") != "creator":
            errors.append(f"{record['id']} is ratified without a creator-issued receipt")
    return receipts, errors


def validate_local_links() -> list[str]:
    errors: list[str] = []
    link_pattern = re.compile(r"\[[^\]]*\]\(([^)]+)\)")
    for markdown in ROOT.rglob("*.md"):
        text = markdown.read_text(encoding="utf-8")
        for target in link_pattern.findall(text):
            if target.startswith(("http://", "https://", "#", "mailto:")):
                continue
            path_part = target.split("#", 1)[0]
            if not path_part:
                continue
            resolved = (markdown.parent / path_part).resolve()
            try:
                resolved.relative_to(ROOT)
            except ValueError:
                errors.append(f"{markdown.relative_to(ROOT)} link escapes foundation: {target}")
                continue
            if not resolved.exists():
                errors.append(f"{markdown.relative_to(ROOT)} has missing local link: {target}")
    return errors


def gfm_heading_anchors(path: Path) -> set[str]:
    """Return GitHub-style heading anchors, including duplicate suffixes."""
    anchors: set[str] = set()
    occurrences: Counter[str] = Counter()
    in_fence = False
    fence_marker = ""
    for line in path.read_text(encoding="utf-8").splitlines():
        fence = re.match(r"^\s*(```|~~~)", line)
        if fence:
            marker = fence.group(1)
            if not in_fence:
                in_fence = True
                fence_marker = marker
            elif marker == fence_marker:
                in_fence = False
                fence_marker = ""
            continue
        if in_fence:
            continue
        match = re.match(r"^\s{0,3}#{1,6}\s+(.+?)\s*$", line)
        if not match:
            continue
        heading = re.sub(r"\s+#+\s*$", "", match.group(1))
        heading = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", heading)
        heading = re.sub(r"<[^>]+>", "", heading)
        base = re.sub(r"[^\w\- ]", "", heading.lower())
        base = re.sub(r"\s", "-", base)
        suffix = occurrences[base]
        occurrences[base] += 1
        anchors.add(base if suffix == 0 else f"{base}-{suffix}")
    return anchors


def validate_packet_system_contracts(path: Path, packet: dict) -> list[str]:
    """Validate Markdown paths and anchors embedded as JSON strings."""
    errors: list[str] = []
    contracts = packet.get("system_contracts")
    if not isinstance(contracts, list) or not contracts:
        return [f"{path.relative_to(ROOT)} system_contracts must be a non-empty array"]
    if len(contracts) != len(set(item for item in contracts if isinstance(item, str))):
        errors.append(f"{path.relative_to(ROOT)} system_contracts repeats a reference")
    for index, target in enumerate(contracts):
        label = f"{path.relative_to(ROOT)} system_contracts[{index}]"
        if not isinstance(target, str) or not target:
            errors.append(f"{label} must be a non-empty Markdown reference")
            continue
        file_part, separator, anchor = target.partition("#")
        if not file_part or file_part.startswith("/") or ".." in Path(file_part).parts:
            errors.append(f"{label} has an unsafe file path: {target!r}")
            continue
        if Path(file_part).suffix.lower() != ".md":
            errors.append(f"{label} must target a Markdown file: {target!r}")
        resolved = (ROOT / file_part).resolve()
        try:
            resolved.relative_to(ROOT.resolve())
        except ValueError:
            errors.append(f"{label} escapes the foundation: {target!r}")
            continue
        if not resolved.is_file():
            errors.append(f"{label} targets a missing file: {target!r}")
            continue
        if separator:
            if not anchor or "#" in anchor:
                errors.append(f"{label} has a malformed anchor: {target!r}")
            elif anchor not in gfm_heading_anchors(resolved):
                errors.append(f"{label} targets a missing GFM anchor: {target!r}")
    return errors


def validate_references(records: list[dict]) -> list[str]:
    errors: list[str] = []
    known = {record["id"] for record in records}
    for path in ROOT.rglob("*.md"):
        for decision_id in set(re.findall(r"D-\d{4}", path.read_text(encoding="utf-8"))):
            if decision_id not in known:
                errors.append(f"{path.relative_to(ROOT)} references unknown {decision_id}")
    return errors


def validate_markdown_scenario_references(scenarios: dict[str, dict]) -> list[str]:
    errors: list[str] = []
    fenced_code = re.compile(r"(^|\n)(```|~~~).*?\n\2(?:\n|$)", flags=re.DOTALL)
    for path in ROOT.rglob("*.md"):
        text = fenced_code.sub("\n", path.read_text(encoding="utf-8"))
        for scenario_id in sorted(set(re.findall(r"SCN_[A-Z0-9_]+", text))):
            if scenario_id not in scenarios:
                errors.append(f"{path.relative_to(ROOT)} references unregistered scenario {scenario_id}")
    return errors


def _json_equal(left: object, right: object) -> bool:
    """JSON equality that does not confuse booleans with numbers."""
    if isinstance(left, bool) or isinstance(right, bool):
        return type(left) is type(right) and left == right
    if isinstance(left, (int, float)) and isinstance(right, (int, float)):
        return left == right
    if isinstance(left, list) and isinstance(right, list):
        return len(left) == len(right) and all(
            _json_equal(left_item, right_item)
            for left_item, right_item in zip(left, right)
        )
    if isinstance(left, dict) and isinstance(right, dict):
        return set(left) == set(right) and all(
            _json_equal(left[key], right[key]) for key in left
        )
    return type(left) is type(right) and left == right


def _json_type_matches(value: object, expected: str) -> bool:
    checks = {
        "null": lambda item: item is None,
        "boolean": lambda item: isinstance(item, bool),
        "object": lambda item: isinstance(item, dict),
        "array": lambda item: isinstance(item, list),
        "string": lambda item: isinstance(item, str),
        "integer": lambda item: isinstance(item, int) and not isinstance(item, bool),
        "number": lambda item: isinstance(item, (int, float)) and not isinstance(item, bool),
    }
    return expected in checks and checks[expected](value)


def _resolve_local_ref(root_schema: dict, reference: str) -> object:
    if not reference.startswith("#/"):
        raise ValueError(f"unsupported non-local schema reference {reference!r}")
    current: object = root_schema
    for raw_part in reference[2:].split("/"):
        part = raw_part.replace("~1", "/").replace("~0", "~")
        if not isinstance(current, dict) or part not in current:
            raise ValueError(f"unresolved schema reference {reference!r}")
        current = current[part]
    return current


def _format_matches(value: str, format_name: str) -> bool:
    if format_name == "date":
        try:
            return date.fromisoformat(value).isoformat() == value
        except ValueError:
            return False
    if format_name == "date-time":
        return parse_datetime(value) is not None and ("T" in value or "t" in value)
    return True


def validate_schema_subset(
    instance: object,
    schema: object,
    root_schema: dict,
    label: str,
) -> list[str]:
    """Recursively assert the exact JSON Schema subset used by this repository.

    This is dependency-free bootstrap lint, not the pinned full Draft 2020-12
    validator required for the protected gatekeeper.
    """
    if isinstance(schema, bool):
        return [] if schema else [f"{label} is rejected by a false schema"]
    if not isinstance(schema, dict):
        return [f"{label} has an invalid local schema node"]
    if "$ref" in schema:
        try:
            target = _resolve_local_ref(root_schema, schema["$ref"])
        except ValueError as exc:
            return [f"{label}: {exc}"]
        ref_errors = validate_schema_subset(instance, target, root_schema, label)
        siblings = {key: value for key, value in schema.items() if key != "$ref"}
        if siblings:
            ref_errors.extend(validate_schema_subset(instance, siblings, root_schema, label))
        return ref_errors

    errors: list[str] = []
    if "allOf" in schema:
        for index, branch in enumerate(schema["allOf"]):
            errors.extend(validate_schema_subset(instance, branch, root_schema, f"{label}.allOf[{index}]"))
    if "oneOf" in schema:
        branch_results = [
            validate_schema_subset(instance, branch, root_schema, label)
            for branch in schema["oneOf"]
        ]
        matches = sum(not branch_errors for branch_errors in branch_results)
        if matches != 1:
            errors.append(f"{label} must match exactly one oneOf branch; matched {matches}")
    if "if" in schema:
        condition_matches = not validate_schema_subset(instance, schema["if"], root_schema, label)
        branch_name = "then" if condition_matches else "else"
        if branch_name in schema:
            errors.extend(validate_schema_subset(instance, schema[branch_name], root_schema, label))

    if "const" in schema and not _json_equal(instance, schema["const"]):
        errors.append(f"{label} must equal {schema['const']!r}")
    if "enum" in schema and not any(_json_equal(instance, choice) for choice in schema["enum"]):
        errors.append(f"{label} is not in the allowed enum")

    expected_types = schema.get("type")
    if isinstance(expected_types, str):
        expected_types = [expected_types]
    if isinstance(expected_types, list) and not any(
        isinstance(item, str) and _json_type_matches(instance, item)
        for item in expected_types
    ):
        errors.append(f"{label} has the wrong JSON type; expected {expected_types}")
        return errors

    if isinstance(instance, str):
        if len(instance) < schema.get("minLength", 0):
            errors.append(f"{label} is shorter than minLength")
        if "maxLength" in schema and len(instance) > schema["maxLength"]:
            errors.append(f"{label} is longer than maxLength")
        if "pattern" in schema and re.search(schema["pattern"], instance) is None:
            errors.append(f"{label} fails pattern {schema['pattern']!r}")
        if "format" in schema and not _format_matches(instance, schema["format"]):
            errors.append(f"{label} fails format {schema['format']!r}")

    if isinstance(instance, (int, float)) and not isinstance(instance, bool):
        if "minimum" in schema and instance < schema["minimum"]:
            errors.append(f"{label} is below minimum")
        if "maximum" in schema and instance > schema["maximum"]:
            errors.append(f"{label} is above maximum")

    if isinstance(instance, list):
        if len(instance) < schema.get("minItems", 0):
            errors.append(f"{label} has fewer than minItems")
        if "maxItems" in schema and len(instance) > schema["maxItems"]:
            errors.append(f"{label} has more than maxItems")
        if schema.get("uniqueItems"):
            if any(
                _json_equal(instance[left], instance[right])
                for left in range(len(instance))
                for right in range(left + 1, len(instance))
            ):
                errors.append(f"{label} violates uniqueItems")
        prefix_items = schema.get("prefixItems")
        prefix_count = 0
        if isinstance(prefix_items, list):
            prefix_count = len(prefix_items)
            for index, item_schema in enumerate(prefix_items):
                if index >= len(instance):
                    break
                errors.extend(
                    validate_schema_subset(
                        instance[index],
                        item_schema,
                        root_schema,
                        f"{label}[{index}]",
                    )
                )
        if "items" in schema:
            item_schema = schema["items"]
            start = prefix_count if isinstance(prefix_items, list) else 0
            if item_schema is False and len(instance) > start:
                errors.append(f"{label} has items beyond its allowed prefix")
            elif item_schema is not False:
                for index in range(start, len(instance)):
                    errors.extend(
                        validate_schema_subset(
                            instance[index],
                            item_schema,
                            root_schema,
                            f"{label}[{index}]",
                        )
                    )

    if isinstance(instance, dict):
        if len(instance) < schema.get("minProperties", 0):
            errors.append(f"{label} has fewer than minProperties")
        properties = schema.get("properties", {})
        required = schema.get("required", [])
        for key in required:
            if key not in instance:
                errors.append(f"{label} is missing required property {key!r}")
        if "propertyNames" in schema:
            for key in instance:
                errors.extend(validate_schema_subset(key, schema["propertyNames"], root_schema, f"{label}.propertyName[{key!r}]"))
        for key, value in instance.items():
            if key in properties:
                errors.extend(validate_schema_subset(value, properties[key], root_schema, f"{label}.{key}"))
                continue
            additional = schema.get("additionalProperties", {})
            if additional is False:
                errors.append(f"{label} has forbidden property {key!r}")
            elif isinstance(additional, (dict, bool)):
                errors.extend(validate_schema_subset(value, additional, root_schema, f"{label}.{key}"))
    return errors


def validate_instance_shape(path: Path, schema_path: Path) -> list[str]:
    """Validate one JSON instance with the repository's recursive subset."""
    instance = load_json(path)
    schema = load_json(schema_path)
    label: str | None = None
    for base in (ROOT, REPO_ROOT):
        for candidate, candidate_base in (
            (path, base),
            (path.resolve(strict=False), base.resolve(strict=False)),
        ):
            try:
                label = str(candidate.relative_to(candidate_base))
            except ValueError:
                continue
            break
        if label is not None:
            break
    return validate_schema_subset(
        instance,
        schema,
        schema,
        label if label is not None else str(path),
    )


def is_nonnegative_int(value: object) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def validate_scenario_definition(path: Path, registered_id: str) -> tuple[dict, list[str]]:
    errors = validate_instance_shape(path, SCENARIO_SCHEMA)
    scenario = load_json(path)
    label = str(path.relative_to(ROOT))
    if not isinstance(scenario, dict):
        return {}, errors + [f"{label} must be an object"]

    scenario_id = scenario.get("id")
    if scenario_id != registered_id:
        errors.append(f"{label} ID {scenario_id!r} does not match registry ID {registered_id!r}")
    if path.stem != registered_id:
        errors.append(f"{label} filename does not match scenario ID {registered_id}")
    if scenario.get("schema_version") != 2:
        errors.append(f"{label} schema_version must be 2")
    if not isinstance(scenario.get("revision"), int) or isinstance(scenario.get("revision"), bool) or scenario["revision"] < 1:
        errors.append(f"{label} revision must be a positive integer")
    if scenario.get("kind") not in {"performance", "save", "migration", "rollback", "gameplay", "aggregate"}:
        errors.append(f"{label} has an invalid kind")
    for field in ("title", "purpose"):
        if not isinstance(scenario.get(field), str) or not scenario[field]:
            errors.append(f"{label} {field} must be a non-empty string")

    packets = scenario.get("applies_to_packets")
    if not isinstance(packets, list) or not packets:
        errors.append(f"{label} applies_to_packets must be a non-empty array")
    elif len(packets) != len(set(packets)) or any(not re.fullmatch(r"WP-\d{4}", str(item)) for item in packets):
        errors.append(f"{label} applies_to_packets contains a duplicate or invalid packet ID")

    fixture = scenario.get("fixture")
    fixture_keys = {
        "world_seed", "starting_tick", "tick_rate_hz", "warmup_ticks", "duration_ticks",
        "warmup_seconds", "duration_seconds", "starting_state_id", "content_set_id", "input_script_id",
        "fixture_manifest",
    }
    if not isinstance(fixture, dict):
        errors.append(f"{label} fixture must be an object")
    else:
        if set(fixture) != fixture_keys:
            errors.append(f"{label} fixture fields differ: expected {sorted(fixture_keys)}, got {sorted(fixture)}")
        for field in ("world_seed", "starting_tick"):
            if not re.fullmatch(r"[0-9]+", str(fixture.get(field, ""))):
                errors.append(f"{label} fixture {field} must be an unsigned decimal string")
        tick_rate = fixture.get("tick_rate_hz")
        if not is_nonnegative_int(tick_rate) or not 1 <= tick_rate <= 120:
            errors.append(f"{label} fixture tick_rate_hz must be an integer from 1 to 120")
        for field in ("warmup_ticks", "duration_ticks", "warmup_seconds", "duration_seconds"):
            if not is_nonnegative_int(fixture.get(field)):
                errors.append(f"{label} fixture {field} must be a non-negative integer")
        if is_nonnegative_int(tick_rate):
            if is_nonnegative_int(fixture.get("warmup_seconds")) and fixture.get("warmup_ticks") != fixture["warmup_seconds"] * tick_rate:
                errors.append(f"{label} warmup_ticks does not equal warmup_seconds * tick_rate_hz")
            if is_nonnegative_int(fixture.get("duration_seconds")) and fixture.get("duration_ticks") != fixture["duration_seconds"] * tick_rate:
                errors.append(f"{label} duration_ticks does not equal duration_seconds * tick_rate_hz")
        fixture_id_pattern = r"[a-z0-9][a-z0-9._-]+"
        for field in ("starting_state_id", "content_set_id", "input_script_id"):
            if not re.fullmatch(fixture_id_pattern, str(fixture.get(field, ""))):
                errors.append(f"{label} fixture {field} is invalid")

        manifest_ref = fixture.get("fixture_manifest")
        if not isinstance(manifest_ref, dict) or set(manifest_ref) != {"path", "sha256"}:
            errors.append(f"{label} fixture_manifest must contain exactly path and sha256")
        else:
            manifest_relative = manifest_ref.get("path")
            manifest_hash = manifest_ref.get("sha256")
            expected_manifest_relative = f"scenarios/fixtures/{scenario_id}.fixture.json"
            if manifest_relative != expected_manifest_relative:
                errors.append(
                    f"{label} fixture_manifest path must be {expected_manifest_relative!r}, "
                    f"got {manifest_relative!r}"
                )
            if not re.fullmatch(r"[0-9a-f]{64}", str(manifest_hash)):
                errors.append(f"{label} fixture_manifest sha256 is invalid")
            if isinstance(manifest_relative, str):
                manifest_path = (ROOT / manifest_relative).resolve()
                try:
                    manifest_path.relative_to(SCENARIO_FIXTURES.resolve())
                except ValueError:
                    errors.append(f"{label} fixture_manifest path escapes scenarios/fixtures")
                else:
                    if not manifest_path.exists() or not manifest_path.is_file():
                        errors.append(f"{label} fixture_manifest is missing: {manifest_relative}")
                    else:
                        actual_manifest_hash = sha256_file(manifest_path)
                        if actual_manifest_hash != manifest_hash:
                            errors.append(
                                f"{label} fixture_manifest hash mismatch: expected {manifest_hash}, "
                                f"got {actual_manifest_hash}"
                            )
                        manifest = load_json(manifest_path)
                        if not isinstance(manifest, dict):
                            errors.append(f"{manifest_relative} must be an object")
                        else:
                            manifest_fields = {
                                "schema_version", "scenario_id", "scenario_revision", "hash_algorithm",
                                "canonicalization", "fixture_contract", "deterministic_contract", "contract_hashes",
                            }
                            if set(manifest) != manifest_fields:
                                errors.append(
                                    f"{manifest_relative} fields differ from the fixture-manifest contract"
                                )
                            if manifest.get("schema_version") != 1:
                                errors.append(f"{manifest_relative} schema_version must be 1")
                            if manifest.get("scenario_id") != scenario_id:
                                errors.append(f"{manifest_relative} scenario_id does not match {scenario_id}")
                            if manifest.get("scenario_revision") != scenario.get("revision"):
                                errors.append(f"{manifest_relative} scenario_revision does not match the scenario")
                            if manifest.get("hash_algorithm") != "sha256":
                                errors.append(f"{manifest_relative} hash_algorithm must be sha256")
                            if manifest.get("canonicalization") != "json-sort-keys-utf8-no-whitespace-v1":
                                errors.append(f"{manifest_relative} has an unsupported canonicalization")
                            expected_fixture_contract = {
                                key: value for key, value in fixture.items() if key != "fixture_manifest"
                            }
                            if manifest.get("fixture_contract") != expected_fixture_contract:
                                errors.append(
                                    f"{manifest_relative} fixture_contract does not exactly bind the scenario fixture"
                                )
                            expected_deterministic_contract = {
                                "counts": scenario.get("counts"),
                                "parameters": scenario.get("parameters"),
                                "oracles": scenario.get("oracles"),
                            }
                            if manifest.get("deterministic_contract") != expected_deterministic_contract:
                                errors.append(
                                    f"{manifest_relative} deterministic_contract does not exactly bind "
                                    "counts, parameters, and oracles"
                                )
                            contract_hashes = manifest.get("contract_hashes")
                            expected_contract_hashes = {
                                "counts_sha256": sha256_canonical_json(scenario.get("counts")),
                                "parameters_sha256": sha256_canonical_json(scenario.get("parameters")),
                                "oracles_sha256": sha256_canonical_json(scenario.get("oracles")),
                            }
                            if contract_hashes != expected_contract_hashes:
                                errors.append(
                                    f"{manifest_relative} contract_hashes do not bind counts, parameters, and oracles"
                                )

    counts = scenario.get("counts")
    if not isinstance(counts, dict) or set(counts) != set(SCENARIO_COUNT_KEYS):
        errors.append(f"{label} counts must declare exactly {sorted(SCENARIO_COUNT_KEYS)}")
    else:
        for group_name, expected_keys in SCENARIO_COUNT_KEYS.items():
            group = counts.get(group_name)
            if not isinstance(group, dict) or set(group) != expected_keys:
                errors.append(f"{label} counts.{group_name} fields differ from the scenario contract")
                continue
            for field, value in group.items():
                if not is_nonnegative_int(value):
                    errors.append(f"{label} counts.{group_name}.{field} must be a non-negative integer")
        entities = counts.get("entities", {})
        if set(entities) == SCENARIO_COUNT_KEYS["entities"] and all(is_nonnegative_int(value) for value in entities.values()):
            entity_sum = sum(value for key, value in entities.items() if key != "total")
            if entities["total"] != entity_sum:
                errors.append(f"{label} counts.entities.total does not equal the declared entity classes")
        objects = counts.get("objects", {})
        if set(objects) == SCENARIO_COUNT_KEYS["objects"] and all(is_nonnegative_int(value) for value in objects.values()):
            if objects["placed_total"] != objects["dynamic"] + objects["static"]:
                errors.append(f"{label} placed_total does not equal dynamic + static")
            if objects["placed_total"] != objects["lod0"] + objects["lod1"] + objects["lod2"]:
                errors.append(f"{label} placed_total does not equal lod0 + lod1 + lod2")
        lights = counts.get("lights", {})
        if set(lights) == SCENARIO_COUNT_KEYS["lights"] and all(is_nonnegative_int(value) for value in lights.values()):
            if lights["total"] != lights["directional"] + lights["point"] + lights["spot"]:
                errors.append(f"{label} lights.total does not equal directional + point + spot")
            if lights["shadowed"] > lights["total"]:
                errors.append(f"{label} shadowed lights exceed total lights")
        crowd = counts.get("crowd", {})
        if (
            set(crowd) == SCENARIO_COUNT_KEYS["crowd"]
            and all(is_nonnegative_int(value) for value in crowd.values())
            and crowd["authoritative_people"] != crowd["visible_agents"] + crowd["offscreen_people"]
        ):
            errors.append(f"{label} authoritative_people does not equal visible_agents + offscreen_people")
        if scenario.get("kind") == "aggregate":
            runtime_values = [value for group in counts.values() for value in group.values()]
            if any(runtime_values):
                errors.append(f"{label} aggregate scenario must declare every runtime count as zero")
            if isinstance(fixture, dict) and (fixture.get("warmup_ticks") != 0 or fixture.get("duration_ticks") != 0):
                errors.append(f"{label} aggregate scenario must have zero warmup and duration")

    parameters = scenario.get("parameters")
    if not isinstance(parameters, list):
        errors.append(f"{label} parameters must be an array")
    else:
        names: list[object] = []
        for parameter in parameters:
            if not isinstance(parameter, dict) or set(parameter) != {"name", "value", "unit"}:
                errors.append(f"{label} has an invalid parameter record")
                continue
            names.append(str(parameter.get("name", "")))
            if not re.fullmatch(r"[a-z][a-z0-9_.-]+", str(parameter.get("name", ""))):
                errors.append(f"{label} has an invalid parameter name")
            if isinstance(parameter.get("value"), (dict, list)) or parameter.get("value") is None:
                errors.append(f"{label} parameter {parameter.get('name')} must have a scalar value")
            if parameter.get("unit") is not None and not isinstance(parameter.get("unit"), str):
                errors.append(f"{label} parameter {parameter.get('name')} has an invalid unit")
        if len(names) != len(set(names)):
            errors.append(f"{label} parameter names are not unique")

    oracles = scenario.get("oracles")
    if not isinstance(oracles, list) or not oracles:
        errors.append(f"{label} oracles must be a non-empty array")
    else:
        oracle_ids: list[object] = []
        oracle_subjects: list[str] = []
        for oracle in oracles:
            if not isinstance(oracle, dict) or set(oracle) != {"id", "subject", "operator", "expected", "unit"}:
                errors.append(f"{label} has an invalid oracle record")
                continue
            oracle_ids.append(str(oracle.get("id", "")))
            oracle_subjects.append(str(oracle.get("subject", "")))
            if not re.fullmatch(r"ORC_[A-Z0-9_]+", str(oracle.get("id", ""))):
                errors.append(f"{label} has an invalid oracle ID")
            if not re.fullmatch(r"[a-z][a-z0-9_.-]+", str(oracle.get("subject", ""))):
                errors.append(f"{label} oracle {oracle.get('id')} has an invalid subject")
            if oracle.get("operator") not in {"equal", "less-than", "less-or-equal", "greater-or-equal", "matches-reference"}:
                errors.append(f"{label} oracle {oracle.get('id')} has an invalid operator")
            if isinstance(oracle.get("expected"), (dict, list)) or oracle.get("expected") is None:
                errors.append(f"{label} oracle {oracle.get('id')} must have a scalar expected value")
            if oracle.get("unit") is not None and not isinstance(oracle.get("unit"), str):
                errors.append(f"{label} oracle {oracle.get('id')} has an invalid unit")
        if len(oracle_ids) != len(set(oracle_ids)):
            errors.append(f"{label} oracle IDs are not unique")
        if len(oracle_subjects) != len(set(oracle_subjects)):
            errors.append(f"{label} oracle subjects are not unique")

    return scenario, errors


def validate_scenario_registry() -> tuple[dict[str, dict], list[str]]:
    errors: list[str] = []
    scenarios: dict[str, dict] = {}
    registry = load_json(SCENARIO_REGISTRY)
    if not isinstance(registry, dict):
        return {}, ["scenarios/registry.json must be an object"]
    expected_registry_fields = {"schema_version", "hash_algorithm", "hash_domain", "scenarios"}
    if set(registry) != expected_registry_fields:
        errors.append("scenarios/registry.json has unexpected or missing fields")
    if registry.get("schema_version") != 1:
        errors.append("scenarios/registry.json schema_version must be 1")
    if registry.get("hash_algorithm") != "sha256" or registry.get("hash_domain") != "raw-file-bytes":
        errors.append("scenarios/registry.json must hash raw file bytes with sha256")
    entries = registry.get("scenarios")
    if not isinstance(entries, list) or not entries:
        return {}, errors + ["scenarios/registry.json scenarios must be a non-empty array"]

    entry_ids: list[str] = []
    entry_paths: list[str] = []
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != {"id", "path", "sha256"}:
            errors.append("scenarios/registry.json contains an invalid entry")
            continue
        scenario_id = str(entry.get("id", ""))
        relative = str(entry.get("path", ""))
        expected_hash = str(entry.get("sha256", ""))
        entry_ids.append(scenario_id)
        entry_paths.append(relative)
        if not re.fullmatch(r"SCN_[A-Z0-9_]+", scenario_id):
            errors.append(f"scenario registry has invalid ID {scenario_id!r}")
        if not re.fullmatch(r"[0-9a-f]{64}", expected_hash):
            errors.append(f"scenario registry {scenario_id} has invalid sha256")
        path = (ROOT / relative).resolve()
        try:
            path.relative_to(SCENARIO_DEFINITIONS.resolve())
        except ValueError:
            errors.append(f"scenario registry {scenario_id} path escapes scenario definitions: {relative}")
            continue
        if not path.exists() or not path.is_file():
            errors.append(f"scenario registry {scenario_id} path is missing: {relative}")
            continue
        actual_hash = sha256_file(path)
        if actual_hash != expected_hash:
            errors.append(f"scenario registry {scenario_id} hash mismatch: expected {expected_hash}, got {actual_hash}")
        scenario, definition_errors = validate_scenario_definition(path, scenario_id)
        errors.extend(definition_errors)
        scenarios[scenario_id] = scenario

    if len(entry_ids) != len(set(entry_ids)):
        errors.append("scenario registry IDs are not unique")
    if len(entry_paths) != len(set(entry_paths)):
        errors.append("scenario registry paths are not unique")
    if entry_ids != sorted(entry_ids):
        errors.append("scenario registry entries must be sorted by ID")
    registered_paths = {(ROOT / relative).resolve() for relative in entry_paths}
    definition_paths = {path.resolve() for path in SCENARIO_DEFINITIONS.glob("*.json")}
    if registered_paths != definition_paths:
        missing = sorted(str(path.relative_to(ROOT)) for path in definition_paths - registered_paths)
        stale = sorted(str(path) for path in registered_paths - definition_paths)
        errors.append(f"scenario registry/file set differs: unregistered={missing}, missing={stale}")
    referenced_fixture_paths = {
        (ROOT / scenario["fixture"]["fixture_manifest"]["path"]).resolve()
        for scenario in scenarios.values()
        if isinstance(scenario.get("fixture"), dict)
        and isinstance(scenario["fixture"].get("fixture_manifest"), dict)
        and isinstance(scenario["fixture"]["fixture_manifest"].get("path"), str)
    }
    fixture_paths = {path.resolve() for path in SCENARIO_FIXTURES.glob("*.fixture.json")}
    if referenced_fixture_paths != fixture_paths:
        unreferenced = sorted(str(path.relative_to(ROOT)) for path in fixture_paths - referenced_fixture_paths)
        missing = sorted(str(path) for path in referenced_fixture_paths - fixture_paths)
        errors.append(
            f"scenario fixture-manifest set differs: unreferenced={unreferenced}, missing={missing}"
        )
    return scenarios, errors


def validate_packet_scenario_references(path: Path, scenarios: dict[str, dict]) -> list[str]:
    errors: list[str] = []
    packet = load_json(path)
    packet_id = packet.get("id")
    references: list[tuple[str, str]] = []
    for scenario_id in packet.get("save_impact", {}).get("golden_scenarios", []):
        references.append(("save_impact.golden_scenarios", scenario_id))
    for metric in packet.get("performance_metrics", []):
        references.append((f"performance_metrics.{metric.get('name')}", metric.get("scenario")))
    for signal in packet.get("rollout", {}).get("health_signals", []):
        references.append((f"rollout.health_signals.{signal.get('name')}", signal.get("scenario")))

    for source, scenario_id in references:
        if not isinstance(scenario_id, str) or not re.fullmatch(r"SCN_[A-Z0-9_]+", scenario_id):
            errors.append(f"{path.relative_to(ROOT)} {source} must reference a registered SCN_ ID, got {scenario_id!r}")
            continue
        scenario = scenarios.get(scenario_id)
        if scenario is None:
            errors.append(f"{path.relative_to(ROOT)} {source} references unknown {scenario_id}")
            continue
        if packet_id not in scenario.get("applies_to_packets", []):
            errors.append(f"{path.relative_to(ROOT)} references {scenario_id}, which does not apply to {packet_id}")
    for signal in packet.get("rollout", {}).get("health_signals", []):
        if signal.get("name") == "required-test-failures":
            scenario = scenarios.get(signal.get("scenario"))
            if scenario is not None and scenario.get("kind") != "aggregate":
                errors.append(f"{path.relative_to(ROOT)} required-test-failures must use an aggregate scenario")
            if scenario is not None and scenario.get("kind") == "aggregate":
                parameters = {item.get("name"): item.get("value") for item in scenario.get("parameters", [])}
                required_count = sum(1 for test in packet.get("acceptance_tests", []) if test.get("required"))
                if parameters.get("required-acceptance-tests") != required_count:
                    errors.append(
                        f"{path.relative_to(ROOT)} has {required_count} required tests but "
                        f"{signal.get('scenario')} freezes {parameters.get('required-acceptance-tests')!r}"
                    )
                minimum_runs = packet.get("rollout", {}).get("minimum_runs")
                if parameters.get("minimum-runs") != minimum_runs:
                    errors.append(
                        f"{path.relative_to(ROOT)} minimum_runs {minimum_runs!r} differs from "
                        f"{signal.get('scenario')} value {parameters.get('minimum-runs')!r}"
                    )

    def check_metric_oracle(metric: dict, source: str) -> None:
        scenario = scenarios.get(metric.get("scenario"))
        if scenario is None:
            return
        matches = [oracle for oracle in scenario.get("oracles", []) if oracle.get("subject") == metric.get("name")]
        if len(matches) != 1:
            errors.append(
                f"{path.relative_to(ROOT)} {source} requires exactly one {metric.get('name')!r} oracle "
                f"in {metric.get('scenario')}, found {len(matches)}"
            )
            return
        oracle = matches[0]
        if oracle.get("expected") != metric.get("target"):
            errors.append(
                f"{path.relative_to(ROOT)} {source} target {metric.get('target')!r} differs from "
                f"{metric.get('scenario')} oracle {oracle.get('expected')!r}"
            )
        packet_comparator = metric.get("comparator")
        oracle_operator = oracle.get("operator")
        compatible = oracle_operator == packet_comparator or (
            oracle_operator == "equal" and packet_comparator in {"less-or-equal", "greater-or-equal"}
        )
        if not compatible:
            errors.append(
                f"{path.relative_to(ROOT)} {source} comparator {packet_comparator!r} conflicts with "
                f"{metric.get('scenario')} oracle operator {oracle_operator!r}"
            )

    for metric in packet.get("performance_metrics", []):
        check_metric_oracle(metric, f"performance_metrics.{metric.get('name')}")
    for signal in packet.get("rollout", {}).get("health_signals", []):
        check_metric_oracle(signal, f"rollout.health_signals.{signal.get('name')}")
    return errors


def validate_count_contract_v2(value: object, label: str) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, dict) or set(value) != set(SCENARIO_COUNT_KEYS):
        return [f"{label} must contain exactly {sorted(SCENARIO_COUNT_KEYS)}"]
    for group, keys in SCENARIO_COUNT_KEYS.items():
        block = value.get(group)
        if not isinstance(block, dict) or set(block) != keys:
            errors.append(f"{label}.{group} must contain exactly {sorted(keys)}")
            continue
        for key, count in block.items():
            if not is_nonnegative_int(count):
                errors.append(f"{label}.{group}.{key} must be a non-negative integer")
    entities = value.get("entities", {})
    if isinstance(entities, dict):
        expected = sum(count for key, count in entities.items() if key != "total" and is_nonnegative_int(count))
        if entities.get("total") != expected:
            errors.append(f"{label}.entities.total {entities.get('total')!r} != component sum {expected}")
    objects = value.get("objects", {})
    if isinstance(objects, dict):
        if objects.get("placed_total") != objects.get("dynamic", 0) + objects.get("static", 0):
            errors.append(f"{label}.objects placed_total != dynamic + static")
        if objects.get("placed_total") != objects.get("lod0", 0) + objects.get("lod1", 0) + objects.get("lod2", 0):
            errors.append(f"{label}.objects placed_total != LOD occupancy sum")
    lights = value.get("lights", {})
    if isinstance(lights, dict) and lights.get("total") != lights.get("directional", 0) + lights.get("point", 0) + lights.get("spot", 0):
        errors.append(f"{label}.lights total != directional + point + spot")
    crowd = value.get("crowd", {})
    if isinstance(crowd, dict) and crowd.get("authoritative_people") != crowd.get("visible_agents", 0) + crowd.get("offscreen_people", 0):
        # Visible agents may include robots, so only reject when visible humans are not explicitly mixed.
        if value.get("entities", {}).get("robots", 0) == 0:
            errors.append(f"{label}.crowd people != visible + offscreen for a no-robot case")
    return errors


def validate_materialized_entities(
    value: object,
    counts: object,
    label: str,
) -> list[str]:
    """Require exact, valued entity records rather than a count-only recipe."""
    errors: list[str] = []
    if not isinstance(value, list):
        return [f"{label} must be a materialized entity array"]
    entity_counts = counts.get("entities", {}) if isinstance(counts, dict) else {}
    expected_total = entity_counts.get("total")
    if len(value) != expected_total:
        errors.append(f"{label} has {len(value)} records; expected {expected_total}")
    ids: list[str] = []
    actual_types: Counter[str] = Counter()
    ordinals: dict[str, list[int]] = {kind: [] for kind in ENTITY_TYPES}
    required = {"id", "type", "ordinal", "value"}
    for index, record in enumerate(value):
        item_label = f"{label}[{index}]"
        if not isinstance(record, dict) or set(record) != required:
            errors.append(f"{item_label} must contain exactly {sorted(required)}")
            continue
        entity_id = record.get("id")
        entity_type = record.get("type")
        ordinal = record.get("ordinal")
        materialized_value = record.get("value")
        if not isinstance(entity_id, str) or not entity_id:
            errors.append(f"{item_label}.id must be a non-empty string")
        else:
            ids.append(entity_id)
        if entity_type not in ENTITY_TYPES:
            errors.append(f"{item_label}.type is invalid: {entity_type!r}")
        else:
            actual_types[entity_type] += 1
            if isinstance(ordinal, int) and not isinstance(ordinal, bool):
                ordinals[entity_type].append(ordinal)
        if not isinstance(ordinal, int) or isinstance(ordinal, bool) or ordinal < 1:
            errors.append(f"{item_label}.ordinal must be a positive integer")
        if not isinstance(materialized_value, dict) or len(materialized_value) < 3:
            errors.append(f"{item_label}.value must contain at least three concrete fields")
    if len(ids) != len(set(ids)):
        errors.append(f"{label} repeats an entity ID")
    for count_key, entity_type in ENTITY_COUNT_TO_TYPE.items():
        expected = entity_counts.get(count_key)
        if actual_types[entity_type] != expected:
            errors.append(
                f"{label} has {actual_types[entity_type]} {entity_type} records; expected {expected}"
            )
        if sorted(ordinals[entity_type]) != list(range(1, actual_types[entity_type] + 1)):
            errors.append(f"{label} {entity_type} ordinals must be contiguous from one")
    return errors


def validate_materialized_state_records(
    value: object,
    expected_count: object,
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, list):
        return [f"{label} must be a materialized state-record array"]
    if len(value) != expected_count:
        errors.append(f"{label} has {len(value)} records; expected {expected_count}")
    ids: list[str] = []
    ordinals: list[int] = []
    required = {"id", "domain", "ordinal", "version", "value"}
    for index, record in enumerate(value):
        item_label = f"{label}[{index}]"
        if not isinstance(record, dict) or set(record) != required:
            errors.append(f"{item_label} must contain exactly {sorted(required)}")
            continue
        record_id = record.get("id")
        ordinal = record.get("ordinal")
        materialized_value = record.get("value")
        if not isinstance(record_id, str) or not record_id:
            errors.append(f"{item_label}.id must be a non-empty string")
        else:
            ids.append(record_id)
        if record.get("domain") not in STATE_DOMAINS:
            errors.append(f"{item_label}.domain is invalid: {record.get('domain')!r}")
        if not isinstance(ordinal, int) or isinstance(ordinal, bool) or ordinal < 1:
            errors.append(f"{item_label}.ordinal must be a positive integer")
        else:
            ordinals.append(ordinal)
        if record.get("version") != 1:
            errors.append(f"{item_label}.version must be 1")
        if not isinstance(materialized_value, dict) or len(materialized_value) < 3:
            errors.append(f"{item_label}.value must contain at least three concrete fields")
    if len(ids) != len(set(ids)):
        errors.append(f"{label} repeats a state-record ID")
    if sorted(ordinals) != list(range(1, len(value) + 1)):
        errors.append(f"{label} ordinals must be contiguous from one")
    return errors


def validate_observations(
    value: object,
    oracles: object,
    start_tick: int,
    end_tick: int,
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, list):
        return [f"{label} must be an explicit observation array"]
    oracle_list = oracles if isinstance(oracles, list) else []
    expected = {
        (oracle.get("id"), oracle.get("subject")): oracle
        for oracle in oracle_list
        if isinstance(oracle, dict)
    }
    actual: dict[tuple[object, object], dict] = {}
    required = {
        "sequence", "tick", "subject", "selector", "operator",
        "expected_source", "oracle_id",
    }
    for index, observation in enumerate(value):
        item_label = f"{label}[{index}]"
        if not isinstance(observation, dict) or set(observation) != required:
            errors.append(f"{item_label} must contain exactly {sorted(required)}")
            continue
        sequence = observation.get("sequence")
        tick = observation.get("tick")
        if sequence != index:
            errors.append(f"{item_label}.sequence must equal its array position")
        if not isinstance(tick, int) or isinstance(tick, bool) or not start_tick <= tick <= end_tick:
            errors.append(f"{item_label}.tick must be within [{start_tick}, {end_tick}]")
        if not isinstance(observation.get("selector"), str) or not observation.get("selector"):
            errors.append(f"{item_label}.selector must be non-empty")
        if observation.get("operator") not in ORACLE_OPERATORS:
            errors.append(f"{item_label}.operator is invalid")
        if observation.get("expected_source") != "scenario-definition":
            errors.append(f"{item_label}.expected_source must be scenario-definition")
        key = (observation.get("oracle_id"), observation.get("subject"))
        if key in actual:
            errors.append(f"{label} repeats oracle observation {key[0]!r}")
        actual[key] = observation
    if set(actual) != set(expected):
        errors.append(
            f"{label} oracle bindings differ: expected {sorted(map(str, expected))}, "
            f"got {sorted(map(str, actual))}"
        )
    for key in set(actual) & set(expected):
        if actual[key].get("operator") != expected[key].get("operator"):
            errors.append(f"{label} {key[0]} operator differs from scenario definition")
    return errors


def validate_explicit_events(
    value: object,
    expected_counts: object,
    start_tick: int,
    end_tick: int,
    label: str,
) -> list[str]:
    errors: list[str] = []
    if not isinstance(value, list):
        return [f"{label} must be an explicit ordered event array"]
    counts = expected_counts if isinstance(expected_counts, dict) else {}
    expected_total = sum(count for count in counts.values() if is_nonnegative_int(count))
    if len(value) != expected_total:
        errors.append(f"{label} has {len(value)} events; expected {expected_total}")
    actual_counts: Counter[str] = Counter()
    ids: list[str] = []
    ordering: list[tuple[int, int]] = []
    required = {"sequence", "tick", "kind", "payload"}
    payload_fields = {
        "scripted_commands": {"command", "command_id", "target", "arguments"},
        "keyboard_events": {"device", "key", "phase"},
        "pointer_events": {"button", "phase", "x_milli", "y_milli"},
        "fault_injections": {"boundary", "fault", "occurrence"},
    }
    for index, event in enumerate(value):
        item_label = f"{label}[{index}]"
        if not isinstance(event, dict) or set(event) != required:
            errors.append(f"{item_label} must contain exactly {sorted(required)}")
            continue
        sequence = event.get("sequence")
        tick = event.get("tick")
        kind = event.get("kind")
        payload = event.get("payload")
        if sequence != index:
            errors.append(f"{item_label}.sequence must equal its array position")
        if not isinstance(tick, int) or isinstance(tick, bool) or not start_tick <= tick <= end_tick:
            errors.append(f"{item_label}.tick must be within [{start_tick}, {end_tick}]")
        if isinstance(tick, int) and not isinstance(tick, bool) and isinstance(sequence, int) and not isinstance(sequence, bool):
            ordering.append((tick, sequence))
        if kind not in EVENT_KINDS:
            errors.append(f"{item_label}.kind is invalid: {kind!r}")
        else:
            actual_counts[kind] += 1
        if not isinstance(payload, dict) or len(payload) < 2:
            errors.append(f"{item_label}.payload must be a concrete object")
            continue
        if kind in payload_fields and set(payload) != payload_fields[kind]:
            errors.append(
                f"{item_label}.payload must contain exactly {sorted(payload_fields[kind])}"
            )
        if kind == "controller_events" and len(payload) < 2:
            errors.append(f"{item_label}.payload lacks a concrete controller event")
        if kind == "scripted_commands":
            if not isinstance(payload.get("command"), str) or not payload.get("command"):
                errors.append(f"{item_label}.payload.command must be non-empty")
            command_id = payload.get("command_id")
            if not isinstance(command_id, str) or not command_id:
                errors.append(f"{item_label}.payload.command_id must be non-empty")
            else:
                ids.append(command_id)
            if not isinstance(payload.get("target"), str) or not payload.get("target"):
                errors.append(f"{item_label}.payload.target must be non-empty")
            if not isinstance(payload.get("arguments"), dict):
                errors.append(f"{item_label}.payload.arguments must be an object")
    if ordering != sorted(ordering):
        errors.append(f"{label} must be ordered by ascending (tick, sequence)")
    if len(ids) != len(set(ids)):
        errors.append(f"{label} repeats a command_id")
    for kind in EVENT_KINDS:
        if actual_counts[kind] != counts.get(kind):
            errors.append(f"{label} has {actual_counts[kind]} {kind}; expected {counts.get(kind)}")
    return errors


def validate_materialized_starting_state(
    artifact: dict,
    scenario: dict,
    label: str,
) -> list[str]:
    errors: list[str] = []
    required = {
        "schema_version", "artifact_kind", "semantic_id", "scenario_id",
        "scenario_revision", "world_seed", "starting_tick", "canonicalization",
        "canonical_entities", "canonical_state_records", "case_states",
    }
    if set(artifact) != required:
        errors.append(f"{label} must contain exactly {sorted(required)}")
    fixture = scenario.get("fixture", {})
    if artifact.get("world_seed") != fixture.get("world_seed") or artifact.get("starting_tick") != fixture.get("starting_tick"):
        errors.append(f"{label} seed/tick differ from scenario fixture")
    if not isinstance(artifact.get("canonicalization"), str) or not artifact.get("canonicalization"):
        errors.append(f"{label} canonicalization must be non-empty")
    errors.extend(validate_materialized_entities(artifact.get("canonical_entities"), scenario.get("counts"), f"{label}.canonical_entities"))
    expected_records = scenario.get("counts", {}).get("state", {}).get("authoritative_records")
    errors.extend(validate_materialized_state_records(artifact.get("canonical_state_records"), expected_records, f"{label}.canonical_state_records"))
    cases = scenario.get("cases", [])
    case_states = artifact.get("case_states")
    if not isinstance(case_states, list):
        return errors + [f"{label}.case_states must be a materialized array"]
    expected_cases = {case.get("id"): case for case in cases if isinstance(case, dict)}
    actual_cases = {case.get("id"): case for case in case_states if isinstance(case, dict)}
    if len(actual_cases) != len(case_states):
        errors.append(f"{label}.case_states must have unique, well-formed IDs")
    if set(actual_cases) != set(expected_cases):
        errors.append(f"{label}.case_states IDs differ from scenario cases")
    case_required = {"id", "canonical_entities", "canonical_state_records", "ablation_contract"}
    for case_id in sorted(set(actual_cases) & set(expected_cases)):
        state = actual_cases[case_id]
        case = expected_cases[case_id]
        case_label = f"{label}.case_states.{case_id}"
        if set(state) != case_required:
            errors.append(f"{case_label} must contain exactly {sorted(case_required)}")
        errors.extend(validate_materialized_entities(state.get("canonical_entities"), case.get("counts"), f"{case_label}.canonical_entities"))
        case_records = case.get("counts", {}).get("state", {}).get("authoritative_records")
        errors.extend(validate_materialized_state_records(state.get("canonical_state_records"), case_records, f"{case_label}.canonical_state_records"))
        contract = state.get("ablation_contract")
        if not isinstance(contract, dict) or not contract:
            errors.append(f"{case_label}.ablation_contract must bind the case parameters")
        expected_contract = {
            item.get("name"): item.get("value")
            for item in case.get("parameters", [])
            if isinstance(item, dict) and item.get("name") in {
                "ablation", "shared-bottleneck", "case-composition",
                "preparation", "vehicle-module", "clock-policy",
            }
        }
        if contract != expected_contract:
            errors.append(f"{case_label}.ablation_contract differs from scenario parameters")
    return errors


def validate_materialized_content_set(
    artifact: dict,
    scenario: dict,
    label: str,
) -> list[str]:
    errors: list[str] = []
    required = {
        "schema_version", "artifact_kind", "semantic_id", "scenario_id",
        "scenario_revision", "canonicalization", "definitions",
    }
    allowed = required | {"render_workload"}
    if not required.issubset(artifact) or not set(artifact).issubset(allowed):
        errors.append(f"{label} has missing or extra materialized content fields")
    if not isinstance(artifact.get("canonicalization"), str) or not artifact.get("canonicalization"):
        errors.append(f"{label} canonicalization must be non-empty")
    definitions = artifact.get("definitions")
    if not isinstance(definitions, list):
        return errors + [f"{label}.definitions must be a materialized array"]
    expected_count = scenario.get("counts", {}).get("state", {}).get("content_definitions")
    if len(definitions) != expected_count:
        errors.append(f"{label}.definitions has {len(definitions)} records; expected {expected_count}")
    ids: list[str] = []
    definition_required = {"id", "kind", "version", "tags", "enabled", "contract"}
    for index, definition in enumerate(definitions):
        item_label = f"{label}.definitions[{index}]"
        if not isinstance(definition, dict) or set(definition) != definition_required:
            errors.append(f"{item_label} must contain exactly {sorted(definition_required)}")
            continue
        definition_id = definition.get("id")
        if not isinstance(definition_id, str) or not definition_id:
            errors.append(f"{item_label}.id must be non-empty")
        else:
            ids.append(definition_id)
        if definition.get("kind") not in CONTENT_KINDS:
            errors.append(f"{item_label}.kind is invalid")
        if definition.get("version") != 1:
            errors.append(f"{item_label}.version must be 1")
        tags = definition.get("tags")
        if not isinstance(tags, list) or not tags or any(not isinstance(tag, str) or not tag for tag in tags):
            errors.append(f"{item_label}.tags must contain non-empty strings")
        if not isinstance(definition.get("enabled"), bool):
            errors.append(f"{item_label}.enabled must be boolean")
        contract = definition.get("contract")
        if not isinstance(contract, dict) or len(contract) < 2:
            errors.append(f"{item_label}.contract must contain concrete content values")
    if len(ids) != len(set(ids)):
        errors.append(f"{label}.definitions repeats an ID")
    if scenario.get("id") == "SCN_SPIKE_SLICE":
        workload = artifact.get("render_workload")
        if not isinstance(workload, dict) or not workload:
            errors.append(f"{label}.render_workload must materialize the renderer fixture")
    return errors


def validate_target_bindings(
    artifact: dict,
    scenario: dict,
    starting_artifact: object,
    content_artifact: object,
    label: str,
) -> list[str]:
    """Prove every logical command target resolves before the first tick."""
    errors: list[str] = []
    bindings = artifact.get("target_bindings")
    if not isinstance(bindings, list):
        return [f"{label}.target_bindings must be an explicit array"]
    if artifact.get("target_bindings_sha256") != sha256_canonical_json(bindings):
        errors.append(f"{label}.target_bindings_sha256 does not bind the canonical table")

    referenced: list[str] = []
    event_groups: list[tuple[str | None, object]] = [(None, artifact.get("events"))]
    for case_event in artifact.get("case_events", []) if isinstance(artifact.get("case_events"), list) else []:
        if isinstance(case_event, dict):
            event_groups.append((case_event.get("id"), case_event.get("events")))
    for case_id, events in event_groups:
        if not isinstance(events, list):
            continue
        for event in events:
            if not isinstance(event, dict) or event.get("kind") != "scripted_commands":
                continue
            payload = event.get("payload")
            target = payload.get("target") if isinstance(payload, dict) else None
            if isinstance(target, str) and target:
                referenced.append(target)

    starting_value = starting_artifact if isinstance(starting_artifact, dict) else {}
    content_value = content_artifact if isinstance(content_artifact, dict) else {}
    base_entities = {
        item.get("id")
        for item in starting_value.get("canonical_entities", [])
        if isinstance(item, dict)
    }
    base_records = {
        item.get("id")
        for item in starting_value.get("canonical_state_records", [])
        if isinstance(item, dict)
    }
    content_definitions = {
        item.get("id")
        for item in content_value.get("definitions", [])
        if isinstance(item, dict)
    }
    case_states = {
        item.get("id"): item
        for item in starting_value.get("case_states", [])
        if isinstance(item, dict)
    }
    expected_case_ids = {
        case.get("id") for case in scenario.get("cases", []) if isinstance(case, dict)
    }

    logical_ids: list[str] = []
    canonical_fields = {"logical_target_id", "binding_kind", "resolved_id", "case_resolutions"}
    selector_fields = {"logical_target_id", "binding_kind", "selector_kind", "selector_value"}
    for index, binding in enumerate(bindings):
        item_label = f"{label}.target_bindings[{index}]"
        if not isinstance(binding, dict):
            errors.append(f"{item_label} must be an object")
            continue
        logical_id = binding.get("logical_target_id")
        if not isinstance(logical_id, str) or not logical_id:
            errors.append(f"{item_label}.logical_target_id must be non-empty")
        else:
            logical_ids.append(logical_id)
        binding_kind = binding.get("binding_kind")
        if binding_kind == "scenario-selector":
            if set(binding) != selector_fields:
                errors.append(f"{item_label} selector must contain exactly {sorted(selector_fields)}")
                continue
            selector_kind = binding.get("selector_kind")
            if selector_kind not in SCENARIO_SELECTOR_VALUES:
                errors.append(f"{item_label} has unknown selector_kind {selector_kind!r}")
                continue
            expected_value = (
                scenario.get("id")
                if selector_kind == "scenario-root"
                else SCENARIO_SELECTOR_VALUES[selector_kind]
            )
            if binding.get("selector_value") != expected_value:
                errors.append(
                    f"{item_label}.selector_value must be {expected_value!r} for {selector_kind}"
                )
            continue
        if set(binding) != canonical_fields:
            errors.append(f"{item_label} canonical binding must contain exactly {sorted(canonical_fields)}")
            continue
        resolved_id = binding.get("resolved_id")
        resolution_sets = {
            "canonical-entity": base_entities,
            "canonical-state-record": base_records,
            "content-definition": content_definitions,
        }
        allowed_ids = resolution_sets.get(binding_kind)
        if allowed_ids is None:
            errors.append(f"{item_label} has unknown binding_kind {binding_kind!r}")
            continue
        if resolved_id not in allowed_ids:
            errors.append(f"{item_label}.resolved_id is absent from its canonical artifact")
        case_resolutions = binding.get("case_resolutions")
        if not isinstance(case_resolutions, list):
            errors.append(f"{item_label}.case_resolutions must be an array")
            continue
        resolution_by_case: dict[object, object] = {}
        for case_index, resolution in enumerate(case_resolutions):
            resolution_label = f"{item_label}.case_resolutions[{case_index}]"
            if not isinstance(resolution, dict) or set(resolution) != {"case_id", "resolved_id"}:
                errors.append(f"{resolution_label} must contain exactly case_id and resolved_id")
                continue
            case_id = resolution.get("case_id")
            if case_id in resolution_by_case:
                errors.append(f"{item_label} repeats case resolution {case_id!r}")
            resolution_by_case[case_id] = resolution.get("resolved_id")
        if binding_kind == "content-definition":
            if case_resolutions:
                errors.append(f"{item_label} content definitions must not declare case overrides")
            continue
        if set(resolution_by_case) != expected_case_ids:
            errors.append(f"{item_label} case resolutions must exactly cover scenario cases")
        for case_id in set(resolution_by_case) & expected_case_ids:
            case_state = case_states.get(case_id, {})
            collection = (
                case_state.get("canonical_entities", [])
                if binding_kind == "canonical-entity"
                else case_state.get("canonical_state_records", [])
            )
            case_ids = {item.get("id") for item in collection if isinstance(item, dict)}
            if resolution_by_case[case_id] not in case_ids:
                errors.append(
                    f"{item_label} case {case_id} resolved_id is absent from its case starting state"
                )

    if len(logical_ids) != len(set(logical_ids)):
        errors.append(f"{label}.target_bindings repeats a logical_target_id")
    if logical_ids != sorted(logical_ids):
        errors.append(f"{label}.target_bindings must be ordered by logical_target_id")
    if set(logical_ids) != set(referenced):
        errors.append(
            f"{label}.target_bindings must exactly equal referenced scripted targets: "
            f"missing={sorted(set(referenced) - set(logical_ids))}, "
            f"unreferenced={sorted(set(logical_ids) - set(referenced))}"
        )
    return errors


def validate_materialized_input_script(
    artifact: dict,
    scenario: dict,
    label: str,
    starting_artifact: object,
    content_artifact: object,
) -> list[str]:
    errors: list[str] = []
    required = {
        "schema_version", "artifact_kind", "semantic_id", "scenario_id",
        "scenario_revision", "tick_rate_hz", "warmup_ticks", "duration_ticks",
        "normative_execution", "target_bindings_sha256", "target_bindings",
        "events", "case_events", "oracle_observations",
    }
    allowed = required | {"camera_path"}
    if not required.issubset(artifact) or not set(artifact).issubset(allowed):
        errors.append(f"{label} has missing or extra explicit input fields")
    fixture = scenario.get("fixture", {})
    for field in ("tick_rate_hz", "warmup_ticks", "duration_ticks"):
        if artifact.get(field) != fixture.get(field):
            errors.append(f"{label}.{field} differs from scenario fixture")
    execution = artifact.get("normative_execution")
    execution_fields = {
        "ordering", "fixed_tick_rule", "case_isolation", "unknown_command_rule",
        "integer_rule", "autonomous_tick_rule", "target_resolution_rule",
        "unresolved_target_rule", "target_binding_hash_rule",
    }
    if not isinstance(execution, dict) or set(execution) != execution_fields:
        errors.append(f"{label}.normative_execution must contain exactly {sorted(execution_fields)}")
    elif any(not isinstance(value, str) or not value for value in execution.values()):
        errors.append(f"{label}.normative_execution rules must be non-empty")
    else:
        exact_target_laws = {
            "target_resolution_rule": TARGET_RESOLUTION_RULE,
            "unresolved_target_rule": UNRESOLVED_TARGET_RULE,
            "target_binding_hash_rule": TARGET_BINDING_HASH_RULE,
        }
        for field, expected in exact_target_laws.items():
            if execution.get(field) != expected:
                errors.append(f"{label}.normative_execution.{field} differs from the exact law")
    start_tick = int(fixture.get("starting_tick", "0"))
    end_tick = start_tick + fixture.get("duration_ticks", 0)
    errors.extend(validate_explicit_events(artifact.get("events"), scenario.get("counts", {}).get("inputs"), start_tick, end_tick, f"{label}.events"))
    errors.extend(validate_observations(artifact.get("oracle_observations"), scenario.get("oracles"), start_tick, end_tick, f"{label}.oracle_observations"))
    errors.extend(validate_target_bindings(artifact, scenario, starting_artifact, content_artifact, label))
    cases = scenario.get("cases", [])
    case_events = artifact.get("case_events")
    if not isinstance(case_events, list):
        return errors + [f"{label}.case_events must be an explicit array"]
    expected_cases = {case.get("id"): case for case in cases if isinstance(case, dict)}
    actual_cases = {case.get("id"): case for case in case_events if isinstance(case, dict)}
    if len(actual_cases) != len(case_events):
        errors.append(f"{label}.case_events must have unique, well-formed IDs")
    if set(actual_cases) != set(expected_cases):
        errors.append(f"{label}.case_events IDs differ from scenario cases")
    case_required = {"id", "events", "oracle_observations"}
    for case_id in sorted(set(actual_cases) & set(expected_cases)):
        case_event = actual_cases[case_id]
        case = expected_cases[case_id]
        case_label = f"{label}.case_events.{case_id}"
        if set(case_event) != case_required:
            errors.append(f"{case_label} must contain exactly {sorted(case_required)}")
        errors.extend(validate_explicit_events(case_event.get("events"), case.get("counts", {}).get("inputs"), start_tick, end_tick, f"{case_label}.events"))
        errors.extend(validate_observations(case_event.get("oracle_observations"), case.get("oracles"), start_tick, end_tick, f"{case_label}.oracle_observations"))
    if scenario.get("id") == "SCN_SPIKE_SLICE":
        camera_path = artifact.get("camera_path")
        if not isinstance(camera_path, dict) or not camera_path:
            errors.append(f"{label}.camera_path must materialize the renderer fixture")
    return errors


def validate_current_scenario_v2(path: Path, entry: dict) -> tuple[dict, list[str]]:
    errors = validate_instance_shape(path, SCENARIO_SCHEMA)
    scenario = load_json(path)
    label = str(path.relative_to(ROOT))
    sid = entry.get("id")
    revision = entry.get("revision")
    if scenario.get("id") != sid or scenario.get("revision") != revision:
        errors.append(f"{label} identity/revision differs from registry")
    expected_path = f"scenarios/definitions/{sid}/r{revision}.json"
    if str(path.relative_to(ROOT)) != expected_path:
        errors.append(f"{label} is not the exact revisioned path {expected_path}")
    if scenario.get("schema_version") != 2:
        errors.append(f"{label} schema_version must be 2")
    errors.extend(validate_count_contract_v2(scenario.get("counts"), f"{label}.counts"))
    cases = scenario.get("cases", [])
    if cases:
        case_ids = [case.get("id") for case in cases if isinstance(case, dict)]
        if len(case_ids) != len(cases) or len(case_ids) != len(set(case_ids)):
            errors.append(f"{label} cases must have unique IDs")
        for case in cases:
            if not isinstance(case, dict) or set(case) != {"id", "counts", "parameters", "oracles"}:
                errors.append(f"{label} has malformed case contract")
                continue
            errors.extend(validate_count_contract_v2(case.get("counts"), f"{label}.cases.{case.get('id')}.counts"))
            oracle_ids = [item.get("id") for item in case.get("oracles", []) if isinstance(item, dict)]
            if len(oracle_ids) != len(set(oracle_ids)):
                errors.append(f"{label} case {case.get('id')} repeats an oracle ID")

    fixture = scenario.get("fixture", {})
    tick_rate = fixture.get("tick_rate_hz")
    if not is_nonnegative_int(tick_rate) or tick_rate < 1:
        errors.append(f"{label} has invalid tick rate")
    elif fixture.get("warmup_ticks") != fixture.get("warmup_seconds", -1) * tick_rate or fixture.get("duration_ticks") != fixture.get("duration_seconds", -1) * tick_rate:
        errors.append(f"{label} fixture seconds/ticks are inconsistent")
    manifest_ref = fixture.get("fixture_manifest", {})
    expected_manifest_path = f"scenarios/fixtures/{sid}/r{revision}.fixture.json"
    if manifest_ref.get("path") != expected_manifest_path or manifest_ref.get("path") != entry.get("fixture_manifest_path"):
        errors.append(f"{label} does not bind registry fixture manifest path")
    if manifest_ref.get("sha256") != entry.get("fixture_manifest_sha256"):
        errors.append(f"{label} does not bind registry fixture manifest hash")
    manifest_path = (ROOT / str(manifest_ref.get("path", ""))).resolve()
    try:
        manifest_path.relative_to(SCENARIO_FIXTURES.resolve())
    except ValueError:
        errors.append(f"{label} fixture manifest escapes scenario fixtures")
        return scenario, errors
    if not manifest_path.is_file():
        errors.append(f"{label} fixture manifest is missing")
        return scenario, errors
    if sha256_file(manifest_path) != manifest_ref.get("sha256"):
        errors.append(f"{label} fixture manifest hash mismatch")
    errors.extend(validate_instance_shape(manifest_path, SCENARIO_FIXTURE_SCHEMA))
    manifest = load_json(manifest_path)
    manifest_fields = {
        "schema_version", "scenario_id", "scenario_revision", "hash_algorithm",
        "artifacts",
    }
    if set(manifest) != manifest_fields:
        errors.append(f"{manifest_path.relative_to(ROOT)} must contain exactly {sorted(manifest_fields)}")
    if manifest.get("schema_version") != 2 or manifest.get("hash_algorithm") != "sha256":
        errors.append(f"{manifest_path.relative_to(ROOT)} must use artifact-backed manifest schema v2 and SHA-256")
    if manifest.get("scenario_id") != sid or manifest.get("scenario_revision") != revision:
        errors.append(f"{manifest_path.relative_to(ROOT)} identity/revision mismatch")
    artifact_ids = {
        "starting_state": fixture.get("starting_state_id"),
        "content_set": fixture.get("content_set_id"),
        "input_script": fixture.get("input_script_id"),
    }
    manifest_artifacts = manifest.get("artifacts")
    if not isinstance(manifest_artifacts, dict) or set(manifest_artifacts) != set(artifact_ids):
        errors.append(f"{manifest_path.relative_to(ROOT)} must bind exactly the three typed artifacts")
    expected_kinds = {"starting_state": "starting-state", "content_set": "content-set", "input_script": "input-script"}
    loaded_artifacts: dict[str, dict] = {}
    for key, semantic_id in artifact_ids.items():
        ref = manifest.get("artifacts", {}).get(key, {}) if isinstance(manifest.get("artifacts"), dict) else {}
        ref_fields = {"kind", "semantic_id", "path", "sha256", "media_type", "schema_version"}
        if not isinstance(ref, dict) or set(ref) != ref_fields:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} must contain exactly {sorted(ref_fields)}")
        expected_artifact_path = f"scenarios/artifacts/{sid}/r{revision}/{key.replace('_', '-')}.json"
        if ref.get("kind") != expected_kinds[key] or ref.get("semantic_id") != semantic_id:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} kind/semantic ID mismatch")
        if ref.get("media_type") != "application/json" or ref.get("schema_version") != 1:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} media/schema contract mismatch")
        if ref.get("path") != expected_artifact_path:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} must use {expected_artifact_path}")
        artifact_path = (ROOT / str(ref.get("path", ""))).resolve()
        try:
            artifact_path.relative_to(SCENARIO_ARTIFACTS.resolve())
        except ValueError:
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} escapes scenario artifacts")
            continue
        if not artifact_path.is_file():
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} is missing")
            continue
        if sha256_file(artifact_path) != ref.get("sha256"):
            errors.append(f"{manifest_path.relative_to(ROOT)} {key} hash mismatch")
        errors.extend(validate_instance_shape(artifact_path, SCENARIO_ARTIFACT_SCHEMA))
        artifact = load_json(artifact_path)
        loaded_artifacts[key] = artifact
        if artifact.get("schema_version") != 1 or artifact.get("artifact_kind") != expected_kinds[key] or artifact.get("semantic_id") != semantic_id:
            errors.append(f"{artifact_path.relative_to(ROOT)} typed identity mismatch")
        if artifact.get("scenario_id") != sid or artifact.get("scenario_revision") != revision:
            errors.append(f"{artifact_path.relative_to(ROOT)} scenario binding mismatch")
        if key == "starting_state":
            errors.extend(validate_materialized_starting_state(artifact, scenario, str(artifact_path.relative_to(ROOT))))
        elif key == "content_set":
            errors.extend(validate_materialized_content_set(artifact, scenario, str(artifact_path.relative_to(ROOT))))
        else:
            errors.extend(
                validate_materialized_input_script(
                    artifact,
                    scenario,
                    str(artifact_path.relative_to(ROOT)),
                    loaded_artifacts.get("starting_state"),
                    loaded_artifacts.get("content_set"),
                )
            )
    return scenario, errors


def validate_scenario_registry_v2() -> tuple[dict[str, dict], list[str]]:
    errors: list[str] = []
    registry = load_json(SCENARIO_REGISTRY)
    registry_fields = {
        "schema_version", "hash_algorithm", "hash_domain",
        "revision_baseline", "scenarios",
    }
    if set(registry) != registry_fields:
        errors.append(f"scenario registry must contain exactly {sorted(registry_fields)}")
    if registry.get("schema_version") != 2 or registry.get("hash_algorithm") != "sha256" or registry.get("hash_domain") != "raw-file-bytes":
        errors.append("scenario registry must use schema_version 2 and raw-file SHA-256")
    if registry.get("revision_baseline") != "initial-commit-r1":
        errors.append("scenario registry must declare the initial-commit-r1 revision baseline")
    entries = registry.get("scenarios", [])
    if not isinstance(entries, list):
        return {}, errors + ["scenario registry scenarios must be an array"]
    expected_fields = {"id", "revision", "path", "sha256", "fixture_manifest_path", "fixture_manifest_sha256"}
    scenarios: dict[str, dict] = {}
    ids: list[str] = []
    registered_definitions: set[Path] = set()
    registered_manifests: set[Path] = set()
    registered_artifacts: set[Path] = set()
    for entry in entries:
        if not isinstance(entry, dict) or set(entry) != expected_fields:
            errors.append("scenario registry contains a malformed revision pin")
            continue
        sid, revision = entry.get("id"), entry.get("revision")
        ids.append(sid)
        if revision != 1:
            errors.append(f"scenario registry {sid} must begin at r1 before the initial commit")
        expected_path = f"scenarios/definitions/{sid}/r{revision}.json"
        expected_fixture = f"scenarios/fixtures/{sid}/r{revision}.fixture.json"
        if entry.get("path") != expected_path or entry.get("fixture_manifest_path") != expected_fixture:
            errors.append(f"scenario registry {sid} has non-revisioned or mismatched paths")
        path = ROOT / entry.get("path", "")
        manifest_path = ROOT / entry.get("fixture_manifest_path", "")
        registered_definitions.add(path.resolve())
        registered_manifests.add(manifest_path.resolve())
        if not path.is_file() or sha256_file(path) != entry.get("sha256"):
            errors.append(f"scenario registry {sid} definition missing or hash mismatch")
            continue
        if not manifest_path.is_file() or sha256_file(manifest_path) != entry.get("fixture_manifest_sha256"):
            errors.append(f"scenario registry {sid} manifest missing or hash mismatch")
        else:
            manifest = load_json(manifest_path)
            for ref in manifest.get("artifacts", {}).values() if isinstance(manifest.get("artifacts"), dict) else []:
                if isinstance(ref, dict) and isinstance(ref.get("path"), str):
                    registered_artifacts.add((ROOT / ref["path"]).resolve())
        scenario, scenario_errors = validate_current_scenario_v2(path, entry)
        scenario["_registry_entry"] = entry
        scenarios[sid] = scenario
        errors.extend(scenario_errors)
    if ids != sorted(ids) or len(ids) != len(set(ids)):
        errors.append("scenario registry IDs must be unique and sorted")
    definitions_on_disk = {item.resolve() for item in SCENARIO_DEFINITIONS.rglob("r*.json")}
    manifests_on_disk = {item.resolve() for item in SCENARIO_FIXTURES.rglob("r*.fixture.json")}
    artifacts_on_disk = {item.resolve() for item in SCENARIO_ARTIFACTS.rglob("*.json")}
    if registered_definitions != definitions_on_disk:
        errors.append(
            "scenario definition closure differs: "
            f"unregistered={sorted(str(item.relative_to(ROOT)) for item in definitions_on_disk - registered_definitions)}, "
            f"missing={sorted(str(item.relative_to(ROOT)) for item in registered_definitions - definitions_on_disk)}"
        )
    if registered_manifests != manifests_on_disk:
        errors.append(
            "scenario fixture-manifest closure differs: "
            f"unregistered={sorted(str(item.relative_to(ROOT)) for item in manifests_on_disk - registered_manifests)}, "
            f"missing={sorted(str(item.relative_to(ROOT)) for item in registered_manifests - manifests_on_disk)}"
        )
    if registered_artifacts != artifacts_on_disk:
        errors.append(
            "scenario artifact closure differs: "
            f"unregistered={sorted(str(item.relative_to(ROOT)) for item in artifacts_on_disk - registered_artifacts)}, "
            f"missing={sorted(str(item.relative_to(ROOT)) for item in registered_artifacts - artifacts_on_disk)}"
        )
    expected_artifact_count = len(entries) * 3
    if len(artifacts_on_disk) != expected_artifact_count:
        errors.append(
            f"scenario artifact set has {len(artifacts_on_disk)} files; expected exactly {expected_artifact_count}"
        )
    return scenarios, errors


def validate_packet_scenario_references_v2(path: Path, scenarios: dict[str, dict]) -> list[str]:
    errors: list[str] = []
    packet = load_json(path)
    packet_id = packet.get("id")
    references: list[tuple[str, object]] = []
    references += [("save_impact.golden_scenarios", sid) for sid in packet.get("save_impact", {}).get("golden_scenarios", [])]
    references += [(f"performance_metrics.{item.get('name')}", item.get("scenario")) for item in packet.get("performance_metrics", [])]
    references += [(f"rollout.health_signals.{item.get('name')}", item.get("scenario")) for item in packet.get("rollout", {}).get("health_signals", [])]
    for test in packet.get("acceptance_tests", []):
        scenario_id = test.get("scenario_id")
        if test.get("kind") in {"scenario", "save", "performance"} and not scenario_id:
            errors.append(f"{path.relative_to(ROOT)} {test.get('id')} must declare scenario_id")
        if scenario_id:
            references.append((f"acceptance_tests.{test.get('id')}", scenario_id))
            command_ids = re.findall(r"SCN_[A-Z0-9_]+", test.get("command") or "")
            if test.get("command") and command_ids != [scenario_id]:
                errors.append(f"{path.relative_to(ROOT)} {test.get('id')} command must reference only {scenario_id}")
    referenced_ids = {sid for _, sid in references if isinstance(sid, str)}
    for source, sid in references:
        scenario = scenarios.get(sid) if isinstance(sid, str) else None
        if scenario is None:
            errors.append(f"{path.relative_to(ROOT)} {source} references unknown scenario {sid!r}")
        elif packet_id not in scenario.get("applies_to_packets", []):
            errors.append(f"{path.relative_to(ROOT)} {source} uses {sid}, which does not apply to {packet_id}")
    pins = packet.get("scenario_pins", [])
    pin_ids = [pin.get("id") for pin in pins if isinstance(pin, dict)]
    if len(pin_ids) != len(set(pin_ids)) or set(pin_ids) != referenced_ids:
        errors.append(f"{path.relative_to(ROOT)} scenario pins must exactly equal references: pins={sorted(pin_ids)}, references={sorted(referenced_ids)}")
    for pin in pins:
        scenario = scenarios.get(pin.get("id")) if isinstance(pin, dict) else None
        if scenario is None:
            continue
        if pin != scenario.get("_registry_entry"):
            errors.append(f"{path.relative_to(ROOT)} has stale or incomplete pin for {pin.get('id')}")
    for signal in packet.get("rollout", {}).get("health_signals", []):
        if signal.get("name") == "required-test-failures":
            scenario = scenarios.get(signal.get("scenario"))
            if scenario and scenario.get("kind") != "aggregate":
                errors.append(f"{path.relative_to(ROOT)} required-test-failures must use an aggregate scenario")
            if scenario:
                parameters = {item.get("name"): item.get("value") for item in scenario.get("parameters", [])}
                required_count = sum(1 for test in packet.get("acceptance_tests", []) if test.get("required"))
                if parameters.get("required-acceptance-tests") != required_count:
                    errors.append(f"{path.relative_to(ROOT)} required test count differs from {signal.get('scenario')}")
                if parameters.get("minimum-runs") != packet.get("rollout", {}).get("minimum_runs"):
                    errors.append(f"{path.relative_to(ROOT)} minimum runs differs from aggregate scenario")
    for collection, prefix in ((packet.get("performance_metrics", []), "performance_metrics"), (packet.get("rollout", {}).get("health_signals", []), "rollout.health_signals")):
        for metric in collection:
            scenario = scenarios.get(metric.get("scenario"))
            if not scenario:
                continue
            matches = [item for item in scenario.get("oracles", []) if item.get("subject") == metric.get("name")]
            if len(matches) != 1:
                errors.append(f"{path.relative_to(ROOT)} {prefix}.{metric.get('name')} needs one matching oracle")
                continue
            oracle_value = matches[0]
            compatible = oracle_value.get("operator") == metric.get("comparator") or (oracle_value.get("operator") == "equal" and metric.get("comparator") in {"less-or-equal", "greater-or-equal"})
            if oracle_value.get("expected") != metric.get("target") or not compatible:
                errors.append(f"{path.relative_to(ROOT)} {prefix}.{metric.get('name')} differs from pinned oracle")
    return errors


def packet_contract_projection(packet: dict) -> dict:
    """Return immutable acceptance domain; lifecycle/evidence fields are excluded."""
    return {
        "contract_version": 1,
        "packet": {field: packet.get(field) for field in PACKET_CONTRACT_FIELDS},
    }


def packet_contract_sha256(packet: dict) -> str:
    return sha256_canonical_json(packet_contract_projection(packet))


def packet_path_is_covered(actual: str, declared: str) -> bool:
    if actual == declared:
        return True
    normalized = declared.rstrip("/")
    return declared.endswith("/") and actual.startswith(f"{normalized}/")


def artifact_is_content_addressed(artifact: object) -> bool:
    return (
        isinstance(artifact, dict)
        and isinstance(artifact.get("uri"), str)
        and artifact.get("uri")
        and not artifact["uri"].startswith("pending://")
        and isinstance(artifact.get("sha256"), str)
        and re.fullmatch(r"[0-9a-f]{64}", artifact["sha256"]) is not None
    )


def validate_wp0002_baseline_evidence_contract(packet: dict) -> list[str]:
    """Keep WP-0002 acceptance inputs immutable, resolved, and non-authoritative."""
    expected = [
        {
            "id": "BASE-WP0003",
            "type": "dependency-completion-evidence",
            "uri": "git-object://b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5/docs/evidence/WP-0003/TECHNICAL-SANDBOX-20260716.md",
            "sha256": "c381771d45bae630b16ab08c1eb11f63d0c9e563c6b7f77e0d7ea2952d5832ae",
        },
        {
            "id": "BASE-IDENTITY",
            "type": "identity-source",
            "uri": "git-object://b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5/docs/foundation-v0.1/ledger/decisions.jsonl",
            "sha256": "df5a4840f468676d6e3b428be1eda957310ef12c812241c5be3cdde6bd4601ef",
        },
        {
            "id": "BASE-CORE",
            "type": "constitutional-source",
            "uri": "git-object://b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5/docs/foundation-v0.1/00-GAME-CONSTITUTION.md",
            "sha256": "ff6d7938c14a074acf796619868b97551cefd1681147559983a293792c20afb8",
        },
        {
            "id": "BASE-CITY-COMPARISON",
            "type": "city-comparison-control",
            "uri": "git-object://b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5/docs/foundation-v0.1/02-SYSTEM-MAP.md",
            "sha256": "ee0ceb2e28d94b894735ddb15d2f32cd4f1403eeaece82e8f03506cf05293c68",
        },
        {
            "id": "BASE-CAMERA",
            "type": "camera-control-source",
            "uri": "git-object://b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5/docs/foundation-v0.1/05-ART-BIBLE.md",
            "sha256": "b1b3b950c13725651df61682d3b7b9c5374e995bb52a1f998b845cf00cd3483f",
        },
        {
            "id": "BASE-SLICE",
            "type": "slice-source",
            "uri": "git-object://b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5/docs/foundation-v0.1/03-VERTICAL-SLICE.md",
            "sha256": "ddf77882b81f54dcbb70a1d3d1d7347ba62be5447edfe2aac236941651c70b9b",
        },
    ]
    baseline = packet.get("baseline_evidence")
    if baseline != expected:
        return [
            "WP-0002 baseline evidence must equal the six protected, content-addressed source artifacts"
        ]
    if packet.get("status") in {
        "accepted",
        "active",
        "verifying",
        "candidate",
        "released",
        "rolled-back",
    } and any(not artifact_is_content_addressed(item) for item in baseline):
        return ["accepted or active WP-0002 baseline evidence cannot remain pending"]
    return []


def validate_status_event_chain(path: Path, packet: dict) -> list[str]:
    errors: list[str] = []
    label = str(path.relative_to(ROOT))
    events = packet.get("status_events", [])
    if not isinstance(events, list) or not events:
        return [f"{label} must retain a non-empty status event chain"]
    event_ids = [event.get("event_id") for event in events if isinstance(event, dict)]
    if len(event_ids) != len(events) or len(event_ids) != len(set(event_ids)):
        errors.append(f"{label} status event IDs must be present and globally unique within the packet")
    previous_to: str | None = None
    previous_at: datetime | None = None
    for index, event in enumerate(events):
        event_label = f"{label} status_events[{index}]"
        if not isinstance(event, dict):
            errors.append(f"{event_label} must be an object")
            continue
        from_status = event.get("from")
        to_status = event.get("to")
        at = parse_datetime(event.get("at"))
        if index == 0:
            if from_status is not None or to_status != "proposed":
                errors.append(f"{event_label} must be the null -> proposed genesis event")
        else:
            if from_status != previous_to:
                errors.append(
                    f"{event_label} from={from_status!r} breaks continuity after {previous_to!r}"
                )
            if from_status not in PACKET_TRANSITIONS or to_status not in PACKET_TRANSITIONS.get(from_status, set()):
                errors.append(f"{event_label} has forbidden transition {from_status!r} -> {to_status!r}")
        if to_status not in PACKET_STATUSES:
            errors.append(f"{event_label} has unknown destination status {to_status!r}")
        if at is None:
            errors.append(f"{event_label} has an invalid timezone-aware timestamp")
        elif previous_at is not None and at <= previous_at:
            errors.append(f"{event_label} is not strictly later than the preceding event")
        previous_to = to_status
        if at is not None:
            previous_at = at
    if previous_to != packet.get("status"):
        errors.append(f"{label} final status event does not equal packet status")
    activation_events = [
        event for event in events
        if isinstance(event, dict) and event.get("to") == "active"
    ]
    if len(activation_events) > 1:
        errors.append(f"{label} has multiple A1 activation events; open a new packet instead")
    return errors


def validate_work_packet_semantics(path: Path) -> list[str]:
    errors: list[str] = []
    packet = load_json(path)
    errors.extend(validate_packet_system_contracts(path, packet))
    calculated_contract = packet_contract_sha256(packet)
    if packet.get("contract_sha256") != calculated_contract:
        errors.append(
            f"{path.relative_to(ROOT)} contract_sha256 does not match canonical packet-contract-v1: "
            f"expected {calculated_contract}"
        )
    if packet.get("id") == "WP-0002":
        errors.extend(validate_wp0002_baseline_evidence_contract(packet))
    rank = {"low": 0, "medium": 1, "high": 2, "constitutional": 3}
    declared = packet.get("declared_risk")
    derived = packet.get("derived_risk")
    effective = packet.get("effective_risk")
    if derived is not None and effective != max((declared, derived), key=lambda item: rank[item]):
        errors.append(f"{path.relative_to(ROOT)} effective risk is not max(declared, derived)")
    if packet.get("class") == "architecture" and rank.get(effective, -1) < rank["high"]:
        errors.append(f"{path.relative_to(ROOT)} architecture work derives at least high risk")
    if {"governance", "save"} & set(packet.get("affected_domains", [])) and rank.get(effective, -1) < rank["high"]:
        errors.append(f"{path.relative_to(ROOT)} governance/save work derives at least high risk")
    principals = [packet.get(field) for field in ("implementer", "verifier", "integrator") if packet.get(field)]
    if len(principals) != len(set(principals)):
        errors.append(f"{path.relative_to(ROOT)} reuses a principal across implementation/verification/integration")
    if packet.get("status") not in {"proposed", "rejected", "superseded"}:
        if not packet.get("approved_by") or not packet.get("approval_receipt_id"):
            errors.append(f"{path.relative_to(ROOT)} advanced without approval receipt")
    if packet.get("status") in {"candidate", "released"}:
        if packet.get("verification", {}).get("status") != "passed" or not packet.get("verifier"):
            errors.append(f"{path.relative_to(ROOT)} candidate/release lacks independent passing verification")
    if packet.get("status") == "released" and not (packet.get("release_id") and packet.get("integrator")):
        errors.append(f"{path.relative_to(ROOT)} released without release ID/integrator")
    errors.extend(validate_status_event_chain(path, packet))
    test_ids = [test.get("id") for test in packet.get("acceptance_tests", [])]
    if len(test_ids) != len(set(test_ids)):
        errors.append(f"{path.relative_to(ROOT)} acceptance test IDs are not unique")
    if packet.get("status") not in {"proposed", "rejected", "superseded"}:
        for test in packet.get("acceptance_tests", []):
            if test.get("required") and not test.get("command") and test.get("kind") != "manual":
                errors.append(f"{path.relative_to(ROOT)} accepted required test lacks a command: {test.get('id')}")
    declared_paths = packet.get("declared_paths", [])
    actual_paths = packet.get("actual_paths", [])
    reserved_paths = packet.get("reservation", {}).get("paths", [])
    for candidate_path in declared_paths + actual_paths + reserved_paths:
        if candidate_path.startswith("/") or ".." in Path(candidate_path).parts:
            errors.append(f"{path.relative_to(ROOT)} has unsafe path: {candidate_path}")
    for actual_path in actual_paths:
        if not any(packet_path_is_covered(actual_path, declared) for declared in declared_paths):
            errors.append(
                f"{path.relative_to(ROOT)} actual path is outside declared scope: {actual_path}"
            )
        if reserved_paths and not any(
            packet_path_is_covered(actual_path, reserved) for reserved in reserved_paths
        ):
            errors.append(
                f"{path.relative_to(ROOT)} actual path is outside its reservation: {actual_path}"
            )

    evidence = packet.get("evidence_manifest", [])
    evidence_by_id = {
        item.get("id"): item for item in evidence if isinstance(item, dict)
    }
    if len(evidence_by_id) != len(evidence):
        errors.append(f"{path.relative_to(ROOT)} evidence manifest IDs must be unique")
    status_events = packet.get("status_events", [])
    candidate_evidence_materialized = packet.get("status") in {
        "verifying",
        "candidate",
        "released",
    } or any(
        isinstance(event, dict)
        and event.get("to") in {"verifying", "candidate", "released"}
        for event in status_events
    )
    if candidate_evidence_materialized:
        if not actual_paths:
            errors.append(
                f"{path.relative_to(ROOT)} packet with candidate history has no retained actual paths"
            )
        candidate = packet.get("candidate_evidence", {})
        reference_fields = (
            "diff_artifact_id", "artifact_manifest_id", "command_log_artifact_id"
        )
        references = [candidate.get(field) for field in reference_fields]
        if any(not isinstance(item, str) or not item for item in references):
                errors.append(
                    f"{path.relative_to(ROOT)} packet with candidate history lacks complete diff/artifact/command evidence references"
                )
        elif len(references) != len(set(references)):
            errors.append(f"{path.relative_to(ROOT)} candidate evidence references must be distinct")
        for evidence_id in references:
            artifact = evidence_by_id.get(evidence_id)
            if artifact is None:
                errors.append(
                    f"{path.relative_to(ROOT)} candidate evidence references unknown artifact {evidence_id!r}"
                )
            elif not artifact_is_content_addressed(artifact):
                errors.append(
                    f"{path.relative_to(ROOT)} candidate artifact {evidence_id!r} is not content-addressed"
                )
    return errors


def supersession_authority_codes(
    predecessor: dict,
    successor: dict,
    receipt: dict | None,
) -> list[str]:
    """Return stable policy codes for an invalid supersession edge."""
    codes: list[str] = []
    predecessor_is_creator = predecessor.get("authority") in CREATOR_AUTHORITIES
    successor_is_creator = successor.get("authority") in CREATOR_AUTHORITIES
    if predecessor_is_creator and not successor_is_creator:
        codes.append("lower-authority-cannot-supersede-creator-authority")
    if predecessor.get("class") == "constitutional" and predecessor_is_creator:
        if successor.get("authority") != "creator-ratification":
            codes.append("constitutional-successor-requires-exact-creator-ratification-authority")
        protected = (
            isinstance(receipt, dict)
            and receipt.get("receipt_id") == successor.get("approval_receipt_id")
            and receipt.get("issuer_role") == "creator"
            and receipt.get("receipt_kind") == "decision-ratification"
            and receipt.get("sealed") is True
            and bool(receipt.get("accepted_commit"))
            and successor.get("id") in receipt.get("subject_ids", [])
            and receipt.get("subject_event_sha256", {}).get(successor.get("id"))
                == successor.get("event_hash")
        )
        if not protected:
            codes.append(
                "creator-constitutional-supersession-requires-protected-receipt"
            )
    return codes


def validate_supersession_graph(records: list[dict]) -> list[str]:
    """Enforce one global, acyclic, non-forked successor chain per decision root."""
    errors: list[str] = []
    records_by_id = {record.get("id"): record for record in records}
    children: dict[str, list[str]] = {}
    for record in records:
        predecessor = record.get("supersedes")
        if predecessor:
            children.setdefault(predecessor, []).append(record.get("id"))
    for predecessor, successors in sorted(children.items()):
        if len(successors) != 1:
            errors.append(f"decision supersession forks at {predecessor}: {successors}")
    for start in records_by_id:
        visited: set[str] = set()
        current = start
        while current in children and len(children[current]) == 1:
            if current in visited:
                errors.append(f"decision supersession cycle reaches {current}")
                break
            visited.add(current)
            current = children[current][0]
    heads = set(records_by_id) - set(children)
    expected_heads = {
        record_id for record_id in records_by_id
        if not any(record.get("supersedes") == record_id for record in records)
    }
    if heads != expected_heads:
        errors.append("decision supersession head derivation is inconsistent")
    return errors


def validate_supersession_authority(
    records: list[dict], receipts: list[dict]
) -> list[str]:
    errors: list[str] = []
    records_by_id = {record["id"]: record for record in records}
    receipts_by_id = {receipt["receipt_id"]: receipt for receipt in receipts}
    for successor in records:
        predecessor_id = successor.get("supersedes")
        if not predecessor_id or predecessor_id not in records_by_id:
            continue
        predecessor = records_by_id[predecessor_id]
        receipt = receipts_by_id.get(successor.get("approval_receipt_id"))
        for code in supersession_authority_codes(predecessor, successor, receipt):
            errors.append(
                f"{successor['id']} cannot supersede {predecessor_id}: {code}"
            )
    return errors


def validate_supersession_fixtures() -> list[str]:
    errors: list[str] = []
    seen_case_ids: set[str] = set()
    fixture_dir = ROOT / "governance" / "fixtures"
    fixture_paths = sorted(fixture_dir.glob("supersession-authority.*.json"))
    if len(fixture_paths) != 2:
        errors.append("supersession authority fixtures must include one valid and one invalid file")
    for fixture_path in fixture_paths:
        fixture = load_json(fixture_path)
        cases = fixture.get("cases")
        if not isinstance(cases, list) or not cases:
            errors.append(f"{fixture_path.relative_to(ROOT)} must contain cases")
            continue
        for case in cases:
            if not isinstance(case, dict):
                errors.append(f"{fixture_path.relative_to(ROOT)} has a non-object case")
                continue
            case_id = case.get("id")
            if not isinstance(case_id, str) or not case_id:
                errors.append(f"{fixture_path.relative_to(ROOT)} has a case without an ID")
                continue
            if case_id in seen_case_ids:
                errors.append(f"duplicate supersession fixture case ID: {case_id}")
            seen_case_ids.add(case_id)
            codes = supersession_authority_codes(
                case.get("predecessor", {}),
                case.get("successor", {}),
                case.get("receipt"),
            )
            expected_valid = case.get("expected_valid")
            if expected_valid is True and codes:
                errors.append(f"{case_id} expected valid but produced {codes}")
            elif expected_valid is False:
                expected_error = case.get("expected_error")
                if expected_error not in codes:
                    errors.append(
                        f"{case_id} expected {expected_error!r} but produced {codes}"
                    )
            else:
                if expected_valid not in {True, False}:
                    errors.append(f"{case_id} expected_valid must be boolean")
    return errors


def parse_datetime(value: object) -> datetime | None:
    if not isinstance(value, str) or not value:
        return None
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None
    if parsed.tzinfo is None:
        return None
    return parsed.astimezone(timezone.utc)


def subject_claims(receipt: dict) -> dict[str, set[str]]:
    return {
        item.get("subject_id"): set(item.get("claims", []))
        for item in receipt.get("subject_claims", [])
        if isinstance(item, dict)
        and isinstance(item.get("subject_id"), str)
        and isinstance(item.get("claims"), list)
    }


def normalize_repo_relative_path(value: object) -> str | None:
    """Return one unambiguous repository-relative POSIX path without a trailing slash."""
    if not isinstance(value, str) or not value or "\\" in value:
        return None
    candidate = Path(value)
    if candidate.is_absolute() or any(part in {"", ".", ".."} for part in candidate.parts):
        return None
    normalized = candidate.as_posix().rstrip("/")
    return normalized or None


def repo_paths_overlap(left: object, right: object) -> bool:
    """Treat equality and ancestor/descendant relationships as path overlap."""
    left_path = normalize_repo_relative_path(left)
    right_path = normalize_repo_relative_path(right)
    if left_path is None or right_path is None:
        return False
    return (
        left_path == right_path
        or left_path.startswith(f"{right_path}/")
        or right_path.startswith(f"{left_path}/")
    )


def repo_path_is_git_ignored(path: str) -> bool:
    """Probe ignore policy without creating the scratch directory."""
    normalized = normalize_repo_relative_path(path)
    if normalized is None:
        return False
    result = subprocess.run(
        [
            "git",
            "-C",
            str(REPO_ROOT),
            "check-ignore",
            "-q",
            "--no-index",
            "--",
            f"{normalized}/.a1-boundary-ignore-probe",
        ],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        check=False,
    )
    return result.returncode == 0


def a1_scratch_boundary_codes(
    packet_id: object,
    reservation_paths: object,
    protection: object,
) -> list[str]:
    """Return stable policy codes for an invalid non-output scratch boundary."""
    if not isinstance(protection, dict):
        return ["scratch-boundary-invalid"]
    scratch_paths = protection.get("ephemeral_scratch_paths")
    if not isinstance(scratch_paths, list):
        return ["scratch-path-list-invalid"]

    codes: set[str] = set()
    scratch_values_are_strings = all(
        isinstance(scratch_path, str) for scratch_path in scratch_paths
    )
    if packet_id == "WP-0001" and (
        not scratch_values_are_strings
        or scratch_paths != list(WP0001_UNITY_EPHEMERAL_SCRATCH_PATHS)
    ):
        codes.add("wp0001-scratch-set-mismatch")
    if protection.get("scratch_destroy_on_close") is not True:
        codes.add("scratch-not-destroyed-on-close")

    normalized_scratch: list[str] = []
    for scratch_path in scratch_paths:
        normalized = normalize_repo_relative_path(scratch_path)
        if normalized is None:
            codes.add("scratch-path-unsafe")
            continue
        normalized_scratch.append(normalized)
        if not repo_path_is_git_ignored(scratch_path):
            codes.add("scratch-not-gitignored")

    reserved = reservation_paths if isinstance(reservation_paths, list) else []
    protected = protection.get("protected_paths", [])
    protected_paths = protected if isinstance(protected, list) else []
    if any(
        repo_paths_overlap(scratch_path, reserved_path)
        for scratch_path in normalized_scratch
        for reserved_path in reserved
    ):
        codes.add("scratch-reservation-overlap")
    if any(
        repo_paths_overlap(scratch_path, protected_path)
        for scratch_path in normalized_scratch
        for protected_path in protected_paths
    ):
        codes.add("scratch-protected-overlap")
    if any(
        repo_paths_overlap(left_path, right_path)
        for left_index, left_path in enumerate(normalized_scratch)
        for right_path in normalized_scratch[left_index + 1 :]
    ):
        codes.add("scratch-internal-overlap")
    return sorted(codes)


def validate_a1_scratch_fixtures() -> list[str]:
    errors: list[str] = []
    fixture_path = ROOT / "governance" / "fixtures" / "a1-scratch-boundary.fixtures.json"
    if not fixture_path.is_file():
        return ["A1 scratch-boundary fixtures are missing"]
    fixture = load_json(fixture_path)
    cases = fixture.get("cases")
    if not isinstance(cases, list) or not cases:
        return [f"{fixture_path.relative_to(ROOT)} must contain cases"]

    seen_case_ids: set[str] = set()
    for case in cases:
        if not isinstance(case, dict):
            errors.append(f"{fixture_path.relative_to(ROOT)} has a non-object case")
            continue
        case_id = case.get("id")
        if not isinstance(case_id, str) or not case_id:
            errors.append(f"{fixture_path.relative_to(ROOT)} has a case without an ID")
            continue
        if case_id in seen_case_ids:
            errors.append(f"duplicate A1 scratch fixture case ID: {case_id}")
        seen_case_ids.add(case_id)
        expected_codes = case.get("expected_codes")
        if (
            not isinstance(expected_codes, list)
            or any(not isinstance(code, str) for code in expected_codes)
            or len(expected_codes) != len(set(expected_codes))
        ):
            errors.append(f"{case_id} expected_codes must be a unique string array")
            continue
        actual_codes = a1_scratch_boundary_codes(
            case.get("packet_id"),
            case.get("reservation_paths"),
            case.get("protection_boundary"),
        )
        if actual_codes != sorted(expected_codes):
            errors.append(
                f"{case_id} expected {sorted(expected_codes)} but produced {actual_codes}"
            )
    return errors


def normalize_absolute_runtime_root(value: object) -> str | None:
    """Accept one exact canonical-looking absolute root without aliases or traversal."""
    if (
        not isinstance(value, str)
        or not value.startswith("/")
        or value == "/"
        or value.endswith("/")
        or "\\" in value
        or "//" in value
    ):
        return None
    if any(part in {"", ".", ".."} for part in value.split("/")[1:]):
        return None
    return value


def absolute_runtime_paths_overlap(left: object, right: object) -> bool:
    left_path = normalize_absolute_runtime_root(left)
    right_path = normalize_absolute_runtime_root(right)
    if left_path is None or right_path is None:
        return False
    return (
        left_path == right_path
        or left_path.startswith(f"{right_path}/")
        or right_path.startswith(f"{left_path}/")
    )


def a1_runtime_boundary_codes(
    environment: object,
    runtime: object,
) -> list[str]:
    """Return stable policy codes for an invalid disposable process boundary."""
    codes: set[str] = set()
    if not isinstance(environment, dict):
        codes.add("runtime-environment-invalid")
        environment = {}
    for field in ("sandbox_profile_sha256", "network_policy_sha256"):
        value = environment.get(field)
        if not isinstance(value, str) or re.fullmatch(r"[0-9a-f]{64}", value) is None:
            codes.add("runtime-policy-hash-invalid")

    if not isinstance(runtime, dict):
        return sorted(codes | {"runtime-boundary-invalid"})
    if runtime.get("isolation_mode") not in {
        "dedicated-ephemeral-os-user",
        "equivalent-os-sandbox",
    }:
        codes.add("runtime-isolation-mode-invalid")
    if not isinstance(runtime.get("principal_uid"), int) or isinstance(
        runtime.get("principal_uid"), bool
    ) or runtime.get("principal_uid", 0) < 1:
        codes.add("runtime-principal-invalid")
    if re.fullmatch(
        r"[0-9a-f]{64}",
        str(runtime.get("boot_session_sha256")),
    ) is None:
        codes.add("runtime-boot-session-invalid")

    home_root = normalize_absolute_runtime_root(runtime.get("ephemeral_home_root"))
    temp_root = normalize_absolute_runtime_root(runtime.get("private_temp_root"))
    if home_root is None or temp_root is None:
        codes.add("runtime-root-unsafe")
    elif absolute_runtime_paths_overlap(home_root, temp_root):
        codes.add("runtime-roots-overlap")

    bindings = runtime.get("environment_bindings")
    expected_bindings = {
        "HOME": home_root,
        "TMPDIR": temp_root,
        "TMP": temp_root,
        "TEMP": temp_root,
    }
    if not isinstance(bindings, dict) or bindings != expected_bindings:
        codes.add("runtime-environment-binding-mismatch")

    host_home = normalize_absolute_runtime_root(runtime.get("ambient_host_home_root"))
    shared_values = runtime.get("ambient_shared_temp_roots")
    shared_roots = (
        [normalize_absolute_runtime_root(value) for value in shared_values]
        if isinstance(shared_values, list)
        else []
    )
    if (
        host_home is None
        or not isinstance(shared_values, list)
        or not shared_values
        or any(value is None for value in shared_roots)
    ):
        codes.add("ambient-root-invalid")
    denied_roots = runtime.get("denied_ambient_write_roots")
    expected_denials = (
        [host_home, *shared_roots]
        if host_home is not None and all(value is not None for value in shared_roots)
        else []
    )
    if not isinstance(denied_roots, list) or denied_roots != expected_denials:
        codes.add("ambient-write-denial-mismatch")
    if (
        runtime.get("ambient_host_home_write_denied") is not True
        or runtime.get("ambient_shared_temp_write_denied") is not True
    ):
        codes.add("ambient-write-denial-disabled")
    exception_values = runtime.get("ambient_shared_temp_write_exceptions")
    exceptions = (
        [normalize_absolute_runtime_root(value) for value in exception_values]
        if isinstance(exception_values, list)
        else []
    )
    if (
        not isinstance(exception_values, list)
        or len(exception_values) != len(set(exception_values))
        or any(value is None for value in exceptions)
    ):
        codes.add("ambient-write-exception-invalid")
    elif any(
        not any(
            isinstance(shared_root, str)
            and isinstance(exception, str)
            and exception.startswith(f"{shared_root}/")
            for shared_root in shared_roots
        )
        for exception in exceptions
    ):
        codes.add("ambient-write-exception-outside-shared-temp")

    ambient_roots = [
        value for value in [host_home, *shared_roots] if isinstance(value, str)
    ]
    if home_root is not None and any(
        absolute_runtime_paths_overlap(home_root, ambient_root)
        for ambient_root in ambient_roots
    ):
        codes.add("runtime-home-overlaps-ambient")
    if temp_root is not None and any(
        absolute_runtime_paths_overlap(temp_root, ambient_root)
        for ambient_root in ambient_roots
    ):
        codes.add("runtime-temp-overlaps-ambient")

    roots_to_check = [
        value
        for value in [home_root, temp_root, host_home, *shared_roots, *exceptions]
        if isinstance(value, str)
    ]
    if any(Path(value).resolve(strict=False).as_posix() != value for value in roots_to_check):
        codes.add("runtime-root-symlink-escape")
    if (
        runtime.get("symlink_escape_forbidden") is not True
        or runtime.get("runtime_roots_symlink_free") is not True
    ):
        codes.add("runtime-symlink-guard-disabled")
    if runtime.get("runtime_roots_importable") is not False:
        codes.add("runtime-roots-importable")
    if runtime.get("destroy_on_close") is not True:
        codes.add("runtime-not-destroyed-on-close")
    return sorted(codes)


def validate_a1_runtime_fixtures() -> list[str]:
    errors: list[str] = []
    fixture_path = ROOT / "governance" / "fixtures" / "a1-runtime-boundary.fixtures.json"
    if not fixture_path.is_file():
        return ["A1 runtime-boundary fixtures are missing"]
    fixture = load_json(fixture_path)
    cases = fixture.get("cases")
    if not isinstance(cases, list) or not cases:
        return [f"{fixture_path.relative_to(ROOT)} must contain cases"]

    seen_case_ids: set[str] = set()
    for case in cases:
        if not isinstance(case, dict):
            errors.append(f"{fixture_path.relative_to(ROOT)} has a non-object case")
            continue
        case_id = case.get("id")
        if not isinstance(case_id, str) or not case_id:
            errors.append(f"{fixture_path.relative_to(ROOT)} has a case without an ID")
            continue
        if case_id in seen_case_ids:
            errors.append(f"duplicate A1 runtime fixture case ID: {case_id}")
        seen_case_ids.add(case_id)
        expected_codes = case.get("expected_codes")
        if (
            not isinstance(expected_codes, list)
            or any(not isinstance(code, str) for code in expected_codes)
            or len(expected_codes) != len(set(expected_codes))
        ):
            errors.append(f"{case_id} expected_codes must be a unique string array")
            continue
        actual_codes = a1_runtime_boundary_codes(
            case.get("approved_environment"),
            case.get("runtime_boundary"),
        )
        if actual_codes != sorted(expected_codes):
            errors.append(
                f"{case_id} expected {sorted(expected_codes)} but produced {actual_codes}"
            )
    return errors


def code_identity_shape_valid(value: object) -> bool:
    return (
        isinstance(value, dict)
        and set(value)
        == {
            "verification_scope",
            "identifier",
            "team_identifier",
            "cdhash",
            "designated_requirement_sha256",
            "authorities_sha256",
        }
        and value.get("verification_scope") == "codesign-strict-component"
        and isinstance(value.get("identifier"), str)
        and bool(value.get("identifier"))
        and re.fullmatch(r"[A-Z0-9]{10}", str(value.get("team_identifier")))
        is not None
        and re.fullmatch(r"[0-9a-f]{40}", str(value.get("cdhash"))) is not None
        and re.fullmatch(
            r"[0-9a-f]{64}",
            str(value.get("designated_requirement_sha256")),
        )
        is not None
        and re.fullmatch(
            r"[0-9a-f]{64}",
            str(value.get("authorities_sha256")),
        )
        is not None
    )


def a1_wp0001_boundary_codes(
    repository: object,
    runtime: object,
    project_seed: object,
    route: object,
    approved_toolchain: object = None,
    approved_environment: object = None,
    toolchain_profile: object = None,
    activation_evidence: object = None,
    raw_capture_collectors: object = None,
    observed_seed_tree_sha256: str | None = None,
) -> list[str]:
    """Return stable policy codes for the WP-0001 protected seed and MCP route."""
    codes: set[str] = set()
    if not isinstance(repository, dict):
        repository = {}
        codes.add("wp0001-repository-invalid")
    if not isinstance(runtime, dict):
        runtime = {}
        codes.add("wp0001-runtime-invalid")
    if not isinstance(project_seed, dict):
        project_seed = {}
        codes.add("wp0001-project-seed-invalid")
    if not isinstance(route, dict):
        route = {}
        codes.add("wp0001-unity-mcp-route-invalid")
    if not isinstance(toolchain_profile, dict):
        toolchain_profile = {}
        codes.add("wp0001-toolchain-profile-invalid")
    if not isinstance(activation_evidence, dict):
        activation_evidence = {}
        codes.add("wp0001-activation-evidence-invalid")
    if not isinstance(raw_capture_collectors, dict):
        raw_capture_collectors = {}
        codes.add("wp0001-raw-collector-authority-invalid")

    repository_root = normalize_absolute_runtime_root(repository.get("absolute_root"))
    if repository_root is None:
        codes.add("wp0001-repository-root-invalid")
    if (
        repository.get("git_directory") != ".git"
        or repository.get("git_common_directory") != ".git"
        or repository.get("detached_head") is not True
        or repository.get("remote_count") != 0
        or repository.get("alternates_present") is not False
    ):
        codes.add("wp0001-repository-isolation-invalid")

    if project_seed.get("mode") != "creator-created-protected-base":
        codes.add("wp0001-project-seed-mode-invalid")
    if project_seed.get("project_root") != "Game":
        codes.add("wp0001-project-seed-root-invalid")
    if project_seed.get("base_commit") != repository.get("base_commit"):
        codes.add("wp0001-project-seed-base-mismatch")
    if project_seed.get("creator_attested_no_implementation") is not True:
        codes.add("wp0001-project-seed-attestation-missing")
    seed_evidence = project_seed.get("evidence")
    if (
        not isinstance(seed_evidence, dict)
        or seed_evidence.get("path") != WP0001_PROJECT_SEED_EVIDENCE_PATH
    ):
        codes.add("wp0001-project-seed-evidence-invalid")
    if (
        observed_seed_tree_sha256 is not None
        and project_seed.get("git_tree_sha256") != observed_seed_tree_sha256
    ):
        codes.add("wp0001-project-seed-tree-mismatch")

    exact_profile = {
        "hub_version": "3.19.5",
        "editor_version": "6000.3.19f1",
        "editor_changeset": "7689f4515d75",
        "editor_architecture": "arm64",
        "mac_il2cpp_installed": True,
        "mac_il2cpp_editor_version": "6000.3.19f1",
        "xcode_version": "26.3",
        "rosetta_installed": True,
        "dotnet_sdk_version": "10.0.301",
        "assistant_package_version": "2.14.0-pre.1",
        "mono_iteration_authorized": True,
        "il2cpp_arm64_acceptance": True,
    }
    if any(toolchain_profile.get(key) != value for key, value in exact_profile.items()):
        codes.add("wp0001-toolchain-profile-mismatch")
    resolved_urp = toolchain_profile.get("resolved_urp_version")
    resolved_tests = toolchain_profile.get("resolved_test_framework_version")
    if (
        not isinstance(resolved_urp, str)
        or re.fullmatch(r"17\.3(?:\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?)?", resolved_urp)
        is None
        or not isinstance(resolved_tests, str)
        or re.fullmatch(r"1\.6(?:\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?)?", resolved_tests)
        is None
    ):
        codes.add("wp0001-toolchain-package-line-mismatch")
    expected_toolchain_records = {
        **WP0001_REQUIRED_TOOLCHAIN_VERSIONS,
        "URP": resolved_urp,
        "Unity Test Framework": resolved_tests,
    }
    toolchain_items = approved_toolchain if isinstance(approved_toolchain, list) else []
    toolchain_names = [
        item.get("name") for item in toolchain_items if isinstance(item, dict)
    ]
    toolchain_records = {
        item.get("name"): item.get("version")
        for item in toolchain_items
        if isinstance(item, dict)
        and isinstance(item.get("name"), str)
        and isinstance(item.get("version"), str)
    }
    if (
        len(toolchain_items) != len(expected_toolchain_records)
        or len(toolchain_names) != len(set(toolchain_names))
        or set(toolchain_names) != set(expected_toolchain_records)
        or any(
            toolchain_records.get(name) != version
            for name, version in expected_toolchain_records.items()
        )
        or any(
            not isinstance(item, dict)
            or not isinstance(item.get("source"), str)
            or not item.get("source")
            or re.fullmatch(r"[0-9a-f]{64}", str(item.get("sha256"))) is None
            for item in toolchain_items
        )
    ):
        codes.add("wp0001-approved-toolchain-mismatch")

    expected_activation_paths = {
        "manifest": "docs/evidence/WP-0001/a1-activation/evidence-manifest.json",
        "toolchain": "docs/evidence/WP-0001/a1-activation/toolchain.json",
        "quarantine": "docs/evidence/WP-0001/a1-activation/quarantine.json",
        "route": "docs/evidence/WP-0001/a1-activation/mcp-route.json",
        "activation_session": "docs/evidence/WP-0001/a1-activation/activation-session.json",
        "deviations": "docs/evidence/WP-0001/a1-activation/deviations.json",
        "sandbox_policy": "docs/evidence/WP-0001/a1-activation/sandbox.policy",
        "network_policy": "docs/evidence/WP-0001/a1-activation/network.policy",
    }
    for key, expected_path in expected_activation_paths.items():
        reference = activation_evidence.get(key)
        if (
            not isinstance(reference, dict)
            or reference.get("path") != expected_path
            or re.fullmatch(r"[0-9a-f]{64}", str(reference.get("sha256"))) is None
        ):
            codes.add("wp0001-activation-evidence-invalid")
    sandbox_policy_ref = (
        activation_evidence.get("sandbox_policy")
        if isinstance(activation_evidence.get("sandbox_policy"), dict)
        else {}
    )
    network_policy_ref = (
        activation_evidence.get("network_policy")
        if isinstance(activation_evidence.get("network_policy"), dict)
        else {}
    )
    if isinstance(approved_environment, dict):
        if (
            sandbox_policy_ref.get("sha256")
            != approved_environment.get("sandbox_profile_sha256")
            or network_policy_ref.get("sha256")
            != approved_environment.get("network_policy_sha256")
        ):
            codes.add("wp0001-policy-evidence-mismatch")
    else:
        codes.add("wp0001-approved-environment-invalid")

    expected_collectors = {
        "protocol": (
            "docs/foundation-v0.1/tools/capture_wp0001_protocol.py"
        ),
        "network": (
            "docs/foundation-v0.1/tools/capture_wp0001_network.py"
        ),
        "policy_attachment": (
            "docs/foundation-v0.1/tools/"
            "capture_wp0001_policy_attachment.py"
        ),
    }
    if (
        set(raw_capture_collectors)
        != {
            "protocol",
            "network",
            "policy_attachment",
            "authority_claim",
        }
        or raw_capture_collectors.get("authority_claim")
        != "AUTHORIZE-WP0001-RAW-COLLECTORS"
        or any(
            not isinstance(raw_capture_collectors.get(kind), dict)
            or set(raw_capture_collectors[kind]) != {"path", "sha256"}
            or raw_capture_collectors[kind].get("path") != expected_path
            or re.fullmatch(
                r"[0-9a-f]{64}",
                str(raw_capture_collectors[kind].get("sha256")),
            )
            is None
            or raw_capture_collectors[kind].get("sha256") == "0" * 64
            for kind, expected_path in expected_collectors.items()
        )
    ):
        codes.add("wp0001-raw-collector-authority-invalid")

    client = route.get("client") if isinstance(route.get("client"), dict) else {}
    relay = route.get("relay") if isinstance(route.get("relay"), dict) else {}
    bridge = route.get("bridge") if isinstance(route.get("bridge"), dict) else {}
    entitlement = (
        route.get("entitlement") if isinstance(route.get("entitlement"), dict) else {}
    )
    identity = (
        route.get("project_identity")
        if isinstance(route.get("project_identity"), dict)
        else {}
    )
    policy = (
        route.get("codex_policy") if isinstance(route.get("codex_policy"), dict) else {}
    )
    process_observation = (
        route.get("process_observation")
        if isinstance(route.get("process_observation"), dict)
        else {}
    )
    connection = (
        route.get("connection") if isinstance(route.get("connection"), dict) else {}
    )
    handshake = (
        route.get("handshake") if isinstance(route.get("handshake"), dict) else {}
    )
    activation_session = (
        route.get("activation_session")
        if isinstance(route.get("activation_session"), dict)
        else {}
    )
    controls = (
        route.get("controls") if isinstance(route.get("controls"), dict) else {}
    )

    if route.get("route") != "UNITY-MCP-EXTERNAL":
        codes.add("wp0001-route-selection-mismatch")
    if (
        route.get("code_identity_authority_claim")
        != "AUTHORIZE-WP0001-CODE-IDENTITIES"
    ):
        codes.add("wp0001-code-identity-authority-invalid")
    expected_target = f"{repository_root}/Game" if repository_root else None
    if bridge.get("project_path") != expected_target:
        codes.add("wp0001-mcp-project-target-mismatch")
    editor_pid = bridge.get("editor_pid")
    expected_arguments = (
        [
            "--mcp",
            "--project-path",
            expected_target,
            "--instance-id",
            str(editor_pid),
        ]
        if expected_target is not None and isinstance(editor_pid, int)
        else None
    )
    if relay.get("arguments") != expected_arguments:
        codes.add("wp0001-mcp-relay-arguments-mismatch")
    if relay.get("parent_pid") != client.get("pid"):
        codes.add("wp0001-mcp-process-parent-mismatch")
    if client.get("cwd") != repository_root:
        codes.add("wp0001-mcp-client-cwd-mismatch")
    editor_tool_hash = next(
        (
            item.get("sha256")
            for item in toolchain_items
            if isinstance(item, dict)
            and item.get("name") == "Unity Editor ARM64"
        ),
        None,
    )
    package_copy_path = relay.get("package_copy_path")
    if (
        any(
            re.fullmatch(r"[0-9a-f]{64}", str(value)) is None
            or value == "0" * 64
            for value in (
                client.get("sha256"),
                relay.get("sha256"),
                relay.get("package_copy_sha256"),
                bridge.get("editor_sha256"),
                bridge.get("connection_file_sha256"),
                client.get("environment_names_sha256"),
                relay.get("environment_names_sha256"),
                bridge.get("environment_names_sha256"),
            )
        )
        or not code_identity_shape_valid(client.get("signing_identity"))
        or not code_identity_shape_valid(relay.get("signing_identity"))
        or not code_identity_shape_valid(bridge.get("signing_identity"))
        or not isinstance(client.get("arguments"), list)
        or not client.get("arguments")
        or not client_arguments_policy_safe(client.get("arguments"))
        or not isinstance(bridge.get("arguments"), list)
        or not bridge.get("arguments")
        or not editor_arguments_policy_safe(
            bridge.get("arguments"),
            expected_target,
        )
        or bridge.get("cwd") != expected_target
        or bridge.get("editor_version") != "6000.3.19f1"
        or bridge.get("editor_changeset") != "7689f4515d75"
        or bridge.get("editor_sha256") != editor_tool_hash
        or not isinstance(package_copy_path, str)
        or not package_copy_path.startswith(
            f"{expected_target}/Library/PackageCache/com.unity.ai.assistant@"
        )
    ):
        codes.add("wp0001-mcp-process-identity-invalid")

    principal_uid = runtime.get("principal_uid")
    if {
        client.get("principal_uid"),
        relay.get("principal_uid"),
        bridge.get("principal_uid"),
    } != {principal_uid}:
        codes.add("wp0001-mcp-principal-mismatch")

    expected_bindings = runtime.get("environment_bindings")
    if policy.get("environment_bindings") != expected_bindings:
        codes.add("wp0001-mcp-environment-mismatch")
    home_root = runtime.get("ephemeral_home_root")
    expected_client_environment = (
        {
            "CODEX_HOME": f"{home_root}/.codex",
            "XDG_CONFIG_HOME": f"{home_root}/.config",
            "XDG_CACHE_HOME": f"{home_root}/.cache",
            "XDG_DATA_HOME": f"{home_root}/.local/share",
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_CONFIG_GLOBAL": f"{home_root}/.gitconfig",
            "GIT_TERMINAL_PROMPT": "0",
            "absent_variables": list(
                WP0001_CLIENT_ABSENT_ENVIRONMENT_VARIABLES
            ),
        }
        if isinstance(home_root, str)
        else None
    )
    canonical_environment_bytes = (
        json.dumps(
            expected_client_environment,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")
        if isinstance(expected_client_environment, dict)
        else b""
    )
    expected_environment_sha256 = hashlib.sha256(
        canonical_environment_bytes
    ).hexdigest()
    if (
        runtime.get("client_environment_guard") != expected_client_environment
        or policy.get("client_environment_guard") != expected_client_environment
        or policy.get("environment_sha256") != expected_environment_sha256
        or client.get("environment_sha256") != expected_environment_sha256
    ):
        codes.add("wp0001-mcp-environment-mismatch")
    expected_inventory_sha256 = policy.get(
        "effective_server_inventory_sha256"
    )
    if (
        re.fullmatch(r"[0-9a-f]{64}", str(expected_inventory_sha256)) is None
        or expected_inventory_sha256 == "0" * 64
        or client.get("server_inventory_sha256")
        != expected_inventory_sha256
        or handshake.get("server_inventory_sha256")
        != expected_inventory_sha256
        or activation_session.get("server_inventory_sha256")
        != expected_inventory_sha256
    ):
        codes.add("wp0001-mcp-config-inventory-invalid")
    enabled_tools = policy.get("enabled_tools")
    visible_tools = policy.get("client_visible_tools")
    if (
        not isinstance(enabled_tools, list)
        or not enabled_tools
        or enabled_tools != visible_tools
        or any(
            not isinstance(tool, str)
            or re.fullmatch(r"Unity_[A-Za-z0-9_]+", tool) is None
            for tool in enabled_tools
        )
    ):
        codes.add("wp0001-mcp-tool-scope-mismatch")
    elif any(tool in WP0001_ALWAYS_FORBIDDEN_MCP_TOOLS for tool in enabled_tools):
        codes.add("wp0001-mcp-forbidden-tool")
    canonical_tool_bytes = (
        json.dumps(
            enabled_tools,
            ensure_ascii=False,
            separators=(",", ":"),
        ).encode("utf-8")
        if isinstance(enabled_tools, list)
        else b""
    )
    if (
        policy.get("enabled_tools_sha256")
        != hashlib.sha256(canonical_tool_bytes).hexdigest()
        or policy.get("tool_scope_authority_claim")
        != "AUTHORIZE-WP0001-MCP-ALLOWLIST"
    ):
        codes.add("wp0001-mcp-tool-scope-authority-invalid")
    if relay.get("sha256") != relay.get("package_copy_sha256"):
        codes.add("wp0001-mcp-relay-package-mismatch")
    if (
        policy.get("server_name") != "unity_mcp_a1_wp0001"
        or policy.get("approval_policy") != "on-request"
        or policy.get("default_tools_approval_mode") != "prompt"
        or policy.get("required") is not True
        or policy.get("exact_command_identity_enforced") is not True
        or policy.get("project_config_disabled") is not True
        or policy.get("global_unity_mcp_absent") is not True
    ):
        codes.add("wp0001-codex-policy-invalid")

    relay_path = relay.get("path")
    connection_file = bridge.get("connection_file")
    runtime_config_path = policy.get("runtime_config_path")
    expected_runtime_config_path = (
        f"{expected_client_environment['CODEX_HOME']}/config.toml"
        if isinstance(expected_client_environment, dict)
        else None
    )
    if not (
        isinstance(home_root, str)
        and isinstance(relay_path, str)
        and relay_path.startswith(f"{home_root}/.unity/relay/")
        and isinstance(connection_file, str)
        and connection_file.startswith(f"{home_root}/.unity/mcp/connections/")
        and runtime_config_path == expected_runtime_config_path
    ):
        codes.add("wp0001-mcp-runtime-state-escape")

    if (
        process_observation.get("path")
        != "docs/evidence/WP-0001/a1-activation/mcp-route.json"
        or process_observation != activation_evidence.get("route")
    ):
        codes.add("wp0001-mcp-process-evidence-mismatch")

    endpoint = bridge.get("endpoint")
    project_hash = (
        hashlib.sha1(f"{repository_root}/Game/Assets".encode("utf-8"))
        .hexdigest()[:8]
        if repository_root is not None
        else None
    )
    expected_endpoint = (
        f"/tmp/unity-mcp-{project_hash}-{editor_pid}"
        if project_hash is not None and isinstance(editor_pid, int)
        else None
    )
    expected_connection_file = (
        f"{home_root}/.unity/mcp/connections/bridge-{project_hash}-{editor_pid}.json"
        if isinstance(home_root, str)
        and project_hash is not None
        and isinstance(editor_pid, int)
        else None
    )
    if (
        endpoint != expected_endpoint
        or connection_file != expected_connection_file
        or bridge.get("discovery_connection_type") != "named_pipe"
        or bridge.get("physical_transport") != "unix_socket"
        or bridge.get("endpoint_owner_uid") != principal_uid
        or bridge.get("endpoint_mode") != "0600"
        or bridge.get("shared_temp_exception") is not True
    ):
        codes.add("wp0001-mcp-endpoint-invalid")
    expected_socket_exception = (
        f"/private{expected_endpoint}" if isinstance(expected_endpoint, str) else None
    )
    if runtime.get("ambient_shared_temp_write_exceptions") != [
        expected_socket_exception
    ]:
        codes.add("wp0001-mcp-socket-exception-invalid")
    discovery_record = bridge.get("discovery_record")
    if (
        not isinstance(discovery_record, dict)
        or discovery_record.get("path")
        != "docs/evidence/WP-0001/a1-activation/bridge-discovery.json"
    ):
        codes.add("wp0001-mcp-discovery-evidence-invalid")

    if (
        connection.get("prior_approval_history_absent") is not True
        or connection.get("preflight_approval_revoked") is not True
        or connection.get("publisher_fallback_used") is not False
        or connection.get("batch_auto_approve") is not False
        or connection.get("direct_requires_approval") is not True
        or connection.get("gateway_allowed") is not False
        or connection.get("first_connection_creator_approved") is not True
        or connection.get("final_state") != "connected"
    ):
        codes.add("wp0001-mcp-connection-state-invalid")
    policy_evidence = policy.get("evidence")
    policy_evidence_sha256 = (
        policy_evidence.get("sha256")
        if isinstance(policy_evidence, dict)
        else None
    )
    handshake_capture = parse_datetime(handshake.get("captured_at"))
    handshake_starts = [
        parse_datetime(handshake.get(field))
        for field in (
            "client_started_at",
            "relay_started_at",
            "editor_started_at",
        )
    ]
    handshake_birth_ids_valid = all(
        re.fullmatch(r"[0-9a-f]{64}", str(handshake.get(field))) is not None
        for field in (
            "client_process_birth_id_sha256",
            "relay_process_birth_id_sha256",
            "editor_process_birth_id_sha256",
        )
    )
    if (
        handshake_capture is None
        or any(start is None for start in handshake_starts)
        or any(
            isinstance(start, datetime) and start > handshake_capture
            for start in handshake_starts
        )
        or not handshake_birth_ids_valid
    ):
        codes.add("wp0001-mcp-handshake-time-invalid")
    if (
        handshake.get("capture_complete") is not True
        or not {"initialize", "tools/list"}.issubset(
            set(handshake.get("observed_methods", []))
            if isinstance(handshake.get("observed_methods"), list)
            else set()
        )
        or any(
            method
            not in {
                "initialize",
                "notifications/initialized",
                "tools/list",
                "ping",
            }
            for method in (
                handshake.get("observed_methods", [])
                if isinstance(handshake.get("observed_methods"), list)
                else []
            )
        )
        or not all(
            isinstance(handshake.get(field), int)
            and not isinstance(handshake.get(field), bool)
            and handshake.get(field) > 0
            for field in ("client_pid", "relay_pid", "editor_pid")
        )
        or handshake.get("editor_pid") != bridge.get("editor_pid")
        or handshake.get("runtime_config_sha256")
        != policy_evidence_sha256
        or handshake.get("enabled_tools_sha256")
        != policy.get("enabled_tools_sha256")
        or handshake.get("environment_sha256")
        != expected_environment_sha256
        or handshake.get("model_prompt_count") != 0
        or handshake.get("unity_tool_call_count") != 0
        or handshake.get("disconnected") is not True
        or handshake.get("approval_revoked_after_disconnect") is not True
    ):
        codes.add("wp0001-mcp-handshake-not-clean")

    runtime_config_sha256 = policy_evidence_sha256
    activation_session_evidence = activation_session.get("evidence")
    if (
        not isinstance(activation_session_evidence, dict)
        or activation_session_evidence
        != activation_evidence.get("activation_session")
        or activation_session_evidence.get("path")
        != "docs/evidence/WP-0001/a1-activation/activation-session.json"
        or activation_session.get("client_pid") != client.get("pid")
        or activation_session.get("relay_pid") != relay.get("pid")
        or activation_session.get("editor_pid") != bridge.get("editor_pid")
        or activation_session.get("client_started_at") != client.get("started_at")
        or activation_session.get("relay_started_at") != relay.get("started_at")
        or activation_session.get("editor_started_at") != bridge.get("started_at")
        or activation_session.get("client_process_birth_id_sha256")
        != client.get("process_birth_id_sha256")
        or activation_session.get("relay_process_birth_id_sha256")
        != relay.get("process_birth_id_sha256")
        or activation_session.get("editor_process_birth_id_sha256")
        != bridge.get("process_birth_id_sha256")
        or activation_session.get("runtime_config_sha256")
        != runtime_config_sha256
        or activation_session.get("enabled_tools_sha256")
        != policy.get("enabled_tools_sha256")
        or activation_session.get("environment_sha256")
        != expected_environment_sha256
        or any(
            not isinstance(activation_session.get(field), str)
            or not activation_session.get(field)
            for field in (
                "client_started_at",
                "relay_started_at",
                "editor_started_at",
                "captured_at",
            )
        )
        or any(
            re.fullmatch(
                r"[0-9a-f]{64}",
                str(activation_session.get(field)),
            )
            is None
            for field in (
                "session_id_sha256",
                "connection_record_sha256",
                "fd_graph_sha256",
            )
        )
        or activation_session.get("connection_record_sha256")
        != bridge.get("connection_file_sha256")
        or activation_session.get("session_id_sha256")
        != wp0001_session_identity_sha256(
            route,
            runtime,
            activation_session.get("fd_graph_sha256"),
        )
        or activation_session.get("approval_history_absent_before_connection")
        is not True
        or activation_session.get("creator_approved") is not True
        or activation_session.get("publisher_fallback_used") is not False
        or activation_session.get("connected") is not True
        or activation_session.get("capture_complete") is not True
        or activation_session.get("model_prompt_count") != 0
        or activation_session.get("unity_tool_call_count") != 0
        or activation_session.get("receipt_must_be_reissued_on_drift") is not True
    ):
        codes.add("wp0001-mcp-activation-session-invalid")
    activation_capture = parse_datetime(activation_session.get("captured_at"))
    activation_starts = [
        parse_datetime(activation_session.get(field))
        for field in (
            "client_started_at",
            "relay_started_at",
            "editor_started_at",
        )
    ]
    if (
        activation_capture is None
        or any(start is None for start in activation_starts)
        or any(
            isinstance(start, datetime) and start > activation_capture
            for start in activation_starts
        )
        or (
            isinstance(activation_starts[0], datetime)
            and activation_capture - activation_starts[0] > timedelta(minutes=15)
        )
        or (
            isinstance(activation_starts[1], datetime)
            and activation_capture - activation_starts[1] > timedelta(minutes=15)
        )
        or (
            handshake_capture is not None
            and activation_capture <= handshake_capture
        )
    ):
        codes.add("wp0001-mcp-activation-session-stale")

    if (
        controls.get("pending_execution_gap_acknowledged") is not True
        or controls.get("hidden_tool_gap_acknowledged") is not True
        or controls.get("os_quarantine_authoritative") is not True
    ):
        codes.add("wp0001-mcp-stock-gap-unacknowledged")
    mitigation = controls.get("mitigation_mode")
    tcp_listener_count = controls.get("tcp_listener_count")
    wildcard_listener_count = controls.get("wildcard_listener_count")
    non_loopback_probes = controls.get("non_loopback_probe_count")
    non_loopback_successes = controls.get(
        "non_loopback_probe_success_count"
    )
    loopback_probes = controls.get("loopback_probe_count")
    loopback_successes = controls.get("loopback_probe_success_count")
    approved_egress_probes = controls.get("approved_egress_probe_count")
    approved_egress_successes = controls.get(
        "approved_egress_probe_success_count"
    )
    unapproved_egress_probes = controls.get(
        "unapproved_egress_probe_count"
    )
    unapproved_egress_successes = controls.get(
        "unapproved_egress_probe_success_count"
    )
    probe_targets = controls.get("probe_targets")
    expected_loopback_target = (
        f"unix://{runtime.get('ambient_shared_temp_write_exceptions', [None])[0]}"
        if isinstance(
            runtime.get("ambient_shared_temp_write_exceptions"), list
        )
        and len(runtime.get("ambient_shared_temp_write_exceptions")) == 1
        else None
    )
    if (
        not isinstance(probe_targets, dict)
        or set(probe_targets)
        != {
            "non_loopback_listener",
            "loopback_control",
            "approved_egress",
            "unapproved_egress",
        }
        or re.fullmatch(
            r"external-observer://[0-9a-f]{64}",
            str(probe_targets.get("non_loopback_listener")),
        )
        is None
        or probe_targets.get("non_loopback_listener")
        == f"external-observer://{'0' * 64}"
        or probe_targets.get("loopback_control")
        != expected_loopback_target
        or probe_targets.get("approved_egress")
        != "https://packages.unity.com/"
        or probe_targets.get("unapproved_egress")
        != "https://example.com/"
        or controls.get("probe_targets_sha256")
        != canonical_json_sha256(probe_targets)
    ):
        codes.add("wp0001-mcp-network-policy-unproven")
    if (
        controls.get("network_policy_enforced") is not True
        or controls.get("network_policy_sha256")
        != (
            approved_environment.get("network_policy_sha256")
            if isinstance(approved_environment, dict)
            else None
        )
        or controls.get("boot_session_sha256")
        != runtime.get("boot_session_sha256")
        or controls.get("non_loopback_reachable_listener_count") != 0
        or non_loopback_successes != 0
        or not isinstance(tcp_listener_count, int)
        or isinstance(tcp_listener_count, bool)
        or not isinstance(wildcard_listener_count, int)
        or isinstance(wildcard_listener_count, bool)
        or wildcard_listener_count > tcp_listener_count
    ):
        codes.add("wp0001-mcp-nonloopback-reachable")
    if mitigation == "persistent-relay-suppressed":
        if (
            controls.get("persistent_relay_process_count") != 0
            or tcp_listener_count != 0
            or wildcard_listener_count != 0
        ):
            codes.add("wp0001-mcp-persistent-relay-not-suppressed")
    elif mitigation == "os-network-denied":
        pass
    else:
        codes.add("wp0001-mcp-mitigation-mode-invalid")
    probe_counts = (
        non_loopback_probes,
        loopback_probes,
        approved_egress_probes,
        unapproved_egress_probes,
    )
    if (
        any(
            not isinstance(count, int)
            or isinstance(count, bool)
            or count < 1
            for count in probe_counts
        )
        or loopback_successes != loopback_probes
        or approved_egress_successes != approved_egress_probes
        or unapproved_egress_successes != 0
    ):
        codes.add("wp0001-mcp-network-policy-unproven")
    network_capture = parse_datetime(controls.get("captured_at"))
    if (
        network_capture is None
        or activation_capture is None
        or network_capture > activation_capture
        or activation_capture - network_capture > timedelta(minutes=15)
        or any(
            isinstance(start, datetime) and start > network_capture
            for start in activation_starts
        )
    ):
        codes.add("wp0001-mcp-network-policy-unproven")

    if (
        entitlement.get("eligible_assigned_seat_verified") is not True
        or entitlement.get("same_organization_project_linkage_verified") is not True
        or entitlement.get("license_secret_material_copied") is not False
    ):
        codes.add("wp0001-unity-entitlement-linkage-invalid")
    if (
        identity.get("company") != "LocalFoundationLab"
        or identity.get("product") != "SashaAtomicLandPirate_WP0001"
        or identity.get("bundle_id")
        != "local.foundation.sashaatomiclandpirate.wp0001"
        or identity.get("dev_profile") != "wp0001-dev-v1"
        or identity.get("test_profile") != "wp0001-test-v1"
        or identity.get("temporary_non_shipping") is not True
        or identity.get("prior_root_discovery_forbidden") is not True
    ):
        codes.add("wp0001-project-identity-invalid")
    return sorted(codes)


def wp0001_seed_tree_policy_codes(tree_listing: bytes | None) -> list[str]:
    """Reject an empty, scratch-bearing, symlinked, or submodule project seed."""
    if not tree_listing:
        return ["wp0001-project-seed-tree-empty"]
    codes: set[str] = set()
    scratch_roots = tuple(
        item.rstrip("/") for item in WP0001_UNITY_EPHEMERAL_SCRATCH_PATHS
    )
    for raw_entry in tree_listing.split(b"\0"):
        if not raw_entry:
            continue
        try:
            metadata, raw_path = raw_entry.split(b"\t", 1)
            mode, object_type, _object_id = metadata.decode("ascii").split(" ", 2)
            path = raw_path.decode("utf-8")
        except (ValueError, UnicodeDecodeError):
            codes.add("wp0001-project-seed-tree-unparseable")
            continue
        if mode == "120000":
            codes.add("wp0001-project-seed-symlink")
        if object_type == "commit" or mode == "160000":
            codes.add("wp0001-project-seed-submodule")
        if any(path == root or path.startswith(f"{root}/") for root in scratch_roots):
            codes.add("wp0001-project-seed-tracks-scratch")
        if path.startswith("Game/Assets/") and path not in {
            "Game/Assets/.gitkeep",
            "Game/Assets/.keep",
        }:
            codes.add("wp0001-project-seed-assets-not-empty")
        if path.startswith("Game/Packages/") and path not in {
            "Game/Packages/manifest.json",
            "Game/Packages/packages-lock.json",
        }:
            codes.add("wp0001-project-seed-embedded-package")
        if not (
            path.startswith("Game/Packages/")
            or path.startswith("Game/ProjectSettings/")
            or path in {"Game/Assets/.gitkeep", "Game/Assets/.keep"}
        ):
            codes.add("wp0001-project-seed-unexpected-path")
    return sorted(codes)


def wp0001_project_seed_content(
    base_commit: str,
    toolchain_profile: object,
) -> tuple[dict, list[str]]:
    """Read the protected Game seed and derive the exact facts evidence must bind."""
    codes: set[str] = set()
    profile = toolchain_profile if isinstance(toolchain_profile, dict) else {}
    required_paths = (
        "Game/Packages/manifest.json",
        "Game/Packages/packages-lock.json",
        "Game/ProjectSettings/ProjectSettings.asset",
        "Game/ProjectSettings/ProjectVersion.txt",
    )
    blobs = {
        path: git_repo_blob(base_commit, path)
        for path in required_paths
    }
    if any(blob is None for blob in blobs.values()):
        codes.add("wp0001-project-seed-required-file-missing")

    project_version_text = ""
    project_version_blob = blobs["Game/ProjectSettings/ProjectVersion.txt"]
    if project_version_blob is not None:
        try:
            project_version_text = project_version_blob.decode("utf-8")
        except UnicodeDecodeError:
            codes.add("wp0001-project-seed-project-version-invalid")
    editor_version = profile.get("editor_version")
    editor_changeset = profile.get("editor_changeset")
    if (
        f"m_EditorVersion: {editor_version}" not in project_version_text
        or f"m_EditorVersionWithRevision: {editor_version} ({editor_changeset})"
        not in project_version_text
    ):
        codes.add("wp0001-project-seed-project-version-invalid")

    def parse_seed_json(path: str) -> dict:
        raw = blobs[path]
        if raw is None:
            return {}
        parsed, parse_errors = load_json_bytes(raw, path)
        if parse_errors or not isinstance(parsed, dict):
            codes.add("wp0001-project-seed-package-json-invalid")
            return {}
        return parsed

    package_manifest = parse_seed_json("Game/Packages/manifest.json")
    package_lock = parse_seed_json("Game/Packages/packages-lock.json")
    dependencies = (
        package_manifest.get("dependencies")
        if isinstance(package_manifest.get("dependencies"), dict)
        else {}
    )
    lock_dependencies = (
        package_lock.get("dependencies")
        if isinstance(package_lock.get("dependencies"), dict)
        else {}
    )
    expected_packages = {
        "com.unity.ai.assistant": profile.get("assistant_package_version"),
        "com.unity.render-pipelines.universal": profile.get("resolved_urp_version"),
        "com.unity.test-framework": profile.get("resolved_test_framework_version"),
    }
    if (
        package_manifest.get("enableLockFile") is not True
        or package_manifest.get("resolutionStrategy") != "lowest"
    ):
        codes.add("wp0001-project-seed-lock-policy-invalid")
    allowed_manifest_keys = {
        "dependencies",
        "enableLockFile",
        "resolutionStrategy",
        "testables",
        "useSatSolver",
    }
    if set(package_manifest) - allowed_manifest_keys:
        codes.add("wp0001-project-seed-package-source-invalid")
    registry_version_pattern = re.compile(
        r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?"
    )
    for package_name, version in dependencies.items():
        if (
            not isinstance(package_name, str)
            or not package_name.startswith("com.unity.")
            or not isinstance(version, str)
            or registry_version_pattern.fullmatch(version) is None
        ):
            codes.add("wp0001-project-seed-package-source-invalid")
    testables = package_manifest.get("testables", [])
    if not isinstance(testables, list):
        codes.add("wp0001-project-seed-package-source-invalid")
        testables = []
    if set(testables) - set(dependencies):
        codes.add("wp0001-project-seed-package-source-invalid")
    if set(dependencies) - set(lock_dependencies):
        codes.add("wp0001-project-seed-package-lock-incomplete")
    for package_name, lock_record in lock_dependencies.items():
        if (
            not isinstance(package_name, str)
            or not package_name.startswith("com.unity.")
            or not isinstance(lock_record, dict)
            or lock_record.get("source") not in {"registry", "builtin"}
            or not isinstance(lock_record.get("depth"), int)
            or isinstance(lock_record.get("depth"), bool)
            or lock_record.get("depth", -1) < 0
            or not isinstance(lock_record.get("version"), str)
            or registry_version_pattern.fullmatch(lock_record["version"]) is None
        ):
            codes.add("wp0001-project-seed-package-source-invalid")
            continue
        lock_url = lock_record.get("url")
        if lock_url not in {None, "https://packages.unity.com"}:
            codes.add("wp0001-project-seed-package-source-invalid")
        transitive = lock_record.get("dependencies", {})
        if not isinstance(transitive, dict):
            codes.add("wp0001-project-seed-package-source-invalid")
            continue
        for transitive_name, transitive_version in transitive.items():
            if (
                not isinstance(transitive_name, str)
                or not transitive_name.startswith("com.unity.")
                or not isinstance(transitive_version, str)
                or registry_version_pattern.fullmatch(transitive_version) is None
            ):
                codes.add("wp0001-project-seed-package-source-invalid")
    for package_name, expected_version in expected_packages.items():
        lock_record = lock_dependencies.get(package_name)
        if (
            dependencies.get(package_name) != expected_version
            or not isinstance(lock_record, dict)
            or lock_record.get("version") != expected_version
        ):
            codes.add("wp0001-project-seed-package-version-mismatch")

    project_settings_text = ""
    project_settings_blob = blobs["Game/ProjectSettings/ProjectSettings.asset"]
    if project_settings_blob is not None:
        try:
            project_settings_text = project_settings_blob.decode("utf-8")
        except UnicodeDecodeError:
            codes.add("wp0001-project-seed-identity-invalid")
    for required_text in (
        "companyName: LocalFoundationLab",
        "productName: SashaAtomicLandPirate_WP0001",
        "local.foundation.sashaatomiclandpirate.wp0001",
    ):
        if required_text not in project_settings_text:
            codes.add("wp0001-project-seed-identity-invalid")

    facts = {
        "project_root": "Game",
        "game_tree_sha256": None,
        "required_seed_files": list(required_paths),
        "editor_version": editor_version,
        "editor_changeset": editor_changeset,
        "assistant_package_version": profile.get("assistant_package_version"),
        "resolved_urp_version": profile.get("resolved_urp_version"),
        "resolved_test_framework_version": profile.get(
            "resolved_test_framework_version"
        ),
        "manifest_dependencies": dependencies,
        "resolved_package_lock": lock_dependencies,
        "package_lock_enabled": package_manifest.get("enableLockFile"),
        "resolution_strategy": package_manifest.get("resolutionStrategy"),
        "company": "LocalFoundationLab",
        "product": "SashaAtomicLandPirate_WP0001",
        "bundle_id": "local.foundation.sashaatomiclandpirate.wp0001",
        "creator_attested_no_implementation": True,
    }
    return facts, sorted(codes)


def apply_fixture_mutations(value: dict, mutations: object) -> dict:
    """Apply simple dotted-path replacement mutations to an isolated JSON fixture."""
    candidate = json.loads(json.dumps(value))
    if not isinstance(mutations, list):
        return candidate
    for mutation in mutations:
        if not isinstance(mutation, dict):
            continue
        path = mutation.get("path")
        if not isinstance(path, str) or not path:
            continue
        parts = path.split(".")
        current: object = candidate
        for part in parts[:-1]:
            if not isinstance(current, dict) or part not in current:
                current = None
                break
            current = current[part]
        if isinstance(current, dict):
            current[parts[-1]] = mutation.get("value")
    return candidate


def validate_a1_wp0001_boundary_fixtures() -> list[str]:
    errors: list[str] = []
    fixture_path = (
        ROOT / "governance" / "fixtures" / "a1-wp0001-boundary.fixtures.json"
    )
    if not fixture_path.is_file():
        return ["WP-0001 A1 boundary fixtures are missing"]
    fixture = load_json(fixture_path)
    base_case = fixture.get("base_case")
    cases = fixture.get("cases")
    if not isinstance(base_case, dict) or not isinstance(cases, list) or not cases:
        return [f"{fixture_path.relative_to(ROOT)} lacks base_case or cases"]
    seen_ids: set[str] = set()
    for case in cases:
        if not isinstance(case, dict):
            errors.append(f"{fixture_path.relative_to(ROOT)} has a non-object case")
            continue
        case_id = case.get("id")
        if not isinstance(case_id, str) or not case_id:
            errors.append(f"{fixture_path.relative_to(ROOT)} has a case without an ID")
            continue
        if case_id in seen_ids:
            errors.append(f"duplicate WP-0001 A1 boundary fixture ID: {case_id}")
        seen_ids.add(case_id)
        candidate = apply_fixture_mutations(base_case, case.get("mutations"))
        expected_codes = case.get("expected_codes")
        if (
            not isinstance(expected_codes, list)
            or any(not isinstance(code, str) for code in expected_codes)
            or len(expected_codes) != len(set(expected_codes))
        ):
            errors.append(f"{case_id} expected_codes must be a unique string array")
            continue
        actual_codes = a1_wp0001_boundary_codes(
            candidate.get("repository"),
            candidate.get("runtime_boundary"),
            candidate.get("project_seed"),
            candidate.get("unity_mcp_route"),
            candidate.get("approved_toolchain"),
            candidate.get("approved_environment"),
            candidate.get("wp0001_toolchain_profile"),
            candidate.get("activation_evidence"),
            candidate.get("raw_capture_collectors"),
            observed_seed_tree_sha256=case.get(
                "observed_seed_tree_sha256",
                candidate.get("project_seed", {}).get("git_tree_sha256"),
            ),
        )
        if actual_codes != sorted(expected_codes):
            errors.append(
                f"{case_id} expected {sorted(expected_codes)} but produced {actual_codes}"
            )
    return errors


def validate_repo_evidence_reference(
    reference: object,
    label: str,
    *,
    expected_path: str | None = None,
    committed_blob: bytes | None = None,
) -> tuple[Path | None, list[str]]:
    errors: list[str] = []
    if not isinstance(reference, dict):
        return None, [f"{label} reference is missing or invalid"]
    relative = reference.get("path")
    if expected_path is not None and relative != expected_path:
        errors.append(f"{label} must use {expected_path}")
    path, path_error = safe_repo_path(relative, f"{label} path")
    if path_error:
        return None, errors + [path_error]
    expected_hash = reference.get("sha256")
    if (
        not isinstance(expected_hash, str)
        or re.fullmatch(r"[0-9a-f]{64}", expected_hash) is None
    ):
        errors.append(f"{label} lacks a raw SHA-256")
        return path, errors
    if committed_blob is not None:
        actual_hash = hashlib.sha256(committed_blob).hexdigest()
        if actual_hash != expected_hash:
            errors.append(f"{label} committed bytes do not match its SHA-256")
    elif path is None or not path.is_file():
        errors.append(f"{label} evidence file is missing")
    elif sha256_file(path) != expected_hash:
        errors.append(f"{label} evidence file does not match its SHA-256")
    return path, errors


def validate_python_collector_source(
    reference: object,
    label: str,
) -> list[str]:
    path, errors = validate_repo_evidence_reference(reference, label)
    if errors or path is None or not path.is_file():
        return errors
    try:
        source = path.read_text(encoding="utf-8")
        compile(source, str(path), "exec")
    except (OSError, UnicodeDecodeError, SyntaxError):
        return errors + [f"{label} is not valid Python source"]
    if (
        "def main(" not in source
        or 'if __name__ == "__main__":' not in source
        or len(
            [
                line
                for line in source.splitlines()
                if line.strip() and not line.lstrip().startswith("#")
            ]
        )
        < 3
    ):
        errors.append(f"{label} is an inert or incomplete collector")
    return errors


def canonical_json_sha256(value: object) -> str:
    return hashlib.sha256(
        json.dumps(
            value,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")
    ).hexdigest()


def _timestamps_within_one_second(left: object, right: object) -> bool:
    left_time = parse_datetime(left)
    right_time = parse_datetime(right)
    return (
        left_time is not None
        and right_time is not None
        and abs((left_time - right_time).total_seconds()) <= 1
    )


def _live_process_capture_matches(
    observed: object,
    expected: dict,
    *,
    executable_path_key: str,
    executable_hash_key: str,
    environment: dict,
) -> bool:
    if not isinstance(observed, dict):
        return False
    expected_keys = {
        "pid",
        "parent_pid",
        "uid",
        "started_at",
        "executable_path",
        "executable_sha256",
        "executable_regular",
        "executable_symlink",
        "cwd",
        "arguments_sha256",
        "process_birth_id_sha256",
        "signing_identity",
        "inspection_error",
    }
    expected_keys.add("environment")
    if set(observed) != expected_keys:
        return False
    signing = observed.get("signing_identity")
    expected_identity = expected.get("signing_identity")
    executable_vnode = (
        signing.get("executable_vnode")
        if isinstance(signing, dict)
        else None
    )
    vnode_matches = (
        isinstance(executable_vnode, dict)
        and set(executable_vnode)
        == {
            "device",
            "inode",
            "size",
            "mtime_ns",
            "stable_across_inspection",
        }
        and all(
            isinstance(executable_vnode.get(key), int)
            and not isinstance(executable_vnode.get(key), bool)
            and executable_vnode.get(key) >= 0
            for key in ("device", "inode", "size", "mtime_ns")
        )
        and executable_vnode.get("stable_across_inspection") is True
    )
    identity_matches = (
        isinstance(signing, dict)
        and code_identity_matches(signing, expected_identity)
        and vnode_matches
    )
    try:
        path_matches = (
            Path(str(observed.get("executable_path"))).resolve(strict=False)
            == Path(str(expected.get(executable_path_key))).resolve(strict=False)
        )
        cwd_matches = (
            Path(str(observed.get("cwd"))).resolve(strict=False)
            == Path(str(expected.get("cwd"))).resolve(strict=False)
        )
    except (OSError, RuntimeError):
        path_matches = False
        cwd_matches = False
    observed_environment = observed.get("environment")
    environment_names = (
        observed_environment.get("names")
        if isinstance(observed_environment, dict)
        else None
    )
    environment_matches = (
        isinstance(observed_environment, dict)
        and set(observed_environment)
        == {
            "values",
            "names",
            "duplicate_names",
            "names_sha256",
            "absent_variable_names_present",
        }
        and observed_environment.get("values") == environment
        and isinstance(environment_names, list)
        and environment_names == sorted(set(environment_names))
        and observed_environment.get("duplicate_names") == []
        and observed_environment.get("names_sha256")
        == canonical_json_sha256(environment_names)
        == expected.get("environment_names_sha256")
        and observed_environment.get("absent_variable_names_present") == []
    )
    return (
        observed.get("inspection_error") is None
        and observed.get("pid")
        == expected.get(
            "editor_pid" if executable_path_key == "editor_path" else "pid"
        )
        and observed.get("uid") == expected.get("principal_uid")
        and _timestamps_within_one_second(
            observed.get("started_at"),
            expected.get("started_at"),
        )
        and observed.get("executable_regular") is True
        and observed.get("executable_symlink") is False
        and path_matches
        and observed.get("executable_sha256")
        == expected.get(executable_hash_key)
        and cwd_matches
        and observed.get("arguments_sha256")
        == canonical_json_sha256(expected.get("arguments"))
        and observed.get("process_birth_id_sha256")
        == expected.get("process_birth_id_sha256")
        and identity_matches
        and environment_matches
    )


def _live_fd_graph_matches(
    observed: object,
    *,
    client_pid: object,
    relay_pid: object,
    editor_pid: object,
    reported_endpoint: object,
    canonical_endpoint: object,
) -> bool:
    if (
        not isinstance(observed, dict)
        or set(observed) != {"graph", "sha256"}
        or not isinstance(observed.get("graph"), dict)
        or observed.get("sha256")
        != canonical_json_sha256(observed.get("graph"))
    ):
        return False
    graph = observed["graph"]
    if set(graph) != {
        "schema_version",
        "endpoint",
        "channels",
        "processes",
        "residuals",
    } or graph.get("schema_version") != 1 or graph.get("residuals") != []:
        return False
    endpoint = graph.get("endpoint")
    accepted_paths = {
        value
        for value in (reported_endpoint, canonical_endpoint)
        if isinstance(value, str)
    }
    if (
        not isinstance(endpoint, dict)
        or set(endpoint)
        != {
            "canonical_path",
            "canonical_path_sha256",
            "accepted_path_sha256s",
        }
        or endpoint.get("canonical_path") != canonical_endpoint
        or endpoint.get("canonical_path_sha256")
        != hashlib.sha256(
            str(canonical_endpoint).encode(
                "utf-8",
                errors="surrogateescape",
            )
        ).hexdigest()
        or endpoint.get("accepted_path_sha256s")
        != sorted(
            hashlib.sha256(
                value.encode("utf-8", errors="surrogateescape")
            ).hexdigest()
            for value in accepted_paths
        )
    ):
        return False
    channels = graph.get("channels")
    if (
        not isinstance(channels, dict)
        or set(channels)
        != {
            "editor_relay_unix",
            "client_relay_stdin",
            "client_relay_stdout",
        }
    ):
        return False
    channel_specs = {
        "editor_relay_unix": {
            "hash_key": "address_sha256",
            "fd_keys": ("editor_fd", "relay_fd"),
        },
        "client_relay_stdin": {
            "hash_key": "address_pair_sha256",
            "fd_keys": ("client_fd", "relay_fd"),
        },
        "client_relay_stdout": {
            "hash_key": "address_pair_sha256",
            "fd_keys": ("client_fd", "relay_fd"),
        },
    }
    channel_hashes: dict[str, str] = {}
    for name, spec in channel_specs.items():
        channel = channels.get(name)
        expected_keys = {spec["hash_key"], *spec["fd_keys"]}
        if (
            not isinstance(channel, dict)
            or set(channel) != expected_keys
            or re.fullmatch(
                r"[0-9a-f]{64}",
                str(channel.get(spec["hash_key"])),
            )
            is None
            or channel.get(spec["hash_key"]) == "0" * 64
            or any(
                not isinstance(channel.get(key), int)
                or isinstance(channel.get(key), bool)
                or channel.get(key) < 0
                for key in spec["fd_keys"]
            )
        ):
            return False
        channel_hashes[name] = channel[spec["hash_key"]]
    if (
        len(set(channel_hashes.values())) != 3
        or channels["client_relay_stdin"]["relay_fd"] != 0
        or channels["client_relay_stdout"]["relay_fd"] != 1
        or channels["client_relay_stdin"]["client_fd"]
        == channels["client_relay_stdout"]["client_fd"]
    ):
        return False
    processes = graph.get("processes")
    expected_processes = [
        ("client", client_pid),
        ("editor", editor_pid),
        ("relay", relay_pid),
    ]
    if not isinstance(processes, list) or len(processes) != 3:
        return False
    descriptors_by_role: dict[str, list[dict]] = {}
    for process, (role, pid) in zip(
        processes,
        expected_processes,
        strict=True,
    ):
        if (
            not isinstance(process, dict)
            or set(process)
            != {
                "role",
                "pid",
                "inspection_complete",
                "inspection_error",
                "descriptors",
            }
            or process.get("role") != role
            or process.get("pid") != pid
            or process.get("inspection_complete") is not True
            or process.get("inspection_error") is not None
            or not isinstance(process.get("descriptors"), list)
            or not process.get("descriptors")
        ):
            return False
        descriptors: list[dict] = process["descriptors"]
        if any(
            not isinstance(descriptor, dict)
            or set(descriptor)
            != {
                "access",
                "channel_address_sha256",
                "fd",
                "state",
                "type",
            }
            or descriptor.get("access") not in {"r", "w", "u"}
            or re.fullmatch(
                r"[0-9a-f]{64}",
                str(descriptor.get("channel_address_sha256")),
            )
            is None
            or descriptor.get("channel_address_sha256") == "0" * 64
            or not isinstance(descriptor.get("fd"), int)
            or isinstance(descriptor.get("fd"), bool)
            or descriptor.get("fd") < 0
            or descriptor.get("state") not in {
                "connected",
                "listening",
                "open",
            }
            or descriptor.get("type") not in {"pipe", "unix"}
            for descriptor in descriptors
        ):
            return False
        descriptors_by_role[role] = descriptors

    def descriptor_present(
        role: str,
        *,
        channel_hash: str,
        fd: int,
        descriptor_type: str,
        access: str | None = None,
    ) -> bool:
        return any(
            descriptor["channel_address_sha256"] == channel_hash
            and descriptor["fd"] == fd
            and descriptor["type"] == descriptor_type
            and (access is None or descriptor["access"] == access)
            for descriptor in descriptors_by_role[role]
        )

    unix = channels["editor_relay_unix"]
    stdin = channels["client_relay_stdin"]
    stdout = channels["client_relay_stdout"]
    return (
        descriptor_present(
            "editor",
            channel_hash=channel_hashes["editor_relay_unix"],
            fd=unix["editor_fd"],
            descriptor_type="unix",
        )
        and descriptor_present(
            "relay",
            channel_hash=channel_hashes["editor_relay_unix"],
            fd=unix["relay_fd"],
            descriptor_type="unix",
        )
        and descriptor_present(
            "client",
            channel_hash=channel_hashes["client_relay_stdin"],
            fd=stdin["client_fd"],
            descriptor_type="pipe",
            access="w",
        )
        and descriptor_present(
            "relay",
            channel_hash=channel_hashes["client_relay_stdin"],
            fd=0,
            descriptor_type="pipe",
            access="r",
        )
        and descriptor_present(
            "client",
            channel_hash=channel_hashes["client_relay_stdout"],
            fd=stdout["client_fd"],
            descriptor_type="pipe",
            access="r",
        )
        and descriptor_present(
            "relay",
            channel_hash=channel_hashes["client_relay_stdout"],
            fd=1,
            descriptor_type="pipe",
            access="w",
        )
    )


def validate_wp0001_mcp_live_capture(
    document: object,
    *,
    route: dict,
    runtime: dict,
    route_contract_sha256: str,
    protected_config_sha256: str | None,
) -> list[str]:
    """Validate the fixed, read-only OS-derived MCP route capture."""
    errors: list[str] = []
    if not isinstance(document, dict):
        return ["MCP live capture must be an object"]
    if set(document) != {
        "schema_version",
        "validator_version",
        "packet_id",
        "captured_at",
        "result",
        "checks",
        "observed",
    }:
        errors.append("MCP live capture has unexpected or missing top-level fields")
    checks = document.get("checks")
    observed = document.get("observed")
    if (
        document.get("schema_version") != 1
        or document.get("validator_version")
        != WP0001_MCP_LIVE_VALIDATOR_VERSION
        or document.get("packet_id") != "WP-0001"
        or parse_datetime(document.get("captured_at")) is None
        or document.get("result") != "PASS"
        or not isinstance(checks, dict)
        or set(checks) != WP0001_MCP_LIVE_CHECKS
        or any(value is not True for value in checks.values())
        or not isinstance(observed, dict)
    ):
        errors.append("MCP live capture did not pass the exact verifier checks")
        return errors
    expected_observed_keys = {
        "boot_session_sha256",
        "route_contract_sha256",
        "client",
        "relay",
        "bridge",
        "runtime_config_sha256",
        "protected_config_sha256",
        "environment_sha256",
        "enabled_tools_sha256",
        "effective_server_inventory",
        "effective_server_inventory_sha256",
        "connection_file",
        "endpoint",
        "fd_graph",
    }
    if set(observed) != expected_observed_keys:
        errors.append("MCP live capture observed inventory is not exact")
        return errors
    client = route.get("client") if isinstance(route.get("client"), dict) else {}
    relay = route.get("relay") if isinstance(route.get("relay"), dict) else {}
    bridge = route.get("bridge") if isinstance(route.get("bridge"), dict) else {}
    policy = (
        route.get("codex_policy")
        if isinstance(route.get("codex_policy"), dict)
        else {}
    )
    handshake = (
        route.get("handshake")
        if isinstance(route.get("handshake"), dict)
        else {}
    )
    session = (
        route.get("activation_session")
        if isinstance(route.get("activation_session"), dict)
        else {}
    )
    guard = (
        policy.get("client_environment_guard")
        if isinstance(policy.get("client_environment_guard"), dict)
        else {}
    )
    environment_values = {
        **(
            policy.get("environment_bindings")
            if isinstance(policy.get("environment_bindings"), dict)
            else {}
        ),
        **{
            key: guard.get(key)
            for key in (
                "CODEX_HOME",
                "XDG_CONFIG_HOME",
                "XDG_CACHE_HOME",
                "XDG_DATA_HOME",
                "GIT_CONFIG_NOSYSTEM",
                "GIT_CONFIG_GLOBAL",
                "GIT_TERMINAL_PROMPT",
            )
        },
    }
    if not _live_process_capture_matches(
        observed.get("client"),
        client,
        executable_path_key="path",
        executable_hash_key="sha256",
        environment=environment_values,
    ):
        errors.append("MCP live capture client facts differ from the boundary")
    relay_observed = observed.get("relay")
    relay_process_observed = (
        {key: value for key, value in relay_observed.items() if key != "package_copy"}
        if isinstance(relay_observed, dict)
        else relay_observed
    )
    if not _live_process_capture_matches(
        relay_process_observed,
        {**relay, "cwd": client.get("cwd")},
        executable_path_key="path",
        executable_hash_key="sha256",
        environment=environment_values,
    ):
        errors.append("MCP live capture relay facts differ from the boundary")
    package_copy = (
        relay_observed.get("package_copy")
        if isinstance(relay_observed, dict)
        else None
    )
    if (
        not isinstance(package_copy, dict)
        or package_copy.get("path") != relay.get("package_copy_path")
        or package_copy.get("sha256") != relay.get("package_copy_sha256")
        or package_copy.get("regular") is not True
        or package_copy.get("symlink") is not False
    ):
        errors.append("MCP live capture relay package copy is invalid")
    if not _live_process_capture_matches(
        observed.get("bridge"),
        bridge,
        executable_path_key="editor_path",
        executable_hash_key="editor_sha256",
        environment=(
            policy.get("environment_bindings")
            if isinstance(policy.get("environment_bindings"), dict)
            else {}
        ),
    ):
        errors.append("MCP live capture Editor facts differ from the boundary")
    policy_evidence = (
        policy.get("evidence") if isinstance(policy.get("evidence"), dict) else {}
    )
    expected_runtime_hash = policy_evidence.get("sha256")
    expected_environment_hash = policy.get("environment_sha256")
    expected_allowlist_hash = policy.get("enabled_tools_sha256")
    expected_inventory_hash = policy.get(
        "effective_server_inventory_sha256"
    )
    candidate_root = (
        route.get("_candidate_root")
        if isinstance(route.get("_candidate_root"), str)
        else None
    )
    if candidate_root is None:
        client_cwd = client.get("cwd")
        candidate_root = client_cwd if isinstance(client_cwd, str) else None
    expected_effective_inventory = (
        [
            {
                "layer": "candidate",
                "path": f"{candidate_root}/.codex/config.toml",
                "sha256": protected_config_sha256,
                "top_level_keys": ["mcp_servers"],
                "servers": [{"enabled": False, "name": "unity_mcp"}],
            },
            {
                "layer": "runtime",
                "path": policy.get("runtime_config_path"),
                "sha256": expected_runtime_hash,
                "top_level_keys": [
                    "approval_policy",
                    "mcp_servers",
                ],
                "servers": [
                    {
                        "enabled": True,
                        "name": policy.get("server_name"),
                    }
                ],
            },
        ]
        if candidate_root is not None
        else None
    )
    if (
        observed.get("boot_session_sha256")
        != runtime.get("boot_session_sha256")
        or observed.get("route_contract_sha256")
        != route_contract_sha256
        or observed.get("runtime_config_sha256") != expected_runtime_hash
        or handshake.get("runtime_config_sha256") != expected_runtime_hash
        or session.get("runtime_config_sha256") != expected_runtime_hash
        or observed.get("environment_sha256") != expected_environment_hash
        or client.get("environment_sha256") != expected_environment_hash
        or handshake.get("environment_sha256") != expected_environment_hash
        or session.get("environment_sha256") != expected_environment_hash
        or observed.get("enabled_tools_sha256") != expected_allowlist_hash
        or handshake.get("enabled_tools_sha256") != expected_allowlist_hash
        or session.get("enabled_tools_sha256") != expected_allowlist_hash
        or observed.get("effective_server_inventory_sha256")
        != expected_inventory_hash
        or client.get("server_inventory_sha256")
        != expected_inventory_hash
        or handshake.get("server_inventory_sha256")
        != expected_inventory_hash
        or session.get("server_inventory_sha256")
        != expected_inventory_hash
        or canonical_json_sha256(
            observed.get("effective_server_inventory")
        )
        != expected_inventory_hash
        or observed.get("effective_server_inventory")
        != expected_effective_inventory
        or observed.get("protected_config_sha256")
        != protected_config_sha256
    ):
        errors.append("MCP live capture hash bindings differ from the boundary")
    connection = observed.get("connection_file")
    expected_connection_record = {
        "connection_type": bridge.get("discovery_connection_type"),
        "connection_path": bridge.get("endpoint"),
        "editor_pid": bridge.get("editor_pid"),
        "project_path": bridge.get("project_path"),
        "protocol_version": bridge.get("protocol_version"),
    }
    if (
        not isinstance(connection, dict)
        or set(connection) != {"path", "sha256", "error", "record"}
        or connection.get("path") != bridge.get("connection_file")
        or connection.get("sha256") != bridge.get("connection_file_sha256")
        or connection.get("error") is not None
        or not isinstance(connection.get("record"), dict)
        or {
            key: connection["record"].get(key)
            for key in expected_connection_record
        }
        != expected_connection_record
        or set(connection["record"])
        != {
            "connection_type",
            "connection_path",
            "created_date",
            "editor_pid",
            "project_path",
            "protocol_version",
        }
        or parse_datetime(connection["record"].get("created_date")) is None
    ):
        errors.append("MCP live capture connection record is invalid")
    endpoint = observed.get("endpoint")
    expected_canonical_endpoint = (
        runtime.get("ambient_shared_temp_write_exceptions", [None])[0]
        if isinstance(
            runtime.get("ambient_shared_temp_write_exceptions"), list
        )
        and len(runtime.get("ambient_shared_temp_write_exceptions")) == 1
        else None
    )
    if (
        not isinstance(endpoint, dict)
        or set(endpoint)
        != {
            "path",
            "canonical_path",
            "exists",
            "is_socket",
            "is_symlink",
            "uid",
            "mode",
        }
        or endpoint.get("path") != bridge.get("endpoint")
        or endpoint.get("canonical_path") != expected_canonical_endpoint
        or endpoint.get("exists") is not True
        or endpoint.get("is_socket") is not True
        or endpoint.get("is_symlink") is not False
        or endpoint.get("uid") != bridge.get("endpoint_owner_uid")
        or endpoint.get("mode") != bridge.get("endpoint_mode")
    ):
        errors.append("MCP live capture endpoint is invalid")
    if not _live_fd_graph_matches(
        observed.get("fd_graph"),
        client_pid=client.get("pid"),
        relay_pid=relay.get("pid"),
        editor_pid=bridge.get("editor_pid"),
        reported_endpoint=bridge.get("endpoint"),
        canonical_endpoint=expected_canonical_endpoint,
    ):
        errors.append("MCP live capture FD graph is invalid")
    fd_graph = observed.get("fd_graph")
    observed_fd_graph_sha256 = (
        fd_graph.get("sha256") if isinstance(fd_graph, dict) else None
    )
    if (
        session.get("connection_record_sha256")
        != bridge.get("connection_file_sha256")
        or session.get("fd_graph_sha256")
        != observed_fd_graph_sha256
        or session.get("session_id_sha256")
        != wp0001_session_identity_sha256(
            route,
            runtime,
            observed_fd_graph_sha256,
        )
    ):
        errors.append("MCP live capture session identity is invalid")
    return errors


def validate_wp0001_policy_attachment_capture(
    document: object,
    *,
    expected_facts: dict,
    expected_collector: dict | None,
) -> list[str]:
    """Validate exact process-bound sandbox/network attachment evidence."""
    errors: list[str] = []
    if not isinstance(document, dict):
        return ["policy-attachment raw capture must be an object"]
    if set(document) != {
        "schema_version",
        "capture_kind",
        "packet_id",
        "captured_at",
        "collector",
        "command",
        "result",
        "facts",
        "subjects",
        "capture_complete",
        "secret_material_included",
    }:
        errors.append(
            "policy-attachment raw capture has unexpected or missing fields"
        )
    collector = document.get("collector")
    if (
        document.get("schema_version") != 1
        or document.get("capture_kind") != "policy-attachment"
        or document.get("packet_id") != "WP-0001"
        or parse_datetime(document.get("captured_at")) is None
        or document.get("result") != "pass"
        or document.get("facts") != expected_facts
        or document.get("captured_at") != expected_facts.get("captured_at")
        or document.get("capture_complete") is not True
        or document.get("secret_material_included") is not False
        or not isinstance(collector, dict)
        or set(collector) != {"name", "version", "path", "sha256"}
        or any(
            not isinstance(collector.get(key), str) or not collector.get(key)
            for key in ("name", "version")
        )
        or {
            "path": collector.get("path"),
            "sha256": collector.get("sha256"),
        }
        != expected_collector
        or document.get("command")
        != [
            "/usr/bin/python3",
            (
                expected_collector.get("path")
                if isinstance(expected_collector, dict)
                else None
            ),
            "policy-attachment",
        ]
    ):
        errors.append("policy-attachment raw capture metadata is invalid")
    if isinstance(expected_collector, dict):
        collector_errors = validate_python_collector_source(
            expected_collector,
            "policy-attachment raw collector",
        )
        errors.extend(collector_errors)
    expected_subjects = [
        (
            "client",
            expected_facts.get("client_pid"),
            expected_facts.get("client_process_birth_id_sha256"),
        ),
        (
            "relay",
            expected_facts.get("relay_pid"),
            expected_facts.get("relay_process_birth_id_sha256"),
        ),
        (
            "editor",
            expected_facts.get("editor_pid"),
            expected_facts.get("editor_process_birth_id_sha256"),
        ),
    ]
    subjects = document.get("subjects")
    if not isinstance(subjects, list) or len(subjects) != 3:
        return errors + [
            "policy-attachment raw capture lacks exact process subjects"
        ]
    for index, (subject, expected) in enumerate(
        zip(subjects, expected_subjects, strict=True)
    ):
        role, pid, birth_id = expected
        if (
            not isinstance(subject, dict)
            or set(subject)
            != {
                "sequence",
                "role",
                "pid",
                "process_birth_id_sha256",
                "principal_uid",
                "sandbox_policy_sha256",
                "network_policy_sha256",
                "sandbox_attachment_mode",
                "network_attachment_mode",
                "sandbox_attachment_handle_sha256",
                "network_attachment_handle_sha256",
                "sandbox_attached",
                "network_attached",
                "record_sha256",
            }
            or subject.get("sequence") != index
            or subject.get("role") != role
            or subject.get("pid") != pid
            or subject.get("process_birth_id_sha256") != birth_id
            or subject.get("principal_uid")
            != expected_facts.get("principal_uid")
            or subject.get("sandbox_policy_sha256")
            != expected_facts.get("sandbox_policy_sha256")
            or subject.get("network_policy_sha256")
            != expected_facts.get("network_policy_sha256")
            or subject.get("sandbox_attachment_mode")
            not in {
                "kernel-sandbox-query",
                "read-only-mount-and-path-policy-query",
            }
            or subject.get("network_attachment_mode")
            not in {
                "kernel-network-policy-query",
                "uid-bound-pf-anchor-query",
            }
            or any(
                re.fullmatch(r"[0-9a-f]{64}", str(subject.get(key)))
                is None
                or subject.get(key) == "0" * 64
                for key in (
                    "sandbox_attachment_handle_sha256",
                    "network_attachment_handle_sha256",
                )
            )
            or subject.get("sandbox_attached") is not True
            or subject.get("network_attached") is not True
            or subject.get("record_sha256")
            != canonical_json_sha256(
                {
                    key: value
                    for key, value in subject.items()
                    if key != "record_sha256"
                }
            )
        ):
            errors.append(
                f"policy-attachment subject {index} is invalid"
            )
    return errors


def validate_wp0001_protocol_raw_capture(
    document: object,
    *,
    expected_kind: str,
    expected_facts: dict,
    enabled_tools: list[str] | None = None,
    expected_collector: dict | None = None,
) -> list[str]:
    """Validate non-empty raw protocol/session/network captures."""
    errors: list[str] = []
    capture_kind_by_record = {
        "clean-handshake": "clean-handshake-raw",
        "activation-session": "activation-session-live",
        "network-observation": "network-probes",
    }
    capture_kind = capture_kind_by_record.get(expected_kind)
    if capture_kind is None:
        return [f"unsupported raw capture kind: {expected_kind}"]
    if not isinstance(document, dict):
        return [f"{expected_kind} raw capture must be an object"]
    common_keys = {
        "schema_version",
        "capture_kind",
        "packet_id",
        "captured_at",
        "collector",
        "command",
        "result",
        "facts",
        "secret_material_included",
    }
    body_key = "probes" if expected_kind == "network-observation" else "events"
    allowed_keys = common_keys | {body_key}
    if expected_kind == "network-observation":
        allowed_keys.add("listeners")
    if set(document) != allowed_keys:
        errors.append(f"{expected_kind} raw capture has unexpected or missing fields")
    collector = document.get("collector")
    if (
        document.get("schema_version") != 1
        or document.get("capture_kind") != capture_kind
        or document.get("packet_id") != "WP-0001"
        or parse_datetime(document.get("captured_at")) is None
        or document.get("result") != "pass"
        or document.get("facts") != expected_facts
        or document.get("secret_material_included") is not False
        or not isinstance(collector, dict)
        or set(collector) != {"name", "version", "path", "sha256"}
        or not all(
            isinstance(collector.get(key), str) and collector.get(key)
            for key in ("name", "version")
        )
        or re.fullmatch(r"[0-9a-f]{64}", str(collector.get("sha256"))) is None
        or collector.get("sha256") == "0" * 64
        or not isinstance(expected_collector, dict)
        or {
            "path": collector.get("path"),
            "sha256": collector.get("sha256"),
        }
        != expected_collector
        or document.get("command")
        != [
            "/usr/bin/python3",
            (
                expected_collector.get("path")
                if isinstance(expected_collector, dict)
                else None
            ),
            expected_kind,
        ]
    ):
        errors.append(f"{expected_kind} raw capture metadata is invalid")
    if isinstance(expected_collector, dict):
        collector_errors = validate_python_collector_source(
            expected_collector,
            f"{expected_kind} raw collector",
        )
        errors.extend(collector_errors)
    expected_capture_time = expected_facts.get("captured_at")
    if (
        isinstance(expected_capture_time, str)
        and document.get("captured_at") != expected_capture_time
    ):
        errors.append(f"{expected_kind} raw capture time differs from its facts")

    if expected_kind in {"clean-handshake", "activation-session"}:
        events = document.get("events")
        if not isinstance(events, list) or len(events) < 3:
            return errors + [f"{expected_kind} raw capture has no protocol events"]
        allowed_event_keys = {
            "sequence",
            "direction",
            "event_type",
            "method",
            "request_id",
            "tool_names",
            "state",
            "record_sha256",
        }
        methods: list[str] = []
        inventories: list[list[str]] = []
        states: set[str] = set()
        for index, event in enumerate(events):
            if (
                not isinstance(event, dict)
                or not set(event).issubset(allowed_event_keys)
                or event.get("sequence") != index
                or event.get("direction")
                not in {
                    "client-to-server",
                    "server-to-client",
                    "bridge",
                    "creator",
                    "collector",
                }
                or event.get("event_type")
                not in {"request", "response", "notification", "state"}
                or re.fullmatch(
                    r"[0-9a-f]{64}",
                    str(event.get("record_sha256")),
                )
                is None
                or event.get("record_sha256") == "0" * 64
                or event.get("record_sha256")
                != canonical_json_sha256(
                    {
                        key: value
                        for key, value in event.items()
                        if key != "record_sha256"
                    }
                )
            ):
                errors.append(
                    f"{expected_kind} raw capture event {index} is invalid"
                )
                continue
            method = event.get("method")
            if isinstance(method, str):
                methods.append(method)
                if method == "tools/call" or method.startswith("Unity_"):
                    errors.append(
                        f"{expected_kind} raw capture contains a Unity tool call"
                    )
            tool_names = event.get("tool_names")
            if isinstance(tool_names, list):
                if any(not isinstance(tool, str) for tool in tool_names):
                    errors.append(
                        f"{expected_kind} raw capture has an invalid tool inventory"
                    )
                else:
                    inventories.append(tool_names)
            state_value = event.get("state")
            if isinstance(state_value, str):
                states.add(state_value)
        expected_inventory = enabled_tools if isinstance(enabled_tools, list) else []
        tools_list_responses = [
            event
            for event in events
            if isinstance(event, dict)
            and event.get("method") == "tools/list"
            and event.get("direction") == "server-to-client"
            and event.get("event_type") == "response"
            and event.get("tool_names") == expected_inventory
        ]
        tools_list_requests = [
            event
            for event in events
            if isinstance(event, dict)
            and event.get("method") == "tools/list"
            and event.get("direction") == "client-to-server"
            and event.get("event_type") == "request"
        ]
        if (
            inventories != [expected_inventory]
            or len(tools_list_responses) != 1
            or len(tools_list_requests) != 1
        ):
            errors.append(
                f"{expected_kind} raw capture lacks the exact tools/list inventory"
            )
        if expected_kind == "clean-handshake":
            allowed_methods = {
                "initialize",
                "notifications/initialized",
                "tools/list",
                "ping",
            }
            required_methods = {
                "initialize",
                "notifications/initialized",
                "tools/list",
            }
            if (
                not required_methods.issubset(methods)
                or any(method not in allowed_methods for method in methods)
                or not any(
                    isinstance(event, dict)
                    and event.get("method") == "initialize"
                    and event.get("direction") == "client-to-server"
                    and event.get("event_type") == "request"
                    for event in events
                )
                or not any(
                    isinstance(event, dict)
                    and event.get("method") == "notifications/initialized"
                    and event.get("direction") == "client-to-server"
                    and event.get("event_type") == "notification"
                    for event in events
                )
                or "disconnected" not in states
                or "approval-revoked" not in states
            ):
                errors.append(
                    "clean-handshake raw capture does not prove a revoked "
                    "handshake-only connection"
                )
            expected_sequence = [
                (
                    "client-to-server",
                    "request",
                    "initialize",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "request_id",
                        "record_sha256",
                    },
                ),
                (
                    "server-to-client",
                    "response",
                    "initialize",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "request_id",
                        "record_sha256",
                    },
                ),
                (
                    "client-to-server",
                    "notification",
                    "notifications/initialized",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "record_sha256",
                    },
                ),
                (
                    "client-to-server",
                    "request",
                    "tools/list",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "request_id",
                        "record_sha256",
                    },
                ),
                (
                    "server-to-client",
                    "response",
                    "tools/list",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "request_id",
                        "tool_names",
                        "record_sha256",
                    },
                ),
                (
                    "collector",
                    "state",
                    None,
                    "disconnected",
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "state",
                        "record_sha256",
                    },
                ),
                (
                    "creator",
                    "state",
                    None,
                    "approval-revoked",
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "state",
                        "record_sha256",
                    },
                ),
            ]
            request_pairs = ((0, 1), (3, 4))
        else:
            allowed_methods = {
                "connection/accepted",
                "approval/creator-approved",
                "initialize",
                "notifications/initialized",
                "tools/list",
                "ping",
            }
            required_methods = {
                "connection/accepted",
                "approval/creator-approved",
                "initialize",
                "notifications/initialized",
                "tools/list",
            }
            if (
                not required_methods.issubset(methods)
                or any(method not in allowed_methods for method in methods)
                or not any(
                    isinstance(event, dict)
                    and event.get("method") == "connection/accepted"
                    and event.get("direction") == "bridge"
                    and event.get("event_type") == "state"
                    for event in events
                )
                or not any(
                    isinstance(event, dict)
                    and event.get("method") == "approval/creator-approved"
                    and event.get("direction") == "creator"
                    and event.get("event_type") == "state"
                    for event in events
                )
                or "connected" not in states
            ):
                errors.append(
                    "activation-session raw capture does not prove the live "
                    "creator-approved connection"
                )
            expected_sequence = [
                (
                    "bridge",
                    "state",
                    "connection/accepted",
                    "connected",
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "state",
                        "record_sha256",
                    },
                ),
                (
                    "creator",
                    "state",
                    "approval/creator-approved",
                    "creator-approved",
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "state",
                        "record_sha256",
                    },
                ),
                (
                    "client-to-server",
                    "request",
                    "initialize",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "request_id",
                        "record_sha256",
                    },
                ),
                (
                    "server-to-client",
                    "response",
                    "initialize",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "request_id",
                        "record_sha256",
                    },
                ),
                (
                    "client-to-server",
                    "notification",
                    "notifications/initialized",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "record_sha256",
                    },
                ),
                (
                    "client-to-server",
                    "request",
                    "tools/list",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "request_id",
                        "record_sha256",
                    },
                ),
                (
                    "server-to-client",
                    "response",
                    "tools/list",
                    None,
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "method",
                        "request_id",
                        "tool_names",
                        "record_sha256",
                    },
                ),
                (
                    "collector",
                    "state",
                    None,
                    "connected",
                    {
                        "sequence",
                        "direction",
                        "event_type",
                        "state",
                        "record_sha256",
                    },
                ),
            ]
            request_pairs = ((2, 3), (5, 6))
        exact_sequence = (
            len(events) == len(expected_sequence)
            and all(
                isinstance(event, dict)
                and (
                    event.get("direction"),
                    event.get("event_type"),
                    event.get("method"),
                    event.get("state"),
                )
                == (direction, event_type, method, state)
                and set(event) == required_keys
                for event, (
                    direction,
                    event_type,
                    method,
                    state,
                    required_keys,
                ) in zip(events, expected_sequence, strict=True)
            )
        )
        request_ids_exact = (
            exact_sequence
            and all(
                isinstance(events[request_index].get("request_id"), str)
                and bool(events[request_index]["request_id"])
                and events[request_index]["request_id"]
                == events[response_index].get("request_id")
                for request_index, response_index in request_pairs
            )
            and len(
                {
                    events[request_index]["request_id"]
                    for request_index, _ in request_pairs
                }
            )
            == len(request_pairs)
        )
        if not exact_sequence or not request_ids_exact:
            errors.append(
                f"{expected_kind} raw capture does not match the exact ordered protocol state machine"
            )
    else:
        listeners = document.get("listeners")
        probes = document.get("probes")
        if not isinstance(listeners, list) or not isinstance(probes, list):
            return errors + ["network-observation raw capture is incomplete"]
        allowed_listener_keys = {
            "transport",
            "local_address",
            "port",
            "pid",
            "wildcard",
            "non_loopback_reachable",
            "record_sha256",
        }
        for index, listener in enumerate(listeners):
            if (
                not isinstance(listener, dict)
                or set(listener) != allowed_listener_keys
                or listener.get("transport") not in {"tcp4", "tcp6"}
                or not isinstance(listener.get("local_address"), str)
                or not isinstance(listener.get("port"), int)
                or isinstance(listener.get("port"), bool)
                or not 1 <= listener.get("port", 0) <= 65535
                or not isinstance(listener.get("pid"), int)
                or isinstance(listener.get("pid"), bool)
                or listener.get("pid", 0) < 1
                or not isinstance(listener.get("wildcard"), bool)
                or not isinstance(listener.get("non_loopback_reachable"), bool)
                or re.fullmatch(
                    r"[0-9a-f]{64}",
                    str(listener.get("record_sha256")),
                )
                is None
                or listener.get("record_sha256") == "0" * 64
                or listener.get("record_sha256")
                != canonical_json_sha256(
                    {
                        key: value
                        for key, value in listener.items()
                        if key != "record_sha256"
                    }
                )
            ):
                errors.append(
                    f"network-observation listener {index} is invalid"
                )
        categories = {
            "non-loopback-listener": (
                expected_facts.get("non_loopback_probe_count"),
                expected_facts.get("non_loopback_probe_success_count"),
                False,
                "non_loopback_listener",
            ),
            "loopback-control": (
                expected_facts.get("loopback_probe_count"),
                expected_facts.get("loopback_probe_success_count"),
                True,
                "loopback_control",
            ),
            "approved-egress": (
                expected_facts.get("approved_egress_probe_count"),
                expected_facts.get("approved_egress_probe_success_count"),
                True,
                "approved_egress",
            ),
            "unapproved-egress": (
                expected_facts.get("unapproved_egress_probe_count"),
                expected_facts.get("unapproved_egress_probe_success_count"),
                False,
                "unapproved_egress",
            ),
        }
        expected_targets = expected_facts.get("probe_targets")
        seen: Counter[str] = Counter()
        successes: Counter[str] = Counter()
        for index, probe in enumerate(probes):
            category = probe.get("category") if isinstance(probe, dict) else None
            expected = categories.get(category)
            if (
                not isinstance(probe, dict)
                or set(probe)
                != {
                    "sequence",
                    "category",
                    "target",
                    "attempted",
                    "success",
                    "expected_success",
                    "record_sha256",
                }
                or probe.get("sequence") != index
                or expected is None
                or not isinstance(probe.get("target"), str)
                or not isinstance(expected_targets, dict)
                or probe.get("target")
                != expected_targets.get(expected[3])
                or probe.get("attempted") is not True
                or not isinstance(probe.get("success"), bool)
                or probe.get("expected_success") is not expected[2]
                or probe.get("success") is not expected[2]
                or re.fullmatch(
                    r"[0-9a-f]{64}",
                    str(probe.get("record_sha256")),
                )
                is None
                or probe.get("record_sha256") == "0" * 64
                or probe.get("record_sha256")
                != canonical_json_sha256(
                    {
                        key: value
                        for key, value in probe.items()
                        if key != "record_sha256"
                    }
                )
            ):
                errors.append(f"network-observation probe {index} is invalid")
                continue
            seen[category] += 1
            successes[category] += int(probe["success"])
        for category, (
            count,
            success_count,
            _expected_success,
            _target_key,
        ) in categories.items():
            if seen[category] != count or successes[category] != success_count:
                errors.append(
                    f"network-observation probe totals differ for {category}"
                )
        if (
            len(listeners) != expected_facts.get("tcp_listener_count")
            or sum(bool(item.get("wildcard")) for item in listeners if isinstance(item, dict))
            != expected_facts.get("wildcard_listener_count")
            or sum(
                bool(item.get("non_loopback_reachable"))
                for item in listeners
                if isinstance(item, dict)
            )
            != expected_facts.get("non_loopback_reachable_listener_count")
        ):
            errors.append("network-observation listener totals differ from facts")
    return errors


def validate_wp0001_evidence_record_data(
    document: object,
    *,
    expected_kind: str,
    expected_facts: dict,
    label: str,
    raw_enabled_tools: list[str] | None = None,
    raw_route: dict | None = None,
    raw_runtime: dict | None = None,
    route_contract_sha256: str | None = None,
    protected_config_sha256: str | None = None,
    raw_collectors: dict | None = None,
) -> list[str]:
    """Validate one content-addressed WP-0001 record and its exact bound facts."""
    schema = load_json(WP0001_A1_EVIDENCE_RECORD_SCHEMA)
    errors = validate_schema_subset(document, schema, schema, label)
    if not isinstance(document, dict):
        return errors + [f"{label} must be an object"]
    if document.get("document_kind") != expected_kind:
        errors.append(f"{label} has the wrong document kind")
    if document.get("facts") != expected_facts:
        errors.append(f"{label} facts differ from the A1 boundary")
    source_artifacts = document.get("source_artifacts")
    source_paths: set[str] = set()
    if isinstance(source_artifacts, list):
        for source in source_artifacts:
            if not isinstance(source, dict):
                continue
            source_path = source.get("path")
            if isinstance(source_path, str):
                source_paths.add(source_path)
            _, source_errors = validate_repo_evidence_reference(
                source,
                f"{label} source artifact",
            )
            errors.extend(source_errors)
    commands_prefix = "docs/evidence/WP-0001/a1-activation/commands/"
    screenshots_prefix = "docs/evidence/WP-0001/a1-activation/screenshots/"
    if expected_kind == "project-seed":
        required_seed_sources = {
            "Game/Packages/manifest.json",
            "Game/Packages/packages-lock.json",
            "Game/ProjectSettings/ProjectSettings.asset",
            "Game/ProjectSettings/ProjectVersion.txt",
        }
        if source_paths != required_seed_sources:
            errors.append(f"{label} must bind exactly the four protected seed files")
        if document.get("capture_method") != "protected-git-derivation":
            errors.append(f"{label} must use protected-git-derivation")
    elif expected_kind in {"entitlement-linkage", "project-identity"}:
        if not any(path.startswith(screenshots_prefix) for path in source_paths):
            errors.append(f"{label} lacks a creator-reviewed screenshot source")
        if document.get("capture_method") not in {
            "creator-ui-capture",
            "creator-command-and-ui-capture",
        }:
            errors.append(f"{label} has the wrong capture method")
    elif expected_kind == "toolchain":
        if (
            not any(path.startswith(commands_prefix) for path in source_paths)
            or not any(path.startswith(screenshots_prefix) for path in source_paths)
        ):
            errors.append(f"{label} requires both command and UI source artifacts")
        if document.get("capture_method") != "creator-command-and-ui-capture":
            errors.append(f"{label} has the wrong capture method")
    elif expected_kind == "quarantine":
        live_capture_path = (
            "docs/evidence/WP-0001/a1-activation/commands/quarantine-live.json"
        )
        policy_attachment_path = WP0001_POLICY_ATTACHMENT_RAW_PATH
        policy_attachment_collector = (
            raw_collectors.get("policy_attachment")
            if isinstance(raw_collectors, dict)
            else None
        )
        policy_attachment_collector_path = (
            policy_attachment_collector.get("path")
            if isinstance(policy_attachment_collector, dict)
            else None
        )
        required_policy_sources = {
            "docs/evidence/WP-0001/a1-activation/sandbox.policy",
            "docs/evidence/WP-0001/a1-activation/network.policy",
            WP0001_QUARANTINE_LIVE_VERIFIER_PATH,
            policy_attachment_path,
        }
        if isinstance(policy_attachment_collector_path, str):
            required_policy_sources.add(policy_attachment_collector_path)
        if (
            not required_policy_sources.issubset(source_paths)
            or live_capture_path not in source_paths
        ):
            errors.append(f"{label} lacks policy bytes or command probe sources")
        live_capture, capture_error = safe_repo_path(
            live_capture_path,
            f"{label} live capture path",
        )
        if (
            capture_error is None
            and live_capture is not None
            and live_capture.is_file()
        ):
            observed = load_json(live_capture)
            expected_repository = expected_facts.get("repository", {})
            expected_runtime = expected_facts.get("runtime_boundary", {})
            checks = observed.get("checks", {}) if isinstance(observed, dict) else {}
            observed_facts = (
                observed.get("observed", {}) if isinstance(observed, dict) else {}
            )
            if (
                observed.get("validator_version") != "wp0001-a1-live-v1"
                or observed.get("result") != "pass"
                or observed.get("candidate_root")
                != expected_repository.get("absolute_root")
                or observed_facts.get("trusted_root")
                != expected_repository.get("trusted_root")
                or observed.get("base_commit")
                != expected_repository.get("base_commit")
                or not isinstance(checks, dict)
                or set(checks) != WP0001_LIVE_QUARANTINE_CHECKS
                or any(value is not True for value in checks.values())
                or observed_facts.get("principal_uid")
                != expected_runtime.get("principal_uid")
                or observed_facts.get("environment_bindings")
                != expected_runtime.get("environment_bindings")
                or observed_facts.get("client_environment")
                != {
                    key: expected_runtime.get(
                        "client_environment_guard", {}
                    ).get(key)
                    for key in (
                        "CODEX_HOME",
                        "XDG_CONFIG_HOME",
                        "XDG_CACHE_HOME",
                        "XDG_DATA_HOME",
                        "GIT_CONFIG_NOSYSTEM",
                        "GIT_CONFIG_GLOBAL",
                        "GIT_TERMINAL_PROMPT",
                    )
                }
                or observed_facts.get("boot_session_sha256")
                != expected_runtime.get("boot_session_sha256")
                or observed_facts.get("runtime_home")
                != expected_runtime.get("ephemeral_home_root")
                or observed_facts.get("runtime_temp")
                != expected_runtime.get("private_temp_root")
                or observed_facts.get("shared_temp_roots")
                != expected_runtime.get("ambient_shared_temp_roots")
                or observed_facts.get("socket_exception")
                != expected_runtime.get("ambient_shared_temp_write_exceptions", [None])[0]
                or observed_facts.get("head")
                != expected_repository.get("base_commit")
                or observed_facts.get("git_directory")
                != f"{expected_repository.get('absolute_root')}/.git"
                or observed_facts.get("git_common_directory")
                != f"{expected_repository.get('absolute_root')}/.git"
                or observed_facts.get("status_porcelain") != []
                or observed_facts.get("remotes") != []
                or observed_facts.get("symbolic_head") not in {"", None}
                or observed_facts.get("forbidden_credential_env_keys_present")
                != []
            ):
                errors.append(f"{label} live quarantine capture does not prove the boundary")
        attachment_capture, attachment_error = safe_repo_path(
            policy_attachment_path,
            f"{label} policy attachment capture path",
        )
        if (
            attachment_error is None
            and attachment_capture is not None
            and attachment_capture.is_file()
        ):
            route = raw_route if isinstance(raw_route, dict) else {}
            client = (
                route.get("client")
                if isinstance(route.get("client"), dict)
                else {}
            )
            relay = (
                route.get("relay")
                if isinstance(route.get("relay"), dict)
                else {}
            )
            bridge = (
                route.get("bridge")
                if isinstance(route.get("bridge"), dict)
                else {}
            )
            controls = (
                route.get("controls")
                if isinstance(route.get("controls"), dict)
                else {}
            )
            runtime = expected_facts.get("runtime_boundary", {})
            approved_environment = expected_facts.get(
                "approved_environment", {}
            )
            attachment_facts = {
                "captured_at": controls.get("captured_at"),
                "principal_uid": runtime.get("principal_uid"),
                "boot_session_sha256": runtime.get(
                    "boot_session_sha256"
                ),
                "sandbox_policy_sha256": approved_environment.get(
                    "sandbox_profile_sha256"
                ),
                "network_policy_sha256": approved_environment.get(
                    "network_policy_sha256"
                ),
                "client_pid": client.get("pid"),
                "relay_pid": relay.get("pid"),
                "editor_pid": bridge.get("editor_pid"),
                "client_process_birth_id_sha256": client.get(
                    "process_birth_id_sha256"
                ),
                "relay_process_birth_id_sha256": relay.get(
                    "process_birth_id_sha256"
                ),
                "editor_process_birth_id_sha256": bridge.get(
                    "process_birth_id_sha256"
                ),
            }
            errors.extend(
                f"{label} {error}"
                for error in validate_wp0001_policy_attachment_capture(
                    load_json(attachment_capture),
                    expected_facts=attachment_facts,
                    expected_collector=policy_attachment_collector,
                )
            )
    elif expected_kind in {
        "mcp-route",
        "bridge-discovery",
        "clean-handshake",
        "activation-session",
        "network-observation",
    }:
        required_raw_source = WP0001_REQUIRED_RAW_SOURCE_BY_KIND[expected_kind]
        if required_raw_source not in source_paths:
            errors.append(
                f"{label} lacks required raw capture {required_raw_source}"
            )
        if (
            expected_kind in {"mcp-route", "bridge-discovery"}
            and WP0001_MCP_LIVE_VERIFIER_PATH not in source_paths
        ):
            errors.append(
                f"{label} does not bind {WP0001_MCP_LIVE_VERIFIER_PATH}"
            )
        raw_path, raw_path_error = safe_repo_path(
            required_raw_source,
            f"{label} raw capture path",
        )
        if (
            raw_path_error is None
            and raw_path is not None
            and raw_path.is_file()
            and expected_kind
            in {
                "clean-handshake",
                "activation-session",
                "network-observation",
            }
        ):
            errors.extend(
                f"{label} {error}"
                for error in validate_wp0001_protocol_raw_capture(
                    load_json(raw_path),
                    expected_kind=expected_kind,
                    expected_facts=expected_facts,
                    enabled_tools=raw_enabled_tools,
                    expected_collector=(
                        raw_collectors.get(
                            "network"
                            if expected_kind == "network-observation"
                            else "protocol"
                        )
                        if isinstance(raw_collectors, dict)
                        else None
                    ),
                )
            )
            raw_document = load_json(raw_path)
            collector_path = (
                raw_document.get("collector", {}).get("path")
                if isinstance(raw_document, dict)
                and isinstance(raw_document.get("collector"), dict)
                else None
            )
            if collector_path not in source_paths:
                errors.append(
                    f"{label} does not bind its raw collector source"
                )
        if (
            raw_path_error is None
            and raw_path is not None
            and raw_path.is_file()
            and expected_kind in {"mcp-route", "bridge-discovery"}
            and isinstance(raw_route, dict)
            and isinstance(raw_runtime, dict)
            and isinstance(route_contract_sha256, str)
        ):
            errors.extend(
                f"{label} {error}"
                for error in validate_wp0001_mcp_live_capture(
                    load_json(raw_path),
                    route=raw_route,
                    runtime=raw_runtime,
                    route_contract_sha256=route_contract_sha256,
                    protected_config_sha256=protected_config_sha256,
                )
            )
    elif expected_kind == "deviations":
        if "docs/evidence/WP-0001/pre-a1-readiness-20260716.json" not in source_paths:
            errors.append(f"{label} does not bind the preserved readiness record")
    return errors


def load_json_bytes(raw: bytes, label: str) -> tuple[object | None, list[str]]:
    try:
        return json.loads(raw.decode("utf-8")), []
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        return None, [f"{label} is not valid UTF-8 JSON: {exc}"]


def validate_wp0002_working_tree_scope_capture(
    manifest: dict,
    packet: dict,
    activation_receipt: dict | None,
) -> list[str]:
    """Resolve and verify WP-0002's attested working-tree scope proof."""
    errors: list[str] = []
    label = "WP-0002 working-tree scope capture"
    reference = manifest.get("working_tree_scope_capture")
    if not isinstance(reference, dict):
        return [f"{label} reference is missing"]
    expected_uri = (
        "repo://docs/evidence/WP-0002/scope-capture/working-tree-scope.json"
    )
    if reference.get("uri") != expected_uri:
        return [f"{label} must use {expected_uri}"]
    relative = expected_uri.removeprefix("repo://")
    capture_path, path_error = safe_repo_path(relative, label)
    if path_error:
        return errors + [path_error]
    if capture_path is None or not capture_path.exists():
        return errors + [f"{label} file does not exist"]
    try:
        metadata = capture_path.lstat()
    except OSError as exc:
        return errors + [f"{label} cannot be inspected: {exc}"]
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return errors + [f"{label} must be a regular non-symlink file"]

    actual_hash = sha256_file(capture_path)
    if reference.get("sha256") != actual_hash:
        errors.append(f"{label} raw hash mismatch")
    errors.extend(validate_instance_shape(capture_path, WP0002_WORKING_TREE_SCOPE_SCHEMA))
    capture = load_json(capture_path)

    reservation = packet.get("reservation", {})
    # This retained capture sealed the original activation boundary.  New
    # read-only controls are proven by the separate append-only amendment
    # capture and must not rewrite the historical evidence contract.
    protected_paths = WP0002_ACTIVATION_PROTECTED_PATHS
    collector, collector_errors = _load_wp0002_scope_collector()
    errors.extend(collector_errors)
    if collector is not None:
        verifier = collector.get("verify_scope_capture")
        if callable(verifier):
            errors.extend(
                verifier(
                    REPO_ROOT,
                    relative,
                    expected_capture_sha256=actual_hash,
                    expected_base_commit=reservation.get("base_commit"),
                    expected_head_commit=reservation.get("base_commit"),
                    expected_checkpoint_commit=reservation.get("base_commit"),
                    expected_reservation_paths=reservation.get("paths", []),
                    expected_protected_paths=protected_paths,
                    receipt_issued_at=(
                        activation_receipt.get("issued_at")
                        if isinstance(activation_receipt, dict)
                        else ""
                    ),
                    # Receipt freshness remains enforced by the collector; only
                    # post-activation working-tree comparisons are retained.
                    mode="terminal-retained",
                )
            )
        else:
            errors.append("WP-0002 scope collector lacks its verifier")

    receipt_hashes = (
        activation_receipt.get("artifact_sha256", {})
        if isinstance(activation_receipt, dict)
        else {}
    )
    if receipt_hashes.get(relative) != actual_hash:
        errors.append(f"WP-0002 activation receipt does not bind scope capture bytes")
    for artifact in capture.get("artifacts", {}).values():
        if not isinstance(artifact, dict):
            continue
        artifact_path = artifact.get("path")
        artifact_hash = artifact.get("sha256")
        if receipt_hashes.get(artifact_path) != artifact_hash:
            errors.append(
                f"WP-0002 activation receipt does not bind scope artifact {artifact_path}"
            )
    return errors


def _load_wp0002_scope_collector() -> tuple[dict[str, object] | None, list[str]]:
    expected_hash = WP0002_PROTECTED_SELF_VERIFICATION.get(
        "Tools/Validation/collect_wp0002_scope_capture.py"
    )
    try:
        metadata = WP0002_SCOPE_COLLECTOR.lstat()
        source = WP0002_SCOPE_COLLECTOR.read_bytes()
    except OSError as exc:
        return None, [f"WP-0002 protected scope collector is missing: {exc}"]
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return None, ["WP-0002 protected scope collector is not a regular file"]
    actual_hash = hashlib.sha256(source).hexdigest()
    if not isinstance(expected_hash, str) or actual_hash != expected_hash:
        return None, ["WP-0002 protected scope collector hash mismatch"]
    namespace: dict[str, object] = {"__name__": "wp0002_scope_collector_pinned"}
    try:
        exec(compile(source, str(WP0002_SCOPE_COLLECTOR), "exec"), namespace)
    except Exception as exc:
        return None, [f"WP-0002 protected scope collector cannot load: {exc}"]
    return namespace, []


def _load_wp0002_transaction_verifier() -> tuple[dict[str, object] | None, list[str]]:
    """Load hash-pinned pure report validators for offline closure checks."""
    expected_hash = WP0002_PROTECTED_SELF_VERIFICATION.get(
        "Tools/Validation/verify_wp0002_local_operator_transaction.py"
    )
    try:
        metadata = WP0002_LOCAL_OPERATOR_ONLINE_VERIFIER.lstat()
        source = WP0002_LOCAL_OPERATOR_ONLINE_VERIFIER.read_bytes()
    except OSError as exc:
        return None, [f"WP-0002 protected transaction verifier is missing: {exc}"]
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return None, ["WP-0002 protected transaction verifier is not a regular file"]
    actual_hash = hashlib.sha256(source).hexdigest()
    if not isinstance(expected_hash, str) or actual_hash != expected_hash:
        return None, ["WP-0002 protected transaction verifier hash mismatch"]
    namespace: dict[str, object] = {
        "__name__": "wp0002_transaction_verifier_pinned"
    }
    try:
        exec(
            compile(
                source,
                str(WP0002_LOCAL_OPERATOR_ONLINE_VERIFIER),
                "exec",
            ),
            namespace,
        )
    except Exception as exc:
        return None, [f"WP-0002 protected transaction verifier cannot load: {exc}"]
    return namespace, []


def _load_wp0002_repo_evidence(
    reference: object,
    *,
    expected_uri: str,
    schema_path: Path,
    label: str,
) -> tuple[dict | None, str | None, str | None, list[str]]:
    errors: list[str] = []
    if not isinstance(reference, dict) or reference.get("uri") != expected_uri:
        return None, None, None, [f"{label} reference is not exact"]
    relative = expected_uri.removeprefix("repo://")
    evidence_path, path_error = safe_repo_path(relative, label)
    if path_error:
        return None, relative, None, [path_error]
    if evidence_path is None or not evidence_path.exists():
        return None, relative, None, [f"{label} file does not exist"]
    try:
        metadata = evidence_path.lstat()
    except OSError as exc:
        return None, relative, None, [f"{label} cannot be inspected: {exc}"]
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return None, relative, None, [f"{label} must be a regular non-symlink file"]
    actual_hash = sha256_file(evidence_path)
    if reference.get("sha256") != actual_hash:
        errors.append(f"{label} raw hash mismatch")
    errors.extend(validate_instance_shape(evidence_path, schema_path))
    try:
        evidence = load_json(evidence_path)
    except (OSError, json.JSONDecodeError) as exc:
        return None, relative, actual_hash, errors + [f"{label} cannot be parsed: {exc}"]
    return evidence, relative, actual_hash, errors


def validate_wp0002_local_operator_amendment_scope_capture(
    manifest: dict,
    packet: dict,
    amendment_receipt: dict,
) -> list[str]:
    """Verify the append-only amendment's retained canonical-root snapshot."""
    errors: list[str] = []
    reference = manifest.get("local_operator_amendment_scope_capture")
    required_reference_keys = {
        "uri",
        "sha256",
        "base_commit",
        "head_commit",
        "checkpoint_commit",
    }
    if not isinstance(reference, dict) or set(reference) != required_reference_keys:
        return ["WP-0002 local operator amendment scope reference is not exact"]
    if (
        reference.get("uri") != WP0002_LOCAL_OPERATOR_SCOPE_URI
        or re.fullmatch(r"[0-9a-f]{64}", str(reference.get("sha256"))) is None
        or any(
            re.fullmatch(r"[0-9a-f]{40}", str(reference.get(field))) is None
            for field in ("base_commit", "head_commit", "checkpoint_commit")
        )
    ):
        return ["WP-0002 local operator amendment scope reference is not exact"]

    capture, relative, actual_hash, load_errors = _load_wp0002_repo_evidence(
        reference,
        expected_uri=WP0002_LOCAL_OPERATOR_SCOPE_URI,
        schema_path=WP0002_LOCAL_OPERATOR_SCOPE_SCHEMA,
        label="WP-0002 local operator amendment scope capture",
    )
    errors.extend(load_errors)
    if not isinstance(capture, dict) or relative is None or actual_hash is None:
        return errors

    for field in ("base_commit", "head_commit", "checkpoint_commit"):
        if capture.get(field) != reference.get(field):
            errors.append(
                f"WP-0002 local operator amendment scope {field} is not bound"
            )

    dirty_paths = capture.get("dirty_paths", [])
    expected_dirty_states: dict[str, str] = {}
    dirty_owners: dict[str, str] = {}
    dirty_policies: dict[str, str] = {}
    if not isinstance(dirty_paths, list):
        errors.append("WP-0002 local operator amendment dirty set is missing")
    else:
        for item in dirty_paths:
            if not isinstance(item, dict) or not isinstance(item.get("path"), str):
                errors.append(
                    "WP-0002 local operator amendment dirty set is malformed"
                )
                continue
            path = item["path"]
            if path in expected_dirty_states:
                errors.append(
                    "WP-0002 local operator amendment dirty set repeats a path"
                )
                continue
            expected_dirty_states[path] = item.get("normalized_git_state")
            dirty_owners[path] = item.get("owner")
            dirty_policies[path] = item.get("policy")
    collector, collector_errors = _load_wp0002_scope_collector()
    errors.extend(collector_errors)
    if collector is not None:
        verifier = collector.get("verify_scope_capture")
        classifier = collector.get("amendment_dirty_profile")
        status_arguments = collector.get("AMENDMENT_STATUS_ARGUMENTS")
        expected_status_arguments = (
            "status",
            "--porcelain=v2",
            "-z",
            "--untracked-files=all",
        )
        if status_arguments != expected_status_arguments:
            errors.append(
                "WP-0002 protected scope collector amendment status tuple differs"
            )
        elif not callable(classifier):
            errors.append(
                "WP-0002 scope collector lacks its amendment classifier"
            )
        else:
            try:
                (
                    derived_states,
                    derived_owners,
                    derived_policies,
                ) = classifier(expected_dirty_states)
            except ValueError as exc:
                errors.append(
                    "WP-0002 local operator amendment dirty classification "
                    f"is invalid: {exc}"
                )
            else:
                if (
                    expected_dirty_states != derived_states
                    or dirty_owners != derived_owners
                    or dirty_policies != derived_policies
                ):
                    errors.append(
                        "WP-0002 local operator amendment dirty classification "
                        "is not a derived exact match"
                    )
        if status_arguments == expected_status_arguments and callable(verifier):
            errors.extend(
                verifier(
                    REPO_ROOT,
                    relative,
                    expected_capture_sha256=actual_hash,
                    expected_base_commit=reference.get("base_commit"),
                    expected_head_commit=reference.get("head_commit"),
                    expected_checkpoint_commit=reference.get("checkpoint_commit"),
                    expected_reservation_paths=packet.get("reservation", {}).get(
                        "paths", []
                    ),
                    expected_protected_paths=manifest.get(
                        "permission_boundary", {}
                    ).get("protected_paths_read_only", []),
                    receipt_issued_at=amendment_receipt.get("issued_at"),
                    mode="terminal-retained",
                    expected_repository_root=WP0002_CANONICAL_REPOSITORY_ROOT,
                    status_arguments=status_arguments,
                )
            )
        elif not callable(verifier):
            errors.append("WP-0002 scope collector lacks its verifier")
    return errors


def _load_wp0002_raw_json_evidence(
    reference: object,
    *,
    expected_uri: str,
    label: str,
) -> tuple[object | None, str | None, str | None, list[str]]:
    if not isinstance(reference, dict) or reference.get("uri") != expected_uri:
        return None, None, None, [f"{label} reference is not exact"]
    relative = expected_uri.removeprefix("repo://")
    path, path_error = safe_repo_path(relative, label)
    if path_error:
        return None, relative, None, [path_error]
    if path is None or not path.exists():
        return None, relative, None, [f"{label} file does not exist"]
    try:
        metadata = path.lstat()
    except OSError as exc:
        return None, relative, None, [f"{label} cannot be inspected: {exc}"]
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return None, relative, None, [f"{label} must be a regular non-symlink file"]
    actual_hash = sha256_file(path)
    errors: list[str] = []
    if reference.get("sha256") != actual_hash:
        errors.append(f"{label} raw hash mismatch")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        return None, relative, actual_hash, errors + [f"{label} is not valid JSON: {exc}"]
    return value, relative, actual_hash, errors


def validate_wp0002_external_policy_and_github_capture(
    manifest: dict,
    packet: dict,
    activation_receipt: dict | None,
) -> list[str]:
    """Verify external Cursor-App policy evidence and live branch protection."""
    errors: list[str] = []
    external = manifest.get("external_cursor_review_control", {})
    expected_external = {
        "provider": "Cursor Approval Agent",
        "check_context": "Cursor Approval Agent: Pull Request Approver",
        "github_app_id": 1210556,
        "optional_review": True,
        "blocking_required_check": False,
        "classification": "independent-ai-review-not-deterministic-validator",
        "prompt_injection_residual_acknowledged": True,
        "deterministic_non_llm_execution_seam": None,
        "policy_path": "Tools/Validation/validate_wp0002_policy.py",
        "policy_source_sha": external.get("policy_source_sha"),
        "same_repository_only": True,
        "base_ref": "main",
        "head_ref_prefix": "agent/",
        "candidate_code_execution": False,
        "creator_manual_transition_required": True,
        "manual_gate_transitions": [
            "proposed-to-accepted",
            "accepted-to-active",
            "active-to-verifying",
            "verifying-to-candidate",
            "candidate-to-released",
        ],
        "configuration_capture": external.get("configuration_capture"),
    }
    if external != expected_external:
        errors.append("WP-0002 external Cursor-App policy boundary is not exact")
    policy_source_sha = external.get("policy_source_sha")
    if not isinstance(policy_source_sha, str) or not re.fullmatch(
        r"[0-9a-f]{40}", policy_source_sha
    ):
        errors.append("WP-0002 external policy lacks a protected source SHA")

    config, config_relative, config_hash, config_errors = _load_wp0002_repo_evidence(
        external.get("configuration_capture"),
        expected_uri="repo://docs/evidence/WP-0002/cursor-approval-policy.json",
        schema_path=WP0002_EXTERNAL_POLICY_CAPTURE_SCHEMA,
        label="WP-0002 external Cursor-App configuration capture",
    )
    errors.extend(config_errors)
    repository = manifest.get("repository", {})
    if isinstance(config, dict):
        raw_config, raw_config_relative, raw_config_hash, raw_config_errors = (
            _load_wp0002_raw_json_evidence(
                config.get("raw_configuration_artifact"),
                expected_uri=(
                    "repo://docs/evidence/WP-0002/cursor-approval-policy.raw-config.json"
                ),
                label="WP-0002 raw external Cursor-App configuration",
            )
        )
        errors.extend(raw_config_errors)
        if config.get("configuration_sha256") != raw_config_hash:
            errors.append("WP-0002 external policy configuration hash does not bind raw bytes")
        expected_raw_config = {
            "revision": config.get("configuration_revision"),
            "check_context": "Cursor Approval Agent: Pull Request Approver",
            "github_app_id": 1210556,
            "classification": "independent-ai-review-not-deterministic-validator",
            "prompt_injection_residual_acknowledged": True,
            "deterministic_non_llm_execution_seam": None,
            "policy_source_sha": policy_source_sha,
            "candidate_code_execution": False,
            "creator_manual_transition_required": True,
        }
        if raw_config != expected_raw_config:
            errors.append("WP-0002 normalized Cursor review differs from raw configuration bytes")
        if config.get("policy_source_sha") != policy_source_sha:
            errors.append("WP-0002 external policy configuration binds another source SHA")
        verification = config.get("verification_run", {})
        if verification.get("base_sha") != repository.get("base_commit"):
            errors.append("WP-0002 external policy verification binds another base")
        if verification.get("head_sha") != repository.get("branch_head_commit"):
            errors.append("WP-0002 external policy verification binds another head")
        config_time = parse_datetime(config.get("captured_at"))
        receipt_time = parse_datetime(
            activation_receipt.get("issued_at")
            if isinstance(activation_receipt, dict)
            else None
        )
        if (
            config_time is None
            or receipt_time is None
            or config_time > receipt_time
            or receipt_time - config_time > timedelta(minutes=15)
        ):
            errors.append("WP-0002 external policy configuration capture is not fresh")

    policy_hash = WP0002_PROTECTED_SELF_VERIFICATION.get(
        "Tools/Validation/validate_wp0002_policy.py"
    )
    if isinstance(policy_source_sha, str) and isinstance(policy_hash, str):
        policy_blob = run_foundation_git(
            [
                "show",
                f"{policy_source_sha}:Tools/Validation/validate_wp0002_policy.py",
            ]
        )
        if (
            policy_blob.returncode != 0
            or hashlib.sha256(policy_blob.stdout).hexdigest() != policy_hash
        ):
            errors.append("WP-0002 external policy source bytes do not match the frozen checker")
    else:
        errors.append("WP-0002 frozen external policy hash is not configured")

    protection_ref = manifest.get("github_protection_capture", {})
    if protection_ref.get("required_schema") != "wp0002-github-protection-v1":
        errors.append("WP-0002 GitHub protection capture schema declaration is wrong")
    protection, protection_relative, protection_hash, protection_errors = (
        _load_wp0002_repo_evidence(
            protection_ref.get("artifact"),
            expected_uri="repo://docs/evidence/WP-0002/github-protection.json",
            schema_path=WP0002_GITHUB_PROTECTION_SCHEMA,
            label="WP-0002 GitHub protection capture",
        )
    )
    errors.extend(protection_errors)
    canary_state = manifest.get("git_safety", {}).get("policy_canary_state")
    expected_checks = [
        {"context": "validate", "app_id": 15368},
        {"context": "wp0002-core", "app_id": 15368},
    ]
    if canary_state == "proven-candidate-independent":
        expected_checks.append({"context": "wp0002-policy", "app_id": 15368})
    if isinstance(protection, dict):
        exact_fields = {
            "repository": "AC-21/sasha-the-land-pirate",
            "protected_branch": "main",
            "stage_c_base_sha": packet.get("reservation", {}).get("base_commit"),
            "post_merge_live_monitoring": "required-separate-live-capture",
            "strict_up_to_date": True,
            "required_status_checks": expected_checks,
            "policy_canary_state": canary_state,
            "enforce_admins": True,
            "pull_request_required": True,
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
            "push_restrictions": {"users": [], "teams": [], "apps": []},
            "conversation_resolution_required": True,
            "linear_history_required": True,
            "force_push_disabled": True,
            "deletion_disabled": True,
            "merge_methods": {"merge": False, "rebase": False, "squash": True},
            "auto_merge_enabled": True,
        }
        for field, expected in exact_fields.items():
            if protection.get(field) != expected:
                errors.append(f"WP-0002 GitHub protection {field} is not exact")
        expected_policy = {
            "policy_path": "Tools/Validation/validate_wp0002_policy.py",
            "policy_source_sha": policy_source_sha,
            "configuration_capture_uri": (
                "repo://docs/evidence/WP-0002/cursor-approval-policy.json"
            ),
            "configuration_capture_sha256": config_hash,
            "candidate_code_execution": False,
            "classification": "independent-ai-review-not-deterministic-validator",
            "prompt_injection_residual_acknowledged": True,
            "deterministic_non_llm_execution_seam": None,
            "creator_manual_transition_required": True,
        }
        if protection.get("external_cursor_review") != expected_policy:
            errors.append("WP-0002 GitHub protection does not bind exact external Cursor review")
        raw_protection, raw_protection_relative, raw_protection_hash, raw_errors = (
            _load_wp0002_raw_json_evidence(
                protection.get("raw_branch_protection_artifact"),
                expected_uri=(
                    "repo://docs/evidence/WP-0002/github-protection.raw-api.json"
                ),
                label="WP-0002 raw GitHub branch-protection API capture",
            )
        )
        errors.extend(raw_errors)
        ruleset_inventory = protection.get("ruleset_inventory", {})
        raw_rulesets, raw_rulesets_relative, raw_rulesets_hash, ruleset_errors = (
            _load_wp0002_raw_json_evidence(
                ruleset_inventory.get("raw_artifact"),
                expected_uri="repo://docs/evidence/WP-0002/github-protection.raw-rulesets.json",
                label="WP-0002 raw GitHub ruleset inventory",
            )
        )
        errors.extend(ruleset_errors)
        if raw_rulesets != [] or ruleset_inventory.get("count") != 0 or ruleset_inventory.get("rulesets") != []:
            errors.append("WP-0002 GitHub ruleset inventory must prove an exact empty response")
        expected_raw = {
            "main": {"sha": protection.get("stage_c_base_sha")},
            "branch_protection": {
                "required_status_checks": {
                    "strict": protection.get("strict_up_to_date"),
                    "checks": protection.get("required_status_checks"),
                },
                "enforce_admins": {"enabled": protection.get("enforce_admins")},
                "required_pull_request_reviews": protection.get(
                    "required_pull_request_reviews"
                ),
                "restrictions": protection.get("push_restrictions"),
                "required_conversation_resolution": {
                    "enabled": protection.get("conversation_resolution_required")
                },
                "required_linear_history": {
                    "enabled": protection.get("linear_history_required")
                },
                "allow_force_pushes": {
                    "enabled": not protection.get("force_push_disabled")
                },
                "allow_deletions": {
                    "enabled": not protection.get("deletion_disabled")
                },
            },
            "repository": {
                "allow_auto_merge": protection.get("auto_merge_enabled"),
                "allow_squash_merge": protection.get("merge_methods", {}).get(
                    "squash"
                ),
                "allow_merge_commit": protection.get("merge_methods", {}).get(
                    "merge"
                ),
                "allow_rebase_merge": protection.get("merge_methods", {}).get(
                    "rebase"
                ),
            },
        }
        if raw_protection != expected_raw:
            errors.append("WP-0002 normalized protection differs from raw GitHub API bytes")
        observed_time = parse_datetime(protection.get("observed_at"))
        receipt_time = parse_datetime(
            activation_receipt.get("issued_at")
            if isinstance(activation_receipt, dict)
            else None
        )
        if (
            observed_time is None
            or receipt_time is None
            or observed_time > receipt_time
            or receipt_time - observed_time > timedelta(minutes=15)
        ):
            errors.append("WP-0002 GitHub protection capture is not fresh")

    receipt_hashes = (
        activation_receipt.get("artifact_sha256", {})
        if isinstance(activation_receipt, dict)
        else {}
    )
    for relative, actual_hash, label in (
        (config_relative, config_hash, "external policy configuration"),
        (protection_relative, protection_hash, "GitHub protection capture"),
        (
            locals().get("raw_config_relative"),
            locals().get("raw_config_hash"),
            "raw external policy configuration",
        ),
        (
            locals().get("raw_protection_relative"),
            locals().get("raw_protection_hash"),
            "raw GitHub branch protection",
        ),
        (
            locals().get("raw_rulesets_relative"),
            locals().get("raw_rulesets_hash"),
            "raw GitHub ruleset inventory",
        ),
    ):
        if relative is not None and receipt_hashes.get(relative) != actual_hash:
            errors.append(f"WP-0002 activation receipt does not bind {label} bytes")
    return errors


def validate_wp0002_local_operator_transaction_evidence(
    amendment_receipt: dict,
    manifest: dict | None = None,
) -> list[str]:
    """Validate the sealed online-verifier report chain without claiming live auth."""
    errors: list[str] = []
    reports: dict[str, dict] = {}
    report_hashes: dict[str, str] = {}
    paths = WP0002_LOCAL_OPERATOR_TRANSACTION_EVIDENCE_CONTRACT[
        "repository_evidence_paths"
    ]
    resolved_paths: dict[str, Path] = {}
    for phase in ("authority", "pre-merge", "complete"):
        path, path_error = safe_repo_path(
            paths[phase], f"WP-0002 local operator {phase} online evidence"
        )
        if path_error is not None or path is None:
            errors.append(path_error or "WP-0002 evidence path cannot be resolved")
        else:
            resolved_paths[phase] = path
    if errors:
        return errors
    present_phases = {
        phase for phase, path in resolved_paths.items() if path.is_file()
    }
    if not present_phases:
        # The exact receipt-only control PR cannot contain reports generated
        # after Stage-1 and after merge.  All-absent is therefore a valid but
        # explicitly non-executable pending-evidence state; the separate
        # protected closure PR must add all three together.
        return []
    if present_phases != {"authority", "pre-merge", "complete"}:
        return [
            "WP-0002 local operator repository evidence is partial; all three "
            "closure reports must appear together"
        ]
    for phase in ("authority", "pre-merge", "complete"):
        path = resolved_paths[phase]
        try:
            metadata = path.lstat()
        except OSError as exc:
            errors.append(
                f"WP-0002 local operator {phase} online evidence cannot be inspected: {exc}"
            )
            continue
        if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
            errors.append(
                f"WP-0002 local operator {phase} online evidence is not a regular file"
            )
            continue
        errors.extend(
            validate_instance_shape(
                path, WP0002_LOCAL_OPERATOR_TRANSACTION_EVIDENCE_SCHEMA
            )
        )
        try:
            report = load_json(path)
        except (OSError, ValueError, json.JSONDecodeError) as exc:
            errors.append(
                f"WP-0002 local operator {phase} online evidence cannot be parsed: {exc}"
            )
            continue
        if report.get("phase") != phase:
            errors.append(
                f"WP-0002 local operator {phase} online evidence names another phase"
            )
            continue
        reports[phase] = report
        # The online verifier binds evidence objects in the explicit canonical
        # JSON domain.  Repository formatting is intentionally irrelevant.
        report_hashes[phase] = sha256_canonical_json(report)

    if set(reports) != {"authority", "pre-merge", "complete"}:
        return errors
    authority = reports["authority"]
    pre_merge = reports["pre-merge"]
    complete = reports["complete"]

    verifier, verifier_errors = _load_wp0002_transaction_verifier()
    errors.extend(verifier_errors)
    if verifier is not None:
        for phase, function_name in (
            ("authority", "validate_authority_evidence"),
            ("pre-merge", "validate_pre_merge_evidence"),
            ("complete", "validate_complete_evidence"),
        ):
            validator = verifier.get(function_name)
            if not callable(validator):
                errors.append(
                    f"WP-0002 protected transaction verifier lacks {function_name}"
                )
                continue
            try:
                validator(reports[phase])
            except Exception as exc:
                errors.append(
                    f"WP-0002 local operator {phase} pure verifier rejected report: {exc}"
                )

    repositories = [
        authority.get("repository"),
        pre_merge.get("repository"),
        complete.get("repository"),
    ]
    if not all(isinstance(item, dict) for item in repositories) or not all(
        item == repositories[0] for item in repositories[1:]
    ):
        errors.append("WP-0002 local operator reports do not bind one repository")
    repository = repositories[0] if isinstance(repositories[0], dict) else {}
    owner_id = repository.get("owner_id")

    authority_comment = authority.get("authorization_comment", {})
    pre_merge_comment = pre_merge.get("authorization_comment", {})
    parsed = authority.get("parsed_binding", {})
    if authority_comment != pre_merge_comment:
        errors.append(
            "WP-0002 local operator authorization comment drifted before merge"
        )
    actor = (
        authority_comment.get("actor", {})
        if isinstance(authority_comment, dict)
        else {}
    )
    if actor.get("id") != owner_id:
        errors.append(
            "WP-0002 local operator authorization actor is not the repository owner"
        )
    source_reference = amendment_receipt.get("source_reference")
    if (
        not isinstance(authority_comment, dict)
        or authority_comment.get("html_url") != source_reference
        or authority_comment.get("body_utf8_sha256")
        != amendment_receipt.get("approval_text_sha256")
    ):
        errors.append(
            "WP-0002 local operator receipt is not bound to the authenticated comment capture"
        )
    if (
        not isinstance(parsed, dict)
        or authority_comment.get("body_utf8_sha256")
        != sha256_canonical_json(parsed)
        or authority_comment.get("created_at")
        != authority_comment.get("updated_at")
    ):
        errors.append(
            "WP-0002 authority comment projection is not exact unedited canonical JSON"
        )

    if pre_merge.get("authority_evidence_sha256") != report_hashes["authority"]:
        errors.append(
            "WP-0002 pre-merge evidence does not bind exact authority report bytes"
        )
    if complete.get("pre_merge_evidence_sha256") != report_hashes["pre-merge"]:
        errors.append(
            "WP-0002 completion evidence does not bind exact pre-merge report bytes"
        )

    stage1 = authority.get("stage1", {})
    pull = authority.get("pull_request", {})
    if (
        not isinstance(stage1, dict)
        or not isinstance(pull, dict)
        or not isinstance(parsed, dict)
        or stage1.get("base_sha") != pull.get("base_sha")
        or stage1.get("commit_sha") != pull.get("head_sha")
        or stage1.get("parent_shas") != [pull.get("base_sha")]
        or parsed.get("stage1_commit") != stage1.get("commit_sha")
        or parsed.get("stage1_tree") != stage1.get("tree_oid")
        or parsed.get("stage1_patch_sha256")
        != stage1.get("deterministic_patch_sha256")
        or parsed.get("changed_files_manifest_sha256")
        != stage1.get("changed_files_manifest_sha256")
    ):
        errors.append("WP-0002 authority report does not bind exact Stage-1 Git facts")
    if parsed.get("claim") != WP0002_LOCAL_OPERATOR_CLAIM:
        errors.append("WP-0002 authority report binds another creator claim")
    if amendment_receipt.get("accepted_commit") != stage1.get("commit_sha"):
        errors.append(
            "WP-0002 local operator receipt does not accept the exact Stage-1 commit"
        )
    if isinstance(manifest, dict):
        scope_reference = manifest.get("local_operator_amendment_scope_capture")
        if (
            not isinstance(scope_reference, dict)
            or scope_reference.get("base_commit") != stage1.get("base_sha")
            or scope_reference.get("head_commit") != stage1.get("base_sha")
            or scope_reference.get("checkpoint_commit") != stage1.get("base_sha")
        ):
            errors.append(
                "WP-0002 transaction Stage-1 base is not the captured protected main"
            )

    final_pull = pre_merge.get("final_pull_request", {})
    receipt_materialization = pre_merge.get("receipt_materialization", {})
    expected_receipt_path = (
        "docs/foundation-v0.1/ledger/receipts/"
        f"{WP0002_LOCAL_OPERATOR_RECEIPT_ID}.json"
    )
    delta = (
        receipt_materialization.get("delta", [])
        if isinstance(receipt_materialization, dict)
        else []
    )
    expected_delta = {
        "path": expected_receipt_path,
        "status": "A",
        "old_mode": "000000",
        "new_mode": "100644",
        "old_oid": "0" * 40,
    }
    if (
        not isinstance(final_pull, dict)
        or not isinstance(receipt_materialization, dict)
        or final_pull.get("number") != pull.get("number")
        or final_pull.get("base_sha") != pull.get("base_sha")
        or final_pull.get("head_repository") != pull.get("head_repository")
        or receipt_materialization.get("parent_sha") != stage1.get("commit_sha")
        or receipt_materialization.get("commit_sha") != final_pull.get("head_sha")
        or receipt_materialization.get("receipt_path") != expected_receipt_path
        or len(delta) != 1
        or not isinstance(delta[0], dict)
        or any(delta[0].get(key) != value for key, value in expected_delta.items())
    ):
        errors.append(
            "WP-0002 final head is not the exact one-file receipt materialization"
        )
    receipt_path = ROOT / "ledger" / "receipts" / (
        f"{WP0002_LOCAL_OPERATOR_RECEIPT_ID}.json"
    )
    if receipt_path.is_file():
        receipt_hash = sha256_file(receipt_path)
        if (
            receipt_materialization.get("receipt_sha256") != receipt_hash
            or not isinstance(delta, list)
            or len(delta) != 1
            or not isinstance(delta[0], dict)
            or delta[0].get("new_blob_sha256") != receipt_hash
        ):
            errors.append(
                "WP-0002 pre-merge evidence does not bind exact receipt bytes"
            )
    else:
        errors.append("WP-0002 local operator receipt file is missing")

    changed_files = stage1.get("changed_files", [])
    expected_artifacts: dict[str, str] = {}
    duplicate_paths = False
    if isinstance(changed_files, list):
        for item in changed_files:
            if not isinstance(item, dict) or not isinstance(item.get("path"), str):
                duplicate_paths = True
                continue
            path = item["path"]
            if path in expected_artifacts:
                duplicate_paths = True
            expected_artifacts[path] = item.get("new_blob_sha256")
            current_path, path_error = safe_repo_path(
                path, "WP-0002 Stage-1 changed file"
            )
            if path_error is not None:
                errors.append(path_error)
                continue
            try:
                metadata = current_path.lstat() if current_path is not None else None
            except OSError as exc:
                errors.append(
                    f"WP-0002 Stage-1 changed file cannot be inspected: {path}: {exc}"
                )
                continue
            if (
                current_path is None
                or metadata is None
                or not stat.S_ISREG(metadata.st_mode)
                or stat.S_ISLNK(metadata.st_mode)
            ):
                errors.append(
                    f"WP-0002 Stage-1 changed file is not a regular non-symlink file: {path}"
                )
                continue
            if sha256_file(current_path) != item.get("new_blob_sha256"):
                errors.append(
                    f"WP-0002 Stage-1 changed file differs from authenticated bytes: {path}"
                )
    else:
        duplicate_paths = True
    required_stage1_paths = {
        "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json",
        f"docs/foundation-v0.1/{WP0002_LOCAL_OPERATOR_GOVERNANCE_PATH}",
        "docs/foundation-v0.1/work-packets/proposed/WP-0002.json",
    }
    if not required_stage1_paths.issubset(expected_artifacts):
        errors.append(
            "WP-0002 Stage-1 evidence omits a required amendment control file"
        )
    external_source_key = (
        source_reference.removeprefix("https://")
        if isinstance(source_reference, str)
        else ""
    )
    if external_source_key in expected_artifacts:
        duplicate_paths = True
    expected_artifacts[external_source_key] = amendment_receipt.get(
        "approval_text_sha256"
    )
    if duplicate_paths or amendment_receipt.get("artifact_sha256") != expected_artifacts:
        errors.append(
            "WP-0002 local operator receipt artifact keys are not the exact Stage-1 set plus authority comment"
        )

    check_runs = pre_merge.get("required_check_runs", [])
    expected_check_names = {"validate", "wp0002-core"}
    if (
        not isinstance(check_runs, list)
        or {item.get("name") for item in check_runs if isinstance(item, dict)}
        != expected_check_names
        or any(
            not isinstance(item, dict)
            or item.get("head_sha") != final_pull.get("head_sha")
            or item.get("app_id") != 15368
            or item.get("status") != "completed"
            or item.get("conclusion") != "success"
            for item in check_runs
        )
    ):
        errors.append("WP-0002 retained checks are not exact successful final-head runs")

    before = pre_merge.get("protection_before", {})
    during = pre_merge.get("protection_during", {})
    after = complete.get("protection_after", {})
    required_before_after = {
        ("validate", 15368),
        ("wp0002-core", 15368),
        ("wp0002-policy", 15368),
    }
    required_during = {("validate", 15368), ("wp0002-core", 15368)}

    def protection_checks(value: object) -> set[tuple[object, object]]:
        if not isinstance(value, dict):
            return set()
        checks = value.get("required_checks", [])
        return {
            (item.get("context"), item.get("app_id"))
            for item in checks
            if isinstance(item, dict)
        }

    if (
        protection_checks(before) != required_before_after
        or protection_checks(during) != required_during
        or protection_checks(after) != required_before_after
    ):
        errors.append("WP-0002 protection phases do not bind the exact check sets")
    ignored_protection_fields = {
        "observed_at",
        "main_sha",
        "required_checks",
        "raw_response_sha256",
        "raw_sha256",
    }

    def stable_protection(value: object) -> dict:
        if not isinstance(value, dict):
            return {}
        return {
            key: item
            for key, item in value.items()
            if key not in ignored_protection_fields
        }

    if not (
        stable_protection(before)
        == stable_protection(during)
        == stable_protection(after)
    ):
        errors.append(
            "WP-0002 protection phases weaken a non-temporary protection"
        )

    merged_pull = complete.get("merged_pull_request", {})
    merge = complete.get("merge", {})
    completion_comment = complete.get("completion_comment", {})
    completion_binding = complete.get("parsed_completion_binding", {})
    completion_actor = (
        completion_comment.get("actor", {})
        if isinstance(completion_comment, dict)
        else {}
    )
    expected_completion_binding = {
        "claim": "COMPLETE-WP0002-LOCAL-OPERATOR-CONTROL-TRANSACTION",
        "transaction_id": "WP0002-LOCAL-OPERATOR-20260717",
        "authority_evidence_sha256": pre_merge.get(
            "authority_evidence_sha256"
        ),
        "pre_merge_evidence_sha256": report_hashes["pre-merge"],
        "pr_number": final_pull.get("number"),
        "base_sha": final_pull.get("base_sha"),
        "head_sha": final_pull.get("head_sha"),
        "final_patch_sha256": final_pull.get("deterministic_patch_sha256"),
        "merge_commit_sha": merged_pull.get("merge_commit_sha"),
        "merge_tree_oid": merge.get("tree_oid"),
        "protection_before_raw_sha256": before.get("raw_sha256"),
        "protection_during_raw_sha256": during.get("raw_sha256"),
        "protection_after_raw_sha256": after.get("raw_sha256"),
    }
    if (
        not isinstance(merged_pull, dict)
        or not isinstance(merge, dict)
        or merged_pull.get("number") != final_pull.get("number")
        or merged_pull.get("base_sha") != final_pull.get("base_sha")
        or merged_pull.get("head_sha") != final_pull.get("head_sha")
        or merge.get("sole_parent_sha") != final_pull.get("base_sha")
        or merge.get("final_head_tree_oid")
        != receipt_materialization.get("tree_oid")
        or merge.get("tree_oid") != merge.get("final_head_tree_oid")
        or after.get("main_sha") != merged_pull.get("merge_commit_sha")
        or completion_actor.get("id") != owner_id
        or completion_binding != expected_completion_binding
        or completion_comment.get("body_utf8_sha256")
        != sha256_canonical_json(completion_binding)
        or completion_comment.get("created_at")
        != completion_comment.get("updated_at")
    ):
        errors.append("WP-0002 completion report does not bind the exact squash result")
    completion_time = parse_datetime(completion_comment.get("created_at"))
    after_time = parse_datetime(after.get("observed_at"))
    merged_time = parse_datetime(merged_pull.get("merged_at"))
    if (
        completion_time is None
        or after_time is None
        or completion_time < after_time
    ):
        errors.append(
            "WP-0002 completion comment predates restored protection evidence"
        )
    if (
        merged_time is None
        or after_time is None
        or after_time < merged_time
        or (
            after_time - merged_time
        ).total_seconds() > WP0002_LOCAL_OPERATOR_MAX_RESTORE_DELAY_SECONDS
    ):
        errors.append(
            "WP-0002 policy restoration was not captured within 600 seconds"
        )
    return errors


def validate_wp0002_local_operator_amendment(
    manifest: dict,
    packet: dict,
    receipts_by_id: dict[str, dict],
    _boundary_sha256: str,
) -> list[str]:
    """Validate offline structure only; live GitHub authority is a separate gate."""
    errors: list[str] = []
    if manifest.get("boundary_amendments") != WP0002_BOUNDARY_AMENDMENTS:
        errors.append("WP-0002 local operator boundary amendment chain is not exact")
    if (
        manifest.get("delegated_local_unity_operator")
        != WP0002_DELEGATED_LOCAL_UNITY_OPERATOR
    ):
        errors.append("WP-0002 delegated local Unity operator boundary is not exact")

    permission_boundary = manifest.get("permission_boundary", {})
    if permission_boundary.get("allowed_actions") != WP0002_ALLOWED_ACTIONS:
        errors.append("WP-0002 allowed actions differ from the amended exact set")
    if permission_boundary.get("denied_actions") != WP0002_DENIED_ACTIONS:
        errors.append("WP-0002 denied actions differ from the retained exact set")

    repository = manifest.get("repository", {})
    unity = manifest.get("unity", {})
    if (
        repository.get("root") != WP0002_CANONICAL_REPOSITORY_ROOT
        or repository.get("game_project_path") != WP0002_CANONICAL_PROJECT_PATH
        or unity.get("project_path") != WP0002_CANONICAL_PROJECT_PATH
    ):
        errors.append("WP-0002 local operator amendment does not bind canonical paths")
    if (
        unity.get("successor_first_use_preconditions")
        != WP0002_SUCCESSOR_FIRST_USE_PRECONDITIONS
    ):
        errors.append(
            "WP-0002 successor first-use gate does not bind delegated exact-project opening"
        )
    if (
        manifest.get("local_operator_transaction_evidence_contract")
        != WP0002_LOCAL_OPERATOR_TRANSACTION_EVIDENCE_CONTRACT
    ):
        errors.append(
            "WP-0002 local operator online transaction evidence contract is not exact"
        )

    amendment_receipt = receipts_by_id.get(WP0002_LOCAL_OPERATOR_RECEIPT_ID)
    if not isinstance(amendment_receipt, dict):
        return errors + [
            "WP-0002 local operator amendment lacks its creator-authorization receipt"
        ]

    resolver = amendment_receipt.get("artifact_resolver")
    claims = subject_claims(amendment_receipt).get("WP-0002", set())
    source_reference = amendment_receipt.get("source_reference")
    signature_reference = amendment_receipt.get("signature_reference")
    if (
        amendment_receipt.get("receipt_kind") != "creator-authorization"
        or amendment_receipt.get("issued_by") != "AC-21"
        or amendment_receipt.get("issuer_role") != "creator"
        or amendment_receipt.get("sealed") is not True
        or not isinstance(resolver, dict)
        or resolver.get("type") != "external-protected"
        or not isinstance(resolver.get("resolver_reference"), str)
        or not resolver.get("resolver_reference")
        or not isinstance(source_reference, str)
        or WP0002_LOCAL_OPERATOR_SOURCE_PATTERN.fullmatch(source_reference) is None
        or source_reference != resolver.get("resolver_reference")
        or signature_reference != resolver.get("resolver_reference")
        or set(amendment_receipt.get("subject_ids", [])) != {"WP-0002"}
        or claims != {WP0002_LOCAL_OPERATOR_CLAIM}
    ):
        errors.append(
            "WP-0002 local operator amendment lacks exact protected creator authority"
        )
    if amendment_receipt.get("subject_contract_sha256") != {
        "WP-0002": packet.get("contract_sha256")
    }:
        errors.append("WP-0002 local operator receipt changes or misbinds the contract")
    if amendment_receipt.get("subject_event_sha256") != {}:
        errors.append("WP-0002 local operator receipt invents a decision event binding")
    errors.extend(
        validate_wp0002_local_operator_amendment_scope_capture(
            manifest,
            packet,
            amendment_receipt,
        )
    )
    errors.extend(
        validate_wp0002_local_operator_transaction_evidence(
            amendment_receipt,
            manifest,
        )
    )

    activation_receipt = receipts_by_id.get(manifest.get("attestation_receipt_id"))
    if (
        not isinstance(activation_receipt, dict)
        or activation_receipt.get("artifact_sha256", {}).get(
            "governance/a1-boundaries/WP-0002.json"
        )
        != WP0002_PREVIOUS_BOUNDARY_SHA256
    ):
        errors.append(
            "WP-0002 local operator amendment does not retain the activated prior boundary hash"
        )
    return errors


def validate_local_a1_boundary_manifest(
    packet: dict,
    state: dict,
    activation_receipt: dict | None,
    receipts_by_id: dict[str, dict],
) -> tuple[dict | None, list[str]]:
    """Validate a compact creator-attested protected-PR local-development boundary."""
    errors: list[str] = []
    packet_id = packet.get("id", "unknown-packet")
    if packet_id not in {"WP-0002", "WP-0003"}:
        return None, [f"{packet_id} is not eligible for the local A1 boundary"]

    reference = packet.get("a1_boundary_manifest")
    if not isinstance(reference, dict):
        return None, [f"{packet_id} lacks a canonical local A1 boundary reference"]
    expected_path = f"governance/a1-boundaries/{packet_id}.json"
    if reference.get("path") != expected_path:
        errors.append(f"{packet_id} local boundary must use {expected_path}")
    manifest_path, path_error = safe_foundation_path(
        reference.get("path"), f"{packet_id} local boundary path"
    )
    if path_error:
        return None, [path_error]
    if manifest_path is None or not manifest_path.is_file():
        return None, errors + [f"{packet_id} local boundary manifest is missing"]

    actual_hash = sha256_file(manifest_path)
    if reference.get("sha256") != actual_hash:
        errors.append(f"{packet_id} local boundary raw hash mismatch")
    errors.extend(validate_instance_shape(manifest_path, LOCAL_A1_BOUNDARY_SCHEMA))
    manifest = load_json(manifest_path)

    if manifest.get("manifest_id") != reference.get("manifest_id"):
        errors.append(f"{packet_id} local boundary ID differs from packet reference")
    if manifest.get("packet_id") != packet_id:
        errors.append(f"{packet_id} local boundary binds another packet")
    if manifest.get("packet_contract_sha256") != packet.get("contract_sha256"):
        errors.append(f"{packet_id} local boundary binds the wrong packet contract")
    if packet_id == "WP-0002":
        expected_tools = [
            "Unity_ReadConsole",
            "Unity_RunCommand",
            "Unity_ManageEditor",
            "Unity_ManageGameObject",
            "Unity_Camera_Capture",
        ]
        expected_local_package_links = {
            "com.ac21.sasha.simulation-core": "file:../../SimulationCore",
            "com.ac21.sasha.save-contracts": "file:../../SaveContracts",
        }
        expected_local_save_boundary = {
            "profile_id": "last-bearing-dev-v1",
            "root_source": "UnityEngine.Application.persistentDataPath",
            "fixed_child": "last-bearing-dev-v1",
            "fixed_child_only": True,
            "write_authority": "unity-runtime-only-via-SaveContracts",
            "direct_agent_or_host_filesystem_write": False,
            "sibling_scan": False,
            "presentation_arbitrary_root_constructor": False,
            "load_visible_pointers": ["current", "last-good"],
        }
        expected_drift = {
            ".codex/config.toml": "unstaged-modified",
            "Game/ProjectSettings/ProjectSettings.asset": "unstaged-modified",
            "Game/ProjectSettings/SceneTemplateSettings.json": "untracked",
        }
        if manifest.get("lifecycle_state") != "attested":
            errors.append(f"{packet_id} local boundary remains proposed")
        if manifest.get("allowed_mcp_tools") != expected_tools:
            errors.append(f"{packet_id} local boundary must bind the exact five-tool Unity MCP route")
        if manifest.get("local_package_links") != expected_local_package_links:
            errors.append(f"{packet_id} local boundary must bind the exact two repository-local UPM links")
        if manifest.get("local_save_boundary") != expected_local_save_boundary:
            errors.append(f"{packet_id} local boundary must bind the exact fixed-child runtime save policy")
        if (
            manifest.get("unity_runcommand_residual_capability")
            != WP0002_RUNCOMMAND_RESIDUAL_CAPABILITY
        ):
            errors.append(
                f"{packet_id} local boundary must acknowledge exact Unity_RunCommand residual capability"
            )
        git_safety = manifest.get("git_safety", {})
        if (
            git_safety.get("auto_merge_required") is not False
            or git_safety.get("autonomy_classification")
            != "creator-delegated-manual-per-pr"
            or git_safety.get("delegated_release_required_per_pr") is not True
        ):
            errors.append(
                f"{packet_id} local boundary must remain creator-delegated per PR until canary proof"
            )
        canary = git_safety.get("policy_canary_state")
        expected_checks = (
            ["validate", "wp0002-core", "wp0002-policy"]
            if canary == "proven-candidate-independent"
            else ["validate", "wp0002-core"]
        )
        if git_safety.get("required_checks") != expected_checks:
            errors.append(f"{packet_id} required checks do not match policy canary state")
        expected_manual_authority = {
            "authority_source": "authenticated-creator-delegation",
            "transmission_agent": "Codex",
            "attribution": "creator-delegated-manual-approval-transmitted-by-Codex",
            "cursor_absence_does_not_block": True,
            "deterministic_checks_nonwaivable": ["validate", "wp0002-core"],
            "external_protected_receipt_required": True,
            "direct_self-merge_authorized": False,
            "repo_admin_capability_acknowledged": True,
            "branch_protection_is_credential_isolation": False,
            "protection_bypass_constraint": (
                "denied-creator-policy-with-live-audit-and-canary"
            ),
        }
        if manifest.get("creator_delegated_manual_authority") != expected_manual_authority:
            errors.append(f"{packet_id} creator-delegated manual authority is not exact")
        if manifest.get("repository", {}).get("clean_at_activation") is not False:
            errors.append(f"{packet_id} local boundary must honestly retain creator-owned drift")
        for field in (
            "index_clean_at_activation",
            "non_excluded_scope_clean_at_activation",
            "reserved_scope_clean_at_activation",
        ):
            if manifest.get(field) is not True:
                errors.append(f"{packet_id} local boundary {field} must be true")
        drift_items = manifest.get("excluded_creator_owned_drift", [])
        observed_drift = {
            item.get("path"): item.get("normalized_git_state")
            for item in drift_items
            if isinstance(item, dict)
        }
        if len(drift_items) != 3 or observed_drift != expected_drift:
            errors.append(f"{packet_id} local boundary creator-owned drift set is not exact")
        if not artifact_is_content_addressed(manifest.get("working_tree_scope_capture")):
            errors.append(f"{packet_id} local boundary lacks a content-addressed working-tree scope capture")
        protected_paths = manifest.get("permission_boundary", {}).get(
            "protected_paths_read_only", []
        )
        if "docs/foundation-v0.1/" not in protected_paths:
            errors.append(f"{packet_id} local boundary must keep foundation/governance/receipts read-only")
        for drift_path in expected_drift:
            if drift_path not in protected_paths:
                errors.append(f"{packet_id} local boundary must protect excluded creator path {drift_path}")

    reservation = packet.get("reservation", {})
    repository = manifest.get("repository", {})
    branch = repository.get("branch")
    if (
        not isinstance(branch, str)
        or not branch.startswith("agent/")
        or not git_branch_name_is_valid(branch)
    ):
        errors.append(
            f"{packet_id} local boundary branch must be a valid agent/* Git branch"
        )
    base_commit = repository.get("base_commit")
    if not isinstance(base_commit, str) or not git_commit_exists(base_commit):
        errors.append(f"{packet_id} local boundary base commit does not exist")
    elif not git_commit_is_ancestor_of_protected_main(base_commit):
        errors.append(
            f"{packet_id} local boundary base commit is not protected-main ancestry"
        )
    if repository.get("base_commit") != reservation.get("base_commit"):
        errors.append(f"{packet_id} local boundary base commit differs from reservation")
    expected_reservation = {
        "lease_id": reservation.get("lease_id"),
        "fencing_token": reservation.get("fencing_token"),
        "expires_at": reservation.get("expires_at"),
        "paths": reservation.get("paths"),
        "domains": reservation.get("domains"),
    }
    if manifest.get("reservation") != expected_reservation:
        errors.append(f"{packet_id} local boundary does not exactly bind its reservation")
    if packet_id == "WP-0002":
        protected_paths = manifest.get("permission_boundary", {}).get(
            "protected_paths_read_only", []
        )
        for reserved_path in reservation.get("paths", []):
            for protected_path in protected_paths:
                if repo_paths_overlap(reserved_path, protected_path):
                    errors.append(
                        f"{packet_id} local boundary reservation overlaps protected path: "
                        f"{reserved_path} vs {protected_path}"
                    )
    if manifest.get("git_safety", {}).get("checkpoint_commit") != reservation.get(
        "base_commit"
    ):
        errors.append(f"{packet_id} local boundary checkpoint differs from reserved base")

    current_foundation = {
        "constitution_sha256": state.get("constitution_sha256"),
        "decision_ledger_sha256": state.get("decision_ledger_sha256"),
        "last_creator_receipt_id": state.get("last_creator_receipt_id"),
    }
    current_authority_status = packet.get("status") in {
        "active",
        "verifying",
        "candidate",
    }
    historical_status = not current_authority_status
    authority_foundation = (
        manifest.get("foundation_binding") if historical_status else current_foundation
    )
    if not historical_status and manifest.get("foundation_binding") != current_foundation:
        errors.append(f"{packet_id} local boundary does not bind current foundation authority")

    required_claims = {
        "A1-LOCAL-BOUNDARY-VERIFIED",
        f"ACTIVATE-A1-{packet_id}",
    }
    if not isinstance(activation_receipt, dict):
        errors.append(f"{packet_id} local boundary has no activation receipt")
    else:
        resolver = activation_receipt.get("artifact_resolver")
        claims = subject_claims(activation_receipt).get(packet_id, set())
        if (
            activation_receipt.get("receipt_kind") != "packet-activation"
            or activation_receipt.get("issuer_role") != "creator"
            or activation_receipt.get("sealed") is not True
            or not isinstance(resolver, dict)
            or resolver.get("type") != "external-protected"
            or not resolver.get("resolver_reference")
            or not isinstance(activation_receipt.get("signature_reference"), str)
            or not activation_receipt.get("signature_reference")
            or set(activation_receipt.get("subject_ids", [])) != {packet_id}
            or not required_claims.issubset(claims)
        ):
            errors.append(
                f"{packet_id} local activation receipt lacks exact protected authority"
            )
        if manifest.get("attestation_receipt_id") != activation_receipt.get(
            "receipt_id"
        ):
            errors.append(f"{packet_id} local boundary names another receipt")
        if manifest.get("attested_by") != activation_receipt.get("issued_by"):
            errors.append(f"{packet_id} local boundary attestor differs from receipt issuer")
        activation_boundary_hash = (
            WP0002_PREVIOUS_BOUNDARY_SHA256
            if packet_id == "WP-0002"
            and manifest.get("boundary_amendments") == WP0002_BOUNDARY_AMENDMENTS
            else actual_hash
        )
        if (
            activation_receipt.get("artifact_sha256", {}).get(expected_path)
            != activation_boundary_hash
        ):
            errors.append(
                f"{packet_id} activation receipt does not bind local boundary bytes"
            )
        if activation_receipt.get("foundation_binding") != authority_foundation:
            qualifier = "retained historical" if historical_status else "current"
            errors.append(
                f"{packet_id} local activation receipt lacks {qualifier} foundation binding"
            )
        if (
            activation_receipt.get("subject_contract_sha256", {}).get(packet_id)
            != packet.get("contract_sha256")
        ):
            errors.append(
                f"{packet_id} local activation receipt binds the wrong contract"
            )
        accepted_commit = activation_receipt.get("accepted_commit")
        if repository.get("branch_head_commit") != accepted_commit:
            errors.append(
                f"{packet_id} local boundary branch head differs from activation commit"
            )
        if (
            not isinstance(accepted_commit, str)
            or not git_commit_exists(accepted_commit)
        ):
            errors.append(
                f"{packet_id} local activation receipt commit does not exist"
            )
        elif isinstance(base_commit, str) and not git_commit_is_ancestor(
            base_commit,
            accepted_commit,
        ):
            errors.append(
                f"{packet_id} local activation receipt commit does not descend from its base"
            )
        if (
            isinstance(authority_foundation, dict)
            and authority_foundation.get("last_creator_receipt_id")
            == activation_receipt.get("receipt_id")
        ):
            errors.append(
                f"{packet_id} local activation receipt cannot self-bind as prior creator receipt"
            )

    historical_last_receipt_id = (
        authority_foundation.get("last_creator_receipt_id")
        if isinstance(authority_foundation, dict)
        else None
    )
    last_creator_receipt = receipts_by_id.get(historical_last_receipt_id)
    if (
        not last_creator_receipt
        or not last_creator_receipt.get("sealed")
        or last_creator_receipt.get("issuer_role") != "creator"
    ):
        errors.append(
            f"{packet_id} foundation binding does not name a sealed prior creator receipt"
        )
    if packet_id == "WP-0002" and manifest.get("lifecycle_state") == "attested":
        errors.extend(
            validate_wp0002_local_operator_amendment(
                manifest,
                packet,
                receipts_by_id,
                actual_hash,
            )
        )
        errors.extend(
            validate_wp0002_working_tree_scope_capture(
                manifest,
                packet,
                activation_receipt,
            )
        )
        errors.extend(
            validate_wp0002_external_policy_and_github_capture(
                manifest,
                packet,
                activation_receipt,
            )
        )
    return manifest, errors


def validate_a1_boundary_manifest(
    packet: dict,
    state: dict,
    activation_receipt: dict | None,
    receipts_by_id: dict[str, dict],
) -> tuple[dict | None, list[str]]:
    errors: list[str] = []
    packet_id = packet.get("id", "unknown-packet")
    if packet_id in {"WP-0002", "WP-0003"}:
        return validate_local_a1_boundary_manifest(
            packet,
            state,
            activation_receipt,
            receipts_by_id,
        )
    reference = packet.get("a1_boundary_manifest")
    if not isinstance(reference, dict):
        return None, [f"{packet_id} lacks a canonical A1 boundary-manifest reference"]
    expected_path = f"governance/a1-boundaries/{packet_id}.json"
    if reference.get("path") != expected_path:
        errors.append(f"{packet_id} boundary manifest must use {expected_path}")
    manifest_path, path_error = safe_foundation_path(
        reference.get("path"), f"{packet_id} boundary manifest path"
    )
    if path_error:
        return None, [path_error]
    if manifest_path is None or not manifest_path.is_file():
        return None, errors + [f"{packet_id} boundary manifest is missing"]
    actual_hash = sha256_file(manifest_path)
    if reference.get("sha256") != actual_hash:
        errors.append(f"{packet_id} boundary manifest raw hash mismatch")
    errors.extend(validate_instance_shape(manifest_path, A1_BOUNDARY_SCHEMA))
    manifest = load_json(manifest_path)
    if manifest.get("manifest_id") != reference.get("manifest_id"):
        errors.append(f"{packet_id} boundary manifest ID differs from packet reference")
    if manifest.get("packet_id") != packet_id:
        errors.append(f"{packet_id} boundary manifest binds another packet")
    if manifest.get("packet_contract_sha256") != packet.get("contract_sha256"):
        errors.append(f"{packet_id} boundary manifest binds the wrong packet contract")
    wp0001_evidence_refs: list[dict] = []
    wp0001_live_capture_times: list[datetime] = []

    reservation = packet.get("reservation", {})
    if manifest.get("repository", {}).get("base_commit") != reservation.get("base_commit"):
        errors.append(f"{packet_id} boundary manifest base commit differs from reservation")
    expected_reservation = {
        "lease_id": reservation.get("lease_id"),
        "fencing_token": reservation.get("fencing_token"),
        "expires_at": reservation.get("expires_at"),
        "paths": reservation.get("paths"),
        "domains": reservation.get("domains"),
    }
    if manifest.get("reservation") != expected_reservation:
        errors.append(f"{packet_id} boundary manifest does not exactly bind its reservation")

    expected_foundation = {
        "constitution_path": "00-GAME-CONSTITUTION.md",
        "constitution_sha256": state.get("constitution_sha256"),
        "decision_ledger_path": "ledger/decisions.jsonl",
        "decision_ledger_sha256": state.get("decision_ledger_sha256"),
        "last_creator_receipt_id": state.get("last_creator_receipt_id"),
    }
    if manifest.get("foundation_binding") != expected_foundation:
        errors.append(f"{packet_id} boundary manifest does not bind current foundation authority")

    protection = manifest.get("protection_boundary", {})
    if set(protection.get("writable_paths", [])) != set(reservation.get("paths", [])):
        errors.append(f"{packet_id} boundary writable paths differ from its exact reservation")
    scratch_code_messages = {
        "scratch-boundary-invalid": "scratch protection record is invalid",
        "scratch-path-list-invalid": "scratch paths must be an array",
        "wp0001-scratch-set-mismatch": (
            "Unity scratch paths differ from the exact WP-0001 non-output set"
        ),
        "scratch-not-destroyed-on-close": "scratch is not marked for destruction on close",
        "scratch-path-unsafe": "scratch contains an unsafe repository-relative path",
        "scratch-not-gitignored": "scratch contains a path that is not repository-ignored",
        "scratch-reservation-overlap": "scratch overlaps reserved packet output",
        "scratch-protected-overlap": "scratch overlaps a protected path",
        "scratch-internal-overlap": "scratch roots overlap each other",
    }
    for code in a1_scratch_boundary_codes(
        packet_id, reservation.get("paths", []), protection
    ):
        errors.append(
            f"{packet_id} boundary {scratch_code_messages.get(code, code)}"
        )
    runtime_code_messages = {
        "runtime-environment-invalid": "approved runtime environment is invalid",
        "runtime-policy-hash-invalid": "lacks exact sandbox/network policy hashes",
        "runtime-boundary-invalid": "runtime protection record is invalid",
        "runtime-isolation-mode-invalid": "lacks a disposable OS-user or sandbox mode",
        "runtime-principal-invalid": "lacks an exact runtime principal UID",
        "runtime-boot-session-invalid": "lacks the exact boot-session identity",
        "runtime-root-unsafe": "runtime HOME or private temp root is unsafe",
        "runtime-roots-overlap": "runtime HOME and private temp roots overlap",
        "runtime-environment-binding-mismatch": "HOME/TMP variables differ from runtime roots",
        "ambient-root-invalid": "ambient host HOME or shared temp roots are invalid",
        "ambient-write-denial-mismatch": "ambient write-denial roots are not exact",
        "ambient-write-denial-disabled": "ambient host HOME/shared-temp writes are not denied",
        "ambient-write-exception-invalid": "shared-temp write exceptions are malformed or duplicated",
        "ambient-write-exception-outside-shared-temp": "a shared-temp write exception escapes the denied shared-temp roots",
        "runtime-home-overlaps-ambient": "ephemeral HOME overlaps ambient host state",
        "runtime-temp-overlaps-ambient": "private temp overlaps ambient shared temp",
        "runtime-root-symlink-escape": "runtime or ambient roots resolve through a symlink",
        "runtime-symlink-guard-disabled": "symlink escape is not forbidden and attested absent",
        "runtime-roots-importable": "runtime roots are importable packet output",
        "runtime-not-destroyed-on-close": "runtime roots are not destroy-on-close",
    }
    for code in a1_runtime_boundary_codes(
        manifest.get("approved_environment"),
        manifest.get("runtime_boundary"),
    ):
        errors.append(
            f"{packet_id} boundary {runtime_code_messages.get(code, code)}"
        )
    if packet_id == "WP-0001":
        repository = manifest.get("repository", {})
        project_seed = manifest.get("project_seed", {})
        route = manifest.get("unity_mcp_route", {})
        wp0001_source_refs: list[dict] = []
        base_commit = repository.get("base_commit")
        seed_tree = (
            git_repo_tree_listing(base_commit, "Game")
            if isinstance(base_commit, str)
            else None
        )
        observed_seed_tree_sha256 = (
            hashlib.sha256(seed_tree).hexdigest()
            if seed_tree is not None
            else None
        )
        route_code_messages = {
            "wp0001-repository-invalid": "repository record is invalid",
            "wp0001-runtime-invalid": "runtime record is invalid",
            "wp0001-project-seed-invalid": "project seed record is invalid",
            "wp0001-unity-mcp-route-invalid": "Unity MCP route record is invalid",
            "wp0001-repository-root-invalid": "lacks an exact absolute candidate root",
            "wp0001-repository-isolation-invalid": "does not prove detached independent Git state",
            "wp0001-project-seed-mode-invalid": "project seed is not creator-created on the protected base",
            "wp0001-project-seed-root-invalid": "project seed root is not exactly Game",
            "wp0001-project-seed-base-mismatch": "project seed base differs from the candidate base",
            "wp0001-project-seed-attestation-missing": "project seed lacks creator no-implementation attestation",
            "wp0001-project-seed-evidence-invalid": "project seed evidence reference is invalid",
            "wp0001-project-seed-tree-mismatch": "project seed Git tree hash mismatches the protected base",
            "wp0001-toolchain-profile-invalid": "exact D-0047 toolchain profile is missing",
            "wp0001-toolchain-profile-mismatch": "toolchain profile differs from D-0047",
            "wp0001-toolchain-package-line-mismatch": "resolved URP or Test Framework version leaves the ratified line",
            "wp0001-approved-toolchain-mismatch": "approved toolchain records differ from the exact WP-0001 profile",
            "wp0001-activation-evidence-invalid": "activation evidence references are incomplete or invalid",
            "wp0001-policy-evidence-mismatch": "sandbox or network policy evidence hash differs from the approved environment",
            "wp0001-approved-environment-invalid": "approved environment record is invalid",
            "wp0001-raw-collector-authority-invalid": "raw protocol, network, and policy-attachment collectors are not exact, hash-bound, and creator-authorized",
            "wp0001-route-selection-mismatch": "route is not UNITY-MCP-EXTERNAL",
            "wp0001-code-identity-authority-invalid": "client, relay, and Editor signing identities lack explicit creator authority",
            "wp0001-mcp-project-target-mismatch": "Bridge target differs from the exact candidate Game path",
            "wp0001-mcp-relay-arguments-mismatch": "relay arguments do not exactly pin project path and Editor PID",
            "wp0001-mcp-process-parent-mismatch": "relay parent is not the exact Codex client",
            "wp0001-mcp-client-cwd-mismatch": "Codex working directory differs from the candidate root",
            "wp0001-mcp-process-identity-invalid": "client, relay, or Editor binary/signing/argv/cwd identity is invalid",
            "wp0001-mcp-principal-mismatch": "Unity, relay, and Codex do not share the isolated principal UID",
            "wp0001-mcp-environment-mismatch": "Codex MCP environment differs from the disposable runtime bindings",
            "wp0001-mcp-config-inventory-invalid": "effective Codex MCP server inventory is absent, drifted, or not session-bound",
            "wp0001-mcp-tool-scope-mismatch": "Codex allowlist and client-visible tool inventory differ or are empty",
            "wp0001-mcp-forbidden-tool": "Codex allowlist includes a categorically forbidden A1 tool",
            "wp0001-mcp-tool-scope-authority-invalid": "Codex allowlist lacks its exact digest or creator authority claim",
            "wp0001-mcp-relay-package-mismatch": "relay binary differs from the exact package copy",
            "wp0001-codex-policy-invalid": "Codex MCP policy is not exact, required, prompt-gated, and fail-closed",
            "wp0001-mcp-runtime-state-escape": "MCP mutable state escapes the disposable runtime HOME",
            "wp0001-mcp-process-evidence-mismatch": "process observation is absent or differs from activation evidence",
            "wp0001-mcp-endpoint-invalid": "Bridge endpoint is not the exact project-hash/PID Unix socket",
            "wp0001-mcp-socket-exception-invalid": "shared-temp policy does not permit exactly the required Bridge socket",
            "wp0001-mcp-discovery-evidence-invalid": "Bridge discovery evidence is absent or misaddressed",
            "wp0001-mcp-connection-state-invalid": "connection approval or revocation state is unsafe",
            "wp0001-mcp-handshake-not-clean": "handshake is incomplete, prompted, invoked a Unity tool, or stayed connected",
            "wp0001-mcp-handshake-time-invalid": "preflight handshake process birth or capture time is invalid",
            "wp0001-mcp-activation-session-invalid": "live activation session is absent, stale, disconnected, or not receipt-bound",
            "wp0001-mcp-activation-session-stale": "live activation session capture is stale or precedes process birth/preflight",
            "wp0001-mcp-stock-gap-unacknowledged": "stock pending/hidden-tool execution gaps are not acknowledged",
            "wp0001-mcp-nonloopback-reachable": "a listener is reachable outside loopback/quarantine",
            "wp0001-mcp-persistent-relay-not-suppressed": "suppression mode still observed a relay process or TCP listener",
            "wp0001-mcp-network-policy-unproven": "network-denial mode lacks successful loopback and failed non-loopback probes",
            "wp0001-mcp-mitigation-mode-invalid": "persistent relay has no accepted mitigation mode",
            "wp0001-unity-entitlement-linkage-invalid": "eligible assigned seat and same-organization project are not verified",
            "wp0001-project-identity-invalid": "temporary WP-0001 project identity is invalid",
        }
        for code in a1_wp0001_boundary_codes(
            repository,
            manifest.get("runtime_boundary"),
            project_seed,
            route,
            manifest.get("approved_toolchain"),
            manifest.get("approved_environment"),
            manifest.get("wp0001_toolchain_profile"),
            manifest.get("activation_evidence"),
            manifest.get("raw_capture_collectors"),
            observed_seed_tree_sha256=observed_seed_tree_sha256,
        ):
            errors.append(
                f"{packet_id} boundary {route_code_messages.get(code, code)}"
            )
        seed_policy_messages = {
            "wp0001-project-seed-tree-empty": "protected base contains no Game project seed",
            "wp0001-project-seed-tree-unparseable": "project seed Git tree cannot be parsed",
            "wp0001-project-seed-symlink": "project seed contains a symlink",
            "wp0001-project-seed-submodule": "project seed contains a submodule",
            "wp0001-project-seed-tracks-scratch": "project seed commits Unity scratch",
            "wp0001-project-seed-assets-not-empty": "project seed contains non-empty Assets content",
            "wp0001-project-seed-embedded-package": "project seed contains an embedded package tree",
            "wp0001-project-seed-unexpected-path": "project seed contains a path outside Packages/ProjectSettings",
            "wp0001-project-seed-required-file-missing": "project seed lacks a required package or ProjectSettings file",
            "wp0001-project-seed-project-version-invalid": "project seed Editor version or changeset differs from D-0047",
            "wp0001-project-seed-package-json-invalid": "project seed package manifest or lock is invalid JSON",
            "wp0001-project-seed-lock-policy-invalid": "project seed package lock policy is not enableLockFile=true and lowest",
            "wp0001-project-seed-package-source-invalid": "project seed contains a non-Unity-registry or unsafe package source",
            "wp0001-project-seed-package-lock-incomplete": "project seed package lock omits a direct dependency",
            "wp0001-project-seed-package-version-mismatch": "project seed package resolution differs from the exact profile",
            "wp0001-project-seed-identity-invalid": "project seed temporary identity is absent or invalid",
        }
        for code in wp0001_seed_tree_policy_codes(seed_tree):
            errors.append(
                f"{packet_id} boundary {seed_policy_messages.get(code, code)}"
            )

        seed_ref = (
            project_seed.get("evidence")
            if isinstance(project_seed, dict)
            else None
        )
        seed_blob = (
            git_repo_blob(base_commit, WP0001_PROJECT_SEED_EVIDENCE_PATH)
            if isinstance(base_commit, str)
            else None
        )
        if seed_blob is None:
            errors.append(
                f"{packet_id} protected base lacks committed project-seed evidence"
            )
        _, seed_ref_errors = validate_repo_evidence_reference(
            seed_ref,
            f"{packet_id} project seed",
            expected_path=WP0001_PROJECT_SEED_EVIDENCE_PATH,
            committed_blob=seed_blob,
        )
        errors.extend(seed_ref_errors)
        if isinstance(base_commit, str):
            seed_facts, seed_content_codes = wp0001_project_seed_content(
                base_commit,
                manifest.get("wp0001_toolchain_profile"),
            )
            seed_facts["game_tree_sha256"] = observed_seed_tree_sha256
            for code in seed_content_codes:
                errors.append(
                    f"{packet_id} boundary {seed_policy_messages.get(code, code)}"
                )
            if seed_blob is not None:
                seed_document, seed_document_errors = load_json_bytes(
                    seed_blob,
                    f"{packet_id} project seed evidence",
                )
                errors.extend(seed_document_errors)
                if seed_document is not None:
                    if isinstance(seed_document, dict) and isinstance(
                        seed_document.get("source_artifacts"), list
                    ):
                        wp0001_source_refs.extend(
                            source
                            for source in seed_document["source_artifacts"]
                            if isinstance(source, dict)
                        )
                    errors.extend(
                        validate_wp0001_evidence_record_data(
                            seed_document,
                            expected_kind="project-seed",
                            expected_facts=seed_facts,
                            label=f"{packet_id} project seed evidence",
                        )
                    )

        activation = (
            manifest.get("activation_evidence")
            if isinstance(manifest.get("activation_evidence"), dict)
            else {}
        )
        route_refs = [
            route.get("entitlement", {}).get("evidence")
            if isinstance(route.get("entitlement"), dict)
            else None,
            route.get("project_identity", {}).get("evidence")
            if isinstance(route.get("project_identity"), dict)
            else None,
            route.get("process_observation"),
            route.get("bridge", {}).get("discovery_record")
            if isinstance(route.get("bridge"), dict)
            else None,
            route.get("codex_policy", {}).get("evidence")
            if isinstance(route.get("codex_policy"), dict)
            else None,
            route.get("handshake", {}).get("transcript")
            if isinstance(route.get("handshake"), dict)
            else None,
            route.get("activation_session", {}).get("evidence")
            if isinstance(route.get("activation_session"), dict)
            else None,
            route.get("controls", {}).get("observation")
            if isinstance(route.get("controls"), dict)
            else None,
        ]
        activation_refs = [
            activation.get(key)
            for key in (
                "manifest",
                "toolchain",
                "quarantine",
                "route",
                "activation_session",
                "deviations",
                "sandbox_policy",
                "network_policy",
            )
        ]
        refs_by_path: dict[str, dict] = {}
        for evidence_ref in [seed_ref, *activation_refs, *route_refs]:
            if not isinstance(evidence_ref, dict):
                continue
            evidence_path = evidence_ref.get("path")
            if not isinstance(evidence_path, str):
                continue
            prior = refs_by_path.get(evidence_path)
            if prior is not None and prior.get("sha256") != evidence_ref.get("sha256"):
                errors.append(
                    f"{packet_id} activation evidence {evidence_path} has conflicting hashes"
                )
            refs_by_path[evidence_path] = evidence_ref

        expected_evidence_paths = set(WP0001_ACTIVATION_EVIDENCE_PATHS)
        if set(refs_by_path) != expected_evidence_paths:
            missing = sorted(expected_evidence_paths - set(refs_by_path))
            unexpected = sorted(set(refs_by_path) - expected_evidence_paths)
            if missing:
                errors.append(
                    f"{packet_id} activation evidence omits {', '.join(missing)}"
                )
            if unexpected:
                errors.append(
                    f"{packet_id} activation evidence references unexpected paths: {', '.join(unexpected)}"
                )
        for expected_path in sorted(expected_evidence_paths):
            evidence_ref = refs_by_path.get(expected_path)
            if expected_path == WP0001_PROJECT_SEED_EVIDENCE_PATH:
                continue
            _, reference_errors = validate_repo_evidence_reference(
                evidence_ref,
                f"{packet_id} activation evidence {expected_path}",
                expected_path=expected_path,
            )
            errors.extend(reference_errors)
        wp0001_evidence_refs = [
            refs_by_path[path] for path in sorted(refs_by_path)
        ]

        evidence_manifest_ref = activation.get("manifest")
        artifact_refs: dict[str, str] = {}
        evidence_manifest_path, _ = validate_repo_evidence_reference(
            evidence_manifest_ref,
            f"{packet_id} activation evidence manifest",
            expected_path="docs/evidence/WP-0001/a1-activation/evidence-manifest.json",
        )
        if evidence_manifest_path is not None and evidence_manifest_path.is_file():
            errors.extend(
                validate_instance_shape(
                    evidence_manifest_path,
                    WP0001_A1_EVIDENCE_SCHEMA,
                )
            )
            evidence_manifest = load_json(evidence_manifest_path)
            if evidence_manifest.get("base_commit") != base_commit:
                errors.append(
                    f"{packet_id} activation evidence manifest binds the wrong base"
                )
            if (
                evidence_manifest.get("producer", {}).get("principal_uid")
                != manifest.get("runtime_boundary", {}).get("principal_uid")
            ):
                errors.append(
                    f"{packet_id} activation evidence manifest names the wrong runtime UID"
                )
            artifact_rows = evidence_manifest.get("artifacts", [])
            for artifact in artifact_rows if isinstance(artifact_rows, list) else []:
                if not isinstance(artifact, dict):
                    continue
                artifact_path = artifact.get("path")
                artifact_hash = artifact.get("sha256")
                if not isinstance(artifact_path, str):
                    continue
                if artifact_path in artifact_refs:
                    errors.append(
                        f"{packet_id} activation evidence manifest repeats {artifact_path}"
                    )
                artifact_refs[artifact_path] = artifact_hash
                resolved_path, path_error = safe_repo_path(
                    artifact_path,
                    f"{packet_id} activation artifact path",
                )
                if path_error:
                    errors.append(path_error)
                    continue
                if (
                    resolved_path is None
                    or not resolved_path.is_file()
                    or sha256_file(resolved_path) != artifact_hash
                    or resolved_path.stat().st_size != artifact.get("byte_size")
                ):
                    errors.append(
                        f"{packet_id} activation artifact {artifact_path} differs from its manifest"
                    )
            required_artifact_refs = {
                path: reference.get("sha256")
                for path, reference in refs_by_path.items()
                if path
                != "docs/evidence/WP-0001/a1-activation/evidence-manifest.json"
            }
            for artifact_path, artifact_hash in required_artifact_refs.items():
                if artifact_refs.get(artifact_path) != artifact_hash:
                    errors.append(
                        f"{packet_id} activation evidence manifest does not bind {artifact_path}"
                    )

        entitlement = (
            route.get("entitlement")
            if isinstance(route.get("entitlement"), dict)
            else {}
        )
        identity = (
            route.get("project_identity")
            if isinstance(route.get("project_identity"), dict)
            else {}
        )
        bridge = (
            route.get("bridge")
            if isinstance(route.get("bridge"), dict)
            else {}
        )
        handshake = (
            route.get("handshake")
            if isinstance(route.get("handshake"), dict)
            else {}
        )
        controls = (
            route.get("controls")
            if isinstance(route.get("controls"), dict)
            else {}
        )
        toolchain_facts = {
            "approved_toolchain": manifest.get("approved_toolchain"),
            "profile": manifest.get("wp0001_toolchain_profile"),
        }
        entitlement_facts = {
            key: value for key, value in entitlement.items() if key != "evidence"
        }
        identity_facts = {
            key: value for key, value in identity.items() if key != "evidence"
        }
        quarantine_facts = {
            "repository": repository,
            "approved_environment": manifest.get("approved_environment"),
            "runtime_boundary": manifest.get("runtime_boundary"),
            "protection_boundary": manifest.get("protection_boundary"),
            "credential_boundary": manifest.get("credential_boundary"),
            "manual_import_boundary": manifest.get("manual_import_boundary"),
        }
        route_facts = json.loads(json.dumps(route))
        route_facts.pop("process_observation", None)
        for section, key in (
            ("bridge", "discovery_record"),
            ("entitlement", "evidence"),
            ("project_identity", "evidence"),
            ("codex_policy", "evidence"),
            ("handshake", "transcript"),
            ("activation_session", "evidence"),
            ("controls", "observation"),
        ):
            if isinstance(route_facts.get(section), dict):
                route_facts[section].pop(key, None)
        bridge_discovery_facts = {
            "connection_type": bridge.get("discovery_connection_type"),
            "connection_path": bridge.get("endpoint"),
            "project_path": bridge.get("project_path"),
            "protocol_version": bridge.get("protocol_version"),
            "editor_pid": bridge.get("editor_pid"),
            "source_file": bridge.get("connection_file"),
            "source_file_sha256": bridge.get("connection_file_sha256"),
            "endpoint_owner_uid": bridge.get("endpoint_owner_uid"),
            "endpoint_mode": bridge.get("endpoint_mode"),
        }
        policy_for_facts = (
            route.get("codex_policy")
            if isinstance(route.get("codex_policy"), dict)
            else {}
        )
        protected_config_blob_for_capture = (
            git_repo_blob(base_commit, ".codex/config.toml")
            if isinstance(base_commit, str)
            else None
        )
        protected_config_sha256_for_capture = (
            hashlib.sha256(protected_config_blob_for_capture).hexdigest()
            if protected_config_blob_for_capture is not None
            else None
        )
        handshake_facts = {
            "client_pid": handshake.get("client_pid"),
            "relay_pid": handshake.get("relay_pid"),
            "editor_pid": handshake.get("editor_pid"),
            "client_started_at": handshake.get("client_started_at"),
            "relay_started_at": handshake.get("relay_started_at"),
            "editor_started_at": handshake.get("editor_started_at"),
            "client_process_birth_id_sha256": handshake.get(
                "client_process_birth_id_sha256"
            ),
            "relay_process_birth_id_sha256": handshake.get(
                "relay_process_birth_id_sha256"
            ),
            "editor_process_birth_id_sha256": handshake.get(
                "editor_process_birth_id_sha256"
            ),
            "project_path": bridge.get("project_path"),
            "runtime_config_sha256": handshake.get("runtime_config_sha256"),
            "enabled_tools_sha256": handshake.get("enabled_tools_sha256"),
            "environment_sha256": handshake.get("environment_sha256"),
            "server_inventory_sha256": handshake.get(
                "server_inventory_sha256"
            ),
            "captured_at": handshake.get("captured_at"),
            "observed_methods": handshake.get("observed_methods"),
            "capture_complete": handshake.get("capture_complete"),
            "model_prompt_count": handshake.get("model_prompt_count"),
            "unity_tool_call_count": handshake.get("unity_tool_call_count"),
            "disconnected": handshake.get("disconnected"),
            "approval_revoked_after_disconnect": handshake.get(
                "approval_revoked_after_disconnect"
            ),
        }
        activation_session_record = (
            route.get("activation_session")
            if isinstance(route.get("activation_session"), dict)
            else {}
        )
        activation_session_facts = {
            key: value
            for key, value in activation_session_record.items()
            if key != "evidence"
        }
        approved_environment_for_facts = (
            manifest.get("approved_environment")
            if isinstance(manifest.get("approved_environment"), dict)
            else {}
        )
        network_facts = {
            **{
                key: value
                for key, value in controls.items()
                if key != "observation"
            },
            "network_policy_sha256": approved_environment_for_facts.get(
                "network_policy_sha256"
            ),
        }
        deviations_facts = {
            "preserved_deviation_ids": list(WP0001_PRESERVED_DEVIATION_IDS)
        }
        expected_record_facts = {
            "docs/evidence/WP-0001/a1-activation/toolchain.json": (
                "toolchain",
                toolchain_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/entitlement-linkage.json": (
                "entitlement-linkage",
                entitlement_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/project-identity.json": (
                "project-identity",
                identity_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/quarantine.json": (
                "quarantine",
                quarantine_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/mcp-route.json": (
                "mcp-route",
                route_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/bridge-discovery.json": (
                "bridge-discovery",
                bridge_discovery_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/clean-handshake.json": (
                "clean-handshake",
                handshake_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/activation-session.json": (
                "activation-session",
                activation_session_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/network-observation.json": (
                "network-observation",
                network_facts,
            ),
            "docs/evidence/WP-0001/a1-activation/deviations.json": (
                "deviations",
                deviations_facts,
            ),
        }
        for evidence_path, (kind, facts) in expected_record_facts.items():
            record_path, path_error = safe_repo_path(
                evidence_path,
                f"{packet_id} {kind} evidence path",
            )
            if path_error or record_path is None or not record_path.is_file():
                continue
            record = load_json(record_path)
            if isinstance(record, dict) and isinstance(
                record.get("source_artifacts"), list
            ):
                wp0001_source_refs.extend(
                    source
                    for source in record["source_artifacts"]
                    if isinstance(source, dict)
                )
            errors.extend(
                validate_wp0001_evidence_record_data(
                    record,
                    expected_kind=kind,
                    expected_facts=facts,
                    label=f"{packet_id} {kind} evidence",
                    raw_enabled_tools=(
                        policy_for_facts.get("enabled_tools")
                        if isinstance(
                            policy_for_facts.get("enabled_tools"), list
                        )
                        else None
                    ),
                    raw_route=route,
                    raw_runtime=(
                        manifest.get("runtime_boundary")
                        if isinstance(
                            manifest.get("runtime_boundary"), dict
                        )
                        else None
                    ),
                    route_contract_sha256=canonical_json_sha256(
                        wp0001_mcp_route_contract(manifest)
                    ),
                    protected_config_sha256=(
                        protected_config_sha256_for_capture
                    ),
                    raw_collectors=(
                        manifest.get("raw_capture_collectors")
                        if isinstance(
                            manifest.get("raw_capture_collectors"), dict
                        )
                        else None
                    ),
                )
            )
        for source_ref in wp0001_source_refs:
            source_path = source_ref.get("path")
            source_hash = source_ref.get("sha256")
            if artifact_refs.get(source_path) != source_hash:
                errors.append(
                    f"{packet_id} activation evidence manifest does not bind source artifact {source_path}"
                )
        quarantine_live_path, quarantine_live_error = safe_repo_path(
            "docs/evidence/WP-0001/a1-activation/commands/quarantine-live.json",
            f"{packet_id} quarantine live capture path",
        )
        if (
            quarantine_live_error is None
            and quarantine_live_path is not None
            and quarantine_live_path.is_file()
        ):
            quarantine_live = load_json(quarantine_live_path)
            quarantine_capture = parse_datetime(
                quarantine_live.get("captured_at")
                if isinstance(quarantine_live, dict)
                else None
            )
            if quarantine_capture is not None:
                wp0001_live_capture_times.append(quarantine_capture)
        for live_capture_relative in (
            WP0001_REQUIRED_RAW_SOURCE_BY_KIND["mcp-route"],
            WP0001_REQUIRED_RAW_SOURCE_BY_KIND["clean-handshake"],
            WP0001_REQUIRED_RAW_SOURCE_BY_KIND["activation-session"],
            WP0001_REQUIRED_RAW_SOURCE_BY_KIND["network-observation"],
        ):
            live_capture_path, live_capture_error = safe_repo_path(
                live_capture_relative,
                f"{packet_id} live capture path",
            )
            if (
                live_capture_error is None
                and live_capture_path is not None
                and live_capture_path.is_file()
            ):
                live_capture = load_json(live_capture_path)
                live_capture_time = parse_datetime(
                    live_capture.get("captured_at")
                    if isinstance(live_capture, dict)
                    else None
                )
                if live_capture_time is not None:
                    wp0001_live_capture_times.append(live_capture_time)

        policy = route.get("codex_policy", {}) if isinstance(route, dict) else {}
        relay = route.get("relay", {}) if isinstance(route, dict) else {}
        runtime_config_ref = (
            policy.get("evidence") if isinstance(policy, dict) else None
        )
        runtime_config_path, _ = validate_repo_evidence_reference(
            runtime_config_ref,
            f"{packet_id} Codex runtime config",
            expected_path=WP0001_RUNTIME_CONFIG_EVIDENCE_PATH,
        )
        if runtime_config_path is not None and runtime_config_path.is_file():
            try:
                runtime_config = tomllib.loads(
                    runtime_config_path.read_text(encoding="utf-8")
                )
            except (OSError, UnicodeDecodeError, tomllib.TOMLDecodeError) as exc:
                errors.append(
                    f"{packet_id} Codex runtime config cannot be parsed: {exc}"
                )
            else:
                server_name = policy.get("server_name")
                configured_servers = runtime_config.get("mcp_servers", {})
                server = (
                    configured_servers.get(server_name, {})
                    if isinstance(configured_servers, dict)
                    else {}
                )
                expected_server = {
                    "command": relay.get("path"),
                    "args": relay.get("arguments"),
                    "enabled": True,
                    "required": True,
                    "enabled_tools": policy.get("enabled_tools"),
                    "default_tools_approval_mode": "prompt",
                    "env": {
                        **(
                            policy.get("environment_bindings")
                            if isinstance(
                                policy.get("environment_bindings"), dict
                            )
                            else {}
                        ),
                        **{
                            key: policy.get(
                                "client_environment_guard", {}
                            ).get(key)
                            for key in (
                                "CODEX_HOME",
                                "XDG_CONFIG_HOME",
                                "XDG_CACHE_HOME",
                                "XDG_DATA_HOME",
                                "GIT_CONFIG_NOSYSTEM",
                                "GIT_CONFIG_GLOBAL",
                                "GIT_TERMINAL_PROMPT",
                            )
                        },
                    },
                }
                if runtime_config.get("approval_policy") != "on-request":
                    errors.append(
                        f"{packet_id} Codex runtime config approval_policy is not on-request"
                    )
                if set(runtime_config) != {"approval_policy", "mcp_servers"}:
                    errors.append(
                        f"{packet_id} Codex runtime config contains undeclared top-level settings"
                    )
                if set(configured_servers) != {server_name}:
                    errors.append(
                        f"{packet_id} Codex runtime config contains undeclared MCP servers"
                    )
                if set(server) != set(expected_server):
                    errors.append(
                        f"{packet_id} Codex runtime MCP server contains undeclared settings"
                    )
                for key, expected_value in expected_server.items():
                    if server.get(key) != expected_value:
                        errors.append(
                            f"{packet_id} Codex runtime config {server_name}.{key} differs from the boundary"
                        )

        protected_config = protected_config_blob_for_capture
        if protected_config is None:
            errors.append(
                f"{packet_id} protected base lacks fail-closed .codex/config.toml"
            )
        else:
            try:
                protected_toml = tomllib.loads(protected_config.decode("utf-8"))
            except (UnicodeDecodeError, tomllib.TOMLDecodeError):
                errors.append(
                    f"{packet_id} protected base .codex/config.toml is invalid"
                )
            else:
                protected_servers = protected_toml.get("mcp_servers", {})
                protected_server = (
                    protected_servers.get("unity_mcp", {})
                    if isinstance(protected_servers, dict)
                    else {}
                )
                expected_protected_server = {
                    "command": (
                        "/Users/sasha/.unity/relay/relay_mac_arm64.app/"
                        "Contents/MacOS/relay_mac_arm64"
                    ),
                    "args": [
                        "--mcp",
                        "--project-path",
                        "/REPLACE-WITH-RECEIPT-BOUND-A1-CLONE/Game",
                    ],
                    "startup_timeout_sec": 120,
                    "enabled": False,
                }
                if (
                    set(protected_toml) != {"mcp_servers"}
                    or not isinstance(protected_servers, dict)
                    or set(protected_servers) != {"unity_mcp"}
                    or protected_server != expected_protected_server
                ):
                    errors.append(
                        f"{packet_id} protected base .codex/config.toml is not the exact fail-closed entry"
                    )
    protected_required = {
        "docs/foundation-v0.1/00-GAME-CONSTITUTION.md",
        "docs/foundation-v0.1/ledger/decisions.jsonl",
        "docs/foundation-v0.1/governance/",
        "docs/foundation-v0.1/ledger/receipts/",
        ".git/refs/heads/main",
        ".codex/",
    }
    if not protected_required.issubset(set(protection.get("protected_paths", []))):
        errors.append(f"{packet_id} boundary omits a required protected path")
    denied_required = {
        "protected-main-write", "governance-write", "receipt-write",
        "merge", "release",
    }
    denied = set(manifest.get("credential_boundary", {}).get("denied_capabilities", []))
    if not denied_required.issubset(denied):
        errors.append(f"{packet_id} boundary credential denial is incomplete")
    if (
        packet_id == "WP-0001"
        and manifest.get("credential_boundary", {}).get(
            "approved_credential_ids"
        )
        != []
    ):
        errors.append(
            f"{packet_id} boundary must approve no credential IDs"
        )

    if not isinstance(activation_receipt, dict):
        errors.append(f"{packet_id} boundary has no activation receipt")
    else:
        required_activation_claims = {
            "A1-QUARANTINE-BOUNDARY-VERIFIED",
            f"ACTIVATE-A1-{packet_id}",
        }
        if packet_id == "WP-0001":
            required_activation_claims.update(
                {
                    "AUTHORIZE-WP0001-MCP-ALLOWLIST",
                    "AUTHORIZE-WP0001-RAW-COLLECTORS",
                    "AUTHORIZE-WP0001-CODE-IDENTITIES",
                }
            )
        receipt_claims = subject_claims(activation_receipt).get(
            packet_id, set()
        )
        if (
            activation_receipt.get("receipt_kind") != "packet-activation"
            or activation_receipt.get("issuer_role") != "creator"
            or activation_receipt.get("sealed") is not True
            or not isinstance(
                activation_receipt.get("artifact_resolver"), dict
            )
            or activation_receipt["artifact_resolver"].get("type")
            != "external-protected"
            or not isinstance(
                activation_receipt["artifact_resolver"].get(
                    "resolver_reference"
                ),
                str,
            )
            or not activation_receipt["artifact_resolver"].get(
                "resolver_reference"
            )
            or not isinstance(
                activation_receipt.get("signature_reference"), str
            )
            or not activation_receipt.get("signature_reference")
            or set(activation_receipt.get("subject_ids", []))
            != {packet_id}
            or not required_activation_claims.issubset(receipt_claims)
        ):
            errors.append(
                f"{packet_id} activation receipt lacks exact external-protected creator activation authority"
            )
        if manifest.get("attestation_receipt_id") != activation_receipt.get("receipt_id"):
            errors.append(f"{packet_id} manifest names a different attestation receipt")
        if manifest.get("attested_by") != activation_receipt.get("issued_by"):
            errors.append(f"{packet_id} manifest attestor differs from receipt issuer")
        if activation_receipt.get("artifact_sha256", {}).get(reference.get("path")) != actual_hash:
            errors.append(f"{packet_id} activation receipt does not bind exact boundary-manifest bytes")
        for evidence_ref in wp0001_evidence_refs:
            evidence_path = evidence_ref.get("path")
            evidence_sha256 = evidence_ref.get("sha256")
            if (
                activation_receipt.get("artifact_sha256", {}).get(evidence_path)
                != evidence_sha256
            ):
                errors.append(
                    f"{packet_id} activation receipt does not bind {evidence_path}"
                )
        if packet_id == "WP-0001":
            activation_session = manifest.get("unity_mcp_route", {}).get(
                "activation_session", {}
            )
            session_capture = parse_datetime(
                activation_session.get("captured_at")
                if isinstance(activation_session, dict)
                else None
            )
            manifest_created = parse_datetime(manifest.get("created_at"))
            receipt_issued = parse_datetime(activation_receipt.get("issued_at"))
            if (
                session_capture is None
                or manifest_created is None
                or receipt_issued is None
                or not (
                    session_capture
                    <= manifest_created
                    <= receipt_issued
                )
                or receipt_issued - session_capture > timedelta(minutes=5)
            ):
                errors.append(
                    f"{packet_id} activation receipt is not fresh for the live MCP session"
                )
            if (
                receipt_issued is None
                or not wp0001_live_capture_times
                or any(
                    capture > receipt_issued
                    or receipt_issued - capture > timedelta(minutes=15)
                    for capture in wp0001_live_capture_times
                )
            ):
                errors.append(
                    f"{packet_id} activation receipt is not fresh for live boundary captures"
                )
        expected_receipt_foundation = {
            "constitution_sha256": state.get("constitution_sha256"),
            "decision_ledger_sha256": state.get("decision_ledger_sha256"),
            "last_creator_receipt_id": state.get("last_creator_receipt_id"),
        }
        if activation_receipt.get("foundation_binding") != expected_receipt_foundation:
            errors.append(f"{packet_id} activation receipt lacks the exact foundation binding")
        if activation_receipt.get("subject_contract_sha256", {}).get(packet_id) != packet.get("contract_sha256"):
            errors.append(f"{packet_id} activation receipt binds the wrong packet contract")

    last_creator_receipt = receipts_by_id.get(state.get("last_creator_receipt_id"))
    if not last_creator_receipt or not last_creator_receipt.get("sealed") or last_creator_receipt.get("issuer_role") != "creator":
        errors.append(f"{packet_id} state does not name a sealed prior creator receipt")
    return manifest, errors


def validate_packet_approval_and_events(
    packet: dict,
    receipts_by_id: dict[str, dict],
    activation_receipt_id: str | None,
) -> list[str]:
    errors: list[str] = []
    packet_id = packet.get("id", "unknown-packet")
    events = [
        event
        for event in packet.get("status_events", [])
        if isinstance(event, dict)
    ]
    acceptance_events = [event for event in events if event.get("to") == "accepted"]
    if acceptance_events:
        acceptance_receipt = receipts_by_id.get(packet.get("approval_receipt_id"))
        if (
            len(acceptance_events) != 1
            or acceptance_events[0].get("receipt_id")
            != packet.get("approval_receipt_id")
        ):
            errors.append(
                f"{packet_id} must retain one acceptance event with its approval receipt"
            )
        if not acceptance_receipt or not acceptance_receipt.get("sealed"):
            errors.append(f"{packet_id} lacks a sealed packet-acceptance receipt")
        else:
            if acceptance_receipt.get("receipt_kind") != "packet-acceptance":
                errors.append(f"{packet_id} approval receipt has the wrong receipt kind")
            if acceptance_receipt.get("issuer_role") != packet.get("required_approver"):
                errors.append(f"{packet_id} required approver role differs from receipt issuer role")
            if packet.get("approved_by") != acceptance_receipt.get("issued_by"):
                errors.append(f"{packet_id} approved_by differs from approval receipt issuer")
            if acceptance_receipt.get("subject_contract_sha256", {}).get(packet_id) != packet.get("contract_sha256"):
                errors.append(f"{packet_id} acceptance receipt does not bind immutable packet contract")
            required_claim = f"ACCEPT-{packet_id}"
            if required_claim not in subject_claims(acceptance_receipt).get(packet_id, set()):
                errors.append(f"{packet_id} acceptance receipt lacks {required_claim}")
    elif packet.get("approval_receipt_id") is not None or packet.get("approved_by") is not None:
        errors.append(f"{packet_id} claims acceptance authority without an acceptance event")
    activation_events = [event for event in events if event.get("to") == "active"]
    if activation_events:
        if len(activation_events) != 1 or activation_events[0].get("receipt_id") != activation_receipt_id:
            errors.append(f"{packet_id} activation history does not retain its sole activation receipt")
        activation_receipt = receipts_by_id.get(activation_receipt_id)
        if not activation_receipt or not activation_receipt.get("sealed") or activation_receipt.get("receipt_kind") != "packet-activation":
            errors.append(f"{packet_id} retained activation evidence is missing, unsealed, or wrong-kind")
        for event in events:
            if event.get("to") in {"active", "verifying", "candidate"} and event.get("receipt_id") != activation_receipt_id:
                errors.append(f"{packet_id} active-lifecycle event {event.get('event_id')} loses activation receipt continuity")
        if not isinstance(packet.get("a1_boundary_manifest"), dict):
            errors.append(f"{packet_id} dropped its boundary manifest after activation")
    release_events = [event for event in events if event.get("to") == "released"]
    if release_events:
        if len(release_events) != 1:
            errors.append(f"{packet_id} must retain exactly one release event")
        else:
            completion_receipt = receipts_by_id.get(release_events[0].get("receipt_id"))
            required_claim = f"ACCEPT-COMPLETION-{packet_id}"
            if (
                not completion_receipt
                or not completion_receipt.get("sealed")
                or completion_receipt.get("receipt_kind") != "packet-completion"
                or completion_receipt.get("issuer_role") != "creator"
            ):
                errors.append(f"{packet_id} release event lacks sealed creator completion evidence")
            else:
                if required_claim not in subject_claims(completion_receipt).get(packet_id, set()):
                    errors.append(f"{packet_id} completion receipt lacks {required_claim}")
                if completion_receipt.get("subject_contract_sha256", {}).get(packet_id) != packet.get("contract_sha256"):
                    errors.append(f"{packet_id} completion receipt binds the wrong packet contract")
    return errors


def validate_wp0002_gate_controls(gate: dict, packet_contract_sha256: str) -> list[str]:
    """Fail closed unless every WP-0002 entry control is explicit and exact."""
    errors: list[str] = []
    expected_decisions = {
        "D-0006": (["ratified"], ["RATIFY-THESIS"], "block-and-revise-constitution-or-packet"),
        "D-0007": (["ratified"], ["DRIVE", "COMMAND"], "block-and-revise-packet"),
        "D-0008": (["ratified"], ["SLOWED", "PAUSED", "FULL"], "block-and-revise-packet"),
        "D-0009": (["ratified"], ["SCARS"], "branch-to-harsh-or-custom-cruelty-proof-packet"),
        "D-0010": (["ratified"], ["CULTURES"], "branch-to-conquest-compatible-packet"),
        "D-0011": (["ratified"], ["ACCENT", "NONE"], "branch-to-combat-pillar-packet"),
        "D-0012": (["ratified"], ["RATIFY-SLICE"], "block-and-revise-slice-and-packet"),
        "D-0021": (["ratified"], ["SOLO-OFFLINE"], "branch-to-connected-architecture-packet"),
        "D-0029": (["ratified"], ["ROUTE+ROAD"], "branch-to-selected-topology-packet"),
        "D-0035": (["ratified"], ["RATIFY-CORE"], "block-and-revise-constitution-or-packet"),
        "D-0036": (["ratified"], ["TITLE-AND-PROTAGONIST-SASHA"], "block-and-repair-title-protagonist-binding"),
        "D-0037": (["ratified"], ["COLONY-HUMANS-ROBOTS-OR-MIXED"], "block-and-revise-composition-proof"),
    }
    decision_requirements = gate.get("decision_requirements", [])
    decisions_by_id = {
        requirement.get("decision_id"): requirement
        for requirement in decision_requirements
        if isinstance(requirement, dict)
    }
    if (
        len(decision_requirements) != len(expected_decisions)
        or set(decisions_by_id) != set(expected_decisions)
    ):
        errors.append("WP-0002 gate must contain exactly the twelve pinned decision requirements")
    for decision_id, (statuses, claims, mismatch_action) in expected_decisions.items():
        requirement = decisions_by_id.get(decision_id)
        if requirement is None:
            continue
        expected_fields = {
            "accepted_statuses": statuses,
            "allowed_claims": claims,
            "required_receipt_kind": "decision-ratification",
            "required_issuer_role": "creator",
            "required_resolver_type": "external-protected",
            "required_subject_contract_sha256": {
                "WP-0002": packet_contract_sha256,
            },
            "mismatch_action": mismatch_action,
        }
        for field, expected in expected_fields.items():
            if requirement.get(field) != expected:
                errors.append(
                    f"WP-0002 decision {decision_id} {field} must equal {expected!r}"
                )
    title_receipt_id = decisions_by_id.get("D-0036", {}).get("receipt_id")
    composition_receipt_id = decisions_by_id.get("D-0037", {}).get("receipt_id")
    if title_receipt_id is not None or composition_receipt_id is not None:
        materialized_decision_ids = [
            requirement.get("receipt_id")
            for requirement in decision_requirements
            if isinstance(requirement, dict)
            and requirement.get("receipt_id") is not None
        ]
        if (
            not isinstance(title_receipt_id, str)
            or not isinstance(composition_receipt_id, str)
            or title_receipt_id == composition_receipt_id
            or materialized_decision_ids.count(title_receipt_id) != 1
            or materialized_decision_ids.count(composition_receipt_id) != 1
        ):
            errors.append(
                "WP-0002 title and composition decisions must materialize through distinct own receipt IDs"
            )

    expected_receipts = {
        "accept-WP-0002": {
            "subject_ids": ["WP-0002"],
            "required_claims": ["ACCEPT-WP-0002"],
            "required_receipt_kind": "packet-acceptance",
        },
        "authorize-city-comparison": {
            "subject_ids": ["D-0030", "WP-0002"],
            "required_claims": ["AUTHORIZE-CITY-COMPARISON"],
            "required_receipt_kind": "creator-authorization",
        },
        "activate-WP-0002-local-development": {
            "subject_ids": ["WP-0002"],
            "required_claims": [
                "A1-LOCAL-BOUNDARY-VERIFIED",
                "ACTIVATE-A1-WP-0002",
            ],
            "required_receipt_kind": "packet-activation",
        },
    }
    requirements_by_purpose = {
        requirement.get("purpose"): requirement
        for requirement in gate.get("receipt_requirements", [])
        if isinstance(requirement, dict)
    }
    if set(requirements_by_purpose) != set(expected_receipts):
        errors.append("WP-0002 receipt controls must contain exactly the three required purposes")
        return errors
    for purpose, receipt_contract in expected_receipts.items():
        requirement = requirements_by_purpose[purpose]
        expected_fields = {
            **receipt_contract,
            "required_issuer_role": "creator",
            "required_resolver_type": "external-protected",
            "required_subject_contract_sha256": {
                "WP-0002": packet_contract_sha256,
            },
        }
        for field, expected in expected_fields.items():
            if requirement.get(field) != expected:
                errors.append(
                    f"WP-0002 receipt purpose {purpose} {field} must equal {expected!r}"
                )
    declared_kinds = {
        requirement.get("required_receipt_kind")
        for requirement in requirements_by_purpose.values()
    }
    if declared_kinds != {
        contract["required_receipt_kind"] for contract in expected_receipts.values()
    }:
        errors.append("WP-0002 acceptance, authorization, and activation kinds must remain distinct")
    receipt_ids = [
        requirement.get("receipt_id")
        for requirement in requirements_by_purpose.values()
        if requirement.get("receipt_id") is not None
    ]
    if len(receipt_ids) != len(set(receipt_ids)):
        errors.append(
            "WP-0002 acceptance, authorization, and activation must use distinct receipt JSONs"
        )
    return errors


def wp0002_expected_unity_meta(relative_target: str, is_directory: bool) -> bytes:
    """Return the deterministic Unity metadata bytes for one package target."""
    guid = hashlib.sha256(
        f"ac21.sasha.unity-meta.v1:{relative_target}".encode("utf-8")
    ).hexdigest()[:32]
    if is_directory:
        body = (
            f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\n"
            "DefaultImporter:\n  externalObjects: {}\n  userData:\n"
            "  assetBundleName:\n  assetBundleVariant:\n"
        )
    elif relative_target.endswith(".cs"):
        body = (
            f"fileFormatVersion: 2\nguid: {guid}\nMonoImporter:\n"
            "  externalObjects: {}\n  serializedVersion: 2\n"
            "  defaultReferences: []\n  executionOrder: 0\n"
            "  icon: {instanceID: 0}\n  userData:\n"
            "  assetBundleName:\n  assetBundleVariant:\n"
        )
    elif relative_target.endswith(".asmdef"):
        body = (
            f"fileFormatVersion: 2\nguid: {guid}\nAssemblyDefinitionImporter:\n"
            "  externalObjects: {}\n  userData:\n"
            "  assetBundleName:\n  assetBundleVariant:\n"
        )
    else:
        body = (
            f"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData:\n"
            "  assetBundleName:\n  assetBundleVariant:\n"
        )
    return body.encode("utf-8")


def validate_wp0002_package_meta_inventory(
    repo_root: Path = REPO_ROOT,
    expected_hashes: dict[str, str] = WP0002_PACKAGE_META_SHA256,
) -> list[str]:
    """Freeze existing package metadata and confine future LastBearing metadata."""
    errors: list[str] = []
    expected_meta = set(expected_hashes)
    expected_targets = {path.removesuffix(".meta") for path in expected_meta}
    future_roots = set(WP0002_PACKAGE_META_FUTURE_ROOTS)
    future_root_meta = {f"{path}.meta" for path in future_roots}
    frozen_targets: set[str] = set()
    frozen_meta: set[str] = set()
    future_targets: set[str] = set()
    future_meta: set[str] = set()

    def is_future(relative: str) -> bool:
        return relative in future_roots or any(
            relative.startswith(f"{root}/") for root in future_roots
        )

    for root_name in WP0002_PACKAGE_META_ROOTS:
        root = repo_root / root_name
        try:
            root_mode = root.lstat().st_mode
        except OSError as exc:
            errors.append(f"WP-0002 package metadata root is missing: {root_name}: {exc}")
            continue
        if not stat.S_ISDIR(root_mode) or stat.S_ISLNK(root_mode):
            errors.append(f"WP-0002 package metadata root must be a regular directory: {root_name}")
            continue
        for path in root.rglob("*"):
            relative = path.relative_to(repo_root).as_posix()
            try:
                mode = path.lstat().st_mode
            except OSError as exc:
                errors.append(f"WP-0002 package metadata cannot inspect {relative}: {exc}")
                continue
            if stat.S_ISLNK(mode) or not (stat.S_ISREG(mode) or stat.S_ISDIR(mode)):
                errors.append(f"WP-0002 package import target must be regular and not a symlink: {relative}")
            if relative.endswith(".meta"):
                if relative in future_root_meta or is_future(relative.removesuffix(".meta")):
                    future_meta.add(relative)
                else:
                    frozen_meta.add(relative)
            elif is_future(relative):
                future_targets.add(relative)
            else:
                frozen_targets.add(relative)

    if frozen_targets != expected_targets:
        errors.append(
            "WP-0002 package import target inventory differs: "
            f"missing={sorted(expected_targets - frozen_targets)}, "
            f"extra={sorted(frozen_targets - expected_targets)}"
        )
    if frozen_meta != expected_meta:
        errors.append(
            "WP-0002 package metadata inventory differs: "
            f"missing={sorted(expected_meta - frozen_meta)}, "
            f"extra={sorted(frozen_meta - expected_meta)}"
        )

    seen_guids: dict[str, str] = {}
    for relative_meta in sorted(frozen_meta & expected_meta):
        meta_path = repo_root / relative_meta
        target_relative = relative_meta.removesuffix(".meta")
        target_path = repo_root / target_relative
        try:
            meta_bytes = meta_path.read_bytes()
            target_mode = target_path.lstat().st_mode
        except OSError as exc:
            errors.append(f"WP-0002 package metadata cannot read {relative_meta}: {exc}")
            continue
        actual_hash = hashlib.sha256(meta_bytes).hexdigest()
        if actual_hash != expected_hashes[relative_meta]:
            errors.append(
                f"WP-0002 package metadata hash mismatch for {relative_meta}: "
                f"expected {expected_hashes[relative_meta]}, found {actual_hash}"
            )
        expected_bytes = wp0002_expected_unity_meta(
            target_relative, stat.S_ISDIR(target_mode)
        )
        if meta_bytes != expected_bytes:
            errors.append(f"WP-0002 package metadata bytes are not deterministic for {relative_meta}")
        match = re.search(rb"^guid: ([0-9a-f]{32})$", meta_bytes, re.MULTILINE)
        if match:
            guid = match.group(1).decode("ascii")
            if guid in seen_guids:
                errors.append(
                    f"WP-0002 package metadata GUID {guid} is duplicated by "
                    f"{seen_guids[guid]} and {relative_meta}"
                )
            seen_guids[guid] = relative_meta
        else:
            errors.append(f"WP-0002 package metadata lacks one valid GUID: {relative_meta}")

    expected_future_meta = {f"{target}.meta" for target in future_targets}
    if future_meta != expected_future_meta:
        errors.append(
            "WP-0002 LastBearing metadata pairing differs: "
            f"missing={sorted(expected_future_meta - future_meta)}, "
            f"extra={sorted(future_meta - expected_future_meta)}"
        )
    for relative_meta in sorted(future_meta & expected_future_meta):
        target_relative = relative_meta.removesuffix(".meta")
        target_path = repo_root / target_relative
        meta_path = repo_root / relative_meta
        try:
            target_mode = target_path.lstat().st_mode
            meta_mode = meta_path.lstat().st_mode
            meta_bytes = meta_path.read_bytes()
        except OSError as exc:
            errors.append(f"WP-0002 LastBearing metadata cannot read {relative_meta}: {exc}")
            continue
        if not stat.S_ISREG(meta_mode) or stat.S_ISLNK(meta_mode):
            errors.append(f"WP-0002 LastBearing metadata must be a regular file: {relative_meta}")
            continue
        if meta_bytes != wp0002_expected_unity_meta(
            target_relative, stat.S_ISDIR(target_mode)
        ):
            errors.append(f"WP-0002 LastBearing metadata is not deterministic: {relative_meta}")
    return errors


def _load_wp0002_package_graph_checker(
    checker_path: Path = WP0002_PACKAGE_GRAPH_CHECKER,
) -> tuple[dict[str, object] | None, list[str]]:
    """Load only the byte-pinned A0 checker, without creating import caches."""
    try:
        metadata = checker_path.lstat()
        checker_bytes = checker_path.read_bytes()
    except OSError as exc:
        return None, [f"WP-0002 protected package-graph checker is missing: {exc}"]
    if not stat.S_ISREG(metadata.st_mode) or stat.S_ISLNK(metadata.st_mode):
        return None, ["WP-0002 protected package-graph checker must be a regular file"]
    actual_hash = hashlib.sha256(checker_bytes).hexdigest()
    if actual_hash != WP0002_PACKAGE_GRAPH_CHECKER_SHA256:
        return None, [
            "WP-0002 protected package-graph checker hash mismatch: "
            f"expected {WP0002_PACKAGE_GRAPH_CHECKER_SHA256}, found {actual_hash}"
        ]
    namespace: dict[str, object] = {
        "__name__": "_wp0002_package_graph_checker",
        "__file__": str(checker_path),
    }
    try:
        exec(compile(checker_bytes, str(checker_path), "exec"), namespace)
    except Exception as exc:  # pragma: no cover - hash pin makes this defensive
        return None, [f"WP-0002 protected package-graph checker cannot load: {exc}"]
    expected_contract = {
        "CHECKER_CONTRACT_VERSION": WP0002_PACKAGE_GRAPH_CHECKER_CONTRACT,
        "PROTECTED_BASE_COMMIT": WP0002_PACKAGE_GRAPH_BASE,
        "GRAPH_PATHS": WP0002_PACKAGE_GRAPH_PATHS,
    }
    for name, expected in expected_contract.items():
        if namespace.get(name) != expected:
            return None, [
                f"WP-0002 protected package-graph checker {name} differs from trusted contract"
            ]
    if not callable(namespace.get("compare_package_graph")):
        return None, [
            "WP-0002 protected package-graph checker lacks compare_package_graph"
        ]
    return namespace, []


def validate_wp0002_package_graph_checker_contract(
    checker_path: Path = WP0002_PACKAGE_GRAPH_CHECKER,
) -> list[str]:
    _, errors = _load_wp0002_package_graph_checker(checker_path)
    return errors


def validate_wp0002_package_graph_documents(
    base_files: dict[str, bytes],
    candidate_files: dict[str, bytes],
    checker_path: Path = WP0002_PACKAGE_GRAPH_CHECKER,
) -> list[str]:
    checker, errors = _load_wp0002_package_graph_checker(checker_path)
    if errors or checker is None:
        return errors
    compare = checker["compare_package_graph"]
    result = compare(base_files, candidate_files)  # type: ignore[operator]
    if not isinstance(result, list) or not all(isinstance(item, str) for item in result):
        return ["WP-0002 protected package-graph checker returned an invalid result"]
    return result


def validate_wp0002_package_graph_worktree(
    base_commit: str = WP0002_PACKAGE_GRAPH_BASE,
) -> list[str]:
    """Apply the protected checker whenever any package-graph file differs."""
    checker, errors = _load_wp0002_package_graph_checker()
    if errors or checker is None:
        return errors
    if base_commit != WP0002_PACKAGE_GRAPH_BASE:
        return [f"WP-0002 package-graph base must equal {WP0002_PACKAGE_GRAPH_BASE}"]
    base_files: dict[str, bytes] = {}
    candidate_files: dict[str, bytes] = {}
    for path in WP0002_PACKAGE_GRAPH_PATHS:
        result = run_foundation_git(["show", f"{base_commit}:{path}"])
        if result.returncode != 0:
            errors.append(f"WP-0002 package-graph protected base lacks {path}")
            continue
        base_files[path] = result.stdout
        candidate_path, path_error = safe_repo_path(path, "WP-0002 package graph path")
        if path_error or candidate_path is None:
            errors.append(path_error or f"WP-0002 package graph path is invalid: {path}")
            continue
        try:
            candidate_files[path] = candidate_path.read_bytes()
        except OSError as exc:
            errors.append(f"WP-0002 package graph cannot read {path}: {exc}")
    if errors:
        return errors
    if all(base_files[path] == candidate_files[path] for path in WP0002_PACKAGE_GRAPH_PATHS):
        return []
    compare = checker["compare_package_graph"]
    result = compare(base_files, candidate_files)  # type: ignore[operator]
    if not isinstance(result, list) or not all(isinstance(item, str) for item in result):
        return ["WP-0002 protected package-graph checker returned an invalid result"]
    return result


def wp0002_ci_requires_lastbearing_project(
    packet: dict,
    materialized_now: bool,
) -> bool:
    """Require the project from current-tree materialization or lifecycle state."""
    if materialized_now:
        return True
    status = packet.get("status")
    if status == "rolled-back":
        return False
    destinations = {
        event.get("to")
        for event in packet.get("status_events", [])
        if isinstance(event, dict)
    }
    materializing_states = {"verifying", "candidate", "released"}
    if status in materializing_states or destinations & materializing_states:
        return True
    # Rejection after activation is not a governance-only terminal path. The one
    # pre-materialization active terminal exception is active -> rolled-back.
    if status == "rejected" and "active" in destinations:
        return True
    return False


def validate_wp0002_ci_save_contract(
    path: Path, packet: dict | None = None
) -> list[str]:
    """Pin the LastBearing lifecycle and complete executable command matrix in CI."""
    try:
        source = path.read_text(encoding="utf-8")
    except OSError as exc:
        return [f"WP-0002 CI save contract cannot read {path}: {exc}"]

    errors: list[str] = []
    required_fragments = (
        'status="$(python3 -B -c \'import json; print(json.load(open('
        '"docs/foundation-v0.1/work-packets/proposed/WP-0002.json", '
        'encoding="utf-8"))["status"])\')"',
        'project="Tests/AtomicLandPirate.CoreTests/LastBearing/'
        'AtomicLandPirate.LastBearingTests.csproj"',
        '"SimulationCore/Runtime/LastBearing"',
        '"SaveContracts/Runtime/LastBearing"',
        '"Game/Assets/AtomicLandPirate/LastBearing"',
        '"Game/Assets/AtomicLandPirate/LastBearing.meta"',
        '"Tests/AtomicLandPirate.CoreTests/LastBearing"',
        'if [[ -e "$path" || -L "$path" ]]; then',
        'foundation.wp0002_ci_requires_lastbearing_project(',
        'foundation.wp0002_ci_requires_lastbearing_project(packet, sys.argv[1] == "true")',
        '"$materialized")"',
        'require_project="$lifecycle_requires_project"',
        'if [[ ! -f "$project" ]]; then',
        'dotnet run --project "$project" --configuration Release -- --test dev-save-atomic',
        'dotnet run --project "$project" --configuration Release -- --test dev-save-boundary',
        'elif [[ "$status" == "proposed" || "$status" == "accepted" || "$status" == "active" || "$status" == "rejected" || "$status" == "superseded" || "$status" == "rolled-back" ]]; then',
        'echo "WP-0002 status $status cannot omit the LastBearing test project." >&2',
    )
    for fragment in required_fragments:
        if fragment not in source:
            errors.append(f"WP-0002 CI save contract lacks exact fragment {fragment!r}")
    for forbidden_fragment in (
        "materialized_in_history",
        'git log --format=%H -- "${last_bearing_paths[@]}"',
    ):
        if forbidden_fragment in source:
            errors.append(
                "WP-0002 CI save contract may not use historical path presence: "
                f"{forbidden_fragment!r}"
            )
    for command in WP0002_REQUIRED_CI_COMMANDS:
        workflow_command = command.replace(
            "Tests/AtomicLandPirate.CoreTests/LastBearing/AtomicLandPirate.LastBearingTests.csproj",
            '"$project"',
        )
        if source.count(workflow_command) != 1:
            errors.append(
                f"WP-0002 CI must invoke exact required command once: {workflow_command}"
            )
    if "--test all" in source:
        errors.append("WP-0002 CI must not replace the two pinned save tests with --test all")
    if packet is not None:
        required_by_id = {
            test.get("id"): test
            for test in packet.get("acceptance_tests", [])
            if isinstance(test, dict) and test.get("required") is True
        }
        packet_commands = tuple(
            required_by_id.get(test_id, {}).get("command")
            for test_id in WP0002_REQUIRED_CI_TEST_IDS
        )
        if packet_commands != WP0002_REQUIRED_CI_COMMANDS:
            errors.append(
                "WP-0002 packet required gameplay/core/save commands differ from the frozen CI matrix"
            )
    return errors


def validate_wp0002_self_verification_contract(packet: dict) -> list[str]:
    """Pin the A0-authored entry, package, and CI seams outside A1 scope."""
    errors: list[str] = []
    declared_paths = set(packet.get("declared_paths", []))
    writable_controls = declared_paths & set(WP0002_PROTECTED_SELF_VERIFICATION)
    if writable_controls:
        errors.append(
            f"WP-0002 self-verification controls are writable: {sorted(writable_controls)}"
        )
    for relative, expected_hash in WP0002_PROTECTED_SELF_VERIFICATION.items():
        path, path_error = safe_repo_path(relative, "WP-0002 protected control path")
        if path_error or path is None or not path.is_file():
            errors.append(path_error or f"WP-0002 protected control is missing: {relative}")
            continue
        actual_hash = sha256_file(path)
        if actual_hash != expected_hash:
            errors.append(
                f"WP-0002 protected control hash mismatch for {relative}: "
                f"expected {expected_hash}, found {actual_hash}"
            )
    errors.extend(
        validate_wp0002_ci_save_contract(
            REPO_ROOT / ".github" / "workflows" / "wp0002-ci.yml",
            packet,
        )
    )
    entry_tests = [
        test
        for test in packet.get("acceptance_tests", [])
        if isinstance(test, dict) and test.get("id") == "T-GATE-IDENTITY"
    ]
    expected_entry_command = (
        "python3 Tools/Validation/validate_wp0002_entry_gate.py "
        "--packet docs/foundation-v0.1/work-packets/proposed/WP-0002.json "
        "--ratification-state docs/foundation-v0.1/governance/ratification-state.json "
        "--require-claim AUTHORIZE-CITY-COMPARISON "
        "--report BuildArtifacts/WP-0002/entry-gate.json"
    )
    if len(entry_tests) != 1 or entry_tests[0].get("command") != expected_entry_command:
        errors.append("WP-0002 T-GATE-IDENTITY must invoke the protected entry checker exactly")
    return errors


def validate_wp0002_package_graph_contract(packet: dict) -> list[str]:
    """Keep WP-0002 package writes and the future protected-base check exact."""
    errors: list[str] = []
    declared_paths = packet.get("declared_paths", [])
    required_writable = {
        "Game/Packages/manifest.json",
        "Game/Packages/packages-lock.json",
        "SimulationCore/Runtime/LastBearing.meta",
        "SaveContracts/Runtime/LastBearing.meta",
    }
    forbidden_writable = {
        "SimulationCore/package.json",
        "SaveContracts/package.json",
        "Tools/Validation/validate_wp0002_package_graph.py",
        "Tools/Validation/validate_wp0002_entry_gate.py",
        ".github/workflows/wp0002-ci.yml",
    }
    missing = required_writable - set(declared_paths)
    if missing:
        errors.append(f"WP-0002 package graph contract lacks writable paths {sorted(missing)}")
    forbidden = forbidden_writable & set(declared_paths)
    if forbidden:
        errors.append(
            f"WP-0002 protected package/control files must remain read-only, found {sorted(forbidden)}"
        )
    runtime_sibling_meta = {
        path
        for path in declared_paths
        if isinstance(path, str)
        and re.fullmatch(r"(?:SimulationCore|SaveContracts)/Runtime/[^/]+\.meta", path)
    }
    expected_runtime_sibling_meta = {
        "SimulationCore/Runtime/LastBearing.meta",
        "SaveContracts/Runtime/LastBearing.meta",
    }
    if runtime_sibling_meta != expected_runtime_sibling_meta:
        errors.append(
            "WP-0002 may reserve only the exact two LastBearing runtime sibling metadata paths: "
            f"found {sorted(runtime_sibling_meta)}"
        )
    tests = [
        test
        for test in packet.get("acceptance_tests", [])
        if isinstance(test, dict) and test.get("id") == "T-PACKAGE-GRAPH"
    ]
    if len(tests) != 1:
        return errors + ["WP-0002 must define exactly one T-PACKAGE-GRAPH test"]
    test = tests[0]
    expected_command = (
        "python3 Tools/Validation/validate_wp0002_package_graph.py "
        "--base b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5 "
        "--manifest Game/Packages/manifest.json "
        "--lock Game/Packages/packages-lock.json "
        "--simulation-package SimulationCore/package.json "
        "--save-package SaveContracts/package.json "
        "--report BuildArtifacts/WP-0002/package-graph.json"
    )
    expected_oracle = (
        "Against protected base b6b283fd63ab54fed5cd9b6dc6ac78a166cc5bb5, "
        "SimulationCore/package.json and SaveContracts/package.json remain byte-identical; "
        "every pre-existing Game/Packages/manifest.json dependency and "
        "Game/Packages/packages-lock.json entry preserves its exact version, source, URL, "
        "depth, and dependency map; the only manifest additions are "
        "com.ac21.sasha.simulation-core=file:../../SimulationCore and "
        "com.ac21.sasha.save-contracts=file:../../SaveContracts; the only lock additions "
        "have matching file:../../ versions, depth 0, source local, and empty dependencies; "
        "every other graph, source, or version delta fails closed."
    )
    expected_fields = {
        "kind": "security",
        "command": expected_command,
        "oracle": expected_oracle,
        "required": True,
        "scenario_id": None,
    }
    for field, expected in expected_fields.items():
        if test.get(field) != expected:
            errors.append(f"WP-0002 T-PACKAGE-GRAPH {field} must equal {expected!r}")
    required_tests = [
        test
        for test in packet.get("acceptance_tests", [])
        if isinstance(test, dict) and test.get("required") is True
    ]
    required_ids = {test.get("id") for test in required_tests}
    if len(required_tests) != 14:
        errors.append("WP-0002 must retain exactly 14 required acceptance tests for SCN_WP0002_GATE r1")
    if "T-SAVE-PERMUTATIONS" in required_ids:
        errors.append("WP-0002 must consolidate T-SAVE-PERMUTATIONS into T-DEV-SAVE-ATOMIC")
    if any(
        isinstance(test, dict) and test.get("id") == "T-MODULES"
        for test in packet.get("acceptance_tests", [])
    ):
        errors.append("WP-0002 must consolidate T-MODULES into T-PERMUTATIONS")
    permutation_tests = [
        test for test in required_tests if test.get("id") == "T-PERMUTATIONS"
    ]
    required_permutation_fields = {
        "kind": "scenario",
        "command": "python3 Tools/ScenarioRunner/run.py SCN_PREPARATION_MODULE_MATRIX",
        "oracle": (
            "Both modules create distinct preparation, route/action, capacity, and return "
            "consequences; all four preparation/module permutations complete, at least two "
            "remain viable, and at least two returns change the next city decision."
        ),
        "required": True,
        "scenario_id": "SCN_PREPARATION_MODULE_MATRIX",
    }
    if len(permutation_tests) != 1 or any(
        permutation_tests[0].get(field) != expected
        for field, expected in required_permutation_fields.items()
    ):
        errors.append(
            "WP-0002 T-PERMUTATIONS must retain the consolidated module and permutation oracle"
        )
    atomic_tests = [
        test for test in required_tests if test.get("id") == "T-DEV-SAVE-ATOMIC"
    ]
    required_atomic_command = (
        "dotnet run --project Tests/AtomicLandPirate.CoreTests/LastBearing/"
        "AtomicLandPirate.LastBearingTests.csproj --configuration Release "
        "-- --test dev-save-atomic"
    )
    required_atomic_oracle = (
        "The dedicated LastBearing headless test proves exact canonical round-trip at all four "
        "preparation/module checkpoints with no duplicate or lost state, atomic current "
        "publication, recovery through the immediately preceding verified last-good generation "
        "after injected partial/corrupt writes, and refusal of corrupt, partial, or unknown "
        "profile versions without migration, reinterpretation, or rewrite."
    )
    required_atomic_fields = {
        "kind": "unit",
        "command": required_atomic_command,
        "oracle": required_atomic_oracle,
        "required": True,
        "scenario_id": None,
    }
    if len(atomic_tests) != 1 or any(
        atomic_tests[0].get(field) != expected
        for field, expected in required_atomic_fields.items()
    ):
        errors.append(
            "WP-0002 T-DEV-SAVE-ATOMIC must remain the exact direct LastBearing unit contract"
        )
    boundary_tests = [
        test for test in required_tests if test.get("id") == "T-DEV-SAVE-BOUNDARY"
    ]
    required_boundary_fields = {
        "kind": "security",
        "command": (
            "dotnet run --project Tests/AtomicLandPirate.CoreTests/LastBearing/"
            "AtomicLandPirate.LastBearingTests.csproj --configuration Release "
            "-- --test dev-save-boundary"
        ),
        "oracle": (
            "The dedicated LastBearing confinement test proves that the Unity runtime adapter "
            "derives only the fixed last-bearing-dev-v1 child of "
            "UnityEngine.Application.persistentDataPath, SaveContracts discovers only current "
            "and last-good within that child, sibling scans and path traversal fail closed, "
            "presentation exposes no arbitrary root constructor or override, and every "
            "persistent write travels only through the Unity-runtime SaveContracts seam."
        ),
        "required": True,
        "scenario_id": None,
    }
    if len(boundary_tests) != 1 or any(
        boundary_tests[0].get(field) != expected
        for field, expected in required_boundary_fields.items()
    ):
        errors.append(
            "WP-0002 T-DEV-SAVE-BOUNDARY must remain the exact executable confinement contract"
        )
    save_transition_refs = []
    if "SCN_SAVE_TRANSITIONS" in packet.get("save_impact", {}).get(
        "golden_scenarios", []
    ):
        save_transition_refs.append("save_impact.golden_scenarios")
    if any(
        pin.get("id") == "SCN_SAVE_TRANSITIONS"
        for pin in packet.get("scenario_pins", [])
        if isinstance(pin, dict)
    ):
        save_transition_refs.append("scenario_pins")
    if any(
        metric.get("scenario") == "SCN_SAVE_TRANSITIONS"
        for metric in packet.get("performance_metrics", [])
        if isinstance(metric, dict)
    ):
        save_transition_refs.append("performance_metrics")
    if any(
        metric.get("scenario") == "SCN_SAVE_TRANSITIONS"
        for metric in packet.get("rollout", {}).get("health_signals", [])
        if isinstance(metric, dict)
    ):
        save_transition_refs.append("rollout.health_signals")
    if save_transition_refs:
        errors.append(
            f"WP-0002 must not repurpose immutable SCN_SAVE_TRANSITIONS: {save_transition_refs}"
        )
    return errors


def validate_packet_dependencies(
    packets: list[dict],
    state: dict,
    receipts_by_id: dict[str, dict],
) -> list[str]:
    errors: list[str] = []
    packets_by_id = {packet.get("id"): packet for packet in packets}
    requirement_map = state.get("packet_dependency_release_requirements", {})
    if set(requirement_map) != set(packets_by_id):
        errors.append("dependency-release state must name every and only canonical work packet")
    for packet_id, packet in packets_by_id.items():
        dependencies = packet.get("dependencies", [])
        for dependency_id in dependencies:
            if dependency_id not in packets_by_id:
                errors.append(f"{packet_id} depends on unknown packet {dependency_id}")
            if dependency_id == packet_id:
                errors.append(f"{packet_id} depends on itself")
        requirements = requirement_map.get(packet_id, [])
        requirement_ids = [item.get("dependency_id") for item in requirements if isinstance(item, dict)]
        if len(requirement_ids) != len(set(requirement_ids)) or set(requirement_ids) != set(dependencies):
            errors.append(f"{packet_id} dependency receipt requirements do not exactly match dependencies")
        if packet.get("status") in {"proposed", "rejected", "superseded"}:
            continue
        for requirement in requirements:
            dependency_id = requirement.get("dependency_id")
            dependency = packets_by_id.get(dependency_id)
            if dependency is None:
                continue
            if dependency.get("status") != "released":
                errors.append(f"{packet_id} is blocked until {dependency_id} is released")
            receipt = receipts_by_id.get(requirement.get("completion_receipt_id"))
            if not receipt or not receipt.get("sealed"):
                errors.append(f"{packet_id} lacks sealed creator-accepted completion evidence for {dependency_id}")
                continue
            if receipt.get("receipt_kind") != "packet-completion" or receipt.get("issuer_role") != "creator":
                errors.append(f"{packet_id} dependency completion receipt has wrong kind/authority")
            missing = set(requirement.get("required_claims", [])) - subject_claims(receipt).get(dependency_id, set())
            if missing:
                errors.append(f"{packet_id} dependency completion receipt lacks {sorted(missing)}")
            if receipt.get("subject_contract_sha256", {}).get(dependency_id) != dependency.get("contract_sha256"):
                errors.append(f"{packet_id} dependency completion receipt binds the wrong contract")

    visiting: set[str] = set()
    visited: set[str] = set()

    def visit(packet_id: str) -> None:
        if packet_id in visiting:
            errors.append(f"work-packet dependency cycle reaches {packet_id}")
            return
        if packet_id in visited:
            return
        visiting.add(packet_id)
        for dependency_id in packets_by_id[packet_id].get("dependencies", []):
            if dependency_id in packets_by_id:
                visit(dependency_id)
        visiting.remove(packet_id)
        visited.add(packet_id)

    for packet_id in sorted(packets_by_id):
        visit(packet_id)
    return errors


def main() -> int:
    for schema_path in sorted((ROOT / "schemas").glob("*.json")):
        load_json(schema_path)

    records, errors = validate_decisions()
    receipts, receipt_errors = validate_receipts(records)
    errors.extend(receipt_errors)
    errors.extend(validate_supersession_graph(records))
    errors.extend(validate_supersession_authority(records, receipts))
    errors.extend(validate_supersession_fixtures())
    errors.extend(validate_a1_scratch_fixtures())
    errors.extend(validate_a1_runtime_fixtures())
    errors.extend(validate_a1_wp0001_boundary_fixtures())
    errors.extend(
        validate_wp0001_pre_a1_readiness(
            ROOT,
            load_json=load_json,
            validate_schema_subset=validate_schema_subset,
            git_commit_exists=git_commit_exists,
        )
    )
    errors.extend(validate_local_links())
    errors.extend(validate_references(records))
    scenarios, scenario_errors = validate_scenario_registry_v2()
    errors.extend(scenario_errors)
    errors.extend(validate_markdown_scenario_references(scenarios))
    packet_paths = sorted((ROOT / "work-packets").rglob("*.json"))
    packets: list[dict] = []
    for packet_path in packet_paths:
        errors.extend(validate_instance_shape(packet_path, ROOT / "schemas" / "work-packet.schema.json"))
        errors.extend(validate_work_packet_semantics(packet_path))
        errors.extend(validate_packet_scenario_references_v2(packet_path, scenarios))
        packets.append(load_json(packet_path))
    ratification_state = ROOT / "governance" / "ratification-state.json"
    errors.extend(
        validate_instance_shape(
            ratification_state,
            ROOT / "schemas" / "ratification-state.schema.json",
        )
    )
    state = load_json(ratification_state)
    receipts_by_id = {receipt["receipt_id"]: receipt for receipt in receipts}
    errors.extend(validate_packet_dependencies(packets, state, receipts_by_id))
    all_event_ids = [
        event.get("event_id")
        for packet in packets
        for event in packet.get("status_events", [])
        if isinstance(event, dict)
    ]
    if len(all_event_ids) != len(set(all_event_ids)):
        errors.append("work-packet status event IDs must be globally unique")
    a1_limit = state.get("a1_max_active_packets")
    if a1_limit != 1:
        errors.append("ratification state a1_max_active_packets must equal 1")
    active_a1_packets: list[str] = []
    for packet in packets:
        if (
            packet.get("rollout", {}).get("required_autonomy") == "A1"
            and packet.get("status") in {"active", "verifying", "candidate"}
        ):
            active_a1_packets.append(packet.get("id", "unknown-packet"))
    effective_a1_limit = (
        a1_limit
        if isinstance(a1_limit, int) and not isinstance(a1_limit, bool)
        else 0
    )
    if len(active_a1_packets) > effective_a1_limit:
        errors.append(
            f"active A1 packets exceed a1_max_active_packets: {active_a1_packets}"
        )
    if active_a1_packets and state.get("active_autonomy") != "A1":
        errors.append(
            "A1 packets are active while active_autonomy is "
            f"{state.get('active_autonomy')!r}: {active_a1_packets}"
        )
    if state.get("active_autonomy") == "A1" and len(active_a1_packets) != 1:
        errors.append(
            "active_autonomy A1 requires exactly one active A1 packet, found "
            f"{active_a1_packets}"
        )
    if state.get("active_autonomy") == "A1":
        if (
            len(active_a1_packets) != 1
            or state.get("active_a1_packet_id") != active_a1_packets[0]
        ):
            errors.append("active_a1_packet_id must name the sole active A1 packet")
        constitution_hash = sha256_file(ROOT / "00-GAME-CONSTITUTION.md")
        ledger_hash = sha256_file(DECISIONS)
        if state.get("constitution_sha256") != constitution_hash:
            errors.append("A1 state constitution_sha256 does not match current constitution bytes")
        if state.get("decision_ledger_sha256") != ledger_hash:
            errors.append("A1 state decision_ledger_sha256 does not match current ledger bytes")
    elif state.get("active_a1_packet_id") is not None:
        errors.append("active_a1_packet_id must be null outside A1")
    known_decisions = {record["id"] for record in records}
    records_by_id = {record["id"]: record for record in records}
    superseders: dict[str, list[str]] = {}
    for record in records:
        if record.get("supersedes"):
            superseders.setdefault(record["supersedes"], []).append(record["id"])

    def active_decision_head(root_id: str) -> dict | None:
        current = root_id
        visited: set[str] = set()
        while current in superseders:
            if current in visited:
                errors.append(f"decision supersession cycle reaches {current}")
                return None
            visited.add(current)
            children = superseders[current]
            if len(children) != 1:
                errors.append(f"decision lineage {root_id} forks at {current}: {children}")
                return None
            current = children[0]
        return records_by_id.get(current)

    gate_resolutions: dict[str, bool] = {}
    for gate_name, gate in state.get("entry_gates", {}).items():
        gate_resolved = True
        if gate_name == "ugly_gameplay_toy":
            wp0002 = next(
                (packet for packet in packets if packet.get("id") == "WP-0002"),
                {},
            )
            control_errors = validate_wp0002_gate_controls(
                gate,
                wp0002.get("contract_sha256", ""),
            )
            if control_errors:
                errors.extend(control_errors)
                gate_resolved = False
            package_graph_errors = validate_wp0002_package_graph_contract(wp0002)
            if package_graph_errors:
                errors.extend(package_graph_errors)
                gate_resolved = False
            package_graph_worktree_errors = validate_wp0002_package_graph_worktree()
            if package_graph_worktree_errors:
                errors.extend(package_graph_worktree_errors)
                gate_resolved = False
            package_meta_errors = validate_wp0002_package_meta_inventory()
            if package_meta_errors:
                errors.extend(package_meta_errors)
                gate_resolved = False
            self_verification_errors = validate_wp0002_self_verification_contract(wp0002)
            if self_verification_errors:
                errors.extend(self_verification_errors)
                gate_resolved = False
        decision_ids: list[str] = []
        for requirement in gate.get("decision_requirements", []):
            decision_id = requirement.get("decision_id")
            decision_ids.append(decision_id)
            if decision_id not in known_decisions:
                errors.append(f"ratification state {gate_name} references unknown {decision_id}")
                gate_resolved = False
                continue
            head = active_decision_head(decision_id)
            if head is None:
                gate_resolved = False
                continue
            if head.get("status") not in requirement.get("accepted_statuses", []):
                gate_resolved = False
            receipt_id = requirement.get("receipt_id")
            if receipt_id is None:
                gate_resolved = False
                continue
            receipt = receipts_by_id.get(receipt_id)
            if receipt is None:
                errors.append(f"ratification state {gate_name} decision {decision_id} references unknown receipt {receipt_id}")
                gate_resolved = False
                continue
            if head["id"] not in receipt.get("subject_ids", []):
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} does not bind active head {head['id']}"
                )
                gate_resolved = False
            if head.get("approval_receipt_id") != receipt_id:
                errors.append(
                    f"ratification state {gate_name} active head {head['id']} approval receipt "
                    f"{head.get('approval_receipt_id')!r} does not equal {receipt_id}"
                )
                gate_resolved = False
            bindings = subject_claims(receipt)
            matching_claims = set(requirement.get("allowed_claims", [])) & bindings.get(head["id"], set())
            if len(matching_claims) != 1:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} must bind exactly one allowed claim "
                    f"to {head['id']}, found {sorted(matching_claims)}"
                )
                gate_resolved = False
            if not receipt.get("sealed"):
                gate_resolved = False
            required_kind = requirement.get("required_receipt_kind")
            if required_kind is not None and receipt.get("receipt_kind") != required_kind:
                errors.append(
                    f"ratification state {gate_name} decision receipt {receipt_id} has kind "
                    f"{receipt.get('receipt_kind')!r}, expected {required_kind!r}"
                )
                gate_resolved = False
            required_role = requirement.get("required_issuer_role")
            if required_role is not None and receipt.get("issuer_role") != required_role:
                errors.append(
                    f"ratification state {gate_name} decision receipt {receipt_id} has issuer role "
                    f"{receipt.get('issuer_role')!r}, expected {required_role!r}"
                )
                gate_resolved = False
            required_resolver = requirement.get("required_resolver_type")
            resolver = receipt.get("artifact_resolver")
            actual_resolver = resolver.get("type") if isinstance(resolver, dict) else None
            if required_resolver is not None and actual_resolver != required_resolver:
                errors.append(
                    f"ratification state {gate_name} decision receipt {receipt_id} has resolver "
                    f"{actual_resolver!r}, expected {required_resolver!r}"
                )
                gate_resolved = False
            required_contracts = requirement.get("required_subject_contract_sha256", {})
            if not isinstance(required_contracts, dict):
                errors.append(
                    f"ratification state {gate_name} decision requirement has malformed contract bindings"
                )
                gate_resolved = False
            else:
                actual_contracts = receipt.get("subject_contract_sha256")
                if not isinstance(actual_contracts, dict):
                    actual_contracts = {}
                for subject_id, expected_hash in required_contracts.items():
                    actual_hash = actual_contracts.get(subject_id)
                    if actual_hash != expected_hash:
                        errors.append(
                            f"ratification state {gate_name} decision receipt {receipt_id} binds contract "
                            f"{actual_hash!r} for {subject_id}, expected {expected_hash!r}"
                        )
                        gate_resolved = False
        if len(decision_ids) != len(set(decision_ids)):
            errors.append(f"ratification state {gate_name} repeats a decision requirement")
        receipt_purposes: list[str] = []
        for requirement in gate.get("receipt_requirements", []):
            receipt_purposes.append(requirement.get("purpose"))
            receipt_id = requirement.get("receipt_id")
            if receipt_id is None:
                gate_resolved = False
                continue
            receipt = receipts_by_id.get(receipt_id)
            if receipt is None:
                errors.append(f"ratification state {gate_name} references unknown receipt {receipt_id}")
                gate_resolved = False
                continue
            missing_subjects = set(requirement.get("subject_ids", [])) - set(receipt.get("subject_ids", []))
            if missing_subjects:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} lacks subjects {sorted(missing_subjects)}"
                )
                gate_resolved = False
            bindings = subject_claims(receipt)
            for subject_id in requirement.get("subject_ids", []):
                missing_claims = set(requirement.get("required_claims", [])) - bindings.get(subject_id, set())
                if missing_claims:
                    errors.append(
                        f"ratification state {gate_name} receipt {receipt_id} lacks claims "
                        f"{sorted(missing_claims)} for {subject_id}"
                    )
                    gate_resolved = False
            required_kind = requirement.get("required_receipt_kind")
            if required_kind is not None and receipt.get("receipt_kind") != required_kind:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} has kind "
                    f"{receipt.get('receipt_kind')!r}, expected {required_kind!r}"
                )
                gate_resolved = False
            required_role = requirement.get("required_issuer_role")
            if required_role is not None and receipt.get("issuer_role") != required_role:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} has issuer role "
                    f"{receipt.get('issuer_role')!r}, expected {required_role!r}"
                )
                gate_resolved = False
            required_resolver = requirement.get("required_resolver_type")
            resolver = receipt.get("artifact_resolver")
            actual_resolver = resolver.get("type") if isinstance(resolver, dict) else None
            if required_resolver is not None and actual_resolver != required_resolver:
                errors.append(
                    f"ratification state {gate_name} receipt {receipt_id} has resolver "
                    f"{actual_resolver!r}, expected {required_resolver!r}"
                )
                gate_resolved = False
            required_contracts = requirement.get("required_subject_contract_sha256", {})
            if not isinstance(required_contracts, dict):
                errors.append(
                    f"ratification state {gate_name} receipt requirement has malformed contract bindings"
                )
                gate_resolved = False
            else:
                undeclared_contract_subjects = set(required_contracts) - set(requirement.get("subject_ids", []))
                if undeclared_contract_subjects:
                    errors.append(
                        f"ratification state {gate_name} receipt requirement binds undeclared contract "
                        f"subjects {sorted(undeclared_contract_subjects)}"
                    )
                    gate_resolved = False
                actual_contracts = receipt.get("subject_contract_sha256")
                if not isinstance(actual_contracts, dict):
                    actual_contracts = {}
                for subject_id, expected_hash in required_contracts.items():
                    actual_hash = actual_contracts.get(subject_id)
                    if actual_hash != expected_hash:
                        errors.append(
                            f"ratification state {gate_name} receipt {receipt_id} binds contract "
                            f"{actual_hash!r} for {subject_id}, expected {expected_hash!r}"
                        )
                        gate_resolved = False
            if not receipt.get("sealed"):
                gate_resolved = False
        if len(receipt_purposes) != len(set(receipt_purposes)):
            errors.append(f"ratification state {gate_name} repeats a receipt purpose")
        if gate_name == "local_development":
            decision_receipt_ids = {
                requirement.get("receipt_id")
                for requirement in gate.get("decision_requirements", [])
                if requirement.get("receipt_id") is not None
            }
            packet_receipt_ids = {
                requirement.get("receipt_id")
                for requirement in gate.get("receipt_requirements", [])
                if requirement.get("receipt_id") is not None
            }
            duplicate_authority_receipts = decision_receipt_ids & packet_receipt_ids
            if duplicate_authority_receipts:
                errors.append(
                    "ratification state local_development must keep decision, "
                    f"packet-acceptance, and activation receipts distinct: "
                    f"{sorted(duplicate_authority_receipts)}"
                )
                gate_resolved = False
            if len(packet_receipt_ids) != len(
                [
                    requirement
                    for requirement in gate.get("receipt_requirements", [])
                    if requirement.get("receipt_id") is not None
                ]
            ):
                errors.append(
                    "ratification state local_development must keep packet-acceptance "
                    "and activation receipts distinct"
                )
                gate_resolved = False
        if gate.get("status") in {"ready", "passed"} and not gate_resolved:
            errors.append(f"ratification state {gate_name} is {gate.get('status')} with unresolved requirements")
        gate_resolutions[gate_name] = gate_resolved

    packet_ids = [packet.get("id") for packet in packets]
    if len(packet_ids) != len(set(packet_ids)):
        errors.append("work packet IDs are not unique")
    packets_by_id = {
        packet["id"]: packet
        for packet in packets
        if isinstance(packet.get("id"), str)
    }
    a1_packet_ids = {
        packet["id"]
        for packet in packets
        if isinstance(packet.get("id"), str)
        and packet.get("rollout", {}).get("required_autonomy") == "A1"
    }
    packet_entry_gates = state.get("packet_entry_gates")
    entry_gates = state.get("entry_gates", {})
    if not isinstance(packet_entry_gates, dict):
        errors.append("ratification state packet_entry_gates must be an object")
        packet_entry_gates = {}
    for packet_id in sorted(a1_packet_ids):
        mapped_gate = packet_entry_gates.get(packet_id)
        if not isinstance(mapped_gate, str):
            errors.append(f"A1 packet {packet_id} lacks exactly one entry-gate mapping")
        elif mapped_gate not in entry_gates:
            errors.append(f"A1 packet {packet_id} maps to unknown gate {mapped_gate!r}")
    for packet_id in sorted(set(packet_entry_gates) - a1_packet_ids):
        if packet_id not in packets_by_id:
            errors.append(f"packet entry-gate mapping references unknown packet {packet_id}")
        else:
            errors.append(f"non-A1 packet {packet_id} has an A1 entry-gate mapping")

    advanced_statuses = {
        "accepted",
        "active",
        "verifying",
        "candidate",
        "released",
        "rolled-back",
    }
    active_statuses = {"active", "verifying", "candidate"}
    for packet_id in sorted(a1_packet_ids):
        packet = packets_by_id[packet_id]
        mapped_gate = packet_entry_gates.get(packet_id)
        gate = entry_gates.get(mapped_gate, {}) if isinstance(mapped_gate, str) else {}
        receipt_requirements = gate.get("receipt_requirements", [])
        accept_purpose = f"accept-{packet_id}"
        accept_requirements = [
            requirement
            for requirement in receipt_requirements
            if requirement.get("purpose") == accept_purpose
        ]
        if len(accept_requirements) != 1:
            errors.append(
                f"mapped gate for {packet_id} must declare exactly one {accept_purpose} receipt"
            )
            accept_requirement: dict = {}
        else:
            accept_requirement = accept_requirements[0]

        activation_claim = f"ACTIVATE-A1-{packet_id}"
        boundary_claim = (
            "A1-LOCAL-BOUNDARY-VERIFIED"
            if packet_id in {"WP-0002", "WP-0003"}
            else "A1-QUARANTINE-BOUNDARY-VERIFIED"
        )
        quarantine_claims = {boundary_claim, activation_claim}
        if packet_id == "WP-0001":
            quarantine_claims.add("AUTHORIZE-WP0001-MCP-ALLOWLIST")
            quarantine_claims.add("AUTHORIZE-WP0001-RAW-COLLECTORS")
            quarantine_claims.add("AUTHORIZE-WP0001-CODE-IDENTITIES")
        quarantine_requirements = [
            requirement
            for requirement in receipt_requirements
            if activation_claim in requirement.get("required_claims", [])
        ]
        if len(quarantine_requirements) != 1:
            errors.append(
                f"mapped gate for {packet_id} must declare exactly one packet activation receipt"
            )
            quarantine_requirement: dict = {}
        else:
            quarantine_requirement = quarantine_requirements[0]
            if set(quarantine_requirement.get("subject_ids", [])) != {packet_id}:
                errors.append(
                    f"{packet_id} quarantine receipt must bind only that packet"
                )
            if not quarantine_claims.issubset(
                set(quarantine_requirement.get("required_claims", []))
            ):
                errors.append(
                    f"{packet_id} quarantine receipt declaration lacks required activation authority claims"
                )

        quarantine_receipt_id = quarantine_requirement.get("receipt_id")
        errors.extend(
            validate_packet_approval_and_events(
                packet,
                receipts_by_id,
                quarantine_receipt_id,
            )
        )
        boundary_manifest: dict | None = None
        activation_events = [
            event for event in packet.get("status_events", [])
            if isinstance(event, dict) and event.get("to") == "active"
        ]
        if activation_events:
            retained_activation_id = activation_events[0].get("receipt_id")
            retained_activation_receipt = receipts_by_id.get(retained_activation_id)
            boundary_manifest, boundary_errors = validate_a1_boundary_manifest(
                packet,
                state,
                retained_activation_receipt,
                receipts_by_id,
            )
            errors.extend(boundary_errors)

        if packet.get("status") in advanced_statuses:
            acceptance_receipt_id = accept_requirement.get("receipt_id")
            if packet.get("approval_receipt_id") != acceptance_receipt_id:
                errors.append(
                    f"{packet_id} approval receipt must equal its mapped ACCEPT-WP receipt"
                )
            acceptance_receipt = receipts_by_id.get(acceptance_receipt_id)
            if not acceptance_receipt or not acceptance_receipt.get("sealed"):
                errors.append(f"{packet_id} acceptance receipt is missing or unsealed")
            else:
                missing_acceptance_claims = set(
                    accept_requirement.get("required_claims", [])
                ) - subject_claims(acceptance_receipt).get(packet_id, set())
                if missing_acceptance_claims:
                    errors.append(
                        f"{packet_id} acceptance receipt lacks claims "
                        f"{sorted(missing_acceptance_claims)}"
                    )

        if packet.get("status") not in active_statuses:
            continue
        if gate.get("status") != "passed" or not gate_resolutions.get(mapped_gate, False):
            errors.append(
                f"active A1 packet {packet_id} requires a passed, fully resolved mapped gate"
            )
        quarantine_receipt = receipts_by_id.get(quarantine_receipt_id)
        if not quarantine_receipt or not quarantine_receipt.get("sealed"):
            errors.append(f"active A1 packet {packet_id} lacks a sealed quarantine receipt")
        else:
            if quarantine_receipt.get("receipt_kind") != "packet-activation":
                errors.append(f"active A1 packet {packet_id} quarantine receipt has wrong kind")
            if quarantine_receipt.get("issuer_role") != "creator":
                errors.append(f"active A1 packet {packet_id} activation is not creator-issued")
            if quarantine_receipt.get("subject_contract_sha256", {}).get(packet_id) != packet.get("contract_sha256"):
                errors.append(f"active A1 packet {packet_id} activation receipt binds wrong contract")
            missing_quarantine_claims = quarantine_claims - subject_claims(
                quarantine_receipt
            ).get(packet_id, set())
            if missing_quarantine_claims:
                errors.append(
                    f"active A1 packet {packet_id} quarantine receipt lacks claims "
                    f"{sorted(missing_quarantine_claims)}"
                )
        exception_ids = set(
            boundary_manifest.get("local_observation_exceptions", [])
            if isinstance(boundary_manifest, dict)
            else []
        )
        baseline_ids = {
            item.get("id") for item in packet.get("baseline_evidence", [])
            if isinstance(item, dict)
        }
        if not exception_ids.issubset(baseline_ids):
            errors.append(f"active A1 packet {packet_id} boundary names unknown baseline exceptions")
        for artifact in packet.get("baseline_evidence", []):
            if not isinstance(artifact, dict):
                continue
            uri = artifact.get("uri")
            evidence_id = artifact.get("id")
            if isinstance(uri, str) and uri.startswith("pending://"):
                errors.append(f"active A1 packet {packet_id} baseline {evidence_id} remains pending")
            elif artifact_is_content_addressed(artifact):
                continue
            elif not (
                evidence_id in exception_ids
                and isinstance(uri, str)
                and uri.startswith("local-observation://")
            ):
                errors.append(
                    f"active A1 packet {packet_id} baseline {evidence_id} is neither content-addressed "
                    "nor an attested local-observation exception"
                )
        if quarantine_receipt_id == accept_requirement.get("receipt_id"):
            errors.append(
                f"active A1 packet {packet_id} must keep acceptance and quarantine receipts distinct"
            )

        reservation = packet.get("reservation", {})
        if reservation.get("status") != "held":
            errors.append(f"active A1 packet {packet_id} reservation is not held")
        base_commit = reservation.get("base_commit")
        if not isinstance(base_commit, str) or not re.fullmatch(
            r"(?:[0-9a-f]{40}|[0-9a-f]{64})", base_commit
        ):
            errors.append(f"active A1 packet {packet_id} lacks an exact base commit")
        for field in ("lease_id", "fencing_token"):
            if not isinstance(reservation.get(field), str) or not reservation.get(field):
                errors.append(f"active A1 packet {packet_id} lacks {field}")
        reserved_paths = reservation.get("paths", [])
        declared_paths = packet.get("declared_paths", [])
        if len(reserved_paths) != len(set(reserved_paths)) or set(reserved_paths) != set(declared_paths):
            errors.append(
                f"active A1 packet {packet_id} reservation paths must exactly match declared_paths"
            )
        reserved_domains = reservation.get("domains", [])
        declared_domains = packet.get("affected_domains", [])
        if len(reserved_domains) != len(set(reserved_domains)) or set(reserved_domains) != set(declared_domains):
            errors.append(
                f"active A1 packet {packet_id} reservation domains must exactly match affected_domains"
            )

        final_event = packet.get("status_events", [])[-1] if packet.get("status_events") else {}
        if final_event.get("to") != packet.get("status"):
            errors.append(f"active A1 packet {packet_id} final event has the wrong status")
        if final_event.get("receipt_id") != quarantine_receipt_id:
            errors.append(
                f"active A1 packet {packet_id} final event must reference its activation receipt"
            )
        activation_at = parse_datetime(final_event.get("at"))
        expires_at = parse_datetime(reservation.get("expires_at"))
        if expires_at is None:
            errors.append(f"active A1 packet {packet_id} has an invalid reservation expiry")
        else:
            if activation_at is None or expires_at <= activation_at:
                errors.append(
                    f"active A1 packet {packet_id} reservation must expire after activation"
                )
            if expires_at <= datetime.now(timezone.utc):
                errors.append(f"active A1 packet {packet_id} reservation has expired")

    for packet in packets:
        if packet.get("id") not in {"WP-0002", "WP-0003"}:
            continue
        if any(
            isinstance(event, dict) and event.get("to") == "active"
            for event in packet.get("status_events", [])
        ):
            continue
        reference = packet.get("a1_boundary_manifest")
        if not isinstance(reference, dict):
            continue
        manifest_path, path_error = safe_foundation_path(
            reference.get("path"), f"{packet.get('id')} proposed local boundary path"
        )
        if path_error:
            errors.append(path_error)
            continue
        if manifest_path is None or not manifest_path.is_file():
            errors.append(f"{packet.get('id')} proposed local boundary manifest is missing")
            continue
        errors.extend(validate_instance_shape(manifest_path, LOCAL_A1_BOUNDARY_SCHEMA))
        manifest = load_json(manifest_path)
        actual_hash = sha256_file(manifest_path)
        if reference.get("sha256") != actual_hash:
            errors.append(f"{packet.get('id')} proposed local boundary raw hash mismatch")
        if manifest.get("manifest_id") != reference.get("manifest_id"):
            errors.append(f"{packet.get('id')} proposed local boundary ID differs from packet reference")
        if manifest.get("packet_id") != packet.get("id"):
            errors.append(f"{packet.get('id')} proposed local boundary binds another packet")
        if manifest.get("packet_contract_sha256") != packet.get("contract_sha256"):
            errors.append(f"{packet.get('id')} proposed local boundary binds the wrong packet contract")
        if packet.get("id") == "WP-0002":
            expected_local_package_links = {
                "com.ac21.sasha.simulation-core": "file:../../SimulationCore",
                "com.ac21.sasha.save-contracts": "file:../../SaveContracts",
            }
            if manifest.get("local_package_links") != expected_local_package_links:
                errors.append(
                    "WP-0002 proposed local boundary must bind the exact two repository-local UPM links"
                )
            reservation = packet.get("reservation", {})
            expected_reservation = {
                "lease_id": reservation.get("lease_id"),
                "fencing_token": reservation.get("fencing_token"),
                "expires_at": reservation.get("expires_at"),
                "paths": reservation.get("paths"),
                "domains": reservation.get("domains"),
            }
            if manifest.get("reservation") != expected_reservation:
                errors.append("WP-0002 proposed local boundary does not exactly bind its reservation")
            if reservation.get("paths") != packet.get("declared_paths"):
                errors.append("WP-0002 proposed reservation paths do not exactly match declared paths")
            if reservation.get("domains") != packet.get("affected_domains"):
                errors.append("WP-0002 proposed reservation domains do not exactly match affected domains")

    boundary_references = {
        (ROOT / packet["a1_boundary_manifest"]["path"]).resolve()
        for packet in packets
        if isinstance(packet.get("a1_boundary_manifest"), dict)
        and isinstance(packet["a1_boundary_manifest"].get("path"), str)
    }
    boundary_directory = ROOT / "governance" / "a1-boundaries"
    boundary_files = (
        {path.resolve() for path in boundary_directory.glob("*.json")}
        if boundary_directory.is_dir()
        else set()
    )
    if boundary_references != boundary_files:
        errors.append(
            "A1 boundary-manifest closure differs between packet references and canonical files"
        )

    if state.get("trusted_gatekeeper_status") != "passed" and state.get("active_autonomy") not in {"A0", "A1"}:
        errors.append("autonomy above A1 requires a passed trusted gatekeeper")

    if errors:
        print("FOUNDATION BOOTSTRAP LINT: FAIL", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    status_counts: dict[str, int] = {}
    for record in records:
        status_counts[record["status"]] = status_counts.get(record["status"], 0) + 1
    print("FOUNDATION BOOTSTRAP LINT: PASS")
    print(f"decision records: {len(records)}")
    print("statuses: " + ", ".join(f"{key}={status_counts[key]}" for key in sorted(status_counts)))
    print(f"markdown files: {len(list(ROOT.rglob('*.md')))}")
    print(f"schemas: {len(list((ROOT / 'schemas').glob('*.json')))}")
    print(f"registered scenarios: {len(scenarios)}")
    print(f"work packets: {len(packet_paths)}")
    print(f"ratification receipts: {len(receipts)} ({sum(1 for receipt in receipts if not receipt.get('sealed'))} unsealed)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
