#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed partial class LastBearingPlayModeTests
    {
        [UnityTest]
        public IEnumerator RangeTankGamepadAndPointerRecoverRailsExactlyOnce()
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
            controller.enabled = false;
            string profileDirectory =
                InstallTemporarySaveAdapter(controller);
            LastBearingState gate = DriveUntilWreckLineAvailable(
                CreateOutboundState(
                    installPatchworkSkidPlate: true,
                    module: VehicleModule.SealedRangeTank));
            InstallControllerState(controller, gate);
            LastBearingRouteModulePointView wreck =
                controller.World!.RouteModulePointView!;
            LastBearingWreckLineInteractor interactor =
                wreck.Interactor!;
            RoadFeelRigInstance roadRig = controller.World.RoadFeelRig!;
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            yield return null;
            yield return null;

            Assert.That(interactor.IsInputArmed, Is.True);
            Assert.That(
                interactor.CurrentStage,
                Is.EqualTo(WreckLineInteractionStage.SealRangeTank));
            Assert.That(wreck.IsFrameRailSourceVisible, Is.False);
            long moduleSequence = controller.State!.NextCommandSequence;
            Press(gamepad.buttonSouth);
            yield return null;

            AssertExactWreckLineCommand<
                OperateWreckLineModuleCommand>(
                controller,
                moduleSequence);
            Assert.That(
                ((OperateWreckLineModuleCommand)
                    PendingCommands(controller)[0]).Action,
                Is.EqualTo(
                    RouteActionKind.CrossExposedDustRoute));
            Assert.That(interactor.ActivateCurrentStage(), Is.False);
            controller.OperateWreckLineModulePoint();
            AssertExactWreckLineCommand<
                OperateWreckLineModuleCommand>(
                controller,
                moduleSequence);
            InvokeSimulationTick(controller);

            Assert.That(
                interactor.CurrentStage,
                Is.EqualTo(WreckLineInteractionStage.RecoverFrameRails));
            Assert.That(interactor.IsTargetVisible, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(wreck.IsFrameRailSourceVisible, Is.True);
            Assert.That(wreck.IsCanonicalFrameRailCargoVisible, Is.False);
            Assert.That(wreck.IsRoadFrameRailCargoVisible, Is.False);
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            Release(gamepad.buttonSouth);
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);

            byte[] sourceCanonical =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string sourceHash = controller.CanonicalHash;
            long recoverySequence = controller.State.NextCommandSequence;
            Dictionary<string, string> sourceSave =
                SnapshotSaveFiles(profileDirectory);
            Vector2 pointer = RequireUnblockedScreenPoint(
                controller,
                interactor.TargetWorldPosition,
                LastBearingWreckLineInteractor.TargetName);
            Physics.SyncTransforms();
            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.True);

            AssertExactWreckLineCommand<
                RecoverWreckLineFrameRailsCommand>(
                controller,
                recoverySequence);
            Assert.That(interactor.ActivateCurrentStage(), Is.False);
            controller.RecoverWreckLineFrameRails();
            AssertExactWreckLineCommand<
                RecoverWreckLineFrameRailsCommand>(
                controller,
                recoverySequence);
            CollectionAssert.AreEqual(
                sourceCanonical,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(controller.CanonicalHash, Is.EqualTo(sourceHash));
            Assert.That(wreck.IsFrameRailSourceVisible, Is.True);
            Assert.That(wreck.IsCanonicalFrameRailCargoVisible, Is.False);
            Assert.That(wreck.IsRoadFrameRailCargoVisible, Is.False);
            AssertSaveSnapshot(
                sourceSave,
                SnapshotSaveFiles(profileDirectory));

            InvokeSimulationTick(controller);

            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(
                controller.ReadModel!.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Vehicle));
            Assert.That(wreck.IsFrameRailSourceVisible, Is.False);
            Assert.That(wreck.IsCanonicalFrameRailCargoVisible, Is.True);
            Assert.That(wreck.IsRoadFrameRailCargoVisible, Is.True);
            Assert.That(interactor.IsTargetVisible, Is.False);
            Assert.That(
                LastBearingModeCoordinator
                    .FrameRailPresentationMassKilograms,
                Is.EqualTo(400));
            Assert.That(
                roadRig.Adapter.LastCargoMassKilograms,
                Is.EqualTo(400));
            Assert.That(
                roadRig.Vehicle.Telemetry.CargoMassKilograms,
                Is.EqualTo(400f));
            AssertSaveSnapshotChanged(
                sourceSave,
                SnapshotSaveFiles(profileDirectory));

            byte[] recoveredCanonical =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string recoveredHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            Assert.That(interactor.ActivateCurrentStage(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            controller.Load();
            yield return null;

            CollectionAssert.AreEqual(
                recoveredCanonical,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(controller.CanonicalHash, Is.EqualTo(recoveredHash));
            Assert.That(
                controller.ReadModel!.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Vehicle));
            Assert.That(wreck.IsFrameRailSourceVisible, Is.False);
            Assert.That(wreck.IsCanonicalFrameRailCargoVisible, Is.True);
            Assert.That(wreck.IsRoadFrameRailCargoVisible, Is.True);
            Assert.That(
                roadRig.Adapter.LastCargoMassKilograms,
                Is.EqualTo(400));
        }

        [UnityTest]
        public IEnumerator RecoveryControlFailsClosedAndSurvivesFourLoads()
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
            controller.enabled = false;
            string profileDirectory =
                InstallTemporarySaveAdapter(controller);
            LastBearingState recoveryReady =
                CreateFrameRailRecoveryReadyState();
            InstallControllerState(controller, recoveryReady);
            controller.Save();
            byte[] savedCanonical =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string savedHash = controller.CanonicalHash;
            Dictionary<string, string> savedFiles =
                SnapshotSaveFiles(profileDirectory);
            LastBearingWreckLineInteractor interactor =
                controller.World!.RouteModulePointView!.Interactor!;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.ReturnToTitle();
                yield return null;

                Assert.That(controller.HasActiveGame, Is.False);
                Assert.That(interactor.IsTargetVisible, Is.False);
                Assert.That(interactor.ActivateCurrentStage(), Is.False);
                Assert.That(PendingCommands(controller), Is.Empty);
                AssertSingleWreckLineTopology(controller, interactor);
                AssertSaveSnapshot(
                    savedFiles,
                    SnapshotSaveFiles(profileDirectory));

                Press(keyboard.eKey);
                Press(gamepad.buttonSouth);
                controller.Load();
                yield return null;
                yield return null;

                Assert.That(controller.HasActiveGame, Is.True);
                Assert.That(interactor.IsInputArmed, Is.False);
                Assert.That(PendingCommands(controller), Is.Empty);
                Release(keyboard.eKey);
                Release(gamepad.buttonSouth);
                yield return null;

                Assert.That(interactor.IsInputArmed, Is.True);
                Assert.That(
                    interactor.CurrentStage,
                    Is.EqualTo(
                        WreckLineInteractionStage.RecoverFrameRails));
                Assert.That(interactor.IsTargetVisible, Is.True);
                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.Driving));
                Assert.That(
                    controller.World.RouteModulePointView!.Interactor,
                    Is.SameAs(interactor));
                CollectionAssert.AreEqual(
                    savedCanonical,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                Assert.That(controller.CanonicalHash, Is.EqualTo(savedHash));
                Assert.That(PendingCommands(controller), Is.Empty);
                AssertSaveSnapshot(
                    savedFiles,
                    SnapshotSaveFiles(profileDirectory));
                AssertSingleWreckLineTopology(controller, interactor);
            }

            controller.TogglePause();
            AssertRejectedWreckLineWithoutMutation(
                controller,
                interactor,
                profileDirectory,
                "unrelated pending command");

            InstallControllerState(controller, recoveryReady);
            yield return null;
            yield return null;
            interactor =
                controller.World.RouteModulePointView!.Interactor!;
            ReplaceRuntimeReadModelOnly(
                controller,
                LastBearingReadModel.FromState(controller.State!));
            AssertRejectedWreckLineWithoutMutation(
                controller,
                interactor,
                profileDirectory,
                "stale read model");

            InstallControllerState(controller, recoveryReady);
            yield return null;
            yield return null;
            interactor =
                controller.World.RouteModulePointView!.Interactor!;
            controller.ModeCoordinator!.ClearSession();
            AssertRejectedWreckLineWithoutMutation(
                controller,
                interactor,
                profileDirectory,
                "wrong mode");
        }

        private static LastBearingState
            CreateFrameRailRecoveryReadyState()
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = DriveUntilWreckLineAvailable(
                CreateOutboundState(
                    installPatchworkSkidPlate: true,
                    module: VehicleModule.SealedRangeTank));
            LastBearingReadModel model =
                LastBearingReadModel.FromState(state);
            state = Apply(
                kernel,
                state,
                sequence =>
                    new OperateWreckLineModuleCommand(
                        sequence,
                        model.RouteActionKind));
            Assert.That(
                LastBearingReadModel.FromState(state)
                    .IsWreckLineFrameRailRecoveryAvailable,
                Is.True);
            return state;
        }

        private static void AssertExactWreckLineCommand<TCommand>(
            LastBearingGameController controller,
            long expectedSequence)
            where TCommand : LastBearingCommand
        {
            LastBearingCommand[] commands =
                PendingCommands(controller);
            Assert.That(commands, Has.Length.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<TCommand>());
            Assert.That(
                commands[0].Sequence,
                Is.EqualTo(expectedSequence));
        }

        private static void ReplaceRuntimeReadModelOnly(
            LastBearingGameController controller,
            LastBearingReadModel model)
        {
            FieldInfo? readModel =
                typeof(LastBearingGameController).GetField(
                    "_readModel",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(readModel, Is.Not.Null);
            readModel!.SetValue(controller, model);
        }

        private static void AssertRejectedWreckLineWithoutMutation(
            LastBearingGameController controller,
            LastBearingWreckLineInteractor interactor,
            string profileDirectory,
            string caseName)
        {
            byte[] canonicalBefore =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string hashBefore = controller.CanonicalHash;
            LastBearingCommand[] commandsBefore =
                PendingCommands(controller);
            Dictionary<string, string> saveBefore =
                SnapshotSaveFiles(profileDirectory);

            Assert.That(
                interactor.ActivateCurrentStage(),
                Is.False,
                caseName);
            Assert.That(
                interactor.LastInteractionRejected,
                Is.True,
                caseName);
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!),
                caseName);
            Assert.That(
                controller.CanonicalHash,
                Is.EqualTo(hashBefore),
                caseName);
            CollectionAssert.AreEqual(
                commandsBefore,
                PendingCommands(controller),
                caseName);
            AssertSaveSnapshot(
                saveBefore,
                SnapshotSaveFiles(profileDirectory));
        }

        private static void AssertSingleWreckLineTopology(
            LastBearingGameController controller,
            LastBearingWreckLineInteractor expected)
        {
            Transform root = controller.transform;
            Assert.That(
                root.GetComponentsInChildren<Camera>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                root.GetComponentsInChildren<AudioListener>(true),
                Has.Length.EqualTo(1));
            LastBearingWreckLineInteractor[] interactors =
                root.GetComponentsInChildren<
                    LastBearingWreckLineInteractor>(true);
            Assert.That(interactors, Has.Length.EqualTo(1));
            Assert.That(interactors[0], Is.SameAs(expected));
            Transform[] targets = root
                .GetComponentsInChildren<Transform>(true)
                .Where(candidate =>
                    candidate.name ==
                    LastBearingWreckLineInteractor.TargetName)
                .ToArray();
            Assert.That(targets, Has.Length.EqualTo(1));
            BoxCollider? collider =
                targets[0].GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider!.isTrigger, Is.True);
            Assert.That(
                collider.gameObject.layer,
                Is.EqualTo(
                    LastBearingWreckLineInteractor.InteractionLayer));
        }

        private static void AssertSaveSnapshotChanged(
            IReadOnlyDictionary<string, string> before,
            IReadOnlyDictionary<string, string> after)
        {
            bool changed =
                before.Count != after.Count ||
                before.Any(pair =>
                    !after.TryGetValue(
                        pair.Key,
                        out string value) ||
                    value != pair.Value);
            Assert.That(changed, Is.True);
        }
    }
}
