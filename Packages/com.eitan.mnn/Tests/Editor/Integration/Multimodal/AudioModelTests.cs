using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class AudioModelTests
    {
        private MNNLlm _model;
        [OneTimeSetUp]
        public void Load() => _model = MNNLlm.Load(MultimodalTestData.Model("LFM2.5-Audio-1.5B-MNN"), cacheDirectory: MultimodalTestData.Cache("audio"));
        [OneTimeTearDown]
        public void Dispose()
        {
            _model?.Dispose();
            MultimodalTestData.CleanCache("audio");
        }

        [Test]
        public void AudioInput_TranscribesRecordedSentence()
        {
            Assert.That(_model.Capabilities.HasFlag(MNNModelCapabilities.AudioInput));
            var result = _model.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: MultimodalTestData.Fixture("speech.wav"), maxNewTokens: 64);
            MultimodalTestData.Report("LFM audio", result);
            StringAssert.Contains("paris", result.Text.ToLowerInvariant());
            Assert.Greater(result.AudioMicroseconds, 0);
            Assert.Greater(result.InputAudioSeconds, 1);
        }

        [UnityTest]
        public IEnumerator AsyncAudioInput_UsesWorkerThread()
        {
            var task = _model.GenerateMultimodalAsync("Transcribe the spoken sentence in English.", audioPath: MultimodalTestData.Fixture("speech.wav"), maxNewTokens: 64);
            while (!task.IsCompleted)
                yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            MultimodalTestData.Report("LFM async audio", task.Result);
            StringAssert.Contains("paris", task.Result.Text.ToLowerInvariant());
        }

        [Test]
        public void CorruptAudio_IsRejectedInsteadOfTextFallback()
        {
            string path = System.IO.Path.Combine(MultimodalTestData.Artifacts, "corrupt.wav");
            System.IO.File.WriteAllText(path, "This is not audio.");
            try
            {
                Assert.Throws<MNNException>(() => _model.GenerateMultimodal("Transcribe.", audioPath: path));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
