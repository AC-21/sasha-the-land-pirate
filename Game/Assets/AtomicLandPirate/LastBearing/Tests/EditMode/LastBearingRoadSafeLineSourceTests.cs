#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingRoadSafeLineSourceTests
    {
        [Test]
        public void SafeLineStaysPresentationOnlyAndAllocationFreePerApply()
        {
            string view = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AtomicLandPirate",
                "LastBearing",
                "Runtime",
                "RoadFeel",
                "LastBearingRoadSafeLineView.cs"));
            string controller = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AtomicLandPirate",
                "LastBearing",
                "Runtime",
                "LastBearingGameController.cs"));
            string permitJob = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "AtomicLandPirate",
                "LastBearing",
                "Runtime",
                "LastBearingPermitJobPresenter.cs"));

            Assert.That(view, Does.Contain("model.VehicleLateralMilli"));
            Assert.That(
                view,
                Does.Contain(
                    "LastBearingBalanceV1.RoadSafeHalfWidthMilli"));
            Assert.That(view, Does.Contain("sharedMaterial = material"));
            Assert.That(view, Does.Not.Contain("void Update("));
            Assert.That(view, Does.Not.Contain("void FixedUpdate("));
            Assert.That(view, Does.Not.Contain("CreatePrimitive("));
            Assert.That(view, Does.Not.Contain("AddComponent<Collider>"));
            Assert.That(view, Does.Not.Contain("AddComponent<Rigidbody>"));
            Assert.That(view, Does.Not.Contain("AddComponent<Camera>"));
            Assert.That(view, Does.Not.Contain("AddComponent<AudioListener>"));
            Assert.That(view, Does.Not.Contain("AddComponent<Light>"));
            Assert.That(view, Does.Not.Contain("AddComponent<RoadFeelSurface>"));
            Assert.That(view, Does.Not.Contain("new LastBearingCommand"));
            Assert.That(controller, Does.Contain("_world.ApplyRoadSafeLine("));
            Assert.That(
                permitJob,
                Does.Contain("\"Hold the bone line\""));
        }
    }
}
