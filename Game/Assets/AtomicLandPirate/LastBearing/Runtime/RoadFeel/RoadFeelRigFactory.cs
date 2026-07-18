#nullable enable

using System;
using System.Collections.Generic;
using AtomicLandPirate.Presentation.LastBearing.Vehicle;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    public readonly struct RoadFeelRigMaterials
    {
        public RoadFeelRigMaterials(
            Material iron,
            Material oxide,
            Material bone,
            Material rubber,
            Material tungsten,
            Material damageLamp)
        {
            Iron = iron ?? throw new ArgumentNullException(nameof(iron));
            Oxide = oxide ?? throw new ArgumentNullException(nameof(oxide));
            Bone = bone ?? throw new ArgumentNullException(nameof(bone));
            Rubber = rubber ?? throw new ArgumentNullException(nameof(rubber));
            Tungsten = tungsten ?? throw new ArgumentNullException(nameof(tungsten));
            DamageLamp = damageLamp ??
                         throw new ArgumentNullException(nameof(damageLamp));
        }

        public Material Iron { get; }
        public Material Oxide { get; }
        public Material Bone { get; }
        public Material Rubber { get; }
        public Material Tungsten { get; }
        public Material DamageLamp { get; }
    }

    public sealed class RoadFeelRigInstance
    {
        internal RoadFeelRigInstance(
            GameObject root,
            RoadFeelVehicleController vehicle,
            LastBearingRoadFeelModeAdapter adapter,
            SashaScoutVisual scoutVisual,
            IReadOnlyList<GameObject> cargoVisuals)
        {
            Root = root;
            Vehicle = vehicle;
            Adapter = adapter;
            ScoutVisual = scoutVisual;
            CargoVisuals = cargoVisuals;
        }

        public GameObject Root { get; }

        public RoadFeelVehicleController Vehicle { get; }

        public LastBearingRoadFeelModeAdapter Adapter { get; }

        public SashaScoutVisual ScoutVisual { get; }

        public IReadOnlyList<GameObject> CargoVisuals { get; }
    }

    /// <summary>
    /// Builds the single primitive Road Feel rig shared by the standalone lab
    /// and the Last Bearing presentation. It creates no camera, reads no input,
    /// and owns no canonical or save state.
    /// </summary>
    public static class RoadFeelRigFactory
    {
        public const string RigName = "Sasha's Planted Utility Rig";

        public static RoadFeelRigInstance Create(
            Transform parent,
            Vector3 worldPosition,
            Quaternion worldRotation,
            RoadFeelRigMaterials materials)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var rig = new GameObject(RigName);
            rig.transform.SetParent(parent, false);
            rig.transform.SetPositionAndRotation(worldPosition, worldRotation);

            var controller = rig.AddComponent<RoadFeelVehicleController>();
            SashaScoutVisual scoutVisual = SashaScoutBlockoutFactory.Create(
                rig.transform,
                new SashaScoutBlockoutMaterials(
                    materials.Iron,
                    materials.Oxide,
                    materials.Bone,
                    materials.Rubber,
                    materials.Tungsten,
                    materials.DamageLamp),
                includeRoadCollisionShell: true);

            var cargoVisuals = new List<GameObject>
            {
                CreateBlock(
                    "Cargo Crate 650kg",
                    rig.transform,
                    new Vector3(-0.47f, 1.42f, -1.45f),
                    new Vector3(0.82f, 0.7f, 1.05f),
                    materials.Oxide,
                    Quaternion.identity),
                CreateBlock(
                    "Cargo Crate 1300kg",
                    rig.transform,
                    new Vector3(0.47f, 1.42f, -1.45f),
                    new Vector3(0.82f, 0.7f, 1.05f),
                    materials.Bone,
                    Quaternion.identity),
            };

            Transform[] contactStations = scoutVisual.CopyContactStations();
            Transform[] wheelVisuals = scoutVisual.CopyWheelVisuals();
            Transform[] steeringPivots = scoutVisual.CopyWheelPivots();

            controller.Initialize(contactStations, wheelVisuals, steeringPivots);
            controller.SetLoad(0f, RoadFeelDamageBand.Healthy);
            controller.ResetAt(worldPosition, worldRotation);
            var adapter = rig.AddComponent<LastBearingRoadFeelModeAdapter>();
            adapter.Configure(controller);
            adapter.SetRoadModeActive(false);
            return new RoadFeelRigInstance(
                rig,
                controller,
                adapter,
                scoutVisual,
                cargoVisuals);
        }

        private static GameObject CreateBlock(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion rotation)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localRotation = rotation;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

    }
}
