using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNResembleDenoiserModel
    {
        internal const string Graph = "denoiser_fp32.mnn";
        private const string CoefficientHash = "b797e5e08a3472809ca6926a0f27a0423d1aa4274a15e2e51b0b07d12963da38";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var c = MNNResembleDenoiserConfig.Read(directory);
                if (c.repository != "ResembleAI/resemble-enhance" || c.revision != "4e3510ce4a8391159f665903544c5150bee7b2cb" ||
                    c.source_revision != "8e978149bfe8abab3eb77d965d579a111afdb0ff" || c.graph != Graph || c.causal ||
                    c.sample_rate != 44100 || c.fft_size != 1680 || c.hop_size != 420 || c.right_padding != 441 ||
                    c.minimum_samples != 400 || c.maximum_samples != 132300 || c.input != "features" || c.output != "spectrum" ||
                    c.coefficient_sha256 != CoefficientHash || c.analysis_window == null || c.analysis_window.Length != 1680) return false;
                var bytes = new byte[1680 * 4]; Buffer.BlockCopy(c.analysis_window, 0, bytes, 0, bytes.Length);
                using (var sha = SHA256.Create()) if (Hash(sha.ComputeHash(bytes)) != CoefficientHash) return false;
                using (var stream = File.OpenRead(Path.Combine(directory, Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 45817900 && Hash(sha.ComputeHash(stream)) == "b1ceb04e1b27adfe0bf025bd7b976f6ffd7753fbca86b71af0efb7d136945a83";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
        private static string Hash(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
