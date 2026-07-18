#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    internal static class LastBearingOwnershipTransaction
    {
        internal static LastBearingState TransferRepairCargo(
            LastBearingState state,
            RepairCargoKind expectedKind,
            RepairCargoCustody expectedCustody,
            RepairCargoCustody nextCustody)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var builder = new LastBearingStateBuilder(state);
            TransferRepairCargo(
                builder,
                expectedKind,
                expectedCustody,
                nextCustody);
            return builder.Build();
        }

        internal static void CreateRepairCargo(
            LastBearingStateBuilder builder,
            RepairCargoKind kind,
            RepairCargoCustody custody)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (builder.RepairCargoKind != RepairCargoKind.None
                || builder.RepairCargoCustody != RepairCargoCustody.None
                || kind == RepairCargoKind.None)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_REPAIR_CARGO_CREATE_INVALID");
            }

            RequireLegalRepairCustody(kind, custody);
            builder.RepairCargoKind = kind;
            builder.RepairCargoCustody = custody;
        }

        internal static void TransferRepairCargo(
            LastBearingStateBuilder builder,
            RepairCargoKind expectedKind,
            RepairCargoCustody expectedCustody,
            RepairCargoCustody nextCustody)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (builder.RepairCargoKind != expectedKind
                || builder.RepairCargoCustody != expectedCustody)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_REPAIR_CARGO_OWNERSHIP_MISMATCH");
            }

            RequireLegalRepairTransition(
                expectedKind,
                expectedCustody,
                nextCustody);
            builder.RepairCargoCustody = nextCustody;
        }

        internal static void RecoverHeavyCargoToVehicle(
            LastBearingStateBuilder builder)
        {
            if (builder.HeavyCargoKind != HeavyCargoKind.PumpRotor
                || builder.HeavyCargoCustody != HeavyCargoCustody.Depot
                || builder.VehicleModule != VehicleModule.WinchAssembly
                || builder.TowSlotsUsed != 0
                || builder.TowSlots < 1)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_HEAVY_CARGO_RECOVERY_INVALID");
            }

            builder.HeavyCargoCustody = HeavyCargoCustody.Vehicle;
            builder.TowSlotsUsed = 1;
        }

        internal static void TransferHeavyCargoToSettlement(
            LastBearingStateBuilder builder)
        {
            if (builder.HeavyCargoKind == HeavyCargoKind.PumpRotor
                && builder.HeavyCargoCustody == HeavyCargoCustody.Depot)
            {
                return;
            }

            if (builder.HeavyCargoKind != HeavyCargoKind.PumpRotor
                || builder.HeavyCargoCustody != HeavyCargoCustody.Vehicle)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_HEAVY_CARGO_OWNERSHIP_MISMATCH");
            }

            builder.HeavyCargoCustody = HeavyCargoCustody.Settlement;
        }

        internal static void InstallHeavyCargoAtAuxiliaryPump(
            LastBearingStateBuilder builder)
        {
            if (builder.HeavyCargoKind != HeavyCargoKind.PumpRotor
                || builder.HeavyCargoCustody != HeavyCargoCustody.Settlement
                || builder.TowSlotsUsed != 1)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_AUXILIARY_PUMP_CARGO_INVALID");
            }

            builder.HeavyCargoCustody =
                HeavyCargoCustody.InstalledAtAuxiliaryPump;
            builder.TowSlotsUsed = 0;
        }

        internal static void CreateLiquidCargo(
            LastBearingStateBuilder builder,
            LiquidCargoKind kind,
            long quantityMilli)
        {
            if (builder.LiquidCargoKind != LiquidCargoKind.None
                || builder.LiquidCargoCustody != LiquidCargoCustody.None
                || builder.LiquidCargoQuantityMilli != 0
                || builder.VehicleModule != VehicleModule.SealedRangeTank
                || kind == LiquidCargoKind.None
                || quantityMilli <= 0
                || quantityMilli > builder.LiquidCapacityMilli)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_LIQUID_CARGO_CREATE_INVALID");
            }

            builder.LiquidCargoKind = kind;
            builder.LiquidCargoQuantityMilli = quantityMilli;
            builder.LiquidCargoCustody = LiquidCargoCustody.Vehicle;
        }

        internal static void TransferLiquidCargoToSettlement(
            LastBearingStateBuilder builder)
        {
            if (builder.LiquidCargoKind == LiquidCargoKind.None)
            {
                return;
            }

            if (builder.LiquidCargoCustody != LiquidCargoCustody.Vehicle)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_LIQUID_CARGO_OWNERSHIP_MISMATCH");
            }

            builder.LiquidCargoCustody = LiquidCargoCustody.Settlement;
        }

        internal static void CreateSpareBearingLotAtWorkshopOutput(
            LastBearingStateBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (builder.SpareBearingRecipe
                    != SpareBearingRecipe.SpareBearingOneGoodBatch
                || builder.SpareBearingBatchPhase
                    != SpareBearingBatchPhase.InProgress
                || builder.SpareBearingElapsedTicks
                    != builder.SpareBearingRequiredTicks
                || builder.SpareBearingRequiredTicks
                    != LastBearingBalanceV1
                        .SpareBearingBatchRequiredSettlementTicks
                || builder.SpareBearingLotQuantity != 0
                || builder.SpareBearingLotCustody
                    != SpareBearingLotCustody.None)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_SPARE_BEARING_LOT_CREATE_INVALID");
            }

            builder.SpareBearingLotQuantity =
                LastBearingBalanceV1.SpareBearingBatchOutputQuantity;
            builder.SpareBearingLotCustody =
                SpareBearingLotCustody.WorkshopOutput;
        }

        internal static void TransferSpareBearingLotToClaimsCounter(
            LastBearingStateBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (builder.SpareBearingRecipe
                    != SpareBearingRecipe.SpareBearingOneGoodBatch
                || builder.SpareBearingBatchPhase
                    != SpareBearingBatchPhase.Complete
                || builder.SpareBearingLotQuantity
                    != LastBearingBalanceV1.SpareBearingBatchOutputQuantity
                || builder.SpareBearingLotCustody
                    != SpareBearingLotCustody.WorkshopOutput)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_SPARE_BEARING_LOT_TRANSFER_INVALID");
            }

            builder.SpareBearingLotCustody =
                SpareBearingLotCustody.LastBearingClaimsCounter;
        }

        private static void RequireLegalRepairCustody(
            RepairCargoKind kind,
            RepairCargoCustody custody)
        {
            if (kind == RepairCargoKind.CeramicBearing
                && (custody == RepairCargoCustody.Depot
                    || custody == RepairCargoCustody.Faction
                    || custody == RepairCargoCustody.Vehicle
                    || custody == RepairCargoCustody.Turbine))
            {
                return;
            }

            if (kind == RepairCargoKind.FieldSleeve
                && (custody == RepairCargoCustody.Faction
                    || custody == RepairCargoCustody.Vehicle
                    || custody == RepairCargoCustody.Consumed))
            {
                return;
            }

            throw new InvalidOperationException(
                "LAST_BEARING_REPAIR_CARGO_CUSTODY_INVALID");
        }

        private static void RequireLegalRepairTransition(
            RepairCargoKind kind,
            RepairCargoCustody previous,
            RepairCargoCustody next)
        {
            RequireLegalRepairCustody(kind, previous);
            RequireLegalRepairCustody(kind, next);
            if (kind == RepairCargoKind.CeramicBearing
                && (previous == RepairCargoCustody.Depot
                    || previous == RepairCargoCustody.Faction)
                && next == RepairCargoCustody.Vehicle)
            {
                return;
            }

            if (kind == RepairCargoKind.CeramicBearing
                && previous == RepairCargoCustody.Vehicle
                && next == RepairCargoCustody.Turbine)
            {
                return;
            }

            if (kind == RepairCargoKind.FieldSleeve
                && previous == RepairCargoCustody.Faction
                && next == RepairCargoCustody.Vehicle)
            {
                return;
            }

            if (kind == RepairCargoKind.FieldSleeve
                && previous == RepairCargoCustody.Vehicle
                && next == RepairCargoCustody.Consumed)
            {
                return;
            }

            throw new InvalidOperationException(
                "LAST_BEARING_REPAIR_CARGO_TRANSITION_INVALID");
        }
    }
}
