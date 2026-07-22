#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Editor;
using NUnit.Framework;
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
