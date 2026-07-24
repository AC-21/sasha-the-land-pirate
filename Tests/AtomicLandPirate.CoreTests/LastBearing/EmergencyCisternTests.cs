#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class EmergencyCisternTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "Emergency Cistern spends one fuel and adds ten water once",
                PumpCommitsExactResourcesOnce);
            harness.Run(
                "Emergency Cistern prerequisites reject atomically",
                PreconditionsFailClosed);
            harness.Run(
                "Emergency Cistern is composition-neutral",
                CompositionSemantics);
            harness.Run(
                "Emergency Cistern turns the base Dust Front breach into a hold",
                PumpChangesBaseDustFrontVerdict);
            harness.Run(
                "schema 8 cistern migration defaults deterministically",
                SaveRoundTripAndSchemaEightMigration);
        }

        private static void PumpCommitsExactResourcesOnce()
        {
            CoreTestDriver driver = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                2801);
            long fuelBefore = driver.State.FuelUnits;
            long waterBefore = driver.State.WaterMilli;
            TestHarness.True(
                driver.View.IsEmergencyCisternPumpAvailable,
                "planned service cell did not expose cistern action");
            TestHarness.Equal(
                LastBearingBalanceV1.EmergencyCisternFuelCostUnits,
                driver.View.EmergencyCisternFuelCostUnits,
                "read-model fuel contract");
            TestHarness.Equal(
                LastBearingBalanceV1.EmergencyCisternWaterMilli,
                driver.View.EmergencyCisternWaterMilli,
                "read-model water contract");

            LastBearingTickResult pumped = driver.Apply(sequence =>
                new PumpEmergencyCisternCommand(sequence));
            TestHarness.Equal(
                fuelBefore
                    - LastBearingBalanceV1.EmergencyCisternFuelCostUnits,
                driver.State.FuelUnits,
                "cistern fuel debit");
            TestHarness.True(
                driver.State.EmergencyCisternCharged,
                "cistern completion flag");
            TestHarness.True(
                !driver.View.IsEmergencyCisternPumpAvailable,
                "charged cistern remained available");
            TestHarness.True(
                pumped.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.EmergencyCisternPumped
                    && item.SubjectId
                        == LastBearingState.EmergencyCisternId
                    && item.BeforeValue == waterBefore
                    && item.AfterValue
                        == waterBefore
                            + LastBearingBalanceV1
                                .EmergencyCisternWaterMilli),
                "exact cistern water event");
            TestHarness.True(
                pumped.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.CityResourcesCommitted
                    && item.SubjectId
                        == LastBearingState.EmergencyCisternId
                    && item.BeforeValue == fuelBefore
                    && item.AfterValue == driver.State.FuelUnits),
                "cistern fuel event");

            long fuelAfter = driver.State.FuelUnits;
            LastBearingTickResult replay = driver.Apply(sequence =>
                new PumpEmergencyCisternCommand(sequence));
            TestHarness.Equal(
                fuelAfter,
                driver.State.FuelUnits,
                "replay fuel");
            TestHarness.Equal(
                0,
                replay.DomainEvents.Count(item =>
                    item.Kind
                        == LastBearingEventKind.EmergencyCisternPumped),
                "replay pump event count");
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "replay acceptance event");
        }

        private static void PreconditionsFailClosed()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2802);
            AssertRejectedWithoutMutation(
                initial,
                "LAST_BEARING_EMERGENCY_CISTERN_SERVICE_CELL_REQUIRED",
                "missing service cell");

            var delivered = new CoreTestDriver(
                ColonyComposition.Mixed,
                2803);
            delivered.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            AssertRejectedWithoutMutation(
                delivered.State,
                "LAST_BEARING_EMERGENCY_CISTERN_RIG_PLAN_REQUIRED",
                "missing rig plan");

            CoreTestDriver workshop = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                2804);
            AssertRejectedWithoutMutation(
                workshop.State,
                "LAST_BEARING_EMERGENCY_CISTERN_OPERATOR_UNAVAILABLE",
                "borrowed operator");

            CoreTestDriver planned = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                2805);
            LastBearingState fullStorage =
                new LastBearingStateBuilder(planned.State)
                {
                    WaterMilli =
                        LastBearingBalanceV1.WaterCapacityMilli
                        - LastBearingBalanceV1.EmergencyCisternWaterMilli
                        + 1,
                }.Build();
            AssertRejectedWithoutMutation(
                fullStorage,
                "LAST_BEARING_EMERGENCY_CISTERN_CAPACITY_REQUIRED",
                "partial fill");

            long routeFuel =
                LastBearingBalanceV1.RouteFuelCost(
                    planned.State.PlannedModule);
            LastBearingState routeOnlyFuel =
                new LastBearingStateBuilder(planned.State)
                {
                    FuelUnits = routeFuel,
                }.Build();
            LastBearingState noFuel =
                new LastBearingStateBuilder(planned.State)
                {
                    FuelUnits = 0,
                }.Build();
            AssertRejectedWithoutMutation(
                noFuel,
                "LAST_BEARING_EMERGENCY_CISTERN_FUEL_INSUFFICIENT",
                "no fuel");
            AssertRejectedWithoutMutation(
                routeOnlyFuel,
                "LAST_BEARING_EMERGENCY_CISTERN_ROUTE_FUEL_RESERVE_REQUIRED",
                "route reserve");

            LastBearingState resolved =
                new LastBearingStateBuilder(planned.State)
                {
                    DustFrontProgressTicks =
                        LastBearingBalanceV1
                            .DustFrontThresholdCrisisTicks,
                    DustFrontOutcome = DustFrontOutcome.Held,
                }.Build();
            AssertRejectedWithoutMutation(
                resolved,
                "LAST_BEARING_EMERGENCY_CISTERN_DUST_FRONT_RESOLVED",
                "resolved Dust Front");

            planned.Apply(sequence =>
                new RunHotShiftCommand(
                    sequence,
                    planned.State.HotShiftCompletedCount));
            AssertRejectedWithoutMutation(
                planned.State,
                "LAST_BEARING_EMERGENCY_CISTERN_HOT_SHIFT_ACTIVE",
                "active Hot Shift");

            CoreTestDriver away = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                2806);
            away.Advance(
                checked((int)away.View.PreparationRemainingTicks));
            const string transactionId = "tx:cistern-away:2806";
            const string fingerprint = "fp:cistern-away:2806";
            away.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            away.Apply(sequence =>
                new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            AssertRejectedWithoutMutation(
                away.State,
                "LAST_BEARING_EMERGENCY_CISTERN_HOME_REQUIRED",
                "Sasha away");
        }

        private static void CompositionSemantics()
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
                    VehicleModule.WinchAssembly,
                    checked(2810 + (int)composition));
                long fuelBefore = driver.State.FuelUnits;
                driver.Apply(sequence =>
                    new PumpEmergencyCisternCommand(sequence));
                TestHarness.True(
                    driver.State.EmergencyCisternCharged,
                    composition + " cistern flag");
                TestHarness.Equal(
                    fuelBefore
                        - LastBearingBalanceV1
                            .EmergencyCisternFuelCostUnits,
                    driver.State.FuelUnits,
                    composition + " fuel debit");
            }
        }

        private static void PumpChangesBaseDustFrontVerdict()
        {
            CoreTestDriver ready = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                2820);
            ready.Advance(
                checked((int)ready.View.PreparationRemainingTicks));
            LastBearingState brink =
                new LastBearingStateBuilder(ready.State)
                {
                    WaterMilli =
                        LastBearingBalanceV1.MinimumRecoverableWaterMilli,
                    DustFrontProgressTicks =
                        LastBearingBalanceV1
                            .DustFrontThresholdCrisisTicks - 1,
                    CrisisAccumulatorMilli = 0,
                }.Build();
            var kernel = new LastBearingKernel();
            LastBearingTickResult unpumped = kernel.Step(
                brink,
                Array.Empty<LastBearingCommand>());
            LastBearingTickResult pumped = kernel.Step(
                brink,
                new LastBearingCommand[]
                {
                    new PumpEmergencyCisternCommand(
                        brink.NextCommandSequence),
                });

            TestHarness.Equal(
                DustFrontOutcome.Breached,
                unpumped.State.DustFrontOutcome,
                "base brink verdict");
            TestHarness.Equal(
                DustFrontOutcome.Held,
                pumped.State.DustFrontOutcome,
                "pumped brink verdict");
            TestHarness.True(
                pumped.State.WaterMilli
                    - unpumped.State.WaterMilli
                    == LastBearingBalanceV1.EmergencyCisternWaterMilli,
                "verdict comparison water delta");
        }

        private static void SaveRoundTripAndSchemaEightMigration()
        {
            CoreTestDriver driver = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                2830);
            LastBearingState uncharged = driver.State;
            driver.Apply(sequence =>
                new PumpEmergencyCisternCommand(sequence));
            LastBearingState charged = driver.State;

            byte[] current = LastBearingCanonicalCodec.Encode(charged);
            LastBearingDecodeResult restored =
                LastBearingCanonicalCodec.TryDecode(current);
            TestHarness.True(
                restored.Succeeded && restored.State != null,
                "schema 9 charged decode");
            TestHarness.True(
                restored.State!.EmergencyCisternCharged,
                "schema 9 charged flag");
            TestHarness.True(
                current.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(restored.State)),
                "schema 9 canonical bytes");

            byte[] schemaEight =
                LastBearingCanonicalCodec
                    .EncodeLegacyV8ForMigrationTests(uncharged);
            LastBearingDecodeResult first =
                LastBearingCanonicalCodec.TryDecode(schemaEight);
            LastBearingDecodeResult second =
                LastBearingCanonicalCodec.TryDecode(schemaEight);
            TestHarness.True(
                first.Succeeded
                    && first.State != null
                    && second.Succeeded
                    && second.State != null,
                "schema 8 migration decode");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                first.State!.SchemaVersion,
                "schema 8 migrated schema");
            TestHarness.True(
                !first.State.EmergencyCisternCharged,
                "schema 8 default");
            TestHarness.True(
                schemaEight.SequenceEqual(
                    LastBearingCanonicalCodec
                        .EncodeLegacyV8ForMigrationTests(first.State)),
                "schema 8 source bytes");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(first.State),
                LastBearingCanonicalCodec.ComputeSha256(second.State!),
                "schema 8 deterministic migration");

            LastBearingState brink =
                new LastBearingStateBuilder(uncharged)
                {
                    WaterMilli = 0,
                    DustFrontProgressTicks =
                        LastBearingBalanceV1
                            .DustFrontThresholdCrisisTicks - 1,
                    CrisisAccumulatorMilli = 0,
                }.Build();
            LastBearingState breachedPaused =
                new LastBearingKernel().Step(
                    brink,
                    Array.Empty<LastBearingCommand>())
                .State;
            TestHarness.Equal(
                DustFrontOutcome.Breached,
                breachedPaused.DustFrontOutcome,
                "schema 8 fixture verdict");
            TestHarness.Equal(
                PauseCause.DustFrontAlert,
                breachedPaused.PauseCause,
                "schema 8 fixture pause");
            TestHarness.True(
                breachedPaused.IsDustFrontAcknowledgementRequired,
                "schema 8 fixture acknowledgement");
            byte[] pausedSchemaEight =
                LastBearingCanonicalCodec
                    .EncodeLegacyV8ForMigrationTests(breachedPaused);
            LastBearingDecodeResult pausedMigration =
                LastBearingCanonicalCodec.TryDecode(pausedSchemaEight);
            TestHarness.True(
                pausedMigration.Succeeded &&
                pausedMigration.State != null,
                "paused schema 8 migration decode");
            TestHarness.Equal(
                DustFrontOutcome.Breached,
                pausedMigration.State!.DustFrontOutcome,
                "paused schema 8 verdict");
            TestHarness.Equal(
                PauseCause.DustFrontAlert,
                pausedMigration.State.PauseCause,
                "paused schema 8 pause");
            TestHarness.True(
                pausedMigration.State
                    .IsDustFrontAcknowledgementRequired,
                "paused schema 8 acknowledgement");
            TestHarness.True(
                pausedSchemaEight.SequenceEqual(
                    LastBearingCanonicalCodec
                        .EncodeLegacyV8ForMigrationTests(
                            pausedMigration.State)),
                "paused schema 8 source bytes");

            LastBearingState sameResourcesCharged =
                new LastBearingStateBuilder(uncharged)
                {
                    EmergencyCisternCharged = true,
                }.Build();
            TestHarness.True(
                LastBearingCanonicalCodec.ComputeMechanicalSha256(
                    uncharged)
                != LastBearingCanonicalCodec.ComputeMechanicalSha256(
                    sameResourcesCharged),
                "cistern flag omitted from mechanical projection");
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

        private static void AssertRejectedWithoutMutation(
            LastBearingState state,
            string expectedCode,
            string label)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new LastBearingCommand[]
                        {
                            new PumpEmergencyCisternCommand(
                                state.NextCommandSequence),
                        }),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                label + " mutated input");
        }
    }
}
