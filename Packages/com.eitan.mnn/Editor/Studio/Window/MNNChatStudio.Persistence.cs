using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void ResetWorkspace() => ResetWorkspaceCore(false);
        private void ResetWorkspaceForModelChange() => ResetWorkspaceCore(true);
        private void ResetWorkspaceCore(bool releaseSession)
        {
            if (Busy)
                return;
            StopVoiceLoop();
            if (releaseSession)
                ReleaseSession();
            _messages.Clear();
            _retrieval = null;
            _error = null;
            _voicePanel = false;
            _voiceMuted = false;
            _streamText = "";
            _streamMarkdown = null;
            _followOutput = true;
            _contextSummary = "";
            _summarizedMessages = 0;
            _contextCompressionPending = false;
            _jumpMessageIndex = _highlightMessageIndex = -1;
            _jumpScroll = -1;
            _draft = "";
            _imagePath = _audioPath = _aecReferencePath = null;
            _avFramesDirectory = null;
            _scroll = Vector2.zero;
            _composerRect = Rect.zero;
            _taskResultMessage = null;
            _query = _document = "";
            _toolScroll = Vector2.zero;
            _taskSpeech = false;
            // Recorded files may still be referenced by a saved draft in this window.
            // They are cleaned when the window closes, after native reads finish.
            ClearPreviews();
            ClearGeneratedImage();
        }

        private void SaveSpeech(MNNStudioResult result)
        {
            ShowDialogAfterGUI(() =>
            {
                string path = EditorUtility.SaveFilePanel("Save generated speech", "", "mnn-speech.wav", "wav");
                if (!string.IsNullOrEmpty(path))
                    File.WriteAllBytes(path, MNNStudioAudio.EncodeWave(result.Waveform, result.SampleRate));
            });
        }

        private void ExportConversation()
        {
            ShowDialogAfterGUI(() =>
            {
                string path = EditorUtility.SaveFilePanel("Export conversation", "", "mnn-chat.md", "md");
                if (string.IsNullOrEmpty(path))
                    return;
                var text = new StringBuilder("# MNN Studio\n\n");
                foreach (var message in _messages)
                {
                    text.Append(message.IsUser ? "## You\n\n" : "## Assistant\n\n").Append(message.Text).Append("\n\n");
                    if (message.ImagePath != null)
                        text.Append("Image: ").Append(message.ImagePath).Append("\n\n");
                    if (message.AudioPath != null)
                        text.Append("Audio: ").Append(message.AudioPath).Append("\n\n");
                }

                File.WriteAllText(path, text.ToString());
            });
        }
    }
}
