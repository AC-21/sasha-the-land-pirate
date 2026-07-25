#nullable enable

using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using NUnit.Framework;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingRoadTractionEvidenceTests
    {
        [TestCase(
            RoadFeelRouteProfile.WinchLine,
            RoadFeelSurfaceKind.Concrete)]
        [TestCase(
            RoadFeelRouteProfile.WinchLine,
            RoadFeelSurfaceKind.Washboard)]
        [TestCase(
            RoadFeelRouteProfile.RangeLine,
            RoadFeelSurfaceKind.Concrete)]
        [TestCase(
            RoadFeelRouteProfile.RangeLine,
            RoadFeelSurfaceKind.Sand)]
        [TestCase(
            RoadFeelRouteProfile.RangeLine,
            RoadFeelSurfaceKind.Gravel)]
        public void RouteCorrectTractionCanAdvance(
            RoadFeelRouteProfile route,
            RoadFeelSurfaceKind surface)
        {
            RoadFeelTractionEvidence evidence = Evaluate(
                route: route,
                surface: surface,
                forwardSpeed:
                    RoadFeelTractionQuantizer
                        .MinimumForwardSpeedMetresPerSecond,
                groundedContacts:
                    RoadFeelTractionQuantizer.MinimumGroundedContacts);

            Assert.That(evidence.CanAdvance, Is.True);
            Assert.That(
                evidence.Reason,
                Is.EqualTo(RoadFeelTractionEvidenceReason.Ready));
        }

        [TestCase(
            RoadFeelRouteProfile.WinchLine,
            RoadFeelSurfaceKind.Sand)]
        [TestCase(
            RoadFeelRouteProfile.WinchLine,
            RoadFeelSurfaceKind.Gravel)]
        [TestCase(
            RoadFeelRouteProfile.RangeLine,
            RoadFeelSurfaceKind.Washboard)]
        [TestCase(
            RoadFeelRouteProfile.WinchLine,
            RoadFeelSurfaceKind.Hardpack)]
        [TestCase(
            RoadFeelRouteProfile.RangeLine,
            RoadFeelSurfaceKind.Hardpack)]
        [TestCase(
            RoadFeelRouteProfile.None,
            RoadFeelSurfaceKind.Concrete)]
        public void WrongRouteAndHardpackFailClosed(
            RoadFeelRouteProfile route,
            RoadFeelSurfaceKind surface)
        {
            RoadFeelTractionEvidence evidence = Evaluate(
                route: route,
                surface: surface);

            AssertRejected(
                evidence,
                RoadFeelTractionEvidenceReason.WrongSurface);
        }

        [Test]
        public void UnknownSurfaceFailsClosed()
        {
            RoadFeelTractionEvidence evidence = Evaluate(
                surface: (RoadFeelSurfaceKind)99);

            AssertRejected(
                evidence,
                RoadFeelTractionEvidenceReason.WrongSurface);
        }

        [Test]
        public void InactiveAdapterWinsOverEveryOtherFailure()
        {
            RoadFeelTractionEvidence evidence = Evaluate(
                adapterActive: false,
                adapterHealthy: false,
                recovering: true,
                groundedContacts: 0,
                forwardSpeed: -10f,
                surface: RoadFeelSurfaceKind.Hardpack);

            AssertRejected(
                evidence,
                RoadFeelTractionEvidenceReason.AdapterInactive);
        }

        [Test]
        public void FaultedAdapterWinsBeforeTelemetryFailures()
        {
            RoadFeelTractionEvidence evidence = Evaluate(
                adapterHealthy: false,
                recovering: true,
                groundedContacts: 0,
                forwardSpeed: -10f,
                surface: RoadFeelSurfaceKind.Hardpack);

            AssertRejected(
                evidence,
                RoadFeelTractionEvidenceReason.AdapterFaulted);
        }

        [Test]
        public void RecoveryFailsBeforeContactAndMotionEvidence()
        {
            RoadFeelTractionEvidence evidence = Evaluate(
                recovering: true,
                groundedContacts: 0,
                forwardSpeed: -10f,
                surface: RoadFeelSurfaceKind.Hardpack);

            AssertRejected(
                evidence,
                RoadFeelTractionEvidenceReason.Recovering);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void FewerThanTwoContactsFailClosed(int groundedContacts)
        {
            RoadFeelTractionEvidence evidence = Evaluate(
                groundedContacts: groundedContacts);

            AssertRejected(
                evidence,
                RoadFeelTractionEvidenceReason.InsufficientGroundContacts);
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(0.7499f)]
        public void ReverseAndStationaryMotionFailClosed(float forwardSpeed)
        {
            RoadFeelTractionEvidence evidence = Evaluate(
                forwardSpeed: forwardSpeed);

            AssertRejected(
                evidence,
                RoadFeelTractionEvidenceReason.NotMovingForward);
        }

        [Test]
        public void NonFiniteForwardMotionFailsClosed()
        {
            AssertRejected(
                Evaluate(forwardSpeed: float.NaN),
                RoadFeelTractionEvidenceReason.NotMovingForward);
            AssertRejected(
                Evaluate(forwardSpeed: float.PositiveInfinity),
                RoadFeelTractionEvidenceReason.NotMovingForward);
            AssertRejected(
                Evaluate(forwardSpeed: float.NegativeInfinity),
                RoadFeelTractionEvidenceReason.NotMovingForward);
        }

        [Test]
        public void DefaultEvidenceCannotAdvance()
        {
            RoadFeelTractionEvidence evidence = default;

            AssertRejected(
                evidence,
                RoadFeelTractionEvidenceReason.AdapterInactive);
        }

        private static RoadFeelTractionEvidence Evaluate(
            bool adapterActive = true,
            bool adapterHealthy = true,
            RoadFeelRouteProfile route = RoadFeelRouteProfile.WinchLine,
            bool recovering = false,
            int groundedContacts = 4,
            float forwardSpeed = 5f,
            RoadFeelSurfaceKind surface =
                RoadFeelSurfaceKind.Washboard)
        {
            return RoadFeelTractionQuantizer.Evaluate(
                adapterActive,
                adapterHealthy,
                route,
                new RoadFeelTelemetry(
                    speedMetresPerSecond: 5f,
                    forwardSpeedMetresPerSecond: forwardSpeed,
                    yawRateDegreesPerSecond: 0f,
                    bodySlipDegrees: 0f,
                    steeringAngleDegrees: 0f,
                    groundedContacts: groundedContacts,
                    averageCompression: 0.5f,
                    dominantSurface: surface,
                    cargoMassKilograms: 0f,
                    damageBand: RoadFeelDamageBand.Healthy,
                    recovering: recovering));
        }

        private static void AssertRejected(
            RoadFeelTractionEvidence evidence,
            RoadFeelTractionEvidenceReason expectedReason)
        {
            Assert.That(evidence.CanAdvance, Is.False);
            Assert.That(evidence.Reason, Is.EqualTo(expectedReason));
        }
    }
}
