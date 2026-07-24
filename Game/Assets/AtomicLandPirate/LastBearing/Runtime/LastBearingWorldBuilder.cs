#nullable enable

using System;
using System.Collections.Generic;
using AtomicLandPirate.Presentation.LastBearing.RoadFeel;
using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Builds the entire Last Bearing first-look from inspectable primitives.
    /// It visualizes the core read model and owns no authoritative mechanics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingWorldBuilder : MonoBehaviour
    {
        public const string RuntimeMaterialTemplateResourcePath =
            "LastBearingRuntimeMaterialTemplate";

        public const string CityScaffoldRootName =
            "City Scaffold [Derived Only]";

        private static readonly Color Bone = new Color32(216, 199, 161, 255);
        private static readonly Color Concrete = new Color32(119, 113, 104, 255);
        private static readonly Color ConcreteDark = new Color32(52, 51, 50, 255);
        private static readonly Color Iron = new Color32(36, 37, 38, 255);
        private static readonly Color Oxide = new Color32(120, 60, 36, 255);
        private static readonly Color Tungsten = new Color32(255, 196, 107, 255);
        private static readonly Color WaterTeal = new Color32(62, 139, 136, 255);
        private static readonly Color SignalCyan = new Color32(92, 199, 208, 255);
        private static readonly Color StopRed = new Color32(168, 66, 50, 255);

        private readonly List<Material> _ownedMaterials = new List<Material>();
        private Shader? _runtimeMaterialShader;
        private Transform? _turbineRotor;
        private Transform? _waterFill;
        private Transform? _auxiliaryPumpRotor;
        private Material? _stopGlass;
        private Material? _waterMaterial;
        private Material? _workshopWindow;
        private Material? _civicWindow;
        private Material? _claimMaterial;
        private Light? _pumpHallLight;
        private Light? _auxiliaryPumpLight;
        private Light? _workshopLight;
        private Light? _canopyLight;
        private GameObject? _humanResident;
        private GameObject? _robotResident;
        private GameObject? _needInspectionMarker;
        private GameObject? _cityStagedPumpRotor;
        private GameObject? _cityInstalledAuxiliaryPump;
        private LastBearingVisualSnapshot _snapshot;
        private bool _built;

        public Camera? MainCamera { get; private set; }

        public LastBearingCameraRig? CameraRig { get; private set; }

        public RoadFeelChaseCamera? RoadChaseCamera { get; private set; }

        public LastBearingVehicleView? VehicleView { get; private set; }

        public RoadFeelRigInstance? RoadFeelRig { get; private set; }

        public LastBearingGarageBayView? GarageBayView { get; private set; }

        public Transform? CityScaffoldRoot { get; private set; }

        public LastBearingReturnServiceView? ReturnServiceView { get; private set; }

        public LastBearingPumpHallCutawayView? PumpHallCutawayView
        {
            get;
            private set;
        }

        public LastBearingOneGoodBatchCutawayView? OneGoodBatchCutawayView
        {
            get;
            private set;
        }

        public Transform? SelectedBuildingCutawayCameraAnchor { get; private set; }

        public Transform? SelectedBuildingCutawayFocusAnchor { get; private set; }

        public bool IsPumpHallCutawaySelected =>
            PumpHallCutawayView?.gameObject.activeSelf == true &&
            OneGoodBatchCutawayView?.gameObject.activeSelf != true;

        public bool IsOneGoodBatchCutawaySelected =>
            OneGoodBatchCutawayView?.gameObject.activeSelf == true &&
            PumpHallCutawayView?.gameObject.activeSelf != true;

        public LastBearingCityGrammarComparison? CityGrammarComparison { get; private set; }

        public LastBearingCityServiceCellView? CityServiceCellView
        {
            get;
            private set;
        }

        public LastBearingDepotApproachRecoveryView? DepotApproachRecoveryView
        {
            get;
            private set;
        }

        public LastBearingDepotCargoLoadingView? DepotCargoLoadingView
        {
            get;
            private set;
        }

        public LastBearingDepotDecisionInteractor? DepotDecisionInteractor
        {
            get;
            private set;
        }

        public LastBearingDepotReturnInteractor? DepotReturnInteractor
        {
            get;
            private set;
        }

        public LastBearingRouteModulePointView? RouteModulePointView
        {
            get;
            private set;
        }

        public Transform? TurbineRotor => _turbineRotor;

        public Transform? WaterFill => _waterFill;

        internal LastBearingVisualSnapshot Snapshot => _snapshot;

        public void Build(
            Transform? drivingModeRoot = null,
            Transform? buildingCutawayModeRoot = null,
            Transform? garageModeRoot = null,
            Transform? cityReturnModeRoot = null)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            ConfigureEnvironment();

            var concrete = CreateMaterial(Concrete, Color.black);
            var darkConcrete = CreateMaterial(ConcreteDark, Color.black);
            var iron = CreateMaterial(Iron, Color.black);
            var oxide = CreateMaterial(Oxide, Color.black);
            var bone = CreateMaterial(Bone, Color.black);
            _waterMaterial = CreateMaterial(WaterTeal, WaterTeal * 0.12f);
            _stopGlass = CreateMaterial(StopRed * 0.55f, StopRed * 1.2f);
            _workshopWindow = CreateMaterial(Iron, Color.black);
            _civicWindow = CreateMaterial(Iron, Color.black);
            _claimMaterial = CreateMaterial(Iron, SignalCyan * 0.8f);
            Material tungsten = CreateMaterial(
                Tungsten * 0.55f,
                Tungsten * 2.1f);
            Material signal = CreateMaterial(
                SignalCyan * 0.5f,
                SignalCyan * 1.5f);

            var cityScaffold = new GameObject(CityScaffoldRootName);
            cityScaffold.transform.SetParent(transform, false);
            CityScaffoldRoot = cityScaffold.transform;

            BuildGround(CityScaffoldRoot, darkConcrete, concrete, oxide);
            BuildWaterworks(
                CityScaffoldRoot,
                concrete,
                darkConcrete,
                iron,
                oxide,
                bone,
                tungsten);
            BuildSettlement(CityScaffoldRoot, concrete, iron, oxide, bone);
            BuildRoadAndDepot(
                CityScaffoldRoot,
                concrete,
                darkConcrete,
                iron,
                oxide,
                bone,
                tungsten,
                signal);
            BuildResidents(CityScaffoldRoot, oxide, bone, iron, concrete);
            BuildCityGrammarComparison(
                CityScaffoldRoot,
                concrete,
                iron,
                oxide,
                bone);
            BuildCityServiceCell(
                CityScaffoldRoot,
                concrete,
                iron,
                oxide,
                bone,
                tungsten,
                signal);
            BuildVehicle(
                iron,
                oxide,
                bone,
                darkConcrete,
                tungsten,
                signal);
            if (cityReturnModeRoot != null)
            {
                BuildReturnService(
                    cityReturnModeRoot,
                    concrete,
                    darkConcrete,
                    oxide,
                    bone,
                    tungsten,
                    signal);
            }

            if (garageModeRoot != null)
            {
                BuildGarageBay(
                    garageModeRoot,
                    concrete,
                    darkConcrete,
                    oxide,
                    bone,
                    tungsten,
                    signal);
            }

            if (buildingCutawayModeRoot != null)
            {
                BuildPumpHallCutaway(
                    buildingCutawayModeRoot,
                    concrete,
                    darkConcrete,
                    oxide,
                    bone,
                    tungsten,
                    signal);
                BuildOneGoodBatchCutaway(
                    buildingCutawayModeRoot,
                    concrete,
                    iron,
                    oxide,
                    bone,
                    tungsten,
                    signal);
                SelectPumpHallCutaway();
            }

            if (drivingModeRoot != null)
            {
                RoadFeelRig = RoadFeelRigFactory.Create(
                    drivingModeRoot,
                    VehicleView!.transform.position,
                    VehicleView.transform.rotation,
                    new RoadFeelRigMaterials(
                        iron,
                        oxide,
                        bone,
                        darkConcrete,
                        tungsten,
                        signal));
                foreach (GameObject cargo in RoadFeelRig.CargoVisuals)
                {
                    cargo.SetActive(false);
                }
            }

            Transform? canonicalCargoSocket =
                VehicleView?.ScoutVisual?.FindSocket(
                    SashaScoutSemanticContract.CargoSocket01Name);
            if (canonicalCargoSocket == null)
            {
                throw new MissingReferenceException(
                    "Canonical scout cargo socket is required for depot cargo custody.");
            }

            Transform? roadCargoSocket = RoadFeelRig?.ScoutVisual.FindSocket(
                SashaScoutSemanticContract.CargoSocket01Name);
            if (roadCargoSocket == null)
            {
                throw new MissingReferenceException(
                    "Road Feel scout cargo socket is required for depot cargo custody.");
            }

            DepotCargoLoadingView?.BindVehicleCargoSockets(
                canonicalCargoSocket,
                roadCargoSocket);

            Transform? canonicalFrameRailSocket =
                VehicleView?.ScoutVisual?.FindSocket(
                    SashaScoutSemanticContract.CargoSocket02Name);
            if (canonicalFrameRailSocket == null)
            {
                throw new MissingReferenceException(
                    "Canonical scout cargo socket 02 is required for frame-rail salvage custody.");
            }

            Transform? roadFrameRailSocket =
                RoadFeelRig?.ScoutVisual.FindSocket(
                    SashaScoutSemanticContract.CargoSocket02Name);
            if (roadFrameRailSocket == null)
            {
                throw new MissingReferenceException(
                    "Road Feel scout cargo socket 02 is required for frame-rail salvage custody.");
            }

            RouteModulePointView?.BindFrameRailCargoSockets(
                canonicalFrameRailSocket,
                roadFrameRailSocket,
                iron,
                oxide);

            BuildCamera();
            BuildDepotReturnInteraction(
                VehicleView!.transform,
                iron,
                oxide,
                bone,
                tungsten,
                signal);

            Apply(new LastBearingVisualSnapshot(
                LastBearingVisualPhase.Title,
                LastBearingVisualModule.None,
                0f,
                0f,
                0.22f,
                workshopPush: false,
                civicBuffer: false,
                factionClaimed: false,
                turbineRepaired: false,
                auxiliaryPumpInstalled: false,
                humanVisible: true,
                robotVisible: true));
        }

        public void SetCityNeedInspected(bool inspected)
        {
            if (_needInspectionMarker != null)
            {
                _needInspectionMarker.SetActive(inspected);
            }
        }

        public void ApplyCityServiceCell(
            LastBearingReadModel model,
            CityBuildingKind previewBuilding,
            int previewPadIndex,
            int previewQuarterTurns,
            bool previewActive)
        {
            if (CityGrammarComparison?.SelectedHypothesis !=
                LastBearingCityGrammarHypothesis.Unselected)
            {
                CityServiceCellView?.Hide();
                return;
            }

            CityServiceCellView?.Apply(
                model,
                previewBuilding,
                previewPadIndex,
                previewQuarterTurns,
                previewActive);
        }

        public void HideCityServiceCell()
        {
            CityServiceCellView?.Hide();
        }

        public void SetCityServiceCellFocus(bool focused)
        {
            CameraRig?.SetComparisonMode(focused);
        }

        public void ConfigureCityServiceCellInteraction(
            LastBearingGameController controller)
        {
            if (MainCamera == null ||
                CityServiceCellView?.Interactor == null)
            {
                throw new InvalidOperationException(
                    "Working service-cell interaction requires its shared city camera.");
            }

            CityServiceCellView.Interactor.Configure(
                controller,
                MainCamera);
        }

        public void ResetCityServiceCellInteraction()
        {
            CityServiceCellView?.Interactor?.ResetLocalSelection();
        }

        public void ConfigureDepotDecisionInteraction(
            LastBearingGameController controller)
        {
            if (MainCamera == null || DepotDecisionInteractor == null)
            {
                throw new InvalidOperationException(
                    "Depot decision interaction requires the shared camera.");
            }

            DepotDecisionInteractor.Configure(controller, MainCamera);
        }

        public void ResetDepotDecisionInteraction()
        {
            DepotDecisionInteractor?.ResetLocalFocus();
        }

        public void ApplyDepotDecisionInteraction(
            LastBearingReadModel model)
        {
            DepotDecisionInteractor?.Apply(model);
        }

        public void ConfigureDepotReturnInteraction(
            LastBearingGameController controller)
        {
            if (MainCamera == null || DepotReturnInteractor == null)
            {
                throw new InvalidOperationException(
                    "Depot return interaction requires Sasha's shared camera.");
            }

            DepotReturnInteractor.Configure(controller, MainCamera);
        }

        public void ResetDepotReturnInteraction()
        {
            DepotReturnInteractor?.ResetLocalFocus();
        }

        public void ApplyDepotReturnInteraction(LastBearingReadModel model)
        {
            DepotReturnInteractor?.Apply(model);
        }

        public void SelectCityGrammarHypothesis(
            LastBearingCityGrammarHypothesis hypothesis)
        {
            CityGrammarComparison?.SelectHypothesis(hypothesis);
            CameraRig?.SetComparisonMode(
                CityGrammarComparison?.SelectedHypothesis !=
                LastBearingCityGrammarHypothesis.Unselected);
        }

        public bool ManipulateCityGrammarPrimary()
        {
            return CityGrammarComparison?.ManipulatePrimary() ?? false;
        }

        public bool RotateCityGrammarPrimary()
        {
            return CityGrammarComparison?.RotatePrimary() ?? false;
        }

        public bool ToggleCityGrammarTrialPiece()
        {
            return CityGrammarComparison?.ToggleSnapGridPiece() ?? false;
        }

        public bool ConnectCityGrammarLogistics()
        {
            return CityGrammarComparison?.ConnectLogistics() ?? false;
        }

        public bool AdvanceCityGrammarDelivery()
        {
            return CityGrammarComparison?.AdvanceDelivery() ?? false;
        }

        public bool RecordCityGrammarPathRead(
            LastBearingCityTrialPathRead pathRead)
        {
            return CityGrammarComparison?.RecordPathRead(pathRead) ?? false;
        }

        public bool ResetActiveCityGrammarTrial()
        {
            return CityGrammarComparison?.ResetActiveTrial() ?? false;
        }

        public void LeaveCityGrammarComparison()
        {
            CityGrammarComparison?.LeaveComparison();
            CameraRig?.SetComparisonMode(false);
        }

        public void ResetCityGrammarComparison()
        {
            CityGrammarComparison?.ResetComparison();
            CameraRig?.SetComparisonMode(false);
        }

        public void BeginCityGrammarComparisonSession()
        {
            CityGrammarComparison?.BeginSession();
            CameraRig?.SetComparisonMode(false);
        }

        public void ApplyDepotApproachRecovery(
            bool available,
            bool unlocked)
        {
            DepotApproachRecoveryPresentationState state = unlocked
                ? DepotApproachRecoveryPresentationState.Unlocked
                : available
                    ? DepotApproachRecoveryPresentationState.Available
                    : DepotApproachRecoveryPresentationState.Dormant;
            DepotApproachRecoveryView?.ApplyState(state);
        }

        public void ApplyRouteModulePoint(
            bool available,
            RouteActionKind action,
            bool operated)
        {
            RouteModulePointPresentationState state;
            if (operated)
            {
                state = action == RouteActionKind.DeployWinch
                    ? RouteModulePointPresentationState.WinchRecovered
                    : RouteModulePointPresentationState.TankCrossed;
            }
            else if (available)
            {
                state = action == RouteActionKind.DeployWinch
                    ? RouteModulePointPresentationState.WinchAvailable
                    : RouteModulePointPresentationState.TankAvailable;
            }
            else
            {
                state = RouteModulePointPresentationState.Dormant;
            }

            RouteModulePointView?.ApplyState(state);
        }

        public void ApplyRoadCargoPresentation(
            HeavyCargoKind kind,
            HeavyCargoCustody custody)
        {
            if (RoadFeelRig == null)
            {
                return;
            }

            bool rotorInVehicle = kind == HeavyCargoKind.PumpRotor &&
                                  custody == HeavyCargoCustody.Vehicle;
            for (var index = 0; index < RoadFeelRig.CargoVisuals.Count; index++)
            {
                RoadFeelRig.CargoVisuals[index].SetActive(
                    rotorInVehicle && index == 1);
            }
        }

        public void ApplyFrameRailSalvage(
            FrameRailSalvageCustody custody,
            bool recoveryAvailable)
        {
            RouteModulePointView?.ApplyFrameRailSalvage(
                custody,
                recoveryAvailable);
        }

        public void ApplyRepairCargoPresentation(
            RepairCargoKind kind,
            RepairCargoCustody custody,
            TurbineCondition turbineCondition)
        {
            DepotCargoLoadingView?.Apply(kind, custody);
            PumpHallCutawayView?.ApplyTurbineRepair(
                kind,
                custody,
                turbineCondition);
        }

        public void ApplyReturnServicePresentation(
            bool checkInReady,
            RepairCargoKind kind,
            RepairCargoCustody custody,
            bool humanVisible,
            bool robotVisible)
        {
            ReturnServiceView?.Apply(
                checkInReady,
                kind,
                custody,
                humanVisible,
                robotVisible);
        }

        public void ApplyCityImprovement(
            HeavyCargoCustody heavyCargoCustody,
            CityImprovementKind improvement,
            bool humanVisible,
            bool robotVisible)
        {
            bool installed = improvement
                == CityImprovementKind.RefurbishedAuxiliaryPump;
            if (_cityStagedPumpRotor != null)
            {
                _cityStagedPumpRotor.SetActive(
                    !installed
                    && heavyCargoCustody == HeavyCargoCustody.Settlement);
            }

            if (_cityInstalledAuxiliaryPump != null)
            {
                _cityInstalledAuxiliaryPump.SetActive(installed);
            }

            if (_auxiliaryPumpLight != null)
            {
                _auxiliaryPumpLight.intensity = installed ? 680f : 90f;
            }

            PumpHallCutawayView?.Apply(
                heavyCargoCustody,
                improvement,
                humanVisible,
                robotVisible);
        }

        public void ApplyOneGoodBatch(
            bool batchStartAvailable,
            SpareBearingBatchPhase phase,
            SpareBearingLotCustody custody,
            long lotQuantity,
            bool routePermitGranted,
            long futureRouteTollFuelUnits,
            bool humanVisible,
            bool robotVisible,
            bool simulationPaused = false)
        {
            OneGoodBatchCutawayView?.Apply(
                batchStartAvailable,
                phase,
                custody,
                lotQuantity,
                routePermitGranted,
                futureRouteTollFuelUnits,
                humanVisible,
                robotVisible,
                simulationPaused);
            DepotApproachRecoveryView?.ApplyRoutePermit(routePermitGranted);
        }

        public void ApplyGaragePreparationProgress(
            long elapsedTicks,
            long requiredTicks)
        {
            GarageBayView?.ApplyPreparationProgress(
                elapsedTicks,
                requiredTicks);
        }

        public void ApplyGaragePlanIntent(PreparationChoice preparation)
        {
            GaragePlanMarkerPresentation marker = preparation switch
            {
                PreparationChoice.WorkshopPush =>
                    GaragePlanMarkerPresentation.WorkshopPush,
                PreparationChoice.CivicBuffer =>
                    GaragePlanMarkerPresentation.CivicBuffer,
                _ => GaragePlanMarkerPresentation.None,
            };
            GarageBayView?.ApplyPlanMarker(marker);
        }

        public void ApplyRigUpgrade(RigUpgrade upgrade)
        {
            SashaScoutUpgradePresentation presentation =
                upgrade == RigUpgrade.PatchworkSkidPlate
                    ? SashaScoutUpgradePresentation.PatchworkSkidPlate
                    : SashaScoutUpgradePresentation.None;
            VehicleView?.ScoutVisual?.ApplyUpgrade(presentation);
            RoadFeelRig?.ScoutVisual.ApplyUpgrade(presentation);
        }

        public void PulseRigUpgradeInstall()
        {
            GarageBayView?.PulseRigUpgradeInstall();
        }

        public void ResetRigUpgradePresentation()
        {
            ApplyRigUpgrade(RigUpgrade.None);
            GarageBayView?.ResetRigUpgradeInstallPulse();
        }

        public void SelectPumpHallCutaway()
        {
            PumpHallCutawayView?.gameObject.SetActive(true);
            OneGoodBatchCutawayView?.gameObject.SetActive(false);
            SelectedBuildingCutawayCameraAnchor =
                PumpHallCutawayView?.CameraAnchor;
            SelectedBuildingCutawayFocusAnchor =
                PumpHallCutawayView?.FocusAnchor;
        }

        public void SelectOneGoodBatchCutaway()
        {
            PumpHallCutawayView?.gameObject.SetActive(false);
            OneGoodBatchCutawayView?.gameObject.SetActive(true);
            SelectedBuildingCutawayCameraAnchor =
                OneGoodBatchCutawayView?.CameraAnchor;
            SelectedBuildingCutawayFocusAnchor =
                OneGoodBatchCutawayView?.FocusAnchor;
        }

        internal void Apply(LastBearingVisualSnapshot snapshot)
        {
            _snapshot = snapshot;

            if (snapshot.Phase == LastBearingVisualPhase.Title)
            {
                GarageBayView?.ApplyPreparationProgress(0, 0);
                GarageBayView?.ApplyPlanMarker(
                    GaragePlanMarkerPresentation.None);
            }

            if (_waterFill != null)
            {
                var height = Mathf.Lerp(0.12f, 2.8f, snapshot.WaterNormalized);
                _waterFill.localScale = new Vector3(5.6f, height, 5.6f);
                _waterFill.localPosition = new Vector3(
                    _waterFill.localPosition.x,
                    -1.42f + height * 0.5f,
                    _waterFill.localPosition.z);
            }

            SetEmission(
                _stopGlass,
                snapshot.TurbineRepaired ? WaterTeal : StopRed,
                snapshot.TurbineRepaired ? 0.55f : 1.35f);
            SetEmission(
                _waterMaterial,
                WaterTeal,
                snapshot.TurbineRepaired ? 0.28f : 0.08f);
            SetEmission(
                _workshopWindow,
                Tungsten,
                snapshot.WorkshopPush ? 1.5f : 0.18f);
            SetEmission(
                _civicWindow,
                Tungsten,
                snapshot.CivicBuffer || snapshot.TurbineRepaired ? 1.15f : 0.22f);
            SetEmission(
                _claimMaterial,
                SignalCyan,
                snapshot.FactionClaimed ? 1.15f : 0.35f);

            if (_pumpHallLight != null)
            {
                _pumpHallLight.color = snapshot.TurbineRepaired ? Tungsten : StopRed;
                _pumpHallLight.intensity = snapshot.TurbineRepaired ? 720f : 360f;
            }

            if (_workshopLight != null)
            {
                _workshopLight.intensity = snapshot.WorkshopPush ? 560f : 120f;
            }

            if (_canopyLight != null)
            {
                _canopyLight.intensity =
                    snapshot.CivicBuffer || snapshot.TurbineRepaired ? 420f : 140f;
            }

            if (_humanResident != null)
            {
                _humanResident.SetActive(snapshot.HumanVisible);
                _humanResident.transform.localPosition =
                    snapshot.AuxiliaryPumpInstalled
                        ? new Vector3(-2.7f, 0f, 0.1f)
                        : new Vector3(-2.2f, 0f, -7.5f);
            }

            if (_robotResident != null)
            {
                _robotResident.SetActive(snapshot.RobotVisible);
                _robotResident.transform.localPosition =
                    snapshot.AuxiliaryPumpInstalled
                        ? new Vector3(2.7f, 0f, 0.1f)
                        : new Vector3(0.2f, 0f, -7.5f);
            }

            VehicleView?.Apply(snapshot);
            RoadFeelRig?.ScoutVisual.ApplyModule(snapshot.Module);
            GarageBayView?.ApplyModule(snapshot.Module);
            if (_cityInstalledAuxiliaryPump != null)
            {
                _cityInstalledAuxiliaryPump.SetActive(
                    snapshot.AuxiliaryPumpInstalled);
            }
            CameraRig?.SetRoadMode(snapshot.IsRoadMode);
        }

        private void Update()
        {
            if (_snapshot.TurbineRepaired && _turbineRotor != null)
            {
                _turbineRotor.Rotate(Vector3.right, 85f * Time.deltaTime, Space.Self);
            }

            if (_snapshot.AuxiliaryPumpInstalled && _auxiliaryPumpRotor != null)
            {
                _auxiliaryPumpRotor.Rotate(
                    Vector3.forward,
                    125f * Time.deltaTime,
                    Space.Self);
            }

            if (_waterMaterial != null && _snapshot.TurbineRepaired)
            {
                var pulse = 0.22f + Mathf.Sin(Time.time * 1.6f) * 0.025f;
                SetEmission(_waterMaterial, WaterTeal, pulse);
            }
        }

        private void ConfigureEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.29f, 0.255f, 0.215f, 1f);
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.29f, 0.255f, 0.21f, 1f);

            var sun = new GameObject("Bleached Wasteland Sun");
            sun.transform.SetParent(transform, false);
            sun.transform.localRotation = Quaternion.Euler(43f, -31f, 0f);
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.87f, 0.68f, 1f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
        }

        private void BuildGround(
            Transform cityScaffoldRoot,
            Material darkConcrete,
            Material concrete,
            Material oxide)
        {
            CreateBlock(
                "Bleached Basin",
                cityScaffoldRoot,
                new Vector3(2f, -0.55f, 13f),
                new Vector3(68f, 1f, 64f),
                darkConcrete);
            CreateBlock(
                "Inherited Spillway",
                cityScaffoldRoot,
                new Vector3(-4f, -0.02f, 3f),
                new Vector3(25f, 0.38f, 20f),
                concrete);
            CreateBlock(
                "Oxide Service Yard",
                cityScaffoldRoot,
                new Vector3(-10f, 0.2f, -7f),
                new Vector3(18f, 0.22f, 10f),
                oxide);
        }

        private void BuildWaterworks(
            Transform cityScaffoldRoot,
            Material concrete,
            Material darkConcrete,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten)
        {
            var root = new GameObject("Monumental Waterworks").transform;
            root.SetParent(cityScaffoldRoot, false);

            CreateBlock("Pump Hall Left", root, new Vector3(-6f, 3.4f, 2f), new Vector3(3.5f, 7f, 11f), concrete);
            CreateBlock("Pump Hall Right", root, new Vector3(6f, 3.4f, 2f), new Vector3(3.5f, 7f, 11f), concrete);
            CreateBlock("Operatic Lintel", root, new Vector3(0f, 7.3f, 2f), new Vector3(15.5f, 2.2f, 3f), concrete);
            CreateBlock("Deep Machine Stage", root, new Vector3(0f, 0.8f, 4.4f), new Vector3(8f, 1.4f, 5f), darkConcrete);

            var rotor = CreateCylinder("Water Turbine Rotor", root, new Vector3(0f, 2.3f, 3.5f), new Vector3(2.2f, 0.65f, 2.2f), iron);
            rotor.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _turbineRotor = rotor.transform;
            CreateBlock("Slack Drive Belt", rotor.transform, new Vector3(0f, 0f, 0f), new Vector3(0.18f, 4.8f, 2.5f), bone);
            _needInspectionMarker = CreateCylinder(
                "Inspected Failing Water Need",
                root,
                new Vector3(0f, 2.3f, 3.5f),
                new Vector3(2.65f, 0.08f, 2.65f),
                bone);
            _needInspectionMarker.transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f);
            _needInspectionMarker.SetActive(false);

            var gauge = CreateCylinder("Water Gauge", root, new Vector3(3.1f, 2.7f, 0.3f), new Vector3(0.72f, 0.18f, 0.72f), bone);
            gauge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateCylinder("Red Stop Glass", root, new Vector3(-3.2f, 2.8f, 0.25f), new Vector3(0.32f, 0.5f, 0.32f), _stopGlass!);

            var reservoir = CreateCylinder("Civic Reservoir", root, new Vector3(0f, 0.65f, -6.2f), new Vector3(6.3f, 1.55f, 6.3f), concrete);
            reservoir.transform.localScale = new Vector3(6.3f, 1.55f, 6.3f);
            var fill = CreateCylinder("Visible Water Reserve", root, new Vector3(0f, -0.5f, -6.2f), new Vector3(5.6f, 0.4f, 5.6f), _waterMaterial!);
            _waterFill = fill.transform;
            CreateBlock("Painted Safe Line", root, new Vector3(0f, 0.1f, -12.05f), new Vector3(7.2f, 0.18f, 0.12f), bone);

            _pumpHallLight = CreatePointLight("Pump Hall Practical", root, new Vector3(0f, 5.2f, 0f), StopRed, 360f, 13f);

            _cityStagedPumpRotor = new GameObject(
                "Fixed Civic Socket · Staged Pump Rotor");
            _cityStagedPumpRotor.transform.SetParent(root, false);
            _cityStagedPumpRotor.transform.localPosition =
                new Vector3(3.85f, 0f, 4.5f);
            CreateBlock(
                "Rotor Shipping Cradle",
                _cityStagedPumpRotor.transform,
                new Vector3(0f, 0.32f, 0f),
                new Vector3(2.5f, 0.42f, 1.7f),
                oxide);
            Transform stagedRotor = CreateCylinder(
                "Returned Pump Rotor",
                _cityStagedPumpRotor.transform,
                new Vector3(0f, 1.05f, 0f),
                new Vector3(0.76f, 1.05f, 0.76f),
                iron).transform;
            stagedRotor.localRotation = Quaternion.Euler(0f, 0f, 90f);

            _cityInstalledAuxiliaryPump = new GameObject(
                "Fixed Civic Socket · Refurbished Auxiliary Pump");
            _cityInstalledAuxiliaryPump.transform.SetParent(root, false);
            _cityInstalledAuxiliaryPump.transform.localPosition =
                new Vector3(3.85f, 0f, 4.5f);
            CreateBlock(
                "Auxiliary Pump Plinth",
                _cityInstalledAuxiliaryPump.transform,
                new Vector3(0f, 0.48f, 0f),
                new Vector3(3.4f, 0.96f, 2.7f),
                concrete);
            CreateCylinder(
                "Auxiliary Pump Housing",
                _cityInstalledAuxiliaryPump.transform,
                new Vector3(0f, 1.6f, 0f),
                new Vector3(1.2f, 1.35f, 1.2f),
                oxide).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _auxiliaryPumpRotor = CreateCylinder(
                "Installed Auxiliary Pump Rotor",
                _cityInstalledAuxiliaryPump.transform,
                new Vector3(0f, 1.6f, -1.25f),
                new Vector3(0.82f, 0.22f, 0.82f),
                tungsten).transform;
            _auxiliaryPumpRotor.localRotation = Quaternion.Euler(90f, 0f, 0f);
            CreateCylinder(
                "Auxiliary Pump Rising Main",
                _cityInstalledAuxiliaryPump.transform,
                new Vector3(0f, 4.35f, 0f),
                new Vector3(0.42f, 2.25f, 0.42f),
                iron);
            CreateBlock(
                "Auxiliary Pump Water Rill",
                _cityInstalledAuxiliaryPump.transform,
                new Vector3(-2.8f, 0.35f, 0f),
                new Vector3(3.2f, 0.18f, 0.7f),
                _waterMaterial!);
            _auxiliaryPumpLight = CreatePointLight(
                "Auxiliary Pump Tungsten Practical",
                _cityInstalledAuxiliaryPump.transform,
                new Vector3(0f, 4.7f, -0.7f),
                Tungsten,
                680f,
                10f);
            _cityStagedPumpRotor.SetActive(false);
            _cityInstalledAuxiliaryPump.SetActive(false);
        }

        private void BuildSettlement(
            Transform cityScaffoldRoot,
            Material concrete,
            Material iron,
            Material oxide,
            Material bone)
        {
            var root = new GameObject("Last Bearing Settlement").transform;
            root.SetParent(cityScaffoldRoot, false);

            CreateBlock("Recycler Body", root, new Vector3(-11f, 1.3f, -2.5f), new Vector3(5f, 2.6f, 4f), oxide);
            CreateCylinder("Recycler Drum", root, new Vector3(-11f, 2.7f, -2.5f), new Vector3(1.2f, 2.2f, 1.2f), iron).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            CreateBlock("Machine Shop", root, new Vector3(-10f, 1.75f, -10f), new Vector3(7f, 3.5f, 5f), iron);
            CreateBlock("Workshop Window", root, new Vector3(-10f, 2.2f, -7.46f), new Vector3(4.2f, 0.8f, 0.08f), _workshopWindow!);
            CreateBlock("Cantilever Crane", root, new Vector3(-6.8f, 4.2f, -9.5f), new Vector3(0.45f, 6f, 0.45f), oxide);
            CreateBlock("Crane Beam", root, new Vector3(-3.8f, 6.9f, -9.5f), new Vector3(6f, 0.4f, 0.4f), oxide);

            CreateBlock("Emergency Storage", root, new Vector3(8f, 1.2f, -8f), new Vector3(6f, 2.4f, 5f), concrete);
            CreateBlock("Civic Window", root, new Vector3(8f, 1.8f, -5.46f), new Vector3(3.8f, 0.7f, 0.08f), _civicWindow!);

            CreateBlock("Patched Shade", root, new Vector3(-1f, 3.2f, -8f), new Vector3(8f, 0.12f, 5.5f), bone);
            CreateBlock("Shared Worktable", root, new Vector3(-1f, 0.85f, -8f), new Vector3(4.2f, 0.22f, 1.8f), oxide);
            CreateBlock("NO VEHICLES EXCEPT SASHA", root, new Vector3(-1f, 2.3f, -10.82f), new Vector3(5.5f, 0.8f, 0.12f), bone);

            _workshopLight = CreatePointLight("Workshop Tungsten", root, new Vector3(-9f, 4.1f, -7.2f), Tungsten, 120f, 10f);
            _canopyLight = CreatePointLight("Civic Canopy Tungsten", root, new Vector3(-1f, 2.7f, -8f), Tungsten, 140f, 9f);
        }

        private void BuildRoadAndDepot(
            Transform cityScaffoldRoot,
            Material concrete,
            Material darkConcrete,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            var root = new GameObject("Two Path Road Corridor").transform;
            root.SetParent(cityScaffoldRoot, false);

            CreateBlock("Route Apron", root, new Vector3(-6f, -0.01f, 3f), new Vector3(5f, 0.25f, 17f), iron).transform.localRotation = Quaternion.Euler(0f, -24f, 0f);
            CreateBlock("Collapsed Short Branch", root, new Vector3(1f, 0f, 17f), new Vector3(4f, 0.24f, 22f), iron).transform.localRotation = Quaternion.Euler(0f, -18f, 0f);
            CreateBlock("Exposed Long Route A", root, new Vector3(10f, 0f, 11f), new Vector3(4f, 0.24f, 22f), iron).transform.localRotation = Quaternion.Euler(0f, -52f, 0f);
            CreateBlock("Exposed Long Route B", root, new Vector3(15f, 0f, 25f), new Vector3(4f, 0.24f, 18f), iron).transform.localRotation = Quaternion.Euler(0f, 25f, 0f);

            CreateBlock("Pipeline Ruin", root, new Vector3(-8f, 2.4f, 19f), new Vector3(18f, 0.55f, 0.55f), oxide);

            var routeModulePoint = new GameObject(
                LastBearingRouteModulePointView.RootName);
            routeModulePoint.transform.SetParent(root, false);
            RouteModulePointView =
                routeModulePoint.AddComponent<LastBearingRouteModulePointView>();
            RouteModulePointView.Build(
                iron,
                oxide,
                bone,
                tungsten,
                signal);

            var depot = new GameObject("Ruined Transit Depot").transform;
            depot.SetParent(root, false);
            depot.localPosition = new Vector3(9f, 0f, 31f);
            CreateBlock("Depot Proscenium Left", depot, new Vector3(-4.5f, 3f, 0f), new Vector3(2.2f, 6f, 8f), concrete);
            CreateBlock("Depot Proscenium Right", depot, new Vector3(4.5f, 3f, 0f), new Vector3(2.2f, 6f, 8f), concrete);
            CreateBlock("Depot Roof", depot, new Vector3(0f, 6.4f, 0f), new Vector3(11f, 1.1f, 8f), darkConcrete);
            CreateBlock("Ledger Table", depot, new Vector3(0f, 0.9f, -1f), new Vector3(4f, 0.25f, 2f), bone);
            CreateCylinder("Bearing Cradle", depot, new Vector3(0f, 1.35f, 1.4f), new Vector3(0.9f, 0.35f, 0.9f), bone);

            var cargoLoading = new GameObject(
                LastBearingDepotCargoLoadingView.RootName);
            cargoLoading.transform.SetParent(depot, false);
            DepotCargoLoadingView =
                cargoLoading.AddComponent<LastBearingDepotCargoLoadingView>();
            DepotCargoLoadingView.Build(
                iron,
                oxide,
                bone,
                tungsten,
                signal);

            var depotDecision = new GameObject(
                LastBearingDepotDecisionInteractor.RootName);
            depotDecision.transform.SetParent(depot, false);
            DepotDecisionInteractor =
                depotDecision.AddComponent<
                    LastBearingDepotDecisionInteractor>();
            DepotDecisionInteractor.Build(
                iron,
                oxide,
                bone,
                tungsten,
                signal);

            var recovery = new GameObject(
                LastBearingDepotApproachRecoveryView.RootName);
            recovery.transform.SetParent(depot, false);
            recovery.transform.localPosition = new Vector3(0f, 0f, -3.75f);
            DepotApproachRecoveryView =
                recovery.AddComponent<LastBearingDepotApproachRecoveryView>();
            DepotApproachRecoveryView.Build(iron, bone, tungsten, signal);

            for (var index = -2; index <= 2; index++)
            {
                CreateCylinder(
                    "Faction Claim Stake " + (index + 3).ToString("00"),
                    depot,
                    new Vector3(index * 1.65f, 1.1f, -3.2f),
                    new Vector3(0.16f, 1.1f, 0.16f),
                    _claimMaterial!);
            }
        }

        private void BuildResidents(
            Transform cityScaffoldRoot,
            Material humanMaterial,
            Material cloth,
            Material robotMaterial,
            Material token)
        {
            _humanResident = new GameObject(
                "Resident " + ResidentRoster.HumanResidentId);
            _humanResident.transform.SetParent(cityScaffoldRoot, false);
            _humanResident.transform.localPosition = new Vector3(-2.2f, 0f, -7.5f);
            CreateCylinder("Human Workwear", _humanResident.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.45f, 0.75f, 0.45f), humanMaterial);
            CreateCylinder("Human Sun Hood", _humanResident.transform, new Vector3(0f, 1.8f, 0f), new Vector3(0.42f, 0.32f, 0.42f), cloth);

            _robotResident = new GameObject(
                "Resident " + ResidentRoster.RobotResidentId);
            _robotResident.transform.SetParent(cityScaffoldRoot, false);
            _robotResident.transform.localPosition = new Vector3(0.2f, 0f, -7.5f);
            CreateBlock("Utility Robot Torso", _robotResident.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.9f, 1.25f, 0.65f), robotMaterial);
            CreateCylinder("Utility Robot Head", _robotResident.transform, new Vector3(0f, 1.92f, 0f), new Vector3(0.35f, 0.35f, 0.35f), token);
            CreateBlock("Robot Personal Token", _robotResident.transform, new Vector3(0.46f, 1.25f, 0f), new Vector3(0.12f, 0.5f, 0.3f), humanMaterial);
        }

        private void BuildVehicle(
            Material iron,
            Material oxide,
            Material bone,
            Material rubber,
            Material tungsten,
            Material signal)
        {
            var vehicle = new GameObject("Sasha Vehicle");
            vehicle.transform.SetParent(transform, false);
            VehicleView = vehicle.AddComponent<LastBearingVehicleView>();
            VehicleView.Build(
                iron,
                oxide,
                bone,
                rubber,
                tungsten,
                signal);
        }

        private void BuildGarageBay(
            Transform garageModeRoot,
            Material concrete,
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            var garage = new GameObject(LastBearingGarageBayView.RootName);
            garage.transform.SetParent(garageModeRoot, false);
            GarageBayView = garage.AddComponent<LastBearingGarageBayView>();
            GarageBayView.Build(
                VehicleView!.transform.position,
                concrete,
                darkIron,
                oxide,
                bone,
                tungsten,
                signal);
        }

        private void BuildReturnService(
            Transform cityReturnModeRoot,
            Material concrete,
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            var returnService = new GameObject(
                LastBearingReturnServiceView.RootName);
            returnService.transform.SetParent(cityReturnModeRoot, false);
            ReturnServiceView =
                returnService.AddComponent<LastBearingReturnServiceView>();
            ReturnServiceView.Build(
                concrete,
                darkIron,
                oxide,
                bone,
                tungsten,
                signal);
        }

        private void BuildPumpHallCutaway(
            Transform buildingCutawayModeRoot,
            Material concrete,
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            var pumpHall = new GameObject(
                LastBearingPumpHallCutawayView.RootName);
            pumpHall.transform.SetParent(buildingCutawayModeRoot, false);
            PumpHallCutawayView =
                pumpHall.AddComponent<LastBearingPumpHallCutawayView>();
            PumpHallCutawayView.Build(
                LastBearingState.AuxiliaryPumpSocketId,
                concrete,
                darkIron,
                oxide,
                bone,
                tungsten,
                signal,
                _waterMaterial!);
        }

        private void BuildOneGoodBatchCutaway(
            Transform buildingCutawayModeRoot,
            Material concrete,
            Material darkIron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            var workshop = new GameObject(
                LastBearingOneGoodBatchCutawayView.RootName);
            workshop.transform.SetParent(buildingCutawayModeRoot, false);
            OneGoodBatchCutawayView =
                workshop.AddComponent<LastBearingOneGoodBatchCutawayView>();
            OneGoodBatchCutawayView.Build(
                concrete,
                darkIron,
                oxide,
                bone,
                tungsten,
                signal);
        }

        private void BuildCityGrammarComparison(
            Transform cityScaffoldRoot,
            Material concrete,
            Material iron,
            Material oxide,
            Material bone)
        {
            var comparison = new GameObject(
                "D-0030 Reversible City Grammar Comparison");
            comparison.transform.SetParent(cityScaffoldRoot, false);
            CityGrammarComparison =
                comparison.AddComponent<LastBearingCityGrammarComparison>();
            CityGrammarComparison.Build(concrete, iron, oxide, bone);
        }

        private void BuildCityServiceCell(
            Transform cityScaffoldRoot,
            Material concrete,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            var serviceCell = new GameObject(
                "Working Service Cell [Derived Only]");
            serviceCell.transform.SetParent(cityScaffoldRoot, false);
            CityServiceCellView =
                serviceCell.AddComponent<LastBearingCityServiceCellView>();
            CityServiceCellView.Build(
                concrete,
                iron,
                oxide,
                bone,
                tungsten,
                signal);
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("Last Bearing Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            MainCamera = cameraObject.AddComponent<Camera>();
            MainCamera.clearFlags = CameraClearFlags.SolidColor;
            MainCamera.backgroundColor = new Color(0.29f, 0.255f, 0.21f, 1f);
            MainCamera.fieldOfView = LastBearingCameraRig.StrategyFieldOfView;
            MainCamera.nearClipPlane = 0.18f;
            MainCamera.farClipPlane = 400f;
            MainCamera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            RoadChaseCamera = cameraObject.AddComponent<RoadFeelChaseCamera>();
            if (RoadFeelRig != null)
            {
                RoadChaseCamera.Configure(
                    RoadFeelRig.Root.transform,
                    RoadFeelRig.Vehicle.Body);
            }

            RoadChaseCamera.SetChaseActive(false);
            CameraRig = cameraObject.AddComponent<LastBearingCameraRig>();
            CameraRig.Configure(VehicleView!.transform, RoadChaseCamera);
        }

        private void BuildDepotReturnInteraction(
            Transform vehicle,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            var controls = new GameObject(
                LastBearingDepotReturnInteractor.RootName);
            controls.transform.SetParent(vehicle, false);
            DepotReturnInteractor =
                controls.AddComponent<LastBearingDepotReturnInteractor>();
            DepotReturnInteractor.Build(
                iron,
                oxide,
                bone,
                tungsten,
                signal);
        }

        private Material CreateMaterial(Color baseColor, Color emission)
        {
            var material = new Material(ResolveRuntimeMaterialShader());
            material.name = "Last Bearing Runtime Material";
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }
            else
            {
                material.color = baseColor;
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.22f);
            }

            SetEmission(material, emission, 1f);
            _ownedMaterials.Add(material);
            return material;
        }

        private Shader ResolveRuntimeMaterialShader()
        {
            if (_runtimeMaterialShader != null)
            {
                return _runtimeMaterialShader;
            }

            Material? template = Resources.Load<Material>(
                RuntimeMaterialTemplateResourcePath);
            _runtimeMaterialShader = template != null
                ? template.shader
                : Shader.Find("Universal Render Pipeline/Lit");
            _runtimeMaterialShader ??= Shader.Find("Standard");
            if (_runtimeMaterialShader == null)
            {
                throw new InvalidOperationException(
                    "LAST_BEARING_RUNTIME_SHADER_MISSING");
            }

            return _runtimeMaterialShader;
        }

        private static void SetEmission(Material? material, Color color, float intensity)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                return;
            }

            var emission = color * Mathf.Max(0f, intensity);
            material.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0.001f)
            {
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }
        }

        private static GameObject CreateBlock(
            string objectName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material oxide)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = oxide;
            return block;
        }

        private static GameObject CreateCylinder(
            string objectName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = objectName;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localScale = scale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            return cylinder;
        }

        private static Light CreatePointLight(
            string objectName,
            Transform parent,
            Vector3 position,
            Color color,
            float intensity,
            float range)
        {
            var lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        private void OnDestroy()
        {
            foreach (var material in _ownedMaterials)
            {
                if (material != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(material);
                    }
                    else
                    {
                        DestroyImmediate(material);
                    }
                }
            }

            _ownedMaterials.Clear();
        }
    }
}
