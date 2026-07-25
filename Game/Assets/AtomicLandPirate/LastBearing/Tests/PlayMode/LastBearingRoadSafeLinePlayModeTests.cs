#nullable enable

using System.Collections;
using System.Linq;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed partial class LastBearingPlayModeTests
    {
        [UnityTest]
        public IEnumerator BoneLineWarnsFromCanonicalSteeringAndRestoresAfterLoad()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<
                    LastBearingGameController>();
            Assert.That(controller, Is.Not.Null);
            controller.enabled = false;

            LastBearingState safeLeft =
                CreateSteeredWithoutProgressState(-1, risk: false);
            LastBearingState riskLeft =
                CreateSteeredWithoutProgressState(-1, risk: true);
            LastBearingState safeRight =
                CreateSteeredWithoutProgressState(1, risk: false);
            LastBearingState riskRight =
                CreateSteeredWithoutProgressState(1, risk: true);
            LastBearingState city =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    7626);

            LastBearingWorldBuilder world = controller.World!;
            LastBearingRoadSafeLineView view = world.RoadSafeLineView!;
            Assert.That(
                world.GetComponentsInChildren<LastBearingRoadSafeLineView>(
                    includeInactive: true),
                Has.Length.EqualTo(1));
            Assert.That(
                view.GetComponentsInChildren<Collider>(
                    includeInactive: true),
                Is.Empty);

            InstallControllerState(controller, safeLeft);
            yield return null;
            Assert.That(
                controller.ReadModel!.VehicleLateralMilli,
                Is.EqualTo(-750));
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.Safe));

            InstallControllerState(controller, safeRight);
            yield return null;
            Assert.That(
                controller.ReadModel!.VehicleLateralMilli,
                Is.EqualTo(750));
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.Safe));

            InstallControllerState(controller, riskLeft);
            yield return null;
            Assert.That(
                controller.ReadModel!.VehicleLateralMilli,
                Is.EqualTo(-751));
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.LeftRisk));
            Assert.That(view.IsLeftWarningVisible, Is.True);
            Assert.That(view.IsRightWarningVisible, Is.False);
            AssertAllSafeLineRenderersActive(view, expected: true);

            InstallControllerState(controller, riskRight);
            yield return null;
            Assert.That(
                controller.ReadModel!.VehicleLateralMilli,
                Is.EqualTo(751));
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.RightRisk));
            Assert.That(view.IsRightWarningVisible, Is.True);
            Assert.That(view.IsLeftWarningVisible, Is.False);
            AssertAllSafeLineRenderersActive(view, expected: true);
            Assert.That(
                controller.ReadModel.RouteProgressTicks,
                Is.Zero);
            Assert.That(
                controller.ReadModel.VehicleConditionMilli,
                Is.EqualTo(
                    LastBearingBalanceV1.StartingVehicleConditionMilli));

            Renderer[] markers =
                view.GetComponentsInChildren<Renderer>(true);
            Renderer[] markerInstances = markers
                .OrderBy(item => item.gameObject.name)
                .ToArray();
            byte[] savedBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string savedHash = controller.CanonicalHash;
            controller.Save();
            Assert.That(controller.SaveStatus, Does.Not.Contain("failed"));
            Assert.That(controller.SaveStatus, Does.Not.Contain("deferred"));

            for (var cycle = 0; cycle < 4; cycle++)
            {
                InstallControllerState(controller, city);
                yield return null;
                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.CityOverview));
                Assert.That(
                    view.State,
                    Is.EqualTo(LastBearingRoadSafeLineState.Clear));
                AssertAllSafeLineRenderersActive(view, expected: false);

                InstallControllerState(controller, riskRight);
                yield return null;
                Assert.That(
                    controller.ModeCoordinator.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.Driving));
                Assert.That(
                    view.State,
                    Is.EqualTo(LastBearingRoadSafeLineState.RightRisk));
                Assert.That(view.IsRightWarningVisible, Is.True);
                Assert.That(
                    world.RoadSafeLineView,
                    Is.SameAs(view));
                CollectionAssert.AreEqual(
                    markerInstances,
                    view.GetComponentsInChildren<Renderer>(true)
                        .OrderBy(item => item.gameObject.name)
                        .ToArray());
                Assert.That(
                    world.GetComponentsInChildren<
                        LastBearingRoadSafeLineView>(true),
                    Has.Length.EqualTo(1));
                Assert.That(
                    view.GetComponentsInChildren<Collider>(true),
                    Is.Empty);
                CollectionAssert.AreEqual(
                    savedBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                Assert.That(controller.CanonicalHash, Is.EqualTo(savedHash));
                AssertOneCameraAndListener();
            }

            controller.ReturnToTitle();
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.Clear));
            AssertAllSafeLineRenderersActive(view, expected: false);
            controller.Load();
            yield return null;

            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));
            Assert.That(
                view.State,
                Is.EqualTo(LastBearingRoadSafeLineState.RightRisk));
            Assert.That(view.IsRightWarningVisible, Is.True);
            CollectionAssert.AreEqual(
                savedBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(controller.CanonicalHash, Is.EqualTo(savedHash));
            Assert.That(world.RoadSafeLineView, Is.SameAs(view));
            AssertOneCameraAndListener();
        }

        private static LastBearingState CreateSteeredWithoutProgressState(
            int direction,
            bool risk)
        {
            Assert.That(direction, Is.EqualTo(-1).Or.EqualTo(1));
            var kernel = new LastBearingKernel();
            LastBearingState state = CreateOutboundState();
            long initialProgress = state.RouteProgressTicks;
            long initialCondition = state.VehicleConditionMilli;
            for (var step = 0; step < 15; step++)
            {
                state = kernel.Step(
                    state,
                    new LastBearingCommand[]
                    {
                        new DriveVehicleCommand(
                            state.NextCommandSequence,
                            throttleMilli: 0,
                            steeringMilli: direction * 1000),
                    }).State;
            }

            if (risk)
            {
                state = kernel.Step(
                    state,
                    new LastBearingCommand[]
                    {
                        new DriveVehicleCommand(
                            state.NextCommandSequence,
                            throttleMilli: 0,
                            steeringMilli: direction * 20),
                    }).State;
            }

            Assert.That(
                state.VehicleLateralMilli,
                Is.EqualTo(direction * (risk ? 751 : 750)));
            Assert.That(
                state.RouteProgressTicks,
                Is.EqualTo(initialProgress));
            Assert.That(
                state.VehicleConditionMilli,
                Is.EqualTo(initialCondition));
            return state;
        }

        private static void AssertAllSafeLineRenderersActive(
            LastBearingRoadSafeLineView view,
            bool expected)
        {
            Renderer[] renderers =
                view.GetComponentsInChildren<Renderer>(true);
            Assert.That(
                renderers.Count(item => item.gameObject.activeInHierarchy),
                Is.EqualTo(
                    expected
                        ? LastBearingRoadSafeLineView.SafeLineMarkerCount +
                          LastBearingRoadSafeLineView.SurfaceCount
                        : 0));
        }

        private static void AssertOneCameraAndListener()
        {
            Assert.That(
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
        }
    }
}
