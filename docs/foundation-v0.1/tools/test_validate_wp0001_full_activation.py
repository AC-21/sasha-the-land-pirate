#!/usr/bin/env python3
"""End-to-end tests for the complete WP-0001 A1 activation validator."""

from __future__ import annotations

import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_foundation as foundation


PACKET_CONTRACT_SHA256 = (
    "eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da"
    "02abfe008faeda3"
)
BOUNDARY_SHA256 = "ab" * 32
BASE_COMMIT = "1" * 40
CAPTURED_AT = "2026-07-16T12:10:05Z"


def raw_sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        indent=2,
        sort_keys=True,
    ).encode("utf-8") + b"\n"


class Wp0001FullActivationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        fixture = foundation.load_json(
            foundation.ROOT
            / "governance"
            / "fixtures"
            / "a1-wp0001-boundary.fixtures.json"
        )
        cls.base_case = fixture["base_case"]

    def write_bytes(self, repo_root: Path, relative: str, data: bytes) -> Path:
        path = repo_root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return path

    def source_ref(self, repo_root: Path, relative: str) -> dict:
        return {
            "path": relative,
            "sha256": raw_sha256((repo_root / relative).read_bytes()),
        }

    def evidence_record(
        self,
        repo_root: Path,
        *,
        kind: str,
        facts: dict,
        source_paths: list[str],
        capture_method: str,
    ) -> dict:
        return {
            "schema_version": 1,
            "document_kind": kind,
            "packet_id": "WP-0001",
            "packet_contract_sha256": PACKET_CONTRACT_SHA256,
            "captured_at": CAPTURED_AT,
            "producer_role": "creator",
            "producer_id": "fixture-creator",
            "capture_method": capture_method,
            "facts_derivation": "Exact fixture sources mapped to boundary facts.",
            "facts": facts,
            "source_artifacts": [
                self.source_ref(repo_root, path) for path in source_paths
            ],
            "known_limitations": [],
            "complete": True,
            "secret_material_included": False,
        }

    def process_capture(
        self,
        expected: dict,
        *,
        executable_path_key: str,
        executable_hash_key: str,
        cwd: str,
        parent_pid: int,
        environment: dict,
    ) -> dict:
        pid_key = (
            "editor_pid" if executable_path_key == "editor_path" else "pid"
        )
        result = {
            "pid": expected[pid_key],
            "parent_pid": parent_pid,
            "uid": expected["principal_uid"],
            "started_at": expected["started_at"],
            "executable_path": expected[executable_path_key],
            "executable_sha256": expected[executable_hash_key],
            "executable_regular": True,
            "executable_symlink": False,
            "cwd": cwd,
            "arguments_sha256": foundation.canonical_json_sha256(
                expected["arguments"]
            ),
            "process_birth_id_sha256": expected[
                "process_birth_id_sha256"
            ],
                "signing_identity": {
                "available": True,
                "strict_verified": True,
                "primary_identity": None,
                "authorities": [],
                "identifier": expected["signing_identity"]["identifier"],
                "team_identifier": expected["signing_identity"][
                    "team_identifier"
                ],
                "cdhash": expected["signing_identity"]["cdhash"],
                "designated_requirement": "fixture requirement",
                "designated_requirement_sha256": expected[
                    "signing_identity"
                ]["designated_requirement_sha256"],
                    "authorities_sha256": expected["signing_identity"][
                        "authorities_sha256"
                    ],
                    "signature": None,
                    "executable_vnode": {
                        "device": 1,
                        "inode": expected[pid_key],
                        "size": 4096,
                        "mtime_ns": 1,
                        "stable_across_inspection": True,
                    },
                },
            "inspection_error": None,
        }
        environment_names = sorted(environment)
        result["environment"] = {
            "values": environment,
            "names": environment_names,
            "duplicate_names": [],
            "names_sha256": foundation.canonical_json_sha256(
                environment_names
            ),
            "absent_variable_names_present": [],
        }
        return result

    def fd_graph_capture(
        self,
        *,
        client_pid: int,
        relay_pid: int,
        editor_pid: int,
        reported_endpoint: str,
        canonical_endpoint: str,
    ) -> dict:
        unix_hash = raw_sha256(b"fixture-editor-relay-unix")
        stdin_hash = raw_sha256(b"fixture-client-relay-stdin")
        stdout_hash = raw_sha256(b"fixture-client-relay-stdout")

        def descriptor(
            *,
            access: str,
            channel_hash: str,
            fd: int,
            descriptor_type: str,
            state: str = "connected",
        ) -> dict:
            return {
                "access": access,
                "channel_address_sha256": channel_hash,
                "fd": fd,
                "state": state,
                "type": descriptor_type,
            }

        graph = {
            "schema_version": 1,
            "endpoint": {
                "canonical_path": canonical_endpoint,
                "canonical_path_sha256": raw_sha256(
                    canonical_endpoint.encode("utf-8")
                ),
                "accepted_path_sha256s": sorted(
                    {
                        raw_sha256(reported_endpoint.encode("utf-8")),
                        raw_sha256(canonical_endpoint.encode("utf-8")),
                    }
                ),
            },
            "channels": {
                "editor_relay_unix": {
                    "address_sha256": unix_hash,
                    "editor_fd": 7,
                    "relay_fd": 8,
                },
                "client_relay_stdin": {
                    "address_pair_sha256": stdin_hash,
                    "client_fd": 9,
                    "relay_fd": 0,
                },
                "client_relay_stdout": {
                    "address_pair_sha256": stdout_hash,
                    "client_fd": 10,
                    "relay_fd": 1,
                },
            },
            "processes": [
                {
                    "role": "client",
                    "pid": client_pid,
                    "inspection_complete": True,
                    "inspection_error": None,
                    "descriptors": [
                        descriptor(
                            access="w",
                            channel_hash=stdin_hash,
                            fd=9,
                            descriptor_type="pipe",
                        ),
                        descriptor(
                            access="r",
                            channel_hash=stdout_hash,
                            fd=10,
                            descriptor_type="pipe",
                        ),
                    ],
                },
                {
                    "role": "editor",
                    "pid": editor_pid,
                    "inspection_complete": True,
                    "inspection_error": None,
                    "descriptors": [
                        descriptor(
                            access="u",
                            channel_hash=unix_hash,
                            fd=7,
                            descriptor_type="unix",
                            state="open",
                        )
                    ],
                },
                {
                    "role": "relay",
                    "pid": relay_pid,
                    "inspection_complete": True,
                    "inspection_error": None,
                    "descriptors": [
                        descriptor(
                            access="r",
                            channel_hash=stdin_hash,
                            fd=0,
                            descriptor_type="pipe",
                        ),
                        descriptor(
                            access="w",
                            channel_hash=stdout_hash,
                            fd=1,
                            descriptor_type="pipe",
                        ),
                        descriptor(
                            access="u",
                            channel_hash=unix_hash,
                            fd=8,
                            descriptor_type="unix",
                        ),
                    ],
                },
            ],
            "residuals": [],
        }
        return {
            "graph": graph,
            "sha256": foundation.canonical_json_sha256(graph),
        }

    def raw_protocol_capture(
        self,
        *,
        kind: str,
        facts: dict,
        enabled_tools: list[str],
        collector: dict,
    ) -> dict:
        if kind == "clean-handshake":
            capture_kind = "clean-handshake-raw"
            events = [
                {
                    "sequence": 0,
                    "direction": "client-to-server",
                    "event_type": "request",
                    "method": "initialize",
                    "request_id": "1",
                    "record_sha256": raw_sha256(b"handshake-initialize"),
                },
                {
                    "sequence": 1,
                    "direction": "server-to-client",
                    "event_type": "response",
                    "method": "initialize",
                    "request_id": "1",
                    "record_sha256": raw_sha256(
                        b"handshake-initialize-response"
                    ),
                },
                {
                    "sequence": 2,
                    "direction": "client-to-server",
                    "event_type": "notification",
                    "method": "notifications/initialized",
                    "record_sha256": raw_sha256(b"handshake-initialized"),
                },
                {
                    "sequence": 3,
                    "direction": "client-to-server",
                    "event_type": "request",
                    "method": "tools/list",
                    "request_id": "2",
                    "record_sha256": raw_sha256(b"handshake-tools-request"),
                },
                {
                    "sequence": 4,
                    "direction": "server-to-client",
                    "event_type": "response",
                    "method": "tools/list",
                    "request_id": "2",
                    "tool_names": enabled_tools,
                    "record_sha256": raw_sha256(b"handshake-tools"),
                },
                {
                    "sequence": 5,
                    "direction": "collector",
                    "event_type": "state",
                    "state": "disconnected",
                    "record_sha256": raw_sha256(b"handshake-disconnected"),
                },
                {
                    "sequence": 6,
                    "direction": "creator",
                    "event_type": "state",
                    "state": "approval-revoked",
                    "record_sha256": raw_sha256(b"handshake-revoked"),
                },
            ]
        else:
            capture_kind = "activation-session-live"
            events = [
                {
                    "sequence": 0,
                    "direction": "bridge",
                    "event_type": "state",
                    "method": "connection/accepted",
                    "state": "connected",
                    "record_sha256": raw_sha256(b"session-connected"),
                },
                {
                    "sequence": 1,
                    "direction": "creator",
                    "event_type": "state",
                    "method": "approval/creator-approved",
                    "state": "creator-approved",
                    "record_sha256": raw_sha256(b"session-approved"),
                },
                {
                    "sequence": 2,
                    "direction": "client-to-server",
                    "event_type": "request",
                    "method": "initialize",
                    "request_id": "1",
                    "record_sha256": raw_sha256(
                        b"session-initialize-request"
                    ),
                },
                {
                    "sequence": 3,
                    "direction": "server-to-client",
                    "event_type": "response",
                    "method": "initialize",
                    "request_id": "1",
                    "record_sha256": raw_sha256(
                        b"session-initialize-response"
                    ),
                },
                {
                    "sequence": 4,
                    "direction": "client-to-server",
                    "event_type": "notification",
                    "method": "notifications/initialized",
                    "record_sha256": raw_sha256(
                        b"session-initialized"
                    ),
                },
                {
                    "sequence": 5,
                    "direction": "client-to-server",
                    "event_type": "request",
                    "method": "tools/list",
                    "request_id": "2",
                    "record_sha256": raw_sha256(b"session-tools-request"),
                },
                {
                    "sequence": 6,
                    "direction": "server-to-client",
                    "event_type": "response",
                    "method": "tools/list",
                    "request_id": "2",
                    "tool_names": enabled_tools,
                    "record_sha256": raw_sha256(b"session-tools"),
                },
                {
                    "sequence": 7,
                    "direction": "collector",
                    "event_type": "state",
                    "state": "connected",
                    "record_sha256": raw_sha256(b"session-live"),
                },
            ]
        for event in events:
            event["record_sha256"] = foundation.canonical_json_sha256(
                {
                    key: value
                    for key, value in event.items()
                    if key != "record_sha256"
                }
            )
        return {
            "schema_version": 1,
            "capture_kind": capture_kind,
            "packet_id": "WP-0001",
            "captured_at": facts["captured_at"],
            "collector": {
                "name": "fixture-protocol-collector",
                "version": "1",
                **collector,
            },
            "command": ["/usr/bin/python3", collector["path"], kind],
            "result": "pass",
            "facts": facts,
            "events": events,
            "secret_material_included": False,
        }

    def raw_network_capture(self, facts: dict, collector: dict) -> dict:
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
                "success": expected_success,
                "expected_success": expected_success,
                "record_sha256": raw_sha256(
                    f"network-{category}".encode("utf-8")
                ),
            }
            for index, (
                category,
                target_key,
                expected_success,
            ) in enumerate(categories)
        ]
        for probe in probes:
            probe["record_sha256"] = foundation.canonical_json_sha256(
                {
                    key: value
                    for key, value in probe.items()
                    if key != "record_sha256"
                }
            )
        return {
            "schema_version": 1,
            "capture_kind": "network-probes",
            "packet_id": "WP-0001",
            "captured_at": facts["captured_at"],
            "collector": {
                "name": "fixture-network-collector",
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
            "listeners": [],
            "probes": probes,
            "secret_material_included": False,
        }

    def raw_policy_attachment_capture(
        self,
        facts: dict,
        collector: dict,
    ) -> dict:
        subjects = []
        for sequence, role in enumerate(("client", "relay", "editor")):
            subject = {
                "sequence": sequence,
                "role": role,
                "pid": facts[f"{role}_pid"],
                "process_birth_id_sha256": facts[
                    f"{role}_process_birth_id_sha256"
                ],
                "principal_uid": facts["principal_uid"],
                "sandbox_policy_sha256": facts[
                    "sandbox_policy_sha256"
                ],
                "network_policy_sha256": facts[
                    "network_policy_sha256"
                ],
                "sandbox_attachment_mode": "kernel-sandbox-query",
                "network_attachment_mode": (
                    "kernel-network-policy-query"
                ),
                "sandbox_attachment_handle_sha256": raw_sha256(
                    f"sandbox-{role}".encode("utf-8")
                ),
                "network_attachment_handle_sha256": raw_sha256(
                    f"network-{role}".encode("utf-8")
                ),
                "sandbox_attached": True,
                "network_attached": True,
            }
            subject["record_sha256"] = (
                foundation.canonical_json_sha256(subject)
            )
            subjects.append(subject)
        return {
            "schema_version": 1,
            "capture_kind": "policy-attachment",
            "packet_id": "WP-0001",
            "captured_at": facts["captured_at"],
            "collector": {
                "name": "fixture-policy-attachment-collector",
                "version": "1",
                **collector,
            },
            "command": [
                "/usr/bin/python3",
                collector["path"],
                "policy-attachment",
            ],
            "result": "pass",
            "facts": facts,
            "subjects": subjects,
            "capture_complete": True,
            "secret_material_included": False,
        }

    def run_full_case(
        self,
        *,
        blank_mcp_capture: bool = False,
        extra_runtime_server: bool = False,
        endpoint_is_socket: bool = True,
        endpoint_canonical_drift: bool = False,
        unstable_executable_vnode: bool = False,
        fd_graph_drift: bool = False,
        policy_attachment_drift: bool = False,
        malformed_evidence: bool = False,
        missing_collector_claim: bool = False,
        local_activation_receipt: bool = False,
        inert_collector_source: bool = False,
        approved_credential_drift: bool = False,
    ) -> list[str]:
        with tempfile.TemporaryDirectory() as temporary:
            repo_root = Path(temporary) / "repo"
            foundation_root = repo_root / "docs" / "foundation-v0.1"
            repo_root = repo_root.resolve()
            foundation_root = foundation_root.resolve()
            boundary_path = (
                foundation_root / "governance" / "a1-boundaries" / "WP-0001.json"
            )
            boundary_path.parent.mkdir(parents=True, exist_ok=True)

            manifest = copy.deepcopy(self.base_case)
            manifest.update(
                {
                    "schema_version": 4,
                    "manifest_id": "A1B-WP-0001-FULL-TEST",
                    "packet_id": "WP-0001",
                    "packet_contract_sha256": PACKET_CONTRACT_SHA256,
                    "created_at": "2026-07-16T12:10:06Z",
                    "attested_by": "fixture-creator",
                    "attestation_receipt_id": "RR-WP0001-FULL-TEST",
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
                            "docs/foundation-v0.1/ledger/decisions.jsonl",
                            "docs/foundation-v0.1/governance/",
                            "docs/foundation-v0.1/ledger/receipts/",
                            ".git/refs/heads/main",
                            ".codex/",
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
            manifest["repository"]["base_commit"] = BASE_COMMIT
            if approved_credential_drift:
                manifest["credential_boundary"][
                    "approved_credential_ids"
                ] = ["github-write"]
            manifest["project_seed"]["base_commit"] = BASE_COMMIT
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

            seed_files = {
                "Game/Packages/manifest.json": json_bytes(
                    {
                        "enableLockFile": True,
                        "resolutionStrategy": "lowest",
                        "dependencies": {
                            "com.unity.ai.assistant": "2.14.0-pre.1",
                            "com.unity.render-pipelines.universal": "17.3.0",
                            "com.unity.test-framework": "1.6.0",
                        },
                    }
                ),
                "Game/Packages/packages-lock.json": json_bytes(
                    {
                        "dependencies": {
                            package: {
                                "version": version,
                                "depth": 0,
                                "source": "registry",
                                "dependencies": {},
                                "url": "https://packages.unity.com",
                            }
                            for package, version in {
                                "com.unity.ai.assistant": "2.14.0-pre.1",
                                "com.unity.render-pipelines.universal": "17.3.0",
                                "com.unity.test-framework": "1.6.0",
                            }.items()
                        }
                    }
                ),
                "Game/ProjectSettings/ProjectSettings.asset": (
                    b"companyName: LocalFoundationLab\n"
                    b"productName: SashaAtomicLandPirate_WP0001\n"
                    b"Standalone: local.foundation.sashaatomiclandpirate.wp0001\n"
                ),
                "Game/ProjectSettings/ProjectVersion.txt": (
                    b"m_EditorVersion: 6000.3.19f1\n"
                    b"m_EditorVersionWithRevision: 6000.3.19f1 "
                    b"(7689f4515d75)\n"
                ),
            }
            for relative, data in seed_files.items():
                self.write_bytes(repo_root, relative, data)
            tree_listing = b"".join(
                (
                    f"100644 blob {index:040x}\t{relative}\0".encode("utf-8")
                    for index, relative in enumerate(seed_files, start=1)
                )
            )
            tree_sha256 = raw_sha256(tree_listing)
            manifest["project_seed"]["git_tree_sha256"] = tree_sha256

            protected_config = (
                b"[mcp_servers.unity_mcp]\n"
                b'command = "/Users/sasha/.unity/relay/relay_mac_arm64.app/'
                b'Contents/MacOS/relay_mac_arm64"\n'
                b'args = ["--mcp", "--project-path", '
                b'"/REPLACE-WITH-RECEIPT-BOUND-A1-CLONE/Game"]\n'
                b"startup_timeout_sec = 120\n"
                b"enabled = false\n"
            )
            self.write_bytes(repo_root, ".codex/config.toml", protected_config)

            route = manifest["unity_mcp_route"]
            policy = route["codex_policy"]
            relay = route["relay"]
            runtime_config = (
                'approval_policy = "on-request"\n'
                "\n"
                "[mcp_servers.unity_mcp_a1_wp0001]\n"
                f'command = "{relay["path"]}"\n'
                "args = ["
                + ", ".join(json.dumps(item) for item in relay["arguments"])
                + "]\n"
                "enabled = true\n"
                "required = true\n"
                'enabled_tools = ["Unity_ReadConsole"]\n'
                'default_tools_approval_mode = "prompt"\n'
                "\n"
                "[mcp_servers.unity_mcp_a1_wp0001.env]\n"
                + "\n".join(
                    f"{key} = {json.dumps(value)}"
                    for key, value in {
                        **policy["environment_bindings"],
                        **{
                            key: policy["client_environment_guard"][key]
                            for key in (
                                "CODEX_HOME",
                                "XDG_CONFIG_HOME",
                                "XDG_CACHE_HOME",
                                "XDG_DATA_HOME",
                                "GIT_CONFIG_NOSYSTEM",
                                "GIT_CONFIG_GLOBAL",
                                "GIT_TERMINAL_PROMPT",
                            )
                        },
                    }.items()
                )
                + "\n"
            )
            if extra_runtime_server:
                runtime_config += (
                    "\n[mcp_servers.unexpected_override]\n"
                    'command = "/usr/bin/false"\n'
                    "args = []\n"
                    "enabled = true\n"
                )
            runtime_config_bytes = runtime_config.encode("utf-8")
            runtime_config_path = foundation.WP0001_RUNTIME_CONFIG_EVIDENCE_PATH
            self.write_bytes(repo_root, runtime_config_path, runtime_config_bytes)
            runtime_config_sha256 = raw_sha256(runtime_config_bytes)
            policy["evidence"]["sha256"] = runtime_config_sha256
            route["handshake"]["runtime_config_sha256"] = runtime_config_sha256
            route["activation_session"][
                "runtime_config_sha256"
            ] = runtime_config_sha256

            effective_inventory = [
                {
                    "layer": "candidate",
                    "path": (
                        f'{manifest["repository"]["absolute_root"]}/'
                        ".codex/config.toml"
                    ),
                    "sha256": raw_sha256(protected_config),
                    "top_level_keys": ["mcp_servers"],
                    "servers": [
                        {"enabled": False, "name": "unity_mcp"}
                    ],
                },
                {
                    "layer": "runtime",
                    "path": policy["runtime_config_path"],
                    "sha256": runtime_config_sha256,
                    "top_level_keys": [
                        "approval_policy",
                        "mcp_servers",
                    ],
                    "servers": [
                        {
                            "enabled": True,
                            "name": policy["server_name"],
                        }
                    ],
                },
            ]
            inventory_sha256 = foundation.canonical_json_sha256(
                effective_inventory
            )
            policy["effective_server_inventory_sha256"] = inventory_sha256
            route["client"]["server_inventory_sha256"] = inventory_sha256
            route["handshake"]["server_inventory_sha256"] = inventory_sha256
            route["activation_session"][
                "server_inventory_sha256"
            ] = inventory_sha256

            sandbox_policy = b"(version 1)\n(deny default)\n"
            network_policy = b"network-policy fixture deny-by-default\n"
            sandbox_path = (
                "docs/evidence/WP-0001/a1-activation/sandbox.policy"
            )
            network_path = (
                "docs/evidence/WP-0001/a1-activation/network.policy"
            )
            self.write_bytes(repo_root, sandbox_path, sandbox_policy)
            self.write_bytes(repo_root, network_path, network_policy)
            sandbox_sha256 = raw_sha256(sandbox_policy)
            network_sha256 = raw_sha256(network_policy)
            manifest["approved_environment"][
                "sandbox_profile_sha256"
            ] = sandbox_sha256
            manifest["approved_environment"][
                "network_policy_sha256"
            ] = network_sha256
            manifest["activation_evidence"]["sandbox_policy"][
                "sha256"
            ] = sandbox_sha256
            manifest["activation_evidence"]["network_policy"][
                "sha256"
            ] = network_sha256
            route["controls"]["network_policy_sha256"] = network_sha256

            placeholder_sources = {
                "docs/evidence/WP-0001/a1-activation/commands/toolchain.txt": (
                    b"fixture toolchain inventory\n"
                ),
                "docs/evidence/WP-0001/a1-activation/screenshots/toolchain.png": (
                    b"fixture-toolchain-png"
                ),
                "docs/evidence/WP-0001/a1-activation/screenshots/seat.png": (
                    b"fixture-seat-png"
                ),
                "docs/evidence/WP-0001/a1-activation/screenshots/identity.png": (
                    b"fixture-identity-png"
                ),
                "docs/evidence/WP-0001/pre-a1-readiness-20260716.json": (
                    json_bytes({"result": "preserved"})
                ),
                foundation.WP0001_MCP_LIVE_VERIFIER_PATH: (
                    b"# fixture-bound MCP live verifier bytes\n"
                ),
                foundation.WP0001_QUARANTINE_LIVE_VERIFIER_PATH: (
                    b"# fixture-bound quarantine verifier bytes\n"
                ),
                manifest["raw_capture_collectors"]["protocol"]["path"]: (
                    (
                        b"# inert fixture collector\n"
                        if inert_collector_source
                        else (
                            b"def main():\n    return 0\n\n"
                            b"if __name__ == \"__main__\":\n"
                            b"    raise SystemExit(main())\n"
                        )
                    )
                ),
                manifest["raw_capture_collectors"]["network"]["path"]: (
                    b"def main():\n    return 0\n\n"
                    b"if __name__ == \"__main__\":\n    raise SystemExit(main())\n"
                ),
                manifest["raw_capture_collectors"][
                    "policy_attachment"
                ]["path"]: (
                    b"def main():\n    return 0\n\n"
                    b"if __name__ == \"__main__\":\n    raise SystemExit(main())\n"
                ),
            }
            for relative, data in placeholder_sources.items():
                self.write_bytes(repo_root, relative, data)
            for collector_kind in (
                "protocol",
                "network",
                "policy_attachment",
            ):
                collector_ref = manifest["raw_capture_collectors"][
                    collector_kind
                ]
                collector_ref["sha256"] = raw_sha256(
                    (
                        repo_root / collector_ref["path"]
                    ).read_bytes()
                )
            protocol_collector = copy.deepcopy(
                manifest["raw_capture_collectors"]["protocol"]
            )
            network_collector = copy.deepcopy(
                manifest["raw_capture_collectors"]["network"]
            )
            policy_attachment_collector = copy.deepcopy(
                manifest["raw_capture_collectors"][
                    "policy_attachment"
                ]
            )

            quarantine_live_path = (
                "docs/evidence/WP-0001/a1-activation/commands/"
                "quarantine-live.json"
            )
            runtime = manifest["runtime_boundary"]
            quarantine_live = {
                "schema_version": 1,
                "validator_version": "wp0001-a1-live-v1",
                "packet_id": "WP-0001",
                "captured_at": "2026-07-16T12:10:03Z",
                "result": "pass",
                "candidate_root": manifest["repository"]["absolute_root"],
                "base_commit": BASE_COMMIT,
                "checks": {
                    name: True
                    for name in foundation.WP0001_LIVE_QUARANTINE_CHECKS
                },
                "observed": {
                    "trusted_root": manifest["repository"]["trusted_root"],
                    "principal_uid": runtime["principal_uid"],
                    "environment_bindings": runtime["environment_bindings"],
                    "client_environment": {
                        key: runtime["client_environment_guard"][key]
                        for key in (
                            "CODEX_HOME",
                            "XDG_CONFIG_HOME",
                            "XDG_CACHE_HOME",
                            "XDG_DATA_HOME",
                            "GIT_CONFIG_NOSYSTEM",
                            "GIT_CONFIG_GLOBAL",
                            "GIT_TERMINAL_PROMPT",
                        )
                    },
                    "boot_session_sha256": runtime[
                        "boot_session_sha256"
                    ],
                    "runtime_home": runtime["ephemeral_home_root"],
                    "runtime_temp": runtime["private_temp_root"],
                    "shared_temp_roots": runtime[
                        "ambient_shared_temp_roots"
                    ],
                    "socket_exception": runtime[
                        "ambient_shared_temp_write_exceptions"
                    ][0],
                    "head": BASE_COMMIT,
                    "git_directory": (
                        f'{manifest["repository"]["absolute_root"]}/.git'
                    ),
                    "git_common_directory": (
                        f'{manifest["repository"]["absolute_root"]}/.git'
                    ),
                    "status_porcelain": [],
                    "remotes": [],
                    "symbolic_head": None,
                    "forbidden_credential_env_keys_present": [],
                },
            }
            self.write_bytes(
                repo_root,
                quarantine_live_path,
                json_bytes(quarantine_live),
            )

            guard = policy["client_environment_guard"]
            client_environment = {
                **policy["environment_bindings"],
                **{
                    key: guard[key]
                    for key in (
                        "CODEX_HOME",
                        "XDG_CONFIG_HOME",
                        "XDG_CACHE_HOME",
                        "XDG_DATA_HOME",
                        "GIT_CONFIG_NOSYSTEM",
                        "GIT_CONFIG_GLOBAL",
                        "GIT_TERMINAL_PROMPT",
                    )
                },
            }
            client = route["client"]
            bridge = route["bridge"]
            relay_capture = self.process_capture(
                relay,
                executable_path_key="path",
                executable_hash_key="sha256",
                cwd=client["cwd"],
                parent_pid=client["pid"],
                environment=client_environment,
            )
            relay_capture["package_copy"] = {
                "path": relay["package_copy_path"],
                "sha256": relay["package_copy_sha256"],
                "regular": True,
                "symlink": False,
            }
            fd_graph_capture = self.fd_graph_capture(
                client_pid=client["pid"],
                relay_pid=relay["pid"],
                editor_pid=bridge["editor_pid"],
                reported_endpoint=bridge["endpoint"],
                canonical_endpoint=runtime[
                    "ambient_shared_temp_write_exceptions"
                ][0],
            )
            activation_session = route["activation_session"]
            activation_session["connection_record_sha256"] = bridge[
                "connection_file_sha256"
            ]
            activation_session["fd_graph_sha256"] = fd_graph_capture[
                "sha256"
            ]
            activation_session["session_id_sha256"] = (
                foundation.wp0001_session_identity_sha256(
                    route,
                    runtime,
                    fd_graph_capture["sha256"],
                )
            )
            mcp_live = {
                "schema_version": 1,
                "validator_version": foundation.WP0001_MCP_LIVE_VALIDATOR_VERSION,
                "packet_id": "WP-0001",
                "captured_at": CAPTURED_AT,
                "result": "PASS",
                "checks": {
                    name: True for name in foundation.WP0001_MCP_LIVE_CHECKS
                },
                "observed": {
                    "boot_session_sha256": runtime[
                        "boot_session_sha256"
                    ],
                    "route_contract_sha256": (
                        foundation.canonical_json_sha256(
                            foundation.wp0001_mcp_route_contract(manifest)
                        )
                    ),
                    "client": self.process_capture(
                        client,
                        executable_path_key="path",
                        executable_hash_key="sha256",
                        cwd=client["cwd"],
                        parent_pid=1,
                        environment=client_environment,
                    ),
                    "relay": relay_capture,
                    "bridge": self.process_capture(
                        bridge,
                        executable_path_key="editor_path",
                        executable_hash_key="editor_sha256",
                        cwd=bridge["cwd"],
                        parent_pid=1,
                        environment=runtime["environment_bindings"],
                    ),
                    "runtime_config_sha256": runtime_config_sha256,
                    "protected_config_sha256": raw_sha256(protected_config),
                    "environment_sha256": policy["environment_sha256"],
                    "enabled_tools_sha256": policy[
                        "enabled_tools_sha256"
                    ],
                    "effective_server_inventory": effective_inventory,
                    "effective_server_inventory_sha256": inventory_sha256,
                    "connection_file": {
                        "path": bridge["connection_file"],
                        "sha256": bridge["connection_file_sha256"],
                        "error": None,
                        "record": {
                            "connection_type": bridge[
                                "discovery_connection_type"
                            ],
                            "connection_path": bridge["endpoint"],
                            "created_date": "2026-07-16T12:09:59Z",
                            "editor_pid": bridge["editor_pid"],
                            "project_path": bridge["project_path"],
                            "protocol_version": bridge["protocol_version"],
                        },
                    },
                    "endpoint": {
                        "path": bridge["endpoint"],
                        "canonical_path": (
                            "/private/tmp/unity-mcp-drift"
                            if endpoint_canonical_drift
                            else runtime[
                                "ambient_shared_temp_write_exceptions"
                            ][0]
                        ),
                        "exists": True,
                        "is_socket": endpoint_is_socket,
                        "is_symlink": False,
                        "uid": bridge["endpoint_owner_uid"],
                        "mode": bridge["endpoint_mode"],
                    },
                    "fd_graph": fd_graph_capture,
                },
            }
            if unstable_executable_vnode:
                mcp_live["observed"]["client"]["signing_identity"][
                    "executable_vnode"
                ]["stable_across_inspection"] = False
            if fd_graph_drift:
                fd_graph = mcp_live["observed"]["fd_graph"]
                fd_graph["graph"]["channels"]["client_relay_stdin"][
                    "client_fd"
                ] = 999
                fd_graph["sha256"] = foundation.canonical_json_sha256(
                    fd_graph["graph"]
                )
            if blank_mcp_capture:
                mcp_live = {}
            mcp_live_path = foundation.WP0001_REQUIRED_RAW_SOURCE_BY_KIND[
                "mcp-route"
            ]
            self.write_bytes(repo_root, mcp_live_path, json_bytes(mcp_live))

            handshake = route["handshake"]
            handshake_facts = {
                "client_pid": handshake["client_pid"],
                "relay_pid": handshake["relay_pid"],
                "editor_pid": handshake["editor_pid"],
                "client_started_at": handshake["client_started_at"],
                "relay_started_at": handshake["relay_started_at"],
                "editor_started_at": handshake["editor_started_at"],
                "client_process_birth_id_sha256": handshake[
                    "client_process_birth_id_sha256"
                ],
                "relay_process_birth_id_sha256": handshake[
                    "relay_process_birth_id_sha256"
                ],
                "editor_process_birth_id_sha256": handshake[
                    "editor_process_birth_id_sha256"
                ],
                "project_path": bridge["project_path"],
                "runtime_config_sha256": handshake[
                    "runtime_config_sha256"
                ],
                "enabled_tools_sha256": handshake[
                    "enabled_tools_sha256"
                ],
                "environment_sha256": handshake["environment_sha256"],
                "server_inventory_sha256": handshake[
                    "server_inventory_sha256"
                ],
                "captured_at": handshake["captured_at"],
                "observed_methods": handshake["observed_methods"],
                "capture_complete": handshake["capture_complete"],
                "model_prompt_count": handshake["model_prompt_count"],
                "unity_tool_call_count": handshake[
                    "unity_tool_call_count"
                ],
                "disconnected": handshake["disconnected"],
                "approval_revoked_after_disconnect": handshake[
                    "approval_revoked_after_disconnect"
                ],
            }
            handshake_raw_path = foundation.WP0001_REQUIRED_RAW_SOURCE_BY_KIND[
                "clean-handshake"
            ]
            self.write_bytes(
                repo_root,
                handshake_raw_path,
                json_bytes(
                    self.raw_protocol_capture(
                        kind="clean-handshake",
                        facts=handshake_facts,
                        enabled_tools=policy["enabled_tools"],
                        collector=protocol_collector,
                    )
                ),
            )

            activation_session = route["activation_session"]
            activation_session_facts = {
                key: value
                for key, value in activation_session.items()
                if key != "evidence"
            }
            activation_raw_path = foundation.WP0001_REQUIRED_RAW_SOURCE_BY_KIND[
                "activation-session"
            ]
            self.write_bytes(
                repo_root,
                activation_raw_path,
                json_bytes(
                    self.raw_protocol_capture(
                        kind="activation-session",
                        facts=activation_session_facts,
                        enabled_tools=policy["enabled_tools"],
                        collector=protocol_collector,
                    )
                ),
            )

            controls = route["controls"]
            network_facts = {
                **{
                    key: value
                    for key, value in controls.items()
                    if key != "observation"
                },
                "network_policy_sha256": manifest["approved_environment"][
                    "network_policy_sha256"
                ],
            }
            network_raw_path = foundation.WP0001_REQUIRED_RAW_SOURCE_BY_KIND[
                "network-observation"
            ]
            self.write_bytes(
                repo_root,
                network_raw_path,
                json_bytes(
                    self.raw_network_capture(
                        network_facts,
                        network_collector,
                    )
                ),
            )
            policy_attachment_facts = {
                "captured_at": controls["captured_at"],
                "principal_uid": runtime["principal_uid"],
                "boot_session_sha256": runtime[
                    "boot_session_sha256"
                ],
                "sandbox_policy_sha256": manifest[
                    "approved_environment"
                ]["sandbox_profile_sha256"],
                "network_policy_sha256": manifest[
                    "approved_environment"
                ]["network_policy_sha256"],
                "client_pid": client["pid"],
                "relay_pid": relay["pid"],
                "editor_pid": bridge["editor_pid"],
                "client_process_birth_id_sha256": client[
                    "process_birth_id_sha256"
                ],
                "relay_process_birth_id_sha256": relay[
                    "process_birth_id_sha256"
                ],
                "editor_process_birth_id_sha256": bridge[
                    "process_birth_id_sha256"
                ],
            }
            policy_attachment_raw_path = (
                foundation.WP0001_POLICY_ATTACHMENT_RAW_PATH
            )
            policy_attachment_capture = (
                self.raw_policy_attachment_capture(
                    policy_attachment_facts,
                    policy_attachment_collector,
                )
            )
            if policy_attachment_drift:
                subject = policy_attachment_capture["subjects"][0]
                subject["network_attached"] = False
                subject["record_sha256"] = (
                    foundation.canonical_json_sha256(
                        {
                            key: value
                            for key, value in subject.items()
                            if key != "record_sha256"
                        }
                    )
                )
            self.write_bytes(
                repo_root,
                policy_attachment_raw_path,
                json_bytes(policy_attachment_capture),
            )

            seed_facts = {
                "project_root": "Game",
                "game_tree_sha256": tree_sha256,
                "required_seed_files": list(seed_files),
                "editor_version": "6000.3.19f1",
                "editor_changeset": "7689f4515d75",
                "assistant_package_version": "2.14.0-pre.1",
                "resolved_urp_version": "17.3.0",
                "resolved_test_framework_version": "1.6.0",
                "manifest_dependencies": {
                    "com.unity.ai.assistant": "2.14.0-pre.1",
                    "com.unity.render-pipelines.universal": "17.3.0",
                    "com.unity.test-framework": "1.6.0",
                },
                "resolved_package_lock": json.loads(
                    seed_files[
                        "Game/Packages/packages-lock.json"
                    ].decode("utf-8")
                )["dependencies"],
                "package_lock_enabled": True,
                "resolution_strategy": "lowest",
                "company": "LocalFoundationLab",
                "product": "SashaAtomicLandPirate_WP0001",
                "bundle_id": (
                    "local.foundation.sashaatomiclandpirate.wp0001"
                ),
                "creator_attested_no_implementation": True,
            }
            entitlement = route["entitlement"]
            identity = route["project_identity"]
            route_facts = json.loads(json.dumps(route))
            route_facts.pop("process_observation", None)
            for section, key in (
                ("bridge", "discovery_record"),
                ("entitlement", "evidence"),
                ("project_identity", "evidence"),
                ("codex_policy", "evidence"),
                ("handshake", "transcript"),
                ("activation_session", "evidence"),
                ("controls", "observation"),
            ):
                route_facts[section].pop(key, None)
            bridge_facts = {
                "connection_type": bridge["discovery_connection_type"],
                "connection_path": bridge["endpoint"],
                "project_path": bridge["project_path"],
                "protocol_version": bridge["protocol_version"],
                "editor_pid": bridge["editor_pid"],
                "source_file": bridge["connection_file"],
                "source_file_sha256": bridge[
                    "connection_file_sha256"
                ],
                "endpoint_owner_uid": bridge["endpoint_owner_uid"],
                "endpoint_mode": bridge["endpoint_mode"],
            }
            records = {
                "docs/evidence/WP-0001/a1-activation/project-seed.json": (
                    self.evidence_record(
                        repo_root,
                        kind="project-seed",
                        facts=seed_facts,
                        source_paths=list(seed_files),
                        capture_method="protected-git-derivation",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/toolchain.json": (
                    self.evidence_record(
                        repo_root,
                        kind="toolchain",
                        facts={
                            "approved_toolchain": manifest[
                                "approved_toolchain"
                            ],
                            "profile": manifest[
                                "wp0001_toolchain_profile"
                            ],
                        },
                        source_paths=[
                            "docs/evidence/WP-0001/a1-activation/commands/"
                            "toolchain.txt",
                            "docs/evidence/WP-0001/a1-activation/screenshots/"
                            "toolchain.png",
                        ],
                        capture_method="creator-command-and-ui-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/entitlement-linkage.json": (
                    self.evidence_record(
                        repo_root,
                        kind="entitlement-linkage",
                        facts={
                            key: value
                            for key, value in entitlement.items()
                            if key != "evidence"
                        },
                        source_paths=[
                            "docs/evidence/WP-0001/a1-activation/screenshots/"
                            "seat.png"
                        ],
                        capture_method="creator-ui-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/project-identity.json": (
                    self.evidence_record(
                        repo_root,
                        kind="project-identity",
                        facts={
                            key: value
                            for key, value in identity.items()
                            if key != "evidence"
                        },
                        source_paths=[
                            "docs/evidence/WP-0001/a1-activation/screenshots/"
                            "identity.png"
                        ],
                        capture_method="creator-ui-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/quarantine.json": (
                    self.evidence_record(
                        repo_root,
                        kind="quarantine",
                        facts={
                            "repository": manifest["repository"],
                            "approved_environment": manifest[
                                "approved_environment"
                            ],
                            "runtime_boundary": runtime,
                            "protection_boundary": manifest[
                                "protection_boundary"
                            ],
                            "credential_boundary": manifest[
                                "credential_boundary"
                            ],
                            "manual_import_boundary": manifest[
                                "manual_import_boundary"
                            ],
                        },
                        source_paths=[
                            sandbox_path,
                            network_path,
                            foundation.WP0001_QUARANTINE_LIVE_VERIFIER_PATH,
                            quarantine_live_path,
                            policy_attachment_raw_path,
                            policy_attachment_collector["path"],
                        ],
                        capture_method="creator-command-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/mcp-route.json": (
                    self.evidence_record(
                        repo_root,
                        kind="mcp-route",
                        facts=route_facts,
                        source_paths=[
                            mcp_live_path,
                            foundation.WP0001_MCP_LIVE_VERIFIER_PATH,
                        ],
                        capture_method="creator-command-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/bridge-discovery.json": (
                    self.evidence_record(
                        repo_root,
                        kind="bridge-discovery",
                        facts=bridge_facts,
                        source_paths=[
                            mcp_live_path,
                            foundation.WP0001_MCP_LIVE_VERIFIER_PATH,
                        ],
                        capture_method="creator-command-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/clean-handshake.json": (
                    self.evidence_record(
                        repo_root,
                        kind="clean-handshake",
                        facts=handshake_facts,
                        source_paths=[
                            handshake_raw_path,
                            protocol_collector["path"],
                        ],
                        capture_method="creator-command-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/activation-session.json": (
                    self.evidence_record(
                        repo_root,
                        kind="activation-session",
                        facts=activation_session_facts,
                        source_paths=[
                            activation_raw_path,
                            protocol_collector["path"],
                        ],
                        capture_method="creator-command-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/network-observation.json": (
                    self.evidence_record(
                        repo_root,
                        kind="network-observation",
                        facts=network_facts,
                        source_paths=[
                            network_raw_path,
                            network_collector["path"],
                        ],
                        capture_method="creator-command-capture",
                    )
                ),
                "docs/evidence/WP-0001/a1-activation/deviations.json": (
                    self.evidence_record(
                        repo_root,
                        kind="deviations",
                        facts={
                            "preserved_deviation_ids": list(
                                foundation.WP0001_PRESERVED_DEVIATION_IDS
                            )
                        },
                        source_paths=[
                            "docs/evidence/WP-0001/"
                            "pre-a1-readiness-20260716.json"
                        ],
                        capture_method="creator-command-capture",
                    )
                ),
            }
            if malformed_evidence:
                records[
                    "docs/evidence/WP-0001/a1-activation/toolchain.json"
                ].pop("facts_derivation")
            for relative, record in records.items():
                self.write_bytes(repo_root, relative, json_bytes(record))

            refs = {
                relative: self.source_ref(repo_root, relative)
                for relative in records
            }
            refs[runtime_config_path] = self.source_ref(
                repo_root, runtime_config_path
            )
            refs[sandbox_path] = self.source_ref(repo_root, sandbox_path)
            refs[network_path] = self.source_ref(repo_root, network_path)

            manifest["project_seed"]["evidence"] = refs[
                "docs/evidence/WP-0001/a1-activation/project-seed.json"
            ]
            if not blank_mcp_capture:
                mcp_live["observed"]["route_contract_sha256"] = (
                    foundation.canonical_json_sha256(
                        foundation.wp0001_mcp_route_contract(manifest)
                    )
                )
                self.write_bytes(
                    repo_root,
                    mcp_live_path,
                    json_bytes(mcp_live),
                )
                for record_path in (
                    "docs/evidence/WP-0001/a1-activation/mcp-route.json",
                    "docs/evidence/WP-0001/a1-activation/"
                    "bridge-discovery.json",
                ):
                    for source in records[record_path]["source_artifacts"]:
                        if source["path"] == mcp_live_path:
                            source["sha256"] = raw_sha256(
                                (repo_root / mcp_live_path).read_bytes()
                            )
                    self.write_bytes(
                        repo_root,
                        record_path,
                        json_bytes(records[record_path]),
                    )
                    refs[record_path] = self.source_ref(
                        repo_root,
                        record_path,
                    )
            manifest["activation_evidence"]["toolchain"] = refs[
                "docs/evidence/WP-0001/a1-activation/toolchain.json"
            ]
            manifest["activation_evidence"]["quarantine"] = refs[
                "docs/evidence/WP-0001/a1-activation/quarantine.json"
            ]
            manifest["activation_evidence"]["route"] = refs[
                "docs/evidence/WP-0001/a1-activation/mcp-route.json"
            ]
            manifest["activation_evidence"]["activation_session"] = refs[
                "docs/evidence/WP-0001/a1-activation/activation-session.json"
            ]
            manifest["activation_evidence"]["deviations"] = refs[
                "docs/evidence/WP-0001/a1-activation/deviations.json"
            ]
            manifest["activation_evidence"]["sandbox_policy"] = refs[
                sandbox_path
            ]
            manifest["activation_evidence"]["network_policy"] = refs[
                network_path
            ]
            route["entitlement"]["evidence"] = refs[
                "docs/evidence/WP-0001/a1-activation/entitlement-linkage.json"
            ]
            route["project_identity"]["evidence"] = refs[
                "docs/evidence/WP-0001/a1-activation/project-identity.json"
            ]
            route["process_observation"] = refs[
                "docs/evidence/WP-0001/a1-activation/mcp-route.json"
            ]
            route["bridge"]["discovery_record"] = refs[
                "docs/evidence/WP-0001/a1-activation/bridge-discovery.json"
            ]
            route["codex_policy"]["evidence"] = refs[runtime_config_path]
            route["handshake"]["transcript"] = refs[
                "docs/evidence/WP-0001/a1-activation/clean-handshake.json"
            ]
            route["activation_session"]["evidence"] = refs[
                "docs/evidence/WP-0001/a1-activation/activation-session.json"
            ]
            route["controls"]["observation"] = refs[
                "docs/evidence/WP-0001/a1-activation/network-observation.json"
            ]

            source_paths = {
                source["path"]
                for record in records.values()
                for source in record["source_artifacts"]
            }
            artifact_paths = (
                set(records)
                | source_paths
                | {runtime_config_path, sandbox_path, network_path}
            )
            evidence_manifest = {
                "schema_version": 1,
                "manifest_id": "A1E-WP-0001-FULL-TEST",
                "packet_id": "WP-0001",
                "packet_contract_sha256": PACKET_CONTRACT_SHA256,
                "base_commit": BASE_COMMIT,
                "created_at": CAPTURED_AT,
                "producer": {
                    "role": "creator",
                    "principal_id": runtime["principal_id"],
                    "principal_uid": runtime["principal_uid"],
                },
                "artifacts": [
                    {
                        "path": relative,
                        "sha256": raw_sha256(
                            (repo_root / relative).read_bytes()
                        ),
                        "byte_size": (repo_root / relative).stat().st_size,
                        "media_type": "application/octet-stream",
                        "captured_at": CAPTURED_AT,
                        "producer": "fixture-creator",
                        "source": "end-to-end-test-fixture",
                        "known_limitations": [],
                        "redaction_status": "not-required",
                    }
                    for relative in sorted(artifact_paths)
                ],
                "preserved_deviation_ids": list(
                    foundation.WP0001_PRESERVED_DEVIATION_IDS
                ),
                "complete": True,
                "secret_material_included": False,
            }
            evidence_manifest_path = (
                "docs/evidence/WP-0001/a1-activation/"
                "evidence-manifest.json"
            )
            self.write_bytes(
                repo_root,
                evidence_manifest_path,
                json_bytes(evidence_manifest),
            )
            refs[evidence_manifest_path] = self.source_ref(
                repo_root, evidence_manifest_path
            )
            manifest["activation_evidence"]["manifest"] = refs[
                evidence_manifest_path
            ]

            boundary_path.write_bytes(json_bytes(manifest))
            packet = {
                "id": "WP-0001",
                "contract_sha256": PACKET_CONTRACT_SHA256,
                "a1_boundary_manifest": {
                    "path": "governance/a1-boundaries/WP-0001.json",
                    "sha256": BOUNDARY_SHA256,
                    "manifest_id": manifest["manifest_id"],
                },
                "reservation": {
                    "base_commit": BASE_COMMIT,
                    **manifest["reservation"],
                },
            }
            state = {
                "constitution_sha256": "5" * 64,
                "decision_ledger_sha256": "6" * 64,
                "last_creator_receipt_id": "RR-FIXTURE-PRIOR",
            }
            receipt_artifacts = {
                "governance/a1-boundaries/WP-0001.json": BOUNDARY_SHA256,
                **{
                    relative: raw_sha256((repo_root / relative).read_bytes())
                    for relative in foundation.WP0001_ACTIVATION_EVIDENCE_PATHS
                },
            }
            activation_receipt = {
                "receipt_id": manifest["attestation_receipt_id"],
                "issued_by": manifest["attested_by"],
                "issued_at": "2026-07-16T12:10:07Z",
                "issuer_role": "creator",
                "receipt_kind": "packet-activation",
                "artifact_resolver": {
                    "type": "external-protected",
                    "resolver_reference": (
                        "https://github.com/AC-21/"
                        "sasha-the-land-pirate/pull/fixture"
                    ),
                },
                "signature_reference": (
                    "https://github.com/AC-21/"
                    "sasha-the-land-pirate/pull/fixture"
                ),
                "subject_ids": ["WP-0001"],
                "subject_claims": [
                    {
                        "subject_id": "WP-0001",
                        "claims": [
                            "A1-QUARANTINE-BOUNDARY-VERIFIED",
                            "ACTIVATE-A1-WP-0001",
                            "AUTHORIZE-WP0001-MCP-ALLOWLIST",
                            "AUTHORIZE-WP0001-RAW-COLLECTORS",
                            "AUTHORIZE-WP0001-CODE-IDENTITIES",
                        ],
                    }
                ],
                "sealed": True,
                "artifact_sha256": receipt_artifacts,
                "foundation_binding": {
                    "constitution_sha256": "5" * 64,
                    "decision_ledger_sha256": "6" * 64,
                    "last_creator_receipt_id": "RR-FIXTURE-PRIOR",
                },
                "subject_contract_sha256": {
                    "WP-0001": PACKET_CONTRACT_SHA256
                },
            }
            if missing_collector_claim:
                activation_receipt["subject_claims"][0]["claims"].remove(
                    "AUTHORIZE-WP0001-RAW-COLLECTORS"
                )
            if local_activation_receipt:
                activation_receipt["artifact_resolver"] = {
                    "type": "local-git-tree",
                    "resolver_reference": "fixture-local",
                }
            receipts_by_id = {
                "RR-FIXTURE-PRIOR": {
                    "sealed": True,
                    "issuer_role": "creator",
                }
            }
            git_blobs = {
                **seed_files,
                ".codex/config.toml": protected_config,
                foundation.WP0001_PROJECT_SEED_EVIDENCE_PATH: (
                    repo_root
                    / foundation.WP0001_PROJECT_SEED_EVIDENCE_PATH
                ).read_bytes(),
            }
            real_sha256_file = foundation.sha256_file

            def fixture_sha256_file(path: Path) -> str:
                if Path(path).resolve() == boundary_path.resolve():
                    return BOUNDARY_SHA256
                return real_sha256_file(path)

            with (
                mock.patch.object(foundation, "ROOT", foundation_root),
                mock.patch.object(foundation, "REPO_ROOT", repo_root),
                mock.patch.object(
                    foundation,
                    "repo_path_is_git_ignored",
                    return_value=True,
                ),
                mock.patch.object(
                    foundation,
                    "git_repo_tree_listing",
                    return_value=tree_listing,
                ),
                mock.patch.object(
                    foundation,
                    "git_repo_blob",
                    side_effect=lambda _commit, path: git_blobs.get(path),
                ),
                mock.patch.object(
                    foundation,
                    "sha256_file",
                    side_effect=fixture_sha256_file,
                ),
            ):
                _document, errors = foundation.validate_a1_boundary_manifest(
                    packet,
                    state,
                    activation_receipt,
                    receipts_by_id,
                )
            return errors

    def test_complete_activation_tree_reaches_and_passes_full_validator(
        self,
    ) -> None:
        self.assertEqual([], self.run_full_case())

    def test_blank_mcp_route_capture_is_rejected_by_live_capture_validator(
        self,
    ) -> None:
        errors = self.run_full_case(blank_mcp_capture=True)
        self.assertTrue(
            any(
                "MCP live capture has unexpected or missing top-level fields"
                in error
                for error in errors
            ),
            errors,
        )

    def test_runtime_config_override_is_rejected(self) -> None:
        errors = self.run_full_case(extra_runtime_server=True)
        self.assertIn(
            "WP-0001 Codex runtime config contains undeclared MCP servers",
            errors,
        )

    def test_non_socket_endpoint_is_rejected_by_live_capture_validator(
        self,
    ) -> None:
        errors = self.run_full_case(endpoint_is_socket=False)
        self.assertTrue(
            any("MCP live capture endpoint is invalid" in error for error in errors),
            errors,
        )

    def test_endpoint_canonical_path_drift_is_rejected(self) -> None:
        errors = self.run_full_case(endpoint_canonical_drift=True)
        self.assertTrue(
            any("MCP live capture endpoint is invalid" in error for error in errors),
            errors,
        )

    def test_unstable_executable_vnode_is_rejected(self) -> None:
        errors = self.run_full_case(unstable_executable_vnode=True)
        self.assertTrue(
            any(
                "MCP live capture client facts differ from the boundary"
                in error
                for error in errors
            ),
            errors,
        )

    def test_fd_graph_drift_is_rejected(self) -> None:
        errors = self.run_full_case(fd_graph_drift=True)
        self.assertTrue(
            any("MCP live capture FD graph is invalid" in error for error in errors),
            errors,
        )

    def test_detached_policy_subject_is_rejected(self) -> None:
        errors = self.run_full_case(policy_attachment_drift=True)
        self.assertTrue(
            any(
                "policy-attachment subject 0 is invalid" in error
                for error in errors
            ),
            errors,
        )

    def test_malformed_evidence_record_is_rejected_by_schema(self) -> None:
        errors = self.run_full_case(malformed_evidence=True)
        self.assertTrue(
            any(
                "toolchain evidence is missing required property "
                "'facts_derivation'" in error
                for error in errors
            ),
            errors,
        )

    def test_activation_receipt_must_authorize_raw_collectors(self) -> None:
        errors = self.run_full_case(missing_collector_claim=True)
        self.assertTrue(
            any(
                "activation receipt lacks exact creator activation authority"
                in error
                or (
                    "activation receipt lacks exact external-protected "
                    "creator activation authority"
                )
                in error
                for error in errors
            ),
            errors,
        )

    def test_local_activation_receipt_is_rejected(self) -> None:
        errors = self.run_full_case(local_activation_receipt=True)
        self.assertTrue(
            any(
                "activation receipt lacks exact external-protected creator "
                "activation authority" in error
                for error in errors
            ),
            errors,
        )

    def test_inert_collector_source_is_rejected(self) -> None:
        errors = self.run_full_case(inert_collector_source=True)
        self.assertTrue(
            any(
                "inert or incomplete collector" in error
                for error in errors
            ),
            errors,
        )

    def test_wp0001_cannot_approve_any_credential_id(self) -> None:
        errors = self.run_full_case(approved_credential_drift=True)
        self.assertTrue(
            any(
                "approved_credential_ids must equal []" in error
                or "must approve no credential IDs" in error
                for error in errors
            ),
            errors,
        )


if __name__ == "__main__":
    unittest.main()
