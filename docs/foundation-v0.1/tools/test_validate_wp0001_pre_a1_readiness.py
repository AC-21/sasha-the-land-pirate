#!/usr/bin/env python3
"""Focused regression tests for the WP-0001 readiness semantics."""

from __future__ import annotations

import copy
import unittest

import validate_foundation as foundation
from validate_wp0001_pre_a1_readiness import (
    PACKET_RELATIVE,
    READINESS_REPO_RELATIVE,
    validate_readiness_semantics,
)


class ReadinessSemanticsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.evidence = foundation.load_json(
            foundation.REPO_ROOT / READINESS_REPO_RELATIVE
        )
        cls.packet = foundation.load_json(foundation.ROOT / PACKET_RELATIVE)
        cls.receipts = {
            receipt["receipt_id"]: receipt
            for receipt in (
                foundation.load_json(path)
                for path in sorted((foundation.ROOT / "ledger" / "receipts").glob("*.json"))
            )
        }

    def validate(self, evidence: dict) -> list[str]:
        return validate_readiness_semantics(
            evidence,
            self.packet,
            git_commit_exists=foundation.git_commit_exists,
            receipts_by_id=self.receipts,
        )

    def test_canonical_snapshot_passes_semantics(self) -> None:
        self.assertEqual([], self.validate(copy.deepcopy(self.evidence)))

    def test_ready_claim_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.evidence)
        candidate["overall_status"] = "ready"
        self.assertTrue(
            any("must remain blocked" in error for error in self.validate(candidate))
        )

    def test_deviation_omission_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.evidence)
        candidate["deviations"] = candidate["deviations"][:1]
        self.assertTrue(
            any("deviation closure differs" in error for error in self.validate(candidate))
        )

    def test_secret_bearing_key_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.evidence)
        candidate["direct_mcp_route"]["external_client"]["api_key"] = "not-a-real-key"
        self.assertTrue(
            any("forbidden secret-bearing key" in error for error in self.validate(candidate))
        )

    def test_relay_package_mismatch_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.evidence)
        candidate["direct_mcp_route"]["relay"]["package_copy_sha256"] = "0" * 64
        self.assertTrue(
            any("relay must match" in error for error in self.validate(candidate))
        )


if __name__ == "__main__":
    unittest.main()
