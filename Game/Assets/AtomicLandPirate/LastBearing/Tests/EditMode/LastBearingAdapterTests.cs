#nullable enable

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Editor;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingAdapterTests
    {
        private GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            foreach (LastBearingGameController controller in
                     UnityEngine.Object.FindObjectsByType<LastBearingGameController>(
                         FindObjectsInactive.Include))
            {
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
            }
        }

        [Test]
        public void SaveAdapterExposesOnlyParameterlessCreateFactory()
        {
            Type adapter = typeof(LastBearingSaveAdapter);
            ConstructorInfo[] visibleConstructors = adapter.GetConstructors(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance);
            Assert.That(
                visibleConstructors.Where(constructor =>
                    constructor.IsPublic ||
                    constructor.IsFamily ||
                    constructor.IsAssembly),
                Is.Empty);

            MethodInfo[] createMethods = adapter.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "Create")
                .ToArray();
            Assert.That(createMethods, Has.Length.EqualTo(1));
            Assert.That(createMethods[0].GetParameters(), Is.Empty);
            Assert.That(createMethods[0].ReturnType, Is.EqualTo(adapter));
        }

        [Test]
        public void RuntimeBuildsOneInspectablePrimitiveWorld()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();

            Assert.That(controller.World, Is.Not.Null);
            Assert.That(controller.World!.MainCamera, Is.Not.Null);
            Assert.That(controller.World.VehicleView, Is.Not.Null);
            Assert.That(controller.World.RouteModulePointView, Is.Not.Null);
            Assert.That(controller.World.TurbineRotor, Is.Not.Null);
            Assert.That(controller.World.WaterFill, Is.Not.Null);
            Assert.That(
                _root.GetComponentsInChildren<Renderer>(includeInactive: true).Length,
                Is.GreaterThan(30));
        }

        [Test]
        public void WreckLineViewIsOnePhysicsFreeCoreIsolatedModulePoint()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.HumanOnly);
            Assert.That(
                _root.GetComponentsInChildren<LastBearingRouteModulePointView>(
                    includeInactive: true),
                Has.Length.EqualTo(1));
            LastBearingRouteModulePointView view =
                controller.World!.RouteModulePointView!;
            string canonicalBefore = controller.CanonicalHash;

            Assert.That(
                LastBearingRouteModulePointView.DirectionPackageId,
                Is.EqualTo("C0-VGR-03"));
            Assert.That(
                LastBearingRouteModulePointView.ContentId,
                Is.EqualTo("poi_wreck_line_module_point_a"));
            Assert.That(view.InteractionAnchor, Is.Not.Null);
            Assert.That(view.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(view.GetComponentsInChildren<Camera>(true), Is.Empty);
            foreach (Collider collider in view.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }

            view.ApplyState(RouteModulePointPresentationState.WinchAvailable);
            Assert.That(view.IsPumpRotorVisible, Is.True);
            Assert.That(view.IsDustCurtainVisible, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            view.ApplyState(RouteModulePointPresentationState.WinchRecovered);
            Assert.That(view.IsPumpRotorVisible, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            view.ApplyState(RouteModulePointPresentationState.TankAvailable);
            Assert.That(view.IsDustCurtainVisible, Is.True);
            Assert.That(view.IsPumpRotorVisible, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [Test]
        public void DepotRecoveryViewIsReadablePhysicsFreeAndCoreIsolated()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.HumanOnly);
            LastBearingDepotApproachRecoveryView recovery =
                controller.World!.DepotApproachRecoveryView!;
            string canonicalBefore = controller.CanonicalHash;

            Assert.That(
                LastBearingDepotApproachRecoveryView.DirectionPackageId,
                Is.EqualTo("C0-VGR-02"));
            Assert.That(
                LastBearingDepotApproachRecoveryView.Revision,
                Is.EqualTo("R1"));
            Assert.That(
                LastBearingDepotApproachRecoveryView.ContentId,
                Is.EqualTo("poi_depot_approach_recovery_a"));
            Assert.That(recovery.InteractionAnchor, Is.Not.Null);
            Assert.That(
                recovery.InteractionAnchor!.name,
                Is.EqualTo(
                    LastBearingDepotApproachRecoveryView.InteractionAnchorName));
            Assert.That(
                recovery.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty);
            Assert.That(
                recovery.GetComponentsInChildren<Camera>(true),
                Is.Empty);
            Assert.That(
                recovery.GetComponentsInChildren<Collider>(true),
                Is.Not.Empty);
            foreach (Collider collider in
                     recovery.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }

            recovery.ApplyState(
                DepotApproachRecoveryPresentationState.Available);
            Assert.That(recovery.IsAvailableToolVisible, Is.True);
            Assert.That(recovery.IsUnlockedLatchVisible, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            recovery.ApplyState(
                DepotApproachRecoveryPresentationState.Unlocked);
            Assert.That(recovery.IsAvailableToolVisible, Is.False);
            Assert.That(recovery.IsUnlockedLatchVisible, Is.True);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.OperateDepotApproachRecoveryPoint();
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [TestCase(ColonyComposition.HumanOnly)]
        [TestCase(ColonyComposition.RobotOnly)]
        [TestCase(ColonyComposition.Mixed)]
        public void NewGameUsesCoreCompositionWithoutPresentationMechanics(
            ColonyComposition composition)
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();

            controller.StartNewGame(composition);

            Assert.That(controller.ReadModel, Is.Not.Null);
            Assert.That(controller.ReadModel!.Composition, Is.EqualTo(composition));
            Assert.That(
                controller.ReadModel.WaterMilli,
                Is.EqualTo(
                    LastBearingBalanceV1.StartingWaterMilli +
                    LastBearingBalanceV1.FailingWaterRateMilliPerSettlementTick));
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(24));
            Assert.That(controller.ReadModel.FuelUnits, Is.EqualTo(18));
            Assert.That(
                controller.ReadModel.AssignedResidentId,
                Is.EqualTo(composition == ColonyComposition.RobotOnly
                    ? ResidentRoster.RobotResidentId
                    : ResidentRoster.HumanResidentId));
            Assert.That(controller.State!.NextCommandSequence, Is.EqualTo(1));
        }

        [TestCase(ColonyComposition.HumanOnly, ResidentRoster.HumanResidentId)]
        [TestCase(ColonyComposition.RobotOnly, ResidentRoster.RobotResidentId)]
        [TestCase(ColonyComposition.Mixed, ResidentRoster.HumanResidentId)]
        public void ValidUnassignedStateCanRecoverItsDefaultLeadImmediately(
            ColonyComposition composition,
            string expectedResidentId)
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            LastBearingState unassigned = LastBearingScenarioFactory.CreateInitial(
                composition,
                2011);
            InstallControllerState(controller, unassigned);

            controller.AssignDefaultLeadResident();

            Assert.That(
                controller.ReadModel!.AssignedResidentId,
                Is.EqualTo(expectedResidentId));
            Assert.That(controller.State!.NextCommandSequence, Is.EqualTo(1));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
        }

        [Test]
        public void SaveDefersWhileAPlayerCommandIsStillPending()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.HumanOnly);
            controller.InspectCityNeed();
            controller.ActivateInfrastructure();

            controller.Save();

            Assert.That(PendingCommandCount(controller), Is.EqualTo(1));
            Assert.That(controller.SaveStatus, Does.Contain("Save deferred"));
        }

        [Test]
        public void CityNeedInspectionGatesTheFirstCityAction()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.HumanOnly);

            controller.ActivateInfrastructure();

            var pendingField = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(pendingField, Is.Not.Null);
            var pending = pendingField!.GetValue(controller) as ICollection;
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending!.Count, Is.EqualTo(0),
                "StartNewGame must commit its roster assignment immediately.");
            Assert.That(controller.CityNeedInspected, Is.False);
            Assert.That(controller.Status, Does.Contain("Inspect"));

            controller.InspectCityNeed();
            controller.ActivateInfrastructure();

            Assert.That(controller.CityNeedInspected, Is.True);
            Assert.That(pending!.Count, Is.EqualTo(1));
        }

        [Test]
        public void CityGrammarComparisonIsReversibleFixedCameraAndCoreIsolated()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            string canonicalBefore = controller.CanonicalHash;

            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            LastBearingWorldBuilder world = controller.World!;
            LastBearingCityGrammarComparison comparison =
                world.CityGrammarComparison!;
            LastBearingCameraRig cameraRig = world.CameraRig!;
            Vector3 fixedPosition = cameraRig.transform.position;
            Quaternion fixedRotation = cameraRig.transform.rotation;
            controller.ManipulateCityGrammarPrimary();
            controller.RotateCityGrammarPrimary();

            Assert.That(
                comparison.SelectedHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.RestrainedSnapGrid));
            Assert.That(comparison.SelectionCount, Is.EqualTo(1));
            Assert.That(comparison.InteractionCount, Is.EqualTo(2));
            Assert.That(cameraRig.IsComparisonMode, Is.True);
            Assert.That(cameraRig.CityFocus, Is.EqualTo(LastBearingCameraRig.ComparisonFocus));
            Assert.That(cameraRig.CityDistance, Is.EqualTo(LastBearingCameraRig.ComparisonDistance));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);

            Assert.That(
                comparison.SelectedHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.DistrictStamp));
            Assert.That(comparison.SelectionCount, Is.EqualTo(2));
            Assert.That(cameraRig.transform.position, Is.EqualTo(fixedPosition));
            Assert.That(cameraRig.transform.rotation, Is.EqualTo(fixedRotation));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.ResetCityGrammarComparison();

            Assert.That(
                comparison.SelectedHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.Unselected));
            Assert.That(cameraRig.IsComparisonMode, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("selections=2"));
        }

        [Test]
        public void CityInspectionModesAreCoreIsolatedAndExactlyOneIsActive()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            string canonicalBefore = controller.CanonicalHash;
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;

            Assert.That(
                coordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(coordinator.ActiveModeCount, Is.EqualTo(1));

            controller.OpenBuildingCutaway();
            Assert.That(
                coordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.BuildingCutaway));
            Assert.That(coordinator.ActiveModeCount, Is.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.OpenGarageBay();
            Assert.That(
                coordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.GarageBay));
            Assert.That(coordinator.ActiveModeCount, Is.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.ShowCityOverview();
            Assert.That(
                coordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(coordinator.ActiveModeCount, Is.EqualTo(1));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [Test]
        public void SessionResetAndLoadSynchronizationDropLocalInspectionIntent()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.HumanOnly);
            controller.OpenGarageBay();
            LastBearingModeCoordinator coordinator = controller.ModeCoordinator!;

            controller.ReturnToTitle();

            Assert.That(coordinator.HasActiveMode, Is.False);
            Assert.That(coordinator.ActiveModeCount, Is.EqualTo(0));

            LastBearingState loadedState = LastBearingScenarioFactory.CreateInitial(
                ColonyComposition.RobotOnly,
                2011);
            coordinator.ResetForSession(LastBearingReadModel.FromState(loadedState));

            Assert.That(coordinator.HasActiveMode, Is.True);
            Assert.That(
                coordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(coordinator.ActiveModeCount, Is.EqualTo(1));
        }

        [Test]
        public void RoadFeelAdapterAcceptsOnlyQuantizedInputsAndReturnsNoOutcome()
        {
            _root = new GameObject("Road Feel Mode Adapter Test");
            var vehicleRoot = new GameObject("Road Feel Vehicle");
            vehicleRoot.transform.SetParent(_root.transform, false);
            var vehicle = vehicleRoot.AddComponent<RoadFeelVehicleController>();
            var adapter = vehicleRoot.AddComponent<LastBearingRoadFeelModeAdapter>();
            adapter.Configure(vehicle);

            var controllerRoot = new GameObject(LastBearingGameController.RuntimeRootName);
            controllerRoot.transform.SetParent(_root.transform, false);
            var controller = controllerRoot.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.HumanOnly);
            string canonicalBefore = controller.CanonicalHash;
            controller.AttachRoadModeAdapter(adapter);
            controller.ModeCoordinator!.ApplyQuantizedRoadCommandShadow(750, -250);

            Assert.That(adapter.IsRoadModeActive, Is.False);
            Assert.That(adapter.LastThrottleMilli, Is.EqualTo(0));
            Assert.That(adapter.LastSteeringMilli, Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            adapter.SetRoadModeActive(true);

            adapter.ApplyQuantizedCommandShadow(750, -250);
            adapter.ApplyPresentationOnlyControls(600, 1000);
            adapter.ApplyDerivedPresentationLoad(
                1300,
                LastBearingRoadDamageBand.Worn);

            Assert.That(adapter.IsRoadModeActive, Is.True);
            Assert.That(adapter.LastThrottleMilli, Is.EqualTo(750));
            Assert.That(adapter.LastSteeringMilli, Is.EqualTo(-250));
            Assert.That(adapter.LastBrakeMilli, Is.EqualTo(600));
            Assert.That(adapter.LastHandbrakeMilli, Is.EqualTo(1000));
            Assert.That(adapter.LastCargoMassKilograms, Is.EqualTo(1300));
            Assert.That(
                adapter.LastDamageBand,
                Is.EqualTo(LastBearingRoadDamageBand.Worn));
            Assert.That(
                adapter.Vehicle!.Telemetry.CargoMassKilograms,
                Is.EqualTo(1300f));
            Assert.That(
                adapter.Vehicle.Telemetry.DamageBand,
                Is.EqualTo(RoadFeelDamageBand.Worn));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                adapter.ApplyQuantizedCommandShadow(1001, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                adapter.ApplyQuantizedCommandShadow(0, -1001));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                adapter.ApplyPresentationOnlyControls(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                adapter.ApplyPresentationOnlyControls(0, 1001));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                adapter.ApplyDerivedPresentationLoad(
                    3001,
                    LastBearingRoadDamageBand.Healthy));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                adapter.ApplyDerivedPresentationLoad(
                    0,
                    (LastBearingRoadDamageBand)99));

            Type interfaceType = typeof(ILastBearingRoadModeAdapter);
            foreach (MethodInfo method in interfaceType.GetMethods())
            {
                Assert.That(method.ReturnType, Is.Not.EqualTo(typeof(RoadFeelTelemetry)));
                Assert.That(method.ReturnType, Is.Not.EqualTo(typeof(Rigidbody)));
                Assert.That(method.ReturnType, Is.Not.EqualTo(typeof(LastBearingState)));
            }
        }

        [TestCase(1000, LastBearingRoadDamageBand.Healthy)]
        [TestCase(999, LastBearingRoadDamageBand.Worn)]
        [TestCase(501, LastBearingRoadDamageBand.Worn)]
        [TestCase(500, LastBearingRoadDamageBand.Critical)]
        [TestCase(0, LastBearingRoadDamageBand.Critical)]
        public void CanonicalConditionDerivesPresentationDamageBand(
            long vehicleConditionMilli,
            LastBearingRoadDamageBand expected)
        {
            Assert.That(
                LastBearingModeCoordinator.DerivePresentationDamageBand(
                    vehicleConditionMilli),
                Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(1001)]
        public void PresentationDamageBandRejectsInvalidCanonicalCondition(
            long vehicleConditionMilli)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LastBearingModeCoordinator.DerivePresentationDamageBand(
                    vehicleConditionMilli));
        }

        [TestCase(1, 0, 0, 0, true)]
        [TestCase(1, 0, 0, 1, false)]
        [TestCase(0, 0, 0, 0, false)]
        public void RequiredTestGateRejectsSkippedOrEmptyRuns(
            int passCount,
            int failCount,
            int inconclusiveCount,
            int skipCount,
            bool expected)
        {
            MethodInfo? method = typeof(WP0002GateDispatcher).GetMethod(
                "RequiredTestGatePassed",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            object? result = method!.Invoke(
                null,
                new object[]
                {
                    passCount,
                    failCount,
                    inconclusiveCount,
                    skipCount
                });
            Assert.That(result, Is.EqualTo(expected));
        }

        private static void InstallControllerState(
            LastBearingGameController controller,
            LastBearingState state)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo? stateField = typeof(LastBearingGameController).GetField(
                "_state",
                flags);
            FieldInfo? readModelField = typeof(LastBearingGameController).GetField(
                "_readModel",
                flags);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(readModelField, Is.Not.Null);
            stateField!.SetValue(controller, state);
            readModelField!.SetValue(controller, LastBearingReadModel.FromState(state));
        }

        private static int PendingCommandCount(LastBearingGameController controller)
        {
            FieldInfo? pendingField = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(pendingField, Is.Not.Null);
            var pending = pendingField!.GetValue(controller) as ICollection;
            Assert.That(pending, Is.Not.Null);
            return pending!.Count;
        }
    }
}
