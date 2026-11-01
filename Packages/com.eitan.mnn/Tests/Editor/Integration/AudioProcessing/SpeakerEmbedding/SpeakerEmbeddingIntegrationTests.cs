using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the pinned speaker graph and independent Kaldi reference fixtures; never downloads.")]
    public class SpeakerEmbeddingIntegrationTests
    {
        private static float[] Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        [Test]
        public void ActualSpeechMatchesIndependentKaldiAndMnnReference()
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string fixtures = Environment.GetEnvironmentVariable("MNN_TEST_FIXTURE_ROOT");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            string directory = Path.Combine(models, "ERes2NetV2-MNN");
            MNNSpeakerModel.Prepare(directory);
            var timer = Stopwatch.StartNew();
            using (var model = MNNSpeakerEmbedding.Load(directory, backendType: MNNBackendType.CPU))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                var vectors = new float[2][];
                for (int n = 3; n <= 4; ++n)
                {
                    float[] pcm = Read(Path.Combine(fixtures, "speaker-" + n + ".pcm"));
                    var reference = Read(Path.Combine(fixtures, "speaker-" + n + ".features"));
                    var features = MNNKaldiFeatures.ExtractSpeaker(pcm, out int frames, default);
                    Assert.AreEqual(reference.Length, frames * 80);
                    double rmse = Math.Sqrt(features.Zip(reference, (a, b) => (double)(a - b) * (a - b)).Average());
                    Assert.Less(rmse, .001, "The independent kaldi-native-fbank frontend must match.");
                    long before = Process.GetCurrentProcess().WorkingSet64;
                    timer.Restart();
                    var vector = model.Encode(pcm);
                    double elapsed = timer.Elapsed.TotalMilliseconds;
                    timer.Restart();
                    var repeat = model.Encode(pcm);
                    double warm = timer.Elapsed.TotalMilliseconds;
                    Assert.AreEqual(192, vector.Length);
                    Assert.That(vector.Sum(x => x * x), Is.EqualTo(1).Within(1e-5));
                    Assert.That(MNNSpeakerEmbedding.Similarity(vector, Read(Path.Combine(fixtures, "speaker-" + n + ".vector"))), Is.GreaterThan(.9999));
                    Assert.That(MNNSpeakerEmbedding.Similarity(vector, repeat), Is.GreaterThan(.99999));
                    vectors[n - 3] = vector;
                    File.WriteAllText(Path.Combine(artifacts, "speaker-" + n + ".json"), string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{{\"load_ms\":{0},\"first_ms\":{1},\"repeat_ms\":{2},\"rss_before\":{3},\"rss_after\":{4},\"feature_rmse\":{5}}}",
                        load, elapsed, warm, before, Process.GetCurrentProcess().WorkingSet64, rmse));
                }
                Assert.Less(MNNSpeakerEmbedding.Similarity(vectors[0], vectors[1]), .8, "Different published speakers should not collapse to the same vector.");
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Encode(new float[16000], cancellationToken: cancelled.Token));
                }
                Assert.Throws<ArgumentException>(() => model.Encode(new[] {float.NaN}));
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Encode(new float[16000]));
            }
            var studio = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.SpeakerRecognition, studio.Task);
            Assert.True(MNNStudioTasks.Get((int)studio.Task).CanRun(studio));
            using (var backend = MNNStudioNativeBackend.Load(studio, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest {Audio = Path.Combine(models, "Jeryuk--kws-zipformer-wenetspeech-3.3M-MNN/test_wavs/3.wav")});
                StringAssert.Contains("192 dimensions", result.Text);
            }
        }
    }
}
