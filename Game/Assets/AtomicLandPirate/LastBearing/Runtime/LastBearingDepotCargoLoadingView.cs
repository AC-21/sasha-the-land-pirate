#nullable enable

using System;
using AtomicLandPirate.Simulation.LastBearing;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum DepotCargoLoadingPresentationState
    {
        Dormant = 0,
        CeramicBearingAtDepot = 1,
        CeramicBearingAtFaction = 2,
        FieldSleeveAtFaction = 3,
        CeramicBearingOnVehicle = 4,
        FieldSleeveOnVehicle = 5,
        Applied = 6,
    }

    /// <summary>
    /// Physical, presentation-only custody read for the two exact repair cargo
    /// outcomes at Last Bearing depot. The view owns no input, command, save,
    /// proximity, physics, or canonical-state authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingDepotCargoLoadingView : MonoBehaviour
    {
        public const string DirectionPackageId = "C0-VGR-10";
        public const string Revision = "R1";
        public const string ContentId = "poi_depot_repair_cargo_load_a";
        public const string RootName =
            "poi_depot_repair_cargo_load_a [C0-VGR-10 R1]";
        public const string InteractionAnchorName =
            "ANCHOR_DEPOT_REPAIR_CARGO_LOAD";

        private GameObject? _ceramicBearingAtDepot;
        private GameObject? _ceramicBearingAtFaction;
        private GameObject? _fieldSleeveAtFaction;
        private GameObject? _canonicalCeramicBearing;
        private GameObject? _canonicalFieldSleeve;
        private GameObject? _roadCeramicBearing;
        private GameObject? _roadFieldSleeve;
        private Material? _bone;
        private Material? _oxide;
        private Material? _signal;
        private Light? _workLight;
        private bool _built;
        private bool _vehicleCargoBuilt;

        public Transform? InteractionAnchor { get; private set; }

        public DepotCargoLoadingPresentationState State { get; private set; }

        public bool IsCeramicBearingAtDepotVisible =>
            _ceramicBearingAtDepot != null && _ceramicBearingAtDepot.activeSelf;

        public bool IsCeramicBearingAtFactionVisible =>
            _ceramicBearingAtFaction != null &&
            _ceramicBearingAtFaction.activeSelf;

        public bool IsFieldSleeveAtFactionVisible =>
            _fieldSleeveAtFaction != null && _fieldSleeveAtFaction.activeSelf;

        public bool IsCanonicalVehicleCargoVisible =>
            IsActive(_canonicalCeramicBearing) || IsActive(_canonicalFieldSleeve);

        public bool IsCanonicalCeramicBearingVisible =>
            IsActive(_canonicalCeramicBearing);

        public bool IsCanonicalFieldSleeveVisible =>
            IsActive(_canonicalFieldSleeve);

        public bool IsRoadVehicleCargoVisible =>
            IsActive(_roadCeramicBearing) || IsActive(_roadFieldSleeve);

        public bool IsRoadCeramicBearingVisible =>
            IsActive(_roadCeramicBearing);

        public bool IsRoadFieldSleeveVisible =>
            IsActive(_roadFieldSleeve);

        public bool IsLoadAvailable =>
            State == DepotCargoLoadingPresentationState.CeramicBearingAtDepot ||
            State == DepotCargoLoadingPresentationState.CeramicBearingAtFaction ||
            State == DepotCargoLoadingPresentationState.FieldSleeveAtFaction;

        public void Build(
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
            _bone = bone ?? throw new ArgumentNullException(nameof(bone));
            _oxide = oxide ?? throw new ArgumentNullException(nameof(oxide));
            _signal = signal ?? throw new ArgumentNullException(nameof(signal));

            InteractionAnchor = new GameObject(InteractionAnchorName).transform;
            InteractionAnchor.SetParent(transform, false);
            InteractionAnchor.localPosition = CeramicSourcePosition;

            CreatePrimitive(
                "Depot Cargo Safe Line",
                PrimitiveType.Cube,
                transform,
                new Vector3(0f, 0.06f, -0.15f),
                new Vector3(5.8f, 0.08f, 0.14f),
                bone,
                Quaternion.identity);
            CreatePrimitive(
                "Faction Service Stand",
                PrimitiveType.Cube,
                transform,
                new Vector3(2.5f, 0.62f, -0.9f),
                new Vector3(1.7f, 1.24f, 1.35f),
                iron,
                Quaternion.identity);

            _ceramicBearingAtDepot = CreateCeramicBearing(
                "Unclaimed Ceramic Bearing At Depot Cradle",
                transform,
                CeramicSourcePosition,
                bone,
                signal);
            _ceramicBearingAtFaction = CreateCeramicBearing(
                "Faction-Held Ceramic Bearing At Service Stand",
                transform,
                FactionSourcePosition,
                bone,
                signal);
            _fieldSleeveAtFaction = CreateFieldSleeve(
                "Faction Field Sleeve At Service Stand",
                transform,
                FactionSourcePosition,
                oxide,
                bone);

            var lightObject = new GameObject("Depot Load Tungsten Practical");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 3.1f, -0.2f);
            _workLight = lightObject.AddComponent<Light>();
            _workLight.type = LightType.Point;
            _workLight.color = tungsten.color;
            _workLight.range = 8f;
            _workLight.shadows = LightShadows.None;

            Apply(RepairCargoKind.None, RepairCargoCustody.None);
        }

        public void BindVehicleCargoSockets(
            Transform canonicalCargoSocket,
            Transform roadCargoSocket)
        {
            if (_vehicleCargoBuilt)
            {
                return;
            }

            if (!_built || _bone == null || _oxide == null || _signal == null)
            {
                throw new InvalidOperationException(
                    "Depot cargo view must be built before vehicle sockets bind.");
            }

            if (canonicalCargoSocket == null)
            {
                throw new ArgumentNullException(nameof(canonicalCargoSocket));
            }

            if (roadCargoSocket == null)
            {
                throw new ArgumentNullException(nameof(roadCargoSocket));
            }

            _vehicleCargoBuilt = true;
            _canonicalCeramicBearing = CreateCeramicBearing(
                "Canonical Scout Ceramic Bearing Load",
                canonicalCargoSocket,
                Vector3.zero,
                _bone,
                _signal);
            _canonicalFieldSleeve = CreateFieldSleeve(
                "Canonical Scout Field Sleeve Load",
                canonicalCargoSocket,
                Vector3.zero,
                _oxide,
                _bone);

            _roadCeramicBearing = CreateCeramicBearing(
                "Road Scout Ceramic Bearing Load",
                roadCargoSocket,
                Vector3.zero,
                _bone,
                _signal);
            _roadFieldSleeve = CreateFieldSleeve(
                "Road Scout Field Sleeve Load",
                roadCargoSocket,
                Vector3.zero,
                _oxide,
                _bone);

            ApplyVisibility(State);
        }

        public void Apply(RepairCargoKind kind, RepairCargoCustody custody)
        {
            if (!Enum.IsDefined(typeof(RepairCargoKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (!Enum.IsDefined(typeof(RepairCargoCustody), custody))
            {
                throw new ArgumentOutOfRangeException(nameof(custody));
            }

            State = ResolveState(kind, custody);
            ApplyVisibility(State);
        }

        private void ApplyVisibility(DepotCargoLoadingPresentationState state)
        {
            SetActive(
                _ceramicBearingAtDepot,
                state == DepotCargoLoadingPresentationState.CeramicBearingAtDepot);
            SetActive(
                _ceramicBearingAtFaction,
                state == DepotCargoLoadingPresentationState.CeramicBearingAtFaction);
            SetActive(
                _fieldSleeveAtFaction,
                state == DepotCargoLoadingPresentationState.FieldSleeveAtFaction);
            SetActive(
                _canonicalCeramicBearing,
                state == DepotCargoLoadingPresentationState.CeramicBearingOnVehicle);
            SetActive(
                _canonicalFieldSleeve,
                state == DepotCargoLoadingPresentationState.FieldSleeveOnVehicle);
            SetActive(
                _roadCeramicBearing,
                state == DepotCargoLoadingPresentationState.CeramicBearingOnVehicle);
            SetActive(
                _roadFieldSleeve,
                state == DepotCargoLoadingPresentationState.FieldSleeveOnVehicle);

            if (InteractionAnchor != null)
            {
                bool sourceAtFaction = state ==
                    DepotCargoLoadingPresentationState.CeramicBearingAtFaction ||
                    state == DepotCargoLoadingPresentationState.FieldSleeveAtFaction;
                InteractionAnchor.localPosition = sourceAtFaction
                        ? FactionSourcePosition
                        : CeramicSourcePosition;
            }

            if (_workLight != null)
            {
                bool loaded = state ==
                    DepotCargoLoadingPresentationState.CeramicBearingOnVehicle ||
                    state == DepotCargoLoadingPresentationState.FieldSleeveOnVehicle;
                _workLight.color = loaded
                    ? new Color32(92, 199, 208, 255)
                    : new Color32(255, 196, 107, 255);
                _workLight.intensity = IsLoadAvailable
                    ? 520f
                    : loaded
                        ? 180f
                        : 35f;
            }
        }

        private static DepotCargoLoadingPresentationState ResolveState(
            RepairCargoKind kind,
            RepairCargoCustody custody)
        {
            if (kind == RepairCargoKind.None && custody == RepairCargoCustody.None)
            {
                return DepotCargoLoadingPresentationState.Dormant;
            }

            if (kind == RepairCargoKind.CeramicBearing)
            {
                return custody switch
                {
                    RepairCargoCustody.Depot =>
                        DepotCargoLoadingPresentationState.CeramicBearingAtDepot,
                    RepairCargoCustody.Faction =>
                        DepotCargoLoadingPresentationState.CeramicBearingAtFaction,
                    RepairCargoCustody.Vehicle =>
                        DepotCargoLoadingPresentationState.CeramicBearingOnVehicle,
                    RepairCargoCustody.Turbine =>
                        DepotCargoLoadingPresentationState.Applied,
                    _ => throw new InvalidOperationException(
                        "LAST_BEARING_DEPOT_CARGO_PRESENTATION_INVALID"),
                };
            }

            if (kind == RepairCargoKind.FieldSleeve)
            {
                return custody switch
                {
                    RepairCargoCustody.Faction =>
                        DepotCargoLoadingPresentationState.FieldSleeveAtFaction,
                    RepairCargoCustody.Vehicle =>
                        DepotCargoLoadingPresentationState.FieldSleeveOnVehicle,
                    RepairCargoCustody.Consumed =>
                        DepotCargoLoadingPresentationState.Applied,
                    _ => throw new InvalidOperationException(
                        "LAST_BEARING_DEPOT_CARGO_PRESENTATION_INVALID"),
                };
            }

            throw new InvalidOperationException(
                "LAST_BEARING_DEPOT_CARGO_PRESENTATION_INVALID");
        }

        private static GameObject CreateCeramicBearing(
            string name,
            Transform parent,
            Vector3 position,
            Material bone,
            Material signal)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            GameObject race = CreatePrimitive(
                "Ceramic Bearing Race",
                PrimitiveType.Cylinder,
                root.transform,
                Vector3.zero,
                new Vector3(0.54f, 0.22f, 0.54f),
                bone,
                Quaternion.Euler(90f, 0f, 0f));
            CreatePrimitive(
                "Ceramic Bearing Custody Pin",
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, 0.02f, -0.24f),
                new Vector3(0.13f, 0.25f, 0.13f),
                signal,
                Quaternion.Euler(90f, 0f, 0f));
            race.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return root;
        }

        private static GameObject CreateFieldSleeve(
            string name,
            Transform parent,
            Vector3 position,
            Material oxide,
            Material bone)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            CreatePrimitive(
                "Field Sleeve Body",
                PrimitiveType.Cylinder,
                root.transform,
                Vector3.zero,
                new Vector3(0.35f, 0.62f, 0.35f),
                oxide,
                Quaternion.Euler(0f, 0f, 90f));
            CreatePrimitive(
                "Field Sleeve Service Wrap",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0f, 0.28f, 0f),
                new Vector3(0.82f, 0.12f, 0.62f),
                bone,
                Quaternion.Euler(0f, 18f, 0f));
            return root;
        }

        private static GameObject CreatePrimitive(
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

        private static bool IsActive(GameObject? value)
        {
            return value != null && value.activeSelf;
        }

        private static void SetActive(GameObject? value, bool active)
        {
            value?.SetActive(active);
        }

        private static Vector3 CeramicSourcePosition =>
            new Vector3(0f, 1.72f, 1.4f);

        private static Vector3 FactionSourcePosition =>
            new Vector3(2.5f, 1.52f, -0.9f);
    }
}
