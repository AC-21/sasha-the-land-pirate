#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;

namespace AC21.Sasha.TechnicalSandbox
{
    internal static class TechnicalSandboxBootstrap
    {
        internal const string SceneName = "WP0003_TechnicalSandbox";

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

        private static void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode loadMode)
        {
            if (scene.name != SceneName ||
                Object.FindAnyObjectByType<TechnicalSandboxController>() !=
                null)
            {
                return;
            }

            var root = new GameObject("WP-0003 Technical Sandbox Runtime");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<TechnicalSandboxController>();
        }
    }
}
