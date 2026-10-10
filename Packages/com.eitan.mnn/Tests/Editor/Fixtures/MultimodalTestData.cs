using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    internal static class MultimodalTestData
    {
        internal static string Artifacts => Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/MNNValidation/Multimodal");
        internal static string Model(string name)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT") ?? Path.Combine(Application.streamingAssetsPath, "MNN", "Models");
            string directory = Path.Combine(root, name);
            if (!File.Exists(Path.Combine(directory, "config.json")))
            {
                if (Environment.GetEnvironmentVariable("MNN_REQUIRE_MODEL_TESTS") == "1")
                    Assert.Fail("Required local model is missing: " + directory);
                Assert.Ignore("Download representative model first: " + name);
            }

            return directory;
        }

        internal static string Fixture(string name)
        {
            string fixtureRoot = Environment.GetEnvironmentVariable("MNN_TEST_FIXTURE_ROOT") ?? Path.Combine(Artifacts, "Fixtures");
            string path = Path.Combine(fixtureRoot, name);
            if (!File.Exists(path))
            {
                if (Environment.GetEnvironmentVariable("MNN_REQUIRE_MODEL_TESTS") == "1")
                    Assert.Fail("Required local model-test fixture is missing: " + path);
                Assert.Ignore("Generate local fixture first: " + path);
            }

            return path;
        }

        internal static string Cache(string name) => Path.Combine(Artifacts, "Cache", name);
        internal static void CleanCache(string name)
        {
            string path = Cache(name);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        internal static void Report(string name, MNNMultimodalResult result)
        {
            string report = name + ": " + result.Text + "\nTokens=" + result.GeneratedTokens + ", vision_us=" + result.VisionMicroseconds + ", audio_us=" + result.AudioMicroseconds + ", input_audio_s=" + result.InputAudioSeconds + ", samples=" + result.Waveform.Length;
            TestContext.WriteLine(report);
            Directory.CreateDirectory(Artifacts);
            File.AppendAllText(Path.Combine(Artifacts, "inference-output.txt"), report + "\n");
            Assert.That(result.GeneratedTokens, Is.GreaterThan(0));
            Assert.IsNotEmpty(result.Text);
        }

        internal static void SaveWave(string name, MNNMultimodalResult result)
        {
            Assert.That(result.SampleRate, Is.EqualTo(24000));
            Assert.That(result.Waveform.Length, Is.GreaterThan(result.SampleRate / 10));
            double energy = 0;
            foreach (float sample in result.Waveform)
            {
                Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                energy += sample * sample;
            }

            Assert.Greater(energy / result.Waveform.Length, 1e-8, "Waveform must contain audible signal.");
            using (var writer = new BinaryWriter(File.Create(Path.Combine(Artifacts, name))))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + result.Waveform.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(result.SampleRate);
                writer.Write(result.SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(result.Waveform.Length * 2);
                foreach (float value in result.Waveform)
                    writer.Write((short)(Math.Max(-1f, Math.Min(1f, value)) * 32767));
            }
        }
    }
}
