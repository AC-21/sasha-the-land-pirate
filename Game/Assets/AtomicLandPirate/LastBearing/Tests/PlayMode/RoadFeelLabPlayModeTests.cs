#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.IO;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Save.LastBearing;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class RoadFeelLabPlayModeTests : InputTestFixture
    {
        private const string CourseName = "R0 Authored Road Feel Course";

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene scene = SceneManager.GetSceneByName(RoadFeelLabBootstrap.SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene("RoadFeelLab_TestCleanup");
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
        public IEnumerator SceneBootstrapsOneVehicleCameraAndAuthoredCourse()
        {
            yield return LoadLab();

            RoadFeelLabController[] controllers =
                Object.FindObjectsByType<RoadFeelLabController>(
                    FindObjectsInactive.Include);
            Assert.That(controllers, Has.Length.EqualTo(1));
            RoadFeelLabController controller = controllers[0];
            Assert.That(controller.name, Is.EqualTo(RoadFeelLabController.RuntimeRootName));

            RoadFeelVehicleController[] vehicles =
                controller.GetComponentsInChildren<RoadFeelVehicleController>(true);
            RoadFeelChaseCamera[] chaseCameras =
                controller.GetComponentsInChildren<RoadFeelChaseCamera>(true);
            LastBearingRoadFeelModeAdapter[] modeAdapters =
                controller.GetComponentsInChildren<LastBearingRoadFeelModeAdapter>(true);
            Camera[] cameras = controller.GetComponentsInChildren<Camera>(true);
            Assert.That(vehicles, Has.Length.EqualTo(1));
            Assert.That(vehicles[0].name, Is.EqualTo(RoadFeelRigFactory.RigName));
            Assert.That(
                vehicles[0].GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThanOrEqualTo(14));
            Assert.That(chaseCameras, Has.Length.EqualTo(1));
            Assert.That(modeAdapters, Has.Length.EqualTo(1));
            Assert.That(modeAdapters[0].IsRoadModeActive, Is.True);
            Assert.That(modeAdapters[0].IsPhysicsSuspended, Is.False);
            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].GetComponent<RoadFeelChaseCamera>(), Is.SameAs(chaseCameras[0]));

            Transform[] descendants = controller.GetComponentsInChildren<Transform>(true);
            Assert.That(CountNamed(descendants, CourseName), Is.EqualTo(1));
            Transform? course = controller.transform.Find(CourseName);
            Assert.That(course, Is.Not.Null);

            RoadFeelSurface[] surfaces =
                course!.GetComponentsInChildren<RoadFeelSurface>(true);
            var kinds = new HashSet<RoadFeelSurfaceKind>();
            foreach (RoadFeelSurface surface in surfaces)
            {
                kinds.Add(surface.Kind);
            }

            Assert.That(
                kinds,
                Is.EquivalentTo(new[]
                {
                    RoadFeelSurfaceKind.Concrete,
                    RoadFeelSurfaceKind.Hardpack,
                    RoadFeelSurfaceKind.Gravel,
                    RoadFeelSurfaceKind.Sand,
                    RoadFeelSurfaceKind.Washboard
                }));

            for (var index = 1; index <= 9; index++)
            {
                GameObject rib = GameObject.Find("Washboard Rib " + index);
                Assert.That(rib, Is.Not.Null);
                RoadFeelSurface ribSurface = rib.GetComponent<RoadFeelSurface>();
                Assert.That(ribSurface, Is.Not.Null);
                Assert.That(
                    ribSurface.Kind,
                    Is.EqualTo(RoadFeelSurfaceKind.Washboard));
            }
        }

        [UnityTest]
        public IEnumerator LoadAndResetApisRemainPresentationOnly()
        {
            LastBearingState canonicalState = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.Mixed,
                2011);
            string canonicalHashBefore =
                LastBearingCanonicalCodec.ComputeSha256(canonicalState);
            string saveBoundaryBefore = CaptureSaveBoundary();

            yield return LoadLab();

            RoadFeelLabController lab =
                Object.FindAnyObjectByType<RoadFeelLabController>();
            RoadFeelVehicleController vehicle =
                Object.FindAnyObjectByType<RoadFeelVehicleController>();
            Assert.That(lab, Is.Not.Null);
            Assert.That(vehicle, Is.Not.Null);

            lab.enabled = false;
            vehicle.enabled = false;
            Rigidbody body = vehicle.Body;
            float unloadedMass = body.mass;

            vehicle.SetLoad(1_300f, RoadFeelDamageBand.Critical);
            Assert.That(body.mass, Is.EqualTo(unloadedMass + 1_300f).Within(0.01f));
            Assert.That(vehicle.Telemetry.CargoMassKilograms, Is.EqualTo(1_300f));
            Assert.That(vehicle.Telemetry.DamageBand, Is.EqualTo(RoadFeelDamageBand.Critical));

            body.constraints = RigidbodyConstraints.FreezeAll;
            body.useGravity = false;
            body.linearVelocity = new Vector3(4f, -2f, 7f);
            body.angularVelocity = new Vector3(0.4f, 0.8f, -0.2f);
            Vector3 resetPosition = new Vector3(12f, 3f, -24f);
            Quaternion resetRotation = Quaternion.Euler(0f, 37f, 0f);
            vehicle.ResetAt(resetPosition, resetRotation);

            Assert.That(Vector3.Distance(body.position, resetPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(body.rotation, resetRotation), Is.LessThan(0.001f));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(vehicle.Telemetry.SpeedMetresPerSecond, Is.EqualTo(0f));
            Assert.That(vehicle.Telemetry.GroundedContacts, Is.EqualTo(0));
            Assert.That(vehicle.Telemetry.Recovering, Is.True);

            Assert.That(
                Object.FindObjectsByType<LastBearingGameController>(
                    FindObjectsInactive.Include),
                Is.Empty);
            Assert.That(
                LastBearingCanonicalCodec.ComputeSha256(canonicalState),
                Is.EqualTo(canonicalHashBefore));
            Assert.That(CaptureSaveBoundary(), Is.EqualTo(saveBoundaryBefore));
        }

        [UnityTest]
        public IEnumerator VehicleFindsRoadAndSupportsThrottleBrakeAndReverse()
        {
            yield return LoadLab();

            RoadFeelLabController lab =
                Object.FindAnyObjectByType<RoadFeelLabController>();
            RoadFeelVehicleController vehicle =
                Object.FindAnyObjectByType<RoadFeelVehicleController>();
            Assert.That(lab, Is.Not.Null);
            Assert.That(vehicle, Is.Not.Null);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.0001f));

            lab.enabled = false;
            vehicle.SetControlInput(default);
            yield return WaitFixedFrames(30);

            Assert.That(vehicle.Telemetry.GroundedContacts, Is.GreaterThanOrEqualTo(2));

            vehicle.SetControlInput(new RoadFeelControlInput(
                throttle: 1f,
                brake: 0f,
                steering: 0f,
                handbrake: 0f));
            yield return WaitFixedFrames(20);
            float drivenSpeed = vehicle.Telemetry.ForwardSpeedMetresPerSecond;
            Assert.That(drivenSpeed, Is.GreaterThan(1f));

            vehicle.SetControlInput(new RoadFeelControlInput(
                throttle: 0f,
                brake: 1f,
                steering: 0f,
                handbrake: 0f));
            yield return WaitFixedFrames(6);
            Assert.That(
                vehicle.Telemetry.ForwardSpeedMetresPerSecond,
                Is.LessThan(drivenSpeed));

            // Reverse arming begins only after the service brake has brought
            // the moving rig into the low-speed window. Budget both that
            // stopping phase and the deliberate 0.18-second arm before
            // requiring observable reverse travel.
            yield return WaitFixedFrames(32);
            Assert.That(
                vehicle.Telemetry.ForwardSpeedMetresPerSecond,
                Is.LessThan(-0.1f));
        }

        [UnityTest]
        public IEnumerator ChaseCameraContractsImmediatelyForNewObstruction()
        {
            yield return LoadLab();

            RoadFeelLabController lab =
                Object.FindAnyObjectByType<RoadFeelLabController>();
            RoadFeelVehicleController vehicle =
                Object.FindAnyObjectByType<RoadFeelVehicleController>();
            RoadFeelChaseCamera chase =
                Object.FindAnyObjectByType<RoadFeelChaseCamera>();
            Assert.That(lab, Is.Not.Null);
            Assert.That(vehicle, Is.Not.Null);
            Assert.That(chase, Is.Not.Null);

            lab.enabled = false;
            vehicle.enabled = false;
            vehicle.Body.useGravity = false;
            vehicle.Body.linearVelocity = Vector3.zero;
            chase.SnapBehind();

            Vector3 focus = vehicle.transform.position + Vector3.up * 1.35f;
            float openDistance = Vector3.Distance(focus, chase.transform.position);
            GameObject obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.name = "Road Feel Camera Collision Probe";
            obstruction.transform.position = Vector3.Lerp(
                focus,
                chase.transform.position,
                0.5f);
            obstruction.transform.localScale = Vector3.one * 2.2f;
            Physics.SyncTransforms();

            yield return null;

            float blockedDistance = Vector3.Distance(
                focus,
                chase.transform.position);
            Assert.That(blockedDistance, Is.LessThan(openDistance * 0.75f));
        }

        private static IEnumerator LoadLab()
        {
            AsyncOperation? load = SceneManager.LoadSceneAsync(
                RoadFeelLabBootstrap.SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
        }

        private static int CountNamed(Transform[] transforms, string name)
        {
            var count = 0;
            foreach (Transform candidate in transforms)
            {
                if (candidate.name == name)
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerator WaitFixedFrames(int count)
        {
            for (var index = 0; index < count; index++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static string CaptureSaveBoundary()
        {
            string profileDirectory = Path.Combine(
                Application.persistentDataPath,
                LastBearingProfileContract.ProfileName);
            if (!Directory.Exists(profileDirectory))
            {
                return "<missing>";
            }

            string[] files = Directory.GetFiles(
                profileDirectory,
                "*",
                SearchOption.AllDirectories);
            var entries = new List<string>(files.Length + 1)
            {
                "<exists>"
            };
            foreach (string file in files)
            {
                var info = new FileInfo(file);
                entries.Add(
                    file + "|" + info.Length + "|" +
                    info.LastWriteTimeUtc.Ticks);
            }

            entries.Sort(System.StringComparer.Ordinal);
            return string.Join("\n", entries);
        }
    }
}
