#nullable enable

using System;
using System.Collections.Generic;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum DepotDecisionControl
    {
        None = 0,
        Cooperate = 1,
        TakeBearing = 2,
    }

    /// <summary>
    /// Physical, depot-only presentation for the existing encounter choice.
    /// This component owns transient focus and derived geometry only; accepted
    /// decisions always pass through the controller's canonical guard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingDepotDecisionInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Face The Claim Decision Stations [Derived Only]";
        public const string CooperateStationName =
            "INTERACT_DEPOT_DECISION_COOPERATE";
        public const string TakeBearingStationName =
            "INTERACT_DEPOT_DECISION_TAKE_BEARING";
        public const string FeedbackLabelName =
            "DEPOT_DECISION_FEEDBACK";

        private const int RaycastBufferSize = 4;
        private const float RaycastDistance = 500f;

        private readonly Dictionary<Collider, DepotDecisionControl> _targets =
            new Dictionary<Collider, DepotDecisionControl>();
        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Camera? _camera;
        private GameObject? _cooperateStation;
        private GameObject? _takeBearingStation;
        private GameObject? _cooperateHighlight;
        private GameObject? _takeBearingHighlight;
        private TextMesh? _cooperateLabel;
        private TextMesh? _takeBearingLabel;
        private TextMesh? _feedbackLabel;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private bool _built;

        public DepotDecisionControl FocusedDecision { get; private set; }

        public string Feedback { get; private set; } = string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public bool IsBuilt => _built;

        public bool IsCooperateStationVisible =>
            IsActive(_cooperateStation);

        public bool IsTakeBearingStationVisible =>
            IsActive(_takeBearingStation);

        public bool IsCooperateStationHighlighted =>
            IsActive(_cooperateHighlight);

        public bool IsTakeBearingStationHighlighted =>
            IsActive(_takeBearingHighlight);

        public bool HasDedicatedInteractionTargets => _targets.Count == 2;

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

            _cooperateStation = CreateCooperateStation(
                iron,
                bone,
                tungsten,
                out _cooperateHighlight,
                out _cooperateLabel);
            _takeBearingStation = CreateTakeBearingStation(
                oxide,
                bone,
                signal,
                out _takeBearingHighlight,
                out _takeBearingLabel);

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(transform, false);
            feedback.transform.localPosition = new Vector3(0f, 4.15f, -2.25f);
            _feedbackLabel = ConfigureLabel(
                feedback,
                fontSize: 48,
                characterSize: 0.04f);

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
            FocusedDecision = DepotDecisionControl.None;
            Feedback = string.Empty;
            LastInteractionRejected = false;
            _hasPointerPosition = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        public void MoveFocus(int direction)
        {
            if (!HasCurrentDecisionPresentation())
            {
                ResetLocalFocus();
                return;
            }

            if (FocusedDecision == DepotDecisionControl.None)
            {
                FocusedDecision = direction < 0
                    ? DepotDecisionControl.Cooperate
                    : DepotDecisionControl.TakeBearing;
            }
            else
            {
                FocusedDecision =
                    FocusedDecision == DepotDecisionControl.Cooperate
                        ? DepotDecisionControl.TakeBearing
                        : DepotDecisionControl.Cooperate;
            }

            SetFeedback(
                DecisionPrompt(FocusedDecision),
                rejected: false);
        }

        public bool OperateFocused()
        {
            return Activate(FocusedDecision);
        }

        public bool ActivateCooperate()
        {
            return Activate(DepotDecisionControl.Cooperate);
        }

        public bool ActivateTakeBearing()
        {
            return Activate(DepotDecisionControl.TakeBearing);
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasCurrentDecisionPresentation())
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                true)
            {
                Reject("FIELD DESK HAS THIS POINTER · depot choice ignored");
                return false;
            }

            if (!TryRaycastTarget(
                    screenPosition,
                    out DepotDecisionControl decision))
            {
                return false;
            }

            Focus(decision);
            Activate(decision);
            return true;
        }

        private void Update()
        {
            if (!HasCurrentDecisionPresentation())
            {
                if (_model != null ||
                    FocusedDecision != DepotDecisionControl.None ||
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
                gamepad?.dpad.left.wasPressedThisFrame == true;
            bool moveRight =
                keyboard?.rightArrowKey.wasPressedThisFrame == true ||
                gamepad?.dpad.right.wasPressedThisFrame == true;
            bool navigated = moveLeft != moveRight;
            if (navigated)
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
            else if (!navigated &&
                !operate &&
                pointerMoved &&
                TryRaycastTarget(
                    pointer,
                    out DepotDecisionControl hovered))
            {
                Focus(hovered);
            }
        }

        private void LateUpdate()
        {
            FaceCamera(_cooperateLabel);
            FaceCamera(_takeBearingLabel);
            FaceCamera(_feedbackLabel);
        }

        private void OnDisable()
        {
            ResetLocalFocus();
        }

        private bool Activate(DepotDecisionControl decision)
        {
            if (!HasCurrentDecisionPresentation() ||
                _controller == null)
            {
                FocusedDecision = DepotDecisionControl.None;
                ApplyVisibility();
                Reject("DEPOT CHOICE STALE · no response queued");
                return false;
            }

            if (decision != DepotDecisionControl.Cooperate &&
                decision != DepotDecisionControl.TakeBearing)
            {
                Reject("SELECT COOPERATE OR TAKE BEARING FIRST");
                return false;
            }

            _controller.ResolveDepot(
                decision == DepotDecisionControl.Cooperate);
            if (!_controller.HasPendingPlayerCommands)
            {
                Reject(_controller.Status);
                return false;
            }

            SetFeedback(
                decision == DepotDecisionControl.Cooperate
                    ? "COOPERATION ENTERED · field sleeve covenant queued"
                    : "CLAIM FACED · ceramic bearing grievance queued",
                rejected: false);
            ApplyVisibility();
            return true;
        }

        private void Focus(DepotDecisionControl decision)
        {
            if (!HasCurrentDecisionPresentation() ||
                (decision != DepotDecisionControl.Cooperate &&
                 decision != DepotDecisionControl.TakeBearing))
            {
                return;
            }

            FocusedDecision = decision;
            SetFeedback(DecisionPrompt(decision), rejected: false);
        }

        private bool HasCurrentDecisionPresentation()
        {
            return _built &&
                   _controller?.HasActiveGame == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel) &&
                   _controller.IsDepotDecisionAvailable &&
                   gameObject.activeInHierarchy;
        }

        private void NormalizeFocus()
        {
            if (!HasCurrentDecisionPresentation())
            {
                FocusedDecision = DepotDecisionControl.None;
                Feedback = string.Empty;
                LastInteractionRejected = false;
                return;
            }

            if (FocusedDecision != DepotDecisionControl.Cooperate &&
                FocusedDecision != DepotDecisionControl.TakeBearing)
            {
                FocusedDecision = DepotDecisionControl.None;
                Feedback =
                    "LEFT / RIGHT · FACE COOPERATE OR TAKE BEARING";
                LastInteractionRejected = false;
            }
        }

        private void ApplyVisibility()
        {
            bool visible = HasCurrentDecisionPresentation();
            SetActive(_cooperateStation, visible);
            SetActive(_takeBearingStation, visible);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.gameObject.SetActive(visible);
            }
        }

        private void RefreshVisuals()
        {
            SetActive(
                _cooperateHighlight,
                IsCooperateStationVisible &&
                FocusedDecision == DepotDecisionControl.Cooperate);
            SetActive(
                _takeBearingHighlight,
                IsTakeBearingStationVisible &&
                FocusedDecision == DepotDecisionControl.TakeBearing);
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
            out DepotDecisionControl decision)
        {
            decision = DepotDecisionControl.None;
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
            float nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _raycastHits[index];
                if (hit.collider == null ||
                    hit.distance >= nearestDistance ||
                    !_targets.TryGetValue(
                        hit.collider,
                        out DepotDecisionControl candidate))
                {
                    continue;
                }

                decision = candidate;
                nearestDistance = hit.distance;
            }

            return decision != DepotDecisionControl.None;
        }

        private GameObject CreateCooperateStation(
            Material iron,
            Material bone,
            Material tungsten,
            out GameObject highlight,
            out TextMesh label)
        {
            var station = new GameObject("Cooperate Service Stand");
            station.transform.SetParent(transform, false);
            station.transform.localPosition = new Vector3(-2.35f, 0f, -2.25f);

            GameObject target = CreateInteractivePrimitive(
                CooperateStationName,
                PrimitiveType.Cube,
                station.transform,
                new Vector3(0f, 1.25f, 0f),
                new Vector3(1.65f, 1.55f, 0.48f),
                iron,
                DepotDecisionControl.Cooperate);
            CreateDecorativePrimitive(
                "Cooperate Ledger Shelf",
                PrimitiveType.Cube,
                station.transform,
                new Vector3(0f, 2.14f, -0.08f),
                new Vector3(1.95f, 0.18f, 0.72f),
                bone,
                Quaternion.identity);
            CreateDecorativePrimitive(
                "Cooperate Linked Hands Left",
                PrimitiveType.Cylinder,
                station.transform,
                new Vector3(-0.34f, 1.4f, -0.32f),
                new Vector3(0.22f, 0.12f, 0.22f),
                tungsten,
                Quaternion.Euler(90f, 0f, 0f));
            CreateDecorativePrimitive(
                "Cooperate Linked Hands Right",
                PrimitiveType.Cylinder,
                station.transform,
                new Vector3(0.34f, 1.4f, -0.32f),
                new Vector3(0.22f, 0.12f, 0.22f),
                tungsten,
                Quaternion.Euler(90f, 0f, 0f));
            CreateDecorativePrimitive(
                "Cooperate Covenant Link",
                PrimitiveType.Cube,
                station.transform,
                new Vector3(0f, 1.4f, -0.32f),
                new Vector3(0.55f, 0.1f, 0.12f),
                tungsten,
                Quaternion.identity);

            highlight = CreateHighlight(
                "Cooperate Station Focus",
                PrimitiveType.Cube,
                target.transform,
                new Vector3(1.12f, 1.1f, 1.18f),
                tungsten);

            var labelObject = new GameObject("Cooperate Consequence Label");
            labelObject.transform.SetParent(station.transform, false);
            labelObject.transform.localPosition =
                new Vector3(0f, 2.82f, -0.15f);
            label = ConfigureLabel(
                labelObject,
                fontSize: 44,
                characterSize: 0.036f);
            label.text =
                "COOPERATE\nFIELD SLEEVE + OBLIGATION";
            return station;
        }

        private GameObject CreateTakeBearingStation(
            Material oxide,
            Material bone,
            Material signal,
            out GameObject highlight,
            out TextMesh label)
        {
            var station = new GameObject("Take Bearing Claim Cradle");
            station.transform.SetParent(transform, false);
            station.transform.localPosition = new Vector3(2.35f, 0f, -2.25f);

            GameObject target = CreateInteractivePrimitive(
                TakeBearingStationName,
                PrimitiveType.Cylinder,
                station.transform,
                new Vector3(0f, 0.82f, 0f),
                new Vector3(0.92f, 0.82f, 0.92f),
                oxide,
                DepotDecisionControl.TakeBearing);
            CreateDecorativePrimitive(
                "Take Bearing Raised Race",
                PrimitiveType.Cylinder,
                station.transform,
                new Vector3(0f, 1.65f, -0.15f),
                new Vector3(0.62f, 0.22f, 0.62f),
                bone,
                Quaternion.Euler(90f, 0f, 0f));
            CreateDecorativePrimitive(
                "Take Bearing Claim Spike Left",
                PrimitiveType.Cube,
                station.transform,
                new Vector3(-0.72f, 1.55f, 0f),
                new Vector3(0.18f, 1.32f, 0.18f),
                signal,
                Quaternion.Euler(0f, 0f, -14f));
            CreateDecorativePrimitive(
                "Take Bearing Claim Spike Right",
                PrimitiveType.Cube,
                station.transform,
                new Vector3(0.72f, 1.55f, 0f),
                new Vector3(0.18f, 1.32f, 0.18f),
                signal,
                Quaternion.Euler(0f, 0f, 14f));

            highlight = CreateHighlight(
                "Take Bearing Station Focus",
                PrimitiveType.Cylinder,
                target.transform,
                new Vector3(1.16f, 1.08f, 1.16f),
                signal);

            var labelObject = new GameObject("Take Bearing Consequence Label");
            labelObject.transform.SetParent(station.transform, false);
            labelObject.transform.localPosition =
                new Vector3(0f, 2.82f, -0.15f);
            label = ConfigureLabel(
                labelObject,
                fontSize: 44,
                characterSize: 0.036f);
            label.text =
                "TAKE BEARING\nCERAMIC + GRIEVANCE";
            return station;
        }

        private GameObject CreateInteractivePrimitive(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            DepotDecisionControl decision)
        {
            GameObject target = GameObject.CreatePrimitive(primitiveType);
            target.name = name;
            target.layer = InteractionLayer;
            target.transform.SetParent(parent, false);
            target.transform.localPosition = position;
            target.transform.localScale = scale;
            target.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = target.GetComponent<Collider>();
            collider.isTrigger = true;
            _targets.Add(collider, decision);
            return target;
        }

        private static GameObject CreateDecorativePrimitive(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion rotation)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.transform.localRotation = rotation;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return primitive;
        }

        private static GameObject CreateHighlight(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 scale,
            Material material)
        {
            GameObject highlight = GameObject.CreatePrimitive(primitiveType);
            highlight.name = name;
            highlight.transform.SetParent(parent, false);
            highlight.transform.localPosition = Vector3.zero;
            highlight.transform.localRotation = Quaternion.identity;
            highlight.transform.localScale = scale;
            highlight.GetComponent<Renderer>().sharedMaterial = material;
            Collider? collider = highlight.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            highlight.SetActive(false);
            return highlight;
        }

        private static TextMesh ConfigureLabel(
            GameObject owner,
            int fontSize,
            float characterSize)
        {
            TextMesh label = owner.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = fontSize;
            label.characterSize = characterSize;
            label.color = new Color32(230, 213, 169, 255);
            return label;
        }

        private void FaceCamera(TextMesh? label)
        {
            if (label == null || _camera == null)
            {
                return;
            }

            Transform labelTransform = label.transform;
            Vector3 towardCamera =
                _camera.transform.position - labelTransform.position;
            if (towardCamera.sqrMagnitude > 0.001f)
            {
                labelTransform.rotation = Quaternion.LookRotation(
                    towardCamera.normalized,
                    Vector3.up);
            }
        }

        private static string DecisionPrompt(
            DepotDecisionControl decision)
        {
            return decision == DepotDecisionControl.Cooperate
                ? "E / SOUTH · COOPERATE · field sleeve + obligation"
                : "E / SOUTH · TAKE BEARING · ceramic + grievance";
        }

        private static Material RequireMaterial(
            Material? material,
            string parameterName)
        {
            return material ??
                throw new ArgumentNullException(parameterName);
        }

        private static bool IsActive(GameObject? value)
        {
            return value != null && value.activeSelf;
        }

        private static void SetActive(GameObject? value, bool active)
        {
            value?.SetActive(active);
        }
    }
}
