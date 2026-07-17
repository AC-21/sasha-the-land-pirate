#nullable enable

using System;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using NUnit.Framework;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class RoadFeelMathTests
    {
        [Test]
        public void SpringRateSupportsDeclaredMassAtDesiredSag()
        {
            const float supportedMass = 850f;
            const float desiredSag = 0.14f;

            float springRate = RoadFeelMath.SpringRateFromSag(
                supportedMass,
                desiredSag);

            Assert.That(
                springRate * desiredSag,
                Is.EqualTo(supportedMass * Physics.gravity.magnitude)
                    .Within(0.01f));
        }

        [Test]
        public void DamperMatchesCriticalDampingRatio()
        {
            const float springRate = 59559f;
            const float supportedMass = 850f;
            const float ratio = 0.82f;

            float damper = RoadFeelMath.DamperFromRatio(
                springRate,
                supportedMass,
                ratio);

            float expected = ratio * 2f *
                             Mathf.Sqrt(springRate * supportedMass);
            Assert.That(damper, Is.EqualTo(expected).Within(0.01f));
        }

        [TestCase(0f, 32f)]
        [TestCase(100f, 10f)]
        [TestCase(-100f, 10f)]
        public void SteeringStaysInsideSpeedEnvelope(
            float speedMetresPerSecond,
            float expectedDegrees)
        {
            float angle = RoadFeelMath.SpeedSensitiveSteerAngle(
                speedMetresPerSecond,
                100f,
                32f,
                10f);

            Assert.That(angle, Is.EqualTo(expectedDegrees).Within(0.001f));
        }

        [Test]
        public void BodySlipPreservesDirection()
        {
            float rightSlip = RoadFeelMath.SignedBodySlipDegrees(
                new Vector3(1f, 0f, 4f),
                Vector3.forward,
                Vector3.up);
            float leftSlip = RoadFeelMath.SignedBodySlipDegrees(
                new Vector3(-1f, 0f, 4f),
                Vector3.forward,
                Vector3.up);

            Assert.That(rightSlip, Is.GreaterThan(0f));
            Assert.That(leftSlip, Is.LessThan(0f));
            Assert.That(Mathf.Abs(rightSlip), Is.EqualTo(Mathf.Abs(leftSlip))
                .Within(0.001f));
        }

        [Test]
        public void ControlInputClampsEveryAxis()
        {
            var input = new RoadFeelControlInput(
                throttle: 2f,
                brake: -1f,
                steering: 3f,
                handbrake: 4f);

            Assert.That(input.Throttle, Is.EqualTo(1f));
            Assert.That(input.Brake, Is.EqualTo(0f));
            Assert.That(input.Steering, Is.EqualTo(1f));
            Assert.That(input.Handbrake, Is.EqualTo(1f));
        }

        [TestCase(0f, 1f, 0f, true, true)]
        [TestCase(0f, 1f, -4f, true, true)]
        [TestCase(0f, 1f, 2f, true, false)]
        [TestCase(1f, 1f, 0f, true, false)]
        [TestCase(0f, 0f, 0f, true, false)]
        [TestCase(0f, 1f, 0f, false, false)]
        public void BrakeBecomesReverseOnlyAfterLowSpeedArming(
            float throttle,
            float brake,
            float forwardSpeed,
            bool reverseArmed,
            bool expected)
        {
            bool actual = RoadFeelMath.ShouldApplyReverse(
                throttle,
                brake,
                forwardSpeed,
                0.75f,
                reverseArmed);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(1f, 0f, false, 0.46f, 1f)]
        [TestCase(1f, 1f, false, 0.46f, 0f)]
        [TestCase(0f, 1f, true, 0.46f, -0.46f)]
        public void ServiceBrakeHasPriorityOverForwardDrive(
            float throttle,
            float brake,
            bool reverseRequested,
            float reverseMultiplier,
            float expected)
        {
            float actual = RoadFeelMath.ResolveDriveInput(
                throttle,
                brake,
                reverseRequested,
                reverseMultiplier);

            Assert.That(actual, Is.EqualTo(expected).Within(0.001f));
        }

        [TestCase(0.01f, 0f, -500f)]
        [TestCase(-0.01f, 0f, 500f)]
        [TestCase(0f, -1.5f, 1_500f)]
        public void BrakeHoldHasNoLowSpeedDeadZoneAndCountersSlope(
            float speed,
            float accelerationAlongForward,
            float expectedForce)
        {
            float force = RoadFeelMath.StoppingForce(
                speed,
                accelerationAlongForward,
                supportedMassKilograms: 1_000f,
                fixedDeltaTimeSeconds: 0.02f,
                maximumForceNewtons: 5_000f);

            Assert.That(force, Is.EqualTo(expectedForce).Within(0.01f));
        }

        [Test]
        public void FixedStepContractIsFiftyHertzWithoutRejectingAlternatives()
        {
            Assert.That(
                RoadFeelMath.ExpectedFixedDeltaTimeSeconds,
                Is.EqualTo(0.02f).Within(0.000001f));
            Assert.That(
                RoadFeelMath.IsExpectedFixedDeltaTime(Time.fixedDeltaTime),
                Is.True,
                "Road Feel Lab expects the checked-in 50 Hz project step.");
            Assert.That(
                RoadFeelMath.IsExpectedFixedDeltaTime(1f / 60f),
                Is.False);

            float alternateStepForce = RoadFeelMath.StoppingForce(
                longitudinalSpeedMetresPerSecond: 0.01f,
                accelerationAlongForwardMetresPerSecondSquared: 0f,
                supportedMassKilograms: 1_000f,
                fixedDeltaTimeSeconds: 1f / 60f,
                maximumForceNewtons: 5_000f);
            Assert.That(alternateStepForce, Is.EqualTo(-600f).Within(0.01f));
        }

        [TestCase(0f, 0.1f)]
        [TestCase(1f, 0f)]
        public void InvalidSpringInputsFailClosed(
            float mass,
            float sag)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RoadFeelMath.SpringRateFromSag(mass, sag));
        }
    }
}
