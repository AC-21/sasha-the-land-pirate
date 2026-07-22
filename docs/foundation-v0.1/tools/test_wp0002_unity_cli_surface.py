from __future__ import annotations

import json
import re
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
EDITOR_ROOT = (
    REPO_ROOT / "Game/Assets/AtomicLandPirate/LastBearing/Editor"
)
EDITMODE_ROOT = (
    REPO_ROOT / "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode"
)
SETTINGS_ROOT = REPO_ROOT / "Game/Assets/Settings/Pipeline"

EXPECTED_COMMANDS = {
    "AssetRefresh": ("wp0002_asset_refresh", "AssetRefreshGate"),
    "EditModeTests": ("wp0002_editmode_tests", "EditModeGate"),
    "PlayModeTests": ("wp0002_playmode_tests", "PlayModeGate"),
    "TechnicalCapture": (
        "wp0002_technical_capture",
        "TechnicalCaptureGate",
    ),
    "NativeBuild": ("wp0002_native_build", "NativeBuildGate"),
    "NativePerformanceStart": (
        "wp0002_native_performance_start",
        "NativePerformanceStartGate",
    ),
    "NativePerformanceCollect": (
        "wp0002_native_performance_collect",
        "NativePerformanceCollectGate",
    ),
}

PIPELINE_DEPENDENCIES = {
    "com.unity.test-framework": "1.1.33",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.nuget.newtonsoft-json": "3.0.2",
    "com.unity.nuget.mono-cecil": "1.11.6",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.screencapture": "1.0.0",
}


def braced_block_after(source: str, marker: str) -> str:
    start = source.find(marker)
    if start < 0:
        raise AssertionError(f"missing block marker {marker}")
    brace = source.find("{", start + len(marker))
    if brace < 0:
        raise AssertionError(f"missing body for {marker}")
    depth = 0
    for offset in range(brace, len(source)):
        if source[offset] == "{":
            depth += 1
        elif source[offset] == "}":
            depth -= 1
            if depth == 0:
                return source[brace : offset + 1]
    raise AssertionError(f"unterminated body for {marker}")


def method_body(source: str, method_name: str) -> str:
    marker = re.search(
        rf"public\s+static\s+string\s+{re.escape(method_name)}\s*\(",
        source,
    )
    if marker is None:
        raise AssertionError(f"missing wrapper method {method_name}")
    return braced_block_after(source, marker.group(0))


