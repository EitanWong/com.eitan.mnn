using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNVbxModel
    {
        internal const string Name = "VBx-ResNet101-16K-MNN", Repository = "BUTSpeechFIT/VBx", Graph = "model_fp32.mnn";
        internal static bool IsComplete(string directory) => ReadVerified(directory) != null;
        internal static MNNVbxConfig ReadVerified(string directory)
        {
            try
            {
                var c = MNNVbxConfig.Read(directory);
                return c.repository == Repository && c.revision == "57466e6e245d5cdfe2e88ee6503702ace3ffdd03" &&
                    c.checkpoint_sha256 == "f550bf3d7766478a0684b8f564184db81c3346a7c99a61051f033b42b8f08373" &&
                    c.graph == Graph && c.execution == "whole_utterance_raw_embedding" && c.input == "x" && c.output == "982" &&
                    c.sample_rate == 16000 && c.minimum_samples == 1520 && c.maximum_samples == 160000 &&
                    c.fft_size == 512 && c.frame_size == 400 && c.frame_shift == 160 && c.mel_bins == 64 &&
                    c.embedding_dimension == 256 && c.cmn_left == 150 && c.cmn_right == 149 && c.dither_seed == 3 && c.dither_level == 8 &&
                    c.coefficient_sha256 == "cdbe527fdedfa3f169ef3656d3a05f0940f033ab90207dcf4e873b08497c99c4" &&
                    Matches(directory, MNNVbxConfig.CoefficientsFile, 134784, c.coefficient_sha256) &&
                    Matches(directory, Graph, 59294480, "d7042c7d4c98b4f8bd77adc964c8603fe292a414f953f15207bcaafec38f05a1") ? c : null;
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
