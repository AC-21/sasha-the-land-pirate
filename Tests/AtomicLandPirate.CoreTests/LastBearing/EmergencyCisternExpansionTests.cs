#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class EmergencyCisternExpansionTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "Emergency Storage expansion spends two parts and preserves returned water",
                InstallConservesReturnedWaterAndResources);
            harness.Run(
                "Emergency Storage expansion rejects invalid intents atomically",
                InvalidInstallationIntentsFailAtomically);
            harness.Run(
                "expanded Emergency Cistern owns the authoritative water ceiling",
                ExpandedCapacityIsAuthoritative);
            harness.Run(
                "expanded Emergency Cistern survives schema 9 canonical round trip",
                ExpansionRoundTripsInSchemaNine);
            harness.Run(
                "forged expanded Emergency Cistern states fail closed",
                ForgedExpansionStatesFailClosed);
        }

        private static void InstallConservesReturnedWaterAndResources()
        {
            CoreTestDriver ready = ReachExpansionReady(3001);
            var driver = new CoreTestDriver(
                new LastBearingStateBuilder(ready.State)
                {
                    PartsUnits = checked(
                        LastBearingBalanceV1
                            .EmergencyCisternExpansionPartsUnits
                        + LastBearingBalanceV1
                            .MinimumPostReturnPartsUnits),
                }.Build());
            driver.Apply(sequence => new SetPauseCommand(sequence, true));
            long partsBefore = driver.State.PartsUnits;
            long waterBefore = driver.State.WaterMilli;
            LiquidCargoKind liquidKindBefore = driver.State.LiquidCargoKind;
            long liquidQuantityBefore = driver.State.LiquidCargoQuantityMilli;
            LiquidCargoCustody liquidCustodyBefore =
                driver.State.LiquidCargoCustody;

            TestHarness.Equal(
                LastBearingBalanceV1.WaterCapacityMilli,
                driver.View.WaterCapacityMilli,
                "pre-install capacity");
            TestHarness.Equal(
                LastBearingBalanceV1.EmergencyCisternExpansionPartsUnits,
                driver.View.CityImprovementPartsCostUnits,
                "presented expansion cost");
            TestHarness.True(
                driver.View.IsCityImprovementInstallationAvailable,
                "expansion installation was not available");
            TestHarness.Equal(
                "expand-emergency-cistern",
                driver.View.NextObjective,
                "expansion objective");

            LastBearingTickResult installed = Install(driver);

            TestHarness.Equal(
                CityImprovementKind.ExpandedEmergencyCistern,
                driver.State.InstalledCityImprovement,
                "installed improvement");
            TestHarness.Equal(
                partsBefore
                    - LastBearingBalanceV1
                        .EmergencyCisternExpansionPartsUnits,
                driver.State.PartsUnits,
                "installation parts debit");
            TestHarness.Equal(
                LastBearingBalanceV1.MinimumPostReturnPartsUnits,
                driver.State.PartsUnits,
                "retained parts reserve");
            TestHarness.Equal(
                NextCityDecision.None,
                driver.State.NextCityDecision,
                "consumed city decision");
            TestHarness.Equal(
                checked(
                    LastBearingBalanceV1.WaterCapacityMilli
                    + LastBearingBalanceV1
                        .EmergencyCisternExpansionCapacityMilli),
                driver.View.WaterCapacityMilli,
                "expanded water capacity");
            TestHarness.Equal(waterBefore, driver.State.WaterMilli, "water");
            TestHarness.Equal(
                liquidKindBefore,
                driver.State.LiquidCargoKind,
                "liquid kind");
            TestHarness.Equal(
                liquidQuantityBefore,
                driver.State.LiquidCargoQuantityMilli,
                "liquid quantity");
            TestHarness.Equal(
                liquidCustodyBefore,
                driver.State.LiquidCargoCustody,
                "liquid custody");
            TestHarness.Equal(
                LastBearingEventKind.CityResourcesCommitted,
                installed.DomainEvents[0].Kind,
                "installation event order 0");
            TestHarness.Equal(
                LastBearingEventKind.CityImprovementInstalled,
                installed.DomainEvents[1].Kind,
                "installation event order 1");
            TestHarness.True(
                installed.DomainEvents.All(item =>
                    item.Kind != LastBearingEventKind.LiquidCargoTransferred),
                "installation transferred or re-credited liquid");

            long replayWaterBefore = driver.State.WaterMilli;
            LiquidCargoKind replayLiquidKindBefore =
                driver.State.LiquidCargoKind;
            long replayLiquidQuantityBefore =
                driver.State.LiquidCargoQuantityMilli;
            LiquidCargoCustody replayLiquidCustodyBefore =
                driver.State.LiquidCargoCustody;
            LastBearingTickResult replay = Install(driver);
            TestHarness.Equal(
                partsBefore
                    - LastBearingBalanceV1
                        .EmergencyCisternExpansionPartsUnits,
                driver.State.PartsUnits,
                "replay parts debit");
            TestHarness.Equal(
                replayWaterBefore,
                driver.State.WaterMilli,
                "replay water");
            TestHarness.Equal(
                replayLiquidKindBefore,
                driver.State.LiquidCargoKind,
                "replay liquid kind");
            TestHarness.Equal(
                replayLiquidQuantityBefore,
                driver.State.LiquidCargoQuantityMilli,
                "replay liquid quantity");
            TestHarness.Equal(
                replayLiquidCustodyBefore,
                driver.State.LiquidCargoCustody,
                "replay liquid custody");
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "replay audit event");
            TestHarness.True(
                replay.DomainEvents.All(item =>
                    item.Kind != LastBearingEventKind.CityResourcesCommitted
                    && item.Kind
                        != LastBearingEventKind.CityImprovementInstalled
                    && item.Kind
                        != LastBearingEventKind.LiquidCargoTransferred),
                "replay repeated installation effects");
        }

        private static void InvalidInstallationIntentsFailAtomically()
        {
            CoreTestDriver ready = ReachExpansionReady(3002);
            AssertRejectedWithoutMutation(
                ready.State,
                new InstallCityImprovementCommand(
                    ready.State.NextCommandSequence,
                    NextCityDecision.RefurbishAuxiliaryPump,
                    LastBearingState.EmergencyStorageExpansionSocketId,
                    LastBearingState
                        .EmergencyStorageExpansionOrientationQuarterTurns),
                "LAST_BEARING_CITY_IMPROVEMENT_DECISION_MISMATCH",
                "wrong decision");
            AssertRejectedWithoutMutation(
                ready.State,
                new InstallCityImprovementCommand(
                    ready.State.NextCommandSequence,
                    NextCityDecision.ExpandEmergencyCistern,
                    "city:last-bearing:socket:forged",
                    LastBearingState
                        .EmergencyStorageExpansionOrientationQuarterTurns),
                "LAST_BEARING_CITY_IMPROVEMENT_SOCKET_INVALID",
                "wrong socket");
            AssertRejectedWithoutMutation(
                ready.State,
                new InstallCityImprovementCommand(
                    ready.State.NextCommandSequence,
                    NextCityDecision.ExpandEmergencyCistern,
                    LastBearingState.EmergencyStorageExpansionSocketId,
                    1),
                "LAST_BEARING_CITY_IMPROVEMENT_ORIENTATION_INVALID",
                "wrong orientation");

            LastBearingState insufficient =
                new LastBearingStateBuilder(ready.State)
                {
                    PartsUnits =
                        LastBearingBalanceV1
                            .EmergencyCisternExpansionPartsUnits
                        + LastBearingBalanceV1
                            .MinimumPostReturnPartsUnits
                        - 1,
                }.Build();
            AssertRejectedWithoutMutation(
                insufficient,
                ExactCommand(insufficient.NextCommandSequence),
                "LAST_BEARING_CITY_IMPROVEMENT_PARTS_INSUFFICIENT",
                "insufficient reserve");

            LastBearingState stale =
                new LastBearingStateBuilder(ready.State)
                {
                    NextCityDecision = NextCityDecision.None,
                }.Build();
            AssertRejectedWithoutMutation(
                stale,
                ExactCommand(stale.NextCommandSequence),
                "LAST_BEARING_CITY_IMPROVEMENT_DECISION_MISMATCH",
                "stale decision");

            LastBearingState early = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                3003);
            early = new LastBearingStateBuilder(early)
            {
                NextCityDecision = NextCityDecision.ExpandEmergencyCistern,
            }.Build();
            AssertRejectedWithoutMutation(
                early,
                ExactCommand(early.NextCommandSequence),
                "LAST_BEARING_CITY_IMPROVEMENT_PHASE_INVALID",
                "early installation");

            LastBearingState wrongBranch =
                CityImprovementTests.CreateInstalledStateForSaveTests();
            wrongBranch = new LastBearingStateBuilder(wrongBranch)
            {
                InstalledCityImprovement = CityImprovementKind.None,
                HeavyCargoCustody = HeavyCargoCustody.Settlement,
                TowSlotsUsed = 1,
                NextCityDecision = NextCityDecision.ExpandEmergencyCistern,
            }.Build();
            AssertRejectedWithoutMutation(
                wrongBranch,
                ExactCommand(wrongBranch.NextCommandSequence),
                "LAST_BEARING_CITY_IMPROVEMENT_CARGO_INVALID",
                "wrong vehicle branch");

            LastBearingState forgedFuel =
                new LastBearingStateBuilder(ready.State)
                {
                    LiquidCargoKind = LiquidCargoKind.Fuel,
                    LiquidCargoQuantityMilli =
                        LastBearingBalanceV1.TankFuelReturnMilli,
                }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(forgedFuel)
                    .IsCityImprovementInstallationAvailable,
                "forged Workshop Push fuel exposed installation");
            AssertRejectedWithoutMutation(
                forgedFuel,
                ExactCommand(forgedFuel.NextCommandSequence),
                "LAST_BEARING_CITY_IMPROVEMENT_CARGO_INVALID",
                "forged Workshop Push fuel");
        }

        private static void ExpandedCapacityIsAuthoritative()
        {
            CoreTestDriver driver = ReachExpansionReady(3004);
            Install(driver);
            long expandedCapacity = checked(
                LastBearingBalanceV1.WaterCapacityMilli
                + LastBearingBalanceV1
                    .EmergencyCisternExpansionCapacityMilli);
            TestHarness.Equal(
                expandedCapacity,
                driver.View.WaterCapacityMilli,
                "read-model capacity");

            LastBearingState nearCapacity =
                new LastBearingStateBuilder(driver.State)
                {
                    PauseCause = PauseCause.None,
                    WaterMilli = expandedCapacity - 5,
                }.Build();
            var ticking = new CoreTestDriver(nearCapacity);
            ticking.Advance(1);
            TestHarness.Equal(
                expandedCapacity,
                ticking.State.WaterMilli,
                "settlement water clamp");

            AssertInvariantRejected(
                new LastBearingStateBuilder(driver.State)
                {
                    WaterMilli = expandedCapacity + 1,
                }.BuildUnchecked(),
                "LAST_BEARING_WATER_OUT_OF_RANGE",
                "water beyond expanded capacity");

            LastBearingState baseState =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    3005);
            TestHarness.Equal(
                LastBearingBalanceV1.WaterCapacityMilli,
                LastBearingReadModel.FromState(baseState).WaterCapacityMilli,
                "base read-model capacity");
            AssertInvariantRejected(
                new LastBearingStateBuilder(baseState)
                {
                    WaterMilli =
                        LastBearingBalanceV1.WaterCapacityMilli + 1,
                }.BuildUnchecked(),
                "LAST_BEARING_WATER_OUT_OF_RANGE",
                "base water beyond capacity");

            LastBearingState pump =
                CityImprovementTests.CreateInstalledStateForSaveTests();
            TestHarness.Equal(
                LastBearingBalanceV1.WaterCapacityMilli,
                LastBearingReadModel.FromState(pump).WaterCapacityMilli,
                "auxiliary pump capacity changed");
        }

        private static void ExpansionRoundTripsInSchemaNine()
        {
            LastBearingState installed =
                CreateExpandedStateForSaveTests();
            TestHarness.Equal(
                9,
                installed.SchemaVersion,
                "installed schema version");
            byte[] canonical = LastBearingCanonicalCodec.Encode(installed);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(canonical);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "expanded cistern decode");
            TestHarness.True(
                canonical.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State!)),
                "expanded cistern canonical bytes");
            TestHarness.Equal(
                CityImprovementKind.ExpandedEmergencyCistern,
                decoded.State!.InstalledCityImprovement,
                "expanded cistern enum");
            TestHarness.Equal(
                checked(
                    LastBearingBalanceV1.WaterCapacityMilli
                    + LastBearingBalanceV1
                        .EmergencyCisternExpansionCapacityMilli),
                LastBearingReadModel.FromState(decoded.State)
                    .WaterCapacityMilli,
                "restored capacity");

            LastBearingState oldSchemaNine =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    3006);
            byte[] oldCanonical =
                LastBearingCanonicalCodec.Encode(oldSchemaNine);
            LastBearingDecodeResult oldDecoded =
                LastBearingCanonicalCodec.TryDecode(oldCanonical);
            TestHarness.True(
                oldDecoded.Succeeded && oldDecoded.State != null,
                "old schema 9 decode");
            TestHarness.Equal(
                CityImprovementKind.None,
                oldDecoded.State!.InstalledCityImprovement,
                "old schema 9 improvement");
            TestHarness.True(
                oldCanonical.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(oldDecoded.State)),
                "old schema 9 canonical bytes");
        }

        private static void ForgedExpansionStatesFailClosed()
        {
            LastBearingState installed =
                CreateExpandedStateForSaveTests();
            AssertInvariantRejected(
                new LastBearingStateBuilder(installed)
                {
                    NextCityDecision =
                        NextCityDecision.ExpandEmergencyCistern,
                }.BuildUnchecked(),
                "LAST_BEARING_CITY_IMPROVEMENT_STATE_INVALID",
                "installed decision retained");
            AssertInvariantRejected(
                new LastBearingStateBuilder(installed)
                {
                    RouteActionUsed = false,
                }.BuildUnchecked(),
                "LAST_BEARING_ARRIVAL_REQUIRES_ROUTE_ACTION",
                "missing Wreck Line lineage");
            AssertInvariantRejected(
                new LastBearingStateBuilder(installed)
                {
                    InstalledCityImprovement = (CityImprovementKind)99,
                }.BuildUnchecked(),
                "LAST_BEARING_INSTALLED_CITY_IMPROVEMENT_INVALID",
                "unknown improvement enum");
            TestHarness.Throws<ArgumentOutOfRangeException>(
                () => LastBearingBalanceV1.EffectiveWaterCapacityMilli(
                    (CityImprovementKind)99),
                "unknown improvement capacity was accepted");
        }

        internal static LastBearingState CreateExpandedStateForSaveTests()
        {
            CoreTestDriver driver = ReachExpansionReady(3010);
            Install(driver);
            return driver.State;
        }

        private static CoreTestDriver ReachExpansionReady(int worldSeed)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.Mixed,
                worldSeed);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:cistern-expansion:" + worldSeed;
            string fingerprint = "fp:cistern-expansion:" + worldSeed;
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
                new ChooseLiquidReturnCommand(
                    sequence,
                    LiquidCargoKind.Water));
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
            driver.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));

            TestHarness.Equal(
                NextCityDecision.ExpandEmergencyCistern,
                driver.State.NextCityDecision,
                "expansion decision");
            TestHarness.Equal(
                LiquidCargoKind.Water,
                driver.State.LiquidCargoKind,
                "returned liquid kind");
            TestHarness.Equal(
                LastBearingBalanceV1.TankWaterReturnMilli,
                driver.State.LiquidCargoQuantityMilli,
                "returned liquid quantity");
            TestHarness.Equal(
                LiquidCargoCustody.Settlement,
                driver.State.LiquidCargoCustody,
                "returned liquid custody");
            TestHarness.True(
                driver.View.IsCityImprovementInstallationAvailable,
                "expansion availability");
            return driver;
        }

        private static LastBearingTickResult Install(CoreTestDriver driver)
        {
            return driver.Apply(sequence => ExactCommand(sequence));
        }

        private static InstallCityImprovementCommand ExactCommand(
            long sequence)
        {
            return new InstallCityImprovementCommand(
                sequence,
                NextCityDecision.ExpandEmergencyCistern,
                LastBearingState.EmergencyStorageExpansionSocketId,
                LastBearingState
                    .EmergencyStorageExpansionOrientationQuarterTurns);
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
                before.SequenceEqual(LastBearingCanonicalCodec.Encode(state)),
                label + " mutated state");
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
