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
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingClockHotShiftPlayModeTests :
        InputTestFixture
    {
        private const string SceneName = "LastBearing";

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
                    SceneManager.CreateScene("ClockHotShift_TestCleanup");
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
        public IEnumerator PointerDeskReturnAndGamepadQueueOneExactShift()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState source = PrepareControllerForHotShift(
                controller,
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer);
            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingCityServiceCellInteractor interactor =
                view.Interactor!;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();

            for (var cycle = 0; cycle < 4; cycle++)
            {
                string cycleHash = controller.CanonicalHash;
                controller.OpenGarageBay();
                Assert.That(interactor.IsHotShiftControlVisible, Is.False);
                controller.ShowCityOverview();
                Assert.That(interactor.IsHotShiftControlVisible, Is.True);
                Assert.That(interactor.IsHotShiftControlFocused, Is.True);
                Assert.That(controller.CanonicalHash, Is.EqualTo(cycleHash));
            }

            Assert.That(
                controller.FieldDesk!.BlocksWorldPointer(
                    new Vector2(30f, Screen.height * 0.5f)),
                Is.True);
            Assert.That(
                interactor.TryActivateAtScreenPosition(
                    new Vector2(30f, Screen.height * 0.5f)),
                Is.False);
            Assert.That(interactor.Feedback, Does.Contain("FIELD DESK"));
            Assert.That(PendingCommands(controller), Is.Empty);

            UIDocument fieldDeskDocument = controller
                .GetComponentsInChildren<UIDocument>(true)
                .Single();
            Button pauseButton = fieldDeskDocument.rootVisualElement
                .Q<Button>("pause-button");
            pauseButton.Focus();
            yield return null;
            Assert.That(
                controller.FieldDesk!.OwnsKeyboardFocus,
                Is.True);
            Press(keyboard.enterKey);
            InvokeInteractorUpdate(interactor);
            Release(keyboard.enterKey);
            Assert.That(PendingCommands(controller), Is.Empty);
            using (NavigationSubmitEvent submit =
                   NavigationSubmitEvent.GetPooled())
            {
                submit.target = pauseButton;
                pauseButton.SendEvent(submit);
            }

            Assert.That(PendingCommands(controller), Has.Length.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<SetPauseCommand>());
            pauseButton.Blur();
            InstallControllerState(controller, source);
            Assert.That(
                controller.FieldDesk!.OwnsKeyboardFocus,
                Is.False);

            Vector3 cameraPosition =
                controller.World.MainCamera!.transform.position;
            Quaternion cameraRotation =
                controller.World.MainCamera.transform.rotation;
            string sourceHash = controller.CanonicalHash;
            long sourceFuel = controller.ReadModel!.FuelUnits;
            ActivateWorldTarget(controller, interactor);
            AssertSingleHotShiftCommand(controller);
            controller.StartHotShift();
            AssertSingleHotShiftCommand(controller);
            AssertPreTickUnchanged(
                controller,
                sourceHash,
                sourceFuel);
            AssertCameraUnmoved(
                controller,
                cameraPosition,
                cameraRotation);
            InvokeSimulationTick(controller);
            string acceptedHash = controller.CanonicalHash;
            Assert.That(
                controller.ReadModel!.FuelUnits,
                Is.EqualTo(
                    sourceFuel -
                    LastBearingBalanceV1.HotShiftFuelCostUnits));

            InstallControllerState(controller, source);
            controller.StartHotShift();
            interactor.ClickHotShiftControl();
            AssertSingleHotShiftCommand(controller);
            AssertPreTickUnchanged(
                controller,
                sourceHash,
                sourceFuel);
            InvokeSimulationTick(controller);
            Assert.That(controller.CanonicalHash, Is.EqualTo(acceptedHash));

            InstallControllerState(controller, source);
            cameraPosition =
                controller.World.MainCamera.transform.position;
            cameraRotation =
                controller.World.MainCamera.transform.rotation;
            Press(keyboard.enterKey);
            InvokeGlobalShortcuts(controller);
            InvokeInteractorUpdate(interactor);
            Release(keyboard.enterKey);
            AssertSingleHotShiftCommand(controller);
            AssertPreTickUnchanged(
                controller,
                sourceHash,
                sourceFuel);
            AssertCameraUnmoved(
                controller,
                cameraPosition,
                cameraRotation);
            InvokeSimulationTick(controller);
            Assert.That(controller.CanonicalHash, Is.EqualTo(acceptedHash));

            InstallControllerState(controller, source);
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            cameraPosition =
                controller.World.MainCamera.transform.position;
            cameraRotation =
                controller.World.MainCamera.transform.rotation;
            Press(gamepad.buttonSouth);
            InvokeInteractorUpdate(interactor);
            InvokeGlobalShortcuts(controller);
            Release(gamepad.buttonSouth);
            AssertSingleHotShiftCommand(controller);
            AssertPreTickUnchanged(
                controller,
                sourceHash,
                sourceFuel);
            AssertCameraUnmoved(
                controller,
                cameraPosition,
                cameraRotation);
            InvokeSimulationTick(controller);
            Assert.That(controller.CanonicalHash, Is.EqualTo(acceptedHash));

            foreach (KeyControl cameraKey in new[]
            {
                keyboard.qKey,
                keyboard.eKey,
                keyboard.leftArrowKey,
                keyboard.rightArrowKey,
            })
            {
                InstallControllerState(controller, source);
                Press(cameraKey);
                InvokeInteractorUpdate(interactor);
                InvokeGlobalShortcuts(controller);
                Release(cameraKey);
                Assert.That(PendingCommands(controller), Is.Empty);
                AssertPreTickUnchanged(
                    controller,
                    sourceHash,
                    sourceFuel);
                Assert.That(interactor.IsHotShiftControlFocused, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator GuardsAndStallsFailClosedAtThePhysicalMachine()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState workshopSource = PrepareControllerForHotShift(
                controller,
                ColonyComposition.Mixed,
                PreparationChoice.WorkshopPush);
            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingCityServiceCellInteractor interactor =
                view.Interactor!;

            interactor.ClickHotShiftControl();
            AssertSingleHotShiftCommand(controller);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.IsHotShiftStalledByWorkshopPush,
                Is.True);
            Assert.That(view.IsHumanOperatorVisible, Is.False);
            Assert.That(view.IsRobotOperatorVisible, Is.False);
            Assert.That(view.IsHotShiftSpindleMoving, Is.False);
            Assert.That(view.IsHotShiftWorkPoolVisible, Is.False);
            Assert.That(
                view.IsWorkshopPushTransferArmVisible,
                Is.True);
            Assert.That(view.IsDustFrontShutterVisible, Is.False);
            Assert.That(view.HotShiftSledProgress, Is.EqualTo(0f));
            interactor.ClickHotShiftControl();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(interactor.LastInteractionRejected, Is.True);

            LastBearingState civicSource = PrepareControllerForHotShift(
                controller,
                ColonyComposition.HumanOnly,
                PreparationChoice.CivicBuffer);
            controller.OpenGarageBay();
            Assert.That(interactor.IsHotShiftControlVisible, Is.False);
            interactor.ClickHotShiftControl();
            Assert.That(PendingCommands(controller), Is.Empty);

            controller.ShowCityOverview();
            ReplaceControllerStateWithoutPresentation(
                controller,
                civicSource);
            interactor.ClickHotShiftControl();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(interactor.LastInteractionRejected, Is.True);

            InstallControllerState(controller, civicSource);
            controller.TogglePause();
            LastBearingCommand[] unrelatedPending =
                PendingCommands(controller);
            Assert.That(unrelatedPending, Has.Length.EqualTo(1));
            Assert.That(
                unrelatedPending[0],
                Is.TypeOf<SetPauseCommand>());
            interactor.ClickHotShiftControl();
            Assert.That(PendingCommands(controller), Has.Length.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<SetPauseCommand>());

            InstallControllerState(controller, civicSource);
            controller.StartHotShift();
            InvokeSimulationTick(controller);
            controller.TogglePause();
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.PauseCause,
                Is.EqualTo(PauseCause.Explicit));
            Assert.That(view.IsHotShiftSpindleMoving, Is.False);
            Assert.That(view.IsHotShiftWorkPoolVisible, Is.False);
            interactor.FocusHotShiftControl();
            Assert.That(
                interactor.HotShiftMachineLabel,
                Does.Contain("PAUSED"));
            Assert.That(interactor.Feedback, Does.Contain("PAUSED"));
            controller.TogglePause();
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.PauseCause,
                Is.EqualTo(PauseCause.None));
            Assert.That(view.IsHotShiftSpindleMoving, Is.True);
            LastBearingState dustStalled =
                CreateAcknowledgedDustFrontBreach(controller.State!);
            InstallControllerState(controller, dustStalled);
            Assert.That(
                controller.ReadModel!.IsHotShiftStalledByDustFront,
                Is.True);
            Assert.That(view.IsHumanOperatorVisible, Is.True);
            Assert.That(view.IsRobotOperatorVisible, Is.False);
            Assert.That(view.IsHotShiftSpindleMoving, Is.False);
            Assert.That(view.IsHotShiftWorkPoolVisible, Is.False);
            Assert.That(
                view.IsWorkshopPushTransferArmVisible,
                Is.False);
            Assert.That(view.IsDustFrontShutterVisible, Is.True);
            string dustHash = controller.CanonicalHash;
            interactor.ClickHotShiftControl();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(controller.CanonicalHash, Is.EqualTo(dustHash));
        }

        [UnityTest]
        public IEnumerator ColonyOperatorsActiveSaveLoadAndCompletionReproject()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;

            foreach (ColonyComposition composition in new[]
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            })
            {
                _ = PrepareControllerForHotShift(
                    controller,
                    composition,
                    PreparationChoice.CivicBuffer);
                string? residentId =
                    controller.ReadModel!.CityServiceResidentId;
                controller.StartHotShift();
                InvokeSimulationTick(controller);

                Assert.That(
                    controller.ReadModel.IsHotShiftActivelyWorking,
                    Is.True);
                Assert.That(view.IsHotShiftSpindleMoving, Is.True);
                Assert.That(view.IsHotShiftWorkPoolVisible, Is.True);
                Assert.That(view.IsDustFrontShutterVisible, Is.False);
                Assert.That(
                    view.IsWorkshopPushTransferArmVisible,
                    Is.False);
                Assert.That(
                    view.IsHumanOperatorVisible,
                    Is.EqualTo(
                        string.Equals(
                            residentId,
                            ResidentRoster.HumanResidentId,
                            StringComparison.Ordinal)));
                Assert.That(
                    view.IsRobotOperatorVisible,
                    Is.EqualTo(
                        string.Equals(
                            residentId,
                            ResidentRoster.RobotResidentId,
                            StringComparison.Ordinal)));
            }

            _ = PrepareControllerForHotShift(
                controller,
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer);
            long startingParts = controller.ReadModel!.PartsUnits;
            long startingFuel = controller.ReadModel.FuelUnits;
            controller.StartHotShift();
            InvokeSimulationTick(controller);
            long waterBeforeActiveTick =
                controller.ReadModel.WaterMilli;
            long activeTrend =
                controller.ReadModel
                    .WaterTrendMilliPerSettlementTick;
            Quaternion spindleAtStart =
                view.HotShiftSpindleLocalRotation;
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.WaterMilli,
                Is.EqualTo(waterBeforeActiveTick + activeTrend));
            Assert.That(
                Quaternion.Angle(
                    spindleAtStart,
                    view.HotShiftSpindleLocalRotation),
                Is.GreaterThan(0.1f));

            for (var tick = 0; tick < 4; tick++)
            {
                InvokeSimulationTick(controller);
            }

            string activeHash = controller.CanonicalHash;
            long activeElapsed =
                controller.ReadModel.HotShiftElapsedTicks;
            float activeSledProgress = view.HotShiftSledProgress;
            controller.Save();
            controller.ReturnToTitle();
            Assert.That(view.IsVisible, Is.False);
            controller.Load();
            yield return null;

            Assert.That(controller.CanonicalHash, Is.EqualTo(activeHash));
            Assert.That(
                controller.ReadModel!.HotShiftElapsedTicks,
                Is.EqualTo(activeElapsed));
            Assert.That(view.IsHotShiftSpindleMoving, Is.True);
            Assert.That(view.IsHotShiftWorkPoolVisible, Is.True);
            Assert.That(
                view.HotShiftSledProgress,
                Is.EqualTo(activeSledProgress).Within(0.0001f));
            Assert.That(
                controller.ReadModel.FuelUnits,
                Is.EqualTo(
                    startingFuel -
                    LastBearingBalanceV1.HotShiftFuelCostUnits));

            var guard = 0;
            while (controller.ReadModel.HotShiftPhase ==
                       HotShiftPhase.InProgress &&
                   guard <
                       LastBearingBalanceV1
                           .HotShiftRequiredSettlementTicks + 2)
            {
                InvokeSimulationTick(controller);
                guard++;
            }

            Assert.That(
                controller.ReadModel.HotShiftPhase,
                Is.EqualTo(HotShiftPhase.Idle));
            Assert.That(
                controller.ReadModel.HotShiftCompletedCount,
                Is.EqualTo(1));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(
                    startingParts +
                    LastBearingBalanceV1.HotShiftOutputPartsUnits));
            Assert.That(view.IsHotShiftSpindleMoving, Is.False);
            Assert.That(view.IsHotShiftWorkPoolVisible, Is.False);
            Assert.That(
                view.IsHotShiftCompletionWitnessVisible,
                Is.True);
            Assert.That(view.HotShiftSledProgress, Is.EqualTo(1f));
            Assert.That(
                view.Interactor!.IsHotShiftControlVisible,
                Is.True);
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

        private static LastBearingState PrepareControllerForHotShift(
            LastBearingGameController controller,
            ColonyComposition composition,
            PreparationChoice preparation)
        {
            controller.StartNewGame(composition);
            controller.InspectCityNeed();
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);
            controller.BeginGaragePlan(preparation);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            InvokeSimulationTick(controller);
            controller.ShowCityOverview();

            Assert.That(controller.CanStartHotShift, Is.True);
            Assert.That(
                controller.ReadModel!.CityDeliveryStage,
                Is.EqualTo(
                    CityDeliveryStage.DeliveredToWorkshop));
            return controller.State!;
        }

        private void InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "clock-hot-shift-" + Guid.NewGuid().ToString("N"));
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

        private static LastBearingState CreateAcknowledgedDustFrontBreach(
            LastBearingState state)
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
            FieldInfo? progress = builderType.GetField(
                "DustFrontProgressTicks",
                flags);
            FieldInfo? water = builderType.GetField(
                "WaterMilli",
                flags);
            MethodInfo? build = builderType.GetMethod("Build", flags);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(progress, Is.Not.Null);
            Assert.That(water, Is.Not.Null);
            Assert.That(build, Is.Not.Null);

            object builder = constructor!.Invoke(new object[] { state });
            progress!.SetValue(
                builder,
                LastBearingBalanceV1
                    .DustFrontThresholdCrisisTicks - 1);
            water!.SetValue(builder, 0L);
            var primed = build!.Invoke(builder, null) as
                LastBearingState;
            Assert.That(primed, Is.Not.Null);

            var kernel = new LastBearingKernel();
            LastBearingState breached = kernel.Step(
                primed!,
                Array.Empty<LastBearingCommand>()).State;
            Assert.That(
                breached.DustFrontOutcome,
                Is.EqualTo(DustFrontOutcome.Breached));
            LastBearingState acknowledged = kernel.Step(
                breached,
                new LastBearingCommand[]
                {
                    new AcknowledgeDustFrontCommand(
                        breached.NextCommandSequence),
                }).State;
            Assert.That(
                LastBearingReadModel.FromState(acknowledged)
                    .IsHotShiftStalledByDustFront,
                Is.True);
            return acknowledged;
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

        private static void InvokeInteractorUpdate(
            LastBearingCityServiceCellInteractor interactor)
        {
            MethodInfo? update =
                typeof(LastBearingCityServiceCellInteractor).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update!.Invoke(interactor, null);
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

        private static void AssertSingleHotShiftCommand(
            LastBearingGameController controller)
        {
            LastBearingCommand[] commands = PendingCommands(controller);
            Assert.That(commands, Has.Length.EqualTo(1));
            Assert.That(commands[0], Is.TypeOf<RunHotShiftCommand>());
            Assert.That(controller.IsHotShiftStartQueued, Is.True);
        }

        private static void AssertPreTickUnchanged(
            LastBearingGameController controller,
            string expectedHash,
            long expectedFuel)
        {
            Assert.That(controller.CanonicalHash, Is.EqualTo(expectedHash));
            Assert.That(controller.ReadModel!.FuelUnits, Is.EqualTo(expectedFuel));
            Assert.That(
                controller.ReadModel.HotShiftPhase,
                Is.EqualTo(HotShiftPhase.Idle));
            Assert.That(
                controller.ReadModel.HotShiftCompletedCount,
                Is.Zero);
        }

        private static void ActivateWorldTarget(
            LastBearingGameController controller,
            LastBearingCityServiceCellInteractor interactor)
        {
            Transform target = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor
                    .HotShiftMachineControlName);
            Vector3 screen =
                controller.World!.MainCamera!.WorldToScreenPoint(
                    target.position);
            var pointer = new Vector2(screen.x, screen.y);
            Assert.That(screen.z, Is.GreaterThan(0f));
            Assert.That(
                screen.x,
                Is.InRange(0f, (float)Screen.width));
            Assert.That(
                screen.y,
                Is.InRange(0f, (float)Screen.height));
            Assert.That(
                controller.FieldDesk!.BlocksWorldPointer(pointer),
                Is.False);
            Physics.SyncTransforms();
            Assert.That(
                interactor.TryActivateAtScreenPosition(pointer),
                Is.True);
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
            Transform camera =
                controller.World!.MainCamera!.transform;
            Assert.That(
                Vector3.Distance(camera.position, expectedPosition),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(camera.rotation, expectedRotation),
                Is.LessThan(0.0001f));
        }
    }
}
