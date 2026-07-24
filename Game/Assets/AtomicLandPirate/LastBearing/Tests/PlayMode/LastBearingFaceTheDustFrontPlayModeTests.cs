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
    public sealed class LastBearingFaceTheDustFrontPlayModeTests :
        InputTestFixture
    {
        private const string SceneName = "LastBearing";

        private enum RelayPath
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
                    SceneManager.CreateScene("DustFrontRelay_TestCleanup");
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
            LastBearingState source =
                PrepareControllerForRelay(
                    controller,
                    DustFrontOutcome.Held);
            LastBearingCityServiceCellInteractor interactor =
                controller.World!.CityServiceCellView!.Interactor!;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();

            Assert.That(controller.CanOpenDustFrontRelay, Is.True);
            Assert.That(
                interactor.IsDustFrontRelayControlVisible,
                Is.True);
            AssertRelayVerdictPresentation(
                controller,
                DustFrontOutcome.Held);

            for (var cycle = 0; cycle < 4; cycle++)
            {
                string cycleHash = controller.CanonicalHash;
                controller.OpenGarageBay();
                Assert.That(
                    interactor.IsDustFrontRelayControlVisible,
                    Is.False);
                Assert.That(
                    interactor.IsDustFrontRelayFocused,
                    Is.False);
                Assert.That(
                    interactor.IsDustFrontRelayInputArmed,
                    Is.False);

                controller.ShowCityOverview();
                Assert.That(
                    interactor.IsDustFrontRelayControlVisible,
                    Is.True);
                Assert.That(
                    interactor.IsDustFrontRelayFocused,
                    Is.False);
                Assert.That(PendingCommands(controller), Is.Empty);
                Assert.That(controller.CanonicalHash, Is.EqualTo(cycleHash));
            }

            byte[] stateBefore =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string hashBefore = controller.CanonicalHash;
            long globalTickBefore = controller.ReadModel!.GlobalTick;
            long settlementTickBefore =
                controller.ReadModel.SettlementTick;
            int generationsBefore = GenerationCount(
                RequireProfileDirectory(controller));
            LastBearingCameraRig cameraRig =
                controller.World!.CameraRig!;
            float yawBeforeHeldRoute = cameraRig.CityYaw;

            Press(keyboard.eKey);
            controller.OpenDustFrontRelay();
            Assert.That(controller.IsDustFrontRelayFocused, Is.True);
            Assert.That(
                interactor.IsDustFrontRelayInputArmed,
                Is.False);
            controller.AcknowledgeDustFront();
            yield return null;
            Assert.That(
                cameraRig.CityYaw,
                Is.EqualTo(yawBeforeHeldRoute).Within(0.0001f),
                "The relay's E input must not rotate the city camera.");
            InvokeInteractorUpdate(interactor);
            interactor.ClickDustFrontRelay();

            Assert.That(
                interactor.IsDustFrontRelayInputArmed,
                Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertPreTickUnchanged(
                controller,
                stateBefore,
                hashBefore,
                globalTickBefore,
                settlementTickBefore,
                DustFrontOutcome.Held);
            Assert.That(
                GenerationCount(RequireProfileDirectory(controller)),
                Is.EqualTo(generationsBefore));

            Release(keyboard.eKey);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(
                interactor.IsDustFrontRelayInputArmed,
                Is.True);
            Assert.That(PendingCommands(controller), Is.Empty);

            controller.OpenDustFrontRelay();
            Assert.That(
                interactor.IsDustFrontRelayInputArmed,
                Is.False,
                "Every explicit route must begin a fresh release cycle.");
            interactor.ResetLocalSelection();
            Assert.That(interactor.IsDustFrontRelayFocused, Is.False);
            AssertPreTickUnchanged(
                controller,
                stateBefore,
                hashBefore,
                globalTickBefore,
                settlementTickBefore,
                DustFrontOutcome.Held);
            Assert.That(controller.State, Is.SameAs(source));
        }

        [UnityTest]
        public IEnumerator FreshInputsAcknowledgeExactHeldAndBreachedVerdicts()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState held =
                PrepareControllerForRelay(
                    controller,
                    DustFrontOutcome.Held);
            LastBearingState breached =
                CreateResolvedDustFrontState(
                    CreateCommissionedState(controller),
                    DustFrontOutcome.Breached);
            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingCityServiceCellInteractor interactor =
                view.Interactor!;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            string profileDirectory = RequireProfileDirectory(controller);

            var cases = new[]
            {
                (RelayPath.Keyboard, held, DustFrontOutcome.Held),
                (RelayPath.Gamepad, breached, DustFrontOutcome.Breached),
                (RelayPath.Pointer, held, DustFrontOutcome.Held),
            };

            foreach (var item in cases)
            {
                InstallControllerState(controller, item.Item2);
                controller.ShowCityOverview();
                interactor.ResetLocalSelection();
                Assert.That(controller.CanOpenDustFrontRelay, Is.True);
                Assert.That(controller.CanAcknowledgeDustFront, Is.False);

                controller.OpenDustFrontRelay();
                Assert.That(interactor.IsDustFrontRelayFocused, Is.True);
                Assert.That(
                    interactor.IsDustFrontRelayInputArmed,
                    Is.False);
                AssertRelayVerdictPresentation(controller, item.Item3);

                yield return null;
                InvokeInteractorUpdate(interactor);
                Assert.That(
                    interactor.IsDustFrontRelayInputArmed,
                    Is.True);
                Assert.That(controller.CanAcknowledgeDustFront, Is.True);

                byte[] stateBefore =
                    LastBearingCanonicalCodec.Encode(controller.State!);
                string hashBefore = controller.CanonicalHash;
                long globalTickBefore = controller.ReadModel!.GlobalTick;
                long settlementTickBefore =
                    controller.ReadModel.SettlementTick;
                long sequenceBefore =
                    controller.State!.NextCommandSequence;
                int generationsBefore =
                    GenerationCount(profileDirectory);

                switch (item.Item1)
                {
                    case RelayPath.Keyboard:
                        Press(keyboard.eKey);
                        InvokeInteractorUpdate(interactor);
                        Release(keyboard.eKey);
                        break;
                    case RelayPath.Gamepad:
                        Press(gamepad.buttonSouth);
                        InvokeInteractorUpdate(interactor);
                        Release(gamepad.buttonSouth);
                        break;
                    case RelayPath.Pointer:
                        ActivateWorldRelayTarget(controller, interactor);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                AssertSingleAcknowledgementCommand(
                    controller,
                    sequenceBefore);
                interactor.ClickDustFrontRelay();
                controller.AcknowledgeDustFront();
                AssertSingleAcknowledgementCommand(
                    controller,
                    sequenceBefore);
                AssertPreTickUnchanged(
                    controller,
                    stateBefore,
                    hashBefore,
                    globalTickBefore,
                    settlementTickBefore,
                    item.Item3);
                AssertRelayVerdictPresentation(controller, item.Item3);
                Assert.That(
                    GenerationCount(profileDirectory),
                    Is.EqualTo(generationsBefore));

                InvokeSimulationTick(controller);
                Assert.That(
                    controller.ReadModel!.DustFrontOutcome,
                    Is.EqualTo(item.Item3));
                Assert.That(
                    controller.ReadModel
                        .IsDustFrontAcknowledgementRequired,
                    Is.False);
                Assert.That(
                    controller.ReadModel.PauseCause,
                    Is.EqualTo(PauseCause.None));
                Assert.That(
                    controller.ReadModel.GlobalTick,
                    Is.EqualTo(globalTickBefore + 1));
                Assert.That(
                    controller.ReadModel.SettlementTick,
                    Is.GreaterThan(settlementTickBefore),
                    "The accepted relay action must resume settlement clocks.");
                Assert.That(
                    controller.IsDustFrontAcknowledgementQueued,
                    Is.False);
                Assert.That(
                    interactor.IsDustFrontRelayControlVisible,
                    Is.True);
                Assert.That(interactor.IsDustFrontRelayFocused, Is.False);
                AssertRelayVerdictPresentation(controller, item.Item3);
                Assert.That(
                    interactor.DustFrontRelayLabel,
                    Is.EqualTo(
                        item.Item3 == DustFrontOutcome.Breached
                            ? "FRONT BREACHED\nSHUTTER DOWN"
                            : "FRONT HELD\nCLOCKS RUNNING"));

                bool expectedBreach =
                    item.Item3 == DustFrontOutcome.Breached;
                Assert.That(
                    controller.ReadModel.IsHotShiftStalledByDustFront,
                    Is.EqualTo(expectedBreach));
                Assert.That(
                    view.IsDustFrontShutterVisible,
                    Is.EqualTo(expectedBreach));
                string acceptedHash = AssertAutosaveBoundary(
                    controller,
                    profileDirectory,
                    generationsBefore,
                    hashBefore);

                controller.ReturnToTitle();
                controller.Load();
                Assert.That(
                    controller.CanonicalHash,
                    Is.EqualTo(acceptedHash));
                Assert.That(
                    controller.ReadModel!.DustFrontOutcome,
                    Is.EqualTo(item.Item3));
                Assert.That(
                    controller.ReadModel
                        .IsDustFrontAcknowledgementRequired,
                    Is.False);
                Assert.That(
                    controller.ReadModel.PauseCause,
                    Is.EqualTo(PauseCause.None));
                Assert.That(
                    controller.ReadModel.IsHotShiftStalledByDustFront,
                    Is.EqualTo(expectedBreach));
                Assert.That(
                    interactor.IsDustFrontRelayControlVisible,
                    Is.True);
                AssertRelayVerdictPresentation(controller, item.Item3);
                Assert.That(
                    interactor.DustFrontRelayLabel,
                    Is.EqualTo(
                        item.Item3 == DustFrontOutcome.Breached
                            ? "FRONT BREACHED\nSHUTTER DOWN"
                            : "FRONT HELD\nCLOCKS RUNNING"));

                if (expectedBreach)
                {
                    InstallControllerState(
                        controller,
                        CreateRepairedDustFrontState(controller.State!));
                    Assert.That(
                        controller.ReadModel!.IsHotShiftStalledByDustFront,
                        Is.False);
                    Assert.That(view.IsDustFrontShutterVisible, Is.False);
                    Assert.That(
                        interactor.IsDustFrontRelayControlVisible,
                        Is.True);
                    AssertRelayVerdictPresentation(
                        controller,
                        DustFrontOutcome.Breached,
                        expectBreachStall: false);
                    Assert.That(
                        interactor.DustFrontRelayLabel,
                        Is.EqualTo("FRONT BREACHED\nREPAIR HOLDS"));
                }
            }
        }

        [UnityTest]
        public IEnumerator NonCityAndGenuinelyMissingRelayUseBoundedFallback()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState source =
                PrepareControllerForRelay(
                    controller,
                    DustFrontOutcome.Held);
            LastBearingCityServiceCellInteractor interactor =
                controller.World!.CityServiceCellView!.Interactor!;

            controller.OpenGarageBay();
            Assert.That(controller.CanOpenDustFrontRelay, Is.False);
            Assert.That(
                controller.CanAcknowledgeDustFrontFallback,
                Is.True);
            long garageSequence = controller.State!.NextCommandSequence;
            controller.AcknowledgeDustFrontFallback();
            AssertSingleAcknowledgementCommand(
                controller,
                garageSequence);

            InstallControllerState(controller, source);
            controller.ShowCityOverview();
            Transform control = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor
                    .DustFrontRelayControlName);
            UnityEngine.Object.DestroyImmediate(control.gameObject);
            Assert.That(interactor.HasDustFrontRelayControl, Is.False);
            Assert.That(controller.CanOpenDustFrontRelay, Is.False);
            Assert.That(
                controller.CanAcknowledgeDustFrontFallback,
                Is.True);

            LastBearingFieldDeskProjection projection =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                projection.PrimaryAction.Intent,
                Is.EqualTo(
                    LastBearingFieldDeskIntent.AcknowledgeDustFront));
            Assert.That(
                projection.PrimaryAction.Label,
                Is.EqualTo("ACKNOWLEDGE FRONT · FALLBACK"));
            Assert.That(projection.PrimaryAction.IsEnabled, Is.True);

            long missingSequence = controller.State!.NextCommandSequence;
            controller.AcknowledgeDustFrontFallback();
            AssertSingleAcknowledgementCommand(
                controller,
                missingSequence);
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.IsDustFrontAcknowledgementRequired,
                Is.False);
            Assert.That(
                controller.ReadModel.PauseCause,
                Is.EqualTo(PauseCause.None));
        }

        [UnityTest]
        public IEnumerator TitleModeFocusStaleAndPendingGuardsFailClosed()
        {
            yield return BootController();
            LastBearingGameController controller = _controller!;
            LastBearingState source =
                PrepareControllerForRelay(
                    controller,
                    DustFrontOutcome.Breached);
            LastBearingCityServiceCellInteractor interactor =
                controller.World!.CityServiceCellView!.Interactor!;
            string profileDirectory = RequireProfileDirectory(controller);
            int generationsBefore = GenerationCount(profileDirectory);

            byte[] sourceBytes = LastBearingCanonicalCodec.Encode(source);
            string sourceHash = controller.CanonicalHash;
            long sourceGlobalTick = controller.ReadModel!.GlobalTick;
            long sourceSettlementTick =
                controller.ReadModel.SettlementTick;

            interactor.ResetLocalSelection();
            interactor.ClickDustFrontRelay();
            controller.AcknowledgeDustFront();
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertPreTickUnchanged(
                controller,
                sourceBytes,
                sourceHash,
                sourceGlobalTick,
                sourceSettlementTick,
                DustFrontOutcome.Breached);

            controller.OpenGarageBay();
            controller.OpenDustFrontRelay();
            interactor.FocusDustFrontRelay();
            interactor.ClickDustFrontRelay();
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(interactor.IsDustFrontRelayFocused, Is.False);
            AssertPreTickUnchanged(
                controller,
                sourceBytes,
                sourceHash,
                sourceGlobalTick,
                sourceSettlementTick,
                DustFrontOutcome.Breached);

            controller.ShowCityOverview();
            InstallControllerState(controller, source);
            ReplaceControllerStateWithoutPresentation(controller, source);
            Assert.That(
                interactor.IsDustFrontRelayControlVisible,
                Is.False);
            controller.OpenDustFrontRelay();
            interactor.FocusDustFrontRelay();
            interactor.ClickDustFrontRelay();
            controller.AcknowledgeDustFront();
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertPreTickUnchanged(
                controller,
                sourceBytes,
                sourceHash,
                sourceGlobalTick,
                sourceSettlementTick,
                DustFrontOutcome.Breached);

            InstallControllerState(controller, source);
            controller.ShowCityOverview();
            interactor.ResetLocalSelection();
            controller.OpenDustFrontRelay();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(
                interactor.IsDustFrontRelayInputArmed,
                Is.True);
            AddUnrelatedPendingCommand(controller);
            LastBearingCommand[] unrelated = PendingCommands(controller);
            Assert.That(unrelated, Has.Length.EqualTo(1));
            Assert.That(unrelated[0], Is.TypeOf<SetPauseCommand>());
            interactor.ClickDustFrontRelay();
            controller.AcknowledgeDustFront();
            Assert.That(PendingCommands(controller), Has.Length.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<SetPauseCommand>());
            AssertPreTickUnchanged(
                controller,
                sourceBytes,
                sourceHash,
                sourceGlobalTick,
                sourceSettlementTick,
                DustFrontOutcome.Breached);

            InstallControllerState(controller, source);
            controller.ReturnToTitle();
            controller.OpenDustFrontRelay();
            interactor.FocusDustFrontRelay();
            interactor.ClickDustFrontRelay();
            controller.AcknowledgeDustFront();
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

        private static LastBearingState PrepareControllerForRelay(
            LastBearingGameController controller,
            DustFrontOutcome outcome)
        {
            LastBearingState commissioned =
                CreateCommissionedState(controller);
            LastBearingState resolved =
                CreateResolvedDustFrontState(commissioned, outcome);
            InstallControllerState(controller, resolved);
            controller.ShowCityOverview();

            Assert.That(
                controller.ReadModel!.DustFrontOutcome,
                Is.EqualTo(outcome));
            Assert.That(
                controller.ReadModel.IsDustFrontAcknowledgementRequired,
                Is.True);
            Assert.That(
                controller.ReadModel.PauseCause,
                Is.EqualTo(PauseCause.DustFrontAlert));
            Assert.That(controller.CanOpenDustFrontRelay, Is.True);
            Assert.That(controller.CanAcknowledgeDustFront, Is.False);
            return resolved;
        }

        private static LastBearingState CreateCommissionedState(
            LastBearingGameController controller)
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

            Assert.That(
                controller.ReadModel!.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.DeliveredToWorkshop));
            Assert.That(
                controller.ReadModel.PreparationChoice,
                Is.EqualTo(PreparationChoice.CivicBuffer));
            Assert.That(
                controller.ReadModel.PlannedModule,
                Is.EqualTo(VehicleModule.SealedRangeTank));
            return controller.State!;
        }

        private static LastBearingState CreateResolvedDustFrontState(
            LastBearingState source,
            DustFrontOutcome outcome)
        {
            Assert.That(
                outcome,
                Is.EqualTo(DustFrontOutcome.Held)
                    .Or.EqualTo(DustFrontOutcome.Breached));
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
            FieldInfo? water = builderType.GetField("WaterMilli", flags);
            MethodInfo? build = builderType.GetMethod("Build", flags);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(progress, Is.Not.Null);
            Assert.That(water, Is.Not.Null);
            Assert.That(build, Is.Not.Null);

            object builder = constructor!.Invoke(new object[] { source });
            progress!.SetValue(
                builder,
                LastBearingBalanceV1.DustFrontThresholdCrisisTicks - 1);
            water!.SetValue(
                builder,
                outcome == DustFrontOutcome.Held ? 60020L : 0L);
            var threshold = build!.Invoke(builder, null) as LastBearingState;
            Assert.That(threshold, Is.Not.Null);

            var command = new RunHotShiftCommand(
                threshold!.NextCommandSequence,
                LastBearingReadModel.FromState(threshold)
                    .HotShiftCompletedCount);
            LastBearingTickResult result = new LastBearingKernel().Step(
                threshold,
                new LastBearingCommand[] { command });
            Assert.That(
                result.State.DustFrontOutcome,
                Is.EqualTo(outcome));
            Assert.That(
                result.State.IsDustFrontAcknowledgementRequired,
                Is.True);
            Assert.That(
                result.State.PauseCause,
                Is.EqualTo(PauseCause.DustFrontAlert));
            Assert.That(
                result.State.HotShiftPhase,
                Is.EqualTo(HotShiftPhase.InProgress));
            Assert.That(
                result.ReadModel.IsHotShiftStalledByDustFront,
                Is.EqualTo(outcome == DustFrontOutcome.Breached));
            return result.State;
        }

        private static LastBearingState CreateRepairedDustFrontState(
            LastBearingState source)
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
            FieldInfo? turbineCondition = builderType.GetField(
                "TurbineCondition",
                flags);
            MethodInfo? build = builderType.GetMethod("Build", flags);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(turbineCondition, Is.Not.Null);
            Assert.That(build, Is.Not.Null);

            object builder = constructor!.Invoke(new object[] { source });
            turbineCondition!.SetValue(
                builder,
                TurbineCondition.BearingRepaired);
            var repaired = build!.Invoke(builder, null) as LastBearingState;
            Assert.That(repaired, Is.Not.Null);
            Assert.That(
                LastBearingReadModel.FromState(repaired!)
                    .IsHotShiftStalledByDustFront,
                Is.False);
            return repaired!;
        }

        private void InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "face-dust-front-" + Guid.NewGuid().ToString("N"));
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

        private static void AddUnrelatedPendingCommand(
            LastBearingGameController controller)
        {
            FieldInfo? pending =
                typeof(LastBearingGameController).GetField(
                    "_pendingCommands",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pending, Is.Not.Null);
            var commands = pending!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(commands, Is.Not.Null);
            commands!.Add(
                new SetPauseCommand(
                    controller.State!.NextCommandSequence,
                    isPaused: true));
        }

        private static void AssertSingleAcknowledgementCommand(
            LastBearingGameController controller,
            long expectedSequence)
        {
            LastBearingCommand[] commands = PendingCommands(controller);
            Assert.That(commands, Has.Length.EqualTo(1));
            Assert.That(
                commands[0],
                Is.TypeOf<AcknowledgeDustFrontCommand>());
            Assert.That(commands[0].Sequence, Is.EqualTo(expectedSequence));
            Assert.That(
                controller.IsDustFrontAcknowledgementQueued,
                Is.True);
        }

        private static void AssertPreTickUnchanged(
            LastBearingGameController controller,
            byte[] expectedState,
            string expectedHash,
            long expectedGlobalTick,
            long expectedSettlementTick,
            DustFrontOutcome expectedOutcome)
        {
            Assert.That(
                LastBearingCanonicalCodec.Encode(controller.State!),
                Is.EqualTo(expectedState));
            Assert.That(controller.CanonicalHash, Is.EqualTo(expectedHash));
            Assert.That(
                controller.ReadModel!.GlobalTick,
                Is.EqualTo(expectedGlobalTick));
            Assert.That(
                controller.ReadModel.SettlementTick,
                Is.EqualTo(expectedSettlementTick));
            Assert.That(
                controller.ReadModel.DustFrontOutcome,
                Is.EqualTo(expectedOutcome));
            Assert.That(
                controller.ReadModel.IsDustFrontAcknowledgementRequired,
                Is.True);
            Assert.That(
                controller.ReadModel.PauseCause,
                Is.EqualTo(PauseCause.DustFrontAlert));
        }

        private static string AssertAutosaveBoundary(
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
            return currentHash;
        }

        private static int GenerationCount(string profileDirectory)
        {
            Assert.That(Directory.Exists(profileDirectory), Is.True);
            return Directory.GetFiles(profileDirectory, "gen-*.lbg").Length;
        }

        private static void AssertRelayVerdictPresentation(
            LastBearingGameController controller,
            DustFrontOutcome outcome,
            bool? expectBreachStall = null)
        {
            LastBearingCityServiceCellView view =
                controller.World!.CityServiceCellView!;
            LastBearingCityServiceCellInteractor interactor =
                view.Interactor!;
            Transform held = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor
                    .DustFrontRelayHeldSignalName);
            Transform breached = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor
                    .DustFrontRelayBreachedSignalName);
            bool expectedBreach = outcome == DustFrontOutcome.Breached;
            bool expectedStall = expectBreachStall ?? expectedBreach;
            Assert.That(
                held.gameObject.activeInHierarchy,
                Is.EqualTo(!expectedBreach));
            Assert.That(
                breached.gameObject.activeInHierarchy,
                Is.EqualTo(expectedBreach));
            Assert.That(
                controller.ReadModel!.IsHotShiftStalledByDustFront,
                Is.EqualTo(expectedStall));
            Assert.That(
                view.IsDustFrontShutterVisible,
                Is.EqualTo(expectedStall));
        }

        private static void ActivateWorldRelayTarget(
            LastBearingGameController controller,
            LastBearingCityServiceCellInteractor interactor)
        {
            Transform target = RequireNamed(
                interactor.transform,
                LastBearingCityServiceCellInteractor
                    .DustFrontRelayControlName);
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
