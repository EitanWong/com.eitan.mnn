using System;
using System.IO;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RegisteredTurnStudioTests
    {
        [Test]
        public void TranscriptRequestUsesItsLanguageThresholdWithoutRequiringAudio()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT");
            var model = MNNStudioModel.Read(Path.Combine(root, "LiveKit-Turn-Detector-Multilingual-MNN"));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                Assert.AreEqual("Turn incomplete.", backend.Run(new MNNStudioRequest { Prompt = "hold on let me finish", TurnLanguage = "en" }).Text);
                Assert.AreEqual("Turn complete.", backend.Run(new MNNStudioRequest { Prompt = "hold on let me finish", TurnLanguage = "zh" }).Text);
            }
        }

        [TestCase("Bairong-TurnSense-1.0-MNN")]
        [TestCase("Bairong-TurnSense-1.1-MNN")]
        public void SharedMarkerDistinguishesVersionsWithoutUsingFolderNames(string name)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT");
            string acceptance = Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
            string directory = Path.Combine(acceptance, "renamed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string marker = "mnn_bairong_turnsense.json";
                File.Copy(Path.Combine(root, name, marker), Path.Combine(directory, marker));
                Assert.AreEqual("EitanWang/" + name, MNNModelRegistry.FindTurn(directory).Repository);
                Assert.False(MNNModelRegistry.FindTurn(directory).IsComplete(directory));
                File.WriteAllText(Path.Combine(directory, marker), "{}");
                Assert.IsNull(MNNModelRegistry.FindTurn(directory));
            }
            finally { Directory.Delete(directory, true); }
        }

        [TestCase("Bairong-TurnSense-1.0-MNN", false)]
        [TestCase("Bairong-TurnSense-1.1-MNN", false)]
        [TestCase("Easy-Turn-MNN", false)]
        [TestCase("LiveKit-Turn-Detector-Multilingual-MNN", true)]
        [TestCase("LiveKit-Turn-Detector-MNN", true)]
        public void SharedRegistrationDiscoversAndExecutesTurnPipeline(string name, bool transcript)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT");
            var model = MNNStudioModel.Read(Path.Combine(root, name));
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.AreEqual(MNNStudioTask.TurnDetection, model.AudioSubtask);
            Assert.AreEqual(transcript, model.RegisteredTurnRequiresTranscript);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.TurnDetection).CanRun(model));
            string acceptance = Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
            byte[] bytes = File.ReadAllBytes(Path.Combine(acceptance, "Bairong", "fixtures-1.0", "complete_real_1-input.f32"));
            float[] pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
            string wave = Path.Combine(acceptance, "registered-turn-studio.wav");
            File.WriteAllBytes(wave, MNNWaveAudio.EncodePcm16(pcm, 16000));
            pcm = MNNWaveAudio.Read(wave, out _);
            bool expected;
            using (var direct = MNNTurnDetector.Load(model.Directory))
                expected = direct.IsComplete(pcm, 16000, "That is all, thank you.");
            using (var backend = MNNStudioNativeBackend.Load(model, 2, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = transcript ? null : wave, Prompt = "That is all, thank you." });
                Assert.AreEqual(expected ? "Turn complete." : "Turn incomplete.", result.Text);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                TestContext.WriteLine(name + ": Studio=" + result.Text);
            }
        }
    }
}
