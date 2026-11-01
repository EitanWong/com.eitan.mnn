using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNSpExPlusModel
    {
        internal const string ReferenceGraph = "spexplus_reference_fp32.mnn";
        internal const string AudioGraph = "spexplus_audio_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var c = MNNSpExPlusConfig.Read(directory);
                return c.repository == "alibabasglab/log_wsj0-2mix_speech_SpEx-plus_2spk" &&
                    c.revision == "2b99a144297ac4d074bb9dcc4ce9734a7e8924fd" && c.source_revision == "6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61" &&
                    c.reference_graph == ReferenceGraph && c.audio_graph == AudioGraph && c.sample_rate == 8000 && c.embedding_size == 256 &&
                    c.minimum_samples == 20 && c.maximum_samples == 32000 && c.minimum_reference_samples == 280 && c.maximum_reference_samples == 64000 &&
                    c.reference_input == "reference" && c.reference_output == "embedding" && c.audio_input == "mixture" && c.audio_output == "target" &&
                    c.normalization == "none; unpadded batch one; unnormalized reference embedding" && !c.causal &&
                    Matches(directory, ReferenceGraph, 6339172, "9fe15a21758bc520cb08a6f5faa45b7ccb778bf6426330d0344a6828c0273754") &&
                    Matches(directory, AudioGraph, 37848780, "595e7f1dfb3994a3427bc93e5add90ffae7a1c6c7ce936de5a02c675e85d204c");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool Matches(string directory, string name, long length, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, name)))
            using (var sha = SHA256.Create())
                return stream.Length == length && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == hash;
        }
    }
}
