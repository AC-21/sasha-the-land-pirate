#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public abstract class LastBearingCommand
    {
        protected LastBearingCommand(long sequence)
        {
            if (sequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Sequence = sequence;
        }

        public long Sequence { get; }

        internal static string RequireToken(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                throw new ArgumentException(
                    "LAST_BEARING_COMMAND_TOKEN_INVALID",
                    parameterName);
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character < 0x21 || character > 0x7e)
                {
                    throw new ArgumentException(
                        "LAST_BEARING_COMMAND_TOKEN_INVALID",
                        parameterName);
                }
            }

            return value;
        }
    }

    public sealed class AssignResidentCommand : LastBearingCommand
    {
        public AssignResidentCommand(long sequence, string stableId)
            : base(sequence)
        {
            StableId = RequireToken(stableId, nameof(stableId));
        }

        public string StableId { get; }
    }

    public sealed class ActivateSliceInfrastructureCommand : LastBearingCommand
    {
        public ActivateSliceInfrastructureCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class PlaceCityBuildingCommand : LastBearingCommand
    {
        public PlaceCityBuildingCommand(
            long sequence,
            CityBuildingKind building,
            int padIndex,
            int orientationQuarterTurns)
            : base(sequence)
        {
            if (building != CityBuildingKind.Recycler
                && building != CityBuildingKind.MachineShop
                && building != CityBuildingKind.EmergencyStorage)
            {
                throw new ArgumentOutOfRangeException(nameof(building));
            }

            if (padIndex < 0
                || padIndex >= LastBearingState.CityConstructionPadCount)
            {
                throw new ArgumentOutOfRangeException(nameof(padIndex));
            }

            if (orientationQuarterTurns < 0 || orientationQuarterTurns > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(orientationQuarterTurns));
            }

            Building = building;
            PadIndex = padIndex;
            OrientationQuarterTurns = orientationQuarterTurns;
        }

        public CityBuildingKind Building { get; }

        public int PadIndex { get; }

        public int OrientationQuarterTurns { get; }
    }

    public sealed class ConnectCityServiceLinkCommand : LastBearingCommand
    {
        public ConnectCityServiceLinkCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class AssignCityServiceResidentCommand : LastBearingCommand
    {
        public AssignCityServiceResidentCommand(long sequence, string stableId)
            : base(sequence)
        {
            StableId = RequireToken(stableId, nameof(stableId));
        }

        public string StableId { get; }
    }

    public sealed class AdvanceCityServiceSledCommand : LastBearingCommand
    {
        public AdvanceCityServiceSledCommand(
            long sequence,
            CityDeliveryStage expectedSourceStage)
            : base(sequence)
        {
            if (expectedSourceStage != CityDeliveryStage.AtRecycler
                && expectedSourceStage != CityDeliveryStage.InTransit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedSourceStage));
            }

            ExpectedSourceStage = expectedSourceStage;
        }

        public CityDeliveryStage ExpectedSourceStage { get; }
    }

    public sealed class SelectPreparationCommand : LastBearingCommand
    {
        public SelectPreparationCommand(
            long sequence,
            PreparationChoice choice,
            VehicleModule plannedModule)
            : base(sequence)
        {
            if (choice == PreparationChoice.Unselected)
            {
                throw new ArgumentOutOfRangeException(nameof(choice));
            }

            if (plannedModule == VehicleModule.None)
            {
                throw new ArgumentOutOfRangeException(nameof(plannedModule));
            }

            Choice = choice;
            PlannedModule = plannedModule;
        }

        public PreparationChoice Choice { get; }

        public VehicleModule PlannedModule { get; }
    }

    public sealed class InstallVehicleModuleCommand : LastBearingCommand
    {
        public InstallVehicleModuleCommand(long sequence, VehicleModule module)
            : base(sequence)
        {
            if (module == VehicleModule.None)
            {
                throw new ArgumentOutOfRangeException(nameof(module));
            }

            Module = module;
        }

        public VehicleModule Module { get; }
    }

    public sealed class InstallRigUpgradeCommand : LastBearingCommand
    {
        public InstallRigUpgradeCommand(long sequence, RigUpgrade upgrade)
            : base(sequence)
        {
            if (upgrade != RigUpgrade.PatchworkSkidPlate)
            {
                throw new ArgumentOutOfRangeException(nameof(upgrade));
            }

            Upgrade = upgrade;
        }

        public RigUpgrade Upgrade { get; }
    }

    public sealed class PrepareExpeditionTransactionCommand : LastBearingCommand
    {
        public PrepareExpeditionTransactionCommand(
            long sequence,
            string transactionId,
            string fingerprint)
            : base(sequence)
        {
            TransactionId = RequireToken(transactionId, nameof(transactionId));
            Fingerprint = RequireToken(fingerprint, nameof(fingerprint));
        }

        public string TransactionId { get; }

        public string Fingerprint { get; }
    }

    public sealed class DebitCityManifestCommand : LastBearingCommand
    {
        public DebitCityManifestCommand(
            long sequence,
            string transactionId,
            string fingerprint)
            : base(sequence)
        {
            TransactionId = RequireToken(transactionId, nameof(transactionId));
            Fingerprint = RequireToken(fingerprint, nameof(fingerprint));
        }

        public string TransactionId { get; }

        public string Fingerprint { get; }
    }

    public sealed class DepartExpeditionCommand : LastBearingCommand
    {
        public DepartExpeditionCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class DriveVehicleCommand : LastBearingCommand
    {
        public DriveVehicleCommand(
            long sequence,
            int throttleMilli,
            int steeringMilli)
            : base(sequence)
        {
            if (throttleMilli < 0 || throttleMilli > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(throttleMilli));
            }

            if (steeringMilli < -1000 || steeringMilli > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(steeringMilli));
            }

            ThrottleMilli = throttleMilli;
            SteeringMilli = steeringMilli;
        }

        public int ThrottleMilli { get; }

        public int SteeringMilli { get; }
    }

    public sealed class ResolveDepotCommand : LastBearingCommand
    {
        public ResolveDepotCommand(long sequence, EncounterChoice choice)
            : base(sequence)
        {
            if (choice == EncounterChoice.Unresolved)
            {
                throw new ArgumentOutOfRangeException(nameof(choice));
            }

            Choice = choice;
        }

        public EncounterChoice Choice { get; }
    }

    public sealed class LoadDepotRepairCargoCommand : LastBearingCommand
    {
        public LoadDepotRepairCargoCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class OperateWreckLineModuleCommand : LastBearingCommand
    {
        public OperateWreckLineModuleCommand(
            long sequence,
            RouteActionKind action)
            : base(sequence)
        {
            if (action == RouteActionKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(action));
            }

            Action = action;
        }

        public RouteActionKind Action { get; }
    }

    public sealed class OperateDepotRecoveryPointCommand : LastBearingCommand
    {
        public OperateDepotRecoveryPointCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class ChooseLiquidReturnCommand : LastBearingCommand
    {
        public ChooseLiquidReturnCommand(long sequence, LiquidCargoKind kind)
            : base(sequence)
        {
            if (kind == LiquidCargoKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Kind = kind;
        }

        public LiquidCargoKind Kind { get; }
    }

    public sealed class FreezeReturnPayloadCommand : LastBearingCommand
    {
        public FreezeReturnPayloadCommand(
            long sequence,
            string transactionId,
            string fingerprint)
            : base(sequence)
        {
            TransactionId = RequireToken(transactionId, nameof(transactionId));
            Fingerprint = RequireToken(fingerprint, nameof(fingerprint));
        }

        public string TransactionId { get; }

        public string Fingerprint { get; }
    }

    public sealed class ReturnHomeCommand : LastBearingCommand
    {
        public ReturnHomeCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class CreditCityReturnCommand : LastBearingCommand
    {
        public CreditCityReturnCommand(
            long sequence,
            string transactionId,
            string fingerprint)
            : base(sequence)
        {
            TransactionId = RequireToken(transactionId, nameof(transactionId));
            Fingerprint = RequireToken(fingerprint, nameof(fingerprint));
        }

        public string TransactionId { get; }

        public string Fingerprint { get; }
    }

    public sealed class FinalizeExpeditionTransactionCommand
        : LastBearingCommand
    {
        public FinalizeExpeditionTransactionCommand(
            long sequence,
            string transactionId,
            string fingerprint)
            : base(sequence)
        {
            TransactionId = RequireToken(transactionId, nameof(transactionId));
            Fingerprint = RequireToken(fingerprint, nameof(fingerprint));
        }

        public string TransactionId { get; }

        public string Fingerprint { get; }
    }

    public sealed class InstallTurbineRepairCommand : LastBearingCommand
    {
        public InstallTurbineRepairCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class InstallCityImprovementCommand : LastBearingCommand
    {
        public InstallCityImprovementCommand(
            long sequence,
            NextCityDecision decision,
            string socketId,
            int orientationQuarterTurns)
            : base(sequence)
        {
            if (decision == NextCityDecision.None)
            {
                throw new ArgumentOutOfRangeException(nameof(decision));
            }

            Decision = decision;
            SocketId = RequireToken(socketId, nameof(socketId));
            OrientationQuarterTurns = orientationQuarterTurns;
        }

        public NextCityDecision Decision { get; }

        public string SocketId { get; }

        public int OrientationQuarterTurns { get; }
    }

    public sealed class StartSpareBearingBatchCommand : LastBearingCommand
    {
        public StartSpareBearingBatchCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class BarterSpareBearingLotCommand : LastBearingCommand
    {
        public BarterSpareBearingLotCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class ServiceFieldSleeveCommand : LastBearingCommand
    {
        public ServiceFieldSleeveCommand(long sequence)
            : base(sequence)
        {
        }
    }

    public sealed class SetPauseCommand : LastBearingCommand
    {
        public SetPauseCommand(long sequence, bool isPaused)
            : base(sequence)
        {
            IsPaused = isPaused;
        }

        public bool IsPaused { get; }
    }

    public sealed class TriggerAutoPauseAlertCommand : LastBearingCommand
    {
        public TriggerAutoPauseAlertCommand(long sequence)
            : base(sequence)
        {
        }
    }
}
