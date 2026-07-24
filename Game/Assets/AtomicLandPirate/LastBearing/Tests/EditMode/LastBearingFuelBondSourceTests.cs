#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingFuelBondSourceTests
    {
        [Test]
        public void PhysicalControlDelegatesWithoutOwningCanonicalTerms()
        {
            string interactor = RuntimeSource(
                "LastBearingFuelBondInteractor.cs");

            Assert.That(
                interactor,
                Does.Contain("_controller.PostFuelBond()"));
            Assert.That(
                interactor,
                Does.Not.Contain("new RestoreDepotAccessCommand"));
            Assert.That(interactor, Does.Not.Contain(".Queue("));
            Assert.That(interactor, Does.Contain("ReferenceEquals("));
            Assert.That(
                interactor,
                Does.Contain("_controller.RuntimeReadModel"));
            Assert.That(
                interactor,
                Does.Contain("IsDepotAccessRestorationAvailable"));
            Assert.That(
                interactor,
                Does.Contain("FuelCanCount = 5"));
        }

        [Test]
        public void DeskRoutesAndControllerAloneConstructsTheCommand()
        {
            string controller = RuntimeSource(
                "LastBearingGameController.cs");
            string presenter = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDeskPresenter.cs"));
            string desk = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDesk.cs"));

            Assert.That(
                controller,
                Does.Contain(
                    "new RestoreDepotAccessCommand(sequence)"));
            Assert.That(
                presenter,
                Does.Contain(
                    "OPEN CLAIMS WICKET · POST FUEL BOND"));
            Assert.That(
                desk,
                Does.Contain(
                    "_controller.OpenFuelBondClaimsWicket()"));
            Assert.That(
                desk,
                Does.Not.Contain(
                    "_controller.PostFuelBond()"));
        }

        [Test]
        public void ExistingPermitEventRemainsTheOnlyAutosaveWitness()
        {
            string controller = RuntimeSource(
                "LastBearingGameController.cs");

            Assert.That(
                controller,
                Does.Contain(
                    "LastBearingEventKind.RoutePermitGranted"));
            Assert.That(
                controller,
                Does.Contain(
                    "_readModel.IsDepotAccessRestorationAvailable &&"));
            Assert.That(
                controller,
                Does.Not.Contain("FuelBondPosted"));
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
