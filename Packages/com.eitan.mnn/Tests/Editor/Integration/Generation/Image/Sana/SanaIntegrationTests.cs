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
    public class SanaIntegrationTests
    {
        [Test]
        public void RealImageEditing()
        {
            string directory = GenerationTestData.Model("MNN-Sana-Edit-V2");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            Assert.That(artifacts, Is.Not.Null.And.Not.Empty);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            byte[] reference = new byte[512 * 512 * 3];
            try
            {
                Assert.True(texture.LoadImage(File.ReadAllBytes(Path.Combine(artifacts, "sd15-red-bicycle.png"))));
                Assert.AreEqual(512, texture.width);
                Assert.AreEqual(512, texture.height);
                var pixels = texture.GetPixels32();
                for (int y = 0; y < 512; ++y)
                    for (int x = 0; x < 512; ++x)
                    {
                        var c = pixels[(511 - y) * 512 + x];
                        int i = (y * 512 + x) * 3;
                        reference[i] = c.r;
                        reference[i + 1] = c.g;
                        reference[i + 2] = c.b;
                    }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            var model = MNNSana.Load(directory);
            try
            {
                var result = model.Edit("Change the red bicycle to blue, keep the wall and scene unchanged.", reference);
                Assert.AreEqual(512, result.Width);
                Assert.AreEqual(512, result.Height);
                Assert.Greater(result.Rgb.Distinct().Count(), 200);
                Assert.Greater(result.Rgb.Zip(reference, (a, b) => Math.Abs(a - b)).Average(), 5);
                var output = MNNStudioImage.CreateTexture(result);
                try
                {
                    File.WriteAllBytes(Path.Combine(artifacts, "sana-blue-bicycle.png"), output.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }

                var repeat = model.Edit("Change the red bicycle to blue, keep the wall and scene unchanged.", reference);
                Assert.AreNotSame(result.Rgb, repeat.Rgb);
                Assert.LessOrEqual(result.Rgb.Zip(repeat.Rgb, (a, b) => Math.Abs(a - b)).Max(), 1);
                var opposite = model.Edit("A yellow bicycle parked beside a white wall, replace all red bicycle paint with yellow paint.", reference);
                double textEffect = result.Rgb.Zip(opposite.Rgb, (a, b) => Math.Abs(a - b)).Average();
                File.WriteAllText(Path.Combine(artifacts, "sana-conditioning.txt"), "Mean pixel difference between different prompts, same reference and seed: " + textEffect);
                Assert.Greater(textEffect, .1, "Changing the edit instruction must influence the model output.");
                Assert.Throws<OperationCanceledException>(() => model.Edit("Make it blue", reference, cancellationToken: new CancellationToken(true)));
            }
            finally
            {
                model.Dispose();
                model.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => model.Edit("Make it blue", reference));
            using (var reload = MNNSana.Load(directory))
                Assert.AreEqual(reference.Length, reload.Edit("Make it blue", reference, steps: 2).Rgb.Length);
        }
    }
}
