using UnityEditor;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private MNNStudioSearchWindow _searchWindow;
        private int _jumpMessageIndex = -1, _highlightMessageIndex = -1;
        private float _jumpScroll = -1;
        private double _highlightUntil;
        private void OpenSearch()
        {
            if (Busy || (_audio != null && _audio.IsRecording))
                return;
            RememberConversation();
            if (_searchWindow != null)
                _searchWindow.Close();
            _searchWindow = MNNStudioSearchWindow.Open(position, _history, JumpToSearchHit);
        }

        private void JumpToSearchHit(MNNStudioSearchHit hit)
        {
            if (Busy)
                return;
            EndVoice();
            OpenConversation(hit.ChatId);
            if (_activeChatId != hit.ChatId)
                return;
            _focusComposer = false;
            if (hit.MessageIndex >= 0 && hit.MessageIndex < _messages.Count)
            {
                _followOutput = false;
                _jumpMessageIndex = _highlightMessageIndex = hit.MessageIndex;
                _highlightUntil = EditorApplication.timeSinceStartup + 8;
            }

            Focus();
            Repaint();
        }
    }
}
