using System;
using UnityEditor;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void EnsureSession()
        {
            if (!CurrentTask.CanRun(Selected))
                throw new NotSupportedException("Choose a runnable model for " + CurrentTask.Name + ". " + CurrentTask.UnavailableReason);
            if (_session != null && !string.IsNullOrEmpty(_sessionModelDirectory) && _sessionModelDirectory != Selected?.Directory)
                ReleaseSession();
            if (_session != null)
                return;
            // Runtime loading must occur on the Unity main thread. Reuse the loaded model
            // for subsequent requests; only inference runs on the worker.
            _session = new MNNStudioSession(MNNStudioNativeBackend.Load(Selected, _threads, _thinking));
            _sessionModelDirectory = Selected.Directory;
            _sessionLastUsed = EditorApplication.timeSinceStartup;
            _session.Completed += OnCompleted;
        }

        private void ReleaseSession()
        {
            var session = _session;
            _session = null;
            _sessionModelDirectory = null;
            _sessionLastUsed = 0;
            session?.Dispose();
        }

        private void SelectModel(int index)
        {
            if (Busy || index < 0 || index >= _available.Count || index == _modelIndex)
                return;
            string previousExample = TaskExample;
            StopVoiceLoop();
            ReleaseSession();
            _modelIndex = index;
            _ttsVoice = 0;
            if (string.IsNullOrWhiteSpace(_query) || _query == previousExample)
                _query = TaskExample;
            _retrieval = null;
            _taskResultMessage = null;
            _markdown.Clear();
            ClearGeneratedImage();
            if (!Selected.Supports(MNNModelCapabilities.Vision))
                _imagePath = null;
            if (!Selected.Supports(MNNModelCapabilities.AudioInput))
                _audioPath = null;
            _error = null;
            _focusComposer = true;
            RememberConversation();
            Repaint();
        }

        private bool TryUnloadIdleModel(double now)
        {
            if (_session == null || Busy || _voiceLoop || _namingChatId != null || now - _sessionLastUsed < IdleModelUnloadSeconds)
                return false;
            ReleaseSession();
            return true;
        }
    }
}
