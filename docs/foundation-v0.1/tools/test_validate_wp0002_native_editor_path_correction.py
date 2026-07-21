from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_foundation as foundation


PATHS = tuple(foundation.WP0002_NATIVE_PATH_FIX_STAGE1_PATHS)
ADDED = {
    "docs/foundation-v0.1/governance/"
    "WP-0002-NATIVE-PLAYER-EXECUTABLE-PATH-CORRECTION-20260720.md",
}


def json_bytes(value: object) -> bytes:
    return json.dumps(value, indent=2, sort_keys=True).encode("utf-8") + b"\n"


def documents() -> tuple[dict, dict]:
    packet = foundation.load_json(
        foundation.ROOT / "work-packets" / "proposed" / "WP-0002.json"
    )
    boundary = foundation.load_json(
        foundation.ROOT / "governance" / "a1-boundaries" / "WP-0002.json"
    )
    return packet, boundary


class SyntheticRepository:
    def __init__(
        self,
        stage1_blobs: dict[str, bytes],
        previous_receipt: bytes,
        receipt_holder: list[bytes],
        *,
        wrong_mode: bool = False,
    ) -> None:
        self.stage1_blobs = stage1_blobs
        self.previous_receipt = previous_receipt
        self.receipt_holder = receipt_holder
        self.wrong_mode = wrong_mode

    def commit(self, commit: str) -> dict[str, object]:
        if commit == "a" * 40:
            return {
                "parents": [foundation.WP0002_NATIVE_PATH_FIX_STAGE1_BASE],
                "tree": "c" * 40,
            }
        if commit == "b" * 40:
            return {"parents": ["a" * 40], "tree": "d" * 40}
        raise ValueError(commit)

    def changed_files(
        self,
        parent: str,
        child: str,
    ) -> list[dict[str, object]]:
        if (
            parent == foundation.WP0002_NATIVE_PATH_FIX_STAGE1_BASE
            and child == "a" * 40
        ):
            result = []
            for path in PATHS:
                result.append(
                    {
                        "path": path,
                        "status": "A" if path in ADDED else "M",
                        "new_mode": (
                            "100755"
                            if self.wrong_mode and path == PATHS[0]
                            else "100644"
                        ),
                        "new_blob_sha256": hashlib.sha256(
                            self.stage1_blobs[path]
                        ).hexdigest(),
                    }
                )
            return result
        if parent == "a" * 40 and child == "b" * 40:
            return [
                {
                    "path": foundation.WP0002_NATIVE_PATH_FIX_RECEIPT_REPO_PATH,
                    "status": "A",
                    "new_mode": "100644",
                }
            ]
        raise ValueError((parent, child))

    def blob_at(self, commit: str, path: str) -> bytes | None:
        predecessor = (
            "docs/foundation-v0.1/" +
            foundation.WP0002_NATIVE_PATH_FIX_PREVIOUS_RECEIPT_PATH
        )
        if path == predecessor and commit in {
            foundation.WP0002_NATIVE_PATH_FIX_STAGE1_BASE,
            "a" * 40,
            "b" * 40,
        }:
            return self.previous_receipt
        if commit == "a" * 40:
            return self.stage1_blobs.get(path)
        if (
            commit == "b" * 40
            and path == foundation.WP0002_NATIVE_PATH_FIX_RECEIPT_REPO_PATH
        ):
            return self.receipt_holder[0]
        return None

    def deterministic_patch(self, parent: str, child: str) -> bytes:
        if (
            parent != foundation.WP0002_NATIVE_PATH_FIX_STAGE1_BASE
            or child != "a" * 40
        ):
            raise ValueError((parent, child))
        return b"exact native player executable-path correction Stage-1 patch\n"


