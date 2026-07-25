#nullable enable

using System;
using System.Globalization;

namespace AtomicLandPirate.Simulation.LastBearing
{
    internal static class LastBearingRepeatExpedition
    {
        private const string TransactionPrefix = "tx:repeat:";
        private const string FingerprintPrefix = "fp:repeat:";

        internal static bool IsReservedIdentity(
            string transactionId,
            string fingerprint)
        {
            return transactionId.StartsWith(
                    TransactionPrefix,
                    StringComparison.Ordinal)
                || fingerprint.StartsWith(
                    FingerprintPrefix,
                    StringComparison.Ordinal);
        }

        internal static void RequireFreshIdentity(
            long sequence,
            string transactionId,
            string fingerprint)
        {
            if (!TryGetIdentitySequence(
                    transactionId,
                    fingerprint,
                    out long identitySequence)
                || identitySequence != sequence)
            {
                throw new ArgumentException(
                    "LAST_BEARING_REPEAT_TRANSACTION_IDENTITY_INVALID",
                    nameof(transactionId));
            }
        }

        internal static bool IsLineage(LastBearingState state)
        {
            if (state == null
                || !TryGetIdentitySequence(
                    state.TransactionId,
                    state.TransactionFingerprint,
                    out long identitySequence)
                || identitySequence >= state.NextCommandSequence
                || !HasPermanentFirstReturnHistory(state))
            {
                return false;
            }

            long salvageUnits =
                LastBearingBalanceV1.WreckLineFrameRailSalvageCargoUnits;
            switch (state.TransactionPhase)
            {
                case TransactionPhase.Prepared:
                case TransactionPhase.CityDebited:
                    return state.ExpeditionPhase == ExpeditionPhase.AtHome
                        && state.VehicleConditionMilli
                            == LastBearingBalanceV1
                                .StartingVehicleConditionMilli
                        && state.ExpeditionFuelManifestUnits
                            == (state.TransactionPhase
                                    == TransactionPhase.Prepared
                                ? 0
                                : FuelCost(state))
                        && state.RouteProgressTicks == 0
                        && state.RouteMovementAccumulatorMilli == 0
                        && state.VehicleLateralMilli == 0
                        && !state.RouteActionUsed
                        && !state.ReturnPayloadFrozen
                        && !state.HasArrivalClaimSnapshot
                        && state.ArrivalFactionClaimProgressMilli == 0
                        && state.FrameRailSalvageCustody
                            == FrameRailSalvageCustody.WreckLine
                        && state.OrdinaryCargoUsedUnits == 0
                        && state.TowSlotsUsed == 0;
                case TransactionPhase.RoadOwned:
                    return (state.ExpeditionPhase
                                == ExpeditionPhase.Outbound
                            || state.ExpeditionPhase
                                == ExpeditionPhase.AtDepot)
                        && HasReachablePreCreditCondition(state)
                        && state.ExpeditionFuelManifestUnits
                            == FuelCost(state)
                        && !state.ReturnPayloadFrozen
                        && state.TowSlotsUsed == 0
                        && HasActiveSalvageShape(
                            state,
                            salvageUnits);
                case TransactionPhase.ReturnPending:
                    return (state.ExpeditionPhase
                                == ExpeditionPhase.Returning
                            || state.ExpeditionPhase
                                == ExpeditionPhase.Returned)
                        && HasReachablePreCreditCondition(state)
                        && state.ExpeditionFuelManifestUnits
                            == FuelCost(state)
                        && state.RouteActionUsed
                        && state.ReturnPayloadFrozen
                        && state.HasArrivalClaimSnapshot
                        && state.FrameRailSalvageCustody
                            == FrameRailSalvageCustody.Vehicle
                        && state.OrdinaryCargoUsedUnits
                            == salvageUnits
                        && state.TowSlotsUsed == 0;
                case TransactionPhase.CityCredited:
                    return state.ExpeditionPhase == ExpeditionPhase.Returned
                        && HasReachableCreditedCondition(state)
                        && HasFinalizedSalvageShape(state);
                case TransactionPhase.Finalized:
                    return state.ExpeditionPhase == ExpeditionPhase.AtHome
                        && (HasReachableCreditedCondition(state)
                            || state.VehicleConditionMilli
                                == LastBearingBalanceV1
                                    .StartingVehicleConditionMilli)
                        && HasFinalizedSalvageShape(state);
                default:
                    return false;
            }
        }

        internal static bool CanLaunchFromCompletedReturn(
            LastBearingState state)
        {
            if (state == null
                || state.ExpeditionPhase != ExpeditionPhase.AtHome
                || state.TransactionPhase != TransactionPhase.Finalized
                || state.TransactionId == null
                || state.TransactionFingerprint == null
                || state.VehicleConditionMilli
                    != LastBearingBalanceV1.StartingVehicleConditionMilli
                || !state.RouteActionUsed
                || !state.ReturnPayloadFrozen
                || !state.HasArrivalClaimSnapshot
                || !HasPermanentFirstReturnHistory(state))
            {
                return false;
            }

            return IsReservedIdentity(
                    state.TransactionId,
                    state.TransactionFingerprint)
                ? IsLineage(state)
                : true;
        }

