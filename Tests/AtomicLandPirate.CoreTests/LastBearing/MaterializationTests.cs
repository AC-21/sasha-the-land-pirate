#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AtomicLandPirate.LastBearingTests
{
    internal static class MaterializationTests
    {
        private static readonly string[] RequiredPaths =
        {
            "SimulationCore/Runtime/LastBearing.meta",
            "SimulationCore/Runtime/LastBearing/LastBearingTypes.cs",
            "SimulationCore/Runtime/LastBearing/ResidentRoster.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingState.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingBalanceV1.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingCommands.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingOwnershipTransaction.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingKernel.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingDomainEvent.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingReadModel.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingTickResult.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingInvariants.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingCanonicalCodec.cs",
            "SimulationCore/Runtime/LastBearing/LastBearingScenarioFactory.cs",
            "SimulationCore/Runtime/LastBearing/AssemblyInfo.LastBearing.cs",
            "SaveContracts/Runtime/LastBearing.meta",
            "SaveContracts/Runtime/LastBearing/LastBearingProfileContract.cs",
            "SaveContracts/Runtime/LastBearing/LastBearingGenerationCodec.cs",
            "SaveContracts/Runtime/LastBearing/LastBearingProfileStore.cs",
            "SaveContracts/Runtime/LastBearing/LastBearingFileOperations.cs",
            "SaveContracts/Runtime/LastBearing/LastBearingSaveResults.cs",
            "SaveContracts/Runtime/LastBearing/AssemblyInfo.LastBearing.cs",
            "Game/Assets/AtomicLandPirate/LastBearing.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingBootstrap.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingGameController.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingModeCoordinator.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingWorldBuilder.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingHud.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingPermitJobPresenter.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingCameraRig.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingCityGrammarComparison.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingCityServiceCellView.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingCityServiceCellView.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingCityServiceCellInteractor.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingCityServiceCellInteractor.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingDepotApproachRecoveryView.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingDepotApproachInteractor.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingDepotApproachInteractor.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingRouteModulePointView.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingWreckLineInteractor.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingWreckLineInteractor.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingPumpHallCutawayView.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingVehicleView.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingSaveAdapter.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/LastBearingFieldDesk.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/LastBearingFieldDesk.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/LastBearingFieldDeskPresenter.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/LastBearingFieldDeskPresenter.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/Resources.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/Resources/LastBearingFieldDeskLayout.uxml",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/Resources/LastBearingFieldDeskLayout.uxml.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/Resources/LastBearingFieldDeskStyles.uss",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/Resources/LastBearingFieldDeskStyles.uss.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/Resources/LastBearingFieldDeskTheme.tss",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/UI/Resources/LastBearingFieldDeskTheme.tss.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/RoadFeelContracts.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/RoadFeelSurface.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/RoadFeelVehicleController.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/RoadFeelChaseCamera.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/RoadFeelLabBootstrap.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/RoadFeelLabController.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/RoadFeelRigFactory.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/LastBearingRoadFeelModeAdapter.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/SashaScoutSemanticContract.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/SashaScoutVisual.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/SashaScoutBlockoutFactory.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/LastBearingGarageBayView.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/LastBearingGarageDepartureInteractor.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/LastBearingGarageDepartureInteractor.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/LastBearingGarageModuleInteractor.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/LastBearingGarageModuleInteractor.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Editor/WP0002GateDispatcher.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/LastBearingAdapterTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/LastBearingFieldDeskPresenterTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/LastBearingFieldDeskPresenterTests.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/LastBearingPermitJobPresenterTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/RoadFeelMathTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/SashaScoutPresentationTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingFieldDeskPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingFieldDeskPlayModeTests.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingBoltTheBellyPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingBoltTheBellyPlayModeTests.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingTurnTheKeyPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingTurnTheKeyPlayModeTests.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingFeelTheLoadPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingFeelTheLoadPlayModeTests.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingHaulTheFrameRailsPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingHaulTheFrameRailsPlayModeTests.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingWorkTheCisternPumpPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingWorkTheCisternPumpPlayModeTests.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingFaceTheDustFrontPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/LastBearingFaceTheDustFrontPlayModeTests.cs.meta",
            "Game/Assets/AtomicLandPirate/LastBearing/Tests/PlayMode/RoadFeelLabPlayModeTests.cs",
            "Game/Assets/AtomicLandPirate/LastBearing/Scenes/LastBearing.unity",
            "Game/Assets/AtomicLandPirate/LastBearing/Scenes/RoadFeelLab.unity",
            "Tests/AtomicLandPirate.CoreTests/LastBearing/AtomicLandPirate.LastBearingTests.csproj",
            "Tools/ScenarioRunner/run.py",
        };

        public static void CompleteUnionIsPresent(string repoRoot)
        {
            foreach (string relative in RequiredPaths)
            {
                TestHarness.True(
                    File.Exists(Path.Combine(repoRoot, relative)),
                    "first-materialization union is missing " + relative);
            }

            VerifyPackageGraph(repoRoot);
            VerifyBuildScene(repoRoot);
            VerifyGameMetadata(repoRoot);
            GameSourceContract.Verify(repoRoot);
            CityServiceCellInteractionSourceContract.Verify(repoRoot);
            FieldDeskSourceContract.Verify(repoRoot);
            RoadFeelSourceContract.Verify(repoRoot);
            SashaScoutSourceContract.Verify(repoRoot);
        }

        private static void VerifyPackageGraph(string repoRoot)
        {
            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(repoRoot, "Game/Packages/manifest.json")));
            using JsonDocument packageLock = JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(repoRoot, "Game/Packages/packages-lock.json")));
            JsonElement manifestDependencies = manifest.RootElement.GetProperty("dependencies");
            JsonElement lockDependencies = packageLock.RootElement.GetProperty("dependencies");
            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["com.ac21.sasha.save-contracts"] = "file:../../SaveContracts",
                ["com.ac21.sasha.simulation-core"] = "file:../../SimulationCore",
            };
            foreach (KeyValuePair<string, string> pair in expected)
            {
                TestHarness.Equal(
                    pair.Value,
                    manifestDependencies.GetProperty(pair.Key).GetString(),
                    pair.Key + " manifest link");
                JsonElement entry = lockDependencies.GetProperty(pair.Key);
                TestHarness.Equal(pair.Value, entry.GetProperty("version").GetString(), "lock version");
                TestHarness.Equal(0, entry.GetProperty("depth").GetInt32(), "lock depth");
                TestHarness.Equal("local", entry.GetProperty("source").GetString(), "lock source");
                TestHarness.Equal(
                    0,
                    entry.GetProperty("dependencies").GetRawText() == "{}" ? 0 : 1,
                    "lock dependency map");
            }
        }

        private static void VerifyBuildScene(string repoRoot)
        {
            string text = File.ReadAllText(
                Path.Combine(repoRoot, "Game/ProjectSettings/EditorBuildSettings.asset"));
            int lastBearing = text.IndexOf(
                "path: Assets/AtomicLandPirate/LastBearing/Scenes/LastBearing.unity",
                StringComparison.Ordinal);
            int sandbox = text.IndexOf(
                "path: Assets/AtomicLandPirate/TechnicalSandbox/Scenes/WP0003_TechnicalSandbox.unity",
                StringComparison.Ordinal);
            int roadFeel = text.IndexOf(
                "path: Assets/AtomicLandPirate/LastBearing/Scenes/RoadFeelLab.unity",
                StringComparison.Ordinal);
            TestHarness.True(lastBearing >= 0, "Last Bearing scene is absent from build settings");
            TestHarness.True(sandbox > lastBearing, "Last Bearing scene is not build index zero");
            TestHarness.True(roadFeel > lastBearing, "Road Feel Lab scene is absent from build settings");
            TestHarness.True(roadFeel < sandbox, "Road Feel Lab must precede the technical sandbox");
        }

        private static void VerifyGameMetadata(string repoRoot)
        {
            string root = Path.Combine(
                repoRoot,
                "Game/Assets/AtomicLandPirate/LastBearing");
            foreach (string entry in Directory.EnumerateFileSystemEntries(
                root,
                "*",
                SearchOption.AllDirectories))
            {
                if (entry.EndsWith(".meta", StringComparison.Ordinal))
                {
                    continue;
                }

                TestHarness.True(
                    File.Exists(entry + ".meta"),
                    "Game asset lacks committed metadata: " + entry.Substring(root.Length + 1));
            }
        }
    }
}
