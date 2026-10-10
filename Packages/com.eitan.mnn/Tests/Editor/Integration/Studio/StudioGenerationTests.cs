using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class StudioGenerationTests
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Get(MNNChatStudio w, string name) => typeof(MNNChatStudio).GetField(name, Flags).GetValue(w);
        private static void Set(MNNChatStudio w, string name, object value) => typeof(MNNChatStudio).GetField(name, Flags).SetValue(w, value);
        private static object Invoke(MNNChatStudio w, string name, params object[] args) => typeof(MNNChatStudio).GetMethod(name, Flags).Invoke(w, args);
        [UnityTest]
        public IEnumerator SupertonicWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true);
        [UnityTest]
        public IEnumerator DiffusionWorkspaceGeneratesDisplaysAndSavesActualImage() => Run(false);
        [UnityTest]
        public IEnumerator SanaWorkspaceEditsDisplaysAndSavesActualImage() => Run(false, true);
        [UnityTest]
        public IEnumerator PiperWorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "piper-voices-MNN");
        [UnityTest]
        public IEnumerator BertVits2WorkspaceGeneratesPlaysAndSavesActualAudio() => Run(true, repository: "bert-vits2-MNN");
        private static IEnumerator Run(bool tts, bool sana = false, string repository = null)
        {
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/StudioGeneration");
            Directory.CreateDirectory(artifacts);
            var model = MNNStudioModel.Read(GenerationTestData.Model(repository ?? (tts ? "supertonic-tts-mnn" : sana ? "MNN-Sana-Edit-V2" : "stable-diffusion-v1-5-mnn")));
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            bool muted = EditorUtility.audioMasterMute;
            try
            {
                EditorUtility.audioMasterMute = false;
                Set(window, "_autoTitle", false);
                Set(window, "_historyPath", Path.Combine(artifacts, "studio-test-history.json"));
                Set(window, "_taskIndex", (int)model.Task);
                Set(window, "_available", new List<MNNStudioModel>{model});
                Set(window, "_modelIndex", 0);
                Set(window, "_query", model.IsBertVits2 ? "你好，欢迎使用语音合成。" : tts ? "The capital of France is Paris." : sana ? "A bright blue bicycle parked beside a white wall, replace all red bicycle paint with blue paint." : "A red bicycle parked beside a white wall, photograph, daylight");
                if (sana)
                    Set(window, "_imagePath", Path.Combine(artifacts, "sd15-red-bicycle.png"));
                Set(window, "_ttsSteps", 5);
                Set(window, "_imageSteps", 20);
                Set(window, "_generationSeed", 42);
                window.position = new Rect(60, 60, 900, 850);
                window.Show();
                for (int i = 0; i < 3; ++i)
                    yield return null;
                if (sana)
                {
                    string llm = Path.Combine(model.Directory, "llm/llm.mnn");
                    LogAssert.Expect(LogType.Log, new Regex(@"\[MNN\] Loaded model from buffer"));
                    LogAssert.Expect(LogType.Log, new Regex(@"\[MNN\] Created session: "));
                    foreach (string stage in new[]{"connector", "projector", "transformer", "vae_encoder", "vae_decoder"})
                    {
                        LogAssert.Expect(LogType.Log, "[MNN] Loaded model from: " + Path.Combine(model.Directory, stage + ".mnn"));
                        LogAssert.Expect(LogType.Log, new Regex(@"\[MNN\] Created session: "));
                    }
                }
                else if (model.IsBertVits2)
                {
                    LogAssert.Expect(LogType.Log, "[MNN] Loaded model from: " + Path.Combine(model.Directory, "common/mnn_models/chinese_bert.mnn"));
                    LogAssert.Expect(LogType.Log, new Regex(@"\[MNN\] Created session: "));
                }
                else if (!model.IsPiper)
                    foreach (string stage in tts ? new[]{"duration_predictor", "text_encoder", "vector_estimator", "vocoder"} : new[]{"text_encoder", "unet", "vae_decoder"})
                    {
                        string path = Path.Combine(model.Directory, tts ? "mnn_models/fp16" : "general", stage + ".mnn");
                        LogAssert.Expect(LogType.Log, "[MNN] Loaded model from: " + path);
                        LogAssert.Expect(LogType.Log, new Regex(@"\[MNN\] Created session: "));
                    }

                Invoke(window, "RunTask");
                var session = (MNNStudioSession)Get(window, "_session");
                Assert.NotNull(session, (string)Get(window, "_error"));
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (session.Busy && timer.Elapsed.TotalSeconds < 360)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.False(session.Busy, "Generation timed out.");
                Assert.IsNull(session.Error);
                var result = (MNNStudioResult)Get(window, "_retrieval");
                Assert.NotNull(result, (string)Get(window, "_error"));
                for (int i = 0; i < 8; ++i)
                {
                    window.Repaint();
                    yield return null;
                }

                if (tts)
                {
                    Assert.AreEqual(model.IsPiper ? 16000 : 44100, result.SampleRate);
                    Assert.Greater(result.Waveform.Length, result.SampleRate);
                    var audio = (MNNStudioAudio)Get(window, "_audio");
                    for (int i = 0; i < 120 && audio.IsLoading; ++i)
                        yield return null;
                    Assert.IsNull(audio.ConsumePlaybackError());
                    Assert.True(audio.IsPlaying, "Actual generated speech must play in the Editor.");
                    File.WriteAllBytes(Path.Combine(artifacts, "studio-" + (repository ?? "supertonic") + ".wav"), MNNStudioAudio.EncodeWave(result.Waveform, result.SampleRate));
                    audio.StopPreview();
                    Assert.False(audio.IsPlaying);
                }
                else
                {
                    Assert.NotNull(result.GeneratedImage);
                    var preview = (Texture2D)Get(window, "_generatedPreview");
                    Assert.NotNull(preview, "Image result must render in the Studio view.");
                    Assert.AreEqual(512, preview.width);
                    Assert.AreEqual(512, preview.height);
                    File.WriteAllBytes(Path.Combine(artifacts, sana ? "studio-sana.png" : "studio-sd15.png"), preview.EncodeToPNG());
                    Invoke(window, "ResetWorkspace");
                    Assert.True(preview == null, "Reset must destroy the generated texture.");
                }

                Assert.IsEmpty((List<MNNStudioMessage>)Get(window, "_messages"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                EditorUtility.audioMasterMute = muted;
            }
        }
    }
}
