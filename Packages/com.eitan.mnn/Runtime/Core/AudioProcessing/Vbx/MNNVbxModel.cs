using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNVbxModel
    {
        internal const string Repository = "BUTSpeechFIT/VBx", Graph = "model_fp32.mnn";
        internal static bool IsComplete(string directory) => ReadVerified(directory) != null;
        internal static MNNVbxConfig ReadVerified(string directory)
        {
            try
            {
                var c = MNNVbxConfig.Read(directory);
                var profile = MNNVbxProfile.Find(c.checkpoint_sha256);
                return c.repository == Repository && c.revision == "57466e6e245d5cdfe2e88ee6503702ace3ffdd03" &&
                    profile != null &&
                    c.graph == Graph && c.execution == "whole_utterance_raw_embedding" && c.input == "x" && c.output == "982" &&
                    c.sample_rate == profile.SampleRate && c.minimum_samples == profile.MinimumSamples && c.maximum_samples == profile.MaximumSamples &&
                    c.fft_size == profile.FftSize && c.frame_size == profile.FrameSize && c.frame_shift == profile.FrameShift && c.mel_bins == 64 &&
                    c.embedding_dimension == 256 && c.cmn_left == 150 && c.cmn_right == 149 && c.dither_seed == 3 && c.dither_level == 8 &&
                    c.coefficient_sha256 == profile.CoefficientSha256 &&
                    Matches(directory, MNNVbxConfig.CoefficientsFile, profile.CoefficientBytes, profile.CoefficientSha256) &&
                    Matches(directory, Graph, 59294480, profile.GraphSha256) ? c : null;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return null; }
        }
        private static bool Matches(string directory, string name, long bytes, string expected)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, name)))
            using (var hash = SHA256.Create())
                return stream.Length == bytes && BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == expected;
        }
    }
}
