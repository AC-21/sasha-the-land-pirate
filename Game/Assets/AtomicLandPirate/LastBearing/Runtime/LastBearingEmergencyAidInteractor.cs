#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Derived-only water-tender witness beside canonical Emergency Storage.
    /// The Field Desk may route focus here, but only fresh physical input
    /// delegates the authoritative emergency-aid command.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingEmergencyAidInteractor : MonoBehaviour
    {
        public const int InteractionLayer =
            LastBearingCityServiceCellInteractor.InteractionLayer;
        public const string RootName =
            "Emergency Aid Water Tender [Derived Only]";
        public const string TenderName = "COOPERATIVE_WATER_TENDER";
        public const string ControlName = "INTERACT_RECEIVE_EMERGENCY_AID";
        public const string FocusRailName =
            "EMERGENCY_AID_TENDER_FOCUS_RAIL";
        public const string QueuedWaterName =
            "EMERGENCY_AID_10000_MILLI_WATER";
        public const string CoiledHoseName =
            "EMERGENCY_AID_RECEIVED_HOSE_COILED";
        public const string ReceiptLabelName =
            "EMERGENCY_AID_10000_MILLI_RECEIPT";

        private const int RaycastBufferSize = 12;
        private const float RaycastDistance = 500f;
        private const float TenderOffset = 3.05f;

        private static readonly Vector3[] PadPositions =
        {
            new Vector3(-2.8f, 0.65f, -0.55f),
            new Vector3(-1.4f, 0.65f, 0.35f),
            new Vector3(0f, 0.65f, -0.55f),
            new Vector3(1.4f, 0.65f, 0.35f),
            new Vector3(2.8f, 0.65f, -0.55f),
        };

        // The working cell stands beside the inherited Emergency Storage
        // mass. These authored service-yard directions keep the tender beside
        // each canonical pad without driving it through that backdrop.
        private static readonly int[] TenderQuarterTurnsByStoragePad =
        {
            0,
            2,
            0,
            3,
            3,
        };

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];
        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _root;
        private GameObject? _tender;
        private GameObject? _control;
        private GameObject? _focusRail;
        private GameObject? _queuedWater;
        private GameObject? _coiledHose;
        private TextMesh? _receiptLabel;
        private BoxCollider? _controlCollider;
        private bool _inputArmed;
        private bool _presentationActive;
        private int _presentationEntryFrame = -1;
        private long _receiptAidMilli = long.MinValue;
        private long _receiptWaterMilli = long.MinValue;
        private long _receiptCapacityMilli = long.MinValue;
        private bool _built;

        public bool IsControlFocused { get; private set; }

        public bool IsInputArmed =>
            IsControlFocused &&
            _inputArmed &&
            CanAcceptInput;

        public bool IsReceptionReady =>
            HasCurrentModel() &&
            _model!.IsEmergencyAidReceptionAvailable;

        public bool IsWitnessVisible => _root?.activeInHierarchy == true;

        public bool IsTenderVisible => _tender?.activeInHierarchy == true;

        public bool IsControlVisible => _control?.activeInHierarchy == true;

        public bool IsQueuedWaterVisible =>
            _queuedWater?.activeInHierarchy == true;

        public bool IsEmptyReceivedTenderVisible =>
            IsTenderVisible &&
            !IsQueuedWaterVisible &&
            HasCurrentModel() &&
            IsAcceptedWitness(_model!);

        public bool IsCoiledHoseVisible =>
            _coiledHose?.activeInHierarchy == true;

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

        public Vector3 TenderLocalPosition =>
            _root?.transform.localPosition ?? Vector3.zero;

        public int TenderOrientationQuarterTurns { get; private set; }

        public string ReceiptLabel => _receiptLabel?.text ?? string.Empty;

        public string Feedback { get; private set; } =
            "WATER TENDER STOWED";

        public bool LastInteractionRejected { get; private set; }

        private bool CanAcceptInput =>
            IsReceptionReady &&
            _controller?.HasPendingPlayerCommands != true;

        internal void Build(
            Material concrete,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal,
            Material water)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            _root = new GameObject(RootName);
            _root.transform.SetParent(transform, false);

            _tender = new GameObject(TenderName);
            _tender.transform.SetParent(_root.transform, false);
            CreatePart(
                "WATER_TENDER_CHASSIS",
                PrimitiveType.Cube,
                _tender.transform,
                new Vector3(0f, 0.38f, 0f),
                new Vector3(2.6f, 0.24f, 1.18f),
                iron);
            CreatePart(
                "WATER_TENDER_TANK",
                PrimitiveType.Cylinder,
                _tender.transform,
                new Vector3(0f, 1.08f, 0f),
                new Vector3(0.68f, 1.12f, 0.68f),
                concrete,
                Quaternion.Euler(0f, 0f, 90f));
            CreatePart(
                "WATER_TENDER_OXIDE_BAND_LEFT",
                PrimitiveType.Cylinder,
                _tender.transform,
                new Vector3(-0.7f, 1.08f, 0f),
                new Vector3(0.72f, 0.08f, 0.72f),
                oxide,
                Quaternion.Euler(0f, 0f, 90f));
            CreatePart(
                "WATER_TENDER_OXIDE_BAND_RIGHT",
                PrimitiveType.Cylinder,
                _tender.transform,
                new Vector3(0.7f, 1.08f, 0f),
                new Vector3(0.72f, 0.08f, 0.72f),
                oxide,
                Quaternion.Euler(0f, 0f, 90f));
            for (var index = 0; index < 4; index++)
            {
                float x = index < 2 ? -0.82f : 0.82f;
                float z = index % 2 == 0 ? -0.62f : 0.62f;
                CreatePart(
                    "WATER_TENDER_WHEEL_" + (index + 1).ToString("00"),
                    PrimitiveType.Cylinder,
                    _tender.transform,
                    new Vector3(x, 0.34f, z),
                    new Vector3(0.34f, 0.16f, 0.34f),
                    iron,
                    Quaternion.Euler(90f, 0f, 0f));
            }

            _queuedWater = CreatePart(
                QueuedWaterName,
                PrimitiveType.Cylinder,
                _tender.transform,
                new Vector3(0f, 1.08f, 0f),
                new Vector3(0.57f, 1.02f, 0.57f),
                water,
                Quaternion.Euler(0f, 0f, 90f));
            CreatePart(
                "WATER_TENDER_10000_MILLI_PLACARD",
                PrimitiveType.Cube,
                _tender.transform,
                new Vector3(0f, 1.15f, -0.72f),
                new Vector3(1.38f, 0.46f, 0.08f),
                bone);
            CreatePart(
                "WATER_TENDER_SHARED_SERVICE_MARK",
                PrimitiveType.Cube,
                _tender.transform,
                new Vector3(0f, 1.58f, -0.7f),
                new Vector3(0.72f, 0.12f, 0.08f),
                signal);

            _coiledHose = CreatePart(
                CoiledHoseName,
                PrimitiveType.Cylinder,
                _tender.transform,
                new Vector3(1.2f, 0.92f, 0f),
                new Vector3(0.36f, 0.08f, 0.36f),
                oxide,
                Quaternion.Euler(0f, 0f, 90f));

            _control = new GameObject(ControlName);
            _control.layer = InteractionLayer;
            _control.transform.SetParent(_root.transform, false);
            _control.transform.localPosition = new Vector3(0f, 0.78f, -1.02f);
            _controlCollider = _control.AddComponent<BoxCollider>();
            _controlCollider.size = new Vector3(1.18f, 0.94f, 0.78f);
            _controlCollider.isTrigger = true;
            CreatePart(
                "WATER_TENDER_DELIVERY_VALVE",
                PrimitiveType.Cylinder,
                _control.transform,
                Vector3.zero,
                new Vector3(0.38f, 0.08f, 0.38f),
                oxide,
                Quaternion.Euler(90f, 0f, 0f));
            CreatePart(
                "WATER_TENDER_DELIVERY_HANDLE",
                PrimitiveType.Cube,
                _control.transform,
                new Vector3(0f, 0.34f, 0f),
                new Vector3(0.62f, 0.12f, 0.12f),
                tungsten);
            _focusRail = CreatePart(
                FocusRailName,
                PrimitiveType.Cube,
                _root.transform,
                new Vector3(0f, 0.28f, -1.46f),
                new Vector3(1.34f, 0.08f, 0.08f),
                tungsten);

            var labelObject = new GameObject(ReceiptLabelName);
            labelObject.transform.SetParent(_root.transform, false);
            labelObject.transform.localPosition =
                new Vector3(0f, 2.35f, -0.7f);
            _receiptLabel = labelObject.AddComponent<TextMesh>();
            _receiptLabel.anchor = TextAnchor.MiddleCenter;
            _receiptLabel.alignment = TextAlignment.Center;
            _receiptLabel.fontSize = 42;
            _receiptLabel.characterSize = 0.032f;
            _receiptLabel.color = new Color32(238, 221, 178, 255);

            _root.SetActive(false);
            RefreshVisuals();
        }

        internal void Configure(
            LastBearingGameController controller,
            Camera cityCamera)
        {
            _controller = controller ??
                throw new ArgumentNullException(nameof(controller));
            _camera = cityCamera ??
                throw new ArgumentNullException(nameof(cityCamera));
            RefreshVisuals();
        }

        internal void Apply(LastBearingReadModel? model)
        {
            _model = model;
            if (model == null || !ShouldShowWitness(model))
            {
                ResetLocalFocus();
            }
            else
            {
                PositionTender(model);
                if (IsAcceptedWitness(model))
                {
                    IsControlFocused = false;
                    _inputArmed = false;
                    SetFeedback(
                        "10.000-MILLI AID RECEIVED · STORAGE CAP HONORED · SHARED SERVICE AND ITS MAINTENANCE PROMISE REMAIN",
                        rejected: false);
                }
            }

            RefreshVisuals();
        }

        public bool FocusControl()
        {
            if (!HasCurrentPresentation() || !CanAcceptInput)
            {
                ResetLocalFocus();
                Reject("WATER TENDER CONTROL UNAVAILABLE");
                return false;
            }

            _controller?.World?.ResetEmergencyAidSiblingInteractions();
            IsControlFocused = true;
            _inputArmed = false;
            _presentationActive = true;
            _presentationEntryFrame = Time.frameCount;
            SetFeedback(
                "WATER TENDER FOCUSED · RELEASE CONTROL · THEN RECEIVE",
                rejected: false);
            RefreshVisuals();
            return true;
        }

        public bool OperateFocused()
        {
            if (!IsControlFocused)
            {
                Reject("FOCUS THE WATER TENDER VALVE FIRST");
                return false;
            }

            if (!IsInputArmed)
            {
                Reject("RELEASE CONTROL · THEN RECEIVE THE WATER");
                return false;
            }

            return QueueReception();
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
                Reject("FIELD DESK HAS THIS POINTER · WATER TENDER IGNORED");
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

        internal static bool ShouldShowWitness(LastBearingReadModel model)
        {
            return model.IsEmergencyAidReceptionAvailable ||
                   IsAcceptedWitness(model);
        }

        internal static bool IsAcceptedWitness(LastBearingReadModel model)
        {
            return model.ExpeditionPhase == ExpeditionPhase.AtHome &&
                   model.TransactionPhase == TransactionPhase.Finalized &&
                   model.TurbineCondition ==
                       TurbineCondition.SleeveRepaired &&
                   model.RepairCargoKind == RepairCargoKind.FieldSleeve &&
                   model.RepairCargoCustody ==
                       RepairCargoCustody.Consumed &&
                   model.FactionClaimState ==
                       FactionClaimState.Cooperating &&
                   model.DepotControl ==
                       DepotControl.SharedAccess &&
                   model.FactionAccessPolicy ==
                       FactionAccessPolicy.SharedService &&
                   model.FactionAidPolicy ==
                       FactionAidPolicy.EmergencyWaterDelivered &&
                   model.EmergencyAidWaterMilli ==
                       LastBearingBalanceV1.CooperateAidWaterMilli &&
                   model.FactionTrust ==
                       LastBearingBalanceV1.CooperateTrustDelta &&
                   model.FactionGrievance == 0 &&
                   model.RoutePermitGranted &&
                   model.FutureRouteTollFuelUnits == 0 &&
                   model.MaintenanceRecipe ==
                       MaintenanceRecipe.FieldSleeveService &&
                   model.MaintenanceObligationActive;
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
            if (_receiptLabel == null || _camera == null)
            {
                return;
            }

            Vector3 towardCamera =
                _camera.transform.position - _receiptLabel.transform.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                _receiptLabel.transform.rotation = Quaternion.LookRotation(
                    towardCamera.normalized,
                    Vector3.up);
            }
        }

        private void OnDisable()
        {
            // An ancestor mode root may be disabled while the city scaffold
            // is stowed. Clear transient ownership without turning off the
            // derived witness itself, so returning to CityOverview restores
            // the same ready tender synchronously.
            IsControlFocused = false;
            _inputArmed = false;
            _presentationActive = false;
            _presentationEntryFrame = -1;
            LastInteractionRejected = false;
        }

        private bool QueueReception()
        {
            if (!HasCurrentPresentation() ||
                !IsReceptionReady ||
                _controller == null)
            {
                Reject("WATER TENDER CONTROL STALE · NO WORK QUEUED");
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                SetFeedback(
                    _controller.IsEmergencyAidReceptionQueued
                        ? "EMERGENCY WATER ALREADY QUEUED"
                        : "ACTION QUEUED · LET THE CITY LEDGER ACCEPT IT FIRST",
                    rejected: !_controller.IsEmergencyAidReceptionQueued);
                return false;
            }

            if (!_controller.CanReceiveEmergencyAid)
            {
                Reject("EMERGENCY WATER TERMS UNAVAILABLE");
                return false;
            }

            _controller.ReceiveEmergencyAid();
            if (!_controller.IsEmergencyAidReceptionQueued)
            {
                Reject(_controller.Status);
                return false;
            }

            _inputArmed = false;
            SetFeedback(
                "10.000-MILLI AID RECEIPT QUEUED · STORAGE CAP APPLIES ON THE CITY TICK",
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
                    "RECEIVE 10.000 MILLI · E · GAMEPAD SOUTH · OR EXACT VALVE",
                    rejected: false);
                RefreshVisuals();
            }
        }

        private bool HasCurrentPresentation()
        {
            return _built &&
                   HasCurrentModel() &&
                   ShouldShowWitness(_model!) &&
                   _controller?.IsExactFieldDeskCityOverview == true &&
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

        private void PositionTender(LastBearingReadModel model)
        {
            if (_root == null ||
                model.EmergencyStoragePadIndex < 0 ||
                model.EmergencyStoragePadIndex >= PadPositions.Length)
            {
                return;
            }

            Vector3 storage = PadPositions[model.EmergencyStoragePadIndex];
            int quarterTurns =
                TenderQuarterTurnsByStoragePad[
                    model.EmergencyStoragePadIndex];

            TenderOrientationQuarterTurns = quarterTurns;
            Vector3 position = storage + RotateOffset(
                new Vector3(0f, 0f, -TenderOffset),
                quarterTurns);
            _root.transform.localPosition =
                new Vector3(position.x, 0.05f, position.z);
            _root.transform.localRotation =
                Quaternion.Euler(0f, quarterTurns * 90f, 0f);
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
            bool accepted =
                visible &&
                _model != null &&
                IsAcceptedWitness(_model);
            bool ready = visible && CanAcceptInput;
            bool queued =
                visible &&
                _controller?.IsEmergencyAidReceptionQueued == true;
            SetActive(_root, visible);
            SetActive(_tender, visible);
            SetActive(_control, ready);
            SetActive(_focusRail, ready && IsControlFocused);
            SetActive(_queuedWater, visible && !accepted);
            SetActive(_coiledHose, accepted);
            if (_controlCollider != null)
            {
                _controlCollider.enabled = ready;
            }

            if (_receiptLabel == null || _model == null)
            {
                return;
            }

            if (accepted)
            {
                if (_receiptAidMilli != _model.EmergencyAidWaterMilli ||
                    _receiptWaterMilli != _model.WaterMilli ||
                    _receiptCapacityMilli != _model.WaterCapacityMilli)
                {
                    _receiptAidMilli = _model.EmergencyAidWaterMilli;
                    _receiptWaterMilli = _model.WaterMilli;
                    _receiptCapacityMilli = _model.WaterCapacityMilli;
                    _receiptLabel.text =
                        "RECEIVED " +
                        FormatMilli(_model.EmergencyAidWaterMilli) +
                        " WATER · TENDER EMPTY / HOSE COILED\n" +
                        "STORAGE " + FormatMilli(_model.WaterMilli) + " / " +
                        FormatMilli(_model.WaterCapacityMilli) +
                        " · CAP HONORED\n" +
                        "SHARED SERVICE OPEN · MAINTENANCE PROMISE RETAINED";
                }

                _receiptLabel.color = new Color32(238, 221, 178, 255);
            }
            else if (queued)
            {
                ClearReceiptCache();
                _receiptLabel.text =
                    "RECEIPT QUEUED · 10.000 WATER OFFER\n" +
                    "CITY TICK PENDING · STORAGE CAP WILL APPLY";
                _receiptLabel.color = new Color32(255, 190, 104, 255);
            }
            else if (IsControlFocused)
            {
                ClearReceiptCache();
                _receiptLabel.text = _inputArmed
                    ? "RECEIVE 10.000 WATER\nE · GAMEPAD SOUTH · OR EXACT VALVE"
                    : "10.000 WATER TENDER\nRELEASE CONTROLS";
                _receiptLabel.color = new Color32(238, 221, 178, 255);
            }
            else
            {
                ClearReceiptCache();
                _receiptLabel.text =
                    "10.000 WATER TENDER · SHARED SERVICE\nSELECT DELIVERY VALVE";
                _receiptLabel.color = new Color32(238, 221, 178, 255);
            }
        }

        private void ClearReceiptCache()
        {
            _receiptAidMilli = long.MinValue;
            _receiptWaterMilli = long.MinValue;
            _receiptCapacityMilli = long.MinValue;
        }

        private void Reject(string message)
        {
            SetFeedback(message, rejected: true);
            RefreshVisuals();
        }

        private void SetFeedback(string message, bool rejected)
        {
            Feedback = message;
            LastInteractionRejected = rejected;
        }

        private static string FormatMilli(long value)
        {
            return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", ".");
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
            part.transform.localRotation = localRotation ?? Quaternion.identity;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return part;
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

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
