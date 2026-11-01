using System;
using System.IO;
using System.Security.Cryptography;
namespace MNN.Unity
{
    internal static class MNNGtcrnModel
    {
        internal const string Repository = "Xiaobin-Rong/gtcrn";
        internal const string GraphCpu = "gtcrn_fp32.mnn", GraphAccelerator = GraphCpu;
        internal static bool IsComplete(string directory)
        {
            try
            {
                var c = MNNGtcrnConfig.Read(directory);
                return c.repository == Repository && c.sample_rate == 16000 && c.fft_size == 512 && c.hop_size == 256 && c.frequency_bins == 257 &&
                    c.graph_cpu == GraphCpu && c.graph_accelerator == GraphAccelerator &&
                    VerifyGraph(directory, GraphCpu, 450936, "ca178a31bba32778ae35ba7fb79871c055f6223d2da316daafc1d4bfcd4b8988");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return false; }
        }
        private static bool VerifyGraph(string directory, string graph, long bytes, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, graph)))
            using (var sha = SHA256.Create())
                return stream.Length == bytes && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
