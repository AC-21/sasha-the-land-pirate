#nullable enable

using System;
using System.Collections.Generic;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum DepotReturnControl
    {
        None = 0,
        WaterValve = 1,
        FuelValve = 2,
        ReturnLatch = 3,
    }

    /// <summary>
    /// Depot-only physical controls for choosing the sealed tank's cargo and
    /// freezing Sasha's loaded return. This component owns transient focus and
    /// derived visuals only; accepted operations delegate to existing
    /// controller verbs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingDepotReturnInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Sasha Depot Return Controls [Derived Only]";
        public const string WaterValveName =
            "INTERACT_DEPOT_RETURN_WATER_VALVE";
        public const string FuelValveName =
            "INTERACT_DEPOT_RETURN_FUEL_VALVE";
        public const string ReturnLatchName =
            "INTERACT_DEPOT_RETURN_RATCHET_LATCH";
        public const string FeedbackLabelName =
            "DEPOT_RETURN_CONTROL_FEEDBACK";

        private const int RaycastBufferSize = 6;
        private const float RaycastDistance = 500f;

        private readonly Dictionary<Collider, DepotReturnControl> _targets =
            new Dictionary<Collider, DepotReturnControl>();
        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _waterValve;
        private GameObject? _fuelValve;
        private GameObject? _returnLatch;
        private GameObject? _waterHighlight;
        private GameObject? _fuelHighlight;
        private GameObject? _latchHighlight;
        private TextMesh? _feedbackLabel;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private bool _built;

        public DepotReturnControl FocusedControl { get; private set; }

        public string Feedback { get; private set; } = string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public bool IsBuilt => _built;

        public bool IsWaterValveVisible => IsActive(_waterValve);

        public bool IsFuelValveVisible => IsActive(_fuelValve);

        public bool IsReturnLatchVisible => IsActive(_returnLatch);

        public bool IsWaterValveHighlighted => IsActive(_waterHighlight);

        public bool IsFuelValveHighlighted => IsActive(_fuelHighlight);

        public bool IsReturnLatchHighlighted => IsActive(_latchHighlight);

        public bool HasDedicatedInteractionTargets => _targets.Count == 3;

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
            gameObject.name = RootName;
            iron = RequireMaterial(iron, nameof(iron));
            oxide = RequireMaterial(oxide, nameof(oxide));
            bone = RequireMaterial(bone, nameof(bone));
            tungsten = RequireMaterial(tungsten, nameof(tungsten));
            signal = RequireMaterial(signal, nameof(signal));

            _waterValve = CreateValve(
                WaterValveName,
                new Vector3(-1.42f, 1.74f, -1.28f),
                signal,
                bone,
                out _waterHighlight);
            _fuelValve = CreateValve(
                FuelValveName,
                new Vector3(1.42f, 1.74f, -1.28f),
                oxide,
                bone,
                out _fuelHighlight);
            _returnLatch = CreateLatch(
                ReturnLatchName,
                new Vector3(0f, 1.18f, -2.62f),
                iron,
                tungsten,
                out _latchHighlight);

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(transform, false);
            feedback.transform.localPosition = new Vector3(0f, 2.95f, -1.5f);
            _feedbackLabel = feedback.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 52;
            _feedbackLabel.characterSize = 0.045f;
            _feedbackLabel.color = new Color32(230, 213, 169, 255);

            ApplyVisibility();
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
            NormalizeFocus();
            ApplyVisibility();
            RefreshVisuals();
        }

        public void ResetLocalFocus()
        {
            _model = null;
            FocusedControl = DepotReturnControl.None;
            Feedback = string.Empty;
            LastInteractionRejected = false;
            _hasPointerPosition = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        public void MoveFocus(int direction)
        {
            if (!IsInteractionActive())
            {
                ResetLocalFocus();
                return;
            }

            DepotReturnControl[] available = AvailableControls();
            if (available.Length == 0)
            {
                ResetLocalFocus();
                return;
            }

            int current = Array.IndexOf(available, FocusedControl);
            int step = direction < 0 ? -1 : 1;
            int next = current < 0
                ? step < 0 ? available.Length - 1 : 0
                : (current + step + available.Length) % available.Length;
            FocusedControl = available[next];
            SetFeedback(ControlPrompt(FocusedControl), rejected: false);
        }

        public bool OperateFocused()
        {
            return Activate(FocusedControl);
        }

        public bool ActivateWaterValve()
        {
            return Activate(DepotReturnControl.WaterValve);
        }

        public bool ActivateFuelValve()
        {
            return Activate(DepotReturnControl.FuelValve);
        }

        public bool ActivateReturnLatch()
        {
            return Activate(DepotReturnControl.ReturnLatch);
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!IsInteractionActive())
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) == true)
            {
                Reject("FIELD DESK HAS THIS POINTER · depot control ignored");
                return false;
            }

            if (!TryRaycastTarget(
                    screenPosition,
                    out DepotReturnControl control))
            {
                return false;
            }

            Focus(control);
            Activate(control);
            return true;
        }

        private void Update()
        {
            if (!IsInteractionActive())
            {
                if (FocusedControl != DepotReturnControl.None ||
                    Feedback.Length != 0)
                {
                    ResetLocalFocus();
                }

                return;
            }

            Keyboard? keyboard = Keyboard.current;
            Gamepad? gamepad = Gamepad.current;
            bool moveLeft =
                keyboard?.leftArrowKey.wasPressedThisFrame == true ||
                keyboard?.aKey.wasPressedThisFrame == true ||
                gamepad?.dpad.left.wasPressedThisFrame == true;
            bool moveRight =
                keyboard?.rightArrowKey.wasPressedThisFrame == true ||
                keyboard?.dKey.wasPressedThisFrame == true ||
                gamepad?.dpad.right.wasPressedThisFrame == true;
            if (moveLeft != moveRight)
            {
                MoveFocus(moveLeft ? -1 : 1);
            }

            bool operate =
                keyboard?.eKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (operate)
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
                TryRaycastTarget(
                    pointer,
                    out DepotReturnControl hovered))
            {
                Focus(hovered);
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

        private void OnDisable()
        {
            ResetLocalFocus();
        }

        private bool Activate(DepotReturnControl control)
        {
            if (!IsInteractionActive() ||
                !IsCurrentModel() ||
                _controller == null ||
                _model == null)
            {
                FocusedControl = DepotReturnControl.None;
                ApplyVisibility();
                Reject("DEPOT CONTROL STALE · no cargo command queued");
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                Reject("ACTION QUEUED · let the depot ledger accept it first");
                return false;
            }

            if (control == DepotReturnControl.WaterValve ||
                control == DepotReturnControl.FuelValve)
            {
                if (!AreLiquidValvesAvailable())
                {
                    Reject("LIQUID VALVES LOCKED · load the repair cargo first");
                    return false;
                }

                LiquidCargoKind kind =
                    control == DepotReturnControl.WaterValve
                        ? LiquidCargoKind.Water
                        : LiquidCargoKind.Fuel;
                _controller.ChooseLiquidReturn(kind);
                if (!_controller.HasPendingPlayerCommands)
                {
                    Reject(_controller.Status);
                    return false;
                }

                SetFeedback(
                    kind == LiquidCargoKind.Water
                        ? "WATER VALVE OPEN · awaiting custody seal"
                        : "FUEL VALVE OPEN · awaiting custody seal",
                    rejected: false);
                return true;
            }

            if (control == DepotReturnControl.ReturnLatch)
            {
                if (!IsReturnLatchAvailable())
                {
                    Reject("RETURN LATCH LOCKED · finish the physical load");
                    return false;
                }

                _controller.BeginReturn();
                if (!_controller.HasPendingPlayerCommands)
                {
                    Reject(_controller.Status);
                    return false;
                }

                SetFeedback(
                    "RETURN RATCHET THROWN · payload freeze queued",
                    rejected: false);
                return true;
            }

            Reject("SELECT A DEPOT CONTROL FIRST");
            return false;
        }

        private void Focus(DepotReturnControl control)
        {
            if (!IsControlVisible(control))
            {
                return;
            }

            FocusedControl = control;
            SetFeedback(ControlPrompt(control), rejected: false);
        }

        private bool IsInteractionActive()
        {
            return _built &&
                   _controller?.HasActiveGame == true &&
                   _model != null &&
                   _model.ExpeditionPhase == ExpeditionPhase.AtDepot &&
                   _model.TransactionPhase == TransactionPhase.RoadOwned &&
                   _controller.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.DepotEncounter &&
                   gameObject.activeInHierarchy;
        }

        private bool IsCurrentModel()
        {
            return _controller != null &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel);
        }

        private bool AreLiquidValvesAvailable()
        {
            return IsDepotRoadOwned() &&
                   HasLoadedRepairCargo() &&
                   _model!.VehicleModule == VehicleModule.SealedRangeTank &&
                   _model.LiquidCargoKind == LiquidCargoKind.None &&
                   _model.LiquidCargoCustody == LiquidCargoCustody.None &&
                   _model.PauseCause == PauseCause.None;
        }

        private bool IsReturnLatchAvailable()
        {
            if (!IsDepotRoadOwned() ||
                !HasLoadedRepairCargo() ||
                !_model!.RouteActionUsed ||
                _model.PauseCause != PauseCause.None)
            {
                return false;
            }

            if (_model.VehicleModule == VehicleModule.WinchAssembly)
            {
                return _model.HeavyCargoKind == HeavyCargoKind.PumpRotor &&
                       _model.HeavyCargoCustody == HeavyCargoCustody.Vehicle;
            }

            return _model.VehicleModule == VehicleModule.SealedRangeTank &&
                   _model.LiquidCargoKind != LiquidCargoKind.None &&
                   _model.LiquidCargoCustody == LiquidCargoCustody.Vehicle &&
                   _model.LiquidCargoQuantityMilli > 0;
        }

        private bool HasLoadedRepairCargo()
        {
            return _model != null &&
                   _model.RepairCargoKind != RepairCargoKind.None &&
                   _model.RepairCargoCustody == RepairCargoCustody.Vehicle;
        }

        private bool IsDepotRoadOwned()
        {
            return _model != null &&
                   _model.ExpeditionPhase == ExpeditionPhase.AtDepot &&
                   _model.TransactionPhase == TransactionPhase.RoadOwned;
        }

        private void NormalizeFocus()
        {
            if (!IsControlVisible(FocusedControl))
            {
                DepotReturnControl[] available = AvailableControls();
                FocusedControl = available.Length == 1
                    ? available[0]
                    : DepotReturnControl.None;
                Feedback = FocusedControl != DepotReturnControl.None
                    ? ControlPrompt(FocusedControl)
                    : available.Length > 1
                        ? "LEFT / RIGHT · CHOOSE WATER OR FUEL"
                        : string.Empty;
                LastInteractionRejected = false;
            }
        }

        private DepotReturnControl[] AvailableControls()
        {
            if (!HasCurrentDepotPresentation())
            {
                return Array.Empty<DepotReturnControl>();
            }

            if (AreLiquidValvesAvailable())
            {
                return new[]
                {
                    DepotReturnControl.WaterValve,
                    DepotReturnControl.FuelValve,
                };
            }

            if (IsReturnLatchAvailable())
            {
                return new[] { DepotReturnControl.ReturnLatch };
            }

            return Array.Empty<DepotReturnControl>();
        }

        private bool IsControlVisible(DepotReturnControl control)
        {
            if (!HasCurrentDepotPresentation())
            {
                return false;
            }

            return control switch
            {
                DepotReturnControl.WaterValve => AreLiquidValvesAvailable(),
                DepotReturnControl.FuelValve => AreLiquidValvesAvailable(),
                DepotReturnControl.ReturnLatch => IsReturnLatchAvailable(),
                _ => false,
            };
        }

        private void ApplyVisibility()
        {
            bool presentationActive = HasCurrentDepotPresentation();
            bool valves =
                presentationActive && AreLiquidValvesAvailable();
            bool latch =
                presentationActive && IsReturnLatchAvailable();
            SetActive(_waterValve, valves);
            SetActive(_fuelValve, valves);
            SetActive(_returnLatch, latch);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.gameObject.SetActive(valves || latch);
            }
        }

        private bool HasCurrentDepotPresentation()
        {
            return IsInteractionActive() && IsCurrentModel();
        }

        private void RefreshVisuals()
        {
            SetActive(
                _waterHighlight,
                IsWaterValveVisible &&
                FocusedControl == DepotReturnControl.WaterValve);
            SetActive(
                _fuelHighlight,
                IsFuelValveVisible &&
                FocusedControl == DepotReturnControl.FuelValve);
            SetActive(
                _latchHighlight,
                IsReturnLatchVisible &&
                FocusedControl == DepotReturnControl.ReturnLatch);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = Feedback;
                _feedbackLabel.color = LastInteractionRejected
                    ? new Color32(230, 103, 74, 255)
                    : new Color32(230, 213, 169, 255);
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

        private bool TryRaycastTarget(
            Vector2 screenPosition,
            out DepotReturnControl control)
        {
            control = DepotReturnControl.None;
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
            float nearestDistance = float.MaxValue;
            bool found = false;
            for (var index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _raycastHits[index];
                if (hit.distance >= nearestDistance ||
                    !_targets.TryGetValue(hit.collider, out DepotReturnControl candidate) ||
                    !IsControlVisible(candidate))
                {
                    continue;
                }

                nearestDistance = hit.distance;
                control = candidate;
                found = true;
            }

            return found;
        }

        private GameObject CreateValve(
            string name,
            Vector3 localPosition,
            Material face,
            Material brace,
            out GameObject highlight)
        {
            GameObject target = CreateTarget(
                name,
                localPosition,
                new Vector3(0.82f, 0.82f, 0.82f),
                name == WaterValveName
                    ? DepotReturnControl.WaterValve
                    : DepotReturnControl.FuelValve);
            CreateVisual(
                "Valve Body",
                PrimitiveType.Cylinder,
                target.transform,
                Vector3.zero,
                new Vector3(0.26f, 0.15f, 0.26f),
                Quaternion.Euler(90f, 0f, 0f),
                face);
            CreateVisual(
                "Valve Crossbar",
                PrimitiveType.Cube,
                target.transform,
                new Vector3(0f, 0.22f, 0f),
                new Vector3(0.74f, 0.12f, 0.12f),
                Quaternion.Euler(0f, 0f, 18f),
                brace);
            highlight = CreateVisual(
                "Focused Valve Halo",
                PrimitiveType.Cylinder,
                target.transform,
                new Vector3(0f, -0.03f, 0f),
                new Vector3(0.42f, 0.04f, 0.42f),
                Quaternion.Euler(90f, 0f, 0f),
                brace);
            highlight.SetActive(false);
            return target;
        }

        private GameObject CreateLatch(
            string name,
            Vector3 localPosition,
            Material body,
            Material handle,
            out GameObject highlight)
        {
            GameObject target = CreateTarget(
                name,
                localPosition,
                new Vector3(1.3f, 0.82f, 0.72f),
                DepotReturnControl.ReturnLatch);
            CreateVisual(
                "Return Ratchet Body",
                PrimitiveType.Cube,
                target.transform,
                Vector3.zero,
                new Vector3(0.9f, 0.28f, 0.32f),
                Quaternion.identity,
                body);
            CreateVisual(
                "Return Ratchet Handle",
                PrimitiveType.Cube,
                target.transform,
                new Vector3(0.35f, 0.34f, 0f),
                new Vector3(0.18f, 0.78f, 0.18f),
                Quaternion.Euler(0f, 0f, -28f),
                handle);
            highlight = CreateVisual(
                "Focused Return Latch Halo",
                PrimitiveType.Cube,
                target.transform,
                new Vector3(0f, -0.2f, 0f),
                new Vector3(1.16f, 0.07f, 0.52f),
                Quaternion.identity,
                handle);
            highlight.SetActive(false);
            return target;
        }

        private GameObject CreateTarget(
            string name,
            Vector3 localPosition,
            Vector3 colliderSize,
            DepotReturnControl control)
        {
            var target = new GameObject(name);
            target.layer = InteractionLayer;
            target.transform.SetParent(transform, false);
            target.transform.localPosition = localPosition;
            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.isTrigger = true;
            _targets.Add(collider, control);
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

        private static string ControlPrompt(DepotReturnControl control)
        {
            return control switch
            {
                DepotReturnControl.WaterValve =>
                    "WATER VALVE · E / GAMEPAD SOUTH",
                DepotReturnControl.FuelValve =>
                    "FUEL VALVE · E / GAMEPAD SOUTH",
                DepotReturnControl.ReturnLatch =>
                    "RETURN RATCHET · E / GAMEPAD SOUTH",
                _ => "LEFT / RIGHT · CHOOSE A DEPOT CONTROL",
            };
        }

        private static Material RequireMaterial(
            Material? material,
            string parameter)
        {
            return material ??
                throw new ArgumentNullException(parameter);
        }

        private static bool IsActive(GameObject? value)
        {
            return value != null && value.activeSelf;
        }

        private static void SetActive(GameObject? value, bool active)
        {
            if (value != null && value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }
    }
}
