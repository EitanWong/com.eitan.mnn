using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNSortformerModel
    {
        internal const string Preencode = "preencode_fp32.mnn", Core = "core_fp32.mnn";

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNSortformerConfig.Read(directory);
                return config.repository == "nvidia/diar_streaming_sortformer_4spk-v2.1" &&
                    config.revision == "cd03eee90fbec18297ac31b8c21546e596b7f71c" &&
                    config.nemo_version == "2.6.0" && config.checkpoint_nemo_version == "2.6.0rc0" &&
                    config.preencode_graph == Preencode && config.core_graph == Core &&
                    config.sample_rate == 16000 && config.speakers == 4 && config.mel_bins == 128 &&
                    config.chunk_frames == 188 && config.cache_frames == 188 && config.maximum_samples == 960000 &&
                    config.analysis_window?.Length == 400 && config.mel_filters?.Length == 128 * 257 &&
                    MNNSpeakerSourceProfile.HasCoefficientHash("39c1e051dab92e282212b2350f8b0f00f1c8603584b787649e39b6f81d1269f0", config.analysis_window, config.mel_filters) &&
                    Matches(Path.Combine(directory, Preencode), 9026912, "604117b43c8a2cd9dfa3f14cbe034ae837f5ac7eb32c1ca84cbd11c02a696427") &&
                    Matches(Path.Combine(directory, Core), 483392324, "9c8a5a493092ffec6af5c5125f04ddb3d74ae2a4995b63ea9aad79f7f2e30c9c");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool Matches(string path, long length, string expected)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return stream.Length == length && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == expected;
        }
    }
}
