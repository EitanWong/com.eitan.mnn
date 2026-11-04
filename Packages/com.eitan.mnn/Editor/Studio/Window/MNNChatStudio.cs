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
    /// <summary>Local developer workspace. Main-thread IMGUI and media services are isolated
    /// from the worker-owned inference session and reusable model/task components.</summary>
    public sealed partial class MNNChatStudio : EditorWindow
    {
        private static readonly string[] TaskNames = MNNStudioTasks.All.Select(task => task.Name).ToArray();
        private MNNStudioTaskInfo CurrentTask => MNNStudioTasks.Get(_taskIndex);
        private readonly List<MNNStudioMessage> _messages = new List<MNNStudioMessage>();
        private readonly Dictionary<string, Texture2D> _previews = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _failedPreviews = new HashSet<string>();
        private readonly Dictionary<MNNStudioMessage, MNNStudioMarkdown> _markdown = new Dictionary<MNNStudioMessage, MNNStudioMarkdown>();
        private readonly List<string> _recordings = new List<string>();
        private List<MNNStudioModel> _models = new List<MNNStudioModel>(), _available = new List<MNNStudioModel>();
        private string[] _modelNames = Array.Empty<string>();
        private MNNStudioSession _session;
        private string _sessionModelDirectory, _contextSummary;
        private MNNStudioAudio _audio;
        private MNNStudioResult _retrieval;
        private Vector2 _scroll, _toolScroll;
        private string _draft = "", _query = "", _document = "", _imagePath, _audioPath, _error, _customRoot;
        private int _taskIndex, _modelIndex, _tokenLimit = 256, _threads = 4;
        private bool _focusComposer, _thinking, _speech, _voiceLoop, _discarded, _contextCompressionPending;
        private double _nextListen, _lastRepaint, _sessionLastUsed;
        private const double IdleModelUnloadSeconds = 300;
        private int _summarizedMessages, _contextTokens = MNNStudioContext.DefaultTokenBudget;
        private MNNStudioPopover _settingsPopup;
        private Rect _composerRect, _conversationRect;
        private bool _mediaDragActive;
        private bool _mediaDropAccepted;
        private string _mediaDropHint;
        private Vector2 _errorScroll;
        private bool _followOutput = true;
        private double _nextStreamLayout;
        private float ContentWidth => MNNStudioUI.ContentWidth(WorkspaceWidth);
        private bool CanSend => !Busy && Selected != null && _audio != null && !_audio.IsRecording && (!string.IsNullOrWhiteSpace(_draft) || !string.IsNullOrWhiteSpace(_imagePath) || !string.IsNullOrWhiteSpace(_audioPath));
        private bool Busy => _voiceConversation != null || _stoppingVoice != null || _keywordListener != null || _session != null && _session.Busy;
        private MNNStudioModel Selected => _modelIndex >= 0 && _modelIndex < _available.Count ? _available[_modelIndex] : null;
        [MenuItem("Window/MNN/Chat Studio %#m")]
        public static void Open()
        {
            var window = GetWindow<MNNChatStudio>();
            window.titleContent = new GUIContent("MNN Studio");
            window.minSize = new Vector2(760, 540);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            _taskIndex = Mathf.Clamp(_taskIndex, 0, TaskNames.Length - 1);
            titleContent = new GUIContent("MNN Studio");
            minSize = new Vector2(760, 540);
            _audio = new MNNStudioAudio();
            _reduceMotion = EditorPrefs.GetBool("MNN.Studio.ReduceMotion", false);
            _speech = EditorPrefs.GetBool("MNN.Studio.SpokenReplies", true);
            // Unity's window serialization can restore null strings as empty strings.
            if (string.IsNullOrWhiteSpace(_imagePath))
                _imagePath = null;
            if (string.IsNullOrWhiteSpace(_audioPath))
                _audioPath = null;
            _namingChatId = _dragChatId = _dragFolderId = null;
            if (string.IsNullOrEmpty(_activeChatId))
                _activeChatId = null;
            _draft = _draft ?? "";
            _customRoot = EditorPrefs.GetString("MNN.Studio.ModelRoot", "");
            RefreshModels();
            _historyPath = Path.GetFullPath(Path.Combine("Library", "MNN", "Studio", "history.json"));
            try
            {
                _history = MNNStudioHistory.Load(_historyPath);
            }
            catch (Exception error)
            {
                _history = new MNNStudioHistory();
                _error = "Unable to read local conversations: " + error.Message;
            }

            EditorApplication.update += Tick;
            MNNModelDownloadTasks.instance.Changed += OnStudioDownloadsChanged;
        }

        private void OnDisable()
        {
            StopKeywordListening();
            EditorApplication.update -= Tick;
            MNNModelDownloadTasks.instance.Changed -= OnStudioDownloadsChanged;
            _session?.FlushMemoryCheckpoint();
            if (_session != null)
            {
                _contextSummary = _session.MemorySummary;
                _summarizedMessages = _session.MemoryCovered;
            }

            RememberConversation();
            _settingsPopup?.editorWindow?.Close();
            _settingsPopup = null;
            if (_searchWindow != null)
                _searchWindow.Close();
            StopVoiceLoop();
            _audio?.Dispose();
            _audio = null;
            if (_session != null)
            {
                // Keep recorded input files alive until any native media read has finished.
                if (_session.Busy)
                    _session.Released += CleanupRecordings;
                else
                    CleanupRecordings();
                _session.Dispose();
                _session = null;
            }
            else
                CleanupRecordings();
            _sessionModelDirectory = null;
            _sessionLastUsed = 0;
            ClearPreviews();
            ClearGeneratedImage();
        }

        private void Tick()
        {
            if (_audio == null)
                return;
            if (_downloadsDirty && !Busy)
            {
                _downloadsDirty = false;
                int completed = MNNModelDownloadTasks.instance.Jobs.Count(job => job.State == MNNDownloadState.Completed);
                if (completed != _completedDownloads)
                {
                    _completedDownloads = completed;
                    RefreshKeepingConversation();
                }
            }

            string playbackError = _audio.ConsumePlaybackError();
            if (!string.IsNullOrEmpty(playbackError))
                _error = "Unable to play generated speech: " + playbackError;
            double now = EditorApplication.timeSinceStartup;
            if (_highlightMessageIndex >= 0 && now >= _highlightUntil)
            {
                _highlightMessageIndex = -1;
                Repaint();
            }

            TickKeywordListening();
            TickVoice(now);
            if (_session != null && Busy && _namingChatId == null)
            {
                _session.FlushMemoryCheckpoint();
                _contextSummary = _session.MemorySummary;
                _summarizedMessages = _session.MemoryCovered;
            }

            if (_audio.IsRecording && CurrentTask.Task == MNNStudioTask.SpeechSynthesis && Selected?.RequiresReferenceAudio == true && _audio.RecordingSeconds >= 15)
                FinishRecording(false);
            if (_audio.CaptureFinished)
                FinishRecording(_voiceLoop);
            if (_session != null && _session.Busy && _namingChatId == null)
                _contextCompressionPending = _session.CompressingContext;
            if (Busy && _namingChatId == null && now >= _nextStreamLayout)
            {
                _nextStreamLayout = now + .05;
                OnStreamUpdated();
            }

            if (!Busy && !_voiceLoop && _autoTitle)
                QueueTitle(_activeChatId);
            if (TryUnloadIdleModel(now))
                Repaint();
            if ((Busy || _audio.IsRecording || _nextListen > 0 || _voicePanel || _audio.IsPlaying) && now - _lastRepaint > ((_voicePanel || _audio.IsRecording || _audio.IsPlaying) && !_reduceMotion ? 1.0 / 30 : .05))
            {
                _lastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void RefreshModels()
        {
            var errors = new List<string>();
            _models = MNNStudioModel.Discover(new[]{Path.Combine(Application.streamingAssetsPath, "MNN", "Models"), Path.Combine(Application.persistentDataPath, "MNN", "Models"), _customRoot}, errors);
            _error = errors.Count > 0 ? string.Join("\n", errors) : null;
            FilterModels();
            RefreshVoiceModels();
        }

        private void FilterModels()
        {
            _available = _models.Where(CurrentTask.Accepts).ToList();
            // Prefer a runnable model over a preparation-only resource.
            _available = _available.OrderByDescending(CurrentTask.CanRun).ToList();
            _modelNames = _available.Select(model => model.DisplayName).ToArray();
            _modelIndex = 0;
        }

        private void OnGUI()
        {
            // Unity can deliver a final repaint after OnDisable / during reload.
            if (_audio == null)
                return;
            ResetLibraryDragOrigin();
            HandleDrop(_composerRect);
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), MNNStudioUI.Background);
            HandleKeyboard();
            bool sidebar = _sidebarOpen;
            if (sidebar)
                EditorGUI.DrawRect(new Rect(0, 0, SidebarWidth, position.height), MNNStudioUI.Sidebar);
            using (new EditorGUILayout.HorizontalScope(GUIStyle.none, GUILayout.ExpandHeight(true)))
            {
                if (sidebar)
                    DrawSidebar();
                using (new EditorGUILayout.VerticalScope(GUIStyle.none, GUILayout.MinWidth(0), GUILayout.ExpandHeight(true)))
                {
                    DrawToolbar();
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
                    {
                        if (_voicePanel && _taskIndex == 0)
                            DrawVoicePanel();
                        else if (_taskIndex == (int)MNNStudioTask.Chat && (Selected != null || _messages.Count > 0))
                            DrawConversation();
                        else if (Selected == null || !CurrentTask.CanRun(Selected) || !MNNPlatformSupport.IsSupported)
                            DrawTaskSetup();
                        else if (_taskIndex == 1 || _taskIndex == 2)
                            DrawRetrieval();
                        else
                            DrawTaskTool();
                    }

                    if (!string.IsNullOrEmpty(_error))
                        DrawError();
                    DrawStatus();
                }

                if (Event.current.type == EventType.Repaint)
                    _workspaceRect = GUILayoutUtility.GetLastRect();
            }

            if (Event.current.type == EventType.Repaint)
                _piperBrowseRect = GUIUtility.ScreenToGUIRect(_piperBrowseScreenRect);
        }

        private void DrawToolbar()
        {
            var rect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            float x = rect.x + 16;
            if (!_sidebarOpen)
            {
                if (MNNStudioUI.ButtonAt(new Rect(x, rect.y + 16, 28, 28), "☰", quiet: true, tooltip: "Show sidebar"))
                    _sidebarOpen = true;
                x += 36;
            }

            var taskRect = new Rect(x, rect.y + 16, Mathf.Clamp(MNNStudioUI.Body.CalcSize(new GUIContent(CurrentTask.Name + " ⌄")).x + 20, 62, 190), 28);
            using (new EditorGUI.DisabledScope(Busy || _audio.IsRecording))
                if (MNNStudioUI.ButtonAt(taskRect, TaskNames[_taskIndex] + " ⌄", quiet: true, tooltip: "Choose workspace"))
                    ShowTaskMenu(taskRect);
            x = taskRect.xMax + 4;
            var modelRect = new Rect(x, rect.y + 16, Mathf.Max(80, rect.xMax - x - 128), 28);
            using (new EditorGUI.DisabledScope(Busy || _audio.IsRecording))
                if (MNNStudioUI.ModelButton(modelRect, (Selected?.DisplayName ?? "Choose model") + "  ⌄", "Select a local model"))
                    ShowModelMenu(modelRect);
            var moreRect = new Rect(rect.xMax - 84, rect.y + 16, 28, 28);
            if (MNNStudioUI.ButtonAt(moreRect, "•••", quiet: true, tooltip: "More actions"))
                ShowActions(moreRect);
            var settingsRect = new Rect(rect.xMax - 48, rect.y + 16, 28, 28);
            if (MNNStudioUI.IconButtonAt(settingsRect, "SettingsIcon", "Model settings"))
                OpenSettings(settingsRect);
        }

        private void ShowActions(Rect anchor)
        {
            var menu = new GenericMenu();
            if (Busy)
                menu.AddDisabledItem(new GUIContent("New chat"));
            else
                menu.AddItem(new GUIContent("New chat"), false, StartNewChat);
            menu.AddItem(new GUIContent("Open Model Manager"), false, MNNModelManagerWindow.ShowWindow);
            if (Busy)
                menu.AddDisabledItem(new GUIContent("Refresh models"));
            else
                menu.AddItem(new GUIContent("Refresh models"), false, RefreshKeepingConversation);
            if (_session == null)
                menu.AddDisabledItem(new GUIContent("Unload active model"));
            else if (Busy)
                menu.AddDisabledItem(new GUIContent("Unload active model"));
            else
                menu.AddItem(new GUIContent("Unload active model"), false, () =>
                {
                    if (Busy)
                        return;
                    StopVoiceLoop();
                    ReleaseSession();
                });
            menu.AddSeparator("");
            if (_messages.Count == 0)
                menu.AddDisabledItem(new GUIContent("Export conversation…"));
            else
                menu.AddItem(new GUIContent("Export conversation…"), false, ExportConversation);
            menu.DropDown(anchor);
        }

        private void RefreshKeepingConversation()
        {
            string previous = Selected?.Directory ?? _history.Conversations.Find(item => item.Id == _activeChatId)?.ModelDirectory;
            RefreshModels();
            int index = _available.FindIndex(model => model.Directory == previous);
            if (index >= 0)
                _modelIndex = index;
            else if (previous != null)
            {
                _modelIndex = -1;
                _error = "The current model is unavailable. Choose a downloaded model to continue.";
            }

            if (_session != null && _sessionModelDirectory != Selected?.Directory)
                ReleaseSession();
            Repaint();
        }

        private void DrawError()
        {
            using (new CenteredContent(ContentWidth))
            using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Unable to complete the request", MNNStudioUI.Heading);
                    GUILayout.FlexibleSpace();
                    if (MNNStudioUI.Button("Dismiss", 74, quiet: true))
                        _error = null;
                }

                if (_error != null)
                {
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_errorScroll, GUILayout.Height(48)))
                    {
                        _errorScroll = scroll.scrollPosition;
                        EditorGUILayout.SelectableLabel(_error, MNNStudioUI.Caption, GUILayout.Height(Mathf.Max(36, MNNStudioUI.Caption.CalcHeight(new GUIContent(_error), ContentWidth - 48))));
                    }
                }
            }

            GUILayout.Space(8);
        }

        private void DrawStatus()
        {
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(rect.x + 24, rect.y + 6, 180, 16), "MNN  ·  On device", MNNStudioUI.Caption);
            string state = _keywordListener != null ? "Listening for wake words" : _namingChatId != null ? "Naming conversation…" : _contextCompressionPending && Busy ? "Compressing context…" : Busy ? "Processing" : _audio != null && _audio.IsRecording ? "Recording" : _session != null ? "Model loaded" : "Model unloaded";
            GUI.Label(new Rect(rect.xMax - 224, rect.y + 6, 200, 16), state, MNNStudioUI.RightCaption);
        }

        private sealed class CenteredContent : GUI.Scope
        {
            private readonly EditorGUILayout.HorizontalScope _row;
            private readonly EditorGUILayout.VerticalScope _column;
            internal CenteredContent(float width)
            {
                _row = new EditorGUILayout.HorizontalScope();
                GUILayout.FlexibleSpace();
                _column = new EditorGUILayout.VerticalScope(GUILayout.Width(width));
            }

            protected override void CloseScope()
            {
                _column.Dispose();
                GUILayout.FlexibleSpace();
                _row.Dispose();
            }
        }

        private Func<string, string, string, string> _attachmentPicker = EditorUtility.OpenFilePanel;
        private Action<Action> _dialogSchedule = action => EditorApplication.delayCall += () => action();
        private bool _dialogPending;
        private void FinishRecording(bool send)
        {
            Try(() =>
            {
                _audioPath = _audio.StopRecording();
                _recordings.Add(_audioPath);
                if (send)
                    Send();
            });
        }

        private void PlayGeneratedSpeech(MNNStudioResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (_audio == null)
                throw new InvalidOperationException("The Studio audio player is unavailable. Reopen MNN Studio and try again.");
            _audio.Play(result.Waveform, result.SampleRate);
        }

        private void EnableEditorAudioAndPlay(MNNStudioResult result)
        {
            EditorUtility.audioMasterMute = false;
            PlayGeneratedSpeech(result);
        }

        private void StopVoiceLoop()
        {
            CommitVoiceDisplay();
            if (_voiceConversation != null)
            {
                _stoppingVoice = _voiceConversation;
                _voiceConversation = null;
            }
            _stoppingVoice?.Stop();
            _voiceLoop = false;
            _nextListen = 0;
            _audio?.CancelRecording();
            _audio?.StopPreview();
        }
    }
}
