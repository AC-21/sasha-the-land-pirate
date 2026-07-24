#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingEmergencyAidSourceTests
    {
        [Test]
        public void PhysicalTenderDelegatesWithoutOwningAidTerms()
        {
            string interactor = RuntimeSource(
                "LastBearingEmergencyAidInteractor.cs");

            Assert.That(
                interactor,
                Does.Contain("_controller.ReceiveEmergencyAid()"));
            Assert.That(
                interactor,
                Does.Not.Contain("new ReceiveEmergencyAidCommand"));
            Assert.That(interactor, Does.Not.Contain(".Queue("));
            Assert.That(interactor, Does.Contain("ReferenceEquals("));
            Assert.That(
                interactor,
                Does.Contain("_controller.RuntimeReadModel"));
            Assert.That(
                interactor,
                Does.Contain("IsEmergencyAidReceptionAvailable"));
            Assert.That(
                interactor,
                Does.Contain("IsEmergencyAidReceptionComplete"));
            Assert.That(
                interactor,
                Does.Contain("model.EmergencyAidWaterMilli"));
            Assert.That(
                interactor,
                Does.Contain("model.WaterCapacityMilli"));
        }

        [Test]
        public void DeskRoutesControllerConstructsAndAutosavesExistingEvent()
        {
            string controller = RuntimeSource(
                "LastBearingGameController.cs");
            string presenter = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDeskPresenter.cs"));
            string desk = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDesk.cs"));

            Assert.That(
                controller,
                Does.Contain("new ReceiveEmergencyAidCommand(sequence)"));
            Assert.That(
                controller,
                Does.Contain(
                    "kind == LastBearingEventKind.EmergencyAidDelivered"));
            Assert.That(
                presenter,
                Does.Contain(
                    "OPEN EMERGENCY STORAGE · RECEIVE WATER TENDER"));
            Assert.That(
                desk,
                Does.Contain(
                    "_controller.OpenEmergencyAidWaterTender()"));
            Assert.That(
                desk,
                Does.Not.Contain(
                    "_controller.ReceiveEmergencyAid()"));
        }

        [Test]
        public void ControllerAndCameraGiveFocusedTenderThePrimaryInput()
        {
            string controller = RuntimeSource(
                "LastBearingGameController.cs");
            string cameraRig = RuntimeSource(
                "LastBearingCameraRig.cs");
            string serviceCell = RuntimeSource(
                "LastBearingCityServiceCellInteractor.cs");
            string expansion = RuntimeSource(
                "LastBearingEmergencyCisternExpansionInteractor.cs");

            Assert.That(
                controller,
                Does.Contain(
                    "_world?.EmergencyAidInteractor"));
            Assert.That(
                controller,
                Does.Contain(
                    "?.IsControlFocused == true)"));
            Assert.That(
                cameraRig,
                Does.Contain("_emergencyAidInteractor"));
            Assert.That(
                cameraRig,
                Does.Contain(
                    ".IsControlFocused != true)"));
            Assert.That(
                serviceCell,
                Does.Contain(
                    "_controller.IsEmergencyAidReceptionFocused != true"));
            Assert.That(
                expansion,
                Does.Contain(
                    "_controller.IsEmergencyAidReceptionFocused != true"));
        }

        [Test]
        public void TenderUsesExistingCityCameraModeAndPrimitiveGrammar()
        {
            string interactor = RuntimeSource(
                "LastBearingEmergencyAidInteractor.cs");

            Assert.That(
                interactor,
                Does.Contain("_controller?.IsExactFieldDeskCityOverview"));
            Assert.That(
                interactor,
                Does.Contain("GameObject.CreatePrimitive"));
            Assert.That(interactor, Does.Not.Contain("new Camera"));
            Assert.That(interactor, Does.Not.Contain("AudioSource"));
            Assert.That(interactor, Does.Not.Contain("SceneManager"));
        }

        private static string RuntimeSource(string relativePath)
        {
            return File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "AtomicLandPirate",
                    "LastBearing",
                    "Runtime",
                    relativePath));
        }
    }
}
