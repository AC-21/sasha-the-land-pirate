#nullable enable

using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class ReturnedRailChassisBraceTests
    {
        internal static void Run(TestHarness harness)
        {
            harness.Run(
                "returned-rail brace spends two parts and one credited bundle",
                InstallHasExactConservationAndEvent);
            harness.Run(
                "returned-rail brace waits for service and exact custody",
                EarlyAndWrongStatesFailAtomically);
            harness.Run(
                "returned-rail brace rejects stale and insufficient intents",
                StaleAndInsufficientIntentsFailAtomically);
            harness.Run(
                "returned-rail brace replay cannot consume later rails",
                DuplicateReplayPreservesLaterSalvage);
            harness.Run(
                "delayed returned-rail brace preserves future repeat lineage",
                DelayedInstallPreservesFutureRepeatLineage);
            harness.Run(
                "returned-rail brace protects both routes for every roster",
                BothModulesAndAllRostersShareExactProtection);
            harness.Run(
                "returned-rail brace migrates schema 9 and round trips schema 11",
                SchemaNineMigrationAndSchemaElevenRoundTrip);
        }

        internal static CoreTestDriver CreateInstalledBraceRepeatReady(
            ColonyComposition composition,
            VehicleModule module,
            int worldSeed)
        {
            CoreTestDriver driver = ReachInstallReady(
                composition,
                module,
                worldSeed);
            driver.Apply(sequence =>
                new InstallReturnedRailChassisBraceCommand(sequence));
            return driver;
        }

        private static void InstallHasExactConservationAndEvent()
        {
            CoreTestDriver driver = ReachInstallReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3601);
            Pause(driver);
            driver = WithParts(
                driver,
                checked(
                    LastBearingBalanceV1
                        .ReturnedRailChassisBracePartsCostUnits
                    + LastBearingBalanceV1.MinimumPostReturnPartsUnits));

            TestHarness.True(
                driver.View.IsReturnedRailChassisBraceInstallAvailable,
                "credited rails did not expose brace installation");
            TestHarness.Equal(
                "install-returned-rail-chassis-brace",
                driver.View.NextObjective,
                "brace install objective");
            TestHarness.Equal(
                LastBearingBalanceV1
                    .ReturnedRailChassisBracePartsCostUnits,
                driver.View.ReturnedRailChassisBracePartsCostUnits,
                "presented brace parts cost");
            TestHarness.Equal(
                LastBearingBalanceV1
                    .ReturnedRailChassisBraceProtectionMilli,
                driver.View.ReturnedRailChassisBraceProtectionMilli,
                "presented brace protection");

            long beforeParts = driver.State.PartsUnits;
            LastBearingTickResult result = driver.Apply(sequence =>
                new InstallReturnedRailChassisBraceCommand(sequence));

            TestHarness.Equal(
                checked(
                    beforeParts
                    - LastBearingBalanceV1
                        .ReturnedRailChassisBracePartsCostUnits),
                driver.State.PartsUnits,
                "brace parts debit");
            TestHarness.Equal(
                LastBearingBalanceV1.MinimumPostReturnPartsUnits,
                driver.State.PartsUnits,
                "brace retained parts reserve");
            TestHarness.Equal(
                FrameRailSalvageCustody.None,
                driver.State.FrameRailSalvageCustody,
                "brace did not consume credited rail custody");
            TestHarness.True(
                driver.State.ReturnedRailChassisBraceInstalled,
                "authoritative brace flag");
            TestHarness.True(
                driver.View.ReturnedRailChassisBraceInstalled,
                "presented brace flag");
            TestHarness.True(
                !driver.View.IsReturnedRailChassisBraceInstallAvailable,
                "installed brace remained available");
            TestHarness.Equal(
                1,
                result.DomainEvents.Count,
                "brace install event count");
            LastBearingDomainEvent installed = result.DomainEvents[0];
            TestHarness.Equal(
                LastBearingEventKind.ReturnedRailChassisBraceInstalled,
                installed.Kind,
                "brace install event kind");
            TestHarness.Equal(
                LastBearingEventCause.PlayerCommand,
                installed.Cause,
                "brace install event cause");
            TestHarness.Equal(
                "vehicle:sasha:upgrade:returned-rail-chassis-brace",
                installed.SubjectId,
                "brace install event subject");
            TestHarness.Equal(0L, installed.BeforeValue, "brace event before");
            TestHarness.Equal(1L, installed.AfterValue, "brace event after");
        }

        private static void EarlyAndWrongStatesFailAtomically()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    3610);
            AssertRejected(
                initial,
                initial.NextCommandSequence,
                "LAST_BEARING_RETURNED_RAIL_CHASSIS_BRACE_NOT_READY",
                "untouched colony");
            AssertInvariantRejected(
                new LastBearingStateBuilder(initial)
                {
                    ReturnedRailChassisBraceInstalled = true,
                }.BuildUnchecked(),
                "LAST_BEARING_RETURNED_RAIL_CHASSIS_BRACE_STATE_INVALID",
                "forged pristine brace");

            CoreTestDriver damaged = ReachUrgentWorkResolved(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3611);
            TestHarness.Equal(
                FrameRailSalvageCustody.Credited,
                damaged.State.FrameRailSalvageCustody,
                "damaged fixture rail custody");
            TestHarness.True(
                damaged.View.IsVehicleServiceAvailable,
                "damaged fixture service");
            AssertRejected(
                damaged.State,
                damaged.State.NextCommandSequence,
                "LAST_BEARING_RETURNED_RAIL_CHASSIS_BRACE_NOT_READY",
                "before Scout service");

            CoreTestDriver ready = ReachInstallReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3612);
            LastBearingState missingBundle =
                new LastBearingStateBuilder(ready.State)
                {
                    FrameRailSalvageCustody =
                        FrameRailSalvageCustody.None,
                }.Build();
            AssertRejected(
                missingBundle,
                missingBundle.NextCommandSequence,
                "LAST_BEARING_RETURNED_RAIL_CHASSIS_BRACE_NOT_READY",
                "missing credited bundle");

            PrepareRepeat(ready);
            TestHarness.Equal(
                FrameRailSalvageCustody.WreckLine,
                ready.State.FrameRailSalvageCustody,
                "prepared repeat custody");
            AssertRejected(
                ready.State,
                ready.State.NextCommandSequence,
                "LAST_BEARING_RETURNED_RAIL_CHASSIS_BRACE_NOT_READY",
                "Wreck Line custody");
            ready.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                ready.State.TransactionId!,
                ready.State.TransactionFingerprint!));
            AdvanceUntil(
                ready,
                model => model.FrameRailSalvageCustody
                    == FrameRailSalvageCustody.Vehicle,
                drive: true,
                "repeat frame-rail recovery");
            AssertRejected(
                ready.State,
                ready.State.NextCommandSequence,
                "LAST_BEARING_RETURNED_RAIL_CHASSIS_BRACE_NOT_READY",
                "vehicle custody");
        }

        private static void StaleAndInsufficientIntentsFailAtomically()
        {
            CoreTestDriver ready = ReachInstallReady(
                ColonyComposition.RobotOnly,
                VehicleModule.SealedRangeTank,
                3620);
            AssertRejected(
                ready.State,
                checked(ready.State.NextCommandSequence + 1),
                "LAST_BEARING_COMMAND_SEQUENCE_MISMATCH",
                "future brace command sequence");

            LastBearingState insufficient =
                new LastBearingStateBuilder(ready.State)
                {
                    PartsUnits = checked(
                        LastBearingBalanceV1
                            .ReturnedRailChassisBracePartsCostUnits
                        + LastBearingBalanceV1
                            .MinimumPostReturnPartsUnits
                        - 1),
                }.Build();
            TestHarness.True(
                !LastBearingReadModel.FromState(insufficient)
                    .IsReturnedRailChassisBraceInstallAvailable,
                "insufficient parts exposed brace install");
            AssertRejected(
                insufficient,
                insufficient.NextCommandSequence,
                "LAST_BEARING_RETURNED_RAIL_CHASSIS_BRACE_PARTS_INSUFFICIENT",
                "insufficient brace parts");
        }

        private static void DuplicateReplayPreservesLaterSalvage()
        {
            CoreTestDriver driver = ReachInstallReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3630);
            driver.Apply(sequence =>
                new InstallReturnedRailChassisBraceCommand(sequence));
            long partsAfterInstall = driver.State.PartsUnits;
            RunRepeatCircuit(driver);

            TestHarness.True(
                driver.State.ReturnedRailChassisBraceInstalled,
                "brace disappeared during repeat route");
            TestHarness.Equal(
                FrameRailSalvageCustody.Credited,
                driver.State.FrameRailSalvageCustody,
                "later repeat bundle was not credited independently");
            Pause(driver);
            long partsWithLaterBundle = driver.State.PartsUnits;
            LastBearingTickResult replay = driver.Apply(sequence =>
                new InstallReturnedRailChassisBraceCommand(sequence));
            TestHarness.Equal(
                partsWithLaterBundle,
                driver.State.PartsUnits,
                "brace replay spent parts");
            TestHarness.True(
                driver.State.PartsUnits > partsAfterInstall,
                "later repeat salvage did not add parts");
            TestHarness.Equal(
                FrameRailSalvageCustody.Credited,
                driver.State.FrameRailSalvageCustody,
                "brace replay consumed later credited rails");
            TestHarness.Equal(
                1,
                replay.DomainEvents.Count,
                "brace replay event count");
            TestHarness.Equal(
                LastBearingEventKind.IdempotentReplayAccepted,
                replay.DomainEvents[0].Kind,
                "brace replay event");
        }

        private static void DelayedInstallPreservesFutureRepeatLineage()
        {
            CoreTestDriver driver = ReachInstallReady(
                ColonyComposition.Mixed,
                VehicleModule.WinchAssembly,
                3631);
            RunRepeatCircuit(driver);
            driver.Apply(sequence => new ServiceScoutCommand(sequence));

            TestHarness.True(
                driver.View.IsReturnedRailChassisBraceInstallAvailable,
                "repeat bundle did not expose delayed brace installation");
            driver.Apply(sequence =>
                new InstallReturnedRailChassisBraceCommand(sequence));
            TestHarness.Equal(
                FrameRailSalvageCustody.None,
                driver.State.FrameRailSalvageCustody,
                "delayed brace did not consume repeat bundle");
            LastBearingState forgedCityCredited =
                new LastBearingStateBuilder(driver.State)
                {
                    ExpeditionPhase = ExpeditionPhase.Returned,
                    TransactionPhase = TransactionPhase.CityCredited,
                    VehicleConditionMilli = checked(
                        LastBearingBalanceV1.StartingVehicleConditionMilli
                        - LastBearingBalanceV1.RouteConditionLoss(
                            driver.State.VehicleModule,
                            driver.State.RigUpgrade,
                            returnedRailChassisBraceInstalled: true)),
                }.BuildUnchecked();
            TestHarness.True(
                !LastBearingRepeatExpedition.IsLineage(
                    forgedCityCredited),
                "CityCredited accepted a brace-consumed bundle");

            byte[] saved = LastBearingCanonicalCodec.Encode(driver.State);
            LastBearingDecodeResult restored =
                LastBearingCanonicalCodec.TryDecode(saved);
            TestHarness.True(
                restored.Succeeded && restored.State != null,
                "delayed brace save did not restore");
            var continued = new CoreTestDriver(restored.State!);
            TestHarness.True(
                continued.State.ReturnedRailChassisBraceInstalled,
                "restored delayed brace flag");
            TestHarness.Equal(
                FrameRailSalvageCustody.None,
                continued.State.FrameRailSalvageCustody,
                "restored consumed repeat bundle");
            TestHarness.True(
                continued.View.IsRepeatExpeditionAvailable,
                "delayed brace permanently blocked the next repeat");
            TestHarness.Equal(
                "prepare-repeat-expedition",
                continued.View.NextObjective,
                "delayed brace next objective");

            PrepareRepeat(continued);
            TestHarness.Equal(
                TransactionPhase.Prepared,
                continued.State.TransactionPhase,
                "next repeat did not prepare after delayed brace");
            TestHarness.Equal(
                FrameRailSalvageCustody.WreckLine,
                continued.State.FrameRailSalvageCustody,
                "next repeat did not reset salvage source");
        }

        private static void BothModulesAndAllRostersShareExactProtection()
        {
            foreach (VehicleModule module in new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            })
            {
                string? expectedInstalledMechanics = null;
                foreach (ColonyComposition composition in new[]
                {
                    ColonyComposition.HumanOnly,
                    ColonyComposition.RobotOnly,
                    ColonyComposition.Mixed,
                })
                {
                    int seed = module == VehicleModule.WinchAssembly
                        ? 3640
                        : 3650;
                    CoreTestDriver unbraced = ReachInstallReady(
                        composition,
                        module,
                        seed);
                    CoreTestDriver braced = ReachInstallReady(
                        composition,
                        module,
                        seed);
                    braced.Apply(sequence =>
                        new InstallReturnedRailChassisBraceCommand(sequence));
                    string mechanics =
                        LastBearingCanonicalCodec.ComputeMechanicalSha256(
                            braced.State);
                    if (expectedInstalledMechanics == null)
                    {
                        expectedInstalledMechanics = mechanics;
                    }
                    else
                    {
                        TestHarness.Equal(
                            expectedInstalledMechanics,
                            mechanics,
                            composition + " installed brace mechanics");
                    }

                    TestHarness.Equal(
                        LastBearingBalanceV1.RouteConditionLoss(
                            module,
                            RigUpgrade.PatchworkSkidPlate,
                            returnedRailChassisBraceInstalled: true),
                        braced.View.ProjectedRoundTripConditionLossMilli,
                        composition + " projected braced loss");

                    RunRepeatCircuit(unbraced);
                    RunRepeatCircuit(braced);
                    long protection = checked(
                        braced.State.VehicleConditionMilli
                        - unbraced.State.VehicleConditionMilli);
                    TestHarness.Equal(
                        LastBearingBalanceV1
                            .ReturnedRailChassisBraceProtectionMilli,
                        protection,
                        composition + " " + module + " route protection");
                    TestHarness.Equal(
                        checked(
                            LastBearingBalanceV1
                                .StartingVehicleConditionMilli
                            - LastBearingBalanceV1.RouteConditionLoss(
                                module,
                                RigUpgrade.PatchworkSkidPlate,
                                returnedRailChassisBraceInstalled: true)),
                        braced.State.VehicleConditionMilli,
                        composition + " " + module + " braced condition");
                }
            }
        }

        private static void SchemaNineMigrationAndSchemaElevenRoundTrip()
        {
            CoreTestDriver current = ReachInstallReady(
                ColonyComposition.Mixed,
                VehicleModule.SealedRangeTank,
                3660);
            current.Apply(sequence =>
                new InstallReturnedRailChassisBraceCommand(sequence));
            TestHarness.Equal(
                11,
                current.State.SchemaVersion,
                "schema 11 installed state");
            byte[] canonical = LastBearingCanonicalCodec.Encode(current.State);
            TestHarness.Equal(
                "420d3f3ab25dc138860398fba8f2770c28bef04e1420f5dd621210a6eedabc2b",
                ComputeSha256(canonical),
                "schema 11 installed golden digest");
            LastBearingDecodeResult restored =
                LastBearingCanonicalCodec.TryDecode(canonical);
            TestHarness.True(
                restored.Succeeded && restored.State != null,
                "schema 11 brace decode");
            TestHarness.True(
                restored.State!.ReturnedRailChassisBraceInstalled,
                "schema 11 brace flag");
            TestHarness.True(
                canonical.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(restored.State)),
                "schema 11 canonical golden round trip");
            TestHarness.Equal(
                current.View.ProjectedRoundTripConditionLossMilli,
                LastBearingReadModel.FromState(restored.State)
                    .ProjectedRoundTripConditionLossMilli,
                "schema 11 restored route protection");

            LastBearingState legacySource =
                new LastBearingStateBuilder(
                    ReachInstallReady(
                        ColonyComposition.HumanOnly,
                        VehicleModule.WinchAssembly,
                        3661).State)
                {
                    EmergencyCisternCharged = true,
                }.Build();
            byte[] schemaNine =
                LastBearingCanonicalCodec.EncodeLegacyV9ForMigrationTests(
                    legacySource);
            TestHarness.Equal(
                "ed574b750d7824e9241a6717cd17c283bd3a1958e275f1a25bd831931091b4bf",
                ComputeSha256(schemaNine),
                "schema 9 source golden digest");
            LastBearingDecodeResult first =
                LastBearingCanonicalCodec.TryDecode(schemaNine);
            LastBearingDecodeResult second =
                LastBearingCanonicalCodec.TryDecode(schemaNine);
            TestHarness.True(
                first.Succeeded
                    && first.State != null
                    && second.Succeeded
                    && second.State != null,
                "schema 9 brace migration decode");
            TestHarness.Equal(
                11,
                first.State!.SchemaVersion,
                "schema 9 migrated schema");
            TestHarness.True(
                !first.State.ReturnedRailChassisBraceInstalled,
                "schema 9 brace default");
            TestHarness.True(
                first.State.EmergencyCisternCharged,
                "schema 9 cistern flag was not preserved");
            TestHarness.True(
                schemaNine.SequenceEqual(
                    LastBearingCanonicalCodec
                        .EncodeLegacyV9ForMigrationTests(first.State)),
                "schema 9 source golden bytes");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(first.State),
                LastBearingCanonicalCodec.ComputeSha256(second.State!),
                "schema 9 migration determinism");
        }

        private static CoreTestDriver ReachInstallReady(
            ColonyComposition composition,
            VehicleModule module,
            int worldSeed)
        {
            CoreTestDriver driver = ReachUrgentWorkResolved(
                composition,
                module,
                worldSeed);
            driver.Apply(sequence => new ServiceScoutCommand(sequence));
            TestHarness.Equal(
                LastBearingBalanceV1.StartingVehicleConditionMilli,
                driver.State.VehicleConditionMilli,
                "brace fixture Scout service");
            TestHarness.True(
                driver.View.IsReturnedRailChassisBraceInstallAvailable,
                "brace fixture install availability");
            return driver;
        }

        private static CoreTestDriver ReachUrgentWorkResolved(
            ColonyComposition composition,
            VehicleModule module,
            int worldSeed)
        {
            CoreTestDriver driver = ReachRepairedReturn(
                composition,
                module,
                worldSeed);
            if (driver.State.FactionAidPolicy
                == FactionAidPolicy.EmergencyWaterQueued)
            {
                driver.Apply(sequence =>
                    new ReceiveEmergencyAidCommand(sequence));
            }

            switch (driver.State.NextCityDecision)
            {
                case NextCityDecision.RefurbishAuxiliaryPump:
                    driver.Apply(sequence =>
                        new InstallCityImprovementCommand(
                            sequence,
                            NextCityDecision.RefurbishAuxiliaryPump,
                            LastBearingState.AuxiliaryPumpSocketId,
                            LastBearingState
                                .AuxiliaryPumpOrientationQuarterTurns));
                    break;
                case NextCityDecision.ExpandEmergencyCistern:
                    driver.Apply(sequence =>
                        new InstallCityImprovementCommand(
                            sequence,
                            NextCityDecision.ExpandEmergencyCistern,
                            LastBearingState
                                .EmergencyStorageExpansionSocketId,
                            LastBearingState
                                .EmergencyStorageExpansionOrientationQuarterTurns));
                    break;
                case NextCityDecision.MachineSpareBearing:
                    driver.Apply(sequence =>
                        new StartSpareBearingBatchCommand(sequence));
                    AdvanceUntil(
                        driver,
                        model => model.SpareBearingBatchPhase
                            == SpareBearingBatchPhase.Complete,
                        drive: false,
                        "brace fixture one good batch");
                    driver.Apply(sequence =>
                        new BarterSpareBearingLotCommand(sequence));
                    break;
                case NextCityDecision.RestoreDepotAccess:
                    driver.Apply(sequence =>
                        new RestoreDepotAccessCommand(sequence));
                    break;
                case NextCityDecision.None:
                    break;
                default:
                    throw new InvalidOperationException(
                        "unexpected brace prerequisite decision");
            }

            TestHarness.Equal(
                FrameRailSalvageCustody.Credited,
                driver.State.FrameRailSalvageCustody,
                "brace fixture credited rails");
            TestHarness.True(
                driver.View.IsVehicleServiceAvailable,
                "brace fixture service availability");
            return driver;
        }

        private static CoreTestDriver ReachRepairedReturn(
            ColonyComposition composition,
            VehicleModule module,
            int worldSeed)
        {
            string residentId =
                composition == ColonyComposition.RobotOnly
                    ? ResidentRoster.RobotResidentId
                    : ResidentRoster.HumanResidentId;
            var driver = new CoreTestDriver(composition, worldSeed);
            driver.Apply(sequence =>
                new AssignResidentCommand(sequence, residentId));
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            driver.Apply(sequence =>
                new InstallRigUpgradeCommand(
                    sequence,
                    RigUpgrade.PatchworkSkidPlate));
            driver.Apply(sequence =>
                new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.CivicBuffer,
                    module));
            driver.Apply(sequence =>
                new InstallVehicleModuleCommand(sequence, module));
            AdvanceUntil(
                driver,
                model => model.PreparationPhase == PreparationPhase.Ready,
                drive: false,
                "brace fixture preparation");

            string transactionId = "tx:brace:first:" + worldSeed;
            string fingerprint = "fp:brace:first:" + worldSeed;
            driver.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "brace fixture depot approach");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence =>
                new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.Cooperate));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            if (module == VehicleModule.SealedRangeTank)
            {
                driver.Apply(sequence =>
                    new ChooseLiquidReturnCommand(
                        sequence,
                        LiquidCargoKind.Fuel));
            }

            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "brace fixture home return");
            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence =>
                new InstallTurbineRepairCommand(sequence));
            return driver;
        }

        private static void RunRepeatCircuit(CoreTestDriver driver)
        {
            PrepareRepeat(driver);
            string transactionId = driver.State.TransactionId!;
            string fingerprint = driver.State.TransactionFingerprint!;
            driver.Apply(sequence => new DebitCityManifestCommand(
                sequence,
                transactionId,
                fingerprint));
            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "brace repeat depot approach");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence => new FreezeReturnPayloadCommand(
                sequence,
                transactionId,
                fingerprint));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "brace repeat home return");
            driver.Apply(sequence => new CreditCityReturnCommand(
                sequence,
                transactionId,
                fingerprint));
            driver.Apply(sequence =>
                new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
        }

        private static void PrepareRepeat(CoreTestDriver driver)
        {
            long sequence = driver.State.NextCommandSequence;
            string suffix = sequence.ToString(CultureInfo.InvariantCulture);
            driver.Apply(current =>
                new PrepareRepeatExpeditionTransactionCommand(
                    current,
                    driver.State.TransactionId!,
                    driver.State.TransactionFingerprint!,
                    "tx:repeat:" + suffix,
                    "fp:repeat:" + suffix));
        }

        private static CoreTestDriver WithParts(
            CoreTestDriver driver,
            long parts)
        {
            return new CoreTestDriver(
                new LastBearingStateBuilder(driver.State)
                {
                    PartsUnits = parts,
                }.Build());
        }

        private static void Pause(CoreTestDriver driver)
        {
            if (driver.State.PauseCause == PauseCause.None)
            {
                driver.Apply(sequence =>
                    new SetPauseCommand(sequence, true));
            }
        }

        private static void AssertRejected(
            LastBearingState state,
            long sequence,
            string expectedCode,
            string label)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new LastBearingCommand[]
                        {
                            new InstallReturnedRailChassisBraceCommand(
                                sequence),
                        }),
                    label + " was accepted");
            TestHarness.Equal(
                expectedCode,
                error.Message,
                label + " rejection code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                label + " mutated source state");
        }

        private static void AdvanceUntil(
            CoreTestDriver driver,
            Func<LastBearingReadModel, bool> predicate,
            bool drive,
            string label)
        {
            for (var ticks = 0; ticks < 7000; ticks++)
            {
                if (predicate(driver.View))
                {
                    return;
                }

                driver.OperateWreckLineIfAvailable();
                if (drive)
                {
                    driver.Apply(sequence =>
                        new DriveVehicleCommand(sequence, 1000, 0));
                }
                else
                {
                    driver.Advance(1);
                }
            }

            throw new InvalidOperationException(
                label + " did not converge");
        }

        private static void AssertInvariantRejected(
            LastBearingState state,
            string expectedCode,
            string label)
        {
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingReadModel.FromState(state),
                    label + " passed invariants");
            TestHarness.Equal(
                expectedCode,
                error.Message,
                label + " invariant code");
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] digest = sha256.ComputeHash(bytes);
            var builder = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
            {
                builder.Append(
                    value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
