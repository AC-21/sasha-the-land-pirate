#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class LastBearingTurnTheKeyPlayModeTests :
        InputTestFixture
    {
        private const string SceneName = "LastBearing";
        private const string TransactionId =
            "transaction:last-bearing:turn-the-key:0001";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:turn-the-key:0001";

        private readonly List<string> _temporarySaveRoots =
            new List<string>();
        private LastBearingGameController? _controller;
        private string? _profileDirectory;

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene(
                    "TurnTheKey_TestCleanup");
                SceneManager.SetActiveScene(cleanup);
                AsyncOperation? unload =
                    SceneManager.UnloadSceneAsync(scene);
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
            _profileDirectory = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerQueuesOrderedManifestThenAutosavesDriving()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            InstallControllerState(
                controller,
                CreateReadyState(ColonyComposition.HumanOnly));
            controller.Save();
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));
            Dictionary<string, string> readyProfile =
                SnapshotSaveFiles(_profileDirectory!);
            string readyHash = controller.CanonicalHash;

            controller.ReturnToTitle();
            controller.Load();
            yield return null;
            Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));
            Assert.That(
                controller.ReadModel!.PreparationPhase,
                Is.EqualTo(PreparationPhase.Ready));

            controller.OpenGarageBay();
            yield return null;
            LastBearingGarageDepartureInteractor interactor =
                RequireInteractor(controller);
            Assert.That(interactor.IsInputArmed, Is.True);
            Assert.That(interactor.IsFocused, Is.False);
            Assert.That(interactor.IsTargetVisible, Is.True);
            Assert.That(
                interactor.HasDedicatedInteractionTarget,
                Is.True);
            Assert.That(controller.CanCommitExpedition, Is.True);
            Assert.That(controller.IsGarageDepartureAvailable, Is.True);
            Assert.That(
                controller.Hud!.BlocksWorldPointer(
                    new Vector2(20f, Screen.height - 20f)),
                Is.True);
            Assert.That(
                interactor.TryActivateAtScreenPosition(
                    new Vector2(20f, Screen.height - 20f)),
                Is.False);

            long fuelBefore = controller.ReadModel.FuelUnits;
            long tickBefore = controller.ReadModel.GlobalTick;
            string canonicalBefore = controller.CanonicalHash;
            Assert.That(ActivateWorldTarget(controller, interactor), Is.True);
            AssertOrderedDepartureComposite(controller);
            Assert.That(interactor.IsLaunchDogThrown, Is.True);
            Assert.That(
                interactor.Feedback,
                Is.EqualTo(
                    "CLAMP THROWN\nLEDGER POSTS NEXT TICK"));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
            Assert.That(controller.ReadModel.GlobalTick, Is.EqualTo(tickBefore));
            Assert.That(controller.ReadModel.FuelUnits, Is.EqualTo(fuelBefore));
            AssertSaveSnapshot(
                readyProfile,
                SnapshotSaveFiles(_profileDirectory!));

            InvokeSimulationTick(controller);

            Assert.That(controller.HasPendingPlayerCommands, Is.False);
            Assert.That(controller.IsExpeditionCommitQueued, Is.False);
            Assert.That(
                controller.ReadModel!.TransactionPhase,
                Is.EqualTo(TransactionPhase.RoadOwned));
            Assert.That(
                controller.ReadModel.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(
                controller.ReadModel.FuelUnits,
                Is.EqualTo(
                    fuelBefore -
                    LastBearingBalanceV1.ShortRouteFuelManifestUnits));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));
            Assert.That(
                controller.ReadModel.PreparationPhase,
                Is.EqualTo(PreparationPhase.Committed));
            Assert.That(
                controller.ModeCoordinator.IsRoadPresentationActive,
                Is.True);
            Assert.That(
                controller.World!.CameraRig!.IsRoadChaseActive,
                Is.True);
            Assert.That(interactor.IsTargetVisible, Is.False);
            Assert.That(interactor.IsFocused, Is.False);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));
            Assert.That(
                SnapshotSaveFiles(_profileDirectory!),
                Is.Not.EqualTo(readyProfile));
            Assert.That(
                GenerationCount(_profileDirectory!),
                Is.EqualTo(2));

            string departedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            yield return null;
            Assert.That(controller.CanonicalHash, Is.EqualTo(departedHash));
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));
            Assert.That(interactor.IsTargetVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator KeyboardGamepadAndCombinedInputsQueueOnceWithParity()
        {
            yield return BootController();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            ColonyComposition[] compositions =
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            };

            for (var index = 0; index < compositions.Length; index++)
            {
                InstallControllerState(
                    _controller!,
                    CreateReadyState(compositions[index]));
                _controller!.ShowCityOverview();
                _controller.OpenGarageBay();
                yield return null;
                LastBearingGarageDepartureInteractor interactor =
                    RequireInteractor(_controller);
                Assert.That(interactor.IsInputArmed, Is.True);
                Assert.That(interactor.IsFocused, Is.False);
                long fuelBefore = _controller.ReadModel!.FuelUnits;
                string hashBefore = _controller.CanonicalHash;

                if (index == 0)
                {
                    Press(keyboard.rightArrowKey);
                    yield return null;
                    Release(keyboard.rightArrowKey);
                    yield return null;
                    Assert.That(interactor.IsFocused, Is.True);
                    Press(keyboard.eKey);
                    yield return null;
                    Release(keyboard.eKey);
                    yield return null;
                }
                else
                {
                    Press(gamepad.dpad.right);
                    yield return null;
                    Release(gamepad.dpad.right);
                    yield return null;
                    Assert.That(interactor.IsFocused, Is.True);
                    if (index == 1)
                    {
                        Press(gamepad.buttonSouth);
                        yield return null;
                        Release(gamepad.buttonSouth);
                        yield return null;
                    }
                    else
                    {
                        Press(keyboard.eKey);
                        Press(gamepad.buttonSouth);
                        yield return null;
                        Release(keyboard.eKey);
                        Release(gamepad.buttonSouth);
                        yield return null;
                    }
                }

                AssertOrderedDepartureComposite(_controller);
                _controller.CommitExpedition();
                AssertOrderedDepartureComposite(_controller);
                Assert.That(_controller.CanonicalHash, Is.EqualTo(hashBefore));
                InvokeSimulationTick(_controller);
                Assert.That(
                    _controller.ReadModel!.Composition,
                    Is.EqualTo(compositions[index]));
                Assert.That(
                    _controller.ReadModel.ExpeditionPhase,
                    Is.EqualTo(ExpeditionPhase.Outbound));
                Assert.That(
                    _controller.ReadModel.FuelUnits,
                    Is.EqualTo(
                        fuelBefore -
                        LastBearingBalanceV1.ShortRouteFuelManifestUnits));
            }
        }

        [UnityTest]
        public IEnumerator GateRejectsWrongModeStalePendingFuelFrontPauseAwayAndRepeat()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState ready =
                CreateReadyState(ColonyComposition.Mixed);

            InstallControllerState(controller, ready);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            controller.CommitExpedition();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(controller.Status, Does.Contain("fixed garage"));

            InstallControllerState(
                controller,
                MutateState(
                    ready,
                    ("PreparationPhase", PreparationPhase.Preparing)));
            controller.OpenGarageBay();
            yield return null;
            Assert.That(controller.CanCommitExpedition, Is.False);
            Assert.That(
                RequireInteractor(controller).IsTargetVisible,
                Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            InstallControllerState(controller, ready);
            controller.OpenGarageBay();
            yield return null;
            LastBearingGarageDepartureInteractor interactor =
                RequireInteractor(controller);
            interactor.FocusControl();
            ReplaceRuntimeReadModelOnly(
                controller,
                LastBearingReadModel.FromState(ready));
            Assert.That(interactor.ActivateControl(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            ApplyPresentation(controller);

            interactor = RequireInteractor(controller);
            interactor.FocusControl();
            controller.TogglePause();
            Assert.That(PendingCommands(controller), Has.Length.EqualTo(1));
            Assert.That(interactor.ActivateControl(), Is.False);
            Assert.That(PendingCommands(controller), Has.Length.EqualTo(1));

            InstallControllerState(
                controller,
                MutateState(ready, ("FuelUnits", 0L)));
            controller.OpenGarageBay();
            yield return null;
            interactor = RequireInteractor(controller);
            interactor.FocusControl();
            Assert.That(interactor.IsTargetVisible, Is.True);
            Assert.That(controller.CanCommitExpedition, Is.False);
            Assert.That(interactor.ActivateControl(), Is.False);
            Assert.That(interactor.Feedback, Does.Contain("FUEL SHORT"));
            Assert.That(PendingCommands(controller), Is.Empty);

            InstallControllerState(
                controller,
                MutateState(
                    ready,
                    ("PauseCause", PauseCause.DustFrontAlert),
                    ("IsDustFrontAcknowledgementRequired", true),
                    ("DustFrontOutcome", DustFrontOutcome.Held)));
            controller.OpenGarageBay();
            yield return null;
            interactor = RequireInteractor(controller);
            interactor.FocusControl();
            Assert.That(interactor.ActivateControl(), Is.False);
            Assert.That(interactor.Feedback, Does.Contain("FRONT ALERT"));
            Assert.That(PendingCommands(controller), Is.Empty);

            InstallControllerState(
                controller,
                MutateState(
                    ready,
                    ("PauseCause", PauseCause.Explicit)));
            controller.OpenGarageBay();
            yield return null;
            interactor = RequireInteractor(controller);
            interactor.FocusControl();
            Assert.That(interactor.ActivateControl(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(controller.Status, Does.Contain("Resume"));

            InstallControllerState(controller, CreateOutboundState(ready));
            controller.CommitExpedition();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(controller.Status, Does.Contain("already away"));

            InstallControllerState(controller, ready);
            controller.OpenGarageBay();
            yield return null;
            interactor = RequireInteractor(controller);
            interactor.FocusControl();
            Assert.That(interactor.ActivateControl(), Is.True);
            AssertOrderedDepartureComposite(controller);
            Assert.That(interactor.ActivateControl(), Is.False);
            controller.CommitExpedition();
            AssertOrderedDepartureComposite(controller);
        }

        [UnityTest]
        public IEnumerator GarageEntryRequiresReleaseAndFourCyclesKeepOneTopology()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            InstallControllerState(
                controller,
                CreateReadyState(ColonyComposition.RobotOnly));
            string canonicalBefore = controller.CanonicalHash;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

            controller.ShowCityOverview();
            Press(keyboard.eKey);
            Press(gamepad.buttonSouth);
            controller.OpenGarageBay();
            yield return null;
            LastBearingGarageDepartureInteractor interactor =
                RequireInteractor(controller);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(interactor.IsFocused, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            Release(keyboard.eKey);
            Release(gamepad.buttonSouth);
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);
            Assert.That(interactor.IsFocused, Is.False);
            Press(gamepad.buttonSouth);
            yield return null;
            Release(gamepad.buttonSouth);
            yield return null;
            Assert.That(
                PendingCommands(controller),
                Is.Empty,
                "South must not act as a global departure shortcut");

            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.ShowCityOverview();
                yield return null;
                Assert.That(interactor.IsTargetVisible, Is.False);
                Assert.That(interactor.IsFocused, Is.False);

                controller.OpenGarageBay();
                yield return null;
                Assert.That(interactor.IsInputArmed, Is.True);
                Assert.That(interactor.IsFocused, Is.False);
                Assert.That(interactor.IsTargetVisible, Is.True);
                AssertSingleGarageTopology(controller);
            }

            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtHome));
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
            _profileDirectory =
                InstallTemporarySaveAdapter(controller);
            _controller = controller;
            yield return null;
        }

        private static LastBearingState CreateReadyState(
            ColonyComposition composition)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(
                    composition,
                    2011);
            string resident = composition == ColonyComposition.RobotOnly
                ? ResidentRoster.RobotResidentId
                : ResidentRoster.HumanResidentId;
            state = Apply(
                kernel,
                state,
                sequence =>
                    new AssignResidentCommand(sequence, resident));
            state = Apply(
                kernel,
                state,
                sequence =>
                    new ActivateSliceInfrastructureCommand(sequence));
            state = Apply(
                kernel,
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.WorkshopPush,
                    VehicleModule.WinchAssembly));
            state = Apply(
                kernel,
                state,
                sequence => new InstallVehicleModuleCommand(
                    sequence,
                    VehicleModule.WinchAssembly));

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
            Assert.That(
                state.ModuleInstallationState,
                Is.EqualTo(ModuleInstallationState.Installed));
            Assert.That(state.TransactionPhase, Is.EqualTo(TransactionPhase.None));
            Assert.That(
                state.IsDustFrontAcknowledgementRequired,
                Is.False);
            return state;
        }

        private static LastBearingState CreateOutboundState(
            LastBearingState ready)
        {
            var kernel = new LastBearingKernel();
            long sequence = ready.NextCommandSequence;
            return kernel.Step(
                ready,
                new LastBearingCommand[]
                {
                    new PrepareExpeditionTransactionCommand(
                        sequence,
                        TransactionId,
                        TransactionFingerprint),
                    new DebitCityManifestCommand(
                        sequence + 1,
                        TransactionId,
                        TransactionFingerprint),
                    new DepartExpeditionCommand(sequence + 2),
                }).State;
        }

        private static LastBearingState Apply(
            LastBearingKernel kernel,
            LastBearingState state,
            Func<long, LastBearingCommand> command)
        {
            return kernel.Step(
                state,
                new[] { command(state.NextCommandSequence) }).State;
        }

        private static LastBearingState MutateState(
            LastBearingState state,
            params (string Field, object Value)[] changes)
        {
            Type? builderType =
                typeof(LastBearingState).Assembly.GetType(
                    "AtomicLandPirate.Simulation.LastBearing." +
                    "LastBearingStateBuilder");
            Assert.That(builderType, Is.Not.Null);
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public;
            ConstructorInfo? constructor = builderType!.GetConstructor(
                flags,
                binder: null,
                new[] { typeof(LastBearingState) },
                modifiers: null);
            MethodInfo? build = builderType.GetMethod("Build", flags);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(build, Is.Not.Null);
            object builder = constructor!.Invoke(new object[] { state });
            foreach ((string fieldName, object value) in changes)
            {
                FieldInfo? field = builderType.GetField(
                    fieldName,
                    flags);
                Assert.That(field, Is.Not.Null, fieldName);
                field!.SetValue(builder, value);
            }

            var result = build!.Invoke(builder, null) as LastBearingState;
            Assert.That(result, Is.Not.Null);
            return result!;
        }

        private static void InstallControllerState(
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
            FieldInfo? pendingField =
                typeof(LastBearingGameController).GetField(
                    "_pendingCommands",
                    flags);
            MethodInfo? resetSnapshots =
                typeof(LastBearingGameController).GetMethod(
                    "ResetPublicSnapshotsToRuntime",
                    flags);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(readModelField, Is.Not.Null);
            Assert.That(pendingField, Is.Not.Null);
            Assert.That(resetSnapshots, Is.Not.Null);
            stateField!.SetValue(controller, state);
            readModelField!.SetValue(
                controller,
                LastBearingReadModel.FromState(state));
            var pending =
                pendingField!.GetValue(controller) as
                    IList<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            pending!.Clear();
            resetSnapshots!.Invoke(controller, null);
            ApplyPresentation(controller);
        }

        private static void ReplaceRuntimeReadModelOnly(
            LastBearingGameController controller,
            LastBearingReadModel model)
        {
            FieldInfo? readModel =
                typeof(LastBearingGameController).GetField(
                    "_readModel",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(readModel, Is.Not.Null);
            readModel!.SetValue(controller, model);
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

        private static void AssertOrderedDepartureComposite(
            LastBearingGameController controller)
        {
            LastBearingCommand[] commands = PendingCommands(controller);
            long firstSequence = controller.State!.NextCommandSequence;
            Assert.That(commands, Has.Length.EqualTo(3));
            Assert.That(
                commands[0],
                Is.TypeOf<PrepareExpeditionTransactionCommand>());
            Assert.That(
                commands[1],
                Is.TypeOf<DebitCityManifestCommand>());
            Assert.That(
                commands[2],
                Is.TypeOf<DepartExpeditionCommand>());
            var prepare =
                (PrepareExpeditionTransactionCommand)commands[0];
            var debit = (DebitCityManifestCommand)commands[1];
            Assert.That(prepare.Sequence, Is.EqualTo(firstSequence));
            Assert.That(debit.Sequence, Is.EqualTo(firstSequence + 1));
            Assert.That(commands[2].Sequence, Is.EqualTo(firstSequence + 2));
            Assert.That(debit.TransactionId, Is.EqualTo(prepare.TransactionId));
            Assert.That(debit.Fingerprint, Is.EqualTo(prepare.Fingerprint));
            Assert.That(controller.IsExpeditionCommitQueued, Is.True);
        }

        private static LastBearingGarageDepartureInteractor RequireInteractor(
            LastBearingGameController controller)
        {
            LastBearingGarageDepartureInteractor? interactor =
                controller.World?.GarageDepartureInteractor;
            Assert.That(interactor, Is.Not.Null);
            return interactor!;
        }

        private static bool ActivateWorldTarget(
            LastBearingGameController controller,
            LastBearingGarageDepartureInteractor interactor)
        {
            Camera camera = controller.World!.MainCamera!;
            Vector3 screen = camera.WorldToScreenPoint(
                interactor.TargetWorldPosition);
            var pointer = new Vector2(screen.x, screen.y);
            Assert.That(screen.z, Is.GreaterThan(0f));
            Assert.That(
                screen.x,
                Is.InRange(0f, (float)Screen.width));
            Assert.That(
                screen.y,
                Is.InRange(0f, (float)Screen.height));
            Assert.That(
                controller.Hud!.BlocksWorldPointer(pointer),
                Is.False);
            Physics.SyncTransforms();
            return interactor.TryActivateAtScreenPosition(pointer);
        }

        private static void AssertSingleGarageTopology(
            LastBearingGameController controller)
        {
            Transform root = controller.transform;
            Assert.That(
                root.GetComponentsInChildren<Camera>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                root.GetComponentsInChildren<AudioListener>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                root.GetComponentsInChildren<
                    LastBearingGarageDepartureInteractor>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                root.GetComponentsInChildren<Transform>(true).Count(
                    candidate =>
                        candidate.name ==
                        LastBearingGarageDepartureInteractor.TargetName),
                Is.EqualTo(1));
        }

        private string InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "turn-the-key-" + Guid.NewGuid().ToString("N"));
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

        private static Dictionary<string, string> SnapshotSaveFiles(
            string profileDirectory)
        {
            Assert.That(Directory.Exists(profileDirectory), Is.True);
            return Directory.GetFiles(
                    profileDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .ToDictionary(
                    path => Path.GetRelativePath(profileDirectory, path),
                    path => Convert.ToBase64String(
                        File.ReadAllBytes(path)),
                    StringComparer.Ordinal);
        }

        private static void AssertSaveSnapshot(
            IReadOnlyDictionary<string, string> expected,
            IReadOnlyDictionary<string, string> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Assert.That(actual.ContainsKey(pair.Key), Is.True);
                Assert.That(actual[pair.Key], Is.EqualTo(pair.Value));
            }
        }

        private static int GenerationCount(string profileDirectory)
        {
            return Directory.GetFiles(
                profileDirectory,
                "gen-*.lbg",
                SearchOption.TopDirectoryOnly).Length;
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
    }
}
