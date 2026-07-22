#nullable enable

using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Performance;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingNativePerformanceContractTests
    {
        [Test]
        public void SourceBoundRequestRequiresMatchingIl2CppArm64Identity()
        {
            LastBearingNativePerformanceRequest request = ValidRequest();
            LastBearingNativePerformanceBuildIdentity identity = ValidIdentity();

            bool valid = LastBearingNativePerformanceLaunch.Validate(
                request,
                identity,
                Repeat('c', 64),
                compiledWithIl2Cpp: true,
                Architecture.Arm64,
                applicationBuildGuid: Repeat('e', 32),
                unityVersion: "6000.5.4f1",
                isDevelopmentBuild: true,
                out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(error, Is.Empty);
        }

        [Test]
        public void SourceBoundRequestRejectsIdentityOrRuntimeDrift()
        {
            LastBearingNativePerformanceRequest request = ValidRequest();
            LastBearingNativePerformanceBuildIdentity identity = ValidIdentity();

            Assert.That(
                LastBearingNativePerformanceLaunch.Validate(
                    request,
                    identity,
                    Repeat('d', 64),
                    compiledWithIl2Cpp: true,
                    Architecture.Arm64,
                    Repeat('e', 32),
                    "6000.5.4f1",
                    true,
                    out _),
                Is.False,
                "a different build-identity sidecar hash must fail closed");
            Assert.That(
                LastBearingNativePerformanceLaunch.Validate(
                    request,
                    identity,
                    Repeat('c', 64),
                    compiledWithIl2Cpp: false,
                    Architecture.Arm64,
                    Repeat('e', 32),
                    "6000.5.4f1",
                    true,
                    out _),
                Is.False,
                "a Mono player must fail closed");
            Assert.That(
                LastBearingNativePerformanceLaunch.Validate(
                    request,
                    identity,
                    Repeat('c', 64),
                    compiledWithIl2Cpp: true,
                    Architecture.X64,
                    Repeat('e', 32),
                    "6000.5.4f1",
                    true,
                    out _),
                Is.False,
                "a non-Arm64 player must fail closed");
            Assert.That(
                LastBearingNativePerformanceLaunch.Validate(
                    request,
                    identity,
                    Repeat('c', 64),
                    compiledWithIl2Cpp: true,
                    Architecture.Arm64,
                    Repeat('e', 32),
                    "6000.5.4f1",
                    false,
                    out _),
                Is.False,
                "a non-development player must fail closed");
            Assert.That(
                LastBearingNativePerformanceLaunch.Validate(
                    request,
                    identity,
                    Repeat('c', 64),
                    compiledWithIl2Cpp: true,
                    Architecture.Arm64,
                    Repeat('e', 32),
                    "6000.5.4f2",
                    true,
                    out _),
                Is.False,
                "a different Unity version must fail closed");
        }

        [Test]
        public void LaunchPathsAreFixedInsideOneBoundedRunDirectory()
        {
            string runDirectory = Path.Combine(
                Path.GetTempPath(),
                "wp0002-native-performance-contract-test");
            string requestPath = Path.Combine(
                runDirectory,
                LastBearingNativePerformanceLaunch.RequestFileName);
            string identityPath = Path.Combine(
                runDirectory,
                LastBearingNativePerformanceLaunch.BuildIdentityFileName);

            Assert.That(
                LastBearingNativePerformanceLaunch.TryValidateBoundedPaths(
                    requestPath,
                    identityPath,
                    runDirectory,
                    out string canonicalRequest,
                    out string canonicalIdentity,
                    out string error),
                Is.True,
                error);
            Assert.That(canonicalRequest, Is.EqualTo(requestPath));
            Assert.That(canonicalIdentity, Is.EqualTo(identityPath));
            Assert.That(
                LastBearingNativePerformanceLaunch.TryValidateBoundedPaths(
                    Path.Combine(
                        Path.GetTempPath(),
                        LastBearingNativePerformanceLaunch.RequestFileName),
                    identityPath,
                    runDirectory,
                    out _,
                    out _,
                    out _),
                Is.False,
                "a caller-selected path outside the run directory must fail closed");
        }

        [Test]
        public void RunDirectoryIsDerivedFromSourceBoundMacPlayerBundle()
        {
            string runDirectory = Path.Combine(
                Path.GetTempPath(),
                "repository",
                "BuildArtifacts",
                "WP-0002",
                "local-only",
                "native-il2cpp-arm64",
                "runs",
                Repeat('b', 12) + "-" + Repeat('e', 32));
            string dataPath = Path.Combine(
                runDirectory,
                LastBearingNativePerformanceLaunch.PlayerBundleName,
                "Contents");

            Assert.That(
                LastBearingNativePerformanceLaunch
                    .TryDeriveRunDirectoryFromMacPlayerDataPath(
                        dataPath,
                        out string actual,
                        out string error),
                Is.True,
                error);
            Assert.That(actual, Is.EqualTo(runDirectory));
            Assert.That(
                LastBearingNativePerformanceLaunch
                    .TryDeriveRunDirectoryFromMacPlayerDataPath(
                        Path.Combine(
                            Path.GetTempPath(),
                            "unbounded",
                            LastBearingNativePerformanceLaunch.PlayerBundleName,
                            "Contents"),
                        out _,
                        out _),
                Is.False,
                "a player outside the bounded BuildArtifacts root must fail closed");
            Assert.That(
                LastBearingNativePerformanceLaunch
                    .TryDeriveRunDirectoryFromMacPlayerDataPath(
                        Path.Combine(runDirectory, "not-a-bundle"),
                        out _,
                        out _),
                Is.False,
                "a non-macOS-player data path must fail closed");
        }

        [TestCase(2560, 1600, true)]
        [TestCase(2560, 1599, false)]
        [TestCase(1920, 1200, false)]
        public void NativeResolutionContractIsExact(
            int width,
            int height,
            bool expected)
        {
            Assert.That(
                LastBearingNativePerformanceEnvironment.IsExactResolution(
                    width,
                    height),
                Is.EqualTo(expected));
        }

        [Test]
        public void ActivationUsesOneFixedFlagAndNoCallerPaths()
        {
            Assert.That(
                LastBearingNativePerformanceLaunch.HasActivationArgument(
                    new[]
                    {
                        "player",
                        LastBearingNativePerformanceLaunch.ActivationArgument,
                    }),
                Is.True);
            Assert.That(
                LastBearingNativePerformanceLaunch.HasActivationArgument(
                    new[]
                    {
                        "player",
                        LastBearingNativePerformanceLaunch.ActivationArgument +
                        "=/caller/path",
                    }),
                Is.False,
                "a path-valued lookalike must not activate the gate");
        }

        [Test]
        public void TryLoadBindsFixedFilesNonceAndRunIdentity()
        {
            string testRoot = Path.Combine(
                Path.GetTempPath(),
                "wp0002-native-load-" + System.Guid.NewGuid().ToString("N"));
            string runsRoot = Path.Combine(
                testRoot,
                "BuildArtifacts",
                "WP-0002",
                "local-only",
                "native-il2cpp-arm64",
                "runs");
            string validRun = Path.Combine(
                runsRoot,
                Repeat('b', 12) + "-" + Repeat('e', 32));
            string mismatchedRun = Path.Combine(
                runsRoot,
                Repeat('c', 12) + "-" + Repeat('e', 32));
            try
            {
                Directory.CreateDirectory(validRun);
                LastBearingNativePerformanceBuildIdentity identity =
                    ValidIdentity();
                string identityJson = JsonUtility.ToJson(identity);
                string identityPath = Path.Combine(
                    validRun,
                    LastBearingNativePerformanceLaunch.BuildIdentityFileName);
                File.WriteAllText(identityPath, identityJson);
                LastBearingNativePerformanceRequest request = ValidRequest();
                request.expected_build_identity_sha256 =
                    LastBearingNativePerformanceLaunch.ComputeSha256(
                        File.ReadAllBytes(identityPath));
                string requestJson = JsonUtility.ToJson(request);
                File.WriteAllText(
                    Path.Combine(
                        validRun,
                        LastBearingNativePerformanceLaunch.RequestFileName),
                    requestJson);

                string[] arguments =
                {
                    "player",
                    LastBearingNativePerformanceLaunch.ActivationArgument,
                };
                Assert.That(
                    LastBearingNativePerformanceLaunch.TryLoad(
                        arguments,
                        compiledWithIl2Cpp: true,
                        Architecture.Arm64,
                        Repeat('e', 32),
                        "6000.5.4f1",
                        isDevelopmentBuild: true,
                        validRun,
                        out LastBearingNativePerformanceLaunch? launch,
                        out string error),
                    Is.True,
                    error);
                Assert.That(launch, Is.Not.Null);
                Assert.That(
                    launch!.ReportPath,
                    Is.EqualTo(
                        Path.Combine(
                            validRun,
                            "wp0002-native-performance-" +
                            Repeat('f', 32) +
                            ".report.json")));

                Assert.That(
                    LastBearingNativePerformanceLaunch.TryLoad(
                        new[]
                        {
                            LastBearingNativePerformanceLaunch.ActivationArgument,
                            LastBearingNativePerformanceLaunch.ActivationArgument,
                        },
                        true,
                        Architecture.Arm64,
                        Repeat('e', 32),
                        "6000.5.4f1",
                        true,
                        validRun,
                        out _,
                        out _),
                    Is.False,
                    "duplicate activation must fail closed");

                Directory.CreateDirectory(mismatchedRun);
                File.Copy(
                    identityPath,
                    Path.Combine(
                        mismatchedRun,
                        LastBearingNativePerformanceLaunch.BuildIdentityFileName));
                File.Copy(
                    Path.Combine(
                        validRun,
                        LastBearingNativePerformanceLaunch.RequestFileName),
                    Path.Combine(
                        mismatchedRun,
                        LastBearingNativePerformanceLaunch.RequestFileName));
                Assert.That(
                    LastBearingNativePerformanceLaunch.TryLoad(
                        arguments,
                        true,
                        Architecture.Arm64,
                        Repeat('e', 32),
                        "6000.5.4f1",
                        true,
                        mismatchedRun,
                        out _,
                        out _),
                    Is.False,
                    "run-name drift from the source/build identity must fail closed");
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, recursive: true);
                }
            }
        }

        [Test]
        public void MemoryTrendFlagsOnlyNondecreasingPositiveGrowth()
        {
            Assert.That(
                LastBearingNativePerformanceMemoryTrend.HasMonotonicGrowth(
                    MemoryCheckpoints(100, 110, 110, 120, 130),
                    LastBearingNativePerformanceMemoryMetric.ManagedHeap),
                Is.True);
            Assert.That(
                LastBearingNativePerformanceMemoryTrend.HasMonotonicGrowth(
                    MemoryCheckpoints(100, 110, 105, 120, 130),
                    LastBearingNativePerformanceMemoryMetric.ManagedHeap),
                Is.False,
                "a measured decline disproves monotonic growth");
            Assert.That(
                LastBearingNativePerformanceMemoryTrend.HasMonotonicGrowth(
                    MemoryCheckpoints(100, 100, 100, 100, 100),
                    LastBearingNativePerformanceMemoryMetric.ManagedHeap),
                Is.False,
                "a flat series is not growth");
            Assert.That(
                LastBearingNativePerformanceMemoryTrend.HasMonotonicGrowth(
                    MemoryCheckpoints(100),
                    LastBearingNativePerformanceMemoryMetric.ManagedHeap),
                Is.True,
                "insufficient checkpoints must fail closed");
        }

        [Test]
        public void ProductionSchedulePinsExactDurationsAndNaturalCycles()
        {
            LastBearingNativePerformanceDurations durations =
                LastBearingNativePerformanceDurations.Production();

            Assert.That(durations.WarmupSeconds, Is.EqualTo(300d));
            Assert.That(durations.PausedSeconds, Is.EqualTo(300d));
            Assert.That(
                durations.RepresentativeUnpausedSeconds,
                Is.EqualTo(300d));
            Assert.That(durations.CityGarageCycles, Is.EqualTo(100));
            Assert.That(durations.CycleHalfDwellSeconds, Is.EqualTo(0.25d));
            Assert.That(
                LastBearingNativePerformanceEnvironment.RequiredWidth,
                Is.EqualTo(2560));
            Assert.That(
                LastBearingNativePerformanceEnvironment.RequiredHeight,
                Is.EqualTo(1600));
        }

        [TestCase(300d, 307712)]
        [TestCase(60d, 61952)]
        public void FrameBuffersCoverBoundedHighRefreshNativeRuns(
            double seconds,
            int expectedCapacity)
        {
            MethodInfo? createSamples =
                typeof(LastBearingNativePerformanceHarness).GetMethod(
                    "CreateSamples",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(createSamples, Is.Not.Null);
            object samples = createSamples!.Invoke(
                null,
                new object[] { "capacity-probe", seconds })!;
            FieldInfo? frameSamples = samples.GetType().GetField(
                "_gcAllocatedBytes",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(frameSamples, Is.Not.Null);
            var values = (long[])frameSamples!.GetValue(samples)!;

            Assert.That(values, Has.Length.EqualTo(expectedCapacity));
        }

        [Test]
        public void InjectedClockTraversesEveryPhaseWithoutRealTimeDelay()
        {
            var clock = new ManualClock();
            var durations = new LastBearingNativePerformanceDurations(
                warmupSeconds: 1d,
                pausedSeconds: 2d,
                representativeUnpausedSeconds: 3d,
                cityGarageCycles: 2,
                cycleHalfDwellSeconds: 0.5d);
            var schedule = new LastBearingNativePerformanceSchedule(
                clock,
                durations);

            Assert.That(
                schedule.Start(),
                Is.EqualTo(LastBearingNativePerformanceAction.BeginWarmup));
            clock.Advance(1d);
            Assert.That(
                schedule.Advance(isPaused: false),
                Is.EqualTo(
                    LastBearingNativePerformanceAction
                        .RequestPauseForPausedMeasurement));
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(
                    LastBearingNativePerformanceAction
                        .PreparePausedMeasurement));
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(
                    LastBearingNativePerformanceAction.BeginPausedMeasurement));
            clock.Advance(2d);
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(
                    LastBearingNativePerformanceAction
                        .EndPausedMeasurementAndRequestResume));
            Assert.That(
                schedule.Advance(isPaused: false),
                Is.EqualTo(
                    LastBearingNativePerformanceAction
                        .BeginRepresentativeUnpausedMeasurement));
            clock.Advance(3d);
            Assert.That(
                schedule.Advance(isPaused: false),
                Is.EqualTo(
                    LastBearingNativePerformanceAction
                        .EndRepresentativeUnpausedMeasurementAndRequestPause));
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(
                    LastBearingNativePerformanceAction.BeginCityGarageCycles));

            AssertCycle(schedule, clock, expectedCompletedCycles: 1);
            AssertCycle(schedule, clock, expectedCompletedCycles: 2);
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(
                    LastBearingNativePerformanceAction
                        .EndCityGarageCyclesAndSubmitResume));
            Assert.That(
                schedule.Advance(isPaused: false),
                Is.EqualTo(LastBearingNativePerformanceAction.Complete));
            Assert.That(
                schedule.Stage,
                Is.EqualTo(LastBearingNativePerformanceStage.Complete));
        }

        [Test]
        public void PausedMeasurementFailsClosedIfExplicitPauseDrifts()
        {
            var clock = new ManualClock();
            var durations = new LastBearingNativePerformanceDurations(
                warmupSeconds: 1d,
                pausedSeconds: 2d,
                representativeUnpausedSeconds: 3d,
                cityGarageCycles: 2,
                cycleHalfDwellSeconds: 0.5d);
            var schedule = new LastBearingNativePerformanceSchedule(
                clock,
                durations);

            schedule.Start();
            clock.Advance(1d);
            schedule.Advance(isPaused: false);
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(
                    LastBearingNativePerformanceAction
                        .PreparePausedMeasurement));
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(
                    LastBearingNativePerformanceAction.BeginPausedMeasurement));
            Assert.That(
                schedule.Advance(isPaused: false),
                Is.EqualTo(
                    LastBearingNativePerformanceAction
                        .FailPausedMeasurementDrift));
        }

        private static void AssertCycle(
            LastBearingNativePerformanceSchedule schedule,
            ManualClock clock,
            int expectedCompletedCycles)
        {
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(LastBearingNativePerformanceAction.ShowGarage));
            clock.Advance(0.5d);
            Assert.That(
                schedule.Advance(isPaused: true),
                Is.EqualTo(LastBearingNativePerformanceAction.ShowCity));
            Assert.That(
                schedule.CompletedCityGarageCycles,
                Is.EqualTo(expectedCompletedCycles));
            clock.Advance(0.5d);
        }

        private static LastBearingNativePerformanceRequest ValidRequest()
        {
            return new LastBearingNativePerformanceRequest
            {
                schema_version = 1,
                contract_id = LastBearingNativePerformanceRequest.ContractId,
                request_nonce = Repeat('f', 32),
                expected_source_commit = Repeat('a', 40),
                expected_source_tree_sha256 = Repeat('b', 64),
                expected_build_identity_sha256 = Repeat('c', 64),
                expected_build_guid = Repeat('e', 32),
                expected_executable_sha256 = Repeat('d', 64),
            };
        }

        private static LastBearingNativePerformanceBuildIdentity ValidIdentity()
        {
            return new LastBearingNativePerformanceBuildIdentity
            {
                schema_version = 1,
                identity_id =
                    LastBearingNativePerformanceBuildIdentity.IdentityId,
                source_commit = Repeat('a', 40),
                source_tree_sha256 = Repeat('b', 64),
                build_guid = Repeat('e', 32),
                unity_version = "6000.5.4f1",
                executable_sha256 = Repeat('d', 64),
                development_build = true,
            };
        }

        private static string Repeat(char value, int count)
        {
            return new string(value, count);
        }

        private static LastBearingNativePerformanceMemoryCheckpoint[]
            MemoryCheckpoints(params long[] managedHeapBytes)
        {
            var checkpoints =
                new LastBearingNativePerformanceMemoryCheckpoint[
                    managedHeapBytes.Length];
            for (var index = 0; index < managedHeapBytes.Length; index++)
            {
                checkpoints[index] =
                    new LastBearingNativePerformanceMemoryCheckpoint
                    {
                        completed_cycles = index * 25,
                        managed_heap_bytes = managedHeapBytes[index],
                    };
            }

            return checkpoints;
        }

        private sealed class ManualClock : ILastBearingNativePerformanceClock
        {
            public double NowSeconds { get; private set; }

            internal void Advance(double seconds)
            {
                NowSeconds += seconds;
            }
        }
    }
}
