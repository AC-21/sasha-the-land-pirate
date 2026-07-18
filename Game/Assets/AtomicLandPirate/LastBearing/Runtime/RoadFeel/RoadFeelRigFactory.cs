#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    public readonly struct RoadFeelRigMaterials
    {
        public RoadFeelRigMaterials(
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material damageLamp)
        {
            Iron = iron ?? throw new ArgumentNullException(nameof(iron));
            Oxide = oxide ?? throw new ArgumentNullException(nameof(oxide));
            Bone = bone ?? throw new ArgumentNullException(nameof(bone));
            Tungsten = tungsten ?? throw new ArgumentNullException(nameof(tungsten));
            DamageLamp = damageLamp ??
                         throw new ArgumentNullException(nameof(damageLamp));
        }

        public Material Iron { get; }
        public Material Oxide { get; }
        public Material Bone { get; }
        public Material Tungsten { get; }
        public Material DamageLamp { get; }
    }

    public sealed class RoadFeelRigInstance
    {
        internal RoadFeelRigInstance(
            GameObject root,
            RoadFeelVehicleController vehicle,
            LastBearingRoadFeelModeAdapter adapter,
            IReadOnlyList<GameObject> cargoVisuals)
        {
            Root = root;
            Vehicle = vehicle;
            Adapter = adapter;
            CargoVisuals = cargoVisuals;
        }

        public GameObject Root { get; }

        public RoadFeelVehicleController Vehicle { get; }

        public LastBearingRoadFeelModeAdapter Adapter { get; }

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
            CreateBlock(
                "Boxed Iron Chassis",
                rig.transform,
                new Vector3(0f, 0.72f, 0f),
                new Vector3(2.25f, 0.48f, 4.7f),
                materials.Iron,
                Quaternion.identity);
            CreateBlock(
                "Oxide Cab",
                rig.transform,
                new Vector3(0f, 1.42f, 0.42f),
                new Vector3(2.08f, 0.96f, 2.05f),
                materials.Oxide,
                Quaternion.identity);
            CreateBlock(
                "Bone Hood Patch",
                rig.transform,
                new Vector3(0f, 1.05f, 1.66f),
                new Vector3(2.04f, 0.38f, 1.2f),
                materials.Bone,
                Quaternion.Euler(-3f, 0f, 0f));
            CreateBlock(
                "Utility Bed",
                rig.transform,
                new Vector3(0f, 1.02f, -1.42f),
                new Vector3(2.18f, 0.28f, 1.55f),
                materials.Iron,
                Quaternion.identity);
            CreateBlock(
                "Front Ram",
                rig.transform,
                new Vector3(0f, 0.58f, 2.55f),
                new Vector3(2.75f, 0.3f, 0.32f),
                materials.Oxide,
                Quaternion.identity);
            CreateBlock(
                "Left Bed Rail",
                rig.transform,
                new Vector3(-1.02f, 1.5f, -1.42f),
                new Vector3(0.12f, 0.95f, 1.72f),
                materials.Bone,
                Quaternion.identity);
            CreateBlock(
                "Right Bed Rail",
                rig.transform,
                new Vector3(1.02f, 1.5f, -1.42f),
                new Vector3(0.12f, 0.95f, 1.72f),
                materials.Bone,
                Quaternion.identity);

            CreateHeadlight(rig.transform, -0.72f, materials.Tungsten);
            CreateHeadlight(rig.transform, 0.72f, materials.Tungsten);
            CreateSphere(
                "Rig Condition Tell-Tale",
                rig.transform,
                new Vector3(0f, 2.18f, 0.35f),
                Vector3.one * 0.25f,
                materials.DamageLamp,
                disableCollider: true);

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

            var stationPositions = new[]
            {
                new Vector3(-1.12f, 0.62f, 1.55f),
                new Vector3(1.12f, 0.62f, 1.55f),
                new Vector3(-1.12f, 0.62f, -1.55f),
                new Vector3(1.12f, 0.62f, -1.55f),
            };
            var contactStations = new Transform[4];
            var wheelVisuals = new Transform[4];
            var steeringPivots = new Transform[4];
            for (var index = 0; index < stationPositions.Length; index++)
            {
                var station = new GameObject("Contact Station " + index).transform;
                station.SetParent(rig.transform, false);
                station.localPosition = stationPositions[index];
                contactStations[index] = station;

                var pivot = new GameObject("Steering Pivot " + index).transform;
                pivot.SetParent(rig.transform, false);
                pivot.localPosition = stationPositions[index];
                steeringPivots[index] = pivot;

                GameObject wheel = CreateCylinder(
                    "Utility Wheel " + index,
                    pivot,
                    Vector3.zero,
                    new Vector3(0.82f, 0.3f, 0.82f),
                    materials.Iron,
                    disableCollider: true);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheelVisuals[index] = wheel.transform;
            }

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
                cargoVisuals);
        }

        private static void CreateHeadlight(
            Transform parent,
            float x,
            Material tungsten)
        {
            GameObject light = CreateCylinder(
                "Tungsten Headlight " + (x < 0f ? "Left" : "Right"),
                parent,
                new Vector3(x, 1.03f, 2.42f),
                new Vector3(0.28f, 0.12f, 0.28f),
                tungsten,
                disableCollider: true);
            light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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

        private static GameObject CreateCylinder(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool disableCollider)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localScale = scale;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            if (disableCollider)
            {
                cylinder.GetComponent<Collider>().enabled = false;
            }

            return cylinder;
        }

        private static GameObject CreateSphere(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool disableCollider)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = position;
            sphere.transform.localScale = scale;
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            if (disableCollider)
            {
                sphere.GetComponent<Collider>().enabled = false;
            }

            return sphere;
        }
    }
}
