namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioModel
    {
        internal void PrepareForDisplay(MNNStudioTask task)
        {
            // Prepare availability used by the current workspace and voice selectors.
            // Avoid validating unrelated pipelines merely to open the chat workspace.
            // Piper's dictionary override is an EditorPrefs setting and stays on the
            // Editor thread; its small directory check must not read prefs on a worker.
            if (!IsPiper)
            {
                _ = MNNStudioTasks.Get((int)(task == MNNStudioTask.Other ? Task : task)).CanRun(this);
                if (Task == MNNStudioTask.SpeechSynthesis)
                    _ = HasGenerationPipeline;
                if (Task == MNNStudioTask.SpeechRecognition)
                    _ = HasSpeechRecognitionPipeline;
            }
        }
    }
}
