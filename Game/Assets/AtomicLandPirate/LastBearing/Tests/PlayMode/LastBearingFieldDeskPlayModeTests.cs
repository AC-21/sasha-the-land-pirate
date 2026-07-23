#nullable enable

using System.Collections;
using System.Reflection;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingFieldDeskPlayModeTests
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

        [UnityTest]
        public IEnumerator DeskOwnsOnlyCityAndReusesDocumentAcrossLifecycle()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            LastBearingFieldDesk desk = RequireDesk(controller);
            UIDocument document = RequireDocument(controller);
            Assert.That(document.panelSettings.themeStyleSheet, Is.Not.Null);
            Assert.That(
                document.panelSettings.scaleMode,
                Is.EqualTo(PanelScaleMode.ConstantPixelSize));
            Assert.That(document.panelSettings.scale, Is.EqualTo(1f));
            VisualElement overlay = document.rootVisualElement.Q<VisualElement>(
                "field-desk-overlay");
            Assert.That(overlay, Is.Not.Null);

            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.True);
            Assert.That(overlay.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            LastBearingHud legacyHud =
                controller.GetComponent<LastBearingHud>();
            Assert.That(legacyHud.enabled, Is.False);

            desk.ResetForLifecycle();
            Assert.That(desk.OwnsCityOverview, Is.False);
            Assert.That(overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(legacyHud.enabled, Is.True);
            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.True);
            Assert.That(legacyHud.enabled, Is.False);

            controller.OpenGarageBay();
            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.False);
            Assert.That(overlay.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(legacyHud.enabled, Is.True);
            Assert.That(RequireDocument(controller), Is.SameAs(document));

            controller.ShowCityOverview();
            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.True);
            Assert.That(legacyHud.enabled, Is.False);

            controller.ReturnToTitle();
            Assert.That(desk.OwnsCityOverview, Is.False);
            Assert.That(legacyHud.enabled, Is.True);

            controller.StartNewGame(ColonyComposition.RobotOnly);
            desk.Refresh(force: true);
            Assert.That(desk.OwnsCityOverview, Is.True);
            Assert.That(legacyHud.enabled, Is.False);
            Assert.That(RequireDocument(controller), Is.SameAs(document));
            Assert.That(
                controller.GetComponentsInChildren<UIDocument>(true),
                Has.Length.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DeskRendersBothOrderExplanationsAndClearsFocusOnExit()
        {
            LastBearingGameController controller = BuildController();
            yield return null;
            LastBearingFieldDesk desk = RequireDesk(controller);
            UIDocument document = RequireDocument(controller);

            controller.InspectCityNeed();
            desk.Refresh(force: true);
            LastBearingFieldDeskProjection projection =
                LastBearingFieldDeskPresenter.Present(controller);
            Label primaryDetail = document.rootVisualElement.Q<Label>(
                "current-action-detail");
            Label secondaryDetail = document.rootVisualElement.Q<Label>(
                "secondary-action-detail");
            Button secondary = document.rootVisualElement.Q<Button>(
                "secondary-action-button");

            Assert.That(primaryDetail.text, Is.EqualTo(projection.PrimaryAction.Detail));
            Assert.That(
                secondaryDetail.text,
                Is.EqualTo(projection.SecondaryAction.Detail));
            Assert.That(
                secondaryDetail.style.display.value,
                Is.EqualTo(DisplayStyle.Flex));

            secondary.Focus();
            yield return null;
            Assert.That(
                document.rootVisualElement.panel.focusController.focusedElement,
                Is.SameAs(secondary));
            controller.OpenGarageBay();
            desk.Refresh(force: true);
            Assert.That(
                document.rootVisualElement.panel.focusController.focusedElement,
                Is.Not.SameAs(secondary));
        }

        [UnityTest]
        public IEnumerator SubmitDelegatesOnceAcrossSameFrameModeReentry()
        {
            LastBearingGameController controller = BuildController();
            LastBearingFieldDesk desk = RequireDesk(controller);
            yield return null;
            controller.InspectCityNeed();
            CompleteDistrictObservation(controller);
            controller.ActivateInfrastructure();
            InvokeSimulationTick(controller);
            desk.Refresh(force: true);

            Button primary = RequireDocument(controller)
                .rootVisualElement.Q<Button>("primary-action-button");
            Assert.That(primary, Is.Not.Null);
            Assert.That(primary.text, Is.EqualTo("PENCIL CIVIC BUFFER"));
            string canonicalBefore = controller.CanonicalHash;

            Submit(primary);
            AssertMode(controller, LastBearingPresentationMode.GarageBay);
            Assert.That(
                controller.GaragePreparationIntent,
                Is.EqualTo(PreparationChoice.CivicBuffer));
            controller.ShowCityOverview();
            desk.Refresh(force: true);
            Submit(primary);
            AssertMode(controller, LastBearingPresentationMode.CityOverview);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));

            yield return null;
            desk.Refresh(force: true);
            Submit(primary);
            AssertMode(controller, LastBearingPresentationMode.GarageBay);
            Assert.That(controller.CanonicalHash, Is.EqualTo(canonicalBefore));
        }

        private LastBearingGameController BuildController()
        {
            _root = new GameObject(LastBearingGameController.RuntimeRootName);
            var controller = _root.AddComponent<LastBearingGameController>();
            controller.Initialize();
            controller.StartNewGame(ColonyComposition.Mixed);
            return controller;
        }

        private static LastBearingFieldDesk RequireDesk(
            LastBearingGameController controller)
        {
            Assert.That(controller.FieldDesk, Is.Not.Null);
            Assert.That(controller.FieldDesk!.IsOperational, Is.True);
            return controller.FieldDesk;
        }

        private static UIDocument RequireDocument(
            LastBearingGameController controller)
        {
            UIDocument[] documents =
                controller.GetComponentsInChildren<UIDocument>(true);
            Assert.That(documents, Has.Length.EqualTo(1));
            return documents[0];
        }

        private static void Submit(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
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
        }

        private static void InvokeSimulationTick(
            LastBearingGameController controller)
        {
            MethodInfo? method = typeof(LastBearingGameController).GetMethod(
                "SimulateOneTick",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method!.Invoke(controller, null);
        }

        private static void AssertMode(
            LastBearingGameController controller,
            LastBearingPresentationMode expected)
        {
            Assert.That(controller.ModeCoordinator!.CurrentMode, Is.EqualTo(expected));
        }
    }
}
