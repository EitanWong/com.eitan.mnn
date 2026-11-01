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
                        string output = result.GeneratedImage != null ? result.GeneratedImage.Width + " × " + result.GeneratedImage.Height : result.Tokens > 0 ? result.Tokens + " tokens" : result.SampleRate > 0 ? result.SampleRate + " Hz" : "Result";
                        GUILayout.Label(output + (result.RealTimeFactor.HasValue ? "" : "  ·  " + result.Seconds.ToString("0.0") + " s") + (result.RepetitionStopped ? "  ·  Repeated output stopped" : result.Cancelled ? "  ·  Stopped" : result.Limited ? "  ·  Response limit reached" : ""), caption);
                        if (result.RealTimeFactor.HasValue)
                        {
                            GUILayout.Label(new GUIContent("RTF " + result.RealTimeFactor.Value.ToString("0.000") + "  ·  " + result.AudioSeconds.ToString("0.00") + " s audio  ·  " + result.Seconds.ToString("0.00") + " s generation", "RTF = generation time / audio duration. Below 1 is faster than real time. Includes text/reference processing, inference and PCM output; excludes model loading, playback and saving. For spoken chat replies, includes text generation too."), caption);
                        }

                        if (!string.IsNullOrEmpty(result.BackendDescription))
                            GUILayout.Label(result.BackendDescription, caption);
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
    }
}
