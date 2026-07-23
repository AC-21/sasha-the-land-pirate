#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingState
    {
        public const int CurrentSchemaVersion = 4;
        public const int CityConstructionPadCount = 5;
        public const int UnplacedCityPadIndex = -1;
        public const string SashaProtagonistId = "sasha";
        public const string LastBearingFactionId = "faction:last-bearing:caravaners";
        public const string AuxiliaryPumpSocketId =
            "city:last-bearing:socket:pump-hall-auxiliary";
        public const int AuxiliaryPumpOrientationQuarterTurns = 0;
        public const string SpareBearingRecipeId =
            "recipe:last-bearing:spare-bearing:0001";
        public const string SpareBearingBatchId =
            "world:last-bearing:manufacturing-job:0001";
        public const string SpareBearingLotId =
            "world:last-bearing:lot:0001";
        public const string SpareBearingTradeContractId =
            "world:last-bearing:trade-contract:0001";
        public const string SpareBearingWorkshopOutputId =
            "settlement:last-bearing:workshop-output";
        public const string LastBearingClaimsCounterId =
            "site:last-bearing-claims-counter";
        public const string DepotCorridorRoutePermitId =
            "world:last-bearing:promise:0001";
        public const string DepotCorridorRouteBoardId =
            "board:last-bearing:depot-corridor";
        public const string OneGoodBatchPresentationContentId =
            "bld_machine_shop_claims_wicket_a";
        public const string RecyclerBuildingId =
            "city:last-bearing:building:recycler";
        public const string MachineShopBuildingId =
            "city:last-bearing:building:machine-shop";
        public const string EmergencyStorageBuildingId =
            "city:last-bearing:building:emergency-storage";
        public const string CityServiceLinkId =
            "city:last-bearing:service-link:recycler-workshop";
        public const string CityServiceSlotId =
            "city:last-bearing:service-slot:working-cell";
        public const string CityServiceBatchId =
            "city:last-bearing:delivery:parts-batch:0001";

        internal LastBearingState(LastBearingStateBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            CopyFrom(builder);
        }

        internal void CopyFrom(LastBearingStateBuilder builder)
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
            RecyclerPadIndex = builder.RecyclerPadIndex;
            RecyclerQuarterTurns = builder.RecyclerQuarterTurns;
            MachineShopPadIndex = builder.MachineShopPadIndex;
            MachineShopQuarterTurns = builder.MachineShopQuarterTurns;
            EmergencyStoragePadIndex = builder.EmergencyStoragePadIndex;
            EmergencyStorageQuarterTurns = builder.EmergencyStorageQuarterTurns;
            CityServiceLinkConnected = builder.CityServiceLinkConnected;
            CityServiceResidentId = builder.CityServiceResidentId;
            CityDeliveryStage = builder.CityDeliveryStage;
            CityDeliveryCount = builder.CityDeliveryCount;

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
            InstalledCityImprovement = builder.InstalledCityImprovement;
            SpareBearingRecipe = builder.SpareBearingRecipe;
            SpareBearingBatchPhase = builder.SpareBearingBatchPhase;
            SpareBearingElapsedTicks = builder.SpareBearingElapsedTicks;
            SpareBearingRequiredTicks = builder.SpareBearingRequiredTicks;
            SpareBearingLotQuantity = builder.SpareBearingLotQuantity;
            SpareBearingLotCustody = builder.SpareBearingLotCustody;

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

        public int SchemaVersion { get; private set; }

        public string BalanceRevision { get; private set; } = string.Empty;

        public int WorldSeed { get; private set; }

        public long GlobalTick { get; private set; }

        public long SettlementTick { get; private set; }

        public long FactionTick { get; private set; }

        public long CrisisTick { get; private set; }

        public long RoadTick { get; private set; }

        public int SettlementAccumulatorMilli { get; private set; }

        public int FactionAccumulatorMilli { get; private set; }

        public int CrisisAccumulatorMilli { get; private set; }

        public int RoadAccumulatorMilli { get; private set; }

        public long NextCommandSequence { get; private set; }

        public string ProtagonistId { get; private set; } = string.Empty;

        public ResidentRoster Roster { get; private set; } = null!;

        public ColonyComposition Composition => Roster.Composition;

        public string? AssignedResidentId { get; private set; }

        public PauseCause PauseCause { get; private set; }

        public bool IsPaused => PauseCause != PauseCause.None;

        public bool SliceInfrastructureActive { get; private set; }

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

        public long WaterMilli { get; private set; }

        public long PartsUnits { get; private set; }

        public long FuelUnits { get; private set; }

        public TurbineCondition TurbineCondition { get; private set; }

        public PreparationChoice PreparationChoice { get; private set; }

        public PreparationPhase PreparationPhase { get; private set; }

        public VehicleModule PlannedModule { get; private set; }

        public long PreparationElapsedTicks { get; private set; }

        public long PreparationRequiredTicks { get; private set; }

        public long PreparationFuelDebitedUnits { get; private set; }

        public int WorkshopServiceSlotsReserved { get; private set; }

        public long ActiveWaterModifierMilliPerSettlementTick { get; private set; }

        public NextCityDecision NextCityDecision { get; private set; }

        public CityImprovementKind InstalledCityImprovement { get; private set; }

        public SpareBearingRecipe SpareBearingRecipe { get; private set; }

        public SpareBearingBatchPhase SpareBearingBatchPhase { get; private set; }

        public long SpareBearingElapsedTicks { get; private set; }

        public long SpareBearingRequiredTicks { get; private set; }

        public long SpareBearingLotQuantity { get; private set; }

        public SpareBearingLotCustody SpareBearingLotCustody { get; private set; }

        public VehicleModule VehicleModule { get; private set; }

        public ModuleInstallationState ModuleInstallationState { get; private set; }

        public RouteKind RouteKind { get; private set; }

        public RouteActionKind RouteActionKind { get; private set; }

        public bool RouteActionUsed { get; private set; }

        public ExpeditionPhase ExpeditionPhase { get; private set; }

        public long RouteProgressTicks { get; private set; }

        public long RouteTargetTicks { get; private set; }

        public int RouteMovementAccumulatorMilli { get; private set; }

        public int VehicleLateralMilli { get; private set; }

        public long VehicleConditionMilli { get; private set; }

        public long ExpeditionFuelManifestUnits { get; private set; }

        public long OrdinaryCargoCapacityUnits { get; private set; }

        public long OrdinaryCargoUsedUnits { get; private set; }

        public int TowSlots { get; private set; }

        public int TowSlotsUsed { get; private set; }

        public long LiquidCapacityMilli { get; private set; }

        public HeavyCargoKind HeavyCargoKind { get; private set; }

        public HeavyCargoCustody HeavyCargoCustody { get; private set; }

        public LiquidCargoKind LiquidCargoKind { get; private set; }

        public long LiquidCargoQuantityMilli { get; private set; }

        public LiquidCargoCustody LiquidCargoCustody { get; private set; }

        public RepairCargoKind RepairCargoKind { get; private set; }

        public RepairCargoCustody RepairCargoCustody { get; private set; }

        public DepotBearingDisposition DepotBearingDisposition { get; private set; }

        public bool ReturnPayloadFrozen { get; private set; }

        public bool HasArrivalClaimSnapshot { get; private set; }

        public long ArrivalFactionClaimProgressMilli { get; private set; }

        public FactionClaimState ArrivalFactionClaimState { get; private set; }

        public string? TransactionId { get; private set; }

        public string? TransactionFingerprint { get; private set; }

        public TransactionPhase TransactionPhase { get; private set; }

        public EncounterChoice DepotResolution { get; private set; }

        public long FactionClaimProgressMilli { get; private set; }

        public FactionClaimState FactionClaimState { get; private set; }

        public DepotControl DepotControl { get; private set; }

        public FactionAccessPolicy FactionAccessPolicy { get; private set; }

        public FactionAidPolicy FactionAidPolicy { get; private set; }

        public long DepotAccessFeePartsUnits { get; private set; }

        public long FutureRouteTollFuelUnits { get; private set; }

        public long EmergencyAidWaterMilli { get; private set; }

        public FactionMemoryRecord? FactionMemory { get; private set; }

        public long FactionTrust { get; private set; }

        public long FactionGrievance { get; private set; }

        public FactionOutcomeKind PendingFactionOutcome { get; private set; }

        public long FactionOutcomeElapsedTicks { get; private set; }

        public bool RoutePermitGranted { get; private set; }

        public MaintenanceRecipe MaintenanceRecipe { get; private set; }

        public bool MaintenanceObligationActive { get; private set; }

        public long MaintenancePartsUnits { get; private set; }

        public long NextMaintenanceDueSettlementTick { get; private set; }

        public bool MaintenanceDue { get; private set; }

        public long DustFrontProgressTicks { get; private set; }
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
            : this()
        {
            CopyFrom(state);
        }

        internal void CopyFrom(LastBearingState state)
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
            InstalledCityImprovement = state.InstalledCityImprovement;
            SpareBearingRecipe = state.SpareBearingRecipe;
            SpareBearingBatchPhase = state.SpareBearingBatchPhase;
            SpareBearingElapsedTicks = state.SpareBearingElapsedTicks;
            SpareBearingRequiredTicks = state.SpareBearingRequiredTicks;
            SpareBearingLotQuantity = state.SpareBearingLotQuantity;
            SpareBearingLotCustody = state.SpareBearingLotCustody;
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
        internal int RecyclerPadIndex;
        internal int RecyclerQuarterTurns;
        internal int MachineShopPadIndex;
        internal int MachineShopQuarterTurns;
        internal int EmergencyStoragePadIndex;
        internal int EmergencyStorageQuarterTurns;
        internal bool CityServiceLinkConnected;
        internal string? CityServiceResidentId;
        internal CityDeliveryStage CityDeliveryStage;
        internal int CityDeliveryCount;
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
        internal CityImprovementKind InstalledCityImprovement;
        internal SpareBearingRecipe SpareBearingRecipe;
        internal SpareBearingBatchPhase SpareBearingBatchPhase;
        internal long SpareBearingElapsedTicks;
        internal long SpareBearingRequiredTicks;
        internal long SpareBearingLotQuantity;
        internal SpareBearingLotCustody SpareBearingLotCustody;
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

        internal void BuildInto(LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.CopyFrom(this);
            LastBearingInvariants.Validate(state);
        }
    }
}
