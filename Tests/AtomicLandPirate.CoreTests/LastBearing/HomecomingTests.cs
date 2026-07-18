#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class HomecomingTests
    {
        private static readonly EncounterChoice[] Choices =
        {
            EncounterChoice.Cooperate,
            EncounterChoice.TakeBearing,
        };

        public static void RunCore(TestHarness harness)
        {
            harness.Run(
                "homecoming check-in is the exact one-step command pair",
                CheckInIsExactOneStepPair);
            harness.Run(
                "homecoming composite failures preserve original bytes",
                CompositeFailuresAreAtomic);
            harness.Run(
                "homecoming commands reject early stale and wrong identity",
                InvalidCommandsFailAtomically);
            harness.Run(
                "homecoming replays do not duplicate accepted effects",
                ReplaysDoNotDuplicateEffects);
            harness.Run(
                "homecoming mechanics match across compositions and outcomes",
                CompositionsShareBothOutcomes);
        }

        public static void RunSave(TestHarness harness, string repoRoot)
        {
            harness.Run(
                "homecoming arrival finalized and repaired saves round trip",
                () => CheckpointsRoundTripExactly(repoRoot));
        }

        private static void CheckInIsExactOneStepPair()
        {
            foreach (EncounterChoice choice in Choices)
            {
                Journey journey = ReachArrival(
                    ColonyComposition.HumanOnly,
                    choice,
                    Seed(choice));
                LastBearingState arrival = journey.Arrival;
                LastBearingTickResult first = CheckIn(arrival);
                LastBearingTickResult second = CheckIn(arrival);
                string label = Label(choice);

                TestHarness.True(
                    LastBearingCanonicalCodec.Encode(first.State).SequenceEqual(
                        LastBearingCanonicalCodec.Encode(second.State)),
                    label + " one-step check-in bytes differ");
                TestHarness.Equal(
                    EventSignature(first.DomainEvents),
                    EventSignature(second.DomainEvents),
                    label + " one-step check-in events differ");
                TestHarness.Equal(
                    arrival.GlobalTick + 1,
                    first.State.GlobalTick,
                    label + " check-in tick count");
                TestHarness.Equal(
                    arrival.NextCommandSequence + 2,
                    first.State.NextCommandSequence,
                    label + " check-in sequence count");
                TestHarness.Equal(
                    ExpeditionPhase.AtHome,
                    first.State.ExpeditionPhase,
                    label + " check-in expedition phase");
                TestHarness.Equal(
                    TransactionPhase.Finalized,
                    first.State.TransactionPhase,
                    label + " check-in transaction phase");
                TestHarness.Equal(
                    arrival.TransactionId,
                    first.State.TransactionId,
                    label + " check-in transaction id");
                TestHarness.Equal(
                    arrival.TransactionFingerprint,
                    first.State.TransactionFingerprint,
                    label + " check-in fingerprint");
                TestHarness.Equal(
                    arrival.RepairCargoKind,
                    first.State.RepairCargoKind,
                    label + " check-in repair kind");
                TestHarness.Equal(
                    RepairCargoCustody.Vehicle,
                    first.State.RepairCargoCustody,
                    label + " check-in repair custody");
                TestHarness.Equal(
                    arrival.OrdinaryCargoUsedUnits,
                    first.State.OrdinaryCargoUsedUnits,
                    label + " check-in ordinary cargo occupancy");
                TestHarness.Equal(
                    TurbineCondition.Failing,
                    first.State.TurbineCondition,
                    label + " check-in turbine condition");

                int credited = EventIndex(
                    first.DomainEvents,
                    LastBearingEventKind.CityReturnCredited);
                int finalized = EventIndex(
                    first.DomainEvents,
                    LastBearingEventKind.ExpeditionTransactionFinalized);
                TestHarness.True(
                    credited >= 0 && finalized == credited + 1,
                    label + " credit/finalize event order");
                TestHarness.Equal(
                    arrival.NextCommandSequence,
                    first.DomainEvents[credited].CommandSequence,
                    label + " credit command sequence");
                TestHarness.Equal(
                    arrival.NextCommandSequence + 1,
                    first.DomainEvents[finalized].CommandSequence,
                    label + " finalize command sequence");
                TestHarness.Equal(
                    arrival.TransactionId,
                    first.DomainEvents[credited].SubjectId,
                    label + " credit identity");
                TestHarness.Equal(
                    arrival.TransactionId,
                    first.DomainEvents[finalized].SubjectId,
                    label + " finalize identity");
            }
        }

        private static void CompositeFailuresAreAtomic()
        {
            foreach (EncounterChoice choice in Choices)
            {
                LastBearingState arrival = ReachArrival(
                    ColonyComposition.HumanOnly,
                    choice,
                    Seed(choice) + 10).Arrival;
                long sequence = arrival.NextCommandSequence;
                string id = TransactionId(arrival);
                string fingerprint = Fingerprint(arrival);
                string label = Label(choice);

                Reject(
                    arrival,
                    new LastBearingCommand[]
                    {
                        new FinalizeExpeditionTransactionCommand(
                            sequence,
                            id,
                            fingerprint),
                        new CreditCityReturnCommand(
                            sequence + 1,
                            id,
                            fingerprint),
                    },
                    "LAST_BEARING_TRANSACTION_NOT_CITY_CREDITED",
                    label + " reversed pair");
                Reject(
                    arrival,
                    new LastBearingCommand[]
                    {
                        new CreditCityReturnCommand(
                            sequence,
                            id,
                            fingerprint),
                        new FinalizeExpeditionTransactionCommand(
                            sequence + 2,
                            id,
                            fingerprint),
                    },
                    "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                    label + " bad second sequence");
                Reject(
                    arrival,
                    new LastBearingCommand[]
                    {
                        new CreditCityReturnCommand(
                            sequence,
                            id,
                            fingerprint),
                        new FinalizeExpeditionTransactionCommand(
                            sequence + 1,
                            "tx:vgr11:wrong",
                            "fp:vgr11:wrong"),
                    },
                    "LAST_BEARING_TRANSACTION_FINGERPRINT_MISMATCH",
                    label + " bad second identity");
            }
        }

        private static void InvalidCommandsFailAtomically()
        {
            Journey journey = ReachArrival(
                ColonyComposition.HumanOnly,
                EncounterChoice.TakeBearing,
                4311);
            LastBearingState returning = journey.Returning;
            LastBearingState arrival = journey.Arrival;
            string id = TransactionId(arrival);
            string fingerprint = Fingerprint(arrival);

            Reject(
                returning,
                One(new CreditCityReturnCommand(
                    returning.NextCommandSequence,
                    id,
                    fingerprint)),
                "LAST_BEARING_CITY_RETURN_NOT_READY",
                "early returning credit");
            Reject(
                arrival,
                One(new FinalizeExpeditionTransactionCommand(
                    arrival.NextCommandSequence,
                    id,
                    fingerprint)),
                "LAST_BEARING_TRANSACTION_NOT_CITY_CREDITED",
                "early finalize");
            Reject(
                arrival,
                One(new InstallTurbineRepairCommand(
                    arrival.NextCommandSequence)),
                "LAST_BEARING_TURBINE_REPAIR_NOT_READY",
                "early install");
            Reject(
                arrival,
                One(new CreditCityReturnCommand(
                    arrival.NextCommandSequence,
                    "tx:vgr11:wrong",
                    fingerprint)),
                "LAST_BEARING_TRANSACTION_FINGERPRINT_MISMATCH",
                "wrong credit transaction id");
            Reject(
                arrival,
                One(new CreditCityReturnCommand(
                    arrival.NextCommandSequence,
                    id,
                    "fp:vgr11:wrong")),
                "LAST_BEARING_TRANSACTION_FINGERPRINT_MISMATCH",
                "wrong credit fingerprint");
            Reject(
                arrival,
                One(new CreditCityReturnCommand(
                    arrival.NextCommandSequence + 1,
                    id,
                    fingerprint)),
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                "stale credit sequence");

            LastBearingState credited = new LastBearingKernel().Step(
                arrival,
                One(new CreditCityReturnCommand(
                    arrival.NextCommandSequence,
                    id,
                    fingerprint))).State;
            Reject(
                credited,
                One(new FinalizeExpeditionTransactionCommand(
                    credited.NextCommandSequence,
                    "tx:vgr11:wrong",
                    fingerprint)),
                "LAST_BEARING_TRANSACTION_FINGERPRINT_MISMATCH",
                "wrong finalize identity");
            Reject(
                credited,
                One(new FinalizeExpeditionTransactionCommand(
                    credited.NextCommandSequence + 1,
                    id,
                    fingerprint)),
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                "stale finalize sequence");
            Reject(
                credited,
                One(new InstallTurbineRepairCommand(
                    credited.NextCommandSequence)),
                "LAST_BEARING_TURBINE_REPAIR_NOT_READY",
                "early credited install");

            LastBearingState finalized = CheckIn(arrival).State;
            Reject(
                finalized,
                One(new InstallTurbineRepairCommand(
                    finalized.NextCommandSequence + 1)),
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                "stale install sequence");
        }

        private static void ReplaysDoNotDuplicateEffects()
        {
            foreach (EncounterChoice choice in Choices)
            {
                LastBearingState arrival = ReachArrival(
                    ColonyComposition.HumanOnly,
                    choice,
                    Seed(choice) + 20).Arrival;
                LastBearingState finalized = CheckIn(arrival).State;
                var kernel = new LastBearingKernel();
                LastBearingState paused = kernel.Step(
                    finalized,
                    One(new SetPauseCommand(
                        finalized.NextCommandSequence,
                        isPaused: true))).State;
                string id = TransactionId(paused);
                string fingerprint = Fingerprint(paused);
                string label = Label(choice);

                LastBearingTickResult creditReplay = kernel.Step(
                    paused,
                    One(new CreditCityReturnCommand(
                        paused.NextCommandSequence,
                        id,
                        fingerprint)));
                AssertReplay(paused, creditReplay, label + " credit replay");

                LastBearingTickResult finalizeReplay = kernel.Step(
                    creditReplay.State,
                    One(new FinalizeExpeditionTransactionCommand(
                        creditReplay.State.NextCommandSequence,
                        id,
                        fingerprint)));
                AssertReplay(
                    creditReplay.State,
                    finalizeReplay,
                    label + " finalize replay");

                LastBearingTickResult installed = kernel.Step(
                    finalizeReplay.State,
                    One(new InstallTurbineRepairCommand(
                        finalizeReplay.State.NextCommandSequence)));
                TestHarness.Equal(
                    1,
                    EventCount(
                        installed.DomainEvents,
                        LastBearingEventKind.TurbineRepaired),
                    label + " accepted repair event count");

                LastBearingTickResult installReplay = kernel.Step(
                    installed.State,
                    One(new InstallTurbineRepairCommand(
                        installed.State.NextCommandSequence)));
                AssertReplay(
                    installed.State,
                    installReplay,
                    label + " install replay");
                AssertRepair(installReplay.State, choice, label);
            }
        }

        private static void CompositionsShareBothOutcomes()
        {
            foreach (EncounterChoice choice in Choices)
            {
                Outcome human = Complete(
                    ColonyComposition.HumanOnly,
                    choice,
                    Seed(choice) + 30);
                Outcome robot = Complete(
                    ColonyComposition.RobotOnly,
                    choice,
                    Seed(choice) + 30);
                Outcome mixed = Complete(
                    ColonyComposition.Mixed,
                    choice,
                    Seed(choice) + 30);
                string label = Label(choice);
                string expected =
                    LastBearingCanonicalCodec.ComputeMechanicalSha256(
                        human.State);

                TestHarness.Equal(
                    expected,
                    LastBearingCanonicalCodec.ComputeMechanicalSha256(
                        robot.State),
                    label + " robot mechanics");
                TestHarness.Equal(
                    expected,
                    LastBearingCanonicalCodec.ComputeMechanicalSha256(
                        mixed.State),
                    label + " mixed mechanics");
                TestHarness.Equal(
                    EventSignature(human.CheckInEvents),
                    EventSignature(robot.CheckInEvents),
                    label + " robot check-in events");
                TestHarness.Equal(
                    EventSignature(human.CheckInEvents),
                    EventSignature(mixed.CheckInEvents),
                    label + " mixed check-in events");
                TestHarness.Equal(
                    EventSignature(human.InstallEvents),
                    EventSignature(robot.InstallEvents),
                    label + " robot install events");
                TestHarness.Equal(
                    EventSignature(human.InstallEvents),
                    EventSignature(mixed.InstallEvents),
                    label + " mixed install events");
                AssertRepair(human.State, choice, label + " human");
                AssertRepair(robot.State, choice, label + " robot");
                AssertRepair(mixed.State, choice, label + " mixed");
            }
        }

        private static void CheckpointsRoundTripExactly(string repoRoot)
        {
            foreach (EncounterChoice choice in Choices)
            {
                Journey journey = ReachArrival(
                    ColonyComposition.HumanOnly,
                    choice,
                    Seed(choice) + 40);
                string label = Label(choice);
                LastBearingState arrival = RoundTrip(
                    repoRoot,
                    label + "-arrival",
                    journey.Arrival);
                TestHarness.Equal(
                    ExpeditionPhase.Returned,
                    arrival.ExpeditionPhase,
                    label + " restored arrival phase");
                TestHarness.Equal(
                    TransactionPhase.ReturnPending,
                    arrival.TransactionPhase,
                    label + " restored arrival transaction");
                TestHarness.Equal(
                    RepairCargoCustody.Vehicle,
                    arrival.RepairCargoCustody,
                    label + " restored arrival custody");

                LastBearingState finalized = CheckIn(journey.Arrival).State;
                LastBearingState restoredFinalized = RoundTrip(
                    repoRoot,
                    label + "-finalized",
                    finalized);
                TestHarness.Equal(
                    ExpeditionPhase.AtHome,
                    restoredFinalized.ExpeditionPhase,
                    label + " restored finalized phase");
                TestHarness.Equal(
                    TransactionPhase.Finalized,
                    restoredFinalized.TransactionPhase,
                    label + " restored finalized transaction");
                TestHarness.Equal(
                    RepairCargoCustody.Vehicle,
                    restoredFinalized.RepairCargoCustody,
                    label + " restored finalized custody");
                TestHarness.Equal(
                    TurbineCondition.Failing,
                    restoredFinalized.TurbineCondition,
                    label + " restored finalized turbine");

                LastBearingState repaired = new LastBearingKernel().Step(
                    finalized,
                    One(new InstallTurbineRepairCommand(
                        finalized.NextCommandSequence))).State;
                LastBearingState restoredRepaired = RoundTrip(
                    repoRoot,
                    label + "-repaired",
                    repaired);
                AssertRepair(
                    restoredRepaired,
                    choice,
                    label + " restored repair");
            }
        }

        private static Journey ReachArrival(
            ColonyComposition composition,
            EncounterChoice choice,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.StartPreparation(
                composition == ColonyComposition.RobotOnly
                    ? ResidentRoster.RobotResidentId
                    : ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            AdvanceUntil(
                driver,
                model => model.PreparationPhase == PreparationPhase.Ready,
                drive: false,
                "preparation");

            string id = "tx:vgr11:" + Label(choice);
            string fingerprint = "fp:vgr11:" + Label(choice);
            driver.Apply(sequence => new PrepareExpeditionTransactionCommand(
                sequence,
                id,
                fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                id,
                fingerprint));
            if (driver.View.ExpeditionPhase == ExpeditionPhase.AtHome)
            {
                driver.Apply(sequence => new DepartExpeditionCommand(sequence));
            }

            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "depot recovery");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new ResolveDepotCommand(sequence, choice));
            driver.Apply(sequence => new LoadDepotRepairCargoCommand(sequence));
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                id,
                fingerprint));
            if (driver.View.ExpeditionPhase == ExpeditionPhase.AtDepot)
            {
                driver.Apply(sequence => new ReturnHomeCommand(sequence));
            }

            LastBearingState returning = driver.State;
            TestHarness.Equal(
                ExpeditionPhase.Returning,
                returning.ExpeditionPhase,
                Label(choice) + " returning fixture");
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "arrival");
            TestHarness.Equal(
                TransactionPhase.ReturnPending,
                driver.State.TransactionPhase,
                Label(choice) + " arrival transaction");
            TestHarness.Equal(
                RepairCargoCustody.Vehicle,
                driver.State.RepairCargoCustody,
                Label(choice) + " arrival custody");
            return new Journey(returning, driver.State);
        }

        private static void AdvanceUntil(
            CoreTestDriver driver,
            Func<LastBearingReadModel, bool> predicate,
            bool drive,
            string label)
        {
            var guard = 0;
            while (!predicate(driver.View) && guard < 1000)
            {
                if (drive)
                {
                    driver.OperateWreckLineIfAvailable();
                    driver.Apply(sequence =>
                        new DriveVehicleCommand(sequence, 1000, 0));
                }
                else
                {
                    driver.Advance(1);
                }

                guard++;
            }

            TestHarness.True(predicate(driver.View), label + " guard exhausted");
        }

        private static LastBearingTickResult CheckIn(LastBearingState arrival)
        {
            long sequence = arrival.NextCommandSequence;
            string id = TransactionId(arrival);
            string fingerprint = Fingerprint(arrival);
            return new LastBearingKernel().Step(
                arrival,
                new LastBearingCommand[]
                {
                    new CreditCityReturnCommand(
                        sequence,
                        id,
                        fingerprint),
                    new FinalizeExpeditionTransactionCommand(
                        sequence + 1,
                        id,
                        fingerprint),
                });
        }

        private static Outcome Complete(
            ColonyComposition composition,
            EncounterChoice choice,
            int worldSeed)
        {
            Journey journey = ReachArrival(composition, choice, worldSeed);
            LastBearingTickResult checkIn = CheckIn(journey.Arrival);
            LastBearingTickResult install = new LastBearingKernel().Step(
                checkIn.State,
                One(new InstallTurbineRepairCommand(
                    checkIn.State.NextCommandSequence)));
            return new Outcome(
                install.State,
                checkIn.DomainEvents,
                install.DomainEvents);
        }

        private static void Reject(
            LastBearingState state,
            IReadOnlyList<LastBearingCommand> commands,
            string expectedCode,
            string label)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(state, commands),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " error");
            TestHarness.True(
                before.SequenceEqual(LastBearingCanonicalCodec.Encode(state)),
                label + " changed original bytes");
        }

        private static void AssertReplay(
            LastBearingState before,
            LastBearingTickResult replay,
            string label)
        {
            TestHarness.Equal(
                PauseCause.Explicit,
                before.PauseCause,
                label + " fixture pause");
            TestHarness.Equal(1, replay.DomainEvents.Count, label + " events");
            TestHarness.Equal(
                LastBearingEventKind.IdempotentReplayAccepted,
                replay.DomainEvents[0].Kind,
                label + " event kind");
            TestHarness.Equal(
                before.NextCommandSequence + 1,
                replay.State.NextCommandSequence,
                label + " next sequence");
            TestHarness.Equal(
                before.GlobalTick + 1,
                replay.State.GlobalTick,
                label + " global tick");

            foreach (PropertyInfo property in typeof(LastBearingState)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name == nameof(LastBearingState.GlobalTick)
                    || property.Name ==
                        nameof(LastBearingState.NextCommandSequence))
                {
                    continue;
                }

                TestHarness.True(
                    Equals(
                        property.GetValue(before),
                        property.GetValue(replay.State)),
                    label + " changed " + property.Name);
            }

            TestHarness.True(
                !LastBearingCanonicalCodec.Encode(before).SequenceEqual(
                    LastBearingCanonicalCodec.Encode(replay.State)),
                label + " incorrectly preserved accepted replay bytes");
        }

        private static LastBearingState RoundTrip(
            string repoRoot,
            string caseName,
            LastBearingState expected)
        {
            byte[] canonical = LastBearingCanonicalCodec.Encode(expected);
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/homecoming",
                caseName);
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }

            Directory.CreateDirectory(parent);
            string profile = Path.Combine(
                parent,
                LastBearingProfileContract.ProfileName);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted = store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                caseName + " persist failed: " + persisted.Code);
            TestHarness.Equal(1UL, persisted.Generation, caseName + " generation");

            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                caseName + " load failed: " + loaded.Code);
            TestHarness.True(
                !loaded.FromLastGood,
                caseName + " unexpectedly loaded last-good");
            TestHarness.True(
                canonical.SequenceEqual(loaded.CanonicalPayload!),
                caseName + " profile bytes changed");

            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                caseName + " decode failed: " + decoded.Code);
            TestHarness.True(
                canonical.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State!)),
                caseName + " decoded bytes changed");
            return decoded.State!;
        }

        private static void AssertRepair(
            LastBearingState state,
            EncounterChoice choice,
            string label)
        {
            if (choice == EncounterChoice.TakeBearing)
            {
                TestHarness.Equal(
                    RepairCargoKind.CeramicBearing,
                    state.RepairCargoKind,
                    label + " repair kind");
                TestHarness.Equal(
                    RepairCargoCustody.Turbine,
                    state.RepairCargoCustody,
                    label + " repair custody");
                TestHarness.Equal(
                    TurbineCondition.BearingRepaired,
                    state.TurbineCondition,
                    label + " turbine");
                TestHarness.Equal(
                    DepotBearingDisposition.InstalledAtTurbine,
                    state.DepotBearingDisposition,
                    label + " bearing disposition");
                return;
            }

            TestHarness.Equal(
                RepairCargoKind.FieldSleeve,
                state.RepairCargoKind,
                label + " repair kind");
            TestHarness.Equal(
                RepairCargoCustody.Consumed,
                state.RepairCargoCustody,
                label + " repair custody");
            TestHarness.Equal(
                TurbineCondition.SleeveRepaired,
                state.TurbineCondition,
                label + " turbine");
        }

        private static LastBearingCommand[] One(LastBearingCommand command)
        {
            return new[] { command };
        }

        private static int EventIndex(
            IReadOnlyList<LastBearingDomainEvent> events,
            LastBearingEventKind kind)
        {
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == kind)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int EventCount(
            IReadOnlyList<LastBearingDomainEvent> events,
            LastBearingEventKind kind)
        {
            var count = 0;
            for (var index = 0; index < events.Count; index++)
            {
                if (events[index].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static string EventSignature(
            IReadOnlyList<LastBearingDomainEvent> events)
        {
            return string.Join(
                "|",
                events.Select(domainEvent => string.Join(
                    ":",
                    domainEvent.Kind,
                    domainEvent.Cause,
                    domainEvent.GlobalTick,
                    domainEvent.DomainTick,
                    domainEvent.CommandSequence,
                    domainEvent.SubjectId,
                    domainEvent.BeforeValue,
                    domainEvent.AfterValue)));
        }

        private static string TransactionId(LastBearingState state)
        {
            return state.TransactionId
                ?? throw new InvalidOperationException(
                    "homecoming transaction id is absent");
        }

        private static string Fingerprint(LastBearingState state)
        {
            return state.TransactionFingerprint
                ?? throw new InvalidOperationException(
                    "homecoming fingerprint is absent");
        }

        private static int Seed(EncounterChoice choice)
        {
            return choice == EncounterChoice.TakeBearing ? 4112 : 4111;
        }

        private static string Label(EncounterChoice choice)
        {
            return choice == EncounterChoice.TakeBearing
                ? "ceramic"
                : "sleeve";
        }

        private sealed class Journey
        {
            internal Journey(
                LastBearingState returning,
                LastBearingState arrival)
            {
                Returning = returning;
                Arrival = arrival;
            }

            internal LastBearingState Returning { get; }

            internal LastBearingState Arrival { get; }
        }

        private sealed class Outcome
        {
            internal Outcome(
                LastBearingState state,
                IReadOnlyList<LastBearingDomainEvent> checkInEvents,
                IReadOnlyList<LastBearingDomainEvent> installEvents)
            {
                State = state;
                CheckInEvents = checkInEvents;
                InstallEvents = installEvents;
            }

            internal LastBearingState State { get; }

            internal IReadOnlyList<LastBearingDomainEvent> CheckInEvents { get; }

            internal IReadOnlyList<LastBearingDomainEvent> InstallEvents { get; }
        }
    }
}
