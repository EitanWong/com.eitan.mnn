using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using NUnit.Framework;
using MNN.Unity.Editor;

namespace MNN.Unity.Tests
{
    [Explicit("Requires pinned MobileCLIP weights and independent tokenizer/reference vectors; never downloads.")]
    public class ImageEmbeddingIntegrationTests
    {
        private static float[] Read(string path)
        {
            var bytes = File.ReadAllBytes(path); var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        [Test]
        public void ActualImageAndTextEncodersMatchReferenceVectors()
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT"), fixtures = Environment.GetEnvironmentVariable("MNN_TEST_FIXTURE_ROOT"), artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            string dir = Path.Combine(models, "budaoshou--MobileCLIP-MNN");
            MNNImageEmbeddingModel.Prepare(dir);
            var image = MNNStudioImage.ReadRgb(Path.Combine(fixtures, "face.jpg"));
            var timer = Stopwatch.StartNew();
            using (var model = MNNImageEmbedding.Load(dir, backendType: MNNBackendType.CPU))
            {
                double load = timer.Elapsed.TotalMilliseconds; long rssBefore = Process.GetCurrentProcess().WorkingSet64; timer.Restart();
                var imageVector = model.EncodeImage(image);
                double imageMs = timer.Elapsed.TotalMilliseconds; timer.Restart();
                Assert.That(imageVector.Zip(Read(Path.Combine(fixtures, "clip-face.vector")), (a,b) => Math.Abs(a-b)).Max(), Is.LessThan(.002));
                var textVector = model.EncodeText("A photograph of a woman.");
                double textMs = timer.Elapsed.TotalMilliseconds;
                File.WriteAllText(Path.Combine(artifacts, "MobileCLIP.json"), string.Format(System.Globalization.CultureInfo.InvariantCulture, "{{\"load_ms\":{0},\"image_ms\":{1},\"text_ms\":{2},\"rss_before\":{3},\"rss_after\":{4}}}", load, imageMs, textMs, rssBefore, Process.GetCurrentProcess().WorkingSet64));
                Assert.That(textVector.Zip(Read(Path.Combine(fixtures, "clip-text-0.vector")), (a,b) => Math.Abs(a-b)).Max(), Is.LessThan(.002));
                Assert.That(MNNImageEmbedding.Similarity(imageVector, textVector), Is.GreaterThan(.1));
                Assert.That(MNNImageEmbedding.Similarity(imageVector, model.EncodeText("A photograph of a dog.")), Is.Not.EqualTo(MNNImageEmbedding.Similarity(imageVector, textVector)).Within(1e-5));
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.EncodeText("x"));
            }
            var studio = MNNStudioModel.Read(dir);
            Assert.AreEqual(MNNStudioTask.ImageEmbedding, studio.Task);
            Assert.True(MNNStudioTasks.Get((int)studio.Task).CanRun(studio));
        }
    }
}
