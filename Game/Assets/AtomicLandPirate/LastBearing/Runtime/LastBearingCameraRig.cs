#nullable enable

using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Presentation-only strategy/chase camera. None of its values enter the
    /// deterministic simulation or save payload.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingCameraRig : MonoBehaviour
    {
        public const float StrategyFieldOfView = 40f;

        public const string ComparisonCameraSetupId =
            "D0022-PROVISIONAL-LAST-BEARING-CAMERA-V1";

        private const float MinimumCityDistance = 18f;
        private const float MaximumCityDistance = 48f;
        public const float ComparisonPitch = 42f;
        public const float ComparisonYaw = 38f;
        public const float ComparisonDistance = 31f;
        private const float CityPanSpeed = 13f;

        private Vector3 _cityFocus = ComparisonFocus;
        private Transform? _roadTarget;
        private Transform? _inspectionCameraAnchor;
        private Transform? _inspectionFocusAnchor;
        private Camera? _camera;
        private RoadFeelChaseCamera? _roadChaseCamera;
        private float _cityYaw = ComparisonYaw;
        private float _cityDistance = ComparisonDistance;
        private bool _roadMode;
        private bool _roadChaseActive;
        private bool _comparisonMode;
        private bool _inspectionMode;

        public static Vector3 ComparisonFocus => new Vector3(2f, 0f, -1.5f);

        public bool IsRoadMode => _roadMode;

        public bool IsComparisonMode => _comparisonMode;

        public bool IsInspectionMode => _inspectionMode;

        public bool IsRoadChaseActive => HasLiveRoadChaseOwnership();

        public bool HasConfiguredRoadChase =>
            _roadChaseCamera?.IsConfigured == true;

        public Transform? RoadTarget => _roadTarget;

        public Transform? InspectionCameraAnchor => _inspectionCameraAnchor;

        public Transform? InspectionFocusAnchor => _inspectionFocusAnchor;

        public float CityDistance => _cityDistance;

        public Vector3 CityFocus => _cityFocus;

        public float CityYaw => _cityYaw;

        public void Configure(
            Transform roadTarget,
            RoadFeelChaseCamera? roadChaseCamera = null)
        {
            _camera = GetComponent<Camera>();
            _roadChaseCamera = roadChaseCamera;
            SetRoadTarget(roadTarget);
            if (_camera != null)
            {
                _camera.fieldOfView = StrategyFieldOfView;
            }

            _roadChaseCamera?.SetChaseActive(false);
        }

        public void SetRoadTarget(Transform roadTarget)
        {
            _roadTarget = roadTarget != null
                ? roadTarget
                : throw new System.ArgumentNullException(nameof(roadTarget));
            ApplyPose(immediate: true);
        }

        public void SetRoadMode(bool roadMode)
        {
            _roadMode = roadMode;
            ApplyPose(immediate: false);
        }

        public void SetRoadChaseActive(bool active)
        {
            if (active && HasLiveRoadChaseOwnership())
            {
                return;
            }

            if (!active &&
                !_roadChaseActive &&
                !IsRoadChaseComponentActive())
            {
                return;
            }

            if (active)
            {
                if (_roadChaseCamera == null ||
                    !_roadChaseCamera.IsConfigured)
                {
                    EndRoadChaseOwnership();
                    return;
                }

                try
                {
                    _roadChaseCamera.SetChaseActive(true);
                    _roadChaseActive = _roadChaseCamera.IsChaseActive;
                    if (!_roadChaseActive)
                    {
                        EndRoadChaseOwnership();
                    }
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning(
                        "LAST_BEARING_CHASE_CAMERA_DISABLED activation " +
                        exception.GetType().Name,
                        this);
                    EndRoadChaseOwnership();
                }

                return;
            }

            EndRoadChaseOwnership();
        }

        public void SetComparisonMode(bool comparisonMode)
        {
            _comparisonMode = comparisonMode;
            if (comparisonMode)
            {
                _cityFocus = ComparisonFocus;
                _cityYaw = ComparisonYaw;
                _cityDistance = ComparisonDistance;
            }

            ApplyPose(immediate: !Application.isPlaying);
        }

        public void SetInspectionPose(
            Transform cameraAnchor,
            Transform focusAnchor,
            bool active)
        {
            _inspectionCameraAnchor = cameraAnchor != null
                ? cameraAnchor
                : throw new System.ArgumentNullException(nameof(cameraAnchor));
            _inspectionFocusAnchor = focusAnchor != null
                ? focusAnchor
                : throw new System.ArgumentNullException(nameof(focusAnchor));
            _inspectionMode = active;
            ApplyPose(immediate: !Application.isPlaying);
        }

        private void Update()
        {
            FailClosedIfRoadChaseOwnershipWasLost();
            if (_roadChaseActive || _roadMode || _comparisonMode || _inspectionMode)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var deltaTime = Time.unscaledDeltaTime;
            var planar = Vector2.zero;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    planar.y += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    planar.y -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    planar.x += 1f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    planar.x -= 1f;
                }

                if (keyboard.qKey.isPressed)
                {
                    _cityYaw -= 48f * deltaTime;
                }

                if (keyboard.eKey.isPressed)
                {
                    _cityYaw += 48f * deltaTime;
                }
            }

            if (planar.sqrMagnitude > 1f)
            {
                planar.Normalize();
            }

            Pan(planar * CityPanSpeed * deltaTime);

            if (mouse == null)
            {
                return;
            }

            var pointerDelta = mouse.delta.ReadValue();
            if (mouse.middleButton.isPressed)
            {
                var scale = _cityDistance * 0.002f;
                Pan(new Vector2(-pointerDelta.x, -pointerDelta.y) * scale);
            }

            if (mouse.rightButton.isPressed)
            {
                _cityYaw += pointerDelta.x * 0.16f;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(scroll, 0f))
            {
                _cityDistance = Mathf.Clamp(
                    _cityDistance - scroll * 0.018f,
                    MinimumCityDistance,
                    MaximumCityDistance);
            }
        }

        private void LateUpdate()
        {
            FailClosedIfRoadChaseOwnershipWasLost();
            if (_roadChaseActive)
            {
                return;
            }

            ApplyPose(immediate: false);
        }

        private void Pan(Vector2 delta)
        {
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            var right = Quaternion.Euler(0f, _cityYaw, 0f) * Vector3.right;
            var forward = Quaternion.Euler(0f, _cityYaw, 0f) * Vector3.forward;
            _cityFocus += right * delta.x + forward * delta.y;
            _cityFocus.x = Mathf.Clamp(_cityFocus.x, -15f, 15f);
            _cityFocus.z = Mathf.Clamp(_cityFocus.z, -12f, 18f);
        }

        private void ApplyPose(bool immediate)
        {
            if (_roadChaseActive)
            {
                return;
            }

            Vector3 targetPosition;
            Quaternion targetRotation;

            if (_inspectionMode &&
                _inspectionCameraAnchor != null &&
                _inspectionFocusAnchor != null)
            {
                targetPosition = _inspectionCameraAnchor.position;
                Vector3 focusDirection =
                    _inspectionFocusAnchor.position - targetPosition;
                targetRotation = focusDirection.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(focusDirection, Vector3.up)
                    : _inspectionCameraAnchor.rotation;
            }
            else if (_roadMode && _roadTarget != null)
            {
                var vehicleForward = _roadTarget.forward;
                vehicleForward.y = 0f;
                if (vehicleForward.sqrMagnitude < 0.001f)
                {
                    vehicleForward = Vector3.forward;
                }

                vehicleForward.Normalize();
                var focus = _roadTarget.position + Vector3.up * 1.15f;
                targetPosition = focus - vehicleForward * 8.5f + Vector3.up * 3.8f;
                targetRotation = Quaternion.LookRotation(focus - targetPosition, Vector3.up);
            }
            else
            {
                targetRotation = Quaternion.Euler(ComparisonPitch, _cityYaw, 0f);
                targetPosition =
                    _cityFocus - targetRotation * Vector3.forward * _cityDistance;
            }

            if (immediate || !Application.isPlaying)
            {
                transform.SetPositionAndRotation(targetPosition, targetRotation);
                return;
            }

            var blend = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, blend);
        }

        private bool HasLiveRoadChaseOwnership()
        {
            if (!_roadChaseActive || _roadChaseCamera == null)
            {
                return false;
            }

            try
            {
                return _roadChaseCamera.IsConfigured &&
                       _roadChaseCamera.IsChaseActive;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private bool IsRoadChaseComponentActive()
        {
            if (_roadChaseCamera == null)
            {
                return false;
            }

            try
            {
                return _roadChaseCamera.IsChaseActive;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private void FailClosedIfRoadChaseOwnershipWasLost()
        {
            if (!_roadChaseActive || HasLiveRoadChaseOwnership())
            {
                return;
            }

            Debug.LogWarning(
                "LAST_BEARING_CHASE_CAMERA_DISABLED ownership-lost",
                this);
            EndRoadChaseOwnership();
        }

        private void EndRoadChaseOwnership()
        {
            _roadChaseActive = false;
            try
            {
                _roadChaseCamera?.SetChaseActive(false);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    "LAST_BEARING_CHASE_CAMERA_DISABLED deactivation " +
                    exception.GetType().Name,
                    this);
            }

            if (_camera != null)
            {
                _camera.fieldOfView = StrategyFieldOfView;
            }

            ApplyPose(immediate: true);
        }
    }
}