class WP0002UnityCliSurfaceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.wrappers = (EDITOR_ROOT / "WP0002CliGateCommands.cs").read_text(
            encoding="utf-8"
        )
        self.bootstrap = (EDITOR_ROOT / "WP0002PipelineBootstrap.cs").read_text(
            encoding="utf-8"
        )

    def test_wrappers_are_exact_one_argument_dispatches(self) -> None:
        command_names = re.findall(
            r'\[CliCommand\(\s*"([^"]+)"', self.wrappers
        )
        self.assertCountEqual(
            command_names,
            [command for command, _ in EXPECTED_COMMANDS.values()],
        )
        self.assertEqual(len(command_names), len(EXPECTED_COMMANDS))
        self.assertEqual(self.wrappers.count("[CliArg("), len(EXPECTED_COMMANDS))
        self.assertEqual(
            self.wrappers.count('"expected_source_sha256"'),
            len(EXPECTED_COMMANDS),
        )
        self.assertEqual(
            len(re.findall(r"(?<!MainThread)Required = true", self.wrappers)),
            len(EXPECTED_COMMANDS),
        )
        self.assertEqual(
            self.wrappers.count("MainThreadRequired = true"),
            len(EXPECTED_COMMANDS),
        )
        self.assertEqual(
            self.wrappers.count("RuntimeOnly = false"),
            len(EXPECTED_COMMANDS),
        )

        for method_name, (_, gate_name) in EXPECTED_COMMANDS.items():
            body = method_body(self.wrappers, method_name)
            compact = re.sub(r"\s+", "", body)
            self.assertEqual(
                compact,
                "{returnWP0002GateDispatcher.Dispatch("
                f"WP0002GateDispatcher.{gate_name},"
                "expectedSourceSha256);}",
                method_name,
            )
            self.assertEqual(
                body.count("WP0002GateDispatcher.Dispatch("), 1, method_name
            )

        forbidden = (
            "string gateId",
            '"gate_id"',
            '"eval"',
            '"eval_file"',
            "System.IO",
            "System.Net",
            "System.Diagnostics",
            "File.",
            "Directory.",
            "Process.",
            "AssetDatabase",
            "EditorApplication",
            "UnityEngine",
            "UnityEditor",
            "UnityWebRequest",
            "BuildPipeline",
            "PackageManager",
        )
        for token in forbidden:
            self.assertNotIn(token, self.wrappers)

    def test_bootstrap_installs_only_filtered_discovery_before_start(self) -> None:
        required = (
            "[InitializeOnLoad]",
            "Application.isBatchMode",
            "AssetDatabase.IsAssetImportWorkerProcess()",
            "ProcessService.level == ProcessLevel.Main",
            "/Users/sasha/Documents/Sasha the Atomic Land Pirate/Development/",
            "sasha-the-land-pirate/Game",
            'Path.Combine(Application.dataPath, "..")',
            'AssetDatabase.FindAssets("t:EditorPipelineManager")',
            "Assets/Settings/Pipeline/EditorPipelineManager.asset",
            "a615acb88f7e4559a2831dad2ac14921",
            "settingsGuids.Length != 1",
            "AssetDatabase.GUIDToAssetPath(settingsGuids[0])",
            "AssetDatabase.LoadAssetAtPath<",
            "settings.AutoStart ||",
            "settings.WatchdogEnabled ||",
            "RuntimeHelpers.RunClassConstructor(",
            "typeof(PipelineServerStartup).TypeHandle",
            "CommandRegistry.SetDiscovery(discovery);",
            "ValidateRegisteredCommands(CommandRegistry.DiscoverCommands());",
            "PipelineServerStartup.EnsureServerStarted();",
            "typeof(T) == typeof(CliCommandAttribute)",
            "Array.Empty<MethodInfo>()",
            "PipelineServerStartup.StopServer();",
            "private static void OnPostprocessAllAssets(",
            "bool didDomainReload",
            "if (!didDomainReload || s_Attempted)",
            "s_Attempted = true;",
            "WP0002PipelineBootstrap.ConfigureAndStartAfterDomainReload();",
        )
        for token in required:
            self.assertIn(token, self.bootstrap)
        self.assertEqual(
            self.bootstrap.count("AutoTickCommand.SetAutoTick(false);"), 2
        )

        early_guard = braced_block_after(
            self.bootstrap, "static WP0002PipelineBootstrap()"
        )
        configure = braced_block_after(
            self.bootstrap,
            "internal static void ConfigureAndStartAfterDomainReload()",
        )
        postprocess = braced_block_after(
            self.bootstrap, "private static void OnPostprocessAllAssets("
        )

        self.assertIn("Application.isBatchMode", early_guard)
        self.assertIn("ProcessService.level == ProcessLevel.Main", early_guard)
        self.assertIn("RuntimeHelpers.RunClassConstructor(", early_guard)
        self.assertIn("PipelineServerStartup.StopServer();", early_guard)
        self.assertLess(
            early_guard.index("PipelineServerStartup.StopServer();"),
            early_guard.index("AutoTickCommand.SetAutoTick(false);"),
        )
        self.assertNotIn("AssetDatabase", early_guard)
        self.assertNotIn("Path.", early_guard)
        self.assertNotIn("CommandRegistry", early_guard)

        validate_project = configure.index("ValidateCanonicalProjectRoot();")
        reject_preexisting_server = configure.index(
            "FailIfServerRunningBeforeAssetValidation();"
        )
        load_settings = configure.index("LoadExactSettingsAsset();")
        validate_settings = configure.index("ValidateSettings(settings);")
        install_filter = configure.index(
            "CommandRegistry.SetDiscovery(discovery);"
        )
        validate_registry = configure.index(
            "ValidateRegisteredCommands(CommandRegistry.DiscoverCommands());"
        )
        start_server = configure.index(
            "PipelineServerStartup.EnsureServerStarted();"
        )
        self.assertLess(reject_preexisting_server, validate_project)
        self.assertLess(validate_project, load_settings)
        self.assertLess(load_settings, validate_settings)
        self.assertLess(validate_settings, install_filter)
        self.assertLess(install_filter, validate_registry)
        self.assertLess(validate_registry, start_server)
        self.assertIn("catch (Exception exception)", configure)
        self.assertIn("PipelineServerStartup.StopServer();", configure)

        domain_guard = postprocess.index(
            "if (!didDomainReload || s_Attempted)"
        )
        attempted = postprocess.index("s_Attempted = true;")
        configure_call = postprocess.index(
            "WP0002PipelineBootstrap.ConfigureAndStartAfterDomainReload();"
        )
        self.assertLess(domain_guard, attempted)
        self.assertLess(attempted, configure_call)

        pre_asset_cleanup = braced_block_after(
            self.bootstrap,
            "private static void FailIfServerRunningBeforeAssetValidation()",
        )
        self.assertLess(
            pre_asset_cleanup.index("PipelineServerStartup.StopServer();"),
            pre_asset_cleanup.index("AutoTickCommand.SetAutoTick(false);"),
        )
        self.assertIn(
            "projectRoot,\n                    CanonicalProjectRoot,\n"
            "                    StringComparison.Ordinal",
            self.bootstrap,
        )

        for method_name, (command_name, _) in EXPECTED_COMMANDS.items():
            self.assertEqual(
                self.bootstrap.count(
                    f"nameof(WP0002CliGateCommands.{method_name})"
                ),
                1,
            )
            self.assertEqual(self.bootstrap.count(f'"{command_name}"'), 1)

        forbidden = (
            "TypeCacheCommandDiscovery",
            "SetDiscovery(null",
            "System.Net",
            "System.Diagnostics",
            "HttpClient",
            "File.",
            "Directory.",
            "Process.",
            "EditorApplication.delayCall",
            "UnityWebRequest",
            "EditorPrefs",
            "PlayerPrefs",
            "Selection.",
            "ExecuteMenuItem",
            "BuildPipeline",
            "PackageManager",
            "Debug.",
            "AssetDatabase.Create",
            "AssetDatabase.Delete",
            "AssetDatabase.Move",
            "AssetDatabase.Copy",
            "AssetDatabase.Import",
            "AssetDatabase.Refresh",
            "AssetDatabase.SaveAssets",
            "AssetDatabase.StartAssetEditing",
        )
        for token in forbidden:
            self.assertNotIn(token, self.bootstrap)

    def test_settings_asset_is_exact_fail_closed_profile(self) -> None:
        asset = (SETTINGS_ROOT / "EditorPipelineManager.asset").read_text(
            encoding="utf-8"
        )
        fields = dict(
            re.findall(
                r"^  (m_(?:Port|AutoStart|WatchdogEnabled|"
                r"WatchdogIntervalSeconds|LogRequestsResponses)): (\d+)$",
                asset,
                flags=re.MULTILINE,
            )
        )
        self.assertEqual(
            fields,
            {
                "m_Port": "0",
                "m_AutoStart": "0",
                "m_WatchdogEnabled": "0",
                "m_WatchdogIntervalSeconds": "5",
                "m_LogRequestsResponses": "0",
            },
        )
        self.assertEqual(asset.count("m_Script:"), 1)
        self.assertIn(
            "m_Script: {fileID: 11500000, "
            "guid: 9e6f601455c0e0549b841c969d30c2b7, type: 3}",
            asset,
        )
        meta = (SETTINGS_ROOT / "EditorPipelineManager.asset.meta").read_text(
            encoding="utf-8"
        )
        self.assertIn("mainObjectFileID: 11400000", meta)
        self.assertIn("guid: a615acb88f7e4559a2831dad2ac14921", meta)
        self.assertTrue(
            (SETTINGS_ROOT.parent / "Pipeline.meta").is_file(),
            "Pipeline folder metadata is missing",
        )
        folder_meta = (SETTINGS_ROOT.parent / "Pipeline.meta").read_text(
            encoding="utf-8"
        )
        self.assertIn("folderAsset: yes", folder_meta)
        self.assertEqual(
            list(SETTINGS_ROOT.glob("*.asset")),
            [SETTINGS_ROOT / "EditorPipelineManager.asset"],
        )

    def test_native_scene_has_no_broad_runtime_pipeline_manager(self) -> None:
        scene = (
            REPO_ROOT
            / "Game/Assets/AtomicLandPirate/LastBearing/Scenes/LastBearing.unity"
        ).read_text(encoding="utf-8")
        self.assertNotIn("dd4ab1c9091e0194196b5f5d51cb932f", scene)

    def test_package_and_assembly_references_are_exactly_pinned(self) -> None:
        manifest = json.loads(
            (REPO_ROOT / "Game/Packages/manifest.json").read_text(
                encoding="utf-8"
            )
        )
        lock = json.loads(
            (REPO_ROOT / "Game/Packages/packages-lock.json").read_text(
                encoding="utf-8"
            )
        )
        self.assertEqual(
            manifest["dependencies"]["com.unity.pipeline"], "0.3.1-exp.1"
        )
        self.assertNotIn("testables", manifest)
        pipeline = lock["dependencies"]["com.unity.pipeline"]
        self.assertEqual(
            pipeline,
            {
                "version": "0.3.1-exp.1",
                "depth": 0,
                "source": "registry",
                "dependencies": PIPELINE_DEPENDENCIES,
                "url": "https://packages.unity.com",
            },
        )
        self.assertEqual(
            lock["dependencies"]["com.unity.modules.screencapture"],
            {
                "version": "1.0.0",
                "depth": 1,
                "source": "builtin",
                "dependencies": {
                    "com.unity.modules.imageconversion": "1.0.0"
                },
            },
        )

        editor_asmdef = json.loads(
            (EDITOR_ROOT / "AC21.Sasha.LastBearing.Editor.asmdef").read_text(
                encoding="utf-8"
            )
        )
        self.assertIn("Unity.Pipeline", editor_asmdef["references"])
        self.assertIn("Unity.Pipeline.Editor", editor_asmdef["references"])
        test_asmdef = json.loads(
            (EDITMODE_ROOT / "AC21.Sasha.LastBearing.EditModeTests.asmdef")
            .read_text(encoding="utf-8")
        )
        self.assertIn("Unity.Pipeline", test_asmdef["references"])
        self.assertIn("Unity.Pipeline.Editor", test_asmdef["references"])


if __name__ == "__main__":
    unittest.main()
