from __future__ import annotations

import importlib.util
import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TOOL_PATH = ROOT.parents[1] / "Tools" / "Validation" / "validate_wp0002_entry_gate.py"
SPEC = importlib.util.spec_from_file_location("validate_wp0002_entry_gate", TOOL_PATH)
assert SPEC is not None and SPEC.loader is not None
entry_gate = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(entry_gate)


class WP0002StandaloneEntryGateTests(unittest.TestCase):
    def materialized_documents(
        self,
    ) -> tuple[dict, dict, dict[str, dict], list[dict]]:
        packet = json.loads(
            (ROOT / "work-packets" / "proposed" / "WP-0002.json").read_text(
                encoding="utf-8"
            )
        )
        state = json.loads(
            (ROOT / "governance" / "ratification-state.json").read_text(
                encoding="utf-8"
            )
        )
        packet["status"] = "active"
        gate = state["entry_gates"]["ugly_gameplay_toy"]
        gate["status"] = "passed"
        receipts: dict[str, dict] = {}
        decision_records: list[dict] = []

        for index, requirement in enumerate(gate["decision_requirements"]):
            receipt_id = f"RR-WP0002-DECISION-{index:02d}"
            requirement["receipt_id"] = receipt_id
            decision_records.append(
                self.decision_record(
                    decision_records,
                    requirement["decision_id"],
                    receipt_id,
                )
            )
        for requirement, record in zip(
            gate["decision_requirements"], decision_records
        ):
            decision_id = requirement["decision_id"]
            receipt_id = requirement["receipt_id"]
            receipts[receipt_id] = self.receipt(
                receipt_id,
                packet["contract_sha256"],
                "decision-ratification",
                [decision_id],
                [decision_id],
                [requirement["allowed_claims"][0]],
                event_bindings={decision_id: record["event_hash"]},
            )

        for index, requirement in enumerate(gate["receipt_requirements"]):
            receipt_id = f"RR-WP0002-GATE-{index:02d}"
            requirement["receipt_id"] = receipt_id
            receipts[receipt_id] = self.receipt(
                receipt_id,
                packet["contract_sha256"],
                requirement["required_receipt_kind"],
                requirement["subject_ids"],
                requirement["subject_ids"],
                requirement["required_claims"],
            )
        return packet, state, receipts, decision_records

    @staticmethod
    def decision_record(
        existing: list[dict],
        decision_id: str,
        receipt_id: str,
        *,
        supersedes: str | None = None,
    ) -> dict:
        record = {
            "id": decision_id,
            "sequence": len(existing) + 1,
            "status": "ratified",
            "approval_receipt_id": receipt_id,
            "supersedes": supersedes,
            "previous_event_hash": existing[-1]["event_hash"] if existing else None,
        }
        record["event_hash"] = hashlib.sha256(
            json.dumps(
                record,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=False,
            ).encode("utf-8")
        ).hexdigest()
        return record

    @staticmethod
    def receipt(
        receipt_id: str,
        contract: str,
        kind: str,
        subject_ids: list[str],
        claim_subjects: list[str],
        claims: list[str],
        *,
        event_bindings: dict[str, str] | None = None,
    ) -> dict:
        return {
            "receipt_id": receipt_id,
            "issuer_role": "creator",
            "receipt_kind": kind,
            "artifact_resolver": {
                "type": "external-protected",
                "resolver_reference": "https://example.invalid/protected-receipt",
            },
            "subject_ids": list(subject_ids),
            "subject_claims": [
                {"subject_id": subject_id, "claims": list(claims)}
                for subject_id in claim_subjects
            ],
            "subject_contract_sha256": {"WP-0002": contract},
            "subject_event_sha256": event_bindings or {},
            "sealed": True,
        }

    def validate(
        self,
        packet: dict,
        state: dict,
        receipts: dict[str, dict],
        decision_records: list[dict],
    ) -> list[str]:
        return entry_gate.validate_entry_gate(
            packet,
            state,
            receipts,
            "AUTHORIZE-CITY-COMPARISON",
            decision_records,
        )

    def test_exact_materialized_gate_passes(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        self.assertEqual(self.validate(packet, state, receipts, decisions), [])

    def test_city_claim_on_wrong_subject_is_rejected(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        city = next(
            item
            for item in state["entry_gates"]["ugly_gameplay_toy"][
                "receipt_requirements"
            ]
            if item["purpose"] == "authorize-city-comparison"
        )
        receipt = receipts[city["receipt_id"]]
        receipt["subject_claims"] = [
            binding
            for binding in receipt["subject_claims"]
            if binding["subject_id"] != "D-0030"
        ]
        errors = self.validate(packet, state, receipts, decisions)
        self.assertTrue(
            any("on D-0030" in error for error in errors),
            errors,
        )

    def test_city_authorization_missing_wp0002_claim_is_rejected(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        city = next(
            item
            for item in state["entry_gates"]["ugly_gameplay_toy"][
                "receipt_requirements"
            ]
            if item["purpose"] == "authorize-city-comparison"
        )
        receipt = receipts[city["receipt_id"]]
        receipt["subject_claims"] = [
            binding
            for binding in receipt["subject_claims"]
            if binding["subject_id"] != "WP-0002"
        ]
        errors = self.validate(packet, state, receipts, decisions)
        self.assertTrue(
            any("on WP-0002" in error for error in errors),
            errors,
        )

    def test_schema_valid_mismatch_action_mutation_is_rejected(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        requirement = state["entry_gates"]["ugly_gameplay_toy"][
            "decision_requirements"
        ][0]
        self.assertEqual(requirement["decision_id"], "D-0006")
        requirement["mismatch_action"] = "silently-ignore"
        errors = self.validate(packet, state, receipts, decisions)
        self.assertTrue(
            any("D-0006 gate mismatch_action differs" in error for error in errors),
            errors,
        )

    def test_mutable_city_requirement_cannot_redefine_the_authority(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        city = next(
            item
            for item in state["entry_gates"]["ugly_gameplay_toy"][
                "receipt_requirements"
            ]
            if item["purpose"] == "authorize-city-comparison"
        )
        city["subject_ids"] = ["WP-0002"]
        city["required_claims"] = ["ACCEPT-WP-0002"]
        errors = self.validate(packet, state, receipts, decisions)
        self.assertTrue(
            any("authorize-city-comparison gate subject_ids differs" in error for error in errors),
            errors,
        )

    def test_required_cli_claim_is_pinned(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        errors = entry_gate.validate_entry_gate(
            packet,
            state,
            receipts,
            "ACCEPT-WP-0002",
            decisions,
        )
        self.assertIn(
            "required claim must equal AUTHORIZE-CITY-COMPARISON",
            errors,
        )

    def add_superseding_head(
        self,
        packet: dict,
        state: dict,
        receipts: dict[str, dict],
        decisions: list[dict],
        *,
        materialize_gate_head: bool,
    ) -> tuple[str, str]:
        requirement = state["entry_gates"]["ugly_gameplay_toy"][
            "decision_requirements"
        ][0]
        self.assertEqual(requirement["decision_id"], "D-0006")
        stale_receipt_id = requirement["receipt_id"]
        head_receipt_id = "RR-WP0002-D0006-SUCCESSOR"
        head = self.decision_record(
            decisions,
            "D-9999",
            head_receipt_id,
            supersedes="D-0006",
        )
        decisions.append(head)
        receipts[head_receipt_id] = self.receipt(
            head_receipt_id,
            packet["contract_sha256"],
            "decision-ratification",
            ["D-9999"],
            ["D-9999"],
            ["RATIFY-THESIS"],
            event_bindings={"D-9999": head["event_hash"]},
        )
        if materialize_gate_head:
            requirement["receipt_id"] = head_receipt_id
        return stale_receipt_id, head_receipt_id

    def test_superseded_active_head_receipt_passes(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        self.add_superseding_head(
            packet,
            state,
            receipts,
            decisions,
            materialize_gate_head=True,
        )
        self.assertEqual(self.validate(packet, state, receipts, decisions), [])

    def test_stale_root_receipt_is_rejected_after_supersession(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        stale_receipt_id, head_receipt_id = self.add_superseding_head(
            packet,
            state,
            receipts,
            decisions,
            materialize_gate_head=False,
        )
        errors = self.validate(packet, state, receipts, decisions)
        self.assertTrue(
            any(
                stale_receipt_id in error and head_receipt_id in error
                for error in errors
            ),
            errors,
        )

    def test_active_head_event_hash_binding_is_required(self) -> None:
        packet, state, receipts, decisions = self.materialized_documents()
        receipt_id = state["entry_gates"]["ugly_gameplay_toy"][
            "decision_requirements"
        ][0]["receipt_id"]
        receipts[receipt_id]["subject_event_sha256"]["D-0006"] = "0" * 64
        errors = self.validate(packet, state, receipts, decisions)
        self.assertTrue(
            any("D-0006 receipt does not bind active head" in error for error in errors),
            errors,
        )


if __name__ == "__main__":
    unittest.main()
