#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    internal enum LastBearingVisualPhase
    {
        Title,
        FailingCity,
        Preparing,
        Ready,
        Outbound,
        Depot,
        Returning,
        Returned,
        Repaired
    }

    internal enum LastBearingVisualModule
    {
        None,
        WinchAssembly,
        SealedRangeTank
    }

    internal readonly struct LastBearingVisualSnapshot
    {
        public LastBearingVisualSnapshot(
            LastBearingVisualPhase phase,
            LastBearingVisualModule module,
            float routeProgress,
            float vehicleLateralNormalized,
            float waterNormalized,
            bool workshopPush,
            bool civicBuffer,
            bool factionClaimed,
            bool turbineRepaired,
            bool humanVisible,
            bool robotVisible)
        {
            Phase = phase;
            Module = module;
            RouteProgress = Mathf.Clamp01(routeProgress);
            VehicleLateralNormalized = Mathf.Clamp(
                vehicleLateralNormalized,
                -1f,
                1f);
            WaterNormalized = Mathf.Clamp01(waterNormalized);
            WorkshopPush = workshopPush;
            CivicBuffer = civicBuffer;
            FactionClaimed = factionClaimed;
            TurbineRepaired = turbineRepaired;
            HumanVisible = humanVisible;
            RobotVisible = robotVisible;
        }

        public LastBearingVisualPhase Phase { get; }
        public LastBearingVisualModule Module { get; }
        public float RouteProgress { get; }
        public float VehicleLateralNormalized { get; }
        public float WaterNormalized { get; }
        public bool WorkshopPush { get; }
        public bool CivicBuffer { get; }
        public bool FactionClaimed { get; }
        public bool TurbineRepaired { get; }
        public bool HumanVisible { get; }
        public bool RobotVisible { get; }

        public bool IsRoadMode =>
            Phase == LastBearingVisualPhase.Outbound ||
            Phase == LastBearingVisualPhase.Depot ||
            Phase == LastBearingVisualPhase.Returning;
    }

    /// <summary>
    /// A chunky, primitive-built field-service vehicle. Its transform is a
    /// derived view of canonical route progress and never feeds simulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingVehicleView : MonoBehaviour
    {
        private const float MaximumLateralOffsetMetres = 1.35f;
        private const float LateralResponsePerTick = 0.28f;
        private const float MaximumBodyLeanDegrees = 6.5f;
        private const float MaximumFrontWheelSteerDegrees = 24f;

        private readonly List<Transform> _wheels = new List<Transform>();
        private readonly List<Transform> _frontWheelSteeringPivots =
            new List<Transform>();
        private GameObject? _winch;
        private GameObject? _tank;
        private Vector3 _lastPosition;
        private float _visibleLateralOffset;

        internal LastBearingVisualModule Module { get; private set; }

        public float NormalizedRouteProgress { get; private set; }

        public float VisibleLateralOffset => _visibleLateralOffset;

        public float VisibleBodyLeanDegrees { get; private set; }

        public float FrontWheelSteerDegrees { get; private set; }

        internal void Build(Material iron, Material darkIron, Material enamel)
        {
            CreatePart(
                "Low Cab",
                PrimitiveType.Cube,
                new Vector3(0f, 1.2f, 0.2f),
                new Vector3(2.25f, 1.35f, 2.3f),
                iron);
            CreatePart(
                "Utility Shoulder Left",
                PrimitiveType.Cube,
                new Vector3(-1.25f, 1.15f, -0.55f),
                new Vector3(0.65f, 1.15f, 3.6f),
                darkIron);
            CreatePart(
                "Utility Shoulder Right",
                PrimitiveType.Cube,
                new Vector3(1.25f, 1.05f, -0.25f),
                new Vector3(0.65f, 0.95f, 3.0f),
                darkIron);
            CreatePart(
                "Bone Enamel Nose",
                PrimitiveType.Cube,
                new Vector3(0f, 0.85f, 1.62f),
                new Vector3(1.65f, 0.5f, 0.5f),
                enamel);

            CreateWheel("Front Left Wheel", new Vector3(-1.4f, 0.55f, 1.2f), darkIron, steerable: true);
            CreateWheel("Front Right Wheel", new Vector3(1.4f, 0.55f, 1.2f), darkIron, steerable: true);
            CreateWheel("Rear Left Wheel", new Vector3(-1.4f, 0.55f, -1.35f), darkIron, steerable: false);
            CreateWheel("Rear Right Wheel", new Vector3(1.4f, 0.55f, -1.35f), darkIron, steerable: false);

            _winch = CreatePart(
                "Winch Assembly - Steel Jaw",
                PrimitiveType.Cube,
                new Vector3(0f, 0.62f, 2.18f),
                new Vector3(1.9f, 0.45f, 0.75f),
                iron);
            var drum = CreatePart(
                "Winch Cable Drum",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.78f, 1.92f),
                new Vector3(0.48f, 0.8f, 0.48f),
                darkIron);
            drum.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            drum.transform.SetParent(_winch.transform, true);

            _tank = CreatePart(
                "Sealed Range Tank",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.65f, -1.05f),
                new Vector3(1.05f, 1.45f, 1.05f),
                enamel);
            _tank.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            SetModule(LastBearingVisualModule.None);
            transform.position = HomePosition;
            _lastPosition = transform.position;
        }

        internal void Apply(LastBearingVisualSnapshot snapshot)
        {
            SetModule(snapshot.Module);
            NormalizedRouteProgress = snapshot.RouteProgress;
            float targetLateralOffset =
                snapshot.VehicleLateralNormalized * MaximumLateralOffsetMetres;
            _visibleLateralOffset = Mathf.MoveTowards(
                _visibleLateralOffset,
                targetLateralOffset,
                LateralResponsePerTick);
            float visibleLateralNormalized = Mathf.Approximately(
                MaximumLateralOffsetMetres,
                0f)
                ? 0f
                : _visibleLateralOffset / MaximumLateralOffsetMetres;
            VisibleBodyLeanDegrees =
                -visibleLateralNormalized * MaximumBodyLeanDegrees;
            FrontWheelSteerDegrees =
                snapshot.VehicleLateralNormalized * MaximumFrontWheelSteerDegrees;

            Vector3 position;
            Vector3 tangent;
            if (snapshot.Phase == LastBearingVisualPhase.Outbound)
            {
                EvaluateRoute(snapshot.RouteProgress, snapshot.Module, out position, out tangent);
            }
            else if (snapshot.Phase == LastBearingVisualPhase.Depot)
            {
                EvaluateRoute(1f, snapshot.Module, out position, out tangent);
            }
            else if (snapshot.Phase == LastBearingVisualPhase.Returning)
            {
                EvaluateRoute(1f - snapshot.RouteProgress, snapshot.Module, out position, out tangent);
                tangent = -tangent;
            }
            else
            {
                position = HomePosition;
                tangent = Vector3.forward;
                _visibleLateralOffset = 0f;
                VisibleBodyLeanDegrees = 0f;
                FrontWheelSteerDegrees = 0f;
            }

            if (tangent.sqrMagnitude > 0.001f)
            {
                tangent.Normalize();
                Vector3 routeRight = Vector3.Cross(Vector3.up, tangent);
                position += routeRight * _visibleLateralOffset;
                transform.rotation =
                    Quaternion.LookRotation(tangent, Vector3.up) *
                    Quaternion.Euler(0f, 0f, VisibleBodyLeanDegrees);
            }

            transform.position = position;
            ApplyFrontWheelSteering();

            var travelled = Vector3.Distance(_lastPosition, transform.position);
            if (travelled > 0f)
            {
                foreach (var wheel in _wheels)
                {
                    wheel.Rotate(Vector3.right, travelled * 62f, Space.Self);
                }
            }

            _lastPosition = transform.position;
        }

        private static Vector3 HomePosition => new Vector3(-8.5f, 0f, -5f);

        private void SetModule(LastBearingVisualModule module)
        {
            Module = module;
            if (_winch != null)
            {
                _winch.SetActive(module == LastBearingVisualModule.WinchAssembly);
            }

            if (_tank != null)
            {
                _tank.SetActive(module == LastBearingVisualModule.SealedRangeTank);
            }
        }

        private void EvaluateRoute(
            float progress,
            LastBearingVisualModule module,
            out Vector3 position,
            out Vector3 tangent)
        {
            progress = Mathf.Clamp01(progress);
            var depot = new Vector3(9f, 0f, 31f);

            if (module == LastBearingVisualModule.WinchAssembly)
            {
                var midpoint = new Vector3(-2f, 0f, 13f);
                position = QuadraticBezier(HomePosition, midpoint, depot, progress);
                tangent = QuadraticBezierTangent(HomePosition, midpoint, depot, progress);
                return;
            }

            var control = new Vector3(16f, 0f, 7f);
            position = QuadraticBezier(HomePosition, control, depot, progress);
            tangent = QuadraticBezierTangent(HomePosition, control, depot, progress);
        }

        private static Vector3 QuadraticBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float t)
        {
            var inverse = 1f - t;
            return inverse * inverse * start +
                   2f * inverse * t * control +
                   t * t * end;
        }

        private static Vector3 QuadraticBezierTangent(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float t)
        {
            return 2f * (1f - t) * (control - start) +
                   2f * t * (end - control);
        }

        private GameObject CreatePart(
            string partName,
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private void CreateWheel(
            string wheelName,
            Vector3 localPosition,
            Material material,
            bool steerable)
        {
            var pivot = new GameObject(wheelName + " Steering Pivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = localPosition;
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = wheelName;
            wheel.transform.SetParent(pivot.transform, false);
            wheel.transform.localScale = new Vector3(0.72f, 0.42f, 0.72f);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.GetComponent<Renderer>().sharedMaterial = material;
            _wheels.Add(wheel.transform);
            if (steerable)
            {
                _frontWheelSteeringPivots.Add(pivot.transform);
            }
        }

        private void ApplyFrontWheelSteering()
        {
            foreach (Transform pivot in _frontWheelSteeringPivots)
            {
                pivot.localRotation = Quaternion.Euler(
                    0f,
                    FrontWheelSteerDegrees,
                    0f);
            }
        }
    }
}
