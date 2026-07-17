from __future__ import annotations

import ast
import contextlib
import hashlib
import io
import json
import os
import shutil
import stat
import subprocess
import tempfile
import types
import unittest
import sys
from pathlib import Path
from unittest import mock


POLICY_PATH = (
    Path(__file__).resolve().parents[3]
    / "Tools"
    / "Validation"
    / "validate_wp0002_policy.py"
)
FOUNDATION_VALIDATOR_PATH = Path(__file__).resolve().parent / "validate_foundation.py"
policy = types.ModuleType("validate_wp0002_policy_under_test")
policy.__dict__["__name__"] = "validate_wp0002_policy_under_test"
policy.__dict__["__file__"] = str(POLICY_PATH)
sys.modules[policy.__name__] = policy
exec(compile(POLICY_PATH.read_bytes(), str(POLICY_PATH), "exec"), policy.__dict__)


def canonical_packet_transitions() -> dict[str, set[str]]:
    tree = ast.parse(FOUNDATION_VALIDATOR_PATH.read_text(encoding="utf-8"))
    for node in tree.body:
        if isinstance(node, ast.Assign) and any(
            isinstance(target, ast.Name) and target.id == "PACKET_TRANSITIONS"
            for target in node.targets
        ):
            return ast.literal_eval(node.value)
    raise AssertionError("foundation validator lacks PACKET_TRANSITIONS")


DECLARED = [
    "SimulationCore/Runtime/LastBearing/",
    "SimulationCore/Runtime/LastBearing.meta",
    "SaveContracts/Runtime/LastBearing/",
    "SaveContracts/Runtime/LastBearing.meta",
]


def entry(
    status: str,
    old_path: str | None,
    new_path: str | None,
    *,
    old_mode: str = "100644",
    new_mode: str = "100644",
) -> object:
    return policy.DiffEntry(
        status,
        old_path,
        new_path,
        old_mode,
        new_mode,
        "1" * 40,
        "2" * 40,
    )


