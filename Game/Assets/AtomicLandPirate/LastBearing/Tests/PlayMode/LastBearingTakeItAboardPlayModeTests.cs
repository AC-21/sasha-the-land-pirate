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
    public sealed class LastBearingTakeItAboardPlayModeTests :
        InputTestFixture
    {
        private const string SceneName = "LastBearing";
        private const string TransactionId =
            "transaction:last-bearing:take-it-aboard:0001";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:take-it-aboard:0001";

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
                    SceneManager.CreateScene(
                        "TakeItAboard_TestCleanup");
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
            yield return null;
        }

        [UnityTest]
        public IEnumerator PointerLoadsFieldSleeveOnlyAfterCanonicalTick()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState source = CreateResolvedDepotState(
                EncounterChoice.Cooperate);
            InstallControllerState(controller, source);
            yield return null;

            LastBearingWorldBuilder world = controller.World!;
            LastBearingDepotCargoInteractor interactor =
                world.DepotCargoInteractor!;
            LastBearingDepotCargoLoadingView cargo =
                world.DepotCargoLoadingView!;
            LastBearingDepotReturnInteractor returnControls =
                world.DepotReturnInteractor!;

            Assert.That(
                controller.IsDepotRepairCargoLoadAvailable,
                Is.True);
            Assert.That(interactor.IsBuilt, Is.True);
            Assert.That(
                interactor.HasDedicatedInteractionTarget,
                Is.True);
            Assert.That(interactor.IsSourceTargetVisible, Is.True);
            Assert.That(interactor.IsSourceFocused, Is.True);
            Assert.That(interactor.IsSourceHighlighted, Is.True);
            Assert.That(cargo.IsFieldSleeveAtFactionVisible, Is.True);
            Assert.That(cargo.IsCanonicalVehicleCargoVisible, Is.False);
            Assert.That(
                Vector3.Distance(
                    interactor.SourceTargetWorldPosition,
                    cargo.InteractionAnchor!.position),
                Is.LessThan(0.0001f));
            AssertSourceTargetIsTrigger(interactor);
            Vector3 cameraPosition =
                world.MainCamera!.transform.position;
            Quaternion cameraRotation =
                world.MainCamera.transform.rotation;

            string sourceHash = controller.CanonicalHash;
            ActivateWorldTarget(controller, interactor);
            AssertCameraUnmoved(
                controller,
                cameraPosition,
                cameraRotation);
            AssertSingleLoadCommand(controller);
            Assert.That(controller.Status, Does.Contain("lift queued"));
            Assert.That(
                controller.Status,
                Does.Contain("Custody changes on the authoritative tick"));
            Assert.That(controller.CanonicalHash, Is.EqualTo(sourceHash));
            Assert.That(interactor.IsSourceTargetVisible, Is.True);
            Assert.That(cargo.IsFieldSleeveAtFactionVisible, Is.True);
            Assert.That(cargo.IsCanonicalVehicleCargoVisible, Is.False);

            Assert.That(interactor.ActivateSource(), Is.False);
            controller.LoadDepotRepairCargo();
            AssertSingleLoadCommand(controller);
            Assert.That(controller.CanonicalHash, Is.EqualTo(sourceHash));
            Assert.That(interactor.IsSourceTargetVisible, Is.True);

            InvokeSimulationTick(controller);

            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(
                controller.IsDepotRepairCargoLoadAvailable,
                Is.False);
            Assert.That(controller.IsDepotRepairCargoLoadQueued, Is.False);
            Assert.That(interactor.IsSourceTargetVisible, Is.False);
            Assert.That(interactor.IsSourceFocused, Is.False);
            Assert.That(cargo.IsFieldSleeveAtFactionVisible, Is.False);
            Assert.That(cargo.IsCanonicalFieldSleeveVisible, Is.True);
            Assert.That(returnControls.IsReturnLatchVisible, Is.True);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));

            string loadedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            Assert.That(interactor.IsSourceTargetVisible, Is.False);
            Assert.That(interactor.ActivateSource(), Is.False);
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(loadedHash));
            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(interactor.IsSourceTargetVisible, Is.False);
            Assert.That(cargo.IsCanonicalFieldSleeveVisible, Is.True);
            Assert.That(returnControls.IsReturnLatchVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator KeyboardAndGamepadLoadByteIdenticalCargo()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState depotSource = CreateResolvedDepotState(
                EncounterChoice.TakeBearing);
            InstallControllerState(controller, depotSource);
            yield return null;

            LastBearingDepotCargoInteractor interactor =
                controller.World!.DepotCargoInteractor!;
            LastBearingDepotCargoLoadingView cargo =
                controller.World.DepotCargoLoadingView!;
            Vector3 depotTarget = interactor.SourceTargetWorldPosition;
            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Depot));
            Assert.That(cargo.IsCeramicBearingAtDepotVisible, Is.True);
            AssertTargetInsideViewport(controller, interactor);
            Assert.That(
                controller.World.CameraRig!.IsRoadMode,
                Is.True);
            Vector3 keyboardCameraPosition =
                controller.World.MainCamera!.transform.position;
            Quaternion keyboardCameraRotation =
                controller.World.MainCamera.transform.rotation;

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.leftArrowKey);
            yield return null;
            Release(keyboard.leftArrowKey);
            yield return null;
            Assert.That(interactor.IsSourceFocused, Is.True);
            Assert.That(interactor.IsSourceHighlighted, Is.True);

            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
            AssertCameraUnmoved(
                controller,
                keyboardCameraPosition,
                keyboardCameraRotation);
            AssertSingleLoadCommand(controller);
            InvokeSimulationTick(controller);
            string keyboardHash = controller.CanonicalHash;
            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));

            controller.ReturnToTitle();
            InstallControllerState(controller, depotSource);
            yield return null;
            Assert.That(interactor.SourceTargetWorldPosition, Is.EqualTo(
                depotTarget));
            AssertTargetInsideViewport(controller, interactor);
            Vector3 gamepadCameraPosition =
                controller.World.MainCamera.transform.position;
            Quaternion gamepadCameraRotation =
                controller.World.MainCamera.transform.rotation;

            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.dpad.right);
            yield return null;
            Release(gamepad.dpad.right);
            yield return null;
            Assert.That(interactor.IsSourceFocused, Is.True);

            Press(gamepad.buttonSouth);
            yield return null;
            Release(gamepad.buttonSouth);
            yield return null;
            AssertCameraUnmoved(
                controller,
                gamepadCameraPosition,
                gamepadCameraRotation);
            AssertSingleLoadCommand(controller);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.CanonicalHash,
                Is.EqualTo(keyboardHash),
                "keyboard and gamepad must queue byte-identical cargo authority");

            LastBearingState factionSource = CreateResolvedDepotState(
                EncounterChoice.TakeBearing,
                waitForFactionClaim: true);
            InstallControllerState(controller, factionSource);
            yield return null;

            Assert.That(
                controller.ReadModel!.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Faction));
            Assert.That(cargo.IsCeramicBearingAtFactionVisible, Is.True);
            Assert.That(
                Vector3.Distance(
                    interactor.SourceTargetWorldPosition,
                    depotTarget),
                Is.GreaterThan(1f));
            Assert.That(
                Vector3.Distance(
                    interactor.SourceTargetWorldPosition,
                    cargo.InteractionAnchor!.position),
                Is.LessThan(0.0001f));
            AssertTargetInsideViewport(controller, interactor);
            Vector3 factionCameraPosition =
                controller.World.MainCamera.transform.position;
            Quaternion factionCameraRotation =
                controller.World.MainCamera.transform.rotation;
            Assert.That(interactor.ActivateSource(), Is.True);
            AssertCameraUnmoved(
                controller,
                factionCameraPosition,
                factionCameraRotation);
            AssertSingleLoadCommand(controller);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            Assert.That(cargo.IsCanonicalCeramicBearingVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator GuardFailsClosedAcrossModeStalePendingTitleAndLoad()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingDepotCargoInteractor interactor =
                controller.World!.DepotCargoInteractor!;

            InstallControllerState(
                controller,
                CreateOutboundState(waitForFactionClaim: false));
            Assert.That(interactor.IsSourceTargetVisible, Is.False);
            Assert.That(interactor.ActivateSource(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            LastBearingState source = CreateResolvedDepotState(
                EncounterChoice.Cooperate);
            InstallControllerState(controller, source);
            Assert.That(interactor.IsSourceTargetVisible, Is.True);

            controller.Load();
            Assert.That(
                controller.SaveStatus,
                Does.StartWith("Load refused:"));
            Assert.That(interactor.IsSourceTargetVisible, Is.True);
            Assert.That(PendingCommands(controller), Is.Empty);

            controller.Save();
            string sourceHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            Assert.That(interactor.IsSourceTargetVisible, Is.False);
            controller.Load();
            yield return null;
            Assert.That(controller.CanonicalHash, Is.EqualTo(sourceHash));
            Assert.That(interactor.IsSourceTargetVisible, Is.True);

            controller.ModeCoordinator!.ClearSession();
            Assert.That(interactor.ActivateSource(), Is.False);
            Assert.That(interactor.IsSourceTargetVisible, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            ApplyPresentation(controller);
            Assert.That(interactor.IsSourceTargetVisible, Is.True);
            ReplaceControllerStateWithoutPresentation(controller, source);
            Assert.That(interactor.ActivateSource(), Is.False);
            Assert.That(interactor.IsSourceTargetVisible, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            ApplyPresentation(controller);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            InvokeGlobalShortcuts(controller);
            Release(keyboard.eKey);
            AssertSingleLoadCommand(controller);
            Assert.That(interactor.IsSourceTargetVisible, Is.True);
            Assert.That(interactor.ActivateSource(), Is.False);
            AssertSingleLoadCommand(controller);
            Assert.That(controller.CanonicalHash, Is.EqualTo(sourceHash));

            InstallControllerState(controller, source);
            Assert.That(interactor.ActivateSource(), Is.True);
            AssertSingleLoadCommand(controller);
            Press(keyboard.eKey);
            InvokeGlobalShortcuts(controller);
            Release(keyboard.eKey);
            AssertSingleLoadCommand(controller);
            Assert.That(controller.CanonicalHash, Is.EqualTo(sourceHash));
            Assert.That(interactor.IsSourceTargetVisible, Is.True);

            controller.ReturnToTitle();
            Assert.That(controller.HasActiveGame, Is.False);
            Assert.That(interactor.IsSourceTargetVisible, Is.False);
            Assert.That(interactor.ActivateSource(), Is.False);
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

        private static LastBearingState CreateResolvedDepotState(
            EncounterChoice encounter,
            bool waitForFactionClaim = false)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = DriveUntilDepotRecoveryAvailable(
                CreateOutboundState(waitForFactionClaim));
            state = Apply(
                kernel,
                state,
                sequence =>
                    new OperateDepotRecoveryPointCommand(sequence));
            return Apply(
                kernel,
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    encounter));
        }

        private static LastBearingState CreateOutboundState(
            bool waitForFactionClaim)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(
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

            state = Apply(
                kernel,
                state,
                sequence => new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId));
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

        private static LastBearingState DriveUntilDepotRecoveryAvailable(
            LastBearingState state)
        {
            var kernel = new LastBearingKernel();
            var guard = 0;
            while (!LastBearingReadModel.FromState(state)
                       .IsDepotApproachRecoveryAvailable &&
                   guard < 1000)
            {
                LastBearingReadModel model =
                    LastBearingReadModel.FromState(state);
                if (model.IsWreckLineModulePointAvailable)
                {
                    state = Apply(
                        kernel,
                        state,
                        sequence => new OperateWreckLineModuleCommand(
                            sequence,
                            model.RouteActionKind));
                }

                model = LastBearingReadModel.FromState(state);
                if (model.IsWreckLineFrameRailRecoveryAvailable)
                {
                    state = Apply(
                        kernel,
                        state,
                        sequence =>
                            new RecoverWreckLineFrameRailsCommand(
                                sequence));
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
                Is.True,
                "depot recovery gate was not reached in 1000 drives");
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
                "take-it-aboard-" + Guid.NewGuid().ToString("N"));
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

        private static void InvokeGlobalShortcuts(
            LastBearingGameController controller)
        {
            MethodInfo? shortcuts =
                typeof(LastBearingGameController).GetMethod(
                    "HandleGlobalShortcuts",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(shortcuts, Is.Not.Null);
            shortcuts!.Invoke(controller, null);
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

        private static void AssertSingleLoadCommand(
            LastBearingGameController controller)
        {
            LastBearingCommand[] commands = PendingCommands(controller);
            Assert.That(commands, Has.Length.EqualTo(1));
            Assert.That(
                commands[0],
                Is.TypeOf<LoadDepotRepairCargoCommand>());
            Assert.That(controller.IsDepotRepairCargoLoadQueued, Is.True);
        }

        private static void ActivateWorldTarget(
            LastBearingGameController controller,
            LastBearingDepotCargoInteractor interactor)
        {
            Vector2 screen = AssertTargetInsideViewport(
                controller,
                interactor);
            Physics.SyncTransforms();
            Assert.That(
                interactor.TryActivateAtScreenPosition(screen),
                Is.True);
        }

        private static Vector2 AssertTargetInsideViewport(
            LastBearingGameController controller,
            LastBearingDepotCargoInteractor interactor)
        {
            Transform target = RequireNamed(
                interactor.transform,
                LastBearingDepotCargoInteractor.SourceTargetName);
            Camera camera = controller.World!.MainCamera!;
            Vector3 screen = camera.WorldToScreenPoint(target.position);
            Assert.That(screen.z, Is.GreaterThan(0f));
            Assert.That(
                screen.x,
                Is.InRange(0f, (float)Screen.width));
            Assert.That(
                screen.y,
                Is.InRange(0f, (float)Screen.height));
            return new Vector2(screen.x, screen.y);
        }

        private static void AssertSourceTargetIsTrigger(
            LastBearingDepotCargoInteractor interactor)
        {
            Transform target = RequireNamed(
                interactor.transform,
                LastBearingDepotCargoInteractor.SourceTargetName);
            Collider? collider = target.GetComponent<Collider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider!.isTrigger, Is.True);
            Assert.That(
                target.gameObject.layer,
                Is.EqualTo(
                    LastBearingDepotCargoInteractor.InteractionLayer));
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

        private static void AssertCameraUnmoved(
            LastBearingGameController controller,
            Vector3 expectedPosition,
            Quaternion expectedRotation)
        {
            Transform camera = controller.World!.MainCamera!.transform;
            Assert.That(
                Vector3.Distance(camera.position, expectedPosition),
                Is.LessThan(0.0001f),
                "cargo input must not pan the active road pose");
            Assert.That(
                Quaternion.Angle(camera.rotation, expectedRotation),
                Is.LessThan(0.0001f),
                "cargo input must not rotate the active road pose");
        }
    }
}
