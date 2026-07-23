#nullable enable

using System;
using System.IO;
using System.Linq;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class RigUpgradeTests
    {
        internal static void Run(TestHarness harness, string repoRoot)
        {
            harness.Run(
                "patchwork skid plate has one exact cost and read-model effect",
                InstallHasExactCostAndPresentationState);
            harness.Run(
                "patchwork skid plate changes the credited road condition loss",
                UpgradeChangesRoadOutcomeDeterministically);
            harness.Run(
                "patchwork skid plate guards fail closed and replay once",
                InvalidInstallationsFailClosed);
            harness.Run(
                "patchwork skid plate save and v4 migration round trip exactly",
                () => SaveAndLegacyV4MigrationRoundTrip(repoRoot));
        }

        private static void InstallHasExactCostAndPresentationState()
        {
            var driver = new CoreTestDriver(
                ColonyComposition.Mixed,
                2401);
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));

            TestHarness.True(
                driver.View.IsPatchworkSkidPlateInstallAvailable,
                "working service cell did not expose the garage upgrade");
            TestHarness.Equal(
                LastBearingBalanceV1.PatchworkSkidPlatePartsCostUnits,
                driver.View.PatchworkSkidPlatePartsCostUnits,
                "presented skid plate cost");
            TestHarness.Equal(
                LastBearingBalanceV1.PatchworkSkidPlateProtectionMilli,
                driver.View.PatchworkSkidPlateProtectionMilli,
                "presented skid plate protection");

            long partsBefore = driver.State.PartsUnits;
            LastBearingTickResult result = driver.Apply(sequence =>
                new InstallRigUpgradeCommand(
                    sequence,
                    RigUpgrade.PatchworkSkidPlate));

            TestHarness.Equal(
                RigUpgrade.PatchworkSkidPlate,
                driver.State.RigUpgrade,
                "authoritative rig upgrade");
            TestHarness.Equal(
                partsBefore
                    - LastBearingBalanceV1
                        .PatchworkSkidPlatePartsCostUnits,
                driver.State.PartsUnits,
                "skid plate parts debit");
            TestHarness.True(
                !driver.View.IsPatchworkSkidPlateInstallAvailable,
                "installed skid plate remained available");
            TestHarness.True(
                result.DomainEvents.Any(item =>
                    item.Kind == LastBearingEventKind.RigUpgradeInstalled
                    && item.BeforeValue == (long)RigUpgrade.None
                    && item.AfterValue
                        == (long)RigUpgrade.PatchworkSkidPlate),
                "skid plate installation event");

            driver.Apply(sequence =>
                new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.CivicBuffer,
                    VehicleModule.WinchAssembly));
            TestHarness.Equal(
                LastBearingBalanceV1
                    .ShortRouteRoundTripConditionLossMilli
                    - LastBearingBalanceV1
                        .PatchworkSkidPlateProtectionMilli,
                driver.View.ProjectedRoundTripConditionLossMilli,
                "projected upgraded route condition loss");
        }

        private static void UpgradeChangesRoadOutcomeDeterministically()
        {
            LastBearingState standard = CompleteCooperativeReturn(
                installUpgrade: false,
                worldSeed: 2402);
            LastBearingState upgraded = CompleteCooperativeReturn(
                installUpgrade: true,
                worldSeed: 2402);
            LastBearingState repeated = CompleteCooperativeReturn(
                installUpgrade: true,
                worldSeed: 2402);

            TestHarness.Equal(
                LastBearingBalanceV1.StartingVehicleConditionMilli
                    - LastBearingBalanceV1
                        .ShortRouteRoundTripConditionLossMilli,
                standard.VehicleConditionMilli,
                "standard round-trip condition");
            TestHarness.Equal(
                standard.VehicleConditionMilli
                    + LastBearingBalanceV1
                        .PatchworkSkidPlateProtectionMilli,
                upgraded.VehicleConditionMilli,
                "upgraded round-trip condition");
            TestHarness.True(
                LastBearingCanonicalCodec.Encode(upgraded)
                    .SequenceEqual(
                        LastBearingCanonicalCodec.Encode(repeated)),
                "identical upgraded road schedules diverged");
        }

        private static void InvalidInstallationsFailClosed()
        {
            LastBearingState initial =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.HumanOnly,
                    2403);
            AssertRejected(
                initial,
                "LAST_BEARING_RIG_UPGRADE_REQUIRES_SERVICE_CELL",
                "installation before service cell");

            var active = new CoreTestDriver(initial);
            active.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            LastBearingState insufficient =
                new LastBearingStateBuilder(active.State)
                {
                    PartsUnits =
                        LastBearingBalanceV1
                            .PatchworkSkidPlatePartsCostUnits - 1,
                }.Build();
            AssertRejected(
                insufficient,
                "LAST_BEARING_RIG_UPGRADE_PARTS_INSUFFICIENT",
                "installation without parts");

            var owned = new CoreTestDriver(
                ColonyComposition.HumanOnly,
                2404);
            owned.StartPreparation(
                ResidentRoster.HumanResidentId,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            AdvanceUntil(
                owned,
                model => model.PreparationPhase == PreparationPhase.Ready,
                drive: false,
                "owned preparation");
            owned.Apply(sequence =>
                new PrepareExpeditionTransactionCommand(
                    sequence,
                    "tx:skid-plate-guard",
                    "fp:skid-plate-guard"));
            AssertRejected(
                owned.State,
                "LAST_BEARING_RIG_UPGRADE_REQUIRES_HOME_GARAGE",
                "installation after expedition ownership");

            var installed = new CoreTestDriver(
                ColonyComposition.RobotOnly,
                2405);
            installed.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            installed.Apply(sequence =>
                new InstallRigUpgradeCommand(
                    sequence,
                    RigUpgrade.PatchworkSkidPlate));
            long afterFirstInstall = installed.State.PartsUnits;
            LastBearingTickResult replay = installed.Apply(sequence =>
                new InstallRigUpgradeCommand(
                    sequence,
                    RigUpgrade.PatchworkSkidPlate));
            TestHarness.Equal(
                afterFirstInstall,
                installed.State.PartsUnits,
                "idempotent retry repeated parts debit");
            TestHarness.True(
                replay.DomainEvents.Any(item =>
                    item.Kind
                        == LastBearingEventKind.IdempotentReplayAccepted),
                "idempotent retry lacked replay evidence");
            TestHarness.Throws<ArgumentOutOfRangeException>(
                () => new InstallRigUpgradeCommand(
                    installed.State.NextCommandSequence,
                    RigUpgrade.None),
                "empty rig upgrade command was accepted");
        }

        private static void SaveAndLegacyV4MigrationRoundTrip(
            string repoRoot)
        {
            var driver = new CoreTestDriver(
                ColonyComposition.Mixed,
                2406);
            driver.Apply(sequence =>
                new ActivateSliceInfrastructureCommand(sequence));
            driver.Apply(sequence =>
                new InstallRigUpgradeCommand(
                    sequence,
                    RigUpgrade.PatchworkSkidPlate));
            driver.Apply(sequence =>
                new SelectPreparationCommand(
                    sequence,
                    PreparationChoice.WorkshopPush,
                    VehicleModule.SealedRangeTank));

            byte[] canonical = LastBearingCanonicalCodec.Encode(driver.State);
            TestHarness.Equal((byte)7, canonical[8], "v7 codec marker");
            string profileParent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/rig-upgrade-save");
            if (Directory.Exists(profileParent))
            {
                Directory.Delete(profileParent, recursive: true);
            }

            Directory.CreateDirectory(profileParent);
            string profile = Path.Combine(
                profileParent,
                LastBearingProfileContract.ProfileName);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted = store.TryPersist(canonical);
            TestHarness.True(
                persisted.Succeeded,
                "upgraded profile persist: " + persisted.Code);
            LastBearingLoadResult loaded = store.TryLoad(payload =>
                LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
            TestHarness.True(
                loaded.Succeeded && loaded.CanonicalPayload != null,
                "upgraded profile load: " + loaded.Code);
            TestHarness.True(
                canonical.SequenceEqual(loaded.CanonicalPayload!),
                "upgraded save payload drifted");
            LastBearingDecodeResult decoded =
                LastBearingCanonicalCodec.TryDecode(
                    loaded.CanonicalPayload!);
            TestHarness.True(
                decoded.Succeeded && decoded.State != null,
                "upgraded save decode");
            TestHarness.Equal(
                RigUpgrade.PatchworkSkidPlate,
                decoded.State!.RigUpgrade,
                "restored rig upgrade");
            TestHarness.Equal(
                driver.View.ProjectedRoundTripConditionLossMilli,
                LastBearingReadModel.FromState(decoded.State)
                    .ProjectedRoundTripConditionLossMilli,
                "restored projected route loss");

            LastBearingState legacySource =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2407);
            byte[] legacyV4 =
                LastBearingCanonicalCodec.EncodeLegacyV4ForMigrationTests(
                    legacySource);
            TestHarness.Equal((byte)4, legacyV4[8], "v4 codec marker");
            LastBearingDecodeResult first =
                LastBearingCanonicalCodec.TryDecode(legacyV4);
            LastBearingDecodeResult second =
                LastBearingCanonicalCodec.TryDecode(legacyV4);
            TestHarness.True(
                first.Succeeded
                    && first.State != null
                    && second.Succeeded
                    && second.State != null,
                "v4 migration decode");
            TestHarness.Equal(
                LastBearingState.CurrentSchemaVersion,
                first.State!.SchemaVersion,
                "v4 migrated schema");
            TestHarness.Equal(
                LastBearingBalanceV1.Revision,
                first.State.BalanceRevision,
                "v4 migrated balance");
            TestHarness.Equal(
                RigUpgrade.None,
                first.State.RigUpgrade,
                "v4 rig upgrade default");
            TestHarness.True(
                legacyV4.SequenceEqual(
                    LastBearingCanonicalCodec
                        .EncodeLegacyV4ForMigrationTests(first.State)),
                "v4 canonical bytes changed");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(first.State),
                LastBearingCanonicalCodec.ComputeSha256(second.State!),
                "v4 migration was not deterministic");
        }

        private static LastBearingState CompleteCooperativeReturn(
            bool installUpgrade,
            int worldSeed)
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
                VehicleModule.WinchAssembly);
            AdvanceUntil(
                driver,
                model => model.PreparationPhase == PreparationPhase.Ready,
                drive: false,
                "road preparation");

            string transactionId = "tx:skid-plate:" + worldSeed;
            string fingerprint = "fp:skid-plate:" + worldSeed;
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
            if (driver.View.ExpeditionPhase == ExpeditionPhase.AtHome)
            {
                driver.Apply(sequence =>
                    new DepartExpeditionCommand(sequence));
            }

            AdvanceUntil(
                driver,
                model => model.IsDepotApproachRecoveryAvailable,
                drive: true,
                "depot recovery");
            driver.Apply(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            driver.Apply(sequence =>
                new ResolveDepotCommand(
                    sequence,
                    EncounterChoice.Cooperate));
            driver.Apply(sequence =>
                new LoadDepotRepairCargoCommand(sequence));
            driver.Apply(sequence =>
                new FreezeReturnPayloadCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            AdvanceUntil(
                driver,
                model => model.ExpeditionPhase == ExpeditionPhase.Returned,
                drive: true,
                "home return");
            driver.Apply(sequence =>
                new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    fingerprint));
            return driver.State;
        }

        private static void AdvanceUntil(
            CoreTestDriver driver,
            Func<LastBearingReadModel, bool> predicate,
            bool drive,
            string label)
        {
            var guard = 0;
            while (!predicate(driver.View) && guard < 1000)
            {
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

                guard++;
            }

            TestHarness.True(
                predicate(driver.View),
                label + " did not complete");
        }

        private static void AssertRejected(
            LastBearingState state,
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
                            new InstallRigUpgradeCommand(
                                state.NextCommandSequence,
                                RigUpgrade.PatchworkSkidPlate),
                        }),
                    label + " was accepted");
            TestHarness.Equal(expectedCode, error.Message, label + " code");
            TestHarness.True(
                before.SequenceEqual(
                    LastBearingCanonicalCodec.Encode(state)),
                label + " mutated state");
        }
    }
}
