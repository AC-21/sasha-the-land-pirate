#nullable enable

using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
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
            LastBearingWorldBuilder world = controller.World;
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            RoadFeelRigInstance roadRig = world.RoadFeelRig!;
            Assert.That(roadRig, Is.Not.Null);
            Assert.That(
                roadRig.Root.transform.IsChildOf(coordinator.GetModeRoot(
                    LastBearingPresentationMode.Driving)),
                Is.True);
            Assert.That(
                roadRig.Root.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(14));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<RoadFeelVehicleController>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<LastBearingRoadFeelModeAdapter>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<RoadFeelLabController>(
                    FindObjectsInactive.Include),
                Is.Empty);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<RoadFeelChaseCamera>(
                    FindObjectsInactive.Include),
                Is.Empty);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(roadRig.Root.activeInHierarchy, Is.False);
            Assert.That(roadRig.Adapter.IsPhysicsSuspended, Is.True);
            Assert.That(roadRig.Vehicle.Body.isKinematic, Is.True);
            Assert.That(roadRig.Vehicle.enabled, Is.False);
            Assert.That(world.VehicleView!.gameObject.activeSelf, Is.True);
            Assert.That(
                world.CameraRig!.RoadTarget,
                Is.SameAs(world.VehicleView.transform));

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
        public IEnumerator OneSceneModeFollowsCanonicalExpeditionSequence()
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
            controller.OpenBuildingCutaway();
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            AssertMode(
                coordinator,
                LastBearingPresentationMode.BuildingCutaway);

            LastBearingState outbound = CreateOutboundState();
            InstallControllerState(controller, outbound);
            AssertMode(coordinator, LastBearingPresentationMode.Driving);
            RoadFeelRigInstance roadRig = controller.World!.RoadFeelRig!;
            AssertRoadPresentation(controller, roadRig, active: true);
            string hashBeforeShadow = controller.CanonicalHash;
            int receiptBefore = roadRig.Adapter.CommandReceiptCount;
            coordinator.ApplyQuantizedRoadCommandShadow(750, -250);
            Assert.That(
                roadRig.Adapter.CommandReceiptCount,
                Is.EqualTo(receiptBefore + 1));
            Assert.That(roadRig.Adapter.LastThrottleMilli, Is.EqualTo(750));
            Assert.That(roadRig.Adapter.LastSteeringMilli, Is.EqualTo(-250));
            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBeforeShadow));

            LastBearingState atDepot = DriveUntilPhase(
                outbound,
                ExpeditionPhase.AtDepot);
            InstallControllerState(controller, atDepot);
            AssertMode(
                coordinator,
                LastBearingPresentationMode.DepotEncounter);
            AssertRoadPresentation(controller, roadRig, active: false);

            var kernel = new LastBearingKernel();
            LastBearingState resolved = Apply(kernel, atDepot, sequence =>
                new ResolveDepotCommand(sequence, EncounterChoice.Cooperate));
            string transactionId = resolved.TransactionId!;
            string fingerprint = resolved.TransactionFingerprint!;
            LastBearingState returning = Apply(kernel, resolved, sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            InstallControllerState(controller, returning);
            AssertMode(coordinator, LastBearingPresentationMode.Driving);
            AssertRoadPresentation(controller, roadRig, active: true);

            LastBearingState returned = DriveUntilPhase(
                returning,
                ExpeditionPhase.Returned);
            InstallControllerState(controller, returned);
            AssertMode(coordinator, LastBearingPresentationMode.CityReturn);
            AssertRoadPresentation(controller, roadRig, active: false);
            Assert.That(
                controller.CanonicalHash,
                Is.EqualTo(LastBearingCanonicalCodec.ComputeSha256(returned)));

            controller.ReturnToTitle();
            Assert.That(coordinator.HasActiveMode, Is.False);
            AssertRoadPresentation(controller, roadRig, active: false);
        }

        [UnityTest]
        public IEnumerator RoadPhysicsSuspendsAcrossPauseAndFixedFrames()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            InstallControllerState(controller, CreateOutboundState());
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            RoadFeelRigInstance roadRig = controller.World!.RoadFeelRig!;
            Rigidbody body = roadRig.Vehicle.Body;

            coordinator.ApplyQuantizedRoadCommandShadow(1000, 0);
            Assert.That(roadRig.Adapter.LastThrottleMilli, Is.EqualTo(1000));
            yield return new WaitForFixedUpdate();
            body.linearVelocity = new Vector3(3f, 1f, 7f);
            body.angularVelocity = new Vector3(0.5f, 1f, 0.25f);

            controller.enabled = false;
            controller.TogglePause();
            InvokeSimulationTick(controller);

            Assert.That(controller.ReadModel!.PauseCause, Is.EqualTo(PauseCause.Explicit));
            AssertMode(coordinator, LastBearingPresentationMode.Driving);
            Assert.That(coordinator.IsRoadPresentationActive, Is.False);
            Assert.That(roadRig.Root.activeInHierarchy, Is.True);
            Assert.That(roadRig.Adapter.IsRoadModeActive, Is.False);
            Assert.That(roadRig.Adapter.IsPhysicsSuspended, Is.True);
            Assert.That(roadRig.Vehicle.enabled, Is.False);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));

            for (var frame = 0; frame < 3; frame++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(body.isKinematic, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ThrowingRoadAdapterCannotBlockCanonicalProgress()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            InstallControllerState(controller, CreateOutboundState());
            controller.enabled = false;
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            var throwingAdapter = new ThrowingRoadAdapter();
            controller.AttachRoadModeAdapter(throwingAdapter);
            long progressBefore = controller.ReadModel!.RouteProgressTicks;
            string hashBefore = controller.CanonicalHash;

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.wKey);
            yield return null;
            LogAssert.Expect(
                LogType.Warning,
                "LAST_BEARING_ROAD_PRESENTATION_DISABLED " +
                "apply-command-shadow InvalidOperationException");
            InvokeSimulationTick(controller);
            Release(keyboard.wKey);
            yield return null;

            Assert.That(throwingAdapter.ApplyAttemptCount, Is.EqualTo(1));
            Assert.That(coordinator.RoadAdapterFaulted, Is.True);
            Assert.That(coordinator.HasRoadAdapter, Is.False);
            Assert.That(coordinator.IsRoadPresentationActive, Is.False);
            Assert.That(
                controller.ReadModel!.RouteProgressTicks,
                Is.GreaterThan(progressBefore));
            Assert.That(controller.CanonicalHash, Is.Not.EqualTo(hashBefore));
            Assert.That(
                controller.World!.RoadFeelRig!.Root.activeInHierarchy,
                Is.False);
            Assert.That(controller.World.VehicleView!.gameObject.activeSelf, Is.True);
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

        private static LastBearingState DriveUntilPhase(
            LastBearingState state,
            ExpeditionPhase target)
        {
            var kernel = new LastBearingKernel();
            var guard = 0;
            while (state.ExpeditionPhase != target && guard < 1000)
            {
                state = Apply(kernel, state, sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                guard++;
            }

            Assert.That(state.ExpeditionPhase, Is.EqualTo(target));
            return state;
        }

        private static void AssertMode(
            LastBearingModeCoordinator coordinator,
            LastBearingPresentationMode expected)
        {
            Assert.That(coordinator.HasActiveMode, Is.True);
            Assert.That(coordinator.CurrentMode, Is.EqualTo(expected));
            Assert.That(coordinator.ActiveModeCount, Is.EqualTo(1));
        }

        private static void AssertRoadPresentation(
            LastBearingGameController controller,
            RoadFeelRigInstance roadRig,
            bool active)
        {
            Assert.That(roadRig.Root.activeInHierarchy, Is.EqualTo(active));
            Assert.That(roadRig.Adapter.IsRoadModeActive, Is.EqualTo(active));
            Assert.That(roadRig.Adapter.IsPhysicsSuspended, Is.EqualTo(!active));
            Assert.That(roadRig.Vehicle.enabled, Is.EqualTo(active));
            Assert.That(roadRig.Vehicle.Body.isKinematic, Is.EqualTo(!active));
            Assert.That(
                controller.World!.VehicleView!.gameObject.activeSelf,
                Is.EqualTo(!active));
            Assert.That(
                controller.World.CameraRig!.RoadTarget,
                active
                    ? Is.SameAs(roadRig.Root.transform)
                    : Is.SameAs(controller.World.VehicleView.transform));
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            MethodInfo? simulate = typeof(LastBearingGameController).GetMethod(
                "SimulateOneTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(simulate, Is.Not.Null);
            simulate!.Invoke(controller, null);
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

        private sealed class ThrowingRoadAdapter : ILastBearingRoadModeAdapter
        {
            public bool IsRoadModeActive { get; private set; }

            public int ApplyAttemptCount { get; private set; }

            public void SetRoadModeActive(bool active)
            {
                IsRoadModeActive = active;
            }

            public void ApplyQuantizedCommandShadow(
                int throttleMilli,
                int steeringMilli)
            {
                ApplyAttemptCount++;
                throw new InvalidOperationException("adversarial-road-adapter");
            }

            public void SynchronizePresentationPose(
                Vector3 position,
                Quaternion rotation)
            {
            }

            public void ResetPresentation()
            {
            }
        }
    }
}
