#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Physical depot-threshold control. It follows the authored recovery
    /// anchor and delegates accepted input to the controller's existing verb.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingDepotApproachInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Seat The Depot Bridle Control [Derived Only]";
        public const string TargetName =
            "INTERACT_DEPOT_RECOVERY_BRIDLE_DOG";
        public const string FeedbackLabelName =
            "DEPOT_RECOVERY_BRIDLE_FEEDBACK";

        private const int RaycastBufferSize = 6;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _target;
        private GameObject? _focusRail;
        private Transform? _bridleDog;
        private TextMesh? _feedbackLabel;
        private BoxCollider? _targetCollider;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private int _presentationEntryFrame = -1;
        private bool _wasPresentationActive;
        private bool _inputArmed;
        private bool _built;
        private bool _operationQueued;

        public bool IsBuilt => _built;

        public bool IsFocused { get; private set; }

        public bool IsInputArmed => _inputArmed;

        public string Feedback { get; private set; } = string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public bool IsOperationQueued => _operationQueued;

        public bool IsTargetVisible => IsActive(_target);

        public bool IsHighlighted => IsActive(_focusRail);

        public bool HasDedicatedInteractionTarget =>
            _targetCollider != null &&
            _targetCollider.isTrigger &&
            _targetCollider.gameObject.layer == InteractionLayer;

        public Vector3 TargetWorldPosition =>
            _target?.transform.position ?? transform.position;

        internal void Build(
            Material iron,
            Material bone,
            Material tungsten,
            Material signal)
        {
            if (_built)
            {
                return;
            }

            iron = iron ?? throw new ArgumentNullException(nameof(iron));
            bone = bone ?? throw new ArgumentNullException(nameof(bone));
            tungsten = tungsten ??
                throw new ArgumentNullException(nameof(tungsten));
            signal = signal ??
                throw new ArgumentNullException(nameof(signal));

            _built = true;
            gameObject.name = RootName;
            _target = new GameObject(TargetName);
            _target.layer = InteractionLayer;
            _target.transform.SetParent(transform, false);
            _target.transform.localPosition = new Vector3(2.6f, 0f, 0f);
            _targetCollider = _target.AddComponent<BoxCollider>();
            _targetCollider.size = new Vector3(1.7f, 2.1f, 1.7f);
            _targetCollider.center = new Vector3(0f, 0.28f, 0f);
            _targetCollider.isTrigger = true;

            CreateVisual(
                "DEPOT_BRIDLE_DOG_PLINTH",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, -0.42f, 0f),
                new Vector3(1.16f, 0.18f, 0.92f),
                Quaternion.identity,
                iron);
            CreateVisual(
                "DEPOT_BRIDLE_TUNGSTEN_ORDER_PLATE",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, 0.12f, 0.12f),
                new Vector3(0.9f, 0.5f, 0.1f),
                Quaternion.Euler(16f, 0f, 0f),
                tungsten);
            var dog = new GameObject("DEPOT_RECOVERY_BRIDLE_DOG");
            dog.transform.SetParent(_target.transform, false);
            dog.transform.localPosition = new Vector3(0f, 0.34f, -0.08f);
            _bridleDog = dog.transform;
            CreateVisual(
                "DEPOT_BRIDLE_DOG_SHAFT",
                PrimitiveType.Cylinder,
                dog.transform,
                Vector3.zero,
                new Vector3(0.13f, 0.5f, 0.13f),
                Quaternion.Euler(0f, 0f, -22f),
                iron);
            CreateVisual(
                "DEPOT_BRIDLE_DOG_HANDLE",
                PrimitiveType.Cylinder,
                dog.transform,
                new Vector3(-0.22f, 0.43f, 0f),
                new Vector3(0.11f, 0.34f, 0.11f),
                Quaternion.Euler(0f, 0f, 90f),
                bone);
            _focusRail = CreateVisual(
                "DEPOT_BRIDLE_FOCUS_SIGNAL_RAIL",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, -0.29f, -0.47f),
                new Vector3(1.28f, 0.04f, 0.05f),
                Quaternion.identity,
                signal);

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(_target.transform, false);
            feedback.transform.localPosition = new Vector3(0f, 1.72f, 0f);
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
                !_model.IsDepotApproachRecoveryAvailable)
            {
                _presentationEntryFrame = -1;
                _wasPresentationActive = false;
                _inputArmed = false;
            }

            if (_operationQueued &&
                _controller?.IsDepotApproachRecoveryQueued != true)
            {
                _operationQueued = false;
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
            _operationQueued = false;
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
                Reject("DEPOT BRIDLE CONTROL STALE");
                return;
            }

            IsFocused = true;
            SetFeedback(ReadyFeedback(), rejected: false);
        }

        public bool OperateFocused()
        {
            if (!IsFocused)
            {
                Reject("FOCUS THE DEPOT BRIDLE DOG FIRST");
                return false;
            }

            return ActivateBridle();
        }

        public bool ActivateBridle()
        {
            if (!HasCurrentPresentation() ||
                _controller == null ||
                _model == null)
            {
                IsFocused = false;
                ApplyVisibility();
                Reject("DEPOT BRIDLE CONTROL STALE");
                return false;
            }

            if (!_inputArmed)
            {
                Reject("RELEASE CONTROLS · THEN SEAT THE BRIDLE");
                return false;
            }

            FocusControl();
            if (_controller.HasPendingPlayerCommands)
            {
                bool alreadyQueued =
                    _controller.IsDepotApproachRecoveryQueued;
                SetFeedback(
                    alreadyQueued
                        ? QueuedFeedback()
                        : "BRIDLE DOG HELD · LEDGER BUSY",
                    rejected: !alreadyQueued);
                return false;
            }

            if (!_controller.IsDepotApproachRecoveryAvailable)
            {
                Reject("DEPOT RECOVERY BRIDLE STALE");
                return false;
            }

            _controller.OperateDepotApproachRecoveryPoint();
            if (!_controller.IsDepotApproachRecoveryQueued)
            {
                Reject(
                    string.IsNullOrWhiteSpace(_controller.Status)
                        ? "DEPOT BRIDLE ACTION NOT QUEUED"
                        : _controller.Status);
                return false;
            }

            _operationQueued = true;
            IsFocused = true;
            SetFeedback(QueuedFeedback(), rejected: false);
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
                Reject("DEPOT BRIDLE HELD · UI HAS POINTER");
                return false;
            }

            if (!TryRaycastTarget(screenPosition))
            {
                return false;
            }

            FocusControl();
            return ActivateBridle();
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
                    "DEPOT RECOVERY BRIDLE\nRELEASE CONTROLS TO SET",
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

            if (_controller?.IsDepotApproachRecoveryQueued == true)
            {
                _operationQueued = true;
                IsFocused = true;
                SetFeedback(QueuedFeedback(), rejected: false);
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
                   _model.IsDepotApproachRecoveryAvailable &&
                   (_controller.IsDepotApproachRecoveryAvailable ||
                    _controller.IsDepotApproachRecoveryQueued) &&
                   _controller.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.Driving &&
                   gameObject.activeInHierarchy;
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
            if (_controller?.IsDepotApproachRecoveryQueued == true)
            {
                _operationQueued = true;
                Feedback = QueuedFeedback();
            }
            else
            {
                Feedback = ReadyFeedback();
            }

            LastInteractionRejected = false;
        }

        private static string ReadyFeedback()
        {
            return "SEAT DEPOT BRIDLE · OPEN THE CORRIDOR\nE / A OR POINTER";
        }

        private static string QueuedFeedback()
        {
            return "BRIDLE DOG THROWN\nLEDGER POSTS NEXT TICK";
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
            bool visible = IsTargetVisible;
            _focusRail?.SetActive(visible && IsFocused);
            if (_bridleDog != null)
            {
                _bridleDog.localRotation = _operationQueued
                    ? Quaternion.Euler(0f, 0f, -34f)
                    : Quaternion.identity;
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
