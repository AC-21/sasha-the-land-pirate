#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    /// <summary>
    /// Input-only adapter around the existing Road Feel rig. It accepts the
    /// same bounded integers sent to DriveVehicleCommand and intentionally
    /// exposes no Rigidbody or telemetry outcome to the canonical core. The
    /// lab's service brake and reverse controls remain local and unchanged.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingRoadFeelModeAdapter : MonoBehaviour,
        ILastBearingRoadModeAdapter
    {
        private RoadFeelVehicleController? _vehicle;

        public bool IsRoadModeActive { get; private set; }

        public int LastThrottleMilli { get; private set; }

        public int LastSteeringMilli { get; private set; }

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
            _vehicle.SetControlInput(new RoadFeelControlInput(
                throttleMilli / 1000f,
                brake: 0f,
                steering: steeringMilli / 1000f,
                handbrake: 0f));
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
            _vehicle?.SetControlInput(default);
        }
    }
}
