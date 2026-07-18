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
            string modeCoordinator = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingModeCoordinator.cs"));
            string world = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingWorldBuilder.cs"));
            string hud = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingHud.cs"));
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
            Require(controller, "ApplyQuantizedRoadCommandShadow");
            Require(controller, "GetModeRoot(");
            Require(controller, "LastBearingPresentationMode.Driving");
            Require(controller, "ConfigurePresentationOwners");
            Require(controller, "AttachRoadModeAdapter");
            Require(controller, "OpenBuildingCutaway");
            Require(controller, "OpenGarageBay");
            string driveInput = Segment(
                controller,
                "private void QueueDriveInputIfApplicable()",
                "float throttle = 0f;");
            Require(driveInput, "_pendingCommands.Count != 0");
            string load = Segment(
                controller,
                "public void Load()",
                "private void Update()");
            Require(load, "_modeCoordinator?.ClearSession();");
            TestHarness.True(
                load.IndexOf("ResetForSession", StringComparison.Ordinal) < 0,
                "load must not activate road presentation before canonical rendering");
            TestHarness.True(
                load.IndexOf("_modeCoordinator?.ClearSession();", StringComparison.Ordinal) <
                load.IndexOf("ApplyPresentation();", StringComparison.Ordinal),
                "load must fail closed before applying the loaded presentation");
            Require(controller, "_world.Apply(new LastBearingVisualSnapshot");
            Require(controller, "_modeCoordinator?.ApplyCanonical(_readModel);");
            TestHarness.True(
                controller.IndexOf(
                    "_world.Apply(new LastBearingVisualSnapshot",
                    StringComparison.Ordinal) <
                controller.IndexOf(
                    "_modeCoordinator?.ApplyCanonical(_readModel);",
                    StringComparison.Ordinal),
                "canonical world pose must render before road activation and synchronization");
            Require(vehicle, "snapshot.VehicleLateralNormalized");
            Require(vehicle, "VisibleLateralOffset");
            Require(vehicle, "FrontWheelSteerDegrees");

            Require(camera, "D0022-PROVISIONAL-LAST-BEARING-CAMERA-V1");
            Require(camera, "SetComparisonMode");
            Require(camera, "SetRoadTarget");
            Require(world, "RoadFeelRigFactory.Create");
            Require(world, "RoadFeelRigInstance");
            Require(world, "drivingModeRoot");
            Require(comparison, "RestrainedSnapGrid");
            Require(comparison, "DistrictStamp");
            Require(comparison, "ResetComparison");
            Require(comparison, "EvidenceSummary");

            Require(modeCoordinator, "LastBearingPresentationMode.CityOverview");
            Require(modeCoordinator, "LastBearingPresentationMode.BuildingCutaway");
            Require(modeCoordinator, "LastBearingPresentationMode.GarageBay");
            Require(modeCoordinator, "LastBearingPresentationMode.Driving");
            Require(modeCoordinator, "LastBearingPresentationMode.DepotEncounter");
            Require(modeCoordinator, "LastBearingPresentationMode.CityReturn");
            Require(modeCoordinator, "ExpeditionPhase.Outbound");
            Require(modeCoordinator, "ExpeditionPhase.AtDepot");
            Require(modeCoordinator, "ExpeditionPhase.Returned");
            Require(modeCoordinator, "ActiveModeCount");
            Require(modeCoordinator, "ILastBearingRoadModeAdapter");
            Require(modeCoordinator, "readModel.PauseCause == PauseCause.None");
            Require(modeCoordinator, "LAST_BEARING_ROAD_PRESENTATION_DISABLED");
            Require(modeCoordinator, "RoadAdapterFaulted");
            Require(modeCoordinator, "SynchronizePresentationPose");
            Require(modeCoordinator, "ApplyPresentationOwnership");
            string roadActivation = Segment(
                modeCoordinator,
                "private void ActivateRoadAdapter()",
                "private void SuspendRoadAdapter");
            Require(roadActivation, "adapter.SynchronizePresentationPose(");
            Require(roadActivation, "adapter.SetRoadModeActive(true)");
            TestHarness.True(
                roadActivation.IndexOf(
                    "adapter.SynchronizePresentationPose(",
                    StringComparison.Ordinal) <
                roadActivation.IndexOf(
                    "adapter.SetRoadModeActive(true)",
                    StringComparison.Ordinal),
                "road pose must synchronize while suspended before physics activation");
            TestHarness.True(
                modeCoordinator.IndexOf("RoadFeelTelemetry", StringComparison.Ordinal) < 0,
                "mode coordinator must not read Road Feel outcomes");
            TestHarness.True(
                modeCoordinator.IndexOf("new LastBearingState", StringComparison.Ordinal) < 0,
                "mode coordinator must not construct canonical state");
            TestHarness.True(
                modeCoordinator.IndexOf("SaveContracts", StringComparison.Ordinal) < 0,
                "mode coordinator must not add a save seam");
            Require(hud, "R0 ROUTING SCAFFOLD");
            Require(hud, "There is no on-foot mode.");

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

        private static string Segment(
            string source,
            string startToken,
            string endToken)
        {
            int start = source.IndexOf(startToken, StringComparison.Ordinal);
            TestHarness.True(start >= 0, "Game source contract is missing " + startToken);
            int end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            TestHarness.True(end > start, "Game source contract is missing " + endToken);
            return source.Substring(start, end - start);
        }
    }
}
