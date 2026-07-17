#nullable enable

using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingPlayModeTests : InputTestFixture
    {
        private const string SceneName = "LastBearing";

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene("LastBearing_TestCleanup");
                SceneManager.SetActiveScene(cleanup);
                AsyncOperation? unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SceneBootsOnePlayableRuntimeWithMixedResidents()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController[] controllers =
                UnityEngine.Object.FindObjectsByType<LastBearingGameController>(
                    FindObjectsInactive.Include);
            Assert.That(controllers, Has.Length.EqualTo(1));
            LastBearingGameController controller = controllers[0];
            Assert.That(controller.name, Is.EqualTo(LastBearingGameController.RuntimeRootName));
            Assert.That(controller.World, Is.Not.Null);
            Assert.That(controller.World!.MainCamera, Is.Not.Null);

            controller.StartNewGame(ColonyComposition.Mixed);
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(controller.ReadModel, Is.Not.Null);
            Assert.That(controller.ReadModel!.Composition, Is.EqualTo(ColonyComposition.Mixed));
            Assert.That(
                controller.ReadModel.AssignedResidentId,
                Is.EqualTo(ResidentRoster.HumanResidentId));
            Assert.That(
                GameObject.Find("Resident " + ResidentRoster.HumanResidentId),
                Is.Not.Null);
            Assert.That(
                GameObject.Find("Resident " + ResidentRoster.RobotResidentId),
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator StrategyCameraRespondsToInputWithoutChangingCoreState()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.StartNewGame(ColonyComposition.HumanOnly);
            yield return new WaitForSecondsRealtime(0.15f);

            LastBearingCameraRig rig = controller.World!.CameraRig!;
            Vector3 originalFocus = rig.CityFocus;
            string originalHash = controller.CanonicalHash;
            controller.enabled = false;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.dKey);
            yield return null;
            Release(keyboard.dKey);
            yield return null;

            Assert.That(rig.CityFocus, Is.Not.EqualTo(originalFocus));
            Assert.That(originalHash, Is.Not.EqualTo("none"));
            Assert.That(controller.CanonicalHash, Is.EqualTo(originalHash));
        }

        [UnityTest]
        public IEnumerator CityGrammarHypothesesShareOneLockedCameraAndCoreState()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.StartNewGame(ColonyComposition.HumanOnly);
            controller.enabled = false;
            controller.InspectCityNeed();
            string originalHash = controller.CanonicalHash;
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            LastBearingCameraRig rig = controller.World!.CameraRig!;
            Vector3 fixedFocus = rig.CityFocus;
            float fixedYaw = rig.CityYaw;
            float fixedDistance = rig.CityDistance;

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.dKey);
            yield return null;
            Release(keyboard.dKey);
            yield return null;
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);

            Assert.That(rig.IsComparisonMode, Is.True);
            Assert.That(rig.CityFocus, Is.EqualTo(fixedFocus));
            Assert.That(rig.CityYaw, Is.EqualTo(fixedYaw));
            Assert.That(rig.CityDistance, Is.EqualTo(fixedDistance));
            Assert.That(controller.CanonicalHash, Is.EqualTo(originalHash));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("selections=2"));
        }

        [UnityTest]
        public IEnumerator RoadSteeringChangesCanonicalLateralAndVisibleVehiclePose()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            LastBearingState outbound = CreateOutboundState();
            InstallControllerState(controller, outbound);
            LastBearingVehicleView vehicle = controller.World!.VehicleView!;
            Vector3 positionBefore = vehicle.transform.position;
            long routeProgressBefore = controller.ReadModel!.RouteProgressTicks;

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.dKey);
            yield return new WaitForSecondsRealtime(0.16f);
            Release(keyboard.dKey);
            yield return null;

            Assert.That(controller.ReadModel, Is.Not.Null);
            Assert.That(controller.ReadModel!.VehicleLateralMilli, Is.GreaterThan(0));
            Assert.That(controller.ReadModel.RouteProgressTicks, Is.EqualTo(routeProgressBefore));
            Assert.That(vehicle.VisibleLateralOffset, Is.GreaterThan(0f));
            Assert.That(vehicle.VisibleBodyLeanDegrees, Is.LessThan(0f));
            Assert.That(vehicle.FrontWheelSteerDegrees, Is.GreaterThan(0f));
            Assert.That(vehicle.transform.position, Is.Not.EqualTo(positionBefore));
        }

        private static LastBearingState CreateOutboundState()
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2011);
            state = Apply(kernel, state, sequence =>
                new AssignResidentCommand(sequence, ResidentRoster.HumanResidentId));
            state = Apply(kernel, state, sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            state = Apply(kernel, state, sequence =>
                new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.WorkshopPush,
                    VehicleModule.WinchAssembly));
            state = Apply(kernel, state, sequence =>
                new InstallVehicleModuleCommand(sequence, VehicleModule.WinchAssembly));
            var guard = 0;
            while (state.PreparationPhase != PreparationPhase.Ready && guard < 1000)
            {
                state = kernel.Step(
                    state,
                    Array.Empty<LastBearingCommand>()).State;
                guard++;
            }

            Assert.That(state.PreparationPhase, Is.EqualTo(PreparationPhase.Ready));
            const string transactionId = "tx:unity-steering-test";
            const string fingerprint = "fp:unity-steering-test";
            state = Apply(kernel, state, sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = Apply(kernel, state, sequence =>
                new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            Assert.That(state.ExpeditionPhase, Is.EqualTo(ExpeditionPhase.Outbound));
            return state;
        }

        private static LastBearingState Apply(
            LastBearingKernel kernel,
            LastBearingState state,
            Func<long, LastBearingCommand> create)
        {
            return kernel.Step(
                state,
                new[] { create(state.NextCommandSequence) }).State;
        }

        private static void InstallControllerState(
            LastBearingGameController controller,
            LastBearingState state)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo? stateField = typeof(LastBearingGameController).GetField(
                "_state",
                flags);
            FieldInfo? readModelField = typeof(LastBearingGameController).GetField(
                "_readModel",
                flags);
            FieldInfo? pendingField = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                flags);
            MethodInfo? applyPresentation = typeof(LastBearingGameController).GetMethod(
                "ApplyPresentation",
                flags);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(readModelField, Is.Not.Null);
            Assert.That(pendingField, Is.Not.Null);
            Assert.That(applyPresentation, Is.Not.Null);

            stateField!.SetValue(controller, state);
            readModelField!.SetValue(controller, LastBearingReadModel.FromState(state));
            var pending = pendingField!.GetValue(controller) as List<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            pending!.Clear();
            applyPresentation!.Invoke(controller, null);
        }
    }
}
