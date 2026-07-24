#nullable enable

using System;
using System.Collections.Generic;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Thin CityOverview input for the fixed working service cell. Every
    /// accepted click delegates to an existing controller verb; colliders,
    /// selection, hover, labels, and highlights are derived-only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingCityServiceCellInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RecyclerSelectorName =
            "INTERACT_SELECT_RECYCLER";
        public const string MachineShopSelectorName =
            "INTERACT_SELECT_MACHINE_SHOP";
        public const string EmergencyStorageSelectorName =
            "INTERACT_SELECT_EMERGENCY_STORAGE";
        public const string RecyclerOutputSocketName =
            "SOCKET_RECYCLER_OUTPUT_INTERACTION";
        public const string MachineShopIntakeSocketName =
            "SOCKET_MACHINE_SHOP_INTAKE_INTERACTION";
        public const string HumanResidentTokenName =
            "TOKEN_SERVICE_OPERATOR_HUMAN";
        public const string RobotResidentTokenName =
            "TOKEN_SERVICE_OPERATOR_ROBOT";
        public const string OperatorSocketName =
            "SOCKET_MACHINE_SHOP_OPERATOR_INTERACTION";
        public const string SledInteractionName =
            "INTERACT_CALIBRATION_SLED";
        public const string SledDestinationName =
            "SOCKET_CALIBRATION_SLED_DESTINATION";
        public const string HotShiftMachineControlName =
            "INTERACT_MACHINE_SHOP_HOT_SHIFT";
        public const string EmergencyCisternPumpControlName =
            "INTERACT_EMERGENCY_CISTERN_PUMP";
        public const string DustFrontRelayControlName =
            EmergencyCisternPumpControlName;
        public const string DustFrontRelayHeldSignalName =
            "DUST_FRONT_RELAY_HELD_SIGNAL";
        public const string DustFrontRelayBreachedSignalName =
            "DUST_FRONT_RELAY_BREACHED_SIGNAL";
        public const string FeedbackLabelName =
            "WORKING_SERVICE_CELL_FEEDBACK";

        private const int PadCount = 5;
        private const int RaycastBufferSize = 12;
        private const float RaycastDistance = 500f;

        private static readonly Vector3[] PadPositions =
        {
            new Vector3(-2.8f, 0.65f, -0.55f),
            new Vector3(-1.4f, 0.65f, 0.35f),
            new Vector3(0f, 0.65f, -0.55f),
            new Vector3(1.4f, 0.65f, 0.35f),
            new Vector3(2.8f, 0.65f, -0.55f),
        };

        private readonly Dictionary<Collider, InteractionTarget> _targets =
            new Dictionary<Collider, InteractionTarget>();
        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];
        private readonly GameObject[] _padTargets = new GameObject[PadCount];
        private readonly GameObject[] _padHighlights = new GameObject[PadCount];

        private LastBearingGameController? _controller;
        private Camera? _camera;
        private LastBearingReadModel? _model;
        private GameObject? _recyclerSelector;
        private GameObject? _machineShopSelector;
        private GameObject? _emergencyStorageSelector;
        private GameObject? _recyclerOutputSocket;
        private GameObject? _machineShopIntakeSocket;
        private GameObject? _humanResidentToken;
        private GameObject? _robotResidentToken;
        private GameObject? _operatorSocket;
        private GameObject? _sledTarget;
        private GameObject? _sledDestination;
        private GameObject? _hotShiftMachineControl;
        private TextMesh? _hotShiftMachineLabel;
        private GameObject? _emergencyCisternPumpControl;
        private GameObject? _emergencyCisternPumpLever;
        private GameObject? _emergencyCisternPumpFocusRail;
        private TextMesh? _emergencyCisternPumpLabel;
        private GameObject? _dustFrontRelayHeldSignal;
        private GameObject? _dustFrontRelayBreachedSignal;
        private TextMesh? _feedbackLabel;
        private int _hoveredPadIndex = -1;
        private bool _linkSourceSelected;
        private string? _selectedResidentId;
        private bool _hotShiftControlFocused;
        private bool _emergencyCisternPumpFocused;
        private bool _emergencyCisternPumpInputArmed;
        private bool _emergencyCisternPumpPresentationActive;
        private int _emergencyCisternPumpPresentationEntryFrame = -1;
        private bool _dustFrontRelayFocused;
        private bool _dustFrontRelayInputArmed;
        private bool _dustFrontRelayPresentationActive;
        private int _dustFrontRelayPresentationEntryFrame = -1;
        private bool _built;

        public string Feedback { get; private set; } =
            "CHOOSE A BUILDING · THEN A WORK PAD";

        public bool LastInteractionRejected { get; private set; }

        public bool IsLinkSourceSelected => _linkSourceSelected;

        public string? SelectedResidentId => _selectedResidentId;

        public int HighlightedPadIndex
        {
            get
            {
                if (_hoveredPadIndex >= 0)
                {
                    return _hoveredPadIndex;
                }

                if (_controller?.HasCityBuildingPreview == true)
                {
                    return _controller.CityPreviewPadIndex;
                }

                return -1;
            }
        }

        public bool IsBuilt => _built;

        public bool IsHotShiftControlFocused =>
            _hotShiftControlFocused &&
            _hotShiftMachineControl?.activeInHierarchy == true;

        public bool IsHotShiftControlVisible =>
            _hotShiftMachineControl?.activeInHierarchy == true;

        public string HotShiftMachineLabel =>
            _hotShiftMachineLabel?.text ?? string.Empty;

        public bool IsEmergencyCisternPumpControlVisible =>
            _emergencyCisternPumpControl?.activeInHierarchy == true &&
            IsInteractionActive() &&
            _model != null &&
            ShouldShowEmergencyCisternPumpControl(_model);

        public bool IsEmergencyCisternPumpFocused =>
            _emergencyCisternPumpFocused &&
            IsEmergencyCisternPumpControlVisible;

        public bool IsEmergencyCisternPumpInputArmed =>
            IsEmergencyCisternPumpFocused &&
            _emergencyCisternPumpInputArmed;

        public bool IsDustFrontRelayControlVisible =>
            _emergencyCisternPumpControl?.activeInHierarchy == true &&
            IsInteractionActive() &&
            _model != null &&
            ShouldShowDustFrontRelayControl(_model);

        public bool HasDustFrontRelayControl =>
            _built && _emergencyCisternPumpControl != null;

        public bool IsDustFrontRelayFocused =>
            _dustFrontRelayFocused &&
            IsDustFrontRelayControlVisible;

        public bool IsDustFrontRelayInputArmed =>
            IsDustFrontRelayFocused &&
            _dustFrontRelayInputArmed;

        public string DustFrontRelayLabel =>
            _emergencyCisternPumpLabel?.text ?? string.Empty;

        public bool HasDedicatedInteractionTargets =>
            _targets.Count >= 17;

        internal void Build(
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
            bone = RequireMaterial(bone, nameof(bone));
            signal = RequireMaterial(signal, nameof(signal));
            tungsten = RequireMaterial(tungsten, nameof(tungsten));
            iron = RequireMaterial(iron, nameof(iron));
            oxide = RequireMaterial(oxide, nameof(oxide));

            _recyclerSelector = CreateInteractionTarget(
                RecyclerSelectorName,
                new Vector3(-4.7f, 0.34f, -3.2f),
                new Vector3(1.7f, 0.68f, 0.9f),
                InteractionKind.SelectRecycler,
                -1,
                oxide,
                "RECYCLER\n2 PARTS · MOVE FREE");
            _machineShopSelector = CreateInteractionTarget(
                MachineShopSelectorName,
                new Vector3(-2.35f, 0.34f, -3.2f),
                new Vector3(1.8f, 0.68f, 0.9f),
                InteractionKind.SelectMachineShop,
                -1,
                iron,
                "MACHINE SHOP\n3 PARTS · MOVE FREE");
            _emergencyStorageSelector = CreateInteractionTarget(
                EmergencyStorageSelectorName,
                new Vector3(0f, 0.34f, -3.2f),
                new Vector3(1.8f, 0.68f, 0.9f),
                InteractionKind.SelectEmergencyStorage,
                -1,
                bone,
                "STORAGE\n1 PART · MOVE FREE");

            for (var index = 0; index < PadCount; index++)
            {
                GameObject target = CreateInvisibleInteractionTarget(
                    "INTERACT_WORK_PAD_" + (index + 1).ToString("00"),
                    new Vector3(
                        PadPositions[index].x,
                        0.72f,
                        PadPositions[index].z),
                    new Vector3(1.5f, 0.72f, 1.7f),
                    InteractionKind.Pad,
                    index);
                GameObject highlight = CreateVisualBlock(
                    "HIGHLIGHT_WORK_PAD_" + (index + 1).ToString("00"),
                    target.transform,
                    new Vector3(0f, -0.34f, 0f),
                    new Vector3(1.46f, 0.08f, 1.66f),
                    tungsten);
                highlight.SetActive(false);
                _padTargets[index] = target;
                _padHighlights[index] = highlight;
            }

            _recyclerOutputSocket = CreateInteractionTarget(
                RecyclerOutputSocketName,
                Vector3.zero,
                new Vector3(0.52f, 0.52f, 0.52f),
                InteractionKind.RecyclerOutput,
                -1,
                signal,
                "OUTPUT");
            _machineShopIntakeSocket = CreateInteractionTarget(
                MachineShopIntakeSocketName,
                Vector3.zero,
                new Vector3(0.52f, 0.52f, 0.52f),
                InteractionKind.MachineShopIntake,
                -1,
                tungsten,
                "INTAKE");
            _humanResidentToken = CreateInteractionTarget(
                HumanResidentTokenName,
                new Vector3(-1.15f, 0.42f, 2.55f),
                new Vector3(0.72f, 0.84f, 0.72f),
                InteractionKind.HumanResident,
                -1,
                oxide,
                "HUMAN OPERATOR");
            _robotResidentToken = CreateInteractionTarget(
                RobotResidentTokenName,
                new Vector3(1.15f, 0.42f, 2.55f),
                new Vector3(0.72f, 0.84f, 0.72f),
                InteractionKind.RobotResident,
                -1,
                iron,
                "UTILITY ROBOT");
            _operatorSocket = CreateInteractionTarget(
                OperatorSocketName,
                Vector3.zero,
                new Vector3(0.64f, 0.64f, 0.64f),
                InteractionKind.OperatorSocket,
                -1,
                signal,
                "OPERATOR SOCKET");
            _sledTarget = CreateInvisibleInteractionTarget(
                SledInteractionName,
                Vector3.zero,
                new Vector3(0.9f, 0.8f, 0.9f),
                InteractionKind.Sled,
                -1);
            CreateVisualBlock(
                "CALIBRATION_SLED_CLICK_MARKER",
                _sledTarget.transform,
                Vector3.zero,
                new Vector3(0.62f, 0.08f, 0.62f),
                signal);
            _sledDestination = CreateInteractionTarget(
                SledDestinationName,
                Vector3.zero,
                new Vector3(0.7f, 0.5f, 0.7f),
                InteractionKind.SledDestination,
                -1,
                tungsten,
                "DELIVER");
            _hotShiftMachineControl = CreateInteractionTarget(
                HotShiftMachineControlName,
                Vector3.zero,
                new Vector3(1.12f, 0.62f, 0.86f),
                InteractionKind.HotShiftMachineControl,
                -1,
                tungsten,
                "CLOCK HOT SHIFT\nRETURN · GAMEPAD SOUTH");
            _hotShiftMachineLabel =
                _hotShiftMachineControl.GetComponentInChildren<TextMesh>();
            _emergencyCisternPumpControl = CreateInvisibleInteractionTarget(
                EmergencyCisternPumpControlName,
                Vector3.zero,
                new Vector3(1.18f, 1.08f, 0.94f),
                InteractionKind.EmergencyCisternPumpControl,
                -1);
            CreateVisualBlock(
                "EMERGENCY_CISTERN_PUMP_PLINTH",
                _emergencyCisternPumpControl.transform,
                new Vector3(0f, -0.38f, 0f),
                new Vector3(0.92f, 0.22f, 0.72f),
                iron);
            _emergencyCisternPumpLever = CreateVisualBlock(
                "EMERGENCY_CISTERN_PUMP_LEVER",
                _emergencyCisternPumpControl.transform,
                new Vector3(0f, 0.06f, 0.02f),
                new Vector3(0.16f, 0.72f, 0.16f),
                oxide);
            CreateVisualBlock(
                "EMERGENCY_CISTERN_PUMP_HANDLE",
                _emergencyCisternPumpLever.transform,
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0.46f, 0.18f, 0.18f),
                signal);
            _emergencyCisternPumpFocusRail = CreateVisualBlock(
                "EMERGENCY_CISTERN_PUMP_FOCUS_RAIL",
                _emergencyCisternPumpControl.transform,
                new Vector3(0f, -0.18f, -0.4f),
                new Vector3(1.02f, 0.08f, 0.08f),
                tungsten);
            _emergencyCisternPumpFocusRail.SetActive(false);
            _dustFrontRelayHeldSignal = CreateVisualBlock(
                DustFrontRelayHeldSignalName,
                _emergencyCisternPumpControl.transform,
                new Vector3(-0.25f, 0.48f, 0.22f),
                new Vector3(0.18f, 0.18f, 0.18f),
                tungsten);
            _dustFrontRelayHeldSignal.SetActive(false);
            _dustFrontRelayBreachedSignal = CreateVisualBlock(
                DustFrontRelayBreachedSignalName,
                _emergencyCisternPumpControl.transform,
                new Vector3(0.25f, 0.48f, 0.22f),
                new Vector3(0.18f, 0.18f, 0.18f),
                oxide);
            _dustFrontRelayBreachedSignal.SetActive(false);
            CreateLabel(
                "EMERGENCY_CISTERN_PUMP_LABEL",
                _emergencyCisternPumpControl.transform,
                "PUMP CISTERN\nE · GAMEPAD SOUTH",
                new Vector3(0f, 0.5f, -0.22f));
            _emergencyCisternPumpLabel =
                _emergencyCisternPumpControl.
                    GetComponentInChildren<TextMesh>();

            var feedbackObject = new GameObject(
                FeedbackLabelName);
            feedbackObject.transform.SetParent(transform, false);
            feedbackObject.transform.localPosition =
                new Vector3(0f, 2.15f, 3.25f);
            _feedbackLabel = feedbackObject.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 54;
            _feedbackLabel.characterSize = 0.055f;
            _feedbackLabel.color = new Color32(230, 213, 169, 255);
            _feedbackLabel.text = Feedback;

            RefreshInteractionVisuals();
        }

        internal void Configure(
            LastBearingGameController controller,
            Camera cityCamera)
        {
            _controller = controller ??
                throw new ArgumentNullException(nameof(controller));
            _camera = cityCamera ??
                throw new ArgumentNullException(nameof(cityCamera));
            RefreshInteractionVisuals();
        }

        internal void Apply(LastBearingReadModel model, Transform sled)
        {
            _model = model ??
                throw new ArgumentNullException(nameof(model));
            if (model.CityServiceLinkConnected ||
                _controller?.HasCityBuildingPreview == true)
            {
                _linkSourceSelected = false;
            }

            if (model.CityServiceResidentId != null &&
                string.Equals(
                    model.CityServiceResidentId,
                    _selectedResidentId,
                    StringComparison.Ordinal))
            {
                _selectedResidentId = null;
            }

            if (!ShouldShowEmergencyCisternPumpControl(model))
            {
                ResetEmergencyCisternPumpFocus();
            }

            if (!CanWorkDustFrontRelay(model))
            {
                ResetDustFrontRelayFocus();
            }

            if (!ShouldShowHotShiftControl(model))
            {
                _hotShiftControlFocused = false;
            }
            else if (!_hotShiftControlFocused &&
                     !_emergencyCisternPumpFocused &&
                     !_dustFrontRelayFocused)
            {
                _hotShiftControlFocused = true;
            }

            bool interactionTargetMoved = PositionServiceSockets(model);
            if (_sledTarget != null)
            {
                bool sledMoved = SetLocalPositionIfChanged(
                    _sledTarget,
                    sled.localPosition);
                bool sledColliderRelevant =
                    model.CityServiceLinkConnected &&
                    model.CityServiceResidentId != null &&
                    model.CityDeliveryStage !=
                        CityDeliveryStage.DeliveredToWorkshop;
                interactionTargetMoved |= sledMoved && sledColliderRelevant;
            }

            if (interactionTargetMoved)
            {
                Physics.SyncTransforms();
            }

            RefreshInteractionVisuals();
        }

        public void ResetLocalSelection()
        {
            _hoveredPadIndex = -1;
            _linkSourceSelected = false;
            _selectedResidentId = null;
            _hotShiftControlFocused = false;
            ResetEmergencyCisternPumpFocus();
            ResetDustFrontRelayFocus();
            SetFeedback(
                "CHOOSE A BUILDING · THEN A WORK PAD",
                rejected: false);
            RefreshInteractionVisuals();
        }

        private void OnDisable()
        {
            ResetLocalSelection();
        }

        public void SelectBuilding(CityBuildingKind building)
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (_model?.CityServiceLinkConnected == true)
            {
                Reject("LAYOUT LOCKED · the service link is permanent");
                return;
            }

            if (_controller!.HasPendingPlayerCommands)
            {
                Reject("ACTION QUEUED · let the city record it first");
                return;
            }

            _linkSourceSelected = false;
            _controller.SelectCityBuildingPreview(building);
            if (!_controller.HasCityBuildingPreview ||
                _controller.CityPreviewBuilding != building)
            {
                Reject(_controller.Status);
                return;
            }

            _hoveredPadIndex = _controller.CityPreviewPadIndex;
            bool repositioning = IsBuildingPlaced(_model, building);
            SetFeedback(
                building + " SELECTED · " +
                (repositioning
                    ? "MOVE FREE"
                    : _controller.CityPreviewPartsCost + " PARTS") +
                " · hover a pad · R rotates · click places",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public void HoverPad(int padIndex)
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (!IsValidPad(padIndex))
            {
                Reject("UNKNOWN WORK PAD");
                return;
            }

            _hoveredPadIndex = padIndex;
            if (_controller?.HasCityBuildingPreview == true &&
                _controller.CityPreviewPadIndex != padIndex &&
                _model?.CityServiceLinkConnected == false &&
                !_controller.HasPendingPlayerCommands)
            {
                MovePreviewToPad(padIndex);
                if (_controller.CanPlaceCityBuildingPreview)
                {
                    SetFeedback(
                        "PAD " + (padIndex + 1) +
                        " · R rotates · click commits this position",
                        rejected: false);
                }
                else
                {
                    bool partsShort =
                        _model != null &&
                        !IsBuildingPlaced(
                            _model,
                            _controller.CityPreviewBuilding) &&
                        _model.PartsUnits < _controller.CityPreviewPartsCost;
                    Reject(partsShort
                        ? "PAD " + (padIndex + 1) + " · NEED " +
                          _controller.CityPreviewPartsCost + " PARTS"
                        : "PAD " + (padIndex + 1) +
                          " OCCUPIED · choose another work pad");
                }
            }
            else if (_controller?.HasCityBuildingPreview != true)
            {
                SetFeedback(
                    "PAD " + (padIndex + 1) +
                    " · choose Recycler, Machine Shop, or Storage",
                    rejected: false);
            }

            RefreshPadHighlights();
        }

        public void RotatePreview()
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (_controller?.HasCityBuildingPreview != true)
            {
                Reject("ROTATE UNAVAILABLE · choose a building first");
                return;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                Reject("ACTION QUEUED · rotation waits for the city record");
                return;
            }

            int before = _controller.CityPreviewQuarterTurns;
            _controller.RotateCityBuildingPreview();
            if (_controller.CityPreviewQuarterTurns == before)
            {
                Reject(_controller.Status);
                return;
            }

            SetFeedback(
                "ROTATED " + (_controller.CityPreviewQuarterTurns * 90) +
                "° · click the highlighted pad",
                rejected: false);
            RefreshPadHighlights();
        }

        public void ClickPad(int padIndex)
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (!IsValidPad(padIndex))
            {
                Reject("UNKNOWN WORK PAD");
                return;
            }

            if (_controller?.HasCityBuildingPreview != true)
            {
                Reject("PAD NEEDS A BUILDING · use a selector first");
                return;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                Reject("ACTION QUEUED · let the city record it first");
                return;
            }

            MovePreviewToPad(padIndex);
            if (!_controller.CanPlaceCityBuildingPreview)
            {
                Reject(
                    "PAD " + (padIndex + 1) +
                    " UNAVAILABLE · occupied or reclaimed parts are short");
                return;
            }

            CityBuildingKind building = _controller.CityPreviewBuilding;
            bool repositioning = IsBuildingPlaced(_model, building);
            _controller.PlaceCityBuildingPreview();
            if (!_controller.HasPendingPlayerCommands)
            {
                Reject(_controller.Status);
                return;
            }

            SetFeedback(
                building + (repositioning
                    ? " REPOSITION QUEUED · no parts charged"
                    : " PLACEMENT QUEUED · " +
                      _controller.CityPreviewPartsCost + " parts"),
                rejected: false);
        }

        public void ClickRecyclerOutput()
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (_controller?.HasPendingPlayerCommands == true)
            {
                Reject("ACTION QUEUED · let the city record it first");
                return;
            }

            if (_controller?.CanConnectCityServiceLink != true)
            {
                Reject(
                    "OUTPUT UNAVAILABLE · place all three buildings and retain 1 part");
                return;
            }

            _linkSourceSelected = true;
            SetFeedback(
                "RECYCLER OUTPUT SELECTED · click Machine Shop intake · costs 1 part",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public void ClickMachineShopIntake()
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (!_linkSourceSelected)
            {
                Reject("SELECT RECYCLER OUTPUT FIRST");
                return;
            }

            if (_controller?.HasPendingPlayerCommands == true ||
                _controller?.CanConnectCityServiceLink != true)
            {
                Reject(
                    "LINK UNAVAILABLE · layout, queue, or 1-part lock cost changed");
                return;
            }

            _controller.ConnectCityServiceLink();
            if (!_controller.HasPendingPlayerCommands)
            {
                Reject(_controller.Status);
                return;
            }

            _linkSourceSelected = false;
            SetFeedback(
                "SERVICE LINK QUEUED · acceptance locks this layout",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public void SelectResident(string stableId)
        {
            if (!RequireCityOverview())
            {
                return;
            }

            bool eligible = string.Equals(
                    stableId,
                    ResidentRoster.HumanResidentId,
                    StringComparison.Ordinal)
                ? _controller?.CanAssignCityServiceHuman == true
                : string.Equals(
                    stableId,
                    ResidentRoster.RobotResidentId,
                    StringComparison.Ordinal) &&
                  _controller?.CanAssignCityServiceRobot == true;
            if (!eligible)
            {
                Reject(
                    "OPERATOR UNAVAILABLE · that resident is not in this colony roster");
                return;
            }

            _selectedResidentId = stableId;
            SetFeedback(
                stableId + " SELECTED · click the Machine Shop operator socket",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public void ClickOperatorSocket()
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (_controller?.HasPendingPlayerCommands == true)
            {
                Reject("ACTION QUEUED · let the city record it first");
                return;
            }

            if (_selectedResidentId == null)
            {
                Reject("SELECT AN ELIGIBLE RESIDENT TOKEN FIRST");
                return;
            }

            string stableId = _selectedResidentId;
            _controller!.AssignCityServiceResident(stableId);
            if (!_controller.HasPendingPlayerCommands)
            {
                Reject(_controller.Status);
                return;
            }

            SetFeedback(
                "OPERATOR QUEUED · " + stableId +
                " · no composition bonus",
                rejected: false);
        }

        public void ClickSled()
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (_model?.CityDeliveryStage == CityDeliveryStage.InTransit)
            {
                Reject(
                    "SLED IN TRANSIT · click the Machine Shop destination socket");
                return;
            }

            if (_model?.CityDeliveryStage ==
                CityDeliveryStage.DeliveredToWorkshop)
            {
                Reject("DELIVERY COMPLETE · the 2-part return was paid once");
                return;
            }

            AdvanceSled(
                "SLED DEPARTURE QUEUED · Recycler → Machine Shop");
        }

        public void ClickSledDestination()
        {
            if (!RequireCityOverview())
            {
                return;
            }

            if (_model?.CityDeliveryStage == CityDeliveryStage.AtRecycler)
            {
                Reject("SEND THE SLED FROM THE RECYCLER FIRST");
                return;
            }

            if (_model?.CityDeliveryStage ==
                CityDeliveryStage.DeliveredToWorkshop)
            {
                Reject("DELIVERY COMPLETE · the 2-part return was paid once");
                return;
            }

            AdvanceSled(
                "COMMISSIONING DELIVERY QUEUED · returns 2 parts once");
        }

        public void FocusHotShiftControl()
        {
            if (!IsInteractionActive() ||
                _model == null ||
                !ShouldShowHotShiftControl(_model))
            {
                _hotShiftControlFocused = false;
                Reject("HOT SHIFT CONTROL UNAVAILABLE");
                RefreshInteractionVisuals();
                return;
            }

            _hotShiftControlFocused = true;
            ResetEmergencyCisternPumpFocus();
            ResetDustFrontRelayFocus();
            SetFeedback(HotShiftFeedback(_model), rejected: false);
            RefreshInteractionVisuals();
        }

        public void ClickHotShiftControl()
        {
            if (!RequireCityOverview() ||
                _model == null ||
                !ShouldShowHotShiftControl(_model))
            {
                Reject("HOT SHIFT CONTROL UNAVAILABLE");
                return;
            }

            _hotShiftControlFocused = true;
            ResetEmergencyCisternPumpFocus();
            ResetDustFrontRelayFocus();
            if (_controller!.HasPendingPlayerCommands)
            {
                SetFeedback(
                    _controller.IsHotShiftStartQueued
                        ? "HOT SHIFT ALREADY QUEUED · custody unchanged until the city tick"
                        : "ACTION QUEUED · let the city record it first",
                    rejected: !_controller.IsHotShiftStartQueued);
                RefreshInteractionVisuals();
                return;
            }

            if (!_controller.CanStartHotShift)
            {
                _controller.StartHotShift();
                Reject(_controller.Status);
                RefreshInteractionVisuals();
                return;
            }

            _controller.StartHotShift();
            if (!_controller.IsHotShiftStartQueued)
            {
                Reject(_controller.Status);
                RefreshInteractionVisuals();
                return;
            }

            SetFeedback(
                "HOT SHIFT QUEUED · 1 fuel and machine motion begin on the authoritative tick",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public void FocusEmergencyCisternPump()
        {
            if (!IsInteractionActive() ||
                _model == null ||
                !ShouldShowEmergencyCisternPumpControl(_model))
            {
                ResetEmergencyCisternPumpFocus();
                Reject("EMERGENCY CISTERN PUMP UNAVAILABLE");
                RefreshInteractionVisuals();
                return;
            }

            _emergencyCisternPumpFocused = true;
            _hotShiftControlFocused = false;
            ResetDustFrontRelayFocus();
            _emergencyCisternPumpInputArmed = false;
            _emergencyCisternPumpPresentationActive = true;
            _emergencyCisternPumpPresentationEntryFrame =
                Time.frameCount;

            SetFeedback(
                _emergencyCisternPumpInputArmed
                    ? "CISTERN PUMP READY · E · GAMEPAD SOUTH · or pull the lever"
                    : "CISTERN PUMP FOCUSED · release controls to set the lever",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public void FocusDustFrontRelay()
        {
            if (!IsInteractionActive() ||
                _model == null ||
                !CanWorkDustFrontRelay(_model))
            {
                ResetDustFrontRelayFocus();
                Reject("DUST FRONT RELAY UNAVAILABLE");
                RefreshInteractionVisuals();
                return;
            }

            _dustFrontRelayFocused = true;
            _hotShiftControlFocused = false;
            ResetEmergencyCisternPumpFocus();
            _dustFrontRelayInputArmed = false;
            _dustFrontRelayPresentationActive = true;
            _dustFrontRelayPresentationEntryFrame = Time.frameCount;
            SetFeedback(
                "DUST FRONT RELAY FOCUSED · release controls before facing the verdict",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public void ClickDustFrontRelay()
        {
            if (!RequireCityOverview() ||
                _model == null ||
                !CanWorkDustFrontRelay(_model))
            {
                Reject("DUST FRONT RELAY UNAVAILABLE");
                return;
            }

            if (!_dustFrontRelayFocused)
            {
                Reject("FOCUS THE DUST FRONT RELAY FIRST");
                return;
            }

            if (_controller?.IsDustFrontAcknowledgementQueued == true)
            {
                SetFeedback(
                    "DUST FRONT VERDICT ALREADY QUEUED · clocks wait for the city tick",
                    rejected: false);
                return;
            }

            if (!_dustFrontRelayInputArmed)
            {
                Reject("RELEASE CONTROLS · THEN FACE THE DUST FRONT");
                return;
            }

            if (_controller?.CanAcknowledgeDustFront != true)
            {
                Reject("DUST FRONT RELAY CONTROL STALE");
                return;
            }

            _controller.AcknowledgeDustFront();
            if (!_controller.IsDustFrontAcknowledgementQueued)
            {
                Reject(
                    string.IsNullOrWhiteSpace(_controller.Status)
                        ? "DUST FRONT VERDICT NOT QUEUED"
                        : _controller.Status);
                return;
            }

            _dustFrontRelayInputArmed = false;
            SetFeedback(
                _model.DustFrontOutcome == DustFrontOutcome.Held
                    ? "FRONT HELD · acknowledgement queued · clocks resume on the authoritative tick"
                    : "FRONT BREACHED · acknowledgement queued · shutter stays down until turbine repair",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public void ClickEmergencyCisternPump()
        {
            if (!RequireCityOverview() ||
                _model == null ||
                !ShouldShowEmergencyCisternPumpControl(_model))
            {
                Reject("EMERGENCY CISTERN PUMP UNAVAILABLE");
                return;
            }

            if (!_emergencyCisternPumpFocused)
            {
                Reject("FOCUS THE EMERGENCY CISTERN PUMP FIRST");
                return;
            }

            if (_controller?.IsEmergencyCisternPumpQueued == true)
            {
                SetFeedback(
                    "CISTERN PUMP ALREADY QUEUED · fill marker waits for the city tick",
                    rejected: false);
                return;
            }

            if (!_emergencyCisternPumpInputArmed)
            {
                Reject("RELEASE CONTROLS · THEN PULL THE CISTERN LEVER");
                return;
            }

            if (_controller?.CanPumpEmergencyCistern != true)
            {
                Reject("CISTERN PUMP CONTROL STALE");
                return;
            }

            _controller.PumpEmergencyCistern();
            if (!_controller.IsEmergencyCisternPumpQueued)
            {
                Reject(
                    string.IsNullOrWhiteSpace(_controller.Status)
                        ? "CISTERN PUMP ACTION NOT QUEUED"
                        : _controller.Status);
                return;
            }

            _emergencyCisternPumpInputArmed = false;
            SetFeedback(
                "CISTERN PUMP QUEUED · 1 fuel · +10.000 water on the authoritative tick",
                rejected: false);
            RefreshInteractionVisuals();
        }

        public bool IsPadHighlighted(int padIndex)
        {
            return IsValidPad(padIndex) &&
                   _padHighlights[padIndex]?.activeSelf == true;
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!IsInteractionActive())
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) == true)
            {
                Reject("FIELD DESK HAS THIS POINTER · world click ignored");
                return false;
            }

            if (!TryRaycastTarget(screenPosition, out InteractionTarget target))
            {
                return false;
            }

            if (target.Kind == InteractionKind.Pad)
            {
                HoverPad(target.Index);
            }

            Activate(target);
            return true;
        }

        private void Update()
        {
            if (!IsInteractionActive())
            {
                if (_hoveredPadIndex != -1 ||
                    _linkSourceSelected ||
                    _selectedResidentId != null ||
                    _hotShiftControlFocused ||
                    _emergencyCisternPumpFocused ||
                    _emergencyCisternPumpPresentationActive ||
                    _dustFrontRelayFocused ||
                    _dustFrontRelayPresentationActive)
                {
                    ResetLocalSelection();
                }

                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard?.rKey.wasPressedThisFrame == true)
            {
                RotatePreview();
            }

            var gamepad = Gamepad.current;
            var mouse = Mouse.current;
            UpdateEmergencyCisternPumpInputArming(
                keyboard,
                gamepad,
                mouse);
            UpdateDustFrontRelayInputArming(
                keyboard,
                gamepad,
                mouse);
            bool operateEmergencyCisternPump =
                keyboard?.eKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            bool operateDustFrontRelay =
                keyboard?.eKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            bool operateHotShift =
                keyboard?.enterKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (operateDustFrontRelay &&
                IsDustFrontRelayFocused &&
                _controller?.FieldDesk?.OwnsKeyboardFocus != true)
            {
                ClickDustFrontRelay();
            }
            else if (operateEmergencyCisternPump &&
                IsEmergencyCisternPumpFocused &&
                _controller?.FieldDesk?.OwnsKeyboardFocus != true)
            {
                ClickEmergencyCisternPump();
            }
            else if (operateHotShift &&
                IsHotShiftControlFocused &&
                _controller?.FieldDesk?.OwnsKeyboardFocus != true)
            {
                ClickHotShiftControl();
            }

            if (mouse != null &&
                _controller?.FieldDesk?.BlocksWorldPointer(
                    mouse.position.ReadValue()) == true)
            {
                if (_hoveredPadIndex != -1)
                {
                    _hoveredPadIndex = -1;
                    RefreshPadHighlights();
                }

                return;
            }

            if (mouse == null ||
                !TryRaycastTarget(mouse.position.ReadValue(), out InteractionTarget target))
            {
                if (_hoveredPadIndex != -1)
                {
                    _hoveredPadIndex = -1;
                    RefreshPadHighlights();
                }

                return;
            }

            if (target.Kind == InteractionKind.Pad &&
                target.Index != _hoveredPadIndex)
            {
                HoverPad(target.Index);
            }
            else if (target.Kind ==
                         InteractionKind.EmergencyCisternPumpControl &&
                     !_emergencyCisternPumpFocused &&
                     !_dustFrontRelayFocused &&
                     _model != null &&
                     (CanWorkDustFrontRelay(_model) ||
                      ShouldShowEmergencyCisternPumpControl(_model)))
            {
                FocusSharedEmergencyControl();
            }
            else if (target.Kind != InteractionKind.Pad &&
                     _hoveredPadIndex != -1)
            {
                _hoveredPadIndex = -1;
                RefreshPadHighlights();
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryActivateAtScreenPosition(mouse.position.ReadValue());
            }
        }

        private void LateUpdate()
        {
            if (_feedbackLabel == null || _camera == null)
            {
                return;
            }

            Transform labelTransform = _feedbackLabel.transform;
            Vector3 towardCamera =
                _camera.transform.position - labelTransform.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                labelTransform.rotation = Quaternion.LookRotation(
                    towardCamera.normalized,
                    Vector3.up);
            }
        }

        private void Activate(InteractionTarget target)
        {
            switch (target.Kind)
            {
                case InteractionKind.SelectRecycler:
                    SelectBuilding(CityBuildingKind.Recycler);
                    break;
                case InteractionKind.SelectMachineShop:
                    SelectBuilding(CityBuildingKind.MachineShop);
                    break;
                case InteractionKind.SelectEmergencyStorage:
                    SelectBuilding(CityBuildingKind.EmergencyStorage);
                    break;
                case InteractionKind.Pad:
                    ClickPad(target.Index);
                    break;
                case InteractionKind.RecyclerOutput:
                    ClickRecyclerOutput();
                    break;
                case InteractionKind.MachineShopIntake:
                    ClickMachineShopIntake();
                    break;
                case InteractionKind.HumanResident:
                    SelectResident(ResidentRoster.HumanResidentId);
                    break;
                case InteractionKind.RobotResident:
                    SelectResident(ResidentRoster.RobotResidentId);
                    break;
                case InteractionKind.OperatorSocket:
                    ClickOperatorSocket();
                    break;
                case InteractionKind.Sled:
                    ClickSled();
                    break;
                case InteractionKind.SledDestination:
                    ClickSledDestination();
                    break;
                case InteractionKind.HotShiftMachineControl:
                    FocusHotShiftControl();
                    ClickHotShiftControl();
                    break;
                case InteractionKind.EmergencyCisternPumpControl:
                    if (_model != null &&
                        CanWorkDustFrontRelay(_model))
                    {
                        if (!IsDustFrontRelayFocused)
                        {
                            FocusDustFrontRelay();
                        }

                        ClickDustFrontRelay();
                    }
                    else if (_model != null &&
                             ShouldShowEmergencyCisternPumpControl(_model))
                    {
                        if (!IsEmergencyCisternPumpFocused)
                        {
                            FocusEmergencyCisternPump();
                        }

                        ClickEmergencyCisternPump();
                    }
                    break;
            }
        }

        private void FocusSharedEmergencyControl()
        {
            if (_model != null && CanWorkDustFrontRelay(_model))
            {
                FocusDustFrontRelay();
                return;
            }

            if (_model != null &&
                ShouldShowEmergencyCisternPumpControl(_model))
            {
                FocusEmergencyCisternPump();
            }
        }

        private void AdvanceSled(string acceptedFeedback)
        {
            if (_controller?.HasPendingPlayerCommands == true)
            {
                Reject("ACTION QUEUED · let the city record it first");
                return;
            }

            if (_controller?.CanAdvanceCityServiceSled != true)
            {
                Reject(
                    "SLED UNAVAILABLE · lock the link and staff the Machine Shop");
                return;
            }

            _controller.AdvanceCityServiceSled();
            if (!_controller.HasPendingPlayerCommands)
            {
                Reject(_controller.Status);
                return;
            }

            SetFeedback(acceptedFeedback, rejected: false);
        }

        private bool RequireCityOverview()
        {
            if (_controller?.IsExactFieldDeskCityOverview == true &&
                _model != null &&
                ReferenceEquals(
                    _model,
                    _controller.RuntimeReadModel))
            {
                return true;
            }

            Reject("OPEN CITY OVERVIEW TO WORK THE SERVICE CELL");
            return false;
        }

        private bool IsInteractionActive()
        {
            return _built &&
                   _controller?.IsExactFieldDeskCityOverview == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel) &&
                   gameObject.activeInHierarchy;
        }

        private bool TryRaycastTarget(
            Vector2 screenPosition,
            out InteractionTarget target)
        {
            target = default;
            if (_camera == null)
            {
                return false;
            }

            Ray ray = _camera.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _raycastHits,
                RaycastDistance,
                1 << InteractionLayer,
                QueryTriggerInteraction.Collide);
            float nearestDistance = float.MaxValue;
            bool found = false;
            for (var index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _raycastHits[index];
                if (hit.distance >= nearestDistance ||
                    !_targets.TryGetValue(hit.collider, out InteractionTarget candidate))
                {
                    continue;
                }

                nearestDistance = hit.distance;
                target = candidate;
                found = true;
            }

            return found;
        }

        private void MovePreviewToPad(int padIndex)
        {
            if (_controller == null ||
                !_controller.HasCityBuildingPreview ||
                !IsValidPad(padIndex))
            {
                return;
            }

            for (var guard = 0;
                 guard < PadCount &&
                 _controller.CityPreviewPadIndex != padIndex;
                 guard++)
            {
                _controller.MoveCityBuildingPreview(1);
            }
        }

        private bool PositionServiceSockets(LastBearingReadModel model)
        {
            bool moved = false;
            if (_recyclerOutputSocket != null &&
                IsValidPad(model.RecyclerPadIndex))
            {
                Vector3 position =
                    PadPositions[model.RecyclerPadIndex] +
                    RotateOffset(
                        new Vector3(0.45f, 0f, 0f),
                        model.RecyclerQuarterTurns);
                moved |= SetLocalPositionIfChanged(
                    _recyclerOutputSocket,
                    WithHeight(position, 0.82f));
            }

            if (_machineShopIntakeSocket != null &&
                IsValidPad(model.MachineShopPadIndex))
            {
                Vector3 position =
                    PadPositions[model.MachineShopPadIndex] +
                    RotateOffset(
                        new Vector3(-0.45f, 0f, 0f),
                        model.MachineShopQuarterTurns);
                moved |= SetLocalPositionIfChanged(
                    _machineShopIntakeSocket,
                    WithHeight(position, 0.82f));
            }

            if (_operatorSocket != null &&
                IsValidPad(model.MachineShopPadIndex))
            {
                Vector3 position =
                    PadPositions[model.MachineShopPadIndex] +
                    RotateOffset(
                        new Vector3(0.55f, 0f, 0.55f),
                        model.MachineShopQuarterTurns);
                moved |= SetLocalPositionIfChanged(
                    _operatorSocket,
                    WithHeight(position, 0.65f));
            }

            if (_sledDestination != null &&
                IsValidPad(model.MachineShopPadIndex))
            {
                Vector3 position =
                    PadPositions[model.MachineShopPadIndex] +
                    RotateOffset(
                        new Vector3(-0.45f, 0f, 0f),
                        model.MachineShopQuarterTurns);
                moved |= SetLocalPositionIfChanged(
                    _sledDestination,
                    WithHeight(position, 0.38f));
            }

            if (_hotShiftMachineControl != null &&
                IsValidPad(model.MachineShopPadIndex))
            {
                Vector3 position =
                    PadPositions[model.MachineShopPadIndex] +
                    RotateOffset(
                        new Vector3(0f, 0f, -0.82f),
                        model.MachineShopQuarterTurns);
                moved |= SetLocalPositionIfChanged(
                    _hotShiftMachineControl,
                    WithHeight(position, 0.58f));
                _hotShiftMachineControl.transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        model.MachineShopQuarterTurns * 90f,
                        0f);
            }

            if (_emergencyCisternPumpControl != null &&
                IsValidPad(model.EmergencyStoragePadIndex))
            {
                int pumpQuarterTurns =
                    SelectEmergencyCisternPumpQuarterTurns(model);
                Vector3 position =
                    PadPositions[model.EmergencyStoragePadIndex] +
                    RotateOffset(
                        new Vector3(0f, 0f, -1.18f),
                        pumpQuarterTurns);
                moved |= SetLocalPositionIfChanged(
                    _emergencyCisternPumpControl,
                    WithHeight(position, 0.56f));
                _emergencyCisternPumpControl.transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        pumpQuarterTurns * 90f,
                        0f);
            }

            return moved;
        }

        private void RefreshInteractionVisuals()
        {
            bool hasModel = _model != null;
            bool linked = _model?.CityServiceLinkConnected == true;
            bool previewActive = _controller?.HasCityBuildingPreview == true;
            bool allPlaced = _model != null &&
                IsValidPad(_model.RecyclerPadIndex) &&
                IsValidPad(_model.MachineShopPadIndex) &&
                IsValidPad(_model.EmergencyStoragePadIndex);
            bool staffed = _model?.CityServiceResidentId != null;
            bool sledComplete = _model?.CityDeliveryStage ==
                CityDeliveryStage.DeliveredToWorkshop;
            bool staffingOpen = linked &&
                _model?.CityDeliveryStage == CityDeliveryStage.AtRecycler;
            bool showHotShiftControl =
                _model != null &&
                ShouldShowHotShiftControl(_model);
            bool showEmergencyCisternPumpControl =
                _model != null &&
                ShouldShowEmergencyCisternPumpControl(_model);
            bool showDustFrontRelayControl =
                _model != null &&
                ShouldShowDustFrontRelayControl(_model);
            bool showSharedEmergencyControl =
                showEmergencyCisternPumpControl ||
                showDustFrontRelayControl;
            bool humanChoiceAvailable = staffingOpen &&
                _controller?.CanAssignCityServiceHuman == true &&
                !string.Equals(
                    _model?.CityServiceResidentId,
                    ResidentRoster.HumanResidentId,
                    StringComparison.Ordinal);
            bool robotChoiceAvailable = staffingOpen &&
                _controller?.CanAssignCityServiceRobot == true &&
                !string.Equals(
                    _model?.CityServiceResidentId,
                    ResidentRoster.RobotResidentId,
                    StringComparison.Ordinal);

            SetActive(_recyclerSelector, hasModel && !linked);
            SetActive(_machineShopSelector, hasModel && !linked);
            SetActive(_emergencyStorageSelector, hasModel && !linked);
            for (var index = 0; index < PadCount; index++)
            {
                SetActive(_padTargets[index], hasModel && !linked && previewActive);
            }

            SetActive(
                _recyclerOutputSocket,
                hasModel && !linked && allPlaced && !previewActive);
            SetActive(
                _machineShopIntakeSocket,
                hasModel && !linked && allPlaced && !previewActive);
            SetActive(
                _humanResidentToken,
                humanChoiceAvailable);
            SetActive(
                _robotResidentToken,
                robotChoiceAvailable);
            SetActive(
                _operatorSocket,
                humanChoiceAvailable || robotChoiceAvailable);
            SetActive(_sledTarget, linked && staffed && !sledComplete);
            SetActive(
                _sledDestination,
                linked && staffed && !sledComplete);
            SetActive(
                _hotShiftMachineControl,
                showHotShiftControl);
            SetActive(
                _emergencyCisternPumpControl,
                showSharedEmergencyControl);

            ScaleSelection(
                _recyclerSelector,
                _controller?.CityPreviewBuilding == CityBuildingKind.Recycler);
            ScaleSelection(
                _machineShopSelector,
                _controller?.CityPreviewBuilding == CityBuildingKind.MachineShop);
            ScaleSelection(
                _emergencyStorageSelector,
                _controller?.CityPreviewBuilding ==
                    CityBuildingKind.EmergencyStorage);
            ScaleSelection(
                _recyclerOutputSocket,
                _linkSourceSelected);
            ScaleSelection(
                _humanResidentToken,
                string.Equals(
                    _selectedResidentId,
                    ResidentRoster.HumanResidentId,
                    StringComparison.Ordinal));
            ScaleSelection(
                _robotResidentToken,
                string.Equals(
                    _selectedResidentId,
                    ResidentRoster.RobotResidentId,
                    StringComparison.Ordinal));
            ScaleSelection(
                _hotShiftMachineControl,
                showHotShiftControl && _hotShiftControlFocused);
            ScaleSelection(
                _emergencyCisternPumpControl,
                (showEmergencyCisternPumpControl &&
                 _emergencyCisternPumpFocused) ||
                (showDustFrontRelayControl &&
                 _dustFrontRelayFocused));
            SetActive(
                _emergencyCisternPumpFocusRail,
                (showEmergencyCisternPumpControl &&
                 _emergencyCisternPumpFocused) ||
                (showDustFrontRelayControl &&
                 _dustFrontRelayFocused));
            SetActive(
                _dustFrontRelayHeldSignal,
                showDustFrontRelayControl &&
                _model?.DustFrontOutcome == DustFrontOutcome.Held);
            SetActive(
                _dustFrontRelayBreachedSignal,
                showDustFrontRelayControl &&
                _model?.DustFrontOutcome == DustFrontOutcome.Breached);
            if (_hotShiftMachineLabel != null && _model != null)
            {
                string label = HotShiftControlLabel(_model);
                if (_hotShiftMachineLabel.text != label)
                {
                    _hotShiftMachineLabel.text = label;
                }

                _hotShiftMachineLabel.color =
                    _model.IsHotShiftStalledByDustFront ||
                    _model.IsHotShiftStalledByWorkshopPush
                        ? new Color32(255, 154, 102, 255)
                        : new Color32(238, 221, 178, 255);
            }

            if (_emergencyCisternPumpLabel != null)
            {
                bool dustFrontQueued =
                    _controller?.IsDustFrontAcknowledgementQueued == true;
                bool dustFrontBreached =
                    _model?.DustFrontOutcome == DustFrontOutcome.Breached;
                string label = showDustFrontRelayControl
                    ? _model?.IsDustFrontAcknowledgementRequired != true
                        ? dustFrontBreached
                            ? _model?.IsHotShiftStalledByDustFront == true
                                ? "FRONT BREACHED\nSHUTTER DOWN"
                                : "FRONT BREACHED\nREPAIR HOLDS"
                            : "FRONT HELD\nCLOCKS RUNNING"
                    : dustFrontQueued
                        ? "DUST FRONT QUEUED\nCITY TICK PENDING"
                        : !_dustFrontRelayFocused
                            ? dustFrontBreached
                                ? "FRONT BREACHED\nFACE RELAY"
                                : "FRONT HELD\nFACE RELAY"
                            : _dustFrontRelayInputArmed
                                ? "FACE DUST FRONT\nE · GAMEPAD SOUTH"
                                : "FACE DUST FRONT\nRELEASE CONTROLS"
                    : _controller?.IsEmergencyCisternPumpQueued == true
                        ? "CISTERN PUMP QUEUED\nCITY TICK PENDING"
                        : !_emergencyCisternPumpFocused
                            ? "PUMP CISTERN\nSELECT LEVER"
                            : _emergencyCisternPumpInputArmed
                                ? "PUMP CISTERN\nE · GAMEPAD SOUTH"
                                : "PUMP CISTERN\nRELEASE CONTROLS";
                if (_emergencyCisternPumpLabel.text != label)
                {
                    _emergencyCisternPumpLabel.text = label;
                }

                _emergencyCisternPumpLabel.color =
                    dustFrontQueued ||
                    _controller?.IsEmergencyCisternPumpQueued == true
                        ? new Color32(255, 190, 104, 255)
                        : showDustFrontRelayControl && dustFrontBreached
                            ? new Color32(255, 116, 86, 255)
                        : new Color32(238, 221, 178, 255);
            }

            if (_emergencyCisternPumpLever != null)
            {
                _emergencyCisternPumpLever.transform.localRotation =
                    Quaternion.Euler(
                        _controller?.IsEmergencyCisternPumpQueued == true
                        || _controller?.IsDustFrontAcknowledgementQueued == true
                            ? -32f
                            : 18f,
                        0f,
                        0f);
            }

            RefreshPadHighlights();
        }

        private void RefreshPadHighlights()
        {
            int highlighted = HighlightedPadIndex;
            for (var index = 0; index < PadCount; index++)
            {
                GameObject? highlight = _padHighlights[index];
                if (highlight != null)
                {
                    highlight.SetActive(
                        _padTargets[index]?.activeSelf == true &&
                        index == highlighted);
                }
            }
        }

        private void SetFeedback(string message, bool rejected)
        {
            Feedback = string.IsNullOrWhiteSpace(message)
                ? "SERVICE CELL READY"
                : message;
            LastInteractionRejected = rejected;
            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = Feedback;
                _feedbackLabel.color = rejected
                    ? new Color32(255, 108, 77, 255)
                    : new Color32(230, 213, 169, 255);
            }
        }

        private void Reject(string reason)
        {
            SetFeedback(reason, rejected: true);
        }

        private GameObject CreateInteractionTarget(
            string objectName,
            Vector3 position,
            Vector3 scale,
            InteractionKind kind,
            int index,
            Material material,
            string label)
        {
            GameObject target = CreateInvisibleInteractionTarget(
                objectName,
                position,
                scale,
                kind,
                index);
            CreateVisualBlock(
                objectName + "_PLATE",
                target.transform,
                Vector3.zero,
                scale * 0.82f,
                material);
            CreateLabel(
                objectName + "_LABEL",
                target.transform,
                label,
                new Vector3(0f, scale.y * 0.58f, -scale.z * 0.18f));
            return target;
        }

        private GameObject CreateInvisibleInteractionTarget(
            string objectName,
            Vector3 position,
            Vector3 scale,
            InteractionKind kind,
            int index)
        {
            var target = new GameObject(objectName);
            target.layer = InteractionLayer;
            target.transform.SetParent(transform, false);
            target.transform.localPosition = position;
            var collider = target.AddComponent<BoxCollider>();
            collider.size = scale;
            collider.isTrigger = true;
            _targets.Add(collider, new InteractionTarget(kind, index));
            return target;
        }

        private GameObject CreateVisualBlock(
            string objectName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = objectName;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return visual;
        }

        private void CreateLabel(
            string objectName,
            Transform parent,
            string value,
            Vector3 position)
        {
            var labelObject = new GameObject(objectName);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = position;
            labelObject.transform.localRotation =
                Quaternion.Euler(70f, 0f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 42;
            label.characterSize = 0.035f;
            label.color = new Color32(238, 221, 178, 255);
            label.text = value;
        }

        private static bool IsBuildingPlaced(
            LastBearingReadModel? model,
            CityBuildingKind building)
        {
            if (model == null)
            {
                return false;
            }

            return building switch
            {
                CityBuildingKind.Recycler => model.RecyclerPadIndex >= 0,
                CityBuildingKind.MachineShop =>
                    model.MachineShopPadIndex >= 0,
                CityBuildingKind.EmergencyStorage =>
                    model.EmergencyStoragePadIndex >= 0,
                _ => false,
            };
        }

        private static bool IsValidPad(int padIndex)
        {
            return padIndex >= 0 && padIndex < PadCount;
        }

        private static bool ShouldShowHotShiftControl(
            LastBearingReadModel model)
        {
            return model.CityDeliveryStage ==
                       CityDeliveryStage.DeliveredToWorkshop &&
                   model.PreparationChoice !=
                       PreparationChoice.Unselected &&
                   model.PlannedModule != VehicleModule.None;
        }

        private static bool ShouldShowEmergencyCisternPumpControl(
            LastBearingReadModel model)
        {
            return model.IsEmergencyCisternPumpAvailable;
        }

        private static bool ShouldShowDustFrontRelayControl(
            LastBearingReadModel model)
        {
            return model.DustFrontOutcome != DustFrontOutcome.Unresolved &&
                   model.EmergencyStoragePadIndex >= 0;
        }

        private static bool CanWorkDustFrontRelay(
            LastBearingReadModel model)
        {
            return model.IsDustFrontAcknowledgementRequired &&
                   ShouldShowDustFrontRelayControl(model);
        }

        private static int SelectEmergencyCisternPumpQuarterTurns(
            LastBearingReadModel model)
        {
            int preferred = model.EmergencyStorageQuarterTurns;
            if (!IsValidPad(model.MachineShopPadIndex))
            {
                return preferred;
            }

            Vector3 storage = PadPositions[model.EmergencyStoragePadIndex];
            Vector3 hotShift = PadPositions[model.MachineShopPadIndex] +
                RotateOffset(
                    new Vector3(0f, 0f, -0.82f),
                    model.MachineShopQuarterTurns);
            int bestQuarterTurns = preferred;
            float bestDistance = float.NegativeInfinity;
            for (var offset = 0; offset < 4; offset++)
            {
                int candidate = (preferred + offset) % 4;
                Vector3 position = storage + RotateOffset(
                    new Vector3(0f, 0f, -1.18f),
                    candidate);
                float distance = (position - hotShift).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestQuarterTurns = candidate;
                }
            }

            return bestQuarterTurns;
        }

        private void UpdateEmergencyCisternPumpInputArming(
            Keyboard? keyboard,
            Gamepad? gamepad,
            Mouse? mouse)
        {
            if (!IsEmergencyCisternPumpFocused)
            {
                _emergencyCisternPumpInputArmed = false;
                _emergencyCisternPumpPresentationActive = false;
                _emergencyCisternPumpPresentationEntryFrame = -1;
                return;
            }

            if (!_emergencyCisternPumpPresentationActive)
            {
                _emergencyCisternPumpPresentationActive = true;
                _emergencyCisternPumpPresentationEntryFrame =
                    Time.frameCount;
                _emergencyCisternPumpInputArmed = false;
                return;
            }

            bool controlsReleased =
                keyboard?.eKey.isPressed != true &&
                gamepad?.buttonSouth.isPressed != true &&
                mouse?.leftButton.isPressed != true;
            if (!_emergencyCisternPumpInputArmed &&
                Time.frameCount >
                    _emergencyCisternPumpPresentationEntryFrame &&
                controlsReleased)
            {
                _emergencyCisternPumpInputArmed = true;
                SetFeedback(
                    "CISTERN PUMP READY · E · GAMEPAD SOUTH · or pull the lever",
                    rejected: false);
                RefreshInteractionVisuals();
            }
        }

        private void ResetEmergencyCisternPumpFocus()
        {
            _emergencyCisternPumpFocused = false;
            _emergencyCisternPumpInputArmed = false;
            _emergencyCisternPumpPresentationActive = false;
            _emergencyCisternPumpPresentationEntryFrame = -1;
        }

        private void UpdateDustFrontRelayInputArming(
            Keyboard? keyboard,
            Gamepad? gamepad,
            Mouse? mouse)
        {
            if (!IsDustFrontRelayFocused)
            {
                _dustFrontRelayInputArmed = false;
                _dustFrontRelayPresentationActive = false;
                _dustFrontRelayPresentationEntryFrame = -1;
                return;
            }

            if (!_dustFrontRelayPresentationActive)
            {
                _dustFrontRelayPresentationActive = true;
                _dustFrontRelayPresentationEntryFrame = Time.frameCount;
                _dustFrontRelayInputArmed = false;
                return;
            }

            bool controlsReleased =
                keyboard?.eKey.isPressed != true &&
                gamepad?.buttonSouth.isPressed != true &&
                mouse?.leftButton.isPressed != true;
            if (!_dustFrontRelayInputArmed &&
                Time.frameCount > _dustFrontRelayPresentationEntryFrame &&
                controlsReleased)
            {
                _dustFrontRelayInputArmed = true;
                SetFeedback(
                    "DUST FRONT RELAY READY · E · GAMEPAD SOUTH · or pull the lever",
                    rejected: false);
                RefreshInteractionVisuals();
            }
        }

        private void ResetDustFrontRelayFocus()
        {
            _dustFrontRelayFocused = false;
            _dustFrontRelayInputArmed = false;
            _dustFrontRelayPresentationActive = false;
            _dustFrontRelayPresentationEntryFrame = -1;
        }

        private static string HotShiftControlLabel(
            LastBearingReadModel model)
        {
            if (model.IsHotShiftStalledByDustFront)
            {
                return "DUST FRONT STOP\nSHUTTER DOWN";
            }

            if (model.IsHotShiftStalledByWorkshopPush)
            {
                return "WORKSHOP PUSH\nOPERATOR AT GARAGE";
            }

            if (model.HotShiftPhase == HotShiftPhase.InProgress &&
                model.PauseCause != PauseCause.None)
            {
                return "HOT SHIFT PAUSED\nCITY CLOCK HELD";
            }

            if (model.IsHotShiftActivelyWorking)
            {
                return "HOT SHIFT RUNNING\nSPINDLE + SLED";
            }

            return model.HotShiftCompletedCount > 0
                ? "CLOCK ANOTHER HOT SHIFT\nRETURN · GAMEPAD SOUTH"
                : "CLOCK HOT SHIFT\nRETURN · GAMEPAD SOUTH";
        }

        private static string HotShiftFeedback(
            LastBearingReadModel model)
        {
            if (model.IsHotShiftStalledByDustFront)
            {
                return "DUST FRONT STOP · resident present · safety shutter down";
            }

            if (model.IsHotShiftStalledByWorkshopPush)
            {
                return "WORKSHOP PUSH STALL · operator borrowed by Sasha's rig";
            }

            if (model.HotShiftPhase == HotShiftPhase.InProgress &&
                model.PauseCause != PauseCause.None)
            {
                return "HOT SHIFT PAUSED · city clock held · machinery stopped";
            }

            if (model.IsHotShiftActivelyWorking)
            {
                return "HOT SHIFT RUNNING · spindle and sled follow the city clock";
            }

            return model.HotShiftCompletedCount > 0
                ? "SHIFT COMPLETE · two-notch output witness · clock another?"
                : "CLOCK HOT SHIFT · 1 fuel · 120 ticks · +2 parts";
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

        private static Vector3 WithHeight(Vector3 position, float height)
        {
            return new Vector3(position.x, height, position.z);
        }

        private static bool SetLocalPositionIfChanged(
            GameObject target,
            Vector3 position)
        {
            if ((target.transform.localPosition - position).sqrMagnitude <=
                0.000001f)
            {
                return false;
            }

            target.transform.localPosition = position;
            return true;
        }

        private static void ScaleSelection(GameObject? target, bool selected)
        {
            if (target == null)
            {
                return;
            }

            target.transform.localScale = selected
                ? new Vector3(1.12f, 1.12f, 1.12f)
                : Vector3.one;
        }

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static Material RequireMaterial(
            Material material,
            string parameterName)
        {
            return material != null
                ? material
                : throw new ArgumentNullException(parameterName);
        }

        private readonly struct InteractionTarget
        {
            public InteractionTarget(InteractionKind kind, int index)
            {
                Kind = kind;
                Index = index;
            }

            public InteractionKind Kind { get; }

            public int Index { get; }
        }

        private enum InteractionKind
        {
            SelectRecycler,
            SelectMachineShop,
            SelectEmergencyStorage,
            Pad,
            RecyclerOutput,
            MachineShopIntake,
            HumanResident,
            RobotResident,
            OperatorSocket,
            Sled,
            SledDestination,
            HotShiftMachineControl,
            EmergencyCisternPumpControl,
        }
    }
}