        internal static long FuelCost(LastBearingState state)
        {
            return checked(
                LastBearingBalanceV1.RouteFuelCost(state.VehicleModule)
                + state.FutureRouteTollFuelUnits);
        }

        private static bool HasReachablePreCreditCondition(
            LastBearingState state)
        {
            long maximumEdgeProgressTicks;
            switch (state.ExpeditionPhase)
            {
                case ExpeditionPhase.Outbound:
                case ExpeditionPhase.AtDepot:
                    maximumEdgeProgressTicks = state.RouteProgressTicks;
                    break;
                case ExpeditionPhase.Returning:
                case ExpeditionPhase.Returned:
                    maximumEdgeProgressTicks = checked(
                        state.RouteTargetTicks
                        + state.RouteProgressTicks);
                    break;
                default:
                    return false;
            }

            return IsReachableCondition(
                state.VehicleConditionMilli,
                maximumEdgeProgressTicks,
                fixedLossMilli: 0);
        }

        private static bool HasReachableCreditedCondition(
            LastBearingState state)
        {
            return IsReachableCondition(
                state.VehicleConditionMilli,
                checked(
                    state.RouteTargetTicks
                    + state.RouteProgressTicks),
                LastBearingBalanceV1.RouteConditionLoss(
                    state.VehicleModule,
                    state.RigUpgrade,
                    state.ReturnedRailChassisBraceInstalled));
        }

        private static bool IsReachableCondition(
            long conditionMilli,
            long maximumEdgeProgressTicks,
            long fixedLossMilli)
        {
            long startingCondition =
                LastBearingBalanceV1.StartingVehicleConditionMilli;
            long maximumCondition = Math.Max(
                0,
                checked(startingCondition - fixedLossMilli));
            long minimumCondition = Math.Max(
                0,
                checked(
                    startingCondition
                    - (maximumEdgeProgressTicks
                        * LastBearingBalanceV1
                            .RoadEdgeConditionLossPerProgressTickMilli)
                    - fixedLossMilli));
            return conditionMilli >= minimumCondition
                && conditionMilli <= maximumCondition;
        }

        private static bool HasActiveSalvageShape(
            LastBearingState state,
            long salvageUnits)
        {
            if (state.FrameRailSalvageCustody
                == FrameRailSalvageCustody.WreckLine)
            {
                return state.OrdinaryCargoUsedUnits == 0;
            }

            return state.FrameRailSalvageCustody
                    == FrameRailSalvageCustody.Vehicle
                && state.RouteActionUsed
                && state.OrdinaryCargoUsedUnits == salvageUnits;
        }

        private static bool HasFinalizedSalvageShape(
            LastBearingState state)
        {
            return state.ExpeditionFuelManifestUnits == FuelCost(state)
                && state.RouteActionUsed
                && state.ReturnPayloadFrozen
                && state.HasArrivalClaimSnapshot
                && state.FrameRailSalvageCustody
                    == FrameRailSalvageCustody.Credited
                && state.OrdinaryCargoUsedUnits == 0
                && state.TowSlotsUsed == 0;
        }

        private static bool HasPermanentFirstReturnHistory(
            LastBearingState state)
        {
            if (!state.SliceInfrastructureActive
                || state.AssignedResidentId == null
                || state.CityDeliveryStage
                    != CityDeliveryStage.DeliveredToWorkshop
                || state.ModuleInstallationState
                    != ModuleInstallationState.Installed
                || state.PreparationPhase
                    != PreparationPhase.Committed
                || state.NextCityDecision != NextCityDecision.None
                || state.DepotResolution == EncounterChoice.Unresolved
                || state.FactionMemory == null
                || state.FactionMemory.EncounterTick > state.GlobalTick)
            {
                return false;
            }

            return state.DepotResolution == EncounterChoice.Cooperate
                ? HasCooperativeHistory(state)
                : HasAdverseHistory(state);
        }

