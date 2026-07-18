#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    /// <summary>
    /// Input-only adapter around the existing Road Feel rig. It accepts the
    /// same bounded integers sent to DriveVehicleCommand and intentionally
    /// exposes no Rigidbody or telemetry outcome to the canonical core. The
    /// Service brake, reverse arming, handbrake, and derived cargo/damage load
    /// remain presentation-only and cannot author canonical route progress.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingRoadFeelModeAdapter : MonoBehaviour,
        ILastBearingRoadModeAdapter
    {
        private RoadFeelVehicleController? _vehicle;

        public bool IsRoadModeActive { get; private set; }

        public int LastThrottleMilli { get; private set; }

        public int LastSteeringMilli { get; private set; }

        public int LastBrakeMilli { get; private set; }

        public int LastHandbrakeMilli { get; private set; }

        public int LastCargoMassKilograms { get; private set; }

        public LastBearingRoadDamageBand LastDamageBand { get; private set; }

        public int CommandReceiptCount { get; private set; }

        public bool IsPhysicsSuspended { get; private set; }

        public RoadFeelVehicleController? Vehicle => _vehicle;

        public void Configure(RoadFeelVehicleController vehicle)
        {
            _vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
            SetRoadModeActive(false);
        }

        public void SetRoadModeActive(bool active)
        {
            if (_vehicle == null)
            {
                IsRoadModeActive = false;
                IsPhysicsSuspended = true;
                return;
            }

            Rigidbody body = _vehicle.Body;
            if (!active)
            {
                ResetPresentation();
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.Sleep();
                    body.isKinematic = true;
                }

                _vehicle.enabled = false;
                IsRoadModeActive = false;
                IsPhysicsSuspended = true;
                return;
            }

            body.isKinematic = false;
            _vehicle.enabled = true;
            body.WakeUp();
            IsRoadModeActive = active;
            IsPhysicsSuspended = false;
        }

        public void ApplyQuantizedCommandShadow(
            int throttleMilli,
            int steeringMilli)
        {
            if (throttleMilli < 0 || throttleMilli > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(throttleMilli));
            }

            if (steeringMilli < -1000 || steeringMilli > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(steeringMilli));
            }

            LastThrottleMilli = throttleMilli;
            LastSteeringMilli = steeringMilli;
            if (!IsRoadModeActive || _vehicle == null)
            {
                return;
            }

            CommandReceiptCount++;
            ApplyPresentationControls();
        }

        public void ApplyPresentationOnlyControls(
            int brakeMilli,
            int handbrakeMilli)
        {
            if (brakeMilli < 0 || brakeMilli > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(brakeMilli));
            }

            if (handbrakeMilli < 0 || handbrakeMilli > 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(handbrakeMilli));
            }

            LastBrakeMilli = brakeMilli;
            LastHandbrakeMilli = handbrakeMilli;
            if (!IsRoadModeActive || _vehicle == null)
            {
                return;
            }

            ApplyPresentationControls();
        }

        public void ApplyDerivedPresentationLoad(
            int cargoMassKilograms,
            LastBearingRoadDamageBand damageBand)
        {
            if (cargoMassKilograms < 0 || cargoMassKilograms > 3000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cargoMassKilograms));
            }

            if (!Enum.IsDefined(typeof(LastBearingRoadDamageBand), damageBand))
            {
                throw new ArgumentOutOfRangeException(nameof(damageBand));
            }

            LastCargoMassKilograms = cargoMassKilograms;
            LastDamageBand = damageBand;
            _vehicle?.SetLoad(
                cargoMassKilograms,
                ToRoadFeelDamageBand(damageBand));
        }

        public void SynchronizePresentationPose(
            Vector3 position,
            Quaternion rotation)
        {
            if (_vehicle == null)
            {
                return;
            }

            _vehicle.ResetAt(position, rotation);
        }

        public void ResetPresentation()
        {
            LastThrottleMilli = 0;
            LastSteeringMilli = 0;
            LastBrakeMilli = 0;
            LastHandbrakeMilli = 0;
            _vehicle?.SetControlInput(default);
        }

        private void ApplyPresentationControls()
        {
            _vehicle?.SetControlInput(new RoadFeelControlInput(
                LastThrottleMilli / 1000f,
                LastBrakeMilli / 1000f,
                LastSteeringMilli / 1000f,
                LastHandbrakeMilli / 1000f));
        }

        private static RoadFeelDamageBand ToRoadFeelDamageBand(
            LastBearingRoadDamageBand damageBand)
        {
            switch (damageBand)
            {
                case LastBearingRoadDamageBand.Healthy:
                    return RoadFeelDamageBand.Healthy;
                case LastBearingRoadDamageBand.Worn:
                    return RoadFeelDamageBand.Worn;
                case LastBearingRoadDamageBand.Critical:
                    return RoadFeelDamageBand.Critical;
                default:
                    throw new ArgumentOutOfRangeException(nameof(damageBand));
            }
        }
    }
}
