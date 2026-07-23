#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class SimulationTests
    {
        public static void Run(TestHarness harness)
        {
            harness.Run("identical command schedules are byte deterministic", DeterministicSchedule);
            harness.Run("one step advances one fixed tick", OneStepOneTick);
            harness.Run(
                "reusable step matches immutable step for representative idle",
                ReusableStepMatchesImmutableStep);
            harness.Run(
                "reusable step rejection retains its committed output",
                ReusableStepRejectsAtomically);
            harness.Run(
                "reusable step allocates no managed memory after warmup",
                ReusableStepAllocatesNoManagedMemoryAfterWarmup);
            harness.Run(
                "valid invariant checks allocate no managed memory after warmup",
                ValidInvariantChecksAllocateNoManagedMemoryAfterWarmup);
            harness.Run(
                "invariant hot path retains exact failure codes",
                InvariantHotPathRetainsExactFailureCodes);
            harness.Run("command sequence mismatch fails closed", SequenceMismatch);
            harness.Run("drive input is bounded and quantized", InputBounds);
            harness.Run("duplicate drive commands fail before tick mutation", DuplicateDriveCommandsFailAtomically);
            harness.Run("preparation progress is exposed without new authority", PreparationProgressIsExposed);
            harness.Run("preparation completes autonomously", PreparationCompletes);
            HomecomingTests.RunCore(harness);
            WreckLineTests.Run(harness);
            FrameRailSalvageTests.RunCore(harness);
            DepotApproachRecoveryTests.Run(harness);
            DepotRepairCargoLoadingTests.Run(harness);
            CityConstructionTests.Run(harness);
            CityImprovementTests.Run(harness);
            OneGoodBatchTests.Run(harness);
            CompositionTests.Run(harness);
            OwnershipTests.Run(harness);
        }

        private static void DeterministicSchedule()
        {
            CoreTestDriver first = Prepared();
            CoreTestDriver second = Prepared();
            byte[] firstBytes = LastBearingCanonicalCodec.Encode(first.State);
            byte[] secondBytes = LastBearingCanonicalCodec.Encode(second.State);
            TestHarness.True(firstBytes.SequenceEqual(secondBytes), "canonical bytes differ");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(first.State),
                LastBearingCanonicalCodec.ComputeSha256(second.State),
                "canonical hashes differ");
        }

        private static void OneStepOneTick()
        {
            var driver = new CoreTestDriver(ColonyComposition.HumanOnly);
            long before = driver.View.GlobalTick;
            driver.Advance(1);
            TestHarness.Equal(before + 1, driver.View.GlobalTick, "global tick");
        }

        private static void ReusableStepMatchesImmutableStep()
        {
            var kernel = new LastBearingKernel();
            var buffer = new LastBearingStepBuffer();
            LastBearingState immutableState =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2011);
            LastBearingState reusableState = immutableState;

            for (var tick = 0; tick < 3000; tick++)
            {
                LastBearingTickResult expected = kernel.Step(
                    immutableState,
                    Array.Empty<LastBearingCommand>());
                kernel.StepInto(
                    reusableState,
                    Array.Empty<LastBearingCommand>(),
                    buffer);

                LastBearingState actual = buffer.State!;
                LastBearingReadModel actualView = buffer.ReadModel!;
                TestHarness.True(
                    LastBearingCanonicalCodec.Encode(expected.State)
                        .SequenceEqual(
                            LastBearingCanonicalCodec.Encode(actual)),
                    "reusable canonical state differs at tick " + tick);
                TestHarness.Equal(
                    expected.ReadModel.GlobalTick,
                    actualView.GlobalTick,
                    "reusable read-model global tick");
                TestHarness.Equal(
                    expected.ReadModel.WaterMilli,
                    actualView.WaterMilli,
                    "reusable read-model water");
                TestHarness.Equal(
                    expected.ReadModel.FactionClaimProgressMilli,
                    actualView.FactionClaimProgressMilli,
                    "reusable read-model faction progress");
                TestHarness.Equal(
                    expected.ReadModel.NextObjective,
                    actualView.NextObjective,
                    "reusable read-model objective");
                AssertEventsEqual(
                    expected.DomainEvents,
                    buffer.DomainEvents,
                    tick);

                immutableState = expected.State;
                reusableState = actual;
            }
        }

        private static void ReusableStepRejectsAtomically()
        {
            var kernel = new LastBearingKernel();
            var buffer = new LastBearingStepBuffer();
            LastBearingState initial = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2011);
            kernel.StepInto(
                initial,
                Array.Empty<LastBearingCommand>(),
                buffer);

            LastBearingState committedState = buffer.State!;
            LastBearingReadModel committedView = buffer.ReadModel!;
            byte[] committedBytes =
                LastBearingCanonicalCodec.Encode(committedState);
            int committedEventCount = buffer.DomainEvents.Count;
            LastBearingEventKind committedFirstEvent =
                committedEventCount == 0
                    ? default
                    : buffer.DomainEvents[0].Kind;

            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => kernel.StepInto(
                        committedState,
                        new LastBearingCommand[]
                        {
                            new SetPauseCommand(
                                committedState.NextCommandSequence + 1,
                                true),
                        },
                        buffer),
                    "reusable step accepted a future command sequence");

            TestHarness.Equal(
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                error.Message,
                "reusable step rejection code");
            TestHarness.True(
                ReferenceEquals(committedState, buffer.State),
                "rejected reusable step replaced committed state");
            TestHarness.True(
                ReferenceEquals(committedView, buffer.ReadModel),
                "rejected reusable step replaced committed read model");
            TestHarness.True(
                committedBytes.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(committedState)),
                "rejected reusable step mutated committed state");
            TestHarness.Equal(
                committedEventCount,
                buffer.DomainEvents.Count,
                "rejected reusable step replaced committed events");
            if (committedEventCount != 0)
            {
                TestHarness.Equal(
                    committedFirstEvent,
                    buffer.DomainEvents[0].Kind,
                    "rejected reusable step mutated committed events");
            }
        }

        private static void
            ReusableStepAllocatesNoManagedMemoryAfterWarmup()
        {
            var kernel = new LastBearingKernel();
            var buffer = new LastBearingStepBuffer();
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                2011);
            LastBearingCommand[] noCommands =
                Array.Empty<LastBearingCommand>();

            for (var tick = 0; tick < 64; tick++)
            {
                kernel.StepInto(state, noCommands, buffer);
                state = buffer.State!;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var tick = 0; tick < 10000; tick++)
            {
                kernel.StepInto(state, noCommands, buffer);
                state = buffer.State!;
            }

            long allocated = checked(
                GC.GetAllocatedBytesForCurrentThread() - before);
            TestHarness.Equal(
                0L,
                allocated,
                "warmed reusable step allocation bytes");
        }

        private static void AssertEventsEqual(
            System.Collections.Generic.IReadOnlyList<LastBearingDomainEvent>
                expected,
            System.Collections.Generic.IReadOnlyList<LastBearingDomainEvent>
                actual,
            int tick)
        {
            TestHarness.Equal(
                expected.Count,
                actual.Count,
                "reusable event count at tick " + tick);
            for (var index = 0; index < expected.Count; index++)
            {
                LastBearingDomainEvent expectedEvent = expected[index];
                LastBearingDomainEvent actualEvent = actual[index];
                TestHarness.Equal(expectedEvent.Kind, actualEvent.Kind, "event kind");
                TestHarness.Equal(expectedEvent.Cause, actualEvent.Cause, "event cause");
                TestHarness.Equal(expectedEvent.GlobalTick, actualEvent.GlobalTick, "event global tick");
                TestHarness.Equal(expectedEvent.DomainTick, actualEvent.DomainTick, "event domain tick");
                TestHarness.Equal(expectedEvent.CommandSequence, actualEvent.CommandSequence, "event command sequence");
                TestHarness.Equal(expectedEvent.SubjectId, actualEvent.SubjectId, "event subject");
                TestHarness.Equal(expectedEvent.BeforeValue, actualEvent.BeforeValue, "event before value");
                TestHarness.Equal(expectedEvent.AfterValue, actualEvent.AfterValue, "event after value");
            }
        }

        private static void
            ValidInvariantChecksAllocateNoManagedMemoryAfterWarmup()
        {
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                2011);
            for (var index = 0; index < 32; index++)
            {
                LastBearingInvariants.Validate(state);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 10000; index++)
            {
                LastBearingInvariants.Validate(state);
            }

            long allocated = checked(
                GC.GetAllocatedBytesForCurrentThread() - before);
            TestHarness.Equal(
                0L,
                allocated,
                "warmed valid invariant allocation bytes");
        }

        private static void InvariantHotPathRetainsExactFailureCodes()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2011);
            AssertInvariantCode(
                new LastBearingStateBuilder(initial)
                {
                    GlobalTick = -1,
                },
                "LAST_BEARING_GLOBAL_TICK_NEGATIVE");
            AssertInvariantCode(
                new LastBearingStateBuilder(initial)
                {
                    SettlementAccumulatorMilli =
                        LastBearingBalanceV1.FullClockScaleMilli,
                },
                "LAST_BEARING_SETTLEMENT_ACCUMULATOR_INVALID");
            AssertInvariantCode(
                new LastBearingStateBuilder(initial)
                {
                    WaterMilli = -1,
                },
                "LAST_BEARING_WATER_OUT_OF_RANGE");
            AssertInvariantCode(
                new LastBearingStateBuilder(initial)
                {
                    PauseCause = (PauseCause)int.MaxValue,
                },
                "LAST_BEARING_PAUSE_CAUSE_INVALID");
        }

        private static void AssertInvariantCode(
            LastBearingStateBuilder builder,
            string expectedCode)
        {
            var state = new LastBearingState(builder);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingInvariants.Validate(state),
                    expectedCode + " state was accepted");
            TestHarness.Equal(
                expectedCode,
                error.Message,
                expectedCode + " error code");
        }

        private static void SequenceMismatch()
        {
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2011);
            TestHarness.Throws<InvalidOperationException>(
                () => new LastBearingKernel().Step(
                    state,
                    new LastBearingCommand[]
                    {
                        new AssignResidentCommand(
                            state.NextCommandSequence + 1,
                            ResidentRoster.HumanResidentId),
                    }),
                "future command sequence was accepted");
        }

        private static void InputBounds()
        {
            TestHarness.Throws<ArgumentOutOfRangeException>(
                () => new DriveVehicleCommand(0, 1001, 0),
                "over-range throttle accepted");
            TestHarness.Throws<ArgumentOutOfRangeException>(
                () => new DriveVehicleCommand(0, 1000, -1001),
                "over-range steering accepted");
        }

        private static void DuplicateDriveCommandsFailAtomically()
        {
            CoreTestDriver driver = Prepared();
            const string transactionId = "tx:duplicate-drive";
            const string fingerprint = "fp:duplicate-drive";
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            TestHarness.Equal(
                ExpeditionPhase.Outbound,
                driver.State.ExpeditionPhase,
                "drive test phase");

            LastBearingState before = driver.State;
            byte[] canonicalBefore = LastBearingCanonicalCodec.Encode(before);
            long sequence = before.NextCommandSequence;
            var kernel = new LastBearingKernel();
            InvalidOperationException duplicateError =
                TestHarness.Throws<InvalidOperationException>(
                    () => kernel.Step(
                        before,
                        new LastBearingCommand[]
                        {
                            new SetPauseCommand(sequence, true),
                            new DriveVehicleCommand(sequence + 1, 1000, 250),
                            new DriveVehicleCommand(sequence + 2, 1000, -250),
                        }),
                    "duplicate drive commands were accepted");
            TestHarness.Equal(
                "LAST_BEARING_MULTIPLE_DRIVE_COMMANDS_PER_STEP",
                duplicateError.Message,
                "duplicate drive rejection");
            TestHarness.True(
                canonicalBefore.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(before)),
                "rejected drive batch mutated canonical state");
            TestHarness.Equal(sequence, before.NextCommandSequence, "rejected command sequence");
            TestHarness.Equal(PauseCause.None, before.PauseCause, "rejected pause state");

            LastBearingTickResult accepted = kernel.Step(
                before,
                new LastBearingCommand[]
                {
                    new DriveVehicleCommand(sequence, 1000, 250),
                });
            TestHarness.Equal(
                before.GlobalTick + 1,
                accepted.State.GlobalTick,
                "accepted global fixed tick");
            TestHarness.Equal(
                before.RoadTick + 1,
                accepted.State.RoadTick,
                "accepted road fixed tick");
            TestHarness.Equal(
                sequence + 1,
                accepted.State.NextCommandSequence,
                "accepted command sequence");
        }

        private static void PreparationCompletes()
        {
            CoreTestDriver driver = Prepared();
            TestHarness.Equal(PreparationPhase.Ready, driver.View.PreparationPhase, "preparation phase");
            TestHarness.Equal(0L, driver.View.PreparationRemainingTicks, "ready preparation remaining ticks");
            TestHarness.Equal(
                VehicleModule.WinchAssembly,
                driver.View.VehicleModule,
                "installed module");
        }

        private static void PreparationProgressIsExposed()
        {
            var driver = new CoreTestDriver(ColonyComposition.Mixed);
            driver.StartPreparation(
                ResidentRoster.RobotResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly);

            TestHarness.Equal(
                driver.State.PreparationElapsedTicks,
                driver.View.PreparationElapsedTicks,
                "preparation elapsed read model");
            TestHarness.Equal(
                driver.State.PreparationRequiredTicks,
                driver.View.PreparationRequiredTicks,
                "preparation required read model");
            TestHarness.Equal(
                driver.State.PreparationRequiredTicks
                    - driver.State.PreparationElapsedTicks,
                driver.View.PreparationRemainingTicks,
                "preparation remaining read model");

            long elapsedBefore = driver.View.PreparationElapsedTicks;
            long remainingBefore = driver.View.PreparationRemainingTicks;
            driver.Advance(17);
            TestHarness.Equal(
                elapsedBefore + 17,
                driver.View.PreparationElapsedTicks,
                "preparation elapsed advances");
            TestHarness.Equal(
                remainingBefore - 17,
                driver.View.PreparationRemainingTicks,
                "preparation remaining advances");
        }

        private static CoreTestDriver Prepared()
        {
            var driver = new CoreTestDriver(ColonyComposition.HumanOnly);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            driver.Advance(310);
            return driver;
        }
    }
}
