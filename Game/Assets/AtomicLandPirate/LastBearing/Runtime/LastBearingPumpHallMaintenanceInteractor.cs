#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Derived pump-hall control for the existing field-sleeve maintenance
    /// obligation. It owns focus, fresh-input arming, pointer targeting, and
    /// presentation only; accepted work delegates to the controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingPumpHallMaintenanceInteractor :
        MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Keep The Promise Service Control [Derived Only]";
        public const string ServiceControlName =
            "INTERACT_FIELD_SLEEVE_SERVICE_CONTROL";
        public const string FeedbackLabelName =
            "FIELD_SLEEVE_SERVICE_FEEDBACK";

        private const int RaycastBufferSize = 6;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _serviceControl;
        private GameObject? _serviceLever;
        private GameObject? _focusRail;
        private GameObject? _firstServicePart;
        private GameObject? _secondServicePart;
        private TextMesh? _feedbackLabel;
        private BoxCollider? _serviceCollider;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private bool _inputArmed;
        private bool _presentationActive;
        private int _presentationEntryFrame = -1;
        private bool _built;

        public bool IsControlFocused { get; private set; }

        public bool IsInputArmed =>
            IsControlFocused &&
            _inputArmed &&
            IsServiceDue;

        public bool IsControlVisible => IsActive(_serviceControl);

        public bool IsServiceDue =>
            HasCurrentModel() &&
            IsMaintenanceDue(_model!);

        public bool IsServiceWitnessVisible =>
            IsControlVisible &&
            HasCurrentModel() &&
            IsMaintenanceWitness(_model!);

        public bool IsFirstServicePartVisible =>
            IsActive(_firstServicePart);

        public bool IsSecondServicePartVisible =>
            IsActive(_secondServicePart);

        public bool IsFocusRailVisible => IsActive(_focusRail);

        public string Feedback { get; private set; } = string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public bool IsBuilt => _built;

        public bool HasDedicatedInteractionTarget =>
            _serviceCollider != null &&
            _serviceCollider.isTrigger &&
            _serviceCollider.gameObject.layer == InteractionLayer;

        public Vector3 ServiceControlWorldPosition =>
            _serviceCollider?.bounds.center ??
            _serviceControl?.transform.position ??
            transform.position;

        internal void Build(
            Transform turbineRepairSocket,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            gameObject.name = RootName;
            turbineRepairSocket = turbineRepairSocket ??
                throw new ArgumentNullException(nameof(turbineRepairSocket));
            iron = iron ?? throw new ArgumentNullException(nameof(iron));
            oxide = oxide ?? throw new ArgumentNullException(nameof(oxide));
            bone = bone ?? throw new ArgumentNullException(nameof(bone));
            tungsten = tungsten ??
                throw new ArgumentNullException(nameof(tungsten));

            transform.localPosition =
                turbineRepairSocket.localPosition +
                new Vector3(3.1f, 0f, -0.3f);

            _serviceControl = new GameObject(ServiceControlName);
            _serviceControl.layer = InteractionLayer;
            _serviceControl.transform.SetParent(transform, false);
            _serviceCollider = _serviceControl.AddComponent<BoxCollider>();
            _serviceCollider.size = new Vector3(1.4f, 1.55f, 1.05f);
            _serviceCollider.center = new Vector3(0f, 1.02f, 0f);
            _serviceCollider.isTrigger = true;

            CreateVisual(
                "FIELD_SLEEVE_SERVICE_PLINTH",
                PrimitiveType.Cube,
                _serviceControl.transform,
                new Vector3(0f, 0.22f, 0f),
                new Vector3(1.18f, 0.44f, 0.86f),
                Quaternion.identity,
                iron);
            _serviceLever = CreateVisual(
                "FIELD_SLEEVE_SERVICE_LEVER",
                PrimitiveType.Cube,
                _serviceControl.transform,
                new Vector3(0f, 1.02f, 0.03f),
                new Vector3(0.16f, 0.92f, 0.16f),
                Quaternion.Euler(0f, 0f, -22f),
                oxide);
            CreateVisual(
                "FIELD_SLEEVE_SERVICE_HANDLE",
                PrimitiveType.Cube,
                _serviceLever.transform,
                new Vector3(0f, 0.58f, 0f),
                new Vector3(0.48f, 0.18f, 0.18f),
                Quaternion.identity,
                bone);
            _focusRail = CreateVisual(
                "FIELD_SLEEVE_SERVICE_FOCUS_RAIL",
                PrimitiveType.Cube,
                _serviceControl.transform,
                new Vector3(0f, 0.08f, -0.5f),
                new Vector3(1.32f, 0.08f, 0.1f),
                Quaternion.identity,
                tungsten);
            _firstServicePart = CreateVisual(
                "FIELD_SLEEVE_SERVICE_PART_01",
                PrimitiveType.Cube,
                _serviceControl.transform,
                new Vector3(-0.3f, 0.54f, -0.24f),
                new Vector3(0.34f, 0.16f, 0.3f),
                Quaternion.Euler(0f, 18f, 0f),
                oxide);
            _secondServicePart = CreateVisual(
                "FIELD_SLEEVE_SERVICE_PART_02",
                PrimitiveType.Cube,
                _serviceControl.transform,
                new Vector3(0.3f, 0.54f, -0.24f),
                new Vector3(0.34f, 0.16f, 0.3f),
                Quaternion.Euler(0f, -18f, 0f),
                oxide);

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(transform, false);
            feedback.transform.localPosition =
                new Vector3(0f, 2.55f, -0.15f);
            _feedbackLabel = feedback.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 48;
            _feedbackLabel.characterSize = 0.04f;
            _feedbackLabel.color = new Color32(230, 213, 169, 255);

            ApplyVisibility();
            RefreshVisuals();
            Physics.SyncTransforms();
        }

        internal void Configure(
            LastBearingGameController controller,
            Camera sharedCamera)
        {
            _controller = controller ??
                throw new ArgumentNullException(nameof(controller));
            _camera = sharedCamera ??
                throw new ArgumentNullException(nameof(sharedCamera));
            RefreshVisuals();
        }

        internal void Apply(LastBearingReadModel model)
        {
            _model = model ??
                throw new ArgumentNullException(nameof(model));
            if (!IsMaintenanceDue(model))
            {
                ResetFocus();
            }

            ApplyVisibility();
            RefreshVisuals();
            Physics.SyncTransforms();
        }

        public void ResetLocalFocus()
        {
            _model = null;
            ResetFocus();
            _hasPointerPosition = false;
            Feedback = string.Empty;
            LastInteractionRejected = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        public bool FocusServiceControl()
        {
            if (!HasCurrentPresentation() ||
                !IsMaintenanceDue(_model!))
            {
                ResetFocus();
                Reject("FIELD SLEEVE SERVICE UNAVAILABLE");
                return false;
            }

            IsControlFocused = true;
            _inputArmed = false;
            _presentationActive = true;
            _presentationEntryFrame = Time.frameCount;
            SetFeedback(
                "RELEASE CONTROL · THEN E / GAMEPAD SOUTH",
                rejected: false);
            ApplyVisibility();
            return true;
        }

        public bool OperateFocused()
        {
            if (!IsControlFocused)
            {
                Reject("FOCUS THE FIELD SLEEVE SERVICE CONTROL FIRST");
                return false;
            }

            if (!IsInputArmed)
            {
                Reject("RELEASE CONTROL · THEN OPERATE");
                return false;
            }

            return QueueService();
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasCurrentPresentation() ||
                !IsMaintenanceDue(_model!) ||
                Time.frameCount <= _presentationEntryFrame)
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                true)
            {
                Reject("FIELD DESK HAS THIS POINTER · service ignored");
                return false;
            }

            if (!TryRaycastControl(screenPosition))
            {
                return false;
            }

            IsControlFocused = true;
            _presentationActive = true;
            return QueueService();
        }

        private void Update()
        {
            if (!HasCurrentPresentation())
            {
                if (_model != null ||
                    IsControlFocused ||
                    Feedback.Length != 0)
                {
                    ResetLocalFocus();
                }

                return;
            }

            Keyboard? keyboard = Keyboard.current;
            Gamepad? gamepad = Gamepad.current;
            bool primaryHeld =
                keyboard?.eKey.isPressed == true ||
                gamepad?.buttonSouth.isPressed == true;
            UpdateInputArming(primaryHeld);

            bool navigated =
                keyboard?.leftArrowKey.wasPressedThisFrame == true ||
                keyboard?.rightArrowKey.wasPressedThisFrame == true ||
                gamepad?.dpad.left.wasPressedThisFrame == true ||
                gamepad?.dpad.right.wasPressedThisFrame == true;
            if (navigated && !IsControlFocused)
            {
                FocusServiceControl();
            }

            bool operated =
                keyboard?.eKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (operated)
            {
                OperateFocused();
            }

            Mouse? mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();
            bool pointerMoved =
                !_hasPointerPosition ||
                (pointer - _lastPointerPosition).sqrMagnitude > 0.01f;
            _lastPointerPosition = pointer;
            _hasPointerPosition = true;
            if (_controller?.FieldDesk?.BlocksWorldPointer(pointer) == true)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryActivateAtScreenPosition(pointer);
            }
            else if (pointerMoved &&
                TryRaycastControl(pointer) &&
                !IsControlFocused)
            {
                FocusServiceControl();
            }
        }

        private void LateUpdate()
        {
            if (_feedbackLabel == null || _camera == null)
            {
                return;
            }

            Transform label = _feedbackLabel.transform;
            Vector3 towardCamera =
                _camera.transform.position - label.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                label.rotation = Quaternion.LookRotation(
                    towardCamera.normalized,
                    Vector3.up);
            }
        }

        private void OnEnable()
        {
            ApplyVisibility();
            RefreshVisuals();
        }

        private void OnDisable()
        {
            ResetFocus();
            ApplyVisibility();
            RefreshVisuals();
        }

        private bool QueueService()
        {
            if (!HasCurrentPresentation() ||
                !IsMaintenanceDue(_model!) ||
                _controller == null)
            {
                Reject("FIELD SLEEVE SERVICE STALE · no work queued");
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                SetFeedback(
                    _controller.IsFieldSleeveServiceQueued
                        ? "FIELD SLEEVE SERVICE ALREADY QUEUED"
                        : "ACTION QUEUED · let the city ledger accept it first",
                    rejected: !_controller.IsFieldSleeveServiceQueued);
                return false;
            }

            if (!_controller.CanServiceFieldSleeve)
            {
                Reject(
                    _model!.PartsUnits <
                    LastBearingBalanceV1.SleeveMaintenancePartsUnits
                        ? "SERVICE NEEDS 2 RECLAIMED PARTS"
                        : "FIELD SLEEVE SERVICE UNAVAILABLE");
                return false;
            }

            _controller.ServiceFieldSleeve();
            if (!_controller.IsFieldSleeveServiceQueued)
            {
                Reject(_controller.Status);
                return false;
            }

            _inputArmed = false;
            SetFeedback(
                "PROMISE WORK QUEUED · PARTS MOVE ON THE CITY TICK",
                rejected: false);
            return true;
        }

        private void UpdateInputArming(bool inputHeld)
        {
            if (!IsControlFocused || !IsMaintenanceDue(_model!))
            {
                _inputArmed = false;
                _presentationActive = false;
                _presentationEntryFrame = -1;
                return;
            }

            if (!_presentationActive)
            {
                _presentationActive = true;
                _presentationEntryFrame = Time.frameCount;
                _inputArmed = false;
                return;
            }

            if (!_inputArmed &&
                Time.frameCount > _presentationEntryFrame &&
                !inputHeld)
            {
                _inputArmed = true;
                SetFeedback(
                    "KEEP THE PROMISE · E / GAMEPAD SOUTH",
                    rejected: false);
            }
        }

        private bool HasCurrentPresentation()
        {
            return _built &&
                   HasCurrentModel() &&
                   IsMaintenanceWitness(_model!) &&
                   _controller?.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.BuildingCutaway &&
                   _controller.World?.IsPumpHallCutawaySelected == true &&
                   gameObject.activeInHierarchy;
        }

        private bool HasCurrentModel()
        {
            return _controller?.HasActiveGame == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel);
        }

        private static bool IsMaintenanceDue(LastBearingReadModel model)
        {
            return IsMaintenanceWitness(model) &&
                   model.MaintenanceDue &&
                   model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                   model.TransactionPhase == TransactionPhase.Finalized &&
                   model.PauseCause == PauseCause.None;
        }

        private static bool IsMaintenanceWitness(
            LastBearingReadModel model)
        {
            return model.RepairCargoKind == RepairCargoKind.FieldSleeve &&
                   model.TurbineCondition ==
                       TurbineCondition.SleeveRepaired &&
                   model.MaintenanceRecipe ==
                       MaintenanceRecipe.FieldSleeveService &&
                   model.MaintenanceObligationActive;
        }

        private void ApplyVisibility()
        {
            bool visible = HasCurrentPresentation();
            SetActive(_serviceControl, visible);
            if (_serviceCollider != null)
            {
                _serviceCollider.enabled =
                    visible && IsMaintenanceDue(_model!);
            }

            if (_feedbackLabel != null)
            {
                _feedbackLabel.gameObject.SetActive(visible);
            }

            SetActive(
                _firstServicePart,
                visible && IsMaintenanceDue(_model!));
            SetActive(
                _secondServicePart,
                visible && IsMaintenanceDue(_model!));
            SetActive(
                _focusRail,
                visible && IsControlFocused);
        }

        private void RefreshVisuals()
        {
            if (_serviceLever != null)
            {
                float angle = _controller?.IsFieldSleeveServiceQueued == true
                    ? 46f
                    : HasCurrentModel() && !IsMaintenanceDue(_model!)
                        ? 22f
                        : -22f;
                _serviceLever.transform.localRotation =
                    Quaternion.Euler(0f, 0f, angle);
            }

            if (_feedbackLabel == null)
            {
                return;
            }

            if (Feedback.Length == 0 && HasCurrentPresentation())
            {
                Feedback = IsMaintenanceDue(_model!)
                    ? "SERVICE DUE · 2 PARTS"
                    : "PROMISE KEPT · NEXT SERVICE TICK " +
                      _controller!
                          .NextFieldSleeveMaintenanceDueSettlementTick;
            }

            _feedbackLabel.text = Feedback;
            _feedbackLabel.color = LastInteractionRejected
                ? new Color32(230, 103, 74, 255)
                : new Color32(230, 213, 169, 255);
        }

        private void ResetFocus()
        {
            IsControlFocused = false;
            _inputArmed = false;
            _presentationActive = false;
            _presentationEntryFrame = -1;
            LastInteractionRejected = false;
            Feedback = string.Empty;
        }

        private void SetFeedback(string feedback, bool rejected)
        {
            Feedback = feedback;
            LastInteractionRejected = rejected;
            ApplyVisibility();
            RefreshVisuals();
        }

        private void Reject(string feedback)
        {
            SetFeedback(feedback, rejected: true);
        }

        private bool TryRaycastControl(Vector2 screenPosition)
        {
            if (_camera == null ||
                _serviceCollider == null ||
                !_serviceCollider.enabled)
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
            for (var index = 0; index < hitCount; index++)
            {
                if (_raycastHits[index].collider == _serviceCollider)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject CreateVisual(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            visual.transform.localRotation = localRotation;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return visual;
        }

        private static bool IsActive(GameObject? value)
        {
            return value != null && value.activeSelf;
        }

        private static void SetActive(GameObject? value, bool active)
        {
            value?.SetActive(active);
        }
    }
}
