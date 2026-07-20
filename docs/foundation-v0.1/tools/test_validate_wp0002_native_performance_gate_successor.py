from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_foundation as foundation


PACKET_CONTRACT_SHA256 = foundation.WP0002_NATIVE_GATE_PACKET_CONTRACT_SHA256
STAGE1_PATHS = tuple(foundation.WP0002_NATIVE_GATE_STAGE1_PATHS)


def json_bytes(value: object) -> bytes:
    return json.dumps(value, indent=2, sort_keys=True).encode("utf-8") + b"\n"


def load_documents() -> tuple[dict, dict]:
    packet = foundation.load_json(
        foundation.ROOT / "work-packets" / "proposed" / "WP-0002.json"
    )
    manifest = foundation.load_json(
        foundation.ROOT / "governance" / "a1-boundaries" / "WP-0002.json"
    )
    return packet, manifest


def synthetic_receipt(
    repository: object,
    packet: dict,
    accepted_commit: str,
    stage1: dict[str, object],
    stage1_delta: list[dict[str, object]],
    artifact_hashes: dict[str, str],
) -> dict:
    source_reference = (
        "https://github.com/AC-21/sasha-the-land-pirate/"
        "pull/101#issuecomment-20260719"
    )
    binding = foundation._wp0002_native_gate_expected_authority_binding(
        repository,
        packet,
        accepted_commit,
        stage1,
        stage1_delta,
        101,
    )
    if binding is None:
        raise AssertionError("synthetic authority binding was not produced")
    approval_sha256 = foundation.sha256_canonical_json(binding)
    return {
        "receipt_id": foundation.WP0002_NATIVE_GATE_RECEIPT_ID,
        "issued_at": "2026-07-19T12:00:00Z",
        "issued_by": "AC-21",
        "issuer_role": "creator",
        "receipt_kind": "creator-authorization",
        "artifact_resolver": {
            "type": "external-protected",
            "resolver_reference": source_reference,
        },
        "source_reference": source_reference,
        "signature_reference": source_reference,
        "subject_ids": [
            foundation.WP0002_NATIVE_GATE_SUPERSESSION_SUBJECT,
            "WP-0002",
        ],
        "subject_claims": [
            {
                "subject_id": foundation.WP0002_NATIVE_GATE_SUPERSESSION_SUBJECT,
                "claims": [foundation.WP0002_NATIVE_GATE_SUPERSESSION_CLAIM],
            },
            {
                "subject_id": "WP-0002",
                "claims": [foundation.WP0002_NATIVE_GATE_CLAIM],
            },
        ],
        "approval_text_sha256": approval_sha256,
        "accepted_commit": accepted_commit,
        "materialization_authority_binding": binding,
        "artifact_sha256": {
            **artifact_hashes,
            source_reference: approval_sha256,
        },
        "subject_contract_sha256": {
            foundation.WP0002_NATIVE_GATE_SUPERSESSION_SUBJECT: (
                foundation.WP0002_NATIVE_GATE_PREVIOUS_BOUNDARY_SHA256
            ),
            "WP-0002": packet["contract_sha256"],
        },
        "subject_event_sha256": {},
        "foundation_binding": None,
        "sealed": True,
    }


