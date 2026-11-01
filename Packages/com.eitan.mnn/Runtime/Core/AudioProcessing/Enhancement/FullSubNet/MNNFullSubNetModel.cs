using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNFullSubNetModel
    {
        internal const string Graph = "fullsubnet_fp32.mnn";
        internal const long Bytes = 22609676;
        internal const string Hash = "7d307874be72d538e684d27f4c892c5b465586ce971b071dae8af083d77d5855";
        internal const string FullbandGraph = "fullsubnet_fullband_32_fp32.mnn";
        private const long FullbandBytes = 401608;
        private const string FullbandHash = "2f5932e260382d90a8a07acdab2e1561e8e1f3ba5db708f93a42150f060216ea";
        internal const string SubbandGraph = "fullsubnet_subband_32_fp32.mnn";
        private const long SubbandBytes = 401432;
        private const string SubbandHash = "7b54368460d4916a586e39338a190a585f8ed3ac61bf4ba99f0a529c0abc5405";
        private const long FullbandWeightBytes = 15248388;
        private const string FullbandWeightHash = "28d4faab0863f19ca87be3c2ed6a91eeca19976e79831d232ab6f663f3db19bf";
        private const long SubbandWeightBytes = 7302152;
        private const string SubbandWeightHash = "ec4dee316c985c88e6a4ae08cde37aa53f6b5539e998d65620ac36252db764aa";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNFullSubNetConfig.Read(directory);
                if (MNNFullSubNetPlusModel.Matches(config)) return MNNFullSubNetPlusModel.VerifyFiles(directory);
                if (config.repository != "Audio-WestlakeU/FullSubNet" || config.graph != Graph || config.sample_rate != 16000 ||
                    config.fft_size != 512 || config.hop_size != 256 || config.look_ahead != 2 ||
                    config.fullband_graph != FullbandGraph || config.subband_graph != SubbandGraph ||
                    config.normalization != "offline_laplace_norm" || config.recurrent_block_frames != 32) return false;
                return Verify(directory, Graph, Bytes, Hash) && Verify(directory, FullbandGraph, FullbandBytes, FullbandHash) &&
                    Verify(directory, SubbandGraph, SubbandBytes, SubbandHash) &&
                    Verify(directory, FullbandGraph + ".weight", FullbandWeightBytes, FullbandWeightHash) &&
                    Verify(directory, SubbandGraph + ".weight", SubbandWeightBytes, SubbandWeightHash);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { return false; }
        }
        internal static bool Verify(string directory, string graph, long bytes, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, graph)))
            using (var sha = SHA256.Create())
                return stream.Length == bytes && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").Equals(hash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
