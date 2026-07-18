#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    /// <summary>
    /// Presentation-only elastic chase camera. It keeps a level world-up
    /// horizon and never feeds camera state back into vehicle physics.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class RoadFeelChaseCamera : MonoBehaviour
    {
        public const float BaseFieldOfView = 62f;
        public const float MaximumFieldOfView = 67f;

        private const float BaseDistance = 7.8f;
        private const float MaximumDistance = 10.2f;
        private const float MinimumPitch = 8f;
        private const float MaximumPitch = 28f;
        private const float DefaultPitch = 16f;
        private const float CollisionRadius = 0.28f;

        private readonly RaycastHit[] _collisionHits = new RaycastHit[12];
        private Transform? _target;
        private Rigidbody? _body;
        private Camera? _camera;
        private Vector3 _positionVelocity;
        private float _orbitYaw;
        private float _orbitPitch = DefaultPitch;
        private float _lastOrbitInputTime;

        public bool IsChaseActive => enabled;

        public bool IsConfigured =>
            _target != null && _body != null && _camera != null;

        public void Configure(Transform target, Rigidbody body)
        {
            _target = target;
            _body = body;
            _camera = GetComponent<Camera>();
            SnapBehind();
        }

        public void SetChaseActive(bool active)
        {
            if (!IsConfigured)
            {
                enabled = false;
                return;
            }

            if (enabled == active)
            {
                return;
            }

            enabled = active;
            _positionVelocity = Vector3.zero;
            if (active)
            {
                SnapBehind();
            }
        }

        public void SnapBehind()
        {
            _orbitYaw = 0f;
            _orbitPitch = DefaultPitch;
            _positionVelocity = Vector3.zero;
            _lastOrbitInputTime = Time.unscaledTime;

            if (_camera != null)
            {
                _camera.fieldOfView = BaseFieldOfView;
            }

            if (_target == null)
            {
                return;
            }

            CalculatePose(out var position, out var rotation, out _, out _);
            transform.SetPositionAndRotation(position, rotation);
        }

        private void LateUpdate()
        {
            if (_target == null || _body == null)
            {
                return;
            }

            ReadOrbitInput();
            if (Time.unscaledTime - _lastOrbitInputTime > 1.35f)
            {
                var recenterBlend = 1f - Mathf.Exp(-2.6f * Time.unscaledDeltaTime);
                _orbitYaw = Mathf.LerpAngle(_orbitYaw, 0f, recenterBlend);
                _orbitPitch = Mathf.Lerp(_orbitPitch, DefaultPitch, recenterBlend);
            }

            CalculatePose(
                out var desiredPosition,
                out var desiredRotation,
                out var focus,
                out var speed);
            var smoothTime = Mathf.Lerp(0.13f, 0.24f, Mathf.InverseLerp(0f, 30f, speed));
            var smoothedCandidate = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _positionVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            var resolvedCandidate = ResolveCollision(
                focus,
                smoothedCandidate,
                _target);
            if ((resolvedCandidate - focus).sqrMagnitude + 0.0001f <
                (smoothedCandidate - focus).sqrMagnitude)
            {
                // Obstructions contract immediately. Clearing them still
                // restores the full chase distance through SmoothDamp.
                _positionVelocity = Vector3.zero;
            }

            transform.position = resolvedCandidate;

            var actualView = focus - transform.position;
            var actualRotation = actualView.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(actualView.normalized, Vector3.up)
                : desiredRotation;

            var rotationBlend = 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                actualRotation,
                rotationBlend);

            if (_camera != null)
            {
                var targetFov = Mathf.Lerp(
                    BaseFieldOfView,
                    MaximumFieldOfView,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 32f, speed)));
                _camera.fieldOfView = Mathf.Lerp(
                    _camera.fieldOfView,
                    targetFov,
                    1f - Mathf.Exp(-3.5f * Time.unscaledDeltaTime));
            }
        }

        private void ReadOrbitInput()
        {
            var orbit = Vector2.zero;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                orbit += mouse.delta.ReadValue() * new Vector2(0.14f, -0.11f);
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                var stick = gamepad.rightStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                {
                    orbit += new Vector2(stick.x, -stick.y) *
                             (105f * Time.unscaledDeltaTime);
                }
            }

            var recenterPressed =
                (Keyboard.current?.vKey.wasPressedThisFrame ?? false) ||
                (gamepad?.rightStickButton.wasPressedThisFrame ?? false);
            if (recenterPressed)
            {
                _orbitYaw = 0f;
                _orbitPitch = DefaultPitch;
                _lastOrbitInputTime = Time.unscaledTime;
                return;
            }

            if (orbit.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _orbitYaw = Mathf.Clamp(_orbitYaw + orbit.x, -115f, 115f);
            _orbitPitch = Mathf.Clamp(
                _orbitPitch + orbit.y,
                MinimumPitch,
                MaximumPitch);
            _lastOrbitInputTime = Time.unscaledTime;
        }

        private void CalculatePose(
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 focus,
            out float speed)
        {
            var target = _target!;
            var body = _body!;
            var velocity = body.linearVelocity;
            var planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            speed = planarVelocity.magnitude;

            var forward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            var lookAhead = Vector3.ClampMagnitude(planarVelocity * 0.12f, 3.6f);
            focus = target.position + Vector3.up * 1.35f + lookAhead;
            var heading = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
            var rear = Quaternion.Euler(0f, heading + _orbitYaw, 0f) * Vector3.back;
            var distance = Mathf.Lerp(
                BaseDistance,
                MaximumDistance,
                Mathf.InverseLerp(8f, 32f, speed));
            var height = 0.65f + Mathf.Tan(_orbitPitch * Mathf.Deg2Rad) * distance;
            var desired = focus + rear * distance + Vector3.up * height;
            position = ResolveCollision(focus, desired, target);

            var view = focus - position;
            if (view.sqrMagnitude < 0.001f)
            {
                view = forward;
            }

            rotation = Quaternion.LookRotation(view.normalized, Vector3.up);
        }

        private Vector3 ResolveCollision(
            Vector3 focus,
            Vector3 desired,
            Transform target)
        {
            var travel = desired - focus;
            var distance = travel.magnitude;
            if (distance <= 0.001f)
            {
                return desired;
            }

            var hitCount = Physics.SphereCastNonAlloc(
                focus,
                CollisionRadius,
                travel / distance,
                _collisionHits,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            var nearest = distance;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = _collisionHits[index];
                if (hit.collider == null || hit.collider.transform.IsChildOf(target))
                {
                    continue;
                }

                nearest = Mathf.Min(nearest, hit.distance);
            }

            return nearest < distance
                ? focus + travel.normalized * Mathf.Max(0.5f, nearest - 0.12f)
                : desired;
        }
    }
}
