using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNLsEendModel
    {
        internal const string Encoder = "encoder_fp32.mnn", Decoder = "decoder_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNLsEendConfig.Read(directory);
                return config.repository == "Audio-WestlakeU/FS-EEND" &&
                    config.revision == "adcdde1327bc731cc4e718aa009b8d78317388e5" &&
                    config.checkpoint_sha256 == "9cbaceeae89c0fcca6bf7e38d47f33a0bb5fd6690081bacb72a24345594c9448" &&
                    config.encoder_graph == Encoder && config.decoder_graph == Decoder &&
                    config.sample_rate == 8000 && config.maximum_samples == 480000 && config.fft_size == 256 &&
                    config.frame_size == 200 && config.frame_shift == 80 && config.mel_bins == 23 &&
                    config.context_size == 7 && config.subsampling == 10 && config.delay_frames == 9 &&
                    config.output_columns == 9 && config.trained_maximum_speakers == 8 &&
                    config.threshold == .5f && config.median_frames == 11 &&
                    CoefficientsMatch(config) &&
                    Matches(Path.Combine(directory, Encoder), 25816876, "cee23687ffaa6bdc6e1cd969053e1853d9c40acb14d6658432195c6d30b53800") &&
                    Matches(Path.Combine(directory, Decoder), 18733656, "36d49b5efda6052065af4c49ae400034c24c7e8eaa54c8487a74846b8b72f2f3");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool CoefficientsMatch(MNNLsEendConfig config)
        {
            const string expected = "2bad444dd2b2107043923fd92a824a8dba7e31d146603faaa85a794efcc112e8";
            if (!BitConverter.IsLittleEndian || config.analysis_window?.Length != 200 || config.mel_filters?.Length != 23 * 129 ||
                config.coefficient_sha256 != expected) return false;
            var bytes = new byte[200 * 8 + 23 * 129 * 4];
            Buffer.BlockCopy(config.analysis_window, 0, bytes, 0, 200 * 8);
            Buffer.BlockCopy(config.mel_filters, 0, bytes, 200 * 8, 23 * 129 * 4);
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes)) == expected;
        }

        private static bool Matches(string path, long length, string expected)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return stream.Length == length && Hex(sha.ComputeHash(stream)) == expected;
        }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
