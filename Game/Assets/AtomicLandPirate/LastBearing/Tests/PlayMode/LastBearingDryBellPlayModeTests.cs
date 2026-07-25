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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingDryBellPlayModeTests
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
                    SceneManager.CreateScene("DryBell_TestCleanup");
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
        public IEnumerator HomeDryBellPreservesCheckpointAndLocksGameplay()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingProfileStore store =
                InstallTemporarySaveAdapter(controller);
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.AssignRoadHand(ResidentRoster.HumanResidentId);
            LastBearingState edge =
                CreateHotShiftCheckpointDryEdge(controller.State!);
            InstallControllerState(controller, edge);

            byte[] safeBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            for (var cycle = 0; cycle < 3; cycle++)
            {
                controller.OpenGarageBay();
                controller.ShowCityOverview();
                Assert.That(
                    LastBearingCanonicalCodec.Encode(controller.State!),
                    Is.EqualTo(safeBytes));
            }

            controller.Save();
            LastBearingLoadResult protectedCheckpoint = Load(store);
            Assert.That(protectedCheckpoint.Generation, Is.EqualTo(1UL));
            Assert.That(
                protectedCheckpoint.CanonicalPayload,
                Is.EqualTo(safeBytes));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));

            InvokeSimulationTick(controller);

            Assert.That(controller.IsSettlementLost, Is.True);
            Assert.That(controller.ReadModel!.WaterMilli, Is.Zero);
            Assert.That(
                controller.ReadModel.SettlementLossReason,
                Is.EqualTo(
                    LastBearingReadModel.DryBellSettlementLossReason));
            Assert.That(controller.Status, Is.EqualTo(
                LastBearingGameController.SettlementLossStatus));
            Assert.That(controller.SaveStatus, Is.EqualTo(
                LastBearingGameController.SettlementLossSaveStatus));
            Assert.That(controller.HasPendingPlayerCommands, Is.False);

            LastBearingFieldDeskProjection projection =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                projection.PermitJob.Headline,
                Is.EqualTo("The settlement went dry"));
            Assert.That(
                projection.Pressure,
                Is.EqualTo("SETTLEMENT LOST · CURRENT MODE HELD"));
            AssertRecoveryAction(
                projection.PrimaryAction,
                LastBearingFieldDeskIntent.Load,
                "LOAD PROTECTED CHECKPOINT",
                enabled: true);
            AssertRecoveryAction(
                projection.SecondaryAction,
                LastBearingFieldDeskIntent.StartNewColony,
                "NEW COLONY · SAME ROSTER",
                enabled: true);
            AssertRecoveryAction(
                projection.SaveAction,
                LastBearingFieldDeskIntent.Save,
                "SAVE DISABLED",
                enabled: false);
            AssertRecoveryAction(
                projection.TitleAction,
                LastBearingFieldDeskIntent.ReturnToTitle,
                "TITLE",
                enabled: true);
            Assert.That(projection.Survey.IsVisible, Is.False);
            LastBearingFieldDeskActionProjection[] deskActions =
            {
                projection.PrimaryAction,
                projection.SecondaryAction,
                projection.PauseAction,
                projection.SaveAction,
                projection.LoadAction,
                projection.TitleAction,
            };
            Assert.That(
                deskActions.Count(action => action.IsEnabled),
                Is.EqualTo(3));
            Assert.That(projection.PauseAction.IsVisible, Is.False);
            Assert.That(projection.LoadAction.IsVisible, Is.False);

            byte[] lossBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            LastBearingPresentationMode heldMode =
                controller.ModeCoordinator.CurrentMode;
            controller.TogglePause();
            controller.StartHotShift();
            controller.OpenGarageBay();
            InvokeSimulationTick(controller);
            Assert.That(controller.HasPendingPlayerCommands, Is.False);
            Assert.That(
                LastBearingCanonicalCodec.Encode(controller.State!),
                Is.EqualTo(lossBytes));
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(heldMode));

            controller.Save();
            LastBearingLoadResult afterManualSave = Load(store);
            Assert.That(
                afterManualSave.Generation,
                Is.EqualTo(protectedCheckpoint.Generation));
            Assert.That(afterManualSave.CanonicalPayload, Is.EqualTo(safeBytes));
            Assert.That(controller.SaveStatus, Is.EqualTo(
                LastBearingGameController.SettlementLossSaveStatus));

            controller.Load();
            Assert.That(controller.IsSettlementLost, Is.False);
            Assert.That(
                LastBearingCanonicalCodec.Encode(controller.State!),
                Is.EqualTo(safeBytes));

            controller.ReturnToTitle();
            Assert.That(controller.HasActiveGame, Is.False);
            controller.StartNewGame(ColonyComposition.Mixed);
            Assert.That(controller.HasActiveGame, Is.True);
            Assert.That(
                controller.ReadModel!.Composition,
                Is.EqualTo(ColonyComposition.Mixed));
        }

        [UnityTest]
        public IEnumerator RoadDryBellHoldsDrivingViewAndExposesHud()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState outbound =
                CreateOutboundState(ColonyComposition.RobotOnly);
            LastBearingState edge = WithState(
                outbound,
                (
                    "WaterMilli",
                    -LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick),
                (
                    "SettlementAccumulatorMilli",
                    LastBearingBalanceV1.FullClockScaleMilli -
                    LastBearingBalanceV1.ExpeditionHomeClockScaleMilli));
            InstallControllerState(controller, edge);

            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));
            Assert.That(controller.FieldDesk!.OwnsRetainedHud, Is.True);
            byte[] before = LastBearingCanonicalCodec.Encode(controller.State!);

            InvokeSimulationTick(controller);

            Assert.That(controller.IsSettlementLost, Is.True);
            Assert.That(controller.ReadModel!.WaterMilli, Is.Zero);
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));
            Assert.That(controller.FieldDesk.OwnsRetainedHud, Is.False);
            Assert.That(controller.Hud, Is.Not.Null);
            Assert.That(controller.Hud!.enabled, Is.True);
            Assert.That(
                controller.Hud.BlocksWorldPointer(
                    new Vector2(30f, Screen.height * 0.5f)),
                Is.True);
            Assert.That(
                LastBearingCanonicalCodec.Encode(controller.State!),
                Is.Not.EqualTo(before));

            byte[] lossBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            InvokeSimulationTick(controller);
            Assert.That(
                LastBearingCanonicalCodec.Encode(controller.State!),
                Is.EqualTo(lossBytes));
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));
        }

        [UnityTest]
        public IEnumerator SameTickWaterShiftRescueMayAutosave()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingProfileStore store =
                InstallTemporarySaveAdapter(controller);
            LastBearingState active =
                CreateActiveWaterShift(ColonyComposition.HumanOnly);
            LastBearingState edge = WithState(
                active,
                (
                    "WaterMilli",
                    -LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick),
                (
                    "HotShiftElapsedTicks",
                    LastBearingBalanceV1
                        .WaterShiftRequiredSettlementTicks - 1));
            InstallControllerState(controller, edge);
            controller.Save();
            LastBearingLoadResult before = Load(store);

            InvokeSimulationTick(controller);

            Assert.That(controller.IsSettlementLost, Is.False);
            Assert.That(controller.ReadModel!.WaterMilli, Is.GreaterThan(0));
            Assert.That(
                controller.ReadModel.ActiveServiceWorkOrder,
                Is.EqualTo(ServiceWorkOrder.None));
            Assert.That(
                controller.ReadModel.WaterShiftCompletedCount,
                Is.EqualTo(1));
            LastBearingLoadResult after = Load(store);
            Assert.That(after.Generation, Is.GreaterThan(before.Generation));
            Assert.That(
                after.CanonicalPayload,
                Is.EqualTo(
                    LastBearingCanonicalCodec.Encode(controller.State!)));
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
                UnityEngine.Object.FindFirstObjectByType<
                    LastBearingGameController>();
            Assert.That(controller, Is.Not.Null);
            _controller = controller;
        }

        private LastBearingProfileStore InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "dry-bell-" + Guid.NewGuid().ToString("N"));
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
            return store;
        }

        private static LastBearingLoadResult Load(
            LastBearingProfileStore store)
        {
            LastBearingLoadResult result = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            Assert.That(result.Succeeded, Is.True, result.Code);
            Assert.That(result.CanonicalPayload, Is.Not.Null);
            return result;
        }

        private static LastBearingState CreateHotShiftCheckpointDryEdge(
            LastBearingState source)
        {
            LastBearingState active =
                StartServiceWorkOrder(source, ServiceWorkOrder.PartsShift);
            return WithState(
                active,
                (
                    "WaterMilli",
                    checked(
                        -LastBearingBalanceV1
                            .FailingWaterRateMilliPerSettlementTick -
                        LastBearingBalanceV1
                            .HotShiftWaterModifierMilliPerSettlementTick)),
                (
                    "HotShiftElapsedTicks",
                    LastBearingBalanceV1
                        .HotShiftCheckpointSettlementTick - 1));
        }

        private static LastBearingState CreateActiveWaterShift(
            ColonyComposition composition)
        {
            LastBearingState source =
                LastBearingScenarioFactory.CreateInitial(composition, 3305);
            return StartServiceWorkOrder(
                source,
                ServiceWorkOrder.WaterShift,
                completePreparationFirst: true);
        }

        private static LastBearingState StartServiceWorkOrder(
            LastBearingState source,
            ServiceWorkOrder order,
            bool completePreparationFirst = false)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = source;
            if (state.AssignedResidentId == null)
            {
                string resident =
                    state.Roster.Composition == ColonyComposition.RobotOnly
                        ? ResidentRoster.RobotResidentId
                        : ResidentRoster.HumanResidentId;
                state = kernel.Step(
                    state,
                    new LastBearingCommand[]
                    {
                        new AssignResidentCommand(
                            state.NextCommandSequence,
                            resident),
                    }).State;
            }

            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new ActivateSliceInfrastructureCommand(
                        state.NextCommandSequence),
                }).State;
            long sequence = state.NextCommandSequence;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new SelectPreparationCommand(
                        sequence,
                        completePreparationFirst
                            ? PreparationChoice.CivicBuffer
                            : PreparationChoice.WorkshopPush,
                        VehicleModule.SealedRangeTank),
                    new InstallVehicleModuleCommand(
                        sequence + 1,
                        VehicleModule.SealedRangeTank),
                }).State;
            if (completePreparationFirst)
            {
                for (var guard = 0;
                     state.PreparationPhase != PreparationPhase.Ready &&
                     guard < 1000;
                     guard++)
                {
                    state = kernel.Step(
                        state,
                        Array.Empty<LastBearingCommand>()).State;
                }

                Assert.That(
                    state.PreparationPhase,
                    Is.EqualTo(PreparationPhase.Ready));
                Assert.That(
                    state.ActiveWaterModifierMilliPerSettlementTick,
                    Is.Zero);
            }

            LastBearingCommand command =
                order == ServiceWorkOrder.PartsShift
                    ? new RunHotShiftCommand(
                        state.NextCommandSequence,
                        state.HotShiftCompletedCount)
                    : new RunWaterShiftCommand(
                        state.NextCommandSequence,
                        state.WaterShiftCompletedCount);
            return kernel.Step(
                state,
                new[] { command }).State;
        }

        private static LastBearingState CreateOutboundState(
            ColonyComposition composition)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(composition, 3304);
            string resident =
                composition == ColonyComposition.RobotOnly
                    ? ResidentRoster.RobotResidentId
                    : ResidentRoster.HumanResidentId;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new AssignResidentCommand(
                        state.NextCommandSequence,
                        resident),
                }).State;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new ActivateSliceInfrastructureCommand(
                        state.NextCommandSequence),
                }).State;
            long sequence = state.NextCommandSequence;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new SelectPreparationCommand(
                        sequence,
                        PreparationChoice.WorkshopPush,
                        VehicleModule.WinchAssembly),
                    new InstallVehicleModuleCommand(
                        sequence + 1,
                        VehicleModule.WinchAssembly),
                }).State;
            for (var guard = 0;
                 (state.PreparationPhase != PreparationPhase.Ready ||
                  state.ModuleInstallationState !=
                      ModuleInstallationState.Installed) &&
                 guard < 1000;
                 guard++)
            {
                state = kernel.Step(
                    state,
                    Array.Empty<LastBearingCommand>()).State;
            }

            Assert.That(
                state.PreparationPhase,
                Is.EqualTo(PreparationPhase.Ready));
            sequence = state.NextCommandSequence;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new PrepareExpeditionTransactionCommand(
                        sequence,
                        "tx:vgr33-dry-bell",
                        "fp:vgr33-dry-bell"),
                    new DebitCityManifestCommand(
                        sequence + 1,
                        "tx:vgr33-dry-bell",
                        "fp:vgr33-dry-bell"),
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
                typeof(LastBearingGameController).GetField("_state", flags);
            FieldInfo? readModelField =
                typeof(LastBearingGameController).GetField(
                    "_readModel",
                    flags);
            FieldInfo? pendingField =
                typeof(LastBearingGameController).GetField(
                    "_pendingCommands",
                    flags);
            MethodInfo? apply =
                typeof(LastBearingGameController).GetMethod(
                    "ApplyPresentation",
                    flags);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(readModelField, Is.Not.Null);
            Assert.That(pendingField, Is.Not.Null);
            Assert.That(apply, Is.Not.Null);

            stateField!.SetValue(controller, state);
            readModelField!.SetValue(
                controller,
                LastBearingReadModel.FromState(state));
            var pending = pendingField!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            pending!.Clear();
            apply!.Invoke(controller, null);
        }

        private static LastBearingState WithState(
            LastBearingState source,
            params (string Name, object Value)[] values)
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
            MethodInfo? build =
                builderType.GetMethod("Build", flags);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(build, Is.Not.Null);

            object builder =
                constructor!.Invoke(new object[] { source });
            foreach ((string name, object value) in values)
            {
                FieldInfo? field = builderType.GetField(name, flags);
                Assert.That(field, Is.Not.Null, name);
                field!.SetValue(builder, value);
            }

            return (LastBearingState)build!.Invoke(builder, null);
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

        private static void AssertRecoveryAction(
            LastBearingFieldDeskActionProjection action,
            LastBearingFieldDeskIntent intent,
            string label,
            bool enabled)
        {
            Assert.That(action.Intent, Is.EqualTo(intent));
            Assert.That(action.Label, Is.EqualTo(label));
            Assert.That(action.IsVisible, Is.True);
            Assert.That(action.IsEnabled, Is.EqualTo(enabled));
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
