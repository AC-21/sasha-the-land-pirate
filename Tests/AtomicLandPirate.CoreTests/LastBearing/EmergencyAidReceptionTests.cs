#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class EmergencyAidReceptionTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "cooperative return queues aid until repaired physical receipt",
                CooperativeReturnQueuesAidUntilRepair);
            harness.Run(
                "water tender receipt credits and clamps exact authored aid",
                ReceiptCreditsAndClampsExactAid);
            harness.Run(
                "water tender works before or after a city improvement",
                ReceiptOrderIsIndependentOfCityImprovement);
            harness.Run(
                "water tender rejects invalid and stale intents atomically",
                InvalidAndStaleIntentsFailAtomically);
            harness.Run(
                "water tender replay never duplicates delivered water",
                DuplicateReceiptIsIdempotent);
            harness.Run(
                "queued and delivered aid round trip in schema 9",
                QueuedAndDeliveredStatesRoundTrip);
            harness.Run(
                "water tender is composition and module invariant",
                CompositionsAndModulesShareMechanics);
            harness.Run(
                "adverse returns never expose the water tender",
                AdverseReturnsNeverExposeReception);
        }

        private static void CooperativeReturnQueuesAidUntilRepair()
        {
            CoreTestDriver driver = ReachReturnedPending(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                EncounterChoice.Cooperate,
                3201);
            driver.Apply(sequence => new SetPauseCommand(sequence, true));
            long waterBeforeCredit = driver.State.WaterMilli;
            string transactionId = TransactionId(3201);
            string fingerprint = Fingerprint(3201);

            TestHarness.Equal(
                FactionAidPolicy.EmergencyWaterQueued,
                driver.State.FactionAidPolicy,
                "returned aid policy");
            TestHarness.Equal(
                LastBearingBalanceV1.CooperateAidWaterMilli,
                driver.State.EmergencyAidWaterMilli,
                "returned aid amount");
            TestHarness.True(
                !driver.View.IsEmergencyAidReceptionAvailable,
                "returned vehicle exposed tender before check-in");

            LastBearingTickResult credited = driver.Apply(sequence =>
                new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            TestHarness.Equal(
                waterBeforeCredit,
                driver.State.WaterMilli,
                "check-in invisibly credited aid");
            TestHarness.Equal(
                FactionAidPolicy.EmergencyWaterQueued,
                driver.State.FactionAidPolicy,
                "check-in consumed queued aid");
            TestHarness.True(
                credited.DomainEvents.All(item =>
                    item.Kind != LastBearingEventKind.EmergencyAidDelivered),
                "check-in emitted aid delivery");

            driver.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            TestHarness.Equal(
                waterBeforeCredit,
                driver.State.WaterMilli,
                "finalization invisibly credited aid");
            TestHarness.True(
                !driver.View.IsEmergencyAidReceptionAvailable,
                "unrepaired return exposed tender");
            TestHarness.Equal(
                "install-turbine-repair",
                driver.View.NextObjective,
                "unrepaired objective");

            driver.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));
            TestHarness.True(
                driver.View.IsEmergencyAidReceptionAvailable,
                "repaired cooperative return did not expose tender");
            TestHarness.Equal(
                LastBearingBalanceV1.CooperateAidWaterMilli,
                driver.View.EmergencyAidWaterMilli,
                "read-model aid amount");
            TestHarness.Equal(
                "receive-emergency-aid-at-water-tender",
                driver.View.NextObjective,
                "water tender objective");
        }

        private static void ReceiptCreditsAndClampsExactAid()
        {
            CoreTestDriver exact = ReachReceptionReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                3202);
            exact.Apply(sequence => new SetPauseCommand(sequence, true));
            long capacity = exact.View.WaterCapacityMilli;
            LastBearingState exactReady =
                new LastBearingStateBuilder(exact.State)
                {
                    WaterMilli = checked(
                        capacity
                        - LastBearingBalanceV1.CooperateAidWaterMilli
                        - 5000),
                }.Build();
            LastBearingTickResult exactResult = ApplyReceipt(exactReady);
            AssertExactTransition(
                exactReady,
                exactResult,
                checked(
                    exactReady.WaterMilli
                    + LastBearingBalanceV1.CooperateAidWaterMilli),
                "exact receipt");

            LastBearingState clampedReady =
                new LastBearingStateBuilder(exact.State)
                {
                    WaterMilli = checked(capacity - 2500),
                }.Build();
            LastBearingTickResult clampedResult =
                ApplyReceipt(clampedReady);
            AssertExactTransition(
                clampedReady,
                clampedResult,
                capacity,
                "clamped receipt");
            TestHarness.Equal(
                2500L,
                clampedResult.DomainEvents[0].AfterValue
                    - clampedResult.DomainEvents[0].BeforeValue,
                "credited clamped quantity");
        }

        private static void ReceiptOrderIsIndependentOfCityImprovement()
        {
            CoreTestDriver receiveFirst = ReachReceptionReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank,
                3203);
            receiveFirst.Apply(sequence =>
                new SetPauseCommand(sequence, true));
            TestHarness.True(
                receiveFirst.View.IsCityImprovementInstallationAvailable,
                "receive-first improvement availability");
            receiveFirst.Apply(sequence =>
                new ReceiveEmergencyAidCommand(sequence));
            receiveFirst.Apply(sequence =>
                new InstallCityImprovementCommand(
                    sequence,
                    NextCityDecision.ExpandEmergencyCistern,
                    LastBearingState.EmergencyStorageExpansionSocketId,
                    LastBearingState
                        .EmergencyStorageExpansionOrientationQuarterTurns));
            TestHarness.Equal(
                FactionAidPolicy.EmergencyWaterDelivered,
                receiveFirst.State.FactionAidPolicy,
                "receive-first aid policy");
            TestHarness.Equal(
                CityImprovementKind.ExpandedEmergencyCistern,
                receiveFirst.State.InstalledCityImprovement,
                "receive-first improvement");

            CoreTestDriver improveFirst = ReachReceptionReady(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.SealedRangeTank,
                3204);
            improveFirst.Apply(sequence =>
                new SetPauseCommand(sequence, true));
            improveFirst.Apply(sequence =>
                new InstallCityImprovementCommand(
                    sequence,
                    NextCityDecision.ExpandEmergencyCistern,
                    LastBearingState.EmergencyStorageExpansionSocketId,
                    LastBearingState
                        .EmergencyStorageExpansionOrientationQuarterTurns));
            TestHarness.True(
                improveFirst.View.IsEmergencyAidReceptionAvailable,
                "improvement-first hid queued aid");
            TestHarness.Equal(
                "receive-emergency-aid-at-water-tender",
                improveFirst.View.NextObjective,
                "improvement-first tender objective");
            long before = improveFirst.State.WaterMilli;
            long expandedCapacity = improveFirst.View.WaterCapacityMilli;
            improveFirst.Apply(sequence =>
                new ReceiveEmergencyAidCommand(sequence));
            TestHarness.Equal(
                Math.Min(
                    expandedCapacity,
                    checked(
                        before
                        + LastBearingBalanceV1.CooperateAidWaterMilli)),
                improveFirst.State.WaterMilli,
                "improvement-first capacity clamp");
            TestHarness.Equal(
                FactionAidPolicy.EmergencyWaterDelivered,
                improveFirst.State.FactionAidPolicy,
                "improvement-first aid policy");
        }

        private static void InvalidAndStaleIntentsFailAtomically()
        {
            CoreTestDriver finalized = ReachFinalizedReturn(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                EncounterChoice.Cooperate,
                3205);
            AssertRejected(
                finalized.State,
                finalized.State.NextCommandSequence,
                "LAST_BEARING_EMERGENCY_AID_NOT_READY",
                "unrepaired cooperative return");

            CoreTestDriver ready = ReachReceptionReady(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                3206);
            AssertRejected(
                ready.State,
                checked(ready.State.NextCommandSequence + 1),
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                "future command sequence");

            LastBearingState forgedAmount =
                new LastBearingStateBuilder(ready.State)
                {
                    EmergencyAidWaterMilli =
                        LastBearingBalanceV1.CooperateAidWaterMilli - 1,
                }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(forgedAmount)
                    .IsEmergencyAidReceptionAvailable,
                "forged aid amount exposed tender");
            AssertRejected(
                forgedAmount,
                forgedAmount.NextCommandSequence,
                "LAST_BEARING_EMERGENCY_AID_NOT_READY",
                "forged aid amount");

            CoreTestDriver adverse = ReachRepairedReturn(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                EncounterChoice.TakeBearing,
                3207);
            AssertRejected(
                adverse.State,
                adverse.State.NextCommandSequence,
                "LAST_BEARING_EMERGENCY_AID_NOT_READY",
                "adverse return");
        }

        private static void DuplicateReceiptIsIdempotent()
        {
            CoreTestDriver driver = ReachReceptionReady(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.SealedRangeTank,
                3208);
            driver.Apply(sequence => new SetPauseCommand(sequence, true));
            driver.Apply(sequence =>
                new ReceiveEmergencyAidCommand(sequence));
            long waterAfter = driver.State.WaterMilli;
            long amountAfter = driver.State.EmergencyAidWaterMilli;

            LastBearingTickResult replay = driver.Apply(sequence =>
                new ReceiveEmergencyAidCommand(sequence));
            TestHarness.Equal(
                waterAfter,
                driver.State.WaterMilli,
                "replay duplicated water");
            TestHarness.Equal(
                amountAfter,
                driver.State.EmergencyAidWaterMilli,
                "replay changed authored amount");
            TestHarness.Equal(
                1,
                replay.DomainEvents.Count,
                "replay event count");
            TestHarness.Equal(
                LastBearingEventKind.IdempotentReplayAccepted,
                replay.DomainEvents[0].Kind,
                "replay event");
        }

        private static void QueuedAndDeliveredStatesRoundTrip()
        {
            LastBearingState queued = ReachReceptionReady(
                ColonyComposition.Mixed,
                ResidentRoster.RobotResidentId,
                PreparationChoice.CivicBuffer,
                VehicleModule.WinchAssembly,
                3209).State;
            AssertRoundTrip(queued, expectedAvailable: true, "queued");

            LastBearingState delivered =
                ApplyReceipt(queued).State;
            AssertRoundTrip(
                delivered,
                expectedAvailable: false,
                "delivered");
            TestHarness.Equal(
                FactionAidPolicy.EmergencyWaterDelivered,
                delivered.FactionAidPolicy,
                "delivered policy");
            TestHarness.Equal(
                LastBearingBalanceV1.CooperateAidWaterMilli,
                delivered.EmergencyAidWaterMilli,
                "delivered provenance");
        }

        private static void CompositionsAndModulesShareMechanics()
        {
            var compositions = new[]
            {
                ColonyComposition.HumanOnly,
                ColonyComposition.RobotOnly,
                ColonyComposition.Mixed,
            };
            var modules = new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            };

            foreach (VehicleModule module in modules)
            {
                string? expectedHash = null;
                foreach (ColonyComposition composition in compositions)
                {
                    string residentId =
                        composition == ColonyComposition.RobotOnly
                            ? ResidentRoster.RobotResidentId
                            : ResidentRoster.HumanResidentId;
                    CoreTestDriver driver = ReachReceptionReady(
                        composition,
                        residentId,
                        PreparationChoice.CivicBuffer,
                        module,
                        3210 + (int)module);
                    driver.Apply(sequence =>
                        new SetPauseCommand(sequence, true));
                    long before = driver.State.WaterMilli;
                    long capacity = driver.View.WaterCapacityMilli;
                    driver.Apply(sequence =>
                        new ReceiveEmergencyAidCommand(sequence));
                    TestHarness.Equal(
                        Math.Min(
                            capacity,
                            checked(
                                before
                                + LastBearingBalanceV1
                                    .CooperateAidWaterMilli)),
                        driver.State.WaterMilli,
                        composition + " " + module + " water");
                    TestHarness.Equal(
                        FactionAidPolicy.EmergencyWaterDelivered,
                        driver.State.FactionAidPolicy,
                        composition + " " + module + " policy");
                    string hash =
                        LastBearingCanonicalCodec
                            .ComputeMechanicalSha256(driver.State);
                    if (expectedHash == null)
                    {
                        expectedHash = hash;
                    }
                    else
                    {
                        TestHarness.Equal(
                            expectedHash,
                            hash,
                            composition + " " + module + " mechanics");
                    }
                }
            }
        }

        private static void AdverseReturnsNeverExposeReception()
        {
            foreach (VehicleModule module in new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            })
            {
                CoreTestDriver driver = ReachRepairedReturn(
                    ColonyComposition.Mixed,
                    ResidentRoster.HumanResidentId,
                    PreparationChoice.CivicBuffer,
                    module,
                    EncounterChoice.TakeBearing,
                    3220 + (int)module);
                TestHarness.True(
                    !driver.View.IsEmergencyAidReceptionAvailable,
                    module + " adverse tender availability");
                TestHarness.Equal(
                    FactionAidPolicy.Withheld,
                    driver.View.FactionAidPolicy,
                    module + " adverse aid policy");
                TestHarness.Equal(
                    0L,
                    driver.View.EmergencyAidWaterMilli,
                    module + " adverse aid amount");
            }
        }

        private static void AssertExactTransition(
            LastBearingState ready,
            LastBearingTickResult result,
            long expectedWater,
            string label)
        {
            LastBearingTickResult ordinaryTick =
                new LastBearingKernel().Step(
                    ready,
                    Array.Empty<LastBearingCommand>());
            LastBearingState expected =
                new LastBearingStateBuilder(ordinaryTick.State)
                {
                    NextCommandSequence = checked(
                        ready.NextCommandSequence + 1),
                    WaterMilli = expectedWater,
                    FactionAidPolicy =
                        FactionAidPolicy.EmergencyWaterDelivered,
                }.Build();

            TestHarness.True(
                LastBearingCanonicalCodec.Encode(expected).SequenceEqual(
                    LastBearingCanonicalCodec.Encode(result.State)),
                label + " changed fields outside the exact transition");
            TestHarness.Equal(
                1,
                result.DomainEvents.Count,
                label + " event count");
            LastBearingDomainEvent delivered = result.DomainEvents[0];
            TestHarness.Equal(
                LastBearingEventKind.EmergencyAidDelivered,
                delivered.Kind,
                label + " event kind");
            TestHarness.Equal(
                LastBearingEventCause.PlayerCommand,
                delivered.Cause,
                label + " event cause");
            TestHarness.Equal(
                "settlement:last-bearing:water",
                delivered.SubjectId,
                label + " event subject");
            TestHarness.Equal(
                ready.WaterMilli,
                delivered.BeforeValue,
                label + " event before");
            TestHarness.Equal(
                expectedWater,
                delivered.AfterValue,
                label + " event after");
            TestHarness.Equal(
                LastBearingBalanceV1.CooperateAidWaterMilli,
                result.State.EmergencyAidWaterMilli,
                label + " retained aid provenance");
            TestHarness.True(
                !result.ReadModel.IsEmergencyAidReceptionAvailable,
                label + " remained available");
        }

        private static void AssertRoundTrip(
            LastBearingState state,
            bool expectedAvailable,
            string label)
        {
            TestHarness.Equal(
                9,
                LastBearingState.CurrentSchemaVersion,
                label + " current schema constant");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                state.SchemaVersion,
                label + " state schema");
            TestHarness.Equal(
                LastBearingBalanceV1.Revision,
                state.BalanceRevision,
                label + " balance revision");
            byte[] encoded = LastBearingCanonicalCodec.Encode(state);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(encoded);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                label + " decode");
            TestHarness.True(
                encoded.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State!)),
                label + " canonical bytes");
            LastBearingReadModel view =
                LastBearingReadModel.FromState(decoded.State!);
            TestHarness.Equal(
                expectedAvailable,
                view.IsEmergencyAidReceptionAvailable,
                label + " restored availability");
            TestHarness.Equal(
                LastBearingBalanceV1.CooperateAidWaterMilli,
                view.EmergencyAidWaterMilli,
                label + " restored aid amount");
        }

        private static CoreTestDriver ReachReceptionReady(
            ColonyComposition composition,
            string residentId,
            PreparationChoice choice,
            VehicleModule module,
            int worldSeed)
        {
            CoreTestDriver driver = ReachFinalizedReturn(
                composition,
                residentId,
                choice,
                module,
                EncounterChoice.Cooperate,
                worldSeed);
            driver.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));
            TestHarness.True(
                driver.View.IsEmergencyAidReceptionAvailable,
                "reception-ready fixture");
            return driver;
        }

        private static CoreTestDriver ReachRepairedReturn(
            ColonyComposition composition,
            string residentId,
            PreparationChoice choice,
            VehicleModule module,
            EncounterChoice encounterChoice,
            int worldSeed)
        {
            CoreTestDriver driver = ReachFinalizedReturn(
                composition,
                residentId,
                choice,
                module,
                encounterChoice,
                worldSeed);
            driver.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));
            return driver;
        }

        private static CoreTestDriver ReachFinalizedReturn(
            ColonyComposition composition,
            string residentId,
            PreparationChoice choice,
            VehicleModule module,
            EncounterChoice encounterChoice,
            int worldSeed)
        {
            CoreTestDriver driver = ReachReturnedPending(
                composition,
                residentId,
                choice,
                module,
                encounterChoice,
                worldSeed);
            driver.Apply(sequence =>
                new CreditCityReturnCommand(
                    sequence,
                    TransactionId(worldSeed),
                    Fingerprint(worldSeed)));
            driver.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    TransactionId(worldSeed),
                    Fingerprint(worldSeed)));
            return driver;
        }

        private static CoreTestDriver ReachReturnedPending(
            ColonyComposition composition,
            string residentId,
            PreparationChoice choice,
            VehicleModule module,
            EncounterChoice encounterChoice,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.StartPreparation(
                residentId,
                choice,
                module);
            AdvanceUntil(
                driver,
                model => model.PreparationPhase == PreparationPhase.Ready,
                drive: false,
                "preparation");

            string transactionId = TransactionId(worldSeed);
            string fingerprint = Fingerprint(worldSeed);
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
            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "depot approach");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence =>
                new ResolveDepotCommand(sequence, encounterChoice));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            if (module == VehicleModule.SealedRangeTank)
            {
                driver.Apply(sequence =>
                    new ChooseLiquidReturnCommand(
                        sequence,
                        choice == PreparationChoice.WorkshopPush
                            ? LiquidCargoKind.Water
                            : LiquidCargoKind.Fuel));
            }

            driver.Apply(sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "home return");
            return driver;
        }

        private static LastBearingTickResult ApplyReceipt(
            LastBearingState state)
        {
            return new LastBearingKernel().Step(
                state,
                new LastBearingCommand[]
                {
                    new ReceiveEmergencyAidCommand(
                        state.NextCommandSequence),
                });
        }

        private static void AssertRejected(
            LastBearingState state,
            long commandSequence,
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
                            new ReceiveEmergencyAidCommand(
                                commandSequence),
                        }),
                    label + " was accepted");
            TestHarness.Equal(
                expectedCode,
                error.Message,
                label + " rejection code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                label + " mutated authoritative state");
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

        private static string TransactionId(int worldSeed)
        {
            return "tx:water-tender:" + worldSeed;
        }

        private static string Fingerprint(int worldSeed)
        {
            return "fp:water-tender:" + worldSeed;
        }
    }
}
