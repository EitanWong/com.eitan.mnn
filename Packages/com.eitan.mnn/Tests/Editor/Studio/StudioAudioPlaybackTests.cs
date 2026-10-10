using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class StudioAudioPlaybackTests
    {
        [TestCase("image.PNG", MNNModelCapabilities.Vision, true)]
        [TestCase("clip.WAV", MNNModelCapabilities.AudioInput, true)]
        [TestCase("image.png", MNNModelCapabilities.Text, false)]
        [TestCase("image.png", MNNModelCapabilities.AudioInput, false)]
        [TestCase("clip.mp3", MNNModelCapabilities.AudioInput, false)]
        public void MediaDrop_RequiresExistingSupportedFormatAndModelCapability(string name, MNNModelCapabilities capability, bool expected)
        {
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioAudioTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, name);
            File.WriteAllBytes(path, new byte[]{1});
            try
            {
                var model = new MNNStudioModel("Drop test", root, MNNStudioTask.Chat, capability);
                bool result = MNNChatStudio.CanAttachDrop(path, model, false, false, out _, out _);
                Assert.AreEqual(expected, result);
                Assert.IsFalse(MNNChatStudio.CanAttachDrop(path, model, true, false, out _, out _));
                Assert.IsFalse(MNNChatStudio.CanAttachDrop(path, model, false, true, out _, out _));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private sealed class SpeechBackend : IMNNStudioBackend
        {
            public MNNStudioRequest LastRequest;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                LastRequest = request;
                var samples = new float[24000];
                for (int i = 0; i < samples.Length; ++i)
                    samples[i] = .03f * Mathf.Sin(2 * Mathf.PI * 440 * i / 24000);
                return new MNNStudioResult{Text = "Hello", Tokens = 1, Waveform = samples, SampleRate = 24000};
            }

            public void Dispose()
            {
            }
        }

        private sealed class WaveformBackend : IMNNStudioBackend
        {
            private readonly float[] _waveform;
            private readonly int _sampleRate;
            internal WaveformBackend(float[] waveform, int sampleRate)
            {
                _waveform = waveform;
                _sampleRate = sampleRate;
            }

            public MNNStudioResult Run(MNNStudioRequest request) => new MNNStudioResult{Text = "Hello", Tokens = 1, Waveform = _waveform, SampleRate = _sampleRate};
            public void Dispose()
            {
            }
        }

        [UnityTest]
        public IEnumerator SpokenReplies_AutoplayInOrdinaryChatWithoutVoiceLoop()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var type = typeof(MNNChatStudio);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioAudioTests", Guid.NewGuid().ToString("N")));
            type.GetField("_audio", flags).SetValue(window, new MNNStudioAudio());
            type.GetField("_speech", flags).SetValue(window, true);
            type.GetMethod("ResetWorkspace", flags).Invoke(window, null);
            var backend = new SpeechBackend();
            var session = new MNNStudioSession(backend);
            type.GetField("_session", flags).SetValue(window, session);
            type.GetField("_speech", flags).SetValue(window, true);
            type.GetField("_autoTitle", flags).SetValue(window, false);
            type.GetField("_historyPath", flags).SetValue(window, Path.Combine(root, "history.json"));
            type.GetField("_history", flags).SetValue(window, new MNNStudioHistory());
            type.GetField("_available", flags).SetValue(window, new List<MNNStudioModel>{new MNNStudioModel("Qwen2.5-Omni-3B-MNN", root, MNNStudioTask.Chat, MNNModelCapabilities.SpeechOutput | MNNModelCapabilities.AudioInput)});
            type.GetField("_modelIndex", flags).SetValue(window, 0);
            type.GetField("_draft", flags).SetValue(window, "Say hello");
            session.Completed += () => type.GetMethod("OnCompleted", flags).Invoke(window, null);
            try
            {
                type.GetMethod("Send", flags).Invoke(window, null);
                for (int i = 0; i < 60 && session.Busy; ++i)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.IsFalse(session.Busy);
                Assert.IsFalse((bool)type.GetField("_voiceLoop", flags).GetValue(window));
                Assert.IsNotNull(backend.LastRequest);
                Assert.IsTrue(backend.LastRequest.Speech, "The ordinary composer must request Omni speech when spoken replies are enabled.");
                var audio = (MNNStudioAudio)type.GetField("_audio", flags).GetValue(window);
                for (int i = 0; i < 120 && audio.IsLoading; ++i)
                    yield return null;
                Assert.IsNull(audio.ConsumePlaybackError());
                Assert.IsTrue(audio.IsPlaying);
                Assert.AreEqual(2, ((List<MNNStudioMessage>)type.GetField("_messages", flags).GetValue(window)).Count);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                session.Dispose();
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [TestCase(.04f, .48f)]
        [TestCase(.1f, .72f)]
        public void RecordingMeterEnvelope_MakesQuietSpeechVisibleAndStaysBounded(float rms, float minimum)
        {
            float level = MNNStudioVoiceEnvelope.Next(0, rms, .05f);
            Assert.GreaterOrEqual(level, minimum);
            Assert.LessOrEqual(level, 1f);
            float silent = MNNStudioVoiceEnvelope.Next(level, 0, .18f);
            Assert.Less(silent, level);
            Assert.GreaterOrEqual(silent, 0);
        }

        [UnityTest]
        public IEnumerator Omni_GeneratedSpeechReallyPlaysAndAdvances()
        {
            MNNMultimodalResult speech;
            using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen2.5-Omni-3B-MNN")))
                speech = model.GenerateConversationStreaming(new[]{new MNNChatMessage(MNNChatRole.User, "Say hello in one short sentence.")}, _ =>
                {
                }, 32, generateSpeech: true);
            Assert.Greater(speech.Waveform.Length, 2400);
            Assert.AreEqual(24000, speech.SampleRate);
            double energy = 0;
            foreach (float sample in speech.Waveform)
            {
                Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                energy += sample * sample;
            }

            Assert.Greater(energy / speech.Waveform.Length, 1e-8);
            string root = Path.GetFullPath(Path.Combine("Library", "MNN", "StudioAudioTests", Guid.NewGuid().ToString("N")));
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var type = typeof(MNNChatStudio);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var audio = new MNNStudioAudio();
            var session = new MNNStudioSession(new WaveformBackend(speech.Waveform, speech.SampleRate));
            type.GetField("_audio", flags).SetValue(window, audio);
            type.GetField("_session", flags).SetValue(window, session);
            type.GetField("_history", flags).SetValue(window, new MNNStudioHistory());
            type.GetField("_historyPath", flags).SetValue(window, Path.Combine(root, "history.json"));
            type.GetField("_autoTitle", flags).SetValue(window, false);
            session.Completed += () => type.GetMethod("OnCompleted", flags).Invoke(window, null);
            try
            {
                session.Send(new MNNStudioRequest{Prompt = "Say hello", Speech = true});
                for (int i = 0; i < 60 && session.Busy; ++i)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.IsFalse(session.Busy);
                for (int i = 0; i < 120 && audio.IsLoading; ++i)
                    yield return null;
                Assert.IsFalse(audio.IsLoading, "Unity's WAV decoder should finish before autoplay is considered ready.");
                Assert.IsTrue(audio.IsPlaying, "The Omni waveform must autoplay through MNN Studio's completion path.");
                var utility = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
                var position = utility.GetMethod("GetPreviewClipPosition", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                double started = EditorApplication.timeSinceStartup;
                while (EditorApplication.timeSinceStartup - started < .15)
                    yield return null;
                Assert.IsTrue(audio.IsPlaying);
                Assert.Greater(Convert.ToDouble(position.Invoke(null, null)), .05);
                audio.StopPreview();
                Assert.IsFalse(audio.IsPlaying);
                bool wasMuted = EditorUtility.audioMasterMute;
                try
                {
                    EditorUtility.audioMasterMute = false;
                    Invoke(window, "PlayGeneratedSpeech", new MNNStudioResult{Waveform = speech.Waveform, SampleRate = speech.SampleRate});
                    for (int i = 0; i < 120 && audio.IsLoading; ++i)
                        yield return null;
                    Assert.IsFalse(audio.IsLoading, "The same manual Play action must finish Unity WAV decoding.");
                    Assert.IsTrue(audio.IsPlaying, "The same manual Play action used by the Studio message row must play the real Omni waveform.");
                    started = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - started < .15)
                        yield return null;
                    Assert.Greater(Convert.ToDouble(position.Invoke(null, null)), .05);
                }
                finally
                {
                    EditorUtility.audioMasterMute = wasMuted;
                    audio.StopPreview();
                }

                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                audio.Dispose();
                session.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [UnityTest]
        public IEnumerator GeneratedPcm_StartsActualEditorPlaybackAndStopsCleanly()
        {
            using (var audio = new MNNStudioAudio())
            {
                var samples = new float[24000];
                for (int i = 0; i < samples.Length; ++i)
                    samples[i] = .03f * Mathf.Sin(2 * Mathf.PI * 440 * i / 24000);
                audio.Play(samples, 24000);
                for (int i = 0; i < 120 && audio.IsLoading; ++i)
                    yield return null;
                Assert.IsFalse(audio.IsLoading, "The generated PCM must be decoded by Unity's WAV decoder.");
                Assert.IsNull(audio.ConsumePlaybackError());
                var utility = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
                var playing = utility.GetMethod("IsPreviewClipPlaying", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.IsNotNull(playing);
                Assert.IsTrue((bool)playing.Invoke(null, null), "Unity's preview backend must actually start; elapsed time alone is not playback evidence.");
                Assert.IsTrue(audio.IsPlaying);
                audio.StopPreview();
                Assert.IsFalse(audio.IsPlaying);
                Assert.IsFalse((bool)playing.Invoke(null, null));
                LogAssert.NoUnexpectedReceived();
            }
        }

        [UnityTest]
        public IEnumerator StudioPlayAction_ReportsEditorMuteAndUnmutesOnlyAfterExplicitEnable()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            bool wasMuted = EditorUtility.audioMasterMute;
            var audio = new MNNStudioAudio();
            typeGetField(window, "_audio", flags, audio);
            var samples = new float[24000];
            for (int i = 0; i < samples.Length; ++i)
                samples[i] = .15f * Mathf.Sin(2 * Mathf.PI * 440 * i / 24000);
            var result = new MNNStudioResult{Waveform = samples, SampleRate = 24000};
            try
            {
                EditorUtility.audioMasterMute = true;
                Assert.IsTrue(MNNStudioAudio.EditorAudioMuted);
                var muted = Assert.Throws<TargetInvocationException>(() => Invoke(window, "PlayGeneratedSpeech", result));
                Assert.IsInstanceOf<InvalidOperationException>(muted.InnerException);
                Assert.IsTrue(EditorUtility.audioMasterMute, "Normal Play must not silently override the user's global mute setting.");
                Invoke(window, "EnableEditorAudioAndPlay", result);
                Assert.IsFalse(EditorUtility.audioMasterMute);
                Assert.IsTrue(audio.IsPlaying, "The Studio button action must start preview playback after audio is enabled.");
                yield return null;
                audio.StopPreview();
                Assert.IsFalse(audio.IsPlaying);
            }
            finally
            {
                EditorUtility.audioMasterMute = wasMuted;
                audio.Dispose();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void typeGetField(object target, string name, BindingFlags flags, object value)
        {
            typeof(MNNChatStudio).GetField(name, flags).SetValue(target, value);
        }

        private static void Invoke(object target, string name, object argument)
        {
            typeof(MNNChatStudio).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, new[]{argument});
        }
    }
}
