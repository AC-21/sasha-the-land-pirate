#nullable enable

using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class SashaScoutPresentationTests
    {
        private GameObject? _root;
        private Material? _material;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            if (_material != null)
            {
                Object.DestroyImmediate(_material);
            }

            foreach (LastBearingGameController controller in
                     Object.FindObjectsByType<LastBearingGameController>(
                         FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void SharedScoutMatchesSemanticAndRoadCollisionContracts()
        {
            _root = new GameObject("Sasha Scout Contract Test");
            SashaScoutBlockoutMaterials materials = CreateMaterials();
            SashaScoutVisual staticVisual = SashaScoutBlockoutFactory.Create(
                _root.transform,
                materials,
                includeRoadCollisionShell: false);
            SashaScoutVisual roadVisual = SashaScoutBlockoutFactory.Create(
                _root.transform,
                materials,
                includeRoadCollisionShell: true);

            Assert.That(
                SashaScoutSemanticContract.DirectionPackageId,
                Is.EqualTo("C0-VGR-01"));
            Assert.That(staticVisual.DirectionStage, Is.EqualTo("C0Blockout"));
            Assert.That(staticVisual.HasProductionGeometry, Is.False);
            Assert.That(staticVisual.ContactStationCount, Is.EqualTo(4));
            Assert.That(staticVisual.WheelVisualCount, Is.EqualTo(4));
            Assert.That(staticVisual.Lod0Root, Is.Not.Null);
            Assert.That(staticVisual.Lod1Root, Is.Not.Null);
            Assert.That(staticVisual.Lod2Root, Is.Not.Null);
            Assert.That(
                staticVisual.FindSocket(
                    SashaScoutSemanticContract.FrontUpgradeSocketName),
                Is.Not.Null);
            Assert.That(
                staticVisual.FindSocket(
                    SashaScoutSemanticContract.CargoUpgradeSocketName),
                Is.Not.Null);
            Assert.That(
                staticVisual.FindSocket(
                    SashaScoutSemanticContract.ToolDeploySocketName),
                Is.Not.Null);
            Assert.That(
                staticVisual.FindSocket(
                    SashaScoutSemanticContract.DriverCameraSocketName),
                Is.Not.Null);

            Transform[] contacts = roadVisual.CopyContactStations();
            Assert.That(contacts, Has.Length.EqualTo(4));
            for (var index = 0; index < contacts.Length; index++)
            {
                Assert.That(
                    contacts[index].localPosition,
                    Is.EqualTo(
                        SashaScoutSemanticContract.GetContactStationLocalPosition(index)));
                Assert.That(
                    contacts[index].IsChildOf(roadVisual.GeometryRoot!),
                    Is.False);
                Assert.That(
                    contacts[index].IsChildOf(roadVisual.CollisionRoot!),
                    Is.False);
            }

            Assert.That(
                staticVisual.CollisionRoot!
                    .GetComponentsInChildren<BoxCollider>(true),
                Is.Empty);
            Assert.That(
                roadVisual.CollisionRoot!
                    .GetComponentsInChildren<BoxCollider>(true),
                Has.Length.EqualTo(
                    SashaScoutBlockoutFactory.RoadCollisionBoxCount));
            Assert.That(
                roadVisual.GetComponentsInChildren<MeshCollider>(true),
                Is.Empty);
            foreach (Renderer renderer in
                     roadVisual.GetComponentsInChildren<Renderer>(true))
            {
                Collider? collider = renderer.GetComponent<Collider>();
                Assert.That(collider, Is.Not.Null, renderer.name);
                Assert.That(collider!.enabled, Is.False, renderer.name);
            }
        }

        [Test]
        public void ScoutModulesAreMutuallyExclusiveAndReuseStableSockets()
        {
            _root = new GameObject("Sasha Scout Module Test");
            SashaScoutVisual visual = SashaScoutBlockoutFactory.Create(
                _root.transform,
                CreateMaterials(),
                includeRoadCollisionShell: false);

            visual.ApplyModule(SashaScoutModulePresentation.None);
            Assert.That(visual.IsWinchVisible, Is.False);
            Assert.That(visual.IsRangeTankVisible, Is.False);

            visual.ApplyModule(SashaScoutModulePresentation.WinchAssembly);
            Assert.That(visual.IsWinchVisible, Is.True);
            Assert.That(visual.IsRangeTankVisible, Is.False);

            visual.ApplyModule(SashaScoutModulePresentation.SealedRangeTank);
            Assert.That(visual.IsWinchVisible, Is.False);
            Assert.That(visual.IsRangeTankVisible, Is.True);
            AssertModuleMatchesSocket(
                visual.WinchModuleRoot,
                visual.FindSocket(
                    SashaScoutSemanticContract.FrontUpgradeSocketName));
            AssertModuleMatchesSocket(
                visual.RangeTankModuleRoot,
                visual.FindSocket(
                    SashaScoutSemanticContract.CargoUpgradeSocketName));
        }

        [Test]
        public void GarageCutawayReusesOneCameraAndCannotChangeCanonicalState()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            string canonicalBefore = controller.CanonicalHash;
            LastBearingGarageBayView garage = controller.World!.GarageBayView!;
            LastBearingCameraRig cameraRig = controller.World.CameraRig!;

            Assert.That(garage.IsDollhouseCutaway, Is.True);
            Assert.That(garage.HasRoof, Is.False);
            Assert.That(garage.HasNearWall, Is.False);
            Assert.That(garage.VehicleDock, Is.Not.Null);
            Assert.That(garage.CameraAnchor, Is.Not.Null);
            Assert.That(garage.FocusAnchor, Is.Not.Null);
            Assert.That(
                garage.GetComponentsInChildren<Camera>(true),
                Is.Empty);
            Assert.That(
                garage.GetComponentsInChildren<CharacterController>(true),
                Is.Empty);
            Assert.That(
                _root.GetComponentsInChildren<Camera>(true),
                Has.Length.EqualTo(1));
            Assert.That(garage.gameObject.activeInHierarchy, Is.False);

            controller.OpenGarageBay();

            Assert.That(garage.gameObject.activeInHierarchy, Is.True);
            Assert.That(cameraRig.IsInspectionMode, Is.True);
            Assert.That(
                cameraRig.InspectionCameraAnchor,
                Is.SameAs(garage.CameraAnchor));
            Assert.That(
                cameraRig.InspectionFocusAnchor,
                Is.SameAs(garage.FocusAnchor));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.ShowCityOverview();

            Assert.That(garage.gameObject.activeInHierarchy, Is.False);
            Assert.That(cameraRig.IsInspectionMode, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        private SashaScoutBlockoutMaterials CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            _material = new Material(shader!);
            return new SashaScoutBlockoutMaterials(
                _material,
                _material,
                _material,
                _material,
                _material,
                _material);
        }

        private static void AssertModuleMatchesSocket(
            Transform? module,
            Transform? socket)
        {
            Assert.That(module, Is.Not.Null);
            Assert.That(socket, Is.Not.Null);
            Assert.That(
                Vector3.Distance(module!.position, socket!.position),
                Is.LessThan(0.00001f));
            Assert.That(
                Quaternion.Angle(module.rotation, socket.rotation),
                Is.LessThan(0.00001f));
        }
    }
}
