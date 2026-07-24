#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum WreckLineInteractionStage
    {
        None = 0,
        DeployWinch = 1,
        SealRangeTank = 2,
    }

    /// <summary>
    /// Physical Wreck Line control. It follows the authored interaction
    /// anchor and delegates accepted input to the controller's existing verbs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingWreckLineInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Work The Wreck Line Control [Derived Only]";
        public const string TargetName =
            "INTERACT_WRECK_LINE_WORK_DOG";
        public const string FeedbackLabelName =
            "WRECK_LINE_WORK_DOG_FEEDBACK";

        private const int RaycastBufferSize = 6;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _target;
        private GameObject? _focusRail;
        private GameObject? _winchGlyph;
        private GameObject? _tankGlyph;
        private BoxCollider? _targetCollider;
        private Transform? _workDog;
        private TextMesh? _feedbackLabel;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private int _presentationEntryFrame = -1;
        private bool _wasPresentationActive;
        private bool _inputArmed;
        private bool _built;
        private WreckLineInteractionStage _queuedStage;

        public bool IsBuilt => _built;

        public bool IsFocused { get; private set; }

        public bool IsInputArmed => _inputArmed;

        public string Feedback { get; private set; } = string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public WreckLineInteractionStage CurrentStage =>
            ResolveStage();

        public WreckLineInteractionStage QueuedStage => _queuedStage;

        public bool IsTargetVisible => IsActive(_target);

        public bool IsHighlighted => IsActive(_focusRail);

        public bool HasDedicatedInteractionTarget =>
            _targetCollider != null &&
            _targetCollider.isTrigger &&
            _targetCollider.gameObject.layer == InteractionLayer;

        public Vector3 TargetWorldPosition =>
            _target?.transform.position ?? transform.position;

        internal void Build(
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

            _built = true;
            gameObject.name = RootName;
            _target = new GameObject(TargetName);
            _target.layer = InteractionLayer;
            _target.transform.SetParent(transform, false);
            _targetCollider = _target.AddComponent<BoxCollider>();
            _targetCollider.size = new Vector3(2.6f, 2.5f, 2.6f);
            _targetCollider.center = new Vector3(0f, 0.35f, 0f);
            _targetCollider.isTrigger = true;

            CreateVisual(
                "WRECK_LINE_ACTUATOR_PLINTH",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, -0.48f, 0f),
                new Vector3(1.45f, 0.22f, 1.1f),
                Quaternion.identity,
                darkIron);
            CreateVisual(
                "WRECK_LINE_TUNGSTEN_ORDER_PLATE",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, 0.18f, 0.18f),
                new Vector3(1.1f, 0.62f, 0.12f),
                Quaternion.Euler(18f, 0f, 0f),
                tungsten);
            var dog = new GameObject("WRECK_LINE_WORK_DOG");
            dog.transform.SetParent(_target.transform, false);
            dog.transform.localPosition = new Vector3(0f, 0.42f, -0.08f);
            _workDog = dog.transform;
            CreateVisual(
                "WRECK_LINE_WORK_DOG_SHAFT",
                PrimitiveType.Cylinder,
                dog.transform,
                Vector3.zero,
                new Vector3(0.14f, 0.58f, 0.14f),
                Quaternion.Euler(0f, 0f, -24f),
                oxide);
            CreateVisual(
                "WRECK_LINE_WORK_DOG_HANDLE",
                PrimitiveType.Cylinder,
                dog.transform,
                new Vector3(-0.24f, 0.5f, 0f),
                new Vector3(0.12f, 0.38f, 0.12f),
                Quaternion.Euler(0f, 0f, 90f),
                oxide);
            _focusRail = CreateVisual(
                "WRECK_LINE_FOCUS_SIGNAL_RAIL",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, -0.33f, -0.56f),
                new Vector3(1.62f, 0.045f, 0.055f),
                Quaternion.identity,
                signal);
            _winchGlyph = CreateVisual(
                "WRECK_LINE_WINCH_GLYPH",
                PrimitiveType.Cylinder,
                _target.transform,
                new Vector3(-0.32f, 0.18f, 0.1f),
                new Vector3(0.2f, 0.08f, 0.2f),
                Quaternion.Euler(90f, 0f, 0f),
                oxide);
            _tankGlyph = CreateVisual(
                "WRECK_LINE_TANK_SEAL_GLYPH",
                PrimitiveType.Cylinder,
                _target.transform,
                new Vector3(0.32f, 0.18f, 0.1f),
                new Vector3(0.22f, 0.08f, 0.22f),
                Quaternion.Euler(90f, 0f, 0f),
                bone);
            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(_target.transform, false);
            feedback.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            _feedbackLabel = feedback.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 48;
            _feedbackLabel.characterSize = 0.038f;
            _feedbackLabel.color = new Color32(236, 216, 174, 255);

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
            if (!ReferenceEquals(
                    _model,
                    _controller?.RuntimeReadModel) ||
                ResolveStage() == WreckLineInteractionStage.None)
            {
                _presentationEntryFrame = -1;
                _wasPresentationActive = false;
                _inputArmed = false;
            }

            if (_queuedStage != WreckLineInteractionStage.None &&
                !IsStageQueued(_queuedStage))
            {
                _queuedStage = WreckLineInteractionStage.None;
            }

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
            _queuedStage = WreckLineInteractionStage.None;
            _hasPointerPosition = false;
            _presentationEntryFrame = -1;
            _wasPresentationActive = false;
            _inputArmed = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        public void FocusControl()
        {
            if (!HasCurrentPresentation())
            {
                IsFocused = false;
                Reject("WRECK LINE CONTROL STALE");
                return;
            }

            IsFocused = true;
            SetFeedback(ReadyFeedback(), rejected: false);
        }

        public bool OperateFocused()
        {
            if (!IsFocused)
            {
                Reject("FOCUS THE WRECK LINE WORK DOG FIRST");
                return false;
            }

            return ActivateCurrentStage();
        }

        public bool ActivateCurrentStage()
        {
            if (!HasCurrentPresentation() ||
                _controller == null ||
                _model == null)
            {
                IsFocused = false;
                ApplyVisibility();
                Reject("WRECK LINE CONTROL STALE");
                return false;
            }

            if (!_inputArmed)
            {
                Reject("RELEASE CONTROLS · THEN WORK THE DOG");
                return false;
            }

            FocusControl();
            WreckLineInteractionStage stage = ResolveStage();
            if (_controller.HasPendingPlayerCommands)
            {
                bool alreadyQueued = IsStageQueued(stage);
                SetFeedback(
                    alreadyQueued
                        ? QueuedFeedback(stage)
                        : "WORK DOG HELD · LEDGER BUSY",
                    rejected: !alreadyQueued);
                return false;
            }

            if (!_controller.IsWreckLineModuleOperationAvailable)
            {
                Reject("MODULE OPERATION STALE");
                return false;
            }

            _controller.OperateWreckLineModulePoint();

            if (!IsStageQueued(stage))
            {
                Reject(
                    string.IsNullOrWhiteSpace(_controller.Status)
                        ? "WRECK LINE ACTION NOT QUEUED"
                        : _controller.Status);
                return false;
            }

            _queuedStage = stage;
            IsFocused = true;
            SetFeedback(QueuedFeedback(stage), rejected: false);
            RefreshVisuals();
            return true;
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasCurrentPresentation())
            {
                return false;
            }

            if (PointerIsBlocked(screenPosition))
            {
                Reject("WRECK LINE CONTROL HELD · UI HAS POINTER");
                return false;
            }

            if (!TryRaycastTarget(screenPosition))
            {
                return false;
            }

            FocusControl();
            return ActivateCurrentStage();
        }

        private void Update()
        {
            if (!HasCurrentPresentation())
            {
                if (IsFocused || Feedback.Length != 0)
                {
                    IsFocused = false;
                    Feedback = string.Empty;
                    LastInteractionRejected = false;
                    ApplyVisibility();
                    RefreshVisuals();
                }

                _wasPresentationActive = false;
                _inputArmed = false;
                _presentationEntryFrame = -1;
                _hasPointerPosition = false;
                return;
            }

            if (!_wasPresentationActive)
            {
                _wasPresentationActive = true;
                _presentationEntryFrame = Time.frameCount;
                _inputArmed = false;
                IsFocused = true;
                SetFeedback(
                    "WRECK LINE WORK DOG\nRELEASE CONTROLS TO SET",
                    rejected: false);
                ApplyVisibility();
                RefreshVisuals();
                Physics.SyncTransforms();
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
                if (Time.frameCount > _presentationEntryFrame &&
                    controlsReleased)
                {
                    _inputArmed = true;
                    FocusControl();
                }

                return;
            }

            WreckLineInteractionStage stage = ResolveStage();
            if (IsStageQueued(stage))
            {
                _queuedStage = stage;
                IsFocused = true;
                SetFeedback(QueuedFeedback(stage), rejected: false);
            }
            else if (!IsFocused)
            {
                FocusControl();
            }

            bool keyboardOwned =
                _controller?.FieldDesk?.OwnsKeyboardFocus == true;
            bool navigated =
                !keyboardOwned &&
                (keyboard?.leftArrowKey.wasPressedThisFrame == true ||
                 keyboard?.rightArrowKey.wasPressedThisFrame == true ||
                 gamepad?.dpad.left.wasPressedThisFrame == true ||
                 gamepad?.dpad.right.wasPressedThisFrame == true);
            if (navigated)
            {
                FocusControl();
            }

            bool submitted =
                keyboard?.eKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (submitted && !keyboardOwned)
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

            if (PointerIsBlocked(pointer))
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
            Vector3 towardCamera = _camera.transform.position - label.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                label.rotation = Quaternion.LookRotation(
                    towardCamera.normalized,
                    Vector3.up);
            }
        }

        private void OnDisable()
        {
            ResetLocalFocus();
        }

        private bool HasCurrentPresentation()
        {
            return _built &&
                   _controller?.HasActiveGame == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel) &&
                   ResolveStage() != WreckLineInteractionStage.None &&
                   _controller.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.Driving &&
                   gameObject.activeInHierarchy;
        }

        private WreckLineInteractionStage ResolveStage()
        {
            if (_model?.IsWreckLineModulePointAvailable != true)
            {
                return WreckLineInteractionStage.None;
            }

            return _model.RouteActionKind switch
            {
                RouteActionKind.DeployWinch =>
                    WreckLineInteractionStage.DeployWinch,
                RouteActionKind.CrossExposedDustRoute =>
                    WreckLineInteractionStage.SealRangeTank,
                _ => WreckLineInteractionStage.None,
            };
        }

        private bool IsStageQueued(WreckLineInteractionStage stage)
        {
            if (_controller == null)
            {
                return false;
            }

            return (stage == WreckLineInteractionStage.DeployWinch ||
                    stage == WreckLineInteractionStage.SealRangeTank) &&
                   _controller.IsWreckLineModuleOperationQueued;
        }

        private void NormalizeState()
        {
            if (!HasCurrentPresentation())
            {
                IsFocused = false;
                Feedback = string.Empty;
                LastInteractionRejected = false;
                return;
            }

            IsFocused = true;
            WreckLineInteractionStage stage = ResolveStage();
            if (IsStageQueued(stage))
            {
                _queuedStage = stage;
                Feedback = QueuedFeedback(stage);
            }
            else
            {
                Feedback = ReadyFeedback();
            }

            LastInteractionRejected = false;
        }

        private string ReadyFeedback()
        {
            return ResolveStage() switch
            {
                WreckLineInteractionStage.DeployWinch =>
                    "SEAT WINCH · WORK THE WRECK LINE\nE / A OR POINTER",
                WreckLineInteractionStage.SealRangeTank =>
                    "LOCK TANK SEALS · CROSS THE DUST LINE\nE / A OR POINTER",
                _ => string.Empty,
            };
        }

        private static string QueuedFeedback(
            WreckLineInteractionStage stage)
        {
            return stage switch
            {
                WreckLineInteractionStage.DeployWinch =>
                    "WINCH DOG THROWN\nLEDGER POSTS NEXT TICK",
                WreckLineInteractionStage.SealRangeTank =>
                    "TANK SEALS CLAMPED\nLEDGER POSTS NEXT TICK",
                _ => "WRECK LINE ACTION QUEUED",
            };
        }

        private void ApplyVisibility()
        {
            bool visible = HasCurrentPresentation();
            _target?.SetActive(visible);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.gameObject.SetActive(visible);
            }
        }

        private void RefreshVisuals()
        {
            WreckLineInteractionStage stage = ResolveStage();
            bool visible = IsTargetVisible;
            if (_target != null)
            {
                _target.transform.localPosition = stage switch
                {
                    WreckLineInteractionStage.DeployWinch =>
                        new Vector3(2.6f, 0f, 0f),
                    WreckLineInteractionStage.SealRangeTank =>
                        new Vector3(2.6f, 0f, 0f),
                    _ => Vector3.zero,
                };
            }

            _focusRail?.SetActive(visible && IsFocused);
            _winchGlyph?.SetActive(
                visible && stage == WreckLineInteractionStage.DeployWinch);
            _tankGlyph?.SetActive(
                visible && stage == WreckLineInteractionStage.SealRangeTank);
            if (_workDog != null)
            {
                _workDog.localRotation =
                    _queuedStage == WreckLineInteractionStage.None
                        ? Quaternion.identity
                        : Quaternion.Euler(0f, 0f, -32f);
            }

            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = Feedback;
                _feedbackLabel.color = LastInteractionRejected
                    ? new Color32(230, 103, 74, 255)
                    : new Color32(236, 216, 174, 255);
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

        private bool PointerIsBlocked(Vector2 screenPosition)
        {
            return
                _controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                    true ||
                _controller?.Hud?.BlocksWorldPointer(screenPosition) == true;
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

        private static bool IsActive(GameObject? value)
        {
            return value != null && value.activeInHierarchy;
        }
    }
}
