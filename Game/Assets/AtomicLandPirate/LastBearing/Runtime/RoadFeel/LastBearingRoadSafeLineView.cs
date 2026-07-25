#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    public enum LastBearingRoadSafeLineState
    {
        Clear = 0,
        Safe = 1,
        LeftRisk = 2,
        RightRisk = 3,
    }

    /// <summary>
    /// Physics-free road paint and edge witnesses derived from the canonical
    /// lateral position. It never feeds presentation state back into the road.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingRoadSafeLineView : MonoBehaviour
    {
        public const string RootName = "Hold the Bone Line [Derived Only]";
        public const string VisualRootName = "ROAD_SAFE_LINE_VISUALS";
        public const string SegmentRootPrefix = "ROAD_SAFE_LINE_SEGMENT_";
        public const string LeftSafeLinePrefix = "BONE_SAFE_LINE_LEFT_";
        public const string RightSafeLinePrefix = "BONE_SAFE_LINE_RIGHT_";
        public const string LeftWarningPrefix = "OXIDE_EDGE_WARNING_LEFT_";
        public const string RightWarningPrefix = "OXIDE_EDGE_WARNING_RIGHT_";
        public const int SurfaceCount = 4;
        public const int SafeLineMarkerCount = SurfaceCount * 2;
        public const int WarningWitnessCount = SurfaceCount * 2;

        private const float SafeLineLocalX = 0.375f;
        private const float WarningLocalX = 0.47f;

        private readonly GameObject[] _leftWarningWitnesses =
            new GameObject[SurfaceCount];
        private readonly GameObject[] _rightWarningWitnesses =
            new GameObject[SurfaceCount];
        private GameObject? _visualRoot;
        private bool _built;

        public LastBearingRoadSafeLineState State { get; private set; } =
            LastBearingRoadSafeLineState.Clear;

        public int WarningTransitionCount { get; private set; }

        public bool IsVisible =>
            _visualRoot?.activeInHierarchy == true;

        public bool IsLeftWarningVisible =>
            State == LastBearingRoadSafeLineState.LeftRisk &&
            IsVisible;

        public bool IsRightWarningVisible =>
            State == LastBearingRoadSafeLineState.RightRisk &&
            IsVisible;

        internal void Build(
            Transform routeApron,
            Transform collapsedShortBranch,
            Transform exposedLongRouteA,
            Transform exposedLongRouteB,
            Material boneEnamel,
            Material oxide)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            gameObject.name = RootName;
            boneEnamel = boneEnamel ??
                throw new ArgumentNullException(nameof(boneEnamel));
            oxide = oxide ?? throw new ArgumentNullException(nameof(oxide));

            _visualRoot = new GameObject(VisualRootName);
            _visualRoot.transform.SetParent(transform, false);

            BuildSurfaceMarkers(
                0,
                RequireSurface(routeApron, nameof(routeApron)),
                boneEnamel,
                oxide);
            BuildSurfaceMarkers(
                1,
                RequireSurface(
                    collapsedShortBranch,
                    nameof(collapsedShortBranch)),
                boneEnamel,
                oxide);
            BuildSurfaceMarkers(
                2,
                RequireSurface(exposedLongRouteA, nameof(exposedLongRouteA)),
                boneEnamel,
                oxide);
            BuildSurfaceMarkers(
                3,
                RequireSurface(exposedLongRouteB, nameof(exposedLongRouteB)),
                boneEnamel,
                oxide);

            ApplyState(LastBearingRoadSafeLineState.Clear);
        }

        public void Apply(
            LastBearingReadModel? model,
            bool isDrivingMode)
        {
            LastBearingRoadSafeLineState next =
                model == null || !isDrivingMode
                    ? LastBearingRoadSafeLineState.Clear
                    : DeriveState(model.VehicleLateralMilli);
            if (next == State)
            {
                return;
            }

            WarningTransitionCount++;
            ApplyState(next);
        }

        public static LastBearingRoadSafeLineState DeriveState(
            long vehicleLateralMilli)
        {
            if (vehicleLateralMilli <
                -LastBearingBalanceV1.RoadSafeHalfWidthMilli)
            {
                return LastBearingRoadSafeLineState.LeftRisk;
            }

            if (vehicleLateralMilli >
                LastBearingBalanceV1.RoadSafeHalfWidthMilli)
            {
                return LastBearingRoadSafeLineState.RightRisk;
            }

            return LastBearingRoadSafeLineState.Safe;
        }

        private void ApplyState(LastBearingRoadSafeLineState state)
        {
            State = state;
            _visualRoot?.SetActive(
                state != LastBearingRoadSafeLineState.Clear);
            bool leftRisk =
                state == LastBearingRoadSafeLineState.LeftRisk;
            bool rightRisk =
                state == LastBearingRoadSafeLineState.RightRisk;
            for (var index = 0; index < SurfaceCount; index++)
            {
                _leftWarningWitnesses[index]?.SetActive(leftRisk);
                _rightWarningWitnesses[index]?.SetActive(rightRisk);
            }
        }

        private void BuildSurfaceMarkers(
            int index,
            Transform surface,
            Material boneEnamel,
            Material oxide)
        {
            string suffix = index.ToString("00");
            MeshFilter surfaceMeshFilter =
                surface.GetComponent<MeshFilter>();
            Mesh? markerMesh = surfaceMeshFilter != null
                ? surfaceMeshFilter.sharedMesh
                : null;
            if (markerMesh == null)
            {
                throw new InvalidOperationException(
                    "ROAD_SAFE_LINE_SURFACE_MESH_MISSING:" + surface.name);
            }

            var segmentRoot = new GameObject(
                SegmentRootPrefix + suffix).transform;
            segmentRoot.SetParent(_visualRoot!.transform, false);
            segmentRoot.localPosition = surface.localPosition;
            segmentRoot.localRotation = surface.localRotation;
            segmentRoot.localScale = surface.localScale;
            CreatePhysicsFreeMarker(
                LeftSafeLinePrefix + suffix,
                segmentRoot,
                new Vector3(-SafeLineLocalX, 0.56f, 0f),
                new Vector3(0.035f, 0.08f, 0.94f),
                markerMesh,
                boneEnamel);
            CreatePhysicsFreeMarker(
                RightSafeLinePrefix + suffix,
                segmentRoot,
                new Vector3(SafeLineLocalX, 0.56f, 0f),
                new Vector3(0.035f, 0.08f, 0.94f),
                markerMesh,
                boneEnamel);
            _leftWarningWitnesses[index] = CreatePhysicsFreeMarker(
                LeftWarningPrefix + suffix,
                segmentRoot,
                new Vector3(-WarningLocalX, 0.64f, 0f),
                new Vector3(0.06f, 0.18f, 0.9f),
                markerMesh,
                oxide);
            _rightWarningWitnesses[index] = CreatePhysicsFreeMarker(
                RightWarningPrefix + suffix,
                segmentRoot,
                new Vector3(WarningLocalX, 0.64f, 0f),
                new Vector3(0.06f, 0.18f, 0.9f),
                markerMesh,
                oxide);
        }

        private static Transform RequireSurface(
            Transform? surface,
            string parameterName)
        {
            return surface ??
                throw new ArgumentNullException(parameterName);
        }

        private static GameObject CreatePhysicsFreeMarker(
            string markerName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Mesh mesh,
            Material material)
        {
            var marker = new GameObject(markerName);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = localScale;
            marker.AddComponent<MeshFilter>().sharedMesh = mesh;
            marker.AddComponent<MeshRenderer>().sharedMaterial = material;

            return marker;
        }
    }
}
