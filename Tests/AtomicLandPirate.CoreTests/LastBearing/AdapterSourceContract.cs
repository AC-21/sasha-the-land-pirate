#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class AdapterSourceContract
    {
        private static readonly string[] ForbiddenAdapterTokens =
        {
            "last-bearing-dev-v1",
            "Environment.",
            "PlayerPrefs",
            "System.Reflection",
            "DllImport",
            "unsafe",
            "#if",
            "File.",
            "Directory.",
            "FileStream",
            "StreamWriter",
            "Enumerate",
            "Process.",
            "HttpClient",
        };

        public static void Verify(string repoRoot)
        {
            string runtimeRoot = Path.Combine(
                repoRoot,
                "Game/Assets/AtomicLandPirate/LastBearing/Runtime");
            string adapterPath = Path.Combine(runtimeRoot, "LastBearingSaveAdapter.cs");
            string source = File.ReadAllText(adapterPath);
            TestHarness.Equal(1, Count(source, "Application.persistentDataPath"), "persistent root read");
            TestHarness.Equal(1, Count(source, "Path.Combine"), "path combine call");
            TestHarness.Equal(
                1,
                Count(source, "LastBearingProfileContract.ProfileName"),
                "fixed profile name use");
            TestHarness.Equal(
                1,
                Count(source, "LastBearingProfileStore.OpenFixedProfileDirectory"),
                "fixed profile store use");
            foreach (string token in ForbiddenAdapterTokens)
            {
                TestHarness.True(
                    source.IndexOf(token, StringComparison.Ordinal) < 0,
                    "adapter contains forbidden token " + token);
            }

            foreach (string path in Directory.GetFiles(runtimeRoot, "*.cs"))
            {
                if (string.Equals(path, adapterPath, StringComparison.Ordinal))
                {
                    continue;
                }

                string sibling = File.ReadAllText(path);
                TestHarness.True(
                    sibling.IndexOf("AtomicLandPirate.Save.LastBearing", StringComparison.Ordinal) < 0,
                    Path.GetFileName(path) + " bypasses the adapter");
                TestHarness.True(
                    sibling.IndexOf("System.IO", StringComparison.Ordinal) < 0,
                    Path.GetFileName(path) + " imports filesystem APIs");
            }

            Type adapter = typeof(LastBearingSaveAdapter);
            ConstructorInfo[] exposedConstructors = adapter.GetConstructors(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            TestHarness.True(
                exposedConstructors.All(constructor => constructor.IsPrivate),
                "adapter exposes a constructor");
            MethodInfo[] factories = adapter.GetMethods(
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => string.Equals(method.Name, "Create", StringComparison.Ordinal))
                .ToArray();
            TestHarness.Equal(1, factories.Length, "adapter factory count");
            TestHarness.Equal(0, factories[0].GetParameters().Length, "adapter factory parameters");

            PropertyInfo[] publicProperties = adapter.GetProperties(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
            foreach (PropertyInfo property in publicProperties)
            {
                string name = property.Name.ToLowerInvariant();
                TestHarness.True(
                    !name.Contains("path", StringComparison.Ordinal) &&
                    !name.Contains("root", StringComparison.Ordinal) &&
                    !name.Contains("store", StringComparison.Ordinal) &&
                    !name.Contains("profile", StringComparison.Ordinal),
                    "adapter exposes persistence internals");
            }
        }

        private static int Count(string source, string value)
        {
            int count = 0;
            int offset = 0;
            while (true)
            {
                int match = source.IndexOf(value, offset, StringComparison.Ordinal);
                if (match < 0)
                {
                    return count;
                }

                count++;
                offset = match + value.Length;
            }
        }
    }
}
