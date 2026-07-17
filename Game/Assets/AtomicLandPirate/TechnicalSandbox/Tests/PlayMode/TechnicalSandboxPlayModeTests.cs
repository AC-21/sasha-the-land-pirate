#nullable enable

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AC21.Sasha.TechnicalSandbox.Tests
{
    public sealed class TechnicalSandboxPlayModeTests : InputTestFixture
    {
        private const string SceneName = "WP0003_TechnicalSandbox";

        [UnityTearDown]
        public IEnumerator RemoveSandboxRuntime()
        {
            var sandboxScene = SceneManager.GetSceneByName(SceneName);
            if (sandboxScene.IsValid() && sandboxScene.isLoaded)
            {
                var cleanupScene = SceneManager.CreateScene(
                    "WP0003_TechnicalSandbox_TestCleanup");
                SceneManager.SetActiveScene(cleanupScene);
                var unload = SceneManager.UnloadSceneAsync(sandboxScene);
                Assert.That(unload, Is.Not.Null);
                yield return unload;
            }
            else
            {
                foreach (var controller in Object.FindObjectsByType<
                             TechnicalSandboxController>())
                {
                    Object.Destroy(controller.gameObject);
                }

                yield return null;
            }

            yield return null;

            Assert.That(
                Object.FindObjectsByType<TechnicalSandboxController>(),
                Is.Empty);
        }

        [UnityTest]
        public IEnumerator SceneBootsAndRespondsToInputSystemControls()
        {
            var load = SceneManager.LoadSceneAsync(
                SceneName,
                LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;

            yield return null;

            var controllers = Object.FindObjectsByType<
                TechnicalSandboxController>();
            Assert.That(controllers, Has.Length.EqualTo(1));
            var controller = controllers[0];

            Assert.That(
                controller.ProbeCount,
                Is.EqualTo(TechnicalProbeState.SupportedProbeCount));
            Assert.That(controller.SandboxCamera, Is.Not.Null);

            var camera = controller.SandboxCamera!;
            Physics.SyncTransforms();
            var visibleMarker = FindVisibleMarkerOutsideHud(camera);
            Assert.That(visibleMarker, Is.Not.Null);

            var mouse = InputSystem.AddDevice<Mouse>();
            var markerScreenPoint = camera.WorldToScreenPoint(
                visibleMarker!.transform.position);
            Set(
                mouse.position,
                new Vector2(markerScreenPoint.x, markerScreenPoint.y));
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;

            Assert.That(
                controller.State.SelectedProbeIndex,
                Is.EqualTo(visibleMarker.ProbeIndex));
            Assert.That(controller.State.InteractionCount, Is.EqualTo(1));

            var rig = camera.GetComponent<TechnicalSandboxCameraRig>();
            Assert.That(rig, Is.Not.Null);
            var originalFocus = rig.Focus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.dKey);
            yield return null;
            Release(keyboard.dKey);
            yield return null;
            Assert.That(rig.Focus, Is.Not.EqualTo(originalFocus));

            var originalDistance = rig.Distance;
            Set(mouse.scroll, new Vector2(0f, 120f));
            yield return null;
            Assert.That(rig.Distance, Is.LessThan(originalDistance));

            var persistence = controller.AttemptPersistence();
            Assert.That(persistence.Succeeded, Is.False);
            Assert.That(persistence.BytesWritten, Is.Zero);
        }

        private static TechnicalProbeMarker? FindVisibleMarkerOutsideHud(
            Camera camera)
        {
            foreach (var marker in Object.FindObjectsByType<
                         TechnicalProbeMarker>())
            {
                var screenPoint = camera.WorldToScreenPoint(
                    marker.transform.position);
                if (screenPoint.z <= 0f)
                {
                    continue;
                }

                var pointerOverHud =
                    screenPoint.x <= 390f &&
                    screenPoint.y >= Screen.height - 288f;
                if (pointerOverHud)
                {
                    continue;
                }

                var ray = camera.ScreenPointToRay(screenPoint);
                if (Physics.Raycast(ray, out var hit, 250f) &&
                    hit.collider.GetComponent<TechnicalProbeMarker>() ==
                    marker)
                {
                    return marker;
                }
            }

            return null;
        }
    }
}
