#!/usr/bin/env python3
"""Focused tests for the read-only WP-0001 MCP live verifier."""

from __future__ import annotations

import copy
import json
import os
import socket
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_wp0001_mcp_live as live


LSOF_HEADER = b"COMMAND PID USER FD TYPE DEVICE SIZE/OFF NODE NAME\n"


def lsof_field_output(
    pid: int,
    records: list[tuple[str, str, str, str]],
) -> bytes:
    output = bytearray(f"p{pid}\0".encode())
    for fd_raw, kind, _device, name in records:
        output.extend(f"f{fd_raw}\0t{kind}\0n{name}\0".encode())
    return bytes(output)


def lsof_human_output(
    command: str,
    pid: int,
    records: list[tuple[str, str, str, str]],
) -> bytes:
    output = bytearray(LSOF_HEADER)
    for fd_raw, kind, device, name in records:
        output.extend(
            f"{command} {pid} operator {fd_raw} {kind} "
            f"{device} 0t0 {name}\n".encode()
        )
    return bytes(output)


def completed(output: bytes) -> subprocess.CompletedProcess[bytes]:
    return subprocess.CompletedProcess([], 0, output, b"")


def valid_fd_graph_results(
    *,
    endpoint_name: str = "/private/tmp/unity-mcp-deadbeef-42",
    editor_after_pid: int = 42,
    relay_unix_peer: str = "0x8774",
    relay_stdin_peer: str = "0xe430",
) -> list[subprocess.CompletedProcess[bytes]]:
    editor_unix = [
        (
            "17u",
            "unix",
            "0xa111",
            f"{endpoint_name} type=STREAM (LISTEN)",
        ),
        (
            "18u",
            "unix",
            "0x8774",
            f"{endpoint_name} type=STREAM",
        ),
        (
            "19u",
            "unix",
            "0xbb00",
            "/private/tmp/unrelated-secret-socket",
        ),
    ]
    relay_unix = [
        ("9u", "unix", "0xf400", f"->{relay_unix_peer}"),
    ]
    client_pipe = [
        ("4w", "PIPE", "0xe430", "->0xf418"),
        ("5r", "PIPE", "0xbe95", "->0x4ccf"),
    ]
    relay_pipe = [
        ("0r", "PIPE", "0xf418", f"->{relay_stdin_peer}"),
        ("1w", "PIPE", "0x4ccf", "->0xbe95"),
    ]
    return [
        completed(lsof_field_output(42, editor_unix)),
        completed(lsof_human_output("Unity", 42, editor_unix)),
        completed(lsof_field_output(editor_after_pid, editor_unix)),
        completed(lsof_field_output(84, relay_unix)),
        completed(lsof_human_output("relay", 84, relay_unix)),
        completed(lsof_field_output(84, relay_unix)),
        completed(lsof_human_output("codex", 21, client_pipe)),
        completed(lsof_human_output("codex", 21, client_pipe)),
        completed(lsof_human_output("relay", 84, relay_pipe)),
        completed(lsof_human_output("relay", 84, relay_pipe)),
    ]