def synthetic_receipt(
    packet: dict,
    repository: SyntheticRepository,
    stage1_blobs: dict[str, bytes],
    previous_receipt: bytes,
) -> dict:
    changed_files = repository.changed_files(
        foundation.WP0002_NATIVE_PATH_FIX_STAGE1_BASE,
        "a" * 40,
    )
    binding = foundation._wp0002_native_path_fix_expected_authority_binding(
        repository,
        packet,
        "a" * 40,
        repository.commit("a" * 40),
        changed_files,
        101,
    )
    if binding is None:
        raise AssertionError("authority binding was not produced")
    source = (
        "https://github.com/AC-21/sasha-the-land-pirate/"
        "pull/101#issuecomment-20260719"
    )
    approval = foundation.sha256_canonical_json(binding)
    artifacts = {
        path: hashlib.sha256(payload).hexdigest()
        for path, payload in stage1_blobs.items()
    }
    artifacts[
        "docs/foundation-v0.1/" +
        foundation.WP0002_NATIVE_PATH_FIX_PREVIOUS_RECEIPT_PATH
    ] = hashlib.sha256(previous_receipt).hexdigest()
    artifacts[
        foundation.WP0002_NATIVE_PATH_FIX_RETAINED_BUILD_PROFILE_PATH
    ] = foundation.WP0002_NATIVE_PATH_FIX_RETAINED_BUILD_PROFILE_SHA256
    artifacts[source] = approval
    return {
        "receipt_id": foundation.WP0002_NATIVE_PATH_FIX_RECEIPT_ID,
        "issued_at": "2026-07-20T02:00:00Z",
        "issued_by": "AC-21",
        "issuer_role": "creator",
        "receipt_kind": "creator-authorization",
        "artifact_resolver": {
            "type": "external-protected",
            "resolver_reference": source,
        },
        "source_reference": source,
        "signature_reference": source,
        "subject_ids": [
            foundation.WP0002_NATIVE_PATH_FIX_SUPERSESSION_SUBJECT,
            "WP-0002",
        ],
        "subject_claims": [
            {
                "subject_id": (
                    foundation.WP0002_NATIVE_PATH_FIX_SUPERSESSION_SUBJECT
                ),
                "claims": [
                    foundation.WP0002_NATIVE_PATH_FIX_SUPERSESSION_CLAIM
                ],
            },
            {
                "subject_id": "WP-0002",
                "claims": [foundation.WP0002_NATIVE_PATH_FIX_CLAIM],
            },
        ],
        "approval_text_sha256": approval,
        "accepted_commit": "a" * 40,
        "materialization_authority_binding": binding,
        "artifact_sha256": artifacts,
        "subject_contract_sha256": {
            foundation.WP0002_NATIVE_PATH_FIX_SUPERSESSION_SUBJECT: (
                foundation.WP0002_NATIVE_PATH_FIX_PREVIOUS_DISPATCHER_SHA256
            ),
            "WP-0002": packet["contract_sha256"],
        },
        "subject_event_sha256": {},
        "foundation_binding": None,
        "sealed": True,
    }


