using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNMossFormerGanModel
    {
        internal const string Graph = "mossformergan_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNMossFormerGanConfig.Read(directory);
                if (config.repository != "alibabasglab/MossFormerGAN_SE_16K" ||
                    config.revision != "dcb1b20e1e44d435090072ab9b223c792ea1f624" ||
                    config.source_revision != "6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61" || config.graph != Graph ||
                    config.sample_rate != 16000 || config.fft_size != 400 || config.hop_size != 100 ||
                    config.power_compression != .3f || config.normalization != "rms_utterance") return false;
                return VerifyWindow(config.analysis_window) &&
                    Verify(Path.Combine(directory, Graph), 15774972, "70e4108553456d77e06bcb893a5830009a64908d071e15f9fd091844538137e3");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool VerifyWindow(float[] window)
        {
            if (window == null || window.Length != 400) return false;
            var bytes = new byte[1600]; Buffer.BlockCopy(window, 0, bytes, 0, bytes.Length);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() ==
                    "000b8c6d7b4a7814b932e8b09ff25fe16c5048dd01c5218ed1a613f2cd65bf2b";
        }

        private static bool Verify(string path, long length, string hash)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return stream.Length == length && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == hash;
        }
    }
}
