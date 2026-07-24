#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Derived-only CityOverview control for the exact Emergency Storage
    /// expansion socket. The Field Desk may focus this control, but only a
    /// fresh physical input delegates the existing installation command.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingEmergencyCisternExpansionInteractor :
        MonoBehaviour
    {
        public const int InteractionLayer =
            LastBearingCityServiceCellInteractor.InteractionLayer;
        public const string ControlName =
            "INTERACT_EMERGENCY_CISTERN_EXPANSION";
        public const string FocusRailName =
            "EMERGENCY_CISTERN_EXPANSION_FOCUS_RAIL";

        private const float RaycastDistance = 500f;
        private const int RaycastBufferSize = 12;

        private static readonly Vector3[] PadPositions =
        {
            new Vector3(-2.8f, 0.65f, -0.55f),
            new Vector3(-1.4f, 0.65f, 0.35f),
            new Vector3(0f, 0.65f, -0.55f),
            new Vector3(1.4f, 0.65f, 0.35f),
            new Vector3(2.8f, 0.65f, -0.55f),
        };

        private LastBearingGameController? _controller;
        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _control;
        private GameObject? _handwheel;
        private GameObject? _focusRail;
        private TextMesh? _label;
        private bool _focused;
        private bool _inputArmed;
        private bool _presentationActive;
        private int _presentationEntryFrame = -1;
        private bool _built;

        public bool HasDedicatedInteractionTarget =>
            _built &&
            _control?.GetComponent<BoxCollider>() != null;

        public bool IsControlVisible =>
            _control?.activeInHierarchy == true &&
            IsInteractionActive() &&
            _model != null &&
            ShouldShowControl(_model);

        public bool IsControlFocused => _focused && IsControlVisible;

        public bool IsInputArmed => IsControlFocused && _inputArmed;

        public bool IsFocusRailVisible =>
            _focusRail?.activeInHierarchy == true;

        public string Label => _label?.text ?? string.Empty;

        public string Feedback { get; private set; } =
            "EXPANSION SOCKET STOWED";

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
            _control = new GameObject(ControlName);
            _control.layer = InteractionLayer;
            _control.transform.SetParent(transform, false);
            var collider = _control.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.22f, 1.12f, 0.96f);
            collider.isTrigger = true;

            CreateVisualBlock(
                "EMERGENCY_CISTERN_EXPANSION_CRADLE",
                _control.transform,
                new Vector3(0f, -0.34f, 0f),
                new Vector3(1.04f, 0.26f, 0.78f),
                iron);
            CreateVisualBlock(
                "EMERGENCY_CISTERN_EXPANSION_SOCKET",
                _control.transform,
                new Vector3(0f, 0.02f, 0.08f),
                new Vector3(0.52f, 0.52f, 0.26f),
                bone);
            _handwheel = CreateVisualCylinder(
                "EMERGENCY_CISTERN_EXPANSION_HANDWHEEL",
                _control.transform,
                new Vector3(0f, 0.2f, -0.28f),
                new Vector3(0.34f, 0.08f, 0.34f),
                Quaternion.Euler(90f, 0f, 0f),
                oxide);
            CreateVisualBlock(
                "EMERGENCY_CISTERN_EXPANSION_INDEX",
                _handwheel.transform,
                new Vector3(0f, 0.32f, 0f),
                new Vector3(0.1f, 0.34f, 0.1f),
                signal);
            _focusRail = CreateVisualBlock(
                FocusRailName,
                _control.transform,
                new Vector3(0f, -0.16f, -0.44f),
                new Vector3(1.1f, 0.08f, 0.08f),
                tungsten);
            _focusRail.SetActive(false);

            var labelObject = new GameObject(
                "EMERGENCY_CISTERN_EXPANSION_LABEL");
            labelObject.transform.SetParent(_control.transform, false);
            labelObject.transform.localPosition =
                new Vector3(0f, 0.58f, -0.2f);
            labelObject.transform.localRotation =
                Quaternion.Euler(70f, 0f, 0f);
            _label = labelObject.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.fontSize = 42;
            _label.characterSize = 0.035f;
            _label.color = new Color32(238, 221, 178, 255);
            _label.text = "EXPAND CISTERN\nE · GAMEPAD SOUTH";

            _control.SetActive(false);
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

        internal void Apply(LastBearingReadModel model)
        {
            _model = model ??
                throw new ArgumentNullException(nameof(model));
            if (!ShouldShowControl(model))
            {
                ResetLocalFocus();
            }

            PositionControl(model);
            RefreshVisuals();
        }

        public void FocusControl()
        {
            if (!IsInteractionActive() ||
                _model == null ||
                !ShouldShowControl(_model))
            {
                ResetLocalFocus();
                Reject("CISTERN EXPANSION SOCKET UNAVAILABLE");
                return;
            }

            _focused = true;
            _inputArmed = false;
            _presentationActive = true;
            _presentationEntryFrame = Time.frameCount;
            SetFeedback(
                "CISTERN EXPANSION FOCUSED · release controls to set the handwheel",
                rejected: false);
            RefreshVisuals();
        }

        public bool OperateFocused()
        {
            if (!RequireCityOverview() ||
                _model == null ||
                !ShouldShowControl(_model))
            {
                Reject("CISTERN EXPANSION SOCKET UNAVAILABLE");
                return false;
            }

            if (!IsControlFocused)
            {
                Reject("FOCUS THE CISTERN EXPANSION SOCKET FIRST");
                return false;
            }

            if (_controller?.IsEmergencyCisternExpansionQueued == true)
            {
                SetFeedback(
                    "CISTERN EXPANSION ALREADY QUEUED · saddle tanks wait for the city tick",
                    rejected: false);
                return false;
            }

            if (!_inputArmed)
            {
                Reject("RELEASE CONTROLS · THEN TURN THE EXPANSION HANDWHEEL");
                return false;
            }

            if (_controller?.CanInstallEmergencyCisternExpansion != true)
            {
                Reject("CISTERN EXPANSION CONTROL STALE");
                return false;
            }

            _controller.InstallEmergencyCisternExpansion();
            if (_controller.IsEmergencyCisternExpansionQueued != true)
            {
                Reject(
                    string.IsNullOrWhiteSpace(_controller.Status)
                        ? "CISTERN EXPANSION NOT QUEUED"
                        : _controller.Status);
                return false;
            }

            _inputArmed = false;
            SetFeedback(
                "CISTERN EXPANSION QUEUED · 2 parts · +30.000 capacity on the authoritative tick",
                rejected: false);
            RefreshVisuals();
            return true;
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!IsInteractionActive() ||
                _camera == null ||
                _control == null)
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                true)
            {
                Reject("FIELD DESK HAS THIS POINTER · expansion click ignored");
                return false;
            }

            Ray ray = _camera.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _raycastHits,
                RaycastDistance,
                1 << InteractionLayer,
                QueryTriggerInteraction.Collide);
            bool found = false;
            for (var index = 0; index < hitCount; index++)
            {
                if (_raycastHits[index].collider.gameObject == _control)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }

            if (!IsControlFocused)
            {
                FocusControl();
            }

            OperateFocused();
            return true;
        }

        public void ResetLocalFocus()
        {
            _focused = false;
            _inputArmed = false;
            _presentationActive = false;
            _presentationEntryFrame = -1;
            RefreshVisuals();
        }

        private void OnDisable()
        {
            ResetLocalFocus();
        }

        private void Update()
        {
            if (!IsInteractionActive())
            {
                if (_focused || _presentationActive)
                {
                    ResetLocalFocus();
                }

                return;
            }

            Keyboard? keyboard = Keyboard.current;
            Gamepad? gamepad = Gamepad.current;
            Mouse? mouse = Mouse.current;
            UpdateInputArming(keyboard, gamepad, mouse);

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
            if (_label == null || _camera == null)
            {
                return;
            }

            Vector3 towardCamera =
                _camera.transform.position - _label.transform.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                _label.transform.rotation = Quaternion.LookRotation(
                    towardCamera.normalized,
                    Vector3.up);
            }
        }

        private void UpdateInputArming(
            Keyboard? keyboard,
            Gamepad? gamepad,
            Mouse? mouse)
        {
            if (!IsControlFocused)
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

            bool released =
                keyboard?.eKey.isPressed != true &&
                gamepad?.buttonSouth.isPressed != true &&
                mouse?.leftButton.isPressed != true;
            if (!_inputArmed &&
                Time.frameCount > _presentationEntryFrame &&
                released)
            {
                _inputArmed = true;
                SetFeedback(
                    "CISTERN EXPANSION READY · E · GAMEPAD SOUTH · or turn the handwheel",
                    rejected: false);
                RefreshVisuals();
            }
        }

        private bool RequireCityOverview()
        {
            if (IsInteractionActive())
            {
                return true;
            }

            Reject("OPEN CITY OVERVIEW TO WORK THE EXPANSION SOCKET");
            return false;
        }

        private bool IsInteractionActive()
        {
            return _built &&
                   _controller?.IsExactFieldDeskCityOverview == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel) &&
                   gameObject.activeInHierarchy;
        }

        private static bool ShouldShowControl(LastBearingReadModel model)
        {
            return model.NextCityDecision ==
                       NextCityDecision.ExpandEmergencyCistern &&
                   model.IsCityImprovementInstallationAvailable &&
                   model.InstalledCityImprovement ==
                       CityImprovementKind.None &&
                   model.EmergencyStoragePadIndex >= 0;
        }

        private void PositionControl(LastBearingReadModel model)
        {
            if (_control == null ||
                model.EmergencyStoragePadIndex < 0 ||
                model.EmergencyStoragePadIndex >= PadPositions.Length)
            {
                return;
            }

            int quarterTurns = SelectExpansionQuarterTurns(model);
            Vector3 position =
                PadPositions[model.EmergencyStoragePadIndex] +
                RotateOffset(
                    new Vector3(0f, 0f, -1.22f),
                    quarterTurns);
            _control.transform.localPosition =
                new Vector3(position.x, 0.58f, position.z);
            _control.transform.localRotation =
                Quaternion.Euler(0f, quarterTurns * 90f, 0f);
        }

        private static int SelectExpansionQuarterTurns(
            LastBearingReadModel model)
        {
            int pumpQuarterTurns =
                LastBearingCityServiceCellInteractor
                    .SelectEmergencyCisternPumpQuarterTurns(model);
            int preferred =
                (model.EmergencyStorageQuarterTurns + 1) % 4;
            if (model.MachineShopPadIndex < 0 ||
                model.MachineShopPadIndex >= PadPositions.Length)
            {
                return preferred == pumpQuarterTurns
                    ? (preferred + 1) % 4
                    : preferred;
            }

            Vector3 storage =
                PadPositions[model.EmergencyStoragePadIndex];
            Vector3 hotShift =
                PadPositions[model.MachineShopPadIndex] +
                RotateOffset(
                    new Vector3(0f, 0f, -0.82f),
                    model.MachineShopQuarterTurns);
            int bestQuarterTurns = -1;
            float bestDistance = float.NegativeInfinity;
            for (var offset = 0; offset < 4; offset++)
            {
                int candidate = (preferred + offset) % 4;
                if (candidate == pumpQuarterTurns)
                {
                    continue;
                }

                Vector3 position = storage + RotateOffset(
                    new Vector3(0f, 0f, -1.22f),
                    candidate);
                float distance = (position - hotShift).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestQuarterTurns = candidate;
                }
            }

            return bestQuarterTurns >= 0
                ? bestQuarterTurns
                : (pumpQuarterTurns + 2) % 4;
        }

        private void RefreshVisuals()
        {
            bool visible =
                _model != null &&
                ShouldShowControl(_model) &&
                IsInteractionActive();
            SetActive(_control, visible);
            SetActive(_focusRail, visible && _focused);
            if (_handwheel != null)
            {
                _handwheel.transform.localRotation =
                    Quaternion.Euler(
                        90f,
                        _controller?.IsEmergencyCisternExpansionQueued == true
                            ? -36f
                            : 0f,
                        0f);
            }

            if (_label != null)
            {
                _label.text =
                    _controller?.IsEmergencyCisternExpansionQueued == true
                        ? "EXPANSION QUEUED\nCITY TICK PENDING"
                        : !_focused
                            ? "EXPAND CISTERN\nSELECT HANDWHEEL"
                            : _inputArmed
                                ? "EXPAND CISTERN\nE · GAMEPAD SOUTH"
                                : "EXPAND CISTERN\nRELEASE CONTROLS";
                _label.color =
                    _controller?.IsEmergencyCisternExpansionQueued == true
                        ? new Color32(255, 190, 104, 255)
                        : new Color32(238, 221, 178, 255);
            }
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

        private static GameObject CreateVisualBlock(
            string objectName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = objectName;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return visual;
        }

        private static GameObject CreateVisualCylinder(
            string objectName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Quaternion rotation,
            Material material)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = objectName;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = scale;
            visual.transform.localRotation = rotation;
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return visual;
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
