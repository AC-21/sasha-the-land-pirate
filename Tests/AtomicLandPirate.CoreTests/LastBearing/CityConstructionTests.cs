#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class CityConstructionTests
    {
        private const string ReleasedInactiveV3Base64 =
            "QUxQTEJDMDEDAAMAAAAhAGxhc3QtYmVhcmluZy1wcm90b3R5cGUtYmFsYW5jZS12MQUJAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABQBzYXNoYQITAHJlc2lk" +
            "ZW50Omh1bWFuOjAwMDEBAAAAEwByZXNpZGVudDpyb2JvdDowMDAxAgAAAAAAAAAAAMDUAQAAAAAAGAAAAAAAAAAS" +
            "AAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAOgDAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEA" +
            "AAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

        private const string ReleasedActiveV3Base64 =
            "QUxQTEJDMDEDAAMAAAAhAGxhc3QtYmVhcmluZy1wcm90b3R5cGUtYmFsYW5jZS12MQYJAAABAAAAAAAAAAEAAAAA" +
            "AAAAAQAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAABQBzYXNoYQETAHJlc2lk" +
            "ZW50OnJvYm90OjAwMDECAAAAAAAAAAABttQBAAAAAAAYAAAAAAAAABIAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA6AMAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAQAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAABAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAQAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAEAAAAAAAAA";

        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "working service cell charges fixed placement and link costs",
                PlacementAndLinkCostsAreExact);
            harness.Run(
                "city buildings move freely before link and reject occupied pads",
                PreLinkPlacementRulesAreAtomic);
            harness.Run(
                "city service link permanently locks the authored layout",
                LinkLocksLayout);
            harness.Run(
                "city service staffing is typed and composition neutral",
                StaffingIsTypedAndCompositionNeutral);
            harness.Run(
                "service sled advances exactly once through each authored stage",
                DeliveryStagesAndValueAreExact);
            harness.Run(
                "survival pressure advances throughout city construction",
                SurvivalClockContinuesDuringConstruction);
            harness.Run(
                "read model exposes the authored service-cell objective chain",
                ReadModelObjectiveChainIsExact);
            harness.Run(
                "working service cell checkpoints round trip through canonical v7",
                CanonicalV7RoundTripsAtEveryCheckpoint);
            harness.Run(
                "delivered service cell keeps reusable stepping allocation free",
                DeliveredCellStepIntoRemainsAllocationFree);
            harness.Run(
                "canonical v3 city states migrate deterministically to v7",
                LegacyV3MigrationIsDeterministic);
            harness.Run(
                "forged city construction states fail closed",
                ForgedConstructionStatesFailClosed);
            harness.Run(
                "legacy activation retains its public compatibility behavior",
                LegacyActivationSeedsCompletedCellWithoutCost);
            harness.Run(
                "legacy activation rejects non-pristine service cells",
                LegacyActivationRejectsNonPristineCell);
        }

        private static void PlacementAndLinkCostsAreExact()
        {
            var driver = new CoreTestDriver(ColonyComposition.Mixed, 2301);
            long initialParts = driver.State.PartsUnits;

            LastBearingTickResult recycler = driver.Apply(sequence =>
                new PlaceCityBuildingCommand(
                    sequence,
                    CityBuildingKind.Recycler,
                    0,
                    1));
            TestHarness.Equal(
                initialParts - LastBearingBalanceV1.RecyclerPlacementPartsUnits,
                driver.State.PartsUnits,
                "recycler cost");
            TestHarness.True(
                recycler.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.CityBuildingPlaced),
                "recycler placement event");

            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.MachineShop,
                1,
                2));
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.EmergencyStorage,
                2,
                3));
            TestHarness.Equal(
                initialParts - 6,
                driver.State.PartsUnits,
                "three placement costs");

            LastBearingTickResult link = driver.Apply(sequence =>
                new ConnectCityServiceLinkCommand(sequence));
            TestHarness.Equal(
                initialParts - 7,
                driver.State.PartsUnits,
                "placement plus link costs");
            TestHarness.True(
                link.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.CityServiceLinkConnected),
                "link event");
        }

        private static void PreLinkPlacementRulesAreAtomic()
        {
            var driver = new CoreTestDriver(ColonyComposition.HumanOnly, 2302);
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.Recycler,
                0,
                0));
            long afterFirstPlacement = driver.State.PartsUnits;
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.Recycler,
                4,
                3));
            TestHarness.Equal(
                afterFirstPlacement,
                driver.State.PartsUnits,
                "pre-link move cost");
            TestHarness.Equal(4, driver.State.RecyclerPadIndex, "moved pad");
            TestHarness.Equal(
                3,
                driver.State.RecyclerQuarterTurns,
                "moved orientation");

            byte[] beforeRejected = LastBearingCanonicalCodec.Encode(
                driver.State);
            InvalidOperationException occupied =
                TestHarness.Throws<InvalidOperationException>(
                    () => driver.Apply(sequence =>
                        new PlaceCityBuildingCommand(
                            sequence,
                            CityBuildingKind.MachineShop,
                            4,
                            0)),
                    "occupied pad accepted");
            TestHarness.Equal(
                "LAST_BEARING_CITY_PAD_OCCUPIED",
                occupied.Message,
                "occupied pad code");
            TestHarness.True(
                beforeRejected.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(driver.State)),
                "occupied pad mutated state");

            TestHarness.Throws<ArgumentOutOfRangeException>(
                () => new PlaceCityBuildingCommand(
                    driver.State.NextCommandSequence,
                    CityBuildingKind.MachineShop,
                    LastBearingState.CityConstructionPadCount,
                    0),
                "out-of-range pad accepted");
            TestHarness.Throws<ArgumentOutOfRangeException>(
                () => new PlaceCityBuildingCommand(
                    driver.State.NextCommandSequence,
                    CityBuildingKind.MachineShop,
                    1,
                    4),
                "non-quarter orientation accepted");
        }

        private static void LinkLocksLayout()
        {
            var driver = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                2303);
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.Recycler,
                0,
                0));
            byte[] incomplete = LastBearingCanonicalCodec.Encode(driver.State);
            InvalidOperationException early =
                TestHarness.Throws<InvalidOperationException>(
                    () => driver.Apply(sequence =>
                        new ConnectCityServiceLinkCommand(sequence)),
                    "incomplete layout linked");
            TestHarness.Equal(
                "LAST_BEARING_CITY_SERVICE_BUILDINGS_REQUIRED",
                early.Message,
                "incomplete link code");
            TestHarness.True(
                incomplete.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(driver.State)),
                "incomplete link mutated state");
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.MachineShop,
                1,
                1));
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.EmergencyStorage,
                2,
                2));
            driver.Apply(sequence =>
                new ConnectCityServiceLinkCommand(sequence));
            byte[] linked = LastBearingCanonicalCodec.Encode(driver.State);
            InvalidOperationException locked =
                TestHarness.Throws<InvalidOperationException>(
                    () => driver.Apply(sequence =>
                        new PlaceCityBuildingCommand(
                            sequence,
                            CityBuildingKind.Recycler,
                            3,
                            0)),
                    "linked layout moved");
            TestHarness.Equal(
                "LAST_BEARING_CITY_BUILDINGS_LOCKED",
                locked.Message,
                "layout lock code");
            TestHarness.True(
                linked.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(driver.State)),
                "locked layout mutated");

            LastBearingTickResult replay = driver.Apply(sequence =>
                new ConnectCityServiceLinkCommand(sequence));
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "link replay event");
        }

        private static void StaffingIsTypedAndCompositionNeutral()
        {
            CoreTestDriver mixed = BuildLinkedCell(
                ColonyComposition.Mixed,
                2304);
            InvalidOperationException missing =
                TestHarness.Throws<InvalidOperationException>(
                    () => mixed.Apply(sequence =>
                        new AssignCityServiceResidentCommand(
                            sequence,
                            "resident:not-in-roster")),
                    "foreign service resident accepted");
            TestHarness.Equal(
                "LAST_BEARING_CITY_SERVICE_RESIDENT_NOT_IN_ROSTER",
                missing.Message,
                "foreign resident code");

            LastBearingState human = BuildDeliveredCell(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                2305).State;
            LastBearingState robot = BuildDeliveredCell(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId,
                2305).State;
            LastBearingState mixedHuman = BuildDeliveredCell(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                2305).State;
            string expected =
                LastBearingCanonicalCodec.ComputeMechanicalSha256(human);
            TestHarness.Equal(
                expected,
                LastBearingCanonicalCodec.ComputeMechanicalSha256(robot),
                "robot service-cell mechanics");
            TestHarness.Equal(
                expected,
                LastBearingCanonicalCodec.ComputeMechanicalSha256(mixedHuman),
                "mixed service-cell mechanics");
        }

        private static void DeliveryStagesAndValueAreExact()
        {
            CoreTestDriver driver = BuildLinkedCell(
                ColonyComposition.HumanOnly,
                2306);
            driver.Apply(sequence => new AssignCityServiceResidentCommand(
                sequence,
                ResidentRoster.HumanResidentId));
            long beforeDelivery = driver.State.PartsUnits;

            byte[] beforePrematureAdvance =
                LastBearingCanonicalCodec.Encode(driver.State);
            InvalidOperationException prematureAdvance =
                TestHarness.Throws<InvalidOperationException>(
                    () => driver.Apply(sequence =>
                        new AdvanceCityServiceSledCommand(
                            sequence,
                            CityDeliveryStage.InTransit)),
                    "second sled edge accepted before the first");
            TestHarness.Equal(
                "LAST_BEARING_CITY_SERVICE_SLED_STAGE_PREMATURE",
                prematureAdvance.Message,
                "premature sled edge code");
            TestHarness.True(
                beforePrematureAdvance.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(driver.State)),
                "premature sled edge mutated state");

            LastBearingTickResult firstAdvance = driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.AtRecycler));
            TestHarness.Equal(
                CityDeliveryStage.InTransit,
                driver.State.CityDeliveryStage,
                "first sled stage");
            TestHarness.Equal(
                beforeDelivery,
                driver.State.PartsUnits,
                "first sled value");
            TestHarness.True(
                !driver.State.SliceInfrastructureActive,
                "infrastructure activated before delivery");
            TestHarness.True(
                firstAdvance.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.CityServiceSledAdvanced),
                "first sled edge event");

            LastBearingTickResult firstReplay = driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.AtRecycler));
            TestHarness.Equal(
                CityDeliveryStage.InTransit,
                driver.State.CityDeliveryStage,
                "first sled retry stage");
            TestHarness.Equal(
                beforeDelivery,
                driver.State.PartsUnits,
                "first sled retry duplicated value");
            TestHarness.True(
                firstReplay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "first sled retry replay event");
            TestHarness.True(
                !firstReplay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.CityServiceSledAdvanced
                    || item.Kind
                        == LastBearingEventKind.CityServiceBatchDelivered),
                "first sled retry emitted delivery effects");

            InvalidOperationException earlyPreparation =
                TestHarness.Throws<InvalidOperationException>(
                    () => driver.Apply(sequence =>
                        new SelectPreparationCommand(
                            sequence,
                            PreparationChoice.WorkshopPush,
                            VehicleModule.WinchAssembly)),
                    "garage preparation opened before delivery");
            TestHarness.Equal(
                "LAST_BEARING_INFRASTRUCTURE_REQUIRED",
                earlyPreparation.Message,
                "early preparation code");

            driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.InTransit));
            TestHarness.Equal(
                CityDeliveryStage.DeliveredToWorkshop,
                driver.State.CityDeliveryStage,
                "second sled stage");
            TestHarness.Equal(1, driver.State.CityDeliveryCount, "batch count");
            TestHarness.Equal(
                beforeDelivery
                    + LastBearingBalanceV1.CityServiceDeliveryPartsUnits,
                driver.State.PartsUnits,
                "delivered value");
            TestHarness.True(
                driver.State.SliceInfrastructureActive,
                "infrastructure not activated by delivery");

            long deliveredParts = driver.State.PartsUnits;
            LastBearingTickResult secondReplay = driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.InTransit));
            TestHarness.Equal(
                deliveredParts,
                driver.State.PartsUnits,
                "delivery replay duplicated value");
            TestHarness.True(
                secondReplay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "second sled retry replay event");
            TestHarness.True(
                !secondReplay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.CityServiceSledAdvanced
                    || item.Kind
                        == LastBearingEventKind.CityServiceBatchDelivered),
                "second sled retry emitted delivery effects");
            TestHarness.Throws<ArgumentOutOfRangeException>(
                () => new AdvanceCityServiceSledCommand(
                    1,
                    CityDeliveryStage.DeliveredToWorkshop),
                "delivered stage accepted as a source edge");
            driver.Apply(sequence => new SelectPreparationCommand(
                sequence,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly));
        }

        private static void SurvivalClockContinuesDuringConstruction()
        {
            var driver = new CoreTestDriver(ColonyComposition.HumanOnly, 2307);
            long startingWater = driver.State.WaterMilli;
            BuildDeliveredCell(
                driver,
                ResidentRoster.HumanResidentId);
            TestHarness.Equal(7L, driver.State.SettlementTick, "settlement ticks");
            TestHarness.Equal(
                startingWater
                    + (7
                        * LastBearingBalanceV1
                            .FailingWaterRateMilliPerSettlementTick),
                driver.State.WaterMilli,
                "construction survival pressure");
        }

        private static void CanonicalV7RoundTripsAtEveryCheckpoint()
        {
            var driver = new CoreTestDriver(ColonyComposition.Mixed, 2308);
            AssertV7RoundTrip(driver.State, "initial");
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.Recycler,
                4,
                3));
            AssertV7RoundTrip(driver.State, "recycler");
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.MachineShop,
                1,
                2));
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.EmergencyStorage,
                2,
                1));
            AssertV7RoundTrip(driver.State, "placed");
            driver.Apply(sequence =>
                new ConnectCityServiceLinkCommand(sequence));
            AssertV7RoundTrip(driver.State, "linked");
            driver.Apply(sequence => new AssignCityServiceResidentCommand(
                sequence,
                ResidentRoster.RobotResidentId));
            AssertV7RoundTrip(driver.State, "staffed");
            driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.AtRecycler));
            AssertV7RoundTrip(driver.State, "in-transit");
            driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.InTransit));
            AssertV7RoundTrip(driver.State, "delivered");
        }

        private static void ReadModelObjectiveChainIsExact()
        {
            var driver = new CoreTestDriver(ColonyComposition.HumanOnly, 2314);
            TestHarness.Equal(
                "place-city-recycler",
                driver.View.NextObjective,
                "initial objective");
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.Recycler,
                0,
                0));
            TestHarness.Equal(
                "place-city-machine-shop",
                driver.View.NextObjective,
                "shop objective");
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.MachineShop,
                1,
                0));
            TestHarness.Equal(
                "place-city-emergency-storage",
                driver.View.NextObjective,
                "storage objective");
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.EmergencyStorage,
                2,
                0));
            TestHarness.Equal(
                "connect-city-service-link",
                driver.View.NextObjective,
                "link objective");
            driver.Apply(sequence =>
                new ConnectCityServiceLinkCommand(sequence));
            TestHarness.Equal(
                "staff-city-service-cell",
                driver.View.NextObjective,
                "staff objective");
            driver.Apply(sequence => new AssignCityServiceResidentCommand(
                sequence,
                ResidentRoster.HumanResidentId));
            TestHarness.Equal(
                "advance-city-service-sled",
                driver.View.NextObjective,
                "first sled objective");
            driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.AtRecycler));
            TestHarness.Equal(
                "advance-city-service-sled",
                driver.View.NextObjective,
                "second sled objective");
            driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.InTransit));
            TestHarness.Equal(
                "assign-expedition-resident",
                driver.View.NextObjective,
                "garage handoff objective");
        }

        private static void LegacyV3MigrationIsDeterministic()
        {
            LastBearingState inactive =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2309);
            byte[] generatedInactiveV3 =
                LastBearingCanonicalCodec.EncodeLegacyV3ForMigrationTests(
                    inactive);
            byte[] inactiveV3 =
                Convert.FromBase64String(ReleasedInactiveV3Base64);
            TestHarness.True(
                inactiveV3.SequenceEqual(generatedInactiveV3),
                "released inactive v3 bytes drifted");
            TestHarness.Equal((byte)3, inactiveV3[8], "inactive v3 marker");
            LastBearingState migratedInactive = Decode(inactiveV3, "inactive");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                migratedInactive.SchemaVersion,
                "inactive schema migration");
            TestHarness.Equal(
                LastBearingBalanceV1.Revision,
                migratedInactive.BalanceRevision,
                "inactive balance migration");
            TestHarness.Equal(
                inactive.PartsUnits,
                migratedInactive.PartsUnits,
                "inactive migration parts");
            TestHarness.Equal(
                LastBearingState.UnplacedCityPadIndex,
                migratedInactive.RecyclerPadIndex,
                "inactive placement migration");
            TestHarness.True(
                inactiveV3.SequenceEqual(
                    LastBearingCanonicalCodec.EncodeLegacyV3ForMigrationTests(
                        migratedInactive)),
                "inactive v3 canonicality");

            var activeDriver = new CoreTestDriver(
                ColonyComposition.RobotOnly,
                2310);
            activeDriver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            byte[] generatedActiveV3 =
                LastBearingCanonicalCodec.EncodeLegacyV3ForMigrationTests(
                    activeDriver.State);
            byte[] activeV3 =
                Convert.FromBase64String(ReleasedActiveV3Base64);
            TestHarness.True(
                activeV3.SequenceEqual(generatedActiveV3),
                "released active v3 bytes drifted");
            LastBearingState first = Decode(activeV3, "active first");
            LastBearingState second = Decode(activeV3, "active second");
            TestHarness.Equal(0, first.RecyclerPadIndex, "migrated recycler pad");
            TestHarness.Equal(1, first.MachineShopPadIndex, "migrated shop pad");
            TestHarness.Equal(
                2,
                first.EmergencyStoragePadIndex,
                "migrated storage pad");
            TestHarness.Equal(
                ResidentRoster.RobotResidentId,
                first.CityServiceResidentId,
                "migrated service resident");
            TestHarness.Equal(
                activeDriver.State.PartsUnits,
                first.PartsUnits,
                "active migration parts");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(first),
                LastBearingCanonicalCodec.ComputeSha256(second),
                "repeat migration hash");
            TestHarness.True(
                activeV3.SequenceEqual(
                    LastBearingCanonicalCodec.EncodeLegacyV3ForMigrationTests(
                        first)),
                "active v3 canonicality");
        }

        private static void DeliveredCellStepIntoRemainsAllocationFree()
        {
            LastBearingState state = BuildDeliveredCell(
                ColonyComposition.Mixed,
                ResidentRoster.RobotResidentId,
                2313).State;
            var kernel = new LastBearingKernel();
            var buffer = new LastBearingStepBuffer();
            for (var index = 0; index < 32; index++)
            {
                kernel.StepInto(
                    state,
                    Array.Empty<LastBearingCommand>(),
                    buffer);
                state = buffer.State!;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 1000; index++)
            {
                kernel.StepInto(
                    state,
                    Array.Empty<LastBearingCommand>(),
                    buffer);
                state = buffer.State!;
            }

            long allocated = checked(
                GC.GetAllocatedBytesForCurrentThread() - before);
            TestHarness.Equal(
                0L,
                allocated,
                "delivered reusable step allocation bytes");
        }

        private static void ForgedConstructionStatesFailClosed()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2311);
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    RecyclerPadIndex = 0,
                    MachineShopPadIndex = 0,
                }.BuildUnchecked(),
                "LAST_BEARING_CITY_PAD_DUPLICATE",
                "duplicate pads");
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    CityServiceLinkConnected = true,
                }.BuildUnchecked(),
                "LAST_BEARING_CITY_SERVICE_LINK_BUILDINGS_INVALID",
                "link without buildings");
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    CityServiceResidentId = "resident:not-in-roster",
                }.BuildUnchecked(),
                "LAST_BEARING_CITY_SERVICE_RESIDENT_NOT_IN_ROSTER",
                "foreign service resident");
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    SliceInfrastructureActive = true,
                }.BuildUnchecked(),
                "LAST_BEARING_CITY_SERVICE_UNLINKED_STATE_INVALID",
                "active without delivery");
        }

        private static void LegacyActivationSeedsCompletedCellWithoutCost()
        {
            var driver = new CoreTestDriver(ColonyComposition.Mixed, 2312);
            long parts = driver.State.PartsUnits;
            driver.Apply(sequence =>
                new AssignResidentCommand(
                    sequence,
                    ResidentRoster.RobotResidentId));
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            TestHarness.Equal(parts, driver.State.PartsUnits, "legacy cost");
            TestHarness.Equal(0, driver.State.RecyclerPadIndex, "legacy recycler");
            TestHarness.Equal(1, driver.State.MachineShopPadIndex, "legacy shop");
            TestHarness.Equal(
                2,
                driver.State.EmergencyStoragePadIndex,
                "legacy storage");
            TestHarness.Equal(
                ResidentRoster.RobotResidentId,
                driver.State.CityServiceResidentId,
                "legacy staff mapping");
            TestHarness.Equal(1, driver.State.CityDeliveryCount, "legacy batch");
            TestHarness.True(
                driver.State.SliceInfrastructureActive,
                "legacy infrastructure inactive");
        }

        private static void LegacyActivationRejectsNonPristineCell()
        {
            var partial = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                2315);
            partial.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.Recycler,
                4,
                3));
            AssertLegacyActivationRejected(partial, "partial placement");

            CoreTestDriver linked = BuildLinkedCell(
                ColonyComposition.RobotOnly,
                2316);
            AssertLegacyActivationRejected(linked, "linked cell");

            CoreTestDriver inTransit = BuildLinkedCell(
                ColonyComposition.Mixed,
                2317);
            inTransit.Apply(sequence =>
                new AssignCityServiceResidentCommand(
                    sequence,
                    ResidentRoster.RobotResidentId));
            inTransit.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.AtRecycler));
            AssertLegacyActivationRejected(inTransit, "in-transit cell");
        }

        private static void AssertLegacyActivationRejected(
            CoreTestDriver driver,
            string label)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(driver.State);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => driver.Apply(sequence =>
                        new ActivateSliceInfrastructureCommand(sequence)),
                    label + " accepted legacy activation");
            TestHarness.Equal(
                "LAST_BEARING_LEGACY_ACTIVATION_CITY_STATE_CONFLICT",
                error.Message,
                label + " rejection code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(driver.State)),
                label + " rejection mutated state");
        }

        private static CoreTestDriver BuildLinkedCell(
            ColonyComposition composition,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            PlaceAllBuildings(driver);
            driver.Apply(sequence =>
                new ConnectCityServiceLinkCommand(sequence));
            return driver;
        }

        private static CoreTestDriver BuildDeliveredCell(
            ColonyComposition composition,
            string residentId,
            int worldSeed)
        {
            CoreTestDriver driver = BuildLinkedCell(composition, worldSeed);
            BuildDeliveredCell(driver, residentId);
            return driver;
        }

        private static void BuildDeliveredCell(
            CoreTestDriver driver,
            string residentId)
        {
            if (!driver.State.CityServiceLinkConnected)
            {
                PlaceAllBuildings(driver);
                driver.Apply(sequence =>
                    new ConnectCityServiceLinkCommand(sequence));
            }

            driver.Apply(sequence => new AssignCityServiceResidentCommand(
                sequence,
                residentId));
            driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.AtRecycler));
            driver.Apply(sequence =>
                new AdvanceCityServiceSledCommand(
                    sequence,
                    CityDeliveryStage.InTransit));
        }

        private static void PlaceAllBuildings(CoreTestDriver driver)
        {
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.Recycler,
                0,
                0));
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.MachineShop,
                1,
                1));
            driver.Apply(sequence => new PlaceCityBuildingCommand(
                sequence,
                CityBuildingKind.EmergencyStorage,
                2,
                2));
        }

        private static void AssertV7RoundTrip(
            LastBearingState state,
            string label)
        {
            byte[] encoded = LastBearingCanonicalCodec.Encode(state);
            TestHarness.Equal((byte)7, encoded[8], label + " codec marker");
            LastBearingDecodeResult result =
                LastBearingCanonicalCodec.TryDecode(encoded);
            TestHarness.True(
                result.Succeeded && result.State != null,
                label + " decode");
            TestHarness.True(
                encoded.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(result.State!)),
                label + " canonical bytes");
        }

        private static LastBearingState Decode(byte[] bytes, string label)
        {
            LastBearingDecodeResult result =
                LastBearingCanonicalCodec.TryDecode(bytes);
            TestHarness.True(
                result.Succeeded && result.State != null,
                label + " v3 migration");
            return result.State!;
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
