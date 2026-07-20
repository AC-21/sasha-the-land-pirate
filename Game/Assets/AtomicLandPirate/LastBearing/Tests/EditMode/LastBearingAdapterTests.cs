#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
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
        public void PumpHallCutawayIsOneFixedPhysicsFreeDerivedDollhouse()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            LastBearingWorldBuilder world = controller.World!;
            LastBearingPumpHallCutawayView view = world.PumpHallCutawayView!;
            string canonicalBefore = controller.CanonicalHash;

            Assert.That(
                _root.GetComponentsInChildren<LastBearingPumpHallCutawayView>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                view.transform.IsChildOf(controller.ModeCoordinator!.GetModeRoot(
                    LastBearingPresentationMode.BuildingCutaway)),
                Is.True);
            Assert.That(
                LastBearingPumpHallCutawayView.DirectionPackageId,
                Is.EqualTo("C0-VGR-04"));
            Assert.That(
                LastBearingPumpHallCutawayView.ContentId,
                Is.EqualTo("bld_pump_hall_cutaway_a"));
            Assert.That(view.IsDollhouseCutaway, Is.True);
            Assert.That(view.HasRoof, Is.False);
            Assert.That(view.HasNearWall, Is.False);
            Assert.That(view.CameraAnchor, Is.Not.Null);
            Assert.That(view.FocusAnchor, Is.Not.Null);
            Assert.That(view.FixedCivicSocket, Is.Not.Null);
            Assert.That(
                view.FixedCivicSocket!.name,
                Is.EqualTo(LastBearingState.AuxiliaryPumpSocketId));
            Assert.That(
                view.FixedCivicSocket.localRotation,
                Is.EqualTo(Quaternion.identity));
            Assert.That(view.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(view.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<CharacterController>(true),
                Is.Empty);
            foreach (Collider collider in view.GetComponentsInChildren<Collider>(true))
            {
                Assert.That(collider.enabled, Is.False, collider.name);
            }

            view.Apply(
                HeavyCargoCustody.Settlement,
                CityImprovementKind.None,
                humanVisible: true,
                robotVisible: false);
            Assert.That(view.IsStagedRotorVisible, Is.True);
            Assert.That(view.IsInstalledPumpVisible, Is.False);
            Assert.That(view.IsHumanWorkerVisible, Is.True);
            Assert.That(view.IsRobotWorkerVisible, Is.False);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            view.Apply(
                HeavyCargoCustody.InstalledAtAuxiliaryPump,
                CityImprovementKind.RefurbishedAuxiliaryPump,
                humanVisible: true,
                robotVisible: true);
            Assert.That(view.IsStagedRotorVisible, Is.False);
            Assert.That(view.IsInstalledPumpVisible, Is.True);
            Assert.That(view.IsHumanWorkerVisible, Is.True);
            Assert.That(view.IsRobotWorkerVisible, Is.True);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
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
            CompleteDistrictObservation(controller, clear: true);
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
            Assert.That(pending!.Count, Is.EqualTo(0));
            Assert.That(controller.Status, Does.Contain("Complete either"));

            CompleteDistrictObservation(controller, clear: true);
            controller.ActivateInfrastructure();

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
        public void CityGrammarTrialsRetainSeparateNeutralServiceCellObservations()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            string canonicalBefore = controller.CanonicalHash;
            LastBearingCityGrammarComparison comparison =
                controller.World!.CityGrammarComparison!;

            controller.OpenGarageBay();
            LastBearingPresentationMode modeBeforeInvalid =
                controller.ModeCoordinator!.CurrentMode;
            Vector3 cameraPositionBeforeInvalid =
                controller.World.CameraRig!.transform.position;
            Quaternion cameraRotationBeforeInvalid =
                controller.World.CameraRig.transform.rotation;

            controller.SelectCityGrammarHypothesis(
                (LastBearingCityGrammarHypothesis)99);
            Assert.That(
                comparison.SelectedHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.Unselected));
            Assert.That(controller.World.CameraRig!.IsComparisonMode, Is.False);
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(modeBeforeInvalid));
            Assert.That(
                controller.World.CameraRig.transform.position,
                Is.EqualTo(cameraPositionBeforeInvalid));
            Assert.That(
                controller.World.CameraRig.transform.rotation,
                Is.EqualTo(cameraRotationBeforeInvalid));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            controller.ManipulateCityGrammarPrimary();
            controller.ToggleCityGrammarTrialPiece();
            controller.ManipulateCityGrammarPrimary();
            int interactionsBeforeRejectedLink = comparison.InteractionCount;
            controller.ConnectCityGrammarLogistics();

            Assert.That(comparison.HasValidSnapGridLayout, Is.False);
            Assert.That(comparison.IsLogisticsConnected, Is.False);
            Assert.That(
                comparison.InteractionCount,
                Is.EqualTo(interactionsBeforeRejectedLink));

            int interactionsBeforePrematureObservation = comparison.InteractionCount;
            Assert.That(
                comparison.RecordPathRead((LastBearingCityTrialPathRead)99),
                Is.False);
            controller.RecordCityGrammarPathRead(clear: true);
            Assert.That(
                comparison.InteractionCount,
                Is.EqualTo(interactionsBeforePrematureObservation));

            controller.ManipulateCityGrammarPrimary();
            controller.ConnectCityGrammarLogistics();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);

            int interactionsAfterObservation = comparison.InteractionCount;
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: false);

            Assert.That(comparison.HasValidSnapGridLayout, Is.True);
            Assert.That(comparison.RecyclerPadIndex, Is.EqualTo(0));
            Assert.That(comparison.WorkshopPadIndex, Is.EqualTo(1));
            Assert.That(
                comparison.PathRead,
                Is.EqualTo(LastBearingCityTrialPathRead.Clear));
            Assert.That(comparison.TrialReady, Is.True);
            Assert.That(
                comparison.InteractionCount,
                Is.EqualTo(interactionsAfterObservation));

            CompleteDistrictObservation(controller, clear: false);

            Assert.That(comparison.DistrictAnchorIndex, Is.EqualTo(0));
            Assert.That(
                comparison.PathRead,
                Is.EqualTo(LastBearingCityTrialPathRead.Unclear));
            Assert.That(comparison.TrialReady, Is.True);
            Assert.That(comparison.CompletedObservationCount, Is.EqualTo(2));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);

            Assert.That(comparison.RecyclerPadIndex, Is.EqualTo(0));
            Assert.That(comparison.WorkshopPadIndex, Is.EqualTo(1));
            Assert.That(
                comparison.PathRead,
                Is.EqualTo(LastBearingCityTrialPathRead.Clear));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("A{layout="));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("B{layout="));
            Assert.That(controller.CityGrammarEvidence, Does.Not.Contain("winner"));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [Test]
        public void EitherObservedTrialConvergesOnIdenticalCanonicalInfrastructure()
        {
            _root = new GameObject("City Trial Convergence");
            var snapRoot = new GameObject("Snap Trial Controller");
            snapRoot.transform.SetParent(_root.transform, false);
            var snapController = snapRoot.AddComponent<LastBearingGameController>();
            snapController.Initialize();
            snapController.StartNewGame(ColonyComposition.Mixed);
            snapController.InspectCityNeed();
            CompleteSnapGridObservation(snapController, clear: true);
            snapController.ActivateInfrastructure();
            SimulateOneTick(snapController);

            var stampRoot = new GameObject("Stamp Trial Controller");
            stampRoot.transform.SetParent(_root.transform, false);
            var stampController = stampRoot.AddComponent<LastBearingGameController>();
            stampController.Initialize();
            stampController.StartNewGame(ColonyComposition.Mixed);
            stampController.InspectCityNeed();
            CompleteDistrictObservation(stampController, clear: false);
            stampController.ActivateInfrastructure();
            SimulateOneTick(stampController);

            Assert.That(snapController.State!.SliceInfrastructureActive, Is.True);
            Assert.That(stampController.State!.SliceInfrastructureActive, Is.True);
            Assert.That(snapController.CanonicalHash, Is.EqualTo(stampController.CanonicalHash));
            Assert.That(
                snapController.CityGrammarHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.Unselected));
            Assert.That(
                stampController.CityGrammarHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.Unselected));
            Assert.That(snapController.Status, Does.Contain("records no layout"));
            Assert.That(stampController.Status, Does.Contain("records no layout"));
        }

        [Test]
        public void CityGrammarTrialClearsAtSessionBoundaries()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.HumanOnly);
            controller.InspectCityNeed();
            CompleteDistrictObservation(controller, clear: true);

            Assert.That(controller.HasCompletedCityGrammarObservation, Is.True);

            controller.ReturnToTitle();
            controller.StartNewGame(ColonyComposition.RobotOnly);

            Assert.That(controller.HasCompletedCityGrammarObservation, Is.False);
            Assert.That(
                controller.CityGrammarHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.Unselected));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("observations=0"));
        }

        [Test]
        public void ClearingBothCityGrammarTrialsClearsObservationEvidence()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            string canonicalBefore = controller.CanonicalHash;
            LastBearingCityGrammarComparison comparison =
                controller.World!.CityGrammarComparison!;

            CompleteSnapGridObservation(controller, clear: true);
            CompleteDistrictObservation(controller, clear: false);
            Assert.That(comparison.CompletedObservationCount, Is.EqualTo(2));

            controller.ResetCityGrammarComparison();

            Assert.That(comparison.HasCompletedObservation, Is.False);
            Assert.That(comparison.CompletedObservationCount, Is.EqualTo(0));
            Assert.That(
                comparison.LastCompletedHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.Unselected));
            Assert.That(
                comparison.SelectedHypothesis,
                Is.EqualTo(LastBearingCityGrammarHypothesis.Unselected));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("observations=0"));
            Assert.That(controller.CityGrammarEvidence, Does.Contain("path=Unrecorded"));
            Assert.That(PendingCommandCount(controller), Is.EqualTo(0));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
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
        public void GaragePlanningIntentIsSwitchableCancelableAndCanonicalNoOp()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            PrepareControllerForGaragePlan(controller);
            byte[] canonicalBefore = LastBearingCanonicalCodec.Encode(
                controller.State!);
            LastBearingState stateBefore = controller.State!;
            long sequenceBefore = stateBefore.NextCommandSequence;

            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);

            Assert.That(controller.IsGaragePlanIntentActive, Is.True);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.WorkshopPush));
            Assert.That(controller.IsGaragePlanCommitAvailable, Is.True);
            Assert.That(
                controller.ModeCoordinator!.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.GarageBay));
            Assert.That(PendingCommandCount(controller), Is.Zero);
            Assert.That(controller.State, Is.SameAs(stateBefore));
            Assert.That(controller.State!.NextCommandSequence, Is.EqualTo(sequenceBefore));
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State));

            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);

            Assert.That(controller.IsGaragePlanIntentActive, Is.True);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.CivicBuffer));
            Assert.That(controller.IsGaragePlanCommitAvailable, Is.True);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!));

            controller.CancelGaragePlan();

            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.Unselected));
            Assert.That(controller.IsGaragePlanCommitAvailable, Is.False);
            Assert.That(
                controller.ModeCoordinator.CurrentMode,
                Is.EqualTo(LastBearingPresentationMode.CityOverview));
            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                canonicalBefore,
                LastBearingCanonicalCodec.Encode(controller.State!));
        }

        [TestCase(ColonyComposition.HumanOnly, PreparationChoice.WorkshopPush, VehicleModule.WinchAssembly)]
        [TestCase(ColonyComposition.HumanOnly, PreparationChoice.WorkshopPush, VehicleModule.SealedRangeTank)]
        [TestCase(ColonyComposition.HumanOnly, PreparationChoice.CivicBuffer, VehicleModule.WinchAssembly)]
        [TestCase(ColonyComposition.HumanOnly, PreparationChoice.CivicBuffer, VehicleModule.SealedRangeTank)]
        [TestCase(ColonyComposition.RobotOnly, PreparationChoice.WorkshopPush, VehicleModule.WinchAssembly)]
        [TestCase(ColonyComposition.RobotOnly, PreparationChoice.WorkshopPush, VehicleModule.SealedRangeTank)]
        [TestCase(ColonyComposition.RobotOnly, PreparationChoice.CivicBuffer, VehicleModule.WinchAssembly)]
        [TestCase(ColonyComposition.RobotOnly, PreparationChoice.CivicBuffer, VehicleModule.SealedRangeTank)]
        [TestCase(ColonyComposition.Mixed, PreparationChoice.WorkshopPush, VehicleModule.WinchAssembly)]
        [TestCase(ColonyComposition.Mixed, PreparationChoice.WorkshopPush, VehicleModule.SealedRangeTank)]
        [TestCase(ColonyComposition.Mixed, PreparationChoice.CivicBuffer, VehicleModule.WinchAssembly)]
        [TestCase(ColonyComposition.Mixed, PreparationChoice.CivicBuffer, VehicleModule.SealedRangeTank)]
        public void GarageCommitMatchesExistingCanonicalCommandPair(
            ColonyComposition composition,
            PreparationChoice preparation,
            VehicleModule module)
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            PrepareControllerForGaragePlan(controller, composition);
            LastBearingState baseline = controller.State!;
            LastBearingState expected = ApplyPlanCommands(
                baseline,
                preparation,
                module);

            controller.BeginGaragePlan(preparation);
            controller.CommitGaragePlan(module);

            LastBearingCommand[] pending = PendingCommands(controller);
            Assert.That(pending, Has.Length.EqualTo(2));
            Assert.That(pending[0], Is.TypeOf<SelectPreparationCommand>());
            Assert.That(pending[1], Is.TypeOf<InstallVehicleModuleCommand>());
            var selected = (SelectPreparationCommand)pending[0];
            var installed = (InstallVehicleModuleCommand)pending[1];
            Assert.That(selected.Sequence, Is.EqualTo(baseline.NextCommandSequence));
            Assert.That(selected.Choice, Is.EqualTo(preparation));
            Assert.That(selected.PlannedModule, Is.EqualTo(module));
            Assert.That(installed.Sequence, Is.EqualTo(baseline.NextCommandSequence + 1));
            Assert.That(installed.Module, Is.EqualTo(module));
            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.Unselected));

            controller.CommitGaragePlan(module);
            Assert.That(
                PendingCommandCount(controller),
                Is.EqualTo(2),
                "a duplicate commitment must not append a second command pair");

            SimulateOneTick(controller);

            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                LastBearingCanonicalCodec.Encode(expected),
                LastBearingCanonicalCodec.Encode(controller.State!));
        }

        [Test]
        public void GarageCommitGuardsFailClosedAcrossInvalidNonGarageStaleAndAwayStates()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();

            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(PendingCommandCount(controller), Is.Zero);

            controller.StartNewGame(ColonyComposition.HumanOnly);
            byte[] earlyBytes = LastBearingCanonicalCodec.Encode(controller.State!);
            controller.BeginGaragePlan(PreparationChoice.Unselected);
            controller.BeginGaragePlan((PreparationChoice)99);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                earlyBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            PrepareControllerForGaragePlan(controller);
            byte[] readyBytes = LastBearingCanonicalCodec.Encode(controller.State!);
            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);
            controller.CommitGaragePlan(VehicleModule.None);
            controller.CommitGaragePlan((VehicleModule)99);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                readyBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            Assert.That(
                controller.ModeCoordinator!.TryShowCityMode(
                    LastBearingPresentationMode.CityOverview,
                    controller.ReadModel),
                Is.True);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                readyBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            LastBearingState stale = ApplyPlanCommands(
                controller.State!,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            SynchronizeControllerState(controller, stale);
            byte[] staleBytes = LastBearingCanonicalCodec.Encode(stale);
            controller.CommitGaragePlan(VehicleModule.SealedRangeTank);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                staleBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));

            PrepareControllerForGaragePlan(controller);
            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);
            LastBearingState outbound = CreateOutboundState(controller.State!);
            SynchronizeControllerState(controller, outbound);
            byte[] outboundBytes = LastBearingCanonicalCodec.Encode(outbound);

            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(controller.IsGaragePlanCommitAvailable, Is.False);
            controller.CommitGaragePlan(VehicleModule.WinchAssembly);
            Assert.That(PendingCommandCount(controller), Is.Zero);
            CollectionAssert.AreEqual(
                outboundBytes,
                LastBearingCanonicalCodec.Encode(controller.State!));
        }

        [Test]
        public void GaragePlanningIntentClearsAtTitleAndNewGameBoundaries()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            PrepareControllerForGaragePlan(controller);
            controller.BeginGaragePlan(PreparationChoice.WorkshopPush);

            controller.ReturnToTitle();

            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.Unselected));
            Assert.That(PendingCommandCount(controller), Is.Zero);

            PrepareControllerForGaragePlan(controller);
            controller.BeginGaragePlan(PreparationChoice.CivicBuffer);
            controller.StartNewGame(ColonyComposition.RobotOnly);

            Assert.That(controller.IsGaragePlanIntentActive, Is.False);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.Unselected));
            Assert.That(PendingCommandCount(controller), Is.Zero);
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

        [Test]
        public void NativePerformanceGateIdsAreFixedAndNonPathValued()
        {
            Assert.That(
                WP0002GateDispatcher.NativeBuildGate,
                Is.EqualTo("wp0002-native-il2cpp-arm64-build"));
            Assert.That(
                WP0002GateDispatcher.NativePerformanceStartGate,
                Is.EqualTo(
                    "wp0002-native-il2cpp-arm64-performance-start"));
            Assert.That(
                WP0002GateDispatcher.NativePerformanceCollectGate,
                Is.EqualTo(
                    "wp0002-native-il2cpp-arm64-performance-collect"));
            foreach (string gateId in new[]
            {
                WP0002GateDispatcher.NativeBuildGate,
                WP0002GateDispatcher.NativePerformanceStartGate,
                WP0002GateDispatcher.NativePerformanceCollectGate
            })
            {
                Assert.That(gateId, Does.Not.Contain("/"));
                Assert.That(gateId, Does.Not.Contain("\\"));
                Assert.That(gateId, Does.Not.Contain(".."));
            }
        }

        [TestCase("0123456789ab-0123456789abcdef0123456789abcdef", true)]
        [TestCase("0123456789AB-0123456789abcdef0123456789abcdef", false)]
        [TestCase("0123456789ab_0123456789abcdef0123456789abcdef", false)]
        [TestCase("0123456789ab-0123456789abcdef0123456789abcdeg", false)]
        [TestCase("runs/0123456789ab-0123456789abcdef0123456789abcdef", false)]
        public void NativeRunDirectoryNameIsExactLowerHexIdentity(
            string value,
            bool expectedValid)
        {
            MethodInfo? method = typeof(WP0002GateDispatcher).GetMethod(
                "ValidateNativeRunDirectoryName",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            if (expectedValid)
            {
                Assert.DoesNotThrow(() =>
                    method!.Invoke(null, new object[] { value }));
            }
            else
            {
                TargetInvocationException? exception = Assert.Throws<
                    TargetInvocationException>(() =>
                        method!.Invoke(null, new object[] { value }));
                Assert.That(
                    exception!.InnerException,
                    Is.TypeOf<InvalidOperationException>());
            }
        }

        [Test]
        public void NativePerformanceReportUsesDoubleDurationFields()
        {
            Type? reportType = typeof(WP0002GateDispatcher).GetNestedType(
                "NativePerformanceReport",
                BindingFlags.NonPublic);
            Assert.That(reportType, Is.Not.Null);
            FieldInfo? requested = reportType!.GetField(
                "requested_warmup_seconds",
                BindingFlags.Public | BindingFlags.Instance);
            FieldInfo? actual = reportType.GetField(
                "actual_warmup_seconds",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(requested, Is.Not.Null);
            Assert.That(actual, Is.Not.Null);
            Assert.That(requested!.FieldType, Is.EqualTo(typeof(double)));
            Assert.That(actual!.FieldType, Is.EqualTo(typeof(double)));
        }

        [Test]
        public void NativeGateKeepsBuildAndRunTrustOnlyInStaticEditorMemory()
        {
            foreach (string fieldName in new[]
            {
                "_trustedNativeBuild",
                "_trustedNativeRun",
                "_scheduledNativeBuild",
                "_quarantinedNativeProcesses",
                "_nativePlayerReloadLockHeld"
            })
            {
                FieldInfo? field = typeof(WP0002GateDispatcher).GetField(
                    fieldName,
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(field, Is.Not.Null, fieldName);
                Assert.That(field!.IsStatic, Is.True, fieldName);
                Assert.That(
                    Attribute.IsDefined(
                        field,
                        typeof(SerializeField)),
                    Is.False,
                    fieldName);
            }
        }

        [Test]
        public void NativePlayerCleanupRetainsAnExactFailClosedProcessHandle()
        {
            const BindingFlags staticFlags =
                BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo? quarantine = typeof(WP0002GateDispatcher).GetField(
                "_quarantinedNativeProcesses",
                staticFlags);
            Assert.That(quarantine, Is.Not.Null);
            Assert.That(quarantine!.IsStatic, Is.True);
            Assert.That(quarantine.IsInitOnly, Is.True);

            Type? cleanupType = typeof(WP0002GateDispatcher).GetNestedType(
                "NativeProcessCleanup",
                BindingFlags.NonPublic);
            Assert.That(cleanupType, Is.Not.Null);
            PropertyInfo? process = cleanupType!.GetProperty(
                "Process",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(process, Is.Not.Null);
            Assert.That(
                process!.PropertyType,
                Is.EqualTo(typeof(System.Diagnostics.Process)));

            MethodInfo? terminate = typeof(WP0002GateDispatcher).GetMethod(
                "TerminateProcess",
                staticFlags,
                null,
                new[]
                {
                    typeof(System.Diagnostics.Process),
                    typeof(string).MakeByRefType()
                },
                null);
            Assert.That(terminate, Is.Not.Null);
            Assert.That(terminate!.ReturnType, Is.EqualTo(typeof(bool)));

            MethodInfo? reject = typeof(WP0002GateDispatcher).GetMethod(
                "RejectNativeGateWhileCleanupQuarantined",
                staticFlags);
            Assert.That(reject, Is.Not.Null);
            MethodInfo? allowQuit = typeof(WP0002GateDispatcher).GetMethod(
                "AllowEditorQuitAfterNativeCleanup",
                staticFlags);
            Assert.That(allowQuit, Is.Not.Null);
            Assert.That(allowQuit!.ReturnType, Is.EqualTo(typeof(bool)));
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

        private static LastBearingCommand[] PendingCommands(
            LastBearingGameController controller)
        {
            FieldInfo? pendingField = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(pendingField, Is.Not.Null);
            var pending = pendingField!.GetValue(controller) as
                IEnumerable<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            return pending!.ToArray();
        }

        private static void PrepareControllerForGaragePlan(
            LastBearingGameController controller,
            ColonyComposition composition = ColonyComposition.HumanOnly)
        {
            controller.StartNewGame(composition);
            controller.InspectCityNeed();
            CompleteDistrictObservation(controller, clear: true);
            controller.ActivateInfrastructure();
            SimulateOneTick(controller);

            Assert.That(controller.ReadModel, Is.Not.Null);
            Assert.That(controller.State!.SliceInfrastructureActive, Is.True);
            Assert.That(
                controller.ReadModel.PreparationChoice,
                Is.EqualTo(PreparationChoice.Unselected));
            Assert.That(PendingCommandCount(controller), Is.Zero);
        }

        private static LastBearingState ApplyPlanCommands(
            LastBearingState state,
            PreparationChoice preparation,
            VehicleModule module)
        {
            var kernel = new LastBearingKernel();
            long sequence = state.NextCommandSequence;
            return kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new SelectPreparationCommand(
                        sequence,
                        preparation,
                        module),
                    new InstallVehicleModuleCommand(sequence + 1, module),
                }).State;
        }

        private static LastBearingState CreateOutboundState(
            LastBearingState readyForPlan)
        {
            var kernel = new LastBearingKernel();
            LastBearingState state = ApplyPlanCommands(
                readyForPlan,
                PreparationChoice.WorkshopPush,
                VehicleModule.WinchAssembly);
            var guard = 0;
            while ((state.PreparationPhase != PreparationPhase.Ready ||
                    state.ModuleInstallationState !=
                    ModuleInstallationState.Installed) &&
                   guard < 1000)
            {
                state = kernel.Step(
                    state,
                    Array.Empty<LastBearingCommand>()).State;
                guard++;
            }

            Assert.That(state.PreparationPhase, Is.EqualTo(PreparationPhase.Ready));
            Assert.That(
                state.ModuleInstallationState,
                Is.EqualTo(ModuleInstallationState.Installed));
            long sequence = state.NextCommandSequence;
            state = kernel.Step(
                state,
                new LastBearingCommand[]
                {
                    new PrepareExpeditionTransactionCommand(
                        sequence,
                        "tx:vgr09-away-guard",
                        "fp:vgr09-away-guard"),
                    new DebitCityManifestCommand(
                        sequence + 1,
                        "tx:vgr09-away-guard",
                        "fp:vgr09-away-guard"),
                    new DepartExpeditionCommand(sequence + 2),
                }).State;
            Assert.That(state.ExpeditionPhase, Is.EqualTo(ExpeditionPhase.Outbound));
            return state;
        }

        private static void SynchronizeControllerState(
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
            FieldInfo? pendingField = typeof(LastBearingGameController).GetField(
                "_pendingCommands",
                flags);
            MethodInfo? applyPresentation = typeof(LastBearingGameController).GetMethod(
                "ApplyPresentation",
                flags);
            Assert.That(stateField, Is.Not.Null);
            Assert.That(readModelField, Is.Not.Null);
            Assert.That(pendingField, Is.Not.Null);
            Assert.That(applyPresentation, Is.Not.Null);
            stateField!.SetValue(controller, state);
            readModelField!.SetValue(controller, LastBearingReadModel.FromState(state));
            var pending = pendingField!.GetValue(controller) as
                List<LastBearingCommand>;
            Assert.That(pending, Is.Not.Null);
            pending!.Clear();
            applyPresentation!.Invoke(controller, null);
        }

        private static void CompleteSnapGridObservation(
            LastBearingGameController controller,
            bool clear)
        {
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            controller.ManipulateCityGrammarPrimary();
            controller.ToggleCityGrammarTrialPiece();
            controller.ManipulateCityGrammarPrimary();
            controller.ManipulateCityGrammarPrimary();
            controller.ConnectCityGrammarLogistics();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear);
        }

        private static void CompleteDistrictObservation(
            LastBearingGameController controller,
            bool clear)
        {
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear);
        }

        private static void SimulateOneTick(LastBearingGameController controller)
        {
            MethodInfo? simulate = typeof(LastBearingGameController).GetMethod(
                "SimulateOneTick",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(simulate, Is.Not.Null);
            simulate!.Invoke(controller, null);
        }
    }
}
