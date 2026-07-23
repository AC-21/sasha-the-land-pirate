#nullable enable

using System;
using System.IO;
using System.Linq;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class HotShiftTests
    {
        private const string ReleasedDeliveredV6Base64 =
            "QUxQTEJDMDEGAAYAAAAhAGxhc3QtYmVhcmluZy1wcm90b3R5cGUtYmFsYW5jZS12MzEKAAABAAAAAAAAAAEAAAAAAAAAAQAA" +
            "AAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAABQBzYXNoYQITAHJlc2lkZW50Omh1bWFuOjAw" +
            "MDEBAAAAEwByZXNpZGVudDpyb2JvdDowMDAxAgAAAAAAAAAAAQAAAAAAAAAAAQAAAAAAAAACAAAAAAAAAAEBEwByZXNpZGVu" +
            "dDpodW1hbjowMDAxAgAAAAEAAAC21AEAAAAAABgAAAAAAAAAEgAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADoAwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAABAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAQAAAAAAAAAA" +
            "AAAAAAABAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAA==";

        internal static void RunCore(TestHarness harness)
        {
            harness.Run(
                "Hot Shift commits one fuel and credits two parts once",
                RunCommitsFuelAndCreditsPartsOnce);
            harness.Run(
                "Workshop Push stalls Hot Shift while Civic Buffer keeps it working",
                PreparationReservationControlsProgress);
            harness.Run(
                "Hot Shift rejects missing service, fuel, and stale expectations",
                InvalidStartsFailClosed);
            harness.Run(
                "Hot Shift pauses exactly and works for every colony composition",
                PauseAndCompositionSemantics);
            harness.Run(
                "Hot Shift checkpoint is exact and independent of One Good Batch",
                CheckpointAndSpareBearingAreIndependent);
            harness.Run(
                "forged Hot Shift progress fails closed",
                ForgedStatesFailClosed);
        }

        internal static void RunSave(
            TestHarness harness,
            string repoRoot)
        {
            harness.Run(
                "Hot Shift progress round trips and released v6 migrates to v7",
                () => SaveAndLegacyV6MigrationRoundTrip(repoRoot));
        }

        private static void RunCommitsFuelAndCreditsPartsOnce()
        {
            CoreTestDriver driver = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                2601);
            long fuelBefore = driver.State.FuelUnits;
            long partsBefore = driver.State.PartsUnits;
            TestHarness.True(
                driver.View.IsHotShiftRunAvailable,
                "delivered cell did not expose Hot Shift");
            TestHarness.Equal(
                LastBearingBalanceV1.HotShiftFuelCostUnits,
                driver.View.HotShiftFuelCostUnits,
                "read-model fuel contract");
            TestHarness.Equal(
                LastBearingBalanceV1.HotShiftOutputPartsUnits,
                driver.View.HotShiftOutputPartsUnits,
                "read-model output contract");

            LastBearingTickResult started = driver.Apply(sequence =>
                new RunHotShiftCommand(
                    sequence,
                    driver.State.HotShiftCompletedCount));
            TestHarness.Equal(
                fuelBefore - LastBearingBalanceV1.HotShiftFuelCostUnits,
                driver.State.FuelUnits,
                "start fuel commitment");
            TestHarness.Equal(
                partsBefore,
                driver.State.PartsUnits,
                "start parts");
            TestHarness.Equal(
                HotShiftPhase.InProgress,
                driver.State.HotShiftPhase,
                "start phase");
            TestHarness.Equal(
                0L,
                driver.State.HotShiftElapsedTicks,
                "start step must not advance production");
            TestHarness.True(
                started.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.HotShiftStarted
                    && item.SubjectId == LastBearingState.HotShiftId
                    && item.BeforeValue == fuelBefore
                    && item.AfterValue == driver.State.FuelUnits),
                "start event");

            LastBearingTickResult duplicate = driver.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            TestHarness.Equal(
                fuelBefore - LastBearingBalanceV1.HotShiftFuelCostUnits,
                driver.State.FuelUnits,
                "in-progress replay fuel");
            TestHarness.Equal(
                1L,
                driver.State.HotShiftElapsedTicks,
                "replay step progress");
            TestHarness.True(
                duplicate.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "in-progress replay event");

            driver.Advance(118);
            TestHarness.Equal(
                LastBearingBalanceV1.HotShiftRequiredSettlementTicks - 1,
                driver.State.HotShiftElapsedTicks,
                "pre-completion progress");
            TestHarness.Equal(
                partsBefore,
                driver.State.PartsUnits,
                "premature parts credit");

            var kernel = new LastBearingKernel();
            LastBearingTickResult completed = kernel.Step(
                driver.State,
                Array.Empty<LastBearingCommand>());
            TestHarness.Equal(
                HotShiftPhase.Idle,
                completed.State.HotShiftPhase,
                "completion phase");
            TestHarness.Equal(
                1L,
                completed.State.HotShiftCompletedCount,
                "completion count");
            TestHarness.Equal(
                partsBefore + LastBearingBalanceV1.HotShiftOutputPartsUnits,
                completed.State.PartsUnits,
                "completion parts");
            TestHarness.True(
                completed.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.HotShiftCompleted
                    && item.BeforeValue == partsBefore
                    && item.AfterValue == completed.State.PartsUnits),
                "completion event");

            var afterCompletion = new CoreTestDriver(completed.State);
            long completedFuel = afterCompletion.State.FuelUnits;
            long completedParts = afterCompletion.State.PartsUnits;
            LastBearingTickResult completedReplay =
                afterCompletion.Apply(sequence =>
                    new RunHotShiftCommand(sequence, 0));
            TestHarness.Equal(
                completedFuel,
                afterCompletion.State.FuelUnits,
                "completed replay fuel");
            TestHarness.Equal(
                completedParts,
                afterCompletion.State.PartsUnits,
                "completed replay parts");
            TestHarness.True(
                completedReplay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "completed replay event");

            afterCompletion.Apply(sequence =>
                new RunHotShiftCommand(sequence, 1));
            TestHarness.Equal(
                HotShiftPhase.InProgress,
                afterCompletion.State.HotShiftPhase,
                "second shift phase");
            TestHarness.Equal(
                completedFuel - LastBearingBalanceV1.HotShiftFuelCostUnits,
                afterCompletion.State.FuelUnits,
                "second shift fuel");
            TestHarness.Equal(
                1L,
                afterCompletion.State.HotShiftCompletedCount,
                "second shift preserves completed count");
        }

        private static void PreparationReservationControlsProgress()
        {
            CoreTestDriver workshop = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                2602);
            workshop.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            TestHarness.True(
                workshop.View.IsHotShiftStalledByWorkshopPush,
                "Workshop Push did not expose stall");
            TestHarness.True(
                !workshop.View.IsHotShiftActivelyWorking,
                "stalled shift exposed as working");
            TestHarness.Equal(
                0L,
                workshop.State.HotShiftElapsedTicks,
                "Workshop Push selection advanced shift");
            TestHarness.Equal(
                LastBearingBalanceV1.FailingWaterRateMilliPerSettlementTick
                    + LastBearingBalanceV1
                        .WorkshopWaterModifierMilliPerSettlementTick,
                workshop.View.WaterTrendMilliPerSettlementTick,
                "stalled water trend");

            workshop.Advance(
                checked((int)workshop.View.PreparationRemainingTicks));
            TestHarness.Equal(
                PreparationPhase.Ready,
                workshop.View.PreparationPhase,
                "Workshop Push preparation did not finish");
            TestHarness.Equal(
                0L,
                workshop.State.HotShiftElapsedTicks,
                "shift advanced before reservation release");
            workshop.Advance(1);
            TestHarness.Equal(
                1L,
                workshop.State.HotShiftElapsedTicks,
                "shift did not resume after reservation release");

            CoreTestDriver civic = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                2603);
            civic.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            civic.Advance(1);
            TestHarness.True(
                !civic.View.IsHotShiftStalledByWorkshopPush,
                "Civic Buffer stalled shift");
            TestHarness.True(
                civic.View.IsHotShiftActivelyWorking,
                "Civic Buffer shift not working");
            TestHarness.Equal(
                1L,
                civic.State.HotShiftElapsedTicks,
                "Civic Buffer selection did not advance shift");
            TestHarness.Equal(
                0L,
                civic.View.WaterTrendMilliPerSettlementTick,
                "Civic Buffer active Hot Shift water trend");
        }

        private static void InvalidStartsFailClosed()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2604);
            AssertRejectedWithoutMutation(
                initial,
                new RunHotShiftCommand(initial.NextCommandSequence, 0),
                "LAST_BEARING_HOT_SHIFT_SERVICE_CELL_REQUIRED",
                "missing service cell");

            CoreTestDriver delivered = DeliveredCell(2605);
            AssertRejectedWithoutMutation(
                delivered.State,
                new RunHotShiftCommand(
                    delivered.State.NextCommandSequence,
                    0),
                "LAST_BEARING_HOT_SHIFT_GARAGE_PLAN_REQUIRED",
                "missing garage plan");

            CoreTestDriver planned = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                2610);
            LastBearingState noFuel =
                new LastBearingStateBuilder(planned.State)
                {
                    FuelUnits = 0,
                }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(noFuel)
                    .IsHotShiftRunAvailable,
                "fuel-free shift exposed");
            AssertRejectedWithoutMutation(
                noFuel,
                new RunHotShiftCommand(noFuel.NextCommandSequence, 0),
                "LAST_BEARING_HOT_SHIFT_FUEL_INSUFFICIENT",
                "missing fuel");

            long routeFuelReserve =
                LastBearingBalanceV1.RouteFuelCost(
                    planned.State.PlannedModule);
            LastBearingState routeOnlyFuel =
                new LastBearingStateBuilder(planned.State)
                {
                    FuelUnits = routeFuelReserve,
                }.Build();
            AssertRejectedWithoutMutation(
                routeOnlyFuel,
                new RunHotShiftCommand(
                    routeOnlyFuel.NextCommandSequence,
                    0),
                "LAST_BEARING_HOT_SHIFT_ROUTE_FUEL_RESERVE_REQUIRED",
                "route fuel reserve");
            AssertRejectedWithoutMutation(
                planned.State,
                new RunHotShiftCommand(
                    planned.State.NextCommandSequence,
                    1),
                "LAST_BEARING_HOT_SHIFT_EXPECTED_COMPLETION_MISMATCH",
                "future completion expectation");

            CoreTestDriver away = OutboundCell(2617);
            TestHarness.True(
                !away.View.IsHotShiftRunAvailable,
                "away shift exposed");
            AssertRejectedWithoutMutation(
                away.State,
                new RunHotShiftCommand(
                    away.State.NextCommandSequence,
                    0),
                "LAST_BEARING_HOT_SHIFT_HOME_REQUIRED",
                "away start");
            TestHarness.Throws<ArgumentOutOfRangeException>(
                () => new RunHotShiftCommand(0, -1),
                "negative completion expectation accepted");
        }

        private static void PauseAndCompositionSemantics()
        {
            foreach (ColonyComposition composition in new[]
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            })
            {
                CoreTestDriver driver = PlannedCell(
                    composition,
                    PreparationChoice.CivicBuffer,
                    VehicleModule.SealedRangeTank,
                    checked(2611 + (int)composition));
                driver.Apply(sequence =>
                    new RunHotShiftCommand(sequence, 0));
                TestHarness.Equal(
                    HotShiftPhase.InProgress,
                    driver.State.HotShiftPhase,
                    composition + " start phase");
                TestHarness.True(
                    driver.View.IsHotShiftActivelyWorking,
                    composition + " start availability");
            }

            CoreTestDriver paused = PlannedCell(
                ColonyComposition.HumanOnly,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                2615);
            paused.Advance(
                checked((int)paused.View.PreparationRemainingTicks));
            paused.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            long waterBeforePause = paused.State.WaterMilli;
            paused.Apply(sequence => new SetPauseCommand(sequence, true));
            paused.Advance(5);
            TestHarness.Equal(
                0L,
                paused.State.HotShiftElapsedTicks,
                "paused progress");
            TestHarness.Equal(
                waterBeforePause,
                paused.State.WaterMilli,
                "paused water");
            paused.Apply(sequence =>
                new SetPauseCommand(sequence, false));
            TestHarness.Equal(
                1L,
                paused.State.HotShiftElapsedTicks,
                "unpaused progress");
            TestHarness.Equal(
                waterBeforePause
                    + LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick
                    + LastBearingBalanceV1
                        .HotShiftWaterModifierMilliPerSettlementTick,
                paused.State.WaterMilli,
                "unpaused water pressure");

            CoreTestDriver activeAway = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                2618);
            activeAway.Advance(
                checked((int)activeAway.View.PreparationRemainingTicks));
            activeAway.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            string transactionId = "tx:hot-shift-active-away:2618";
            string fingerprint = "fp:hot-shift-active-away:2618";
            activeAway.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            activeAway.Apply(sequence =>
                new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            TestHarness.Equal(
                ExpeditionPhase.Outbound,
                activeAway.State.ExpeditionPhase,
                "active shift departure");
            long progressAtDeparture =
                activeAway.State.HotShiftElapsedTicks;
            activeAway.Advance(2);
            TestHarness.Equal(
                progressAtDeparture + 1,
                activeAway.State.HotShiftElapsedTicks,
                "away home-clock progress");
        }

        private static void CheckpointAndSpareBearingAreIndependent()
        {
            CoreTestDriver checkpoint = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                2616);
            checkpoint.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            checkpoint.Advance(59);
            var kernel = new LastBearingKernel();
            LastBearingTickResult reached = kernel.Step(
                checkpoint.State,
                Array.Empty<LastBearingCommand>());
            TestHarness.Equal(
                LastBearingBalanceV1.HotShiftCheckpointSettlementTick,
                reached.State.HotShiftElapsedTicks,
                "checkpoint elapsed");
            TestHarness.Equal(
                1,
                reached.DomainEvents.Count(item =>
                    item.Kind
                        == LastBearingEventKind.HotShiftCheckpointReached),
                "checkpoint event count");

            LastBearingDecodeResult restored =
                LastBearingCanonicalCodec.TryDecode(
                    LastBearingCanonicalCodec.Encode(reached.State));
            TestHarness.True(
                restored.Succeeded && restored.State != null,
                "checkpoint restore");
            LastBearingState restoredCheckpoint = restored.State!;
            LastBearingTickResult afterRestore = kernel.Step(
                restoredCheckpoint,
                new LastBearingCommand[]
                {
                    new RunHotShiftCommand(
                        restoredCheckpoint.NextCommandSequence,
                        0),
                });
            TestHarness.Equal(
                0,
                afterRestore.DomainEvents.Count(item =>
                    item.Kind
                        == LastBearingEventKind.HotShiftCheckpointReached),
                "checkpoint replayed after restore");
            TestHarness.Equal(
                1,
                afterRestore.DomainEvents.Count(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "checkpoint duplicate command replay");

            LastBearingState spareStarted =
                OneGoodBatchTests.CreateStartedStateForSaveTests();
            LastBearingTickResult spareControl = kernel.Step(
                spareStarted,
                Array.Empty<LastBearingCommand>());
            LastBearingTickResult hotStarted = kernel.Step(
                spareStarted,
                new LastBearingCommand[]
                {
                    new RunHotShiftCommand(
                        spareStarted.NextCommandSequence,
                        0),
                });
            AssertSpareBearingEqual(
                spareControl.State,
                hotStarted.State,
                "Hot Shift start");
            LastBearingTickResult spareControlNext = kernel.Step(
                spareControl.State,
                Array.Empty<LastBearingCommand>());
            LastBearingTickResult hotNext = kernel.Step(
                hotStarted.State,
                Array.Empty<LastBearingCommand>());
            AssertSpareBearingEqual(
                spareControlNext.State,
                hotNext.State,
                "Hot Shift work");
            TestHarness.Equal(
                1L,
                hotNext.State.HotShiftElapsedTicks,
                "concurrent Hot Shift progress");
        }

        private static void ForgedStatesFailClosed()
        {
            CoreTestDriver delivered = DeliveredCell(2606);
            AssertInvariantRejected(
                new LastBearingStateBuilder(delivered.State)
                {
                    HotShiftElapsedTicks = 1,
                }.BuildUnchecked(),
                "LAST_BEARING_HOT_SHIFT_IDLE_STATE_INVALID",
                "idle progress");

            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    2607);
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    HotShiftPhase = HotShiftPhase.InProgress,
                    HotShiftRequiredTicks =
                        LastBearingBalanceV1
                            .HotShiftRequiredSettlementTicks,
                    HotShiftFuelCommittedUnits =
                        LastBearingBalanceV1.HotShiftFuelCostUnits,
                }.BuildUnchecked(),
                "LAST_BEARING_HOT_SHIFT_PROGRESS_STATE_INVALID",
                "progress without delivered cell");
        }

        private static void SaveAndLegacyV6MigrationRoundTrip(
            string repoRoot)
        {
            CoreTestDriver active = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                2608);
            active.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            active.Advance(60);

            byte[] canonical =
                LastBearingCanonicalCodec.Encode(active.State);
            TestHarness.Equal((byte)7, canonical[8], "v7 codec marker");
            string profile = FreshProfile(repoRoot, "active");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted =
                store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                "Hot Shift persist: " + persisted.Code);
            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                "Hot Shift load: " + loaded.Code);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(
                    loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "Hot Shift decode");
            TestHarness.Equal(
                HotShiftPhase.InProgress,
                decoded.State!.HotShiftPhase,
                "restored phase");
            TestHarness.Equal(
                60L,
                decoded.State.HotShiftElapsedTicks,
                "restored progress");
            TestHarness.Equal(
                LastBearingBalanceV1.HotShiftFuelCostUnits,
                decoded.State.HotShiftFuelCommittedUnits,
                "restored fuel commitment");
            TestHarness.True(
                canonical.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State)),
                "active Hot Shift canonical bytes changed");

            CoreTestDriver delivered = DeliveredCell(2609);
            byte[] generatedV6 =
                LastBearingCanonicalCodec
                    .EncodeLegacyV6ForMigrationTests(delivered.State);
            string generatedV6Base64 =
                Convert.ToBase64String(generatedV6);
            TestHarness.Equal(
                ReleasedDeliveredV6Base64,
                generatedV6Base64,
                "released delivered v6 bytes drifted");
            TestHarness.Equal((byte)6, generatedV6[8], "v6 marker");
            LastBearingDecodeResult first =
                LastBearingCanonicalCodec.TryDecode(generatedV6);
            LastBearingDecodeResult second =
                LastBearingCanonicalCodec.TryDecode(generatedV6);
            TestHarness.True(
                first.Succeeded
                    && first.State != null
                    && second.Succeeded
                    && second.State != null,
                "v6 migration decode");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                first.State!.SchemaVersion,
                "v6 migrated schema");
            TestHarness.Equal(
                LastBearingBalanceV1.Revision,
                first.State.BalanceRevision,
                "v6 migrated balance");
            TestHarness.Equal(
                HotShiftPhase.Idle,
                first.State.HotShiftPhase,
                "v6 migrated phase");
            TestHarness.Equal(
                0L,
                first.State.HotShiftCompletedCount,
                "v6 migrated completion count");
            TestHarness.True(
                generatedV6.SequenceEqual(
                    LastBearingCanonicalCodec
                        .EncodeLegacyV6ForMigrationTests(first.State)),
                "v6 canonical bytes changed");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(first.State),
                LastBearingCanonicalCodec.ComputeSha256(second.State!),
                "v6 migration hash");
        }

        private static CoreTestDriver DeliveredCell(int worldSeed)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.Mixed,
                worldSeed);
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            return driver;
        }

        private static CoreTestDriver PlannedCell(
            ColonyComposition composition,
            PreparationChoice choice,
            VehicleModule module,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            string residentId = composition == ColonyComposition.RobotOnly
                ? ResidentRoster.RobotResidentId
                : ResidentRoster.HumanResidentId;
            driver.Apply(sequence =>
                new AssignResidentCommand(sequence, residentId));
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            driver.Apply(sequence =>
                new SelectPreparationCommand(
                    sequence,
                    choice,
                    module));
            driver.Apply(sequence =>
                new InstallVehicleModuleCommand(sequence, module));
            return driver;
        }

        private static CoreTestDriver OutboundCell(int worldSeed)
        {
            CoreTestDriver driver = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                worldSeed);
            driver.Advance(
                checked((int)driver.View.PreparationRemainingTicks));
            string transactionId = "tx:hot-shift-away:" + worldSeed;
            string fingerprint = "fp:hot-shift-away:" + worldSeed;
            driver.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence =>
                new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            TestHarness.Equal(
                ExpeditionPhase.Outbound,
                driver.State.ExpeditionPhase,
                "away test departure");
            return driver;
        }

        private static void AssertSpareBearingEqual(
            LastBearingState expected,
            LastBearingState actual,
            string label)
        {
            TestHarness.Equal(
                expected.SpareBearingRecipe,
                actual.SpareBearingRecipe,
                label + " recipe");
            TestHarness.Equal(
                expected.SpareBearingBatchPhase,
                actual.SpareBearingBatchPhase,
                label + " phase");
            TestHarness.Equal(
                expected.SpareBearingElapsedTicks,
                actual.SpareBearingElapsedTicks,
                label + " elapsed");
            TestHarness.Equal(
                expected.SpareBearingRequiredTicks,
                actual.SpareBearingRequiredTicks,
                label + " required");
            TestHarness.Equal(
                expected.SpareBearingLotQuantity,
                actual.SpareBearingLotQuantity,
                label + " lot quantity");
            TestHarness.Equal(
                expected.SpareBearingLotCustody,
                actual.SpareBearingLotCustody,
                label + " lot custody");
        }

        private static void AssertRejectedWithoutMutation(
            LastBearingState state,
            LastBearingCommand command,
            string expectedCode,
            string label)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new[] { command }),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                label + " mutated input");
        }

        private static void AssertInvariantRejected(
            LastBearingState state,
            string expectedCode,
            string label)
        {
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingInvariants.Validate(state),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
        }

        private static string FreshProfile(
            string repoRoot,
            string caseName)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/hot-shift",
                caseName);
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }

            Directory.CreateDirectory(parent);
            return Path.Combine(
                parent,
                LastBearingProfileContract.ProfileName);
        }
    }
}
