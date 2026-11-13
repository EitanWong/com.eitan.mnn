using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("MiniMindO"), Explicit("Requires the locally converted MiniMind-o installation; never downloads weights.")]
    public sealed class MiniMindOIntegrationTests
    {
        private static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "StreamingAssets/MNN/Models/MiniMindO-113M-MNN"));
        private static string Artifacts => Path.GetFullPath(Path.Combine(Application.dataPath, "../TestArtifacts~/MiniMindO-20261113/Integration/Unity"));

        [Test]
        public void TokenizerMatchesReleasedHuggingFaceIds()
        {
            var tokenizer = new MNNMiniMindOTokenizer(Path.Combine(DirectoryPath, "tokenizer.json"));
            var fixture = MNNModelData.Read(Path.Combine(Application.dataPath,
                "../Packages/com.eitan.mnn/Tests/Fixtures/MiniMindO/tokenization.json"));
            foreach (var row in MNNModelData.Array(fixture))
            {
                string text = (string)MNNModelData.Get(row, "text");
                CollectionAssert.AreEqual(MNNModelData.Shape(MNNModelData.Get(row, "ids")), tokenizer.Encode(text), text);
                Assert.AreEqual((string)MNNModelData.Get(row, "decoded"), tokenizer.Decode(tokenizer.Encode(text)));
            }
        }

        [Test]
        public void InstalledModelStreamsSpeechCancelsRecoversAndDisposes()
        {
            Assert.True(MNNMiniMindO.IsComplete(DirectoryPath));
            var model = MNNMiniMindO.Load(DirectoryPath);
            try
            {
                int samples = 0, frames = 0;
                double firstAudio = -1;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var result = model.Generate("你好，请用一句话介绍你自己。", 128, 42, update =>
                {
                    if (firstAudio < 0 && update.Waveform.Length > 0) firstAudio = timer.Elapsed.TotalSeconds;
                    samples += update.Waveform.Length;
                    if (update.AudioFrame != null) ++frames;
                });
                Assert.IsNotEmpty(result.Text);
                Assert.False(result.TokenLimitReached, "The default generation budget must complete this reference turn.");
                Assert.That(result.AudioCodes.Length, Is.GreaterThan(0));
                Assert.AreEqual(result.AudioCodes.Length, frames);
                Assert.AreEqual(result.AudioCodes.Length * 1920, result.Waveform.Length);
                Assert.AreEqual(result.Waveform.Length, samples);
                Assert.True(result.AudioCodes.All(f => f.Length == 8 && f.All(c => c >= 0 && c < 2048)));
                Assert.That(result.Waveform.Sum(x => (double)x * x) / result.Waveform.Length, Is.GreaterThan(1e-8));
                System.IO.Directory.CreateDirectory(Artifacts);
                var normalized = result.Waveform.Select(x => Math.Max(-1, Math.Min(1, x))).ToArray();
                File.WriteAllBytes(Path.Combine(Artifacts, "minimind-o-reply.wav"), MNNWaveAudio.EncodePcm16(normalized, result.SampleRate));
                File.WriteAllText(Path.Combine(Artifacts, "reply-codes.json"), "[" + string.Join(",", result.AudioCodes.Select(f => "[" + string.Join(",", f) + "]")) + "]");
                var pcmBytes = new byte[result.Waveform.Length * sizeof(float)];
                Buffer.BlockCopy(result.Waveform, 0, pcmBytes, 0, pcmBytes.Length);
                File.WriteAllBytes(Path.Combine(Artifacts, "reply.f32"), pcmBytes);
                TestContext.WriteLine("TEXT=" + result.Text + "\nFRAMES=" + frames + "\nPCM_SAMPLES=" + samples + "\nFIRST_AUDIO_SECONDS=" + firstAudio + "\nSECONDS=" + timer.Elapsed.TotalSeconds);
                using (var stop = new CancellationTokenSource())
                    Assert.Throws<OperationCanceledException>(() => model.Generate("请介绍你自己。", 48, 42, _ => stop.Cancel(), stop.Token));
                var recovered = model.Generate(new MNNMiniMindORequest { Text = "你好" },
                    new MNNMiniMindOOptions { MaxNewTokens = 8, Temperature = 0, GenerateSpeech = false });
                Assert.IsNotEmpty(recovered.Text);
                Assert.AreEqual(0, recovered.Waveform.Length);
                Assert.Throws<ArgumentException>(() => model.Generate(new MNNMiniMindORequest { Text = "你好", ImagePixels = new float[1] }));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Generate("你好"));
            model.Dispose();
        }

        [Test]
        public void InstalledModelAcceptsImageAndAudioInOneTurn()
        {
            var rgb = new byte[37 * 53 * 3];
            for (int i = 0; i < rgb.Length; i += 3) rgb[i] = 255;
            var image = MNNMiniMindO.PrepareImage(new MNNRgbImage(rgb, 37, 53));
            // Deterministic PCM checks the acoustic path; this is not a speech comprehension score.
            var audio = Enumerable.Range(0, 16000).Select(i => .1f * (float)Math.Sin(2 * Math.PI * 440 * i / 16000)).ToArray();
            using (var model = MNNMiniMindO.Load(DirectoryPath))
            {
                var result = model.Generate(new MNNMiniMindORequest { Text = "图片的主要颜色是什么？", ImagePixels = image, Audio16Khz = audio },
                    new MNNMiniMindOOptions { MaxNewTokens = 24, Temperature = 0, GenerateSpeech = false });
                Assert.IsNotEmpty(result.Text);
                TestContext.WriteLine("AUDIO_IMAGE_TEXT=" + result.Text);
            }
        }

        [Test]
        public void InstalledModelAcceptsActualGeneratedSpeech()
        {
            using (var model = MNNMiniMindO.Load(DirectoryPath))
            {
                var spoken = model.Generate("请说：你好。", 128);
                Assert.That(spoken.Waveform.Length, Is.GreaterThan(2400));
                var normalized = spoken.Waveform.Select(x => Math.Max(-1, Math.Min(1, x))).ToArray();
                var audio = MNNAudioResampling.Resample(normalized, 24000, 16000);
                var result = model.Generate(new MNNMiniMindORequest { Text = "请转录这段语音。", Audio16Khz = audio },
                    new MNNMiniMindOOptions { MaxNewTokens = 64, Temperature = 0, GenerateSpeech = false });
                Assert.IsNotEmpty(result.Text);
                TestContext.WriteLine("SPOKEN_TEXT=" + spoken.Text + "\nAUDIO_INPUT_REPLY=" + result.Text);
            }
        }

        [Test]
        public void StudioDiscoversAndRunsTheDedicatedMiniMindOAdapter()
        {
            var model = MNNStudioModel.Read(DirectoryPath);
            Assert.True(model.IsMiniMindO);
            Assert.True(model.HasMiniMindOPipeline);
            Assert.True(model.Supports(MNNModelCapabilities.Vision | MNNModelCapabilities.AudioInput | MNNModelCapabilities.SpeechOutput));
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.Omni).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest { Prompt = "你好", TokenLimit = 16,
                    Conversation = new[] { new MNNChatMessage(MNNChatRole.User, "你好") } });
                Assert.IsNotEmpty(result.Text);
                StringAssert.Contains("MiniMind-o", result.BackendDescription);
                TestContext.WriteLine("STUDIO_TEXT=" + result.Text);
            }
        }
    }
}
