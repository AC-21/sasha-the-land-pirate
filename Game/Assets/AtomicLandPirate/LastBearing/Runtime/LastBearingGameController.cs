#nullable enable

using System;
using System.Collections.Generic;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing
{
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
        private LastBearingState? _state;
        private LastBearingReadModel? _readModel;
        private LastBearingWorldBuilder? _world;
        private LastBearingHud? _hud;
        private LastBearingModeCoordinator? _modeCoordinator;
        private LastBearingSaveAdapter? _saveAdapter;
        private float _accumulator;
        private bool _initialized;
        private bool _cityNeedInspected;
        private string _status = "Choose who calls Last Bearing home.";
        private string _saveStatus = "No local profile loaded.";

        public bool HasActiveGame => _state != null && _readModel != null;

        public LastBearingState? State => _state;

        public LastBearingReadModel? ReadModel => _readModel;

        public LastBearingWorldBuilder? World => _world;

        public LastBearingModeCoordinator? ModeCoordinator => _modeCoordinator;

        public string Status => _status;

        public string SaveStatus => _saveStatus;

        public bool CityNeedInspected => _cityNeedInspected;

        public LastBearingCityGrammarHypothesis CityGrammarHypothesis =>
            _world?.CityGrammarComparison?.SelectedHypothesis ??
            LastBearingCityGrammarHypothesis.Unselected;

        public string CityGrammarEvidence =>
            _world?.CityGrammarComparison?.EvidenceSummary ??
            "comparison-surface-unavailable";

        public string CanonicalHash =>
            _state == null ? "none" : LastBearingCanonicalCodec.ComputeSha256(_state);

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
            _world.Build(_modeCoordinator.GetModeRoot(
                LastBearingPresentationMode.Driving));
            _modeCoordinator.ConfigurePresentationOwners(
                _world.CameraRig!,
                _world.VehicleView!,
                _world.RoadFeelRig!.Root.transform);
            _modeCoordinator.AttachRoadModeAdapter(
                _world.RoadFeelRig.Adapter);
            _hud = gameObject.AddComponent<LastBearingHud>();
            _hud.Configure(this);

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
            _pendingCommands.Clear();
            _accumulator = 0f;
            _state = LastBearingScenarioFactory.CreateInitial(composition, 2011);
            _readModel = LastBearingReadModel.FromState(_state);
            _cityNeedInspected = false;
            _saveStatus = "Unsaved local development state.";
            _world?.SetCityNeedInspected(false);
            _world?.BeginCityGrammarComparisonSession();
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
            _state = null;
            _readModel = null;
            _accumulator = 0f;
            _cityNeedInspected = false;
            _status = "Choose who calls Last Bearing home.";
            _world?.SetCityNeedInspected(false);
            _world?.BeginCityGrammarComparisonSession();
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
                humanVisible: true,
                robotVisible: true));
        }

        public void ActivateInfrastructure()
        {
            if (!RequireCityNeedInspected())
            {
                return;
            }

            Queue(sequence => new ActivateSliceInfrastructureCommand(sequence));
            _status = "Recycler and machine shop are coming online.";
        }

        public void ChoosePlan(PreparationChoice preparation, VehicleModule module)
        {
            if (!RequireCityNeedInspected())
            {
                return;
            }

            Queue(
                sequence => new SelectPreparationCommand(
                    sequence,
                    preparation,
                    module),
                sequence => new InstallVehicleModuleCommand(sequence, module));
            _status = preparation == PreparationChoice.WorkshopPush
                ? "Workshop Push: leave sooner and ask home to run lean."
                : "Civic Buffer: protect home and risk a later claim.";
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
            if (!RequireCityNeedInspected())
            {
                return;
            }

            _world?.SelectCityGrammarHypothesis(hypothesis);
            _status =
                hypothesis +
                " is visible only as a reversible D-0030 comparison; no grammar was selected.";
        }

        public void ManipulateCityGrammarPrimary()
        {
            if (!RequireCityNeedInspected())
            {
                return;
            }

            _status = _world?.ManipulateCityGrammarPrimary() == true
                ? "Moved the active comparison prototype; canonical city state is unchanged."
                : "Select one provisional city-grammar hypothesis first.";
        }

        public void RotateCityGrammarPrimary()
        {
            if (!RequireCityNeedInspected())
            {
                return;
            }

            _status = _world?.RotateCityGrammarPrimary() == true
                ? "Rotated the active comparison prototype; canonical city state is unchanged."
                : "Select one provisional city-grammar hypothesis first.";
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
            TryShowCityMode(
                LastBearingPresentationMode.BuildingCutaway,
                "Building cutaway routing scaffold active; the city state is unchanged.");
        }

        public void OpenGarageBay()
        {
            TryShowCityMode(
                LastBearingPresentationMode.GarageBay,
                "Garage bay routing scaffold active; the vehicle state is unchanged.");
        }

        public void AttachRoadModeAdapter(ILastBearingRoadModeAdapter adapter)
        {
            _modeCoordinator?.AttachRoadModeAdapter(adapter);
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
                ? "The bearing stays claimed; a field sleeve and obligation come home."
                : "Sasha takes the claimed bearing. The faction will remember the custody change.";
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
            Queue(
                sequence => new CreditCityReturnCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint),
                sequence => new FinalizeExpeditionTransactionCommand(
                    sequence,
                    TransactionId,
                    TransactionFingerprint));
            _status = "The road outcome is back in Last Bearing's inventory and memory.";
        }

        public void RepairTurbine()
        {
            Queue(sequence => new InstallTurbineRepairCommand(sequence));
            _status = "The civic organ is turning again.";
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
                _accumulator = 0f;
                _cityNeedInspected = true;
                _world?.SetCityNeedInspected(true);
                _world?.BeginCityGrammarComparisonSession();
                _saveStatus = result.Code + " · " + CanonicalHash.Substring(0, 12);
                _status = "Exact city, vehicle, custody, crisis, and faction state restored.";
                ApplyPresentation();
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
                return;
            }

            HandleGlobalShortcuts();
            _accumulator += Mathf.Min(Time.unscaledDeltaTime, TickSeconds * MaximumCatchUpTicks);
            int ticks = 0;
            while (_accumulator >= TickSeconds && ticks < MaximumCatchUpTicks)
            {
                SimulateOneTick();
                _accumulator -= TickSeconds;
                ticks++;
            }
        }

        private void HandleGlobalShortcuts()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.pKey.wasPressedThisFrame)
            {
                TogglePause();
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                Save();
            }

            if (keyboard.f9Key.wasPressedThisFrame)
            {
                Load();
            }
        }

        private void SimulateOneTick()
        {
            if (_state == null || _readModel == null)
            {
                return;
            }

            QueueDriveInputIfApplicable();
            var commands = _pendingCommands.ToArray();
            _pendingCommands.Clear();

            try
            {
                LastBearingTickResult result = _kernel.Step(_state, commands);
                _state = result.State;
                _readModel = result.ReadModel;
                ApplyPresentation();
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
                (_readModel.ExpeditionPhase != ExpeditionPhase.Outbound &&
                 _readModel.ExpeditionPhase != ExpeditionPhase.Returning))
            {
                return;
            }

            float throttle = 0f;
            float steering = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    throttle += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    throttle = 0f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    steering -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    steering += 1f;
                }
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                throttle += gamepad.rightTrigger.ReadValue();
                throttle = Mathf.Max(
                    0f,
                    throttle - gamepad.leftTrigger.ReadValue());
                steering += gamepad.leftStick.x.ReadValue();
            }

            float clampedSteering = Mathf.Clamp(steering, -1f, 1f);
            int throttleMilli = Mathf.RoundToInt(Mathf.Clamp01(throttle) * 1000f);
            int steeringMilli = Mathf.RoundToInt(clampedSteering * 1000f);
            _modeCoordinator?.ApplyQuantizedRoadCommandShadow(
                throttleMilli,
                steeringMilli);
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
                humanVisible,
                robotVisible));
            _modeCoordinator?.ApplyCanonical(_readModel);
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

        private bool RequireCityNeedInspected()
        {
            if (_cityNeedInspected)
            {
                return true;
            }

            _status = "Inspect the failing water system before committing city action.";
            return false;
        }

        private void TryShowCityMode(
            LastBearingPresentationMode mode,
            string successStatus)
        {
            if (_modeCoordinator?.TryShowCityMode(mode, _readModel) == true)
            {
                _status = successStatus;
                return;
            }

            _status = "City inspection views are available only while Sasha is home.";
        }
    }
}
