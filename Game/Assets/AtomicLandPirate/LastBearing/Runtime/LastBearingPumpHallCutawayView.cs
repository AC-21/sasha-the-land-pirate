#nullable enable

using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Fixed, derived-only pump-hall dollhouse. It owns no input, avatar,
    /// physics, save, or canonical mutation seam.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingPumpHallCutawayView : MonoBehaviour
    {
        public const string DirectionPackageId = "C0-VGR-04";
        public const string Revision = "R1";
        public const string ContentId = "bld_pump_hall_cutaway_a";

        public const string RootName =
            "bld_pump_hall_cutaway_a [VGR-04 Blockout]";

        private GameObject? _stagedRotor;
        private GameObject? _installedPump;
        private GameObject? _humanWorker;
        private GameObject? _robotWorker;
        private Transform? _installedRotor;
        private Light? _workLight;
        private bool _built;

        public Transform? CameraAnchor { get; private set; }

        public Transform? FocusAnchor { get; private set; }

        public Transform? FixedCivicSocket { get; private set; }

        public bool IsDollhouseCutaway => true;

        public bool HasRoof => false;

        public bool HasNearWall => false;

        public bool IsStagedRotorVisible =>
            _stagedRotor != null && _stagedRotor.activeSelf;

        public bool IsInstalledPumpVisible =>
            _installedPump != null && _installedPump.activeSelf;

        public bool IsHumanWorkerVisible =>
            _humanWorker != null && _humanWorker.activeSelf;

        public bool IsRobotWorkerVisible =>
            _robotWorker != null && _robotWorker.activeSelf;

        internal void Build(
            string fixedCivicSocketId,
            Material concrete,
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal,
            Material water)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            gameObject.name = RootName;
            transform.localPosition = new Vector3(0f, 0f, 0f);
            FocusAnchor = CreateAnchor(
                "ANCHOR_PUMP_HALL_FOCUS",
                new Vector3(0f, 1.8f, 0f));
            CameraAnchor = CreateAnchor(
                "ANCHOR_PUMP_HALL_CAMERA",
                new Vector3(10.6f, 7.8f, 11.4f));
            FixedCivicSocket = CreateAnchor(
                fixedCivicSocketId,
                new Vector3(2.8f, 0f, 0.6f));

            CreatePart(
                "PUMP_HALL_FLOOR",
                PrimitiveType.Cube,
                new Vector3(0f, -0.2f, 0f),
                new Vector3(10.5f, 0.4f, 9f),
                concrete);
            CreatePart(
                "PUMP_HALL_REAR_WALL",
                PrimitiveType.Cube,
                new Vector3(0f, 3.4f, -4.4f),
                new Vector3(10.5f, 7.2f, 0.45f),
                concrete);
            CreatePart(
                "PUMP_HALL_LEFT_WALL",
                PrimitiveType.Cube,
                new Vector3(-5.05f, 2.7f, 0f),
                new Vector3(0.45f, 5.8f, 9f),
                darkIron);
            CreatePart(
                "CIVIC_PROSCENIUM",
                PrimitiveType.Cube,
                new Vector3(0f, 5.9f, -3.95f),
                new Vector3(10f, 0.7f, 0.8f),
                concrete);
            CreatePart(
                "SERVICE_TRENCH",
                PrimitiveType.Cube,
                new Vector3(0f, -0.01f, 0.4f),
                new Vector3(2.1f, 0.1f, 6.6f),
                darkIron);
            CreatePart(
                "WATER_RILL",
                PrimitiveType.Cube,
                new Vector3(-2.75f, 0.12f, 0.4f),
                new Vector3(2.7f, 0.16f, 5.8f),
                water);
            CreatePart(
                "OVERHEAD_RISING_MAIN",
                PrimitiveType.Cylinder,
                new Vector3(-2.8f, 4.3f, -0.4f),
                new Vector3(0.36f, 3.5f, 0.36f),
                darkIron).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            _stagedRotor = new GameObject("RETURNED_ROTOR_STAGING");
            _stagedRotor.transform.SetParent(FixedCivicSocket, false);
            CreatePart(
                "ROTOR_CRADLE",
                PrimitiveType.Cube,
                new Vector3(0f, 0.32f, 0f),
                new Vector3(2.8f, 0.64f, 2f),
                oxide,
                _stagedRotor.transform);
            CreatePart(
                "STAGED_RETURNED_ROTOR",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.25f, 0f),
                new Vector3(0.82f, 1.15f, 0.82f),
                darkIron,
                _stagedRotor.transform).transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f);

            _installedPump = new GameObject("INSTALLED_AUXILIARY_PUMP");
            _installedPump.transform.SetParent(FixedCivicSocket, false);
            CreatePart(
                "AUXILIARY_PUMP_PLINTH",
                PrimitiveType.Cube,
                new Vector3(0f, 0.48f, 0f),
                new Vector3(3.4f, 0.96f, 2.8f),
                concrete,
                _installedPump.transform);
            CreatePart(
                "AUXILIARY_PUMP_BODY",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.7f, 0f),
                new Vector3(1.25f, 1.4f, 1.25f),
                oxide,
                _installedPump.transform).transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f);
            _installedRotor = CreatePart(
                "AUXILIARY_PUMP_ROTOR",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.7f, -1.28f),
                new Vector3(0.85f, 0.22f, 0.85f),
                tungsten,
                _installedPump.transform).transform;
            _installedRotor.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreatePart(
                "AUXILIARY_PUMP_DISCHARGE",
                PrimitiveType.Cylinder,
                new Vector3(0f, 3.75f, 0f),
                new Vector3(0.42f, 1.8f, 0.42f),
                darkIron,
                _installedPump.transform);
            CreatePart(
                "LEGACY_STATUS_LAMP",
                PrimitiveType.Cylinder,
                new Vector3(0f, 3.3f, -4.68f),
                new Vector3(0.35f, 0.08f, 0.35f),
                signal).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _humanWorker = CreateHumanWorker(oxide, bone);
            _robotWorker = CreateRobotWorker(darkIron, concrete, oxide);
            _workLight = CreatePracticalLight(
                "PUMP_HALL_TUNGSTEN_WORKLIGHT",
                new Vector3(2.8f, 4.8f, -0.4f),
                new Color(1f, 0.72f, 0.38f, 1f),
                240f,
                9f);
            Apply(
                HeavyCargoCustody.Depot,
                CityImprovementKind.None,
                humanVisible: true,
                robotVisible: true);
        }

        public void Apply(
            HeavyCargoCustody custody,
            CityImprovementKind improvement,
            bool humanVisible,
            bool robotVisible)
        {
            bool installed = improvement
                == CityImprovementKind.RefurbishedAuxiliaryPump;
            _stagedRotor?.SetActive(
                !installed && custody == HeavyCargoCustody.Settlement);
            _installedPump?.SetActive(installed);
            _humanWorker?.SetActive(humanVisible);
            _robotWorker?.SetActive(robotVisible);
            if (_workLight != null)
            {
                _workLight.intensity = installed ? 720f : 240f;
            }
        }

        private void Update()
        {
            if (_installedPump != null
                && _installedPump.activeSelf
                && _installedRotor != null)
            {
                _installedRotor.Rotate(
                    Vector3.forward,
                    135f * Time.deltaTime,
                    Space.Self);
            }
        }

        private GameObject CreateHumanWorker(Material workwear, Material cloth)
        {
            var worker = new GameObject("PUMP_WORKER_HUMAN");
            worker.transform.SetParent(transform, false);
            worker.transform.localPosition = new Vector3(-1.3f, 0f, 1.7f);
            CreatePart(
                "HUMAN_WORKWEAR",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.9f, 0f),
                new Vector3(0.44f, 0.72f, 0.44f),
                workwear,
                worker.transform);
            CreatePart(
                "HUMAN_SUN_HOOD",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.75f, 0f),
                new Vector3(0.4f, 0.3f, 0.4f),
                cloth,
                worker.transform);
            return worker;
        }

        private GameObject CreateRobotWorker(
            Material iron,
            Material concrete,
            Material token)
        {
            var worker = new GameObject("PUMP_WORKER_UTILITY_ROBOT");
            worker.transform.SetParent(transform, false);
            worker.transform.localPosition = new Vector3(0.7f, 0f, 1.7f);
            CreatePart(
                "ROBOT_TORSO",
                PrimitiveType.Cube,
                new Vector3(0f, 1.05f, 0f),
                new Vector3(0.9f, 1.25f, 0.65f),
                iron,
                worker.transform);
            CreatePart(
                "ROBOT_HEAD",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.92f, 0f),
                new Vector3(0.34f, 0.34f, 0.34f),
                concrete,
                worker.transform);
            CreatePart(
                "ROBOT_PERSONAL_TOKEN",
                PrimitiveType.Cube,
                new Vector3(0.46f, 1.25f, 0f),
                new Vector3(0.12f, 0.5f, 0.3f),
                token,
                worker.transform);
            return worker;
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
            PrimitiveType primitive,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Transform? parent = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent ?? transform, false);
            part.transform.localPosition = localPosition;
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
