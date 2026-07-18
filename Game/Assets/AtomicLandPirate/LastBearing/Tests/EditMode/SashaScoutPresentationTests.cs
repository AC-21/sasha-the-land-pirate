#nullable enable

using System.Reflection;
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

        [Test]
        public void GarageAssemblyGaugeShowsDerivedPreparationProgressOnly()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            LastBearingWorldBuilder world = controller.World!;
            LastBearingGarageBayView garage = world.GarageBayView!;
            string canonicalBefore = controller.CanonicalHash;

            Assert.That(garage.IsPreparationGaugeVisible, Is.False);
            Assert.That(garage.PreparationGaugeLitSegments, Is.Zero);
            Assert.That(garage.PreparationProgressNormalized, Is.Zero);

            world.ApplyGaragePreparationProgress(60, 120);

            Assert.That(garage.IsPreparationGaugeVisible, Is.True);
            Assert.That(
                garage.PreparationGaugeLitSegments,
                Is.EqualTo(LastBearingGarageBayView.PreparationGaugeSegmentCount / 2));
            Assert.That(
                CountActiveGaugeSegments(garage),
                Is.EqualTo(LastBearingGarageBayView.PreparationGaugeSegmentCount / 2));
            Assert.That(garage.PreparationProgressNormalized, Is.EqualTo(0.5f));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            world.ApplyGaragePreparationProgress(119, 120);

            Assert.That(
                garage.PreparationGaugeLitSegments,
                Is.EqualTo(LastBearingGarageBayView.PreparationGaugeSegmentCount - 1),
                "the final segment is reserved for exact canonical completion");
            Assert.That(
                CountActiveGaugeSegments(garage),
                Is.EqualTo(LastBearingGarageBayView.PreparationGaugeSegmentCount - 1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            world.ApplyGaragePreparationProgress(120, 120);

            Assert.That(
                garage.PreparationGaugeLitSegments,
                Is.EqualTo(LastBearingGarageBayView.PreparationGaugeSegmentCount));
            Assert.That(
                CountActiveGaugeSegments(garage),
                Is.EqualTo(LastBearingGarageBayView.PreparationGaugeSegmentCount));
            Assert.That(garage.PreparationProgressNormalized, Is.EqualTo(1f));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.ReturnToTitle();

            Assert.That(garage.IsPreparationGaugeVisible, Is.False);
            Assert.That(garage.PreparationGaugeLitSegments, Is.Zero);
        }

        [Test]
        public void GaragePlanningMarkersKeepBothChoicesInTheSharedCameraFrame()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            PrepareControllerForGaragePlan(controller);
            LastBearingWorldBuilder world = controller.World!;
            LastBearingGarageBayView garage = world.GarageBayView!;
            LastBearingCameraRig cameraRig = world.CameraRig!;
            string canonicalBefore = controller.CanonicalHash;

            Assert.That(garage.IsWinchStaged, Is.True);
            Assert.That(garage.IsRangeTankStaged, Is.True);
            Assert.That(
                garage.ActivePlanMarker,
                Is.EqualTo(GaragePlanMarkerPresentation.None));
            Assert.That(garage.IsWorkshopPushPlanMarkerVisible, Is.False);
            Assert.That(garage.IsCivicBufferPlanMarkerVisible, Is.False);
            Assert.That(
                _root.GetComponentsInChildren<Camera>(includeInactive: true),
                Has.Length.EqualTo(1));
            Assert.That(garage.GetComponentsInChildren<Camera>(true), Is.Empty);

            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);

            Assert.That(garage.gameObject.activeInHierarchy, Is.True);
            Assert.That(garage.IsWinchStaged, Is.True);
            Assert.That(garage.IsRangeTankStaged, Is.True);
            Assert.That(
                garage.ActivePlanMarker,
                Is.EqualTo(GaragePlanMarkerPresentation.WorkshopPush));
            Assert.That(garage.IsWorkshopPushPlanMarkerVisible, Is.True);
            Assert.That(garage.IsCivicBufferPlanMarkerVisible, Is.False);
            Assert.That(cameraRig.IsInspectionMode, Is.True);
            Assert.That(cameraRig.InspectionCameraAnchor, Is.SameAs(garage.CameraAnchor));
            Assert.That(cameraRig.InspectionFocusAnchor, Is.SameAs(garage.FocusAnchor));
            AssertRenderedRootInsideFrame(
                world.MainCamera!,
                RequireGarageRoot(garage, "PLAN_MARKER_WORKSHOP_PUSH"));
            AssertRenderedRootInsideFrame(
                world.MainCamera!,
                RequireGarageRoot(garage, "MODULE_STAND_WINCH"));
            AssertRenderedRootInsideFrame(
                world.MainCamera!,
                RequireGarageRoot(garage, "MODULE_STAND_RANGE_TANK"));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);

            Assert.That(garage.IsWinchStaged, Is.True);
            Assert.That(garage.IsRangeTankStaged, Is.True);
            Assert.That(
                garage.ActivePlanMarker,
                Is.EqualTo(GaragePlanMarkerPresentation.CivicBuffer));
            Assert.That(garage.IsWorkshopPushPlanMarkerVisible, Is.False);
            Assert.That(garage.IsCivicBufferPlanMarkerVisible, Is.True);
            AssertRenderedRootInsideFrame(
                world.MainCamera!,
                RequireGarageRoot(garage, "PLAN_MARKER_CIVIC_BUFFER"));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.CancelGaragePlan();

            Assert.That(
                garage.ActivePlanMarker,
                Is.EqualTo(GaragePlanMarkerPresentation.None));
            Assert.That(garage.IsWorkshopPushPlanMarkerVisible, Is.False);
            Assert.That(garage.IsCivicBufferPlanMarkerVisible, Is.False);
            Assert.That(garage.gameObject.activeInHierarchy, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            InvokeSimulationTick(controller);

            Assert.That(garage.gameObject.activeInHierarchy, Is.True);
            Assert.That(garage.IsWinchStaged, Is.False);
            Assert.That(garage.IsRangeTankStaged, Is.True);
            Assert.That(
                garage.ActivePlanMarker,
                Is.EqualTo(GaragePlanMarkerPresentation.WorkshopPush));
            Assert.That(garage.IsWorkshopPushPlanMarkerVisible, Is.True);
            Assert.That(garage.IsCivicBufferPlanMarkerVisible, Is.False);
            Assert.That(garage.IsPreparationGaugeVisible, Is.True);
            AssertRenderedRootInsideFrame(
                world.MainCamera!,
                RequireGarageRoot(garage, "PLAN_MARKER_WORKSHOP_PUSH"));
            AssertRenderedRootInsideFrame(
                world.MainCamera!,
                RequireGarageRoot(garage, "ASSEMBLY_PROGRESS_GAUGE"));
            AssertRenderedRootInsideFrame(
                world.MainCamera!,
                world.VehicleView!.transform);
            Assert.That(
                _root.GetComponentsInChildren<Camera>(includeInactive: true),
                Has.Length.EqualTo(1));
        }

        private static Transform RequireGarageRoot(
            LastBearingGarageBayView garage,
            string name)
        {
            Transform? root = garage.transform.Find(name);
            Assert.That(root, Is.Not.Null, name);
            return root!;
        }

        private static void AssertRenderedRootInsideFrame(
            Camera camera,
            Transform renderedRoot)
        {
            Renderer[] renderers =
                renderedRoot.GetComponentsInChildren<Renderer>(false);
            Assert.That(renderers, Is.Not.Empty, renderedRoot.name);

            Bounds bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + new Vector3(
                            extents.x * x,
                            extents.y * y,
                            extents.z * z);
                        Vector3 viewport = camera.WorldToViewportPoint(corner);
                        Assert.That(
                            viewport.z,
                            Is.GreaterThan(0f),
                            renderedRoot.name + " is behind the shared camera");
                        Assert.That(
                            viewport.x,
                            Is.InRange(0f, 1f),
                            renderedRoot.name + " leaves the horizontal frame");
                        Assert.That(
                            viewport.y,
                            Is.InRange(0f, 1f),
                            renderedRoot.name + " leaves the vertical frame");
                    }
                }
            }
        }

        private static int CountActiveGaugeSegments(
            LastBearingGarageBayView garage)
        {
            var count = 0;
            foreach (Transform child in
                     garage.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.StartsWith("ASSEMBLY_PROGRESS_LIT_")
                    && child.gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private static void PrepareControllerForGaragePlan(
            LastBearingGameController controller)
        {
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);

            Assert.That(controller.ReadModel, Is.Not.Null);
            Assert.That(controller.State!.SliceInfrastructureActive, Is.True);
            Assert.That(
                controller.ReadModel.PreparationChoice,
                Is.EqualTo(PreparationChoice.Unselected));
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            MethodInfo? simulate = typeof(LastBearingGameController).GetMethod(
                "SimulateOneTick",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(simulate, Is.Not.Null);
            simulate!.Invoke(controller, null);
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
