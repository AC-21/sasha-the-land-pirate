#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing.Vehicle
{
    /// <summary>
    /// Garage-local service pendant for Sasha's scout. It owns only fresh
    /// physical input and derived witness presentation; the controller remains
    /// the sole presentation-to-command seam.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingScoutServiceInteractor : MonoBehaviour
    {
        public const int InteractionLayer =
            LastBearingGarageDepartureInteractor.InteractionLayer;
        public const string RootName =
            "Scout Service Pendant [Derived Only]";
        public const string TargetName =
            "INTERACT_SERVICE_SASHA_SCOUT";
        public const string FocusRailName =
            "SCOUT_SERVICE_PENDANT_FOCUS_RAIL";
        public const string FeedbackLabelName =
            "SCOUT_SERVICE_CONDITION_RECEIPT";

        private const int RaycastBufferSize = 8;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];
        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private LastBearingGarageBayView? _garageBay;
        private Camera? _camera;
        private GameObject? _witnessRoot;
        private GameObject? _target;
        private GameObject? _focusRail;
        private TextMesh? _feedbackLabel;
        private BoxCollider? _targetCollider;
        private bool _inputArmed;
        private bool _presentationActive;
        private int _presentationEntryFrame = -1;
        private bool _built;

        public bool IsControlFocused { get; private set; }

        public bool IsInputArmed =>
            IsControlFocused &&
            _inputArmed &&
            CanAcceptInput;

        public bool IsServiceReady =>
            HasCurrentModel() &&
            _model!.IsVehicleServiceAvailable;

        public bool IsWitnessVisible =>
            _witnessRoot?.activeInHierarchy == true;

        public bool IsControlVisible =>
            _target?.activeInHierarchy == true;

        public bool IsFocusRailVisible =>
            _focusRail?.activeInHierarchy == true;

        public bool IsAcceptedReceiptVisible =>
            IsWitnessVisible &&
            HasCurrentModel() &&
            IsAcceptedWitness(_model!);

        public bool HasDedicatedInteractionTarget =>
            _targetCollider != null &&
            _targetCollider.isTrigger &&
            _targetCollider.gameObject.layer == InteractionLayer;

        public Vector3 ControlWorldPosition =>
            _targetCollider?.bounds.center ??
            _target?.transform.position ??
            transform.position;

        public string Feedback { get; private set; } =
            "SCOUT SERVICE STOWED";

        public string ReceiptLabel =>
            _feedbackLabel?.text ?? string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public long DisplayedConditionMilli =>
            HasCurrentModel() ? _model!.VehicleConditionMilli : 0;

        public long DisplayedCostPartsUnits =>
            HasCurrentModel() ? _model!.VehicleServicePartsCostUnits : 0;

        public long DisplayedReservePartsUnits =>
            HasCurrentModel()
                ? _model!.VehicleServiceReservePartsUnits
                : 0;

        private bool CanAcceptInput =>
            IsServiceReady &&
            _controller?.HasPendingPlayerCommands != true;

        internal void Build(
            LastBearingGarageBayView garageBay,
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
            _garageBay = garageBay ??
                throw new ArgumentNullException(nameof(garageBay));
            darkIron = darkIron ??
                throw new ArgumentNullException(nameof(darkIron));
            oxide = oxide ??
                throw new ArgumentNullException(nameof(oxide));
            bone = bone ??
                throw new ArgumentNullException(nameof(bone));
            tungsten = tungsten ??
                throw new ArgumentNullException(nameof(tungsten));
            signal = signal ??
                throw new ArgumentNullException(nameof(signal));

            _witnessRoot = new GameObject(
                "SCOUT_SERVICE_PENDANT_WITNESS");
            _witnessRoot.transform.SetParent(transform, false);

            CreateVisual(
                "SCOUT_SERVICE_PENDANT_IRON_BACK",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(0f, 0.78f, 0f),
                new Vector3(1.72f, 1.65f, 0.2f),
                Quaternion.identity,
                darkIron);
            CreateVisual(
                "SCOUT_SERVICE_PENDANT_BONE_LEDGER",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(0f, 0.82f, -0.13f),
                new Vector3(1.46f, 1.25f, 0.08f),
                Quaternion.identity,
                bone);
            CreateVisual(
                "SCOUT_SERVICE_PENDANT_TWO_PART_NOTCH",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(-0.38f, 0.24f, -0.2f),
                new Vector3(0.18f, 0.34f, 0.08f),
                Quaternion.identity,
                oxide);
            CreateVisual(
                "SCOUT_SERVICE_PENDANT_RESERVE_NOTCH",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(0.38f, 0.24f, -0.2f),
                new Vector3(0.18f, 0.34f, 0.08f),
                Quaternion.identity,
                signal);

            _target = new GameObject(TargetName);
            _target.layer = InteractionLayer;
            _target.transform.SetParent(_witnessRoot.transform, false);
            _target.transform.localPosition =
                new Vector3(0f, 0.8f, -0.36f);
            _targetCollider = _target.AddComponent<BoxCollider>();
            _targetCollider.size = new Vector3(1.3f, 1.22f, 0.7f);
            _targetCollider.isTrigger = true;
            CreateVisual(
                "SCOUT_SERVICE_PENDANT_PULL",
                PrimitiveType.Cylinder,
                _target.transform,
                new Vector3(0f, -0.12f, 0f),
                new Vector3(0.22f, 0.42f, 0.22f),
                Quaternion.Euler(0f, 0f, 90f),
                oxide);
            CreateVisual(
                "SCOUT_SERVICE_PENDANT_TUNGSTEN_HANDLE",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, 0.32f, 0f),
                new Vector3(0.74f, 0.14f, 0.14f),
                Quaternion.identity,
                tungsten);

            _focusRail = CreateVisual(
                FocusRailName,
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(0f, 0.02f, -0.28f),
                new Vector3(1.55f, 0.08f, 0.08f),
                Quaternion.identity,
                tungsten);

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(_witnessRoot.transform, false);
            feedback.transform.localPosition =
                new Vector3(0f, 2.05f, 0f);
            _feedbackLabel = feedback.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 48;
            _feedbackLabel.characterSize = 0.031f;
            _feedbackLabel.color = new Color32(238, 221, 178, 255);

            _witnessRoot.SetActive(false);
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

        internal void Apply(LastBearingReadModel? model)
        {
            _model = model;
            if (model == null || !ShouldShowWitness(model))
            {
                ResetLocalFocus();
                _model = model;
            }
            else if (IsAcceptedWitness(model))
            {
                IsControlFocused = false;
                _inputArmed = false;
                _presentationActive = false;
                SetFeedback(
                    "SCOUT SERVICED · " +
                    model.VehicleConditionMilli + " / " +
                    LastBearingBalanceV1.StartingVehicleConditionMilli +
                    "\n" + model.VehicleServicePartsCostUnits +
                    " PARTS SPENT · " +
                    model.VehicleServiceReservePartsUnits +
                    " PARTS HELD IN RESERVE",
                    rejected: false);
            }
            else if (!IsControlFocused)
            {
                SetFeedback(ReadyFeedback(), rejected: false);
            }

            RefreshVisuals();
            Physics.SyncTransforms();
        }

        public bool FocusControl()
        {
            if (!HasCurrentPresentation() || !CanAcceptInput)
            {
                ResetTransientFocus();
                Reject("SCOUT SERVICE PENDANT UNAVAILABLE");
                return false;
            }

            IsControlFocused = true;
            _inputArmed = false;
            _presentationActive = true;
            _presentationEntryFrame = Time.frameCount;
            SetFeedback(
                "SCOUT SERVICE FOCUSED · RELEASE CONTROL\nTHEN PULL THE PENDANT",
                rejected: false);
            RefreshVisuals();
            return true;
        }

        public bool OperateFocused()
        {
            if (!IsControlFocused)
            {
                Reject("FOCUS THE SCOUT SERVICE PENDANT FIRST");
                return false;
            }

            if (!IsInputArmed)
            {
                Reject("RELEASE CONTROL · THEN SERVICE SASHA'S SCOUT");
                return false;
            }

            return QueueService();
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasCurrentPresentation() ||
                !CanAcceptInput ||
                _camera == null ||
                _targetCollider == null)
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                    true ||
                _controller?.Hud?.BlocksWorldPointer(screenPosition) == true)
            {
                Reject("GARAGE DESK HAS THIS POINTER · SERVICE IGNORED");
                return false;
            }

            if (!TryRaycastTarget(screenPosition))
            {
                return false;
            }

            if (!IsControlFocused)
            {
                return FocusControl();
            }

            return OperateFocused();
        }

        public void ResetLocalFocus()
        {
            _model = null;
            ResetTransientFocus();
            Feedback = "SCOUT SERVICE STOWED";
            LastInteractionRejected = false;
            RefreshVisuals();
        }

        internal static bool ShouldShowWitness(LastBearingReadModel model)
        {
            return model.IsVehicleServiceAvailable ||
                   IsAcceptedWitness(model);
        }

        internal static bool IsAcceptedWitness(LastBearingReadModel model)
        {
            return model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                   model.TransactionPhase == TransactionPhase.Finalized &&
                   model.SliceInfrastructureActive &&
                   model.CityDeliveryStage ==
                       CityDeliveryStage.DeliveredToWorkshop &&
                   model.TurbineCondition != TurbineCondition.Failing &&
                   model.VehicleConditionMilli ==
                       LastBearingBalanceV1.StartingVehicleConditionMilli &&
                   !model.IsVehicleServiceNeeded &&
                   model.PartsUnits >=
                       model.VehicleServiceReservePartsUnits &&
                   model.HotShiftPhase == HotShiftPhase.Idle &&
                   model.NextCityDecision == NextCityDecision.None &&
                   model.FactionAidPolicy !=
                       FactionAidPolicy.EmergencyWaterQueued &&
                   !model.MaintenanceDue &&
                   model.SpareBearingBatchPhase !=
                       SpareBearingBatchPhase.InProgress &&
                   model.SpareBearingBatchPhase !=
                       SpareBearingBatchPhase.Complete &&
                   !model.IsDustFrontAcknowledgementRequired;
        }

        private void Update()
        {
            if (!HasCurrentPresentation())
            {
                if (IsControlFocused ||
                    _presentationActive ||
                    _inputArmed)
                {
                    ResetTransientFocus();
                    RefreshVisuals();
                }

                return;
            }

            Keyboard? keyboard = Keyboard.current;
            Gamepad? gamepad = Gamepad.current;
            Mouse? mouse = Mouse.current;
            bool primaryHeld =
                keyboard?.eKey.isPressed == true ||
                gamepad?.buttonSouth.isPressed == true ||
                mouse?.leftButton.isPressed == true;
            UpdateInputArming(primaryHeld);

            if (IsControlFocused &&
                _controller?.FieldDesk?.OwnsKeyboardFocus != true &&
                (keyboard?.eKey.wasPressedThisFrame == true ||
                 gamepad?.buttonSouth.wasPressedThisFrame == true))
            {
                OperateFocused();
            }

            if (mouse?.leftButton.wasPressedThisFrame == true)
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

            Vector3 towardCamera =
                _camera.transform.position - _feedbackLabel.transform.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                _feedbackLabel.transform.rotation = Quaternion.LookRotation(
                    towardCamera.normalized,
                    Vector3.up);
            }
        }

        private void OnDisable()
        {
            _model = null;
            ResetTransientFocus();
            LastInteractionRejected = false;
        }

        private bool QueueService()
        {
            if (!HasCurrentPresentation() ||
                !IsServiceReady ||
                _controller == null)
            {
                Reject("SCOUT SERVICE PENDANT STALE · NO WORK QUEUED");
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                SetFeedback(
                    _controller.IsScoutServiceQueued
                        ? "SCOUT SERVICE ALREADY QUEUED"
                        : "ACTION QUEUED · LET THE CITY LEDGER ACCEPT IT FIRST",
                    rejected: !_controller.IsScoutServiceQueued);
                return false;
            }

            if (!_controller.CanServiceScout)
            {
                Reject("SCOUT SERVICE TERMS UNAVAILABLE");
                return false;
            }

            _controller.ServiceScout();
            if (!_controller.IsScoutServiceQueued)
            {
                Reject(_controller.Status);
                return false;
            }

            _inputArmed = false;
            SetFeedback(
                "SCOUT SERVICE QUEUED · " +
                _model!.VehicleServicePartsCostUnits +
                " PARTS MOVE ON THE CITY TICK · " +
                _model.VehicleServiceReservePartsUnits +
                " STAY IN RESERVE",
                rejected: false);
            RefreshVisuals();
            return true;
        }

        private void UpdateInputArming(bool inputHeld)
        {
            if (!IsControlFocused || !CanAcceptInput)
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
                SetFeedback(ReadyFeedback(), rejected: false);
                RefreshVisuals();
            }
        }

        private bool HasCurrentPresentation()
        {
            return HasCurrentModel() &&
                   ShouldShowWitness(_model!) &&
                   _controller?.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.GarageBay &&
                   gameObject.activeInHierarchy;
        }

        private bool HasCurrentModel()
        {
            return _built &&
                   _controller?.HasActiveGame == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel);
        }

        private string ReadyFeedback()
        {
            if (!HasCurrentModel())
            {
                return "SCOUT SERVICE STOWED";
            }

            if (_controller?.IsScoutServiceQueued == true)
            {
                return "SCOUT SERVICE ALREADY QUEUED";
            }

            return "SCOUT WORN · " +
                   _model!.VehicleConditionMilli + " / " +
                   LastBearingBalanceV1.StartingVehicleConditionMilli +
                   "\nSERVICE " +
                   _model.VehicleServicePartsCostUnits +
                   " PARTS · HOLD " +
                   _model.VehicleServiceReservePartsUnits +
                   " IN RESERVE · E / GAMEPAD SOUTH / EXACT PENDANT";
        }

        private void ResetTransientFocus()
        {
            IsControlFocused = false;
            _inputArmed = false;
            _presentationActive = false;
            _presentationEntryFrame = -1;
        }

        private void RefreshVisuals()
        {
            bool hasCurrentModel = HasCurrentModel();
            bool showWitness =
                hasCurrentModel &&
                ShouldShowWitness(_model!);
            bool accepted =
                showWitness &&
                IsAcceptedWitness(_model!);
            _witnessRoot?.SetActive(showWitness);
            _target?.SetActive(showWitness && !accepted);
            _focusRail?.SetActive(
                showWitness &&
                !accepted &&
                IsControlFocused);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = Feedback;
                _feedbackLabel.color = LastInteractionRejected
                    ? new Color32(230, 103, 74, 255)
                    : new Color32(238, 221, 178, 255);
            }

            _garageBay?.ApplyScoutServicePresentation(
                showWitness,
                accepted,
                !hasCurrentModel ||
                _model!.VehicleConditionMilli >=
                    LastBearingBalanceV1.StartingVehicleConditionMilli);
        }

        private void SetFeedback(string feedback, bool rejected)
        {
            Feedback = feedback;
            LastInteractionRejected = rejected;
            RefreshVisuals();
        }

        private void Reject(string feedback)
        {
            SetFeedback(feedback, rejected: true);
        }

        private bool TryRaycastTarget(Vector2 screenPosition)
        {
            if (_camera == null || _targetCollider == null)
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
                if (_raycastHits[index].collider == _targetCollider)
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
    }
}
