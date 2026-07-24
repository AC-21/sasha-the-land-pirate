#nullable enable

using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// One fixed, derived-only machine-shop and claims-wicket dollhouse.
    /// It visualizes the bounded VGR-05 batch and custody seam without owning
    /// input, canonical state, physics, saving, or a general production UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingOneGoodBatchCutawayView : MonoBehaviour
    {
        public const string DirectionPackageId = "C3-VGR-05-CANDIDATE";
        public const string Revision = "R0";
        public const string ContentId = "bld_machine_shop_claims_wicket_a";
        public const string RootName =
            "bld_machine_shop_claims_wicket_a [VGR-05 Blockout]";

        public const string CameraAnchorName =
            "ANCHOR_ONE_GOOD_BATCH_CAMERA";
        public const string FocusAnchorName =
            "ANCHOR_ONE_GOOD_BATCH_FOCUS";
        public const string InputAnchorName = "SOCKET_BATCH_INPUT_01";
        public const string WorkAnchorName = "SOCKET_BATCH_WORK_01";
        public const string OutputAnchorName = "SOCKET_BATCH_OUTPUT_01";
        public const string ClaimsAnchorName = "SOCKET_CLAIMS_HANDOFF_01";
        public const string PermitAnchorName = "SOCKET_ROUTE_PERMIT_01";

        public static readonly Vector3 CameraAnchorPosition =
            new Vector3(10.6f, 7.8f, 11.4f);
        public static readonly Vector3 FocusAnchorPosition =
            new Vector3(0f, 1.8f, 0f);
        public static readonly Vector3 InputAnchorPosition =
            new Vector3(-3.55f, 0f, 1.65f);
        public static readonly Vector3 WorkAnchorPosition =
            new Vector3(-1f, 0.85f, 0.25f);
        public static readonly Vector3 OutputAnchorPosition =
            new Vector3(1.45f, 0f, 0.35f);
        public static readonly Vector3 ClaimsAnchorPosition =
            new Vector3(3.55f, 0f, -1.8f);
        public static readonly Vector3 PermitAnchorPosition =
            new Vector3(3.55f, 2.85f, -4.35f);

        private static readonly Vector3 WorkerPrimaryPosition =
            new Vector3(-0.35f, 0f, 1.85f);
        private static readonly Vector3 WorkerLeftPosition =
            new Vector3(-1.25f, 0f, 1.85f);
        private static readonly Vector3 WorkerRightPosition =
            new Vector3(0.55f, 0f, 1.85f);

        private GameObject? _inputStock;
        private GameObject? _workpiece;
        private GameObject? _bearingLot;
        private GameObject? _permitLocked;
        private GameObject? _permitGranted;
        private GameObject? _twoFuelTollTerms;
        private GameObject? _humanWorker;
        private GameObject? _robotWorker;
        private Transform? _machineSpindle;
        private Light? _machineWorkLight;
        private Light? _claimsWorkLight;
        private bool _machineRunning;
        private bool _built;

        public Transform? CameraAnchor { get; private set; }

        public Transform? FocusAnchor { get; private set; }

        public Transform? InputAnchor { get; private set; }

        public Transform? WorkAnchor { get; private set; }

        public Transform? OutputAnchor { get; private set; }

        public Transform? ClaimsAnchor { get; private set; }

        public Transform? PermitAnchor { get; private set; }

        public LastBearingFuelBondInteractor? FuelBondInteractor
        {
            get;
            private set;
        }

        public Transform? MachineSpindle => _machineSpindle;

        public GameObject? BearingLot => _bearingLot;

        public bool IsDollhouseCutaway => true;

        public bool HasRoof => false;

        public bool HasNearWall => false;

        public bool IsInputStockVisible =>
            _inputStock != null && _inputStock.activeSelf;

        public bool IsWorkpieceVisible =>
            _workpiece != null && _workpiece.activeSelf;

        public bool IsBearingLotVisible =>
            _bearingLot != null && _bearingLot.activeSelf;

        public bool IsPermitLockedVisible =>
            _permitLocked != null && _permitLocked.activeSelf;

        public bool IsPermitGrantedVisible =>
            _permitGranted != null && _permitGranted.activeSelf;

        public bool IsTwoFuelTollVisible =>
            _twoFuelTollTerms != null && _twoFuelTollTerms.activeSelf;

        public bool IsHumanWorkerVisible =>
            _humanWorker != null && _humanWorker.activeSelf;

        public bool IsRobotWorkerVisible =>
            _robotWorker != null && _robotWorker.activeSelf;

        public bool IsMachineRunning => _machineRunning;

        internal void Build(
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
            transform.localPosition = Vector3.zero;

            CameraAnchor = CreateAnchor(CameraAnchorName, CameraAnchorPosition);
            FocusAnchor = CreateAnchor(FocusAnchorName, FocusAnchorPosition);
            InputAnchor = CreateAnchor(InputAnchorName, InputAnchorPosition);
            WorkAnchor = CreateAnchor(WorkAnchorName, WorkAnchorPosition);
            OutputAnchor = CreateAnchor(OutputAnchorName, OutputAnchorPosition);
            ClaimsAnchor = CreateAnchor(ClaimsAnchorName, ClaimsAnchorPosition);
            PermitAnchor = CreateAnchor(PermitAnchorName, PermitAnchorPosition);

            CreatePart(
                "WORKSHOP_FLOOR",
                PrimitiveType.Cube,
                new Vector3(0f, -0.2f, 0f),
                new Vector3(10.8f, 0.4f, 9.2f),
                concrete);
            CreatePart(
                "WORKSHOP_REAR_WORLD_BONE",
                PrimitiveType.Cube,
                new Vector3(0f, 3.35f, -4.45f),
                new Vector3(10.8f, 7.1f, 0.45f),
                concrete);
            CreatePart(
                "WORKSHOP_LEFT_IRON_WALL",
                PrimitiveType.Cube,
                new Vector3(-5.2f, 2.7f, 0f),
                new Vector3(0.42f, 5.8f, 9.2f),
                darkIron);
            CreatePart(
                "CIVIC_MACHINE_PROSCENIUM",
                PrimitiveType.Cube,
                new Vector3(-0.6f, 5.85f, -3.98f),
                new Vector3(9.2f, 0.72f, 0.8f),
                concrete);
            CreatePart(
                "PATCHED_SERVICE_STRIP",
                PrimitiveType.Cube,
                new Vector3(-2.9f, 4.95f, -4.72f),
                new Vector3(3.4f, 0.28f, 0.08f),
                bone);

            BuildOneMachine(darkIron, oxide, bone);
            BuildInputStillage(darkIron, oxide, bone);
            BuildOutputLot(darkIron, oxide, bone);
            BuildClaimsWicket(darkIron, oxide, bone, tungsten, signal);
            FuelBondInteractor =
                gameObject.AddComponent<LastBearingFuelBondInteractor>();
            FuelBondInteractor.Build(
                darkIron,
                oxide,
                bone,
                tungsten,
                signal);
            _humanWorker = CreateHumanWorker(oxide, bone);
            _robotWorker = CreateRobotWorker(darkIron, concrete, oxide);

            _machineWorkLight = CreatePracticalLight(
                "TUNGSTEN_MACHINE_TASK_LIGHT",
                new Vector3(-1.2f, 4.35f, 0.4f),
                120f,
                8f);
            _claimsWorkLight = CreatePracticalLight(
                "TUNGSTEN_CLAIMS_TASK_LIGHT",
                new Vector3(3.45f, 3.8f, -1.75f),
                70f,
                6f);

            Apply(
                batchStartAvailable: false,
                SpareBearingBatchPhase.None,
                SpareBearingLotCustody.None,
                lotQuantity: 0,
                routePermitGranted: false,
                futureRouteTollFuelUnits: 0,
                humanVisible: true,
                robotVisible: true);
        }

        public void Apply(
            bool batchStartAvailable,
            SpareBearingBatchPhase phase,
            SpareBearingLotCustody custody,
            long lotQuantity,
            bool routePermitGranted,
            long futureRouteTollFuelUnits,
            bool humanVisible,
            bool robotVisible,
            bool simulationPaused = false)
        {
            _machineRunning =
                phase == SpareBearingBatchPhase.InProgress &&
                !simulationPaused;
            _inputStock?.SetActive(
                batchStartAvailable && phase == SpareBearingBatchPhase.None);
            _workpiece?.SetActive(
                phase == SpareBearingBatchPhase.InProgress);

            Transform? custodyAnchor = custody switch
            {
                SpareBearingLotCustody.WorkshopOutput => OutputAnchor,
                SpareBearingLotCustody.LastBearingClaimsCounter => ClaimsAnchor,
                _ => null,
            };
            if (_bearingLot != null)
            {
                bool lotVisible = lotQuantity > 0 && custodyAnchor != null;
                _bearingLot.SetActive(lotVisible);
                if (lotVisible)
                {
                    _bearingLot.transform.SetParent(custodyAnchor!, false);
                    _bearingLot.transform.localPosition = Vector3.zero;
                    _bearingLot.transform.localRotation = Quaternion.identity;
                }
            }

            _permitLocked?.SetActive(!routePermitGranted);
            _permitGranted?.SetActive(routePermitGranted);
            _twoFuelTollTerms?.SetActive(
                futureRouteTollFuelUnits
                    == LastBearingBalanceV1.TakeFutureRouteTollFuelUnits);
            ApplyRoster(humanVisible, robotVisible);

            if (_machineWorkLight != null)
            {
                _machineWorkLight.intensity = phase switch
                {
                    SpareBearingBatchPhase.InProgress => 620f,
                    SpareBearingBatchPhase.Complete => 320f,
                    SpareBearingBatchPhase.Settled => 160f,
                    _ => 120f,
                };
            }

            if (_claimsWorkLight != null)
            {
                _claimsWorkLight.intensity = custody ==
                    SpareBearingLotCustody.LastBearingClaimsCounter
                        ? 420f
                        : phase == SpareBearingBatchPhase.Complete
                            ? 180f
                            : 70f;
            }
        }

        private void Update()
        {
            if (_machineRunning && _machineSpindle != null)
            {
                _machineSpindle.Rotate(
                    Vector3.right,
                    110f * Time.deltaTime,
                    Space.Self);
            }
        }

        private void BuildOneMachine(
            Material darkIron,
            Material oxide,
            Material bone)
        {
            CreatePart(
                "ONE_GOOD_BATCH_MACHINE_BED",
                PrimitiveType.Cube,
                new Vector3(-1f, 0.68f, 0.25f),
                new Vector3(4.2f, 1.18f, 1.5f),
                darkIron);
            CreatePart(
                "REPAIRED_HEADSTOCK",
                PrimitiveType.Cube,
                new Vector3(-2.25f, 1.55f, 0.25f),
                new Vector3(1.4f, 1.5f, 1.65f),
                oxide);
            _machineSpindle = CreatePart(
                "MACHINE_SPINDLE",
                PrimitiveType.Cylinder,
                new Vector3(-1.25f, 1.6f, 0.25f),
                new Vector3(0.52f, 0.72f, 0.52f),
                darkIron).transform;
            _machineSpindle.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreatePart(
                "TOOL_SLIDE",
                PrimitiveType.Cube,
                new Vector3(0.15f, 1.3f, 0.25f),
                new Vector3(1.15f, 0.34f, 1.05f),
                bone);
            CreatePart(
                "REPAIRED_HANDWHEEL",
                PrimitiveType.Cylinder,
                new Vector3(0.65f, 1.55f, -0.62f),
                new Vector3(0.42f, 0.09f, 0.42f),
                oxide).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _workpiece = new GameObject("SPARE_BEARING_WORKPIECE");
            _workpiece.transform.SetParent(WorkAnchor, false);
            CreatePart(
                "ACTIVE_BEARING_BLANK",
                PrimitiveType.Cylinder,
                Vector3.zero,
                new Vector3(0.38f, 0.22f, 0.38f),
                bone,
                _workpiece.transform).transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f);
        }

        private void BuildInputStillage(
            Material darkIron,
            Material oxide,
            Material bone)
        {
            _inputStock = new GameObject("BOUNDED_BATCH_INPUT_STILLAGE");
            _inputStock.transform.SetParent(InputAnchor, false);
            CreatePart(
                "INPUT_STILLAGE",
                PrimitiveType.Cube,
                new Vector3(0f, 0.34f, 0f),
                new Vector3(2.15f, 0.68f, 1.55f),
                oxide,
                _inputStock.transform);
            CreatePart(
                "PHYSICAL_INPUT_PART_01",
                PrimitiveType.Cube,
                new Vector3(-0.43f, 0.88f, 0f),
                new Vector3(0.62f, 0.48f, 1.05f),
                darkIron,
                _inputStock.transform);
            CreatePart(
                "PHYSICAL_INPUT_PART_02",
                PrimitiveType.Cube,
                new Vector3(0.43f, 0.88f, 0f),
                new Vector3(0.62f, 0.48f, 1.05f),
                darkIron,
                _inputStock.transform);
            CreatePart(
                "ONE_BATCH_INPUT_TAG",
                PrimitiveType.Cube,
                new Vector3(0f, 1.2f, -0.56f),
                new Vector3(0.85f, 0.24f, 0.06f),
                bone,
                _inputStock.transform);
        }

        private void BuildOutputLot(
            Material darkIron,
            Material oxide,
            Material bone)
        {
            CreatePart(
                "WORKSHOP_OUTPUT_CRADLE",
                PrimitiveType.Cube,
                new Vector3(1.45f, 0.34f, 0.35f),
                new Vector3(2.15f, 0.68f, 1.6f),
                oxide);
            _bearingLot = new GameObject("LOT_SPARE_BEARING_ONE_GOOD_BATCH");
            _bearingLot.transform.SetParent(OutputAnchor, false);
            CreatePart(
                "PHYSICAL_SPARE_BEARING_LOT",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.82f, 0f),
                new Vector3(0.68f, 0.28f, 0.68f),
                darkIron,
                _bearingLot.transform).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            CreatePart(
                "STABLE_LOT_TAG",
                PrimitiveType.Cube,
                new Vector3(0.72f, 0.92f, -0.28f),
                new Vector3(0.46f, 0.28f, 0.08f),
                bone,
                _bearingLot.transform);
        }

        private void BuildClaimsWicket(
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            CreatePart(
                "CLAIMS_COUNTER",
                PrimitiveType.Cube,
                new Vector3(3.55f, 0.78f, -1.8f),
                new Vector3(2.8f, 1.55f, 1.05f),
                oxide);
            CreatePart(
                "CUSTODY_GRATING",
                PrimitiveType.Cube,
                new Vector3(4.62f, 2.05f, -1.8f),
                new Vector3(0.12f, 2.5f, 2.8f),
                darkIron);
            CreatePart(
                "PHYSICAL_CLAIMS_LEDGER",
                PrimitiveType.Cube,
                new Vector3(3.2f, 1.62f, -1.8f),
                new Vector3(0.95f, 0.14f, 0.7f),
                bone);
            CreatePart(
                "CLAIMS_HANDOFF_SCALE",
                PrimitiveType.Cylinder,
                new Vector3(3.55f, 1.68f, -1.8f),
                new Vector3(0.68f, 0.12f, 0.68f),
                darkIron);
            CreatePart(
                "FIXED_DEPOT_ROUTE_BOARD",
                PrimitiveType.Cube,
                PermitAnchorPosition,
                new Vector3(2.45f, 1.35f, 0.14f),
                darkIron);
            _twoFuelTollTerms = new GameObject(
                "CANONICAL_FUTURE_TOLL_TERMS");
            _twoFuelTollTerms.transform.SetParent(transform, false);
            CreatePart(
                "PERSISTENT_TWO_FUEL_TOLL_TERMS",
                PrimitiveType.Cube,
                PermitAnchorPosition + new Vector3(0f, -0.48f, -0.12f),
                new Vector3(1.65f, 0.22f, 0.12f),
                bone,
                _twoFuelTollTerms.transform);
            CreatePart(
                "FUTURE_TOLL_FUEL_UNIT_01",
                PrimitiveType.Cylinder,
                PermitAnchorPosition + new Vector3(-0.42f, -0.18f, -0.18f),
                new Vector3(0.22f, 0.09f, 0.22f),
                bone,
                _twoFuelTollTerms.transform).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            CreatePart(
                "FUTURE_TOLL_FUEL_UNIT_02",
                PrimitiveType.Cylinder,
                PermitAnchorPosition + new Vector3(0.42f, -0.18f, -0.18f),
                new Vector3(0.22f, 0.09f, 0.22f),
                bone,
                _twoFuelTollTerms.transform).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);

            _permitLocked = new GameObject("DEPOT_ROUTE_PERMIT_LOCKED");
            _permitLocked.transform.SetParent(PermitAnchor, false);
            CreatePart(
                "LOCKED_HORIZONTAL_CROSSBAR",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(1.85f, 0.18f, 0.12f),
                oxide,
                _permitLocked.transform);

            _permitGranted = new GameObject("DEPOT_ROUTE_PERMIT_GRANTED");
            _permitGranted.transform.SetParent(PermitAnchor, false);
            CreatePart(
                "RAISED_PERMIT_ARM",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(0.18f, 1.65f, 0.12f),
                tungsten,
                _permitGranted.transform);
            CreatePart(
                "PHYSICAL_ROUTE_PERMIT",
                PrimitiveType.Cube,
                new Vector3(0.58f, 0f, -0.08f),
                new Vector3(0.82f, 0.52f, 0.08f),
                bone,
                _permitGranted.transform);
            CreatePart(
                "RARE_LEGACY_ROUTE_SIGNAL",
                PrimitiveType.Cylinder,
                new Vector3(-0.65f, 0.42f, -0.1f),
                new Vector3(0.22f, 0.08f, 0.22f),
                signal,
                _permitGranted.transform).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
        }

        private GameObject CreateHumanWorker(Material workwear, Material cloth)
        {
            var worker = new GameObject("BATCH_WORKER_HUMAN");
            worker.transform.SetParent(transform, false);
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
            var worker = new GameObject("BATCH_WORKER_UTILITY_ROBOT");
            worker.transform.SetParent(transform, false);
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

        private void ApplyRoster(bool humanVisible, bool robotVisible)
        {
            if (_humanWorker != null)
            {
                _humanWorker.SetActive(humanVisible);
                _humanWorker.transform.localPosition =
                    humanVisible && robotVisible
                        ? WorkerLeftPosition
                        : WorkerPrimaryPosition;
            }

            if (_robotWorker != null)
            {
                _robotWorker.SetActive(robotVisible);
                _robotWorker.transform.localPosition =
                    humanVisible && robotVisible
                        ? WorkerRightPosition
                        : WorkerPrimaryPosition;
            }
        }

        private Transform CreateAnchor(string anchorName, Vector3 position)
        {
            var anchor = new GameObject(anchorName).transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = position;
            return anchor;
        }

        private GameObject CreatePart(
            string partName,
            PrimitiveType primitiveType,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform? parent = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(parent ?? transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return part;
        }

        private Light CreatePracticalLight(
            string lightName,
            Vector3 position,
            float intensity,
            float range)
        {
            var lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.38f, 1f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
