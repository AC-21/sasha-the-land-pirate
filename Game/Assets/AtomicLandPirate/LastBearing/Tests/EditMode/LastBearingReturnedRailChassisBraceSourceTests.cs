#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingReturnedRailChassisBraceSourceTests
    {
        [Test]
        public void JigDelegatesOnceAndControllerAloneBuildsTheCommand()
        {
            string interactor = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "LastBearingReturnedRailChassisBraceInteractor.cs"));
            string controller = RuntimeSource(
                "LastBearingGameController.cs");
            string desk = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDesk.cs"));

            Assert.That(
                Count(
                    interactor,
                    "_controller.InstallReturnedRailChassisBrace()"),
                Is.EqualTo(1));
            Assert.That(
                interactor,
                Does.Not.Contain(
                    "new InstallReturnedRailChassisBraceCommand"));
            Assert.That(interactor, Does.Not.Contain(".Queue("));
            Assert.That(interactor, Does.Contain("ReferenceEquals("));
            Assert.That(
                interactor,
                Does.Contain("_controller.RuntimeReadModel"));
            Assert.That(
                Count(
                    controller,
                    "new InstallReturnedRailChassisBraceCommand(sequence)"),
                Is.EqualTo(1));
            Assert.That(
                desk,
                Does.Contain(
                    "_controller.OpenReturnedRailChassisBraceJig()"));
            Assert.That(
                desk,
                Does.Not.Contain(
                    "_controller.InstallReturnedRailChassisBrace()"));
        }

        [Test]
        public void DedicatedRouteIsStableAndSitsBetweenServiceAndRepeat()
        {
            string presenter = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDeskPresenter.cs"));
            int service = presenter.IndexOf(
                "if (model.IsVehicleServiceAvailable)",
                StringComparison.Ordinal);
            int brace = presenter.IndexOf(
                "if (model.IsReturnedRailChassisBraceInstallAvailable)",
                service,
                StringComparison.Ordinal);
            int repeat = presenter.IndexOf(
                "if (model.IsRepeatExpeditionAvailable)",
                brace,
                StringComparison.Ordinal);

            Assert.That(
                presenter,
                Does.Contain(
                    "OpenReturnedRailChassisBraceJig = 38"));
            Assert.That(service, Is.GreaterThanOrEqualTo(0));
            Assert.That(brace, Is.GreaterThan(service));
            Assert.That(repeat, Is.GreaterThan(brace));
            Assert.That(
                presenter,
                Does.Contain(
                    "model.ReturnedRailChassisBracePartsCostUnits"));
            Assert.That(
                presenter,
                Does.Contain(
                    "model.ReturnedRailChassisBraceProtectionMilli"));
            Assert.That(
                presenter,
                Does.Contain(
                    "model.ProjectedRoundTripConditionLossMilli"));
            Assert.That(
                presenter,
                Does.Not.Contain(
                    "InstallReturnedRailChassisBraceCommand"));
        }

        [Test]
        public void EventAutosavesAndEveryPlayerInputUsesTheExactJig()
        {
            string interactor = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "LastBearingReturnedRailChassisBraceInteractor.cs"));
            string controller = RuntimeSource(
                "LastBearingGameController.cs");

            Assert.That(
                controller,
                Does.Contain(
                    "LastBearingEventKind\n                            .ReturnedRailChassisBraceInstalled"));
            Assert.That(interactor, Does.Contain("keyboard?.eKey"));
            Assert.That(interactor, Does.Contain("gamepad?.buttonSouth"));
            Assert.That(
                interactor,
                Does.Contain("mouse?.leftButton.wasPressedThisFrame"));
            Assert.That(
                interactor,
                Does.Contain("Physics.RaycastNonAlloc("));
            Assert.That(
                interactor,
                Does.Contain("FieldDesk?.BlocksWorldPointer"));
            Assert.That(
                interactor,
                Does.Contain("Hud?.BlocksWorldPointer"));
            Assert.That(
                interactor,
                Does.Contain("if (!_inputArmed)"));
            Assert.That(interactor, Does.Not.Contain("new Camera"));
            Assert.That(interactor, Does.Not.Contain("SceneManager"));
            Assert.That(interactor, Does.Not.Contain("Rigidbody"));
        }

        [Test]
        public void PairedTrussRemainsIndependentAndPresentationOnly()
        {
            string semantics = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "SashaScoutSemanticContract.cs"));
            string factory = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "SashaScoutBlockoutFactory.cs"));
            string visual = RuntimeSource(
                Path.Combine(
                    "Vehicle",
                    "SashaScoutVisual.cs"));

            Assert.That(
                semantics,
                Does.Contain(
                    "SOCKET_UPGRADE_RETURNED_RAIL_CHASSIS_BRACE"));
            Assert.That(
                semantics,
                Does.Contain(
                    "UPGRADE_RETURNED_RAIL_CHASSIS_BRACE"));
            Assert.That(
                factory,
                Does.Contain("\"RETURNED_RAIL_SISTER_LEFT\""));
            Assert.That(
                factory,
                Does.Contain("\"RETURNED_RAIL_SISTER_RIGHT\""));
            Assert.That(
                factory,
                Does.Contain("\"RETURNED_RAIL_RISER_LEFT\""));
            Assert.That(
                factory,
                Does.Contain("\"RETURNED_RAIL_RISER_RIGHT\""));
            Assert.That(
                factory,
                Does.Contain(
                    "\"RETURNED_RAIL_BONE_WITNESS_BAND_RIGHT\""));
            Assert.That(
                factory,
                Does.Contain(
                    "\"RETURNED_RAIL_TUNGSTEN_SERVICE_MARKER_REAR\""));
            Assert.That(
                visual,
                Does.Contain("ApplyReturnedRailChassisBrace(bool installed)"));
            Assert.That(
                visual,
                Does.Contain("ApplyUpgrade(SashaScoutUpgradePresentation upgrade)"));
            Assert.That(
                visual,
                Does.Not.Contain(
                    "SashaScoutUpgradePresentation.ReturnedRail"));
            Assert.That(factory, Does.Not.Contain("AddComponent<Light>"));
            Assert.That(factory, Does.Not.Contain("AddComponent<Rigidbody>"));
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
