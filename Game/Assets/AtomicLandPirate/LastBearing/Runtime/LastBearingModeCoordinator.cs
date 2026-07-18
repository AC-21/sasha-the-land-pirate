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

    /// <summary>
    /// Narrow input-only bridge for a road presentation. Physics telemetry and
    /// object state deliberately have no route back through this interface.
    /// This is not the lab's complete local driving surface: service brake and
    /// reverse remain local until a canonical command contract exists.
    /// </summary>
    public interface ILastBearingRoadModeAdapter
    {
        bool IsRoadModeActive { get; }

        void SetRoadModeActive(bool active);

        void ApplyQuantizedCommandShadow(int throttleMilli, int steeringMilli);

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
        private Transform? _modeRoot;
        private bool _initialized;

        public bool HasActiveMode { get; private set; }

        public LastBearingPresentationMode CurrentMode { get; private set; } =
            LastBearingPresentationMode.CityOverview;

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

        public void AttachRoadModeAdapter(ILastBearingRoadModeAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            if (_roadAdapter != null && !ReferenceEquals(_roadAdapter, adapter))
            {
                _roadAdapter.SetRoadModeActive(false);
                _roadAdapter.ResetPresentation();
            }

            _roadAdapter = adapter;
            _roadAdapter.SetRoadModeActive(
                HasActiveMode && CurrentMode == LastBearingPresentationMode.Driving);
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

            _roadAdapter?.SetRoadModeActive(false);
            _roadAdapter?.ResetPresentation();
        }

        public void ApplyCanonical(LastBearingReadModel? readModel)
        {
            Initialize();
            if (readModel == null)
            {
                ClearSession();
                return;
            }

            Activate(ResolveMode(readModel, _requestedCityMode));
        }

        public void ApplyQuantizedRoadCommandShadow(
            int throttleMilli,
            int steeringMilli)
        {
            if (!HasActiveMode ||
                CurrentMode != LastBearingPresentationMode.Driving)
            {
                return;
            }

            _roadAdapter?.ApplyQuantizedCommandShadow(
                throttleMilli,
                steeringMilli);
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

        private void Activate(LastBearingPresentationMode mode)
        {
            for (var index = 0; index < OrderedModes.Length; index++)
            {
                _modeRoots[index]?.SetActive(OrderedModes[index] == mode);
            }

            HasActiveMode = true;
            CurrentMode = mode;
            bool roadActive = mode == LastBearingPresentationMode.Driving;
            _roadAdapter?.SetRoadModeActive(roadActive);
            if (!roadActive)
            {
                _roadAdapter?.ResetPresentation();
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
