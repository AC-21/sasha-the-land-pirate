#nullable enable

using System;
using System.Collections.Generic;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public interface ILastBearingSimulationTickPerformanceObserver
    {
        void RecordSimulationTick(long stopwatchTicks);
    }

    /// <summary>
    /// Thin Unity adapter around the deterministic Last Bearing kernel.
    /// Quantized commands enter the core at 10 Hz; Unity objects only render
    /// the resulting read model.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingGameController : MonoBehaviour
    {
        public const string RuntimeRootName = "WP-0002 Last Bearing Runtime";

        private const float TickSeconds = 0.1f;
        private const int MaximumCatchUpTicks = 8;
        private const string TransactionId = "transaction:last-bearing:unity:0001";
        private const string TransactionFingerprint = "fingerprint:last-bearing:unity:0001";

        private readonly List<LastBearingCommand> _pendingCommands =
            new List<LastBearingCommand>();
        private readonly LastBearingKernel _kernel = new LastBearingKernel();
        private readonly LastBearingStepBuffer _stepBuffer =
            new LastBearingStepBuffer();
        private LastBearingState? _state;
        private LastBearingReadModel? _readModel;
        private LastBearingState? _stateSnapshot;
        private LastBearingReadModel? _readModelSnapshot;
        private LastBearingState? _snapshotSource;
        private long _snapshotGlobalTick = -1L;
        private LastBearingWorldBuilder? _world;
        private LastBearingHud? _hud;
        private LastBearingFieldDesk? _fieldDesk;
        private LastBearingModeCoordinator? _modeCoordinator;
        private LastBearingSaveAdapter? _saveAdapter;
        private ILastBearingSimulationTickPerformanceObserver?
            _simulationTickPerformanceObserver;
        private float _accumulator;
        private bool _initialized;
        private bool _cityNeedInspected;
        private PreparationChoice _garagePreparationIntent =
            PreparationChoice.Unselected;
        private string _status = "Choose who calls Last Bearing home.";
        private string _saveStatus = "No local profile loaded.";

        public bool HasActiveGame => _state != null && _readModel != null;

        public LastBearingState? State
        {
            get
            {
                EnsurePublicSnapshots();
                return _stateSnapshot;
            }
        }

        public LastBearingReadModel? ReadModel
        {
            get
            {
                EnsurePublicSnapshots();
                return _readModelSnapshot;
            }
        }

        internal LastBearingReadModel? RuntimeReadModel => _readModel;

        public LastBearingWorldBuilder? World => _world;

        public LastBearingModeCoordinator? ModeCoordinator => _modeCoordinator;

        public LastBearingFieldDesk? FieldDesk => _fieldDesk;

        public bool IsExactFieldDeskCityOverview =>
            HasActiveGame &&
            _modeCoordinator?.HasActiveMode == true &&
            _modeCoordinator.CurrentMode ==
                LastBearingPresentationMode.CityOverview;

        public bool HasPendingPlayerCommands => _pendingCommands.Count != 0;

        internal int PendingPlayerCommandCountForPerformance =>
            _pendingCommands.Count;

        public bool CanRecoverRoadPresentation =>
            _modeCoordinator?.CanRecoverRoadPresentation ?? false;

        public bool IsReturnCheckInAvailable =>
            _pendingCommands.Count == 0 &&
            _state != null &&
            _state.ReturnPayloadFrozen &&
            _state.TransactionId != null &&
            _state.TransactionFingerprint != null &&
            _readModel != null &&
            _readModel.ExpeditionPhase == ExpeditionPhase.Returned &&
            _readModel.TransactionPhase == TransactionPhase.ReturnPending &&
            _readModel.RepairCargoKind != RepairCargoKind.None &&
            _readModel.RepairCargoCustody == RepairCargoCustody.Vehicle &&
            _modeCoordinator?.HasActiveMode == true &&
            _modeCoordinator.CurrentMode == LastBearingPresentationMode.CityReturn;

        public bool IsTurbineRepairReady =>
            _readModel != null &&
            _readModel.ExpeditionPhase == ExpeditionPhase.AtHome &&
            _readModel.TransactionPhase == TransactionPhase.Finalized &&
            _readModel.TurbineCondition == TurbineCondition.Failing &&
            _readModel.RepairCargoKind != RepairCargoKind.None &&
            _readModel.RepairCargoCustody == RepairCargoCustody.Vehicle;

        public bool IsPumpHallRepairAvailable =>
            _pendingCommands.Count == 0 &&
            IsTurbineRepairReady &&
            _modeCoordinator?.HasActiveMode == true &&
            _modeCoordinator.CurrentMode ==
                LastBearingPresentationMode.BuildingCutaway &&
            _world?.IsPumpHallCutawaySelected == true;

        public bool IsWorkshopBatchStartAvailable =>
            _pendingCommands.Count == 0 &&
            _readModel?.IsSpareBearingBatchStartAvailable == true &&
            _modeCoordinator?.HasActiveMode == true &&
            _modeCoordinator.CurrentMode ==
                LastBearingPresentationMode.BuildingCutaway &&
            _world?.IsOneGoodBatchCutawaySelected == true;

        public bool IsWorkshopBarterAvailable =>
            _pendingCommands.Count == 0 &&
            _readModel?.IsSpareBearingBarterAvailable == true &&
            _modeCoordinator?.HasActiveMode == true &&
            _modeCoordinator.CurrentMode ==
                LastBearingPresentationMode.BuildingCutaway &&
            _world?.IsOneGoodBatchCutawaySelected == true;

        public string Status => _status;

        public string SaveStatus => _saveStatus;

        public bool CityNeedInspected => _cityNeedInspected;

        public PreparationChoice GaragePreparationIntent =>
            _garagePreparationIntent;

        public bool IsGaragePlanIntentActive =>
            _garagePreparationIntent != PreparationChoice.Unselected;

        public bool IsGaragePlanCommitAvailable =>
            IsGaragePlanIntentActive &&
            _pendingCommands.Count == 0 &&
            _readModel != null &&
            _readModel.ExpeditionPhase == ExpeditionPhase.AtHome &&
            _readModel.PreparationChoice == PreparationChoice.Unselected &&
            _modeCoordinator?.HasActiveMode == true &&
            _modeCoordinator.CurrentMode == LastBearingPresentationMode.GarageBay;

        public LastBearingCityGrammarHypothesis CityGrammarHypothesis =>
            _world?.CityGrammarComparison?.SelectedHypothesis ??
            LastBearingCityGrammarHypothesis.Unselected;

        public string CityGrammarEvidence =>
            _world?.CityGrammarComparison?.EvidenceSummary ??
            "comparison-surface-unavailable";

        public bool CityGrammarTrialReady =>
            _world?.CityGrammarComparison?.TrialReady ?? false;

        public bool HasCompletedCityGrammarObservation =>
            _world?.CityGrammarComparison?.HasCompletedObservation ?? false;

        public LastBearingCityTrialPiece ActiveCityGrammarPiece =>
            _world?.CityGrammarComparison?.ActiveSnapGridPiece ??
            LastBearingCityTrialPiece.Recycler;

        public LastBearingCityTrialDeliveryStage CityGrammarDeliveryStage =>
            _world?.CityGrammarComparison?.DeliveryStage ??
            LastBearingCityTrialDeliveryStage.AtRecycler;

        public LastBearingCityTrialPathRead CityGrammarPathRead =>
            _world?.CityGrammarComparison?.PathRead ??
            LastBearingCityTrialPathRead.Unrecorded;

        public bool CityGrammarLogisticsConnected =>
            _world?.CityGrammarComparison?.IsLogisticsConnected ?? false;

        public int CityGrammarInteractionCount =>
            _world?.CityGrammarComparison?.InteractionCount ?? 0;

        public bool CanConnectCityGrammarLogistics =>
            _world?.CityGrammarComparison is
            {
                SelectedHypothesis:
                    LastBearingCityGrammarHypothesis.RestrainedSnapGrid,
                HasValidSnapGridLayout: true,
                IsLogisticsConnected: false
            };

        public string CanonicalHash =>
            _state == null ? "none" : LastBearingCanonicalCodec.ComputeSha256(_state);

        public void AttachSimulationTickPerformanceObserver(
            ILastBearingSimulationTickPerformanceObserver observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            if (_simulationTickPerformanceObserver != null &&
                !ReferenceEquals(_simulationTickPerformanceObserver, observer))
            {
                throw new InvalidOperationException(
                    "a simulation-tick performance observer is already attached");
            }

            EnsurePublicSnapshots();
            _simulationTickPerformanceObserver = observer;
        }

        public void DetachSimulationTickPerformanceObserver(
            ILastBearingSimulationTickPerformanceObserver observer)
        {
            if (ReferenceEquals(_simulationTickPerformanceObserver, observer))
            {
                _simulationTickPerformanceObserver = null;
            }
        }

        internal void SetLegacyHudSuppressedByFieldDesk(bool suppressed)
        {
            bool enabled = !suppressed;
            if (_hud != null && _hud.enabled != enabled)
            {
                _hud.enabled = enabled;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            gameObject.name = RuntimeRootName;

            _modeCoordinator = gameObject.AddComponent<LastBearingModeCoordinator>();
            _modeCoordinator.Initialize();
            _world = gameObject.AddComponent<LastBearingWorldBuilder>();
            _world.Build(
                _modeCoordinator.GetModeRoot(
                    LastBearingPresentationMode.Driving),
                _modeCoordinator.GetModeRoot(
                    LastBearingPresentationMode.BuildingCutaway),
                _modeCoordinator.GetModeRoot(
                    LastBearingPresentationMode.GarageBay),
                _modeCoordinator.GetModeRoot(
                    LastBearingPresentationMode.CityReturn));
            _modeCoordinator.ConfigurePresentationOwners(
                _world.CameraRig!,
                _world.VehicleView!,
                _world.RoadFeelRig!.Root.transform,
                _world.CityScaffoldRoot!,
                _world.GarageBayView!.CameraAnchor!,
                _world.GarageBayView.FocusAnchor!,
                _world.PumpHallCutawayView!.CameraAnchor!,
                _world.PumpHallCutawayView.FocusAnchor!,
                _world.ReturnServiceView!.CameraAnchor!,
                _world.ReturnServiceView.FocusAnchor!);
            _modeCoordinator.AttachRoadModeAdapter(
                _world.RoadFeelRig.Adapter);
            try
            {
                _fieldDesk = gameObject.AddComponent<LastBearingFieldDesk>();
                _fieldDesk.Configure(this);
            }
            catch (Exception exception)
            {
                _fieldDesk = null;
                Debug.LogException(exception, this);
            }

            _hud = gameObject.AddComponent<LastBearingHud>();
            _hud.Configure(this, _fieldDesk);
            SetLegacyHudSuppressedByFieldDesk(
                _fieldDesk?.OwnsCityOverview == true);

            try
            {
                _saveAdapter = LastBearingSaveAdapter.Create();
            }
            catch (Exception exception)
            {
                _saveStatus = "Save boundary unavailable: " + exception.GetType().Name;
                Debug.LogException(exception, this);
            }
        }

        public void StartNewGame(ColonyComposition composition)
        {
            _fieldDesk?.ResetForLifecycle();
            _pendingCommands.Clear();
            ClearGaragePlanIntent();
            _accumulator = 0f;
            _state = LastBearingScenarioFactory.CreateInitial(composition, 2011);
            _readModel = LastBearingReadModel.FromState(_state);
            ResetPublicSnapshotsToRuntime();
            _cityNeedInspected = false;
            _saveStatus = "Unsaved local development state.";
            _world?.SetCityNeedInspected(false);
            _world?.BeginCityGrammarComparisonSession();
            _world?.SelectPumpHallCutaway();
            _modeCoordinator?.ClearSession();

            AssignDefaultLeadResident();
            _status = "Water is falling. Inspect the turbine, then wake the civic machinery.";
            ApplyPresentation();
        }

        public void AssignDefaultLeadResident()
        {
            if (_state == null || _readModel == null)
            {
                return;
            }

            if (_readModel.AssignedResidentId != null)
            {
                _status = "The expedition lead is already assigned.";
                return;
            }

            string assignedResident = _readModel.Composition == ColonyComposition.RobotOnly
                ? ResidentRoster.RobotResidentId
                : ResidentRoster.HumanResidentId;
            Queue(sequence => new AssignResidentCommand(sequence, assignedResident));
            SimulateOneTick();
            _status = string.Equals(
                _readModel?.AssignedResidentId,
                assignedResident,
                StringComparison.Ordinal)
                ? "Expedition lead assigned: " + assignedResident + "."
                : "Lead assignment failed closed; choose the recovery action again.";
        }

        public void ReturnToTitle()
        {
            _pendingCommands.Clear();
            ClearGaragePlanIntent();
            _state = null;
            _readModel = null;
            ResetPublicSnapshotsToRuntime();
            _accumulator = 0f;
            _cityNeedInspected = false;
            _status = "Choose who calls Last Bearing home.";
            _world?.SetCityNeedInspected(false);
            _world?.BeginCityGrammarComparisonSession();
            _world?.SelectPumpHallCutaway();
            _modeCoordinator?.ClearSession();
            _world?.Apply(new LastBearingVisualSnapshot(
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
            _world?.ApplyDepotApproachRecovery(
                available: false,
                unlocked: false);
            _world?.ApplyRouteModulePoint(
                available: false,
                RouteActionKind.None,
                operated: false);
            _world?.ApplyRoadCargoPresentation(
                HeavyCargoKind.PumpRotor,
                HeavyCargoCustody.Depot);
            _world?.ApplyRepairCargoPresentation(
                RepairCargoKind.None,
                RepairCargoCustody.None,
                TurbineCondition.Failing);
            _world?.ApplyReturnServicePresentation(
                checkInReady: false,
                RepairCargoKind.None,
                RepairCargoCustody.None,
                humanVisible: false,
                robotVisible: false);
            _world?.ApplyCityImprovement(
                HeavyCargoCustody.Depot,
                CityImprovementKind.None,
                humanVisible: true,
                robotVisible: true);
            _world?.ApplyOneGoodBatch(
                batchStartAvailable: false,
                SpareBearingBatchPhase.None,
                SpareBearingLotCustody.None,
                lotQuantity: 0,
                routePermitGranted: false,
                futureRouteTollFuelUnits: 0,
                humanVisible: true,
                robotVisible: true);
            _fieldDesk?.ResetForLifecycle();
        }

        public void ActivateInfrastructure()
        {
            if (!RequireCityNeedInspected())
            {
                return;
            }

            if (_readModel?.NextObjective != "activate-slice-infrastructure")
            {
                _status = "The recycler and machine shop are already accounted for.";
                return;
            }

            if (!HasCompletedCityGrammarObservation)
            {
                _status =
                    "Complete either neutral service-cell trial before bringing the same canonical machinery online.";
                return;
            }

            Queue(sequence => new ActivateSliceInfrastructureCommand(sequence));
            _world?.LeaveCityGrammarComparison();
            _status =
                "Recycler and machine shop are coming online. The canonical result records no layout and D-0030 remains open.";
        }

        public void BeginGaragePlan(PreparationChoice preparation)
        {
            if (!RequireCityNeedInspected())
            {
                return;
            }

            if (preparation != PreparationChoice.WorkshopPush &&
                preparation != PreparationChoice.CivicBuffer)
            {
                _status = "Unknown preparation posture ignored.";
                return;
            }

            if (_readModel == null ||
                _readModel.ExpeditionPhase != ExpeditionPhase.AtHome ||
                _readModel.PreparationChoice != PreparationChoice.Unselected ||
                _readModel.NextObjective != "select-preparation-and-module" ||
                _pendingCommands.Count != 0)
            {
                ClearGaragePlanIntent();
                _status =
                    "Rig planning is available only after the service cell is online and Sasha is home.";
                return;
            }

            _world?.LeaveCityGrammarComparison();
            if (_modeCoordinator?.TryShowCityMode(
                    LastBearingPresentationMode.GarageBay,
                    _readModel) != true)
            {
                ClearGaragePlanIntent();
                _status = "The fixed garage cutaway could not be opened safely.";
                return;
            }

            _garagePreparationIntent = preparation;
            _world?.ApplyGaragePlanIntent(_garagePreparationIntent);
            _status = preparation == PreparationChoice.WorkshopPush
                ? "Workshop Push is penciled in. Choose Sasha's rig module in the garage to commit it."
                : "Civic Buffer is penciled in. Choose Sasha's rig module in the garage to commit it.";
        }

        public void CommitGaragePlan(VehicleModule module)
        {
            if (!IsGaragePlanIntentActive)
            {
                _status = "Choose a city preparation posture before committing rig work.";
                return;
            }

            if (module != VehicleModule.WinchAssembly &&
                module != VehicleModule.SealedRangeTank)
            {
                _status = "Unknown Sasha Scout module ignored.";
                return;
            }

            if (_readModel == null ||
                _readModel.ExpeditionPhase != ExpeditionPhase.AtHome ||
                _readModel.PreparationChoice != PreparationChoice.Unselected ||
                _pendingCommands.Count != 0)
            {
                ClearGaragePlanIntent();
                _status = "That garage plan is stale and was cleared without committing work.";
                return;
            }

            if (!IsGaragePlanCommitAvailable)
            {
                _status = "Return to the fixed garage cutaway before committing Sasha's rig.";
                return;
            }

            PreparationChoice preparation = _garagePreparationIntent;
            Queue(
                sequence => new SelectPreparationCommand(
                    sequence,
                    preparation,
                    module),
                sequence => new InstallVehicleModuleCommand(sequence, module));
            ClearGaragePlanIntent();
            _status = preparation + " + " + module +
                " committed at Sasha's rig. Work begins only if both canonical commands are accepted.";
        }

        public void CancelGaragePlan()
        {
            if (!IsGaragePlanIntentActive)
            {
                _status = "No uncommitted garage plan is active.";
                return;
            }

            ClearGaragePlanIntent();
            if (_readModel?.ExpeditionPhase == ExpeditionPhase.AtHome)
            {
                _modeCoordinator?.TryShowCityMode(
                    LastBearingPresentationMode.CityOverview,
                    _readModel);
            }

            _status = "Garage plan canceled. No preparation or module command was queued.";
        }

        /// <summary>
        /// Compatibility seam for existing tests and callers. New player flow
        /// stages preparation first and commits the module from the garage.
        /// </summary>
        public void ChoosePlan(PreparationChoice preparation, VehicleModule module)
        {
            BeginGaragePlan(preparation);
            if (_garagePreparationIntent == preparation &&
                IsGaragePlanCommitAvailable)
            {
                CommitGaragePlan(module);
            }
        }

        public void InspectCityNeed()
        {
            if (_readModel == null)
            {
                return;
            }

            _cityNeedInspected = true;
            _world?.SetCityNeedInspected(true);
            _status =
                "Inspection: the turbine is failing, reserve is falling, and " +
                "the recycler + machine shop are the bounded intervention.";
        }

        public void SelectCityGrammarHypothesis(
            LastBearingCityGrammarHypothesis hypothesis)
        {
            if (hypothesis == LastBearingCityGrammarHypothesis.Unselected)
            {
                LeaveCityGrammarComparison();
                return;
            }

            if (hypothesis !=
                    LastBearingCityGrammarHypothesis.RestrainedSnapGrid &&
                hypothesis != LastBearingCityGrammarHypothesis.DistrictStamp)
            {
                _status = "Unknown city-grammar hypothesis ignored.";
                return;
            }

            if (!RequireCityTrialAtHome())
            {
                return;
            }

            if (_modeCoordinator?.TryShowCityMode(
                    LastBearingPresentationMode.CityOverview,
                    _readModel) != true)
            {
                _status = "The city trial is available only while Sasha is home.";
                return;
            }

            _world?.SelectCityGrammarHypothesis(hypothesis);
            _status =
                hypothesis +
                " is staged for the same empty calibration run. No grammar was selected.";
        }

        public void ManipulateCityGrammarPrimary()
        {
            if (!RequireCityTrialAtHome())
            {
                return;
            }

            _status = _world?.ManipulateCityGrammarPrimary() == true
                ? "Placed or moved the active trial piece; canonical city state is unchanged."
                : "Select one provisional city-grammar hypothesis first.";
        }

        public void RotateCityGrammarPrimary()
        {
            if (!RequireCityTrialAtHome())
            {
                return;
            }

            _status = _world?.RotateCityGrammarPrimary() == true
                ? "Rotated the active comparison prototype; canonical city state is unchanged."
                : "Select one provisional city-grammar hypothesis first.";
        }

        public void ToggleCityGrammarTrialPiece()
        {
            if (!RequireCityTrialAtHome())
            {
                return;
            }

            _status = _world?.ToggleCityGrammarTrialPiece() == true
                ? "Active individual building: " + ActiveCityGrammarPiece + "."
                : "Piece switching belongs to the individual-building trial.";
        }

        public void ConnectCityGrammarLogistics()
        {
            if (!RequireCityTrialAtHome())
            {
                return;
            }

            _status = _world?.ConnectCityGrammarLogistics() == true
                ? "Recycler output connected to workshop input with a local service link."
                : "Place the two individual buildings on different pads before linking them.";
        }

        public void AdvanceCityGrammarDelivery()
        {
            if (!RequireCityTrialAtHome())
            {
                return;
            }

            _status = _world?.AdvanceCityGrammarDelivery() == true
                ? CityGrammarDeliveryStage ==
                  LastBearingCityTrialDeliveryStage.DeliveredToWorkshop
                    ? "The empty calibration sled reached the workshop. Record only whether the path reads clearly."
                    : "The empty calibration sled is crossing the service path."
                : "Finish a valid layout and logistics link before moving the empty sled.";
        }

        public void RecordCityGrammarPathRead(bool clear)
        {
            if (!RequireCityTrialAtHome())
            {
                return;
            }

            LastBearingCityTrialPathRead pathRead = clear
                ? LastBearingCityTrialPathRead.Clear
                : LastBearingCityTrialPathRead.Unclear;
            _status = _world?.RecordCityGrammarPathRead(pathRead) == true
                ? "Observation recorded as " + pathRead +
                  ". It is evidence, not a score or city-grammar selection."
                : "Deliver the empty calibration sled before recording path legibility.";
        }

        public void ResetActiveCityGrammarTrial()
        {
            if (!RequireCityTrialAtHome())
            {
                return;
            }

            _status = _world?.ResetActiveCityGrammarTrial() == true
                ? "The active trial was reset; the other hypothesis is unchanged."
                : "Select a city-grammar hypothesis to reset its trial.";
        }

        public void LeaveCityGrammarComparison()
        {
            _world?.LeaveCityGrammarComparison();
            _status =
                "Left the city trial. Its in-memory comparison remains available until departure, title, new game, or load.";
        }

        public void ResetCityGrammarComparison()
        {
            _world?.ResetCityGrammarComparison();
            _status =
                "City-grammar comparison cleared. D-0030 remains open.";
        }

        public void ShowCityOverview()
        {
            TryShowCityMode(
                LastBearingPresentationMode.CityOverview,
                "City overview restored.");
        }

        public void OpenBuildingCutaway()
        {
            _world?.LeaveCityGrammarComparison();
            ApplySelectedBuildingCutawayPose();
            TryShowCityMode(
                LastBearingPresentationMode.BuildingCutaway,
                "Building cutaway routing scaffold active; the city state is unchanged.");
        }

        public void OpenGarageBay()
        {
            _world?.LeaveCityGrammarComparison();
            TryShowCityMode(
                LastBearingPresentationMode.GarageBay,
                "Sasha Scout service-bay cutaway active; the vehicle state is unchanged.");
        }

        public void AttachRoadModeAdapter(ILastBearingRoadModeAdapter adapter)
        {
            _modeCoordinator?.AttachRoadModeAdapter(adapter);
        }

        public bool RecoverRoadPresentation()
        {
            if (_modeCoordinator?.TryRecoverRoadPresentation() != true)
            {
                return false;
            }

            _status =
                "Presentation rig recovered to Sasha's current canonical road marker.";
            return true;
        }

        public void CommitExpedition()
        {
            Queue(
                sequence => new PrepareExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new DebitCityManifestCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new DepartExpeditionCommand(sequence));
            _status = "Manifest committed. Sasha owns the road consequence now.";
        }

        public void ResolveDepot(bool cooperate)
        {
            Queue(sequence => new ResolveDepotCommand(
                sequence,
                cooperate ? EncounterChoice.Cooperate : EncounterChoice.TakeBearing));
            _status = cooperate
                ? "The bearing stays claimed; a field sleeve and obligation wait at the faction stand."
                : "Sasha declares the take. The bearing stays at its canonical source until you load it.";
        }

        public void LoadDepotRepairCargo()
        {
            if (_readModel == null ||
                !_readModel.IsRepairCargoLoadAvailable)
            {
                _status = "No depot repair cargo is canonically available to load.";
                return;
            }

            RepairCargoKind cargoKind = _readModel.RepairCargoKind;
            Queue(sequence => new LoadDepotRepairCargoCommand(sequence));
            _status = cargoKind == RepairCargoKind.FieldSleeve
                ? "Field sleeve secured at Sasha's cargo socket. The maintenance promise rides home too."
                : "Ceramic bearing secured at Sasha's cargo socket. The depot grievance rides home too.";
        }

        public void OperateDepotApproachRecoveryPoint()
        {
            if (_readModel == null ||
                !_readModel.IsDepotApproachRecoveryAvailable)
            {
                _status =
                    "The depot recovery point is not canonically available yet.";
                return;
            }

            Queue(sequence =>
                new OperateDepotRecoveryPointCommand(sequence));
            _status =
                "Recovery bridle seated. The depot encounter can now begin.";
        }

        public void OperateWreckLineModulePoint()
        {
            if (_readModel == null ||
                !_readModel.IsWreckLineModulePointAvailable)
            {
                _status = "The Wreck Line module point is not canonically available yet.";
                return;
            }

            RouteActionKind action = _readModel.RouteActionKind;
            Queue(sequence => new OperateWreckLineModuleCommand(
                sequence,
                action));
            _status = action == RouteActionKind.DeployWinch
                ? "Winch seated. The existing pump rotor is coming into vehicle custody."
                : "Seals checked. Cross the Wreck Line dust exposure without heavy cargo.";
        }

        public void ChooseLiquidReturn(LiquidCargoKind kind)
        {
            Queue(sequence => new ChooseLiquidReturnCommand(sequence, kind));
            _status = kind == LiquidCargoKind.Water
                ? "The range tank is carrying emergency water."
                : "The range tank is carrying fuel for the next decision.";
        }

        public void BeginReturn()
        {
            if (_readModel == null ||
                _readModel.ExpeditionPhase != ExpeditionPhase.AtDepot)
            {
                _status = "The return payload can only be frozen at the depot.";
                return;
            }

            if (_readModel.RepairCargoCustody != RepairCargoCustody.Vehicle)
            {
                _status = "Load the repair cargo into Sasha's scout before freezing the return payload.";
                return;
            }

            if (_readModel.VehicleModule == VehicleModule.SealedRangeTank &&
                _readModel.LiquidCargoKind == LiquidCargoKind.None)
            {
                _status = "Choose water or fuel before sealing the range tank.";
                return;
            }

            Queue(sequence => new FreezeReturnPayloadCommand(
                sequence,
                TransactionId,
                TransactionFingerprint));
            _status = "Return payload frozen. Nothing can duplicate between road and home.";
        }

        public void CompleteReturn()
        {
            if (!IsReturnCheckInAvailable)
            {
                _status =
                    "The loaded return can only check in from Sasha's fixed home apron.";
                return;
            }

            string transactionId = _state!.TransactionId!;
            string transactionFingerprint = _state.TransactionFingerprint!;
            Queue(
                sequence => new CreditCityReturnCommand(
                    sequence,
                    transactionId,
                    transactionFingerprint),
                sequence => new FinalizeExpeditionTransactionCommand(
                    sequence,
                    transactionId,
                    transactionFingerprint));
            _status = "The road outcome is back in Last Bearing's inventory and memory.";
        }

        public void RepairTurbine()
        {
            if (!IsPumpHallRepairAvailable)
            {
                _status =
                    "Seat the repair only from the selected pump-hall cutaway.";
                return;
            }

            Queue(sequence => new InstallTurbineRepairCommand(sequence));
            _status = "The civic organ is turning again.";
        }

        public void OpenPumpHallRepair()
        {
            if (!TryRouteToPumpHallRepair(
                    "The loaded repair is framed at the pump-hall service line."))
            {
                _status =
                    "The pump-hall repair route is available only after return check-in.";
            }
        }

        public void InstallCityImprovement()
        {
            if (_readModel == null
                || !_readModel.IsCityImprovementInstallationAvailable)
            {
                _status =
                    "The fixed pump-hall civic socket is not canonically ready.";
                return;
            }

            Queue(sequence => new InstallCityImprovementCommand(
                sequence,
                NextCityDecision.RefurbishAuxiliaryPump,
                LastBearingState.AuxiliaryPumpSocketId,
                LastBearingState.AuxiliaryPumpOrientationQuarterTurns));
            _status =
                "The returned rotor is committed to the auxiliary pump hall.";
        }

        public void StartSpareBearingBatch()
        {
            if (!IsWorkshopBatchStartAvailable)
            {
                _status =
                    "Start the one approved spare-bearing batch only from the selected machine-shop cutaway.";
                return;
            }

            Queue(sequence => new StartSpareBearingBatchCommand(sequence));
            _status =
                "One Good Batch start queued. Inputs remain staged until canonical acceptance.";
        }

        public void BarterSpareBearingLot()
        {
            if (!IsWorkshopBarterAvailable)
            {
                _status =
                    "Barter the physical lot only from the selected claims-wicket cutaway.";
                return;
            }

            Queue(sequence => new BarterSpareBearingLotCommand(sequence));
            _status =
                "Claims-wicket handoff queued. The lot remains at workshop output until canonical acceptance.";
        }

        public void OpenOneGoodBatchWorkshop()
        {
            if (!TryRouteToOneGoodBatchWorkshop(
                    "The machine shop and claims wicket are framed for the next physical handoff."))
            {
                _status =
                    "The workshop action line is available only for the active batch or physical-lot handoff.";
            }
        }

        public void ServiceFieldSleeve()
        {
            Queue(sequence => new ServiceFieldSleeveCommand(sequence));
            _status = "Field sleeve serviced. The cooperative obligation remains legible.";
        }

        public void TogglePause()
        {
            if (_readModel == null)
            {
                return;
            }

            if (_readModel.PauseCause == PauseCause.AutoAlert)
            {
                _status = "Resolve the depot encounter before domain clocks resume.";
                return;
            }

            bool pause = _readModel.PauseCause == PauseCause.None;
            Queue(sequence => new SetPauseCommand(sequence, pause));
            _status = pause ? "Simulation paused." : "Simulation resumed.";
        }

        public void TriggerAutoPauseAlert()
        {
            if (_readModel == null ||
                _readModel.ExpeditionPhase != ExpeditionPhase.AtDepot ||
                _readModel.RepairCargoKind != RepairCargoKind.None)
            {
                _status = "No unresolved depot forecast alert is available.";
                return;
            }

            Queue(sequence => new TriggerAutoPauseAlertCommand(sequence));
            _status = "Forecast alert auto-paused the domain clocks.";
        }

        public void Save()
        {
            if (_state == null || _saveAdapter == null)
            {
                _saveStatus = "No active state or save boundary.";
                return;
            }

            if (_pendingCommands.Count != 0 || _state.AssignedResidentId == null)
            {
                _saveStatus =
                    "Save deferred until queued actions and lead assignment are authoritative.";
                return;
            }

            try
            {
                var result = _saveAdapter.TryPersist(_state);
                _saveStatus = result.Code +
                              " · generation " + result.Generation +
                              " · " + CanonicalHash.Substring(0, 12);
            }
            catch (Exception exception)
            {
                _saveStatus = "Save failed closed: " + exception.GetType().Name;
                Debug.LogException(exception, this);
            }
        }

        public void Load()
        {
            _fieldDesk?.ResetForLifecycle();
            ClearGaragePlanIntent();
            if (_saveAdapter == null)
            {
                _saveStatus = "Save boundary unavailable.";
                return;
            }

            try
            {
                LastBearingAdapterLoadResult result = _saveAdapter.TryLoad();
                if (!result.Succeeded || result.State == null)
                {
                    _saveStatus = "Load refused: " + result.Code;
                    return;
                }

                _pendingCommands.Clear();
                _modeCoordinator?.ClearSession();
                _state = result.State;
                _readModel = LastBearingReadModel.FromState(_state);
                ResetPublicSnapshotsToRuntime();
                _accumulator = 0f;
                _cityNeedInspected = true;
                _world?.SetCityNeedInspected(true);
                _world?.BeginCityGrammarComparisonSession();
                _world?.SelectPumpHallCutaway();
                _saveStatus = result.Code + " · " + CanonicalHash.Substring(0, 12);
                _status = "Exact city, vehicle, custody, crisis, and faction state restored.";
                ApplyPresentation();
                if (!TryRouteToPumpHallRepair(
                        "Exact finalized return restored at the pump-hall service line."))
                {
                    TryRouteToOneGoodBatchWorkshop(
                        "Exact workshop batch and physical-lot state restored at One Good Batch.");
                }
            }
            catch (Exception exception)
            {
                _saveStatus = "Load failed closed: " + exception.GetType().Name;
                Debug.LogException(exception, this);
            }
        }

        private void Update()
        {
            if (!HasActiveGame)
            {
                _fieldDesk?.Refresh();
                return;
            }

            HandleGlobalShortcuts();
            AdvanceSimulation(Time.unscaledDeltaTime);
            _fieldDesk?.Refresh();
        }

        private void AdvanceSimulation(float elapsedSeconds)
        {
            _accumulator += Mathf.Min(
                Mathf.Max(0f, elapsedSeconds),
                TickSeconds * MaximumCatchUpTicks);
            int ticks = 0;
            while (_accumulator >= TickSeconds &&
                   ticks < MaximumCatchUpTicks)
            {
                SimulateOneTick();
                _accumulator -= TickSeconds;
                ticks++;
            }
        }

        private void HandleGlobalShortcuts()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
            {
                TogglePause();
            }

            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                Save();
            }

            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                Load();
            }

            if ((keyboard != null && keyboard.rKey.wasPressedThisFrame) ||
                (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame))
            {
                RecoverRoadPresentation();
            }

            if (_readModel != null &&
                _readModel.IsWreckLineModulePointAvailable &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)))
            {
                OperateWreckLineModulePoint();
            }
            else if (_readModel != null &&
                _readModel.IsDepotApproachRecoveryAvailable &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)))
            {
                OperateDepotApproachRecoveryPoint();
            }
            else if (_readModel != null &&
                _readModel.IsRepairCargoLoadAvailable &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)))
            {
                LoadDepotRepairCargo();
            }
            else if (IsReturnCheckInAvailable &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)))
            {
                CompleteReturn();
            }
            else if (IsPumpHallRepairAvailable &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)))
            {
                RepairTurbine();
            }
            else if (IsWorkshopBatchStartAvailable &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)))
            {
                StartSpareBearingBatch();
            }
            else if (IsWorkshopBarterAvailable &&
                ((keyboard != null && keyboard.eKey.wasPressedThisFrame) ||
                 (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)))
            {
                BarterSpareBearingLot();
            }
        }

        private void SimulateOneTick()
        {
            if (_state == null || _readModel == null)
            {
                return;
            }

            ILastBearingSimulationTickPerformanceObserver? observer =
                _simulationTickPerformanceObserver;
            long pausedGuardStartedAt =
                observer == null ? 0L : Stopwatch.GetTimestamp();
            if (_readModel.PauseCause != PauseCause.None &&
                _pendingCommands.Count == 0)
            {
                if (observer != null)
                {
                    observer.RecordSimulationTick(
                        Stopwatch.GetTimestamp() - pausedGuardStartedAt);
                }

                return;
            }

            QueueDriveInputIfApplicable();
            LastBearingCommand[] commands = _pendingCommands.Count == 0
                ? Array.Empty<LastBearingCommand>()
                : _pendingCommands.ToArray();
            bool hasCommands = commands.Length != 0;
            _pendingCommands.Clear();

            try
            {
                ExpeditionPhase previousPhase = _readModel.ExpeditionPhase;
                long simulationStartedAt =
                    observer == null ? 0L : Stopwatch.GetTimestamp();
                LastBearingStepBuffer result = _stepBuffer;
                _kernel.StepInto(_state, commands, result);
                if (observer != null)
                {
                    observer.RecordSimulationTick(
                        Stopwatch.GetTimestamp() - simulationStartedAt);
                }
                if (result.State == null)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_STEP_BUFFER_STATE_UNAVAILABLE");
                }

                if (result.ReadModel == null)
                {
                    throw new InvalidOperationException(
                        "LAST_BEARING_STEP_BUFFER_READ_MODEL_UNAVAILABLE");
                }
                bool returnCheckInAccepted =
                    ContainsEvent(
                        result.DomainEvents,
                        LastBearingEventKind.CityReturnCredited) &&
                    ContainsEvent(
                        result.DomainEvents,
                        LastBearingEventKind.ExpeditionTransactionFinalized);
                bool turbineRepairAccepted = ContainsEvent(
                    result.DomainEvents,
                    LastBearingEventKind.TurbineRepaired);
                bool spareBearingBatchCompleted = ContainsEvent(
                    result.DomainEvents,
                    LastBearingEventKind.SpareBearingBatchCompleted);
                bool spareBearingBatchStarted = ContainsEvent(
                    result.DomainEvents,
                    LastBearingEventKind.SpareBearingBatchStarted);
                bool spareBearingLotBartered = ContainsEvent(
                    result.DomainEvents,
                    LastBearingEventKind.SpareBearingLotBartered);
                _state = result.State;
                _readModel = result.ReadModel;
                if (hasCommands)
                {
                    PublishPublicSnapshots();
                }
                if (previousPhase == ExpeditionPhase.AtHome &&
                    _readModel.ExpeditionPhase != ExpeditionPhase.AtHome)
                {
                    ClearGaragePlanIntent();
                    _world?.BeginCityGrammarComparisonSession();
                }

                TryAutosave(result.DomainEvents);
                ApplyPresentation();
                if (returnCheckInAccepted)
                {
                    TryRouteToPumpHallRepair(
                        "Return checked in. Seat the loaded repair at the pump hall.");
                }

                if (turbineRepairAccepted &&
                    _readModel.IsSpareBearingBatchStartAvailable)
                {
                    _status =
                        "The turbine turns again. One Good Batch is ready when you open the machine shop.";
                }

                if (spareBearingBatchCompleted)
                {
                    TryRouteToOneGoodBatchWorkshop(
                        "The physical lot is complete. Carry it across the workshop to the claims wicket.");
                }

                if (spareBearingBatchStarted)
                {
                    _status =
                        "One Good Batch accepted. Two staged inputs are now on the machine.";
                }

                if (spareBearingLotBartered)
                {
                    _status =
                        "Barter accepted. The physical lot crossed the claims wicket and the route permit is recorded.";
                }
            }
            catch (Exception exception)
            {
                _status = "Command rejected safely: " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void QueueDriveInputIfApplicable()
        {
            if (_pendingCommands.Count != 0 ||
                _readModel == null ||
                _readModel.PauseCause != PauseCause.None ||
                _readModel.IsWreckLineModulePointAvailable ||
                _readModel.IsDepotApproachRecoveryAvailable ||
                (_readModel.ExpeditionPhase != ExpeditionPhase.Outbound &&
                 _readModel.ExpeditionPhase != ExpeditionPhase.Returning))
            {
                return;
            }

            float throttle = 0f;
            float brake = 0f;
            float steering = 0f;
            float handbrake = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    throttle += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    brake = 1f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    steering -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    steering += 1f;
                }

                if (keyboard.spaceKey.isPressed)
                {
                    handbrake = 1f;
                }
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                throttle += gamepad.rightTrigger.ReadValue();
                brake = Mathf.Max(
                    brake,
                    gamepad.leftTrigger.ReadValue());
                steering += gamepad.leftStick.x.ReadValue();
                if (gamepad.leftShoulder.isPressed)
                {
                    handbrake = 1f;
                }
            }

            float clampedSteering = Mathf.Clamp(steering, -1f, 1f);
            int throttleMilli = Mathf.RoundToInt(Mathf.Clamp01(throttle) * 1000f);
            int brakeMilli = Mathf.RoundToInt(Mathf.Clamp01(brake) * 1000f);
            int steeringMilli = Mathf.RoundToInt(clampedSteering * 1000f);
            int handbrakeMilli = Mathf.RoundToInt(
                Mathf.Clamp01(handbrake) * 1000f);
            _modeCoordinator?.ApplyQuantizedRoadCommandShadow(
                throttleMilli,
                steeringMilli);
            _modeCoordinator?.ApplyPresentationOnlyRoadControls(
                brakeMilli,
                handbrakeMilli);
            if (throttleMilli == 0 && steeringMilli == 0)
            {
                return;
            }

            Queue(sequence => new DriveVehicleCommand(
                sequence,
                throttleMilli,
                steeringMilli));
        }

        private void ApplyPresentation()
        {
            if (_readModel == null || _world == null)
            {
                return;
            }

            if (IsGaragePlanIntentActive &&
                (_readModel.ExpeditionPhase != ExpeditionPhase.AtHome ||
                 _readModel.PreparationChoice != PreparationChoice.Unselected))
            {
                ClearGaragePlanIntent();
            }

            bool repaired = _readModel.TurbineCondition != TurbineCondition.Failing;
            LastBearingVisualPhase phase;
            switch (_readModel.ExpeditionPhase)
            {
                case ExpeditionPhase.Outbound:
                    phase = LastBearingVisualPhase.Outbound;
                    break;
                case ExpeditionPhase.AtDepot:
                    phase = LastBearingVisualPhase.Depot;
                    break;
                case ExpeditionPhase.Returning:
                    phase = LastBearingVisualPhase.Returning;
                    break;
                case ExpeditionPhase.Returned:
                    phase = repaired
                        ? LastBearingVisualPhase.Repaired
                        : LastBearingVisualPhase.Returned;
                    break;
                default:
                    if (repaired)
                    {
                        phase = LastBearingVisualPhase.Repaired;
                    }
                    else if (_readModel.PreparationPhase == PreparationPhase.Ready ||
                             _readModel.PreparationPhase == PreparationPhase.Committed)
                    {
                        phase = LastBearingVisualPhase.Ready;
                    }
                    else if (_readModel.PreparationChoice != PreparationChoice.Unselected)
                    {
                        phase = LastBearingVisualPhase.Preparing;
                    }
                    else
                    {
                        phase = LastBearingVisualPhase.FailingCity;
                    }

                    break;
            }

            LastBearingVisualModule module = _readModel.VehicleModule switch
            {
                VehicleModule.WinchAssembly => LastBearingVisualModule.WinchAssembly,
                VehicleModule.SealedRangeTank => LastBearingVisualModule.SealedRangeTank,
                _ => LastBearingVisualModule.None
            };
            if (module == LastBearingVisualModule.None)
            {
                module = _readModel.PlannedModule switch
                {
                    VehicleModule.WinchAssembly => LastBearingVisualModule.WinchAssembly,
                    VehicleModule.SealedRangeTank => LastBearingVisualModule.SealedRangeTank,
                    _ => LastBearingVisualModule.None
                };
            }

            float routeProgress = _readModel.RouteTargetTicks <= 0
                ? 0f
                : Mathf.Clamp01(
                    (float)_readModel.RouteProgressTicks /
                    _readModel.RouteTargetTicks);
            float vehicleLateralNormalized = Mathf.Clamp(
                (float)_readModel.VehicleLateralMilli /
                LastBearingBalanceV1.RoadLateralLimitMilli,
                -1f,
                1f);
            float waterNormalized = Mathf.Clamp01(_readModel.WaterMilli / 180000f);
            bool humanVisible = _readModel.Composition != ColonyComposition.RobotOnly;
            bool robotVisible = _readModel.Composition != ColonyComposition.HumanOnly;
            bool factionClaimed =
                _readModel.FactionClaimState == FactionClaimState.Claimed ||
                _readModel.DepotControl == DepotControl.FactionClaimed;

            _world.Apply(new LastBearingVisualSnapshot(
                phase,
                module,
                routeProgress,
                vehicleLateralNormalized,
                waterNormalized,
                _readModel.PreparationChoice == PreparationChoice.WorkshopPush,
                _readModel.PreparationChoice == PreparationChoice.CivicBuffer,
                factionClaimed,
                repaired,
                _readModel.InstalledCityImprovement
                    == CityImprovementKind.RefurbishedAuxiliaryPump,
                humanVisible,
                robotVisible));
            _world.ApplyDepotApproachRecovery(
                _readModel.IsDepotApproachRecoveryAvailable,
                _readModel.ExpeditionPhase == ExpeditionPhase.AtDepot);
            _world.ApplyRouteModulePoint(
                _readModel.IsWreckLineModulePointAvailable,
                _readModel.RouteActionKind,
                _readModel.RouteActionUsed);
            _world.ApplyRoadCargoPresentation(
                _readModel.HeavyCargoKind,
                _readModel.HeavyCargoCustody);
            _world.ApplyRepairCargoPresentation(
                _readModel.RepairCargoKind,
                _readModel.RepairCargoCustody,
                _readModel.TurbineCondition);
            _world.ApplyCityImprovement(
                _readModel.HeavyCargoCustody,
                _readModel.InstalledCityImprovement,
                humanVisible,
                robotVisible);
            _world.ApplyOneGoodBatch(
                _readModel.IsSpareBearingBatchStartAvailable,
                _readModel.SpareBearingBatchPhase,
                _readModel.SpareBearingLotCustody,
                _readModel.SpareBearingLotQuantity,
                _readModel.RoutePermitGranted,
                _readModel.FutureRouteTollFuelUnits,
                humanVisible,
                robotVisible,
                simulationPaused: _readModel.PauseCause != PauseCause.None);
            _world.ApplyGaragePreparationProgress(
                _readModel.PreparationElapsedTicks,
                _readModel.PreparationRequiredTicks);
            _world.ApplyGaragePlanIntent(
                IsGaragePlanIntentActive
                    ? _garagePreparationIntent
                    : _readModel.PreparationChoice);
            _modeCoordinator?.ApplyCanonical(_readModel);
            _world.ApplyReturnServicePresentation(
                IsReturnCheckInAvailable,
                _readModel.RepairCargoKind,
                _readModel.RepairCargoCustody,
                humanVisible,
                robotVisible);
            _fieldDesk?.Refresh();
        }

        private void TryAutosave(
            IReadOnlyList<LastBearingDomainEvent> domainEvents)
        {
            for (var index = 0; index < domainEvents.Count; index++)
            {
                LastBearingEventKind kind = domainEvents[index].Kind;
                if (kind == LastBearingEventKind.ExpeditionDeparted
                    || kind == LastBearingEventKind.RouteActionUsed
                    || kind == LastBearingEventKind.DepotRecoveryPointOperated
                    || kind == LastBearingEventKind.DepotResolved
                    || kind == LastBearingEventKind.RepairCargoTransferred
                    || kind == LastBearingEventKind.ReturnPayloadFrozen
                    || kind == LastBearingEventKind.VehicleReturned
                    || kind == LastBearingEventKind.CityReturnCredited
                    || kind == LastBearingEventKind.TurbineRepaired
                    || kind == LastBearingEventKind.CityImprovementInstalled
                    || kind == LastBearingEventKind.SpareBearingBatchStarted
                    || kind == LastBearingEventKind.SpareBearingBatchCheckpointReached
                    || kind == LastBearingEventKind.SpareBearingBatchCompleted
                    || kind == LastBearingEventKind.SpareBearingLotCreated
                    || kind == LastBearingEventKind.SpareBearingLotBartered
                    || kind == LastBearingEventKind.RoutePermitGranted)
                {
                    Save();
                    return;
                }
            }
        }

        private void ApplySelectedBuildingCutawayPose()
        {
            Transform? cameraAnchor =
                _world?.SelectedBuildingCutawayCameraAnchor;
            Transform? focusAnchor =
                _world?.SelectedBuildingCutawayFocusAnchor;
            if (cameraAnchor != null && focusAnchor != null)
            {
                _modeCoordinator?.SetBuildingCutawayInspectionPose(
                    cameraAnchor,
                    focusAnchor);
            }
        }

        private bool TryRouteToPumpHallRepair(string successStatus)
        {
            if (!IsTurbineRepairReady ||
                _world == null ||
                _modeCoordinator == null)
            {
                return false;
            }

            _world.SelectPumpHallCutaway();
            ApplySelectedBuildingCutawayPose();
            if (!_modeCoordinator.TryShowCityMode(
                    LastBearingPresentationMode.BuildingCutaway,
                    _readModel))
            {
                return false;
            }

            _status = successStatus;
            return true;
        }

        private bool TryRouteToOneGoodBatchWorkshop(string successStatus)
        {
            if (!IsOneGoodBatchWorkshopRelevant() ||
                _world == null ||
                _modeCoordinator == null)
            {
                return false;
            }

            _world.SelectOneGoodBatchCutaway();
            ApplySelectedBuildingCutawayPose();
            if (!_modeCoordinator.TryShowCityMode(
                    LastBearingPresentationMode.BuildingCutaway,
                    _readModel))
            {
                return false;
            }

            _status = successStatus;
            return true;
        }

        private bool IsOneGoodBatchWorkshopRelevant()
        {
            return _pendingCommands.Count == 0 &&
                   _readModel != null &&
                   (_readModel.IsSpareBearingBatchStartAvailable ||
                    _readModel.SpareBearingBatchPhase ==
                        SpareBearingBatchPhase.InProgress ||
                    _readModel.IsSpareBearingBarterAvailable ||
                    _readModel.SpareBearingBatchPhase ==
                        SpareBearingBatchPhase.Settled);
        }

        private static bool ContainsEvent(
            IReadOnlyList<LastBearingDomainEvent> domainEvents,
            LastBearingEventKind eventKind)
        {
            for (var index = 0; index < domainEvents.Count; index++)
            {
                if (domainEvents[index].Kind == eventKind)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsurePublicSnapshots()
        {
            if (_state == null)
            {
                if (_stateSnapshot == null && _readModelSnapshot == null)
                {
                    return;
                }

                ResetPublicSnapshotsToRuntime();
                return;
            }

            if (_stateSnapshot != null &&
                _readModelSnapshot != null &&
                ReferenceEquals(_snapshotSource, _state) &&
                _snapshotGlobalTick == _state.GlobalTick)
            {
                return;
            }

            PublishPublicSnapshots();
        }

        private void PublishPublicSnapshots()
        {
            if (_state == null || _readModel == null)
            {
                ResetPublicSnapshotsToRuntime();
                return;
            }

            LastBearingState snapshot =
                new LastBearingStateBuilder(_state).Build();
            _stateSnapshot = snapshot;
            _readModelSnapshot = LastBearingReadModel.FromState(snapshot);
            _snapshotSource = _state;
            _snapshotGlobalTick = _state.GlobalTick;
        }

        private void ResetPublicSnapshotsToRuntime()
        {
            _stateSnapshot = _state;
            _readModelSnapshot = _readModel;
            _snapshotSource = _state;
            _snapshotGlobalTick = _state?.GlobalTick ?? -1L;
        }

        private void Queue(params Func<long, LastBearingCommand>[] factories)
        {
            if (_state == null)
            {
                return;
            }

            foreach (var factory in factories)
            {
                long sequence = checked(
                    _state.NextCommandSequence + _pendingCommands.Count);
                _pendingCommands.Add(factory(sequence));
            }
        }

        private void ClearGaragePlanIntent()
        {
            _garagePreparationIntent = PreparationChoice.Unselected;
            _world?.ApplyGaragePlanIntent(
                _readModel?.PreparationChoice ?? PreparationChoice.Unselected);
        }

        private bool RequireCityNeedInspected()
        {
            if (_cityNeedInspected)
            {
                return true;
            }

            _status = "Inspect the failing water system before committing city action.";
            return false;
        }

        private bool RequireCityTrialAtHome()
        {
            if (!RequireCityNeedInspected())
            {
                return false;
            }

            if (_readModel?.ExpeditionPhase == ExpeditionPhase.AtHome)
            {
                return true;
            }

            _status = "The reversible city trial is available only while Sasha is home.";
            return false;
        }

        private void TryShowCityMode(
            LastBearingPresentationMode mode,
            string successStatus)
        {
            if (_modeCoordinator?.TryShowCityMode(mode, _readModel) == true)
            {
                _status = successStatus;
                _fieldDesk?.Refresh(force: true);
                return;
            }

            _status = "City inspection views are available only while Sasha is home.";
        }
    }
}
