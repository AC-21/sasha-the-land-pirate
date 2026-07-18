#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingReadModel
    {
        private LastBearingReadModel(LastBearingState state)
        {
            GlobalTick = state.GlobalTick;
            SettlementTick = state.SettlementTick;
            FactionTick = state.FactionTick;
            CrisisTick = state.CrisisTick;
            RoadTick = state.RoadTick;
            Composition = state.Composition;
            Residents = state.Roster.Residents;
            AssignedResidentId = state.AssignedResidentId;
            WaterMilli = state.WaterMilli;
            WaterTrendMilliPerSettlementTick = ComputeWaterTrend(state);
            IsWaterRecovering = WaterTrendMilliPerSettlementTick > 0;
            PartsUnits = state.PartsUnits;
            FuelUnits = state.FuelUnits;
            TurbineCondition = state.TurbineCondition;
            PreparationChoice = state.PreparationChoice;
            PreparationPhase = state.PreparationPhase;
            PlannedModule = state.PlannedModule;
            VehicleModule = state.VehicleModule;
            ExpeditionPhase = state.ExpeditionPhase;
            TransactionPhase = state.TransactionPhase;
            RouteKind = state.RouteKind;
            RouteActionKind = state.RouteActionKind;
            RouteActionUsed = state.RouteActionUsed;
            RouteProgressTicks = state.RouteProgressTicks;
            RouteTargetTicks = state.RouteTargetTicks;
            WreckLineGateTicks = state.VehicleModule == VehicleModule.None
                ? 0
                : LastBearingBalanceV1.WreckLineGateTicks(
                    state.VehicleModule);
            VehicleLateralMilli = state.VehicleLateralMilli;
            VehicleConditionMilli = state.VehicleConditionMilli;
            RepairCargoKind = state.RepairCargoKind;
            RepairCargoCustody = state.RepairCargoCustody;
            HeavyCargoKind = state.HeavyCargoKind;
            HeavyCargoCustody = state.HeavyCargoCustody;
            LiquidCargoKind = state.LiquidCargoKind;
            LiquidCargoQuantityMilli = state.LiquidCargoQuantityMilli;
            LiquidCargoCustody = state.LiquidCargoCustody;
            FactionClaimProgressMilli = state.FactionClaimProgressMilli;
            FactionClaimState = state.FactionClaimState;
            DepotControl = state.DepotControl;
            FactionAccessPolicy = state.FactionAccessPolicy;
            FactionAidPolicy = state.FactionAidPolicy;
            FactionTrust = state.FactionTrust;
            FactionGrievance = state.FactionGrievance;
            MaintenanceRecipe = state.MaintenanceRecipe;
            MaintenanceObligationActive = state.MaintenanceObligationActive;
            MaintenanceDue = state.MaintenanceDue;
            NextCityDecision = state.NextCityDecision;
            InstalledCityImprovement = state.InstalledCityImprovement;
            IsCityImprovementInstallationAvailable =
                ComputeCityImprovementInstallationAvailable(state);
            SpareBearingRecipe = state.SpareBearingRecipe;
            SpareBearingBatchPhase = state.SpareBearingBatchPhase;
            SpareBearingElapsedTicks = state.SpareBearingElapsedTicks;
            SpareBearingRequiredTicks = state.SpareBearingRequiredTicks;
            SpareBearingLotQuantity = state.SpareBearingLotQuantity;
            SpareBearingLotCustody = state.SpareBearingLotCustody;
            RoutePermitGranted = state.RoutePermitGranted;
            FutureRouteTollFuelUnits = state.FutureRouteTollFuelUnits;
            IsSpareBearingBatchStartAvailable =
                ComputeSpareBearingBatchStartAvailable(state);
            IsSpareBearingBarterAvailable =
                ComputeSpareBearingBarterAvailable(state);
            SpareBearingRemainingTicks = Math.Max(
                0,
                state.SpareBearingRequiredTicks
                    - state.SpareBearingElapsedTicks);
            PauseCause = state.PauseCause;
            IsDepotApproachRecoveryAvailable =
                ComputeDepotApproachRecoveryAvailable(state);
            IsWreckLineModulePointAvailable =
                ComputeWreckLineModulePointAvailable(state);

            WaterZeroSettlementTicks = WaterTrendMilliPerSettlementTick < 0
                ? DivideCeiling(
                    state.WaterMilli,
                    checked(-WaterTrendMilliPerSettlementTick))
                : (long?)null;
            ClaimContestedFactionTicks = state.DepotResolution
                == EncounterChoice.Unresolved
                ? ThresholdTicks(
                    LastBearingBalanceV1.FactionContestedThresholdMilli,
                    state.FactionClaimProgressMilli,
                    LastBearingBalanceV1.FactionClaimRateMilliPerFactionTick)
                : 0;
            ClaimedFactionTicks = state.DepotResolution
                == EncounterChoice.Unresolved
                ? ThresholdTicks(
                    LastBearingBalanceV1.FactionClaimThresholdMilli,
                    state.FactionClaimProgressMilli,
                    LastBearingBalanceV1.FactionClaimRateMilliPerFactionTick)
                : 0;
            DustFrontCrisisTicks = Math.Max(
                0,
                LastBearingBalanceV1.DustFrontThresholdCrisisTicks
                    - state.DustFrontProgressTicks);
            RouteArrivalGlobalTicks = state.ExpeditionPhase
                == ExpeditionPhase.Outbound
                ? Math.Max(0, state.RouteTargetTicks - state.RouteProgressTicks)
                : state.ExpeditionPhase == ExpeditionPhase.AtDepot
                    ? 0
                    : (long?)null;
            RouteReturnGlobalTicks = state.ExpeditionPhase
                == ExpeditionPhase.Returning
                ? Math.Max(0, state.RouteTargetTicks - state.RouteProgressTicks)
                : state.ExpeditionPhase == ExpeditionPhase.Returned
                    ? 0
                    : (long?)null;
            NextObjective = ComputeNextObjective(state);
        }

        public long GlobalTick { get; }
        public long SettlementTick { get; }
        public long FactionTick { get; }
        public long CrisisTick { get; }
        public long RoadTick { get; }
        public ColonyComposition Composition { get; }
        public IReadOnlyList<ResidentRecord> Residents { get; }
        public string? AssignedResidentId { get; }
        public long WaterMilli { get; }
        public long WaterTrendMilliPerSettlementTick { get; }
        public bool IsWaterRecovering { get; }
        public long PartsUnits { get; }
        public long FuelUnits { get; }
        public TurbineCondition TurbineCondition { get; }
        public PreparationChoice PreparationChoice { get; }
        public PreparationPhase PreparationPhase { get; }
        public VehicleModule PlannedModule { get; }
        public VehicleModule VehicleModule { get; }
        public ExpeditionPhase ExpeditionPhase { get; }
        public TransactionPhase TransactionPhase { get; }
        public RouteKind RouteKind { get; }
        public RouteActionKind RouteActionKind { get; }
        public bool RouteActionUsed { get; }
        public long RouteProgressTicks { get; }
        public long RouteTargetTicks { get; }
        public long WreckLineGateTicks { get; }
        public int VehicleLateralMilli { get; }
        public long VehicleConditionMilli { get; }
        public RepairCargoKind RepairCargoKind { get; }
        public RepairCargoCustody RepairCargoCustody { get; }
        public HeavyCargoKind HeavyCargoKind { get; }
        public HeavyCargoCustody HeavyCargoCustody { get; }
        public LiquidCargoKind LiquidCargoKind { get; }
        public long LiquidCargoQuantityMilli { get; }
        public LiquidCargoCustody LiquidCargoCustody { get; }
        public long FactionClaimProgressMilli { get; }
        public FactionClaimState FactionClaimState { get; }
        public DepotControl DepotControl { get; }
        public FactionAccessPolicy FactionAccessPolicy { get; }
        public FactionAidPolicy FactionAidPolicy { get; }
        public long FactionTrust { get; }
        public long FactionGrievance { get; }
        public MaintenanceRecipe MaintenanceRecipe { get; }
        public bool MaintenanceObligationActive { get; }
        public bool MaintenanceDue { get; }
        public NextCityDecision NextCityDecision { get; }
        public CityImprovementKind InstalledCityImprovement { get; }
        public bool IsCityImprovementInstallationAvailable { get; }
        public SpareBearingRecipe SpareBearingRecipe { get; }
        public SpareBearingBatchPhase SpareBearingBatchPhase { get; }
        public long SpareBearingElapsedTicks { get; }
        public long SpareBearingRequiredTicks { get; }
        public long SpareBearingLotQuantity { get; }
        public SpareBearingLotCustody SpareBearingLotCustody { get; }
        public bool RoutePermitGranted { get; }
        public long FutureRouteTollFuelUnits { get; }
        public bool IsSpareBearingBatchStartAvailable { get; }
        public bool IsSpareBearingBarterAvailable { get; }
        public long SpareBearingRemainingTicks { get; }
        public PauseCause PauseCause { get; }
        public bool IsDepotApproachRecoveryAvailable { get; }
        public bool IsWreckLineModulePointAvailable { get; }
        public long? WaterZeroSettlementTicks { get; }
        public long ClaimContestedFactionTicks { get; }
        public long ClaimedFactionTicks { get; }
        public long DustFrontCrisisTicks { get; }
        public long? RouteArrivalGlobalTicks { get; }
        public long? RouteReturnGlobalTicks { get; }
        public string NextObjective { get; }

        public static LastBearingReadModel FromState(LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return new LastBearingReadModel(state);
        }

        internal static long ComputeWaterTrend(LastBearingState state)
        {
            long baseRate;
            switch (state.TurbineCondition)
            {
                case TurbineCondition.Failing:
                    baseRate =
                        LastBearingBalanceV1.FailingWaterRateMilliPerSettlementTick;
                    break;
                case TurbineCondition.BearingRepaired:
                    baseRate =
                        LastBearingBalanceV1.BearingRepairRateMilliPerSettlementTick;
                    break;
                case TurbineCondition.SleeveRepaired:
                    baseRate =
                        LastBearingBalanceV1.SleeveRepairRateMilliPerSettlementTick;
                    break;
                default:
                    throw new InvalidOperationException(
                        "LAST_BEARING_TURBINE_CONDITION_INVALID");
            }

            return checked(
                baseRate
                + state.ActiveWaterModifierMilliPerSettlementTick
                + LastBearingBalanceV1.CityImprovementWaterModifier(
                    state.InstalledCityImprovement));
        }

        private static long ThresholdTicks(
            long threshold,
            long current,
            long rate)
        {
            if (current >= threshold)
            {
                return 0;
            }

            return DivideCeiling(checked(threshold - current), rate);
        }

        private static long DivideCeiling(long numerator, long denominator)
        {
            if (numerator < 0 || denominator <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numerator));
            }

            if (numerator == 0)
            {
                return 0;
            }

            return checked(((numerator - 1) / denominator) + 1);
        }

        private static string ComputeNextObjective(LastBearingState state)
        {
            if (!state.SliceInfrastructureActive)
            {
                return "activate-slice-infrastructure";
            }

            if (state.AssignedResidentId == null)
            {
                return "assign-expedition-resident";
            }

            if (state.PreparationPhase == PreparationPhase.Unselected)
            {
                return "select-preparation-and-module";
            }

            if (state.PreparationPhase == PreparationPhase.Preparing)
            {
                return "complete-preparation";
            }

            if (state.TransactionPhase == TransactionPhase.None)
            {
                return "prepare-expedition-transaction";
            }

            if (state.TransactionPhase == TransactionPhase.Prepared)
            {
                return "debit-city-manifest";
            }

            if (state.ExpeditionPhase == ExpeditionPhase.Outbound)
            {
                if (ComputeWreckLineModulePointAvailable(state))
                {
                    return state.RouteActionKind == RouteActionKind.DeployWinch
                        ? "deploy-winch-at-wreck-line"
                        : "cross-wreck-line-dust-exposure";
                }

                return ComputeDepotApproachRecoveryAvailable(state)
                    ? "operate-depot-recovery-point"
                    : "drive-to-depot";
            }

            if (state.ExpeditionPhase == ExpeditionPhase.AtDepot
                && state.DepotResolution == EncounterChoice.Unresolved)
            {
                return "resolve-depot";
            }

            if (state.ExpeditionPhase == ExpeditionPhase.AtDepot)
            {
                return "freeze-return-payload";
            }

            if (state.ExpeditionPhase == ExpeditionPhase.Returning)
            {
                return "drive-home";
            }

            if (state.TransactionPhase == TransactionPhase.ReturnPending)
            {
                return "credit-city-return";
            }

            if (state.TransactionPhase == TransactionPhase.CityCredited)
            {
                return "finalize-expedition";
            }

            if (state.TurbineCondition == TurbineCondition.Failing
                && state.RepairCargoKind != RepairCargoKind.None)
            {
                return "install-turbine-repair";
            }

            if (ComputeCityImprovementInstallationAvailable(state))
            {
                return "install-refurbished-auxiliary-pump";
            }

            if (ComputeSpareBearingBatchStartAvailable(state))
            {
                return "start-one-good-batch";
            }

            if (state.SpareBearingBatchPhase
                == SpareBearingBatchPhase.InProgress)
            {
                return "machine-one-good-batch";
            }

            if (ComputeSpareBearingBarterAvailable(state))
            {
                return "barter-spare-bearing-at-claims-counter";
            }

            if (state.SpareBearingBatchPhase
                == SpareBearingBatchPhase.Settled)
            {
                return "route-permit-recorded";
            }

            if (state.NextCityDecision != NextCityDecision.None)
            {
                return "await-next-city-decision-authority";
            }

            return "observe-recovering-waterworks";
        }

        private static bool ComputeCityImprovementInstallationAvailable(
            LastBearingState state)
        {
            long requiredParts = checked(
                LastBearingBalanceV1.AuxiliaryPumpInstallationPartsUnits
                + LastBearingBalanceV1.MinimumPostReturnPartsUnits);
            return state.ExpeditionPhase == ExpeditionPhase.AtHome
                && state.TransactionPhase == TransactionPhase.Finalized
                && state.TurbineCondition != TurbineCondition.Failing
                && state.NextCityDecision
                    == NextCityDecision.RefurbishAuxiliaryPump
                && state.InstalledCityImprovement == CityImprovementKind.None
                && state.PreparationChoice == PreparationChoice.WorkshopPush
                && state.VehicleModule == VehicleModule.WinchAssembly
                && state.RouteActionUsed
                && state.HeavyCargoKind == HeavyCargoKind.PumpRotor
                && state.HeavyCargoCustody == HeavyCargoCustody.Settlement
                && state.TowSlotsUsed == 1
                && state.PartsUnits >= requiredParts;
        }

        private static bool ComputeSpareBearingBatchStartAvailable(
            LastBearingState state)
        {
            return state.SpareBearingBatchPhase == SpareBearingBatchPhase.None
                && state.SpareBearingRecipe == SpareBearingRecipe.None
                && state.SpareBearingElapsedTicks == 0
                && state.SpareBearingRequiredTicks == 0
                && state.SpareBearingLotQuantity == 0
                && state.SpareBearingLotCustody == SpareBearingLotCustody.None
                && state.DepotResolution == EncounterChoice.TakeBearing
                && state.PreparationChoice == PreparationChoice.CivicBuffer
                && state.VehicleModule == VehicleModule.WinchAssembly
                && state.ExpeditionPhase == ExpeditionPhase.AtHome
                && state.TransactionPhase == TransactionPhase.Finalized
                && state.TurbineCondition
                    == TurbineCondition.BearingRepaired
                && state.NextCityDecision
                    == NextCityDecision.MachineSpareBearing
                && state.DepotControl == DepotControl.Depleted
                && state.FactionClaimState == FactionClaimState.Aggrieved
                && state.FactionAccessPolicy == FactionAccessPolicy.Closed
                && state.FactionAidPolicy == FactionAidPolicy.Withheld
                && state.PendingFactionOutcome == FactionOutcomeKind.Adverse
                && state.FutureRouteTollFuelUnits
                    == LastBearingBalanceV1.TakeFutureRouteTollFuelUnits
                && !state.RoutePermitGranted
                && state.PartsUnits
                    >= LastBearingBalanceV1
                        .SpareBearingBatchMinimumPreStartPartsUnits;
        }

        private static bool ComputeSpareBearingBarterAvailable(
            LastBearingState state)
        {
            return state.SpareBearingRecipe
                    == SpareBearingRecipe.SpareBearingOneGoodBatch
                && state.SpareBearingBatchPhase
                    == SpareBearingBatchPhase.Complete
                && state.SpareBearingElapsedTicks
                    == LastBearingBalanceV1
                        .SpareBearingBatchRequiredSettlementTicks
                && state.SpareBearingRequiredTicks
                    == LastBearingBalanceV1
                        .SpareBearingBatchRequiredSettlementTicks
                && state.SpareBearingLotQuantity
                    == LastBearingBalanceV1.SpareBearingBatchOutputQuantity
                && state.SpareBearingLotCustody
                    == SpareBearingLotCustody.WorkshopOutput
                && state.FactionClaimState == FactionClaimState.Aggrieved
                && state.FactionAccessPolicy == FactionAccessPolicy.Closed
                && !state.RoutePermitGranted;
        }

        private static bool ComputeDepotApproachRecoveryAvailable(
            LastBearingState state)
        {
            return state.ExpeditionPhase == ExpeditionPhase.Outbound
                && state.TransactionPhase == TransactionPhase.RoadOwned
                && state.RouteActionUsed
                && state.RouteTargetTicks > 0
                && state.RouteProgressTicks == state.RouteTargetTicks
                && state.HasArrivalClaimSnapshot;
        }

        private static bool ComputeWreckLineModulePointAvailable(
            LastBearingState state)
        {
            return state.ExpeditionPhase == ExpeditionPhase.Outbound
                && state.TransactionPhase == TransactionPhase.RoadOwned
                && state.VehicleModule != VehicleModule.None
                && !state.RouteActionUsed
                && state.RouteProgressTicks ==
                    LastBearingBalanceV1.WreckLineGateTicks(
                        state.VehicleModule);
        }
    }
}
