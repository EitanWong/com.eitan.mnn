using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class StudioTaskInferenceTests
    {
        [UnityTest]
        public IEnumerator ImageWorkspaceRunsSelectedVisionModel() => Run(MNNStudioTask.Vision, "SmolVLM-256M-Instruct-MNN", "What is the main color in this image? Answer in one word.", "red.png", null, "red");
        [UnityTest]
        public IEnumerator TranscriptionWorkspaceRunsSelectedAudioModel() => Run(MNNStudioTask.SpeechRecognition, "LFM2.5-Audio-1.5B-MNN", "Transcribe the spoken sentence in English.", null, "speech.wav", "paris");
        [UnityTest]
        public IEnumerator OmniWorkspaceRunsImageAndAudioTogether() => Run(MNNStudioTask.Omni, "Qwen2.5-Omni-3B-MNN", "Name the color of the image, then name the city mentioned in the audio.", "blue.png", "speech.wav", "blue", "paris");
        private static IEnumerator Run(MNNStudioTask task, string modelName, string prompt, string image, string audio, params string[] expected)
        {
            var model = MNNStudioModel.Read(MultimodalTestData.Model(modelName));
            string imagePath = image == null ? null : MultimodalTestData.Fixture(image);
            string audioPath = audio == null ? null : MultimodalTestData.Fixture(audio);
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            try
            {
                Set(window, "_autoTitle", false);
                Set(window, "_taskIndex", (int)task);
                Set(window, "_available", new List<MNNStudioModel>{model});
                Set(window, "_modelIndex", 0);
                Set(window, "_query", prompt);
                Set(window, "_imagePath", imagePath);
                Set(window, "_audioPath", audioPath);
                Set(window, "_tokenLimit", 64);
                Invoke(window, "RunTask");
                var session = (MNNStudioSession)Get(window, "_session");
                Assert.IsNotNull(session, (string)Get(window, "_error"));
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (session.Busy && timer.Elapsed.TotalSeconds < 180)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }

                Assert.False(session.Busy);
                Assert.IsNull(session.Error);
                var result = (MNNStudioResult)Get(window, "_retrieval");
                Assert.IsNotNull(result, (string)Get(window, "_error"));
                Assert.Greater(result.Tokens, 0);
                foreach (string value in expected)
                    StringAssert.Contains(value, result.Text.ToLowerInvariant());
                Assert.IsEmpty((List<MNNStudioMessage>)Get(window, "_messages"));
                TestContext.WriteLine(task + ": " + result.Text);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Set(MNNChatStudio window, string name, object value) => Field(name).SetValue(window, value);
        private static object Get(MNNChatStudio window, string name) => Field(name).GetValue(window);
        private static void Invoke(MNNChatStudio window, string method) => typeof(MNNChatStudio).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
    }
}
