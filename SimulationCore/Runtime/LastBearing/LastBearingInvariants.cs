#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public static class LastBearingInvariants
    {
        private static readonly ResidentRoster HumanRoster =
            ResidentRoster.CreateForComposition(
                ColonyComposition.HumanOnly);
        private static readonly ResidentRoster RobotRoster =
            ResidentRoster.CreateForComposition(
                ColonyComposition.RobotOnly);
        private static readonly ResidentRoster MixedRoster =
            ResidentRoster.CreateForComposition(
                ColonyComposition.Mixed);

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
                roster.Equals(CanonicalRoster(state.Composition)),
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
            ValidateCityConstruction(state);
            ValidateHotShift(state);
            ValidateVehicleAndTransaction(state);
            ValidateCargo(state);
            ValidateFaction(state);
            ValidateCityImprovement(state);
            ValidateSpareBearingBatch(state);
        }

        private static void ValidateCityConstruction(LastBearingState state)
        {
            ValidateCityBuildingPlacement(
                state.RecyclerPadIndex,
                state.RecyclerQuarterTurns);
            ValidateCityBuildingPlacement(
                state.MachineShopPadIndex,
                state.MachineShopQuarterTurns);
            ValidateCityBuildingPlacement(
                state.EmergencyStoragePadIndex,
                state.EmergencyStorageQuarterTurns);
            Require(
                state.RecyclerPadIndex == LastBearingState.UnplacedCityPadIndex
                    || state.RecyclerPadIndex != state.MachineShopPadIndex,
                "LAST_BEARING_CITY_PAD_DUPLICATE");
            Require(
                state.RecyclerPadIndex == LastBearingState.UnplacedCityPadIndex
                    || state.RecyclerPadIndex
                        != state.EmergencyStoragePadIndex,
                "LAST_BEARING_CITY_PAD_DUPLICATE");
            Require(
                state.MachineShopPadIndex
                        == LastBearingState.UnplacedCityPadIndex
                    || state.MachineShopPadIndex
                        != state.EmergencyStoragePadIndex,
                "LAST_BEARING_CITY_PAD_DUPLICATE");
            RequireEnum(state.CityDeliveryStage, "CITY_DELIVERY_STAGE");
            Require(
                state.CityDeliveryCount == 0 || state.CityDeliveryCount == 1,
                "LAST_BEARING_CITY_DELIVERY_COUNT_INVALID");
            Require(
                state.CityServiceResidentId == null
                    || state.Roster.Contains(state.CityServiceResidentId),
                "LAST_BEARING_CITY_SERVICE_RESIDENT_NOT_IN_ROSTER");

            bool allBuildingsPlaced =
                state.RecyclerPadIndex != LastBearingState.UnplacedCityPadIndex
                && state.MachineShopPadIndex
                    != LastBearingState.UnplacedCityPadIndex
                && state.EmergencyStoragePadIndex
                    != LastBearingState.UnplacedCityPadIndex;
            if (!state.CityServiceLinkConnected)
            {
                Require(
                    state.CityServiceResidentId == null
                    && state.CityDeliveryStage
                        == CityDeliveryStage.AtRecycler
                    && state.CityDeliveryCount == 0
                    && !state.SliceInfrastructureActive,
                    "LAST_BEARING_CITY_SERVICE_UNLINKED_STATE_INVALID");
            }
            else
            {
                Require(
                    allBuildingsPlaced,
                    "LAST_BEARING_CITY_SERVICE_LINK_BUILDINGS_INVALID");
            }

            if (state.CityServiceResidentId == null)
            {
                Require(
                    state.CityDeliveryStage == CityDeliveryStage.AtRecycler
                    && state.CityDeliveryCount == 0
                    && !state.SliceInfrastructureActive,
                    "LAST_BEARING_CITY_SERVICE_UNSTAFFED_STATE_INVALID");
            }

            switch (state.CityDeliveryStage)
            {
                case CityDeliveryStage.AtRecycler:
                case CityDeliveryStage.InTransit:
                    Require(
                        state.CityDeliveryCount == 0
                        && !state.SliceInfrastructureActive,
                        "LAST_BEARING_CITY_DELIVERY_PENDING_STATE_INVALID");
                    break;
                case CityDeliveryStage.DeliveredToWorkshop:
                    Require(
                        state.CityServiceLinkConnected
                        && state.CityServiceResidentId != null
                        && state.CityDeliveryCount == 1
                        && state.SliceInfrastructureActive,
                        "LAST_BEARING_CITY_DELIVERY_COMPLETE_STATE_INVALID");
                    break;
                default:
                    throw new InvalidOperationException(
                        "LAST_BEARING_CITY_DELIVERY_STAGE_INVALID");
            }
        }

        private static void ValidateCityBuildingPlacement(
            int padIndex,
            int quarterTurns)
        {
            Require(
                padIndex >= LastBearingState.UnplacedCityPadIndex
                    && padIndex < LastBearingState.CityConstructionPadCount,
                "LAST_BEARING_CITY_BUILDING_PAD_INVALID");
            Require(
                quarterTurns >= 0 && quarterTurns <= 3,
                "LAST_BEARING_CITY_BUILDING_ORIENTATION_INVALID");
            Require(
                padIndex != LastBearingState.UnplacedCityPadIndex
                    || quarterTurns == 0,
                "LAST_BEARING_CITY_BUILDING_UNPLACED_INVALID");
        }

        private static void ValidateHotShift(LastBearingState state)
        {
            RequireEnum(state.HotShiftPhase, "HOT_SHIFT_PHASE");
            RequireNonnegative(
                state.HotShiftElapsedTicks,
                "HOT_SHIFT_ELAPSED_TICKS");
            RequireNonnegative(
                state.HotShiftRequiredTicks,
                "HOT_SHIFT_REQUIRED_TICKS");
            RequireNonnegative(
                state.HotShiftFuelCommittedUnits,
                "HOT_SHIFT_FUEL_COMMITTED");
            RequireNonnegative(
                state.HotShiftCompletedCount,
                "HOT_SHIFT_COMPLETED_COUNT");

            if (state.HotShiftPhase == HotShiftPhase.Idle)
            {
                Require(
                    state.HotShiftElapsedTicks == 0
                    && state.HotShiftRequiredTicks == 0
                    && state.HotShiftFuelCommittedUnits == 0,
                    "LAST_BEARING_HOT_SHIFT_IDLE_STATE_INVALID");
                return;
            }

            Require(
                state.SliceInfrastructureActive
                && state.CityDeliveryStage
                    == CityDeliveryStage.DeliveredToWorkshop
                && state.PreparationChoice
                    != PreparationChoice.Unselected
                && state.PlannedModule != VehicleModule.None
                && state.ModuleInstallationState
                    != ModuleInstallationState.None
                && state.HotShiftRequiredTicks
                    == LastBearingBalanceV1
                        .HotShiftRequiredSettlementTicks
                && state.HotShiftElapsedTicks
                    < state.HotShiftRequiredTicks
                && state.HotShiftFuelCommittedUnits
                    == LastBearingBalanceV1.HotShiftFuelCostUnits,
                "LAST_BEARING_HOT_SHIFT_PROGRESS_STATE_INVALID");
        }

        private static void ValidatePreparation(LastBearingState state)
        {
            RequireEnum(state.PreparationChoice, "PREPARATION_CHOICE");
            RequireEnum(state.PreparationPhase, "PREPARATION_PHASE");
            RequireEnum(state.PlannedModule, "PLANNED_MODULE");
            RequireEnum(state.VehicleModule, "VEHICLE_MODULE");
            RequireEnum(state.RigUpgrade, "RIG_UPGRADE");
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
                Require(
                    state.ExpeditionPhase != ExpeditionPhase.Outbound
                    || !state.RouteActionUsed
                    || state.FrameRailSalvageCustody
                        != FrameRailSalvageCustody.WreckLine
                    || state.RouteProgressTicks == wreckLineGate,
                    "LAST_BEARING_FRAME_RAIL_SALVAGE_GATE_BYPASSED");
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
            RequireEnum(
                state.FrameRailSalvageCustody,
                "FRAME_RAIL_SALVAGE_CUSTODY");
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
                    state.RepairCargoCustody == RepairCargoCustody.None
                    && state.DepotResolution == EncounterChoice.Unresolved
                    && state.OrdinaryCargoUsedUnits
                        == FrameRailCargoUnits(state),
                    "LAST_BEARING_EMPTY_REPAIR_CUSTODY_INVALID");
            }
            else if (state.RepairCargoKind == RepairCargoKind.CeramicBearing)
            {
                Require(
                    state.DepotResolution == EncounterChoice.TakeBearing
                    && (state.RepairCargoCustody
                            == RepairCargoCustody.Depot
                        || state.RepairCargoCustody
                            == RepairCargoCustody.Faction
                        || state.RepairCargoCustody
                            == RepairCargoCustody.Vehicle
                        || state.RepairCargoCustody
                            == RepairCargoCustody.Turbine),
                    "LAST_BEARING_BEARING_CUSTODY_INVALID");
            }
            else
            {
                Require(
                    state.DepotResolution == EncounterChoice.Cooperate
                    && (state.RepairCargoCustody
                            == RepairCargoCustody.Faction
                        || state.RepairCargoCustody
                            == RepairCargoCustody.Vehicle
                        || state.RepairCargoCustody
                            == RepairCargoCustody.Consumed),
                    "LAST_BEARING_SLEEVE_CUSTODY_INVALID");
            }

            if (state.RepairCargoCustody == RepairCargoCustody.Depot
                || state.RepairCargoCustody == RepairCargoCustody.Faction)
            {
                Require(
                    state.ExpeditionPhase == ExpeditionPhase.AtDepot
                    && state.TransactionPhase == TransactionPhase.RoadOwned
                    && !state.ReturnPayloadFrozen
                    && state.OrdinaryCargoUsedUnits
                        == FrameRailCargoUnits(state),
                    "LAST_BEARING_REPAIR_CARGO_SOURCE_PHASE_INVALID");

                bool exactDepotLineage =
                    (state.DepotControl == DepotControl.Unclaimed
                        && state.FactionClaimProgressMilli
                            < LastBearingBalanceV1
                                .FactionContestedThresholdMilli)
                    || (state.DepotControl == DepotControl.Contested
                        && state.FactionClaimProgressMilli
                            >= LastBearingBalanceV1
                                .FactionContestedThresholdMilli
                        && state.FactionClaimProgressMilli
                            < LastBearingBalanceV1.FactionClaimThresholdMilli);
                bool exactFactionLineage =
                    state.DepotControl == DepotControl.FactionClaimed
                    && state.FactionClaimProgressMilli
                        == LastBearingBalanceV1.FactionClaimThresholdMilli;

                bool exactCooperativeSource =
                    state.RepairCargoKind == RepairCargoKind.FieldSleeve
                    && state.RepairCargoCustody
                        == RepairCargoCustody.Faction
                    && state.DepotResolution == EncounterChoice.Cooperate
                    && state.DepotBearingDisposition
                        == DepotBearingDisposition.FactionHeld
                    && state.DepotControl == DepotControl.SharedAccess
                    && state.FactionClaimState
                        == FactionClaimState.Cooperating;
                bool exactDepotBearingSource =
                    state.RepairCargoKind == RepairCargoKind.CeramicBearing
                    && state.RepairCargoCustody == RepairCargoCustody.Depot
                    && state.DepotResolution == EncounterChoice.TakeBearing
                    && state.DepotBearingDisposition
                        == DepotBearingDisposition.AtDepot
                    && exactDepotLineage
                    && state.FactionClaimState == FactionClaimState.Aggrieved;
                bool exactFactionBearingSource =
                    state.RepairCargoKind == RepairCargoKind.CeramicBearing
                    && state.RepairCargoCustody
                        == RepairCargoCustody.Faction
                    && state.DepotResolution == EncounterChoice.TakeBearing
                    && state.DepotBearingDisposition
                        == DepotBearingDisposition.FactionHeld
                    && exactFactionLineage
                    && state.FactionClaimState == FactionClaimState.Aggrieved;
                Require(
                    exactCooperativeSource
                    || exactDepotBearingSource
                    || exactFactionBearingSource,
                    "LAST_BEARING_REPAIR_CARGO_SOURCE_INVALID");
            }
            else if (state.RepairCargoCustody == RepairCargoCustody.Vehicle)
            {
                bool exactOutcome =
                    state.DepotResolution == EncounterChoice.Cooperate
                        ? state.RepairCargoKind == RepairCargoKind.FieldSleeve
                            && state.DepotBearingDisposition
                                == DepotBearingDisposition.FactionHeld
                            && state.DepotControl
                                == DepotControl.SharedAccess
                        : state.DepotResolution
                                == EncounterChoice.TakeBearing
                            && state.RepairCargoKind
                                == RepairCargoKind.CeramicBearing
                            && state.DepotBearingDisposition
                                == DepotBearingDisposition.TakenBySasha
                            && state.DepotControl == DepotControl.Depleted;
                Require(
                    exactOutcome,
                    "LAST_BEARING_VEHICLE_REPAIR_CARGO_OUTCOME_INVALID");
                Require(
                    state.OrdinaryCargoUsedUnits
                        == checked(1 + FrameRailCargoUnits(state))
                    || (state.OrdinaryCargoUsedUnits
                            == FrameRailCargoUnits(state)
                        && state.ExpeditionPhase == ExpeditionPhase.AtDepot
                        && state.TransactionPhase
                            == TransactionPhase.RoadOwned
                        && !state.ReturnPayloadFrozen),
                    "LAST_BEARING_VEHICLE_REPAIR_CARGO_OCCUPANCY_INVALID");
            }
            else if (state.RepairCargoCustody == RepairCargoCustody.Turbine
                || state.RepairCargoCustody == RepairCargoCustody.Consumed)
            {
                Require(
                    state.OrdinaryCargoUsedUnits
                        == checked(1 + FrameRailCargoUnits(state)),
                    "LAST_BEARING_APPLIED_REPAIR_CARGO_OCCUPANCY_INVALID");
            }

            if (state.TurbineCondition == TurbineCondition.Failing)
            {
                Require(
                    state.RepairCargoCustody != RepairCargoCustody.Turbine
                    && state.RepairCargoCustody != RepairCargoCustody.Consumed,
                    "LAST_BEARING_UNAPPLIED_REPAIR_CARGO_INVALID");
            }

            ValidateFrameRailSalvage(state);

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

        private static void ValidateFrameRailSalvage(
            LastBearingState state)
        {
            if (state.FrameRailSalvageCustody
                == FrameRailSalvageCustody.None)
            {
                return;
            }

            Require(
                state.RigUpgrade == RigUpgrade.PatchworkSkidPlate,
                "LAST_BEARING_FRAME_RAIL_SALVAGE_UPGRADE_REQUIRED");

            if (state.FrameRailSalvageCustody
                == FrameRailSalvageCustody.WreckLine)
            {
                Require(
                    state.ExpeditionPhase == ExpeditionPhase.AtHome
                        ? state.TransactionPhase
                            != TransactionPhase.Finalized
                        : state.ExpeditionPhase
                                == ExpeditionPhase.Outbound
                            && state.TransactionPhase
                                == TransactionPhase.RoadOwned
                            && (!state.RouteActionUsed
                                || state.RouteProgressTicks
                                    == LastBearingBalanceV1
                                        .WreckLineGateTicks(
                                            state.VehicleModule)),
                    "LAST_BEARING_FRAME_RAIL_SALVAGE_WRECK_LINE_INVALID");
                return;
            }

            if (state.FrameRailSalvageCustody
                == FrameRailSalvageCustody.Vehicle)
            {
                Require(
                    state.RouteActionUsed
                    && (state.ExpeditionPhase == ExpeditionPhase.Outbound
                        || state.ExpeditionPhase == ExpeditionPhase.AtDepot
                        || state.ExpeditionPhase
                            == ExpeditionPhase.Returning
                        || state.ExpeditionPhase == ExpeditionPhase.Returned)
                    && state.TransactionPhase >= TransactionPhase.RoadOwned
                    && state.TransactionPhase
                        <= TransactionPhase.ReturnPending,
                    "LAST_BEARING_FRAME_RAIL_SALVAGE_VEHICLE_INVALID");
                return;
            }

            Require(
                state.FrameRailSalvageCustody
                    == FrameRailSalvageCustody.Credited
                && state.TransactionPhase >= TransactionPhase.CityCredited
                && (state.ExpeditionPhase == ExpeditionPhase.Returned
                    || state.ExpeditionPhase == ExpeditionPhase.AtHome),
                "LAST_BEARING_FRAME_RAIL_SALVAGE_CREDIT_INVALID");
        }

        private static long FrameRailCargoUnits(LastBearingState state)
        {
            return state.FrameRailSalvageCustody
                    == FrameRailSalvageCustody.Vehicle
                ? LastBearingBalanceV1
                    .WreckLineFrameRailSalvageCargoUnits
                : 0;
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

        private static void ValidateSpareBearingBatch(LastBearingState state)
        {
            RequireEnum(state.SpareBearingRecipe, "SPARE_BEARING_RECIPE");
            RequireEnum(
                state.SpareBearingBatchPhase,
                "SPARE_BEARING_BATCH_PHASE");
            RequireEnum(
                state.SpareBearingLotCustody,
                "SPARE_BEARING_LOT_CUSTODY");
            RequireNonnegative(
                state.SpareBearingElapsedTicks,
                "SPARE_BEARING_ELAPSED_TICKS");
            RequireNonnegative(
                state.SpareBearingRequiredTicks,
                "SPARE_BEARING_REQUIRED_TICKS");
            RequireNonnegative(
                state.SpareBearingLotQuantity,
                "SPARE_BEARING_LOT_QUANTITY");

            if (state.SpareBearingBatchPhase == SpareBearingBatchPhase.None)
            {
                Require(
                    state.SpareBearingRecipe == SpareBearingRecipe.None
                    && state.SpareBearingElapsedTicks == 0
                    && state.SpareBearingRequiredTicks == 0
                    && state.SpareBearingLotQuantity == 0
                    && state.SpareBearingLotCustody
                        == SpareBearingLotCustody.None,
                    "LAST_BEARING_EMPTY_SPARE_BEARING_BATCH_INVALID");
                return;
            }

            Require(
                state.SpareBearingRecipe
                    == SpareBearingRecipe.SpareBearingOneGoodBatch
                && state.DepotResolution == EncounterChoice.TakeBearing
                && state.PreparationChoice == PreparationChoice.CivicBuffer
                && state.VehicleModule == VehicleModule.WinchAssembly
                && state.ExpeditionPhase == ExpeditionPhase.AtHome
                && state.TransactionPhase == TransactionPhase.Finalized
                && state.TurbineCondition
                    == TurbineCondition.BearingRepaired
                && state.DepotControl == DepotControl.Depleted
                && state.DepotBearingDisposition
                    == DepotBearingDisposition.InstalledAtTurbine
                && state.FactionClaimState == FactionClaimState.Aggrieved
                && state.PendingFactionOutcome == FactionOutcomeKind.Adverse
                && state.FactionMemory != null
                && string.Equals(
                    state.FactionMemory.StableId,
                    "memory:last-bearing:take:0001",
                    StringComparison.Ordinal)
                && string.Equals(
                    state.FactionMemory.WitnessedAction,
                    "TakeClaimedBearing",
                    StringComparison.Ordinal)
                && string.Equals(
                    state.FactionMemory.AffectedFactionId,
                    LastBearingState.LastBearingFactionId,
                    StringComparison.Ordinal)
                && state.FactionMemory.Magnitude
                    == LastBearingBalanceV1.TakeGrievanceDelta
                && string.Equals(
                    state.FactionMemory.DoctrineTag,
                    "custody-breach",
                    StringComparison.Ordinal)
                && state.FactionMemory.EncounterTick <= state.GlobalTick
                && string.Equals(
                    state.FactionMemory.ConsequenceCode,
                    "DEPOT_ACCESS_CLOSED",
                    StringComparison.Ordinal)
                && state.FactionTrust == LastBearingBalanceV1.TakeTrustDelta
                && state.FactionGrievance
                    == LastBearingBalanceV1.TakeGrievanceDelta
                && state.FactionAidPolicy == FactionAidPolicy.Withheld
                && state.EmergencyAidWaterMilli == 0
                && state.DepotAccessFeePartsUnits
                    == (state.FactionClaimProgressMilli
                            == LastBearingBalanceV1.FactionClaimThresholdMilli
                        ? LastBearingBalanceV1
                            .ClaimedDepotAccessFeePartsUnits
                        : 0)
                && state.FutureRouteTollFuelUnits
                    == LastBearingBalanceV1.TakeFutureRouteTollFuelUnits
                && state.SpareBearingRequiredTicks
                    == LastBearingBalanceV1
                        .SpareBearingBatchRequiredSettlementTicks
                && state.PartsUnits
                    >= LastBearingBalanceV1
                        .SpareBearingBatchRetainedReservePartsUnits,
                "LAST_BEARING_SPARE_BEARING_LINEAGE_INVALID");

            if (state.SpareBearingBatchPhase
                == SpareBearingBatchPhase.InProgress)
            {
                Require(
                    state.SpareBearingElapsedTicks
                        < state.SpareBearingRequiredTicks
                    && state.SpareBearingLotQuantity == 0
                    && state.SpareBearingLotCustody
                        == SpareBearingLotCustody.None
                    && state.NextCityDecision
                        == NextCityDecision.MachineSpareBearing
                    && !state.RoutePermitGranted
                    && state.FactionAccessPolicy
                        == FactionAccessPolicy.Closed,
                    "LAST_BEARING_SPARE_BEARING_BATCH_PROGRESS_INVALID");
                return;
            }

            Require(
                state.SpareBearingElapsedTicks
                    == state.SpareBearingRequiredTicks
                && state.SpareBearingLotQuantity
                    == LastBearingBalanceV1.SpareBearingBatchOutputQuantity
                && state.NextCityDecision == NextCityDecision.None,
                "LAST_BEARING_SPARE_BEARING_BATCH_COMPLETION_INVALID");

            if (state.SpareBearingBatchPhase
                == SpareBearingBatchPhase.Complete)
            {
                Require(
                    state.SpareBearingLotCustody
                        == SpareBearingLotCustody.WorkshopOutput
                    && !state.RoutePermitGranted
                    && state.FactionAccessPolicy
                        == FactionAccessPolicy.Closed,
                    "LAST_BEARING_SPARE_BEARING_LOT_OUTPUT_INVALID");
                return;
            }

            Require(
                state.SpareBearingBatchPhase
                    == SpareBearingBatchPhase.Settled
                && state.SpareBearingLotCustody
                    == SpareBearingLotCustody.LastBearingClaimsCounter
                && state.RoutePermitGranted
                && state.FactionAccessPolicy
                    == FactionAccessPolicy.PermitRequired,
                "LAST_BEARING_SPARE_BEARING_SETTLEMENT_INVALID");
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
            if (value >= 0 &&
                value < LastBearingBalanceV1.FullClockScaleMilli)
            {
                return;
            }

            throw new InvalidOperationException(
                "LAST_BEARING_" + label + "_ACCUMULATOR_INVALID");
        }

        private static void RequireNonnegative(long value, string label)
        {
            if (value >= 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "LAST_BEARING_" + label + "_NEGATIVE");
        }

        private static void RequireRange(
            long value,
            long minimum,
            long maximum,
            string label)
        {
            if (value >= minimum && value <= maximum)
            {
                return;
            }

            throw new InvalidOperationException(
                "LAST_BEARING_" + label + "_OUT_OF_RANGE");
        }

        private static void RequireEnum<TEnum>(TEnum value, string label)
            where TEnum : struct, Enum
        {
            if (DefinedEnumValues<TEnum>.Values.Contains(value))
            {
                return;
            }

            throw new InvalidOperationException(
                "LAST_BEARING_" + label + "_INVALID");
        }

        private static ResidentRoster CanonicalRoster(
            ColonyComposition composition)
        {
            switch (composition)
            {
                case ColonyComposition.HumanOnly:
                    return HumanRoster;
                case ColonyComposition.RobotOnly:
                    return RobotRoster;
                case ColonyComposition.Mixed:
                    return MixedRoster;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(composition));
            }
        }

        private static class DefinedEnumValues<TEnum>
            where TEnum : struct, Enum
        {
            internal static readonly HashSet<TEnum> Values =
                new HashSet<TEnum>(
                    (TEnum[])Enum.GetValues(typeof(TEnum)));
        }
    }
}
