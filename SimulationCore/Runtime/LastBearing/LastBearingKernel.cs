#nullable enable

using System;
using System.Collections.Generic;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingKernel
    {
        public LastBearingTickResult Step(
            LastBearingState state,
            IReadOnlyList<LastBearingCommand> commands)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            LastBearingInvariants.Validate(state);
            RejectMultipleDriveCommands(commands);
            var expeditionWasAwayAtTickStart = IsAwayFromSettlement(
                state.ExpeditionPhase);
            var builder = new LastBearingStateBuilder(state);
            var events = new List<LastBearingDomainEvent>();
            var expectedSequence = builder.NextCommandSequence;

            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index]
                    ?? throw new ArgumentException(
                        "LAST_BEARING_NULL_COMMAND",
                        nameof(commands));
                if (command.Sequence != expectedSequence)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH");
                }

                ApplyCommand(builder, command, events);
                expectedSequence = checked(expectedSequence + 1);
                builder.NextCommandSequence = expectedSequence;
            }

            builder.GlobalTick = checked(builder.GlobalTick + 1);
            if (builder.PauseCause == PauseCause.None)
            {
                var expeditionWasAway = expeditionWasAwayAtTickStart
                    || IsAwayFromSettlement(builder.ExpeditionPhase);
                var homeScale = expeditionWasAway
                    ? LastBearingBalanceV1.ExpeditionHomeClockScaleMilli
                    : LastBearingBalanceV1.FullClockScaleMilli;
                var roadScale = expeditionWasAway
                    ? LastBearingBalanceV1.FullClockScaleMilli
                    : 0;

                AdvanceSettlementClock(builder, homeScale, events);
                AdvanceFactionClock(builder, homeScale, events);
                AdvanceCrisisClock(builder, homeScale);
                AdvanceRoadClock(builder, roadScale);
            }

            var nextState = builder.Build();
            return new LastBearingTickResult(
                nextState,
                events.ToArray(),
                LastBearingReadModel.FromState(nextState));
        }

        private static void RejectMultipleDriveCommands(
            IReadOnlyList<LastBearingCommand> commands)
        {
            var driveCommandSeen = false;
            for (var index = 0; index < commands.Count; index++)
            {
                if (!(commands[index] is DriveVehicleCommand))
                {
                    continue;
                }

                if (driveCommandSeen)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_MULTIPLE_DRIVE_COMMANDS_PER_STEP");
                }

                driveCommandSeen = true;
            }
        }

        private static void ApplyCommand(
            LastBearingStateBuilder builder,
            LastBearingCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (command is AssignResidentCommand assignResident)
            {
                ApplyAssignResident(builder, assignResident, events);
            }
            else if (command is ActivateSliceInfrastructureCommand activate)
            {
                ApplyActivateInfrastructure(builder, activate, events);
            }
            else if (command is SelectPreparationCommand selectPreparation)
            {
                ApplySelectPreparation(builder, selectPreparation, events);
            }
            else if (command is InstallVehicleModuleCommand installModule)
            {
                ApplyInstallModule(builder, installModule, events);
            }
            else if (command is PrepareExpeditionTransactionCommand prepare)
            {
                ApplyPrepareTransaction(builder, prepare, events);
            }
            else if (command is DebitCityManifestCommand debit)
            {
                ApplyDebitManifest(builder, debit, events);
            }
            else if (command is DepartExpeditionCommand depart)
            {
                ApplyDepart(builder, depart, events);
            }
            else if (command is DriveVehicleCommand drive)
            {
                ApplyDrive(builder, drive, events);
            }
            else if (command is OperateWreckLineModuleCommand operateModule)
            {
                ApplyOperateWreckLineModule(builder, operateModule, events);
            }
            else if (command is OperateDepotRecoveryPointCommand operateRecovery)
            {
                ApplyOperateDepotRecoveryPoint(
                    builder,
                    operateRecovery,
                    events);
            }
            else if (command is ResolveDepotCommand resolve)
            {
                ApplyResolveDepot(builder, resolve, events);
            }
            else if (command is ChooseLiquidReturnCommand chooseLiquid)
            {
                ApplyChooseLiquid(builder, chooseLiquid, events);
            }
            else if (command is FreezeReturnPayloadCommand freeze)
            {
                ApplyFreezeReturn(builder, freeze, events);
            }
            else if (command is ReturnHomeCommand returnHome)
            {
                ApplyReturnHome(builder, returnHome, events);
            }
            else if (command is CreditCityReturnCommand credit)
            {
                ApplyCreditReturn(builder, credit, events);
            }
            else if (command is FinalizeExpeditionTransactionCommand finalize)
            {
                ApplyFinalize(builder, finalize, events);
            }
            else if (command is InstallTurbineRepairCommand installRepair)
            {
                ApplyInstallRepair(builder, installRepair, events);
            }
            else if (command is InstallCityImprovementCommand installImprovement)
            {
                ApplyInstallCityImprovement(
                    builder,
                    installImprovement,
                    events);
            }
            else if (command is ServiceFieldSleeveCommand service)
            {
                ApplyServiceSleeve(builder, service, events);
            }
            else if (command is SetPauseCommand pause)
            {
                ApplySetPause(builder, pause, events);
            }
            else if (command is TriggerAutoPauseAlertCommand autoPause)
            {
                ApplyAutoPause(builder, autoPause, events);
            }
            else
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_COMMAND_TYPE_UNKNOWN");
            }
        }

        private static void ApplyAssignResident(
            LastBearingStateBuilder builder,
            AssignResidentCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (!builder.Roster.Contains(command.StableId))
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_ASSIGNED_RESIDENT_NOT_IN_ROSTER");
            }

            if (string.Equals(
                builder.AssignedResidentId,
                command.StableId,
                StringComparison.Ordinal))
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            builder.AssignedResidentId = command.StableId;
            Emit(
                builder,
                events,
                LastBearingEventKind.ResidentAssigned,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                "expedition-slot:lead",
                0,
                1);
        }

        private static void ApplyActivateInfrastructure(
            LastBearingStateBuilder builder,
            ActivateSliceInfrastructureCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.SliceInfrastructureActive)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            builder.SliceInfrastructureActive = true;
            Emit(
                builder,
                events,
                LastBearingEventKind.SliceInfrastructureActivated,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                "settlement:last-bearing:workshop",
                0,
                1);
        }

        private static void ApplySelectPreparation(
            LastBearingStateBuilder builder,
            SelectPreparationCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.PreparationPhase != PreparationPhase.Unselected)
            {
                if (builder.PreparationChoice == command.Choice
                    && builder.PlannedModule == command.PlannedModule)
                {
                    EmitReplay(builder, command.Sequence, events);
                    return;
                }

                throw new InvalidOperationException(
                    "LAST_BEARING_PREPARATION_ALREADY_SELECTED");
            }

            if (!builder.SliceInfrastructureActive)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_INFRASTRUCTURE_REQUIRED");
            }

            var preparationFuel =
                LastBearingBalanceV1.PreparationFuelCost(command.Choice);
            var projectedFuel = checked(
                preparationFuel
                + LastBearingBalanceV1.ModuleInstallFuelCost(
                    command.PlannedModule)
                + LastBearingBalanceV1.RouteFuelCost(command.PlannedModule));
            var projectedParts = checked(
                LastBearingBalanceV1.ModulePartsCost(command.PlannedModule)
                + LastBearingBalanceV1.MinimumPostReturnPartsUnits);
            if (builder.FuelUnits < projectedFuel
                || builder.PartsUnits < projectedParts)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_PREPARATION_RESOURCES_INSUFFICIENT");
            }

            builder.FuelUnits = checked(builder.FuelUnits - preparationFuel);
            builder.PreparationChoice = command.Choice;
            builder.PreparationPhase = PreparationPhase.Preparing;
            builder.PlannedModule = command.PlannedModule;
            builder.PreparationElapsedTicks = 0;
            builder.PreparationRequiredTicks =
                LastBearingBalanceV1.PreparationDuration(
                    command.Choice,
                    command.PlannedModule);
            builder.PreparationFuelDebitedUnits = preparationFuel;
            builder.WorkshopServiceSlotsReserved =
                command.Choice == PreparationChoice.WorkshopPush ? 1 : 0;
            builder.ActiveWaterModifierMilliPerSettlementTick =
                LastBearingBalanceV1.PreparationWaterModifier(command.Choice);

            Emit(
                builder,
                events,
                LastBearingEventKind.PreparationStarted,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                "settlement:last-bearing:preparation",
                0,
                builder.PreparationRequiredTicks);
        }

        private static void ApplyInstallModule(
            LastBearingStateBuilder builder,
            InstallVehicleModuleCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.PlannedModule != command.Module
                || builder.PreparationPhase == PreparationPhase.Unselected)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_MODULE_DOES_NOT_MATCH_PLAN");
            }

            if (builder.ModuleInstallationState != ModuleInstallationState.None)
            {
                if (builder.PlannedModule == command.Module)
                {
                    EmitReplay(builder, command.Sequence, events);
                    return;
                }

                throw new InvalidOperationException(
                    "LAST_BEARING_MODULE_ALREADY_AUTHORIZED");
            }

            var partsCost = LastBearingBalanceV1.ModulePartsCost(command.Module);
            var fuelCost =
                LastBearingBalanceV1.ModuleInstallFuelCost(command.Module);
            if (builder.PartsUnits < partsCost || builder.FuelUnits < fuelCost)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_MODULE_RESOURCES_INSUFFICIENT");
            }

            builder.PartsUnits = checked(builder.PartsUnits - partsCost);
            builder.FuelUnits = checked(builder.FuelUnits - fuelCost);
            if (builder.PreparationPhase == PreparationPhase.Preparing)
            {
                builder.ModuleInstallationState = ModuleInstallationState.Pending;
            }
            else
            {
                CompleteModuleInstallation(builder, command.Sequence, events);
            }
        }

        private static void ApplyPrepareTransaction(
            LastBearingStateBuilder builder,
            PrepareExpeditionTransactionCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.TransactionPhase != TransactionPhase.None)
            {
                EnsureTransactionIdentity(
                    builder,
                    command.TransactionId,
                    command.Fingerprint);
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (builder.AssignedResidentId == null
                || !builder.SliceInfrastructureActive
                || builder.PreparationPhase == PreparationPhase.Unselected
                || builder.ModuleInstallationState == ModuleInstallationState.None)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_TRANSACTION_PREREQUISITES_MISSING");
            }

            builder.TransactionId = command.TransactionId;
            builder.TransactionFingerprint = command.Fingerprint;
            builder.TransactionPhase = TransactionPhase.Prepared;
            Emit(
                builder,
                events,
                LastBearingEventKind.ExpeditionTransactionPrepared,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                command.TransactionId,
                (long)TransactionPhase.None,
                (long)TransactionPhase.Prepared);
        }

        private static void ApplyDebitManifest(
            LastBearingStateBuilder builder,
            DebitCityManifestCommand command,
            List<LastBearingDomainEvent> events)
        {
            EnsureTransactionIdentity(
                builder,
                command.TransactionId,
                command.Fingerprint);
            if (builder.TransactionPhase > TransactionPhase.Prepared)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (builder.TransactionPhase != TransactionPhase.Prepared)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_TRANSACTION_NOT_PREPARED");
            }

            var routeFuel = LastBearingBalanceV1.RouteFuelCost(
                builder.PlannedModule);
            if (builder.FuelUnits < routeFuel)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_ROUTE_FUEL_INSUFFICIENT");
            }

            builder.FuelUnits = checked(builder.FuelUnits - routeFuel);
            builder.ExpeditionFuelManifestUnits = routeFuel;
            builder.TransactionPhase = TransactionPhase.CityDebited;
            Emit(
                builder,
                events,
                LastBearingEventKind.ExpeditionFuelCommitted,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                command.TransactionId,
                0,
                routeFuel);
            TryStartDeparture(builder, command.Sequence, events);
        }

        private static void ApplyDepart(
            LastBearingStateBuilder builder,
            DepartExpeditionCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.TransactionPhase > TransactionPhase.CityDebited)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (!TryStartDeparture(builder, command.Sequence, events))
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_DEPARTURE_NOT_READY");
            }
        }

        private static void ApplyDrive(
            LastBearingStateBuilder builder,
            DriveVehicleCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.PauseCause != PauseCause.None)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_DRIVE_WHILE_PAUSED");
            }

            if (builder.ExpeditionPhase != ExpeditionPhase.Outbound
                && builder.ExpeditionPhase != ExpeditionPhase.Returning)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_DRIVE_PHASE_INVALID");
            }

            if (IsWreckLineModulePointAvailable(builder))
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_WRECK_LINE_MODULE_ACTION_REQUIRED");
            }

            if (IsDepotApproachRecoveryAvailable(builder))
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_DEPOT_RECOVERY_REQUIRED");
            }

            var before = builder.RouteProgressTicks;
            var previousLateral = builder.VehicleLateralMilli;
            var steeringDelta =
                command.SteeringMilli / LastBearingBalanceV1.SteeringResponseDivisor;
            builder.VehicleLateralMilli = Math.Max(
                -LastBearingBalanceV1.RoadLateralLimitMilli,
                Math.Min(
                    LastBearingBalanceV1.RoadLateralLimitMilli,
                    checked(builder.VehicleLateralMilli + steeringDelta)));
            if (builder.VehicleLateralMilli != previousLateral)
            {
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.VehicleSteered,
                    LastBearingEventCause.PlayerCommand,
                    builder.RoadTick,
                    command.Sequence,
                    "vehicle:sasha:lateral",
                    previousLateral,
                    builder.VehicleLateralMilli);
            }

            var accumulator = checked(
                builder.RouteMovementAccumulatorMilli + command.ThrottleMilli);
            var progress = accumulator / LastBearingBalanceV1.FullClockScaleMilli;
            builder.RouteMovementAccumulatorMilli =
                accumulator % LastBearingBalanceV1.FullClockScaleMilli;
            if (progress > 0)
            {
                var progressLimit = builder.ExpeditionPhase ==
                        ExpeditionPhase.Outbound &&
                    !builder.RouteActionUsed
                    ? LastBearingBalanceV1.WreckLineGateTicks(
                        builder.VehicleModule)
                    : builder.RouteTargetTicks;
                builder.RouteProgressTicks = Math.Min(
                    progressLimit,
                    checked(builder.RouteProgressTicks + progress));
                if (Math.Abs(builder.VehicleLateralMilli)
                    > LastBearingBalanceV1.RoadSafeHalfWidthMilli)
                {
                    var previousCondition = builder.VehicleConditionMilli;
                    builder.VehicleConditionMilli = Math.Max(
                        0,
                        checked(
                            builder.VehicleConditionMilli
                            - (LastBearingBalanceV1
                                .RoadEdgeConditionLossPerProgressTickMilli
                                * progress)));
                    Emit(
                        builder,
                        events,
                        LastBearingEventKind.VehicleConditionChanged,
                        LastBearingEventCause.PlayerCommand,
                        builder.RoadTick,
                        command.Sequence,
                        "vehicle:sasha:road-edge",
                        previousCondition,
                        builder.VehicleConditionMilli);
                }
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.RouteProgressed,
                    LastBearingEventCause.PlayerCommand,
                    builder.RoadTick,
                    command.Sequence,
                    "vehicle:sasha:route",
                    before,
                    builder.RouteProgressTicks);
            }

            if (builder.RouteProgressTicks == builder.RouteTargetTicks)
            {
                if (builder.ExpeditionPhase == ExpeditionPhase.Outbound)
                {
                    builder.HasArrivalClaimSnapshot = true;
                    builder.ArrivalFactionClaimProgressMilli =
                        builder.FactionClaimProgressMilli;
                    builder.ArrivalFactionClaimState = builder.FactionClaimState;
                }
                else
                {
                    builder.ExpeditionPhase = ExpeditionPhase.Returned;
                    Emit(
                        builder,
                        events,
                        LastBearingEventKind.VehicleReturned,
                        LastBearingEventCause.PlayerCommand,
                        builder.RoadTick,
                        command.Sequence,
                        "vehicle:sasha",
                        (long)ExpeditionPhase.Returning,
                        (long)ExpeditionPhase.Returned);
                }

                builder.RouteMovementAccumulatorMilli = 0;
            }
        }

        private static void ApplyOperateWreckLineModule(
            LastBearingStateBuilder builder,
            OperateWreckLineModuleCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (command.Action != builder.RouteActionKind)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_WRECK_LINE_MODULE_ACTION_MISMATCH");
            }

            if (builder.RouteActionUsed)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (!IsWreckLineModulePointAvailable(builder))
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_WRECK_LINE_MODULE_POINT_NOT_READY");
            }

            if (builder.VehicleModule == VehicleModule.WinchAssembly)
            {
                LastBearingOwnershipTransaction.RecoverHeavyCargoToVehicle(
                    builder);
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.HeavyCargoTransferred,
                    LastBearingEventCause.PlayerCommand,
                    builder.RoadTick,
                    command.Sequence,
                    "cargo:last-bearing:pump-rotor",
                    (long)HeavyCargoCustody.Depot,
                    (long)HeavyCargoCustody.Vehicle);
            }
            else if (builder.VehicleModule != VehicleModule.SealedRangeTank)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_WRECK_LINE_MODULE_INVALID");
            }

            builder.RouteMovementAccumulatorMilli = 0;
            builder.RouteActionUsed = true;
            Emit(
                builder,
                events,
                LastBearingEventKind.RouteActionUsed,
                LastBearingEventCause.PlayerCommand,
                builder.RoadTick,
                command.Sequence,
                "world:last-bearing:wreck-line",
                0,
                (long)command.Action);
        }

        private static bool IsWreckLineModulePointAvailable(
            LastBearingStateBuilder builder)
        {
            return builder.ExpeditionPhase == ExpeditionPhase.Outbound
                && builder.TransactionPhase == TransactionPhase.RoadOwned
                && builder.VehicleModule != VehicleModule.None
                && !builder.RouteActionUsed
                && builder.RouteProgressTicks ==
                    LastBearingBalanceV1.WreckLineGateTicks(
                        builder.VehicleModule);
        }

        private static void ApplyOperateDepotRecoveryPoint(
            LastBearingStateBuilder builder,
            OperateDepotRecoveryPointCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.HasArrivalClaimSnapshot
                && builder.ExpeditionPhase != ExpeditionPhase.Outbound)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (!IsDepotApproachRecoveryAvailable(builder))
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_DEPOT_RECOVERY_NOT_READY");
            }

            builder.RouteMovementAccumulatorMilli = 0;
            builder.ExpeditionPhase = ExpeditionPhase.AtDepot;
            Emit(
                builder,
                events,
                LastBearingEventKind.DepotRecoveryPointOperated,
                LastBearingEventCause.PlayerCommand,
                builder.RoadTick,
                command.Sequence,
                "world:last-bearing:depot-approach-recovery",
                (long)ExpeditionPhase.Outbound,
                (long)ExpeditionPhase.AtDepot);
        }

        private static bool IsDepotApproachRecoveryAvailable(
            LastBearingStateBuilder builder)
        {
            return builder.ExpeditionPhase == ExpeditionPhase.Outbound
                && builder.TransactionPhase == TransactionPhase.RoadOwned
                && builder.RouteActionUsed
                && builder.RouteTargetTicks > 0
                && builder.RouteProgressTicks == builder.RouteTargetTicks
                && builder.HasArrivalClaimSnapshot;
        }

        private static void ApplyResolveDepot(
            LastBearingStateBuilder builder,
            ResolveDepotCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.DepotResolution != EncounterChoice.Unresolved)
            {
                if (builder.DepotResolution == command.Choice)
                {
                    EmitReplay(builder, command.Sequence, events);
                    return;
                }

                throw new InvalidOperationException(
                    "LAST_BEARING_DEPOT_ALREADY_RESOLVED");
            }

            if (builder.ExpeditionPhase != ExpeditionPhase.AtDepot
                || builder.TransactionPhase != TransactionPhase.RoadOwned)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_DEPOT_RESOLUTION_PHASE_INVALID");
            }

            if (builder.PauseCause == PauseCause.AutoAlert)
            {
                builder.PauseCause = PauseCause.None;
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.PauseChanged,
                    LastBearingEventCause.SystemTransition,
                    builder.GlobalTick,
                    command.Sequence,
                    "simulation:last-bearing:auto-pause",
                    (long)PauseCause.AutoAlert,
                    (long)PauseCause.None);
            }

            builder.DepotResolution = command.Choice;
            if (command.Choice == EncounterChoice.Cooperate)
            {
                ApplyCooperativeOutcome(builder, command.Sequence, events);
            }
            else
            {
                ApplyAdverseOutcome(builder, command.Sequence, events);
            }

            Emit(
                builder,
                events,
                LastBearingEventKind.DepotResolved,
                LastBearingEventCause.PlayerCommand,
                builder.FactionTick,
                command.Sequence,
                "world:last-bearing:depot",
                (long)EncounterChoice.Unresolved,
                (long)command.Choice);
        }

        private static void ApplyChooseLiquid(
            LastBearingStateBuilder builder,
            ChooseLiquidReturnCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.VehicleModule != VehicleModule.SealedRangeTank
                || builder.DepotResolution == EncounterChoice.Unresolved
                || builder.ReturnPayloadFrozen)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_LIQUID_SELECTION_PHASE_INVALID");
            }

            if (builder.LiquidCargoKind != LiquidCargoKind.None)
            {
                if (builder.LiquidCargoKind == command.Kind)
                {
                    EmitReplay(builder, command.Sequence, events);
                    return;
                }

                throw new InvalidOperationException(
                    "LAST_BEARING_LIQUID_ALREADY_SELECTED");
            }

            long quantity;
            if (command.Kind == LiquidCargoKind.Water)
            {
                quantity = LastBearingBalanceV1.TankWaterReturnMilli;
            }
            else
            {
                var projectedWater = ProjectWaterAtReturn(builder);
                if (builder.PreparationChoice != PreparationChoice.CivicBuffer
                    || projectedWater
                        < LastBearingBalanceV1.MinimumRecoverableWaterMilli)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_FUEL_RETURN_WATER_UNSAFE");
                }

                quantity = LastBearingBalanceV1.TankFuelReturnMilli;
            }

            LastBearingOwnershipTransaction.CreateLiquidCargo(
                builder,
                command.Kind,
                quantity);
            Emit(
                builder,
                events,
                LastBearingEventKind.LiquidCargoTransferred,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                "cargo:last-bearing:liquid",
                0,
                quantity);
        }

        private static void ApplyFreezeReturn(
            LastBearingStateBuilder builder,
            FreezeReturnPayloadCommand command,
            List<LastBearingDomainEvent> events)
        {
            EnsureTransactionIdentity(
                builder,
                command.TransactionId,
                command.Fingerprint);
            if (builder.TransactionPhase >= TransactionPhase.ReturnPending)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (builder.TransactionPhase != TransactionPhase.RoadOwned
                || builder.ExpeditionPhase != ExpeditionPhase.AtDepot
                || builder.DepotResolution == EncounterChoice.Unresolved
                || !builder.RouteActionUsed)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_RETURN_FREEZE_PHASE_INVALID");
            }

            if (builder.VehicleModule == VehicleModule.WinchAssembly)
            {
                if (builder.HeavyCargoKind != HeavyCargoKind.PumpRotor
                    || builder.HeavyCargoCustody
                        != HeavyCargoCustody.Vehicle
                    || builder.TowSlotsUsed != 1)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_WINCH_ROTOR_NOT_RECOVERED");
                }
            }
            else if (builder.LiquidCargoKind == LiquidCargoKind.None)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_TANK_RETURN_SELECTION_REQUIRED");
            }

            builder.OrdinaryCargoUsedUnits = 1;
            builder.ReturnPayloadFrozen = true;
            builder.TransactionPhase = TransactionPhase.ReturnPending;
            builder.ExpeditionPhase = ExpeditionPhase.Returning;
            builder.RouteProgressTicks = 0;
            builder.RouteMovementAccumulatorMilli = 0;
            builder.VehicleLateralMilli = 0;
            Emit(
                builder,
                events,
                LastBearingEventKind.ReturnPayloadFrozen,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                command.TransactionId,
                (long)TransactionPhase.RoadOwned,
                (long)TransactionPhase.ReturnPending);
        }

        private static void ApplyReturnHome(
            LastBearingStateBuilder builder,
            ReturnHomeCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.ExpeditionPhase != ExpeditionPhase.Returned
                || builder.TransactionPhase != TransactionPhase.ReturnPending)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_VEHICLE_NOT_HOME");
            }

            EmitReplay(builder, command.Sequence, events);
        }

        private static void ApplyCreditReturn(
            LastBearingStateBuilder builder,
            CreditCityReturnCommand command,
            List<LastBearingDomainEvent> events)
        {
            EnsureTransactionIdentity(
                builder,
                command.TransactionId,
                command.Fingerprint);
            if (builder.TransactionPhase >= TransactionPhase.CityCredited)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (builder.TransactionPhase != TransactionPhase.ReturnPending
                || builder.ExpeditionPhase != ExpeditionPhase.Returned)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_RETURN_NOT_READY");
            }

            var previousCondition = builder.VehicleConditionMilli;
            builder.VehicleConditionMilli = Math.Max(
                0,
                checked(
                    builder.VehicleConditionMilli
                    - LastBearingBalanceV1.RouteConditionLoss(
                        builder.VehicleModule)));
            Emit(
                builder,
                events,
                LastBearingEventKind.VehicleConditionChanged,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                "vehicle:sasha",
                previousCondition,
                builder.VehicleConditionMilli);

            LastBearingOwnershipTransaction.TransferHeavyCargoToSettlement(
                builder);
            CreditLiquidCargo(builder, command.Sequence, events);
            ApplyCooperativeReturnConsequences(builder, command.Sequence, events);

            var previousDecision = builder.NextCityDecision;
            builder.NextCityDecision = DetermineNextCityDecision(builder);
            Emit(
                builder,
                events,
                LastBearingEventKind.NextCityDecisionSet,
                LastBearingEventCause.PlayerCommand,
                builder.SettlementTick,
                command.Sequence,
                "settlement:last-bearing:next-decision",
                (long)previousDecision,
                (long)builder.NextCityDecision);
            builder.TransactionPhase = TransactionPhase.CityCredited;
            Emit(
                builder,
                events,
                LastBearingEventKind.CityReturnCredited,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                command.TransactionId,
                (long)TransactionPhase.ReturnPending,
                (long)TransactionPhase.CityCredited);
        }

        private static void ApplyFinalize(
            LastBearingStateBuilder builder,
            FinalizeExpeditionTransactionCommand command,
            List<LastBearingDomainEvent> events)
        {
            EnsureTransactionIdentity(
                builder,
                command.TransactionId,
                command.Fingerprint);
            if (builder.TransactionPhase == TransactionPhase.Finalized)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (builder.TransactionPhase != TransactionPhase.CityCredited)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_TRANSACTION_NOT_CITY_CREDITED");
            }

            builder.TransactionPhase = TransactionPhase.Finalized;
            builder.ExpeditionPhase = ExpeditionPhase.AtHome;
            Emit(
                builder,
                events,
                LastBearingEventKind.ExpeditionTransactionFinalized,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                command.TransactionId,
                (long)TransactionPhase.CityCredited,
                (long)TransactionPhase.Finalized);
        }

        private static void ApplyInstallRepair(
            LastBearingStateBuilder builder,
            InstallTurbineRepairCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.TurbineCondition != TurbineCondition.Failing)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (builder.ExpeditionPhase != ExpeditionPhase.AtHome
                || builder.TransactionPhase != TransactionPhase.Finalized
                || builder.RepairCargoCustody != RepairCargoCustody.Vehicle)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_TURBINE_REPAIR_NOT_READY");
            }

            var previousTrend = ComputeWaterTrend(builder);
            if (builder.RepairCargoKind == RepairCargoKind.CeramicBearing)
            {
                LastBearingOwnershipTransaction.TransferRepairCargo(
                    builder,
                    RepairCargoKind.CeramicBearing,
                    RepairCargoCustody.Vehicle,
                    RepairCargoCustody.Turbine);
                builder.DepotBearingDisposition =
                    DepotBearingDisposition.InstalledAtTurbine;
                builder.TurbineCondition = TurbineCondition.BearingRepaired;
            }
            else if (builder.RepairCargoKind == RepairCargoKind.FieldSleeve)
            {
                LastBearingOwnershipTransaction.TransferRepairCargo(
                    builder,
                    RepairCargoKind.FieldSleeve,
                    RepairCargoCustody.Vehicle,
                    RepairCargoCustody.Consumed);
                builder.TurbineCondition = TurbineCondition.SleeveRepaired;
            }
            else
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_REPAIR_CARGO_MISSING");
            }

            Emit(
                builder,
                events,
                LastBearingEventKind.TurbineRepaired,
                LastBearingEventCause.PlayerCommand,
                builder.SettlementTick,
                command.Sequence,
                "settlement:last-bearing:turbine",
                previousTrend,
                ComputeWaterTrend(builder));
        }

        private static void ApplyInstallCityImprovement(
            LastBearingStateBuilder builder,
            InstallCityImprovementCommand command,
            List<LastBearingDomainEvent> events)
        {
            bool exactAuxiliaryPumpRequest =
                command.Decision == NextCityDecision.RefurbishAuxiliaryPump
                && string.Equals(
                    command.SocketId,
                    LastBearingState.AuxiliaryPumpSocketId,
                    StringComparison.Ordinal)
                && command.OrientationQuarterTurns
                    == LastBearingState.AuxiliaryPumpOrientationQuarterTurns;
            if (builder.InstalledCityImprovement != CityImprovementKind.None)
            {
                if (builder.InstalledCityImprovement
                        == CityImprovementKind.RefurbishedAuxiliaryPump
                    && exactAuxiliaryPumpRequest)
                {
                    EmitReplay(builder, command.Sequence, events);
                    return;
                }

                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_IMPROVEMENT_ALREADY_INSTALLED");
            }

            if (command.Decision != builder.NextCityDecision)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_IMPROVEMENT_DECISION_MISMATCH");
            }

            if (command.Decision != NextCityDecision.RefurbishAuxiliaryPump)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_IMPROVEMENT_UNSUPPORTED");
            }

            if (!string.Equals(
                    command.SocketId,
                    LastBearingState.AuxiliaryPumpSocketId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_IMPROVEMENT_SOCKET_INVALID");
            }

            if (command.OrientationQuarterTurns
                != LastBearingState.AuxiliaryPumpOrientationQuarterTurns)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_IMPROVEMENT_ORIENTATION_INVALID");
            }

            if (builder.ExpeditionPhase != ExpeditionPhase.AtHome
                || builder.TransactionPhase != TransactionPhase.Finalized
                || builder.TurbineCondition == TurbineCondition.Failing)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_IMPROVEMENT_PHASE_INVALID");
            }

            if (builder.PreparationChoice != PreparationChoice.WorkshopPush
                || builder.VehicleModule != VehicleModule.WinchAssembly
                || !builder.RouteActionUsed
                || builder.HeavyCargoKind != HeavyCargoKind.PumpRotor
                || builder.HeavyCargoCustody != HeavyCargoCustody.Settlement
                || builder.TowSlotsUsed != 1)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_IMPROVEMENT_CARGO_INVALID");
            }

            long partsCost = LastBearingBalanceV1.CityImprovementPartsCost(
                CityImprovementKind.RefurbishedAuxiliaryPump);
            long requiredParts = checked(
                partsCost + LastBearingBalanceV1.MinimumPostReturnPartsUnits);
            if (builder.PartsUnits < requiredParts)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_CITY_IMPROVEMENT_PARTS_INSUFFICIENT");
            }

            long previousParts = builder.PartsUnits;
            builder.PartsUnits = checked(builder.PartsUnits - partsCost);
            LastBearingOwnershipTransaction.InstallHeavyCargoAtAuxiliaryPump(
                builder);
            builder.InstalledCityImprovement =
                CityImprovementKind.RefurbishedAuxiliaryPump;
            builder.NextCityDecision = NextCityDecision.None;
            Emit(
                builder,
                events,
                LastBearingEventKind.CityResourcesCommitted,
                LastBearingEventCause.PlayerCommand,
                builder.SettlementTick,
                command.Sequence,
                "settlement:last-bearing:parts",
                previousParts,
                builder.PartsUnits);
            Emit(
                builder,
                events,
                LastBearingEventKind.HeavyCargoTransferred,
                LastBearingEventCause.PlayerCommand,
                builder.SettlementTick,
                command.Sequence,
                "cargo:last-bearing:pump-rotor",
                (long)HeavyCargoCustody.Settlement,
                (long)HeavyCargoCustody.InstalledAtAuxiliaryPump);
            Emit(
                builder,
                events,
                LastBearingEventKind.CityImprovementInstalled,
                LastBearingEventCause.PlayerCommand,
                builder.SettlementTick,
                command.Sequence,
                LastBearingState.AuxiliaryPumpSocketId,
                (long)CityImprovementKind.None,
                (long)builder.InstalledCityImprovement);
        }

        private static void ApplyServiceSleeve(
            LastBearingStateBuilder builder,
            ServiceFieldSleeveCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (!builder.MaintenanceObligationActive || !builder.MaintenanceDue
                || builder.MaintenanceRecipe != MaintenanceRecipe.FieldSleeveService)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_MAINTENANCE_NOT_DUE");
            }

            if (builder.PartsUnits < builder.MaintenancePartsUnits)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_MAINTENANCE_PARTS_INSUFFICIENT");
            }

            builder.PartsUnits = checked(
                builder.PartsUnits - builder.MaintenancePartsUnits);
            var previousDue = builder.NextMaintenanceDueSettlementTick;
            builder.NextMaintenanceDueSettlementTick = checked(
                builder.SettlementTick
                + LastBearingBalanceV1.SleeveMaintenanceIntervalSettlementTicks);
            builder.MaintenanceDue = false;
            Emit(
                builder,
                events,
                LastBearingEventKind.MaintenanceServiced,
                LastBearingEventCause.PlayerCommand,
                builder.SettlementTick,
                command.Sequence,
                "settlement:last-bearing:sleeve-service",
                previousDue,
                builder.NextMaintenanceDueSettlementTick);
        }

        private static void ApplySetPause(
            LastBearingStateBuilder builder,
            SetPauseCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.PauseCause == PauseCause.AutoAlert)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_AUTO_ALERT_REQUIRES_ENCOUNTER_RESOLUTION");
            }

            var next = command.IsPaused ? PauseCause.Explicit : PauseCause.None;
            if (builder.PauseCause == next)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            var previous = builder.PauseCause;
            builder.PauseCause = next;
            Emit(
                builder,
                events,
                LastBearingEventKind.PauseChanged,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                command.Sequence,
                "simulation:last-bearing:pause",
                (long)previous,
                (long)next);
        }

        private static void ApplyAutoPause(
            LastBearingStateBuilder builder,
            TriggerAutoPauseAlertCommand command,
            List<LastBearingDomainEvent> events)
        {
            if (builder.PauseCause == PauseCause.AutoAlert)
            {
                EmitReplay(builder, command.Sequence, events);
                return;
            }

            if (builder.PauseCause == PauseCause.Explicit)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_EXPLICIT_PAUSE_ALREADY_ACTIVE");
            }

            if (builder.ExpeditionPhase != ExpeditionPhase.AtDepot ||
                builder.DepotResolution != EncounterChoice.Unresolved)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_AUTO_ALERT_REQUIRES_UNRESOLVED_DEPOT");
            }

            builder.PauseCause = PauseCause.AutoAlert;
            Emit(
                builder,
                events,
                LastBearingEventKind.PauseChanged,
                LastBearingEventCause.SystemTransition,
                builder.GlobalTick,
                command.Sequence,
                "simulation:last-bearing:auto-pause",
                (long)PauseCause.None,
                (long)PauseCause.AutoAlert);
        }

        private static void CompleteModuleInstallation(
            LastBearingStateBuilder builder,
            long commandSequence,
            List<LastBearingDomainEvent> events)
        {
            builder.VehicleModule = builder.PlannedModule;
            builder.ModuleInstallationState = ModuleInstallationState.Installed;
            builder.RouteKind = LastBearingBalanceV1.RouteFor(
                builder.VehicleModule);
            builder.RouteActionKind = LastBearingBalanceV1.RouteActionFor(
                builder.VehicleModule);
            builder.RouteTargetTicks = LastBearingBalanceV1.RouteOneWayTicks(
                builder.VehicleModule);
            if (builder.VehicleModule == VehicleModule.WinchAssembly)
            {
                builder.OrdinaryCargoCapacityUnits =
                    LastBearingBalanceV1.WinchOrdinaryCargoUnits;
                builder.TowSlots = LastBearingBalanceV1.WinchTowSlots;
                builder.LiquidCapacityMilli = 0;
            }
            else
            {
                builder.OrdinaryCargoCapacityUnits =
                    LastBearingBalanceV1.TankOrdinaryCargoUnits;
                builder.TowSlots = 0;
                builder.LiquidCapacityMilli =
                    LastBearingBalanceV1.TankLiquidCapacityMilli;
            }

            Emit(
                builder,
                events,
                LastBearingEventKind.VehicleModuleInstalled,
                commandSequence == LastBearingDomainEvent.AutonomousCommandSequence
                    ? LastBearingEventCause.AutonomousSettlementTick
                    : LastBearingEventCause.PlayerCommand,
                builder.SettlementTick,
                commandSequence,
                "vehicle:sasha:module",
                (long)VehicleModule.None,
                (long)builder.VehicleModule);
        }

        private static bool TryStartDeparture(
            LastBearingStateBuilder builder,
            long commandSequence,
            List<LastBearingDomainEvent> events)
        {
            if (builder.TransactionPhase != TransactionPhase.CityDebited
                || builder.PreparationPhase != PreparationPhase.Ready
                || builder.ModuleInstallationState
                    != ModuleInstallationState.Installed)
            {
                return false;
            }

            builder.TransactionPhase = TransactionPhase.RoadOwned;
            builder.PreparationPhase = PreparationPhase.Committed;
            builder.ExpeditionPhase = ExpeditionPhase.Outbound;
            builder.RouteProgressTicks = 0;
            builder.RouteMovementAccumulatorMilli = 0;
            builder.VehicleLateralMilli = 0;
            builder.RouteActionUsed = false;
            Emit(
                builder,
                events,
                LastBearingEventKind.ExpeditionDeparted,
                commandSequence == LastBearingDomainEvent.AutonomousCommandSequence
                    ? LastBearingEventCause.AutonomousSettlementTick
                    : LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                commandSequence,
                "vehicle:sasha",
                (long)ExpeditionPhase.AtHome,
                (long)ExpeditionPhase.Outbound);
            return true;
        }

        private static void ApplyCooperativeOutcome(
            LastBearingStateBuilder builder,
            long commandSequence,
            List<LastBearingDomainEvent> events)
        {
            LastBearingOwnershipTransaction.CreateRepairCargo(
                builder,
                RepairCargoKind.FieldSleeve,
                RepairCargoCustody.Vehicle);
            builder.DepotBearingDisposition = DepotBearingDisposition.FactionHeld;
            builder.DepotControl = DepotControl.SharedAccess;
            builder.FactionClaimState = FactionClaimState.Cooperating;
            builder.FactionTrust = checked(
                builder.FactionTrust + LastBearingBalanceV1.CooperateTrustDelta);
            builder.PendingFactionOutcome = FactionOutcomeKind.Cooperative;
            builder.FactionOutcomeElapsedTicks = 0;
            builder.FactionMemory = new FactionMemoryRecord(
                "memory:last-bearing:cooperate:0001",
                "CooperateAtBearingDepot",
                LastBearingState.LastBearingFactionId,
                LastBearingBalanceV1.CooperateTrustDelta,
                "shared-maintenance",
                builder.GlobalTick,
                "FIELD_SLEEVE_SERVICE");
            Emit(
                builder,
                events,
                LastBearingEventKind.RepairCargoTransferred,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                commandSequence,
                "cargo:last-bearing:field-sleeve",
                (long)RepairCargoCustody.None,
                (long)RepairCargoCustody.Vehicle);
            Emit(
                builder,
                events,
                LastBearingEventKind.FactionMemoryRecorded,
                LastBearingEventCause.PlayerCommand,
                builder.FactionTick,
                commandSequence,
                builder.FactionMemory.StableId,
                0,
                builder.FactionMemory.Magnitude);
        }

        private static void ApplyAdverseOutcome(
            LastBearingStateBuilder builder,
            long commandSequence,
            List<LastBearingDomainEvent> events)
        {
            if (builder.DepotBearingDisposition != DepotBearingDisposition.AtDepot
                && builder.DepotBearingDisposition
                    != DepotBearingDisposition.FactionHeld)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_BEARING_NOT_AT_DEPOT");
            }

            LastBearingOwnershipTransaction.CreateRepairCargo(
                builder,
                RepairCargoKind.CeramicBearing,
                RepairCargoCustody.Vehicle);
            builder.DepotBearingDisposition =
                DepotBearingDisposition.TakenBySasha;
            builder.DepotControl = DepotControl.Depleted;
            builder.FactionClaimState = FactionClaimState.Aggrieved;
            builder.FactionTrust = checked(
                builder.FactionTrust + LastBearingBalanceV1.TakeTrustDelta);
            builder.FactionGrievance = checked(
                builder.FactionGrievance
                + LastBearingBalanceV1.TakeGrievanceDelta);
            builder.PendingFactionOutcome = FactionOutcomeKind.Adverse;
            builder.FactionOutcomeElapsedTicks = 0;
            builder.FactionMemory = new FactionMemoryRecord(
                "memory:last-bearing:take:0001",
                "TakeClaimedBearing",
                LastBearingState.LastBearingFactionId,
                LastBearingBalanceV1.TakeGrievanceDelta,
                "custody-breach",
                builder.GlobalTick,
                "DEPOT_ACCESS_CLOSED");
            Emit(
                builder,
                events,
                LastBearingEventKind.RepairCargoTransferred,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                commandSequence,
                "cargo:last-bearing:ceramic-bearing",
                (long)RepairCargoCustody.None,
                (long)RepairCargoCustody.Vehicle);
            Emit(
                builder,
                events,
                LastBearingEventKind.FactionMemoryRecorded,
                LastBearingEventCause.PlayerCommand,
                builder.FactionTick,
                commandSequence,
                builder.FactionMemory.StableId,
                0,
                builder.FactionMemory.Magnitude);
            Emit(
                builder,
                events,
                LastBearingEventKind.FactionGrievanceRecorded,
                LastBearingEventCause.PlayerCommand,
                builder.FactionTick,
                commandSequence,
                LastBearingState.LastBearingFactionId,
                0,
                builder.FactionGrievance);
        }

        private static void CreditLiquidCargo(
            LastBearingStateBuilder builder,
            long commandSequence,
            List<LastBearingDomainEvent> events)
        {
            if (builder.LiquidCargoKind == LiquidCargoKind.None)
            {
                return;
            }

            var quantity = builder.LiquidCargoQuantityMilli;
            if (builder.LiquidCargoKind == LiquidCargoKind.Water)
            {
                builder.WaterMilli = Math.Min(
                    LastBearingBalanceV1.WaterCapacityMilli,
                    checked(builder.WaterMilli + quantity));
            }
            else
            {
                if (quantity % 1000 != 0)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_FUEL_CARGO_NOT_WHOLE_UNITS");
                }

                builder.FuelUnits = checked(builder.FuelUnits + (quantity / 1000));
            }

            LastBearingOwnershipTransaction.TransferLiquidCargoToSettlement(
                builder);
            Emit(
                builder,
                events,
                LastBearingEventKind.LiquidCargoTransferred,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                commandSequence,
                "cargo:last-bearing:liquid",
                (long)LiquidCargoCustody.Vehicle,
                (long)LiquidCargoCustody.Settlement);
        }

        private static void ApplyCooperativeReturnConsequences(
            LastBearingStateBuilder builder,
            long commandSequence,
            List<LastBearingDomainEvent> events)
        {
            if (builder.PendingFactionOutcome != FactionOutcomeKind.Cooperative)
            {
                return;
            }

            if (!builder.MaintenanceObligationActive)
            {
                builder.MaintenanceRecipe = MaintenanceRecipe.FieldSleeveService;
                builder.MaintenanceObligationActive = true;
                builder.MaintenancePartsUnits =
                    LastBearingBalanceV1.SleeveMaintenancePartsUnits;
                builder.NextMaintenanceDueSettlementTick = checked(
                    builder.SettlementTick
                    + LastBearingBalanceV1.SleeveMaintenanceIntervalSettlementTicks);
                builder.MaintenanceDue = false;
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.DoctrineRecipeActivated,
                    LastBearingEventCause.PlayerCommand,
                    builder.SettlementTick,
                    commandSequence,
                    "settlement:last-bearing:field-sleeve-service",
                    (long)MaintenanceRecipe.None,
                    (long)MaintenanceRecipe.FieldSleeveService);
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.MaintenanceObligationCreated,
                    LastBearingEventCause.PlayerCommand,
                    builder.SettlementTick,
                    commandSequence,
                    "settlement:last-bearing:field-sleeve-service",
                    0,
                    builder.NextMaintenanceDueSettlementTick);
            }

            if (builder.FactionAidPolicy == FactionAidPolicy.EmergencyWaterQueued)
            {
                var before = builder.WaterMilli;
                builder.WaterMilli = Math.Min(
                    LastBearingBalanceV1.WaterCapacityMilli,
                    checked(
                        builder.WaterMilli + builder.EmergencyAidWaterMilli));
                builder.FactionAidPolicy =
                    FactionAidPolicy.EmergencyWaterDelivered;
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.EmergencyAidDelivered,
                    LastBearingEventCause.PlayerCommand,
                    builder.SettlementTick,
                    commandSequence,
                    "settlement:last-bearing:water",
                    before,
                    builder.WaterMilli);
            }
        }

        private static NextCityDecision DetermineNextCityDecision(
            LastBearingStateBuilder builder)
        {
            if (builder.PreparationChoice == PreparationChoice.WorkshopPush)
            {
                return builder.VehicleModule == VehicleModule.WinchAssembly
                    ? NextCityDecision.RefurbishAuxiliaryPump
                    : NextCityDecision.ExpandEmergencyCistern;
            }

            return builder.VehicleModule == VehicleModule.WinchAssembly
                ? NextCityDecision.MachineSpareBearing
                : NextCityDecision.RestoreDepotAccess;
        }

        private static void AdvanceSettlementClock(
            LastBearingStateBuilder builder,
            int scaleMilli,
            List<LastBearingDomainEvent> events)
        {
            builder.SettlementAccumulatorMilli = checked(
                builder.SettlementAccumulatorMilli + scaleMilli);
            if (builder.SettlementAccumulatorMilli
                < LastBearingBalanceV1.FullClockScaleMilli)
            {
                return;
            }

            builder.SettlementAccumulatorMilli -=
                LastBearingBalanceV1.FullClockScaleMilli;
            builder.SettlementTick = checked(builder.SettlementTick + 1);
            var previousWater = builder.WaterMilli;
            builder.WaterMilli = Math.Max(
                0,
                Math.Min(
                    LastBearingBalanceV1.WaterCapacityMilli,
                    checked(builder.WaterMilli + ComputeWaterTrend(builder))));
            if (previousWater != builder.WaterMilli)
            {
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.HomeWaterChanged,
                    LastBearingEventCause.AutonomousSettlementTick,
                    builder.SettlementTick,
                    LastBearingDomainEvent.AutonomousCommandSequence,
                    "settlement:last-bearing:water",
                    previousWater,
                    builder.WaterMilli);
            }

            if (builder.PreparationPhase == PreparationPhase.Preparing)
            {
                builder.PreparationElapsedTicks = checked(
                    builder.PreparationElapsedTicks + 1);
                if (builder.PreparationElapsedTicks
                    == builder.PreparationRequiredTicks)
                {
                    builder.PreparationPhase = PreparationPhase.Ready;
                    builder.ActiveWaterModifierMilliPerSettlementTick = 0;
                    builder.WorkshopServiceSlotsReserved = 0;
                    Emit(
                        builder,
                        events,
                        LastBearingEventKind.PreparationCompleted,
                        LastBearingEventCause.AutonomousSettlementTick,
                        builder.SettlementTick,
                        LastBearingDomainEvent.AutonomousCommandSequence,
                        "settlement:last-bearing:preparation",
                        builder.PreparationElapsedTicks - 1,
                        builder.PreparationElapsedTicks);
                    if (builder.ModuleInstallationState
                        == ModuleInstallationState.Pending)
                    {
                        CompleteModuleInstallation(
                            builder,
                            LastBearingDomainEvent.AutonomousCommandSequence,
                            events);
                    }

                    TryStartDeparture(
                        builder,
                        LastBearingDomainEvent.AutonomousCommandSequence,
                        events);
                }
            }

            if (builder.MaintenanceObligationActive
                && builder.SettlementTick
                    >= builder.NextMaintenanceDueSettlementTick)
            {
                builder.MaintenanceDue = true;
            }
        }

        private static void AdvanceFactionClock(
            LastBearingStateBuilder builder,
            int scaleMilli,
            List<LastBearingDomainEvent> events)
        {
            builder.FactionAccumulatorMilli = checked(
                builder.FactionAccumulatorMilli + scaleMilli);
            if (builder.FactionAccumulatorMilli
                < LastBearingBalanceV1.FullClockScaleMilli)
            {
                return;
            }

            builder.FactionAccumulatorMilli -=
                LastBearingBalanceV1.FullClockScaleMilli;
            builder.FactionTick = checked(builder.FactionTick + 1);
            AdvanceAutonomousClaim(builder, events);
            AdvanceFactionOutcome(builder, events);
        }

        private static void AdvanceAutonomousClaim(
            LastBearingStateBuilder builder,
            List<LastBearingDomainEvent> events)
        {
            if (builder.DepotResolution != EncounterChoice.Unresolved
                || builder.FactionClaimProgressMilli
                    >= LastBearingBalanceV1.FactionClaimThresholdMilli)
            {
                return;
            }

            var previous = builder.FactionClaimProgressMilli;
            builder.FactionClaimProgressMilli = Math.Min(
                LastBearingBalanceV1.FactionClaimThresholdMilli,
                checked(
                    builder.FactionClaimProgressMilli
                    + LastBearingBalanceV1.FactionClaimRateMilliPerFactionTick));
            Emit(
                builder,
                events,
                LastBearingEventKind.FactionClaimAdvanced,
                LastBearingEventCause.AutonomousFactionTick,
                builder.FactionTick,
                LastBearingDomainEvent.AutonomousCommandSequence,
                "world:last-bearing:depot-claim",
                previous,
                builder.FactionClaimProgressMilli);

            if (previous < LastBearingBalanceV1.FactionContestedThresholdMilli
                && builder.FactionClaimProgressMilli
                    >= LastBearingBalanceV1.FactionContestedThresholdMilli)
            {
                builder.FactionClaimState = FactionClaimState.Contested;
                builder.DepotControl = DepotControl.Contested;
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.FactionClaimContested,
                    LastBearingEventCause.AutonomousFactionTick,
                    builder.FactionTick,
                    LastBearingDomainEvent.AutonomousCommandSequence,
                    "world:last-bearing:depot",
                    (long)DepotControl.Unclaimed,
                    (long)DepotControl.Contested);
            }

            if (builder.FactionClaimProgressMilli
                == LastBearingBalanceV1.FactionClaimThresholdMilli)
            {
                var previousFee = builder.DepotAccessFeePartsUnits;
                builder.FactionClaimState = FactionClaimState.Claimed;
                builder.DepotControl = DepotControl.FactionClaimed;
                builder.DepotBearingDisposition =
                    DepotBearingDisposition.FactionHeld;
                builder.FactionAccessPolicy =
                    FactionAccessPolicy.PermitRequired;
                builder.DepotAccessFeePartsUnits =
                    LastBearingBalanceV1.ClaimedDepotAccessFeePartsUnits;
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.FactionDepotClaimed,
                    LastBearingEventCause.AutonomousFactionTick,
                    builder.FactionTick,
                    LastBearingDomainEvent.AutonomousCommandSequence,
                    "world:last-bearing:depot",
                    (long)DepotControl.Contested,
                    (long)DepotControl.FactionClaimed);
                Emit(
                    builder,
                    events,
                    LastBearingEventKind.DepotAccessTermsChanged,
                    LastBearingEventCause.AutonomousFactionTick,
                    builder.FactionTick,
                    LastBearingDomainEvent.AutonomousCommandSequence,
                    "settlement:last-bearing:depot-access-fee",
                    previousFee,
                    builder.DepotAccessFeePartsUnits);
            }
        }

        private static void AdvanceFactionOutcome(
            LastBearingStateBuilder builder,
            List<LastBearingDomainEvent> events)
        {
            if (builder.PendingFactionOutcome == FactionOutcomeKind.None)
            {
                return;
            }

            builder.FactionOutcomeElapsedTicks = checked(
                builder.FactionOutcomeElapsedTicks + 1);
            if (builder.FactionOutcomeElapsedTicks == 1)
            {
                var previous = builder.FactionAccessPolicy;
                if (builder.PendingFactionOutcome
                    == FactionOutcomeKind.Cooperative)
                {
                    builder.FactionAccessPolicy =
                        FactionAccessPolicy.SharedService;
                    builder.DepotAccessFeePartsUnits = 0;
                    builder.RoutePermitGranted = true;
                }
                else
                {
                    builder.FactionAccessPolicy = FactionAccessPolicy.Closed;
                }

                Emit(
                    builder,
                    events,
                    LastBearingEventKind.FactionBehaviorChanged,
                    LastBearingEventCause.AutonomousFactionTick,
                    builder.FactionTick,
                    LastBearingDomainEvent.AutonomousCommandSequence,
                    builder.FactionMemory == null
                        ? LastBearingState.LastBearingFactionId
                        : builder.FactionMemory.StableId,
                    (long)previous,
                    (long)builder.FactionAccessPolicy);
            }

            if (builder.FactionOutcomeElapsedTicks
                == LastBearingBalanceV1.FactionOutcomeMaturationTicks)
            {
                var previous = builder.FactionAidPolicy;
                if (builder.PendingFactionOutcome
                    == FactionOutcomeKind.Cooperative)
                {
                    builder.FactionAidPolicy =
                        FactionAidPolicy.EmergencyWaterQueued;
                    builder.EmergencyAidWaterMilli =
                        LastBearingBalanceV1.CooperateAidWaterMilli;
                }
                else
                {
                    builder.FactionAidPolicy = FactionAidPolicy.Withheld;
                    builder.FutureRouteTollFuelUnits =
                        LastBearingBalanceV1.TakeFutureRouteTollFuelUnits;
                }

                Emit(
                    builder,
                    events,
                    LastBearingEventKind.FactionBehaviorChanged,
                    LastBearingEventCause.AutonomousFactionTick,
                    builder.FactionTick,
                    LastBearingDomainEvent.AutonomousCommandSequence,
                    builder.FactionMemory == null
                        ? LastBearingState.LastBearingFactionId
                        : builder.FactionMemory.StableId,
                    (long)previous,
                    (long)builder.FactionAidPolicy);
            }
        }

        private static void AdvanceCrisisClock(
            LastBearingStateBuilder builder,
            int scaleMilli)
        {
            builder.CrisisAccumulatorMilli = checked(
                builder.CrisisAccumulatorMilli + scaleMilli);
            if (builder.CrisisAccumulatorMilli
                < LastBearingBalanceV1.FullClockScaleMilli)
            {
                return;
            }

            builder.CrisisAccumulatorMilli -=
                LastBearingBalanceV1.FullClockScaleMilli;
            builder.CrisisTick = checked(builder.CrisisTick + 1);
            builder.DustFrontProgressTicks = checked(
                builder.DustFrontProgressTicks + 1);
        }

        private static void AdvanceRoadClock(
            LastBearingStateBuilder builder,
            int scaleMilli)
        {
            builder.RoadAccumulatorMilli = checked(
                builder.RoadAccumulatorMilli + scaleMilli);
            if (builder.RoadAccumulatorMilli
                < LastBearingBalanceV1.FullClockScaleMilli)
            {
                return;
            }

            builder.RoadAccumulatorMilli -=
                LastBearingBalanceV1.FullClockScaleMilli;
            builder.RoadTick = checked(builder.RoadTick + 1);
        }

        private static long ProjectWaterAtReturn(LastBearingStateBuilder builder)
        {
            var roadSettlementTicks = checked(
                (builder.RouteTargetTicks
                    * LastBearingBalanceV1.ExpeditionHomeClockScaleMilli)
                / LastBearingBalanceV1.FullClockScaleMilli);
            var projected = checked(
                builder.WaterMilli + (ComputeWaterTrend(builder) * roadSettlementTicks));
            return Math.Max(0, projected);
        }

        private static long ComputeWaterTrend(LastBearingStateBuilder builder)
        {
            long baseRate;
            switch (builder.TurbineCondition)
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
                + builder.ActiveWaterModifierMilliPerSettlementTick
                + LastBearingBalanceV1.CityImprovementWaterModifier(
                    builder.InstalledCityImprovement));
        }

        private static void EnsureTransactionIdentity(
            LastBearingStateBuilder builder,
            string transactionId,
            string fingerprint)
        {
            if (!string.Equals(
                    builder.TransactionId,
                    transactionId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    builder.TransactionFingerprint,
                    fingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_TRANSACTION_FINGERPRINT_MISMATCH");
            }
        }

        private static bool IsAwayFromSettlement(ExpeditionPhase phase)
        {
            return phase == ExpeditionPhase.Outbound
                || phase == ExpeditionPhase.AtDepot
                || phase == ExpeditionPhase.Returning;
        }

        private static void EmitReplay(
            LastBearingStateBuilder builder,
            long commandSequence,
            List<LastBearingDomainEvent> events)
        {
            Emit(
                builder,
                events,
                LastBearingEventKind.IdempotentReplayAccepted,
                LastBearingEventCause.PlayerCommand,
                builder.GlobalTick,
                commandSequence,
                "audit:last-bearing:idempotent-replay",
                commandSequence,
                commandSequence);
        }

        private static void Emit(
            LastBearingStateBuilder builder,
            List<LastBearingDomainEvent> events,
            LastBearingEventKind kind,
            LastBearingEventCause cause,
            long domainTick,
            long commandSequence,
            string subjectId,
            long beforeValue,
            long afterValue)
        {
            events.Add(
                new LastBearingDomainEvent(
                    kind,
                    cause,
                    builder.GlobalTick,
                    domainTick,
                    commandSequence,
                    subjectId,
                    beforeValue,
                    afterValue));
        }
    }
}
