#nullable enable

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
        public void AvailabilityTracksModeTrialAndPendingCommand()
        {
            LastBearingGameController controller =
                BuildController(ColonyComposition.Mixed);
            string canonicalBefore = controller.CanonicalHash;

            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.InspectCityNeed,
                expected: true);
            controller.InspectCityNeed();
            LastBearingFieldDeskProjection inspection =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(inspection.Survey.IsVisible, Is.True);
            Assert.That(
                inspection.PrimaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.SelectTrialA));
            Assert.That(
                inspection.SecondaryAction.Intent,
                Is.EqualTo(LastBearingFieldDeskIntent.SelectTrialB));
            Assert.That(inspection.PrimaryAction.Detail, Is.Not.Empty);
            Assert.That(inspection.SecondaryAction.Detail, Is.Not.Empty);
            Assert.That(
                inspection.SecondaryAction.Detail,
                Is.Not.EqualTo(inspection.PrimaryAction.Detail));
            AssertAvailable(controller, LastBearingFieldDeskIntent.SelectTrialA, true);
            AssertAvailable(controller, LastBearingFieldDeskIntent.SelectTrialB, true);

            controller.OpenGarageBay();
            LastBearingFieldDeskProjection garage =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(garage.PrimaryAction.IsVisible, Is.True);
            Assert.That(garage.PrimaryAction.IsEnabled, Is.False);
            AssertAvailable(controller, LastBearingFieldDeskIntent.SelectTrialA, false);

            controller.ShowCityOverview();
            CompleteDistrictObservation(controller);
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.ActivateInfrastructure,
                true);
            controller.ActivateInfrastructure();
            LastBearingFieldDeskProjection pending =
                LastBearingFieldDeskPresenter.Present(controller);
            Assert.That(controller.HasPendingPlayerCommands, Is.True);
            Assert.That(pending.PrimaryAction.IsEnabled, Is.False);
            Assert.That(pending.SaveAction.IsEnabled, Is.False);
            AssertAvailable(
                controller,
                LastBearingFieldDeskIntent.ActivateInfrastructure,
                false);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
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

        private static void CompleteDistrictObservation(
            LastBearingGameController controller)
        {
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.DistrictStamp);
            controller.ManipulateCityGrammarPrimary();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);
            Assert.That(controller.HasCompletedCityGrammarObservation, Is.True);
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
