#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    /// <summary>
    /// Runtime-built, presentation-only road-feel course. It exercises the
    /// vehicle controller without creating canonical game or save state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadFeelLabController : MonoBehaviour
    {
        public const string RuntimeRootName = "Road Feel Lab Runtime [Presentation Only]";

        private static readonly Color Basin = new Color32(44, 40, 34, 255);
        private static readonly Color Concrete = new Color32(111, 104, 91, 255);
        private static readonly Color Hardpack = new Color32(112, 77, 52, 255);
        private static readonly Color Gravel = new Color32(86, 78, 68, 255);
        private static readonly Color Sand = new Color32(166, 126, 76, 255);
        private static readonly Color Iron = new Color32(31, 33, 34, 255);
        private static readonly Color Oxide = new Color32(132, 58, 31, 255);
        private static readonly Color Bone = new Color32(209, 190, 150, 255);
        private static readonly Color Tungsten = new Color32(255, 183, 82, 255);
        private static readonly Color SignalCyan = new Color32(72, 180, 188, 255);
        private static readonly Color CriticalRed = new Color32(195, 55, 40, 255);

        private static readonly string[] SectionNames =
        {
            "LAUNCH + BRAKE STRAIGHT",
            "HARDPACK SLALOM",
            "HARDPACK → GRAVEL BEND",
            "WASHBOARD + CURB",
            "OFF-CAMBER LOAD",
            "SAND MOMENTUM LANE",
            "RECOVERY BAY"
        };

        private readonly List<Material> _ownedMaterials = new List<Material>();
        private readonly List<Transform> _resetAnchors = new List<Transform>();
        private readonly List<GameObject> _cargoCrates = new List<GameObject>();
        private RoadFeelVehicleController? _vehicle;
        private RoadFeelChaseCamera? _chaseCamera;
        private Material? _damageLampMaterial;
        private int _activeSection;
        private int _cargoStep;
        private RoadFeelDamageBand _damageBand = RoadFeelDamageBand.Healthy;
        private float _runStartTime;
        private float _invertedTime;
        private bool _hudVisible = true;
        private string _status = "Launch cleanly, brake once, then read every surface.";
        private GUIStyle? _panelStyle;
        private GUIStyle? _titleStyle;
        private GUIStyle? _headingStyle;
        private GUIStyle? _bodyStyle;
        private GUIStyle? _mutedStyle;
        private GUIStyle? _buttonStyle;
        private Texture2D? _hudBackdrop;

        private float CargoMassKilograms => _cargoStep * 650f;

        private void Awake()
        {
            BuildLab();
        }

        private void OnDestroy()
        {
            foreach (var material in _ownedMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }

            if (_hudBackdrop != null)
            {
                Destroy(_hudBackdrop);
                _hudBackdrop = null;
            }
        }

        private void Update()
        {
            if (_vehicle == null)
            {
                return;
            }

            ReadMetaControls();
            _vehicle.SetControlInput(ReadVehicleControls());
            UpdateCourseProgress();
            RecoverIfNeeded();
        }

        private void BuildLab()
        {
            ConfigureEnvironment();

            var basin = CreateMaterial(Basin, Color.black, 0.04f);
            var concrete = CreateMaterial(Concrete, Color.black, 0.18f);
            var hardpack = CreateMaterial(Hardpack, Color.black, 0.08f);
            var gravel = CreateMaterial(Gravel, Color.black, 0.04f);
            var sand = CreateMaterial(Sand, Color.black, 0.03f);
            var iron = CreateMaterial(Iron, Color.black, 0.32f);
            var oxide = CreateMaterial(Oxide, Color.black, 0.18f);
            var bone = CreateMaterial(Bone, Color.black, 0.22f);
            var tungsten = CreateMaterial(
                Tungsten * 0.55f,
                Tungsten * 2.1f,
                0.38f);
            var signal = CreateMaterial(
                SignalCyan * 0.52f,
                SignalCyan * 1.2f,
                0.28f);
            _damageLampMaterial = CreateMaterial(
                SignalCyan * 0.5f,
                SignalCyan * 1.5f,
                0.38f);

            BuildCourse(
                basin,
                concrete,
                hardpack,
                gravel,
                sand,
                iron,
                oxide,
                bone,
                tungsten,
                signal);
            BuildRig(iron, oxide, bone, tungsten);
            BuildCamera();
            ApplyLoadProfile();
            _runStartTime = Time.unscaledTime;
        }

        private void ConfigureEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.31f, 0.27f, 0.215f, 1f);
            RenderSettings.fogDensity = 0.0027f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.245f, 0.19f, 1f);

            var sunObject = new GameObject("Bleached West Texas Sun");
            sunObject.transform.SetParent(transform, false);
            sunObject.transform.rotation = Quaternion.Euler(38f, -36f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.83f, 0.63f, 1f);
            sun.intensity = 1.24f;
            sun.shadows = LightShadows.Soft;
        }

        private void BuildCourse(
            Material basin,
            Material concrete,
            Material hardpack,
            Material gravel,
            Material sand,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            var course = new GameObject("R0 Authored Road Feel Course").transform;
            course.SetParent(transform, false);

            CreateBlock(
                "Bleached Basin Catch Ground",
                course,
                new Vector3(45f, -1.05f, -18f),
                new Vector3(280f, 1.8f, 245f),
                basin,
                Quaternion.identity);

            CreateSurface(
                "Concrete Launch Pad",
                course,
                new Vector3(0f, 0f, -101f),
                new Vector3(12f, 0.32f, 38f),
                concrete,
                Quaternion.identity,
                RoadFeelSurfaceKind.Concrete);
            CreateSurface(
                "Hardpack Brake Straight",
                course,
                new Vector3(0f, 0f, -68f),
                new Vector3(12f, 0.32f, 28f),
                hardpack,
                Quaternion.identity,
                RoadFeelSurfaceKind.Hardpack);
            CreateSurface(
                "Hardpack Slalom Lane",
                course,
                new Vector3(0f, 0f, -29f),
                new Vector3(12f, 0.32f, 50f),
                hardpack,
                Quaternion.identity,
                RoadFeelSurfaceKind.Hardpack);
            CreateSurface(
                "Bend Entry Hardpack",
                course,
                new Vector3(0f, 0f, 7f),
                new Vector3(12f, 0.32f, 22f),
                hardpack,
                Quaternion.identity,
                RoadFeelSurfaceKind.Hardpack);

            CreateSurface(
                "Gravel Bend 15",
                course,
                new Vector3(2.5f, 0f, 22f),
                new Vector3(12f, 0.32f, 18f),
                gravel,
                Quaternion.Euler(0f, 15f, 0f),
                RoadFeelSurfaceKind.Gravel);
            CreateSurface(
                "Gravel Bend 32",
                course,
                new Vector3(9.5f, 0f, 38f),
                new Vector3(12f, 0.32f, 18f),
                gravel,
                Quaternion.Euler(0f, 32f, 0f),
                RoadFeelSurfaceKind.Gravel);
            CreateSurface(
                "Gravel Bend 50",
                course,
                new Vector3(21.5f, 0f, 51.5f),
                new Vector3(12f, 0.32f, 18f),
                gravel,
                Quaternion.Euler(0f, 50f, 0f),
                RoadFeelSurfaceKind.Gravel);
            CreateSurface(
                "Gravel Bend 68",
                course,
                new Vector3(37f, 0f, 60.5f),
                new Vector3(12f, 0.32f, 18f),
                gravel,
                Quaternion.Euler(0f, 68f, 0f),
                RoadFeelSurfaceKind.Gravel);
            CreateSurface(
                "Gravel Bend Exit",
                course,
                new Vector3(54f, 0f, 65f),
                new Vector3(12f, 0.32f, 18f),
                gravel,
                Quaternion.Euler(0f, 84f, 0f),
                RoadFeelSurfaceKind.Gravel);

            CreateSurface(
                "Washboard Deck",
                course,
                new Vector3(78f, 0f, 66f),
                new Vector3(12f, 0.32f, 32f),
                gravel,
                Quaternion.Euler(0f, 90f, 0f),
                RoadFeelSurfaceKind.Washboard);
            for (var index = 0; index < 9; index++)
            {
                GameObject rib = CreateBlock(
                    "Washboard Rib " + (index + 1),
                    course,
                    new Vector3(66f + index * 3f, 0.24f, 66f),
                    new Vector3(0.24f, 0.18f, 11.2f),
                    bone,
                    Quaternion.identity);
                rib.AddComponent<RoadFeelSurface>().Configure(
                    RoadFeelSurfaceKind.Washboard);
            }

            CreateBlock(
                "Washboard North Curb",
                course,
                new Vector3(78f, 0.38f, 72f),
                new Vector3(33f, 0.42f, 0.45f),
                oxide,
                Quaternion.identity);
            CreateBlock(
                "Washboard South Curb",
                course,
                new Vector3(78f, 0.38f, 60f),
                new Vector3(33f, 0.42f, 0.45f),
                oxide,
                Quaternion.identity);

            var offCamberRotation =
                Quaternion.Euler(0f, 90f, 0f) *
                Quaternion.AngleAxis(-7f, Vector3.forward);
            CreateSurface(
                "Off-Camber Iron Causeway",
                course,
                new Vector3(105f, 0.7f, 66f),
                new Vector3(12f, 0.4f, 22f),
                concrete,
                offCamberRotation,
                RoadFeelSurfaceKind.Hardpack);
            CreateSurface(
                "Sand Momentum Lane",
                course,
                new Vector3(143f, 0f, 66f),
                new Vector3(12f, 0.42f, 54f),
                sand,
                Quaternion.Euler(0f, 90f, 0f),
                RoadFeelSurfaceKind.Sand);
            CreateSurface(
                "Recovery Bay Slab",
                course,
                new Vector3(181f, 0f, 66f),
                new Vector3(20f, 0.46f, 20f),
                concrete,
                Quaternion.Euler(0f, 90f, 0f),
                RoadFeelSurfaceKind.Concrete);

            BuildLaunchInstruments(course, iron, oxide, bone, tungsten);
            BuildSlalom(course, oxide, bone, signal);
            BuildSectionGates(course, iron, oxide, bone, tungsten, signal);
            BuildRecoveryBay(course, iron, oxide, bone, tungsten);

            AddResetAnchor(course, "Launch Reset", new Vector3(0f, 0.58f, -114f), 0f);
            AddResetAnchor(course, "Slalom Reset", new Vector3(0f, 0.58f, -49f), 0f);
            AddResetAnchor(course, "Bend Reset", new Vector3(0f, 0.58f, 7f), 0f);
            AddResetAnchor(course, "Washboard Reset", new Vector3(63f, 0.58f, 66f), 90f);
            AddResetAnchor(course, "Off-Camber Reset", new Vector3(96f, 1.34f, 66f), 90f);
            AddResetAnchor(course, "Sand Reset", new Vector3(119f, 0.62f, 66f), 90f);
            AddResetAnchor(course, "Recovery Reset", new Vector3(176f, 0.66f, 66f), 90f);
        }

        private void BuildLaunchInstruments(
            Transform parent,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten)
        {
            for (var index = 0; index < 5; index++)
            {
                var z = -111f + index * 13f;
                CreateBlock(
                    "Brake Measure Post " + (index + 1),
                    parent,
                    new Vector3(-6.8f, 1f, z),
                    new Vector3(0.25f, 2f, 0.25f),
                    index == 0 ? tungsten : bone,
                    Quaternion.identity);
            }

            CreateBlock(
                "Launch Iron Gantry Left",
                parent,
                new Vector3(-6.4f, 3.2f, -119f),
                new Vector3(0.75f, 6.4f, 1.1f),
                iron,
                Quaternion.identity);
            CreateBlock(
                "Launch Iron Gantry Right",
                parent,
                new Vector3(6.4f, 3.2f, -119f),
                new Vector3(0.75f, 6.4f, 1.1f),
                iron,
                Quaternion.identity);
            CreateBlock(
                "Launch Oxide Lintel",
                parent,
                new Vector3(0f, 6.1f, -119f),
                new Vector3(13.6f, 0.7f, 1.1f),
                oxide,
                Quaternion.identity);
        }

        private void BuildSlalom(
            Transform parent,
            Material oxide,
            Material bone,
            Material signal)
        {
            for (var index = 0; index < 7; index++)
            {
                var side = index % 2 == 0 ? -1f : 1f;
                var marker = CreateCylinder(
                    "Slalom Gate " + (index + 1),
                    parent,
                    new Vector3(side * 2.7f, 0.76f, -49f + index * 7f),
                    new Vector3(0.62f, 0.75f, 0.62f),
                    index == 6 ? signal : oxide);
                marker.transform.localScale = new Vector3(0.62f, 0.75f, 0.62f);
                CreateCylinder(
                    "Slalom Bone Cap " + (index + 1),
                    parent,
                    new Vector3(side * 2.7f, 1.57f, -49f + index * 7f),
                    new Vector3(0.72f, 0.08f, 0.72f),
                    bone);
            }
        }

        private void BuildSectionGates(
            Transform parent,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten,
            Material signal)
        {
            BuildGate(parent, "GRAVEL", new Vector3(0f, 0f, 11f), 0f, iron, oxide, signal);
            BuildGate(parent, "WASHBOARD", new Vector3(61f, 0f, 66f), 90f, iron, oxide, tungsten);
            BuildGate(parent, "SAND", new Vector3(117f, 0f, 66f), 90f, iron, bone, signal);
        }

        private void BuildGate(
            Transform parent,
            string name,
            Vector3 position,
            float yaw,
            Material post,
            Material lintel,
            Material beacon)
        {
            var root = new GameObject(name + " Instrument Gate").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            CreateBlock(
                name + " Left Pylon",
                root,
                new Vector3(-6.5f, 2.7f, 0f),
                new Vector3(0.65f, 5.4f, 0.8f),
                post,
                Quaternion.identity);
            CreateBlock(
                name + " Right Pylon",
                root,
                new Vector3(6.5f, 2.7f, 0f),
                new Vector3(0.65f, 5.4f, 0.8f),
                post,
                Quaternion.identity);
            CreateBlock(
                name + " Civic Lintel",
                root,
                new Vector3(0f, 5.25f, 0f),
                new Vector3(13.7f, 0.55f, 0.8f),
                lintel,
                Quaternion.identity);
            CreateSphere(
                name + " Tungsten Signal",
                root,
                new Vector3(0f, 5.72f, 0f),
                Vector3.one * 0.44f,
                beacon,
                disableCollider: true);
        }

        private void BuildRecoveryBay(
            Transform parent,
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten)
        {
            CreateBlock(
                "Recovery Bay Backstop",
                parent,
                new Vector3(191f, 3f, 66f),
                new Vector3(1f, 6f, 20f),
                iron,
                Quaternion.identity);
            CreateBlock(
                "Recovery Bay Operatic Cap",
                parent,
                new Vector3(186f, 6.3f, 66f),
                new Vector3(10f, 0.8f, 20f),
                oxide,
                Quaternion.identity);
            CreateBlock(
                "Recovery Safe Line",
                parent,
                new Vector3(176f, 0.28f, 66f),
                new Vector3(0.35f, 0.12f, 17f),
                bone,
                Quaternion.identity);
            CreatePointLight(
                "Recovery Tungsten Practical",
                parent,
                new Vector3(186f, 5.6f, 66f),
                Tungsten,
                620f,
                17f);
            CreateSphere(
                "Recovery Practical Housing",
                parent,
                new Vector3(186f, 5.6f, 66f),
                Vector3.one * 0.75f,
                tungsten,
                disableCollider: true);
        }

        private void AddResetAnchor(
            Transform parent,
            string name,
            Vector3 position,
            float yaw)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = position;
            anchor.localRotation = Quaternion.Euler(0f, yaw, 0f);
            _resetAnchors.Add(anchor);
        }

        private void BuildRig(
            Material iron,
            Material oxide,
            Material bone,
            Material tungsten)
        {
            RoadFeelRigInstance rig = RoadFeelRigFactory.Create(
                transform,
                _resetAnchors[0].position,
                _resetAnchors[0].rotation,
                new RoadFeelRigMaterials(
                    iron,
                    oxide,
                    bone,
                    tungsten,
                    _damageLampMaterial!));
            _vehicle = rig.Vehicle;
            foreach (GameObject cargo in rig.CargoVisuals)
            {
                _cargoCrates.Add(cargo);
            }

            rig.Adapter.SetRoadModeActive(true);
        }

        private void BuildCamera()
        {
            if (_vehicle == null)
            {
                return;
            }

            var cameraObject = new GameObject("Road Feel Elastic Chase Camera");
            cameraObject.transform.SetParent(transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 520f;
            camera.fieldOfView = 62f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.31f, 0.27f, 0.215f, 1f);
            cameraObject.AddComponent<AudioListener>();
            _chaseCamera = cameraObject.AddComponent<RoadFeelChaseCamera>();
            _chaseCamera.Configure(_vehicle.transform, _vehicle.Body);
        }

        private RoadFeelControlInput ReadVehicleControls()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            var throttle = keyboard != null &&
                           (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                ? 1f
                : 0f;
            var brake = keyboard != null &&
                        (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                ? 1f
                : 0f;
            var steering = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    steering -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    steering += 1f;
                }
            }

            var handbrake = keyboard != null && keyboard.spaceKey.isPressed
                ? 1f
                : 0f;
            if (gamepad != null)
            {
                throttle = Mathf.Max(throttle, gamepad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, gamepad.leftTrigger.ReadValue());
                var stickSteer = gamepad.leftStick.ReadValue().x;
                if (Mathf.Abs(stickSteer) > Mathf.Abs(steering))
                {
                    steering = stickSteer;
                }

                handbrake = Mathf.Max(
                    handbrake,
                    gamepad.buttonSouth.isPressed ? 1f : 0f);
            }

            return new RoadFeelControlInput(throttle, brake, steering, handbrake);
        }

        private void ReadMetaControls()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard?.rKey.wasPressedThisFrame ?? false) ||
                (gamepad?.buttonNorth.wasPressedThisFrame ?? false))
            {
                ResetVehicle("Manual recovery to the latest cleared instrument.");
            }

            if ((keyboard?.cKey.wasPressedThisFrame ?? false) ||
                (gamepad?.leftShoulder.wasPressedThisFrame ?? false))
            {
                CycleCargo();
            }

            if ((keyboard?.xKey.wasPressedThisFrame ?? false) ||
                (gamepad?.rightShoulder.wasPressedThisFrame ?? false))
            {
                CycleDamage();
            }

            if ((keyboard?.hKey.wasPressedThisFrame ?? false) ||
                (gamepad?.selectButton.wasPressedThisFrame ?? false))
            {
                _hudVisible = !_hudVisible;
            }
        }

        private void CycleCargo()
        {
            _cargoStep = (_cargoStep + 1) % 3;
            ApplyLoadProfile();
            _status = "Cargo stepped to " + CargoMassKilograms.ToString("0") +
                      " kg. Read braking distance, roll, and sand momentum.";
        }

        private void CycleDamage()
        {
            _damageBand = _damageBand switch
            {
                RoadFeelDamageBand.Healthy => RoadFeelDamageBand.Worn,
                RoadFeelDamageBand.Worn => RoadFeelDamageBand.Critical,
                _ => RoadFeelDamageBand.Healthy
            };
            ApplyLoadProfile();
            _status = "Rig condition stepped to " + _damageBand +
                      ". Read power, damping, pull, and recovery.";
        }

        private void ApplyLoadProfile()
        {
            _vehicle?.SetLoad(CargoMassKilograms, _damageBand);
            for (var index = 0; index < _cargoCrates.Count; index++)
            {
                _cargoCrates[index].SetActive(index < _cargoStep);
            }

            var lampColor = _damageBand switch
            {
                RoadFeelDamageBand.Healthy => SignalCyan,
                RoadFeelDamageBand.Worn => Tungsten,
                _ => CriticalRed
            };
            SetEmission(_damageLampMaterial, lampColor, 1.5f);
        }

        private void UpdateCourseProgress()
        {
            if (_vehicle == null || _resetAnchors.Count == 0)
            {
                return;
            }

            var position = _vehicle.Body.position;
            var nextSection = Mathf.Min(_activeSection + 1, _resetAnchors.Count - 1);
            if (nextSection > _activeSection &&
                Vector3.Distance(position, _resetAnchors[nextSection].position) < 14f)
            {
                _activeSection = nextSection;
                _status = _activeSection == SectionNames.Length - 1
                    ? "Recovery bay reached. Reset, change load, and compare another pass."
                    : "Instrument cleared: " + SectionNames[_activeSection] + ".";
            }
        }

        private void RecoverIfNeeded()
        {
            if (_vehicle == null)
            {
                return;
            }

            var body = _vehicle.Body;
            var position = body.position;
            var outOfBounds = position.y < -4f ||
                              position.x < -30f || position.x > 215f ||
                              position.z < -145f || position.z > 96f;
            var inverted = Vector3.Dot(body.transform.up, Vector3.up) < 0.15f &&
                           body.linearVelocity.magnitude < 4f;
            _invertedTime = inverted
                ? _invertedTime + Time.unscaledDeltaTime
                : 0f;
            if (outOfBounds || _invertedTime > 2.2f)
            {
                ResetVehicle(outOfBounds
                    ? "Course boundary recovery."
                    : "Inversion recovery after the rig settled.");
            }
        }

        private void ResetVehicle(string status)
        {
            if (_vehicle == null || _resetAnchors.Count == 0)
            {
                return;
            }

            var anchor = _resetAnchors[_activeSection];
            _vehicle.ResetAt(anchor.position, anchor.rotation);
            _chaseCamera?.SnapBehind();
            _invertedTime = 0f;
            _status = status;
        }

        private void OnGUI()
        {
            if (_vehicle == null)
            {
                return;
            }

            if (!_hudVisible)
            {
                GUI.Label(
                    new Rect(18f, 18f, 380f, 24f),
                    "H / SELECT · show Road Feel Lab instruments");
                return;
            }

            EnsureStyles();
            var telemetry = _vehicle.Telemetry;
            var panel = new Rect(18f, 18f, 382f, Screen.height - 36f);
            GUI.Box(panel, GUIContent.none, _panelStyle!);
            GUILayout.BeginArea(new Rect(
                panel.x + 18f,
                panel.y + 16f,
                panel.width - 36f,
                panel.height - 32f));
            GUILayout.Label("ROAD FEEL LAB", _titleStyle);
            GUILayout.Label("R0 · PRESENTATION COURSE · TARGET 60–90 SEC", _mutedStyle);
            GUILayout.Space(10f);
            GUILayout.Label(SectionNames[_activeSection], _headingStyle);
            GUILayout.Label(
                "ELAPSED " + (Time.unscaledTime - _runStartTime).ToString("0.0") + " s",
                _mutedStyle);
            GUILayout.Label(_status, _bodyStyle);
            GUILayout.Space(12f);

            GUILayout.Label("LIVE INSTRUMENTS", _headingStyle);
            GUILayout.Label(
                "SPEED       " + telemetry.SpeedKilometresPerHour.ToString("0.0") + " km/h\n" +
                "SURFACE     " + telemetry.DominantSurface + "\n" +
                "BODY SLIP   " + telemetry.BodySlipDegrees.ToString("+0.0;-0.0;0.0") + "°\n" +
                "YAW RATE    " + telemetry.YawRateDegreesPerSecond.ToString("+0.0;-0.0;0.0") + "°/s\n" +
                "STEER       " + telemetry.SteeringAngleDegrees.ToString("+0.0;-0.0;0.0") + "°\n" +
                "CONTACTS    " + telemetry.GroundedContacts + " / 4\n" +
                "COMPRESSION " + (telemetry.AverageCompression * 100f).ToString("0") + "%\n" +
                "CARGO       " + telemetry.CargoMassKilograms.ToString("0") + " kg\n" +
                "CONDITION   " + telemetry.DamageBand,
                _bodyStyle);
            GUILayout.Space(12f);

            GUILayout.Label("COMPARE THE LOAD", _headingStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CARGO", _buttonStyle))
            {
                CycleCargo();
            }

            if (GUILayout.Button("DAMAGE", _buttonStyle))
            {
                CycleDamage();
            }

            if (GUILayout.Button("RESET", _buttonStyle))
            {
                ResetVehicle("Manual recovery to the latest cleared instrument.");
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
            GUILayout.Label("CONTROLS", _headingStyle);
            GUILayout.Label(
                "W / RT · throttle      S / LT · brake / reverse\n" +
                "A D / LEFT STICK · steer\n" +
                "SPACE / A · handbrake\n" +
                "R / Y · reset      C / LB · cargo      X / RB · damage\n" +
                "RMB / RIGHT STICK · orbit      V / RS · recenter\n" +
                "H / SELECT · hide instruments",
                _mutedStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box);
            _panelStyle.normal.background = MakeTexture(
                new Color(0.045f, 0.047f, 0.045f, 0.93f));
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.76f, 0.38f, 1f) }
            };
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.76f, 0.88f, 0.87f, 1f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.89f, 0.84f, 0.72f, 1f) }
            };
            _mutedStyle = new GUIStyle(_bodyStyle)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.63f, 0.61f, 0.55f, 1f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                fixedHeight = 30f,
                normal = { textColor = Bone }
            };
        }

        private Texture2D MakeTexture(Color color)
        {
            _hudBackdrop = new Texture2D(1, 1)
            {
                name = "Road Feel Lab HUD Backdrop",
                hideFlags = HideFlags.HideAndDontSave
            };
            _hudBackdrop.SetPixel(0, 0, color);
            _hudBackdrop.Apply();
            return _hudBackdrop;
        }

        private Material CreateMaterial(
            Color color,
            Color emission,
            float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard");
            var material = new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            SetEmission(material, emission, 1f);
            _ownedMaterials.Add(material);
            return material;
        }

        private static void SetEmission(
            Material? material,
            Color color,
            float intensity)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
        }

        private GameObject CreateSurface(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion rotation,
            RoadFeelSurfaceKind kind)
        {
            var surfaceObject = CreateBlock(
                name,
                parent,
                position,
                scale,
                material,
                rotation);
            surfaceObject.AddComponent<RoadFeelSurface>().Configure(kind);
            return surfaceObject;
        }

        private static GameObject CreateBlock(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            Quaternion rotation)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
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
            bool disableCollider = false)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
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
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
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

        private static Light CreatePointLight(
            string name,
            Transform parent,
            Vector3 position,
            Color color,
            float intensity,
            float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.Soft;
            return light;
        }
    }
}
