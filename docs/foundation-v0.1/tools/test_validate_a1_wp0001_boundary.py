#!/usr/bin/env python3
"""Focused adversarial tests for the WP-0001 A1 activation boundary."""

from __future__ import annotations

import copy
import hashlib
import json
import subprocess
import unittest
from unittest import mock

import validate_foundation as foundation


class Wp0001A1BoundaryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        fixture = foundation.load_json(
            foundation.ROOT
            / "governance"
            / "fixtures"
            / "a1-wp0001-boundary.fixtures.json"
        )
        cls.base_case = fixture["base_case"]

    def full_schema_manifest(self) -> dict:
        manifest = copy.deepcopy(self.base_case)
        manifest.update(
            {
                "schema_version": 4,
                "manifest_id": "A1B-WP-0001-FIXTURE",
                "packet_id": "WP-0001",
                "packet_contract_sha256": (
                    "eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da"
                    "02abfe008faeda3"
                ),
                "created_at": "2026-07-16T12:10:06Z",
                "attested_by": "fixture-creator",
                "attestation_receipt_id": "RR-WP0001-FIXTURE",
                "reservation": {
                    "lease_id": "fixture-lease",
                    "fencing_token": "fixture-fence",
                    "expires_at": "2026-07-16T20:10:06Z",
                    "paths": ["BuildArtifacts/WP-0001/"],
                    "domains": ["wp0001"],
                },
                "foundation_binding": {
                    "constitution_path": "00-GAME-CONSTITUTION.md",
                    "constitution_sha256": "5" * 64,
                    "decision_ledger_path": "ledger/decisions.jsonl",
                    "decision_ledger_sha256": "6" * 64,
                    "last_creator_receipt_id": "RR-FIXTURE-PRIOR",
                },
                "protection_boundary": {
                    "protected_paths": [
                        "docs/foundation-v0.1/00-GAME-CONSTITUTION.md",
                    ],
                    "writable_paths": ["BuildArtifacts/WP-0001/"],
                    "ephemeral_scratch_paths": list(
                        foundation.WP0001_UNITY_EPHEMERAL_SCRATCH_PATHS
                    ),
                    "scratch_destroy_on_close": True,
                    "foundation_read_only": True,
                },
                "credential_boundary": {
                    "approved_credential_ids": [],
                    "denied_capabilities": [
                        "protected-main-write",
                        "governance-write",
                        "receipt-write",
                        "merge",
                        "release",
                    ],
                },
                "manual_import_boundary": {
                    "mode": "creator-operated-import-or-reject",
                    "agent_can_merge": False,
                    "agent_can_release": False,
                    "agent_can_accept_evidence": False,
                    "creator_review_required": True,
                },
                "local_observation_exceptions": [],
            }
        )
        manifest["approved_environment"].update(
            {
                "os": "macOS",
                "os_version": "fixture",
                "architecture": "arm64",
                "hardware_id": "fixture-hardware",
                "sandbox_profile": "fixture-sandbox",
                "network_policy": "fixture-network",
            }
        )
        return manifest

    def validate(
        self,
        candidate: dict,
        *,
        observed_seed_tree_sha256: str | None = None,
    ) -> list[str]:
        return foundation.a1_wp0001_boundary_codes(
            candidate.get("repository"),
            candidate.get("runtime_boundary"),
            candidate.get("project_seed"),
            candidate.get("unity_mcp_route"),
            candidate.get("approved_toolchain"),
            candidate.get("approved_environment"),
            candidate.get("wp0001_toolchain_profile"),
            candidate.get("activation_evidence"),
            candidate.get("raw_capture_collectors"),
            observed_seed_tree_sha256=(
                observed_seed_tree_sha256
                or candidate["project_seed"]["git_tree_sha256"]
            ),
        )

    def test_canonical_fixture_passes(self) -> None:
        self.assertEqual([], self.validate(copy.deepcopy(self.base_case)))
        self.assertEqual(
            [],
            foundation.validate_a1_wp0001_boundary_fixtures(),
        )

    def test_foundation_git_is_absolute_sanitized_and_replace_safe(
        self,
    ) -> None:
        completed = subprocess.CompletedProcess([], 0, b"", b"")
        with mock.patch.object(
            foundation.subprocess,
            "run",
            return_value=completed,
        ) as run_mock:
            foundation.run_foundation_git(
                ["cat-file", "-e", f"{'1' * 40}^{{commit}}"]
            )
        arguments = run_mock.call_args.args[0]
        options = run_mock.call_args.kwargs
        self.assertEqual("/usr/bin/git", arguments[0])
        self.assertIn("--no-replace-objects", arguments)
        self.assertIn("core.hooksPath=/dev/null", arguments)
        self.assertEqual("1", options["env"]["GIT_NO_LAZY_FETCH"])
        self.assertEqual("/dev/null", options["env"]["GIT_CONFIG_GLOBAL"])
        self.assertEqual(subprocess.DEVNULL, options["stdin"])

    def test_complete_wp0001_manifest_passes_schema_v4(self) -> None:
        schema = foundation.load_json(foundation.A1_BOUNDARY_SCHEMA)
        self.assertEqual(
            [],
            foundation.validate_schema_subset(
                self.full_schema_manifest(),
                schema,
                schema,
                "fixture A1 boundary",
            ),
        )

    def test_forbidden_sanitized_tool_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.base_case)
        tools = ["Unity_RunCommand"]
        policy = candidate["unity_mcp_route"]["codex_policy"]
        policy["enabled_tools"] = tools
        policy["client_visible_tools"] = tools
        policy["enabled_tools_sha256"] = hashlib.sha256(
            json.dumps(
                tools,
                ensure_ascii=False,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        self.assertIn("wp0001-mcp-forbidden-tool", self.validate(candidate))

    def test_socket_pid_drift_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.base_case)
        candidate["unity_mcp_route"]["bridge"]["editor_pid"] = 999999
        codes = self.validate(candidate)
        self.assertIn("wp0001-mcp-endpoint-invalid", codes)
        self.assertIn("wp0001-mcp-relay-arguments-mismatch", codes)
        self.assertIn("wp0001-mcp-socket-exception-invalid", codes)

    def test_noncanonical_runtime_config_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.base_case)
        candidate["unity_mcp_route"]["codex_policy"]["runtime_config_path"] = (
            "/private/var/wp0001/home/.codex/wp0001.config.toml"
        )
        self.assertIn("wp0001-mcp-runtime-state-escape", self.validate(candidate))

    def test_effective_server_inventory_drift_is_rejected(self) -> None:
        candidate = copy.deepcopy(self.base_case)
        candidate["unity_mcp_route"]["activation_session"][
            "server_inventory_sha256"
        ] = "9" * 64
        self.assertIn(
            "wp0001-mcp-config-inventory-invalid",
            self.validate(candidate),
        )

    def test_raw_collectors_require_exact_hash_bound_creator_authority(
        self,
    ) -> None:
        for mutation in (
            lambda value: value.pop("network"),
            lambda value: value.update(
                {"authority_claim": "UNAUTHORIZED-COLLECTORS"}
            ),
            lambda value: value["protocol"].update(
                {"path": "docs/foundation-v0.1/tools/other.py"}
            ),
            lambda value: value["network"].update({"sha256": "0" * 64}),
        ):
            with self.subTest(mutation=mutation):
                candidate = copy.deepcopy(self.base_case)
                mutation(candidate["raw_capture_collectors"])
                self.assertIn(
                    "wp0001-raw-collector-authority-invalid",
                    self.validate(candidate),
                )

    def test_code_identities_require_explicit_creator_authority(self) -> None:
        candidate = copy.deepcopy(self.base_case)
        candidate["unity_mcp_route"][
            "code_identity_authority_claim"
        ] = "SELF-ATTESTED"
        self.assertIn(
            "wp0001-code-identity-authority-invalid",
            self.validate(candidate),
        )

    def test_network_denial_requires_control_probes(self) -> None:
        candidate = copy.deepcopy(self.base_case)
        controls = candidate["unity_mcp_route"]["controls"]
        controls["mitigation_mode"] = "os-network-denied"
        controls["persistent_relay_process_count"] = 1
        controls["tcp_listener_count"] = 1
        controls["wildcard_listener_count"] = 1
        controls["non_loopback_probe_count"] = 0
        controls["loopback_probe_count"] = 0
        controls["loopback_probe_success_count"] = 0
        controls["approved_egress_probe_count"] = 0
        controls["approved_egress_probe_success_count"] = 0
        controls["unapproved_egress_probe_count"] = 0
        self.assertIn("wp0001-mcp-network-policy-unproven", self.validate(candidate))

    def test_activation_session_must_be_fresh_and_connected(self) -> None:
        candidate = copy.deepcopy(self.base_case)
        session = candidate["unity_mcp_route"]["activation_session"]
        session["captured_at"] = "2020-01-01T00:00:00Z"
        session["connected"] = False
        codes = self.validate(candidate)
        self.assertIn("wp0001-mcp-activation-session-invalid", codes)
        self.assertIn("wp0001-mcp-activation-session-stale", codes)

    def test_activation_session_hashes_are_derived_not_self_attested(
        self,
    ) -> None:
        for field in (
            "session_id_sha256",
            "connection_record_sha256",
            "fd_graph_sha256",
        ):
            with self.subTest(field=field):
                candidate = copy.deepcopy(self.base_case)
                candidate["unity_mcp_route"]["activation_session"][
                    field
                ] = "9" * 64
                self.assertIn(
                    "wp0001-mcp-activation-session-invalid",
                    self.validate(candidate),
                )

    def test_approved_toolchain_rejects_duplicate_or_extra_records(self) -> None:
        candidate = copy.deepcopy(self.base_case)
        candidate["approved_toolchain"].append(
            {
                "name": "Unity Editor ARM64",
                "version": "9999.0.0f1",
                "source": "fixture",
                "sha256": "4" * 64,
            }
        )
        candidate["approved_toolchain"].append(
            {
                "name": "Unapproved Tool",
                "version": "1.0.0",
                "source": "fixture",
                "sha256": "5" * 64,
            }
        )
        self.assertIn("wp0001-approved-toolchain-mismatch", self.validate(candidate))

    def test_seed_tree_policy_rejects_symlink_submodule_and_scratch(self) -> None:
        listing = (
            b"120000 blob 1111111111111111111111111111111111111111"
            b"\tGame/Assets/link\0"
            b"160000 commit 2222222222222222222222222222222222222222"
            b"\tGame/Packages/vendor\0"
            b"100644 blob 3333333333333333333333333333333333333333"
            b"\tGame/Library/state.bin\0"
        )
        self.assertEqual(
            [
                "wp0001-project-seed-assets-not-empty",
                "wp0001-project-seed-embedded-package",
                "wp0001-project-seed-submodule",
                "wp0001-project-seed-symlink",
                "wp0001-project-seed-tracks-scratch",
                "wp0001-project-seed-unexpected-path",
            ],
            foundation.wp0001_seed_tree_policy_codes(listing),
        )

    def test_seed_tree_policy_rejects_embedded_package_code(self) -> None:
        listing = (
            b"100644 blob 1111111111111111111111111111111111111111"
            b"\tGame/Packages/manifest.json\0"
            b"100644 blob 2222222222222222222222222222222222222222"
            b"\tGame/Packages/packages-lock.json\0"
            b"100644 blob 3333333333333333333333333333333333333333"
            b"\tGame/Packages/com.example.bad/Editor/Bootstrap.cs\0"
        )
        self.assertIn(
            "wp0001-project-seed-embedded-package",
            foundation.wp0001_seed_tree_policy_codes(listing),
        )

    def test_project_seed_content_binds_versions_lock_and_identity(self) -> None:
        blobs = {
            "Game/Packages/manifest.json": json.dumps(
                {
                    "enableLockFile": True,
                    "resolutionStrategy": "lowest",
                    "dependencies": {
                        "com.unity.ai.assistant": "2.14.0-pre.1",
                        "com.unity.render-pipelines.universal": "17.3.0",
                        "com.unity.test-framework": "1.6.0",
                    },
                }
            ).encode(),
            "Game/Packages/packages-lock.json": json.dumps(
                {
                    "dependencies": {
                        "com.unity.ai.assistant": {
                            "version": "2.14.0-pre.1",
                            "depth": 0,
                            "source": "registry",
                            "dependencies": {},
                            "url": "https://packages.unity.com",
                        },
                        "com.unity.render-pipelines.universal": {
                            "version": "17.3.0",
                            "depth": 0,
                            "source": "registry",
                            "dependencies": {},
                            "url": "https://packages.unity.com",
                        },
                        "com.unity.test-framework": {
                            "version": "1.6.0",
                            "depth": 0,
                            "source": "registry",
                            "dependencies": {},
                            "url": "https://packages.unity.com",
                        },
                    }
                }
            ).encode(),
            "Game/ProjectSettings/ProjectSettings.asset": (
                b"companyName: LocalFoundationLab\n"
                b"productName: SashaAtomicLandPirate_WP0001\n"
                b"Standalone: local.foundation.sashaatomiclandpirate.wp0001\n"
            ),
            "Game/ProjectSettings/ProjectVersion.txt": (
                b"m_EditorVersion: 6000.3.19f1\n"
                b"m_EditorVersionWithRevision: 6000.3.19f1 (7689f4515d75)\n"
            ),
        }
        with mock.patch.object(
            foundation,
            "git_repo_blob",
            side_effect=lambda _commit, path: blobs.get(path),
        ):
            facts, codes = foundation.wp0001_project_seed_content(
                "1" * 40,
                self.base_case["wp0001_toolchain_profile"],
            )
        self.assertEqual([], codes)
        self.assertTrue(facts["package_lock_enabled"])
        self.assertEqual("lowest", facts["resolution_strategy"])

    def test_project_seed_package_drift_is_rejected(self) -> None:
        blobs = {
            "Game/Packages/manifest.json": json.dumps(
                {
                    "enableLockFile": True,
                    "resolutionStrategy": "lowest",
                    "dependencies": {
                        "com.unity.ai.assistant": "2.14.0-pre.1",
                        "com.unity.render-pipelines.universal": "file:../bad",
                        "com.unity.test-framework": "1.6.0",
                    },
                }
            ).encode(),
            "Game/Packages/packages-lock.json": json.dumps(
                {
                    "dependencies": {
                        "com.unity.ai.assistant": {
                            "version": "2.14.0-pre.1",
                            "depth": 0,
                            "source": "registry",
                            "dependencies": {},
                            "url": "https://packages.unity.com",
                        },
                        "com.unity.render-pipelines.universal": {
                            "version": "17.4.0",
                            "depth": 0,
                            "source": "registry",
                            "dependencies": {},
                            "url": "https://packages.unity.com",
                        },
                        "com.unity.test-framework": {
                            "version": "1.6.0",
                            "depth": 0,
                            "source": "registry",
                            "dependencies": {},
                            "url": "https://packages.unity.com",
                        },
                    }
                }
            ).encode(),
            "Game/ProjectSettings/ProjectSettings.asset": (
                b"companyName: LocalFoundationLab\n"
                b"productName: SashaAtomicLandPirate_WP0001\n"
                b"local.foundation.sashaatomiclandpirate.wp0001\n"
            ),
            "Game/ProjectSettings/ProjectVersion.txt": (
                b"m_EditorVersion: 6000.3.19f1\n"
                b"m_EditorVersionWithRevision: 6000.3.19f1 (7689f4515d75)\n"
            ),
        }
        with mock.patch.object(
            foundation,
            "git_repo_blob",
            side_effect=lambda _commit, path: blobs.get(path),
        ):
            _, codes = foundation.wp0001_project_seed_content(
                "1" * 40,
                self.base_case["wp0001_toolchain_profile"],
            )
        self.assertIn("wp0001-project-seed-package-version-mismatch", codes)
        self.assertIn("wp0001-project-seed-package-source-invalid", codes)

    def test_evidence_record_rejects_fact_drift(self) -> None:
        expected_facts = {"client_pid": 101, "unity_tool_call_count": 0}
        document = {
            "schema_version": 1,
            "document_kind": "clean-handshake",
            "packet_id": "WP-0001",
            "packet_contract_sha256": (
                "eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da"
                "02abfe008faeda3"
            ),
            "captured_at": "2026-07-16T12:00:00Z",
            "producer_role": "creator",
            "producer_id": "fixture-creator",
            "capture_method": "creator-command-capture",
            "facts_derivation": "fixture derivation",
            "facts": {"client_pid": 101, "unity_tool_call_count": 1},
            "source_artifacts": [
                {
                    "path": (
                        "docs/evidence/WP-0001/a1-activation/commands/"
                        "fixture-handshake.json"
                    ),
                    "sha256": "1" * 64,
                }
            ],
            "known_limitations": [],
            "complete": True,
            "secret_material_included": False,
        }
        errors = foundation.validate_wp0001_evidence_record_data(
            document,
            expected_kind="clean-handshake",
            expected_facts=expected_facts,
            label="fixture handshake",
        )
        self.assertTrue(any("facts differ" in error for error in errors))

    def test_blank_protocol_capture_is_rejected(self) -> None:
        errors = foundation.validate_wp0001_protocol_raw_capture(
            {},
            expected_kind="clean-handshake",
            expected_facts={"captured_at": "2026-07-16T12:05:05Z"},
            enabled_tools=["Unity_ReadConsole"],
        )
        self.assertTrue(errors)

    def test_blank_mcp_live_capture_cannot_self_attest(self) -> None:
        capture = {
            "schema_version": 1,
            "validator_version": foundation.WP0001_MCP_LIVE_VALIDATOR_VERSION,
            "packet_id": "WP-0001",
            "captured_at": "2026-07-16T12:10:05Z",
            "result": "PASS",
            "checks": {
                name: True for name in foundation.WP0001_MCP_LIVE_CHECKS
            },
            "observed": {},
        }
        self.assertTrue(
            foundation.validate_wp0001_mcp_live_capture(
                capture,
                route=self.base_case["unity_mcp_route"],
                runtime=self.base_case["runtime_boundary"],
                route_contract_sha256="a" * 64,
                protected_config_sha256="b" * 64,
            )
        )

    def test_clean_handshake_raw_capture_binds_events_and_inventory(self) -> None:
        collector = {
            "path": (
                "docs/foundation-v0.1/tools/"
                "validate_foundation.py"
            ),
            "sha256": foundation.sha256_file(
                foundation.ROOT / "tools" / "validate_foundation.py"
            ),
        }
        facts = {
            "captured_at": "2026-07-16T12:05:05Z",
            "model_prompt_count": 0,
            "unity_tool_call_count": 0,
        }
        methods = [
            ("client-to-server", "request", "initialize", None),
            ("server-to-client", "response", "initialize", None),
            (
                "client-to-server",
                "notification",
                "notifications/initialized",
                None,
            ),
            (
                "client-to-server",
                "request",
                "tools/list",
                None,
            ),
            (
                "server-to-client",
                "response",
                "tools/list",
                ["Unity_ReadConsole"],
            ),
            ("collector", "state", None, None),
            ("creator", "state", None, None),
        ]
        states = [
            None,
            None,
            None,
            None,
            None,
            "disconnected",
            "approval-revoked",
        ]
        events = []
        for index, ((direction, event_type, method, tools), state) in enumerate(
            zip(methods, states, strict=True)
        ):
            event = {
                "sequence": index,
                "direction": direction,
                "event_type": event_type,
                "record_sha256": f"{index + 1:x}" * 64,
            }
            if method is not None:
                event["method"] = method
            if event_type == "request":
                event["request_id"] = (
                    "1" if method == "initialize" else "2"
                )
            elif event_type == "response":
                event["request_id"] = (
                    "1" if method == "initialize" else "2"
                )
            if tools is not None:
                event["tool_names"] = tools
            if state is not None:
                event["state"] = state
            event["record_sha256"] = foundation.canonical_json_sha256(
                {
                    key: value
                    for key, value in event.items()
                    if key != "record_sha256"
                }
            )
            events.append(event)
        capture = {
            "schema_version": 1,
            "capture_kind": "clean-handshake-raw",
            "packet_id": "WP-0001",
            "captured_at": facts["captured_at"],
            "collector": {
                "name": "fixture",
                "version": "1",
                **collector,
            },
            "command": [
                "/usr/bin/python3",
                collector["path"],
                "clean-handshake",
            ],
            "result": "pass",
            "facts": facts,
            "secret_material_included": False,
            "events": events,
        }
        self.assertEqual(
            [],
            foundation.validate_wp0001_protocol_raw_capture(
                capture,
                expected_kind="clean-handshake",
                expected_facts=facts,
                enabled_tools=["Unity_ReadConsole"],
                expected_collector=collector,
            ),
        )
        capture["command"] = ["/usr/bin/python3", collector["path"], "wrong"]
        self.assertTrue(
            foundation.validate_wp0001_protocol_raw_capture(
                capture,
                expected_kind="clean-handshake",
                expected_facts=facts,
                enabled_tools=["Unity_ReadConsole"],
                expected_collector=collector,
            )
        )
        capture["command"] = [
            "/usr/bin/python3",
            collector["path"],
            "clean-handshake",
        ]
        capture["collector"]["sha256"] = "9" * 64
        self.assertTrue(
            foundation.validate_wp0001_protocol_raw_capture(
                capture,
                expected_kind="clean-handshake",
                expected_facts=facts,
                enabled_tools=["Unity_ReadConsole"],
                expected_collector=collector,
            )
        )
        capture["collector"]["sha256"] = collector["sha256"]
        capture["events"][0]["method"] = "tools/call"
        self.assertTrue(
            foundation.validate_wp0001_protocol_raw_capture(
                capture,
                expected_kind="clean-handshake",
                expected_facts=facts,
                enabled_tools=["Unity_ReadConsole"],
                expected_collector=collector,
            )
        )

    def test_network_raw_capture_rejects_missing_probe_category(self) -> None:
        collector = {
            "path": (
                "docs/foundation-v0.1/tools/"
                "validate_foundation.py"
            ),
            "sha256": foundation.sha256_file(
                foundation.ROOT / "tools" / "validate_foundation.py"
            ),
        }
        facts = {
            "captured_at": "2026-07-16T12:10:04Z",
            "tcp_listener_count": 0,
            "wildcard_listener_count": 0,
            "non_loopback_reachable_listener_count": 0,
            "non_loopback_probe_count": 1,
            "non_loopback_probe_success_count": 0,
            "loopback_probe_count": 1,
            "loopback_probe_success_count": 1,
            "approved_egress_probe_count": 1,
            "approved_egress_probe_success_count": 1,
            "unapproved_egress_probe_count": 1,
            "unapproved_egress_probe_success_count": 0,
            "probe_targets": {
                "non_loopback_listener": (
                    "external-observer://" + "a" * 64
                ),
                "loopback_control": (
                    "unix:///private/tmp/unity-mcp-fixture"
                ),
                "approved_egress": "https://packages.unity.com/",
                "unapproved_egress": "https://example.com/",
            },
        }
        facts["probe_targets_sha256"] = (
            foundation.canonical_json_sha256(facts["probe_targets"])
        )
        categories = (
            (
                "non-loopback-listener",
                "non_loopback_listener",
                False,
            ),
            ("loopback-control", "loopback_control", True),
            ("approved-egress", "approved_egress", True),
            ("unapproved-egress", "unapproved_egress", False),
        )
        probes = [
            {
                "sequence": index,
                "category": category,
                "target": facts["probe_targets"][target_key],
                "attempted": True,
                "success": expected,
                "expected_success": expected,
                "record_sha256": f"{index + 1:x}" * 64,
            }
            for index, (category, target_key, expected) in enumerate(
                categories
            )
        ]
        for probe in probes:
            probe["record_sha256"] = foundation.canonical_json_sha256(
                {
                    key: value
                    for key, value in probe.items()
                    if key != "record_sha256"
                }
            )
        capture = {
            "schema_version": 1,
            "capture_kind": "network-probes",
            "packet_id": "WP-0001",
            "captured_at": facts["captured_at"],
            "collector": {
                "name": "fixture",
                "version": "1",
                **collector,
            },
            "command": [
                "/usr/bin/python3",
                collector["path"],
                "network-observation",
            ],
            "result": "pass",
            "facts": facts,
            "secret_material_included": False,
            "listeners": [],
            "probes": probes,
        }
        self.assertEqual(
            [],
            foundation.validate_wp0001_protocol_raw_capture(
                capture,
                expected_kind="network-observation",
                expected_facts=facts,
                expected_collector=collector,
            ),
        )
        capture["probes"][0]["target"] = "tcp://127.0.0.1:9"
        capture["probes"][0]["record_sha256"] = (
            foundation.canonical_json_sha256(
                {
                    key: value
                    for key, value in capture["probes"][0].items()
                    if key != "record_sha256"
                }
            )
        )
        self.assertTrue(
            foundation.validate_wp0001_protocol_raw_capture(
                capture,
                expected_kind="network-observation",
                expected_facts=facts,
                expected_collector=collector,
            )
        )
        capture["probes"][0]["target"] = facts["probe_targets"][
            "non_loopback_listener"
        ]
        capture["probes"][0]["record_sha256"] = (
            foundation.canonical_json_sha256(
                {
                    key: value
                    for key, value in capture["probes"][0].items()
                    if key != "record_sha256"
                }
            )
        )
        capture["probes"].pop()
        self.assertTrue(
            foundation.validate_wp0001_protocol_raw_capture(
                capture,
                expected_kind="network-observation",
                expected_facts=facts,
                expected_collector=collector,
            )
        )


if __name__ == "__main__":
    unittest.main()
