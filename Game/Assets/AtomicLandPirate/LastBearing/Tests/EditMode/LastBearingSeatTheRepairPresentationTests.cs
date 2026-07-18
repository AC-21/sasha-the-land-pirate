#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingSeatTheRepairPresentationTests
    {
        private const string TransactionId =
            "transaction:last-bearing:vgr11-editmode:nondefault";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:vgr11-editmode:nondefault";
        private GameObject? _root;
        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }
        [Test]
        public void ReturnServiceHasFixedPhysicsFreeTruthAndExactWorkers()
        {
            LastBearingGameController controller = CreateController();
            LastBearingReturnServiceView view =
                controller.World!.ReturnServiceView!;
            AssertAnchor(view.CameraAnchor,
                LastBearingReturnServiceView.CameraAnchorName);
            AssertAnchor(view.FocusAnchor,
                LastBearingReturnServiceView.FocusAnchorName);
            AssertAnchor(view.VehicleAnchor,
                LastBearingReturnServiceView.VehicleAnchorName);
            AssertAnchor(view.CheckInAnchor,
                LastBearingReturnServiceView.CheckInAnchorName);
            AssertAnchor(view.PumpHallApproachAnchor,
                LastBearingReturnServiceView.PumpHallApproachAnchorName);
            AssertAnchor(view.ExitAnchor,
                LastBearingReturnServiceView.ExitAnchorName);
            Assert.That(view.IsCheckInMarkerVisible, Is.False);
            Assert.That(view.IsPumpHallApproachVisible, Is.True);
            Assert.That(view.IsExitRouteVisible, Is.True);
            Assert.That(view.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<AudioListener>(true),
                Is.Empty);
            Assert.That(view.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<CharacterController>(true),
                Is.Empty);
            foreach (Collider collider in
                     view.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }
            string[] cargoTerms = { "cargo", "ceramic bearing", "field sleeve" };
            Renderer[] cargoRenderers = view
                .GetComponentsInChildren<Renderer>(true)
                .Where(renderer => cargoTerms.Any(term =>
                    renderer.name.IndexOf(
                        term,
                        StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();
            Assert.That(
                cargoRenderers,
                Is.Empty,
                "the apron must not build a second solid repair-cargo proxy");
            view.Apply(
                checkInReady: true,
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Vehicle,
                humanVisible: true,
                robotVisible: true);
            Assert.That(view.IsCheckInMarkerVisible, Is.True);
            ColonyComposition[] compositions =
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            };
            foreach (ColonyComposition composition in compositions)
            {
                controller.StartNewGame(composition);
                Assert.That(
                    view.IsHumanWorkerVisible,
                    Is.EqualTo(composition != ColonyComposition.RobotOnly),
                    composition.ToString());
                Assert.That(
                    view.IsRobotWorkerVisible,
                    Is.EqualTo(composition != ColonyComposition.HumanOnly),
                    composition.ToString());
                Assert.That(view.IsCheckInMarkerVisible, Is.False);
            }
        }
        [Test]
        public void ReturnCheckInQueuesExactIdentityPairOnceAndRejectsWrongMode()
        {
            LastBearingGameController controller = CreateController();
            LastBearingState returned = CreateReturnedState(
                EncounterChoice.TakeBearing);
            InstallControllerState(controller, returned);
            Assert.That(returned.ExpeditionPhase, Is.EqualTo(ExpeditionPhase.Returned));
            Assert.That(
                returned.TransactionPhase,
                Is.EqualTo(TransactionPhase.ReturnPending));
            Assert.That(returned.ReturnPayloadFrozen, Is.True);
            Assert.That(
                returned.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(returned.TransactionId, Is.EqualTo(TransactionId));
            Assert.That(
                returned.TransactionFingerprint,
                Is.EqualTo(TransactionFingerprint));
            Assert.That(
                TransactionId,
                Is.Not.EqualTo("transaction:last-bearing:unity:0001"));
            Assert.That(controller.IsReturnCheckInAvailable, Is.True);
            byte[] validBytes = LastBearingCanonicalCodec.Encode(returned);
            controller.CompleteReturn();
            LastBearingCommand[] queued = PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(2));
            Assert.That(queued[0], Is.TypeOf<CreditCityReturnCommand>());
            Assert.That(
                queued[1],
                Is.TypeOf<FinalizeExpeditionTransactionCommand>());
            var credit = (CreditCityReturnCommand)queued[0];
            var finalize = (FinalizeExpeditionTransactionCommand)queued[1];
            Assert.That(credit.TransactionId, Is.EqualTo(TransactionId));
            Assert.That(credit.Fingerprint, Is.EqualTo(TransactionFingerprint));
            Assert.That(finalize.TransactionId, Is.EqualTo(TransactionId));
            Assert.That(
                finalize.Fingerprint,
                Is.EqualTo(TransactionFingerprint));
            Assert.That(finalize.Sequence, Is.EqualTo(credit.Sequence + 1));
            controller.CompleteReturn();
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(2));
            CollectionAssert.AreEqual(
                validBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            InstallControllerState(controller, returned);
            ActivateModeForGuardTest(
                controller.ModeCoordinator!,
                LastBearingPresentationMode.CityOverview);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            byte[] wrongModeBytes = LastBearingCanonicalCodec.Encode(
                controller.State!);
            controller.CompleteReturn();
            Assert.That(PendingCommands(controller), Is.Empty);
            CollectionAssert.AreEqual(
                wrongModeBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
        }
        [Test]
        public void RepairInstallsOnlyAtSelectedPumpHallAndOutcomesAreExact()
        {
            LastBearingGameController controller = CreateController();
            LastBearingState ceramicFinalized = FinalizeReturn(
                CreateReturnedState(EncounterChoice.TakeBearing));
            InstallControllerState(controller, ceramicFinalized);
            LastBearingWorldBuilder world = controller.World!;
            world.SelectOneGoodBatchCutaway();
            Assert.That(
                controller.ModeCoordinator!.TryShowCityMode(
                    LastBearingPresentationMode.BuildingCutaway,
                    controller.ReadModel),
                Is.True);
            Assert.That(world.IsPumpHallCutawaySelected, Is.False);
            byte[] wrongCutawayBytes = LastBearingCanonicalCodec.Encode(
                controller.State!);
            controller.RepairTurbine();
            Assert.That(PendingCommands(controller), Is.Empty);
            CollectionAssert.AreEqual(
                wrongCutawayBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            world.SelectPumpHallCutaway();
            Assert.That(world.IsPumpHallCutawaySelected, Is.True);
            Assert.That(controller.IsPumpHallRepairAvailable, Is.True);
            controller.RepairTurbine();
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<InstallTurbineRepairCommand>());
            controller.RepairTurbine();
            Assert.That(
                PendingCommands(controller),
                Has.Count.EqualTo(1),
                "a duplicate same-frame request must not append a command");
            LastBearingState ceramicRepaired = ApplyOne(
                ceramicFinalized,
                sequence => new InstallTurbineRepairCommand(sequence));
            AssertRepairPresentation(
                controller,
                ceramicRepaired,
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Turbine,
                TurbineCondition.BearingRepaired,
                ceramicVisible: true,
                sleeveVisible: false,
                "CERAMIC BEARING\nSEATED AT TURBINE");
            LastBearingState sleeveRepaired = ApplyOne(
                FinalizeReturn(CreateReturnedState(EncounterChoice.Cooperate)),
                sequence => new InstallTurbineRepairCommand(sequence));
            AssertRepairPresentation(
                controller,
                sleeveRepaired,
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Consumed,
                TurbineCondition.SleeveRepaired,
                ceramicVisible: false,
                sleeveVisible: true,
                "FIELD SLEEVE\nCONSUMED IN REPAIR");
        }
        private LastBearingGameController CreateController()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            return controller;
        }
        private static void AssertAnchor(Transform? anchor, string expectedName)
        {
            Assert.That(anchor, Is.Not.Null, expectedName);
            Assert.That(anchor!.name, Is.EqualTo(expectedName));
        }
        private static void AssertRepairPresentation(
            LastBearingGameController controller,
            LastBearingState repaired,
            RepairCargoKind kind,
            RepairCargoCustody custody,
            TurbineCondition condition,
            bool ceramicVisible,
            bool sleeveVisible,
            string label)
        {
            InstallControllerState(controller, repaired);
            LastBearingPumpHallCutawayView pump =
                controller.World!.PumpHallCutawayView!;
            LastBearingDepotCargoLoadingView cargo =
                controller.World.DepotCargoLoadingView!;
            Assert.That(controller.ReadModel!.RepairCargoKind, Is.EqualTo(kind));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(custody));
            Assert.That(
                controller.ReadModel.TurbineCondition,
                Is.EqualTo(condition));
            Assert.That(
                pump.IsCeramicBearingAtTurbineVisible,
                Is.EqualTo(ceramicVisible));
            Assert.That(
                pump.IsFieldSleeveRepairVisible,
                Is.EqualTo(sleeveVisible));
            Assert.That(pump.IsTurbineRepairTargetVisible, Is.False);
            Assert.That(pump.VisibleTurbineCondition, Is.EqualTo(condition));
            Assert.That(pump.RepairOutcomeLabel, Is.EqualTo(label));
            Assert.That(cargo.IsCanonicalVehicleCargoVisible, Is.False);
            Assert.That(cargo.IsRoadVehicleCargoVisible, Is.False);
        }
        private static void InstallControllerState(
            LastBearingGameController controller,
            LastBearingState state)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo? stateField = typeof(LastBearingGameController).GetField(
                "_state",
                flags);
            FieldInfo? readModelField =
                typeof(LastBearingGameController).GetField("_readModel", flags);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(readModelField, Is.Not.Null);
            controller.ModeCoordinator!.ClearSession();
            stateField!.SetValue(controller, state);
            readModelField!.SetValue(
                controller,
                LastBearingReadModel.FromState(state));
            PendingCommands(controller).Clear();
            InvokeController(controller, "ApplyPresentation");
        }
        private static List<LastBearingCommand> PendingCommands(
            LastBearingGameController controller)
        {
            FieldInfo? field = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var commands = field!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(commands, Is.Not.Null);
            return commands!;
        }
        private static void InvokeController(
            LastBearingGameController controller,
            string methodName)
        {
            MethodInfo? method = typeof(LastBearingGameController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method!.Invoke(controller, null);
        }
        private static void ActivateModeForGuardTest(
            LastBearingModeCoordinator coordinator,
            LastBearingPresentationMode mode)
        {
            MethodInfo? method = typeof(LastBearingModeCoordinator).GetMethod(
                "Activate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(
                coordinator,
                new object[] { mode, false, false, false });
        }
        private static LastBearingState CreateReturnedState(
            EncounterChoice encounter)
        {
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                7111);
            state = ApplyMany(
                state,
                sequence => new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId),
                sequence => new ActivateSliceInfrastructureCommand(sequence));
            state = ApplyMany(
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.WorkshopPush,
                    VehicleModule.WinchAssembly),
                sequence => new InstallVehicleModuleCommand(
                    sequence,
                    VehicleModule.WinchAssembly));
            state = AdvanceUntil(
                state,
                model => model.PreparationPhase == PreparationPhase.Ready);
            state = ApplyMany(
                state,
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new DebitCityManifestCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            state = AdvanceUntil(
                state,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                operateWreckLine: true);
            state = ApplyOne(
                state,
                sequence => new OperateDepotRecoveryPointCommand(sequence));
            state = ApplyOne(
                state,
                sequence => new ResolveDepotCommand(sequence, encounter));
            state = ApplyOne(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            state = ApplyOne(
                state,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            state = AdvanceUntil(
                state,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true);
            return state;
        }
        private static LastBearingState FinalizeReturn(LastBearingState state)
        {
            return ApplyMany(
                state,
                sequence => new CreditCityReturnCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new FinalizeExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
        }
        private static LastBearingState AdvanceUntil(
            LastBearingState state,
            Func<LastBearingReadModel, bool> completed,
            bool drive = false,
            bool operateWreckLine = false)
        {
            for (var guard = 0; guard < 2000; guard++)
            {
                LastBearingReadModel model = LastBearingReadModel.FromState(state);
                if (completed(model))
                {
                    return state;
                }
                if (operateWreckLine && model.IsWreckLineModulePointAvailable)
                {
                    state = ApplyOne(state, sequence =>
                        new OperateWreckLineModuleCommand(
                            sequence,
                            model.RouteActionKind));
                }
                else if (drive)
                {
                    state = ApplyOne(state, sequence =>
                        new DriveVehicleCommand(sequence, 1000, 0));
                }
                else
                {
                    state = new LastBearingKernel().Step(
                        state,
                        Array.Empty<LastBearingCommand>()).State;
                }
            }
            Assert.Fail("VGR-11 fixture did not reach its requested state.");
            return state;
        }
        private static LastBearingState ApplyOne(
            LastBearingState state,
            Func<long, LastBearingCommand> factory)
        {
            return ApplyMany(state, factory);
        }
        private static LastBearingState ApplyMany(
            LastBearingState state,
            params Func<long, LastBearingCommand>[] factories)
        {
            var commands = new List<LastBearingCommand>(factories.Length);
            for (var index = 0; index < factories.Length; index++)
            {
                commands.Add(factories[index](checked(
                    state.NextCommandSequence + index)));
            }
            return new LastBearingKernel().Step(state, commands).State;
        }
    }
}
