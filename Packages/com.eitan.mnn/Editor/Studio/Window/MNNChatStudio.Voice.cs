using System;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private bool _voicePanel, _voiceMuted, _reduceMotion, _autoSend = true;
        private readonly MNNStudioVoiceActivity _voiceActivity = new MNNStudioVoiceActivity();
        private float _voiceLevel;
        private double _voiceLevelTime;
        private Rect _voiceOrbRect, _voiceControlsRect;
        private string _streamText = "";
        private MNNStudioMarkdown _streamMarkdown;
        private MNNGenerationUpdate _shownProgress;
        private MNNStudioVoiceState VoiceState => !string.IsNullOrEmpty(_error) ? MNNStudioVoiceState.Error : _voiceMuted ? MNNStudioVoiceState.Muted : Busy ? MNNStudioVoiceState.Thinking : _audio != null && _audio.IsRecording ? MNNStudioVoiceState.Listening : _audio != null && _audio.IsPlaying ? MNNStudioVoiceState.Speaking : MNNStudioVoiceState.Ready;
        private void BeginVoice()
        {
            if (Selected == null || !Selected.Supports(MNNModelCapabilities.AudioInput | MNNModelCapabilities.SpeechOutput))
                return;
            _voicePanel = true;
            _voiceMuted = false;
            _voiceLoop = _speech = true;
            _error = null;
            if (!Busy)
                StartVoiceCapture();
            else
                _nextListen = EditorApplication.timeSinceStartup + .25;
        }

        private void StartVoiceCapture()
        {
            if (_voiceMuted || !_voiceLoop)
                return;
            Try(() =>
            {
                _audio.StartRecording();
                _voiceActivity.Reset(EditorApplication.timeSinceStartup);
            });
        }

        private void EndVoice()
        {
            _session?.StopGeneration();
            StopVoiceLoop();
            _voicePanel = false;
            _voiceMuted = false;
        }

        private void ToggleVoiceMute()
        {
            _voiceMuted = !_voiceMuted;
            if (_voiceMuted)
            {
                _audio.CancelRecording();
                _nextListen = 0;
            }
            else if (!Busy && !_audio.IsPlaying)
                StartVoiceCapture();
            else
                _nextListen = EditorApplication.timeSinceStartup + .25;
        }

        private void InterruptVoice()
        {
            _audio.StopPreview();
            _nextListen = 0;
            if (Busy)
                _session.StopGeneration();
            else if (_voiceLoop && !_voiceMuted)
                StartVoiceCapture();
        }

        private void TickVoice(double now)
        {
            _audio.UpdateLevel();
            _voiceLevel = MNNStudioVoiceEnvelope.Next(_voiceLevel, _audio.Level, (float)Math.Min(.1, now - _voiceLevelTime));
            _voiceLevelTime = now;
            if (_voicePanel && _voiceLoop && !_voiceMuted && _autoSend && _audio.IsRecording && _voiceActivity.ShouldSend(now, _audio.Level))
                FinishRecording(true);
        }

        private void DrawVoicePanel()
        {
            GUILayout.FlexibleSpace();
            using (new CenteredContent(ContentWidth))
            {
                GUILayout.Label(Selected?.DisplayName ?? "Voice conversation", new GUIStyle(MNNStudioUI.Heading)
                {alignment = TextAnchor.MiddleCenter});
                GUILayout.Space(16);
                float size = Mathf.Clamp(position.height - 300, 112, 230);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    var rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
                    if (Event.current.type == EventType.Repaint)
                        _voiceOrbRect = rect;
                    MNNStudioVoiceOrb.Draw(rect, VoiceState, _voiceLevel, EditorApplication.timeSinceStartup, _reduceMotion);
                    GUILayout.FlexibleSpace();
                }

                GUILayout.Space(12);
                string status = VoiceState == MNNStudioVoiceState.Listening ? "Listening…" : VoiceState == MNNStudioVoiceState.Thinking ? "Thinking…" : VoiceState == MNNStudioVoiceState.Speaking ? "Speaking…" : VoiceState == MNNStudioVoiceState.Muted ? "Microphone muted" : VoiceState == MNNStudioVoiceState.Error ? "Voice paused" : "Ready when you are";
                GUILayout.Label(status, new GUIStyle(MNNStudioUI.Heading)
                {alignment = TextAnchor.MiddleCenter}, GUILayout.Height(24));
                GUILayout.Space(4);
                GUILayout.Label("Turn-based voice · microphone pauses during replies", new GUIStyle(MNNStudioUI.Caption)
                {alignment = TextAnchor.MiddleCenter}, GUILayout.Height(20));
                GUILayout.Space(18);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (MNNStudioUI.Button(_voiceMuted ? "Unmute" : "Mute", 76, tooltip: "Mute microphone (Space)"))
                        ToggleVoiceMute();
                    using (new EditorGUI.DisabledScope(!_audio.IsRecording && !Busy && !_audio.IsPlaying))
                        if (MNNStudioUI.Button(_audio.IsRecording ? "Send turn" : "Interrupt", 100, tooltip: "Send recording or interrupt reply"))
                        {
                            if (_audio.IsRecording)
                                FinishRecording(true);
                            else
                                InterruptVoice();
                        }

                    if (MNNStudioUI.Button("End", 64, primary: true, tooltip: "End voice conversation (Escape)"))
                        EndVoice();
                    GUILayout.FlexibleSpace();
                }

                if (Event.current.type == EventType.Repaint)
                    _voiceControlsRect = GUILayoutUtility.GetLastRect();
                GUILayout.Space(12);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (MNNStudioUI.Button("Show chat", 92, quiet: true))
                        _voicePanel = false;
                    if (VoiceState == MNNStudioVoiceState.Error && MNNStudioUI.Button("Try again", 92, quiet: true))
                        BeginVoice();
                    GUILayout.FlexibleSpace();
                }
            }

            GUILayout.FlexibleSpace();
        }

        private void OnStreamUpdated()
        {
            if (_session?.Progress == null)
                return;
            if (_shownProgress == _session.Progress)
                return;
            _contextCompressionPending = false;
            _shownProgress = _session.Progress;
            _streamText = _session.Progress.Text;
            _streamMarkdown = new MNNStudioMarkdown(_streamText);
            if (_followOutput)
                _scroll.y = float.MaxValue;
        }

        private void StopReply()
        {
            _session?.StopGeneration();
            if (!_voicePanel)
                StopVoiceLoop();
        }
    }
}
