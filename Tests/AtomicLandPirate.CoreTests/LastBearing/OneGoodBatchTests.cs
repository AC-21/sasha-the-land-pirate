#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class OneGoodBatchTests
    {
        private const int CanonicalWorldSeed = 2301;

        public static void Run(TestHarness harness)
        {
            harness.Run(
                "one good batch starts with exact conservation",
                StartConservesExactResources);
            harness.Run(
                "one good batch uses 120 subsequent unpaused ticks",
                ProgressUsesSubsequentUnpausedTicks);
            harness.Run(
                "one good batch completes one fixed physical lot",
                CompletionCreatesOneFixedLot);
            harness.Run(
                "one good batch barter preserves adverse history",
                BarterPreservesAdverseHistory);
            harness.Run(
                "one good batch invalid transitions fail atomically",
                InvalidTransitionsFailAtomically);
            harness.Run(
                "one good batch retries do not duplicate value",
                RetriesDoNotDuplicateValue);
            harness.Run(
                "one good batch canonical v7 is exact and unknown versions refuse",
                CanonicalV7RoundTripsAndUnknownVersionsRefuse);
            harness.Run(
                "one good batch mechanics are composition invariant",
                CompositionsShareExactMechanics);
            harness.Run(
                "one good batch fixed identities match the authored contract",
                FixedIdentitiesAreExact);
        }

        private static void StartConservesExactResources()
        {
            LastBearingState ready = CreateReadyState(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                CanonicalWorldSeed);
            TestHarness.True(
                LastBearingReadModel.FromState(ready)
                    .IsSpareBearingBatchStartAvailable,
                "batch start was unavailable");
            long partsBefore = ready.PartsUnits;
            LastBearingTickResult result = Apply(
                ready,
                sequence => new StartSpareBearingBatchCommand(sequence));

            TestHarness.Equal(
                partsBefore - LastBearingBalanceV1.SpareBearingBatchPartsCostUnits,
                result.State.PartsUnits,
                "batch parts debit");
            TestHarness.True(
                result.State.PartsUnits
                    >= LastBearingBalanceV1
                        .SpareBearingBatchRetainedReservePartsUnits,
                "batch retained reserve");
            TestHarness.Equal(
                SpareBearingRecipe.SpareBearingOneGoodBatch,
                result.State.SpareBearingRecipe,
                "batch recipe");
            TestHarness.Equal(
                SpareBearingBatchPhase.InProgress,
                result.State.SpareBearingBatchPhase,
                "batch phase");
            TestHarness.Equal(0L, result.State.SpareBearingElapsedTicks, "start tick");
            TestHarness.Equal(
                LastBearingBalanceV1.SpareBearingBatchRequiredSettlementTicks,
                result.State.SpareBearingRequiredTicks,
                "required ticks");
            TestHarness.Equal(0L, result.State.SpareBearingLotQuantity, "start lot");
            TestHarness.Equal(
                SpareBearingLotCustody.None,
                result.State.SpareBearingLotCustody,
                "start custody");
            TestHarness.Equal(
                NextCityDecision.MachineSpareBearing,
                result.State.NextCityDecision,
                "pending decision");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.CityResourcesCommitted
                    && item.BeforeValue == partsBefore
                    && item.AfterValue == result.State.PartsUnits),
                "resource event");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.SpareBearingBatchStarted
                    && item.SubjectId == LastBearingState.SpareBearingBatchId),
                "batch start event");
            TestHarness.Equal(
                "machine-one-good-batch",
                result.ReadModel.NextObjective,
                "start objective");
        }

        private static void ProgressUsesSubsequentUnpausedTicks()
        {
            LastBearingState state = CreateStartedStateForSaveTests();
            LastBearingTickResult paused = Apply(
                state,
                sequence => new SetPauseCommand(sequence, true));
            state = paused.State;
            for (int index = 0; index < 10; index++)
            {
                state = Step(state).State;
            }

            TestHarness.Equal(0L, state.SpareBearingElapsedTicks, "paused progress");
            LastBearingTickResult unpaused = Apply(
                state,
                sequence => new SetPauseCommand(sequence, false));
            state = unpaused.State;
            TestHarness.Equal(1L, state.SpareBearingElapsedTicks, "unpause tick");

            int checkpointEvents = CountEvents(
                unpaused,
                LastBearingEventKind.SpareBearingBatchCheckpointReached);
            for (int index = 0; index < 58; index++)
            {
                LastBearingTickResult tick = Step(state);
                state = tick.State;
                checkpointEvents += CountEvents(
                    tick,
                    LastBearingEventKind.SpareBearingBatchCheckpointReached);
            }

            TestHarness.Equal(59L, state.SpareBearingElapsedTicks, "pre-checkpoint");
            LastBearingTickResult checkpoint = Step(state);
            state = checkpoint.State;
            checkpointEvents += CountEvents(
                checkpoint,
                LastBearingEventKind.SpareBearingBatchCheckpointReached);
            TestHarness.Equal(60L, state.SpareBearingElapsedTicks, "checkpoint tick");
            TestHarness.Equal(1, checkpointEvents, "checkpoint event count");

            for (int index = 0; index < 59; index++)
            {
                LastBearingTickResult tick = Step(state);
                state = tick.State;
                checkpointEvents += CountEvents(
                    tick,
                    LastBearingEventKind.SpareBearingBatchCheckpointReached);
            }

            TestHarness.Equal(119L, state.SpareBearingElapsedTicks, "pre-completion");
            LastBearingTickResult completion = Step(state);
            TestHarness.Equal(
                SpareBearingBatchPhase.Complete,
                completion.State.SpareBearingBatchPhase,
                "completion phase");
            TestHarness.Equal(1, checkpointEvents, "single checkpoint");
        }

        private static void CompletionCreatesOneFixedLot()
        {
            LastBearingState midpoint = CreateMidpointStateForSaveTests();
            LastBearingState state = midpoint;
            LastBearingTickResult? completion = null;
            for (int index = 0; index < 60; index++)
            {
                completion = Step(state);
                state = completion.State;
            }

            TestHarness.True(completion != null, "completion result missing");
            TestHarness.Equal(120L, state.SpareBearingElapsedTicks, "elapsed");
            TestHarness.Equal(
                SpareBearingBatchPhase.Complete,
                state.SpareBearingBatchPhase,
                "complete phase");
            TestHarness.Equal(1L, state.SpareBearingLotQuantity, "lot quantity");
            TestHarness.Equal(
                SpareBearingLotCustody.WorkshopOutput,
                state.SpareBearingLotCustody,
                "lot output custody");
            TestHarness.Equal(
                NextCityDecision.None,
                state.NextCityDecision,
                "decision consumption");
            TestHarness.True(!state.RoutePermitGranted, "premature permit");
            TestHarness.Equal(
                0L,
                LastBearingReadModel.FromState(state).SpareBearingRemainingTicks,
                "remaining ticks");
            TestHarness.True(
                completion!.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.SpareBearingBatchCompleted),
                "completion event");
            TestHarness.True(
                completion.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.SpareBearingLotCreated
                    && item.SubjectId == LastBearingState.SpareBearingLotId
                    && item.AfterValue == 1),
                "lot event");
        }

        private static void BarterPreservesAdverseHistory()
        {
            LastBearingState complete = CreateCompletedStateForSaveTests();
            LastBearingTickResult ordinaryTick = Step(complete);
            LastBearingTickResult barter = Apply(
                complete,
                sequence => new BarterSpareBearingLotCommand(sequence));
            var expected = new LastBearingStateBuilder(ordinaryTick.State)
            {
                NextCommandSequence = checked(complete.NextCommandSequence + 1),
                SpareBearingBatchPhase = SpareBearingBatchPhase.Settled,
                SpareBearingLotCustody =
                    SpareBearingLotCustody.LastBearingClaimsCounter,
                RoutePermitGranted = true,
                FactionAccessPolicy = FactionAccessPolicy.PermitRequired,
            }.Build();
            TestHarness.True(
                LastBearingCanonicalCodec.Encode(expected).SequenceEqual(
                    LastBearingCanonicalCodec.Encode(barter.State)),
                "barter changed fields outside the fixed settlement transition");
            TestHarness.Equal(
                DepotControl.Depleted,
                barter.State.DepotControl,
                "depot control");
            TestHarness.Equal(
                complete.DepotBearingDisposition,
                barter.State.DepotBearingDisposition,
                "bearing history");
            TestHarness.Equal(
                FactionClaimState.Aggrieved,
                barter.State.FactionClaimState,
                "faction claim history");
            TestHarness.True(barter.State.FactionMemory != null, "faction memory");
            TestHarness.Equal(
                LastBearingBalanceV1.TakeTrustDelta,
                barter.State.FactionTrust,
                "faction trust");
            TestHarness.Equal(
                LastBearingBalanceV1.TakeGrievanceDelta,
                barter.State.FactionGrievance,
                "faction grievance");
            TestHarness.Equal(
                LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                barter.State.FutureRouteTollFuelUnits,
                "future route toll");
            TestHarness.True(
                barter.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.SpareBearingLotBartered),
                "barter event");
            TestHarness.True(
                barter.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.RoutePermitGranted
                    && item.SubjectId
                        == LastBearingState.DepotCorridorRoutePermitId),
                "permit event");
            TestHarness.Equal(
                "route-permit-recorded",
                barter.ReadModel.NextObjective,
                "settled objective");
        }

        private static void InvalidTransitionsFailAtomically()
        {
            LastBearingState ready = CreateReadyState(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                CanonicalWorldSeed);
            LastBearingState insufficient = new LastBearingStateBuilder(ready)
            {
                PartsUnits =
                    LastBearingBalanceV1
                        .SpareBearingBatchMinimumPreStartPartsUnits - 1,
            }.Build();
            AssertRejectedWithoutMutation(
                insufficient,
                new StartSpareBearingBatchCommand(
                    insufficient.NextCommandSequence),
                "LAST_BEARING_SPARE_BEARING_BATCH_PARTS_INSUFFICIENT",
                "insufficient parts");

            LastBearingState wrongDecision = new LastBearingStateBuilder(ready)
            {
                NextCityDecision = NextCityDecision.None,
            }.Build();
            AssertRejectedWithoutMutation(
                wrongDecision,
                new StartSpareBearingBatchCommand(
                    wrongDecision.NextCommandSequence),
                "LAST_BEARING_SPARE_BEARING_BATCH_NOT_ELIGIBLE",
                "wrong decision");

            LastBearingState existingPermit = new LastBearingStateBuilder(ready)
            {
                RoutePermitGranted = true,
                FactionAccessPolicy = FactionAccessPolicy.PermitRequired,
            }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(existingPermit)
                    .IsSpareBearingBatchStartAvailable,
                "existing permit exposed batch start");
            AssertRejectedWithoutMutation(
                existingPermit,
                new StartSpareBearingBatchCommand(
                    existingPermit.NextCommandSequence),
                "LAST_BEARING_SPARE_BEARING_BATCH_NOT_ELIGIBLE",
                "existing permit");

            LastBearingState started = CreateStartedStateForSaveTests();
            AssertRejectedWithoutMutation(
                started,
                new BarterSpareBearingLotCommand(started.NextCommandSequence),
                "LAST_BEARING_SPARE_BEARING_LOT_NOT_AVAILABLE",
                "barter before completion");

            LastBearingState forged = new LastBearingStateBuilder(started)
            {
                SpareBearingLotQuantity = 1,
            }.BuildUnchecked();
            InvalidOperationException invariant =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingInvariants.Validate(forged),
                    "forged in-progress lot was accepted");
            TestHarness.Equal(
                "LAST_BEARING_SPARE_BEARING_BATCH_PROGRESS_INVALID",
                invariant.Message,
                "forged lot invariant");

            AssertForgedBatchStateRejected(
                started,
                builder => builder.FactionClaimState = FactionClaimState.Claimed,
                "forged faction claim");
            AssertForgedBatchStateRejected(
                started,
                builder => builder.FactionAccessPolicy =
                    FactionAccessPolicy.SharedService,
                "forged access policy");
            AssertForgedBatchStateRejected(
                started,
                builder => builder.DepotAccessFeePartsUnits = checked(
                    builder.DepotAccessFeePartsUnits + 1),
                "forged depot fee");
            AssertForgedBatchStateRejected(
                started,
                builder => builder.FactionMemory = new FactionMemoryRecord(
                    "memory:last-bearing:take:0001",
                    "ForgedAction",
                    LastBearingState.LastBearingFactionId,
                    LastBearingBalanceV1.TakeGrievanceDelta,
                    "custody-breach",
                    started.FactionMemory!.EncounterTick,
                    "DEPOT_ACCESS_CLOSED"),
                "forged faction memory");

            LastBearingState complete = CreateCompletedStateForSaveTests();
            AssertForgedBatchStateRejected(
                complete,
                builder => builder.FactionAccessPolicy =
                    FactionAccessPolicy.Open,
                "forged completed access policy");
        }

        private static void RetriesDoNotDuplicateValue()
        {
            LastBearingState complete = CreateCompletedStateForSaveTests();
            LastBearingTickResult startReplay = Apply(
                complete,
                sequence => new StartSpareBearingBatchCommand(sequence));
            TestHarness.Equal(1L, startReplay.State.SpareBearingLotQuantity, "start replay lot");
            TestHarness.Equal(
                complete.PartsUnits,
                startReplay.State.PartsUnits,
                "start replay parts");
            TestHarness.True(
                startReplay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "start replay audit");

            LastBearingState settled = CreateSettledStateForSaveTests();
            LastBearingTickResult barterReplay = Apply(
                settled,
                sequence => new BarterSpareBearingLotCommand(sequence));
            TestHarness.Equal(1L, barterReplay.State.SpareBearingLotQuantity, "barter replay lot");
            TestHarness.Equal(
                SpareBearingLotCustody.LastBearingClaimsCounter,
                barterReplay.State.SpareBearingLotCustody,
                "barter replay custody");
            TestHarness.True(
                barterReplay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "barter replay audit");
            TestHarness.True(
                barterReplay.DomainEvents.All(item =>
                    item.Kind != LastBearingEventKind.SpareBearingLotBartered
                    && item.Kind != LastBearingEventKind.RoutePermitGranted),
                "barter replay repeated settlement effects");
        }

        private static void CanonicalV7RoundTripsAndUnknownVersionsRefuse()
        {
            foreach (LastBearingState state in new[]
            {
                CreateStartedStateForSaveTests(),
                CreateMidpointStateForSaveTests(),
                CreateCompletedStateForSaveTests(),
                CreateSettledStateForSaveTests(),
            })
            {
                byte[] encoded = LastBearingCanonicalCodec.Encode(state);
                TestHarness.Equal((byte)7, encoded[8], "codec version low byte");
                TestHarness.Equal((byte)0, encoded[9], "codec version high byte");
                LastBearingDecodeResult decoded =
                    LastBearingCanonicalCodec.TryDecode(encoded);
                TestHarness.True(
                    decoded.Succeeded && decoded.State != null,
                    "v7 round trip decode");
                TestHarness.True(
                    encoded.SequenceEqual(
                        LastBearingCanonicalCodec.Encode(decoded.State!)),
                    "v7 canonical bytes");
            }

            foreach (byte version in new byte[] { 1, 2, 8 })
            {
                byte[] encoded = LastBearingCanonicalCodec.Encode(
                    CreateStartedStateForSaveTests());
                encoded[8] = version;
                encoded[9] = 0;
                LastBearingDecodeResult decoded =
                    LastBearingCanonicalCodec.TryDecode(encoded);
                TestHarness.True(!decoded.Succeeded, "unknown version accepted");
                TestHarness.Equal(
                    LastBearingCanonicalCodec.DecodeUnknownVersionCode,
                    decoded.Code,
                    "unknown version code");
            }
        }

        private static void CompositionsShareExactMechanics()
        {
            LastBearingState human = CreateSettledState(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId);
            LastBearingState robot = CreateSettledState(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId);
            LastBearingState mixed = CreateSettledState(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId);
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

        private static void FixedIdentitiesAreExact()
        {
            TestHarness.Equal(
                "recipe:last-bearing:spare-bearing:0001",
                LastBearingState.SpareBearingRecipeId,
                "recipe identity");
            TestHarness.Equal(
                "world:last-bearing:manufacturing-job:0001",
                LastBearingState.SpareBearingBatchId,
                "manufacturing identity");
            TestHarness.Equal(
                "world:last-bearing:lot:0001",
                LastBearingState.SpareBearingLotId,
                "lot identity");
            TestHarness.Equal(
                "world:last-bearing:trade-contract:0001",
                LastBearingState.SpareBearingTradeContractId,
                "trade contract identity");
            TestHarness.Equal(
                "world:last-bearing:promise:0001",
                LastBearingState.DepotCorridorRoutePermitId,
                "permit promise identity");
            TestHarness.Equal(
                "settlement:last-bearing:workshop-output",
                LastBearingState.SpareBearingWorkshopOutputId,
                "workshop identity");
            TestHarness.Equal(
                "site:last-bearing-claims-counter",
                LastBearingState.LastBearingClaimsCounterId,
                "claims identity");
            TestHarness.Equal(
                "board:last-bearing:depot-corridor",
                LastBearingState.DepotCorridorRouteBoardId,
                "board identity");
            TestHarness.Equal(
                "bld_machine_shop_claims_wicket_a",
                LastBearingState.OneGoodBatchPresentationContentId,
                "presentation identity");
        }

        internal static LastBearingState CreateStartedStateForSaveTests()
        {
            LastBearingState ready = CreateReadyState(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                CanonicalWorldSeed);
            return Apply(
                ready,
                sequence => new StartSpareBearingBatchCommand(sequence)).State;
        }

        internal static LastBearingState CreateMidpointStateForSaveTests()
        {
            return Advance(CreateStartedStateForSaveTests(), 60);
        }

        internal static LastBearingState CreateCompletedStateForSaveTests()
        {
            return Advance(CreateStartedStateForSaveTests(), 120);
        }

        internal static LastBearingState CreateSettledStateForSaveTests()
        {
            LastBearingState complete = CreateCompletedStateForSaveTests();
            return Apply(
                complete,
                sequence => new BarterSpareBearingLotCommand(sequence)).State;
        }

        private static LastBearingState CreateSettledState(
            ColonyComposition composition,
            string residentId)
        {
            LastBearingState ready = CreateReadyState(
                composition,
                residentId,
                CanonicalWorldSeed);
            LastBearingState started = Apply(
                ready,
                sequence => new StartSpareBearingBatchCommand(sequence)).State;
            LastBearingState complete = Advance(started, 120);
            return Apply(
                complete,
                sequence => new BarterSpareBearingLotCommand(sequence)).State;
        }

        private static LastBearingState CreateReadyState(
            ColonyComposition composition,
            string residentId,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.StartPreparation(
                residentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:one-good-batch:" + worldSeed;
            string fingerprint = "fp:one-good-batch:" + worldSeed;
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            while (!driver.View.IsDepotApproachRecoveryAvailable)
            {
                driver.OperateWreckLineIfAvailable();
                driver.Apply(sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence => new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new ResolveDepotCommand(
                sequence,
                EncounterChoice.TakeBearing));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            while (driver.View.ExpeditionPhase != ExpeditionPhase.Returned)
            {
                driver.Apply(sequence => new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new FinalizeExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new InstallTurbineRepairCommand(sequence));
            TestHarness.Equal(
                LastBearingBalanceV1.TakeFutureRouteTollFuelUnits,
                driver.State.FutureRouteTollFuelUnits,
                "canonical adverse outcome matured");
            return driver.State;
        }

        private static LastBearingTickResult Apply(
            LastBearingState state,
            Func<long, LastBearingCommand> create)
        {
            return new LastBearingKernel().Step(
                state,
                new[] { create(state.NextCommandSequence) });
        }

        private static LastBearingTickResult Step(LastBearingState state)
        {
            return new LastBearingKernel().Step(
                state,
                Array.Empty<LastBearingCommand>());
        }

        private static LastBearingState Advance(
            LastBearingState state,
            int ticks)
        {
            for (int index = 0; index < ticks; index++)
            {
                state = Step(state).State;
            }

            return state;
        }

        private static int CountEvents(
            LastBearingTickResult result,
            LastBearingEventKind kind)
        {
            return result.DomainEvents.Count(item => item.Kind == kind);
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

        private static void AssertForgedBatchStateRejected(
            LastBearingState state,
            Action<LastBearingStateBuilder> mutate,
            string label)
        {
            var builder = new LastBearingStateBuilder(state);
            mutate(builder);
            LastBearingState forged = builder.BuildUnchecked();
            TestHarness.Throws<InvalidOperationException>(
                () => LastBearingInvariants.Validate(forged),
                label + " was accepted");
        }
    }
}
