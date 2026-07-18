#nullable enable

using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Vehicle
{
    /// <summary>
    /// Derived-only dollhouse service bay. It stages vehicle inspection from
    /// one fixed camera pose and deliberately adds no on-foot mode.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingGarageBayView : MonoBehaviour
    {
        public const string RootName = "bld_service_bay_cutaway_a [C0 Blockout]";

        private GameObject? _winchStand;
        private GameObject? _tankStand;
        private Light? _moduleWorkLight;
        private bool _built;

        public Transform? VehicleDock { get; private set; }

        public Transform? CameraAnchor { get; private set; }

        public Transform? FocusAnchor { get; private set; }

        public bool IsDollhouseCutaway => true;

        public bool HasRoof => false;

        public bool HasNearWall => false;

        public bool IsWinchStaged =>
            _winchStand != null && _winchStand.activeSelf;

        public bool IsRangeTankStaged =>
            _tankStand != null && _tankStand.activeSelf;

        internal void Build(
            Vector3 vehicleWorldPosition,
            Material concrete,
            Material darkIron,
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
            transform.position = vehicleWorldPosition;

            VehicleDock = CreateAnchor("ANCHOR_VEHICLE_DOCK", Vector3.zero);
            FocusAnchor = CreateAnchor(
                "ANCHOR_GARAGE_FOCUS",
                new Vector3(0f, 1.15f, -0.15f));
            CameraAnchor = CreateAnchor(
                "ANCHOR_GARAGE_CAMERA",
                new Vector3(8.8f, 7.2f, 9.8f));

            CreatePart(
                "SERVICE_FLOOR",
                PrimitiveType.Cube,
                new Vector3(0f, -0.18f, -0.2f),
                new Vector3(9f, 0.36f, 9f),
                concrete);
            CreatePart(
                "SERVICE_CHANNEL",
                PrimitiveType.Cube,
                new Vector3(0f, -0.02f, 0f),
                new Vector3(1.25f, 0.08f, 6.6f),
                darkIron);
            CreatePart(
                "REAR_MONUMENT_WALL",
                PrimitiveType.Cube,
                new Vector3(0f, 2.9f, -4.4f),
                new Vector3(9f, 6.2f, 0.42f),
                concrete);
            CreatePart(
                "LEFT_SERVICE_WALL",
                PrimitiveType.Cube,
                new Vector3(-4.4f, 2.35f, -0.2f),
                new Vector3(0.42f, 5.1f, 8.8f),
                darkIron);
            CreatePart(
                "OPERATING_PROSCENIUM",
                PrimitiveType.Cube,
                new Vector3(0f, 4.75f, -3.95f),
                new Vector3(8.6f, 0.6f, 0.7f),
                concrete);

            CreatePart(
                "GANTRY_POST_LEFT",
                PrimitiveType.Cube,
                new Vector3(-3.25f, 2.25f, -1.15f),
                new Vector3(0.34f, 4.5f, 0.34f),
                oxide);
            CreatePart(
                "GANTRY_POST_RIGHT",
                PrimitiveType.Cube,
                new Vector3(3.25f, 2.25f, -1.15f),
                new Vector3(0.34f, 4.5f, 0.34f),
                oxide);
            CreatePart(
                "GANTRY_BEAM",
                PrimitiveType.Cube,
                new Vector3(0f, 4.35f, -1.15f),
                new Vector3(6.8f, 0.34f, 0.34f),
                oxide);
            GameObject hoist = CreatePart(
                "SERVICE_HOIST",
                PrimitiveType.Cylinder,
                new Vector3(0f, 3.75f, -1.15f),
                new Vector3(0.42f, 0.68f, 0.42f),
                darkIron);
            hoist.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreatePart(
                "HOIST_CABLE",
                PrimitiveType.Cylinder,
                new Vector3(0f, 2.75f, -1.15f),
                new Vector3(0.045f, 1f, 0.045f),
                darkIron);

            CreatePart(
                "TOOL_BENCH",
                PrimitiveType.Cube,
                new Vector3(-2.2f, 0.82f, -3.65f),
                new Vector3(3.2f, 0.22f, 1.05f),
                oxide);
            CreatePart(
                "TOOL_SHADOW_BOARD",
                PrimitiveType.Cube,
                new Vector3(-2.2f, 2.1f, -4.12f),
                new Vector3(3.4f, 1.8f, 0.08f),
                bone);
            CreatePart(
                "SASHA_ONLY_SERVICE_CARD",
                PrimitiveType.Cube,
                new Vector3(-2.2f, 3.25f, -4.16f),
                new Vector3(2.8f, 0.48f, 0.06f),
                tungsten);

            _winchStand = CreateWinchStand(
                new Vector3(3.05f, 0f, -2.9f),
                darkIron,
                oxide);
            _tankStand = CreateTankStand(
                new Vector3(3.05f, 0f, -0.65f),
                bone,
                oxide);

            CreatePart(
                "LEGACY_ALIGNMENT_GAUGE",
                PrimitiveType.Cylinder,
                new Vector3(2.55f, 2.65f, -4.14f),
                new Vector3(0.46f, 0.09f, 0.46f),
                signal).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            CreatePracticalLight(
                "TUNGSTEN_WORKLIGHT_LEFT",
                new Vector3(-2.6f, 3.9f, -2.9f),
                new Color(1f, 0.72f, 0.38f, 1f),
                340f,
                8f);
            CreatePracticalLight(
                "TUNGSTEN_WORKLIGHT_RIGHT",
                new Vector3(2.6f, 3.9f, -2.9f),
                new Color(1f, 0.72f, 0.38f, 1f),
                340f,
                8f);
            _moduleWorkLight = CreatePracticalLight(
                "MODULE_STAGING_WORKLIGHT",
                new Vector3(3.1f, 2.8f, -1.75f),
                new Color(1f, 0.72f, 0.38f, 1f),
                180f,
                5f);

            ApplyModule(SashaScoutModulePresentation.None);
        }

        public void ApplyModule(SashaScoutModulePresentation module)
        {
            if (_winchStand != null)
            {
                _winchStand.SetActive(
                    module != SashaScoutModulePresentation.WinchAssembly);
            }

            if (_tankStand != null)
            {
                _tankStand.SetActive(
                    module != SashaScoutModulePresentation.SealedRangeTank);
            }

            if (_moduleWorkLight != null)
            {
                _moduleWorkLight.intensity =
                    module == SashaScoutModulePresentation.None ? 180f : 110f;
            }
        }

        internal void ApplyModule(LastBearingVisualModule module)
        {
            ApplyModule(module switch
            {
                LastBearingVisualModule.WinchAssembly =>
                    SashaScoutModulePresentation.WinchAssembly,
                LastBearingVisualModule.SealedRangeTank =>
                    SashaScoutModulePresentation.SealedRangeTank,
                _ => SashaScoutModulePresentation.None,
            });
        }

        private GameObject CreateWinchStand(
            Vector3 position,
            Material iron,
            Material oxide)
        {
            var stand = new GameObject("MODULE_STAND_WINCH");
            stand.transform.SetParent(transform, false);
            stand.transform.localPosition = position;
            CreatePart(
                "WINCH_STAND_PLINTH",
                PrimitiveType.Cube,
                new Vector3(position.x, 0.34f, position.z),
                new Vector3(2f, 0.68f, 1.5f),
                oxide,
                stand.transform,
                useParentLocalPosition: true);
            CreatePart(
                "STAGED_WINCH_DRUM",
                PrimitiveType.Cylinder,
                new Vector3(position.x, 0.92f, position.z),
                new Vector3(0.46f, 0.75f, 0.46f),
                iron,
                stand.transform,
                useParentLocalPosition: true).transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f);
            return stand;
        }

        private GameObject CreateTankStand(
            Vector3 position,
            Material bone,
            Material oxide)
        {
            var stand = new GameObject("MODULE_STAND_RANGE_TANK");
            stand.transform.SetParent(transform, false);
            stand.transform.localPosition = position;
            CreatePart(
                "TANK_STAND_PLINTH",
                PrimitiveType.Cube,
                new Vector3(position.x, 0.34f, position.z),
                new Vector3(2f, 0.68f, 1.5f),
                oxide,
                stand.transform,
                useParentLocalPosition: true);
            CreatePart(
                "STAGED_RANGE_TANK",
                PrimitiveType.Cylinder,
                new Vector3(position.x, 1.05f, position.z),
                new Vector3(0.66f, 0.85f, 0.66f),
                bone,
                stand.transform,
                useParentLocalPosition: true).transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f);
            return stand;
        }

        private Transform CreateAnchor(string name, Vector3 localPosition)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = localPosition;
            return anchor;
        }

        private GameObject CreatePart(
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Transform? explicitParent = null,
            bool useParentLocalPosition = false)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            Transform parent = explicitParent ?? transform;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = useParentLocalPosition
                ? localPosition - parent.localPosition
                : localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return part;
        }

        private Light CreatePracticalLight(
            string name,
            Vector3 localPosition,
            Color color,
            float intensity,
            float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = localPosition;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
