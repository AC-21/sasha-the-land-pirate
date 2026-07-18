#nullable enable

using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    /// <summary>
    /// Two reversible, presentation-only D-0030 hypotheses. This surface is a
    /// comparison instrument, not a city-grammar selection or canonical state.
    /// </summary>
    public enum LastBearingCityGrammarHypothesis
    {
        Unselected = 0,
        RestrainedSnapGrid = 1,
        DistrictStamp = 2
    }

    public enum LastBearingCityTrialPiece
    {
        Recycler = 0,
        Workshop = 1
    }

    public enum LastBearingCityTrialDeliveryStage
    {
        AtRecycler = 0,
        InTransit = 1,
        DeliveredToWorkshop = 2
    }

    public enum LastBearingCityTrialPathRead
    {
        Unrecorded = 0,
        Clear = 1,
        Unclear = 2
    }

    /// <summary>
    /// A local observation lab for one identical service-cell task. Neither
    /// transform layout nor its evidence enters the deterministic kernel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingCityGrammarComparison : MonoBehaviour
    {
        private const int Unplaced = -1;

        private static readonly Vector3[] SnapGridPositions =
        {
            new Vector3(-0.7f, 0.65f, -0.25f),
            new Vector3(0.7f, 0.65f, -0.25f),
            new Vector3(2.1f, 0.65f, -0.25f)
        };

        private static readonly Vector3[] DistrictStampPositions =
        {
            new Vector3(0f, 0f, 0.2f),
            new Vector3(-2.8f, 0f, 0.2f),
            new Vector3(2.8f, 0f, 0.2f)
        };

        private static readonly Vector3 ServiceBuildingScale =
            new Vector3(1.3f, 1.2f, 1.4f);

        private static readonly Vector3 RecyclerHoldingPosition =
            new Vector3(-2.4f, 0.7f, -4.15f);
        private static readonly Vector3 WorkshopHoldingPosition =
            new Vector3(2.4f, 0.7f, -4.15f);
        private static readonly Vector3 DistrictHoldingPosition =
            new Vector3(0f, 0f, -4.1f);

        private GameObject? _snapGridRoot;
        private GameObject? _districtStampRoot;
        private Transform? _snapGridRecycler;
        private Transform? _snapGridWorkshop;
        private GameObject? _snapGridLink;
        private Transform? _snapGridCarrier;
        private Transform? _districtStampPrototype;
        private Transform? _districtCarrier;
        private bool _built;
        private int _snapRecyclerPadIndex;
        private int _snapWorkshopPadIndex;
        private int _districtAnchorIndex;
        private int _snapRecyclerQuarterTurns;
        private int _snapWorkshopQuarterTurns;
        private int _districtQuarterTurns;
        private bool _snapLinkConnected;
        private LastBearingCityTrialDeliveryStage _snapDeliveryStage;
        private LastBearingCityTrialDeliveryStage _districtDeliveryStage;
        private LastBearingCityTrialPathRead _snapPathRead;
        private LastBearingCityTrialPathRead _districtPathRead;
        private int _selectionCount;
        private int _interactionCount;
        private int _snapLayoutActionCount;
        private int _snapLogisticsActionCount;
        private int _districtLayoutActionCount;
        private int _districtLogisticsActionCount;
        private int _completedObservationCount;

        public LastBearingCityGrammarHypothesis SelectedHypothesis { get; private set; }

        public LastBearingCityGrammarHypothesis LastCompletedHypothesis
        {
            get;
            private set;
        }

        public LastBearingCityTrialPiece ActiveSnapGridPiece { get; private set; }

        public int SelectionCount => _selectionCount;

        public int InteractionCount => _interactionCount;

        public int CompletedObservationCount => _completedObservationCount;

        public int RecyclerPadIndex => _snapRecyclerPadIndex;

        public int WorkshopPadIndex => _snapWorkshopPadIndex;

        public int DistrictAnchorIndex => _districtAnchorIndex;

        public int PoseIndex => SelectedHypothesis ==
                                LastBearingCityGrammarHypothesis.DistrictStamp
            ? _districtAnchorIndex
            : ActiveSnapGridPiece == LastBearingCityTrialPiece.Recycler
                ? _snapRecyclerPadIndex
                : _snapWorkshopPadIndex;

        public int QuarterTurns => SelectedHypothesis ==
                                   LastBearingCityGrammarHypothesis.DistrictStamp
            ? _districtQuarterTurns
            : ActiveSnapGridPiece == LastBearingCityTrialPiece.Recycler
                ? _snapRecyclerQuarterTurns
                : _snapWorkshopQuarterTurns;

        public bool HasValidSnapGridLayout =>
            _snapRecyclerPadIndex >= 0 &&
            _snapWorkshopPadIndex >= 0 &&
            _snapRecyclerPadIndex != _snapWorkshopPadIndex;

        public bool IsLogisticsConnected => SelectedHypothesis switch
        {
            LastBearingCityGrammarHypothesis.RestrainedSnapGrid =>
                HasValidSnapGridLayout && _snapLinkConnected,
            LastBearingCityGrammarHypothesis.DistrictStamp =>
                _districtAnchorIndex >= 0,
            _ => false
        };

        public LastBearingCityTrialDeliveryStage DeliveryStage =>
            SelectedHypothesis == LastBearingCityGrammarHypothesis.DistrictStamp
                ? _districtDeliveryStage
                : _snapDeliveryStage;

        public LastBearingCityTrialPathRead PathRead =>
            SelectedHypothesis == LastBearingCityGrammarHypothesis.DistrictStamp
                ? _districtPathRead
                : _snapPathRead;

        public bool TrialReady =>
            SelectedHypothesis != LastBearingCityGrammarHypothesis.Unselected &&
            IsLogisticsConnected &&
            DeliveryStage == LastBearingCityTrialDeliveryStage.DeliveredToWorkshop &&
            PathRead != LastBearingCityTrialPathRead.Unrecorded;

        public bool HasCompletedObservation =>
            _snapPathRead != LastBearingCityTrialPathRead.Unrecorded ||
            _districtPathRead != LastBearingCityTrialPathRead.Unrecorded;

        public string FixedCameraSetupId =>
            LastBearingCameraRig.ComparisonCameraSetupId;

        public string EvidenceSummary
        {
            get
            {
                int layoutActions = SelectedHypothesis ==
                                    LastBearingCityGrammarHypothesis.DistrictStamp
                    ? _districtLayoutActionCount
                    : _snapLayoutActionCount;
                int logisticsActions = SelectedHypothesis ==
                                       LastBearingCityGrammarHypothesis.DistrictStamp
                    ? _districtLogisticsActionCount
                    : _snapLogisticsActionCount;
                return
                    "task=empty-calibration-sled-recycler-to-workshop" +
                    " · camera=" + FixedCameraSetupId +
                    " · hypothesis=" + SelectedHypothesis +
                    " · active-piece=" + ActiveSnapGridPiece +
                    " · recycler-pad=" + FormatSocket(_snapRecyclerPadIndex) +
                    " · workshop-pad=" + FormatSocket(_snapWorkshopPadIndex) +
                    " · district-anchor=" + FormatSocket(_districtAnchorIndex) +
                    " · link=" + (IsLogisticsConnected ? "ready" : "open") +
                    " · delivery=" + DeliveryStage +
                    " · path-read=" + PathRead +
                    " · trial-ready=" + TrialReady +
                    " · current-layout-actions=" + layoutActions +
                    " · current-logistics-actions=" + logisticsActions +
                    " · A{layout=" + _snapLayoutActionCount +
                    ",logistics=" + _snapLogisticsActionCount +
                    ",path=" + _snapPathRead + "}" +
                    " · B{layout=" + _districtLayoutActionCount +
                    ",logistics=" + _districtLogisticsActionCount +
                    ",path=" + _districtPathRead + "}" +
                    " · selections=" + SelectionCount +
                    " · interactions=" + InteractionCount +
                    " · completed=" + LastCompletedHypothesis +
                    " · observations=" + CompletedObservationCount;
            }
        }

        internal void Build(
            Material concrete,
            Material iron,
            Material oxide,
            Material bone)
        {
            if (_built)
            {
                return;
            }

            _built = true;
            transform.localPosition = new Vector3(15f, 0.15f, -8f);

            _snapGridRoot = new GameObject(
                "HYPOTHESIS A - Restrained Snap Grid Service Cell");
            _snapGridRoot.transform.SetParent(transform, false);
            for (var index = 0; index < SnapGridPositions.Length; index++)
            {
                CreateBlock(
                    "Snap Pad " + (index + 1).ToString("00"),
                    _snapGridRoot.transform,
                    new Vector3(SnapGridPositions[index].x, 0.05f, SnapGridPositions[index].z),
                    new Vector3(1.35f, 0.1f, 1.55f),
                    bone);
            }

            _snapGridRecycler = CreateBlock(
                "Individually Placed Recycler",
                _snapGridRoot.transform,
                RecyclerHoldingPosition,
                ServiceBuildingScale,
                oxide).transform;
            _snapGridWorkshop = CreateBlock(
                "Individually Placed Workshop",
                _snapGridRoot.transform,
                WorkshopHoldingPosition,
                ServiceBuildingScale,
                iron).transform;
            _snapGridLink = CreateBlock(
                "Player Connected Logistics Link",
                _snapGridRoot.transform,
                Vector3.zero,
                new Vector3(1f, 0.12f, 0.55f),
                concrete);
            _snapGridCarrier = CreateBlock(
                "Shared Empty Calibration Sled",
                _snapGridRoot.transform,
                Vector3.zero,
                new Vector3(0.48f, 0.38f, 0.38f),
                bone).transform;

            _districtStampRoot = new GameObject(
                "HYPOTHESIS B - District Stamp Service Cell");
            _districtStampRoot.transform.SetParent(transform, false);
            for (var index = 0; index < DistrictStampPositions.Length; index++)
            {
                CreateBlock(
                    "District Anchor " + (index + 1).ToString("00"),
                    _districtStampRoot.transform,
                    new Vector3(DistrictStampPositions[index].x, 0.04f, DistrictStampPositions[index].z),
                    new Vector3(2.4f, 0.08f, 2.2f),
                    bone);
            }

            var stamp = new GameObject("Whole Civic Service Stamp");
            stamp.transform.SetParent(_districtStampRoot.transform, false);
            _districtStampPrototype = stamp.transform;
            CreateBlock(
                "Stamped Recycler",
                stamp.transform,
                new Vector3(-0.7f, 0.6f, -0.45f),
                ServiceBuildingScale,
                oxide);
            CreateBlock(
                "Stamped Workshop",
                stamp.transform,
                new Vector3(0.7f, 0.6f, -0.45f),
                ServiceBuildingScale,
                iron);
            CreateBlock(
                "Stamped Shared Logistics Apron",
                stamp.transform,
                new Vector3(0f, 0.08f, 0.3f),
                new Vector3(2.35f, 0.12f, 0.75f),
                concrete);
            _districtCarrier = CreateBlock(
                "Shared Empty Calibration Sled",
                stamp.transform,
                Vector3.zero,
                new Vector3(0.48f, 0.38f, 0.38f),
                bone).transform;

            BeginSession();
        }

        public void SelectHypothesis(LastBearingCityGrammarHypothesis hypothesis)
        {
            if (hypothesis != LastBearingCityGrammarHypothesis.Unselected &&
                hypothesis !=
                    LastBearingCityGrammarHypothesis.RestrainedSnapGrid &&
                hypothesis != LastBearingCityGrammarHypothesis.DistrictStamp)
            {
                return;
            }

            if (hypothesis == LastBearingCityGrammarHypothesis.Unselected)
            {
                LeaveComparison();
                return;
            }

            if (SelectedHypothesis != hypothesis)
            {
                SelectedHypothesis = hypothesis;
                _selectionCount++;
            }

            ApplyPresentation();
        }

        public bool ManipulatePrimary()
        {
            switch (SelectedHypothesis)
            {
                case LastBearingCityGrammarHypothesis.RestrainedSnapGrid:
                    if (ActiveSnapGridPiece == LastBearingCityTrialPiece.Recycler)
                    {
                        _snapRecyclerPadIndex = NextSocket(_snapRecyclerPadIndex);
                    }
                    else
                    {
                        _snapWorkshopPadIndex = NextSocket(_snapWorkshopPadIndex);
                    }

                    _snapLinkConnected = false;
                    _snapDeliveryStage = LastBearingCityTrialDeliveryStage.AtRecycler;
                    InvalidateSnapObservation();
                    _snapLayoutActionCount++;
                    break;
                case LastBearingCityGrammarHypothesis.DistrictStamp:
                    _districtAnchorIndex = NextSocket(_districtAnchorIndex);
                    _districtDeliveryStage = LastBearingCityTrialDeliveryStage.AtRecycler;
                    InvalidateDistrictObservation();
                    _districtLayoutActionCount++;
                    break;
                default:
                    return false;
            }

            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public bool RotatePrimary()
        {
            switch (SelectedHypothesis)
            {
                case LastBearingCityGrammarHypothesis.RestrainedSnapGrid:
                    if (ActiveSnapGridPiece == LastBearingCityTrialPiece.Recycler)
                    {
                        _snapRecyclerQuarterTurns =
                            (_snapRecyclerQuarterTurns + 1) % 4;
                    }
                    else
                    {
                        _snapWorkshopQuarterTurns =
                            (_snapWorkshopQuarterTurns + 1) % 4;
                    }

                    _snapDeliveryStage = LastBearingCityTrialDeliveryStage.AtRecycler;
                    InvalidateSnapObservation();
                    _snapLayoutActionCount++;
                    break;
                case LastBearingCityGrammarHypothesis.DistrictStamp:
                    _districtQuarterTurns = (_districtQuarterTurns + 1) % 4;
                    _districtDeliveryStage = LastBearingCityTrialDeliveryStage.AtRecycler;
                    InvalidateDistrictObservation();
                    _districtLayoutActionCount++;
                    break;
                default:
                    return false;
            }

            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public bool ToggleSnapGridPiece()
        {
            if (SelectedHypothesis !=
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid)
            {
                return false;
            }

            ActiveSnapGridPiece = ActiveSnapGridPiece ==
                                  LastBearingCityTrialPiece.Recycler
                ? LastBearingCityTrialPiece.Workshop
                : LastBearingCityTrialPiece.Recycler;
            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public bool ConnectLogistics()
        {
            if (SelectedHypothesis !=
                    LastBearingCityGrammarHypothesis.RestrainedSnapGrid ||
                !HasValidSnapGridLayout ||
                _snapLinkConnected)
            {
                return false;
            }

            _snapLinkConnected = true;
            _snapDeliveryStage = LastBearingCityTrialDeliveryStage.AtRecycler;
            InvalidateSnapObservation();
            _snapLogisticsActionCount++;
            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public bool AdvanceDelivery()
        {
            if (!IsLogisticsConnected ||
                DeliveryStage ==
                LastBearingCityTrialDeliveryStage.DeliveredToWorkshop)
            {
                return false;
            }

            if (SelectedHypothesis ==
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid)
            {
                _snapDeliveryStage++;
                _snapLogisticsActionCount++;
            }
            else if (SelectedHypothesis ==
                     LastBearingCityGrammarHypothesis.DistrictStamp)
            {
                _districtDeliveryStage++;
                _districtLogisticsActionCount++;
            }
            else
            {
                return false;
            }

            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public bool RecordPathRead(LastBearingCityTrialPathRead pathRead)
        {
            if ((pathRead != LastBearingCityTrialPathRead.Clear &&
                 pathRead != LastBearingCityTrialPathRead.Unclear) ||
                SelectedHypothesis == LastBearingCityGrammarHypothesis.Unselected ||
                !IsLogisticsConnected ||
                DeliveryStage !=
                LastBearingCityTrialDeliveryStage.DeliveredToWorkshop ||
                PathRead != LastBearingCityTrialPathRead.Unrecorded)
            {
                return false;
            }

            if (SelectedHypothesis ==
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid)
            {
                _snapPathRead = pathRead;
                _snapLogisticsActionCount++;
            }
            else
            {
                _districtPathRead = pathRead;
                _districtLogisticsActionCount++;
            }

            LastCompletedHypothesis = SelectedHypothesis;
            _completedObservationCount++;
            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public bool ResetActiveTrial()
        {
            switch (SelectedHypothesis)
            {
                case LastBearingCityGrammarHypothesis.RestrainedSnapGrid:
                    ResetSnapGridLayout();
                    break;
                case LastBearingCityGrammarHypothesis.DistrictStamp:
                    ResetDistrictLayout();
                    break;
                default:
                    return false;
            }

            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public void LeaveComparison()
        {
            SelectedHypothesis = LastBearingCityGrammarHypothesis.Unselected;
            ApplyPresentation();
        }

        public void ResetComparison()
        {
            if (SelectedHypothesis != LastBearingCityGrammarHypothesis.Unselected ||
                _snapRecyclerPadIndex != Unplaced ||
                _snapWorkshopPadIndex != Unplaced ||
                _districtAnchorIndex != Unplaced)
            {
                _interactionCount++;
            }

            ResetTrialLayout();
            SelectedHypothesis = LastBearingCityGrammarHypothesis.Unselected;
            LastCompletedHypothesis = LastBearingCityGrammarHypothesis.Unselected;
            _completedObservationCount = 0;
            ApplyPresentation();
        }

        internal void BeginSession()
        {
            ResetTrialLayout();
            SelectedHypothesis = LastBearingCityGrammarHypothesis.Unselected;
            LastCompletedHypothesis = LastBearingCityGrammarHypothesis.Unselected;
            ActiveSnapGridPiece = LastBearingCityTrialPiece.Recycler;
            _selectionCount = 0;
            _interactionCount = 0;
            _snapLayoutActionCount = 0;
            _snapLogisticsActionCount = 0;
            _districtLayoutActionCount = 0;
            _districtLogisticsActionCount = 0;
            _completedObservationCount = 0;
            ApplyPresentation();
        }

        private void ResetTrialLayout()
        {
            ResetSnapGridLayout();
            ResetDistrictLayout();
        }

        private void ResetSnapGridLayout()
        {
            _snapRecyclerPadIndex = Unplaced;
            _snapWorkshopPadIndex = Unplaced;
            _snapRecyclerQuarterTurns = 0;
            _snapWorkshopQuarterTurns = 0;
            _snapLinkConnected = false;
            _snapDeliveryStage = LastBearingCityTrialDeliveryStage.AtRecycler;
            InvalidateSnapObservation();
            ActiveSnapGridPiece = LastBearingCityTrialPiece.Recycler;
        }

        private void ResetDistrictLayout()
        {
            _districtAnchorIndex = Unplaced;
            _districtQuarterTurns = 0;
            _districtDeliveryStage = LastBearingCityTrialDeliveryStage.AtRecycler;
            InvalidateDistrictObservation();
        }

        private void InvalidateSnapObservation()
        {
            _snapPathRead = LastBearingCityTrialPathRead.Unrecorded;
            if (LastCompletedHypothesis ==
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid)
            {
                LastCompletedHypothesis =
                    _districtPathRead != LastBearingCityTrialPathRead.Unrecorded
                        ? LastBearingCityGrammarHypothesis.DistrictStamp
                        : LastBearingCityGrammarHypothesis.Unselected;
            }
        }

        private void InvalidateDistrictObservation()
        {
            _districtPathRead = LastBearingCityTrialPathRead.Unrecorded;
            if (LastCompletedHypothesis ==
                LastBearingCityGrammarHypothesis.DistrictStamp)
            {
                LastCompletedHypothesis =
                    _snapPathRead != LastBearingCityTrialPathRead.Unrecorded
                        ? LastBearingCityGrammarHypothesis.RestrainedSnapGrid
                        : LastBearingCityGrammarHypothesis.Unselected;
            }
        }

        private void ApplyPresentation()
        {
            if (!_built ||
                _snapGridRoot == null ||
                _districtStampRoot == null ||
                _snapGridRecycler == null ||
                _snapGridWorkshop == null ||
                _snapGridLink == null ||
                _snapGridCarrier == null ||
                _districtStampPrototype == null ||
                _districtCarrier == null)
            {
                return;
            }

            _snapGridRoot.SetActive(
                SelectedHypothesis == LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            _districtStampRoot.SetActive(
                SelectedHypothesis == LastBearingCityGrammarHypothesis.DistrictStamp);

            Vector3 recyclerPosition = ResolvePosition(
                _snapRecyclerPadIndex,
                RecyclerHoldingPosition,
                SnapGridPositions);
            Vector3 workshopPosition = ResolvePosition(
                _snapWorkshopPadIndex,
                WorkshopHoldingPosition,
                SnapGridPositions);
            _snapGridRecycler.localPosition = recyclerPosition;
            _snapGridWorkshop.localPosition = workshopPosition;
            _snapGridRecycler.localRotation = Quaternion.Euler(
                0f,
                _snapRecyclerQuarterTurns * 90f,
                0f);
            _snapGridWorkshop.localRotation = Quaternion.Euler(
                0f,
                _snapWorkshopQuarterTurns * 90f,
                0f);

            bool showSnapRoute = HasValidSnapGridLayout && _snapLinkConnected;
            _snapGridLink.SetActive(showSnapRoute);
            _snapGridCarrier.gameObject.SetActive(showSnapRoute);
            if (showSnapRoute)
            {
                Vector3 routeStart = WithHeight(recyclerPosition, 0.18f);
                Vector3 routeEnd = WithHeight(workshopPosition, 0.18f);
                _snapGridLink.transform.localPosition =
                    Vector3.Lerp(routeStart, routeEnd, 0.5f);
                _snapGridLink.transform.localScale = new Vector3(
                    Vector3.Distance(routeStart, routeEnd) + 0.35f,
                    0.12f,
                    0.55f);
                _snapGridCarrier.localPosition = Vector3.Lerp(
                    WithHeight(recyclerPosition, 0.38f),
                    WithHeight(workshopPosition, 0.38f),
                    (int)_snapDeliveryStage / 2f);
            }

            _districtStampPrototype.localPosition = ResolvePosition(
                _districtAnchorIndex,
                DistrictHoldingPosition,
                DistrictStampPositions);
            _districtStampPrototype.localRotation = Quaternion.Euler(
                0f,
                _districtQuarterTurns * 90f,
                0f);
            _districtCarrier.gameObject.SetActive(_districtAnchorIndex >= 0);
            _districtCarrier.localPosition = _districtDeliveryStage switch
            {
                LastBearingCityTrialDeliveryStage.AtRecycler =>
                    new Vector3(-0.7f, 0.38f, -0.05f),
                LastBearingCityTrialDeliveryStage.InTransit =>
                    new Vector3(0f, 0.38f, 0.3f),
                _ => new Vector3(0.7f, 0.38f, -0.05f)
            };
        }

        private static int NextSocket(int current)
        {
            return current < 0 ? 0 : (current + 1) % 3;
        }

        private static Vector3 ResolvePosition(
            int index,
            Vector3 holdingPosition,
            Vector3[] positions)
        {
            return index < 0 ? holdingPosition : positions[index];
        }

        private static Vector3 WithHeight(Vector3 position, float height)
        {
            return new Vector3(position.x, height, position.z);
        }

        private static string FormatSocket(int index)
        {
            return index < 0 ? "holding" : (index + 1).ToString("00");
        }

        private static GameObject CreateBlock(
            string objectName,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }
    }
}
