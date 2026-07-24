#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Direct depot interaction for the one canonical repair-cargo source.
    /// The target follows the cargo view's derived anchor and delegates the
    /// accepted load to the existing controller verb.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingDepotCargoInteractor : MonoBehaviour
    {
        public const int InteractionLayer = 30;
        public const string RootName =
            "Take It Aboard Cargo Interaction [Derived Only]";
        public const string SourceTargetName =
            "INTERACT_DEPOT_REPAIR_CARGO_SOURCE";
        public const string FeedbackLabelName =
            "DEPOT_REPAIR_CARGO_SOURCE_FEEDBACK";

        private const int RaycastBufferSize = 6;
        private const float RaycastDistance = 500f;

        private readonly RaycastHit[] _raycastHits =
            new RaycastHit[RaycastBufferSize];

        private LastBearingGameController? _controller;
        private LastBearingReadModel? _model;
        private Transform? _sourceAnchor;
        private Camera? _camera;
        private GameObject? _sourceTarget;
        private GameObject? _focusHalo;
        private TextMesh? _feedbackLabel;
        private Collider? _sourceCollider;
        private Vector2 _lastPointerPosition;
        private bool _hasPointerPosition;
        private bool _built;

        public bool IsSourceFocused { get; private set; }

        public string Feedback { get; private set; } = string.Empty;

        public bool LastInteractionRejected { get; private set; }

        public bool IsBuilt => _built;

        public bool IsSourceTargetVisible => IsActive(_sourceTarget);

        public bool IsSourceHighlighted => IsActive(_focusHalo);

        public bool HasDedicatedInteractionTarget =>
            _sourceCollider != null &&
            _sourceCollider.isTrigger &&
            _sourceCollider.gameObject.layer == InteractionLayer;

        public Vector3 SourceTargetWorldPosition =>
            _sourceTarget?.transform.position ?? transform.position;

        internal void Build(
            Transform sourceAnchor,
            Material tungsten,
            Material signal)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            gameObject.name = RootName;
            _sourceAnchor = sourceAnchor ??
                throw new ArgumentNullException(nameof(sourceAnchor));
            tungsten = tungsten ??
                throw new ArgumentNullException(nameof(tungsten));
            signal = signal ??
                throw new ArgumentNullException(nameof(signal));

            _sourceTarget = new GameObject(SourceTargetName);
            _sourceTarget.layer = InteractionLayer;
            _sourceTarget.transform.SetParent(transform, false);
            var collider = _sourceTarget.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.35f, 1.2f, 1.35f);
            collider.center = new Vector3(0f, 0.08f, 0f);
            collider.isTrigger = true;
            _sourceCollider = collider;

            CreateVisual(
                "Take It Aboard Load Bracket",
                PrimitiveType.Cube,
                _sourceTarget.transform,
                new Vector3(0f, -0.42f, -0.12f),
                new Vector3(0.92f, 0.08f, 0.62f),
                Quaternion.identity,
                tungsten);
            _focusHalo = CreateVisual(
                "Take It Aboard Focus Halo",
                PrimitiveType.Cylinder,
                _sourceTarget.transform,
                new Vector3(0f, -0.48f, 0f),
                new Vector3(0.82f, 0.035f, 0.82f),
                Quaternion.identity,
                signal);

            var feedback = new GameObject(FeedbackLabelName);
            feedback.transform.SetParent(transform, false);
            feedback.transform.localPosition =
                new Vector3(0f, 1.45f, -0.25f);
            _feedbackLabel = feedback.AddComponent<TextMesh>();
            _feedbackLabel.anchor = TextAnchor.MiddleCenter;
            _feedbackLabel.alignment = TextAlignment.Center;
            _feedbackLabel.fontSize = 48;
            _feedbackLabel.characterSize = 0.04f;
            _feedbackLabel.color = new Color32(230, 213, 169, 255);

            SyncToSourceAnchor();
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
            RefreshVisuals();
        }

        internal void Apply(LastBearingReadModel model)
        {
            _model = model ??
                throw new ArgumentNullException(nameof(model));
            SyncToSourceAnchor();
            NormalizeFocus();
            ApplyVisibility();
            RefreshVisuals();
            Physics.SyncTransforms();
        }

        public void ResetLocalFocus()
        {
            _model = null;
            IsSourceFocused = false;
            Feedback = string.Empty;
            LastInteractionRejected = false;
            _hasPointerPosition = false;
            ApplyVisibility();
            RefreshVisuals();
        }

        public void MoveFocus(int direction)
        {
            _ = direction;
            if (!HasCurrentSourcePresentation())
            {
                ResetLocalFocus();
                return;
            }

            FocusSource();
        }

        public bool OperateFocused()
        {
            if (!IsSourceFocused)
            {
                Reject("FOCUS THE REPAIR CARGO FIRST");
                return false;
            }

            return ActivateSource();
        }

        public bool ActivateSource()
        {
            if (!HasCurrentSourcePresentation())
            {
                IsSourceFocused = false;
                ApplyVisibility();
                Reject("CARGO SOURCE STALE · no load queued");
                return false;
            }

            FocusSource();
            if (_controller == null || _model == null)
            {
                Reject("CARGO SOURCE STALE · no load queued");
                return false;
            }

            if (_controller.HasPendingPlayerCommands)
            {
                SetFeedback(
                    _controller.IsDepotRepairCargoLoadQueued
                        ? "REPAIR CARGO LOAD ALREADY QUEUED"
                        : "ACTION QUEUED · let the depot ledger accept it first",
                    rejected: !_controller.IsDepotRepairCargoLoadQueued);
                return false;
            }

            if (!_controller.IsDepotRepairCargoLoadAvailable)
            {
                Reject("REPAIR CARGO LOAD UNAVAILABLE · no load queued");
                return false;
            }

            RepairCargoKind kind = _model.RepairCargoKind;
            _controller.LoadDepotRepairCargo();
            if (!_controller.IsDepotRepairCargoLoadQueued)
            {
                Reject(_controller.Status);
                return false;
            }

            SetFeedback(
                kind == RepairCargoKind.FieldSleeve
                    ? "FIELD SLEEVE LIFT QUEUED · SASHA HOLDS FOR THE LEDGER"
                    : "CERAMIC BEARING LIFT QUEUED · SASHA HOLDS FOR THE LEDGER",
                rejected: false);
            return true;
        }

        public bool TryActivateAtScreenPosition(Vector2 screenPosition)
        {
            if (!HasCurrentSourcePresentation())
            {
                return false;
            }

            if (_controller?.FieldDesk?.BlocksWorldPointer(screenPosition) ==
                true)
            {
                Reject("FIELD DESK HAS THIS POINTER · cargo load ignored");
                return false;
            }

            if (!TryRaycastSource(screenPosition))
            {
                return false;
            }

            FocusSource();
            return ActivateSource();
        }

        private void Update()
        {
            if (!HasCurrentSourcePresentation())
            {
                if (_model != null ||
                    IsSourceFocused ||
                    Feedback.Length != 0)
                {
                    ResetLocalFocus();
                }

                return;
            }

            Keyboard? keyboard = Keyboard.current;
            Gamepad? gamepad = Gamepad.current;
            bool navigated =
                keyboard?.leftArrowKey.wasPressedThisFrame == true ||
                keyboard?.rightArrowKey.wasPressedThisFrame == true ||
                gamepad?.dpad.left.wasPressedThisFrame == true ||
                gamepad?.dpad.right.wasPressedThisFrame == true;
            if (navigated)
            {
                FocusSource();
            }

            bool operated =
                keyboard?.eKey.wasPressedThisFrame == true ||
                gamepad?.buttonSouth.wasPressedThisFrame == true;
            if (operated)
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
                !operated &&
                pointerMoved &&
                TryRaycastSource(pointer))
            {
                FocusSource();
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

        private bool HasCurrentSourcePresentation()
        {
            return _built &&
                   _controller?.HasActiveGame == true &&
                   _model != null &&
                   ReferenceEquals(
                       _model,
                       _controller.RuntimeReadModel) &&
                   _model.IsRepairCargoLoadAvailable &&
                   (_controller.IsDepotRepairCargoLoadAvailable ||
                    _controller.IsDepotRepairCargoLoadQueued) &&
                   _controller.ModeCoordinator?.HasActiveMode == true &&
                   _controller.ModeCoordinator.CurrentMode ==
                       LastBearingPresentationMode.DepotEncounter &&
                   gameObject.activeInHierarchy;
        }

        private void NormalizeFocus()
        {
            if (!HasCurrentSourcePresentation())
            {
                IsSourceFocused = false;
                Feedback = string.Empty;
                LastInteractionRejected = false;
                return;
            }

            IsSourceFocused = true;
            if (Feedback.Length == 0)
            {
                Feedback = "TAKE IT ABOARD · E / GAMEPAD SOUTH";
                LastInteractionRejected = false;
            }
        }

        private void FocusSource()
        {
            if (!HasCurrentSourcePresentation())
            {
                return;
            }

            IsSourceFocused = true;
            SetFeedback(
                "TAKE IT ABOARD · E / GAMEPAD SOUTH",
                rejected: false);
        }

        private void ApplyVisibility()
        {
            bool visible = HasCurrentSourcePresentation();
            SetActive(_sourceTarget, visible);
            if (_feedbackLabel != null)
            {
                _feedbackLabel.gameObject.SetActive(visible);
            }
        }

        private void RefreshVisuals()
        {
            SetActive(
                _focusHalo,
                IsSourceTargetVisible && IsSourceFocused);
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

        private bool TryRaycastSource(Vector2 screenPosition)
        {
            if (_camera == null || _sourceCollider == null)
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
                if (_raycastHits[index].collider == _sourceCollider)
                {
                    return true;
                }
            }

            return false;
        }

        private void SyncToSourceAnchor()
        {
            if (_sourceAnchor == null)
            {
                return;
            }

            transform.position = _sourceAnchor.position;
            transform.rotation = _sourceAnchor.rotation;
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
            return value != null && value.activeSelf;
        }

        private static void SetActive(GameObject? value, bool active)
        {
            value?.SetActive(active);
        }
    }
}
