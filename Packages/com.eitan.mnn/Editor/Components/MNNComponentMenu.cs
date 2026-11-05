using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Shared, undoable component commands for MNN and imported sample menus.</summary>
    public static class MNNComponentMenu
    {
        [MenuItem("GameObject/MNN/Inference/Inference Component", false, 10)]
        private static void AddInferenceComponent(MenuCommand command) => AddToSelection<MNNInferenceComponent>(command);

        [MenuItem("GameObject/MNN/Models/Model Loader", false, 10)]
        private static void AddModelLoader(MenuCommand command) => AddToSelection<MNNModelLoader>(command);

        [MenuItem("GameObject/MNN/Models/Model Manager", false, 10)]
        private static void AddModelManager(MenuCommand command) => AddToSelection<MNNModelManager>(command);

        [MenuItem("GameObject/MNN/Realtime/Audio Device", false, 10)]
        private static void AddRealtimeAudioDevice(MenuCommand command) => AddToSelection<MNNRealtimeAudioDevice>(command);

        [MenuItem("GameObject/MNN/Realtime/Session", false, 10)]
        private static void AddRealtimeSession(MenuCommand command) => AddToSelection<MNNRealtimeSessionComponent>(command);

        [MenuItem("GameObject/MNN/Realtime/Unity Microphone Capture", false, 10)]
        private static void AddUnityMicrophoneCapture(MenuCommand command) => AddToSelection<MNNUnityMicrophoneCapture>(command);

        [MenuItem("GameObject/MNN/Realtime/Speech Output Filter", false, 10)]
        private static void AddSpeechOutputFilter(MenuCommand command) => AddToSelection<MNNSpeechOutputFilter>(command);

        [MenuItem("GameObject/MNN/Realtime/Unity Echo Reference Tap", false, 10)]
        private static void AddUnityEchoReferenceTap(MenuCommand command) => AddToSelection<MNNUnityEchoReferenceTap>(command);

        /// <summary>
        /// Adds once per editable scene object. An unselected context targets only that object;
        /// otherwise multi-selection is preserved. With no scene target, creates in the current stage.
        /// Never edits prefab assets in the Project window. The whole command is one Undo step.
        /// </summary>
        public static void AddToSelection<T>(MenuCommand command) where T : Component
        {
            var targets = Selection.gameObjects;
            var context = command?.context as GameObject;
            if (IsEditable(context) && !Selection.Contains(context))
                targets = new[] { context };

            bool hasTarget = false;
            bool needsComponent = false;
            foreach (var target in targets)
            {
                if (!IsEditable(target))
                    continue;
                hasTarget = true;
                needsComponent |= target.GetComponent<T>() == null;
            }
            if (hasTarget && !needsComponent)
                return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add " + typeof(T).Name);
            try
            {
                if (!hasTarget)
                {
                    var created = new GameObject(typeof(T).Name);
                    StageUtility.PlaceGameObjectInCurrentStage(created);
                    Undo.RegisterCreatedObjectUndo(created, "Create MNN GameObject");
                    targets = new[] { created };
                    Selection.activeGameObject = created;
                }

                foreach (var target in targets)
                {
                    if (IsEditable(target) && target.GetComponent<T>() == null)
                        Undo.AddComponent<T>(target);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }

        private static bool IsEditable(GameObject target)
        {
            return target != null && target.scene.IsValid() && !EditorUtility.IsPersistent(target) &&
                (target.hideFlags & HideFlags.NotEditable) == 0;
        }
    }
}
