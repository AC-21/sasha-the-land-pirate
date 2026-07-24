#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.UIElements;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public sealed class LastBearingFieldDeskPerformanceTopology
    {
        private readonly object? _documentObjectIdentity;
        private readonly object? _documentIdentity;
        private readonly object? _panelSettingsIdentity;
        private readonly object? _visualRootIdentity;
        private readonly object[] _bindingIdentities;

        internal LastBearingFieldDeskPerformanceTopology(
            object? documentObjectIdentity,
            object? documentIdentity,
            object? panelSettingsIdentity,
            object? visualRootIdentity,
            object[] bindingIdentities,
            int ownedUnityObjectCount,
            int visualElementCount,
            int bindingCount,
            int registeredCallbackCount)
        {
            _documentObjectIdentity = documentObjectIdentity;
            _documentIdentity = documentIdentity;
            _panelSettingsIdentity = panelSettingsIdentity;
            _visualRootIdentity = visualRootIdentity;
            _bindingIdentities = bindingIdentities;
            OwnedUnityObjectCount = ownedUnityObjectCount;
            VisualElementCount = visualElementCount;
            BindingCount = bindingCount;
            RegisteredCallbackCount = registeredCallbackCount;
        }

        public int OwnedUnityObjectCount { get; }

        public int VisualElementCount { get; }

        public int BindingCount { get; }

        public int RegisteredCallbackCount { get; }

        internal object? DocumentObjectIdentity => _documentObjectIdentity;

        internal object? DocumentIdentity => _documentIdentity;

        internal object? PanelSettingsIdentity => _panelSettingsIdentity;

        internal object? VisualRootIdentity => _visualRootIdentity;

        internal object[] BindingIdentities => _bindingIdentities;
    }

    [DisallowMultipleComponent]
    public sealed class LastBearingFieldDesk : MonoBehaviour
    {
        private const string LayoutResource = "LastBearingFieldDeskLayout";
        private const string StylesResource = "LastBearingFieldDeskStyles";
        private const string ThemeResource = "LastBearingFieldDeskTheme";
        private const float RefreshIntervalSeconds = 1f;

        private LastBearingGameController? _controller;
        private GameObject? _documentObject;
        private PanelSettings? _panelSettings;
        private UIDocument? _document;
        private VisualElement? _overlay;
        private VisualElement? _desk;
        private ScrollView? _scroll;
        private Foldout? _audit;
        private Label? _auditHash;
        private Label? _composition;
        private Label? _pauseState;
        private Label? _water;
        private Label? _waterTrend;
        private Label? _parts;
        private Label? _fuel;
        private Label? _turbine;
        private Label? _pressure;
        private Label? _frontForecast;
        private Label? _permitChapter;
        private Label? _permitStep;
        private Label? _permitHeadline;
        private Label? _permitDetail;
        private Label? _permitProgressLabel;
        private ProgressBar? _permitProgress;
        private Label? _permitCue;
        private Label? _currentDetail;
        private Label? _secondaryDetail;
        private VisualElement? _survey;
        private Label? _surveyHypothesis;
        private Label? _surveyEvidence;
        private Label? _status;
        private Label? _saveStatus;
        private ButtonBinding[] _bindings = Array.Empty<ButtonBinding>();
        private ButtonBinding? _primary;
        private ButtonBinding? _secondary;
        private float _nextRefreshTime;
        private int _lastDispatchFrame = -1;
        private ulong _lastStamp;
        private bool _hasStamp;
        private bool _visible;
        private bool _auditCallbackRegistered;
        private bool _physicalWorkRouted;

        public bool IsOperational { get; private set; }

        public bool OwnsCityOverview =>
            IsOperational &&
            _visible &&
            _overlay != null &&
            _controller?.IsExactFieldDeskCityOverview == true;

        public bool OwnsKeyboardFocus
        {
            get
            {
                if (!OwnsCityOverview || _desk == null)
                {
                    return false;
                }

                var focused = _document?.rootVisualElement.panel
                    ?.focusController.focusedElement as VisualElement;
                return focused != null &&
                       (ReferenceEquals(focused, _desk) ||
                        _desk.Contains(focused));
            }
        }

        public bool BlocksWorldPointer(Vector2 screenPosition)
        {
            if (!OwnsCityOverview ||
                _desk?.panel == null)
            {
                return false;
            }

            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(
                _desk.panel,
                new Vector2(
                    screenPosition.x,
                    Screen.height - screenPosition.y));
            return _desk.worldBound.Contains(panelPosition);
        }

        public LastBearingFieldDeskPerformanceTopology
            CapturePerformanceTopology()
        {
            var bindingIdentities = new object[_bindings.Length];
            for (var index = 0; index < _bindings.Length; index++)
            {
                bindingIdentities[index] = _bindings[index];
            }

            int ownedUnityObjectCount = 0;
            if (_documentObject != null) ownedUnityObjectCount++;
            if (_document != null) ownedUnityObjectCount++;
            if (_panelSettings != null) ownedUnityObjectCount++;

            return new LastBearingFieldDeskPerformanceTopology(
                _documentObject,
                _document,
                _panelSettings,
                _document?.rootVisualElement,
                bindingIdentities,
                ownedUnityObjectCount,
                CountVisualElements(_document?.rootVisualElement),
                _bindings.Length,
                CountRegisteredCallbacks());
        }

        public bool MatchesPerformanceTopology(
            LastBearingFieldDeskPerformanceTopology baseline)
        {
            if (baseline == null)
            {
                throw new ArgumentNullException(nameof(baseline));
            }

            if (!ReferenceEquals(
                    baseline.DocumentObjectIdentity,
                    _documentObject) ||
                !ReferenceEquals(baseline.DocumentIdentity, _document) ||
                !ReferenceEquals(
                    baseline.PanelSettingsIdentity,
                    _panelSettings) ||
                !ReferenceEquals(
                    baseline.VisualRootIdentity,
                    _document?.rootVisualElement) ||
                baseline.BindingIdentities.Length != _bindings.Length ||
                baseline.OwnedUnityObjectCount !=
                    ((_documentObject == null ? 0 : 1) +
                     (_document == null ? 0 : 1) +
                     (_panelSettings == null ? 0 : 1)) ||
                baseline.VisualElementCount !=
                    CountVisualElements(_document?.rootVisualElement) ||
                baseline.RegisteredCallbackCount != CountRegisteredCallbacks())
            {
                return false;
            }

            for (var index = 0; index < _bindings.Length; index++)
            {
                if (!ReferenceEquals(
                        baseline.BindingIdentities[index],
                        _bindings[index]) ||
                    !_bindings[index].IsRegistered)
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TrySubmitPauseTwiceForNativePerformanceGate(
            out int firstSubmitCommandDelta,
            out int duplicateSubmitCommandDelta)
        {
            firstSubmitCommandDelta = 0;
            duplicateSubmitCommandDelta = 0;
            if (_controller == null ||
                !OwnsCityOverview ||
                _bindings.Length != 18 ||
                !_bindings[14].IsRegistered ||
                _bindings[14].Intent != LastBearingFieldDeskIntent.TogglePause)
            {
                return false;
            }

            int before =
                _controller.PendingPlayerCommandCountForPerformance;
            _bindings[14].SubmitForNativePerformanceGate();
            int afterFirst =
                _controller.PendingPlayerCommandCountForPerformance;
            _bindings[14].SubmitForNativePerformanceGate();
            int afterDuplicate =
                _controller.PendingPlayerCommandCountForPerformance;
            firstSubmitCommandDelta = afterFirst - before;
            duplicateSubmitCommandDelta = afterDuplicate - afterFirst;
            return firstSubmitCommandDelta == 1 &&
                   duplicateSubmitCommandDelta == 0;
        }

        public void Configure(LastBearingGameController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            _controller = controller;
            if (IsOperational)
            {
                Refresh(force: true);
                return;
            }

            try
            {
                CreateDocument();
                CacheElements();
                RegisterCallbacks();
                IsOperational = true;
                Refresh(force: true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Last Bearing Field Desk unavailable; legacy controls remain active. " +
                    exception.GetType().Name,
                    this);
                FailOpenCleanup();
            }
        }

        public void Refresh(bool force = false)
        {
            if (!IsOperational || _controller == null || _overlay == null)
            {
                return;
            }

            if (!_controller.IsExactFieldDeskCityOverview)
            {
                _physicalWorkRouted = false;
                HideAndResetTransient();
                return;
            }

            if (_physicalWorkRouted)
            {
                bool physicalControlStillFocused =
                    _controller.IsEmergencyCisternPumpFocused ||
                    _controller.IsDustFrontRelayFocused ||
                    _controller.IsFieldSleeveServiceFocused;
                if (physicalControlStillFocused)
                {
                    HideAndResetTransient();
                    _controller.SetLegacyHudSuppressedByFieldDesk(true);
                    return;
                }

                _physicalWorkRouted = false;
            }

            LastBearingFieldDeskStamp stamp =
                LastBearingFieldDeskPresenter.CaptureStamp(_controller);
            SetDeskVisible(true);
            bool projectionChanged =
                !_hasStamp || _lastStamp != stamp.Value;
            float now = Time.unscaledTime;
            if (!force && !projectionChanged && now < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = now + RefreshIntervalSeconds;
            if (!force && !projectionChanged)
            {
                return;
            }

            LastBearingFieldDeskProjection projection =
                LastBearingFieldDeskPresenter.Present(_controller);
            ApplyProjection(projection);
            _lastStamp = stamp.Value;
            _hasStamp = true;
        }

        public void ResetForLifecycle()
        {
            if (!IsOperational)
            {
                return;
            }

            _lastDispatchFrame = Time.frameCount;
            _physicalWorkRouted = false;
            HideAndResetTransient();
        }

        private void OnDestroy()
        {
            _controller?.SetLegacyHudSuppressedByFieldDesk(false);
            UnregisterCallbacks();
            DestroyOwnedObjects();
            IsOperational = false;
        }

        private void CreateDocument()
        {
            VisualTreeAsset? layout =
                Resources.Load<VisualTreeAsset>(LayoutResource);
            StyleSheet? styles = Resources.Load<StyleSheet>(StylesResource);
            ThemeStyleSheet? theme =
                Resources.Load<ThemeStyleSheet>(ThemeResource);
            if (layout == null || styles == null || theme == null)
            {
                throw new InvalidOperationException("Field Desk resources are missing.");
            }

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "Last Bearing Field Desk Panel";
            _panelSettings.hideFlags = HideFlags.DontSave;
            _panelSettings.themeStyleSheet = theme;
            _panelSettings.renderMode = PanelRenderMode.ScreenSpaceOverlay;
            _panelSettings.clearColor = false;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = 1f;
            _panelSettings.sortingOrder = 100f;

            _documentObject = new GameObject("Last Bearing Field Desk Document");
            _documentObject.hideFlags = HideFlags.DontSave;
            _documentObject.transform.SetParent(transform, false);
            _documentObject.SetActive(false);
            _document = _documentObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _document.sortingOrder = 100f;
            _documentObject.SetActive(true);

            VisualElement root = _document.rootVisualElement;
            root.styleSheets.Add(styles);
            layout.CloneTree(root);
            _overlay = Require<VisualElement>(root, "field-desk-overlay");
            _overlay.style.display = DisplayStyle.None;
        }

        private void CacheElements()
        {
            VisualElement root = _document!.rootVisualElement;
            _desk = Require<VisualElement>(root, "field-desk");
            _scroll = Require<ScrollView>(root, "desk-scroll");
            _audit = Require<Foldout>(root, "audit-foldout");
            _auditHash = Require<Label>(root, "audit-hash-label");
            _composition = Require<Label>(root, "composition-label");
            _pauseState = Require<Label>(root, "pause-label");
            _water = Require<Label>(root, "water-label");
            _waterTrend = Require<Label>(root, "water-trend-label");
            _parts = Require<Label>(root, "parts-label");
            _fuel = Require<Label>(root, "fuel-label");
            _turbine = Require<Label>(root, "turbine-label");
            _pressure = Require<Label>(root, "pressure-label");
            _frontForecast = Require<Label>(
                root,
                "front-forecast-label");
            _permitChapter = Require<Label>(root, "permit-chapter-label");
            _permitStep = Require<Label>(root, "permit-step-label");
            _permitHeadline = Require<Label>(root, "permit-headline-label");
            _permitDetail = Require<Label>(root, "permit-detail-label");
            _permitProgressLabel = Require<Label>(root, "permit-progress-label");
            _permitProgress = Require<ProgressBar>(root, "permit-progress-bar");
            _permitCue = Require<Label>(root, "permit-cue-label");
            _currentDetail = Require<Label>(root, "current-action-detail");
            _secondaryDetail = Require<Label>(
                root,
                "secondary-action-detail");
            _survey = Require<VisualElement>(root, "survey-section");
            _surveyHypothesis = Require<Label>(root, "survey-hypothesis-label");
            _surveyEvidence = Require<Label>(root, "survey-evidence-label");
            _status = Require<Label>(root, "status-label");
            _saveStatus = Require<Label>(root, "save-status-label");

            _primary = Bind(root, "primary-action-button");
            _secondary = Bind(root, "secondary-action-button");
            _bindings = new[]
            {
                _primary,
                _secondary,
                Bind(root, "trial-a-button"),
                Bind(root, "trial-b-button"),
                Bind(root, "manipulate-button"),
                Bind(root, "rotate-button"),
                Bind(root, "toggle-piece-button"),
                Bind(root, "connect-button"),
                Bind(root, "advance-button"),
                Bind(root, "record-clear-button"),
                Bind(root, "record-unclear-button"),
                Bind(root, "reset-trial-button"),
                Bind(root, "leave-trial-button"),
                Bind(root, "clear-trials-button"),
                Bind(root, "pause-button"),
                Bind(root, "save-button"),
                Bind(root, "load-button"),
                Bind(root, "title-button"),
            };
        }

        private void RegisterCallbacks()
        {
            for (var index = 0; index < _bindings.Length; index++)
            {
                _bindings[index].Register();
            }

            if (!_auditCallbackRegistered)
            {
                _audit!.RegisterValueChangedCallback(OnAuditChanged);
                _auditCallbackRegistered = true;
            }
        }

        private void UnregisterCallbacks()
        {
            for (var index = 0; index < _bindings.Length; index++)
            {
                _bindings[index].Unregister();
            }

            if (_auditCallbackRegistered)
            {
                _audit?.UnregisterValueChangedCallback(OnAuditChanged);
                _auditCallbackRegistered = false;
            }
        }

        private void ApplyProjection(LastBearingFieldDeskProjection projection)
        {
            SetText(_composition!, projection.Composition);
            SetText(_pauseState!, projection.PauseState);
            SetText(_water!, projection.WaterAmount);
            SetText(_waterTrend!, projection.WaterTrend);
            SetText(_parts!, projection.Parts);
            SetText(_fuel!, projection.Fuel);
            SetText(_turbine!, projection.Turbine);
            SetText(_pressure!, projection.Pressure);
            SetText(_frontForecast!, projection.DryLine.Forecast);

            LastBearingPermitJobPresentation job = projection.PermitJob;
            SetText(_permitChapter!, job.ChapterLabel);
            SetText(_permitStep!, "STEP " + job.StepIndex + " / " + job.StepCount);
            SetText(_permitHeadline!, job.Headline);
            SetText(_permitDetail!, job.Detail);
            SetText(_permitProgressLabel!, job.ProgressLabel);
            SetVisible(_permitProgress!, job.HasMeasuredPhaseProgress);
            if (job.HasMeasuredPhaseProgress)
            {
                _permitProgress!.lowValue = 0f;
                _permitProgress.highValue = job.PhaseProgressTarget;
                _permitProgress.SetValueWithoutNotify(job.PhaseProgressCurrent);
            }

            SetText(_permitCue!, job.RecommendedFirstRunCue);
            SetVisible(_permitCue!, job.ShowRecommendedFirstRunCue);
            SetText(_currentDetail!, projection.PrimaryAction.Detail);
            SetVisible(_currentDetail!, projection.PrimaryAction.IsVisible);
            SetText(_secondaryDetail!, projection.SecondaryAction.Detail);
            SetVisible(_secondaryDetail!, projection.SecondaryAction.IsVisible);
            _primary!.Apply(projection.PrimaryAction);
            _secondary!.Apply(projection.SecondaryAction);

            SetVisible(_survey!, projection.Survey.IsVisible);
            SetText(_surveyHypothesis!, projection.Survey.HypothesisLabel);
            SetText(_surveyEvidence!, projection.Survey.Evidence);
            _bindings[2].Apply(projection.Survey.SelectRecycler);
            _bindings[3].Apply(projection.Survey.SelectMachineShop);
            _bindings[4].Apply(projection.Survey.SelectEmergencyStorage);
            _bindings[5].Apply(projection.Survey.Rotate);
            _bindings[6].Apply(projection.Survey.PreviousPad);
            _bindings[7].Apply(projection.Survey.NextPad);
            _bindings[8].Apply(projection.Survey.Place);
            _bindings[9].Apply(projection.Survey.ConnectLink);
            _bindings[10].Apply(projection.Survey.StaffHuman);
            _bindings[11].Apply(projection.Survey.StaffRobot);
            _bindings[12].Apply(projection.Survey.AdvanceSled);
            _bindings[13].Apply(projection.Survey.CancelPreview);
            _bindings[14].Apply(projection.PauseAction);
            _bindings[15].Apply(projection.SaveAction);
            _bindings[16].Apply(projection.LoadAction);
            _bindings[17].Apply(projection.TitleAction);
            SetText(_status!, projection.ControllerStatus);
            SetText(_saveStatus!, projection.SaveStatus);
            if (_audit!.value)
            {
                UpdateAudit();
            }
        }

        private void Dispatch(ButtonBinding source)
        {
            if (_controller == null ||
                !IsOperational ||
                !_controller.IsExactFieldDeskCityOverview ||
                _controller.HasPendingPlayerCommands ||
                _lastDispatchFrame == Time.frameCount ||
                !LastBearingFieldDeskPresenter.IsIntentAvailable(
                    _controller,
                    source.Intent))
            {
                return;
            }

            _lastDispatchFrame = Time.frameCount;
            source.DisableForDispatch();
            switch (source.Intent)
            {
                case LastBearingFieldDeskIntent.AssignDefaultLead: _controller.AssignDefaultLeadResident(); break;
                case LastBearingFieldDeskIntent.InspectCityNeed: _controller.InspectCityNeed(); break;
                case LastBearingFieldDeskIntent.SelectRecycler: _controller.SelectCityBuildingPreview(CityBuildingKind.Recycler); break;
                case LastBearingFieldDeskIntent.SelectMachineShop: _controller.SelectCityBuildingPreview(CityBuildingKind.MachineShop); break;
                case LastBearingFieldDeskIntent.SelectEmergencyStorage: _controller.SelectCityBuildingPreview(CityBuildingKind.EmergencyStorage); break;
                case LastBearingFieldDeskIntent.RotateCityBuilding: _controller.RotateCityBuildingPreview(); break;
                case LastBearingFieldDeskIntent.PreviousCityPad: _controller.MoveCityBuildingPreview(-1); break;
                case LastBearingFieldDeskIntent.NextCityPad: _controller.MoveCityBuildingPreview(1); break;
                case LastBearingFieldDeskIntent.PlaceCityBuilding: _controller.PlaceCityBuildingPreview(); break;
                case LastBearingFieldDeskIntent.ConnectCityServiceLink: _controller.ConnectCityServiceLink(); break;
                case LastBearingFieldDeskIntent.StaffCityServiceHuman: _controller.AssignCityServiceResident(ResidentRoster.HumanResidentId); break;
                case LastBearingFieldDeskIntent.StaffCityServiceRobot: _controller.AssignCityServiceResident(ResidentRoster.RobotResidentId); break;
                case LastBearingFieldDeskIntent.AdvanceCityServiceSled: _controller.AdvanceCityServiceSled(); break;
                case LastBearingFieldDeskIntent.CancelCityBuildingPreview: _controller.CancelCityBuildingPreview(); break;
                case LastBearingFieldDeskIntent.RunHotShift: _controller.StartHotShift(); break;
                case LastBearingFieldDeskIntent.AcknowledgeDustFront:
                    _controller.AcknowledgeDustFrontFallback();
                    break;
                case LastBearingFieldDeskIntent.OpenEmergencyCisternPump:
                    _controller.OpenEmergencyCisternPump();
                    _physicalWorkRouted =
                        _controller.IsEmergencyCisternPumpFocused;
                    HideAndResetTransient();
                    break;
                case LastBearingFieldDeskIntent.OpenDustFrontRelay:
                    _controller.OpenDustFrontRelay();
                    _physicalWorkRouted =
                        _controller.IsDustFrontRelayFocused;
                    HideAndResetTransient();
                    break;
                case LastBearingFieldDeskIntent.BeginWorkshopPush: _controller.BeginGaragePlan(PreparationChoice.WorkshopPush); break;
                case LastBearingFieldDeskIntent.BeginCivicBuffer: _controller.BeginGaragePlan(PreparationChoice.CivicBuffer); break;
                case LastBearingFieldDeskIntent.OpenGarage: _controller.OpenGarageBay(); break;
                case LastBearingFieldDeskIntent.CommitExpedition: _controller.CommitExpedition(); break;
                case LastBearingFieldDeskIntent.OpenPumpHallRepair: _controller.OpenPumpHallRepair(); break;
                case LastBearingFieldDeskIntent.OpenOneGoodBatchWorkshop: _controller.OpenOneGoodBatchWorkshop(); break;
                case LastBearingFieldDeskIntent.OpenPumpHallImprovement: _controller.OpenPumpHallImprovement(); break;
                case LastBearingFieldDeskIntent.ServiceFieldSleeve:
                    _controller.OpenFieldSleeveService();
                    _physicalWorkRouted =
                        _controller.IsFieldSleeveServiceFocused;
                    HideAndResetTransient();
                    break;
                case LastBearingFieldDeskIntent.TogglePause: _controller.TogglePause(); break;
                case LastBearingFieldDeskIntent.Save: _controller.Save(); break;
                case LastBearingFieldDeskIntent.Load: _controller.Load(); break;
                case LastBearingFieldDeskIntent.ReturnToTitle: _controller.ReturnToTitle(); break;
            }

            _hasStamp = false;
            Refresh(force: true);
        }

        private void OnAuditChanged(ChangeEvent<bool> change)
        {
            if (change.newValue)
            {
                UpdateAudit();
            }
            else
            {
                SetText(_auditHash!, "AUDIT STOWED");
            }
        }

        private void UpdateAudit()
        {
            SetText(_auditHash!, "CANONICAL SHA-256 · " + (_controller?.CanonicalHash ?? "none"));
        }

        private void HideAndResetTransient()
        {
            if (!_visible && !_hasStamp && _nextRefreshTime == 0f)
            {
                return;
            }

            _document?.rootVisualElement.panel?.focusController
                .focusedElement?.Blur();
            SetDeskVisible(false);
            if (_scroll != null)
            {
                _scroll.scrollOffset = Vector2.zero;
            }

            _audit?.SetValueWithoutNotify(false);
            if (_auditHash != null)
            {
                SetText(_auditHash, "AUDIT STOWED");
            }

            _hasStamp = false;
            _nextRefreshTime = 0f;
        }

        private void SetDeskVisible(bool visible)
        {
            if (_overlay == null)
            {
                return;
            }

            _controller?.SetLegacyHudSuppressedByFieldDesk(visible);
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
            _overlay.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static T Require<T>(VisualElement root, string name)
            where T : VisualElement
        {
            T? element = root.Q<T>(name);
            return element ?? throw new InvalidOperationException(
                "Field Desk element missing: " + name);
        }

        private ButtonBinding Bind(VisualElement root, string name)
        {
            return new ButtonBinding(this, Require<Button>(root, name));
        }

        private static void SetText(Label label, string value)
        {
            if (label.text != value)
            {
                label.text = value;
            }
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display.value != display)
            {
                element.style.display = display;
            }
        }

        private int CountRegisteredCallbacks()
        {
            int count = _auditCallbackRegistered ? 1 : 0;
            for (var index = 0; index < _bindings.Length; index++)
            {
                if (_bindings[index].IsRegistered)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountVisualElements(VisualElement? element)
        {
            if (element == null)
            {
                return 0;
            }

            var count = 1;
            for (var index = 0; index < element.childCount; index++)
            {
                count = checked(count + CountVisualElements(element[index]));
            }

            return count;
        }

        private void FailOpenCleanup()
        {
            _controller?.SetLegacyHudSuppressedByFieldDesk(false);
            UnregisterCallbacks();
            if (_overlay != null)
            {
                _overlay.style.display = DisplayStyle.None;
            }

            DestroyOwnedObjects();
            IsOperational = false;
        }

        private void DestroyOwnedObjects()
        {
            if (_documentObject != null)
            {
                if (Application.isPlaying) Destroy(_documentObject);
                else DestroyImmediate(_documentObject);
            }

            if (_panelSettings != null)
            {
                if (Application.isPlaying) Destroy(_panelSettings);
                else DestroyImmediate(_panelSettings);
            }

            _documentObject = null;
            _document = null;
            _panelSettings = null;
            _overlay = null;
        }

        private sealed class ButtonBinding
        {
            private readonly LastBearingFieldDesk _owner;
            private readonly Button _button;
            private bool _registered;

            internal ButtonBinding(LastBearingFieldDesk owner, Button button)
            {
                _owner = owner;
                _button = button;
            }

            internal LastBearingFieldDeskIntent Intent { get; private set; }

            internal bool IsRegistered => _registered;

            internal void Register()
            {
                if (_registered) return;
                _button.clicked += OnClicked;
                _registered = true;
            }

            internal void Unregister()
            {
                if (!_registered) return;
                _button.clicked -= OnClicked;
                _registered = false;
            }

            internal void Apply(LastBearingFieldDeskActionProjection action)
            {
                Intent = action.Intent;
                if (_button.text != action.Label) _button.text = action.Label;
                SetVisible(_button, action.IsVisible);
                if (_button.enabledSelf != action.IsEnabled)
                {
                    _button.SetEnabled(action.IsEnabled);
                }

                _button.EnableInClassList("action-primary", action.Tone == LastBearingFieldDeskActionTone.Primary);
                _button.EnableInClassList("action-signal", action.Tone == LastBearingFieldDeskActionTone.Signal);
                _button.EnableInClassList("action-hazard", action.Tone == LastBearingFieldDeskActionTone.Hazard);
            }

            internal void DisableForDispatch()
            {
                _button.SetEnabled(false);
            }

            internal void SubmitForNativePerformanceGate()
            {
                using (NavigationSubmitEvent submit =
                       NavigationSubmitEvent.GetPooled())
                {
                    submit.target = _button;
                    _button.SendEvent(submit);
                }
            }

            private void OnClicked()
            {
                _owner.Dispatch(this);
            }
        }
    }
}
