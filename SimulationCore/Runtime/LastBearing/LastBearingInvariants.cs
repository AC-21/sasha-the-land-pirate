#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public static class LastBearingInvariants
    {
        public static void Validate(LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            Require(
                state.SchemaVersion == LastBearingState.CurrentSchemaVersion,
                "LAST_BEARING_SCHEMA_VERSION_INVALID");
            Require(
                string.Equals(
                    state.BalanceRevision,
                    LastBearingBalanceV1.Revision,
                    StringComparison.Ordinal),
                "LAST_BEARING_BALANCE_REVISION_INVALID");
            Require(
                string.Equals(
                    state.ProtagonistId,
                    LastBearingState.SashaProtagonistId,
                    StringComparison.Ordinal),
                "LAST_BEARING_PROTAGONIST_INVALID");
            var roster = state.Roster
                ?? throw new InvalidOperationException(
                    "LAST_BEARING_ROSTER_REQUIRED");
            Require(
                roster.Equals(
                    ResidentRoster.CreateForComposition(state.Composition)),
                "LAST_BEARING_ROSTER_NOT_EXACT");
            Require(
                state.AssignedResidentId == null
                || roster.Contains(state.AssignedResidentId),
                "LAST_BEARING_ASSIGNED_RESIDENT_NOT_IN_ROSTER");

            RequireNonnegative(state.GlobalTick, "GLOBAL_TICK");
            RequireNonnegative(state.SettlementTick, "SETTLEMENT_TICK");
            RequireNonnegative(state.FactionTick, "FACTION_TICK");
            RequireNonnegative(state.CrisisTick, "CRISIS_TICK");
            RequireNonnegative(state.RoadTick, "ROAD_TICK");
            RequireNonnegative(state.NextCommandSequence, "COMMAND_SEQUENCE");
            RequireAccumulator(state.SettlementAccumulatorMilli, "SETTLEMENT");
            RequireAccumulator(state.FactionAccumulatorMilli, "FACTION");
            RequireAccumulator(state.CrisisAccumulatorMilli, "CRISIS");
            RequireAccumulator(state.RoadAccumulatorMilli, "ROAD");
            RequireEnum(state.PauseCause, "PAUSE_CAUSE");

            RequireRange(
                state.WaterMilli,
                0,
                LastBearingBalanceV1.WaterCapacityMilli,
                "WATER");
            RequireNonnegative(state.PartsUnits, "PARTS");
            RequireNonnegative(state.FuelUnits, "FUEL");
            RequireRange(state.VehicleConditionMilli, 0, 1000, "VEHICLE_CONDITION");
            RequireEnum(state.TurbineCondition, "TURBINE_CONDITION");
            RequireEnum(state.NextCityDecision, "NEXT_CITY_DECISION");
            RequireEnum(
                state.InstalledCityImprovement,
                "INSTALLED_CITY_IMPROVEMENT");

            ValidatePreparation(state);
            ValidateVehicleAndTransaction(state);
            ValidateCargo(state);
            ValidateFaction(state);
            ValidateCityImprovement(state);
        }

        private static void ValidatePreparation(LastBearingState state)
        {
            RequireEnum(state.PreparationChoice, "PREPARATION_CHOICE");
            RequireEnum(state.PreparationPhase, "PREPARATION_PHASE");
            RequireEnum(state.PlannedModule, "PLANNED_MODULE");
            RequireEnum(state.VehicleModule, "VEHICLE_MODULE");
            RequireEnum(
                state.ModuleInstallationState,
                "MODULE_INSTALLATION_STATE");
            RequireNonnegative(state.PreparationElapsedTicks, "PREPARATION_ELAPSED");
            RequireNonnegative(state.PreparationRequiredTicks, "PREPARATION_REQUIRED");
            RequireNonnegative(
                state.PreparationFuelDebitedUnits,
                "PREPARATION_FUEL_DEBITED");
            Require(
                state.WorkshopServiceSlotsReserved == 0
                || state.WorkshopServiceSlotsReserved == 1,
                "LAST_BEARING_WORKSHOP_SLOT_INVALID");

            if (state.PreparationPhase == PreparationPhase.Unselected)
            {
                Require(
                    state.PreparationChoice == PreparationChoice.Unselected
                    && state.PlannedModule == VehicleModule.None
                    && state.PreparationElapsedTicks == 0
                    && state.PreparationRequiredTicks == 0
                    && state.PreparationFuelDebitedUnits == 0
                    && state.WorkshopServiceSlotsReserved == 0
                    && state.ActiveWaterModifierMilliPerSettlementTick == 0,
                    "LAST_BEARING_UNSELECTED_PREPARATION_INVALID");
            }
            else
            {
                Require(
                    state.PreparationChoice != PreparationChoice.Unselected
                    && state.PlannedModule != VehicleModule.None,
                    "LAST_BEARING_SELECTED_PREPARATION_INVALID");
                Require(
                    state.PreparationRequiredTicks
                    == LastBearingBalanceV1.PreparationDuration(
                        state.PreparationChoice,
                        state.PlannedModule),
                    "LAST_BEARING_PREPARATION_DURATION_INVALID");
                Require(
                    state.PreparationFuelDebitedUnits
                    == LastBearingBalanceV1.PreparationFuelCost(
                        state.PreparationChoice),
                    "LAST_BEARING_PREPARATION_DEBIT_INVALID");
                Require(
                    state.PreparationElapsedTicks
                    <= state.PreparationRequiredTicks,
                    "LAST_BEARING_PREPARATION_ELAPSED_INVALID");
                Require(
                    state.WorkshopServiceSlotsReserved
                    == (state.PreparationChoice == PreparationChoice.WorkshopPush
                        && state.PreparationPhase == PreparationPhase.Preparing
                        ? 1
                        : 0),
                    "LAST_BEARING_WORKSHOP_SLOT_STATE_INVALID");

                var expectedModifier = state.PreparationPhase
                    == PreparationPhase.Preparing
                    ? LastBearingBalanceV1.PreparationWaterModifier(
                        state.PreparationChoice)
                    : 0;
                Require(
                    state.ActiveWaterModifierMilliPerSettlementTick
                    == expectedModifier,
                    "LAST_BEARING_PREPARATION_MODIFIER_INVALID");
            }

            if (state.ModuleInstallationState == ModuleInstallationState.None)
            {
                Require(
                    state.VehicleModule == VehicleModule.None,
                    "LAST_BEARING_UNINSTALLED_MODULE_INVALID");
            }
            else if (state.ModuleInstallationState
                == ModuleInstallationState.Pending)
            {
                Require(
                    state.VehicleModule == VehicleModule.None
                    && state.PlannedModule != VehicleModule.None
                    && state.PreparationPhase == PreparationPhase.Preparing,
                    "LAST_BEARING_PENDING_MODULE_INVALID");
            }
            else
            {
                Require(
                    state.VehicleModule == state.PlannedModule
                    && state.VehicleModule != VehicleModule.None
                    && state.PreparationPhase != PreparationPhase.Unselected,
                    "LAST_BEARING_INSTALLED_MODULE_INVALID");
            }
        }

        private static void ValidateVehicleAndTransaction(LastBearingState state)
        {
            RequireEnum(state.RouteKind, "ROUTE_KIND");
            RequireEnum(state.RouteActionKind, "ROUTE_ACTION_KIND");
            RequireEnum(state.ExpeditionPhase, "EXPEDITION_PHASE");
            RequireEnum(state.TransactionPhase, "TRANSACTION_PHASE");
            RequireEnum(state.DepotResolution, "DEPOT_RESOLUTION");
            RequireNonnegative(state.RouteProgressTicks, "ROUTE_PROGRESS");
            RequireNonnegative(state.RouteTargetTicks, "ROUTE_TARGET");
            RequireAccumulator(
                state.RouteMovementAccumulatorMilli,
                "ROUTE_MOVEMENT");
            RequireRange(
                state.VehicleLateralMilli,
                -LastBearingBalanceV1.RoadLateralLimitMilli,
                LastBearingBalanceV1.RoadLateralLimitMilli,
                "VEHICLE_LATERAL");
            RequireNonnegative(
                state.ExpeditionFuelManifestUnits,
                "EXPEDITION_FUEL_MANIFEST");
            RequireNonnegative(
                state.OrdinaryCargoCapacityUnits,
                "ORDINARY_CARGO_CAPACITY");
            RequireRange(
                state.OrdinaryCargoUsedUnits,
                0,
                state.OrdinaryCargoCapacityUnits,
                "ORDINARY_CARGO_USED");
            Require(state.TowSlots >= 0, "LAST_BEARING_TOW_SLOTS_NEGATIVE");
            Require(
                state.TowSlotsUsed >= 0 && state.TowSlotsUsed <= state.TowSlots,
                "LAST_BEARING_TOW_SLOTS_USED_INVALID");
            RequireNonnegative(state.LiquidCapacityMilli, "LIQUID_CAPACITY");

            if (state.VehicleModule == VehicleModule.None)
            {
                Require(
                    state.RouteKind == RouteKind.None
                    && state.RouteActionKind == RouteActionKind.None
                    && state.RouteTargetTicks == 0
                    && state.OrdinaryCargoCapacityUnits == 0
                    && state.TowSlots == 0
                    && state.LiquidCapacityMilli == 0,
                    "LAST_BEARING_EMPTY_VEHICLE_CAPABILITY_INVALID");
            }
            else
            {
                Require(
                    state.RouteKind
                    == LastBearingBalanceV1.RouteFor(state.VehicleModule)
                    && state.RouteActionKind
                    == LastBearingBalanceV1.RouteActionFor(state.VehicleModule)
                    && state.RouteTargetTicks
                    == LastBearingBalanceV1.RouteOneWayTicks(state.VehicleModule),
                    "LAST_BEARING_ROUTE_CAPABILITY_INVALID");
            }

            Require(
                state.RouteProgressTicks <= state.RouteTargetTicks,
                "LAST_BEARING_ROUTE_PROGRESS_EXCEEDS_TARGET");

            if (state.VehicleModule != VehicleModule.None)
            {
                var wreckLineGate = LastBearingBalanceV1.WreckLineGateTicks(
                    state.VehicleModule);
                Require(
                    state.ExpeditionPhase != ExpeditionPhase.Outbound
                    || state.RouteActionUsed
                    || state.RouteProgressTicks <= wreckLineGate,
                    "LAST_BEARING_WRECK_LINE_BYPASSED");
                Require(
                    state.ExpeditionPhase != ExpeditionPhase.Outbound
                    || !state.RouteActionUsed
                    || state.RouteProgressTicks >= wreckLineGate,
                    "LAST_BEARING_WRECK_LINE_ACTION_BEFORE_GATE");
                Require(
                    !state.RouteActionUsed
                    || state.TransactionPhase >= TransactionPhase.RoadOwned,
                    "LAST_BEARING_ROUTE_ACTION_PHASE_INVALID");
            }
            else
            {
                Require(
                    !state.RouteActionUsed,
                    "LAST_BEARING_EMPTY_VEHICLE_ROUTE_ACTION_INVALID");
            }

            var hasTransaction = state.TransactionPhase != TransactionPhase.None;
            Require(
                hasTransaction
                    ? state.TransactionId != null
                        && state.TransactionFingerprint != null
                    : state.TransactionId == null
                        && state.TransactionFingerprint == null,
                "LAST_BEARING_TRANSACTION_IDENTITY_INVALID");

            switch (state.ExpeditionPhase)
            {
                case ExpeditionPhase.AtHome:
                    Require(
                        state.TransactionPhase == TransactionPhase.None
                        || state.TransactionPhase == TransactionPhase.Prepared
                        || state.TransactionPhase == TransactionPhase.CityDebited
                        || state.TransactionPhase == TransactionPhase.Finalized,
                        "LAST_BEARING_HOME_TRANSACTION_PHASE_INVALID");
                    break;
                case ExpeditionPhase.Outbound:
                case ExpeditionPhase.AtDepot:
                    Require(
                        state.TransactionPhase == TransactionPhase.RoadOwned,
                        "LAST_BEARING_ROAD_OWNERSHIP_INVALID");
                    break;
                case ExpeditionPhase.Returning:
                    Require(
                        state.TransactionPhase == TransactionPhase.ReturnPending,
                        "LAST_BEARING_RETURN_OWNERSHIP_INVALID");
                    break;
                case ExpeditionPhase.Returned:
                    Require(
                        state.TransactionPhase == TransactionPhase.ReturnPending
                        || state.TransactionPhase == TransactionPhase.CityCredited,
                        "LAST_BEARING_RETURN_OWNERSHIP_INVALID");
                    break;
                default:
                    throw new InvalidOperationException(
                        "LAST_BEARING_EXPEDITION_PHASE_INVALID");
            }

            Require(
                !state.ReturnPayloadFrozen
                || state.TransactionPhase >= TransactionPhase.ReturnPending,
                "LAST_BEARING_RETURN_FREEZE_PHASE_INVALID");
            Require(
                state.DepotResolution == EncounterChoice.Unresolved
                || state.ExpeditionPhase == ExpeditionPhase.AtDepot
                || state.ExpeditionPhase == ExpeditionPhase.Returning
                || state.ExpeditionPhase == ExpeditionPhase.Returned
                || state.TransactionPhase >= TransactionPhase.CityCredited,
                "LAST_BEARING_DEPOT_RESOLUTION_PHASE_INVALID");

            if (state.ExpeditionPhase == ExpeditionPhase.Outbound)
            {
                Require(
                    state.RouteProgressTicks != state.RouteTargetTicks
                    || state.HasArrivalClaimSnapshot,
                    "LAST_BEARING_ROUTE_ARRIVAL_SNAPSHOT_REQUIRED");
                Require(
                    !state.HasArrivalClaimSnapshot
                    || (state.RouteTargetTicks > 0
                        && state.RouteProgressTicks == state.RouteTargetTicks),
                    "LAST_BEARING_OUTBOUND_ARRIVAL_SNAPSHOT_INVALID");
            }

            if (state.ExpeditionPhase == ExpeditionPhase.AtDepot
                || state.ExpeditionPhase == ExpeditionPhase.Returning
                || state.ExpeditionPhase == ExpeditionPhase.Returned)
            {
                Require(
                    state.HasArrivalClaimSnapshot,
                    "LAST_BEARING_POST_ARRIVAL_SNAPSHOT_REQUIRED");
            }

            if (state.HasArrivalClaimSnapshot)
            {
                Require(
                    state.RouteActionUsed,
                    "LAST_BEARING_ARRIVAL_REQUIRES_ROUTE_ACTION");
                RequireRange(
                    state.ArrivalFactionClaimProgressMilli,
                    0,
                    LastBearingBalanceV1.FactionClaimThresholdMilli,
                    "ARRIVAL_CLAIM_PROGRESS");
                RequireEnum(
                    state.ArrivalFactionClaimState,
                    "ARRIVAL_CLAIM_STATE");
            }
            else
            {
                Require(
                    state.ArrivalFactionClaimProgressMilli == 0,
                    "LAST_BEARING_ABSENT_ARRIVAL_SNAPSHOT_INVALID");
            }
        }

        private static void ValidateCargo(LastBearingState state)
        {
            RequireEnum(state.RepairCargoKind, "REPAIR_CARGO_KIND");
            RequireEnum(state.RepairCargoCustody, "REPAIR_CARGO_CUSTODY");
            RequireEnum(state.HeavyCargoKind, "HEAVY_CARGO_KIND");
            RequireEnum(state.HeavyCargoCustody, "HEAVY_CARGO_CUSTODY");
            RequireEnum(state.LiquidCargoKind, "LIQUID_CARGO_KIND");
            RequireEnum(state.LiquidCargoCustody, "LIQUID_CARGO_CUSTODY");
            RequireEnum(
                state.DepotBearingDisposition,
                "DEPOT_BEARING_DISPOSITION");

            if (state.RepairCargoKind == RepairCargoKind.None)
            {
                Require(
                    state.RepairCargoCustody == RepairCargoCustody.None,
                    "LAST_BEARING_EMPTY_REPAIR_CUSTODY_INVALID");
            }
            else if (state.RepairCargoKind == RepairCargoKind.CeramicBearing)
            {
                Require(
                    state.RepairCargoCustody == RepairCargoCustody.Vehicle
                    || state.RepairCargoCustody == RepairCargoCustody.Turbine,
                    "LAST_BEARING_BEARING_CUSTODY_INVALID");
            }
            else
            {
                Require(
                    state.RepairCargoCustody == RepairCargoCustody.Vehicle
                    || state.RepairCargoCustody == RepairCargoCustody.Consumed,
                    "LAST_BEARING_SLEEVE_CUSTODY_INVALID");
            }

            if (state.TurbineCondition == TurbineCondition.Failing)
            {
                Require(
                    state.RepairCargoCustody != RepairCargoCustody.Turbine
                    && state.RepairCargoCustody != RepairCargoCustody.Consumed,
                    "LAST_BEARING_UNAPPLIED_REPAIR_CARGO_INVALID");
            }

            Require(
                state.HeavyCargoKind == HeavyCargoKind.PumpRotor,
                "LAST_BEARING_PUMP_ROTOR_REQUIRED");
            Require(
                state.HeavyCargoCustody == HeavyCargoCustody.Depot
                || state.HeavyCargoCustody == HeavyCargoCustody.Vehicle
                || state.HeavyCargoCustody == HeavyCargoCustody.Settlement
                || state.HeavyCargoCustody
                    == HeavyCargoCustody.InstalledAtAuxiliaryPump,
                "LAST_BEARING_HEAVY_CUSTODY_INVALID");
            Require(
                state.HeavyCargoCustody != HeavyCargoCustody.Settlement
                || state.TransactionPhase >= TransactionPhase.CityCredited,
                "LAST_BEARING_ROTOR_SETTLEMENT_PHASE_INVALID");
            if (state.VehicleModule == VehicleModule.WinchAssembly
                && state.RouteActionUsed)
            {
                HeavyCargoCustody expectedCustody =
                    state.InstalledCityImprovement
                        == CityImprovementKind.RefurbishedAuxiliaryPump
                        ? HeavyCargoCustody.InstalledAtAuxiliaryPump
                        : state.TransactionPhase >= TransactionPhase.CityCredited
                            ? HeavyCargoCustody.Settlement
                            : HeavyCargoCustody.Vehicle;
                Require(
                    state.HeavyCargoCustody == expectedCustody,
                    "LAST_BEARING_WINCH_ACTION_ROTOR_CUSTODY_INVALID");
            }

            if (state.HeavyCargoCustody == HeavyCargoCustody.Depot)
            {
                Require(
                    state.TowSlotsUsed == 0,
                    "LAST_BEARING_DEPOT_ROTOR_TOW_SLOT_INVALID");
            }
            else if (state.HeavyCargoCustody
                != HeavyCargoCustody.InstalledAtAuxiliaryPump)
            {
                Require(
                    state.VehicleModule == VehicleModule.WinchAssembly
                    && state.RouteActionUsed
                    && state.TowSlotsUsed == 1,
                    "LAST_BEARING_RECOVERED_ROTOR_STATE_INVALID");
            }
            else
            {
                Require(
                    state.VehicleModule == VehicleModule.WinchAssembly
                    && state.RouteActionUsed
                    && state.TowSlotsUsed == 0,
                    "LAST_BEARING_INSTALLED_ROTOR_STATE_INVALID");
            }

            if (state.LiquidCargoKind == LiquidCargoKind.None)
            {
                Require(
                    state.LiquidCargoQuantityMilli == 0
                    && state.LiquidCargoCustody == LiquidCargoCustody.None,
                    "LAST_BEARING_EMPTY_LIQUID_CARGO_INVALID");
            }
            else
            {
                Require(
                    state.VehicleModule == VehicleModule.SealedRangeTank
                    && state.LiquidCargoQuantityMilli > 0
                    && state.LiquidCargoQuantityMilli <= state.LiquidCapacityMilli
                    && (state.LiquidCargoCustody == LiquidCargoCustody.Vehicle
                        || state.LiquidCargoCustody
                            == LiquidCargoCustody.Settlement),
                    "LAST_BEARING_LIQUID_CARGO_INVALID");
            }

            if (state.TurbineCondition == TurbineCondition.BearingRepaired)
            {
                Require(
                    state.RepairCargoKind == RepairCargoKind.CeramicBearing
                    && state.RepairCargoCustody == RepairCargoCustody.Turbine
                    && state.DepotBearingDisposition
                        == DepotBearingDisposition.InstalledAtTurbine,
                    "LAST_BEARING_BEARING_REPAIR_INVALID");
            }
            else if (state.TurbineCondition == TurbineCondition.SleeveRepaired)
            {
                Require(
                    state.RepairCargoKind == RepairCargoKind.FieldSleeve
                    && state.RepairCargoCustody == RepairCargoCustody.Consumed,
                    "LAST_BEARING_SLEEVE_REPAIR_INVALID");
            }
        }

        private static void ValidateFaction(LastBearingState state)
        {
            RequireRange(
                state.FactionClaimProgressMilli,
                0,
                LastBearingBalanceV1.FactionClaimThresholdMilli,
                "FACTION_CLAIM_PROGRESS");
            RequireEnum(state.FactionClaimState, "FACTION_CLAIM_STATE");
            RequireEnum(state.DepotControl, "DEPOT_CONTROL");
            RequireEnum(state.FactionAccessPolicy, "FACTION_ACCESS_POLICY");
            RequireEnum(state.FactionAidPolicy, "FACTION_AID_POLICY");
            RequireEnum(
                state.PendingFactionOutcome,
                "PENDING_FACTION_OUTCOME");
            RequireEnum(state.MaintenanceRecipe, "MAINTENANCE_RECIPE");
            RequireNonnegative(
                state.DepotAccessFeePartsUnits,
                "DEPOT_ACCESS_FEE");
            RequireNonnegative(
                state.FutureRouteTollFuelUnits,
                "FUTURE_ROUTE_TOLL");
            RequireNonnegative(
                state.EmergencyAidWaterMilli,
                "EMERGENCY_AID_WATER");
            RequireNonnegative(
                state.FactionOutcomeElapsedTicks,
                "FACTION_OUTCOME_ELAPSED");
            RequireNonnegative(
                state.MaintenancePartsUnits,
                "MAINTENANCE_PARTS");
            RequireNonnegative(
                state.NextMaintenanceDueSettlementTick,
                "NEXT_MAINTENANCE_DUE");
            RequireNonnegative(
                state.DustFrontProgressTicks,
                "DUST_FRONT_PROGRESS");

            Require(
                (state.FactionMemory == null)
                    == (state.PendingFactionOutcome == FactionOutcomeKind.None),
                "LAST_BEARING_FACTION_MEMORY_OUTCOME_INVALID");
            Require(
                state.MaintenanceObligationActive
                    ? state.MaintenanceRecipe
                            == MaintenanceRecipe.FieldSleeveService
                        && state.MaintenancePartsUnits > 0
                        && state.NextMaintenanceDueSettlementTick > 0
                    : state.MaintenanceRecipe == MaintenanceRecipe.None
                        && state.MaintenancePartsUnits == 0
                        && state.NextMaintenanceDueSettlementTick == 0
                        && !state.MaintenanceDue,
                "LAST_BEARING_MAINTENANCE_STATE_INVALID");
        }

        private static void ValidateCityImprovement(LastBearingState state)
        {
            bool rotorInstalled = state.HeavyCargoCustody
                == HeavyCargoCustody.InstalledAtAuxiliaryPump;
            Require(
                (state.InstalledCityImprovement
                    == CityImprovementKind.RefurbishedAuxiliaryPump)
                    == rotorInstalled,
                "LAST_BEARING_CITY_IMPROVEMENT_CUSTODY_INVALID");
            if (state.InstalledCityImprovement == CityImprovementKind.None)
            {
                return;
            }

            Require(
                state.ExpeditionPhase == ExpeditionPhase.AtHome
                && state.TransactionPhase == TransactionPhase.Finalized
                && state.TurbineCondition != TurbineCondition.Failing
                && state.NextCityDecision == NextCityDecision.None
                && state.PreparationChoice == PreparationChoice.WorkshopPush
                && state.VehicleModule == VehicleModule.WinchAssembly
                && state.RouteActionUsed
                && state.TowSlotsUsed == 0
                && state.PartsUnits
                    >= LastBearingBalanceV1.MinimumPostReturnPartsUnits,
                "LAST_BEARING_CITY_IMPROVEMENT_STATE_INVALID");
        }

        private static void Require(bool condition, string code)
        {
            if (!condition)
            {
                throw new InvalidOperationException(code);
            }
        }

        private static void RequireAccumulator(int value, string label)
        {
            Require(
                value >= 0 && value < LastBearingBalanceV1.FullClockScaleMilli,
                "LAST_BEARING_" + label + "_ACCUMULATOR_INVALID");
        }

        private static void RequireNonnegative(long value, string label)
        {
            Require(value >= 0, "LAST_BEARING_" + label + "_NEGATIVE");
        }

        private static void RequireRange(
            long value,
            long minimum,
            long maximum,
            string label)
        {
            Require(
                value >= minimum && value <= maximum,
                "LAST_BEARING_" + label + "_OUT_OF_RANGE");
        }

        private static void RequireEnum<TEnum>(TEnum value, string label)
            where TEnum : struct
        {
            Require(
                Enum.IsDefined(typeof(TEnum), value),
                "LAST_BEARING_" + label + "_INVALID");
        }
    }
}
