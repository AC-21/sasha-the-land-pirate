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
        private GameObject? _link;
        private Transform? _sled;
        private GameObject? _humanOperator;
        private GameObject? _robotOperator;
        private bool _built;

        public bool IsVisible => gameObject.activeSelf;

        public bool IsRecyclerVisible =>
            _recycler?.gameObject.activeSelf == true;

        public bool IsMachineShopVisible =>
            _machineShop?.gameObject.activeSelf == true;

        public bool IsEmergencyStorageVisible =>
            _emergencyStorage?.gameObject.activeSelf == true;

        public bool IsLinkVisible => _link?.activeSelf == true;

        public bool IsSledVisible => _sled?.gameObject.activeSelf == true;

        public bool IsHumanOperatorVisible =>
            _humanOperator?.activeSelf == true;

        public bool IsRobotOperatorVisible =>
            _robotOperator?.activeSelf == true;

        internal void Build(
            Material concrete,
            Material iron,
            Material oxide,
            Material bone)
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
                _link == null ||
                _sled == null ||
                _humanOperator == null ||
                _robotOperator == null)
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

            bool linked = model.CityServiceLinkConnected &&
                IsValidPad(model.RecyclerPadIndex) &&
                IsValidPad(model.MachineShopPadIndex);
            _link.SetActive(linked);
            _sled.gameObject.SetActive(linked);
            if (linked)
            {
                Vector3 routeStart = WithHeight(
                    PadPositions[model.RecyclerPadIndex],
                    0.18f);
                Vector3 routeEnd = WithHeight(
                    PadPositions[model.MachineShopPadIndex],
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

                _sled.localPosition = Vector3.Lerp(
                    WithHeight(routeStart, 0.38f),
                    WithHeight(routeEnd, 0.38f),
                    sledProgress);
            }

            bool humanAssigned = string.Equals(
                model.CityServiceResidentId,
                ResidentRoster.HumanResidentId,
                StringComparison.Ordinal);
            bool robotAssigned = string.Equals(
                model.CityServiceResidentId,
                ResidentRoster.RobotResidentId,
                StringComparison.Ordinal);
            _humanOperator.SetActive(humanAssigned);
            _robotOperator.SetActive(robotAssigned);
            if (IsValidPad(model.MachineShopPadIndex))
            {
                Vector3 operatorPosition =
                    PadPositions[model.MachineShopPadIndex] +
                    new Vector3(0.55f, 0f, 0.55f);
                _humanOperator.transform.localPosition = operatorPosition;
                _robotOperator.transform.localPosition = operatorPosition;
            }
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
