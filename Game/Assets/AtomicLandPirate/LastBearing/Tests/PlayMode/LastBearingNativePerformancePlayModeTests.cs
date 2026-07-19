#nullable enable

using System.Collections;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Performance;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingNativePerformancePlayModeTests
    {
        private GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }

            foreach (LastBearingGameController controller in
                     Object.FindObjectsByType<LastBearingGameController>(
                         FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(controller.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator InjectedScheduleUsesNaturalModesAndRetainsTopology()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            StageRepresentativeCity(controller);
            LastBearingFieldDesk desk = controller.FieldDesk!;
            LastBearingFieldDeskPerformanceTopology topology =
                desk.CapturePerformanceTopology();

            var clock = new ManualClock();
            var durations = new LastBearingNativePerformanceDurations(
                warmupSeconds: 0.01d,
                pausedSeconds: 0.01d,
                representativeUnpausedSeconds: 0.01d,
                cityGarageCycles: 2,
                cycleHalfDwellSeconds: 0.01d);
            var schedule = new LastBearingNativePerformanceSchedule(
                clock,
                durations);
            Assert.That(
                schedule.Start(),
                Is.EqualTo(LastBearingNativePerformanceAction.BeginWarmup));

            clock.Advance(0.01d);
            Apply(schedule.Advance(isPaused: false), controller);
            ApplyPendingTick(controller);
            Assert.That(
                controller.ReadModel?.PauseCause,
                Is.EqualTo(PauseCause.Explicit));
            Apply(schedule.Advance(isPaused: true), controller);

            clock.Advance(0.01d);
            Apply(schedule.Advance(isPaused: true), controller);
            ApplyPendingTick(controller);
            Assert.That(
                controller.ReadModel?.PauseCause,
                Is.EqualTo(PauseCause.None));
            Apply(schedule.Advance(isPaused: false), controller);

            // Cross the decimal duration boundary instead of depending on
            // exact binary floating-point equality at 0.03 - 0.02.
            clock.Advance(0.02d);
            Apply(schedule.Advance(isPaused: false), controller);
            ApplyPendingTick(controller);
            Apply(schedule.Advance(isPaused: true), controller);

            for (var cycle = 0; cycle < 2; cycle++)
            {
                Apply(schedule.Advance(isPaused: true), controller);
                Assert.That(
                    controller.ModeCoordinator?.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.GarageBay));
                yield return null;
                clock.Advance(0.01d);
                Apply(schedule.Advance(isPaused: true), controller);
                Assert.That(
                    controller.ModeCoordinator?.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.CityOverview));
                Assert.That(
                    desk.MatchesPerformanceTopology(topology),
                    Is.True);
                yield return null;
                clock.Advance(0.01d);
            }

            Apply(schedule.Advance(isPaused: true), controller);
            ApplyPendingTick(controller);
            Apply(schedule.Advance(isPaused: false), controller);

            Assert.That(
                schedule.Stage,
                Is.EqualTo(LastBearingNativePerformanceStage.Complete));
            Assert.That(schedule.CompletedCityGarageCycles, Is.EqualTo(2));
            Assert.That(desk.MatchesPerformanceTopology(topology), Is.True);
            Assert.That(topology.BindingCount, Is.EqualTo(18));
            Assert.That(topology.RegisteredCallbackCount, Is.EqualTo(19));
            Assert.That(topology.OwnedUnityObjectCount, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator RuntimeTargetRequiresOneActiveCanonicalController()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            LastBearingGameController[] controllers =
                Object.FindObjectsByType<LastBearingGameController>(
                    FindObjectsInactive.Include);
            Assert.That(
                LastBearingNativePerformanceRuntimeTarget
                    .TrySelectCanonicalController(
                        controllers,
                        out LastBearingGameController? selected,
                        out string error),
                Is.True,
                error);
            Assert.That(selected, Is.SameAs(controller));

            controller.gameObject.name = "stale Last Bearing root";
            Assert.That(
                LastBearingNativePerformanceRuntimeTarget
                    .TrySelectCanonicalController(
                        controllers,
                        out _,
                        out _),
                Is.False,
                "a renamed runtime root must fail closed");
            controller.gameObject.name = LastBearingGameController.RuntimeRootName;

            var duplicateRoot = new GameObject("duplicate Last Bearing root");
            duplicateRoot.AddComponent<LastBearingGameController>();
            yield return null;
            controllers = Object.FindObjectsByType<LastBearingGameController>(
                FindObjectsInactive.Include);
            Assert.That(controllers, Has.Length.EqualTo(2));
            Assert.That(
                LastBearingNativePerformanceRuntimeTarget
                    .TrySelectCanonicalController(
                        controllers,
                        out _,
                        out _),
                Is.False,
                "duplicate runtime roots must fail closed");
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
            controller.ShowCityOverview();
            controller.FieldDesk?.Refresh(force: true);
        }

        private static void Apply(
            LastBearingNativePerformanceAction action,
            LastBearingGameController controller)
        {
            switch (action)
            {
                case LastBearingNativePerformanceAction
                    .RequestPauseForPausedMeasurement:
                case LastBearingNativePerformanceAction
                    .EndRepresentativeUnpausedMeasurementAndRequestPause:
                case LastBearingNativePerformanceAction
                    .EndPausedMeasurementAndRequestResume:
                    controller.TogglePause();
                    break;
                case LastBearingNativePerformanceAction
                    .EndCityGarageCyclesAndSubmitResume:
                    SubmitPostCyclePauseTwice(controller);
                    break;
                case LastBearingNativePerformanceAction.ShowGarage:
                    controller.OpenGarageBay();
                    controller.FieldDesk?.Refresh(force: true);
                    break;
                case LastBearingNativePerformanceAction.ShowCity:
                    controller.ShowCityOverview();
                    controller.FieldDesk?.Refresh(force: true);
                    break;
            }
        }

        private static void ApplyPendingTick(
            LastBearingGameController controller)
        {
            MethodInfo? method = typeof(LastBearingGameController).GetMethod(
                "SimulateOneTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(controller, null);
        }

        private static void SubmitPostCyclePauseTwice(
            LastBearingGameController controller)
        {
            LastBearingFieldDesk? desk = controller.FieldDesk;
            Assert.That(desk, Is.Not.Null);
            MethodInfo? method = typeof(LastBearingFieldDesk).GetMethod(
                "TrySubmitPauseTwiceForNativePerformanceGate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { 0, 0 };
            object? result = method!.Invoke(desk, arguments);
            Assert.That(result, Is.EqualTo(true));
            Assert.That(arguments[0], Is.EqualTo(1));
            Assert.That(arguments[1], Is.EqualTo(0));
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
