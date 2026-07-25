#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class RoadHandTests
    {
        public static void Run(TestHarness harness)
        {
            harness.Run(
                "mixed road-hand choices use identical mechanics",
                MixedChoicesUseIdenticalMechanics);
            harness.Run(
                "road-hand choice rejects residents outside the roster",
                WrongRosterFailsAtomically);
            harness.Run(
                "road-hand choice commits once",
                AssignmentCommitsOnce);
            harness.Run(
                "road-hand choice locks after preparation begins",
                AssignmentLocksAfterPreparation);
            harness.Run(
                "road-hand choice survives canonical round trip",
                AssignmentRoundTrips);
        }

        private static void AssignmentCommitsOnce()
        {
            LastBearingState assigned =
                AssignMixed(ResidentRoster.HumanResidentId).State;
            byte[] before = LastBearingCanonicalCodec.Encode(assigned);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        assigned,
                        new LastBearingCommand[]
                        {
                            new AssignResidentCommand(
                                assigned.NextCommandSequence,
                                ResidentRoster.RobotResidentId),
                        }),
                    "a second road hand replaced the committed choice");

            TestHarness.Equal(
                "LAST_BEARING_ROAD_HAND_ALREADY_ASSIGNED",
                error.Message,
                "second road-hand failure code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(assigned)),
                "second road-hand rejection mutated the input state");
        }

        private static void MixedChoicesUseIdenticalMechanics()
        {
            LastBearingTickResult human = AssignMixed(
                ResidentRoster.HumanResidentId);
            LastBearingTickResult robot = AssignMixed(
                ResidentRoster.RobotResidentId);

            TestHarness.Equal(
                ResidentRoster.HumanResidentId,
                human.ReadModel.AssignedResidentId,
                "human road hand");
            TestHarness.Equal(
                ResidentRoster.RobotResidentId,
                robot.ReadModel.AssignedResidentId,
                "robot road hand");
            TestHarness.Equal(
                MechanicalProjection(human.ReadModel),
                MechanicalProjection(robot.ReadModel),
                "road-hand mechanics");
            AssertRoadHandEvent(human);
            AssertRoadHandEvent(robot);
        }

        private static void WrongRosterFailsAtomically()
        {
            LastBearingState state =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    2011);
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new LastBearingCommand[]
                        {
                            new AssignResidentCommand(
                                state.NextCommandSequence,
                                ResidentRoster.RobotResidentId),
                        }),
                    "robot road hand was accepted into a human-only roster");

            TestHarness.Equal(
                "LAST_BEARING_ASSIGNED_RESIDENT_NOT_IN_ROSTER",
                error.Message,
                "wrong-roster failure code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                "wrong-roster rejection mutated the input state");
        }

        private static void AssignmentLocksAfterPreparation()
        {
            var driver = new CoreTestDriver(ColonyComposition.Mixed);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            LastBearingState prepared = driver.State;
            byte[] before = LastBearingCanonicalCodec.Encode(prepared);
            var kernel = new LastBearingKernel();

            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => kernel.Step(
                        prepared,
                        new LastBearingCommand[]
                        {
                            new AssignResidentCommand(
                                prepared.NextCommandSequence,
                                ResidentRoster.RobotResidentId),
                        }),
                    "road hand changed after preparation began");
            TestHarness.Equal(
                "LAST_BEARING_ROAD_HAND_ASSIGNMENT_PHASE_INVALID",
                error.Message,
                "late assignment failure code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(prepared)),
                "late assignment rejection mutated the input state");

            LastBearingTickResult replay = kernel.Step(
                prepared,
                new LastBearingCommand[]
                {
                    new AssignResidentCommand(
                        prepared.NextCommandSequence,
                        ResidentRoster.HumanResidentId),
                });
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind ==
                        LastBearingEventKind.IdempotentReplayAccepted),
                "same road hand did not replay idempotently");
        }

        private static void AssignmentRoundTrips()
        {
            LastBearingState assigned =
                AssignMixed(ResidentRoster.RobotResidentId).State;
            byte[] encoded = LastBearingCanonicalCodec.Encode(assigned);
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(encoded);

            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "assigned road hand did not decode");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                decoded.State!.SchemaVersion,
                "road-hand round trip changed schema");
            TestHarness.Equal(
                ResidentRoster.RobotResidentId,
                decoded.State.AssignedResidentId,
                "road-hand round trip changed identity");
            TestHarness.True(
                encoded.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(decoded.State)),
                "road-hand canonical bytes changed after decode");
        }

        private static LastBearingTickResult AssignMixed(string stableId)
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2011);
            return new LastBearingKernel().Step(
                initial,
                new LastBearingCommand[]
                {
                    new AssignResidentCommand(
                        initial.NextCommandSequence,
                        stableId),
                });
        }

        private static void AssertRoadHandEvent(LastBearingTickResult result)
        {
            LastBearingDomainEvent assigned = result.DomainEvents.Single(
                item => item.Kind == LastBearingEventKind.ResidentAssigned);
            TestHarness.Equal(
                "expedition-slot:road-hand",
                assigned.SubjectId,
                "road-hand event subject");
        }

        private static string MechanicalProjection(
            LastBearingReadModel model)
        {
            return string.Join(
                "|",
                model.GlobalTick,
                model.SettlementTick,
                model.FactionTick,
                model.CrisisTick,
                model.RoadTick,
                model.WaterMilli,
                model.PartsUnits,
                model.FuelUnits,
                model.PreparationChoice,
                model.PreparationPhase,
                model.VehicleModule,
                model.ExpeditionPhase,
                model.TransactionPhase,
                model.NextObjective);
        }
    }
}