        private static bool HasCooperativeHistory(
            LastBearingState state)
        {
            return state.TurbineCondition
                    == TurbineCondition.SleeveRepaired
                && state.RepairCargoKind == RepairCargoKind.FieldSleeve
                && state.RepairCargoCustody
                    == RepairCargoCustody.Consumed
                && state.DepotBearingDisposition
                    == DepotBearingDisposition.FactionHeld
                && state.DepotControl == DepotControl.SharedAccess
                && state.FactionClaimState
                    == FactionClaimState.Cooperating
                && state.FactionAccessPolicy
                    == FactionAccessPolicy.SharedService
                && state.FactionAidPolicy
                    == FactionAidPolicy.EmergencyWaterDelivered
                && state.EmergencyAidWaterMilli
                    == LastBearingBalanceV1.CooperateAidWaterMilli
                && state.PendingFactionOutcome
                    == FactionOutcomeKind.Cooperative
                && state.FactionOutcomeElapsedTicks
                    >= LastBearingBalanceV1
                        .FactionOutcomeMaturationTicks
                && state.FactionTrust
                    == LastBearingBalanceV1.CooperateTrustDelta
                && state.FactionGrievance == 0
                && state.DepotAccessFeePartsUnits == 0
                && state.FutureRouteTollFuelUnits == 0
                && state.RoutePermitGranted
                && state.MaintenanceRecipe
                    == MaintenanceRecipe.FieldSleeveService
                && state.MaintenanceObligationActive
                && state.MaintenancePartsUnits
                    == LastBearingBalanceV1.SleeveMaintenancePartsUnits
                && HasFactionMemory(
                    state,
                    "memory:last-bearing:cooperate:0001",
                    "CooperateAtBearingDepot",
                    LastBearingBalanceV1.CooperateTrustDelta,
                    "shared-maintenance",
                    "FIELD_SLEEVE_SERVICE");
        }

        private static bool HasAdverseHistory(LastBearingState state)
        {
            bool accessResolved =
                (state.FactionAccessPolicy
                        == FactionAccessPolicy.Closed
                    && !state.RoutePermitGranted)
                || (state.FactionAccessPolicy
                        == FactionAccessPolicy.PermitRequired
                    && state.RoutePermitGranted);
            return state.TurbineCondition
                    == TurbineCondition.BearingRepaired
                && state.RepairCargoKind
                    == RepairCargoKind.CeramicBearing
                && state.RepairCargoCustody
                    == RepairCargoCustody.Turbine
                && state.DepotBearingDisposition
                    == DepotBearingDisposition.InstalledAtTurbine
                && state.DepotControl == DepotControl.Depleted
                && state.FactionClaimState == FactionClaimState.Aggrieved
                && accessResolved
                && state.FactionAidPolicy == FactionAidPolicy.Withheld
                && state.EmergencyAidWaterMilli == 0
                && state.PendingFactionOutcome
                    == FactionOutcomeKind.Adverse
                && state.FactionTrust
                    == LastBearingBalanceV1.TakeTrustDelta
                && state.FactionGrievance
                    == LastBearingBalanceV1.TakeGrievanceDelta
                && state.FutureRouteTollFuelUnits
                    == LastBearingBalanceV1.TakeFutureRouteTollFuelUnits
                && !state.MaintenanceObligationActive
                && state.MaintenanceRecipe == MaintenanceRecipe.None
                && state.MaintenancePartsUnits == 0
                && HasFactionMemory(
                    state,
                    "memory:last-bearing:take:0001",
                    "TakeClaimedBearing",
                    LastBearingBalanceV1.TakeGrievanceDelta,
                    "custody-breach",
                    "DEPOT_ACCESS_CLOSED");
        }

        private static bool HasFactionMemory(
            LastBearingState state,
            string stableId,
            string action,
            long magnitude,
            string doctrine,
            string consequence)
        {
            FactionMemoryRecord memory = state.FactionMemory!;
            return string.Equals(
                    memory.StableId,
                    stableId,
                    StringComparison.Ordinal)
                && string.Equals(
                    memory.WitnessedAction,
                    action,
                    StringComparison.Ordinal)
                && string.Equals(
                    memory.AffectedFactionId,
                    LastBearingState.LastBearingFactionId,
                    StringComparison.Ordinal)
                && memory.Magnitude == magnitude
                && string.Equals(
                    memory.DoctrineTag,
                    doctrine,
                    StringComparison.Ordinal)
                && string.Equals(
                    memory.ConsequenceCode,
                    consequence,
                    StringComparison.Ordinal);
        }

        private static bool TryGetIdentitySequence(
            string? transactionId,
            string? fingerprint,
            out long sequence)
        {
            sequence = -1;
            if (transactionId == null
                || fingerprint == null
                || !transactionId.StartsWith(
                    TransactionPrefix,
                    StringComparison.Ordinal)
                || !fingerprint.StartsWith(
                    FingerprintPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string transactionSuffix =
                transactionId.Substring(TransactionPrefix.Length);
            string fingerprintSuffix =
                fingerprint.Substring(FingerprintPrefix.Length);
            if (transactionSuffix.Length == 0
                || !string.Equals(
                    transactionSuffix,
                    fingerprintSuffix,
                    StringComparison.Ordinal)
                || !long.TryParse(
                    transactionSuffix,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out sequence)
                || sequence < 0
                || !string.Equals(
                    transactionSuffix,
                    sequence.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                sequence = -1;
                return false;
            }

            return true;
        }
    }
}
