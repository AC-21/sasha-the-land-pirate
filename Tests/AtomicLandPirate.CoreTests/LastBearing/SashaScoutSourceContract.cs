#nullable enable

using System;
using System.IO;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class SashaScoutSourceContract
    {
        public static void Verify(string repoRoot)
        {
            string runtimeRoot = Path.Combine(
                repoRoot,
                "Game/Assets/AtomicLandPirate/LastBearing/Runtime");
            string vehicleRoot = Path.Combine(runtimeRoot, "Vehicle");
            string contract = Read(vehicleRoot, "SashaScoutSemanticContract.cs");
            string visual = Read(vehicleRoot, "SashaScoutVisual.cs");
            string factory = Read(vehicleRoot, "SashaScoutBlockoutFactory.cs");
            string garage = Read(vehicleRoot, "LastBearingGarageBayView.cs");
            string departure = Read(
                vehicleRoot,
                "LastBearingGarageDepartureInteractor.cs");
            string vehicleView = Read(runtimeRoot, "LastBearingVehicleView.cs");
            string world = Read(runtimeRoot, "LastBearingWorldBuilder.cs");
            string controller = Read(
                runtimeRoot,
                "LastBearingGameController.cs");
            string coordinator = Read(runtimeRoot, "LastBearingModeCoordinator.cs");
            string camera = Read(runtimeRoot, "LastBearingCameraRig.cs");
            string roadFactory = Read(
                Path.Combine(runtimeRoot, "RoadFeel"),
                "RoadFeelRigFactory.cs");

            foreach (string token in new[]
                     {
                         "C0-VGR-01",
                         "veh_sasha_scout_a",
                         "C0Blockout",
                         "WheelCount = 4",
                         "Lod0BaseMinimumTriangles = 24_000",
                         "Lod0BaseMaximumTriangles = 28_000",
                         "Lod0WithModuleMaximumTriangles = 32_000",
                         "Lod1MinimumTriangles = 9_000",
                         "Lod1MaximumTriangles = 12_000",
                         "Lod2MinimumTriangles = 3_000",
                         "Lod2MaximumTriangles = 5_000",
                         "ProductionMaterialSlotMaximum = 3",
                         "ProductionTextureSetSize = 2_048",
                         "SOCKET_UPGRADE_FRONT",
                         "SOCKET_UPGRADE_CARGO_01",
                         "SOCKET_UPGRADE_UNDERBODY",
                         "SOCKET_CARGO_01",
                         "SOCKET_CARGO_02",
                         "SOCKET_TOOL_DEPLOY",
                         "SOCKET_DRIVER_CAMERA",
                         "MODULE_WINCH_ASSEMBLY",
                         "MODULE_SEALED_RANGE_TANK",
                         "UPGRADE_PATCHWORK_SKID_PLATE",
                         "new Vector3(-1.12f, 0.62f, 1.55f)",
                         "new Vector3(1.12f, 0.62f, 1.55f)",
                         "new Vector3(-1.12f, 0.62f, -1.55f)",
                         "new Vector3(1.12f, 0.62f, -1.55f)",
                     })
            {
                Require(contract, token);
            }

            Require(visual, "HasProductionGeometry => false");
            Require(visual, "SashaScoutBlockoutMaterials Materials");
            Require(visual, "WinchModuleRoot");
            Require(visual, "RangeTankModuleRoot");
            Require(visual, "PatchworkSkidPlateUpgradeRoot");
            Require(visual, "IsPatchworkSkidPlateVisible");
            Require(visual, "ApplyModule");
            Require(visual, "ApplyUpgrade");
            Require(visual, "SetFrontSteering");
            Require(visual, "RotateWheels");
            Require(factory, "SashaScoutBlockoutFactory");
            Require(factory, "includeRoadCollisionShell");
            Require(factory, "RoadCollisionBoxCount = 7");
            Require(factory, "CopySocketPose");
            Require(factory, "SKID_PLATE_CENTER");
            Require(factory, "SKID_PLATE_FORE");
            Require(factory, "SKID_PLATE_AFT");
            Require(factory, "SKID_BRACE_LEFT");
            Require(factory, "SKID_BRACE_RIGHT");
            Require(factory, "collider.enabled = false");
            Require(factory, "collision.AddComponent<BoxCollider>()");
            TestHarness.True(
                factory.IndexOf("MeshCollider", StringComparison.Ordinal) < 0,
                "C0 scout must not use detailed render meshes as physics collision");

            Require(garage, "IsDollhouseCutaway => true");
            Require(garage, "HasRoof => false");
            Require(garage, "HasNearWall => false");
            Require(garage, "ANCHOR_GARAGE_CAMERA");
            Require(garage, "ANCHOR_GARAGE_FOCUS");
            Require(garage, "ANCHOR_VEHICLE_DOCK");
            Require(garage, "MODULE_STAND_WINCH");
            Require(garage, "MODULE_STAND_RANGE_TANK");
            Require(garage, "ASSEMBLY_PROGRESS_GAUGE");
            Require(garage, "ApplyPreparationProgress");
            Require(garage, "PreparationGaugeLitSegments");
            Require(garage, "RigUpgradeInstallPulseDurationSeconds");
            Require(garage, "public void PulseRigUpgradeInstall()");
            Require(garage, "public void ResetRigUpgradeInstallPulse()");
            Require(
                garage,
                "LastBearingGarageDepartureInteractor.RootName");
            Require(garage, "DepartureInteractor.Build(");
            TestHarness.True(
                garage.IndexOf("AddComponent<Camera>", StringComparison.Ordinal) < 0,
                "garage cutaway must reuse the one Last Bearing camera");
            TestHarness.True(
                garage.IndexOf("CharacterController", StringComparison.Ordinal) < 0,
                "garage cutaway must not add an on-foot controller");

            Require(vehicleView, "SashaScoutBlockoutFactory.Create");
            Require(vehicleView, "ScoutVisual");
            Require(roadFactory, "SashaScoutBlockoutFactory.Create");
            Require(roadFactory, "CopyContactStations");
            Require(roadFactory, "ScoutVisual");
            Require(roadFactory, "materials.Rubber");
            Require(world, "GarageBayView");
            Require(world, "RoadFeelRig?.ScoutVisual.ApplyModule(snapshot.Module)");
            Require(world, "GarageBayView?.ApplyModule(snapshot.Module)");
            Require(world, "GarageBayView?.ApplyPreparationProgress(");
            string upgradePresentation = Segment(
                world,
                "public void ApplyRigUpgrade(RigUpgrade upgrade)",
                "public void PulseRigUpgradeInstall()");
            Require(
                upgradePresentation,
                "upgrade == RigUpgrade.PatchworkSkidPlate");
            Require(
                upgradePresentation,
                "VehicleView?.ScoutVisual?.ApplyUpgrade(presentation)");
            Require(
                upgradePresentation,
                "RoadFeelRig?.ScoutVisual.ApplyUpgrade(presentation)");
            foreach (string forbidden in new[]
                     {
                         "LastBearingState",
                         "LastBearingKernel",
                         "LastBearingCommand",
                         "LastBearingCanonicalCodec",
                         "Rigidbody",
                         "Physics.",
                         "Save(",
                     })
            {
                TestHarness.True(
                    upgradePresentation.IndexOf(
                        forbidden,
                        StringComparison.Ordinal) < 0,
                    "scout upgrade presentation contains forbidden authority " +
                    forbidden);
            }

            string simulationTick = Segment(
                controller,
                "private void SimulateOneTick()",
                "private void QueueDriveInputIfApplicable()");
            Require(
                simulationTick,
                "LastBearingEventKind.RigUpgradeInstalled");
            Require(
                simulationTick,
                "_world?.PulseRigUpgradeInstall();");
            int acceptedEvent = simulationTick.IndexOf(
                "bool rigUpgradeInstalled = ContainsEvent(",
                StringComparison.Ordinal);
            int canonicalCommit = simulationTick.IndexOf(
                "_state = result.State;",
                StringComparison.Ordinal);
            int autosave = simulationTick.IndexOf(
                "TryAutosave(result.DomainEvents);",
                StringComparison.Ordinal);
            int presentation = simulationTick.IndexOf(
                "ApplyPresentation();",
                StringComparison.Ordinal);
            int pulse = simulationTick.IndexOf(
                "_world?.PulseRigUpgradeInstall();",
                StringComparison.Ordinal);
            TestHarness.True(
                acceptedEvent >= 0 &&
                canonicalCommit > acceptedEvent &&
                autosave > canonicalCommit &&
                presentation > autosave &&
                pulse > presentation,
                "upgrade worklight must pulse only after the accepted canonical " +
                "event is committed, saved, and rendered");
            TestHarness.Equal(
                1,
                CountOccurrences(
                    controller,
                    "_world?.PulseRigUpgradeInstall();"),
                "controller must expose one accepted-event pulse path");
            Require(
                controller,
                "_world.ApplyRigUpgrade(_readModel.RigUpgrade);");
            Require(coordinator, "garageInspectionSelected");
            Require(coordinator, "SetInspectionPose");
            Require(camera, "IsInspectionMode");
            Require(camera, "SetInspectionPose");
            Require(departure, "INTERACT_SASHA_SCOUT_DEPARTURE_CLAMP");
            Require(departure, "_controller.CommitExpedition();");
            Require(departure, "_controller?.Hud?.BlocksWorldPointer");
            Require(controller, "public bool CanCommitExpedition");
            Require(controller, "public bool IsGarageDepartureAvailable");
            Require(controller, "public bool IsExpeditionCommitQueued");
            string commit = Segment(
                controller,
                "public void CommitExpedition()",
                "public void ResolveDepot(");
            Require(commit, "if (IsExpeditionCommitQueued)");
            Require(commit, "if (!IsGarageDepartureAvailable)");
            Require(commit, "new PrepareExpeditionTransactionCommand(");
            Require(commit, "new DebitCityManifestCommand(");
            Require(commit, "new DepartExpeditionCommand(sequence)");
            foreach (string forbidden in new[]
                     {
                         "new PrepareExpeditionTransactionCommand",
                         "new DebitCityManifestCommand",
                         "new DepartExpeditionCommand",
                         "LastBearingKernel",
                         "LastBearingStateBuilder",
                         "LastBearingCanonicalCodec",
                         "Save(",
                     })
            {
                TestHarness.True(
                    departure.IndexOf(
                        forbidden,
                        StringComparison.Ordinal) < 0,
                    "garage departure interactor contains forbidden authority " +
                    forbidden);
            }

            foreach (string source in new[] { contract, visual, factory, garage })
            {
                foreach (string forbidden in new[]
                         {
                             "SaveContracts",
                             "AtomicLandPirate.Simulation",
                             "System.IO",
                             "Resources.Load",
                             ".fbx",
                             "Tripo",
                         })
                {
                    TestHarness.True(
                        source.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                        "C0 scout source contains forbidden production/runtime surface " +
                        forbidden);
                }
            }
        }

        private static string Read(string root, string name)
        {
            return File.ReadAllText(Path.Combine(root, name));
        }

        private static void Require(string source, string token)
        {
            TestHarness.True(
                source.IndexOf(token, StringComparison.Ordinal) >= 0,
                "Sasha Scout source contract is missing " + token);
        }

        private static string Segment(
            string source,
            string startToken,
            string endToken)
        {
            int start = source.IndexOf(
                startToken,
                StringComparison.Ordinal);
            int end = source.IndexOf(
                endToken,
                start >= 0 ? start : 0,
                StringComparison.Ordinal);
            TestHarness.True(
                start >= 0 && end > start,
                "Sasha Scout source segment is unavailable: " + startToken);
            return source.Substring(start, end - start);
        }

        private static int CountOccurrences(
            string source,
            string token)
        {
            var count = 0;
            var offset = 0;
            while ((offset = source.IndexOf(
                       token,
                       offset,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }

            return count;
        }
    }
}
