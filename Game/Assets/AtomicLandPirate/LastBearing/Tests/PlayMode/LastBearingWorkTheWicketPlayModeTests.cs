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
    /// <summary>
    /// VGR-12 acceptance through the public Unity controller. The golden path
    /// never injects canonical state or stages the cutaway view directly.
    /// </summary>
    public sealed class LastBearingWorkTheWicketPlayModeTests : InputTestFixture
    {
        private const string SceneName = "LastBearing";

        private readonly List<string> _temporarySaveRoots =
            new List<string>();

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene(
                    "LastBearing_WorkTheWicket_TestCleanup");
                SceneManager.SetActiveScene(cleanup);
                AsyncOperation? unload = SceneManager.UnloadSceneAsync(scene);
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
            yield return null;
        }

        [UnityTest]
        public IEnumerator GoldenPathWorksThePhysicalWicketAndReloadsEveryCheckpoint()
        {
            yield return LoadScene();
            LastBearingGameController controller = RequireController();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();

            yield return AdvanceToWorkshopReady(
                controller,
                keyboard,
                ColonyComposition.Mixed);

            LastBearingWorldBuilder world = controller.World!;
            LastBearingOneGoodBatchCutawayView workshop =
                world.OneGoodBatchCutawayView!;
            Assert.That(world.IsPumpHallCutawaySelected, Is.True);
            Assert.That(controller.IsWorkshopBatchStartAvailable, Is.False);
            Assert.That(controller.ReadModel!.IsSpareBearingBatchStartAvailable, Is.True);

            byte[] readyBytes = CanonicalBytes(controller);
            IReadOnlyDictionary<string, string> readySave =
                SnapshotSaveFiles(profileDirectory);
            controller.StartSpareBearingBatch();
            Assert.That(PendingCommands(controller), Is.Empty);
            CollectionAssert.AreEqual(readyBytes, CanonicalBytes(controller));
            AssertSaveSnapshot(readySave, SnapshotSaveFiles(profileDirectory));

            controller.ShowCityOverview();
            controller.StartSpareBearingBatch();
            Assert.That(PendingCommands(controller), Is.Empty);
            CollectionAssert.AreEqual(readyBytes, CanonicalBytes(controller));
            AssertSaveSnapshot(readySave, SnapshotSaveFiles(profileDirectory));

            controller.OpenOneGoodBatchWorkshop();
            Assert.That(world.IsOneGoodBatchCutawaySelected, Is.True);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.BuildingCutaway));
            Assert.That(controller.IsWorkshopBatchStartAvailable, Is.True);
            CollectionAssert.AreEqual(readyBytes, CanonicalBytes(controller));
            AssertSaveSnapshot(readySave, SnapshotSaveFiles(profileDirectory));
            AssertWorkshopStock(
                workshop,
                input: true,
                work: false,
                output: false,
                claims: false,
                permit: false);
            Assert.That(workshop.IsHumanWorkerVisible, Is.True);
            Assert.That(workshop.IsRobotWorkerVisible, Is.True);
            for (var repeat = 0; repeat < 2; repeat++)
            {
                controller.ShowCityOverview();
                controller.OpenOneGoodBatchWorkshop();
            }

            CollectionAssert.AreEqual(readyBytes, CanonicalBytes(controller));
            AssertSaveSnapshot(readySave, SnapshotSaveFiles(profileDirectory));

            AssertCheckpointRoundTrip(
                controller,
                profileDirectory,
                SpareBearingBatchPhase.None,
                SpareBearingLotCustody.None,
                batchAvailable: true,
                barterAvailable: false);

            long sequenceBeforeStart = controller.State!.NextCommandSequence;
            byte[] beforeStart = CanonicalBytes(controller);
            IReadOnlyDictionary<string, string> beforeStartSave =
                SnapshotSaveFiles(profileDirectory);
            Press(keyboard.eKey);
            yield return null;
            InvokeGlobalShortcuts(controller);
            InvokeGlobalShortcuts(controller);
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<StartSpareBearingBatchCommand>());
            controller.StartSpareBearingBatch();
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            CollectionAssert.AreEqual(beforeStart, CanonicalBytes(controller));
            AssertSaveSnapshot(beforeStartSave, SnapshotSaveFiles(profileDirectory));
            Release(keyboard.eKey);
            yield return null;
            InvokeSimulationTick(controller);

            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(
                controller.State!.NextCommandSequence,
                Is.EqualTo(sequenceBeforeStart + 1));
            Assert.That(
                controller.ReadModel!.SpareBearingBatchPhase,
                Is.EqualTo(SpareBearingBatchPhase.InProgress));
            Assert.That(controller.ReadModel.SpareBearingElapsedTicks, Is.Zero);
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(14));
            AssertWorkshopStock(
                workshop,
                input: false,
                work: true,
                output: false,
                claims: false,
                permit: false);
            AssertRejectedWorkshopRequestsPreserve(
                controller,
                profileDirectory,
                rejectStart: true,
                rejectBarter: true);

            AssertCheckpointRoundTrip(
                controller,
                profileDirectory,
                SpareBearingBatchPhase.InProgress,
                SpareBearingLotCustody.None,
                batchAvailable: false,
                barterAvailable: false);

            for (var tick = 1; tick <= 120; tick++)
            {
                InvokeSimulationTick(controller);
                if (tick == 60)
                {
                    Assert.That(
                        controller.ReadModel!.SpareBearingElapsedTicks,
                        Is.EqualTo(60));
                    Assert.That(workshop.IsMachineRunning, Is.True);
                    Assert.That(workshop.IsWorkpieceVisible, Is.True);
                }
            }

            Assert.That(
                controller.ReadModel!.SpareBearingBatchPhase,
                Is.EqualTo(SpareBearingBatchPhase.Complete));
            Assert.That(controller.ReadModel.SpareBearingElapsedTicks, Is.EqualTo(120));
            Assert.That(controller.ReadModel.SpareBearingLotQuantity, Is.EqualTo(1));
            Assert.That(
                controller.ReadModel.SpareBearingLotCustody,
                Is.EqualTo(SpareBearingLotCustody.WorkshopOutput));
            Assert.That(world.IsOneGoodBatchCutawaySelected, Is.True);
            AssertWorkshopStock(
                workshop,
                input: false,
                work: false,
                output: true,
                claims: false,
                permit: false);
            AssertRejectedWorkshopRequestsPreserve(
                controller,
                profileDirectory,
                rejectStart: true,
                rejectBarter: false);

            AssertCheckpointRoundTrip(
                controller,
                profileDirectory,
                SpareBearingBatchPhase.Complete,
                SpareBearingLotCustody.WorkshopOutput,
                batchAvailable: false,
                barterAvailable: true);

            byte[] completeBytes = CanonicalBytes(controller);
            IReadOnlyDictionary<string, string> completeSave =
                SnapshotSaveFiles(profileDirectory);
            world.SelectPumpHallCutaway();
            controller.OpenBuildingCutaway();
            Assert.That(world.IsOneGoodBatchCutawaySelected, Is.False);
            Assert.That(controller.IsWorkshopBarterAvailable, Is.False);
            controller.BarterSpareBearingLot();
            Assert.That(PendingCommands(controller), Is.Empty);

            controller.OpenOneGoodBatchWorkshop();
            controller.ShowCityOverview();
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(controller.IsWorkshopBarterAvailable, Is.False);
            controller.BarterSpareBearingLot();
            Assert.That(PendingCommands(controller), Is.Empty);
            CollectionAssert.AreEqual(completeBytes, CanonicalBytes(controller));
            AssertSaveSnapshot(
                completeSave,
                SnapshotSaveFiles(profileDirectory));

            controller.OpenOneGoodBatchWorkshop();
            Assert.That(controller.IsWorkshopBarterAvailable, Is.True);

            long sequenceBeforeBarter = controller.State!.NextCommandSequence;
            Press(gamepad.buttonSouth);
            yield return null;
            InvokeGlobalShortcuts(controller);
            InvokeGlobalShortcuts(controller);
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            Assert.That(
                PendingCommands(controller)[0],
                Is.TypeOf<BarterSpareBearingLotCommand>());
            controller.BarterSpareBearingLot();
            Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
            Release(gamepad.buttonSouth);
            yield return null;
            InvokeSimulationTick(controller);

            LastBearingReadModel settled = controller.ReadModel!;
            Assert.That(
                controller.State!.NextCommandSequence,
                Is.EqualTo(sequenceBeforeBarter + 1));
            Assert.That(
                settled.SpareBearingBatchPhase,
                Is.EqualTo(SpareBearingBatchPhase.Settled));
            Assert.That(settled.SpareBearingLotQuantity, Is.EqualTo(1));
            Assert.That(
                settled.SpareBearingLotCustody,
                Is.EqualTo(SpareBearingLotCustody.LastBearingClaimsCounter));
            Assert.That(settled.RoutePermitGranted, Is.True);
            Assert.That(settled.FutureRouteTollFuelUnits, Is.EqualTo(2));
            Assert.That(settled.FactionGrievance, Is.GreaterThan(0));
            Assert.That(settled.NextObjective, Is.EqualTo("route-permit-recorded"));
            AssertWorkshopStock(
                workshop,
                input: false,
                work: false,
                output: false,
                claims: true,
                permit: true);
            AssertRejectedWorkshopRequestsPreserve(
                controller,
                profileDirectory,
                rejectStart: true,
                rejectBarter: true);

            Assert.That(settled.GlobalTick, Is.EqualTo(1152));
            Assert.That(settled.SettlementTick, Is.EqualTo(999));
            Assert.That(settled.FactionTick, Is.EqualTo(999));
            Assert.That(settled.CrisisTick, Is.EqualTo(999));
            Assert.That(settled.RoadTick, Is.EqualTo(306));
            Assert.That(controller.State!.NextCommandSequence, Is.EqualTo(317));
            Assert.That(settled.PartsUnits, Is.EqualTo(14));
            Assert.That(settled.WaterMilli, Is.EqualTo(129330));

            AssertCheckpointRoundTrip(
                controller,
                profileDirectory,
                SpareBearingBatchPhase.Settled,
                SpareBearingLotCustody.LastBearingClaimsCounter,
                batchAvailable: false,
                barterAvailable: false);
            AssertWorkshopStock(
                workshop,
                input: false,
                work: false,
                output: false,
                claims: true,
                permit: true);
            Assert.That(
                UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                UnityEngine.Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EveryCompositionUsesTheSameWorkshopCommandsAndWorkers()
        {
            yield return LoadScene();
            LastBearingGameController controller = RequireController();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            ColonyComposition[] compositions =
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            };

            foreach (ColonyComposition composition in compositions)
            {
                yield return AdvanceToWorkshopReady(
                    controller,
                    keyboard,
                    composition);
                controller.OpenOneGoodBatchWorkshop();
                Assert.That(controller.IsWorkshopBatchStartAvailable, Is.True);
                AssertCheckpointRoundTrip(
                    controller,
                    profileDirectory,
                    SpareBearingBatchPhase.None,
                    SpareBearingLotCustody.None,
                    batchAvailable: true,
                    barterAvailable: false);
                if (composition == ColonyComposition.RobotOnly)
                {
                    Press(gamepad.buttonSouth);
                    yield return null;
                    InvokeGlobalShortcuts(controller);
                    Release(gamepad.buttonSouth);
                    yield return null;
                    Assert.That(PendingCommands(controller), Has.Count.EqualTo(1));
                    Assert.That(
                        PendingCommands(controller).Single(),
                        Is.TypeOf<StartSpareBearingBatchCommand>());
                }
                else
                {
                    controller.StartSpareBearingBatch();
                }

                InvokeSimulationTick(controller);
                AssertCheckpointRoundTrip(
                    controller,
                    profileDirectory,
                    SpareBearingBatchPhase.InProgress,
                    SpareBearingLotCustody.None,
                    batchAvailable: false,
                    barterAvailable: false);
                if (composition == ColonyComposition.HumanOnly)
                {
                    IReadOnlyDictionary<string, string> pausedSave =
                        SnapshotSaveFiles(profileDirectory);
                    controller.TogglePause();
                    InvokeSimulationTick(controller);
                    long pausedElapsed =
                        controller.ReadModel!.SpareBearingElapsedTicks;
                    LastBearingOneGoodBatchCutawayView pausedWorkshop =
                        controller.World!.OneGoodBatchCutawayView!;
                    Assert.That(pausedWorkshop.IsMachineRunning, Is.False);
                    Assert.That(pausedWorkshop.IsWorkpieceVisible, Is.True);
                    for (var pausedTick = 0; pausedTick < 3; pausedTick++)
                    {
                        InvokeSimulationTick(controller);
                    }

                    Assert.That(
                        controller.ReadModel.SpareBearingElapsedTicks,
                        Is.EqualTo(pausedElapsed));
                    controller.TogglePause();
                    InvokeSimulationTick(controller);
                    AssertSaveSnapshot(
                        pausedSave,
                        SnapshotSaveFiles(profileDirectory));
                }

                var batchTicks = 0;
                while (controller.ReadModel!.SpareBearingBatchPhase ==
                           SpareBearingBatchPhase.InProgress &&
                       batchTicks < 130)
                {
                    InvokeSimulationTick(controller);
                    batchTicks++;
                }

                Assert.That(controller.IsWorkshopBarterAvailable, Is.True);
                AssertCheckpointRoundTrip(
                    controller,
                    profileDirectory,
                    SpareBearingBatchPhase.Complete,
                    SpareBearingLotCustody.WorkshopOutput,
                    batchAvailable: false,
                    barterAvailable: true);
                if (composition == ColonyComposition.HumanOnly)
                {
                    Press(keyboard.eKey);
                    yield return null;
                    InvokeGlobalShortcuts(controller);
                    Release(keyboard.eKey);
                    yield return null;
                }
                else
                {
                    controller.BarterSpareBearingLot();
                }

                InvokeSimulationTick(controller);
                LastBearingReadModel model = controller.ReadModel!;
                LastBearingOneGoodBatchCutawayView workshop =
                    controller.World!.OneGoodBatchCutawayView!;
                Assert.That(model.Composition, Is.EqualTo(composition));
                Assert.That(
                    model.SpareBearingBatchPhase,
                    Is.EqualTo(SpareBearingBatchPhase.Settled));
                Assert.That(model.RoutePermitGranted, Is.True);
                Assert.That(model.PartsUnits, Is.EqualTo(14));
                Assert.That(
                    workshop.IsHumanWorkerVisible,
                    Is.EqualTo(composition != ColonyComposition.RobotOnly));
                Assert.That(
                    workshop.IsRobotWorkerVisible,
                    Is.EqualTo(composition != ColonyComposition.HumanOnly));
                AssertCheckpointRoundTrip(
                    controller,
                    profileDirectory,
                    SpareBearingBatchPhase.Settled,
                    SpareBearingLotCustody.LastBearingClaimsCounter,
                    batchAvailable: false,
                    barterAvailable: false);
            }
        }

        [UnityTest]
        public IEnumerator EarlyFailedLoadAndTitleRequestsNeverWrite()
        {
            yield return LoadScene();
            LastBearingGameController controller = RequireController();
            controller.enabled = false;
            string profileDirectory = InstallTemporarySaveAdapter(controller);
            controller.StartNewGame(ColonyComposition.HumanOnly);
            byte[] initial = CanonicalBytes(controller);
            controller.StartSpareBearingBatch();
            controller.BarterSpareBearingLot();
            controller.OpenOneGoodBatchWorkshop();
            controller.Load();
            CollectionAssert.AreEqual(initial, CanonicalBytes(controller));
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(Directory.Exists(profileDirectory), Is.False);
            Assert.That(
                controller.SaveStatus,
                Is.EqualTo("Load refused: " + LastBearingSaveCodes.NoProfile));

            controller.ReturnToTitle();
            controller.StartSpareBearingBatch();
            controller.BarterSpareBearingLot();
            controller.OpenOneGoodBatchWorkshop();
            controller.Load();
            Assert.That(controller.HasActiveGame, Is.False);
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(Directory.Exists(profileDirectory), Is.False);
        }

        private IEnumerator AdvanceToWorkshopReady(
            LastBearingGameController controller,
            Keyboard keyboard,
            ColonyComposition composition)
        {
            controller.StartNewGame(composition);
            Assert.That(controller.ReadModel!.GlobalTick, Is.EqualTo(1));
            Assert.That(PendingCommands(controller), Is.Empty);
            controller.InspectCityNeed();
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);
            Assert.That(controller.HasCompletedCityGrammarObservation, Is.True);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);

            controller.ChoosePlan(
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly);
            var preparationTicks = 0;
            while (controller.ReadModel!.PreparationPhase != PreparationPhase.Ready &&
                   preparationTicks < 1000)
            {
                InvokeSimulationTick(controller);
                preparationTicks++;
            }

            Assert.That(preparationTicks, Is.EqualTo(720));
            Assert.That(
                controller.ReadModel!.VehicleModule,
                Is.EqualTo(VehicleModule.WinchAssembly));
            controller.OpenGarageBay();
            controller.CommitExpedition();
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.Outbound));

            Press(keyboard.wKey);
            yield return null;
            var firstRoadTicks = 0;
            while (!controller.ReadModel.IsWreckLineModulePointAvailable &&
                   firstRoadTicks < 200)
            {
                InvokeSimulationTick(controller);
                firstRoadTicks++;
            }

            Release(keyboard.wKey);
            yield return null;
            Assert.That(firstRoadTicks, Is.EqualTo(75));
            controller.OperateWreckLineModulePoint();
            InvokeSimulationTick(controller);
            Assert.That(controller.ReadModel.RouteActionUsed, Is.True);
            Assert.That(
                controller.ReadModel.HeavyCargoCustody,
                Is.EqualTo(HeavyCargoCustody.Vehicle));

            Press(keyboard.wKey);
            yield return null;
            var secondRoadTicks = 0;
            while (!controller.ReadModel.IsDepotApproachRecoveryAvailable &&
                   secondRoadTicks < 200)
            {
                InvokeSimulationTick(controller);
                secondRoadTicks++;
            }

            Release(keyboard.wKey);
            yield return null;
            Assert.That(secondRoadTicks, Is.EqualTo(75));
            controller.OperateDepotApproachRecoveryPoint();
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.ExpeditionPhase,
                Is.EqualTo(ExpeditionPhase.AtDepot));
            controller.ResolveDepot(cooperate: false);
            InvokeSimulationTick(controller);
            controller.LoadDepotRepairCargo();
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.RepairCargoKind,
                Is.EqualTo(RepairCargoKind.CeramicBearing));
            Assert.That(
                controller.ReadModel.RepairCargoCustody,
                Is.EqualTo(RepairCargoCustody.Vehicle));
            controller.BeginReturn();
            InvokeSimulationTick(controller);

            Press(keyboard.wKey);
            yield return null;
            var returnTicks = 0;
            while (controller.ReadModel.ExpeditionPhase != ExpeditionPhase.Returned &&
                   returnTicks < 300)
            {
                InvokeSimulationTick(controller);
                returnTicks++;
            }

            Release(keyboard.wKey);
            yield return null;
            Assert.That(returnTicks, Is.EqualTo(150));
            controller.CompleteReturn();
            InvokeSimulationTick(controller);
            Assert.That(controller.World!.IsPumpHallCutawaySelected, Is.True);
            Assert.That(controller.IsPumpHallRepairAvailable, Is.True);
            controller.RepairTurbine();
            InvokeSimulationTick(controller);
            Assert.That(
                controller.ReadModel.TurbineCondition,
                Is.EqualTo(TurbineCondition.BearingRepaired));
            Assert.That(controller.ReadModel.IsSpareBearingBatchStartAvailable, Is.True);
        }

        private static void AssertRejectedWorkshopRequestsPreserve(
            LastBearingGameController controller,
            string profileDirectory,
            bool rejectStart,
            bool rejectBarter)
        {
            byte[] canonical = CanonicalBytes(controller);
            IReadOnlyDictionary<string, string> save =
                SnapshotSaveFiles(profileDirectory);
            if (rejectStart)
            {
                controller.StartSpareBearingBatch();
            }

            if (rejectBarter)
            {
                controller.BarterSpareBearingLot();
            }

            Assert.That(PendingCommands(controller), Is.Empty);
            CollectionAssert.AreEqual(canonical, CanonicalBytes(controller));
            AssertSaveSnapshot(save, SnapshotSaveFiles(profileDirectory));
        }

        private static void AssertCheckpointRoundTrip(
            LastBearingGameController controller,
            string profileDirectory,
            SpareBearingBatchPhase phase,
            SpareBearingLotCustody custody,
            bool batchAvailable,
            bool barterAvailable)
        {
            byte[] expected = CanonicalBytes(controller);
            controller.Save();
            Assert.That(
                controller.SaveStatus,
                Does.StartWith(LastBearingSaveCodes.SaveOk + " ·"),
                controller.SaveStatus);
            IReadOnlyDictionary<string, string> saved =
                SnapshotSaveFiles(profileDirectory);
            controller.ReturnToTitle();
            controller.StartSpareBearingBatch();
            controller.BarterSpareBearingLot();
            controller.OpenOneGoodBatchWorkshop();
            Assert.That(PendingCommands(controller), Is.Empty);
            AssertSaveSnapshot(saved, SnapshotSaveFiles(profileDirectory));
            controller.Load();
            CollectionAssert.AreEqual(expected, CanonicalBytes(controller));
            AssertSaveSnapshot(saved, SnapshotSaveFiles(profileDirectory));
            Assert.That(PendingCommands(controller), Is.Empty);
            Assert.That(
                controller.ReadModel!.SpareBearingBatchPhase,
                Is.EqualTo(phase));
            Assert.That(
                controller.ReadModel.SpareBearingLotCustody,
                Is.EqualTo(custody));
            Assert.That(controller.World!.IsOneGoodBatchCutawaySelected, Is.True);
            Assert.That(controller.IsWorkshopBatchStartAvailable, Is.EqualTo(batchAvailable));
            Assert.That(controller.IsWorkshopBarterAvailable, Is.EqualTo(barterAvailable));
            LastBearingOneGoodBatchCutawayView workshop =
                controller.World.OneGoodBatchCutawayView!;
            AssertWorkshopStock(
                workshop,
                input: phase == SpareBearingBatchPhase.None && batchAvailable,
                work: phase == SpareBearingBatchPhase.InProgress,
                output: custody == SpareBearingLotCustody.WorkshopOutput,
                claims: custody == SpareBearingLotCustody.LastBearingClaimsCounter,
                permit: controller.ReadModel.RoutePermitGranted);
            Assert.That(
                workshop.IsMachineRunning,
                Is.EqualTo(phase == SpareBearingBatchPhase.InProgress));
        }

        private static void AssertWorkshopStock(
            LastBearingOneGoodBatchCutawayView workshop,
            bool input,
            bool work,
            bool output,
            bool claims,
            bool permit)
        {
            Assert.That(workshop.IsInputStockVisible, Is.EqualTo(input));
            Assert.That(workshop.IsWorkpieceVisible, Is.EqualTo(work));
            Assert.That(workshop.IsBearingLotVisible, Is.EqualTo(output || claims));
            if (workshop.BearingLot != null && (output || claims))
            {
                Assert.That(
                    workshop.BearingLot.transform.parent,
                    Is.EqualTo(claims ? workshop.ClaimsAnchor : workshop.OutputAnchor));
            }

            Assert.That(workshop.IsPermitGrantedVisible, Is.EqualTo(permit));
            Assert.That(workshop.IsPermitLockedVisible, Is.EqualTo(!permit));
        }

        private static byte[] CanonicalBytes(
            LastBearingGameController controller)
        {
            return LastBearingCanonicalCodec.Encode(controller.State!);
        }

        private static IReadOnlyList<LastBearingCommand> PendingCommands(
            LastBearingGameController controller)
        {
            FieldInfo? field = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var commands = field!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(commands, Is.Not.Null);
            return commands!;
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            InvokePrivate(controller, "SimulateOneTick");
        }

        private static void InvokeGlobalShortcuts(
            LastBearingGameController controller)
        {
            InvokePrivate(controller, "HandleGlobalShortcuts");
        }

        private static void InvokePrivate(
            LastBearingGameController controller,
            string methodName)
        {
            MethodInfo? method = typeof(LastBearingGameController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(controller, null);
        }

        private IEnumerator LoadScene()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
        }

        private static LastBearingGameController RequireController()
        {
            LastBearingGameController? controller =
                UnityEngine.Object.FindAnyObjectByType<LastBearingGameController>();
            Assert.That(controller, Is.Not.Null);
            return controller!;
        }

        private string InstallTemporarySaveAdapter(
            LastBearingGameController controller)
        {
            string root = Path.Combine(
                GetConfinementSafeTemporaryRoot(),
                "wp0002-work-the-wicket-" + Guid.NewGuid().ToString("N"));
            string profileDirectory = Path.Combine(
                root,
                LastBearingProfileContract.ProfileName);
            Directory.CreateDirectory(root);
            _temporarySaveRoots.Add(root);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profileDirectory);
            ConstructorInfo? constructor = typeof(LastBearingSaveAdapter).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(LastBearingProfileStore) },
                modifiers: null);
            FieldInfo? adapterField = typeof(LastBearingGameController).GetField(
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

        private static string GetConfinementSafeTemporaryRoot()
        {
            string root = Path.GetTempPath();
            bool mac = Application.platform == RuntimePlatform.OSXEditor ||
                       Application.platform == RuntimePlatform.OSXPlayer;
            return mac && root.StartsWith("/var/", StringComparison.Ordinal)
                ? "/private" + root
                : root;
        }

        private static IReadOnlyDictionary<string, string> SnapshotSaveFiles(
            string profileDirectory)
        {
            Assert.That(Directory.Exists(profileDirectory), Is.True);
            return Directory.GetFiles(
                    profileDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToDictionary(
                    path => Path.GetRelativePath(profileDirectory, path),
                    path => Convert.ToBase64String(File.ReadAllBytes(path)),
                    StringComparer.Ordinal);
        }

        private static void AssertSaveSnapshot(
            IReadOnlyDictionary<string, string> expected,
            IReadOnlyDictionary<string, string> actual)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Assert.That(
                    actual[pair.Key],
                    Is.EqualTo(pair.Value),
                    "presentation-only workshop routing changed " + pair.Key);
            }
        }
    }
}
