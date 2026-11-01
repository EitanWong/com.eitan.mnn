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
    }
}
