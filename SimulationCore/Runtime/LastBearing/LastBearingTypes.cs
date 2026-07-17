#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public enum ColonyComposition
    {
        HumanOnly = 1,
        RobotOnly = 2,
        Mixed = 3,
    }

    public enum ResidentKind
    {
        HumanCohort = 1,
        UtilityRobot = 2,
    }

    public enum PreparationChoice
    {
        Unselected = 0,
        WorkshopPush = 1,
        CivicBuffer = 2,
    }

    public enum PreparationPhase
    {
        Unselected = 0,
        Preparing = 1,
        Ready = 2,
        Committed = 3,
    }

    public enum ModuleInstallationState
    {
        None = 0,
        Pending = 1,
        Installed = 2,
    }

    public enum VehicleModule
    {
        None = 0,
        WinchAssembly = 1,
        SealedRangeTank = 2,
    }

    public enum RouteKind
    {
        None = 0,
        CollapsedShortBranch = 1,
        ExposedLongRoute = 2,
    }

    public enum RouteActionKind
    {
        None = 0,
        DeployWinch = 1,
        CrossExposedDustRoute = 2,
    }

    public enum ExpeditionPhase
    {
        AtHome = 0,
        Outbound = 1,
        AtDepot = 2,
        Returning = 3,
        Returned = 4,
    }

    public enum TransactionPhase
    {
        None = 0,
        Prepared = 1,
        CityDebited = 2,
        RoadOwned = 3,
        ReturnPending = 4,
        CityCredited = 5,
        Finalized = 6,
    }

    public enum EncounterChoice
    {
        Unresolved = 0,
        Cooperate = 1,
        TakeBearing = 2,
    }

    public enum PauseCause
    {
        None = 0,
        Explicit = 1,
        AutoAlert = 2,
    }

    public enum RepairCargoKind
    {
        None = 0,
        CeramicBearing = 1,
        FieldSleeve = 2,
    }

    public enum RepairCargoCustody
    {
        None = 0,
        Depot = 1,
        Faction = 2,
        Vehicle = 3,
        Turbine = 4,
        Consumed = 5,
    }

    public enum HeavyCargoKind
    {
        None = 0,
        PumpRotor = 1,
    }

    public enum HeavyCargoCustody
    {
        None = 0,
        Depot = 1,
        Vehicle = 2,
        Settlement = 3,
    }

    public enum LiquidCargoKind
    {
        None = 0,
        Water = 1,
        Fuel = 2,
    }

    public enum LiquidCargoCustody
    {
        None = 0,
        Depot = 1,
        Vehicle = 2,
        Settlement = 3,
    }

    public enum TurbineCondition
    {
        Failing = 1,
        BearingRepaired = 2,
        SleeveRepaired = 3,
    }

    public enum FactionClaimState
    {
        Telegraphed = 1,
        Contested = 2,
        Claimed = 3,
        Cooperating = 4,
        Aggrieved = 5,
        Resolved = 6,
    }

    public enum DepotControl
    {
        Unclaimed = 0,
        Contested = 1,
        FactionClaimed = 2,
        SharedAccess = 3,
        Depleted = 4,
    }

    public enum DepotBearingDisposition
    {
        AtDepot = 1,
        FactionHeld = 2,
        InVehicle = 3,
        InstalledAtTurbine = 4,
        TakenBySasha = 5,
    }

    public enum FactionAccessPolicy
    {
        Open = 0,
        PermitRequired = 1,
        SharedService = 2,
        Closed = 3,
    }

    public enum FactionAidPolicy
    {
        CaseByCase = 0,
        EmergencyWaterQueued = 1,
        EmergencyWaterDelivered = 2,
        Withheld = 3,
    }

    public enum FactionOutcomeKind
    {
        None = 0,
        Cooperative = 1,
        Adverse = 2,
    }

    public enum MaintenanceRecipe
    {
        None = 0,
        FieldSleeveService = 1,
    }

    public enum NextCityDecision
    {
        None = 0,
        RefurbishAuxiliaryPump = 1,
        ExpandEmergencyCistern = 2,
        MachineSpareBearing = 3,
        RestoreDepotAccess = 4,
    }

    public enum LastBearingEventKind
    {
        ResidentAssigned = 1,
        SliceInfrastructureActivated = 2,
        PreparationStarted = 3,
        PreparationCompleted = 4,
        HomeWaterChanged = 5,
        VehicleModuleInstalled = 6,
        ExpeditionTransactionPrepared = 7,
        ExpeditionFuelCommitted = 8,
        ExpeditionDeparted = 9,
        RouteActionUsed = 10,
        RouteProgressed = 11,
        VehicleConditionChanged = 12,
        FactionClaimAdvanced = 13,
        FactionClaimContested = 14,
        FactionDepotClaimed = 15,
        DepotAccessTermsChanged = 16,
        DepotResolved = 17,
        FactionMemoryRecorded = 18,
        FactionBehaviorChanged = 19,
        FactionGrievanceRecorded = 20,
        RepairCargoTransferred = 21,
        HeavyCargoTransferred = 22,
        LiquidCargoTransferred = 23,
        ReturnPayloadFrozen = 24,
        VehicleReturned = 25,
        CityReturnCredited = 26,
        ExpeditionTransactionFinalized = 27,
        DoctrineRecipeActivated = 28,
        MaintenanceObligationCreated = 29,
        EmergencyAidDelivered = 30,
        TurbineRepaired = 31,
        NextCityDecisionSet = 32,
        PauseChanged = 33,
        MaintenanceServiced = 34,
        IdempotentReplayAccepted = 35,
        VehicleSteered = 36,
    }

    public enum LastBearingEventCause
    {
        PlayerCommand = 1,
        AutonomousSettlementTick = 2,
        AutonomousFactionTick = 3,
        AutonomousCrisisTick = 4,
        SystemTransition = 5,
    }

    public sealed class FactionMemoryRecord : IEquatable<FactionMemoryRecord>
    {
        public FactionMemoryRecord(
            string stableId,
            string witnessedAction,
            string affectedFactionId,
            long magnitude,
            string doctrineTag,
            long encounterTick,
            string consequenceCode)
        {
            StableId = RequireText(stableId, nameof(stableId));
            WitnessedAction = RequireText(
                witnessedAction,
                nameof(witnessedAction));
            AffectedFactionId = RequireText(
                affectedFactionId,
                nameof(affectedFactionId));
            DoctrineTag = RequireText(doctrineTag, nameof(doctrineTag));
            ConsequenceCode = RequireText(
                consequenceCode,
                nameof(consequenceCode));
            if (encounterTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(encounterTick));
            }

            Magnitude = magnitude;
            EncounterTick = encounterTick;
        }

        public string StableId { get; }

        public string WitnessedAction { get; }

        public string AffectedFactionId { get; }

        public long Magnitude { get; }

        public string DoctrineTag { get; }

        public long EncounterTick { get; }

        public string ConsequenceCode { get; }

        public bool Equals(FactionMemoryRecord? other)
        {
            return other != null
                && string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                && string.Equals(
                    WitnessedAction,
                    other.WitnessedAction,
                    StringComparison.Ordinal)
                && string.Equals(
                    AffectedFactionId,
                    other.AffectedFactionId,
                    StringComparison.Ordinal)
                && Magnitude == other.Magnitude
                && string.Equals(
                    DoctrineTag,
                    other.DoctrineTag,
                    StringComparison.Ordinal)
                && EncounterTick == other.EncounterTick
                && string.Equals(
                    ConsequenceCode,
                    other.ConsequenceCode,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as FactionMemoryRecord);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(StableId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(WitnessedAction);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(AffectedFactionId);
                hash = (hash * 397) ^ Magnitude.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DoctrineTag);
                hash = (hash * 397) ^ EncounterTick.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ConsequenceCode);
                return hash;
            }
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "LAST_BEARING_MEMORY_TEXT_REQUIRED",
                    parameterName);
            }

            if (value.Length > 128)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }
}
