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
        private MNNStudioMessage _taskResultMessage;
        private int _generationSeed = 42, _ttsVoice, _ttsSteps = 5, _imageSteps = 20, _sanaSteps = 10;
        private float _sanaGuidance = 4.5f;
        private float _ttsSpeed = 1, _imageGuidance = 7.5f;
        private string _negativePrompt = "";
        private Texture2D _generatedPreview;
        private MNNGeneratedImage _previewResult;
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

        private string TaskExample => Selected?.IsBertVits2 == true ? "你好，欢迎使用语音合成。" : CurrentTask.Example;
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
            string path = EditorUtility.OpenFolderPanel("Choose the complete MNN model folder", _customRoot, "");
            if (!string.IsNullOrEmpty(path))
                UseModelForTask(path, CurrentTask.Task);
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
                        Application.OpenURL("https://modelscope.cn/models/" + repository);
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
                        GUILayout.Label("Instruction", MNNStudioUI.Heading);
                        GUILayout.Space(8);
                        _query = EditorGUILayout.TextArea(_query, MNNStudioUI.Input, GUILayout.Height(80));
                        if (!string.IsNullOrEmpty(TaskExample) && MNNStudioUI.Button("Use example", 112, quiet: true))
                            _query = TaskExample;
                        GUILayout.Space(12);
                        if ((CurrentTask.Required & MNNModelCapabilities.Vision) != 0)
                            DrawTaskAttachment(true);
                        if ((CurrentTask.Required & MNNModelCapabilities.AudioInput) != 0)
                            DrawTaskAttachment(false);
                        if (CurrentTask.Task == MNNStudioTask.SpeechSynthesis)
                        {
                            string[] voices = Selected.TtsVoices;
                            _ttsVoice = EditorGUILayout.Popup("Voice", Mathf.Clamp(_ttsVoice, 0, voices.Length - 1), voices);
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

                            if (!Selected.IsPiper && !Selected.IsBertVits2)
                            {
                                _ttsSteps = EditorGUILayout.IntSlider("Synthesis steps", _ttsSteps, 1, 30);
                                _ttsSpeed = EditorGUILayout.Slider("Speed", _ttsSpeed, .5f, 2);
                            }

                            GUILayout.Label(Selected.IsBertVits2 ? "Chenxi · Chinese text · Maximum 300 characters" : Selected.IsPiper ? "English voices · eSpeak-NG required (install or set MNN_ESPEAK_NG_PATH) · Maximum 1000 characters" : "English vocabulary · Maximum 500 characters and 30 seconds per request", MNNStudioUI.Caption);
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

                        if (CurrentTask.Task == MNNStudioTask.ImageGeneration || CurrentTask.Task == MNNStudioTask.SpeechSynthesis && !Selected.IsPiper && !Selected.IsBertVits2)
                            _generationSeed = EditorGUILayout.IntField("Seed", _generationSeed);
                    }

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
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(!CanRunTask()))
                            if (MNNStudioUI.Button(CurrentTask.Task == MNNStudioTask.SpeechRecognition ? "Transcribe" : "Run", 128, primary: true))
                                RunTask();
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

        private bool CanRunTask()
        {
            if (Busy || _audio == null || _audio.IsRecording || !MNNPlatformSupport.IsSupported || !CurrentTask.CanRun(Selected))
                return false;
            if (Selected.IsSana && !File.Exists(_imagePath))
                return false;
            if ((CurrentTask.Required & MNNModelCapabilities.Vision) != 0 && CurrentTask.Task != MNNStudioTask.Omni && !File.Exists(_imagePath))
                return false;
            if ((CurrentTask.Required & MNNModelCapabilities.AudioInput) != 0 && CurrentTask.Task != MNNStudioTask.Omni && !File.Exists(_audioPath))
                return false;
            return !string.IsNullOrWhiteSpace(_query) || !string.IsNullOrEmpty(TaskExample);
        }

        private void RunTask()
        {
            if (!CanRunTask())
                return;
            Try(() =>
            {
                string prompt = string.IsNullOrWhiteSpace(_query) ? TaskExample : _query.Trim();
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
                _session.Send(new MNNStudioRequest{Prompt = prompt, Voice = CurrentTask.Task == MNNStudioTask.SpeechSynthesis ? Selected.TtsVoices.ElementAtOrDefault(_ttsVoice) : null, Steps = CurrentTask.Task == MNNStudioTask.SpeechSynthesis ? _ttsSteps : Selected.IsSana ? _sanaSteps : _imageSteps, Speed = _ttsSpeed, Seed = _generationSeed, Guidance = Selected.IsSana ? _sanaGuidance : _imageGuidance, NegativePrompt = _negativePrompt, ReferenceRgb = Selected.IsSana ? MNNStudioImage.ReadReference(_imagePath) : null, Conversation = new[]{new MNNChatMessage(MNNChatRole.User, prompt)}, Image = (CurrentTask.Required & MNNModelCapabilities.Vision) != 0 ? _imagePath : null, Audio = (CurrentTask.Required & MNNModelCapabilities.AudioInput) != 0 ? _audioPath : null, Speech = CurrentTask.Task == MNNStudioTask.Omni && _taskSpeech && Selected.Supports(MNNModelCapabilities.SpeechOutput), TokenLimit = _tokenLimit, ContextTokens = _contextTokens});
            });
        }

        private void DrawGeneratedImage(MNNGeneratedImage result)
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
            GUILayout.Label(result.Width + " × " + result.Height + " · Seed " + result.Seed, MNNStudioUI.Caption);
            if (MNNStudioUI.Button("Save PNG…", 120))
            {
                string path = EditorUtility.SaveFilePanel("Save generated image", "", "mnn-image.png", "png");
                if (!string.IsNullOrEmpty(path))
                    Try(() => File.WriteAllBytes(path, _generatedPreview.EncodeToPNG()));
            }
        }

        private void ClearGeneratedImage()
        {
            if (_generatedPreview != null)
                DestroyImmediate(_generatedPreview);
            _generatedPreview = null;
            _previewResult = null;
        }
    }
}