class Wp0001McpLiveVerifierTests(unittest.TestCase):
    def test_read_only_tools_are_absolute_allowlisted_and_sanitized(self) -> None:
        completed = subprocess.CompletedProcess([], 0, b"", b"")
        with mock.patch.object(
            live.subprocess,
            "run",
            return_value=completed,
        ) as run_mock:
            live.run_read_only([live.SYSTEM_PS, "-p", "1"])
        kwargs = run_mock.call_args.kwargs
        self.assertEqual(live.SYSTEM_PS, run_mock.call_args.args[0][0])
        self.assertEqual(
            "/usr/bin:/bin:/usr/sbin:/sbin",
            kwargs["env"]["PATH"],
        )
        self.assertNotIn("GH_TOKEN", kwargs["env"])
        self.assertEqual(subprocess.DEVNULL, kwargs["stdin"])
        with self.assertRaises(ValueError):
            live.run_read_only(["ps", "-p", "1"])

    def test_boot_identity_fails_closed(self) -> None:
        with mock.patch.object(
            live,
            "run_read_only",
            return_value=subprocess.CompletedProcess([], 1, b"", b"denied"),
        ):
            with self.assertRaises(ValueError):
                live.boot_session_sha256()

    def test_route_contract_excludes_circular_evidence_refs(self) -> None:
        boundary = {
            "packet_id": "WP-0001",
            "packet_contract_sha256": "1" * 64,
            "repository": {"absolute_root": "/private/repo"},
            "runtime_boundary": {"principal_uid": 777},
            "unity_mcp_route": {
                "client": {"pid": 10},
                "process_observation": {
                    "path": "docs/evidence/route.json",
                    "sha256": "2" * 64,
                },
                "activation_session": {
                    "session_id_sha256": "3" * 64,
                    "evidence": {
                        "path": "docs/evidence/session.json",
                        "sha256": "4" * 64,
                    },
                },
            },
            "activation_evidence": {"manifest": {"sha256": "5" * 64}},
        }
        changed_evidence = copy.deepcopy(boundary)
        changed_evidence["unity_mcp_route"]["process_observation"]["sha256"] = (
            "6" * 64
        )
        changed_evidence["activation_evidence"]["manifest"]["sha256"] = "7" * 64
        self.assertEqual(
            live.canonical_sha256(live.route_contract(boundary)),
            live.canonical_sha256(live.route_contract(changed_evidence)),
        )
        changed_route = copy.deepcopy(boundary)
        changed_route["unity_mcp_route"]["client"]["pid"] = 11
        self.assertNotEqual(
            live.canonical_sha256(live.route_contract(boundary)),
            live.canonical_sha256(live.route_contract(changed_route)),
        )

    def test_client_and_editor_argv_grammar_is_fail_closed(self) -> None:
        self.assertTrue(
            live.client_arguments_policy_safe(["codex", "mcp-client"])
        )
        self.assertFalse(
            live.client_arguments_policy_safe(
                ["codex", "mcp-client", "--profile", "unsafe"]
            )
        )
        target = "/private/repo/Game"
        self.assertTrue(
            live.editor_arguments_policy_safe(
                ["Unity", "-projectPath", target],
                target,
            )
        )
        self.assertFalse(
            live.editor_arguments_policy_safe(
                ["Unity", "-projectPath", target, "-executeMethod", "Bad.Run"],
                target,
            )
        )

    def test_help_has_no_filesystem_side_effects(self) -> None:
        script = Path(live.__file__)
        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw)
            before = list(directory.iterdir())
            result = subprocess.run(
                [sys.executable, str(script), "--help"],
                cwd=directory,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(0, result.returncode)
            self.assertIn("--boundary", result.stdout)
            self.assertIn("--output", result.stdout)
            self.assertEqual(before, list(directory.iterdir()))

    def test_process_birth_id_rejects_arbitrary_hash(self) -> None:
        arguments = ["codex", "mcp-client"]
        actual = live.process_birth_id(
            boot_sha256="1" * 64,
            pid=42,
            started_at="2026-07-16T12:10:00Z",
            executable_sha256="2" * 64,
            arguments=arguments,
        )
        changed = live.process_birth_id(
            boot_sha256="1" * 64,
            pid=43,
            started_at="2026-07-16T12:10:00Z",
            executable_sha256="2" * 64,
            arguments=arguments,
        )
        self.assertNotEqual("3" * 64, actual)
        self.assertNotEqual(actual, changed)
        self.assertEqual(actual, live.process_birth_id(
            boot_sha256="1" * 64,
            pid=42,
            started_at="2026-07-16T12:10:00+00:00",
            executable_sha256="2" * 64,
            arguments=arguments,
        ))

    def test_relay_argument_convention_strips_only_executable(self) -> None:
        actual = ["/private/home/relay", "--mcp", "--instance-id", "42"]
        expected = ["--mcp", "--instance-id", "42"]
        self.assertEqual(
            expected,
            live.normalized_process_arguments(actual, expected),
        )
        self.assertEqual(
            actual,
            live.normalized_process_arguments(actual, ["relay", "--mcp"]),
        )

    def test_fake_codesign_identity_does_not_match(self) -> None:
        observed = live.parse_codesign_identity(
            "Authority=Fake Corp\n"
            "Identifier=bad.fake\n"
            "TeamIdentifier=EVILTEAM12\n"
            "CDHash=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n"
            "designated => identifier bad.fake\n",
            strict_verified=True,
        )
        expected = {
            "verification_scope": "codesign-strict-component",
            "identifier": observed["identifier"],
            "team_identifier": observed["team_identifier"],
            "cdhash": observed["cdhash"],
            "designated_requirement_sha256": observed[
                "designated_requirement_sha256"
            ],
            "authorities_sha256": observed["authorities_sha256"],
        }
        self.assertFalse(
            live.code_identity_matches(
                observed,
                {**expected, "team_identifier": "OPENAI1234"},
            )
        )
        self.assertTrue(live.code_identity_matches(observed, expected))
        observed["strict_verified"] = False
        self.assertFalse(live.code_identity_matches(observed, expected))

    def test_procargs_parser_never_returns_secret_values(self) -> None:
        payload = (
            (2).to_bytes(4, sys.byteorder, signed=True)
            + b"/bin/tool\0\0tool\0--mcp\0"
            + b"HOME=/private/home\0"
            + b"GH_TOKEN=super-secret-value\0"
            + b"UNRELATED=must-not-return\0"
        )
        argv, values, present, names, duplicates = live.parse_procargs_buffer(
            payload,
            value_names=["HOME"],
            presence_only_names=["GH_TOKEN"],
        )
        self.assertEqual(["tool", "--mcp"], argv)
        self.assertEqual({"HOME": "/private/home"}, values)
        self.assertEqual(["GH_TOKEN"], present)
        self.assertEqual(
            ["GH_TOKEN", "HOME", "UNRELATED"],
            names,
        )
        self.assertEqual([], duplicates)
        serialized = json.dumps([argv, values, present, names, duplicates])
        self.assertNotIn("super-secret-value", serialized)
        self.assertNotIn("must-not-return", serialized)

    def test_connection_record_rejects_extra_key(self) -> None:
        expected = {
            "discovery_connection_type": "named_pipe",
            "endpoint": "/tmp/unity-mcp-deadbeef-42",
            "project_path": "/private/repo/Game",
            "protocol_version": "2.0",
            "editor_pid": 42,
        }
        record = {
            "connection_type": "named_pipe",
            "connection_path": "/tmp/unity-mcp-deadbeef-42",
            "created_date": "2026-07-16T12:00:00Z",
            "project_path": "/private/repo/Game",
            "protocol_version": "2.0",
            "editor_pid": 42,
            "untrusted_extra": True,
        }
        checks = live.validate_connection_record(record, expected)
        self.assertFalse(checks["keys_exact"])
        self.assertTrue(checks["editor_pid_matches"])

    def test_socket_facts_reject_regular_file_and_symlink(self) -> None:
        temp_root = Path(tempfile.gettempdir()).resolve()
        with tempfile.TemporaryDirectory(dir=temp_root) as raw:
            directory = Path(raw)
            regular = directory / "regular"
            regular.write_text("not a socket", encoding="utf-8")
            regular_facts = live.socket_facts(regular)
            self.assertFalse(regular_facts["is_socket"])
            self.assertFalse(regular_facts["is_symlink"])

            link = directory / "link"
            link.symlink_to(regular)
            link_facts = live.socket_facts(link)
            self.assertFalse(link_facts["is_socket"])
            self.assertTrue(link_facts["is_symlink"])

            endpoint = directory / "socket"
            server = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
            try:
                try:
                    server.bind(str(endpoint))
                except PermissionError as exc:
                    self.skipTest(
                        f"host policy forbids local Unix sockets: {exc}"
                    )
                socket_facts = live.socket_facts(endpoint)
                self.assertTrue(socket_facts["is_socket"])
                self.assertFalse(socket_facts["is_symlink"])
            finally:
                server.close()

    def test_lsof_endpoint_fd_graph_binds_exact_editor_and_relay_pids(
        self,
    ) -> None:
        endpoint = Path("/private/tmp/unity-mcp-deadbeef-42")
        with mock.patch.object(
            live,
            "run_read_only",
            side_effect=valid_fd_graph_results(),
        ) as run_mock:
            observed, checks = live.inspect_endpoint_fd_graph(
                client_pid=21,
                editor_pid=42,
                relay_pid=84,
                endpoint=endpoint,
            )
        self.assertTrue(all(checks.values()))
        self.assertEqual(
            observed["sha256"],
            live.canonical_sha256(observed["graph"]),
        )
        self.assertEqual(
            [
                {
                    "access": "u",
                    "channel_address_sha256": live.kernel_address_sha256(
                        "0xa111"
                    ),
                    "fd": 17,
                    "state": "listening",
                    "type": "unix",
                },
                {
                    "access": "u",
                    "channel_address_sha256": live.kernel_address_sha256(
                        "0x8774"
                    ),
                    "fd": 18,
                    "state": "open",
                    "type": "unix",
                },
            ],
            observed["graph"]["processes"][1]["descriptors"],
        )
        serialized = json.dumps(observed)
        self.assertNotIn("unrelated-secret-socket", serialized)
        for address in (
            "0xa111",
            "0x8774",
            "0xf400",
            "0xe430",
            "0xf418",
            "0xbe95",
            "0x4ccf",
        ):
            self.assertNotIn(address, serialized)
        self.assertEqual(
            [
                live.SYSTEM_LSOF,
                "-nP",
                "-a",
                "-p",
                "42",
                "-U",
                "-F0pftn",
            ],
            run_mock.call_args_list[0].args[0],
        )
        self.assertEqual(
            [
                live.SYSTEM_LSOF,
                "-nP",
                "-p",
                "21",
            ],
            run_mock.call_args_list[6].args[0],
        )
        self.assertEqual(
            [
                live.SYSTEM_LSOF,
                "-nP",
                "-a",
                "-p",
                "84",
                "-d",
                "0,1",
            ],
            run_mock.call_args_list[8].args[0],
        )
        self.assertEqual([], observed["graph"]["residuals"])
        self.assertTrue(checks["client_relay_channel_proven"])

    def test_lsof_endpoint_fd_graph_rejects_stale_or_decoy_socket(
        self,
    ) -> None:
        endpoint = Path("/private/tmp/unity-mcp-deadbeef-42")
        with mock.patch.object(
            live,
            "run_read_only",
            side_effect=valid_fd_graph_results(
                endpoint_name="/private/tmp/unity-mcp-deadbeef-420",
            ),
        ):
            _, checks = live.inspect_endpoint_fd_graph(
                client_pid=21,
                editor_pid=42,
                relay_pid=84,
                endpoint=endpoint,
            )
        self.assertFalse(
            checks["editor_endpoint_listener_owned_by_exact_pid"]
        )
        self.assertFalse(checks["relay_peer_bound_to_editor_endpoint_fd"])
        self.assertTrue(checks["client_relay_channel_proven"])

    def test_lsof_endpoint_fd_graph_accepts_only_prevalidated_tmp_alias(
        self,
    ) -> None:
        canonical = Path("/private/tmp/unity-mcp-deadbeef-42")
        reported = Path("/tmp/unity-mcp-deadbeef-42")
        with mock.patch.object(
            live,
            "run_read_only",
            side_effect=valid_fd_graph_results(endpoint_name=str(reported)),
        ):
            observed, checks = live.inspect_endpoint_fd_graph(
                client_pid=21,
                editor_pid=42,
                relay_pid=84,
                endpoint=canonical,
                accepted_aliases=(reported,),
            )
        self.assertTrue(all(checks.values()))
        self.assertEqual(
            str(canonical),
            observed["graph"]["endpoint"]["canonical_path"],
        )
        self.assertEqual(
            {
                "accepted_path_sha256s",
                "canonical_path",
                "canonical_path_sha256",
            },
            set(observed["graph"]["endpoint"]),
        )

    def test_lsof_endpoint_fd_graph_rejects_reinspection_pid_change(
        self,
    ) -> None:
        endpoint = Path("/private/tmp/unity-mcp-deadbeef-42")
        with mock.patch.object(
            live,
            "run_read_only",
            side_effect=valid_fd_graph_results(editor_after_pid=99),
        ):
            observed, checks = live.inspect_endpoint_fd_graph(
                client_pid=21,
                editor_pid=42,
                relay_pid=84,
                endpoint=endpoint,
            )
        self.assertFalse(checks["inspection_complete"])
        self.assertIsNotNone(
            observed["graph"]["processes"][1]["inspection_error"]
        )

    def test_lsof_endpoint_fd_graph_rejects_unmatched_relay_peer(
        self,
    ) -> None:
        endpoint = Path("/private/tmp/unity-mcp-deadbeef-42")
        with mock.patch.object(
            live,
            "run_read_only",
            side_effect=valid_fd_graph_results(relay_unix_peer="0x9999"),
        ):
            _, checks = live.inspect_endpoint_fd_graph(
                client_pid=21,
                editor_pid=42,
                relay_pid=84,
                endpoint=endpoint,
            )
        self.assertTrue(checks["relay_connected_peer_open_by_exact_pid"])
        self.assertFalse(checks["relay_peer_bound_to_editor_endpoint_fd"])

    def test_lsof_endpoint_fd_graph_rejects_broken_client_relay_pipe(
        self,
    ) -> None:
        endpoint = Path("/private/tmp/unity-mcp-deadbeef-42")
        with mock.patch.object(
            live,
            "run_read_only",
            side_effect=valid_fd_graph_results(relay_stdin_peer="0x9999"),
        ):
            _, checks = live.inspect_endpoint_fd_graph(
                client_pid=21,
                editor_pid=42,
                relay_pid=84,
                endpoint=endpoint,
            )
        self.assertFalse(checks["client_relay_stdin_pipe_bound"])
        self.assertTrue(checks["client_relay_stdout_pipe_bound"])
        self.assertFalse(checks["client_relay_channel_proven"])

    def test_lsof_parser_rejects_incomplete_or_duplicate_fields(self) -> None:
        records, errors = live.parse_lsof_field_records(
            b"p42\0f17u\0tunix\0tunix\0"
        )
        self.assertEqual([], records)
        self.assertIn("lsof type field is duplicated", errors)
        self.assertIn("lsof file record is incomplete", errors)

    def test_lsof_human_parser_matches_real_macos_channel_shape(self) -> None:
        records, errors = live.parse_lsof_human_records(
            LSOF_HEADER
            + b"relay 84 operator 9u unix 0xf400 0t0 ->0x8774\n"
            + b"relay 84 operator 0r PIPE 0xf418 0t0 ->0xe430\n"
        )
        self.assertEqual([], errors)
        self.assertEqual("0xf400", records[0]["device"])
        self.assertEqual("0x8774", live.lsof_peer_address(records[0]["name"]))
        self.assertEqual("pipe", records[1]["type"])

    def test_effective_inventory_rejects_config_extras(self) -> None:
        records = [
            {
                "layer": "runtime",
                "path": "/private/home/.codex/config.toml",
                "sha256": "1" * 64,
                "servers": [
                    {"name": "unity_mcp_a1_wp0001", "enabled": True},
                    {"name": "unexpected", "enabled": True},
                ],
            },
            {
                "layer": "candidate",
                "path": "/private/repo/.codex/config.toml",
                "sha256": "2" * 64,
                "servers": [{"name": "unity_mcp", "enabled": False}],
            },
            {
                "layer": "ancestor",
                "path": "/private/.codex/config.toml",
                "sha256": "3" * 64,
                "servers": [{"name": "hidden_extra", "enabled": False}],
            },
        ]
        active = live.active_inventory_servers(records)
        unexpected = live.unexpected_project_servers(records)
        self.assertEqual(2, len(active))
        self.assertEqual(
            [{"layer": "ancestor", "name": "hidden_extra", "path": "/private/.codex/config.toml"}],
            unexpected,
        )

    def test_toml_loader_rejects_symlink_config(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw).resolve()
            target = directory / "real.toml"
            target.write_text(
                "[mcp_servers.safe]\nenabled = false\n",
                encoding="utf-8",
            )
            link = directory / "config.toml"
            link.symlink_to(target)
            with self.assertRaises(ValueError):
                live.load_toml_regular(link)

    def test_path_ancestry_rejects_symlinked_parent(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw).resolve()
            real = directory / "real"
            real.mkdir()
            config = real / "config.toml"
            config.write_text("[mcp_servers.safe]\nenabled=false\n")
            alias = directory / "alias"
            alias.symlink_to(real, target_is_directory=True)
            escaped = alias / "config.toml"
            facts = live.path_ancestry_facts(
                escaped,
                require_leaf=True,
            )
            self.assertFalse(facts["safe"])
            self.assertEqual([str(alias)], facts["symlink_components"])
            with self.assertRaises(ValueError):
                live.load_toml_regular(escaped)

    def test_only_exact_macos_tmp_alias_can_be_canonicalized(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw).resolve()
            physical_tmp = directory / "private-tmp"
            physical_tmp.mkdir(mode=0o777)
            physical_tmp.chmod(0o1777)
            alias = directory / "tmp"
            alias.symlink_to(physical_tmp, target_is_directory=True)
            endpoint = physical_tmp / "unity-mcp-42"
            endpoint.touch()
            reported = alias / endpoint.name
            socket_result = {
                "exists": True,
                "is_socket": True,
                "is_symlink": False,
                "uid": os.getuid(),
                "mode": "0600",
            }
            with (
                mock.patch.object(live.sys, "platform", "darwin"),
                mock.patch.object(live, "MACOS_TMP_ALIAS", alias),
                mock.patch.object(
                    live,
                    "MACOS_PRIVATE_TMP",
                    physical_tmp,
                ),
                mock.patch.object(live, "SYSTEM_ROOT_UID", os.getuid()),
                mock.patch.object(
                    live,
                    "socket_facts",
                    return_value=socket_result,
                ) as socket_mock,
            ):
                observation, binding = live.inspect_endpoint(reported)
            self.assertTrue(binding["safe"])
            self.assertTrue(binding["alias_used"])
            self.assertEqual(str(reported), binding["reported"])
            self.assertEqual(str(endpoint), binding["canonical"])
            self.assertEqual(str(reported), observation["path"])
            self.assertEqual(str(endpoint), observation["canonical_path"])
            self.assertTrue(observation["is_socket"])
            socket_mock.assert_called_once_with(endpoint)

            other_alias = directory / "other"
            other_alias.symlink_to(physical_tmp, target_is_directory=True)
            with (
                mock.patch.object(live.sys, "platform", "darwin"),
                mock.patch.object(live, "MACOS_TMP_ALIAS", alias),
                mock.patch.object(live, "MACOS_PRIVATE_TMP", physical_tmp),
                mock.patch.object(live, "SYSTEM_ROOT_UID", os.getuid()),
            ):
                rejected = live.canonical_physical_path(
                    other_alias / endpoint.name,
                    allow_macos_tmp_alias=True,
                )
            self.assertFalse(rejected["safe"])
            self.assertIsNone(rejected["canonical"])

    def test_runtime_codex_home_must_contain_only_runtime_config(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            codex_home = Path(raw).resolve() / ".codex"
            codex_home.mkdir()
            runtime_config = codex_home / "config.toml"
            runtime_config.write_text("approval_policy='on-request'\n")
            entries, errors = live.runtime_codex_home_entries(runtime_config)
            self.assertEqual([str(runtime_config)], entries)
            self.assertEqual([], errors)
            (codex_home / "cloud-cache.json").write_text("{}")
            entries, errors = live.runtime_codex_home_entries(runtime_config)
            self.assertEqual(2, len(entries))
            self.assertEqual(
                ["runtime CODEX_HOME contains unexpected entries"],
                errors,
            )

    def test_config_inventory_enumerates_profile_requirements_and_legacy(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw).resolve()
            candidate = directory / "repo"
            (candidate / ".codex").mkdir(parents=True)
            (candidate / ".codex" / "config.toml").write_text(
                "[mcp_servers.unity_mcp]\nenabled=false\n"
            )
            codex_home = directory / "runtime" / ".codex"
            codex_home.mkdir(parents=True)
            runtime_config = codex_home / "config.toml"
            runtime_config.write_text(
                "approval_policy='on-request'\n"
                "[mcp_servers.unity_mcp_a1_wp0001]\nenabled=true\n"
            )
            (codex_home / "review.config.toml").write_text("")
            system = directory / "etc-codex"
            system.mkdir()
            requirements = system / "requirements.toml"
            managed = system / "managed_config.toml"
            requirements.write_text("")
            managed.write_text("")
            missing_system_config = system / "config.toml"
            with (
                mock.patch.object(
                    live,
                    "SYSTEM_CODEX_CONFIG",
                    missing_system_config,
                ),
                mock.patch.object(
                    live,
                    "SYSTEM_CODEX_REQUIREMENTS",
                    requirements,
                ),
                mock.patch.object(
                    live,
                    "SYSTEM_CODEX_MANAGED_CONFIG",
                    managed,
                ),
            ):
                records, _, errors = live.inspect_config_inventory(
                    runtime_config=runtime_config,
                    candidate=candidate,
                )
            self.assertEqual([], errors)
            self.assertEqual(
                {"runtime", "candidate", "profile", "requirements", "managed"},
                {record["layer"] for record in records},
            )
            unexpected = live.unexpected_config_features(records)
            self.assertEqual(
                {"profile", "requirements", "managed"},
                {record["layer"] for record in unexpected},
            )

    def test_hook_and_plugin_scan_detects_broken_symlink(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw).resolve()
            candidate = directory / "repo"
            (candidate / ".codex").mkdir(parents=True)
            runtime_config = directory / "runtime" / ".codex" / "config.toml"
            runtime_config.parent.mkdir(parents=True)
            runtime_config.write_text("")
            broken = candidate / ".codex" / "plugins"
            broken.symlink_to(directory / "missing")
            with (
                mock.patch.object(
                    live,
                    "SYSTEM_CODEX_HOOKS",
                    directory / "no-system-hooks",
                ),
                mock.patch.object(
                    live,
                    "SYSTEM_CODEX_PLUGINS",
                    directory / "no-system-plugins",
                ),
            ):
                active = live.active_hook_or_plugin_paths(
                    candidate=candidate,
                    runtime_config=runtime_config,
                )
            self.assertEqual([str(broken)], active)

    def test_mdm_probe_reads_types_without_collecting_values(self) -> None:
        present = subprocess.CompletedProcess([], 0, b"Type is string\n", b"")
        absent = subprocess.CompletedProcess(
            [],
            1,
            b"",
            b"The domain/default pair does not exist\n",
        )
        with (
            mock.patch.object(live.sys, "platform", "darwin"),
            mock.patch.object(
                live,
                "run_read_only",
                side_effect=[present, absent],
            ) as run_mock,
        ):
            keys, errors = live.macos_managed_preference_keys()
        self.assertEqual(["config_toml_base64"], keys)
        self.assertEqual([], errors)
        self.assertEqual(
            [
                live.SYSTEM_DEFAULTS,
                "read-type",
                live.CODEX_MDM_DOMAIN,
                "config_toml_base64",
            ],
            run_mock.call_args_list[0].args[0],
        )

    def test_mdm_filesystem_scan_finds_system_and_user_payloads(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            root = Path(raw).resolve() / "Managed Preferences"
            user = root / "operator"
            user.mkdir(parents=True)
            system_payload = root / f"{live.CODEX_MDM_DOMAIN}.plist"
            user_payload = user / f"{live.CODEX_MDM_DOMAIN}.plist"
            system_payload.write_bytes(b"system")
            user_payload.write_bytes(b"user")
            with (
                mock.patch.object(live.sys, "platform", "darwin"),
                mock.patch.object(
                    live,
                    "SYSTEM_MANAGED_PREFERENCES_ROOT",
                    root,
                ),
            ):
                files, errors = live.macos_managed_preference_files()
            self.assertEqual(
                sorted([str(system_payload), str(user_payload)]),
                files,
            )
            self.assertEqual([], errors)

    def test_process_snapshot_reinspection_rejects_vnode_change(self) -> None:
        with tempfile.TemporaryDirectory() as raw:
            directory = Path(raw).resolve()
            executable = directory / "codex"
            executable.write_bytes(b"fixed executable")
            digest = live.regular_file_facts(executable)["sha256"]
            arguments = ["codex", "mcp-client"]
            started_at = "2026-07-16T12:10:00Z"
            signing = {
                "available": True,
                "strict_verified": True,
                "identifier": "com.openai.codex",
                "team_identifier": "OPENAI1234",
                "cdhash": "a" * 40,
                "designated_requirement_sha256": "b" * 64,
                "authorities_sha256": "c" * 64,
            }
            expected = {
                "pid": 42,
                "path": str(executable),
                "sha256": digest,
                "cwd": str(directory),
                "arguments": arguments,
                "principal_uid": 501,
                "started_at": started_at,
                "process_birth_id_sha256": live.process_birth_id(
                    boot_sha256="d" * 64,
                    pid=42,
                    started_at=started_at,
                    executable_sha256=str(digest),
                    arguments=arguments,
                ),
                "signing_identity": {
                    "verification_scope": "codesign-strict-component",
                    "identifier": signing["identifier"],
                    "team_identifier": signing["team_identifier"],
                    "cdhash": signing["cdhash"],
                    "designated_requirement_sha256": signing[
                        "designated_requirement_sha256"
                    ],
                    "authorities_sha256": signing["authorities_sha256"],
                },
                "environment_names_sha256": live.canonical_sha256(["HOME"]),
            }
            facts = live.regular_file_facts(executable)
            snapshot = {
                "metadata": {
                    "pid": 42,
                    "parent_pid": 1,
                    "uid": 501,
                    "started_at": started_at,
                },
                "executable": executable,
                "cwd": directory,
                "argv": arguments,
                "environment": {"HOME": str(directory)},
                "forbidden_present": [],
                "environment_names": ["HOME"],
                "duplicate_environment_names": [],
                "executable_facts": facts,
            }
            changed = copy.deepcopy(snapshot)
            changed["executable_facts"]["inode"] += 1
            with (
                mock.patch.object(
                    live,
                    "collect_process_snapshot",
                    side_effect=[snapshot, changed],
                ) as snapshot_mock,
                mock.patch.object(
                    live,
                    "codesign_identity",
                    return_value=signing,
                ),
            ):
                observed, checks = live.inspect_process(
                    expected,
                    executable_key="path",
                    pid_key="pid",
                    boot_sha256="d" * 64,
                    expected_environment_values={"HOME": str(directory)},
                    absent_environment_names=[],
                )
            self.assertEqual(2, snapshot_mock.call_count)
            self.assertFalse(checks["process_inspection_complete"])
            self.assertFalse(checks["executable_vnode_stable"])
            self.assertIsNotNone(observed["inspection_error"])
            self.assertFalse(
                observed["signing_identity"]["executable_vnode"][
                    "stable_across_inspection"
                ]
            )

    def test_runtime_server_requires_exact_command_args_and_allowlist(self) -> None:
        policy = {
            "required": True,
            "enabled_tools": ["Unity_ReadConsole"],
        }
        relay = {
            "path": "/private/home/relay",
            "arguments": ["--mcp", "--project-path", "/private/repo/Game"],
        }
        server = {
            "command": "/private/home/not-the-relay",
            "args": relay["arguments"],
            "required": True,
            "enabled_tools": ["Unity_RunCommand"],
            "env": {},
        }
        checks = live.runtime_server_checks(
            server,
            policy=policy,
            relay=relay,
        )
        self.assertFalse(checks["command_matches"])
        self.assertFalse(checks["enabled_tools_match"])
        self.assertTrue(checks["arguments_match"])


if __name__ == "__main__":
    unittest.main()
