#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class DepotApproachRecoveryTests
    {
        public static void Run(TestHarness harness)
        {
            harness.Run(
                "both modules stop at the explicit depot recovery gate",
                BothModulesStopAtGate);
            harness.Run(
                "depot recovery rejects early encounter and post-target drive",
                GateRejectsBypassAtomically);
            harness.Run(
                "depot recovery operation preserves arrival and replays deterministically",
                OperationPreservesArrivalAndReplaysDeterministically);
            harness.Run(
                "depot arrival snapshot survives idle and catch-up ticks",
                ArrivalSnapshotSurvivesIdleTicks);
            harness.Run(
                "depot recovery replay survives downstream progression and load",
                ReplaySurvivesProgressionAndLoad);
            harness.Run(
                "depot recovery gate round trips byte exactly",
                GateRoundTripsByteExactly);
        }

        private static void BothModulesStopAtGate()
        {
            foreach (VehicleModule module in new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            })
            {
                CoreTestDriver driver = ReachGate(module, 2031);
                TestHarness.Equal(
                    ExpeditionPhase.Outbound,
                    driver.State.ExpeditionPhase,
                    module + " gate phase");
                TestHarness.Equal(
                    driver.State.RouteTargetTicks,
                    driver.State.RouteProgressTicks,
                    module + " gate progress");
                TestHarness.Equal(
                    TransactionPhase.RoadOwned,
                    driver.State.TransactionPhase,
                    module + " gate transaction phase");
                TestHarness.Equal(
                    EncounterChoice.Unresolved,
                    driver.State.DepotResolution,
                    module + " gate depot resolution");
                TestHarness.Equal(
                    RepairCargoKind.None,
                    driver.State.RepairCargoKind,
                    module + " gate repair cargo");
                TestHarness.True(
                    driver.View.IsDepotApproachRecoveryAvailable,
                    module + " gate unavailable");
                TestHarness.Equal(
                    "operate-depot-recovery-point",
                    driver.View.NextObjective,
                    module + " gate objective");
                TestHarness.True(
                    driver.State.HasArrivalClaimSnapshot,
                    module + " omitted the canonical arrival snapshot");
            }
        }

        private static void GateRejectsBypassAtomically()
        {
            CoreTestDriver early = ReadyForRoad(VehicleModule.WinchAssembly, 2032);
            LastBearingState earlyState = early.State;
            string earlyHash = LastBearingCanonicalCodec.ComputeSha256(earlyState);
            InvalidOperationException earlyError =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        earlyState,
                        new LastBearingCommand[]
                        {
                            new OperateDepotRecoveryPointCommand(
                                earlyState.NextCommandSequence),
                        }),
                    "early depot recovery operation was accepted");
            TestHarness.Equal(
                "LAST_BEARING_DEPOT_RECOVERY_NOT_READY",
                earlyError.Message,
                "early recovery rejection");
            TestHarness.Equal(
                earlyHash,
                LastBearingCanonicalCodec.ComputeSha256(earlyState),
                "early rejection mutated input state");

            CoreTestDriver gate = ReachGate(VehicleModule.WinchAssembly, 2033);
            LastBearingState gateState = gate.State;
            string gateHash = LastBearingCanonicalCodec.ComputeSha256(gateState);
            var kernel = new LastBearingKernel();
            InvalidOperationException driveError =
                TestHarness.Throws<InvalidOperationException>(
                    () => kernel.Step(
                        gateState,
                        new LastBearingCommand[]
                        {
                            new DriveVehicleCommand(
                                gateState.NextCommandSequence,
                                1000,
                                0),
                        }),
                    "post-target drive bypassed depot recovery");
            TestHarness.Equal(
                "LAST_BEARING_DEPOT_RECOVERY_REQUIRED",
                driveError.Message,
                "post-target drive rejection");
            InvalidOperationException encounterError =
                TestHarness.Throws<InvalidOperationException>(
                    () => kernel.Step(
                        gateState,
                        new LastBearingCommand[]
                        {
                            new ResolveDepotCommand(
                                gateState.NextCommandSequence,
                                EncounterChoice.Cooperate),
                        }),
                    "encounter bypassed depot recovery");
            TestHarness.Equal(
                "LAST_BEARING_DEPOT_RESOLUTION_PHASE_INVALID",
                encounterError.Message,
                "pre-recovery encounter rejection");
            TestHarness.Equal(
                gateHash,
                LastBearingCanonicalCodec.ComputeSha256(gateState),
                "gate rejection mutated input state");
        }

        private static void OperationPreservesArrivalAndReplaysDeterministically()
        {
            CoreTestDriver first = ReachGate(VehicleModule.WinchAssembly, 2034);
            CoreTestDriver second = ReachGate(VehicleModule.WinchAssembly, 2034);
            long expectedProgress = first.State.ArrivalFactionClaimProgressMilli;
            FactionClaimState expectedClaimState =
                first.State.ArrivalFactionClaimState;

            LastBearingTickResult firstResult = first.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            LastBearingTickResult secondResult = second.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));

            TestHarness.Equal(
                ExpeditionPhase.AtDepot,
                first.State.ExpeditionPhase,
                "operated gate phase");
            TestHarness.True(
                first.State.HasArrivalClaimSnapshot,
                "operated state omitted arrival snapshot");
            TestHarness.Equal(
                expectedProgress,
                first.State.ArrivalFactionClaimProgressMilli,
                "operation replaced arrival progress snapshot");
            TestHarness.Equal(
                expectedClaimState,
                first.State.ArrivalFactionClaimState,
                "arrival claim state snapshot");
            LastBearingDomainEvent operated = firstResult.DomainEvents.First(
                item => item.Kind ==
                    LastBearingEventKind.DepotRecoveryPointOperated);
            TestHarness.Equal(
                LastBearingEventKind.DepotRecoveryPointOperated,
                firstResult.DomainEvents[0].Kind,
                "recovery operation event ordering");
            TestHarness.Equal(
                LastBearingEventCause.PlayerCommand,
                operated.Cause,
                "recovery operation event cause");
            TestHarness.Equal(
                "world:last-bearing:depot-approach-recovery",
                operated.SubjectId,
                "recovery event subject");
            TestHarness.Equal(
                (long)ExpeditionPhase.Outbound,
                operated.BeforeValue,
                "recovery event before phase");
            TestHarness.Equal(
                (long)ExpeditionPhase.AtDepot,
                operated.AfterValue,
                "recovery event after phase");
            TestHarness.True(
                LastBearingCanonicalCodec.Encode(first.State).SequenceEqual(
                    LastBearingCanonicalCodec.Encode(second.State)),
                "identical recovery schedules diverged");
            TestHarness.True(
                EventsEqual(firstResult, secondResult),
                "identical recovery events diverged");

            long snapshotProgress = first.State.ArrivalFactionClaimProgressMilli;
            LastBearingTickResult replay = first.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            TestHarness.Equal(
                snapshotProgress,
                first.State.ArrivalFactionClaimProgressMilli,
                "replay replaced arrival snapshot");
            TestHarness.Equal(
                1,
                replay.DomainEvents.Count,
                "replay emitted duplicate recovery effects");
            TestHarness.Equal(
                LastBearingEventKind.IdempotentReplayAccepted,
                replay.DomainEvents[0].Kind,
                "recovery replay event");
        }

        private static void ArrivalSnapshotSurvivesIdleTicks()
        {
            CoreTestDriver driver = ReachGate(
                VehicleModule.WinchAssembly,
                2036);
            long arrivalProgress = driver.State.ArrivalFactionClaimProgressMilli;
            FactionClaimState arrivalState = driver.State.ArrivalFactionClaimState;
            long currentProgress = driver.State.FactionClaimProgressMilli;

            driver.Advance(20);

            TestHarness.True(
                driver.View.IsDepotApproachRecoveryAvailable,
                "idle ticks closed the recovery gate");
            TestHarness.True(
                driver.State.FactionClaimProgressMilli > currentProgress,
                "idle ticks did not exercise autonomous faction progress");
            TestHarness.Equal(
                arrivalProgress,
                driver.State.ArrivalFactionClaimProgressMilli,
                "idle ticks replaced the arrival snapshot");
            TestHarness.Equal(
                arrivalState,
                driver.State.ArrivalFactionClaimState,
                "idle ticks replaced the arrival claim state");

            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            TestHarness.Equal(
                arrivalProgress,
                driver.State.ArrivalFactionClaimProgressMilli,
                "recovery operation replaced the arrival snapshot");
            TestHarness.Equal(
                arrivalState,
                driver.State.ArrivalFactionClaimState,
                "recovery operation replaced the arrival claim state");
        }

        private static void ReplaySurvivesProgressionAndLoad()
        {
            CoreTestDriver driver = ReachGate(
                VehicleModule.WinchAssembly,
                2037);
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new ResolveDepotCommand(
                sequence,
                EncounterChoice.Cooperate));

            AssertRecoveryReplay(driver, "resolved depot");

            string transactionId = driver.State.TransactionId!;
            string fingerprint = driver.State.TransactionFingerprint!;
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            TestHarness.Equal(
                ExpeditionPhase.Returning,
                driver.State.ExpeditionPhase,
                "return freeze phase");
            AssertRecoveryReplay(driver, "returning expedition");

            byte[] encoded = LastBearingCanonicalCodec.Encode(driver.State);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(encoded);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "post-recovery state decode failed");
            LastBearingTickResult replayAfterLoad = new LastBearingKernel().Step(
                decoded.State!,
                new LastBearingCommand[]
                {
                    new OperateDepotRecoveryPointCommand(
                        decoded.State!.NextCommandSequence),
                });
            TestHarness.True(
                replayAfterLoad.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                "loaded post-recovery state rejected semantic replay");
        }

        private static void GateRoundTripsByteExactly()
        {
            CoreTestDriver driver = ReachGate(
                VehicleModule.SealedRangeTank,
                2035);
            byte[] encoded = LastBearingCanonicalCodec.Encode(driver.State);
            string hash = LastBearingCanonicalCodec.ComputeSha256(driver.State);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(encoded);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "gate state decode failed");
            TestHarness.True(
                encoded.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State!)),
                "gate state bytes changed after decode");
            TestHarness.Equal(
                hash,
                LastBearingCanonicalCodec.ComputeSha256(decoded.State!),
                "gate state hash changed after decode");
            TestHarness.True(
                LastBearingReadModel.FromState(decoded.State!)
                    .IsDepotApproachRecoveryAvailable,
                "decoded gate state lost availability");
        }

        private static CoreTestDriver ReachGate(
            VehicleModule module,
            int worldSeed)
        {
            CoreTestDriver driver = ReadyForRoad(module, worldSeed);
            int guard = 0;
            while (!driver.View.IsDepotApproachRecoveryAvailable && guard < 1000)
            {
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                guard++;
            }

            TestHarness.True(
                driver.View.IsDepotApproachRecoveryAvailable,
                module + " did not reach depot recovery gate");
            return driver;
        }

        private static CoreTestDriver ReadyForRoad(
            VehicleModule module,
            int worldSeed)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                worldSeed);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                module);
            int guard = 0;
            while (driver.View.PreparationPhase != PreparationPhase.Ready
                   && guard < 1000)
            {
                driver.Advance(1);
                guard++;
            }

            TestHarness.Equal(
                PreparationPhase.Ready,
                driver.View.PreparationPhase,
                module + " preparation");
            const string transactionId = "tx:depot-recovery";
            const string fingerprint = "fp:depot-recovery";
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            if (driver.View.ExpeditionPhase == ExpeditionPhase.AtHome)
            {
                driver.Apply(sequence => new DepartExpeditionCommand(sequence));
            }

            TestHarness.Equal(
                ExpeditionPhase.Outbound,
                driver.View.ExpeditionPhase,
                module + " outbound phase");
            return driver;
        }

        private static bool EventsEqual(
            LastBearingTickResult first,
            LastBearingTickResult second)
        {
            if (first.DomainEvents.Count != second.DomainEvents.Count)
            {
                return false;
            }

            for (int index = 0; index < first.DomainEvents.Count; index++)
            {
                LastBearingDomainEvent left = first.DomainEvents[index];
                LastBearingDomainEvent right = second.DomainEvents[index];
                if (left.Kind != right.Kind
                    || left.Cause != right.Cause
                    || left.GlobalTick != right.GlobalTick
                    || left.DomainTick != right.DomainTick
                    || left.CommandSequence != right.CommandSequence
                    || !string.Equals(
                        left.SubjectId,
                        right.SubjectId,
                        StringComparison.Ordinal)
                    || left.BeforeValue != right.BeforeValue
                    || left.AfterValue != right.AfterValue)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AssertRecoveryReplay(
            CoreTestDriver driver,
            string checkpoint)
        {
            long arrivalProgress = driver.State.ArrivalFactionClaimProgressMilli;
            LastBearingTickResult replay = driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            TestHarness.Equal(
                arrivalProgress,
                driver.State.ArrivalFactionClaimProgressMilli,
                checkpoint + " replay replaced the arrival snapshot");
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.IdempotentReplayAccepted),
                checkpoint + " replay was not accepted idempotently");
        }
    }
}
