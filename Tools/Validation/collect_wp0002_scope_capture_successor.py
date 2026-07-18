#!/usr/bin/env python3
"""Collect the forward-only WP-0002 local-operator successor scope proof.

The original collector remains the immutable implementation bound by the v1
receipt.  This adapter hash-pins and loads it as a real temporary module, then
routes collection to a new content-addressed successor namespace.  Creator
file bytes remain excluded exactly as in v1.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
import types
from pathlib import Path


V1_COLLECTOR_SHA256 = (
    "68dfa2c5ce802b71a29717f530be63344d74c50cc8e5e5de4c1b26aa3dcde9f2"
)
V1_COLLECTOR_PATH = Path(__file__).with_name("collect_wp0002_scope_capture.py")
SUCCESSOR_CAPTURE_CONTRACT = (
    "wp0002-local-operator-successor-scope-capture-v2"
)
SUCCESSOR_OUTPUT_ROOT = (
    "docs/evidence/WP-0002/local-operator-successor/scope-capture"
)
SUCCESSOR_RETAINED_CAPTURE = f"{SUCCESSOR_OUTPUT_ROOT}/working-tree-scope.json"
_MODULE_NAME = "_wp0002_scope_collector_v1_pinned_for_successor"
_MISSING = object()


class SuccessorCollectorError(RuntimeError):
    """Raised when the pinned v1 collector cannot be loaded exactly."""


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _load_v1() -> types.ModuleType:
    try:
        source = V1_COLLECTOR_PATH.read_bytes()
    except OSError as exc:
        raise SuccessorCollectorError(
            f"v1 scope collector cannot be read: {exc}"
        ) from exc
    if _sha256(source) != V1_COLLECTOR_SHA256:
        raise SuccessorCollectorError("v1 scope collector hash mismatch")

    previous = sys.modules.get(_MODULE_NAME, _MISSING)
    module = types.ModuleType(_MODULE_NAME)
    module.__file__ = str(V1_COLLECTOR_PATH)
    module.__package__ = ""
    sys.modules[_MODULE_NAME] = module
    try:
        exec(compile(source, str(V1_COLLECTOR_PATH), "exec"), module.__dict__)
    except Exception as exc:
        raise SuccessorCollectorError(
            f"v1 scope collector cannot load: {exc}"
        ) from exc
    finally:
        if previous is _MISSING:
            sys.modules.pop(_MODULE_NAME, None)
        else:
            sys.modules[_MODULE_NAME] = previous  # type: ignore[assignment]
    return module


_v1 = _load_v1()
_v1.AMENDMENT_CAPTURE_CONTRACT = SUCCESSOR_CAPTURE_CONTRACT
_v1.AMENDMENT_OUTPUT_ROOT = SUCCESSOR_OUTPUT_ROOT
_v1.AMENDMENT_RETAINED_CAPTURE = SUCCESSOR_RETAINED_CAPTURE

collect_scope_capture = _v1.collect_scope_capture
verify_scope_capture = _v1.verify_scope_capture
amendment_dirty_profile = _v1.amendment_dirty_profile
parse_porcelain_v2_z = _v1.parse_porcelain_v2_z
AMENDMENT_STATUS_ARGUMENTS = _v1.AMENDMENT_STATUS_ARGUMENTS
CANONICAL_AMENDMENT_ROOT = _v1.CANONICAL_AMENDMENT_ROOT


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True)
    parser.add_argument("--checkpoint", required=True)
    parser.add_argument(
        "--ack-local-operator-successor-capture",
        action="store_true",
    )
    args = parser.parse_args(argv)
    repo_root = Path(__file__).resolve().parents[2]

    try:
        if not args.ack_local_operator_successor_capture:
            raise ValueError(
                "successor collector requires explicit local-operator "
                "successor capture acknowledgement"
            )
        if os.environ.get("CI") or os.environ.get("GITHUB_ACTIONS"):
            raise ValueError("scope collector is forbidden in CI")
        branch = _v1._git_output(
            repo_root,
            ["symbolic-ref", "--short", "HEAD"],
            "agent branch",
        ).decode("utf-8").strip()
        if not branch.startswith("agent/"):
            raise ValueError("scope collector requires an agent/* branch")

        packet = _v1._load_json(repo_root / _v1.PACKET_PATH)
        boundary = _v1._load_json(repo_root / _v1.BOUNDARY_PATH)
        reservation_paths = packet.get("reservation", {}).get("paths")
        protected_paths = boundary.get("permission_boundary", {}).get(
            "protected_paths_read_only"
        )
        if not isinstance(reservation_paths, list) or not isinstance(
            protected_paths, list
        ):
            raise ValueError("packet or boundary paths are unavailable")

        observed_root = Path(_v1.CANONICAL_AMENDMENT_ROOT)
        result = _v1.collect_scope_capture(
            observed_root,
            base_commit=args.base,
            checkpoint_commit=args.checkpoint,
            reservation_paths=reservation_paths,
            protected_paths_read_only=protected_paths,
            output_relative=SUCCESSOR_RETAINED_CAPTURE,
            expected_repository_root=_v1.CANONICAL_AMENDMENT_ROOT,
            evidence_root=repo_root,
            status_arguments=_v1.AMENDMENT_STATUS_ARGUMENTS,
        )
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"WP-0002 SUCCESSOR SCOPE CAPTURE: FAIL: {exc}", file=sys.stderr)
        return 1

    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
