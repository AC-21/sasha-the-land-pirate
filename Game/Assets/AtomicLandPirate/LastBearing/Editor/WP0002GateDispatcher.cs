#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AtomicLandPirate.Presentation.LastBearing;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestRunner;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;

[assembly: TestRunCallback(
    typeof(AtomicLandPirate.Presentation.LastBearing.Editor.WP0002TestRunCallback))]

namespace AtomicLandPirate.Presentation.LastBearing.Editor
{
    /// <summary>
    /// The only authorized target for WP-0002 Unity_RunCommand. Calls bind to
    /// this file's bytes and one of seven enumerated gate IDs; no free-form
    /// editor operation, path, test filter, or capture target is accepted.
    /// </summary>
    [InitializeOnLoad]
    public static class WP0002GateDispatcher
    {
        public const string AssetRefreshGate = "asset-refresh-and-compile";
        public const string EditModeGate = "wp0002-editmode-test-assembly";
        public const string PlayModeGate = "wp0002-playmode-test-assembly";
        public const string TechnicalCaptureGate = "wp0002-technical-capture";
        public const string NativeBuildGate =
            "wp0002-native-il2cpp-arm64-build";
        public const string NativePerformanceStartGate =
            "wp0002-native-il2cpp-arm64-performance-start";
        public const string NativePerformanceCollectGate =
            "wp0002-native-il2cpp-arm64-performance-collect";

        private const string RuntimeAssembly = "AC21.Sasha.LastBearing.Runtime";
        private const string EditModeAssembly = "AC21.Sasha.LastBearing.EditModeTests";
        private const string PlayModeAssembly = "AC21.Sasha.LastBearing.PlayModeTests";
        private const string DispatcherAssetPath =
            "Assets/AtomicLandPirate/LastBearing/Editor/WP0002GateDispatcher.cs";
        private const string BoundaryRelativePath =
            "docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json";
        private const string NativeProfileAssetPath =
            "Assets/AtomicLandPirate/LastBearing/BuildProfiles/" +
            "WP0002NativeIl2CppArm64Performance.asset";
        private const string NativeSceneAssetPath =
            "Assets/AtomicLandPirate/LastBearing/Scenes/LastBearing.unity";
        private const string NativeOutputRelativePath =
            "BuildArtifacts/WP-0002/local-only/native-il2cpp-arm64";
        private const string NativePlayerName =
            "SashaAtomicLandPirateVGR13.app";
        private const string NativeExecutableRelativePath =
            "SashaAtomicLandPirateVGR13.app/Contents/MacOS/Game";
        private const string NativeBuildManifestName =
            "wp0002-native-performance-build-manifest.json";
        private const string NativeBuildIdentityName =
            "wp0002-native-performance-build-identity.json";
        private const string NativeRequestName =
            "wp0002-native-performance-request.json";
        private const string NativeRunManifestName =
            "wp0002-native-performance-run-manifest.json";
        private const string NativeLatestBuildName = "latest-build.json";
        private const string NativeLatestRunName = "latest-run.json";
        private const string NativeRuntimeFlag =
            "--wp0002-native-performance";
        private const string NativeDispatcherContract =
            "wp0002-gate-dispatcher-v3";
        private const string NativeBuildManifestContract =
            "WP0002_NATIVE_IL2CPP_ARM64_BUILD_MANIFEST_V1";
        private const string NativeBuildPointerContract =
            "WP0002_NATIVE_IL2CPP_ARM64_BUILD_POINTER_V1";
        private const string NativeRunManifestContract =
            "WP0002_NATIVE_PERFORMANCE_RUN_MANIFEST_V1";
        private const string NativeRunPointerContract =
            "WP0002_NATIVE_PERFORMANCE_RUN_POINTER_V1";
        private const string NativeRequestContract =
            "WP0002_NATIVE_PERFORMANCE_REQUEST_V1";
        private const string NativeBuildIdentityContract =
            "WP0002_NATIVE_PERFORMANCE_BUILD_IDENTITY_V1";
        private const string NativeRuntimeReportContract =
            "WP0002_VGR13_NATIVE_PERFORMANCE_REPORT_V1";
        private const string NativeAuthorizationReceiptId =
            "RR-WP0002-NATIVE-PLAYER-EXECUTABLE-PATH-CORRECTION-" +
            "20260720";
        private const string NativeAuthorizationClaim =
            "AUTHORIZE-WP0002-NATIVE-PLAYER-EXECUTABLE-PATH-" +
            "CORRECTION";
        private const string NativeAuthorizationSupersessionClaim =
            "SUPERSEDE-WP0002-GATE-DISPATCHER-V3-PLAYER-EXECUTABLE-" +
            "PATH-ONLY";
        private const string NativeAuthorizationReceiptRelativePath =
            "docs/foundation-v0.1/ledger/receipts/" +
            NativeAuthorizationReceiptId + ".json";
        private const string NativeGovernanceRecordRelativePath =
            "docs/foundation-v0.1/governance/" +
            "WP-0002-NATIVE-PLAYER-EXECUTABLE-PATH-CORRECTION-" +
            "20260720.md";
        private const string NativePreviousAuthorizationReceiptRelativePath =
            "docs/foundation-v0.1/ledger/receipts/" +
            "RR-WP0002-NATIVE-BOUNDARY-DUPLICATE-COUNT-CORRECTION-" +
            "20260720.json";
        private const string NativePreviousAuthorizationReceiptSha256 =
            "11bf6d2bd90881fdfcae427c3532694614533089bab16c0353860d4747958dff";
        private const string NativePreviousDispatcherSha256 =
            "aafa9b87455ff8658a82226e57b207be3a8907f7590ec163f144e52e2e50abd0";
        private const string PacketContractSha256 =
            "ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa";
        private const string NativeControlBaseCommit =
            "c5cfa463bf2b5ff9714be9483f67287f8180ec05";
        private const string GitExecutablePath = "/usr/bin/git";
        private const string RequiredGitBinarySha256 =
            "7f30f076d0e9c38f772a76449fca9da8cf97f6a3d43b94c90a00e4f9ce7ad39e";
        private const string RequiredUnityVersion = "6000.5.4f1";
        private const string RequiredUnityBinarySha256 =
            "5dcf81c5df5a9ff35006ee05832a1a6194c60fc4a386df652b9f49ea3a2a238b";
        private const string UnityEditorApplicationBundlePath =
            "/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app";
        private const string UnityEditorExecutableRelativePath =
            "Contents/MacOS/Unity";
        private const string RequiredXcodeVersion = "26.3";
        private const string RequiredXcodeBuildVersion = "17C529";
        private const string RequiredXcodeBuildBinarySha256 =
            "8910e7a24ef01bb0c2d4e66c07b14c321cd150a8017ca17cfa335bd888182ec1";
        private const string RequiredMacOsSdkVersion = "26.2";
        private const string XcodeDeveloperDirectory =
            "/Applications/Xcode.app/Contents/Developer";
        private const string XcodeBuildPath =
            "/Applications/Xcode.app/Contents/Developer/usr/bin/xcodebuild";
        private const string XcrunPath = "/usr/bin/xcrun";
        private const int NativeScreenWidth = 2560;
        private const int NativeScreenHeight = 1600;
        private const int NativeWarmupSeconds = 300;
        private const int NativePausedSeconds = 300;
        private const int NativeUnpausedSeconds = 300;
        private const int NativeCityGarageCycles = 100;
        private const string StartingPhase = "starting";
        private const string RunningPhase = "running";
        private const string CompletingPhase = "completing";
        private const int TestGateTimeoutMinutes = 10;
        private const int FixedChildProcessTimeoutMilliseconds = 15000;
        private const int FixedChildCaptureTimeoutMilliseconds = 5000;
        private const int NativeProcessTerminationTimeoutMilliseconds = 5000;

        private static readonly HashSet<string> AllowedGateIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                AssetRefreshGate,
                EditModeGate,
                PlayModeGate,
                TechnicalCaptureGate,
                NativeBuildGate,
                NativePerformanceStartGate,
                NativePerformanceCollectGate
            };

