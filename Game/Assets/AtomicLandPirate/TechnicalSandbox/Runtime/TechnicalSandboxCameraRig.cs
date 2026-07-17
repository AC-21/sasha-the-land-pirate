#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;

namespace AC21.Sasha.TechnicalSandbox
{
    /// <summary>
    /// Presentation-only strategy camera for the WP-0003 technical sandbox.
    /// Camera movement never enters authoritative state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TechnicalSandboxCameraRig : MonoBehaviour
    {
        private const float MinimumDistance = 8f;
        private const float MaximumDistance = 34f;

        private Vector3 _focus = Vector3.zero;
        private float _yaw = 38f;
        private float _pitch = 57f;
        private float _distance = 21f;

        public Vector3 Focus => _focus;

        public float Distance => _distance;

        public void Configure(Vector3 focus)
        {
            _focus = focus;
            ApplyPose();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var deltaTime = Time.unscaledDeltaTime;

            var planarInput = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    planarInput.y += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    planarInput.y -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    planarInput.x += 1f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    planarInput.x -= 1f;
                }

                if (keyboard.qKey.isPressed)
                {
                    _yaw -= 52f * deltaTime;
                }

                if (keyboard.eKey.isPressed)
                {
                    _yaw += 52f * deltaTime;
                }
            }

            if (planarInput.sqrMagnitude > 1f)
            {
                planarInput.Normalize();
            }

            Pan(planarInput * (9f * deltaTime));

            if (mouse != null)
            {
                var pointerDelta = mouse.delta.ReadValue();
                if (mouse.middleButton.isPressed)
                {
                    var dragScale = _distance * 0.0018f;
                    Pan(new Vector2(-pointerDelta.x, -pointerDelta.y) * dragScale);
                }

                if (mouse.rightButton.isPressed)
                {
                    _yaw += pointerDelta.x * 0.18f;
                    _pitch = Mathf.Clamp(
                        _pitch - pointerDelta.y * 0.12f,
                        38f,
                        72f);
                }

                var scroll = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(scroll, 0f))
                {
                    _distance = Mathf.Clamp(
                        _distance - scroll * 0.0125f,
                        MinimumDistance,
                        MaximumDistance);
                }
            }
        }

        private void LateUpdate()
        {
            ApplyPose();
        }

        private void Pan(Vector2 delta)
        {
            if (delta.sqrMagnitude <= 0f)
            {
                return;
            }

            var right = transform.right;
            right.y = 0f;
            right.Normalize();

            var forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            _focus += right * delta.x + forward * delta.y;
            _focus.x = Mathf.Clamp(_focus.x, -11f, 11f);
            _focus.z = Mathf.Clamp(_focus.z, -8f, 8f);
        }

        private void ApplyPose()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(
                _focus - rotation * Vector3.forward * _distance,
                rotation);
        }
    }
}
