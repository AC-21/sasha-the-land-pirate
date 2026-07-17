#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class ScenarioTargetResolver
    {
        public static void Verify(
            JsonElement bindings,
            IReadOnlyCollection<string> referencedTargets,
            IReadOnlyCollection<string> caseIds,
            byte[] startingStateBytes)
        {
            using JsonDocument startingState = JsonDocument.Parse(startingStateBytes);
            HashSet<string> baseRecordIds = ReadRecordIds(
                startingState.RootElement.GetProperty("canonical_state_records"),
                "base");
            IReadOnlyDictionary<string, HashSet<string>> caseRecordIds =
                ReadCaseRecordIds(startingState.RootElement.GetProperty("case_states"));
            if (!caseRecordIds.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(caseIds))
            {
                throw new InvalidDataException("starting-state case set differs");
            }

            var boundTargets = new HashSet<string>(StringComparer.Ordinal);
            string? previous = null;
            foreach (JsonElement binding in bindings.EnumerateArray())
            {
                string logical = RequiredString(binding, "logical_target_id");
                if (previous != null && string.CompareOrdinal(previous, logical) >= 0)
                {
                    throw new InvalidDataException("target bindings are not strictly sorted");
                }

                if (!boundTargets.Add(logical))
                {
                    throw new InvalidDataException("duplicate target binding " + logical);
                }

                previous = logical;
                string bindingKind = RequiredString(binding, "binding_kind");
                if (bindingKind.Length == 0)
                {
                    throw new InvalidDataException("empty binding kind");
                }

                if (string.Equals(bindingKind, "scenario-selector", StringComparison.Ordinal))
                {
                    if (binding.TryGetProperty("resolved_id", out _) ||
                        binding.TryGetProperty("case_resolutions", out _))
                    {
                        throw new InvalidDataException("scenario selector has record resolution");
                    }

                    string selectorKind = RequiredString(binding, "selector_kind");
                    string selectorValue = RequiredString(binding, "selector_value");
                    bool supported =
                        (string.Equals(selectorKind, "clock-subsystem", StringComparison.Ordinal) &&
                         string.Equals(selectorValue, "clock", StringComparison.Ordinal)) ||
                        (string.Equals(selectorKind, "oracle-surface", StringComparison.Ordinal) &&
                         string.Equals(selectorValue, "oracles", StringComparison.Ordinal)) ||
                        (string.Equals(selectorKind, "save-subsystem", StringComparison.Ordinal) &&
                         string.Equals(selectorValue, "save", StringComparison.Ordinal));
                    if (!supported)
                    {
                        throw new InvalidDataException(
                            "unknown scenario selector " + selectorKind + ":" + selectorValue);
                    }

                    continue;
                }

                if (!string.Equals(
                    bindingKind,
                    "canonical-state-record",
                    StringComparison.Ordinal))
                {
                    throw new InvalidDataException("unknown binding kind " + bindingKind);
                }

                string resolved = RequiredString(binding, "resolved_id");
                if (!baseRecordIds.Contains(resolved))
                {
                    throw new InvalidDataException("unresolved base state target " + resolved);
                }

                var resolvedCases = new HashSet<string>(StringComparer.Ordinal);
                if (binding.TryGetProperty("case_resolutions", out JsonElement resolutions))
                {
                    foreach (JsonElement resolution in resolutions.EnumerateArray())
                    {
                        string caseId = RequiredString(resolution, "case_id");
                        if (!resolvedCases.Add(caseId))
                        {
                            throw new InvalidDataException("duplicate target case " + caseId);
                        }

                        string caseResolved = RequiredString(resolution, "resolved_id");
                        if (!caseRecordIds.TryGetValue(caseId, out HashSet<string>? records) ||
                            !records.Contains(caseResolved))
                        {
                            throw new InvalidDataException("unresolved case target " + caseResolved);
                        }
                    }
                }

                if (!resolvedCases.SetEquals(caseIds))
                {
                    throw new InvalidDataException("target case resolution set differs");
                }
            }

            if (!boundTargets.SetEquals(referencedTargets))
            {
                string missing = string.Join(",", referencedTargets.Except(boundTargets));
                string extra = string.Join(",", boundTargets.Except(referencedTargets));
                throw new InvalidDataException(
                    "target reference set differs; missing=" + missing + "; extra=" + extra);
            }
        }

        private static HashSet<string> ReadRecordIds(
            JsonElement records,
            string label)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement record in records.EnumerateArray())
            {
                string id = RequiredString(record, "id");
                if (!ids.Add(id))
                {
                    throw new InvalidDataException("duplicate " + label + " record " + id);
                }
            }

            return ids;
        }

        private static IReadOnlyDictionary<string, HashSet<string>> ReadCaseRecordIds(
            JsonElement caseStates)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (JsonElement caseState in caseStates.EnumerateArray())
            {
                string caseId = RequiredString(caseState, "id");
                if (!result.TryAdd(
                    caseId,
                    ReadRecordIds(
                        caseState.GetProperty("canonical_state_records"),
                        "case " + caseId)))
                {
                    throw new InvalidDataException("duplicate starting-state case " + caseId);
                }
            }

            return result;
        }

        private static string RequiredString(JsonElement element, string property)
        {
            string? value = element.GetProperty(property).GetString();
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidDataException("missing string " + property);
            }

            return value;
        }
    }
}
