using System;
using System.Security.Cryptography;

namespace MNN.Unity
{
    // Immutable original-checkpoint identity; deployment JSON is untrusted until matched.
    internal sealed class MNNSpeakerSourceProfile
    {
        internal readonly string Name, Repository, Revision, Graph, Sha256;
        internal readonly string Frontend;
        internal readonly long Bytes;
        internal readonly bool PreferMetal;
        internal readonly MNNGpuTuningMode GpuTuning;
        internal readonly bool RecreateGpuSessionOnShapeChange;
        internal MNNSpeakerSourceProfile(string name, string repository, string revision, string graph, long bytes, string sha256, bool preferMetal, MNNGpuTuningMode gpuTuning = MNNGpuTuningMode.Fast, bool recreateGpuSessionOnShapeChange = false, string frontend = "3d-speaker-normalized-pcm-fbank80-cmn")
        {
            Name = name; Repository = repository; Revision = revision; Graph = graph;
            Bytes = bytes; Sha256 = sha256; PreferMetal = preferMetal;
            GpuTuning = gpuTuning;
            RecreateGpuSessionOnShapeChange = recreateGpuSessionOnShapeChange;
            Frontend = frontend;
        }
        internal bool Matches(MNNSpeakerModelConfig config) => config != null && config.repository == Repository &&
            config.source_revision == Revision && config.graph == Graph && string.IsNullOrEmpty(config.graph_fp16) &&
            config.frontend == Frontend && (Frontend != MNNSpeechBrainFeatures.Frontend || MNNSpeechBrainFeatures.HasSourceCoefficients(config)) &&
            (Frontend != MNNTitaNetFeatures.Frontend || MNNTitaNetFeatures.HasSourceCoefficients(config));

        internal static bool HasCoefficientHash(string expected, params float[][] parts)
        {
            int length = 0;
            foreach (var part in parts) length += part.Length * 4;
            var bytes = new byte[length]; int offset = 0;
            foreach (var part in parts)
            {
                Buffer.BlockCopy(part, 0, bytes, offset, part.Length * 4);
                offset += part.Length * 4;
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == expected;
        }
    }
}
