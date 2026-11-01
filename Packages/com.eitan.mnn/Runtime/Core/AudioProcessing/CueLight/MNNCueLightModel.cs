using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNCueLightModel
    {
        internal const string CpuGraph = "step_fp32.mnn", MetalGraph = "step_accurate_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var c = MNNCueLightConfig.Read(directory);
                return c.repository == "sshah03/cuelight-nano" && c.revision == "8bc69fe809cd9b6968e65f3bd00439724b5a7c67" &&
                    c.checkpoint_sha256 == "4d4ec1abb7d32d6268035117d0ab93116f145cdb55e510d3c71450ecd0adf841" &&
                    c.cpu_graph == CpuGraph && c.metal_graph == MetalGraph && c.sample_rate == 16000 &&
                    c.frame_samples == 320 && c.maximum_samples == 960000 && c.fft_size == 512 &&
                    c.window_samples == 400 && c.hop_samples == 160 && c.mel_bins == 64 &&
                    c.hidden_dimension == 112 && c.blocks == 3 && c.outputs == 6 &&
                    c.reset_silence_frames == 100 && c.reset_onset_frames == 3 && c.reset_replay_frames == 18 &&
                    c.coefficient_sha256 == "39491c322c7c3bf1b33f9231a6b0fae95f159b4315d021dd99e79dedfbc600d0" &&
                    Matches(directory, MNNCueLightConfig.CoefficientsFile, 67392, c.coefficient_sha256) &&
                    Matches(directory, CpuGraph, 1993228, "e8e7dc004c9decc48c25cc4b94b25c2c94d5b35461410bc78ca1619e7a0112d3") &&
                    Matches(directory, MetalGraph, 2000936, "d73504f638574627ea3fa3da2d7074948357219536c5401dc476a455a7be52a3");
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return false; }
        }
        private static bool Matches(string directory, string name, long size, string expected)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, name)))
            using (var hash = SHA256.Create())
                return stream.Length == size && BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == expected;
        }
    }
}
