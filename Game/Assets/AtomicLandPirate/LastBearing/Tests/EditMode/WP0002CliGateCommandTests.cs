#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Editor;
using NUnit.Framework;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class WP0002CliGateCommandTests
    {
        private const string CliCommandAttributeName =
            "Unity.Pipeline.Commands.CliCommandAttribute";
        private const string CliArgAttributeName =
            "Unity.Pipeline.Commands.CliArgAttribute";

        private static readonly IReadOnlyDictionary<string, string>
            ExpectedCommandToGate = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["wp0002_asset_refresh"] = "AssetRefreshGate",
                ["wp0002_editmode_tests"] = "EditModeGate",
                ["wp0002_playmode_tests"] = "PlayModeGate",
                ["wp0002_technical_capture"] = "TechnicalCaptureGate",
                ["wp0002_native_build"] = "NativeBuildGate",
                ["wp0002_native_performance_start"] =
                    "NativePerformanceStartGate",
                ["wp0002_native_performance_collect"] =
                    "NativePerformanceCollectGate"
            };

        [Test]
        public void CommandsAreUniqueStaticMainThreadEditorMethods()
        {
            MethodInfo[] methods = typeof(WP0002CliGateCommands).GetMethods(
                    BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(methods, Has.Length.EqualTo(ExpectedCommandToGate.Count));
            var discoveredNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (MethodInfo method in methods)
            {
                Assert.That(method.IsStatic, Is.True, method.Name);
                Assert.That(method.ReturnType, Is.EqualTo(typeof(string)), method.Name);

                CustomAttributeData command = SingleAttribute(
                    method.CustomAttributes,
                    CliCommandAttributeName,
                    method.Name);
                string commandName = (string)command.ConstructorArguments[0].Value!;
                Assert.That(
                    ExpectedCommandToGate.ContainsKey(commandName),
                    Is.True,
                    method.Name);
                Assert.That(discoveredNames.Add(commandName), Is.True, commandName);
                Assert.That(
                    NamedBoolean(command, "MainThreadRequired"),
                    Is.True,
                    commandName);
                Assert.That(
                    NamedBoolean(command, "RuntimeOnly"),
                    Is.False,
                    commandName);

                ParameterInfo[] parameters = method.GetParameters();
                Assert.That(parameters, Has.Length.EqualTo(1), commandName);
                Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(string)));
                Assert.That(parameters[0].Name, Is.EqualTo("expectedSourceSha256"));
                CustomAttributeData argument = SingleAttribute(
                    parameters[0].CustomAttributes,
                    CliArgAttributeName,
                    commandName);
                Assert.That(
                    argument.ConstructorArguments[0].Value,
                    Is.EqualTo("expected_source_sha256"),
                    commandName);
                Assert.That(NamedBoolean(argument, "Required"), Is.True, commandName);
            }

            Assert.That(discoveredNames, Is.EquivalentTo(ExpectedCommandToGate.Keys));
        }

        [Test]
        public void SettingsDisableBroadPackageAutoStart()
        {
            EditorPipelineManager settings = EditorPipelineManager.Load();

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.AutoStart, Is.False);
            Assert.That(settings.Port, Is.Zero);
            Assert.That(settings.WatchdogEnabled, Is.False);
            Assert.That(settings.WatchdogIntervalSeconds, Is.EqualTo(5));
            Assert.That(settings.LogRequestsResponses, Is.False);
        }

        [Test]
        public void RegistryContainsOnlyTheSevenFixedWrappers()
        {
            CommandInfo[] commands = CommandRegistry.DiscoverCommands()
                .OrderBy(command => command.Name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(commands, Has.Length.EqualTo(ExpectedCommandToGate.Count));
            Assert.That(
                commands.Select(command => command.Name),
                Is.EquivalentTo(ExpectedCommandToGate.Keys));
            Assert.That(commands.Select(command => command.Name),
                Does.Not.Contain("eval"));
            Assert.That(commands.Select(command => command.Name),
                Does.Not.Contain("eval_file"));
            Assert.That(commands.Any(command =>
                    command.Name.StartsWith("package_", StringComparison.Ordinal)),
                Is.False);

            foreach (CommandInfo command in commands)
            {
                Assert.That(
                    command.Method.DeclaringType,
                    Is.EqualTo(typeof(WP0002CliGateCommands)),
                    command.Name);
                Assert.That(command.MainThreadRequired, Is.True, command.Name);
                Assert.That(command.RuntimeOnly, Is.False, command.Name);
                Assert.That(command.Parameters, Has.Count.EqualTo(1), command.Name);
                Assert.That(
                    command.Parameters[0].Name,
                    Is.EqualTo("expected_source_sha256"),
                    command.Name);
                Assert.That(command.Parameters[0].Required, Is.True, command.Name);
                Assert.That(
                    command.Parameters[0].ParameterType,
                    Is.EqualTo(typeof(string)),
                    command.Name);
            }
        }

        [Test]
        public void BootstrapDefersAssetValidationUntilDomainReloadPostprocess()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AtomicLandPirate/LastBearing/Editor/WP0002PipelineBootstrap.cs"));
            string earlyGuard = Segment(
                source,
                "static WP0002PipelineBootstrap()",
                "internal static void ConfigureAndStartAfterDomainReload()");
            string configure = Segment(
                source,
                "internal static void ConfigureAndStartAfterDomainReload()",
                "private static void ValidateCanonicalProjectRoot()");

            StringAssert.Contains("[InitializeOnLoad]", source);
            StringAssert.Contains("Application.isBatchMode", earlyGuard);
            StringAssert.Contains(
                "ProcessService.level == ProcessLevel.Main",
                earlyGuard);
            StringAssert.Contains(
                "RuntimeHelpers.RunClassConstructor(",
                earlyGuard);
            StringAssert.Contains(
                "typeof(PipelineServerStartup).TypeHandle",
                earlyGuard);
            StringAssert.Contains(
                "PipelineServerStartup.StopServer();",
                earlyGuard);
            int earlyStop = earlyGuard.IndexOf(
                "PipelineServerStartup.StopServer();",
                StringComparison.Ordinal);
            int earlyDisable = earlyGuard.IndexOf(
                "AutoTickCommand.SetAutoTick(false);",
                StringComparison.Ordinal);
            Assert.That(earlyDisable, Is.GreaterThan(earlyStop));
            StringAssert.DoesNotContain("AssetDatabase", earlyGuard);
            StringAssert.DoesNotContain("Path.", earlyGuard);
            StringAssert.DoesNotContain("CommandRegistry", earlyGuard);

            StringAssert.Contains(
                "AssetDatabase.IsAssetImportWorkerProcess()",
                configure);
            StringAssert.Contains(
                "/Users/sasha/Documents/Sasha the Atomic Land Pirate/Development/",
                source);
            StringAssert.Contains(
                "sasha-the-land-pirate/Game",
                source);
            StringAssert.Contains(
                "Path.Combine(Application.dataPath, \"..\")",
                source);
            StringAssert.Contains(
                "AssetDatabase.FindAssets(\"t:EditorPipelineManager\")",
                source);
            StringAssert.Contains(
                "Assets/Settings/Pipeline/EditorPipelineManager.asset",
                source);
            StringAssert.Contains(
                "a615acb88f7e4559a2831dad2ac14921",
                source);
            StringAssert.Contains("settings.AutoStart ||", source);
            StringAssert.Contains(
                "private static void OnPostprocessAllAssets(",
                source);
            StringAssert.Contains(
                "bool didDomainReload",
                source);
            StringAssert.Contains(
                "if (!didDomainReload || s_Attempted)",
                source);
            StringAssert.Contains("s_Attempted = true;", source);
            StringAssert.Contains(
                "WP0002PipelineBootstrap.ConfigureAndStartAfterDomainReload();",
                source);
            StringAssert.Contains(
                "CommandRegistry.SetDiscovery(discovery);",
                configure);
            StringAssert.Contains(
                "typeof(T) == typeof(CliCommandAttribute)",
                source);
            StringAssert.Contains("Array.Empty<MethodInfo>()", source);
            StringAssert.DoesNotContain("TypeCacheCommandDiscovery", source);
            StringAssert.DoesNotContain("SetDiscovery(null", source);
            Assert.That(
                CountOccurrences(
                    source,
                    "AutoTickCommand.SetAutoTick(false);"),
                Is.EqualTo(2));

            int validateProject = configure.IndexOf(
                "ValidateCanonicalProjectRoot();",
                StringComparison.Ordinal);
            int rejectPreexistingServer = configure.IndexOf(
                "FailIfServerRunningBeforeAssetValidation();",
                StringComparison.Ordinal);
            int loadSettings = configure.IndexOf(
                "LoadExactSettingsAsset();",
                StringComparison.Ordinal);
            int validateSettings = configure.IndexOf(
                "ValidateSettings(settings);",
                StringComparison.Ordinal);
            int installFilter = configure.IndexOf(
                "CommandRegistry.SetDiscovery(discovery);",
                StringComparison.Ordinal);
            int validateRegistry = configure.IndexOf(
                "ValidateRegisteredCommands(CommandRegistry.DiscoverCommands());",
                StringComparison.Ordinal);
            int startServer = configure.IndexOf(
                "PipelineServerStartup.EnsureServerStarted();",
                StringComparison.Ordinal);
            Assert.That(rejectPreexistingServer, Is.GreaterThanOrEqualTo(0));
            Assert.That(validateProject, Is.GreaterThan(rejectPreexistingServer));
            Assert.That(loadSettings, Is.GreaterThan(validateProject));
            Assert.That(validateSettings, Is.GreaterThan(loadSettings));
            Assert.That(installFilter, Is.GreaterThan(validateSettings));
            Assert.That(validateRegistry, Is.GreaterThan(installFilter));
            Assert.That(startServer, Is.GreaterThan(validateRegistry));

            int postprocess = source.IndexOf(
                "private static void OnPostprocessAllAssets(",
                StringComparison.Ordinal);
            int domainGuard = source.IndexOf(
                "if (!didDomainReload || s_Attempted)",
                postprocess,
                StringComparison.Ordinal);
            int attempted = source.IndexOf(
                "s_Attempted = true;",
                domainGuard,
                StringComparison.Ordinal);
            int configureCall = source.IndexOf(
                "WP0002PipelineBootstrap.ConfigureAndStartAfterDomainReload();",
                attempted,
                StringComparison.Ordinal);
            Assert.That(postprocess, Is.GreaterThanOrEqualTo(0));
            Assert.That(domainGuard, Is.GreaterThan(postprocess));
            Assert.That(attempted, Is.GreaterThan(domainGuard));
            Assert.That(configureCall, Is.GreaterThan(attempted));

            string preAssetCleanup = Segment(
                source,
                "private static void FailIfServerRunningBeforeAssetValidation()",
                "private static EditorPipelineManager LoadExactSettingsAsset()");
            int cleanupStop = preAssetCleanup.IndexOf(
                "PipelineServerStartup.StopServer();",
                StringComparison.Ordinal);
            int cleanupDisable = preAssetCleanup.IndexOf(
                "AutoTickCommand.SetAutoTick(false);",
                StringComparison.Ordinal);
            Assert.That(cleanupStop, Is.GreaterThanOrEqualTo(0));
            Assert.That(cleanupDisable, Is.GreaterThan(cleanupStop));
        }

        [Test]
        public void CommandsDelegateOnlyToTheirFixedDispatcherGate()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AtomicLandPirate/LastBearing/Editor/WP0002CliGateCommands.cs"));

            foreach (KeyValuePair<string, string> pair in ExpectedCommandToGate)
            {
                string commandSegment = SegmentForCommand(source, pair.Key);
                StringAssert.Contains(
                    "WP0002GateDispatcher." + pair.Value,
                    commandSegment,
                    pair.Key);
                Assert.That(
                    CountOccurrences(commandSegment, "WP0002GateDispatcher.Dispatch("),
                    Is.EqualTo(1),
                    pair.Key);
                Assert.That(
                    CountOccurrences(commandSegment, "expectedSourceSha256"),
                    Is.EqualTo(2),
                    pair.Key);
                foreach (string otherGate in ExpectedCommandToGate.Values
                    .Where(gate => !string.Equals(
                        gate,
                        pair.Value,
                        StringComparison.Ordinal)))
                {
                    StringAssert.DoesNotContain(
                        "WP0002GateDispatcher." + otherGate,
                        commandSegment,
                        pair.Key);
                }
            }

            StringAssert.DoesNotContain("string gateId", source);
            StringAssert.DoesNotContain("\"gate_id\"", source);
            StringAssert.DoesNotContain("\"eval\"", source);
            StringAssert.DoesNotContain("System.IO", source);
        }

        private static CustomAttributeData SingleAttribute(
            IEnumerable<CustomAttributeData> attributes,
            string attributeName,
            string context)
        {
            CustomAttributeData[] matches = attributes
                .Where(attribute => string.Equals(
                    attribute.AttributeType.FullName,
                    attributeName,
                    StringComparison.Ordinal))
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), context);
            return matches[0];
        }

        private static bool NamedBoolean(
            CustomAttributeData attribute,
            string memberName)
        {
            CustomAttributeNamedArgument argument = attribute.NamedArguments.Single(
                candidate => string.Equals(
                    candidate.MemberName,
                    memberName,
                    StringComparison.Ordinal));
            return (bool)argument.TypedValue.Value!;
        }

        private static string SegmentForCommand(string source, string commandName)
        {
            string marker = "\"" + commandName + "\"";
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), commandName);
            int next = source.IndexOf("[CliCommand(", start, StringComparison.Ordinal);
            return next < 0
                ? source.Substring(start)
                : source.Substring(start, next - start);
        }

        private static string Segment(
            string source,
            string startToken,
            string endToken)
        {
            int start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startToken);
            int end = source.IndexOf(
                endToken,
                start + startToken.Length,
                StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endToken);
            return source.Substring(start, end - start);
        }

        private static int CountOccurrences(string source, string token)
        {
            var count = 0;
            var offset = 0;
            while (offset <= source.Length - token.Length)
            {
                int match = source.IndexOf(token, offset, StringComparison.Ordinal);
                if (match < 0)
                {
                    break;
                }

                count++;
                offset = match + token.Length;
            }

            return count;
        }
    }
}
