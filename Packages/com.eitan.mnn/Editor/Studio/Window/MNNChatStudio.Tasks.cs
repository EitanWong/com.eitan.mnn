using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private bool _downloadsDirty;
        private int _completedDownloads;
        private bool _taskSpeech;
        private bool _ocrCroppedLine;
        [SerializeField] private string _keywords = MNNKwsModel.DefaultKeywords;
        [SerializeField] private float _keywordThreshold = MNNKeywordSpotter.DefaultThreshold, _keywordScore = 1;
        private MNNStudioMessage _taskResultMessage;
        private string _referenceText = "";
        private int _speechLanguage, _ttsEmotion;
        private int _recognitionLanguage;
        private int _generationSeed = 42, _ttsVoice, _ttsSteps = 5, _dhVaaniSteps = 8, _indicF5Steps = 32, _imageSteps = 20, _sanaSteps = 10;
        private float _sanaGuidance = 4.5f;
        private float _ttsSpeed = 1.05f, _imageGuidance = 7.5f;
        private string _negativePrompt = "";
        private Texture2D _generatedPreview;
        private MNNGeneratedImage _previewResult;
        private Rect _taskInputScreenRect, _taskExampleScreenRect, _taskAudioAttachmentScreenRect, _taskRunScreenRect, _referenceTranscriptScreenRect;
        private void OnStudioDownloadsChanged()
        {
            _downloadsDirty = true;
            Repaint();
        }

        internal static void OpenModelForTask(string directory, MNNStudioTask task)
        {
            Open();
            var window = GetWindow<MNNChatStudio>();
            window.UseModelForTask(directory, task);
        }

        private string TaskExample => Selected?.IsTurnSense == true ? "I am finished." : Selected?.IsVoxTurn == true ? "I am finished." : Selected?.IsIndicF5 == true ? "नमस्ते, यह आवाज़ का परीक्षण है।" : Selected?.IsQwen3Explicit == true ? (_speechLanguage == 1 ? "Paris is the capital of France." : "你好，这是语音合成测试。") : (Selected?.IsDhVaani == true || Selected?.IsPocket == true) ? "The capital of France is Paris." : Selected?.TtsProfile == MNNTtsModelProfile.MossNano ? (_ttsVoice >= 6 && _ttsVoice < 11 ? "The capital of France is Paris." : _ttsVoice >= 11 ? "こんにちは、これは音声合成のテストです。" : "你好，这是语音合成测试。") : Selected?.TtsProfile == MNNTtsModelProfile.SherpaVitsChinese ? "你好，这是语音合成测试。" : Selected?.TtsProfile == MNNTtsModelProfile.MmsTamil ? "வணக்கம் உலகம். இது ஒரு குரல் தொகுப்பு சோதனை." : Selected?.TtsProfile == MNNTtsModelProfile.Kokoro ? (Selected.TtsVoices.ElementAtOrDefault(_ttsVoice)?.StartsWith("z", StringComparison.Ordinal) == true ? "你好，欢迎使用语音合成。" : "The capital of France is Paris.") : Selected?.TtsProfile == MNNTtsModelProfile.IndicVits ? MNNIndicVits.SpeakerExamples[Mathf.Clamp(_ttsVoice, 0, MNNIndicVits.SpeakerExamples.Length - 1)] : Selected?.IsPaddlePage == true && !_ocrCroppedLine ? "Read all visible text in this image." : Selected?.IsPaddleText == true ? "Recognize this upright cropped text line." : (Selected?.TtsProfile == MNNTtsModelProfile.Kokoro || Selected?.IsBertVits2 == true || Selected?.IsCosyVoice == true || Selected?.IsCosyVoice300M == true || Selected?.RequiresReferenceAudio == true) ? "你好，欢迎使用语音合成。" : CurrentTask.Example;
        private void UseModelForTask(string directory, MNNStudioTask task)
        {
            if (Busy)
            {
                _error = "Finish or stop the current request before loading another model.";
                return;
            }

            Try(() =>
            {
                var model = MNNStudioModel.Read(directory);
                if (!MNNStudioTasks.Get((int)task).Accepts(model) && task != MNNStudioTask.Other)
                    throw new NotSupportedException("This folder does not have the format or media capabilities required for " + MNNStudioTasks.Get((int)task).Name + ". Choose a complete MNN model repository.");
                RememberConversation();
                ResetWorkspaceForModelChange();
                _activeChatId = null;
                _taskIndex = (int)task;
                _customRoot = Path.GetDirectoryName(Path.GetFullPath(directory));
                EditorPrefs.SetString("MNN.Studio.ModelRoot", _customRoot);
                RefreshModels();
                _modelIndex = _available.FindIndex(item => item.Directory == model.Directory);
                _query = TaskExample;
                Repaint();
            });
        }

        private void PickTaskModel()
        {
            var task = CurrentTask.Task;
            ShowDialogAfterGUI(() =>
            {
                if (Busy || _audio.IsRecording || CurrentTask.Task != task)
                    return;
                string path = EditorUtility.OpenFolderPanel("Choose the complete MNN model folder", _customRoot, "");
                if (this != null && _audio != null && !Busy && !_audio.IsRecording && CurrentTask.Task == task && !string.IsNullOrEmpty(path))
                    UseModelForTask(path, task);
            });
        }

        private void DrawTaskSetup()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label(CurrentTask.Name, MNNStudioUI.Title);
                    GUILayout.Space(8);
                    GUILayout.Label(CurrentTask.Description, MNNStudioUI.Body);
                    if (CurrentTask.Task == MNNStudioTask.Other)
                        DrawWorkspaceCatalog();
                    GUILayout.Space(16);
                    string status = !CurrentTask.Runnable || Selected != null && !CurrentTask.CanRun(Selected) ? CurrentTask.UnavailableReason : !MNNPlatformSupport.IsSupported ? MNNPlatformSupport.UnsupportedReason : "Choose a compatible local model to get started. The model loads on the first request.";
                    EditorGUILayout.HelpBox(status, MessageType.Info);
                    if (Selected != null)
                    {
                        GUILayout.Space(8);
                        GUILayout.Label("Local model: " + Selected.DisplayName, MNNStudioUI.Heading);
                        EditorGUILayout.SelectableLabel(Selected.Directory, MNNStudioUI.Caption, GUILayout.Height(36));
                        if (MNNStudioUI.Button("Locate files", 110))
                            EditorUtility.RevealInFinder(Selected.Directory);
                    }

                    GUILayout.Space(20);
                    DrawTaskModelGuide();
                    GUILayout.Space(24);
                }
            }
        }

        private void DrawWorkspaceCatalog()
        {
            GUILayout.Space(16);
            foreach (var task in MNNStudioTasks.All.Where(item => item.Task != MNNStudioTask.Other))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(Busy))
                        if (MNNStudioUI.Button(task.Name, 210, quiet: true, tooltip: task.Description))
                        {
                            int index = (int)task.Task;
                            EditorApplication.delayCall += () =>
                            {
                                if (this != null)
                                {
                                    SelectTask(index);
                                    Repaint();
                                }
                            };
                        }

                    int local = _models.Count(task.Accepts);
                    int ready = _models.Count(task.CanRun);
                    GUILayout.Label(!task.Runnable ? "Model setup" : ready > 0 && MNNPlatformSupport.IsSupported ? ready + " ready" : "Choose model", MNNStudioUI.Caption);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(local + " local", MNNStudioUI.Caption, GUILayout.Width(60));
                }
            }

            GUILayout.Space(16);
        }

        private void DrawTaskModelGuide()
        {
            using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
            {
                GUILayout.Label("1. Choose a model", MNNStudioUI.Heading);
                GUILayout.Space(8);
                GUILayout.Label(string.IsNullOrEmpty(CurrentTask.Recommendation) ? "Browse the task's official MNN catalog and read its model card." : CurrentTask.Recommendation, MNNStudioUI.Body);
                GUILayout.Space(8);
                foreach (string repository in CurrentTask.Repositories)
                    if (GUILayout.Button(new GUIContent(repository + " ↗", "Open the official model card"), EditorStyles.linkLabel))
                        Application.OpenURL(((MNNCosyVoiceModel.IsRepository(repository) || MNNReferenceSpeechModel.IsRepository(repository) || MNNVerifiedTtsModel.IsHuggingFaceRepository(repository)) ? "https://huggingface.co/" : "https://modelscope.cn/models/") + repository);
                GUILayout.Space(12);
                GUILayout.Label("2. Download the complete repository", MNNStudioUI.Heading);
                GUILayout.Space(8);
                GUILayout.Label(CurrentTask.Requirements, MNNStudioUI.Caption);
                GUILayout.Space(8);
                if (MNNStudioUI.Button("Find / download models", 190, primary: true))
                    MNNModelManagerWindow.ShowForTask(CurrentTask);
                GUILayout.Space(12);
                GUILayout.Label("3. Select it in Studio", MNNStudioUI.Heading);
                GUILayout.Space(8);
                GUILayout.Label("Downloads are installed in Assets/StreamingAssets/MNN/Models and appear automatically when complete. Select a model above or choose an existing model folder.", MNNStudioUI.Caption);
                GUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(Busy))
                {
                    if (MNNStudioUI.Button("Choose local folder…", 170))
                        PickTaskModel();
                    if (MNNStudioUI.Button("Refresh models", 128, quiet: true))
                        RefreshKeepingConversation();
                }

                if (CurrentTask.Task == MNNStudioTask.SpeechSynthesis)
                {
                    GUILayout.Space(8);
                    if (MNNStudioUI.Button("Use Omni spoken replies", 196))
                        SelectTask((int)MNNStudioTask.Omni);
                }
            }
        }

        private Func<string, string> _piperDataFolderPicker = start => EditorUtility.OpenFolderPanel("Choose espeak-ng-data folder", start, "");
        private Action<Action> _piperPickerSchedule = action => EditorApplication.delayCall += () => action();
        private bool _piperPickerPending;
        private Rect _piperBrowseScreenRect, _piperBrowseRect;
        private void BrowsePiperDataFolder()
        {
            if (_piperPickerPending || Busy || Selected == null)
                return;
            _piperPickerPending = true;
            string modelDirectory = Selected.Directory;
            _piperPickerSchedule(() =>
            {
                if (this == null)
                    return;
                _piperPickerPending = false;
                if (Busy || Selected?.Directory != modelDirectory)
                    return;
                Try(() =>
                {
                    string previous = EditorPrefs.GetString(MNNStudioPiperSettings.DataPreference(modelDirectory), "");
                    string start = MNNStudioPiperSettings.PickerDirectory(previous, modelDirectory);
                    string chosen = _piperDataFolderPicker(start);
                    if (!string.IsNullOrWhiteSpace(chosen))
                        ApplyPiperDataFolder(chosen);
                });
                Repaint();
            });
        }

        private void ApplyPiperDataFolder(string chosen)
        {
            string path = string.IsNullOrWhiteSpace(chosen) ? "" : MNNPiper.ResolveDataDirectory(Selected.Directory, selected: chosen);
            EditorPrefs.SetString(MNNStudioPiperSettings.DataPreference(Selected.Directory), path);
            ReleaseSession();
            _error = null;
            Repaint();
        }

        private void DrawTaskTool()
        {
            if (Selected?.IsTurnSense == true)
            {
                DrawTurnSenseTool();
                return;
            }
            if (CurrentTask.Task == MNNStudioTask.EchoCancellation)
            {
                DrawEchoCancellationTool();
                return;
            }
            if (CurrentTask.Task == MNNStudioTask.NLP)
            {
                DrawTextDecisionTool();
                return;
            }
            if (CurrentTask.Task == MNNStudioTask.Pose)
            {
                DrawPoseTool();
                return;
            }
            if (CurrentTask.Task == MNNStudioTask.DepthEstimation)
            {
                DrawDepthTool();
                return;
            }
            if (CurrentTask.Task == MNNStudioTask.Segmentation)
            {
                DrawSegmentationTool();
                return;
            }
            if (CurrentTask.Task == MNNStudioTask.StyleTransfer)
            {
                DrawStyleTransferTool();
                return;
            }
            if (CurrentTask.Task == MNNStudioTask.DocumentLayout)
            {
                DrawDocumentLayoutTool();
                return;
            }
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label(CurrentTask.Name, MNNStudioUI.Heading);
                    GUILayout.Space(8);
                    GUILayout.Label(CurrentTask.Description, MNNStudioUI.Caption);
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(Busy || _audio.IsRecording))
                    {
                        if (!Selected.IsKeywordSpotter)
                        {
                            GUILayout.Label("Instruction", MNNStudioUI.Heading);
                            GUILayout.Space(8);
                            GUI.SetNextControlName("MNNStudioTaskInstruction");
                            _query = EditorGUILayout.TextArea(_query, MNNStudioUI.Input, GUILayout.Height(80));
                            if (Event.current.type == EventType.Repaint)
                                _taskInputScreenRect = GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect());
                            if (!string.IsNullOrEmpty(TaskExample) && MNNStudioUI.Button("Use example", 112, quiet: true))
                            {
                                // End the active TextEditor session before replacing its
                                // backing value, otherwise its old buffer can restore it.
                                GUI.FocusControl(null);
                                GUIUtility.keyboardControl = 0;
                                EditorGUIUtility.editingTextField = false;
                                _query = TaskExample;
                                Repaint();
                            }
                            if (Event.current.type == EventType.Repaint)
                                _taskExampleScreenRect = GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect());
                            GUILayout.Space(12);
                        }
                        if ((CurrentTask.Required & MNNModelCapabilities.Vision) != 0)
                            DrawTaskAttachment(true);
                        if ((CurrentTask.Required & MNNModelCapabilities.AudioInput) != 0)
                            DrawTaskAttachment(false);
                        if (Selected.IsDualTurn) EditorGUILayout.HelpBox("DualTurn: mono is the user with a silent agent; stereo CH0 is the user and CH1 is the agent. The final 80 ms frame is padded with silence when needed.", MessageType.Info);
                        if (Selected.IsVoxTurn) DrawVoxTurnSettings();
                        if (Selected.IsSpExPlus) DrawSpeakerReferenceInput();
                        if (Selected.IsSeg) DrawGestureFeaturesInput();
                        if (Selected.RequiresVisualFrames)
                            DrawAvExtractionInput();
                        if (CurrentTask.Task == MNNStudioTask.SpeechRecognition && Selected.SupportsWhisperChinese)
                            _recognitionLanguage = EditorGUILayout.Popup("Recognition language", _recognitionLanguage, new[] {"English", "Chinese"});
                        if (Selected.IsKeywordSpotter)
                            DrawKeywordSettings();
                        if (Selected.IsPaddlePage && CurrentTask.Task == MNNStudioTask.OCR)
                        {
                            _ocrCroppedLine = EditorGUILayout.Toggle("Cropped text line", _ocrCroppedLine);
                            GUILayout.Label(_ocrCroppedLine ? "Recognize one upright line directly." : "Detect text regions and read the full image.", MNNStudioUI.Caption);
                        }
                        if (CurrentTask.Task == MNNStudioTask.SpeechSynthesis)
                        {
                            string[] voices = Selected.TtsVoices;
                            if (!Selected.RequiresReferenceAudio)
                                _ttsVoice = EditorGUILayout.Popup("Voice", Mathf.Clamp(_ttsVoice, 0, voices.Length - 1), voices);
                            else
                            {
                                GUILayout.Label("Reference voice · WAV, 0.5–15 seconds", MNNStudioUI.Heading);
                                DrawTaskAttachment(false);
                                if (Selected.RequiresReferenceTranscript)
                                {
                                    GUILayout.Label("Reference audio transcript (required)", MNNStudioUI.Heading);
                                    GUILayout.Label("Enter the exact words spoken in the reference WAV here.", MNNStudioUI.Caption);
                                    GUILayout.Space(6);
                                    GUI.SetNextControlName("MNNStudioReferenceTranscript");
                                    _referenceText = EditorGUILayout.TextArea(_referenceText ?? "", MNNStudioUI.Input, GUILayout.Height(80));
                                    if (Event.current.type == EventType.Repaint)
                                        _referenceTranscriptScreenRect = GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect());
                                    GUILayout.Space(8);
                                }
                                DrawReferenceRecordingControls();
                                if (!Selected.IsDhVaani && !Selected.IsPocket && !Selected.IsIndicF5)
                                    _speechLanguage = EditorGUILayout.Popup("Speech language", _speechLanguage, new[]{"Chinese", "English"});
                            }
                            if (Selected.IsQwen3Explicit)
                                _speechLanguage = EditorGUILayout.Popup("Speech language", Mathf.Clamp(_speechLanguage, 0, 1), new[]{"Chinese", "English"});
                            if (Selected.TtsProfile == MNNTtsModelProfile.MmsTamil)
                            {
                                string[] precisions = MNNMmsTamil.InstalledPrecisions(Selected.Directory);
                                int current = Math.Max(0, Array.IndexOf(precisions, Selected.MmsTamilPrecision));
                                int next = EditorGUILayout.Popup("Precision", current, precisions);
                                if (next != current)
                                {
                                    ReleaseSession();
                                    Selected.MmsTamilPrecision = precisions[next];
                                }
                            }
                            if (Selected.TtsProfile == MNNTtsModelProfile.IndicVits)
                                _ttsEmotion = EditorGUILayout.Popup("Emotion", Mathf.Clamp(_ttsEmotion, 0, MNNIndicVits.StyleNames.Length - 1), MNNIndicVits.StyleNames);
                            if (Selected.TtsProfile == MNNTtsModelProfile.Supertonic && Selected.SpeechLanguages.Length > 1)
                                _speechLanguage = EditorGUILayout.Popup("Speech language", Mathf.Clamp(_speechLanguage, 0, Selected.SpeechLanguages.Length - 1), Selected.SpeechLanguages);
                            if (Selected.IsPiper)
                            {
                                string dataPreference = MNNStudioPiperSettings.DataPreference(Selected.Directory);
                                string selectedData = EditorPrefs.GetString(dataPreference, "");
                                string data = string.IsNullOrWhiteSpace(selectedData) ? Selected.PiperDataDirectory : selectedData;
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    string edited = EditorGUILayout.DelayedTextField("eSpeak data folder", data);
                                    if (edited != data)
                                        Try(() => ApplyPiperDataFolder(edited));
                                    if (MNNStudioUI.Button("Browse…", 90, quiet: true, tooltip: "Choose the espeak-ng-data folder"))
                                        BrowsePiperDataFolder();
                                    if (Event.current.type == EventType.Repaint)
                                        _piperBrowseScreenRect = GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect());
                                }

                                GUILayout.Label(string.IsNullOrWhiteSpace(selectedData) ? "Using espeak-ng-data from the model folder automatically." : "Using the selected data folder for this model.", MNNStudioUI.Caption);
                                string executable = MNNStudioPiperSettings.FindExecutable(Selected.Directory);
                                GUILayout.Label(executable != null ? "eSpeak-NG tool found automatically." : "Data is included with the model. Install eSpeak-NG or set MNN_ESPEAK_NG_PATH to its executable.", MNNStudioUI.Caption);
                            }

                            if (Selected.IsIndicF5)
                            {
                                _indicF5Steps = EditorGUILayout.IntSlider("Synthesis steps", _indicF5Steps, 8, 64);
                                _ttsSpeed = EditorGUILayout.Slider("Speed", _ttsSpeed, .5f, 2);
                            }
                            if (Selected.IsDhVaani)
                            {
                                _dhVaaniSteps = EditorGUILayout.IntSlider("Synthesis steps", _dhVaaniSteps, 1, 32);
                                _ttsSpeed = EditorGUILayout.Slider("Speed", _ttsSpeed, .5f, 2);
                            }
                            if (Selected.TtsProfile == MNNTtsModelProfile.Supertonic || Selected.TtsProfile == MNNTtsModelProfile.Kokoro || Selected.TtsProfile == MNNTtsModelProfile.MmsTamil || Selected.TtsProfile == MNNTtsModelProfile.SherpaVitsChinese)
                            {
                                if (Selected.TtsProfile == MNNTtsModelProfile.Supertonic)
                                    _ttsSteps = EditorGUILayout.IntSlider("Synthesis steps", _ttsSteps, 1, 30);
                                _ttsSpeed = EditorGUILayout.Slider("Speed", _ttsSpeed, .5f, 2);
                            }

                            GUILayout.Label(Selected.IsIndicF5 ? "IndicF5 · Reference WAV and exact transcript required · Hindi verified · Tamil experimental · Maximum 2048 total mel frames" : Selected.IsPocket ? "Pocket-TTS · English · Reference WAV required · Maximum 50 tokens / 30 seconds" : Selected.IsQwen3Explicit ? "Qwen3-TTS · Prepared voice assets · Chinese / English · Maximum 200 characters / 30 seconds" : Selected.IsDhVaani ? "DhVaani · Hindi / English · Maximum 500 characters / 30 seconds · Reference transcript required" : Selected.TtsProfile == MNNTtsModelProfile.MossNano ? "MOSS Nano · 18 reference voices · Maximum 250 tokens / 30 seconds · Write numbers as spoken words" : Selected.TtsProfile == MNNTtsModelProfile.SherpaVitsChinese ? "Chinese lexicon · Maximum 300 characters · Write numbers as Chinese words" : Selected.TtsProfile == MNNTtsModelProfile.MmsTamil ? "Tamil character vocabulary · Maximum 300 characters · Write numbers as spoken words" : Selected.TtsProfile == MNNTtsModelProfile.IndicVits ? "Match text to the selected speaker language · Maximum 300 characters · CPU" : Selected.TtsProfile == MNNTtsModelProfile.Kokoro ? "Mandarin / English lexicon · Maximum 2000 characters · Write numbers as spoken words" : Selected.RequiresReferenceAudio ? "Chinese / English · Maximum 200 characters · Write numbers as spoken words" : Selected.IsCosyVoice300M ? "CosyVoice-300M-MNN · Default reference voice · 22050 Hz · Maximum 200 characters" : Selected.IsCosyVoice ? "CosyVoice3 · Default reference voice · Maximum 200 characters" : Selected.IsBertVits2 ? "Chenxi · Chinese / English / mixed text · Maximum 300 characters" : Selected.IsPiper ? "English voices · eSpeak-NG required (install or set MNN_ESPEAK_NG_PATH) · Maximum 1000 characters" : Selected.SpeechLanguages.Length > 1 ? "Select the language matching your text · Chinese is not supported · Maximum 500 characters" : "English vocabulary · Maximum 500 characters · Long text is split into short segments", MNNStudioUI.Caption);
                        }

                        if (CurrentTask.Task == MNNStudioTask.ImageGeneration)
                        {
                            if (Selected.IsSana)
                            {
                                DrawTaskAttachment(true);
                                _sanaSteps = EditorGUILayout.IntSlider("Editing steps", _sanaSteps, 2, 30);
                                _sanaGuidance = EditorGUILayout.Slider("Guidance", _sanaGuidance, 1, 10);
                                GUILayout.Label("Sana Edit V2 · 512 × 512 · Reference image required", MNNStudioUI.Caption);
                            }
                            else
                            {
                                _negativePrompt = EditorGUILayout.TextField("Negative prompt", _negativePrompt);
                                _imageSteps = EditorGUILayout.IntSlider("Denoising steps", _imageSteps, 2, 50);
                                _imageGuidance = EditorGUILayout.Slider("Guidance", _imageGuidance, 1, 20);
                                GUILayout.Label("SD 1.5 · 512 × 512 · CPU", MNNStudioUI.Caption);
                            }
                        }

                        if (CurrentTask.Task == MNNStudioTask.ImageGeneration || CurrentTask.Task == MNNStudioTask.SpeechSynthesis && (Selected.TtsProfile == MNNTtsModelProfile.Supertonic || Selected.RequiresReferenceAudio || Selected.IsCosyVoice || Selected.IsCosyVoice300M || Selected.IsQwen3Explicit))
                            _generationSeed = EditorGUILayout.IntField("Seed", _generationSeed);
                    }

                    if (Selected.IsKeywordSpotter)
                        DrawKeywordListening();

                    if ((CurrentTask.Required & MNNModelCapabilities.AudioInput) != 0)
                    {
                        using (new EditorGUI.DisabledScope(Busy))
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (MNNStudioUI.Button(_audio.IsRecording ? "Stop recording" : "Record audio", 128))
                            {
                                if (_audio.IsRecording)
                                    FinishRecording(false);
                                else
                                    Try(() => _audio.StartRecording());
                            }

                            if (_audio.IsRecording && MNNStudioUI.Button("Cancel", 68))
                                _audio.CancelRecording();
                            if (_audio.IsRecording)
                                GUILayout.Label(_audio.RecordingSeconds.ToString("0") + " / 30 s", MNNStudioUI.Caption);
                        }
                    }

                    if (CurrentTask.Task == MNNStudioTask.Omni && Selected.Supports(MNNModelCapabilities.SpeechOutput))
                        using (new EditorGUI.DisabledScope(Busy))
                            _taskSpeech = EditorGUILayout.Toggle("Generate spoken reply", _taskSpeech);
                    GUILayout.Space(16);
                    string runBlockedReason = GetTaskRunBlockedReason();
                    if (runBlockedReason != null && !Busy)
                        EditorGUILayout.HelpBox(runBlockedReason, MessageType.Info);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(runBlockedReason != null))
                            if (MNNStudioUI.Button(CurrentTask.Task == MNNStudioTask.SpeechRecognition ? "Transcribe" : "Run", 128, primary: true, tooltip: runBlockedReason))
                                RunTask();
                        if (Event.current.type == EventType.Repaint)
                            _taskRunScreenRect = GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect());
                        if (Busy && MNNStudioUI.Button("Stop", 68))
                            StopReply();
                        GUILayout.FlexibleSpace();
                        if (MNNStudioUI.Button("Find models", 112, quiet: true))
                            MNNModelManagerWindow.ShowForTask(CurrentTask);
                    }

                    if (_retrieval != null)
                    {
                        GUILayout.Space(20);
                        if (_taskResultMessage == null || _taskResultMessage.Result != _retrieval)
                            _taskResultMessage = new MNNStudioMessage(false, _retrieval.Text, result: _retrieval);
                        DrawMessage(_taskResultMessage);
                        if (_retrieval.GeneratedImage != null)
                            DrawGeneratedImage(_retrieval.GeneratedImage);
                        if (MNNStudioUI.Button("Copy result", 110))
                            EditorGUIUtility.systemCopyBuffer = _retrieval.Text ?? "";
                    }
                    else if (Busy && !string.IsNullOrEmpty(_streamText))
                    {
                        GUILayout.Space(20);
                        _streamMarkdown?.Draw(ContentWidth);
                    }

                    GUILayout.Space(24);
                }
            }
        }

        private void DrawTaskAttachment(bool image)
        {
            string path = image ? _imagePath : _audioPath;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MNNStudioUI.Button(image ? "Choose image…" : "Choose WAV…", 140))
                    PickAttachment(image);
                if (!image && Event.current.type == EventType.Repaint)
                    _taskAudioAttachmentScreenRect = GUIUtility.GUIToScreenRect(GUILayoutUtility.GetLastRect());
                if (!string.IsNullOrEmpty(path))
                {
                    GUILayout.Label(new GUIContent(Path.GetFileName(path), path), MNNStudioUI.Caption, GUILayout.MinWidth(40));
                    if (MNNStudioUI.Button("×", 28, quiet: true))
                    {
                        if (image)
                            _imagePath = null;
                        else
                            _audioPath = null;
                    }
                }
            }

            if (image && !string.IsNullOrEmpty(path) && Preview(path) != null)
                GUILayout.Label(Preview(path), GUILayout.Height(100), GUILayout.MaxWidth(ContentWidth));
            GUILayout.Space(8);
        }

        private void DrawReferenceRecordingControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(Busy))
            {
                if (MNNStudioUI.Button(_audio.IsRecording ? "Stop recording" : "Record reference", 142,
                    tooltip: "Record the voice sample used to condition speech synthesis."))
                {
                    if (_audio.IsRecording)
                        FinishRecording(false);
                    else
                        Try(() => _audio.StartRecording());
                }

                if (_audio.IsRecording && MNNStudioUI.Button("Cancel", 68, quiet: true))
                    _audio.CancelRecording();
                if (_audio.IsRecording)
                    GUILayout.Label(_audio.RecordingSeconds.ToString("0.0") + " s · stop by 15 s", MNNStudioUI.Caption);
            }
            GUILayout.Space(8);
        }

        private bool CanRunTask() => GetTaskRunBlockedReason() == null;

        private string GetTaskRunBlockedReason()
        {
            if (_keywordListener != null)
                return "Stop listening before running a recording.";
            if (Busy)
                return "Finish or stop the current request before running another.";
            if (_audio == null)
                return "Studio is not initialized. Reopen the window to continue.";
            if (_audio.IsRecording)
                return "Stop recording before running the request.";
            if (!MNNPlatformSupport.IsSupported)
                return MNNPlatformSupport.UnsupportedReason;
            if (!CurrentTask.CanRun(Selected))
                return "Choose a complete, compatible model for " + CurrentTask.Name + ".";
            if (CurrentTask.Task == MNNStudioTask.NLP)
            {
                if (string.IsNullOrWhiteSpace(_query)) return "Enter the situation to analyze.";
                try { BuildDecisionQuestion(); }
                catch (ArgumentException error) { return error.Message; }
            }
            if (CurrentTask.Task == MNNStudioTask.EchoCancellation && !File.Exists(_aecReferencePath))
                return "Choose the synchronized playback reference WAV.";
            if (Selected.IsSeg && !File.Exists(_gestureFeaturesPath))
                return "Choose the synchronized SEG gesture features JSON.";
            if (Selected.IsSpExPlus && !File.Exists(_speakerReferencePath))
                return "Choose a separate recording of the target speaker.";
            if (Selected.RequiresVisualFrames && !Directory.Exists(_avFramesDirectory))
                return "Choose the target speaker's aligned face-frame folder at 25 FPS.";
            if (Selected.IsKeywordSpotter && KeywordInputError() != null)
                return KeywordInputError();
            if (Selected.RequiresReferenceAudio && !File.Exists(_audioPath))
                return "Choose or record a reference WAV to enable synthesis.";
            if (Selected.RequiresReferenceAudio && Selected.RequiresReferenceTranscript && string.IsNullOrWhiteSpace(_referenceText))
                return "Enter the reference recording’s transcript to enable synthesis.";
            if (Selected.IsSana && !File.Exists(_imagePath))
                return "Choose a reference image to enable image editing.";
            if ((CurrentTask.Required & MNNModelCapabilities.Vision) != 0 && CurrentTask.Task != MNNStudioTask.Omni && !File.Exists(_imagePath))
                return "Choose an image to run this request.";
            if (Selected.IsTurnSense) return null;
            if ((CurrentTask.Required & MNNModelCapabilities.AudioInput) != 0 && CurrentTask.Task != MNNStudioTask.Omni && !Selected.IsVoxTurn && !File.Exists(_audioPath))
                return "Choose or record audio to run this request.";
            if (Selected.IsVoxTurn) return VoxTurnValidation();
            return !string.IsNullOrWhiteSpace(_query) || !string.IsNullOrEmpty(TaskExample) ? null : "Enter an instruction to run this request.";
        }

        private void RunTask()
        {
            if (!CanRunTask())
                return;
            Try(() =>
            {
                string prompt = Selected.IsTurnSense ? BuildTurnSensePrompt() : Selected.IsVoxTurn ? (_query ?? string.Empty) : string.IsNullOrWhiteSpace(_query) ? TaskExample : _query.Trim();
                if (prompt.Length > MNNStudioPrompt.MaximumCharacters)
                    throw new ArgumentException("Instruction is too long (16,000 characters maximum).");
                EnsureSession();
                _error = null;
                _retrieval = null;
                _streamText = "";
                _streamMarkdown = null;
                ClearGeneratedImage();
                _markdown.Clear();
                _taskResultMessage = null;
                _session.Send(new MNNStudioRequest{TurnLanguage = _voxTurnLanguage == 1 ? "zh" : "en", TurnSpeaker = _voxTurnSpeaker == 1 ? "agent" : "user", TurnQuestions = Selected.IsVoxTurn ? BuildVoxTurnQuestions() : null, GestureFeatures = Selected.IsSeg ? _gestureFeaturesPath : null, SpeakerReference = Selected.IsSpExPlus ? _speakerReferencePath : null, VisualFrames = Selected.RequiresVisualFrames ? MNNStudioAvFrames.Read(_avFramesDirectory) : null, PlaybackReference = CurrentTask.Task == MNNStudioTask.EchoCancellation ? _aecReferencePath : null, DecisionQuestion = CurrentTask.Task == MNNStudioTask.NLP ? BuildDecisionQuestion() : null, Keywords = _keywords, KeywordThreshold = _keywordThreshold, KeywordScore = _keywordScore, RecognitionLanguage = Selected.SupportsWhisperChinese ? (_recognitionLanguage == 0 ? "en" : "zh") : null, OcrCroppedLine = _ocrCroppedLine, LayoutScoreThreshold = _layoutScoreThreshold, VisionImage = (Selected.HasVisionPipeline || Selected.HasImageEmbeddingPipeline || Selected.HasDocumentLayoutPipeline) ? MNNStudioImage.ReadRgb(_imagePath) : null, Prompt = prompt, Voice = CurrentTask.Task == MNNStudioTask.SpeechSynthesis ? Selected.TtsVoices.ElementAtOrDefault(_ttsVoice) : null, EmotionId = MNNIndicVits.StyleIds[Mathf.Clamp(_ttsEmotion, 0, MNNIndicVits.StyleIds.Length - 1)], Steps = CurrentTask.Task == MNNStudioTask.SpeechSynthesis ? (Selected.IsIndicF5 ? _indicF5Steps : Selected.IsDhVaani ? _dhVaaniSteps : _ttsSteps) : Selected.IsSana ? _sanaSteps : _imageSteps, Speed = _ttsSpeed, Seed = _generationSeed, Guidance = Selected.IsSana ? _sanaGuidance : _imageGuidance, NegativePrompt = _negativePrompt, ReferenceRgb = Selected.IsSana ? MNNStudioImage.ReadReference(_imagePath) : null, Conversation = new[]{new MNNChatMessage(MNNChatRole.User, prompt)}, Image = (CurrentTask.Required & MNNModelCapabilities.Vision) != 0 ? _imagePath : null, Audio = Selected.RequiresReferenceAudio || (CurrentTask.Required & MNNModelCapabilities.AudioInput) != 0 ? _audioPath : null, ReferenceText = _referenceText, SpeechLanguage = Selected.SpeechLanguages.ElementAtOrDefault(_speechLanguage) ?? Selected.SpeechLanguages[0], Speech = CurrentTask.Task == MNNStudioTask.Omni && _taskSpeech && Selected.Supports(MNNModelCapabilities.SpeechOutput), TokenLimit = _tokenLimit, ContextTokens = _contextTokens});
            });
        }

        private void DrawGeneratedImage(MNNGeneratedImage result, bool showSeed = true)
        {
            if (_previewResult != result)
            {
                ClearGeneratedImage();
                _generatedPreview = MNNStudioImage.CreateTexture(result);
                _previewResult = result;
            }

            float size = Mathf.Min(ContentWidth, 512);
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, _generatedPreview, ScaleMode.ScaleToFit);
            GUILayout.Label(result.Width + " × " + result.Height + (showSeed ? " · Seed " + result.Seed : ""), MNNStudioUI.Caption);
            if (MNNStudioUI.Button("Save PNG…", 120))
            {
                // Keep the clicked image even if the preview changes while the dialog is open.
                byte[] png = _generatedPreview.EncodeToPNG();
                ShowDialogAfterGUI(() =>
                {
                    string path = EditorUtility.SaveFilePanel("Save generated image", "", "mnn-image.png", "png");
                    if (!string.IsNullOrEmpty(path))
                        File.WriteAllBytes(path, png);
                });
            }
        }

        private void ClearGeneratedImage()
        {
            _poseSource = null;
            _poseOverlay = null;
            if (_generatedPreview != null)
                DestroyImmediate(_generatedPreview);
            _generatedPreview = null;
            _previewResult = null;
        }
    }
}
