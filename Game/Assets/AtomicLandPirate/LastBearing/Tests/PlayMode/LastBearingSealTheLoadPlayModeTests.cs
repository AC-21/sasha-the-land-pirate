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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingSealTheLoadPlayModeTests : InputTestFixture
    {
        private const string SceneName = "LastBearing";
        private const string TransactionId =
            "transaction:last-bearing:unity:0001";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:unity:0001";

        private readonly List<string> _temporarySaveRoots =
            new List<string>();
        private LastBearingGameController? _controller;

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup =
                    SceneManager.CreateScene("SealTheLoad_TestCleanup");
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
            _controller = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator RangeTankPointerAndGamepadLatchAutosaveExactLoad()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState loaded = CreateLoadedDepotState(
                VehicleModule.SealedRangeTank);
            InstallControllerState(controller, loaded);
            yield return null;

            LastBearingDepotReturnInteractor interactor =
                controller.World!.DepotReturnInteractor!;
            Assert.That(interactor.IsBuilt, Is.True);
            Assert.That(interactor.HasDedicatedInteractionTargets, Is.True);
            Assert.That(interactor.IsWaterValveVisible, Is.True);
            Assert.That(interactor.IsFuelValveVisible, Is.True);
            Assert.That(interactor.IsReturnLatchVisible, Is.False);
            Assert.That(interactor.FocusedControl, Is.EqualTo(
                DepotReturnControl.None));

            string beforeLiquid = controller.CanonicalHash;
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingDepotReturnInteractor.WaterValveName);
            AssertSinglePending<ChooseLiquidReturnCommand>(controller);
            Assert.That(
                ((ChooseLiquidReturnCommand)PendingCommands(controller)[0]).Kind,
                Is.EqualTo(LiquidCargoKind.Water));
            Assert.That(controller.CanonicalHash, Is.EqualTo(beforeLiquid));

            Assert.That(interactor.ActivateFuelValve(), Is.False);
            AssertSinglePending<ChooseLiquidReturnCommand>(controller);
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.LiquidCargoKind,
                Is.EqualTo(LiquidCargoKind.Water));
            Assert.That(
                controller.ReadModel.LiquidCargoCustody,
                Is.EqualTo(LiquidCargoCustody.Vehicle));
            Assert.That(interactor.IsWaterValveVisible, Is.False);
            Assert.That(interactor.IsFuelValveVisible, Is.False);
            Assert.That(interactor.IsReturnLatchVisible, Is.True);
            Assert.That(interactor.FocusedControl, Is.EqualTo(
                DepotReturnControl.ReturnLatch));
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                "liquid custody transfer must trigger the critical autosave");

            string liquidHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            Assert.That(interactor.FocusedControl, Is.EqualTo(
                DepotReturnControl.None));
            Assert.That(interactor.IsReturnLatchVisible, Is.False);
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(liquidHash));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.DepotEncounter));
            Assert.That(interactor.IsReturnLatchVisible, Is.True);

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.buttonSouth);
            yield return null;
            Release(gamepad.buttonSouth);
            yield return null;

            AssertSinglePending<FreezeReturnPayloadCommand>(controller);
            Assert.That(interactor.ActivateReturnLatch(), Is.False);
            AssertSinglePending<FreezeReturnPayloadCommand>(controller);
            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returning));
            Assert.That(
                controller.ReadModel.TransactionPhase,
                Is.EqualTo(TransactionPhase.ReturnPending));
            Assert.That(interactor.IsReturnLatchVisible, Is.False);
            Assert.That(interactor.FocusedControl, Is.EqualTo(
                DepotReturnControl.None));
            Assert.That(interactor.ActivateReturnLatch(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));

            string frozenHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(frozenHash));
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returning));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));
            Assert.That(interactor.FocusedControl, Is.EqualTo(
                DepotReturnControl.None));
            Assert.That(interactor.IsWaterValveVisible, Is.False);
            Assert.That(interactor.IsFuelValveVisible, Is.False);
            Assert.That(interactor.IsReturnLatchVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator KeyboardAndGamepadSelectTheSameSingleFuelCommand()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState loaded = CreateLoadedDepotState(
                VehicleModule.SealedRangeTank);
            InstallControllerState(controller, loaded);
            yield return null;

            LastBearingDepotReturnInteractor interactor =
                controller.World!.DepotReturnInteractor!;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Transform waterValve = RequireNamed(
                interactor.transform,
                LastBearingDepotReturnInteractor.WaterValveName);
            Vector3 waterScreen =
                controller.World.MainCamera!.WorldToScreenPoint(
                    waterValve.position);
            Set(
                mouse.position,
                new Vector2(waterScreen.x, waterScreen.y));
            yield return null;
            Assert.That(
                interactor.FocusedControl,
                Is.EqualTo(DepotReturnControl.WaterValve));

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.rightArrowKey);
            yield return null;
            Release(keyboard.rightArrowKey);
            yield return null;
            Assert.That(
                interactor.FocusedControl,
                Is.EqualTo(DepotReturnControl.FuelValve));
            yield return null;
            Assert.That(
                interactor.FocusedControl,
                Is.EqualTo(DepotReturnControl.FuelValve),
                "a stationary pointer must not steal keyboard focus back");
            Assert.That(interactor.IsFuelValveHighlighted, Is.True);

            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
            AssertSingleFuelCommand(controller);

            controller.ReturnToTitle();
            InstallControllerState(controller, loaded);
            yield return null;
            Assert.That(
                interactor.FocusedControl,
                Is.EqualTo(DepotReturnControl.WaterValve),
                "the stationary pointer may establish focus once on mode entry");

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.dpad.left);
            yield return null;
            Release(gamepad.dpad.left);
            yield return null;
            Assert.That(
                interactor.FocusedControl,
                Is.EqualTo(DepotReturnControl.FuelValve));
            yield return null;
            Assert.That(
                interactor.FocusedControl,
                Is.EqualTo(DepotReturnControl.FuelValve),
                "a stationary pointer must not steal gamepad focus back");

            Press(gamepad.buttonSouth);
            yield return null;
            Release(gamepad.buttonSouth);
            yield return null;
            AssertSingleFuelCommand(controller);
        }

        [UnityTest]
        public IEnumerator WinchLatchRejectsEveryNonCanonicalOperation()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState early = CreateResolvedDepotState(
                VehicleModule.WinchAssembly);
            InstallControllerState(controller, early);
            yield return null;

            LastBearingDepotReturnInteractor interactor =
                controller.World!.DepotReturnInteractor!;
            Assert.That(interactor.IsReturnLatchVisible, Is.False);
            Assert.That(interactor.ActivateReturnLatch(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            LastBearingState loaded = Apply(
                new LastBearingKernel(),
                early,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            InstallControllerState(controller, loaded);
            yield return null;
            Assert.That(interactor.IsWaterValveVisible, Is.False);
            Assert.That(interactor.IsFuelValveVisible, Is.False);
            Assert.That(interactor.IsReturnLatchVisible, Is.True);
            Assert.That(interactor.FocusedControl, Is.EqualTo(
                DepotReturnControl.ReturnLatch));

            string loadedHash = controller.CanonicalHash;
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(loadedHash));
            Assert.That(interactor.IsReturnLatchVisible, Is.True);

            ReplaceControllerStateWithoutPresentation(controller, early);
            Assert.That(interactor.ActivateReturnLatch(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(interactor.IsReturnLatchVisible, Is.False);

            InstallControllerState(controller, loaded);
            controller.ModeCoordinator!.ClearSession();
            Assert.That(interactor.ActivateReturnLatch(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(interactor.IsReturnLatchVisible, Is.False);

            ApplyPresentation(controller);
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.DepotEncounter));
            Assert.That(interactor.IsReturnLatchVisible, Is.True);
            Assert.That(interactor.ActivateReturnLatch(), Is.True);
            AssertSinglePending<FreezeReturnPayloadCommand>(controller);
            Assert.That(interactor.ActivateReturnLatch(), Is.False);
            AssertSinglePending<FreezeReturnPayloadCommand>(controller);

            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returning));
            Assert.That(interactor.ActivateReturnLatch(), Is.False);
            Assert.That(interactor.ActivateWaterValve(), Is.False);
            Assert.That(interactor.ActivateFuelValve(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
        }

        private IEnumerator BootController()
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
            InstallTemporarySaveAdapter(controller);
            _controller = controller;
            yield return null;
        }

        private static LastBearingState CreateLoadedDepotState(
            VehicleModule module)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = CreateResolvedDepotState(module);
            return Apply(
                kernel,
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
        }

        private static LastBearingState CreateResolvedDepotState(
            VehicleModule module)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = CreateOutboundState(module);
            var guard = 0;
            while (!LastBearingReadModel.FromState(state)
                       .IsDepotApproachRecoveryAvailable &&
                   guard < 1000)
            {
                LastBearingReadModel current =
                    LastBearingReadModel.FromState(state);
                if (current.IsWreckLineModulePointAvailable)
                {
                    state = Apply(
                        kernel,
                        state,
                        sequence => new OperateWreckLineModuleCommand(
                            sequence,
                            current.RouteActionKind));
                }

                state = Apply(
                    kernel,
                    state,
                    sequence => new DriveVehicleCommand(
                        sequence,
                        1000,
                        0));
                guard++;
            }

            Assert.That(
                LastBearingReadModel.FromState(state)
                    .IsDepotApproachRecoveryAvailable,
                Is.True);
            state = Apply(
                kernel,
                state,
                sequence => new OperateDepotRecoveryPointCommand(sequence));
            return Apply(
                kernel,
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
        }

        private static LastBearingState CreateOutboundState(
            VehicleModule module)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2011);
            state = Apply(
                kernel,
                state,
                sequence => new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId));
            state = Apply(
                kernel,
                state,
                sequence => new ActivateSliceInfrastructureCommand(sequence));
            PreparationChoice preparation =
                module == VehicleModule.SealedRangeTank
                    ? PreparationChoice.CivicBuffer
                    : PreparationChoice.WorkshopPush;
            state = Apply(
                kernel,
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    preparation,
                    module));
            state = Apply(
                kernel,
                state,
                sequence => new InstallVehicleModuleCommand(
                    sequence,
                    module));

            var guard = 0;
            while (state.PreparationPhase != PreparationPhase.Ready &&
                   guard < 1000)
            {
                state = kernel.Step(
                    state,
                    Array.Empty<LastBearingCommand>()).State;
                guard++;
            }

            Assert.That(
                state.PreparationPhase,
                Is.EqualTo(PreparationPhase.Ready));
            state = Apply(
                kernel,
                state,
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            state = Apply(
                kernel,
                state,
                sequence => new DebitCityManifestCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            Assert.That(
                state.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
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

        private void InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "seal-the-load-" + Guid.NewGuid().ToString("N"));
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
            ReplaceControllerStateWithoutPresentation(controller, state);
            ApplyPresentation(controller);
        }

        private static void ReplaceControllerStateWithoutPresentation(
            LastBearingGameController controller,
            LastBearingState state)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo? stateField =
                typeof(LastBearingGameController).GetField("_state", flags);
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

            stateField!.SetValue(controller, state);
            readModelField!.SetValue(
                controller,
                LastBearingReadModel.FromState(state));
            var pending = pendingField!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            pending!.Clear();
        }

        private static void ApplyPresentation(
            LastBearingGameController controller)
        {
            MethodInfo? apply =
                typeof(LastBearingGameController).GetMethod(
                    "ApplyPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);
            apply!.Invoke(controller, null);
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            MethodInfo? simulate =
                typeof(LastBearingGameController).GetMethod(
                    "SimulateOneTick",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(simulate, Is.Not.Null);
            simulate!.Invoke(controller, null);
        }

        private static LastBearingCommand[] PendingCommands(
            LastBearingGameController controller)
        {
            FieldInfo? pending =
                typeof(LastBearingGameController).GetField(
                    "_pendingCommands",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pending, Is.Not.Null);
            var commands = pending!.GetValue(controller) as
                IEnumerable<LastBearingCommand>;
            Assert.That(commands, Is.Not.Null);
            return commands!.ToArray();
        }

        private static void AssertSinglePending<T>(
            LastBearingGameController controller)
            where T : LastBearingCommand
        {
            LastBearingCommand[] commands = PendingCommands(controller);
            Assert.That(commands, Has.Length.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<T>());
        }

        private static void AssertSingleFuelCommand(
            LastBearingGameController controller)
        {
            AssertSinglePending<ChooseLiquidReturnCommand>(controller);
            Assert.That(
                ((ChooseLiquidReturnCommand)PendingCommands(controller)[0]).Kind,
                Is.EqualTo(LiquidCargoKind.Fuel));
        }

        private static void ActivateWorldTarget(
            LastBearingGameController controller,
            LastBearingDepotReturnInteractor interactor,
            string targetName)
        {
            Transform target = RequireNamed(interactor.transform, targetName);
            Camera camera = controller.World!.MainCamera!;
            Vector3 screen = camera.WorldToScreenPoint(target.position);
            var pointer = new Vector2(screen.x, screen.y);
            Assert.That(screen.z, Is.GreaterThan(0f), targetName);
            Assert.That(
                screen.x,
                Is.InRange(0f, (float)Screen.width),
                targetName);
            Assert.That(
                screen.y,
                Is.InRange(0f, (float)Screen.height),
                targetName);
            Physics.SyncTransforms();
            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.True,
                targetName);
        }

        private static Transform RequireNamed(
            Transform root,
            string name)
        {
            foreach (Transform candidate in
                     root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name)
                {
                    return candidate;
                }
            }

            throw new AssertionException("Missing transform: " + name);
        }
    }
}
