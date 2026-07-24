#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class DepotAccessRestorationTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "cooperative tank return records shared access without a false decision",
                CooperativeReturnNeedsNoRestoration);
            harness.Run(
                "forged SharedService does not erase the adverse decision",
                SharedServiceAloneDoesNotClearAdverseDecision);
            harness.Run(
                "fuel bond restores depot access with exact conservation",
                FuelBondRestoresAccessExactly);
            harness.Run(
                "fuel bond rejects invalid intents atomically",
                InvalidRestorationIntentsFailAtomically);
            harness.Run(
                "fuel bond retries do not duplicate value",
                RetriesDoNotDuplicateValue);
            harness.Run(
                "fuel bond preserves schema 9 save compatibility",
                ExistingSchemaRoundTripsReadyAndSettledStates);
            harness.Run(
                "fuel bond mechanics are composition invariant",
                CompositionsShareExactMechanics);
        }

        private static void CooperativeReturnNeedsNoRestoration()
        {
            CoreTestDriver driver = ReachFinalizedReturn(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                EncounterChoice.Cooperate,
                3101);

            TestHarness.Equal(
                FactionOutcomeKind.Cooperative,
                driver.State.PendingFactionOutcome,
                "cooperative outcome");
            TestHarness.Equal(
                FactionAccessPolicy.SharedService,
                driver.State.FactionAccessPolicy,
                "shared access");
            TestHarness.True(
                driver.State.RoutePermitGranted,
                "shared route access");
            TestHarness.Equal(
                NextCityDecision.None,
                driver.State.NextCityDecision,
                "cooperative decision");
            TestHarness.True(
                !driver.View.IsDepotAccessRestorationAvailable,
                "cooperative fuel bond availability");
            TestHarness.Equal(
                "route-permit-recorded",
                driver.View.NextObjective,
                "cooperative objective");
        }

        private static void FuelBondRestoresAccessExactly()
        {
            CoreTestDriver driver = ReachRestorationReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                3102);
            driver.Apply(sequence => new SetPauseCommand(sequence, true));
            long fuelBefore = driver.State.FuelUnits;
            long grievanceBefore = driver.State.FactionGrievance;
            long tollBefore = driver.State.FutureRouteTollFuelUnits;
            LiquidCargoKind liquidKindBefore = driver.State.LiquidCargoKind;
            long liquidQuantityBefore = driver.State.LiquidCargoQuantityMilli;
            LiquidCargoCustody liquidCustodyBefore =
                driver.State.LiquidCargoCustody;
            LastBearingState ready = driver.State;
            LastBearingTickResult ordinaryTick =
                new LastBearingKernel().Step(
                    ready,
                    Array.Empty<LastBearingCommand>());

            TestHarness.True(
                driver.View.IsDepotAccessRestorationAvailable,
                "fuel bond availability");
            TestHarness.Equal(
                "post-fuel-bond-at-claims-counter",
                driver.View.NextObjective,
                "fuel bond objective");

            LastBearingTickResult result = driver.Apply(
                sequence => new RestoreDepotAccessCommand(sequence));
            var expected = new LastBearingStateBuilder(ordinaryTick.State)
            {
                NextCommandSequence = checked(
                    ready.NextCommandSequence + 1),
                FuelUnits = checked(
                    ready.FuelUnits
                    - (LastBearingBalanceV1.TankFuelReturnMilli / 1000)),
                RoutePermitGranted = true,
                FactionAccessPolicy =
                    FactionAccessPolicy.PermitRequired,
                NextCityDecision = NextCityDecision.None,
            }.Build();

            TestHarness.Equal(
                fuelBefore
                    - (LastBearingBalanceV1.TankFuelReturnMilli / 1000),
                driver.State.FuelUnits,
                "fuel bond debit");
            TestHarness.True(
                driver.State.RoutePermitGranted,
                "route permit");
            TestHarness.Equal(
                FactionAccessPolicy.PermitRequired,
                driver.State.FactionAccessPolicy,
                "access policy");
            TestHarness.Equal(
                NextCityDecision.None,
                driver.State.NextCityDecision,
                "consumed decision");
            TestHarness.Equal(
                grievanceBefore,
                driver.State.FactionGrievance,
                "grievance");
            TestHarness.Equal(
                FactionAidPolicy.Withheld,
                driver.State.FactionAidPolicy,
                "withheld aid");
            TestHarness.Equal(
                FactionOutcomeKind.Adverse,
                driver.State.PendingFactionOutcome,
                "adverse outcome");
            TestHarness.Equal(
                tollBefore,
                driver.State.FutureRouteTollFuelUnits,
                "future route toll");
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
            TestHarness.True(
                LastBearingCanonicalCodec.Encode(expected).SequenceEqual(
                    LastBearingCanonicalCodec.Encode(driver.State)),
                "fuel bond changed fields outside the exact transition");
            TestHarness.Equal(
                4,
                result.DomainEvents.Count,
                "event count");
            TestHarness.Equal(
                LastBearingEventKind.CityResourcesCommitted,
                result.DomainEvents[0].Kind,
                "event order 0");
            TestHarness.Equal(
                LastBearingEventKind.DepotAccessTermsChanged,
                result.DomainEvents[1].Kind,
                "event order 1");
            TestHarness.Equal(
                LastBearingEventKind.RoutePermitGranted,
                result.DomainEvents[2].Kind,
                "event order 2");
            TestHarness.Equal(
                LastBearingEventKind.NextCityDecisionSet,
                result.DomainEvents[3].Kind,
                "event order 3");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.CityResourcesCommitted
                    && item.BeforeValue == fuelBefore
                    && item.AfterValue == driver.State.FuelUnits),
                "fuel event");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.DepotAccessTermsChanged
                    && item.BeforeValue
                        == (long)FactionAccessPolicy.Closed
                    && item.AfterValue
                        == (long)FactionAccessPolicy.PermitRequired),
                "access event");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.RoutePermitGranted
                    && item.SubjectId
                        == LastBearingState.DepotCorridorRoutePermitId),
                "permit event");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.NextCityDecisionSet
                    && item.BeforeValue
                        == (long)NextCityDecision.RestoreDepotAccess
                    && item.AfterValue == (long)NextCityDecision.None),
                "decision event");
            TestHarness.True(
                result.DomainEvents.All(item =>
                    item.Kind != LastBearingEventKind.LiquidCargoTransferred),
                "fuel bond re-transferred liquid");
            TestHarness.Equal(
                "route-permit-recorded",
                result.ReadModel.NextObjective,
                "settled objective");
        }

        private static void SharedServiceAloneDoesNotClearAdverseDecision()
        {
            const int worldSeed = 3111;
            CoreTestDriver returned = ReachReturnedPending(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                EncounterChoice.TakeBearing,
                worldSeed);
            var forged = new CoreTestDriver(
                new LastBearingStateBuilder(returned.State)
                {
                    FactionAccessPolicy =
                        FactionAccessPolicy.SharedService,
                }.Build());
            string transactionId = "tx:fuel-bond:" + worldSeed;
            string fingerprint = "fp:fuel-bond:" + worldSeed;

            forged.Apply(sequence =>
                new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    fingerprint));

            TestHarness.Equal(
                FactionOutcomeKind.Adverse,
                forged.State.PendingFactionOutcome,
                "adverse outcome");
            TestHarness.Equal(
                NextCityDecision.RestoreDepotAccess,
                forged.State.NextCityDecision,
                "adverse decision");
            TestHarness.True(
                !forged.View.IsDepotAccessRestorationAvailable,
                "forged access exposed restoration");
        }

        private static void InvalidRestorationIntentsFailAtomically()
        {
            LastBearingState ready = ReachRestorationReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                3103).State;
            AssertRejectedWithoutMutation(
                new LastBearingStateBuilder(ready)
                {
                    NextCityDecision = NextCityDecision.None,
                }.Build(),
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_NOT_ELIGIBLE",
                "stale decision");
            AssertRejectedWithoutMutation(
                new LastBearingStateBuilder(ready)
                {
                    LiquidCargoQuantityMilli =
                        LastBearingBalanceV1.TankFuelReturnMilli - 1,
                }.BuildUnchecked(),
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_NOT_ELIGIBLE",
                "wrong returned quantity");
            AssertRejectedWithoutMutation(
                new LastBearingStateBuilder(ready)
                {
                    LiquidCargoCustody = LiquidCargoCustody.Vehicle,
                }.BuildUnchecked(),
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_NOT_ELIGIBLE",
                "wrong returned custody");
            AssertRejectedWithoutMutation(
                new LastBearingStateBuilder(ready)
                {
                    FactionAccessPolicy = FactionAccessPolicy.SharedService,
                }.Build(),
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_NOT_ELIGIBLE",
                "wrong access policy");
            AssertRejectedWithoutMutation(
                new LastBearingStateBuilder(ready)
                {
                    FutureRouteTollFuelUnits = 0,
                }.Build(),
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_NOT_ELIGIBLE",
                "missing future toll");
            LastBearingState forgedMemory =
                new LastBearingStateBuilder(ready)
                {
                    FactionMemory = new FactionMemoryRecord(
                        "memory:last-bearing:take:0001",
                        "ForgedAction",
                        LastBearingState.LastBearingFactionId,
                        LastBearingBalanceV1.TakeGrievanceDelta,
                        "custody-breach",
                        ready.FactionMemory!.EncounterTick,
                        "DEPOT_ACCESS_CLOSED"),
                }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(forgedMemory)
                    .IsDepotAccessRestorationAvailable,
                "forged memory exposed restoration");
            AssertRejectedWithoutMutation(
                forgedMemory,
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_NOT_ELIGIBLE",
                "forged memory");
            LastBearingState forgedFee =
                new LastBearingStateBuilder(ready)
                {
                    DepotAccessFeePartsUnits = checked(
                        ready.DepotAccessFeePartsUnits + 1),
                }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(forgedFee)
                    .IsDepotAccessRestorationAvailable,
                "forged access fee exposed restoration");
            AssertRejectedWithoutMutation(
                forgedFee,
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_NOT_ELIGIBLE",
                "forged access fee");

            LastBearingState insufficient =
                new LastBearingStateBuilder(ready)
                {
                    FuelUnits =
                        (LastBearingBalanceV1.TankFuelReturnMilli / 1000) - 1,
                }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(insufficient)
                    .IsDepotAccessRestorationAvailable,
                "insufficient fuel exposed restoration");
            AssertRejectedWithoutMutation(
                insufficient,
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_FUEL_INSUFFICIENT",
                "insufficient fuel");

            CoreTestDriver cooperative = ReachFinalizedReturn(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                EncounterChoice.Cooperate,
                3104);
            AssertRejectedWithoutMutation(
                cooperative.State,
                "LAST_BEARING_DEPOT_ACCESS_RESTORATION_NOT_ELIGIBLE",
                "cooperative route");
        }

        private static void RetriesDoNotDuplicateValue()
        {
            CoreTestDriver driver = ReachRestorationReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                3105);
            driver.Apply(sequence => new SetPauseCommand(sequence, true));
            driver.Apply(sequence => new RestoreDepotAccessCommand(sequence));
            long fuelAfter = driver.State.FuelUnits;

            LastBearingTickResult replay = driver.Apply(
                sequence => new RestoreDepotAccessCommand(sequence));

            TestHarness.Equal(fuelAfter, driver.State.FuelUnits, "replay fuel");
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "replay audit");
            TestHarness.True(
                replay.DomainEvents.All(item =>
                    item.Kind != LastBearingEventKind.CityResourcesCommitted
                    && item.Kind
                        != LastBearingEventKind.DepotAccessTermsChanged
                    && item.Kind
                        != LastBearingEventKind.RoutePermitGranted
                    && item.Kind
                        != LastBearingEventKind.NextCityDecisionSet),
                "replay repeated settlement effects");
        }

        private static void ExistingSchemaRoundTripsReadyAndSettledStates()
        {
            LastBearingState ready = ReachRestorationReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                3106).State;
            LastBearingState settled = Apply(
                ready,
                sequence => new RestoreDepotAccessCommand(sequence)).State;

            foreach (LastBearingState state in new[] { ready, settled })
            {
                TestHarness.Equal(9, state.SchemaVersion, "schema version");
                byte[] encoded = LastBearingCanonicalCodec.Encode(state);
                LastBearingDecodeResult decoded =
                    LastBearingCanonicalCodec.TryDecode(encoded);
                TestHarness.True(
                    decoded.Succeeded && decoded.State != null,
                    "schema 9 decode");
                TestHarness.True(
                    encoded.SequenceEqual(
                        LastBearingCanonicalCodec.Encode(decoded.State!)),
                    "schema 9 canonical bytes");
            }

            LastBearingState oldState =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    3107);
            byte[] oldBytes = LastBearingCanonicalCodec.Encode(oldState);
            LastBearingDecodeResult oldDecoded =
                LastBearingCanonicalCodec.TryDecode(oldBytes);
            TestHarness.True(
                oldDecoded.Succeeded && oldDecoded.State != null,
                "old schema 9 decode");
            TestHarness.True(
                oldBytes.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(oldDecoded.State!)),
                "old schema 9 canonical bytes");
        }

        private static void CompositionsShareExactMechanics()
        {
            LastBearingState human = CreateSettledState(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                3108);
            LastBearingState robot = CreateSettledState(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId,
                3108);
            LastBearingState mixed = CreateSettledState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                3108);
            string expected =
                LastBearingCanonicalCodec.ComputeMechanicalSha256(human);
            TestHarness.Equal(
                expected,
                LastBearingCanonicalCodec.ComputeMechanicalSha256(robot),
                "robot mechanics");
            TestHarness.Equal(
                expected,
                LastBearingCanonicalCodec.ComputeMechanicalSha256(mixed),
                "mixed mechanics");
        }

        private static LastBearingState CreateSettledState(
            ColonyComposition composition,
            string residentId,
            int worldSeed)
        {
            LastBearingState ready = ReachRestorationReady(
                composition,
                residentId,
                worldSeed).State;
            return Apply(
                ready,
                sequence => new RestoreDepotAccessCommand(sequence)).State;
        }

        private static CoreTestDriver ReachRestorationReady(
            ColonyComposition composition,
            string residentId,
            int worldSeed)
        {
            CoreTestDriver driver = ReachFinalizedReturn(
                composition,
                residentId,
                EncounterChoice.TakeBearing,
                worldSeed);
            TestHarness.Equal(
                NextCityDecision.RestoreDepotAccess,
                driver.State.NextCityDecision,
                "restoration decision");
            TestHarness.Equal(
                LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                driver.State.FutureRouteTollFuelUnits,
                "matured future toll");
            TestHarness.True(
                driver.View.IsDepotAccessRestorationAvailable,
                "restoration availability");
            return driver;
        }

        private static CoreTestDriver ReachFinalizedReturn(
            ColonyComposition composition,
            string residentId,
            EncounterChoice encounterChoice,
            int worldSeed)
        {
            CoreTestDriver driver = ReachReturnedPending(
                composition,
                residentId,
                encounterChoice,
                worldSeed);
            string transactionId = "tx:fuel-bond:" + worldSeed;
            string fingerprint = "fp:fuel-bond:" + worldSeed;
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
                LiquidCargoKind.Fuel,
                driver.State.LiquidCargoKind,
                "returned fuel kind");
            TestHarness.Equal(
                LastBearingBalanceV1.TankFuelReturnMilli,
                driver.State.LiquidCargoQuantityMilli,
                "returned fuel quantity");
            TestHarness.Equal(
                LiquidCargoCustody.Settlement,
                driver.State.LiquidCargoCustody,
                "returned fuel custody");
            return driver;
        }

        private static CoreTestDriver ReachReturnedPending(
            ColonyComposition composition,
            string residentId,
            EncounterChoice encounterChoice,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.StartPreparation(
                residentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:fuel-bond:" + worldSeed;
            string fingerprint = "fp:fuel-bond:" + worldSeed;
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
                new ResolveDepotCommand(sequence, encounterChoice));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            driver.Apply(sequence =>
                new ChooseLiquidReturnCommand(
                    sequence,
                    LiquidCargoKind.Fuel));
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

            return driver;
        }

        private static LastBearingTickResult Apply(
            LastBearingState state,
            Func<long, LastBearingCommand> create)
        {
            return new LastBearingKernel().Step(
                state,
                new[] { create(state.NextCommandSequence) });
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
                            new RestoreDepotAccessCommand(
                                state.NextCommandSequence),
                        }),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
            TestHarness.True(
                before.SequenceEqual(LastBearingCanonicalCodec.Encode(state)),
                label + " mutated state");
        }
    }
}
