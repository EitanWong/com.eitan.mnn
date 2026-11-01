using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class StableDiffusionIntegrationTests
    {
        [Test]
        public void RealImageRepeatedCallsAndDisposal()
        {
            string directory = GenerationTestData.Model("stable-diffusion-v1-5-mnn");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/GenerationOutputs");
            Directory.CreateDirectory(artifacts);
            const string prompt = "A red bicycle parked beside a white wall, photograph, daylight";
            var model = MNNStableDiffusion.Load(directory);
            try
            {
                var image = model.Generate(prompt, steps: 20);
                Assert.AreEqual(512, image.Width);
                Assert.AreEqual(512, image.Height);
                Assert.AreEqual(512 * 512 * 3, image.Rgb.Length);
                Assert.Greater(image.Rgb.Distinct().Count(), 200);
                double mean = image.Rgb.Average(b => (double)b);
                Assert.That(mean, Is.InRange(10, 245));
                Assert.Greater(image.Rgb.Average(b => (b - mean) * (b - mean)), 100);
                var texture = MNNStudioImage.CreateTexture(image);
                try
                {
                    File.WriteAllBytes(Path.Combine(artifacts, "sd15-red-bicycle.png"), texture.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                var repeat = model.Generate(prompt, steps: 20);
                Assert.AreNotSame(image.Rgb, repeat.Rgb);
                Assert.LessOrEqual(image.Rgb.Zip(repeat.Rgb, (a, b) => Math.Abs(a - b)).Max(), 1);
                Assert.Throws<OperationCanceledException>(() => model.Generate(prompt, cancellationToken: new CancellationToken(true)));
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => model.Generate(prompt));
            using (var reload = MNNStableDiffusion.Load(directory))
                Assert.AreEqual(512 * 512 * 3, reload.Generate("A yellow flower", steps: 2).Rgb.Length);
        }
    }
}
