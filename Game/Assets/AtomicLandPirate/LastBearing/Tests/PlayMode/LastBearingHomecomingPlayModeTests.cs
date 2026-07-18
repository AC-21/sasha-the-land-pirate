#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    /// <summary>
    /// Bounded end-to-end acceptance for VGR-11. These tests drive the public
    /// Unity adapter and inspect its queued core commands; they do not add a
    /// second gameplay or save authority.
    /// </summary>
    public sealed class LastBearingHomecomingPlayModeTests : InputTestFixture
    {
        private const string SceneName = "LastBearing";
        private const string TransactionId =
            "transaction:last-bearing:vgr11-nondefault:0001";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:vgr11-nondefault:0001";

        private readonly List<string> _temporarySaveRoots =
            new List<string>();

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene(
                    "LastBearing_Homecoming_TestCleanup");
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
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArrivalCheckInAndRepairAutosaveAndReloadExactly()
        {
            yield return LoadScene();
            LastBearingGameController controller = RequireController();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            LastBearingState beforeArrival = CreateOneDriveBeforeReturn(
                ColonyComposition.HumanOnly,
                EncounterChoice.Cooperate);
            InstallControllerState(controller, beforeArrival);

            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returning));
            Assert.That(Directory.Exists(profileDirectory), Is.False);

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.wKey);
            InvokeSimulationTick(controller);
            Release(keyboard.wKey);
            yield return null;

            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returned));
            Assert.That(
                controller.ReadModel.TransactionPhase,
                Is.EqualTo(TransactionPhase.ReturnPending));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityReturn));
            Assert.That(controller.IsReturnCheckInAvailable, Is.True);
            Assert.That(
                controller.World!.ReturnServiceView!.IsCheckInMarkerVisible,
                Is.True);
            Assert.That(
                controller.World.ReturnServiceView.IsHumanWorkerVisible,
                Is.True);
            Assert.That(
                controller.World.ReturnServiceView.IsRobotWorkerVisible,
                Is.False);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                controller.SaveStatus);
            AssertGenerationCount(profileDirectory, 1);

            yield return AssertReturnCameraSettlesDeterministically(controller);

            string arrivalHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(arrivalHash));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityReturn));
            Assert.That(controller.IsReturnCheckInAvailable, Is.True);

            controller.ModeCoordinator.ClearSession();
            controller.CompleteReturn();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(controller.CanonicalHash, Is.EqualTo(arrivalHash));

            InvokeApplyPresentation(controller);
            Assert.That(controller.IsReturnCheckInAvailable, Is.True);
            Press(keyboard.eKey);
            InvokeGlobalShortcuts(controller);
            yield return null;
            Release(keyboard.eKey);
            yield return null;

            IReadOnlyList<LastBearingCommand> queued =
                PendingCommands(controller);
            AssertExactCheckInPair(queued);
            controller.CompleteReturn();
            AssertExactCheckInPair(PendingCommands(controller));

            long sequenceBeforeCheckIn =
                controller.State!.NextCommandSequence;
            InvokeSimulationTick(controller);

            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(
                controller.State!.NextCommandSequence,
                Is.EqualTo(sequenceBeforeCheckIn + 2));
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtHome));
            Assert.That(
                controller.ReadModel.TransactionPhase,
                Is.EqualTo(TransactionPhase.Finalized));
            Assert.That(
                controller.ReadModel.RepairCargoKind,
                Is.EqualTo(RepairCargoKind.FieldSleeve));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.BuildingCutaway));
            Assert.That(controller.World!.IsPumpHallCutawaySelected, Is.True);
            Assert.That(controller.IsPumpHallRepairAvailable, Is.True);
            AssertGenerationCount(profileDirectory, 2);

            string finalizedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(finalizedHash));
            Assert.That(controller.IsPumpHallRepairAvailable, Is.True);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.BuildingCutaway));

            controller.RepairTurbine();
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<InstallTurbineRepairCommand>());
            controller.RepairTurbine();
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.TurbineCondition,
                Is.EqualTo(TurbineCondition.SleeveRepaired));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Consumed));
            LastBearingPumpHallCutawayView pumpHall =
                controller.World!.PumpHallCutawayView!;
            Assert.That(pumpHall.IsFieldSleeveRepairVisible, Is.True);
            Assert.That(pumpHall.IsCeramicBearingAtTurbineVisible, Is.False);
            Assert.That(
                pumpHall.RepairOutcomeLabel,
                Does.Contain("FIELD SLEEVE"));
            AssertGenerationCount(profileDirectory, 3);

            string repairedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(repairedHash));
            Assert.That(
                controller.ReadModel!.TurbineCondition,
                Is.EqualTo(TurbineCondition.SleeveRepaired));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Consumed));
            Assert.That(pumpHall.IsFieldSleeveRepairVisible, Is.True);
            AssertGenerationCount(profileDirectory, 3);
        }

        [UnityTest]
        public IEnumerator GamepadSouthUsesTheSameCeramicCheckInRoute()
        {
            yield return LoadScene();
            LastBearingGameController controller = RequireController();
            controller.enabled = false;
            _ = InstallTemporarySaveAdapter(controller);
            LastBearingState returned = CreateReturnedState(
                ColonyComposition.HumanOnly,
                EncounterChoice.TakeBearing);
            InstallControllerState(controller, returned);
            string returnedHash = controller.CanonicalHash;
            Assert.That(
                controller.World!.ReturnServiceView!.IsHumanWorkerVisible,
                Is.True);
            Assert.That(
                controller.World.ReturnServiceView.IsRobotWorkerVisible,
                Is.False);

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.buttonSouth);
            InvokeGlobalShortcuts(controller);
            Release(gamepad.buttonSouth);
            yield return null;

            AssertExactCheckInPair(PendingCommands(controller));
            controller.CompleteReturn();
            AssertExactCheckInPair(PendingCommands(controller));
            Assert.That(controller.CanonicalHash, Is.EqualTo(returnedHash));
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.TransactionPhase,
                Is.EqualTo(TransactionPhase.Finalized));
            Assert.That(
                controller.ReadModel.RepairCargoKind,
                Is.EqualTo(RepairCargoKind.CeramicBearing));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(controller.IsPumpHallRepairAvailable, Is.True);

            string finalizedHash = controller.CanonicalHash;
            controller.ShowCityOverview();
            Assert.That(controller.IsPumpHallRepairAvailable, Is.False);
            controller.RepairTurbine();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(controller.CanonicalHash, Is.EqualTo(finalizedHash));

            controller.OpenPumpHallRepair();
            Assert.That(controller.IsPumpHallRepairAvailable, Is.True);
            controller.RepairTurbine();
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel.TurbineCondition,
                Is.EqualTo(TurbineCondition.BearingRepaired));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Turbine));
            LastBearingPumpHallCutawayView pumpHall =
                controller.World!.PumpHallCutawayView!;
            Assert.That(pumpHall.IsCeramicBearingAtTurbineVisible, Is.True);
            Assert.That(pumpHall.IsFieldSleeveRepairVisible, Is.False);
            Assert.That(
                pumpHall.RepairOutcomeLabel,
                Does.Contain("CERAMIC BEARING"));
        }

        private static IEnumerator LoadScene()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
        }

        private static LastBearingGameController RequireController()
        {
            LastBearingGameController[] controllers =
                UnityEngine.Object.FindObjectsByType<LastBearingGameController>(
                    FindObjectsInactive.Include);
            Assert.That(controllers, Has.Length.EqualTo(1));
            return controllers[0];
        }

        private static IEnumerator AssertReturnCameraSettlesDeterministically(
            LastBearingGameController controller)
        {
            LastBearingWorldBuilder world = controller.World!;
            LastBearingReturnServiceView returnService =
                world.ReturnServiceView!;
            Assert.That(
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(world.CameraRig!.IsInspectionMode, Is.True);
            Assert.That(
                world.CameraRig.InspectionCameraAnchor,
                Is.SameAs(returnService.CameraAnchor));
            Assert.That(
                world.CameraRig.InspectionFocusAnchor,
                Is.SameAs(returnService.FocusAnchor));

            Camera camera = world.MainCamera!;
            float elapsed = 0f;
            var frames = 0;
            while (frames < 240 && elapsed < 2f)
            {
                Vector3 focus =
                    (returnService.FocusAnchor!.position -
                     camera.transform.position).normalized;
                bool atAnchor = Vector3.Distance(
                    camera.transform.position,
                    returnService.CameraAnchor!.position) < 0.1f;
                bool onFocus = Vector3.Dot(
                    camera.transform.forward,
                    focus) > 0.998f;
                if (atAnchor && onFocus)
                {
                    break;
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
                frames++;
            }

            Vector3 finalPosition = camera.transform.position;
            Assert.That(
                Vector3.Distance(
                    finalPosition,
                    returnService.CameraAnchor!.position),
                Is.LessThan(0.1f));
            Vector3 focusDirection =
                (returnService.FocusAnchor!.position - finalPosition)
                .normalized;
            Assert.That(
                Vector3.Dot(
                    camera.transform.forward,
                    focusDirection),
                Is.GreaterThan(0.998f));
        }

        private string InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "wp0002-homecoming-tests-" + Guid.NewGuid().ToString("N"));
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
            var adapter = constructor!.Invoke(new object[] { store }) as
                LastBearingSaveAdapter;
            Assert.That(adapter, Is.Not.Null);
            adapterField!.SetValue(controller, adapter);
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
                typeof(LastBearingGameController).GetField(
                    "_readModel",
                    flags);
            FieldInfo? pendingField =
                typeof(LastBearingGameController).GetField(
                    "_pendingCommands",
                    flags);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(readModelField, Is.Not.Null);
            Assert.That(pendingField, Is.Not.Null);

            controller.ModeCoordinator!.ClearSession();
            stateField!.SetValue(controller, state);
            readModelField!.SetValue(
                controller,
                LastBearingReadModel.FromState(state));
            var pending = pendingField!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            pending!.Clear();
            InvokeApplyPresentation(controller);
        }

        private static IReadOnlyList<LastBearingCommand> PendingCommands(
            LastBearingGameController controller)
        {
            FieldInfo? pendingField =
                typeof(LastBearingGameController).GetField(
                    "_pendingCommands",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pendingField, Is.Not.Null);
            var pending = pendingField!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            return pending!;
        }

        private static void AssertExactCheckInPair(
            IReadOnlyList<LastBearingCommand> commands)
        {
            Assert.That(commands, Has.Count.EqualTo(2));
            Assert.That(commands[0], Is.TypeOf<CreditCityReturnCommand>());
            Assert.That(
                commands[1],
                Is.TypeOf<FinalizeExpeditionTransactionCommand>());
            var credit = (CreditCityReturnCommand)commands[0];
            var finalize = (FinalizeExpeditionTransactionCommand)commands[1];
            Assert.That(credit.TransactionId, Is.EqualTo(TransactionId));
            Assert.That(
                credit.Fingerprint,
                Is.EqualTo(TransactionFingerprint));
            Assert.That(finalize.TransactionId, Is.EqualTo(TransactionId));
            Assert.That(
                finalize.Fingerprint,
                Is.EqualTo(TransactionFingerprint));
            Assert.That(
                commands[1].Sequence,
                Is.EqualTo(commands[0].Sequence + 1));
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            InvokePrivate(controller, "SimulateOneTick");
        }

        private static void InvokeGlobalShortcuts(
            LastBearingGameController controller)
        {
            InvokePrivate(controller, "HandleGlobalShortcuts");
        }

        private static void InvokeApplyPresentation(
            LastBearingGameController controller)
        {
            InvokePrivate(controller, "ApplyPresentation");
        }

        private static void InvokePrivate(
            LastBearingGameController controller,
            string methodName)
        {
            MethodInfo? method = typeof(LastBearingGameController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method!.Invoke(controller, null);
        }

        private static void AssertGenerationCount(
            string profileDirectory,
            int expected)
        {
            Assert.That(Directory.Exists(profileDirectory), Is.True);
            Assert.That(
                Directory.GetFiles(profileDirectory, "gen-*.lbg").Length,
                Is.EqualTo(expected));
        }

        private static LastBearingState CreateOneDriveBeforeReturn(
            ColonyComposition composition,
            EncounterChoice encounter)
        {
            LastBearingState state = CreateReturningState(
                composition,
                encounter);
            var kernel = new LastBearingKernel();
            for (var guard = 0; guard < 1000; guard++)
            {
                LastBearingState next = Apply(kernel, state, sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                if (next.ExpeditionPhase == ExpeditionPhase.Returned)
                {
                    Assert.That(
                        state.ExpeditionPhase,
                        Is.EqualTo(ExpeditionPhase.Returning));
                    return state;
                }

                state = next;
            }

            throw new AssertionException(
                "return arrival was not reached within 1000 drives");
        }

        private static LastBearingState CreateReturnedState(
            ColonyComposition composition,
            EncounterChoice encounter)
        {
            Assert.That(
                composition,
                Is.EqualTo(ColonyComposition.HumanOnly),
                "the shared source-state helper authors the human-only case");
            LastBearingState returning = CreateReturningState(
                composition,
                encounter);
            return InvokeExistingStateHelper(
                "DriveUntilPhase",
                returning,
                ExpeditionPhase.Returned);
        }

        private static LastBearingState CreateReturningState(
            ColonyComposition composition,
            EncounterChoice encounter)
        {
            Assert.That(
                composition,
                Is.EqualTo(ColonyComposition.HumanOnly),
                "the shared source-state helper authors the human-only case");
            var kernel = new LastBearingKernel();
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                composition,
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
                new InstallVehicleModuleCommand(
                    sequence,
                    VehicleModule.WinchAssembly));
            for (var guard = 0;
                 state.PreparationPhase != PreparationPhase.Ready && guard < 1000;
                 guard++)
            {
                state = kernel.Step(
                    state,
                    Array.Empty<LastBearingCommand>()).State;
            }

            Assert.That(
                state.PreparationPhase,
                Is.EqualTo(PreparationPhase.Ready));
            state = Apply(kernel, state, sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            state = Apply(kernel, state, sequence =>
                new DebitCityManifestCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            state = InvokeExistingStateHelper(
                "DriveUntilDepotRecoveryAvailable",
                state);
            state = Apply(kernel, state, sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            state = Apply(kernel, state, sequence =>
                new ResolveDepotCommand(sequence, encounter));
            state = Apply(kernel, state, sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            state = Apply(kernel, state, sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            Assert.That(
                state.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returning));
            return state;
        }

        private static LastBearingState InvokeExistingStateHelper(
            string methodName,
            params object[] arguments)
        {
            MethodInfo? method = typeof(LastBearingPlayModeTests).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            object? result = method!.Invoke(null, arguments);
            Assert.That(result, Is.TypeOf<LastBearingState>(), methodName);
            return (LastBearingState)result!;
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
    }
}
