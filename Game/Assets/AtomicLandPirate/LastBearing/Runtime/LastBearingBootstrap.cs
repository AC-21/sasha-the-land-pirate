#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;

namespace AtomicLandPirate.Presentation.LastBearing
{
    public static class LastBearingBootstrap
    {
        public const string SceneName = "LastBearing";

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
                Object.FindAnyObjectByType<LastBearingGameController>() != null)
            {
                return;
            }

            var root = new GameObject(LastBearingGameController.RuntimeRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<LastBearingGameController>();
        }
    }
}
