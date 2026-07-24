#!/usr/bin/env python3
"""Executable contract tests for the canonical Unity CLI launcher."""

from __future__ import annotations

import json
import os
from pathlib import Path
import stat
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
LAUNCHER = ROOT / "Tools" / "unity-canonical"
PINNED_REPOSITORY = Path(
    "/Users/sasha/Documents/Sasha the Atomic Land Pirate/Development/"
    "sasha-the-land-pirate"
)


class UnityCanonicalLauncherTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="unity-canonical-contract-"
        )
        self.root = Path(self.temporary.name).resolve()
        self.repository = self.root / "canonical-repository"
        self.project = self.repository / "Game"
        (self.project / "Assets").mkdir(parents=True)
        settings = self.project / "ProjectSettings"
        settings.mkdir()
        (settings / "ProjectVersion.txt").write_text(
            "m_EditorVersion: 6000.5.4f1\n",
            encoding="utf-8",
        )

        source = LAUNCHER.read_text(encoding="utf-8")
        pinned = str(PINNED_REPOSITORY)
        self.assertEqual(source.count(pinned), 1)
        self.launcher = self.root / "unity-canonical"
        self.launcher.write_text(
            source.replace(pinned, str(self.repository)),
            encoding="utf-8",
        )
        self.launcher.chmod(
            self.launcher.stat().st_mode | stat.S_IXUSR
        )

        self.capture = self.root / "capture.json"
        self.stub = self.root / "unity-stub"
        self.stub.write_text(
            "#!/usr/bin/env python3\n"
            "import json, os, pathlib, sys\n"
            "pathlib.Path(os.environ['UNITY_STUB_CAPTURE']).write_text(\n"
            "    json.dumps({\n"
            "        'argv': sys.argv[1:],\n"
            "        'cwd': os.getcwd(),\n"
            "        'project': os.environ.get('UNITY_PROJECT_PATH'),\n"
            "    }),\n"
            "    encoding='utf-8',\n"
            ")\n",
            encoding="utf-8",
        )
        self.stub.chmod(self.stub.stat().st_mode | stat.S_IXUSR)

        self.invocation_root = self.root / "invocation"
        self.invocation_root.mkdir()

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def run_launcher(self, *arguments: str) -> subprocess.CompletedProcess[str]:
        self.capture.unlink(missing_ok=True)
        environment = os.environ.copy()
        environment["UNITY_CLI_BIN"] = str(self.stub)
        environment["UNITY_STUB_CAPTURE"] = str(self.capture)
        return subprocess.run(
            [str(self.launcher), *arguments],
            cwd=self.invocation_root,
            env=environment,
            text=True,
            capture_output=True,
            check=False,
        )

    def accepted(self, *arguments: str) -> dict[str, object]:
        result = self.run_launcher(*arguments)
        self.assertEqual(result.returncode, 0, result.stderr)
        payload = json.loads(self.capture.read_text(encoding="utf-8"))
        self.assertEqual(payload["cwd"], str(self.project))
        self.assertEqual(payload["project"], str(self.project))
        return payload

    def rejected(self, *arguments: str) -> None:
        result = self.run_launcher(*arguments)
        self.assertEqual(result.returncode, 64, result.stderr)
        self.assertFalse(self.capture.exists())

    def test_supported_routes_inject_only_the_canonical_project(self) -> None:
        payload = self.accepted("test", "--mode", "PlayMode")
        self.assertEqual(
            payload["argv"],
            ["test", str(self.project), "--mode", "PlayMode"],
        )

        payload = self.accepted("command", "Unity_ReadConsole")
        self.assertEqual(
            payload["argv"],
            [
                "command",
                "--project-path",
                str(self.project),
                "Unity_ReadConsole",
            ],
        )

        payload = self.accepted("status", "--format", "json")
        self.assertEqual(
            payload["argv"],
            ["status", "--project", str(self.project), "--format", "json"],
        )

        payload = self.accepted("list")
        self.assertEqual(
            payload["argv"],
            ["list", "--project-path", str(self.project)],
        )

        payload = self.accepted("projects", "info")
        self.assertEqual(
            payload["argv"],
            ["projects", "info", str(self.project)],
        )

    def test_selector_escapes_are_rejected_case_insensitively(self) -> None:
        attempts = (
            ("status", "--project", "/other/Game"),
            ("status", "--PrOjEcT=/other/Game"),
            ("command", "--project-path", "/other/Game"),
            ("command", "--PROJECT-PATH=/other/Game"),
            ("open", "--args", "-projectPath /other/Game"),
            ("open", "--args=-PrOjEcTpAtH=/other/Game"),
            ("command", "--runtime", "OtherPlayer"),
            ("command", "--runtime-path=/tmp/other-player"),
        )
        for attempt in attempts:
            with self.subTest(attempt=attempt):
                self.rejected(*attempt)

    def test_unknown_and_hub_mutation_commands_are_rejected(self) -> None:
        attempts = (
            ("cloud-pipeline", "deploy"),
            ("cloud", "project", "link"),
            ("shell",),
            ("projects", "add", "/other/Game"),
            ("projects", "remove", "/other/Game"),
            ("projects", "link", "other"),
            ("projects", "unlink", "other"),
            ("projects", "pin", "other"),
            ("projects", "unpin", "other"),
        )
        for attempt in attempts:
            with self.subTest(attempt=attempt):
                self.rejected(*attempt)

    def test_project_operands_are_rejected_before_and_after_cwd_change(self) -> None:
        outside = self.invocation_root / "outside-project"
        (outside / "Assets").mkdir(parents=True)
        (outside / "ProjectSettings").mkdir()
        (outside / "ProjectSettings" / "ProjectVersion.txt").touch()
        self.rejected("test", "outside-project")

        post_change = self.project / "relative-project"
        (post_change / "Assets").mkdir(parents=True)
        (post_change / "ProjectSettings").mkdir()
        (post_change / "ProjectSettings" / "ProjectVersion.txt").touch()
        self.rejected("projects", "list", "relative-project")


if __name__ == "__main__":
    unittest.main()
