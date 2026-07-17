#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    public enum RoadFeelSurfaceKind
    {
        Concrete,
        Hardpack,
        Gravel,
        Sand,
        Washboard
    }

    public enum RoadFeelDamageBand
    {
        Healthy,
        Worn,
        Critical
    }

    public readonly struct RoadFeelControlInput
    {
        public RoadFeelControlInput(
            float throttle,
            float brake,
            float steering,
            float handbrake)
        {
            Throttle = Mathf.Clamp01(throttle);
            Brake = Mathf.Clamp01(brake);
            Steering = Mathf.Clamp(steering, -1f, 1f);
            Handbrake = Mathf.Clamp01(handbrake);
        }

        public float Throttle { get; }
        public float Brake { get; }
        public float Steering { get; }
        public float Handbrake { get; }
    }

    public readonly struct RoadFeelTelemetry
    {
        public RoadFeelTelemetry(
            float speedMetresPerSecond,
            float forwardSpeedMetresPerSecond,
            float yawRateDegreesPerSecond,
            float bodySlipDegrees,
            float steeringAngleDegrees,
            int groundedContacts,
            float averageCompression,
            RoadFeelSurfaceKind dominantSurface,
            float cargoMassKilograms,
            RoadFeelDamageBand damageBand,
            bool recovering)
        {
            SpeedMetresPerSecond = speedMetresPerSecond;
            ForwardSpeedMetresPerSecond = forwardSpeedMetresPerSecond;
            YawRateDegreesPerSecond = yawRateDegreesPerSecond;
            BodySlipDegrees = bodySlipDegrees;
            SteeringAngleDegrees = steeringAngleDegrees;
            GroundedContacts = groundedContacts;
            AverageCompression = averageCompression;
            DominantSurface = dominantSurface;
            CargoMassKilograms = cargoMassKilograms;
            DamageBand = damageBand;
            Recovering = recovering;
        }

        public float SpeedMetresPerSecond { get; }
        public float SpeedKilometresPerHour => SpeedMetresPerSecond * 3.6f;
        public float ForwardSpeedMetresPerSecond { get; }
        public float YawRateDegreesPerSecond { get; }
        public float BodySlipDegrees { get; }
        public float SteeringAngleDegrees { get; }
        public int GroundedContacts { get; }
        public float AverageCompression { get; }
        public RoadFeelSurfaceKind DominantSurface { get; }
        public float CargoMassKilograms { get; }
        public RoadFeelDamageBand DamageBand { get; }
        public bool Recovering { get; }
    }

    public static class RoadFeelMath
    {
        public const float ExpectedFixedDeltaTimeSeconds = 1f / 50f;

        public static float SpringRateFromSag(
            float supportedMassKilograms,
            float desiredSagMetres)
        {
            if (supportedMassKilograms <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(supportedMassKilograms));
            }

            if (desiredSagMetres <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(desiredSagMetres));
            }

            return supportedMassKilograms * Physics.gravity.magnitude /
                   desiredSagMetres;
        }

        public static float DamperFromRatio(
            float springRate,
            float supportedMassKilograms,
            float dampingRatio)
        {
            if (springRate <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(springRate));
            }

            if (supportedMassKilograms <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(supportedMassKilograms));
            }

            if (dampingRatio <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dampingRatio));
            }

            return dampingRatio * 2f *
                   Mathf.Sqrt(springRate * supportedMassKilograms);
        }

        public static float SpeedSensitiveSteerAngle(
            float speedMetresPerSecond,
            float maximumSpeedMetresPerSecond,
            float lowSpeedDegrees,
            float highSpeedDegrees)
        {
            if (maximumSpeedMetresPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSpeedMetresPerSecond));
            }

            float normalized = Mathf.Clamp01(
                Mathf.Abs(speedMetresPerSecond) /
                maximumSpeedMetresPerSecond);
            float shaped = normalized * normalized * (3f - 2f * normalized);
            return Mathf.Lerp(lowSpeedDegrees, highSpeedDegrees, shaped);
        }

        public static float SignedBodySlipDegrees(
            Vector3 velocity,
            Vector3 forward,
            Vector3 up)
        {
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
            Vector3 planarForward = Vector3.ProjectOnPlane(forward, up);
            if (planarVelocity.sqrMagnitude < 0.0001f ||
                planarForward.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(
                planarForward,
                planarVelocity,
                up);
        }

        public static bool ShouldApplyReverse(
            float throttle,
            float brake,
            float forwardSpeedMetresPerSecond,
            float transitionSpeedMetresPerSecond,
            bool reverseArmed)
        {
            if (transitionSpeedMetresPerSecond < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(transitionSpeedMetresPerSecond));
            }

            return reverseArmed &&
                   throttle <= 0.05f &&
                   brake > 0.01f &&
                   forwardSpeedMetresPerSecond <=
                   transitionSpeedMetresPerSecond;
        }

        public static float ResolveDriveInput(
            float throttle,
            float brake,
            bool reverseRequested,
            float reverseMultiplier)
        {
            if (reverseRequested)
            {
                return -Mathf.Clamp01(brake) *
                       Mathf.Clamp01(reverseMultiplier);
            }

            return brake > 0.01f ? 0f : Mathf.Clamp01(throttle);
        }

        public static float StoppingForce(
            float longitudinalSpeedMetresPerSecond,
            float accelerationAlongForwardMetresPerSecondSquared,
            float supportedMassKilograms,
            float fixedDeltaTimeSeconds,
            float maximumForceNewtons)
        {
            if (supportedMassKilograms <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(supportedMassKilograms));
            }

            if (fixedDeltaTimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixedDeltaTimeSeconds));
            }

            if (maximumForceNewtons < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumForceNewtons));
            }

            float force = -(
                longitudinalSpeedMetresPerSecond /
                fixedDeltaTimeSeconds +
                accelerationAlongForwardMetresPerSecondSquared) *
                supportedMassKilograms;
            return Mathf.Clamp(
                force,
                -maximumForceNewtons,
                maximumForceNewtons);
        }

        public static bool IsExpectedFixedDeltaTime(
            float fixedDeltaTimeSeconds)
        {
            return fixedDeltaTimeSeconds > 0f &&
                   Mathf.Abs(
                       fixedDeltaTimeSeconds -
                       ExpectedFixedDeltaTimeSeconds) <= 0.00001f;
        }
    }
}
