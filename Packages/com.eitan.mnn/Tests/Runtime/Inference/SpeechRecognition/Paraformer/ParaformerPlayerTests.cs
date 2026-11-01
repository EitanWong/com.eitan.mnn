using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("ParaformerPlayer")]
    public class ParaformerPlayerTests
    {
        [Test]
        public void ChineseReferenceCancellationAndStateResetInActualPlayer()
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_PARAFORMER_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --paraformer-root with existing prepared weights and the independent Chinese WAV.");
            var recognizer = MNNParaformerRecognizer.Load(Path.Combine(root, "Models/KindBrave-paraformer-online"));
            var pcm = MNNWaveAudio.Read(Path.Combine(root, "Inspection/Reference/example--asr_example.wav"), out int rate);
            try
            {
                var first = recognizer.Recognize(pcm, rate);
                Assert.AreEqual("欢迎大家来体验达摩院推出的语音识别模型", first.Text);
                Assert.AreEqual(MNNBackendType.CPU, recognizer.Backend);
                Assert.AreEqual(MNNBackendType.CPU, recognizer.DecoderBackend);
                Assert.IsEmpty(first.Timestamps);
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<OperationCanceledException>(() => recognizer.Recognize(pcm, rate, new CancellationToken(true)));
                var next = Task.Run(() => recognizer.RecognizeAsync(pcm, rate)).GetAwaiter().GetResult();
                Assert.AreEqual(first.Text, next.Text);
                CollectionAssert.AreEqual(first.TokenIds, next.TokenIds);
            }
            finally { recognizer.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => recognizer.Recognize(pcm, rate));
        }
    }
}
