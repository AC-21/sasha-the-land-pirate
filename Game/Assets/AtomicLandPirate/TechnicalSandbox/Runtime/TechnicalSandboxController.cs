#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AC21.Sasha.TechnicalSandbox
{
    /// <summary>
    /// Builds and presents the WP-0003 non-gameplay technical interaction
    /// sandbox from inspectable primitives.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TechnicalSandboxController : MonoBehaviour
    {
        private const float HudWidth = 372f;
        private const float HudHeight = 270f;

        private static readonly Color IronColor =
            new Color(0.18f, 0.205f, 0.215f, 1f);
        private static readonly Color DarkIronColor =
            new Color(0.07f, 0.085f, 0.09f, 1f);
        private static readonly Color TungstenColor =
            new Color(1f, 0.62f, 0.31f, 1f);
        private static readonly Color NeonAccentColor =
            new Color(0.18f, 0.78f, 0.86f, 1f);

        private readonly List<Material> _ownedMaterials =
            new List<Material>();

        private TechnicalProbeMarker[] _markers =
            new TechnicalProbeMarker[0];
        private Camera? _sandboxCamera;
        private GUIStyle? _panelStyle;
        private GUIStyle? _titleStyle;
        private GUIStyle? _bodyStyle;
        private Color _previousAmbientLight;
        private Color _previousFogColor;
        private float _previousFogDensity;
        private bool _previousFogEnabled;
        private bool _renderSettingsCaptured;
        private bool _initialized;

        public TechnicalProbeState State { get; private set; } =
            new TechnicalProbeState();

        public int ProbeCount => _markers.Length;

        public Camera? SandboxCamera => _sandboxCamera;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            State = new TechnicalProbeState();
            BuildEnvironment();
            RefreshMarkers();
        }

        public void ActivateProbe(int probeIndex)
        {
            State.ActivateProbe(probeIndex);
            RefreshMarkers();
        }

        public PersistenceProbeResult AttemptPersistence()
        {
            return State.AttemptPersistence();
        }

        public void ResetPresentationState()
        {
            State.Reset();
            RefreshMarkers();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame ||
                _sandboxCamera == null)
            {
                return;
            }

            var pointer = mouse.position.ReadValue();
            var pointerOverHud =
                pointer.x <= HudWidth + 18f &&
                pointer.y >= Screen.height - HudHeight - 18f;
            if (pointerOverHud)
            {
                return;
            }

            var ray = _sandboxCamera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out var hit, 250f))
            {
                return;
            }

            var marker = hit.collider.GetComponent<TechnicalProbeMarker>();
            if (marker != null)
            {
                ActivateProbe(marker.ProbeIndex);
            }
        }

        private void BuildEnvironment()
        {
            _previousAmbientLight = RenderSettings.ambientLight;
            _previousFogEnabled = RenderSettings.fog;
            _previousFogColor = RenderSettings.fogColor;
            _previousFogDensity = RenderSettings.fogDensity;
            _renderSettingsCaptured = true;

            RenderSettings.ambientLight = new Color(0.085f, 0.09f, 0.095f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.035f, 0.045f, 0.05f);
            RenderSettings.fogDensity = 0.008f;

            BuildCamera();
            BuildLight();

            CreateBlock(
                "Technical Ground",
                new Vector3(0f, -0.18f, 0f),
                new Vector3(24f, 0.35f, 18f),
                DarkIronColor,
                Color.black);

            CreateBlock(
                "Tungsten Spine",
                new Vector3(0f, 0.05f, 0f),
                new Vector3(1.1f, 0.12f, 15f),
                TungstenColor * 0.55f,
                TungstenColor * 0.22f);

            _markers = new TechnicalProbeMarker[
                TechnicalProbeState.SupportedProbeCount];

            for (var index = 0; index < _markers.Length; index++)
            {
                var column = index % 5;
                var row = index / 5;
                var height = 0.75f + ((index * 7) % 4) * 0.32f;
                var position = new Vector3(
                    (column - 2) * 3.1f,
                    height * 0.5f,
                    (row - 1) * 3.55f);

                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                probe.name = "Technical Probe " + index.ToString("00");
                probe.transform.SetParent(transform, false);
                probe.transform.localPosition = position;
                probe.transform.localScale = new Vector3(2.15f, height, 2.15f);

                var renderer = probe.GetComponent<Renderer>();
                var material = CreateMaterial(IronColor, Color.black);
                renderer.sharedMaterial = material;

                var marker = probe.AddComponent<TechnicalProbeMarker>();
                marker.Configure(
                    index,
                    renderer,
                    material,
                    position.y,
                    height,
                    IronColor,
                    NeonAccentColor);
                _markers[index] = marker;
            }

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Technical Beacon";
            beacon.transform.SetParent(transform, false);
            beacon.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            beacon.transform.localScale = new Vector3(0.38f, 1.65f, 0.38f);
            beacon.GetComponent<Renderer>().sharedMaterial = CreateMaterial(
                TungstenColor,
                TungstenColor * 1.8f);
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("Technical Sandbox Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";

            _sandboxCamera = cameraObject.AddComponent<Camera>();
            _sandboxCamera.clearFlags = CameraClearFlags.SolidColor;
            _sandboxCamera.backgroundColor =
                new Color(0.018f, 0.026f, 0.03f, 1f);
            _sandboxCamera.fieldOfView = 48f;
            _sandboxCamera.nearClipPlane = 0.2f;
            _sandboxCamera.farClipPlane = 300f;
            _sandboxCamera.allowHDR = true;

            var rig = cameraObject.AddComponent<TechnicalSandboxCameraRig>();
            rig.Configure(Vector3.zero);
        }

        private void BuildLight()
        {
            var lightObject = new GameObject("Tungsten Key Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(
                48f,
                -32f,
                0f);

            var lightComponent = lightObject.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.color = TungstenColor;
            lightComponent.intensity = 2.2f;
            lightComponent.shadows = LightShadows.Soft;
            lightComponent.useColorTemperature = true;
            lightComponent.colorTemperature = 3200f;
        }

        private void CreateBlock(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color,
            Color emission)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(transform, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial =
                CreateMaterial(color, emission);
        }

        private Material CreateMaterial(Color color, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard");
            if (shader == null)
            {
                throw new MissingReferenceException(
                    "TECHNICAL_SANDBOX_SHADER_MISSING");
            }

            var material = new Material(shader)
            {
                hideFlags = HideFlags.DontSave
            };
            TechnicalProbeMarker.SetMaterialColor(material, color, emission);
            _ownedMaterials.Add(material);
            return material;
        }

        private void RefreshMarkers()
        {
            for (var index = 0; index < _markers.Length; index++)
            {
                var marker = _markers[index];
                marker.Refresh(
                    index == State.SelectedProbeIndex,
                    State.GetActivationCount(index));
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.09f, 0.105f, 0.11f, 0.96f);

            GUILayout.BeginArea(
                new Rect(18f, 18f, HudWidth, HudHeight),
                GUIContent.none,
                _panelStyle!);
            GUILayout.Label("SASHA - TECHNICAL SANDBOX", _titleStyle!);
            GUILayout.Label(
                "Non-gameplay WP-0003 interaction proof",
                _bodyStyle!);
            GUILayout.Space(8f);
            GUILayout.Label(
                "Selected probe: " +
                (State.SelectedProbeIndex < 0
                    ? "none"
                    : State.SelectedProbeIndex.ToString("00")),
                _bodyStyle!);
            GUILayout.Label(
                "Presentation interactions: " + State.InteractionCount,
                _bodyStyle!);
            GUILayout.Label("Persistence: DISABLED / 0 bytes", _bodyStyle!);
            GUILayout.Label(State.LastStatus, _bodyStyle!);
            GUILayout.Space(8f);
            GUILayout.Label(
                "LMB select | MMB/WASD pan | RMB/QE rotate | wheel zoom",
                _bodyStyle!);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("RESET PROBES", GUILayout.Height(30f)))
            {
                ResetPresentationState();
            }

            if (GUILayout.Button("PROBE SAVE GATE", GUILayout.Height(30f)))
            {
                AttemptPersistence();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.backgroundColor = previousBackground;
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 14, 14)
            };
            _panelStyle.normal.textColor = Color.white;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = TungstenColor }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.86f, 0.89f, 0.9f) }
            };
        }

        private void OnDestroy()
        {
            if (_renderSettingsCaptured)
            {
                RenderSettings.ambientLight = _previousAmbientLight;
                RenderSettings.fog = _previousFogEnabled;
                RenderSettings.fogColor = _previousFogColor;
                RenderSettings.fogDensity = _previousFogDensity;
                _renderSettingsCaptured = false;
            }

            foreach (var material in _ownedMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            _ownedMaterials.Clear();
        }
    }

    public sealed class TechnicalProbeMarker : MonoBehaviour
    {
        private Renderer? _renderer;
        private Material? _material;
        private float _baseCenterY;
        private float _baseHeight;
        private Color _baseColor;
        private Color _selectedColor;

        public int ProbeIndex { get; private set; }

        public void Configure(
            int probeIndex,
            Renderer markerRenderer,
            Material material,
            float baseCenterY,
            float baseHeight,
            Color baseColor,
            Color selectedColor)
        {
            ProbeIndex = probeIndex;
            _renderer = markerRenderer;
            _material = material;
            _baseCenterY = baseCenterY;
            _baseHeight = baseHeight;
            _baseColor = baseColor;
            _selectedColor = selectedColor;
        }

        public void Refresh(bool selected, int activationCount)
        {
            if (_renderer == null || _material == null)
            {
                return;
            }

            var addedHeight = Mathf.Min(activationCount, 6) * 0.16f;
            var scale = transform.localScale;
            scale.y = _baseHeight + addedHeight;
            transform.localScale = scale;

            var position = transform.localPosition;
            position.y = _baseCenterY + addedHeight * 0.5f;
            transform.localPosition = position;

            SetMaterialColor(
                _material,
                selected ? _selectedColor : _baseColor,
                selected ? _selectedColor * 1.35f : Color.black);
        }

        public static void SetMaterialColor(
            Material material,
            Color color,
            Color emission)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
                if (emission.maxColorComponent > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                }
            }
        }
    }
}
