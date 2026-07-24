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
    public sealed class LastBearingFeelTheLoadPlayModeTests :
        InputTestFixture
    {
        private const string SceneName = "LastBearing";
        private const string TransactionId =
            "transaction:last-bearing:feel-the-load:0001";
        private const string TransactionFingerprint =
            "fingerprint:last-bearing:feel-the-load:0001";

        private readonly List<string> _temporarySaveRoots =
            new List<string>();
        private LastBearingGameController? _controller;
        private string? _profileDirectory;

        private enum CommitPath
        {
            Pointer = 0,
            Keyboard = 1,
            Gamepad = 2,
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene(
                    "FeelTheLoad_TestCleanup");
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
        public IEnumerator PhysicalInputsQueueExactPlanPairAndApplyOnOneTick()
        {
            yield return BootController();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

            yield return ExerciseCommitPath(
                ColonyComposition.HumanOnly,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                CommitPath.Pointer,
                keyboard,
                gamepad);
            yield return ExerciseCommitPath(
                ColonyComposition.RobotOnly,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                CommitPath.Keyboard,
                keyboard,
                gamepad);
            yield return ExerciseCommitPath(
                ColonyComposition.Mixed,
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank,
                CommitPath.Gamepad,
                keyboard,
                gamepad);
        }

        [UnityTest]
        public IEnumerator HeldEntryAndInvalidStatesFailClosed()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

            PrepareControllerForGaragePlan(
                controller,
                ColonyComposition.HumanOnly);
            Press(keyboard.eKey);
            Press(gamepad.buttonSouth);
            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);
            yield return null;

            LastBearingGarageModuleInteractor interactor =
                RequireInteractor(controller);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(
                interactor.FocusModule(VehicleModule.WinchAssembly),
                Is.True);
            Assert.That(
                interactor.FocusedModule,
                Is.EqualTo(VehicleModule.WinchAssembly));
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            yield return null;
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            Release(keyboard.eKey);
            Release(gamepad.buttonSouth);
            yield return null;
            Assert.That(interactor.IsInputArmed, Is.True);
            Press(keyboard.eKey);
            yield return null;
            Release(keyboard.eKey);
            yield return null;
            AssertOrderedPlanComposite(
                controller,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);

            PrepareControllerForGaragePlan(
                controller,
                ColonyComposition.Mixed);
            LastBearingState uncommitted = controller.State!;
            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            yield return null;
            interactor = RequireInteractor(controller);

            controller.ShowCityOverview();
            yield return null;
            AssertRejectedWithoutMutation(
                controller,
                () => interactor.ActivateModule(
                    VehicleModule.SealedRangeTank),
                "wrong presentation mode");

            controller.OpenGarageBay();
            yield return null;
            controller.CancelGaragePlan();
            controller.OpenGarageBay();
            yield return null;
            Assert.That(interactor.IsWinchTargetVisible, Is.False);
            Assert.That(interactor.IsRangeTankTargetVisible, Is.False);
            AssertRejectedWithoutMutation(
                controller,
                () => interactor.ActivateModule(
                    VehicleModule.WinchAssembly),
                "no local preparation plan");

            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            yield return null;
            interactor = RequireInteractor(controller);
            ReplaceRuntimeReadModelOnly(
                controller,
                LastBearingReadModel.FromState(controller.State!));
            AssertRejectedWithoutMutation(
                controller,
                () => interactor.ActivateModule(
                    VehicleModule.SealedRangeTank),
                "stale derived model");
            ApplyPresentation(controller);

            interactor = RequireInteractor(controller);
            controller.TogglePause();
            Assert.That(PendingCommands(controller), Has.Length.EqualTo(1));
            AssertRejectedWithoutMutation(
                controller,
                () => interactor.ActivateModule(
                    VehicleModule.SealedRangeTank),
                "unrelated pending command");

            InstallControllerState(
                controller,
                ApplyPlanCommands(
                    uncommitted,
                    PreparationChoice.WorkshopPush,
                    VehicleModule.WinchAssembly));
            controller.OpenGarageBay();
            yield return null;
            interactor = RequireInteractor(controller);
            AssertRejectedWithoutMutation(
                controller,
                () => interactor.ActivateModule(
                    VehicleModule.SealedRangeTank),
                "canonical module already selected");

            InstallControllerState(
                controller,
                CreateOutboundState(uncommitted));
            controller.OpenGarageBay();
            yield return null;
            interactor = RequireInteractor(controller);
            AssertRejectedWithoutMutation(
                controller,
                () => interactor.ActivateModule(
                    VehicleModule.WinchAssembly),
                "Sasha already away");
        }

        [UnityTest]
        public IEnumerator FourCityGarageCyclesKeepOnePhysicalTopology()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            PrepareControllerForGaragePlan(
                controller,
                ColonyComposition.RobotOnly);
            controller.Save();

            byte[] canonicalBefore =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string hashBefore = controller.CanonicalHash;
            long fuelBefore = controller.ReadModel!.FuelUnits;
            string saveStatusBefore = controller.SaveStatus;
            Dictionary<string, string> saveFilesBefore =
                SnapshotSaveFiles(_profileDirectory!);

            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            yield return null;
            LastBearingGarageModuleInteractor interactor =
                RequireInteractor(controller);

            for (var cycle = 0; cycle < 4; cycle++)
            {
                if (cycle != 0)
                {
                    controller.OpenGarageBay();
                    yield return null;
                }

                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.GarageBay));
                Assert.That(RequireInteractor(controller), Is.SameAs(interactor));
                Assert.That(interactor.IsWinchTargetVisible, Is.True);
                Assert.That(interactor.IsRangeTankTargetVisible, Is.True);
                Assert.That(
                    interactor.FocusedModule,
                    Is.EqualTo(VehicleModule.None));
                AssertSingleGarageModuleTopology(controller, interactor);
                AssertNoCanonicalOrSaveMutation(
                    controller,
                    canonicalBefore,
                    hashBefore,
                    fuelBefore,
                    saveStatusBefore,
                    saveFilesBefore);

                controller.ShowCityOverview();
                yield return null;

                Assert.That(
                    controller.ModeCoordinator.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.CityOverview));
                Assert.That(interactor.IsWinchTargetVisible, Is.False);
                Assert.That(interactor.IsRangeTankTargetVisible, Is.False);
                Assert.That(RequireInteractor(controller), Is.SameAs(interactor));
                AssertSingleGarageModuleTopology(controller, interactor);
                AssertNoCanonicalOrSaveMutation(
                    controller,
                    canonicalBefore,
                    hashBefore,
                    fuelBefore,
                    saveStatusBefore,
                    saveFilesBefore);
            }

            Assert.That(controller.IsGaragePlanIntentActive, Is.True);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.CivicBuffer));
        }

        private IEnumerator ExerciseCommitPath(
            ColonyComposition composition,
            PreparationChoice preparation,
            VehicleModule module,
            CommitPath path,
            Keyboard keyboard,
            Gamepad gamepad)
        {
            LastBearingGameController controller = _controller!;
            PrepareControllerForGaragePlan(controller, composition);
            controller.Save();

            byte[] canonicalBefore =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string hashBefore = controller.CanonicalHash;
            long fuelBefore = controller.ReadModel!.FuelUnits;
            long tickBefore = controller.ReadModel.GlobalTick;
            string saveStatusBefore = controller.SaveStatus;
            Dictionary<string, string> saveFilesBefore =
                SnapshotSaveFiles(_profileDirectory!);

            controller.BeginGaragePlan(preparation);
            yield return null;
            yield return null;
            LastBearingGarageModuleInteractor interactor =
                RequireInteractor(controller);
            Assert.That(interactor.IsBuilt, Is.True);
            Assert.That(interactor.HasDedicatedInteractionTargets, Is.True);
            Assert.That(interactor.IsInputArmed, Is.True);
            Assert.That(
                interactor.FocusedModule,
                Is.EqualTo(VehicleModule.None));
            Assert.That(interactor.IsWinchTargetVisible, Is.True);
            Assert.That(interactor.IsRangeTankTargetVisible, Is.True);
            RequireUnblockedScreenPoint(
                controller,
                interactor.WinchTargetWorldPosition,
                "winch module station");
            RequireUnblockedScreenPoint(
                controller,
                interactor.RangeTankTargetWorldPosition,
                "range tank module station");
            RequireUnblockedScreenPoint(
                controller,
                RequireNamedTransform(
                    controller.transform,
                    LastBearingGarageModuleInteractor.FeedbackLabelName)
                    .position,
                "garage module feedback");

            if (path == CommitPath.Pointer)
            {
                Assert.That(
                    ActivateWorldTarget(controller, interactor, module),
                    Is.True);
            }
            else
            {
                if (path == CommitPath.Keyboard)
                {
                    Press(keyboard.rightArrowKey);
                    yield return null;
                    Release(keyboard.rightArrowKey);
                    yield return null;
                    Press(keyboard.rightArrowKey);
                    yield return null;
                    Release(keyboard.rightArrowKey);
                    yield return null;
                }
                else
                {
                    Press(gamepad.dpad.right);
                    yield return null;
                    Release(gamepad.dpad.right);
                    yield return null;
                    Press(gamepad.dpad.right);
                    yield return null;
                    Release(gamepad.dpad.right);
                    yield return null;
                }

                Assert.That(interactor.FocusedModule, Is.EqualTo(module));
                Assert.That(
                    module == VehicleModule.WinchAssembly
                        ? interactor.IsWinchHighlighted
                        : interactor.IsRangeTankHighlighted,
                    Is.True);
                if (path == CommitPath.Keyboard)
                {
                    Press(keyboard.eKey);
                    yield return null;
                    Release(keyboard.eKey);
                    yield return null;
                }
                else
                {
                    Press(gamepad.buttonSouth);
                    yield return null;
                    Release(gamepad.buttonSouth);
                    yield return null;
                }
            }

            AssertOrderedPlanComposite(controller, preparation, module);
            Assert.That(interactor.QueuedModule, Is.EqualTo(module));
            Assert.That(
                module == VehicleModule.WinchAssembly
                    ? interactor.IsWinchHighlighted
                    : interactor.IsRangeTankHighlighted,
                Is.True);
            Assert.That(interactor.IsWinchTargetVisible, Is.True);
            Assert.That(interactor.IsRangeTankTargetVisible, Is.True);

            controller.CommitGaragePlan(module);
            AssertOrderedPlanComposite(controller, preparation, module);
            Assert.That(
                interactor.ActivateModule(module),
                Is.False,
                "a same-frame fallback must not append another pair");
            AssertOrderedPlanComposite(controller, preparation, module);

            Assert.That(controller.ReadModel!.GlobalTick, Is.EqualTo(tickBefore));
            AssertNoCanonicalOrSaveMutation(
                controller,
                canonicalBefore,
                hashBefore,
                fuelBefore,
                saveStatusBefore,
                saveFilesBefore,
                expectedPendingCount: 2);

            InvokeSimulationTick(controller);

            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(
                controller.ReadModel!.Composition,
                Is.EqualTo(composition));
            Assert.That(
                controller.ReadModel.PreparationChoice,
                Is.EqualTo(preparation));
            Assert.That(
                controller.ReadModel.PlannedModule,
                Is.EqualTo(module));
            Assert.That(
                controller.ReadModel.PreparationPhase,
                Is.EqualTo(PreparationPhase.Preparing));
            Assert.That(
                controller.State!.ModuleInstallationState,
                Is.EqualTo(ModuleInstallationState.Pending));
            Assert.That(
                controller.ReadModel.FuelUnits,
                Is.EqualTo(
                    fuelBefore -
                    PreparationFuelCost(preparation) -
                    ModuleInstallFuelCost(module)));
            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(
                interactor.QueuedModule,
                Is.EqualTo(VehicleModule.None));
            Assert.That(interactor.IsWinchTargetVisible, Is.False);
            Assert.That(interactor.IsRangeTankTargetVisible, Is.False);
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

        private static void PrepareControllerForGaragePlan(
            LastBearingGameController controller,
            ColonyComposition composition)
        {
            controller.StartNewGame(composition);
            controller.InspectCityNeed();
            CompleteDistrictObservation(controller, clear: true);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);

            Assert.That(controller.ReadModel, Is.Not.Null);
            Assert.That(
                controller.ReadModel!.SliceInfrastructureActive,
                Is.True);
            Assert.That(
                controller.ReadModel.PreparationChoice,
                Is.EqualTo(PreparationChoice.Unselected));
            Assert.That(
                controller.ReadModel.PlannedModule,
                Is.EqualTo(VehicleModule.None));
            Assert.That(PendingCommands(controller), Is.Empty);
        }

        private static void CompleteDistrictObservation(
            LastBearingGameController controller,
            bool clear)
        {
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear);
        }

        private static LastBearingState ApplyPlanCommands(
            LastBearingState state,
            PreparationChoice preparation,
            VehicleModule module)
        {
            var kernel = new LastBearingKernel();
            long sequence = state.NextCommandSequence;
            return kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new SelectPreparationCommand(
                        sequence,
                        preparation,
                        module),
                    new InstallVehicleModuleCommand(
                        sequence + 1,
                        module),
                }).State;
        }

        private static LastBearingState CreateOutboundState(
            LastBearingState readyForPlan)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = ApplyPlanCommands(
                readyForPlan,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            var guard = 0;
            while ((state.PreparationPhase != PreparationPhase.Ready ||
                    state.ModuleInstallationState !=
                        ModuleInstallationState.Installed) &&
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
            long sequence = state.NextCommandSequence;
            state = kernel.Step(
                state,
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
            Assert.That(
                state.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
            return state;
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

        private static void AssertOrderedPlanComposite(
            LastBearingGameController controller,
            PreparationChoice preparation,
            VehicleModule module)
        {
            LastBearingCommand[] commands = PendingCommands(controller);
            long firstSequence = controller.State!.NextCommandSequence;
            Assert.That(commands, Has.Length.EqualTo(2));
            Assert.That(
                commands[0],
                Is.TypeOf<SelectPreparationCommand>());
            Assert.That(
                commands[1],
                Is.TypeOf<InstallVehicleModuleCommand>());
            var select = (SelectPreparationCommand)commands[0];
            var install = (InstallVehicleModuleCommand)commands[1];
            Assert.That(select.Sequence, Is.EqualTo(firstSequence));
            Assert.That(select.Choice, Is.EqualTo(preparation));
            Assert.That(select.PlannedModule, Is.EqualTo(module));
            Assert.That(install.Sequence, Is.EqualTo(firstSequence + 1));
            Assert.That(install.Module, Is.EqualTo(module));
        }

        private static void AssertRejectedWithoutMutation(
            LastBearingGameController controller,
            Func<bool> operation,
            string caseName)
        {
            byte[] canonicalBefore =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string hashBefore = controller.CanonicalHash;
            long fuelBefore = controller.ReadModel!.FuelUnits;
            LastBearingCommand[] pendingBefore = PendingCommands(controller);

            Assert.That(operation(), Is.False, caseName);

            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!),
                caseName);
            Assert.That(
                controller.CanonicalHash,
                Is.EqualTo(hashBefore),
                caseName);
            Assert.That(
                controller.ReadModel!.FuelUnits,
                Is.EqualTo(fuelBefore),
                caseName);
            CollectionAssert.AreEqual(
                pendingBefore,
                PendingCommands(controller),
                caseName);
        }

        private static bool ActivateWorldTarget(
            LastBearingGameController controller,
            LastBearingGarageModuleInteractor interactor,
            VehicleModule module)
        {
            Vector3 worldPosition =
                module == VehicleModule.WinchAssembly
                    ? interactor.WinchTargetWorldPosition
                    : interactor.RangeTankTargetWorldPosition;
            Vector2 pointer = RequireUnblockedScreenPoint(
                controller,
                worldPosition,
                module.ToString());
            Physics.SyncTransforms();
            return interactor.TryActivateAtScreenPosition(pointer);
        }

        private static Vector2 RequireUnblockedScreenPoint(
            LastBearingGameController controller,
            Vector3 worldPosition,
            string context)
        {
            Camera camera = controller.World!.MainCamera!;
            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            var pointer = new Vector2(screen.x, screen.y);
            Assert.That(screen.z, Is.GreaterThan(0f), context);
            Assert.That(
                screen.x,
                Is.InRange(0f, (float)Screen.width),
                context);
            Assert.That(
                screen.y,
                Is.InRange(0f, (float)Screen.height),
                context);
            Assert.That(
                controller.FieldDesk!.BlocksWorldPointer(pointer),
                Is.False,
                context);
            Assert.That(
                controller.Hud!.BlocksWorldPointer(pointer),
                Is.False,
                context);
            return pointer;
        }

        private static LastBearingGarageModuleInteractor RequireInteractor(
            LastBearingGameController controller)
        {
            LastBearingGarageModuleInteractor? interactor =
                controller.World?.GarageModuleInteractor;
            Assert.That(interactor, Is.Not.Null);
            return interactor!;
        }

        private static void AssertSingleGarageModuleTopology(
            LastBearingGameController controller,
            LastBearingGarageModuleInteractor expected)
        {
            Transform root = controller.transform;
            Assert.That(
                root.GetComponentsInChildren<Camera>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                root.GetComponentsInChildren<
                    LastBearingGarageModuleInteractor>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                root.GetComponentInChildren<
                    LastBearingGarageModuleInteractor>(true),
                Is.SameAs(expected));
            Assert.That(expected.HasDedicatedInteractionTargets, Is.True);
            AssertNamedTrigger(
                root,
                LastBearingGarageModuleInteractor.WinchTargetName);
            AssertNamedTrigger(
                root,
                LastBearingGarageModuleInteractor.RangeTankTargetName);
        }

        private static void AssertNamedTrigger(
            Transform root,
            string targetName)
        {
            Transform target = RequireNamedTransform(root, targetName);
            BoxCollider? collider =
                target.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null, targetName);
            Assert.That(collider!.isTrigger, Is.True, targetName);
            Assert.That(
                collider.gameObject.layer,
                Is.EqualTo(
                    LastBearingGarageModuleInteractor.InteractionLayer),
                targetName);
        }

        private static Transform RequireNamedTransform(
            Transform root,
            string targetName)
        {
            Transform[] matches = root
                .GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == targetName)
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), targetName);
            return matches[0];
        }

        private void AssertNoCanonicalOrSaveMutation(
            LastBearingGameController controller,
            byte[] canonicalBefore,
            string hashBefore,
            long fuelBefore,
            string saveStatusBefore,
            IReadOnlyDictionary<string, string> saveFilesBefore,
            int expectedPendingCount = 0)
        {
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!));
            Assert.That(controller.CanonicalHash, Is.EqualTo(hashBefore));
            Assert.That(
                controller.ReadModel!.FuelUnits,
                Is.EqualTo(fuelBefore));
            Assert.That(controller.SaveStatus, Is.EqualTo(saveStatusBefore));
            Assert.That(
                PendingCommands(controller),
                Has.Length.EqualTo(expectedPendingCount));
            AssertSaveSnapshot(
                saveFilesBefore,
                SnapshotSaveFiles(_profileDirectory!));
        }

        private string InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "feel-the-load-" + Guid.NewGuid().ToString("N"));
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
                    path => Convert.ToBase64String(File.ReadAllBytes(path)),
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

        private static long PreparationFuelCost(
            PreparationChoice preparation)
        {
            return preparation == PreparationChoice.WorkshopPush
                ? LastBearingBalanceV1.WorkshopPreparationFuelUnits
                : LastBearingBalanceV1.CivicBufferPreparationFuelUnits;
        }

        private static long ModuleInstallFuelCost(VehicleModule module)
        {
            return module == VehicleModule.WinchAssembly
                ? LastBearingBalanceV1.WinchInstallFuelUnits
                : LastBearingBalanceV1.TankInstallFuelUnits;
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
