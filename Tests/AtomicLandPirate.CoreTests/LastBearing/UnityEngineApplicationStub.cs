#nullable enable

namespace UnityEngine
{
    public static class Application
    {
        private static string _persistentDataPath = string.Empty;

        public static int PersistentDataPathReadCount { get; private set; }

        public static string persistentDataPath
        {
            get
            {
                PersistentDataPathReadCount++;
                return _persistentDataPath;
            }
        }

        public static void SetPersistentDataPathForTests(string value)
        {
            _persistentDataPath = value;
            PersistentDataPathReadCount = 0;
        }
    }
}
