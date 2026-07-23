#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingReadModel
    {
        private LastBearingReadModel(LastBearingState state)
        {
            CopyFrom(state);
        }

        internal void CopyFrom(LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            GlobalTick = state.GlobalTick;
            SettlementTick = state.SettlementTick;
            FactionTick = state.FactionTick;
            CrisisTick = state.CrisisTick;
            RoadTick = state.RoadTick;
            Composition = state.Composition;
            Residents = state.Roster.Residents;
            AssignedResidentId = state.AssignedResidentId;
            RecyclerPadIndex = state.RecyclerPadIndex;
            RecyclerQuarterTurns = state.RecyclerQuarterTurns;
            MachineShopPadIndex = state.MachineShopPadIndex;
            MachineShopQuarterTurns = state.MachineShopQuarterTurns;
            EmergencyStoragePadIndex = state.EmergencyStoragePadIndex;
            EmergencyStorageQuarterTurns = state.EmergencyStorageQuarterTurns;
            CityServiceLinkConnected = state.CityServiceLinkConnected;
            CityServiceResidentId = state.CityServiceResidentId;
            CityDeliveryStage = state.CityDeliveryStage;
            CityDeliveryCount = state.CityDeliveryCount;
            SliceInfrastructureActive = state.SliceInfrastructureActive;
            HotShiftPhase = state.HotShiftPhase;
            HotShiftElapsedTicks = state.HotShiftElapsedTicks;
            HotShiftRequiredTicks = state.HotShiftRequiredTicks;
            HotShiftRemainingTicks = Math.Max(
                0,
                state.HotShiftRequiredTicks
                    - state.HotShiftElapsedTicks);
            HotShiftFuelCostUnits =
                LastBearingBalanceV1.HotShiftFuelCostUnits;
            HotShiftOutputPartsUnits =
                LastBearingBalanceV1.HotShiftOutputPartsUnits;
            HotShiftWaterModifierMilliPerSettlementTick =
                LastBearingBalanceV1
                    .HotShiftWaterModifierMilliPerSettlementTick;
            HotShiftCompletedCount = state.HotShiftCompletedCount;
            IsHotShiftRunAvailable =
                ComputeHotShiftRunAvailable(state);
            IsHotShiftStalledByWorkshopPush =
                state.HotShiftPhase == HotShiftPhase.InProgress
                && state.WorkshopServiceSlotsReserved > 0;
            IsHotShiftStalledByDustFront =
                IsHotShiftBlockedByDustFront(state);
            IsHotShiftActivelyWorking =
                state.HotShiftPhase == HotShiftPhase.InProgress
                && state.WorkshopServiceSlotsReserved == 0
                && !IsHotShiftStalledByDustFront;
            WaterMilli = state.WaterMilli;
            WaterTrendMilliPerSettlementTick = ComputeWaterTrend(state);
            IsWaterRecovering = WaterTrendMilliPerSettlementTick > 0;
            PartsUnits = state.PartsUnits;
            FuelUnits = state.FuelUnits;
            TurbineCondition = state.TurbineCondition;
            PreparationChoice = state.PreparationChoice;
            PreparationPhase = state.PreparationPhase;
            PreparationElapsedTicks = state.PreparationElapsedTicks;
            PreparationRequiredTicks = state.PreparationRequiredTicks;
            PreparationRemainingTicks = Math.Max(
                0,
                state.PreparationRequiredTicks
                    - state.PreparationElapsedTicks);
            PlannedModule = state.PlannedModule;
            VehicleModule = state.VehicleModule;
            RigUpgrade = state.RigUpgrade;
            PatchworkSkidPlatePartsCostUnits =
                LastBearingBalanceV1.PatchworkSkidPlatePartsCostUnits;
            PatchworkSkidPlateProtectionMilli =
                LastBearingBalanceV1.PatchworkSkidPlateProtectionMilli;
            IsPatchworkSkidPlateInstallAvailable =
                ComputePatchworkSkidPlateInstallAvailable(state);
            VehicleModule projectedModule = state.VehicleModule
                != VehicleModule.None
                ? state.VehicleModule
                : state.PlannedModule;
            ProjectedRoundTripConditionLossMilli =
                projectedModule == VehicleModule.None
                    ? 0
                    : LastBearingBalanceV1.RouteConditionLoss(
                        projectedModule,
                        state.RigUpgrade);
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
            FrameRailSalvageCustody =
                state.FrameRailSalvageCustody;
            FrameRailSalvagePartsUnits =
                LastBearingBalanceV1
                    .WreckLineFrameRailSalvagePartsUnits;
            FrameRailSalvageCargoUnits =
                LastBearingBalanceV1
                    .WreckLineFrameRailSalvageCargoUnits;
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
            IsWreckLineFrameRailRecoveryAvailable =
                ComputeWreckLineFrameRailRecoveryAvailable(state);
            IsRepairCargoLoadAvailable =
                ComputeRepairCargoLoadAvailable(state);

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
            DustFrontOutcome = state.DustFrontOutcome;
            IsDustFrontAcknowledgementRequired =
                state.IsDustFrontAcknowledgementRequired;
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

        public long GlobalTick { get; private set; }
        public long SettlementTick { get; private set; }
        public long FactionTick { get; private set; }
        public long CrisisTick { get; private set; }
        public long RoadTick { get; private set; }
        public ColonyComposition Composition { get; private set; }
        public IReadOnlyList<ResidentRecord> Residents { get; private set; } =
            Array.Empty<ResidentRecord>();
        public string? AssignedResidentId { get; private set; }
        public int RecyclerPadIndex { get; private set; }
        public int RecyclerQuarterTurns { get; private set; }
        public int MachineShopPadIndex { get; private set; }
        public int MachineShopQuarterTurns { get; private set; }
        public int EmergencyStoragePadIndex { get; private set; }
        public int EmergencyStorageQuarterTurns { get; private set; }
        public bool CityServiceLinkConnected { get; private set; }
        public string? CityServiceResidentId { get; private set; }
        public CityDeliveryStage CityDeliveryStage { get; private set; }
        public int CityDeliveryCount { get; private set; }
        public bool SliceInfrastructureActive { get; private set; }
        public HotShiftPhase HotShiftPhase { get; private set; }
        public long HotShiftElapsedTicks { get; private set; }
        public long HotShiftRequiredTicks { get; private set; }
        public long HotShiftRemainingTicks { get; private set; }
        public long HotShiftFuelCostUnits { get; private set; }
        public long HotShiftOutputPartsUnits { get; private set; }
        public long HotShiftWaterModifierMilliPerSettlementTick
        {
            get;
            private set;
        }
        public long HotShiftCompletedCount { get; private set; }
        public bool IsHotShiftRunAvailable { get; private set; }
        public bool IsHotShiftStalledByWorkshopPush { get; private set; }
        public bool IsHotShiftStalledByDustFront { get; private set; }
        public bool IsHotShiftActivelyWorking { get; private set; }
        public long WaterMilli { get; private set; }
        public long WaterTrendMilliPerSettlementTick { get; private set; }
        public bool IsWaterRecovering { get; private set; }
        public long PartsUnits { get; private set; }
        public long FuelUnits { get; private set; }
        public TurbineCondition TurbineCondition { get; private set; }
        public PreparationChoice PreparationChoice { get; private set; }
        public PreparationPhase PreparationPhase { get; private set; }
        public long PreparationElapsedTicks { get; private set; }
        public long PreparationRequiredTicks { get; private set; }
        public long PreparationRemainingTicks { get; private set; }
        public VehicleModule PlannedModule { get; private set; }
        public VehicleModule VehicleModule { get; private set; }
        public RigUpgrade RigUpgrade { get; private set; }
        public long PatchworkSkidPlatePartsCostUnits { get; private set; }
        public long PatchworkSkidPlateProtectionMilli { get; private set; }
        public bool IsPatchworkSkidPlateInstallAvailable { get; private set; }
        public long ProjectedRoundTripConditionLossMilli { get; private set; }
        public ExpeditionPhase ExpeditionPhase { get; private set; }
        public TransactionPhase TransactionPhase { get; private set; }
        public RouteKind RouteKind { get; private set; }
        public RouteActionKind RouteActionKind { get; private set; }
        public bool RouteActionUsed { get; private set; }
        public long RouteProgressTicks { get; private set; }
        public long RouteTargetTicks { get; private set; }
        public long WreckLineGateTicks { get; private set; }
        public int VehicleLateralMilli { get; private set; }
        public long VehicleConditionMilli { get; private set; }
        public RepairCargoKind RepairCargoKind { get; private set; }
        public RepairCargoCustody RepairCargoCustody { get; private set; }
        public FrameRailSalvageCustody FrameRailSalvageCustody
        {
            get;
            private set;
        }
        public long FrameRailSalvagePartsUnits { get; private set; }
        public long FrameRailSalvageCargoUnits { get; private set; }
        public HeavyCargoKind HeavyCargoKind { get; private set; }
        public HeavyCargoCustody HeavyCargoCustody { get; private set; }
        public LiquidCargoKind LiquidCargoKind { get; private set; }
        public long LiquidCargoQuantityMilli { get; private set; }
        public LiquidCargoCustody LiquidCargoCustody { get; private set; }
        public long FactionClaimProgressMilli { get; private set; }
        public FactionClaimState FactionClaimState { get; private set; }
        public DepotControl DepotControl { get; private set; }
        public FactionAccessPolicy FactionAccessPolicy { get; private set; }
        public FactionAidPolicy FactionAidPolicy { get; private set; }
        public long FactionTrust { get; private set; }
        public long FactionGrievance { get; private set; }
        public MaintenanceRecipe MaintenanceRecipe { get; private set; }
        public bool MaintenanceObligationActive { get; private set; }
        public bool MaintenanceDue { get; private set; }
        public NextCityDecision NextCityDecision { get; private set; }
        public CityImprovementKind InstalledCityImprovement { get; private set; }
        public bool IsCityImprovementInstallationAvailable { get; private set; }
        public SpareBearingRecipe SpareBearingRecipe { get; private set; }
        public SpareBearingBatchPhase SpareBearingBatchPhase { get; private set; }
        public long SpareBearingElapsedTicks { get; private set; }
        public long SpareBearingRequiredTicks { get; private set; }
        public long SpareBearingLotQuantity { get; private set; }
        public SpareBearingLotCustody SpareBearingLotCustody { get; private set; }
        public bool RoutePermitGranted { get; private set; }
        public long FutureRouteTollFuelUnits { get; private set; }
        public bool IsSpareBearingBatchStartAvailable { get; private set; }
        public bool IsSpareBearingBarterAvailable { get; private set; }
        public long SpareBearingRemainingTicks { get; private set; }
        public PauseCause PauseCause { get; private set; }
        public bool IsDepotApproachRecoveryAvailable { get; private set; }
        public bool IsWreckLineModulePointAvailable { get; private set; }
        public bool IsWreckLineFrameRailRecoveryAvailable
        {
            get;
            private set;
        }
        public bool IsRepairCargoLoadAvailable { get; private set; }
        public long? WaterZeroSettlementTicks { get; private set; }
        public long ClaimContestedFactionTicks { get; private set; }
        public long ClaimedFactionTicks { get; private set; }
        public long DustFrontCrisisTicks { get; private set; }
        public DustFrontOutcome DustFrontOutcome { get; private set; }
        public bool IsDustFrontAcknowledgementRequired { get; private set; }
        public long? RouteArrivalGlobalTicks { get; private set; }
        public long? RouteReturnGlobalTicks { get; private set; }
        public string NextObjective { get; private set; } = string.Empty;

        public static LastBearingReadModel FromState(LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return new LastBearingReadModel(state);
        }

        internal static LastBearingReadModel CreateReusable(
            LastBearingState state)
        {
            return FromState(state);
        }

        internal void RefreshFrom(LastBearingState state)
        {
            LastBearingInvariants.Validate(state);
            CopyFrom(state);
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
                + (state.HotShiftPhase == HotShiftPhase.InProgress
                        && state.WorkshopServiceSlotsReserved == 0
                        && !IsHotShiftBlockedByDustFront(state)
                    ? LastBearingBalanceV1
                        .HotShiftWaterModifierMilliPerSettlementTick
                    : 0)
                + LastBearingBalanceV1.CityImprovementWaterModifier(
                    state.InstalledCityImprovement));
        }

        private static bool ComputeHotShiftRunAvailable(
            LastBearingState state)
        {
            return state.SliceInfrastructureActive
                && state.HotShiftPhase == HotShiftPhase.Idle
                && state.PreparationChoice
                    != PreparationChoice.Unselected
                && state.PlannedModule != VehicleModule.None
                && state.ModuleInstallationState
                    != ModuleInstallationState.None
                && state.ExpeditionPhase == ExpeditionPhase.AtHome
                && state.FuelUnits
                    >= checked(
                        LastBearingBalanceV1.HotShiftFuelCostUnits
                        + LastBearingBalanceV1.RouteFuelCost(
                            state.PlannedModule));
        }

        private static bool IsHotShiftBlockedByDustFront(
            LastBearingState state)
        {
            return state.DustFrontOutcome == DustFrontOutcome.Breached
                && state.TurbineCondition == TurbineCondition.Failing;
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
                if (state.RecyclerPadIndex
                    == LastBearingState.UnplacedCityPadIndex)
                {
                    return "place-city-recycler";
                }

                if (state.MachineShopPadIndex
                    == LastBearingState.UnplacedCityPadIndex)
                {
                    return "place-city-machine-shop";
                }

                if (state.EmergencyStoragePadIndex
                    == LastBearingState.UnplacedCityPadIndex)
                {
                    return "place-city-emergency-storage";
                }

                if (!state.CityServiceLinkConnected)
                {
                    return "connect-city-service-link";
                }

                if (state.CityServiceResidentId == null)
                {
                    return "staff-city-service-cell";
                }

                return "advance-city-service-sled";
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

                if (ComputeWreckLineFrameRailRecoveryAvailable(state))
                {
                    return "recover-wreck-line-frame-rails";
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
                if (ComputeRepairCargoLoadAvailable(state))
                {
                    return "load-depot-repair-cargo";
                }

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

        private static bool ComputePatchworkSkidPlateInstallAvailable(
            LastBearingState state)
        {
            return state.RigUpgrade == RigUpgrade.None
                && state.SliceInfrastructureActive
                && state.ExpeditionPhase == ExpeditionPhase.AtHome
                && state.TransactionPhase == TransactionPhase.None
                && state.PartsUnits
                    >= LastBearingBalanceV1
                        .PatchworkSkidPlatePartsCostUnits;
        }

        private static bool ComputeRepairCargoLoadAvailable(
            LastBearingState state)
        {
            if (state.ExpeditionPhase != ExpeditionPhase.AtDepot
                || state.TransactionPhase != TransactionPhase.RoadOwned
                || state.DepotResolution == EncounterChoice.Unresolved
                || state.ReturnPayloadFrozen
                || state.OrdinaryCargoUsedUnits
                    != FrameRailCargoUnits(state)
                || checked(state.OrdinaryCargoUsedUnits + 1)
                    > state.OrdinaryCargoCapacityUnits)
            {
                return false;
            }

            if (state.DepotResolution == EncounterChoice.Cooperate)
            {
                return state.RepairCargoKind == RepairCargoKind.FieldSleeve
                    && state.RepairCargoCustody
                        == RepairCargoCustody.Faction;
            }

            RepairCargoCustody expectedSource =
                state.DepotBearingDisposition
                    == DepotBearingDisposition.AtDepot
                    ? RepairCargoCustody.Depot
                    : state.DepotBearingDisposition
                            == DepotBearingDisposition.FactionHeld
                        ? RepairCargoCustody.Faction
                        : RepairCargoCustody.None;
            return state.RepairCargoKind == RepairCargoKind.CeramicBearing
                && expectedSource != RepairCargoCustody.None
                && state.RepairCargoCustody == expectedSource;
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

        private static bool ComputeWreckLineFrameRailRecoveryAvailable(
            LastBearingState state)
        {
            return state.ExpeditionPhase == ExpeditionPhase.Outbound
                && state.TransactionPhase == TransactionPhase.RoadOwned
                && state.RigUpgrade == RigUpgrade.PatchworkSkidPlate
                && state.RouteActionUsed
                && state.FrameRailSalvageCustody
                    == FrameRailSalvageCustody.WreckLine
                && state.RouteProgressTicks ==
                    LastBearingBalanceV1.WreckLineGateTicks(
                        state.VehicleModule)
                && checked(
                    state.OrdinaryCargoUsedUnits
                    + LastBearingBalanceV1
                        .WreckLineFrameRailSalvageCargoUnits)
                    <= state.OrdinaryCargoCapacityUnits;
        }

        private static long FrameRailCargoUnits(LastBearingState state)
        {
            return state.FrameRailSalvageCustody
                    == FrameRailSalvageCustody.Vehicle
                ? LastBearingBalanceV1
                    .WreckLineFrameRailSalvageCargoUnits
                : 0;
        }
    }
}
