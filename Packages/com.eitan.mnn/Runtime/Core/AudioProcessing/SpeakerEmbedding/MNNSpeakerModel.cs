using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNSpeakerModel
    {
        internal const string Repository = "Jeryuk/3dspeaker_speech_eres2netv2_sv_zh-cn_16k-common-MNN";
        internal const string OfficialRepository = "iic/speech_eres2netv2_sv_zh-cn_16k-common";
        internal const string SourceRevision = "1cf80d41fb3435bd3d8df185b5c423333b2db42a";
        internal const string OfficialGraph = "eres2netv2_fp32.mnn";
        internal const long OfficialBytes = 71441744;
        internal const string OfficialSha256 = "4a0e80fca60a8b7db593ae403a191dfe4d14aff22e5ff3c2029bb7795ddad3cd";
        private static readonly MNNSpeakerSourceProfile[] SourceProfiles =
        {
            new MNNSpeakerSourceProfile("ERes2NetV2", OfficialRepository, SourceRevision, OfficialGraph, OfficialBytes, OfficialSha256, true),
            new MNNSpeakerSourceProfile("CAM++", "iic/speech_campplus_sv_zh-cn_16k-common", "013c18884dffb82846cf472dd06dd4a08d97e2cd",
                "campplus_fp32.mnn", 27968516, "aec29dfac16447a90d87ad49e03b276112e725de02ce165fdb4f2faffc3ac461", false, MNNGpuTuningMode.None, true),
            new MNNSpeakerSourceProfile("ECAPA-TDNN", "speechbrain/spkrec-ecapa-voxceleb", "0f99f2d0ebe89ac095bcc5903c4dd8f72b367286",
                "ecapa_fp32.mnn", 83127584, "c15df46d51574f92dd8a6b05f6febd82dcdc5e642d40ba3c12ad43aa4fc0aeca", true,
                MNNGpuTuningMode.None, true, MNNSpeechBrainFeatures.Frontend),
            new MNNSpeakerSourceProfile("TitaNet", "nvidia/speakerverification_en_titanet_large", "0dc382f40121a5fbd34db10a2bb04d826c2be6a8",
                "titanet_fp32.mnn", 88671656, "0d8bef1371d13d266a7cd09d776cf1f3dcb52e520f0b1475fe7bdf72908739ac", false,
                MNNGpuTuningMode.None, true, MNNTitaNetFeatures.Frontend)
        };
        internal static MNNSpeakerSourceProfile FindSourceProfile(MNNSpeakerModelConfig config)
        {
            foreach (var profile in SourceProfiles)
                if (profile.Matches(config)) return profile;
            return null;
        }
        internal static bool IsOfficial(MNNSpeakerModelConfig config) => FindSourceProfile(config) != null;
        private static bool IsLegacy(MNNSpeakerModelConfig config) => config != null && config.repository == Repository &&
            config.graph == Graph && string.IsNullOrEmpty(config.source_revision) &&
            (string.IsNullOrEmpty(config.graph_fp16) || config.graph_fp16 == GraphFp16);
        internal const string Graph = "3dspeaker_speech_eres2netv2_sv_zh-cn_16k-common.mnn";
        internal const string GraphFp16 = "eres2netv2_fp16.mnn";
        internal const string Sha256 = "07fbdcd9107b088a6b430620cb81c6b3441fb589c2b08ecc106b5f568d266ec3";
        internal const long BytesFp16 = 18591708;
        internal const string Sha256Fp16 = "17be2b0099d81cee57420ce0b51793bd8629c33b7bc5d7c39a4c17338c3d98ed";
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, MNNSpeakerModelConfig.FileName)));
                if (!IsOfficial(config) && !IsLegacy(config)) return false;
                var profile = FindSourceProfile(config);
                return new FileInfo(Path.Combine(directory, config.graph)).Length == (profile?.Bytes ?? 18592284);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { return false; }
        }
        internal static void Verify(string directory)
        {
            if (!IsComplete(directory)) throw new InvalidDataException("Incomplete pinned speaker embedding profile.");
            var config = JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, MNNSpeakerModelConfig.FileName)));
            var profile = FindSourceProfile(config);
            if (profile != null)
            {
                if (Hash(Path.Combine(directory, profile.Graph)) != profile.Sha256)
                    throw new InvalidDataException("Original-checkpoint " + profile.Name + " graph failed SHA-256 verification.");
                return;
            }
            if (!IsLegacy(config) || Hash(Path.Combine(directory, Graph)) != Sha256)
                throw new InvalidDataException("Speaker profile does not match the pinned graph and protocol.");
            string fp16 = Path.Combine(directory, GraphFp16);
            if (File.Exists(fp16) && (new FileInfo(fp16).Length != BytesFp16 || Hash(fp16) != Sha256Fp16))
                throw new InvalidDataException("Speaker profile FP16 graph failed verification.");
        }
        internal static void Prepare(string directory)
        {
            string configPath = Path.Combine(directory, MNNSpeakerModelConfig.FileName);
            if (File.Exists(configPath))
            {
                var config = JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(configPath));
                if (config != null && (config.repository == OfficialRepository || !string.IsNullOrEmpty(config.source_revision)))
                {
                    Verify(directory);
                    return;
                }
            }
            string graph = Path.Combine(directory, Graph);
            if (!File.Exists(graph) || Hash(graph) != Sha256) throw new InvalidDataException("Speaker graph does not match the pinned 3D-Speaker export.");
            File.WriteAllText(Path.Combine(directory, MNNSpeakerModelConfig.FileName), JsonUtility.ToJson(new MNNSpeakerModelConfig(), true));
        }
        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
