#nullable enable

using System;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class OwnershipTests
    {
        public static void Run(TestHarness harness)
        {
            harness.Run("ownership transaction is not a public mutation surface", TransactionIsInternal);
            harness.Run("repair cargo cannot appear through an invalid transfer", PhantomTransferFails);
            harness.Run("repair cargo cannot be stranded after transfer", StrandedRepairCargoFails);
            harness.Run("initial ownership state satisfies all invariants", InitialOwnershipIsExact);
        }

        private static void TransactionIsInternal()
        {
            Type transactionType = typeof(LastBearingOwnershipTransaction);
            TestHarness.True(
                transactionType.IsNotPublic,
                "ownership transaction type is publicly exported");
            TestHarness.True(
                transactionType.GetMethod(
                    "TransferRepairCargo",
                    BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly) == null,
                "repair transfer method is publicly exported");
        }

        private static void PhantomTransferFails()
        {
            LastBearingState initial = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2011);
            TestHarness.Throws<InvalidOperationException>(
                () => LastBearingOwnershipTransaction.TransferRepairCargo(
                    initial,
                    RepairCargoKind.CeramicBearing,
                    RepairCargoCustody.Vehicle,
                    RepairCargoCustody.Turbine),
                "phantom bearing transfer was accepted");
        }

        private static void StrandedRepairCargoFails()
        {
            LastBearingState initial = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2011);

            var bearingBuilder = new LastBearingStateBuilder(initial);
            LastBearingOwnershipTransaction.CreateRepairCargo(
                bearingBuilder,
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Vehicle);
            bearingBuilder.DepotBearingDisposition =
                DepotBearingDisposition.TakenBySasha;
            LastBearingState bearingInVehicle = bearingBuilder.Build();
            InvalidOperationException bearingError =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingOwnershipTransaction.TransferRepairCargo(
                        bearingInVehicle,
                        RepairCargoKind.CeramicBearing,
                        RepairCargoCustody.Vehicle,
                        RepairCargoCustody.Turbine),
                    "bearing transfer left a failing turbine");
            TestHarness.Equal(
                "LAST_BEARING_UNAPPLIED_REPAIR_CARGO_INVALID",
                bearingError.Message,
                "bearing invariant error");

            var sleeveBuilder = new LastBearingStateBuilder(initial);
            LastBearingOwnershipTransaction.CreateRepairCargo(
                sleeveBuilder,
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Vehicle);
            sleeveBuilder.DepotBearingDisposition =
                DepotBearingDisposition.FactionHeld;
            LastBearingState sleeveInVehicle = sleeveBuilder.Build();
            InvalidOperationException sleeveError =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingOwnershipTransaction.TransferRepairCargo(
                        sleeveInVehicle,
                        RepairCargoKind.FieldSleeve,
                        RepairCargoCustody.Vehicle,
                        RepairCargoCustody.Consumed),
                    "sleeve transfer left a failing turbine");
            TestHarness.Equal(
                "LAST_BEARING_UNAPPLIED_REPAIR_CARGO_INVALID",
                sleeveError.Message,
                "sleeve invariant error");
        }

        private static void InitialOwnershipIsExact()
        {
            LastBearingState initial = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                2011);
            LastBearingInvariants.Validate(initial);
            TestHarness.Equal(RepairCargoKind.None, initial.RepairCargoKind, "repair cargo");
            TestHarness.Equal(RepairCargoCustody.None, initial.RepairCargoCustody, "repair custody");
            TestHarness.Equal(HeavyCargoKind.None, initial.HeavyCargoKind, "heavy cargo");
            TestHarness.Equal(LiquidCargoKind.None, initial.LiquidCargoKind, "liquid cargo");
            TestHarness.Equal(
                DepotBearingDisposition.AtDepot,
                initial.DepotBearingDisposition,
                "initial bearing location");
        }
    }
}
