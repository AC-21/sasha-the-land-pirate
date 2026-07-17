#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingState
    {
        public const int CurrentSchemaVersion = 1;
        public const string SashaProtagonistId = "sasha";
        public const string LastBearingFactionId = "faction:last-bearing:caravaners";

        internal LastBearingState(LastBearingStateBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            SchemaVersion = builder.SchemaVersion;
            BalanceRevision = builder.BalanceRevision;
            WorldSeed = builder.WorldSeed;
            GlobalTick = builder.GlobalTick;
            SettlementTick = builder.SettlementTick;
            FactionTick = builder.FactionTick;
            CrisisTick = builder.CrisisTick;
            RoadTick = builder.RoadTick;
            SettlementAccumulatorMilli = builder.SettlementAccumulatorMilli;
            FactionAccumulatorMilli = builder.FactionAccumulatorMilli;
            CrisisAccumulatorMilli = builder.CrisisAccumulatorMilli;
            RoadAccumulatorMilli = builder.RoadAccumulatorMilli;
            NextCommandSequence = builder.NextCommandSequence;
            ProtagonistId = builder.ProtagonistId;
            Roster = builder.Roster;
            AssignedResidentId = builder.AssignedResidentId;
            PauseCause = builder.PauseCause;
            SliceInfrastructureActive = builder.SliceInfrastructureActive;

            WaterMilli = builder.WaterMilli;
            PartsUnits = builder.PartsUnits;
            FuelUnits = builder.FuelUnits;
            TurbineCondition = builder.TurbineCondition;
            PreparationChoice = builder.PreparationChoice;
            PreparationPhase = builder.PreparationPhase;
            PlannedModule = builder.PlannedModule;
            PreparationElapsedTicks = builder.PreparationElapsedTicks;
            PreparationRequiredTicks = builder.PreparationRequiredTicks;
            PreparationFuelDebitedUnits = builder.PreparationFuelDebitedUnits;
            WorkshopServiceSlotsReserved = builder.WorkshopServiceSlotsReserved;
            ActiveWaterModifierMilliPerSettlementTick =
                builder.ActiveWaterModifierMilliPerSettlementTick;
            NextCityDecision = builder.NextCityDecision;

            VehicleModule = builder.VehicleModule;
            ModuleInstallationState = builder.ModuleInstallationState;
            RouteKind = builder.RouteKind;
            RouteActionKind = builder.RouteActionKind;
            RouteActionUsed = builder.RouteActionUsed;
            ExpeditionPhase = builder.ExpeditionPhase;
            RouteProgressTicks = builder.RouteProgressTicks;
            RouteTargetTicks = builder.RouteTargetTicks;
            RouteMovementAccumulatorMilli = builder.RouteMovementAccumulatorMilli;
            VehicleLateralMilli = builder.VehicleLateralMilli;
            VehicleConditionMilli = builder.VehicleConditionMilli;
            ExpeditionFuelManifestUnits = builder.ExpeditionFuelManifestUnits;
            OrdinaryCargoCapacityUnits = builder.OrdinaryCargoCapacityUnits;
            OrdinaryCargoUsedUnits = builder.OrdinaryCargoUsedUnits;
            TowSlots = builder.TowSlots;
            TowSlotsUsed = builder.TowSlotsUsed;
            LiquidCapacityMilli = builder.LiquidCapacityMilli;
            HeavyCargoKind = builder.HeavyCargoKind;
            HeavyCargoCustody = builder.HeavyCargoCustody;
            LiquidCargoKind = builder.LiquidCargoKind;
            LiquidCargoQuantityMilli = builder.LiquidCargoQuantityMilli;
            LiquidCargoCustody = builder.LiquidCargoCustody;
            RepairCargoKind = builder.RepairCargoKind;
            RepairCargoCustody = builder.RepairCargoCustody;
            DepotBearingDisposition = builder.DepotBearingDisposition;
            ReturnPayloadFrozen = builder.ReturnPayloadFrozen;
            HasArrivalClaimSnapshot = builder.HasArrivalClaimSnapshot;
            ArrivalFactionClaimProgressMilli =
                builder.ArrivalFactionClaimProgressMilli;
            ArrivalFactionClaimState = builder.ArrivalFactionClaimState;

            TransactionId = builder.TransactionId;
            TransactionFingerprint = builder.TransactionFingerprint;
            TransactionPhase = builder.TransactionPhase;
            DepotResolution = builder.DepotResolution;

            FactionClaimProgressMilli = builder.FactionClaimProgressMilli;
            FactionClaimState = builder.FactionClaimState;
            DepotControl = builder.DepotControl;
            FactionAccessPolicy = builder.FactionAccessPolicy;
            FactionAidPolicy = builder.FactionAidPolicy;
            DepotAccessFeePartsUnits = builder.DepotAccessFeePartsUnits;
            FutureRouteTollFuelUnits = builder.FutureRouteTollFuelUnits;
            EmergencyAidWaterMilli = builder.EmergencyAidWaterMilli;
            FactionMemory = builder.FactionMemory;
            FactionTrust = builder.FactionTrust;
            FactionGrievance = builder.FactionGrievance;
            PendingFactionOutcome = builder.PendingFactionOutcome;
            FactionOutcomeElapsedTicks = builder.FactionOutcomeElapsedTicks;
            RoutePermitGranted = builder.RoutePermitGranted;
            MaintenanceRecipe = builder.MaintenanceRecipe;
            MaintenanceObligationActive = builder.MaintenanceObligationActive;
            MaintenancePartsUnits = builder.MaintenancePartsUnits;
            NextMaintenanceDueSettlementTick =
                builder.NextMaintenanceDueSettlementTick;
            MaintenanceDue = builder.MaintenanceDue;
            DustFrontProgressTicks = builder.DustFrontProgressTicks;
        }

        public int SchemaVersion { get; }

        public string BalanceRevision { get; }

        public int WorldSeed { get; }

        public long GlobalTick { get; }

        public long SettlementTick { get; }

        public long FactionTick { get; }

        public long CrisisTick { get; }

        public long RoadTick { get; }

        public int SettlementAccumulatorMilli { get; }

        public int FactionAccumulatorMilli { get; }

        public int CrisisAccumulatorMilli { get; }

        public int RoadAccumulatorMilli { get; }

        public long NextCommandSequence { get; }

        public string ProtagonistId { get; }

        public ResidentRoster Roster { get; }

        public ColonyComposition Composition => Roster.Composition;

        public string? AssignedResidentId { get; }

        public PauseCause PauseCause { get; }

        public bool IsPaused => PauseCause != PauseCause.None;

        public bool SliceInfrastructureActive { get; }

        public long WaterMilli { get; }

        public long PartsUnits { get; }

        public long FuelUnits { get; }

        public TurbineCondition TurbineCondition { get; }

        public PreparationChoice PreparationChoice { get; }

        public PreparationPhase PreparationPhase { get; }

        public VehicleModule PlannedModule { get; }

        public long PreparationElapsedTicks { get; }

        public long PreparationRequiredTicks { get; }

        public long PreparationFuelDebitedUnits { get; }

        public int WorkshopServiceSlotsReserved { get; }

        public long ActiveWaterModifierMilliPerSettlementTick { get; }

        public NextCityDecision NextCityDecision { get; }

        public VehicleModule VehicleModule { get; }

        public ModuleInstallationState ModuleInstallationState { get; }

        public RouteKind RouteKind { get; }

        public RouteActionKind RouteActionKind { get; }

        public bool RouteActionUsed { get; }

        public ExpeditionPhase ExpeditionPhase { get; }

        public long RouteProgressTicks { get; }

        public long RouteTargetTicks { get; }

        public int RouteMovementAccumulatorMilli { get; }

        public int VehicleLateralMilli { get; }

        public long VehicleConditionMilli { get; }

        public long ExpeditionFuelManifestUnits { get; }

        public long OrdinaryCargoCapacityUnits { get; }

        public long OrdinaryCargoUsedUnits { get; }

        public int TowSlots { get; }

        public int TowSlotsUsed { get; }

        public long LiquidCapacityMilli { get; }

        public HeavyCargoKind HeavyCargoKind { get; }

        public HeavyCargoCustody HeavyCargoCustody { get; }

        public LiquidCargoKind LiquidCargoKind { get; }

        public long LiquidCargoQuantityMilli { get; }

        public LiquidCargoCustody LiquidCargoCustody { get; }

        public RepairCargoKind RepairCargoKind { get; }

        public RepairCargoCustody RepairCargoCustody { get; }

        public DepotBearingDisposition DepotBearingDisposition { get; }

        public bool ReturnPayloadFrozen { get; }

        public bool HasArrivalClaimSnapshot { get; }

        public long ArrivalFactionClaimProgressMilli { get; }

        public FactionClaimState ArrivalFactionClaimState { get; }

        public string? TransactionId { get; }

        public string? TransactionFingerprint { get; }

        public TransactionPhase TransactionPhase { get; }

        public EncounterChoice DepotResolution { get; }

        public long FactionClaimProgressMilli { get; }

        public FactionClaimState FactionClaimState { get; }

        public DepotControl DepotControl { get; }

        public FactionAccessPolicy FactionAccessPolicy { get; }

        public FactionAidPolicy FactionAidPolicy { get; }

        public long DepotAccessFeePartsUnits { get; }

        public long FutureRouteTollFuelUnits { get; }

        public long EmergencyAidWaterMilli { get; }

        public FactionMemoryRecord? FactionMemory { get; }

        public long FactionTrust { get; }

        public long FactionGrievance { get; }

        public FactionOutcomeKind PendingFactionOutcome { get; }

        public long FactionOutcomeElapsedTicks { get; }

        public bool RoutePermitGranted { get; }

        public MaintenanceRecipe MaintenanceRecipe { get; }

        public bool MaintenanceObligationActive { get; }

        public long MaintenancePartsUnits { get; }

        public long NextMaintenanceDueSettlementTick { get; }

        public bool MaintenanceDue { get; }

        public long DustFrontProgressTicks { get; }
    }

    internal sealed class LastBearingStateBuilder
    {
        internal LastBearingStateBuilder()
        {
            BalanceRevision = string.Empty;
            ProtagonistId = string.Empty;
            Roster = ResidentRoster.CreateForComposition(
                ColonyComposition.HumanOnly);
        }

        internal LastBearingStateBuilder(LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            SchemaVersion = state.SchemaVersion;
            BalanceRevision = state.BalanceRevision;
            WorldSeed = state.WorldSeed;
            GlobalTick = state.GlobalTick;
            SettlementTick = state.SettlementTick;
            FactionTick = state.FactionTick;
            CrisisTick = state.CrisisTick;
            RoadTick = state.RoadTick;
            SettlementAccumulatorMilli = state.SettlementAccumulatorMilli;
            FactionAccumulatorMilli = state.FactionAccumulatorMilli;
            CrisisAccumulatorMilli = state.CrisisAccumulatorMilli;
            RoadAccumulatorMilli = state.RoadAccumulatorMilli;
            NextCommandSequence = state.NextCommandSequence;
            ProtagonistId = state.ProtagonistId;
            Roster = state.Roster;
            AssignedResidentId = state.AssignedResidentId;
            PauseCause = state.PauseCause;
            SliceInfrastructureActive = state.SliceInfrastructureActive;
            WaterMilli = state.WaterMilli;
            PartsUnits = state.PartsUnits;
            FuelUnits = state.FuelUnits;
            TurbineCondition = state.TurbineCondition;
            PreparationChoice = state.PreparationChoice;
            PreparationPhase = state.PreparationPhase;
            PlannedModule = state.PlannedModule;
            PreparationElapsedTicks = state.PreparationElapsedTicks;
            PreparationRequiredTicks = state.PreparationRequiredTicks;
            PreparationFuelDebitedUnits = state.PreparationFuelDebitedUnits;
            WorkshopServiceSlotsReserved = state.WorkshopServiceSlotsReserved;
            ActiveWaterModifierMilliPerSettlementTick =
                state.ActiveWaterModifierMilliPerSettlementTick;
            NextCityDecision = state.NextCityDecision;
            VehicleModule = state.VehicleModule;
            ModuleInstallationState = state.ModuleInstallationState;
            RouteKind = state.RouteKind;
            RouteActionKind = state.RouteActionKind;
            RouteActionUsed = state.RouteActionUsed;
            ExpeditionPhase = state.ExpeditionPhase;
            RouteProgressTicks = state.RouteProgressTicks;
            RouteTargetTicks = state.RouteTargetTicks;
            RouteMovementAccumulatorMilli = state.RouteMovementAccumulatorMilli;
            VehicleLateralMilli = state.VehicleLateralMilli;
            VehicleConditionMilli = state.VehicleConditionMilli;
            ExpeditionFuelManifestUnits = state.ExpeditionFuelManifestUnits;
            OrdinaryCargoCapacityUnits = state.OrdinaryCargoCapacityUnits;
            OrdinaryCargoUsedUnits = state.OrdinaryCargoUsedUnits;
            TowSlots = state.TowSlots;
            TowSlotsUsed = state.TowSlotsUsed;
            LiquidCapacityMilli = state.LiquidCapacityMilli;
            HeavyCargoKind = state.HeavyCargoKind;
            HeavyCargoCustody = state.HeavyCargoCustody;
            LiquidCargoKind = state.LiquidCargoKind;
            LiquidCargoQuantityMilli = state.LiquidCargoQuantityMilli;
            LiquidCargoCustody = state.LiquidCargoCustody;
            RepairCargoKind = state.RepairCargoKind;
            RepairCargoCustody = state.RepairCargoCustody;
            DepotBearingDisposition = state.DepotBearingDisposition;
            ReturnPayloadFrozen = state.ReturnPayloadFrozen;
            HasArrivalClaimSnapshot = state.HasArrivalClaimSnapshot;
            ArrivalFactionClaimProgressMilli =
                state.ArrivalFactionClaimProgressMilli;
            ArrivalFactionClaimState = state.ArrivalFactionClaimState;
            TransactionId = state.TransactionId;
            TransactionFingerprint = state.TransactionFingerprint;
            TransactionPhase = state.TransactionPhase;
            DepotResolution = state.DepotResolution;
            FactionClaimProgressMilli = state.FactionClaimProgressMilli;
            FactionClaimState = state.FactionClaimState;
            DepotControl = state.DepotControl;
            FactionAccessPolicy = state.FactionAccessPolicy;
            FactionAidPolicy = state.FactionAidPolicy;
            DepotAccessFeePartsUnits = state.DepotAccessFeePartsUnits;
            FutureRouteTollFuelUnits = state.FutureRouteTollFuelUnits;
            EmergencyAidWaterMilli = state.EmergencyAidWaterMilli;
            FactionMemory = state.FactionMemory;
            FactionTrust = state.FactionTrust;
            FactionGrievance = state.FactionGrievance;
            PendingFactionOutcome = state.PendingFactionOutcome;
            FactionOutcomeElapsedTicks = state.FactionOutcomeElapsedTicks;
            RoutePermitGranted = state.RoutePermitGranted;
            MaintenanceRecipe = state.MaintenanceRecipe;
            MaintenanceObligationActive = state.MaintenanceObligationActive;
            MaintenancePartsUnits = state.MaintenancePartsUnits;
            NextMaintenanceDueSettlementTick =
                state.NextMaintenanceDueSettlementTick;
            MaintenanceDue = state.MaintenanceDue;
            DustFrontProgressTicks = state.DustFrontProgressTicks;
        }

        internal int SchemaVersion;
        internal string BalanceRevision;
        internal int WorldSeed;
        internal long GlobalTick;
        internal long SettlementTick;
        internal long FactionTick;
        internal long CrisisTick;
        internal long RoadTick;
        internal int SettlementAccumulatorMilli;
        internal int FactionAccumulatorMilli;
        internal int CrisisAccumulatorMilli;
        internal int RoadAccumulatorMilli;
        internal long NextCommandSequence;
        internal string ProtagonistId;
        internal ResidentRoster Roster;
        internal string? AssignedResidentId;
        internal PauseCause PauseCause;
        internal bool SliceInfrastructureActive;
        internal long WaterMilli;
        internal long PartsUnits;
        internal long FuelUnits;
        internal TurbineCondition TurbineCondition;
        internal PreparationChoice PreparationChoice;
        internal PreparationPhase PreparationPhase;
        internal VehicleModule PlannedModule;
        internal long PreparationElapsedTicks;
        internal long PreparationRequiredTicks;
        internal long PreparationFuelDebitedUnits;
        internal int WorkshopServiceSlotsReserved;
        internal long ActiveWaterModifierMilliPerSettlementTick;
        internal NextCityDecision NextCityDecision;
        internal VehicleModule VehicleModule;
        internal ModuleInstallationState ModuleInstallationState;
        internal RouteKind RouteKind;
        internal RouteActionKind RouteActionKind;
        internal bool RouteActionUsed;
        internal ExpeditionPhase ExpeditionPhase;
        internal long RouteProgressTicks;
        internal long RouteTargetTicks;
        internal int RouteMovementAccumulatorMilli;
        internal int VehicleLateralMilli;
        internal long VehicleConditionMilli;
        internal long ExpeditionFuelManifestUnits;
        internal long OrdinaryCargoCapacityUnits;
        internal long OrdinaryCargoUsedUnits;
        internal int TowSlots;
        internal int TowSlotsUsed;
        internal long LiquidCapacityMilli;
        internal HeavyCargoKind HeavyCargoKind;
        internal HeavyCargoCustody HeavyCargoCustody;
        internal LiquidCargoKind LiquidCargoKind;
        internal long LiquidCargoQuantityMilli;
        internal LiquidCargoCustody LiquidCargoCustody;
        internal RepairCargoKind RepairCargoKind;
        internal RepairCargoCustody RepairCargoCustody;
        internal DepotBearingDisposition DepotBearingDisposition;
        internal bool ReturnPayloadFrozen;
        internal bool HasArrivalClaimSnapshot;
        internal long ArrivalFactionClaimProgressMilli;
        internal FactionClaimState ArrivalFactionClaimState;
        internal string? TransactionId;
        internal string? TransactionFingerprint;
        internal TransactionPhase TransactionPhase;
        internal EncounterChoice DepotResolution;
        internal long FactionClaimProgressMilli;
        internal FactionClaimState FactionClaimState;
        internal DepotControl DepotControl;
        internal FactionAccessPolicy FactionAccessPolicy;
        internal FactionAidPolicy FactionAidPolicy;
        internal long DepotAccessFeePartsUnits;
        internal long FutureRouteTollFuelUnits;
        internal long EmergencyAidWaterMilli;
        internal FactionMemoryRecord? FactionMemory;
        internal long FactionTrust;
        internal long FactionGrievance;
        internal FactionOutcomeKind PendingFactionOutcome;
        internal long FactionOutcomeElapsedTicks;
        internal bool RoutePermitGranted;
        internal MaintenanceRecipe MaintenanceRecipe;
        internal bool MaintenanceObligationActive;
        internal long MaintenancePartsUnits;
        internal long NextMaintenanceDueSettlementTick;
        internal bool MaintenanceDue;
        internal long DustFrontProgressTicks;

        internal LastBearingState Build()
        {
            var state = new LastBearingState(this);
            LastBearingInvariants.Validate(state);
            return state;
        }
    }
}
