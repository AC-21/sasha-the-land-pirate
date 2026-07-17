from __future__ import annotations

import json
import shutil
import tempfile
import unittest
from pathlib import Path

import validate_foundation as foundation


REPO_ROOT = Path(__file__).resolve().parents[3]
FOUNDATION_ROOT = REPO_ROOT / "docs" / "foundation-v0.1"


class WP0002PackageMetaTests(unittest.TestCase):
    def copy_packages(self, destination: Path) -> None:
        for name in foundation.WP0002_PACKAGE_META_ROOTS:
            shutil.copytree(REPO_ROOT / name, destination / name)

    def packet(self) -> dict:
        return json.loads(
            (FOUNDATION_ROOT / "work-packets/proposed/WP-0002.json").read_text(
                encoding="utf-8"
            )
        )

    def test_exact_current_inventory_and_hashes_pass(self) -> None:
        self.assertEqual(len(foundation.WP0002_PACKAGE_META_SHA256), 27)
        self.assertEqual(foundation.validate_wp0002_package_meta_inventory(), [])

    def test_missing_modified_and_extra_sibling_metadata_fail(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.copy_packages(root)

            missing = root / "SimulationCore/README.md.meta"
            original = missing.read_bytes()
            missing.unlink()
            self.assertTrue(foundation.validate_wp0002_package_meta_inventory(root))
            missing.write_bytes(original)

            changed = root / "SaveContracts/package.json.meta"
            changed.write_bytes(changed.read_bytes() + b"# changed\n")
            self.assertTrue(foundation.validate_wp0002_package_meta_inventory(root))
            shutil.copy2(REPO_ROOT / "SaveContracts/package.json.meta", changed)

            sibling = root / "SimulationCore/Runtime/Other.cs"
            sibling.write_text("internal sealed class Other {}\n", encoding="utf-8")
            sibling.with_name("Other.cs.meta").write_bytes(
                foundation.wp0002_expected_unity_meta(
                    "SimulationCore/Runtime/Other.cs", False
                )
            )
            errors = foundation.validate_wp0002_package_meta_inventory(root)
            self.assertTrue(any("inventory differs" in error for error in errors))

    def test_future_lastbearing_tree_requires_exact_deterministic_pairing(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.copy_packages(root)
            future = root / "SimulationCore/Runtime/LastBearing"
            future_meta = root / "SimulationCore/Runtime/LastBearing.meta"
            if future.exists():
                shutil.rmtree(future)
            if future_meta.exists():
                future_meta.unlink()
            future.mkdir()
            future_meta.write_bytes(
                foundation.wp0002_expected_unity_meta(
                    "SimulationCore/Runtime/LastBearing", True
                )
            )
            source = future / "FirstPlayableState.cs"
            source.write_text("internal sealed class FirstPlayableState {}\n", encoding="utf-8")
            source.with_name("FirstPlayableState.cs.meta").write_bytes(
                foundation.wp0002_expected_unity_meta(
                    "SimulationCore/Runtime/LastBearing/FirstPlayableState.cs", False
                )
            )
            self.assertEqual(foundation.validate_wp0002_package_meta_inventory(root), [])

            source.with_name("FirstPlayableState.cs.meta").write_bytes(b"fileFormatVersion: 2\n")
            self.assertTrue(foundation.validate_wp0002_package_meta_inventory(root))

    def test_symlink_import_target_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.copy_packages(root)
            target = root / "SimulationCore/Runtime/CanonicalState.cs"
            target.unlink()
            target.symlink_to(root / "SimulationCore/Runtime/TechnicalState.cs")
            errors = foundation.validate_wp0002_package_meta_inventory(root)
            self.assertTrue(any("not a symlink" in error for error in errors))

    def test_packet_reserves_only_exact_lastbearing_sibling_metadata(self) -> None:
        packet = self.packet()
        self.assertEqual(foundation.validate_wp0002_package_graph_contract(packet), [])

        packet["declared_paths"].remove("SimulationCore/Runtime/LastBearing.meta")
        self.assertTrue(foundation.validate_wp0002_package_graph_contract(packet))

        packet = self.packet()
        packet["declared_paths"].append("SimulationCore/Runtime/Other.meta")
        self.assertTrue(foundation.validate_wp0002_package_graph_contract(packet))


if __name__ == "__main__":
    unittest.main()
