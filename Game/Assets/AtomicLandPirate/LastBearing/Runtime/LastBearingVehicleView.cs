#nullable enable

using AtomicLandPirate.Presentation.LastBearing.Vehicle;
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

        private SashaScoutVisual? _scoutVisual;
        private Vector3 _lastPosition;
        private float _visibleLateralOffset;
        private LastBearingVisualSnapshot _lastSnapshot;
        private bool _hasSnapshot;

        internal LastBearingVisualModule Module { get; private set; }

        public float NormalizedRouteProgress { get; private set; }

        public float VisibleLateralOffset => _visibleLateralOffset;

        public float VisibleBodyLeanDegrees { get; private set; }

        public float FrontWheelSteerDegrees { get; private set; }

        public SashaScoutVisual? ScoutVisual => _scoutVisual;

        public static Vector3 HomePosition => new Vector3(-8.5f, 0f, -5f);

        internal void Build(
            Material iron,
            Material oxide,
            Material enamel,
            Material rubber,
            Material tungsten,
            Material signal)
        {
            _scoutVisual = SashaScoutBlockoutFactory.Create(
                transform,
                new SashaScoutBlockoutMaterials(
                    iron,
                    oxide,
                    enamel,
                    rubber,
                    tungsten,
                    signal),
                includeRoadCollisionShell: false);
            SetModule(LastBearingVisualModule.None);
            transform.position = HomePosition;
            _lastPosition = transform.position;
        }

        internal void Apply(LastBearingVisualSnapshot snapshot)
        {
            Apply(snapshot, snapLateral: false);
        }

        internal void SnapToCanonicalRoadPose()
        {
            if (!_hasSnapshot || !_lastSnapshot.IsRoadMode)
            {
                return;
            }

            Apply(_lastSnapshot, snapLateral: true);
        }

        private void Apply(
            LastBearingVisualSnapshot snapshot,
            bool snapLateral)
        {
            _lastSnapshot = snapshot;
            _hasSnapshot = true;
            SetModule(snapshot.Module);
            NormalizedRouteProgress = snapshot.RouteProgress;
            float targetLateralOffset =
                snapshot.VehicleLateralNormalized * MaximumLateralOffsetMetres;
            _visibleLateralOffset = snapLateral
                ? targetLateralOffset
                : Mathf.MoveTowards(
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
            _scoutVisual?.SetFrontSteering(FrontWheelSteerDegrees);

            var travelled = Vector3.Distance(_lastPosition, transform.position);
            if (travelled > 0f)
            {
                _scoutVisual?.RotateWheels(travelled * 62f);
            }

            _lastPosition = transform.position;
        }

        private void SetModule(LastBearingVisualModule module)
        {
            Module = module;
            _scoutVisual?.ApplyModule(module);
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

    }
}
