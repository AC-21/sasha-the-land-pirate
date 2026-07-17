#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AtomicLandPirate.LastBearingTests
{
    internal sealed class ScenarioPreflightResult
    {
        public ScenarioPreflightResult(
            ScenarioPin pin,
            int commandCount,
            IReadOnlyList<string> caseIds,
            int worldSeed,
            int durationTicks,
            IReadOnlyList<string> oracleIds)
        {
            Pin = pin;
            CommandCount = commandCount;
            CaseIds = caseIds;
            WorldSeed = worldSeed;
            DurationTicks = durationTicks;
            OracleIds = oracleIds;
        }

        public ScenarioPin Pin { get; }
        public int CommandCount { get; }
        public IReadOnlyList<string> CaseIds { get; }
        public int WorldSeed { get; }
        public int DurationTicks { get; }
        public IReadOnlyList<string> OracleIds { get; }
    }

    internal static class ScenarioIntegrityPreflight
    {
        private static readonly HashSet<string> AllowedCommands =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "InspectCityNeed", "InspectNeed", "AssignResident",
                "PlaceOrActivateBuilding", "SetCivicPriority", "SelectPreparation",
                "InstallVehicleModule", "CommitExpedition", "DebitCityManifest",
                "DepartExpedition", "TraverseRoute", "TraverseSelectedRoute",
                "AdvanceRoute", "AdvanceRoadTick", "ResolveDepot", "ResolveEncounter",
                "FreezeReturnPayload", "ReturnPayload", "ReturnExpedition",
                "CreditCityReturn", "AdvanceFactionIntent", "SetClockPolicy",
                "RequestPause", "TriggerAutoPauseAlert", "ObserveDomainClocks",
                "ObserveNextCityDecision", "ObserveOracle", "SaveCheckpoint",
                "LoadCheckpoint",
            };

        public static ScenarioPreflightResult Verify(string repoRoot, string scenarioId)
        {
            ScenarioPin pin = PinnedScenarioLoader.Get(scenarioId);
            string foundationRoot = Path.Combine(repoRoot, "docs", "foundation-v0.1");
            string definitionPath = Path.Combine(
                foundationRoot, "scenarios", "definitions", scenarioId, "r1.json");
            string fixturePath = Path.Combine(
                foundationRoot, "scenarios", "fixtures", scenarioId, "r1.fixture.json");
            byte[] definitionBytes = ReadPinned(definitionPath, pin.DefinitionSha256);
            byte[] fixtureBytes = ReadPinned(fixturePath, pin.FixtureSha256);

            using JsonDocument definition = JsonDocument.Parse(definitionBytes);
            using JsonDocument fixture = JsonDocument.Parse(fixtureBytes);
            RequireInt(definition.RootElement, "schema_version", 2);
            RequireString(definition.RootElement, "id", scenarioId);
            RequireInt(definition.RootElement, "revision", 1);
            RequireInt(fixture.RootElement, "schema_version", 2);
            RequireString(fixture.RootElement, "scenario_id", scenarioId);
            RequireInt(fixture.RootElement, "scenario_revision", 1);
            RequireString(fixture.RootElement, "hash_algorithm", "sha256");

            JsonElement definitionFixture = definition.RootElement.GetProperty("fixture");
            JsonElement fixtureManifest = definitionFixture.GetProperty("fixture_manifest");
            RequireString(
                fixtureManifest,
                "path",
                "scenarios/fixtures/" + scenarioId + "/r1.fixture.json");
            RequireString(fixtureManifest, "sha256", pin.FixtureSha256);
            int worldSeed = ParseInvariantInt(
                definitionFixture.GetProperty("world_seed").GetString(),
                "world_seed");
            int durationTicks = definitionFixture.GetProperty("duration_ticks").GetInt32();
            int tickRateHz = definitionFixture.GetProperty("tick_rate_hz").GetInt32();
            int warmupTicks = definitionFixture.GetProperty("warmup_ticks").GetInt32();
            if (durationTicks <= 0 || tickRateHz <= 0 || warmupTicks < 0)
            {
                throw new InvalidDataException("invalid fixed-tick fixture bounds");
            }

            JsonElement artifacts = fixture.RootElement.GetProperty("artifacts");
            byte[] startingStateBytes = ReadArtifact(
                foundationRoot,
                artifacts.GetProperty("starting_state"),
                pin.StartingStateSha256,
                "starting-state",
                RequiredStringValue(definitionFixture, "starting_state_id"));
            byte[] contentSetBytes = ReadArtifact(
                foundationRoot,
                artifacts.GetProperty("content_set"),
                pin.ContentSetSha256,
                "content-set",
                RequiredStringValue(definitionFixture, "content_set_id"));
            byte[] inputScriptBytes = ReadArtifact(
                foundationRoot,
                artifacts.GetProperty("input_script"),
                pin.InputScriptSha256,
                "input-script",
                RequiredStringValue(definitionFixture, "input_script_id"));

            VerifyArtifactIdentity(
                startingStateBytes,
                scenarioId,
                "starting-state",
                RequiredStringValue(definitionFixture, "starting_state_id"));
            VerifyStartingState(startingStateBytes, worldSeed, definitionFixture);
            VerifyArtifactIdentity(
                contentSetBytes,
                scenarioId,
                "content-set",
                RequiredStringValue(definitionFixture, "content_set_id"));
            using JsonDocument script = JsonDocument.Parse(inputScriptBytes);
            RequireInt(script.RootElement, "schema_version", 1);
            RequireString(script.RootElement, "scenario_id", scenarioId);
            RequireInt(script.RootElement, "scenario_revision", 1);
            RequireString(script.RootElement, "artifact_kind", "input-script");
            RequireString(
                script.RootElement,
                "semantic_id",
                RequiredStringValue(definitionFixture, "input_script_id"));
            RequireInt(script.RootElement, "tick_rate_hz", tickRateHz);
            RequireInt(script.RootElement, "warmup_ticks", warmupTicks);
            RequireInt(script.RootElement, "duration_ticks", durationTicks);

            JsonElement bindings = script.RootElement.GetProperty("target_bindings");
            string bindingHash = Sha256(CanonicalJson(bindings));
            RequireString(script.RootElement, "target_bindings_sha256", bindingHash);
            if (!string.Equals(bindingHash, pin.TargetBindingsSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("target binding hash mismatch");
            }

            IReadOnlyDictionary<string, string> baseParameters = ReadParameters(
                definition.RootElement.GetProperty("parameters"));
            var referencedTargets = new HashSet<string>(StringComparer.Ordinal);
            JsonElement events = script.RootElement.GetProperty("events");
            int commandCount = VerifyEvents(
                events,
                referencedTargets,
                baseParameters,
                durationTicks,
                scenarioId);
            int declaredCommandCount = definition.RootElement
                .GetProperty("counts")
                .GetProperty("inputs")
                .GetProperty("scripted_commands")
                .GetInt32();
            if (commandCount != declaredCommandCount)
            {
                throw new InvalidDataException("definition command count mismatch");
            }

            var caseIds = new List<string>();
            var caseContracts = new Dictionary<string, (int CommandCount, IReadOnlyDictionary<string, string> Parameters)>(
                StringComparer.Ordinal);
            if (definition.RootElement.TryGetProperty("cases", out JsonElement definitionCases))
            {
                foreach (JsonElement definitionCase in definitionCases.EnumerateArray())
                {
                    string caseId = RequiredStringValue(definitionCase, "id");
                    if (!caseContracts.TryAdd(
                        caseId,
                        (
                            definitionCase.GetProperty("counts")
                                .GetProperty("inputs")
                                .GetProperty("scripted_commands")
                                .GetInt32(),
                            ReadParameters(definitionCase.GetProperty("parameters")))))
                    {
                        throw new InvalidDataException("duplicate definition case " + caseId);
                    }

                    caseIds.Add(caseId);
                }
            }

            JsonElement caseEvents = script.RootElement.GetProperty("case_events");
            JsonElement[] scriptCases = caseEvents.EnumerateArray().ToArray();
            if (scriptCases.Length != caseIds.Count)
            {
                throw new InvalidDataException("script case set differs from definition");
            }

            int caseCommandCount = 0;
            for (int index = 0; index < scriptCases.Length; index++)
            {
                JsonElement caseElement = scriptCases[index];
                string caseId = RequiredStringValue(caseElement, "id");
                if (!string.Equals(caseId, caseIds[index], StringComparison.Ordinal))
                {
                    throw new InvalidDataException("script case order differs from definition");
                }

                var contract = caseContracts[caseId];
                int actualCaseCommands = VerifyEvents(
                    caseElement.GetProperty("events"),
                    referencedTargets,
                    contract.Parameters,
                    durationTicks,
                    scenarioId);
                if (actualCaseCommands != contract.CommandCount)
                {
                    throw new InvalidDataException("case command count mismatch for " + caseId);
                }

                caseCommandCount += actualCaseCommands;
            }

            if (caseIds.Count > 0 && caseCommandCount != commandCount)
            {
                throw new InvalidDataException("aggregate and isolated case command counts differ");
            }

            ScenarioTargetResolver.Verify(
                bindings,
                referencedTargets,
                caseIds,
                startingStateBytes);

            IReadOnlyList<string> oracleIds = VerifyOracleObservations(
                definition.RootElement.GetProperty("oracles"),
                script.RootElement.GetProperty("oracle_observations"),
                durationTicks);

            return new ScenarioPreflightResult(
                pin,
                commandCount,
                caseIds.AsReadOnly(),
                worldSeed,
                durationTicks,
                oracleIds);
        }

        private static byte[] ReadArtifact(
            string foundationRoot,
            JsonElement descriptor,
            string expectedHash,
            string expectedKind,
            string expectedSemanticId)
        {
            RequireString(descriptor, "sha256", expectedHash);
            RequireString(descriptor, "media_type", "application/json");
            RequireString(descriptor, "kind", expectedKind);
            RequireString(descriptor, "semantic_id", expectedSemanticId);
            RequireInt(descriptor, "schema_version", 1);
            string relative = descriptor.GetProperty("path").GetString()
                ?? throw new InvalidDataException("artifact path is null");
            if (Path.IsPathRooted(relative) || relative.Contains("\\", StringComparison.Ordinal))
            {
                throw new InvalidDataException("artifact path is not confined");
            }

            string[] components = relative.Split('/');
            if (components.Any(component => component.Length == 0 || component == "." || component == ".."))
            {
                throw new InvalidDataException("artifact path has unsafe components");
            }

            string root = Path.GetFullPath(foundationRoot) + Path.DirectorySeparatorChar;
            string absolute = Path.GetFullPath(Path.Combine(foundationRoot, relative));
            if (!absolute.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidDataException("artifact escapes foundation root");
            }

            var info = new FileInfo(absolute);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("artifact may not be a link");
            }

            return ReadPinned(absolute, expectedHash);
        }

        private static byte[] ReadPinned(string path, string expectedHash)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string actual = Sha256(bytes);
            if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException("hash mismatch for " + path);
            }

            return bytes;
        }

        private static void VerifyArtifactIdentity(
            byte[] bytes,
            string scenarioId,
            string kind,
            string semanticId)
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            RequireInt(document.RootElement, "schema_version", 1);
            RequireString(document.RootElement, "scenario_id", scenarioId);
            RequireInt(document.RootElement, "scenario_revision", 1);
            RequireString(document.RootElement, "artifact_kind", kind);
            RequireString(document.RootElement, "semantic_id", semanticId);
        }

        private static int VerifyEvents(
            JsonElement events,
            HashSet<string> referencedTargets,
            IReadOnlyDictionary<string, string> expectedParameters,
            int durationTicks,
            string scenarioId)
        {
            int expectedSequence = 0;
            long previousTick = -1;
            var commandIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement item in events.EnumerateArray())
            {
                int sequence = item.GetProperty("sequence").GetInt32();
                long tick = item.GetProperty("tick").GetInt64();
                if (sequence != expectedSequence || tick < previousTick ||
                    tick < 0 || tick >= durationTicks)
                {
                    throw new InvalidDataException("events are not contiguous and ordered");
                }

                RequireString(item, "kind", "scripted_commands");

                JsonElement payload = item.GetProperty("payload");
                string command = payload.GetProperty("command").GetString()
                    ?? throw new InvalidDataException("command is null");
                string commandId = payload.GetProperty("command_id").GetString()
                    ?? throw new InvalidDataException("command id is null");
                string target = payload.GetProperty("target").GetString()
                    ?? throw new InvalidDataException("target is null");
                if (!AllowedCommands.Contains(command))
                {
                    throw new InvalidDataException("unknown command " + command);
                }

                if (commandId.Length == 0 || !commandIds.Add(commandId))
                {
                    throw new InvalidDataException("duplicate or empty command id");
                }

                if (!commandId.StartsWith(scenarioId + ":cmd:", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("command id is outside scenario namespace");
                }

                if (target.Length == 0)
                {
                    throw new InvalidDataException("empty logical target");
                }

                referencedTargets.Add(target);

                JsonElement arguments = payload.GetProperty("arguments");
                if (arguments.GetProperty("ordinal").GetInt32() != sequence + 1)
                {
                    throw new InvalidDataException("command ordinal differs from sequence");
                }

                VerifyParameterBindings(
                    arguments.GetProperty("bindings"),
                    expectedParameters);

                previousTick = tick;
                expectedSequence++;
            }

            return expectedSequence;
        }

        private static void VerifyStartingState(
            byte[] bytes,
            int worldSeed,
            JsonElement definitionFixture)
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            RequireString(
                document.RootElement,
                "world_seed",
                worldSeed.ToString(CultureInfo.InvariantCulture));
            RequireString(
                document.RootElement,
                "starting_tick",
                RequiredStringValue(definitionFixture, "starting_tick"));
        }

        private static IReadOnlyDictionary<string, string> ReadParameters(JsonElement parameters)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonElement parameter in parameters.EnumerateArray())
            {
                string name = RequiredStringValue(parameter, "name");
                string value = Encoding.UTF8.GetString(
                    CanonicalJson(parameter.GetProperty("value")));
                if (!result.TryAdd(name, value))
                {
                    throw new InvalidDataException("duplicate parameter " + name);
                }
            }

            return result;
        }

        private static void VerifyParameterBindings(
            JsonElement bindings,
            IReadOnlyDictionary<string, string> expected)
        {
            var actual = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty property in bindings.EnumerateObject())
            {
                if (!actual.TryAdd(
                    property.Name,
                    Encoding.UTF8.GetString(CanonicalJson(property.Value))))
                {
                    throw new InvalidDataException("duplicate parameter binding " + property.Name);
                }
            }

            if (actual.Count != expected.Count ||
                expected.Any(pair =>
                    !actual.TryGetValue(pair.Key, out string? value) ||
                    !string.Equals(value, pair.Value, StringComparison.Ordinal)))
            {
                throw new InvalidDataException("event parameter bindings differ from definition");
            }
        }

        private static IReadOnlyList<string> VerifyOracleObservations(
            JsonElement definitionOracles,
            JsonElement observations,
            int durationTicks)
        {
            var contracts = new Dictionary<string, (string Subject, string Operator)>(
                StringComparer.Ordinal);
            foreach (JsonElement oracle in definitionOracles.EnumerateArray())
            {
                string id = RequiredStringValue(oracle, "id");
                if (!contracts.TryAdd(
                    id,
                    (
                        RequiredStringValue(oracle, "subject"),
                        RequiredStringValue(oracle, "operator"))))
                {
                    throw new InvalidDataException("duplicate definition oracle " + id);
                }
            }

            var observed = new List<string>();
            int expectedSequence = 0;
            foreach (JsonElement observation in observations.EnumerateArray())
            {
                if (observation.GetProperty("sequence").GetInt32() != expectedSequence ||
                    observation.GetProperty("tick").GetInt32() != durationTicks)
                {
                    throw new InvalidDataException("oracle observation schedule differs");
                }

                string oracleId = RequiredStringValue(observation, "oracle_id");
                if (!contracts.TryGetValue(oracleId, out var contract) ||
                    !string.Equals(
                        RequiredStringValue(observation, "subject"),
                        contract.Subject,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        RequiredStringValue(observation, "operator"),
                        contract.Operator,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("oracle observation differs from definition");
                }

                RequireString(observation, "expected_source", "scenario-definition");
                observed.Add(oracleId);
                expectedSequence++;
            }

            if (observed.Count != contracts.Count ||
                !observed.ToHashSet(StringComparer.Ordinal).SetEquals(contracts.Keys))
            {
                throw new InvalidDataException("oracle observation set differs from definition");
            }

            return observed.AsReadOnly();
        }

        private static byte[] CanonicalJson(JsonElement element)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                WriteCanonical(writer, element);
            }

            return stream.ToArray();
        }

        private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (JsonProperty property in element.EnumerateObject().OrderBy(
                        property => property.Name,
                        StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCanonical(writer, property.Value);
                    }

                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        WriteCanonical(writer, child);
                    }

                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    writer.WriteRawValue(element.GetRawText(), skipInputValidation: false);
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                    writer.WriteNullValue();
                    break;
                default:
                    throw new InvalidDataException("unsupported JSON token");
            }
        }

        private static string Sha256(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        private static void RequireString(JsonElement element, string name, string expected)
        {
            string? actual = element.GetProperty(name).GetString();
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(name + " mismatch");
            }
        }

        private static string RequiredStringValue(JsonElement element, string name)
        {
            string? value = element.GetProperty(name).GetString();
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidDataException("missing string " + name);
            }

            return value;
        }

        private static int ParseInvariantInt(string? value, string name)
        {
            if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int result))
            {
                throw new InvalidDataException("invalid integer string " + name);
            }

            return result;
        }

        private static void RequireInt(JsonElement element, string name, int expected)
        {
            if (element.GetProperty(name).GetInt32() != expected)
            {
                throw new InvalidDataException(name + " mismatch");
            }
        }
    }
}
