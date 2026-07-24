#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Physics-free presentation of the authoritative working service cell.
    /// Placement previews are local and disappear when a core command lands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingCityServiceCellView : MonoBehaviour
    {
        private static readonly Vector3[] PadPositions =
        {
            new Vector3(-2.8f, 0.65f, -0.55f),
            new Vector3(-1.4f, 0.65f, 0.35f),
            new Vector3(0f, 0.65f, -0.55f),
            new Vector3(1.4f, 0.65f, 0.35f),
            new Vector3(2.8f, 0.65f, -0.55f)
        };

        private static readonly Vector3 ServiceBuildingScale =
            new Vector3(1.3f, 1.2f, 1.4f);

        private Transform? _recycler;
        private Transform? _machineShop;
        private Transform? _emergencyStorage;
        private GameObject? _emergencyCisternFill;
        private GameObject? _link;
        private Transform? _sled;
        private GameObject? _humanOperator;
        private GameObject? _robotOperator;
        private Transform? _hotShiftSpindle;
        private GameObject? _hotShiftWorkPool;
        private GameObject? _workshopPushTransferArm;
        private GameObject? _dustFrontShutter;
        private GameObject? _completionWitnessA;
        private GameObject? _completionWitnessB;
        private float _hotShiftSledProgress = 1f;
        private bool _built;

        public LastBearingCityServiceCellInteractor? Interactor
        {
            get;
            private set;
        }

        public bool IsVisible => gameObject.activeSelf;

        public bool IsRecyclerVisible =>
            _recycler?.gameObject.activeSelf == true;

        public bool IsMachineShopVisible =>
            _machineShop?.gameObject.activeSelf == true;

        public bool IsEmergencyStorageVisible =>
            _emergencyStorage?.gameObject.activeSelf == true;

        public bool IsEmergencyCisternFillVisible =>
            _emergencyCisternFill?.activeInHierarchy == true;

        public bool IsLinkVisible => _link?.activeSelf == true;

        public bool IsSledVisible => _sled?.gameObject.activeSelf == true;

        public bool IsHumanOperatorVisible =>
            _humanOperator?.activeSelf == true;

        public bool IsRobotOperatorVisible =>
            _robotOperator?.activeSelf == true;

        public bool IsHotShiftSpindleMoving =>
            _hotShiftSpindle?.gameObject.activeInHierarchy == true;

        public bool IsHotShiftWorkPoolVisible =>
            _hotShiftWorkPool?.activeInHierarchy == true;

        public bool IsWorkshopPushTransferArmVisible =>
            _workshopPushTransferArm?.activeInHierarchy == true;

        public bool IsDustFrontShutterVisible =>
            _dustFrontShutter?.activeInHierarchy == true;

        public bool IsHotShiftCompletionWitnessVisible =>
            _completionWitnessA?.activeInHierarchy == true &&
            _completionWitnessB?.activeInHierarchy == true;

        public float HotShiftSledProgress => _hotShiftSledProgress;

        public Quaternion HotShiftSpindleLocalRotation =>
            _hotShiftSpindle?.localRotation ?? Quaternion.identity;

        internal void Build(
            Material concrete,
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
            transform.localPosition = new Vector3(15f, 0.15f, -8f);
            for (var index = 0; index < PadPositions.Length; index++)
            {
                CreateBlock(
                    "Working Cell Pad " + (index + 1).ToString("00"),
                    transform,
                    new Vector3(
                        PadPositions[index].x,
                        0.05f,
                        PadPositions[index].z),
                    new Vector3(1.35f, 0.1f, 1.55f),
                    bone);
            }

            _recycler = CreateBlock(
                "Canonical Recycler",
                transform,
                Vector3.zero,
                ServiceBuildingScale,
                oxide).transform;
            _machineShop = CreateBlock(
                "Canonical Machine Shop",
                transform,
                Vector3.zero,
                ServiceBuildingScale,
                iron).transform;
            _emergencyStorage = CreateBlock(
                "Canonical Emergency Storage",
                transform,
                Vector3.zero,
                new Vector3(1.25f, 0.9f, 1.25f),
                concrete).transform;
            _emergencyCisternFill = CreateBlock(
                "Emergency Cistern Full Marker",
                _emergencyStorage,
                new Vector3(0f, 0.46f, 0f),
                new Vector3(0.86f, 0.08f, 0.86f),
                bone);
            _link = CreateBlock(
                "Canonical Recycler Workshop Link",
                transform,
                Vector3.zero,
                new Vector3(1f, 0.12f, 0.55f),
                concrete);
            _sled = CreateBlock(
                "Canonical Parts Sled",
                transform,
                Vector3.zero,
                new Vector3(0.48f, 0.38f, 0.38f),
                bone).transform;
            _humanOperator = CreateBlock(
                "Human Machine Shop Operator",
                transform,
                Vector3.zero,
                new Vector3(0.28f, 0.8f, 0.28f),
                oxide);
            _robotOperator = CreateBlock(
                "Utility Robot Machine Shop Operator",
                transform,
                Vector3.zero,
                new Vector3(0.34f, 0.7f, 0.34f),
                iron);
            _hotShiftSpindle = CreateBlock(
                "Hot Shift Machine Spindle",
                _machineShop,
                new Vector3(0f, 0.58f, -0.34f),
                new Vector3(0.42f, 0.18f, 0.18f),
                iron).transform;
            _hotShiftWorkPool = CreateBlock(
                "Hot Shift Tungsten Work Pool",
                _machineShop,
                new Vector3(0f, 0.66f, 0.22f),
                new Vector3(0.72f, 0.06f, 0.48f),
                tungsten);
            _workshopPushTransferArm = CreateBlock(
                "Workshop Push Garageward Transfer Arm",
                _machineShop,
                new Vector3(0.52f, 0.46f, 0f),
                new Vector3(0.8f, 0.12f, 0.12f),
                bone);
            _dustFrontShutter = CreateBlock(
                "Dust Front Physical Safety Shutter",
                _machineShop,
                new Vector3(0f, 0.48f, -0.48f),
                new Vector3(0.9f, 0.8f, 0.08f),
                oxide);
            _completionWitnessA = CreateBlock(
                "Hot Shift Output Witness Notch 01",
                _machineShop,
                new Vector3(-0.18f, 0.72f, 0.4f),
                new Vector3(0.12f, 0.18f, 0.12f),
                bone);
            _completionWitnessB = CreateBlock(
                "Hot Shift Output Witness Notch 02",
                _machineShop,
                new Vector3(0.18f, 0.72f, 0.4f),
                new Vector3(0.12f, 0.18f, 0.12f),
                bone);
            Interactor =
                gameObject.AddComponent<LastBearingCityServiceCellInteractor>();
            Interactor.Build(
                iron,
                oxide,
                bone,
                tungsten,
                signal);
            gameObject.SetActive(false);
        }

        public void Apply(
            LastBearingReadModel model,
            CityBuildingKind previewBuilding,
            int previewPadIndex,
            int previewQuarterTurns,
            bool previewActive)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (!_built ||
                _recycler == null ||
                _machineShop == null ||
                _emergencyStorage == null ||
                _emergencyCisternFill == null ||
                _link == null ||
                _sled == null ||
                _humanOperator == null ||
                _robotOperator == null ||
                _hotShiftSpindle == null ||
                _hotShiftWorkPool == null ||
                _workshopPushTransferArm == null ||
                _dustFrontShutter == null ||
                _completionWitnessA == null ||
                _completionWitnessB == null)
            {
                return;
            }

            gameObject.SetActive(true);
            ApplyBuilding(
                _recycler,
                CityBuildingKind.Recycler,
                model.RecyclerPadIndex,
                model.RecyclerQuarterTurns,
                previewBuilding,
                previewPadIndex,
                previewQuarterTurns,
                previewActive);
            ApplyBuilding(
                _machineShop,
                CityBuildingKind.MachineShop,
                model.MachineShopPadIndex,
                model.MachineShopQuarterTurns,
                previewBuilding,
                previewPadIndex,
                previewQuarterTurns,
                previewActive);
            ApplyBuilding(
                _emergencyStorage,
                CityBuildingKind.EmergencyStorage,
                model.EmergencyStoragePadIndex,
                model.EmergencyStorageQuarterTurns,
                previewBuilding,
                previewPadIndex,
                previewQuarterTurns,
                previewActive);
            _emergencyCisternFill.SetActive(
                model.EmergencyCisternCharged &&
                _emergencyStorage.gameObject.activeSelf);

            bool linked = model.CityServiceLinkConnected &&
                IsValidPad(model.RecyclerPadIndex) &&
                IsValidPad(model.MachineShopPadIndex);
            _link.SetActive(linked);
            _sled.gameObject.SetActive(linked);
            if (linked)
            {
                Vector3 routeStart = WithHeight(
                    PadPositions[model.RecyclerPadIndex] +
                    RotateOffset(
                        new Vector3(0.45f, 0f, 0f),
                        model.RecyclerQuarterTurns),
                    0.18f);
                Vector3 routeEnd = WithHeight(
                    PadPositions[model.MachineShopPadIndex] +
                    RotateOffset(
                        new Vector3(-0.45f, 0f, 0f),
                        model.MachineShopQuarterTurns),
                    0.18f);
                Vector3 routeDirection = routeEnd - routeStart;
                _link.transform.localPosition =
                    Vector3.Lerp(routeStart, routeEnd, 0.5f);
                _link.transform.localScale = new Vector3(
                    Vector3.Distance(routeStart, routeEnd) + 0.35f,
                    0.12f,
                    0.55f);
                _link.transform.localRotation = Quaternion.Euler(
                    0f,
                    Mathf.Atan2(routeDirection.z, routeDirection.x) *
                    Mathf.Rad2Deg,
                    0f);
                float sledProgress = (int)model.CityDeliveryStage / 2f;
                if (model.CityDeliveryStage ==
                    CityDeliveryStage.DeliveredToWorkshop)
                {
                    sledProgress = model.HotShiftPhase ==
                                   HotShiftPhase.InProgress
                        ? model.HotShiftRequiredTicks <= 0
                            ? 0f
                            : Mathf.Clamp01(
                                (float)model.HotShiftElapsedTicks /
                                model.HotShiftRequiredTicks)
                        : 1f;
                }

                _hotShiftSledProgress = sledProgress;
                _sled.localPosition = Vector3.Lerp(
                    WithHeight(routeStart, 0.38f),
                    WithHeight(routeEnd, 0.38f),
                    sledProgress);
            }
            else
            {
                _hotShiftSledProgress = 0f;
            }

            bool humanAssigned = string.Equals(
                model.CityServiceResidentId,
                ResidentRoster.HumanResidentId,
                StringComparison.Ordinal);
            bool robotAssigned = string.Equals(
                model.CityServiceResidentId,
                ResidentRoster.RobotResidentId,
                StringComparison.Ordinal);
            bool operatorAtMachine =
                !model.IsHotShiftStalledByWorkshopPush;
            _humanOperator.SetActive(humanAssigned && operatorAtMachine);
            _robotOperator.SetActive(robotAssigned && operatorAtMachine);
            if (IsValidPad(model.MachineShopPadIndex))
            {
                Vector3 operatorPosition =
                    PadPositions[model.MachineShopPadIndex] +
                    RotateOffset(
                        new Vector3(0.55f, 0f, 0.55f),
                        model.MachineShopQuarterTurns);
                _humanOperator.transform.localPosition = operatorPosition;
                _robotOperator.transform.localPosition = operatorPosition;
            }

            bool activelyWorking =
                model.IsHotShiftActivelyWorking &&
                model.PauseCause == PauseCause.None;
            _hotShiftSpindle.gameObject.SetActive(activelyWorking);
            _hotShiftWorkPool.SetActive(activelyWorking);
            _workshopPushTransferArm.SetActive(
                model.IsHotShiftStalledByWorkshopPush);
            _dustFrontShutter.SetActive(
                model.IsHotShiftStalledByDustFront);
            bool hasCompletedRun = model.HotShiftCompletedCount > 0;
            _completionWitnessA.SetActive(hasCompletedRun);
            _completionWitnessB.SetActive(hasCompletedRun);
            _hotShiftSpindle.localRotation = Quaternion.Euler(
                model.HotShiftElapsedTicks * 31f,
                0f,
                0f);

            Interactor?.Apply(model, _sled);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static void ApplyBuilding(
            Transform buildingTransform,
            CityBuildingKind building,
            int canonicalPadIndex,
            int canonicalQuarterTurns,
            CityBuildingKind previewBuilding,
            int previewPadIndex,
            int previewQuarterTurns,
            bool previewActive)
        {
            bool isPreview = previewActive && previewBuilding == building;
            int padIndex = isPreview ? previewPadIndex : canonicalPadIndex;
            int quarterTurns = isPreview
                ? previewQuarterTurns
                : canonicalQuarterTurns;
            bool visible = IsValidPad(padIndex);
            buildingTransform.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            buildingTransform.localPosition = PadPositions[padIndex];
            buildingTransform.localRotation = Quaternion.Euler(
                0f,
                quarterTurns * 90f,
                0f);
            Vector3 baseScale = building == CityBuildingKind.EmergencyStorage
                ? new Vector3(1.25f, 0.9f, 1.25f)
                : ServiceBuildingScale;
            buildingTransform.localScale = baseScale * (isPreview ? 0.9f : 1f);
        }

        private static bool IsValidPad(int padIndex)
        {
            return padIndex >= 0 && padIndex < PadPositions.Length;
        }

        private static Vector3 WithHeight(Vector3 position, float height)
        {
            return new Vector3(position.x, height, position.z);
        }

        private static Vector3 RotateOffset(
            Vector3 offset,
            int quarterTurns)
        {
            return Quaternion.Euler(
                0f,
                quarterTurns * 90f,
                0f) * offset;
        }

        private static GameObject CreateBlock(
            string objectName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = block.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return block;
        }
    }
}
