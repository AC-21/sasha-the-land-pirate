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
            string permitJobPresenter = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingPermitJobPresenter.cs"));
            string vehicle = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingVehicleView.cs"));
            string camera = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingCameraRig.cs"));
            string roadChaseCamera = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "RoadFeel/RoadFeelChaseCamera.cs"));
            string comparison = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingCityGrammarComparison.cs"));
            string cityServiceCell = File.ReadAllText(
                Path.Combine(runtimeRoot, "LastBearingCityServiceCellView.cs"));
            string recovery = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingDepotApproachRecoveryView.cs"));
            string wreckLine = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingRouteModulePointView.cs"));
            string depotCargoLoading = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingDepotCargoLoadingView.cs"));
            string pumpHall = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingPumpHallCutawayView.cs"));
            string oneGoodBatch = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "LastBearingOneGoodBatchCutawayView.cs"));
            string garage = File.ReadAllText(
                Path.Combine(
                    runtimeRoot,
                    "Vehicle/LastBearingGarageBayView.cs"));
            string dispatcher = File.ReadAllText(
                Path.Combine(
                    repoRoot,
                    "Game/Assets/AtomicLandPirate/LastBearing/Editor/" +
                    "WP0002GateDispatcher.cs"));
            string nativeBuildProfile = File.ReadAllText(
                Path.Combine(
                    repoRoot,
                    "Game/Assets/AtomicLandPirate/LastBearing/BuildProfiles/" +
                    "WP0002NativeIl2CppArm64Performance.asset"));
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
            Require(controller, "public PreparationChoice GaragePreparationIntent");
            Require(controller, "public bool IsGaragePlanIntentActive");
            Require(controller, "public bool IsGaragePlanCommitAvailable");
            Require(controller, "public void BeginGaragePlan(");
            Require(controller, "public void CommitGaragePlan(");
            Require(controller, "public void CancelGaragePlan()");
            Require(controller, "public void StartSpareBearingBatch()");
            Require(controller, "new StartSpareBearingBatchCommand(sequence)");
            Require(controller, "public void BarterSpareBearingLot()");
            Require(controller, "new BarterSpareBearingLotCommand(sequence)");
            Require(controller, "LastBearingEventKind.SpareBearingBatchStarted");
            Require(controller, "LastBearingEventKind.SpareBearingBatchCheckpointReached");
            Require(controller, "LastBearingEventKind.SpareBearingBatchCompleted");
            Require(controller, "LastBearingEventKind.SpareBearingLotBartered");
            Require(controller, "LastBearingEventKind.RoutePermitGranted");
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
            string depotCargoLoadOperation = Segment(
                controller,
                "public void LoadDepotRepairCargo()",
                "public void OperateDepotApproachRecoveryPoint()");
            Require(
                depotCargoLoadOperation,
                "_readModel.IsRepairCargoLoadAvailable");
            Require(
                depotCargoLoadOperation,
                "new LoadDepotRepairCargoCommand(sequence)");
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
                    depotCargoLoadOperation.IndexOf(
                        forbidden,
                        StringComparison.Ordinal) < 0,
                    "depot cargo load operation reads presentation authority " +
                    forbidden);
            }
            string beginReturn = Segment(
                controller,
                "public void BeginReturn()",
                "public void CompleteReturn()");
            Require(
                beginReturn,
                "_readModel.RepairCargoCustody != RepairCargoCustody.Vehicle");
            Require(beginReturn, "new FreezeReturnPayloadCommand(");
            TestHarness.True(
                beginReturn.IndexOf(
                    "_readModel.RepairCargoCustody",
                    StringComparison.Ordinal) <
                beginReturn.IndexOf(
                    "_readModel.LiquidCargoKind",
                    StringComparison.Ordinal),
                "repair cargo custody must gate return before optional tank cargo");
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
            Require(
                globalShortcuts,
                "_readModel.IsRepairCargoLoadAvailable");
            Require(globalShortcuts, "LoadDepotRepairCargo();");
            Require(globalShortcuts, "keyboard.eKey.wasPressedThisFrame");
            Require(globalShortcuts, "gamepad.buttonSouth.wasPressedThisFrame");
            Require(globalShortcuts, "keyboard.rKey.wasPressedThisFrame");
            Require(globalShortcuts, "gamepad.buttonNorth.wasPressedThisFrame");
            Require(globalShortcuts, "RecoverRoadPresentation();");
            int recoveryHotkey = globalShortcuts.IndexOf(
                "RecoverRoadPresentation();",
                StringComparison.Ordinal);
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
            int cargoGate = globalShortcuts.IndexOf(
                "_readModel.IsRepairCargoLoadAvailable",
                StringComparison.Ordinal);
            int cargoHotkey = globalShortcuts.IndexOf(
                "keyboard.eKey.wasPressedThisFrame",
                cargoGate,
                StringComparison.Ordinal);
            TestHarness.True(
                recoveryHotkey >= 0 && recoveryHotkey < wreckGate &&
                wreckGate >= 0 && wreckHotkey > wreckGate &&
                depotGate > wreckHotkey && depotHotkey > depotGate &&
                cargoGate > depotHotkey && cargoHotkey > cargoGate,
                "manual recovery must precede context-gated road interactions");
            TestHarness.Equal(
                1,
                CountOccurrences(
                    globalShortcuts,
                    "RecoverRoadPresentation();"),
                "the explicit shortcut must be the only automatic update-loop call");
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
            Require(controller, "_world.ApplyRepairCargoPresentation(");
            Require(controller, "_world.ApplyCityImprovement(");
            Require(controller, "_world.ApplyGaragePreparationProgress(");
            Require(controller, "_readModel.PreparationElapsedTicks");
            Require(controller, "_readModel.PreparationRequiredTicks");
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
                "RepairCargoTransferred",
                "ReturnPayloadFrozen",
                "VehicleReturned",
                "CityReturnCredited",
                "TurbineRepaired",
                "CityImprovementInstalled",
                "SpareBearingBatchStarted",
                "SpareBearingBatchCheckpointReached",
                "SpareBearingBatchCompleted",
                "SpareBearingLotCreated",
                "SpareBearingLotBartered",
                "RoutePermitGranted",
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
            Require(camera, "public const float StrategyFieldOfView = 40f;");
            Require(camera, "SetRoadChaseActive");
            Require(camera, "IsRoadChaseActive");
            Require(camera, "IsRoadChaseRecoveryRequired");
            Require(camera, "HasConfiguredRoadChase");
            Require(camera, "if (_roadChaseActive)");
            Require(camera, "_camera.fieldOfView = StrategyFieldOfView;");
            Require(camera, "FailClosedIfRoadChaseOwnershipWasLost");
            Require(camera, "EndRoadChaseOwnership");
            Require(camera, "TryRecoverRoadChaseOwnership");
            Require(camera, "ResetRoadChaseFailure");
            Require(camera, "_roadChaseRecoveryRequired = true;");
            Require(
                camera,
                "LAST_BEARING_CHASE_CAMERA_DISABLED ownership-lost");
            Require(roadChaseCamera, "public const float BaseFieldOfView = 62f;");
            Require(roadChaseCamera, "public const float MaximumFieldOfView = 67f;");
            Require(roadChaseCamera, "public bool IsConfigured");
            Require(roadChaseCamera, "public void SetChaseActive(bool active)");
            Require(roadChaseCamera, "public void SnapBehind()");
            Require(roadChaseCamera, "_camera.fieldOfView = BaseFieldOfView;");
            Require(world, "RoadFeelRigFactory.Create");
            Require(world, "RoadFeelRigInstance");
            Require(world, "drivingModeRoot");
            string cameraBuild = Segment(
                world,
                "private void BuildCamera()",
                "private Material CreateMaterial(");
            Require(cameraBuild, "cameraObject.AddComponent<Camera>()");
            Require(cameraBuild, "cameraObject.AddComponent<AudioListener>()");
            Require(
                cameraBuild,
                "cameraObject.AddComponent<RoadFeelChaseCamera>()");
            Require(
                cameraBuild,
                "cameraObject.AddComponent<LastBearingCameraRig>()");
            TestHarness.Equal(
                1,
                CountOccurrences(cameraBuild, "AddComponent<Camera>()"),
                "the canonical world must build exactly one shared Camera");
            TestHarness.Equal(
                1,
                CountOccurrences(cameraBuild, "AddComponent<AudioListener>()"),
                "the canonical world must build exactly one shared AudioListener");
            TestHarness.Equal(
                1,
                CountOccurrences(
                    cameraBuild,
                    "AddComponent<RoadFeelChaseCamera>()"),
                "the canonical world must build exactly one chase writer");
            TestHarness.Equal(
                1,
                CountOccurrences(
                    cameraBuild,
                    "AddComponent<LastBearingCameraRig>()"),
                "the canonical world must build exactly one strategy writer");
            Require(world, "LastBearingDepotApproachRecoveryView");
            Require(world, "DepotApproachRecoveryView.Build(");
            Require(world, "LastBearingDepotCargoLoadingView");
            Require(world, "DepotCargoLoadingView.Build(");
            Require(world, "ApplyRepairCargoPresentation(");
            Require(world, "BindVehicleCargoSockets(");
            Require(world, "SashaScoutSemanticContract.CargoSocket01Name");
            Require(world, "if (roadCargoSocket == null)");
            Require(
                world,
                "Road Feel scout cargo socket is required for depot cargo custody.");
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
            Require(
                comparison,
                "empty-calibration-sled-recycler-to-workshop");
            Require(comparison, "Individually Placed Recycler");
            Require(comparison, "Individually Placed Workshop");
            Require(comparison, "Stamped Shared Logistics Apron");
            Require(comparison, "Shared Empty Calibration Sled");
            Require(comparison, "RecordPathRead");
            Require(comparison, "HasCompletedObservation");
            foreach (string forbidden in new[]
            {
                "AtomicLandPirate.Simulation",
                "LastBearingState",
                "LastBearingReadModel",
                "LastBearingKernel",
                "LastBearingCommand",
                "SaveContracts",
                "PlayerPrefs",
                "System.IO",
                "File.",
                "UnityWebRequest",
                "NavMesh",
            })
            {
                TestHarness.True(
                    comparison.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "city comparison contains forbidden authority " + forbidden);
            }
            Require(world, "LastBearingCityServiceCellView");
            Require(world, "BuildCityServiceCell(");
            Require(world, "CityServiceCellView?.Apply(");
            Require(cityServiceCell, "LastBearingReadModel model");
            Require(cityServiceCell, "CityBuildingKind previewBuilding");
            Require(cityServiceCell, "Working Cell Pad ");
            Require(cityServiceCell, "Canonical Recycler");
            Require(cityServiceCell, "Canonical Machine Shop");
            Require(cityServiceCell, "Canonical Emergency Storage");
            Require(cityServiceCell, "Canonical Parts Sled");
            Require(cityServiceCell, "collider.enabled = false");
            foreach (string forbidden in new[]
            {
                "LastBearingKernel",
                "LastBearingCommand",
                "Queue(",
                "SaveContracts",
                "PlayerPrefs",
                "System.IO",
                "File.",
                "AddComponent<Rigidbody>",
            })
            {
                TestHarness.True(
                    cityServiceCell.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "working service cell view contains forbidden authority " +
                    forbidden);
            }
            string cityTrialController = Segment(
                controller,
                "public void SelectCityGrammarHypothesis(",
                "public void ResetCityGrammarComparison()");
            TestHarness.True(
                cityTrialController.IndexOf("Queue(", StringComparison.Ordinal) < 0,
                "reversible city trial verbs must not queue canonical commands");
            TestHarness.True(
                cityTrialController.IndexOf(
                    "LastBearingCommand",
                    StringComparison.Ordinal) < 0,
                "reversible city trial verbs must not construct canonical commands");
            string infrastructureActivation = Segment(
                controller,
                "public void ActivateInfrastructure()",
                "public void BeginGaragePlan(");
            Require(
                infrastructureActivation,
                "HasCompletedCityGrammarObservation");
            Require(
                infrastructureActivation,
                "new ActivateSliceInfrastructureCommand(sequence)");
            Require(
                infrastructureActivation,
                "records no layout and D-0030 remains open");
            Require(controller, "public void SelectCityBuildingPreview(");
            Require(controller, "public void MoveCityBuildingPreview(");
            Require(controller, "public void RotateCityBuildingPreview()");
            Require(controller, "public void CancelCityBuildingPreview()");
            Require(controller, "public void PlaceCityBuildingPreview()");
            Require(controller, "new PlaceCityBuildingCommand(");
            Require(controller, "public void ConnectCityServiceLink()");
            Require(controller, "new ConnectCityServiceLinkCommand(sequence)");
            Require(controller, "public void AssignCityServiceResident(");
            Require(controller, "new AssignCityServiceResidentCommand(");
            Require(controller, "public void AdvanceCityServiceSled()");
            Require(controller, "new AdvanceCityServiceSledCommand(");

            string beginGaragePlan = Segment(
                controller,
                "public void BeginGaragePlan(",
                "public void CommitGaragePlan(");
            Require(beginGaragePlan, "PreparationChoice.WorkshopPush");
            Require(beginGaragePlan, "PreparationChoice.CivicBuffer");
            Require(
                beginGaragePlan,
                "_readModel.NextObjective != \"select-preparation-and-module\"");
            Require(
                beginGaragePlan,
                "LastBearingPresentationMode.GarageBay");
            Require(beginGaragePlan, "_world?.ApplyGaragePlanIntent(");
            foreach (string forbidden in new[]
            {
                "Queue(",
                "LastBearingCommand",
                "SelectPreparationCommand",
                "InstallVehicleModuleCommand",
                "Save();",
                "_saveAdapter",
                "_state =",
            })
            {
                TestHarness.True(
                    beginGaragePlan.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "beginning a garage plan crosses transient intent boundary " +
                    forbidden);
            }

            string commitGaragePlan = Segment(
                controller,
                "public void CommitGaragePlan(",
                "public void CancelGaragePlan()");
            Require(commitGaragePlan, "IsGaragePlanIntentActive");
            Require(commitGaragePlan, "VehicleModule.WinchAssembly");
            Require(commitGaragePlan, "VehicleModule.SealedRangeTank");
            Require(commitGaragePlan, "IsGaragePlanCommitAvailable");
            Require(commitGaragePlan, "new SelectPreparationCommand(");
            Require(commitGaragePlan, "new InstallVehicleModuleCommand(");
            Require(commitGaragePlan, "ClearGaragePlanIntent();");
            TestHarness.Equal(
                1,
                CountOccurrences(
                    commitGaragePlan,
                    "new SelectPreparationCommand("),
                "garage commit must queue the existing preparation command once");
            TestHarness.Equal(
                1,
                CountOccurrences(
                    commitGaragePlan,
                    "new InstallVehicleModuleCommand("),
                "garage commit must queue the existing module command once");

            string cancelGaragePlan = Segment(
                controller,
                "public void CancelGaragePlan()",
                "public void ChoosePlan(");
            Require(cancelGaragePlan, "ClearGaragePlanIntent();");
            Require(
                cancelGaragePlan,
                "LastBearingPresentationMode.CityOverview");
            TestHarness.True(
                cancelGaragePlan.IndexOf("Queue(", StringComparison.Ordinal) < 0,
                "canceling a garage plan must not queue a canonical command");
            TestHarness.True(
                hud.IndexOf(".ChoosePlan(", StringComparison.Ordinal) < 0,
                "the HUD must not retain a global composite planning action");
            Require(hud, ".BeginGaragePlan(");
            Require(hud, ".CommitGaragePlan(");
            Require(hud, ".CancelGaragePlan();");
            Require(world, "ApplyGaragePlanIntent(PreparationChoice preparation)");
            Require(controller, "_world.ApplyGaragePlanIntent(");

            Require(garage, "GaragePlanMarkerPresentation");
            Require(garage, "PLAN_MARKER_WORKSHOP_PUSH");
            Require(garage, "PLAN_MARKER_CIVIC_BUFFER");
            Require(garage, "public GaragePlanMarkerPresentation ActivePlanMarker");
            Require(garage, "public void ApplyPlanMarker(");
            Require(garage, "IsWorkshopPushPlanMarkerVisible");
            Require(garage, "IsCivicBufferPlanMarkerVisible");
            foreach (string forbidden in new[]
            {
                "AtomicLandPirate.Simulation",
                "LastBearingState",
                "LastBearingReadModel",
                "LastBearingKernel",
                "LastBearingCommand",
                "SaveContracts",
                "PlayerPrefs",
                "System.IO",
                "File.",
                "Rigidbody",
                "Physics.",
                "OnTrigger",
                "OnCollision",
                "Keyboard.current",
                "Gamepad.current",
                "AddComponent<Camera>",
                "CharacterController",
            })
            {
                TestHarness.True(
                    garage.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "garage planning view contains forbidden authority " +
                    forbidden);
            }

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
            Require(modeCoordinator, "CanRecoverRoadPresentation");
            Require(modeCoordinator, "SetRoadChaseActive(");
            Require(modeCoordinator, "SetRoadModeActiveOrThrow");
            Require(modeCoordinator, "adapter.IsRoadModeActive != active");
            string manualRecovery = Segment(
                modeCoordinator,
                "public bool TryRecoverRoadPresentation()",
                "public static LastBearingPresentationMode ResolveMode(");
            Require(manualRecovery, "active: false");
            Require(manualRecovery, "_roadPresentationActive = false;");
            Require(manualRecovery, "canonicalVehicle.SnapToCanonicalRoadPose()");
            Require(manualRecovery, "adapter.SynchronizePresentationPose(");
            Require(manualRecovery, "active: true");
            Require(manualRecovery, "_roadPresentationActive = true;");
            Require(manualRecovery, "TryRecoverRoadChaseOwnership()");
            int manualSuspend = manualRecovery.IndexOf(
                "active: false",
                StringComparison.Ordinal);
            int localYield = manualRecovery.IndexOf(
                "_roadPresentationActive = false;",
                manualSuspend,
                StringComparison.Ordinal);
            int canonicalSnap = manualRecovery.IndexOf(
                "canonicalVehicle.SnapToCanonicalRoadPose()",
                localYield,
                StringComparison.Ordinal);
            int presentationSync = manualRecovery.IndexOf(
                "adapter.SynchronizePresentationPose(",
                canonicalSnap,
                StringComparison.Ordinal);
            int manualReactivate = manualRecovery.IndexOf(
                "active: true",
                presentationSync,
                StringComparison.Ordinal);
            int chaseReclaim = manualRecovery.IndexOf(
                "_roadPresentationActive = true;",
                manualReactivate,
                StringComparison.Ordinal);
            int cameraReclaim = manualRecovery.IndexOf(
                "TryRecoverRoadChaseOwnership()",
                chaseReclaim,
                StringComparison.Ordinal);
            TestHarness.True(
                manualSuspend >= 0 && localYield > manualSuspend &&
                canonicalSnap > localYield && presentationSync > canonicalSnap &&
                manualReactivate > presentationSync &&
                chaseReclaim > manualReactivate &&
                cameraReclaim > chaseReclaim,
                "manual recovery must suspend, yield camera, snap, synchronize, " +
                "reactivate physics, then explicitly reclaim chase ownership");
            foreach (string forbidden in new[]
            {
                "Queue(",
                "Save(",
                "_saveAdapter",
                "_pendingCommands",
                "_state",
                "_readModel",
                "LastBearingState",
                "LastBearingKernel",
                "LastBearingCommand",
                "LastBearingCanonicalCodec",
                "RoadFeelTelemetry",
                ".Telemetry",
                "System.IO",
                "File.",
                "PlayerPrefs",
                "Application.persistentDataPath",
            })
            {
                TestHarness.True(
                    manualRecovery.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "manual recovery contains forbidden authority " + forbidden);
            }

            TestHarness.Equal(
                1,
                CountOccurrences(
                    modeCoordinator,
                    "TryRecoverRoadPresentation()"),
                "manual recovery must never be invoked automatically by the coordinator");
            string controllerRecovery = Segment(
                controller,
                "public bool RecoverRoadPresentation()",
                "public void CommitExpedition()");
            Require(controllerRecovery, "TryRecoverRoadPresentation()");
            foreach (string forbidden in new[]
            {
                "Queue(",
                "Save(",
                "_saveAdapter",
                "_pendingCommands",
                "_state",
                "_readModel",
                "LastBearingState",
                "LastBearingReadModel",
                "LastBearingKernel",
                "LastBearingCommand",
                "LastBearingCanonicalCodec",
                "RoadFeelTelemetry",
                ".Telemetry",
                "System.IO",
                "File.",
                "PlayerPrefs",
                "Application.persistentDataPath",
            })
            {
                TestHarness.True(
                    controllerRecovery.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "controller recovery contains forbidden authority " + forbidden);
            }
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
            Require(recoveryHold, "SetRoadModeActiveOrThrow");
            Require(recoveryHold, "active: false");
            Require(recoveryHold, "_canonicalVehicle.SnapToCanonicalRoadPose()");
            Require(recoveryHold, "adapter.SynchronizePresentationPose(");
            TestHarness.True(
                recoveryHold.IndexOf(
                    "active: false",
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
            Require(roadActivation, "SetRoadModeActiveOrThrow");
            Require(roadActivation, "active: true");
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
                    "active: true",
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
            Require(hud, "model.IsRepairCargoLoadAvailable");
            Require(hud, "_controller!.LoadDepotRepairCargo();");
            Require(hud, "LOAD FIELD SLEEVE · E / GAMEPAD SOUTH");
            Require(hud, "LOAD CERAMIC BEARING · E / GAMEPAD SOUTH");
            Require(hud, ".Append(model.RepairCargoCustody)");
            Require(hud, "IsCityImprovementInstallationAvailable");
            Require(hud, "INSTALL REFURBISHED AUXILIARY PUMP");
            Require(hud, "LastBearingPermitJobPresenter.Present(");
            Require(hud, "DrawPermitJob(permitJob)");
            Require(hud, "DrawContextActions(model, permitJob)");
            Require(hud, "GUILayout.Label(permitJob.ProgressLabel");
            Require(hud, "bool opensPermitJob");
            Require(hud, "RECOMMENDED FIRST RUN");
            Require(hud, "PERMIT JOB");
            TestHarness.True(
                hud.IndexOf(
                    "GUILayout.Label(model.NextObjective",
                    StringComparison.Ordinal) < 0,
                "raw objective identifiers must not be the player-facing fallback");
            TestHarness.True(
                hud.IndexOf(
                    "Append(model.NextObjective)",
                    StringComparison.Ordinal) < 0,
                "raw objective identifiers must not leak through civic instruments");
            TestHarness.True(
                hud.IndexOf(
                    "FUTURE TOLL 2 FUEL",
                    StringComparison.Ordinal) < 0,
                "finale facts must remain presenter-derived instead of HUD-hardcoded");
            Require(permitJobPresenter, "public static class LastBearingPermitJobPresenter");
            Require(permitJobPresenter, "public static LastBearingPermitJobPresentation Present(");
            Require(permitJobPresenter, "IsPermitJobFinale");
            Require(permitJobPresenter, "PresentAlternateConclusion");
            Require(
                permitJobPresenter,
                "model.IsRepairCargoLoadAvailable");
            Require(
                permitJobPresenter,
                "Load the repair cargo with E or gamepad south.");
            foreach (string forbidden in new[]
            {
                "using UnityEngine",
                "LastBearingState",
                "LastBearingKernel",
                "LastBearingCommand",
                "SaveContracts",
                "System.IO",
            })
            {
                TestHarness.True(
                    permitJobPresenter.IndexOf(
                        forbidden,
                        StringComparison.Ordinal) < 0,
                    "Permit Job presenter contains forbidden authority " +
                    forbidden);
            }
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

            Require(depotCargoLoading, "C0-VGR-10");
            Require(depotCargoLoading, "Revision = \"R1\"");
            Require(
                depotCargoLoading,
                "poi_depot_repair_cargo_load_a");
            Require(
                depotCargoLoading,
                "ANCHOR_DEPOT_REPAIR_CARGO_LOAD");
            Require(
                depotCargoLoading,
                "RepairCargoCustody.Depot");
            Require(
                depotCargoLoading,
                "RepairCargoCustody.Faction");
            Require(
                depotCargoLoading,
                "RepairCargoCustody.Vehicle");
            Require(
                depotCargoLoading,
                "BindVehicleCargoSockets(");
            Require(depotCargoLoading, "collider.enabled = false");
            foreach (string forbidden in new[]
            {
                "LastBearingState",
                "LastBearingReadModel",
                "LastBearingKernel",
                "LastBearingCommand",
                "SaveContracts",
                "System.IO",
                "Application.persistentDataPath",
                "Rigidbody",
                "Physics.",
                "OnTrigger",
                "OnCollision",
                "Keyboard.current",
                "Gamepad.current",
                "AddComponent<Camera>",
                "CharacterController",
            })
            {
                TestHarness.True(
                    depotCargoLoading.IndexOf(
                        forbidden,
                        StringComparison.Ordinal) < 0,
                    "depot cargo-loading view contains forbidden authority " +
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

            Require(oneGoodBatch, "C3-VGR-05-CANDIDATE");
            Require(oneGoodBatch, "bld_machine_shop_claims_wicket_a");
            Require(oneGoodBatch, "LOT_SPARE_BEARING_ONE_GOOD_BATCH");
            Require(oneGoodBatch, "PHYSICAL_INPUT_PART_01");
            Require(oneGoodBatch, "PHYSICAL_INPUT_PART_02");
            Require(oneGoodBatch, "PERSISTENT_TWO_FUEL_TOLL_TERMS");
            Require(oneGoodBatch, "FUTURE_TOLL_FUEL_UNIT_01");
            Require(oneGoodBatch, "FUTURE_TOLL_FUEL_UNIT_02");
            Require(oneGoodBatch, "_bearingLot.transform.SetParent(custodyAnchor!");
            Require(oneGoodBatch, "SpareBearingLotCustody.WorkshopOutput");
            Require(oneGoodBatch, "SpareBearingLotCustody.LastBearingClaimsCounter");
            Require(oneGoodBatch, "HasRoof => false");
            Require(oneGoodBatch, "HasNearWall => false");
            Require(oneGoodBatch, "collider.enabled = false");
            foreach (string forbidden in new[]
            {
                "Rigidbody",
                "RoadFeelTelemetry",
                "Physics.",
                "OnTrigger",
                "OnCollision",
                "LastBearingKernel",
                "LastBearingCommand",
                "SaveContracts",
                "System.IO",
                "Application.persistentDataPath",
                "Keyboard.current",
                "Gamepad.current",
                "price",
                "currency",
                "order book",
            })
            {
                TestHarness.True(
                    oneGoodBatch.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "One Good Batch cutaway contains forbidden authority or market grammar " +
                    forbidden);
            }

            Require(world, "SelectPumpHallCutaway");
            Require(world, "SelectOneGoodBatchCutaway");
            Require(world, "ApplyOneGoodBatch(");
            Require(world, "ApplyGaragePreparationProgress(");
            Require(hud, "ONE GOOD BATCH");
            Require(hud, "ONE-OFF BARTER · CARAVAN EXCHANGE CLOSED");
            Require(hud, "Future route toll  ");
            Require(hud, "model.FutureRouteTollFuelUnits");
            Require(recovery, "ApplyRoutePermit(bool granted)");
            Require(recovery, "Permit Locked Horizontal Crossbar");
            Require(recovery, "Permit Raised Vertical Arm");

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
            Require(dispatcher, "wp0002-gate-dispatcher-v3");
            Require(dispatcher, "wp0002-native-il2cpp-arm64-build");
            Require(
                dispatcher,
                "wp0002-native-il2cpp-arm64-performance-start");
            Require(
                dispatcher,
                "wp0002-native-il2cpp-arm64-performance-collect");
            Require(dispatcher, "Arguments = NativeRuntimeFlag,");
            Require(dispatcher, "UseShellExecute = false");
            Require(
                dispatcher,
                "DEVELOPER_DIR\"] =\n                XcodeDeveloperDirectory");
            Require(
                dispatcher,
                "/Applications/Xcode.app/Contents/Developer");
            Require(dispatcher, "private const string RequiredXcodeVersion = \"26.3\"");
            Require(
                dispatcher,
                "private const string RequiredXcodeBuildVersion = \"17C529\"");
            Require(
                dispatcher,
                "private const string RequiredMacOsSdkVersion = \"26.2\"");
            Require(
                dispatcher,
                "5dcf81c5df5a9ff35006ee05832a1a6194c60fc4a386df652b9f49ea3a2a238b");
            Require(
                dispatcher,
                "/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app");
            Require(dispatcher, "Contents/MacOS/Unity");
            Require(
                dispatcher,
                "SashaAtomicLandPirateVGR13.app/Contents/MacOS/Game");
            TestHarness.True(
                dispatcher.IndexOf(
                    "SashaAtomicLandPirateVGR13.app/Contents/MacOS/\" +\n" +
                    "            \"Sasha the Atomic Land Pirate",
                    StringComparison.Ordinal) < 0,
                "native player executable identity must not fall back to the " +
                "title-derived path");
            Require(
                dispatcher,
                "IsExpectedUnityEditorApplicationPath(");
            Require(
                dispatcher,
                "unity-editor-application-bundle-path-mismatch");
            string nativeIdentity = Segment(
                dispatcher,
                "private static NativeSourceIdentity VerifyNativeGateContract(",
                "private static bool IsExpectedUnityEditorApplicationPath(");
            Require(
                nativeIdentity,
                "RequireRegularDirectory(\n                unityApplicationBundle,");
            Require(
                nativeIdentity,
                "Path.Combine(\n                    unityApplicationBundle,\n" +
                "                    UnityEditorExecutableRelativePath)");
            Require(nativeIdentity, "RequireRegularFile(\n                unityExecutable,");
            Require(nativeIdentity, "ComputeSha256(unityExecutable)");
            TestHarness.True(
                nativeIdentity.IndexOf(
                    "Path.GetFullPath(EditorApplication.applicationPath)",
                    StringComparison.Ordinal) < 0,
                "native identity must not compare the reported application " +
                "bundle with the nested executable");
            Require(
                dispatcher,
                "8910e7a24ef01bb0c2d4e66c07b14c321cd150a8017ca17cfa335bd888182ec1");
            Require(dispatcher, "private const int NativeWarmupSeconds = 300");
            Require(dispatcher, "public double requested_warmup_seconds;");
            Require(dispatcher, "private const int NativePausedSeconds = 300");
            Require(dispatcher, "private const int NativeUnpausedSeconds = 300");
            Require(dispatcher, "private const int NativeCityGarageCycles = 100");
            Require(dispatcher, "private const int NativeScreenWidth = 2560");
            Require(dispatcher, "private const int NativeScreenHeight = 1600");
            Require(dispatcher, "request_runtime_identity_matched");
            Require(dispatcher, "ComputeNativeSourceTreeSha256(");
            Require(dispatcher, "ComputeDirectoryTreeSha256(playerPath)");
            Require(dispatcher, "ComputeSha256(executablePath)");
            Require(dispatcher, "ComputeSha256(identityPayload)");
            Require(dispatcher, "ComputeSha256(requestPayload)");
            Require(dispatcher, "ComputeSha256(runPayload)");
            Require(dispatcher, "ComputeSha256(reportPayload)");
            Require(
                dispatcher,
                "native-gate-authorization-receipt-missing");
            Require(
                dispatcher,
                "SUPERSEDE-WP0002-GATE-DISPATCHER-V3-PLAYER-EXECUTABLE-");
            Require(
                dispatcher,
                "AUTHORIZE-WP0002-NATIVE-PLAYER-EXECUTABLE-PATH-");
            Require(
                dispatcher,
                "NativePreviousAuthorizationReceiptSha256");
            Require(
                dispatcher,
                "native-gate-receipt-does-not-match-protected-main");
            Require(
                dispatcher,
                "native-gate-control-squash-path-set-mismatch");
            Require(
                dispatcher,
                "native-build-lacks-same-editor-attestation");
            Require(
                dispatcher,
                "native-run-lacks-same-editor-attestation");
            Require(
                dispatcher,
                "RequirePathComponentsNotReparse(");
            Require(
                dispatcher,
                "EditorApplication.delayCall += ExecuteScheduledNativeBuild");
            Require(
                dispatcher,
                "EditorApplication.wantsToQuit +=");
            Require(
                dispatcher,
                "EditorApplication.LockReloadAssemblies();");
            Require(
                dispatcher,
                "EditorApplication.UnlockReloadAssemblies();");
            Require(dispatcher, "_quarantinedNativeProcesses");
            Require(dispatcher, "private sealed class NativeProcessCleanup");
            Require(dispatcher, "private static bool TerminateProcess(");
            Require(dispatcher, "out string failure");
            Require(dispatcher, "native-player-termination-timeout");
            Require(dispatcher, "native-player-termination-unconfirmed");
            Require(dispatcher, "native-player-cleanup-quarantined:");
            Require(
                dispatcher,
                "native-player-quarantine-cleared-retry-gate");
            Require(dispatcher, "QuarantineNativeProcess(");
            Require(
                dispatcher,
                "AttemptQuarantinedNativeProcessCleanup()");
            string nativeStartOperation = Segment(
                dispatcher,
                "private static string StartNativePerformance(",
                "private static string CollectNativePerformance(");
            string nativeStartFailure = Segment(
                nativeStartOperation,
                "catch (Exception exception)",
                "return CompleteNativeGate(");
            Require(nativeStartFailure, "if (TerminateProcess(");
            Require(nativeStartFailure, "QuarantineNativeProcess(");
            TestHarness.True(
                nativeStartFailure.IndexOf(
                    "QuarantineNativeProcess(",
                    StringComparison.Ordinal) <
                nativeStartFailure.IndexOf(
                    "launchedProcess = null;",
                    StringComparison.Ordinal),
                "a failed native start must retain the exact process handle before clearing the local reference");
            string nativeInvalidation = Segment(
                dispatcher,
                "private static void InvalidateTrustedNativeRun()",
                "private static bool AllowEditorQuitAfterNativeCleanup()");
            TestHarness.True(
                nativeInvalidation.IndexOf(
                    "if (TerminateProcess(",
                    StringComparison.Ordinal) <
                nativeInvalidation.IndexOf(
                    "_trustedNativeRun = null;",
                    StringComparison.Ordinal),
                "trusted native run cleanup must verify termination before clearing trust");
            Require(nativeInvalidation, "QuarantineNativeProcess(");
            string nativeTermination = Segment(
                dispatcher,
                "private static bool TerminateProcess(",
                "private static string ResolveGitHead(");
            Require(nativeTermination, "if (!process.WaitForExit(");
            Require(nativeTermination, "if (!process.HasExited)");
            Require(dispatcher, "process.StandardOutput.ReadToEndAsync()");
            Require(
                dispatcher,
                "process.StandardOutput.BaseStream.CopyToAsync(output)");
            Require(dispatcher, "process.StandardError.ReadToEndAsync()");
            Require(dispatcher, "Task.WaitAll(");
            Require(dispatcher, "FixedChildProcessTimeoutMilliseconds");
            Require(dispatcher, "FixedChildCaptureTimeoutMilliseconds");
            Require(dispatcher, "CreateFixedChildFailure(");
            Require(
                dispatcher,
                "native-fixed-child-cleanup-quarantined:");
            TestHarness.True(
                dispatcher.IndexOf(
                    "process.StandardOutput.ReadToEnd()",
                    StringComparison.Ordinal) < 0 &&
                dispatcher.IndexOf(
                    "process.StandardError.ReadToEnd()",
                    StringComparison.Ordinal) < 0 &&
                dispatcher.IndexOf(
                    "process.StandardOutput.BaseStream.CopyTo(output)",
                    StringComparison.Ordinal) < 0,
                "fixed subprocess capture must not synchronously block before its timeout");
            TestHarness.True(
                nativeTermination.IndexOf(
                    "best-effort cleanup",
                    StringComparison.Ordinal) < 0,
                "native player cleanup must not swallow an unverified termination");
            Require(
                dispatcher,
                "receipt-required-fail-closed");
            Require(
                dispatcher,
                "BuildArtifacts/WP-0002/local-only/native-il2cpp-arm64");
            TestHarness.True(
                dispatcher.IndexOf(
                    "private const int NativeMeasurementSeconds = 1800",
                    StringComparison.Ordinal) < 0,
                "the VGR-13 control must not invent the future full-V0 phase");

            Require(
                nativeBuildProfile,
                "m_Name: WP0002 Native IL2CPP ARM64 Performance");
            Require(nativeBuildProfile, "m_BuildTarget: 2");
            Require(nativeBuildProfile, "m_Subtarget: 2");
            Require(nativeBuildProfile, "m_OverrideGlobalSceneList: 1");
            Require(
                nativeBuildProfile,
                "m_path: Assets/AtomicLandPirate/LastBearing/Scenes/LastBearing.unity");
            Require(nativeBuildProfile, "|   defaultScreenWidth: 2560");
            Require(nativeBuildProfile, "|   defaultScreenHeight: 1600");
            Require(nativeBuildProfile, "|     Standalone: 1");
            Require(nativeBuildProfile, "m_Development: 1");
            Require(nativeBuildProfile, "m_ConnectProfiler: 0");
            Require(nativeBuildProfile, "m_BuildWithDeepProfilingSupport: 0");
            Require(nativeBuildProfile, "m_AllowDebugging: 0");
            Require(nativeBuildProfile, "m_Architecture: 1");
            Require(nativeBuildProfile, "m_CreateXcodeProject: 0");
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

        private static int CountOccurrences(string source, string token)
        {
            var count = 0;
            var offset = 0;
            while (offset <= source.Length - token.Length)
            {
                int match = source.IndexOf(
                    token,
                    offset,
                    StringComparison.Ordinal);
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
