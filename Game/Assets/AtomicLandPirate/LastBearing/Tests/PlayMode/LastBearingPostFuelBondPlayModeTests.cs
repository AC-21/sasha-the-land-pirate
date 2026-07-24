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
    public sealed class LastBearingPostFuelBondPlayModeTests :
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
        public IEnumerator FieldDeskRoutesFourCyclesFreshKeyboardPostsAndReloads()
        {
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            LastBearingState ready = CreateFuelBondState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                5401,
                cooperate: false,
                useNonDefaultLayout: true);
            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            _ = InstallTemporarySaveAdapter(controller);
            yield return null;

            LastBearingReadModel readyModel = controller.ReadModel!;
            LastBearingOneGoodBatchCutawayView view =
                controller.World!.OneGoodBatchCutawayView!;
            LastBearingFuelBondInteractor interactor =
                view.FuelBondInteractor!;
            Assert.That(
                readyModel.IsDepotAccessRestorationAvailable,
                Is.True);
            Assert.That(
                readyModel.NextCityDecision,
                Is.EqualTo(NextCityDecision.RestoreDepotAccess));
            Assert.That(readyModel.LiquidCargoKind, Is.EqualTo(LiquidCargoKind.Fuel));
            Assert.That(
                readyModel.LiquidCargoQuantityMilli,
                Is.EqualTo(LastBearingBalanceV1.TankFuelReturnMilli));
            Assert.That(
                readyModel.LiquidCargoCustody,
                Is.EqualTo(LiquidCargoCustody.Settlement));
            Assert.That(interactor.HasDedicatedInteractionTarget, Is.True);
            Assert.That(view.IsPermitLockedVisible, Is.True);
            Assert.That(view.IsPermitGrantedVisible, Is.False);
            Assert.That(view.IsTwoFuelTollVisible, Is.True);
            Assert.That(view.IsBearingLotVisible, Is.False);
            Assert.That(
                view.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("RETURNED_FUEL_CAN_")),
                Is.EqualTo(LastBearingFuelBondInteractor.FuelCanCount));
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
                Is.EqualTo(LastBearingPresentationMode.BuildingCutaway));
            Assert.That(controller.World.IsOneGoodBatchCutawaySelected, Is.True);
            Assert.That(interactor.IsControlFocused, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);

            controller.ShowCityOverview();
            yield return null;
            byte[] routeBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            LastBearingFieldDesk desk = controller.FieldDesk!;
            UIDocument document = controller
                .GetComponentsInChildren<UIDocument>(true)
                .Single();
            Button action = document.rootVisualElement
                .Q<Button>("primary-action-button");
            for (var cycle = 0; cycle < 4; cycle++)
            {
                desk.Refresh(force: true);
                LastBearingFieldDeskProjection projection =
                    LastBearingFieldDeskPresenter.Present(controller);
                Assert.That(desk.OwnsCityOverview, Is.True);
                Assert.That(
                    projection.PrimaryAction.Intent,
                    Is.EqualTo(
                        LastBearingFieldDeskIntent
                            .OpenFuelBondClaimsWicket));
                Assert.That(
                    action.text,
                    Is.EqualTo(
                        "OPEN CLAIMS WICKET · POST FUEL BOND"));
                Submit(action);
                Assert.That(
                    controller.ModeCoordinator.CurrentMode,
                    Is.EqualTo(
                        LastBearingPresentationMode.BuildingCutaway));
                Assert.That(interactor.IsControlFocused, Is.True);
                Assert.That(desk.OwnsCityOverview, Is.False);
                CollectionAssert.AreEqual(
                    routeBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));

                controller.ShowCityOverview();
                yield return null;
                desk.Refresh(force: true);
                Assert.That(desk.OwnsCityOverview, Is.True);
                Assert.That(interactor.IsControlFocused, Is.False);
                Assert.That(
                    action.text,
                    Is.EqualTo(
                        "OPEN CLAIMS WICKET · POST FUEL BOND"));
                CollectionAssert.AreEqual(
                    routeBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
            }

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            Submit(action);
            Assert.That(interactor.IsControlFocused, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            Release(keyboard.eKey);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);

            long sequence = controller.State!.NextCommandSequence;
            long fuelBefore = controller.ReadModel!.FuelUnits;
            long grievanceBefore = controller.ReadModel.FactionGrievance;
            Vector3 offeredPosition = interactor.FuelLotLocalPosition;
            Press(keyboard.eKey);
            InvokeInteractorUpdate(interactor);
            Release(keyboard.eKey);
            RestoreDepotAccessCommand queued =
                AssertExactFuelBondCommand(controller, sequence);
            Assert.That(queued.Sequence, Is.EqualTo(sequence));
            controller.PostFuelBond();
            Assert.That(
                PendingCommands(controller),
                Has.Count.EqualTo(1),
                "A duplicate posting appended a second command.");
            CollectionAssert.AreEqual(
                routeBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            InvokeSimulationTick(controller);
            LastBearingReadModel posted = controller.ReadModel!;
            Assert.That(posted.RoutePermitGranted, Is.True);
            Assert.That(
                posted.FactionAccessPolicy,
                Is.EqualTo(FactionAccessPolicy.PermitRequired));
            Assert.That(
                posted.NextCityDecision,
                Is.EqualTo(NextCityDecision.None));
            Assert.That(
                posted.FuelUnits,
                Is.EqualTo(
                    fuelBefore -
                    LastBearingBalanceV1.TankFuelReturnMilli / 1000));
            Assert.That(
                posted.FactionGrievance,
                Is.EqualTo(grievanceBefore));
            Assert.That(
                posted.FactionAidPolicy,
                Is.EqualTo(FactionAidPolicy.Withheld));
            Assert.That(
                posted.FutureRouteTollFuelUnits,
                Is.EqualTo(
                    LastBearingBalanceV1.TakeFutureRouteTollFuelUnits));
            Assert.That(posted.LiquidCargoKind, Is.EqualTo(LiquidCargoKind.Fuel));
            Assert.That(
                posted.LiquidCargoQuantityMilli,
                Is.EqualTo(LastBearingBalanceV1.TankFuelReturnMilli),
                "The liquid fields remain return provenance.");
            Assert.That(
                posted.LiquidCargoCustody,
                Is.EqualTo(LiquidCargoCustody.Settlement));
            Assert.That(view.IsPermitLockedVisible, Is.False);
            Assert.That(view.IsPermitGrantedVisible, Is.True);
            Assert.That(view.IsTwoFuelTollVisible, Is.True);
            Assert.That(view.IsBearingLotVisible, Is.False);
            Assert.That(interactor.IsPostedWitnessVisible, Is.True);
            Assert.That(
                interactor.FuelLotLocalPosition,
                Is.Not.EqualTo(offeredPosition));
            Transform grating = view
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == "CUSTODY_GRATING");
            float gratingDepotEdge =
                grating.position.x +
                grating.lossyScale.x * 0.5f;
            foreach (Transform can in view
                         .GetComponentsInChildren<Transform>(true)
                         .Where(item =>
                             item.name.StartsWith(
                                 "RETURNED_FUEL_CAN_",
                                 StringComparison.Ordinal)))
            {
                float canSettlementEdge =
                    can.position.x -
                    can.lossyScale.x * 0.5f;
                Assert.That(
                    canSettlementEdge,
                    Is.GreaterThan(gratingDepotEdge),
                    can.name + " did not fully cross the custody grating.");
            }
            Assert.That(controller.Status, Does.Contain("grievance"));
            Assert.That(controller.Status, Does.Contain("two-fuel"));
            Assert.That(
                LastBearingPermitJobPresenter
                    .Present(posted, cityNeedInspected: true)
                    .Headline,
                Is.EqualTo("Permit posted. Grievance retained."));
            Assert.That(controller.SaveStatus, Does.Not.Contain("Unsaved"));

            string postedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(postedHash));
            Assert.That(controller.ReadModel!.RoutePermitGranted, Is.True);
            Assert.That(controller.IsFuelBondFocused, Is.False);
            controller.World!.SelectOneGoodBatchCutaway();
            controller.OpenBuildingCutaway();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsPostedWitnessVisible, Is.True);
            Assert.That(view.IsPermitGrantedVisible, Is.True);
            Assert.That(view.IsTwoFuelTollVisible, Is.True);
            Assert.That(view.IsBearingLotVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator InputsCompositionsWrongModeStaleAndPendingFailClosed()
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
                LastBearingState ready = CreateFuelBondState(
                    setup.Composition,
                    setup.Resident,
                    5500 + (int)setup.Composition,
                    cooperate: false,
                    useNonDefaultLayout:
                        setup.Composition == ColonyComposition.Mixed);
                InstallControllerState(controller, ready);
                controller.ShowCityOverview();
                yield return null;

                LastBearingOneGoodBatchCutawayView view =
                    controller.World!.OneGoodBatchCutawayView!;
                LastBearingFuelBondInteractor interactor =
                    view.FuelBondInteractor!;
                Assert.That(
                    view.IsHumanWorkerVisible,
                    Is.EqualTo(
                        setup.Composition != ColonyComposition.RobotOnly));
                Assert.That(
                    view.IsRobotWorkerVisible,
                    Is.EqualTo(
                        setup.Composition != ColonyComposition.HumanOnly));

                string readyHash = controller.CanonicalHash;
                controller.OpenGarageBay();
                controller.PostFuelBond();
                Assert.That(PendingCommands(controller), Is.Empty);
                Assert.That(interactor.FocusControl(), Is.False);
                Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));

                controller.ShowCityOverview();
                controller.OpenFuelBondClaimsWicket();
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
                    Physics.SyncTransforms();
                    Vector3 screen = controller.World.MainCamera!
                        .WorldToScreenPoint(interactor.ControlWorldPosition);
                    Assert.That(
                        interactor.TryActivateAtScreenPosition(screen),
                        Is.True);
                }

                _ = AssertExactFuelBondCommand(controller, sequence);
                Assert.That(interactor.OperateFocused(), Is.False);
                controller.PostFuelBond();
                Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
                InvokeInteractorUpdate(interactor);
                Assert.That(interactor.IsInputArmed, Is.False);
                Assert.That(interactor.Feedback, Does.Contain("QUEUED"));
                Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));

                if (setup.Composition == ColonyComposition.Mixed)
                {
                    InstallControllerState(controller, ready);
                    controller.ShowCityOverview();
                    controller.OpenFuelBondClaimsWicket();
                    yield return null;
                    InvokeInteractorUpdate(interactor);
                    typeof(LastBearingGameController)
                        .GetField(
                            "_readModel",
                            BindingFlags.Instance |
                            BindingFlags.NonPublic)!
                        .SetValue(
                            controller,
                            LastBearingReadModel.FromState(ready));
                    Assert.That(interactor.OperateFocused(), Is.False);
                    Assert.That(interactor.LastInteractionRejected, Is.True);
                    Assert.That(PendingCommands(controller), Is.Empty);
                    Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));
                }
            }
        }

        [UnityTest]
        public IEnumerator CooperativeSharedServiceForBothModulesNeverLooksPaid()
        {
            foreach (VehicleModule module in new[]
                     {
                         VehicleModule.WinchAssembly,
                         VehicleModule.SealedRangeTank,
                     })
            {
                LastBearingGameController controller =
                    CreateController(ColonyComposition.RobotOnly);
                LastBearingState cooperative = CreateFuelBondState(
                    ColonyComposition.RobotOnly,
                    ResidentRoster.RobotResidentId,
                    5601 + (int)module,
                    cooperate: true,
                    useNonDefaultLayout: false,
                    module);
                InstallControllerState(controller, cooperative);
                controller.ShowCityOverview();
                yield return null;

                LastBearingReadModel model = controller.ReadModel!;
                Assert.That(
                    model.FactionAccessPolicy,
                    Is.EqualTo(FactionAccessPolicy.SharedService));
                Assert.That(model.RoutePermitGranted, Is.True);
                Assert.That(
                    model.NextCityDecision,
                    Is.EqualTo(NextCityDecision.None));
                Assert.That(
                    model.IsDepotAccessRestorationAvailable,
                    Is.False);

                LastBearingFieldDeskProjection projection =
                    LastBearingFieldDeskPresenter.Present(controller);
                Assert.That(
                    projection.PrimaryAction.Intent,
                    Is.Not.EqualTo(
                        LastBearingFieldDeskIntent
                            .OpenFuelBondClaimsWicket));
                LastBearingPermitJobPresentation job =
                    LastBearingPermitJobPresenter.Present(
                        model,
                        cityNeedInspected: true);
                Assert.That(
                    job.Headline,
                    Is.EqualTo("The depot gate stayed open"));
                Assert.That(
                    job.Detail,
                    Does.Contain("No paid fuel bond is pending"));

                controller.World!.SelectOneGoodBatchCutaway();
                controller.OpenBuildingCutaway();
                yield return null;
                LastBearingFuelBondInteractor interactor =
                    controller.World.FuelBondInteractor!;
                InvokeInteractorUpdate(interactor);
                Assert.That(interactor.IsWitnessVisible, Is.False);
                Assert.That(interactor.IsControlFocused, Is.False);
                controller.PostFuelBond();
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
                "vgr21-save-" + Guid.NewGuid().ToString("N"));
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

        private static LastBearingState CreateFuelBondState(
            ColonyComposition composition,
            string resident,
            int seed,
            bool cooperate,
            bool useNonDefaultLayout,
            VehicleModule module = VehicleModule.SealedRangeTank)
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
                    PreparationChoice.CivicBuffer,
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

            string transactionId = "tx:vgr21:" + seed;
            string fingerprint = "fp:vgr21:" + seed;
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
                        LiquidCargoKind.Fuel));
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
            state = Apply(
                state,
                sequence => new InstallTurbineRepairCommand(sequence));
            LastBearingReadModel modelAfterRepair =
                LastBearingReadModel.FromState(state);
            Assert.That(
                modelAfterRepair.NextCityDecision,
                Is.EqualTo(
                    cooperate
                        ? NextCityDecision.None
                        : module == VehicleModule.SealedRangeTank
                            ? NextCityDecision.RestoreDepotAccess
                            : NextCityDecision.MachineSpareBearing));
            Assert.That(
                modelAfterRepair.IsDepotAccessRestorationAvailable,
                Is.EqualTo(
                    !cooperate &&
                    module == VehicleModule.SealedRangeTank));
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

        private static RestoreDepotAccessCommand
            AssertExactFuelBondCommand(
                LastBearingGameController controller,
                long sequence)
        {
            LastBearingCommand[] queued =
                PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(1));
            Assert.That(
                queued[0],
                Is.TypeOf<RestoreDepotAccessCommand>());
            var command = (RestoreDepotAccessCommand)queued[0];
            Assert.That(command.Sequence, Is.EqualTo(sequence));
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
            LastBearingFuelBondInteractor interactor)
        {
            typeof(LastBearingFuelBondInteractor)
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
