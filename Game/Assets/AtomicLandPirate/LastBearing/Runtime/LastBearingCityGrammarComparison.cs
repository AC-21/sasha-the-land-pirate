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

    [DisallowMultipleComponent]
    public sealed class LastBearingCityGrammarComparison : MonoBehaviour
    {
        private static readonly Vector3[] SnapGridPositions =
        {
            new Vector3(-2.4f, 0.7f, -1.8f),
            new Vector3(0f, 0.7f, -1.8f),
            new Vector3(2.4f, 0.7f, -1.8f)
        };

        private static readonly Vector3[] DistrictStampPositions =
        {
            new Vector3(-2.6f, 0f, 0.2f),
            new Vector3(0f, 0f, 0.2f),
            new Vector3(2.6f, 0f, 0.2f)
        };

        private GameObject? _snapGridRoot;
        private GameObject? _districtStampRoot;
        private Transform? _snapGridPrototype;
        private Transform? _districtStampPrototype;
        private bool _built;
        private int _poseIndex;
        private int _quarterTurns;
        private int _selectionCount;
        private int _interactionCount;

        public LastBearingCityGrammarHypothesis SelectedHypothesis { get; private set; }

        public int SelectionCount => _selectionCount;

        public int InteractionCount => _interactionCount;

        public int PoseIndex => _poseIndex;

        public int QuarterTurns => _quarterTurns;

        public string FixedCameraSetupId =>
            LastBearingCameraRig.ComparisonCameraSetupId;

        public string EvidenceSummary =>
            "camera=" + FixedCameraSetupId +
            " · hypothesis=" + SelectedHypothesis +
            " · selections=" + SelectionCount +
            " · manipulations=" + InteractionCount +
            " · pose=" + PoseIndex +
            " · quarter-turns=" + QuarterTurns;

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
                "HYPOTHESIS A - Restrained Snap Grid Buildings");
            _snapGridRoot.transform.SetParent(transform, false);
            for (var index = 0; index < SnapGridPositions.Length; index++)
            {
                CreateBlock(
                    "Snap Pad " + (index + 1).ToString("00"),
                    _snapGridRoot.transform,
                    new Vector3(SnapGridPositions[index].x, 0.05f, SnapGridPositions[index].z),
                    new Vector3(2.05f, 0.1f, 2.05f),
                    bone);
            }

            _snapGridPrototype = CreateBlock(
                "Individually Placed Recycler Prototype",
                _snapGridRoot.transform,
                SnapGridPositions[0],
                new Vector3(1.7f, 1.35f, 1.7f),
                oxide).transform;
            CreateBlock(
                "Physical Road Stub",
                _snapGridRoot.transform,
                new Vector3(0f, 0.08f, 0.25f),
                new Vector3(7.2f, 0.12f, 1f),
                iron);

            _districtStampRoot = new GameObject(
                "HYPOTHESIS B - District Stamp");
            _districtStampRoot.transform.SetParent(transform, false);
            for (var index = 0; index < DistrictStampPositions.Length; index++)
            {
                CreateBlock(
                    "District Anchor " + (index + 1).ToString("00"),
                    _districtStampRoot.transform,
                    new Vector3(DistrictStampPositions[index].x, 0.04f, DistrictStampPositions[index].z),
                    new Vector3(2.25f, 0.08f, 3.8f),
                    bone);
            }

            var stamp = new GameObject("Whole Civic Service Stamp");
            stamp.transform.SetParent(_districtStampRoot.transform, false);
            _districtStampPrototype = stamp.transform;
            CreateBlock(
                "Stamped Recycler",
                stamp.transform,
                new Vector3(-0.65f, 0.62f, -0.55f),
                new Vector3(1.1f, 1.2f, 1.4f),
                oxide);
            CreateBlock(
                "Stamped Workshop",
                stamp.transform,
                new Vector3(0.65f, 0.62f, -0.55f),
                new Vector3(1.1f, 1.2f, 1.4f),
                iron);
            CreateBlock(
                "Stamped Shared Logistics Apron",
                stamp.transform,
                new Vector3(0f, 0.08f, 0.95f),
                new Vector3(2.35f, 0.12f, 1.2f),
                concrete);

            BeginSession();
        }

        public void SelectHypothesis(LastBearingCityGrammarHypothesis hypothesis)
        {
            if (hypothesis == LastBearingCityGrammarHypothesis.Unselected)
            {
                ResetComparison();
                return;
            }

            if (SelectedHypothesis != hypothesis)
            {
                SelectedHypothesis = hypothesis;
                _selectionCount++;
                _poseIndex = 0;
                _quarterTurns = 0;
            }

            ApplyPresentation();
        }

        public bool ManipulatePrimary()
        {
            if (SelectedHypothesis == LastBearingCityGrammarHypothesis.Unselected)
            {
                return false;
            }

            _poseIndex = (_poseIndex + 1) % 3;
            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public bool RotatePrimary()
        {
            if (SelectedHypothesis == LastBearingCityGrammarHypothesis.Unselected)
            {
                return false;
            }

            _quarterTurns = (_quarterTurns + 1) % 4;
            _interactionCount++;
            ApplyPresentation();
            return true;
        }

        public void ResetComparison()
        {
            if (SelectedHypothesis != LastBearingCityGrammarHypothesis.Unselected ||
                _poseIndex != 0 ||
                _quarterTurns != 0)
            {
                _interactionCount++;
            }

            SelectedHypothesis = LastBearingCityGrammarHypothesis.Unselected;
            _poseIndex = 0;
            _quarterTurns = 0;
            ApplyPresentation();
        }

        internal void BeginSession()
        {
            SelectedHypothesis = LastBearingCityGrammarHypothesis.Unselected;
            _poseIndex = 0;
            _quarterTurns = 0;
            _selectionCount = 0;
            _interactionCount = 0;
            ApplyPresentation();
        }

        private void ApplyPresentation()
        {
            if (!_built ||
                _snapGridRoot == null ||
                _districtStampRoot == null ||
                _snapGridPrototype == null ||
                _districtStampPrototype == null)
            {
                return;
            }

            _snapGridRoot.SetActive(
                SelectedHypothesis == LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            _districtStampRoot.SetActive(
                SelectedHypothesis == LastBearingCityGrammarHypothesis.DistrictStamp);
            _snapGridPrototype.localPosition = SnapGridPositions[_poseIndex];
            _districtStampPrototype.localPosition = DistrictStampPositions[_poseIndex];
            var rotation = Quaternion.Euler(0f, _quarterTurns * 90f, 0f);
            _snapGridPrototype.localRotation = rotation;
            _districtStampPrototype.localRotation = rotation;
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