        private static TransientTestErrorCallbacks? _activeTransientCallback;
        private static double _nextWatchdogAt;
        private static readonly string NativeEditorSessionNonce =
            Guid.NewGuid().ToString("N");
        private static readonly int NativeEditorProcessId =
            Process.GetCurrentProcess().Id;
        private static readonly string NativeEditorProcessStartedAt =
            Process.GetCurrentProcess().StartTime.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture);
        private static TrustedNativeBuild? _trustedNativeBuild;
        private static TrustedNativeRun? _trustedNativeRun;
        private static readonly List<NativeProcessCleanup>
            _quarantinedNativeProcesses = new List<NativeProcessCleanup>();
        private static bool _nativePlayerReloadLockHeld;
        private static string _nativePlayerCleanupFailure = string.Empty;
        private static ScheduledNativeBuild? _scheduledNativeBuild;
        private static NativeBuildScheduleState _nativeBuildScheduleState;
        private static string _nativeBuildScheduleFailure = string.Empty;

        private static readonly string[] NativeControlStage1Paths =
        {
            "Game/Assets/AtomicLandPirate/LastBearing/Editor/WP0002GateDispatcher.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/" +
                "LastBearingAdapterTests.cs",
            "Tests/AtomicLandPirate.CoreTests/LastBearing/GameSourceContract.cs",
            "docs/foundation-v0.1/governance/" +
                "WP-0002-NATIVE-PLAYER-EXECUTABLE-PATH-CORRECTION-" +
                "20260720.md",
            BoundaryRelativePath,
            "docs/foundation-v0.1/schemas/local-a1-boundary.schema.json",
            "docs/foundation-v0.1/tools/validate_foundation.py",
            "docs/foundation-v0.1/tools/" +
            "test_validate_wp0002_native_editor_path_correction.py"
        };

        static WP0002GateDispatcher()
        {
            RestoreTransientCallback();
            EditorApplication.update += WatchPendingTestGate;
            AssemblyReloadEvents.beforeAssemblyReload +=
                InvalidateTrustedNativeState;
            EditorApplication.wantsToQuit +=
                AllowEditorQuitAfterNativeCleanup;
            EditorApplication.quitting += InvalidateTrustedNativeState;
        }

        public static string Dispatch(string gateId, string expectedSourceSha256)
        {
            string startedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            if (!AllowedGateIds.Contains(gateId))
            {
                throw new InvalidOperationException("WP0002_GATE_ID_REJECTED");
            }

            RejectNativeGateWhileCleanupQuarantined(gateId);

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
                case NativeBuildGate:
                    return RunNativeBuild(
                        boundary,
                        actualSourceSha256,
                        startedAt);
                case NativePerformanceStartGate:
                    return StartNativePerformance(
                        boundary,
                        actualSourceSha256,
                        startedAt);
                case NativePerformanceCollectGate:
                    return CollectNativePerformance(
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

            string result = "failed:runtime-capture-contract";
            if (valid)
            {
                try
                {
                    string artifact = WriteTechnicalCapture(
                        boundary,
                        controllers[0].World!.MainCamera!,
                        sourceSha256);
                    result = "success:capture-ready;artifact=" + artifact;
                }
                catch (Exception exception)
                {
                    result = "failed:technical-capture-write=" +
                             NormalizeMessage(exception.Message);
                }
            }

            return CompleteGate(
                boundary,
                TechnicalCaptureGate,
                sourceSha256,
                "validate-runtime-root-camera-controller",
                result,
                startedAt);
        }

        private static string WriteTechnicalCapture(
            ProjectBoundary boundary,
            Camera camera,
            string sourceSha256)
        {
            const int width = 1920;
            const int height = 1080;
            string fileName = "last-bearing-" + sourceSha256.Substring(0, 12) +
                              "-" + Guid.NewGuid().ToString("N") + ".png";
            string relativePath = Path.Combine(
                    "BuildArtifacts",
                    "WP-0002",
                    "technical-captures",
                    fileName)
                .Replace(Path.DirectorySeparatorChar, '/');
            string absolutePath = Path.Combine(
                boundary.RepositoryRoot,
                relativePath);
            string? directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException(
                    "WP0002_TECHNICAL_CAPTURE_PATH_INVALID");
            }

            Directory.CreateDirectory(directory);
            RenderTexture? previousTarget = camera.targetTexture;
            RenderTexture? previousActive = RenderTexture.active;
            var target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32);
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGB24,
                false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply(false, false);
                byte[] payload = ImageConversion.EncodeToPNG(texture);
                if (payload == null || payload.Length == 0)
                {
                    throw new InvalidOperationException(
                        "WP0002_TECHNICAL_CAPTURE_EMPTY");
                }

                WriteImmutableFile(absolutePath, payload);
                return relativePath;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (target.IsCreated())
                {
                    target.Release();
                }

                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static string RunNativeBuild(
            ProjectBoundary boundary,
            string sourceSha256,
            string startedAt)
        {
            try
            {
                if (_scheduledNativeBuild != null ||
                    _nativeBuildScheduleState == NativeBuildScheduleState.Pending ||
                    _nativeBuildScheduleState == NativeBuildScheduleState.Running)
                {
                    throw new InvalidOperationException(
                        "native-build-already-scheduled");
                }

                if (_trustedNativeRun != null)
                {
                    throw new InvalidOperationException(
                        "native-performance-run-already-attested");
                }

                _trustedNativeBuild = null;
                NativeSourceIdentity source = VerifyNativeGateContract(
                    boundary,
                    sourceSha256);
                VerifyNativeBuildPrerequisites();
                _nativeBuildScheduleFailure = string.Empty;
                _nativeBuildScheduleState = NativeBuildScheduleState.Pending;
                _scheduledNativeBuild = new ScheduledNativeBuild(
                    boundary,
                    source,
                    startedAt);
                EditorApplication.delayCall += ExecuteScheduledNativeBuild;
                return CompleteNativeGate(
                    boundary,
                    NativeBuildGate,
                    source.DispatcherSha256,
                    "EditorApplication.delayCall(fixed-source-controlled-profile)",
                    "pending:build-scheduled",
                    startedAt);
            }
            catch (Exception exception)
            {
                EditorApplication.delayCall -= ExecuteScheduledNativeBuild;
                _scheduledNativeBuild = null;
                _nativeBuildScheduleState = NativeBuildScheduleState.Failed;
                _nativeBuildScheduleFailure = NormalizeMessage(exception.Message);
                return CompleteNativeGate(
                    boundary,
                    NativeBuildGate,
                    sourceSha256,
                    "BuildPipeline.BuildPlayer(fixed-source-controlled-profile)",
                    "failed:" + NormalizeMessage(exception.Message),
                    startedAt);
            }
        }

        private static void ExecuteScheduledNativeBuild()
        {
            ScheduledNativeBuild? scheduled = _scheduledNativeBuild;
            _scheduledNativeBuild = null;
            if (scheduled == null ||
                _nativeBuildScheduleState != NativeBuildScheduleState.Pending)
            {
                return;
            }

            _nativeBuildScheduleState = NativeBuildScheduleState.Running;
            try
            {
                ExecuteNativeBuild(
                    scheduled.Boundary,
                    scheduled.Source,
                    scheduled.StartedAt);
                _nativeBuildScheduleState = NativeBuildScheduleState.Succeeded;
            }
            catch (Exception exception)
            {
                _trustedNativeBuild = null;
                _nativeBuildScheduleFailure = NormalizeMessage(exception.Message);
                _nativeBuildScheduleState = NativeBuildScheduleState.Failed;
                CompleteNativeGate(
                    scheduled.Boundary,
                    NativeBuildGate,
                    scheduled.Source.DispatcherSha256,
                    "BuildPipeline.BuildPlayer(fixed-source-controlled-profile)",
                    "failed:" + _nativeBuildScheduleFailure,
                    scheduled.StartedAt);
            }
        }

        private static string ExecuteNativeBuild(
            ProjectBoundary boundary,
            NativeSourceIdentity source,
            string startedAt)
        {
            EnsureNativeOutputRoot(boundary);
            string invocationId = Guid.NewGuid().ToString("N");
            string stagingRoot = ResolveNativeOutputPath(
                boundary,
                ".staging-" + invocationId);
            if (Directory.Exists(stagingRoot) || File.Exists(stagingRoot))
            {
                throw new InvalidOperationException(
                    "native-build-staging-conflict");
            }

            Directory.CreateDirectory(stagingRoot);
            RequireRegularDirectory(
                stagingRoot,
                "native-build-staging-invalid");
            bool stagingMoved = false;
            try
            {
                string stagingPlayer = Path.Combine(
                    stagingRoot,
                    NativePlayerName);
                BuildProfile? profile =
                    AssetDatabase.LoadAssetAtPath<BuildProfile>(
                        NativeProfileAssetPath);
                if (profile == null)
                {
                    throw new InvalidOperationException(
                        "native-build-profile-unloadable");
                }

                string? previousDeveloperDirectory =
                    Environment.GetEnvironmentVariable("DEVELOPER_DIR");
                BuildReport report;
                try
                {
                    Environment.SetEnvironmentVariable(
                        "DEVELOPER_DIR",
                        XcodeDeveloperDirectory);
                    report = BuildPipeline.BuildPlayer(
                        new BuildPlayerWithProfileOptions
                        {
                            buildProfile = profile,
                            locationPathName = stagingPlayer,
                            options = BuildOptions.CleanBuildCache |
                                      BuildOptions.Development |
                                      BuildOptions.StrictMode
                        });
                }
                finally
                {
                    Environment.SetEnvironmentVariable(
                        "DEVELOPER_DIR",
                        previousDeveloperDirectory);
                }

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "native-build-result-" +
                        report.summary.result.ToString().ToLowerInvariant());
                }
                if ((report.summary.options & BuildOptions.Development) == 0)
                {
                    throw new InvalidOperationException(
                        "native-build-is-not-development");
                }

                string buildGuid = NormalizeBuildGuid(
                    report.summary.guid.ToString());
                string runDirectoryName =
                    source.SourceTreeSha256.Substring(0, 12) + "-" +
                    buildGuid;
                ValidateNativeRunDirectoryName(runDirectoryName);
                string runDirectory = ResolveNativeOutputPath(
                    boundary,
                    "runs/" + runDirectoryName);
                if (Directory.Exists(runDirectory) || File.Exists(runDirectory))
                {
                    throw new InvalidOperationException(
                        "native-build-guid-already-exists");
                }

                string runsDirectory = ResolveNativeOutputPath(
                    boundary,
                    "runs");
                Directory.CreateDirectory(runsDirectory);
                RequireRegularDirectory(
                    runsDirectory,
                    "native-runs-directory-invalid");
                Directory.Move(stagingRoot, runDirectory);
                stagingMoved = true;

                string playerPath = Path.Combine(runDirectory, NativePlayerName);
                string executablePath = Path.Combine(
                    runDirectory,
                    NativeExecutableRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar));
                string gameAssemblyPath = Path.Combine(
                    playerPath,
                    "Contents",
                    "Frameworks",
                    "GameAssembly.dylib");
                RequireRegularFile(executablePath, "native-executable-missing");
                RequireRegularFile(
                    gameAssemblyPath,
                    "native-il2cpp-gameassembly-missing");
                if (!IsMachOArm64Only(executablePath) ||
                    !IsMachOArm64Only(gameAssemblyPath))
                {
                    throw new InvalidOperationException(
                        "native-build-is-not-arm64-only");
                }

                string postBuildSourceTree = ComputeNativeSourceTreeSha256(
                    boundary.RepositoryRoot);
                if (!string.Equals(
                        postBuildSourceTree,
                        source.SourceTreeSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "native-source-changed-during-build");
                }

                var manifest = new NativeBuildManifest
                {
                    schema_version = 1,
                    contract_id = NativeBuildManifestContract,
                    build_guid = buildGuid,
                    source_commit = source.SourceCommit,
                    source_tree_sha256 = source.SourceTreeSha256,
                    dispatcher_sha256 = source.DispatcherSha256,
                    build_profile_sha256 = source.BuildProfileSha256,
                    scene_sha256 = source.SceneSha256,
                    unity_version = RequiredUnityVersion,
                    unity_binary_sha256 = RequiredUnityBinarySha256,
                    xcode_version = RequiredXcodeVersion,
                    xcode_build_version = RequiredXcodeBuildVersion,
                    xcodebuild_binary_sha256 =
                        RequiredXcodeBuildBinarySha256,
                    macos_sdk_version = RequiredMacOsSdkVersion,
                    build_target = "StandaloneOSX",
                    architecture = "arm64",
                    scripting_backend = "IL2CPP",
                    development_build = true,
                    width = NativeScreenWidth,
                    height = NativeScreenHeight,
                    run_directory = "runs/" + runDirectoryName,
                    player_relative_path = NativePlayerName,
                    executable_relative_path = NativeExecutableRelativePath,
                    executable_sha256 = ComputeSha256(executablePath),
                    build_artifact_sha256 = ComputeDirectoryTreeSha256(playerPath),
                    total_size_bytes = checked((long)report.summary.totalSize),
                    built_at = DateTime.UtcNow.ToString(
                        "O",
                        CultureInfo.InvariantCulture)
                };
                string manifestPath = Path.Combine(
                    runDirectory,
                    NativeBuildManifestName);
                byte[] manifestPayload = SerializeJson(manifest);
                WriteImmutableFile(manifestPath, manifestPayload);
                string manifestSha256 = ComputeSha256(manifestPayload);

                var pointer = new NativeBuildPointer
                {
                    schema_version = 1,
                    contract_id = NativeBuildPointerContract,
                    build_guid = buildGuid,
                    run_directory = manifest.run_directory,
                    build_manifest_relative_path =
                        manifest.run_directory + "/" +
                        NativeBuildManifestName,
                    build_manifest_sha256 = manifestSha256
                };
                WriteReplaceFile(
                    Path.Combine(
                        boundary.NativeOutputRoot,
                        NativeLatestBuildName),
                    SerializeJson(pointer));

                string completion = CompleteNativeGate(
                    boundary,
                    NativeBuildGate,
                    source.DispatcherSha256,
                    "BuildPipeline.BuildPlayer(fixed-source-controlled-profile)",
                    "success:build-manifest=" +
                    pointer.build_manifest_relative_path +
                    ";build_manifest_sha256=" + manifestSha256,
                    startedAt);
                _trustedNativeBuild = new TrustedNativeBuild(
                    NativeEditorSessionNonce,
                    NativeEditorProcessId,
                    NativeEditorProcessStartedAt,
                    manifest.build_guid,
                    manifest.source_commit,
                    manifest.source_tree_sha256,
                    manifest.dispatcher_sha256,
                    manifest.build_profile_sha256,
                    manifest.scene_sha256,
                    manifest.run_directory,
                    Path.GetFullPath(executablePath),
                    manifest.executable_sha256,
                    manifest.build_artifact_sha256,
                    manifestSha256);
                return completion;
            }
            finally
            {
                if (!stagingMoved && Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, true);
                }
            }
        }

        private static string StartNativePerformance(
            ProjectBoundary boundary,
            string sourceSha256,
            string startedAt)
        {
            Process? launchedProcess = null;
            try
            {
                NativeSourceIdentity source = VerifyNativeGateContract(
                    boundary,
                    sourceSha256);
                if (_nativeBuildScheduleState == NativeBuildScheduleState.Pending ||
                    _nativeBuildScheduleState == NativeBuildScheduleState.Running)
                {
                    throw new InvalidOperationException("native-build-pending");
                }
                if (_nativeBuildScheduleState == NativeBuildScheduleState.Failed)
                {
                    throw new InvalidOperationException(
                        "native-build-failed:" + _nativeBuildScheduleFailure);
                }
                if (_trustedNativeRun != null)
                {
                    throw new InvalidOperationException(
                        "native-performance-run-already-attested");
                }

                TrustedNativeBuild trustedBuild = RequireTrustedNativeBuild(
                    source);
                EnsureNativeOutputRoot(boundary);
                NativeBuildPointer pointer = ReadJsonFile<NativeBuildPointer>(
                    ResolveNativeOutputPath(
                        boundary,
                        NativeLatestBuildName));
                NativeBuildManifest manifest = VerifyNativeBuildPointer(
                    boundary,
                    source,
                    pointer);
                VerifyTrustedNativeBuild(trustedBuild, pointer, manifest);
                string runDirectory = ResolveNativeOutputPath(
                    boundary,
                    manifest.run_directory);

                string identityPath = ResolveNativeOutputPath(
                    boundary,
                    manifest.run_directory + "/" + NativeBuildIdentityName);
                string requestPath = ResolveNativeOutputPath(
                    boundary,
                    manifest.run_directory + "/" + NativeRequestName);
                string runManifestPath = ResolveNativeOutputPath(
                    boundary,
                    manifest.run_directory + "/" + NativeRunManifestName);
                if (File.Exists(identityPath) ||
                    File.Exists(requestPath) ||
                    File.Exists(runManifestPath))
                {
                    throw new InvalidOperationException(
                        "native-performance-run-already-started");
                }

                var identity = new NativeBuildIdentity
                {
                    schema_version = 1,
                    identity_id = NativeBuildIdentityContract,
                    source_commit = manifest.source_commit,
                    source_tree_sha256 = manifest.source_tree_sha256,
                    build_guid = manifest.build_guid,
                    unity_version = manifest.unity_version,
                    executable_sha256 = manifest.executable_sha256,
                    development_build = manifest.development_build
                };
                byte[] identityPayload = SerializeJson(identity);
                WriteImmutableFile(identityPath, identityPayload);
                string identitySha256 = ComputeSha256(identityPayload);

                string requestNonce = Guid.NewGuid().ToString("N");
                var request = new NativePerformanceRequest
                {
                    schema_version = 1,
                    contract_id = NativeRequestContract,
                    request_nonce = requestNonce,
                    expected_source_commit = manifest.source_commit,
                    expected_source_tree_sha256 = manifest.source_tree_sha256,
                    expected_build_identity_sha256 = identitySha256,
                    expected_build_guid = manifest.build_guid,
                    expected_executable_sha256 = manifest.executable_sha256
                };
                byte[] requestPayload = SerializeJson(request);
                WriteImmutableFile(requestPath, requestPayload);
                string requestSha256 = ComputeSha256(requestPayload);

                string reportName = "wp0002-native-performance-" +
                                    requestNonce + ".report.json";
                string reportPath = ResolveNativeOutputPath(
                    boundary,
                    manifest.run_directory + "/" + reportName);
                if (File.Exists(reportPath))
                {
                    throw new InvalidOperationException(
                        "native-performance-report-preexists");
                }

                string executablePath = ResolveNativeOutputPath(
                    boundary,
                    manifest.run_directory + "/" +
                    manifest.executable_relative_path);
                RequireRegularFile(executablePath, "native-executable-missing");
                if (!string.Equals(
                        Path.GetFullPath(executablePath),
                        trustedBuild.ExecutablePath,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "native-trusted-build-executable-path-mismatch");
                }

                string latestRunPath = ResolveNativeOutputPath(
                    boundary,
                    NativeLatestRunName);
                RequirePathComponentsNotReparse(
                    latestRunPath,
                    true,
                    "native-run-pointer-path-invalid");
                if (File.Exists(latestRunPath))
                {
                    RequireRegularFile(
                        latestRunPath,
                        "native-run-pointer-path-invalid");
                }

                var processInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = NativeRuntimeFlag,
                    WorkingDirectory = runDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                _trustedNativeBuild = null;
                HoldNativePlayerReloadLock();
                launchedProcess = Process.Start(processInfo);
                if (launchedProcess == null)
                {
                    throw new InvalidOperationException(
                        "native-performance-process-start-failed");
                }

                string processStartedAt = launchedProcess.StartTime
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture);
                var runManifest = new NativeRunManifest
                {
                    schema_version = 1,
                    contract_id = NativeRunManifestContract,
                    request_nonce = requestNonce,
                    source_commit = manifest.source_commit,
                    source_tree_sha256 = manifest.source_tree_sha256,
                    dispatcher_sha256 = manifest.dispatcher_sha256,
                    build_profile_sha256 = manifest.build_profile_sha256,
                    build_guid = manifest.build_guid,
                    build_artifact_sha256 = manifest.build_artifact_sha256,
                    executable_sha256 = manifest.executable_sha256,
                    build_manifest_sha256 = pointer.build_manifest_sha256,
                    build_identity_sha256 = identitySha256,
                    request_sha256 = requestSha256,
                    run_directory = manifest.run_directory,
                    report_relative_path =
                        manifest.run_directory + "/" + reportName,
                    process_id = launchedProcess.Id,
                    process_started_at = processStartedAt,
                    started_at = DateTime.UtcNow.ToString(
                        "O",
                        CultureInfo.InvariantCulture)
                };
                byte[] runPayload = SerializeJson(runManifest);
                WriteImmutableFile(runManifestPath, runPayload);
                string runManifestSha256 = ComputeSha256(runPayload);
                var runPointer = new NativeRunPointer
                {
                    schema_version = 1,
                    contract_id = NativeRunPointerContract,
                    request_nonce = requestNonce,
                    run_manifest_relative_path =
                        manifest.run_directory + "/" + NativeRunManifestName,
                    run_manifest_sha256 = runManifestSha256
                };
                WriteReplaceFile(latestRunPath, SerializeJson(runPointer));

                string completion = CompleteNativeGate(
                    boundary,
                    NativePerformanceStartGate,
                    sourceSha256,
                    "Process.Start(fixed-manifest-executable;UseShellExecute=false)",
                    "success:request_nonce=" + requestNonce +
                    ";run_manifest_sha256=" + runManifestSha256,
                    startedAt);
                _trustedNativeRun = new TrustedNativeRun(
                    NativeEditorSessionNonce,
                    NativeEditorProcessId,
                    NativeEditorProcessStartedAt,
                    launchedProcess,
                    requestNonce,
                    processStartedAt,
                    manifest.run_directory,
                    runManifest.report_relative_path,
                    pointer.build_manifest_sha256,
                    runManifestSha256,
                    identitySha256,
                    requestSha256,
                    manifest.executable_sha256,
                    manifest.build_artifact_sha256);
                launchedProcess = null;
                return completion;
            }
            catch (Exception exception)
            {
                _trustedNativeBuild = null;
                string failure = NormalizeMessage(exception.Message);
                if (launchedProcess != null)
                {
                    string cleanupFailure;
                    if (TerminateProcess(
                            launchedProcess,
                            out cleanupFailure))
                    {
                        launchedProcess.Dispose();
                    }
                    else
                    {
                        QuarantineNativeProcess(
                            launchedProcess,
                            "native-start-failure:" + cleanupFailure);
                        failure += ";native-player-cleanup-quarantined:" +
                                   cleanupFailure;
                    }

                    launchedProcess = null;
                }

                ReleaseNativePlayerReloadLockIfSafe();

                return CompleteNativeGate(
                    boundary,
                    NativePerformanceStartGate,
                    sourceSha256,
                    "Process.Start(fixed-manifest-executable;UseShellExecute=false)",
                    "failed:" + failure,
                    startedAt);
            }
        }

        private static string CollectNativePerformance(
            ProjectBoundary boundary,
            string sourceSha256,
            string startedAt)
        {
            TrustedNativeRun? trustedRun = null;
            try
            {
                NativeSourceIdentity source = VerifyNativeGateContract(
                    boundary,
                    sourceSha256);
                trustedRun = RequireTrustedNativeRun();
                EnsureNativeOutputRoot(boundary);
                NativeRunPointer runPointer = ReadJsonFile<NativeRunPointer>(
                    ResolveNativeOutputPath(boundary, NativeLatestRunName));
                NativeRunManifest runManifest = VerifyNativeRunPointer(
                    boundary,
                    source,
                    runPointer);
                VerifyTrustedNativeRun(trustedRun, runPointer, runManifest);
                RequireNativeProcessExited(trustedRun, runManifest);

                string reportPath = ResolveNativeOutputPath(
                    boundary,
                    runManifest.report_relative_path);
                RequireRegularFile(
                    reportPath,
                    "native-performance-report-missing");
                byte[] reportPayload = File.ReadAllBytes(reportPath);
                string reportText = Encoding.UTF8.GetString(reportPayload);
                if (reportText.Contains(
                        "\"representative_v0\"",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "native-performance-report-claims-v0-phase");
                }

                NativePerformanceReport report =
                    ReadJson<NativePerformanceReport>(reportPayload);
                ValidateNativePerformanceReport(runManifest, report);
                string reportSha256 = ComputeSha256(reportPayload);
                string evidenceRelativePath = runManifest.run_directory +
                    "/wp0002-native-performance-evidence.json";
                var evidence = new NativePerformanceEvidence
                {
                    schema_version = 1,
                    contract_id =
                        "WP0002_VGR13_NATIVE_PERFORMANCE_EVIDENCE_V1",
                    request_nonce = runManifest.request_nonce,
                    source_commit = runManifest.source_commit,
                    source_tree_sha256 = runManifest.source_tree_sha256,
                    dispatcher_sha256 = runManifest.dispatcher_sha256,
                    build_profile_sha256 =
                        runManifest.build_profile_sha256,
                    build_guid = runManifest.build_guid,
                    build_artifact_sha256 =
                        runManifest.build_artifact_sha256,
                    executable_sha256 = runManifest.executable_sha256,
                    build_manifest_sha256 =
                        runManifest.build_manifest_sha256,
                    build_identity_sha256 =
                        runManifest.build_identity_sha256,
                    request_sha256 = runManifest.request_sha256,
                    run_manifest_sha256 =
                        runPointer.run_manifest_sha256,
                    runtime_report_sha256 = reportSha256,
                    runtime_report_relative_path =
                        runManifest.report_relative_path,
                    width = NativeScreenWidth,
                    height = NativeScreenHeight,
                    warmup_seconds = report.actual_warmup_seconds,
                    paused_seconds = report.paused.measured_seconds,
                    representative_unpaused_seconds =
                        report.representative_unpaused.measured_seconds,
                    city_garage_cycles = report.cycles.completed_cycles,
                    accepted = true,
                    collected_at = DateTime.UtcNow.ToString(
                        "O",
                        CultureInfo.InvariantCulture)
                };
                byte[] evidencePayload = SerializeJson(evidence);
                string evidencePath = ResolveNativeOutputPath(
                    boundary,
                    evidenceRelativePath);
                WriteImmutableFile(evidencePath, evidencePayload);
                string evidenceSha256 = ComputeSha256(evidencePayload);

                string completion = CompleteNativeGate(
                    boundary,
                    NativePerformanceCollectGate,
                    sourceSha256,
                    "validate-fixed-vgr13-native-performance-report",
                    "success:evidence=" + evidenceRelativePath +
                    ";evidence_sha256=" + evidenceSha256,
                    startedAt);
                _trustedNativeRun = null;
                trustedRun.Process.Dispose();
                ReleaseNativePlayerReloadLockIfSafe();
                return completion;
            }
            catch (Exception exception)
            {
                if (!string.Equals(
                        exception.Message,
                        "native-performance-process-still-running",
                        StringComparison.Ordinal))
                {
                    InvalidateTrustedNativeRun();
                }

                return CompleteNativeGate(
                    boundary,
                    NativePerformanceCollectGate,
                    sourceSha256,
                    "validate-fixed-vgr13-native-performance-report",
                    "failed:" + NormalizeMessage(exception.Message),
                    startedAt);
            }
        }

        private static NativeSourceIdentity VerifyNativeGateContract(
            ProjectBoundary boundary,
            string sourceSha256)
        {
            if (!string.Equals(
                    Application.unityVersion,
                    RequiredUnityVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "unity-version-mismatch");
            }

            string unityApplicationBundle = Path.GetFullPath(
                UnityEditorApplicationBundlePath);
            if (!IsExpectedUnityEditorApplicationPath(
                    EditorApplication.applicationPath))
            {
                throw new InvalidOperationException(
                    "unity-editor-application-bundle-path-mismatch");
            }
            RequireRegularDirectory(
                unityApplicationBundle,
                "unity-editor-application-bundle-missing");
            string unityExecutable = Path.GetFullPath(
                Path.Combine(
                    unityApplicationBundle,
                    UnityEditorExecutableRelativePath));
            RequireRegularFile(
                unityExecutable,
                "unity-editor-executable-missing");
            if (!string.Equals(
                    ComputeSha256(unityExecutable),
                    RequiredUnityBinarySha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "unity-editor-binary-hash-mismatch");
            }

            string profilePath = Path.Combine(
                Path.GetDirectoryName(boundary.DispatcherSourcePath)!,
                "..",
                "BuildProfiles",
                "WP0002NativeIl2CppArm64Performance.asset");
            profilePath = Path.GetFullPath(profilePath);
            RequireRegularFile(profilePath, "native-build-profile-missing");
            string profileText = File.ReadAllText(profilePath, Encoding.UTF8);
            ValidateExactNativeProfileText(profileText);
            string profileSha256 = ComputeSha256(profilePath);

            string scenePath = Path.GetFullPath(
                Path.Combine(
                    boundary.RepositoryRoot,
                    "Game",
                    NativeSceneAssetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            RequireRegularFile(scenePath, "native-build-scene-missing");
            string sceneSha256 = ComputeSha256(scenePath);
            string boundaryText = File.ReadAllText(
                boundary.BoundaryPath,
                Encoding.UTF8);
            RequireBoundaryBinding(
                boundaryText,
                "\"contract\": \"" + NativeDispatcherContract + "\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"build_profile_sha256\": \"" +
                profileSha256 + "\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"required_editor_version\": \"" + RequiredUnityVersion + "\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"editor_binary_sha256\": \"" +
                RequiredUnityBinarySha256 + "\"",
                expectedOccurrences: 2);
            RequireBoundaryBinding(
                boundaryText,
                "\"xcodebuild_binary_sha256\": \"" +
                RequiredXcodeBuildBinarySha256 + "\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"authorization_receipt_id\": \"" +
                NativeAuthorizationReceiptId + "\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"path_correction_state\": " +
                "\"receipt-required-fail-closed\"");

            string sourceCommit = ResolveGitHead(boundary.RepositoryRoot);
            VerifyNativeAuthorizationReceipt(
                boundary,
                profileSha256,
                boundaryText,
                sourceCommit);

            return new NativeSourceIdentity(
                sourceSha256,
                profileSha256,
                sceneSha256,
                sourceCommit,
                ComputeNativeSourceTreeSha256(boundary.RepositoryRoot));
        }

        private static bool IsExpectedUnityEditorApplicationPath(
            string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(candidate),
                    Path.GetFullPath(UnityEditorApplicationBundlePath),
                    StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RequireBoundaryBinding(
            string boundaryText,
            string exactToken,
            int expectedOccurrences = 1)
        {
            if (string.IsNullOrEmpty(exactToken) || expectedOccurrences < 1)
            {
                throw new InvalidOperationException(
                    "native-boundary-binding-missing-or-ambiguous");
            }

            int actualOccurrences = 0;
            int offset = 0;
            while (offset <= boundaryText.Length - exactToken.Length)
            {
                int match = boundaryText.IndexOf(
                    exactToken,
                    offset,
                    StringComparison.Ordinal);
                if (match < 0)
                {
                    break;
                }

                actualOccurrences++;
                if (actualOccurrences > expectedOccurrences)
                {
                    throw new InvalidOperationException(
                        "native-boundary-binding-missing-or-ambiguous");
                }

                offset = match + exactToken.Length;
            }

            if (actualOccurrences != expectedOccurrences)
            {
                throw new InvalidOperationException(
                    "native-boundary-binding-missing-or-ambiguous");
            }
        }

        private static void ValidateExactNativeProfileText(string profileText)
        {
            foreach (string token in new[]
            {
                "m_Name: WP0002 Native IL2CPP ARM64 Performance",
                "m_BuildTarget: 2",
                "m_Subtarget: 2",
                "m_PlatformId: 0d2129357eac403d8b359c2dcbf82502",
                "m_OverrideGlobalSceneList: 1",
                "m_path: " + NativeSceneAssetPath,
                "|   defaultScreenWidth: 2560",
                "|   defaultScreenHeight: 1600",
                "|   defaultIsNativeResolution: 0",
                "|   fullscreenMode: 3",
                "|     Standalone: 1",
                "m_Development: 1",
                "m_ConnectProfiler: 0",
                "m_BuildWithDeepProfilingSupport: 0",
                "m_AllowDebugging: 0",
                "m_Architecture: 1",
                "m_CreateXcodeProject: 0"
            })
            {
                if (profileText.IndexOf(token, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        "native-build-profile-contract-mismatch");
                }
            }

            if (CountOccurrences(profileText, "    m_path: ") != 1 ||
                CountOccurrences(profileText, "|     Standalone: 1") != 1)
            {
                throw new InvalidOperationException(
                    "native-build-profile-contract-ambiguous");
            }
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int offset = 0;
            while (offset < source.Length)
            {
                int next = source.IndexOf(
                    token,
                    offset,
                    StringComparison.Ordinal);
                if (next < 0)
                {
                    break;
                }

                count++;
                offset = next + token.Length;
            }

            return count;
        }

        private static void VerifyNativeBuildPrerequisites()
        {
            string contentsPath = Path.GetFullPath(
                EditorApplication.applicationContentsPath);
            string il2CppVariation = Path.GetFullPath(
                Path.Combine(
                    contentsPath,
                    "..",
                    "..",
                    "PlaybackEngines",
                    "MacStandaloneSupport",
                    "Variations",
                    "macos_arm64_player_development_il2cpp"));
            if (!Directory.Exists(il2CppVariation))
            {
                throw new InvalidOperationException(
                    "mac-il2cpp-arm64-module-missing");
            }

            if (!Directory.Exists(XcodeDeveloperDirectory) ||
                !File.Exists(XcodeBuildPath))
            {
                throw new InvalidOperationException("xcode-26.3-missing");
            }

            RequireRegularFile(XcodeBuildPath, "xcodebuild-binary-missing");
            if (!string.Equals(
                    ComputeSha256(XcodeBuildPath),
                    RequiredXcodeBuildBinarySha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "xcodebuild-binary-hash-mismatch");
            }

            NativeProcessResult version = RunFixedProcess(
                XcodeBuildPath,
                "-version");
            string expectedVersion = "Xcode " + RequiredXcodeVersion + "\n" +
                                     "Build version " +
                                     RequiredXcodeBuildVersion;
            if (version.ExitCode != 0 ||
                !string.Equals(
                    version.StandardOutput.Trim().Replace("\r\n", "\n"),
                    expectedVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("xcode-version-mismatch");
            }

            NativeProcessResult license = RunFixedProcess(
                XcodeBuildPath,
                "-license check");
            if (license.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "xcode-license-not-accepted");
            }

            NativeProcessResult firstLaunch = RunFixedProcess(
                XcodeBuildPath,
                "-checkFirstLaunchStatus");
            if (firstLaunch.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "xcode-first-launch-incomplete");
            }

            NativeProcessResult sdk = RunFixedProcess(
                XcrunPath,
                "--sdk macosx --show-sdk-version");
            if (sdk.ExitCode != 0 ||
                !string.Equals(
                    sdk.StandardOutput.Trim(),
                    RequiredMacOsSdkVersion,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "macos-sdk-version-mismatch");
            }
        }

        private static NativeProcessResult RunFixedProcess(
            string executablePath,
            string arguments)
        {
            var info = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            info.EnvironmentVariables["DEVELOPER_DIR"] =
                XcodeDeveloperDirectory;
            Process? process = Process.Start(info);
            if (process == null)
            {
                throw new InvalidOperationException(
                    "fixed-tool-process-start-failed");
            }

            bool handleReleased = false;
            try
            {
                Task<string> standardOutput =
                    process.StandardOutput.ReadToEndAsync();
                Task<string> standardError =
                    process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(
                        FixedChildProcessTimeoutMilliseconds))
                {
                    InvalidOperationException failure =
                        CreateFixedChildFailure(
                            process,
                            "fixed-tool-process-timeout");
                    handleReleased = true;
                    throw failure;
                }

                if (!Task.WaitAll(
                        new Task[] { standardOutput, standardError },
                        FixedChildCaptureTimeoutMilliseconds))
                {
                    InvalidOperationException failure =
                        CreateFixedChildFailure(
                            process,
                            "fixed-tool-output-capture-timeout");
                    handleReleased = true;
                    throw failure;
                }

                var result = new NativeProcessResult(
                    process.ExitCode,
                    standardOutput.GetAwaiter().GetResult(),
                    standardError.GetAwaiter().GetResult());
                handleReleased = true;
                process.Dispose();
                return result;
            }
            catch (Exception exception)
            {
                if (handleReleased)
                {
                    throw;
                }

                throw CreateFixedChildFailure(
                    process,
                    "fixed-tool-process-failed:" +
                    NormalizeMessage(exception.Message));
            }
        }

        private static void InvalidateTrustedNativeState()
        {
            EditorApplication.delayCall -= ExecuteScheduledNativeBuild;
            _scheduledNativeBuild = null;
            _nativeBuildScheduleState = NativeBuildScheduleState.None;
            _nativeBuildScheduleFailure = string.Empty;
            _trustedNativeBuild = null;
            InvalidateTrustedNativeRun();
        }

        private static void InvalidateTrustedNativeRun()
        {
            TrustedNativeRun? trusted = _trustedNativeRun;
            if (trusted != null)
            {
                string cleanupFailure;
                if (TerminateProcess(trusted.Process, out cleanupFailure))
                {
                    _trustedNativeRun = null;
                    trusted.Process.Dispose();
                }
                else
                {
                    QuarantineNativeProcess(
                        trusted.Process,
                        "trusted-run-invalidation:" + cleanupFailure);
                    _trustedNativeRun = null;
                }
            }
            else if (_quarantinedNativeProcesses.Count != 0)
            {
                AttemptQuarantinedNativeProcessCleanup();
            }

            ReleaseNativePlayerReloadLockIfSafe();
        }

        private static bool AllowEditorQuitAfterNativeCleanup()
        {
            InvalidateTrustedNativeState();
            bool safe = _trustedNativeRun == null &&
                        _quarantinedNativeProcesses.Count == 0;
            if (!safe)
            {
                Debug.LogError(
                    "WP0002_NATIVE_PLAYER_QUARANTINED editor quit denied: " +
                    _nativePlayerCleanupFailure);
            }

            return safe;
        }

        private static void RejectNativeGateWhileCleanupQuarantined(
            string gateId)
        {
            if (!IsNativeGate(gateId) ||
                (_quarantinedNativeProcesses.Count == 0 &&
                 !(_nativePlayerReloadLockHeld && _trustedNativeRun == null)))
            {
                return;
            }

            bool cleaned = AttemptQuarantinedNativeProcessCleanup();
            ReleaseNativePlayerReloadLockIfSafe();
            if (cleaned && !_nativePlayerReloadLockHeld)
            {
                throw new InvalidOperationException(
                    "native-player-quarantine-cleared-retry-gate");
            }

            throw new InvalidOperationException(
                "native-player-cleanup-quarantined:" +
                _nativePlayerCleanupFailure);
        }

        private static bool IsNativeGate(string gateId)
        {
            return string.Equals(gateId, NativeBuildGate, StringComparison.Ordinal) ||
                   string.Equals(
                       gateId,
                       NativePerformanceStartGate,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       gateId,
                       NativePerformanceCollectGate,
                       StringComparison.Ordinal);
        }

        private static void HoldNativePlayerReloadLock()
        {
            if (_nativePlayerReloadLockHeld)
            {
                return;
            }

            EditorApplication.LockReloadAssemblies();
            _nativePlayerReloadLockHeld = true;
            _nativePlayerCleanupFailure = string.Empty;
        }

        private static void ReleaseNativePlayerReloadLockIfSafe()
        {
            if (!_nativePlayerReloadLockHeld ||
                _trustedNativeRun != null ||
                _quarantinedNativeProcesses.Count != 0)
            {
                return;
            }

            try
            {
                EditorApplication.UnlockReloadAssemblies();
                _nativePlayerReloadLockHeld = false;
                _nativePlayerCleanupFailure = string.Empty;
            }
            catch (Exception exception)
            {
                _nativePlayerCleanupFailure =
                    "native-player-reload-unlock-failed:" +
                    NormalizeMessage(exception.Message);
                Debug.LogError(_nativePlayerCleanupFailure);
            }
        }

        private static void QuarantineNativeProcess(
            Process process,
            string failure)
        {
            NativeProcessCleanup? retained = null;
            foreach (NativeProcessCleanup cleanup in
                     _quarantinedNativeProcesses)
            {
                if (ReferenceEquals(cleanup.Process, process))
                {
                    retained = cleanup;
                    break;
                }
            }

            if (retained == null)
            {
                retained = new NativeProcessCleanup(process, failure);
                _quarantinedNativeProcesses.Add(retained);
            }

            retained.Failure = failure;
            _nativePlayerCleanupFailure = failure;
            try
            {
                HoldNativePlayerReloadLock();
            }
            catch (Exception exception)
            {
                string lockFailure = failure +
                    ":native-process-reload-lock-failed:" +
                    NormalizeMessage(exception.Message);
                retained.Failure = lockFailure;
                _nativePlayerCleanupFailure = lockFailure;
                Debug.LogError(lockFailure);
            }
        }

        private static bool AttemptQuarantinedNativeProcessCleanup()
        {
            string latestFailure = string.Empty;
            for (int index = _quarantinedNativeProcesses.Count - 1;
                 index >= 0;
                 index--)
            {
                NativeProcessCleanup cleanup =
                    _quarantinedNativeProcesses[index];
                string failure;
                if (TerminateProcess(cleanup.Process, out failure))
                {
                    cleanup.Process.Dispose();
                    _quarantinedNativeProcesses.RemoveAt(index);
                }
                else
                {
                    cleanup.Failure = failure;
                    latestFailure = failure;
                }
            }

            if (_quarantinedNativeProcesses.Count != 0)
            {
                _nativePlayerCleanupFailure = latestFailure;
                return false;
            }

            return true;
        }

        private static InvalidOperationException CreateFixedChildFailure(
            Process process,
            string failure)
        {
            string cleanupFailure;
            if (TerminateProcess(process, out cleanupFailure))
            {
                try
                {
                    process.Dispose();
                }
                catch (Exception exception)
                {
                    failure += ";fixed-child-dispose-after-exit-failed:" +
                               NormalizeMessage(exception.Message);
                }
                return new InvalidOperationException(failure);
            }

            string quarantineFailure =
                "fixed-child-process:" + cleanupFailure;
            QuarantineNativeProcess(process, quarantineFailure);
            return new InvalidOperationException(
                failure + ";native-fixed-child-cleanup-quarantined:" +
                cleanupFailure);
        }

        private static bool TerminateProcess(
            Process process,
            out string failure)
        {
            failure = string.Empty;
            try
            {
                if (process.HasExited)
                {
                    return true;
                }

                process.Kill();
                if (!process.WaitForExit(
                        NativeProcessTerminationTimeoutMilliseconds))
                {
                    failure = "native-player-termination-timeout";
                    return false;
                }

                if (!process.HasExited)
                {
                    failure = "native-player-termination-unconfirmed";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    if (process.HasExited)
                    {
                        return true;
                    }
                }
                catch (Exception confirmationException)
                {
                    failure = "native-player-termination-failed:" +
                              NormalizeMessage(exception.Message) + ":" +
                              NormalizeMessage(confirmationException.Message);
                    return false;
                }

                failure = "native-player-termination-failed:" +
                          NormalizeMessage(exception.Message);
                return false;
            }
        }

        private static string ResolveGitHead(string repositoryRoot)
        {
            string marker = Path.Combine(repositoryRoot, ".git");
            string gitDirectory;
            if (Directory.Exists(marker))
            {
                gitDirectory = marker;
            }
            else if (File.Exists(marker))
            {
                string content = File.ReadAllText(marker, Encoding.UTF8).Trim();
                const string prefix = "gitdir: ";
                if (!content.StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "native-git-directory-invalid");
                }

                string value = content.Substring(prefix.Length);
                gitDirectory = Path.GetFullPath(
                    Path.IsPathRooted(value)
                        ? value
                        : Path.Combine(repositoryRoot, value));
            }
            else
            {
                throw new InvalidOperationException(
                    "native-git-directory-missing");
            }

            string head = File.ReadAllText(
                Path.Combine(gitDirectory, "HEAD"),
                Encoding.UTF8).Trim();
            const string refPrefix = "ref: ";
            string commit;
            if (head.StartsWith(refPrefix, StringComparison.Ordinal))
            {
                string reference = head.Substring(refPrefix.Length);
                if (reference.Contains("..", StringComparison.Ordinal) ||
                    Path.IsPathRooted(reference))
                {
                    throw new InvalidOperationException(
                        "native-git-head-reference-invalid");
                }

                string looseRef = Path.Combine(
                    gitDirectory,
                    reference.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(looseRef))
                {
                    commit = File.ReadAllText(looseRef, Encoding.UTF8).Trim();
                }
                else
                {
                    commit = ResolvePackedGitReference(
                        gitDirectory,
                        reference);
                }
            }
            else
            {
                commit = head;
            }

            ValidateLowerHex(commit, 40, "native-git-head-invalid");
            return commit;
        }

        private static string ResolvePackedGitReference(
            string gitDirectory,
            string reference)
        {
            string packedRefs = Path.Combine(gitDirectory, "packed-refs");
            if (!File.Exists(packedRefs))
            {
                throw new InvalidOperationException(
                    "native-git-head-reference-missing");
            }

            foreach (string line in File.ReadAllLines(
                         packedRefs,
                         Encoding.UTF8))
            {
                if (line.StartsWith("#", StringComparison.Ordinal) ||
                    line.StartsWith("^", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf(' ');
                if (separator > 0 &&
                    string.Equals(
                        line.Substring(separator + 1),
                        reference,
                        StringComparison.Ordinal))
                {
                    return line.Substring(0, separator);
                }
            }

            throw new InvalidOperationException(
                "native-git-head-reference-missing");
        }

        private static string ComputeNativeSourceTreeSha256(
            string repositoryRoot)
        {
            var roots = new[]
            {
                "Game/Assets",
                "Game/Packages",
                "Game/ProjectSettings",
                "SimulationCore",
                "SaveContracts"
            };
            var files = new List<string>();
            foreach (string relativeRoot in roots)
            {
                string absoluteRoot = Path.Combine(
                    repositoryRoot,
                    relativeRoot.Replace(
                        '/',
                        Path.DirectorySeparatorChar));
                if (!Directory.Exists(absoluteRoot))
                {
                    throw new InvalidOperationException(
                        "native-source-root-missing");
                }

                foreach (string file in Directory.EnumerateFiles(
                             absoluteRoot,
                             "*",
                             SearchOption.AllDirectories))
                {
                    files.Add(file);
                }
            }

            files.Sort(StringComparer.Ordinal);
            return ComputeFileManifestSha256(repositoryRoot, files);
        }

        private static string ComputeDirectoryTreeSha256(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new InvalidOperationException(
                    "native-build-artifact-missing");
            }

            var files = new List<string>(Directory.EnumerateFiles(
                directory,
                "*",
                SearchOption.AllDirectories));
            files.Sort(StringComparer.Ordinal);
            if (files.Count == 0)
            {
                throw new InvalidOperationException(
                    "native-build-artifact-empty");
            }

            return ComputeFileManifestSha256(directory, files);
        }

        private static string ComputeFileManifestSha256(
            string root,
            List<string> files)
        {
            var manifest = new StringBuilder();
            foreach (string file in files)
            {
                RequireRegularFile(file, "native-tree-file-invalid");
                string relative = Path.GetRelativePath(root, file)
                    .Replace(Path.DirectorySeparatorChar, '/');
                var info = new FileInfo(file);
                manifest.Append(relative)
                    .Append('\t')
                    .Append(info.Length.ToString(CultureInfo.InvariantCulture))
                    .Append('\t')
                    .Append(ComputeSha256(file))
                    .Append('\n');
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(manifest.ToString()));
        }

        private static void EnsureNativeOutputRoot(ProjectBoundary boundary)
        {
            RequirePathComponentsNotReparse(
                boundary.NativeOutputRoot,
                true,
                "native-output-root-invalid");
            Directory.CreateDirectory(boundary.NativeOutputRoot);
            string expected = Path.GetFullPath(
                Path.Combine(
                    boundary.RepositoryRoot,
                    NativeOutputRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            if (!string.Equals(
                    expected,
                    Path.GetFullPath(boundary.NativeOutputRoot),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-output-root-mismatch");
            }

            RequireRegularDirectory(
                boundary.NativeOutputRoot,
                "native-output-root-invalid");
        }

        private static string ResolveNativeOutputPath(
            ProjectBoundary boundary,
            string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) ||
                Path.IsPathRooted(relativePath) ||
                relativePath.Contains("..", StringComparison.Ordinal) ||
                relativePath.IndexOf('\\') >= 0)
            {
                throw new InvalidOperationException(
                    "native-output-relative-path-invalid");
            }

            string root = Path.GetFullPath(boundary.NativeOutputRoot)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string resolved = Path.GetFullPath(
                Path.Combine(
                    root,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            if (!resolved.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-output-path-escaped");
            }

            RequirePathComponentsNotReparse(
                resolved,
                true,
                "native-output-path-reparse-point");
            return resolved;
        }

        private static void RequirePathComponentsNotReparse(
            string path,
            bool allowMissingLeaf,
            string error)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                throw new InvalidOperationException(error);
            }

            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
            {
                throw new InvalidOperationException(error);
            }

            string current = root;
            string remainder = fullPath.Substring(root.Length);
            string[] components = remainder.Split(
                new[] { Path.DirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < components.Length; index++)
            {
                current = Path.Combine(current, components[index]);
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(current);
                }
                catch (FileNotFoundException)
                {
                    if (!allowMissingLeaf && index == components.Length - 1)
                    {
                        throw new InvalidOperationException(error);
                    }

                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    if (!allowMissingLeaf && index == components.Length - 1)
                    {
                        throw new InvalidOperationException(error);
                    }

                    continue;
                }
                catch (Exception)
                {
                    throw new InvalidOperationException(error);
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException(error);
                }
            }
        }

        private static void ValidateNativeRunDirectoryName(string value)
        {
            if (value.Length != 45 || value[12] != '-')
            {
                throw new InvalidOperationException(
                    "native-run-directory-name-invalid");
            }

            ValidateLowerHex(
                value.Substring(0, 12),
                12,
                "native-run-directory-name-invalid");
            ValidateLowerHex(
                value.Substring(13),
                32,
                "native-run-directory-name-invalid");
        }

        private static string NormalizeBuildGuid(string value)
        {
            string normalized = value.Trim()
                .Replace("-", string.Empty)
                .Replace("{", string.Empty)
                .Replace("}", string.Empty)
                .ToLowerInvariant();
            ValidateLowerHex(
                normalized,
                32,
                "native-build-guid-invalid");
            return normalized;
        }

        private static void ValidateLowerHex(
            string value,
            int length,
            string error)
        {
            if (value == null || value.Length != length)
            {
                throw new InvalidOperationException(error);
            }

            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    throw new InvalidOperationException(error);
                }
            }
        }

        private static bool IsMachOArm64Only(string path)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var header = new byte[8];
            if (stream.Read(header, 0, header.Length) != header.Length)
            {
                return false;
            }

            bool littleEndian64 =
                header[0] == 0xcf && header[1] == 0xfa &&
                header[2] == 0xed && header[3] == 0xfe;
            bool bigEndian64 =
                header[0] == 0xfe && header[1] == 0xed &&
                header[2] == 0xfa && header[3] == 0xcf;
            bool littleArm64 =
                header[4] == 0x0c && header[5] == 0x00 &&
                header[6] == 0x00 && header[7] == 0x01;
            bool bigArm64 =
                header[4] == 0x01 && header[5] == 0x00 &&
                header[6] == 0x00 && header[7] == 0x0c;
            return (littleEndian64 && littleArm64) ||
                   (bigEndian64 && bigArm64);
        }

        private static void VerifyNativeAuthorizationReceipt(
            ProjectBoundary boundary,
            string profileSha256,
            string boundaryText,
            string sourceCommit)
        {
            string previousReceiptPath = Path.Combine(
                boundary.RepositoryRoot,
                NativePreviousAuthorizationReceiptRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            RequireRegularFile(
                previousReceiptPath,
                "native-gate-predecessor-receipt-missing");
            byte[] previousReceiptBytes = File.ReadAllBytes(
                previousReceiptPath);
            if (!string.Equals(
                    ComputeSha256(previousReceiptBytes),
                    NativePreviousAuthorizationReceiptSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-gate-predecessor-receipt-hash-mismatch");
            }

            string receiptPath = Path.Combine(
                boundary.RepositoryRoot,
                NativeAuthorizationReceiptRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            RequireRegularFile(
                receiptPath,
                "native-gate-authorization-receipt-missing");
            byte[] receiptBytes = File.ReadAllBytes(receiptPath);
            string receiptText = Encoding.UTF8.GetString(receiptBytes);

            string protectedMain = ReadNativeGitText(
                boundary,
                NativeGitRead.OriginMainCommit).Trim();
            ValidateLowerHex(
                protectedMain,
                40,
                "native-protected-main-commit-invalid");
            byte[] protectedPreviousReceipt = ReadNativeGitBytes(
                boundary,
                NativeGitRead.PredecessorReceiptAtOriginMain,
                protectedMain);
            if (!ByteArraysEqual(
                    previousReceiptBytes,
                    protectedPreviousReceipt))
            {
                throw new InvalidOperationException(
                    "native-gate-predecessor-receipt-does-not-match-" +
                    "protected-main");
            }
            string[] introductions = ReadNativeGitText(
                    boundary,
                    NativeGitRead.ReceiptIntroductions)
                .Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);
            if (introductions.Length != 1)
            {
                throw new InvalidOperationException(
                    "native-gate-receipt-protected-introduction-not-unique");
            }

            string introduction = introductions[0];
            ValidateLowerHex(
                introduction,
                40,
                "native-gate-receipt-introduction-invalid");
            string[] parents = ReadNativeGitText(
                    boundary,
                    NativeGitRead.IntroductionParents,
                    introduction)
                .Trim()
                .Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);
            if (parents.Length != 1 ||
                !string.Equals(
                    parents[0],
                    NativeControlBaseCommit,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-gate-control-squash-base-mismatch");
            }

            NativeGitReadResult ancestry = RunNativeGitRead(
                boundary,
                NativeGitRead.IntroductionAncestorOfSource,
                introduction,
                sourceCommit);
            if (ancestry.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "native-gate-source-does-not-descend-from-control-squash");
            }

            string changedPathText = ReadNativeGitText(
                boundary,
                NativeGitRead.ControlChangedPaths,
                introduction);
            var actualChangedPaths = new HashSet<string>(
                changedPathText.Split(
                    new[] { '\0' },
                    StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
            var expectedChangedPaths = new HashSet<string>(
                NativeControlStage1Paths,
                StringComparer.Ordinal)
            {
                NativeAuthorizationReceiptRelativePath
            };
            if (actualChangedPaths.Count != expectedChangedPaths.Count ||
                !actualChangedPaths.SetEquals(expectedChangedPaths))
            {
                throw new InvalidOperationException(
                    "native-gate-control-squash-path-set-mismatch");
            }

            byte[] introductionReceipt = ReadNativeGitBytes(
                boundary,
                NativeGitRead.ReceiptAtIntroduction,
                introduction);
            byte[] protectedReceipt = ReadNativeGitBytes(
                boundary,
                NativeGitRead.ReceiptAtOriginMain,
                protectedMain);
            if (!ByteArraysEqual(receiptBytes, introductionReceipt) ||
                !ByteArraysEqual(receiptBytes, protectedReceipt))
            {
                throw new InvalidOperationException(
                    "native-gate-receipt-does-not-match-protected-main");
            }

            NativeGateAuthorizationReceipt receipt =
                ReadJson<NativeGateAuthorizationReceipt>(
                    receiptBytes);
            if (!string.Equals(
                    receipt.receipt_id,
                    NativeAuthorizationReceiptId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.receipt_kind,
                    "creator-authorization",
                    StringComparison.Ordinal) ||
                !receipt.@sealed ||
                !string.Equals(receipt.issued_by, "AC-21", StringComparison.Ordinal) ||
                !string.Equals(receipt.issuer_role, "creator", StringComparison.Ordinal) ||
                !IsLowerHex(receipt.accepted_commit, 40))
            {
                throw new InvalidOperationException(
                    "native-gate-authorization-receipt-invalid");
            }

            string boundarySha256 = ComputeSha256(boundary.BoundaryPath);
            string governancePath = Path.Combine(
                boundary.RepositoryRoot,
                NativeGovernanceRecordRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            RequireRegularFile(
                governancePath,
                "native-gate-governance-record-missing");
            string governanceSha256 = ComputeSha256(governancePath);
            foreach (string token in new[]
            {
                "\"" + NativeAuthorizationClaim + "\"",
                "\"" + NativeAuthorizationSupersessionClaim + "\"",
                "\"wp0002-gate-dispatcher-v3\": \"" +
                    NativePreviousDispatcherSha256 + "\"",
                "\"" + NativePreviousAuthorizationReceiptRelativePath +
                    "\": \"" +
                    NativePreviousAuthorizationReceiptSha256 + "\"",
                "\"WP-0002\": \"" + PacketContractSha256 + "\"",
                "\"Game/" + NativeProfileAssetPath + "\": \"" +
                    profileSha256 + "\"",
                "\"" + BoundaryRelativePath + "\": \"" +
                    boundarySha256 + "\"",
                "\"" + NativeGovernanceRecordRelativePath + "\": \"" +
                    governanceSha256 + "\""
            })
            {
                if (receiptText.IndexOf(token, StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException(
                        "native-gate-authorization-receipt-binding-mismatch");
                }
            }

            RequireBoundaryBinding(
                boundaryText,
                "\"amendment_id\": " +
                "\"A1B-WP-0002-NATIVE-EDITOR-PATH-CORRECTION-20260719\"",
                expectedOccurrences: 2);
            RequireBoundaryBinding(
                boundaryText,
                "\"amendment_id\": " +
                "\"A1B-WP-0002-NATIVE-BOUNDARY-DUPLICATE-COUNT-" +
                "CORRECTION-20260720\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"stage2_delta_policy\": " +
                "\"exactly-one-added-regular-sealed-duplicate-count-" +
                "receipt-file\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"duplicate_count_corrected_gates_may_validate_their_own_" +
                "control_pr\": false");
            RequireBoundaryBinding(
                boundaryText,
                "\"amendment_id\": \"A1B-WP-0002-NATIVE-PLAYER-" +
                "EXECUTABLE-PATH-CORRECTION-20260720\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"player_executable_relative_path\": \"" +
                NativeExecutableRelativePath + "\"");
            RequireBoundaryBinding(
                boundaryText,
                "\"accepted_player_executable_path_count\": 1");
            RequireBoundaryBinding(
                boundaryText,
                "\"fallback_player_executable_paths_allowed\": false");
            RequireBoundaryBinding(
                boundaryText,
                "\"hash_or_signature_checks_changed\": false");
            RequireBoundaryBinding(
                boundaryText,
                "\"stage2_delta_policy\": \"exactly-one-added-regular-" +
                "sealed-player-executable-path-correction-receipt-file\"");
        }

        private static string ReadNativeGitText(
            ProjectBoundary boundary,
            NativeGitRead operation,
            string commit = "",
            string sourceCommit = "")
        {
            return Encoding.UTF8.GetString(
                ReadNativeGitBytes(
                    boundary,
                    operation,
                    commit,
                    sourceCommit));
        }

        private static byte[] ReadNativeGitBytes(
            ProjectBoundary boundary,
            NativeGitRead operation,
            string commit = "",
            string sourceCommit = "")
        {
            NativeGitReadResult result = RunNativeGitRead(
                boundary,
                operation,
                commit,
                sourceCommit);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "native-gate-protected-git-read-failed-" +
                    operation.ToString().ToLowerInvariant());
            }

            return result.StandardOutput;
        }

        private static NativeGitReadResult RunNativeGitRead(
            ProjectBoundary boundary,
            NativeGitRead operation,
            string commit = "",
            string sourceCommit = "")
        {
            RequireRegularFile(
                GitExecutablePath,
                "native-git-executable-missing");
            if (!string.Equals(
                    ComputeSha256(GitExecutablePath),
                    RequiredGitBinarySha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-git-executable-hash-mismatch");
            }

            RequireRegularDirectory(
                boundary.RepositoryRoot,
                "native-git-working-directory-invalid");
            if (!string.IsNullOrEmpty(commit))
            {
                ValidateLowerHex(commit, 40, "native-git-read-commit-invalid");
            }
            if (!string.IsNullOrEmpty(sourceCommit))
            {
                ValidateLowerHex(
                    sourceCommit,
                    40,
                    "native-git-read-source-commit-invalid");
            }

            string arguments;
            switch (operation)
            {
                case NativeGitRead.OriginMainCommit:
                    arguments =
                        "--no-replace-objects rev-parse --verify " +
                        "refs/remotes/origin/main^{commit}";
                    break;
                case NativeGitRead.ReceiptIntroductions:
                    arguments =
                        "--no-replace-objects log --first-parent " +
                        "--diff-filter=A --format=%H refs/remotes/origin/main -- " +
                        NativeAuthorizationReceiptRelativePath;
                    break;
                case NativeGitRead.IntroductionParents:
                    arguments =
                        "--no-replace-objects show -s --format=%P " + commit;
                    break;
                case NativeGitRead.ControlChangedPaths:
                    arguments =
                        "--no-replace-objects diff-tree --no-commit-id " +
                        "--name-only -r -z " + NativeControlBaseCommit + " " +
                        commit;
                    break;
                case NativeGitRead.ReceiptAtIntroduction:
                    arguments =
                        "--no-replace-objects cat-file blob " + commit + ":" +
                        NativeAuthorizationReceiptRelativePath;
                    break;
                case NativeGitRead.ReceiptAtOriginMain:
                    arguments =
                        "--no-replace-objects cat-file blob " + commit + ":" +
                        NativeAuthorizationReceiptRelativePath;
                    break;
                case NativeGitRead.PredecessorReceiptAtOriginMain:
                    arguments =
                        "--no-replace-objects cat-file blob " + commit + ":" +
                        NativePreviousAuthorizationReceiptRelativePath;
                    break;
                case NativeGitRead.IntroductionAncestorOfSource:
                    arguments =
                        "--no-replace-objects merge-base --is-ancestor " +
                        commit + " " + sourceCommit;
                    break;
                default:
                    throw new InvalidOperationException(
                        "native-git-read-operation-rejected");
            }

            var info = new ProcessStartInfo
            {
                FileName = GitExecutablePath,
                Arguments = arguments,
                WorkingDirectory = boundary.RepositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            var inheritedGitKeys = new List<string>();
            foreach (string key in info.EnvironmentVariables.Keys)
            {
                if (key.StartsWith("GIT_", StringComparison.Ordinal))
                {
                    inheritedGitKeys.Add(key);
                }
            }
            foreach (string key in inheritedGitKeys)
            {
                info.EnvironmentVariables.Remove(key);
            }
            info.EnvironmentVariables["GIT_CONFIG_NOSYSTEM"] = "1";
            info.EnvironmentVariables["GIT_CONFIG_GLOBAL"] = "/dev/null";
            info.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";
            info.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            using var output = new MemoryStream();
            Process? process = Process.Start(info);
            if (process == null)
            {
                throw new InvalidOperationException(
                    "native-git-read-process-start-failed");
            }

            bool handleReleased = false;
            try
            {
                Task outputCapture =
                    process.StandardOutput.BaseStream.CopyToAsync(output);
                Task<string> standardError =
                    process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(
                        FixedChildProcessTimeoutMilliseconds))
                {
                    InvalidOperationException failure =
                        CreateFixedChildFailure(
                            process,
                            "native-git-read-process-timeout");
                    handleReleased = true;
                    throw failure;
                }

                if (!Task.WaitAll(
                        new Task[] { outputCapture, standardError },
                        FixedChildCaptureTimeoutMilliseconds))
                {
                    InvalidOperationException failure =
                        CreateFixedChildFailure(
                            process,
                            "native-git-read-output-capture-timeout");
                    handleReleased = true;
                    throw failure;
                }

                var result = new NativeGitReadResult(
                    process.ExitCode,
                    output.ToArray(),
                    standardError.GetAwaiter().GetResult());
                handleReleased = true;
                process.Dispose();
                return result;
            }
            catch (Exception exception)
            {
                if (handleReleased)
                {
                    throw;
                }

                throw CreateFixedChildFailure(
                    process,
                    "native-git-read-process-failed:" +
                    NormalizeMessage(exception.Message));
            }
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }

        private static NativeBuildManifest VerifyNativeBuildPointer(
            ProjectBoundary boundary,
            NativeSourceIdentity source,
            NativeBuildPointer pointer)
        {
            if (pointer.schema_version != 1 ||
                !string.Equals(
                    pointer.contract_id,
                    NativeBuildPointerContract,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-build-pointer-contract-mismatch");
            }

            ValidateLowerHex(
                pointer.build_guid,
                32,
                "native-build-pointer-guid-invalid");
            ValidateSha256(pointer.build_manifest_sha256);
            string expectedRunDirectory = "runs/" +
                source.SourceTreeSha256.Substring(0, 12) + "-" +
                pointer.build_guid;
            string expectedManifestPath = expectedRunDirectory + "/" +
                NativeBuildManifestName;
            if (!string.Equals(
                    pointer.run_directory,
                    expectedRunDirectory,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    pointer.build_manifest_relative_path,
                    expectedManifestPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-build-pointer-path-mismatch");
            }

            string manifestPath = ResolveNativeOutputPath(
                boundary,
                expectedManifestPath);
            RequireRegularFile(
                manifestPath,
                "native-build-manifest-missing");
            byte[] manifestPayload = File.ReadAllBytes(manifestPath);
            if (!string.Equals(
                    ComputeSha256(manifestPayload),
                    pointer.build_manifest_sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-build-manifest-hash-mismatch");
            }

            NativeBuildManifest manifest =
                ReadJson<NativeBuildManifest>(manifestPayload);
            bool valid = manifest.schema_version == 1 &&
                string.Equals(
                    manifest.contract_id,
                    NativeBuildManifestContract,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.build_guid,
                    pointer.build_guid,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.source_commit,
                    source.SourceCommit,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.source_tree_sha256,
                    source.SourceTreeSha256,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.dispatcher_sha256,
                    source.DispatcherSha256,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.build_profile_sha256,
                    source.BuildProfileSha256,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.scene_sha256,
                    source.SceneSha256,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.unity_version,
                    RequiredUnityVersion,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.unity_binary_sha256,
                    RequiredUnityBinarySha256,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.xcode_version,
                    RequiredXcodeVersion,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.xcode_build_version,
                    RequiredXcodeBuildVersion,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.xcodebuild_binary_sha256,
                    RequiredXcodeBuildBinarySha256,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.macos_sdk_version,
                    RequiredMacOsSdkVersion,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.build_target,
                    "StandaloneOSX",
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.architecture,
                    "arm64",
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.scripting_backend,
                    "IL2CPP",
                    StringComparison.Ordinal) &&
                manifest.development_build &&
                manifest.width == NativeScreenWidth &&
                manifest.height == NativeScreenHeight &&
                string.Equals(
                    manifest.run_directory,
                    expectedRunDirectory,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.player_relative_path,
                    NativePlayerName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.executable_relative_path,
                    NativeExecutableRelativePath,
                    StringComparison.Ordinal) &&
                manifest.total_size_bytes > 0 &&
                TryParseRoundtripUtc(manifest.built_at);
            if (!valid)
            {
                throw new InvalidOperationException(
                    "native-build-manifest-contract-mismatch");
            }

            ValidateSha256(manifest.executable_sha256);
            ValidateSha256(manifest.build_artifact_sha256);
            string runDirectory = ResolveNativeOutputPath(
                boundary,
                manifest.run_directory);
            RequireRegularDirectory(
                runDirectory,
                "native-run-directory-missing");
            string playerPath = Path.Combine(
                runDirectory,
                manifest.player_relative_path);
            RequireRegularDirectory(
                playerPath,
                "native-player-bundle-missing");
            string executablePath = Path.Combine(
                runDirectory,
                manifest.executable_relative_path.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string gameAssemblyPath = Path.Combine(
                playerPath,
                "Contents",
                "Frameworks",
                "GameAssembly.dylib");
            RequireRegularFile(
                executablePath,
                "native-executable-missing");
            RequireRegularFile(
                gameAssemblyPath,
                "native-il2cpp-gameassembly-missing");
            if (!IsMachOArm64Only(executablePath) ||
                !IsMachOArm64Only(gameAssemblyPath) ||
                !string.Equals(
                    ComputeSha256(executablePath),
                    manifest.executable_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ComputeDirectoryTreeSha256(playerPath),
                    manifest.build_artifact_sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-build-artifact-binding-mismatch");
            }

            return manifest;
        }

        private static TrustedNativeBuild RequireTrustedNativeBuild(
            NativeSourceIdentity source)
        {
            TrustedNativeBuild? trusted = _trustedNativeBuild;
            if (trusted == null ||
                !string.Equals(
                    trusted.EditorSessionNonce,
                    NativeEditorSessionNonce,
                    StringComparison.Ordinal) ||
                trusted.EditorProcessId != NativeEditorProcessId ||
                !string.Equals(
                    trusted.EditorProcessStartedAt,
                    NativeEditorProcessStartedAt,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.SourceCommit,
                    source.SourceCommit,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.SourceTreeSha256,
                    source.SourceTreeSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.DispatcherSha256,
                    source.DispatcherSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.BuildProfileSha256,
                    source.BuildProfileSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.SceneSha256,
                    source.SceneSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-build-lacks-same-editor-attestation");
            }

            return trusted;
        }

        private static void VerifyTrustedNativeBuild(
            TrustedNativeBuild trusted,
            NativeBuildPointer pointer,
            NativeBuildManifest manifest)
        {
            if (!string.Equals(
                    trusted.BuildGuid,
                    pointer.build_guid,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.RunDirectory,
                    pointer.run_directory,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.BuildManifestSha256,
                    pointer.build_manifest_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.BuildGuid,
                    manifest.build_guid,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.ExecutableSha256,
                    manifest.executable_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.BuildArtifactSha256,
                    manifest.build_artifact_sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-build-disk-evidence-does-not-match-attestation");
            }
        }

        private static TrustedNativeRun RequireTrustedNativeRun()
        {
            TrustedNativeRun? trusted = _trustedNativeRun;
            if (trusted == null ||
                !string.Equals(
                    trusted.EditorSessionNonce,
                    NativeEditorSessionNonce,
                    StringComparison.Ordinal) ||
                trusted.EditorProcessId != NativeEditorProcessId ||
                !string.Equals(
                    trusted.EditorProcessStartedAt,
                    NativeEditorProcessStartedAt,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-run-lacks-same-editor-attestation");
            }

            return trusted;
        }

        private static void VerifyTrustedNativeRun(
            TrustedNativeRun trusted,
            NativeRunPointer pointer,
            NativeRunManifest run)
        {
            if (!string.Equals(
                    trusted.RequestNonce,
                    pointer.request_nonce,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.RunManifestSha256,
                    pointer.run_manifest_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.RequestNonce,
                    run.request_nonce,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.ProcessStartedAt,
                    run.process_started_at,
                    StringComparison.Ordinal) ||
                trusted.Process.Id != run.process_id ||
                !string.Equals(
                    trusted.RunDirectory,
                    run.run_directory,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.ReportRelativePath,
                    run.report_relative_path,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.BuildManifestSha256,
                    run.build_manifest_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.BuildIdentitySha256,
                    run.build_identity_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.RequestSha256,
                    run.request_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.ExecutableSha256,
                    run.executable_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    trusted.BuildArtifactSha256,
                    run.build_artifact_sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-run-disk-evidence-does-not-match-attestation");
            }
        }

        private static NativeRunManifest VerifyNativeRunPointer(
            ProjectBoundary boundary,
            NativeSourceIdentity source,
            NativeRunPointer pointer)
        {
            if (pointer.schema_version != 1 ||
                !string.Equals(
                    pointer.contract_id,
                    NativeRunPointerContract,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-run-pointer-contract-mismatch");
            }

            ValidateLowerHex(
                pointer.request_nonce,
                32,
                "native-request-nonce-invalid");
            ValidateSha256(pointer.run_manifest_sha256);
            NativeBuildPointer buildPointer =
                ReadJsonFile<NativeBuildPointer>(
                    Path.Combine(
                        boundary.NativeOutputRoot,
                        NativeLatestBuildName));
            NativeBuildManifest build = VerifyNativeBuildPointer(
                boundary,
                source,
                buildPointer);
            string expectedManifestPath = build.run_directory + "/" +
                NativeRunManifestName;
            if (!string.Equals(
                    pointer.run_manifest_relative_path,
                    expectedManifestPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-run-pointer-path-mismatch");
            }

            string runManifestPath = ResolveNativeOutputPath(
                boundary,
                expectedManifestPath);
            RequireRegularFile(
                runManifestPath,
                "native-run-manifest-missing");
            byte[] runPayload = File.ReadAllBytes(runManifestPath);
            if (!string.Equals(
                    ComputeSha256(runPayload),
                    pointer.run_manifest_sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-run-manifest-hash-mismatch");
            }

            NativeRunManifest run = ReadJson<NativeRunManifest>(runPayload);
            bool valid = run.schema_version == 1 &&
                string.Equals(
                    run.contract_id,
                    NativeRunManifestContract,
                    StringComparison.Ordinal) &&
                string.Equals(
                    run.request_nonce,
                    pointer.request_nonce,
                    StringComparison.Ordinal) &&
                string.Equals(run.source_commit, build.source_commit, StringComparison.Ordinal) &&
                string.Equals(run.source_tree_sha256, build.source_tree_sha256, StringComparison.Ordinal) &&
                string.Equals(run.dispatcher_sha256, build.dispatcher_sha256, StringComparison.Ordinal) &&
                string.Equals(run.build_profile_sha256, build.build_profile_sha256, StringComparison.Ordinal) &&
                string.Equals(run.build_guid, build.build_guid, StringComparison.Ordinal) &&
                string.Equals(run.build_artifact_sha256, build.build_artifact_sha256, StringComparison.Ordinal) &&
                string.Equals(run.executable_sha256, build.executable_sha256, StringComparison.Ordinal) &&
                string.Equals(run.build_manifest_sha256, buildPointer.build_manifest_sha256, StringComparison.Ordinal) &&
                string.Equals(run.run_directory, build.run_directory, StringComparison.Ordinal) &&
                string.Equals(
                    run.report_relative_path,
                    build.run_directory + "/wp0002-native-performance-" +
                    run.request_nonce + ".report.json",
                    StringComparison.Ordinal) &&
                run.process_id > 0 &&
                TryParseRoundtripUtc(run.process_started_at) &&
                TryParseRoundtripUtc(run.started_at);
            if (!valid)
            {
                throw new InvalidOperationException(
                    "native-run-manifest-contract-mismatch");
            }

            ValidateSha256(run.build_identity_sha256);
            ValidateSha256(run.request_sha256);
            string runDirectory = ResolveNativeOutputPath(
                boundary,
                run.run_directory);
            string identityPath = Path.Combine(
                runDirectory,
                NativeBuildIdentityName);
            string requestPath = Path.Combine(
                runDirectory,
                NativeRequestName);
            RequireRegularFile(
                identityPath,
                "native-build-identity-missing");
            RequireRegularFile(
                requestPath,
                "native-performance-request-missing");
            byte[] identityPayload = File.ReadAllBytes(identityPath);
            byte[] requestPayload = File.ReadAllBytes(requestPath);
            if (!string.Equals(
                    ComputeSha256(identityPayload),
                    run.build_identity_sha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ComputeSha256(requestPayload),
                    run.request_sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-run-input-hash-mismatch");
            }

            NativeBuildIdentity identity =
                ReadJson<NativeBuildIdentity>(identityPayload);
            NativePerformanceRequest request =
                ReadJson<NativePerformanceRequest>(requestPayload);
            if (identity.schema_version != 1 ||
                !string.Equals(identity.identity_id, NativeBuildIdentityContract, StringComparison.Ordinal) ||
                !string.Equals(identity.source_commit, build.source_commit, StringComparison.Ordinal) ||
                !string.Equals(identity.source_tree_sha256, build.source_tree_sha256, StringComparison.Ordinal) ||
                !string.Equals(identity.build_guid, build.build_guid, StringComparison.Ordinal) ||
                !string.Equals(identity.unity_version, RequiredUnityVersion, StringComparison.Ordinal) ||
                !string.Equals(identity.executable_sha256, build.executable_sha256, StringComparison.Ordinal) ||
                !identity.development_build)
            {
                throw new InvalidOperationException(
                    "native-build-identity-contract-mismatch");
            }

            if (request.schema_version != 1 ||
                !string.Equals(request.contract_id, NativeRequestContract, StringComparison.Ordinal) ||
                !string.Equals(request.request_nonce, run.request_nonce, StringComparison.Ordinal) ||
                !string.Equals(request.expected_source_commit, build.source_commit, StringComparison.Ordinal) ||
                !string.Equals(request.expected_source_tree_sha256, build.source_tree_sha256, StringComparison.Ordinal) ||
                !string.Equals(request.expected_build_identity_sha256, run.build_identity_sha256, StringComparison.Ordinal) ||
                !string.Equals(request.expected_build_guid, build.build_guid, StringComparison.Ordinal) ||
                !string.Equals(request.expected_executable_sha256, build.executable_sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-performance-request-contract-mismatch");
            }

            return run;
        }

        private static void RequireNativeProcessExited(
            TrustedNativeRun trusted,
            NativeRunManifest run)
        {
            try
            {
                string actualStartedAt = trusted.Process.StartTime.ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture);
                if (!string.Equals(
                        actualStartedAt,
                        run.process_started_at,
                        StringComparison.Ordinal) ||
                    trusted.Process.Id != run.process_id)
                {
                    throw new InvalidOperationException(
                        "native-performance-process-identity-mismatch");
                }

                if (!trusted.Process.HasExited)
                {
                    throw new InvalidOperationException(
                        "native-performance-process-still-running");
                }
            }
            catch (InvalidOperationException exception) when (
                !string.Equals(
                    exception.Message,
                    "native-performance-process-still-running",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    exception.Message,
                    "native-performance-process-identity-mismatch",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "native-performance-process-handle-invalid");
            }
        }

        private static void ValidateNativePerformanceReport(
            NativeRunManifest run,
            NativePerformanceReport report)
        {
            if (report == null ||
                report.schema_version != 1 ||
                !string.Equals(report.report_kind, NativeRuntimeReportContract, StringComparison.Ordinal) ||
                !string.Equals(report.status, "completed", StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(report.failure_code) ||
                !string.Equals(report.request_nonce, run.request_nonce, StringComparison.Ordinal) ||
                !string.Equals(report.request_sha256, run.request_sha256, StringComparison.Ordinal) ||
                !TryParseRoundtripUtc(report.run_started_utc) ||
                !TryParseRoundtripUtc(report.report_generated_utc) ||
                report.requested_warmup_seconds != NativeWarmupSeconds ||
                !IsFinite(report.actual_warmup_seconds) ||
                report.actual_warmup_seconds < NativeWarmupSeconds ||
                report.build == null ||
                report.environment == null ||
                report.paused == null ||
                report.representative_unpaused == null ||
                report.city_garage_cycles == null ||
                report.cycles == null ||
                report.topology == null ||
                report.paused_retention == null ||
                report.acceptance == null)
            {
                throw new InvalidOperationException(
                    "native-performance-report-contract-mismatch");
            }

            ValidateNativeReportBuild(run, report.build);
            if (report.environment.requested_width != NativeScreenWidth ||
                report.environment.requested_height != NativeScreenHeight ||
                report.environment.actual_width != NativeScreenWidth ||
                report.environment.actual_height != NativeScreenHeight ||
                !string.Equals(
                    report.environment.actual_full_screen_mode,
                    "Windowed",
                    StringComparison.Ordinal) ||
                !report.environment.exact_resolution_verified)
            {
                throw new InvalidOperationException(
                    "native-performance-resolution-not-exact");
            }

            ValidateNativePhase(
                report.paused,
                "paused-unchanged-city",
                NativePausedSeconds);
            ValidateNativePhase(
                report.representative_unpaused,
                "representative-unpaused-city",
                NativeUnpausedSeconds);
            ValidateNativePhase(
                report.city_garage_cycles,
                "city-garage-cycles",
                0);
            ValidateNativeCycles(report.cycles);
            ValidateNativeTopology(report.topology);
            ValidateNativePausedRetention(report.paused_retention);

            NativeAcceptance acceptance = report.acceptance;
            if (!acceptance.paused_p95_zero_bytes_per_frame ||
                !acceptance.representative_unpaused_p95_zero_bytes_per_frame ||
                !acceptance.representative_unpaused_average_below_1024_bytes_per_frame ||
                !acceptance.all_sample_buffers_populated_without_overflow ||
                !acceptance.exact_100_cycles ||
                !acceptance.stable_retained_topology ||
                !acceptance.paused_no_retained_memory_growth ||
                !acceptance.cycles_no_monotonic_memory_growth ||
                !acceptance.post_cycle_exactly_one_action ||
                !acceptance.exact_2560x1600 ||
                !acceptance.passed)
            {
                throw new InvalidOperationException(
                    "native-performance-acceptance-failed");
            }
        }

        private static void ValidateNativeReportBuild(
            NativeRunManifest run,
            NativeReportBuild build)
        {
            if (!string.Equals(build.source_commit, run.source_commit, StringComparison.Ordinal) ||
                !string.Equals(build.source_tree_sha256, run.source_tree_sha256, StringComparison.Ordinal) ||
                !string.Equals(build.build_identity_sha256, run.build_identity_sha256, StringComparison.Ordinal) ||
                !string.Equals(build.executable_sha256, run.executable_sha256, StringComparison.Ordinal) ||
                !string.Equals(build.application_build_guid, run.build_guid, StringComparison.Ordinal) ||
                !string.Equals(build.unity_version, RequiredUnityVersion, StringComparison.Ordinal) ||
                !build.enable_il2cpp ||
                !string.Equals(build.process_architecture, "Arm64", StringComparison.Ordinal) ||
                !build.arm64_process ||
                !build.development_build ||
                !build.request_runtime_identity_matched)
            {
                throw new InvalidOperationException(
                    "native-performance-runtime-identity-mismatch");
            }
        }

        private static void ValidateNativePhase(
            NativePerformancePhase phase,
            string phaseId,
            double minimumMeasuredSeconds)
        {
            if (!string.Equals(phase.phase_id, phaseId, StringComparison.Ordinal) ||
                !IsFinite(phase.started_at_realtime_seconds) ||
                phase.started_at_realtime_seconds < 0 ||
                !IsFinite(phase.ended_at_realtime_seconds) ||
                phase.ended_at_realtime_seconds < phase.started_at_realtime_seconds ||
                !IsFinite(phase.measured_seconds) ||
                phase.measured_seconds < minimumMeasuredSeconds ||
                phase.frame_sample_count <= 0 ||
                phase.frame_sample_capacity_exceeded ||
                phase.simulation_tick_sample_count <= 0 ||
                phase.simulation_tick_capacity_exceeded ||
                !IsFiniteNonNegative(phase.gc_allocated_average_bytes) ||
                phase.gc_allocated_p95_bytes < 0 ||
                phase.gc_allocated_max_bytes < 0 ||
                !IsFiniteNonNegative(phase.frame_time_average_ms) ||
                !IsFiniteNonNegative(phase.frame_time_p95_ms) ||
                !IsFiniteNonNegative(phase.frame_time_p99_ms) ||
                !IsFiniteNonNegative(phase.frame_time_max_ms) ||
                !IsFiniteNonNegative(phase.simulation_tick_average_ms) ||
                !IsFiniteNonNegative(phase.simulation_tick_p95_ms) ||
                !IsFiniteNonNegative(phase.simulation_tick_max_ms))
            {
                throw new InvalidOperationException(
                    "native-performance-phase-invalid-" + phaseId);
            }
        }

        private static void ValidateNativeCycles(NativeCycles cycles)
        {
            if (cycles.requested_cycles != NativeCityGarageCycles ||
                cycles.completed_cycles != NativeCityGarageCycles ||
                !cycles.canonical_state_unchanged ||
                !IsLowerHex(cycles.canonical_sha256_before, 64) ||
                !string.Equals(
                    cycles.canonical_sha256_before,
                    cycles.canonical_sha256_after,
                    StringComparison.Ordinal) ||
                !cycles.topology_stable_at_all_checkpoints ||
                cycles.managed_heap_monotonic_growth_detected ||
                cycles.unity_allocated_monotonic_growth_detected ||
                cycles.unity_reserved_monotonic_growth_detected ||
                !cycles.no_monotonic_memory_growth ||
                cycles.post_cycle_first_submit_command_delta != 1 ||
                cycles.post_cycle_duplicate_submit_command_delta != 0 ||
                !cycles.post_cycle_exactly_one_action ||
                cycles.memory_checkpoints == null ||
                cycles.memory_checkpoints.Length < 2)
            {
                throw new InvalidOperationException(
                    "native-performance-cycle-proof-invalid");
            }

            int previousCompleted = -1;
            foreach (NativeMemoryCheckpoint checkpoint in cycles.memory_checkpoints)
            {
                if (checkpoint == null ||
                    checkpoint.completed_cycles <= previousCompleted ||
                    checkpoint.completed_cycles > NativeCityGarageCycles ||
                    checkpoint.managed_heap_bytes < 0 ||
                    checkpoint.unity_allocated_bytes < 0 ||
                    checkpoint.unity_reserved_bytes < 0)
                {
                    throw new InvalidOperationException(
                        "native-performance-memory-checkpoint-invalid");
                }

                previousCompleted = checkpoint.completed_cycles;
            }

            if (previousCompleted != NativeCityGarageCycles)
            {
                throw new InvalidOperationException(
                    "native-performance-final-checkpoint-missing");
            }
        }

        private static void ValidateNativeTopology(NativeTopology topology)
        {
            if (topology.initial_owned_unity_objects < 0 ||
                topology.initial_owned_unity_objects != topology.final_owned_unity_objects ||
                topology.initial_visual_elements < 0 ||
                topology.initial_visual_elements != topology.final_visual_elements ||
                topology.initial_bindings < 0 ||
                topology.initial_bindings != topology.final_bindings ||
                topology.initial_registered_callbacks < 0 ||
                topology.initial_registered_callbacks != topology.final_registered_callbacks ||
                !topology.exact_identity_set_retained ||
                topology.initial_ui_documents < 0 ||
                topology.initial_ui_documents != topology.final_ui_documents ||
                !topology.exact_ui_document_set_retained ||
                topology.initial_cameras < 0 ||
                topology.initial_cameras != topology.final_cameras ||
                !topology.exact_camera_set_retained ||
                topology.initial_audio_listeners < 0 ||
                topology.initial_audio_listeners != topology.final_audio_listeners ||
                !topology.exact_audio_listener_set_retained)
            {
                throw new InvalidOperationException(
                    "native-performance-topology-invalid");
            }
        }

        private static void ValidateNativePausedRetention(
            NativePausedRetention retention)
        {
            if (retention.managed_heap_bytes_before < 0 ||
                retention.managed_heap_bytes_after < 0 ||
                retention.managed_heap_growth_bytes > 0 ||
                !IsFinite(retention.managed_heap_growth_percent) ||
                retention.managed_heap_growth_percent > 0 ||
                !retention.managed_heap_not_grown ||
                retention.unity_allocated_bytes_before < 0 ||
                retention.unity_allocated_bytes_after < 0 ||
                retention.unity_allocated_growth_bytes > 0 ||
                !IsFinite(retention.unity_allocated_growth_percent) ||
                retention.unity_allocated_growth_percent > 0 ||
                !retention.unity_allocated_not_grown ||
                retention.unity_reserved_bytes_before < 0 ||
                retention.unity_reserved_bytes_after < 0 ||
                retention.unity_reserved_growth_bytes > 0 ||
                !IsFinite(retention.unity_reserved_growth_percent) ||
                retention.unity_reserved_growth_percent > 0 ||
                !retention.unity_reserved_not_grown ||
                !retention.no_retained_memory_growth ||
                !retention.canonical_state_unchanged ||
                !IsLowerHex(retention.canonical_sha256_before, 64) ||
                !string.Equals(
                    retention.canonical_sha256_before,
                    retention.canonical_sha256_after,
                    StringComparison.Ordinal) ||
                !retention.exact_topology_retained)
            {
                throw new InvalidOperationException(
                    "native-performance-paused-retention-invalid");
            }
        }

        private static bool TryParseRoundtripUtc(string value)
        {
            return DateTime.TryParse(
                       value,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out DateTime parsed) &&
                   parsed.Kind == DateTimeKind.Utc;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return IsFinite(value) && value >= 0;
        }

        private static bool IsLowerHex(string value, int length)
        {
            if (value == null || value.Length != length)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static void RequireRegularFile(string path, string error)
        {
            RequirePathComponentsNotReparse(path, false, error);
            if (!File.Exists(path) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(error);
            }
        }

        private static void RequireRegularDirectory(string path, string error)
        {
            RequirePathComponentsNotReparse(path, false, error);
            if (!Directory.Exists(path) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(error);
            }
        }

        private static T ReadJsonFile<T>(string path) where T : class
        {
            RequireRegularFile(path, "native-json-file-missing");
            return ReadJson<T>(File.ReadAllBytes(path));
        }

        private static T ReadJson<T>(byte[] payload) where T : class
        {
            if (payload == null || payload.Length == 0)
            {
                throw new InvalidOperationException(
                    "native-json-payload-empty");
            }

            T? value;
            try
            {
                value = JsonUtility.FromJson<T>(
                    Encoding.UTF8.GetString(payload));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "native-json-invalid:" +
                    NormalizeMessage(exception.Message));
            }

            return value ?? throw new InvalidOperationException(
                "native-json-null");
        }

        private static byte[] SerializeJson<T>(T value) where T : class
        {
            return Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(value, true) + "\n");
        }

        private static void WriteReplaceFile(string path, byte[] payload)
        {
            RequirePathComponentsNotReparse(
                path,
                true,
                "native-replace-path-invalid");
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException(
                    "native-replace-path-invalid");
            }

            Directory.CreateDirectory(directory);
            RequireRegularDirectory(
                directory,
                "native-replace-directory-invalid");
            if (File.Exists(path))
            {
                RequireRegularFile(
                    path,
                    "native-replace-target-invalid");
            }
            string temporaryPath = path + "." +
                Guid.NewGuid().ToString("N") + ".replace.tmp";
            RequirePathComponentsNotReparse(
                temporaryPath,
                true,
                "native-replace-temporary-path-invalid");
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

                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                RequireRegularFile(path, "native-replace-target-invalid");

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
                boundaryPath,
                Path.Combine(
                    repositoryRoot,
                    "BuildArtifacts",
                    "WP-0002",
                    "unity-gates"),
                Path.Combine(
                    repositoryRoot,
                    NativeOutputRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)),
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

        private static string CompleteNativeGate(
            ProjectBoundary boundary,
            string gateId,
            string sourceSha256,
            string command,
            string result,
            string startedAt)
        {
            EnsureNativeOutputRoot(boundary);
            string invocationId = Guid.NewGuid().ToString("N");
            string completedAt = DateTime.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture);
            string evidenceDirectory = ResolveNativeOutputPath(
                boundary,
                "gate-evidence");
            Directory.CreateDirectory(evidenceDirectory);
            RequireRegularDirectory(
                evidenceDirectory,
                "native-gate-evidence-directory-invalid");
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
            byte[] payload = SerializeJson(record);
            WriteImmutableFile(
                Path.Combine(
                    evidenceDirectory,
                    gateId + "-" + invocationId + ".json"),
                payload);
            string line = "WP0002_GATE_COMPLETED " + gateId + " " +
                          sourceSha256 + " " + result;
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
            RequirePathComponentsNotReparse(
                path,
                true,
                "WP0002_EVIDENCE_PATH_NOT_REGULAR");
            if (File.Exists(path))
            {
                RequireRegularFile(
                    path,
                    "WP0002_EVIDENCE_RECORD_NOT_REGULAR");
                VerifyExactFile(path, payload);
                return;
            }

            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") +
                                   ".tmp";
            RequirePathComponentsNotReparse(
                temporaryPath,
                true,
                "WP0002_EVIDENCE_TEMP_PATH_NOT_REGULAR");
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
                    RequireRegularFile(
                        path,
                        "WP0002_EVIDENCE_RECORD_NOT_REGULAR");
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
            RequireRegularFile(
                path,
                "WP0002_EVIDENCE_RECORD_NOT_REGULAR");
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

        private static string ComputeSha256(byte[] payload)
        {
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(payload);
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

                bool passed = RequiredTestGatePassed(
                    result.PassCount,
                    result.FailCount,
                    result.InconclusiveCount,
                    result.SkipCount);
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
            discovered = 0;
            failure = "invalid-root";
            if (root == null || root.Test == null)
            {
                return false;
            }

            int leafCount = 0;
            int assemblyNodeCount = 0;
            var assemblies = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<ITestResult>();
            pending.Push(root);
            while (pending.Count != 0)
            {
                ITestResult current = pending.Pop();
                if (current.Test is TestAssembly testAssembly)
                {
                    assemblyNodeCount++;
                    string name = NormalizeAssemblyName(testAssembly.Name);
                    assemblies.Add(name);
                    if (string.Equals(
                            name,
                            expectedAssembly,
                            StringComparison.Ordinal))
                    {
                        discovered = testAssembly.TestCaseCount;
                    }
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
            if (assemblyNodeCount != 1 ||
                assemblies.Count != 1 ||
                !containsExpectedAssembly)
            {
                failure = "assembly-mismatch";
                return false;
            }

            if (discovered <= 0 ||
                leafCount != discovered ||
                accounted != discovered)
            {
                failure = "count-mismatch";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static string NormalizeAssemblyName(string name)
        {
            return name.EndsWith(
                    ".dll",
                    StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4)
                : name;
        }

        private static int ResultCount(ITestResult result)
        {
            return result.PassCount + result.FailCount + result.SkipCount +
                   result.InconclusiveCount;
        }

        private static bool RequiredTestGatePassed(
            int passCount,
            int failCount,
            int inconclusiveCount,
            int skipCount)
        {
            return passCount > 0 &&
                   failCount == 0 &&
                   inconclusiveCount == 0 &&
                   skipCount == 0;
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
                string boundaryPath,
                string evidenceDirectory,
                string nativeOutputRoot,
                string pendingTestGatePath)
            {
                RepositoryRoot = repositoryRoot;
                DispatcherSourcePath = dispatcherSourcePath;
                BoundaryPath = boundaryPath;
                EvidenceDirectory = evidenceDirectory;
                NativeOutputRoot = nativeOutputRoot;
                PendingTestGatePath = pendingTestGatePath;
            }

            internal string RepositoryRoot { get; }
            internal string DispatcherSourcePath { get; }
            internal string BoundaryPath { get; }
            internal string EvidenceDirectory { get; }
            internal string NativeOutputRoot { get; }
            internal string PendingTestGatePath { get; }
        }

        private readonly struct NativeSourceIdentity
        {
            internal NativeSourceIdentity(
                string dispatcherSha256,
                string buildProfileSha256,
                string sceneSha256,
                string sourceCommit,
                string sourceTreeSha256)
            {
                DispatcherSha256 = dispatcherSha256;
                BuildProfileSha256 = buildProfileSha256;
                SceneSha256 = sceneSha256;
                SourceCommit = sourceCommit;
                SourceTreeSha256 = sourceTreeSha256;
            }

            internal string DispatcherSha256 { get; }
            internal string BuildProfileSha256 { get; }
            internal string SceneSha256 { get; }
            internal string SourceCommit { get; }
            internal string SourceTreeSha256 { get; }
        }

        private enum NativeGitRead
        {
            OriginMainCommit,
            ReceiptIntroductions,
            IntroductionParents,
            ControlChangedPaths,
            ReceiptAtIntroduction,
            ReceiptAtOriginMain,
            PredecessorReceiptAtOriginMain,
            IntroductionAncestorOfSource
        }

        private enum NativeBuildScheduleState
        {
            None,
            Pending,
            Running,
            Succeeded,
            Failed
        }

        private sealed class ScheduledNativeBuild
        {
            internal ScheduledNativeBuild(
                ProjectBoundary boundary,
                NativeSourceIdentity source,
                string startedAt)
            {
                Boundary = boundary;
                Source = source;
                StartedAt = startedAt;
            }

            internal ProjectBoundary Boundary { get; }
            internal NativeSourceIdentity Source { get; }
            internal string StartedAt { get; }
        }

        private readonly struct NativeGitReadResult
        {
            internal NativeGitReadResult(
                int exitCode,
                byte[] standardOutput,
                string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            internal int ExitCode { get; }
            internal byte[] StandardOutput { get; }
            internal string StandardError { get; }
        }

        private sealed class TrustedNativeBuild
        {
            internal TrustedNativeBuild(
                string editorSessionNonce,
                int editorProcessId,
                string editorProcessStartedAt,
                string buildGuid,
                string sourceCommit,
                string sourceTreeSha256,
                string dispatcherSha256,
                string buildProfileSha256,
                string sceneSha256,
                string runDirectory,
                string executablePath,
                string executableSha256,
                string buildArtifactSha256,
                string buildManifestSha256)
            {
                EditorSessionNonce = editorSessionNonce;
                EditorProcessId = editorProcessId;
                EditorProcessStartedAt = editorProcessStartedAt;
                BuildGuid = buildGuid;
                SourceCommit = sourceCommit;
                SourceTreeSha256 = sourceTreeSha256;
                DispatcherSha256 = dispatcherSha256;
                BuildProfileSha256 = buildProfileSha256;
                SceneSha256 = sceneSha256;
                RunDirectory = runDirectory;
                ExecutablePath = executablePath;
                ExecutableSha256 = executableSha256;
                BuildArtifactSha256 = buildArtifactSha256;
                BuildManifestSha256 = buildManifestSha256;
            }

            internal string EditorSessionNonce { get; }
            internal int EditorProcessId { get; }
            internal string EditorProcessStartedAt { get; }
            internal string BuildGuid { get; }
            internal string SourceCommit { get; }
            internal string SourceTreeSha256 { get; }
            internal string DispatcherSha256 { get; }
            internal string BuildProfileSha256 { get; }
            internal string SceneSha256 { get; }
            internal string RunDirectory { get; }
            internal string ExecutablePath { get; }
            internal string ExecutableSha256 { get; }
            internal string BuildArtifactSha256 { get; }
            internal string BuildManifestSha256 { get; }
        }

        private sealed class TrustedNativeRun
        {
            internal TrustedNativeRun(
                string editorSessionNonce,
                int editorProcessId,
                string editorProcessStartedAt,
                Process process,
                string requestNonce,
                string processStartedAt,
                string runDirectory,
                string reportRelativePath,
                string buildManifestSha256,
                string runManifestSha256,
                string buildIdentitySha256,
                string requestSha256,
                string executableSha256,
                string buildArtifactSha256)
            {
                EditorSessionNonce = editorSessionNonce;
                EditorProcessId = editorProcessId;
                EditorProcessStartedAt = editorProcessStartedAt;
                Process = process;
                RequestNonce = requestNonce;
                ProcessStartedAt = processStartedAt;
                RunDirectory = runDirectory;
                ReportRelativePath = reportRelativePath;
                BuildManifestSha256 = buildManifestSha256;
                RunManifestSha256 = runManifestSha256;
                BuildIdentitySha256 = buildIdentitySha256;
                RequestSha256 = requestSha256;
                ExecutableSha256 = executableSha256;
                BuildArtifactSha256 = buildArtifactSha256;
            }

            internal string EditorSessionNonce { get; }
            internal int EditorProcessId { get; }
            internal string EditorProcessStartedAt { get; }
            internal Process Process { get; }
            internal string RequestNonce { get; }
            internal string ProcessStartedAt { get; }
            internal string RunDirectory { get; }
            internal string ReportRelativePath { get; }
            internal string BuildManifestSha256 { get; }
            internal string RunManifestSha256 { get; }
            internal string BuildIdentitySha256 { get; }
            internal string RequestSha256 { get; }
            internal string ExecutableSha256 { get; }
            internal string BuildArtifactSha256 { get; }
        }

        private sealed class NativeProcessCleanup
        {
            internal NativeProcessCleanup(Process process, string failure)
            {
                Process = process;
                Failure = failure;
                QuarantinedAt = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture);
            }

            internal Process Process { get; }
            internal string Failure { get; set; }
            internal string QuarantinedAt { get; }
        }

        private readonly struct NativeProcessResult
        {
            internal NativeProcessResult(
                int exitCode,
                string standardOutput,
                string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            internal int ExitCode { get; }
            internal string StandardOutput { get; }
            internal string StandardError { get; }
        }

        [Serializable]
        private sealed class NativeGateAuthorizationReceipt
        {
            public string receipt_id = string.Empty;
            public string issued_by = string.Empty;
            public string issuer_role = string.Empty;
            public string receipt_kind = string.Empty;
            public string accepted_commit = string.Empty;
            public bool @sealed;
        }

        [Serializable]
        private sealed class NativeBuildManifest
        {
            public int schema_version;
            public string contract_id = string.Empty;
            public string build_guid = string.Empty;
            public string source_commit = string.Empty;
            public string source_tree_sha256 = string.Empty;
            public string dispatcher_sha256 = string.Empty;
            public string build_profile_sha256 = string.Empty;
            public string scene_sha256 = string.Empty;
            public string unity_version = string.Empty;
            public string unity_binary_sha256 = string.Empty;
            public string xcode_version = string.Empty;
            public string xcode_build_version = string.Empty;
            public string xcodebuild_binary_sha256 = string.Empty;
            public string macos_sdk_version = string.Empty;
            public string build_target = string.Empty;
            public string architecture = string.Empty;
            public string scripting_backend = string.Empty;
            public bool development_build;
            public int width;
            public int height;
            public string run_directory = string.Empty;
            public string player_relative_path = string.Empty;
            public string executable_relative_path = string.Empty;
            public string executable_sha256 = string.Empty;
            public string build_artifact_sha256 = string.Empty;
            public long total_size_bytes;
            public string built_at = string.Empty;
        }

        [Serializable]
        private sealed class NativeBuildPointer
        {
            public int schema_version;
            public string contract_id = string.Empty;
            public string build_guid = string.Empty;
            public string run_directory = string.Empty;
            public string build_manifest_relative_path = string.Empty;
            public string build_manifest_sha256 = string.Empty;
        }

        [Serializable]
        private sealed class NativeBuildIdentity
        {
            public int schema_version;
            public string identity_id = string.Empty;
            public string source_commit = string.Empty;
            public string source_tree_sha256 = string.Empty;
            public string build_guid = string.Empty;
            public string unity_version = string.Empty;
            public string executable_sha256 = string.Empty;
            public bool development_build;
        }

        [Serializable]
        private sealed class NativePerformanceRequest
        {
            public int schema_version;
            public string contract_id = string.Empty;
            public string request_nonce = string.Empty;
            public string expected_source_commit = string.Empty;
            public string expected_source_tree_sha256 = string.Empty;
            public string expected_build_identity_sha256 = string.Empty;
            public string expected_build_guid = string.Empty;
            public string expected_executable_sha256 = string.Empty;
        }

        [Serializable]
        private sealed class NativeRunManifest
        {
            public int schema_version;
            public string contract_id = string.Empty;
            public string request_nonce = string.Empty;
            public string source_commit = string.Empty;
            public string source_tree_sha256 = string.Empty;
            public string dispatcher_sha256 = string.Empty;
            public string build_profile_sha256 = string.Empty;
            public string build_guid = string.Empty;
            public string build_artifact_sha256 = string.Empty;
            public string executable_sha256 = string.Empty;
            public string build_manifest_sha256 = string.Empty;
            public string build_identity_sha256 = string.Empty;
            public string request_sha256 = string.Empty;
            public string run_directory = string.Empty;
            public string report_relative_path = string.Empty;
            public int process_id;
            public string process_started_at = string.Empty;
            public string started_at = string.Empty;
        }

        [Serializable]
        private sealed class NativeRunPointer
        {
            public int schema_version;
            public string contract_id = string.Empty;
            public string request_nonce = string.Empty;
            public string run_manifest_relative_path = string.Empty;
            public string run_manifest_sha256 = string.Empty;
        }

        [Serializable]
        private sealed class NativePerformanceReport
        {
            public int schema_version;
            public string report_kind = string.Empty;
            public string status = string.Empty;
            public string failure_code = string.Empty;
            public string request_nonce = string.Empty;
            public string request_sha256 = string.Empty;
            public string run_started_utc = string.Empty;
            public string report_generated_utc = string.Empty;
            public double requested_warmup_seconds;
            public double actual_warmup_seconds;
            public NativeReportBuild build = new NativeReportBuild();
            public NativeEnvironment environment = new NativeEnvironment();
            public NativePerformancePhase paused =
                new NativePerformancePhase();
            public NativePerformancePhase representative_unpaused =
                new NativePerformancePhase();
            public NativePerformancePhase city_garage_cycles =
                new NativePerformancePhase();
            public NativeCycles cycles = new NativeCycles();
            public NativeTopology topology = new NativeTopology();
            public NativePausedRetention paused_retention =
                new NativePausedRetention();
            public NativeAcceptance acceptance = new NativeAcceptance();
        }

        [Serializable]
        private sealed class NativeReportBuild
        {
            public string source_commit = string.Empty;
            public string source_tree_sha256 = string.Empty;
            public string build_identity_sha256 = string.Empty;
            public string executable_sha256 = string.Empty;
            public string application_build_guid = string.Empty;
            public string unity_version = string.Empty;
            public bool enable_il2cpp;
            public string process_architecture = string.Empty;
            public bool arm64_process;
            public bool development_build;
            public bool request_runtime_identity_matched;
        }

        [Serializable]
        private sealed class NativeEnvironment
        {
            public int requested_width;
            public int requested_height;
            public int actual_width;
            public int actual_height;
            public string actual_full_screen_mode = string.Empty;
            public bool exact_resolution_verified;
        }

        [Serializable]
        private sealed class NativePerformancePhase
        {
            public string phase_id = string.Empty;
            public double started_at_realtime_seconds;
            public double ended_at_realtime_seconds;
            public double measured_seconds;
            public int frame_sample_count;
            public bool frame_sample_capacity_exceeded;
            public double gc_allocated_average_bytes;
            public long gc_allocated_p95_bytes;
            public long gc_allocated_max_bytes;
            public double frame_time_average_ms;
            public double frame_time_p95_ms;
            public double frame_time_p99_ms;
            public double frame_time_max_ms;
            public int simulation_tick_sample_count;
            public bool simulation_tick_capacity_exceeded;
            public double simulation_tick_average_ms;
            public double simulation_tick_p95_ms;
            public double simulation_tick_max_ms;
        }

        [Serializable]
        private sealed class NativeCycles
        {
            public int requested_cycles;
            public int completed_cycles;
            public bool canonical_state_unchanged;
            public string canonical_sha256_before = string.Empty;
            public string canonical_sha256_after = string.Empty;
            public bool topology_stable_at_all_checkpoints;
            public bool managed_heap_monotonic_growth_detected;
            public bool unity_allocated_monotonic_growth_detected;
            public bool unity_reserved_monotonic_growth_detected;
            public bool no_monotonic_memory_growth;
            public int post_cycle_first_submit_command_delta;
            public int post_cycle_duplicate_submit_command_delta;
            public bool post_cycle_exactly_one_action;
            public NativeMemoryCheckpoint[] memory_checkpoints =
                Array.Empty<NativeMemoryCheckpoint>();
        }

        [Serializable]
        private sealed class NativeMemoryCheckpoint
        {
            public int completed_cycles;
            public long managed_heap_bytes;
            public long unity_allocated_bytes;
            public long unity_reserved_bytes;
        }

        [Serializable]
        private sealed class NativeTopology
        {
            public int initial_owned_unity_objects;
            public int final_owned_unity_objects;
            public int initial_visual_elements;
            public int final_visual_elements;
            public int initial_bindings;
            public int final_bindings;
            public int initial_registered_callbacks;
            public int final_registered_callbacks;
            public bool exact_identity_set_retained;
            public int initial_ui_documents;
            public int final_ui_documents;
            public bool exact_ui_document_set_retained;
            public int initial_cameras;
            public int final_cameras;
            public bool exact_camera_set_retained;
            public int initial_audio_listeners;
            public int final_audio_listeners;
            public bool exact_audio_listener_set_retained;
        }

        [Serializable]
        private sealed class NativePausedRetention
        {
            public long managed_heap_bytes_before;
            public long managed_heap_bytes_after;
            public long managed_heap_growth_bytes;
            public double managed_heap_growth_percent;
            public bool managed_heap_not_grown;
            public long unity_allocated_bytes_before;
            public long unity_allocated_bytes_after;
            public long unity_allocated_growth_bytes;
            public double unity_allocated_growth_percent;
            public bool unity_allocated_not_grown;
            public long unity_reserved_bytes_before;
            public long unity_reserved_bytes_after;
            public long unity_reserved_growth_bytes;
            public double unity_reserved_growth_percent;
            public bool unity_reserved_not_grown;
            public bool no_retained_memory_growth;
            public bool canonical_state_unchanged;
            public string canonical_sha256_before = string.Empty;
            public string canonical_sha256_after = string.Empty;
            public bool exact_topology_retained;
        }

        [Serializable]
        private sealed class NativeAcceptance
        {
            public bool paused_p95_zero_bytes_per_frame;
            public bool representative_unpaused_p95_zero_bytes_per_frame;
            public bool representative_unpaused_average_below_1024_bytes_per_frame;
            public bool all_sample_buffers_populated_without_overflow;
            public bool exact_100_cycles;
            public bool stable_retained_topology;
            public bool paused_no_retained_memory_growth;
            public bool cycles_no_monotonic_memory_growth;
            public bool post_cycle_exactly_one_action;
            public bool exact_2560x1600;
            public bool passed;
        }

        [Serializable]
        private sealed class NativePerformanceEvidence
        {
            public int schema_version;
            public string contract_id = string.Empty;
            public string request_nonce = string.Empty;
            public string source_commit = string.Empty;
            public string source_tree_sha256 = string.Empty;
            public string dispatcher_sha256 = string.Empty;
            public string build_profile_sha256 = string.Empty;
            public string build_guid = string.Empty;
            public string build_artifact_sha256 = string.Empty;
            public string executable_sha256 = string.Empty;
            public string build_manifest_sha256 = string.Empty;
            public string build_identity_sha256 = string.Empty;
            public string request_sha256 = string.Empty;
            public string run_manifest_sha256 = string.Empty;
            public string runtime_report_sha256 = string.Empty;
            public string runtime_report_relative_path = string.Empty;
            public int width;
            public int height;
            public double warmup_seconds;
            public double paused_seconds;
            public double representative_unpaused_seconds;
            public int city_garage_cycles;
            public bool accepted;
            public string collected_at = string.Empty;
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
