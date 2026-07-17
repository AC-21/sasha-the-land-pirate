#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtomicLandPirate.Presentation.LastBearing.RoadFeel
{
    /// <summary>
    /// Keeps the authored RoadFeelLab scene empty and materializes its
    /// presentation-only course when that exact scene is loaded.
    /// </summary>
    public static class RoadFeelLabBootstrap
    {
        public const string SceneName = "RoadFeelLab";

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (scene.name != SceneName ||
                Object.FindAnyObjectByType<RoadFeelLabController>() != null)
            {
                return;
            }

            var root = new GameObject(RoadFeelLabController.RuntimeRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<RoadFeelLabController>();
        }
    }
}
