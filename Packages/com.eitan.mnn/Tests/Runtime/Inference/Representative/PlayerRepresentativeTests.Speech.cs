using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public partial class PlayerRepresentativeTests
    {
        [UnityTest, Category("MobileRealSpeech")]
        public IEnumerator Supertonic_Int8AutoMatchesCpuAcrossInputShapes()
        {
#if !(UNITY_ANDROID && !UNITY_EDITOR && ENABLE_IL2CPP)
            Assert.Ignore("This regression requires the Android ARM64 IL2CPP Player.");
#endif
            string root = MobileModelRoot();
            string directory = Path.Combine(root, "models", "Supertonic-3-INT8-MNN");
            if (!Directory.Exists(directory)) Assert.Ignore("Stage the existing Supertonic INT8 weights in mobile-llm/models.");
            var task = Task.Run(() =>
            {
                var report = new List<string>();
                using (var reference = MNNSupertonic.Load(directory, threads: 4, precision: "int8", backendType: MNNBackendType.CPU))
                using (var automatic = MNNSupertonic.Load(directory, threads: 4, precision: "int8"))
                {
                    if (MNNInferenceCache.IsAdreno740(MNNInferenceCache.GraphicsDevice))
                        foreach (var stage in automatic.StageBackends)
                            Assert.AreEqual(MNNBackendType.CPU, stage.Value, stage.Key);
                    string[] phrases = { "Hello.", "The capital of France is Paris.", "Two plus two equals four." };
                    foreach (string phrase in phrases)
                    {
                        var expected = reference.Synthesize(phrase, steps: 4, seed: 42);
                        for (int repeat = 0; repeat < 2; ++repeat)
                        {
                            var clock = Stopwatch.StartNew();
                            var actual = automatic.Synthesize(phrase, steps: 4, seed: 42);
                            double seconds = clock.Elapsed.TotalSeconds;
                            Assert.AreEqual(44100, actual.SampleRate);
                            Assert.AreEqual(expected.Waveform.Length, actual.Waveform.Length);
                            Assert.Greater(actual.Waveform.Length, 4410);
                            double energy = 0, error = 0;
                            bool finite = true;
                            for (int i = 0; i < actual.Waveform.Length; ++i)
                            {
                                float value = actual.Waveform[i];
                                finite &= !float.IsNaN(value) && !float.IsInfinity(value);
                                energy += value * value;
                                error = Math.Max(error, Math.Abs(value - expected.Waveform[i]));
                            }
                            Assert.True(finite);
                            Assert.Greater(energy / actual.Waveform.Length, 1e-7);
                            Assert.Less(error, 1e-5, "Auto must preserve the CPU reference waveform.");
                            double audioSeconds = actual.Waveform.Length / 44100d;
                            report.Add($"phrase={phrase}; repeat={repeat}; synthesis_s={seconds:F3}; audio_s={audioSeconds:F3}; rtf={seconds / audioSeconds:F3}; max_error={error}");
                            if (repeat == 0) File.WriteAllBytes(Path.Combine(root, "supertonic-" + Array.IndexOf(phrases, phrase) + ".wav"), MNNWaveAudio.EncodePcm16(actual.Waveform, actual.SampleRate));
                        }
                    }
                    report.Add("stages=" + string.Join(",", automatic.StageBackends.Select(stage => stage.Key + ":" + stage.Value)));
                }
                File.WriteAllLines(Path.Combine(root, "supertonic-results.txt"), report);
                foreach (string line in report) TestContext.WriteLine(line);
            });
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception;
        }
    }
}
