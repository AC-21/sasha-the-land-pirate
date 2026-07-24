#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Vehicle
{
    public readonly struct SashaScoutBlockoutMaterials
    {
        public SashaScoutBlockoutMaterials(
            Material iron,
            Material oxide,
            Material bone,
            Material rubber,
            Material tungsten,
            Material signal)
        {
            Iron = iron ?? throw new ArgumentNullException(nameof(iron));
            Oxide = oxide ?? throw new ArgumentNullException(nameof(oxide));
            Bone = bone ?? throw new ArgumentNullException(nameof(bone));
            Rubber = rubber ?? throw new ArgumentNullException(nameof(rubber));
            Tungsten = tungsten ?? throw new ArgumentNullException(nameof(tungsten));
            Signal = signal ?? throw new ArgumentNullException(nameof(signal));
        }

        public Material Iron { get; }
        public Material Oxide { get; }
        public Material Bone { get; }
        public Material Rubber { get; }
        public Material Tungsten { get; }
        public Material Signal { get; }
    }

    /// <summary>
    /// Builds the shared, inspectable C0 scout blockout. Detailed render
    /// geometry never owns collision; the Road Feel instance receives a
    /// separate primitive collision shell with the existing dimensions.
    /// </summary>
    public static class SashaScoutBlockoutFactory
    {
        public const int RoadCollisionBoxCount = 7;

        public static SashaScoutVisual Create(
            Transform parent,
            SashaScoutBlockoutMaterials materials,
            bool includeRoadCollisionShell)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var root = new GameObject(SashaScoutSemanticContract.RootName);
            root.transform.SetParent(parent, false);
            var visual = root.AddComponent<SashaScoutVisual>();

            Transform geometryRoot = CreateRoot(
                SashaScoutSemanticContract.GeometryRootName,
                root.transform);
            Transform lod0Root = CreateRoot(
                SashaScoutSemanticContract.Lod0RootName,
                geometryRoot);
            Transform lod1Root = CreateRoot(
                SashaScoutSemanticContract.Lod1RootName,
                geometryRoot);
            Transform lod2Root = CreateRoot(
                SashaScoutSemanticContract.Lod2RootName,
                geometryRoot);
            Transform rigRoot = CreateRoot(
                SashaScoutSemanticContract.RigRootName,
                root.transform);
            Transform contactsRoot = CreateRoot(
                SashaScoutSemanticContract.ContactsRootName,
                rigRoot);
            Transform frontAxle = CreateRoot(
                SashaScoutSemanticContract.FrontAxleName,
                rigRoot);
            Transform rearAxle = CreateRoot(
                SashaScoutSemanticContract.RearAxleName,
                rigRoot);
            Transform socketsRoot = CreateRoot(
                SashaScoutSemanticContract.SocketsRootName,
                root.transform);
            Transform modulesRoot = CreateRoot(
                SashaScoutSemanticContract.ModulesRootName,
                root.transform);
            Transform collisionRoot = CreateRoot(
                SashaScoutSemanticContract.CollisionRootName,
                root.transform);

            BuildBody(lod0Root, materials);
            BuildSockets(socketsRoot);

            var contactStations = new Transform[SashaScoutSemanticContract.WheelCount];
            var wheelVisuals = new Transform[SashaScoutSemanticContract.WheelCount];
            var wheelPivots = new Transform[SashaScoutSemanticContract.WheelCount];
            for (var index = 0;
                 index < SashaScoutSemanticContract.WheelCount;
                 index++)
            {
                Vector3 position =
                    SashaScoutSemanticContract.GetContactStationLocalPosition(index);
                Transform station = CreateRoot(
                    SashaScoutSemanticContract.GetContactStationName(index),
                    contactsRoot);
                station.localPosition = position;
                contactStations[index] = station;

                Transform axle = index < SashaScoutSemanticContract.FrontWheelCount
                    ? frontAxle
                    : rearAxle;
                Transform pivot = CreateRoot(
                    SashaScoutSemanticContract.GetWheelPivotName(index),
                    axle);
                pivot.localPosition = position;
                wheelPivots[index] = pivot;

                GameObject wheel = CreateRenderPrimitive(
                    SashaScoutSemanticContract.GetWheelName(index),
                    PrimitiveType.Cylinder,
                    pivot,
                    Vector3.zero,
                    new Vector3(0.82f, 0.3f, 0.82f),
                    Quaternion.Euler(0f, 0f, 90f),
                    materials.Rubber);
                wheelVisuals[index] = wheel.transform;
                CreateRenderPrimitive(
                    "HUBCAP_" + index.ToString("00"),
                    PrimitiveType.Cylinder,
                    wheel.transform,
                    Vector3.zero,
                    new Vector3(0.68f, 0.34f, 0.68f),
                    Quaternion.identity,
                    index < 2 ? materials.Bone : materials.Oxide);
            }

            Transform driverDoor = CreateRoot(
                SashaScoutSemanticContract.DriverDoorTransformName,
                rigRoot);
            driverDoor.localPosition = new Vector3(-1.08f, 1.45f, 0.38f);

            Transform winchSocket = socketsRoot.Find(
                SashaScoutSemanticContract.FrontUpgradeSocketName)!;
            Transform rangeTankSocket = socketsRoot.Find(
                SashaScoutSemanticContract.CargoUpgradeSocketName)!;
            Transform underbodyUpgradeSocket = socketsRoot.Find(
                SashaScoutSemanticContract.UnderbodyUpgradeSocketName)!;
            Transform winch = BuildWinchModule(
                modulesRoot,
                winchSocket,
                materials);
            Transform tank = BuildRangeTankModule(
                modulesRoot,
                rangeTankSocket,
                materials);
            Transform patchworkSkidPlate = BuildPatchworkSkidPlateUpgrade(
                modulesRoot,
                underbodyUpgradeSocket,
                materials);
            if (includeRoadCollisionShell)
            {
                BuildRoadCollisionShell(collisionRoot);
            }

            visual.Configure(
                materials,
                geometryRoot,
                lod0Root,
                lod1Root,
                lod2Root,
                rigRoot,
                socketsRoot,
                modulesRoot,
                collisionRoot,
                contactStations,
                wheelVisuals,
                wheelPivots,
                winch,
                tank,
                patchworkSkidPlate);
            return visual;
        }

        private static void BuildBody(
            Transform root,
            SashaScoutBlockoutMaterials materials)
        {
            CreateRenderPrimitive(
                "BOXED_IRON_CHASSIS",
                PrimitiveType.Cube,
                root,
                new Vector3(0f, 0.72f, 0f),
                new Vector3(2.25f, 0.48f, 4.7f),
                Quaternion.identity,
                materials.Iron);
            CreateRenderPrimitive(
                "LOW_OXIDE_CAB",
                PrimitiveType.Cube,
                root,
                new Vector3(0f, 1.42f, 0.42f),
                new Vector3(2.08f, 0.96f, 2.05f),
                Quaternion.identity,
                materials.Oxide);
            CreateRenderPrimitive(
                "BONE_ENAMEL_HOOD_PATCH",
                PrimitiveType.Cube,
                root,
                new Vector3(0f, 1.05f, 1.66f),
                new Vector3(2.04f, 0.38f, 1.2f),
                Quaternion.Euler(-3f, 0f, 0f),
                materials.Bone);
            CreateRenderPrimitive(
                "UTILITY_BED",
                PrimitiveType.Cube,
                root,
                new Vector3(0f, 1.02f, -1.42f),
                new Vector3(2.18f, 0.28f, 1.55f),
                Quaternion.identity,
                materials.Iron);
            CreateRenderPrimitive(
                "FRONT_RECOVERY_RAM",
                PrimitiveType.Cube,
                root,
                new Vector3(0f, 0.58f, 2.55f),
                new Vector3(2.75f, 0.3f, 0.32f),
                Quaternion.identity,
                materials.Oxide);
            CreateRenderPrimitive(
                "LEFT_BED_RAIL",
                PrimitiveType.Cube,
                root,
                new Vector3(-1.02f, 1.5f, -1.42f),
                new Vector3(0.12f, 0.95f, 1.72f),
                Quaternion.identity,
                materials.Bone);
            CreateRenderPrimitive(
                "RIGHT_BED_RAIL",
                PrimitiveType.Cube,
                root,
                new Vector3(1.02f, 1.5f, -1.42f),
                new Vector3(0.12f, 0.95f, 1.72f),
                Quaternion.identity,
                materials.Bone);

            for (var index = 0; index < SashaScoutSemanticContract.WheelCount; index++)
            {
                Vector3 station =
                    SashaScoutSemanticContract.GetContactStationLocalPosition(index);
                CreateRenderPrimitive(
                    "FIELD_FENDER_" + index.ToString("00"),
                    PrimitiveType.Cube,
                    root,
                    station + new Vector3(0f, 0.42f, 0f),
                    new Vector3(0.22f, 0.32f, 1.18f),
                    Quaternion.identity,
                    index < 2 ? materials.Oxide : materials.Iron);
            }

            CreateHeadlight(root, -0.72f, materials.Tungsten);
            CreateHeadlight(root, 0.72f, materials.Tungsten);
            CreateRenderPrimitive(
                "SCOUT_CONDITION_TELL_TALE",
                PrimitiveType.Sphere,
                root,
                new Vector3(0f, 2.18f, 0.35f),
                Vector3.one * 0.25f,
                Quaternion.identity,
                materials.Signal);
        }

        private static void BuildSockets(Transform root)
        {
            CreateSocket(
                SashaScoutSemanticContract.FrontUpgradeSocketName,
                root,
                new Vector3(0f, 0.62f, 2.62f));
            CreateSocket(
                SashaScoutSemanticContract.CargoUpgradeSocketName,
                root,
                new Vector3(0f, 1.42f, -1.38f));
            CreateSocket(
                SashaScoutSemanticContract.UnderbodyUpgradeSocketName,
                root,
                new Vector3(0f, 0.39f, 0.08f));
            CreateSocket(
                SashaScoutSemanticContract.CargoSocket01Name,
                root,
                new Vector3(-0.47f, 1.42f, -1.45f));
            CreateSocket(
                SashaScoutSemanticContract.CargoSocket02Name,
                root,
                new Vector3(0.47f, 1.42f, -1.45f));
            CreateSocket(
                SashaScoutSemanticContract.ToolDeploySocketName,
                root,
                new Vector3(0f, 0.82f, 2.8f));
            CreateSocket(
                SashaScoutSemanticContract.DriverCameraSocketName,
                root,
                new Vector3(0f, 2.08f, -0.25f));
        }

        private static Transform BuildWinchModule(
            Transform root,
            Transform socket,
            SashaScoutBlockoutMaterials materials)
        {
            Transform module = CreateRoot(
                SashaScoutSemanticContract.WinchModuleName,
                root);
            CopySocketPose(module, socket);
            CreateRenderPrimitive(
                "WINCH_STEEL_JAW",
                PrimitiveType.Cube,
                module,
                new Vector3(0f, 0f, 0.1f),
                new Vector3(1.9f, 0.45f, 0.75f),
                Quaternion.identity,
                materials.Iron);
            CreateRenderPrimitive(
                "WINCH_CABLE_DRUM",
                PrimitiveType.Cylinder,
                module,
                new Vector3(0f, 0.16f, -0.18f),
                new Vector3(0.48f, 0.8f, 0.48f),
                Quaternion.Euler(0f, 0f, 90f),
                materials.Rubber);
            return module;
        }

        private static Transform BuildRangeTankModule(
            Transform root,
            Transform socket,
            SashaScoutBlockoutMaterials materials)
        {
            Transform module = CreateRoot(
                SashaScoutSemanticContract.RangeTankModuleName,
                root);
            CopySocketPose(module, socket);
            CreateRenderPrimitive(
                "SEALED_RANGE_TANK",
                PrimitiveType.Cylinder,
                module,
                new Vector3(0f, 0.23f, 0.33f),
                new Vector3(1.05f, 1.45f, 1.05f),
                Quaternion.Euler(0f, 0f, 90f),
                materials.Bone);
            CreateRenderPrimitive(
                "TANK_SERVICE_BAND",
                PrimitiveType.Cylinder,
                module,
                new Vector3(0f, 0.23f, 0.33f),
                new Vector3(1.1f, 0.28f, 1.1f),
                Quaternion.Euler(0f, 0f, 90f),
                materials.Oxide);
            return module;
        }

        private static Transform BuildPatchworkSkidPlateUpgrade(
            Transform root,
            Transform socket,
            SashaScoutBlockoutMaterials materials)
        {
            Transform upgrade = CreateRoot(
                SashaScoutSemanticContract.PatchworkSkidPlateUpgradeName,
                root);
            CopySocketPose(upgrade, socket);
            CreateRenderPrimitive(
                "SKID_PLATE_CENTER",
                PrimitiveType.Cube,
                upgrade,
                Vector3.zero,
                new Vector3(1.68f, 0.1f, 1.42f),
                Quaternion.identity,
                materials.Iron);
            CreateRenderPrimitive(
                "SKID_PLATE_FORE",
                PrimitiveType.Cube,
                upgrade,
                new Vector3(0f, 0.04f, 1.02f),
                new Vector3(1.5f, 0.1f, 0.72f),
                Quaternion.Euler(-8f, 0f, 0f),
                materials.Bone);
            CreateRenderPrimitive(
                "SKID_PLATE_AFT",
                PrimitiveType.Cube,
                upgrade,
                new Vector3(0f, 0.04f, -1.02f),
                new Vector3(1.5f, 0.1f, 0.72f),
                Quaternion.Euler(8f, 0f, 0f),
                materials.Oxide);
            CreateRenderPrimitive(
                "SKID_BRACE_LEFT",
                PrimitiveType.Cube,
                upgrade,
                new Vector3(-0.74f, 0.13f, 0f),
                new Vector3(0.12f, 0.18f, 2.38f),
                Quaternion.identity,
                materials.Oxide);
            CreateRenderPrimitive(
                "SKID_BRACE_RIGHT",
                PrimitiveType.Cube,
                upgrade,
                new Vector3(0.74f, 0.13f, 0f),
                new Vector3(0.12f, 0.18f, 2.38f),
                Quaternion.identity,
                materials.Oxide);
            return upgrade;
        }

        private static void CopySocketPose(
            Transform module,
            Transform socket)
        {
            module.position = socket.position;
            module.rotation = socket.rotation;
        }

        private static void BuildRoadCollisionShell(Transform root)
        {
            CreateCollisionBox(
                "COL_BOXED_IRON_CHASSIS",
                root,
                new Vector3(0f, 0.72f, 0f),
                new Vector3(2.25f, 0.48f, 4.7f),
                Quaternion.identity);
            CreateCollisionBox(
                "COL_LOW_OXIDE_CAB",
                root,
                new Vector3(0f, 1.42f, 0.42f),
                new Vector3(2.08f, 0.96f, 2.05f),
                Quaternion.identity);
            CreateCollisionBox(
                "COL_BONE_ENAMEL_HOOD_PATCH",
                root,
                new Vector3(0f, 1.05f, 1.66f),
                new Vector3(2.04f, 0.38f, 1.2f),
                Quaternion.Euler(-3f, 0f, 0f));
            CreateCollisionBox(
                "COL_UTILITY_BED",
                root,
                new Vector3(0f, 1.02f, -1.42f),
                new Vector3(2.18f, 0.28f, 1.55f),
                Quaternion.identity);
            CreateCollisionBox(
                "COL_FRONT_RECOVERY_RAM",
                root,
                new Vector3(0f, 0.58f, 2.55f),
                new Vector3(2.75f, 0.3f, 0.32f),
                Quaternion.identity);
            CreateCollisionBox(
                "COL_LEFT_BED_RAIL",
                root,
                new Vector3(-1.02f, 1.5f, -1.42f),
                new Vector3(0.12f, 0.95f, 1.72f),
                Quaternion.identity);
            CreateCollisionBox(
                "COL_RIGHT_BED_RAIL",
                root,
                new Vector3(1.02f, 1.5f, -1.42f),
                new Vector3(0.12f, 0.95f, 1.72f),
                Quaternion.identity);
        }

        private static void CreateHeadlight(
            Transform parent,
            float x,
            Material material)
        {
            CreateRenderPrimitive(
                x < 0f ? "TUNGSTEN_HEADLIGHT_LEFT" : "TUNGSTEN_HEADLIGHT_RIGHT",
                PrimitiveType.Cylinder,
                parent,
                new Vector3(x, 1.03f, 2.42f),
                new Vector3(0.28f, 0.12f, 0.28f),
                Quaternion.Euler(90f, 0f, 0f),
                material);
        }

        private static Transform CreateRoot(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static void CreateSocket(
            string name,
            Transform parent,
            Vector3 localPosition)
        {
            Transform socket = CreateRoot(name, parent);
            socket.localPosition = localPosition;
        }

        private static GameObject CreateRenderPrimitive(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.transform.localRotation = localRotation;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return primitive;
        }

        private static void CreateCollisionBox(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation)
        {
            var collision = new GameObject(name);
            collision.transform.SetParent(parent, false);
            collision.transform.localPosition = localPosition;
            collision.transform.localScale = localScale;
            collision.transform.localRotation = localRotation;
            collision.AddComponent<BoxCollider>();
        }
    }
}
