#nullable enable

using System;
using System.IO;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class RoadFeelSourceContract
    {
        private static readonly string[] ForbiddenRuntimeTokens =
        {
            "WheelCollider",
            "AtomicLandPirate.Simulation",
            "LastBearingKernel",
            "System.IO",
            "UnityEngine.Random",
            "Application.persistentDataPath",
        };

        public static void Verify(string repoRoot)
        {
            string root = Path.Combine(
                repoRoot,
                "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel");
            string contracts = Read(root, "RoadFeelContracts.cs");
            string vehicle = Read(root, "RoadFeelVehicleController.cs");
            string surface = Read(root, "RoadFeelSurface.cs");
            string camera = Read(root, "RoadFeelChaseCamera.cs");
            string lab = Read(root, "RoadFeelLabController.cs");
            string bootstrap = Read(root, "RoadFeelLabBootstrap.cs");
            string modeAdapter = Read(root, "LastBearingRoadFeelModeAdapter.cs");

            Require(vehicle, "FixedUpdate");
            Require(vehicle, "AddForceAtPosition");
            Require(vehicle, "SpringRateFromSag");
            Require(vehicle, "SetControlInput");
            Require(vehicle, "SetLoad");
            Require(vehicle, "ResetAt");
            Require(vehicle, "RoadFeelTelemetry");
            Require(contracts, "ShouldApplyReverse");

            Require(surface, "RoadFeelSurfaceKind.Concrete");
            Require(surface, "RoadFeelSurfaceKind.Hardpack");
            Require(surface, "RoadFeelSurfaceKind.Gravel");
            Require(surface, "RoadFeelSurfaceKind.Sand");
            Require(surface, "RoadFeelSurfaceKind.Washboard");

            Require(camera, "SphereCastNonAlloc");
            Require(camera, "Vector3.up");
            Require(camera, "Gamepad.current");
            Require(camera, "Mouse.current");

            Require(lab, "Keyboard.current");
            Require(lab, "Gamepad.current");
            Require(lab, "SetControlInput");
            Require(lab, "SetLoad");
            Require(lab, "ResetAt");
            Require(lab, "RoadFeelSurfaceKind.Gravel");
            Require(lab, "RoadFeelSurfaceKind.Sand");
            Require(lab, "LastBearingRoadFeelModeAdapter");

            Require(modeAdapter, "ILastBearingRoadModeAdapter");
            Require(modeAdapter, "ApplyQuantizedCommandShadow");
            Require(modeAdapter, "SetControlInput");
            TestHarness.True(
                modeAdapter.IndexOf("RoadFeelTelemetry", StringComparison.Ordinal) < 0,
                "Road Feel mode adapter must not return physics telemetry");
            TestHarness.True(
                modeAdapter.IndexOf("LastBearingState", StringComparison.Ordinal) < 0,
                "Road Feel mode adapter must not receive canonical mutable state");

            Require(bootstrap, "RoadFeelLab");
            Require(bootstrap, "RuntimeInitializeOnLoadMethod");

            string timeManager = File.ReadAllText(Path.Combine(
                repoRoot,
                "Game/ProjectSettings/TimeManager.asset"));
            Require(timeManager, "Fixed Timestep: 0.02");

            foreach (string source in new[]
                     {
                         contracts,
                         vehicle,
                         surface,
                         camera,
                         lab,
                         bootstrap,
                         modeAdapter,
                     })
            {
                foreach (string token in ForbiddenRuntimeTokens)
                {
                    TestHarness.True(
                        source.IndexOf(token, StringComparison.Ordinal) < 0,
                        "Road Feel presentation contains forbidden token " + token);
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
                "Road Feel source contract is missing " + token);
        }
    }
}
