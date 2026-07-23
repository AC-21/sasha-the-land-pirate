from __future__ import annotations

import unittest

import validate_foundation as foundation


class ProductFirstSourcePinTests(unittest.TestCase):
    def test_historical_path_fix_does_not_pin_live_gameplay_sources(self) -> None:
        for path in (
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/"
            "LastBearingAdapterTests.cs",
            "Tests/AtomicLandPirate.CoreTests/LastBearing/GameSourceContract.cs",
        ):
            with self.subTest(path=path):
                self.assertNotIn(
                    path,
                    foundation.WP0002_NATIVE_PATH_FIX_ARTIFACT_SHA256,
                )


if __name__ == "__main__":
    unittest.main()
