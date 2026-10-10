using System.IO;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private const float SidebarWidth = 208;
        [SerializeField]
        private bool _sidebarOpen = true;
        private MNNStudioHistory _history = new MNNStudioHistory();
        private string _historyPath, _activeChatId;
        private Vector2 _sidebarScroll;
        private Rect _workspaceRect;
        private float WorkspaceWidth => position.width - (_sidebarOpen ? SidebarWidth : 0);
        private void DrawSidebar()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true)))
            {
                var heading = GUILayoutUtility.GetRect(SidebarWidth, 60, GUILayout.ExpandWidth(false));
                GUI.Label(new Rect(heading.x + 16, heading.y + 16, SidebarWidth - 60, 28), "MNN Studio", new GUIStyle(MNNStudioUI.Heading)
                {alignment = TextAnchor.MiddleLeft});
                if (MNNStudioUI.ButtonAt(new Rect(heading.xMax - 40, heading.y + 16, 28, 28), "‹", quiet: true, tooltip: "Hide sidebar"))
                    _sidebarOpen = false;
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(10);
                    using (new EditorGUI.DisabledScope(Busy))
                        if (MNNStudioUI.Navigation("＋   New chat", false, SidebarWidth - 20))
                            StartNewChat();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(10);
                    using (new EditorGUI.DisabledScope(Busy))
                        if (MNNStudioUI.Navigation("⌕   Search chats", false, SidebarWidth - 20, "Search titles and messages (⌘/Ctrl K)"))
                            OpenSearch();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(10);
                    using (new EditorGUI.DisabledScope(Busy))
                        if (MNNStudioUI.Navigation("Tasks & models", _taskIndex == (int)MNNStudioTask.Other, SidebarWidth - 20, "Browse tasks and prepare their models"))
                            SelectTask((int)MNNStudioTask.Other);
                }

                GUILayout.Space(16);
                using (var scroll = new EditorGUILayout.ScrollViewScope(_sidebarScroll, GUILayout.ExpandHeight(true)))
                {
                    _sidebarScroll = scroll.scrollPosition;
                    DrawLibrary();
                }

                GUILayout.Space(12);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(20);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label("On device", MNNStudioUI.Body);
                        GUILayout.Space(4);
                        GUILayout.Label("Models run on your device", MNNStudioUI.Caption);
                    }

                    GUILayout.Space(12);
                }

                GUILayout.Space(20);
            }
        }

        private static void SidebarCaption(string text)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(22);
                GUILayout.Label(text, MNNStudioUI.Caption, GUILayout.Width(SidebarWidth - 38));
            }
        }

        private void SelectTask(int task)
        {
            if (Busy || task == _taskIndex || task < 0 || task >= TaskNames.Length)
                return;
            RememberConversation();
            ResetWorkspaceForModelChange();
            _activeChatId = null;
            _taskIndex = task;
            FilterModels();
            _focusComposer = task == 0;
            _query = TaskExample;
        }

        private void StartNewChat()
        {
            if (Busy)
                return;
            RememberConversation();
            ResetWorkspace();
            _activeChatId = null;
            if (_taskIndex != 0)
            {
                _taskIndex = 0;
                FilterModels();
            }

            _focusComposer = true;
        }

        private void RememberConversation()
        {
            if (_taskIndex != 0 || (_messages.Count == 0 && string.IsNullOrWhiteSpace(_draft) && string.IsNullOrWhiteSpace(_imagePath) && string.IsNullOrWhiteSpace(_audioPath)))
                return;
            string model = Selected?.Directory ?? _history.Conversations.Find(item => item.Id == _activeChatId)?.ModelDirectory;
            var chat = MNNStudioConversation.Capture(_activeChatId, model, _messages, _draft, _imagePath, _audioPath, _contextSummary, _summarizedMessages);
            _activeChatId = chat.Id;
            _history.Remember(chat);
            if (!string.IsNullOrEmpty(_historyPath))
                Try(() => _history.Save(_historyPath));
        }

        private void OpenConversation(string id)
        {
            if (Busy || (_taskIndex == 0 && id == _activeChatId))
                return;
            var chat = _history.Conversations.Find(item => item.Id == id);
            if (chat == null)
                return;
            string currentModel = Selected?.Directory;
            RememberConversation();
            if (currentModel != chat.ModelDirectory)
                ReleaseSession();
            ResetWorkspace();
            _taskIndex = 0;
            FilterModels();
            // Never silently run a restored conversation against a different model.
            int model = _available.FindIndex(item => item.Directory == chat.ModelDirectory);
            _modelIndex = model < 0 ? -1 : model;
            _activeChatId = chat.Id;
            _messages.AddRange(chat.RestoreMessages());
            _draft = chat.Draft ?? "";
            _contextSummary = MNNStudioPrompt.BoundSummary(chat.ContextSummary);
            _summarizedMessages = chat.SummarizedMessages;
            _imagePath = File.Exists(chat.ImagePath) ? chat.ImagePath : null;
            _audioPath = File.Exists(chat.AudioPath) ? chat.AudioPath : null;
            if (model < 0)
                _error = "The model used by this conversation is unavailable. Choose a downloaded model to continue.";
            _scroll.y = float.MaxValue;
            _focusComposer = true;
        }

        private void ShowTaskMenu(Rect anchor)
        {
            var menu = new GenericMenu();
            for (int i = 0; i < TaskNames.Length; ++i)
            {
                int task = i;
                var info = MNNStudioTasks.Get(i);
                string label = info.Group + "/" + info.Name.Replace("/", "∕") + (info.Runnable ? "" : " (model setup)");
                if (Busy)
                    menu.AddDisabledItem(new GUIContent(label));
                else
                    menu.AddItem(new GUIContent(label), i == _taskIndex, () => SelectTask(task));
            }

            menu.DropDown(anchor);
        }

        private void ShowModelMenu(Rect anchor)
        {
            var menu = new GenericMenu();
            for (int i = 0; i < _available.Count; ++i)
            {
                int index = i;
                menu.AddItem(new GUIContent(_available[i].DisplayName.Replace("/", "∕")), i == _modelIndex, () =>
                {
                    SelectModel(index);
                });
            }

            if (_available.Count > 0)
                menu.AddSeparator("");
            menu.AddItem(new GUIContent("Find models for this task…"), false, () => MNNModelManagerWindow.ShowForTask(CurrentTask));
            menu.AddItem(new GUIContent("Choose local model folder…"), false, PickTaskModel);
            menu.AddItem(new GUIContent("Manage all models…"), false, MNNModelManagerWindow.ShowWindow);
            menu.DropDown(anchor);
        }

        private void ShowAttachmentMenu(Rect anchor)
        {
            var menu = new GenericMenu();
            if (Selected.Supports(MNNModelCapabilities.Vision))
                menu.AddItem(new GUIContent("Attach image…"), false, () => PickAttachment(true));
            if (Selected.Supports(MNNModelCapabilities.AudioInput))
                menu.AddItem(new GUIContent("Attach audio…"), false, () => PickAttachment(false));
            menu.DropDown(anchor);
        }

        private void ShowVoiceMenu(Rect anchor)
        {
            var menu = new GenericMenu();
            if (Busy || _voiceLoop)
                menu.AddDisabledItem(new GUIContent("Spoken replies"), _speech);
            else
                menu.AddItem(new GUIContent("Spoken replies"), _speech, () => SetSpokenReplies(!_speech));
            menu.AddItem(new GUIContent("Voice conversation"), _voiceLoop, () =>
            {
                if (_voiceLoop)
                    EndVoice();
                else
                    BeginVoice();
            });
            menu.DropDown(anchor);
        }

        private void SetSpokenReplies(bool enabled)
        {
            _speech = enabled;
            EditorPrefs.SetBool("MNN.Studio.SpokenReplies", enabled);
            Repaint();
        }
    }
}
