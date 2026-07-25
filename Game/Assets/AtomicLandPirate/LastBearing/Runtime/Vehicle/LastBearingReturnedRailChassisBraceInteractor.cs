#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing.Vehicle
{
    /// <summary>
    /// Garage-local jig for turning the credited Wreck Line rails into one
    /// derived Scout brace. It owns fresh physical input and witness
    /// presentation only; the controller remains the sole command seam.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingReturnedRailChassisBraceInteractor :
        MonoBehaviour
    {
        public const int InteractionLayer =
            LastBearingGarageDepartureInteractor.InteractionLayer;
        public const string RootName =
            "Returned Rail Chassis Brace Jig [Derived Only]";
        public const string TargetName =
            "INTERACT_INSTALL_RETURNED_RAIL_CHASSIS_BRACE";
        public const string FocusRailName =
            "RETURNED_RAIL_CHASSIS_BRACE_JIG_FOCUS_RAIL";
        public const string FeedbackLabelName =
            "RETURNED_RAIL_CHASSIS_BRACE_RECEIPT";

        private const int RaycastBufferSize = 8;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];
        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
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

        public bool IsInstallReady =>
            HasCurrentModel() &&
            _model!.IsReturnedRailChassisBraceInstallAvailable;

        public bool IsWitnessVisible =>
            _witnessRoot?.activeInHierarchy == true;

        public bool IsControlVisible =>
            _target?.activeInHierarchy == true;

        public bool IsFocusRailVisible =>
            _focusRail?.activeInHierarchy == true;

        public bool IsAcceptedReceiptVisible =>
            IsWitnessVisible &&
            HasCurrentModel() &&
            _model!.ReturnedRailChassisBraceInstalled;

        public bool HasDedicatedInteractionTarget =>
            _targetCollider != null &&
            _targetCollider.isTrigger &&
            _targetCollider.gameObject.layer == InteractionLayer;

        public Vector3 ControlWorldPosition =>
            _targetCollider?.bounds.center ??
            _target?.transform.position ??
            transform.position;

        public string Feedback { get; private set; } =
            "RETURNED RAIL BRACE JIG STOWED";

        public string ReceiptLabel =>
            _feedbackLabel?.text ?? string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public long DisplayedCostPartsUnits =>
            HasCurrentModel()
                ? _model!.ReturnedRailChassisBracePartsCostUnits
                : 0;

        public long DisplayedProtectionMilli =>
            HasCurrentModel()
                ? _model!.ReturnedRailChassisBraceProtectionMilli
                : 0;

        public long DisplayedProjectedConditionLossMilli =>
            HasCurrentModel()
                ? _model!.ProjectedRoundTripConditionLossMilli
                : 0;

        private bool CanAcceptInput =>
            IsInstallReady &&
            _controller?.HasPendingPlayerCommands != true;

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

            _built = true;
            gameObject.name = RootName;
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
                "RETURNED_RAIL_CHASSIS_BRACE_JIG_WITNESS");
            _witnessRoot.transform.SetParent(transform, false);

            CreateVisual(
                "BRACE_JIG_IRON_BED",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(0f, 0.24f, 0f),
                new Vector3(2.35f, 0.18f, 1.22f),
                Quaternion.identity,
                darkIron);
            CreateVisual(
                "BRACE_JIG_OXIDE_SHOE_LEFT",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(-0.86f, 0.52f, 0f),
                new Vector3(0.22f, 0.58f, 0.9f),
                Quaternion.Euler(0f, 0f, -9f),
                oxide);
            CreateVisual(
                "BRACE_JIG_OXIDE_SHOE_RIGHT",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(0.86f, 0.52f, 0f),
                new Vector3(0.22f, 0.58f, 0.9f),
                Quaternion.Euler(0f, 0f, 9f),
                oxide);
            CreateVisual(
                "BRACE_JIG_BONE_WORK_ORDER",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(0f, 0.84f, 0.42f),
                new Vector3(1.42f, 0.72f, 0.08f),
                Quaternion.Euler(-14f, 0f, 0f),
                bone);
            CreateVisual(
                "BRACE_JIG_SIGNAL_COST_NOTCH",
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(-0.35f, 0.93f, 0.34f),
                new Vector3(0.14f, 0.28f, 0.06f),
                Quaternion.Euler(-14f, 0f, 0f),
                signal);

            _target = new GameObject(TargetName);
            _target.layer = InteractionLayer;
            _target.transform.SetParent(_witnessRoot.transform, false);
            _target.transform.localPosition =
                new Vector3(0f, 0.58f, -0.68f);
            _targetCollider = _target.AddComponent<BoxCollider>();
            _targetCollider.size = new Vector3(1.52f, 1.1f, 0.72f);
            _targetCollider.isTrigger = true;
            CreateVisual(
                "BRACE_JIG_TUNGSTEN_DOG",
                PrimitiveType.Cylinder,
                _target.transform,
                new Vector3(0f, 0f, 0f),
                new Vector3(0.24f, 0.48f, 0.24f),
                Quaternion.Euler(0f, 0f, 90f),
                tungsten);
            CreateVisual(
                "BRACE_JIG_OXIDE_HANDLE",
                PrimitiveType.Cube,
                _target.transform,
                new Vector3(0f, 0.32f, 0f),
                new Vector3(0.86f, 0.14f, 0.14f),
                Quaternion.identity,
                oxide);

            _focusRail = CreateVisual(
                FocusRailName,
                PrimitiveType.Cube,
                _witnessRoot.transform,
                new Vector3(0f, 0.04f, -0.54f),
                new Vector3(2.08f, 0.08f, 0.08f),
                Quaternion.identity,
                tungsten);

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(_witnessRoot.transform, false);
            feedback.transform.localPosition =
                new Vector3(0f, 1.62f, 0f);
            _feedbackLabel = feedback.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 48;
            _feedbackLabel.characterSize = 0.028f;
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
            else if (model.ReturnedRailChassisBraceInstalled)
            {
                IsControlFocused = false;
                _inputArmed = false;
                _presentationActive = false;
                SetFeedback(
                    "RETURNED RAIL BRACE INSTALLED\n-" +
                    model.ReturnedRailChassisBraceProtectionMilli +
                    " PROJECTED CONDITION LOSS · " +
                    model.ProjectedRoundTripConditionLossMilli +
                    " REMAINS",
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
                Reject("RETURNED RAIL BRACE JIG UNAVAILABLE");
                return false;
            }

            IsControlFocused = true;
            _inputArmed = false;
            _presentationActive = true;
            _presentationEntryFrame = Time.frameCount;
            SetFeedback(
                "BRACE JIG FOCUSED · RELEASE CONTROL\nTHEN THROW THE DOG",
                rejected: false);
            RefreshVisuals();
            return true;
        }

        public bool OperateFocused()
        {
            if (!IsControlFocused)
            {
                Reject("FOCUS THE RETURNED RAIL BRACE JIG FIRST");
                return false;
            }

            if (!IsInputArmed)
            {
                Reject("RELEASE CONTROL · THEN INSTALL THE BRACE");
                return false;
            }

            return QueueInstall();
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
                Reject("GARAGE DESK HAS THIS POINTER · BRACE JIG IGNORED");
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
            Feedback = "RETURNED RAIL BRACE JIG STOWED";
            LastInteractionRejected = false;
            RefreshVisuals();
        }

        internal static bool ShouldShowWitness(LastBearingReadModel model)
        {
            return model.IsReturnedRailChassisBraceInstallAvailable ||
                   model.ReturnedRailChassisBraceInstalled;
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

        private bool QueueInstall()
        {
            if (!HasCurrentPresentation() ||
                !IsInstallReady ||
                _controller == null)
            {
                Reject("RETURNED RAIL BRACE JIG STALE · NO WORK QUEUED");
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                SetFeedback(
                    _controller.IsReturnedRailChassisBraceInstallQueued
                        ? "RETURNED RAIL BRACE ALREADY QUEUED"
                        : "ACTION QUEUED · LET THE CITY LEDGER ACCEPT IT FIRST",
                    rejected:
                        !_controller
                            .IsReturnedRailChassisBraceInstallQueued);
                return false;
            }

            if (!_controller.CanInstallReturnedRailChassisBrace)
            {
                Reject("RETURNED RAIL BRACE TERMS UNAVAILABLE");
                return false;
            }

            _controller.InstallReturnedRailChassisBrace();
            if (!_controller.IsReturnedRailChassisBraceInstallQueued)
            {
                Reject(_controller.Status);
                return false;
            }

            _inputArmed = false;
            SetFeedback(
                "RETURNED RAIL BRACE QUEUED · " +
                _model!.ReturnedRailChassisBracePartsCostUnits +
                " PARTS MOVE ON THE CITY TICK · " +
                _model.ReturnedRailChassisBraceProtectionMilli +
                " CONDITION PROTECTION",
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
                return "RETURNED RAIL BRACE JIG STOWED";
            }

            if (_controller?.IsReturnedRailChassisBraceInstallQueued == true)
            {
                return "RETURNED RAIL BRACE ALREADY QUEUED";
            }

            long projectedAfter = Math.Max(
                0,
                _model!.ProjectedRoundTripConditionLossMilli -
                _model.ReturnedRailChassisBraceProtectionMilli);
            return "4 RETURNED RAILS · " +
                   _model.ReturnedRailChassisBracePartsCostUnits +
                   " PARTS\n" +
                   _model.ReturnedRailChassisBraceProtectionMilli +
                   " CONDITION PROTECTION · PROJECTED " +
                   projectedAfter +
                   " · E / GAMEPAD SOUTH / EXACT JIG";
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
                _model!.ReturnedRailChassisBraceInstalled;
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
