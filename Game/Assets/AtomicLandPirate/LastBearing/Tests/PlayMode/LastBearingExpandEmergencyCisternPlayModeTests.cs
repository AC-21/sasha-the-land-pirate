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
    public sealed class LastBearingExpandEmergencyCisternPlayModeTests :
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
        public IEnumerator FieldDeskRouteFreshKeyboardAndAcceptedWitnessAreExact()
        {
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            LastBearingState ready =
                CreateExpansionReadyState(
                    ColonyComposition.Mixed,
                    ResidentRoster.HumanResidentId,
                    5201);
            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            _ = InstallTemporarySaveAdapter(controller);
            yield return null;

            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingEmergencyCisternExpansionInteractor interactor =
                view.EmergencyCisternExpansionInteractor!;
            Assert.That(interactor.HasDedicatedInteractionTarget, Is.True);
            Assert.That(interactor.IsControlVisible, Is.True);
            Assert.That(view.IsEmergencyCisternExpansionSaddleVisible, Is.False);
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
                Is.EqualTo(LastBearingPresentationMode.CityOverview),
                "A ready cistern expansion load must not enter the pump hall.");
            Assert.That(
                controller.IsCityImprovementInstallationAvailable,
                Is.False,
                "The auxiliary-pump presentation must stay unavailable.");
            Assert.That(
                controller.CanOpenEmergencyCisternExpansion,
                Is.True);
            Assert.That(interactor.IsControlVisible, Is.True);
            Assert.That(PendingCommands(controller), Is.Empty);
            yield return null;

            LastBearingFieldDeskProjection projection =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                projection.PrimaryAction.Intent,
                Is.EqualTo(
                    LastBearingFieldDeskIntent
                        .OpenEmergencyCisternExpansion));
            Assert.That(
                projection.PrimaryAction.Label,
                Is.EqualTo(
                    "OPEN EMERGENCY STORAGE · EXPAND CISTERN"));
            Assert.That(projection.PrimaryAction.IsEnabled, Is.True);

            LastBearingFieldDesk desk = controller.FieldDesk!;
            desk.Refresh(force: true);
            Button expansionAction = controller
                .GetComponentsInChildren<UIDocument>(true)
                .Single()
                .rootVisualElement
                .Q<Button>("primary-action-button");
            Assert.That(expansionAction, Is.Not.Null);
            Assert.That(
                expansionAction.text,
                Is.EqualTo("OPEN EMERGENCY STORAGE · EXPAND CISTERN"));

            byte[] routeBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string routeHash = controller.CanonicalHash;
            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.OpenGarageBay();
                Assert.That(interactor.IsControlVisible, Is.False);
                Assert.That(interactor.IsControlFocused, Is.False);
                controller.ShowCityOverview();
                Assert.That(interactor.IsControlVisible, Is.True);
                CollectionAssert.AreEqual(
                    routeBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                Assert.That(controller.CanonicalHash, Is.EqualTo(routeHash));
            }

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            float yawBefore = controller.World.CameraRig!.CityYaw;
            Submit(expansionAction);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(interactor.IsControlFocused, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(
                desk.OwnsCityOverview,
                Is.False,
                "The Field Desk must stay stowed while the expansion handwheel owns focus.");
            Assert.That(
                controller.Hud!.enabled,
                Is.False,
                "Routing must not expose the legacy overlay over the physical handwheel.");
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(
                controller.World.CameraRig.CityYaw,
                Is.EqualTo(yawBefore).Within(0.0001f));
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            CollectionAssert.AreEqual(
                routeBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            Release(keyboard.eKey);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);

            float baseColumn =
                view.Interactor!.DryLineWaterColumnNormalized;
            float baseMarker =
                view.Interactor.DryLineMarkerNormalized;
            long projectedBefore =
                LastBearingFieldDeskPresenter
                    .ProjectDryLine(controller.ReadModel!)
                    .ProjectedWaterMilli;
            long sequence = controller.State!.NextCommandSequence;
            Press(keyboard.eKey);
            InvokeInteractorUpdate(interactor);
            Release(keyboard.eKey);

            InstallCityImprovementCommand queued =
                AssertExactExpansionCommand(controller, sequence);
            Assert.That(
                queued.SocketId,
                Is.EqualTo(
                    LastBearingState.EmergencyStorageExpansionSocketId));
            Assert.That(
                queued.OrientationQuarterTurns,
                Is.EqualTo(
                    LastBearingState
                        .EmergencyStorageExpansionOrientationQuarterTurns));
            controller.InstallEmergencyCisternExpansion();
            Assert.That(
                PendingCommands(controller),
                Has.Count.EqualTo(1),
                "duplicate operation appended a second command");
            CollectionAssert.AreEqual(
                routeBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.InstalledCityImprovement,
                Is.EqualTo(
                    CityImprovementKind.ExpandedEmergencyCistern));
            Assert.That(controller.ReadModel.WaterCapacityMilli, Is.EqualTo(210000));
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(2));
            Assert.That(controller.ReadModel.LiquidCargoKind, Is.EqualTo(LiquidCargoKind.Water));
            Assert.That(
                controller.ReadModel.LiquidCargoQuantityMilli,
                Is.EqualTo(LastBearingBalanceV1.TankWaterReturnMilli));
            Assert.That(
                controller.ReadModel.LiquidCargoCustody,
                Is.EqualTo(LiquidCargoCustody.Settlement));
            Assert.That(interactor.IsControlVisible, Is.False);
            Assert.That(view.IsEmergencyCisternExpansionSaddleVisible, Is.True);
            Assert.That(
                view.Interactor.DryLineWaterColumnNormalized,
                Is.LessThan(baseColumn));
            Assert.That(
                view.Interactor.DryLineMarkerNormalized,
                Is.LessThan(baseMarker));
            Assert.That(
                view.Interactor.DryLineMarkerNormalized,
                Is.EqualTo(60000f / 210000f).Within(0.0001f));
            Assert.That(
                LastBearingFieldDeskPresenter
                    .ProjectDryLine(controller.ReadModel)
                    .ProjectedWaterMilli,
                Is.GreaterThan(projectedBefore));

            controller.Save();
            string installedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            Assert.That(view.IsEmergencyCisternExpansionSaddleVisible, Is.False);
            controller.Load();
            controller.ShowCityOverview();
            Assert.That(controller.CanonicalHash, Is.EqualTo(installedHash));
            Assert.That(controller.ReadModel!.WaterCapacityMilli, Is.EqualTo(210000));
            Assert.That(view.IsEmergencyCisternExpansionSaddleVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator NonDefaultLayoutKeepsPhysicalTargetsDistinctAndStaleSafe()
        {
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            LastBearingState ready = CreateExpansionReadyState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                5251,
                useNonDefaultLayout: true);
            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            yield return null;

            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingEmergencyCisternExpansionInteractor interactor =
                view.EmergencyCisternExpansionInteractor!;
            Transform expansion = view.transform.Find(
                LastBearingEmergencyCisternExpansionInteractor.ControlName)!;
            Transform pump = view.transform.Find(
                LastBearingCityServiceCellInteractor
                    .EmergencyCisternPumpControlName)!;
            Assert.That(expansion, Is.Not.Null);
            Assert.That(pump, Is.Not.Null);
            Assert.That(
                Vector3.Distance(
                    expansion.localPosition,
                    pump.localPosition),
                Is.GreaterThan(1.5f),
                "Valid authored layouts must keep the two physical targets distinct.");

            string staleHash = controller.CanonicalHash;
            typeof(LastBearingGameController)
                .GetField(
                    "_readModel",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(
                    controller,
                    LastBearingReadModel.FromState(ready));
            interactor.FocusControl();
            Assert.That(interactor.LastInteractionRejected, Is.True);
            Assert.That(interactor.IsControlFocused, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(controller.CanonicalHash, Is.EqualTo(staleHash));

            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            yield return null;
            LastBearingFieldDesk desk = controller.FieldDesk!;
            desk.Refresh(force: true);
            Button expansionAction = controller
                .GetComponentsInChildren<UIDocument>(true)
                .Single()
                .rootVisualElement
                .Q<Button>("primary-action-button");
            Submit(expansionAction);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);

            Physics.SyncTransforms();
            Vector3 screen = controller.World.MainCamera!
                .WorldToScreenPoint(
                    expansion.GetComponent<Collider>().bounds.center);
            long sequence = controller.State!.NextCommandSequence;
            Assert.That(
                interactor.TryActivateAtScreenPosition(screen),
                Is.True);
            _ = AssertExactExpansionCommand(controller, sequence);
        }

        [UnityTest]
        public IEnumerator GamepadPointerAndThreeCompositionsStayNeutral()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            foreach ((ColonyComposition Composition, string Resident)
                     setup in new[]
                     {
                         (
                             ColonyComposition.HumanOnly,
                             ResidentRoster.HumanResidentId),
                         (
                             ColonyComposition.RobotOnly,
                             ResidentRoster.RobotResidentId),
                         (
                             ColonyComposition.Mixed,
                             ResidentRoster.HumanResidentId),
                     })
            {
                LastBearingGameController controller =
                    CreateController(setup.Composition);
                LastBearingState ready = CreateExpansionReadyState(
                    setup.Composition,
                    setup.Resident,
                    5300 + (int)setup.Composition);
                InstallControllerState(controller, ready);
                controller.ShowCityOverview();
                yield return null;

                LastBearingEmergencyCisternExpansionInteractor interactor =
                    controller.World!.CityServiceCellView!
                        .EmergencyCisternExpansionInteractor!;
                LastBearingFieldDeskProjection projection =
                    LastBearingFieldDeskPresenter.Present(controller);
                Assert.That(
                    projection.PrimaryAction.Intent,
                    Is.EqualTo(
                        LastBearingFieldDeskIntent
                            .OpenEmergencyCisternExpansion));
                Assert.That(projection.PrimaryAction.IsEnabled, Is.True);
                Assert.That(
                    controller.ReadModel!.CityImprovementPartsCostUnits,
                    Is.EqualTo(2));

                controller.OpenEmergencyCisternExpansion();
                yield return null;
                InvokeInteractorUpdate(interactor);
                Assert.That(interactor.IsInputArmed, Is.True);
                long sequence = controller.State!.NextCommandSequence;
                if (setup.Composition == ColonyComposition.HumanOnly)
                {
                    Press(gamepad.buttonSouth);
                    InvokeInteractorUpdate(interactor);
                    Release(gamepad.buttonSouth);
                }
                else
                {
                    Transform control = interactor.transform.Find(
                        LastBearingEmergencyCisternExpansionInteractor
                            .ControlName)!;
                    Physics.SyncTransforms();
                    Vector3 screen = controller.World.MainCamera!
                        .WorldToScreenPoint(
                            control.GetComponent<Collider>().bounds.center);
                    Assert.That(
                        interactor.TryActivateAtScreenPosition(screen),
                        Is.True);
                }

                _ = AssertExactExpansionCommand(controller, sequence);
                InstallControllerState(controller, ready);
                controller.OpenGarageBay();
                controller.OpenEmergencyCisternExpansion();
                Assert.That(interactor.IsControlFocused, Is.False);
                Assert.That(PendingCommands(controller), Is.Empty);
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
            return controller;
        }

        private string InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetSafeTempRoot(),
                "vgr20-save-" + Guid.NewGuid().ToString("N"));
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

        private static LastBearingState CreateExpansionReadyState(
            ColonyComposition composition,
            string resident,
            int seed,
            bool useNonDefaultLayout = false)
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
                        0));
                state = Apply(
                    state,
                    sequence => new PlaceCityBuildingCommand(
                        sequence,
                        CityBuildingKind.MachineShop,
                        4,
                        0));
                state = Apply(
                    state,
                    sequence => new PlaceCityBuildingCommand(
                        sequence,
                        CityBuildingKind.EmergencyStorage,
                        1,
                        0));
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
                    PreparationChoice.WorkshopPush,
                    VehicleModule.SealedRangeTank));
            state = Apply(
                state,
                sequence => new InstallVehicleModuleCommand(
                    sequence,
                    VehicleModule.SealedRangeTank));
            while (LastBearingReadModel.FromState(state).PreparationPhase !=
                   PreparationPhase.Ready)
            {
                state = Advance(state);
            }

            string transactionId = "tx:vgr20:" + seed;
            string fingerprint = "fp:vgr20:" + seed;
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
                    EncounterChoice.TakeBearing));
            state = Apply(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            state = Apply(
                state,
                sequence => new ChooseLiquidReturnCommand(
                    sequence,
                    LiquidCargoKind.Water));
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
            state = Apply(
                state,
                sequence => new InstallTurbineRepairCommand(sequence));
            LastBearingReadModel ready = LastBearingReadModel.FromState(state);
            Assert.That(
                ready.NextCityDecision,
                Is.EqualTo(NextCityDecision.ExpandEmergencyCistern));
            Assert.That(
                ready.IsCityImprovementInstallationAvailable,
                Is.True);
            Assert.That(ready.LiquidCargoKind, Is.EqualTo(LiquidCargoKind.Water));
            return state;
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
            typeof(LastBearingGameController)
                .GetField("_readModel", flags)!
                .SetValue(controller, LastBearingReadModel.FromState(state));
            PendingCommands(controller).Clear();
            typeof(LastBearingGameController)
                .GetMethod("ResetPublicSnapshotsToRuntime", flags)!
                .Invoke(controller, null);
            typeof(LastBearingGameController)
                .GetMethod("ApplyPresentation", flags)!
                .Invoke(controller, null);
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

        private static InstallCityImprovementCommand
            AssertExactExpansionCommand(
                LastBearingGameController controller,
                long sequence)
        {
            LastBearingCommand[] queued =
                PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(1));
            Assert.That(
                queued[0],
                Is.TypeOf<InstallCityImprovementCommand>());
            var command = (InstallCityImprovementCommand)queued[0];
            Assert.That(command.Sequence, Is.EqualTo(sequence));
            Assert.That(
                command.Decision,
                Is.EqualTo(NextCityDecision.ExpandEmergencyCistern));
            return command;
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
            LastBearingEmergencyCisternExpansionInteractor interactor)
        {
            typeof(LastBearingEmergencyCisternExpansionInteractor)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(interactor, null);
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
