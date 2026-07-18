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

        public void Configure(RoadFeelVehicleController vehicle)
        {
            _vehicle = vehicle ?? throw new ArgumentNullException(nameof(vehicle));
        }

        public void SetRoadModeActive(bool active)
        {
            IsRoadModeActive = active;
            if (!active)
            {
                ResetPresentation();
            }
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

            _vehicle.SetControlInput(new RoadFeelControlInput(
                throttleMilli / 1000f,
                brake: 0f,
                steering: steeringMilli / 1000f,
                handbrake: 0f));
        }

        public void ResetPresentation()
        {
            LastThrottleMilli = 0;
            LastSteeringMilli = 0;
            _vehicle?.SetControlInput(default);
        }
    }
}
