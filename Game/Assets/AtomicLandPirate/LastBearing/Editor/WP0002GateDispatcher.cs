#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AtomicLandPirate.Presentation.LastBearing;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtomicLandPirate.Presentation.LastBearing.Editor
{
    /// <summary>
    /// The only authorized target for WP-0002 Unity_RunCommand. Calls bind to
    /// this file's bytes and one of four enumerated gate IDs; no free-form
    /// editor operation, path, test filter, or capture target is accepted.
    /// </summary>
    public static class WP0002GateDispatcher
    {
        public const string AssetRefreshGate = "asset-refresh-and-compile";
        public const string EditModeGate = "wp0002-editmode-test-assembly";
        public const string PlayModeGate = "wp0002-playmode-test-assembly";
        public const string TechnicalCaptureGate = "wp0002-technical-capture";

        private const string RuntimeAssembly = "AC21.Sasha.LastBearing.Runtime";
        private const string EditModeAssembly = "AC21.Sasha.LastBearing.EditModeTests";
        private const string PlayModeAssembly = "AC21.Sasha.LastBearing.PlayModeTests";
        private const string DispatcherAssetPath =
            "Assets/AtomicLandPirate/LastBearing/Editor/WP0002GateDispatcher.cs";
        private const string BoundaryRelativePath =
            "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json";

        private static readonly HashSet<string> AllowedGateIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                AssetRefreshGate,
                EditModeGate,
                PlayModeGate,
                TechnicalCaptureGate
            };

        private static readonly List<TestGateCallbacks> ActiveTestCallbacks =
            new List<TestGateCallbacks>();

        public static string Dispatch(string gateId, string expectedSourceSha256)
        {
            string startedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            if (!AllowedGateIds.Contains(gateId))
            {
                throw new InvalidOperationException("WP0002_GATE_ID_REJECTED");
            }

            ProjectBoundary boundary = VerifyProjectBoundary();
            ValidateSha256(expectedSourceSha256);
            string actualSourceSha256 = ComputeSha256(boundary.DispatcherSourcePath);
            if (!string.Equals(
                    expectedSourceSha256,
                    actualSourceSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("WP0002_DISPATCHER_HASH_MISMATCH");
            }

            Debug.Log(
                "WP0002_GATE_STARTED " + gateId + " " + actualSourceSha256);

            switch (gateId)
            {
                case AssetRefreshGate:
                    return RunAssetRefresh(boundary, actualSourceSha256, startedAt);
                case EditModeGate:
                    return StartTestGate(
                        boundary,
                        gateId,
                        actualSourceSha256,
                        startedAt,
                        TestMode.EditMode,
                        EditModeAssembly);
                case PlayModeGate:
                    return StartTestGate(
                        boundary,
                        gateId,
                        actualSourceSha256,
                        startedAt,
                        TestMode.PlayMode,
                        PlayModeAssembly);
                case TechnicalCaptureGate:
                    return RunTechnicalCapture(
                        boundary,
                        actualSourceSha256,
                        startedAt);
                default:
                    throw new InvalidOperationException("WP0002_GATE_ID_REJECTED");
            }
        }

        private static string RunAssetRefresh(
            ProjectBoundary boundary,
            string sourceSha256,
            string startedAt)
        {
            if (EditorApplication.isCompiling)
            {
                return CompleteGate(
                    boundary,
                    AssetRefreshGate,
                    sourceSha256,
                    "AssetDatabase.Refresh(ForceSynchronousImport|ForceUpdate)",
                    "rejected:editor-already-compiling",
                    startedAt);
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            string result;
            if (EditorApplication.isCompiling)
            {
                result = "rejected:compilation-started-retry-after-completion";
            }
            else if (LastScriptCompilationFailed())
            {
                result = "failed:last-script-compilation-reported-errors";
            }
            else
            {
                result = "success:assets-refreshed-editor-idle-no-known-compilation-failure";
            }

            return CompleteGate(
                boundary,
                AssetRefreshGate,
                sourceSha256,
                "AssetDatabase.Refresh(ForceSynchronousImport|ForceUpdate)",
                result,
                startedAt);
        }

        private static string StartTestGate(
            ProjectBoundary boundary,
            string gateId,
            string sourceSha256,
            string startedAt,
            TestMode mode,
            string assemblyName)
        {
            if (ActiveTestCallbacks.Count != 0)
            {
                return CompleteGate(
                    boundary,
                    gateId,
                    sourceSha256,
                    "TestRunnerApi:" + assemblyName,
                    "rejected:test-gate-already-active",
                    startedAt);
            }

            var filter = new Filter
            {
                testMode = mode,
                assemblyNames = new[] { assemblyName }
            };
            var settings = new ExecutionSettings(filter);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new TestGateCallbacks(
                api,
                boundary,
                gateId,
                sourceSha256,
                startedAt,
                assemblyName);
            ActiveTestCallbacks.Add(callbacks);
            api.RegisterCallbacks(callbacks);
            string runId = api.Execute(settings);
            callbacks.BindRun(runId);
            return "WP0002_GATE_ASYNC " + gateId + " " + sourceSha256;
        }

        private static bool LastScriptCompilationFailed()
        {
            PropertyInfo? property = typeof(EditorUtility).GetProperty(
                "scriptCompilationFailed",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || property.PropertyType != typeof(bool))
            {
                throw new InvalidOperationException(
                    "WP0002_COMPILATION_STATUS_UNAVAILABLE");
            }

            object? value = property.GetValue(null, null);
            return value is bool failed && failed;
        }

        private static string RunTechnicalCapture(
            ProjectBoundary boundary,
            string sourceSha256,
            string startedAt)
        {
            if (!Application.isPlaying)
            {
                return CompleteGate(
                    boundary,
                    TechnicalCaptureGate,
                    sourceSha256,
                    "validate-runtime-root-camera-controller",
                    "rejected:not-in-play-mode",
                    startedAt);
            }

            LastBearingGameController[] controllers =
                UnityEngine.Object.FindObjectsByType<LastBearingGameController>(
                    FindObjectsInactive.Include);
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);
            bool valid =
                SceneManager.GetActiveScene().name == LastBearingBootstrap.SceneName &&
                controllers.Length == 1 &&
                controllers[0].name == LastBearingGameController.RuntimeRootName &&
                controllers[0].World != null &&
                controllers[0].World!.MainCamera != null &&
                cameras.Length >= 1;

            return CompleteGate(
                boundary,
                TechnicalCaptureGate,
                sourceSha256,
                "validate-runtime-root-camera-controller",
                valid
                    ? "success:capture-ready"
                    : "failed:runtime-capture-contract",
                startedAt);
        }

        private static ProjectBoundary VerifyProjectBoundary()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            if (!string.Equals(
                    new DirectoryInfo(projectRoot).Name,
                    "Game",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("WP0002_PROJECT_ROOT_REJECTED");
            }

            string repositoryRoot = Directory.GetParent(projectRoot)?.FullName ??
                                    throw new InvalidOperationException(
                                        "WP0002_REPOSITORY_ROOT_MISSING");
            string dispatcherSourcePath = Path.GetFullPath(
                Path.Combine(projectRoot, DispatcherAssetPath));
            string boundaryPath = Path.GetFullPath(
                Path.Combine(repositoryRoot, BoundaryRelativePath));
            string agentsPath = Path.Combine(repositoryRoot, "AGENTS.md");
            string simulationPackage = Path.Combine(
                repositoryRoot,
                "SimulationCore",
                "package.json");
            string savePackage = Path.Combine(
                repositoryRoot,
                "SaveContracts",
                "package.json");

            if (!File.Exists(dispatcherSourcePath) ||
                !File.Exists(boundaryPath) ||
                !File.Exists(agentsPath) ||
                !File.Exists(simulationPackage) ||
                !File.Exists(savePackage))
            {
                throw new InvalidOperationException("WP0002_REPOSITORY_MARKER_MISSING");
            }

            string boundaryText = File.ReadAllText(boundaryPath, Encoding.UTF8);
            if (!boundaryText.Contains("\"packet_id\": \"WP-0002\"") ||
                !boundaryText.Contains("\"lifecycle_state\": \"attested\"") ||
                !boundaryText.Contains("\"boundary_mode\": \"local-development\""))
            {
                throw new InvalidOperationException("WP0002_BOUNDARY_MARKER_REJECTED");
            }

            return new ProjectBoundary(
                repositoryRoot,
                dispatcherSourcePath,
                Path.Combine(
                    repositoryRoot,
                    "BuildArtifacts",
                    "WP-0002",
                    "unity-gates"));
        }

        private static string CompleteGate(
            ProjectBoundary boundary,
            string gateId,
            string sourceSha256,
            string command,
            string result,
            string startedAt)
        {
            string completedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            WriteEvidence(
                boundary,
                gateId,
                sourceSha256,
                command,
                result,
                startedAt,
                completedAt);
            string line =
                "WP0002_GATE_COMPLETED " + gateId + " " + sourceSha256 + " " + result;
            Debug.Log(line);
            return line;
        }

        private static void WriteEvidence(
            ProjectBoundary boundary,
            string gateId,
            string sourceSha256,
            string command,
            string result,
            string startedAt,
            string completedAt)
        {
            Directory.CreateDirectory(boundary.EvidenceDirectory);
            var record = new GateEvidence
            {
                schema_version = 1,
                gate_id = gateId,
                source_sha256 = sourceSha256,
                command = command,
                result = result,
                started_at = startedAt,
                completed_at = completedAt,
                project_root = "Game",
                runtime_assembly = RuntimeAssembly
            };
            string fileName =
                completedAt.Replace(":", string.Empty).Replace(".", string.Empty) +
                "-" + gateId + "-" + Guid.NewGuid().ToString("N") + ".json";
            string path = Path.Combine(boundary.EvidenceDirectory, fileName);
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(JsonUtility.ToJson(record, true));
            writer.Write('\n');
        }

        private static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            byte[] digest = sha.ComputeHash(stream);
            var builder = new StringBuilder(64);
            foreach (byte value in digest)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void ValidateSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                throw new InvalidOperationException("WP0002_DISPATCHER_HASH_INVALID");
            }

            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    throw new InvalidOperationException(
                        "WP0002_DISPATCHER_HASH_INVALID");
                }
            }
        }

        private sealed class TestGateCallbacks : IErrorCallbacks
        {
            private readonly TestRunnerApi _api;
            private readonly ProjectBoundary _boundary;
            private readonly string _gateId;
            private readonly string _sourceSha256;
            private readonly string _startedAt;
            private readonly string _assemblyName;
            private string _runId = string.Empty;
            private int _discoveredTestCases;
            private bool _completed;

            internal TestGateCallbacks(
                TestRunnerApi api,
                ProjectBoundary boundary,
                string gateId,
                string sourceSha256,
                string startedAt,
                string assemblyName)
            {
                _api = api;
                _boundary = boundary;
                _gateId = gateId;
                _sourceSha256 = sourceSha256;
                _startedAt = startedAt;
                _assemblyName = assemblyName;
            }

            internal void BindRun(string runId)
            {
                if (string.IsNullOrEmpty(runId))
                {
                    Finish("failed:test-run-id-missing");
                    return;
                }

                _runId = runId;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                _discoveredTestCases = testsToRun.TestCaseCount;
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                bool passed =
                    _discoveredTestCases > 0 &&
                    result.PassCount > 0 &&
                    result.FailCount == 0 &&
                    result.InconclusiveCount == 0;
                string outcome = passed
                    ? "success:run=" + _runId + ";discovered=" +
                      _discoveredTestCases + ";passed=" + result.PassCount +
                      ";skipped=" + result.SkipCount
                    : "failed:run=" + _runId + ";discovered=" +
                      _discoveredTestCases + ";passed=" + result.PassCount +
                      ";failed=" + result.FailCount + ";inconclusive=" +
                      result.InconclusiveCount + ";skipped=" + result.SkipCount;
                Finish(outcome);
            }

            public void OnError(string message)
            {
                string normalized = (message ?? string.Empty)
                    .Replace('\r', ' ')
                    .Replace('\n', ' ');
                if (normalized.Length > 512)
                {
                    normalized = normalized.Substring(0, 512);
                }

                Finish("failed:test-run-error=" + normalized);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            private void Finish(string outcome)
            {
                if (_completed)
                {
                    return;
                }

                _completed = true;
                try
                {
                    CompleteGate(
                        _boundary,
                        _gateId,
                        _sourceSha256,
                        "TestRunnerApi:" + _assemblyName,
                        outcome,
                        _startedAt);
                }
                finally
                {
                    _api.UnregisterCallbacks(this);
                    ActiveTestCallbacks.Remove(this);
                    UnityEngine.Object.DestroyImmediate(_api);
                }
            }
        }

        private readonly struct ProjectBoundary
        {
            internal ProjectBoundary(
                string repositoryRoot,
                string dispatcherSourcePath,
                string evidenceDirectory)
            {
                RepositoryRoot = repositoryRoot;
                DispatcherSourcePath = dispatcherSourcePath;
                EvidenceDirectory = evidenceDirectory;
            }

            internal string RepositoryRoot { get; }
            internal string DispatcherSourcePath { get; }
            internal string EvidenceDirectory { get; }
        }

        [Serializable]
        private sealed class GateEvidence
        {
            public int schema_version;
            public string gate_id = string.Empty;
            public string source_sha256 = string.Empty;
            public string command = string.Empty;
            public string result = string.Empty;
            public string started_at = string.Empty;
            public string completed_at = string.Empty;
            public string project_root = string.Empty;
            public string runtime_assembly = string.Empty;
        }
    }
}
