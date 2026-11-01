using MNN.Unity.Editor;
using UnityEditor;

namespace MNN.Examples.Editor
{
    internal static class MNNExampleMenu
    {
        [MenuItem("GameObject/MNN/Examples/Basic Status Example", false, 10)]
        private static void AddBasicExample(MenuCommand command) => MNNComponentMenu.AddToSelection<MNNBasicExample>(command);

        [MenuItem("GameObject/MNN/Examples/Inference Example", false, 10)]
        private static void AddInferenceExample(MenuCommand command) => MNNComponentMenu.AddToSelection<MNNInferenceExample>(command);

        [MenuItem("GameObject/MNN/Examples/Unified Inference Example", false, 10)]
        private static void AddUnifiedExample(MenuCommand command) => MNNComponentMenu.AddToSelection<MNNUnifiedExample>(command);
    }
}
