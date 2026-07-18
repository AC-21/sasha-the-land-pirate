#nullable enable

using System;
using UnityEngine;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public enum DepotApproachRecoveryPresentationState
    {
        Dormant = 0,
        Available = 1,
        Unlocked = 2,
    }

    /// <summary>
    /// C0 blockout for the depot-threshold recovery point. It presents a
    /// canonical read-model state and deliberately owns no input, physics, or
    /// save authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastBearingDepotApproachRecoveryView : MonoBehaviour
    {
        public const string DirectionPackageId = "C0-VGR-02";
        public const string Revision = "R1";
        public const string ContentId = "poi_depot_approach_recovery_a";
        public const string RootName =
            "poi_depot_approach_recovery_a [C0-VGR-02 R1]";
        public const string InteractionAnchorName =
            "ANCHOR_DEPOT_RECOVERY_INTERACTION";

        private GameObject? _availableTool;
        private GameObject? _unlockedLatch;
        private Light? _workLight;
        private bool _built;

        public Transform? InteractionAnchor { get; private set; }

        public DepotApproachRecoveryPresentationState State { get; private set; }

        public bool IsAvailableToolVisible =>
            _availableTool != null && _availableTool.activeSelf;

        public bool IsUnlockedLatchVisible =>
            _unlockedLatch != null && _unlockedLatch.activeSelf;

        public void Build(
            Material iron,
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
            InteractionAnchor = new GameObject(InteractionAnchorName).transform;
            InteractionAnchor.SetParent(transform, false);
            InteractionAnchor.localPosition = new Vector3(0f, 0.55f, 0f);

            CreatePrimitive(
                "Recovery Plinth",
                PrimitiveType.Cube,
                transform,
                new Vector3(0f, 0.12f, 0f),
                new Vector3(2.8f, 0.24f, 2.4f),
                iron,
                Quaternion.identity);
            CreatePrimitive(
                "Bone Recovery Eye",
                PrimitiveType.Cylinder,
                InteractionAnchor,
                Vector3.zero,
                new Vector3(0.5f, 0.18f, 0.5f),
                bone,
                Quaternion.Euler(90f, 0f, 0f));
            CreatePrimitive(
                "Mechanical Safe Line",
                PrimitiveType.Cube,
                transform,
                new Vector3(0f, 0.26f, -1.05f),
                new Vector3(2.3f, 0.08f, 0.14f),
                bone,
                Quaternion.identity);

            _availableTool = CreatePrimitive(
                "Available Recovery Bridle",
                PrimitiveType.Cube,
                InteractionAnchor,
                new Vector3(0f, 0.22f, -0.52f),
                new Vector3(0.12f, 0.12f, 1.05f),
                tungsten,
                Quaternion.identity);
            _unlockedLatch = CreatePrimitive(
                "Mechanically Seated Latch",
                PrimitiveType.Cube,
                InteractionAnchor,
                new Vector3(0f, 0.38f, 0f),
                new Vector3(0.82f, 0.16f, 0.22f),
                signal,
                Quaternion.Euler(0f, 45f, 0f));

            var lightObject = new GameObject("Recovery Tungsten Practical");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            _workLight = lightObject.AddComponent<Light>();
            _workLight.type = LightType.Point;
            _workLight.range = 7f;
            _workLight.shadows = LightShadows.None;
            ApplyState(DepotApproachRecoveryPresentationState.Dormant);
        }

        public void ApplyState(DepotApproachRecoveryPresentationState state)
        {
            if (!Enum.IsDefined(typeof(DepotApproachRecoveryPresentationState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            State = state;
            if (_availableTool != null)
            {
                _availableTool.SetActive(
                    state == DepotApproachRecoveryPresentationState.Available);
            }

            if (_unlockedLatch != null)
            {
                _unlockedLatch.SetActive(
                    state == DepotApproachRecoveryPresentationState.Unlocked);
            }

            if (_workLight != null)
            {
                _workLight.color = state ==
                    DepotApproachRecoveryPresentationState.Unlocked
                    ? new Color32(92, 199, 208, 255)
                    : new Color32(255, 196, 107, 255);
                _workLight.intensity = state switch
                {
                    DepotApproachRecoveryPresentationState.Available => 460f,
                    DepotApproachRecoveryPresentationState.Unlocked => 260f,
                    _ => 45f,
                };
            }
        }

        private static GameObject CreatePrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion rotation)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            primitive.transform.localRotation = rotation;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            var collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return primitive;
        }
    }
}
