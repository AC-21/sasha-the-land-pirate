#!/usr/bin/env python3
"""Tests for the non-Unity WP-0001 quarantine verifier."""

from __future__ import annotations

import os
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import validate_wp0001_a1_live as live


class Wp0001A1LiveTests(unittest.TestCase):
    def test_documented_cli_has_no_trusted_root_override(self) -> None:
        script = Path(live.__file__).resolve()
        result = subprocess.run(
            [sys.executable, str(script), "--help"],
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(0, result.returncode)
        self.assertIn("--boundary", result.stdout)
        self.assertIn("--output", result.stdout)
        self.assertNotIn("--trusted-root", result.stdout)

    def test_regular_file_is_not_accepted_as_socket(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "endpoint"
            path.write_bytes(b"")
            path.chmod(0o600)
            metadata = live.unix_socket_metadata(path)
        self.assertTrue(metadata["exists"])
        self.assertFalse(metadata["is_socket"])
        self.assertFalse(metadata["is_symlink"])

    def test_symlink_is_not_accepted_as_socket(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "target"
            target.write_bytes(b"")
            link = root / "endpoint"
            link.symlink_to(target)
            metadata = live.unix_socket_metadata(link)
        self.assertTrue(metadata["exists"])
        self.assertTrue(metadata["is_symlink"])
        self.assertFalse(metadata["is_socket"])

    def test_unix_domain_socket_is_identified(self) -> None:
        metadata_record = mock.Mock(
            st_mode=stat.S_IFSOCK | 0o600,
            st_uid=777,
        )
        with mock.patch.object(Path, "lstat", return_value=metadata_record):
            metadata = live.unix_socket_metadata(Path("/tmp/fixture-socket"))
        self.assertTrue(metadata["exists"])
        self.assertTrue(metadata["is_socket"])
        self.assertFalse(metadata["is_symlink"])
        self.assertEqual("0600", metadata["mode"])

    def test_tree_inode_scan_detects_symlink_and_hardlink(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            left = root / "left"
            right = root / "right"
            left.mkdir()
            right.mkdir()
            source = left / "source"
            source.write_bytes(b"fixture")
            os.link(source, right / "hardlink")
            (left / "symlink").symlink_to(source)
            left_inodes, left_symlink_free = live.tree_file_inodes(left)
            right_inodes, right_symlink_free = live.tree_file_inodes(right)
        self.assertFalse(left_symlink_free)
        self.assertTrue(right_symlink_free)
        self.assertTrue(left_inodes & right_inodes)

    def test_tree_inode_scan_rejects_symlink_root(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "target"
            target.mkdir()
            (target / "file").write_bytes(b"fixture")
            alias = root / "alias"
            alias.symlink_to(target, target_is_directory=True)
            inodes, symlink_free = live.tree_file_inodes(alias)
        self.assertEqual(set(), inodes)
        self.assertFalse(symlink_free)

    def test_component_scan_rejects_symlink_ancestor_and_missing_parent_race(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            real = root / "real"
            real.mkdir()
            (real / "child").write_bytes(b"fixture")
            alias = root / "alias"
            alias.symlink_to(real, target_is_directory=True)
            self.assertFalse(
                live.path_components_symlink_free(alias / "child")
            )
            self.assertFalse(
                live.canonical_symlink_free_path(alias / "child")
            )
            self.assertTrue(
                live.path_components_symlink_free(
                    real / "not-created" / "child",
                    allow_missing_leaf=True,
                )
            )

    def test_declared_relative_path_rejects_symlink_escape(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            outside = root / "outside"
            repository = root / "repository"
            outside.mkdir()
            repository.mkdir()
            (outside / "protected").write_bytes(b"fixture")
            (repository / "escape").symlink_to(
                outside,
                target_is_directory=True,
            )
            self.assertIsNone(
                live.resolve_declared_relative_path(
                    repository,
                    "escape/protected",
                )
            )
            self.assertIsNone(
                live.resolve_declared_relative_path(repository, "../outside")
            )

    def test_protected_path_requires_recursive_nonmutating_write_denial(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            protected = root / "protected"
            nested = protected / "nested"
            nested.mkdir(parents=True)
            first = protected / "first"
            second = nested / "second"
            first.write_bytes(b"first")
            second.write_bytes(b"second")
            first.chmod(0o444)
            second.chmod(0o444)
            nested.chmod(0o555)
            protected.chmod(0o555)
            try:
                read_only, files, directories = (
                    live.protected_path_is_read_only(protected)
                )
                self.assertTrue(read_only)
                self.assertEqual(2, files)
                self.assertEqual(2, directories)
                self.assertEqual(b"first", first.read_bytes())
                self.assertEqual(b"second", second.read_bytes())

                nested.chmod(0o755)
                self.assertFalse(
                    live.protected_path_is_read_only(protected)[0]
                )
            finally:
                protected.chmod(0o755)
                nested.chmod(0o755)
                first.chmod(0o644)
                second.chmod(0o644)

    def test_protected_path_rejects_symlink_descendant(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            protected = root / "protected"
            outside = root / "outside"
            protected.mkdir()
            outside.write_bytes(b"fixture")
            (protected / "escape").symlink_to(outside)
            protected.chmod(0o555)
            try:
                self.assertFalse(
                    live.protected_path_is_read_only(protected)[0]
                )
            finally:
                protected.chmod(0o755)

    def test_git_inspection_is_absolute_and_disables_execution_hooks(self) -> None:
        completed = subprocess.CompletedProcess([], 0, "", "")
        with mock.patch.object(
            live.subprocess,
            "run",
            return_value=completed,
        ) as run_mock:
            live.git(Path("/private/candidate"), "ls-files", "--stage", "-z")
        args = run_mock.call_args.args[0]
        kwargs = run_mock.call_args.kwargs
        self.assertEqual("/usr/bin/git", args[0])
        self.assertIn("core.fsmonitor=false", args)
        self.assertIn("core.hooksPath=/dev/null", args)
        self.assertIn("--no-optional-locks", args)
        self.assertEqual("/dev/null", kwargs["env"]["GIT_CONFIG_GLOBAL"])
        self.assertEqual("1", kwargs["env"]["GIT_CONFIG_NOSYSTEM"])
        self.assertEqual(subprocess.DEVNULL, kwargs["stdin"])
        with self.assertRaises(ValueError):
            live.git(Path("/private/candidate"), "status", "--porcelain=v1")

    def test_git_config_parser_accepts_only_inert_core_configuration(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / "config"
            config.write_text(
                "\n".join(
                    [
                        "[core]",
                        "\trepositoryformatversion = 0",
                        "\tfilemode = true",
                        "\tbare = false",
                        "\tlogallrefupdates = true",
                        "\tignorecase = true",
                        "\tprecomposeunicode = true",
                    ]
                )
                + "\n",
                encoding="utf-8",
            )
            self.assertTrue(live.passive_git_config(config))

    def test_git_config_parser_rejects_execution_and_indirection_variants(
        self,
    ) -> None:
        malicious_configs = {
            "filter-dot": "[filter.probe]\nclean = /usr/bin/false\n",
            "filter-subsection": (
                '[filter "probe"]\nclean = /usr/bin/false\n'
            ),
            "diff-dot": "[diff.probe]\ncommand = /usr/bin/false\n",
            "include": "[include]\npath = /tmp/host-config\n",
            "conditional-include": (
                '[includeIf "gitdir:/tmp/"]\npath = /tmp/host-config\n'
            ),
            "hooks": "[core]\nrepositoryformatversion = 0\nbare = false\n"
            "hooksPath = /tmp/hooks\n",
            "fsmonitor": "[core]\nrepositoryformatversion = 0\nbare = false\n"
            "fsmonitor = /tmp/fsmonitor\n",
            "maintenance": "[maintenance]\nauto = true\n",
            "remote": '[remote "origin"]\nurl = https://example.invalid/x\n',
            "worktree": "[extensions]\nworktreeConfig = true\n",
        }
        with tempfile.TemporaryDirectory() as directory:
            config = Path(directory) / "config"
            for label, text in malicious_configs.items():
                with self.subTest(label=label):
                    config.write_text(text, encoding="utf-8")
                    self.assertFalse(live.passive_git_config(config))

    def test_git_metadata_rejects_active_hook_replace_ref_and_fsmonitor(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory).resolve()
            git_directory = candidate / ".git"
            hooks = git_directory / "hooks"
            hooks.mkdir(parents=True)
            (git_directory / "config").write_text(
                "[core]\n"
                "repositoryformatversion = 0\n"
                "filemode = true\n"
                "bare = false\n"
                "logallrefupdates = true\n",
                encoding="utf-8",
            )
            (hooks / "pre-commit.sample").write_bytes(b"sample")
            self.assertTrue(live.git_metadata_is_passive(candidate))

            active_hook = hooks / "pre-commit"
            active_hook.write_bytes(b"#!/bin/sh\nexit 1\n")
            self.assertFalse(live.git_metadata_is_passive(candidate))
            active_hook.unlink()

            replace = git_directory / "refs" / "replace"
            replace.mkdir(parents=True)
            self.assertFalse(live.git_metadata_is_passive(candidate))
            replace.rmdir()

            fsmonitor = git_directory / "fsmonitor--daemon.ipc"
            fsmonitor.write_bytes(b"fixture")
            self.assertFalse(live.git_metadata_is_passive(candidate))

    def test_git_metadata_rejects_packed_replace_ref(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory)
            git_directory = candidate / ".git"
            git_directory.mkdir()
            (git_directory / "config").write_text(
                "[core]\nrepositoryformatversion = 0\nbare = false\n",
                encoding="utf-8",
            )
            (git_directory / "packed-refs").write_text(
                "1" * 40 + " refs/replace/" + "2" * 40 + "\n",
                encoding="utf-8",
            )
            self.assertFalse(live.git_metadata_is_passive(candidate))

    def test_boot_identity_fails_closed(self) -> None:
        with mock.patch.object(
            live,
            "run",
            return_value=subprocess.CompletedProcess([], 0, "", ""),
        ):
            with self.assertRaises(ValueError):
                live.boot_session_sha256()

    def test_raw_worktree_scan_does_not_execute_clean_filter(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository = root / "repository"
            marker = root / "filter-executed"
            environment = {
                "HOME": str(root),
                "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
                "LANG": "C",
                "LC_ALL": "C",
            }

            def setup_git(*args: str) -> None:
                subprocess.run(
                    ["/usr/bin/git", *args],
                    check=True,
                    capture_output=True,
                    env=environment,
                )

            setup_git("init", "-q", str(repository))
            setup_git("-C", str(repository), "config", "user.name", "Fixture")
            setup_git(
                "-C",
                str(repository),
                "config",
                "user.email",
                "fixture@example.invalid",
            )
            (repository / ".gitattributes").write_text(
                "payload filter=probe\n",
                encoding="utf-8",
            )
            (repository / "payload").write_text("original\n", encoding="utf-8")
            setup_git("-C", str(repository), "add", ".")
            setup_git("-C", str(repository), "commit", "-qm", "fixture")
            setup_git(
                "-C",
                str(repository),
                "config",
                "filter.probe.clean",
                f"/usr/bin/touch {marker}",
            )
            (repository / "payload").write_text("modified\n", encoding="utf-8")
            index, index_errors = live.parse_index_listing(
                live.git(
                    repository,
                    "ls-files",
                    "--stage",
                    "-z",
                ).stdout
            )
            tree, tree_errors = live.parse_tree_listing(
                live.git(
                    repository,
                    "ls-tree",
                    "-r",
                    "-z",
                    "HEAD",
                ).stdout
            )
            findings = live.raw_worktree_status(
                repository,
                index,
                tree,
                [*index_errors, *tree_errors],
            )
            filter_executed = marker.exists()
        self.assertFalse(filter_executed)
        self.assertIn("content:payload", findings)

    def test_raw_worktree_scan_allows_only_exact_untracked_scratch(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory)
            scratch_file = candidate / "Game" / "Library" / "cache.bin"
            scratch_file.parent.mkdir(parents=True)
            scratch_file.write_bytes(b"scratch")
            self.assertEqual(
                [],
                live.raw_worktree_status(
                    candidate,
                    {},
                    {},
                    [],
                    ["Game/Library/"],
                ),
            )
            unexpected = candidate / "Game" / "Assets" / "payload.bin"
            unexpected.parent.mkdir(parents=True)
            unexpected.write_bytes(b"unexpected")
            findings = live.raw_worktree_status(
                candidate,
                {},
                {},
                [],
                ["Game/Library/"],
            )
        self.assertIn("untracked:Game/Assets/payload.bin", findings)

    def test_raw_worktree_scan_rejects_symlink_inside_allowed_scratch(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            candidate = root / "candidate"
            outside = root / "outside"
            candidate.mkdir()
            outside.mkdir()
            scratch = candidate / "Game" / "Library"
            scratch.parent.mkdir(parents=True)
            scratch.symlink_to(outside, target_is_directory=True)
            findings = live.raw_worktree_status(
                candidate,
                {},
                {},
                [],
                ["Game/Library/"],
            )
        self.assertIn("symlink:Game/Library", findings)

    def test_candidate_write_scope_rejects_writable_undeclared_descendant(
        self,
    ) -> None:
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory).resolve()
            allowed = candidate / "BuildArtifacts" / "WP-0001"
            undeclared = candidate / "Game" / "Assets"
            allowed.mkdir(parents=True)
            undeclared.mkdir(parents=True)
            payload = undeclared / "existing.txt"
            payload.write_text("fixture\n", encoding="utf-8")
            with (
                mock.patch.object(
                    live,
                    "can_create_probe",
                    side_effect=lambda path: path == undeclared,
                ),
                mock.patch.object(
                    live,
                    "write_open_is_denied",
                    return_value=True,
                ),
            ):
                safe, _, _ = live.candidate_write_scope_is_exact(
                    candidate,
                    ["BuildArtifacts/WP-0001/"],
                )
                self.assertFalse(safe)
            with (
                mock.patch.object(
                    live,
                    "can_create_probe",
                    return_value=False,
                ),
                mock.patch.object(
                    live,
                    "write_open_is_denied",
                    return_value=True,
                ),
            ):
                safe, files, directories = (
                    live.candidate_write_scope_is_exact(
                        candidate,
                        ["BuildArtifacts/WP-0001/"],
                    )
                )
                self.assertTrue(safe)
                self.assertGreaterEqual(files, 1)
                self.assertGreaterEqual(directories, 4)


if __name__ == "__main__":
    unittest.main()
