#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class SaveBoundaryTests
    {
        public static void Run(TestHarness harness, string repoRoot)
        {
            harness.Run("fixed Unity save boundary source and API", () =>
                AdapterSourceContract.Verify(repoRoot));
            harness.Run("fixed Unity save boundary runtime", () =>
                RuntimeBoundary(repoRoot));
            harness.Run("SaveContracts public API has no discovery seam", PublicApiBoundary);
            harness.Run("invalid profile roots fail closed", () => InvalidRootsFail(repoRoot));
        }

        private static void RuntimeBoundary(string repoRoot)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/save-boundary/runtime");
            Recreate(parent);
            string sentinel = Path.Combine(parent, "sibling-sentinel.bin");
            byte[] sentinelBytes = { 0x53, 0x41, 0x53, 0x48, 0x41 };
            File.WriteAllBytes(sentinel, sentinelBytes);

            Application.SetPersistentDataPathForTests(parent);
            LastBearingSaveAdapter adapter = LastBearingSaveAdapter.Create();
            TestHarness.Equal(
                1,
                Application.PersistentDataPathReadCount,
                "persistentDataPath read count");
            LastBearingState state = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.HumanOnly,
                2011);
            LastBearingPersistResult persisted = adapter.TryPersist(state);
            TestHarness.True(persisted.Succeeded, "adapter persist failed: " + persisted.Code);
            LastBearingAdapterLoadResult loaded = adapter.TryLoad();
            TestHarness.True(loaded.Succeeded && loaded.State != null, "adapter load failed");
            TestHarness.Equal(
                LastBearingCanonicalCodec.ComputeSha256(state),
                LastBearingCanonicalCodec.ComputeSha256(loaded.State!),
                "adapter canonical round trip");
            TestHarness.True(
                Directory.Exists(Path.Combine(parent, LastBearingProfileContract.ProfileName)),
                "fixed child was not created");
            TestHarness.True(
                sentinelBytes.SequenceEqual(File.ReadAllBytes(sentinel)),
                "sibling sentinel changed");
        }

        private static void PublicApiBoundary()
        {
            Assembly saveAssembly = typeof(LastBearingProfileStore).Assembly;
            foreach (Type type in saveAssembly.GetExportedTypes())
            {
                string lowered = type.Name.ToLowerInvariant();
                TestHarness.True(
                    !lowered.Contains("fileoperations", StringComparison.Ordinal) &&
                    !lowered.Contains("filesystem", StringComparison.Ordinal),
                    "public file operations seam " + type.FullName);
                foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    string name = member.Name.ToLowerInvariant();
                    TestHarness.True(
                        !name.Contains("enumerate", StringComparison.Ordinal) &&
                        !name.Contains("listfiles", StringComparison.Ordinal) &&
                        !name.Contains("migrate", StringComparison.Ordinal) &&
                        !name.Contains("repair", StringComparison.Ordinal),
                        "public persistence discovery seam " + member.Name);
                }
            }

            MethodInfo[] factories = typeof(LastBearingProfileStore).GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => string.Equals(
                    method.Name,
                    "OpenFixedProfileDirectory",
                    StringComparison.Ordinal))
                .ToArray();
            TestHarness.Equal(1, factories.Length, "profile store factory count");
            ParameterInfo[] parameters = factories[0].GetParameters();
            TestHarness.Equal(1, parameters.Length, "profile store factory parameter count");
            TestHarness.Equal(typeof(string), parameters[0].ParameterType, "profile store path type");
        }

        private static void InvalidRootsFail(string repoRoot)
        {
            string parent = Path.Combine(
                repoRoot,
                "BuildArtifacts/WP-0002/local-only/save-boundary/invalid");
            Recreate(parent);
            string[] invalid =
            {
                Path.Combine(parent, "wrong-profile"),
                Path.Combine(parent, LastBearingProfileContract.ProfileName) + Path.DirectorySeparatorChar,
                Path.Combine(parent, "..", LastBearingProfileContract.ProfileName),
                LastBearingProfileContract.ProfileName,
            };
            foreach (string path in invalid)
            {
                bool rejected = false;
                try
                {
                    LastBearingProfileStore store =
                        LastBearingProfileStore.OpenFixedProfileDirectory(path);
                    LastBearingLoadResult loaded = store.TryLoad(_ => true);
                    rejected = !loaded.Succeeded &&
                        string.Equals(
                            loaded.Code,
                            LastBearingSaveCodes.ConfinementFailure,
                            StringComparison.Ordinal);
                }
                catch (ArgumentException)
                {
                    rejected = true;
                }

                TestHarness.True(rejected, "invalid profile root was accepted: " + path);
            }
        }

        private static void Recreate(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            Directory.CreateDirectory(path);
        }
    }
}
