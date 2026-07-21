#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AtomicLandPirate.Simulation.LastBearing;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityProfiler = UnityEngine.Profiling.Profiler;

namespace AtomicLandPirate.Presentation.LastBearing.Performance
{
    public static class LastBearingNativePerformanceRuntimeTarget
    {
        public static bool TrySelectCanonicalController(
            LastBearingGameController[] controllers,
            out LastBearingGameController? controller,
            out string error)
        {
            controller = null;
            error = string.Empty;
            if (controllers == null || controllers.Length != 1)
            {
                error = "exactly one Last Bearing controller is required";
                return false;
            }

            LastBearingGameController candidate = controllers[0];
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !string.Equals(
                    candidate.gameObject.name,
                    LastBearingGameController.RuntimeRootName,
                    StringComparison.Ordinal))
            {
                error = "Last Bearing controller is not the active canonical root";
                return false;
            }

            controller = candidate;
            return true;
        }
    }

    public static class LastBearingNativePerformanceBootstrap
    {
        public const string RuntimeObjectName =
            "WP-0002 Native Performance Harness";

#if ENABLE_IL2CPP
        public const bool CompiledWithIl2Cpp = true;
#else
        public const bool CompiledWithIl2Cpp = false;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenExplicitlyRequested()
        {
#if UNITY_EDITOR
            return;
#else
            string[] arguments = Environment.GetCommandLineArgs();
            if (!LastBearingNativePerformanceLaunch.HasActivationArgument(arguments))
            {
                return;
            }

            if (!LastBearingNativePerformanceLaunch
                    .TryDeriveRunDirectoryFromMacPlayerDataPath(
                        Application.dataPath,
                        out string allowedRunDirectory,
                        out string pathError))
            {
                Debug.LogError(
                    "WP0002_NATIVE_PERFORMANCE_REQUEST_REJECTED " + pathError);
                Application.Quit(2);
                return;
            }

            if (!LastBearingNativePerformanceLaunch.TryLoad(
                    arguments,
                    CompiledWithIl2Cpp,
                    RuntimeInformation.ProcessArchitecture,
                    Application.buildGUID,
                    Application.unityVersion,
                    Debug.isDebugBuild,
                    allowedRunDirectory,
                    out LastBearingNativePerformanceLaunch? launch,
                    out string error))
            {
                Debug.LogError(
                    "WP0002_NATIVE_PERFORMANCE_REQUEST_REJECTED " + error);
                Application.Quit(2);
                return;
            }

            var root = new GameObject(RuntimeObjectName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            var harness = root.AddComponent<LastBearingNativePerformanceHarness>();
            harness.Configure(launch!);
#endif
        }
    }

    [DisallowMultipleComponent]
    public sealed class LastBearingNativePerformanceHarness :
        MonoBehaviour,
        ILastBearingSimulationTickPerformanceObserver
    {
        private const int MaximumControllerWaitFrames = 600;
        private const int MaximumResolutionWaitFrames = 600;
        private const double MaximumExpectedFrameRate = 240d;
        private const double MaximumExpectedSimulationRate = 12d;

        private readonly LastBearingNativePerformanceMemoryCheckpoint[]
            _cycleMemoryCheckpoints =
                new LastBearingNativePerformanceMemoryCheckpoint[5];
        private LastBearingNativePerformanceLaunch? _launch;
        private LastBearingNativePerformanceDurations? _durations;
        private ILastBearingNativePerformanceClock? _clock;
        private LastBearingNativePerformanceSchedule? _schedule;
        private LastBearingGameController? _controller;
        private LastBearingFieldDeskPerformanceTopology? _initialTopology;
        private LastBearingFieldDeskPerformanceTopology? _finalTopology;
        private LastBearingNativePerformanceSampleBuffer? _pausedSamples;
        private LastBearingNativePerformanceSampleBuffer? _unpausedSamples;
        private LastBearingNativePerformanceSampleBuffer? _cycleSamples;
        private LastBearingNativePerformanceSampleBuffer? _activeSamples;
        private ProfilerRecorder _gcRecorder;
        private bool _gcRecorderRunning;
        private bool _recorderInvalidObserved;
        private bool _prepared;
        private bool _finished;
        private bool _topologyStable = true;
        private bool _resolutionRequested;
        private bool _resolutionVerified;
        private string _runStartedUtc = string.Empty;
        private int _resolutionWaitFrames;
        private double _warmupStartedAt;
        private double _warmupEndedAt;
        private int _controllerWaitFrames;
        private int _nextCycleCheckpoint = 25;
        private int _cycleCheckpointCount;
        private string _cycleCanonicalBefore = string.Empty;
        private string _cycleCanonicalAfter = string.Empty;
        private string _pausedCanonicalBefore = string.Empty;
        private string _pausedCanonicalAfter = string.Empty;
        private int _postCycleFirstSubmitCommandDelta;
        private int _postCycleDuplicateSubmitCommandDelta;
        private bool _postCycleExactlyOneAction;
        private LastBearingNativePerformanceMemoryCheckpoint
            _retentionBefore;
        private LastBearingNativePerformanceMemoryCheckpoint
            _retentionAfter;
        private UIDocument[] _initialUiDocuments = Array.Empty<UIDocument>();
        private UIDocument[] _finalUiDocuments = Array.Empty<UIDocument>();
        private Camera[] _initialCameras = Array.Empty<Camera>();
        private Camera[] _finalCameras = Array.Empty<Camera>();
        private AudioListener[] _initialAudioListeners =
            Array.Empty<AudioListener>();
        private AudioListener[] _finalAudioListeners =
            Array.Empty<AudioListener>();

        public void Configure(LastBearingNativePerformanceLaunch launch)
        {
            if (_launch != null)
            {
                throw new InvalidOperationException(
                    "native performance harness is already configured");
            }

            _launch = launch ?? throw new ArgumentNullException(nameof(launch));
            _durations = LastBearingNativePerformanceDurations.Production();
            _clock = new LastBearingUnityRealtimeClock();
            _runStartedUtc = DateTime.UtcNow.ToString("O");
        }

        public void RecordSimulationTick(long stopwatchTicks)
        {
            _activeSamples?.RecordSimulationTick(stopwatchTicks);
        }

        private void Update()
        {
            if (_finished || _launch == null || _durations == null || _clock == null)
            {
                return;
            }

            try
            {
                if (!_prepared)
                {
                    TryPrepare();
                    return;
                }

                if (!LastBearingNativePerformanceEnvironment.IsExactResolution(
                        Screen.width,
                        Screen.height))
                {
                    Fail("native-resolution-drifted-after-verification");
                    return;
                }

                if (_recorderInvalidObserved)
                {
                    Fail("gc-profiler-recorder-became-invalid");
                    return;
                }

                bool isPaused =
                    _controller?.ReadModel?.PauseCause != PauseCause.None;
                LastBearingNativePerformanceAction action =
                    _schedule!.Advance(isPaused);
                Apply(action);
            }
            catch (Exception exception)
            {
                Fail("runtime-exception-" + exception.GetType().Name);
            }
        }

        private void LateUpdate()
        {
            if (!_gcRecorderRunning || _activeSamples == null)
            {
                return;
            }

            if (!_gcRecorder.Valid)
            {
                _recorderInvalidObserved = true;
                return;
            }

            _activeSamples.RecordFrame(
                _gcRecorder.LastValue,
                Time.unscaledDeltaTime * 1000d);
        }

        private void OnDestroy()
        {
            StopMeasurement();
        }

        private void TryPrepare()
        {
            if (!_resolutionRequested)
            {
                Screen.SetResolution(
                    LastBearingNativePerformanceEnvironment.RequiredWidth,
                    LastBearingNativePerformanceEnvironment.RequiredHeight,
                    FullScreenMode.Windowed);
                _resolutionRequested = true;
                return;
            }

            if (!LastBearingNativePerformanceEnvironment.IsExactResolution(
                    Screen.width,
                    Screen.height))
            {
                _resolutionWaitFrames++;
                if (_resolutionWaitFrames >= MaximumResolutionWaitFrames)
                {
                    Fail("native-resolution-did-not-reach-2560x1600");
                }

                return;
            }

            _resolutionVerified = true;
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    LastBearingBootstrap.SceneName,
                    StringComparison.Ordinal))
            {
                Fail("active-scene-is-not-last-bearing");
                return;
            }

            if (_controller == null)
            {
                LastBearingGameController[] controllers =
                    UnityEngine.Object.FindObjectsByType<
                        LastBearingGameController>(
                        FindObjectsInactive.Include);
                if (controllers.Length == 0)
                {
                    _controllerWaitFrames++;
                    if (_controllerWaitFrames >= MaximumControllerWaitFrames)
                    {
                        Fail("last-bearing-controller-not-found");
                    }

                    return;
                }

                if (!LastBearingNativePerformanceRuntimeTarget
                        .TrySelectCanonicalController(
                            controllers,
                            out _controller,
                            out _))
                {
                    Fail(
                        controllers.Length == 1
                            ? "last-bearing-controller-is-not-canonical"
                            : "multiple-last-bearing-controllers-found");
                    return;
                }

                if (_controller!.gameObject.scene !=
                    SceneManager.GetActiveScene())
                {
                    Fail("last-bearing-controller-is-not-in-active-scene");
                    return;
                }
            }

            PrepareRepresentativeCity(_controller);
            LastBearingFieldDesk? desk = _controller.FieldDesk;
            if (desk == null || !desk.IsOperational || !desk.OwnsCityOverview)
            {
                Fail("field-desk-not-operational-in-city-overview");
                return;
            }

            _initialTopology = desk.CapturePerformanceTopology();
            if (_initialTopology.BindingCount != 18 ||
                _initialTopology.RegisteredCallbackCount != 19 ||
                _initialTopology.OwnedUnityObjectCount != 3)
            {
                Fail("field-desk-topology-does-not-match-vgr13-contract");
                return;
            }

            _initialUiDocuments =
                UnityEngine.Object.FindObjectsByType<UIDocument>(
                    FindObjectsInactive.Include);
            _initialCameras =
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include);
            _initialAudioListeners =
                UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include);

            LastBearingNativePerformanceDurations durations = _durations!;
            ILastBearingNativePerformanceClock clock = _clock!;
            AllocateSampleBuffers(durations);
            _schedule = new LastBearingNativePerformanceSchedule(
                clock,
                durations);
            _prepared = true;
            Apply(_schedule.Start());
        }

        private static void PrepareRepresentativeCity(
            LastBearingGameController controller)
        {
            if (controller.HasActiveGame)
            {
                controller.ReturnToTitle();
            }

            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            controller.ManipulateCityGrammarPrimary();
            controller.ShowCityOverview();
            controller.FieldDesk?.Refresh(force: true);
        }

        private void AllocateSampleBuffers(
            LastBearingNativePerformanceDurations durations)
        {
            _pausedSamples = CreateSamples(
                "paused-unchanged-city",
                durations.PausedSeconds);
            _unpausedSamples = CreateSamples(
                "representative-unpaused-city",
                durations.RepresentativeUnpausedSeconds);
            double cycleSeconds =
                durations.CityGarageCycles *
                durations.CycleHalfDwellSeconds *
                2d + 10d;
            _cycleSamples = CreateSamples("city-garage-cycles", cycleSeconds);
        }

        private static LastBearingNativePerformanceSampleBuffer CreateSamples(
            string phaseId,
            double seconds)
        {
            int frameCapacity = checked(
                (int)Math.Ceiling(seconds * MaximumExpectedFrameRate) + 512);
            int simulationCapacity = checked(
                (int)Math.Ceiling(seconds * MaximumExpectedSimulationRate) + 64);
            return new LastBearingNativePerformanceSampleBuffer(
                phaseId,
                frameCapacity,
                simulationCapacity);
        }

        private void Apply(LastBearingNativePerformanceAction action)
        {
            if (action == LastBearingNativePerformanceAction.None)
            {
                return;
            }

            LastBearingGameController controller = _controller!;
            switch (action)
            {
                case LastBearingNativePerformanceAction.BeginWarmup:
                    WarmGcRecorder();
                    _warmupStartedAt = _clock!.NowSeconds;
                    break;

                case LastBearingNativePerformanceAction
                    .RequestPauseForPausedMeasurement:
                    _warmupEndedAt = _clock!.NowSeconds;
                    RequestExplicitPause(controller);
                    break;

                case LastBearingNativePerformanceAction.BeginPausedMeasurement:
                    if (controller.ReadModel?.PauseCause != PauseCause.Explicit)
                    {
                        throw new InvalidOperationException(
                            "paused phase did not reach explicit pause");
                    }

                    _pausedCanonicalBefore = controller.CanonicalHash;
                    // Retention snapshots must compare the same active
                    // recorder lifecycle rather than its start/stop residue.
                    BeginMeasurement(_pausedSamples!);
                    _retentionBefore = CaptureMemoryCheckpoint(
                        completedCycles: 0,
                        forceFullCollection: true);
                    break;

                case LastBearingNativePerformanceAction
                    .EndPausedMeasurementAndRequestResume:
                    _retentionAfter = CaptureMemoryCheckpoint(
                        completedCycles: 0,
                        forceFullCollection: true);
                    StopMeasurement();
                    _pausedCanonicalAfter = controller.CanonicalHash;
                    ValidateCycleTopology();
                    RequestResume(controller);
                    break;

                case LastBearingNativePerformanceAction
                    .BeginRepresentativeUnpausedMeasurement:
                    if (!controller.IsExactFieldDeskCityOverview)
                    {
                        throw new InvalidOperationException(
                            "representative pass did not retain city overview");
                    }

                    BeginMeasurement(_unpausedSamples!);
                    break;

                case LastBearingNativePerformanceAction
                    .EndRepresentativeUnpausedMeasurementAndRequestPause:
                    StopMeasurement();
                    RequestExplicitPause(controller);
                    break;

                case LastBearingNativePerformanceAction.BeginCityGarageCycles:
                    _cycleCanonicalBefore = controller.CanonicalHash;
                    _cycleCheckpointCount = 0;
                    _nextCycleCheckpoint = 25;
                    // Every cycle checkpoint includes the same live recorder.
                    BeginMeasurement(_cycleSamples!);
                    CaptureCycleCheckpoint(0);
                    break;

                case LastBearingNativePerformanceAction.ShowGarage:
                    controller.OpenGarageBay();
                    break;

                case LastBearingNativePerformanceAction.ShowCity:
                    controller.ShowCityOverview();
                    ValidateCycleTopology();
                    int completedCycles =
                        _schedule!.CompletedCityGarageCycles;
                    if (completedCycles == _nextCycleCheckpoint)
                    {
                        CaptureCycleCheckpoint(completedCycles);
                        _nextCycleCheckpoint += 25;
                    }

                    break;

                case LastBearingNativePerformanceAction
                    .EndCityGarageCyclesAndSubmitResume:
                    StopMeasurement();
                    _cycleCanonicalAfter = controller.CanonicalHash;
                    ValidateCycleTopology();
                    LastBearingFieldDesk? postCycleDesk = controller.FieldDesk;
                    if (postCycleDesk == null ||
                        !postCycleDesk.TrySubmitPauseTwiceForNativePerformanceGate(
                            out _postCycleFirstSubmitCommandDelta,
                            out _postCycleDuplicateSubmitCommandDelta))
                    {
                        throw new InvalidOperationException(
                            "post-cycle Field Desk action was not exactly once");
                    }

                    _postCycleExactlyOneAction = true;
                    break;

                case LastBearingNativePerformanceAction.Complete:
                    _finalTopology =
                        controller.FieldDesk?.CapturePerformanceTopology();
                    _topologyStable &=
                        controller.FieldDesk?.MatchesPerformanceTopology(
                            _initialTopology!) == true;
                    _finalUiDocuments =
                        UnityEngine.Object.FindObjectsByType<UIDocument>(
                            FindObjectsInactive.Include);
                    _finalCameras =
                        UnityEngine.Object.FindObjectsByType<Camera>(
                            FindObjectsInactive.Include);
                    _finalAudioListeners =
                        UnityEngine.Object.FindObjectsByType<AudioListener>(
                            FindObjectsInactive.Include);
                    Complete();
                    break;
            }
        }

        private static void RequestExplicitPause(
            LastBearingGameController controller)
        {
            if (controller.ReadModel?.PauseCause == PauseCause.None)
            {
                controller.TogglePause();
            }
        }

        private static void RequestResume(LastBearingGameController controller)
        {
            if (controller.ReadModel?.PauseCause == PauseCause.Explicit)
            {
                controller.TogglePause();
            }
        }

        private void BeginMeasurement(
            LastBearingNativePerformanceSampleBuffer samples)
        {
            if (_gcRecorderRunning || _activeSamples != null)
            {
                throw new InvalidOperationException(
                    "a native performance measurement is already active");
            }

            _gcRecorder = StartGcRecorder();

            samples.StartedAtSeconds = _clock!.NowSeconds;
            _activeSamples = samples;
            _controller!.AttachSimulationTickPerformanceObserver(this);
            _gcRecorderRunning = true;
        }

        private static void WarmGcRecorder()
        {
            ProfilerRecorder recorder = StartGcRecorder();
            recorder.Stop();
            recorder.Dispose();
        }

        private static ProfilerRecorder StartGcRecorder()
        {
            ProfilerRecorderOptions options =
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached;
            ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                capacity: 1,
                options: options);
            if (!recorder.Valid)
            {
                recorder.Dispose();
                throw new InvalidOperationException(
                    "GC Allocated In Frame recorder is unavailable");
            }

            return recorder;
        }

        private void StopMeasurement()
        {
            _controller?.DetachSimulationTickPerformanceObserver(this);
            if (_activeSamples != null && _clock != null)
            {
                _activeSamples.EndedAtSeconds = _clock.NowSeconds;
            }

            if (_gcRecorderRunning)
            {
                _gcRecorder.Stop();
                _gcRecorder.Dispose();
            }

            _gcRecorderRunning = false;
            _activeSamples = null;
        }

        private void ValidateCycleTopology()
        {
            _topologyStable &=
                _controller?.FieldDesk?.MatchesPerformanceTopology(
                    _initialTopology!) == true;
        }

        private void CaptureCycleCheckpoint(int completedCycles)
        {
            if (_cycleCheckpointCount >= _cycleMemoryCheckpoints.Length)
            {
                return;
            }

            _cycleMemoryCheckpoints[_cycleCheckpointCount] =
                CaptureMemoryCheckpoint(completedCycles);
            _cycleCheckpointCount++;
        }

        private static LastBearingNativePerformanceMemoryCheckpoint
            CaptureMemoryCheckpoint(
                int completedCycles,
                bool forceFullCollection = false)
        {
            return new LastBearingNativePerformanceMemoryCheckpoint
            {
                completed_cycles = completedCycles,
                managed_heap_bytes = GC.GetTotalMemory(forceFullCollection),
                unity_allocated_bytes = UnityProfiler.GetTotalAllocatedMemoryLong(),
                unity_reserved_bytes = UnityProfiler.GetTotalReservedMemoryLong(),
            };
        }

        private void Complete()
        {
            StopMeasurement();
            LastBearingNativePerformanceReport report =
                BuildReport("completed", string.Empty);
            int exitCode = 0;
            if (!ApplyAcceptance(report, out string failureCode))
            {
                report.status = "failed";
                report.failure_code = failureCode;
                exitCode = 3;
            }

            WriteReportAndFinish(report, exitCode);
        }

        private void Fail(string failureCode)
        {
            if (_finished)
            {
                return;
            }

            StopMeasurement();
            WriteReportAndFinish(
                BuildReport("failed", failureCode),
                exitCode: 3);
        }

        private void WriteReportAndFinish(
            LastBearingNativePerformanceReport report,
            int exitCode)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            try
            {
                string json = JsonUtility.ToJson(report, prettyPrint: true) + "\n";
                WriteAtomically(_launch!.ReportPath, json);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "WP0002_NATIVE_PERFORMANCE_REPORT_WRITE_FAILED " +
                    exception.GetType().Name);
                exitCode = 4;
            }

            if (!Application.isEditor)
            {
                Application.Quit(exitCode);
            }
        }

        private LastBearingNativePerformanceReport BuildReport(
            string status,
            string failureCode)
        {
            LastBearingNativePerformanceBuildIdentity identity =
                _launch!.BuildIdentity;
            LastBearingNativePerformanceReport report = new()
            {
                status = status,
                failure_code = failureCode,
                request_nonce = _launch.Request.request_nonce,
                request_sha256 = _launch.RequestSha256,
                run_started_utc = _runStartedUtc,
                report_generated_utc = DateTime.UtcNow.ToString("O"),
                requested_warmup_seconds = _durations?.WarmupSeconds ?? 0d,
                actual_warmup_seconds = Math.Max(
                    0d,
                    _warmupEndedAt - _warmupStartedAt),
                build = new LastBearingNativePerformanceBuildProof
                {
                    source_commit = identity.source_commit,
                    source_tree_sha256 = identity.source_tree_sha256,
                    build_identity_sha256 = _launch.BuildIdentitySha256,
                    executable_sha256 = identity.executable_sha256,
                    application_build_guid = identity.build_guid,
                    unity_version = identity.unity_version,
                    enable_il2cpp =
                        LastBearingNativePerformanceBootstrap.CompiledWithIl2Cpp,
                    process_architecture =
                        RuntimeInformation.ProcessArchitecture.ToString(),
                    arm64_process =
                        RuntimeInformation.ProcessArchitecture == Architecture.Arm64,
                    development_build = Debug.isDebugBuild,
                    request_runtime_identity_matched = true,
                },
                environment = new LastBearingNativePerformanceEnvironmentProof
                {
                    requested_width =
                        LastBearingNativePerformanceEnvironment.RequiredWidth,
                    requested_height =
                        LastBearingNativePerformanceEnvironment.RequiredHeight,
                    actual_width = Screen.width,
                    actual_height = Screen.height,
                    actual_full_screen_mode = Screen.fullScreenMode.ToString(),
                    exact_resolution_verified =
                        _resolutionVerified &&
                        LastBearingNativePerformanceEnvironment.IsExactResolution(
                            Screen.width,
                            Screen.height),
                },
                paused = BuildPhase(_pausedSamples),
                representative_unpaused = BuildPhase(_unpausedSamples),
                city_garage_cycles = BuildPhase(_cycleSamples),
            };

            report.cycles = BuildCycleReport();
            report.topology = BuildTopologyReport();
            report.paused_retention = BuildRetentionReport();
            return report;
        }

        private static LastBearingNativePerformancePhaseReport BuildPhase(
            LastBearingNativePerformanceSampleBuffer? samples)
        {
            return samples == null
                ? new LastBearingNativePerformancePhaseReport()
                : samples.BuildReport();
        }

        private LastBearingNativePerformanceCycleReport BuildCycleReport()
        {
            var checkpoints =
                new LastBearingNativePerformanceMemoryCheckpoint[
                    _cycleCheckpointCount];
            Array.Copy(
                _cycleMemoryCheckpoints,
                checkpoints,
                _cycleCheckpointCount);
            bool managedMonotonicGrowth =
                LastBearingNativePerformanceMemoryTrend.HasMonotonicGrowth(
                checkpoints,
                LastBearingNativePerformanceMemoryMetric.ManagedHeap);
            bool allocatedMonotonicGrowth =
                LastBearingNativePerformanceMemoryTrend.HasMonotonicGrowth(
                checkpoints,
                LastBearingNativePerformanceMemoryMetric.UnityAllocated);
            bool reservedMonotonicGrowth =
                LastBearingNativePerformanceMemoryTrend.HasMonotonicGrowth(
                checkpoints,
                LastBearingNativePerformanceMemoryMetric.UnityReserved);
            return new LastBearingNativePerformanceCycleReport
            {
                requested_cycles = _durations?.CityGarageCycles ?? 0,
                completed_cycles =
                    _schedule?.CompletedCityGarageCycles ?? 0,
                canonical_state_unchanged =
                    !string.IsNullOrEmpty(_cycleCanonicalBefore) &&
                    string.Equals(
                        _cycleCanonicalBefore,
                        _cycleCanonicalAfter,
                        StringComparison.Ordinal),
                canonical_sha256_before = _cycleCanonicalBefore,
                canonical_sha256_after = _cycleCanonicalAfter,
                topology_stable_at_all_checkpoints =
                    _topologyStable &&
                    _cycleCheckpointCount == _cycleMemoryCheckpoints.Length &&
                    (_schedule?.CompletedCityGarageCycles ?? 0) ==
                    (_durations?.CityGarageCycles ?? -1),
                managed_heap_monotonic_growth_detected =
                    managedMonotonicGrowth,
                unity_allocated_monotonic_growth_detected =
                    allocatedMonotonicGrowth,
                unity_reserved_monotonic_growth_detected =
                    reservedMonotonicGrowth,
                no_monotonic_memory_growth =
                    checkpoints.Length == 5 &&
                    !managedMonotonicGrowth &&
                    !allocatedMonotonicGrowth &&
                    !reservedMonotonicGrowth,
                post_cycle_first_submit_command_delta =
                    _postCycleFirstSubmitCommandDelta,
                post_cycle_duplicate_submit_command_delta =
                    _postCycleDuplicateSubmitCommandDelta,
                post_cycle_exactly_one_action =
                    _postCycleExactlyOneAction &&
                    _postCycleFirstSubmitCommandDelta == 1 &&
                    _postCycleDuplicateSubmitCommandDelta == 0,
                memory_checkpoints = checkpoints,
            };
        }

        private LastBearingNativePerformanceTopologyReport
            BuildTopologyReport()
        {
            LastBearingFieldDeskPerformanceTopology? initial = _initialTopology;
            LastBearingFieldDeskPerformanceTopology? final = _finalTopology;
            return new LastBearingNativePerformanceTopologyReport
            {
                initial_owned_unity_objects = initial?.OwnedUnityObjectCount ?? 0,
                final_owned_unity_objects = final?.OwnedUnityObjectCount ?? 0,
                initial_visual_elements = initial?.VisualElementCount ?? 0,
                final_visual_elements = final?.VisualElementCount ?? 0,
                initial_bindings = initial?.BindingCount ?? 0,
                final_bindings = final?.BindingCount ?? 0,
                initial_registered_callbacks =
                    initial?.RegisteredCallbackCount ?? 0,
                final_registered_callbacks =
                    final?.RegisteredCallbackCount ?? 0,
                exact_identity_set_retained =
                    _topologyStable && initial != null && final != null,
                initial_ui_documents = _initialUiDocuments.Length,
                final_ui_documents = _finalUiDocuments.Length,
                exact_ui_document_set_retained = ExactObjectSet(
                    _initialUiDocuments,
                    _finalUiDocuments),
                initial_cameras = _initialCameras.Length,
                final_cameras = _finalCameras.Length,
                exact_camera_set_retained = ExactObjectSet(
                    _initialCameras,
                    _finalCameras),
                initial_audio_listeners = _initialAudioListeners.Length,
                final_audio_listeners = _finalAudioListeners.Length,
                exact_audio_listener_set_retained = ExactObjectSet(
                    _initialAudioListeners,
                    _finalAudioListeners),
            };
        }

        private LastBearingNativePerformanceRetentionReport
            BuildRetentionReport()
        {
            return new LastBearingNativePerformanceRetentionReport
            {
                managed_heap_bytes_before = _retentionBefore.managed_heap_bytes,
                managed_heap_bytes_after = _retentionAfter.managed_heap_bytes,
                managed_heap_growth_bytes =
                    _retentionAfter.managed_heap_bytes -
                    _retentionBefore.managed_heap_bytes,
                managed_heap_growth_percent =
                    LastBearingNativePerformanceSampleBuffer.GrowthPercent(
                        _retentionBefore.managed_heap_bytes,
                        _retentionAfter.managed_heap_bytes),
                managed_heap_not_grown =
                    _retentionAfter.managed_heap_bytes <=
                    _retentionBefore.managed_heap_bytes,
                unity_allocated_bytes_before =
                    _retentionBefore.unity_allocated_bytes,
                unity_allocated_bytes_after =
                    _retentionAfter.unity_allocated_bytes,
                unity_allocated_growth_bytes =
                    _retentionAfter.unity_allocated_bytes -
                    _retentionBefore.unity_allocated_bytes,
                unity_allocated_growth_percent =
                    LastBearingNativePerformanceSampleBuffer.GrowthPercent(
                        _retentionBefore.unity_allocated_bytes,
                        _retentionAfter.unity_allocated_bytes),
                unity_allocated_not_grown =
                    _retentionAfter.unity_allocated_bytes <=
                    _retentionBefore.unity_allocated_bytes,
                unity_reserved_bytes_before =
                    _retentionBefore.unity_reserved_bytes,
                unity_reserved_bytes_after =
                    _retentionAfter.unity_reserved_bytes,
                unity_reserved_growth_bytes =
                    _retentionAfter.unity_reserved_bytes -
                    _retentionBefore.unity_reserved_bytes,
                unity_reserved_growth_percent =
                    LastBearingNativePerformanceSampleBuffer.GrowthPercent(
                        _retentionBefore.unity_reserved_bytes,
                        _retentionAfter.unity_reserved_bytes),
                unity_reserved_not_grown =
                    _retentionAfter.unity_reserved_bytes <=
                    _retentionBefore.unity_reserved_bytes,
                no_retained_memory_growth =
                    _retentionAfter.managed_heap_bytes <=
                        _retentionBefore.managed_heap_bytes &&
                    _retentionAfter.unity_allocated_bytes <=
                        _retentionBefore.unity_allocated_bytes &&
                    _retentionAfter.unity_reserved_bytes <=
                        _retentionBefore.unity_reserved_bytes,
                canonical_state_unchanged =
                    !string.IsNullOrEmpty(_pausedCanonicalBefore) &&
                    string.Equals(
                        _pausedCanonicalBefore,
                        _pausedCanonicalAfter,
                        StringComparison.Ordinal),
                canonical_sha256_before = _pausedCanonicalBefore,
                canonical_sha256_after = _pausedCanonicalAfter,
                exact_topology_retained = _topologyStable,
            };
        }

        private bool ApplyAcceptance(
            LastBearingNativePerformanceReport report,
            out string failureCode)
        {
            LastBearingNativePerformanceAcceptanceReport acceptance = new()
            {
                paused_p95_zero_bytes_per_frame =
                    report.paused.gc_allocated_p95_bytes == 0L,
                representative_unpaused_p95_zero_bytes_per_frame =
                    report.representative_unpaused.gc_allocated_p95_bytes == 0L,
                representative_unpaused_average_below_1024_bytes_per_frame =
                    report.representative_unpaused
                        .gc_allocated_average_bytes < 1024d,
                all_sample_buffers_populated_without_overflow =
                    HealthyPhase(
                        report.paused,
                        _durations!.PausedSeconds) &&
                    HealthyPhase(
                        report.representative_unpaused,
                        _durations.RepresentativeUnpausedSeconds) &&
                    HealthyCyclePhase(report.city_garage_cycles),
                exact_100_cycles =
                    report.cycles.requested_cycles == 100 &&
                    report.cycles.completed_cycles == 100 &&
                    report.cycles.canonical_state_unchanged &&
                    report.cycles.topology_stable_at_all_checkpoints &&
                    report.cycles.memory_checkpoints.Length == 5,
                stable_retained_topology =
                    report.topology.exact_identity_set_retained &&
                    report.topology.exact_ui_document_set_retained &&
                    report.topology.exact_camera_set_retained &&
                    report.topology.exact_audio_listener_set_retained &&
                    report.paused_retention.canonical_state_unchanged &&
                    report.paused_retention.exact_topology_retained,
                paused_no_retained_memory_growth =
                    report.paused_retention.no_retained_memory_growth,
                cycles_no_monotonic_memory_growth =
                    report.cycles.no_monotonic_memory_growth,
                post_cycle_exactly_one_action =
                    report.cycles.post_cycle_exactly_one_action,
                exact_2560x1600 =
                    report.environment.exact_resolution_verified,
            };

            acceptance.passed =
                report.actual_warmup_seconds >=
                    report.requested_warmup_seconds &&
                acceptance.paused_p95_zero_bytes_per_frame &&
                acceptance.representative_unpaused_p95_zero_bytes_per_frame &&
                acceptance
                    .representative_unpaused_average_below_1024_bytes_per_frame &&
                acceptance.all_sample_buffers_populated_without_overflow &&
                acceptance.exact_100_cycles &&
                acceptance.stable_retained_topology &&
                acceptance.paused_no_retained_memory_growth &&
                acceptance.cycles_no_monotonic_memory_growth &&
                acceptance.post_cycle_exactly_one_action &&
                acceptance.exact_2560x1600;
            report.acceptance = acceptance;

            if (report.actual_warmup_seconds < report.requested_warmup_seconds)
            {
                failureCode = "warmup-shorter-than-300-seconds";
            }
            else if (!acceptance.all_sample_buffers_populated_without_overflow)
            {
                failureCode = "sample-buffer-empty-short-or-overflowed";
            }
            else if (!acceptance.paused_p95_zero_bytes_per_frame)
            {
                failureCode = "paused-city-managed-allocation-p95-not-zero";
            }
            else if (!acceptance.representative_unpaused_p95_zero_bytes_per_frame)
            {
                failureCode =
                    "representative-unpaused-city-managed-allocation-p95-not-zero";
            }
            else if (!acceptance
                         .representative_unpaused_average_below_1024_bytes_per_frame)
            {
                failureCode =
                    "representative-unpaused-city-managed-allocation-average-too-high";
            }
            else if (!acceptance.exact_100_cycles)
            {
                failureCode = "city-garage-cycle-proof-incomplete";
            }
            else if (!acceptance.stable_retained_topology)
            {
                failureCode = "retained-object-topology-changed";
            }
            else if (!acceptance.paused_no_retained_memory_growth)
            {
                failureCode = "paused-city-retained-memory-grew";
            }
            else if (!acceptance.cycles_no_monotonic_memory_growth)
            {
                failureCode = "city-garage-memory-growth-was-monotonic";
            }
            else if (!acceptance.post_cycle_exactly_one_action)
            {
                failureCode = "post-cycle-action-cardinality-not-one";
            }
            else if (!acceptance.exact_2560x1600)
            {
                failureCode = "native-resolution-not-2560x1600";
            }
            else
            {
                failureCode = string.Empty;
            }

            return acceptance.passed;
        }

        private static bool HealthyPhase(
            LastBearingNativePerformancePhaseReport phase,
            double requiredSeconds)
        {
            return phase.measured_seconds >= requiredSeconds &&
                   phase.frame_sample_count > 0 &&
                   !phase.frame_sample_capacity_exceeded &&
                   phase.simulation_tick_sample_count > 0 &&
                   !phase.simulation_tick_capacity_exceeded;
        }

        private static bool HealthyCyclePhase(
            LastBearingNativePerformancePhaseReport phase)
        {
            return phase.measured_seconds > 0d &&
                   phase.frame_sample_count > 0 &&
                   !phase.frame_sample_capacity_exceeded &&
                   phase.simulation_tick_sample_count > 0 &&
                   !phase.simulation_tick_capacity_exceeded;
        }

        private static bool ExactObjectSet<T>(T[] before, T[] after)
            where T : UnityEngine.Object
        {
            if (before.Length != after.Length)
            {
                return false;
            }

            for (var index = 0; index < before.Length; index++)
            {
                var found = false;
                for (var candidate = 0;
                     candidate < after.Length;
                     candidate++)
                {
                    if (ReferenceEquals(before[index], after[candidate]))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteAtomically(string path, string json)
        {
            string temporaryPath = path + ".tmp";
            if (File.Exists(path) || File.Exists(temporaryPath))
            {
                throw new IOException("nonce-bound report path already exists");
            }

            byte[] bytes = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false).GetBytes(json);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path);
        }
    }
}
