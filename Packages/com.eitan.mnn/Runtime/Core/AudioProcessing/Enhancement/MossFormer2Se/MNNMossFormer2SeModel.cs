using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNMossFormer2SeModel
    {
        internal const string Graph = "mossformer2_se_fp32.mnn";
        private const string CoefficientHash = "d69d3bf9479e33e075df2dc8952466cecc3c4be63b5e1160737fd1d19229e598";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNMossFormer2SeConfig.Read(directory);
                if (config.repository != "alibabasglab/MossFormer2_SE_48K" ||
                    config.revision != "eff8c97925c8bec812af707814b3e5d777fd4503" ||
                    config.source_revision != "6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61" || config.graph != Graph ||
                    config.sample_rate != 48000 || config.fft_size != 1920 || config.hop_size != 384 ||
                    config.mel_fft_size != 2048 || config.mel_bins != 60 || config.dither != 1 ||
                    config.dither_seed != 20261030 || config.coefficient_sha256 != CoefficientHash ||
                    config.analysis_window == null || config.analysis_window.Length != 1920 ||
                    config.mel_filters == null || config.mel_filters.Length != 60 * 1024) return false;
                var coefficients = new byte[(1920 + 60 * 1024) * 4];
                Buffer.BlockCopy(config.analysis_window, 0, coefficients, 0, 1920 * 4);
                Buffer.BlockCopy(config.mel_filters, 0, coefficients, 1920 * 4, 60 * 1024 * 4);
                using (var sha = SHA256.Create())
                    if (Hash(sha.ComputeHash(coefficients)) != CoefficientHash) return false;
                using (var stream = File.OpenRead(Path.Combine(directory, Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 227012912 && Hash(sha.ComputeHash(stream)) ==
                        "cd88eaf0d9ffc1a9df84d9f9018ad22ceba057c204d2b0b9fdd3f735b57e8fa9";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
        private static string Hash(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