class NativeEditorPathCorrectionTests(unittest.TestCase):
    def test_predecessor_receipt_absence_cannot_skip_correction(self) -> None:
        packet, boundary = documents()
        with mock.patch.object(
            foundation,
            "_wp0002_native_path_fix_previous_receipt_bytes",
            return_value=None,
        ):
            errors = foundation.validate_wp0002_native_performance_gate_successor(
                packet,
                boundary,
                {},
            )
        self.assertIn(
            "WP-0002 native Editor path correction predecessor receipt drifted",
            errors,
        )

    def test_receipt_absent_stage1_is_exact_and_fail_closed(self) -> None:
        packet, boundary = documents()
        self.assertEqual(
            foundation.validate_wp0002_native_editor_path_correction(
                packet,
                boundary,
                {},
            ),
            [],
        )
        correction = boundary["boundary_amendments"][-1]
        self.assertEqual(
            correction["amendment_id"],
            foundation.WP0002_NATIVE_PATH_FIX_AMENDMENT_ID,
        )
        self.assertFalse(correction["authority_expansion"])

    def test_projection_rejects_path_or_authority_widening(self) -> None:
        packet, boundary = documents()
        for field, value in (
            ("editor_application_bundle_path", "/Applications/Unity.app"),
            ("editor_executable_relative_path", "../Unity"),
            ("authority_expansion", True),
            ("gate_surface_changed", True),
        ):
            changed = copy.deepcopy(boundary)
            changed["native_performance_gate_successor"][
                "editor_path_correction"
            ][field] = value
            errors = foundation.validate_wp0002_native_editor_path_correction(
                packet,
                changed,
                {},
            )
            self.assertIn(
                "WP-0002 native Editor path correction projection is not exact",
                errors,
            )

    def test_player_executable_projection_is_one_exact_path(self) -> None:
        packet, boundary = documents()
        correction = boundary["boundary_amendments"][-1]
        self.assertEqual(
            correction["previous_player_executable_relative_path"],
            "SashaAtomicLandPirateVGR13.app/Contents/MacOS/"
            "Sasha the Atomic Land Pirate",
        )
        self.assertEqual(
            correction["player_executable_relative_path"],
            "SashaAtomicLandPirateVGR13.app/Contents/MacOS/Game",
        )
        self.assertEqual(correction["accepted_player_executable_path_count"], 1)
        self.assertFalse(correction["fallback_player_executable_paths_allowed"])
        self.assertFalse(correction["hash_or_signature_checks_changed"])

        for field, value in (
            (
                "player_executable_relative_path",
                "SashaAtomicLandPirateVGR13.app/Contents/MacOS/"
                "Sasha the Atomic Land Pirate",
            ),
            ("accepted_player_executable_path_count", 2),
            ("fallback_player_executable_paths_allowed", True),
            ("hash_or_signature_checks_changed", True),
            ("build_profile_changed", True),
            ("project_settings_changed", True),
            ("gate_surface_changed", True),
            ("authority_expansion", True),
        ):
            changed = copy.deepcopy(boundary)
            changed["boundary_amendments"][-1][field] = value
            errors = foundation.validate_wp0002_native_editor_path_correction(
                packet,
                changed,
                {},
            )
            self.assertIn(
                "WP-0002 native Editor path correction amendment is not exact",
                errors,
            )

    def test_partial_receipt_lacks_exact_creator_authority(self) -> None:
        packet, boundary = documents()
        partial = {
            "receipt_id": foundation.WP0002_NATIVE_PATH_FIX_RECEIPT_ID,
            "issued_by": "AC-21",
            "sealed": True,
        }
        with mock.patch.object(
            foundation,
            "validate_wp0002_native_editor_path_correction_receipt",
            return_value=[],
        ):
            errors = foundation.validate_wp0002_native_editor_path_correction(
                packet,
                boundary,
                {foundation.WP0002_NATIVE_PATH_FIX_RECEIPT_ID: partial},
            )
        self.assertIn(
            "WP-0002 native Editor path correction receipt lacks exact "
            "protected creator authority",
            errors,
        )

    def test_exact_receipt_only_stage2_validates(self) -> None:
        packet, _ = documents()
        stage1_blobs = {
            path: ("player-executable-path-fix:" + path + "\n").encode("utf-8")
            for path in PATHS
        }
        previous = b"sealed predecessor receipt\n"
        holder: list[bytes] = []
        repository = SyntheticRepository(stage1_blobs, previous, holder)
        receipt = synthetic_receipt(packet, repository, stage1_blobs, previous)
        self.assertEqual(
            receipt["artifact_sha256"][
                foundation.WP0002_NATIVE_PATH_FIX_RETAINED_BUILD_PROFILE_PATH
            ],
            foundation.WP0002_NATIVE_PATH_FIX_RETAINED_BUILD_PROFILE_SHA256,
        )
        with tempfile.TemporaryDirectory() as temporary:
            receipt_path = Path(temporary) / "receipt.json"
            receipt_path.write_bytes(json_bytes(receipt))
            self.assertEqual(
                foundation.validate_instance_shape(
                    receipt_path,
                    foundation.ROOT
                    / "schemas"
                    / "ratification-receipt.schema.json",
                ),
                [],
            )
        holder.append(json_bytes(receipt))

        def resolve(reference: str) -> str | None:
            if reference == "refs/remotes/origin/main":
                return foundation.WP0002_NATIVE_PATH_FIX_STAGE1_BASE
            if reference == "HEAD":
                return "b" * 40
            return None

        with (
            mock.patch.object(
                foundation,
                "_load_wp0002_transaction_verifier",
                return_value=({"GitRepository": lambda _root: repository}, []),
            ),
            mock.patch.object(
                foundation,
                "_wp0002_native_path_fix_previous_receipt_bytes",
                return_value=previous,
            ),
            mock.patch.object(
                foundation,
                "git_rev_parse_commit",
                side_effect=resolve,
            ),
        ):
            self.assertEqual(
                foundation.validate_wp0002_native_editor_path_correction_git_materialization(
                    packet,
                    receipt,
                    holder[0],
                ),
                [],
            )

    def test_receipt_retains_exact_unchanged_build_profile(self) -> None:
        packet, _ = documents()
        stage1_blobs = {
            path: ("player-executable-path-fix:" + path + "\n").encode("utf-8")
            for path in PATHS
        }
        previous = b"sealed predecessor receipt\n"
        holder: list[bytes] = []
        repository = SyntheticRepository(stage1_blobs, previous, holder)
        receipt = synthetic_receipt(packet, repository, stage1_blobs, previous)
        predecessor_key = (
            "docs/foundation-v0.1/" +
            foundation.WP0002_NATIVE_PATH_FIX_PREVIOUS_RECEIPT_PATH
        )
        receipt["artifact_sha256"][predecessor_key] = (
            foundation.WP0002_NATIVE_PATH_FIX_PREVIOUS_RECEIPT_SHA256
        )

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            receipt_path = (
                root / foundation.WP0002_NATIVE_PATH_FIX_RECEIPT_PATH
            )
            receipt_path.parent.mkdir(parents=True)
            receipt_path.write_bytes(json_bytes(receipt))
            with (
                mock.patch.object(foundation, "ROOT", root),
                mock.patch.object(
                    foundation,
                    "validate_wp0002_native_editor_path_correction_git_materialization",
                    return_value=[],
                ),
            ):
                self.assertEqual(
                    foundation.validate_wp0002_native_editor_path_correction_receipt(
                        packet,
                        receipt,
                    ),
                    [],
                )

                receipt["artifact_sha256"][
                    foundation.WP0002_NATIVE_PATH_FIX_RETAINED_BUILD_PROFILE_PATH
                ] = "0" * 64
                receipt_path.write_bytes(json_bytes(receipt))
                errors = (
                    foundation.validate_wp0002_native_editor_path_correction_receipt(
                        packet,
                        receipt,
                    )
                )
        self.assertIn(
            "WP-0002 native Editor path correction does not bind its unchanged "
            "build profile",
            errors,
        )

    def test_stage1_wrong_mode_and_predecessor_drift_fail_closed(self) -> None:
        packet, _ = documents()
        stage1_blobs = {
            path: ("player-executable-path-fix:" + path + "\n").encode("utf-8")
            for path in PATHS
        }
        previous = b"sealed predecessor receipt\n"
        holder: list[bytes] = []
        repository = SyntheticRepository(
            stage1_blobs,
            previous,
            holder,
            wrong_mode=True,
        )
        receipt = synthetic_receipt(packet, repository, stage1_blobs, previous)
        errors = foundation._validate_wp0002_native_path_fix_stage1(
            repository,
            packet,
            receipt,
            "a" * 40,
            previous,
        )
        self.assertTrue(any("mode or status differs" in error for error in errors))

        with mock.patch.object(
            foundation,
            "_wp0002_native_path_fix_previous_receipt_bytes",
            return_value=None,
        ):
            errors = (
                foundation.validate_wp0002_native_editor_path_correction_git_materialization(
                    packet,
                    receipt,
                    json_bytes(receipt),
                )
            )
        self.assertIn(
            "WP-0002 native Editor path correction predecessor receipt drifted",
            errors,
        )


if __name__ == "__main__":
    unittest.main()
