#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingReceiveEmergencyAidPlayModeTests :
        InputTestFixture
    {
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<string> _saveRoots = new List<string>();

        [UnityTearDown]
        public IEnumerator TearDownRuntime()
        {
            foreach (GameObject root in _roots)
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            foreach (string root in _saveRoots)
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            _roots.Clear();
            _saveRoots.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeskRoutesFourFreshCyclesReceivesThenImprovesAndReloads()
        {
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            LastBearingState ready = CreateAidState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                6201,
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank,
                cooperate: true,
                useNonDefaultLayout: true,
                installRepair: true);
            long capacity =
                LastBearingReadModel.FromState(ready).WaterCapacityMilli;
            ready = WithWater(ready, capacity - 2500);
            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            yield return null;

            LastBearingReadModel readyModel = controller.ReadModel!;
            LastBearingEmergencyAidInteractor interactor =
                controller.World!.EmergencyAidInteractor!;
            AssertReadyCooperativeAid(readyModel);
            Assert.That(
                readyModel.IsCityImprovementInstallationAvailable,
                Is.True,
                "Workshop Push improvement must coexist with the tender.");
            Assert.That(interactor.HasDedicatedInteractionTarget, Is.True);
            Assert.That(interactor.IsWitnessVisible, Is.True);
            Assert.That(interactor.IsQueuedWaterVisible, Is.True);
            Assert.That(interactor.IsCoiledHoseVisible, Is.False);
            Assert.That(
                interactor.ReceiptLabel,
                Does.Contain("10.000 WATER TENDER"));
            AssertNoCityControlOverlap(controller, interactor);
            Assert.That(
                controller.GetComponentsInChildren<Camera>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                controller.GetComponentsInChildren<AudioListener>(true),
                Has.Length.EqualTo(1));

            string readyHash = controller.CanonicalHash;
            controller.Save();
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(interactor.IsControlFocused, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(controller.FieldDesk!.OwnsCityOverview, Is.False);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);

            interactor.ResetLocalFocus();
            controller.OpenBuildingCutaway();
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsControlVisible, Is.False);
            controller.ShowCityOverview();
            Assert.That(
                interactor.IsControlVisible,
                Is.True,
                "BuildingCutaway return did not synchronously restore the tender.");
            controller.OpenGarageBay();
            Assert.That(interactor.IsControlVisible, Is.False);
            controller.ShowCityOverview();
            Assert.That(
                interactor.IsControlVisible,
                Is.True,
                "Garage return did not synchronously restore the tender.");
            controller.FieldDesk.Refresh(force: true);
            byte[] routeBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            UIDocument document = controller
                .GetComponentsInChildren<UIDocument>(true)
                .Single();
            Button action = document.rootVisualElement
                .Q<Button>("primary-action-button");
            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.FieldDesk.Refresh(force: true);
                LastBearingFieldDeskProjection projection =
                    LastBearingFieldDeskPresenter.Present(controller);
                Assert.That(controller.FieldDesk.OwnsCityOverview, Is.True);
                Assert.That(
                    projection.PrimaryAction.Intent,
                    Is.EqualTo(
                        LastBearingFieldDeskIntent
                            .OpenEmergencyAidWaterTender));
                Assert.That(
                    action.text,
                    Is.EqualTo(
                        "OPEN EMERGENCY STORAGE · RECEIVE WATER TENDER"));
                Submit(action);
                Assert.That(
                    controller.ModeCoordinator.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.CityOverview));
                Assert.That(interactor.IsControlFocused, Is.True);
                Assert.That(controller.FieldDesk.OwnsCityOverview, Is.False);
                CollectionAssert.AreEqual(
                    routeBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));

                interactor.ResetLocalFocus();
                controller.FieldDesk.Refresh(force: true);
                Assert.That(controller.FieldDesk.OwnsCityOverview, Is.True);
                CollectionAssert.AreEqual(
                    routeBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                yield return null;
            }

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            Submit(action);
            Assert.That(interactor.IsControlFocused, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            LastBearingEmergencyCisternExpansionInteractor expansion =
                controller.World.CityServiceCellView!
                    .EmergencyCisternExpansionInteractor!;
            LastBearingCityServiceCellInteractor serviceCell =
                controller.World.CityServiceCellView.Interactor!;
            Assert.That(expansion.IsControlVisible, Is.True);
            Assert.That(serviceCell.IsHotShiftControlVisible, Is.True);
            expansion.FocusControl();
            serviceCell.FocusHotShiftControl();
            Assert.That(
                expansion.IsControlFocused,
                Is.False,
                "The simultaneous improvement stole tender input ownership.");
            Assert.That(
                serviceCell.IsHotShiftControlFocused,
                Is.False,
                "The Hot Shift control stole tender input ownership.");
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            Release(keyboard.eKey);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);

            float yawBefore = controller.World.CameraRig!.CityYaw;
            long sequence = controller.State!.NextCommandSequence;
            long waterBefore = controller.ReadModel!.WaterMilli;
            Press(keyboard.eKey);
            InvokeCameraUpdate(controller.World.CameraRig);
            InvokeInteractorUpdate(interactor);
            Release(keyboard.eKey);
            Assert.That(
                controller.World.CameraRig.CityYaw,
                Is.EqualTo(yawBefore).Within(0.0001f),
                "Focused tender input also rotated the strategy camera.");
            ReceiveEmergencyAidCommand queued =
                AssertExactAidCommand(controller, sequence);
            Assert.That(queued.Sequence, Is.EqualTo(sequence));
            controller.ReceiveEmergencyAid();
            Assert.That(
                PendingCommands(controller),
                Has.Count.EqualTo(1),
                "Duplicate receipt appended a second command.");
            CollectionAssert.AreEqual(
                routeBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            InvokeSimulationTick(controller);
            LastBearingReadModel received = controller.ReadModel!;
            Assert.That(
                received.FactionAidPolicy,
                Is.EqualTo(FactionAidPolicy.EmergencyWaterDelivered));
            Assert.That(
                received.EmergencyAidWaterMilli,
                Is.EqualTo(LastBearingBalanceV1.CooperateAidWaterMilli));
            Assert.That(received.WaterMilli, Is.EqualTo(capacity));
            Assert.That(
                received.WaterMilli - waterBefore,
                Is.EqualTo(2500),
                "The UI path ignored the current storage clamp.");
            Assert.That(
                received.FactionAccessPolicy,
                Is.EqualTo(FactionAccessPolicy.SharedService));
            Assert.That(received.MaintenanceObligationActive, Is.True);
            Assert.That(
                interactor.IsEmptyReceivedTenderVisible,
                Is.True);
            Assert.That(interactor.IsQueuedWaterVisible, Is.False);
            Assert.That(interactor.IsCoiledHoseVisible, Is.True);
            Assert.That(
                interactor.ReceiptLabel,
                Does.Contain("RECEIVED 10.000 WATER"));
            Assert.That(
                interactor.ReceiptLabel,
                Does.Contain("STORAGE " + FormatMilli(capacity) +
                             " / " + FormatMilli(capacity)));
            Assert.That(interactor.ReceiptLabel, Does.Contain("CAP HONORED"));
            Assert.That(
                interactor.ReceiptLabel,
                Does.Contain("MAINTENANCE PROMISE RETAINED"));
            Assert.That(controller.Status, Does.Contain("capacity clamp"));
            Assert.That(controller.SaveStatus, Does.Not.Contain("Unsaved"));

            controller.FieldDesk.Refresh(force: true);
            LastBearingFieldDeskProjection afterReceipt =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                afterReceipt.PrimaryAction.Intent,
                Is.EqualTo(
                    LastBearingFieldDeskIntent
                        .OpenEmergencyCisternExpansion),
                "Receipt-first hid the simultaneous city improvement.");

            controller.OpenEmergencyCisternExpansion();
            Assert.That(expansion.IsControlFocused, Is.True);
            yield return null;
            InvokeExpansionUpdate(expansion);
            Assert.That(expansion.IsInputArmed, Is.True);
            Assert.That(expansion.OperateFocused(), Is.True);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.InstalledCityImprovement,
                Is.EqualTo(CityImprovementKind.ExpandedEmergencyCistern));
            Assert.That(
                controller.ReadModel.FactionAidPolicy,
                Is.EqualTo(FactionAidPolicy.EmergencyWaterDelivered));
            Assert.That(
                interactor.IsEmptyReceivedTenderVisible,
                Is.True);

            string acceptedHash = controller.CanonicalHash;
            controller.Save();
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(acceptedHash));
            Assert.That(
                controller.ReadModel!.FactionAidPolicy,
                Is.EqualTo(FactionAidPolicy.EmergencyWaterDelivered));
            Assert.That(controller.IsEmergencyAidReceptionFocused, Is.False);
            controller.ShowCityOverview();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(
                interactor.IsEmptyReceivedTenderVisible,
                Is.True);
            Assert.That(interactor.IsCoiledHoseVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator ImprovementFirstRetainsTenderAndExactPointerReceipt()
        {
            LastBearingState ready = CreateAidState(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId,
                6202,
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank,
                cooperate: true,
                useNonDefaultLayout: false,
                installRepair: true);
            ready = Apply(
                ready,
                sequence => new InstallCityImprovementCommand(
                    sequence,
                    NextCityDecision.ExpandEmergencyCistern,
                    LastBearingState.EmergencyStorageExpansionSocketId,
                    LastBearingState
                        .EmergencyStorageExpansionOrientationQuarterTurns));
            LastBearingGameController controller =
                CreateController(ColonyComposition.RobotOnly);
            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            yield return null;

            LastBearingReadModel model = controller.ReadModel!;
            LastBearingEmergencyAidInteractor interactor =
                controller.World!.EmergencyAidInteractor!;
            Assert.That(
                model.InstalledCityImprovement,
                Is.EqualTo(CityImprovementKind.ExpandedEmergencyCistern));
            AssertReadyCooperativeAid(model);
            Assert.That(
                LastBearingFieldDeskPresenter.Present(controller)
                    .PrimaryAction.Intent,
                Is.EqualTo(
                    LastBearingFieldDeskIntent
                        .OpenEmergencyAidWaterTender));

            controller.OpenEmergencyAidWaterTender();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);
            Physics.SyncTransforms();
            Vector3 screen = controller.World.MainCamera!
                .WorldToScreenPoint(interactor.ControlWorldPosition);
            Assert.That(
                interactor.TryActivateAtScreenPosition(screen),
                Is.True);
            _ = AssertExactAidCommand(
                controller,
                ready.NextCommandSequence);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.FactionAidPolicy,
                Is.EqualTo(FactionAidPolicy.EmergencyWaterDelivered));
            Assert.That(
                controller.ReadModel.InstalledCityImprovement,
                Is.EqualTo(CityImprovementKind.ExpandedEmergencyCistern));
            Assert.That(
                controller.ReadModel.WaterCapacityMilli,
                Is.EqualTo(
                    LastBearingBalanceV1.WaterCapacityMilli +
                    LastBearingBalanceV1
                        .EmergencyCisternExpansionCapacityMilli));
            Assert.That(interactor.IsCoiledHoseVisible, Is.True);
            AssertNoCityControlOverlap(controller, interactor);
        }

        [UnityTest]
        public IEnumerator CompositionsModulesAndGamepadShareOneFailClosedTender()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            (ColonyComposition Composition, string Resident,
                VehicleModule Module, bool NonDefault)[] setups =
            {
                (
                    ColonyComposition.HumanOnly,
                    ResidentRoster.HumanResidentId,
                    VehicleModule.WinchAssembly,
                    false),
                (
                    ColonyComposition.RobotOnly,
                    ResidentRoster.RobotResidentId,
                    VehicleModule.SealedRangeTank,
                    false),
                (
                    ColonyComposition.Mixed,
                    ResidentRoster.HumanResidentId,
                    VehicleModule.WinchAssembly,
                    true),
            };

            for (var index = 0; index < setups.Length; index++)
            {
                var setup = setups[index];
                LastBearingState ready = CreateAidState(
                    setup.Composition,
                    setup.Resident,
                    6300 + index,
                    PreparationChoice.CivicBuffer,
                    setup.Module,
                    cooperate: true,
                    useNonDefaultLayout: setup.NonDefault,
                    installRepair: true);
                LastBearingGameController controller =
                    CreateController(setup.Composition);
                InstallControllerState(controller, ready);
                controller.ShowCityOverview();
                yield return null;

                LastBearingEmergencyAidInteractor interactor =
                    controller.World!.EmergencyAidInteractor!;
                AssertReadyCooperativeAid(controller.ReadModel!);
                Assert.That(interactor.IsTenderVisible, Is.True);
                AssertNoCityControlOverlap(controller, interactor);
                string readyHash = controller.CanonicalHash;

                controller.OpenGarageBay();
                controller.ReceiveEmergencyAid();
                Assert.That(PendingCommands(controller), Is.Empty);
                Assert.That(interactor.FocusControl(), Is.False);
                Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));

                controller.ShowCityOverview();
                controller.OpenEmergencyAidWaterTender();
                yield return null;
                InvokeInteractorUpdate(interactor);
                Assert.That(interactor.IsInputArmed, Is.True);

                long sequence = controller.State!.NextCommandSequence;
                Press(gamepad.buttonSouth);
                InvokeCameraUpdate(controller.World.CameraRig!);
                InvokeInteractorUpdate(interactor);
                Release(gamepad.buttonSouth);
                _ = AssertExactAidCommand(controller, sequence);
                Assert.That(interactor.OperateFocused(), Is.False);
                controller.ReceiveEmergencyAid();
                Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
                Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));
                _roots.Remove(controller.gameObject);
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PreRepairAdverseStaleAndPendingPathsQueueNothingExtra()
        {
            LastBearingState unrepaired = CreateAidState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                6401,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                cooperate: true,
                useNonDefaultLayout: false,
                installRepair: false);
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            InstallControllerState(controller, unrepaired);
            controller.ShowCityOverview();
            yield return null;

            LastBearingEmergencyAidInteractor interactor =
                controller.World!.EmergencyAidInteractor!;
            Assert.That(
                controller.ReadModel!.IsEmergencyAidReceptionAvailable,
                Is.False);
            Assert.That(interactor.IsWitnessVisible, Is.False);
            controller.OpenEmergencyAidWaterTender();
            controller.ReceiveEmergencyAid();
            Assert.That(PendingCommands(controller), Is.Empty);

            LastBearingState adverse = CreateAidState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                6402,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                cooperate: false,
                useNonDefaultLayout: false,
                installRepair: true);
            InstallControllerState(controller, adverse);
            controller.ShowCityOverview();
            yield return null;
            Assert.That(
                controller.ReadModel!.FactionAidPolicy,
                Is.EqualTo(FactionAidPolicy.Withheld));
            Assert.That(interactor.IsWitnessVisible, Is.False);
            controller.OpenEmergencyAidWaterTender();
            controller.ReceiveEmergencyAid();
            Assert.That(PendingCommands(controller), Is.Empty);

            LastBearingState ready = CreateAidState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                6403,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                cooperate: true,
                useNonDefaultLayout: true,
                installRepair: true);
            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            controller.OpenEmergencyAidWaterTender();
            yield return null;
            InvokeInteractorUpdate(interactor);
            string readyHash = controller.CanonicalHash;
            ReplaceRuntimeReadModel(
                controller,
                LastBearingReadModel.FromState(ready));
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(interactor.LastInteractionRejected, Is.True);
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));

            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            controller.OpenEmergencyAidWaterTender();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.OperateFocused(), Is.True);
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            Assert.That(interactor.OperateFocused(), Is.False);
            controller.ReceiveEmergencyAid();
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));
        }

        [Test]
        public void ForgedDeliveredLookingStateDoesNotShowAcceptedTender()
        {
            LastBearingState delivered = CreateAidState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                6404,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                cooperate: true,
                useNonDefaultLayout: false,
                installRepair: true);
            delivered = Apply(
                delivered,
                sequence => new ReceiveEmergencyAidCommand(sequence));
            LastBearingReadModel natural =
                LastBearingReadModel.FromState(delivered);
            Assert.That(
                IsAcceptedTenderWitness(natural),
                Is.True);
            Assert.That(
                LastBearingPermitJobPresenter
                    .Present(natural, cityNeedInspected: true)
                    .Detail,
                Does.Contain("tender was received"));

            (string Field, object Value)[] forgeries =
            {
                (
                    "FactionClaimState",
                    FactionClaimState.Aggrieved),
                (
                    "DepotControl",
                    DepotControl.FactionClaimed),
                (
                    "FactionTrust",
                    0L),
                (
                    "FactionGrievance",
                    1L),
                (
                    "RoutePermitGranted",
                    false),
                (
                    "FutureRouteTollFuelUnits",
                    LastBearingBalanceV1
                        .TakeFutureRouteTollFuelUnits),
                (
                    "DepotBearingDisposition",
                    DepotBearingDisposition.AtDepot),
                (
                    "PendingFactionOutcome",
                    FactionOutcomeKind.Adverse),
                (
                    "FactionOutcomeElapsedTicks",
                    LastBearingBalanceV1
                        .FactionOutcomeMaturationTicks - 1),
                (
                    "DepotAccessFeePartsUnits",
                    1L),
                (
                    "MaintenancePartsUnits",
                    LastBearingBalanceV1
                        .SleeveMaintenancePartsUnits + 1),
                (
                    "FactionMemory",
                    new FactionMemoryRecord(
                        "memory:last-bearing:cooperate:forged",
                        "CooperateAtBearingDepot",
                        LastBearingState.LastBearingFactionId,
                        LastBearingBalanceV1.CooperateTrustDelta,
                        "shared-maintenance",
                        delivered.GlobalTick,
                        "FIELD_SLEEVE_SERVICE")),
            };

            foreach (var forgery in forgeries)
            {
                LastBearingState forged = WithStateField(
                    delivered,
                    forgery.Field,
                    forgery.Value);
                LastBearingReadModel forgedModel =
                    LastBearingReadModel.FromState(forged);
                Assert.That(
                    forgedModel.FactionAccessPolicy,
                    Is.EqualTo(FactionAccessPolicy.SharedService));
                Assert.That(
                    forgedModel.FactionAidPolicy,
                    Is.EqualTo(
                        FactionAidPolicy.EmergencyWaterDelivered));
                Assert.That(
                    forgedModel.EmergencyAidWaterMilli,
                    Is.EqualTo(
                        LastBearingBalanceV1.CooperateAidWaterMilli));
                Assert.That(
                    IsAcceptedTenderWitness(forgedModel),
                    Is.False,
                    "Forged " + forgery.Field +
                    " still produced an accepted tender.");
                string forgedDetail =
                    LastBearingPermitJobPresenter
                        .Present(forgedModel, cityNeedInspected: true)
                        .Detail;
                Assert.That(
                    forgedDetail,
                    Does.Not.Contain("tender was received"),
                    "Forged " + forgery.Field +
                    " still produced accepted Permit Job copy.");
                Assert.That(
                    forgedDetail,
                    Does.Contain(
                        "waits beside Emergency Storage for physical receipt"),
                    "Forged " + forgery.Field +
                    " did not fail closed in Permit Job copy.");
            }
        }

        private LastBearingGameController CreateController(
            ColonyComposition composition)
        {
            var root = new GameObject(
                LastBearingGameController.RuntimeRootName);
            _roots.Add(root);
            var controller = root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(composition);
            if (composition == ColonyComposition.Mixed)
            {
                controller.AssignRoadHand(
                    ResidentRoster.HumanResidentId);
            }

            _ = InstallTemporarySaveAdapter(controller);
            return controller;
        }

        private string InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetSafeTempRoot(),
                "vgr22-save-" + Guid.NewGuid().ToString("N"));
            string profile = Path.Combine(
                root,
                LastBearingProfileContract.ProfileName);
            Directory.CreateDirectory(root);
            _saveRoots.Add(root);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            ConstructorInfo? constructor =
                typeof(LastBearingSaveAdapter).GetConstructor(
                    flags,
                    null,
                    new[] { typeof(LastBearingProfileStore) },
                    null);
            FieldInfo? field =
                typeof(LastBearingGameController).GetField(
                    "_saveAdapter",
                    flags);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(field, Is.Not.Null);
            field!.SetValue(
                controller,
                constructor!.Invoke(new object[] { store }));
            return profile;
        }

        private static string GetSafeTempRoot()
        {
            string root = Path.GetTempPath();
            return (Application.platform == RuntimePlatform.OSXEditor ||
                    Application.platform == RuntimePlatform.OSXPlayer) &&
                   root.StartsWith("/var/", StringComparison.Ordinal)
                ? "/private" + root
                : root;
        }

        private static LastBearingState CreateAidState(
            ColonyComposition composition,
            string resident,
            int seed,
            PreparationChoice choice,
            VehicleModule module,
            bool cooperate,
            bool useNonDefaultLayout,
            bool installRepair)
        {
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(composition, seed);
            state = Apply(
                state,
                sequence => new AssignResidentCommand(sequence, resident));
            if (useNonDefaultLayout)
            {
                state = Apply(
                    state,
                    sequence => new PlaceCityBuildingCommand(
                        sequence,
                        CityBuildingKind.Recycler,
                        0,
                        1));
                state = Apply(
                    state,
                    sequence => new PlaceCityBuildingCommand(
                        sequence,
                        CityBuildingKind.MachineShop,
                        4,
                        2));
                state = Apply(
                    state,
                    sequence => new PlaceCityBuildingCommand(
                        sequence,
                        CityBuildingKind.EmergencyStorage,
                        1,
                        3));
                state = Apply(
                    state,
                    sequence => new ConnectCityServiceLinkCommand(sequence));
                state = Apply(
                    state,
                    sequence => new AssignCityServiceResidentCommand(
                        sequence,
                        resident));
                state = Apply(
                    state,
                    sequence => new AdvanceCityServiceSledCommand(
                        sequence,
                        CityDeliveryStage.AtRecycler));
                state = Apply(
                    state,
                    sequence => new AdvanceCityServiceSledCommand(
                        sequence,
                        CityDeliveryStage.InTransit));
            }
            else
            {
                state = Apply(
                    state,
                    sequence =>
                        new ActivateSliceInfrastructureCommand(sequence));
            }

            state = Apply(
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    choice,
                    module));
            state = Apply(
                state,
                sequence => new InstallVehicleModuleCommand(
                    sequence,
                    module));
            while (LastBearingReadModel.FromState(state).PreparationPhase !=
                   PreparationPhase.Ready)
            {
                state = Advance(state);
            }

            string transactionId = "tx:vgr22:" + seed;
            string fingerprint = "fp:vgr22:" + seed;
            state = Apply(
                state,
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = Apply(
                state,
                sequence => new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            while (!LastBearingReadModel.FromState(state)
                       .IsDepotApproachRecoveryAvailable)
            {
                LastBearingReadModel model =
                    LastBearingReadModel.FromState(state);
                if (model.IsWreckLineModulePointAvailable)
                {
                    state = Apply(
                        state,
                        sequence => new OperateWreckLineModuleCommand(
                            sequence,
                            model.RouteActionKind));
                    model = LastBearingReadModel.FromState(state);
                }

                if (model.IsWreckLineFrameRailRecoveryAvailable)
                {
                    state = Apply(
                        state,
                        sequence =>
                            new RecoverWreckLineFrameRailsCommand(sequence));
                }

                state = Apply(
                    state,
                    sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            state = Apply(
                state,
                sequence => new OperateDepotRecoveryPointCommand(sequence));
            state = Apply(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    cooperate
                        ? EncounterChoice.Cooperate
                        : EncounterChoice.TakeBearing));
            state = Apply(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            if (module == VehicleModule.SealedRangeTank)
            {
                state = Apply(
                    state,
                    sequence => new ChooseLiquidReturnCommand(
                        sequence,
                        choice == PreparationChoice.WorkshopPush
                            ? LiquidCargoKind.Water
                            : LiquidCargoKind.Fuel));
            }

            state = Apply(
                state,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            while (LastBearingReadModel.FromState(state).ExpeditionPhase !=
                   ExpeditionPhase.Returned)
            {
                state = Apply(
                    state,
                    sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            state = Apply(
                state,
                sequence => new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = Apply(
                state,
                sequence => new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            if (installRepair)
            {
                state = Apply(
                    state,
                    sequence => new InstallTurbineRepairCommand(sequence));
            }

            LastBearingReadModel result =
                LastBearingReadModel.FromState(state);
            Assert.That(
                result.IsEmergencyAidReceptionAvailable,
                Is.EqualTo(cooperate && installRepair));
            return state;
        }

        private static LastBearingState WithWater(
            LastBearingState state,
            long waterMilli)
        {
            return WithStateField(
                state,
                "WaterMilli",
                waterMilli);
        }

        private static LastBearingState WithStateField(
            LastBearingState state,
            string fieldName,
            object value)
        {
            Type builderType = typeof(LastBearingState).Assembly.GetType(
                "AtomicLandPirate.Simulation.LastBearing.LastBearingStateBuilder",
                throwOnError: true)!;
            ConstructorInfo constructor = builderType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(LastBearingState) },
                null)!;
            object builder = constructor.Invoke(new object[] { state });
            builderType.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(builder, value);
            return (LastBearingState)builderType.GetMethod(
                    "Build",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(builder, null)!;
        }

        private static LastBearingState Apply(
            LastBearingState state,
            Func<long, LastBearingCommand> create)
        {
            return new LastBearingKernel()
                .Step(
                    state,
                    new[] { create(state.NextCommandSequence) })
                .State;
        }

        private static LastBearingState Advance(LastBearingState state)
        {
            return new LastBearingKernel()
                .Step(state, Array.Empty<LastBearingCommand>())
                .State;
        }

        private static void InstallControllerState(
            LastBearingGameController controller,
            LastBearingState state)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            controller.ModeCoordinator!.ClearSession();
            typeof(LastBearingGameController)
                .GetField("_state", flags)!
                .SetValue(controller, state);
            ReplaceRuntimeReadModel(
                controller,
                LastBearingReadModel.FromState(state));
            PendingCommands(controller).Clear();
            typeof(LastBearingGameController)
                .GetMethod("ResetPublicSnapshotsToRuntime", flags)!
                .Invoke(controller, null);
            typeof(LastBearingGameController)
                .GetMethod("ApplyPresentation", flags)!
                .Invoke(controller, null);
        }

        private static void ReplaceRuntimeReadModel(
            LastBearingGameController controller,
            LastBearingReadModel model)
        {
            typeof(LastBearingGameController)
                .GetField(
                    "_readModel",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(controller, model);
        }

        private static List<LastBearingCommand> PendingCommands(
            LastBearingGameController controller)
        {
            return (List<LastBearingCommand>)typeof(
                    LastBearingGameController)
                .GetField(
                    "_pendingCommands",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(controller)!;
        }

        private static ReceiveEmergencyAidCommand AssertExactAidCommand(
            LastBearingGameController controller,
            long sequence)
        {
            LastBearingCommand[] queued =
                PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(1));
            Assert.That(
                queued[0],
                Is.TypeOf<ReceiveEmergencyAidCommand>());
            var command = (ReceiveEmergencyAidCommand)queued[0];
            Assert.That(command.Sequence, Is.EqualTo(sequence));
            return command;
        }

        private static bool IsAcceptedTenderWitness(
            LastBearingReadModel model)
        {
            MethodInfo? method =
                typeof(LastBearingEmergencyAidInteractor).GetMethod(
                    "IsAcceptedWitness",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method!.Invoke(
                null,
                new object[] { model })!;
        }

        private static void AssertReadyCooperativeAid(
            LastBearingReadModel model)
        {
            Assert.That(model.IsEmergencyAidReceptionAvailable, Is.True);
            Assert.That(
                model.FactionAidPolicy,
                Is.EqualTo(FactionAidPolicy.EmergencyWaterQueued));
            Assert.That(
                model.EmergencyAidWaterMilli,
                Is.EqualTo(LastBearingBalanceV1.CooperateAidWaterMilli));
            Assert.That(
                model.FactionAccessPolicy,
                Is.EqualTo(FactionAccessPolicy.SharedService));
            Assert.That(
                model.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Consumed));
            Assert.That(model.MaintenanceObligationActive, Is.True);
        }

        private static void AssertNoCityControlOverlap(
            LastBearingGameController controller,
            LastBearingEmergencyAidInteractor interactor)
        {
            Physics.SyncTransforms();
            BoxCollider tender = interactor
                .GetComponentsInChildren<BoxCollider>(true)
                .Single(collider =>
                    collider.gameObject.name ==
                    LastBearingEmergencyAidInteractor.ControlName);
            foreach (BoxCollider other in controller.World!
                         .CityServiceCellView!
                         .GetComponentsInChildren<BoxCollider>(true))
            {
                if (ReferenceEquals(other, tender) ||
                    other.transform.IsChildOf(interactor.transform) ||
                    !other.gameObject.name.StartsWith(
                        "INTERACT_",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                bool overlaps = Physics.ComputePenetration(
                    tender,
                    tender.transform.position,
                    tender.transform.rotation,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out _,
                    out _);
                Assert.That(
                    overlaps,
                    Is.False,
                    "Tender control overlaps " + other.gameObject.name + ".");
            }

            Renderer inheritedStorage = controller.World
                .GetComponentsInChildren<Renderer>(true)
                .Single(renderer =>
                    renderer.gameObject.name == "Emergency Storage");
            foreach (Renderer tenderRenderer in interactor
                         .GetComponentsInChildren<Renderer>(true))
            {
                if (!tenderRenderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Assert.That(
                    tenderRenderer.bounds.Intersects(
                        inheritedStorage.bounds),
                    Is.False,
                    "Water tender clips inherited Emergency Storage: " +
                    tenderRenderer.gameObject.name);
            }
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            typeof(LastBearingGameController)
                .GetMethod(
                    "SimulateOneTick",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(controller, null);
        }

        private static void InvokeInteractorUpdate(
            LastBearingEmergencyAidInteractor interactor)
        {
            typeof(LastBearingEmergencyAidInteractor)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(interactor, null);
        }

        private static void InvokeExpansionUpdate(
            LastBearingEmergencyCisternExpansionInteractor interactor)
        {
            typeof(LastBearingEmergencyCisternExpansionInteractor)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(interactor, null);
        }

        private static void InvokeCameraUpdate(
            LastBearingCameraRig cameraRig)
        {
            typeof(LastBearingCameraRig)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(cameraRig, null);
        }

        private static string FormatMilli(long value)
        {
            return value.ToString(
                    "N0",
                    System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", ".");
        }

        private static void Submit(Button button)
        {
            using (NavigationSubmitEvent submit =
                   NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }
    }
}
