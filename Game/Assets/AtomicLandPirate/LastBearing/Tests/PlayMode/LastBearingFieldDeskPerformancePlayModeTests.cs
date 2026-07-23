#nullable enable

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingFieldDeskPerformancePlayModeTests
    {
        private const string RequestRelativePath =
            "BuildArtifacts/WP-0002/local-only/" +
            "vgr-13-field-desk-performance-request.json";
        private const string DiagnosticPaused = "diagnostic_paused";
        private const string DiagnosticUnpaused = "diagnostic_unpaused";
        private const string PausedFiveMinuteSoak = "paused_five_minute_soak";
        private const int RecorderCapacity = 65536;
        private const float WarmupSeconds = 10f;
        private const float DiagnosticSeconds = 30f;
        private const float SoakSeconds = 300f;

        private GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
                _root = null;
            }

            foreach (LastBearingGameController controller in
                     UnityEngine.Object.FindObjectsByType<LastBearingGameController>(
                         FindObjectsInactive.Include))
            {
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator MeasureManagedAllocationDiagnosticWhenLocallyRequested()
        {
            string requestPath = Path.Combine(
                ResolveRepositoryRoot(),
                RequestRelativePath);
            if (!File.Exists(requestPath))
            {
                yield break;
            }

            DiagnosticRequest request = ParseRequest(requestPath);
            if (!string.Equals(request.phase, DiagnosticPaused, StringComparison.Ordinal) &&
                !string.Equals(request.phase, DiagnosticUnpaused, StringComparison.Ordinal))
            {
                yield break;
            }

            bool paused = string.Equals(
                request.phase,
                DiagnosticPaused,
                StringComparison.Ordinal);

            LastBearingGameController controller = BuildController();
            yield return null;
            StageRepresentativeCity(controller);

            if (paused)
            {
                controller.TogglePause();
                for (var frame = 0;
                     frame < 240 && controller.HasPendingPlayerCommands;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(controller.HasPendingPlayerCommands, Is.False);
                Assert.That(
                    controller.ReadModel?.PauseCause,
                    Is.EqualTo(PauseCause.Explicit));
            }
            else
            {
                Assert.That(
                    controller.ReadModel?.PauseCause,
                    Is.EqualTo(PauseCause.None));
            }

            Assert.That(controller.FieldDesk?.OwnsCityOverview, Is.True);
            yield return new WaitForSecondsRealtime(WarmupSeconds);

            var measurementWait = new WaitForSecondsRealtime(
                DiagnosticSeconds);
            ProfilerRecorderOptions options =
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached;
            ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                RecorderCapacity,
                options);
            double startedAt = Time.realtimeSinceStartupAsDouble;
            yield return measurementWait;
            while (Time.realtimeSinceStartupAsDouble - startedAt <
                   DiagnosticSeconds)
            {
                yield return null;
            }

            double measuredSeconds =
                Time.realtimeSinceStartupAsDouble - startedAt;
            recorder.Stop();
            try
            {
                Assert.That(recorder.Valid, Is.True);
                Assert.That(recorder.WrappedAround, Is.False);
                Assert.That(recorder.Count, Is.GreaterThan(0));

                var samples = new long[recorder.Count];
                long sum = 0;
                int zeroCount = 0;
                long maximum = 0;
                for (var index = 0; index < samples.Length; index++)
                {
                    long value = recorder.GetSample(index).Value;
                    Assert.That(value, Is.GreaterThanOrEqualTo(0));
                    samples[index] = value;
                    sum = checked(sum + value);
                    if (value == 0)
                    {
                        zeroCount++;
                    }

                    if (value > maximum)
                    {
                        maximum = value;
                    }
                }

                Array.Sort(samples);
                int rank = checked((int)((95L * samples.Length + 99L) / 100L));
                long p95 = samples[rank - 1];
                double average = (double)sum / samples.Length;
                Debug.Log(
                    "VGR13_PERF_DIAGNOSTIC" +
                    " phase=" + request.phase +
                    " duration_seconds=" + measuredSeconds.ToString("F3") +
                    " samples=" + samples.Length +
                    " unit=" + recorder.UnitType +
                    " zero_samples=" + zeroCount +
                    " p95_bytes=" + p95 +
                    " average_bytes=" + average.ToString("F3") +
                    " max_bytes=" + maximum);
            }
            finally
            {
                recorder.Dispose();
            }
        }

        [UnityTest]
        [Timeout(360000)]
        public IEnumerator PausedFieldDeskFiveMinuteSoakWhenLocallyRequested()
        {
            string requestPath = Path.Combine(
                ResolveRepositoryRoot(),
                RequestRelativePath);
            if (!File.Exists(requestPath))
            {
                yield break;
            }

            DiagnosticRequest request = ParseRequest(requestPath);
            if (!string.Equals(
                    request.phase,
                    PausedFiveMinuteSoak,
                    StringComparison.Ordinal))
            {
                yield break;
            }

            LastBearingGameController controller = BuildController();
            yield return null;
            StageRepresentativeCity(controller);
            controller.TogglePause();
            for (var frame = 0;
                 frame < 240 && controller.HasPendingPlayerCommands;
                 frame++)
            {
                yield return null;
            }

            Assert.That(controller.HasPendingPlayerCommands, Is.False);
            Assert.That(
                controller.ReadModel?.PauseCause,
                Is.EqualTo(PauseCause.Explicit));

            LastBearingFieldDesk desk = RequireDesk(controller);
            UIDocument document = RequireDocument(controller);
            PanelSettings panelSettings = document.panelSettings;
            VisualElement visualRoot = document.rootVisualElement;
            int visualElementCount = CountVisualElements(visualRoot);
            Array bindings = RequireBindings(desk);
            object[] bindingIdentities = CopyIdentities(bindings);
            string canonicalBefore = controller.CanonicalHash;
            controller.enabled = false;
            desk.Refresh(force: true);
            yield return new WaitForSecondsRealtime(1.1f);

            double startedAt = Time.realtimeSinceStartupAsDouble;
            double nextTopologyCheck = startedAt;
            var samples = 0;
            while (Time.realtimeSinceStartupAsDouble - startedAt < SoakSeconds)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                desk.Refresh();
                long allocated = checked(
                    GC.GetAllocatedBytesForCurrentThread() - before);
                Assert.That(
                    allocated,
                    Is.EqualTo(0),
                    "The paused Field Desk allocated during its five-minute soak.");
                samples++;

                double now = Time.realtimeSinceStartupAsDouble;
                if (now >= nextTopologyCheck)
                {
                    Assert.That(controller.FieldDesk, Is.SameAs(desk));
                    Assert.That(RequireDocument(controller), Is.SameAs(document));
                    Assert.That(document.panelSettings, Is.SameAs(panelSettings));
                    Assert.That(document.rootVisualElement, Is.SameAs(visualRoot));
                    Assert.That(
                        CountVisualElements(visualRoot),
                        Is.EqualTo(visualElementCount));
                    AssertBindingIdentities(bindings, bindingIdentities);
                    AssertRegisteredBindings(bindings);
                    Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
                    nextTopologyCheck = now + 30d;
                }

                yield return null;
            }

            Assert.That(samples, Is.GreaterThan(0));
            Debug.Log(
                "VGR13_PAUSED_SOAK" +
                " duration_seconds=" +
                (Time.realtimeSinceStartupAsDouble - startedAt).ToString("F3") +
                " samples=" + samples +
                " max_field_desk_refresh_bytes=0" +
                " visual_elements=" + visualElementCount +
                " bindings=" + bindings.Length);
        }

        [UnityTest]
        public IEnumerator PausedSteadyStateRefreshAllocatesNoManagedMemory()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            StageRepresentativeCity(controller);
            controller.TogglePause();
            for (var frame = 0;
                 frame < 240 && controller.HasPendingPlayerCommands;
                 frame++)
            {
                yield return null;
            }

            Assert.That(controller.HasPendingPlayerCommands, Is.False);
            Assert.That(
                controller.ReadModel?.PauseCause,
                Is.EqualTo(PauseCause.Explicit));
            LastBearingFieldDesk desk = RequireDesk(controller);
            controller.enabled = false;
            desk.Refresh(force: true);

            // Let the one-second refresh deadline expire while the controller
            // is disabled, then measure both the due stamp check and the normal
            // per-frame early-return path on this thread with no Editor loop
            // work interleaved.
            yield return new WaitForSecondsRealtime(1.1f);
            long dueBefore = GC.GetAllocatedBytesForCurrentThread();
            desk.Refresh();
            long dueBytes = checked(
                GC.GetAllocatedBytesForCurrentThread() - dueBefore);
            Assert.That(
                dueBytes,
                Is.EqualTo(0),
                "An unchanged due Field Desk stamp check allocated managed memory.");

            for (var warmup = 0; warmup < 32; warmup++)
            {
                desk.Refresh();
            }

            long steadyBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var sample = 0; sample < 10000; sample++)
            {
                desk.Refresh();
            }

            long steadyBytes = checked(
                GC.GetAllocatedBytesForCurrentThread() - steadyBefore);
            Assert.That(
                steadyBytes,
                Is.EqualTo(0),
                "The paused unchanged Field Desk fast path allocated managed memory.");
        }

        [UnityTest]
        public IEnumerator HundredModeCyclesRetainOneDeskTreeAndBehavior()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            StageRepresentativeCity(controller);
            controller.TogglePause();
            for (var frame = 0;
                 frame < 240 && controller.HasPendingPlayerCommands;
                 frame++)
            {
                yield return null;
            }

            Assert.That(controller.HasPendingPlayerCommands, Is.False);
            LastBearingFieldDesk desk = RequireDesk(controller);
            UIDocument document = RequireDocument(controller);
            PanelSettings panelSettings = document.panelSettings;
            VisualElement visualRoot = document.rootVisualElement;
            int visualElementCount = CountVisualElements(visualRoot);
            Array bindings = RequireBindings(desk);
            AssertRegisteredBindings(bindings);
            Assert.That(bindings.Length, Is.EqualTo(18));
            object[] bindingIdentities = CopyIdentities(bindings);
            Camera[] camerasBefore =
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include);
            AudioListener[] listenersBefore =
                UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include);
            string canonicalBefore = controller.CanonicalHash;
            controller.enabled = false;

            for (var cycle = 0; cycle < 100; cycle++)
            {
                controller.OpenGarageBay();
                desk.Refresh(force: true);
                Assert.That(desk.OwnsCityOverview, Is.False);
                yield return null;

                controller.ShowCityOverview();
                desk.Refresh(force: true);
                Assert.That(desk.OwnsCityOverview, Is.True);
                yield return null;

                if ((cycle + 1) % 10 == 0)
                {
                    Assert.That(controller.FieldDesk, Is.SameAs(desk));
                    Assert.That(RequireDocument(controller), Is.SameAs(document));
                    Assert.That(document.panelSettings, Is.SameAs(panelSettings));
                    Assert.That(document.rootVisualElement, Is.SameAs(visualRoot));
                    Assert.That(
                        CountVisualElements(visualRoot),
                        Is.EqualTo(visualElementCount));
                    Assert.That(RequireBindings(desk), Is.SameAs(bindings));
                    AssertBindingIdentities(bindings, bindingIdentities);
                    AssertRegisteredBindings(bindings);
                }
            }

            Assert.That(
                controller.GetComponentsInChildren<LastBearingFieldDesk>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                controller.GetComponentsInChildren<UIDocument>(true),
                Has.Length.EqualTo(1));
            Assert.That(RequireBindings(desk), Is.SameAs(bindings));
            Assert.That(controller.ModeCoordinator?.ActiveModeCount, Is.EqualTo(1));
            Assert.That(
                controller.ModeCoordinator?.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
            AssertSameUnityObjects(
                camerasBefore,
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include));
            AssertSameUnityObjects(
                listenersBefore,
                UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include));

            Button? pauseButton =
                document.rootVisualElement.Q<Button>("pause-button");
            Assert.That(pauseButton, Is.Not.Null);
            Assert.That(RequirePendingCommandCount(controller), Is.EqualTo(0));
            Submit(pauseButton!);
            Assert.That(
                RequirePendingCommandCount(controller),
                Is.EqualTo(1),
                "One post-cycle submit must enqueue exactly one existing command.");
            Submit(pauseButton!);
            Assert.That(
                RequirePendingCommandCount(controller),
                Is.EqualTo(1),
                "The same-frame dispatch latch must reject a duplicate submit.");
        }

        private LastBearingGameController BuildController()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            return controller;
        }

        private static void StageRepresentativeCity(
            LastBearingGameController controller)
        {
            controller.InspectCityNeed();
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            controller.ManipulateCityGrammarPrimary();
            controller.FieldDesk?.Refresh(force: true);
        }

        private static LastBearingFieldDesk RequireDesk(
            LastBearingGameController controller)
        {
            Assert.That(controller.FieldDesk, Is.Not.Null);
            Assert.That(controller.FieldDesk!.IsOperational, Is.True);
            return controller.FieldDesk;
        }

        private static UIDocument RequireDocument(
            LastBearingGameController controller)
        {
            UIDocument[] documents =
                controller.GetComponentsInChildren<UIDocument>(true);
            Assert.That(documents, Has.Length.EqualTo(1));
            return documents[0];
        }

        private static int CountVisualElements(VisualElement element)
        {
            var count = 1;
            for (var index = 0; index < element.childCount; index++)
            {
                count = checked(count + CountVisualElements(element[index]));
            }

            return count;
        }

        private static Array RequireBindings(LastBearingFieldDesk desk)
        {
            FieldInfo? field = typeof(LastBearingFieldDesk).GetField(
                "_bindings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Array? bindings = field!.GetValue(desk) as Array;
            Assert.That(bindings, Is.Not.Null);
            return bindings!;
        }

        private static object[] CopyIdentities(Array bindings)
        {
            var identities = new object[bindings.Length];
            for (var index = 0; index < bindings.Length; index++)
            {
                object? binding = bindings.GetValue(index);
                Assert.That(binding, Is.Not.Null);
                identities[index] = binding!;
            }

            return identities;
        }

        private static void AssertBindingIdentities(
            Array bindings,
            object[] expected)
        {
            Assert.That(bindings.Length, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(bindings.GetValue(index), Is.SameAs(expected[index]));
            }
        }

        private static void AssertRegisteredBindings(Array bindings)
        {
            for (var index = 0; index < bindings.Length; index++)
            {
                object? binding = bindings.GetValue(index);
                Assert.That(binding, Is.Not.Null);
                FieldInfo? registered = binding!.GetType().GetField(
                    "_registered",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(registered, Is.Not.Null);
                Assert.That(registered!.GetValue(binding), Is.EqualTo(true));
            }
        }

        private static int RequirePendingCommandCount(
            LastBearingGameController controller)
        {
            FieldInfo? field = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            ICollection? commands = field!.GetValue(controller) as ICollection;
            Assert.That(commands, Is.Not.Null);
            return commands!.Count;
        }

        private static void Submit(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        private static void AssertSameUnityObjects<T>(T[] before, T[] after)
            where T : UnityEngine.Object
        {
            Assert.That(after, Has.Length.EqualTo(before.Length));
            for (var index = 0; index < before.Length; index++)
            {
                var found = false;
                for (var candidate = 0; candidate < after.Length; candidate++)
                {
                    if (ReferenceEquals(before[index], after[candidate]))
                    {
                        found = true;
                        break;
                    }
                }

                Assert.That(found, Is.True);
            }
        }

        private static DiagnosticRequest ParseRequest(string path)
        {
            string json = File.ReadAllText(path);
            DiagnosticRequest? request =
                JsonUtility.FromJson<DiagnosticRequest>(json);
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.schema_version, Is.EqualTo(1));
            Assert.That(
                request.phase,
                Is.EqualTo(DiagnosticPaused)
                    .Or.EqualTo(DiagnosticUnpaused)
                    .Or.EqualTo(PausedFiveMinuteSoak));
            return request;
        }

        private static string ResolveRepositoryRoot()
        {
            DirectoryInfo? game = Directory.GetParent(Application.dataPath);
            Assert.That(game, Is.Not.Null);
            DirectoryInfo? repository = game!.Parent;
            Assert.That(repository, Is.Not.Null);
            return repository!.FullName;
        }

        [Serializable]
        private sealed class DiagnosticRequest
        {
            public int schema_version;
            public string phase = string.Empty;
        }
    }
}
