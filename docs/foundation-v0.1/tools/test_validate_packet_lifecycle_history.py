from __future__ import annotations

import copy
import json
import tempfile
import unittest
from pathlib import Path

import validate_foundation as foundation


CONTRACT = "a" * 64
ACCEPTANCE_ID = "RR-ACCEPT"
ACTIVATION_ID = "RR-ACTIVATE"
COMPLETION_ID = "RR-COMPLETE"


def event(to_status: str, from_status: str | None, receipt_id: str | None) -> dict:
    return {
        "event_id": f"EVENT-{from_status or 'NULL'}-{to_status}",
        "from": from_status,
        "to": to_status,
        "actor": "creator",
        "at": "2026-07-16T00:00:00Z",
        "receipt_id": receipt_id,
    }


def packet(status: str, events: list[dict]) -> dict:
    accepted = any(item.get("to") == "accepted" for item in events)
    activated = any(item.get("to") == "active" for item in events)
    return {
        "id": "WP-TEST",
        "status": status,
        "contract_sha256": CONTRACT,
        "required_approver": "creator",
        "approval_receipt_id": ACCEPTANCE_ID if accepted else None,
        "approved_by": "AC-21" if accepted else None,
        "a1_boundary_manifest": {"manifest_id": "BOUNDARY"} if activated else None,
        "status_events": events,
    }


def receipts() -> dict[str, dict]:
    return {
        ACCEPTANCE_ID: {
            "receipt_id": ACCEPTANCE_ID,
            "sealed": True,
            "receipt_kind": "packet-acceptance",
            "issuer_role": "creator",
            "issued_by": "AC-21",
            "subject_contract_sha256": {"WP-TEST": CONTRACT},
            "subject_claims": [
                {"subject_id": "WP-TEST", "claims": ["ACCEPT-WP-TEST"]}
            ],
        },
        ACTIVATION_ID: {
            "receipt_id": ACTIVATION_ID,
            "sealed": True,
            "receipt_kind": "packet-activation",
        },
        COMPLETION_ID: {
            "receipt_id": COMPLETION_ID,
            "sealed": True,
            "receipt_kind": "packet-completion",
            "issuer_role": "creator",
            "subject_contract_sha256": {"WP-TEST": CONTRACT},
            "subject_claims": [
                {
                    "subject_id": "WP-TEST",
                    "claims": ["ACCEPT-COMPLETION-WP-TEST"],
                }
            ],
        },
    }


PROPOSED = event("proposed", None, None)
ACCEPTED = event("accepted", "proposed", ACCEPTANCE_ID)
ACTIVE = event("active", "accepted", ACTIVATION_ID)
VERIFYING = event("verifying", "active", ACTIVATION_ID)
CANDIDATE = event("candidate", "verifying", ACTIVATION_ID)
RELEASED = event("released", "candidate", COMPLETION_ID)


class PacketRetainedAuthorityHistoryTests(unittest.TestCase):
    def validate(self, candidate: dict, authority: dict[str, dict] | None = None) -> list[str]:
        return foundation.validate_packet_approval_and_events(
            candidate,
            receipts() if authority is None else authority,
            ACTIVATION_ID,
        )

    def test_pre_release_rollback_does_not_require_completion(self) -> None:
        candidate = packet(
            "rolled-back",
            [PROPOSED, ACCEPTED, ACTIVE, event("rolled-back", "active", ACTIVATION_ID)],
        )
        authority = receipts()
        del authority[COMPLETION_ID]
        self.assertEqual(self.validate(candidate, authority), [])

    def test_released_then_superseded_retains_completion(self) -> None:
        candidate = packet(
            "superseded",
            [
                PROPOSED,
                ACCEPTED,
                ACTIVE,
                VERIFYING,
                CANDIDATE,
                RELEASED,
                event("superseded", "released", COMPLETION_ID),
            ],
        )
        self.assertEqual(self.validate(candidate), [])
        authority = receipts()
        del authority[COMPLETION_ID]
        errors = self.validate(candidate, authority)
        self.assertTrue(
            any("release event lacks sealed creator completion" in item for item in errors),
            errors,
        )

    def test_rejected_or_superseded_after_acceptance_retains_acceptance(self) -> None:
        for status in ("rejected", "superseded"):
            with self.subTest(status=status):
                candidate = packet(
                    status,
                    [PROPOSED, ACCEPTED, event(status, "accepted", ACCEPTANCE_ID)],
                )
                self.assertEqual(self.validate(candidate), [])
                authority = receipts()
                del authority[ACCEPTANCE_ID]
                errors = self.validate(candidate, authority)
                self.assertTrue(
                    any("lacks a sealed packet-acceptance" in item for item in errors),
                    errors,
                )

    def test_pre_acceptance_terminal_packet_needs_no_acceptance(self) -> None:
        for status in ("rejected", "superseded"):
            with self.subTest(status=status):
                candidate = packet(
                    status,
                    [PROPOSED, event(status, "proposed", None)],
                )
                self.assertEqual(self.validate(candidate, {}), [])

    def test_acceptance_fields_without_history_are_rejected(self) -> None:
        candidate = packet("rejected", [PROPOSED, event("rejected", "proposed", None)])
        candidate["approval_receipt_id"] = ACCEPTANCE_ID
        candidate["approved_by"] = "AC-21"
        errors = self.validate(candidate)
        self.assertTrue(any("without an acceptance event" in item for item in errors))

    def test_released_packet_retains_candidate_evidence(self) -> None:
        source = foundation.load_json(
            foundation.ROOT / "work-packets/proposed/WP-0003.json"
        )
        source["status"] = "released"
        source["candidate_evidence"] = {
            "diff_artifact_id": None,
            "artifact_manifest_id": None,
            "command_log_artifact_id": None,
            "known_limits": [],
        }
        with tempfile.TemporaryDirectory(dir=foundation.ROOT) as temporary:
            path = Path(temporary) / "WP-TEST.json"
            path.write_text(json.dumps(source), encoding="utf-8")
            errors = foundation.validate_work_packet_semantics(path)
        self.assertTrue(
            any(
                "packet with candidate history lacks complete"
                in item
                for item in errors
            ),
            errors,
        )

    def test_terminal_packet_retains_materialized_candidate_evidence(self) -> None:
        source = foundation.load_json(
            foundation.ROOT / "work-packets/proposed/WP-0003.json"
        )
        transitions = {
            "rolled-back": [
                ("active", "verifying"),
                ("verifying", "candidate"),
                ("candidate", "rolled-back"),
            ],
            "superseded": [
                ("active", "verifying"),
                ("verifying", "candidate"),
                ("candidate", "released"),
                ("released", "superseded"),
            ],
        }
        for terminal, edges in transitions.items():
            with self.subTest(terminal=terminal):
                candidate = json.loads(json.dumps(source))
                candidate["status"] = terminal
                candidate["actual_paths"] = []
                candidate["candidate_evidence"] = {
                    "diff_artifact_id": None,
                    "artifact_manifest_id": None,
                    "command_log_artifact_id": None,
                    "known_limits": [],
                }
                for index, (from_status, to_status) in enumerate(edges, start=1):
                    candidate["status_events"].append(
                        {
                            "event_id": f"WPE-RETAIN-{terminal}-{index}",
                            "from": from_status,
                            "to": to_status,
                            "actor": "test",
                            "at": f"2026-07-16T19:4{index + 1}:34Z",
                            "receipt_id": "RR-TEST",
                        }
                    )
                with tempfile.TemporaryDirectory(dir=foundation.ROOT) as temporary:
                    path = Path(temporary) / f"WP-{terminal}.json"
                    path.write_text(json.dumps(candidate), encoding="utf-8")
                    errors = foundation.validate_work_packet_semantics(path)
                self.assertTrue(
                    any(
                        "packet with candidate history lacks complete" in item
                        for item in errors
                    ),
                    errors,
                )