class NativePerformanceGateSuccessorTests(unittest.TestCase):
    def test_pending_stage1_is_structurally_valid_and_fail_closed(self) -> None:
        packet, manifest = load_documents()
        self.assertEqual(
            foundation.validate_wp0002_native_performance_gate_successor(
                packet,
                manifest,
                {},
            ),
            [],
        )
        self.assertEqual(
            manifest["native_performance_gate_successor"]["authorization_state"],
            "receipt-required-fail-closed",
        )

    def test_partial_or_malformed_receipt_fails_closed(self) -> None:
        packet, manifest = load_documents()
        partial = {
            "receipt_id": foundation.WP0002_NATIVE_GATE_RECEIPT_ID,
            "issued_by": "AC-21",
            "sealed": True,
        }
        with mock.patch.object(
            foundation,
            "validate_wp0002_native_performance_gate_receipt",
            return_value=[],
        ):
            errors = (
                foundation.validate_wp0002_native_performance_gate_successor(
                    packet,
                    manifest,
                    {foundation.WP0002_NATIVE_GATE_RECEIPT_ID: partial},
                )
            )
        self.assertIn(
            "WP-0002 native performance gate receipt lacks exact protected "
            "creator authority",
            errors,
        )

    def test_exact_receipt_only_stage2_validates(self) -> None:
        packet, manifest = load_documents()
        base = foundation.WP0002_NATIVE_GATE_STAGE1_BASE
        accepted_commit = "a" * 40
        stage2_commit = "b" * 40
        merge_commit = "e" * 40
        stage1_tree = "c" * 40
        stage1_blobs = {
            path: f"native-stage1:{path}\n".encode("utf-8")
            for path in STAGE1_PATHS
        }
        stage1_delta = [
            {
                "path": path,
                "status": "A",
                "new_mode": "100644",
                "new_blob_sha256": hashlib.sha256(blob).hexdigest(),
            }
            for path, blob in stage1_blobs.items()
        ]
        receipt_delta = [
            {
                "path": foundation.WP0002_NATIVE_GATE_RECEIPT_REPO_PATH,
                "status": "A",
                "new_mode": "100644",
            }
        ]
        receipt_bytes_holder: list[bytes] = []

        class Repository:
            def __init__(self, _root: Path) -> None:
                pass

            def commit(self, commit: str) -> dict[str, object]:
                if commit == accepted_commit:
                    return {"parents": [base], "tree": stage1_tree}
                if commit == stage2_commit:
                    return {"parents": [accepted_commit], "tree": "d" * 40}
                if commit == merge_commit:
                    return {
                        "parents": [base, stage2_commit],
                        "tree": "d" * 40,
                    }
                raise ValueError(commit)

            def changed_files(
                self,
                parent: str,
                child: str,
            ) -> list[dict[str, object]]:
                if (parent, child) == (base, accepted_commit):
                    return copy.deepcopy(stage1_delta)
                if (parent, child) == (accepted_commit, stage2_commit):
                    return copy.deepcopy(receipt_delta)
                raise ValueError((parent, child))

            def blob_at(self, commit: str, path: str) -> bytes | None:
                if commit == base:
                    return None
                if commit == accepted_commit:
                    return stage1_blobs.get(path)
                if (
                    commit == stage2_commit
                    and path == foundation.WP0002_NATIVE_GATE_RECEIPT_REPO_PATH
                ):
                    return receipt_bytes_holder[0]
                return None

            def deterministic_patch(self, parent: str, child: str) -> bytes:
                if (parent, child) != (base, accepted_commit):
                    raise ValueError((parent, child))
                return b"exact native Stage-1 patch\n"

        repository = Repository(Path("."))
        artifact_hashes = {
            path: hashlib.sha256(blob).hexdigest()
            for path, blob in stage1_blobs.items()
        }
        receipt = synthetic_receipt(
            repository,
            packet,
            accepted_commit,
            repository.commit(accepted_commit),
            stage1_delta,
            artifact_hashes,
        )
        receipt_bytes_holder.append(json_bytes(receipt))

        def resolve(reference: str) -> str | None:
            if reference == "refs/remotes/origin/main":
                return base
            if reference == "HEAD":
                return merge_commit
            return None

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            receipt_path = root / foundation.WP0002_NATIVE_GATE_RECEIPT_PATH
            receipt_path.parent.mkdir(parents=True, exist_ok=True)
            receipt_path.write_bytes(receipt_bytes_holder[0])
            with (
                mock.patch.object(foundation, "ROOT", root),
                mock.patch.object(foundation, "REPO_ROOT", root),
                mock.patch.object(
                    foundation,
                    "_load_wp0002_transaction_verifier",
                    return_value=({"GitRepository": Repository}, []),
                ),
                mock.patch.object(
                    foundation,
                    "git_rev_parse_commit",
                    side_effect=resolve,
                ),
            ):
                self.assertEqual(
                    foundation.validate_wp0002_native_performance_gate_receipt(
                        packet,
                        receipt,
                    ),
                    [],
                )
        with mock.patch.object(
            foundation,
            "validate_wp0002_native_performance_gate_receipt",
            return_value=[],
        ):
            self.assertEqual(
                foundation.validate_wp0002_native_performance_gate_successor(
                    packet,
                    manifest,
                    {foundation.WP0002_NATIVE_GATE_RECEIPT_ID: receipt},
                ),
                [],
            )

    def test_receipt_artifact_set_and_creator_claim_are_exact(self) -> None:
        packet, manifest = load_documents()
        receipt = {
            "receipt_id": foundation.WP0002_NATIVE_GATE_RECEIPT_ID,
            "issued_by": "AC-21",
            "issuer_role": "creator",
            "receipt_kind": "creator-authorization",
            "sealed": True,
            "subject_ids": [
                foundation.WP0002_NATIVE_GATE_SUPERSESSION_SUBJECT,
                "WP-0002",
            ],
            "subject_claims": [
                {
                    "subject_id": foundation.WP0002_NATIVE_GATE_SUPERSESSION_SUBJECT,
                    "claims": [foundation.WP0002_NATIVE_GATE_SUPERSESSION_CLAIM],
                },
                {
                    "subject_id": "WP-0002",
                    "claims": ["AUTHORIZE-ARBITRARY-PROCESS"],
                },
            ],
            "subject_contract_sha256": {
                foundation.WP0002_NATIVE_GATE_SUPERSESSION_SUBJECT: (
                    foundation.WP0002_NATIVE_GATE_PREVIOUS_BOUNDARY_SHA256
                ),
                "WP-0002": packet["contract_sha256"],
            },
            "subject_event_sha256": {},
            "source_reference": (
                "https://github.com/AC-21/sasha-the-land-pirate/"
                "pull/101#issuecomment-20260719"
            ),
            "signature_reference": (
                "https://github.com/AC-21/sasha-the-land-pirate/"
                "pull/101#issuecomment-20260719"
            ),
            "artifact_resolver": {
                "type": "external-protected",
                "resolver_reference": (
                    "https://github.com/AC-21/sasha-the-land-pirate/"
                    "pull/101#issuecomment-20260719"
                ),
            },
        }
        with mock.patch.object(
            foundation,
            "validate_wp0002_native_performance_gate_receipt",
            return_value=[
                "WP-0002 native performance gate receipt artifact set is not exact"
            ],
        ):
            errors = (
                foundation.validate_wp0002_native_performance_gate_successor(
                    packet,
                    manifest,
                    {foundation.WP0002_NATIVE_GATE_RECEIPT_ID: receipt},
                )
            )
        self.assertTrue(any("creator authority" in error for error in errors))
        self.assertTrue(any("artifact set" in error for error in errors))

    def test_contract_boundary_hash_and_fixed_protocol_reject_drift(self) -> None:
        packet, manifest = load_documents()
        mutated_packet = copy.deepcopy(packet)
        mutated_packet["contract_sha256"] = "00" * 32
        self.assertIn(
            "WP-0002 native performance gate successor changes the packet contract",
            foundation.validate_wp0002_native_performance_gate_successor(
                mutated_packet,
                manifest,
                {},
            ),
        )

        schema = foundation.load_json(foundation.LOCAL_A1_BOUNDARY_SCHEMA)
        mutations = [
            lambda item: item["native_performance_gate_successor"].__setitem__(
                "previous_boundary_sha256", "00" * 32
            ),
            lambda item: item["native_performance_gate_successor"].__setitem__(
                "authorized_dispatcher_sha256", "00" * 32
            ),
            lambda item: item["native_performance_gate_successor"][
                "toolchain"
            ].__setitem__("architecture", "universal"),
            lambda item: item["native_performance_gate_successor"][
                "protocol"
            ].__setitem__("runtime_identity_claim_scope", "all-source-authentic"),
            lambda item: item["native_performance_gate_successor"][
                "protocol"
            ].__setitem__("future_full_v0_measurement_seconds", 1800),
            lambda item: item["native_performance_gate_successor"][
                "protocol"
            ].__setitem__("representative_v0_claim_allowed", True),
            lambda item: item["native_performance_gate_successor"].__setitem__(
                "output_root", "/tmp/arbitrary"
            ),
            lambda item: item["native_performance_gate_successor"][
                "gate_ids"
            ].append("arbitrary-process-and-arguments"),
            lambda item: item["boundary_amendments"][-1][
                "materialization_control"
            ].__setitem__("new_gates_may_validate_their_own_control_pr", True),
            lambda item: item["unity_runcommand_residual_capability"][
                "dispatcher"
            ]["allowed_gate_ids"].append("arbitrary-editor-command"),
        ]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                candidate = copy.deepcopy(manifest)
                mutate(candidate)
                errors = foundation.validate_schema_subset(
                    candidate,
                    schema,
                    schema,
                    "manifest",
                )
                self.assertTrue(errors)

    def test_protected_receipt_requires_one_first_parent_introduction(self) -> None:
        packet, _ = load_documents()
        origin_main = "d" * 40
        receipt_bytes = b"{}\n"

        class Repository:
            def __init__(self, _root: Path) -> None:
                pass

            def blob_at(self, commit: str, path: str) -> bytes | None:
                if (
                    commit == origin_main
                    and path == foundation.WP0002_NATIVE_GATE_RECEIPT_REPO_PATH
                ):
                    return receipt_bytes
                return None

        receipt = {"accepted_commit": "a" * 40}
        with (
            mock.patch.object(
                foundation,
                "_load_wp0002_transaction_verifier",
                return_value=({"GitRepository": Repository}, []),
            ),
            mock.patch.object(
                foundation,
                "git_rev_parse_commit",
                return_value=origin_main,
            ),
            mock.patch.object(
                foundation,
                "git_first_parent_path_additions",
                return_value=["b" * 40, "c" * 40],
            ),
        ):
            self.assertEqual(
                foundation.validate_wp0002_native_performance_gate_git_materialization(
                    packet,
                    receipt,
                    receipt_bytes,
                ),
                [
                    "WP-0002 native performance gate receipt has no unique "
                    "protected-main first-parent introduction"
                ],
            )


if __name__ == "__main__":
    unittest.main()
