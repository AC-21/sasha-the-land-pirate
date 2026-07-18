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
        private GameObject? _turbineRepairTarget;
        private GameObject? _ceramicBearingAtTurbine;
        private GameObject? _fieldSleeveRepairCollar;
        private GameObject? _slackDriveBelt;
        private GameObject? _tautDriveBelt;
        private Transform? _installedRotor;
        private Transform? _civicTurbineRotor;
        private Light? _workLight;
        private Light? _turbineStatusLight;
        private TextMesh? _repairStatusText;
        private TurbineCondition _visibleTurbineCondition =
            TurbineCondition.Failing;
        private bool _built;

        public Transform? CameraAnchor { get; private set; }

        public Transform? FocusAnchor { get; private set; }

        public Transform? FixedCivicSocket { get; private set; }

        public Transform? FixedTurbineRepairSocket { get; private set; }

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

        public bool IsTurbineRepairTargetVisible =>
            _turbineRepairTarget != null && _turbineRepairTarget.activeSelf;

        public bool IsCeramicBearingAtTurbineVisible =>
            _ceramicBearingAtTurbine != null &&
            _ceramicBearingAtTurbine.activeSelf;

        public bool IsFieldSleeveRepairVisible =>
            _fieldSleeveRepairCollar != null &&
            _fieldSleeveRepairCollar.activeSelf;

        public TurbineCondition VisibleTurbineCondition =>
            _visibleTurbineCondition;

        public string RepairOutcomeLabel => _repairStatusText?.text ?? string.Empty;

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
                new Vector3(-2.2f, 1.55f, -1.1f));
            CameraAnchor = CreateAnchor(
                "ANCHOR_PUMP_HALL_CAMERA",
                new Vector3(11.8f, 8.8f, 13.2f));
            FixedCivicSocket = CreateAnchor(
                fixedCivicSocketId,
                new Vector3(2.8f, 0f, 0.6f));
            FixedTurbineRepairSocket = CreateAnchor(
                "SOCKET_TURBINE_REPAIR",
                new Vector3(-2.45f, 0f, 0.4f));

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

            BuildTurbineRepairStation(
                concrete,
                darkIron,
                oxide,
                bone,
                tungsten,
                signal);

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
            ApplyTurbineRepair(
                RepairCargoKind.None,
                RepairCargoCustody.None,
                TurbineCondition.Failing);
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

        public void ApplyTurbineRepair(
            RepairCargoKind cargoKind,
            RepairCargoCustody cargoCustody,
            TurbineCondition turbineCondition)
        {
            bool bearingRepaired =
                turbineCondition == TurbineCondition.BearingRepaired;
            bool sleeveRepaired =
                turbineCondition == TurbineCondition.SleeveRepaired;
            if (bearingRepaired &&
                (cargoKind != RepairCargoKind.CeramicBearing ||
                 cargoCustody != RepairCargoCustody.Turbine))
            {
                throw new System.InvalidOperationException(
                    "LAST_BEARING_PUMP_HALL_BEARING_PRESENTATION_INVALID");
            }

            if (sleeveRepaired &&
                (cargoKind != RepairCargoKind.FieldSleeve ||
                 cargoCustody != RepairCargoCustody.Consumed))
            {
                throw new System.InvalidOperationException(
                    "LAST_BEARING_PUMP_HALL_SLEEVE_PRESENTATION_INVALID");
            }

            _visibleTurbineCondition = turbineCondition;
            bool repairCargoOnVehicle =
                cargoCustody == RepairCargoCustody.Vehicle &&
                cargoKind != RepairCargoKind.None;
            bool repaired = bearingRepaired || sleeveRepaired;
            _turbineRepairTarget?.SetActive(
                turbineCondition == TurbineCondition.Failing &&
                repairCargoOnVehicle);
            _ceramicBearingAtTurbine?.SetActive(bearingRepaired);
            _fieldSleeveRepairCollar?.SetActive(sleeveRepaired);
            _slackDriveBelt?.SetActive(!repaired);
            _tautDriveBelt?.SetActive(repaired);

            if (_repairStatusText != null)
            {
                _repairStatusText.text = bearingRepaired
                    ? "CERAMIC BEARING\nSEATED AT TURBINE"
                    : sleeveRepaired
                        ? "FIELD SLEEVE\nCONSUMED IN REPAIR"
                        : repairCargoOnVehicle
                            ? "EMPTY TURBINE SOCKET\nSEAT " +
                              (cargoKind == RepairCargoKind.CeramicBearing
                                  ? "CERAMIC BEARING"
                                  : "FIELD SLEEVE")
                            : "TURBINE STOPPED\nREPAIR CARGO ABSENT";
                _repairStatusText.color = repaired
                    ? new Color(0.86f, 0.76f, 0.52f, 1f)
                    : new Color(0.78f, 0.33f, 0.24f, 1f);
            }

            if (_turbineStatusLight != null)
            {
                _turbineStatusLight.color = repaired
                    ? new Color(1f, 0.72f, 0.38f, 1f)
                    : new Color(0.66f, 0.18f, 0.12f, 1f);
                _turbineStatusLight.intensity = repaired ? 760f : 230f;
            }

            PositionRepairWorkers(repaired);
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

            if (_visibleTurbineCondition != TurbineCondition.Failing &&
                _civicTurbineRotor != null)
            {
                _civicTurbineRotor.Rotate(
                    Vector3.forward,
                    118f * Time.deltaTime,
                    Space.Self);
            }
        }

        private void BuildTurbineRepairStation(
            Material concrete,
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            if (FixedTurbineRepairSocket == null)
            {
                throw new System.InvalidOperationException(
                    "Pump-hall turbine repair socket is unavailable.");
            }

            CreatePart(
                "SCOUT_TO_TURBINE_SERVICE_PLATE",
                PrimitiveType.Cube,
                new Vector3(-5.9f, 0.02f, -2.25f),
                new Vector3(7.2f, 0.12f, 1.5f),
                darkIron).transform.localRotation = Quaternion.Euler(0f, 28f, 0f);
            CreatePart(
                "SERVICE_ACCESS_SAFE_LINE",
                PrimitiveType.Cube,
                new Vector3(-5.55f, 0.1f, -1.65f),
                new Vector3(6.8f, 0.08f, 0.12f),
                bone).transform.localRotation = Quaternion.Euler(0f, 28f, 0f);
            CreatePart(
                "CIVIC_TURBINE_PLINTH",
                PrimitiveType.Cube,
                new Vector3(0f, 0.48f, 0f),
                new Vector3(3.6f, 0.96f, 3.1f),
                concrete,
                FixedTurbineRepairSocket);
            CreatePart(
                "CIVIC_TURBINE_HOUSING",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.75f, 0f),
                new Vector3(1.35f, 1.5f, 1.35f),
                oxide,
                FixedTurbineRepairSocket).transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f);
            _civicTurbineRotor = CreatePart(
                "CIVIC_TURBINE_ROTOR",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.75f, -1.48f),
                new Vector3(0.92f, 0.22f, 0.92f),
                darkIron,
                FixedTurbineRepairSocket).transform;
            _civicTurbineRotor.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _slackDriveBelt = CreatePart(
                "SLACK_TURBINE_DRIVE_BELT",
                PrimitiveType.Cube,
                new Vector3(0f, 2.45f, -1.7f),
                new Vector3(0.22f, 2.15f, 0.16f),
                bone,
                FixedTurbineRepairSocket);
            _slackDriveBelt.transform.localRotation =
                Quaternion.Euler(0f, 0f, 14f);
            _tautDriveBelt = CreatePart(
                "TAUT_TURBINE_DRIVE_BELT",
                PrimitiveType.Cube,
                new Vector3(0f, 2.45f, -1.7f),
                new Vector3(0.22f, 2.15f, 0.16f),
                tungsten,
                FixedTurbineRepairSocket);

            _turbineRepairTarget = new GameObject(
                "EMPTY_KEYED_TURBINE_REPAIR_TARGET");
            _turbineRepairTarget.transform.SetParent(
                FixedTurbineRepairSocket,
                false);
            CreatePart(
                "TARGET_KEY_LEFT",
                PrimitiveType.Cube,
                new Vector3(-0.58f, 1.75f, -1.75f),
                new Vector3(0.12f, 1.45f, 0.12f),
                signal,
                _turbineRepairTarget.transform);
            CreatePart(
                "TARGET_KEY_RIGHT",
                PrimitiveType.Cube,
                new Vector3(0.58f, 1.75f, -1.75f),
                new Vector3(0.12f, 1.45f, 0.12f),
                signal,
                _turbineRepairTarget.transform);
            CreatePart(
                "TARGET_KEY_TOP",
                PrimitiveType.Cube,
                new Vector3(0f, 2.42f, -1.75f),
                new Vector3(1.28f, 0.12f, 0.12f),
                signal,
                _turbineRepairTarget.transform);

            _ceramicBearingAtTurbine = new GameObject(
                "CERAMIC_BEARING_AT_TURBINE");
            _ceramicBearingAtTurbine.transform.SetParent(
                FixedTurbineRepairSocket,
                false);
            CreatePart(
                "SEATED_CERAMIC_BEARING_OUTER",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.75f, -1.78f),
                new Vector3(0.72f, 0.16f, 0.72f),
                bone,
                _ceramicBearingAtTurbine.transform).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            CreatePart(
                "SEATED_CERAMIC_BEARING_HUB",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.75f, -1.97f),
                new Vector3(0.32f, 0.08f, 0.32f),
                darkIron,
                _ceramicBearingAtTurbine.transform).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);

            _fieldSleeveRepairCollar = new GameObject(
                "FIELD_SLEEVE_CONSUMED_REPAIR_COLLAR");
            _fieldSleeveRepairCollar.transform.SetParent(
                FixedTurbineRepairSocket,
                false);
            CreatePart(
                "FIELD_REPAIR_COLLAR_TOP",
                PrimitiveType.Cube,
                new Vector3(0f, 2.05f, -1.78f),
                new Vector3(1.3f, 0.28f, 0.28f),
                oxide,
                _fieldSleeveRepairCollar.transform);
            CreatePart(
                "FIELD_REPAIR_COLLAR_BOTTOM",
                PrimitiveType.Cube,
                new Vector3(0f, 1.45f, -1.78f),
                new Vector3(1.3f, 0.28f, 0.28f),
                oxide,
                _fieldSleeveRepairCollar.transform);

            var status = new GameObject("TURBINE_REPAIR_OUTCOME_TEXT");
            status.transform.SetParent(transform, false);
            status.transform.localPosition = new Vector3(-4.75f, 3.95f, -4.58f);
            status.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            _repairStatusText = status.AddComponent<TextMesh>();
            _repairStatusText.anchor = TextAnchor.MiddleLeft;
            _repairStatusText.alignment = TextAlignment.Left;
            _repairStatusText.characterSize = 0.18f;
            _repairStatusText.fontSize = 42;

            _turbineStatusLight = CreatePracticalLight(
                "TURBINE_REPAIR_TUNGSTEN_PRACTICAL",
                new Vector3(-2.45f, 4.9f, -0.8f),
                new Color(0.66f, 0.18f, 0.12f, 1f),
                230f,
                10f);
        }

        private void PositionRepairWorkers(bool repaired)
        {
            if (_humanWorker != null)
            {
                _humanWorker.transform.localPosition = repaired
                    ? new Vector3(-3.85f, 0f, 1.2f)
                    : new Vector3(-1.3f, 0f, 1.7f);
            }

            if (_robotWorker != null)
            {
                _robotWorker.transform.localPosition = repaired
                    ? new Vector3(-1.05f, 0f, 2.25f)
                    : new Vector3(0.7f, 0f, 1.7f);
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
