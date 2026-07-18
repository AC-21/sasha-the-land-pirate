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
            string recovery = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingDepotApproachRecoveryView.cs"));
            string wreckLine = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingRouteModulePointView.cs"));
            string pumpHall = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingPumpHallCutawayView.cs"));
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
            Require(controller, "ApplyPresentationOnlyRoadControls");
            Require(controller, "OpenBuildingCutaway");
            Require(controller, "OpenGarageBay");
            string installationOperation = Segment(
                controller,
                "public void InstallCityImprovement()",
                "public void ServiceFieldSleeve()");
            Require(
                installationOperation,
                "_readModel.IsCityImprovementInstallationAvailable");
            Require(
                installationOperation,
                "new InstallCityImprovementCommand");
            Require(
                installationOperation,
                "NextCityDecision.RefurbishAuxiliaryPump");
            Require(
                installationOperation,
                "LastBearingState.AuxiliaryPumpSocketId");
            Require(
                installationOperation,
                "LastBearingState.AuxiliaryPumpOrientationQuarterTurns");
            string wreckLineOperation = Segment(
                controller,
                "public void OperateWreckLineModulePoint()",
                "public void ChooseLiquidReturn");
            Require(
                wreckLineOperation,
                "_readModel.IsWreckLineModulePointAvailable");
            Require(
                wreckLineOperation,
                "new OperateWreckLineModuleCommand");
            foreach (string forbidden in new[]
            {
                "Rigidbody",
                "RoadFeelTelemetry",
                "Collider",
                "Physics",
                ".position",
                ".transform",
            })
            {
                TestHarness.True(
                    wreckLineOperation.IndexOf(
                        forbidden,
                        StringComparison.Ordinal) < 0,
                    "Wreck Line controller operation reads presentation authority " +
                    forbidden);
            }
            string recoveryOperation = Segment(
                controller,
                "public void OperateDepotApproachRecoveryPoint()",
                "public void ChooseLiquidReturn");
            Require(
                recoveryOperation,
                "_readModel.IsDepotApproachRecoveryAvailable");
            Require(
                recoveryOperation,
                "new OperateDepotRecoveryPointCommand");
            foreach (string forbidden in new[]
            {
                "Rigidbody",
                "RoadFeelTelemetry",
                "Collider",
                "Physics",
                ".position",
                ".transform",
            })
            {
                TestHarness.True(
                    recoveryOperation.IndexOf(
                        forbidden,
                        StringComparison.Ordinal) < 0,
                    "depot recovery controller operation reads presentation authority " +
                    forbidden);
            }
            string globalShortcuts = Segment(
                controller,
                "private void HandleGlobalShortcuts()",
                "private void SimulateOneTick()");
            Require(
                globalShortcuts,
                "_readModel.IsWreckLineModulePointAvailable");
            Require(
                globalShortcuts,
                "_readModel.IsDepotApproachRecoveryAvailable");
            Require(globalShortcuts, "keyboard.eKey.wasPressedThisFrame");
            Require(globalShortcuts, "gamepad.buttonSouth.wasPressedThisFrame");
            int wreckGate = globalShortcuts.IndexOf(
                "_readModel.IsWreckLineModulePointAvailable",
                StringComparison.Ordinal);
            int depotGate = globalShortcuts.IndexOf(
                "_readModel.IsDepotApproachRecoveryAvailable",
                StringComparison.Ordinal);
            int wreckHotkey = globalShortcuts.IndexOf(
                "keyboard.eKey.wasPressedThisFrame",
                wreckGate,
                StringComparison.Ordinal);
            int depotHotkey = globalShortcuts.IndexOf(
                "keyboard.eKey.wasPressedThisFrame",
                depotGate,
                StringComparison.Ordinal);
            TestHarness.True(
                wreckGate >= 0 && wreckHotkey > wreckGate &&
                depotGate > wreckHotkey && depotHotkey > depotGate,
                "road hotkeys must be context-gated before consuming city-camera E");
            Require(camera, "keyboard.eKey.isPressed");
            string driveInput = Segment(
                controller,
                "private void QueueDriveInputIfApplicable()",
                "float throttle = 0f;");
            Require(driveInput, "_pendingCommands.Count != 0");
            Require(
                driveInput,
                "_readModel.IsWreckLineModulePointAvailable");
            Require(
                driveInput,
                "_readModel.IsDepotApproachRecoveryAvailable");
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
            Require(controller, "_world.ApplyDepotApproachRecovery(");
            Require(controller, "_world.ApplyRouteModulePoint(");
            Require(controller, "_world.ApplyRoadCargoPresentation(");
            Require(controller, "_world.ApplyCityImprovement(");
            Require(controller, "_modeCoordinator?.ApplyCanonical(_readModel);");
            TestHarness.True(
                controller.IndexOf(
                    "_world.Apply(new LastBearingVisualSnapshot",
                    StringComparison.Ordinal) <
                controller.IndexOf(
                    "_modeCoordinator?.ApplyCanonical(_readModel);",
                    StringComparison.Ordinal),
                "canonical world pose must render before road activation and synchronization");
            string simulationTick = Segment(
                controller,
                "private void SimulateOneTick()",
                "private void QueueDriveInputIfApplicable()");
            Require(simulationTick, "TryAutosave(result.DomainEvents);");
            Require(simulationTick, "ApplyPresentation();");
            TestHarness.True(
                simulationTick.IndexOf(
                    "_state = result.State;",
                    StringComparison.Ordinal) <
                simulationTick.IndexOf(
                    "TryAutosave(result.DomainEvents);",
                    StringComparison.Ordinal),
                "critical autosave must follow the committed canonical state");
            TestHarness.True(
                simulationTick.IndexOf(
                    "TryAutosave(result.DomainEvents);",
                    StringComparison.Ordinal) <
                simulationTick.IndexOf(
                    "ApplyPresentation();",
                    StringComparison.Ordinal),
                "critical autosave must precede fallible derived presentation");
            string autosave = Segment(
                controller,
                "private void TryAutosave(",
                "private void Queue(");
            foreach (string criticalEvent in new[]
            {
                "ExpeditionDeparted",
                "RouteActionUsed",
                "DepotRecoveryPointOperated",
                "DepotResolved",
                "ReturnPayloadFrozen",
                "VehicleReturned",
                "CityReturnCredited",
                "TurbineRepaired",
                "CityImprovementInstalled",
            })
            {
                Require(autosave, "LastBearingEventKind." + criticalEvent);
            }

            Require(autosave, "Save();");
            Require(autosave, "return;");
            TestHarness.True(
                autosave.IndexOf(
                    "IdempotentReplayAccepted",
                    StringComparison.Ordinal) < 0,
                "idempotent replay alone must not trigger autosave");
            Require(vehicle, "snapshot.VehicleLateralNormalized");
            Require(vehicle, "VisibleLateralOffset");
            Require(vehicle, "FrontWheelSteerDegrees");
            Require(vehicle, "SnapToCanonicalRoadPose");
            Require(vehicle, "Apply(snapshot, snapLateral: false)");
            Require(vehicle, "Apply(_lastSnapshot, snapLateral: true)");

            Require(camera, "D0022-PROVISIONAL-LAST-BEARING-CAMERA-V1");
            Require(camera, "SetComparisonMode");
            Require(camera, "SetRoadTarget");
            Require(world, "RoadFeelRigFactory.Create");
            Require(world, "RoadFeelRigInstance");
            Require(world, "drivingModeRoot");
            Require(world, "LastBearingDepotApproachRecoveryView");
            Require(world, "DepotApproachRecoveryView.Build(");
            Require(world, "LastBearingRouteModulePointView");
            Require(world, "RouteModulePointView.Build(");
            string pumpHallBuild = Segment(
                world,
                "private void BuildPumpHallCutaway(",
                "private void BuildCityGrammarComparison(");
            Require(
                pumpHallBuild,
                "PumpHallCutawayView.Build(\n" +
                "                LastBearingState.AuxiliaryPumpSocketId,");
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
            Require(modeCoordinator, "readModel.IsWreckLineModulePointAvailable");
            Require(modeCoordinator, "ApplyDerivedPresentationLoad");
            Require(modeCoordinator, "readModel.VehicleConditionMilli");
            Require(modeCoordinator, "DerivePresentationDamageBand");
            Require(modeCoordinator, "LastBearingRoadDamageBand.Critical");
            Require(modeCoordinator, "ApplyPresentationOnlyControls");
            Require(modeCoordinator, "LAST_BEARING_ROAD_PRESENTATION_DISABLED");
            Require(modeCoordinator, "RoadAdapterFaulted");
            Require(modeCoordinator, "SynchronizePresentationPose");
            Require(modeCoordinator, "ApplyPresentationOwnership");
            Require(
                modeCoordinator,
                "IsRoadPresentationHeldAtRecovery");
            Require(
                modeCoordinator,
                "IsRoadPresentationHeldAtModulePoint");
            string recoveryHold = Segment(
                modeCoordinator,
                "private void HoldRoadAdapterAtCanonicalRecoveryPose()",
                "private void SuspendRoadAdapter");
            Require(recoveryHold, "adapter.SetRoadModeActive(false)");
            Require(recoveryHold, "_canonicalVehicle.SnapToCanonicalRoadPose()");
            Require(recoveryHold, "adapter.SynchronizePresentationPose(");
            TestHarness.True(
                recoveryHold.IndexOf(
                    "adapter.SetRoadModeActive(false)",
                    StringComparison.Ordinal) <
                recoveryHold.IndexOf(
                    "_canonicalVehicle.SnapToCanonicalRoadPose()",
                    StringComparison.Ordinal),
                "recovery hold must suspend physics before snapping canonical pose");
            TestHarness.True(
                recoveryHold.IndexOf(
                    "_canonicalVehicle.SnapToCanonicalRoadPose()",
                    StringComparison.Ordinal) <
                recoveryHold.IndexOf(
                    "adapter.SynchronizePresentationPose(",
                    StringComparison.Ordinal),
                "recovery hold must render canonical pose before synchronization");
            string roadActivation = Segment(
                modeCoordinator,
                "private void ActivateRoadAdapter()",
                "private void SuspendRoadAdapter");
            Require(roadActivation, "_canonicalVehicle.SnapToCanonicalRoadPose()");
            Require(roadActivation, "adapter.SynchronizePresentationPose(");
            Require(roadActivation, "adapter.SetRoadModeActive(true)");
            TestHarness.True(
                roadActivation.IndexOf(
                    "_canonicalVehicle.SnapToCanonicalRoadPose()",
                    StringComparison.Ordinal) <
                roadActivation.IndexOf(
                    "adapter.SynchronizePresentationPose(",
                    StringComparison.Ordinal),
                "canonical road pose must snap before adapter synchronization");
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
            Require(hud, "OperateDepotApproachRecoveryPoint");
            Require(hud, "OPERATE DEPOT RECOVERY POINT");
            Require(hud, "OperateWreckLineModulePoint");
            Require(hud, "DEPLOY WINCH · RECOVER PUMP ROTOR");
            Require(hud, "CROSS SEALED DUST EXPOSURE");
            Require(hud, "IsCityImprovementInstallationAvailable");
            Require(hud, "INSTALL REFURBISHED AUXILIARY PUMP");
            TestHarness.True(
                hud.IndexOf(
                    "model.IsCityImprovementInstallationAvailable",
                    StringComparison.Ordinal) <
                hud.IndexOf("model.MaintenanceDue", StringComparison.Ordinal),
                "available fixed-socket installation must not be hidden by maintenance");

            Require(recovery, "C0-VGR-02");
            Require(recovery, "Revision = \"R1\"");
            Require(recovery, "poi_depot_approach_recovery_a");
            Require(recovery, "ANCHOR_DEPOT_RECOVERY_INTERACTION");
            Require(recovery, "collider.enabled = false");
            foreach (string forbidden in new[]
            {
                "Rigidbody",
                "RoadFeelTelemetry",
                "Physics.",
                "OnTrigger",
                "OnCollision",
                "LastBearingKernel",
                "LastBearingCommand",
                "LastBearingState",
                "SaveContracts",
                "System.IO",
                "Application.persistentDataPath",
                "Keyboard.current",
                "Gamepad.current",
            })
            {
                TestHarness.True(
                    recovery.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "depot recovery view contains forbidden authority " +
                    forbidden);
            }

            Require(wreckLine, "C0-VGR-03");
            Require(wreckLine, "Revision = \"R1\"");
            Require(wreckLine, "poi_wreck_line_module_point_a");
            Require(wreckLine, "ANCHOR_WRECK_LINE_MODULE_INTERACTION");
            Require(wreckLine, "Existing Pump Rotor");
            Require(wreckLine, "Dust Exposure Curtain");
            Require(wreckLine, "collider.enabled = false");
            foreach (string forbidden in new[]
            {
                "Rigidbody",
                "RoadFeelTelemetry",
                "Physics.",
                "OnTrigger",
                "OnCollision",
                "LastBearingKernel",
                "LastBearingCommand",
                "LastBearingState",
                "SaveContracts",
                "System.IO",
                "Application.persistentDataPath",
                "Keyboard.current",
                "Gamepad.current",
            })
            {
                TestHarness.True(
                    wreckLine.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "Wreck Line view contains forbidden authority " +
                    forbidden);
            }

            Require(pumpHall, "C0-VGR-04");
            Require(pumpHall, "Revision = \"R1\"");
            Require(pumpHall, "bld_pump_hall_cutaway_a");
            Require(pumpHall, "string fixedCivicSocketId");
            Require(pumpHall, "CreateAnchor(\n                fixedCivicSocketId,");
            Require(pumpHall, "HasRoof => false");
            Require(pumpHall, "HasNearWall => false");
            Require(pumpHall, "collider.enabled = false");
            foreach (string forbidden in new[]
            {
                "Rigidbody",
                "RoadFeelTelemetry",
                "Physics.",
                "OnTrigger",
                "OnCollision",
                "LastBearingKernel",
                "LastBearingCommand",
                "LastBearingState",
                "SaveContracts",
                "System.IO",
                "Application.persistentDataPath",
                "Keyboard.current",
                "Gamepad.current",
            })
            {
                TestHarness.True(
                    pumpHall.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "pump-hall cutaway contains forbidden authority " +
                    forbidden);
            }

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