class WP0002PolicyTests(unittest.TestCase):
    def validate(self, *entries: object, phase: str = "implementation") -> list[str]:
        return policy.validate_delta(
            list(entries),
            phase=phase,
            declared_paths=DECLARED,
            reservation_paths=DECLARED,
            candidate_blobs={},
        )

    def test_allowed_nested_reserved_path_and_exact_folder_meta_pass(self) -> None:
        self.assertEqual(
            self.validate(
                entry(
                    "A",
                    None,
                    "SimulationCore/Runtime/LastBearing/Clock.cs",
                    old_mode="000000",
                ),
                entry(
                    "A",
                    None,
                    "SimulationCore/Runtime/LastBearing.meta",
                    old_mode="000000",
                ),
            ),
            [],
        )

    def test_folder_meta_has_no_undeclared_bypass(self) -> None:
        errors = policy.validate_delta(
            [
                entry(
                    "A",
                    None,
                    "SimulationCore/Runtime/LastBearing.meta",
                    old_mode="000000",
                )
            ],
            phase="implementation",
            declared_paths=["SimulationCore/Runtime/LastBearing/"],
            reservation_paths=["SimulationCore/Runtime/LastBearing/"],
        )
        self.assertTrue(any("escapes exact" in error for error in errors), errors)

    def test_arbitrary_path_and_deletion_fail(self) -> None:
        errors = self.validate(entry("M", "README.md", "README.md"))
        self.assertTrue(any("escapes exact" in error for error in errors), errors)
        errors = self.validate(
            entry(
                "D",
                "Outside/Old.cs",
                None,
                new_mode="000000",
            )
        )
        self.assertTrue(any("escapes exact" in error for error in errors), errors)
        self.assertEqual(
            self.validate(
                entry(
                    "D",
                    "SimulationCore/Runtime/LastBearing/Old.cs",
                    None,
                    new_mode="000000",
                )
            ),
            [],
        )

    def test_rename_and_copy_must_keep_both_sides_in_scope(self) -> None:
        for status in ("R", "C"):
            with self.subTest(status=status):
                errors = self.validate(
                    entry(
                        status,
                        "SimulationCore/Runtime/LastBearing/Source.cs",
                        "Outside/Escape.cs",
                    )
                )
                self.assertTrue(any("escapes exact" in error for error in errors), errors)

    def test_symlink_and_nonregular_modes_fail(self) -> None:
        for mode in ("120000", "160000"):
            with self.subTest(mode=mode):
                errors = self.validate(
                    entry(
                        "A",
                        None,
                        "SimulationCore/Runtime/LastBearing/Unsafe",
                        old_mode="000000",
                        new_mode=mode,
                    )
                )
                self.assertTrue(any("not a regular file mode" in error for error in errors), errors)

    def test_creator_drift_and_frozen_controls_fail(self) -> None:
        for path in (
            ".codex/config.toml",
            "Game/ProjectSettings/ProjectSettings.asset",
            "Game/ProjectSettings/SceneTemplateSettings.json",
            ".github/workflows/wp0002-ci.yml",
            "Tools/Validation/validate_wp0002_policy.py",
            "Tools/Validation/validate_wp0002_package_graph.py",
            "docs/foundation-v0.1/tools/validate_foundation.py",
        ):
            with self.subTest(path=path):
                errors = self.validate(entry("M", path, path))
                self.assertTrue(errors)

    def test_candidate_cannot_replace_same_named_workflow_or_checker_with_noop(self) -> None:
        for path in (
            ".github/workflows/wp0002-ci.yml",
            "Tools/Validation/validate_wp0002_policy.py",
        ):
            with self.subTest(path=path):
                errors = self.validate(entry("M", path, path))
                self.assertTrue(any("frozen self-verification" in error for error in errors), errors)

    @staticmethod
    def receipt(path: str) -> bytes:
        return json.dumps(
            {
                "receipt_id": Path(path).stem,
                "sealed": True,
                "issuer_role": "creator",
                "artifact_resolver": {"type": "external-protected"},
            }
        ).encode("utf-8")

    @staticmethod
    def scope_capture_fixture() -> tuple[dict[str, bytes], set[str]]:
        raw = b"1 .M N... 100644 100644 100644 " + b"1" * 40 + b" " + b"2" * 40 + b" file\0"
        observations = b'{"observations":[]}\n'
        raw_hash = hashlib.sha256(raw).hexdigest()
        observations_hash = hashlib.sha256(observations).hexdigest()
        raw_path = (
            "docs/evidence/WP-0002/scope-capture/"
            f"working-tree-scope.status.{raw_hash}.bin"
        )
        observations_path = (
            "docs/evidence/WP-0002/scope-capture/"
            f"working-tree-scope.observations.{observations_hash}.json"
        )
        capture = {
            "capture_contract": "wp0002-working-tree-scope-capture-v2",
            "packet_id": "WP-0002",
            "boundary_manifest_id": "A1B-WP-0002-LOCAL-DEV",
            "artifacts": {
                "raw_status": {
                    "path": raw_path,
                    "sha256": raw_hash,
                    "byte_size": len(raw),
                },
                "observations": {
                    "path": observations_path,
                    "sha256": observations_hash,
                    "byte_size": len(observations),
                },
            },
        }
        blobs = {
            policy.STAGE_C_SCOPE_CAPTURE_PATH: json.dumps(
                capture, sort_keys=True
            ).encode("utf-8"),
            raw_path: raw,
            observations_path: observations,
        }
        return blobs, {raw_path, observations_path}

    def test_stage_b_and_stage_c_are_explicit_and_complete(self) -> None:
        receipt_path = f"{policy.RECEIPT_PREFIX}RR-WP0002-TRANSITION.json"
        stage_b = [
            entry("M", "docs/foundation-v0.1/01-DECISION-LEDGER.md", "docs/foundation-v0.1/01-DECISION-LEDGER.md"),
            entry("M", "docs/foundation-v0.1/ledger/decisions.jsonl", "docs/foundation-v0.1/ledger/decisions.jsonl"),
            entry("M", "docs/foundation-v0.1/governance/ratification-state.json", "docs/foundation-v0.1/governance/ratification-state.json"),
            entry("M", policy.PACKET_PATH, policy.PACKET_PATH),
            entry("M", policy.WP0003_PACKET_PATH, policy.WP0003_PACKET_PATH),
            entry("M", "docs/evidence/WP-0003/VALIDATION.json", "docs/evidence/WP-0003/VALIDATION.json"),
            entry("A", None, receipt_path, old_mode="000000"),
        ]
        self.assertEqual(
            policy.validate_delta(
                stage_b,
                phase="stage-b",
                declared_paths=DECLARED,
                reservation_paths=DECLARED,
                candidate_blobs={receipt_path: self.receipt(receipt_path)},
            ),
            [],
        )
        scope_blobs, scope_paths = self.scope_capture_fixture()
        stage_c = [
            entry("M", "docs/foundation-v0.1/governance/ratification-state.json", "docs/foundation-v0.1/governance/ratification-state.json"),
            entry("M", policy.PACKET_PATH, policy.PACKET_PATH),
            entry("M", "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json", "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json"),
            *(entry("A", None, path, old_mode="000000") for path in sorted(policy.STAGE_C_FIXED_CAPTURE_PATHS)),
            *(entry("A", None, path, old_mode="000000") for path in sorted(scope_paths)),
            entry("A", None, receipt_path, old_mode="000000"),
        ]
        stage_c_blobs = {**scope_blobs, receipt_path: self.receipt(receipt_path)}
        self.assertEqual(
            policy.validate_delta(
                stage_c,
                phase="stage-c",
                declared_paths=DECLARED,
                reservation_paths=DECLARED,
                candidate_blobs=stage_c_blobs,
            ),
            [],
        )
        incomplete = stage_c[:-2] + [stage_c[-1]]
        self.assertTrue(
            policy.validate_delta(
                incomplete,
                phase="stage-c",
                declared_paths=DECLARED,
                reservation_paths=DECLARED,
                candidate_blobs=stage_c_blobs,
            )
        )
        old_alias = stage_c + [
            entry(
                "A",
                None,
                "docs/evidence/WP-0002/raw-porcelain.bin",
                old_mode="000000",
            )
        ]
        errors = policy.validate_delta(
            old_alias,
            phase="stage-c",
            declared_paths=DECLARED,
            reservation_paths=DECLARED,
            candidate_blobs=stage_c_blobs,
        )
        self.assertTrue(any("not enumerated" in error for error in errors), errors)
        capture = json.loads(scope_blobs[policy.STAGE_C_SCOPE_CAPTURE_PATH])
        capture["artifacts"]["raw_status"]["path"] = policy.STAGE_C_SCOPE_CAPTURE_PATH
        cyclic_blobs = {
            **stage_c_blobs,
            policy.STAGE_C_SCOPE_CAPTURE_PATH: json.dumps(capture).encode("utf-8"),
        }
        errors = policy.validate_delta(
            stage_c,
            phase="stage-c",
            declared_paths=DECLARED,
            reservation_paths=DECLARED,
            candidate_blobs=cyclic_blobs,
        )
        self.assertTrue(any("cycle" in error for error in errors), errors)
        errors = self.validate(
            entry("M", "docs/foundation-v0.1/04-TECHNICAL-ARCHITECTURE.md", "docs/foundation-v0.1/04-TECHNICAL-ARCHITECTURE.md"),
            phase="stage-c",
        )
        self.assertTrue(any("not enumerated" in error for error in errors), errors)

    def test_lifecycle_matrix_and_terminal_lock_are_exact(self) -> None:
        implementation = entry(
            "A",
            None,
            "SimulationCore/Runtime/LastBearing/State.cs",
            old_mode="000000",
        )
        packet = entry("M", policy.PACKET_PATH, policy.PACKET_PATH)
        evidence = entry(
            "A", None, "docs/evidence/WP-0002/run.json", old_mode="000000"
        )
        for phase in ("active-to-verifying", "verifying-to-candidate"):
            with self.subTest(phase=phase):
                self.assertEqual(
                    self.validate(implementation, packet, evidence, phase=phase), []
                )
        for phase in (
            "verification-to-active",
            "candidate-to-verifying",
            "candidate-to-released",
            "early-cancellation",
            "rejected-transition",
            "superseded-transition",
        ):
            with self.subTest(phase=phase):
                self.assertEqual(self.validate(packet, evidence, phase=phase), [])
                self.assertTrue(self.validate(implementation, packet, evidence, phase=phase))
        rollback_delete = entry(
            "D",
            "SimulationCore/Runtime/LastBearing/Old.cs",
            None,
            new_mode="000000",
        )
        self.assertEqual(
            self.validate(rollback_delete, packet, evidence, phase="rollback-transition"),
            [],
        )
        self.assertEqual(self.validate(rollback_delete, phase="implementation"), [])
        unrelated = entry("M", "OtherPacket/file.txt", "OtherPacket/file.txt")
        self.assertEqual(
            self.validate(unrelated, phase="post-terminal-unrelated"), []
        )
        self.assertTrue(
            self.validate(implementation, phase="post-terminal-unrelated")
        )

    def test_transition_phase_detection_and_receipt_binding_are_complete(self) -> None:
        expected = {
            ("proposed", "accepted"): "stage-b",
            ("proposed", "rejected"): "early-cancellation",
            ("proposed", "superseded"): "early-cancellation",
            ("accepted", "active"): "stage-c",
            ("accepted", "rejected"): "early-cancellation",
            ("accepted", "superseded"): "early-cancellation",
            ("active", "active"): "implementation",
            ("active", "verifying"): "active-to-verifying",
            ("active", "rolled-back"): "rollback-transition",
            ("active", "rejected"): "rejected-transition",
            ("verifying", "active"): "verification-to-active",
            ("verifying", "candidate"): "verifying-to-candidate",
            ("verifying", "rolled-back"): "rollback-transition",
            ("verifying", "rejected"): "rejected-transition",
            ("candidate", "verifying"): "candidate-to-verifying",
            ("candidate", "released"): "candidate-to-released",
            ("candidate", "rolled-back"): "rollback-transition",
            ("candidate", "rejected"): "rejected-transition",
            ("released", "rolled-back"): "rollback-transition",
            ("released", "superseded"): "superseded-transition",
            ("released", "released"): "post-terminal-unrelated",
            ("rejected", "rejected"): "post-terminal-unrelated",
            ("rolled-back", "rolled-back"): "post-terminal-unrelated",
            ("superseded", "superseded"): "post-terminal-unrelated",
        }
        for transition, phase in expected.items():
            with self.subTest(transition=transition):
                self.assertEqual(policy._phase_for_transition(*transition), phase)
        canonical_edges = {
            (source, destination)
            for source, destinations in policy.PACKET_TRANSITIONS.items()
            for destination in destinations
        }
        self.assertEqual(policy.PACKET_TRANSITIONS, canonical_packet_transitions())
        self.assertEqual(
            canonical_edges,
            {
                transition
                for transition in expected
                if transition[0] != transition[1]
            },
        )
        self.assertIsNone(policy._phase_for_transition("proposed", "active"))
        packet = {
            "status_events": [
                {
                    "from": "active",
                    "to": "verifying",
                    "receipt_id": "RR-TRANSITION",
                }
            ]
        }
        receipt_path = f"{policy.RECEIPT_PREFIX}RR-TRANSITION.json"
        with mock.patch.object(
            policy, "_git_path_bytes", return_value=self.receipt(receipt_path)
        ):
            self.assertEqual(
                policy._validate_transition_receipt(
                    Path("/unused"), "2" * 40, "active", "verifying", packet
                ),
                [],
            )
        self.assertTrue(
            policy._validate_transition_receipt(
                Path("/unused"), "2" * 40, "candidate", "released", packet
            )
        )

    def test_invocation_rejects_forks_nonmain_and_nonagent_head(self) -> None:
        common = {
            "base": "1" * 40,
            "head": "2" * 40,
            "base_ref": "main",
            "head_ref": "agent/wp0002",
            "base_repository": "AC-21/sasha",
            "head_repository": "AC-21/sasha",
            "policy_source_sha": "3" * 40,
        }
        self.assertEqual(policy._validate_invocation(**common), [])
        for field, value in (
            ("head_repository", "fork/sasha"),
            ("base_ref", "develop"),
            ("head_ref", "feature/untrusted"),
            ("head", "$(touch pwned)"),
        ):
            with self.subTest(field=field):
                candidate = dict(common)
                candidate[field] = value
                self.assertTrue(policy._validate_invocation(**candidate))


