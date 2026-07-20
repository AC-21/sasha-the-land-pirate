from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path
from unittest import mock

import validate_foundation as foundation


ROOT = Path(__file__).resolve().parents[1]


def load(relative: str) -> dict:
    return json.loads((ROOT / relative).read_text(encoding="utf-8"))


class Wp0002ReservationRenewalTests(unittest.TestCase):
    def documents(self) -> tuple[dict, dict, dict]:
        packet = load("work-packets/proposed/WP-0002.json")
        manifest = load("governance/a1-boundaries/WP-0002.json")
        receipt = load(foundation.WP0002_RESERVATION_RENEWAL_RECEIPT_PATH)
        return packet, manifest, receipt

    def test_exact_renewal_is_valid(self) -> None:
        packet, manifest, receipt = self.documents()
        errors = foundation.validate_wp0002_reservation_renewal(
            packet,
            manifest,
            {foundation.WP0002_RESERVATION_RENEWAL_RECEIPT_ID: receipt},
        )
        self.assertEqual(errors, [])

    def test_receipt_claim_drift_fails_closed(self) -> None:
        packet, manifest, receipt = self.documents()
        receipt = copy.deepcopy(receipt)
        receipt["subject_claims"][0]["claims"] = ["AUTHORIZE-ANY-RENEWAL"]
        errors = foundation.validate_wp0002_reservation_renewal(
            packet,
            manifest,
            {foundation.WP0002_RESERVATION_RENEWAL_RECEIPT_ID: receipt},
        )
        self.assertIn(
            "WP-0002 reservation renewal receipt lacks exact protected creator authority",
            errors,
        )

    def test_scope_or_contract_drift_fails_closed(self) -> None:
        packet, manifest, receipt = self.documents()
        manifest = copy.deepcopy(manifest)
        manifest["active_reservation_renewal"]["paths_changed"] = True
        errors = foundation.validate_wp0002_reservation_renewal(
            packet,
            manifest,
            {foundation.WP0002_RESERVATION_RENEWAL_RECEIPT_ID: receipt},
        )
        self.assertIn("WP-0002 active reservation renewal is not exact", errors)

    def test_historical_artifact_tampering_at_protected_introduction_fails_closed(
        self,
    ) -> None:
        packet, manifest, receipt = self.documents()
        introductions = foundation.git_first_parent_path_additions(
            "refs/remotes/origin/main",
            foundation.WP0002_RESERVATION_RENEWAL_RECEIPT_REPO_PATH,
        )
        self.assertIsNotNone(introductions)
        self.assertEqual(len(introductions or []), 1)
        introduction = (introductions or [""])[0]
        tampered_path = foundation.WP0002_RESERVATION_RENEWAL_ARTIFACT_PATHS[0]
        original_blob = foundation.git_repo_blob

        def blob_at(commit: str, relative: str) -> bytes | None:
            if commit == introduction and relative == tampered_path:
                return b"tampered protected historical blob\n"
            return original_blob(commit, relative)

        with mock.patch.object(foundation, "git_repo_blob", side_effect=blob_at):
            errors = foundation.validate_wp0002_reservation_renewal(
                packet,
                manifest,
                {foundation.WP0002_RESERVATION_RENEWAL_RECEIPT_ID: receipt},
            )

        self.assertIn(
            "WP-0002 reservation renewal protected introduction blob drifted: "
            + tampered_path,
            errors,
        )


if __name__ == "__main__":
    unittest.main()
