#nullable enable

using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    /// <summary>
    /// A presentation-only four-contact vehicle for handling experiments. It
    /// deliberately has no dependency on canonical simulation state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RoadFeelVehicleController : MonoBehaviour
    {
        private const int ContactCount = 4;
        private const int FrontContactCount = 2;
        private const int SphereCastBufferSize = 8;
        private const float DesignedFixedDeltaTime =
            RoadFeelMath.ExpectedFixedDeltaTimeSeconds;
        private const float MinimumPlanarMagnitudeSquared = 0.0001f;

        [Header("Mass and body")]
        [SerializeField, Min(1f)]
        private float _dryMassKilograms = 1_850f;

        [SerializeField, Min(0f)]
        private float _maximumCargoMassKilograms = 3_000f;

        [SerializeField]
        private Vector3 _centreOfMassOffset = new Vector3(0f, -0.42f, -0.08f);

        [Header("Contact geometry")]
        [SerializeField, Min(0.05f)]
        private float _wheelRadiusMetres = 0.42f;

        [SerializeField, Min(0.1f)]
        private float _suspensionLengthMetres = 0.64f;

        [SerializeField, Min(0.01f)]
        private float _suspensionTravelMetres = 0.34f;

        [SerializeField, Min(0.01f)]
        private float _desiredSagMetres = 0.18f;

        [SerializeField, Range(0.1f, 1.5f)]
        private float _dampingRatio = 0.72f;

        [SerializeField]
        private LayerMask _contactLayers = ~0;

        [Header("Tire and power envelope")]
        [SerializeField, Min(0f)]
        private float _maximumDriveForceNewtons = 15_500f;

        [SerializeField, Range(0f, 1f)]
        private float _reverseDriveMultiplier = 0.46f;

        [SerializeField, Min(0f)]
        private float _reverseTransitionSpeedMetresPerSecond = 0.75f;

        [SerializeField, Min(0f)]
        private float _reverseArmDelaySeconds = 0.18f;

        [SerializeField, Min(0f)]
        private float _reverseArmSpeedMetresPerSecond = 0.08f;

        [SerializeField, Min(0f)]
        private float _maximumBrakeForceNewtons = 21_000f;

        [SerializeField, Min(0f)]
        private float _maximumHandbrakeForceNewtons = 16_000f;

        [SerializeField, Min(0f)]
        private float _lateralResponsePerSecond = 10.5f;

        [Header("Steering and stability")]
        [SerializeField, Min(0.1f)]
        private float _steeringReferenceSpeedMetresPerSecond = 34f;

        [SerializeField, Range(0f, 60f)]
        private float _lowSpeedSteeringDegrees = 32f;

        [SerializeField, Range(0f, 60f)]
        private float _highSpeedSteeringDegrees = 9f;

        [SerializeField, Min(0f)]
        private float _antiRollForceNewtons = 13_500f;

        [SerializeField, Min(0f)]
        private float _groundedAngularDamping = 1_200f;

        private readonly Transform?[] _contactStations =
            new Transform?[ContactCount];
        private readonly Transform?[] _wheelVisuals =
            new Transform?[ContactCount];
        private readonly Transform?[] _steeringPivots =
            new Transform?[ContactCount];
        private readonly Quaternion[] _wheelBaseLocalRotations =
            new Quaternion[ContactCount];
        private readonly Quaternion[] _steeringBaseLocalRotations =
            new Quaternion[ContactCount];
        private readonly float[] _wheelSpinDegrees = new float[ContactCount];
        private readonly bool[] _grounded = new bool[ContactCount];
        private readonly RaycastHit[] _contactHits =
            new RaycastHit[ContactCount];
        private readonly RaycastHit[] _sphereCastBuffer =
            new RaycastHit[SphereCastBufferSize];
        private readonly float[] _compressionMetres = new float[ContactCount];
        private readonly float[] _normalLoadsNewtons = new float[ContactCount];
        private readonly RoadFeelSurfaceKind[] _surfaceKinds =
            new RoadFeelSurfaceKind[ContactCount];
        private readonly float[] _surfaceGrip = new float[ContactCount];
        private readonly float[] _surfaceRollingResistance =
            new float[ContactCount];
        private readonly float[] _surfaceLoadTotals =
            new float[(int)RoadFeelSurfaceKind.Washboard + 1];

        private Rigidbody? _body;
        private RoadFeelControlInput _controlInput;
        private RoadFeelTelemetry _telemetry;
        private float _cargoMassKilograms;
        private RoadFeelDamageBand _damageBand = RoadFeelDamageBand.Healthy;
        private float _steeringAngleDegrees;
        private int _recoveryTicksRemaining;
        private float _reverseIntentSeconds;
        private bool _reverseArmed;

        public Rigidbody Body
        {
            get
            {
                if (_body == null)
                {
                    _body = GetComponent<Rigidbody>();
                }

                return _body!;
            }
        }

        public RoadFeelTelemetry Telemetry => _telemetry;

        private void Awake()
        {
            ConfigureBody();
        }

        public void Initialize(
            Transform[] contactStations,
            Transform[] wheelVisuals,
            Transform[] steeringPivots)
        {
            for (int index = 0; index < ContactCount; index++)
            {
                _contactStations[index] = ElementAt(contactStations, index);
                _wheelVisuals[index] = ElementAt(wheelVisuals, index);
                _steeringPivots[index] = ElementAt(steeringPivots, index);

                Transform? wheel = _wheelVisuals[index];
                _wheelBaseLocalRotations[index] = wheel != null
                    ? wheel.localRotation
                    : Quaternion.identity;

                Transform? steeringPivot = _steeringPivots[index];
                _steeringBaseLocalRotations[index] = steeringPivot != null
                    ? steeringPivot.localRotation
                    : Quaternion.identity;
                _wheelSpinDegrees[index] = 0f;
            }
        }

        public void SetControlInput(RoadFeelControlInput input)
        {
            _controlInput = input;
        }

        public void SetLoad(
            float cargoMassKilograms,
            RoadFeelDamageBand damageBand)
        {
            _cargoMassKilograms = Mathf.Clamp(
                cargoMassKilograms,
                0f,
                Mathf.Max(0f, _maximumCargoMassKilograms));
            _damageBand = IsKnown(damageBand)
                ? damageBand
                : RoadFeelDamageBand.Healthy;
            ConfigureBody();
            UpdateTelemetry(Body, CountGroundedContacts());
        }

        public void ResetAt(Vector3 position, Quaternion rotation)
        {
            Rigidbody body = Body;
            bool wasKinematic = body.isKinematic;
            if (!wasKinematic)
            {
                body.isKinematic = true;
            }

            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            if (!wasKinematic)
            {
                body.isKinematic = false;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (!wasKinematic)
            {
                body.WakeUp();
            }

            _controlInput = default;
            _steeringAngleDegrees = 0f;
            _recoveryTicksRemaining = 25;
            _reverseIntentSeconds = 0f;
            _reverseArmed = false;
            for (int index = 0; index < ContactCount; index++)
            {
                _wheelSpinDegrees[index] = 0f;
                _grounded[index] = false;
                _compressionMetres[index] = 0f;
                _normalLoadsNewtons[index] = 0f;
            }

            ApplySteeringVisuals();
            UpdateTelemetry(body, 0);
        }

        private void FixedUpdate()
        {
            Rigidbody body = Body;
            float fixedDeltaTime = Time.fixedDeltaTime > 0f
                ? Time.fixedDeltaTime
                : DesignedFixedDeltaTime;
            float supportedMass = body.mass / ContactCount;
            float sag = Mathf.Clamp(
                _desiredSagMetres,
                0.01f,
                Mathf.Max(0.01f, _suspensionTravelMetres));
            float springRate = RoadFeelMath.SpringRateFromSag(
                supportedMass,
                sag);
            float damping = RoadFeelMath.DamperFromRatio(
                springRate,
                supportedMass,
                Mathf.Max(0.01f, _dampingRatio));

            int groundedContacts = SampleContacts(
                body,
                springRate,
                damping);

            float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float availableSteering = RoadFeelMath.SpeedSensitiveSteerAngle(
                forwardSpeed,
                Mathf.Max(0.1f, _steeringReferenceSpeedMetresPerSecond),
                _lowSpeedSteeringDegrees,
                _highSpeedSteeringDegrees);
            _steeringAngleDegrees = _controlInput.Steering *
                                    availableSteering *
                                    DamageSteeringMultiplier();

            ApplyTireForces(
                body,
                supportedMass,
                groundedContacts,
                fixedDeltaTime);
            ApplyAntiRoll(body);
            ApplyBoundedAngularDamping(body, groundedContacts);
            UpdateWheelVisuals(body, fixedDeltaTime);
            ApplySteeringVisuals();
            UpdateTelemetry(body, groundedContacts);

            if (_recoveryTicksRemaining > 0)
            {
                _recoveryTicksRemaining--;
            }
        }

        private int SampleContacts(
            Rigidbody body,
            float springRate,
            float damping)
        {
            int groundedContacts = 0;
            Vector3 suspensionUp = transform.up;
            Vector3 castDirection = -suspensionUp;
            float suspensionLength = Mathf.Max(
                0.1f,
                _suspensionLengthMetres);
            float travel = Mathf.Clamp(
                _suspensionTravelMetres,
                0.01f,
                suspensionLength);
            float maximumSpringForce =
                body.mass * Physics.gravity.magnitude * 3.5f / ContactCount;

            for (int index = 0; index < ContactCount; index++)
            {
                Vector3 stationPosition = StationWorldPosition(index);
                bool found = TryFindGround(
                    stationPosition,
                    Mathf.Max(0.05f, _wheelRadiusMetres),
                    castDirection,
                    suspensionLength,
                    body,
                    out RaycastHit hit);
                _grounded[index] = found;
                _contactHits[index] = hit;
                _normalLoadsNewtons[index] = 0f;
                _compressionMetres[index] = 0f;
                _surfaceKinds[index] = RoadFeelSurfaceKind.Hardpack;
                _surfaceGrip[index] = 0.84f;
                _surfaceRollingResistance[index] = 0.026f;

                if (!found)
                {
                    continue;
                }

                groundedContacts++;
                float compression = Mathf.Clamp(
                    suspensionLength - hit.distance,
                    0f,
                    travel);
                Vector3 pointVelocity = body.GetPointVelocity(stationPosition);
                float suspensionVelocity = Vector3.Dot(
                    pointVelocity,
                    suspensionUp);
                float springForce = Mathf.Clamp(
                    compression * springRate - suspensionVelocity * damping,
                    0f,
                    maximumSpringForce);

                _compressionMetres[index] = compression;
                _normalLoadsNewtons[index] = springForce;
                ResolveSurface(
                    hit.collider,
                    out _surfaceKinds[index],
                    out _surfaceGrip[index],
                    out _surfaceRollingResistance[index]);

                body.AddForceAtPosition(
                    suspensionUp * springForce,
                    stationPosition,
                    ForceMode.Force);
            }

            return groundedContacts;
        }

        private void ApplyTireForces(
            Rigidbody body,
            float supportedMass,
            int groundedContacts,
            float fixedDeltaTime)
        {
            if (groundedContacts <= 0)
            {
                return;
            }

            float vehicleForwardSpeed = Vector3.Dot(
                body.linearVelocity,
                transform.forward);
            UpdateReverseIntent(vehicleForwardSpeed, fixedDeltaTime);
            bool reverseRequested = RoadFeelMath.ShouldApplyReverse(
                _controlInput.Throttle,
                _controlInput.Brake,
                vehicleForwardSpeed,
                Mathf.Max(0f, _reverseTransitionSpeedMetresPerSecond),
                _reverseArmed);
            float signedDriveInput = RoadFeelMath.ResolveDriveInput(
                _controlInput.Throttle,
                _controlInput.Brake,
                reverseRequested,
                _reverseDriveMultiplier);
            float drivePerGroundedContact =
                signedDriveInput *
                Mathf.Max(0f, _maximumDriveForceNewtons) *
                DamageDriveMultiplier() /
                groundedContacts;
            bool serviceBrakeRequested =
                !reverseRequested && _controlInput.Brake > 0.01f;
            float brakePerGroundedContact =
                (serviceBrakeRequested ? _controlInput.Brake : 0f) *
                Mathf.Max(0f, _maximumBrakeForceNewtons) /
                groundedContacts;
            float handbrakePerRearContact =
                _controlInput.Handbrake *
                Mathf.Max(0f, _maximumHandbrakeForceNewtons) /
                Mathf.Max(1, groundedContacts - FrontContactCount);

            for (int index = 0; index < ContactCount; index++)
            {
                if (!_grounded[index])
                {
                    continue;
                }

                RaycastHit hit = _contactHits[index];
                Vector3 normal = hit.normal.sqrMagnitude >
                                 MinimumPlanarMagnitudeSquared
                    ? hit.normal.normalized
                    : transform.up;
                Quaternion steerRotation = index < FrontContactCount
                    ? Quaternion.AngleAxis(_steeringAngleDegrees, normal)
                    : Quaternion.identity;
                Vector3 forward = Vector3.ProjectOnPlane(
                    steerRotation * transform.forward,
                    normal);
                if (forward.sqrMagnitude < MinimumPlanarMagnitudeSquared)
                {
                    forward = Vector3.ProjectOnPlane(transform.up, normal);
                }

                if (forward.sqrMagnitude < MinimumPlanarMagnitudeSquared)
                {
                    continue;
                }

                forward.Normalize();
                Vector3 right = Vector3.Cross(normal, forward).normalized;
                Vector3 pointVelocity = body.GetPointVelocity(hit.point);
                float longitudinalSpeed = Vector3.Dot(pointVelocity, forward);
                float lateralSpeed = Vector3.Dot(pointVelocity, right);

                float maximumStoppingForce = brakePerGroundedContact;
                if (index >= FrontContactCount)
                {
                    maximumStoppingForce += handbrakePerRearContact;
                }

                float forwardForce = drivePerGroundedContact;
                if (maximumStoppingForce > 0f)
                {
                    forwardForce += RoadFeelMath.StoppingForce(
                        longitudinalSpeed,
                        Vector3.Dot(Physics.gravity, forward),
                        supportedMass,
                        Mathf.Max(0.001f, fixedDeltaTime),
                        maximumStoppingForce);
                }
                else if (Mathf.Abs(longitudinalSpeed) > 0.001f)
                {
                    float rollingForce =
                        _normalLoadsNewtons[index] *
                        _surfaceRollingResistance[index];
                    forwardForce -= Mathf.Sign(longitudinalSpeed) *
                                    Mathf.Min(
                                        rollingForce,
                                        Mathf.Abs(longitudinalSpeed) *
                                        supportedMass /
                                        Mathf.Max(0.001f, fixedDeltaTime));
                }

                float rearLateralRelease = index >= FrontContactCount
                    ? Mathf.Lerp(1f, 0.28f, _controlInput.Handbrake)
                    : 1f;
                float lateralForce =
                    -lateralSpeed *
                    supportedMass *
                    Mathf.Max(0f, _lateralResponsePerSecond) *
                    rearLateralRelease;

                float grip = _surfaceGrip[index] * DamageGripMultiplier();
                if (index >= FrontContactCount)
                {
                    grip *= Mathf.Lerp(1f, 0.72f, _controlInput.Handbrake);
                }

                float forceLimit = Mathf.Max(
                    0f,
                    _normalLoadsNewtons[index] * grip);
                float combinedMagnitude = Mathf.Sqrt(
                    forwardForce * forwardForce +
                    lateralForce * lateralForce);
                if (combinedMagnitude > forceLimit &&
                    combinedMagnitude > 0.001f)
                {
                    float scale = forceLimit / combinedMagnitude;
                    forwardForce *= scale;
                    lateralForce *= scale;
                }

                body.AddForceAtPosition(
                    forward * forwardForce + right * lateralForce,
                    hit.point,
                    ForceMode.Force);
            }
        }

        private void ApplyAntiRoll(Rigidbody body)
        {
            ApplyAntiRollPair(body, 0, 1);
            ApplyAntiRollPair(body, 2, 3);
        }

        private void UpdateReverseIntent(
            float forwardSpeedMetresPerSecond,
            float fixedDeltaTime)
        {
            bool pureBrakeIntent = _controlInput.Throttle <= 0.05f &&
                                   _controlInput.Brake > 0.01f;
            float armSpeed = Mathf.Max(
                0.01f,
                _reverseArmSpeedMetresPerSecond);
            if (!pureBrakeIntent ||
                forwardSpeedMetresPerSecond > armSpeed)
            {
                _reverseIntentSeconds = 0f;
                _reverseArmed = false;
                return;
            }

            if (forwardSpeedMetresPerSecond < -armSpeed)
            {
                _reverseArmed = true;
                return;
            }

            if (_reverseArmed)
            {
                return;
            }

            _reverseIntentSeconds += Mathf.Max(0f, fixedDeltaTime);
            if (_reverseIntentSeconds >= Mathf.Max(
                    0f,
                    _reverseArmDelaySeconds))
            {
                _reverseArmed = true;
            }
        }

        private void ApplyAntiRollPair(
            Rigidbody body,
            int leftIndex,
            int rightIndex)
        {
            if (!_grounded[leftIndex] || !_grounded[rightIndex])
            {
                return;
            }

            float travel = Mathf.Max(0.01f, _suspensionTravelMetres);
            float difference =
                (_compressionMetres[leftIndex] -
                 _compressionMetres[rightIndex]) /
                travel;
            float maximumAntiRollForce = Mathf.Max(
                0f,
                _antiRollForceNewtons);
            float antiRollForce = Mathf.Clamp(
                difference * maximumAntiRollForce,
                -maximumAntiRollForce,
                maximumAntiRollForce);
            Vector3 force = transform.up * antiRollForce;
            body.AddForceAtPosition(
                force,
                StationWorldPosition(leftIndex),
                ForceMode.Force);
            body.AddForceAtPosition(
                -force,
                StationWorldPosition(rightIndex),
                ForceMode.Force);
        }

        private void ApplyBoundedAngularDamping(
            Rigidbody body,
            int groundedContacts)
        {
            if (groundedContacts < 2 || _groundedAngularDamping <= 0f)
            {
                return;
            }

            Vector3 localAngularVelocity = transform.InverseTransformDirection(
                body.angularVelocity);
            Vector3 dampingTorqueLocal = new Vector3(
                -localAngularVelocity.x,
                0f,
                -localAngularVelocity.z) * _groundedAngularDamping;
            float maximumTorque = body.mass * Physics.gravity.magnitude * 0.7f;
            dampingTorqueLocal = Vector3.ClampMagnitude(
                dampingTorqueLocal,
                maximumTorque);
            body.AddTorque(
                transform.TransformDirection(dampingTorqueLocal),
                ForceMode.Force);
        }

        private void UpdateWheelVisuals(
            Rigidbody body,
            float fixedDeltaTime)
        {
            float radius = Mathf.Max(0.05f, _wheelRadiusMetres);
            for (int index = 0; index < ContactCount; index++)
            {
                Transform? wheel = _wheelVisuals[index];
                if (wheel == null)
                {
                    continue;
                }

                Vector3 stationPosition = StationWorldPosition(index);
                Vector3 wheelPosition = _grounded[index]
                    ? stationPosition - transform.up * _contactHits[index].distance
                    : stationPosition - transform.up *
                      Mathf.Max(0.1f, _suspensionLengthMetres);
                wheel.position = wheelPosition;

                Vector3 pointVelocity = body.GetPointVelocity(wheelPosition);
                float spinSpeed = Vector3.Dot(pointVelocity, transform.forward);
                _wheelSpinDegrees[index] = Mathf.Repeat(
                    _wheelSpinDegrees[index] +
                    spinSpeed / radius * Mathf.Rad2Deg * fixedDeltaTime,
                    360f);

                float steer = index < FrontContactCount
                    ? _steeringAngleDegrees
                    : 0f;
                bool wheelIsPivot = _steeringPivots[index] == wheel;
                Quaternion steering = wheelIsPivot
                    ? Quaternion.Euler(0f, steer, 0f)
                    : Quaternion.identity;
                wheel.localRotation =
                    _wheelBaseLocalRotations[index] *
                    steering *
                    Quaternion.AngleAxis(
                        _wheelSpinDegrees[index],
                        Vector3.up);
            }
        }

        private void ApplySteeringVisuals()
        {
            for (int index = 0; index < ContactCount; index++)
            {
                Transform? pivot = _steeringPivots[index];
                if (pivot == null || pivot == _wheelVisuals[index])
                {
                    continue;
                }

                float steer = index < FrontContactCount
                    ? _steeringAngleDegrees
                    : 0f;
                pivot.localRotation =
                    _steeringBaseLocalRotations[index] *
                    Quaternion.Euler(0f, steer, 0f);
            }
        }

        private void UpdateTelemetry(
            Rigidbody body,
            int groundedContacts)
        {
            for (int index = 0; index < _surfaceLoadTotals.Length; index++)
            {
                _surfaceLoadTotals[index] = 0f;
            }

            float compressionTotal = 0f;
            RoadFeelSurfaceKind firstGroundedSurface =
                RoadFeelSurfaceKind.Hardpack;
            bool foundFirstSurface = false;
            for (int index = 0; index < ContactCount; index++)
            {
                if (!_grounded[index])
                {
                    continue;
                }

                compressionTotal += _compressionMetres[index];
                int surfaceIndex = (int)_surfaceKinds[index];
                if (surfaceIndex >= 0 &&
                    surfaceIndex < _surfaceLoadTotals.Length)
                {
                    _surfaceLoadTotals[surfaceIndex] += Mathf.Max(
                        0.001f,
                        _normalLoadsNewtons[index]);
                }

                if (!foundFirstSurface)
                {
                    firstGroundedSurface = _surfaceKinds[index];
                    foundFirstSurface = true;
                }
            }

            RoadFeelSurfaceKind dominantSurface = firstGroundedSurface;
            if (groundedContacts > 0)
            {
                float dominantLoad = -1f;
                for (int index = 0;
                     index < _surfaceLoadTotals.Length;
                     index++)
                {
                    if (_surfaceLoadTotals[index] > dominantLoad)
                    {
                        dominantLoad = _surfaceLoadTotals[index];
                        dominantSurface = (RoadFeelSurfaceKind)index;
                    }
                }
            }

            Vector3 velocity = body.linearVelocity;
            _telemetry = new RoadFeelTelemetry(
                velocity.magnitude,
                Vector3.Dot(velocity, transform.forward),
                Vector3.Dot(body.angularVelocity, transform.up) *
                Mathf.Rad2Deg,
                RoadFeelMath.SignedBodySlipDegrees(
                    velocity,
                    transform.forward,
                    transform.up),
                _steeringAngleDegrees,
                groundedContacts,
                groundedContacts > 0
                    ? compressionTotal /
                      (groundedContacts *
                       Mathf.Max(0.01f, _suspensionTravelMetres))
                    : 0f,
                dominantSurface,
                _cargoMassKilograms,
                _damageBand,
                _recoveryTicksRemaining > 0);
        }

        private bool TryFindGround(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float distance,
            Rigidbody body,
            out RaycastHit nearestHit)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _sphereCastBuffer,
                distance,
                _contactLayers,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            nearestHit = default;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = _sphereCastBuffer[index];
                if (candidate.collider == null ||
                    candidate.rigidbody == body ||
                    candidate.collider.transform.IsChildOf(transform) ||
                    candidate.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidate.distance;
                nearestHit = candidate;
                found = true;
            }

            return found;
        }

        private Vector3 StationWorldPosition(int index)
        {
            Transform? station = _contactStations[index];
            if (station != null)
            {
                return station.position;
            }

            float side = index == 0 || index == 2 ? -0.92f : 0.92f;
            float longitudinal = index < FrontContactCount ? 1.28f : -1.28f;
            return transform.TransformPoint(new Vector3(
                side,
                0f,
                longitudinal));
        }

        private void ConfigureBody()
        {
            Rigidbody body = Body;
            body.mass = Mathf.Max(1f, _dryMassKilograms) +
                        Mathf.Max(0f, _cargoMassKilograms);
            body.centerOfMass = _centreOfMassOffset;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 8f;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.12f;
        }

        private static Transform? ElementAt(Transform[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index]
                : null;
        }

        private static void ResolveSurface(
            Collider collider,
            out RoadFeelSurfaceKind kind,
            out float grip,
            out float rollingResistance)
        {
            RoadFeelSurface? surface = collider.GetComponent<RoadFeelSurface>();
            if (surface == null)
            {
                surface = collider.GetComponentInParent<RoadFeelSurface>();
            }

            if (surface == null)
            {
                kind = RoadFeelSurfaceKind.Hardpack;
                grip = 0.84f;
                rollingResistance = 0.026f;
                return;
            }

            kind = surface.Kind;
            grip = surface.Grip;
            rollingResistance = surface.RollingResistance;
        }

        private float DamageDriveMultiplier()
        {
            switch (_damageBand)
            {
                case RoadFeelDamageBand.Worn:
                    return 0.82f;
                case RoadFeelDamageBand.Critical:
                    return 0.58f;
                default:
                    return 1f;
            }
        }

        private float DamageGripMultiplier()
        {
            switch (_damageBand)
            {
                case RoadFeelDamageBand.Worn:
                    return 0.86f;
                case RoadFeelDamageBand.Critical:
                    return 0.68f;
                default:
                    return 1f;
            }
        }

        private float DamageSteeringMultiplier()
        {
            switch (_damageBand)
            {
                case RoadFeelDamageBand.Worn:
                    return 0.88f;
                case RoadFeelDamageBand.Critical:
                    return 0.72f;
                default:
                    return 1f;
            }
        }

        private static bool IsKnown(RoadFeelDamageBand damageBand)
        {
            switch (damageBand)
            {
                case RoadFeelDamageBand.Healthy:
                case RoadFeelDamageBand.Worn:
                case RoadFeelDamageBand.Critical:
                    return true;
                default:
                    return false;
            }
        }

        private int CountGroundedContacts()
        {
            int count = 0;
            for (int index = 0; index < ContactCount; index++)
            {
                if (_grounded[index])
                {
                    count++;
                }
            }

            return count;
        }
    }
}
