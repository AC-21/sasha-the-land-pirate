#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class DryBellTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "Dry Bell boundary is exact for every colony composition",
                BoundaryIsExactForEveryComposition);
            harness.Run(
                "repaired turbine keeps a dry settlement alive",
                RepairedTurbineKeepsDrySettlementAlive);
            harness.Run(
                "Dry Bell can strike at home or while Sasha is on the road",
                FailureCanStrikeAtHomeOrOnRoad);
            harness.Run(
                "post-loss empty steps preserve canonical bytes",
                EmptyStepsPreserveCanonicalBytes);
            harness.Run(
                "post-loss commands reject without sequence or byte drift",
                CommandsRejectWithoutMutation);
            harness.Run(
                "same-tick Water Shift completion rescues the settlement",
                SameTickWaterShiftCompletionRescuesSettlement);
            harness.Run(
                "Dry Bell is rederived from a canonical load",
                CanonicalLoadRederivesLoss);
        }

        private static void BoundaryIsExactForEveryComposition()
        {
            ColonyComposition[] compositions =
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            };

            foreach (ColonyComposition composition in compositions)
            {
                LastBearingState initial =
                    LastBearingScenarioFactory.CreateInitial(
                        composition,
                        3301 + (int)composition);
                LastBearingReadModel oneMilli =
                    LastBearingReadModel.FromState(
                        new LastBearingStateBuilder(initial)
                        {
                            WaterMilli = 1,
                        }.Build());
                TestHarness.True(
                    !oneMilli.IsSettlementLost,
                    composition + " lost with one milli-water");
                TestHarness.Equal<string?>(
                    null,
                    oneMilli.SettlementLossReason,
                    composition + " live loss reason");

                LastBearingReadModel dry =
                    LastBearingReadModel.FromState(
                        new LastBearingStateBuilder(initial)
                        {
                            WaterMilli = 0,
                        }.Build());
                TestHarness.True(
                    dry.IsSettlementLost,
                    composition + " dry failing turbine stayed alive");
                TestHarness.Equal(
                    LastBearingReadModel.DryBellSettlementLossReason,
                    dry.SettlementLossReason,
                    composition + " loss reason");
            }
        }

        private static void RepairedTurbineKeepsDrySettlementAlive()
        {
            LastBearingState repaired =
                CityImprovementTests.CreateInstalledStateForSaveTests();
            LastBearingState dry = new LastBearingStateBuilder(repaired)
            {
                WaterMilli = 0,
            }.Build();
            LastBearingReadModel view =
                LastBearingReadModel.FromState(dry);

            TestHarness.True(
                dry.TurbineCondition != TurbineCondition.Failing,
                "repaired fixture turbine");
            TestHarness.True(
                !view.IsSettlementLost,
                "repaired dry settlement was lost");
            TestHarness.Equal<string?>(
                null,
                view.SettlementLossReason,
                "repaired dry loss reason");
        }

        private static void FailureCanStrikeAtHomeOrOnRoad()
        {
            var kernel = new LastBearingKernel();
            LastBearingState home = new LastBearingStateBuilder(
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    3304))
            {
                WaterMilli = checked(
                    -LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick),
            }.Build();
            LastBearingTickResult homeLoss = kernel.Step(
                home,
                Array.Empty<LastBearingCommand>());
            TestHarness.True(
                homeLoss.ReadModel.IsSettlementLost,
                "home Dry Bell did not strike");
            TestHarness.Equal(
                ExpeditionPhase.AtHome,
                homeLoss.State.ExpeditionPhase,
                "home loss phase");

            CoreTestDriver road = ReachRoad();
            LastBearingState roadBrink =
                new LastBearingStateBuilder(road.State)
                {
                    WaterMilli = checked(
                        -LastBearingBalanceV1
                            .FailingWaterRateMilliPerSettlementTick),
                    SettlementAccumulatorMilli =
                        LastBearingBalanceV1
                            .ExpeditionHomeClockScaleMilli,
                }.Build();
            LastBearingTickResult roadLoss = kernel.Step(
                roadBrink,
                Array.Empty<LastBearingCommand>());
            TestHarness.True(
                roadLoss.ReadModel.IsSettlementLost,
                "road Dry Bell did not strike");
            TestHarness.Equal(
                ExpeditionPhase.Outbound,
                roadLoss.State.ExpeditionPhase,
                "road loss phase");
        }

        private static void EmptyStepsPreserveCanonicalBytes()
        {
            LastBearingState lost = CreateLostState(
                ColonyComposition.Mixed,
                3305);
            byte[] before = LastBearingCanonicalCodec.Encode(lost);
            var kernel = new LastBearingKernel();

            LastBearingTickResult result = kernel.Step(
                lost,
                Array.Empty<LastBearingCommand>());
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(result.State)),
                "allocating empty step changed lost bytes");
            TestHarness.Equal(
                0,
                result.DomainEvents.Count,
                "allocating empty step emitted after loss");
            TestHarness.True(
                result.ReadModel.IsSettlementLost,
                "allocating empty step lost derived verdict");

            var buffer = new LastBearingStepBuffer();
            kernel.StepInto(
                lost,
                Array.Empty<LastBearingCommand>(),
                buffer);
            TestHarness.True(
                buffer.State != null
                    && before.SequenceEqual(
                        LastBearingCanonicalCodec.Encode(buffer.State)),
                "reusable empty step changed lost bytes");
            TestHarness.Equal(
                0,
                buffer.DomainEvents.Count,
                "reusable empty step emitted after loss");
            TestHarness.True(
                buffer.ReadModel != null
                    && buffer.ReadModel.IsSettlementLost,
                "reusable empty step lost derived verdict");
        }

        private static void CommandsRejectWithoutMutation()
        {
            LastBearingState lost = CreateLostState(
                ColonyComposition.HumanOnly,
                3306);
            byte[] before = LastBearingCanonicalCodec.Encode(lost);
            long sequence = lost.NextCommandSequence;
            var kernel = new LastBearingKernel();

            InvalidOperationException rejected =
                TestHarness.Throws<InvalidOperationException>(
                    () => kernel.Step(
                        lost,
                        new LastBearingCommand[]
                        {
                            new AssignResidentCommand(
                                sequence,
                                ResidentRoster.HumanResidentId),
                        }),
                    "post-loss command was accepted");
            TestHarness.Equal(
                "LAST_BEARING_SETTLEMENT_LOST",
                rejected.Message,
                "post-loss command rejection");
            TestHarness.Equal(
                sequence,
                lost.NextCommandSequence,
                "post-loss command sequence");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(lost)),
                "post-loss command changed canonical bytes");
        }

        private static void SameTickWaterShiftCompletionRescuesSettlement()
        {
            var driver = new CoreTestDriver(
                ColonyComposition.Mixed,
                3307);
            driver.Apply(sequence => new AssignResidentCommand(
                sequence,
                ResidentRoster.HumanResidentId));
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            driver.Apply(sequence => new SelectPreparationCommand(
                sequence,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly));
            driver.Apply(sequence => new InstallVehicleModuleCommand(
                sequence,
                VehicleModule.WinchAssembly));
            driver.Apply(sequence => new RunWaterShiftCommand(
                sequence,
                driver.State.WaterShiftCompletedCount));

            LastBearingState rescueReady =
                new LastBearingStateBuilder(driver.State)
                {
                    WaterMilli = checked(
                        -LastBearingBalanceV1
                            .FailingWaterRateMilliPerSettlementTick),
                    HotShiftElapsedTicks = checked(
                        LastBearingBalanceV1
                            .WaterShiftRequiredSettlementTicks - 1),
                }.Build();
            LastBearingTickResult rescued =
                new LastBearingKernel().Step(
                    rescueReady,
                    Array.Empty<LastBearingCommand>());

            TestHarness.Equal(
                LastBearingBalanceV1.WaterShiftOutputWaterMilli,
                rescued.State.WaterMilli,
                "same-tick rescued water");
            TestHarness.Equal(
                1L,
                rescued.State.WaterShiftCompletedCount,
                "same-tick completion count");
            TestHarness.Equal(
                HotShiftPhase.Idle,
                rescued.State.HotShiftPhase,
                "same-tick scheduler release");
            TestHarness.True(
                !rescued.ReadModel.IsSettlementLost,
                "same-tick Water Shift still lost settlement");
            TestHarness.Equal<string?>(
                null,
                rescued.ReadModel.SettlementLossReason,
                "same-tick rescue loss reason");
        }

        private static void CanonicalLoadRederivesLoss()
        {
            LastBearingState lost = CreateLostState(
                ColonyComposition.RobotOnly,
                3308);
            byte[] bytes = LastBearingCanonicalCodec.Encode(lost);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(bytes);

            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "lost canonical load");
            LastBearingReadModel view =
                LastBearingReadModel.FromState(decoded.State!);
            TestHarness.True(
                view.IsSettlementLost,
                "loaded Dry Bell was not rederived");
            TestHarness.Equal(
                LastBearingReadModel.DryBellSettlementLossReason,
                view.SettlementLossReason,
                "loaded Dry Bell reason");
            TestHarness.True(
                bytes.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State!)),
                "loaded Dry Bell bytes");
        }

        private static LastBearingState CreateLostState(
            ColonyComposition composition,
            int worldSeed)
        {
            return new LastBearingStateBuilder(
                LastBearingScenarioFactory.CreateInitial(
                    composition,
                    worldSeed))
            {
                WaterMilli = 0,
            }.Build();
        }

        private static CoreTestDriver ReachRoad()
        {
            var driver = new CoreTestDriver(
                ColonyComposition.Mixed,
                3309);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            driver.Advance(
                checked((int)driver.View.PreparationRemainingTicks));
            const string transactionId = "tx:dry-bell-road";
            const string fingerprint = "fp:dry-bell-road";
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
                "road fixture phase");
            return driver;
        }
    }
}
