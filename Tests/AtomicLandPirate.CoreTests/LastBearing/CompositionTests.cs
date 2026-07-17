#nullable enable

using System;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class CompositionTests
    {
        public static void Run(TestHarness harness)
        {
            harness.Run("composition rosters are exact typed sets", ExactRosters);
            harness.Run("composition remains mechanically identical", MechanicalEquality);
            harness.Run("typed rosters survive canonical codec", RosterRoundTrip);
        }

        private static void ExactRosters()
        {
            CoreTestDriver human = new CoreTestDriver(ColonyComposition.HumanOnly);
            CoreTestDriver robot = new CoreTestDriver(ColonyComposition.RobotOnly);
            CoreTestDriver mixed = new CoreTestDriver(ColonyComposition.Mixed);
            TestHarness.Equal(1, human.View.Residents.Count, "human roster size");
            TestHarness.Equal(
                ResidentRoster.HumanResidentId,
                human.View.Residents[0].StableId,
                "human resident id");
            TestHarness.Equal(1, robot.View.Residents.Count, "robot roster size");
            TestHarness.Equal(
                ResidentRoster.RobotResidentId,
                robot.View.Residents[0].StableId,
                "robot resident id");
            TestHarness.Equal(2, mixed.View.Residents.Count, "mixed roster size");
            TestHarness.Equal(
                ResidentRoster.HumanResidentId,
                mixed.View.Residents[0].StableId,
                "mixed first resident id");
            TestHarness.Equal(
                ResidentRoster.RobotResidentId,
                mixed.View.Residents[1].StableId,
                "mixed second resident id");
        }

        private static void MechanicalEquality()
        {
            CoreTestDriver human = Prepared(
                ColonyComposition.HumanOnly,
                ResidentRoster.HumanResidentId);
            CoreTestDriver robot = Prepared(
                ColonyComposition.RobotOnly,
                ResidentRoster.RobotResidentId);
            CoreTestDriver mixed = Prepared(
                ColonyComposition.Mixed,
                ResidentRoster.HumanResidentId);
            string humanProjection = MechanicalProjection(human.View);
            TestHarness.Equal(humanProjection, MechanicalProjection(robot.View), "robot mechanics");
            TestHarness.Equal(humanProjection, MechanicalProjection(mixed.View), "mixed mechanics");
        }

        private static void RosterRoundTrip()
        {
            foreach (ColonyComposition composition in Enum.GetValues(typeof(ColonyComposition)))
            {
                LastBearingState original =
                    LastBearingScenarioFactory.CreateInitial(composition, 2011);
                byte[] encoded = LastBearingCanonicalCodec.Encode(original);
                LastBearingDecodeResult decoded = LastBearingCanonicalCodec.TryDecode(encoded);
                TestHarness.True(decoded.Succeeded && decoded.State != null, "decode failed");
                LastBearingTickResult result = new LastBearingKernel().Step(
                    decoded.State!,
                    Array.Empty<LastBearingCommand>());
                string[] expected = ResidentRoster.CreateForComposition(composition)
                    .Residents.Select(resident => resident.StableId).ToArray();
                string[] actual = result.ReadModel.Residents
                    .Select(resident => resident.StableId).ToArray();
                TestHarness.True(expected.SequenceEqual(actual), "roster substituted on decode");
            }
        }

        private static CoreTestDriver Prepared(
            ColonyComposition composition,
            string residentId)
        {
            var driver = new CoreTestDriver(composition);
            driver.StartPreparation(
                residentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            driver.Advance(40);
            return driver;
        }

        private static string MechanicalProjection(LastBearingReadModel view)
        {
            return string.Join(
                "|",
                view.GlobalTick,
                view.SettlementTick,
                view.FactionTick,
                view.CrisisTick,
                view.RoadTick,
                view.WaterMilli,
                view.WaterTrendMilliPerSettlementTick,
                view.PartsUnits,
                view.FuelUnits,
                view.TurbineCondition,
                view.PreparationChoice,
                view.PreparationPhase,
                view.PlannedModule,
                view.VehicleModule,
                view.ExpeditionPhase,
                view.TransactionPhase,
                view.RouteKind,
                view.RouteProgressTicks,
                view.RouteTargetTicks,
                view.VehicleLateralMilli,
                view.VehicleConditionMilli,
                view.FactionClaimProgressMilli,
                view.FactionClaimState,
                view.DepotControl,
                view.NextCityDecision,
                view.PauseCause,
                view.NextObjective);
        }
    }
}
