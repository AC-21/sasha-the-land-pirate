#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Fixed C0 homecoming apron around Sasha's canonical home vehicle pose.
    /// It presents only canonical-derived check-in readiness and composition
    /// visibility. It owns no input, command, core, save, cargo, proximity,
    /// physics, or camera-control authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingReturnServiceView : MonoBehaviour
    {
        public const string DirectionPackageId = "C0-VGR-11";
        public const string Revision = "R1";
        public const string ContentId = "poi_home_return_service_apron_a";
        public const string RootName =
            "poi_home_return_service_apron_a [C0-VGR-11 R1]";

        public const string CameraAnchorName =
            "ANCHOR_RETURN_SERVICE_CAMERA";
        public const string FocusAnchorName =
            "ANCHOR_RETURN_SERVICE_FOCUS";
        public const string VehicleAnchorName =
            "ANCHOR_RETURN_SERVICE_VEHICLE";
        public const string CheckInAnchorName =
            "ANCHOR_RETURN_CHECK_IN";
        public const string PumpHallApproachAnchorName =
            "ANCHOR_PUMP_HALL_APPROACH";
        public const string ExitAnchorName =
            "ANCHOR_RETURN_ROUTE_EXIT";

        private GameObject? _checkInMarker;
        private GameObject? _pumpHallApproach;
        private GameObject? _exitRoute;
        private GameObject? _humanWorker;
        private GameObject? _robotWorker;
        private Light? _checkInLight;
        private bool _built;

        public Transform? CameraAnchor { get; private set; }

        public Transform? FocusAnchor { get; private set; }

        public Transform? VehicleAnchor { get; private set; }

        public Transform? CheckInAnchor { get; private set; }

        public Transform? PumpHallApproachAnchor { get; private set; }

        public Transform? ExitAnchor { get; private set; }

        public bool IsBuilt => _built;

        public bool IsCheckInReady { get; private set; }

        public bool HasVehicleRepairCargo { get; private set; }

        public RepairCargoKind CargoKind { get; private set; }

        public RepairCargoCustody CargoCustody { get; private set; }

        public bool IsCheckInMarkerVisible => IsActive(_checkInMarker);

        public bool IsHumanWorkerVisible => IsActive(_humanWorker);

        public bool IsRobotWorkerVisible => IsActive(_robotWorker);

        public bool IsPumpHallApproachVisible => IsActive(_pumpHallApproach);

        public bool IsExitRouteVisible => IsActive(_exitRoute);

        public void Build(
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

            concrete = RequireMaterial(concrete, nameof(concrete));
            darkIron = RequireMaterial(darkIron, nameof(darkIron));
            oxide = RequireMaterial(oxide, nameof(oxide));
            bone = RequireMaterial(bone, nameof(bone));
            tungsten = RequireMaterial(tungsten, nameof(tungsten));
            signal = RequireMaterial(signal, nameof(signal));

            _built = true;
            gameObject.name = RootName;
            transform.position = LastBearingVehicleView.HomePosition;
            transform.rotation = Quaternion.identity;

            VehicleAnchor = CreateAnchor(VehicleAnchorName, Vector3.zero);
            FocusAnchor = CreateAnchor(
                FocusAnchorName,
                new Vector3(1.8f, 1.45f, 1.5f));
            CameraAnchor = CreateAnchor(
                CameraAnchorName,
                new Vector3(11.4f, 8.2f, 10.8f));
            CheckInAnchor = CreateAnchor(
                CheckInAnchorName,
                new Vector3(3.35f, 0f, -0.45f));
            PumpHallApproachAnchor = CreateAnchor(
                PumpHallApproachAnchorName,
                new Vector3(7.15f, 0f, 4.55f));
            ExitAnchor = CreateAnchor(
                ExitAnchorName,
                new Vector3(-1.3f, 0f, 6.15f));

            CreatePrimitive(
                "Return Service Apron",
                PrimitiveType.Cube,
                transform,
                new Vector3(0.3f, -0.13f, 0.35f),
                new Vector3(8.8f, 0.26f, 8.6f),
                concrete,
                Quaternion.identity);
            CreatePrimitive(
                "Scout Service Channel",
                PrimitiveType.Cube,
                transform,
                new Vector3(0f, 0.02f, 0.25f),
                new Vector3(1.2f, 0.06f, 6.5f),
                darkIron,
                Quaternion.identity);
            CreatePrimitive(
                "Return Apron Safe Line Left",
                PrimitiveType.Cube,
                transform,
                new Vector3(-2.75f, 0.035f, 0.45f),
                new Vector3(0.12f, 0.07f, 7.1f),
                bone,
                Quaternion.identity);
            CreatePrimitive(
                "Return Apron Safe Line Right",
                PrimitiveType.Cube,
                transform,
                new Vector3(2.75f, 0.035f, 0.45f),
                new Vector3(0.12f, 0.07f, 7.1f),
                bone,
                Quaternion.identity);
            CreatePrimitive(
                "Scout Front Wheel Stop",
                PrimitiveType.Cube,
                transform,
                new Vector3(0f, 0.18f, 3.25f),
                new Vector3(3.7f, 0.36f, 0.3f),
                oxide,
                Quaternion.identity);

            BuildCheckInMarker(darkIron, oxide, bone, tungsten, signal);
            BuildPumpHallApproach(concrete, darkIron, oxide, bone, tungsten);
            BuildExitRoute(darkIron, oxide, bone);
            _humanWorker = BuildHumanWorker(oxide, bone);
            _robotWorker = BuildRobotWorker(darkIron, concrete, oxide);

            Apply(
                checkInReady: false,
                RepairCargoKind.None,
                RepairCargoCustody.None,
                humanVisible: false,
                robotVisible: false);
        }

        public void Apply(
            bool checkInReady,
            RepairCargoKind repairCargoKind,
            RepairCargoCustody repairCargoCustody,
            bool humanVisible,
            bool robotVisible)
        {
            if (!_built)
            {
                throw new InvalidOperationException(
                    "Return service view must be built before state is applied.");
            }

            ValidateRepairCargo(repairCargoKind, repairCargoCustody);
            bool hasVehicleRepairCargo =
                (repairCargoKind == RepairCargoKind.CeramicBearing ||
                 repairCargoKind == RepairCargoKind.FieldSleeve) &&
                repairCargoCustody == RepairCargoCustody.Vehicle;
            if (checkInReady && !hasVehicleRepairCargo)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_RETURN_SERVICE_READINESS_INVALID");
            }

            IsCheckInReady = checkInReady;
            HasVehicleRepairCargo = hasVehicleRepairCargo;
            CargoKind = repairCargoKind;
            CargoCustody = repairCargoCustody;

            _checkInMarker?.SetActive(checkInReady);
            _humanWorker?.SetActive(humanVisible);
            _robotWorker?.SetActive(robotVisible);
            if (_checkInLight != null)
            {
                _checkInLight.intensity = checkInReady ? 520f : 0f;
            }
        }

        private void BuildCheckInMarker(
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            if (CheckInAnchor == null)
            {
                throw new InvalidOperationException(
                    "Return service check-in anchor is unavailable.");
            }

            CreatePrimitive(
                "Return Check-In Plinth",
                PrimitiveType.Cube,
                CheckInAnchor,
                new Vector3(0f, 0.38f, 0f),
                new Vector3(1.8f, 0.76f, 1.45f),
                darkIron,
                Quaternion.identity);
            CreatePrimitive(
                "Physical Return Ledger",
                PrimitiveType.Cube,
                CheckInAnchor,
                new Vector3(0f, 1.02f, -0.24f),
                new Vector3(1.2f, 0.72f, 0.12f),
                bone,
                Quaternion.Euler(18f, 0f, 0f));
            CreatePrimitive(
                "Return Ledger Brace",
                PrimitiveType.Cube,
                CheckInAnchor,
                new Vector3(0f, 0.94f, 0.12f),
                new Vector3(0.18f, 1.25f, 0.18f),
                oxide,
                Quaternion.identity);

            _checkInMarker = new GameObject(
                "Canonical Return Check-In Ready Marker");
            _checkInMarker.transform.SetParent(CheckInAnchor, false);
            CreatePrimitive(
                "Raised Tungsten Check-In Bar",
                PrimitiveType.Cube,
                _checkInMarker.transform,
                new Vector3(0f, 1.58f, 0f),
                new Vector3(1.55f, 0.18f, 0.18f),
                tungsten,
                Quaternion.identity);
            CreatePrimitive(
                "Mechanical Check-In Pin",
                PrimitiveType.Cylinder,
                _checkInMarker.transform,
                new Vector3(0.58f, 1.26f, -0.14f),
                new Vector3(0.18f, 0.1f, 0.18f),
                signal,
                Quaternion.Euler(90f, 0f, 0f));

            var lightObject = new GameObject(
                "Return Check-In Tungsten Practical");
            lightObject.transform.SetParent(_checkInMarker.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            _checkInLight = lightObject.AddComponent<Light>();
            _checkInLight.type = LightType.Point;
            _checkInLight.color = tungsten.color;
            _checkInLight.range = 7f;
            _checkInLight.shadows = LightShadows.None;
        }

        private void BuildPumpHallApproach(
            Material concrete,
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten)
        {
            _pumpHallApproach = new GameObject(
                "Physical Pump Hall Service Approach");
            _pumpHallApproach.transform.SetParent(transform, false);

            CreatePrimitive(
                "Pump Hall Approach Plate A",
                PrimitiveType.Cube,
                _pumpHallApproach.transform,
                new Vector3(3.65f, 0.01f, 2.05f),
                new Vector3(4.6f, 0.12f, 1.55f),
                darkIron,
                Quaternion.Euler(0f, 31f, 0f));
            CreatePrimitive(
                "Pump Hall Approach Plate B",
                PrimitiveType.Cube,
                _pumpHallApproach.transform,
                new Vector3(6.25f, 0.01f, 3.75f),
                new Vector3(3.4f, 0.12f, 1.55f),
                concrete,
                Quaternion.Euler(0f, 35f, 0f));
            CreatePrimitive(
                "Pump Hall Approach Bone Rail",
                PrimitiveType.Cube,
                _pumpHallApproach.transform,
                new Vector3(4.9f, 0.12f, 3.2f),
                new Vector3(0.12f, 0.12f, 6.2f),
                bone,
                Quaternion.Euler(0f, -55f, 0f));
            CreatePrimitive(
                "Pump Hall Service Arch Left",
                PrimitiveType.Cube,
                _pumpHallApproach.transform,
                new Vector3(6.45f, 1.65f, 4.75f),
                new Vector3(0.32f, 3.3f, 0.32f),
                oxide,
                Quaternion.identity);
            CreatePrimitive(
                "Pump Hall Service Arch Right",
                PrimitiveType.Cube,
                _pumpHallApproach.transform,
                new Vector3(7.85f, 1.65f, 4.75f),
                new Vector3(0.32f, 3.3f, 0.32f),
                oxide,
                Quaternion.identity);
            CreatePrimitive(
                "Pump Hall Tungsten Header",
                PrimitiveType.Cube,
                _pumpHallApproach.transform,
                new Vector3(7.15f, 3.15f, 4.75f),
                new Vector3(1.75f, 0.22f, 0.22f),
                tungsten,
                Quaternion.identity);
        }

        private void BuildExitRoute(
            Material darkIron,
            Material oxide,
            Material bone)
        {
            _exitRoute = new GameObject("Physical Return Route Exit Read");
            _exitRoute.transform.SetParent(transform, false);

            CreatePrimitive(
                "Return Route Exit Plate",
                PrimitiveType.Cube,
                _exitRoute.transform,
                new Vector3(-1.3f, 0f, 5.25f),
                new Vector3(4.4f, 0.12f, 3.2f),
                darkIron,
                Quaternion.identity);
            CreatePrimitive(
                "Return Route Exit Spine",
                PrimitiveType.Cube,
                _exitRoute.transform,
                new Vector3(-1.3f, 0.1f, 5.45f),
                new Vector3(0.18f, 0.1f, 2.2f),
                bone,
                Quaternion.identity);
            CreatePrimitive(
                "Return Route Exit Arrow Left",
                PrimitiveType.Cube,
                _exitRoute.transform,
                new Vector3(-1.65f, 0.1f, 6.25f),
                new Vector3(0.16f, 0.1f, 0.9f),
                oxide,
                Quaternion.Euler(0f, 45f, 0f));
            CreatePrimitive(
                "Return Route Exit Arrow Right",
                PrimitiveType.Cube,
                _exitRoute.transform,
                new Vector3(-0.95f, 0.1f, 6.25f),
                new Vector3(0.16f, 0.1f, 0.9f),
                oxide,
                Quaternion.Euler(0f, -45f, 0f));
        }

        private GameObject BuildHumanWorker(Material workwear, Material bone)
        {
            var worker = new GameObject("RETURN_SERVICE_WORKER_HUMAN");
            worker.transform.SetParent(transform, false);
            worker.transform.localPosition = new Vector3(2.45f, 0f, -1.75f);
            CreatePrimitive(
                "Human Return Workwear",
                PrimitiveType.Cylinder,
                worker.transform,
                new Vector3(0f, 0.82f, 0f),
                new Vector3(0.43f, 0.68f, 0.43f),
                workwear,
                Quaternion.identity);
            CreatePrimitive(
                "Human Return Service Hood",
                PrimitiveType.Cylinder,
                worker.transform,
                new Vector3(0f, 1.66f, 0f),
                new Vector3(0.38f, 0.28f, 0.38f),
                bone,
                Quaternion.identity);
            return worker;
        }

        private GameObject BuildRobotWorker(
            Material darkIron,
            Material concrete,
            Material token)
        {
            var worker = new GameObject("RETURN_SERVICE_WORKER_ROBOT");
            worker.transform.SetParent(transform, false);
            worker.transform.localPosition = new Vector3(4.35f, 0f, -1.55f);
            CreatePrimitive(
                "Robot Return Service Torso",
                PrimitiveType.Cube,
                worker.transform,
                new Vector3(0f, 0.9f, 0f),
                new Vector3(0.86f, 1.18f, 0.64f),
                darkIron,
                Quaternion.identity);
            CreatePrimitive(
                "Robot Return Service Head",
                PrimitiveType.Cylinder,
                worker.transform,
                new Vector3(0f, 1.74f, 0f),
                new Vector3(0.34f, 0.34f, 0.34f),
                concrete,
                Quaternion.identity);
            CreatePrimitive(
                "Robot Return Service Token",
                PrimitiveType.Cube,
                worker.transform,
                new Vector3(0.46f, 1.08f, 0f),
                new Vector3(0.12f, 0.42f, 0.24f),
                token,
                Quaternion.identity);
            return worker;
        }

        private Transform CreateAnchor(string name, Vector3 localPosition)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = localPosition;
            return anchor;
        }

        private static Material RequireMaterial(Material material, string name)
        {
            return material ?? throw new ArgumentNullException(name);
        }

        private static void ValidateRepairCargo(
            RepairCargoKind kind,
            RepairCargoCustody custody)
        {
            if (!Enum.IsDefined(typeof(RepairCargoKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (!Enum.IsDefined(typeof(RepairCargoCustody), custody))
            {
                throw new ArgumentOutOfRangeException(nameof(custody));
            }

            bool valid = kind switch
            {
                RepairCargoKind.None => custody == RepairCargoCustody.None,
                RepairCargoKind.CeramicBearing =>
                    custody == RepairCargoCustody.Depot ||
                    custody == RepairCargoCustody.Faction ||
                    custody == RepairCargoCustody.Vehicle ||
                    custody == RepairCargoCustody.Turbine,
                RepairCargoKind.FieldSleeve =>
                    custody == RepairCargoCustody.Faction ||
                    custody == RepairCargoCustody.Vehicle ||
                    custody == RepairCargoCustody.Consumed,
                _ => false,
            };
            if (!valid)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_RETURN_SERVICE_CARGO_INVALID");
            }
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

        private static bool IsActive(GameObject? value)
        {
            return value != null && value.activeSelf;
        }
    }
}
