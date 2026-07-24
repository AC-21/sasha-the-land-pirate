#nullable enable

using System.Collections;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingWreckLineBitePlayModeTests
    {
        private const string SceneName = "LastBearing";

        private static readonly string[] CorridorSegmentNames =
        {
            LastBearingWorldBuilder.RouteApronName,
            LastBearingWorldBuilder.CollapsedShortBranchName,
            LastBearingWorldBuilder.ExposedLongRouteAName,
            LastBearingWorldBuilder.ExposedLongRouteBName,
        };

        private static readonly RoadFeelSurfaceKind[] CorridorSurfaceKinds =
        {
            RoadFeelSurfaceKind.Concrete,
            RoadFeelSurfaceKind.Washboard,
            RoadFeelSurfaceKind.Sand,
            RoadFeelSurfaceKind.Gravel,
        };

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene(
                    "LastBearing_WreckLineBite_TestCleanup");
                SceneManager.SetActiveScene(cleanup);
                AsyncOperation? unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator CorridorOwnsExactSurfaceGrammarAndCameraBoundary()
        {
            yield return LoadScene();

            LastBearingGameController controller =
                Object.FindAnyObjectByType<LastBearingGameController>();
            Assert.That(controller, Is.Not.Null);
            LastBearingWorldBuilder world = controller.World!;
            Transform corridor = FindDescendant(
                world.transform,
                LastBearingWorldBuilder.RoadCorridorRootName);

            RoadFeelSurface[] surfaces =
                corridor.GetComponentsInChildren<RoadFeelSurface>(true);
            Assert.That(surfaces, Has.Length.EqualTo(CorridorSegmentNames.Length));

            for (var index = 0; index < CorridorSegmentNames.Length; index++)
            {
                Transform segment = FindDescendant(
                    corridor,
                    CorridorSegmentNames[index]);
                RoadFeelSurface? surface =
                    segment.GetComponent<RoadFeelSurface>();

                Assert.That(
                    surface,
                    Is.Not.Null,
                    CorridorSegmentNames[index] + " must own its road feel.");
                Assert.That(
                    surface!.Kind,
                    Is.EqualTo(CorridorSurfaceKinds[index]));
                Assert.That(segment.GetComponent<Collider>(), Is.Not.Null);
            }

            Assert.That(
                Object.FindObjectsByType<Camera>(FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(world.CameraRig!.HasConfiguredRoadChase, Is.True);
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.RecoverRoadPresentation(), Is.False);
        }

        [UnityTest]
        public IEnumerator RigTelemetryReadsEveryCorridorSurfaceWithoutAuthority()
        {
            yield return LoadScene();

            LastBearingGameController controller =
                Object.FindAnyObjectByType<LastBearingGameController>();
            Assert.That(controller, Is.Not.Null);
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.enabled = false;

            LastBearingState canonicalState = controller.State!;
            string canonicalHash = controller.CanonicalHash;
            long globalTick = canonicalState.GlobalTick;
            long commandSequence = canonicalState.NextCommandSequence;
            LastBearingWorldBuilder world = controller.World!;
            LastBearingModeCoordinator coordinator =
                controller.ModeCoordinator!;
            RoadFeelRigInstance roadRig = world.RoadFeelRig!;
            Transform corridor = FindDescendant(
                world.transform,
                LastBearingWorldBuilder.RoadCorridorRootName);

            coordinator.GetModeRoot(
                    LastBearingPresentationMode.Driving)
                .gameObject.SetActive(true);
            roadRig.Adapter.SetRoadModeActive(true);
            roadRig.Adapter.ResetPresentation();

            for (var index = 0; index < CorridorSegmentNames.Length; index++)
            {
                Transform segment = FindDescendant(
                    corridor,
                    CorridorSegmentNames[index]);
                Collider collider = segment.GetComponent<Collider>();
                Vector3 landingPosition =
                    collider.bounds.center +
                    Vector3.up * (collider.bounds.extents.y + 0.42f);

                roadRig.Adapter.SynchronizePresentationPose(
                    landingPosition,
                    segment.rotation);
                yield return WaitFixedFrames(30);

                Assert.That(
                    roadRig.Vehicle.Telemetry.GroundedContacts,
                    Is.GreaterThanOrEqualTo(2),
                    CorridorSegmentNames[index] +
                    " must support Sasha's four-contact rig.");
                Assert.That(
                    roadRig.Vehicle.Telemetry.DominantSurface,
                    Is.EqualTo(CorridorSurfaceKinds[index]),
                    CorridorSegmentNames[index] +
                    " must reach the existing road telemetry.");
            }

            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalHash));
            Assert.That(controller.State, Is.SameAs(canonicalState));
            Assert.That(controller.State!.GlobalTick, Is.EqualTo(globalTick));
            Assert.That(
                controller.State.NextCommandSequence,
                Is.EqualTo(commandSequence));
            Assert.That(controller.CanRecoverRoadPresentation, Is.False);
            Assert.That(controller.RecoverRoadPresentation(), Is.False);
            Assert.That(
                Object.FindObjectsByType<Camera>(FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
            Assert.That(
                Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include),
                Has.Length.EqualTo(1));
        }

        private static IEnumerator LoadScene()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            Transform[] descendants =
                root.GetComponentsInChildren<Transform>(true);
            foreach (Transform candidate in descendants)
            {
                if (candidate.name == objectName)
                {
                    return candidate;
                }
            }

            throw new AssertionException(
                "Missing authored object: " + objectName);
        }

        private static IEnumerator WaitFixedFrames(int count)
        {
            for (var index = 0; index < count; index++)
            {
                yield return new WaitForFixedUpdate();
            }
        }
    }

    public sealed partial class LastBearingPlayModeTests
    {
        [UnityTest]
        public IEnumerator WreckLineSurfacesSurviveFourCityDrivingCycles()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            LastBearingGameController controller =
                Object.FindAnyObjectByType<LastBearingGameController>();
            Assert.That(controller, Is.Not.Null);
            controller.enabled = false;

            LastBearingState city =
                LastBearingScenarioFactory.CreateInitial(
                    ColonyComposition.Mixed,
                    2011);
            LastBearingState outbound = CreateOutboundState();
            LastBearingWorldBuilder world = controller.World!;
            LastBearingModeCoordinator coordinator =
                controller.ModeCoordinator!;
            Transform? corridor = null;
            foreach (Transform candidate in
                     world.transform.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name ==
                    LastBearingWorldBuilder.RoadCorridorRootName)
                {
                    corridor = candidate;
                    break;
                }
            }

            Assert.That(corridor, Is.Not.Null);
            Transform roadCorridor = corridor!;
            for (var cycle = 0; cycle < 4; cycle++)
            {
                InstallControllerState(controller, outbound);
                yield return null;

                Assert.That(
                    coordinator.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.Driving));
                Assert.That(
                    roadCorridor.GetComponentsInChildren<RoadFeelSurface>(true),
                    Has.Length.EqualTo(4));
                Assert.That(
                    Object.FindObjectsByType<Camera>(
                        FindObjectsInactive.Include),
                    Has.Length.EqualTo(1));
                Assert.That(
                    Object.FindObjectsByType<AudioListener>(
                        FindObjectsInactive.Include),
                    Has.Length.EqualTo(1));
                Assert.That(controller.CanRecoverRoadPresentation, Is.True);
                string drivingHash = controller.CanonicalHash;
                LastBearingState drivingState = controller.State!;

                Assert.That(controller.RecoverRoadPresentation(), Is.True);
                Assert.That(controller.CanonicalHash, Is.EqualTo(drivingHash));
                Assert.That(controller.State, Is.SameAs(drivingState));
                Assert.That(controller.CanRecoverRoadPresentation, Is.True);
                AssertCameraOwnership(controller, chaseActive: true);

                InstallControllerState(controller, city);
                yield return null;

                Assert.That(
                    coordinator.CurrentMode,
                    Is.EqualTo(LastBearingPresentationMode.CityOverview));
                Assert.That(controller.CanRecoverRoadPresentation, Is.False);
                Assert.That(controller.RecoverRoadPresentation(), Is.False);
                Assert.That(
                    roadCorridor.GetComponentsInChildren<RoadFeelSurface>(true),
                    Has.Length.EqualTo(4));
                Assert.That(
                    Object.FindObjectsByType<Camera>(
                        FindObjectsInactive.Include),
                    Has.Length.EqualTo(1));
                Assert.That(
                    Object.FindObjectsByType<AudioListener>(
                        FindObjectsInactive.Include),
                    Has.Length.EqualTo(1));
                AssertCameraOwnership(controller, chaseActive: false);
            }
        }
    }
}
