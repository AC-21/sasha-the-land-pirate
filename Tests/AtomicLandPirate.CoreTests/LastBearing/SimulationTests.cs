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
            harness.Run("command sequence mismatch fails closed", SequenceMismatch);
            harness.Run("drive input is bounded and quantized", InputBounds);
            harness.Run("duplicate drive commands fail before tick mutation", DuplicateDriveCommandsFailAtomically);
            harness.Run("preparation progress is exposed without new authority", PreparationProgressIsExposed);
            harness.Run("preparation completes autonomously", PreparationCompletes);
            WreckLineTests.Run(harness);
            DepotApproachRecoveryTests.Run(harness);
            DepotRepairCargoLoadingTests.Run(harness);
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
