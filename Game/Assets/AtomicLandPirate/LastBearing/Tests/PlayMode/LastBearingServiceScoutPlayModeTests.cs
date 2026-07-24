#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingServiceScoutPlayModeTests :
        InputTestFixture
    {
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<string> _saveRoots = new List<string>();

        [UnityTearDown]
        public IEnumerator TearDownRuntime()
        {
            foreach (GameObject root in _roots)
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }
            }

            foreach (string root in _saveRoots)
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }

            _roots.Clear();
            _saveRoots.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourRoutesStayPureThenFreshPendantServicesAndReloads()
        {
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            LastBearingState ready = CreateServiceReadyState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                7301);
            InstallControllerState(controller, ready);
            controller.ShowCityOverview();
            yield return null;

            LastBearingReadModel readyModel = controller.ReadModel!;
            LastBearingScoutServiceInteractor interactor =
                controller.World!.ScoutServiceInteractor!;
            LastBearingGarageBayView garage =
                controller.World.GarageBayView!;
            Assert.That(controller.CanOpenScoutServiceBay, Is.True);
            Assert.That(
                LastBearingFieldDeskPresenter
                    .Present(controller)
                    .PrimaryAction.Intent,
                Is.EqualTo(
                    LastBearingFieldDeskIntent.OpenScoutServiceBay));
            controller.OpenScoutServiceBay();
            AssertReadyPresentation(controller, interactor, readyModel);
            Assert.That(
                controller.GetComponentsInChildren<Camera>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                controller.GetComponentsInChildren<AudioListener>(true),
                Has.Length.EqualTo(1));
            controller.ShowCityOverview();

            byte[] readyBytes =
                LastBearingCanonicalCodec.Encode(controller.State!);
            string readyHash = controller.CanonicalHash;
            controller.Save();
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(readyHash));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.GarageBay));
            Assert.That(interactor.IsControlFocused, Is.True);
            Assert.That(interactor.IsInputArmed, Is.False);
            controller.ShowCityOverview();

            for (var cycle = 0; cycle < 4; cycle++)
            {
                LastBearingFieldDeskProjection projection =
                    LastBearingFieldDeskPresenter.Present(controller);
                Assert.That(
                    projection.PrimaryAction.Intent,
                    Is.EqualTo(
                        LastBearingFieldDeskIntent.OpenScoutServiceBay));
                Assert.That(
                    projection.PrimaryAction.Label,
                    Is.EqualTo(
                        "OPEN GARAGE · SERVICE SASHA'S SCOUT"));
                Assert.That(
                    projection.PrimaryAction.Detail,
                    Does.Contain(
                        readyModel.VehicleConditionMilli + " / 1000"));
                Assert.That(
                    projection.PrimaryAction.Detail,
                    Does.Contain(
                        "Spend " +
                        readyModel.VehicleServicePartsCostUnits +
                        " parts"));
                Assert.That(
                    projection.PrimaryAction.Detail,
                    Does.Contain(
                        "preserve " +
                        readyModel.VehicleServiceReservePartsUnits));

                controller.OpenScoutServiceBay();
                Assert.That(
                    controller.ModeCoordinator.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.GarageBay));
                Assert.That(interactor.IsControlFocused, Is.True);
                Assert.That(
                    garage.ModuleWorkLightIntensity,
                    Is.EqualTo(480f).Within(0.01f));
                CollectionAssert.AreEqual(
                    readyBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                controller.ShowCityOverview();
                CollectionAssert.AreEqual(
                    readyBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
            }

            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            controller.OpenScoutServiceBay();
            LastBearingReadModel currentModel =
                RuntimeReadModel(controller);
            ReplaceRuntimeReadModel(
                controller,
                LastBearingReadModel.FromState(controller.State!));
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            ReplaceRuntimeReadModel(controller, currentModel);
            InvokeApplyPresentation(controller);
            controller.ShowCityOverview();

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            controller.OpenScoutServiceBay();
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.False);
            Assert.That(interactor.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);

            Release(keyboard.eKey);
            yield return null;
            InvokeInteractorUpdate(interactor);
            Assert.That(interactor.IsInputArmed, Is.True);

            LastBearingState before = controller.State!;
            long sequence = before.NextCommandSequence;
            Press(keyboard.eKey);
            InvokeInteractorUpdate(interactor);
            Release(keyboard.eKey);
            ServiceScoutCommand command =
                AssertExactServiceCommand(controller, sequence);
            Assert.That(command.Sequence, Is.EqualTo(sequence));
            controller.ServiceScout();
            Assert.That(
                PendingCommands(controller),
                Has.Count.EqualTo(1),
                "Duplicate presentation input queued a second service.");
            CollectionAssert.AreEqual(
                readyBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            InvokeSimulationTick(controller);
            LastBearingReadModel accepted = controller.ReadModel!;
            Assert.That(
                accepted.VehicleConditionMilli,
                Is.EqualTo(
                    LastBearingBalanceV1.StartingVehicleConditionMilli));
            Assert.That(
                accepted.PartsUnits,
                Is.EqualTo(
                    before.PartsUnits -
                    readyModel.VehicleServicePartsCostUnits));
            Assert.That(
                accepted.PartsUnits,
                Is.GreaterThanOrEqualTo(
                    accepted.VehicleServiceReservePartsUnits));
            AssertPreserved(before, controller.State!);
            Assert.That(interactor.IsAcceptedReceiptVisible, Is.True);
            Assert.That(
                interactor.ReceiptLabel,
                Does.Contain("SCOUT SERVICED · 1000 / 1000"));
            Assert.That(
                interactor.ReceiptLabel,
                Does.Contain(
                    readyModel.VehicleServicePartsCostUnits +
                    " PARTS SPENT"));
            Assert.That(
                interactor.ReceiptLabel,
                Does.Contain(
                    readyModel.VehicleServiceReservePartsUnits +
                    " PARTS HELD IN RESERVE"));
            Assert.That(garage.IsScoutServiceHoistRaised, Is.True);
            Assert.That(garage.IsScoutConditionTelltaleHealthy, Is.True);
            Assert.That(
                garage.ModuleWorkLightIntensity,
                Is.EqualTo(640f).Within(0.01f));
            Assert.That(controller.SaveStatus, Does.Not.Contain("Unsaved"));

            string acceptedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(acceptedHash));
            Assert.That(
                controller.ReadModel!.VehicleConditionMilli,
                Is.EqualTo(1000));
            Assert.That(controller.IsScoutServiceFocused, Is.False);
            controller.ShowCityOverview();
            controller.OpenGarageBay();
            Assert.That(interactor.IsAcceptedReceiptVisible, Is.True);
            Assert.That(garage.IsScoutServiceHoistRaised, Is.True);
            Assert.That(garage.IsScoutConditionTelltaleHealthy, Is.True);
        }

        [UnityTest]
        public IEnumerator RepeatCircuitRoutesLaunchesReturnsAndReloads()
        {
            LastBearingGameController controller =
                CreateController(ColonyComposition.Mixed);
            LastBearingState serviceReady = CreateServiceReadyState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                7321);
            LastBearingState repeatReady = Apply(
                serviceReady,
                sequence => new ServiceScoutCommand(sequence));
            InstallControllerState(controller, repeatReady);
            controller.ShowCityOverview();
            yield return null;

            LastBearingReadModel readyModel = controller.ReadModel!;
            Assert.That(readyModel.IsRepeatExpeditionAvailable, Is.True);
            Assert.That(readyModel.IsRepeatExpedition, Is.False);
            LastBearingFieldDeskProjection desk =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                desk.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.OpenGarage));
            Assert.That(
                desk.PrimaryAction.Label,
                Is.EqualTo(
                    "OPEN GARAGE · RUN THE WRECK LINE AGAIN"));
            Assert.That(
                desk.PrimaryAction.Detail,
                Does.Contain(
                    readyModel.FutureRouteTollFuelUnits + " toll"));
            Assert.That(
                desk.PrimaryAction.Detail,
                Does.Contain(
                    readyModel.ProjectedRoundTripConditionLossMilli +
                    " condition"));
            Assert.That(
                desk.PrimaryAction.Detail,
                Does.Contain(
                    "+" +
                    readyModel.FrameRailSalvagePartsUnits +
                    " parts"));

            byte[] repeatReadyBytes =
                LastBearingCanonicalCodec.Encode(repeatReady);
            string repeatReadyHash = controller.CanonicalHash;
            controller.Save();
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(repeatReadyHash));
            CollectionAssert.AreEqual(
                repeatReadyBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            long preparedSequence = repeatReady.NextCommandSequence;
            string repeatSuffix = preparedSequence.ToString(
                CultureInfo.InvariantCulture);
            string repeatTransactionId = "tx:repeat:" + repeatSuffix;
            string repeatFingerprint = "fp:repeat:" + repeatSuffix;
            LastBearingState prepared = Apply(
                repeatReady,
                sequence =>
                    new PrepareRepeatExpeditionTransactionCommand(
                        sequence,
                        repeatReady.TransactionId!,
                        repeatReady.TransactionFingerprint!,
                        repeatTransactionId,
                        repeatFingerprint));
            InstallControllerState(controller, prepared);
            controller.Save();
            string preparedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(preparedHash));
            Assert.That(
                controller.ReadModel!.TransactionPhase,
                Is.EqualTo(TransactionPhase.Prepared));
            Assert.That(controller.ReadModel.IsRepeatExpedition, Is.True);
            Assert.That(
                controller.State!.TransactionId,
                Is.EqualTo(repeatTransactionId));
            Assert.That(
                controller.State.TransactionFingerprint,
                Is.EqualTo(repeatFingerprint));

            InstallControllerState(controller, repeatReady);
            controller.Save();
            controller.ShowCityOverview();
            LastBearingGarageDepartureInteractor launchDog =
                controller.World!.GarageDepartureInteractor!;
            Camera camera = controller.World.MainCamera!;
            AudioListener listener =
                controller.GetComponentInChildren<AudioListener>(true)!;
            for (var cycle = 0; cycle < 4; cycle++)
            {
                controller.OpenGarageBay();
                Assert.That(
                    controller.ModeCoordinator!.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.GarageBay));
                Assert.That(
                    controller.World.GarageDepartureInteractor,
                    Is.SameAs(launchDog));
                Assert.That(
                    controller.World.MainCamera,
                    Is.SameAs(camera));
                Assert.That(
                    controller.GetComponentInChildren<AudioListener>(true),
                    Is.SameAs(listener));
                Assert.That(launchDog.IsTargetVisible, Is.True);
                Assert.That(launchDog.IsFocused, Is.True);
                CollectionAssert.AreEqual(
                    repeatReadyBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
                controller.ShowCityOverview();
                CollectionAssert.AreEqual(
                    repeatReadyBytes,
                    LastBearingCanonicalCodec.Encode(controller.State!));
            }

            controller.OpenGarageBay();
            LastBearingReadModel currentModel = RuntimeReadModel(controller);
            ReplaceRuntimeReadModel(
                controller,
                LastBearingReadModel.FromState(controller.State!));
            Assert.That(launchDog.OperateFocused(), Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            ReplaceRuntimeReadModel(controller, currentModel);
            InvokeApplyPresentation(controller);
            controller.ShowCityOverview();

            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.eKey);
            controller.OpenGarageBay();
            yield return null;
            InvokeGarageDepartureUpdate(launchDog);
            Assert.That(launchDog.IsInputArmed, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            Release(keyboard.eKey);
            yield return null;
            InvokeGarageDepartureUpdate(launchDog);
            Assert.That(launchDog.IsInputArmed, Is.True);

            long launchSequence = controller.State!.NextCommandSequence;
            string completedTransactionId =
                controller.State.TransactionId!;
            string completedFingerprint =
                controller.State.TransactionFingerprint!;
            Press(keyboard.eKey);
            InvokeGarageDepartureUpdate(launchDog);
            Release(keyboard.eKey);
            AssertExactRepeatLaunch(
                controller,
                launchSequence,
                completedTransactionId,
                completedFingerprint);
            controller.CommitExpedition();
            Assert.That(
                PendingCommands(controller),
                Has.Count.EqualTo(3),
                "Duplicate presentation input queued another launch.");
            CollectionAssert.AreEqual(
                repeatReadyBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));
            Assert.That(controller.ReadModel.IsRepeatExpedition, Is.True);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));
            string outboundHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(outboundHash));
            Assert.That(controller.ReadModel!.IsRepeatExpedition, Is.True);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));

            LastBearingState atDepot =
                AdvanceRepeatToDepot(controller.State!);
            InstallControllerState(controller, atDepot);
            Assert.That(controller.ReadModel!.IsRepeatExpedition, Is.True);
            Assert.That(
                controller.ReadModel.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Vehicle));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.DepotEncounter));
            LastBearingDepotReturnInteractor returnRatchet =
                controller.World!.DepotReturnInteractor!;
            Assert.That(returnRatchet.IsWaterValveVisible, Is.False);
            Assert.That(returnRatchet.IsFuelValveVisible, Is.False);
            Assert.That(returnRatchet.IsReturnLatchVisible, Is.True);
            Assert.That(returnRatchet.ActivateReturnLatch(), Is.True);
            LastBearingCommand[] returnCommands =
                PendingCommands(controller).ToArray();
            Assert.That(returnCommands, Has.Length.EqualTo(1));
            Assert.That(
                returnCommands[0],
                Is.TypeOf<FreezeReturnPayloadCommand>());
            var freeze =
                (FreezeReturnPayloadCommand)returnCommands[0];
            Assert.That(
                freeze.TransactionId,
                Is.EqualTo(atDepot.TransactionId));
            Assert.That(
                freeze.Fingerprint,
                Is.EqualTo(atDepot.TransactionFingerprint));

            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Returning));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.Driving));

            LastBearingState returned =
                AdvanceRepeatToHomeApron(controller.State!);
            long partsBeforeCheckIn = returned.PartsUnits;
            InstallControllerState(controller, returned);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityReturn));
            Assert.That(controller.IsReturnCheckInAvailable, Is.True);
            LastBearingReturnServiceView returnService =
                controller.World!.ReturnServiceView!;
            Assert.That(returnService.IsCheckInMarkerVisible, Is.True);
            Assert.That(returnService.HasVehicleRepairCargo, Is.False);
            Assert.That(
                returnService.HasVehicleFrameRailSalvage,
                Is.True);

            controller.Save();
            string returnedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(returnedHash));
            Assert.That(controller.IsReturnCheckInAvailable, Is.True);
            controller.CompleteReturn();
            LastBearingCommand[] checkIn =
                PendingCommands(controller).ToArray();
            Assert.That(checkIn, Has.Length.EqualTo(2));
            Assert.That(checkIn[0], Is.TypeOf<CreditCityReturnCommand>());
            Assert.That(
                checkIn[1],
                Is.TypeOf<FinalizeExpeditionTransactionCommand>());
            controller.CompleteReturn();
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(2));

            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel!.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtHome));
            Assert.That(
                controller.ReadModel.TransactionPhase,
                Is.EqualTo(TransactionPhase.Finalized));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(
                    partsBeforeCheckIn +
                    readyModel.FrameRailSalvagePartsUnits));
            Assert.That(
                controller.Status,
                Does.Contain("Repeat circuit checked in."));
            Assert.That(controller.Status, Does.Contain("service Sasha's Scout"));
            Assert.That(controller.Status, Does.Not.Contain("pump hall"));
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(
                LastBearingFieldDeskPresenter
                    .Present(controller)
                    .PrimaryAction.Intent,
                Is.EqualTo(
                    LastBearingFieldDeskIntent.OpenScoutServiceBay));

            string finalizedHash = controller.CanonicalHash;
            controller.ReturnToTitle();
            controller.Load();
            Assert.That(controller.CanonicalHash, Is.EqualTo(finalizedHash));
            Assert.That(
                controller.ReadModel!.FrameRailSalvageCustody,
                Is.EqualTo(FrameRailSalvageCustody.Credited));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(
                    partsBeforeCheckIn +
                    readyModel.FrameRailSalvagePartsUnits));
        }

        [UnityTest]
        public IEnumerator EveryCompositionAcceptsKeyboardGamepadOrExactPendant()
        {
            var cases = new[]
            {
                (
                    ColonyComposition.HumanOnly,
                    ResidentRoster.HumanResidentId,
                    7311),
                (
                    ColonyComposition.RobotOnly,
                    ResidentRoster.RobotResidentId,
                    7312),
                (
                    ColonyComposition.Mixed,
                    ResidentRoster.RobotResidentId,
                    7313),
            };

            for (var index = 0; index < cases.Length; index++)
            {
                var item = cases[index];
                LastBearingGameController controller =
                    CreateController(item.Item1);
                InstallControllerState(
                    controller,
                    CreateServiceReadyState(
                        item.Item1,
                        item.Item2,
                        item.Item3,
                        viaBarter: index == 1));
                controller.ShowCityOverview();
                Assert.That(
                    LastBearingFieldDeskPresenter
                        .Present(controller)
                        .PrimaryAction.Intent,
                    Is.EqualTo(
                        LastBearingFieldDeskIntent.OpenScoutServiceBay),
                    item.Item1.ToString());
                controller.OpenScoutServiceBay();
                yield return null;

                LastBearingScoutServiceInteractor interactor =
                    controller.World!.ScoutServiceInteractor!;
                InvokeInteractorUpdate(interactor);
                Assert.That(interactor.IsInputArmed, Is.True);
                long sequence = controller.State!.NextCommandSequence;

                if (index == 0)
                {
                    Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                    Press(keyboard.eKey);
                    InvokeInteractorUpdate(interactor);
                    Release(keyboard.eKey);
                }
                else if (index == 1)
                {
                    Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
                    Press(gamepad.buttonSouth);
                    InvokeInteractorUpdate(interactor);
                    Release(gamepad.buttonSouth);
                }
                else
                {
                    Vector3 screen = controller.World.MainCamera!
                        .WorldToScreenPoint(interactor.ControlWorldPosition);
                    Assert.That(screen.z, Is.GreaterThan(0f));
                    Assert.That(
                        interactor.TryActivateAtScreenPosition(
                            new Vector2(screen.x, screen.y)),
                        Is.True);
                }

                AssertExactServiceCommand(controller, sequence);
                InvokeSimulationTick(controller);
                Assert.That(
                    controller.ReadModel!.VehicleConditionMilli,
                    Is.EqualTo(1000),
                    item.Item1.ToString());
                Assert.That(
                    interactor.IsAcceptedReceiptVisible,
                    Is.True,
                    item.Item1.ToString());
                Object.DestroyImmediate(controller.gameObject);
            }
        }

        private LastBearingGameController CreateController(
            ColonyComposition composition)
        {
            var root = new GameObject(
                LastBearingGameController.RuntimeRootName);
            _roots.Add(root);
            var controller =
                root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(composition);
            InstallTemporarySaveAdapter(controller);
            return controller;
        }

        private void InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetSafeTempRoot(),
                "vgr23-scout-service-" + Guid.NewGuid().ToString("N"));
            string profile = Path.Combine(
                root,
                LastBearingProfileContract.ProfileName);
            Directory.CreateDirectory(root);
            _saveRoots.Add(root);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            ConstructorInfo? constructor =
                typeof(LastBearingSaveAdapter).GetConstructor(
                    flags,
                    null,
                    new[] { typeof(LastBearingProfileStore) },
                    null);
            FieldInfo? field =
                typeof(LastBearingGameController).GetField(
                    "_saveAdapter",
                    flags);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(field, Is.Not.Null);
            field!.SetValue(
                controller,
                constructor!.Invoke(new object[] { store }));
        }

        private static LastBearingState CreateServiceReadyState(
            ColonyComposition composition,
            string resident,
            int seed,
            bool viaBarter = false)
        {
            PreparationChoice preparation = viaBarter
                ? PreparationChoice.CivicBuffer
                : PreparationChoice.WorkshopPush;
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(composition, seed);
            state = Apply(
                state,
                sequence => new AssignResidentCommand(sequence, resident));
            state = Apply(
                state,
                sequence =>
                    new ActivateSliceInfrastructureCommand(sequence));
            state = Apply(
                state,
                sequence => new InstallRigUpgradeCommand(
                    sequence,
                    RigUpgrade.PatchworkSkidPlate));
            state = Apply(
                state,
                sequence => new SelectPreparationCommand(
                    sequence,
                    preparation,
                    VehicleModule.WinchAssembly));
            state = Apply(
                state,
                sequence => new InstallVehicleModuleCommand(
                    sequence,
                    VehicleModule.WinchAssembly));
            while (LastBearingReadModel.FromState(state).PreparationPhase !=
                   PreparationPhase.Ready)
            {
                state = Advance(state);
            }

            string transactionId = "tx:vgr23:" + seed;
            string fingerprint = "fp:vgr23:" + seed;
            state = Apply(
                state,
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = Apply(
                state,
                sequence => new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            while (!LastBearingReadModel.FromState(state)
                       .IsDepotApproachRecoveryAvailable)
            {
                LastBearingReadModel model =
                    LastBearingReadModel.FromState(state);
                if (model.IsWreckLineModulePointAvailable)
                {
                    state = Apply(
                        state,
                        sequence => new OperateWreckLineModuleCommand(
                            sequence,
                            model.RouteActionKind));
                    model = LastBearingReadModel.FromState(state);
                }

                if (model.IsWreckLineFrameRailRecoveryAvailable)
                {
                    state = Apply(
                        state,
                        sequence =>
                            new RecoverWreckLineFrameRailsCommand(sequence));
                }

                state = Apply(
                    state,
                    sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            state = Apply(
                state,
                sequence =>
                    new OperateDepotRecoveryPointCommand(sequence));
            state = Apply(
                state,
                sequence => new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            state = Apply(
                state,
                sequence => new LoadDepotRepairCargoCommand(sequence));
            state = Apply(
                state,
                sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            while (LastBearingReadModel.FromState(state).ExpeditionPhase !=
                   ExpeditionPhase.Returned)
            {
                state = Apply(
                    state,
                    sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            state = Apply(
                state,
                sequence => new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = Apply(
                state,
                sequence => new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            state = Apply(
                state,
                sequence =>
                    new InstallTurbineRepairCommand(sequence));
            LastBearingReadModel repaired =
                LastBearingReadModel.FromState(state);
            if (repaired.NextCityDecision ==
                NextCityDecision.RefurbishAuxiliaryPump)
            {
                state = Apply(
                    state,
                    sequence => new InstallCityImprovementCommand(
                        sequence,
                        NextCityDecision.RefurbishAuxiliaryPump,
                        LastBearingState.AuxiliaryPumpSocketId,
                        LastBearingState
                            .AuxiliaryPumpOrientationQuarterTurns));
            }
            else if (repaired.NextCityDecision ==
                     NextCityDecision.MachineSpareBearing)
            {
                state = Apply(
                    state,
                    sequence =>
                        new StartSpareBearingBatchCommand(sequence));
                while (LastBearingReadModel.FromState(state)
                           .SpareBearingBatchPhase !=
                       SpareBearingBatchPhase.Complete)
                {
                    state = Advance(state);
                }

                state = Apply(
                    state,
                    sequence =>
                        new BarterSpareBearingLotCommand(sequence));
            }
            else
            {
                Assert.Fail(
                    "Unexpected service prerequisite: " +
                    repaired.NextCityDecision);
            }
            if (state.PauseCause == PauseCause.None)
            {
                state = Apply(
                    state,
                    sequence => new SetPauseCommand(sequence, true));
            }

            LastBearingReadModel result =
                LastBearingReadModel.FromState(state);
            Assert.That(result.IsVehicleServiceNeeded, Is.True);
            Assert.That(result.IsVehicleServiceAvailable, Is.True);
            Assert.That(
                result.NextObjective,
                Is.EqualTo("service-scout-in-garage"));
            return state;
        }

        private static void AssertReadyPresentation(
            LastBearingGameController controller,
            LastBearingScoutServiceInteractor interactor,
            LastBearingReadModel model)
        {
            Assert.That(model.VehicleConditionMilli, Is.LessThan(1000));
            Assert.That(interactor.HasDedicatedInteractionTarget, Is.True);
            Assert.That(interactor.IsWitnessVisible, Is.True);
            Assert.That(interactor.IsAcceptedReceiptVisible, Is.False);
            Assert.That(
                interactor.DisplayedConditionMilli,
                Is.EqualTo(model.VehicleConditionMilli));
            Assert.That(
                interactor.DisplayedCostPartsUnits,
                Is.EqualTo(model.VehicleServicePartsCostUnits));
            Assert.That(
                interactor.DisplayedReservePartsUnits,
                Is.EqualTo(model.VehicleServiceReservePartsUnits));
            Assert.That(
                interactor.ReceiptLabel,
                Does.Contain(model.VehicleConditionMilli + " / 1000"));
            Assert.That(
                LastBearingPermitJobPresenter
                    .Present(model, cityNeedInspected: true)
                    .Headline,
                Is.EqualTo("Put the road wear right"));
            Assert.That(controller.IsScoutServiceFocused, Is.True);
        }

        private static void AssertPreserved(
            LastBearingState before,
            LastBearingState after)
        {
            Assert.That(after.Composition, Is.EqualTo(before.Composition));
            Assert.That(after.VehicleModule, Is.EqualTo(before.VehicleModule));
            Assert.That(after.RigUpgrade, Is.EqualTo(before.RigUpgrade));
            Assert.That(
                after.RepairCargoKind,
                Is.EqualTo(before.RepairCargoKind));
            Assert.That(
                after.RepairCargoCustody,
                Is.EqualTo(before.RepairCargoCustody));
            Assert.That(
                after.HeavyCargoKind,
                Is.EqualTo(before.HeavyCargoKind));
            Assert.That(
                after.HeavyCargoCustody,
                Is.EqualTo(before.HeavyCargoCustody));
            Assert.That(
                after.FactionMemory,
                Is.EqualTo(before.FactionMemory));
            Assert.That(
                after.FactionTrust,
                Is.EqualTo(before.FactionTrust));
            Assert.That(
                after.FactionGrievance,
                Is.EqualTo(before.FactionGrievance));
            Assert.That(
                after.InstalledCityImprovement,
                Is.EqualTo(before.InstalledCityImprovement));
            Assert.That(
                after.RecyclerPadIndex,
                Is.EqualTo(before.RecyclerPadIndex));
            Assert.That(
                after.MachineShopPadIndex,
                Is.EqualTo(before.MachineShopPadIndex));
            Assert.That(
                after.EmergencyStoragePadIndex,
                Is.EqualTo(before.EmergencyStoragePadIndex));
            Assert.That(
                after.CityServiceResidentId,
                Is.EqualTo(before.CityServiceResidentId));
        }

        private static ServiceScoutCommand AssertExactServiceCommand(
            LastBearingGameController controller,
            long sequence)
        {
            LastBearingCommand[] queued =
                PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(1));
            Assert.That(queued[0], Is.TypeOf<ServiceScoutCommand>());
            var command = (ServiceScoutCommand)queued[0];
            Assert.That(command.Sequence, Is.EqualTo(sequence));
            return command;
        }

        private static void AssertExactRepeatLaunch(
            LastBearingGameController controller,
            long firstSequence,
            string completedTransactionId,
            string completedFingerprint)
        {
            LastBearingCommand[] queued =
                PendingCommands(controller).ToArray();
            Assert.That(queued, Has.Length.EqualTo(3));
            Assert.That(
                queued[0],
                Is.TypeOf<PrepareRepeatExpeditionTransactionCommand>());
            Assert.That(
                queued[1],
                Is.TypeOf<DebitCityManifestCommand>());
            Assert.That(
                queued[2],
                Is.TypeOf<DepartExpeditionCommand>());

            var prepare =
                (PrepareRepeatExpeditionTransactionCommand)queued[0];
            var debit = (DebitCityManifestCommand)queued[1];
            string suffix = firstSequence.ToString(
                CultureInfo.InvariantCulture);
            Assert.That(prepare.Sequence, Is.EqualTo(firstSequence));
            Assert.That(
                prepare.CompletedTransactionId,
                Is.EqualTo(completedTransactionId));
            Assert.That(
                prepare.CompletedFingerprint,
                Is.EqualTo(completedFingerprint));
            Assert.That(
                prepare.TransactionId,
                Is.EqualTo("tx:repeat:" + suffix));
            Assert.That(
                prepare.Fingerprint,
                Is.EqualTo("fp:repeat:" + suffix));
            Assert.That(
                debit.Sequence,
                Is.EqualTo(firstSequence + 1));
            Assert.That(
                debit.TransactionId,
                Is.EqualTo(prepare.TransactionId));
            Assert.That(
                debit.Fingerprint,
                Is.EqualTo(prepare.Fingerprint));
            Assert.That(
                queued[2].Sequence,
                Is.EqualTo(firstSequence + 2));
            Assert.That(controller.IsExpeditionCommitQueued, Is.True);
        }

        private static LastBearingState AdvanceRepeatToDepot(
            LastBearingState source)
        {
            LastBearingState state = source;
            for (var ticks = 0; ticks < 7000; ticks++)
            {
                LastBearingReadModel model =
                    LastBearingReadModel.FromState(state);
                if (model.IsDepotApproachRecoveryAvailable)
                {
                    state = Apply(
                        state,
                        sequence =>
                            new OperateDepotRecoveryPointCommand(sequence));
                    Assert.That(
                        LastBearingReadModel
                            .FromState(state)
                            .ExpeditionPhase,
                        Is.EqualTo(ExpeditionPhase.AtDepot));
                    return state;
                }

                if (model.IsWreckLineModulePointAvailable)
                {
                    state = Apply(
                        state,
                        sequence =>
                            new OperateWreckLineModuleCommand(
                                sequence,
                                model.RouteActionKind));
                    model = LastBearingReadModel.FromState(state);
                }

                if (model.IsWreckLineFrameRailRecoveryAvailable)
                {
                    state = Apply(
                        state,
                        sequence =>
                            new RecoverWreckLineFrameRailsCommand(sequence));
                }

                state = Apply(
                    state,
                    sequence =>
                        new DriveVehicleCommand(sequence, 1000, 0));
            }

            throw new InvalidOperationException(
                "Repeat circuit did not reach the depot.");
        }

        private static LastBearingState AdvanceRepeatToHomeApron(
            LastBearingState source)
        {
            LastBearingState state = source;
            for (var ticks = 0; ticks < 7000; ticks++)
            {
                if (LastBearingReadModel
                        .FromState(state)
                        .ExpeditionPhase ==
                    ExpeditionPhase.Returned)
                {
                    return state;
                }

                state = Apply(
                    state,
                    sequence =>
                        new DriveVehicleCommand(sequence, 1000, 0));
            }

            throw new InvalidOperationException(
                "Repeat circuit did not reach the home apron.");
        }

        private static List<LastBearingCommand> PendingCommands(
            LastBearingGameController controller)
        {
            return (List<LastBearingCommand>)typeof(
                    LastBearingGameController)
                .GetField(
                    "_pendingCommands",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(controller)!;
        }

        private static void InstallControllerState(
            LastBearingGameController controller,
            LastBearingState state)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            controller.ModeCoordinator!.ClearSession();
            typeof(LastBearingGameController)
                .GetField("_state", flags)!
                .SetValue(controller, state);
            ReplaceRuntimeReadModel(
                controller,
                LastBearingReadModel.FromState(state));
            typeof(LastBearingGameController)
                .GetField("_cityNeedInspected", flags)!
                .SetValue(controller, true);
            PendingCommands(controller).Clear();
            typeof(LastBearingGameController)
                .GetMethod("ResetPublicSnapshotsToRuntime", flags)!
                .Invoke(controller, null);
            InvokeApplyPresentation(controller);
        }

        private static void ReplaceRuntimeReadModel(
            LastBearingGameController controller,
            LastBearingReadModel model)
        {
            typeof(LastBearingGameController)
                .GetField(
                    "_readModel",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(controller, model);
        }

        private static LastBearingReadModel RuntimeReadModel(
            LastBearingGameController controller)
        {
            return (LastBearingReadModel)typeof(
                    LastBearingGameController)
                .GetField(
                    "_readModel",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(controller)!;
        }

        private static void InvokeApplyPresentation(
            LastBearingGameController controller)
        {
            typeof(LastBearingGameController)
                .GetMethod(
                    "ApplyPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(controller, null);
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            typeof(LastBearingGameController)
                .GetMethod(
                    "SimulateOneTick",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(controller, null);
        }

        private static void InvokeInteractorUpdate(
            LastBearingScoutServiceInteractor interactor)
        {
            typeof(LastBearingScoutServiceInteractor)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(interactor, null);
        }

        private static void InvokeGarageDepartureUpdate(
            LastBearingGarageDepartureInteractor interactor)
        {
            typeof(LastBearingGarageDepartureInteractor)
                .GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(interactor, null);
        }

        private static LastBearingState Apply(
            LastBearingState state,
            Func<long, LastBearingCommand> create)
        {
            return new LastBearingKernel()
                .Step(
                    state,
                    new[] { create(state.NextCommandSequence) })
                .State;
        }

        private static LastBearingState Advance(LastBearingState state)
        {
            return new LastBearingKernel()
                .Step(state, Array.Empty<LastBearingCommand>())
                .State;
        }

        private static string GetSafeTempRoot()
        {
            string root = Path.GetTempPath();
            return (Application.platform == RuntimePlatform.OSXEditor ||
                    Application.platform == RuntimePlatform.OSXPlayer) &&
                   root.StartsWith("/var/", StringComparison.Ordinal)
                ? "/private" + root
                : root;
        }
    }
}
