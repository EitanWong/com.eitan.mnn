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
        private readonly List<MNNKeywordDetection> _liveKeywordMatches = new List<MNNKeywordDetection>();

        private void DrawKeywordSettings()
        {
            GUILayout.Space(12);
            GUILayout.Label("Wake words · Chinese", MNNStudioUI.Heading);
            GUILayout.Label("One phrase per line: space-separated tone-marked pinyin tokens, then @name. Example: x iǎo ài t óng x ué @小爱同学", MNNStudioUI.Caption);
            GUI.SetNextControlName("MNNStudioKeywords");
            _keywords = EditorGUILayout.TextArea(_keywords, MNNStudioUI.Input, GUILayout.MinHeight(92));
            _keywordThreshold = EditorGUILayout.Slider(new GUIContent("Detection threshold", "Higher values reduce triggers; lower values make wake words easier to detect."), _keywordThreshold, .05f, .95f);
            _keywordScore = EditorGUILayout.Slider(new GUIContent("Keyword boost", "Raises the search score of keyword tokens. Higher values can increase false triggers."), _keywordScore, 0, 5);
            if (MNNStudioUI.Button("Use example wake words", 180, quiet: true))
            {
                GUI.FocusControl(null);
                _keywords = MNNKwsModel.DefaultKeywords;
            }
            string assets = MNNKwsModel.AssetDirectory(Selected.Directory);
            if (File.Exists(Path.Combine(assets, "test_wavs/3.wav")) && File.Exists(Path.Combine(assets, "test_wavs/test_keywords.txt")))
            {
                if (MNNStudioUI.Button("Load demo recording", 180, quiet: true))
                {
                    GUI.FocusControl(null);
                    Try(LoadKeywordDemo);
                }
                GUILayout.Label("Demo: detects 文森特卡索 and 法国. Select Run after loading.", MNNStudioUI.Caption);
            }
            GUILayout.Space(8);
        }

        private void LoadKeywordDemo()
        {
            string assets = MNNKwsModel.AssetDirectory(Selected.Directory);
            string keywords = File.ReadAllText(Path.Combine(assets, "test_wavs/test_keywords.txt"));
            string audio = Path.Combine(assets, "test_wavs/3.wav");
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
                    using (new EditorGUI.DisabledScope(Busy || _audio.IsRecording || !MNNPlatformSupport.IsSupported || !CurrentTask.CanRun(Selected) || string.IsNullOrWhiteSpace(_keywords)))
                        if (MNNStudioUI.Button("Start listening", 140, primary: true))
                            Try(StartKeywordListening);
                    GUILayout.Label("Live microphone · local detection", MNNStudioUI.Caption);
                }
            }
        }

        private void StartKeywordListening()
        {
            if (Busy || _audio.IsRecording || Selected?.IsKeywordSpotter != true) return;
            ReleaseSession();
            _audio.StopPreview();
            _keywordListener = new MNNStudioKeywordListener(Selected.Directory, _keywords, _keywordThreshold, _keywordScore, _threads);
            _error = null;
            _liveKeywordMatches.Clear();
            _retrieval = new MNNStudioResult {Text = "Listening for wake words…", BackendDescription = "Zipformer2 Chinese KWS · CPU"};
            _taskResultMessage = null;
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
                    KeywordDetections = _liveKeywordMatches.ToArray(), BackendDescription = "Live Zipformer2 Chinese KWS · CPU"};
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
