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
        private bool Busy => _session != null && _session.Busy;
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
            _voiceLoop = false;
            _nextListen = 0;
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

            TickVoice(now);
            if (_session != null && Busy && _namingChatId == null)
            {
                _session.FlushMemoryCheckpoint();
                _contextSummary = _session.MemorySummary;
                _summarizedMessages = _session.MemoryCovered;
            }

            if (_audio.CaptureFinished)
                FinishRecording(_voiceLoop);
            if (_voiceLoop && !_voiceMuted && _nextListen > 0 && EditorApplication.timeSinceStartup >= _nextListen && !Busy && !_audio.IsPlaying)
            {
                _nextListen = 0;
                StartVoiceCapture();
            }

            if (Busy && _namingChatId == null)
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
            if (MNNStudioUI.ButtonAt(taskRect, TaskNames[_taskIndex] + " ⌄", quiet: true, tooltip: "Choose workspace"))
                ShowTaskMenu(taskRect);
            x = taskRect.xMax + 4;
            var modelRect = new Rect(x, rect.y + 16, Mathf.Max(80, rect.xMax - x - 128), 28);
            using (new EditorGUI.DisabledScope(Busy))
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

        private void OpenSettings(Rect anchor)
        {
            _settingsPopup = new MNNStudioPopover(new Vector2(344, Busy ? 422 : 394), DrawSettings);
            PopupWindow.Show(anchor, _settingsPopup);
        }

        private void DrawSettings()
        {
            GUILayout.Label("Model settings", MNNStudioUI.Heading);
            GUILayout.Space(16);
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = 110;
                using (new EditorGUI.DisabledScope(Busy))
                {
                    int threads = EditorGUILayout.IntSlider("CPU threads", _threads, 1, Math.Min(16, Math.Max(1, SystemInfo.processorCount)));
                    GUILayout.Space(8);
                    bool thinking = EditorGUILayout.Toggle("Thinking", _thinking);
                    if (threads != _threads || thinking != _thinking)
                    {
                        StopVoiceLoop();
                        ReleaseSession();
                        _threads = threads;
                        _thinking = thinking;
                    }
                }

                GUILayout.Space(8);
                _contextTokens = Mathf.Clamp(EditorGUILayout.IntField("Context tokens", _contextTokens), 2048, 32768);
                _tokenLimit = Mathf.Clamp(EditorGUILayout.IntSlider("Response tokens", _tokenLimit, 16, 2048), 16, 2048);
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }

            GUILayout.Space(12);
            MNNStudioUI.Separator();
            GUILayout.Space(12);
            GUILayout.Label("Additional model folder", MNNStudioUI.Heading);
            GUILayout.Space(8);
            var pathStyle = new GUIStyle(MNNStudioUI.Caption)
            {wordWrap = false, clipping = TextClipping.Clip};
            GUILayout.Label(new GUIContent(string.IsNullOrEmpty(_customRoot) ? "No additional folder selected" : _customRoot, _customRoot), pathStyle, GUILayout.Height(20));
            using (new EditorGUI.DisabledScope(Busy))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MNNStudioUI.Button("Choose folder…", 124))
                {
                    string root = EditorUtility.OpenFolderPanel("Additional MNN model folder", _customRoot, "");
                    if (!string.IsNullOrEmpty(root))
                    {
                        _customRoot = root;
                        EditorPrefs.SetString("MNN.Studio.ModelRoot", root);
                        RefreshKeepingConversation();
                    }
                }

                if (!string.IsNullOrEmpty(_customRoot) && MNNStudioUI.Button("Remove", 76, quiet: true))
                {
                    _customRoot = "";
                    EditorPrefs.DeleteKey("MNN.Studio.ModelRoot");
                    RefreshKeepingConversation();
                }
            }

            GUILayout.Space(12);
            GUILayout.Label("Bundled and runtime model folders are included automatically. Settings apply to the next request.", MNNStudioUI.Caption);
            GUILayout.Space(8);
            bool reduce = EditorGUILayout.Toggle("Reduce motion", _reduceMotion);
            if (reduce != _reduceMotion)
            {
                _reduceMotion = reduce;
                EditorPrefs.SetBool("MNN.Studio.ReduceMotion", reduce);
            }

            _autoSend = EditorGUILayout.Toggle("Auto send after silence", _autoSend);
            _autoTitle = EditorGUILayout.Toggle("Automatic chat titles", _autoTitle);
            if (Busy)
            {
                GUILayout.Space(8);
                GUILayout.Label("Model options can be changed when the current request finishes.", MNNStudioUI.Caption);
            }
        }

        private void DrawConversation()
        {
            var evt = Event.current;
            if (evt.type == EventType.ScrollWheel && _conversationRect.Contains(evt.mousePosition))
                _followOutput = false;
            using (var scope = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.ExpandHeight(true)))
            {
                _scroll = scope.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    if (_messages.Count == 0)
                    {
                        GUILayout.Space(Mathf.Clamp((position.height - 460) * .2f, 0, 100));
                        MNNStudioUI.EmptyState("What can I help you with?", "Chat with your local models.");
                        DrawSuggestions();
                    }

                    for (int index = 0; index < _messages.Count; ++index)
                    {
                        DrawMessage(_messages[index]);
                        if (Event.current.type == EventType.Repaint)
                        {
                            var messageRect = GUILayoutUtility.GetLastRect();
                            if (index == _jumpMessageIndex)
                                _jumpScroll = Mathf.Max(0, messageRect.y - 24);
                            if (index == _highlightMessageIndex && EditorApplication.timeSinceStartup < _highlightUntil)
                                EditorGUI.DrawRect(new Rect(messageRect.x - 8, messageRect.y, 3, messageRect.height), MNNStudioUI.Accent);
                        }
                    }

                    if (Busy && _namingChatId == null)
                    {
                        GUILayout.Space(12);
                        if (!string.IsNullOrEmpty(_streamText))
                        {
                            GUILayout.Label(Selected?.DisplayName ?? "Assistant", MNNStudioUI.Caption);
                            GUILayout.Space(6);
                            _streamMarkdown?.Draw(ContentWidth);
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(_discarded ? "Stopping…" : string.IsNullOrEmpty(_streamText) ? "Thinking…" : "Generating…", MNNStudioUI.Caption);
                            GUILayout.FlexibleSpace();
                            if (!_discarded && MNNStudioUI.Button("Stop", 60, quiet: true, tooltip: "Stop at the next token boundary"))
                            {
                                _discarded = true;
                                StopReply();
                            }
                        }
                    }

                    GUILayout.Space(24);
                }
            }

            if (Event.current.type == EventType.Repaint)
                _conversationRect = GUILayoutUtility.GetLastRect();
            if (_jumpScroll >= 0 && Event.current.type == EventType.Repaint)
            {
                _scroll.y = _jumpScroll;
                _jumpScroll = -1;
                _jumpMessageIndex = -1;
                Repaint();
            }

            if (!_followOutput && MNNStudioUI.Button("↓ Latest", 88, quiet: true))
            {
                _followOutput = true;
                _scroll.y = float.MaxValue;
            }

            DrawComposer();
        }

        private void DrawSuggestions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (MNNStudioUI.Button("Explain an idea", 144))
                {
                    _draft = "Explain this idea in simple terms: ";
                    _focusComposer = true;
                }

                GUILayout.Space(8);
                if (MNNStudioUI.Button("Help me write", 144))
                {
                    _draft = "Help me write a clear description of ";
                    _focusComposer = true;
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawMessage(MNNStudioMessage message)
        {
            var style = message.IsUser ? MNNStudioUI.UserBody : MNNStudioUI.Body;
            var caption = message.IsUser ? MNNStudioUI.UserCaption : MNNStudioUI.Caption;
            float maximum = ContentWidth * (message.IsUser ? .82f : 1f);
            float desired = style.CalcSize(new GUIContent(message.Text)).x + 28;
            float width = message.IsUser ? Mathf.Clamp(desired, message.ImagePath != null || message.AudioPath != null ? 270 : 170, maximum) : maximum;
            GUILayout.Space(message.IsUser ? 12 : 20);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (message.IsUser)
                    GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope(message.IsUser ? MNNStudioUI.User : GUIStyle.none, GUILayout.Width(width)))
                {
                    float innerWidth = width - (message.IsUser ? 28 : 0);
                    if (!message.IsUser)
                    {
                        GUILayout.Label(Selected?.DisplayName ?? "Assistant", caption);
                        GUILayout.Space(6);
                    }

                    if (message.IsUser)
                        EditorGUILayout.SelectableLabel(message.Text, style, GUILayout.Height(Mathf.Max(20, style.CalcHeight(new GUIContent(message.Text), innerWidth) + 2)));
                    else
                    {
                        if (!_markdown.TryGetValue(message, out var markdown))
                        {
                            markdown = new MNNStudioMarkdown(message.Text);
                            _markdown[message] = markdown;
                        }

                        markdown.Draw(innerWidth);
                    }

                    if (message.ImagePath != null)
                    {
                        var preview = Preview(message.ImagePath);
                        if (preview != null)
                        {
                            GUILayout.Space(8);
                            float imageWidth = Mathf.Min(256, innerWidth), imageHeight = Mathf.Min(180, imageWidth * preview.height / preview.width);
                            var rect = GUILayoutUtility.GetRect(imageWidth, imageHeight, GUILayout.ExpandWidth(false));
                            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                        }

                        GUILayout.Space(4);
                        GUILayout.Label(Path.GetFileName(message.ImagePath), caption);
                    }

                    if (message.AudioPath != null)
                    {
                        GUILayout.Space(8);
                        GUILayout.Label("Audio · " + Path.GetFileName(message.AudioPath), caption);
                    }

                    if (!message.IsUser && message.Result != null)
                    {
                        var result = message.Result;
                        GUILayout.Space(8);
                        string output = result.GeneratedImage != null ? result.GeneratedImage.Width + " × " + result.GeneratedImage.Height : result.Tokens > 0 ? result.Tokens + " tokens" : result.Waveform.Length > 0 && result.SampleRate > 0 ? ((double)result.Waveform.Length / result.SampleRate).ToString("0.0") + " s audio · " + result.SampleRate + " Hz" : "Result";
                        GUILayout.Label(output + "  ·  " + result.Seconds.ToString("0.0") + " s" + (result.RepetitionStopped ? "  ·  Repeated output stopped" : result.Cancelled ? "  ·  Stopped" : result.Limited ? "  ·  Response limit reached" : ""), caption);
                        GUILayout.Space(4);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (MNNStudioUI.Button("Copy", 52, quiet: true, tooltip: "Copy response"))
                            {
                                EditorGUIUtility.systemCopyBuffer = message.Text;
                                ShowNotification(new GUIContent("Response copied"));
                            }

                            if (result.Waveform.Length > 0)
                            {
                                GUILayout.Space(4);
                                if (MNNStudioAudio.EditorAudioMuted)
                                {
                                    GUILayout.Label("Editor audio muted", MNNStudioUI.Caption, GUILayout.Width(112));
                                    if (MNNStudioUI.Button("Enable & Play", 104, quiet: true, tooltip: "Unmute Unity Editor audio and play this response"))
                                        Try(() => EnableEditorAudioAndPlay(result));
                                }
                                else
                                {
                                    using (new EditorGUI.DisabledScope(_audio != null && _audio.IsLoading))
                                        if (MNNStudioUI.Button(_audio != null && _audio.IsLoading ? "Loading…" : _audio != null && _audio.IsPlaying ? "Replay" : "Play", 68, quiet: true, tooltip: "Play generated voice"))
                                            Try(() => PlayGeneratedSpeech(result));
                                }

                                if (MNNStudioUI.Button("Stop", 52, quiet: true, tooltip: "Stop audio playback"))
                                    Try(() => _audio.StopPreview());
                                if (MNNStudioUI.Button("Save audio", 86, quiet: true))
                                    SaveSpeech(result);
                            }

                            GUILayout.FlexibleSpace();
                        }
                    }
                }

                if (!message.IsUser)
                    GUILayout.FlexibleSpace();
            }
        }

        private void DrawComposer()
        {
            GUILayout.Space(12);
            using (new CenteredContent(ContentWidth))
            {
                using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
                {
                    if (!string.IsNullOrWhiteSpace(_imagePath) || !string.IsNullOrWhiteSpace(_audioPath))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        using (new EditorGUI.DisabledScope(Busy))
                        {
                            float chipWidth = Mathf.Min(288, (ContentWidth - 40) * .5f);
                            if (!string.IsNullOrWhiteSpace(_imagePath) && MNNStudioUI.Attachment("Image", _imagePath, Preview(_imagePath), chipWidth))
                                _imagePath = null;
                            if (!string.IsNullOrWhiteSpace(_imagePath) && !string.IsNullOrWhiteSpace(_audioPath))
                                GUILayout.Space(8);
                            if (!string.IsNullOrWhiteSpace(_audioPath) && MNNStudioUI.Attachment("Audio", _audioPath, null, chipWidth))
                                _audioPath = null;
                            GUILayout.FlexibleSpace();
                        }

                        GUILayout.Space(8);
                    }

                    GUI.SetNextControlName("MNNStudioComposer");
                    float textHeight = Mathf.Clamp(MNNStudioUI.Input.CalcHeight(new GUIContent(_draft + "\n"), ContentWidth - 36), 44, 100);
                    _draft = EditorGUILayout.TextArea(_draft, MNNStudioUI.Input, GUILayout.Height(textHeight));
                    var input = GUILayoutUtility.GetLastRect();
                    if (_focusComposer)
                    {
                        GUI.FocusControl("MNNStudioComposer");
                        _focusComposer = false;
                    }

                    if (string.IsNullOrEmpty(_draft) && Event.current.type == EventType.Repaint)
                        GUI.Label(input, "Ask anything…", MNNStudioUI.Placeholder);
                    if (GUI.GetNameOfFocusedControl() == "MNNStudioComposer")
                        EditorGUI.DrawRect(new Rect(input.x, input.yMax + 2, input.width, 1), MNNStudioUI.Accent);
                    GUILayout.Space(8);
                    if (_audio.IsRecording)
                    {
                        var meter = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
                        MNNStudioVoiceOrb.Meter(meter, _voiceLevel, EditorApplication.timeSinceStartup, _reduceMotion);
                        GUILayout.Space(8);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool media = Selected != null && (Selected.Supports(MNNModelCapabilities.Vision) || Selected.Supports(MNNModelCapabilities.AudioInput));
                        using (new EditorGUI.DisabledScope(Busy || _audio.IsRecording || !media))
                        {
                            if (media && MNNStudioUI.Button("＋", 28, quiet: true, tooltip: "Add an image or audio file"))
                                ShowAttachmentMenu(GUILayoutUtility.GetLastRect());
                        }

                        if (Selected != null && Selected.Supports(MNNModelCapabilities.AudioInput))
                        {
                            using (new EditorGUI.DisabledScope(Busy))
                                if (MNNStudioUI.Button(_audio.IsRecording ? "Stop recording" : "Record", _audio.IsRecording ? 114 : 64, quiet: !_audio.IsRecording))
                                {
                                    if (_audio.IsRecording)
                                        FinishRecording(_voiceLoop);
                                    else
                                        Try(() => _audio.StartRecording());
                                }

                            if (_audio.IsRecording && MNNStudioUI.Button("Cancel", 60, quiet: true))
                                StopVoiceLoop();
                        }

                        if (Selected != null && Selected.Supports(MNNModelCapabilities.SpeechOutput | MNNModelCapabilities.AudioInput) && !_audio.IsRecording)
                        {
                            if (MNNStudioUI.Button(_voiceLoop ? "Voice on" : "Voice", 76, quiet: true, tooltip: "Open voice conversation"))
                            {
                                if (_voiceLoop)
                                    _voicePanel = true;
                                else
                                    BeginVoice();
                            }
                        }

                        if (Selected != null && Selected.Supports(MNNModelCapabilities.SpeechOutput))
                        {
                            using (new EditorGUI.DisabledScope(Busy || _voiceLoop || _audio.IsRecording))
                                if (MNNStudioUI.Button(_speech ? "Sound on" : "Sound off", 88, quiet: !_speech, tooltip: "Generate and automatically play spoken replies"))
                                    SetSpokenReplies(!_speech);
                        }

                        GUILayout.FlexibleSpace();
                        if (Busy)
                        {
                            if (MNNStudioUI.Button("■", 32, primary: true, tooltip: "Stop generating (Escape)"))
                            {
                                _discarded = true;
                                StopReply();
                            }
                        }
                        else if (MNNStudioUI.SendButton(CanSend))
                            Send();
                    }
                }

                if (Event.current.type == EventType.Repaint)
                {
                    _composerRect = GUILayoutUtility.GetLastRect();
                    DrawMediaDropFeedback(_composerRect);
                }

                GUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(_mediaDragActive ? _mediaDropHint : _audio.IsRecording ? "Recording " + _audio.RecordingSeconds.ToString("0") + " s / 30 s  ·  Escape to cancel" : _voiceLoop ? "Voice conversation is on" : "Enter to send · Shift + Enter for a new line", MNNStudioUI.Caption);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(Selected?.CapabilityLabel ?? "Choose a model", MNNStudioUI.Caption, GUILayout.ExpandWidth(false));
                }

                GUILayout.Space(16);
            }
        }

        private void HandleKeyboard()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown)
                return;
            if (_voicePanel && evt.keyCode == KeyCode.Space)
            {
                ToggleVoiceMute();
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.Escape && _voicePanel)
            {
                EndVoice();
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.Escape && Busy)
            {
                _discarded = true;
                StopReply();
                evt.Use();
            }
            else if ((evt.control || evt.command) && (evt.keyCode == KeyCode.N || evt.shift && evt.keyCode == KeyCode.O))
            {
                if (!Busy)
                    StartNewChat();
                evt.Use();
            }
            else if ((evt.control || evt.command) && (evt.keyCode == KeyCode.B || evt.shift && evt.keyCode == KeyCode.S))
            {
                _sidebarOpen = !_sidebarOpen;
                evt.Use();
            }
            else if ((evt.control || evt.command) && evt.keyCode == KeyCode.K)
            {
                OpenSearch();
                evt.Use();
            }
            else if (evt.shift && evt.keyCode == KeyCode.Escape)
            {
                _focusComposer = true;
                evt.Use();
            }
            else if ((evt.control || evt.command) && evt.shift && evt.keyCode == KeyCode.C)
            {
                var last = _messages.LastOrDefault(message => !message.IsUser);
                if (last != null)
                    EditorGUIUtility.systemCopyBuffer = last.Text;
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.Escape && _audio != null && _audio.IsRecording)
            {
                StopVoiceLoop();
                evt.Use();
            }
            else if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && !evt.shift && GUI.GetNameOfFocusedControl() == "MNNStudioComposer" && !evt.alt && string.IsNullOrEmpty(Input.compositionString))
            {
                evt.Use();
                if (CanSend)
                    Send();
            }
        }

        private void DrawRetrieval()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(28);
                    GUILayout.Label(_taskIndex == 1 ? "Compare semantic meaning" : "Find the most relevant answer", MNNStudioUI.Heading);
                    GUILayout.Space(8);
                    GUILayout.Label(_taskIndex == 1 ? "Measure how closely a document matches your query." : "Evaluate a document against your query.", MNNStudioUI.Caption);
                    GUILayout.Space(24);
                    using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
                    {
                        GUILayout.Label("Query", MNNStudioUI.Heading);
                        GUILayout.Space(8);
                        _query = EditorGUILayout.TextArea(_query, MNNStudioUI.Input, GUILayout.Height(64));
                        GUILayout.Space(12);
                        MNNStudioUI.Separator();
                        GUILayout.Space(12);
                        GUILayout.Label("Document", MNNStudioUI.Heading);
                        GUILayout.Space(8);
                        _document = EditorGUILayout.TextArea(_document, MNNStudioUI.Input, GUILayout.Height(108));
                    }

                    GUILayout.Space(16);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(Busy || string.IsNullOrWhiteSpace(_query) || string.IsNullOrWhiteSpace(_document)))
                            if (MNNStudioUI.Button(Busy ? "Computing…" : _taskIndex == 1 ? "Compare" : "Score relevance", 144, primary: true))
                                RunRetrieval();
                        GUILayout.FlexibleSpace();
                    }

                    if (_taskIndex == 1)
                    {
                        GUILayout.Space(12);
                        GUILayout.Label("Tip: use the model's recommended Instruct / Query prefix for retrieval queries.", MNNStudioUI.Caption);
                    }

                    if (_retrieval != null)
                    {
                        GUILayout.Space(24);
                        using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
                        {
                            GUILayout.Label("Result", MNNStudioUI.Heading);
                            GUILayout.Space(16);
                            MNNStudioUI.ResultMetric(_taskIndex == 1 ? "Cosine similarity" : "Relevance", _retrieval.Score?.ToString("0.0000"));
                            MNNStudioUI.ResultMetric("Inference time", _retrieval.Seconds.ToString("0.00") + " s");
                            if (_retrieval.Vector != null)
                            {
                                MNNStudioUI.ResultMetric("Dimensions", _retrieval.Vector.Length.ToString());
                                if (MNNStudioUI.Button("Copy vector as JSON", 170))
                                    EditorGUIUtility.systemCopyBuffer = "[" + string.Join(",", _retrieval.Vector.Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "]";
                            }
                        }
                    }

                    GUILayout.Space(24);
                }
            }
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
            string state = _namingChatId != null ? "Naming conversation…" : _contextCompressionPending && Busy ? "Compressing context…" : Busy ? "Processing" : _audio != null && _audio.IsRecording ? "Recording" : _session != null ? "Model loaded" : "Model unloaded";
            var style = new GUIStyle(MNNStudioUI.Caption)
            {alignment = TextAnchor.MiddleRight};
            GUI.Label(new Rect(rect.xMax - 224, rect.y + 6, 200, 16), state, style);
        }

        private sealed class CenteredContent : IDisposable
        {
            private readonly EditorGUILayout.HorizontalScope _row;
            private readonly EditorGUILayout.VerticalScope _column;
            internal CenteredContent(float width)
            {
                _row = new EditorGUILayout.HorizontalScope();
                GUILayout.FlexibleSpace();
                _column = new EditorGUILayout.VerticalScope(GUILayout.Width(width));
            }

            public void Dispose()
            {
                _column.Dispose();
                GUILayout.FlexibleSpace();
                _row.Dispose();
            }
        }

        private void Send()
        {
            if (!CanSend)
                return;
            Try(() =>
            {
                string text = _draft.Trim();
                if (text.Length == 0)
                    text = _audioPath != null ? "Transcribe and respond to this audio." : _imagePath != null ? "Describe this image." : "";
                if (text.Length > MNNStudioPrompt.MaximumCharacters)
                    throw new ArgumentException("The message is too long (16,000 characters maximum).");
                EnsureSession();
                _discarded = false;
                _streamText = "";
                _streamMarkdown = null;
                _followOutput = true;
                _session.Send(new MNNStudioRequest{Prompt = text, History = _messages.ToArray(), ContextSummary = _contextSummary, SummarizedMessages = _summarizedMessages, Image = _imagePath, Audio = _audioPath, TokenLimit = _tokenLimit, ContextTokens = _contextTokens, Speech = _speech && Selected.Supports(MNNModelCapabilities.SpeechOutput)});
                _contextCompressionPending = _messages.Skip(_summarizedMessages).Sum(message => (long)message.Text.Length) + text.Length > MNNStudioPrompt.CompressionThresholdCharacters;
                _messages.Add(new MNNStudioMessage(true, text, _imagePath, _audioPath));
                foreach (var cached in _markdown.Keys.Where(message => !_messages.Contains(message)).ToArray())
                    _markdown.Remove(cached);
                foreach (string cached in _previews.Keys.Where(path => !_messages.Any(message => message.ImagePath == path)).ToArray())
                {
                    DestroyImmediate(_previews[cached]);
                    _previews.Remove(cached);
                }

                _draft = "";
                _imagePath = _audioPath = null;
                _error = null;
                _scroll.y = float.MaxValue;
                _focusComposer = true;
                RememberConversation();
            });
        }

        private void RunRetrieval()
        {
            Try(() =>
            {
                EnsureSession();
                _error = null;
                _retrieval = null;
                _session.Send(new MNNStudioRequest{Prompt = _query, Document = _document});
            });
        }

        private void OnCompleted()
        {
            if (_namingChatId != null)
            {
                var chat = _history.Conversations.Find(item => item.Id == _namingChatId);
                _namingChatId = null;
                if (chat != null)
                {
                    string title = _session.Result != null && !_session.Result.Cancelled ? MNNStudioNaming.CleanTitle(_session.Result.Text) : "";
                    if (title.Length > 0)
                    {
                        chat.Title = title;
                        chat.Named = true;
                    }
                    else
                        chat.Named = true; // Keep fallback; a manual model-title action can retry.
                }

                _streamText = "";
                _streamMarkdown = null;
                _shownProgress = _session.Progress;
                SaveLibrary();
                return;
            }

            _contextCompressionPending = false;
            if (_session.MemorySummary != null)
            {
                _contextSummary = _session.MemorySummary;
                _summarizedMessages = _session.MemoryCovered;
            }

            if (_session.Error != null)
            {
                _error = _session.Error;
                StopVoiceLoop();
            }
            else if (_session.Result != null)
            {
                var result = _session.Result;
                _contextCompressionPending = false;
                if (!string.IsNullOrEmpty(result.ContextSummary))
                    _contextSummary = MNNStudioPrompt.BoundSummary(result.ContextSummary);
                if (_taskIndex == 0)
                {
                    if (!string.IsNullOrEmpty(result.Text))
                        _messages.Add(new MNNStudioMessage(false, result.Text, result: result));
                    if (_followOutput)
                        _scroll.y = float.MaxValue;
                    if ((!_voiceLoop || !_voiceMuted) && result.Waveform.Length > 0 && !result.Cancelled)
                    {
                        Try(() => PlayGeneratedSpeech(result));
                        if (_voiceLoop)
                            _nextListen = EditorApplication.timeSinceStartup + (double)result.Waveform.Length / result.SampleRate + .5;
                    }
                    else if (_voiceLoop && !_voiceMuted)
                        _nextListen = EditorApplication.timeSinceStartup + .25;
                }
                else
                {
                    _retrieval = result;
                    _taskResultMessage = new MNNStudioMessage(false, result.Text, result: result);
                    if (result.Waveform.Length > 0 && !result.Cancelled)
                        Try(() => PlayGeneratedSpeech(result));
                }
            }

            RememberConversation();
            _sessionLastUsed = EditorApplication.timeSinceStartup;
            _streamText = "";
            _streamMarkdown = null;
            Repaint();
        }

        private void PickAttachment(bool image)
        {
            string path = EditorUtility.OpenFilePanel(image ? "Attach image" : "Attach audio", "", image ? "png,jpg,jpeg,bmp" : "wav");
            if (!string.IsNullOrEmpty(path))
                Try(() => Attach(path, image));
        }

        private void Attach(string path, bool image)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("The attachment does not exist.", path);
            if (!Selected.Supports(image ? MNNModelCapabilities.Vision : MNNModelCapabilities.AudioInput))
                throw new NotSupportedException("This model does not support this attachment.");
            if (path.IndexOfAny(new[]{'<', '>', '\n', '\r'}) >= 0)
                throw new ArgumentException("Attachment paths cannot contain media tag delimiters.");
            if (image)
                _imagePath = Path.GetFullPath(path);
            else
                _audioPath = Path.GetFullPath(path);
        }

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
            _voiceLoop = false;
            _nextListen = 0;
            _audio?.CancelRecording();
            _audio?.StopPreview();
        }

        private void Try(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                _error = error.GetBaseException().Message;
                StopVoiceLoop();
            }
        }

        private Texture2D Preview(string path)
        {
            if (_previews.TryGetValue(path, out var texture))
                return texture;
            if (_failedPreviews.Contains(path))
                return null;
            Texture2D candidate = null;
            try
            {
                // Decode once, and never retry corrupt/oversized previews on every repaint.
                if (new FileInfo(path).Length > 16 * 1024 * 1024)
                {
                    _failedPreviews.Add(path);
                    return null;
                }

                candidate = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {hideFlags = HideFlags.HideAndDontSave};
                if (!candidate.LoadImage(File.ReadAllBytes(path), true))
                    throw new IOException("Invalid image.");
                if (Math.Max(candidate.width, candidate.height) > 320)
                {
                    float scale = 320f / Math.Max(candidate.width, candidate.height);
                    var temporary = RenderTexture.GetTemporary(Math.Max(1, Mathf.RoundToInt(candidate.width * scale)), Math.Max(1, Mathf.RoundToInt(candidate.height * scale)), 0, RenderTextureFormat.ARGB32);
                    var previous = RenderTexture.active;
                    Texture2D thumbnail = null;
                    try
                    {
                        Graphics.Blit(candidate, temporary);
                        RenderTexture.active = temporary;
                        thumbnail = new Texture2D(temporary.width, temporary.height, TextureFormat.RGBA32, false)
                        {hideFlags = HideFlags.HideAndDontSave};
                        thumbnail.ReadPixels(new Rect(0, 0, temporary.width, temporary.height), 0, 0);
                        thumbnail.Apply(false, true);
                        DestroyImmediate(candidate);
                        candidate = thumbnail;
                        thumbnail = null;
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        RenderTexture.ReleaseTemporary(temporary);
                        if (thumbnail != null)
                            DestroyImmediate(thumbnail);
                    }
                }

                _previews[path] = candidate;
                return candidate;
            }
            catch
            {
                if (candidate != null)
                    DestroyImmediate(candidate);
                _failedPreviews.Add(path);
                return null;
            }
        }

        private void ClearPreviews()
        {
            foreach (var texture in _previews.Values)
                if (texture != null)
                    DestroyImmediate(texture);
            _previews.Clear();
            _failedPreviews.Clear();
            _markdown.Clear();
        }

        private void CleanupRecordings()
        {
            foreach (string path in _recordings)
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

            _recordings.Clear();
        }

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
            _imagePath = _audioPath = null;
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
            string path = EditorUtility.SaveFilePanel("Save generated speech", "", "mnn-speech.wav", "wav");
            if (path.Length > 0)
                Try(() => File.WriteAllBytes(path, MNNStudioAudio.EncodeWave(result.Waveform, result.SampleRate)));
        }

        private void ExportConversation()
        {
            string path = EditorUtility.SaveFilePanel("Export conversation", "", "mnn-chat.md", "md");
            if (path.Length == 0)
                return;
            Try(() =>
            {
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
