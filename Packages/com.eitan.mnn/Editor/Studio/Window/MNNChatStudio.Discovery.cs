using System.IO;
using UnityEngine;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private MNNStudioModelDiscovery _modelDiscovery;
        private bool DiscoveringModels => _modelDiscovery?.IsRunning == true;

        private void RefreshModels()
        {
            _modelDiscovery.Refresh(new[]
            {
                Path.Combine(Application.streamingAssetsPath, "MNN", "Models"),
                Path.Combine(Application.persistentDataPath, "MNN", "Models"),
                _customRoot
            }, CurrentTask.Task);
            Repaint();
        }

        private void PollModelDiscovery()
        {
            if (DiscoveringModels && _modelDiscovery.WorkspaceTask != CurrentTask.Task)
                RefreshModels();
            if (_modelDiscovery == null || !_modelDiscovery.TryTake(out var snapshot))
                return;
            if (snapshot?.Models == null)
            {
                if (snapshot?.Error != null)
                    _error = snapshot.Error;
                Repaint();
                return;
            }
            // Use the selection at publication time: a user can switch tasks or choose
            // another known model while a refresh is running. Workers never edit UI state.
            string previous = Selected?.Directory ?? _history.Conversations.Find(item => item.Id == _activeChatId)?.ModelDirectory;
            _models = snapshot.Models;
            if (snapshot.Error != null)
                _error = snapshot.Error;
            FilterModels();
            RefreshVoiceModels();
            RestoreDiscoveredSelection(previous);
            Repaint();
        }

        private void RestoreDiscoveredSelection(string previous)
        {
            if (previous != null)
            {
                _modelIndex = _available.FindIndex(model => model.Directory == previous);
                if (_modelIndex < 0)
                    _error = "The current model is unavailable. Choose a downloaded model to continue.";
            }
            _transcriptRows.Clear();
            if (_session != null && _sessionModelDirectory != Selected?.Directory)
                ReleaseSession();
        }
    }
}
