using UnityEditor;

namespace MNN.Unity.Editor
{
    internal static class MNNInferenceCacheInitialization
    {
        [InitializeOnLoadMethod]
        private static void Initialize() => MNNRuntime.Initialize();
    }
}
