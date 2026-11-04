using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private bool _voicePanel, _voiceMuted, _reduceMotion;
        private float _voiceLevel;
        private double _voiceLevelTime;
        private Rect _voiceOrbRect, _voiceControlsRect;
        private string _streamText = "";
        private MNNStudioMarkdown _streamMarkdown;
        private MNNGenerationUpdate _shownProgress;
        private MNNStudioRealtimeConversation _voiceConversation, _stoppingVoice;
        private MNNStudioModel[] _voiceRecognizers = Array.Empty<MNNStudioModel>(), _voiceSynthesizers = Array.Empty<MNNStudioModel>();
        private string[] _recognizerNames = Array.Empty<string>(), _synthesizerNames = Array.Empty<string>();
        private int _voiceRecognizer, _voiceSynthesizer;
        private MNNResponseId _voiceResponse;
        private MNNStudioVoiceState VoiceState => !string.IsNullOrEmpty(_error) ? MNNStudioVoiceState.Error :
            _voiceMuted ? MNNStudioVoiceState.Muted : _voiceConversation == null ? (Busy ? MNNStudioVoiceState.Thinking : MNNStudioVoiceState.Ready) :
            _voiceConversation.SpeakerAudible ? MNNStudioVoiceState.Speaking :
            !_voiceConversation.Ready || _voiceConversation.IsResponding ? MNNStudioVoiceState.Thinking : MNNStudioVoiceState.Listening;

        private void RefreshVoiceModels()
        {
            _voiceRecognizers = _models.Where(model => model.Task == MNNStudioTask.SpeechRecognition && model.HasSpeechRecognitionPipeline)
                .OrderByDescending(model => model.Name.IndexOf("sensevoice", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            _voiceSynthesizers = _models.Where(model => model.Task == MNNStudioTask.SpeechSynthesis && model.HasGenerationPipeline && !model.RequiresReferenceAudio)
                .OrderByDescending(model => model.Name.IndexOf("supertonic", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            _recognizerNames = _voiceRecognizers.Select(model => model.DisplayName).ToArray();
            _synthesizerNames = _voiceSynthesizers.Select(model => model.DisplayName).ToArray();
            _voiceRecognizer = Math.Max(0, Math.Min(_voiceRecognizer, _voiceRecognizers.Length - 1));
            _voiceSynthesizer = Math.Max(0, Math.Min(_voiceSynthesizer, _voiceSynthesizers.Length - 1));
        }

        private void BeginVoice()
        {
            if (Selected?.Task != MNNStudioTask.Chat || Busy) return;
            _voicePanel = true;
            Try(() =>
            {
                if (_voiceRecognizers.Length == 0 || _voiceSynthesizers.Length == 0)
                    throw new InvalidOperationException("Install a speech recognition model and a speech synthesis model in Model Manager, then Refresh. Choose voice models in Settings.");
                if (!MNNPlatformSupport.IsSupported) throw new PlatformNotSupportedException(MNNPlatformSupport.UnsupportedReason);
                var asr = _voiceRecognizers[_voiceRecognizer];
                var tts = _voiceSynthesizers[_voiceSynthesizer];
                var providers = new MNNConversationProviders
                {
                    Model = MNNConversationMnnProviders.Language(Selected.Directory, _threads, _tokenLimit, _thinking),
                    Asr = MNNConversationMnnProviders.Recognition(asr.Directory, 1),
                    Tts = MNNConversationMnnProviders.Speech(tts.Directory, new MNNSpeechSynthesisOptions
                    { Precision = tts.SupertonicPrecision }, _threads)
                };
                var history = new System.Collections.Generic.List<MNNOpenAIMessage>();
                foreach (var message in _messages)
                    history.Add(new MNNOpenAIMessage(message.IsUser ? MNNChatRole.User : MNNChatRole.Assistant, message.Text));
                ReleaseSession();
                _voiceConversation = new MNNStudioRealtimeConversation(providers, new MNNConversationOptions(), history, _modelRelease);
                _voiceMuted = false;
                _voiceLoop = _speech = true;
                _streamText = "";
                _voiceResponse = default;
                _error = null;
            });
        }

        private void EndVoice()
        {
            _session?.StopGeneration();
            CommitVoiceDisplay();
            StopVoiceLoop();
            _voicePanel = false;
            _voiceMuted = false;
        }

        private void ToggleVoiceMute()
        {
            _voiceMuted = !_voiceMuted;
            _voiceConversation?.SetMuted(_voiceMuted);
        }

        private void InterruptVoice()
        {
            CommitVoiceDisplay();
            if (_voiceConversation != null) ObserveVoiceInterruption(_voiceConversation.InterruptAsync());
        }
        private async void ObserveVoiceInterruption(System.Threading.Tasks.Task interruption)
        {
            try { await interruption; }
            catch (Exception error) { _error = error.GetBaseException().Message; StopVoiceLoop(); }
        }
        private void CommitVoiceDisplay()
        {
            if (_streamText.Length == 0) return;
            _messages.Add(new MNNStudioMessage(false, _streamText));
            _streamText = "";
            _streamMarkdown = null;
            RememberConversation();
        }

        private void TickVoice(double now)
        {
            if (_stoppingVoice?.Closed == true) _stoppingVoice = null;
            if (_stoppingVoice?.Error != null) _error = _stoppingVoice.Error;
            var voice = _voiceConversation;
            if (voice != null)
            {
                if (voice.Error != null) { _error = voice.Error; StopVoiceLoop(); return; }
                string warning = voice.TakeWarning();
                if (warning != null) _error = warning;
                if (voice.TakeExit()) { EndVoice(); return; }
                while (voice.TryRead(out var update))
                {
                    if (!update.Assistant)
                    {
                        CommitVoiceDisplay();
                        _messages.Add(new MNNStudioMessage(true, update.Text));
                        RememberConversation();
                    }
                    else
                    {
                        if (update.Text == null) continue;
                        if (_voiceResponse.IsValid && _voiceResponse != update.Response) CommitVoiceDisplay();
                        _voiceResponse = update.Response;
                        _streamText = update.Text;
                        _streamMarkdown = new MNNStudioMarkdown(_streamText);
                    }
                    if (_followOutput) _scroll.y = float.MaxValue;
                }
                if (voice.ReplyPresented) CommitVoiceDisplay();
            }
            _audio.UpdateLevel();
            float level = voice?.Level ?? _audio.Level;
            _voiceLevel = MNNStudioVoiceEnvelope.Next(_voiceLevel, level, (float)Math.Min(.1, now - _voiceLevelTime));
            _voiceLevelTime = now;
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
                GUILayout.Label("Continuous capture · automatic turns · interrupt with the button", new GUIStyle(MNNStudioUI.Caption)
                {alignment = TextAnchor.MiddleCenter}, GUILayout.Height(20));
                GUILayout.Label(_voiceConversation?.Diagnostics ?? "Choose recognition and synthesis models in Settings", MNNStudioUI.Caption);
                GUILayout.Space(18);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (MNNStudioUI.Button(_voiceMuted ? "Unmute" : "Mute", 76, tooltip: "Mute microphone (Space)"))
                        ToggleVoiceMute();
                    using (new EditorGUI.DisabledScope(_voiceConversation == null || !_voiceConversation.IsResponding && !_voiceConversation.SpeakerAudible))
                        if (MNNStudioUI.Button("Interrupt", 100, tooltip: "Interrupt reply and keep listening"))
                            InterruptVoice();

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
            StopKeywordListening();
            _session?.StopGeneration();
            if (_voiceConversation != null) InterruptVoice();
            if (!_voicePanel)
                StopVoiceLoop();
        }
    }
}
