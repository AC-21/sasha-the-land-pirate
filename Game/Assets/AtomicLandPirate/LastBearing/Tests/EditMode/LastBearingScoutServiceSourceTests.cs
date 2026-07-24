#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingScoutServiceSourceTests
    {
        [Test]
        public void PendantDelegatesOneCommandWithoutOwningServiceTerms()
        {
            string interactor = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "LastBearingScoutServiceInteractor.cs"));

            Assert.That(
                interactor,
                Does.Contain("_controller.ServiceScout()"));
            Assert.That(
                interactor,
                Does.Not.Contain("new ServiceScoutCommand"));
            Assert.That(interactor, Does.Not.Contain(".Queue("));
            Assert.That(interactor, Does.Contain("ReferenceEquals("));
            Assert.That(
                interactor,
                Does.Contain("_controller.RuntimeReadModel"));
            Assert.That(
                Count(
                    interactor,
                    "_controller.ServiceScout()"),
                Is.EqualTo(1));
        }

        [Test]
        public void DeskRoutesOnlyAndControllerOwnsExactPairedAutosave()
        {
            string controller = RuntimeSource(
                "LastBearingGameController.cs");
            string presenter = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDeskPresenter.cs"));
            string desk = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDesk.cs"));

            Assert.That(
                controller,
                Does.Contain("new ServiceScoutCommand(sequence)"));
            Assert.That(
                Count(
                    controller,
                    "new ServiceScoutCommand(sequence)"),
                Is.EqualTo(1));
            Assert.That(
                controller,
                Does.Contain("ContainsScoutServiceEventPair(domainEvents)"));
            Assert.That(
                controller,
                Does.Contain("\"settlement:last-bearing:parts\""));
            Assert.That(
                controller,
                Does.Contain("\"vehicle:sasha:service-cell\""));
            Assert.That(
                presenter,
                Does.Contain("OpenScoutServiceBay = 35"));
            Assert.That(
                presenter,
                Does.Contain("OPEN GARAGE · SERVICE SASHA'S SCOUT"));
            Assert.That(
                desk,
                Does.Contain("_controller.OpenScoutServiceBay()"));
            Assert.That(
                desk,
                Does.Not.Contain("_controller.ServiceScout()"));
        }

        [Test]
        public void ReturnPriorityPrecedesServiceAndCostsComeFromReadModel()
        {
            string presenter = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDeskPresenter.cs"));
            int fuelBond = presenter.IndexOf(
                "if (model.IsDepotAccessRestorationAvailable)",
                StringComparison.Ordinal);
            int service = presenter.IndexOf(
                "if (model.IsVehicleServiceAvailable)",
                fuelBond,
                StringComparison.Ordinal);
            int workshop = presenter.IndexOf(
                "if (IsWorkshopRelevant(model))",
                service,
                StringComparison.Ordinal);

            Assert.That(fuelBond, Is.GreaterThanOrEqualTo(0));
            Assert.That(service, Is.GreaterThan(fuelBond));
            Assert.That(workshop, Is.GreaterThan(service));
            Assert.That(
                presenter,
                Does.Contain("model.VehicleConditionMilli"));
            Assert.That(
                presenter,
                Does.Contain("model.VehicleServicePartsCostUnits"));
            Assert.That(
                presenter,
                Does.Contain("model.VehicleServiceReservePartsUnits"));
            Assert.That(
                presenter,
                Does.Contain("!model.IsVehicleServiceAvailable"));
            Assert.That(
                presenter,
                Does.Not.Contain("ServiceScoutCommand"));
        }

        [Test]
        public void GarageReusesCameraHoistWorklightAndScoutTelltale()
        {
            string interactor = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "LastBearingScoutServiceInteractor.cs"));
            string garage = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "LastBearingGarageBayView.cs"));
            string world = RuntimeSource(
                "LastBearingWorldBuilder.cs");

            Assert.That(
                garage,
                Does.Contain("\"SERVICE_HOIST\""));
            Assert.That(
                garage,
                Does.Contain("\"HOIST_CABLE\""));
            Assert.That(
                garage,
                Does.Contain("_moduleWorkLight"));
            Assert.That(
                world,
                Does.Contain("\"SCOUT_CONDITION_TELL_TALE\""));
            Assert.That(interactor, Does.Contain("sharedCamera"));
            Assert.That(interactor, Does.Contain("GameObject.CreatePrimitive"));
            Assert.That(interactor, Does.Not.Contain("new Camera"));
            Assert.That(interactor, Does.Not.Contain("AudioSource"));
            Assert.That(interactor, Does.Not.Contain("SceneManager"));
        }

        private static int Count(string source, string needle)
        {
            int count = 0;
            int offset = 0;
            while ((offset = source.IndexOf(
                       needle,
                       offset,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += needle.Length;
            }

            return count;
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
