#nullable enable

using System;
using System.IO;
using System.Linq;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class FrameRailSalvageTests
    {
        internal static void RunCore(TestHarness harness)
        {
            harness.Run(
                "both modules hold for one explicit Wreck Line frame-rail recovery",
                BothModulesHoldForExplicitRecovery);
            harness.Run(
                "routes without the skid plate preserve the existing Wreck Line flow",
                RouteWithoutUpgradeIsUnchanged);
            harness.Run(
                "frame rails coexist with repair cargo and credit four parts once",
                SalvageCoexistsAndCreditsOnce);
            harness.Run(
                "forged frame-rail custody and occupancy fail closed",
                ForgedSalvageStatesFailClosed);
        }

        internal static void RunSave(
            TestHarness harness,
            string repoRoot)
        {
            harness.Run(
                "frame-rail custody round trips and v5 migration is deterministic",
                () => SaveAndLegacyV5MigrationRoundTrip(repoRoot));
        }

        private static void BothModulesHoldForExplicitRecovery()
        {
            foreach (VehicleModule module in new[]
            {
                VehicleModule.WinchAssembly,
                VehicleModule.SealedRangeTank,
            })
            {
                CoreTestDriver driver = ReachGate(
                    module,
                    checked(2500 + (int)module),
                    installUpgrade: true);
                long gate = driver.View.WreckLineGateTicks;
                LastBearingTickResult moduleResult = driver.Apply(sequence =>
                    new OperateWreckLineModuleCommand(
                        sequence,
                        driver.View.RouteActionKind));

                TestHarness.True(
                    moduleResult.DomainEvents.Any(item =>
                        item.Kind == LastBearingEventKind.RouteActionUsed),
                    module + " module action event");
                TestHarness.Equal(
                    FrameRailSalvageCustody.WreckLine,
                    driver.State.FrameRailSalvageCustody,
                    module + " staged salvage custody");
                TestHarness.True(
                    driver.View.IsWreckLineFrameRailRecoveryAvailable,
                    module + " salvage interaction unavailable");
                TestHarness.Equal(
                    "recover-wreck-line-frame-rails",
                    driver.View.NextObjective,
                    module + " recovery objective");
                AssertRejectedWithoutMutation(
                    driver.State,
                    new DriveVehicleCommand(
                        driver.State.NextCommandSequence,
                        1000,
                        0),
                    "LAST_BEARING_WRECK_LINE_FRAME_RAIL_RECOVERY_REQUIRED",
                    module + " drove past salvage");

                LastBearingTickResult recovered = driver.Apply(sequence =>
                    new RecoverWreckLineFrameRailsCommand(sequence));
                TestHarness.Equal(
                    FrameRailSalvageCustody.Vehicle,
                    driver.State.FrameRailSalvageCustody,
                    module + " recovered custody");
                TestHarness.Equal(
                    LastBearingBalanceV1
                        .WreckLineFrameRailSalvageCargoUnits,
                    driver.State.OrdinaryCargoUsedUnits,
                    module + " salvage cargo occupancy");
                TestHarness.True(
                    recovered.DomainEvents.Any(item =>
                        item.Kind
                            == LastBearingEventKind
                                .FrameRailSalvageTransferred
                        && item.BeforeValue
                            == (long)FrameRailSalvageCustody.WreckLine
                        && item.AfterValue
                            == (long)FrameRailSalvageCustody.Vehicle),
                    module + " salvage transfer event");

                LastBearingTickResult replay = driver.Apply(sequence =>
                    new RecoverWreckLineFrameRailsCommand(sequence));
                TestHarness.Equal(
                    LastBearingBalanceV1
                        .WreckLineFrameRailSalvageCargoUnits,
                    driver.State.OrdinaryCargoUsedUnits,
                    module + " duplicate cargo occupancy");
                TestHarness.True(
                    replay.DomainEvents.Any(item =>
                        item.Kind
                            == LastBearingEventKind
                                .IdempotentReplayAccepted),
                    module + " recovery replay");
                TestHarness.True(
                    replay.DomainEvents.All(item =>
                        item.Kind
                            != LastBearingEventKind
                                .FrameRailSalvageTransferred),
                    module + " duplicate transfer event");

                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
                TestHarness.True(
                    driver.State.RouteProgressTicks > gate,
                    module + " route did not resume after recovery");
            }
        }

        private static void RouteWithoutUpgradeIsUnchanged()
        {
            CoreTestDriver driver = ReachGate(
                VehicleModule.SealedRangeTank,
                2510,
                installUpgrade: false);
            long gate = driver.View.WreckLineGateTicks;
            driver.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    driver.View.RouteActionKind));

            TestHarness.Equal(
                FrameRailSalvageCustody.None,
                driver.State.FrameRailSalvageCustody,
                "standard route invented salvage");
            TestHarness.True(
                !driver.View.IsWreckLineFrameRailRecoveryAvailable,
                "standard route exposed salvage interaction");
            driver.Apply(sequence =>
                new DriveVehicleCommand(sequence, 1000, 0));
            TestHarness.True(
                driver.State.RouteProgressTicks > gate,
                "standard route no longer resumes after module action");
        }

        private static void SalvageCoexistsAndCreditsOnce()
        {
            CoreTestDriver driver = ReachGate(
                VehicleModule.WinchAssembly,
                2511,
                installUpgrade: true);
            driver.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    driver.View.RouteActionKind));
            driver.Apply(sequence =>
                new RecoverWreckLineFrameRailsCommand(sequence));
            DriveToDepot(driver);
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence =>
                new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.Cooperate));

            TestHarness.True(
                driver.View.IsRepairCargoLoadAvailable,
                "salvage blocked repair cargo loading");
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            TestHarness.Equal(
                2L,
                driver.State.OrdinaryCargoUsedUnits,
                "salvage and repair cargo occupancy");
            TestHarness.Equal(
                FrameRailSalvageCustody.Vehicle,
                driver.State.FrameRailSalvageCustody,
                "salvage custody before return");
            TestHarness.Equal(
                RepairCargoCustody.Vehicle,
                driver.State.RepairCargoCustody,
                "repair custody before return");

            string transactionId = driver.State.TransactionId!;
            string fingerprint = driver.State.TransactionFingerprint!;
            driver.Apply(sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            while (driver.State.ExpeditionPhase != ExpeditionPhase.Returned)
            {
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            LastBearingState arrival = driver.State;
            long partsBefore = arrival.PartsUnits;
            long sequence = arrival.NextCommandSequence;
            var kernel = new LastBearingKernel();
            LastBearingTickResult checkIn = kernel.Step(
                arrival,
                new LastBearingCommand[]
                {
                    new CreditCityReturnCommand(
                        sequence,
                        transactionId,
                        fingerprint),
                    new FinalizeExpeditionTransactionCommand(
                        sequence + 1,
                        transactionId,
                        fingerprint),
                });

            TestHarness.Equal(
                partsBefore
                    + LastBearingBalanceV1
                        .WreckLineFrameRailSalvagePartsUnits,
                checkIn.State.PartsUnits,
                "frame-rail parts credit");
            TestHarness.Equal(
                FrameRailSalvageCustody.Credited,
                checkIn.State.FrameRailSalvageCustody,
                "credited salvage custody");
            TestHarness.Equal(
                1L,
                checkIn.State.OrdinaryCargoUsedUnits,
                "salvage cargo unit was not released");
            TestHarness.Equal(
                RepairCargoCustody.Vehicle,
                checkIn.State.RepairCargoCustody,
                "home check-in moved repair cargo");
            TestHarness.True(
                checkIn.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind
                            .FrameRailSalvageTransferred
                    && item.BeforeValue
                        == (long)FrameRailSalvageCustody.Vehicle
                    && item.AfterValue
                        == (long)FrameRailSalvageCustody.Credited),
                "home salvage credit event");

            long replaySequence = checkIn.State.NextCommandSequence;
            LastBearingTickResult replay = kernel.Step(
                checkIn.State,
                new LastBearingCommand[]
                {
                    new CreditCityReturnCommand(
                        replaySequence,
                        transactionId,
                        fingerprint),
                    new FinalizeExpeditionTransactionCommand(
                        replaySequence + 1,
                        transactionId,
                        fingerprint),
                });
            TestHarness.Equal(
                checkIn.State.PartsUnits,
                replay.State.PartsUnits,
                "repeated check-in duplicated parts");
            TestHarness.Equal(
                1L,
                replay.State.OrdinaryCargoUsedUnits,
                "repeated check-in changed repair occupancy");
            TestHarness.Equal(
                RepairCargoCustody.Vehicle,
                replay.State.RepairCargoCustody,
                "repeated check-in moved repair cargo");
            TestHarness.True(
                replay.DomainEvents.All(item =>
                    item.Kind
                        != LastBearingEventKind
                            .FrameRailSalvageTransferred),
                "repeated check-in emitted salvage transfer");
            TestHarness.Equal(
                2,
                replay.DomainEvents.Count(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "repeated check-in replay count");
        }

        private static void ForgedSalvageStatesFailClosed()
        {
            CoreTestDriver gate = ReachGate(
                VehicleModule.WinchAssembly,
                2512,
                installUpgrade: true);
            gate.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    gate.View.RouteActionKind));

            var noUpgrade = new LastBearingStateBuilder(gate.State)
            {
                RigUpgrade = RigUpgrade.None,
            };
            AssertInvariantRejected(
                new LastBearingState(noUpgrade),
                "LAST_BEARING_FRAME_RAIL_SALVAGE_UPGRADE_REQUIRED",
                "salvage without skid plate");

            var bypassedGate = new LastBearingStateBuilder(gate.State)
            {
                RouteProgressTicks = checked(
                    gate.State.RouteProgressTicks + 1),
            };
            AssertInvariantRejected(
                new LastBearingState(bypassedGate),
                "LAST_BEARING_FRAME_RAIL_SALVAGE_GATE_BYPASSED",
                "unrecovered salvage beyond gate");

            gate.Apply(sequence =>
                new RecoverWreckLineFrameRailsCommand(sequence));
            var missingOccupancy = new LastBearingStateBuilder(gate.State)
            {
                OrdinaryCargoUsedUnits = 0,
            };
            AssertInvariantRejected(
                new LastBearingState(missingOccupancy),
                "LAST_BEARING_EMPTY_REPAIR_CUSTODY_INVALID",
                "vehicle salvage without cargo occupancy");

            var prematureCredit = new LastBearingStateBuilder(gate.State)
            {
                FrameRailSalvageCustody =
                    FrameRailSalvageCustody.Credited,
                OrdinaryCargoUsedUnits = 0,
            };
            AssertInvariantRejected(
                new LastBearingState(prematureCredit),
                "LAST_BEARING_FRAME_RAIL_SALVAGE_CREDIT_INVALID",
                "salvage credited on the road");
        }

        private static void SaveAndLegacyV5MigrationRoundTrip(
            string repoRoot)
        {
            CoreTestDriver gate = ReachGate(
                VehicleModule.SealedRangeTank,
                2513,
                installUpgrade: true);
            gate.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    gate.View.RouteActionKind));
            gate.Apply(sequence =>
                new RecoverWreckLineFrameRailsCommand(sequence));

            byte[] canonical = LastBearingCanonicalCodec.Encode(gate.State);
            TestHarness.Equal((byte)7, canonical[8], "v7 codec marker");
            string profile = FreshProfile(repoRoot, "frame-rail-vehicle");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted = store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                "frame-rail profile persist: " + persisted.Code);
            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                "frame-rail profile load: " + loaded.Code);
            TestHarness.True(
                canonical.SequenceEqual(loaded.CanonicalPayload!),
                "frame-rail profile bytes changed");
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(
                    loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "frame-rail profile decode");
            TestHarness.Equal(
                FrameRailSalvageCustody.Vehicle,
                decoded.State!.FrameRailSalvageCustody,
                "restored vehicle salvage");
            TestHarness.Equal(
                1L,
                decoded.State.OrdinaryCargoUsedUnits,
                "restored salvage occupancy");

            CoreTestDriver legacyGate = ReachGate(
                VehicleModule.WinchAssembly,
                2514,
                installUpgrade: true);
            legacyGate.Apply(sequence =>
                new OperateWreckLineModuleCommand(
                    sequence,
                    legacyGate.View.RouteActionKind));
            AssertLegacyV5Migration(
                legacyGate.State,
                FrameRailSalvageCustody.WreckLine,
                interactionAvailable: true,
                "legacy exact gate");

            var postGateBuilder =
                new LastBearingStateBuilder(legacyGate.State)
                {
                    FrameRailSalvageCustody =
                        FrameRailSalvageCustody.None,
                    RouteProgressTicks = checked(
                        legacyGate.State.RouteProgressTicks + 1),
                };
            LastBearingState postGate = postGateBuilder.Build();
            AssertLegacyV5Migration(
                postGate,
                FrameRailSalvageCustody.None,
                interactionAvailable: false,
                "legacy post-gate");

            var home = new CoreTestDriver(
                ColonyComposition.Mixed,
                2515);
            home.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            home.Apply(sequence =>
                new InstallRigUpgradeCommand(
                    sequence,
                    RigUpgrade.PatchworkSkidPlate));
            AssertLegacyV5Migration(
                home.State,
                FrameRailSalvageCustody.WreckLine,
                interactionAvailable: false,
                "legacy home");
        }

        private static void AssertLegacyV5Migration(
            LastBearingState source,
            FrameRailSalvageCustody expectedCustody,
            bool interactionAvailable,
            string label)
        {
            byte[] legacy =
                LastBearingCanonicalCodec
                    .EncodeLegacyV5ForMigrationTests(source);
            TestHarness.Equal((byte)5, legacy[8], label + " v5 marker");
            LastBearingDecodeResult first =
                LastBearingCanonicalCodec.TryDecode(legacy);
            LastBearingDecodeResult second =
                LastBearingCanonicalCodec.TryDecode(legacy);
            TestHarness.True(
                first.Succeeded
                    && first.State != null
                    && second.Succeeded
                    && second.State != null,
                label + " migration decode");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                first.State!.SchemaVersion,
                label + " migrated schema");
            TestHarness.Equal(
                expectedCustody,
                first.State.FrameRailSalvageCustody,
                label + " migrated custody");
            TestHarness.Equal(
                interactionAvailable,
                LastBearingReadModel.FromState(first.State)
                    .IsWreckLineFrameRailRecoveryAvailable,
                label + " migrated interaction");
            TestHarness.True(
                legacy.SequenceEqual(
                    LastBearingCanonicalCodec
                        .EncodeLegacyV5ForMigrationTests(first.State)),
                label + " legacy bytes changed");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(first.State),
                LastBearingCanonicalCodec.ComputeSha256(second.State!),
                label + " migration was not deterministic");
        }

        private static CoreTestDriver ReachGate(
            VehicleModule module,
            int worldSeed,
            bool installUpgrade)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                worldSeed);
            if (installUpgrade)
            {
                driver.Apply(sequence =>
                    new ActivateSliceInfrastructureCommand(sequence));
                driver.Apply(sequence =>
                    new InstallRigUpgradeCommand(
                        sequence,
                        RigUpgrade.PatchworkSkidPlate));
            }

            driver.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                module);
            while (driver.View.PreparationPhase != PreparationPhase.Ready)
            {
                driver.Advance(1);
            }

            string transactionId = "tx:frame-rails:" + worldSeed;
            string fingerprint = "fp:frame-rails:" + worldSeed;
            driver.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            driver.Apply(sequence =>
                new DebitCityManifestCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            while (!driver.View.IsWreckLineModulePointAvailable)
            {
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }

            return driver;
        }

        private static void DriveToDepot(CoreTestDriver driver)
        {
            while (!driver.View.IsDepotApproachRecoveryAvailable)
            {
                driver.Apply(sequence =>
                    new DriveVehicleCommand(sequence, 1000, 0));
            }
        }

        private static void AssertRejectedWithoutMutation(
            LastBearingState state,
            LastBearingCommand command,
            string expectedCode,
            string label)
        {
            byte[] before = LastBearingCanonicalCodec.Encode(state);
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => new LastBearingKernel().Step(
                        state,
                        new[] { command }),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                label + " mutated input");
        }

        private static void AssertInvariantRejected(
            LastBearingState state,
            string expectedCode,
            string label)
        {
            InvalidOperationException error =
                TestHarness.Throws<InvalidOperationException>(
                    () => LastBearingInvariants.Validate(state),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
        }

        private static string FreshProfile(
            string repoRoot,
            string caseName)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/frame-rail-salvage",
                caseName);
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }

            Directory.CreateDirectory(parent);
            return Path.Combine(
                parent,
                LastBearingProfileContract.ProfileName);
        }
    }
}