class WP0002LastBearingLifecycleTests(unittest.TestCase):
    @staticmethod
    def lifecycle_packet(status: str, destinations: list[str]) -> dict:
        return {
            "status": status,
            "status_events": [{"to": destination} for destination in destinations],
        }

    def test_transition_matrix(self) -> None:
        cases = (
            ("proposed", ["proposed"], False, False),
            ("accepted", ["proposed", "accepted"], False, False),
            ("active", ["proposed", "accepted", "active"], False, False),
            ("rejected", ["proposed", "rejected"], False, False),
            ("rejected", ["proposed", "accepted", "rejected"], False, False),
            ("superseded", ["proposed", "superseded"], False, False),
            ("superseded", ["proposed", "accepted", "superseded"], False, False),
            ("rolled-back", ["proposed", "accepted", "active", "rolled-back"], False, False),
            ("verifying", ["proposed", "accepted", "active", "verifying"], False, True),
            ("candidate", ["proposed", "accepted", "active", "verifying", "candidate"], False, True),
            ("released", ["proposed", "accepted", "active", "verifying", "candidate", "released"], False, True),
            ("rejected", ["proposed", "accepted", "active", "rejected"], False, True),
            ("rolled-back", ["proposed", "accepted", "active", "verifying", "rolled-back"], False, False),
            ("superseded", ["proposed", "accepted", "active", "verifying", "candidate", "released", "superseded"], False, True),
            ("proposed", ["proposed"], True, True),
            ("rolled-back", ["proposed", "accepted", "active", "verifying", "rolled-back"], True, True),
        )
        for status, destinations, now, expected in cases:
            with self.subTest(
                status=status,
                destinations=destinations,
                materialized_now=now,
            ):
                self.assertEqual(
                    foundation.wp0002_ci_requires_lastbearing_project(
                        self.lifecycle_packet(status, destinations), now
                    ),
                    expected,
                )

    def test_ci_contract_pins_current_tree_and_lifecycle_checks(self) -> None:
        source = (foundation.REPO_ROOT / ".github/workflows/wp0002-ci.yml").read_text(
            encoding="utf-8"
        )
        with tempfile.TemporaryDirectory() as temporary:
            workflow = Path(temporary) / "wp0002-ci.yml"
            workflow.write_text(source, encoding="utf-8")
            self.assertEqual(foundation.validate_wp0002_ci_save_contract(workflow), [])
            mutations = (
                source.replace(
                    "foundation.wp0002_ci_requires_lastbearing_project(",
                    "foundation.untrusted_lastbearing_policy(",
                ),
                source.replace(
                    ' || "$status" == "rejected"',
                    "",
                ),
            )
            for index, mutation in enumerate(mutations):
                with self.subTest(index=index):
                    workflow.write_text(mutation, encoding="utf-8")
                    self.assertTrue(
                        foundation.validate_wp0002_ci_save_contract(workflow)
                    )


if __name__ == "__main__":
    unittest.main()
