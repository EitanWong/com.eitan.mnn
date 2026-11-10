using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private MNNStudioKeywordListener _keywordListener;
        [SerializeField] private MNNKeywordLanguage _keywordLanguage;
        private string _keywordSettingsDirectory;
        private readonly List<MNNKeywordDetection> _liveKeywordMatches = new List<MNNKeywordDetection>();

        private void DrawKeywordSettings()
        {
            var language = MNNKeywordSpotterConfig.Read(Selected.Directory).Language;
            string defaults = language == MNNKeywordLanguage.English ? MNNKeywordSpotterConfig.EnglishDefaultWords : MNNKeywordSpotter.DefaultKeywords;
            if (_keywordSettingsDirectory != Selected.Directory)
            {
                _keywordSettingsDirectory = Selected.Directory;
                if (_keywords == MNNKeywordSpotter.DefaultKeywords || _keywords == MNNKeywordSpotterConfig.EnglishDefaultWords) _keywords = defaults;
                _keywordLanguage = MNNKeywordLanguage.Automatic;
            }
            GUILayout.Space(12);
            GUILayout.Label("Wake words · " + language, MNNStudioUI.Heading);
            GUILayout.Label(language == MNNKeywordLanguage.English ? "Type English wake words, one phrase per line. Example: Hey Siri" : "Type Chinese wake words, one phrase per line. Example: 小爱同学", MNNStudioUI.Caption);
            _keywordLanguage = EditorGUILayout.Popup("Wake word language", _keywordLanguage == MNNKeywordLanguage.Automatic ? 0 : 1,
                new[] { "Automatic · " + language + " model", language.ToString() }) == 0 ? MNNKeywordLanguage.Automatic : language;
            GUI.SetNextControlName("MNNStudioKeywords");
            string visibleWords = MNNKeywordText.GetDisplayText(_keywords);
            string editedWords = EditorGUILayout.TextArea(visibleWords, MNNStudioUI.Input, GUILayout.MinHeight(92));
            if (editedWords != visibleWords) _keywords = editedWords;
            string keywordError = KeywordInputError();
            if (keywordError != null) EditorGUILayout.HelpBox(keywordError, MessageType.Warning);
            _keywordThreshold = EditorGUILayout.Slider(new GUIContent("Detection threshold", "Higher values reduce triggers; lower values make wake words easier to detect."), _keywordThreshold, .05f, .95f);
            _keywordScore = EditorGUILayout.Slider(new GUIContent("Keyword boost", "Raises the search score of keyword tokens. Higher values can increase false triggers."), _keywordScore, 0, 5);
            if (MNNStudioUI.Button("Use example wake words", 180, quiet: true))
            {
                GUI.FocusControl(null);
                _keywords = defaults;
            }
            string assets = MNNKeywordSpotter.AssetDirectory(Selected.Directory);
            if (File.Exists(Path.Combine(assets, language == MNNKeywordLanguage.English ? "test_wavs/0.wav" : "test_wavs/3.wav")))
            {
                if (MNNStudioUI.Button("Load demo recording", 180, quiet: true))
                {
                    GUI.FocusControl(null);
                    Try(LoadKeywordDemo);
                }
                GUILayout.Label(language == MNNKeywordLanguage.English ? "Demo: detects light up. Select Run after loading." : "Demo: detects 文森特卡索 and 法国. Select Run after loading.", MNNStudioUI.Caption);
            }
            GUILayout.Space(8);
        }

        private void LoadKeywordDemo()
        {
            string assets = MNNKeywordSpotter.AssetDirectory(Selected.Directory);
            bool english = MNNKeywordSpotterConfig.Read(Selected.Directory).Language == MNNKeywordLanguage.English;
            // The demo input uses the same ordinary-text path as a user's custom phrases.
            string keywords = english ? "light up\nlovely child\nforever" : "文森特卡索\n周望军\n朱丽楠\n蒋友伯\n女儿\n法国\n见面会\n落实";
            string audio = Path.Combine(assets, english ? "test_wavs/0.wav" : "test_wavs/3.wav");
            if (!File.Exists(audio)) throw new FileNotFoundException("The KWS demo recording is missing. Download the complete KWS profile again.", audio);
            _keywords = keywords;
            _audioPath = audio;
            _retrieval = null;
            _taskResultMessage = null;
        }

        private void DrawKeywordListening()
        {
            GUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_keywordListener != null)
                {
                    if (MNNStudioUI.Button("Stop listening", 140)) StopKeywordListening();
                    GUILayout.Label("Listening for wake words…", MNNStudioUI.Caption);
                }
                else
                {
                    using (new EditorGUI.DisabledScope(Busy || _audio.IsRecording || !MNNPlatformSupport.IsSupported || !CurrentTask.CanRun(Selected) || KeywordInputError() != null))
                        if (MNNStudioUI.Button("Start listening", 140, primary: true))
                            Try(StartKeywordListening);
                    GUILayout.Label("Live microphone · local detection", MNNStudioUI.Caption);
                }
            }
        }

        private void StartKeywordListening()
        {
            if (Busy || _audio.IsRecording || Selected?.IsKeywordSpotter != true) return;
            MNNKeywordText.Prepare(_keywords, MNNKeywordSpotterConfig.Read(Selected.Directory).Language);
            ReleaseSession();
            _audio.StopPreview();
            _keywordListener = new MNNStudioKeywordListener(Selected.Directory, _keywords, _keywordThreshold, _keywordScore, _threads);
            _error = null;
            _liveKeywordMatches.Clear();
            _retrieval = new MNNStudioResult {Text = "Listening for wake words…", BackendDescription = "Zipformer2 KWS · CPU"};
            _taskResultMessage = null;
        }

        private string KeywordInputError()
        {
            try { MNNKeywordText.Prepare(_keywords, MNNKeywordSpotterConfig.Read(Selected.Directory).Language); return null; }
            catch (ArgumentException error) { return error.Message; }
        }

        private void TickKeywordListening()
        {
            if (_keywordListener == null) return;
            try
            {
                var matches = _keywordListener.Update();
                if (matches.Length == 0) return;
                _liveKeywordMatches.AddRange(matches);
                if (_liveKeywordMatches.Count > 100) _liveKeywordMatches.RemoveRange(0, _liveKeywordMatches.Count - 100);
                _retrieval = new MNNStudioResult {Text = MNNStudioNativeBackend.FormatKeywords(_liveKeywordMatches.ToArray()),
                    KeywordDetections = _liveKeywordMatches.ToArray(), BackendDescription = "Live Zipformer2 KWS · CPU"};
                _taskResultMessage = null;
                Repaint();
            }
            catch (Exception error)
            {
                StopKeywordListening();
                _error = error.Message;
            }
        }

        private void StopKeywordListening()
        {
            if (_keywordListener == null) return;
            var listener = _keywordListener;
            _keywordListener = null;
            listener.Dispose();
            if (_retrieval != null && _liveKeywordMatches.Count == 0)
                _retrieval.Text = "Listening stopped. No wake word detected.";
        }
    }
}
