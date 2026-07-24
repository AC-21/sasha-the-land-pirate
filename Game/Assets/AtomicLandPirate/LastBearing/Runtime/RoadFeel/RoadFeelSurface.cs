#nullable enable

using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    /// <summary>
    /// Presentation-only road material shared by the authored corridor and
    /// Road Feel Lab. The values are fixed presets so authoring a surface kind
    /// cannot create a hidden per-collider handling variant.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadFeelSurface : MonoBehaviour
    {
        [SerializeField]
        private RoadFeelSurfaceKind _kind = RoadFeelSurfaceKind.Hardpack;

        public RoadFeelSurfaceKind Kind => _kind;

        public float Grip
        {
            get
            {
                switch (_kind)
                {
                    case RoadFeelSurfaceKind.Concrete:
                        return 1.00f;
                    case RoadFeelSurfaceKind.Hardpack:
                        return 0.84f;
                    case RoadFeelSurfaceKind.Gravel:
                        return 0.70f;
                    case RoadFeelSurfaceKind.Sand:
                        return 0.50f;
                    case RoadFeelSurfaceKind.Washboard:
                        return 0.62f;
                    default:
                        return 0.84f;
                }
            }
        }

        public float RollingResistance
        {
            get
            {
                switch (_kind)
                {
                    case RoadFeelSurfaceKind.Concrete:
                        return 0.014f;
                    case RoadFeelSurfaceKind.Hardpack:
                        return 0.026f;
                    case RoadFeelSurfaceKind.Gravel:
                        return 0.042f;
                    case RoadFeelSurfaceKind.Sand:
                        return 0.090f;
                    case RoadFeelSurfaceKind.Washboard:
                        return 0.055f;
                    default:
                        return 0.026f;
                }
            }
        }

        public void Configure(RoadFeelSurfaceKind kind)
        {
            _kind = IsKnown(kind) ? kind : RoadFeelSurfaceKind.Hardpack;
        }

        private void OnValidate()
        {
            if (!IsKnown(_kind))
            {
                _kind = RoadFeelSurfaceKind.Hardpack;
            }
        }

        private static bool IsKnown(RoadFeelSurfaceKind kind)
        {
            switch (kind)
            {
                case RoadFeelSurfaceKind.Concrete:
                case RoadFeelSurfaceKind.Hardpack:
                case RoadFeelSurfaceKind.Gravel:
                case RoadFeelSurfaceKind.Sand:
                case RoadFeelSurfaceKind.Washboard:
                    return true;
                default:
                    return false;
            }
        }
    }
}
