#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AtomicLandPirate.Presentation.LastBearing;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(
    typeof(AtomicLandPirate.Presentation.LastBearing.Editor.WP0002TestRunCallback))]

namespace AtomicLandPirate.Presentation.LastBearing.Editor
{
    /// <summary>
    /// The only authorized target for WP-0002 Unity_RunCommand. Calls bind to
    /// this file's bytes and one of four enumerated gate IDs; no free-form
    /// editor operation, path, test filter, or capture target is accepted.
    /// </summary>
    [InitializeOnLoad]
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
        private const string StartingPhase = "starting";
        private const string RunningPhase = "running";
        private const string CompletingPhase = "completing";
        private const int TestGateTimeoutMinutes = 10;

        private static readonly HashSet<string> AllowedGateIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                AssetRefreshGate,
                EditModeGate,
                PlayModeGate,
                TechnicalCaptureGate
            };

        private static TransientTestErrorCallbacks? _activeTransientCallback;
        private static double _nextWatchdogAt;

        static WP0002GateDispatcher()
        {
            RestoreTransientCallback();
            EditorApplication.update += WatchPendingTestGate;
        }

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
            string? pendingRejection = ResolvePendingTestGate(boundary);
            if (pendingRejection != null)
            {
                return CompleteGate(
                    boundary,
                    gateId,
                    sourceSha256,
                    "TestRunnerApi:" + assemblyName,
                    pendingRejection,
                    startedAt);
            }

            var filter = new Filter
            {
                testMode = mode,
                assemblyNames = new[] { assemblyName }
            };
            var settings = new ExecutionSettings(filter);
            var pending = new PendingTestGate
            {
                schema_version = 1,
                invocation_id = Guid.NewGuid().ToString("N"),
                gate_id = gateId,
                source_sha256 = sourceSha256,
                started_at = startedAt,
                deadline_at = DateTime.UtcNow
                    .AddMinutes(TestGateTimeoutMinutes)
                    .ToString("O", CultureInfo.InvariantCulture),
                assembly_name = assemblyName,
                phase = StartingPhase,
                editor_process_id = CurrentEditorProcessId(),
                editor_process_started_at = CurrentEditorProcessStartedAt()
            };
            TestRunnerApi? api = null;
            string runId = string.Empty;
            try
            {
                WritePendingTestGate(boundary, pending);
                RegisterTransient(pending.invocation_id);
                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                runId = api.Execute(settings);
                if (!Guid.TryParse(runId, out _))
                {
                    throw new InvalidOperationException(
                        "WP0002_TEST_RUN_ID_INVALID");
                }

                pending.run_id = runId;
                pending.phase = RunningPhase;
                ReplacePendingTestGate(boundary, pending);
                return "WP0002_GATE_ASYNC " + gateId + " " + sourceSha256;
            }
            catch (Exception exception)
            {
                ReleaseTransient(pending.invocation_id);

                if (Guid.TryParse(runId, out _))
                {
                    try
                    {
                        TestRunnerApi.CancelTestRun(runId);
                    }
                    catch (Exception cancelException)
                    {
                        Debug.LogError(
                            "WP0002_TEST_RUN_CANCEL_FAILED " +
                            NormalizeMessage(cancelException.Message));
                    }
                }

                string outcome = "failed:test-run-start=" +
                                 NormalizeMessage(exception.Message);
                return File.Exists(boundary.PendingTestGatePath)
                    ? FinishPendingTestGate(boundary, pending, outcome)
                    : CompleteGate(
                        boundary,
                        gateId,
                        sourceSha256,
                        "TestRunnerApi:" + assemblyName,
                        outcome,
                        startedAt);
            }
            finally
            {
                if (api != null)
                {
                    UnityEngine.Object.DestroyImmediate(api);
                }
            }
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
                    "unity-gates"),
                Path.Combine(
                    projectRoot,
                    "Library",
                    "AC21.Sasha.LastBearing",
                    "wp0002-pending-test-gate.json"));
        }

        private static string CompleteGate(
            ProjectBoundary boundary,
            string gateId,
            string sourceSha256,
            string command,
            string result,
            string startedAt)
        {
            return CompleteGate(
                boundary,
                gateId,
                sourceSha256,
                command,
                result,
                startedAt,
                Guid.NewGuid().ToString("N"),
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }

        private static string CompleteGate(
            ProjectBoundary boundary,
            string gateId,
            string sourceSha256,
            string command,
            string result,
            string startedAt,
            string invocationId,
            string completedAt)
        {
            WriteEvidence(
                boundary,
                gateId,
                sourceSha256,
                command,
                result,
                startedAt,
                completedAt,
                invocationId);
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
            string completedAt,
            string invocationId)
        {
            if (!Guid.TryParseExact(invocationId, "N", out _))
            {
                throw new InvalidOperationException(
                    "WP0002_EVIDENCE_INVOCATION_ID_INVALID");
            }

            Directory.CreateDirectory(boundary.EvidenceDirectory);
            var record = new GateEvidence
            {
                schema_version = 1,
                invocation_id = invocationId,
                gate_id = gateId,
                source_sha256 = sourceSha256,
                command = command,
                result = result,
                started_at = startedAt,
                completed_at = completedAt,
                project_root = "Game",
                runtime_assembly = RuntimeAssembly
            };
            byte[] payload = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(record, true) + "\n");
            string path = Path.Combine(
                boundary.EvidenceDirectory,
                gateId + "-" + invocationId + ".json");
            WriteImmutableFile(path, payload);
        }

        private static void WriteImmutableFile(string path, byte[] payload)
        {
            if (File.Exists(path))
            {
                VerifyExactFile(path, payload);
                return;
            }

            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") +
                                   ".tmp";
            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }

                try
                {
                    File.Move(temporaryPath, path);
                }
                catch (IOException) when (File.Exists(path))
                {
                    VerifyExactFile(path, payload);
                }

                VerifyExactFile(path, payload);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void VerifyExactFile(string path, byte[] expected)
        {
            byte[] actual = File.ReadAllBytes(path);
            if (actual.Length != expected.Length)
            {
                throw new InvalidOperationException(
                    "WP0002_EVIDENCE_RECORD_CONFLICT");
            }

            for (int index = 0; index < actual.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    throw new InvalidOperationException(
                        "WP0002_EVIDENCE_RECORD_CONFLICT");
                }
            }
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

        private static string? ResolvePendingTestGate(ProjectBoundary boundary)
        {
            if (!File.Exists(boundary.PendingTestGatePath))
            {
                return null;
            }

            PendingTestGate pending;
            try
            {
                pending = ReadPendingTestGate(boundary);
            }
            catch (Exception exception)
            {
                return "rejected:pending-test-gate-invalid=" +
                       NormalizeMessage(exception.Message);
            }

            if (OwnsCurrentEditorProcess(pending))
            {
                if (string.Equals(
                        pending.phase,
                        CompletingPhase,
                        StringComparison.Ordinal))
                {
                    FinishPendingTestGate(
                        boundary,
                        pending,
                        pending.result);
                    return null;
                }

                return "rejected:test-gate-already-active";
            }

            FinishPendingTestGate(
                boundary,
                pending,
                "failed:editor-session-ended-before-test-completion");
            return null;
        }

        private static void WritePendingTestGate(
            ProjectBoundary boundary,
            PendingTestGate pending)
        {
            string? directory = Path.GetDirectoryName(
                boundary.PendingTestGatePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException(
                    "WP0002_PENDING_TEST_GATE_PATH_INVALID");
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = boundary.PendingTestGatePath + "." +
                                   pending.invocation_id + ".tmp";
            byte[] payload = SerializePendingTestGate(pending);
            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }

                File.Move(temporaryPath, boundary.PendingTestGatePath);
                VerifyExactFile(boundary.PendingTestGatePath, payload);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void ReplacePendingTestGate(
            ProjectBoundary boundary,
            PendingTestGate pending)
        {
            PendingTestGate current = ReadPendingTestGate(boundary);
            if (!SamePendingInvocation(current, pending) ||
                !ValidPendingPhaseTransition(current, pending))
            {
                throw new InvalidOperationException(
                    "WP0002_PENDING_TEST_GATE_TRANSITION_REJECTED");
            }

            byte[] payload = SerializePendingTestGate(pending);
            string temporaryPath = boundary.PendingTestGatePath + "." +
                                   pending.invocation_id + ".replace.tmp";
            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }

                File.Replace(
                    temporaryPath,
                    boundary.PendingTestGatePath,
                    null);
                VerifyExactFile(boundary.PendingTestGatePath, payload);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static byte[] SerializePendingTestGate(PendingTestGate pending)
        {
            return Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(pending, true) + "\n");
        }

        private static bool SamePendingInvocation(
            PendingTestGate left,
            PendingTestGate right)
        {
            return left.schema_version == right.schema_version &&
                   left.editor_process_id == right.editor_process_id &&
                   string.Equals(
                       left.invocation_id,
                       right.invocation_id,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.gate_id,
                       right.gate_id,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.source_sha256,
                       right.source_sha256,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.started_at,
                       right.started_at,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.deadline_at,
                       right.deadline_at,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.assembly_name,
                       right.assembly_name,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.editor_process_started_at,
                       right.editor_process_started_at,
                       StringComparison.Ordinal);
        }

        private static bool ValidPendingPhaseTransition(
            PendingTestGate current,
            PendingTestGate next)
        {
            if (string.Equals(
                    current.phase,
                    StartingPhase,
                    StringComparison.Ordinal) &&
                string.Equals(
                    next.phase,
                    RunningPhase,
                    StringComparison.Ordinal))
            {
                return string.IsNullOrEmpty(current.run_id) &&
                       Guid.TryParse(next.run_id, out _);
            }

            return (string.Equals(
                        current.phase,
                        StartingPhase,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        current.phase,
                        RunningPhase,
                        StringComparison.Ordinal)) &&
                   string.Equals(
                       next.phase,
                       CompletingPhase,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       current.run_id,
                       next.run_id,
                       StringComparison.Ordinal) &&
                   !string.IsNullOrEmpty(next.result) &&
                   !string.IsNullOrEmpty(next.completed_at);
        }

        private static PendingTestGate ReadPendingTestGate(
            ProjectBoundary boundary)
        {
            string serialized = File.ReadAllText(
                boundary.PendingTestGatePath,
                Encoding.UTF8);
            PendingTestGate? pending =
                JsonUtility.FromJson<PendingTestGate>(serialized);
            bool validPhase = pending != null &&
                              (string.Equals(
                                   pending.phase,
                                   StartingPhase,
                                   StringComparison.Ordinal) ||
                               string.Equals(
                                   pending.phase,
                                   RunningPhase,
                                   StringComparison.Ordinal) ||
                               string.Equals(
                                   pending.phase,
                                   CompletingPhase,
                                   StringComparison.Ordinal));
            bool validRunId = pending != null &&
                              (string.Equals(
                                   pending.phase,
                                   StartingPhase,
                                   StringComparison.Ordinal)
                                  ? string.IsNullOrEmpty(pending.run_id)
                                  : string.Equals(
                                        pending.phase,
                                        RunningPhase,
                                        StringComparison.Ordinal)
                                      ? Guid.TryParse(pending.run_id, out _)
                                      : string.IsNullOrEmpty(pending.run_id) ||
                                        Guid.TryParse(pending.run_id, out _));
            bool validCompletion = pending != null &&
                                   (string.Equals(
                                        pending.phase,
                                        CompletingPhase,
                                        StringComparison.Ordinal)
                                       ? !string.IsNullOrEmpty(pending.result) &&
                                         DateTime.TryParse(
                                             pending.completed_at,
                                             CultureInfo.InvariantCulture,
                                             DateTimeStyles.RoundtripKind,
                                             out _)
                                       : string.IsNullOrEmpty(pending.result) &&
                                         string.IsNullOrEmpty(
                                             pending.completed_at));
            if (pending == null ||
                pending.schema_version != 1 ||
                !Guid.TryParseExact(pending.invocation_id, "N", out _) ||
                !validPhase ||
                !validRunId ||
                !validCompletion ||
                pending.editor_process_id <= 0 ||
                !DateTime.TryParse(
                    pending.started_at,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _) ||
                !DateTime.TryParse(
                    pending.deadline_at,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _) ||
                !DateTime.TryParse(
                    pending.editor_process_started_at,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                throw new InvalidOperationException(
                    "WP0002_PENDING_TEST_GATE_INVALID");
            }

            ValidateSha256(pending.source_sha256);
            if (!string.Equals(
                    ExpectedAssemblyForGate(pending.gate_id),
                    pending.assembly_name,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "WP0002_PENDING_TEST_GATE_ASSEMBLY_REJECTED");
            }

            return pending;
        }

        internal static void HandleTestRunFinished(ITestResult result)
        {
            try
            {
                ProjectBoundary boundary = VerifyProjectBoundary();
                if (!File.Exists(boundary.PendingTestGatePath))
                {
                    return;
                }

                PendingTestGate pending = ReadPendingTestGate(boundary);
                if (!OwnsCurrentEditorProcess(pending))
                {
                    return;
                }

                if (string.Equals(
                        pending.phase,
                        CompletingPhase,
                        StringComparison.Ordinal))
                {
                    FinishPendingTestGate(
                        boundary,
                        pending,
                        pending.result);
                    return;
                }

                if (!string.Equals(
                        pending.phase,
                        RunningPhase,
                        StringComparison.Ordinal))
                {
                    FinishPendingTestGate(
                        boundary,
                        pending,
                        "failed:test-result-before-run-bind");
                    return;
                }

                bool containsExpectedAssembly;
                string treeFailure;
                int discovered;
                bool exactTree = ValidateResultTree(
                    result,
                    pending.assembly_name,
                    out containsExpectedAssembly,
                    out discovered,
                    out treeFailure);
                if (!containsExpectedAssembly)
                {
                    return;
                }

                string actualSourceSha256 = ComputeSha256(
                    boundary.DispatcherSourcePath);
                if (!string.Equals(
                        pending.source_sha256,
                        actualSourceSha256,
                        StringComparison.Ordinal))
                {
                    FinishPendingTestGate(
                        boundary,
                        pending,
                        "failed:dispatcher-source-changed-before-test-completion");
                    return;
                }

                if (!exactTree)
                {
                    FinishPendingTestGate(
                        boundary,
                        pending,
                        "failed:test-result-tree=" + treeFailure);
                    return;
                }

                bool passed =
                    result.PassCount > 0 &&
                    result.FailCount == 0 &&
                    result.InconclusiveCount == 0;
                string outcome = (passed ? "success:" : "failed:") +
                    "run=" + pending.run_id + ";discovered=" + discovered +
                    ";passed=" + result.PassCount +
                    ";failed=" + result.FailCount +
                    ";inconclusive=" + result.InconclusiveCount +
                    ";skipped=" + result.SkipCount;
                FinishPendingTestGate(boundary, pending, outcome);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "WP0002_TEST_RUN_CALLBACK_FAILED " +
                    NormalizeMessage(exception.Message));
            }
        }

        private static bool ValidateResultTree(
            ITestResult root,
            string expectedAssembly,
            out bool containsExpectedAssembly,
            out int discovered,
            out string failure)
        {
            containsExpectedAssembly = false;
            discovered = root?.Test?.TestCaseCount ?? 0;
            failure = "invalid-root";
            if (root == null || root.Test == null || discovered <= 0)
            {
                return false;
            }

            int leafCount = 0;
            var assemblies = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<ITestResult>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                ITestResult current = pending.Pop();
                if (current.Test is TestAssembly testAssembly)
                {
                    string name = testAssembly.Name;
                    assemblies.Add(
                        name.EndsWith(
                            ".dll",
                            StringComparison.OrdinalIgnoreCase)
                            ? name.Substring(0, name.Length - 4)
                            : name);
                }

                bool hasChildren = false;
                if (current.Children != null)
                {
                    foreach (ITestResult child in current.Children)
                    {
                        hasChildren = true;
                        pending.Push(child);
                    }
                }

                if (hasChildren)
                {
                    continue;
                }

                leafCount++;
            }

            containsExpectedAssembly = assemblies.Contains(expectedAssembly);
            int accounted = ResultCount(root);
            if (assemblies.Count != 1 || !containsExpectedAssembly)
            {
                failure = "assembly-mismatch";
                return false;
            }

            if (leafCount != discovered || accounted != discovered)
            {
                failure = "count-mismatch";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static int ResultCount(ITestResult result)
        {
            return result.PassCount + result.FailCount + result.SkipCount +
                   result.InconclusiveCount;
        }

        private static string FinishPendingTestGate(
            ProjectBoundary boundary,
            PendingTestGate pending,
            string outcome)
        {
            PendingTestGate current = ReadPendingTestGate(boundary);
            if (!SamePendingInvocation(current, pending))
            {
                throw new InvalidOperationException(
                    "WP0002_PENDING_TEST_GATE_INVOCATION_MISMATCH");
            }

            if (!string.Equals(
                    current.phase,
                    CompletingPhase,
                    StringComparison.Ordinal))
            {
                current.phase = CompletingPhase;
                current.result = outcome;
                current.completed_at = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture);
                ReplacePendingTestGate(boundary, current);
            }

            string line = CompleteGate(
                boundary,
                current.gate_id,
                current.source_sha256,
                "TestRunnerApi:" + current.assembly_name,
                current.result,
                current.started_at,
                current.invocation_id,
                current.completed_at);
            ReleaseTransient(current.invocation_id);
            File.Delete(boundary.PendingTestGatePath);
            return line;
        }

        private static void WatchPendingTestGate()
        {
            if (EditorApplication.timeSinceStartup < _nextWatchdogAt)
            {
                return;
            }

            _nextWatchdogAt = EditorApplication.timeSinceStartup + 1.0;
            try
            {
                ProjectBoundary boundary = VerifyProjectBoundary();
                if (!File.Exists(boundary.PendingTestGatePath))
                {
                    return;
                }

                PendingTestGate pending = ReadPendingTestGate(boundary);
                if (!OwnsCurrentEditorProcess(pending))
                {
                    return;
                }

                if (string.Equals(
                        pending.phase,
                        CompletingPhase,
                        StringComparison.Ordinal))
                {
                    FinishPendingTestGate(
                        boundary,
                        pending,
                        pending.result);
                    return;
                }

                DateTime deadline = DateTime.Parse(
                    pending.deadline_at,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
                if (DateTime.UtcNow >= deadline.ToUniversalTime())
                {
                    CancelPendingRun(pending);
                    FinishPendingTestGate(
                        boundary,
                        pending,
                        "failed:test-run-deadline-exceeded;phase=" +
                        pending.phase);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "WP0002_TEST_GATE_WATCHDOG_FAILED " +
                    NormalizeMessage(exception.Message));
            }
        }

        internal static void HandleTransientTestError(
            string invocationId,
            string message)
        {
            try
            {
                ProjectBoundary boundary = VerifyProjectBoundary();
                if (!File.Exists(boundary.PendingTestGatePath))
                {
                    return;
                }

                PendingTestGate pending = ReadPendingTestGate(boundary);
                if (!OwnsCurrentEditorProcess(pending) ||
                    !string.Equals(
                        pending.invocation_id,
                        invocationId,
                        StringComparison.Ordinal))
                {
                    return;
                }

                CancelPendingRun(pending);
                FinishPendingTestGate(
                    boundary,
                    pending,
                    "failed:test-run-error=" + NormalizeMessage(message));
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "WP0002_TRANSIENT_TEST_ERROR_CALLBACK_FAILED " +
                    NormalizeMessage(exception.Message));
            }
        }

        private static void CancelPendingRun(PendingTestGate pending)
        {
            if (!Guid.TryParse(pending.run_id, out _))
            {
                return;
            }

            try
            {
                TestRunnerApi.CancelTestRun(pending.run_id);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "WP0002_TEST_RUN_CANCEL_FAILED " +
                    NormalizeMessage(exception.Message));
            }
        }

        private static void RestoreTransientCallback()
        {
            try
            {
                ProjectBoundary boundary = VerifyProjectBoundary();
                if (!File.Exists(boundary.PendingTestGatePath))
                {
                    return;
                }

                PendingTestGate pending = ReadPendingTestGate(boundary);
                if (OwnsCurrentEditorProcess(pending) &&
                    !string.Equals(
                        pending.phase,
                        CompletingPhase,
                        StringComparison.Ordinal))
                {
                    RegisterTransient(pending.invocation_id);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "WP0002_TRANSIENT_CALLBACK_RESTORE_FAILED " +
                    NormalizeMessage(exception.Message));
            }
        }

        private static void RegisterTransient(string invocationId)
        {
            if (_activeTransientCallback != null &&
                string.Equals(
                    _activeTransientCallback.InvocationId,
                    invocationId,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (_activeTransientCallback != null)
            {
                ReleaseTransient(_activeTransientCallback.InvocationId);
            }

            var callbacks = new TransientTestErrorCallbacks(invocationId);
            TestRunnerApi.RegisterTestCallback(callbacks);
            _activeTransientCallback = callbacks;
        }

        private static void ReleaseTransient(string invocationId)
        {
            TransientTestErrorCallbacks? callbacks = _activeTransientCallback;
            if (callbacks == null ||
                !string.Equals(
                    callbacks.InvocationId,
                    invocationId,
                    StringComparison.Ordinal))
            {
                return;
            }

            _activeTransientCallback = null;
            try
            {
                TestRunnerApi.UnregisterTestCallback(callbacks);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "WP0002_TRANSIENT_CALLBACK_RELEASE_FAILED " +
                    NormalizeMessage(exception.Message));
            }
        }

        private static string ExpectedAssemblyForGate(string gateId)
        {
            if (string.Equals(gateId, EditModeGate, StringComparison.Ordinal))
            {
                return EditModeAssembly;
            }

            if (string.Equals(gateId, PlayModeGate, StringComparison.Ordinal))
            {
                return PlayModeAssembly;
            }

            throw new InvalidOperationException(
                "WP0002_PENDING_TEST_GATE_ID_REJECTED");
        }

        private static bool OwnsCurrentEditorProcess(PendingTestGate pending)
        {
            return pending.editor_process_id == CurrentEditorProcessId() &&
                   string.Equals(
                       pending.editor_process_started_at,
                       CurrentEditorProcessStartedAt(),
                       StringComparison.Ordinal);
        }

        private static int CurrentEditorProcessId()
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            return process.Id;
        }

        private static string CurrentEditorProcessStartedAt()
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            return process.StartTime.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture);
        }

        private static string NormalizeMessage(string? message)
        {
            string normalized = (message ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            return normalized.Length <= 512
                ? normalized
                : normalized.Substring(0, 512);
        }

        private sealed class TransientTestErrorCallbacks : IErrorCallbacks
        {
            internal TransientTestErrorCallbacks(string invocationId)
            {
                InvocationId = invocationId;
            }

            internal string InvocationId { get; }

            public void OnError(string message)
            {
                HandleTransientTestError(InvocationId, message);
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }

        private readonly struct ProjectBoundary
        {
            internal ProjectBoundary(
                string repositoryRoot,
                string dispatcherSourcePath,
                string evidenceDirectory,
                string pendingTestGatePath)
            {
                RepositoryRoot = repositoryRoot;
                DispatcherSourcePath = dispatcherSourcePath;
                EvidenceDirectory = evidenceDirectory;
                PendingTestGatePath = pendingTestGatePath;
            }

            internal string RepositoryRoot { get; }
            internal string DispatcherSourcePath { get; }
            internal string EvidenceDirectory { get; }
            internal string PendingTestGatePath { get; }
        }

        [Serializable]
        private sealed class PendingTestGate
        {
            public int schema_version;
            public string invocation_id = string.Empty;
            public string gate_id = string.Empty;
            public string source_sha256 = string.Empty;
            public string started_at = string.Empty;
            public string deadline_at = string.Empty;
            public string assembly_name = string.Empty;
            public string phase = string.Empty;
            public string run_id = string.Empty;
            public string result = string.Empty;
            public string completed_at = string.Empty;
            public int editor_process_id;
            public string editor_process_started_at = string.Empty;
        }

        [Serializable]
        private sealed class GateEvidence
        {
            public int schema_version;
            public string invocation_id = string.Empty;
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

    public sealed class WP0002TestRunCallback : ITestRunCallback
    {
        public void RunStarted(ITest testsToRun)
        {
        }

        public void RunFinished(ITestResult testResults)
        {
            WP0002GateDispatcher.HandleTestRunFinished(testResults);
        }

        public void TestStarted(ITest test)
        {
        }

        public void TestFinished(ITestResult result)
        {
        }
    }
}
