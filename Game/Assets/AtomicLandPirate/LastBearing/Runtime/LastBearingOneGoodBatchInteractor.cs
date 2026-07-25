#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Derived-only physical controls for One Good Batch. The input stillage
    /// starts the bounded batch; the finished lot itself hands the completed
    /// batch across. Accepted work delegates existing controller verbs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingOneGoodBatchInteractor : MonoBehaviour
    {
        public const int InteractionLayer =
            LastBearingCityServiceCellInteractor.InteractionLayer;
        public const string RootName =
            "One Good Batch Physical Controls [Derived Only]";
        public const string BatchStartControlName =
            "INTERACT_ONE_GOOD_BATCH_INPUT";
        public const string OutputLotControlName =
            "INTERACT_ONE_GOOD_BATCH_OUTPUT_LOT";
        public const string InputFocusRailName =
            "ONE_GOOD_BATCH_INPUT_FOCUS_RAIL";
        public const string HandoffFocusRailName =
            "ONE_GOOD_BATCH_HANDOFF_FOCUS_RAIL";
        public const string FeedbackLabelName =
            "ONE_GOOD_BATCH_PHYSICAL_FEEDBACK";

        private const int RaycastBufferSize = 8;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _root;
        private GameObject? _batchStartControl;
        private GameObject? _outputLotControl;
        private GameObject? _inputFocusRail;
        private GameObject? _outputFocusRail;
        private GameObject? _feedbackRoot;
        private TextMesh? _feedbackLabel;
        private BoxCollider? _batchStartCollider;
        private BoxCollider? _outputLotCollider;
        private FocusedAction _focusedAction;
        private bool _inputArmed;
        private bool _presentationActive;
        private int _presentationEntryFrame = -1;
        private bool _built;

        public bool IsBatchStartControlFocused =>
            _focusedAction == FocusedAction.StartBatch &&
            IsBatchStartControlVisible;

        public bool IsBarterControlFocused =>
            _focusedAction == FocusedAction.BarterLot &&
            IsBarterControlVisible;

        public bool IsInputArmed =>
            _focusedAction != FocusedAction.None &&
            _inputArmed &&
            CanAcceptFocusedAction;

        public bool IsBatchStartControlVisible =>
            _batchStartControl?.activeInHierarchy == true;

        public bool IsBarterControlVisible =>
            _outputLotControl?.activeInHierarchy == true;

        public bool IsInputFocusRailVisible =>
            _inputFocusRail?.activeInHierarchy == true;

        public bool IsHandoffFocusRailVisible =>
            _outputFocusRail?.activeInHierarchy == true;

        public bool HasDedicatedInteractionTargets =>
            IsDedicatedTrigger(_batchStartCollider) &&
            IsDedicatedTrigger(_outputLotCollider);

        public Vector3 BatchStartControlWorldPosition =>
            _batchStartCollider?.bounds.center ??
            _batchStartControl?.transform.position ??
            transform.position;

        public Vector3 OutputLotControlWorldPosition =>
            _outputLotCollider?.bounds.center ??
            _outputLotControl?.transform.position ??
            transform.position;

        public Vector3 BarterControlWorldPosition =>
            OutputLotControlWorldPosition;

        public string Feedback { get; private set; } =
            "ONE GOOD BATCH CONTROLS STOWED";

        public bool LastInteractionRejected { get; private set; }

        private bool CanAcceptFocusedAction =>
            _controller?.HasPendingPlayerCommands != true &&
            (_focusedAction switch
            {
                FocusedAction.StartBatch =>
                    _controller?.IsWorkshopBatchStartAvailable == true &&
                    IsExactBatchStartModel(),
                FocusedAction.BarterLot =>
                    _controller?.IsWorkshopBarterAvailable == true &&
                    IsExactBarterModel(),
                _ => false,
            });

        internal void Build(
            Transform inputAnchor,
            GameObject bearingLot,
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
            inputAnchor = inputAnchor ??
                throw new ArgumentNullException(nameof(inputAnchor));
            bearingLot = bearingLot ??
                throw new ArgumentNullException(nameof(bearingLot));

            _root = new GameObject(RootName);
            _root.transform.SetParent(transform, false);

            _batchStartControl = CreateTarget(
                BatchStartControlName,
                inputAnchor,
                new Vector3(0f, 1.55f, 0f),
                new Vector3(1.36f, 0.54f, 1.08f),
                oxide,
                out _batchStartCollider);
            CreatePart(
                "ONE_GOOD_BATCH_START_LEVER",
                _batchStartControl.transform,
                new Vector3(0f, 0.43f, 0f),
                new Vector3(0.16f, 0.7f, 0.16f),
                signal);
            CreateLabel(
                "ONE_GOOD_BATCH_START_LABEL",
                _batchStartControl.transform,
                "LOAD ONE BATCH\nSELECT · RELEASE · E",
                new Vector3(0f, 0.72f, -0.42f));
            _inputFocusRail = CreatePart(
                InputFocusRailName,
                _batchStartControl.transform,
                new Vector3(0f, -0.34f, -0.58f),
                new Vector3(1.48f, 0.08f, 0.08f),
                tungsten);

            _outputLotControl = CreateTarget(
                OutputLotControlName,
                bearingLot.transform,
                new Vector3(0f, 1.5f, 0f),
                new Vector3(1.6f, 0.56f, 1.22f),
                bone,
                out _outputLotCollider);
            CreateLabel(
                "ONE_GOOD_BATCH_OUTPUT_LABEL",
                _outputLotControl.transform,
                "LIFT FINISHED LOT\nTO CLAIMS",
                new Vector3(0f, 0.58f, -0.42f));
            _outputFocusRail = CreatePart(
                HandoffFocusRailName,
                _outputLotControl.transform,
                new Vector3(0f, -0.38f, -0.62f),
                new Vector3(1.62f, 0.08f, 0.08f),
                signal);

            _feedbackRoot = new GameObject(FeedbackLabelName);
            _feedbackRoot.transform.SetParent(_root.transform, false);
            _feedbackRoot.transform.localPosition =
                new Vector3(0f, 4.1f, 2.7f);
            _feedbackLabel = _feedbackRoot.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 46;
            _feedbackLabel.characterSize = 0.04f;
            _feedbackLabel.color = new Color32(238, 221, 178, 255);

            SetActive(_root, false);
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
            bool completedWhileOpen =
                _model?.SpareBearingBatchPhase ==
                    SpareBearingBatchPhase.InProgress &&
                model?.SpareBearingBatchPhase ==
                    SpareBearingBatchPhase.Complete;
            _model = model;
            if (!HasCurrentModel() ||
                (_focusedAction == FocusedAction.StartBatch &&
                 !IsExactBatchStartModel()) ||
                (_focusedAction == FocusedAction.BarterLot &&
                 !IsExactBarterModel()))
            {
                ResetLocalFocus();
            }

            Physics.SyncTransforms();
            if (completedWhileOpen &&
                _focusedAction == FocusedAction.None &&
                HasCurrentPresentation() &&
                IsExactBarterModel())
            {
                FocusBarterControl();
                return;
            }

            RefreshVisuals();
        }

        public bool FocusAvailableControl()
        {
            if (_controller?.IsWorkshopBatchStartAvailable == true &&
                IsExactBatchStartModel())
            {
                return FocusBatchStartControl();
            }

            if (_controller?.IsWorkshopBarterAvailable == true &&
                IsExactBarterModel())
            {
                return FocusBarterControl();
            }

            ResetLocalFocus();
            return false;
        }

        public bool FocusBatchStartControl()
        {
            return FocusControl(
                FocusedAction.StartBatch,
                "BATCH INPUT FOCUSED · RELEASE CONTROL · THEN LOAD");
        }

        public bool FocusBarterControl()
        {
            return FocusControl(
                FocusedAction.BarterLot,
                "FINISHED LOT FOCUSED · RELEASE CONTROL · THEN HAND ACROSS");
        }

        public bool OperateFocused()
        {
            if (_focusedAction == FocusedAction.None)
            {
                Reject("FOCUS THE PHYSICAL BATCH CONTROL FIRST");
                return false;
            }

            if (_controller?.HasPendingPlayerCommands == true)
            {
                Reject(
                    "WORKSHOP LEDGER BUSY · NO SECOND BATCH OR LOT QUEUED");
                return false;
            }

            if (!_inputArmed)
            {
                Reject("RELEASE CONTROL · THEN OPERATE ONE GOOD BATCH");
                return false;
            }

            if (!HasCurrentPresentation() ||
                !CanAcceptFocusedAction ||
                _controller == null)
            {
                Reject("ONE GOOD BATCH CONTROL STALE · NO WORK QUEUED");
                return false;
            }

            if (_focusedAction == FocusedAction.StartBatch)
            {
                _controller.StartSpareBearingBatch();
            }
            else
            {
                _controller.BarterSpareBearingLot();
            }

            if (!_controller.HasPendingPlayerCommands)
            {
                Reject(_controller.Status);
                return false;
            }

            _inputArmed = false;
            SetFeedback(
                _focusedAction == FocusedAction.StartBatch
                    ? "ONE GOOD BATCH QUEUED · INPUTS MOVE ON THE CITY TICK"
                    : "LOT HANDOFF QUEUED · CUSTODY MOVES ON THE CITY TICK",
                rejected: false);
            RefreshVisuals();
            return true;
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasCurrentPresentation() || _camera == null)
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                    true ||
                _controller?.Hud?.BlocksWorldPointer(screenPosition) == true)
            {
                Reject("CITY UI HAS THIS POINTER · WORKSHOP IGNORED");
                return false;
            }

            FocusedAction action = RaycastAction(screenPosition);
            if (action == FocusedAction.None)
            {
                return false;
            }

            if (_focusedAction != action)
            {
                return action == FocusedAction.StartBatch
                    ? FocusBatchStartControl()
                    : FocusBarterControl();
            }

            OperateFocused();
            return true;
        }

        public void ResetLocalFocus()
        {
            _focusedAction = FocusedAction.None;
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
                if (_focusedAction != FocusedAction.None ||
                    _presentationActive ||
                    _inputArmed ||
                    _root?.activeSelf == true)
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

            if (_focusedAction != FocusedAction.None &&
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

        private void OnDisable()
        {
            ResetLocalFocus();
        }

        private bool FocusControl(FocusedAction action, string feedback)
        {
            if (!HasCurrentPresentation() ||
                _controller?.HasPendingPlayerCommands == true ||
                (action == FocusedAction.StartBatch &&
                 (_controller?.IsWorkshopBatchStartAvailable != true ||
                  !IsExactBatchStartModel())) ||
                (action == FocusedAction.BarterLot &&
                 (_controller?.IsWorkshopBarterAvailable != true ||
                  !IsExactBarterModel())))
            {
                ResetLocalFocus();
                Reject("ONE GOOD BATCH CONTROL UNAVAILABLE");
                return false;
            }

            _focusedAction = action;
            _inputArmed = false;
            _presentationActive = true;
            _presentationEntryFrame = Time.frameCount;
            _controller?.World?.FuelBondInteractor?.ResetLocalFocus();
            SetFeedback(feedback, rejected: false);
            RefreshVisuals();
            return true;
        }

        private void UpdateInputArming(bool inputHeld)
        {
            if (_focusedAction == FocusedAction.None ||
                !CanAcceptFocusedAction)
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
                    _focusedAction == FocusedAction.StartBatch
                        ? "LOAD ONE BATCH · E · GAMEPAD SOUTH · OR INPUT LEVER"
                        : "HAND LOT ACROSS · E · GAMEPAD SOUTH · OR CLICK LOT",
                    rejected: false);
                RefreshVisuals();
            }
        }

        private bool HasCurrentPresentation()
        {
            return _built &&
                   HasCurrentModel() &&
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

        private bool IsExactBatchStartModel()
        {
            return HasCurrentModel() &&
                   _model!.IsSpareBearingBatchStartAvailable &&
                   _model.SpareBearingBatchPhase ==
                       SpareBearingBatchPhase.None &&
                   _model.SpareBearingLotCustody ==
                       SpareBearingLotCustody.None;
        }

        private bool IsExactBarterModel()
        {
            return HasCurrentModel() &&
                   _model!.IsSpareBearingBarterAvailable &&
                   _model.SpareBearingBatchPhase ==
                       SpareBearingBatchPhase.Complete &&
                   _model.SpareBearingLotQuantity == 1 &&
                   _model.SpareBearingLotCustody ==
                       SpareBearingLotCustody.WorkshopOutput;
        }

        private FocusedAction RaycastAction(Vector2 screenPosition)
        {
            if (_camera == null)
            {
                return FocusedAction.None;
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
                if (collider == _batchStartCollider)
                {
                    return FocusedAction.StartBatch;
                }

                if (collider == _outputLotCollider)
                {
                    return FocusedAction.BarterLot;
                }
            }

            return FocusedAction.None;
        }

        private void RefreshVisuals()
        {
            bool presentation = HasCurrentPresentation();
            bool startVisible =
                presentation &&
                _controller?.IsWorkshopBatchStartAvailable == true &&
                IsExactBatchStartModel();
            bool barterVisible =
                presentation &&
                _controller?.IsWorkshopBarterAvailable == true &&
                IsExactBarterModel();

            SetActive(_root, presentation && (startVisible || barterVisible));
            SetActive(_batchStartControl, startVisible);
            SetActive(_outputLotControl, barterVisible);
            SetColliderEnabled(_batchStartCollider, startVisible);
            SetColliderEnabled(_outputLotCollider, barterVisible);
            SetActive(
                _inputFocusRail,
                startVisible &&
                _focusedAction == FocusedAction.StartBatch);
            SetActive(
                _outputFocusRail,
                barterVisible &&
                _focusedAction == FocusedAction.BarterLot);

            if (_feedbackLabel != null)
            {
                _feedbackLabel.text =
                    _focusedAction != FocusedAction.None
                        ? Feedback
                        : startVisible
                            ? "ONE BOUNDED BATCH\nSELECT INPUT STILLAGE\n" +
                              LastBearingBalanceV1
                                  .SpareBearingBatchPartsCostUnits +
                              " PARTS · " +
                              LastBearingBalanceV1
                                  .SpareBearingBatchRequiredSettlementTicks +
                              " TICKS"
                            : barterVisible
                                ? "ONE FINISHED LOT\nSELECT PHYSICAL LOT"
                                : string.Empty;
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

        private static bool IsDedicatedTrigger(BoxCollider? collider)
        {
            return collider != null &&
                   collider.isTrigger &&
                   collider.gameObject.layer == InteractionLayer;
        }

        private static void SetColliderEnabled(
            Collider? collider,
            bool enabled)
        {
            if (collider != null && collider.enabled != enabled)
            {
                collider.enabled = enabled;
            }
        }

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static GameObject CreateTarget(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            out BoxCollider collider)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.layer = InteractionLayer;
            target.transform.SetParent(parent, false);
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = localScale;
            target.GetComponent<Renderer>().sharedMaterial = material;
            collider = target.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            return target;
        }

        private static GameObject CreatePart(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            return part;
        }

        private static TextMesh CreateLabel(
            string name,
            Transform parent,
            string text,
            Vector3 localPosition)
        {
            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 38;
            label.characterSize = 0.035f;
            label.color = new Color32(238, 221, 178, 255);
            return label;
        }

        private enum FocusedAction
        {
            None = 0,
            StartBatch = 1,
            BarterLot = 2,
        }
    }
}
