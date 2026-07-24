#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Derived-only physical control for the exact Civic Buffer range-tank
    /// fuel bond. It owns focus, fresh-input arming, pointer targeting, and
    /// presentation; accepted work delegates one core command.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingFuelBondInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Post The Fuel Bond Control [Derived Only]";
        public const string ControlName = "INTERACT_POST_FUEL_BOND";
        public const string FocusRailName = "FUEL_BOND_FOCUS_RAIL";
        public const string FuelCanRootName = "RETURNED_FUEL_BOND_LOT";
        public const string FeedbackLabelName = "FUEL_BOND_FEEDBACK";
        public const int FuelCanCount = 5;

        private const int RaycastBufferSize = 6;
        private const float RaycastDistance = 500f;

        private static readonly Vector3 OfferedFuelPosition =
            new Vector3(2.4f, 1.04f, -1.42f);
        private static readonly Vector3 PostedFuelPosition =
            new Vector3(5.32f, 1.04f, -1.42f);
        private static readonly Vector3 ControlPosition =
            new Vector3(3.02f, 1.75f, -1.15f);

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _visualRoot;
        private GameObject? _control;
        private GameObject? _focusRail;
        private GameObject? _fuelCanRoot;
        private TextMesh? _feedbackLabel;
        private BoxCollider? _controlCollider;
        private bool _inputArmed;
        private bool _presentationActive;
        private int _presentationEntryFrame = -1;
        private bool _built;

        public bool IsControlFocused { get; private set; }

        public bool IsInputArmed =>
            IsControlFocused &&
            _inputArmed &&
            CanAcceptInput;

        public bool IsBondReady =>
            HasCurrentModel() &&
            _model!.IsDepotAccessRestorationAvailable;

        private bool CanAcceptInput =>
            IsBondReady &&
            _controller?.HasPendingPlayerCommands != true;

        public bool IsWitnessVisible =>
            _visualRoot?.activeInHierarchy == true;

        public bool IsFuelLotVisible =>
            _fuelCanRoot?.activeInHierarchy == true;

        public bool IsPostedWitnessVisible =>
            IsFuelLotVisible &&
            HasCurrentModel() &&
            IsAcceptedWitness(_model!);

        public bool IsFocusRailVisible =>
            _focusRail?.activeInHierarchy == true;

        public bool HasDedicatedInteractionTarget =>
            _controlCollider != null &&
            _controlCollider.isTrigger &&
            _controlCollider.gameObject.layer == InteractionLayer;

        public Vector3 ControlWorldPosition =>
            _controlCollider?.bounds.center ??
            _control?.transform.position ??
            transform.position;

        public Vector3 FuelLotLocalPosition =>
            _fuelCanRoot?.transform.localPosition ?? Vector3.zero;

        public string Feedback { get; private set; } =
            "FUEL BOND WICKET STOWED";

        public bool LastInteractionRejected { get; private set; }

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
            _visualRoot = new GameObject(RootName);
            _visualRoot.transform.SetParent(transform, false);

            _control = new GameObject(ControlName);
            _control.layer = InteractionLayer;
            _control.transform.SetParent(_visualRoot.transform, false);
            _control.transform.localPosition = ControlPosition;
            _controlCollider = _control.AddComponent<BoxCollider>();
            _controlCollider.size = new Vector3(1.15f, 0.52f, 0.92f);
            _controlCollider.isTrigger = true;

            CreatePart(
                "FUEL_BOND_LEDGER_PLINTH",
                PrimitiveType.Cube,
                _control.transform,
                Vector3.zero,
                new Vector3(1.08f, 0.22f, 0.82f),
                oxide);
            CreatePart(
                "FUEL_BOND_LEDGER_STAMP",
                PrimitiveType.Cylinder,
                _control.transform,
                new Vector3(0f, 0.2f, 0f),
                new Vector3(0.28f, 0.14f, 0.28f),
                iron,
                Quaternion.Euler(0f, 0f, 90f));
            CreatePart(
                "FUEL_BOND_FIVE_MARK",
                PrimitiveType.Cube,
                _control.transform,
                new Vector3(0.34f, 0.18f, -0.25f),
                new Vector3(0.28f, 0.08f, 0.18f),
                signal);

            _focusRail = CreatePart(
                FocusRailName,
                PrimitiveType.Cube,
                _visualRoot.transform,
                ControlPosition + new Vector3(0f, -0.26f, -0.5f),
                new Vector3(1.24f, 0.08f, 0.08f),
                tungsten);

            _fuelCanRoot = new GameObject(FuelCanRootName);
            _fuelCanRoot.transform.SetParent(_visualRoot.transform, false);
            _fuelCanRoot.transform.localPosition = OfferedFuelPosition;
            for (var index = 0; index < FuelCanCount; index++)
            {
                int column = index % 3;
                int row = index / 3;
                Vector3 position = new Vector3(
                    (column - 1) * 0.38f,
                    row * 0.42f,
                    0f);
                GameObject can = CreatePart(
                    "RETURNED_FUEL_CAN_" + (index + 1).ToString("00"),
                    PrimitiveType.Cylinder,
                    _fuelCanRoot.transform,
                    position,
                    new Vector3(0.28f, 0.34f, 0.28f),
                    oxide);
                CreatePart(
                    "FUEL_CAN_IRON_BAND_" + (index + 1).ToString("00"),
                    PrimitiveType.Cylinder,
                    can.transform,
                    new Vector3(0f, 0.22f, 0f),
                    new Vector3(1.04f, 0.08f, 1.04f),
                    iron);
                CreatePart(
                    "FUEL_CAN_CUSTODY_TAG_" + (index + 1).ToString("00"),
                    PrimitiveType.Cube,
                    can.transform,
                    new Vector3(0f, 0.3f, -0.3f),
                    new Vector3(0.28f, 0.12f, 0.05f),
                    bone);
            }

            var labelObject = new GameObject(FeedbackLabelName);
            labelObject.transform.SetParent(_visualRoot.transform, false);
            labelObject.transform.localPosition =
                new Vector3(3.2f, 3.18f, -1.35f);
            _feedbackLabel = labelObject.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 42;
            _feedbackLabel.characterSize = 0.035f;
            _feedbackLabel.color = new Color32(238, 221, 178, 255);

            _visualRoot.SetActive(false);
            RefreshVisuals();
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
            }
            else if (IsAcceptedWitness(model))
            {
                IsControlFocused = false;
                _inputArmed = false;
                SetFeedback(
                    "FUEL BOND POSTED · DEPOT ROUTE PERMIT RECORDED",
                    rejected: false);
            }

            RefreshVisuals();
        }

        public bool FocusControl()
        {
            if (!HasCurrentPresentation() || !CanAcceptInput)
            {
                ResetLocalFocus();
                Reject("FUEL BOND CONTROL UNAVAILABLE");
                return false;
            }

            IsControlFocused = true;
            _inputArmed = false;
            _presentationActive = true;
            _presentationEntryFrame = Time.frameCount;
            SetFeedback(
                "FUEL BOND FOCUSED · RELEASE CONTROL · THEN POST",
                rejected: false);
            RefreshVisuals();
            return true;
        }

        public bool OperateFocused()
        {
            if (!IsControlFocused)
            {
                Reject("FOCUS THE FUEL BOND LEDGER FIRST");
                return false;
            }

            if (!IsInputArmed)
            {
                Reject("RELEASE CONTROL · THEN POST THE FUEL BOND");
                return false;
            }

            return QueueFuelBond();
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasCurrentPresentation() ||
                !CanAcceptInput ||
                _camera == null ||
                _controlCollider == null)
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                true)
            {
                Reject("FIELD DESK HAS THIS POINTER · FUEL BOND IGNORED");
                return false;
            }

            if (!TryRaycastControl(screenPosition))
            {
                return false;
            }

            if (!IsControlFocused)
            {
                FocusControl();
                return true;
            }

            OperateFocused();
            return true;
        }

        public void ResetLocalFocus()
        {
            IsControlFocused = false;
            _inputArmed = false;
            _presentationActive = false;
            _presentationEntryFrame = -1;
            LastInteractionRejected = false;
            RefreshVisuals();
        }

        private void Update()
        {
            if (!HasCurrentPresentation())
            {
                if (IsControlFocused ||
                    IsWitnessVisible ||
                    _presentationActive ||
                    _inputArmed)
                {
                    ResetLocalFocus();
                }

                return;
            }

            RefreshVisuals();
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
                _camera.transform.position -
                _feedbackLabel.transform.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                _feedbackLabel.transform.rotation = Quaternion.LookRotation(
                    towardCamera.normalized,
                    Vector3.up);
            }
        }

        private void OnEnable()
        {
            RefreshVisuals();
        }

        private void OnDisable()
        {
            ResetLocalFocus();
        }

        private bool QueueFuelBond()
        {
            if (!HasCurrentPresentation() ||
                !IsBondReady ||
                _controller == null)
            {
                Reject("FUEL BOND CONTROL STALE · NO WORK QUEUED");
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                SetFeedback(
                    _controller.IsFuelBondQueued
                        ? "FUEL BOND ALREADY QUEUED"
                        : "ACTION QUEUED · LET THE CITY LEDGER ACCEPT IT FIRST",
                    rejected: !_controller.IsFuelBondQueued);
                return false;
            }

            if (!_controller.CanPostFuelBond)
            {
                Reject("FUEL BOND TERMS UNAVAILABLE");
                return false;
            }

            _controller.PostFuelBond();
            if (!_controller.IsFuelBondQueued)
            {
                Reject(_controller.Status);
                return false;
            }

            _inputArmed = false;
            SetFeedback(
                "FUEL BOND QUEUED · FIVE RETURNED FUEL MOVE ON THE CITY TICK",
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
                SetFeedback(
                    "POST FIVE FUEL · E · GAMEPAD SOUTH · OR STAMP LEDGER",
                    rejected: false);
                RefreshVisuals();
            }
        }

        private bool HasCurrentPresentation()
        {
            return _built &&
                   HasCurrentModel() &&
                   ShouldShowWitness(_model!) &&
                   _controller?.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.BuildingCutaway &&
                   _controller.World?.IsOneGoodBatchCutawaySelected == true &&
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

        internal static bool ShouldShowWitness(
            LastBearingReadModel model)
        {
            return model.IsDepotAccessRestorationAvailable ||
                   IsAcceptedWitness(model);
        }

        internal static bool IsAcceptedWitness(
            LastBearingReadModel model)
        {
            return model.PreparationChoice == PreparationChoice.CivicBuffer &&
                   model.VehicleModule == VehicleModule.SealedRangeTank &&
                   model.LiquidCargoKind == LiquidCargoKind.Fuel &&
                   model.LiquidCargoQuantityMilli ==
                       LastBearingBalanceV1.TankFuelReturnMilli &&
                   model.LiquidCargoCustody ==
                       LiquidCargoCustody.Settlement &&
                   model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                   model.TransactionPhase == TransactionPhase.Finalized &&
                   model.TurbineCondition ==
                       TurbineCondition.BearingRepaired &&
                   model.NextCityDecision == NextCityDecision.None &&
                   model.FactionClaimState == FactionClaimState.Aggrieved &&
                   model.FactionAccessPolicy ==
                       FactionAccessPolicy.PermitRequired &&
                   model.FactionAidPolicy == FactionAidPolicy.Withheld &&
                   model.FutureRouteTollFuelUnits ==
                       LastBearingBalanceV1.TakeFutureRouteTollFuelUnits &&
                   model.RoutePermitGranted;
        }

        private bool TryRaycastControl(Vector2 screenPosition)
        {
            if (_camera == null || _controlCollider == null)
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
                if (_raycastHits[index].collider == _controlCollider)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshVisuals()
        {
            bool visible =
                _model != null &&
                ShouldShowWitness(_model) &&
                HasCurrentPresentation();
            SetActive(_visualRoot, visible);
            bool hasPending =
                visible &&
                _controller?.HasPendingPlayerCommands == true;
            bool fuelBondQueued =
                hasPending &&
                _controller?.IsFuelBondQueued == true;
            bool ready =
                visible &&
                CanAcceptInput;
            if (_controlCollider != null)
            {
                _controlCollider.enabled = ready;
            }

            SetActive(_control, ready);
            SetActive(_focusRail, ready && IsControlFocused);
            SetActive(_fuelCanRoot, visible);
            bool accepted =
                _model != null &&
                IsAcceptedWitness(_model);
            if (_fuelCanRoot != null)
            {
                _fuelCanRoot.transform.localPosition =
                    accepted
                        ? PostedFuelPosition
                        : OfferedFuelPosition;
            }

            if (_feedbackLabel != null)
            {
                string label = "FUEL BOND STOWED";
                if (visible && accepted)
                {
                    label = "PERMIT\nFUEL BOND POSTED · TOLL 2";
                }
                else if (fuelBondQueued)
                {
                    label = "FUEL BOND QUEUED\nCITY TICK PENDING";
                }
                else if (hasPending)
                {
                    label = "CLAIMS LEDGER BUSY";
                }
                else if (ready)
                {
                    label = IsControlFocused
                        ? Feedback
                        : "LOCKED\nPOST 5 FUEL · SELECT LEDGER";
                }

                _feedbackLabel.text = label;
            }
        }

        private void Reject(string feedback)
        {
            SetFeedback(feedback, rejected: true);
            RefreshVisuals();
        }

        private void SetFeedback(string feedback, bool rejected)
        {
            Feedback = feedback ?? string.Empty;
            LastInteractionRejected = rejected;
        }

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static GameObject CreatePart(
            string name,
            PrimitiveType primitive,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation =
                localRotation ?? Quaternion.identity;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return part;
        }
    }
}
