#nullable enable

using System;
using System.IO;
using System.Linq;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class SaveAtomicTests
    {
        public static void Run(TestHarness harness, string repoRoot)
        {
            harness.Run("frozen generation and pointer vector", FrozenVector);
            harness.Run("four preparation and module checkpoints round trip", () =>
                PreparationModuleCheckpointsRoundTrip(repoRoot));
            harness.Run("atomic current and immediately preceding last-good", () =>
                CurrentAndLastGood(repoRoot));
            harness.Run("corrupt current recovers only verified last-good", () =>
                CorruptCurrentRecovery(repoRoot));
            harness.Run("corrupt current refuses a forked persist", () =>
                CorruptCurrentRefusesPersist(repoRoot));
            harness.Run("unknown current version refuses downgrade and persist", () =>
                UnknownCurrentVersionFailsClosed(repoRoot));
            harness.Run("profile load ignores sibling and staging decoys", () =>
                DecoysAreNotDiscoverable(repoRoot));
            harness.Run("faulted publication remains recoverable and retry converges", () =>
                FaultedPublicationConverges(repoRoot));
            harness.Run("scratch recovery cannot remove a published generation", () =>
                ScratchRecoveryRejectsPublishedGeneration(repoRoot));
            harness.Run("load does not create a missing profile", () =>
                MissingLoadIsReadOnly(repoRoot));
        }

        private static void FrozenVector()
        {
            byte[] payload = { 0x00, 0x01, 0xfe, 0xff };
            LastBearingGenerationRecord generation =
                LastBearingGenerationCodec.EncodeGeneration(1, payload);
            TestHarness.Equal(
                "gen-00000000000000000001-504fed45000abc9524158b12cb8e1fc14f57d05891be9ca3aa5f9a1746d6d233.lbg",
                generation.FileName.Value,
                "generation filename");
            TestHarness.Equal(
                "414c504c4247303101001300010000000000000004000000c5dbae22661af6db18a1f676db82a7ef7de46d27c3a263a872f00478b0d99fc46c6173742d62656172696e672d6465762d76310001feff",
                ToHex(generation.EncodedBytes),
                "generation bytes");
            LastBearingPointerRecord pointer =
                LastBearingGenerationCodec.EncodePointer(generation);
            TestHarness.Equal(
                "414c504c425030310100130001000000000000005d00504fed45000abc9524158b12cb8e1fc14f57d05891be9ca3aa5f9a1746d6d2336c6173742d62656172696e672d6465762d763167656e2d30303030303030303030303030303030303030312d353034666564343530303061626339353234313538623132636238653166633134663537643035383931626539636133616135663961313734366436643233332e6c6267",
                ToHex(pointer.EncodedBytes),
                "pointer bytes");
        }

        private static void PreparationModuleCheckpointsRoundTrip(string repoRoot)
        {
            foreach (PreparationChoice choice in new[]
            {
                PreparationChoice.WorkshopPush,
                PreparationChoice.CivicBuffer,
            })
            {
                foreach (VehicleModule module in new[]
                {
                    VehicleModule.WinchAssembly,
                    VehicleModule.SealedRangeTank,
                })
                {
                    var driver = new CoreTestDriver(ColonyComposition.HumanOnly, 2012);
                    driver.StartPreparation(
                        ResidentRoster.HumanResidentId,
                        choice,
                        module);
                    int guard = 0;
                    while (driver.View.PreparationPhase != PreparationPhase.Ready &&
                           guard < 1000)
                    {
                        driver.Advance(1);
                        guard++;
                    }

                    TestHarness.Equal(
                        PreparationPhase.Ready,
                        driver.View.PreparationPhase,
                        choice + "/" + module + " preparation checkpoint");
                    byte[] canonical = LastBearingCanonicalCodec.Encode(driver.State);
                    string profile = FreshProfile(
                        repoRoot,
                        "checkpoint-" + choice + "-" + module);
                    LastBearingProfileStore store =
                        LastBearingProfileStore.OpenFixedProfileDirectory(profile);
                    LastBearingPersistResult persisted = store.TryPersist(canonical);
                    TestHarness.True(
                        persisted.Succeeded,
                        choice + "/" + module + " checkpoint persist failed");
                    LastBearingLoadResult loaded = store.TryLoad(payload =>
                        LastBearingCanonicalCodec.TryDecode(payload).Succeeded);
                    TestHarness.True(
                        loaded.Succeeded && loaded.CanonicalPayload != null,
                        choice + "/" + module + " checkpoint load failed");
                    TestHarness.True(
                        canonical.SequenceEqual(loaded.CanonicalPayload!),
                        choice + "/" + module + " canonical bytes changed");
                    LastBearingDecodeResult decoded =
                        LastBearingCanonicalCodec.TryDecode(loaded.CanonicalPayload!);
                    TestHarness.True(
                        decoded.Succeeded && decoded.State != null,
                        choice + "/" + module + " canonical decode failed");
                    LastBearingReadModel restored =
                        LastBearingReadModel.FromState(decoded.State!);
                    TestHarness.Equal(choice, restored.PreparationChoice, "restored preparation");
                    TestHarness.Equal(module, restored.VehicleModule, "restored module");
                }
            }
        }

        private static void CurrentAndLastGood(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "current-last-good");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            byte[] first = { 0x10, 0x11 };
            byte[] second = { 0x20, 0x21, 0x22 };
            byte[] third = { 0x30, 0x31, 0x32, 0x33 };
            TestHarness.Equal(1UL, Persist(store, first).Generation, "first generation");
            TestHarness.Equal(2UL, Persist(store, second).Generation, "second generation");
            TestHarness.Equal(3UL, Persist(store, third).Generation, "third generation");
            LastBearingPersistResult retry = Persist(store, third);
            TestHarness.True(retry.AlreadyCurrent, "idempotent retry wrote a generation");
            TestHarness.Equal(3UL, retry.Generation, "idempotent retry generation");
            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(loaded.Succeeded && !loaded.FromLastGood, "current load failed");
            TestHarness.True(third.SequenceEqual(loaded.CanonicalPayload!), "current payload differs");
        }

        private static void CorruptCurrentRecovery(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "current-recovery");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            byte[] first = { 0x41, 0x42, 0x43 };
            byte[] second = { 0x51, 0x52, 0x53 };
            Persist(store, first);
            Persist(store, second);
            Mutate(Path.Combine(profile, LastBearingProfileContract.CurrentPointerName));
            LastBearingLoadResult recovered = store.TryLoad(_ => true);
            TestHarness.True(recovered.Succeeded && recovered.FromLastGood, "last-good did not recover");
            TestHarness.Equal(
                LastBearingSaveCodes.RecoveredLastGood,
                recovered.Code,
                "recovery code");
            TestHarness.True(first.SequenceEqual(recovered.CanonicalPayload!), "recovered wrong generation");

            Mutate(Path.Combine(profile, LastBearingProfileContract.LastGoodPointerName));
            LastBearingLoadResult refused = store.TryLoad(_ => true);
            TestHarness.True(!refused.Succeeded, "corrupt current and last-good loaded");
            TestHarness.Equal(LastBearingSaveCodes.BothCorrupt, refused.Code, "double-corrupt code");
        }

        private static void CorruptCurrentRefusesPersist(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "fork-refusal");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            Persist(store, new byte[] { 0x61 });
            Persist(store, new byte[] { 0x62 });
            Mutate(Path.Combine(profile, LastBearingProfileContract.CurrentPointerName));
            string[] before = Directory.GetFiles(profile).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            LastBearingPersistResult result = store.TryPersist(new byte[] { 0x63 });
            string[] after = Directory.GetFiles(profile).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            TestHarness.True(!result.Succeeded, "persist forked from corrupt current");
            TestHarness.True(before.SequenceEqual(after), "corrupt-current refusal changed namespace");
        }

        private static void UnknownCurrentVersionFailsClosed(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "unknown-version-refusal");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            Persist(store, new byte[] { 0x91 });
            Persist(store, new byte[] { 0x92 });
            string currentPath = Path.Combine(
                profile,
                LastBearingProfileContract.CurrentPointerName);
            byte[] unknownPointer = File.ReadAllBytes(currentPath);
            unknownPointer[8] = 0x02;
            unknownPointer[9] = 0x00;
            File.WriteAllBytes(currentPath, unknownPointer);

            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(!loaded.Succeeded, "unknown current version downgraded to last-good");
            TestHarness.Equal(
                LastBearingSaveCodes.UnknownVersion,
                loaded.Code,
                "unknown-version load code");
            LastBearingPersistResult persisted = store.TryPersist(new byte[] { 0x93 });
            TestHarness.True(!persisted.Succeeded, "unknown current version accepted a persist");
            TestHarness.Equal(
                LastBearingSaveCodes.UnknownVersion,
                persisted.Code,
                "unknown-version persist code");
            TestHarness.True(
                unknownPointer.SequenceEqual(File.ReadAllBytes(currentPath)),
                "unknown current pointer was rewritten");
        }

        private static void DecoysAreNotDiscoverable(string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "decoy-refusal");
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            byte[] canonical = { 0x71, 0x72, 0x73 };
            Persist(store, canonical);
            string orphan = Path.Combine(profile, "orphan.stage");
            string sibling = Path.Combine(profile, "valid-looking-sibling.lbg");
            File.WriteAllBytes(orphan, new byte[] { 0xde, 0xad });
            File.WriteAllBytes(sibling, new byte[] { 0xbe, 0xef });
            DateTime orphanWrite = File.GetLastWriteTimeUtc(orphan);
            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(loaded.Succeeded, "decoys prevented current load");
            TestHarness.True(canonical.SequenceEqual(loaded.CanonicalPayload!), "decoy was discovered");
            TestHarness.True(File.Exists(orphan) && File.Exists(sibling), "load rewrote or cleaned siblings");
            TestHarness.Equal(orphanWrite, File.GetLastWriteTimeUtc(orphan), "load rewrote a decoy");
        }

        private static void MissingLoadIsReadOnly(string repoRoot)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/save-atomic/missing");
            Recreate(parent);
            string profile = Path.Combine(parent, LastBearingProfileContract.ProfileName);
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(!loaded.Succeeded, "missing profile loaded");
            TestHarness.Equal(LastBearingSaveCodes.NoProfile, loaded.Code, "missing profile code");
            TestHarness.True(!Directory.Exists(profile), "read-only load created a profile");
        }

        private static void FaultedPublicationConverges(string repoRoot)
        {
            foreach (SaveFaultPoint point in new[]
            {
                SaveFaultPoint.AfterGenerationStageDurableClose,
                SaveFaultPoint.AfterGenerationPublication,
                SaveFaultPoint.AfterLastGoodPublication,
                SaveFaultPoint.AfterCurrentPublication,
                SaveFaultPoint.PartialGenerationStage,
                SaveFaultPoint.PartialLastGoodPointerStage,
                SaveFaultPoint.PartialCurrentPointerStage,
            })
            {
                string profile = FreshProfile(repoRoot, "fault-" + point);
                LastBearingProfileStore normal =
                    LastBearingProfileStore.OpenFixedProfileDirectory(profile);
                byte[] first = { 0x81, 0x01 };
                byte[] second = { 0x82, 0x02 };
                byte[] attempted = { 0x83, 0x03 };
                Persist(normal, first);
                Persist(normal, second);

                var operations = new FaultInjectingFileOperations(
                    LastBearingFileOperations.Instance,
                    point);
                LastBearingProfileStore faulting =
                    LastBearingProfileStore.OpenFixedProfileDirectoryForTests(
                        profile,
                        operations);
                LastBearingPersistResult interrupted = faulting.TryPersist(attempted);
                TestHarness.True(operations.Fired, "fault point did not fire: " + point);
                TestHarness.True(!interrupted.Succeeded, "faulted persist reported success");

                LastBearingLoadResult visible = normal.TryLoad(_ => true);
                TestHarness.True(visible.Succeeded, "fault left no valid visible generation");
                if (IsPartialStageFault(point))
                {
                    TestHarness.True(
                        second.SequenceEqual(visible.CanonicalPayload!),
                        "partial stage became visible: " + point);
                    TestHarness.True(
                        Directory.GetFiles(profile)
                            .Select(Path.GetFileName)
                            .Any(name => name != null &&
                                name.StartsWith(".stage-", StringComparison.Ordinal)),
                        "partial stage residue was not created: " + point);
                }

                TestHarness.True(
                    second.SequenceEqual(visible.CanonicalPayload!) ||
                    attempted.SequenceEqual(visible.CanonicalPayload!),
                    "fault exposed a partial or unrelated payload");

                LastBearingPersistResult retried = normal.TryPersist(attempted);
                TestHarness.True(retried.Succeeded, "fault retry did not converge");
                LastBearingLoadResult converged = normal.TryLoad(_ => true);
                TestHarness.True(
                    converged.Succeeded && attempted.SequenceEqual(converged.CanonicalPayload!),
                    "fault retry did not publish attempted payload");
                TestHarness.True(
                    !Directory.GetFiles(profile)
                        .Select(Path.GetFileName)
                        .Any(name => name != null &&
                            name.StartsWith(".stage-", StringComparison.Ordinal)),
                    "retry left scratch stage residue: " + point);
            }
        }

        private static bool IsPartialStageFault(SaveFaultPoint point)
        {
            return point == SaveFaultPoint.PartialGenerationStage ||
                point == SaveFaultPoint.PartialLastGoodPointerStage ||
                point == SaveFaultPoint.PartialCurrentPointerStage;
        }

        private static void ScratchRecoveryRejectsPublishedGeneration(
            string repoRoot)
        {
            string profile = FreshProfile(repoRoot, "published-generation-retention");
            byte[] payload = { 0xa1, 0xb2, 0xc3 };
            LastBearingProfileStore store =
                LastBearingProfileStore.OpenFixedProfileDirectory(profile);
            LastBearingPersistResult persisted = Persist(store, payload);
            LastBearingGenerationRecord generation =
                LastBearingGenerationCodec.EncodeGeneration(
                    persisted.Generation,
                    payload);
            LastBearingDirectoryOpenResult opened =
                LastBearingFileOperations.Instance.TryOpenProfileDirectory(
                    profile,
                    createFixedTerminalIfMissing: false);
            TestHarness.True(
                opened.Status == LastBearingOperationStatus.Success &&
                    opened.Directory != null,
                "published-generation profile did not open");
            using (ILastBearingProfileDirectory directory = opened.Directory!)
            {
                LastBearingFileResult removal =
                    directory.TryRemoveScratchStage(generation.FileName);
                TestHarness.Equal(
                    LastBearingOperationStatus.ConfinementFailure,
                    removal.Status,
                    "published generation entered scratch deletion path");
            }

            LastBearingLoadResult loaded = store.TryLoad(_ => true);
            TestHarness.True(
                loaded.Succeeded && payload.SequenceEqual(loaded.CanonicalPayload!),
                "published generation changed after rejected scratch removal");
        }

        private static LastBearingPersistResult Persist(
            LastBearingProfileStore store,
            byte[] payload)
        {
            LastBearingPersistResult result = store.TryPersist(payload);
            TestHarness.True(result.Succeeded, "persist failed: " + result.Code);
            return result;
        }

        private static string FreshProfile(string repoRoot, string caseName)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/save-atomic",
                caseName);
            Recreate(parent);
            return Path.Combine(parent, LastBearingProfileContract.ProfileName);
        }

        private static void Recreate(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            Directory.CreateDirectory(path);
        }

        private static void Mutate(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bytes[bytes.Length / 2] ^= 0x01;
            File.WriteAllBytes(path, bytes);
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
