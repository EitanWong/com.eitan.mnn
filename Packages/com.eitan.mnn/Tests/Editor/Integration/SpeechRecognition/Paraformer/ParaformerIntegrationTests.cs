using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [TestFixture, Category("Integration"), Category("Paraformer")]
    public class ParaformerIntegrationTests
    {
        private const string Expected = "欢迎大家来体验达摩院推出的语音识别模型";
        private static string Root
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("MNN_TEST_PARAFORMER_ROOT");
                if (string.IsNullOrEmpty(root)) Assert.Ignore("Set MNN_TEST_PARAFORMER_ROOT to the explicit local fixtures. No automatic downloads.");
                return root;
            }
        }
        private static string Model => Path.Combine(Root, "Models/KindBrave-paraformer-online");
        private static string Wave => Path.Combine(Root, "Inspection/Reference/example--asr_example.wav");

        [Test]
        public void ChineseReferenceResetsStateAndRejectsInvalidInputs()
        {
            MNNParaformerModel.Prepare(Model);
            var pcm = MNNWaveAudio.Read(Wave, out int rate);
            var recognizer = MNNParaformerRecognizer.Load(Model);
            try
            {
                Assert.AreEqual(MNNBackendType.CPU, recognizer.Backend);
                var first = recognizer.Recognize(pcm, rate);
                Assert.AreEqual(Expected, first.Text, "Independent FunASR test_paraformer_streaming.py reference.");
                Assert.AreEqual(0, first.Timestamps.Count, "No timestamp alignment is implemented.");
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(new[] {float.NaN}, rate));
                var invalid = new float[16000]; invalid[9000] = float.NaN;
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(invalid, 16000));
                invalid[9000] = 1.1f;
                Assert.Throws<ArgumentException>(() => recognizer.Recognize(invalid, 16000));
                Assert.Throws<OperationCanceledException>(() => recognizer.Recognize(pcm, rate, new CancellationToken(true)));
                using (var cancel = new CancellationTokenSource())
                {
                    cancel.CancelAfter(30);
                    Assert.Throws<OperationCanceledException>(() => recognizer.Recognize(pcm, rate, cancel.Token));
                }
                var second = recognizer.RecognizeWaveFile(Wave);
                Assert.AreEqual(Expected, second.Text);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
            }
            finally { recognizer.Dispose(); recognizer.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => recognizer.Recognize(pcm, rate));
        }

        [Test]
        public void OrdinaryTaskPoolSupportsLoadingAndOwnedAsyncInput()
        {
            // Studio uses Task.Run; this must work without a dedicated large-stack thread.
            Task.Run(() =>
            {
                using (var recognizer = MNNParaformerRecognizer.Load(Model))
                {
                    var pcm = MNNWaveAudio.Read(Wave, out int rate);
                    var pending = recognizer.RecognizeAsync(pcm, rate);
                    Array.Clear(pcm, 0, pcm.Length);
                    Assert.AreEqual(Expected, pending.GetAwaiter().GetResult().Text);
                }
            }).GetAwaiter().GetResult();
        }
    }
}
