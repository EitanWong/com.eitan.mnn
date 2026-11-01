using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNCueLightModel
    {
        internal const string CpuGraph = "step_fp32.mnn", MetalGraph = "step_accurate_fp32.mnn";
        internal static bool IsComplete(string directory) => ReadVerified(directory) != null;
        internal static MNNCueLightProfile ReadVerified(string directory)
        {
            try
            {
                var c = MNNCueLightConfig.Read(directory);
                var p = MNNCueLightProfile.Find(c.repository);
                return p != null && c.revision == p.Revision && c.checkpoint_sha256 == p.Checkpoint &&
                    c.cpu_graph == CpuGraph && c.metal_graph == (p.SupportsMetal ? MetalGraph : "") && c.sample_rate == 16000 &&
                    c.frame_samples == 320 && c.maximum_samples == 960000 && c.fft_size == 512 &&
                    c.window_samples == 400 && c.hop_samples == 160 && c.mel_bins == 64 &&
                    c.hidden_dimension == p.HiddenDimension && c.blocks == 3 && c.outputs == 6 &&
                    c.reset_silence_frames == 100 && c.reset_onset_frames == 3 && c.reset_replay_frames == 18 &&
                    c.coefficient_sha256 == "39491c322c7c3bf1b33f9231a6b0fae95f159b4315d021dd99e79dedfbc600d0" &&
                    Matches(directory, MNNCueLightConfig.CoefficientsFile, 67392, c.coefficient_sha256) &&
                    Matches(directory, CpuGraph, p.CpuBytes, p.CpuHash) &&
                    (!p.SupportsMetal || Matches(directory, MetalGraph, p.MetalBytes, p.MetalHash)) ? p : null;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return null; }
        }
        private static bool Matches(string directory, string name, long size, string expected)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, name)))
            using (var hash = SHA256.Create())
                return stream.Length == size && BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == expected;
        }
    }
}
