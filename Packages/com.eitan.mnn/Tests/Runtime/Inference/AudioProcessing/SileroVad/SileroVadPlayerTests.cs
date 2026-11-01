using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("SileroVadPlayer")]
    public class SileroVadPlayerTests
    {
        [Test]
        public void VoiceIntervalsResetCancelAndMatchIndependentReferenceInActualPlayer()
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_SILERO_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --silero-root with prepared local weights and ONNX reference PCM; no downloads.");
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, "Reference/two-utterances-pcm.f32"));
            var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
            bytes = File.ReadAllBytes(Path.Combine(root, "Reference/two-utterances-probs.f32"));
            var reference = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, reference, 0, bytes.Length);
            var vad = MNNSileroVad.Load(Path.Combine(root, "Models"));
            try
            {
                var first = vad.Detect(pcm, 16000);
                Assert.AreEqual(MNNBackendType.CPU, vad.Backend);
                Assert.AreEqual(2, first.Segments.Count);
                Assert.AreEqual(6176 / 16000.0, first.Segments[0].StartSeconds);
                Assert.AreEqual(99808 / 16000.0, first.Segments[0].EndSeconds);
                Assert.AreEqual(128032 / 16000.0, first.Segments[1].StartSeconds);
                Assert.AreEqual(222176 / 16000.0, first.Segments[1].EndSeconds);
                Assert.Less(first.Probabilities.Select((p, i) => Math.Abs(p - reference[i])).Max(), .08);
                Assert.AreEqual(0, vad.Detect(new float[16000], 16000).Segments.Count);
                Assert.Throws<OperationCanceledException>(() => vad.Detect(pcm, 16000, cancellationToken: new CancellationToken(true)));
                var second = Task.Run(() => vad.DetectAsync(pcm, 16000)).GetAwaiter().GetResult();
                CollectionAssert.AreEqual(first.Probabilities, second.Probabilities);
                Assert.Throws<ArgumentException>(() => vad.Detect(new[] {float.NaN}, 16000));
            }
            finally { vad.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => vad.Detect(pcm, 16000));
        }

        [Test]
        public void ResampledVoiceIntervalsStayInsideOriginalDurationInActualPlayer()
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_SILERO_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --silero-root with prepared local weights and PCM.");
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, "Reference/en-pcm.f32"));
            var source = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, source, 0, bytes.Length);
            var pcm = MNNSpeechDsp.Resample(source, 16000, 44100, CancellationToken.None);
            using (var vad = MNNSileroVad.Load(Path.Combine(root, "Models")))
            {
                var result = vad.Detect(pcm, 44100);
                Assert.AreEqual(1, result.Segments.Count);
                Assert.That(result.Segments[0].StartSeconds, Is.EqualTo(.386).Within(.032));
                Assert.That(result.Segments[0].EndSeconds, Is.EqualTo(6.238).Within(.032));
                Assert.LessOrEqual(result.Segments[0].EndSeconds, result.AudioSeconds);
            }
            var clamped = new MNNVoiceActivityResult(new[] {new MNNSpeechSegment(0, 16000)}, new[] {.9f}, .99999);
            Assert.AreEqual(.99999, clamped.Segments[0].EndSeconds);
        }
    }
}
