#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum RouteModulePointPresentationState
    {
        Dormant = 0,
        WinchAvailable = 1,
        TankAvailable = 2,
        WinchRecovered = 3,
        TankCrossed = 4,
    }

    /// <summary>
    /// One procedural C0 route landmark with two mutually exclusive module
    /// readings. It renders canonical availability only and owns no input,
    /// physics, proximity, save, or route-progress authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingRouteModulePointView : MonoBehaviour
    {
        public const string DirectionPackageId = "C0-VGR-03";
        public const string Revision = "R1";
        public const string ContentId = "poi_wreck_line_module_point_a";
        public const string RootName =
            "poi_wreck_line_module_point_a [C0-VGR-03 R1]";
        public const string InteractionAnchorName =
            "ANCHOR_WRECK_LINE_MODULE_INTERACTION";

        private GameObject? _winchSite;
        private GameObject? _tankSite;
        private GameObject? _pumpRotor;
        private GameObject? _recoveredLatch;
        private GameObject? _dustCurtain;
        private GameObject? _crossedSignal;
        private Light? _winchLight;
        private Light? _tankLight;
        private bool _built;

        public Transform? InteractionAnchor { get; private set; }

        public RouteModulePointPresentationState State { get; private set; }

        public bool IsPumpRotorVisible =>
            _pumpRotor != null && _pumpRotor.activeSelf;

        public bool IsDustCurtainVisible =>
            _dustCurtain != null && _dustCurtain.activeSelf;

        public void Build(
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            gameObject.name = RootName;
            InteractionAnchor = new GameObject(InteractionAnchorName).transform;
            InteractionAnchor.SetParent(transform, false);

            _winchSite = new GameObject("Collapsed Branch Wreck Line");
            _winchSite.transform.SetParent(transform, false);
            _winchSite.transform.localPosition = new Vector3(-0.9f, 0f, 13f);
            CreatePrimitive(
                "Wreck Line Rail Bed",
                PrimitiveType.Cube,
                _winchSite.transform,
                new Vector3(0f, 0.16f, 0f),
                new Vector3(5.4f, 0.32f, 3.2f),
                iron,
                Quaternion.Euler(0f, -18f, 0f));
            CreatePrimitive(
                "Collapsed Pump Gantry",
                PrimitiveType.Cube,
                _winchSite.transform,
                new Vector3(-0.9f, 1.25f, 0.15f),
                new Vector3(0.42f, 2.5f, 4.5f),
                oxide,
                Quaternion.Euler(72f, 18f, 4f));
            _pumpRotor = CreatePrimitive(
                "Existing Pump Rotor",
                PrimitiveType.Cylinder,
                _winchSite.transform,
                new Vector3(0.65f, 0.72f, 0.2f),
                new Vector3(0.95f, 0.34f, 0.95f),
                bone,
                Quaternion.Euler(90f, 0f, 0f));
            _recoveredLatch = CreatePrimitive(
                "Winch Recovery Seated",
                PrimitiveType.Cube,
                _winchSite.transform,
                new Vector3(0.65f, 0.55f, 0.2f),
                new Vector3(1.7f, 0.14f, 0.22f),
                signal,
                Quaternion.Euler(0f, 32f, 0f));
            _winchLight = CreateWorkLight(
                "Wreck Line Winch Practical",
                _winchSite.transform,
                new Vector3(0f, 2.8f, -0.4f),
                tungsten);

            _tankSite = new GameObject("Exposed Route Dust Line");
            _tankSite.transform.SetParent(transform, false);
            _tankSite.transform.localPosition = new Vector3(8.1f, 0f, 12.5f);
            CreatePrimitive(
                "Dust Line Threshold",
                PrimitiveType.Cube,
                _tankSite.transform,
                new Vector3(0f, 0.14f, 0f),
                new Vector3(5.2f, 0.28f, 2.4f),
                iron,
                Quaternion.Euler(0f, -52f, 0f));
            for (var index = -1; index <= 1; index++)
            {
                CreatePrimitive(
                    "Dust Baffle " + (index + 2).ToString("00"),
                    PrimitiveType.Cube,
                    _tankSite.transform,
                    new Vector3(index * 1.4f, 1.2f, 0f),
                    new Vector3(0.18f, 2.4f, 1.6f),
                    oxide,
                    Quaternion.Euler(0f, 12f * index, 0f));
            }

            _dustCurtain = CreatePrimitive(
                "Dust Exposure Curtain",
                PrimitiveType.Cube,
                _tankSite.transform,
                new Vector3(0f, 1.1f, 0.35f),
                new Vector3(4.2f, 2.2f, 0.08f),
                tungsten,
                Quaternion.identity);
            _crossedSignal = CreatePrimitive(
                "Sealed Crossing Confirmed",
                PrimitiveType.Cube,
                _tankSite.transform,
                new Vector3(0f, 2.5f, -0.4f),
                new Vector3(2.2f, 0.18f, 0.18f),
                signal,
                Quaternion.identity);
            _tankLight = CreateWorkLight(
                "Dust Line Tank Practical",
                _tankSite.transform,
                new Vector3(0f, 3.2f, -0.5f),
                tungsten);

            ApplyState(RouteModulePointPresentationState.Dormant);
        }

        public void ApplyState(RouteModulePointPresentationState state)
        {
            if (!Enum.IsDefined(typeof(RouteModulePointPresentationState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            State = state;
            bool winch = state == RouteModulePointPresentationState.WinchAvailable ||
                         state == RouteModulePointPresentationState.WinchRecovered;
            bool tank = state == RouteModulePointPresentationState.TankAvailable ||
                        state == RouteModulePointPresentationState.TankCrossed;
            _winchSite?.SetActive(winch);
            _tankSite?.SetActive(tank);
            _pumpRotor?.SetActive(
                state == RouteModulePointPresentationState.WinchAvailable);
            _recoveredLatch?.SetActive(
                state == RouteModulePointPresentationState.WinchRecovered);
            _dustCurtain?.SetActive(
                state == RouteModulePointPresentationState.TankAvailable);
            _crossedSignal?.SetActive(
                state == RouteModulePointPresentationState.TankCrossed);

            if (InteractionAnchor != null)
            {
                InteractionAnchor.localPosition = winch
                    ? new Vector3(-0.9f, 0.75f, 13f)
                    : new Vector3(8.1f, 0.75f, 12.5f);
            }

            if (_winchLight != null)
            {
                _winchLight.intensity = state ==
                    RouteModulePointPresentationState.WinchAvailable
                    ? 520f
                    : 180f;
            }

            if (_tankLight != null)
            {
                _tankLight.intensity = state ==
                    RouteModulePointPresentationState.TankAvailable
                    ? 520f
                    : 180f;
            }
        }

        private static Light CreateWorkLight(
            string name,
            Transform parent,
            Vector3 position,
            Material tungsten)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = tungsten.color;
            light.range = 7f;
            light.shadows = LightShadows.None;
            return light;
        }

        private static GameObject CreatePrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion rotation)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.transform.localRotation = rotation;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return primitive;
        }
    }
}
