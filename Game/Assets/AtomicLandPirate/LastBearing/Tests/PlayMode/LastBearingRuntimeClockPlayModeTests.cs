#nullable enable

using System.Collections;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingRuntimeClockPlayModeTests
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
        public IEnumerator PausedIdleRetainsCanonicalStateUntilResumeCommand()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            controller.enabled = false;
            controller.TogglePause();
            AdvanceSimulation(controller, 0.1f);

            Assert.That(
                controller.ReadModel?.PauseCause,
                Is.EqualTo(PauseCause.Explicit));
            LastBearingState pausedState = controller.State!;
            string pausedHash = controller.CanonicalHash;

            AdvanceSimulation(controller, 0.8f);

            Assert.That(controller.State, Is.SameAs(pausedState));
            Assert.That(controller.CanonicalHash, Is.EqualTo(pausedHash));

            controller.TogglePause();
            AdvanceSimulation(controller, 0.05f);
            Assert.That(controller.State, Is.SameAs(pausedState));
            AdvanceSimulation(controller, 0.05f);
            Assert.That(
                controller.ReadModel?.PauseCause,
                Is.EqualTo(PauseCause.None));
            Assert.That(controller.State, Is.Not.SameAs(pausedState));
        }

        [UnityTest]
        public IEnumerator SchedulerRetainsTenHertzCadenceAndCommandBoundaries()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            controller.enabled = false;

            long initialTick = controller.State!.GlobalTick;
            AdvanceSimulation(controller, 0.1f);
            Assert.That(
                controller.State.GlobalTick,
                Is.EqualTo(initialTick + 1),
                "One tenth of a second must execute one authoritative tick.");

            for (var tick = 0; tick < 7; tick++)
            {
                AdvanceSimulation(controller, 0.1f);
            }
            Assert.That(
                controller.State.GlobalTick,
                Is.EqualTo(initialTick + 8),
                "Catch-up must retain the original ten-hertz schedule.");

            controller.TogglePause();
            AdvanceSimulation(controller, 0.05f);
            Assert.That(
                controller.ReadModel?.PauseCause,
                Is.EqualTo(PauseCause.None),
                "A command must not cross an unelapsed tick boundary.");
            AdvanceSimulation(controller, 0.05f);
            Assert.That(
                controller.ReadModel?.PauseCause,
                Is.EqualTo(PauseCause.Explicit),
                "A pending command must retain the ordinary 0.1-second cadence.");
        }

        private LastBearingGameController BuildController()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            return controller;
        }

        private static void AdvanceSimulation(
            LastBearingGameController controller,
            float elapsedSeconds)
        {
            MethodInfo? method = typeof(LastBearingGameController).GetMethod(
                "AdvanceSimulation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(controller, new object[] { elapsedSeconds });
        }
    }
}
