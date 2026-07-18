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
            LastBearingState bearingInVehicle =
                ReachLoadedDepotRepair(
                    EncounterChoice.TakeBearing,
                    2111).State;
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

            LastBearingState sleeveInVehicle =
                ReachLoadedDepotRepair(
                    EncounterChoice.Cooperate,
                    2112).State;
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

        private static CoreTestDriver ReachLoadedDepotRepair(
            EncounterChoice choice,
            int worldSeed)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                worldSeed);
            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:ownership:" + worldSeed;
            string fingerprint = "fp:ownership:" + worldSeed;
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
            driver.Apply(sequence =>
                new ResolveDepotCommand(sequence, choice));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            return driver;
        }

        private static void InitialOwnershipIsExact()
        {
            LastBearingState initial = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                2011);
            LastBearingInvariants.Validate(initial);
            TestHarness.Equal(RepairCargoKind.None, initial.RepairCargoKind, "repair cargo");
            TestHarness.Equal(RepairCargoCustody.None, initial.RepairCargoCustody, "repair custody");
            TestHarness.Equal(HeavyCargoKind.PumpRotor, initial.HeavyCargoKind, "heavy cargo");
            TestHarness.Equal(
                HeavyCargoCustody.Depot,
                initial.HeavyCargoCustody,
                "initial pump rotor custody");
            TestHarness.Equal(LiquidCargoKind.None, initial.LiquidCargoKind, "liquid cargo");
            TestHarness.Equal(
                DepotBearingDisposition.AtDepot,
                initial.DepotBearingDisposition,
                "initial bearing location");
        }
    }
}
