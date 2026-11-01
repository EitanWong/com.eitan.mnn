using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNFsEendModel
    {
        internal const string Graph = "model_fp32.mnn";
        internal static bool IsComplete(string directory) => ReadVerified(directory) != null;
        internal static MNNFsEendConfig ReadVerified(string directory)
        {
            try
            {
                var c = MNNFsEendConfig.Read(directory);
                return c.repository == "Audio-WestlakeU/FS-EEND" && c.revision == "adcdde1327bc731cc4e718aa009b8d78317388e5" &&
                    c.checkpoint_sha256 == "172fa6ee613476dac0daa5cfc14671faeed68a4c29d5db313768a7e857e3fa3d" &&
                    c.graph == Graph && c.input_transform == "logmel23" && c.execution == "full_recording_causal" &&
                    c.sample_rate == 8000 && c.maximum_samples == 480000 && c.fft_size == 256 && c.frame_size == 200 && c.frame_shift == 80 &&
                    c.mel_bins == 23 && c.context_size == 7 && c.subsampling == 10 && c.delay_frames == 9 && c.output_columns == 5 &&
                    c.trained_maximum_speakers == 4 && c.threshold == .5f && c.median_frames == 11 &&
                    c.coefficient_sha256 == "2bad444dd2b2107043923fd92a824a8dba7e31d146603faaa85a794efcc112e8" &&
                    Matches(directory, MNNFsEendConfig.CoefficientsFile, 13468, c.coefficient_sha256) &&
                    Matches(directory, Graph, 39670976, "77e706ed223fbf6b790daca598852aed8eb1a562710b511c874b445f22651721") ? c : null;
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
