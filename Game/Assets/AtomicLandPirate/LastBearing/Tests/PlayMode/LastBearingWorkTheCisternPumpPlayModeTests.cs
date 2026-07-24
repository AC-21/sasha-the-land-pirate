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
    public sealed class LastBearingWorkTheCisternPumpPlayModeTests :
        InputTestFixture
    {
        private const string SceneName = "LastBearing";

        private enum PumpPath
        {
            Keyboard,
            Gamepad,
            Pointer,
        }

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
                    SceneManager.CreateScene("CisternPump_TestCleanup");
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
        public IEnumerator RouteSurvivesFourModeCyclesAndHeldInputFailsClosed()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            _ = PrepareControllerForPump(controller, pause: false);
            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingCityServiceCellInteractor interactor =
                view.Interactor!;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();

            for (var cycle = 0; cycle < 4; cycle++)
            {
                string cycleHash = controller.CanonicalHash;
                controller.OpenGarageBay();
                Assert.That(
                    interactor.IsEmergencyCisternPumpControlVisible,
                    Is.False);
                Assert.That(
                    interactor.IsEmergencyCisternPumpFocused,
                    Is.False);
                Assert.That(
                    interactor.IsEmergencyCisternPumpInputArmed,
                    Is.False);
                controller.ShowCityOverview();
                Assert.That(
                    interactor.IsEmergencyCisternPumpControlVisible,
                    Is.True);
                Assert.That(
                    interactor.IsEmergencyCisternPumpFocused,
                    Is.False);
                Assert.That(PendingCommands(controller), Is.Empty);
                Assert.That(controller.CanonicalHash, Is.EqualTo(cycleHash));
            }

            byte[] stateBefore =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string hashBefore = controller.CanonicalHash;
            long fuelBefore = controller.ReadModel!.FuelUnits;
            long waterBefore = controller.ReadModel.WaterMilli;
            int generationsBefore = GenerationCount(
                RequireProfileDirectory(controller));
            LastBearingCameraRig cameraRig =
                controller.World!.CameraRig!;
            float yawBeforeHeldRoute = cameraRig.CityYaw;

            Press(keyboard.eKey);
            controller.OpenEmergencyCisternPump();
            Assert.That(
                interactor.IsEmergencyCisternPumpFocused,
                Is.True);
            Assert.That(
                interactor.IsEmergencyCisternPumpInputArmed,
                Is.False);
            controller.PumpEmergencyCistern();
            yield return null;
            Assert.That(
                cameraRig.CityYaw,
                Is.EqualTo(yawBeforeHeldRoute).Within(0.0001f),
                "The pump's E input must not rotate the city camera.");
            InvokeInteractorUpdate(interactor);
            interactor.ClickEmergencyCisternPump();

            Assert.That(
                interactor.IsEmergencyCisternPumpInputArmed,
                Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertPreTickUnchanged(
                controller,
                stateBefore,
                hashBefore,
                fuelBefore,
                waterBefore);
            Assert.That(
                GenerationCount(RequireProfileDirectory(controller)),
                Is.EqualTo(generationsBefore));

            Release(keyboard.eKey);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(
                interactor.IsEmergencyCisternPumpInputArmed,
                Is.True);
            Assert.That(PendingCommands(controller), Is.Empty);
            controller.OpenEmergencyCisternPump();
            Assert.That(
                interactor.IsEmergencyCisternPumpInputArmed,
                Is.False,
                "Every explicit route must begin a fresh release cycle.");
            interactor.ResetLocalSelection();
            Assert.That(
                interactor.IsEmergencyCisternPumpFocused,
                Is.False);
            AssertPreTickUnchanged(
                controller,
                stateBefore,
                hashBefore,
                fuelBefore,
                waterBefore);
        }

        [UnityTest]
        public IEnumerator RotatedAdjacentLayoutKeepsPumpPointerExact()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            PrepareRotatedAdjacentControllerForPump(controller);
            LastBearingCityServiceCellInteractor interactor =
                controller.World!.CityServiceCellView!.Interactor!;
            Transform pump = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor
                    .EmergencyCisternPumpControlName);
            Transform hotShift = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor
                    .HotShiftMachineControlName);
            Collider pumpCollider = pump.GetComponent<Collider>();
            Collider hotShiftCollider = hotShift.GetComponent<Collider>();
            Assert.That(pumpCollider, Is.Not.Null);
            Assert.That(hotShiftCollider, Is.Not.Null);
            Physics.SyncTransforms();
            Assert.That(
                pumpCollider.bounds.Intersects(hotShiftCollider.bounds),
                Is.False,
                "Valid adjacent rotations must not make pump clicks choose Hot Shift.");

            controller.OpenEmergencyCisternPump();
            yield return null;
            InvokeInteractorUpdate(interactor);
            long sequence = controller.State!.NextCommandSequence;
            ActivateWorldPumpTarget(controller, interactor);
            AssertSinglePumpCommand(controller, sequence);
        }

        [UnityTest]
        public IEnumerator FreshKeyboardGamepadAndPointerCommitOneExactFill()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState source =
                PrepareControllerForPump(controller, pause: true);
            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingCityServiceCellInteractor interactor =
                view.Interactor!;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            string profileDirectory = RequireProfileDirectory(controller);
            string? acceptedHash = null;

            Assert.That(
                LastBearingBalanceV1.EmergencyCisternFuelCostUnits,
                Is.EqualTo(1));
            Assert.That(
                LastBearingBalanceV1.EmergencyCisternWaterMilli,
                Is.EqualTo(10000));

            foreach (PumpPath path in new[]
            {
                PumpPath.Keyboard,
                PumpPath.Gamepad,
                PumpPath.Pointer,
            })
            {
                InstallControllerState(controller, source);
                interactor.ResetLocalSelection();
                Assert.That(controller.CanOpenEmergencyCisternPump, Is.True);
                Assert.That(controller.CanPumpEmergencyCistern, Is.False);
                controller.OpenEmergencyCisternPump();
                Assert.That(
                    interactor.IsEmergencyCisternPumpFocused,
                    Is.True);
                Assert.That(
                    interactor.IsEmergencyCisternPumpInputArmed,
                    Is.False);

                yield return null;
                InvokeInteractorUpdate(interactor);
                Assert.That(
                    interactor.IsEmergencyCisternPumpInputArmed,
                    Is.True);
                Assert.That(controller.CanPumpEmergencyCistern, Is.True);

                byte[] stateBefore =
                    LastBearingCanonicalCodec.Encode(controller.State!);
                string hashBefore = controller.CanonicalHash;
                long fuelBefore = controller.ReadModel!.FuelUnits;
                long waterBefore = controller.ReadModel.WaterMilli;
                long sequenceBefore = controller.State!.NextCommandSequence;
                int generationsBefore =
                    GenerationCount(profileDirectory);

                switch (path)
                {
                    case PumpPath.Keyboard:
                        Press(keyboard.eKey);
                        InvokeInteractorUpdate(interactor);
                        Release(keyboard.eKey);
                        break;
                    case PumpPath.Gamepad:
                        Press(gamepad.buttonSouth);
                        InvokeInteractorUpdate(interactor);
                        Release(gamepad.buttonSouth);
                        break;
                    case PumpPath.Pointer:
                        ActivateWorldPumpTarget(controller, interactor);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                AssertSinglePumpCommand(controller, sequenceBefore);
                interactor.ClickEmergencyCisternPump();
                controller.PumpEmergencyCistern();
                AssertSinglePumpCommand(controller, sequenceBefore);
                AssertPreTickUnchanged(
                    controller,
                    stateBefore,
                    hashBefore,
                    fuelBefore,
                    waterBefore);
                Assert.That(view.IsEmergencyCisternFillVisible, Is.False);
                Assert.That(
                    GenerationCount(profileDirectory),
                    Is.EqualTo(generationsBefore));

                InvokeSimulationTick(controller);
                Assert.That(
                    controller.ReadModel!.FuelUnits,
                    Is.EqualTo(
                        fuelBefore -
                        LastBearingBalanceV1
                            .EmergencyCisternFuelCostUnits));
                Assert.That(
                    controller.ReadModel.WaterMilli,
                    Is.EqualTo(
                        waterBefore +
                        LastBearingBalanceV1
                            .EmergencyCisternWaterMilli));
                Assert.That(
                    controller.ReadModel.EmergencyCisternCharged,
                    Is.True);
                Assert.That(
                    controller.IsEmergencyCisternPumpQueued,
                    Is.False);
                Assert.That(view.IsEmergencyCisternFillVisible, Is.True);
                AssertAutosaveBoundary(
                    controller,
                    profileDirectory,
                    generationsBefore,
                    hashBefore);

                if (acceptedHash == null)
                {
                    acceptedHash = controller.CanonicalHash;
                }
                else
                {
                    Assert.That(
                        controller.CanonicalHash,
                        Is.EqualTo(acceptedHash),
                        path + " diverged from the accepted pump state");
                }
            }

            Assert.That(acceptedHash, Is.Not.Null);
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(acceptedHash));
            Assert.That(
                controller.ReadModel!.EmergencyCisternCharged,
                Is.True);
            Assert.That(
                controller.ReadModel.WaterMilli,
                Is.EqualTo(
                    source.WaterMilli +
                    LastBearingBalanceV1.EmergencyCisternWaterMilli));
            controller.ShowCityOverview();
            Assert.That(view.IsEmergencyCisternFillVisible, Is.True);
            Assert.That(
                interactor.IsEmergencyCisternPumpControlVisible,
                Is.False);
        }

        [UnityTest]
        public IEnumerator TitleModeFocusStaleAndPendingGuardsFailClosed()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState source =
                PrepareControllerForPump(controller, pause: false);
            LastBearingCityServiceCellInteractor interactor =
                controller.World!.CityServiceCellView!.Interactor!;
            string profileDirectory = RequireProfileDirectory(controller);
            int generationsBefore = GenerationCount(profileDirectory);

            byte[] sourceBytes = LastBearingCanonicalCodec.Encode(source);
            string sourceHash = controller.CanonicalHash;
            long sourceFuel = controller.ReadModel!.FuelUnits;
            long sourceWater = controller.ReadModel.WaterMilli;

            interactor.ResetLocalSelection();
            interactor.ClickEmergencyCisternPump();
            controller.PumpEmergencyCistern();
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertPreTickUnchanged(
                controller,
                sourceBytes,
                sourceHash,
                sourceFuel,
                sourceWater);

            controller.OpenGarageBay();
            controller.OpenEmergencyCisternPump();
            interactor.FocusEmergencyCisternPump();
            interactor.ClickEmergencyCisternPump();
            controller.PumpEmergencyCistern();
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertPreTickUnchanged(
                controller,
                sourceBytes,
                sourceHash,
                sourceFuel,
                sourceWater);

            controller.ShowCityOverview();
            InstallControllerState(controller, source);
            ReplaceControllerStateWithoutPresentation(controller, source);
            Assert.That(
                interactor.IsEmergencyCisternPumpControlVisible,
                Is.False);
            controller.OpenEmergencyCisternPump();
            interactor.FocusEmergencyCisternPump();
            interactor.ClickEmergencyCisternPump();
            controller.PumpEmergencyCistern();
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertPreTickUnchanged(
                controller,
                sourceBytes,
                sourceHash,
                sourceFuel,
                sourceWater);

            InstallControllerState(controller, source);
            interactor.ResetLocalSelection();
            controller.OpenEmergencyCisternPump();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(
                interactor.IsEmergencyCisternPumpInputArmed,
                Is.True);
            controller.TogglePause();
            LastBearingCommand[] unrelated = PendingCommands(controller);
            Assert.That(unrelated, Has.Length.EqualTo(1));
            Assert.That(unrelated[0], Is.TypeOf<SetPauseCommand>());
            interactor.ClickEmergencyCisternPump();
            controller.PumpEmergencyCistern();
            Assert.That(PendingCommands(controller), Has.Length.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<SetPauseCommand>());
            AssertPreTickUnchanged(
                controller,
                sourceBytes,
                sourceHash,
                sourceFuel,
                sourceWater);

            InstallControllerState(controller, source);
            controller.ReturnToTitle();
            controller.OpenEmergencyCisternPump();
            interactor.FocusEmergencyCisternPump();
            interactor.ClickEmergencyCisternPump();
            controller.PumpEmergencyCistern();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(
                GenerationCount(profileDirectory),
                Is.EqualTo(generationsBefore));
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

        private static LastBearingState PrepareControllerForPump(
            LastBearingGameController controller,
            bool pause)
        {
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);
            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            controller.CommitGaragePlan(VehicleModule.SealedRangeTank);
            InvokeSimulationTick(controller);
            controller.ShowCityOverview();
            if (pause)
            {
                controller.TogglePause();
                InvokeSimulationTick(controller);
            }

            Assert.That(controller.CanOpenEmergencyCisternPump, Is.True);
            Assert.That(controller.CanPumpEmergencyCistern, Is.False);
            Assert.That(
                controller.ReadModel!.PauseCause,
                Is.EqualTo(
                    pause
                        ? PauseCause.Explicit
                        : PauseCause.None));
            return controller.State!;
        }

        private static void PrepareRotatedAdjacentControllerForPump(
            LastBearingGameController controller)
        {
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();

            controller.SelectCityBuildingPreview(CityBuildingKind.Recycler);
            controller.PlaceCityBuildingPreview();
            InvokeSimulationTick(controller);
            controller.SelectCityBuildingPreview(CityBuildingKind.MachineShop);
            controller.PlaceCityBuildingPreview();
            InvokeSimulationTick(controller);
            controller.SelectCityBuildingPreview(
                CityBuildingKind.EmergencyStorage);
            controller.RotateCityBuildingPreview();
            controller.PlaceCityBuildingPreview();
            InvokeSimulationTick(controller);
            Assert.That(controller.ReadModel!.MachineShopPadIndex, Is.EqualTo(1));
            Assert.That(controller.ReadModel.MachineShopQuarterTurns, Is.EqualTo(0));
            Assert.That(controller.ReadModel.EmergencyStoragePadIndex, Is.EqualTo(2));
            Assert.That(controller.ReadModel.EmergencyStorageQuarterTurns, Is.EqualTo(1));

            controller.ConnectCityServiceLink();
            InvokeSimulationTick(controller);
            controller.AssignCityServiceResident(ResidentRoster.RobotResidentId);
            InvokeSimulationTick(controller);
            controller.AdvanceCityServiceSled();
            InvokeSimulationTick(controller);
            controller.AdvanceCityServiceSled();
            InvokeSimulationTick(controller);
            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            InvokeSimulationTick(controller);
            controller.ShowCityOverview();
            Assert.That(controller.CanOpenEmergencyCisternPump, Is.True);
        }

        private void InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "work-cistern-pump-" + Guid.NewGuid().ToString("N"));
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

        private string RequireProfileDirectory(
            LastBearingGameController controller)
        {
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
            var pending = pendingField!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            pending!.Clear();
            resetSnapshots!.Invoke(controller, null);
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

        private static void AssertSinglePumpCommand(
            LastBearingGameController controller,
            long expectedSequence)
        {
            LastBearingCommand[] commands = PendingCommands(controller);
            Assert.That(commands, Has.Length.EqualTo(1));
            Assert.That(
                commands[0],
                Is.TypeOf<PumpEmergencyCisternCommand>());
            Assert.That(commands[0].Sequence, Is.EqualTo(expectedSequence));
            Assert.That(controller.IsEmergencyCisternPumpQueued, Is.True);
        }

        private static void AssertPreTickUnchanged(
            LastBearingGameController controller,
            byte[] expectedState,
            string expectedHash,
            long expectedFuel,
            long expectedWater)
        {
            Assert.That(
                LastBearingCanonicalCodec.Encode(controller.State!),
                Is.EqualTo(expectedState));
            Assert.That(controller.CanonicalHash, Is.EqualTo(expectedHash));
            Assert.That(
                controller.ReadModel!.FuelUnits,
                Is.EqualTo(expectedFuel));
            Assert.That(
                controller.ReadModel.WaterMilli,
                Is.EqualTo(expectedWater));
            Assert.That(
                controller.ReadModel.EmergencyCisternCharged,
                Is.False);
        }

        private static void AssertAutosaveBoundary(
            LastBearingGameController controller,
            string profileDirectory,
            int generationsBefore,
            string hashBefore)
        {
            string currentHash = controller.CanonicalHash;
            int expectedGeneration = generationsBefore + 1;
            Assert.That(currentHash, Is.Not.EqualTo(hashBefore));
            Assert.That(
                GenerationCount(profileDirectory),
                Is.EqualTo(expectedGeneration));
            Assert.That(
                controller.SaveStatus,
                Is.EqualTo(
                    LastBearingSaveCodes.SaveOk +
                    " · generation " + expectedGeneration +
                    " · " + currentHash.Substring(0, 12)));
        }

        private static int GenerationCount(string profileDirectory)
        {
            Assert.That(Directory.Exists(profileDirectory), Is.True);
            return Directory.GetFiles(profileDirectory, "gen-*.lbg").Length;
        }

        private static void ActivateWorldPumpTarget(
            LastBearingGameController controller,
            LastBearingCityServiceCellInteractor interactor)
        {
            Transform target = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor
                    .EmergencyCisternPumpControlName);
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
    }
}
