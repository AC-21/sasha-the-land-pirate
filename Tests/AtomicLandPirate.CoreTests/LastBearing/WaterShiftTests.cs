#nullable enable

using System;
using System.IO;
using System.Linq;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class WaterShiftTests
    {
        internal static void Run(TestHarness harness, string repoRoot)
        {
            harness.Run(
                "Water Shift spends one fuel and credits exactly ten thousand water",
                CreditsExactWaterWithoutParts);
            harness.Run(
                "Parts and Water shifts share one fail-closed service scheduler",
                WorkOrdersAreMutuallyExclusive);
            harness.Run(
                "Water Shift replay and stale expectations are exact",
                ReplayAndStaleExpectationsAreExact);
            harness.Run(
                "Water Shift retains the exact route fuel reserve for both rigs",
                RouteFuelReserveIsExactForBothModules);
            harness.Run(
                "adverse repeat toll is reserved by both service orders",
                AdverseRepeatTollIsReservedByBothOrders);
            harness.Run(
                "Water Shift checkpoint and Workshop Push ownership are exact",
                CheckpointAndWorkshopPushAreExact);
            harness.Run(
                "Dust Front stalls Water Shift and releases Workshop Push",
                DustFrontStallReleasesPreparation);
            harness.Run(
                "Water Shift completes for human robot and mixed colonies",
                CompletesForEveryColonyComposition);
            harness.Run(
                "Water Shift reserves full output headroom and pauses exactly",
                CapacityReservationAndPauseAreExact);
            harness.Run(
                "repaired auxiliary pump inflow preserves Water Shift headroom",
                PositiveOrdinaryInflowPreservesHeadroom);
            harness.Run(
                "returned water cargo cannot consume reserved Water Shift headroom",
                ReturnedWaterCargoOverReservationIsAtomic);
            harness.Run(
                "exact-fit returned water cargo preserves the complete Water Shift output",
                ExactFitReturnedWaterCargoPreservesOutput);
            harness.Run(
                "Water Shift state round trips and schema v10 infers Parts Shift",
                () => SaveAndSchemaV10MigrationRoundTrip(repoRoot));
            harness.Run(
                "forged Water Shift state fails closed",
                ForgedStatesFailClosed);
        }

        private static void CreditsExactWaterWithoutParts()
        {
            CoreTestDriver driver = PlannedCell(3101);
            long fuelBefore = driver.State.FuelUnits;
            long partsBefore = driver.State.PartsUnits;
            long ordinaryWaterTrend =
                driver.View.WaterTrendMilliPerSettlementTick;
            TestHarness.True(
                driver.View.IsWaterShiftRunAvailable,
                "delivered cell did not expose Water Shift");
            TestHarness.Equal(
                LastBearingBalanceV1.WaterShiftFuelCostUnits,
                driver.View.WaterShiftFuelCostUnits,
                "read-model fuel contract");
            TestHarness.Equal(
                LastBearingBalanceV1.WaterShiftOutputWaterMilli,
                driver.View.WaterShiftOutputWaterMilli,
                "read-model output contract");

            LastBearingTickResult started = driver.Apply(sequence =>
                new RunWaterShiftCommand(
                    sequence,
                    driver.State.WaterShiftCompletedCount));
            long waterAfterStart = driver.State.WaterMilli;
            TestHarness.Equal(
                fuelBefore - LastBearingBalanceV1.WaterShiftFuelCostUnits,
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
                ServiceWorkOrder.WaterShift,
                driver.State.ActiveServiceWorkOrder,
                "active order");
            TestHarness.Equal(
                0L,
                driver.State.HotShiftElapsedTicks,
                "start step must not advance production");
            TestHarness.True(
                started.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.WaterShiftStarted
                    && item.SubjectId == LastBearingState.WaterShiftId
                    && item.BeforeValue == fuelBefore
                    && item.AfterValue == driver.State.FuelUnits),
                "start event");

            TestHarness.Equal(
                ordinaryWaterTrend,
                driver.View.WaterTrendMilliPerSettlementTick,
                "Water Shift inherited the Parts Shift water modifier");
            driver.Advance(119);
            TestHarness.Equal(
                LastBearingBalanceV1.WaterShiftRequiredSettlementTicks - 1,
                driver.State.HotShiftElapsedTicks,
                "pre-completion progress");

            LastBearingTickResult completed = new LastBearingKernel().Step(
                driver.State,
                Array.Empty<LastBearingCommand>());
            TestHarness.Equal(
                HotShiftPhase.Idle,
                completed.State.HotShiftPhase,
                "completion phase");
            TestHarness.Equal(
                ServiceWorkOrder.None,
                completed.State.ActiveServiceWorkOrder,
                "completion releases scheduler");
            TestHarness.Equal(
                1L,
                completed.State.WaterShiftCompletedCount,
                "water completion count");
            TestHarness.Equal(
                0L,
                completed.State.HotShiftCompletedCount,
                "parts completion count");
            TestHarness.Equal(
                partsBefore,
                completed.State.PartsUnits,
                "Water Shift produced parts");
            TestHarness.Equal(
                checked(
                    waterAfterStart
                    + ordinaryWaterTrend
                        * LastBearingBalanceV1
                            .WaterShiftRequiredSettlementTicks
                    + LastBearingBalanceV1.WaterShiftOutputWaterMilli),
                completed.State.WaterMilli,
                "exact water credit");
            TestHarness.True(
                completed.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.WaterShiftCompleted
                    && item.SubjectId == LastBearingState.WaterShiftId
                    && item.BeforeValue
                        == completed.State.WaterMilli
                            - LastBearingBalanceV1
                                .WaterShiftOutputWaterMilli
                    && item.AfterValue == completed.State.WaterMilli),
                "completion event");
        }

        private static void WorkOrdersAreMutuallyExclusive()
        {
            CoreTestDriver water = PlannedCell(3102);
            water.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 0));
            AssertRejectedWithoutMutation(
                water.State,
                new RunHotShiftCommand(
                    water.State.NextCommandSequence,
                    water.State.HotShiftCompletedCount),
                "LAST_BEARING_SERVICE_WORK_ORDER_ACTIVE",
                "Parts Shift while Water Shift owns scheduler");

            LastBearingTickResult waterReplay = water.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 0));
            TestHarness.True(
                waterReplay.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "Water Shift replay");

            CoreTestDriver parts = PlannedCell(3103);
            parts.Apply(sequence => new RunHotShiftCommand(sequence, 0));
            AssertRejectedWithoutMutation(
                parts.State,
                new RunWaterShiftCommand(
                    parts.State.NextCommandSequence,
                    parts.State.WaterShiftCompletedCount),
                "LAST_BEARING_SERVICE_WORK_ORDER_ACTIVE",
                "Water Shift while Parts Shift owns scheduler");
        }

        private static void ReplayAndStaleExpectationsAreExact()
        {
            CoreTestDriver driver = PlannedCell(3114);
            driver.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 0));
            driver.Advance(
                checked((int)driver.View.HotShiftRemainingTicks));
            TestHarness.Equal(
                1L,
                driver.State.WaterShiftCompletedCount,
                "first completed count");

            LastBearingState completed = driver.State;
            var kernel = new LastBearingKernel();
            LastBearingTickResult ordinaryTick = kernel.Step(
                completed,
                Array.Empty<LastBearingCommand>());
            LastBearingTickResult replay = kernel.Step(
                completed,
                new LastBearingCommand[]
                {
                    new RunWaterShiftCommand(
                        completed.NextCommandSequence,
                        0),
                });
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "post-completion replay event");
            TestHarness.True(
                !replay.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.WaterShiftCompleted),
                "post-completion replay emitted completion");
            TestHarness.Equal(
                ordinaryTick.State.FuelUnits,
                replay.State.FuelUnits,
                "post-completion replay fuel");
            TestHarness.Equal(
                ordinaryTick.State.WaterMilli,
                replay.State.WaterMilli,
                "post-completion replay water");
            TestHarness.Equal(
                ordinaryTick.State.WaterShiftCompletedCount,
                replay.State.WaterShiftCompletedCount,
                "post-completion replay count");
            TestHarness.Equal(
                ordinaryTick.State.PartsUnits,
                replay.State.PartsUnits,
                "post-completion replay parts");

            AssertRejectedWithoutMutation(
                replay.State,
                new RunWaterShiftCommand(
                    replay.State.NextCommandSequence,
                    checked(
                        replay.State.WaterShiftCompletedCount + 1)),
                "LAST_BEARING_WATER_SHIFT_EXPECTED_COMPLETION_MISMATCH",
                "future completion expectation");

            driver = new CoreTestDriver(replay.State);
            driver.Apply(sequence =>
                new RunWaterShiftCommand(
                    sequence,
                    driver.State.WaterShiftCompletedCount));
            driver.Advance(
                checked((int)driver.View.HotShiftRemainingTicks));
            TestHarness.Equal(
                2L,
                driver.State.WaterShiftCompletedCount,
                "second completed count");
            AssertRejectedWithoutMutation(
                driver.State,
                new RunWaterShiftCommand(
                    driver.State.NextCommandSequence,
                    0),
                "LAST_BEARING_WATER_SHIFT_EXPECTED_COMPLETION_MISMATCH",
                "too-stale completion expectation");
        }

        private static void RouteFuelReserveIsExactForBothModules()
        {
            foreach (VehicleModule module in new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            })
            {
                CoreTestDriver source = PlannedCell(
                    ColonyComposition.Mixed,
                    PreparationChoice.CivicBuffer,
                    module,
                    checked(3115 + (int)module));
                long routeReserve =
                    LastBearingBalanceV1.RouteFuelCost(module);
                long exactFuel = checked(
                    routeReserve
                    + LastBearingBalanceV1.WaterShiftFuelCostUnits);
                LastBearingState exact =
                    new LastBearingStateBuilder(source.State)
                    {
                        FuelUnits = exactFuel,
                    }.Build();
                LastBearingReadModel exactView =
                    LastBearingReadModel.FromState(exact);
                TestHarness.Equal(
                    routeReserve,
                    exactView.ServiceWorkOrderRouteFuelReserveUnits,
                    module + " presented reserve");
                TestHarness.True(
                    exactView.IsWaterShiftRunAvailable,
                    module + " exact reserve availability");
                LastBearingTickResult started =
                    new LastBearingKernel().Step(
                        exact,
                        new LastBearingCommand[]
                        {
                            new RunWaterShiftCommand(
                                exact.NextCommandSequence,
                                exact.WaterShiftCompletedCount),
                        });
                TestHarness.Equal(
                    routeReserve,
                    started.State.FuelUnits,
                    module + " retained route reserve");

                LastBearingState oneBelow =
                    new LastBearingStateBuilder(source.State)
                    {
                        FuelUnits = checked(exactFuel - 1),
                    }.Build();
                TestHarness.True(
                    !LastBearingReadModel.FromState(oneBelow)
                        .IsWaterShiftRunAvailable,
                    module + " one-below reserve availability");
                AssertRejectedWithoutMutation(
                    oneBelow,
                    new RunWaterShiftCommand(
                        oneBelow.NextCommandSequence,
                        oneBelow.WaterShiftCompletedCount),
                    "LAST_BEARING_WATER_SHIFT_ROUTE_FUEL_RESERVE_REQUIRED",
                    module + " one-below route reserve");
            }
        }

        private static void AdverseRepeatTollIsReservedByBothOrders()
        {
            CoreTestDriver source =
                RepeatWreckLineTests.ReachAdverseRepeatReady(3117);
            TestHarness.Equal(
                LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                source.State.FutureRouteTollFuelUnits,
                "adverse repeat toll");
            long payableReserve = checked(
                LastBearingBalanceV1.RouteFuelCost(
                    source.State.VehicleModule)
                + source.State.FutureRouteTollFuelUnits);
            long maximumStartWater = checked(
                source.View.WaterCapacityMilli
                - LastBearingBalanceV1.WaterShiftOutputWaterMilli);

            foreach (ServiceWorkOrder order in new[]
            {
                ServiceWorkOrder.PartsShift,
                ServiceWorkOrder.WaterShift,
            })
            {
                long shiftFuelCost =
                    order == ServiceWorkOrder.PartsShift
                        ? LastBearingBalanceV1.HotShiftFuelCostUnits
                        : LastBearingBalanceV1.WaterShiftFuelCostUnits;
                LastBearingState exact =
                    new LastBearingStateBuilder(source.State)
                    {
                        FuelUnits = checked(
                            payableReserve + shiftFuelCost),
                        WaterMilli = Math.Min(
                            source.State.WaterMilli,
                            maximumStartWater),
                    }.Build();
                LastBearingReadModel exactView =
                    LastBearingReadModel.FromState(exact);
                TestHarness.Equal(
                    payableReserve,
                    exactView.ServiceWorkOrderRouteFuelReserveUnits,
                    order + " adverse payable reserve");
                TestHarness.True(
                    order == ServiceWorkOrder.PartsShift
                        ? exactView.IsHotShiftRunAvailable
                        : exactView.IsWaterShiftRunAvailable,
                    order + " adverse exact availability");
                LastBearingCommand exactCommand =
                    order == ServiceWorkOrder.PartsShift
                        ? new RunHotShiftCommand(
                            exact.NextCommandSequence,
                            exact.HotShiftCompletedCount)
                        : new RunWaterShiftCommand(
                            exact.NextCommandSequence,
                            exact.WaterShiftCompletedCount);
                LastBearingTickResult started =
                    new LastBearingKernel().Step(
                        exact,
                        new[] { exactCommand });
                TestHarness.Equal(
                    payableReserve,
                    started.State.FuelUnits,
                    order + " adverse retained reserve");

                LastBearingState oneBelow =
                    new LastBearingStateBuilder(exact)
                    {
                        FuelUnits = checked(
                            payableReserve + shiftFuelCost - 1),
                    }.Build();
                LastBearingReadModel belowView =
                    LastBearingReadModel.FromState(oneBelow);
                TestHarness.True(
                    order == ServiceWorkOrder.PartsShift
                        ? !belowView.IsHotShiftRunAvailable
                        : !belowView.IsWaterShiftRunAvailable,
                    order + " adverse one-below availability");
                LastBearingCommand belowCommand =
                    order == ServiceWorkOrder.PartsShift
                        ? new RunHotShiftCommand(
                            oneBelow.NextCommandSequence,
                            oneBelow.HotShiftCompletedCount)
                        : new RunWaterShiftCommand(
                            oneBelow.NextCommandSequence,
                            oneBelow.WaterShiftCompletedCount);
                AssertRejectedWithoutMutation(
                    oneBelow,
                    belowCommand,
                    order == ServiceWorkOrder.PartsShift
                        ? "LAST_BEARING_HOT_SHIFT_ROUTE_FUEL_RESERVE_REQUIRED"
                        : "LAST_BEARING_WATER_SHIFT_ROUTE_FUEL_RESERVE_REQUIRED",
                    order + " adverse one-below reserve");
            }
        }

        private static void CapacityReservationAndPauseAreExact()
        {
            CoreTestDriver baseCell = PlannedCell(3104);
            long capacity = baseCell.View.WaterCapacityMilli;
            long maximumStartWater = checked(
                capacity
                - LastBearingBalanceV1.WaterShiftOutputWaterMilli);
            LastBearingState overfull = new LastBearingStateBuilder(
                baseCell.State)
            {
                WaterMilli = checked(maximumStartWater + 1),
            }.BuildUnchecked();
            LastBearingReadModel overfullView =
                LastBearingReadModel.FromState(overfull);
            TestHarness.True(
                !overfullView.IsWaterShiftRunAvailable
                    && overfullView.IsHotShiftRunAvailable,
                "capacity gate hid the wrong work order");
            AssertRejectedWithoutMutation(
                overfull,
                new RunWaterShiftCommand(
                    overfull.NextCommandSequence,
                    overfull.WaterShiftCompletedCount),
                "LAST_BEARING_WATER_SHIFT_CAPACITY_REQUIRED",
                "Water Shift without full-fit headroom");

            var driver = new CoreTestDriver(
                new LastBearingStateBuilder(baseCell.State)
                {
                    WaterMilli = maximumStartWater,
                }.BuildUnchecked());
            driver.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 0));
            driver.Advance(7);
            long heldProgress = driver.State.HotShiftElapsedTicks;
            long heldWater = driver.State.WaterMilli;
            driver.Apply(sequence => new SetPauseCommand(sequence, true));
            driver.Advance(5);
            TestHarness.Equal(
                heldProgress,
                driver.State.HotShiftElapsedTicks,
                "paused progress");
            TestHarness.Equal(
                heldWater,
                driver.State.WaterMilli,
                "paused water");
            driver.Apply(sequence => new SetPauseCommand(sequence, false));
            driver.Advance(
                checked((int)driver.View.HotShiftRemainingTicks));
            TestHarness.Equal(
                capacity,
                driver.State.WaterMilli,
                "reserved output did not fit exactly");
            TestHarness.Equal(
                1L,
                driver.State.WaterShiftCompletedCount,
                "capacity-edge completion count");
        }

        private static void PositiveOrdinaryInflowPreservesHeadroom()
        {
            LastBearingState installed =
                CityImprovementTests.CreateInstalledStateForSaveTests();
            long capacity =
                LastBearingBalanceV1.EffectiveWaterCapacityMilli(
                    installed.InstalledCityImprovement);
            long reservedCeiling = checked(
                capacity
                - LastBearingBalanceV1.WaterShiftOutputWaterMilli);
            long routeReserve =
                LastBearingBalanceV1.RouteFuelCost(
                    installed.PlannedModule);
            var driver = new CoreTestDriver(
                new LastBearingStateBuilder(installed)
                {
                    WaterMilli = checked(reservedCeiling - 100),
                    FuelUnits = checked(
                        routeReserve
                        + LastBearingBalanceV1.WaterShiftFuelCostUnits
                        + 1),
                }.Build());
            long ordinaryTrend =
                driver.View.WaterTrendMilliPerSettlementTick;
            TestHarness.True(
                driver.State.TurbineCondition
                    == TurbineCondition.BearingRepaired
                    && driver.State.InstalledCityImprovement
                        == CityImprovementKind.RefurbishedAuxiliaryPump
                    && ordinaryTrend > 0,
                "positive repaired-pump fixture");

            driver.Apply(sequence =>
                new RunWaterShiftCommand(
                    sequence,
                    driver.State.WaterShiftCompletedCount));
            TestHarness.Equal(
                ordinaryTrend,
                driver.View.WaterTrendMilliPerSettlementTick,
                "Water Shift changed ordinary repaired-pump inflow");
            driver.Advance(6);
            TestHarness.Equal(
                reservedCeiling,
                driver.State.WaterMilli,
                "positive inflow crossed reserved ceiling");
            driver.Advance(
                checked((int)driver.View.HotShiftRemainingTicks));
            TestHarness.Equal(
                capacity,
                driver.State.WaterMilli,
                "reserved output did not complete at capacity");
            TestHarness.Equal(
                1L,
                driver.State.WaterShiftCompletedCount,
                "positive-inflow completion count");
        }

        private static void ReturnedWaterCargoOverReservationIsAtomic()
        {
            const int worldSeed = 3119;
            CoreTestDriver returned = ReachReturnedWaterCargo(worldSeed);
            long reservedCeiling = checked(
                returned.View.WaterCapacityMilli
                - LastBearingBalanceV1.WaterShiftOutputWaterMilli);
            long cargoWater = returned.State.LiquidCargoQuantityMilli;
            LastBearingState active = ActiveReturnedWaterShift(
                returned.State,
                checked(reservedCeiling - cargoWater + 1));
            byte[] canonicalBefore =
                LastBearingCanonicalCodec.Encode(active);

            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        active,
                        new LastBearingCommand[]
                        {
                            new CreditCityReturnCommand(
                                active.NextCommandSequence,
                                ReturnTransactionId(worldSeed),
                                ReturnFingerprint(worldSeed)),
                        }),
                    "over-ceiling returned water was accepted");
            TestHarness.Equal(
                "LAST_BEARING_RETURN_WATER_SHIFT_HEADROOM_RESERVED",
                error.Message,
                "over-ceiling return rejection code");
            TestHarness.Equal(
                LiquidCargoCustody.Vehicle,
                active.LiquidCargoCustody,
                "over-ceiling return changed cargo custody");
            TestHarness.Equal(
                TransactionPhase.ReturnPending,
                active.TransactionPhase,
                "over-ceiling return changed transaction");
            TestHarness.True(
                canonicalBefore.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(active)),
                "over-ceiling return changed canonical state");
        }

        private static void ExactFitReturnedWaterCargoPreservesOutput()
        {
            const int worldSeed = 3120;
            CoreTestDriver returned = ReachReturnedWaterCargo(worldSeed);
            long capacity = returned.View.WaterCapacityMilli;
            long reservedCeiling = checked(
                capacity
                - LastBearingBalanceV1.WaterShiftOutputWaterMilli);
            long cargoWater = returned.State.LiquidCargoQuantityMilli;
            LastBearingState active = ActiveReturnedWaterShift(
                returned.State,
                checked(reservedCeiling - cargoWater));

            LastBearingTickResult credited =
                new LastBearingKernel().Step(
                    active,
                    new LastBearingCommand[]
                    {
                        new CreditCityReturnCommand(
                            active.NextCommandSequence,
                            ReturnTransactionId(worldSeed),
                            ReturnFingerprint(worldSeed)),
                    });
            TestHarness.Equal(
                LiquidCargoCustody.Settlement,
                credited.State.LiquidCargoCustody,
                "exact-fit return cargo custody");
            TestHarness.Equal(
                TransactionPhase.CityCredited,
                credited.State.TransactionPhase,
                "exact-fit return transaction");
            TestHarness.Equal(
                reservedCeiling,
                credited.State.WaterMilli,
                "exact-fit return consumed reserved headroom");
            TestHarness.Equal(
                LastBearingBalanceV1.WaterShiftOutputWaterMilli,
                capacity - credited.State.WaterMilli,
                "exact-fit return did not preserve complete output");
            TestHarness.Equal(
                ServiceWorkOrder.WaterShift,
                credited.State.ActiveServiceWorkOrder,
                "exact-fit return released active order");

            LastBearingTickResult completed =
                new LastBearingKernel().Step(
                    credited.State,
                    new LastBearingCommand[]
                    {
                        new SetPauseCommand(
                            credited.State.NextCommandSequence,
                            false),
                    });
            LastBearingDomainEvent completion =
                completed.DomainEvents.Single(item =>
                    item.Kind
                        == LastBearingEventKind.WaterShiftCompleted);
            TestHarness.Equal(
                LastBearingBalanceV1.WaterShiftOutputWaterMilli,
                completion.AfterValue - completion.BeforeValue,
                "exact-fit return clamped or spilled Water Shift output");
            TestHarness.Equal(
                1L,
                completed.State.WaterShiftCompletedCount,
                "exact-fit completion count");
            TestHarness.Equal(
                ServiceWorkOrder.None,
                completed.State.ActiveServiceWorkOrder,
                "exact-fit completion did not release scheduler");
            TestHarness.True(
                completed.State.WaterMilli <= capacity,
                "exact-fit completion exceeded storage capacity");
        }

        private static void CheckpointAndWorkshopPushAreExact()
        {
            CoreTestDriver driver = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                3109);
            driver.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 0));
            long preparationAtStart =
                driver.State.PreparationElapsedTicks;
            TestHarness.True(
                driver.View.IsPreparationStalledByHotShift,
                "Water Shift did not own the Workshop Push service slot");
            driver.Advance(59);
            TestHarness.Equal(
                preparationAtStart,
                driver.State.PreparationElapsedTicks,
                "Workshop Push advanced before checkpoint");

            LastBearingTickResult checkpoint =
                new LastBearingKernel().Step(
                    driver.State,
                    Array.Empty<LastBearingCommand>());
            TestHarness.Equal(
                LastBearingBalanceV1.WaterShiftCheckpointSettlementTick,
                checkpoint.State.HotShiftElapsedTicks,
                "checkpoint tick");
            TestHarness.Equal(
                preparationAtStart,
                checkpoint.State.PreparationElapsedTicks,
                "Workshop Push advanced on checkpoint");
            TestHarness.True(
                checkpoint.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind
                            .WaterShiftCheckpointReached
                    && item.SubjectId == LastBearingState.WaterShiftId
                    && item.BeforeValue
                        == LastBearingBalanceV1
                            .WaterShiftCheckpointSettlementTick - 1
                    && item.AfterValue
                        == LastBearingBalanceV1
                            .WaterShiftCheckpointSettlementTick),
                "checkpoint event");
        }

        private static void DustFrontStallReleasesPreparation()
        {
            CoreTestDriver driver = PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly,
                3110);
            driver = new CoreTestDriver(
                new LastBearingStateBuilder(driver.State)
                {
                    WaterMilli = 59990,
                    DustFrontProgressTicks =
                        LastBearingBalanceV1
                            .DustFrontThresholdCrisisTicks - 1,
                }.Build());
            driver.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 0));
            TestHarness.Equal(
                DustFrontOutcome.Breached,
                driver.State.DustFrontOutcome,
                "Dust Front breach setup");
            TestHarness.True(
                driver.View.IsHotShiftStalledByDustFront
                    && !driver.View.IsHotShiftActivelyWorking,
                "Water Shift breach stall");
            long preparationBeforeAcknowledgement =
                driver.State.PreparationElapsedTicks;
            long waterBeforeAcknowledgement =
                driver.State.WaterMilli;

            driver.Apply(sequence =>
                new AcknowledgeDustFrontCommand(sequence));
            TestHarness.Equal(
                0L,
                driver.State.HotShiftElapsedTicks,
                "stalled Water Shift advanced");
            TestHarness.Equal(
                preparationBeforeAcknowledgement + 1,
                driver.State.PreparationElapsedTicks,
                "Dust Front stall did not release Workshop Push");
            TestHarness.True(
                driver.View.IsPreparationActivelyWorking
                    && !driver.View.IsPreparationStalledByHotShift,
                "released Workshop Push status");
            TestHarness.Equal(
                waterBeforeAcknowledgement
                    + LastBearingBalanceV1
                        .FailingWaterRateMilliPerSettlementTick
                    + LastBearingBalanceV1
                        .WorkshopWaterModifierMilliPerSettlementTick,
                driver.State.WaterMilli,
                "stalled Water Shift charged machine-water draw");
        }

        private static void CompletesForEveryColonyComposition()
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
                    checked(3111 + (int)composition));
                long partsBefore = driver.State.PartsUnits;
                TestHarness.True(
                    driver.View.IsWaterShiftRunAvailable,
                    composition + " availability");
                driver.Apply(sequence =>
                    new RunWaterShiftCommand(sequence, 0));
                driver.Advance(
                    checked((int)driver.View.HotShiftRemainingTicks));
                TestHarness.Equal(
                    1L,
                    driver.State.WaterShiftCompletedCount,
                    composition + " completion");
                TestHarness.Equal(
                    partsBefore,
                    driver.State.PartsUnits,
                    composition + " parts conservation");
                TestHarness.Equal(
                    ServiceWorkOrder.None,
                    driver.State.ActiveServiceWorkOrder,
                    composition + " scheduler release");
            }
        }

        private static void SaveAndSchemaV10MigrationRoundTrip(
            string repoRoot)
        {
            CoreTestDriver active = PlannedCell(3105);
            active.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 0));
            active.Advance(120);
            active.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 1));
            active.Advance(37);
            byte[] canonical =
                LastBearingCanonicalCodec.Encode(active.State);
            string profile = FreshProfile(repoRoot, "active-water");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted =
                store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                "Water Shift persist: " + persisted.Code);
            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                "Water Shift load: " + loaded.Code);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(
                    loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "Water Shift decode");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                decoded.State!.SchemaVersion,
                "restored schema");
            TestHarness.Equal(
                ServiceWorkOrder.WaterShift,
                decoded.State.ActiveServiceWorkOrder,
                "restored active order");
            TestHarness.Equal(
                1L,
                decoded.State.WaterShiftCompletedCount,
                "restored water completion count");
            TestHarness.Equal(
                37L,
                decoded.State.HotShiftElapsedTicks,
                "restored progress");
            TestHarness.True(
                canonical.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State)),
                "Water Shift canonical bytes changed");

            var kernel = new LastBearingKernel();
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(
                    kernel.Step(
                        active.State,
                        Array.Empty<LastBearingCommand>()).State),
                LastBearingCanonicalCodec.ComputeSha256(
                    kernel.Step(
                        decoded.State,
                        Array.Empty<LastBearingCommand>()).State),
                "Water Shift deterministic continuation");

            CoreTestDriver legacyParts = PlannedCell(3106);
            legacyParts.Apply(sequence =>
                new RunHotShiftCommand(sequence, 0));
            legacyParts.Advance(19);
            byte[] legacyV10 =
                LastBearingCanonicalCodec
                    .EncodeLegacyV10ForMigrationTests(
                        legacyParts.State);
            LastBearingDecodeResult migrated =
                LastBearingCanonicalCodec.TryDecode(legacyV10);
            TestHarness.True(
                migrated.Succeeded && migrated.State != null,
                "schema v10 migration");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                migrated.State!.SchemaVersion,
                "schema v10 migrated version");
            TestHarness.Equal(
                ServiceWorkOrder.PartsShift,
                migrated.State.ActiveServiceWorkOrder,
                "schema v10 active order inference");
            TestHarness.Equal(
                0L,
                migrated.State.WaterShiftCompletedCount,
                "schema v10 water completion default");
            TestHarness.True(
                legacyV10.SequenceEqual(
                    LastBearingCanonicalCodec
                        .EncodeLegacyV10ForMigrationTests(
                            migrated.State)),
                "schema v10 canonical bytes changed");

            CoreTestDriver legacyIdle = PlannedCell(3118);
            byte[] legacyIdleV10 =
                LastBearingCanonicalCodec
                    .EncodeLegacyV10ForMigrationTests(
                        legacyIdle.State);
            LastBearingDecodeResult migratedIdle =
                LastBearingCanonicalCodec.TryDecode(legacyIdleV10);
            TestHarness.True(
                migratedIdle.Succeeded && migratedIdle.State != null,
                "schema v10 idle migration");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                migratedIdle.State!.SchemaVersion,
                "schema v10 idle migrated version");
            TestHarness.Equal(
                HotShiftPhase.Idle,
                migratedIdle.State.HotShiftPhase,
                "schema v10 idle phase");
            TestHarness.Equal(
                ServiceWorkOrder.None,
                migratedIdle.State.ActiveServiceWorkOrder,
                "schema v10 idle order inference");
            TestHarness.Equal(
                0L,
                migratedIdle.State.WaterShiftCompletedCount,
                "schema v10 idle water completion default");
        }

        private static void ForgedStatesFailClosed()
        {
            CoreTestDriver idle = PlannedCell(3107);
            AssertInvariantRejected(
                new LastBearingStateBuilder(idle.State)
                {
                    ActiveServiceWorkOrder =
                        ServiceWorkOrder.PartsShift,
                }.BuildUnchecked(),
                "LAST_BEARING_HOT_SHIFT_IDLE_STATE_INVALID",
                "idle scheduler ownership");

            CoreTestDriver active = PlannedCell(3108);
            active.Apply(sequence =>
                new RunWaterShiftCommand(sequence, 0));
            AssertInvariantRejected(
                new LastBearingStateBuilder(active.State)
                {
                    WaterMilli = checked(
                        active.View.WaterCapacityMilli
                        - LastBearingBalanceV1
                            .WaterShiftOutputWaterMilli
                        + 1),
                }.BuildUnchecked(),
                "LAST_BEARING_WATER_SHIFT_HEADROOM_INVALID",
                "forged reserved headroom");
        }

        private static CoreTestDriver PlannedCell(int worldSeed)
        {
            return PlannedCell(
                ColonyComposition.Mixed,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                worldSeed);
        }

        private static CoreTestDriver PlannedCell(
            ColonyComposition composition,
            PreparationChoice choice,
            VehicleModule module,
            int worldSeed)
        {
            var driver = new CoreTestDriver(
                composition,
                worldSeed);
            string residentId =
                composition == ColonyComposition.RobotOnly
                    ? ResidentRoster.RobotResidentId
                    : ResidentRoster.HumanResidentId;
            driver.Apply(sequence =>
                new AssignResidentCommand(
                    sequence,
                    residentId));
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            driver.Apply(sequence =>
                new SelectPreparationCommand(
                    sequence,
                    choice,
                    module));
            driver.Apply(sequence =>
                new InstallVehicleModuleCommand(
                    sequence,
                    module));
            return driver;
        }

        private static CoreTestDriver ReachReturnedWaterCargo(int worldSeed)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.Mixed,
                worldSeed);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank);
            AdvanceUntil(
                driver,
                model => model.PreparationPhase == PreparationPhase.Ready,
                drive: false,
                "returned-water preparation");
            driver.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    ReturnTransactionId(worldSeed),
                    ReturnFingerprint(worldSeed)));
            driver.Apply(sequence =>
                new DebitCityManifestCommand(
                    sequence,
                    ReturnTransactionId(worldSeed),
                    ReturnFingerprint(worldSeed)));
            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "returned-water depot approach");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence =>
                new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.TakeBearing));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            driver.Apply(sequence =>
                new ChooseLiquidReturnCommand(
                    sequence,
                    LiquidCargoKind.Water));
            driver.Apply(sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    ReturnTransactionId(worldSeed),
                    ReturnFingerprint(worldSeed)));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "returned-water home arrival");
            TestHarness.Equal(
                LiquidCargoKind.Water,
                driver.State.LiquidCargoKind,
                "returned-water fixture kind");
            TestHarness.Equal(
                LiquidCargoCustody.Vehicle,
                driver.State.LiquidCargoCustody,
                "returned-water fixture custody");
            TestHarness.Equal(
                TransactionPhase.ReturnPending,
                driver.State.TransactionPhase,
                "returned-water fixture transaction");
            return driver;
        }

        private static LastBearingState ActiveReturnedWaterShift(
            LastBearingState returned,
            long waterMilli)
        {
            return new LastBearingStateBuilder(returned)
            {
                PauseCause = PauseCause.Explicit,
                WaterMilli = waterMilli,
                HotShiftPhase = HotShiftPhase.InProgress,
                HotShiftElapsedTicks = checked(
                    LastBearingBalanceV1
                        .WaterShiftRequiredSettlementTicks - 1),
                HotShiftRequiredTicks =
                    LastBearingBalanceV1
                        .WaterShiftRequiredSettlementTicks,
                HotShiftFuelCommittedUnits =
                    LastBearingBalanceV1.WaterShiftFuelCostUnits,
                ActiveServiceWorkOrder =
                    ServiceWorkOrder.WaterShift,
            }.Build();
        }

        private static void AdvanceUntil(
            CoreTestDriver driver,
            Func<LastBearingReadModel, bool> predicate,
            bool drive,
            string label)
        {
            for (var ticks = 0; ticks < 1000; ticks++)
            {
                if (predicate(driver.View))
                {
                    return;
                }

                driver.OperateWreckLineIfAvailable();
                if (drive)
                {
                    driver.Apply(sequence =>
                        new DriveVehicleCommand(sequence, 1000, 0));
                }
                else
                {
                    driver.Advance(1);
                }
            }

            throw new InvalidOperationException(
                label + " exceeded deterministic tick budget");
        }

        private static string ReturnTransactionId(int worldSeed)
        {
            return "tx:water-shift-cargo:" + worldSeed;
        }

        private static string ReturnFingerprint(int worldSeed)
        {
            return "fp:water-shift-cargo:" + worldSeed;
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
            TestHarness.Equal(
                expectedCode,
                error.Message,
                label + " code");
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
            TestHarness.Equal(
                expectedCode,
                error.Message,
                label + " code");
        }

        private static string FreshProfile(
            string repoRoot,
            string caseName)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/water-shift",
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
