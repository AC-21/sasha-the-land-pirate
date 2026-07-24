#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingEmergencyCisternExpansionSourceTests
    {
        [Test]
        public void PhysicalControlDelegatesWithoutOwningCanonicalRules()
        {
            string runtime = RuntimeSource(
                "LastBearingEmergencyCisternExpansionInteractor.cs");
            Assert.That(
                runtime,
                Does.Contain("_controller.InstallEmergencyCisternExpansion()"));
            Assert.That(runtime, Does.Not.Contain("new InstallCityImprovementCommand"));
            Assert.That(runtime, Does.Not.Contain(".Queue("));
            Assert.That(runtime, Does.Contain("ReferenceEquals("));
            Assert.That(runtime, Does.Contain("_controller.RuntimeReadModel"));
            Assert.That(
                runtime,
                Does.Contain("NextCityDecision.ExpandEmergencyCistern"));
            Assert.That(
                runtime,
                Does.Not.Contain("OpenPumpHall"));
        }

        [Test]
        public void ControllerUsesExactExpansionCommandAndDeskRoutesOnly()
        {
            string controller = RuntimeSource(
                "LastBearingGameController.cs");
            string presenter = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDeskPresenter.cs"));
            string desk = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDesk.cs"));

            Assert.That(
                controller,
                Does.Contain("NextCityDecision.ExpandEmergencyCistern,"));
            Assert.That(
                controller,
                Does.Contain(
                    "LastBearingState.EmergencyStorageExpansionSocketId"));
            Assert.That(
                controller,
                Does.Contain(
                    ".EmergencyStorageExpansionOrientationQuarterTurns"));
            Assert.That(
                presenter,
                Does.Contain(
                    "OPEN EMERGENCY STORAGE · EXPAND CISTERN"));
            Assert.That(
                desk,
                Does.Contain(
                    "_controller.OpenEmergencyCisternExpansion()"));
            Assert.That(
                desk,
                Does.Not.Contain(
                    "OpenEmergencyCisternExpansion(); _controller.Install"));
        }

        [Test]
        public void DryLineAndWorldWaterUseEffectiveReadModelCapacity()
        {
            string interactor = RuntimeSource(
                "LastBearingCityServiceCellInteractor.cs");
            string presenter = RuntimeSource(
                Path.Combine("UI", "LastBearingFieldDeskPresenter.cs"));
            string controller = RuntimeSource(
                "LastBearingGameController.cs");

            Assert.That(
                interactor,
                Does.Contain("long capacity = Math.Max(1, model.WaterCapacityMilli)"));
            Assert.That(
                presenter,
                Does.Contain("long capacity = Math.Max(1, model.WaterCapacityMilli)"));
            Assert.That(
                presenter,
                Does.Contain("model.WaterCapacityMilli -"));
            Assert.That(
                presenter,
                Does.Contain("model.EmergencyCisternWaterMilli"));
            Assert.That(
                controller,
                Does.Contain("(float)_readModel.WaterMilli /"));
            Assert.That(
                controller,
                Does.Contain("_readModel.WaterCapacityMilli"));
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
