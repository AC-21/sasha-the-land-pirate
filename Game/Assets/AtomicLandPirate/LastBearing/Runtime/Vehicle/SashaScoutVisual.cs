#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.Vehicle
{
    /// <summary>
    /// Stable presentation handles for the C0 scout. The component contains
    /// no input, Rigidbody, canonical state, save state, or asset loading.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SashaScoutVisual : MonoBehaviour
    {
        private Transform[] _contactStations = Array.Empty<Transform>();
        private Transform[] _wheelVisuals = Array.Empty<Transform>();
        private Transform[] _wheelPivots = Array.Empty<Transform>();
        private Transform? _winchModule;
        private Transform? _rangeTankModule;

        public string DirectionStage => SashaScoutSemanticContract.Stage;

        public Transform? GeometryRoot { get; private set; }

        public Transform? Lod0Root { get; private set; }

        public Transform? Lod1Root { get; private set; }

        public Transform? Lod2Root { get; private set; }

        public Transform? RigRoot { get; private set; }

        public Transform? SocketsRoot { get; private set; }

        public Transform? ModulesRoot { get; private set; }

        public Transform? CollisionRoot { get; private set; }

        public int ContactStationCount => _contactStations.Length;

        public int WheelVisualCount => _wheelVisuals.Length;

        public bool HasProductionGeometry => false;

        public SashaScoutBlockoutMaterials Materials { get; private set; }

        public bool IsWinchVisible =>
            _winchModule != null && _winchModule.gameObject.activeSelf;

        public bool IsRangeTankVisible =>
            _rangeTankModule != null && _rangeTankModule.gameObject.activeSelf;

        public Transform? WinchModuleRoot => _winchModule;

        public Transform? RangeTankModuleRoot => _rangeTankModule;

        public SashaScoutModulePresentation Module { get; private set; }

        internal void Configure(
            SashaScoutBlockoutMaterials materials,
            Transform geometryRoot,
            Transform lod0Root,
            Transform lod1Root,
            Transform lod2Root,
            Transform rigRoot,
            Transform socketsRoot,
            Transform modulesRoot,
            Transform collisionRoot,
            Transform[] contactStations,
            Transform[] wheelVisuals,
            Transform[] wheelPivots,
            Transform winchModule,
            Transform rangeTankModule)
        {
            if (contactStations == null ||
                wheelVisuals == null ||
                wheelPivots == null ||
                contactStations.Length != SashaScoutSemanticContract.WheelCount ||
                wheelVisuals.Length != SashaScoutSemanticContract.WheelCount ||
                wheelPivots.Length != SashaScoutSemanticContract.WheelCount)
            {
                throw new ArgumentException(
                    "Sasha Scout requires exactly four contact, wheel, and pivot transforms.");
            }

            GeometryRoot = geometryRoot ??
                           throw new ArgumentNullException(nameof(geometryRoot));
            Materials = materials;
            Lod0Root = lod0Root ?? throw new ArgumentNullException(nameof(lod0Root));
            Lod1Root = lod1Root ?? throw new ArgumentNullException(nameof(lod1Root));
            Lod2Root = lod2Root ?? throw new ArgumentNullException(nameof(lod2Root));
            RigRoot = rigRoot ?? throw new ArgumentNullException(nameof(rigRoot));
            SocketsRoot = socketsRoot ??
                          throw new ArgumentNullException(nameof(socketsRoot));
            ModulesRoot = modulesRoot ??
                          throw new ArgumentNullException(nameof(modulesRoot));
            CollisionRoot = collisionRoot ??
                            throw new ArgumentNullException(nameof(collisionRoot));
            _contactStations = (Transform[])contactStations.Clone();
            _wheelVisuals = (Transform[])wheelVisuals.Clone();
            _wheelPivots = (Transform[])wheelPivots.Clone();
            _winchModule = winchModule ??
                           throw new ArgumentNullException(nameof(winchModule));
            _rangeTankModule = rangeTankModule ??
                               throw new ArgumentNullException(nameof(rangeTankModule));
            ApplyModule(SashaScoutModulePresentation.None);
        }

        public Transform[] CopyContactStations()
        {
            return (Transform[])_contactStations.Clone();
        }

        public Transform[] CopyWheelVisuals()
        {
            return (Transform[])_wheelVisuals.Clone();
        }

        public Transform[] CopyWheelPivots()
        {
            return (Transform[])_wheelPivots.Clone();
        }

        public Transform? FindSocket(string socketName)
        {
            return SocketsRoot?.Find(socketName);
        }

        public void ApplyModule(SashaScoutModulePresentation module)
        {
            Module = module;
            if (_winchModule != null)
            {
                _winchModule.gameObject.SetActive(
                    module == SashaScoutModulePresentation.WinchAssembly);
            }

            if (_rangeTankModule != null)
            {
                _rangeTankModule.gameObject.SetActive(
                    module == SashaScoutModulePresentation.SealedRangeTank);
            }
        }

        internal void ApplyModule(LastBearingVisualModule module)
        {
            ApplyModule(module switch
            {
                LastBearingVisualModule.WinchAssembly =>
                    SashaScoutModulePresentation.WinchAssembly,
                LastBearingVisualModule.SealedRangeTank =>
                    SashaScoutModulePresentation.SealedRangeTank,
                _ => SashaScoutModulePresentation.None,
            });
        }

        internal void SetFrontSteering(float degrees)
        {
            for (var index = 0;
                 index < SashaScoutSemanticContract.FrontWheelCount;
                 index++)
            {
                _wheelPivots[index].localRotation =
                    Quaternion.Euler(0f, degrees, 0f);
            }
        }

        internal void RotateWheels(float degrees)
        {
            foreach (Transform wheel in _wheelVisuals)
            {
                wheel.Rotate(Vector3.right, degrees, Space.Self);
            }
        }
    }
}
