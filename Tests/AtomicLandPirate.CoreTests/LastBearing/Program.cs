#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class Program
    {
        private static readonly string[] ScenarioIds =
        {
            "SCN_COMPOSITION_LOOP_SMOKE",
            "SCN_TIME_POLICY",
            "SCN_PREPARATION_MODULE_MATRIX",
            "SCN_FACTION_WAIT_CLAIM",
            "SCN_BEARING_COOPERATE",
            "SCN_BEARING_TAKE",
        };

        public static int Main(string[] args)
        {
            string repoRoot = FindRepositoryRoot();
            if (args.Length == 2 && string.Equals(args[0], "--scenario", StringComparison.Ordinal))
            {
                return RunScenario(repoRoot, args[1]);
            }

            if (args.Length == 2 && string.Equals(args[0], "--test", StringComparison.Ordinal))
            {
                return RunNamedTest(repoRoot, args[1]);
            }

            if (args.Length != 0)
            {
                Console.Error.WriteLine(
                    "usage: AtomicLandPirate.LastBearingTests [--scenario ID | --test ID]");
                return 2;
            }

            var harness = new TestHarness();
            harness.Run("complete first-materialization union", () =>
                MaterializationTests.CompleteUnionIsPresent(repoRoot));
            SimulationTests.Run(harness);
            SaveAtomicTests.Run(harness, repoRoot);
            HomecomingTests.RunSave(harness, repoRoot);
            SaveBoundaryTests.Run(harness, repoRoot);
            foreach (string scenarioId in ScenarioIds)
            {
                ScenarioTests.Run(harness, repoRoot, scenarioId);
            }

            return harness.Finish("all");
        }

        private static int RunScenario(string repoRoot, string scenarioId)
        {
            if (Array.IndexOf(ScenarioIds, scenarioId) < 0)
            {
                Console.Error.WriteLine("unknown protected scenario: " + scenarioId);
                return 2;
            }

            var harness = new TestHarness();
            ScenarioPreflightResult? preflight = null;
            harness.Run("complete first-materialization union", () =>
                MaterializationTests.CompleteUnionIsPresent(repoRoot));
            harness.Run("pinned scenario integrity", () =>
                preflight = ScenarioIntegrityPreflight.Verify(repoRoot, scenarioId));
            SimulationTests.Run(harness);
            ScenarioTests.RunMechanics(harness, scenarioId);
            int result = harness.Finish(scenarioId);
            if (preflight != null)
            {
                Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["scenario_id"] = scenarioId,
                    ["revision"] = 1,
                    ["definition_sha256"] = preflight.Pin.DefinitionSha256,
                    ["fixture_sha256"] = preflight.Pin.FixtureSha256,
                    ["command_count"] = preflight.CommandCount,
                    ["case_ids"] = preflight.CaseIds,
                    ["world_seed"] = preflight.WorldSeed,
                    ["duration_ticks"] = preflight.DurationTicks,
                    ["oracle_ids"] = preflight.OracleIds,
                    ["passed"] = result == 0,
                }));
            }

            return result;
        }

        private static int RunNamedTest(string repoRoot, string id)
        {
            var harness = new TestHarness();
            switch (id)
            {
                case "dev-save-atomic":
                    SaveAtomicTests.Run(harness, repoRoot);
                    HomecomingTests.RunSave(harness, repoRoot);
                    break;
                case "dev-save-boundary":
                    SaveBoundaryTests.Run(harness, repoRoot);
                    break;
                case "vgr05-one-good-batch":
                    SaveAtomicTests.RunOneGoodBatch(harness, repoRoot);
                    break;
                case "vgr14-working-service-cell":
                    CityConstructionTests.Run(harness);
                    break;
                default:
                    Console.Error.WriteLine("unknown protected test: " + id);
                    return 2;
            }

            return harness.Finish(id);
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (current != null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                    Directory.Exists(Path.Combine(current.FullName, "SimulationCore")) &&
                    Directory.Exists(Path.Combine(current.FullName, "Game")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("repository root is unavailable");
        }
    }
}
