#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing.Vehicle
{
    public enum GarageModuleStation
    {
        None = 0,
        WinchAssembly = 1,
        SealedRangeTank = 2,
    }

    /// <summary>
    /// Physical module selectors for Sasha's fixed garage. This derived-only
    /// adapter owns garage input and delegates accepted choices to the
    /// controller's existing composite.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingGarageModuleInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Garage Module Choice Controls [Derived Only]";
        public const string WinchTargetName =
            "INTERACT_GARAGE_MODULE_WINCH_ASSEMBLY";
        public const string RangeTankTargetName =
            "INTERACT_GARAGE_MODULE_SEALED_RANGE_TANK";
        public const string FeedbackLabelName =
            "GARAGE_MODULE_CHOICE_FEEDBACK";

        private const int RaycastBufferSize = 8;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _winchTarget;
        private GameObject? _rangeTankTarget;
        private GameObject? _winchFocusHalo;
        private GameObject? _rangeTankFocusHalo;
        private BoxCollider? _winchTargetCollider;
        private BoxCollider? _rangeTankTargetCollider;
        private TextMesh? _feedbackLabel;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private int _garageEntryFrame = -1;
        private bool _wasPresentationActive;
        private bool _inputArmed;
        private bool _built;
        private VehicleModule _queuedModule = VehicleModule.None;

        public bool IsBuilt => _built;

        public bool IsInputArmed => _inputArmed;

        public VehicleModule FocusedModule { get; private set; } =
            VehicleModule.None;

        public GarageModuleStation FocusedStation =>
            ToStation(FocusedModule);

        public GarageModuleStation QueuedStation =>
            ToStation(_queuedModule);

        public string Feedback { get; private set; } = string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public VehicleModule QueuedModule => _queuedModule;

        public bool HasDedicatedInteractionTargets =>
            IsDedicatedTarget(_winchTargetCollider) &&
            IsDedicatedTarget(_rangeTankTargetCollider);

        public bool IsWinchTargetVisible =>
            _winchTarget?.activeInHierarchy == true;

        public bool IsRangeTankTargetVisible =>
            _rangeTankTarget?.activeInHierarchy == true;

        public bool IsWinchHighlighted =>
            _winchFocusHalo?.activeInHierarchy == true;

        public bool IsRangeTankHighlighted =>
            _rangeTankFocusHalo?.activeInHierarchy == true;

        public Vector3 WinchTargetWorldPosition =>
            _winchTarget?.transform.position ?? transform.position;

        public Vector3 RangeTankTargetWorldPosition =>
            _rangeTankTarget?.transform.position ?? transform.position;

        public Vector3 TargetWorldPosition(GarageModuleStation station)
        {
            return station == GarageModuleStation.WinchAssembly
                ? WinchTargetWorldPosition
                : station == GarageModuleStation.SealedRangeTank
                    ? RangeTankTargetWorldPosition
                    : transform.position;
        }

        internal void Build(
            Transform winchStand,
            Transform rangeTankStand,
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

            winchStand = winchStand ??
                throw new ArgumentNullException(nameof(winchStand));
            rangeTankStand = rangeTankStand ??
                throw new ArgumentNullException(nameof(rangeTankStand));
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
            BoxCollider winchCollider;
            GameObject winchFocusHalo;
            _winchTarget = BuildTarget(
                WinchTargetName,
                winchStand,
                darkIron,
                oxide,
                tungsten,
                signal,
                out winchCollider,
                out winchFocusHalo);
            _winchTargetCollider = winchCollider;
            _winchFocusHalo = winchFocusHalo;
            BoxCollider rangeTankCollider;
            GameObject rangeTankFocusHalo;
            _rangeTankTarget = BuildTarget(
                RangeTankTargetName,
                rangeTankStand,
                darkIron,
                bone,
                tungsten,
                signal,
                out rangeTankCollider,
                out rangeTankFocusHalo);
            _rangeTankTargetCollider = rangeTankCollider;
            _rangeTankFocusHalo = rangeTankFocusHalo;

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(transform, false);
            feedback.transform.localPosition =
                new Vector3(3.05f, 2.82f, 1.725f);
            _feedbackLabel = feedback.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 48;
            _feedbackLabel.characterSize = 0.034f;
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
            if (IsSupportedModule(_queuedModule) &&
                (_model.PreparationChoice != PreparationChoice.Unselected ||
                 _controller?.HasPendingPlayerCommands != true))
            {
                _queuedModule = VehicleModule.None;
            }

            NormalizeState();
            ApplyVisibility();
            RefreshVisuals();
            Physics.SyncTransforms();
        }

        public void ResetLocalFocus()
        {
            _model = null;
            FocusedModule = VehicleModule.None;
            _queuedModule = VehicleModule.None;
            Feedback = string.Empty;
            LastInteractionRejected = false;
            _hasPointerPosition = false;
            _wasPresentationActive = false;
            _inputArmed = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        public bool MoveFocus(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            VehicleModule next = direction < 0
                ? VehicleModule.WinchAssembly
                : VehicleModule.SealedRangeTank;

            return FocusModule(next);
        }

        public bool FocusModule(VehicleModule module)
        {
            if (!IsSupportedModule(module))
            {
                Reject("UNKNOWN RIG MODULE");
                return false;
            }

            if (!HasCurrentPresentation())
            {
                FocusedModule = VehicleModule.None;
                Reject("MODULE STANDS STALE · REOPEN GARAGE PLAN");
                return false;
            }

            FocusedModule = module;
            SetFeedback(ReadyFeedback(), rejected: false);
            return true;
        }

        public bool OperateFocused()
        {
            if (!IsSupportedModule(FocusedModule))
            {
                Reject("FOCUS A RIG MODULE FIRST");
                return false;
            }

            return ActivateModule(FocusedModule);
        }

        public bool ActivateWinch()
        {
            return ActivateModule(VehicleModule.WinchAssembly);
        }

        public bool ActivateRangeTank()
        {
            return ActivateModule(VehicleModule.SealedRangeTank);
        }

        public bool ActivateModule(VehicleModule module)
        {
            if (!HasCurrentPresentation())
            {
                FocusedModule = VehicleModule.None;
                ApplyVisibility();
                Reject("MODULE STANDS STALE · REOPEN GARAGE PLAN");
                return false;
            }

            if (!_inputArmed)
            {
                Reject("RELEASE CONTROLS\nTHEN FOCUS MODULE");
                return false;
            }

            if (IsSupportedModule(_queuedModule))
            {
                SetFeedback(QueuedFeedback(), rejected: false);
                return false;
            }

            if (!FocusModule(module) ||
                _controller == null ||
                _model == null)
            {
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                Reject("MODULE CLAMPS HELD · LEDGER BUSY");
                return false;
            }

            if (!_controller.IsGaragePlanCommitAvailable)
            {
                Reject("MODULE PLAN STALE · CHECK FIELD DESK");
                return false;
            }

            _controller.CommitGaragePlan(module);
            bool accepted =
                _controller.HasPendingPlayerCommands &&
                !_controller.IsGaragePlanIntentActive;
            if (!accepted)
            {
                string status = _controller.Status;
                Reject(
                    string.IsNullOrWhiteSpace(status)
                        ? "MODULE PLAN NOT COMMITTED"
                        : status);
                return false;
            }

            _queuedModule = module;
            FocusedModule = module;
            SetFeedback(QueuedFeedback(), rejected: false);
            ApplyVisibility();
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
                Reject("MODULE CLAMPS HELD · GARAGE DESK");
                return false;
            }

            if (!TryRaycastModule(screenPosition, out VehicleModule module))
            {
                return false;
            }

            return ActivateModule(module);
        }

        private void Update()
        {
            bool presentationActive = HasCurrentPresentation();
            if (!presentationActive)
            {
                if (_wasPresentationActive ||
                    FocusedModule != VehicleModule.None ||
                    Feedback.Length != 0)
                {
                    FocusedModule = VehicleModule.None;
                    Feedback = string.Empty;
                    LastInteractionRejected = false;
                    RefreshVisuals();
                }

                _wasPresentationActive = false;
                _inputArmed = false;
                _hasPointerPosition = false;
                ApplyVisibility();
                return;
            }

            if (!_wasPresentationActive)
            {
                _wasPresentationActive = true;
                _garageEntryFrame = Time.frameCount;
                _inputArmed = false;
                FocusedModule = VehicleModule.None;
                SetFeedback(
                    "RIG MODULES\nRELEASE CONTROLS TO FOCUS",
                    rejected: false);
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
                    SetFeedback(ReadyFeedback(), rejected: false);
                }

                return;
            }

            bool keyboardOwned =
                _controller?.FieldDesk?.OwnsKeyboardFocus == true;
            bool focusWinch =
                keyboard?.leftArrowKey.wasPressedThisFrame == true ||
                keyboard?.upArrowKey.wasPressedThisFrame == true ||
                gamepad?.dpad.left.wasPressedThisFrame == true ||
                gamepad?.dpad.up.wasPressedThisFrame == true;
            bool focusRangeTank =
                keyboard?.rightArrowKey.wasPressedThisFrame == true ||
                keyboard?.downArrowKey.wasPressedThisFrame == true ||
                gamepad?.dpad.right.wasPressedThisFrame == true ||
                gamepad?.dpad.down.wasPressedThisFrame == true;
            bool navigated = !keyboardOwned &&
                (focusWinch || focusRangeTank);
            if (navigated)
            {
                MoveFocus(focusWinch ? -1 : 1);
            }

            bool submitted =
                keyboard?.eKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (submitted &&
                !keyboardOwned &&
                IsSupportedModule(FocusedModule))
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
                     TryRaycastModule(pointer, out VehicleModule hovered))
            {
                FocusModule(hovered);
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
            _wasPresentationActive = false;
            _inputArmed = false;
            FocusedModule = VehicleModule.None;
            _hasPointerPosition = false;
            NormalizeState();
            ApplyVisibility();
            RefreshVisuals();
        }

        private void OnDisable()
        {
            _model = null;
            FocusedModule = VehicleModule.None;
            _queuedModule = VehicleModule.None;
            Feedback = string.Empty;
            LastInteractionRejected = false;
            _hasPointerPosition = false;
            _wasPresentationActive = false;
            _inputArmed = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        private bool HasCurrentPresentation()
        {
            return (HasLiveGaragePlan() || HasQueuedGaragePlan()) &&
                   _controller?.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.GarageBay &&
                   gameObject.activeInHierarchy;
        }

        private bool HasLiveGaragePlan()
        {
            return _built &&
                   _controller?.HasActiveGame == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel) &&
                   _model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                   _model.TransactionPhase == TransactionPhase.None &&
                   _model.PreparationChoice ==
                       PreparationChoice.Unselected &&
                   IsSupportedPreparation(
                       _controller.GaragePreparationIntent) &&
                   _controller.IsGaragePlanIntentActive &&
                   _controller.IsGaragePlanCommitAvailable;
        }

        private bool HasQueuedGaragePlan()
        {
            return _built &&
                   _controller?.HasActiveGame == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel) &&
                   _model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                   _model.TransactionPhase == TransactionPhase.None &&
                   _model.PreparationChoice ==
                       PreparationChoice.Unselected &&
                   _model.PlannedModule == VehicleModule.None &&
                   IsSupportedModule(_queuedModule) &&
                   _controller.HasPendingPlayerCommands;
        }

        private void NormalizeState()
        {
            if (!HasCurrentPresentation())
            {
                FocusedModule = VehicleModule.None;
                Feedback = string.Empty;
                LastInteractionRejected = false;
                return;
            }

            if (IsSupportedModule(_queuedModule))
            {
                FocusedModule = _queuedModule;
                Feedback = QueuedFeedback();
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
            if (IsSupportedModule(_queuedModule))
            {
                return QueuedFeedback();
            }

            if (_controller?.HasPendingPlayerCommands == true)
            {
                return "RIG LEDGER BUSY";
            }

            if (FocusedModule == VehicleModule.WinchAssembly)
            {
                return "WINCH ASSEMBLY · SHORT CUTS\nFIT · E / A";
            }

            if (FocusedModule == VehicleModule.SealedRangeTank)
            {
                return "SEALED RANGE TANK · LONG HAUL\nFIT · E / A";
            }

            return _controller?.GaragePreparationIntent ==
                   PreparationChoice.WorkshopPush
                ? "WORKSHOP PUSH · CHOOSE RIG MODULE\nPOINTER / D-PAD"
                : "CIVIC BUFFER · CHOOSE RIG MODULE\nPOINTER / D-PAD";
        }

        private void ApplyVisibility()
        {
            bool visible = HasCurrentPresentation();
            _winchTarget?.SetActive(visible);
            _rangeTankTarget?.SetActive(visible);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.gameObject.SetActive(visible);
            }
        }

        private void RefreshVisuals()
        {
            _winchFocusHalo?.SetActive(
                IsWinchTargetVisible &&
                (FocusedModule == VehicleModule.WinchAssembly ||
                 _queuedModule == VehicleModule.WinchAssembly));
            _rangeTankFocusHalo?.SetActive(
                IsRangeTankTargetVisible &&
                (FocusedModule == VehicleModule.SealedRangeTank ||
                 _queuedModule == VehicleModule.SealedRangeTank));
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

        private string QueuedFeedback()
        {
            return ModuleLabel(_queuedModule) +
                " CLAMPED\nPOSTS NEXT TICK";
        }

        private bool PointerIsBlocked(Vector2 screenPosition)
        {
            return
                _controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                    true ||
                _controller?.Hud?.BlocksWorldPointer(screenPosition) == true;
        }

        private bool TryRaycastModule(
            Vector2 screenPosition,
            out VehicleModule module)
        {
            module = VehicleModule.None;
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
            for (var index = 0; index < hitCount; index++)
            {
                Collider collider = _raycastHits[index].collider;
                if (collider == _winchTargetCollider)
                {
                    module = VehicleModule.WinchAssembly;
                    return true;
                }

                if (collider == _rangeTankTargetCollider)
                {
                    module = VehicleModule.SealedRangeTank;
                    return true;
                }
            }

            return false;
        }

        private static GameObject BuildTarget(
            string name,
            Transform stand,
            Material darkIron,
            Material moduleMaterial,
            Material tungsten,
            Material signal,
            out BoxCollider collider,
            out GameObject focusHalo)
        {
            var target = new GameObject(name);
            target.layer = InteractionLayer;
            target.transform.SetParent(stand, false);
            collider = target.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.35f, 2.4f, 1.85f);
            collider.center = new Vector3(0f, 1f, 0f);
            collider.isTrigger = true;

            CreateVisual(
                name + "_SELECTOR_RAIL",
                PrimitiveType.Cube,
                target.transform,
                new Vector3(0.94f, 0.82f, 0f),
                new Vector3(0.08f, 1.45f, 1.62f),
                Quaternion.identity,
                darkIron);
            CreateVisual(
                name + "_CHOICE_PLATE",
                PrimitiveType.Cube,
                target.transform,
                new Vector3(1f, 1.55f, 0f),
                new Vector3(0.08f, 0.5f, 1.28f),
                Quaternion.identity,
                tungsten);
            CreateVisual(
                name + "_MODULE_SIGIL",
                PrimitiveType.Cylinder,
                target.transform,
                new Vector3(1.07f, 1.55f, 0f),
                new Vector3(0.22f, 0.46f, 0.22f),
                Quaternion.Euler(0f, 0f, 90f),
                moduleMaterial);
            focusHalo = CreateVisual(
                name + "_FOCUS_SIGNAL_RAIL",
                PrimitiveType.Cube,
                target.transform,
                new Vector3(1.08f, 1.55f, 0f),
                new Vector3(0.035f, 0.58f, 1.42f),
                Quaternion.identity,
                signal);
            CreateVisual(
                name + "_SELECTOR_DOG",
                PrimitiveType.Cylinder,
                target.transform,
                new Vector3(1.12f, 0.72f, 0.52f),
                new Vector3(0.11f, 0.34f, 0.11f),
                Quaternion.Euler(0f, 0f, 90f),
                moduleMaterial);
            return target;
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

        private static bool IsDedicatedTarget(BoxCollider? collider)
        {
            return collider != null &&
                   collider.isTrigger &&
                   collider.gameObject.layer == InteractionLayer;
        }

        private static bool IsSupportedPreparation(
            PreparationChoice preparation)
        {
            return preparation == PreparationChoice.WorkshopPush ||
                   preparation == PreparationChoice.CivicBuffer;
        }

        private static bool IsSupportedModule(VehicleModule module)
        {
            return module == VehicleModule.WinchAssembly ||
                   module == VehicleModule.SealedRangeTank;
        }

        private static string ModuleLabel(VehicleModule module)
        {
            return module == VehicleModule.WinchAssembly
                ? "WINCH ASSEMBLY"
                : "SEALED RANGE TANK";
        }

        private static GarageModuleStation ToStation(
            VehicleModule module)
        {
            return module switch
            {
                VehicleModule.WinchAssembly =>
                    GarageModuleStation.WinchAssembly,
                VehicleModule.SealedRangeTank =>
                    GarageModuleStation.SealedRangeTank,
                _ => GarageModuleStation.None,
            };
        }
    }
}
