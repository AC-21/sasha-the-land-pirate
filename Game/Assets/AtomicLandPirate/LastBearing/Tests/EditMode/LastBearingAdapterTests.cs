#nullable enable

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AtomicLandPirate.Presentation.LastBearing.Editor;
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
            Assert.That(controller.World.TurbineRotor, Is.Not.Null);
            Assert.That(controller.World.WaterFill, Is.Not.Null);
            Assert.That(
                _root.GetComponentsInChildren<Renderer>(includeInactive: true).Length,
                Is.GreaterThan(30));
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
