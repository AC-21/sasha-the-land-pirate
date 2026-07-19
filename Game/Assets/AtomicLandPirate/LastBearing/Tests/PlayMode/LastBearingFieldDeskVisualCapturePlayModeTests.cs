#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AtomicLandPirate.Simulation.LastBearing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AtomicLandPirate.Presentation.LastBearing.Tests
{
    public sealed class LastBearingFieldDeskVisualCapturePlayModeTests
    {
        private const string SceneName = "LastBearing";
        private const string CaptureSet =
            "vgr-13-field-desk-visual-acceptance";
        private const string RequestRelativePath =
            "BuildArtifacts/WP-0002/local-only/" +
            "vgr-13-field-desk-capture-request.json";
        private const string OutputRelativeDirectory =
            "BuildArtifacts/WP-0002/visual-captures/vgr-13";
        private const int CreatorTargetWidth = 2560;
        private const int CreatorTargetHeight = 1600;
        private const int ResolutionWaitFrames = 120;
        private const int CameraSettleFrames = 240;

        private readonly List<PendingArtifact> _pending =
            new List<PendingArtifact>();
        private int _originalWidth;
        private int _originalHeight;
        private FullScreenMode _originalFullScreenMode;
        private bool _resolutionSnapshotTaken;
        private bool _sceneLoadedByCapture;

        [UnityTearDown]
        public IEnumerator TearDownSceneAndResolution()
        {
            if (_resolutionSnapshotTaken)
            {
                Screen.SetResolution(
                    _originalWidth,
                    _originalHeight,
                    _originalFullScreenMode);
                for (var frame = 0; frame < ResolutionWaitFrames; frame++)
                {
                    if (Screen.width == _originalWidth &&
                        Screen.height == _originalHeight)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (_sceneLoadedByCapture && scene.IsValid() && scene.isLoaded)
            {
                Scene cleanup = SceneManager.CreateScene(
                    "LastBearing_FieldDeskCaptureCleanup");
                SceneManager.SetActiveScene(cleanup);
                AsyncOperation? unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            _pending.Clear();
            _resolutionSnapshotTaken = false;
            _sceneLoadedByCapture = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CaptureVisualAcceptanceMatrixWhenLocallyRequested()
        {
            string repositoryRoot = ResolveRepositoryRoot();
            string requestPath = Path.Combine(
                repositoryRoot,
                RequestRelativePath);
            if (!File.Exists(requestPath))
            {
                yield break;
            }

            byte[] requestBytes = File.ReadAllBytes(requestPath);
            CaptureRequest request = ParseAndValidateRequest(
                repositoryRoot,
                requestBytes);
            _originalWidth = Screen.width;
            _originalHeight = Screen.height;
            _originalFullScreenMode = Screen.fullScreenMode;
            _resolutionSnapshotTaken = true;
            Resolution currentResolution = Screen.currentResolution;

            var manifest = new CaptureManifest
            {
                packet_id = "WP-0002",
                slice_id = "VGR-13",
                capture_set = CaptureSet,
                request_id = request.request_id,
                request_sha256 = ComputeSha256(requestBytes),
                source_head = request.source_head,
                source_tree = request.source_tree,
                base_commit = request.base_commit,
                branch = request.branch,
                contract_path = request.contract_path,
                contract_sha256 = request.contract_sha256,
                unity_version = Application.unityVersion,
                scene = SceneName,
                operator_identity = "Codex under creator authority",
                requested_at_utc = request.requested_at_utc,
                captured_at_utc = UtcNow(),
                original_width = _originalWidth,
                original_height = _originalHeight,
                original_full_screen_mode = _originalFullScreenMode.ToString(),
                display_resolution_width = currentResolution.width,
                display_resolution_height = currentResolution.height,
                creator_target_width = CreatorTargetWidth,
                creator_target_height = CreatorTargetHeight,
                creator_target_semantics =
                    "2560x1600 render target fixed by Technical Architecture",
                quality_level = QualitySettings.names[QualitySettings.GetQualityLevel()],
                color_space = QualitySettings.activeColorSpace.ToString(),
                render_scale_width = ScalableBufferManager.widthScaleFactor,
                render_scale_height = ScalableBufferManager.heightScaleFactor,
                graphics_api = SystemInfo.graphicsDeviceType.ToString(),
                graphics_device = SystemInfo.graphicsDeviceName,
                operating_system = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                system_memory_mb = SystemInfo.systemMemorySize,
                bloom_state = "absent from the reserved Last Bearing source",
                grayscale_conversion =
                    "integer Rec.709 approximation: " +
                    "(54*R + 183*G + 19*B + 128) >> 8; alpha preserved",
                save_interaction = "none; no Save or Load method invoked",
                known_limits = new[]
                {
                    "Captures do not prove interaction, allocation, memory, or target-Mac performance.",
                    "Title and garage are representative legacy-surface checks, not exhaustive mode coverage.",
                    "The source head and tree are request-bound; this harness does not inspect Git metadata."
                },
                state_sequence = new[]
                {
                    "ReturnToTitle()",
                    "StartNewGame(Mixed)",
                    "InspectCityNeed()",
                    "SelectCityGrammarHypothesis(RestrainedSnapGrid)",
                    "ManipulateCityGrammarPrimary()",
                    "ToggleCityGrammarTrialPiece()",
                    "ManipulateCityGrammarPrimary() x2",
                    "ConnectCityGrammarLogistics()",
                    "AdvanceCityGrammarDelivery() x2",
                    "RecordCityGrammarPathRead(true)",
                    "OpenGarageBay()"
                }
            };

            AsyncOperation? load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            _sceneLoadedByCapture = true;
            yield return load;
            yield return null;

            LastBearingGameController controller = RequireController();
            controller.enabled = false;
            Camera camera = RequireCamera(controller);

            try
            {
                controller.ReturnToTitle();
                Assert.That(controller.HasActiveGame, Is.False);
                Assert.That(controller.FieldDesk?.OwnsCityOverview, Is.False);
                yield return CaptureFrame(
                    controller,
                    "legacy-title-creator-target",
                    "title",
                    CreatorTargetWidth,
                    CreatorTargetHeight,
                    ScrollPosition.None,
                    makeGrayscale: false);

                StageTrialA(controller);
                yield return WaitForCityCamera(controller);
                manifest.camera = CaptureCamera(camera);
                string cityCanonicalHash = controller.CanonicalHash;
                manifest.city_canonical_sha256_before = cityCanonicalHash;

                yield return CaptureFrame(
                    controller,
                    "city-trial-a-top-1280x720",
                    "city-trial-a",
                    1280,
                    720,
                    ScrollPosition.Top,
                    makeGrayscale: false);
                yield return CaptureFrame(
                    controller,
                    "city-trial-a-bottom-1280x720",
                    "city-trial-a",
                    1280,
                    720,
                    ScrollPosition.Bottom,
                    makeGrayscale: false);
                yield return CaptureFrame(
                    controller,
                    "city-trial-a-top-1920x1200",
                    "city-trial-a",
                    1920,
                    1200,
                    ScrollPosition.Top,
                    makeGrayscale: true);
                yield return CaptureFrame(
                    controller,
                    "city-trial-a-top-creator-target-2560x1600",
                    "city-trial-a",
                    CreatorTargetWidth,
                    CreatorTargetHeight,
                    ScrollPosition.Top,
                    makeGrayscale: false);

                Assert.That(controller.CanonicalHash, Is.EqualTo(cityCanonicalHash));
                manifest.city_canonical_sha256_after = controller.CanonicalHash;

                controller.OpenGarageBay();
                controller.FieldDesk?.Refresh(force: true);
                Assert.That(controller.FieldDesk?.OwnsCityOverview, Is.False);
                yield return WaitForGarageCamera(controller);
                yield return CaptureFrame(
                    controller,
                    "legacy-garage-creator-target",
                    "garage",
                    CreatorTargetWidth,
                    CreatorTargetHeight,
                    ScrollPosition.None,
                    makeGrayscale: false);

                AssertExactCaptureMatrix();
                manifest.artifacts = BuildArtifactRecords();
                WriteEvidence(repositoryRoot, manifest);
            }
            finally
            {
                Screen.SetResolution(
                    _originalWidth,
                    _originalHeight,
                    _originalFullScreenMode);
            }

            yield return null;
        }

        private IEnumerator CaptureFrame(
            LastBearingGameController controller,
            string label,
            string state,
            int width,
            int height,
            ScrollPosition scrollPosition,
            bool makeGrayscale)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            for (var frame = 0; frame < ResolutionWaitFrames; frame++)
            {
                if (Screen.width == width && Screen.height == height)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(
                new Vector2Int(Screen.width, Screen.height),
                Is.EqualTo(new Vector2Int(width, height)),
                "Editor did not reach the exact requested capture resolution.");

            bool isCity = scrollPosition != ScrollPosition.None;
            ScrollView? scroll = null;
            bool currentOrderVisible = false;
            bool serviceStripVisible = false;
            float resolvedScrollOffset = 0f;
            if (isCity)
            {
                LastBearingFieldDesk desk = RequireFieldDesk(controller);
                desk.Refresh(force: true);
                Assert.That(desk.OwnsCityOverview, Is.True);
                UIDocument document = RequireDocument(controller);
                VisualElement root = document.rootVisualElement;
                scroll = root.Q<ScrollView>("desk-scroll");
                Assert.That(scroll, Is.Not.Null);
                yield return null;
                yield return null;

                if (scrollPosition == ScrollPosition.Top)
                {
                    scroll!.scrollOffset = Vector2.zero;
                }
                else
                {
                    float highValue = scroll!.verticalScroller.highValue;
                    Assert.That(highValue, Is.GreaterThan(0f));
                    scroll.verticalScroller.value = highValue;
                    scroll.scrollOffset = new Vector2(0f, highValue);
                }

                yield return null;
                resolvedScrollOffset = scroll!.scrollOffset.y;
                if (scrollPosition == ScrollPosition.Top)
                {
                    Assert.That(resolvedScrollOffset, Is.EqualTo(0f).Within(1f));
                }
                else
                {
                    Assert.That(
                        resolvedScrollOffset,
                        Is.EqualTo(scroll.verticalScroller.highValue).Within(1f));
                }

                currentOrderVisible = IsFullyOnScreen(
                    root.Q<VisualElement>(className: "current-order")?.worldBound,
                    width,
                    height);
                serviceStripVisible = IsFullyOnScreen(
                    root.Q<VisualElement>(className: "service-strip")?.worldBound,
                    width,
                    height);
                Assert.That(currentOrderVisible, Is.True);
                Assert.That(serviceStripVisible, Is.True);
            }

            yield return new WaitForEndOfFrame();
            Texture2D? texture = CaptureScreenshotAsTexture();
            Assert.That(texture, Is.Not.Null);
            try
            {
                Assert.That(texture!.width, Is.EqualTo(width));
                Assert.That(texture.height, Is.EqualTo(height));
                byte[] png = texture.EncodeToPNG();
                CameraRecord frameCamera = CaptureCamera(
                    RequireCamera(controller));
                PendingArtifact color = AddPendingArtifact(
                    label,
                    state,
                    width,
                    height,
                    texture.width,
                    texture.height,
                    scrollPosition,
                    resolvedScrollOffset,
                    currentOrderVisible,
                    serviceStripVisible,
                    "color",
                    string.Empty,
                    frameCamera,
                    png);

                if (makeGrayscale)
                {
                    byte[] grayscale = EncodeGrayscalePng(texture);
                    AddPendingArtifact(
                        label + "-grayscale",
                        state,
                        width,
                        height,
                        texture.width,
                        texture.height,
                        scrollPosition,
                        resolvedScrollOffset,
                        currentOrderVisible,
                        serviceStripVisible,
                        "grayscale",
                        color.sha256,
                        frameCamera,
                        grayscale);
                }
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        private static void StageTrialA(LastBearingGameController controller)
        {
            controller.StartNewGame(ColonyComposition.Mixed);
            controller.InspectCityNeed();
            controller.SelectCityGrammarHypothesis(
                LastBearingCityGrammarHypothesis.RestrainedSnapGrid);
            controller.ManipulateCityGrammarPrimary();
            controller.ToggleCityGrammarTrialPiece();
            controller.ManipulateCityGrammarPrimary();
            controller.ManipulateCityGrammarPrimary();
            controller.ConnectCityGrammarLogistics();
            controller.AdvanceCityGrammarDelivery();
            controller.AdvanceCityGrammarDelivery();
            controller.RecordCityGrammarPathRead(clear: true);
            controller.FieldDesk?.Refresh(force: true);
            Assert.That(
                controller.CityGrammarHypothesis,
                Is.EqualTo(
                    LastBearingCityGrammarHypothesis.RestrainedSnapGrid));
            Assert.That(controller.CityGrammarLogisticsConnected, Is.True);
            Assert.That(
                controller.CityGrammarDeliveryStage,
                Is.EqualTo(
                    LastBearingCityTrialDeliveryStage.DeliveredToWorkshop));
            Assert.That(
                controller.CityGrammarPathRead,
                Is.EqualTo(LastBearingCityTrialPathRead.Clear));
            Assert.That(controller.CityGrammarTrialReady, Is.True);
            Assert.That(controller.HasCompletedCityGrammarObservation, Is.True);
            Assert.That(controller.HasPendingPlayerCommands, Is.False);
            Assert.That(controller.FieldDesk?.OwnsCityOverview, Is.True);
        }

        private static IEnumerator WaitForCityCamera(
            LastBearingGameController controller)
        {
            Camera camera = RequireCamera(controller);
            Quaternion targetRotation = Quaternion.Euler(
                LastBearingCameraRig.ComparisonPitch,
                LastBearingCameraRig.ComparisonYaw,
                0f);
            Vector3 targetPosition =
                LastBearingCameraRig.ComparisonFocus -
                targetRotation * Vector3.forward *
                LastBearingCameraRig.ComparisonDistance;
            yield return WaitForCameraPose(
                camera,
                targetPosition,
                targetRotation,
                "city comparison");
        }

        private static IEnumerator WaitForGarageCamera(
            LastBearingGameController controller)
        {
            Assert.That(controller.World, Is.Not.Null);
            Assert.That(controller.World!.CameraRig, Is.Not.Null);
            LastBearingCameraRig rig = controller.World.CameraRig!;
            Assert.That(rig.IsInspectionMode, Is.True);
            Assert.That(rig.InspectionCameraAnchor, Is.Not.Null);
            Assert.That(rig.InspectionFocusAnchor, Is.Not.Null);
            Vector3 targetPosition = rig.InspectionCameraAnchor!.position;
            Vector3 focusDirection =
                rig.InspectionFocusAnchor!.position - targetPosition;
            Quaternion targetRotation = focusDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(focusDirection, Vector3.up)
                : rig.InspectionCameraAnchor.rotation;
            yield return WaitForCameraPose(
                RequireCamera(controller),
                targetPosition,
                targetRotation,
                "garage inspection");
        }

        private static IEnumerator WaitForCameraPose(
            Camera camera,
            Vector3 targetPosition,
            Quaternion targetRotation,
            string label)
        {
            for (var frame = 0; frame < CameraSettleFrames; frame++)
            {
                if (Vector3.Distance(camera.transform.position, targetPosition) <= 0.02f &&
                    Quaternion.Angle(camera.transform.rotation, targetRotation) <= 0.2f)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.That(
                Vector3.Distance(camera.transform.position, targetPosition),
                Is.LessThanOrEqualTo(0.02f),
                label + " camera position did not settle.");
            Assert.That(
                Quaternion.Angle(camera.transform.rotation, targetRotation),
                Is.LessThanOrEqualTo(0.2f),
                label + " camera rotation did not settle.");
        }

        private PendingArtifact AddPendingArtifact(
            string label,
            string state,
            int targetWidth,
            int targetHeight,
            int actualWidth,
            int actualHeight,
            ScrollPosition scrollPosition,
            float scrollOffset,
            bool currentOrderVisible,
            bool serviceStripVisible,
            string variant,
            string derivedFromSha256,
            CameraRecord camera,
            byte[] bytes)
        {
            string sha256 = ComputeSha256(bytes);
            string fileName = label + "-" + actualWidth + "x" + actualHeight +
                              "-" + sha256 + ".png";
            string relativePath = OutputRelativeDirectory + "/" + fileName;
            var pending = new PendingArtifact
            {
                label = label,
                state = state,
                target_width = targetWidth,
                target_height = targetHeight,
                actual_width = actualWidth,
                actual_height = actualHeight,
                scroll_position = scrollPosition.ToString().ToLowerInvariant(),
                scroll_offset_y = scrollOffset,
                current_order_fully_visible = currentOrderVisible,
                service_strip_fully_visible = serviceStripVisible,
                variant = variant,
                derived_from_sha256 = derivedFromSha256,
                sha256 = sha256,
                byte_count = bytes.LongLength,
                relative_path = relativePath,
                captured_at_utc = UtcNow(),
                camera = camera,
                bytes = bytes
            };
            _pending.Add(pending);
            return pending;
        }

        private void AssertExactCaptureMatrix()
        {
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "legacy-title-creator-target|color|2560x1600",
                "city-trial-a-top-1280x720|color|1280x720",
                "city-trial-a-bottom-1280x720|color|1280x720",
                "city-trial-a-top-1920x1200|color|1920x1200",
                "city-trial-a-top-1920x1200-grayscale|grayscale|1920x1200",
                "city-trial-a-top-creator-target-2560x1600|color|2560x1600",
                "legacy-garage-creator-target|color|2560x1600"
            };
            var actual = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < _pending.Count; index++)
            {
                PendingArtifact artifact = _pending[index];
                string key = artifact.label + "|" + artifact.variant + "|" +
                             artifact.target_width + "x" + artifact.target_height;
                Assert.That(actual.Add(key), Is.True, "Duplicate capture: " + key);
                Assert.That(artifact.actual_width, Is.EqualTo(artifact.target_width));
                Assert.That(artifact.actual_height, Is.EqualTo(artifact.target_height));
            }

            Assert.That(_pending, Has.Count.EqualTo(7));
            Assert.That(actual.SetEquals(expected), Is.True);

            PendingArtifact? grayscale = _pending.Find(
                artifact => artifact.variant == "grayscale");
            Assert.That(grayscale, Is.Not.Null);
            Assert.That(IsLowerHex(grayscale!.derived_from_sha256, 64), Is.True);
            Assert.That(
                _pending.Exists(
                    artifact => artifact.sha256 ==
                                grayscale.derived_from_sha256),
                Is.True);
        }

        private ArtifactRecord[] BuildArtifactRecords()
        {
            var records = new ArtifactRecord[_pending.Count];
            for (var index = 0; index < _pending.Count; index++)
            {
                PendingArtifact pending = _pending[index];
                records[index] = new ArtifactRecord
                {
                    label = pending.label,
                    state = pending.state,
                    target_width = pending.target_width,
                    target_height = pending.target_height,
                    actual_width = pending.actual_width,
                    actual_height = pending.actual_height,
                    scroll_position = pending.scroll_position,
                    scroll_offset_y = pending.scroll_offset_y,
                    current_order_fully_visible = pending.current_order_fully_visible,
                    service_strip_fully_visible = pending.service_strip_fully_visible,
                    variant = pending.variant,
                    derived_from_sha256 = pending.derived_from_sha256,
                    sha256 = pending.sha256,
                    byte_count = pending.byte_count,
                    relative_path = pending.relative_path,
                    captured_at_utc = pending.captured_at_utc,
                    camera = pending.camera
                };
            }

            return records;
        }

        private void WriteEvidence(
            string repositoryRoot,
            CaptureManifest manifest)
        {
            string outputDirectory = Path.GetFullPath(Path.Combine(
                repositoryRoot,
                OutputRelativeDirectory));
            string allowedRoot = Path.GetFullPath(Path.Combine(
                repositoryRoot,
                "BuildArtifacts/WP-0002"));
            Assert.That(
                outputDirectory.StartsWith(
                    allowedRoot + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal),
                Is.True);
            Directory.CreateDirectory(outputDirectory);

            for (var index = 0; index < _pending.Count; index++)
            {
                PendingArtifact artifact = _pending[index];
                WriteImmutable(
                    Path.Combine(repositoryRoot, artifact.relative_path),
                    artifact.bytes);
            }

            string json = JsonUtility.ToJson(manifest, prettyPrint: true) + "\n";
            byte[] manifestBytes = new UTF8Encoding(false).GetBytes(json);
            string manifestSha256 = ComputeSha256(manifestBytes);
            string manifestPath = Path.Combine(
                outputDirectory,
                "manifest-" + manifestSha256 + ".json");
            WriteImmutable(manifestPath, manifestBytes);
            Debug.Log(
                "VGR13_VISUAL_CAPTURE_COMPLETE " +
                Path.GetRelativePath(repositoryRoot, manifestPath)
                    .Replace(Path.DirectorySeparatorChar, '/'));
        }

        private static void WriteImmutable(string path, byte[] bytes)
        {
            if (File.Exists(path))
            {
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
                return;
            }

            string? directory = Path.GetDirectoryName(path);
            Assert.That(directory, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(directory!);
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        private static byte[] EncodeGrayscalePng(Texture2D source)
        {
            Color32[] pixels = source.GetPixels32();
            for (var index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                byte luminance = (byte)(
                    (54 * pixel.r + 183 * pixel.g + 19 * pixel.b + 128) >> 8);
                pixels[index] = new Color32(
                    luminance,
                    luminance,
                    luminance,
                    pixel.a);
            }

            var grayscale = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            try
            {
                grayscale.SetPixels32(pixels);
                grayscale.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                return grayscale.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(grayscale);
            }
        }

        private static Texture2D CaptureScreenshotAsTexture()
        {
            Type? screenCapture = Type.GetType(
                "UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule",
                throwOnError: false);
            MethodInfo? capture = screenCapture?.GetMethod(
                "CaptureScreenshotAsTexture",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null);
            Assert.That(screenCapture, Is.Not.Null);
            Assert.That(capture, Is.Not.Null);
            object? result = capture!.Invoke(null, new object[] { 1 });
            Assert.That(result, Is.InstanceOf<Texture2D>());
            return (Texture2D)result!;
        }

        private static bool IsFullyOnScreen(
            Rect? bounds,
            int width,
            int height)
        {
            if (!bounds.HasValue)
            {
                return false;
            }

            Rect value = bounds.Value;
            return value.width > 0f &&
                   value.height > 0f &&
                   value.xMin >= -1f &&
                   value.yMin >= -1f &&
                   value.xMax <= width + 1f &&
                   value.yMax <= height + 1f;
        }

        private static CaptureRequest ParseAndValidateRequest(
            string repositoryRoot,
            byte[] bytes)
        {
            string json = new UTF8Encoding(false, true).GetString(bytes);
            CaptureRequest? request = JsonUtility.FromJson<CaptureRequest>(json);
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.schema_version, Is.EqualTo(1));
            Assert.That(request.capture_set, Is.EqualTo(CaptureSet));
            Assert.That(request.expected_unity_version, Is.EqualTo(Application.unityVersion));
            Assert.That(IsLowerHex(request.source_head, 40), Is.True);
            Assert.That(IsLowerHex(request.source_tree, 40), Is.True);
            Assert.That(IsLowerHex(request.base_commit, 40), Is.True);
            Assert.That(IsLowerHex(request.contract_sha256, 64), Is.True);
            Assert.That(request.request_id, Is.Not.Null.And.Not.Empty);
            Assert.That(request.branch, Does.StartWith("agent/"));
            Assert.That(
                request.contract_path,
                Is.EqualTo("docs/playtests/WP-0002/VGR-13-FIELD-DESK-CONTRACT.md"));
            string contractPath = Path.GetFullPath(Path.Combine(
                repositoryRoot,
                request.contract_path));
            string allowedContractPath = Path.GetFullPath(Path.Combine(
                repositoryRoot,
                "docs/playtests/WP-0002/VGR-13-FIELD-DESK-CONTRACT.md"));
            Assert.That(contractPath, Is.EqualTo(allowedContractPath));
            Assert.That(File.Exists(contractPath), Is.True);
            Assert.That(
                ComputeSha256(File.ReadAllBytes(contractPath)),
                Is.EqualTo(request.contract_sha256));
            Assert.That(
                DateTime.TryParse(
                    request.requested_at_utc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal |
                    DateTimeStyles.AssumeUniversal,
                    out _),
                Is.True);
            return request;
        }

        private static bool IsLowerHex(string? value, int length)
        {
            if (value == null || value.Length != length)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static LastBearingGameController RequireController()
        {
            LastBearingGameController[] controllers =
                UnityEngine.Object.FindObjectsByType<LastBearingGameController>(
                    FindObjectsInactive.Include);
            Assert.That(controllers, Has.Length.EqualTo(1));
            return controllers[0];
        }

        private static LastBearingFieldDesk RequireFieldDesk(
            LastBearingGameController controller)
        {
            Assert.That(controller.FieldDesk, Is.Not.Null);
            Assert.That(controller.FieldDesk!.IsOperational, Is.True);
            return controller.FieldDesk;
        }

        private static UIDocument RequireDocument(
            LastBearingGameController controller)
        {
            UIDocument[] documents =
                controller.GetComponentsInChildren<UIDocument>(true);
            Assert.That(documents, Has.Length.EqualTo(1));
            return documents[0];
        }

        private static Camera RequireCamera(
            LastBearingGameController controller)
        {
            Assert.That(controller.World, Is.Not.Null);
            Assert.That(controller.World!.MainCamera, Is.Not.Null);
            return controller.World.MainCamera!;
        }

        private static CameraRecord CaptureCamera(Camera camera)
        {
            Transform transform = camera.transform;
            return new CameraRecord
            {
                name = camera.name,
                orthographic = camera.orthographic,
                field_of_view = camera.fieldOfView,
                near_clip = camera.nearClipPlane,
                far_clip = camera.farClipPlane,
                position = FormatVector(transform.position),
                euler_angles = FormatVector(transform.eulerAngles),
                clear_flags = camera.clearFlags.ToString()
            };
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:R},{1:R},{2:R}",
                value.x,
                value.y,
                value.z);
        }

        private static string ResolveRepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                ".."));
        }

        private static string UtcNow()
        {
            return DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] digest = sha256.ComputeHash(bytes);
            var builder = new StringBuilder(digest.Length * 2);
            for (var index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private enum ScrollPosition
        {
            None,
            Top,
            Bottom
        }

        [Serializable]
        private sealed class CaptureRequest
        {
            public int schema_version;
            public string capture_set = string.Empty;
            public string request_id = string.Empty;
            public string source_head = string.Empty;
            public string source_tree = string.Empty;
            public string base_commit = string.Empty;
            public string branch = string.Empty;
            public string contract_path = string.Empty;
            public string contract_sha256 = string.Empty;
            public string expected_unity_version = string.Empty;
            public string requested_at_utc = string.Empty;
        }

        [Serializable]
        private sealed class CaptureManifest
        {
            public int schema_version = 1;
            public string packet_id = string.Empty;
            public string slice_id = string.Empty;
            public string capture_set = string.Empty;
            public string request_id = string.Empty;
            public string request_sha256 = string.Empty;
            public string source_head = string.Empty;
            public string source_tree = string.Empty;
            public string base_commit = string.Empty;
            public string branch = string.Empty;
            public string contract_path = string.Empty;
            public string contract_sha256 = string.Empty;
            public string unity_version = string.Empty;
            public string scene = string.Empty;
            public string operator_identity = string.Empty;
            public string requested_at_utc = string.Empty;
            public string captured_at_utc = string.Empty;
            public int original_width;
            public int original_height;
            public string original_full_screen_mode = string.Empty;
            public int display_resolution_width;
            public int display_resolution_height;
            public int creator_target_width;
            public int creator_target_height;
            public string creator_target_semantics = string.Empty;
            public string quality_level = string.Empty;
            public string color_space = string.Empty;
            public float render_scale_width;
            public float render_scale_height;
            public string graphics_api = string.Empty;
            public string graphics_device = string.Empty;
            public string operating_system = string.Empty;
            public string processor = string.Empty;
            public int system_memory_mb;
            public string bloom_state = string.Empty;
            public string grayscale_conversion = string.Empty;
            public string save_interaction = string.Empty;
            public string city_canonical_sha256_before = string.Empty;
            public string city_canonical_sha256_after = string.Empty;
            public string[] known_limits = Array.Empty<string>();
            public string[] state_sequence = Array.Empty<string>();
            public CameraRecord camera = new CameraRecord();
            public ArtifactRecord[] artifacts = Array.Empty<ArtifactRecord>();
        }

        [Serializable]
        private sealed class CameraRecord
        {
            public string name = string.Empty;
            public bool orthographic;
            public float field_of_view;
            public float near_clip;
            public float far_clip;
            public string position = string.Empty;
            public string euler_angles = string.Empty;
            public string clear_flags = string.Empty;
        }

        [Serializable]
        private class ArtifactRecord
        {
            public string label = string.Empty;
            public string state = string.Empty;
            public int target_width;
            public int target_height;
            public int actual_width;
            public int actual_height;
            public string scroll_position = string.Empty;
            public float scroll_offset_y;
            public bool current_order_fully_visible;
            public bool service_strip_fully_visible;
            public string variant = string.Empty;
            public string derived_from_sha256 = string.Empty;
            public string sha256 = string.Empty;
            public long byte_count;
            public string relative_path = string.Empty;
            public string captured_at_utc = string.Empty;
            public CameraRecord camera = new CameraRecord();
        }

        private sealed class PendingArtifact : ArtifactRecord
        {
            public byte[] bytes = Array.Empty<byte>();
        }
    }
}
