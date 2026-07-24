#nullable enable

using System.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using AtomicLandPirate.Save.LastBearing;
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
        private readonly List<string> _temporarySaveRoots = new List<string>();
        private readonly Dictionary<LastBearingGameController, string>
            _temporaryProfilesByController =
                new Dictionary<LastBearingGameController, string>();

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

            foreach (string root in _temporarySaveRoots)
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            _temporarySaveRoots.Clear();
            _temporaryProfilesByController.Clear();

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
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<LastBearingCameraRig>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Camera sharedCamera = world.MainCamera!;
            Assert.That(world.CameraRig!.gameObject, Is.SameAs(sharedCamera.gameObject));
            Assert.That(
                world.RoadChaseCamera!.gameObject,
                Is.SameAs(sharedCamera.gameObject));
            Assert.That(
                sharedCamera.GetComponent<AudioListener>()!.gameObject,
                Is.SameAs(sharedCamera.gameObject));
            Assert.That(world.CameraRig.HasConfiguredRoadChase, Is.True);
            AssertCameraOwnership(controller, chaseActive: false);
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.RecoverRoadPresentation(), Is.False);
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
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.RecoverRoadPresentation(), Is.False);
            AssertCameraOwnership(controller, chaseActive: false);
        }

        [UnityTest]
        public IEnumerator WorkingServiceCellSaveTitleLoadAndFallbackAreExact()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();

            controller.SelectCityBuildingPreview(CityBuildingKind.Recycler);
            controller.RotateCityBuildingPreview();
            controller.PlaceCityBuildingPreview();
            InvokeSimulationTick(controller);
            controller.SelectCityBuildingPreview(CityBuildingKind.MachineShop);
            controller.PlaceCityBuildingPreview();
            InvokeSimulationTick(controller);
            controller.SelectCityBuildingPreview(
                CityBuildingKind.EmergencyStorage);
            controller.PlaceCityBuildingPreview();
            InvokeSimulationTick(controller);
            controller.SelectCityBuildingPreview(CityBuildingKind.Recycler);
            controller.MoveCityBuildingPreview(-1);
            controller.RotateCityBuildingPreview();
            controller.PlaceCityBuildingPreview();
            InvokeSimulationTick(controller);
            controller.ConnectCityServiceLink();
            InvokeSimulationTick(controller);
            controller.AssignCityServiceResident(ResidentRoster.RobotResidentId);
            InvokeSimulationTick(controller);
            controller.AdvanceCityServiceSled();
            InvokeSimulationTick(controller);

            LastBearingReadModel partial = controller.ReadModel!;
            string partialHash = controller.CanonicalHash;
            long partialParts = partial.PartsUnits;
            Assert.That(partial.RecyclerPadIndex, Is.EqualTo(4));
            Assert.That(partial.RecyclerQuarterTurns, Is.EqualTo(2));
            Assert.That(partial.MachineShopPadIndex, Is.EqualTo(1));
            Assert.That(partial.EmergencyStoragePadIndex, Is.EqualTo(2));
            Assert.That(partial.CityServiceLinkConnected, Is.True);
            Assert.That(
                partial.CityServiceResidentId,
                Is.EqualTo(ResidentRoster.RobotResidentId));
            Assert.That(
                partial.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.InTransit));
            Assert.That(partial.CityDeliveryCount, Is.Zero);
            Assert.That(
                partial.NextObjective,
                Is.EqualTo("advance-city-service-sled"));

            controller.Save();
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));
            controller.ReturnToTitle();
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(partialHash));
            Assert.That(controller.ReadModel!.RecyclerPadIndex, Is.EqualTo(4));
            Assert.That(controller.ReadModel.RecyclerQuarterTurns, Is.EqualTo(2));
            Assert.That(
                controller.ReadModel.CityServiceResidentId,
                Is.EqualTo(ResidentRoster.RobotResidentId));
            Assert.That(
                controller.ReadModel.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.InTransit));
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(partialParts));

            controller.AdvanceCityServiceSled();
            InvokeSimulationTick(controller);
            string deliveredHash = controller.CanonicalHash;
            Assert.That(controller.ReadModel.CityDeliveryCount, Is.EqualTo(1));
            Assert.That(
                controller.ReadModel.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.DeliveredToWorkshop));
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(partialParts + 2));
            Assert.That(
                controller.ReadModel.NextObjective,
                Is.EqualTo("select-preparation-and-module"));

            controller.ReturnToTitle();
            controller.Load();
            yield return null;
            Assert.That(controller.CanonicalHash, Is.EqualTo(deliveredHash));
            Assert.That(controller.ReadModel!.CityDeliveryCount, Is.EqualTo(1));

            controller.ReturnToTitle();
            File.WriteAllBytes(
                Path.Combine(
                    profileDirectory,
                    LastBearingProfileContract.CurrentPointerName),
                new byte[] { 0 });
            controller.Load();
            yield return null;

            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.RecoveredLastGood + " ·"));
            Assert.That(controller.CanonicalHash, Is.EqualTo(partialHash));
            Assert.That(
                controller.ReadModel!.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.InTransit));
            Assert.That(controller.ReadModel.CityDeliveryCount, Is.Zero);
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(partialParts));
        }

        [UnityTest]
        public IEnumerator HandsOnServiceCellRunsFromWorldAndSurvivesFourModeTransitions()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            _ = InstallTemporarySaveAdapter(controller);
            controller.StartNewGame(ColonyComposition.HumanOnly);
            controller.InspectCityNeed();
            yield return null;

            LastBearingCityServiceCellInteractor interactor =
                controller.World!.CityServiceCellView!.Interactor!;
            Assert.That(interactor, Is.Not.Null);
            Assert.That(interactor.IsBuilt, Is.True);
            Assert.That(interactor.HasDedicatedInteractionTargets, Is.True);
            AssertRenderedRootInsideViewport(
                controller.World.MainCamera!,
                interactor.transform);
            long startingParts = controller.ReadModel!.PartsUnits;
            string canonicalBeforePreview = controller.CanonicalHash;

            Assert.That(
                controller.FieldDesk!.BlocksWorldPointer(
                    new Vector2(30f, Screen.height * 0.5f)),
                Is.True);
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.RecyclerSelectorName);
            interactor.HoverPad(0);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBeforePreview));
            Assert.That(interactor.HighlightedPadIndex, Is.EqualTo(0));
            Assert.That(interactor.IsPadHighlighted(0), Is.True);

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.rKey);
            yield return null;
            Release(keyboard.rKey);
            Assert.That(controller.CityPreviewQuarterTurns, Is.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBeforePreview));

            ActivateWorldTarget(
                controller,
                interactor,
                "INTERACT_WORK_PAD_01");
            Assert.That(controller.HasPendingPlayerCommands, Is.True);
            InvokeSimulationTick(controller);
            Assert.That(controller.ReadModel!.RecyclerPadIndex, Is.EqualTo(0));
            Assert.That(controller.ReadModel.RecyclerQuarterTurns, Is.EqualTo(1));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(startingParts - 2));

            long partsBeforeFreeMove = controller.ReadModel.PartsUnits;
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.RecyclerSelectorName);
            interactor.HoverPad(4);
            interactor.RotatePreview();
            ActivateWorldTarget(
                controller,
                interactor,
                "INTERACT_WORK_PAD_05");
            InvokeSimulationTick(controller);
            Assert.That(controller.ReadModel.RecyclerPadIndex, Is.EqualTo(4));
            Assert.That(controller.ReadModel.RecyclerQuarterTurns, Is.EqualTo(2));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(partsBeforeFreeMove),
                "pre-lock reposition must remain free");

            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.MachineShopSelectorName);
            interactor.HoverPad(4);
            Assert.That(interactor.LastInteractionRejected, Is.True);
            Assert.That(interactor.Feedback, Does.Contain("OCCUPIED"));
            TextMesh feedbackLabel = RequireNamed(
                    interactor.transform,
                    LastBearingCityServiceCellInteractor.FeedbackLabelName)
                .GetComponent<TextMesh>();
            Assert.That(feedbackLabel.text, Is.EqualTo(interactor.Feedback));
            Assert.That(
                feedbackLabel.color.r,
                Is.GreaterThan(feedbackLabel.color.g));
            interactor.HoverPad(1);
            Assert.That(interactor.LastInteractionRejected, Is.False);
            ActivateWorldTarget(
                controller,
                interactor,
                "INTERACT_WORK_PAD_02");
            InvokeSimulationTick(controller);
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.EmergencyStorageSelectorName);
            interactor.HoverPad(2);
            ActivateWorldTarget(
                controller,
                interactor,
                "INTERACT_WORK_PAD_03");
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(startingParts - 6));

            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.MachineShopIntakeSocketName);
            Assert.That(interactor.LastInteractionRejected, Is.True);
            Assert.That(interactor.Feedback, Does.Contain("OUTPUT FIRST"));
            Assert.That(controller.HasPendingPlayerCommands, Is.False);
            long partsBeforeLink = controller.ReadModel.PartsUnits;
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.RecyclerOutputSocketName);
            Assert.That(interactor.IsLinkSourceSelected, Is.True);
            controller.SelectCityBuildingPreview(CityBuildingKind.MachineShop);
            Assert.That(interactor.IsLinkSourceSelected, Is.False);
            ActivateWorldTarget(
                controller,
                interactor,
                "INTERACT_WORK_PAD_02");
            InvokeSimulationTick(controller);
            Assert.That(
                controller.HasCityBuildingPreview,
                Is.False,
                "An accepted same-pad placement should close the preview so world sockets are operable again.");
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(partsBeforeLink));
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.RecyclerOutputSocketName);
            Assert.That(
                interactor.IsLinkSourceSelected,
                Is.True,
                interactor.Feedback);
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.MachineShopIntakeSocketName);
            Assert.That(
                controller.HasPendingPlayerCommands,
                Is.True,
                interactor.Feedback + " · " + controller.Status);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.CityServiceLinkConnected,
                Is.True,
                interactor.Feedback + " · " + controller.Status);
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(partsBeforeLink - 1));

            interactor.SelectBuilding(CityBuildingKind.Recycler);
            Assert.That(interactor.LastInteractionRejected, Is.True);
            Assert.That(interactor.Feedback, Does.Contain("LAYOUT LOCKED"));
            Assert.That(controller.HasCityBuildingPreview, Is.False);

            interactor.SelectResident(ResidentRoster.RobotResidentId);
            Assert.That(interactor.LastInteractionRejected, Is.True);
            Assert.That(interactor.Feedback, Does.Contain("not in this colony"));
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.HumanResidentTokenName);
            Assert.That(
                interactor.SelectedResidentId,
                Is.EqualTo(ResidentRoster.HumanResidentId));
            controller.OpenBuildingCutaway(); // City → Building
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.BuildingCutaway));
            yield return null;
            Assert.That(interactor.SelectedResidentId, Is.Null);
            controller.ShowCityOverview(); // Building → City
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Transform returningHumanToken = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor.HumanResidentTokenName);
            Transform returningOperatorSocket = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor.OperatorSocketName);
            Camera returningCityCamera = controller.World.MainCamera!;
            float cityCameraDeadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < cityCameraDeadline)
            {
                Vector3 tokenScreen = returningCityCamera
                    .WorldToScreenPoint(returningHumanToken.position);
                Vector3 socketScreen = returningCityCamera
                    .WorldToScreenPoint(returningOperatorSocket.position);
                bool tokenVisible = tokenScreen.z > 0f &&
                    tokenScreen.x >= 0f &&
                    tokenScreen.x <= Screen.width &&
                    tokenScreen.y >= 0f &&
                    tokenScreen.y <= Screen.height;
                bool socketVisible = socketScreen.z > 0f &&
                    socketScreen.x >= 0f &&
                    socketScreen.x <= Screen.width &&
                    socketScreen.y >= 0f &&
                    socketScreen.y <= Screen.height;
                if (tokenVisible && socketVisible)
                {
                    break;
                }

                yield return null;
            }
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.HumanResidentTokenName);
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.OperatorSocketName);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.CityServiceResidentId,
                Is.EqualTo(ResidentRoster.HumanResidentId));
            AssertRenderedRootInsideViewport(
                controller.World.MainCamera!,
                interactor.transform);

            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.SledDestinationName);
            Assert.That(interactor.LastInteractionRejected, Is.True);
            Assert.That(interactor.Feedback, Does.Contain("RECYCLER FIRST"));
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.SledInteractionName);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.InTransit));
            AssertRenderedRootInsideViewport(
                controller.World.MainCamera!,
                interactor.transform);
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.SledInteractionName);
            Assert.That(interactor.LastInteractionRejected, Is.True);
            Assert.That(interactor.Feedback, Does.Contain("IN TRANSIT"));
            long partsBeforeDelivery = controller.ReadModel.PartsUnits;
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingCityServiceCellInteractor.SledDestinationName);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.DeliveredToWorkshop));
            Assert.That(controller.ReadModel.CityDeliveryCount, Is.EqualTo(1));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(partsBeforeDelivery + 2));
            interactor.ClickSledDestination();
            Assert.That(controller.HasPendingPlayerCommands, Is.False);
            Assert.That(controller.ReadModel.CityDeliveryCount, Is.EqualTo(1));

            controller.Save();
            string savedHash = controller.CanonicalHash;
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));

            controller.ReturnToTitle(); // City → Title
            Assert.That(controller.HasActiveGame, Is.False);
            controller.Load(); // Title → City
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(savedHash));
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(controller.ReadModel!.RecyclerPadIndex, Is.EqualTo(4));
            Assert.That(controller.ReadModel.RecyclerQuarterTurns, Is.EqualTo(2));
            Assert.That(controller.ReadModel.CityServiceLinkConnected, Is.True);
            Assert.That(
                controller.ReadModel.CityServiceResidentId,
                Is.EqualTo(ResidentRoster.HumanResidentId));
            Assert.That(controller.ReadModel.CityDeliveryCount, Is.EqualTo(1));
            Assert.That(interactor.IsLinkSourceSelected, Is.False);
            Assert.That(interactor.SelectedResidentId, Is.Null);
        }

        [UnityTest]
        public IEnumerator HandsOnServiceCellMouseInputPlacesRecyclerOnNaturalTick()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            _ = InstallTemporarySaveAdapter(controller);
            controller.StartNewGame(ColonyComposition.HumanOnly);
            controller.InspectCityNeed();
            yield return null;

            LastBearingCityServiceCellInteractor interactor =
                controller.World!.CityServiceCellView!.Interactor!;
            Camera camera = controller.World.MainCamera!;
            long startingParts = controller.ReadModel!.PartsUnits;
            var mouse = InputSystem.AddDevice<Mouse>();

            Transform recyclerSelector = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor.RecyclerSelectorName);
            Vector3 selectorScreen = camera.WorldToScreenPoint(
                recyclerSelector.position);
            Set(
                mouse.position,
                new Vector2(selectorScreen.x, selectorScreen.y));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.That(controller.HasCityBuildingPreview, Is.True);
            Assert.That(
                controller.CityPreviewBuilding,
                Is.EqualTo(CityBuildingKind.Recycler));

            Transform firstPad = RequireNamed(
                interactor.transform,
                "INTERACT_WORK_PAD_01");
            Vector3 padScreen = camera.WorldToScreenPoint(firstPad.position);
            Set(mouse.position, new Vector2(padScreen.x, padScreen.y));
            yield return null;
            Assert.That(interactor.IsPadHighlighted(0), Is.True);
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);

            float placementDeadline = Time.realtimeSinceStartup + 2f;
            while (controller.ReadModel!.RecyclerPadIndex != 0 &&
                   Time.realtimeSinceStartup < placementDeadline)
            {
                yield return null;
            }

            Assert.That(controller.ReadModel!.RecyclerPadIndex, Is.EqualTo(0));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(startingParts - 2));
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
        public IEnumerator CityCameraEInputDoesNotInvokeDepotRecovery()
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
            LastBearingCameraRig rig = controller.World!.CameraRig!;
            float originalYaw = rig.CityYaw;
            string originalStatus = controller.Status;
            string originalHash = controller.CanonicalHash;

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            InvokeGlobalShortcuts(controller);
            yield return null;
            Release(keyboard.eKey);
            yield return null;

            Assert.That(rig.CityYaw, Is.GreaterThan(originalYaw));
            Assert.That(controller.Status, Is.EqualTo(originalStatus));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
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
            AssertCameraOwnership(controller, chaseActive: false);
            AssertRecoveryUnavailableWithoutWrites(controller);
            string originalHash = controller.CanonicalHash;
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            LastBearingCameraRig rig = controller.World!.CameraRig!;
            LastBearingCityGrammarComparison comparison =
                controller.World.CityGrammarComparison!;
            Vector3 fixedFocus = rig.CityFocus;
            float fixedYaw = rig.CityYaw;
            float fixedDistance = rig.CityDistance;

            controller.ManipulateCityGrammarPrimary();
            controller.ToggleCityGrammarTrialPiece();
            controller.ManipulateCityGrammarPrimary();
            controller.ManipulateCityGrammarPrimary();
            controller.ConnectCityGrammarLogistics();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);

            Assert.That(comparison.TrialReady, Is.True);
            Assert.That(
                comparison.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name == "Shared Empty Calibration Sled"),
                Is.EqualTo(2));

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.dKey);
            yield return null;
            Release(keyboard.dKey);
            yield return null;
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: false);

            Assert.That(rig.IsComparisonMode, Is.True);
            Assert.That(rig.CityFocus, Is.EqualTo(fixedFocus));
            Assert.That(rig.CityYaw, Is.EqualTo(fixedYaw));
            Assert.That(rig.CityDistance, Is.EqualTo(fixedDistance));
            Assert.That(controller.CanonicalHash, Is.EqualTo(originalHash));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("selections=2"));
            Assert.That(comparison.CompletedObservationCount, Is.EqualTo(2));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("path=Clear"));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("path=Unclear"));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator CityGrammarTrialClearsOnLoadAndActualDepartureOnly()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            _ = InstallTemporarySaveAdapter(controller);
            controller.StartNewGame(ColonyComposition.HumanOnly);
            controller.InspectCityNeed();
            CompleteDistrictObservation(controller, clear: true);
            controller.Save();

            Assert.That(controller.HasCompletedCityGrammarObservation, Is.True);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));

            controller.Load();

            Assert.That(controller.HasCompletedCityGrammarObservation, Is.False);
            Assert.That(controller.CityGrammarEvidence, Does.Contain("observations=0"));

            CompleteDistrictObservation(controller, clear: false);
            controller.CommitExpedition();

            Assert.That(
                controller.HasCompletedCityGrammarObservation,
                Is.True,
                "a wrong-mode departure attempt is not a canonical phase transition");
            Assert.That(
                controller.HasPendingPlayerCommands,
                Is.False,
                "wrong-mode departure must fail before a core command is queued");
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtHome));
            Assert.That(controller.HasCompletedCityGrammarObservation, Is.True);

            InstallControllerState(
                controller,
                CreateAtHomeModuleState(VehicleModule.WinchAssembly));
            controller.OpenGarageBay();
            controller.CommitExpedition();
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(controller.HasCompletedCityGrammarObservation, Is.False);
            Assert.That(
                controller.CityGrammarHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.Unselected));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("observations=0"));
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
            AssertCameraOwnership(controller, chaseActive: false);
            AssertRecoveryUnavailableWithoutWrites(controller);

            LastBearingState outbound = CreateOutboundState();
            InstallControllerState(controller, outbound);
            AssertMode(coordinator, LastBearingPresentationMode.Driving);
            RoadFeelRigInstance roadRig = controller.World!.RoadFeelRig!;
            AssertRoadPresentation(controller, roadRig, active: true);
            AssertCameraOwnership(controller, chaseActive: true);
            string hashBeforeShadow = controller.CanonicalHash;
            int receiptBefore = roadRig.Adapter.CommandReceiptCount;
            coordinator.ApplyQuantizedRoadCommandShadow(750, -250);
            Assert.That(
                roadRig.Adapter.CommandReceiptCount,
                Is.EqualTo(receiptBefore + 1));
            Assert.That(roadRig.Adapter.LastThrottleMilli, Is.EqualTo(750));
            Assert.That(roadRig.Adapter.LastSteeringMilli, Is.EqualTo(-250));
            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBeforeShadow));

            LastBearingState recoveryGate =
                DriveUntilDepotRecoveryAvailable(outbound);
            InstallControllerState(controller, recoveryGate);
            AssertMode(coordinator, LastBearingPresentationMode.Driving);
            AssertRoadRecoveryHold(controller, roadRig);
            AssertCameraOwnership(controller, chaseActive: false);
            Assert.That(
                controller.World.DepotApproachRecoveryView!.State,
                Is.EqualTo(DepotApproachRecoveryPresentationState.Available));

            var kernel = new LastBearingKernel();
            LastBearingState atDepot = Apply(kernel, recoveryGate, sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            InstallControllerState(controller, atDepot);
            AssertMode(
                coordinator,
                LastBearingPresentationMode.DepotEncounter);
            AssertRoadPresentation(controller, roadRig, active: false);
            AssertCameraOwnership(controller, chaseActive: false);
            AssertRecoveryUnavailableWithoutWrites(controller);
            Assert.That(
                controller.World.DepotApproachRecoveryView!.State,
                Is.EqualTo(DepotApproachRecoveryPresentationState.Unlocked));

            LastBearingState resolved = Apply(kernel, atDepot, sequence =>
                new ResolveDepotCommand(sequence, EncounterChoice.Cooperate));
            LastBearingState loaded = Apply(kernel, resolved, sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            string transactionId = resolved.TransactionId!;
            string fingerprint = resolved.TransactionFingerprint!;
            LastBearingState returning = Apply(kernel, loaded, sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            InstallControllerState(controller, returning);
            AssertMode(coordinator, LastBearingPresentationMode.Driving);
            AssertRoadPresentation(controller, roadRig, active: true);
            AssertCameraOwnership(controller, chaseActive: true);

            LastBearingState returned = DriveUntilPhase(
                returning,
                ExpeditionPhase.Returned);
            InstallControllerState(controller, returned);
            AssertMode(coordinator, LastBearingPresentationMode.CityReturn);
            AssertRoadPresentation(controller, roadRig, active: false);
            AssertCameraOwnership(controller, chaseActive: false);
            AssertRecoveryUnavailableWithoutWrites(controller);
            Assert.That(
                controller.CanonicalHash,
                Is.EqualTo(LastBearingCanonicalCodec.ComputeSha256(returned)));

            controller.ReturnToTitle();
            Assert.That(coordinator.HasActiveMode, Is.False);
            AssertRoadPresentation(controller, roadRig, active: false);
            AssertCameraOwnership(controller, chaseActive: false);
        }

        [UnityTest]
        public IEnumerator DepotRecoveryUsesCanonicalGateNotRoadRigPose()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            LastBearingState outbound = CreateOutboundState();
            InstallControllerState(controller, outbound);
            LastBearingWorldBuilder world = controller.World!;
            RoadFeelRigInstance roadRig = world.RoadFeelRig!;
            Rigidbody body = roadRig.Vehicle.Body;
            LastBearingDepotApproachInteractor interactor =
                world.DepotApproachInteractor!;
            Transform interactionAnchor =
                world.DepotApproachRecoveryView!.InteractionAnchor!;
            string outboundHash = controller.CanonicalHash;

            body.isKinematic = true;
            body.position = interactionAnchor.position;
            controller.OperateDepotApproachRecoveryPoint();

            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.EqualTo(outboundHash));
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));

            LastBearingState recoveryGate =
                DriveUntilDepotRecoveryAvailable(outbound);
            InstallControllerState(controller, recoveryGate);
            yield return null;
            Assert.That(interactor.IsTargetVisible, Is.True);
            Assert.That(interactor.IsFocused, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            Release(keyboard.eKey);
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);
            AssertRoadRecoveryHold(controller, roadRig);
            long arrivalProgress =
                controller.State!.ArrivalFactionClaimProgressMilli;
            long factionProgress = controller.State.FactionClaimProgressMilli;
            for (var tick = 0; tick < 8; tick++)
            {
                InvokeSimulationTick(controller);
            }

            Assert.That(
                controller.State.FactionClaimProgressMilli,
                Is.GreaterThan(factionProgress));
            Assert.That(
                controller.State.ArrivalFactionClaimProgressMilli,
                Is.EqualTo(arrivalProgress));
            Assert.That(
                controller.ReadModel!.IsDepotApproachRecoveryAvailable,
                Is.True);
            AssertRoadRecoveryHold(controller, roadRig);
            string gateHash = controller.CanonicalHash;
            Assert.That(
                Vector3.Distance(
                    interactor.TargetWorldPosition,
                    body.position),
                Is.GreaterThan(2f),
                "the depot bridle dog must sit clear of Sasha's chassis");
            _ = RequireUnblockedScreenPoint(
                controller,
                interactor.TargetWorldPosition,
                "depot recovery bridle dog");

            body.position += new Vector3(400f, 40f, -300f);
            Press(keyboard.eKey);
            yield return null;
            InvokeGlobalShortcuts(controller);
            Release(keyboard.eKey);
            yield return null;

            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<OperateDepotRecoveryPointCommand>());
            Assert.That(interactor.IsOperationQueued, Is.True);
            Assert.That(interactor.LastInteractionRejected, Is.False);
            Assert.That(interactor.IsTargetVisible, Is.True);
            Assert.That(controller.CanonicalHash, Is.EqualTo(gateHash));
            InvokeSimulationTick(controller);

            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.Not.EqualTo(gateHash));
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtDepot));
            Assert.That(
                controller.State.ArrivalFactionClaimProgressMilli,
                Is.EqualTo(arrivalProgress));
            AssertMode(
                controller.ModeCoordinator!,
                LastBearingPresentationMode.DepotEncounter);
            Assert.That(
                world.DepotApproachRecoveryView.State,
                Is.EqualTo(DepotApproachRecoveryPresentationState.Unlocked));
            Assert.That(interactor.IsTargetVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator DepotRecoveryPointerAndGamepadUseOneRangeTankBridle()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            LastBearingState gate = DriveUntilDepotRecoveryAvailable(
                CreateOutboundState(
                    module: VehicleModule.SealedRangeTank));
            InstallControllerState(controller, gate);
            LastBearingDepotApproachInteractor interactor =
                controller.World!.DepotApproachInteractor!;

            yield return null;
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);
            Assert.That(interactor.IsTargetVisible, Is.True);
            Assert.That(
                controller.ReadModel!.VehicleModule,
                Is.EqualTo(VehicleModule.SealedRangeTank));
            LastBearingReadModel gateModel = controller.ReadModel;
            Vector2 pointer = RequireUnblockedScreenPoint(
                controller,
                interactor.TargetWorldPosition,
                "range tank depot recovery bridle dog");

            InstallControllerState(
                controller,
                CreateAtHomeModuleState(VehicleModule.SealedRangeTank));
            ApplyDepotApproachModel(interactor, gateModel);
            Assert.That(interactor.IsTargetVisible, Is.False);
            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.False);
            Assert.That(PendingCommandCount(controller), Is.Zero);

            InstallControllerState(controller, gate);
            yield return null;
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);
            pointer = RequireUnblockedScreenPoint(
                controller,
                interactor.TargetWorldPosition,
                "range tank depot recovery bridle dog");
            Physics.SyncTransforms();
            string gateHash = controller.CanonicalHash;

            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.True);
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.False,
                "a held bridle dog must not queue a second command");
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(gateHash));

            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtDepot));
            Assert.That(interactor.IsTargetVisible, Is.False);

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.buttonSouth);
            InstallControllerState(controller, gate);
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.False);
            Release(gamepad.buttonSouth);
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);

            Press(gamepad.buttonSouth);
            InvokeGlobalShortcuts(controller);
            yield return null;
            Release(gamepad.buttonSouth);
            yield return null;

            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<OperateDepotRecoveryPointCommand>());
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtDepot));
            Assert.That(interactor.IsTargetVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator WreckLineHotkeyUsesCanonicalGateAndDerivedRoadLoad()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            LastBearingState gate = DriveUntilWreckLineAvailable(
                CreateOutboundState());
            InstallControllerState(controller, gate);
            LastBearingWorldBuilder world = controller.World!;
            RoadFeelRigInstance roadRig = world.RoadFeelRig!;
            Rigidbody body = roadRig.Vehicle.Body;
            LastBearingWreckLineInteractor interactor =
                world.WreckLineInteractor!;

            AssertMode(
                controller.ModeCoordinator!,
                LastBearingPresentationMode.Driving);
            AssertRoadModulePointHold(controller, roadRig);
            Assert.That(
                world.RouteModulePointView!.State,
                Is.EqualTo(RouteModulePointPresentationState.WinchAvailable));
            Assert.That(world.RouteModulePointView.IsPumpRotorVisible, Is.True);
            Assert.That(
                controller.ReadModel!.HeavyCargoCustody,
                Is.EqualTo(HeavyCargoCustody.Depot));
            Assert.That(interactor.HasDedicatedInteractionTarget, Is.True);
            yield return null;
            Assert.That(interactor.IsTargetVisible, Is.True);
            Assert.That(interactor.IsFocused, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            Release(keyboard.eKey);
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);
            Assert.That(
                interactor.CurrentStage,
                Is.EqualTo(WreckLineInteractionStage.DeployWinch));
            Assert.That(
                Vector3.Distance(
                    interactor.TargetWorldPosition,
                    body.position),
                Is.GreaterThan(2f),
                "the winch work dog must sit clear of Sasha's chassis");
            _ = RequireUnblockedScreenPoint(
                controller,
                interactor.TargetWorldPosition,
                "winch Wreck Line work dog");
            string gateHash = controller.CanonicalHash;

            body.position += new Vector3(250f, 40f, -175f);
            Press(keyboard.eKey);
            yield return null;
            InvokeGlobalShortcuts(controller);
            Release(keyboard.eKey);
            yield return null;

            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<OperateWreckLineModuleCommand>());
            Assert.That(
                interactor.QueuedStage,
                Is.EqualTo(WreckLineInteractionStage.DeployWinch));
            Assert.That(interactor.LastInteractionRejected, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(gateHash));
            InvokeSimulationTick(controller);

            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.Not.EqualTo(gateHash));
            Assert.That(controller.ReadModel!.RouteActionUsed, Is.True);
            Assert.That(
                controller.ReadModel.HeavyCargoCustody,
                Is.EqualTo(HeavyCargoCustody.Vehicle));
            Assert.That(
                controller.ReadModel.RouteProgressTicks,
                Is.EqualTo(controller.ReadModel.WreckLineGateTicks));
            Assert.That(
                world.RouteModulePointView.State,
                Is.EqualTo(RouteModulePointPresentationState.WinchRecovered));
            Assert.That(world.RouteModulePointView.IsPumpRotorVisible, Is.False);
            Assert.That(interactor.IsTargetVisible, Is.False);
            Assert.That(
                interactor.CurrentStage,
                Is.EqualTo(WreckLineInteractionStage.None));
            Assert.That(
                interactor.QueuedStage,
                Is.EqualTo(WreckLineInteractionStage.None));
            AssertRoadPresentation(controller, roadRig, active: true);
            Assert.That(roadRig.Adapter.LastCargoMassKilograms, Is.EqualTo(1300));
            Assert.That(
                roadRig.Vehicle.Telemetry.CargoMassKilograms,
                Is.EqualTo(1300f));
            Assert.That(
                roadRig.Adapter.LastDamageBand,
                Is.EqualTo(LastBearingRoadDamageBand.Healthy));
            Assert.That(
                roadRig.Vehicle.Telemetry.DamageBand,
                Is.EqualTo(RoadFeelDamageBand.Healthy));
            Assert.That(roadRig.CargoVisuals[0].activeSelf, Is.False);
            Assert.That(roadRig.CargoVisuals[1].activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator WreckLineRangeTankPointerUsesFittedVerbAndFailsClosed()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            LastBearingState gate = DriveUntilWreckLineAvailable(
                CreateOutboundState(
                    module: VehicleModule.SealedRangeTank));
            InstallControllerState(controller, gate);
            LastBearingWreckLineInteractor interactor =
                controller.World!.WreckLineInteractor!;

            yield return null;
            yield return null;
            Assert.That(
                interactor.CurrentStage,
                Is.EqualTo(WreckLineInteractionStage.SealRangeTank));
            Assert.That(interactor.IsTargetVisible, Is.True);
            Assert.That(interactor.IsInputArmed, Is.True);
            Assert.That(
                Vector3.Distance(
                    interactor.TargetWorldPosition,
                    controller.World.RoadFeelRig!.Vehicle.Body.position),
                Is.GreaterThan(2f),
                "the tank-seal work dog must sit clear of Sasha's chassis");
            Assert.That(
                controller.ReadModel!.RouteActionKind,
                Is.EqualTo(RouteActionKind.CrossExposedDustRoute));

            LastBearingReadModel gateModel = controller.ReadModel;
            InstallControllerState(
                controller,
                CreateAtHomeModuleState(VehicleModule.SealedRangeTank));
            ApplyWreckLineModel(interactor, gateModel);
            Assert.That(interactor.IsTargetVisible, Is.False);
            Assert.That(interactor.ActivateCurrentStage(), Is.False);
            Assert.That(PendingCommandCount(controller), Is.Zero);

            InstallControllerState(controller, gate);
            yield return null;
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);
            Vector2 pointer = RequireUnblockedScreenPoint(
                controller,
                interactor.TargetWorldPosition,
                "sealed range tank Wreck Line work dog");
            string gateHash = controller.CanonicalHash;
            Physics.SyncTransforms();

            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.True);
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<OperateWreckLineModuleCommand>());
            var queued =
                (OperateWreckLineModuleCommand)PendingCommands(controller)[0];
            Assert.That(
                queued.Action,
                Is.EqualTo(RouteActionKind.CrossExposedDustRoute));
            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.False,
                "a held work dog must not queue a second command");
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(gateHash));

            InvokeSimulationTick(controller);

            Assert.That(PendingCommandCount(controller), Is.Zero);
            Assert.That(controller.CanonicalHash, Is.Not.EqualTo(gateHash));
            Assert.That(controller.ReadModel!.RouteActionUsed, Is.True);
            Assert.That(
                controller.ReadModel.HeavyCargoCustody,
                Is.EqualTo(HeavyCargoCustody.Depot));
            Assert.That(controller.State!.TowSlotsUsed, Is.Zero);
            Assert.That(interactor.IsTargetVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator FrameRailsRequireSecondPressPersistAndCreditFourPartsOnce()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            _ = InstallTemporarySaveAdapter(controller);
            LastBearingState gate = DriveUntilWreckLineAvailable(
                CreateOutboundState(installPatchworkSkidPlate: true));
            InstallControllerState(controller, gate);
            LastBearingRouteModulePointView wreck =
                controller.World!.RouteModulePointView!;
            RoadFeelRigInstance roadRig = controller.World.RoadFeelRig!;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;
            yield return null;
            Assert.That(wreck.Interactor!.IsInputArmed, Is.True);

            Press(keyboard.eKey);
            yield return null;
            InvokeGlobalShortcuts(controller);
            Release(keyboard.eKey);
            yield return null;
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<OperateWreckLineModuleCommand>());
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.IsWreckLineFrameRailRecoveryAvailable,
                Is.True);
            Assert.That(
                controller.ReadModel.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.WreckLine));
            Assert.That(wreck.IsFrameRailSourceVisible, Is.True);
            Assert.That(wreck.IsRoadFrameRailCargoVisible, Is.False);
            Assert.That(wreck.Interactor!.IsTargetVisible, Is.False);
            AssertRoadModulePointHold(controller, roadRig);

            Press(keyboard.eKey);
            yield return null;
            InvokeGlobalShortcuts(controller);
            Release(keyboard.eKey);
            yield return null;
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<RecoverWreckLineFrameRailsCommand>());
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Vehicle));
            Assert.That(controller.State!.OrdinaryCargoUsedUnits, Is.EqualTo(1));
            Assert.That(wreck.IsFrameRailSourceVisible, Is.False);
            Assert.That(wreck.IsCanonicalFrameRailCargoVisible, Is.True);
            Assert.That(wreck.IsRoadFrameRailCargoVisible, Is.True);
            AssertRoadPresentation(controller, roadRig, active: true);
            int rotorAndRailMass =
                LastBearingModeCoordinator.PumpRotorPresentationMassKilograms +
                LastBearingModeCoordinator.FrameRailPresentationMassKilograms;
            Assert.That(
                roadRig.Adapter.LastCargoMassKilograms,
                Is.EqualTo(rotorAndRailMass));
            Assert.That(
                roadRig.Vehicle.Telemetry.CargoMassKilograms,
                Is.EqualTo((float)rotorAndRailMass));
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));
            string loadedCargoHash = controller.CanonicalHash;

            controller.ReturnToTitle();
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(loadedCargoHash));
            Assert.That(
                controller.ReadModel!.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Vehicle));
            Assert.That(wreck.IsRoadFrameRailCargoVisible, Is.True);
            Assert.That(
                roadRig.Adapter.LastCargoMassKilograms,
                Is.EqualTo(rotorAndRailMass),
                "canonical reload must reconstruct the same road load");

            var kernel = new LastBearingKernel();
            LastBearingState state = DriveUntilDepotRecoveryAvailable(
                controller.State!);
            state = Apply(kernel, state, sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            state = Apply(kernel, state, sequence =>
                new ResolveDepotCommand(sequence, EncounterChoice.TakeBearing));
            state = Apply(kernel, state, sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            string transactionId = state.TransactionId!;
            string fingerprint = state.TransactionFingerprint!;
            state = Apply(kernel, state, sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = DriveUntilPhase(state, ExpeditionPhase.Returned);
            InstallControllerState(controller, state);
            long partsBeforeCheckIn = controller.ReadModel!.PartsUnits;
            int loadedReturnMass =
                rotorAndRailMass +
                LastBearingModeCoordinator
                    .CeramicBearingPresentationMassKilograms;

            Assert.That(controller.IsReturnCheckInAvailable, Is.True);
            Assert.That(wreck.IsCanonicalFrameRailCargoVisible, Is.True);
            Assert.That(
                roadRig.Adapter.LastCargoMassKilograms,
                Is.EqualTo(loadedReturnMass));
            controller.CompleteReturn();
            Assert.That(PendingCommandCount(controller), Is.EqualTo(2));
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Credited));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(
                    partsBeforeCheckIn +
                    LastBearingBalanceV1.WreckLineFrameRailSalvagePartsUnits));
            Assert.That(controller.State!.OrdinaryCargoUsedUnits, Is.EqualTo(1));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(
                roadRig.Adapter.LastCargoMassKilograms,
                Is.EqualTo(
                    LastBearingModeCoordinator
                        .CeramicBearingPresentationMassKilograms),
                "credited rails and unloaded rotor must stop weighing on the rig");
            Assert.That(wreck.IsCanonicalFrameRailCargoVisible, Is.False);
            Assert.That(wreck.IsRoadFrameRailCargoVisible, Is.False);
            Assert.That(controller.Status, Does.Contain("+4 reclaimed parts"));

            long partsAfterCheckIn = controller.ReadModel.PartsUnits;
            controller.CompleteReturn();
            Assert.That(PendingCommandCount(controller), Is.Zero);
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(partsAfterCheckIn));
        }

        [UnityTest]
        public IEnumerator DepotRepairCargoLoadsThroughCanonicalGateAndAutosaveRestoresExactCustody()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            LastBearingWorldBuilder world = controller.World!;
            LastBearingDepotCargoLoadingView cargoView =
                world.DepotCargoLoadingView!;

            LastBearingState sleeveSource = CreateResolvedDepotState(
                EncounterChoice.Cooperate);
            InstallControllerState(controller, sleeveSource);
            yield return null;

            Assert.That(controller.ReadModel!.IsRepairCargoLoadAvailable, Is.True);
            Assert.That(
                controller.ReadModel.RepairCargoKind,
                Is.EqualTo(RepairCargoKind.FieldSleeve));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Faction));
            Assert.That(cargoView.IsLoadAvailable, Is.True);
            Assert.That(cargoView.IsCanonicalVehicleCargoVisible, Is.False);
            Assert.That(cargoView.IsRoadVehicleCargoVisible, Is.False);
            Transform sleeveAtFaction = RequireNamed(
                cargoView.transform,
                "Faction Field Sleeve At Service Stand");
            AssertRenderedRootInsideViewport(world.MainCamera!, sleeveAtFaction);

            string sourceHash = controller.CanonicalHash;
            controller.BeginReturn();
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.EqualTo(sourceHash));
            Assert.That(controller.Status, Does.Contain("Load the repair cargo"));

            controller.LoadDepotRepairCargo();
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(cargoView.IsCanonicalFieldSleeveVisible, Is.True);
            Transform canonicalSleeve = RequireNamed(
                world.VehicleView!.transform,
                "Canonical Scout Field Sleeve Load");
            AssertRenderedRootInsideViewport(world.MainCamera, canonicalSleeve);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));

            LastBearingState depotSource = CreateResolvedDepotState(
                EncounterChoice.TakeBearing,
                waitForFactionClaim: false);
            InstallControllerState(controller, depotSource);
            yield return null;
            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Depot));
            Assert.That(cargoView.IsCeramicBearingAtDepotVisible, Is.True);
            Transform unclaimedBearing = RequireNamed(
                cargoView.transform,
                "Unclaimed Ceramic Bearing At Depot Cradle");
            AssertRenderedRootInsideViewport(world.MainCamera, unclaimedBearing);

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            yield return null;
            InvokeGlobalShortcuts(controller);
            Release(keyboard.eKey);
            yield return null;

            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            InvokeSimulationTick(controller);
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(controller.ReadModel.IsRepairCargoLoadAvailable, Is.False);
            Assert.That(cargoView.IsLoadAvailable, Is.False);
            Assert.That(cargoView.IsCanonicalCeramicBearingVisible, Is.True);
            Assert.That(cargoView.IsRoadCeramicBearingVisible, Is.True);
            Assert.That(cargoView.IsCanonicalFieldSleeveVisible, Is.False);
            Assert.That(cargoView.IsRoadFieldSleeveVisible, Is.False);
            Transform canonicalBearing = RequireNamed(
                world.VehicleView!.transform,
                "Canonical Scout Ceramic Bearing Load");
            AssertRenderedRootInsideViewport(world.MainCamera!, canonicalBearing);

            string loadedHash = controller.CanonicalHash;
            controller.LoadDepotRepairCargo();
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.EqualTo(loadedHash));

            string savedHash = controller.CanonicalHash;
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                "accepted cargo load must trigger the critical-event autosave");
            Assert.That(Directory.Exists(profileDirectory), Is.True);

            controller.ReturnToTitle();
            Assert.That(cargoView.State, Is.EqualTo(
                DepotCargoLoadingPresentationState.Dormant));
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(savedHash));
            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(cargoView.IsCanonicalCeramicBearingVisible, Is.True);
            Assert.That(cargoView.IsRoadCeramicBearingVisible, Is.True);

            controller.BeginReturn();
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returning));
            Transform roadBearing = RequireNamed(
                world.RoadFeelRig!.Root.transform,
                "Road Scout Ceramic Bearing Load");
            Assert.That(canonicalBearing.gameObject.activeInHierarchy, Is.False);
            Assert.That(roadBearing.gameObject.activeInHierarchy, Is.True);
            AssertCameraOwnership(controller, chaseActive: true);

            LastBearingState factionSource = CreateResolvedDepotState(
                EncounterChoice.TakeBearing,
                waitForFactionClaim: true);
            InstallControllerState(controller, factionSource);
            yield return null;
            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Faction));
            Assert.That(cargoView.IsCeramicBearingAtFactionVisible, Is.True);
            Transform factionBearing = RequireNamed(
                cargoView.transform,
                "Faction-Held Ceramic Bearing At Service Stand");
            AssertRenderedRootInsideViewport(world.MainCamera, factionBearing);

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.buttonSouth);
            yield return null;
            InvokeGlobalShortcuts(controller);
            Release(gamepad.buttonSouth);
            yield return null;
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(cargoView.IsCanonicalCeramicBearingVisible, Is.True);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));
        }

        [UnityTest]
        public IEnumerator DepotRecoveryGateSaveLoadRestoresHeldRoadRig()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            LastBearingState recoveryGate =
                DriveUntilDepotRecoveryAvailable(CreateOutboundState());
            InstallControllerState(controller, recoveryGate);
            RoadFeelRigInstance roadRig = controller.World!.RoadFeelRig!;
            string savedHash = controller.CanonicalHash;
            AssertRoadRecoveryHold(controller, roadRig);

            controller.Save();
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                controller.SaveStatus);
            Assert.That(Directory.Exists(profileDirectory), Is.True);

            controller.ReturnToTitle();
            controller.Load();

            Assert.That(controller.CanonicalHash, Is.EqualTo(savedHash));
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(
                controller.ReadModel.IsDepotApproachRecoveryAvailable,
                Is.True);
            AssertMode(
                controller.ModeCoordinator!,
                LastBearingPresentationMode.Driving);
            AssertRoadRecoveryHold(controller, roadRig);
        }

        [UnityTest]
        public IEnumerator FaultingRoadAdapterCannotBlockDepotRecoveryOperation()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            LastBearingState recoveryGate =
                DriveUntilDepotRecoveryAvailable(CreateOutboundState());
            InstallControllerState(controller, recoveryGate);
            var throwingAdapter = new ThrowingRoadAdapter(
                throwOnSynchronize: true);
            LogAssert.Expect(
                LogType.Warning,
                "LAST_BEARING_ROAD_PRESENTATION_DISABLED " +
                "hold-recovery-synchronize InvalidOperationException");

            controller.AttachRoadModeAdapter(throwingAdapter);

            Assert.That(controller.ModeCoordinator!.RoadAdapterFaulted, Is.True);
            Assert.That(controller.ModeCoordinator.HasRoadAdapter, Is.False);
            string gateHash = controller.CanonicalHash;
            controller.OperateDepotApproachRecoveryPoint();
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            InvokeSimulationTick(controller);

            Assert.That(controller.CanonicalHash, Is.Not.EqualTo(gateHash));
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtDepot));
            AssertMode(
                controller.ModeCoordinator,
                LastBearingPresentationMode.DepotEncounter);
            Assert.That(
                controller.World!.DepotApproachRecoveryView!.State,
                Is.EqualTo(DepotApproachRecoveryPresentationState.Unlocked));
        }

        [UnityTest]
        public IEnumerator RoadInputReachesPresentationBeforeCanonicalTick()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            InstallControllerState(controller, CreateOutboundState());
            RoadFeelRigInstance roadRig = controller.World!.RoadFeelRig!;
            string hashBefore = controller.CanonicalHash;
            long sequenceBefore = controller.State!.NextCommandSequence;
            long progressBefore = controller.ReadModel!.RouteProgressTicks;
            int receiptCountBefore = roadRig.Adapter.CommandReceiptCount;

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.wKey);
            Press(keyboard.sKey);
            Press(keyboard.dKey);
            Press(keyboard.spaceKey);
            yield return null;

            InvokeRoadPresentationInput(controller);

            Assert.That(roadRig.Adapter.LastThrottleMilli, Is.EqualTo(1000));
            Assert.That(roadRig.Adapter.LastBrakeMilli, Is.EqualTo(1000));
            Assert.That(roadRig.Adapter.LastSteeringMilli, Is.EqualTo(1000));
            Assert.That(roadRig.Adapter.LastHandbrakeMilli, Is.EqualTo(1000));
            Assert.That(
                roadRig.Adapter.CommandReceiptCount,
                Is.EqualTo(receiptCountBefore));
            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBefore));
            Assert.That(
                controller.State!.NextCommandSequence,
                Is.EqualTo(sequenceBefore));
            Assert.That(PendingCommandCount(controller), Is.Zero);

            InvokeSimulationTick(controller);
            Release(keyboard.wKey);
            Release(keyboard.sKey);
            Release(keyboard.dKey);
            Release(keyboard.spaceKey);
            yield return null;

            Assert.That(
                roadRig.Adapter.CommandReceiptCount,
                Is.EqualTo(receiptCountBefore + 1));
            Assert.That(
                controller.ReadModel!.RouteProgressTicks,
                Is.GreaterThan(progressBefore));
            Assert.That(
                controller.ReadModel.VehicleLateralMilli,
                Is.GreaterThan(0));
            Assert.That(controller.CanonicalHash, Is.Not.EqualTo(hashBefore));
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

            controller.enabled = false;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.wKey);
            Press(keyboard.dKey);
            yield return null;
            body.linearVelocity = new Vector3(3f, 1f, 7f);
            body.angularVelocity = new Vector3(0.5f, 1f, 0.25f);
            long routeProgressBefore = controller.ReadModel!.RouteProgressTicks;
            int receiptCountBefore = roadRig.Adapter.CommandReceiptCount;

            controller.TogglePause();
            Assert.That(roadRig.Adapter.LastThrottleMilli, Is.Zero);
            Assert.That(roadRig.Adapter.LastSteeringMilli, Is.Zero);
            Assert.That(roadRig.Adapter.LastBrakeMilli, Is.Zero);
            Assert.That(roadRig.Adapter.LastHandbrakeMilli, Is.Zero);
            InvokeSimulationTick(controller);
            Release(keyboard.wKey);
            Release(keyboard.dKey);

            Assert.That(controller.ReadModel!.PauseCause, Is.EqualTo(PauseCause.Explicit));
            Assert.That(
                controller.ReadModel.RouteProgressTicks,
                Is.EqualTo(routeProgressBefore));
            Assert.That(
                roadRig.Adapter.CommandReceiptCount,
                Is.EqualTo(receiptCountBefore));
            AssertMode(coordinator, LastBearingPresentationMode.Driving);
            Assert.That(coordinator.IsRoadPresentationActive, Is.False);
            Assert.That(roadRig.Root.activeInHierarchy, Is.True);
            Assert.That(roadRig.Adapter.IsRoadModeActive, Is.False);
            Assert.That(roadRig.Adapter.IsPhysicsSuspended, Is.True);
            Assert.That(roadRig.Vehicle.enabled, Is.False);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.RecoverRoadPresentation(), Is.False);
            AssertCameraOwnership(controller, chaseActive: false);

            for (var frame = 0; frame < 3; frame++)
            {
                yield return new WaitForFixedUpdate();
                Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
                Assert.That(body.isKinematic, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator BrakeReverseAndHandbrakeRemainPresentationOnly()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            InstallControllerState(controller, CreateOutboundState());
            RoadFeelRigInstance roadRig = controller.World!.RoadFeelRig!;
            long progressBefore = controller.ReadModel!.RouteProgressTicks;
            long sequenceBefore = controller.State!.NextCommandSequence;

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.sKey);
            Press(keyboard.spaceKey);
            yield return null;
            InvokeSimulationTick(controller);
            Release(keyboard.sKey);
            Release(keyboard.spaceKey);
            yield return null;

            Assert.That(roadRig.Adapter.LastThrottleMilli, Is.EqualTo(0));
            Assert.That(roadRig.Adapter.LastBrakeMilli, Is.EqualTo(1000));
            Assert.That(roadRig.Adapter.LastHandbrakeMilli, Is.EqualTo(1000));
            Assert.That(
                controller.ReadModel!.RouteProgressTicks,
                Is.EqualTo(progressBefore));
            Assert.That(
                controller.State!.NextCommandSequence,
                Is.EqualTo(sequenceBefore));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ManualRoadRecoveryRecentersPresentationWithoutCanonicalOrSaveWrites()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            LastBearingState loadedWorn = CreateLoadedWornOutboundState();
            InstallControllerState(controller, loadedWorn);

            LastBearingWorldBuilder world = controller.World!;
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            RoadFeelRigInstance roadRig = world.RoadFeelRig!;
            LastBearingRoadFeelModeAdapter adapter = roadRig.Adapter;
            Rigidbody body = roadRig.Vehicle.Body;
            Camera camera = world.MainCamera!;
            Transform canonicalVehicle = world.VehicleView!.transform;

            Assert.That(controller.CanRecoverRoadPresentation, Is.True);
            Assert.That(adapter.LastCargoMassKilograms, Is.EqualTo(1300));
            Assert.That(adapter.LastDamageBand, Is.EqualTo(LastBearingRoadDamageBand.Worn));
            Assert.That(roadRig.Vehicle.Telemetry.CargoMassKilograms, Is.EqualTo(1300f));
            Assert.That(roadRig.Vehicle.Telemetry.DamageBand, Is.EqualTo(RoadFeelDamageBand.Worn));
            AssertCameraOwnership(controller, chaseActive: true);

            controller.Save();
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                controller.SaveStatus);
            Dictionary<string, string> saveBefore = SnapshotSaveFiles(profileDirectory);
            string saveStatusBefore = controller.SaveStatus;
            string hashBefore = controller.CanonicalHash;
            LastBearingState stateBefore = controller.State!;
            long sequenceBefore = stateBefore.NextCommandSequence;
            long routeProgressBefore = stateBefore.RouteProgressTicks;
            long conditionBefore = stateBefore.VehicleConditionMilli;
            HeavyCargoCustody custodyBefore = stateBefore.HeavyCargoCustody;
            Vector3 expectedPosition = canonicalVehicle.position;
            Quaternion expectedRotation = canonicalVehicle.rotation;
            Vector3 expectedRecoveredCameraPosition = camera.transform.position;

            coordinator.ApplyQuantizedRoadCommandShadow(875, -625);
            coordinator.ApplyPresentationOnlyRoadControls(450, 700);
            body.position = expectedPosition + new Vector3(70f, 9f, -55f);
            body.rotation = Quaternion.Euler(18f, 121f, 7f);
            body.linearVelocity = new Vector3(11f, 3f, -8f);
            body.angularVelocity = new Vector3(2f, -1.5f, 0.75f);
            Physics.SyncTransforms();
            yield return null;

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.rKey);
            yield return null;
            Assert.That(keyboard.rKey.wasPressedThisFrame, Is.True);
            InvokeGlobalShortcuts(controller);
            Release(keyboard.rKey);

            Assert.That(controller.Status, Does.Contain("recovered to Sasha's"));
            Assert.That(controller.CanRecoverRoadPresentation, Is.True);
            Assert.That(coordinator.IsRoadPresentationActive, Is.True);
            Assert.That(adapter.IsRoadModeActive, Is.True);
            Assert.That(adapter.IsPhysicsSuspended, Is.False);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(adapter.LastThrottleMilli, Is.EqualTo(0));
            Assert.That(adapter.LastSteeringMilli, Is.EqualTo(0));
            Assert.That(adapter.LastBrakeMilli, Is.EqualTo(0));
            Assert.That(adapter.LastHandbrakeMilli, Is.EqualTo(0));
            Assert.That(
                Vector3.Distance(body.position, expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(
                Quaternion.Angle(body.rotation, expectedRotation),
                Is.LessThan(0.01f));
            Assert.That(adapter.LastCargoMassKilograms, Is.EqualTo(1300));
            Assert.That(adapter.LastDamageBand, Is.EqualTo(LastBearingRoadDamageBand.Worn));
            Assert.That(roadRig.Vehicle.Telemetry.CargoMassKilograms, Is.EqualTo(1300f));
            Assert.That(roadRig.Vehicle.Telemetry.DamageBand, Is.EqualTo(RoadFeelDamageBand.Worn));
            AssertCameraOwnership(controller, chaseActive: true);
            Assert.That(
                Vector3.Distance(camera.transform.position, expectedRecoveredCameraPosition),
                Is.LessThan(0.025f));
            AssertChaseCameraLooksBehindSasha(world, roadRig);

            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBefore));
            Assert.That(controller.State, Is.SameAs(stateBefore));
            Assert.That(stateBefore.NextCommandSequence, Is.EqualTo(sequenceBefore));
            Assert.That(stateBefore.RouteProgressTicks, Is.EqualTo(routeProgressBefore));
            Assert.That(stateBefore.VehicleConditionMilli, Is.EqualTo(conditionBefore));
            Assert.That(stateBefore.HeavyCargoCustody, Is.EqualTo(custodyBefore));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.SaveStatus, Is.EqualTo(saveStatusBefore));
            AssertSaveSnapshot(saveBefore, SnapshotSaveFiles(profileDirectory));

            Vector3 recoveredCameraPosition = camera.transform.position;
            Quaternion recoveredCameraRotation = camera.transform.rotation;
            Assert.That(controller.RecoverRoadPresentation(), Is.True);

            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBefore));
            Assert.That(controller.State, Is.SameAs(stateBefore));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(
                Vector3.Distance(camera.transform.position, recoveredCameraPosition),
                Is.LessThan(0.001f));
            Assert.That(
                Quaternion.Angle(camera.transform.rotation, recoveredCameraRotation),
                Is.LessThan(0.01f));
            Assert.That(adapter.LastCargoMassKilograms, Is.EqualTo(1300));
            Assert.That(adapter.LastDamageBand, Is.EqualTo(LastBearingRoadDamageBand.Worn));
            Assert.That(controller.SaveStatus, Is.EqualTo(saveStatusBefore));
            AssertSaveSnapshot(saveBefore, SnapshotSaveFiles(profileDirectory));
            AssertCameraOwnership(controller, chaseActive: true);
        }

        [UnityTest]
        public IEnumerator ExternallyDisabledChaseFailsClosedToFixedRoadCamera()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            InstallControllerState(controller, CreateOutboundState());

            LastBearingWorldBuilder world = controller.World!;
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            LastBearingCameraRig cameraRig = world.CameraRig!;
            RoadFeelChaseCamera chaseCamera = world.RoadChaseCamera!;
            RoadFeelRigInstance roadRig = world.RoadFeelRig!;
            Camera sharedCamera = world.MainCamera!;
            controller.Save();
            Dictionary<string, string> saveBefore =
                SnapshotSaveFiles(profileDirectory);
            long globalTickBefore = controller.State!.GlobalTick;
            long sequenceBefore = controller.State.NextCommandSequence;
            long routeProgressBefore = controller.State.RouteProgressTicks;
            long conditionBefore = controller.State.VehicleConditionMilli;
            HeavyCargoCustody custodyBefore =
                controller.State.HeavyCargoCustody;
            int pendingBefore = PendingCommandCount(controller);
            string statusBefore = controller.Status;
            string saveStatusBefore = controller.SaveStatus;

            AssertCameraOwnership(controller, chaseActive: true);
            Assert.That(coordinator.IsRoadPresentationActive, Is.True);
            Assert.That(roadRig.Adapter.IsRoadModeActive, Is.True);
            LogAssert.Expect(
                LogType.Warning,
                "LAST_BEARING_CHASE_CAMERA_DISABLED ownership-lost");

            chaseCamera.enabled = false;
            yield return new WaitForEndOfFrame();
            AssertFixedRoadFallbackPose(sharedCamera, roadRig.Root.transform);
            yield return new WaitForSecondsRealtime(0.25f);

            AssertCameraOwnership(controller, chaseActive: false);
            Assert.That(cameraRig.IsRoadChaseRecoveryRequired, Is.True);
            Assert.That(cameraRig.IsRoadMode, Is.True);
            Assert.That(
                cameraRig.RoadTarget,
                Is.SameAs(roadRig.Root.transform));
            Assert.That(sharedCamera.enabled, Is.True);
            Assert.That(sharedCamera.gameObject.activeInHierarchy, Is.True);
            Assert.That(coordinator.IsRoadPresentationActive, Is.True);
            Assert.That(roadRig.Adapter.IsRoadModeActive, Is.True);
            Assert.That(roadRig.Adapter.IsPhysicsSuspended, Is.False);
            Assert.That(roadRig.Vehicle.Body.isKinematic, Is.False);

            Assert.That(controller.State!.GlobalTick, Is.GreaterThan(globalTickBefore));
            Assert.That(controller.State.NextCommandSequence, Is.EqualTo(sequenceBefore));
            Assert.That(controller.State.RouteProgressTicks, Is.EqualTo(routeProgressBefore));
            Assert.That(controller.State.VehicleConditionMilli, Is.EqualTo(conditionBefore));
            Assert.That(controller.State.HeavyCargoCustody, Is.EqualTo(custodyBefore));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(pendingBefore));
            Assert.That(controller.Status, Is.EqualTo(statusBefore));
            Assert.That(controller.SaveStatus, Is.EqualTo(saveStatusBefore));
            AssertSaveSnapshot(saveBefore, SnapshotSaveFiles(profileDirectory));

            Assert.That(
                controller.CanRecoverRoadPresentation,
                Is.True,
                "active Driving keeps explicit recovery available so the player " +
                "can deliberately reclaim chase ownership");

            string hashBeforeRecovery = controller.CanonicalHash;
            LastBearingState stateBeforeRecovery = controller.State;
            Assert.That(controller.RecoverRoadPresentation(), Is.True);
            AssertCameraOwnership(controller, chaseActive: true);
            Assert.That(cameraRig.IsRoadChaseRecoveryRequired, Is.False);
            AssertChaseCameraLooksBehindSasha(world, roadRig);
            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBeforeRecovery));
            Assert.That(controller.State, Is.SameAs(stateBeforeRecovery));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(pendingBefore));
            Assert.That(controller.SaveStatus, Is.EqualTo(saveStatusBefore));
            AssertSaveSnapshot(saveBefore, SnapshotSaveFiles(profileDirectory));
        }

        [UnityTest]
        public IEnumerator MissingVehicleControllerMakesManualRecoveryFailClosed()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            InstallControllerState(controller, CreateOutboundState());

            LastBearingWorldBuilder world = controller.World!;
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            RoadFeelRigInstance roadRig = world.RoadFeelRig!;
            Rigidbody body = roadRig.Vehicle.Body;
            controller.Save();
            Dictionary<string, string> saveBefore =
                SnapshotSaveFiles(profileDirectory);
            string hashBefore = controller.CanonicalHash;
            LastBearingState stateBefore = controller.State!;
            int pendingBefore = PendingCommandCount(controller);
            string statusBefore = controller.Status;
            string saveStatusBefore = controller.SaveStatus;

            Assert.That(controller.CanRecoverRoadPresentation, Is.True);
            AssertCameraOwnership(controller, chaseActive: true);
            UnityEngine.Object.DestroyImmediate(roadRig.Vehicle);
            LogAssert.Expect(
                LogType.Warning,
                "LAST_BEARING_ROAD_PRESENTATION_DISABLED " +
                "manual-recovery-reactivate InvalidOperationException");

            Assert.That(controller.RecoverRoadPresentation(), Is.False);

            Assert.That(coordinator.RoadAdapterFaulted, Is.True);
            Assert.That(coordinator.HasRoadAdapter, Is.False);
            Assert.That(coordinator.IsRoadPresentationActive, Is.False);
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            AssertCameraOwnership(controller, chaseActive: false);
            Assert.That(roadRig.Root.activeInHierarchy, Is.False);
            Assert.That(body.gameObject.activeInHierarchy, Is.False);
            Assert.That(world.VehicleView!.gameObject.activeInHierarchy, Is.True);
            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBefore));
            Assert.That(controller.State, Is.SameAs(stateBefore));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(pendingBefore));
            Assert.That(controller.Status, Is.EqualTo(statusBefore));
            Assert.That(controller.SaveStatus, Is.EqualTo(saveStatusBefore));
            AssertSaveSnapshot(saveBefore, SnapshotSaveFiles(profileDirectory));
        }

        [UnityTest]
        public IEnumerator OutboundSaveLoadSynchronizesRoadRigAfterCanonicalRender()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            LastBearingState outbound = CreateOutboundState();
            var kernel = new LastBearingKernel();
            for (var tick = 0; tick < 20; tick++)
            {
                outbound = Apply(kernel, outbound, sequence =>
                    new DriveVehicleCommand(sequence, 0, 1000));
            }

            Assert.That(outbound.ExpeditionPhase, Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(
                outbound.VehicleLateralMilli,
                Is.EqualTo(LastBearingBalanceV1.RoadLateralLimitMilli));
            InstallControllerState(controller, outbound);
            LastBearingWorldBuilder world = controller.World!;
            RoadFeelRigInstance roadRig = world.RoadFeelRig!;
            Assert.That(
                world.VehicleView!.VisibleLateralOffset,
                Is.EqualTo(1.35f).Within(0.001f));
            Vector3 expectedPosition = world.VehicleView!.transform.position;
            Quaternion expectedRotation = world.VehicleView.transform.rotation;
            string savedHash = controller.CanonicalHash;
            AssertRoadPresentation(controller, roadRig, active: true);

            controller.Save();
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                controller.SaveStatus);
            Assert.That(Directory.Exists(profileDirectory), Is.True);

            controller.ReturnToTitle();
            AssertCameraOwnership(controller, chaseActive: false);
            Assert.That(
                Vector3.Distance(world.VehicleView.transform.position, expectedPosition),
                Is.GreaterThan(0.1f));

            controller.Load();

            Assert.That(controller.CanonicalHash, Is.EqualTo(savedHash));
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(
                controller.ReadModel.VehicleLateralMilli,
                Is.EqualTo(LastBearingBalanceV1.RoadLateralLimitMilli));
            Assert.That(
                world.VehicleView.VisibleLateralOffset,
                Is.EqualTo(1.35f).Within(0.001f));
            AssertRoadPresentation(controller, roadRig, active: true);
            AssertCameraOwnership(controller, chaseActive: true);
            Assert.That(
                Vector3.Distance(
                    roadRig.Vehicle.Body.position,
                    expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(
                Quaternion.Angle(
                    roadRig.Vehicle.Body.rotation,
                    expectedRotation),
                Is.LessThan(0.01f));
            Assert.That(
                Vector3.Distance(
                    roadRig.Vehicle.Body.position,
                    world.VehicleView.transform.position),
                Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator FaultingLiveRoadInputCannotBlockCanonicalTick()
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
                "apply-presentation-input InvalidOperationException");

            InvokeRoadPresentationInput(controller);

            Assert.That(throwingAdapter.ApplyAttemptCount, Is.EqualTo(1));
            Assert.That(coordinator.RoadAdapterFaulted, Is.True);
            Assert.That(coordinator.HasRoadAdapter, Is.False);
            Assert.That(coordinator.IsRoadPresentationActive, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBefore));
            Assert.That(PendingCommandCount(controller), Is.Zero);

            InvokeSimulationTick(controller);
            Release(keyboard.wKey);
            yield return null;

            Assert.That(throwingAdapter.ApplyAttemptCount, Is.EqualTo(1));
            Assert.That(
                controller.ReadModel!.RouteProgressTicks,
                Is.GreaterThan(progressBefore));
            Assert.That(controller.CanonicalHash, Is.Not.EqualTo(hashBefore));
            Assert.That(controller.World!.RoadFeelRig!.Root.activeInHierarchy, Is.False);
            Assert.That(controller.World.VehicleView!.gameObject.activeSelf, Is.True);
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
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.RecoverRoadPresentation(), Is.False);
            AssertCameraOwnership(controller, chaseActive: false);
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

        [UnityTest]
        public IEnumerator SharedScoutOwnsStableSemanticsInStaticAndRoadPresentations()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.StartNewGame(ColonyComposition.Mixed);
            yield return new WaitForSecondsRealtime(0.15f);

            SashaScoutVisual[] scouts =
                UnityEngine.Object.FindObjectsByType<SashaScoutVisual>(
                    FindObjectsInactive.Include);
            Assert.That(scouts, Has.Length.EqualTo(2));
            foreach (SashaScoutVisual scout in scouts)
            {
                Assert.That(
                    scout.DirectionStage,
                    Is.EqualTo(SashaScoutSemanticContract.Stage));
                Assert.That(scout.HasProductionGeometry, Is.False);
                Assert.That(
                    scout.ContactStationCount,
                    Is.EqualTo(SashaScoutSemanticContract.WheelCount));
                Assert.That(
                    scout.WheelVisualCount,
                    Is.EqualTo(SashaScoutSemanticContract.WheelCount));
            }

            LastBearingWorldBuilder world = controller.World!;
            SashaScoutVisual staticScout = world.VehicleView!.ScoutVisual!;
            SashaScoutVisual roadScout = world.RoadFeelRig!.ScoutVisual;
            AssertSharedScoutPalette(staticScout, roadScout);
            AssertModuleSocketAlignment(staticScout);
            AssertModuleSocketAlignment(roadScout);
            Assert.That(staticScout.gameObject.activeInHierarchy, Is.True);
            Assert.That(roadScout.gameObject.activeInHierarchy, Is.False);
            Assert.That(CountActiveScouts(scouts), Is.EqualTo(1));
            Assert.That(
                staticScout.CollisionRoot!
                    .GetComponentsInChildren<BoxCollider>(true),
                Is.Empty);
            Assert.That(
                roadScout.CollisionRoot!
                    .GetComponentsInChildren<BoxCollider>(true),
                Has.Length.EqualTo(
                    SashaScoutBlockoutFactory.RoadCollisionBoxCount));
            Assert.That(
                roadScout.GetComponentsInChildren<MeshCollider>(true),
                Is.Empty);

            Transform[] contacts = roadScout.CopyContactStations();
            for (var index = 0; index < contacts.Length; index++)
            {
                Assert.That(
                    contacts[index].localPosition,
                    Is.EqualTo(
                        SashaScoutSemanticContract.GetContactStationLocalPosition(index)));
            }

            InstallControllerState(controller, CreateOutboundState());
            yield return null;

            Assert.That(staticScout.gameObject.activeInHierarchy, Is.False);
            Assert.That(roadScout.gameObject.activeInHierarchy, Is.True);
            Assert.That(CountActiveScouts(scouts), Is.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<RoadFeelVehicleController>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Rigidbody[] rigidbodies =
                UnityEngine.Object.FindObjectsByType<Rigidbody>(
                    FindObjectsInactive.Include);
            Assert.That(rigidbodies, Has.Length.EqualTo(1));
            Assert.That(
                rigidbodies[0],
                Is.SameAs(world.RoadFeelRig.Vehicle.Body));
        }

        [UnityTest]
        public IEnumerator GarageCutawayUsesFixedInspectionPoseWithoutCanonicalWrites()
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
            controller.enabled = false;

            LastBearingWorldBuilder world = controller.World!;
            LastBearingGarageBayView garage = world.GarageBayView!;
            LastBearingCameraRig cameraRig = world.CameraRig!;
            string canonicalBefore = controller.CanonicalHash;
            Vector3 cityFocusBefore = cameraRig.CityFocus;

            controller.OpenGarageBay();
            yield return new WaitForSecondsRealtime(1f);

            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.GarageBay));
            Assert.That(controller.ModeCoordinator.ActiveModeCount, Is.EqualTo(1));
            Assert.That(garage.gameObject.activeInHierarchy, Is.True);
            Assert.That(cameraRig.IsInspectionMode, Is.True);
            AssertCameraOwnership(controller, chaseActive: false);
            AssertRecoveryUnavailableWithoutWrites(controller);
            Assert.That(
                Vector3.Distance(
                    world.MainCamera!.transform.position,
                    garage.CameraAnchor!.position),
                Is.LessThan(0.15f));
            Vector3 cameraForward = world.MainCamera.transform.forward;
            Vector3 focusDirection =
                (garage.FocusAnchor!.position - world.MainCamera.transform.position)
                .normalized;
            Assert.That(Vector3.Dot(cameraForward, focusDirection), Is.GreaterThan(0.998f));

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.dKey);
            yield return new WaitForSecondsRealtime(0.1f);
            Release(keyboard.dKey);
            yield return null;

            Assert.That(cameraRig.CityFocus, Is.EqualTo(cityFocusBefore));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
            Assert.That(
                garage.GetComponentsInChildren<Camera>(true),
                Is.Empty);
            Assert.That(
                garage.GetComponentsInChildren<CharacterController>(true),
                Is.Empty);

            controller.ShowCityOverview();
            yield return null;

            Assert.That(garage.gameObject.activeInHierarchy, Is.False);
            Assert.That(cameraRig.IsInspectionMode, Is.False);
            AssertCameraOwnership(controller, chaseActive: false);
            AssertRecoveryUnavailableWithoutWrites(controller);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [UnityTest]
        public IEnumerator UncommittedGarageIntentSavesCanonicalBytesAndNeverReloads()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            CompleteDistrictObservation(controller, clear: true);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);
            byte[] canonicalBefore = LastBearingCanonicalCodec.Encode(
                controller.State!);

            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);

            Assert.That(controller.IsGaragePlanIntentActive, Is.True);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.CivicBuffer));
            Assert.That(controller.IsGaragePlanCommitAvailable, Is.True);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!));

            controller.Save();

            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                controller.SaveStatus);
            Assert.That(Directory.Exists(profileDirectory), Is.True);
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!));

            controller.Load();

            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.Unselected));
            Assert.That(controller.IsGaragePlanCommitAvailable, Is.False);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(
                controller.ReadModel!.PreparationChoice,
                Is.EqualTo(PreparationChoice.Unselected));
            Assert.That(
                controller.ReadModel.PlannedModule,
                Is.EqualTo(VehicleModule.None));
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!));

            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);
            Assert.That(controller.IsGaragePlanIntentActive, Is.True);
            Directory.Delete(profileDirectory, recursive: true);

            controller.Load();

            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.Unselected));
            Assert.That(PendingCommandCount(controller), Is.Zero);
            Assert.That(
                controller.SaveStatus,
                Is.EqualTo("Load refused: " + LastBearingSaveCodes.NoProfile));
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!));
        }

        [UnityTest]
        public IEnumerator GarageSkidPlateInstallsPersistsAndLeavesModuleChoiceOpen()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            CompleteDistrictObservation(controller, clear: true);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);
            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            long partsBefore = controller.ReadModel!.PartsUnits;

            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.GarageBay));
            Assert.That(
                controller.GetComponent<LastBearingHud>().enabled,
                Is.True,
                "the garage must return ownership to the HUD action card");
            Assert.That(controller.IsPatchworkSkidPlateInstallAvailable, Is.True);

            controller.InstallPatchworkSkidPlate();

            Assert.That(controller.IsPatchworkSkidPlateInstallQueued, Is.True);
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(controller.IsGaragePlanIntentActive, Is.True);
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.RigUpgrade,
                Is.EqualTo(RigUpgrade.PatchworkSkidPlate));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(
                    partsBefore -
                    LastBearingBalanceV1.PatchworkSkidPlatePartsCostUnits));
            Assert.That(controller.IsGaragePlanIntentActive, Is.True);
            Assert.That(controller.IsGaragePlanCommitAvailable, Is.True);
            Assert.That(
                controller.Status,
                Does.Contain("Round-trip condition loss is reduced by 40"));

            string installedHash = controller.CanonicalHash;
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                controller.SaveStatus);
            controller.ReturnToTitle();
            controller.Load();

            Assert.That(controller.CanonicalHash, Is.EqualTo(installedHash));
            Assert.That(
                controller.ReadModel!.RigUpgrade,
                Is.EqualTo(RigUpgrade.PatchworkSkidPlate));
            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));

            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel.PreparationChoice,
                Is.EqualTo(PreparationChoice.CivicBuffer));
            Assert.That(
                controller.ReadModel.PlannedModule,
                Is.EqualTo(VehicleModule.WinchAssembly));
            Assert.That(
                controller.ReadModel.RigUpgrade,
                Is.EqualTo(RigUpgrade.PatchworkSkidPlate));
            Assert.That(Directory.Exists(profileDirectory), Is.True);
        }

        [UnityTest]
        public IEnumerator PumpHallHomecomingInstallsAutosavesAndReloadsExactly()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            LastBearingState ready = CreateInstallationReadyState();
            InstallControllerState(controller, ready);
            LastBearingWorldBuilder world = controller.World!;
            LastBearingPumpHallCutawayView pumpHall = world.PumpHallCutawayView!;
            LastBearingCameraRig cameraRig = world.CameraRig!;
            string readyHash = controller.CanonicalHash;

            Assert.That(
                controller.ReadModel!.IsCityImprovementInstallationAvailable,
                Is.True);
            Assert.That(pumpHall.IsStagedRotorVisible, Is.True);
            Assert.That(pumpHall.IsInstalledPumpVisible, Is.False);
            Assert.That(
                Directory.Exists(profileDirectory),
                Is.False,
                "opening the fixed store must not create a profile before persist");

            controller.OpenBuildingCutaway();
            yield return new WaitForSecondsRealtime(1f);

            AssertMode(
                controller.ModeCoordinator!,
                LastBearingPresentationMode.BuildingCutaway);
            Assert.That(cameraRig.IsInspectionMode, Is.True);
            Assert.That(
                Vector3.Distance(
                    world.MainCamera!.transform.position,
                    pumpHall.CameraAnchor!.position),
                Is.LessThan(0.15f));
            Vector3 focusDirection =
                (pumpHall.FocusAnchor!.position - world.MainCamera.transform.position)
                .normalized;
            Assert.That(
                Vector3.Dot(world.MainCamera.transform.forward, focusDirection),
                Is.GreaterThan(0.998f));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));

            controller.InstallCityImprovement();
            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            InvokeSimulationTick(controller);

            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.Not.EqualTo(readyHash));
            Assert.That(
                controller.ReadModel!.InstalledCityImprovement,
                Is.EqualTo(CityImprovementKind.RefurbishedAuxiliaryPump));
            Assert.That(
                controller.ReadModel.HeavyCargoCustody,
                Is.EqualTo(HeavyCargoCustody.InstalledAtAuxiliaryPump));
            Assert.That(controller.State!.TowSlotsUsed, Is.EqualTo(0));
            Assert.That(pumpHall.IsStagedRotorVisible, Is.False);
            Assert.That(pumpHall.IsInstalledPumpVisible, Is.True);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                controller.SaveStatus);
            Assert.That(
                Directory.GetFiles(profileDirectory, "gen-*.lbg").Length,
                Is.EqualTo(1),
                "one critical installation tick must publish one generation");
            string installedHash = controller.CanonicalHash;

            controller.ReturnToTitle();
            Assert.That(pumpHall.IsStagedRotorVisible, Is.False);
            Assert.That(pumpHall.IsInstalledPumpVisible, Is.False);
            controller.Load();

            Assert.That(controller.CanonicalHash, Is.EqualTo(installedHash));
            Assert.That(
                controller.ReadModel!.InstalledCityImprovement,
                Is.EqualTo(CityImprovementKind.RefurbishedAuxiliaryPump));
            Assert.That(
                controller.ReadModel.HeavyCargoCustody,
                Is.EqualTo(HeavyCargoCustody.InstalledAtAuxiliaryPump));
            Assert.That(pumpHall.IsStagedRotorVisible, Is.False);
            Assert.That(pumpHall.IsInstalledPumpVisible, Is.True);
            Assert.That(
                Directory.GetFiles(profileDirectory, "gen-*.lbg").Length,
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InstalledModuleStaysExclusiveAndSynchronizedAcrossAllViews()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            LastBearingWorldBuilder world = controller.World!;
            SashaScoutVisual staticScout = world.VehicleView!.ScoutVisual!;
            SashaScoutVisual roadScout = world.RoadFeelRig!.ScoutVisual;
            LastBearingGarageBayView garage = world.GarageBayView!;

            InstallControllerState(
                controller,
                CreatePlannedModuleState(VehicleModule.WinchAssembly));
            yield return null;

            Assert.That(
                controller.ReadModel!.PlannedModule,
                Is.EqualTo(VehicleModule.WinchAssembly));
            Assert.That(
                controller.ReadModel.VehicleModule,
                Is.EqualTo(VehicleModule.None));
            AssertModulePresentation(
                staticScout,
                roadScout,
                garage,
                SashaScoutModulePresentation.WinchAssembly);

            InstallControllerState(
                controller,
                CreateAtHomeModuleState(VehicleModule.WinchAssembly));
            yield return null;

            AssertModulePresentation(
                staticScout,
                roadScout,
                garage,
                SashaScoutModulePresentation.WinchAssembly);

            InstallControllerState(
                controller,
                CreateAtHomeModuleState(VehicleModule.SealedRangeTank));
            yield return null;

            AssertModulePresentation(
                staticScout,
                roadScout,
                garage,
                SashaScoutModulePresentation.SealedRangeTank);
        }

        private static LastBearingState CreateOutboundState(
            bool waitForFactionClaim = false,
            bool installPatchworkSkidPlate = false,
            VehicleModule module = VehicleModule.WinchAssembly)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2011);
            if (waitForFactionClaim)
            {
                for (var tick = 0; tick < 9000; tick++)
                {
                    state = kernel.Step(
                        state,
                        Array.Empty<LastBearingCommand>()).State;
                }

                Assert.That(
                    state.FactionClaimState,
                    Is.EqualTo(FactionClaimState.Claimed));
            }

            state = Apply(kernel, state, sequence =>
                new AssignResidentCommand(sequence, ResidentRoster.HumanResidentId));
            if (installPatchworkSkidPlate)
            {
                state = Apply(kernel, state, sequence =>
                    new PlaceCityBuildingCommand(
                        sequence,
                        CityBuildingKind.Recycler,
                        0,
                        0));
                state = Apply(kernel, state, sequence =>
                    new PlaceCityBuildingCommand(
                        sequence,
                        CityBuildingKind.MachineShop,
                        1,
                        0));
                state = Apply(kernel, state, sequence =>
                    new PlaceCityBuildingCommand(
                        sequence,
                        CityBuildingKind.EmergencyStorage,
                        2,
                        0));
                state = Apply(kernel, state, sequence =>
                    new ConnectCityServiceLinkCommand(sequence));
                state = Apply(kernel, state, sequence =>
                    new AssignCityServiceResidentCommand(
                        sequence,
                        ResidentRoster.HumanResidentId));
                state = Apply(kernel, state, sequence =>
                    new AdvanceCityServiceSledCommand(
                        sequence,
                        CityDeliveryStage.AtRecycler));
                state = Apply(kernel, state, sequence =>
                    new AdvanceCityServiceSledCommand(
                        sequence,
                        CityDeliveryStage.InTransit));
            }

            state = Apply(kernel, state, sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            if (installPatchworkSkidPlate)
            {
                state = Apply(kernel, state, sequence =>
                    new InstallRigUpgradeCommand(
                        sequence,
                        RigUpgrade.PatchworkSkidPlate));
            }

            state = Apply(kernel, state, sequence =>
                new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.WorkshopPush,
                    module));
            state = Apply(kernel, state, sequence =>
                new InstallVehicleModuleCommand(sequence, module));
            var guard = 0;
            while (state.PreparationPhase != PreparationPhase.Ready && guard < 1000)
            {
                state = kernel.Step(
                    state,
                    Array.Empty<LastBearingCommand>()).State;
                guard++;
            }

            Assert.That(state.PreparationPhase, Is.EqualTo(PreparationPhase.Ready));
            const string transactionId =
                "transaction:last-bearing:unity:0001";
            const string fingerprint =
                "fingerprint:last-bearing:unity:0001";
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

        private static LastBearingState CreateResolvedDepotState(
            EncounterChoice encounter,
            bool waitForFactionClaim = false)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = DriveUntilDepotRecoveryAvailable(
                CreateOutboundState(waitForFactionClaim));
            state = Apply(kernel, state, sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            return Apply(kernel, state, sequence =>
                new ResolveDepotCommand(sequence, encounter));
        }

        private static LastBearingState CreateLoadedWornOutboundState()
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = DriveUntilWreckLineAvailable(
                CreateOutboundState());
            RouteActionKind action =
                LastBearingReadModel.FromState(state).RouteActionKind;
            state = Apply(kernel, state, sequence =>
                new OperateWreckLineModuleCommand(sequence, action));
            for (var steer = 0; steer < 20; steer++)
            {
                state = Apply(kernel, state, sequence =>
                    new DriveVehicleCommand(sequence, 0, 1000));
            }

            state = Apply(kernel, state, sequence =>
                new DriveVehicleCommand(sequence, 1000, 0));
            Assert.That(state.ExpeditionPhase, Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(state.RouteActionUsed, Is.True);
            Assert.That(state.HeavyCargoKind, Is.EqualTo(HeavyCargoKind.PumpRotor));
            Assert.That(state.HeavyCargoCustody, Is.EqualTo(HeavyCargoCustody.Vehicle));
            Assert.That(
                state.VehicleConditionMilli,
                Is.LessThan(LastBearingBalanceV1.StartingVehicleConditionMilli));
            Assert.That(
                LastBearingModeCoordinator.DerivePresentationDamageBand(
                    state.VehicleConditionMilli),
                Is.EqualTo(LastBearingRoadDamageBand.Worn));
            return state;
        }

        private static LastBearingState CreateAtHomeModuleState(
            VehicleModule module)
        {
            LastBearingState state = CreatePlannedModuleState(module);
            var kernel = new LastBearingKernel();
            var guard = 0;
            while ((state.PreparationPhase != PreparationPhase.Ready ||
                    state.ModuleInstallationState !=
                    ModuleInstallationState.Installed) &&
                   guard < 1000)
            {
                state = kernel.Step(
                    state,
                    Array.Empty<LastBearingCommand>()).State;
                guard++;
            }

            Assert.That(
                state.PreparationPhase,
                Is.EqualTo(PreparationPhase.Ready),
                "module preparation did not become ready within 1000 deterministic ticks");
            Assert.That(
                state.ModuleInstallationState,
                Is.EqualTo(ModuleInstallationState.Installed),
                "module installation did not complete within 1000 deterministic ticks");
            Assert.That(state.VehicleModule, Is.EqualTo(module));
            Assert.That(state.ExpeditionPhase, Is.EqualTo(ExpeditionPhase.AtHome));
            return state;
        }

        private static LastBearingState CreateInstallationReadyState()
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = DriveUntilDepotRecoveryAvailable(
                CreateOutboundState());
            state = Apply(kernel, state, sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            state = Apply(kernel, state, sequence =>
                new ResolveDepotCommand(sequence, EncounterChoice.TakeBearing));
            state = Apply(kernel, state, sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            string transactionId = state.TransactionId!;
            string fingerprint = state.TransactionFingerprint!;
            state = Apply(kernel, state, sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = DriveUntilPhase(state, ExpeditionPhase.Returned);
            state = Apply(kernel, state, sequence =>
                new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = Apply(kernel, state, sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = Apply(kernel, state, sequence =>
                new InstallTurbineRepairCommand(sequence));
            Assert.That(
                LastBearingReadModel.FromState(state)
                    .IsCityImprovementInstallationAvailable,
                Is.True);
            return state;
        }

        private static LastBearingState CreatePlannedModuleState(
            VehicleModule module)
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
                    module));
            state = Apply(kernel, state, sequence =>
                new InstallVehicleModuleCommand(sequence, module));
            Assert.That(state.PlannedModule, Is.EqualTo(module));
            Assert.That(state.VehicleModule, Is.EqualTo(VehicleModule.None));
            Assert.That(
                state.ModuleInstallationState,
                Is.EqualTo(ModuleInstallationState.Pending));
            Assert.That(state.PreparationPhase, Is.EqualTo(PreparationPhase.Preparing));
            return state;
        }

        private static int CountActiveScouts(
            IEnumerable<SashaScoutVisual> scouts)
        {
            var count = 0;
            foreach (SashaScoutVisual scout in scouts)
            {
                if (scout.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertModulePresentation(
            SashaScoutVisual staticScout,
            SashaScoutVisual roadScout,
            LastBearingGarageBayView garage,
            SashaScoutModulePresentation expected)
        {
            Assert.That(staticScout.Module, Is.EqualTo(expected));
            Assert.That(roadScout.Module, Is.EqualTo(expected));
            Assert.That(
                staticScout.IsWinchVisible,
                Is.EqualTo(expected == SashaScoutModulePresentation.WinchAssembly));
            Assert.That(
                staticScout.IsRangeTankVisible,
                Is.EqualTo(expected == SashaScoutModulePresentation.SealedRangeTank));
            Assert.That(
                roadScout.IsWinchVisible,
                Is.EqualTo(expected == SashaScoutModulePresentation.WinchAssembly));
            Assert.That(
                roadScout.IsRangeTankVisible,
                Is.EqualTo(expected == SashaScoutModulePresentation.SealedRangeTank));
            Assert.That(
                garage.IsWinchStaged,
                Is.EqualTo(expected != SashaScoutModulePresentation.WinchAssembly));
            Assert.That(
                garage.IsRangeTankStaged,
                Is.EqualTo(expected != SashaScoutModulePresentation.SealedRangeTank));
        }

        private static void AssertSharedScoutPalette(
            SashaScoutVisual staticScout,
            SashaScoutVisual roadScout)
        {
            Assert.That(
                roadScout.Materials.Iron,
                Is.SameAs(staticScout.Materials.Iron));
            Assert.That(
                roadScout.Materials.Oxide,
                Is.SameAs(staticScout.Materials.Oxide));
            Assert.That(
                roadScout.Materials.Bone,
                Is.SameAs(staticScout.Materials.Bone));
            Assert.That(
                roadScout.Materials.Rubber,
                Is.SameAs(staticScout.Materials.Rubber));
            Assert.That(
                roadScout.Materials.Tungsten,
                Is.SameAs(staticScout.Materials.Tungsten));
            Assert.That(
                roadScout.Materials.Signal,
                Is.SameAs(staticScout.Materials.Signal));
        }

        private static void AssertModuleSocketAlignment(
            SashaScoutVisual scout)
        {
            Transform? winchSocket = scout.FindSocket(
                SashaScoutSemanticContract.FrontUpgradeSocketName);
            Transform? tankSocket = scout.FindSocket(
                SashaScoutSemanticContract.CargoUpgradeSocketName);
            Assert.That(winchSocket, Is.Not.Null);
            Assert.That(tankSocket, Is.Not.Null);
            Assert.That(scout.WinchModuleRoot, Is.Not.Null);
            Assert.That(scout.RangeTankModuleRoot, Is.Not.Null);
            Assert.That(
                Vector3.Distance(
                    scout.WinchModuleRoot!.position,
                    winchSocket!.position),
                Is.LessThan(0.00001f));
            Assert.That(
                Quaternion.Angle(
                    scout.WinchModuleRoot.rotation,
                    winchSocket.rotation),
                Is.LessThan(0.00001f));
            Assert.That(
                Vector3.Distance(
                    scout.RangeTankModuleRoot!.position,
                    tankSocket!.position),
                Is.LessThan(0.00001f));
            Assert.That(
                Quaternion.Angle(
                    scout.RangeTankModuleRoot.rotation,
                    tankSocket.rotation),
                Is.LessThan(0.00001f));
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
                LastBearingReadModel readModel =
                    LastBearingReadModel.FromState(state);
                if (readModel.IsWreckLineModulePointAvailable)
                {
                    state = Apply(kernel, state, sequence =>
                        new OperateWreckLineModuleCommand(
                            sequence,
                            readModel.RouteActionKind));
                }

                readModel = LastBearingReadModel.FromState(state);
                if (readModel.IsWreckLineFrameRailRecoveryAvailable)
                {
                    state = Apply(kernel, state, sequence =>
                        new RecoverWreckLineFrameRailsCommand(sequence));
                }

                state = Apply(kernel, state, sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                guard++;
            }

            Assert.That(state.ExpeditionPhase, Is.EqualTo(target));
            return state;
        }

        private static LastBearingState DriveUntilDepotRecoveryAvailable(
            LastBearingState state)
        {
            var kernel = new LastBearingKernel();
            var guard = 0;
            while (!LastBearingReadModel.FromState(state)
                       .IsDepotApproachRecoveryAvailable &&
                   guard < 1000)
            {
                LastBearingReadModel current =
                    LastBearingReadModel.FromState(state);
                if (current.IsWreckLineModulePointAvailable)
                {
                    state = Apply(kernel, state, sequence =>
                        new OperateWreckLineModuleCommand(
                            sequence,
                            current.RouteActionKind));
                }

                current = LastBearingReadModel.FromState(state);
                if (current.IsWreckLineFrameRailRecoveryAvailable)
                {
                    state = Apply(kernel, state, sequence =>
                        new RecoverWreckLineFrameRailsCommand(sequence));
                }

                state = Apply(kernel, state, sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                guard++;
            }

            LastBearingReadModel readModel = LastBearingReadModel.FromState(state);
            Assert.That(
                readModel.IsDepotApproachRecoveryAvailable,
                Is.True,
                "depot recovery gate was not reached within 1000 canonical drives");
            Assert.That(state.ExpeditionPhase, Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(state.RouteProgressTicks, Is.EqualTo(state.RouteTargetTicks));
            Assert.That(state.HasArrivalClaimSnapshot, Is.True);
            return state;
        }

        private static LastBearingState DriveUntilWreckLineAvailable(
            LastBearingState state)
        {
            var kernel = new LastBearingKernel();
            var guard = 0;
            while (!LastBearingReadModel.FromState(state)
                       .IsWreckLineModulePointAvailable &&
                   guard < 1000)
            {
                state = Apply(kernel, state, sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                guard++;
            }

            LastBearingReadModel readModel = LastBearingReadModel.FromState(state);
            Assert.That(
                readModel.IsWreckLineModulePointAvailable,
                Is.True,
                "Wreck Line was not reached within 1000 canonical drives");
            Assert.That(state.ExpeditionPhase, Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(
                state.RouteProgressTicks,
                Is.EqualTo(readModel.WreckLineGateTicks));
            Assert.That(state.RouteActionUsed, Is.False);
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

        private static void AssertCameraOwnership(
            LastBearingGameController controller,
            bool chaseActive)
        {
            LastBearingWorldBuilder world = controller.World!;
            Camera sharedCamera = world.MainCamera!;
            LastBearingCameraRig cameraRig = world.CameraRig!;
            RoadFeelChaseCamera chaseCamera = world.RoadChaseCamera!;
            AudioListener listener = sharedCamera.GetComponent<AudioListener>()!;

            Assert.That(listener, Is.Not.Null);
            Assert.That(cameraRig.gameObject, Is.SameAs(sharedCamera.gameObject));
            Assert.That(chaseCamera.gameObject, Is.SameAs(sharedCamera.gameObject));
            Assert.That(listener.gameObject, Is.SameAs(sharedCamera.gameObject));
            Assert.That(cameraRig.IsRoadChaseActive, Is.EqualTo(chaseActive));
            Assert.That(chaseCamera.IsChaseActive, Is.EqualTo(chaseActive));
            Assert.That(chaseCamera.enabled, Is.EqualTo(chaseActive));
            if (chaseActive)
            {
                Assert.That(
                    sharedCamera.fieldOfView,
                    Is.InRange(
                        RoadFeelChaseCamera.BaseFieldOfView,
                        RoadFeelChaseCamera.MaximumFieldOfView));
            }
            else
            {
                Assert.That(
                    sharedCamera.fieldOfView,
                    Is.EqualTo(LastBearingCameraRig.StrategyFieldOfView)
                        .Within(0.001f));
            }
        }

        private static void AssertChaseCameraLooksBehindSasha(
            LastBearingWorldBuilder world,
            RoadFeelRigInstance roadRig)
        {
            Transform target = roadRig.Root.transform;
            Camera sharedCamera = world.MainCamera!;
            Transform camera = sharedCamera.transform;
            Vector3 targetToCamera = camera.position - target.position;
            Assert.That(
                Vector3.Dot(targetToCamera.normalized, target.forward),
                Is.LessThan(-0.25f));
            Vector3 focus = target.position + (Vector3.up * 1.65f);
            Vector3 focusDirection = (focus - camera.position).normalized;
            Assert.That(
                Vector3.Dot(camera.forward, focusDirection),
                Is.GreaterThan(0.9f));
            Assert.That(
                sharedCamera.fieldOfView,
                Is.EqualTo(RoadFeelChaseCamera.BaseFieldOfView).Within(0.001f));
        }

        private static void AssertFixedRoadFallbackPose(
            Camera camera,
            Transform roadTarget)
        {
            Vector3 vehicleForward = roadTarget.forward;
            vehicleForward.y = 0f;
            if (vehicleForward.sqrMagnitude < 0.001f)
            {
                vehicleForward = Vector3.forward;
            }

            vehicleForward.Normalize();
            Vector3 focus = roadTarget.position + (Vector3.up * 1.15f);
            Vector3 expectedPosition =
                focus - (vehicleForward * 8.5f) + (Vector3.up * 3.8f);
            Assert.That(
                Vector3.Distance(camera.transform.position, expectedPosition),
                Is.LessThan(0.025f),
                "the moving fail-closed road target may retain a bounded " +
                "presentation-only smoothing offset while the fixed rig owns " +
                "the shared camera");
            Assert.That(
                Vector3.Dot(
                    camera.transform.forward,
                    (focus - camera.transform.position).normalized),
                Is.GreaterThan(0.999f));
            Assert.That(
                camera.fieldOfView,
                Is.EqualTo(LastBearingCameraRig.StrategyFieldOfView)
                    .Within(0.001f));
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
            if (!active)
            {
                AssertRoadControlsCleared(roadRig);
            }
        }

        private static void AssertRoadRecoveryHold(
            LastBearingGameController controller,
            RoadFeelRigInstance roadRig)
        {
            LastBearingWorldBuilder world = controller.World!;
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            Rigidbody body = roadRig.Vehicle.Body;
            Assert.That(roadRig.Root.activeInHierarchy, Is.True);
            Assert.That(roadRig.Adapter.IsRoadModeActive, Is.False);
            Assert.That(roadRig.Adapter.IsPhysicsSuspended, Is.True);
            Assert.That(roadRig.Vehicle.enabled, Is.False);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(coordinator.IsRoadPresentationActive, Is.False);
            Assert.That(coordinator.IsRoadPresentationHeldAtRecovery, Is.True);
            Assert.That(coordinator.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            AssertRoadControlsCleared(roadRig);
            AssertRecoveryUnavailableWithoutWrites(controller);
            Assert.That(world.VehicleView!.gameObject.activeSelf, Is.False);
            Assert.That(
                world.CameraRig!.RoadTarget,
                Is.SameAs(roadRig.Root.transform));
            Assert.That(
                Vector3.Distance(
                    body.position,
                    world.VehicleView.transform.position),
                Is.LessThan(0.001f));
            Assert.That(
                Quaternion.Angle(
                    body.rotation,
                    world.VehicleView.transform.rotation),
                Is.LessThan(0.01f));
            Assert.That(
                world.DepotApproachRecoveryView!.State,
                Is.EqualTo(DepotApproachRecoveryPresentationState.Available));
        }

        private static void AssertRoadModulePointHold(
            LastBearingGameController controller,
            RoadFeelRigInstance roadRig)
        {
            LastBearingWorldBuilder world = controller.World!;
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;
            Rigidbody body = roadRig.Vehicle.Body;
            Assert.That(roadRig.Root.activeInHierarchy, Is.True);
            Assert.That(roadRig.Adapter.IsRoadModeActive, Is.False);
            Assert.That(roadRig.Adapter.IsPhysicsSuspended, Is.True);
            Assert.That(roadRig.Vehicle.enabled, Is.False);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(coordinator.IsRoadPresentationActive, Is.False);
            Assert.That(coordinator.IsRoadPresentationHeldAtRecovery, Is.False);
            Assert.That(coordinator.IsRoadPresentationHeldAtModulePoint, Is.True);
            Assert.That(coordinator.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            AssertRoadControlsCleared(roadRig);
            AssertRecoveryUnavailableWithoutWrites(controller);
            Assert.That(world.VehicleView!.gameObject.activeSelf, Is.False);
            Assert.That(
                world.CameraRig!.RoadTarget,
                Is.SameAs(roadRig.Root.transform));
            Assert.That(
                Vector3.Distance(
                    body.position,
                    world.VehicleView.transform.position),
                Is.LessThan(0.001f));
            Assert.That(
                Quaternion.Angle(
                    body.rotation,
                    world.VehicleView.transform.rotation),
                Is.LessThan(0.01f));
        }

        private static int PendingCommandCount(
            LastBearingGameController controller)
        {
            FieldInfo? pendingField = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pendingField, Is.Not.Null);
            var pending = pendingField!.GetValue(controller) as ICollection;
            Assert.That(pending, Is.Not.Null);
            return pending!.Count;
        }

        private static LastBearingCommand[] PendingCommands(
            LastBearingGameController controller)
        {
            FieldInfo? pendingField = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pendingField, Is.Not.Null);
            var pending = pendingField!.GetValue(controller) as
                IEnumerable<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            return pending!.ToArray();
        }

        private static void AssertRecoveryUnavailableWithoutWrites(
            LastBearingGameController controller)
        {
            string hashBefore = controller.CanonicalHash;
            string statusBefore = controller.Status;
            string saveStatusBefore = controller.SaveStatus;
            int pendingBefore = PendingCommandCount(controller);
            LastBearingState? stateBefore = controller.State;

            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.RecoverRoadPresentation(), Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBefore));
            Assert.That(controller.Status, Is.EqualTo(statusBefore));
            Assert.That(controller.SaveStatus, Is.EqualTo(saveStatusBefore));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(pendingBefore));
            Assert.That(controller.State, Is.SameAs(stateBefore));
        }

        private static Transform RequireNamed(Transform root, string name)
        {
            foreach (Transform candidate in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            throw new AssertionException("Missing authored transform: " + name);
        }

        private static void ActivateWorldTarget(
            LastBearingGameController controller,
            LastBearingCityServiceCellInteractor interactor,
            string targetName)
        {
            Transform target = RequireNamed(interactor.transform, targetName);
            Vector3 screen =
                controller.World!.MainCamera!.WorldToScreenPoint(
                    target.position);
            var pointer = new Vector2(screen.x, screen.y);
            Assert.That(screen.z, Is.GreaterThan(0f), targetName);
            Assert.That(
                screen.x,
                Is.InRange(0f, (float)Screen.width),
                targetName + " leaves the horizontal viewport");
            Assert.That(
                screen.y,
                Is.InRange(0f, (float)Screen.height),
                targetName + " leaves the vertical viewport");
            Assert.That(
                controller.FieldDesk!.BlocksWorldPointer(pointer),
                Is.False,
                targetName + " is hidden behind the Field Desk");
            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.True,
                targetName + " cannot be activated through the world ray");
        }

        private static Vector2 RequireUnblockedScreenPoint(
            LastBearingGameController controller,
            Vector3 worldPosition,
            string context)
        {
            Camera camera = controller.World!.MainCamera!;
            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            var pointer = new Vector2(screen.x, screen.y);
            Assert.That(screen.z, Is.GreaterThan(0f), context);
            Assert.That(
                screen.x,
                Is.InRange(0f, (float)Screen.width),
                context + " leaves the horizontal viewport");
            Assert.That(
                screen.y,
                Is.InRange(0f, (float)Screen.height),
                context + " leaves the vertical viewport");
            Assert.That(
                controller.FieldDesk!.BlocksWorldPointer(pointer),
                Is.False,
                context + " is hidden behind the Field Desk");
            Assert.That(
                controller.Hud!.BlocksWorldPointer(pointer),
                Is.False,
                context + " is hidden behind the legacy HUD");
            return pointer;
        }

        private static void AssertRenderedRootInsideViewport(
            Camera camera,
            Transform renderedRoot)
        {
            Renderer[] renderers =
                renderedRoot.GetComponentsInChildren<Renderer>(false);
            Assert.That(renderers, Is.Not.Empty, renderedRoot.name);

            Bounds bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        Vector3 viewport = camera.WorldToViewportPoint(
                            center + new Vector3(
                                extents.x * x,
                                extents.y * y,
                                extents.z * z));
                        Assert.That(viewport.z, Is.GreaterThan(0f));
                        Assert.That(
                            viewport.x,
                            Is.InRange(0f, 1f),
                            renderedRoot.name + " leaves the horizontal viewport");
                        Assert.That(
                            viewport.y,
                            Is.InRange(0f, 1f),
                            renderedRoot.name + " leaves the vertical viewport");
                    }
                }
            }
        }

        private static Dictionary<string, string> SnapshotSaveFiles(
            string profileDirectory)
        {
            Assert.That(Directory.Exists(profileDirectory), Is.True);
            return Directory.GetFiles(
                    profileDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetRelativePath(profileDirectory, path),
                    path => Convert.ToBase64String(File.ReadAllBytes(path)),
                    StringComparer.Ordinal);
        }

        private static void AssertSaveSnapshot(
            IReadOnlyDictionary<string, string> expected,
            IReadOnlyDictionary<string, string> actual)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Assert.That(
                    actual[pair.Key],
                    Is.EqualTo(pair.Value),
                    "manual presentation recovery changed save file " + pair.Key);
            }
        }

        private static void CompleteDistrictObservation(
            LastBearingGameController controller,
            bool clear)
        {
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear);
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

        private static void InvokeRoadPresentationInput(
            LastBearingGameController controller)
        {
            MethodInfo? apply = typeof(LastBearingGameController).GetMethod(
                "ApplyRoadPresentationInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);
            apply!.Invoke(controller, null);
        }

        private static void InvokeGlobalShortcuts(
            LastBearingGameController controller)
        {
            MethodInfo? shortcuts = typeof(LastBearingGameController).GetMethod(
                "HandleGlobalShortcuts",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(shortcuts, Is.Not.Null);
            shortcuts!.Invoke(controller, null);
        }

        private static void ApplyWreckLineModel(
            LastBearingWreckLineInteractor interactor,
            LastBearingReadModel model)
        {
            MethodInfo? apply = typeof(LastBearingWreckLineInteractor).GetMethod(
                "Apply",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);
            apply!.Invoke(interactor, new object[] { model });
        }

        private static void ApplyDepotApproachModel(
            LastBearingDepotApproachInteractor interactor,
            LastBearingReadModel model)
        {
            MethodInfo? apply =
                typeof(LastBearingDepotApproachInteractor).GetMethod(
                    "Apply",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);
            apply!.Invoke(interactor, new object[] { model });
        }

        private string InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            if (_temporaryProfilesByController.TryGetValue(
                    controller,
                    out string existingProfile))
            {
                return existingProfile;
            }

            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "wp0002-last-bearing-tests-" + Guid.NewGuid().ToString("N"));
            string profileDirectory = Path.Combine(
                root,
                LastBearingProfileContract.ProfileName);
            Directory.CreateDirectory(root);
            _temporarySaveRoots.Add(root);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profileDirectory);
            ConstructorInfo? constructor = typeof(LastBearingSaveAdapter).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(LastBearingProfileStore) },
                modifiers: null);
            FieldInfo? adapterField = typeof(LastBearingGameController).GetField(
                "_saveAdapter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(adapterField, Is.Not.Null);
            var adapter = constructor!.Invoke(new object[] { store }) as
                LastBearingSaveAdapter;
            Assert.That(adapter, Is.Not.Null);
            adapterField!.SetValue(controller, adapter);
            _temporaryProfilesByController.Add(controller, profileDirectory);
            return profileDirectory;
        }

        private static string GetConfinementSafeTemporaryRoot()
        {
            string temporaryRoot = Path.GetTempPath();
            bool isMacOs = Application.platform == RuntimePlatform.OSXEditor ||
                           Application.platform == RuntimePlatform.OSXPlayer;
            if (isMacOs &&
                temporaryRoot.StartsWith("/var/", StringComparison.Ordinal))
            {
                return "/private" + temporaryRoot;
            }

            return temporaryRoot;
        }

        private void InstallControllerState(
            LastBearingGameController controller,
            LastBearingState state)
        {
            _ = InstallTemporarySaveAdapter(controller);
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

        private static void AssertRoadControlsCleared(
            RoadFeelRigInstance roadRig)
        {
            Assert.That(roadRig.Adapter.LastThrottleMilli, Is.Zero);
            Assert.That(roadRig.Adapter.LastBrakeMilli, Is.Zero);
            Assert.That(roadRig.Adapter.LastSteeringMilli, Is.Zero);
            Assert.That(roadRig.Adapter.LastHandbrakeMilli, Is.Zero);
        }

        private sealed class ThrowingRoadAdapter : ILastBearingRoadModeAdapter
        {
            private readonly bool _throwOnSynchronize;

            public ThrowingRoadAdapter(bool throwOnSynchronize = false)
            {
                _throwOnSynchronize = throwOnSynchronize;
            }

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

            public void ApplyPresentationOnlyControls(
                int brakeMilli,
                int handbrakeMilli)
            {
            }

            public void ApplyPresentationInputSample(
                int throttleMilli,
                int brakeMilli,
                int steeringMilli,
                int handbrakeMilli)
            {
                ApplyAttemptCount++;
                throw new InvalidOperationException("adversarial-road-adapter");
            }

            public void ApplyDerivedPresentationLoad(
                int cargoMassKilograms,
                LastBearingRoadDamageBand damageBand)
            {
            }

            public void SynchronizePresentationPose(
                Vector3 position,
                Quaternion rotation)
            {
                if (_throwOnSynchronize)
                {
                    throw new InvalidOperationException(
                        "adversarial-road-synchronization");
                }
            }

            public void ResetPresentation()
            {
            }
        }
    }
}
