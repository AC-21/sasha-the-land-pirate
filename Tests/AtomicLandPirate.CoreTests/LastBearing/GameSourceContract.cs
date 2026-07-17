#nullable enable

using System;
using System.IO;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class GameSourceContract
    {
        public static void Verify(string repoRoot)
        {
            string runtimeRoot = Path.Combine(
                repoRoot,
                "Game/Assets/AtomicLandPirate/LastBearing/Runtime");
            string controller = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingGameController.cs"));
            string vehicle = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingVehicleView.cs"));
            string camera = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingCameraRig.cs"));
            string comparison = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingCityGrammarComparison.cs"));
            string dispatcher = File.ReadAllText(
                Path.Combine(
                    repoRoot,
                    "Game/Assets/AtomicLandPirate/LastBearing/Editor/" +
                    "WP0002GateDispatcher.cs"));
            string editorAssembly = File.ReadAllText(
                Path.Combine(
                    repoRoot,
                    "Game/Assets/AtomicLandPirate/LastBearing/Editor/" +
                    "AC21.Sasha.LastBearing.Editor.asmdef"));

            TestHarness.True(
                controller.IndexOf("new ReturnHomeCommand", StringComparison.Ordinal) < 0,
                "Unity adapter queues ReturnHome before the vehicle has returned");
            Require(controller, "_readModel.VehicleLateralMilli");
            Require(controller, "LastBearingBalanceV1.RoadLateralLimitMilli");
            Require(vehicle, "snapshot.VehicleLateralNormalized");
            Require(vehicle, "VisibleLateralOffset");
            Require(vehicle, "FrontWheelSteerDegrees");

            Require(camera, "D0022-PROVISIONAL-LAST-BEARING-CAMERA-V1");
            Require(camera, "SetComparisonMode");
            Require(comparison, "RestrainedSnapGrid");
            Require(comparison, "DistrictStamp");
            Require(comparison, "ResetComparison");
            Require(comparison, "EvidenceSummary");

            Require(dispatcher, "[assembly: TestRunCallback(");
            Require(dispatcher, "WP0002TestRunCallback : ITestRunCallback");
            Require(dispatcher, "wp0002-pending-test-gate.json");
            Require(dispatcher, "ValidateResultTree");
            Require(dispatcher, "TestRunnerApi.CancelTestRun(runId)");
            Require(dispatcher, "phase = StartingPhase");
            Require(dispatcher, "ReplacePendingTestGate(boundary, pending)");
            Require(dispatcher, "deadline_at");
            Require(dispatcher, "WatchPendingTestGate");
            Require(dispatcher, "TransientTestErrorCallbacks : IErrorCallbacks");
            Require(dispatcher, "TestRunnerApi.RegisterTestCallback(callbacks)");
            Require(dispatcher, "TestRunnerApi.UnregisterTestCallback(callbacks)");
            Require(dispatcher, "gateId + \"-\" + invocationId + \".json\"");
            Require(dispatcher, "VerifyExactFile(path, payload)");
            Require(editorAssembly, "\"UnityEngine.TestRunner\"");
        }

        private static void Require(string source, string token)
        {
            TestHarness.True(
                source.IndexOf(token, StringComparison.Ordinal) >= 0,
                "Game source contract is missing " + token);
        }
    }
}
