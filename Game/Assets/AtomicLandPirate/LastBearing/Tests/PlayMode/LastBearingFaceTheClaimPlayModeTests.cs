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
    public sealed class LastBearingFaceTheClaimPlayModeTests :
        InputTestFixture
    {
        private const string SceneName = "LastBearing";
        private const string TransactionId =
            "transaction:last-bearing:face-the-claim:0001";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:face-the-claim:0001";

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
                        "FaceTheClaim_TestCleanup");
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
        public IEnumerator PointerCooperateAutosavesAndReloadsFieldSleeveSource()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState decision = Apply(
                new LastBearingKernel(),
                CreateDecisionState(),
                sequence => new TriggerAutoPauseAlertCommand(sequence));
            long trustBefore = decision.FactionTrust;
            InstallControllerState(controller, decision);
            yield return null;

            LastBearingDepotDecisionInteractor interactor =
                controller.World!.DepotDecisionInteractor!;
            LastBearingDepotCargoLoadingView cargo =
                controller.World.DepotCargoLoadingView!;
            Assert.That(controller.IsDepotDecisionAvailable, Is.True);
            Assert.That(
                controller.ReadModel!.PauseCause,
                Is.EqualTo(PauseCause.AutoAlert));
            Assert.That(interactor.IsBuilt, Is.True);
            Assert.That(
                interactor.HasDedicatedInteractionTargets,
                Is.True);
            Assert.That(interactor.IsCooperateStationVisible, Is.True);
            Assert.That(interactor.IsTakeBearingStationVisible, Is.True);
            Assert.That(
                cargo.State,
                Is.EqualTo(DepotCargoLoadingPresentationState.Dormant));
            AssertDecisionTargetIsTrigger(
                interactor,
                LastBearingDepotDecisionInteractor.CooperateStationName);
            AssertDecisionTargetIsTrigger(
                interactor,
                LastBearingDepotDecisionInteractor.TakeBearingStationName);

            string unresolvedHash = controller.CanonicalHash;
            ActivateWorldTarget(
                controller,
                interactor,
                LastBearingDepotDecisionInteractor.CooperateStationName);
            AssertSingleResolution(
                controller,
                EncounterChoice.Cooperate);
            Assert.That(controller.CanonicalHash, Is.EqualTo(unresolvedHash));
            Assert.That(interactor.ActivateTakeBearing(), Is.False);
            AssertSingleResolution(
                controller,
                EncounterChoice.Cooperate);

            InvokeSimulationTick(controller);
            Assert.That(
                controller.State!.DepotResolution,
                Is.EqualTo(EncounterChoice.Cooperate));
            Assert.That(
                controller.ReadModel!.PauseCause,
                Is.EqualTo(PauseCause.None));
            Assert.That(
                controller.ReadModel.RepairCargoKind,
                Is.EqualTo(RepairCargoKind.FieldSleeve));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Faction));
            Assert.That(
                controller.ReadModel.DepotControl,
                Is.EqualTo(DepotControl.SharedAccess));
            Assert.That(
                controller.ReadModel.FactionTrust,
                Is.EqualTo(
                    trustBefore +
                    LastBearingBalanceV1.CooperateTrustDelta));
            Assert.That(controller.IsDepotDecisionAvailable, Is.False);
            Assert.That(interactor.IsCooperateStationVisible, Is.False);
            Assert.That(interactor.IsTakeBearingStationVisible, Is.False);
            Assert.That(
                interactor.FocusedDecision,
                Is.EqualTo(DepotDecisionControl.None));
            Assert.That(cargo.IsFieldSleeveAtFactionVisible, Is.True);
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"));

            string resolvedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            Assert.That(interactor.IsCooperateStationVisible, Is.False);
            Assert.That(interactor.IsTakeBearingStationVisible, Is.False);
            Assert.That(interactor.ActivateCooperate(), Is.False);
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(resolvedHash));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(
                    LastBearingPresentationMode.DepotEncounter));
            Assert.That(interactor.IsCooperateStationVisible, Is.False);
            Assert.That(interactor.IsTakeBearingStationVisible, Is.False);
            Assert.That(cargo.IsFieldSleeveAtFactionVisible, Is.True);
            Assert.That(
                controller.ReadModel!.FactionTrust,
                Is.EqualTo(
                    trustBefore +
                    LastBearingBalanceV1.CooperateTrustDelta));
        }

        [UnityTest]
        public IEnumerator KeyboardAndGamepadTakeAreByteIdentical()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState decision = CreateDecisionState();
            RepairCargoCustody expectedCustody =
                ExpectedTakeCustody(decision);
            long expectedGrievance =
                decision.FactionGrievance +
                LastBearingBalanceV1.TakeGrievanceDelta;
            InstallControllerState(controller, decision);

            LastBearingDepotDecisionInteractor interactor =
                controller.World!.DepotDecisionInteractor!;
            LastBearingCameraRig cameraRig = controller.World.CameraRig!;
            Assert.That(cameraRig.IsRoadMode, Is.True);
            Vector3 keyboardCameraPosition =
                controller.World.MainCamera!.transform.position;
            Quaternion keyboardCameraRotation =
                controller.World.MainCamera.transform.rotation;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Vector2 cooperateScreen = ScreenPosition(
                controller,
                interactor,
                LastBearingDepotDecisionInteractor.CooperateStationName);
            Set(mouse.position, cooperateScreen);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.rightArrowKey);
            yield return null;
            Release(keyboard.rightArrowKey);
            yield return null;
            Assert.That(
                interactor.FocusedDecision,
                Is.EqualTo(DepotDecisionControl.TakeBearing),
                "first-frame stationary mouse hover must not beat keyboard");
            yield return null;
            Assert.That(
                interactor.FocusedDecision,
                Is.EqualTo(DepotDecisionControl.TakeBearing),
                "stationary mouse must not reclaim keyboard focus");
            AssertCameraUnmoved(
                controller,
                keyboardCameraPosition,
                keyboardCameraRotation);

            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
            AssertCameraUnmoved(
                controller,
                keyboardCameraPosition,
                keyboardCameraRotation);
            AssertSingleResolution(
                controller,
                EncounterChoice.TakeBearing);
            InvokeSimulationTick(controller);
            string keyboardHash = controller.CanonicalHash;
            AssertTakeOutcome(
                controller,
                expectedCustody,
                expectedGrievance);

            controller.ReturnToTitle();
            InstallControllerState(controller, decision);
            Assert.That(cameraRig.IsRoadMode, Is.True);
            Vector3 gamepadCameraPosition =
                controller.World.MainCamera.transform.position;
            Quaternion gamepadCameraRotation =
                controller.World.MainCamera.transform.rotation;
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.dpad.right);
            yield return null;
            Release(gamepad.dpad.right);
            yield return null;
            Assert.That(
                interactor.FocusedDecision,
                Is.EqualTo(DepotDecisionControl.TakeBearing),
                "first-frame stationary mouse hover must not beat gamepad");
            yield return null;
            Assert.That(
                interactor.FocusedDecision,
                Is.EqualTo(DepotDecisionControl.TakeBearing),
                "stationary mouse must not reclaim gamepad focus");
            AssertCameraUnmoved(
                controller,
                gamepadCameraPosition,
                gamepadCameraRotation);

            Press(gamepad.buttonSouth);
            yield return null;
            Release(gamepad.buttonSouth);
            yield return null;
            AssertCameraUnmoved(
                controller,
                gamepadCameraPosition,
                gamepadCameraRotation);
            AssertSingleResolution(
                controller,
                EncounterChoice.TakeBearing);
            InvokeSimulationTick(controller);
            AssertTakeOutcome(
                controller,
                expectedCustody,
                expectedGrievance);
            Assert.That(
                controller.CanonicalHash,
                Is.EqualTo(keyboardHash),
                "keyboard and gamepad must queue byte-identical authority");
        }

        [UnityTest]
        public IEnumerator GuardRejectsEarlyWrongModeDustStalePendingAndTitle()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingDepotDecisionInteractor interactor =
                controller.World!.DepotDecisionInteractor!;

            InstallControllerState(
                controller,
                CreateOutboundState());
            Assert.That(controller.IsDepotDecisionAvailable, Is.False);
            Assert.That(interactor.IsCooperateStationVisible, Is.False);
            Assert.That(interactor.IsTakeBearingStationVisible, Is.False);
            Assert.That(interactor.ActivateTakeBearing(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            LastBearingState decision = CreateDecisionState();
            RepairCargoCustody expectedCustody =
                ExpectedTakeCustody(decision);
            long expectedGrievance =
                decision.FactionGrievance +
                LastBearingBalanceV1.TakeGrievanceDelta;
            InstallControllerState(controller, decision);
            Assert.That(controller.IsDepotDecisionAvailable, Is.True);
            controller.Load();
            Assert.That(
                controller.SaveStatus,
                Does.StartWith("Load refused:"));
            Assert.That(controller.IsDepotDecisionAvailable, Is.True);
            Assert.That(interactor.IsCooperateStationVisible, Is.True);
            Assert.That(interactor.IsTakeBearingStationVisible, Is.True);
            Assert.That(PendingCommands(controller), Is.Empty);

            controller.ModeCoordinator!.ClearSession();
            Assert.That(controller.IsDepotDecisionAvailable, Is.False);
            Assert.That(interactor.ActivateCooperate(), Is.False);
            Assert.That(interactor.IsCooperateStationVisible, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            ApplyPresentation(controller);
            Assert.That(controller.IsDepotDecisionAvailable, Is.True);
            SetRuntimePauseCause(
                controller,
                PauseCause.DustFrontAlert);
            Assert.That(controller.IsDepotDecisionAvailable, Is.False);
            Assert.That(interactor.ActivateTakeBearing(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            InstallControllerState(controller, decision);
            ReplaceControllerStateWithoutPresentation(
                controller,
                decision);
            Assert.That(controller.IsDepotDecisionAvailable, Is.True);
            Assert.That(interactor.ActivateTakeBearing(), Is.False);
            Assert.That(interactor.IsTakeBearingStationVisible, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            ApplyPresentation(controller);
            Assert.That(interactor.ActivateTakeBearing(), Is.True);
            controller.ResolveDepot(cooperate: true);
            AssertSingleResolution(
                controller,
                EncounterChoice.TakeBearing);
            Assert.That(controller.IsDepotDecisionAvailable, Is.False);
            InvokeSimulationTick(controller);
            AssertTakeOutcome(
                controller,
                expectedCustody,
                expectedGrievance);
            Assert.That(interactor.IsCooperateStationVisible, Is.False);
            Assert.That(interactor.IsTakeBearingStationVisible, Is.False);
            Assert.That(
                expectedCustody == RepairCargoCustody.Depot
                    ? controller.World.DepotCargoLoadingView!
                        .IsCeramicBearingAtDepotVisible
                    : controller.World.DepotCargoLoadingView!
                        .IsCeramicBearingAtFactionVisible,
                Is.True);

            controller.ReturnToTitle();
            Assert.That(controller.HasActiveGame, Is.False);
            Assert.That(controller.IsDepotDecisionAvailable, Is.False);
            Assert.That(interactor.ActivateCooperate(), Is.False);
            Assert.That(interactor.ActivateTakeBearing(), Is.False);
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

        private static LastBearingState CreateDecisionState()
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = CreateOutboundState();
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
                sequence =>
                    new OperateDepotRecoveryPointCommand(sequence));
            Assert.That(
                state.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtDepot));
            Assert.That(
                state.DepotResolution,
                Is.EqualTo(EncounterChoice.Unresolved));
            return state;
        }

        private static LastBearingState CreateOutboundState()
        {
            var kernel = new LastBearingKernel();
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(
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
                sequence =>
                    new ActivateSliceInfrastructureCommand(sequence));
            state = Apply(
                kernel,
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.CivicBuffer,
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
                "face-the-claim-" + Guid.NewGuid().ToString("N"));
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

        private static void SetRuntimePauseCause(
            LastBearingGameController controller,
            PauseCause pauseCause)
        {
            FieldInfo? readModelField =
                typeof(LastBearingGameController).GetField(
                    "_readModel",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo? pauseProperty =
                typeof(LastBearingReadModel).GetProperty(
                    nameof(LastBearingReadModel.PauseCause),
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(readModelField, Is.Not.Null);
            Assert.That(pauseProperty, Is.Not.Null);
            var model = readModelField!.GetValue(controller) as
                LastBearingReadModel;
            Assert.That(model, Is.Not.Null);
            pauseProperty!.SetValue(model, pauseCause);
            controller.World!.ApplyDepotDecisionInteraction(model!);
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

        private static void AssertSingleResolution(
            LastBearingGameController controller,
            EncounterChoice expected)
        {
            LastBearingCommand[] commands = PendingCommands(controller);
            Assert.That(commands, Has.Length.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<ResolveDepotCommand>());
            Assert.That(
                ((ResolveDepotCommand)commands[0]).Choice,
                Is.EqualTo(expected));
        }

        private static void AssertTakeOutcome(
            LastBearingGameController controller,
            RepairCargoCustody expectedCustody,
            long expectedGrievance)
        {
            Assert.That(
                controller.State!.DepotResolution,
                Is.EqualTo(EncounterChoice.TakeBearing));
            Assert.That(
                controller.ReadModel!.RepairCargoKind,
                Is.EqualTo(RepairCargoKind.CeramicBearing));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(expectedCustody));
            Assert.That(
                controller.ReadModel.FactionClaimState,
                Is.EqualTo(FactionClaimState.Aggrieved));
            Assert.That(
                controller.ReadModel.FactionGrievance,
                Is.EqualTo(expectedGrievance));
        }

        private static RepairCargoCustody ExpectedTakeCustody(
            LastBearingState decision)
        {
            return decision.DepotBearingDisposition ==
                   DepotBearingDisposition.AtDepot
                ? RepairCargoCustody.Depot
                : RepairCargoCustody.Faction;
        }

        private static void ActivateWorldTarget(
            LastBearingGameController controller,
            LastBearingDepotDecisionInteractor interactor,
            string targetName)
        {
            Vector2 pointer = ScreenPosition(
                controller,
                interactor,
                targetName);
            Physics.SyncTransforms();
            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.True,
                targetName);
        }

        private static Vector2 ScreenPosition(
            LastBearingGameController controller,
            LastBearingDepotDecisionInteractor interactor,
            string targetName)
        {
            Transform target = RequireNamed(
                interactor.transform,
                targetName);
            Camera camera = controller.World!.MainCamera!;
            Vector3 screen =
                camera.WorldToScreenPoint(target.position);
            Assert.That(screen.z, Is.GreaterThan(0f), targetName);
            Assert.That(
                screen.x,
                Is.InRange(0f, (float)Screen.width),
                targetName);
            Assert.That(
                screen.y,
                Is.InRange(0f, (float)Screen.height),
                targetName);
            return new Vector2(screen.x, screen.y);
        }

        private static void AssertDecisionTargetIsTrigger(
            LastBearingDepotDecisionInteractor interactor,
            string targetName)
        {
            Transform target = RequireNamed(
                interactor.transform,
                targetName);
            Collider? collider = target.GetComponent<Collider>();
            Assert.That(collider, Is.Not.Null, targetName);
            Assert.That(collider!.isTrigger, Is.True, targetName);
            Assert.That(
                target.gameObject.layer,
                Is.EqualTo(
                    LastBearingDepotDecisionInteractor.InteractionLayer),
                targetName);
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
                "depot choice input must not pan the active road pose");
            Assert.That(
                Quaternion.Angle(camera.rotation, expectedRotation),
                Is.LessThan(0.0001f),
                "depot choice input must not rotate the active road pose");
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
