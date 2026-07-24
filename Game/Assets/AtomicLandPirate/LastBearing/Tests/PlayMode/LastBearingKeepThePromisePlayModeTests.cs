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

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingKeepThePromisePlayModeTests :
        InputTestFixture
    {
        private const string TransactionId =
            "transaction:last-bearing:vgr18:keep-the-promise";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:vgr18:keep-the-promise";

        private readonly List<string> _temporarySaveRoots =
            new List<string>();
        private GameObject? _root;

        [UnityTearDown]
        public IEnumerator TearDownRuntime()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            foreach (LastBearingGameController controller in
                     UnityEngine.Object.FindObjectsByType<
                         LastBearingGameController>(
                         FindObjectsInactive.Include))
            {
                UnityEngine.Object.DestroyImmediate(
                    controller.gameObject);
            }

            foreach (string root in _temporarySaveRoots)
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            _temporarySaveRoots.Clear();
            _root = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator RouteRequiresFreshInputThenAutosavesExactService()
        {
            LastBearingGameController controller = CreateController();
            LastBearingState due = CreateDueMaintenanceState();
            Assert.That(
                LastBearingBalanceV1.SleeveMaintenancePartsUnits,
                Is.EqualTo(2));
            Assert.That(
                LastBearingBalanceV1
                    .SleeveMaintenanceIntervalSettlementTicks,
                Is.EqualTo(600));
            InstallControllerState(controller, due);
            controller.ShowCityOverview();
            var interactor =
                controller.World!.PumpHallMaintenanceInteractor!;
            Assert.That(
                interactor.HasDedicatedInteractionTarget,
                Is.True);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            string profileDirectory =
                RequireProfileDirectory(controller);

            LastBearingFieldDeskProjection projection =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                projection.PrimaryAction.Intent,
                Is.EqualTo(
                    LastBearingFieldDeskIntent.ServiceFieldSleeve));
            Assert.That(
                projection.PrimaryAction.Label,
                Is.EqualTo("OPEN PUMP HALL · KEEP THE PROMISE"));
            Assert.That(projection.PrimaryAction.IsEnabled, Is.True);

            byte[] routeBytes = LastBearingCanonicalCodec.Encode(due);
            string routeHash = controller.CanonicalHash;
            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.OpenFieldSleeveService();
                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(
                        LastBearingPresentationMode.BuildingCutaway));
                Assert.That(
                    controller.World.IsPumpHallCutawaySelected,
                    Is.True);
                Assert.That(
                    interactor.IsControlFocused,
                    Is.True);
                Assert.That(interactor.IsControlVisible, Is.True);
                Assert.That(
                    interactor.IsFirstServicePartVisible,
                    Is.True);
                Assert.That(
                    interactor.IsSecondServicePartVisible,
                    Is.True);
                Assert.That(interactor.IsFocusRailVisible, Is.True);
                Assert.That(
                    PendingCommands(controller),
                    Is.Empty);
                CollectionAssert.AreEqual(
                    routeBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                Assert.That(
                    controller.CanonicalHash,
                    Is.EqualTo(routeHash));

                controller.ShowCityOverview();
                yield return null;
                Assert.That(
                    interactor.IsControlFocused,
                    Is.False);
                Assert.That(interactor.IsControlVisible, Is.False);
                Assert.That(
                    PendingCommands(controller),
                    Is.Empty);
            }

            Press(keyboard.eKey);
            float yawBeforeHeldRoute =
                controller.World!.CameraRig!.CityYaw;
            controller.OpenFieldSleeveService();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(
                controller.World.CameraRig.CityYaw,
                Is.EqualTo(yawBeforeHeldRoute).Within(0.0001f),
                "the focused service lever must own E before city yaw");
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            CollectionAssert.AreEqual(
                routeBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            Release(keyboard.eKey);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);

            long sequenceBefore = due.NextCommandSequence;
            long partsBefore = due.PartsUnits;
            long settlementTickBefore = due.SettlementTick;
            int generationsBefore =
                GenerationCount(profileDirectory);
            Press(keyboard.eKey);
            InvokeInteractorUpdate(interactor);
            Release(keyboard.eKey);

            LastBearingCommand[] queued =
                PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(1));
            Assert.That(
                queued[0],
                Is.TypeOf<ServiceFieldSleeveCommand>());
            Assert.That(queued[0].Sequence, Is.EqualTo(sequenceBefore));
            controller.ServiceFieldSleeve();
            Assert.That(
                PendingCommands(controller),
                Has.Count.EqualTo(1),
                "duplicate service must not append another command");
            CollectionAssert.AreEqual(
                routeBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.PartsUnits,
                Is.EqualTo(partsBefore - 2));
            Assert.That(controller.ReadModel.MaintenanceDue, Is.False);
            Assert.That(
                controller.State!.NextMaintenanceDueSettlementTick,
                Is.EqualTo(settlementTickBefore + 600));
            Assert.That(
                controller.ReadModel.MaintenanceObligationActive,
                Is.True);
            Assert.That(
                controller.ReadModel.MaintenanceRecipe,
                Is.EqualTo(MaintenanceRecipe.FieldSleeveService));
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(interactor.IsServiceWitnessVisible, Is.True);
            Assert.That(
                interactor.IsFirstServicePartVisible,
                Is.False);
            Assert.That(
                interactor.IsSecondServicePartVisible,
                Is.False);
            Assert.That(interactor.IsControlFocused, Is.False);
            Assert.That(
                interactor.Feedback,
                Does.Contain(
                    controller.State!
                        .NextMaintenanceDueSettlementTick.ToString()));
            Assert.That(
                GenerationCount(profileDirectory),
                Is.EqualTo(generationsBefore + 1));

            string acceptedHash = controller.CanonicalHash;
            long acceptedParts = controller.ReadModel.PartsUnits;
            long acceptedDue =
                controller.State!.NextMaintenanceDueSettlementTick;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(acceptedHash));
            Assert.That(
                controller.ReadModel!.PartsUnits,
                Is.EqualTo(acceptedParts));
            Assert.That(controller.ReadModel.MaintenanceDue, Is.False);
            Assert.That(
                controller.State!.NextMaintenanceDueSettlementTick,
                Is.EqualTo(acceptedDue));
            Assert.That(
                controller.ReadModel.MaintenanceObligationActive,
                Is.True);

            controller.World!.SelectPumpHallCutaway();
            controller.OpenBuildingCutaway();
            InvokeController(controller, "ApplyPresentation");
            Assert.That(interactor.IsServiceWitnessVisible, Is.True);
            Assert.That(interactor.IsFirstServicePartVisible, Is.False);
            Assert.That(interactor.IsSecondServicePartVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator GamepadAndPointerQueueOnlyTheExistingCommand()
        {
            LastBearingGameController controller = CreateController();
            LastBearingState due = CreateDueMaintenanceState();
            var interactor =
                controller.World!.PumpHallMaintenanceInteractor!;
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

            InstallControllerState(controller, due);
            controller.ShowCityOverview();
            controller.OpenFieldSleeveService();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);
            Press(gamepad.buttonSouth);
            InvokeInteractorUpdate(interactor);
            Release(gamepad.buttonSouth);
            AssertExactServiceCommand(controller, due.NextCommandSequence);

            InstallControllerState(controller, due);
            controller.ShowCityOverview();
            controller.OpenFieldSleeveService();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);
            Physics.SyncTransforms();
            Vector3 screen =
                controller.World.MainCamera!.WorldToScreenPoint(
                    interactor.ServiceControlWorldPosition);
            Assert.That(screen.z, Is.GreaterThan(0f));
            Assert.That(
                interactor.TryActivateAtScreenPosition(
                    new Vector2(screen.x, screen.y)),
                Is.True);
            AssertExactServiceCommand(controller, due.NextCommandSequence);
        }

        [UnityTest]
        public IEnumerator WrongModeStalePendingAndTitleGuardsFailClosed()
        {
            LastBearingGameController controller = CreateController();
            LastBearingState due = CreateDueMaintenanceState();
            InstallControllerState(controller, due);
            controller.ShowCityOverview();
            var interactor =
                controller.World!.PumpHallMaintenanceInteractor!;
            string dueHash = controller.CanonicalHash;
            byte[] dueBytes = LastBearingCanonicalCodec.Encode(due);

            controller.ServiceFieldSleeve();
            Assert.That(PendingCommands(controller), Is.Empty);
            controller.OpenGarageBay();
            controller.OpenFieldSleeveService();
            Assert.That(interactor.IsControlFocused, Is.False);
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertUnchanged(controller, dueBytes, dueHash);

            InstallControllerState(controller, due);
            controller.ShowCityOverview();
            controller.OpenFieldSleeveService();
            yield return null;
            InvokeInteractorUpdate(interactor);
            AddUnrelatedPendingCommand(controller);
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<SetPauseCommand>());
            AssertUnchanged(controller, dueBytes, dueHash);

            InstallControllerState(controller, due);
            controller.ShowCityOverview();
            controller.OpenFieldSleeveService();
            yield return null;
            InvokeInteractorUpdate(interactor);
            ReplaceControllerStateWithoutPresentation(controller, due);
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertUnchanged(controller, dueBytes, dueHash);

            InstallControllerState(controller, due);
            controller.ReturnToTitle();
            controller.OpenFieldSleeveService();
            controller.ServiceFieldSleeve();
            Assert.That(interactor.IsControlFocused, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
        }

        private LastBearingGameController CreateController()
        {
            _root = new GameObject(
                LastBearingGameController.RuntimeRootName);
            var controller =
                _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.enabled = false;
            InstallTemporarySaveAdapter(controller);
            return controller;
        }

        private static LastBearingState CreateDueMaintenanceState()
        {
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                worldSeed: 4818);
            state = ApplyMany(
                state,
                sequence => new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId),
                sequence =>
                    new ActivateSliceInfrastructureCommand(sequence));
            state = ApplyMany(
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.CivicBuffer,
                    VehicleModule.WinchAssembly),
                sequence => new InstallVehicleModuleCommand(
                    sequence,
                    VehicleModule.WinchAssembly));
            state = AdvanceUntil(
                state,
                model =>
                    model.PreparationPhase ==
                    PreparationPhase.Ready);
            state = ApplyMany(
                state,
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new DebitCityManifestCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new DepartExpeditionCommand(sequence));
            state = AdvanceUntil(
                state,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                operateWreckLine: true);
            state = ApplyOne(
                state,
                sequence =>
                    new OperateDepotRecoveryPointCommand(sequence));
            state = ApplyOne(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.Cooperate));
            state = ApplyOne(
                state,
                sequence =>
                    new LoadDepotRepairCargoCommand(sequence));
            state = ApplyOne(
                state,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            state = AdvanceUntil(
                state,
                model =>
                    model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true);
            state = ApplyMany(
                state,
                sequence => new CreditCityReturnCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence =>
                    new FinalizeExpeditionTransactionCommand(
                        sequence,
                        TransactionId,
                        TransactionFingerprint));
            state = ApplyOne(
                state,
                sequence =>
                    new InstallTurbineRepairCommand(sequence));
            state = AdvanceUntil(
                state,
                model => model.MaintenanceDue);

            LastBearingReadModel due =
                LastBearingReadModel.FromState(state);
            Assert.That(
                due.TurbineCondition,
                Is.EqualTo(TurbineCondition.SleeveRepaired));
            Assert.That(
                due.RepairCargoKind,
                Is.EqualTo(RepairCargoKind.FieldSleeve));
            Assert.That(
                due.MaintenanceRecipe,
                Is.EqualTo(MaintenanceRecipe.FieldSleeveService));
            Assert.That(due.MaintenanceObligationActive, Is.True);
            Assert.That(due.MaintenanceDue, Is.True);
            Assert.That(
                due.PartsUnits,
                Is.GreaterThanOrEqualTo(
                    LastBearingBalanceV1
                        .SleeveMaintenancePartsUnits));
            return state;
        }

        private static LastBearingState AdvanceUntil(
            LastBearingState state,
            Func<LastBearingReadModel, bool> completed,
            bool drive = false,
            bool operateWreckLine = false)
        {
            for (var guard = 0; guard < 3000; guard++)
            {
                LastBearingReadModel model =
                    LastBearingReadModel.FromState(state);
                if (completed(model))
                {
                    return state;
                }

                if (operateWreckLine &&
                    model.IsWreckLineModulePointAvailable)
                {
                    state = ApplyOne(
                        state,
                        sequence =>
                            new OperateWreckLineModuleCommand(
                                sequence,
                                model.RouteActionKind));
                }
                else if (operateWreckLine &&
                         model.IsWreckLineFrameRailRecoveryAvailable)
                {
                    state = ApplyOne(
                        state,
                        sequence =>
                            new RecoverWreckLineFrameRailsCommand(
                                sequence));
                }
                else if (drive)
                {
                    state = ApplyOne(
                        state,
                        sequence => new DriveVehicleCommand(
                            sequence,
                            1000,
                            0));
                }
                else
                {
                    state = new LastBearingKernel().Step(
                        state,
                        Array.Empty<LastBearingCommand>()).State;
                }
            }

            Assert.Fail(
                "VGR-18 fixture did not reach its requested state.");
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
            var commands =
                new List<LastBearingCommand>(factories.Length);
            for (var index = 0; index < factories.Length; index++)
            {
                commands.Add(
                    factories[index](
                        checked(
                            state.NextCommandSequence + index)));
            }

            return new LastBearingKernel().Step(state, commands).State;
        }

        private void InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "keep-the-promise-" +
                Guid.NewGuid().ToString("N"));
            string profileDirectory = Path.Combine(
                root,
                LastBearingProfileContract.ProfileName);
            Directory.CreateDirectory(root);
            _temporarySaveRoots.Add(root);

            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(
                    profileDirectory);
            ConstructorInfo? constructor =
                typeof(LastBearingSaveAdapter).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    new[] { typeof(LastBearingProfileStore) },
                    modifiers: null);
            FieldInfo? adapterField =
                typeof(LastBearingGameController).GetField(
                    "_saveAdapter",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(adapterField, Is.Not.Null);
            var adapter = constructor!.Invoke(
                new object[] { store }) as LastBearingSaveAdapter;
            Assert.That(adapter, Is.Not.Null);
            adapterField!.SetValue(controller, adapter);
        }

        private string RequireProfileDirectory(
            LastBearingGameController controller)
        {
            Assert.That(controller, Is.Not.Null);
            Assert.That(_temporarySaveRoots, Has.Count.EqualTo(1));
            return Path.Combine(
                _temporarySaveRoots[0],
                LastBearingProfileContract.ProfileName);
        }

        private static string GetConfinementSafeTemporaryRoot()
        {
            string root = Path.GetTempPath();
            bool isMacOs =
                Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.OSXPlayer;
            return isMacOs &&
                   root.StartsWith("/var/", StringComparison.Ordinal)
                ? "/private" + root
                : root;
        }

        private static void InstallControllerState(
            LastBearingGameController controller,
            LastBearingState state)
        {
            controller.ModeCoordinator!.ClearSession();
            ReplaceControllerStateWithoutPresentation(controller, state);
            InvokeController(controller, "ApplyPresentation");
        }

        private static void ReplaceControllerStateWithoutPresentation(
            LastBearingGameController controller,
            LastBearingState state)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo? stateField =
                typeof(LastBearingGameController).GetField(
                    "_state",
                    flags);
            FieldInfo? readModelField =
                typeof(LastBearingGameController).GetField(
                    "_readModel",
                    flags);
            MethodInfo? resetSnapshots =
                typeof(LastBearingGameController).GetMethod(
                    "ResetPublicSnapshotsToRuntime",
                    flags);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(readModelField, Is.Not.Null);
            Assert.That(resetSnapshots, Is.Not.Null);

            stateField!.SetValue(controller, state);
            readModelField!.SetValue(
                controller,
                LastBearingReadModel.FromState(state));
            PendingCommands(controller).Clear();
            resetSnapshots!.Invoke(controller, null);
        }

        private static List<LastBearingCommand> PendingCommands(
            LastBearingGameController controller)
        {
            FieldInfo? pending =
                typeof(LastBearingGameController).GetField(
                    "_pendingCommands",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pending, Is.Not.Null);
            var commands = pending!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(commands, Is.Not.Null);
            return commands!;
        }

        private static void AddUnrelatedPendingCommand(
            LastBearingGameController controller)
        {
            PendingCommands(controller).Add(
                new SetPauseCommand(
                    controller.State!.NextCommandSequence,
                    isPaused: true));
        }

        private static void InvokeInteractorUpdate(
            LastBearingPumpHallMaintenanceInteractor interactor)
        {
            MethodInfo? update =
                typeof(LastBearingPumpHallMaintenanceInteractor)
                    .GetMethod(
                        "Update",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update!.Invoke(interactor, null);
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            InvokeController(controller, "SimulateOneTick");
        }

        private static void InvokeController(
            LastBearingGameController controller,
            string methodName)
        {
            MethodInfo? method =
                typeof(LastBearingGameController).GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method!.Invoke(controller, null);
        }

        private static void AssertExactServiceCommand(
            LastBearingGameController controller,
            long expectedSequence)
        {
            LastBearingCommand[] queued =
                PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(1));
            Assert.That(
                queued[0],
                Is.TypeOf<ServiceFieldSleeveCommand>());
            Assert.That(
                queued[0].Sequence,
                Is.EqualTo(expectedSequence));
        }

        private static void AssertUnchanged(
            LastBearingGameController controller,
            byte[] expectedBytes,
            string expectedHash)
        {
            CollectionAssert.AreEqual(
                expectedBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(
                controller.CanonicalHash,
                Is.EqualTo(expectedHash));
        }

        private static int GenerationCount(string profileDirectory)
        {
            return Directory.Exists(profileDirectory)
                ? Directory.GetDirectories(
                    profileDirectory,
                    "generation-*",
                    SearchOption.TopDirectoryOnly).Length
                : 0;
        }
    }
}
