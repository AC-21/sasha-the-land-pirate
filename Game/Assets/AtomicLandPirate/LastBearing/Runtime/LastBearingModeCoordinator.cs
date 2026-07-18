#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum LastBearingPresentationMode
    {
        CityOverview = 0,
        BuildingCutaway = 1,
        GarageBay = 2,
        Driving = 3,
        DepotEncounter = 4,
        CityReturn = 5,
    }

    public enum LastBearingRoadDamageBand
    {
        Healthy = 0,
        Worn = 1,
        Critical = 2,
    }

    /// <summary>
    /// Narrow input-only bridge for a road presentation. Physics telemetry and
    /// object state deliberately have no route back through this interface.
    /// Service brake, reverse arming, handbrake, cargo mass, and the derived
    /// damage band are explicitly local presentation inputs; none can author
    /// canonical progress.
    /// </summary>
    public interface ILastBearingRoadModeAdapter
    {
        bool IsRoadModeActive { get; }

        void SetRoadModeActive(bool active);

        void ApplyQuantizedCommandShadow(int throttleMilli, int steeringMilli);

        void ApplyPresentationOnlyControls(
            int brakeMilli,
            int handbrakeMilli);

        void ApplyDerivedPresentationLoad(
            int cargoMassKilograms,
            LastBearingRoadDamageBand damageBand);

        void SynchronizePresentationPose(
            Vector3 position,
            Quaternion rotation);

        void ResetPresentation();
    }

    /// <summary>
    /// Owns the one-scene presentation route. Expedition modes are derived
    /// from the canonical read model; cutaway and garage are reversible local
    /// views available only while the canonical expedition is at home.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingModeCoordinator : MonoBehaviour
    {
        public const string ModeRootName = "Presentation Modes [Derived Only]";

        private static readonly LastBearingPresentationMode[] OrderedModes =
        {
            LastBearingPresentationMode.CityOverview,
            LastBearingPresentationMode.BuildingCutaway,
            LastBearingPresentationMode.GarageBay,
            LastBearingPresentationMode.Driving,
            LastBearingPresentationMode.DepotEncounter,
            LastBearingPresentationMode.CityReturn,
        };

        private readonly GameObject?[] _modeRoots =
            new GameObject?[OrderedModes.Length];
        private LastBearingPresentationMode _requestedCityMode =
            LastBearingPresentationMode.CityOverview;
        private ILastBearingRoadModeAdapter? _roadAdapter;
        private LastBearingCameraRig? _cameraRig;
        private LastBearingVehicleView? _canonicalVehicle;
        private Transform? _roadTarget;
        private Transform? _garageCameraAnchor;
        private Transform? _garageFocusAnchor;
        private Transform? _modeRoot;
        private bool _roadRunRequested;
        private bool _roadRecoveryHoldRequested;
        private bool _roadModulePointHoldRequested;
        private bool _roadPresentationActive;
        private bool _initialized;

        public bool HasActiveMode { get; private set; }

        public LastBearingPresentationMode CurrentMode { get; private set; } =
            LastBearingPresentationMode.CityOverview;

        public bool HasRoadAdapter => _roadAdapter != null;

        public bool RoadAdapterFaulted { get; private set; }

        public bool IsRoadPresentationActive => _roadPresentationActive;

        public bool IsRoadPresentationHeldAtRecovery { get; private set; }

        public bool IsRoadPresentationHeldAtModulePoint { get; private set; }

        public int ActiveModeCount
        {
            get
            {
                var count = 0;
                foreach (GameObject? root in _modeRoots)
                {
                    if (root != null && root.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _modeRoot = new GameObject(ModeRootName).transform;
            _modeRoot.SetParent(transform, false);
            for (var index = 0; index < OrderedModes.Length; index++)
            {
                var root = new GameObject(OrderedModes[index].ToString());
                root.transform.SetParent(_modeRoot, false);
                root.SetActive(false);
                _modeRoots[index] = root;
            }
        }

        public Transform GetModeRoot(LastBearingPresentationMode mode)
        {
            Initialize();
            for (var index = 0; index < OrderedModes.Length; index++)
            {
                if (OrderedModes[index] == mode)
                {
                    return _modeRoots[index]!.transform;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        public void ConfigurePresentationOwners(
            LastBearingCameraRig cameraRig,
            LastBearingVehicleView canonicalVehicle,
            Transform roadTarget,
            Transform garageCameraAnchor,
            Transform garageFocusAnchor)
        {
            _cameraRig = cameraRig ?? throw new ArgumentNullException(nameof(cameraRig));
            _canonicalVehicle = canonicalVehicle ??
                                throw new ArgumentNullException(nameof(canonicalVehicle));
            _roadTarget = roadTarget ?? throw new ArgumentNullException(nameof(roadTarget));
            _garageCameraAnchor = garageCameraAnchor ??
                                  throw new ArgumentNullException(nameof(garageCameraAnchor));
            _garageFocusAnchor = garageFocusAnchor ??
                                 throw new ArgumentNullException(nameof(garageFocusAnchor));
            ApplyPresentationOwnership();
        }

        public void AttachRoadModeAdapter(ILastBearingRoadModeAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            if (_roadAdapter != null && !ReferenceEquals(_roadAdapter, adapter))
            {
                SuspendRoadAdapter("replace-adapter");
            }

            _roadAdapter = adapter;
            RoadAdapterFaulted = false;
            _roadPresentationActive = false;
            if (_roadRecoveryHoldRequested || _roadModulePointHoldRequested)
            {
                HoldRoadAdapterAtCanonicalRecoveryPose();
            }
            else if (_roadRunRequested)
            {
                ActivateRoadAdapter();
            }
            else
            {
                SuspendRoadAdapter("attach-inactive");
            }

            ApplyPresentationOwnership();
        }

        public bool TryShowCityMode(
            LastBearingPresentationMode mode,
            LastBearingReadModel? readModel)
        {
            if (!IsCityMode(mode) ||
                readModel == null ||
                readModel.ExpeditionPhase != ExpeditionPhase.AtHome)
            {
                return false;
            }

            _requestedCityMode = mode;
            ApplyCanonical(readModel);
            return true;
        }

        public void ResetForSession(LastBearingReadModel? readModel)
        {
            _requestedCityMode = LastBearingPresentationMode.CityOverview;
            ApplyCanonical(readModel);
        }

        public void ClearSession()
        {
            _requestedCityMode = LastBearingPresentationMode.CityOverview;
            HasActiveMode = false;
            foreach (GameObject? root in _modeRoots)
            {
                root?.SetActive(false);
            }

            _roadRunRequested = false;
            _roadRecoveryHoldRequested = false;
            _roadModulePointHoldRequested = false;
            SuspendRoadAdapter("clear-session");
            ApplyPresentationOwnership();
        }

        public void ApplyCanonical(LastBearingReadModel? readModel)
        {
            Initialize();
            if (readModel == null)
            {
                ClearSession();
                return;
            }

            LastBearingPresentationMode mode = ResolveMode(
                readModel,
                _requestedCityMode);
            bool holdAtRecovery =
                mode == LastBearingPresentationMode.Driving &&
                readModel.IsDepotApproachRecoveryAvailable;
            bool holdAtModulePoint =
                mode == LastBearingPresentationMode.Driving &&
                readModel.IsWreckLineModulePointAvailable;
            int presentationCargoMass =
                readModel.HeavyCargoKind == HeavyCargoKind.PumpRotor &&
                readModel.HeavyCargoCustody == HeavyCargoCustody.Vehicle
                    ? 1300
                    : 0;
            LastBearingRoadDamageBand presentationDamageBand =
                DerivePresentationDamageBand(readModel.VehicleConditionMilli);
            TryInvokeRoadAdapter(
                "apply-derived-load",
                adapter => adapter.ApplyDerivedPresentationLoad(
                    presentationCargoMass,
                    presentationDamageBand));
            Activate(
                mode,
                mode == LastBearingPresentationMode.Driving &&
                readModel.PauseCause == PauseCause.None &&
                !holdAtRecovery &&
                !holdAtModulePoint,
                holdAtRecovery,
                holdAtModulePoint);
        }

        public static LastBearingRoadDamageBand DerivePresentationDamageBand(
            long vehicleConditionMilli)
        {
            if (vehicleConditionMilli < 0 ||
                vehicleConditionMilli >
                    LastBearingBalanceV1.StartingVehicleConditionMilli)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(vehicleConditionMilli));
            }

            if (vehicleConditionMilli <=
                LastBearingBalanceV1.MinimumReturnVehicleConditionMilli)
            {
                return LastBearingRoadDamageBand.Critical;
            }

            return vehicleConditionMilli <
                LastBearingBalanceV1.StartingVehicleConditionMilli
                    ? LastBearingRoadDamageBand.Worn
                    : LastBearingRoadDamageBand.Healthy;
        }

        public void ApplyQuantizedRoadCommandShadow(
            int throttleMilli,
            int steeringMilli)
        {
            if (!HasActiveMode ||
                CurrentMode != LastBearingPresentationMode.Driving ||
                !_roadPresentationActive)
            {
                return;
            }

            TryInvokeRoadAdapter(
                "apply-command-shadow",
                adapter => adapter.ApplyQuantizedCommandShadow(
                    throttleMilli,
                    steeringMilli));
        }

        public void ApplyPresentationOnlyRoadControls(
            int brakeMilli,
            int handbrakeMilli)
        {
            if (!HasActiveMode ||
                CurrentMode != LastBearingPresentationMode.Driving ||
                !_roadPresentationActive)
            {
                return;
            }

            TryInvokeRoadAdapter(
                "apply-presentation-controls",
                adapter => adapter.ApplyPresentationOnlyControls(
                    brakeMilli,
                    handbrakeMilli));
        }

        public static LastBearingPresentationMode ResolveMode(
            LastBearingReadModel readModel,
            LastBearingPresentationMode requestedCityMode)
        {
            if (readModel == null)
            {
                throw new ArgumentNullException(nameof(readModel));
            }

            switch (readModel.ExpeditionPhase)
            {
                case ExpeditionPhase.Outbound:
                case ExpeditionPhase.Returning:
                    return LastBearingPresentationMode.Driving;
                case ExpeditionPhase.AtDepot:
                    return LastBearingPresentationMode.DepotEncounter;
                case ExpeditionPhase.Returned:
                    return LastBearingPresentationMode.CityReturn;
                default:
                    return IsCityMode(requestedCityMode)
                        ? requestedCityMode
                        : LastBearingPresentationMode.CityOverview;
            }
        }

        private void Activate(
            LastBearingPresentationMode mode,
            bool roadShouldRun,
            bool holdAtRecovery,
            bool holdAtModulePoint)
        {
            for (var index = 0; index < OrderedModes.Length; index++)
            {
                _modeRoots[index]?.SetActive(OrderedModes[index] == mode);
            }

            HasActiveMode = true;
            CurrentMode = mode;
            _roadRunRequested = roadShouldRun;
            _roadRecoveryHoldRequested = holdAtRecovery;
            _roadModulePointHoldRequested = holdAtModulePoint;
            if (holdAtRecovery || holdAtModulePoint)
            {
                HoldRoadAdapterAtCanonicalRecoveryPose();
            }
            else if (roadShouldRun)
            {
                if (!_roadPresentationActive)
                {
                    ActivateRoadAdapter();
                }
            }
            else
            {
                SuspendRoadAdapter("canonical-mode-or-pause");
            }

            ApplyPresentationOwnership();
        }

        private void ActivateRoadAdapter()
        {
            IsRoadPresentationHeldAtRecovery = false;
            IsRoadPresentationHeldAtModulePoint = false;
            if (_roadAdapter == null)
            {
                _roadPresentationActive = false;
                return;
            }

            if (_canonicalVehicle != null)
            {
                _canonicalVehicle.SnapToCanonicalRoadPose();
                if (!TryInvokeRoadAdapter(
                        "synchronize-pose",
                        adapter => adapter.SynchronizePresentationPose(
                            _canonicalVehicle.transform.position,
                            _canonicalVehicle.transform.rotation)))
                {
                    return;
                }
            }

            if (!TryInvokeRoadAdapter(
                    "activate-road",
                    adapter => adapter.SetRoadModeActive(true)))
            {
                return;
            }

            _roadPresentationActive = true;
        }

        private void HoldRoadAdapterAtCanonicalRecoveryPose()
        {
            _roadPresentationActive = false;
            IsRoadPresentationHeldAtRecovery = false;
            IsRoadPresentationHeldAtModulePoint = false;
            if (_roadAdapter == null || _canonicalVehicle == null)
            {
                return;
            }

            if (!TryInvokeRoadAdapter(
                    "hold-recovery-suspend",
                    adapter => adapter.SetRoadModeActive(false)))
            {
                return;
            }

            _canonicalVehicle.SnapToCanonicalRoadPose();
            if (!TryInvokeRoadAdapter(
                    "hold-recovery-synchronize",
                    adapter => adapter.SynchronizePresentationPose(
                        _canonicalVehicle.transform.position,
                        _canonicalVehicle.transform.rotation)))
            {
                return;
            }

            IsRoadPresentationHeldAtRecovery = _roadRecoveryHoldRequested;
            IsRoadPresentationHeldAtModulePoint =
                _roadModulePointHoldRequested;
        }

        private void SuspendRoadAdapter(string operation)
        {
            _roadPresentationActive = false;
            IsRoadPresentationHeldAtRecovery = false;
            IsRoadPresentationHeldAtModulePoint = false;
            if (_roadAdapter == null)
            {
                return;
            }

            TryInvokeRoadAdapter(
                operation,
                adapter =>
                {
                    adapter.SetRoadModeActive(false);
                    adapter.ResetPresentation();
                });
        }

        private bool TryInvokeRoadAdapter(
            string operation,
            Action<ILastBearingRoadModeAdapter> action)
        {
            ILastBearingRoadModeAdapter? adapter = _roadAdapter;
            if (adapter == null)
            {
                return false;
            }

            try
            {
                action(adapter);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "LAST_BEARING_ROAD_PRESENTATION_DISABLED " +
                    operation + " " + exception.GetType().Name,
                    this);
                try
                {
                    adapter.ResetPresentation();
                }
                catch (Exception)
                {
                    // The canonical core must keep progressing even if cleanup fails.
                }

                try
                {
                    adapter.SetRoadModeActive(false);
                }
                catch (Exception)
                {
                    // Detaching below is the final fail-closed boundary.
                }

                _roadAdapter = null;
                _roadPresentationActive = false;
                IsRoadPresentationHeldAtRecovery = false;
                IsRoadPresentationHeldAtModulePoint = false;
                RoadAdapterFaulted = true;
                ApplyPresentationOwnership();
                return false;
            }
        }

        private void ApplyPresentationOwnership()
        {
            bool roadModeSelected =
                HasActiveMode &&
                CurrentMode == LastBearingPresentationMode.Driving;
            bool roadPresentationAvailable =
                roadModeSelected &&
                !RoadAdapterFaulted &&
                _roadAdapter != null &&
                _roadTarget != null;
            bool garageInspectionSelected =
                HasActiveMode &&
                CurrentMode == LastBearingPresentationMode.GarageBay;

            if (_roadTarget != null)
            {
                _roadTarget.gameObject.SetActive(roadPresentationAvailable);
            }

            if (_canonicalVehicle != null)
            {
                _canonicalVehicle.gameObject.SetActive(!roadPresentationAvailable);
            }

            if (_cameraRig != null && _canonicalVehicle != null)
            {
                _cameraRig.SetRoadTarget(
                    roadPresentationAvailable
                        ? _roadTarget!
                        : _canonicalVehicle.transform);
                if (_garageCameraAnchor != null && _garageFocusAnchor != null)
                {
                    _cameraRig.SetInspectionPose(
                        _garageCameraAnchor,
                        _garageFocusAnchor,
                        garageInspectionSelected);
                }
            }
        }

        private static bool IsCityMode(LastBearingPresentationMode mode)
        {
            return mode == LastBearingPresentationMode.CityOverview ||
                   mode == LastBearingPresentationMode.BuildingCutaway ||
                   mode == LastBearingPresentationMode.GarageBay;
        }
    }
}
