#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing.Vehicle
{
    /// <summary>
    /// Physical departure control for Sasha's fixed garage. It owns garage
    /// submit input and delegates the accepted intent to the existing
    /// controller composite without constructing game commands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingGarageDepartureInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Launch Dog Departure Control [Derived Only]";
        public const string TargetName =
            "INTERACT_SASHA_SCOUT_DEPARTURE_CLAMP";
        public const string FeedbackLabelName =
            "SASHA_SCOUT_DEPARTURE_FEEDBACK";

        private const int RaycastBufferSize = 4;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _target;
        private GameObject? _focusHalo;
        private TextMesh? _feedbackLabel;
        private BoxCollider? _targetCollider;
        private Transform? _launchDog;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private int _garageEntryFrame = -1;
        private bool _inputArmed;
        private bool _built;

        public bool IsFocused { get; private set; }

        public string Feedback { get; private set; } = string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public bool IsBuilt => _built;

        public bool IsInputArmed => _inputArmed;

        public bool IsTargetVisible =>
            _target?.activeInHierarchy == true;

        public bool IsHighlighted =>
            _focusHalo?.activeInHierarchy == true;

        public bool HasDedicatedInteractionTarget =>
            _targetCollider != null &&
            _targetCollider.isTrigger &&
            _targetCollider.gameObject.layer == InteractionLayer;

        public bool IsLaunchDogThrown { get; private set; }

        public Vector3 TargetWorldPosition =>
            _target?.transform.position ?? transform.position;

        internal void Build(
            Material darkIron,
            Material oxide,
            Material tungsten,
            Material signal)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            gameObject.name = RootName;
            darkIron = darkIron ??
                throw new ArgumentNullException(nameof(darkIron));
            oxide = oxide ??
                throw new ArgumentNullException(nameof(oxide));
            tungsten = tungsten ??
                throw new ArgumentNullException(nameof(tungsten));
            signal = signal ??
                throw new ArgumentNullException(nameof(signal));

            _target = new GameObject(TargetName);
            _target.layer = InteractionLayer;
            _target.transform.SetParent(transform, false);
            _targetCollider = _target.AddComponent<BoxCollider>();
            _targetCollider.size = new Vector3(1.55f, 1.4f, 1.25f);
            _targetCollider.center = new Vector3(0f, 0.12f, 0f);
            _targetCollider.isTrigger = true;

            CreateVisual(
                "LAUNCH_DOG_FLOOR_PLINTH",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, -0.38f, 0f),
                new Vector3(1.32f, 0.24f, 0.98f),
                Quaternion.identity,
                darkIron);
            CreateVisual(
                "MANIFEST_CLAMP_JAW",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, -0.08f, 0f),
                new Vector3(0.92f, 0.38f, 0.58f),
                Quaternion.Euler(0f, 0f, -8f),
                darkIron);
            var dog = new GameObject("LAUNCH_DOG");
            dog.transform.SetParent(_target.transform, false);
            dog.transform.localPosition = new Vector3(0.3f, 0.34f, 0f);
            _launchDog = dog.transform;
            CreateVisual(
                "LAUNCH_DOG_THICK_SHAFT",
                PrimitiveType.Cylinder,
                dog.transform,
                Vector3.zero,
                new Vector3(0.15f, 0.48f, 0.15f),
                Quaternion.identity,
                oxide);
            CreateVisual(
                "LAUNCH_DOG_T_HANDLE",
                PrimitiveType.Cylinder,
                dog.transform,
                new Vector3(0f, 0.5f, 0f),
                new Vector3(0.12f, 0.38f, 0.12f),
                Quaternion.Euler(0f, 0f, 90f),
                oxide);
            CreateVisual(
                "LAUNCH_DOG_TUNGSTEN_DETENT",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(-0.33f, 0.08f, 0f),
                new Vector3(0.18f, 0.18f, 0.18f),
                Quaternion.Euler(0f, 0f, 45f),
                tungsten);
            _focusHalo = CreateVisual(
                "DEPARTURE_CONTROL_FOCUS_HALO",
                PrimitiveType.Cylinder,
                _target.transform,
                new Vector3(0f, -0.48f, 0f),
                new Vector3(0.82f, 0.025f, 0.72f),
                Quaternion.identity,
                signal);

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(transform, false);
            feedback.transform.localPosition =
                new Vector3(0f, 1.42f, 0f);
            _feedbackLabel = feedback.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 48;
            _feedbackLabel.characterSize = 0.035f;
            _feedbackLabel.color = new Color32(238, 221, 178, 255);

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
            NormalizeState();
            ApplyVisibility();
            RefreshVisuals();
        }

        internal void Apply(LastBearingReadModel model)
        {
            _model = model ??
                throw new ArgumentNullException(nameof(model));
            NormalizeState();
            ApplyVisibility();
            RefreshVisuals();
            Physics.SyncTransforms();
        }

        public void ResetLocalFocus()
        {
            _model = null;
            IsFocused = false;
            Feedback = string.Empty;
            LastInteractionRejected = false;
            _hasPointerPosition = false;
            _inputArmed = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        public void FocusControl()
        {
            if (!HasCurrentPresentation())
            {
                IsFocused = false;
                Reject("DEPARTURE CONTROL STALE · no manifest queued");
                return;
            }

            IsFocused = true;
            SetFeedback(
                ReadyFeedback(),
                rejected: false);
        }

        public bool OperateFocused()
        {
            if (!IsFocused)
            {
                Reject("FOCUS SASHA'S LAUNCH CLAMP FIRST");
                return false;
            }

            return ActivateControl();
        }

        public bool ActivateControl()
        {
            if (!HasCurrentPresentation())
            {
                IsFocused = false;
                ApplyVisibility();
                Reject("DEPARTURE CONTROL STALE · no manifest queued");
                return false;
            }

            if (!_inputArmed)
            {
                Reject("RELEASE CONTROLS\nTHEN FOCUS DOG");
                return false;
            }

            FocusControl();
            if (_controller == null || _model == null)
            {
                Reject("DEPARTURE CONTROL STALE · no manifest queued");
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                SetFeedback(
                    _controller.IsExpeditionCommitQueued
                        ? "CLAMP THROWN\nLEDGER POSTS NEXT TICK"
                        : "CLAMP HELD · LEDGER BUSY",
                    rejected: !_controller.IsExpeditionCommitQueued);
                return false;
            }

            _controller.CommitExpedition();
            if (!_controller.IsExpeditionCommitQueued)
            {
                Reject(ReadyFeedback());
                return false;
            }

            SetFeedback(
                "CLAMP THROWN\nLEDGER POSTS NEXT TICK",
                rejected: false);
            return true;
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasCurrentPresentation())
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                    true ||
                _controller?.Hud?.BlocksWorldPointer(screenPosition) == true)
            {
                Reject("CLAMP HELD · GARAGE DESK");
                return false;
            }

            if (!TryRaycastTarget(screenPosition))
            {
                return false;
            }

            FocusControl();
            return ActivateControl();
        }

        private void Update()
        {
            if (!HasCurrentPresentation())
            {
                if (_model != null ||
                    IsFocused ||
                    Feedback.Length != 0)
                {
                    ResetLocalFocus();
                }

                return;
            }

            Keyboard? keyboard = Keyboard.current;
            Gamepad? gamepad = Gamepad.current;
            Mouse? mouse = Mouse.current;
            if (!_inputArmed)
            {
                bool controlsReleased =
                    keyboard?.eKey.isPressed != true &&
                    gamepad?.buttonSouth.isPressed != true &&
                    mouse?.leftButton.isPressed != true;
                if (Time.frameCount > _garageEntryFrame &&
                    controlsReleased)
                {
                    _inputArmed = true;
                    SetFeedback(
                        "LAUNCH DOG\nFOCUS · POINTER / D-PAD",
                        rejected: false);
                }

                return;
            }

            bool navigated =
                keyboard?.leftArrowKey.wasPressedThisFrame == true ||
                keyboard?.rightArrowKey.wasPressedThisFrame == true ||
                gamepad?.dpad.left.wasPressedThisFrame == true ||
                gamepad?.dpad.right.wasPressedThisFrame == true;
            if (navigated)
            {
                FocusControl();
            }

            bool submitted =
                keyboard?.eKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (submitted &&
                IsFocused &&
                _controller?.FieldDesk?.OwnsKeyboardFocus != true)
            {
                OperateFocused();
            }

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

            if (_controller?.FieldDesk?.BlocksWorldPointer(pointer) == true ||
                _controller?.Hud?.BlocksWorldPointer(pointer) == true)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryActivateAtScreenPosition(pointer);
            }
            else if (!submitted &&
                     !navigated &&
                     pointerMoved &&
                     TryRaycastTarget(pointer))
            {
                FocusControl();
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
            _garageEntryFrame = Time.frameCount;
            _inputArmed = false;
            IsFocused = false;
            _hasPointerPosition = false;
            NormalizeState();
            ApplyVisibility();
            RefreshVisuals();
        }

        private void OnDisable()
        {
            _model = null;
            IsFocused = false;
            Feedback = string.Empty;
            LastInteractionRejected = false;
            _hasPointerPosition = false;
            _inputArmed = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        private bool HasCurrentPresentation()
        {
            return HasReadyState() &&
                   _controller?.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.GarageBay &&
                   gameObject.activeInHierarchy;
        }

        private bool HasReadyState()
        {
            return _built &&
                   _controller?.HasActiveGame == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel) &&
                   _model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                   _model.TransactionPhase == TransactionPhase.None &&
                   _model.PreparationPhase == PreparationPhase.Ready &&
                   _model.PreparationChoice !=
                       PreparationChoice.Unselected &&
                   _model.PlannedModule != VehicleModule.None;
        }

        private void NormalizeState()
        {
            if (!HasReadyState())
            {
                IsFocused = false;
                Feedback = string.Empty;
                LastInteractionRejected = false;
                return;
            }

            if (Feedback.Length == 0)
            {
                Feedback = ReadyFeedback();
                LastInteractionRejected = false;
            }
        }

        private string ReadyFeedback()
        {
            if (_controller?.IsExpeditionCommitQueued == true)
            {
                return "DEPARTURE QUEUED · AUTHORITATIVE TICK PENDING";
            }

            if (_controller?.CanCommitExpedition == true)
            {
                return IsFocused
                    ? "LAUNCH DOG READY\nPULL · E / A"
                    : "LAUNCH DOG\nFOCUS · POINTER / D-PAD";
            }

            if (_model?.IsDustFrontAcknowledgementRequired == true ||
                _model?.PauseCause == PauseCause.DustFrontAlert)
            {
                return "CLAMP HELD · FRONT ALERT";
            }

            return _model != null &&
                   _model.FuelUnits <
                       LastBearingBalanceV1.RouteFuelCost(
                           _model.PlannedModule)
                ? "CLAMP HELD · FUEL SHORT"
                : "CLAMP HELD · CHECK FIELD DESK";
        }

        private void ApplyVisibility()
        {
            bool visible = HasReadyState();
            _target?.SetActive(visible);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.gameObject.SetActive(visible);
            }
        }

        private void RefreshVisuals()
        {
            IsLaunchDogThrown =
                _controller?.IsExpeditionCommitQueued == true;
            if (_launchDog != null)
            {
                _launchDog.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    IsLaunchDogThrown ? -72f : -10f);
            }

            _focusHalo?.SetActive(IsTargetVisible && IsFocused);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = Feedback;
                _feedbackLabel.color = LastInteractionRejected
                    ? new Color32(230, 103, 74, 255)
                    : new Color32(238, 221, 178, 255);
            }
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
