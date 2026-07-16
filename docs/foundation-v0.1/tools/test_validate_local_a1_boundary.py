from __future__ import annotations

import copy
import hashlib
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_foundation as foundation


CONTRACT_SHA256 = "ab" * 32
CONSTITUTION_SHA256 = "cd" * 32
LEDGER_SHA256 = "ef" * 32
BASE_COMMIT = "1" * 40
ACTIVATION_COMMIT = "2" * 40


def json_bytes(value: object) -> bytes:
    return json.dumps(value, indent=2, sort_keys=True).encode("utf-8") + b"\n"


class LocalA1BoundaryTests(unittest.TestCase):
    def base_documents(self, root: Path) -> tuple[dict, dict, dict, dict]:
        reservation = {
            "status": "held",
            "lease_id": "lease-local-a1",
            "base_commit": BASE_COMMIT,
            "paths": ["Game/", "SimulationCore/"],
            "domains": ["presentation", "tooling"],
            "expires_at": "2026-07-17T00:00:00Z",
            "fencing_token": "fence-local-a1",
        }
        packet = {
            "id": "WP-0003",
            "contract_sha256": CONTRACT_SHA256,
            "reservation": reservation,
            "a1_boundary_manifest": {
                "manifest_id": "A1B-WP-0003-LOCAL-DEV",
                "path": "governance/a1-boundaries/WP-0003.json",
                "sha256": "",
            },
        }
        state = {
            "constitution_sha256": CONSTITUTION_SHA256,
            "decision_ledger_sha256": LEDGER_SHA256,
            "last_creator_receipt_id": "RR-PRIOR",
        }
        manifest = {
            "schema_version": 1,
            "manifest_id": "A1B-WP-0003-LOCAL-DEV",
            "boundary_mode": "local-development",
            "packet_id": "WP-0003",
            "packet_contract_sha256": CONTRACT_SHA256,
            "created_at": "2026-07-16T23:00:00Z",
            "attested_by": "AC-21",
            "attestation_receipt_id": "RR-WP0003-ACTIVATE",
            "repository": {
                "root": "/Users/sasha/Documents/Codex/sasha-the-land-pirate",
                "remote_url": "https://github.com/AC-21/sasha-the-land-pirate",
                "base_commit": BASE_COMMIT,
                "branch": "agent/wp0003-local",
                "branch_head_commit": ACTIVATION_COMMIT,
                "branch_is_main": False,
                "independent_git_directory": True,
                "shared_worktree": False,
                "clean_at_activation": True,
                "game_project_path": "/Users/sasha/Documents/Codex/sasha-the-land-pirate/Game",
            },
            "reservation": {
                "lease_id": reservation["lease_id"],
                "fencing_token": reservation["fencing_token"],
                "expires_at": reservation["expires_at"],
                "paths": reservation["paths"],
                "domains": reservation["domains"],
            },
            "foundation_binding": {
                "constitution_sha256": CONSTITUTION_SHA256,
                "decision_ledger_sha256": LEDGER_SHA256,
                "last_creator_receipt_id": "RR-PRIOR",
            },
            "unity": {
                "editor_version": "6000.5.4f1",
                "project_path": "/Users/sasha/Documents/Codex/sasha-the-land-pirate/Game",
                "project_state": "bootstrap-pending",
                "mcp_authorization_mode": "conditional-first-use",
                "first_use_preconditions": [
                    "creator-opened-exact-game-project",
                    "creator-confirmed-licensed-editor",
                    "bridge-running",
                    "codex-selected-and-approved",
                    "target-matches-game",
                    "requested-call-within-allowed-actions",
                ],
            },
            "git_safety": {
                "checkpoint_commit": BASE_COMMIT,
                "main_protected": True,
                "pull_requests_required": True,
                "required_checks": [
                    "Cursor Approval Agent: Pull Request Approver",
                    "validate",
                ],
                "force_push_disabled": True,
            },
            "permission_boundary": {
                "allowed_actions": [
                    "edit-declared-repository-paths",
                    "unity-mcp-project-and-object-edits",
                    "unity-mcp-play-mode",
                    "unity-mcp-project-tests",
                    "unity-mcp-console-read",
                    "unity-mcp-screen-capture",
                    "local-nondestructive-validation",
                    "git-commit-push-protected-pr",
                ],
                "denied_actions": [
                    "direct-unity-process-invocation",
                    "dependency-or-tool-install-without-creator",
                    "account-seat-license-billing-purchase-change",
                    "credential-or-secret-access",
                    "publish-release-deploy-monetize",
                    "external-third-party-contact",
                    "git-history-rewrite-or-protection-bypass",
                    "direct-agent-write-outside-repository",
                    "constitutional-governance-credential-dependency-save-release-auto-merge",
                ],
            },
            "credential_boundary": {
                "secrets_committed": False,
                "opaque_git_transport_authorized": True,
                "credential_material_access_authorized": False,
                "release_credentials_available": False,
            },
            "local_observation_exceptions": [
                "BASE-WP0003-MAC",
                "BASE-WP0003-REPOSITORY",
                "BASE-WP0003-UNITY",
            ],
        }
        manifest_path = root / packet["a1_boundary_manifest"]["path"]
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_data = json_bytes(manifest)
        manifest_path.write_bytes(manifest_data)
        manifest_sha256 = hashlib.sha256(manifest_data).hexdigest()
        packet["a1_boundary_manifest"]["sha256"] = manifest_sha256
        activation_receipt = {
            "receipt_id": "RR-WP0003-ACTIVATE",
            "issued_at": "2026-07-16T23:05:00Z",
            "issued_by": "AC-21",
            "issuer_role": "creator",
            "receipt_kind": "packet-activation",
            "artifact_resolver": {
                "type": "external-protected",
                "resolver_reference": "https://github.com/example/receipt",
            },
            "signature_reference": "https://github.com/example/receipt",
            "source_reference": "https://github.com/example/receipt",
            "subject_ids": ["WP-0003"],
            "subject_claims": [
                {
                    "subject_id": "WP-0003",
                    "claims": [
                        "A1-LOCAL-BOUNDARY-VERIFIED",
                        "ACTIVATE-A1-WP-0003",
                    ],
                }
            ],
            "artifact_sha256": {
                packet["a1_boundary_manifest"]["path"]: manifest_sha256
            },
            "approval_text_sha256": "12" * 32,
            "accepted_commit": ACTIVATION_COMMIT,
            "foundation_binding": manifest["foundation_binding"],
            "subject_contract_sha256": {"WP-0003": CONTRACT_SHA256},
            "subject_event_sha256": {},
            "sealed": True,
        }
        return packet, state, manifest, activation_receipt

    def validate(
        self,
        root: Path,
        packet: dict,
        state: dict,
        activation_receipt: dict,
        *,
        commit_exists: bool = True,
        protected_main_ancestor: bool = True,
        activation_descends_from_base: bool = True,
    ) -> list[str]:
        receipts = {
            "RR-PRIOR": {
                "receipt_id": "RR-PRIOR",
                "issuer_role": "creator",
                "sealed": True,
            },
            activation_receipt["receipt_id"]: activation_receipt,
        }
        with (
            mock.patch.object(foundation, "ROOT", root),
            mock.patch.object(
                foundation,
                "LOCAL_A1_BOUNDARY_SCHEMA",
                Path(__file__).resolve().parents[1]
                / "schemas"
                / "local-a1-boundary.schema.json",
            ),
            mock.patch.object(
                foundation,
                "git_commit_exists",
                return_value=commit_exists,
            ),
            mock.patch.object(
                foundation,
                "git_commit_is_ancestor_of_protected_main",
                return_value=protected_main_ancestor,
            ),
            mock.patch.object(
                foundation,
                "git_commit_is_ancestor",
                return_value=activation_descends_from_base,
            ),
        ):
            _, errors = foundation.validate_local_a1_boundary_manifest(
                packet,
                state,
                activation_receipt,
                receipts,
            )
        return errors

    def test_valid_local_boundary_passes(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, state, _, receipt = self.base_documents(root)
            self.assertEqual(self.validate(root, packet, state, receipt), [])

    def test_protected_main_never_falls_back_to_local_main(self) -> None:
        missing_origin = subprocess.CompletedProcess(
            args=[],
            returncode=1,
            stdout=b"",
            stderr=b"",
        )
        runner = mock.Mock(return_value=missing_origin)
        with mock.patch.object(foundation, "run_foundation_git", runner):
            self.assertFalse(
                foundation.git_commit_is_ancestor_of_protected_main(BASE_COMMIT)
            )
        runner.assert_called_once_with(
            [
                "rev-parse",
                "--verify",
                "--quiet",
                "refs/remotes/origin/main",
            ]
        )

    def test_permission_expansion_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, state, manifest, receipt = self.base_documents(root)
            manifest["permission_boundary"]["allowed_actions"].append(
                "publish-release"
            )
            path = root / packet["a1_boundary_manifest"]["path"]
            data = json_bytes(manifest)
            path.write_bytes(data)
            digest = hashlib.sha256(data).hexdigest()
            packet["a1_boundary_manifest"]["sha256"] = digest
            receipt["artifact_sha256"][packet["a1_boundary_manifest"]["path"]] = (
                digest
            )
            errors = self.validate(root, packet, state, receipt)
            self.assertTrue(
                any("permission_boundary.allowed_actions" in error for error in errors),
                errors,
            )

    def test_unbound_manifest_hash_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, state, _, receipt = self.base_documents(root)
            receipt["artifact_sha256"][packet["a1_boundary_manifest"]["path"]] = (
                "00" * 32
            )
            errors = self.validate(root, packet, state, receipt)
            self.assertIn(
                "WP-0003 activation receipt does not bind local boundary bytes",
                errors,
            )

    def test_disguised_protected_branch_name_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, state, manifest, receipt = self.base_documents(root)
            manifest["repository"]["branch"] = "refs/heads/main"
            path = root / packet["a1_boundary_manifest"]["path"]
            data = json_bytes(manifest)
            path.write_bytes(data)
            digest = hashlib.sha256(data).hexdigest()
            packet["a1_boundary_manifest"]["sha256"] = digest
            receipt["artifact_sha256"][packet["a1_boundary_manifest"]["path"]] = (
                digest
            )
            errors = self.validate(root, packet, state, receipt)
            self.assertIn(
                "WP-0003 local boundary branch must be a valid agent/* Git branch",
                errors,
            )

    def test_nonexistent_base_commit_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, state, _, receipt = self.base_documents(root)
            errors = self.validate(
                root,
                packet,
                state,
                receipt,
                commit_exists=False,
            )
            self.assertIn(
                "WP-0003 local boundary base commit does not exist",
                errors,
            )

    def test_base_outside_protected_main_ancestry_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, state, _, receipt = self.base_documents(root)
            errors = self.validate(
                root,
                packet,
                state,
                receipt,
                protected_main_ancestor=False,
            )
            self.assertIn(
                "WP-0003 local boundary base commit is not protected-main ancestry",
                errors,
            )

    def test_activation_commit_must_descend_from_reserved_base(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, state, _, receipt = self.base_documents(root)
            errors = self.validate(
                root,
                packet,
                state,
                receipt,
                activation_descends_from_base=False,
            )
            self.assertIn(
                "WP-0003 local activation receipt commit does not descend from its base",
                errors,
            )

    def test_activation_receipt_cannot_be_its_own_prior_authority(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, state, manifest, receipt = self.base_documents(root)
            state["last_creator_receipt_id"] = receipt["receipt_id"]
            manifest["foundation_binding"]["last_creator_receipt_id"] = receipt[
                "receipt_id"
            ]
            receipt["foundation_binding"] = manifest["foundation_binding"]
            path = root / packet["a1_boundary_manifest"]["path"]
            data = json_bytes(manifest)
            path.write_bytes(data)
            digest = hashlib.sha256(data).hexdigest()
            packet["a1_boundary_manifest"]["sha256"] = digest
            receipt["artifact_sha256"][packet["a1_boundary_manifest"]["path"]] = (
                digest
            )
            errors = self.validate(root, packet, state, receipt)
            self.assertIn(
                "WP-0003 local activation receipt cannot self-bind as prior creator receipt",
                errors,
            )


if __name__ == "__main__":
    unittest.main()
