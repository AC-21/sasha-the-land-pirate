#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class DustFrontVerdictTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "Dust Front resolves exactly at its authored threshold",
                ResolvesExactlyAtThreshold);
            harness.Run(
                "Dust Front holds on water or repair and breaches at the water line",
                BothVerdictsUseExistingRecoveryConditions);
            harness.Run(
                "Dust Front acknowledgement is durable and resolution is one shot",
                AcknowledgementAndOneShotSemantics);
            harness.Run(
                "Dust Front breach stalls Hot Shift without water draw until repair",
                BreachStallsHotShiftUntilRepair);
            harness.Run(
                "Dust Front v8 round trips and v7 migration is deterministic",
                SaveRoundTripAndLegacyMigration);
            harness.Run(
                "forged Dust Front states fail closed",
                ForgedStatesFailClosed);
        }

        private static void ResolvesExactlyAtThreshold()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2701);
            LastBearingState nearFront = new LastBearingStateBuilder(initial)
            {
                DustFrontProgressTicks =
                    LastBearingBalanceV1.DustFrontThresholdCrisisTicks - 2,
            }.Build();

            LastBearingTickResult beforeThreshold = Step(nearFront);
            TestHarness.Equal(
                LastBearingBalanceV1.DustFrontThresholdCrisisTicks - 1,
                beforeThreshold.State.DustFrontProgressTicks,
                "pre-threshold progress");
            TestHarness.Equal(
                DustFrontOutcome.Unresolved,
                beforeThreshold.State.DustFrontOutcome,
                "pre-threshold outcome");
            TestHarness.Equal(
                PauseCause.None,
                beforeThreshold.State.PauseCause,
                "pre-threshold pause");
            TestHarness.Equal(
                0,
                Count(
                    beforeThreshold,
                    LastBearingEventKind.DustFrontResolved),
                "pre-threshold resolution events");

            LastBearingTickResult resolved = Step(beforeThreshold.State);
            TestHarness.Equal(
                LastBearingBalanceV1.DustFrontThresholdCrisisTicks,
                resolved.State.DustFrontProgressTicks,
                "threshold progress");
            TestHarness.Equal(
                DustFrontOutcome.Held,
                resolved.State.DustFrontOutcome,
                "threshold outcome");
            TestHarness.Equal(
                PauseCause.DustFrontAlert,
                resolved.State.PauseCause,
                "threshold pause");
            TestHarness.True(
                resolved.State.IsDustFrontAcknowledgementRequired,
                "threshold acknowledgement");
            TestHarness.Equal(
                1,
                Count(resolved, LastBearingEventKind.DustFrontResolved),
                "threshold resolution event");
            TestHarness.Equal(
                1,
                Count(resolved, LastBearingEventKind.PauseChanged),
                "threshold pause event");
            LastBearingDomainEvent verdict = resolved.DomainEvents.Single(item =>
                item.Kind == LastBearingEventKind.DustFrontResolved);
            TestHarness.Equal(
                LastBearingEventCause.AutonomousCrisisTick,
                verdict.Cause,
                "verdict event cause");
            TestHarness.Equal(
                LastBearingState.DustFrontId,
                verdict.SubjectId,
                "verdict event subject");
            TestHarness.Equal(
                (long)DustFrontOutcome.Unresolved,
                verdict.BeforeValue,
                "verdict before");
            TestHarness.Equal(
                (long)DustFrontOutcome.Held,
                verdict.AfterValue,
                "verdict after");
        }

        private static void BothVerdictsUseExistingRecoveryConditions()
        {
            LastBearingState exactWater = ThresholdReady(
                worldSeed: 2702,
                waterBeforeSettlementTick:
                    LastBearingBalanceV1.MinimumRecoverableWaterMilli
                    - LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick);
            LastBearingTickResult breached = Step(exactWater);
            TestHarness.Equal(
                LastBearingBalanceV1.MinimumRecoverableWaterMilli,
                breached.State.WaterMilli,
                "exact threshold water");
            TestHarness.Equal(
                DustFrontOutcome.Breached,
                breached.State.DustFrontOutcome,
                "exact threshold must breach");

            LastBearingState aboveWater = ThresholdReady(
                worldSeed: 2703,
                waterBeforeSettlementTick:
                    LastBearingBalanceV1.MinimumRecoverableWaterMilli
                    - LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick
                    + 1);
            LastBearingTickResult waterHeld = Step(aboveWater);
            TestHarness.Equal(
                LastBearingBalanceV1.MinimumRecoverableWaterMilli + 1,
                waterHeld.State.WaterMilli,
                "above threshold water");
            TestHarness.Equal(
                DustFrontOutcome.Held,
                waterHeld.State.DustFrontOutcome,
                "above threshold must hold");

            LastBearingState repaired =
                CityImprovementTests.CreateInstalledStateForSaveTests();
            repaired = new LastBearingStateBuilder(repaired)
            {
                WaterMilli = 0,
                DustFrontProgressTicks =
                    LastBearingBalanceV1.DustFrontThresholdCrisisTicks - 1,
            }.Build();
            LastBearingTickResult repairHeld = Step(repaired);
            TestHarness.Equal(
                DustFrontOutcome.Held,
                repairHeld.State.DustFrontOutcome,
                "repaired turbine must hold");
        }

        private static void AcknowledgementAndOneShotSemantics()
        {
            LastBearingTickResult resolved = Step(ThresholdReady(
                worldSeed: 2704,
                waterBeforeSettlementTick:
                    LastBearingBalanceV1.StartingWaterMilli));
            var driver = new CoreTestDriver(resolved.State);
            byte[] beforeRejectedUnpause =
                LastBearingCanonicalCodec.Encode(driver.State);
            InvalidOperationException rejected =
                TestHarness.Throws<InvalidOperationException>(
                    () => driver.Apply(sequence =>
                        new SetPauseCommand(sequence, false)),
                    "generic unpause cleared Dust Front alert");
            TestHarness.Equal(
                "LAST_BEARING_DUST_FRONT_ACKNOWLEDGEMENT_REQUIRED",
                rejected.Message,
                "generic unpause code");
            TestHarness.True(
                beforeRejectedUnpause.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(driver.State)),
                "rejected unpause mutated state");

            LastBearingTickResult acknowledged = driver.Apply(sequence =>
                new AcknowledgeDustFrontCommand(sequence));
            TestHarness.Equal(
                PauseCause.None,
                driver.State.PauseCause,
                "acknowledged pause");
            TestHarness.True(
                !driver.State.IsDustFrontAcknowledgementRequired,
                "acknowledgement remained pending");
            TestHarness.Equal(
                1,
                Count(
                    acknowledged,
                    LastBearingEventKind.DustFrontAcknowledged),
                "acknowledgement event");
            TestHarness.Equal(
                0,
                Count(
                    acknowledged,
                    LastBearingEventKind.DustFrontResolved),
                "acknowledgement repeated resolution");

            LastBearingTickResult replay = driver.Apply(sequence =>
                new AcknowledgeDustFrontCommand(sequence));
            TestHarness.Equal(
                1,
                Count(
                    replay,
                    LastBearingEventKind.IdempotentReplayAccepted),
                "acknowledgement replay");
            TestHarness.Equal(
                0,
                Count(replay, LastBearingEventKind.DustFrontResolved),
                "acknowledgement replay resolved again");

            var kernel = new LastBearingKernel();
            int extraResolutions = 0;
            for (var index = 0; index < 20; index++)
            {
                LastBearingTickResult tick = kernel.Step(
                    driver.State,
                    Array.Empty<LastBearingCommand>());
                driver = new CoreTestDriver(tick.State);
                extraResolutions += Count(
                    tick,
                    LastBearingEventKind.DustFrontResolved);
            }

            TestHarness.Equal(0, extraResolutions, "later resolution events");
            TestHarness.Equal(
                LastBearingBalanceV1.DustFrontThresholdCrisisTicks,
                driver.State.DustFrontProgressTicks,
                "resolved progress changed");
            TestHarness.Equal(
                DustFrontOutcome.Held,
                driver.State.DustFrontOutcome,
                "resolved outcome changed");
        }

        private static void BreachStallsHotShiftUntilRepair()
        {
            var driver = new CoreTestDriver(
                ColonyComposition.Mixed,
                2705);
            driver.Apply(sequence =>
                new AssignResidentCommand(
                    sequence,
                    ResidentRoster.HumanResidentId));
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            driver.Apply(sequence =>
                new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.CivicBuffer,
                    VehicleModule.WinchAssembly));
            driver.Apply(sequence =>
                new InstallVehicleModuleCommand(
                    sequence,
                    VehicleModule.WinchAssembly));
            driver = new CoreTestDriver(
                new LastBearingStateBuilder(driver.State)
                {
                    WaterMilli = 59990,
                    DustFrontProgressTicks =
                        LastBearingBalanceV1
                            .DustFrontThresholdCrisisTicks - 1,
                }.Build());

            driver.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            TestHarness.Equal(
                DustFrontOutcome.Breached,
                driver.State.DustFrontOutcome,
                "breach setup outcome");
            TestHarness.Equal(
                HotShiftPhase.InProgress,
                driver.State.HotShiftPhase,
                "breach setup shift");
            TestHarness.True(
                driver.View.IsHotShiftStalledByDustFront,
                "breach stall not exposed");
            TestHarness.True(
                !driver.View.IsHotShiftActivelyWorking,
                "breached shift exposed as working");
            TestHarness.Equal(
                0L,
                driver.State.HotShiftElapsedTicks,
                "start step advanced breached shift");
            TestHarness.Equal(
                LastBearingBalanceV1.FailingWaterRateMilliPerSettlementTick
                    + LastBearingBalanceV1
                        .CivicBufferWaterModifierMilliPerSettlementTick,
                driver.View.WaterTrendMilliPerSettlementTick,
                "breached shift added water draw");

            driver.Apply(sequence =>
                new AcknowledgeDustFrontCommand(sequence));
            long stalledElapsed = driver.State.HotShiftElapsedTicks;
            long stalledWater = driver.State.WaterMilli;
            driver.Advance(1);
            TestHarness.Equal(
                stalledElapsed,
                driver.State.HotShiftElapsedTicks,
                "acknowledged breach advanced shift");
            TestHarness.Equal(
                stalledWater
                    + LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick
                    + LastBearingBalanceV1
                        .CivicBufferWaterModifierMilliPerSettlementTick,
                driver.State.WaterMilli,
                "breached shift charged working water");

            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:dust-front-repair:2705";
            string fingerprint = "fp:dust-front-repair:2705";
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
            while (!driver.View.IsDepotApproachRecoveryAvailable)
            {
                driver.OperateWreckLineIfAvailable();
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence =>
                new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            driver.Apply(sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            while (driver.View.ExpeditionPhase != ExpeditionPhase.Returned)
            {
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence =>
                new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            TestHarness.Equal(
                0L,
                driver.State.HotShiftElapsedTicks,
                "breached shift advanced before repair");

            driver = new CoreTestDriver(
                new LastBearingStateBuilder(driver.State)
                {
                    WaterMilli = 1000,
                }.Build());
            driver.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));
            TestHarness.Equal(
                TurbineCondition.BearingRepaired,
                driver.State.TurbineCondition,
                "repair condition");
            TestHarness.Equal(
                1L,
                driver.State.HotShiftElapsedTicks,
                "shift did not resume on repair");
            TestHarness.True(
                !driver.View.IsHotShiftStalledByDustFront
                    && driver.View.IsHotShiftActivelyWorking,
                "repaired shift status");
            TestHarness.Equal(
                1000L
                    + LastBearingBalanceV1
                        .BearingRepairRateMilliPerSettlementTick
                    + LastBearingBalanceV1
                        .HotShiftWaterModifierMilliPerSettlementTick,
                driver.State.WaterMilli,
                "repaired working water trend");
        }

        private static void SaveRoundTripAndLegacyMigration()
        {
            LastBearingTickResult held = Step(ThresholdReady(
                worldSeed: 2706,
                waterBeforeSettlementTick:
                    LastBearingBalanceV1.StartingWaterMilli));
            byte[] current = LastBearingCanonicalCodec.Encode(held.State);
            TestHarness.Equal((byte)7, current[8], "codec framing marker");
            LastBearingDecodeResult restored =
                LastBearingCanonicalCodec.TryDecode(current);
            TestHarness.True(
                restored.Succeeded && restored.State != null,
                "v8 decode");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                restored.State!.SchemaVersion,
                "v8 schema");
            TestHarness.Equal(
                DustFrontOutcome.Held,
                restored.State.DustFrontOutcome,
                "v8 outcome");
            TestHarness.True(
                restored.State.IsDustFrontAcknowledgementRequired,
                "v8 acknowledgement");
            TestHarness.Equal(
                PauseCause.DustFrontAlert,
                restored.State.PauseCause,
                "v8 pause");
            TestHarness.True(
                current.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(restored.State)),
                "v8 canonical bytes");

            LastBearingState preThreshold =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    2707);
            byte[] legacyPre =
                LastBearingCanonicalCodec
                    .EncodeLegacyV7ForMigrationTests(preThreshold);
            LastBearingDecodeResult migratedPre =
                LastBearingCanonicalCodec.TryDecode(legacyPre);
            TestHarness.True(
                migratedPre.Succeeded && migratedPre.State != null,
                "pre-threshold v7 migration");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                migratedPre.State!.SchemaVersion,
                "pre-threshold migrated schema");
            TestHarness.Equal(
                DustFrontOutcome.Unresolved,
                migratedPre.State.DustFrontOutcome,
                "pre-threshold migrated outcome");
            TestHarness.True(
                !migratedPre.State.IsDustFrontAcknowledgementRequired,
                "pre-threshold migrated acknowledgement");

            var acknowledged = new CoreTestDriver(held.State);
            acknowledged.Apply(sequence =>
                new AcknowledgeDustFrontCommand(sequence));
            long legacyPostThresholdTicks = checked(
                LastBearingBalanceV1.DustFrontThresholdCrisisTicks + 77);
            byte[] legacyPost =
                LastBearingCanonicalCodec
                    .EncodeLegacyV7ForMigrationTests(
                        acknowledged.State,
                        legacyPostThresholdTicks);
            LastBearingDecodeResult first =
                LastBearingCanonicalCodec.TryDecode(legacyPost);
            LastBearingDecodeResult second =
                LastBearingCanonicalCodec.TryDecode(legacyPost);
            TestHarness.True(
                first.Succeeded
                    && first.State != null
                    && second.Succeeded
                    && second.State != null,
                "post-threshold v7 migration");
            TestHarness.Equal(
                DustFrontOutcome.Held,
                first.State!.DustFrontOutcome,
                "post-threshold grandfathered outcome");
            TestHarness.Equal(
                LastBearingBalanceV1.DustFrontThresholdCrisisTicks,
                first.State.DustFrontProgressTicks,
                "post-threshold grandfathered progress");
            TestHarness.True(
                !first.State.IsDustFrontAcknowledgementRequired,
                "post-threshold grandfathered acknowledgement");
            TestHarness.Equal(
                PauseCause.None,
                first.State.PauseCause,
                "post-threshold grandfathered pause");
            TestHarness.True(
                legacyPost.SequenceEqual(
                    LastBearingCanonicalCodec
                        .EncodeLegacyV7ForMigrationTests(
                            first.State,
                            legacyPostThresholdTicks)),
                "legacy v7 canonical bytes");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(first.State),
                LastBearingCanonicalCodec.ComputeSha256(second.State!),
                "legacy v7 migration hash");

            foreach (PauseCause legacyPause in new[]
                     {
                         PauseCause.Explicit,
                         PauseCause.AutoAlert,
                     })
            {
                LastBearingState pausedLegacyState =
                    new LastBearingStateBuilder(acknowledged.State)
                    {
                        PauseCause = legacyPause,
                    }.Build();
                byte[] pausedLegacyBytes =
                    LastBearingCanonicalCodec
                        .EncodeLegacyV7ForMigrationTests(pausedLegacyState);
                LastBearingDecodeResult pausedMigration =
                    LastBearingCanonicalCodec.TryDecode(pausedLegacyBytes);
                TestHarness.True(
                    pausedMigration.Succeeded && pausedMigration.State != null,
                    legacyPause + " v7 migration");
                TestHarness.Equal(
                    DustFrontOutcome.Held,
                    pausedMigration.State!.DustFrontOutcome,
                    legacyPause + " grandfathered outcome");
                TestHarness.True(
                    !pausedMigration.State
                        .IsDustFrontAcknowledgementRequired,
                    legacyPause + " retroactive acknowledgement");
                TestHarness.Equal(
                    legacyPause,
                    pausedMigration.State.PauseCause,
                    legacyPause + " pause preservation");
            }
        }

        private static void ForgedStatesFailClosed()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    2708);
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    DustFrontOutcome = DustFrontOutcome.Held,
                }.BuildUnchecked(),
                "LAST_BEARING_DUST_FRONT_RESOLVED_PROGRESS_INVALID",
                "resolved without threshold");
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    DustFrontProgressTicks =
                        LastBearingBalanceV1
                            .DustFrontThresholdCrisisTicks,
                }.BuildUnchecked(),
                "LAST_BEARING_DUST_FRONT_UNRESOLVED_STATE_INVALID",
                "threshold without outcome");

            LastBearingTickResult held = Step(ThresholdReady(
                worldSeed: 2709,
                waterBeforeSettlementTick:
                    LastBearingBalanceV1.StartingWaterMilli));
            var acknowledged = new CoreTestDriver(held.State);
            acknowledged.Apply(sequence =>
                new AcknowledgeDustFrontCommand(sequence));
            AssertInvariantRejected(
                new LastBearingStateBuilder(acknowledged.State)
                {
                    IsDustFrontAcknowledgementRequired = true,
                }.BuildUnchecked(),
                "LAST_BEARING_DUST_FRONT_ACKNOWLEDGEMENT_STATE_INVALID",
                "pending acknowledgement without alert");
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    DustFrontOutcome = (DustFrontOutcome)int.MaxValue,
                }.BuildUnchecked(),
                "LAST_BEARING_DUST_FRONT_OUTCOME_INVALID",
                "invalid outcome enum");
        }

        private static LastBearingState ThresholdReady(
            int worldSeed,
            long waterBeforeSettlementTick)
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    worldSeed);
            return new LastBearingStateBuilder(initial)
            {
                WaterMilli = waterBeforeSettlementTick,
                DustFrontProgressTicks =
                    LastBearingBalanceV1.DustFrontThresholdCrisisTicks - 1,
            }.Build();
        }

        private static LastBearingTickResult Step(LastBearingState state)
        {
            return new LastBearingKernel().Step(
                state,
                Array.Empty<LastBearingCommand>());
        }

        private static int Count(
            LastBearingTickResult result,
            LastBearingEventKind kind)
        {
            return result.DomainEvents.Count(item => item.Kind == kind);
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
    }
}
