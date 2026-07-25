#nullable enable

using System;
using System.Globalization;
using System.Linq;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class RoadSafeLineTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "road safe line is strict and steering alone causes no damage",
                SafeWidthAndProgressDamageAreExact);
            harness.Run(
                "returned-rail brace never erases authored road-edge damage",
                BraceProtectsFixedLossOnly);
        }

        private static void SafeWidthAndProgressDamageAreExact()
        {
            AssertBoundary(
                ColonyComposition.HumanOnly,
                VehicleModule.WinchAssembly,
                direction: 1,
                worldSeed: 3701);
            AssertBoundary(
                ColonyComposition.RobotOnly,
                VehicleModule.SealedRangeTank,
                direction: -1,
                worldSeed: 3702);
        }

        private static void AssertBoundary(
            ColonyComposition composition,
            VehicleModule module,
            int direction,
            int worldSeed)
        {
            int safeLateral = checked(
                direction * LastBearingBalanceV1.RoadSafeHalfWidthMilli);
            int unsafeLateral = checked(safeLateral + direction);
            string label = composition + " " + module + " "
                + direction.ToString(CultureInfo.InvariantCulture);

            CoreTestDriver safe = ReachFirstOutbound(
                composition,
                module,
                worldSeed);
            SteerWithoutProgress(safe, safeLateral, label + " safe");
            long safeCondition = safe.State.VehicleConditionMilli;
            long safeProgress = safe.State.RouteProgressTicks;
            LastBearingTickResult safeAdvance = safe.Apply(sequence =>
                new DriveVehicleCommand(sequence, 1000, 0));
            TestHarness.Equal(
                checked(safeProgress + 1),
                safe.State.RouteProgressTicks,
                label + " safe progress");
            TestHarness.Equal(
                safeCondition,
                safe.State.VehicleConditionMilli,
                label + " exact safe edge caused damage");
            TestHarness.True(
                safeAdvance.DomainEvents.All(item =>
                    item.Kind
                        != LastBearingEventKind.VehicleConditionChanged),
                label + " exact safe edge emitted condition damage");

            CoreTestDriver offRoad = ReachFirstOutbound(
                composition,
                module,
                checked(worldSeed + 10));
            SteerWithoutProgress(
                offRoad,
                unsafeLateral,
                label + " unsafe");
            TestHarness.True(
                Math.Abs(offRoad.View.VehicleLateralMilli)
                    > LastBearingBalanceV1.RoadSafeHalfWidthMilli,
                label + " unsafe warning input");

            for (var tick = 0; tick < 3; tick++)
            {
                long previousCondition =
                    offRoad.State.VehicleConditionMilli;
                long previousProgress = offRoad.State.RouteProgressTicks;
                LastBearingTickResult result = offRoad.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                TestHarness.Equal(
                    checked(previousProgress + 1),
                    offRoad.State.RouteProgressTicks,
                    label + " unsafe progress " + tick);
                TestHarness.Equal(
                    checked(
                        previousCondition
                        - LastBearingBalanceV1
                            .RoadEdgeConditionLossPerProgressTickMilli),
                    offRoad.State.VehicleConditionMilli,
                    label + " unsafe condition " + tick);
                TestHarness.Equal(
                    1,
                    result.DomainEvents.Count(item =>
                        item.Kind
                            == LastBearingEventKind
                                .VehicleConditionChanged
                        && string.Equals(
                            item.SubjectId,
                            "vehicle:sasha:road-edge",
                            StringComparison.Ordinal)),
                    label + " unsafe damage event " + tick);
            }
        }

        private static void BraceProtectsFixedLossOnly()
        {
            var cases = new[]
            {
                (ColonyComposition.HumanOnly, VehicleModule.WinchAssembly),
                (ColonyComposition.RobotOnly, VehicleModule.WinchAssembly),
                (ColonyComposition.Mixed, VehicleModule.WinchAssembly),
                (ColonyComposition.HumanOnly, VehicleModule.SealedRangeTank),
                (ColonyComposition.RobotOnly, VehicleModule.SealedRangeTank),
                (ColonyComposition.Mixed, VehicleModule.SealedRangeTank),
            };

            for (var index = 0; index < cases.Length; index++)
            {
                ColonyComposition composition = cases[index].Item1;
                VehicleModule module = cases[index].Item2;
                string label = composition + " " + module;
                CoreTestDriver driver =
                    ReturnedRailChassisBraceTests
                        .CreateInstalledBraceRepeatReady(
                            composition,
                            module,
                            checked(3720 + index));
                long startingCondition = driver.State.VehicleConditionMilli;
                PrepareRepeat(driver);
                string transactionId = driver.State.TransactionId!;
                string fingerprint = driver.State.TransactionFingerprint!;
                driver.Apply(sequence => new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
                SteerWithoutProgress(
                    driver,
                    checked(
                        LastBearingBalanceV1.RoadSafeHalfWidthMilli + 1),
                    label + " braced unsafe");

                AdvanceUntil(
                    driver,
                    model => model.IsDepotApproachRecoveryAvailable,
                    drive: true,
                    label + " depot approach");
                driver.Apply(sequence =>
                    new OperateDepotRecoveryPointCommand(sequence));
                driver.Apply(sequence => new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
                TestHarness.Equal(
                    0,
                    driver.State.VehicleLateralMilli,
                    label + " return lateral reset");
                SteerWithoutProgress(
                    driver,
                    checked(
                        LastBearingBalanceV1.RoadSafeHalfWidthMilli + 1),
                    label + " braced unsafe return");
                AdvanceUntil(
                    driver,
                    model => model.ExpeditionPhase
                        == ExpeditionPhase.Returned,
                    drive: true,
                    label + " home return");
                driver.Apply(sequence => new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    fingerprint));
                driver.Apply(sequence =>
                    new FinalizeExpeditionTransactionCommand(
                        sequence,
                        transactionId,
                        fingerprint));

                long unbracedFixedLoss =
                    LastBearingBalanceV1.RouteConditionLoss(
                        module,
                        driver.State.RigUpgrade,
                        returnedRailChassisBraceInstalled: false);
                long bracedFixedLoss =
                    LastBearingBalanceV1.RouteConditionLoss(
                        module,
                        driver.State.RigUpgrade,
                        returnedRailChassisBraceInstalled: true);
                long roadEdgeLoss = checked(
                    2
                    * LastBearingBalanceV1.RouteOneWayTicks(module)
                    * LastBearingBalanceV1
                        .RoadEdgeConditionLossPerProgressTickMilli);
                TestHarness.Equal(
                    LastBearingBalanceV1
                        .ReturnedRailChassisBraceProtectionMilli,
                    checked(unbracedFixedLoss - bracedFixedLoss),
                    label + " fixed brace protection");
                TestHarness.Equal(
                    checked(
                        startingCondition
                        - roadEdgeLoss
                        - bracedFixedLoss),
                    driver.State.VehicleConditionMilli,
                    label + " braced road and fixed loss");
                TestHarness.True(
                    roadEdgeLoss > 0,
                    label + " road-edge loss disappeared");
                TestHarness.True(
                    LastBearingRepeatExpedition.IsLineage(driver.State),
                    label + " off-road repeat lineage");

                driver.Apply(sequence =>
                    new ServiceScoutCommand(sequence));
                TestHarness.True(
                    LastBearingRepeatExpedition
                        .CanLaunchFromCompletedReturn(driver.State),
                    label + " serviced repeat lineage");
            }
        }

        private static CoreTestDriver ReachFirstOutbound(
            ColonyComposition composition,
            VehicleModule module,
            int worldSeed)
        {
            var driver = new CoreTestDriver(composition, worldSeed);
            string residentId =
                composition == ColonyComposition.RobotOnly
                    ? ResidentRoster.RobotResidentId
                    : ResidentRoster.HumanResidentId;
            driver.Apply(sequence =>
                new AssignResidentCommand(sequence, residentId));
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            driver.Apply(sequence =>
                new InstallRigUpgradeCommand(
                    sequence,
                    RigUpgrade.PatchworkSkidPlate));
            driver.Apply(sequence =>
                new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.CivicBuffer,
                    module));
            driver.Apply(sequence =>
                new InstallVehicleModuleCommand(sequence, module));
            AdvanceUntil(
                driver,
                model => model.PreparationPhase
                    == PreparationPhase.Ready,
                drive: false,
                "first outbound preparation");

            string suffix = worldSeed.ToString(
                CultureInfo.InvariantCulture);
            string transactionId = "tx:road-safe:" + suffix;
            string fingerprint = "fp:road-safe:" + suffix;
            driver.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            TestHarness.Equal(
                ExpeditionPhase.Outbound,
                driver.State.ExpeditionPhase,
                "first outbound phase");
            return driver;
        }

        private static void PrepareRepeat(CoreTestDriver driver)
        {
            long sequence = driver.State.NextCommandSequence;
            string suffix = sequence.ToString(
                CultureInfo.InvariantCulture);
            driver.Apply(current =>
                new PrepareRepeatExpeditionTransactionCommand(
                    current,
                    driver.State.TransactionId!,
                    driver.State.TransactionFingerprint!,
                    "tx:repeat:" + suffix,
                    "fp:repeat:" + suffix));
        }

        private static void SteerWithoutProgress(
            CoreTestDriver driver,
            int targetLateral,
            string label)
        {
            long condition = driver.State.VehicleConditionMilli;
            long progress = driver.State.RouteProgressTicks;
            int maximumLateralStep = checked(
                1000 / LastBearingBalanceV1.SteeringResponseDivisor);
            while (driver.State.VehicleLateralMilli != targetLateral)
            {
                int remaining = checked(
                    targetLateral - driver.State.VehicleLateralMilli);
                int lateralStep = Math.Sign(remaining)
                    * Math.Min(
                        Math.Abs(remaining),
                        maximumLateralStep);
                int steering = checked(
                    lateralStep
                    * LastBearingBalanceV1.SteeringResponseDivisor);
                LastBearingTickResult result = driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 0, steering));
                TestHarness.Equal(
                    condition,
                    driver.State.VehicleConditionMilli,
                    label + " steering condition");
                TestHarness.Equal(
                    progress,
                    driver.State.RouteProgressTicks,
                    label + " steering progress");
                TestHarness.True(
                    result.DomainEvents.All(item =>
                        item.Kind
                            != LastBearingEventKind
                                .VehicleConditionChanged
                        && item.Kind
                            != LastBearingEventKind.RouteProgressed),
                    label + " steering-only damage or progress event");
            }

            TestHarness.Equal(
                targetLateral,
                driver.View.VehicleLateralMilli,
                label + " lateral state");
        }

        private static void AdvanceUntil(
            CoreTestDriver driver,
            Func<LastBearingReadModel, bool> predicate,
            bool drive,
            string label)
        {
            for (var ticks = 0; ticks < 7000; ticks++)
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
                label + " did not converge");
        }
    }
}
