#nullable enable

using System;
using System.IO;
using System.Linq;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class SaveAtomicTests
    {
        public static void Run(TestHarness harness, string repoRoot)
        {
            harness.Run("frozen generation and pointer vector", FrozenVector);
            harness.Run("four preparation and module checkpoints round trip", () =>
                PreparationModuleCheckpointsRoundTrip(repoRoot));
            harness.Run("repair source and loaded save checkpoints round trip", () =>
                RepairCargoCheckpointsRoundTrip(repoRoot));
            harness.Run("installed auxiliary pump checkpoint round trips", () =>
                InstalledAuxiliaryPumpCheckpointRoundTrips(repoRoot));
            RunEmergencyCisternExpansion(harness, repoRoot);
            harness.Run("atomic current and immediately preceding last-good", () =>
                CurrentAndLastGood(repoRoot));
            harness.Run("corrupt current recovers only verified last-good", () =>
                CorruptCurrentRecovery(repoRoot));
            harness.Run("corrupt current refuses a forked persist", () =>
                CorruptCurrentRefusesPersist(repoRoot));
            harness.Run("unknown current version refuses downgrade and persist", () =>
                UnknownCurrentVersionFailsClosed(repoRoot));
            harness.Run("profile load ignores sibling and staging decoys", () =>
                DecoysAreNotDiscoverable(repoRoot));
            harness.Run("faulted publication remains recoverable and retry converges", () =>
                FaultedPublicationConverges(repoRoot));
            harness.Run("scratch recovery cannot remove a published generation", () =>
                ScratchRecoveryRejectsPublishedGeneration(repoRoot));
            harness.Run("load does not create a missing profile", () =>
                MissingLoadIsReadOnly(repoRoot));
            RunOneGoodBatch(harness, repoRoot);
        }

        internal static void RunEmergencyCisternExpansion(
            TestHarness harness,
            string repoRoot)
        {
            harness.Run(
                "expanded Emergency Cistern checkpoint round trips",
                () => ExpandedEmergencyCisternCheckpointRoundTrips(
                    repoRoot));
        }

        internal static void RunOneGoodBatch(
            TestHarness harness,
            string repoRoot)
        {
            harness.Run(
                "one good batch save checkpoints round trip exactly",
                () => OneGoodBatchCheckpointsRoundTrip(repoRoot));
            harness.Run(
                "one good batch faulted publications recover and retry exactly",
                () => OneGoodBatchFaultedPublicationsConverge(repoRoot));
        }

        private static void FrozenVector()
        {
            byte[] payload = { 0x00, 0x01, 0xfe, 0xff };
            LastBearingGenerationRecord generation =
                LastBearingGenerationCodec.EncodeGeneration(1, payload);
            TestHarness.Equal(
                "gen-00000000000000000001-504fed45000abc9524158b12cb8e1fc14f57d05891be9ca3aa5f9a1746d6d233.lbg",
                generation.FileName.Value,
                "generation filename");
            TestHarness.Equal(
                "414c504c4247303101001300010000000000000004000000c5dbae22661af6db18a1f676db82a7ef7de46d27c3a263a872f00478b0d99fc46c6173742d62656172696e672d6465762d76310001feff",
                ToHex(generation.EncodedBytes),
                "generation bytes");
            LastBearingPointerRecord pointer =
                LastBearingGenerationCodec.EncodePointer(generation);
            TestHarness.Equal(
                "414c504c425030310100130001000000000000005d00504fed45000abc9524158b12cb8e1fc14f57d05891be9ca3aa5f9a1746d6d2336c6173742d62656172696e672d6465762d763167656e2d30303030303030303030303030303030303030312d353034666564343530303061626339353234313538623132636238653166633134663537643035383931626539636133616135663961313734366436643233332e6c6267",
                ToHex(pointer.EncodedBytes),
                "pointer bytes");
        }

        private static void PreparationModuleCheckpointsRoundTrip(string repoRoot)
        {
            foreach (PreparationChoice choice in new[]
            {
                PreparationChoice.WorkshopPush,
                PreparationChoice.CivicBuffer,
            })
            {
                foreach (VehicleModule module in new[]
                {
                    VehicleModule.WinchAssembly,
                    VehicleModule.SealedRangeTank,
                })
                {
                    var driver = new CoreTestDriver(ColonyComposition.HumanOnly, 2012);
                    driver.StartPreparation(
                        ResidentRoster.HumanResidentId,
                        choice,
                        module);
                    int guard = 0;
                    while (driver.View.PreparationPhase != PreparationPhase.Ready &&
                           guard < 1000)
                    {
                        driver.Advance(1);
                        guard++;
                    }

                    TestHarness.Equal(
                        PreparationPhase.Ready,
                        driver.View.PreparationPhase,
                        choice + "/" + module + " preparation checkpoint");
                    byte[] canonical = LastBearingCanonicalCodec.Encode(driver.State);
                    string profile = FreshProfile(
                        repoRoot,
                        "checkpoint-" + choice + "-" + module);
                    LastBearingProfileStore store =
                        LastBearingProfileStore.OpenFixedProfileDirectory(profile);
                    LastBearingPersistResult persisted = store.TryPersist(canonical);
                    TestHarness.True(
                        persisted.Succeeded,
                        choice + "/" + module + " checkpoint persist failed");
                    LastBearingLoadResult loaded = store.TryLoad(payload =>
                        LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
                    TestHarness.True(
                        loaded.Succeeded && loaded.CanonicalPayload != null,
                        choice + "/" + module + " checkpoint load failed");
                    TestHarness.True(
                        canonical.SequenceEqual(loaded.CanonicalPayload!),
                        choice + "/" + module + " canonical bytes changed");
                    LastBearingDecodeResult decoded =
                        LastBearingCanonicalCodec.TryDecode(loaded.CanonicalPayload!);
                    TestHarness.True(
                        decoded.Succeeded && decoded.State != null,
                        choice + "/" + module + " canonical decode failed");
                    LastBearingReadModel restored =
                        LastBearingReadModel.FromState(decoded.State!);
                    TestHarness.Equal(choice, restored.PreparationChoice, "restored preparation");
                    TestHarness.Equal(module, restored.VehicleModule, "restored module");
                }
            }
        }

        private static void RepairCargoCheckpointsRoundTrip(string repoRoot)
        {
            RoundTripRepairCargoPath(
                repoRoot,
                "sleeve-faction",
                EncounterChoice.Cooperate,
                waitForFactionClaim: false,
                worldSeed: 2004,
                RepairCargoCustody.Faction);
            RoundTripRepairCargoPath(
                repoRoot,
                "bearing-depot",
                EncounterChoice.TakeBearing,
                waitForFactionClaim: false,
                worldSeed: 2005,
                RepairCargoCustody.Depot);
            RoundTripRepairCargoPath(
                repoRoot,
                "bearing-faction",
                EncounterChoice.TakeBearing,
                waitForFactionClaim: true,
                worldSeed: 2003,
                RepairCargoCustody.Faction);
        }

        private static void RoundTripRepairCargoPath(
            string repoRoot,
            string caseName,
            EncounterChoice choice,
            bool waitForFactionClaim,
            int worldSeed,
            RepairCargoCustody expectedSource)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                worldSeed);
            if (waitForFactionClaim)
            {
                driver.Advance(checked((int)
                    LastBearingBalanceV1.DustFrontThresholdCrisisTicks));
                driver.Apply(sequence =>
                    new AcknowledgeDustFrontCommand(sequence));
                driver.Advance(3000);
                TestHarness.Equal(
                    FactionClaimState.Claimed,
                    driver.View.FactionClaimState,
                    caseName + " faction claim setup");
            }

            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:repair-save:" + worldSeed;
            string fingerprint = "fp:repair-save:" + worldSeed;
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            while (!driver.View.IsDepotApproachRecoveryAvailable)
            {
                driver.OperateWreckLineIfAvailable();
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new ResolveDepotCommand(
                sequence,
                choice));
            LastBearingState source = RoundTripRepairCargoCheckpoint(
                repoRoot,
                "repair-source-" + caseName,
                driver.State,
                expectedSource,
                interactionAvailable: true);

            driver = new CoreTestDriver(source);
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            RoundTripRepairCargoCheckpoint(
                repoRoot,
                "repair-loaded-" + caseName,
                driver.State,
                RepairCargoCustody.Vehicle,
                interactionAvailable: false);
        }

        private static LastBearingState RoundTripRepairCargoCheckpoint(
            string repoRoot,
            string caseName,
            LastBearingState expected,
            RepairCargoCustody expectedCustody,
            bool interactionAvailable)
        {
            byte[] canonical = LastBearingCanonicalCodec.Encode(expected);
            string profile = FreshProfile(repoRoot, caseName);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted = store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                caseName + " persist failed: " + persisted.Code);
            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                caseName + " load failed: " + loaded.Code);
            TestHarness.True(
                canonical.SequenceEqual(loaded.CanonicalPayload!),
                caseName + " canonical bytes changed");
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(
                    loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                caseName + " decode failed: " + decoded.Code);
            LastBearingReadModel restored =
                LastBearingReadModel.FromState(decoded.State!);
            TestHarness.Equal(
                expectedCustody,
                restored.RepairCargoCustody,
                caseName + " custody");
            TestHarness.Equal(
                interactionAvailable,
                restored.IsRepairCargoLoadAvailable,
                caseName + " interaction availability");
            TestHarness.Equal(
                expected.TransactionId,
                decoded.State!.TransactionId,
                caseName + " transaction identity");
            TestHarness.Equal(
                expected.FactionGrievance,
                restored.FactionGrievance,
                caseName + " faction consequence");
            TestHarness.Equal(
                expected.GlobalTick,
                restored.GlobalTick,
                caseName + " global clock");
            return decoded.State!;
        }

        private static void CurrentAndLastGood(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "current-last-good");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            byte[] first = { 0x10, 0x11 };
            byte[] second = { 0x20, 0x21, 0x22 };
            byte[] third = { 0x30, 0x31, 0x32, 0x33 };
            TestHarness.Equal(1UL, Persist(store, first).Generation, "first generation");
            TestHarness.Equal(2UL, Persist(store, second).Generation, "second generation");
            TestHarness.Equal(3UL, Persist(store, third).Generation, "third generation");
            LastBearingPersistResult retry = Persist(store, third);
            TestHarness.True(retry.AlreadyCurrent, "idempotent retry wrote a generation");
            TestHarness.Equal(3UL, retry.Generation, "idempotent retry generation");
            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(loaded.Succeeded && !loaded.FromLastGood, "current load failed");
            TestHarness.True(third.SequenceEqual(loaded.CanonicalPayload!), "current payload differs");
        }

        private static void InstalledAuxiliaryPumpCheckpointRoundTrips(
            string repoRoot)
        {
            LastBearingState installed =
                CityImprovementTests.CreateInstalledStateForSaveTests();
            byte[] canonical = LastBearingCanonicalCodec.Encode(installed);
            string profile = FreshProfile(
                repoRoot,
                "checkpoint-installed-auxiliary-pump");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);

            LastBearingPersistResult persisted = store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                "installed auxiliary pump checkpoint persist failed");
            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                "installed auxiliary pump checkpoint load failed");
            TestHarness.True(
                canonical.SequenceEqual(loaded.CanonicalPayload!),
                "installed auxiliary pump canonical bytes changed");
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "installed auxiliary pump canonical decode failed");
            TestHarness.Equal(
                CityImprovementKind.RefurbishedAuxiliaryPump,
                decoded.State!.InstalledCityImprovement,
                "restored city improvement");
            TestHarness.Equal(
                HeavyCargoCustody.InstalledAtAuxiliaryPump,
                decoded.State.HeavyCargoCustody,
                "restored installed rotor custody");
            TestHarness.Equal(0, decoded.State.TowSlotsUsed, "restored tow slot");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeMechanicalSha256(installed),
                LastBearingCanonicalCodec.ComputeMechanicalSha256(decoded.State),
                "restored installation mechanics");
        }

        private static void ExpandedEmergencyCisternCheckpointRoundTrips(
            string repoRoot)
        {
            LastBearingState installed =
                EmergencyCisternExpansionTests
                    .CreateExpandedStateForSaveTests();
            byte[] canonical = LastBearingCanonicalCodec.Encode(installed);
            string profile = FreshProfile(
                repoRoot,
                "checkpoint-expanded-emergency-cistern");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);

            LastBearingPersistResult persisted = store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                "expanded Emergency Cistern checkpoint persist failed");
            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                "expanded Emergency Cistern checkpoint load failed");
            TestHarness.True(
                canonical.SequenceEqual(loaded.CanonicalPayload!),
                "expanded Emergency Cistern canonical bytes changed");
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(
                    loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "expanded Emergency Cistern canonical decode failed");
            TestHarness.Equal(
                9,
                decoded.State!.SchemaVersion,
                "restored schema version");
            TestHarness.Equal(
                CityImprovementKind.ExpandedEmergencyCistern,
                decoded.State.InstalledCityImprovement,
                "restored city improvement");
            TestHarness.Equal(
                LiquidCargoKind.Water,
                decoded.State.LiquidCargoKind,
                "restored liquid kind");
            TestHarness.Equal(
                LastBearingBalanceV1.TankWaterReturnMilli,
                decoded.State.LiquidCargoQuantityMilli,
                "restored liquid quantity");
            TestHarness.Equal(
                LiquidCargoCustody.Settlement,
                decoded.State.LiquidCargoCustody,
                "restored liquid custody");
            TestHarness.Equal(
                checked(
                    LastBearingBalanceV1.WaterCapacityMilli
                    + LastBearingBalanceV1
                        .EmergencyCisternExpansionCapacityMilli),
                LastBearingReadModel.FromState(decoded.State)
                    .WaterCapacityMilli,
                "restored expanded capacity");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeMechanicalSha256(installed),
                LastBearingCanonicalCodec.ComputeMechanicalSha256(
                    decoded.State),
                "restored expansion mechanics");
        }

        private static void OneGoodBatchCheckpointsRoundTrip(string repoRoot)
        {
            LastBearingState started = RoundTripOneGoodBatchCheckpoint(
                repoRoot,
                "one-good-batch-started",
                CreateOneGoodBatchState(elapsedTicks: 0, settle: false));
            AssertOneGoodBatchCheckpoint(
                started,
                SpareBearingBatchPhase.InProgress,
                0,
                0,
                SpareBearingLotCustody.None,
                routePermitGranted: false,
                "started");

            long partsAfterStart = started.PartsUnits;
            var startReplay = new CoreTestDriver(started);
            startReplay.Apply(sequence =>
                new StartSpareBearingBatchCommand(sequence));
            TestHarness.Equal(
                partsAfterStart,
                startReplay.State.PartsUnits,
                "loaded start replay debited parts");
            TestHarness.Equal(
                SpareBearingBatchPhase.InProgress,
                startReplay.State.SpareBearingBatchPhase,
                "loaded start replay changed phase");
            TestHarness.Equal(
                0L,
                startReplay.State.SpareBearingLotQuantity,
                "loaded start replay created a lot");

            LastBearingState midpoint = RoundTripOneGoodBatchCheckpoint(
                repoRoot,
                "one-good-batch-midpoint",
                CreateOneGoodBatchState(elapsedTicks: 60, settle: false));
            AssertOneGoodBatchCheckpoint(
                midpoint,
                SpareBearingBatchPhase.InProgress,
                60,
                0,
                SpareBearingLotCustody.None,
                routePermitGranted: false,
                "midpoint");

            LastBearingState completed = RoundTripOneGoodBatchCheckpoint(
                repoRoot,
                "one-good-batch-completed",
                CreateOneGoodBatchState(elapsedTicks: 120, settle: false));
            AssertOneGoodBatchCheckpoint(
                completed,
                SpareBearingBatchPhase.Complete,
                120,
                1,
                SpareBearingLotCustody.WorkshopOutput,
                routePermitGranted: false,
                "completed");
            TestHarness.Equal(
                NextCityDecision.None,
                completed.NextCityDecision,
                "completion did not consume the city decision");

            LastBearingState settled = RoundTripOneGoodBatchCheckpoint(
                repoRoot,
                "one-good-batch-settled",
                CreateOneGoodBatchState(elapsedTicks: 120, settle: true));
            AssertOneGoodBatchCheckpoint(
                settled,
                SpareBearingBatchPhase.Settled,
                120,
                1,
                SpareBearingLotCustody.LastBearingClaimsCounter,
                routePermitGranted: true,
                "settled");
            TestHarness.Equal(
                FactionAccessPolicy.PermitRequired,
                settled.FactionAccessPolicy,
                "settled permit access policy");
            TestHarness.Equal(
                2L,
                settled.FutureRouteTollFuelUnits,
                "settled future toll");

            var barterReplay = new CoreTestDriver(settled);
            long trustBeforeReplay = settled.FactionTrust;
            long grievanceBeforeReplay = settled.FactionGrievance;
            barterReplay.Apply(sequence =>
                new BarterSpareBearingLotCommand(sequence));
            TestHarness.Equal(
                1L,
                barterReplay.State.SpareBearingLotQuantity,
                "loaded barter replay duplicated or consumed the lot");
            TestHarness.Equal(
                SpareBearingLotCustody.LastBearingClaimsCounter,
                barterReplay.State.SpareBearingLotCustody,
                "loaded barter replay changed custody");
            TestHarness.Equal(
                trustBeforeReplay,
                barterReplay.State.FactionTrust,
                "loaded barter replay changed trust");
            TestHarness.Equal(
                grievanceBeforeReplay,
                barterReplay.State.FactionGrievance,
                "loaded barter replay changed grievance");
            TestHarness.Equal(
                2L,
                barterReplay.State.FutureRouteTollFuelUnits,
                "loaded barter replay changed the future toll");
        }

        private static void OneGoodBatchFaultedPublicationsConverge(
            string repoRoot)
        {
            LastBearingState initial = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2301);
            LastBearingState started = CreateOneGoodBatchState(
                elapsedTicks: 0,
                settle: false);
            LastBearingState midpoint = CreateOneGoodBatchState(
                elapsedTicks: 60,
                settle: false);
            LastBearingState completed = CreateOneGoodBatchState(
                elapsedTicks: 120,
                settle: false);
            LastBearingState settled = CreateOneGoodBatchState(
                elapsedTicks: 120,
                settle: true);
            var transitions = new[]
            {
                new OneGoodBatchSaveTransition(
                    "started",
                    initial,
                    started,
                    SpareBearingBatchPhase.InProgress,
                    0,
                    0,
                    SpareBearingLotCustody.None,
                    false),
                new OneGoodBatchSaveTransition(
                    "midpoint",
                    started,
                    midpoint,
                    SpareBearingBatchPhase.InProgress,
                    60,
                    0,
                    SpareBearingLotCustody.None,
                    false),
                new OneGoodBatchSaveTransition(
                    "completed",
                    midpoint,
                    completed,
                    SpareBearingBatchPhase.Complete,
                    120,
                    1,
                    SpareBearingLotCustody.WorkshopOutput,
                    false),
                new OneGoodBatchSaveTransition(
                    "settled",
                    completed,
                    settled,
                    SpareBearingBatchPhase.Settled,
                    120,
                    1,
                    SpareBearingLotCustody.LastBearingClaimsCounter,
                    true),
            };

            foreach (OneGoodBatchSaveTransition transition in transitions)
            {
                byte[] previous = LastBearingCanonicalCodec.Encode(
                    transition.Previous);
                byte[] attempted = LastBearingCanonicalCodec.Encode(
                    transition.Attempted);
                foreach (SaveFaultPoint point in new[]
                {
                    SaveFaultPoint.AfterGenerationStageDurableClose,
                    SaveFaultPoint.AfterGenerationPublication,
                    SaveFaultPoint.AfterLastGoodPublication,
                    SaveFaultPoint.AfterCurrentPublication,
                    SaveFaultPoint.PartialGenerationStage,
                    SaveFaultPoint.PartialLastGoodPointerStage,
                    SaveFaultPoint.PartialCurrentPointerStage,
                })
                {
                    string profile = FreshProfile(
                        repoRoot,
                        "one-good-batch-fault-" + transition.Name + "-" + point);
                    LastBearingProfileStore normal =
                        LastBearingProfileStore.OpenFixedProfileDirectory(profile);
                    Persist(normal, previous);

                    var operations = new FaultInjectingFileOperations(
                        LastBearingFileOperations.Instance,
                        point);
                    LastBearingProfileStore faulting =
                        LastBearingProfileStore.OpenFixedProfileDirectoryForTests(
                            profile,
                            operations);
                    LastBearingPersistResult interrupted =
                        faulting.TryPersist(attempted);
                    TestHarness.True(
                        operations.Fired,
                        transition.Name + " fault did not fire: " + point);
                    TestHarness.True(
                        !interrupted.Succeeded,
                        transition.Name + " fault reported success: " + point);

                    LastBearingLoadResult visible = normal.TryLoad(payload =>
                        LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
                    TestHarness.True(
                        visible.Succeeded && visible.CanonicalPayload != null,
                        transition.Name + " fault left no decodable state: " + point);
                    TestHarness.True(
                        previous.SequenceEqual(visible.CanonicalPayload!) ||
                        attempted.SequenceEqual(visible.CanonicalPayload!),
                        transition.Name + " fault exposed partial state: " + point);

                    LastBearingPersistResult retried = normal.TryPersist(attempted);
                    TestHarness.True(
                        retried.Succeeded,
                        transition.Name + " retry failed: " + point);
                    LastBearingLoadResult converged = normal.TryLoad(payload =>
                        LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
                    TestHarness.True(
                        converged.Succeeded && converged.CanonicalPayload != null &&
                        attempted.SequenceEqual(converged.CanonicalPayload),
                        transition.Name + " retry did not converge: " + point);
                    LastBearingDecodeResult decoded =
                        LastBearingCanonicalCodec.TryDecode(
                            converged.CanonicalPayload!);
                    TestHarness.True(
                        decoded.Succeeded && decoded.State != null,
                        transition.Name + " converged state did not decode: " + point);
                    AssertOneGoodBatchCheckpoint(
                        decoded.State!,
                        transition.Phase,
                        transition.ElapsedTicks,
                        transition.LotQuantity,
                        transition.Custody,
                        transition.RoutePermitGranted,
                        transition.Name + "/" + point);
                }
            }
        }

        private static LastBearingState CreateOneGoodBatchState(
            int elapsedTicks,
            bool settle)
        {
            if (elapsedTicks < 0 || elapsedTicks > 120)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTicks));
            }

            var driver = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                2301);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            const string transactionId = "tx:one-good-batch:2301";
            const string fingerprint = "fp:one-good-batch:2301";
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            while (!driver.View.IsDepotApproachRecoveryAvailable)
            {
                driver.OperateWreckLineIfAvailable();
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new ResolveDepotCommand(
                sequence,
                EncounterChoice.TakeBearing));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            while (driver.View.ExpeditionPhase != ExpeditionPhase.Returned)
            {
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new FinalizeExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));
            TestHarness.True(
                driver.View.IsSpareBearingBatchStartAvailable,
                "one good batch save fixture did not reach eligibility");
            driver.Apply(sequence =>
                new StartSpareBearingBatchCommand(sequence));
            driver.Advance(elapsedTicks);
            if (settle)
            {
                TestHarness.True(
                    driver.View.IsSpareBearingBarterAvailable,
                    "one good batch save fixture did not reach barter");
                driver.Apply(sequence =>
                    new BarterSpareBearingLotCommand(sequence));
            }

            return driver.State;
        }

        private static LastBearingState RoundTripOneGoodBatchCheckpoint(
            string repoRoot,
            string caseName,
            LastBearingState expected)
        {
            byte[] canonical = LastBearingCanonicalCodec.Encode(expected);
            string profile = FreshProfile(repoRoot, caseName);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted = store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                caseName + " persist failed: " + persisted.Code);
            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                caseName + " load failed: " + loaded.Code);
            TestHarness.True(
                canonical.SequenceEqual(loaded.CanonicalPayload!),
                caseName + " canonical bytes changed");
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                caseName + " decode failed: " + decoded.Code);
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeMechanicalSha256(expected),
                LastBearingCanonicalCodec.ComputeMechanicalSha256(decoded.State!),
                caseName + " mechanical projection changed");
            return decoded.State!;
        }

        private static void AssertOneGoodBatchCheckpoint(
            LastBearingState state,
            SpareBearingBatchPhase expectedPhase,
            long expectedElapsedTicks,
            long expectedLotQuantity,
            SpareBearingLotCustody expectedCustody,
            bool routePermitGranted,
            string label)
        {
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                state.SchemaVersion,
                label + " state schema");
            TestHarness.Equal(
                SpareBearingRecipe.SpareBearingOneGoodBatch,
                state.SpareBearingRecipe,
                label + " recipe");
            TestHarness.Equal(
                expectedPhase,
                state.SpareBearingBatchPhase,
                label + " phase");
            TestHarness.Equal(
                expectedElapsedTicks,
                state.SpareBearingElapsedTicks,
                label + " elapsed ticks");
            TestHarness.Equal(
                120L,
                state.SpareBearingRequiredTicks,
                label + " required ticks");
            TestHarness.Equal(
                expectedLotQuantity,
                state.SpareBearingLotQuantity,
                label + " lot quantity");
            TestHarness.Equal(
                expectedCustody,
                state.SpareBearingLotCustody,
                label + " lot custody");
            TestHarness.Equal(
                routePermitGranted,
                state.RoutePermitGranted,
                label + " route permit");
        }

        private sealed class OneGoodBatchSaveTransition
        {
            internal OneGoodBatchSaveTransition(
                string name,
                LastBearingState previous,
                LastBearingState attempted,
                SpareBearingBatchPhase phase,
                long elapsedTicks,
                long lotQuantity,
                SpareBearingLotCustody custody,
                bool routePermitGranted)
            {
                Name = name;
                Previous = previous;
                Attempted = attempted;
                Phase = phase;
                ElapsedTicks = elapsedTicks;
                LotQuantity = lotQuantity;
                Custody = custody;
                RoutePermitGranted = routePermitGranted;
            }

            internal string Name { get; }

            internal LastBearingState Previous { get; }

            internal LastBearingState Attempted { get; }

            internal SpareBearingBatchPhase Phase { get; }

            internal long ElapsedTicks { get; }

            internal long LotQuantity { get; }

            internal SpareBearingLotCustody Custody { get; }

            internal bool RoutePermitGranted { get; }
        }

        private static void CorruptCurrentRecovery(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "current-recovery");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            byte[] first = { 0x41, 0x42, 0x43 };
            byte[] second = { 0x51, 0x52, 0x53 };
            Persist(store, first);
            Persist(store, second);
            Mutate(Path.Combine(profile, LastBearingProfileContract.CurrentPointerName));
            LastBearingLoadResult recovered = store.TryLoad(_ => true);
            TestHarness.True(recovered.Succeeded && recovered.FromLastGood, "last-good did not recover");
            TestHarness.Equal(
                LastBearingSaveCodes.RecoveredLastGood,
                recovered.Code,
                "recovery code");
            TestHarness.True(first.SequenceEqual(recovered.CanonicalPayload!), "recovered wrong generation");

            Mutate(Path.Combine(profile, LastBearingProfileContract.LastGoodPointerName));
            LastBearingLoadResult refused = store.TryLoad(_ => true);
            TestHarness.True(!refused.Succeeded, "corrupt current and last-good loaded");
            TestHarness.Equal(LastBearingSaveCodes.BothCorrupt, refused.Code, "double-corrupt code");
        }

        private static void CorruptCurrentRefusesPersist(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "fork-refusal");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            Persist(store, new byte[] { 0x61 });
            Persist(store, new byte[] { 0x62 });
            Mutate(Path.Combine(profile, LastBearingProfileContract.CurrentPointerName));
            string[] before = Directory.GetFiles(profile).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            LastBearingPersistResult result = store.TryPersist(new byte[] { 0x63 });
            string[] after = Directory.GetFiles(profile).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            TestHarness.True(!result.Succeeded, "persist forked from corrupt current");
            TestHarness.True(before.SequenceEqual(after), "corrupt-current refusal changed namespace");
        }

        private static void UnknownCurrentVersionFailsClosed(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "unknown-version-refusal");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            Persist(store, new byte[] { 0x91 });
            Persist(store, new byte[] { 0x92 });
            string currentPath = Path.Combine(
                profile,
                LastBearingProfileContract.CurrentPointerName);
            byte[] unknownPointer = File.ReadAllBytes(currentPath);
            unknownPointer[8] = 0x02;
            unknownPointer[9] = 0x00;
            File.WriteAllBytes(currentPath, unknownPointer);

            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(!loaded.Succeeded, "unknown current version downgraded to last-good");
            TestHarness.Equal(
                LastBearingSaveCodes.UnknownVersion,
                loaded.Code,
                "unknown-version load code");
            LastBearingPersistResult persisted = store.TryPersist(new byte[] { 0x93 });
            TestHarness.True(!persisted.Succeeded, "unknown current version accepted a persist");
            TestHarness.Equal(
                LastBearingSaveCodes.UnknownVersion,
                persisted.Code,
                "unknown-version persist code");
            TestHarness.True(
                unknownPointer.SequenceEqual(File.ReadAllBytes(currentPath)),
                "unknown current pointer was rewritten");
        }

        private static void DecoysAreNotDiscoverable(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "decoy-refusal");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            byte[] canonical = { 0x71, 0x72, 0x73 };
            Persist(store, canonical);
            string orphan = Path.Combine(profile, "orphan.stage");
            string sibling = Path.Combine(profile, "valid-looking-sibling.lbg");
            File.WriteAllBytes(orphan, new byte[] { 0xde, 0xad });
            File.WriteAllBytes(sibling, new byte[] { 0xbe, 0xef });
            DateTime orphanWrite = File.GetLastWriteTimeUtc(orphan);
            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(loaded.Succeeded, "decoys prevented current load");
            TestHarness.True(canonical.SequenceEqual(loaded.CanonicalPayload!), "decoy was discovered");
            TestHarness.True(File.Exists(orphan) && File.Exists(sibling), "load rewrote or cleaned siblings");
            TestHarness.Equal(orphanWrite, File.GetLastWriteTimeUtc(orphan), "load rewrote a decoy");
        }

        private static void MissingLoadIsReadOnly(string repoRoot)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/save-atomic/missing");
            Recreate(parent);
            string profile = Path.Combine(parent, LastBearingProfileContract.ProfileName);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(!loaded.Succeeded, "missing profile loaded");
            TestHarness.Equal(LastBearingSaveCodes.NoProfile, loaded.Code, "missing profile code");
            TestHarness.True(!Directory.Exists(profile), "read-only load created a profile");
        }

        private static void FaultedPublicationConverges(string repoRoot)
        {
            foreach (SaveFaultPoint point in new[]
            {
                SaveFaultPoint.AfterGenerationStageDurableClose,
                SaveFaultPoint.AfterGenerationPublication,
                SaveFaultPoint.AfterLastGoodPublication,
                SaveFaultPoint.AfterCurrentPublication,
                SaveFaultPoint.PartialGenerationStage,
                SaveFaultPoint.PartialLastGoodPointerStage,
                SaveFaultPoint.PartialCurrentPointerStage,
            })
            {
                string profile = FreshProfile(repoRoot, "fault-" + point);
                LastBearingProfileStore normal =
                    LastBearingProfileStore.OpenFixedProfileDirectory(profile);
                byte[] first = { 0x81, 0x01 };
                byte[] second = { 0x82, 0x02 };
                byte[] attempted = { 0x83, 0x03 };
                Persist(normal, first);
                Persist(normal, second);

                var operations = new FaultInjectingFileOperations(
                    LastBearingFileOperations.Instance,
                    point);
                LastBearingProfileStore faulting =
                    LastBearingProfileStore.OpenFixedProfileDirectoryForTests(
                        profile,
                        operations);
                LastBearingPersistResult interrupted = faulting.TryPersist(attempted);
                TestHarness.True(operations.Fired, "fault point did not fire: " + point);
                TestHarness.True(!interrupted.Succeeded, "faulted persist reported success");

                LastBearingLoadResult visible = normal.TryLoad(_ => true);
                TestHarness.True(visible.Succeeded, "fault left no valid visible generation");
                if (IsPartialStageFault(point))
                {
                    TestHarness.True(
                        second.SequenceEqual(visible.CanonicalPayload!),
                        "partial stage became visible: " + point);
                    TestHarness.True(
                        Directory.GetFiles(profile)
                            .Select(Path.GetFileName)
                            .Any(name => name != null &&
                                name.StartsWith(".stage-", StringComparison.Ordinal)),
                        "partial stage residue was not created: " + point);
                }

                TestHarness.True(
                    second.SequenceEqual(visible.CanonicalPayload!) ||
                    attempted.SequenceEqual(visible.CanonicalPayload!),
                    "fault exposed a partial or unrelated payload");

                LastBearingPersistResult retried = normal.TryPersist(attempted);
                TestHarness.True(retried.Succeeded, "fault retry did not converge");
                LastBearingLoadResult converged = normal.TryLoad(_ => true);
                TestHarness.True(
                    converged.Succeeded && attempted.SequenceEqual(converged.CanonicalPayload!),
                    "fault retry did not publish attempted payload");
                TestHarness.True(
                    !Directory.GetFiles(profile)
                        .Select(Path.GetFileName)
                        .Any(name => name != null &&
                            name.StartsWith(".stage-", StringComparison.Ordinal)),
                    "retry left scratch stage residue: " + point);
            }
        }

        private static bool IsPartialStageFault(SaveFaultPoint point)
        {
            return point == SaveFaultPoint.PartialGenerationStage ||
                point == SaveFaultPoint.PartialLastGoodPointerStage ||
                point == SaveFaultPoint.PartialCurrentPointerStage;
        }

        private static void ScratchRecoveryRejectsPublishedGeneration(
            string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "published-generation-retention");
            byte[] payload = { 0xa1, 0xb2, 0xc3 };
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted = Persist(store, payload);
            LastBearingGenerationRecord generation =
                LastBearingGenerationCodec.EncodeGeneration(
                    persisted.Generation,
                    payload);
            LastBearingDirectoryOpenResult opened =
                LastBearingFileOperations.Instance.TryOpenProfileDirectory(
                    profile,
                    createFixedTerminalIfMissing: false);
            TestHarness.True(
                opened.Status == LastBearingOperationStatus.Success &&
                    opened.Directory != null,
                "published-generation profile did not open");
            using (ILastBearingProfileDirectory directory = opened.Directory!)
            {
                LastBearingFileResult removal =
                    directory.TryRemoveScratchStage(generation.FileName);
                TestHarness.Equal(
                    LastBearingOperationStatus.ConfinementFailure,
                    removal.Status,
                    "published generation entered scratch deletion path");
            }

            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(
                loaded.Succeeded && payload.SequenceEqual(loaded.CanonicalPayload!),
                "published generation changed after rejected scratch removal");
        }

        private static LastBearingPersistResult Persist(
            LastBearingProfileStore store,
            byte[] payload)
        {
            LastBearingPersistResult result = store.TryPersist(payload);
            TestHarness.True(result.Succeeded, "persist failed: " + result.Code);
            return result;
        }

        private static string FreshProfile(string repoRoot, string caseName)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/save-atomic",
                caseName);
            Recreate(parent);
            return Path.Combine(parent, LastBearingProfileContract.ProfileName);
        }

        private static void Recreate(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            Directory.CreateDirectory(path);
        }

        private static void Mutate(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bytes[bytes.Length / 2] ^= 0x01;
            File.WriteAllBytes(path, bytes);
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
