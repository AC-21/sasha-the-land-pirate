#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AtomicLandPirate.Simulation.LastBearing
{
    public sealed class LastBearingDecodeResult
    {
        internal LastBearingDecodeResult(
            bool succeeded,
            string code,
            LastBearingState? state)
        {
            Succeeded = succeeded;
            Code = code ?? throw new ArgumentNullException(nameof(code));
            State = state;
        }

        public bool Succeeded { get; }

        public string Code { get; }

        public LastBearingState? State { get; }
    }

    public static class LastBearingCanonicalCodec
    {
        public const string DecodeOkCode = "LB_CORE_DECODE_OK";
        public const string DecodeInvalidCode = "LB_CORE_DECODE_INVALID";
        public const string DecodeUnknownVersionCode =
            "LB_CORE_DECODE_UNKNOWN_VERSION";

        private const ushort CodecVersion = 7;
        private const ushort LegacyCodecVersionV6 = 6;
        private const ushort LegacyCodecVersionV5 = 5;
        private const ushort LegacyCodecVersionV4 = 4;
        private const ushort LegacyCodecVersionV3 = 3;
        private const int MaximumCanonicalBytes = 1_048_576;
        private static readonly byte[] Magic =
            Encoding.ASCII.GetBytes("ALPLBC01");

        public static byte[] Encode(LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return EncodeVersion(
                state,
                CodecVersion,
                LastBearingState.CurrentSchemaVersion,
                LastBearingBalanceV1.Revision,
                includeCityConstruction: true,
                includeRigUpgrade: true,
                includeFrameRailSalvage: true,
                includeHotShift: true,
                includeRepresentation: true,
                includeDustFrontVerdict: true,
                includeEmergencyCisternCharged: true);
        }

        public static LastBearingDecodeResult TryDecode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length == 0 || bytes.Length > MaximumCanonicalBytes)
            {
                return Failure(DecodeInvalidCode);
            }

            try
            {
                var reader = new CanonicalReader(bytes);
                reader.RequireBytes(Magic);
                var version = reader.ReadUInt16();
                if (version != CodecVersion
                    && version != LegacyCodecVersionV6
                    && version != LegacyCodecVersionV5
                    && version != LegacyCodecVersionV4
                    && version != LegacyCodecVersionV3)
                {
                    return Failure(DecodeUnknownVersionCode);
                }

                var builder = new LastBearingStateBuilder
                {
                    SchemaVersion = reader.ReadInt32(),
                    BalanceRevision = reader.ReadString(),
                    WorldSeed = reader.ReadInt32(),
                    GlobalTick = reader.ReadInt64(),
                    SettlementTick = reader.ReadInt64(),
                    FactionTick = reader.ReadInt64(),
                    CrisisTick = reader.ReadInt64(),
                    RoadTick = reader.ReadInt64(),
                    SettlementAccumulatorMilli = reader.ReadInt32(),
                    FactionAccumulatorMilli = reader.ReadInt32(),
                    CrisisAccumulatorMilli = reader.ReadInt32(),
                    RoadAccumulatorMilli = reader.ReadInt32(),
                    NextCommandSequence = reader.ReadInt64(),
                    ProtagonistId = reader.ReadString(),
                };
                int sourceSchemaVersion = builder.SchemaVersion;
                if (version == CodecVersion
                    && (sourceSchemaVersion != 7
                        && sourceSchemaVersion != 8
                        && sourceSchemaVersion
                            != LastBearingState.CurrentSchemaVersion
                        || !string.Equals(
                            builder.BalanceRevision,
                            LastBearingBalanceV1.Revision,
                            StringComparison.Ordinal)))
                {
                    return Failure(DecodeInvalidCode);
                }

                var residentCount = reader.ReadByte();
                if (residentCount < 1 || residentCount > 2)
                {
                    return Failure(DecodeInvalidCode);
                }

                var residents = new ResidentRecord[residentCount];
                for (var index = 0; index < residents.Length; index++)
                {
                    residents[index] = new ResidentRecord(
                        reader.ReadString(),
                        reader.ReadEnum<ResidentKind>());
                }

                builder.Roster = new ResidentRoster(residents);
                builder.AssignedResidentId = reader.ReadNullableString();
                builder.PauseCause = reader.ReadEnum<PauseCause>();
                builder.SliceInfrastructureActive = reader.ReadBoolean();
                if (version >= LegacyCodecVersionV4)
                {
                    builder.RecyclerPadIndex = reader.ReadInt32();
                    builder.RecyclerQuarterTurns = reader.ReadInt32();
                    builder.MachineShopPadIndex = reader.ReadInt32();
                    builder.MachineShopQuarterTurns = reader.ReadInt32();
                    builder.EmergencyStoragePadIndex = reader.ReadInt32();
                    builder.EmergencyStorageQuarterTurns = reader.ReadInt32();
                    builder.CityServiceLinkConnected = reader.ReadBoolean();
                    builder.CityServiceResidentId =
                        reader.ReadNullableString();
                    builder.CityDeliveryStage =
                        reader.ReadEnum<CityDeliveryStage>();
                    builder.CityDeliveryCount = reader.ReadInt32();
                }

                builder.WaterMilli = reader.ReadInt64();
                builder.PartsUnits = reader.ReadInt64();
                builder.FuelUnits = reader.ReadInt64();
                builder.TurbineCondition = reader.ReadEnum<TurbineCondition>();
                builder.PreparationChoice = reader.ReadEnum<PreparationChoice>();
                builder.PreparationPhase = reader.ReadEnum<PreparationPhase>();
                builder.PlannedModule = reader.ReadEnum<VehicleModule>();
                builder.PreparationElapsedTicks = reader.ReadInt64();
                builder.PreparationRequiredTicks = reader.ReadInt64();
                builder.PreparationFuelDebitedUnits = reader.ReadInt64();
                builder.WorkshopServiceSlotsReserved = reader.ReadInt32();
                builder.ActiveWaterModifierMilliPerSettlementTick =
                    reader.ReadInt64();
                builder.NextCityDecision = reader.ReadEnum<NextCityDecision>();
                builder.InstalledCityImprovement =
                    reader.ReadEnum<CityImprovementKind>();
                builder.SpareBearingRecipe =
                    reader.ReadEnum<SpareBearingRecipe>();
                builder.SpareBearingBatchPhase =
                    reader.ReadEnum<SpareBearingBatchPhase>();
                builder.SpareBearingElapsedTicks = reader.ReadInt64();
                builder.SpareBearingRequiredTicks = reader.ReadInt64();
                builder.SpareBearingLotQuantity = reader.ReadInt64();
                builder.SpareBearingLotCustody =
                    reader.ReadEnum<SpareBearingLotCustody>();

                builder.VehicleModule = reader.ReadEnum<VehicleModule>();
                builder.ModuleInstallationState =
                    reader.ReadEnum<ModuleInstallationState>();
                builder.RouteKind = reader.ReadEnum<RouteKind>();
                builder.RouteActionKind = reader.ReadEnum<RouteActionKind>();
                builder.RouteActionUsed = reader.ReadBoolean();
                builder.ExpeditionPhase = reader.ReadEnum<ExpeditionPhase>();
                builder.RouteProgressTicks = reader.ReadInt64();
                builder.RouteTargetTicks = reader.ReadInt64();
                builder.RouteMovementAccumulatorMilli = reader.ReadInt32();
                builder.VehicleLateralMilli = reader.ReadInt32();
                builder.VehicleConditionMilli = reader.ReadInt64();
                builder.ExpeditionFuelManifestUnits = reader.ReadInt64();
                builder.OrdinaryCargoCapacityUnits = reader.ReadInt64();
                builder.OrdinaryCargoUsedUnits = reader.ReadInt64();
                builder.TowSlots = reader.ReadInt32();
                builder.TowSlotsUsed = reader.ReadInt32();
                builder.LiquidCapacityMilli = reader.ReadInt64();
                builder.HeavyCargoKind = reader.ReadEnum<HeavyCargoKind>();
                builder.HeavyCargoCustody =
                    reader.ReadEnum<HeavyCargoCustody>();
                builder.LiquidCargoKind = reader.ReadEnum<LiquidCargoKind>();
                builder.LiquidCargoQuantityMilli = reader.ReadInt64();
                builder.LiquidCargoCustody =
                    reader.ReadEnum<LiquidCargoCustody>();
                builder.RepairCargoKind = reader.ReadEnum<RepairCargoKind>();
                builder.RepairCargoCustody =
                    reader.ReadEnum<RepairCargoCustody>();
                builder.DepotBearingDisposition =
                    reader.ReadEnum<DepotBearingDisposition>();
                builder.ReturnPayloadFrozen = reader.ReadBoolean();
                builder.HasArrivalClaimSnapshot = reader.ReadBoolean();
                builder.ArrivalFactionClaimProgressMilli = reader.ReadInt64();
                builder.ArrivalFactionClaimState =
                    reader.ReadEnum<FactionClaimState>();

                builder.TransactionId = reader.ReadNullableString();
                builder.TransactionFingerprint = reader.ReadNullableString();
                builder.TransactionPhase = reader.ReadEnum<TransactionPhase>();
                builder.DepotResolution = reader.ReadEnum<EncounterChoice>();

                builder.FactionClaimProgressMilli = reader.ReadInt64();
                builder.FactionClaimState = reader.ReadEnum<FactionClaimState>();
                builder.DepotControl = reader.ReadEnum<DepotControl>();
                builder.FactionAccessPolicy =
                    reader.ReadEnum<FactionAccessPolicy>();
                builder.FactionAidPolicy =
                    reader.ReadEnum<FactionAidPolicy>();
                builder.DepotAccessFeePartsUnits = reader.ReadInt64();
                builder.FutureRouteTollFuelUnits = reader.ReadInt64();
                builder.EmergencyAidWaterMilli = reader.ReadInt64();
                builder.FactionMemory = ReadMemory(reader);
                builder.FactionTrust = reader.ReadInt64();
                builder.FactionGrievance = reader.ReadInt64();
                builder.PendingFactionOutcome =
                    reader.ReadEnum<FactionOutcomeKind>();
                builder.FactionOutcomeElapsedTicks = reader.ReadInt64();
                builder.RoutePermitGranted = reader.ReadBoolean();
                builder.MaintenanceRecipe = reader.ReadEnum<MaintenanceRecipe>();
                builder.MaintenanceObligationActive = reader.ReadBoolean();
                builder.MaintenancePartsUnits = reader.ReadInt64();
                builder.NextMaintenanceDueSettlementTick = reader.ReadInt64();
                builder.MaintenanceDue = reader.ReadBoolean();
                builder.DustFrontProgressTicks = reader.ReadInt64();
                long sourceDustFrontProgressTicks =
                    builder.DustFrontProgressTicks;
                if (version >= LegacyCodecVersionV5)
                {
                    builder.RigUpgrade = reader.ReadEnum<RigUpgrade>();
                }

                if (version >= LegacyCodecVersionV6)
                {
                    builder.FrameRailSalvageCustody =
                        reader.ReadEnum<FrameRailSalvageCustody>();
                }

                if (version == CodecVersion)
                {
                    builder.HotShiftPhase =
                        reader.ReadEnum<HotShiftPhase>();
                    builder.HotShiftElapsedTicks = reader.ReadInt64();
                    builder.HotShiftRequiredTicks = reader.ReadInt64();
                    builder.HotShiftFuelCommittedUnits = reader.ReadInt64();
                    builder.HotShiftCompletedCount = reader.ReadInt64();
                }

                if (version == CodecVersion
                    && sourceSchemaVersion >= 8)
                {
                    builder.DustFrontOutcome =
                        reader.ReadEnum<DustFrontOutcome>();
                    builder.IsDustFrontAcknowledgementRequired =
                        reader.ReadBoolean();
                }

                if (version == CodecVersion
                    && sourceSchemaVersion
                        == LastBearingState.CurrentSchemaVersion)
                {
                    builder.EmergencyCisternCharged =
                        reader.ReadBoolean();
                }

                reader.RequireEnd();

                if (version == CodecVersion && sourceSchemaVersion == 7)
                {
                    MigrateLegacyV7(builder);
                }
                else if (version == CodecVersion
                    && sourceSchemaVersion == 8)
                {
                    MigrateLegacyV8(builder);
                }
                else if (version == LegacyCodecVersionV3)
                {
                    if (builder.SchemaVersion != 3
                        || !string.Equals(
                            builder.BalanceRevision,
                            LastBearingBalanceV1.LegacyRevisionV1,
                            StringComparison.Ordinal))
                    {
                        return Failure(DecodeInvalidCode);
                    }

                    MigrateLegacyV3(builder);
                }
                else if (version == LegacyCodecVersionV4)
                {
                    if (builder.SchemaVersion != 4
                        || !string.Equals(
                            builder.BalanceRevision,
                            LastBearingBalanceV1.LegacyRevisionV2,
                            StringComparison.Ordinal))
                    {
                        return Failure(DecodeInvalidCode);
                    }

                    MigrateLegacyV4(builder);
                }
                else if (version == LegacyCodecVersionV5)
                {
                    if (builder.SchemaVersion != 5
                        || !string.Equals(
                            builder.BalanceRevision,
                            LastBearingBalanceV1.LegacyRevisionV3,
                            StringComparison.Ordinal))
                    {
                        return Failure(DecodeInvalidCode);
                    }

                    MigrateLegacyV5(builder);
                }
                else if (version == LegacyCodecVersionV6)
                {
                    if (builder.SchemaVersion != 6
                        || !string.Equals(
                            builder.BalanceRevision,
                            LastBearingBalanceV1.LegacyRevisionV3,
                            StringComparison.Ordinal))
                    {
                        return Failure(DecodeInvalidCode);
                    }

                    MigrateLegacyV6(builder);
                }

                var state = builder.Build();
                byte[] canonical;
                if (version == CodecVersion
                    && sourceSchemaVersion
                        == LastBearingState.CurrentSchemaVersion)
                {
                    canonical = Encode(state);
                }
                else if (version == CodecVersion
                    && sourceSchemaVersion == 8)
                {
                    canonical = EncodeVersion(
                        state,
                        CodecVersion,
                        8,
                        LastBearingBalanceV1.Revision,
                        includeCityConstruction: true,
                        includeRigUpgrade: true,
                        includeFrameRailSalvage: true,
                        includeHotShift: true,
                        includeRepresentation: true,
                        includeDustFrontVerdict: true,
                        dustFrontProgressTicksOverride:
                            sourceDustFrontProgressTicks);
                }
                else if (version == CodecVersion)
                {
                    canonical = EncodeVersion(
                        state,
                        CodecVersion,
                        7,
                        LastBearingBalanceV1.Revision,
                        includeCityConstruction: true,
                        includeRigUpgrade: true,
                        includeFrameRailSalvage: true,
                        includeHotShift: true,
                        includeRepresentation: true,
                        dustFrontProgressTicksOverride:
                            sourceDustFrontProgressTicks);
                }
                else if (version == LegacyCodecVersionV6)
                {
                    canonical = EncodeVersion(
                        state,
                        LegacyCodecVersionV6,
                        6,
                        LastBearingBalanceV1.LegacyRevisionV3,
                        includeCityConstruction: true,
                        includeRigUpgrade: true,
                        includeFrameRailSalvage: true,
                        includeHotShift: false,
                        includeRepresentation: true,
                        dustFrontProgressTicksOverride:
                            sourceDustFrontProgressTicks);
                }
                else if (version == LegacyCodecVersionV5)
                {
                    canonical = EncodeVersion(
                        state,
                        LegacyCodecVersionV5,
                        5,
                        LastBearingBalanceV1.LegacyRevisionV3,
                        includeCityConstruction: true,
                        includeRigUpgrade: true,
                        includeFrameRailSalvage: false,
                        includeHotShift: false,
                        includeRepresentation: true,
                        dustFrontProgressTicksOverride:
                            sourceDustFrontProgressTicks);
                }
                else if (version == LegacyCodecVersionV4)
                {
                    canonical = EncodeVersion(
                        state,
                        LegacyCodecVersionV4,
                        4,
                        LastBearingBalanceV1.LegacyRevisionV2,
                        includeCityConstruction: true,
                        includeRigUpgrade: false,
                        includeFrameRailSalvage: false,
                        includeHotShift: false,
                        includeRepresentation: true,
                        dustFrontProgressTicksOverride:
                            sourceDustFrontProgressTicks);
                }
                else
                {
                    canonical = EncodeVersion(
                        state,
                        LegacyCodecVersionV3,
                        3,
                        LastBearingBalanceV1.LegacyRevisionV1,
                        includeCityConstruction: false,
                        includeRigUpgrade: false,
                        includeFrameRailSalvage: false,
                        includeHotShift: false,
                        includeRepresentation: true,
                        dustFrontProgressTicksOverride:
                            sourceDustFrontProgressTicks);
                }

                if (!BytesEqual(bytes, canonical))
                {
                    return Failure(DecodeInvalidCode);
                }

                return new LastBearingDecodeResult(
                    true,
                    DecodeOkCode,
                    state);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is OverflowException
                || exception is DecoderFallbackException)
            {
                return Failure(DecodeInvalidCode);
            }
        }

        public static string ComputeSha256(LastBearingState state)
        {
            return ComputeDigestHex(Encode(state));
        }

        internal static byte[] EncodeMechanicalProjection(
            LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return EncodeVersion(
                state,
                CodecVersion,
                LastBearingState.CurrentSchemaVersion,
                LastBearingBalanceV1.Revision,
                includeCityConstruction: true,
                includeRigUpgrade: true,
                includeFrameRailSalvage: true,
                includeHotShift: true,
                includeRepresentation: false,
                includeDustFrontVerdict: true,
                includeEmergencyCisternCharged: true);
        }

        internal static byte[] EncodeLegacyV8ForMigrationTests(
            LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return EncodeVersion(
                state,
                CodecVersion,
                8,
                LastBearingBalanceV1.Revision,
                includeCityConstruction: true,
                includeRigUpgrade: true,
                includeFrameRailSalvage: true,
                includeHotShift: true,
                includeRepresentation: true,
                includeDustFrontVerdict: true);
        }

        internal static byte[] EncodeLegacyV7ForMigrationTests(
            LastBearingState state,
            long? legacyDustFrontProgressTicks = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            if (legacyDustFrontProgressTicks < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(legacyDustFrontProgressTicks));
            }

            return EncodeVersion(
                state,
                CodecVersion,
                7,
                LastBearingBalanceV1.Revision,
                includeCityConstruction: true,
                includeRigUpgrade: true,
                includeFrameRailSalvage: true,
                includeHotShift: true,
                includeRepresentation: true,
                dustFrontProgressTicksOverride:
                    legacyDustFrontProgressTicks);
        }

        internal static byte[] EncodeLegacyV6ForMigrationTests(
            LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return EncodeVersion(
                state,
                LegacyCodecVersionV6,
                6,
                LastBearingBalanceV1.LegacyRevisionV3,
                includeCityConstruction: true,
                includeRigUpgrade: true,
                includeFrameRailSalvage: true,
                includeHotShift: false,
                includeRepresentation: true);
        }

        internal static byte[] EncodeLegacyV5ForMigrationTests(
            LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return EncodeVersion(
                state,
                LegacyCodecVersionV5,
                5,
                LastBearingBalanceV1.LegacyRevisionV3,
                includeCityConstruction: true,
                includeRigUpgrade: true,
                includeFrameRailSalvage: false,
                includeHotShift: false,
                includeRepresentation: true);
        }

        internal static byte[] EncodeLegacyV4ForMigrationTests(
            LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return EncodeVersion(
                state,
                LegacyCodecVersionV4,
                4,
                LastBearingBalanceV1.LegacyRevisionV2,
                includeCityConstruction: true,
                includeRigUpgrade: false,
                includeFrameRailSalvage: false,
                includeHotShift: false,
                includeRepresentation: true);
        }

        internal static byte[] EncodeLegacyV3ForMigrationTests(
            LastBearingState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            LastBearingInvariants.Validate(state);
            return EncodeVersion(
                state,
                LegacyCodecVersionV3,
                3,
                LastBearingBalanceV1.LegacyRevisionV1,
                includeCityConstruction: false,
                includeRigUpgrade: false,
                includeFrameRailSalvage: false,
                includeHotShift: false,
                includeRepresentation: true);
        }

        internal static string ComputeMechanicalSha256(
            LastBearingState state)
        {
            return ComputeDigestHex(EncodeMechanicalProjection(state));
        }

        private static byte[] EncodeInternal(
            LastBearingState state,
            bool includeRepresentation)
        {
            return EncodeVersion(
                state,
                CodecVersion,
                LastBearingState.CurrentSchemaVersion,
                LastBearingBalanceV1.Revision,
                includeCityConstruction: true,
                includeRigUpgrade: true,
                includeFrameRailSalvage: true,
                includeHotShift: true,
                includeRepresentation: includeRepresentation,
                includeDustFrontVerdict: true,
                includeEmergencyCisternCharged: true);
        }

        private static byte[] EncodeVersion(
            LastBearingState state,
            ushort codecVersion,
            int schemaVersion,
            string balanceRevision,
            bool includeCityConstruction,
            bool includeRigUpgrade,
            bool includeFrameRailSalvage,
            bool includeHotShift,
            bool includeRepresentation,
            bool includeDustFrontVerdict = false,
            bool includeEmergencyCisternCharged = false,
            long? dustFrontProgressTicksOverride = null)
        {
            var writer = new CanonicalWriter();
            writer.WriteBytes(Magic);
            writer.WriteUInt16(codecVersion);
            writer.WriteInt32(schemaVersion);
            writer.WriteString(balanceRevision);
            writer.WriteInt32(state.WorldSeed);
            writer.WriteInt64(state.GlobalTick);
            writer.WriteInt64(state.SettlementTick);
            writer.WriteInt64(state.FactionTick);
            writer.WriteInt64(state.CrisisTick);
            writer.WriteInt64(state.RoadTick);
            writer.WriteInt32(state.SettlementAccumulatorMilli);
            writer.WriteInt32(state.FactionAccumulatorMilli);
            writer.WriteInt32(state.CrisisAccumulatorMilli);
            writer.WriteInt32(state.RoadAccumulatorMilli);
            writer.WriteInt64(state.NextCommandSequence);
            writer.WriteString(state.ProtagonistId);

            if (includeRepresentation)
            {
                writer.WriteByte(checked((byte)state.Roster.Residents.Count));
                foreach (var resident in state.Roster.Residents)
                {
                    writer.WriteString(resident.StableId);
                    writer.WriteEnum(resident.Kind);
                }

                writer.WriteNullableString(state.AssignedResidentId);
            }
            else
            {
                writer.WriteByte(0);
                writer.WriteNullableString(null);
            }

            writer.WriteEnum(state.PauseCause);
            writer.WriteBoolean(state.SliceInfrastructureActive);
            if (includeCityConstruction)
            {
                writer.WriteInt32(state.RecyclerPadIndex);
                writer.WriteInt32(state.RecyclerQuarterTurns);
                writer.WriteInt32(state.MachineShopPadIndex);
                writer.WriteInt32(state.MachineShopQuarterTurns);
                writer.WriteInt32(state.EmergencyStoragePadIndex);
                writer.WriteInt32(state.EmergencyStorageQuarterTurns);
                writer.WriteBoolean(state.CityServiceLinkConnected);
                writer.WriteNullableString(
                    includeRepresentation
                        ? state.CityServiceResidentId
                        : null);
                writer.WriteEnum(state.CityDeliveryStage);
                writer.WriteInt32(state.CityDeliveryCount);
            }

            writer.WriteInt64(state.WaterMilli);
            writer.WriteInt64(state.PartsUnits);
            writer.WriteInt64(state.FuelUnits);
            writer.WriteEnum(state.TurbineCondition);
            writer.WriteEnum(state.PreparationChoice);
            writer.WriteEnum(state.PreparationPhase);
            writer.WriteEnum(state.PlannedModule);
            writer.WriteInt64(state.PreparationElapsedTicks);
            writer.WriteInt64(state.PreparationRequiredTicks);
            writer.WriteInt64(state.PreparationFuelDebitedUnits);
            writer.WriteInt32(state.WorkshopServiceSlotsReserved);
            writer.WriteInt64(state.ActiveWaterModifierMilliPerSettlementTick);
            writer.WriteEnum(state.NextCityDecision);
            writer.WriteEnum(state.InstalledCityImprovement);
            writer.WriteEnum(state.SpareBearingRecipe);
            writer.WriteEnum(state.SpareBearingBatchPhase);
            writer.WriteInt64(state.SpareBearingElapsedTicks);
            writer.WriteInt64(state.SpareBearingRequiredTicks);
            writer.WriteInt64(state.SpareBearingLotQuantity);
            writer.WriteEnum(state.SpareBearingLotCustody);

            writer.WriteEnum(state.VehicleModule);
            writer.WriteEnum(state.ModuleInstallationState);
            writer.WriteEnum(state.RouteKind);
            writer.WriteEnum(state.RouteActionKind);
            writer.WriteBoolean(state.RouteActionUsed);
            writer.WriteEnum(state.ExpeditionPhase);
            writer.WriteInt64(state.RouteProgressTicks);
            writer.WriteInt64(state.RouteTargetTicks);
            writer.WriteInt32(state.RouteMovementAccumulatorMilli);
            writer.WriteInt32(state.VehicleLateralMilli);
            writer.WriteInt64(state.VehicleConditionMilli);
            writer.WriteInt64(state.ExpeditionFuelManifestUnits);
            writer.WriteInt64(state.OrdinaryCargoCapacityUnits);
            writer.WriteInt64(state.OrdinaryCargoUsedUnits);
            writer.WriteInt32(state.TowSlots);
            writer.WriteInt32(state.TowSlotsUsed);
            writer.WriteInt64(state.LiquidCapacityMilli);
            writer.WriteEnum(state.HeavyCargoKind);
            writer.WriteEnum(state.HeavyCargoCustody);
            writer.WriteEnum(state.LiquidCargoKind);
            writer.WriteInt64(state.LiquidCargoQuantityMilli);
            writer.WriteEnum(state.LiquidCargoCustody);
            writer.WriteEnum(state.RepairCargoKind);
            writer.WriteEnum(state.RepairCargoCustody);
            writer.WriteEnum(state.DepotBearingDisposition);
            writer.WriteBoolean(state.ReturnPayloadFrozen);
            writer.WriteBoolean(state.HasArrivalClaimSnapshot);
            writer.WriteInt64(state.ArrivalFactionClaimProgressMilli);
            writer.WriteEnum(state.ArrivalFactionClaimState);

            writer.WriteNullableString(state.TransactionId);
            writer.WriteNullableString(state.TransactionFingerprint);
            writer.WriteEnum(state.TransactionPhase);
            writer.WriteEnum(state.DepotResolution);

            writer.WriteInt64(state.FactionClaimProgressMilli);
            writer.WriteEnum(state.FactionClaimState);
            writer.WriteEnum(state.DepotControl);
            writer.WriteEnum(state.FactionAccessPolicy);
            writer.WriteEnum(state.FactionAidPolicy);
            writer.WriteInt64(state.DepotAccessFeePartsUnits);
            writer.WriteInt64(state.FutureRouteTollFuelUnits);
            writer.WriteInt64(state.EmergencyAidWaterMilli);
            WriteMemory(writer, state.FactionMemory);
            writer.WriteInt64(state.FactionTrust);
            writer.WriteInt64(state.FactionGrievance);
            writer.WriteEnum(state.PendingFactionOutcome);
            writer.WriteInt64(state.FactionOutcomeElapsedTicks);
            writer.WriteBoolean(state.RoutePermitGranted);
            writer.WriteEnum(state.MaintenanceRecipe);
            writer.WriteBoolean(state.MaintenanceObligationActive);
            writer.WriteInt64(state.MaintenancePartsUnits);
            writer.WriteInt64(state.NextMaintenanceDueSettlementTick);
            writer.WriteBoolean(state.MaintenanceDue);
            writer.WriteInt64(
                dustFrontProgressTicksOverride
                    ?? state.DustFrontProgressTicks);
            if (includeRigUpgrade)
            {
                writer.WriteEnum(state.RigUpgrade);
            }

            if (includeFrameRailSalvage)
            {
                writer.WriteEnum(state.FrameRailSalvageCustody);
            }

            if (includeHotShift)
            {
                writer.WriteEnum(state.HotShiftPhase);
                writer.WriteInt64(state.HotShiftElapsedTicks);
                writer.WriteInt64(state.HotShiftRequiredTicks);
                writer.WriteInt64(state.HotShiftFuelCommittedUnits);
                writer.WriteInt64(state.HotShiftCompletedCount);
            }

            if (includeDustFrontVerdict)
            {
                writer.WriteEnum(state.DustFrontOutcome);
                writer.WriteBoolean(
                    state.IsDustFrontAcknowledgementRequired);
            }

            if (includeEmergencyCisternCharged)
            {
                writer.WriteBoolean(state.EmergencyCisternCharged);
            }

            return writer.ToArray();
        }

        private static void MigrateLegacyV3(LastBearingStateBuilder builder)
        {
            if (builder.SliceInfrastructureActive)
            {
                builder.RecyclerPadIndex = 0;
                builder.RecyclerQuarterTurns = 0;
                builder.MachineShopPadIndex = 1;
                builder.MachineShopQuarterTurns = 0;
                builder.EmergencyStoragePadIndex = 2;
                builder.EmergencyStorageQuarterTurns = 0;
                builder.CityServiceLinkConnected = true;
                builder.CityServiceResidentId = builder.AssignedResidentId
                    ?? builder.Roster.Residents[0].StableId;
                builder.CityDeliveryStage =
                    CityDeliveryStage.DeliveredToWorkshop;
                builder.CityDeliveryCount = 1;
            }
            else
            {
                builder.RecyclerPadIndex =
                    LastBearingState.UnplacedCityPadIndex;
                builder.RecyclerQuarterTurns = 0;
                builder.MachineShopPadIndex =
                    LastBearingState.UnplacedCityPadIndex;
                builder.MachineShopQuarterTurns = 0;
                builder.EmergencyStoragePadIndex =
                    LastBearingState.UnplacedCityPadIndex;
                builder.EmergencyStorageQuarterTurns = 0;
                builder.CityServiceLinkConnected = false;
                builder.CityServiceResidentId = null;
                builder.CityDeliveryStage = CityDeliveryStage.AtRecycler;
                builder.CityDeliveryCount = 0;
            }

            MigrateLegacyV4(builder);
        }

        private static void MigrateLegacyV4(LastBearingStateBuilder builder)
        {
            builder.RigUpgrade = RigUpgrade.None;
            MigrateLegacyV5(builder);
        }

        private static void MigrateLegacyV5(LastBearingStateBuilder builder)
        {
            builder.SchemaVersion = 6;
            builder.BalanceRevision = LastBearingBalanceV1.LegacyRevisionV3;
            builder.FrameRailSalvageCustody =
                InferLegacyFrameRailSalvageCustody(builder);
            MigrateLegacyV6(builder);
        }

        private static void MigrateLegacyV6(LastBearingStateBuilder builder)
        {
            builder.SchemaVersion = 7;
            builder.BalanceRevision = LastBearingBalanceV1.Revision;
            builder.HotShiftPhase = HotShiftPhase.Idle;
            builder.HotShiftElapsedTicks = 0;
            builder.HotShiftRequiredTicks = 0;
            builder.HotShiftFuelCommittedUnits = 0;
            builder.HotShiftCompletedCount = 0;
            MigrateLegacyV7(builder);
        }

        private static void MigrateLegacyV7(LastBearingStateBuilder builder)
        {
            builder.SchemaVersion = 8;
            if (builder.DustFrontProgressTicks
                >= LastBearingBalanceV1.DustFrontThresholdCrisisTicks)
            {
                builder.DustFrontProgressTicks =
                    LastBearingBalanceV1.DustFrontThresholdCrisisTicks;
                builder.DustFrontOutcome = DustFrontOutcome.Held;
            }
            else
            {
                builder.DustFrontOutcome = DustFrontOutcome.Unresolved;
            }

            builder.IsDustFrontAcknowledgementRequired = false;
            MigrateLegacyV8(builder);
        }

        private static void MigrateLegacyV8(LastBearingStateBuilder builder)
        {
            builder.SchemaVersion = LastBearingState.CurrentSchemaVersion;
            builder.BalanceRevision = LastBearingBalanceV1.Revision;
            builder.EmergencyCisternCharged = false;
        }

        private static FrameRailSalvageCustody
            InferLegacyFrameRailSalvageCustody(
                LastBearingStateBuilder builder)
        {
            if (builder.RigUpgrade != RigUpgrade.PatchworkSkidPlate)
            {
                return FrameRailSalvageCustody.None;
            }

            if (builder.ExpeditionPhase == ExpeditionPhase.AtHome)
            {
                return builder.TransactionPhase == TransactionPhase.Finalized
                    ? FrameRailSalvageCustody.None
                    : FrameRailSalvageCustody.WreckLine;
            }

            if (builder.ExpeditionPhase == ExpeditionPhase.Outbound
                && builder.VehicleModule != VehicleModule.None
                && builder.RouteProgressTicks
                    <= LastBearingBalanceV1.WreckLineGateTicks(
                        builder.VehicleModule))
            {
                return FrameRailSalvageCustody.WreckLine;
            }

            return FrameRailSalvageCustody.None;
        }

        private static void WriteMemory(
            CanonicalWriter writer,
            FactionMemoryRecord? memory)
        {
            writer.WriteBoolean(memory != null);
            if (memory == null)
            {
                return;
            }

            writer.WriteString(memory.StableId);
            writer.WriteString(memory.WitnessedAction);
            writer.WriteString(memory.AffectedFactionId);
            writer.WriteInt64(memory.Magnitude);
            writer.WriteString(memory.DoctrineTag);
            writer.WriteInt64(memory.EncounterTick);
            writer.WriteString(memory.ConsequenceCode);
        }

        private static FactionMemoryRecord? ReadMemory(CanonicalReader reader)
        {
            if (!reader.ReadBoolean())
            {
                return null;
            }

            return new FactionMemoryRecord(
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadString(),
                reader.ReadInt64(),
                reader.ReadString(),
                reader.ReadInt64(),
                reader.ReadString());
        }

        private static LastBearingDecodeResult Failure(string code)
        {
            return new LastBearingDecodeResult(false, code, null);
        }

        private static bool BytesEqual(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < first.Length; index++)
            {
                difference |= first[index] ^ second[index];
            }

            return difference == 0;
        }

        private static string ComputeDigestHex(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(digest.Length * 2);
                foreach (var value in digest)
                {
                    builder.Append(
                        value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private sealed class CanonicalWriter
        {
            private static readonly UTF8Encoding StrictUtf8 =
                new UTF8Encoding(false, true);
            private readonly List<byte> _bytes = new List<byte>(1024);

            internal void WriteByte(byte value)
            {
                _bytes.Add(value);
            }

            internal void WriteBoolean(bool value)
            {
                WriteByte(value ? (byte)1 : (byte)0);
            }

            internal void WriteUInt16(ushort value)
            {
                _bytes.Add((byte)value);
                _bytes.Add((byte)(value >> 8));
            }

            internal void WriteInt32(int value)
            {
                unchecked
                {
                    var unsigned = (uint)value;
                    for (var index = 0; index < sizeof(int); index++)
                    {
                        _bytes.Add((byte)(unsigned >> (index * 8)));
                    }
                }
            }

            internal void WriteInt64(long value)
            {
                unchecked
                {
                    var unsigned = (ulong)value;
                    for (var index = 0; index < sizeof(long); index++)
                    {
                        _bytes.Add((byte)(unsigned >> (index * 8)));
                    }
                }
            }

            internal void WriteEnum<TEnum>(TEnum value)
                where TEnum : struct
            {
                if (!Enum.IsDefined(typeof(TEnum), value))
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_CODEC_ENUM_INVALID");
                }

                WriteInt32(Convert.ToInt32(value, CultureInfo.InvariantCulture));
            }

            internal void WriteString(string value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                var encoded = StrictUtf8.GetBytes(value);
                if (encoded.Length > ushort.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                WriteUInt16(checked((ushort)encoded.Length));
                WriteBytes(encoded);
            }

            internal void WriteNullableString(string? value)
            {
                WriteBoolean(value != null);
                if (value != null)
                {
                    WriteString(value);
                }
            }

            internal void WriteBytes(byte[] bytes)
            {
                if (bytes == null)
                {
                    throw new ArgumentNullException(nameof(bytes));
                }

                _bytes.AddRange(bytes);
            }

            internal byte[] ToArray()
            {
                return _bytes.ToArray();
            }
        }

        private sealed class CanonicalReader
        {
            private static readonly UTF8Encoding StrictUtf8 =
                new UTF8Encoding(false, true);
            private readonly byte[] _bytes;
            private int _offset;

            internal CanonicalReader(byte[] bytes)
            {
                _bytes = bytes;
            }

            internal byte ReadByte()
            {
                RequireAvailable(1);
                return _bytes[_offset++];
            }

            internal bool ReadBoolean()
            {
                var value = ReadByte();
                if (value > 1)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_CODEC_BOOLEAN_INVALID");
                }

                return value == 1;
            }

            internal ushort ReadUInt16()
            {
                RequireAvailable(sizeof(ushort));
                var value = (ushort)(
                    _bytes[_offset]
                    | (_bytes[_offset + 1] << 8));
                _offset += sizeof(ushort);
                return value;
            }

            internal int ReadInt32()
            {
                RequireAvailable(sizeof(int));
                uint value = 0;
                for (var index = 0; index < sizeof(int); index++)
                {
                    value |= (uint)_bytes[_offset + index] << (index * 8);
                }

                _offset += sizeof(int);
                return unchecked((int)value);
            }

            internal long ReadInt64()
            {
                RequireAvailable(sizeof(long));
                ulong value = 0;
                for (var index = 0; index < sizeof(long); index++)
                {
                    value |= (ulong)_bytes[_offset + index] << (index * 8);
                }

                _offset += sizeof(long);
                return unchecked((long)value);
            }

            internal TEnum ReadEnum<TEnum>()
                where TEnum : struct
            {
                var raw = ReadInt32();
                var value = (TEnum)Enum.ToObject(typeof(TEnum), raw);
                if (!Enum.IsDefined(typeof(TEnum), value))
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_CODEC_ENUM_INVALID");
                }

                return value;
            }

            internal string ReadString()
            {
                var length = ReadUInt16();
                RequireAvailable(length);
                var value = StrictUtf8.GetString(_bytes, _offset, length);
                _offset += length;
                return value;
            }

            internal string? ReadNullableString()
            {
                return ReadBoolean() ? ReadString() : null;
            }

            internal void RequireBytes(byte[] expected)
            {
                RequireAvailable(expected.Length);
                for (var index = 0; index < expected.Length; index++)
                {
                    if (_bytes[_offset + index] != expected[index])
                    {
                        throw new InvalidOperationException(
                            "LAST_BEARING_CODEC_MAGIC_INVALID");
                    }
                }

                _offset += expected.Length;
            }

            internal void RequireEnd()
            {
                if (_offset != _bytes.Length)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_CODEC_TRAILING_BYTES");
                }
            }

            private void RequireAvailable(int count)
            {
                if (count < 0 || _offset > _bytes.Length - count)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_CODEC_TRUNCATED");
                }
            }
        }
    }
}
