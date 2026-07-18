#nullable enable

using System;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public static class LastBearingBalanceV1
    {
        public const string Revision = "last-bearing-prototype-balance-v1";
        public const int TickRateHz = 10;
        public const int FullClockScaleMilli = 1000;
        public const int ExpeditionHomeClockScaleMilli = 500;

        public const long StartingWaterMilli = 120000;
        public const long StartingPartsUnits = 24;
        public const long StartingFuelUnits = 18;
        public const long StartingVehicleConditionMilli = 1000;
        public const long WaterCapacityMilli = 180000;

        public const long FailingWaterRateMilliPerSettlementTick = -10;
        public const long BearingRepairRateMilliPerSettlementTick = 30;
        public const long SleeveRepairRateMilliPerSettlementTick = 15;
        public const long WorkshopWaterModifierMilliPerSettlementTick = -10;
        public const long CivicBufferWaterModifierMilliPerSettlementTick = 20;

        public const long WorkshopBasePreparationTicks = 180;
        public const long CivicBufferBasePreparationTicks = 600;
        public const long WinchFabricationTicks = 120;
        public const long TankFabricationTicks = 60;
        public const long WorkshopPreparationFuelUnits = 2;
        public const long CivicBufferPreparationFuelUnits = 1;

        public const long WinchPartsCostUnits = 8;
        public const long WinchInstallFuelUnits = 2;
        public const long TankPartsCostUnits = 3;
        public const long TankInstallFuelUnits = 6;

        public const long ShortRouteOneWayTicks = 150;
        public const long LongRouteOneWayTicks = 300;
        public const long ShortRouteFuelManifestUnits = 4;
        public const long LongRouteFuelManifestUnits = 7;
        public const long ShortRouteRoundTripConditionLossMilli = 80;
        public const long LongRouteRoundTripConditionLossMilli = 180;
        public const int SteeringResponseDivisor = 20;
        public const int RoadLateralLimitMilli = 1000;
        public const int RoadSafeHalfWidthMilli = 750;
        public const long RoadEdgeConditionLossPerProgressTickMilli = 1;

        public const long WinchOrdinaryCargoUnits = 6;
        public const int WinchTowSlots = 1;
        public const long TankOrdinaryCargoUnits = 10;
        public const long TankLiquidCapacityMilli = 30000;
        public const long TankWaterReturnMilli = 30000;
        public const long TankFuelReturnMilli = 5000;

        public const long FactionClaimRateMilliPerFactionTick = 1;
        public const long FactionContestedThresholdMilli = 750;
        public const long FactionClaimThresholdMilli = 1000;
        public const long DustFrontThresholdCrisisTicks = 6000;
        public const long ClaimedDepotAccessFeePartsUnits = 3;
        public const long CooperateTrustDelta = 25;
        public const long TakeTrustDelta = -20;
        public const long TakeGrievanceDelta = 40;
        public const long FactionOutcomeMaturationTicks = 50;
        public const long CooperateAidWaterMilli = 10000;
        public const long SleeveMaintenancePartsUnits = 2;
        public const long SleeveMaintenanceIntervalSettlementTicks = 600;
        public const long TakeFutureRouteTollFuelUnits = 2;

        public const long MinimumRecoverableWaterMilli = 60000;
        public const long MinimumPostReturnPartsUnits = 2;
        public const long MinimumReturnVehicleConditionMilli = 500;

        internal static long PreparationFuelCost(PreparationChoice choice)
        {
            switch (choice)
            {
                case PreparationChoice.WorkshopPush:
                    return WorkshopPreparationFuelUnits;
                case PreparationChoice.CivicBuffer:
                    return CivicBufferPreparationFuelUnits;
                default:
                    throw new ArgumentOutOfRangeException(nameof(choice));
            }
        }

        internal static long PreparationDuration(
            PreparationChoice choice,
            VehicleModule module)
        {
            checked
            {
                return PreparationBaseTicks(choice) + ModuleFabricationTicks(module);
            }
        }

        internal static long PreparationWaterModifier(PreparationChoice choice)
        {
            switch (choice)
            {
                case PreparationChoice.WorkshopPush:
                    return WorkshopWaterModifierMilliPerSettlementTick;
                case PreparationChoice.CivicBuffer:
                    return CivicBufferWaterModifierMilliPerSettlementTick;
                default:
                    throw new ArgumentOutOfRangeException(nameof(choice));
            }
        }

        internal static long ModulePartsCost(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return WinchPartsCostUnits;
                case VehicleModule.SealedRangeTank:
                    return TankPartsCostUnits;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module));
            }
        }

        internal static long ModuleInstallFuelCost(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return WinchInstallFuelUnits;
                case VehicleModule.SealedRangeTank:
                    return TankInstallFuelUnits;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module));
            }
        }

        internal static long RouteFuelCost(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return ShortRouteFuelManifestUnits;
                case VehicleModule.SealedRangeTank:
                    return LongRouteFuelManifestUnits;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module));
            }
        }

        internal static long RouteOneWayTicks(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return ShortRouteOneWayTicks;
                case VehicleModule.SealedRangeTank:
                    return LongRouteOneWayTicks;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module));
            }
        }

        internal static long RouteConditionLoss(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return ShortRouteRoundTripConditionLossMilli;
                case VehicleModule.SealedRangeTank:
                    return LongRouteRoundTripConditionLossMilli;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module));
            }
        }

        internal static RouteKind RouteFor(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return RouteKind.CollapsedShortBranch;
                case VehicleModule.SealedRangeTank:
                    return RouteKind.ExposedLongRoute;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module));
            }
        }

        internal static RouteActionKind RouteActionFor(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return RouteActionKind.DeployWinch;
                case VehicleModule.SealedRangeTank:
                    return RouteActionKind.CrossExposedDustRoute;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module));
            }
        }

        public static long WreckLineGateTicks(VehicleModule module)
        {
            var oneWayTicks = RouteOneWayTicks(module);
            if (oneWayTicks < 2)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_WRECK_LINE_ROUTE_TOO_SHORT");
            }

            return oneWayTicks / 2;
        }

        private static long PreparationBaseTicks(PreparationChoice choice)
        {
            switch (choice)
            {
                case PreparationChoice.WorkshopPush:
                    return WorkshopBasePreparationTicks;
                case PreparationChoice.CivicBuffer:
                    return CivicBufferBasePreparationTicks;
                default:
                    throw new ArgumentOutOfRangeException(nameof(choice));
            }
        }

        private static long ModuleFabricationTicks(VehicleModule module)
        {
            switch (module)
            {
                case VehicleModule.WinchAssembly:
                    return WinchFabricationTicks;
                case VehicleModule.SealedRangeTank:
                    return TankFabricationTicks;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module));
            }
        }
    }
}
