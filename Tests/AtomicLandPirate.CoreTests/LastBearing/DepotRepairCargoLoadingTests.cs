#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class DepotRepairCargoLoadingTests
    {
        public static void Run(TestHarness harness)
        {
            harness.Run(
                "depot outcomes stage repair cargo at truthful sources",
                OutcomesStageTruthfulSources);
            harness.Run(
                "claimed bearing custody transfers with adverse control",
                ClaimedBearingTransfersAtomically);
            harness.Run(
                "depot repair load is exact atomic and idempotent",
                LoadIsExactAtomicAndIdempotent);
            harness.Run(
                "forged depot source lineage fails closed",
                ForgedSourceLineageFailsClosed);
            harness.Run(
                "repair source and loaded checkpoints round trip exactly",
                CheckpointsRoundTripExactly);
            harness.Run(
                "legacy vehicle cargo occupancy normalizes only at freeze",
                LegacyVehicleCargoNormalizesAtFreeze);
        }

        private static void OutcomesStageTruthfulSources()
        {
            CoreTestDriver cooperate = ReachResolvedDepot(
                EncounterChoice.Cooperate,
                waitForFactionClaim: false,
                2401);
            AssertSource(
                cooperate,
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Faction,
                "cooperate");
            AssertAcceptedLoad(
                cooperate,
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Faction,
                "cooperate");
            TestHarness.Equal(
                DepotBearingDisposition.FactionHeld,
                cooperate.State.DepotBearingDisposition,
                "cooperate bearing disposition after load");
            TestHarness.Equal(
                DepotControl.SharedAccess,
                cooperate.View.DepotControl,
                "cooperate depot control after load");

            CoreTestDriver take = ReachResolvedDepot(
                EncounterChoice.TakeBearing,
                waitForFactionClaim: false,
                2402);
            AssertSource(
                take,
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Depot,
                "unclaimed take");
            TestHarness.True(
                take.State.DepotBearingDisposition
                    == DepotBearingDisposition.AtDepot,
                "unclaimed take committed disposition before physical load");
            AssertAcceptedLoad(
                take,
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Depot,
                "unclaimed take");
            TestHarness.Equal(
                DepotBearingDisposition.TakenBySasha,
                take.State.DepotBearingDisposition,
                "take disposition after load");
            TestHarness.Equal(
                DepotControl.Depleted,
                take.View.DepotControl,
                "take depot control after load");
        }

        private static void ClaimedBearingTransfersAtomically()
        {
            CoreTestDriver driver = ReachDepot(
                waitForFactionClaim: true,
                2003);
            TestHarness.Equal(
                DepotBearingDisposition.FactionHeld,
                driver.State.DepotBearingDisposition,
                "claimed bearing disposition before resolution");
            TestHarness.Equal(
                DepotControl.FactionClaimed,
                driver.View.DepotControl,
                "claimed depot control before resolution");

            driver.Apply(sequence => new ResolveDepotCommand(
                sequence,
                EncounterChoice.TakeBearing));
            AssertSource(
                driver,
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Faction,
                "claimed take");
            TestHarness.Equal(
                DepotBearingDisposition.FactionHeld,
                driver.State.DepotBearingDisposition,
                "claimed bearing moved before load");
            TestHarness.Equal(
                DepotControl.FactionClaimed,
                driver.View.DepotControl,
                "claimed depot depleted before load");
            TestHarness.True(
                driver.View.FactionGrievance > 0,
                "take resolution omitted grievance");

            AssertAcceptedLoad(
                driver,
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Faction,
                "claimed take");
            TestHarness.Equal(
                DepotBearingDisposition.TakenBySasha,
                driver.State.DepotBearingDisposition,
                "claimed take disposition after load");
            TestHarness.Equal(
                DepotControl.Depleted,
                driver.View.DepotControl,
                "claimed take depot control after load");
        }

        private static void LoadIsExactAtomicAndIdempotent()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    2403);
            AssertRejectedAtomically(
                initial,
                new LoadDepotRepairCargoCommand(
                    initial.NextCommandSequence),
                "LAST_BEARING_REPAIR_CARGO_LOAD_PHASE_INVALID",
                "early load");

            CoreTestDriver unresolved = ReachDepot(
                waitForFactionClaim: false,
                2404);
            AssertRejectedAtomically(
                unresolved.State,
                new LoadDepotRepairCargoCommand(
                    unresolved.State.NextCommandSequence),
                "LAST_BEARING_REPAIR_CARGO_LOAD_PHASE_INVALID",
                "unresolved load");

            unresolved.Apply(sequence => new ResolveDepotCommand(
                sequence,
                EncounterChoice.Cooperate));
            AssertRejectedAtomically(
                unresolved.State,
                new FreezeReturnPayloadCommand(
                    unresolved.State.NextCommandSequence,
                    unresolved.State.TransactionId!,
                    unresolved.State.TransactionFingerprint!),
                "LAST_BEARING_REPAIR_CARGO_NOT_LOADED",
                "source-custody freeze");
            AssertRejectedAtomically(
                unresolved.State,
                new LoadDepotRepairCargoCommand(
                    unresolved.State.NextCommandSequence + 1),
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                "load sequence mismatch");

            var mismatched = new LastBearingStateBuilder(unresolved.State);
            mismatched.RepairCargoCustody = RepairCargoCustody.Depot;
            TestHarness.Throws<InvalidOperationException>(
                () => mismatched.Build(),
                "mismatched source state was accepted");
            var empty = new LastBearingStateBuilder(unresolved.State);
            empty.RepairCargoKind = RepairCargoKind.None;
            empty.RepairCargoCustody = RepairCargoCustody.None;
            TestHarness.Throws<InvalidOperationException>(
                () => empty.Build(),
                "resolved empty source state was accepted");

            AssertAcceptedLoad(
                unresolved,
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Faction,
                "idempotency fixture");
            long usedBeforeReplay =
                unresolved.State.OrdinaryCargoUsedUnits;
            LastBearingTickResult replay = unresolved.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            TestHarness.Equal(
                usedBeforeReplay,
                unresolved.State.OrdinaryCargoUsedUnits,
                "replay changed cargo occupancy");
            TestHarness.Equal(
                RepairCargoCustody.Vehicle,
                unresolved.View.RepairCargoCustody,
                "replay moved loaded cargo");
            TestHarness.Equal(
                0,
                replay.DomainEvents.Count(domainEvent =>
                    domainEvent.Kind
                        == LastBearingEventKind.RepairCargoTransferred),
                "replay emitted a second custody transfer");
            TestHarness.Equal(
                1,
                replay.DomainEvents.Count(domainEvent =>
                    domainEvent.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "replay acknowledgement count");

            string transactionId = unresolved.State.TransactionId!;
            string fingerprint = unresolved.State.TransactionFingerprint!;
            unresolved.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            AssertRejectedAtomically(
                unresolved.State,
                new LoadDepotRepairCargoCommand(
                    unresolved.State.NextCommandSequence),
                "LAST_BEARING_REPAIR_CARGO_LOAD_PHASE_INVALID",
                "post-freeze load");

            while (unresolved.View.ExpeditionPhase
                != ExpeditionPhase.Returned)
            {
                unresolved.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            AssertRejectedAtomically(
                unresolved.State,
                new LoadDepotRepairCargoCommand(
                    unresolved.State.NextCommandSequence),
                "LAST_BEARING_REPAIR_CARGO_LOAD_PHASE_INVALID",
                "returned load");
            unresolved.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            unresolved.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            AssertRejectedAtomically(
                unresolved.State,
                new LoadDepotRepairCargoCommand(
                    unresolved.State.NextCommandSequence),
                "LAST_BEARING_REPAIR_CARGO_LOAD_PHASE_INVALID",
                "finalized load");
            unresolved.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));
            AssertRejectedAtomically(
                unresolved.State,
                new LoadDepotRepairCargoCommand(
                    unresolved.State.NextCommandSequence),
                "LAST_BEARING_REPAIR_CARGO_LOAD_PHASE_INVALID",
                "installed load");
        }

        private static void CheckpointsRoundTripExactly()
        {
            AssertPathRoundTrips(
                EncounterChoice.Cooperate,
                waitForFactionClaim: false,
                worldSeed: 2405,
                RepairCargoCustody.Faction,
                "cooperative sleeve");
            AssertPathRoundTrips(
                EncounterChoice.TakeBearing,
                waitForFactionClaim: false,
                worldSeed: 2406,
                RepairCargoCustody.Depot,
                "unclaimed bearing");
            AssertPathRoundTrips(
                EncounterChoice.TakeBearing,
                waitForFactionClaim: true,
                worldSeed: 2407,
                RepairCargoCustody.Faction,
                "faction-held bearing");
        }

        private static void AssertPathRoundTrips(
            EncounterChoice choice,
            bool waitForFactionClaim,
            int worldSeed,
            RepairCargoCustody expectedSource,
            string label)
        {
            CoreTestDriver driver = ReachResolvedDepot(
                choice,
                waitForFactionClaim,
                worldSeed);
            AssertCanonicalRoundTrip(
                driver.State,
                interactionAvailable: true,
                expectedSource,
                label + " source checkpoint");

            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            AssertCanonicalRoundTrip(
                driver.State,
                interactionAvailable: false,
                RepairCargoCustody.Vehicle,
                label + " loaded checkpoint");
        }

        private static void ForgedSourceLineageFailsClosed()
        {
            CoreTestDriver unclaimed = ReachResolvedDepot(
                EncounterChoice.TakeBearing,
                waitForFactionClaim: false,
                2408);
            var forgedFactionSource =
                new LastBearingStateBuilder(unclaimed.State);
            forgedFactionSource.RepairCargoCustody =
                RepairCargoCustody.Faction;
            forgedFactionSource.DepotBearingDisposition =
                DepotBearingDisposition.FactionHeld;
            TestHarness.Throws<InvalidOperationException>(
                () => forgedFactionSource.Build(),
                "unclaimed control accepted a forged faction source");

            CoreTestDriver claimed = ReachResolvedDepot(
                EncounterChoice.TakeBearing,
                waitForFactionClaim: true,
                2409);
            var forgedDepotSource = new LastBearingStateBuilder(claimed.State);
            forgedDepotSource.RepairCargoCustody = RepairCargoCustody.Depot;
            forgedDepotSource.DepotBearingDisposition =
                DepotBearingDisposition.AtDepot;
            TestHarness.Throws<InvalidOperationException>(
                () => forgedDepotSource.Build(),
                "faction control accepted a forged depot source");

            var forgedDepotLineage =
                new LastBearingStateBuilder(claimed.State);
            forgedDepotLineage.RepairCargoCustody = RepairCargoCustody.Depot;
            forgedDepotLineage.DepotBearingDisposition =
                DepotBearingDisposition.AtDepot;
            forgedDepotLineage.DepotControl = DepotControl.Unclaimed;
            TestHarness.Throws<InvalidOperationException>(
                () => forgedDepotLineage.Build(),
                "claimed progress accepted forged unclaimed lineage");

            CoreTestDriver cooperate = ReachResolvedDepot(
                EncounterChoice.Cooperate,
                waitForFactionClaim: false,
                2410);
            var forgedCooperativeControl =
                new LastBearingStateBuilder(cooperate.State);
            forgedCooperativeControl.DepotControl = DepotControl.FactionClaimed;
            TestHarness.Throws<InvalidOperationException>(
                () => forgedCooperativeControl.Build(),
                "cooperative sleeve accepted non-shared control");
        }

        private static void LegacyVehicleCargoNormalizesAtFreeze()
        {
            foreach (EncounterChoice choice in new[]
            {
                EncounterChoice.Cooperate,
                EncounterChoice.TakeBearing,
            })
            {
                CoreTestDriver driver = ReachResolvedDepot(
                    choice,
                    waitForFactionClaim: false,
                    2410 + (int)choice);
                driver.Apply(sequence =>
                    new LoadDepotRepairCargoCommand(sequence));

                var legacyBuilder =
                    new LastBearingStateBuilder(driver.State);
                legacyBuilder.OrdinaryCargoUsedUnits = 0;
                var legacy = new CoreTestDriver(legacyBuilder.Build());
                TestHarness.True(
                    !legacy.View.IsRepairCargoLoadAvailable,
                    choice + " legacy cargo exposed a source interaction");

                LastBearingTickResult replay = legacy.Apply(sequence =>
                    new LoadDepotRepairCargoCommand(sequence));
                TestHarness.Equal(
                    0L,
                    legacy.State.OrdinaryCargoUsedUnits,
                    choice + " replay normalized legacy occupancy");
                TestHarness.Equal(
                    1,
                    replay.DomainEvents.Count(domainEvent =>
                        domainEvent.Kind
                            == LastBearingEventKind.IdempotentReplayAccepted),
                    choice + " legacy replay event");

                legacy.Apply(sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    legacy.State.TransactionId!,
                    legacy.State.TransactionFingerprint!));
                TestHarness.Equal(
                    1L,
                    legacy.State.OrdinaryCargoUsedUnits,
                    choice + " freeze did not normalize legacy occupancy");
                TestHarness.True(
                    legacy.State.ReturnPayloadFrozen,
                    choice + " legacy payload did not freeze");
            }
        }

        private static CoreTestDriver ReachResolvedDepot(
            EncounterChoice choice,
            bool waitForFactionClaim,
            int worldSeed)
        {
            CoreTestDriver driver = ReachDepot(
                waitForFactionClaim,
                worldSeed);
            driver.Apply(sequence =>
                new ResolveDepotCommand(sequence, choice));
            return driver;
        }

        private static CoreTestDriver ReachDepot(
            bool waitForFactionClaim,
            int worldSeed)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                worldSeed);
            if (waitForFactionClaim)
            {
                driver.Advance(checked((int)
                    LastBearingBalanceV1.DustFrontThresholdCrisisTicks));
                driver.Apply(sequence =>
                    new AcknowledgeDustFrontCommand(sequence));
                driver.Advance(3000);
                TestHarness.Equal(
                    FactionClaimState.Claimed,
                    driver.View.FactionClaimState,
                    "faction claim setup");
            }

            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:depot-load:" + worldSeed;
            string fingerprint = "fp:depot-load:" + worldSeed;
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
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            return driver;
        }

        private static void AssertSource(
            CoreTestDriver driver,
            RepairCargoKind expectedKind,
            RepairCargoCustody expectedCustody,
            string label)
        {
            TestHarness.Equal(
                expectedKind,
                driver.View.RepairCargoKind,
                label + " repair kind");
            TestHarness.Equal(
                expectedCustody,
                driver.View.RepairCargoCustody,
                label + " source custody");
            TestHarness.Equal(
                0L,
                driver.State.OrdinaryCargoUsedUnits,
                label + " source occupancy");
            TestHarness.True(
                driver.View.IsRepairCargoLoadAvailable,
                label + " load availability");
            TestHarness.Equal(
                "load-depot-repair-cargo",
                driver.View.NextObjective,
                label + " next objective");
        }

        private static void AssertAcceptedLoad(
            CoreTestDriver driver,
            RepairCargoKind expectedKind,
            RepairCargoCustody expectedSource,
            string label)
        {
            long sequenceBefore = driver.State.NextCommandSequence;
            LastBearingTickResult result = driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            LastBearingDomainEvent[] transfers = result.DomainEvents
                .Where(domainEvent => domainEvent.Kind
                    == LastBearingEventKind.RepairCargoTransferred)
                .ToArray();
            TestHarness.Equal(1, transfers.Length, label + " transfer count");
            TestHarness.Equal(
                (long)expectedSource,
                transfers[0].BeforeValue,
                label + " transfer source");
            TestHarness.Equal(
                (long)RepairCargoCustody.Vehicle,
                transfers[0].AfterValue,
                label + " transfer destination");
            TestHarness.Equal(
                expectedKind,
                driver.View.RepairCargoKind,
                label + " loaded kind");
            TestHarness.Equal(
                RepairCargoCustody.Vehicle,
                driver.View.RepairCargoCustody,
                label + " loaded custody");
            TestHarness.Equal(
                1L,
                driver.State.OrdinaryCargoUsedUnits,
                label + " loaded occupancy");
            TestHarness.True(
                !driver.View.IsRepairCargoLoadAvailable,
                label + " interaction remained available");
            TestHarness.Equal(
                sequenceBefore + 1,
                driver.State.NextCommandSequence,
                label + " command sequence");
        }

        private static void AssertCanonicalRoundTrip(
            LastBearingState state,
            bool interactionAvailable,
            RepairCargoCustody expectedCustody,
            string label)
        {
            byte[] canonical = LastBearingCanonicalCodec.Encode(state);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(canonical);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                label + " decode");
            TestHarness.True(
                canonical.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State!)),
                label + " bytes changed");
            LastBearingReadModel restored =
                LastBearingReadModel.FromState(decoded.State!);
            TestHarness.Equal(
                expectedCustody,
                restored.RepairCargoCustody,
                label + " custody");
            TestHarness.Equal(
                interactionAvailable,
                restored.IsRepairCargoLoadAvailable,
                label + " interaction availability");
        }

        private static void AssertRejectedAtomically(
            LastBearingState state,
            LastBearingCommand command,
            string expectedCode,
            string label)
        {
            byte[] canonicalBefore = LastBearingCanonicalCodec.Encode(state);
            long sequenceBefore = state.NextCommandSequence;
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new[] { command }),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
            TestHarness.True(
                canonicalBefore.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                label + " mutated canonical state");
            TestHarness.Equal(
                sequenceBefore,
                state.NextCommandSequence,
                label + " changed command sequence");
        }
    }
}
