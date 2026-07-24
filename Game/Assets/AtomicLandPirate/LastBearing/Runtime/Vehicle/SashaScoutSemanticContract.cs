#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Vehicle
{
    public enum SashaScoutModulePresentation
    {
        None = 0,
        WinchAssembly = 1,
        SealedRangeTank = 2,
    }

    /// <summary>
    /// C0 direction contract for Sasha's first scout. The procedural geometry
    /// is an inspectable blockout, not production art or an accepted asset.
    /// Stable transforms here are shared by the city and Road Feel views.
    /// </summary>
    public static class SashaScoutSemanticContract
    {
        public const string DirectionPackageId = "C0-VGR-01";
        public const string ContentId = "veh_sasha_scout_a";
        public const string Stage = "C0Blockout";
        public const string RootName = "veh_sasha_scout_a [C0 Blockout]";

        public const int WheelCount = 4;
        public const int FrontWheelCount = 2;

        public const int Lod0BaseMinimumTriangles = 24_000;
        public const int Lod0BaseMaximumTriangles = 28_000;
        public const int Lod0WithModuleMaximumTriangles = 32_000;
        public const int Lod1MinimumTriangles = 9_000;
        public const int Lod1MaximumTriangles = 12_000;
        public const int Lod2MinimumTriangles = 3_000;
        public const int Lod2MaximumTriangles = 5_000;
        public const int ProductionMaterialSlotMaximum = 3;
        public const int ProductionTextureSetSize = 2_048;

        public const string GeometryRootName = "GEO";
        public const string Lod0RootName = "LOD0_C0_BLOCKOUT";
        public const string Lod1RootName = "LOD1_RESERVED";
        public const string Lod2RootName = "LOD2_RESERVED";
        public const string RigRootName = "RIG";
        public const string ContactsRootName = "CONTACTS";
        public const string FrontAxleName = "AXLE_FRONT";
        public const string RearAxleName = "AXLE_REAR";
        public const string SocketsRootName = "SOCKETS";
        public const string ModulesRootName = "MODULES";
        public const string CollisionRootName = "COLLISION";

        public const string FrontUpgradeSocketName = "SOCKET_UPGRADE_FRONT";
        public const string CargoUpgradeSocketName = "SOCKET_UPGRADE_CARGO_01";
        public const string UnderbodyUpgradeSocketName =
            "SOCKET_UPGRADE_UNDERBODY";
        public const string CargoSocket01Name = "SOCKET_CARGO_01";
        public const string CargoSocket02Name = "SOCKET_CARGO_02";
        public const string ToolDeploySocketName = "SOCKET_TOOL_DEPLOY";
        public const string DriverCameraSocketName = "SOCKET_DRIVER_CAMERA";
        public const string DriverDoorTransformName = "DOOR_DRIVER";
        public const string WinchModuleName = "MODULE_WINCH_ASSEMBLY";
        public const string RangeTankModuleName = "MODULE_SEALED_RANGE_TANK";
        public const string PatchworkSkidPlateUpgradeName =
            "UPGRADE_PATCHWORK_SKID_PLATE";

        public static Vector3 ForwardAxis => Vector3.forward;

        public static Vector3 UpAxis => Vector3.up;

        public static Vector3 GetContactStationLocalPosition(int index)
        {
            switch (index)
            {
                case 0:
                    return new Vector3(-1.12f, 0.62f, 1.55f);
                case 1:
                    return new Vector3(1.12f, 0.62f, 1.55f);
                case 2:
                    return new Vector3(-1.12f, 0.62f, -1.55f);
                case 3:
                    return new Vector3(1.12f, 0.62f, -1.55f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static string GetContactStationName(int index)
        {
            switch (index)
            {
                case 0:
                    return "CONTACT_FL";
                case 1:
                    return "CONTACT_FR";
                case 2:
                    return "CONTACT_RL";
                case 3:
                    return "CONTACT_RR";
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static string GetWheelName(int index)
        {
            switch (index)
            {
                case 0:
                    return "WHEEL_FL";
                case 1:
                    return "WHEEL_FR";
                case 2:
                    return "WHEEL_RL";
                case 3:
                    return "WHEEL_RR";
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static string GetWheelPivotName(int index)
        {
            return (index < FrontWheelCount ? "STEER_" : "HUB_") +
                   GetWheelName(index).Substring("WHEEL_".Length);
        }
    }
}