class WP0002PolicyGitAndReportTests(unittest.TestCase):
    @staticmethod
    def git(*args: str, input_bytes: bytes | None = None) -> subprocess.CompletedProcess[bytes]:
        return subprocess.run(
            ["/usr/bin/git", *args],
            input=input_bytes,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
            env={
                "HOME": "/var/empty",
                "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
                "LANG": "C",
                "LC_ALL": "C",
                "TZ": "UTC",
                "GIT_CONFIG_NOSYSTEM": "1",
                "GIT_CONFIG_GLOBAL": "/dev/null",
                "GIT_TERMINAL_PROMPT": "0",
            },
        )

    def init_bare(self, root: Path, *, object_format: str = "sha1") -> Path:
        repo = root / "objects.git"
        result = self.git("init", "--bare", f"--object-format={object_format}", str(repo))
        if result.returncode != 0:
            self.skipTest(
                f"installed Git cannot create {object_format} repository: "
                f"{result.stderr.decode('utf-8', 'replace')}"
            )
        return repo

    def test_git_invocation_ignores_ambient_config_hooks_aliases_and_replace_refs(self) -> None:
        completed = subprocess.CompletedProcess(args=[], returncode=0, stdout=b"", stderr=b"")
        with mock.patch.object(policy.subprocess, "run", return_value=completed) as run:
            policy._run_git(Path("/private/tmp/policy.git"), ["status"])
        argv = run.call_args.args[0]
        env = run.call_args.kwargs["env"]
        for item in (
            "--no-replace-objects",
            "core.fsmonitor=false",
            "core.hooksPath=/dev/null",
            "maintenance.auto=false",
            "gc.auto=0",
            "fetch.writeCommitGraph=false",
            "--no-optional-locks",
            "--no-pager",
        ):
            self.assertIn(item, argv)
        self.assertEqual(env, policy.GIT_ENV)
        self.assertEqual(env["GIT_CONFIG_GLOBAL"], "/dev/null")
        self.assertEqual(env["GIT_NO_REPLACE_OBJECTS"], "1")
        self.assertEqual(env["GIT_NO_LAZY_FETCH"], "1")
        self.assertEqual(env["TZ"], "UTC")

    def test_replace_ref_alias_include_and_fsmonitor_configs_fail(self) -> None:
        mutations = (
            ("alias", ("config", "alias.evil", "!touch /tmp/pwned")),
            ("include", ("config", "include.path", "/tmp/evil-config")),
            ("fsmonitor", ("config", "core.fsmonitor", "/tmp/evil-monitor")),
            ("unapproved-config", ("config", "color.ui", "always")),
        )
        for label, command in mutations:
            with self.subTest(label=label), tempfile.TemporaryDirectory() as temporary:
                repo = self.init_bare(Path(temporary))
                self.assertEqual(self.git("--git-dir", str(repo), *command).returncode, 0)
                with self.assertRaisesRegex(ValueError, "unsafe local Git config"):
                    policy._require_repository(repo)

        with tempfile.TemporaryDirectory() as temporary:
            repo = self.init_bare(Path(temporary))
            first = self.git("--git-dir", str(repo), "hash-object", "-w", "--stdin", input_bytes=b"one")
            second = self.git("--git-dir", str(repo), "hash-object", "-w", "--stdin", input_bytes=b"two")
            self.assertEqual(first.returncode, 0)
            self.assertEqual(second.returncode, 0)
            source = first.stdout.strip().decode("ascii")
            target = second.stdout.strip().decode("ascii")
            self.assertEqual(
                self.git(
                    "--git-dir",
                    str(repo),
                    "update-ref",
                    f"refs/replace/{source}",
                    target,
                ).returncode,
                0,
            )
            with self.assertRaisesRegex(ValueError, "replace refs"):
                policy._require_repository(repo)

    def test_non_sha1_object_format_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repo = self.init_bare(Path(temporary), object_format="sha256")
            with self.assertRaisesRegex(ValueError, "sha1 object format"):
                policy._require_repository(repo)

    def test_secure_content_addressed_report_rejects_symlink_escape(self) -> None:
        payload = policy._content_address_report(
            {
                "schema_version": 1,
                "contract": policy.POLICY_CONTRACT,
                "source": {"commit_sha1": "1" * 40},
                "base": {"commit_sha1": "2" * 40},
                "candidate": {"commit_sha1": "3" * 40},
            },
            [],
        )
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "root"
            outside = Path(temporary) / "outside"
            (root / "BuildArtifacts").mkdir(parents=True)
            outside.mkdir()
            (root / "BuildArtifacts/WP-0002").symlink_to(outside, target_is_directory=True)
            with self.assertRaises((OSError, ValueError)):
                policy._write_report(
                    root, "BuildArtifacts/WP-0002/policy.json", payload
                )
            self.assertFalse((outside / "policy.json").exists())

    def test_cli_rejects_noncanonical_report_destination(self) -> None:
        stderr = io.StringIO()
        with contextlib.redirect_stderr(stderr), self.assertRaises(SystemExit):
            policy.main(
                [
                    "--repo", "/unused",
                    "--base", "1" * 40,
                    "--head", "2" * 40,
                    "--base-ref", "main",
                    "--head-ref", "agent/test",
                    "--base-repository", "AC-21/sasha",
                    "--head-repository", "AC-21/sasha",
                    "--policy-source-sha", "3" * 40,
                    "--report-root", "/unused",
                    "--report", "BuildArtifacts/WP-0002/alias.json",
                ]
            )
        self.assertIn("invalid choice", stderr.getvalue())

    def test_end_to_end_bare_repo_cli_writes_bound_report(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            work = root / "work"
            bare = root / "candidate.git"
            report_root = root / "report-root"
            report_root.mkdir()
            self.assertEqual(self.git("init", str(work)).returncode, 0)
            self.assertEqual(self.git("-C", str(work), "config", "user.name", "Policy Test").returncode, 0)
            self.assertEqual(self.git("-C", str(work), "config", "user.email", "policy@example.invalid").returncode, 0)
            policy_target = work / policy.POLICY_PATH
            policy_target.parent.mkdir(parents=True)
            shutil.copy2(POLICY_PATH, policy_target)
            packet_target = work / policy.PACKET_PATH
            packet_target.parent.mkdir(parents=True)
            packet_target.write_text(
                json.dumps(
                    {
                        "status": "active",
                        "declared_paths": ["Allowed/"],
                        "reservation": {"paths": ["Allowed/"]},
                    },
                    sort_keys=True,
                )
                + "\n",
                encoding="utf-8",
            )
            self.assertEqual(self.git("-C", str(work), "add", ".").returncode, 0)
            self.assertEqual(self.git("-C", str(work), "commit", "-m", "base").returncode, 0)
            base = self.git("-C", str(work), "rev-parse", "HEAD").stdout.strip().decode("ascii")
            allowed = work / "Allowed/value.txt"
            allowed.parent.mkdir()
            allowed.write_text("candidate\n", encoding="utf-8")
            self.assertEqual(self.git("-C", str(work), "add", ".").returncode, 0)
            self.assertEqual(self.git("-C", str(work), "commit", "-m", "candidate").returncode, 0)
            head = self.git("-C", str(work), "rev-parse", "HEAD").stdout.strip().decode("ascii")
            self.assertEqual(self.git("clone", "--bare", str(work), str(bare)).returncode, 0)

            stdout = io.StringIO()
            with contextlib.redirect_stdout(stdout):
                result = policy.main(
                    [
                        "--repo", str(bare),
                        "--base", base,
                        "--head", head,
                        "--base-ref", "main",
                        "--head-ref", "agent/wp0002-test",
                        "--base-repository", "AC-21/sasha",
                        "--head-repository", "AC-21/sasha",
                        "--policy-source-sha", base,
                        "--report-root", str(report_root),
                        "--report", policy.POLICY_REPORT_PATH,
                    ]
                )
            self.assertEqual(result, 0)
            report_path = report_root / policy.POLICY_REPORT_PATH
            report = json.loads(report_path.read_text(encoding="utf-8"))
            digest = report.pop("report_sha256")
            canonical = json.dumps(
                report, sort_keys=True, separators=(",", ":"), ensure_ascii=False
            ).encode("utf-8")
            self.assertEqual(digest, hashlib.sha256(canonical).hexdigest())
            self.assertEqual(report["source"]["commit_sha1"], base)
            self.assertEqual(report["base"]["commit_sha1"], base)
            self.assertEqual(report["candidate"]["commit_sha1"], head)
            self.assertRegex(report["source"]["blob_sha256"], r"^[0-9a-f]{64}$")
            self.assertRegex(report["base"]["packet_sha256"], r"^[0-9a-f]{64}$")
            self.assertRegex(report["candidate"]["packet_sha256"], r"^[0-9a-f]{64}$")
            self.assertRegex(report["base_head_diff_sha256"], r"^[0-9a-f]{64}$")
            self.assertIn(digest, stdout.getvalue())
            self.assertTrue(stat.S_ISREG(report_path.lstat().st_mode))
            self.assertEqual(stat.S_IMODE(report_path.lstat().st_mode), 0o600)


if __name__ == "__main__":
    unittest.main()
