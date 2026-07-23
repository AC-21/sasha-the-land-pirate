#nullable enable

using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingFieldDeskPresenterTests
    {
        private GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (LastBearingGameController controller in
                     Object.FindObjectsByType<LastBearingGameController>(
                         FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(controller.gameObject);
            }
        }

        [TestCase(ColonyComposition.HumanOnly, "HUMAN SETTLEMENT")]
        [TestCase(ColonyComposition.RobotOnly, "UTILITY-ROBOT SETTLEMENT")]
        [TestCase(ColonyComposition.Mixed, "HUMAN + UTILITY-ROBOT SETTLEMENT")]
        public void CityProjectionIsTruthfulPureAndUsesPermitJob(
            ColonyComposition composition,
            string expectedComposition)
        {
            LastBearingGameController controller = BuildController(composition);
            LastBearingReadModel model = controller.ReadModel!;
            string canonicalBefore = controller.CanonicalHash;

            LastBearingFieldDeskProjection projection =
                LastBearingFieldDeskPresenter.Present(controller);
            LastBearingPermitJobPresentation expectedJob =
                LastBearingPermitJobPresenter.Present(model, false);

            Assert.That(projection.Composition, Is.EqualTo(expectedComposition));
            Assert.That(projection.PauseState, Is.EqualTo("CLOCKS RUNNING"));
            Assert.That(projection.WaterAmount, Does.EndWith(" WATER"));
            Assert.That(
                projection.WaterTrend,
                Is.EqualTo("-0.010 WATER / SETTLEMENT TICK"));
            Assert.That(projection.Parts, Is.EqualTo(model.PartsUnits + " UNITS"));
            Assert.That(projection.Fuel, Is.EqualTo(model.FuelUnits + " UNITS"));
            Assert.That(projection.Turbine, Does.Contain("FAILING"));
            Assert.That(projection.Pressure, Does.Contain("FALLING"));
            Assert.That(
                projection.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.InspectCityNeed));
            Assert.That(projection.PrimaryAction.IsEnabled, Is.True);
            Assert.That(projection.Survey.IsVisible, Is.False);
            Assert.That(projection.PermitJob.Chapter, Is.EqualTo(expectedJob.Chapter));
            Assert.That(projection.PermitJob.StepIndex, Is.EqualTo(expectedJob.StepIndex));
            Assert.That(projection.PermitJob.Headline, Is.EqualTo(expectedJob.Headline));
            Assert.That(projection.PermitJob.Detail, Is.EqualTo(expectedJob.Detail));
            Assert.That(
                projection.PermitJob.ProgressLabel,
                Is.EqualTo(expectedJob.ProgressLabel));
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        [Test]
        public void IntentValuesRetainEighteenBindingContract()
        {
            Assert.That((int)LastBearingFieldDeskIntent.SelectRecycler, Is.EqualTo(3));
            Assert.That((int)LastBearingFieldDeskIntent.SelectMachineShop, Is.EqualTo(4));
            Assert.That((int)LastBearingFieldDeskIntent.SelectEmergencyStorage, Is.EqualTo(5));
            Assert.That((int)LastBearingFieldDeskIntent.RotateCityBuilding, Is.EqualTo(6));
            Assert.That((int)LastBearingFieldDeskIntent.PreviousCityPad, Is.EqualTo(7));
            Assert.That((int)LastBearingFieldDeskIntent.NextCityPad, Is.EqualTo(8));
            Assert.That((int)LastBearingFieldDeskIntent.PlaceCityBuilding, Is.EqualTo(9));
            Assert.That((int)LastBearingFieldDeskIntent.ConnectCityServiceLink, Is.EqualTo(10));
            Assert.That((int)LastBearingFieldDeskIntent.StaffCityServiceHuman, Is.EqualTo(11));
            Assert.That((int)LastBearingFieldDeskIntent.StaffCityServiceRobot, Is.EqualTo(12));
            Assert.That((int)LastBearingFieldDeskIntent.AdvanceCityServiceSled, Is.EqualTo(13));
            Assert.That((int)LastBearingFieldDeskIntent.CancelCityBuildingPreview, Is.EqualTo(14));
            Assert.That((int)LastBearingFieldDeskIntent.ActivateInfrastructure, Is.EqualTo(15));
            Assert.That((int)LastBearingFieldDeskIntent.TogglePause, Is.EqualTo(24));
        }

        [Test]
        public void WorkingServiceCellTracksObjectivesCostsLockAndDelivery()
        {
            LastBearingGameController controller =
                BuildController(ColonyComposition.Mixed);
            string canonicalBefore = controller.CanonicalHash;

            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.InspectCityNeed,
                expected: true);
            controller.InspectCityNeed();
            LastBearingFieldDeskProjection recyclerOrder =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(recyclerOrder.Survey.IsVisible, Is.True);
            Assert.That(
                recyclerOrder.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.SelectRecycler));
            Assert.That(
                recyclerOrder.PrimaryAction.Label,
                Does.Contain("2 PARTS"));
            Assert.That(
                recyclerOrder.Survey.Evidence,
                Does.Contain("MOVES FREE BEFORE LINK"));
            Assert.That(
                recyclerOrder.Survey.Evidence,
                Does.Contain("LINK LOCKS PERMANENTLY FOR 1 PART"));
            Assert.That(
                recyclerOrder.Survey.Evidence,
                Does.Contain("OPERATOR IS NEUTRAL"));
            Assert.That(
                recyclerOrder.Survey.Evidence,
                Does.Contain("+2 PARTS ONCE"));
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.SelectRecycler,
                true);

            controller.OpenGarageBay();
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.SelectRecycler,
                false);

            controller.ShowCityOverview();
            controller.SelectCityBuildingPreview(CityBuildingKind.Recycler);
            LastBearingFieldDeskProjection recyclerPreview =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                recyclerPreview.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.PlaceCityBuilding));
            Assert.That(recyclerPreview.PrimaryAction.Label, Does.Contain("2 PARTS"));
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.RotateCityBuilding,
                true);
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.PreviousCityPad,
                true);
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.NextCityPad,
                true);
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.CancelCityBuildingPreview,
                true);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            long startingParts = controller.ReadModel!.PartsUnits;
            PlaceAndAccept(controller);
            Assert.That(
                controller.ReadModel!.NextObjective,
                Is.EqualTo("place-city-machine-shop"));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(startingParts - 2));

            LastBearingFieldDeskProjection shopOrder =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                shopOrder.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.SelectMachineShop));
            Assert.That(shopOrder.PrimaryAction.Label, Does.Contain("3 PARTS"));
            controller.SelectCityBuildingPreview(CityBuildingKind.MachineShop);
            PlaceAndAccept(controller);
            Assert.That(
                controller.ReadModel.NextObjective,
                Is.EqualTo("place-city-emergency-storage"));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(startingParts - 5));

            LastBearingFieldDeskProjection storageOrder =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                storageOrder.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.SelectEmergencyStorage));
            Assert.That(storageOrder.PrimaryAction.Label, Does.Contain("1 PART"));
            controller.SelectCityBuildingPreview(
                CityBuildingKind.EmergencyStorage);
            PlaceAndAccept(controller);
            Assert.That(
                controller.ReadModel.NextObjective,
                Is.EqualTo("connect-city-service-link"));
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(startingParts - 6));

            LastBearingFieldDeskProjection linkOrder =
                LastBearingFieldDeskPresenter.Present(controller);
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.ConnectCityServiceLink,
                true);
            Assert.That(linkOrder.PrimaryAction.Label, Does.Contain("1 PART"));
            Assert.That(linkOrder.PrimaryAction.Detail, Does.Contain("Permanent"));

            controller.SelectCityBuildingPreview(CityBuildingKind.Recycler);
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.ConnectCityServiceLink,
                false);
            controller.CancelCityBuildingPreview();
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.ConnectCityServiceLink,
                true);

            controller.ConnectCityServiceLink();
            LastBearingFieldDeskProjection pending =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(controller.HasPendingPlayerCommands, Is.True);
            Assert.That(pending.PrimaryAction.IsEnabled, Is.False);
            Assert.That(pending.SaveAction.IsEnabled, Is.False);
            SimulateOneTick(controller);
            Assert.That(controller.ReadModel.CityServiceLinkConnected, Is.True);
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(startingParts - 7));
            Assert.That(
                LastBearingFieldDeskPresenter.Present(controller)
                    .Survey.ConnectLink.Label,
                Is.EqualTo("SERVICE LINK · LOCKED"));
            Assert.That(
                controller.ReadModel.NextObjective,
                Is.EqualTo("staff-city-service-cell"));

            LastBearingFieldDeskProjection staffOrder =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                staffOrder.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.StaffCityServiceHuman));
            Assert.That(
                staffOrder.SecondaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.StaffCityServiceRobot));
            Assert.That(staffOrder.PrimaryAction.Detail, Does.Contain("no V0 bonus"));
            Assert.That(staffOrder.SecondaryAction.Detail, Does.Contain("no V0 bonus"));
            controller.AssignCityServiceResident(ResidentRoster.RobotResidentId);
            SimulateOneTick(controller);
            Assert.That(
                controller.ReadModel.CityServiceResidentId,
                Is.EqualTo(ResidentRoster.RobotResidentId));
            Assert.That(
                controller.ReadModel.NextObjective,
                Is.EqualTo("advance-city-service-sled"));

            long beforeDelivery = controller.ReadModel.PartsUnits;
            LastBearingFieldDeskProjection sledOrder =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(
                sledOrder.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.AdvanceCityServiceSled));
            controller.AdvanceCityServiceSled();
            SimulateOneTick(controller);
            Assert.That(
                controller.ReadModel.CityDeliveryStage,
                Is.EqualTo(CityDeliveryStage.InTransit));
            Assert.That(controller.ReadModel.PartsUnits, Is.EqualTo(beforeDelivery));
            LastBearingFieldDeskProjection deliveryOrder =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(deliveryOrder.PrimaryAction.Label, Does.Contain("+2 PARTS"));
            controller.AdvanceCityServiceSled();
            SimulateOneTick(controller);
            Assert.That(controller.ReadModel.CityDeliveryCount, Is.EqualTo(1));
            Assert.That(controller.ReadModel.SliceInfrastructureActive, Is.True);
            Assert.That(
                controller.ReadModel.PartsUnits,
                Is.EqualTo(beforeDelivery + 2));
            Assert.That(
                LastBearingFieldDeskPresenter.Present(controller).Survey.IsVisible,
                Is.False);
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.ActivateInfrastructure,
                false);
        }

        private LastBearingGameController BuildController(
            ColonyComposition composition)
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(composition);
            Assert.That(controller.IsExactFieldDeskCityOverview, Is.True);
            return controller;
        }

        private static void PlaceAndAccept(
            LastBearingGameController controller)
        {
            controller.PlaceCityBuildingPreview();
            Assert.That(controller.HasPendingPlayerCommands, Is.True);
            SimulateOneTick(controller);
            Assert.That(controller.HasPendingPlayerCommands, Is.False);
        }

        private static void SimulateOneTick(
            LastBearingGameController controller)
        {
            MethodInfo? simulate = typeof(LastBearingGameController).GetMethod(
                "SimulateOneTick",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(simulate, Is.Not.Null);
            simulate!.Invoke(controller, null);
        }

        private static void AssertAvailable(
            LastBearingGameController controller,
            LastBearingFieldDeskIntent intent,
            bool expected)
        {
            Assert.That(
                LastBearingFieldDeskPresenter.IsIntentAvailable(controller, intent),
                Is.EqualTo(expected));
        }
    }
}
